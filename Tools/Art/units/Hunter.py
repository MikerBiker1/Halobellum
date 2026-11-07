# The Hunters (D87): the Hunter of the Hunter Pair, and the Hunter Captain, from the owner's enemiesmulticovenanthunters.glb, rigged by Meshy
# (Tools/Meshy/rig_inputs.py and meshy_rig.py, kept in Prepared/<unit>_Rig). Meshy's clips walked them with the shield hanging at the side and
# fired with it out of the way, so their clips are made here, as the Flamethrower Trooper's are, around the pose the file shows:
#   Guard  standing braced, the shield held up in front (the baker holds this as the still frame);
#   Walk   a heavy stride behind the shield;
#   Aim    crouched wide behind the shield, the cannon arm levelled over it at the target, kicking back with each shot (the owner's picture);
#   Bash   the shield driven forward into whatever is in front, then drawn back (its melee, D87);
#   Charge a lunging run behind the shield, closing the gap before the first bash (its leap, D87);
#   Dead   falling forward onto the shield.
# The shield is part of the body, weighted by Meshy partly to the upper arm and partly to the hand, so it would fold at the elbow: everything
# beside the left arm, outside SHIELD_X, is weighted to the forearm alone, and the shield stays one plate.
# Writes Prepared/<unit>/: <unit>.fbx (clips named Rig|<clip>), <unit>.png, report.json and pictures.
#   blender --background --factory-startup --python Tools/Art/units/Hunter.py               (the Hunter)
#   HB_UNIT=HunterCaptain blender --background --factory-startup --python Tools/Art/units/Hunter.py
# Facing -Y, feet on the origin. The character's left is +X: its shield arm. Its cannon is the right arm.
import json
import math
import os
import shutil
import sys

import bpy
from mathutils import Euler, Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

UNIT = os.environ.get("HB_UNIT", "Hunter")
CONFIG = {
    "Hunter": dict(height=3.6, shield_x=0.86),
    "HunterCaptain": dict(height=3.8, shield_x=0.86),
}[UNIT]
SOURCE = "sketchfab.com/enemiesmulticovenanthunters.glb"
RIG = os.path.join(mt.SOURCE, "Prepared", UNIT + "_Rig", "meshy", "rigged.glb")  # Meshy's rig, already paid for: never downloaded again here.
HEIGHT = CONFIG["height"]
FPS = 30
CLIPS = ("Guard", "Walk", "Aim", "Bash", "Charge", "Dead")
POSES_ONLY = os.environ.get("HB_POSES_ONLY") == "1"  # Quick pictures of single poses, for working on them; writes no FBX.

folder = os.path.join(mt.PREPARED, UNIT)
shutil.rmtree(folder, ignore_errors=True)
os.makedirs(os.path.join(folder, "shots"))
mt._state.update(name=UNIT, folder=folder, source=SOURCE)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=RIG)
bpy.context.view_layer.update()
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
arm.animation_data_clear()  # Meshy's file brings an action of its own, which would hold every bone where it puts it.
for action in list(bpy.data.actions):
    bpy.data.actions.remove(action)
parts = sorted([o for o in bpy.context.scene.objects if o.type == "MESH"], key=lambda o: -len(o.data.polygons))
for o in parts[1:]:
    print(f"REPORT removed {o.name}, {len(o.data.polygons)} faces")  # Meshy's file brings a small stray ball beside the body.
    bpy.data.objects.remove(o)
parts = parts[:1]
image = mt._input_image(parts[0].data.materials[0], "Base Color")
print(f"REPORT texture {image.name} {tuple(image.size)}, {len(arm.data.bones)} bones")

# Everything applied, in the rest pose, which is the pose the file shows; then stood on the origin at its height.
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
mt._save_scaled(image, os.path.join(folder, UNIT + ".png"), 1024)
mesh.parent = arm
modifier = mesh.modifiers.new("Rig", "ARMATURE")
modifier.object = arm
arm.name = "Rig"
arm.data.name = "Rig"

