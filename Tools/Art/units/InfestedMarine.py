# The Infested Marine, from the owner's halo_2_anniversary_human_flood_form.glb: a human Flood form with a great scythe for a left arm.
# Meshy refused it twice (pose estimation failed), so it keeps the file's own rig of 40 bones, in a body baked for crowds like the Meshy figures.
# The file has no clips, so they are made here, as the Abomination's are: it runs lurching with its scythe out to the side, hacks with
# the scythe, and pitches forward dead. Writes Art_Source/Prepared/InfestedMarine/: InfestedMarine.fbx, InfestedMarine.png, report.json and pictures.
# Clips by role: moving Walk, shooting Swipe, dead Dead, still Still.
#   blender --background --factory-startup --python Tools/Art/units/InfestedMarine.py
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

SOURCE = "Meshy/halo_2_anniversary_human_flood_form.glb"
HEIGHT = 2.0
TRIANGLES = 5000
FPS = 30
BONES = {
    "GLTF_created_0_rootJoint": "Root", "physics_control_38": "Control", "pelvis_37": "Hips", "spine_36": "Spine", "spine1_35": "Spine1", "neck_30": "Neck", "head_29": "Head", "jaw_28": "Jaw",
    "l_thigh_2": "LeftUpLeg", "l_calf_1": "LeftLeg", "l_foot_0": "LeftFoot", "r_thigh_5": "RightUpLeg", "r_calf_4": "RightLeg", "r_foot_3": "RightFoot",
    "l_clavicle_27": "LeftShoulder", "l_upperarm_26": "LeftArm", "l_forearm_25": "LeftForeArm", "l_hand_15": "LeftHand",
    "r_clavicle_34": "RightShoulder", "r_upperarm_33": "RightArm", "r_forearm_32": "RightForeArm", "r_hand_31": "RightHand",
    "spike_lg0_18": "ScytheA0", "spike_lg1_17": "ScytheA1", "spike_lg2_16": "ScytheA2", "spike_md0_21": "ScytheB0", "spike_md1_20": "ScytheB1", "spike_md2_19": "ScytheB2",
    "spike_sm0_24": "ScytheC0", "spike_sm1_23": "ScytheC1", "spike_sm2_22": "ScytheC2",
    "ant_lg0_9": "StalkA0", "ant_lg1_8": "StalkA1", "ant_lg2_7": "StalkA2", "ant_lg3_6": "StalkA3", "ant_md0_12": "StalkB0", "ant_md1_11": "StalkB1", "ant_md2_10": "StalkB2", "ant_sm0_14": "StalkC0", "ant_sm1_13": "StalkC1",
}
SCYTHE = [["ScytheA0", "ScytheA1", "ScytheA2"], ["ScytheB0", "ScytheB1", "ScytheB2"], ["ScytheC0", "ScytheC1", "ScytheC2"]]
STALKS = [["StalkA0", "StalkA1", "StalkA2", "StalkA3"], ["StalkB0", "StalkB1", "StalkB2"], ["StalkC0", "StalkC1"]]

folder = os.path.join(mt.PREPARED, "InfestedMarine")
shutil.rmtree(folder, ignore_errors=True)
os.makedirs(os.path.join(folder, "shots"))
mt._state.update(name="InfestedMarine", folder=folder, source=SOURCE)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.join(mt.SOURCE, SOURCE))
bpy.context.view_layer.update()
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
# The Icosphere is a stray ball, and the three see-through fronds on the stalks would bake to black squares.
for o in list(bpy.context.scene.objects):
    if o.type == "MESH" and o.name in ("Icosphere", "Object_7"):
        bpy.data.objects.remove(o)
parts = [o for o in bpy.context.scene.objects if o.type == "MESH"]

# Its rest pose lies flat with arms straight out, and what the file shows stands up: that pose becomes the rest pose.
mt._select(parts + [arm])
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
for o in list(bpy.context.scene.objects):
    if o.type == "EMPTY":
        bpy.data.objects.remove(o)
for o in parts:
    mt._select([o])
    bpy.ops.object.modifier_apply(modifier=o.modifiers[0].name)
