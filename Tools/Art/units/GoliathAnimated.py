# The Goliath, animated (D139): its static model (Goliath.py) rigged here so the crowd baker can play it. The owner found its old animation
# really bad (four limbs swinging stiffly from the shoulder, as Meshy's rig, made for a man, tore its armour) and made it melee only, with a
# charge like the Hunters'. A great beast that walks on its knuckles and two short hind legs:
#   Walk    a heavy knuckle-walk, each fist and foot planted in turn (left hind, left fist, right hind, right fist), shoulders rolling;
#   Charge  a bounding gallop, both fists reaching out and landing together, the hind legs driving after them;
#   Slam    its strike: rearing back on its hind legs, both fists raised over its head, then brought down together on the ground in front;
#   Idle    standing on its knuckles, breathing, looking about;
#   Dead    its arms give way, its chest crashes down and it rolls onto its side.
#   blender --background --factory-startup --python Tools/Art/units/GoliathAnimated.py
#   HB_ONLY=measure | bones | weights | clips (stop after that), HB_CLIPS and HB_PICTURES (which clips to make and film), to lay it out.
import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import creature_rig as cr  # noqa: E402

ONLY = os.environ.get("HB_ONLY", "")
FPS = 30
HEIGHT = 6.0  # Its height in the game, as RealModelBuilder sized its static model.

mesh = cr.load("GoliathAnimated", "Goliath", "Meshy/Meshy_AI_Goliath.glb", height=HEIGHT, fps=FPS)

if ONLY == "measure":
    import numpy as np
    co = np.array([v.co[:] for v in mesh.data.vertices])
    print("REPORT bounds", co.min(0).round(2), co.max(0).round(2))
    for z0 in np.arange(0, co[:, 2].max() + 0.25, 0.25):
        s = co[(co[:, 2] >= z0) & (co[:, 2] < z0 + 0.25)]
        if len(s):
            print(f"REPORT z {z0:4.2f}: n {len(s):5d} x {s[:, 0].min():6.2f}..{s[:, 0].max():6.2f} y {s[:, 1].min():6.2f}..{s[:, 1].max():6.2f}")
    cr.skeleton([("Body", (0, 0.3, 1.6), (0, -0.2, 2.4), None, 0.5)])
    cr.bone_picture("measure", step=0.25)
    sys.exit(0)

# Metres in the game, front toward -Y, its left +X. The arms are thick columns from the shoulder to a fist on the ground in front of it; the
# hind legs are short, its left one stretched back in the model's stride.
ARM = {  # Shoulder, elbow (behind the line, as a knuckle-walker's), wrist, and the knuckles it stands on.
    "R": [(-1.52, -0.9, 3.0), (-1.9, -0.6, 1.8), (-1.95, -1.3, 0.72), (-1.9, -1.85, 0.06)],
    "L": [(1.52, -0.85, 3.0), (1.95, -0.55, 1.8), (2.0, -1.15, 0.72), (2.05, -1.7, 0.06)],
}
LEG = {  # Hip, knee (forward), ankle, toes.
    "R": [(-0.68, 0.58, 1.67), (-0.88, 0.15, 1.05), (-0.92, 0.58, 0.42), (-0.95, -0.22, 0.04)],
    "L": [(0.68, 0.68, 1.67), (0.78, 1.15, 0.72), (0.85, 2.12, 0.58), (0.88, 1.62, 0.04)],
}
spec = [
    ("Body", (0, 0.9, 1.85), (0, -0.15, 2.7), None, 0.9),
    ("Chest", (0, -0.15, 2.7), (0, -1.0, 3.25), "Body", 1.0),
    ("Neck", (0, -1.1, 3.05), (0, -1.75, 2.85), "Chest", 0.55),
    ("Head", (0, -1.75, 2.85), (0, -3.4, 2.65), "Neck", 0.6),
]
for side, sign in (("R", -1), ("L", 1)):
    s, e, w, k = ARM[side]
    spec += [
        ("Shoulder" + side, (sign * 0.45, -0.85, 3.2), s, "Chest", 0.6),
        ("Arm" + side + "_0", s, e, "Shoulder" + side, 0.6),
        ("Arm" + side + "_1", e, w, "Arm" + side + "_0", 0.6),
        ("Fist" + side, w, k, "Arm" + side + "_1", 0.5),
    ]
    h, n, a, t = LEG[side]
    spec += [
        ("Leg" + side + "_0", h, n, "Body", 0.45),
        ("Leg" + side + "_1", n, a, "Leg" + side + "_0", 0.4),
        ("Foot" + side, a, t, "Leg" + side + "_1", 0.35),
    ]
