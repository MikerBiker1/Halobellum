# The Stalker Form, animated (D90): its static model (StalkerForm.py) rigged here, as Meshy's rig stretched it, so the crowd baker can play it.
# A fast predatory quadruped: it runs low on its four limbs in diagonal pairs, its spine, neck and tail sack rolling through each stride;
# it attacks in sudden lunges, three blows to a round (one on each of its weapon's strikes), and springs back into its crouch; it dies
# folding onto its side. Sizes in metres at its height in the game (2.4 m, RealModelBuilder's), front toward -Y. Its left is +X.
#   blender --background --factory-startup --python Tools/Art/units/StalkerFormAnimated.py
#   HB_ONLY=bones (skeleton pictures only) or HB_ONLY=weights (and weight pictures), to lay it out.
import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import creature_rig as cr  # noqa: E402

ONLY = os.environ.get("HB_ONLY", "")
FPS = 60  # It runs at 5 m/s: at 30 frames a second its feet would jump 17 cm a frame.
SPEED = 5.0  # Its move speed in the game, m/s: a planted foot slides back this fast.
STRIKE_SECONDS = 0.4  # Its Talons' fire interval: the lunge clip holds three blows, one on each strike.

mesh = cr.load("StalkerFormAnimated", "StalkerForm", "sketchfab.com/enemieshalo_3the_floodpure_stalker.glb", height=2.4, lift=(1.4, 1.4, 1.3), fps=FPS)
cr.double_sided()  # The fronds under its tusks.

spec = [
    ("Body", (0, 0.32, 1.95), (0, -0.05, 2.02), None, 0.4),
    ("Chest", (0, -0.05, 2.02), (0, -0.45, 1.9), "Body", 0.4),
    ("Neck", (0, -0.45, 1.9), (0, -0.72, 1.82), "Chest", 0.25),
    ("Head", (0, -0.72, 1.82), (0, -1.9, 1.48), "Neck", 0.25),
    ("Tail_0", (0, 0.32, 1.95), (0, 0.9, 2.08), "Body", 0.2),
    ("Tail_1", (0, 0.9, 2.08), (0, 1.4, 2.12), "Tail_0", 0.32),
    ("Tail_2", (0, 1.4, 2.12), (0, 1.92, 2.15), "Tail_1", 0.3),
]
for side, sx in (("L", 1), ("R", -1)):
    spec += [
        (f"Arm_{side}_0", (0.40 * sx, -0.28, 1.6), (0.43 * sx, -0.68, 1.2), "Chest", 0.13),
        (f"Arm_{side}_1", (0.43 * sx, -0.68, 1.2), (0.33 * sx, -0.5, 0.95), f"Arm_{side}_0", 0.12),
        (f"Hand_{side}", (0.33 * sx, -0.5, 0.95), (0.22 * sx, 0.42, 0.86), f"Arm_{side}_1", 0.12),
        (f"Leg_{side}_0", (0.5 * sx, 0.2, 1.62), (0.5 * sx, 0.48, 1.25), "Body", 0.14),
        (f"Leg_{side}_1", (0.5 * sx, 0.48, 1.25), (0.68 * sx, -0.1, 0.62), f"Leg_{side}_0", 0.13),
        (f"Foot_{side}", (0.68 * sx, -0.1, 0.62), (1.0 * sx, -0.15, 0.0), f"Leg_{side}_1", 0.2),
    ]
arm = cr.skeleton(spec)
cr.bone_picture("bones")
if ONLY == "bones":
    sys.exit(0)

counts = cr.weigh(blend=0.3, smooth=3)
print("REPORT vertices per bone:", counts)
for name, direction in (("weights_quarter", (0.8, -1, 0.5)), ("weights_back", (-0.7, 1, 0.4)), ("weights_side", (1, 0, 0.1)), ("weights_under", (0.3, -0.4, -1))):
    cr.weight_picture(name, direction, (0, 0, 1.2), 7.5)
if ONLY == "weights":
    sys.exit(0)

# ---- Clips ----
for side in ("L", "R"):
    cr.leg(f"arm_{side}", f"Arm_{side}_0", f"Arm_{side}_1", f"Hand_{side}", pole=(0, -1, 0.3))  # Its elbow juts forward, folded like a mantis.
    cr.leg(f"leg_{side}", f"Leg_{side}_0", f"Leg_{side}_1", f"Foot_{side}", pole=(0, 1, 0))  # Its knee bends backward, as a bird's does.

CROUCH = 0.42  # How much lower than its model it carries its body.
HAND_DOWN = (-65, 0, 0)  # The long clawed hand turned to point down at the ground, a little back, as it runs on it.
CYCLE = 0.4  # Seconds per stride.
DUTY = 0.3  # The share of a stride each foot is on the ground: a bounding run, with moments in the air.
STRIDE = SPEED * CYCLE * DUTY  # How far a planted foot slides back under the body: as far as the body moves on, so it stands still.
CONTACT = {"arm_L": Vector((0.3, 0.02, 0)), "arm_R": Vector((-0.3, 0.02, 0)), "leg_L": Vector((0.95, 0.12, 0)), "leg_R": Vector((-0.95, 0.12, 0))}
PHASE = {"arm_L": 0.0, "leg_R": 0.04, "arm_R": 0.5, "leg_L": 0.54}  # Diagonal pairs, the hind foot a moment after the front.


