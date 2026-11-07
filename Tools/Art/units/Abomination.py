# The Flood Abomination, from the owner's Meshy model with Meshy's smart rig (43 bones and weights, no clips; no human shape for Meshy's
# library to fit). Its clips are made here (D93, remade from D79's waves): an enormous mass of Flood biomass, it walks in slow, heavy steps
# on planted feet, the whole bulk dropping onto each and its torso twisting and swaying over them; the great clawed arm swings, the long
# tentacle arm drags and its three tendrils writhe all the time. Its attack is one slam to a clip, timed to land with the Tendril Slam's
# strike: it rears up, both arms raised high, a long held anticipation; crashes down; heaves up again slowly; and between slams its long
# tentacle arm cracks out in a single whip, for its Tendril Whip. It dies sinking to its knees and pitching forward. Sizes in metres at
# its height in the game (11 m); front toward -Y. The rig is Meshy's, its hips renamed Body under a still Root (creature_rig.adopt).
# Writes Art_Source/Prepared/Abomination/: Abomination.fbx, Abomination.png, and pictures.
#   blender --background --factory-startup --python Tools/Art/units/Abomination.py
#   HB_ONLY=clips (stop after the pictures), HB_CLIPS and HB_PICTURES (which clips to make and film).
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import creature_rig as cr  # noqa: E402
import model_tools as mt  # noqa: E402

ONLY = os.environ.get("HB_ONLY", "")
HEIGHT = 11.0
FPS = 30
SPEED = 1.25  # Its move speed in the game, m/s.
SLAM_SECONDS = 2.5  # The Tendril Slam's interval: the attack clip is one slam long.
SLAM_MOMENT = 0.62  # The share of the attack clip at which the slam lands.

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.join(mt.SOURCE, "Meshy", "Abomination_Meshy.glb"))
for o in list(bpy.context.scene.objects):
    if o.type == "MESH" and o.name != "output_unwrapped":
        bpy.data.objects.remove(o)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
mesh = next(o for o in bpy.context.scene.objects if o.type == "MESH")
mesh.name = "Body_Mesh"
arm.name = "Rig"
arm.data.name = "Rig"
arm.animation_data_clear()
low = min((mesh.matrix_world @ v.co).z for v in mesh.data.vertices)
high = max((mesh.matrix_world @ v.co).z for v in mesh.data.vertices)
mt._select([arm, mesh])
bpy.context.view_layer.objects.active = arm
factor = HEIGHT / (high - low)
arm.scale = (factor, factor, factor)
bpy.context.view_layer.update()
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
cr.adopt("Abomination", "Meshy/Abomination_Meshy.glb", mesh, arm, "Bone_000", fps=FPS)
image = mt._input_image(mesh.data.materials[0], "Base Color")
mt._save_scaled(image, os.path.join(cr._c["folder"], "Abomination.png"), 1024)

HEAD = ["Bone_014", "Bone_013"]
CLAW_ARM = ["Bone_019", "Bone_018", "Bone_017", "Bone_016", "Bone_015"]  # The great clawed arm, on its -X side.
CLAWS = [["Bone_028", "Bone_027", "Bone_026"], ["Bone_032", "Bone_031", "Bone_030", "Bone_029"]]
WHIP_ARM = ["Bone_025", "Bone_024", "Bone_023", "Bone_022", "Bone_021", "Bone_020"]  # The long tentacle arm, on its +X side, down to the ground.
TENDRILS = [["Bone_034", "Bone_033"], ["Bone_038", "Bone_037", "Bone_036", "Bone_035"], ["Bone_042", "Bone_041", "Bone_040", "Bone_039"]]

cr.leg("R", "Bone_008", "Bone_007", "Bone_006", pole=(1, -0.3, 0))  # Its legs are splayed, knees out.
cr.leg("L", "Bone_012", "Bone_011", "Bone_010", pole=(-1, -0.3, 0))
CROUCH = 0.6  # Its legs are nearly straight in its model: it carries its bulk lower, knees bent under the weight.
CONTACT = {"R": Vector((1.1, 0.4, 0.28)), "L": Vector((-3.7, -0.3, 0.22))}
CYCLE = 2.4  # Seconds per stride: two slow steps.
DUTY = 0.65
PHASE = {"R": 0.0, "L": 0.5}


def add(pose, bone, x=0.0, y=0.0, z=0.0):
    old = pose["turn"].get(bone, (0, 0, 0))
    pose["turn"][bone] = (old[0] + x, old[1] + y, old[2] + z)


def wave_down(pose, chain, t, size, lag=0.12, axis=0, grow=0.25):
    """A wave running down a chain of bones, growing toward its end."""
    for i, name in enumerate(chain):
        degrees = [0.0, 0.0, 0.0]
        degrees[axis] = size * (0.5 + grow * i) * cr.wave(t - i * lag)
        add(pose, name, *degrees)