# The shield: every vertex beside the left arm follows the forearm alone.
forearm = mesh.vertex_groups.get("LeftForeArm")
moved = 0
for v in mesh.data.vertices:
    if v.co.x <= CONFIG["shield_x"]:
        continue
    for g in list(v.groups):
        mesh.vertex_groups[g.group].remove([v.index])
    forearm.add([v.index], 1.0, "REPLACE")
    moved += 1
print(f"REPORT shield: {moved} vertices weighted to the forearm alone")


def world_turn(bone_name, degrees):
    """A turn given in the model's axes (x across, y front to back, z up), as a pose bone's local rotation."""
    rest = arm.data.bones[bone_name].matrix_local.to_3x3().to_quaternion()
    turn = Euler([math.radians(d) for d in degrees], "XYZ").to_quaternion()
    return rest.inverted() @ turn @ rest


def apply(pose):
    for bone in arm.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = world_turn(bone.name, pose.get(bone.name, (0, 0, 0)))
    root = arm.pose.bones["Hips"]
    rest = arm.data.bones["Hips"].matrix_local.to_3x3()
    root.location = rest.inverted() @ Vector(pose.get("shift", (0, 0, 0)))


def key(frame, pose):
    """pose: bone name to degrees; "shift" moves the whole body in metres."""
    apply(pose)
    for bone in arm.pose.bones:
        bone.keyframe_insert("rotation_quaternion", frame=frame)
    arm.pose.bones["Hips"].keyframe_insert("location", frame=frame)


def lowest_point():
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh.evaluated_get(depsgraph)
    data = evaluated.to_mesh()
    result = min((mesh.matrix_world @ v.co).z for v in data.vertices)
    evaluated.to_mesh_clear()
    return result


