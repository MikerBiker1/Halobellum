# The Pure Form Ranged, animated (D90): its static model (PureFormRanged.py) rigged here, as Meshy refused it, so the crowd baker can play it.
# A tall spined column on a squat hub, two shoulder arms (a hooked claw on its right one), a jaw whose tusks reach the ground, two stubby
# legs behind. It crawls lopsided: its right leg reaches long and slow, its left hitches short and quick, the arms paw in turn with the
# legs, the hub lurches, the column sways late and its top shudders. To shoot it recoils, the column arching back and the arms spreading,
# then thrusts forward hard and lobs its shard from the top of the column (Muzzle, the baker's muzzleBone) at the end of the thrust; it
# shudders and settles. It dies with the column toppling over. Sizes in metres at its height in the game (2.9 m), front toward -Y.
# The model is lopsided, so its middle line is at x = 0.105, not 0. Its left is +X.
#   blender --background --factory-startup --python Tools/Art/units/PureFormRangedAnimated.py
#   HB_ONLY=bones | weights | clips (stop after that), HB_CLIPS and HB_PICTURES (which clips to make and film), to lay it out.
import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import creature_rig as cr  # noqa: E402

ONLY = os.environ.get("HB_ONLY", "")
FPS = 30
SPEED = 1.0  # Its move speed in the game, m/s.
SHOT_SECONDS = 3.0  # Bone Shards' fire interval: the shooting clip is one shot long.
SHOT_MOMENT = 0.6  # The share of the shooting clip at which the shard leaves: the end of the thrust.
MID = 0.105  # Its middle line.

mesh = cr.load("PureFormRangedAnimated", "PureFormRanged", "sketchfab.com/enemieshalo_3the_floodpure_ranged.glb", height=2.9, lift=(1.6, 1.6, 1.45), fps=FPS)
cr.double_sided()  # The fronds under its jaw.


def mirror(point):
    """The same point on its other side, across its middle line."""
    return (2 * MID - point[0], point[1], point[2])


spec = [
    ("Body", (MID, 0.2, 0.9), (MID, 0.25, 1.35), None, 0.35),
    ("Col_0", (MID, 0.25, 1.35), (MID, 0.5, 1.75), "Body", 0.42),
    ("Col_1", (MID, 0.5, 1.75), (MID, 0.52, 2.15), "Col_0", 0.42),
    ("Col_2", (MID, 0.52, 2.15), (MID, 0.5, 2.5), "Col_1", 0.4),
    ("Col_3", (MID, 0.5, 2.5), (MID, 0.56, 2.92), "Col_2", 0.35),
    ("Muzzle", (MID, -0.1, 2.55), (MID, -0.3, 2.55), "Col_3"),
    ("Sack", (MID, 0.35, 1.1), (MID, 0.95, 1.15), "Body", 0.32),
    ("Jaw", (MID, 0.05, 0.9), (MID, -0.68, 0.08), "Body", 0.25),
    ("Arm_L_0", (0.25, 0.0, 1.22), (0.6, -0.03, 1.2), "Body", 0.16),
    ("Arm_L_1", (0.6, -0.03, 1.2), (0.97, -0.06, 1.15), "Arm_L_0", 0.15),
    ("Arm_R_0", (-0.04, 0.0, 1.22), (-0.4, -0.03, 1.2), "Body", 0.16),
    ("Arm_R_1", (-0.4, -0.03, 1.2), (-0.78, -0.06, 1.15), "Arm_R_0", 0.15),
    ("Hook", (-0.35, 0.38, 1.45), (-1.0, 0.3, 1.15), "Arm_R_0", 0.15),
]
LEG = {"hip": (-0.3, 0.15, 0.95), "knee": (-0.55, 0.68, 0.55), "ankle": (-0.6, 0.8, 0.22), "toe": (-0.66, 0.93, 0.0)}
for side, flip in (("R", False), ("L", True)):
    p = {k: (mirror(v) if flip else v) for k, v in LEG.items()}
    spec += [
        (f"Leg_{side}_0", p["hip"], p["knee"], "Body", 0.15),
        (f"Leg_{side}_1", p["knee"], p["ankle"], f"Leg_{side}_0", 0.14),
        (f"Foot_{side}", p["ankle"], p["toe"], f"Leg_{side}_1", 0.16),
    ]
