# rig_finish.py
# Checks a unit's rig and clips from Meshy in Blender, and writes what Unity needs to Art_Source/Prepared/<Unit>_Rig/:
# <Unit>.fbx (Meshy's merged file as it is: mesh, rig and every clip), <Unit>.png and rig_report.json.
#   blender --background --factory-startup --python Tools/Meshy/rig_finish.py -- <Unit>
# Run with <Unit> <file> --measure-only to measure any FBX without writing anything.
import json
import math
import os
import re
import shutil
import sys

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
args = sys.argv[sys.argv.index("--") + 1:]
unit = args[0]
folder = os.path.join(ROOT, "Art_Source", "Prepared", unit + "_Rig")
measure_only = "--measure-only" in args
source = os.path.abspath(args[1]) if measure_only else os.path.join(folder, "meshy", "clips.fbx")
target = os.path.join(folder, unit + ".fbx")
ROLES = {
    "Grunt": {"moving": "Run_and_Shoot", "shooting": "Walk_Forward_While_Shooting", "dead": "Dead", "still": "walking_2"},
    "PureFormTank": {"moving": "Monster_Walk", "shooting": "Punch_Combo", "dead": "Dead", "still": "walking_2"},
    "Spartan": {"moving": "Run_and_Shoot", "shooting": "Walk_Forward_While_Shooting", "dead": "Dead", "still": "walking_2"},
    "Elite": {"moving": "Run_and_Shoot", "shooting": "Walk_Forward_While_Shooting", "dead": "Dead", "still": "walking_2"},
    "Hunter": {"moving": "Monster_Walk", "shooting": "Walk_Forward_While_Shooting", "dead": "Dead", "still": "walking_2"},
    "HunterCaptain": {"moving": "Monster_Walk", "shooting": "Walk_Forward_While_Shooting", "dead": "Dead", "still": "walking_2"},
    "GruntGoblin": {"moving": "Monster_Walk", "shooting": "Walk_Forward_While_Shooting", "dead": "Dead", "still": "walking_2"},
    "InfestedMarine": {"moving": "Monster_Walk", "shooting": "Punch_Combo", "dead": "Dead", "still": "walking_2"},
    "ODST": {"moving": "Run_and_Shoot", "shooting": "Walk_Forward_While_Shooting", "dead": "Dead", "still": "walking_2"},
    "Jackal": {"moving": "Run_and_Shoot", "shooting": "Walk_Forward_While_Shooting", "dead": "Dead", "still": "walking_2"},
    "BruteGrenadier": {"moving": "Run_and_Shoot", "shooting": "Walk_Forward_While_Shooting", "dead": "Dead", "still": "walking_2"},
    "CombatForm": {"moving": "Monster_Walk", "shooting": "Punch_Combo", "dead": "Dead", "still": "walking_2"},
    "Goliath": {"moving": "Monster_Walk", "shooting": "Walk_Forward_While_Shooting", "dead": "Dead", "still": "walking_2"},
}
REQUIRED = ("RightHand", "LeftHand", "LeftFoot", "RightFoot")

if not measure_only:
    shutil.copyfile(source, target)
    source = target
takes = sorted(set(m.group(1).decode() for m in re.finditer(rb"([\w| ]+)\x00\x01AnimStack", open(source, "rb").read())))
print(f"CHECK takes in the FBX: {takes}")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=source)
bpy.context.view_layer.update()
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
mesh = next(o for o in bpy.context.scene.objects if o.type == "MESH")
bones = [b.name for b in arm.data.bones]
missing = [b for b in REQUIRED if b not in bones]
tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
weighted = sum(1 for v in mesh.data.vertices if len(v.groups))
skinned = any(m.type == "ARMATURE" and m.object == arm for m in mesh.modifiers) and weighted == len(mesh.data.vertices)
print(f"CHECK {unit}: {len(bones)} bones, missing {missing}, {tris} triangles, skinned {skinned}, {weighted} of {len(mesh.data.vertices)} vertices weighted")
edges = np.array([e.vertices[:] for e in mesh.data.edges], dtype=np.int64)


def positions():
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh.evaluated_get(depsgraph)
    data = evaluated.to_mesh()
    points = np.empty(len(data.vertices) * 3, dtype=np.float64)
    data.vertices.foreach_get("co", points)
    evaluated.to_mesh_clear()
    world = np.array(mesh.matrix_world)
    return points.reshape(-1, 3) @ world[:3, :3].T + world[:3, 3]