def writhe(pose, t, size=1.0, speed=2):
    """The three tendrils of the tentacle arm and the claws, never still, each on its own beat."""
    for n, chain in enumerate(TENDRILS):
        wave_down(pose, chain, t * speed + n * 0.31, 12 * size, 0.18, axis=2)
        wave_down(pose, chain, t * (speed + 1) + n * 0.17, 6 * size, 0.2, axis=0)
    for n, chain in enumerate(CLAWS):
        wave_down(pose, chain, t * speed + 0.4 + n * 0.25, 5 * size, 0.15, axis=1)


def stance():
    pose = {"move": (0, 0, -CROUCH), "turn": {}, "feet": {}, "foot_turn": {}}
    add(pose, "Bone_004", 5)
    add(pose, "Bone_003", 4)
    # Its arms reach the ground in its model: crouched, they would sink into it, so both are lifted a little at the shoulder.
    add(pose, WHIP_ARM[0], 0, -9, 0)
    add(pose, CLAW_ARM[0], 0, 9, 0)
    for name, contact in CONTACT.items():
        pose["feet"][name] = cr.target_for(name, contact)
        pose["foot_turn"][name] = (0, 0, 0)
    return pose


def thud(t, name, width=0.05):
    """1 as a foot comes down (the end of its swing), falling away."""
    d = (t - PHASE[name] + 0.5) % 1.0 - 0.5
    return math.exp(-(d / width) ** 2)


def walk(t):
    pose = stance()
    w = cr.wave
    impact = thud(t, "R") + thud(t, "L")
    # The bulk drops hard onto each foot, rolls over it, and the torso twists against the stride; the head lags.
    pose["move"] = (0.35 * w(t + 0.25), 0.0, -CROUCH - 0.25 * impact + 0.08 * w(t * 2 - 0.1))
    add(pose, "Body", 3 * impact, 6 * w(t + 0.25), 7 * w(t))
    add(pose, "Bone_001", 2 * impact, 2 * w(t + 0.2), -4 * w(t - 0.05))
    add(pose, "Bone_004", 2 * w(t * 2 - 0.2), -3 * w(t + 0.15), -7 * w(t - 0.1))
    add(pose, "Bone_003", 0, -2 * w(t + 0.1), -5 * w(t - 0.15))
    add(pose, HEAD[0], -5 * impact + 3 * w(t * 2 - 0.3), 0, 6 * w(t - 0.25))
    # The clawed arm swings heavily against the legs; the tentacle arm drags, swaying.
    wave_down(pose, CLAW_ARM[:3], t + 0.5, 10, 0.08)
    add(pose, CLAW_ARM[0], 0, 0, 6 * w(t + 0.4))
    wave_down(pose, WHIP_ARM, t, 6, 0.1, axis=2)
    writhe(pose, t)
    for name, contact in CONTACT.items():
        stride = SPEED * CYCLE * DUTY
        offset, planted = cr.step(t, PHASE[name], DUTY, stride, 0.55)
        u = ((t - PHASE[name]) % 1.0 - DUTY) / (1 - DUTY)
        turn = (0, 0, 0) if planted else (-20 * math.sin(math.pi * u), 0, 0)
        pose["feet"][name] = cr.target_for(name, contact + offset, turn)
        pose["foot_turn"][name] = turn
    return pose


def still(t):
    pose = stance()
    w = cr.wave
    pose["move"] = (0, 0, -CROUCH - 0.06 * w(t))
    add(pose, "Bone_004", 2 * w(t), 0, 2 * w(t * 0.5))
    add(pose, HEAD[0], 2 * w(t + 0.2), 0, 5 * w(t * 0.5 - 0.1))
    writhe(pose, t, 0.7, 1)
    return pose


def whip_at(t, start, length=0.2):
    """The single whip of the tentacle arm: drawn back, then a crack running down it to the tip, over length of the clip from start."""
    u = (t - start) / length
    if u < 0 or u > 1:
        return None
    back = math.sin(math.pi * min(u / 0.4, 1)) if u < 0.4 else 0.0
    crack = math.sin(math.pi * (u - 0.35) / 0.65) if u > 0.35 else 0.0
    return back, crack, u


