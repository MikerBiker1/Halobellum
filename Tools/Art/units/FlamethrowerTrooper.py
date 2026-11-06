# The Flamethrower Trooper, from the owner's Meshy_AI_Flamethrower_trooper.glb: a soldier caught mid-stride, crouched, with the flamethrower in
# both hands and its tanks on his back. Meshy rigged it (Tools/Meshy/rig_inputs.py and meshy_rig.py, kept in Prepared/FlamethrowerTrooper_Rig),
# but Meshy's clips are made for a figure standing straight with its arms out: on this crouched one they bent him double and tore the gun apart.
# So it keeps Meshy's rig and weights, and its clips are made here, as the Infested Marine's are, around the pose the file shows. The arms never
# move against the chest, so the gun, which is part of the body, stays whole: he runs, walks forward spraying, and pitches forward dead.
# Writes Prepared/FlamethrowerTrooper/: FlamethrowerTrooper.fbx, FlamethrowerTrooper.png, report.json and pictures.
# Clips by role: moving Run, shooting Spray, dead Dead, still Still. His shots start at the front of the gun (BakedBodyDefinition.muzzleAtFront).
#   python Tools/Meshy/meshy_rig.py rig FlamethrowerTrooper     (downloads the rig again, already paid for, if Prepared lacks it)
#   blender --background --factory-startup --python Tools/Art/units/FlamethrowerTrooper.py
# Height 2 m, feet on the origin, facing -Y. Character's left is +X.
import json
import math
import os
import shutil
import sys

import bpy
from mathutils import Euler, Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

SOURCE = "Meshy/Meshy_AI_Flamethrower_trooper.glb"
RIG = os.path.join(mt.PREPARED, "FlamethrowerTrooper_Rig", "meshy", "rigged.glb")
HEIGHT = 2.0
FPS = 30

folder = os.path.join(mt.PREPARED, "FlamethrowerTrooper")
shutil.rmtree(folder, ignore_errors=True)
os.makedirs(os.path.join(folder, "shots"))
mt._state.update(name="FlamethrowerTrooper", folder=folder, source=SOURCE)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=RIG)
bpy.context.view_layer.update()
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
parts = sorted([o for o in bpy.context.scene.objects if o.type == "MESH"], key=lambda o: -len(o.data.polygons))
for o in parts[1:]:
    print(f"REPORT removed {o.name}, {len(o.data.polygons)} faces")  # Meshy's file brings a small stray mesh beside the body.
    bpy.data.objects.remove(o)
parts = parts[:1]
image = mt._input_image(parts[0].data.materials[0], "Base Color")
print(f"REPORT texture {image.name} {tuple(image.size)}, {len(arm.data.bones)} bones")

# Meshy's file is in centimetres under a scaled parent: everything is applied, in the rest pose, which is the pose the file shows.
mt._select(parts + [arm])
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
for o in list(bpy.context.scene.objects):
    if o.type == "EMPTY":
        bpy.data.objects.remove(o)
for o in parts:
    mt._select([o])
    for modifier in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=modifier.name)
mt._select(parts + [arm])
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mesh = parts[0]
low, high = mt._bounds([mesh])
points = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
feet = [p for p in points if p.z < low.z + (high.z - low.z) * 0.1]
middle = Vector(((min(p.x for p in feet) + max(p.x for p in feet)) / 2, (min(p.y for p in feet) + max(p.y for p in feet)) / 2, low.z))
factor = HEIGHT / (high.z - low.z)
for o in (mesh, arm):
    o.location = -middle * factor
    o.scale = (factor, factor, factor)
bpy.context.view_layer.update()
mt._select([mesh, arm])
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mt.report("standing")
mt._save_scaled(image, os.path.join(folder, "FlamethrowerTrooper.png"), 1024)
mesh.parent = arm
modifier = mesh.modifiers.new("Rig", "ARMATURE")
modifier.object = arm
arm.name = "Rig"
arm.data.name = "Rig"
print(f"REPORT bones {sorted(b.name for b in arm.data.bones)}")


def world_turn(bone_name, degrees):
    """A turn given in the model's axes (x across, y front to back, z up), as a pose bone's local rotation."""
    rest = arm.data.bones[bone_name].matrix_local.to_3x3().to_quaternion()
    turn = Euler([math.radians(d) for d in degrees], "XYZ").to_quaternion()
    return rest.inverted() @ turn @ rest


def key(frame, pose):
    """pose: bone name to degrees; "shift" moves the whole body in metres."""
    for bone in arm.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = world_turn(bone.name, pose.get(bone.name, (0, 0, 0)))
        bone.keyframe_insert("rotation_quaternion", frame=frame)
    root = arm.pose.bones["Hips"]
    rest = arm.data.bones["Hips"].matrix_local.to_3x3()
    root.location = rest.inverted() @ Vector(pose.get("shift", (0, 0, 0)))
    root.keyframe_insert("location", frame=frame)


def lowest_point():
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh.evaluated_get(depsgraph)
    data = evaluated.to_mesh()
    result = min((mesh.matrix_world @ v.co).z for v in data.vertices)
    evaluated.to_mesh_clear()
    return result


def make(name, frames, pose_at, grounded=True):
    """Each frame is keyed twice: the second time lifted or lowered so that its lowest point is on the ground."""
    action = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = action
    for frame in range(frames + 1):
        pose = pose_at(frame / frames)
        key(frame + 1, pose)
        if grounded:
            bpy.context.scene.frame_set(frame + 1)
            bpy.context.view_layer.update()
            x, y, z = pose.get("shift", (0, 0, 0))
            pose["shift"] = (x, y, z - lowest_point())
            key(frame + 1, pose)
    action.use_fake_user = True
    action.use_frame_range = True
    action.frame_start = 1
    action.frame_end = frames + 1
    print(f"REPORT clip {name}, {frames} frames")


