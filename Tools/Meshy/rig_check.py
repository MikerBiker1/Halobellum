# rig_check.py
# Opens an FBX from Meshy in Blender, prints its bones, skinned mesh and clips, and renders poses to look at.
#   blender --background --factory-startup --python Tools/Meshy/rig_check.py -- <file.fbx> <output folder> <label> [--poses]
# With --poses: a picture of each clip, from the front and the side, at a quarter and three quarters of the way through.
import math
import os
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
path, out, label = os.path.abspath(args[0]), os.path.abspath(args[1]), args[2]
os.makedirs(out, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
bpy.context.view_layer.update()

arm = next((o for o in bpy.context.scene.objects if o.type == "ARMATURE"), None)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
print(f"CHECK objects: {[(o.name, o.type) for o in bpy.context.scene.objects]}")
for o in meshes:
    groups = {g.name for g in o.vertex_groups}
    weighted = sum(1 for v in o.data.vertices if len(v.groups))
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    mods = [m.type for m in o.modifiers]
    print(f"CHECK mesh {o.name}: {tris} tris, {len(o.data.vertices)} verts, {weighted} with weights, {len(groups)} groups, modifiers {mods}, materials {[m.name for m in o.data.materials]}")
    for m in o.data.materials:
        if m and m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE" and n.image:
                    print(f"CHECK   image {n.image.name} {tuple(n.image.size)} packed {bool(n.image.packed_file)} path {n.image.filepath}")
if arm:
    print(f"CHECK armature {arm.name}: scale {tuple(round(s, 4) for s in arm.scale)}, bones {len(arm.data.bones)}")
    for b in arm.data.bones:
        head = arm.matrix_world @ b.head_local
        print(f"CHECK   bone {b.name} parent {b.parent.name if b.parent else None} at {tuple(round(c, 3) for c in head)}")
for a in bpy.data.actions:
    print(f"CHECK action {a.name}: frames {tuple(a.frame_range)}, slots {[s.name_display for s in a.slots]}")
print(f"CHECK fps {bpy.context.scene.render.fps}")


def bounds():
    depsgraph = bpy.context.evaluated_depsgraph_get()
    points = []
    for o in meshes:
        evaluated = o.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        points += [evaluated.matrix_world @ v.co for v in mesh.vertices]
        evaluated.to_mesh_clear()
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return low, high


scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 12
scene.cycles.use_denoising = False
scene.render.resolution_x, scene.render.resolution_y = 420, 480
scene.render.image_settings.file_format = "PNG"
world = bpy.data.worlds.new("sky")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.75, 0.78, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
scene.world = world
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 3
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
scene.collection.objects.link(sun)
eye = bpy.data.objects.new("eye", bpy.data.cameras.new("eye"))
scene.collection.objects.link(eye)
scene.camera = eye


def shot(name, direction):
    low, high = bounds()
    centre = (low + high) / 2
    span = (high - low).length
    eye.location = centre + Vector(direction).normalized() * span * 1.5
    eye.rotation_euler = (centre - eye.location).to_track_quat("-Z", "Y").to_euler()
    eye.data.clip_start = span * 0.01
    eye.data.clip_end = span * 20
    scene.render.filepath = os.path.join(out, name + ".png")
    bpy.ops.render.render(write_still=True)
    print(f"CHECK picture {scene.render.filepath}, height {high.z - low.z:.3f}, from {tuple(round(c, 2) for c in low)} to {tuple(round(c, 2) for c in high)}")


if "--rest" in args or not bpy.data.actions:
    shot(label + "_rest_front", (0, -1, 0.1))
    shot(label + "_rest_side", (1, 0, 0.1))

if "--poses" in args and arm:
    for i, action in enumerate(bpy.data.actions):
        arm.animation_data_create()
        arm.animation_data.action = action
        if action.slots and arm.animation_data.action_slot is None:
            arm.animation_data.action_slot = action.slots[0]
        first, last = action.frame_range
        for share in (0.25, 0.75):
            scene.frame_set(int(first + (last - first) * share))
            bpy.context.view_layer.update()
            tag = f"{label}_{i}_{int(share * 100)}"
            shot(tag + "_front", (0, -1, 0.1))
            shot(tag + "_side", (1, 0, 0.1))
        print(f"CHECK posed {action.name}: {first} to {last}")
