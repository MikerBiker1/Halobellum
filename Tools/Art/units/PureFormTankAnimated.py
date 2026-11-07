# The Pure Form Tank, animated (D93): its static model (PureFormTank.py) rigged here, as Meshy's rig was unusable (morning item 19), so the
# crowd baker can play it. A hulking, lopsided brute: it walks on its two stumpy legs and its huge club arm, one limb at a time, each set
# down firmly and carrying its weight before the next lifts, the body rolling heavily over it; the small arm swings loose. To strike it
# hauls the club back and up, twisting away, then sweeps it round in front in a brutal arc, the torso following through, and staggers a
# step from the force before it rights itself. It dies toppling onto its club side. Sizes in metres at its height in the game (3.8 m),
# front toward -Y; its right (the club's side) is -X.
#   blender --background --factory-startup --python Tools/Art/units/PureFormTankAnimated.py
#   HB_ONLY=bones | weights | clips (stop after that), HB_CLIPS and HB_PICTURES (which clips to make and film), to lay it out.
import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import creature_rig as cr  # noqa: E402

ONLY = os.environ.get("HB_ONLY", "")
FPS = 30
SPEED = 1.6  # Its move speed in the game, m/s.
SLAM_SECONDS = 2.0  # Its Slam's interval: the striking clip is one blow long.
MID = 0.36  # Its middle line, between its legs.

mesh = cr.load("PureFormTankAnimated", "PureFormTank", "sketchfab.com/enemieshalo_3the_floodpure_tank.glb", height=3.8, fps=FPS)
cr.double_sided()  # Its fronds.

spec = [
    ("Body", (MID, 0.9, 1.6), (MID, 0.6, 2.4), None, 0.6),
    ("Chest", (MID, 0.6, 2.4), (0.4, 0.25, 3.25), "Body", 0.7),
    ("Head", (0.45, 0.05, 2.95), (0.5, -0.95, 2.45), "Chest", 0.45),
    ("ClubArm_0", (-0.75, 0.75, 2.6), (-1.25, 0.6, 1.6), "Chest", 0.3),
    ("ClubArm_1", (-1.25, 0.6, 1.6), (-1.05, 0.15, 0.85), "ClubArm_0", 0.3),
    ("Club", (-1.05, 0.15, 0.85), (-0.3, -0.2, 0.3), "ClubArm_1", 0.3),
    ("SmallArm_0", (1.1, 0.7, 2.6), (1.4, 0.8, 1.9), "Chest", 0.18),
    ("SmallArm_1", (1.4, 0.8, 1.9), (1.25, 0.35, 1.15), "SmallArm_0", 0.16),
    ("SmallHand", (1.25, 0.35, 1.15), (1.15, 0.2, 0.95), "SmallArm_1", 0.14),
    ("LegR_0", (-0.1, 0.9, 1.65), (-0.3, 0.95, 0.9), "Body", 0.3),
    ("LegR_1", (-0.3, 0.95, 0.9), (-0.28, 0.9, 0.3), "LegR_0", 0.28),
    ("FootR", (-0.28, 0.9, 0.3), (-0.3, 0.55, 0.02), "LegR_1", 0.3),
    ("LegL_0", (0.75, 0.85, 1.65), (0.95, 0.8, 0.9), "Body", 0.28),
    ("LegL_1", (0.95, 0.8, 0.9), (0.98, 0.78, 0.3), "LegL_0", 0.26),
    ("FootL", (0.98, 0.78, 0.3), (1.0, 0.5, 0.02), "LegL_1", 0.26),
]
arm = cr.skeleton(spec)
if ONLY in ("bones", ""):
    cr.bone_picture("bones")
if ONLY == "bones":
    sys.exit(0)

print("REPORT vertices per bone:", cr.weigh(blend=0.3, smooth=3, whole_below=30))
for name, direction in (("weights_front", (0.3, -1, 0.2)), ("weights_side", (1, 0, 0.1))):
    cr.weight_picture(name, direction, (MID, 0, 1.8), 10)
if ONLY == "weights":
    sys.exit(0)

# ---- Clips ----
cr.leg("legR", "LegR_0", "LegR_1", "FootR", pole=(0, -1, 0))
cr.leg("legL", "LegL_0", "LegL_1", "FootL", pole=(0, -1, 0))
cr.leg("club", "ClubArm_0", "ClubArm_1", "Club", pole=(-0.6, 1, 0))  # It walks on its club, knuckles down, the elbow out and back.