def stance(extra=None):
    """The crouch every clip starts from: low, nose down a little, head forward and level, sack lowered."""
    pose = {"move": (0, 0, -CROUCH), "turn": {"Body": (5, 0, 0), "Neck": (-9, 0, 0), "Head": (-4, 0, 0), "Tail_0": (-10, 0, 0), "Tail_1": (4, 0, 0)},
            "feet": {}, "foot_turn": {}}
    for name, contact in CONTACT.items():
        turn = HAND_DOWN if name.startswith("arm") else (0, 0, 0)
        pose["feet"][name] = cr.target_for(name, contact, turn)
        pose["foot_turn"][name] = turn
    for key, value in (extra or {}).items():
        pose[key] = value
    return pose


def add(pose, bone, x=0.0, y=0.0, z=0.0):
    old = pose["turn"].get(bone, (0, 0, 0))
    pose["turn"][bone] = (old[0] + x, old[1] + y, old[2] + z)


def twice(t, shift):
    return math.cos(4 * math.pi * (t - shift))


def run(t):
    pose = stance()
    w = cr.wave
    # Twice a stride the body sinks onto a diagonal pair, and its spine rolls from side to side once, head steady, sack trailing.
    pose["move"] = (0.03 * w(t + 0.1), 0, -CROUCH - 0.05 * twice(t, 0.17))
    add(pose, "Body", 2 * twice(t, 0.1), 4 * w(t), 6 * w(t))
    add(pose, "Chest", 0, -2 * w(t - 0.05), -5 * w(t - 0.1))
    add(pose, "Neck", -1.5 * twice(t, 0.2), 0, -4 * w(t - 0.2))
    add(pose, "Head", 0, -2 * w(t - 0.1), -3 * w(t - 0.3))
    add(pose, "Tail_0", 3 * twice(t, 0.25), 0, -9 * w(t - 0.15))
    add(pose, "Tail_1", 5 * twice(t, 0.32), 0, -12 * w(t - 0.25))
    add(pose, "Tail_2", 6 * twice(t, 0.4), 0, -15 * w(t - 0.35))
    for name, contact in CONTACT.items():
        lift = 0.32 if name.startswith("arm") else 0.26
        offset, planted = cr.step(t, PHASE[name], DUTY, STRIDE, lift)
        u = ((t - PHASE[name]) % 1.0 - DUTY) / (1 - DUTY)
        if name.startswith("arm"):
            turn = HAND_DOWN if planted else (HAND_DOWN[0] + 50 * math.sin(math.pi * u), 0, 0)  # The hand curls back as it leaves the ground.
        else:
            turn = (0, 0, 0) if planted else (-22 * math.sin(math.pi * u), 0, 0)  # The claws trail as the foot swings.
        pose["feet"][name] = cr.target_for(name, contact + offset, turn)
        pose["foot_turn"][name] = turn
    return pose


def stalk(t):
    """Standing: it breathes, and its head turns, searching."""
    pose = stance()
    w = cr.wave
    pose["move"] = (0, 0, -CROUCH - 0.02 * w(t))
    add(pose, "Chest", 1.5 * w(t), 0, 0)
    add(pose, "Neck", -2 * w(t + 0.2), 0, 10 * w(t * 2 - 0.1) * (0.5 + 0.5 * w(t)))
    add(pose, "Tail_1", 3 * w(t - 0.2), 0, 4 * w(t - 0.3))
    add(pose, "Tail_2", 3 * w(t - 0.3), 0, 6 * w(t - 0.4))
    return pose


def blow_curve(u):
    """One blow, u from 0 to 1: coils (to -0.3), then lunges in a few frames (to 1 at the strike, u = 0.5), holds, and springs back."""
    if u < 0.32:
        return -0.3 * cr.ease(u / 0.32)
    if u < 0.5:
        s = (u - 0.32) / 0.18
        return -0.3 + 1.3 * (1 - (1 - s) ** 3)
    if u < 0.58:
        return 1.0
    return 1.0 - cr.ease((u - 0.58) / 0.42)


STRIKES = ((("arm_L", "arm_R"), 1.0), (("arm_L",), 0.75), (("arm_R",), 0.85))  # Which arms strike in each of its three blows, and how far it lunges.