def feet_lowest():
    """The lowest point of the feet only: the shield may reach below them as it swings, and must not lift the body."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh.evaluated_get(depsgraph)
    data = evaluated.to_mesh()
    result = min((mesh.matrix_world @ v.co).z for v in data.vertices if abs((mesh.matrix_world @ mesh.data.vertices[v.index].co).x) < 0.6)
    evaluated.to_mesh_clear()
    return result


def make(name, frames, pose_at, grounded=True, ground=None):
    """Each frame is keyed twice: the second time lifted or lowered so that its feet (or, falling, its lowest point) stand on the ground."""
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
            pose["shift"] = (x, y, z - (ground or feet_lowest)())
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


def ease(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


# ---- The pieces poses are made of. Angles in degrees around the model's axes; for a limb hanging down, -x swings it forward. ----

def stance(pose, crouch, spread):
    """Knees bent by crouch, legs apart by spread, feet flat."""
    add(pose, "LeftUpLeg", -crouch, -spread, 0)
    add(pose, "RightUpLeg", -crouch, spread, 0)
    add(pose, "LeftLeg", crouch * 2)
    add(pose, "RightLeg", crouch * 2)
    add(pose, "LeftFoot", -crouch, spread, 0)
    add(pose, "RightFoot", -crouch, -spread, 0)


def lean(pose, forward, turn=0.0):
    """The upper body bent forward from the waist, and turned (positive: its right shoulder forward)."""
    add(pose, "Spine02", forward * 0.6, 0, turn * 0.5)
    add(pose, "Spine01", forward * 0.4, 0, turn * 0.5)
    add(pose, "neck", -forward * 0.7, 0, -turn * 0.6)


def shield_up(pose, k=1.0, push=0.0):
    """
    The shield brought round in front of the body, its face forward. Push drives it straight forward from the shoulder, the forearm keeping
    its slant so the shield stays upright (negative draws it back).
    """
    add(pose, "LeftShoulder", 0, 0, -18 * push)
    add(pose, "LeftArm", (-35 - 24 * push) * k, 18 * k, 0)
    add(pose, "LeftForeArm", (-15 + 22 * push) * k, 0, -90 * k)


def cannon_level(pose, k=1.0, kick=0.0):
    """The cannon arm raised to point ahead; kick throws it up and back, as a shot does."""
    add(pose, "RightArm", (-80 + 14 * kick) * k, 0, 10 * k)
    add(pose, "RightForeArm", (-6 - 12 * kick) * k, 0, 0)


AIM_SECONDS = {"Hunter": 2.0, "HunterCaptain": 1.5}[UNIT]  # The Assault Cannon fires every 2 s; the Captain's laser twice in this loop.
SHOTS_PER_AIM = {"Hunter": 1, "HunterCaptain": 2}[UNIT]


def guard(t):
    pose = {}
    stance(pose, 18, 9)
    lean(pose, 10)
    shield_up(pose)
    cannon_level(pose, 0.45)
    add(pose, "Spine02", 1.5 * sin(t))  # Breathing.
    return pose


def kick_at(t, shots):
    """How far a shot has thrown the cannon back at this point of the loop: up at once, settling over a third of a second."""
    local = (t * shots) % 1.0
    seconds = local * AIM_SECONDS / shots
    return math.exp(-9 * seconds) * min(1.0, seconds / 0.05)


def aim(t):
    # The owner's picture: crouched low and wide behind the shield, the cannon levelled over it, the head down behind the shield's rim.
    pose = {}
    stance(pose, 32, 13)
    lean(pose, 20, 8)
    shield_up(pose, 1.0, 0.15)
    kick = kick_at(t, SHOTS_PER_AIM)
    cannon_level(pose, 1.0, kick)
    add(pose, "Spine02", 4 * kick)  # The shot rocks the body back a little.
    return pose


def walk(t):
    # 1.2 s for two strides, for its 1.1 m/s: heavy, knees bent, the shield carried in front and the body rolling over each foot.
    pose = {}
    stance(pose, 14, 7)
    for side, phase in (("Left", t), ("Right", t + 0.5)):
        thigh = -22 * sin(phase)
        knee = 34 * max(0.0, cos(phase)) ** 1.5
        add(pose, side + "UpLeg", thigh)
        add(pose, side + "Leg", knee)
        add(pose, side + "Foot", -(thigh + knee) * 0.5)
    lean(pose, 12, 6 * sin(t))
    add(pose, "Hips", 0, 3 * sin(t), 0)  # Rolling over the foot that carries it.
    shield_up(pose, 1.0, 0.05 * sin(t * 2))
    cannon_level(pose, 0.3)
    add(pose, "RightArm", 10 * sin(t))
    return pose


def bash(t):
    # 1.5 s, one blow: drawn back behind the shield, then the left shoulder and the shield driven forward with a step, then back on guard.
    pose = {}
    stance(pose, 22, 11)
    back = ease(t / 0.35) * (1 - ease((t - 0.35) / 0.12))
    hit = ease((t - 0.35) / 0.12) * (1 - ease((t - 0.62) / 0.38))
    lean(pose, 14 + 14 * hit - 4 * back, 22 * back - 30 * hit)
    shield_up(pose, 1.0, -0.4 * back + 1.0 * hit)
    add(pose, "LeftUpLeg", -18 * hit)  # The step in.
    add(pose, "LeftLeg", 12 * hit)
    add(pose, "RightUpLeg", 10 * hit)
    pose["shift"] = (0, -0.35 * hit, 0)
    cannon_level(pose, 0.4 - 0.3 * hit)
    return pose


def charge(t):
    # 0.5 s, two long strides: leaning hard behind the shield, the cannon arm swung back.
    pose = {}
    stance(pose, 18, 6)
    for side, phase in (("Left", t), ("Right", t + 0.5)):
        thigh = -36 * sin(phase)
        knee = 55 * max(0.0, cos(phase)) ** 1.3
        add(pose, side + "UpLeg", thigh)
        add(pose, side + "Leg", knee)
        add(pose, side + "Foot", -(thigh + knee) * 0.4)
    lean(pose, 26, 8 * sin(t))
    shield_up(pose, 1.0, 0.35)
    add(pose, "RightArm", 25 - 15 * sin(t), 0, 0)
    add(pose, "RightForeArm", -30)
    return pose


def fall(t):
    # Pitching forward onto the shield, the knees giving way first.
    s = ease(t / 0.7)
    settle = 2.5 * math.exp(-8 * max(0.0, t - 0.7)) * math.sin(max(0.0, t - 0.7) * 30)
    pose = {"shift": (0, -1.2 * s, 0)}
    stance(pose, 18 + 30 * s, 9)
    add(pose, "Hips", 78 * s + settle, 0, 10 * s)
    shield_up(pose, 1.0 - 0.6 * s)
    add(pose, "RightArm", -30 * s, 0, 30 * s)
    add(pose, "neck", -20 * s)
    return pose


POSES = {"guard": guard, "aim": aim, "walk": walk, "bash_back": lambda t: bash(0.33), "bash_hit": lambda t: bash(0.5), "charge": charge,
         "dead": lambda t: fall(1.0)}

if POSES_ONLY:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x, scene.render.resolution_y = 420, 480
    eye = bpy.data.objects.new("eye", bpy.data.cameras.new("eye"))
    scene.collection.objects.link(eye)
    scene.camera = eye
    centre = Vector((0, -0.2, HEIGHT * 0.45))
    wanted = os.environ.get("HB_POSES", "").split(",")
    for name, at in POSES.items():
        if wanted != [""] and name not in wanted:
            continue
        pose = at(0.1)
        apply(pose)
        bpy.context.view_layer.update()
        x, y, z = pose.get("shift", (0, 0, 0))
        pose["shift"] = (x, y, z - (lowest_point() if name == "dead" else feet_lowest()))
        apply(pose)
        bpy.context.view_layer.update()
        for view, direction in (("front", (0, -1, 0.1)), ("quarter", (0.7, -1, 0.25)), ("side", (-1, 0, 0.1)), ("other", (-0.7, -1, 0.25))):
            eye.location = centre + Vector(direction).normalized() * HEIGHT * 2.4
            eye.rotation_euler = (centre - eye.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(folder, "shots", f"pose_{name}_{view}.png")
            bpy.ops.render.render(write_still=True)
    raise SystemExit(0)

bpy.context.scene.render.fps = FPS
make("Guard", 40, guard)
make("Walk", 36, walk)
make("Aim", int(round(AIM_SECONDS * FPS)), aim)
make("Bash", 45, bash)
make("Charge", 15, charge)
make("Dead", 60, fall, ground=lowest_point)
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
    if action.name not in CLIPS:
        continue
    arm.animation_data.action = action
    if action.slots and arm.animation_data.action_slot is None:
        arm.animation_data.action_slot = action.slots[0]
    first, last = (int(f) for f in action.frame_range)
    for share in (0.0, 0.25, 0.5, 0.75):
        scene.frame_set(int(first + (last - first) * share))
        for view, direction in (("quarter", (0.7, -1, 0.25)), ("side", (-1, 0, 0.1))):
            eye.location = centre + Vector(direction).normalized() * HEIGHT * 2.6
            eye.rotation_euler = (centre - eye.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(folder, "shots", f"{action.name}_{int(share * 100)}_{view}.png")
            bpy.ops.render.render(write_still=True)
arm.animation_data.action = None
scene.frame_set(1)

for o in (sun, eye):
    bpy.data.objects.remove(o)
mt._select([arm, mesh])
bpy.ops.export_scene.fbx(filepath=os.path.join(folder, UNIT + ".fbx"), use_selection=True, object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y",
                         bake_space_transform=False, apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False,
                         add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
                         bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
triangles = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
with open(os.path.join(folder, "report.json"), "w", encoding="utf-8") as f:
    json.dump({"source": SOURCE, "triangles": triangles, "bones": len(arm.data.bones),
               "clips": {"moving": "Rig|Walk", "shooting": "Rig|Aim", "dead": "Rig|Dead", "still": "Rig|Guard", "melee": "Rig|Bash", "charge": "Rig|Charge"}}, f, indent=1)
print(f"REPORT wrote {UNIT}.fbx, {triangles} triangles, {len(arm.data.bones)} bones")