CROUCH = 0.32  # Its legs are straight columns in its model: it carries its bulk lower, knees bent.
CONTACT = {"legR": Vector((-0.28, 0.55, 0.02)), "legL": Vector((1.0, 0.5, 0.02)), "club": Vector((-0.42, 0.08, 0.06))}
CYCLE = 1.2  # Seconds per stride.
DUTY = 0.62  # Each limb carries weight three fifths of the stride: at most one is off the ground.
PHASE = {"legR": 0.0, "club": 0.33, "legL": 0.66}  # The right leg, then the club in front of it, then the left leg.
LIFT = {"legR": 0.28, "legL": 0.28, "club": 0.4}


def add(pose, bone, x=0.0, y=0.0, z=0.0):
    old = pose["turn"].get(bone, (0, 0, 0))
    pose["turn"][bone] = (old[0] + x, old[1] + y, old[2] + z)


def stance(club=True):
    """Low and lopsided, leaning onto its club side; the small arm hangs."""
    pose = {"move": (-0.06, 0, -CROUCH), "turn": {}, "feet": {}, "foot_turn": {}}
    add(pose, "Body", 4, -3, 0)
    add(pose, "Chest", 5, -2, 0)
    add(pose, "SmallArm_0", -6, 0, 6)
    for name, contact in CONTACT.items():
        if name == "club" and not club:
            continue
        pose["feet"][name] = cr.target_for(name, contact)
        pose["foot_turn"][name] = (0, 0, 0)
    return pose


def landing(t, name, width=0.06):
    d = (t - (PHASE[name] + 1.0 - DUTY * 0 - (1 - DUTY)) + 0.5) % 1.0 - 0.5  # The moment it comes down: the end of its swing.
    return math.exp(-(d / width) ** 2)


def walk(t):
    pose = stance()
    w = cr.wave
    # Each limb comes down hard: the body drops onto it and rolls toward it; the bulk sways over the limbs that carry it.
    thud = {n: landing(t, n) for n in PHASE}
    roll = -6 * thud["legR"] - 4 * thud["club"] + 7 * thud["legL"]
    pose["move"] = (-0.06 + 0.1 * w(t + 0.1), 0.0, -CROUCH - 0.08 * sum(thud.values()) + 0.03 * w(t * 3))
    add(pose, "Body", 2 * sum(thud.values()), roll + 3 * w(t), 5 * w(t))
    add(pose, "Chest", 3 * thud["club"], -0.5 * roll, -6 * w(t - 0.08))
    add(pose, "Head", -4 * sum(thud.values()) + 3 * w(t * 3 - 0.2), 0, 4 * w(t - 0.2))
    add(pose, "SmallArm_0", 14 * w(t - 0.1), 0, 4 * w(t))
    add(pose, "SmallArm_1", -10 - 8 * w(t - 0.2))
    for name, contact in CONTACT.items():
        stride = SPEED * CYCLE * DUTY
        offset, planted = cr.step(t, PHASE[name], DUTY, stride, LIFT[name])
        u = ((t - PHASE[name]) % 1.0 - DUTY) / (1 - DUTY)
        turn = (0, 0, 0) if planted else ((-15 if name != "club" else 25) * math.sin(math.pi * u), 0, 0)
        pose["feet"][name] = cr.target_for(name, contact + offset, turn)
        pose["foot_turn"][name] = turn
    return pose


def idle(t):
    pose = stance()
    w = cr.wave
    pose["move"] = (-0.06, 0, -CROUCH - 0.03 * w(t))
    add(pose, "Chest", 2 * w(t), 0, 0)
    add(pose, "Head", 2 * w(t + 0.2), 0, 6 * w(t * 0.5))
    add(pose, "SmallArm_0", 4 * w(t + 0.3))
    return pose


SLAM_MOMENT = 0.5  # The share of the striking clip at which the club lands.


