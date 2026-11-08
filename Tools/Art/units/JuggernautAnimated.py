# The Juggernaut, animated (D107): its static model (Juggernaut.py) rigged here so the crowd baker can play it, as the owner found it badly
# animated (it wobbled as one piece, its two scythe arms swinging stiffly from the shoulder).
#   blender --background --factory-startup --python Tools/Art/units/JuggernautAnimated.py
#   HB_ONLY=measure | bones | weights | clips (stop after that), HB_CLIPS and HB_PICTURES (which clips to make and film), to lay it out.
import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import creature_rig as cr  # noqa: E402
import model_tools as mt  # noqa: E402

ONLY = os.environ.get("HB_ONLY", "")
FPS = 30
LENGTH = 8.0  # Its longest side across the ground in the game, arm to arm, as RealModelBuilder sized its static model.

mesh = cr.load("JuggernautAnimated", "Juggernaut", "Meshy/Meshy_AI_Juggernaut_flood.glb", length=LENGTH, fps=FPS)

if ONLY == "measure":
    import numpy as np
    co = np.array([v.co[:] for v in mesh.data.vertices])
    print("REPORT bounds", co.min(0).round(2), co.max(0).round(2))
    # Height profile: for slices of z, the x and y extent, to find the legs, body and arms.
    for z0 in np.arange(0, co[:, 2].max() + 0.25, 0.25):
        s = co[(co[:, 2] >= z0) & (co[:, 2] < z0 + 0.25)]
        if len(s):
            print(f"REPORT z {z0:4.2f}: n {len(s):5d} x {s[:, 0].min():6.2f}..{s[:, 0].max():6.2f} y {s[:, 1].min():6.2f}..{s[:, 1].max():6.2f}")
    for p in cr.pieces()[:8]:
        pc = co[p]
        print("REPORT piece", len(p), pc.min(0).round(2), pc.max(0).round(2))
    cr.skeleton([("Body", (0, 0, 1.5), (0, 0, 2.5), None, 0.5)])
    cr.bone_picture("measure", step=0.5)
    sys.exit(0)

LOW = [(-0.45, 0.05, 2.95), (-1.3, 0.85, 2.5), (-2.4, 1.4, 2.2), (-3.4, 1.05, 1.8), (-3.85, 0.15, 1.35), (-3.55, -0.75, 1.05),
       (-2.75, -1.6, 0.95), (-1.95, -2.25, 1.15), (-1.17, -2.83, 1.6)]  # The low arm, on its left (-X): out and back, round, forward to its blade.
HIGH = [(0.8, 0.65, 3.3), (1.8, 1.6, 3.25), (2.7, 2.35, 3.8), (3.25, 2.75, 4.5), (3.8, 2.35, 4.65), (3.95, 1.5, 4.4), (3.7, 0.8, 4.1),
        (3.5, 0.25, 3.9)]  # The high arm, on its right (+X): back and up, over in a loop, its blade hanging forward.
spec = [
    ("Body", (0.15, 0.2, 2.35), (0.15, 0.3, 3.0), None, 0.55),
    ("Head", (0.15, 0.3, 3.0), (0.1, 0.35, 4.9), "Body", 1.1),
    ("LegL_0", (-0.2, -0.05, 2.45), (-0.5, -0.55, 1.75), "Body", 0.3),
    ("LegL_1", (-0.5, -0.55, 1.75), (-0.95, -0.6, 0.3), "LegL_0", 0.25),
    ("FootL", (-0.95, -0.6, 0.3), (-1.0, -0.95, 0.02), "LegL_1", 0.3),
    ("LegR_0", (0.5, 0.45, 2.55), (1.35, 1.0, 2.1), "Body", 0.3),
    ("LegR_1", (1.35, 1.0, 2.1), (1.6, 1.45, 0.3), "LegR_0", 0.25),
    ("FootR", (1.6, 1.45, 0.3), (1.65, 1.1, 0.02), "LegR_1", 0.3),
]
spec += cr.chain("Low", LOW, "Body", 0.3)
spec += cr.chain("High", HIGH, "Body", 0.3)
arm = cr.skeleton(spec)
if ONLY in ("bones", ""):
    cr.bone_picture("bones", step=0.5)
