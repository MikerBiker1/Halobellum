# The Mantis, animated (D97): its static model (Mantis.py) rigged here so the crowd baker can play it, as the owner found it hardly moved
# (it only turned at the waist and swung its legs stiffly). A heavy two-legged walker: each stride a planted, weighty step, the hips
# dropping onto the foot that lands and the torso rolling over it, the knees taking the shock; the arms swing a little against the legs,
# each with its weight. To fire it brings both arms up to aim, the chaingun shuddering with every round (its 0.2 s) and the rocket arm
# kicking back as a rocket leaves, once a clip, the torso braced. It dies with its knees giving, toppling forward onto its arms.
# Sizes in metres at its height in the game (6 m), front toward -Y; its right (the chaingun's side) is -X. The static preparation sliced the
# skin at the waist and across the thighs (with caps), which the weights here simply follow.
#   blender --background --factory-startup --python Tools/Art/units/MantisAnimated.py
#   HB_ONLY=measure | bones | weights | clips (stop after that), HB_CLIPS and HB_PICTURES (which clips to make and film), to lay it out.
import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import creature_rig as cr  # noqa: E402

ONLY = os.environ.get("HB_ONLY", "")
FPS = 30
SPEED = 1.5  # Its move speed in the game, m/s.
HEIGHT = 6.0
CHAINGUN = 0.2  # Seconds between the chaingun's rounds: its main weapon, which the firing clip keeps time with.

# Lifted as RealModelBuilder lifted the static model: its texture is dark, and read as a black shape from the game camera.
mesh = cr.load("MantisAnimated", "Mantis", "Meshy/Meshy_AI_UNSC_Mantis.glb", height=HEIGHT, fps=FPS, lift=(1.3, 1.3, 1.3))

if ONLY == "measure":
    # Where the flesh is at each height: the body (|x| < 1.25) and each arm, to lay the bones out.
    points = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
    for step in range(25):
        z0, z1 = step * 0.25, step * 0.25 + 0.25
        band = [p for p in points if z0 <= p.z < z1]
        for name, test in (("right", lambda p: p.x < -1.25), ("body", lambda p: abs(p.x) <= 1.25), ("left", lambda p: p.x > 1.25)):
            part = [p for p in band if test(p)]
            if not part:
                continue
            xs, ys = [p.x for p in part], [p.y for p in part]
            print(f"REPORT z {z0:.2f} {name:5s} n {len(part):4d} x {min(xs):6.2f}..{max(xs):6.2f} y {min(ys):6.2f}..{max(ys):6.2f}")
    sys.exit(0)

HIP_X, KNEE_Z, ANKLE_Z = 0.62, 2.0, 0.44
spec = [
    ("Body", (0, -0.12, 2.95), (0, -0.12, 3.62), None, 0.75),
    ("Spine", (0, -0.12, 3.62), (0, -0.05, 4.3), "Body", 0.8),
    ("Chest", (0, -0.05, 4.3), (0, 0.0, 5.2), "Spine", 1.0),
    ("Head", (0, -0.05, 5.2), (0, -0.25, 5.95), "Chest", 0.45),
    ("RightArm", (-1.45, -0.05, 4.8), (-1.75, -0.28, 3.45), "Chest", 0.45),
    ("RightForeArm", (-1.75, -0.28, 3.45), (-1.88, -0.95, 2.05), "RightArm", 0.42),
    ("RightGun", (-1.88, -0.95, 2.05), (-1.92, -1.32, 1.5), "RightForeArm", 0.25),
    ("Muzzle", (-1.92, -1.36, 1.44), (-1.92, -1.5, 1.3), "RightGun"),
    ("LeftArm", (1.45, -0.05, 4.8), (1.75, -0.22, 3.4), "Chest", 0.45),
    ("LeftForeArm", (1.75, -0.22, 3.4), (1.86, -0.5, 2.15), "LeftArm", 0.48),
    ("LeftHand", (1.86, -0.5, 2.15), (1.9, -0.82, 1.72), "LeftForeArm", 0.25),
    # The rocket pod's mouth, on the forearm: where the second weapon's shots leave. Named Muzzle..., it moves no flesh.
    ("MuzzleRockets", (1.97, -0.62, 2.95), (1.97, -0.9, 2.95), "LeftForeArm"),
    ("RightUpLeg", (-HIP_X, -0.15, 3.12), (-HIP_X, -0.2, KNEE_Z), "Body", 0.5),
    ("RightLeg", (-HIP_X, -0.2, KNEE_Z), (-HIP_X, 0.0, ANKLE_Z), "RightUpLeg", 0.5),
    ("RightFoot", (-HIP_X, 0.0, ANKLE_Z), (-HIP_X, -0.85, 0.06), "RightLeg", 0.5),
    ("LeftUpLeg", (HIP_X, -0.15, 3.12), (HIP_X, -0.2, KNEE_Z), "Body", 0.5),
    ("LeftLeg", (HIP_X, -0.2, KNEE_Z), (HIP_X, 0.0, ANKLE_Z), "LeftUpLeg", 0.5),
    ("LeftFoot", (HIP_X, 0.0, ANKLE_Z), (HIP_X, -0.85, 0.06), "LeftLeg", 0.5),
]
arm = cr.skeleton(spec)
if ONLY in ("bones", ""):
    cr.bone_picture("bones")