arm = cr.skeleton(spec)
if ONLY in ("bones", ""):
    cr.bone_picture("bones")
if ONLY == "bones":
    sys.exit(0)

# The jaw with its tusks, the hook, the spines and the fronds are each a separate piece and go whole to one bone.
print("REPORT vertices per bone:", cr.weigh(blend=0.3, smooth=3, whole_below=230, pieces_only=("Jaw", "Hook")))
for name, direction in (("weights_front", (0.3, -1, 0.3)), ("weights_back", (-0.7, 1, 0.4)), ("weights_side", (1, 0, 0.1))):
    cr.weight_picture(name, direction, (MID, 0, 1.3), 8)
if ONLY == "weights":
    sys.exit(0)

# ---- Clips ----
for side in ("L", "R"):
    cr.leg(f"leg_{side}", f"Leg_{side}_0", f"Leg_{side}_1", f"Foot_{side}", pole=(0.4 if side == "L" else -0.4, 1, 0))  # Knees back and out.

CROUCH = 0.22  # It squats a little lower than its model, its legs bent under it.
CONTACT = {"leg_R": Vector((-0.6, 0.5, 0)), "leg_L": Vector(mirror((-0.6, 0.5, 0)))}
CYCLE = 1.5  # Seconds per crawl cycle; the clip holds one.
GAIT = {"leg_R": (0.0, 0.66, 0.2), "leg_L": (0.43, 0.82, 0.13)}  # phase, duty, lift: the right foot reaches long and slow, the left hitches short and fast.


def add(pose, bone, x=0.0, y=0.0, z=0.0):
    old = pose["turn"].get(bone, (0, 0, 0))
    pose["turn"][bone] = (old[0] + x, old[1] + y, old[2] + z)


def stance():
    pose = {"move": (0, 0, -CROUCH), "turn": {"Jaw": (-14, 0, 0), "Col_0": (4, 0, 0)}, "feet": {}, "foot_turn": {}}
    for name, contact in CONTACT.items():
        pose["feet"][name] = cr.target_for(name, contact)
        pose["foot_turn"][name] = (0, 0, 0)
    return pose


def pulse(t, centre, width):
    """A bump of height 1 at centre (shares of the cycle, wrapping), width wide."""
    d = (t - centre + 0.5) % 1.0 - 0.5
    return math.exp(-(d / width) ** 2)


def crawl(t):
    pose = stance()
    w = cr.wave
    # Each step of the right leg throws the hub forward and round to the left in a heavy lurch; the left leg's hitch jerks it back, short.
    lurch = pulse(t, 0.75, 0.12)  # The right foot lands at 0.66 + its swing: the body heaves onto it.
    hitch = pulse(t, 0.12, 0.05)
    pose["move"] = (0.05 * lurch - 0.04 * hitch, 0.0, -CROUCH - 0.06 * lurch + 0.03 * hitch + 0.02 * w(t * 2))
    add(pose, "Body", 4 * lurch - 3 * hitch, -6 * lurch + 5 * hitch + 2 * w(t), 9 * lurch - 7 * hitch)
    # The column follows late, its top later still, and shudders after the lurch.
    shudder = math.exp(-((t - 0.82) % 1.0) * 7) * math.sin(((t - 0.82) % 1.0) * 70)
    add(pose, "Col_0", 3 * pulse(t, 0.85, 0.15) - 2, 5 * pulse(t, 0.85, 0.14) - 3 * pulse(t, 0.22, 0.08), -6 * pulse(t, 0.85, 0.14))
    add(pose, "Col_1", 4 * pulse(t, 0.92, 0.15), 6 * pulse(t, 0.92, 0.15) - 4 * pulse(t, 0.28, 0.08) + 2 * shudder, -5 * pulse(t, 0.92, 0.14))
    add(pose, "Col_2", 3 * shudder, 4 * pulse(t, 0.98, 0.15) + 3 * shudder, -3 * shudder)
    add(pose, "Col_3", -4 * shudder + 3 * w(t * 3), 3 * shudder, 6 * w(t + 0.2))
    add(pose, "Sack", 6 * w(t * 2 - 0.2), 4 * w(t - 0.3), 3 * lurch)
    # The arms paw in turn: the left pulls as the right foot swings, the right as the left hitches; the hook twitches.
    left_pull = pulse(t, 0.55, 0.14)
    right_pull = pulse(t, 0.06, 0.1)
    add(pose, "Arm_L_0", 22 * left_pull - 8, 0, 30 * left_pull - 10 * w(t))
    add(pose, "Arm_L_1", 35 * left_pull, 0, 20 * left_pull)
    add(pose, "Arm_R_0", 18 * right_pull - 6, 0, -25 * right_pull + 6 * w(t))
    add(pose, "Arm_R_1", 30 * right_pull, 0, -18 * right_pull)
    twitch = pulse(t, 0.33, 0.025) - pulse(t, 0.4, 0.025) + 0.6 * pulse(t, 0.9, 0.02)
    add(pose, "Hook", 25 * twitch, 0, 15 * twitch)
    # The jaw swings with the lurch, its tusks scraping forward over the ground and back.
    add(pose, "Jaw", -8 * lurch + 5 * hitch + 3 * w(t * 2), 0, 4 * w(t))
    for name, contact in CONTACT.items():
        phase, duty, lift = GAIT[name]
        stride = SPEED * CYCLE * duty
        offset, planted = cr.step(t, phase, duty, stride, lift)
        u = ((t - phase) % 1.0 - duty) / (1 - duty)
        turn = (0, 0, 0) if planted else (-30 * math.sin(math.pi * u), 0, 0)  # The claws drag behind as the foot lifts.
        pose["feet"][name] = cr.target_for(name, contact + offset, turn)
        pose["foot_turn"][name] = turn
    return pose