if ONLY == "bones":
    sys.exit(0)

print("REPORT vertices per bone:", cr.weigh(blend=0.3, smooth=3, whole_below=60))
for name, direction in (("weights_front", (0.15, -1, 0.2)), ("weights_side", (1, 0.1, 0.15)), ("weights_back", (-0.3, 1, 0.4))):
    cr.weight_picture(name, direction, (0, 0, 2.5), 14)
if ONLY == "weights":
    sys.exit(0)

# ---- Clips ----
cr.leg("legL", "LegL_0", "LegL_1", "FootL", pole=(-0.3, -1, 0))  # Its knee bends forward and out.
cr.leg("legR", "LegR_0", "LegR_1", "FootR", pole=(0.9, 0.5, 0))  # Its back leg's knee splays out and back, insect-like.

SPEED = 3.25  # Its move speed in the game, m/s.
STRIKE_SECONDS = 1.2  # Its Crushing Charge's interval: the striking clip is one blow long.
STRIKE_MOMENT = 0.42  # The share of the striking clip at which both blades meet.
CROUCH = 0.18  # Its legs stand nearly straight in its model: it carries itself a little lower, knees bent.
CONTACT = {"legL": Vector((-1.0, -0.7, 0.02)), "legR": Vector((1.6, 0.9, 0.02))}  # A little less staggered than it stands, to run.
CYCLE = 0.8  # Seconds per stride: a bounding lope.
DUTY = 0.45  # Each foot carries it a little under half the stride: a moment in the air between footfalls.
PHASE = {"legL": 0.0, "legR": 0.5}
LIFT = 0.5
LOW_BONES = [f"Low_{i}" for i in range(len(LOW) - 1)]
HIGH_BONES = [f"High_{i}" for i in range(len(HIGH) - 1)]


def add(pose, bone, x=0.0, y=0.0, z=0.0):
    old = pose["turn"].get(bone, (0, 0, 0))
    pose["turn"][bone] = (old[0] + x, old[1] + y, old[2] + z)


def stance(feet=True):
    pose = {"move": (0, 0, -CROUCH), "turn": {}, "feet": {}, "foot_turn": {}}
    if feet:
        for name, contact in CONTACT.items():
            pose["feet"][name] = cr.target_for(name, contact)
            pose["foot_turn"][name] = (0, 0, 0)
    return pose


def writhe(pose, bones, t, cycles, amount, lag=0.11, lift=0.0):
    """A wave running down an arm from its shoulder to its blade, each joint turning a little after the one before: it adds up to a whip."""
    for i, bone in enumerate(bones):
        a = 2 * math.pi * (cycles * t - lag * i)
        grow = 0.6 + 0.4 * i / max(1, len(bones) - 1)  # The far joints swing more, as a tentacle does.
        add(pose, bone, amount * grow * math.sin(a + 1.3) + lift, 0.5 * amount * grow * math.sin(a + 2.4), amount * grow * math.sin(a))