if ONLY == "bones":
    sys.exit(0)

print("REPORT vertices per bone:", cr.weigh(blend=0.25, smooth=3, whole_below=40))
for name, direction in (("weights_front", (0.2, -1, 0.15)), ("weights_side", (1, 0, 0.1)), ("weights_back", (-0.3, 1, 0.3))):
    cr.weight_picture(name, direction, (0, 0, 3.0), 15)
if ONLY == "weights":
    sys.exit(0)

# ---- Clips ----
cr.leg("legR", "RightUpLeg", "RightLeg", "RightFoot", pole=(0, -1, 0))
cr.leg("legL", "LeftUpLeg", "LeftLeg", "LeftFoot", pole=(0, -1, 0))
CONTACT = {"legR": cr.contact_of("legR"), "legL": cr.contact_of("legL")}
SIDE = {"legR": -1.0, "legL": 1.0}
CROUCH = 0.14  # Its legs are straight in the model: it walks a little lower, knees bent, so they have room to reach.
CYCLE = 1.6  # Seconds per stride: a big machine's slow, heavy pace.
DUTY = 0.62  # Each foot carries weight three fifths of the stride; both are down for a moment as each lands.
PHASE = {"legR": 0.0, "legL": 0.5}
LIFT = 0.34


def add(pose, bone, x=0.0, y=0.0, z=0.0):
    old = pose["turn"].get(bone, (0, 0, 0))
    pose["turn"][bone] = (old[0] + x, old[1] + y, old[2] + z)


def stance(width=0.0, crouch=CROUCH):
    pose = {"move": (0, 0, -crouch), "turn": {}, "feet": {}, "foot_turn": {}}
    for name, contact in CONTACT.items():
        pose["feet"][name] = cr.target_for(name, contact + Vector((SIDE[name] * width, 0, 0)))
        pose["foot_turn"][name] = (0, 0, 0)
    return pose


def thud(t, at, width=0.05):
    """1 as a foot lands at this share of the stride, falling away either side."""
    d = (t - at + 0.5) % 1.0 - 0.5
    return math.exp(-(d / width) ** 2)


