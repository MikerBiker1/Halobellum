# meshy_rig.py
# Sends a prepared model (Art_Source/Prepared/<Unit>_Rig/input.glb, from rig_inputs.py) to Meshy to be rigged, and asks for
# animation clips for a rig that came out well. Each task id goes in Tools/Meshy/rig_state.json before it is waited for, so a
# second run resumes instead of paying twice. The key is read from Art_Source/meshy_key.txt and never printed.
#   python Tools/Meshy/meshy_rig.py balance
#   python Tools/Meshy/meshy_rig.py rig <Unit>                  rigs it (at most 2 attempts), saves the rig and Meshy's free walk
#   python Tools/Meshy/meshy_rig.py animate <Unit> 98,234,8,566 buys these clips in one merged FBX
#   python Tools/Meshy/meshy_rig.py animate <Unit> 98,234,8,566,128 --again   buys a new set in place of the clips already bought
#     (Meshy merges only the clips of one request into one FBX, so a clip added later means buying the set again; the old task is kept
#     under "earlier": the Brute Grenadier's swing, 2026-11-07)
# Needs only the Python standard library.

import base64
import json
import os
import sys
import time
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
PREPARED = os.environ.get("HB_PREPARED") or os.path.join(ROOT, "Art_Source", "Prepared")  # As in model_tools.py: a worktree can prepare outside the shared Art_Source.
API = "https://api.meshy.ai/openapi"
KEY = open(os.path.join(ROOT, "Art_Source", "meshy_key.txt"), encoding="utf-8").read().strip()
STATE_PATH = os.path.join(HERE, "rig_state.json")
HEIGHTS = {"Grunt": 1.3, "StalkerForm": 2.4, "PureFormRanged": 2.9, "PureFormTank": 3.8, "Spartan": 2.3, "Elite": 2.6, "Hunter": 3.6, "HunterCaptain": 3.8, "GruntGoblin": 4.4, "InfestedMarine": 2.0, "ODST": 2.0, "Jackal": 2.0, "BruteGrenadier": 2.8, "CombatForm": 2.6, "Goliath": 6.0, "FlamethrowerTrooper": 2.0}
MAX_ATTEMPTS = 2
BUDGET = 300


class Refused(Exception):
    def __init__(self, code, text):
        super().__init__(f"HTTP {code}: {text}")
        self.code = code


def load():
    return json.load(open(STATE_PATH, encoding="utf-8")) if os.path.exists(STATE_PATH) else {}


def save(state):
    with open(STATE_PATH, "w", encoding="utf-8") as f:
        json.dump(state, f, indent=1)


def get(path):
    request = urllib.request.Request(API + path, headers={"Authorization": "Bearer " + KEY})
    for attempt in range(6):
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                return json.load(response)
        except urllib.error.HTTPError as error:
            if error.code in (429, 500, 502, 503, 504) and attempt < 5:
                time.sleep(15 * (attempt + 1))
                continue
            raise Refused(error.code, error.read().decode(errors="replace")[:400])
        except urllib.error.URLError:
            if attempt == 5:
                raise
            time.sleep(15 * (attempt + 1))


def post(path, body):
    """Sends once, retrying only when Meshy says it took nothing (429). A timeout may have been paid for, so it stops the run."""
    data = json.dumps(body).encode()
    request = urllib.request.Request(API + path, data=data, method="POST", headers={"Authorization": "Bearer " + KEY, "Content-Type": "application/json"})
    for attempt in range(4):
        try:
            with urllib.request.urlopen(request, timeout=300) as response:
                return json.load(response)["result"]
        except urllib.error.HTTPError as error:
            if error.code == 429 and attempt < 3:
                time.sleep(30)
                continue
            raise Refused(error.code, error.read().decode(errors="replace")[:400])


def wait(path):
    while True:
        task = get(path)
        if task.get("status") in ("SUCCEEDED", "FAILED", "CANCELED", "EXPIRED"):
            return task
        time.sleep(5)


def download(url, path):
    request = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(request, timeout=300) as response, open(path, "wb") as f:
        f.write(response.read())
    print(f"  saved {os.path.basename(path)}, {os.path.getsize(path)} bytes", flush=True)


def check_budget(state, cost):
    spent = sum(u.get("credits", 0) for u in state.values())
    if spent + cost > BUDGET:
        raise SystemExit(f"Would go over the budget: {spent} spent, {cost} more, limit {BUDGET}")