def run(t):
    pose = stance()
    w = cr.wave
    land = math.cos(4 * math.pi * (t - DUTY / 2))  # 1 at each foot's mid-stance, when it carries the most.
    side = math.cos(2 * math.pi * (t - DUTY / 2))  # 1 while the left foot carries it, -1 the right.
    pose["move"] = (-0.12 * side, 0.06 * land, -CROUCH - 0.14 * land + 0.04)
    add(pose, "Body", 9 + 3 * land, -5 * side, 7 * w(t))
    add(pose, "Head", -6 + 4 * math.cos(4 * math.pi * (t - DUTY / 2 - 0.08)), 3 * math.cos(2 * math.pi * (t - 0.3)), -5 * w(t - 0.1))
    writhe(pose, LOW_BONES, t, 1, 4)
    add(pose, "Low_0", 0, 6, 0)  # Carried a little higher as it runs, its blade clear of the ground.
    writhe(pose, HIGH_BONES, t, 1, 5, lift=-1.5)  # Swept back a little by its run.
    for name, contact in CONTACT.items():
        offset, planted = cr.step(t, PHASE[name], DUTY, SPEED * CYCLE * DUTY, LIFT)
        u = ((t - PHASE[name]) % 1.0 - DUTY) / (1 - DUTY)
        turn = (0, 0, 0) if planted else (-25 * math.sin(math.pi * u), 0, 0)  # Toes down as it lifts the foot, flat as it lands.
        pose["feet"][name] = cr.target_for(name, contact + offset, turn)
        pose["foot_turn"][name] = turn
    return pose


def idle(t):
    pose = stance()
    w = cr.wave
    pose["move"] = (0, 0, -CROUCH - 0.04 * w(t))
    add(pose, "Body", 2 * w(t), 0, 0)
    add(pose, "Head", 3 * w(t + 0.25), 2 * w(t + 0.1), 6 * w(t + 0.4))
    writhe(pose, LOW_BONES, t, 1, 2.5, lag=0.08)
    writhe(pose, HIGH_BONES, t, 1, 2.5, lag=0.08)
    add(pose, LOW_BONES[-1], 0, 0, 8 * max(0.0, w(t * 3)) ** 3)  # A blade twitches now and then.
    add(pose, HIGH_BONES[-1], 6 * max(0.0, w(t * 3 + 0.5)) ** 3)
    return pose


def strike(t):
    """Rearing up, both arms drawn back (to 0.32); the high blade chopping down from over its head as the low one sweeps in across its front,
    both meeting in front of it at STRIKE_MOMENT as its body lunges after them; it rights itself."""
    if t < 0.32:
        rear, hit = cr.ease(t / 0.32), 0.0
    elif t < STRIKE_MOMENT:
        s = (t - 0.32) / (STRIKE_MOMENT - 0.32)
        rear, hit = 1 - cr.ease(s), cr.ease(s)
    else:
        rear, hit = 0.0, 1 - cr.ease((t - 0.52) / 0.45)
    pose = stance()
    pose["move"] = (0.05 * hit, 0.3 * rear - 0.55 * hit, -CROUCH + 0.15 * rear - 0.22 * hit)
    add(pose, "Body", -12 * rear + 18 * hit, 0, 4 * rear - 6 * hit)
    add(pose, "Head", -10 * rear + 10 * hit, 0, 0)
    # The high arm: raised higher and back, then swung down on its side and round to the front; its far joints curl in after it.
    add(pose, "High_0", 0, -18 * rear + 45 * hit, 15 * rear - 55 * hit)
    for i, bone in enumerate(HIGH_BONES[1:], 1):
        add(pose, bone, 0, 3 * hit, -2 * rear - (3 + i) * hit)
    # The low arm: out and up, then down and in across the front.
    add(pose, "Low_0", 0, 14 * rear - 16 * hit, -18 * rear + 34 * hit)
    for i, bone in enumerate(LOW_BONES[1:], 1):
        add(pose, bone, 0, 0, -3 * rear + (4 + i) * hit)
    # The front foot takes a little of the lunge.
    pose["feet"]["legL"] = cr.target_for("legL", CONTACT["legL"] + Vector((0, -0.25 * hit, 0)))
    return pose


def pounce(t):
    """In the air on its leap: pitched forward, its legs drawn up under it, both arms swept back and writhing."""
    pose = stance(feet=False)
    add(pose, "Body", 22, 0, 0)
    add(pose, "Head", -8, 0, 0)
    add(pose, "High_0", -22, 0, 12)
    add(pose, "Low_0", 0, 18, -22)
    writhe(pose, LOW_BONES, t, 1, 5, lag=0.13)
    writhe(pose, HIGH_BONES, t, 1, 5, lag=0.13)
    tuck = (40, 0, 0)
    pose["feet"]["legL"] = cr.target_for("legL", CONTACT["legL"] + Vector((0.2, 0.9, 1.0)), tuck)
    pose["feet"]["legR"] = cr.target_for("legR", CONTACT["legR"] + Vector((-0.2, 0.3, 1.2)), tuck)
    pose["foot_turn"] = {"legL": tuck, "legR": tuck}
    return pose