arm = cr.skeleton(spec)
if ONLY in ("bones", ""):
    cr.bone_picture("bones", step=0.25)
if ONLY == "bones":
    sys.exit(0)

print("REPORT vertices per bone:", cr.weigh(blend=0.3, smooth=3, whole_below=60))
for name, direction in (("weights_front", (0.15, -1, 0.2)), ("weights_side", (1, 0.1, 0.15)), ("weights_back", (-0.3, 1, 0.4))):
    cr.weight_picture(name, direction, (0, 0, 2.5), 16)
if ONLY == "weights":
    sys.exit(0)

# ---- Clips ----
# Its fists are feet: each arm reaches its knuckles to a target, the elbow bending back; each hind leg its toes, the knee forward.
cr.leg("armR", "ArmR_0", "ArmR_1", "FistR", pole=(-0.25, 1, 0))
cr.leg("armL", "ArmL_0", "ArmL_1", "FistL", pole=(0.25, 1, 0))
cr.leg("legR", "LegR_0", "LegR_1", "FootR", pole=(-0.1, -1, 0))
cr.leg("legL", "LegL_0", "LegL_1", "FootL", pole=(0.1, -1, 0))

SPEED = 1.1  # Its move speed in the game, m/s.
CHARGE_SPEED = float(os.environ.get("HB_CHARGE_SPEED", "9"))  # Its charge, as the Hunters' leap (range 5, 9 m/s).
SLAM_SECONDS = float(os.environ.get("HB_SLAM_SECONDS", "2"))  # Its Fist Slam's interval: the Slam is one blow long.
SLAM_MOMENT = 0.5  # The share of the Slam at which both fists hit the ground.
# Where each fist and foot stands while it walks: the fists level, the hind feet under the hips (in the model the left one is stretched back).
CONTACT = {"armR": Vector((-1.92, -1.75, 0.06)), "armL": Vector((1.96, -1.75, 0.06)),
           "legR": Vector((-0.95, 0.1, 0.04)), "legL": Vector((0.95, 0.55, 0.04))}
WALK = dict(cycle=1.6, duty=0.7, phase={"legL": 0.0, "armL": 0.25, "legR": 0.5, "armR": 0.75}, lift={"arm": 0.38, "leg": 0.25})
GALLOP = dict(cycle=0.6, duty=0.32, phase={"legL": 0.0, "legR": 0.08, "armL": 0.5, "armR": 0.58}, lift={"arm": 0.6, "leg": 0.45})
LIMBS = ("armR", "armL", "legR", "legL")


def add(pose, bone, x=0.0, y=0.0, z=0.0):
    old = pose["turn"].get(bone, (0, 0, 0))
    pose["turn"][bone] = (old[0] + x, old[1] + y, old[2] + z)


def place(pose, name, contact, turn=(0, 0, 0)):
    pose["feet"][name] = cr.target_for(name, contact, turn)
    pose["foot_turn"][name] = turn


def stance():
    pose = {"move": (0, 0, 0), "turn": {}, "feet": {}, "foot_turn": {}}
    for name in LIMBS:
        place(pose, name, CONTACT[name])
    return pose