def lengths(points):
    return np.linalg.norm(points[edges[:, 0]] - points[edges[:, 1]], axis=1)


def set_action(action):
    arm.animation_data_create()
    arm.animation_data.action = action
    if action.slots and arm.animation_data.action_slot is None:
        arm.animation_data.action_slot = action.slots[0]


scene = bpy.context.scene
arm.animation_data_create()
arm.animation_data.action = None
for b in arm.pose.bones:
    b.matrix_basis.identity()
bpy.context.view_layer.update()
rest = lengths(positions())
keep = rest > 1e-5
clips = {}
for action in bpy.data.actions:
    set_action(action)
    first, last = (int(f) for f in action.frame_range)
    worst, worst_share = 1.0, 0.0
    lowest = []
    for frame in range(first, last + 1, max(1, (last - first) // 12)):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        points = positions()
        ratio = lengths(points)[keep] / rest[keep]
        worst = max(worst, float(np.percentile(ratio, 99.9)))
        worst_share = max(worst_share, float(np.mean(ratio > 1.5)))
        lowest.append(float(points[:, 2].min()))
    scene.frame_set(first)
    bpy.context.view_layer.update()
    start = positions()
    scene.frame_set(last)
    bpy.context.view_layer.update()
    end = positions()
    height = float(np.ptp(start[:, 2]))
    seam = np.linalg.norm((end - end.mean(axis=0)) - (start - start.mean(axis=0)), axis=1)
    clips[action.name.split("|")[-1]] = {
        "frames": last - first + 1,
        "worst_edge_stretch": round(worst, 2),
        "share_of_edges_stretched_over_1_5": round(worst_share, 4),
        "loop_seam_mean_gap": round(float(seam.mean()), 3),
        "loop_seam_max_gap": round(float(seam.max()), 3),
        "lowest_point_first": round(lowest[0], 3),
        "lowest_point_last": round(lowest[-1], 3),
        "body_height": round(height, 3),
    }
    print(f"CHECK clip {action.name}: {clips[action.name.split('|')[-1]]}")

if measure_only:
    sys.exit(0)

roles = ROLES[unit]
for role, clip in roles.items():
    if clip not in takes:
        raise SystemExit(f"{unit}: no clip {clip} for {role}; the FBX has {takes}")

# The picture: the middle of the moving clip, bigger than the earlier checks, from the front and the side.
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 16
scene.cycles.use_denoising = False
scene.render.resolution_x, scene.render.resolution_y = 560, 640
scene.render.image_settings.file_format = "PNG"
world = bpy.data.worlds.new("sky")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.75, 0.78, 1)
scene.world = world
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 3
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
scene.collection.objects.link(sun)
eye = bpy.data.objects.new("eye", bpy.data.cameras.new("eye"))
scene.collection.objects.link(eye)
scene.camera = eye
moving = next(a for a in bpy.data.actions if a.name.split("|")[-1] == roles["moving"])
set_action(moving)
scene.frame_set(int(sum(moving.frame_range) / 2))
bpy.context.view_layer.update()
points = positions()
low, high = Vector(points.min(axis=0)), Vector(points.max(axis=0))
centre, span = (low + high) / 2, (high - low).length
for name, direction in (("front", (0, -1, 0.1)), ("side", (1, 0, 0.1)), ("quarter", (0.8, -1, 0.4))):
    eye.location = centre + Vector(direction).normalized() * span * 1.4
    eye.rotation_euler = (centre - eye.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(folder, "shots", f"moving_middle_{name}.png")
    bpy.ops.render.render(write_still=True)
    print(f"CHECK picture {scene.render.filepath}")

state = json.load(open(os.path.join(HERE, "rig_state.json"), encoding="utf-8"))[unit]
report = {
    "unit": unit,
    "fbx": unit + ".fbx",
    "texture": unit + ".png",
    "clips": roles,
    "clip_names_in_fbx": takes,
    "action_ids": state.get("actions"),
    "bones": bones,
    "triangles": tris,
    "skinned": skinned,
    "credits_spent": state["credits"],
    "rig_attempts": len(state["attempts"]),
    "measured": clips,
}
with open(os.path.join(folder, "rig_report.json"), "w", encoding="utf-8") as f:
    json.dump(report, f, indent=1)
print(f"CHECK wrote rig_report.json for {unit}")