def rig(unit):
    state = load()
    record = state.setdefault(unit, {"attempts": [], "credits": 0})
    folder = os.path.join(PREPARED, unit + "_Rig")
    if not record.get("rig"):
        running = next((a for a in record["attempts"] if a.get("id") and not a.get("finished")), None)
        if not running:
            if len(record["attempts"]) >= MAX_ATTEMPTS:
                raise SystemExit(f"{unit}: already {MAX_ATTEMPTS} attempts, stopping")
            check_budget(state, 5)
            attempt = {"started": time.strftime("%H:%M:%S")}
            record["attempts"].append(attempt)
            save(state)  # Counted before it is sent: if the call dies halfway, that still used up an attempt.
            source = open(os.path.join(folder, "input.glb"), "rb").read()
            body = {"model_url": "data:application/octet-stream;base64," + base64.b64encode(source).decode(), "height_meters": HEIGHTS[unit]}
            try:
                attempt["id"] = post("/v1/rigging", body)
                save(state)
                running = attempt
            except Refused as error:
                attempt.update(finished=True, refused=str(error))
                save(state)
                print(f"{unit}: refused, attempt {len(record['attempts'])}: {error}", flush=True)
                return False
        task = wait("/v1/rigging/" + running["id"])
        running["finished"] = True
        running["status"] = task["status"]
        running["credits"] = task.get("consumed_credits") or 0
        record["credits"] += running["credits"]
        if task["status"] != "SUCCEEDED":
            running["error"] = (task.get("task_error") or {}).get("message", "")
            save(state)
            print(f"{unit}: {task['status']}, attempt {len(record['attempts'])}: {running['error']}", flush=True)
            return False
        record["rig"] = running["id"]
        save(state)
        print(f"{unit}: rigged, {running['credits']} credits", flush=True)
    task = get("/v1/rigging/" + record["rig"])
    result = task["result"]
    os.makedirs(os.path.join(folder, "meshy"), exist_ok=True)
    download(result["rigged_character_fbx_url"], os.path.join(folder, "meshy", "rigged.fbx"))
    download(result["rigged_character_glb_url"], os.path.join(folder, "meshy", "rigged.glb"))
    walking = (result.get("basic_animations") or {}).get("walking_fbx_url")
    if walking:
        download(walking, os.path.join(folder, "meshy", "free_walk.fbx"))
    return True


def animate(unit, action_ids, again=False):
    state = load()
    record = state[unit]
    folder = os.path.join(PREPARED, unit + "_Rig")
    if again and record.get("animation") and record.get("actions") != action_ids:
        record.setdefault("earlier", []).append({k: record.pop(k) for k in ("animation", "actions", "animation_credits") if k in record})
        save(state)
    if not record.get("animation"):
        check_budget(state, 3 * len(action_ids))
        record["animation"] = post("/v1/animations", {"rig_task_id": record["rig"], "action_ids": action_ids})
        record["actions"] = action_ids
        save(state)
    elif record.get("actions") != action_ids:
        raise SystemExit(f"{unit}: clips {record.get('actions')} were already bought; asked for {action_ids}")
    task = wait("/v1/animations/" + record["animation"])
    if task["status"] != "SUCCEEDED":
        print(f"{unit}: animation {task['status']}: {task.get('task_error')}", flush=True)
        record.pop("animation")
        record.pop("actions")
        save(state)
        return
    if not record.get("animation_credits"):
        record["animation_credits"] = task.get("consumed_credits") or 0
        record["credits"] += record["animation_credits"]
        save(state)
    print(f"{unit}: animated, {record['animation_credits']} credits, result fields: {', '.join(sorted(k for k, v in task['result'].items() if v))}", flush=True)
    os.makedirs(os.path.join(folder, "meshy"), exist_ok=True)
    download(task["result"]["animation_fbx_url"], os.path.join(folder, "meshy", "clips.fbx"))


def main():
    command = sys.argv[1]
    if command == "balance":
        print("Credits:", get("/v1/balance")["balance"])
    elif command == "rig":
        rig(sys.argv[2])
    elif command == "animate":
        animate(sys.argv[2], [int(x) for x in sys.argv[3].split(",")], again="--again" in sys.argv)


if __name__ == "__main__":
    main()
