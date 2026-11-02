# Saves Meshy's animation library (free) to Tools/Meshy/library.json and prints the balance.
import json, os, urllib.request
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
KEY = open(os.path.join(ROOT, "Art_Source", "meshy_key.txt"), encoding="utf-8").read().strip()
def get(path):
    r = urllib.request.Request("https://api.meshy.ai/openapi" + path, headers={"Authorization": "Bearer " + KEY})
    return json.load(urllib.request.urlopen(r, timeout=60))
print("balance", get("/v1/balance")["balance"])
lib = get("/v1/animations/library")
for a in lib: a.pop("preview_url_", None)
json.dump(lib, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "library.json"), "w"), indent=0)
print(len(lib), "animations")
words = ("zomb", "monster", "lurch", "creep", "claw", "stagger", "limp", "injured", "crawl", "beast", "punch", "swipe", "scratch", "hook", "strike", "melee", "smash", "slam", "attack")
for a in lib:
    t = (a["name"] + " " + a["key"]).lower()
    if any(w in t for w in words) or a["action_id"] in (98, 234, 8, 566):
        print(a["action_id"], a["name"], a["category"], a["sub_category"])