def walk(t):
    pose = stance()
    w = cr.wave
    land = thud(t, 0.0) + thud(t, 0.5)
    # The weight goes over the planted foot; the body drops as each foot lands and rises over it; the hips turn with the legs and the
    # chest turns back against them; the arms swing a little the other way, the head stays steady.
    sway = -0.11 * math.sin(2 * math.pi * t)
    rise = 0.05 * math.cos(4 * math.pi * (t - 0.06))
    pose["move"] = (sway, 0.0, -CROUCH - 0.09 * land + rise)
    add(pose, "Body", 2.5 * land, 3.0 * math.sin(2 * math.pi * t), 6 * math.sin(2 * math.pi * (t + 0.25)))
    add(pose, "Spine", -1.5 * land, -1.5 * math.sin(2 * math.pi * t), -4 * math.sin(2 * math.pi * (t + 0.25)))
    add(pose, "Chest", 1.0 * land, -1.0 * math.sin(2 * math.pi * t), -3 * math.sin(2 * math.pi * (t + 0.25)))
    add(pose, "Head", -2.5 * land + 1.5 * w(t * 2 + 0.1), 0.5 * math.sin(2 * math.pi * t), 1.5 * math.sin(2 * math.pi * (t - 0.1)))
    swing = math.sin(2 * math.pi * (t + 0.25))  # +1 as the right leg is furthest forward: then the left arm is.
    add(pose, "RightArm", 7 * swing - 3, 0, 2)
    add(pose, "RightForeArm", -4 - 3 * max(0.0, swing))
    add(pose, "LeftArm", -7 * swing - 3, 0, -2)
    add(pose, "LeftForeArm", -4 - 3 * max(0.0, -swing))
    # Weight in the arms too: they jolt down a little as each foot lands.
    add(pose, "RightForeArm", 3 * land)
    add(pose, "LeftForeArm", 3 * land)
    for name, contact in CONTACT.items():
        stride = SPEED * CYCLE * DUTY
        offset, planted = cr.step(t, PHASE[name], DUTY, stride, LIFT)
        u = ((t - PHASE[name]) % 1.0 - DUTY) / (1 - DUTY)
        # In the air the foot pitches its toe up as it swings through and comes down flat.
        turn = (0, 0, 0) if planted else (12 * math.sin(math.pi * u) - 6 * math.sin(2 * math.pi * u), 0, 0)
        pose["feet"][name] = cr.target_for(name, contact + offset, turn)
        pose["foot_turn"][name] = turn
    return pose


def idle(t):
    pose = stance()
    w = cr.wave
    pose["move"] = (0.02 * w(t), 0, -CROUCH - 0.025 * (0.5 + 0.5 * w(t)))
    add(pose, "Chest", 1.2 * w(t), 0, 2.5 * w(t * 0.5))
    add(pose, "Head", 2 * w(t + 0.2), 0, 10 * w(t * 0.5 - 0.1))  # It looks about.
    add(pose, "RightArm", -2 + 1.5 * w(t + 0.3))
    add(pose, "LeftArm", -2 + 1.5 * w(t + 0.6))
    return pose


ROUNDS = 12  # The firing clip is twelve rounds of the chaingun long.
FIRE_SECONDS = ROUNDS * CHAINGUN
SHOT_MOMENT = 0.5 / ROUNDS  # The first round goes in the middle of its slot; the clip is timed to the chaingun by it.
ROCKET_AT = 0.5  # The rocket arm kicks once a clip.


def fire(t):
    """Braced, both arms up and aimed; the chaingun shudders with each round, the rocket arm kicks once and recovers."""
    pose = stance(width=0.12, crouch=CROUCH + 0.1)
    round_phase = (t * ROUNDS) % 1.0
    kick = math.exp(-round_phase / 0.18) * (1 - math.exp(-round_phase / 0.02))  # Each round: a sharp kick, quickly gone.
    rocket = 0.0
    d = t - ROCKET_AT
    if 0 <= d < 0.25:
        rocket = math.exp(-d / 0.06) * (1 - math.exp(-d / 0.01))
    w = cr.wave
    pose["move"] = (0.0, 0.03 * kick + 0.12 * rocket, -CROUCH - 0.1 - 0.01 * kick)
    add(pose, "Body", 3 + 0.4 * kick, -1.5 * rocket, -3)
    add(pose, "Spine", 2 - 1.5 * rocket, 0, -4)
    add(pose, "Chest", 1 - 0.8 * kick - 3 * rocket, 0.6 * kick, -2 + 0.5 * w(t))
    add(pose, "Head", -4 - 1 * rocket, 0, 4)
    # The chaingun arm: up and in, the gun level and pointing ahead; each round knocks it back and up a little.
    add(pose, "RightArm", -32 + 2.5 * kick, 0.8 * kick, 9)
    add(pose, "RightForeArm", -36 + 4 * kick, 0, 4)
    add(pose, "RightGun", -6 + 2 * kick)
    # The rocket arm: up, the pod aimed; it kicks up and back as the rocket leaves.
    add(pose, "LeftArm", -28 + 10 * rocket, 0, -8)
    add(pose, "LeftForeArm", -34 + 16 * rocket, 0, -4)
    add(pose, "LeftHand", -10 + 6 * rocket)
    return pose