mt._select([arm])
bpy.ops.object.mode_set(mode="POSE")
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode="OBJECT")
mt._select(parts + [arm])
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Its height, with its feet on the origin.
low, high = mt._bounds()
factor = HEIGHT / (high.z - low.z)
points = [o.matrix_world @ v.co for o in parts for v in o.data.vertices]
feet = [p for p in points if p.z < low.z + (high.z - low.z) * 0.1]
middle = Vector(((min(p.x for p in feet) + max(p.x for p in feet)) / 2, (min(p.y for p in feet) + max(p.y for p in feet)) / 2, low.z))
for o in parts + [arm]:
    o.location = -middle * factor
    o.scale = (factor, factor, factor)
bpy.context.view_layer.update()
mt._select(parts + [arm])
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mt.report("standing")
mt.look("before", "front", size=(560, 560))

# One mesh, one texture. The armature goes back on only after this, as nothing here should bend it.
mt.weld(0.0004)
mt.reduce(TRIANGLES, flat_angle=0)
mt.plain_materials()
image = mt.bake_one(1024)
mt._save_scaled(image, os.path.join(folder, "InfestedMarine.png"), 1024)
mesh = mt.meshes()[0]
mt.weld(0.0004)
mt.smooth(60)
mesh.parent = arm
modifier = mesh.modifiers.new("Rig", "ARMATURE")
modifier.object = arm
arm.name = "Rig"
arm.data.name = "Rig"
for old, new in BONES.items():
    arm.data.bones[old].name = new
print(f"REPORT bones {sorted(g.name for g in mesh.vertex_groups)}")


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


def make(name, frames, pose_at):
    """Each frame is keyed twice: the second time lifted or lowered so that its lowest point is on the ground."""
    action = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = action
    for frame in range(frames + 1):
        pose = pose_at(frame / frames)
        key(frame + 1, pose)
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


def wave(pose, chain, t, size, each=0.12, axis=0):
    """A wave running down a chain of bones: it grows with its length."""
    for i, name in enumerate(chain):
        degrees = [0.0, 0.0, 0.0]
        degrees[axis] = size * (0.5 + 0.25 * i) * sin(t - i * each)
        add(pose, name, *degrees)


def base():
    """How it holds itself: bent forward, the good arm hanging, the scythe out to the side and a little forward, the head dropped from where it lolls in the file."""
    return {
        "Spine": (8, 0, 0), "Spine1": (4, 0, 0), "Neck": (28, 0, 0),
        "RightArm": (0, -28, 0), "RightForeArm": (-20, 0, 0),
        "LeftArm": (0, 8, -15), "LeftForeArm": (-10, 0, 0),
    }


def leg(pose, side, phase):
    thigh = -30 * sin(phase) - 6
    knee = 12 + 42 * max(0.0, cos(phase)) ** 1.5
    add(pose, side + "UpLeg", thigh)
    add(pose, side + "Leg", knee)
    add(pose, side + "Foot", -(thigh + knee) * 0.7)


def walk(t):
    # 0.7 seconds for two strides of about a metre each, for its 3 m/s.
    pose = base()
    s = sin(t)
    leg(pose, "Left", t)
    leg(pose, "Right", t + 0.5)
    add(pose, "Hips", 5 + 2 * sin(t * 2), -4 * s, 7 * s)
    add(pose, "Spine", z=-6 * s)
    add(pose, "Spine1", 3 * sin(t * 2 + 0.1), 3 * s, -6 * s)
    add(pose, "Neck", 5 * sin(t * 2 + 0.2), 0, 8 * s)
    add(pose, "RightArm", -24 * s)
    add(pose, "RightForeArm", -12 * s - 6)
    add(pose, "LeftArm", 8 * s, 3 * sin(t * 2), 6 * s)
    add(pose, "Jaw", 8 + 6 * sin(t * 2))
    for i, chain in enumerate(SCYTHE):
        wave(pose, chain, t * 2 + i * 0.2, 6, axis=1)
    for i, chain in enumerate(STALKS):
        wave(pose, chain, t * 2 + i * 0.3, 14, 0.2, axis=2)
    return pose