def slam(t):
    """The club hauled back and up, the torso twisting away (to 0.42); a sweep round in front (the hit at SLAM_MOMENT); a stagger; it rights itself."""
    if t < 0.42:
        back, sweep = cr.ease(t / 0.42), 0.0
    elif t < SLAM_MOMENT:
        s = (t - 0.42) / (SLAM_MOMENT - 0.42)
        back, sweep = 1 - s, s ** 0.7
    else:
        back, sweep = 0.0, 1.0 - cr.ease((t - SLAM_MOMENT) / 0.45)
    stagger = math.sin(math.pi * min(max((t - SLAM_MOMENT) / 0.3, 0), 1)) if t > SLAM_MOMENT else 0.0
    pose = stance(club=False)
    pose["move"] = (-0.06 - 0.15 * back + 0.3 * stagger, 0.2 * back - 0.3 * sweep, -CROUCH + 0.08 * back - 0.12 * sweep - 0.06 * stagger)
    add(pose, "Body", -6 * back + 8 * sweep, -8 * back + 6 * sweep + 6 * stagger, -16 * back + 22 * sweep)
    add(pose, "Chest", -10 * back + 12 * sweep, -4 * back, -18 * back + 26 * sweep)
    add(pose, "Head", -10 * back + 8 * sweep, 0, 10 * back - 8 * sweep)
    # The club: up and back behind the shoulder, then round low in front from the right to the left.
    add(pose, "ClubArm_0", 70 * back - 55 * sweep, 0, -25 * back + 55 * sweep)
    add(pose, "ClubArm_1", -40 * back + 15 * sweep, 0, 0)
    add(pose, "Club", -20 * back - 10 * sweep, 0, 20 * sweep)
    add(pose, "SmallArm_0", 20 * back - 30 * sweep, 0, 15 * back)
    # Its left foot is driven out a step by the force and comes back.
    pose["feet"]["legL"] = cr.target_for("legL", CONTACT["legL"] + Vector((0.35 * stagger, -0.1 * stagger, 0.18 * stagger * (1 - stagger))))
    return pose


def fall(t):
    """Its legs give and it topples onto its club side, rolling over the club, head dropping."""
    s = cr.ease(t / 0.75)
    pose = stance(club=False)
    pose["move"] = (-0.6 * s, 0.1 * s, -CROUCH - 1.1 * s)
    add(pose, "Body", 15 * s, -65 * s, -10 * s)
    add(pose, "Chest", 10 * s, -10 * s, 0)
    add(pose, "Head", 25 * s, 0, 15 * s)
    add(pose, "ClubArm_0", -40 * s, 0, 40 * s)
    add(pose, "SmallArm_0", -30 * s, 0, 40 * s)
    pose["feet"]["legR"] = cr.target_for("legR", CONTACT["legR"] + Vector((-0.2 * s, 0, 0)))
    pose["feet"]["legL"] = cr.target_for("legL", CONTACT["legL"] + Vector((-0.6 * s, 0.2 * s, 0.4 * s)))
    return pose


CLIPS = os.environ.get("HB_CLIPS", "Walk,Idle,Slam,Dead").split(",")
if "Walk" in CLIPS:
    cr.clip("Walk", round(CYCLE * FPS), walk)
if "Idle" in CLIPS:
    cr.clip("Idle", 3 * FPS, idle)
if "Slam" in CLIPS:
    cr.clip("Slam", round(SLAM_SECONDS * FPS), slam)
if "Dead" in CLIPS:
    cr.clip("Dead", round(2.0 * FPS), fall, floor=True)
slide = cr.walk_slide("Walk", list(CONTACT), SPEED, floor=0.06) if "Walk" in CLIPS else None

SIDE, QUARTER, FRONT = (1, 0.05, 0.12), (0.8, -1, 0.35), (0.15, -1, 0.25)
CENTRE = (MID, -0.2, 1.7)
for clip_name, count in (("Walk", 8), ("Slam", 12), ("Dead", 8), ("Idle", 4)):
    if clip_name not in CLIPS or clip_name not in os.environ.get("HB_PICTURES", "Walk,Slam,Dead").split(","):
        continue
    shares = [i / (count - 1) for i in range(count)] if clip_name == "Dead" else [i / count for i in range(count)]
    for view_name, view in (("side", SIDE), ("front", FRONT)):
        cr.strip(cr.film(clip_name, f"{clip_name}_{view_name}", view, CENTRE, 11, shares=shares, size=(360, 360)), f"strip_{clip_name}_{view_name}", columns=4)
if ONLY:
    sys.exit(0)
cr.finish({"moving": "Walk", "shooting": "Slam", "dead": "Dead", "still": "Idle"}, {"speed": SPEED, "walk_slide": slide, "shot_moment": SLAM_MOMENT})