def fall(t):
    """The knees give, it sinks, pitches forward and topples onto its arms, the head dropping."""
    s = cr.ease(t / 0.8)
    knees = cr.ease(t / 0.35)
    pose = stance(width=0.05)
    # It goes down flat on its chest, the arms flung out and limp, nothing left holding it up.
    pose["move"] = (0.05 * s, -2.0 * s, -CROUCH - 0.9 * knees - 2.2 * s)
    add(pose, "Body", 86 * s, 6 * s, 4 * s)
    add(pose, "Spine", 4 * s, 0, 0)
    add(pose, "Head", 14 * s, 0, 18 * s)
    add(pose, "RightArm", 10 * s, 30 * s, 0)
    add(pose, "RightForeArm", 40 * s)
    add(pose, "LeftArm", 12 * s, -32 * s, 0)
    add(pose, "LeftForeArm", 35 * s)
    # The feet stay where they are until the body has gone over, then the legs trail out behind it.
    pose["feet"]["legR"] = cr.target_for("legR", CONTACT["legR"] + Vector((-0.1, 1.2 * s * s, 0.3 * s * s)), (-50 * s, 0, 0))
    pose["feet"]["legL"] = cr.target_for("legL", CONTACT["legL"] + Vector((0.15, 1.0 * s * s, 0.25 * s * s)), (-45 * s, 0, 0))
    return pose


CLIPS = os.environ.get("HB_CLIPS", "Walk,Idle,Fire,Dead").split(",")
if "Walk" in CLIPS:
    cr.clip("Walk", round(CYCLE * FPS), walk)
if "Idle" in CLIPS:
    cr.clip("Idle", 3 * FPS, idle)
if "Fire" in CLIPS:
    cr.clip("Fire", round(FIRE_SECONDS * FPS), fire)
if "Dead" in CLIPS:
    # Only the body and legs hold it off the ground as it lies: its limp arms may sink a little into it rather than prop it up.
    ARMS = ("RightArm", "RightForeArm", "RightGun", "LeftArm", "LeftForeArm", "LeftHand")
    groups = [g.name for g in mesh.vertex_groups]
    not_arms = [v.index for v in mesh.data.vertices if v.groups and groups[max(v.groups, key=lambda g: g.weight).group] not in ARMS]
    cr.clip("Dead", round(2.2 * FPS), fall, floor=not_arms)
slide = cr.walk_slide("Walk", list(CONTACT), SPEED, floor=0.06) if "Walk" in CLIPS else None

SIDE_VIEW, QUARTER = (1, 0.05, 0.12), (0.8, -1, 0.35)
CENTRE = (0, -0.3, 3.0)
for clip_name, count in (("Walk", 8), ("Fire", 8), ("Dead", 8), ("Idle", 4)):
    if clip_name not in CLIPS or clip_name not in os.environ.get("HB_PICTURES", "Walk,Fire,Dead").split(","):
        continue
    shares = [i / (count - 1) for i in range(count)] if clip_name == "Dead" else [i / count for i in range(count)]
    for view_name, view in (("side", SIDE_VIEW), ("quarter", QUARTER)):
        cr.strip(cr.film(clip_name, f"{clip_name}_{view_name}", view, CENTRE, 17, shares=shares, size=(300, 360)), f"strip_{clip_name}_{view_name}", columns=8)
if os.environ.get("HB_BIG"):
    for clip_name, view_name, view in (("Fire", "front", (0.25, -1, 0.15)), ("Fire", "side", SIDE_VIEW), ("Walk", "front", (0.25, -1, 0.15))):
        if clip_name in CLIPS:
            cr.strip(cr.film(clip_name, f"big_{clip_name}_{view_name}", view, CENTRE, 13, shares=[0.02, 0.27, 0.52, 0.77], size=(420, 520)), f"big_{clip_name}_{view_name}", columns=4)
if ONLY:
    sys.exit(0)
cr.finish({"moving": "Walk", "shooting": "Fire", "dead": "Dead", "still": "Idle"}, {"speed": SPEED, "walk_slide": slide, "shot_moment": SHOT_MOMENT})