def still(t):
    pose = base()
    add(pose, "Spine1", 2 * sin(t))
    add(pose, "Neck", 0, 0, 5 * sin(t))
    add(pose, "LeftUpLeg", -4)
    add(pose, "LeftLeg", 8)
    add(pose, "LeftFoot", -3)
    add(pose, "RightUpLeg", 2)
    add(pose, "RightLeg", 3)
    add(pose, "RightFoot", -4)
    for i, chain in enumerate(STALKS):
        wave(pose, chain, t + i * 0.3, 8, 0.2, axis=2)
    return pose


def swipe(t):
    # 0.8 seconds, its Scythe Limb's fire interval: back and up over 45%, down and across hard in 15%, then back to where it began.
    if t < 0.45:
        raised = math.sin(t / 0.45 * math.pi / 2)
    elif t < 0.6:
        raised = 1 - 2 * (t - 0.45) / 0.15
    else:
        raised = -(1 - (t - 0.6) / 0.4)
    pose = base()
    add(pose, "Hips", 3, 0, 10 * raised)
    add(pose, "Spine", 2 - 8 * raised, 0, 14 * raised)
    add(pose, "Spine1", -6 * raised, -6 * raised, 12 * raised)
    add(pose, "Neck", 0, 0, -14 * raised)
    pose["LeftArm"] = (-10 * max(0.0, -raised), 8 - 42 * max(0.0, raised), -30 + 45 * raised)
    add(pose, "LeftForeArm", -24 * max(0.0, -raised))
    add(pose, "RightArm", -16 * raised, 0, 6 * raised)
    add(pose, "LeftUpLeg", -16)
    add(pose, "LeftLeg", 20)
    add(pose, "LeftFoot", -3)
    add(pose, "RightUpLeg", 12)
    add(pose, "RightLeg", 8)
    add(pose, "RightFoot", -14)
    add(pose, "Jaw", 10 * max(0.0, -raised) + 6)
    for i, chain in enumerate(SCYTHE):
        wave(pose, chain, t * 2 + i * 0.2, 9, axis=2)
    for i, chain in enumerate(STALKS):
        wave(pose, chain, t * 2 + i * 0.3, 16, 0.2, axis=2)
    return pose


def fall(t):
    s = min(1.0, t / 0.75) ** 1.6
    settle = 3 * math.exp(-8 * max(0.0, t - 0.75)) * math.sin(max(0.0, t - 0.75) * 30)
    pose = base()
    pose["shift"] = (0, -0.75 * s, 0)
    add(pose, "Hips", 88 * s + settle, 0, 10 * s)
    add(pose, "Spine", -8 * s)
    add(pose, "Spine1", -4 * s)
    add(pose, "Neck", -18 * s, 0, 30 * s)
    add(pose, "LeftUpLeg", -18 * s)
    add(pose, "LeftLeg", 15 * s)
    add(pose, "LeftFoot", 25 * s)
    add(pose, "RightUpLeg", -10 * s)
    add(pose, "RightLeg", 6 * s)
    add(pose, "RightFoot", 30 * s)
    add(pose, "RightArm", -35 * s, 10 * s, 0)
    add(pose, "LeftArm", -25 * s, 0, -15 * s)
    add(pose, "Jaw", 18 * s)
    return pose


bpy.context.scene.render.fps = FPS
make("Walk", 21, walk)
make("Still", 40, still)
make("Swipe", 24, swipe)
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
centre = Vector((0.2, 0, HEIGHT * 0.45))
for action in bpy.data.actions:
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

for o in (sun, eye):
    bpy.data.objects.remove(o)
mt._select([arm, mesh])
bpy.ops.export_scene.fbx(filepath=os.path.join(folder, "InfestedMarine.fbx"), use_selection=True, object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y",
                         bake_space_transform=False, apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False,
                         add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
                         bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
triangles = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
with open(os.path.join(folder, "report.json"), "w", encoding="utf-8") as f:
    json.dump({"source": SOURCE, "triangles": triangles, "bones": len(arm.data.bones), "clips": {"moving": "Walk", "shooting": "Swipe", "dead": "Dead", "still": "Still"}}, f, indent=1)
print(f"REPORT wrote InfestedMarine.fbx, {triangles} triangles, {len(arm.data.bones)} bones")
