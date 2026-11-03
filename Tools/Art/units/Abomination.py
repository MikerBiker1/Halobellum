# The Flood Abomination, from the owner's Meshy model with Meshy's smart rig (43 bones, no clips, and no human shape for Meshy's library to
# fit). Its clips are made here from waves along the bones: it lurches on its two legs, swings its arms and writhes its tendrils; it
# slams with both arms; it collapses forward. Writes Art_Source/Prepared/Abomination/: Abomination.fbx, Abomination.png, and pictures.
#   blender --background --factory-startup --python Tools/Art/units/Abomination.py
import math
import os
import shutil
import sys

import bpy
from mathutils import Euler, Quaternion, Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

HEIGHT = 11.0
FPS = 30
folder = os.path.join(mt.PREPARED, "Abomination")
shutil.rmtree(folder, ignore_errors=True)
os.makedirs(os.path.join(folder, "shots"))
mt._state.update(name="Abomination", folder=folder)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.join(mt.SOURCE, "Meshy", "Abomination_Meshy.glb"))
for o in list(bpy.context.scene.objects):
    if o.type == "MESH" and o.name != "output_unwrapped":
        bpy.data.objects.remove(o)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
mesh = next(o for o in bpy.context.scene.objects if o.type == "MESH")
mesh.name = "Body"
arm.name = "Rig"
arm.data.name = "Rig"

# The texture, at a size the game wants.
image = mt._input_image(mesh.data.materials[0], "Base Color")
mt._save_scaled(image, os.path.join(folder, "Abomination.png"), 1024)

# Its height in the game, with the rig: the whole armature is scaled and the scale applied.
low = min((mesh.matrix_world @ v.co).z for v in mesh.data.vertices)
high = max((mesh.matrix_world @ v.co).z for v in mesh.data.vertices)
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
mesh.select_set(True)
bpy.context.view_layer.objects.active = arm
factor = HEIGHT / (high - low)
arm.scale = (factor, factor, factor)
bpy.context.view_layer.update()
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
mesh.select_set(False)
bpy.context.view_layer.update()
print(f"REPORT height {high - low} scaled by {factor}")

LEG_R = ["Bone_008", "Bone_007", "Bone_006", "Bone_005"]
LEG_L = ["Bone_012", "Bone_011", "Bone_010", "Bone_009"]
SPINE = ["Bone_001", "Bone_004", "Bone_003", "Bone_002"]
ARM_LEFT = ["Bone_019", "Bone_018", "Bone_017", "Bone_016", "Bone_015"]
FINGERS_LEFT = [["Bone_028", "Bone_027", "Bone_026"], ["Bone_032", "Bone_031", "Bone_030", "Bone_029"]]
ARM_RIGHT = ["Bone_025", "Bone_024", "Bone_023", "Bone_022", "Bone_021", "Bone_020"]
TENDRILS = [["Bone_034", "Bone_033"], ["Bone_038", "Bone_037", "Bone_036", "Bone_035"], ["Bone_042", "Bone_041", "Bone_040", "Bone_039"]]
HEAD = ["Bone_014", "Bone_013"]
ROOT = "Bone_000"


def world_turn(bone_name, degrees):
    """A turn given in the model's axes (x across, y front to back, z up), as a pose bone's local rotation."""
    rest = arm.data.bones[bone_name].matrix_local.to_3x3().to_quaternion()
    turn = Euler([math.radians(d) for d in degrees], "XYZ").to_quaternion()
    return rest.inverted() @ turn @ rest


def key(action_frame, pose):
    """pose: bone name to degrees; ROOT's "shift" lifts and moves the whole body in metres."""
    for bone in arm.pose.bones:
        degrees = pose.get(bone.name, (0, 0, 0))
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = world_turn(bone.name, degrees)
        bone.keyframe_insert("rotation_quaternion", frame=action_frame)
    root = arm.pose.bones[ROOT]
    rest = arm.data.bones[ROOT].matrix_local.to_3x3()
    root.location = rest.inverted() @ Vector(pose.get("shift", (0, 0, 0)))
    root.keyframe_insert("location", frame=action_frame)


def make(name, frames, pose_at):
    action = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = action
    for frame in range(frames + 1):
        key(frame + 1, pose_at(frame / frames))
    action.use_fake_user = True
    action.use_frame_range = True
    action.frame_start = 1
    action.frame_end = frames + 1
    print(f"REPORT clip {name}, {frames} frames")


def sin(t):
    return math.sin(t * math.pi * 2)


def wave(chain, t, size, each=0.12, axis=0):
    """A wave running down a chain of bones: it grows with its length."""
    pose = {}
    for i, name in enumerate(chain):
        degrees = [0, 0, 0]
        degrees[axis] = size * (0.5 + 0.25 * i) * sin(t - i * each)
        pose[name] = tuple(degrees)
    return pose