def gait(pose, t, g, speed, curl):
    """Each fist and foot planted for the duty of the cycle, sliding back at the unit's speed, then lifted and swung forward."""
    for name in LIMBS:
        offset, planted = cr.step(t, g["phase"][name], g["duty"], speed * g["cycle"] * g["duty"], g["lift"][name[:3]])
        u = ((t - g["phase"][name]) % 1.0 - g["duty"]) / (1 - g["duty"])
        turn = (0, 0, 0) if planted else (curl[name[:3]] * math.sin(math.pi * u), 0, 0)
        place(pose, name, CONTACT[name] + offset, turn)


def walk(t):
    """A heavy knuckle-walk: left hind, left fist, right hind, right fist. The body sinks onto each fist as it lands and rolls over it,
    the shoulder of the swinging arm coming forward; the head keeps looking ahead."""
    w = cr.wave
    pose = stance()
    gait(pose, t, WALK, SPEED, {"arm": 28, "leg": -20})
    land = math.cos(4 * math.pi * (t - 0.33))  # 1 just after each fist lands, when it takes the weight.
    swing = math.cos(2 * math.pi * (t - 0.6))  # 1 while the right arm swings (the left carries it), -1 the left.
    pose["move"] = (0.06 * swing, 0.04 * land, -0.07 - 0.07 * land)
    add(pose, "Body", 3 + 1.5 * land, 2.5 * swing, -2.5 * swing)
    add(pose, "Chest", 2 * land, 2 * swing, 7 * swing)
    add(pose, "Head", -2 - 2 * math.cos(4 * math.pi * (t - 0.4)), -2 * swing, -6 * swing + 2 * w(t))
    return pose


def charge(t):
    """A bounding gallop: the hind legs land and drive, both fists reach far out and land together, the body pitching over them, head low."""
    pose = stance()
    gait(pose, t, GALLOP, CHARGE_SPEED, {"arm": 45, "leg": -35})
    pitch = math.cos(2 * math.pi * (t - 0.62))  # 1 as the fists land and take it, -1 as the hind legs drive.
    pose["move"] = (0, 0.12 * pitch, -0.12 + 0.16 * math.cos(4 * math.pi * (t - 0.4)))
    add(pose, "Body", 9 + 7 * pitch, 0, 2 * cr.wave(t))
    add(pose, "Chest", 6 + 5 * pitch, 0, 4 * cr.wave(t - 0.55))
    add(pose, "Head", 6 - 6 * pitch, 0, 0)
    return pose


def slam(t):
    """Rearing back onto its hind legs, both fists raised high over its head (to 0.38); brought down together on the ground in front,
    the body crashing after them (SLAM_MOMENT); a shudder, then it settles back onto its knuckles."""
    if t < 0.38:
        up, down = cr.ease(t / 0.38), 0.0
    elif t < SLAM_MOMENT:
        s = cr.ease((t - 0.38) / (SLAM_MOMENT - 0.38))
        up, down = 1 - s, s
    else:
        up, down = 0.0, 1 - cr.ease((t - 0.62) / 0.38)
    after = max(0.0, t - SLAM_MOMENT)
    shudder = 0.04 * math.exp(-14 * after) * math.sin(after * 70) if t >= SLAM_MOMENT else 0.0
    pose = stance()
    pose["move"] = (0, 0.4 * up - 0.3 * down, 0.3 * up - 0.28 * down + shudder)
    add(pose, "Body", -30 * up + 14 * down, 0, 0)
    add(pose, "Chest", -12 * up + 8 * down, 0, 0)
    add(pose, "Head", 14 * up - 4 * down, 0, 0)
    for name, sign in (("armR", -1), ("armL", 1)):
        raised = Vector((sign * 1.3, -0.45, 5.55))
        hit = Vector((sign * 1.7, -2.65, 0.06))
        rest = CONTACT[name]
        place(pose, name, rest * (1 - up - down) + raised * up + hit * down, (-120 * up + 15 * down, 0, sign * 10 * up))
    return pose


