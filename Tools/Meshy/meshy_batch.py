# meshy_batch.py
# Generates units through Meshy's API (M9), so nobody has to click through the website for each one.
# Infantry: text to 3D (preview, then texture), auto-rig, then the animations the owner chose for the Marine.
# Vehicles: text to 3D, then Auto Split into named parts, so that their tracks and turret can move.
# Results go to Art_Source/Meshy/<unit>/. The key is read from Art_Source/meshy_key.txt (ignored by Git).
# Every task id is saved to state.json as soon as it exists, so running this again resumes the work
# instead of paying for it twice.
#   python Tools/Meshy/meshy_batch.py units.json
# Needs only the Python standard library.

import json
import os
import sys
import threading
import time
import urllib.error
import urllib.request

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Art_Source", "Meshy")
API = "https://api.meshy.ai/openapi"
KEY = open(os.path.join(ROOT, "Art_Source", "meshy_key.txt"), encoding="utf-8").read().strip()
# The animations in the owner's Marine: Run and Shoot, Walk Forward While Shooting, Dead, Walking.
ACTIONS = [98, 234, 8, 566]
LOCK = threading.Lock()


def log(unit, message):
    print(f"{time.strftime('%H:%M:%S')} {unit}: {message}", flush=True)


def call(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(API + path, data=data, method=method, headers={"Authorization": "Bearer " + KEY, "Content-Type": "application/json"})
    for attempt in range(6):
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                return json.load(response)
        except urllib.error.HTTPError as error:
            text = error.read().decode(errors="replace")[:400]
            if error.code in (429, 500, 502, 503, 504) and attempt < 5:
                time.sleep(15 * (attempt + 1))
                continue
            raise RuntimeError(f"{method} {path}: HTTP {error.code}: {text}")
        except urllib.error.URLError:
            if attempt == 5:
                raise
            time.sleep(15 * (attempt + 1))


def wait(unit, label, path):
    while True:
        task = call("GET", path)
        status = task.get("status")
        if status == "SUCCEEDED":
            log(unit, f"{label} done, {task.get('consumed_credits')} credits")
            return task
        if status in ("FAILED", "CANCELED", "EXPIRED"):
            error = task.get("task_error") or task.get("error") or {}
            raise RuntimeError(f"{label} {status}: {error}")
        time.sleep(10)


def download(url, path):
    request = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(request, timeout=300) as response, open(path, "wb") as f:
        f.write(response.read())


def step(state, save, unit, name, path, body, get):
    """Starts a task once (its id is saved at once), then waits for it."""
    if name not in state:
        state[name] = call("POST", path, body)["result"]
        save()
        log(unit, f"{name} started")
    return wait(unit, name, get.format(state[name]))


def generate(unit, folder, state, save):
    """Text to 3D: untextured mesh, then textures. Saves a picture to judge it by."""
    preview = {"mode": "preview", "prompt": unit["prompt"], "art_style": "realistic", "should_remesh": True,
               "target_polycount": unit["polycount"], "topology": "triangle"}
    if unit.get("pose"):
        preview["pose_mode"] = unit["pose"]
    step(state, save, unit["name"], "preview", "/v2/text-to-3d", preview, "/v2/text-to-3d/{}")
    refined = step(state, save, unit["name"], "refine", "/v2/text-to-3d",
                   {"mode": "refine", "preview_task_id": state["preview"], "texture_resolution": "2k", "enable_pbr": False, "target_formats": ["glb", "fbx"]},
                   "/v2/text-to-3d/{}")
    if refined.get("thumbnail_url"):
        download(refined["thumbnail_url"], os.path.join(folder, "thumbnail.png"))
    textures = refined.get("texture_urls") or []
    if textures and textures[0].get("base_color"):
        download(textures[0]["base_color"], os.path.join(folder, unit["name"] + "_texture.png"))
    return refined


def infantry(unit, folder, state, save):
    generate(unit, folder, state, save)
    rig = step(state, save, unit["name"], "rig", "/v1/rigging",
               {"input_task_id": state["refine"], "animation_type": "biped", "height_meters": unit["height"]}, "/v1/rigging/{}")
    log(unit["name"], "rig result fields: " + ", ".join(sorted((rig.get("result") or {}).keys())))
    animated = step(state, save, unit["name"], "animations", "/v1/animations",
                    {"rig_task_id": state["rig"], "action_ids": ACTIONS}, "/v1/animations/{}")
    download(animated["result"]["animation_fbx_url"], os.path.join(folder, unit["name"] + ".fbx"))


def vehicle(unit, folder, state, save):
    refined = generate(unit, folder, state, save)
    download(refined["model_urls"]["fbx"], os.path.join(folder, unit["name"] + "_whole.fbx"))
    split = step(state, save, unit["name"], "split", "/v1/print/split",
                 {"input_task_id": state["refine"], "mode": "by_parts", "prompt": unit["parts"], "target_formats": ["fbx", "glb"],
                  "layout": "assembled", "keep_texture": True}, "/v1/print/split/{}")
    log(unit["name"], "split fields: " + ", ".join(sorted(split.keys())))
    urls = split.get("model_urls") or {}
    for form in ("fbx", "glb"):
        if urls.get(form):
            download(urls[form], os.path.join(folder, f"{unit['name']}_parts.{form}"))
    for i, texture in enumerate(split.get("texture_urls") or []):
        if texture.get("base_color"):
            download(texture["base_color"], os.path.join(folder, f"{unit['name']}_parts_texture{i}.png"))


def work(unit, states, save):
    name = unit["name"]
    folder = os.path.join(OUT, name)
    os.makedirs(folder, exist_ok=True)
    state = states.setdefault(name, {})
    try:
        (vehicle if unit.get("parts") else infantry)(unit, folder, state, save)
        log(name, "ALL DONE")
    except Exception as error:  # One failed unit shouldn't stop the others.
        log(name, f"FAILED: {error}")


def main():
    units = json.load(open(sys.argv[1], encoding="utf-8"))
    os.makedirs(OUT, exist_ok=True)
    state_path = os.path.join(OUT, "state.json")
    states = json.load(open(state_path, encoding="utf-8")) if os.path.exists(state_path) else {}

    def save():
        with LOCK:
            with open(state_path, "w", encoding="utf-8") as f:
                json.dump(states, f, indent=1)

    before = call("GET", "/v1/balance")["balance"]
    print(f"Credits before: {before}", flush=True)
    threads = [threading.Thread(target=work, args=(unit, states, save)) for unit in units]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()
    after = call("GET", "/v1/balance")["balance"]
    print(f"Credits after: {after}, spent {before - after}", flush=True)


if __name__ == "__main__":
    main()