def attack(t):
    """A whip; then it rears, arms raised high (a long anticipation); the slam (SLAM_MOMENT); it heaves up again; another whip."""
    pose = stance()
    if t < 0.22:
        raised, slammed = 0.0, 0.0
    elif t < 0.55:
        raised, slammed = cr.ease((t - 0.22) / 0.33), 0.0
    elif t < SLAM_MOMENT:
        s = (t - 0.55) / (SLAM_MOMENT - 0.55)
        raised, slammed = 1 - s, s ** 0.6
    else:
        raised, slammed = 0.0, 1 - cr.ease((t - SLAM_MOMENT) / 0.3)
    shake = math.exp(-max(t - SLAM_MOMENT, 0) * 25) * math.sin(max(t - SLAM_MOMENT, 0) * 80) if t > SLAM_MOMENT else 0.0
    pose["move"] = (0, 0.4 * raised - 0.9 * slammed, -CROUCH + 0.5 * raised - 0.7 * slammed + 0.15 * shake)
    # Rearing back, arms high; then pitching forward over the arms as they crash down.
    add(pose, "Body", -10 * raised + 18 * slammed, 0, -6 * raised + 4 * slammed)
    add(pose, "Bone_004", -12 * raised + 14 * slammed + 3 * shake)
    add(pose, "Bone_003", -10 * raised + 10 * slammed)
    add(pose, HEAD[0], -18 * raised + 14 * slammed)
    add(pose, CLAW_ARM[0], -70 * raised + 25 * slammed, 0, 25 * raised - 10 * slammed)
    add(pose, CLAW_ARM[1], -45 * raised + 30 * slammed)
    add(pose, CLAW_ARM[2], -20 * raised + 25 * slammed)
    add(pose, WHIP_ARM[0], -55 * raised + 20 * slammed, 0, -20 * raised)
    add(pose, WHIP_ARM[1], -40 * raised + 25 * slammed)
    add(pose, WHIP_ARM[2], -25 * raised + 20 * slammed)
    for start in (0.02, 0.8):
        lash = whip_at(t, start, 0.18)
        if lash:
            back, crack, u = lash
            # The tentacle arm swings up and back, then cracks forward and across, the crack running down to its tendrils.
            for i, name in enumerate(WHIP_ARM):
                k = math.sin(math.pi * min(max(u * 1.6 - i * 0.12, 0), 1))
                add(pose, name, (30 * back - 45 * crack * k) * (1 if i < 3 else 0.6), 0, (-20 * back + 30 * crack * k) * 0.6)
            for chain in TENDRILS:
                wave_down(pose, chain, u * 2, 30 * crack, 0.15, axis=2)
            add(pose, "Bone_004", 0, 0, 10 * back - 14 * crack)
    writhe(pose, t, 1.0 + 0.6 * raised, 2)
    return pose


def fall(t):
    """Its legs give; it sinks to its knees, then pitches forward onto its arms and face, the tendrils going still."""
    knees = cr.ease(t / 0.45)
    pitch = cr.ease((t - 0.3) / 0.55)
    pose = stance()
    pose["move"] = (0, -1.2 * pitch, -CROUCH - 2.2 * knees - 1.6 * pitch)
    add(pose, "Body", 62 * pitch, 0, 6 * pitch)
    add(pose, "Bone_004", 10 * pitch)
    add(pose, HEAD[0], 20 * knees)
    add(pose, CLAW_ARM[0], -40 * pitch, 0, 20 * pitch)
    add(pose, WHIP_ARM[0], -30 * pitch, 0, -20 * pitch)
    writhe(pose, t * 0.6, max(0.0, 1 - t * 1.4), 2)
    for name, contact in CONTACT.items():
        out = 1 if contact.x > -1.3 else -1
        pose["feet"][name] = cr.target_for(name, contact + Vector((0.5 * out * knees, 0.6 * knees, 0)), (-30 * knees, 0, 0))
        pose["foot_turn"][name] = (-30 * knees, 0, 0)
    return pose


CLIPS = os.environ.get("HB_CLIPS", "Walk,Still,Slam,Dead").split(",")
if "Walk" in CLIPS:
    cr.clip("Walk", round(CYCLE * FPS), walk)
if "Still" in CLIPS:
    cr.clip("Still", 3 * FPS, still)
if "Slam" in CLIPS:
    cr.clip("Slam", round(SLAM_SECONDS * FPS), attack)
if "Dead" in CLIPS:
    cr.clip("Dead", round(3.5 * FPS), fall, floor=True)
slide = cr.walk_slide("Walk", list(CONTACT), SPEED, floor=0.28) if "Walk" in CLIPS else None

SIDE, QUARTER = (1, 0.05, 0.12), (0.8, -1, 0.3)
CENTRE = (-0.5, -0.5, 4.5)
for clip_name, count in (("Walk", 8), ("Slam", 12), ("Dead", 8)):
    if clip_name not in CLIPS or clip_name not in os.environ.get("HB_PICTURES", "Walk,Slam,Dead").split(","):
        continue
    shares = [i / (count - 1) for i in range(count)] if clip_name == "Dead" else [i / count for i in range(count)]
    for view_name, view in (("side", SIDE), ("quarter", QUARTER)):
        cr.strip(cr.film(clip_name, f"{clip_name}_{view_name}", view, CENTRE, 30, shares=shares, size=(360, 360)), f"strip_{clip_name}_{view_name}", columns=4)
if ONLY:
    sys.exit(0)
cr.finish({"moving": "Walk", "shooting": "Slam", "dead": "Dead", "still": "Still"}, {"speed": SPEED, "walk_slide": slide, "shot_moment": SLAM_MOMENT})
