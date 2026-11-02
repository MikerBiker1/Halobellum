# bring_in.py
# Moves prepared models into the project and, after Unity has built them in the batch copy, brings what Unity made back.
#   python Tools/Art/bring_in.py copy <names...>   Art_Source/Prepared/<name> into Art/Imported/Models/<name>
#   python Tools/Art/bring_in.py back              Metas, prefabs, materials, baked bodies and unit fields from the batch copy
# The batch copy mirrors the project, so anything Unity makes must come back or it is lost on the next run.

import os
import re
import shutil
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "Art_Source")
PREPARED = os.path.join(SOURCE, "Prepared")
PROJECT = os.path.join(ROOT, "Assets", "_Project")
BATCH = r"E:\AI\_hbci\Halobellum\Assets\_Project"
SOURCE_NOTE = "Source: found by the project owner on Sketchfab, file {source} in Art_Source.\nPrepared by Tools/Art/units/{name}.py (decision D45).\nThe repository is private and must stay private.\n"


def copy(names):
    # Named one by one: a rigged try and a static model of one unit share a target, and which of them is used is a choice.
    for name in names:
        source = os.path.join(PREPARED, name)
        # A rigged model's folder ends in _Rig, and it takes the unit's own name in the project.
        target = os.path.join(PROJECT, "Art", "Imported", "Models", name[:-4] if name.endswith("_Rig") else name)
        os.makedirs(target, exist_ok=True)
        for f in os.listdir(source):
            if f.lower().endswith((".fbx", ".png", ".json")) and f not in ("report.json", "rig_report.json"):
                shutil.copy(os.path.join(source, f), os.path.join(target, f))
        report_path = os.path.join(source, "report.json")
        found = re.search(r'"source": "([^"]+)"', open(report_path, encoding="utf-8").read()) if os.path.exists(report_path) else None
        origin = found.group(1) if found else name[:-4] + "/" + name[:-4].lower() + ".glb, rigged through Meshy by Tools/Meshy/meshy_rig.py"
        note = SOURCE_NOTE.format(source=origin, name=name)
        if name.endswith("_Rig"):
            note = note.replace(f"Tools/Art/units/{name}.py", "Tools/Meshy/rig_inputs.py and meshy_rig.py")
        link = os.path.join(SOURCE, origin.split("/")[0], "Source.txt")
        if os.path.exists(link):
            note = "Link: " + open(link, encoding="utf-8").read().strip() + "\n" + note
        with open(os.path.join(target, "SOURCE.txt"), "w", encoding="utf-8", newline="\n") as f:
            f.write(note)
        print("copied", name)


def copy_changed(source_folder, target_folder, keep):
    for root, dirs, files in os.walk(source_folder):
        dirs[:] = [d for d in dirs if d != "Materials" and not d.endswith(".fbm")]
        target = root.replace(source_folder, target_folder)
        os.makedirs(target, exist_ok=True)
        for f in files:
            if f == "Materials.meta" or ".fbm" in f or not keep(f):
                continue
            shutil.copy(os.path.join(root, f), os.path.join(target, f))


def back():
    models = os.path.join("Art", "Imported", "Models")
    # Of the models' files, only Unity's import settings and the materials it made come back.
    copy_changed(os.path.join(BATCH, models), os.path.join(PROJECT, models), lambda f: f.endswith((".meta", ".mat")))
    copy_changed(os.path.join(BATCH, "Prefabs", "Units"), os.path.join(PROJECT, "Prefabs", "Units"), lambda f: True)
    copy_changed(os.path.join(BATCH, "Resources", "BakedBodies"), os.path.join(PROJECT, "Resources", "BakedBodies"), lambda f: True)
    # Unit assets: only the fields the builder sets, so that the files don't change otherwise.
    fields = ("prefab", "keepsOwnMaterials", "bakedBody")
    units = os.path.join("Data", "Units")
    for root, _, files in os.walk(os.path.join(BATCH, units)):
        for f in files:
            if not f.endswith(".asset"):
                continue
            built = open(os.path.join(root, f), encoding="utf-8").read()
            path = os.path.join(root.replace(BATCH, PROJECT), f)
            text = open(path, encoding="utf-8", newline="").read()
            for field in fields:
                found = re.search(rf"^  {field}: .*$", built, re.M)
                if found:
                    text = re.sub(rf"^  {field}: .*?(\r?)$", lambda m: found.group(0).rstrip("\r") + m.group(1), text, flags=re.M)
            open(path, "w", encoding="utf-8", newline="").write(text)
    print("brought back")


if __name__ == "__main__":
    if sys.argv[1] == "copy":
        copy(sys.argv[2:])
    else:
        back()