def fall(t):
    """Its knees fold under it and the head mass pitches forward onto the ground with a heavy bounce, its arms falling limp around it."""
    s = cr.ease(t / 0.6)
    bounce = 0.15 * math.sin(math.pi * min(max((t - 0.5) / 0.22, 0), 1))
    limp = cr.ease((t - 0.35) / 0.5)
    pose = stance()
    pose["move"] = (0, -0.7 * s, -CROUCH - 1.55 * s + bounce)
    add(pose, "Body", 30 * s, -8 * s, 0)
    add(pose, "Head", 16 * s - 4 * bounce, 0, 0)
    add(pose, "High_0", 0, 42 * s, -15 * s)  # The high arm drops on its side to the ground.
    for i, bone in enumerate(HIGH_BONES[1:], 1):
        add(pose, bone, 0, 6 * limp, -4 * limp * (1 if i % 2 else -1))
    add(pose, "Low_0", 0, -6 * s, 6 * s)
    for i, bone in enumerate(LOW_BONES[1:], 1):
        add(pose, bone, 0, 0, 4 * limp * (1 if i % 2 else -1))
    pose["feet"]["legL"] = cr.target_for("legL", CONTACT["legL"] + Vector((-0.35 * s, -0.2 * s, 0)))
    pose["feet"]["legR"] = cr.target_for("legR", CONTACT["legR"] + Vector((0.3 * s, 0.25 * s, 0)))
    return pose


CLIPS = os.environ.get("HB_CLIPS", "Run,Idle,Strike,Pounce,Dead").split(",")
if "Run" in CLIPS:
    cr.clip("Run", round(CYCLE * FPS), run)
if "Idle" in CLIPS:
    cr.clip("Idle", 3 * FPS, idle)
if "Strike" in CLIPS:
    cr.clip("Strike", round(STRIKE_SECONDS * FPS), strike)
if "Pounce" in CLIPS:
    cr.clip("Pounce", round(0.6 * FPS), pounce)
if "Dead" in CLIPS:
    solid = sorted(set(cr.vertices_not_of("Low")) & set(cr.vertices_not_of("High")))  # Its limp arms may lie in the dirt; its body rests on it.
    cr.clip("Dead", round(2.0 * FPS), fall, floor=solid)
slide = cr.walk_slide("Run", list(CONTACT), SPEED, floor=0.06) if "Run" in CLIPS else None

SIDE, FRONT = (1, 0.05, 0.12), (0.2, -1, 0.25)
CENTRE = (0, 0, 2.3)
for clip_name, count in (("Run", 8), ("Strike", 12), ("Dead", 8), ("Idle", 4), ("Pounce", 4)):
    if clip_name not in CLIPS or clip_name not in os.environ.get("HB_PICTURES", "Run,Strike,Dead").split(","):
        continue
    shares = [i / (count - 1) for i in range(count)] if clip_name == "Dead" else [i / count for i in range(count)]
    for view_name, view in (("side", SIDE), ("front", FRONT)):
        cr.strip(cr.film(clip_name, f"{clip_name}_{view_name}", view, CENTRE, 16, shares=shares, size=(360, 360)), f"strip_{clip_name}_{view_name}", columns=4)
if ONLY:
    sys.exit(0)
cr.finish({"moving": "Run", "shooting": "Strike", "dead": "Dead", "still": "Idle", "charge": "Pounce"},
          {"speed": SPEED, "walk_slide": slide, "shot_moment": STRIKE_MOMENT})