def walk(t):
    pose = {
        "shift": (0.15 * HEIGHT * 0.1 * sin(t), 0, 0.012 * HEIGHT * sin(t * 2)),
        LEG_R[0]: (26 * sin(t), 0, 0), LEG_R[1]: (-18 * sin(t + 0.2) - 8, 0, 0), LEG_R[2]: (10 * sin(t + 0.35), 0, 0),
        LEG_L[0]: (-26 * sin(t), 0, 0), LEG_L[1]: (18 * sin(t + 0.2) - 8, 0, 0), LEG_L[2]: (-10 * sin(t + 0.35), 0, 0),
        SPINE[0]: (0, 5 * sin(t), 4 * sin(t)), SPINE[1]: (3 * sin(t * 2), 0, -3 * sin(t)), SPINE[3]: (0, -5 * sin(t), 0),
        HEAD[0]: (6 * sin(t * 2 + 0.2), 0, 8 * sin(t)),
    }
    pose.update(wave(ARM_LEFT, t + 0.5, 9))
    pose.update(wave(ARM_RIGHT, t, 7))
    for chain in FINGERS_LEFT:
        pose.update(wave(chain, t * 2, 8, axis=1))
    for i, chain in enumerate(TENDRILS):
        pose.update(wave(chain, t * 2 + i * 0.3, 11, 0.2, axis=2))
    return pose


def idle(t):
    pose = {SPINE[1]: (2 * sin(t), 0, 0), HEAD[0]: (0, 0, 5 * sin(t))}
    for i, chain in enumerate(TENDRILS):
        pose.update(wave(chain, t + i * 0.3, 6, 0.2, axis=2))
    return pose


def slam(t):
    # Up over 60% of the swing, then down hard, then a slow recovery that ends where it began.
    if t < 0.45:
        raised = math.sin(t / 0.45 * math.pi / 2)
    elif t < 0.6:
        raised = 1 - 1.4 * (t - 0.45) / 0.15
    else:
        raised = -0.4 * (1 - (t - 0.6) / 0.4)
    pose = {
        "shift": (0, 0, -0.01 * HEIGHT * max(0, -raised) * 3),
        SPINE[1]: (-14 * raised, 0, 0), SPINE[3]: (-12 * raised, 0, 0), HEAD[0]: (-14 * raised, 0, 0),
        ARM_LEFT[0]: (-62 * raised, 0, -8 * raised), ARM_LEFT[1]: (-30 * raised, 0, 0), ARM_LEFT[2]: (-14 * raised, 0, 0),
        ARM_RIGHT[0]: (-36 * raised, 0, 6 * raised), ARM_RIGHT[1]: (-16 * raised, 0, 0), ARM_RIGHT[2]: (-8 * raised, 0, 0),
        LEG_R[0]: (6 * max(0, -raised), 0, 0), LEG_L[0]: (6 * max(0, -raised), 0, 0),
    }
    for i, chain in enumerate(TENDRILS):
        pose.update(wave(chain, t * 3 + i * 0.3, 14, 0.2, axis=2))
    return pose


def fall(t):
    s = min(1.0, t / 0.8) ** 1.6
    pose = {
        "shift": (0, -0.08 * HEIGHT * s, -0.43 * HEIGHT * s),
        ROOT: (72 * s, 0, 8 * s),
        SPINE[1]: (24 * s, 0, 0), SPINE[3]: (22 * s, 0, 0), HEAD[0]: (20 * s, 0, 0),
        LEG_R[0]: (-70 * s, 0, 0), LEG_R[1]: (58 * s, 0, 0), LEG_L[0]: (-80 * s, 0, 0), LEG_L[1]: (62 * s, 0, 0),
        ARM_LEFT[0]: (-30 * s, 0, 20 * s), ARM_LEFT[1]: (-22 * s, 0, 0),
        ARM_RIGHT[0]: (-26 * s, 0, -20 * s), ARM_RIGHT[1]: (-22 * s, 0, 0),
    }
    if t < 0.9:
        for i, chain in enumerate(TENDRILS):
            pose.update(wave(chain, t * 3 + i * 0.3, 16 * (1 - t), 0.2, axis=2))
    return pose


bpy.context.scene.render.fps = FPS
make("Walk", 72, walk)
make("Still", 90, idle)
make("Slam", 75, slam)
make("Dead", 105, fall)
arm.animation_data.action = None

# Pictures of each clip, before writing.
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x, scene.render.resolution_y = 360, 420
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
centre = Vector((0, 0, HEIGHT * 0.45))
eye.location = centre + Vector((0.7, -1, 0.25)).normalized() * HEIGHT * 2.4
eye.rotation_euler = (centre - eye.location).to_track_quat("-Z", "Y").to_euler()
for action in bpy.data.actions:
    arm.animation_data.action = action
    if action.slots and arm.animation_data.action_slot is None:
        arm.animation_data.action_slot = action.slots[0]
    first, last = (int(f) for f in action.frame_range)
    for share in (0.0, 0.25, 0.5, 0.75, 1.0):
        scene.frame_set(int(first + (last - first) * share))
        scene.render.filepath = os.path.join(folder, "shots", f"{action.name}_{int(share * 100)}.png")
        bpy.ops.render.render(write_still=True)
arm.animation_data.action = None
scene.frame_set(1)

for o in (sun, eye):
    bpy.data.objects.remove(o)
for obj in (arm, mesh):
    obj.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=os.path.join(folder, "Abomination.fbx"), use_selection=True, object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y",
                         bake_space_transform=False, apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False,
                         add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
                         bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
print("REPORT wrote Abomination.fbx")