def idle(t):
    pose = stance()
    w = cr.wave
    pose["move"] = (0, 0, -CROUCH - 0.015 * w(t))
    add(pose, "Col_0", 1.5 * w(t), 1.5 * w(t * 0.5 - 0.1), 0)
    add(pose, "Col_1", 1.5 * w(t - 0.1), 2 * w(t * 0.5 - 0.2), 0)
    add(pose, "Col_3", 2 * w(t - 0.25), 0, 5 * w(t * 0.5 - 0.3))
    add(pose, "Hook", 12 * pulse(t, 0.6, 0.03), 0, 0)
    add(pose, "Arm_L_0", 3 * w(t + 0.1), 0, 0)
    add(pose, "Arm_R_0", 3 * w(t + 0.4), 0, 0)
    add(pose, "Sack", 4 * w(t * 2), 0, 0)
    return pose


def shoot(t):
    """One shot: it settles, recoils (0.22 to 0.55), thrusts forward hard (to the shot at SHOT_MOMENT), shudders and settles again."""
    if t < 0.22:
        back, thrust = 0.15 * cr.ease(t / 0.22), 0.0
    elif t < 0.55:
        back, thrust = 0.15 + 0.85 * cr.ease((t - 0.22) / 0.33), 0.0
    elif t < SHOT_MOMENT:
        s = (t - 0.55) / (SHOT_MOMENT - 0.55)
        back, thrust = 1.0 - s, s ** 0.6
    else:
        s = (t - SHOT_MOMENT) / (1 - SHOT_MOMENT)
        back, thrust = 0.0, (1 - cr.ease(s / 0.8)) if s < 0.8 else 0.0
    after = max(t - SHOT_MOMENT, 0.0)
    shudder = math.exp(-after * 14) * math.sin(after * 60) if t > SHOT_MOMENT else 0.0
    pose = stance()
    pose["move"] = (0, 0.28 * back - 0.3 * thrust, -CROUCH + 0.06 * back - 0.08 * thrust)
    add(pose, "Body", -12 * back + 14 * thrust, 0, 0)
    add(pose, "Col_0", -14 * back + 16 * thrust + 4 * shudder, 0, 0)
    add(pose, "Col_1", -12 * back + 14 * thrust + 6 * shudder, 2 * shudder, 0)
    add(pose, "Col_2", -10 * back + 12 * thrust + 8 * shudder, -3 * shudder, 0)
    add(pose, "Col_3", -12 * back + 18 * thrust + 10 * shudder, 0, 0)
    add(pose, "Sack", 10 * back - 12 * thrust, 0, 0)
    # Arms flung wide and up as it draws back, then thrown forward with the shot.
    add(pose, "Arm_L_0", -25 * back + 30 * thrust, 0, -25 * back + 25 * thrust)
    add(pose, "Arm_L_1", -20 * back + 25 * thrust, 0, -10 * back)
    add(pose, "Arm_R_0", -25 * back + 30 * thrust, 0, 25 * back - 25 * thrust)
    add(pose, "Arm_R_1", -20 * back + 25 * thrust, 0, 10 * back)
    add(pose, "Hook", -35 * back + 40 * thrust, 0, 0)
    add(pose, "Jaw", 18 * back - 22 * thrust, 0, 0)  # Gaping as it draws back.
    return pose


