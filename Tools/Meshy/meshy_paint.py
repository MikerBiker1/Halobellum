# meshy_paint.py
# Has Meshy paint a model that came with no colours (Art_Source/Prepared/_Paint/<name>/input.glb, from texture_inputs.py), keeping its
# shape. The task id goes in paint_state.json before it is waited for, so a second run resumes instead of paying twice. The key is read
# from Art_Source/meshy_key.txt and never printed.
#   python Tools/Meshy/meshy_paint.py <name> "<what it should look like>"
# Writes painted.glb next to input.glb. Needs only the Python standard library.
import base64
import json
import os
import sys

from meshy_rig import PREPARED, download, get, post, wait

HERE = os.path.dirname(os.path.abspath(__file__))
STATE_PATH = os.path.join(HERE, "paint_state.json")


def main():
    name, prompt = sys.argv[1], sys.argv[2]
    state = json.load(open(STATE_PATH, encoding="utf-8")) if os.path.exists(STATE_PATH) else {}
    folder = os.path.join(PREPARED, "_Paint", name)
    if name not in state:
        source = open(os.path.join(folder, "input.glb"), "rb").read()
        body = {"model_url": "data:application/octet-stream;base64," + base64.b64encode(source).decode(), "text_style_prompt": prompt,
                "enable_original_uv": False, "enable_pbr": False}
        state[name] = {"id": post("/v1/retexture", body), "prompt": prompt}
        with open(STATE_PATH, "w", encoding="utf-8") as f:
            json.dump(state, f, indent=1)
    task = wait("/v1/retexture/" + state[name]["id"])
    print(f"{name}: {task['status']}, {task.get('consumed_credits')} credits", flush=True)
    if task["status"] != "SUCCEEDED":
        print(task.get("task_error"), flush=True)
        return
    download(task["model_urls"]["glb"], os.path.join(folder, "painted.glb"))


if __name__ == "__main__":
    main()