def sin(t):
    return math.sin(t * math.pi * 2)


def cos(t):
    return math.cos(t * math.pi * 2)


def add(pose, bone, x=0.0, y=0.0, z=0.0):
    old = pose.get(bone, (0, 0, 0))
    pose[bone] = (old[0] + x, old[1] + y, old[2] + z)


# In the file his left thigh reaches 32 degrees forward and his right one 8 degrees back, his right shin far back behind him. These bring
# both legs to the middle of his stride, from where they swing in turn.
LEFT_MIDDLE, RIGHT_MIDDLE = 20.0, -18.0


def leg(pose, side, phase, swing, lift):
    middle = LEFT_MIDDLE if side == "Left" else RIGHT_MIDDLE
    thigh = middle - swing * sin(phase)
    knee = lift * max(0.0, cos(phase)) ** 1.5 - (0.0 if side == "Left" else 14.0)
    add(pose, side + "UpLeg", thigh)
    add(pose, side + "Leg", knee)
    add(pose, side + "Foot", -(thigh - middle + knee) * 0.6)


def run(t):
    # 0.7 seconds for two strides, for his 1.75 m/s. The upper body sways with the hips, and the gun with it, all in one piece.
    pose = {}
    s = sin(t)
    leg(pose, "Left", t, 24, 30)
    leg(pose, "Right", t + 0.5, 24, 30)
    add(pose, "Hips", 2 * sin(t * 2), 0, 5 * s)
    add(pose, "neck", 0, 0, -4 * s)
    return pose


def spray(t):
    # Walking forward while the flames pour out: a shorter, slower step over 1.1 seconds, and the gun swept a little from side to side.
    pose = {}
    leg(pose, "Left", t, 14, 20)
    leg(pose, "Right", t + 0.5, 14, 20)
    add(pose, "Hips", 1.5 * sin(t * 2), 0, 6 * sin(t))
    add(pose, "neck", 0, 0, -5 * sin(t))
    return pose


def still(t):
    # Feet brought together and the knees still bent, ready: the baker holds the moment of this clip when the feet are closest.
    pose = {}
    add(pose, "LeftUpLeg", LEFT_MIDDLE + 6, 0, -14)
    add(pose, "LeftLeg", -6)
    add(pose, "LeftFoot", -6)
    add(pose, "RightUpLeg", RIGHT_MIDDLE + 6, 0, 14)
    add(pose, "RightLeg", -14)
    add(pose, "RightFoot", 0)
    add(pose, "Hips", 0, 0, 2 * sin(t))
    return pose


def fall(t):
    s = min(1.0, t / 0.75) ** 1.6
    settle = 3 * math.exp(-8 * max(0.0, t - 0.75)) * math.sin(max(0.0, t - 0.75) * 30)
    pose = {"shift": (0, -0.7 * s, 0)}
    add(pose, "Hips", 80 * s + settle, 0, 12 * s)
    add(pose, "neck", -15 * s, 0, 20 * s)
    add(pose, "LeftUpLeg", -10 * s)
    add(pose, "LeftLeg", 20 * s)
    add(pose, "LeftFoot", 25 * s)
    add(pose, "RightUpLeg", 10 * s)
    add(pose, "RightLeg", 10 * s)
    add(pose, "RightFoot", 30 * s)
    return pose


bpy.context.scene.render.fps = FPS
make("Run", 21, run)
make("Still", 40, still)
make("Spray", 33, spray)
make("Dead", 45, fall)
arm.animation_data.action = None

# Pictures of each clip, before writing.
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
scene.world = world
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 3
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
scene.collection.objects.link(sun)
eye = bpy.data.objects.new("eye", bpy.data.cameras.new("eye"))
scene.collection.objects.link(eye)
scene.camera = eye
centre = Vector((0, -0.2, HEIGHT * 0.45))
for action in bpy.data.actions:
    if action.name not in ("Run", "Still", "Spray", "Dead"):
        continue
    arm.animation_data.action = action
    if action.slots and arm.animation_data.action_slot is None:
        arm.animation_data.action_slot = action.slots[0]
    first, last = (int(f) for f in action.frame_range)
    for share in (0.0, 0.25, 0.5, 0.75, 1.0):
        scene.frame_set(int(first + (last - first) * share))
        for view, direction in (("quarter", (0.7, -1, 0.25)), ("side", (1, 0, 0.1))):
            eye.location = centre + Vector(direction).normalized() * HEIGHT * 2.6
            eye.rotation_euler = (centre - eye.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(folder, "shots", f"{action.name}_{int(share * 100)}_{view}.png")
            bpy.ops.render.render(write_still=True)
arm.animation_data.action = None
scene.frame_set(1)
for action in [a for a in bpy.data.actions if a.name not in ("Run", "Still", "Spray", "Dead")]:
    bpy.data.actions.remove(action)  # Anything that came with Meshy's file.

for o in (sun, eye):
    bpy.data.objects.remove(o)
mt._select([arm, mesh])
bpy.ops.export_scene.fbx(filepath=os.path.join(folder, "FlamethrowerTrooper.fbx"), use_selection=True, object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y",
                         bake_space_transform=False, apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False,
                         add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
                         bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
triangles = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
with open(os.path.join(folder, "report.json"), "w", encoding="utf-8") as f:
    json.dump({"source": SOURCE, "tris_before": 15855, "triangles": triangles, "bones": len(arm.data.bones), "meshy_credits": 17,
               "clips": {"moving": "Run", "shooting": "Spray", "dead": "Dead", "still": "Still"}}, f, indent=1)
print(f"REPORT wrote FlamethrowerTrooper.fbx, {triangles} triangles, {len(arm.data.bones)} bones")