def fall(t):
    """It shrieks back, the hub drops, and the column topples over to its right, the arms flopping."""
    rear = math.sin(math.pi * min(t / 0.2, 1)) if t < 0.2 else 0.0
    s = cr.ease((t - 0.12) / 0.6)
    late = cr.ease((t - 0.3) / 0.55)
    pose = stance()
    pose["move"] = (-0.25 * s, 0.1 * rear + 0.15 * s, -CROUCH + 0.06 * rear - 0.45 * s)
    add(pose, "Body", -12 * rear + 8 * s, -25 * s, -10 * s)
    add(pose, "Col_0", -10 * rear - 10 * late, -25 * late, 0)
    add(pose, "Col_1", -6 * rear, -20 * late, 5 * late)
    add(pose, "Col_2", 0, -15 * late, 0)
    add(pose, "Col_3", 10 * rear, -12 * late, 10 * late)
    add(pose, "Arm_L_0", 20 * late, 0, -30 * late)
    add(pose, "Arm_L_1", 30 * late, 0, -20 * late)
    add(pose, "Arm_R_0", 35 * late, 0, 10 * late)
    add(pose, "Arm_R_1", 25 * late, 0, 15 * late)
    add(pose, "Hook", 30 * late, 0, 0)
    add(pose, "Jaw", 25 * rear - 10 * s, 0, 10 * late)
    add(pose, "Sack", -15 * s, 0, 0)
    for name, contact in CONTACT.items():
        sx = 1 if name.endswith("L") else -1
        pose["feet"][name] = cr.target_for(name, contact + Vector((0.25 * sx * s, -0.15 * s, 0)))
    return pose


CLIPS = os.environ.get("HB_CLIPS", "Crawl,Idle,Shoot,Dead").split(",")
if "Crawl" in CLIPS:
    cr.clip("Crawl", round(CYCLE * FPS), crawl)
if "Idle" in CLIPS:
    cr.clip("Idle", 3 * FPS, idle)
if "Shoot" in CLIPS:
    cr.clip("Shoot", round(SHOT_SECONDS * FPS), shoot)
if "Dead" in CLIPS:
    cr.clip("Dead", round(1.8 * FPS), fall, floor=True)
slide = cr.walk_slide("Crawl", list(CONTACT), SPEED) if "Crawl" in CLIPS else None

SIDE, QUARTER = (1, 0.05, 0.1), (0.8, -1, 0.3)
CENTRE = (MID, -0.1, 1.3)
for clip_name, count in (("Crawl", 8), ("Shoot", 12), ("Dead", 8), ("Idle", 4)):
    if clip_name not in CLIPS or clip_name not in os.environ.get("HB_PICTURES", "Crawl,Shoot,Dead,Idle"):
        continue
    shares = [i / (count - 1) for i in range(count)] if clip_name == "Dead" else [i / count for i in range(count)]
    for view_name, view in (("side", SIDE), ("quarter", QUARTER)):
        cr.strip(cr.film(clip_name, f"{clip_name}_{view_name}", view, CENTRE, 7.5, shares=shares, size=(400, 400)), f"strip_{clip_name}_{view_name}", columns=4)
if ONLY:
    sys.exit(0)
cr.finish({"moving": "Crawl", "shooting": "Shoot", "dead": "Dead", "still": "Idle"},
          {"speed": SPEED, "crawl_slide": slide, "shot_moment": SHOT_MOMENT, "muzzle_bone": "Muzzle"})