def idle(t):
    """On its knuckles, breathing slowly, looking one way and the other."""
    w = cr.wave
    pose = stance()
    pose["move"] = (0, 0, -0.03 - 0.03 * w(t))
    add(pose, "Body", 1.5 * w(t), 0, 0)
    add(pose, "Chest", 2 * w(t - 0.1), 0, 0)
    add(pose, "Head", 3 * w(t + 0.2), 2 * w(t + 0.1), 12 * w(t + 0.4))
    return pose


def fall(t):
    """Its arms give way, splaying out; the chest crashes down, head first; then it rolls onto its left side, the right arm falling over."""
    s1 = cr.ease(t / 0.35)
    s2 = cr.ease((t - 0.3) / 0.45)
    bounce = 0.12 * math.sin(math.pi * min(max((t - 0.32) / 0.14, 0), 1))
    pose = stance()
    pose["move"] = (0.7 * s2, -0.35 * s1, -1.0 * s1 - 0.55 * s2 + bounce)
    add(pose, "Body", 16 * s1 - 6 * s2, 55 * s2, 0)
    add(pose, "Chest", 8 * s1, 10 * s2, 0)
    add(pose, "Head", 14 * s1, 8 * s2, -10 * s2)
    place(pose, "armR", CONTACT["armR"] + Vector((-0.7 * s1 + 1.3 * s2, -0.3 * s1, 1.7 * s2)), (30 * s1, 0, 0))
    place(pose, "armL", CONTACT["armL"] + Vector((0.8 * s1 + 0.3 * s2, -0.3 * s1, 0)), (30 * s1, 0, 0))
    place(pose, "legR", CONTACT["legR"] + Vector((-0.3 * s1 + 0.6 * s2, 0.1, 0.5 * s2)))
    place(pose, "legL", CONTACT["legL"] + Vector((0.3 * s1, 0.2, 0)))
    return pose


CLIPS = os.environ.get("HB_CLIPS", "Walk,Charge,Slam,Idle,Dead").split(",")
if "Walk" in CLIPS:
    cr.clip("Walk", round(WALK["cycle"] * FPS), walk)
if "Charge" in CLIPS:
    cr.clip("Charge", round(GALLOP["cycle"] * FPS), charge)
if "Slam" in CLIPS:
    cr.clip("Slam", round(SLAM_SECONDS * FPS), slam)
if "Idle" in CLIPS:
    cr.clip("Idle", 3 * FPS, idle)
if "Dead" in CLIPS:
    cr.clip("Dead", round(2.5 * FPS), fall, floor=True)
slide = cr.walk_slide("Walk", list(LIMBS), SPEED, floor=0.08) if "Walk" in CLIPS else None
charge_slide = cr.walk_slide("Charge", list(LIMBS), CHARGE_SPEED, floor=0.1) if "Charge" in CLIPS else None

SIDE, FRONT, GAME = (1, 0.05, 0.12), (0.25, -1, 0.25), (0.6, -1, 1.1)
CENTRE = (0, -0.3, 2.6)
for clip_name, count in (("Walk", 8), ("Charge", 8), ("Slam", 12), ("Dead", 8), ("Idle", 4)):
    if clip_name not in CLIPS or clip_name not in os.environ.get("HB_PICTURES", "Walk,Charge,Slam,Dead,Idle").split(","):
        continue
    shares = [i / (count - 1) for i in range(count)] if clip_name == "Dead" else [i / count for i in range(count)]
    for view_name, view in (("side", SIDE), ("front", FRONT), ("game", GAME)):
        cr.strip(cr.film(clip_name, f"{clip_name}_{view_name}", view, CENTRE, 10.5, shares=shares, size=(360, 360)), f"strip_{clip_name}_{view_name}",
                 columns=4)
if ONLY:
    sys.exit(0)
cr.finish({"moving": "Walk", "shooting": "Slam", "dead": "Dead", "still": "Idle", "charge": "Charge"},
          {"speed": SPEED, "walk_slide": slide, "charge_speed": CHARGE_SPEED, "charge_slide": charge_slide, "shot_moment": SLAM_MOMENT})