def lunge(t):
    """Three blows, one on each strike of its Talons: both arms and its tusks, then the left arm, then the right."""
    third = min(int(t * 3), 2)
    u = t * 3 - third
    k = blow_curve(u)
    arms, reach = STRIKES[third]
    coiled, struck = max(-k, 0) / 0.3, max(k, 0)
    forward = 0.6 * reach * struck
    swing = 1 if third == 2 else -1 if third == 1 else 0  # The single blows twist toward the striking arm.
    pose = stance()
    pose["move"] = (0.06 * swing * struck, -forward, -CROUCH - 0.1 * coiled + 0.06 * struck)
    add(pose, "Body", -6 * coiled + 9 * struck, 0, -6 * swing * struck)
    add(pose, "Chest", -4 * coiled + 5 * struck, 0, -4 * swing * struck)
    add(pose, "Neck", -14 * coiled + 12 * struck, 0, 0)
    add(pose, "Head", -8 * coiled + (14 if third == 0 else 6) * struck, 0, 0)
    add(pose, "Tail_0", 8 * coiled - 6 * struck, 0, 0)
    add(pose, "Tail_1", 6 * coiled - 10 * struck, 0, 0)
    add(pose, "Tail_2", 4 * coiled - 14 * struck, 0, 0)
    for name in arms:
        sx = 1 if name.endswith("L") else -1
        # Up and back as it coils, then a chop forward and down past where the target stands.
        wrist = Vector((0.36 * sx, -0.35 - 0.1 * coiled - 0.5 * reach * struck - forward, 1.05 + 0.35 * coiled + 0.05 * struck - CROUCH))
        pose["feet"][name] = wrist
        pose["foot_turn"][name] = (40 * coiled - 120 * struck, 0, -12 * sx * struck)
    return pose


def fall(t):
    """Struck down: it rears, then its legs fold under it where its feet stand, and it slumps half onto its left side, head and sack dropping."""
    rear = math.sin(math.pi * min(t / 0.25, 1)) if t < 0.25 else 0.0
    s = cr.ease((t - 0.15) / 0.6)
    curl = cr.ease((t - 0.4) / 0.5)
    pose = stance()
    pose["move"] = (0.15 * s, 0.1 * rear, -CROUCH + 0.12 * rear - 0.95 * s)
    add(pose, "Body", -14 * rear + 10 * s, 50 * s, 12 * s)
    add(pose, "Chest", 4 * s, 6 * s, 6 * s)
    add(pose, "Neck", -22 * rear + 16 * s, 0, 18 * s)
    add(pose, "Head", -10 * rear - 6 * curl, 10 * curl, 14 * curl)
    add(pose, "Tail_0", -12 * s, 0, -14 * s)
    add(pose, "Tail_1", -18 * s, 0, -16 * s)
    add(pose, "Tail_2", -10 * s, 0, -12 * s)
    for name, contact in CONTACT.items():
        sx = 1 if name.endswith("L") else -1
        if name.startswith("arm"):
            # The arms give way and slide out in front, the hands opening flat.
            turn = (HAND_DOWN[0] + 45 * s, 0, 0)
            pose["feet"][name] = cr.target_for(name, contact + Vector((0.3 * sx * s, -0.4 * s, 0)), turn)
        else:
            turn = (0, 0, 0)
            pose["feet"][name] = cr.target_for(name, contact + Vector((0.2 * sx * s, 0.12 * s, 0)), turn)
        pose["foot_turn"][name] = turn
    return pose


CLIPS = os.environ.get("HB_CLIPS", "Run,Stalk,Lunge,Dead").split(",")
if "Run" in CLIPS:
    cr.clip("Run", round(CYCLE * FPS), run)
if "Stalk" in CLIPS:
    cr.clip("Stalk", 2 * FPS, stalk)
if "Lunge" in CLIPS:
    cr.clip("Lunge", round(3 * STRIKE_SECONDS * FPS), lunge)
if "Dead" in CLIPS:
    cr.clip("Dead", round(1.4 * FPS), fall, floor=True)
slide = cr.walk_slide("Run", list(CONTACT), SPEED) if "Run" in CLIPS else None

SIDE, QUARTER = (1, 0.05, 0.12), (0.8, -1, 0.35)
CENTRE = (0, -0.2, 0.9)
for clip_name, count in (("Run", 8), ("Lunge", 12), ("Dead", 8), ("Stalk", 4)):
    if clip_name not in CLIPS or clip_name not in os.environ.get("HB_PICTURES", "Run,Lunge,Dead,Stalk"):
        continue
    shares = [i / (count - 1) for i in range(count)] if clip_name == "Dead" else [i / count for i in range(count)]
    for view_name, view in (("side", SIDE), ("quarter", QUARTER)):
        cr.strip(cr.film(clip_name, f"{clip_name}_{view_name}", view, CENTRE, 6.5, shares=shares, size=(400, 330)), f"strip_{clip_name}_{view_name}", columns=4)
if ONLY:
    sys.exit(0)
cr.finish({"moving": "Run", "shooting": "Lunge", "dead": "Dead", "still": "Stalk"},
          {"speed": SPEED, "run_slide": slide, "shot_moment": round(0.5 / 3, 4), "strikes_per_clip": 3})
