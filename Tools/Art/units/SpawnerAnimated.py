# The Spawner, animated (D90): its static model (Spawner.py, the owner's Meshy model reduced and repainted) rigged here so the crowd baker can
# play it. A big, lumbering Flood creature: it walks on four legs, one at a time, its body rolling heavily onto each foot as it lands and its
# head swinging late; every one of its 54 tendrils has its own chain of bones and writhes to its own rhythm. Its belly swells and then
# heaves, a wave of contraction running forward to the maw, which gapes, tendrils splaying, as it gives birth: the clips are timed in the
# game so that this moment comes as the battle spawns its forms (spawnMoment). Its Spore Lash is its tendrils whipping forward, three to a
# clip. It dies collapsing onto its belly, the tendrils going limp. Sizes in metres at its length in the game (9 m), front toward -Y.
#   blender --background --factory-startup --python Tools/Art/units/SpawnerAnimated.py
#   HB_ONLY=bones | weights | clips (stop after that), HB_CLIPS and HB_PICTURES (which clips to make and film), to lay it out.
import math
import os
import sys

import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import creature_rig as cr  # noqa: E402

ONLY = os.environ.get("HB_ONLY", "")
FPS = 30
SPEED = 0.9  # Its move speed in the game, m/s.
SPAWN_SECONDS = 4.0  # Its spawn interval: the walking and lashing clips are one spawn long, and the game keeps them in step with its spawns.
BIRTH = 0.64  # The share of those clips at which it gives birth (spawnMoment).
MAW = Vector((0, -0.5, 2.2))  # Where its tendrils grow from.

mesh = cr.load("SpawnerAnimated", "Spawner", "Meshy/Meshy_AI_Spawner.glb", length=9.0, fps=FPS)

spec = [
    ("Body", (0, 2.7, 2.5), (0, 1.2, 2.8), None, 1.0),
    ("Chest", (0, 1.2, 2.8), (0, 0.0, 2.6), "Body", 1.0),
    ("Maw", (0, 0.0, 2.6), (0, -1.0, 2.1), "Chest", 0.7),
    ("Belly_1", (0, 2.6, 1.95), (0, 1.5, 1.85), "Body", 0.75),
    ("Belly_0", (0, 1.5, 1.85), (0, 0.4, 1.95), "Chest", 0.75),
    ("Leg_FR_0", (1.4, 0.55, 2.4), (1.9, 0.0, 1.15), "Chest", 0.45),
    ("Leg_FR_1", (1.9, 0.0, 1.15), (2.02, -0.45, 0.55), "Leg_FR_0", 0.35),
    ("Foot_FR", (2.02, -0.45, 0.55), (2.13, -1.45, 0.05), "Leg_FR_1", 0.3),
    ("Leg_FL_0", (-1.45, 0.8, 2.3), (-1.9, 0.8, 1.2), "Chest", 0.45),
    ("Leg_FL_1", (-1.9, 0.8, 1.2), (-2.05, 0.95, 0.3), "Leg_FL_0", 0.35),
    ("Foot_FL", (-2.05, 0.95, 0.3), (-1.75, -0.8, 0.1), "Leg_FL_1", 0.3),
    ("Leg_RR_0", (0.85, 2.25, 2.2), (1.0, 2.3, 1.0), "Body", 0.45),
    ("Leg_RR_1", (1.0, 2.3, 1.0), (1.15, 1.95, 0.3), "Leg_RR_0", 0.35),
    ("Foot_RR", (1.15, 1.95, 0.3), (1.25, 1.5, 0.05), "Leg_RR_1", 0.3),
    ("Leg_RL_0", (-0.75, 2.9, 2.1), (-0.9, 3.55, 0.9), "Body", 0.45),
    ("Leg_RL_1", (-0.9, 3.55, 0.9), (-0.86, 4.1, 0.35), "Leg_RL_0", 0.35),
    ("Foot_RL", (-0.86, 4.1, 0.35), (-0.84, 4.35, 0.02), "Leg_RL_1", 0.3),
    ("Muzzle", (0, -1.1, 2.1), (0, -1.4, 2.0), "Maw"),
]

# Every tendril gets a chain along it, from its root at the maw: three bones for a long one, two for a short one, none for a wisp (it
# follows the maw). Each tendril follows only its own chain.
TENDRILS = []
piece_bones = {}
for n, members in enumerate(cr.pieces()[1:]):
    path, length = cr.piece_axis(members, MAW)
    if length < 0.35 or len(members) < 12:
        piece_bones[min(members)] = ["Maw"]
        continue
    bones = 3 if length > 1.6 else 2
    points = [path[0], path[2], path[4], path[5]] if bones == 3 else [path[0], path[3], path[5]]
    names = [f"Tendril_{n}_{i}" for i in range(bones)]
    for i in range(bones):
        spec.append((names[i], tuple(points[i]), tuple(points[i + 1]), "Maw" if i == 0 else names[i - 1], max(0.08, length / 10)))
    piece_bones[min(members)] = ["Maw"] + names
    TENDRILS.append({"names": names, "length": length, "root": path[0], "tip": path[-1]})
print(f"REPORT {len(TENDRILS)} tendrils with chains, {len(piece_bones) - len(TENDRILS)} wisps on the maw")

arm = cr.skeleton(spec)
if ONLY in ("bones", ""):
    cr.bone_picture("bones", step=0.5)
if ONLY == "bones":
    sys.exit(0)

print("REPORT vertices per bone:", {k: v for k, v in cr.weigh(blend=0.3, smooth=3, whole_below=0, piece_bones=piece_bones).items() if not k.startswith("Tendril")})
for name, direction in (("weights_quarter", (0.8, -1, 0.5)), ("weights_side", (1, 0, 0.1)), ("weights_under", (0.4, -0.3, -1))):
    cr.weight_picture(name, direction, (0, -0.3, 1.8), 13)
if ONLY == "weights":
    sys.exit(0)

# ---- Clips ----
cr.leg("FR", "Leg_FR_0", "Leg_FR_1", "Foot_FR", pole=(0, -1, 0))
cr.leg("FL", "Leg_FL_0", "Leg_FL_1", "Foot_FL", pole=(0, -1, 0))
cr.leg("RR", "Leg_RR_0", "Leg_RR_1", "Foot_RR", pole=(0, 1, 0))
cr.leg("RL", "Leg_RL_0", "Leg_RL_1", "Foot_RL", pole=(0, 1, 0))

CROUCH = 0.35  # Its model stands on straight legs, mid-stride: it carries itself lower, legs bent under its weight.
# Where each foot stands in the middle of its step: under its shoulder or hip, a little out (the model has them mid-stride, all over).
CONTACT = {"FR": Vector((2.06, -0.85, 0.05)), "FL": Vector((-1.85, -0.9, 0.1)), "RR": Vector((1.25, 1.7, 0.05)), "RL": Vector((-0.95, 3.35, 0.02))}
CYCLE = 2.0  # Seconds per stride; the walking clip, one spawn long, holds two.
DUTY = 0.75  # One foot off the ground at a time.
PHASE = {"RL": 0.0, "FL": 0.25, "RR": 0.5, "FR": 0.75}  # A walk: hind foot, then the front foot of that side, then the other side.
rng = np.random.default_rng(54)
for tendril in TENDRILS:
    # Each its own rhythm: whole cycles per clip, so the clips loop; a phase, a size that grows with its length, and how far it curls.
    tendril["cycles"] = int(rng.integers(1, 4))
    tendril["second"] = int(rng.integers(3, 7))
    tendril["phase"] = float(rng.uniform(0, 1))
    tendril["phase2"] = float(rng.uniform(0, 1))
    tendril["size"] = float(rng.uniform(7, 15)) * min(1.6, 0.6 + tendril["length"] / 2.5)
    tendril["out"] = Vector((tendril["tip"].x - tendril["root"].x, 0, tendril["tip"].z - tendril["root"].z)).normalized() \
        if abs(tendril["tip"].x - tendril["root"].x) + abs(tendril["tip"].z - tendril["root"].z) > 1e-3 else Vector((0, 0, 1))


def add(pose, bone, x=0.0, y=0.0, z=0.0):
    old = pose["turn"].get(bone, (0, 0, 0))
    pose["turn"][bone] = (old[0] + x, old[1] + y, old[2] + z)


def stance():
    pose = {"move": (0, 0, -CROUCH), "turn": {}, "scale": {}, "feet": {}, "foot_turn": {}}
    for name, contact in CONTACT.items():
        pose["feet"][name] = cr.target_for(name, contact)
        pose["foot_turn"][name] = (0, 0, 0)
    return pose


def bump(t, centre, width):
    d = (t - centre + 0.5) % 1.0 - 0.5
    return math.exp(-(d / width) ** 2)


def writhe(pose, t, calm=1.0, splay=0.0, whip=0.0, droop=0.0):
    """Every tendril on its own rhythm, a wave running down its chain; splay opens them outward from the maw, whip throws them forward and
    down (back and up when negative), droop lets them hang."""
    for tendril in TENDRILS:
        for i, name in enumerate(tendril["names"]):
            lag = 0.11 * i
            k = tendril["size"] * calm * (0.6 + 0.45 * i)
            a = k * cr.wave(tendril["cycles"] * t + tendril["phase"] - lag) + 0.35 * k * cr.wave(tendril["second"] * t + tendril["phase2"] - lag)
            b = k * cr.wave(tendril["cycles"] * t + tendril["phase"] + 0.25 - lag)
            out = tendril["out"]
            # Splaying turns the root away from the maw's middle: up for those above (a turn about x lowers a tendril that points
            # forward), sideways for those to the sides.
            sx = -out.z * 30 * splay if i == 0 else 0.0
            sz = out.x * 30 * splay if i == 0 else 0.0
            pitch = (22 * whip + 14 * droop) * (1.0 if i == 0 else 0.6)
            add(pose, name, a + sx + pitch, 0, b + sz)


def belly(pose, t):
    """The belly swells, then a wave of contraction runs forward to the maw, which gapes as it gives birth (at BIRTH), then aftershocks."""
    swell = 0.13 * cr.ease(t / 0.45) * (1 - cr.ease((t - 0.5) / 0.15))
    rear = 0.36 * bump(t, BIRTH - 0.12, 0.035)
    front = 0.38 * bump(t, BIRTH - 0.06, 0.035)
    after = 0.07 * math.exp(-max(t - BIRTH, 0) * 9) * math.sin(max(t - BIRTH, 0) * 70) if t > BIRTH else 0.0
    pose["scale"]["Belly_1"] = (1 + swell - rear + after, 1, 1 + swell - rear + after)
    pose["scale"]["Belly_0"] = (1 + swell - front - after, 1, 1 + swell - front - after)
    gape = bump(t, BIRTH, 0.03)
    heave = bump(t, BIRTH - 0.08, 0.06)
    x, y, z = pose["move"]
    pose["move"] = (x, y, z - 0.12 * heave)
    add(pose, "Body", 3 * heave, 0, 0)
    add(pose, "Chest", 4 * heave, 0, 0)
    add(pose, "Maw", -16 * gape + 8 * heave, 0, 0)
    return gape


def walk(t):
    pose = stance()
    cycle = (t * 2) % 1.0  # Two strides to the clip.
    w = cr.wave
    # It sways over the feet that carry it and sags toward the one it lifts, and dips each time a foot comes down.
    landing = sum(bump(cycle, (PHASE[n] + 1.0) % 1.0, 0.06) for n in PHASE)
    pose["move"] = (0.2 * w(cycle), 0.05 * w(cycle * 2), -CROUCH - 0.07 * landing + 0.03 * w(cycle * 4 + 0.1))
    add(pose, "Body", 2 * w(cycle * 2 - 0.1), -6 * w(cycle), 4 * w(cycle))
    add(pose, "Chest", 1.5 * w(cycle * 2 - 0.15), -3 * w(cycle - 0.08), -5 * w(cycle - 0.1))
    add(pose, "Maw", -3 * w(cycle * 2 - 0.25), 4 * w(cycle - 0.2), -8 * w(cycle - 0.22))  # The head swings late.
    gape = belly(pose, t)
    writhe(pose, t, splay=gape)
    for name, contact in CONTACT.items():
        stride = SPEED * CYCLE * DUTY
        offset, planted = cr.step(cycle, PHASE[name], DUTY, stride, 0.45)
        u = ((cycle - PHASE[name]) % 1.0 - DUTY) / (1 - DUTY)
        turn = (0, 0, 0) if planted else (-18 * math.sin(math.pi * u), 0, 0)
        pose["feet"][name] = cr.target_for(name, contact + offset, turn)
        pose["foot_turn"][name] = turn
    return pose


LASHES = (0.12, 0.45, 0.78)  # Its three lashes in the clip, about one a strike of its Spore Lash.


def lash(t):
    pose = stance()
    w = cr.wave
    whip = 0.0
    lunge = 0.0
    for at in LASHES:
        d = t - at
        if -0.07 < d < 0.0:
            whip += -cr.ease((d + 0.07) / 0.07)  # Drawn back and up.
        elif 0.0 <= d < 0.03:
            whip += -1 + 2.2 * (d / 0.03)  # Thrown forward hard.
        elif 0.03 <= d < 0.12:
            whip += 1.2 * (1 - cr.ease((d - 0.03) / 0.09))
        lunge += bump(t, at + 0.01, 0.035)
    pose["move"] = (0, -0.3 * lunge, -CROUCH - 0.08 * lunge + 0.02 * w(t * 3))
    add(pose, "Body", 3 * lunge, 0, 0)
    add(pose, "Chest", 5 * lunge, 0, 0)
    add(pose, "Maw", 12 * lunge - 6 * max(-whip, 0), 0, 0)
    gape = belly(pose, t)
    writhe(pose, t, calm=0.7, splay=gape, whip=whip)
    return pose


def idle(t):
    pose = stance()
    pose["move"] = (0, 0, -CROUCH - 0.03 * cr.wave(t))
    add(pose, "Chest", 1.5 * cr.wave(t), 0, 0)
    add(pose, "Maw", 2 * cr.wave(t + 0.2), 0, 4 * cr.wave(t * 2))
    pose["scale"]["Belly_1"] = (1 + 0.03 * cr.wave(t), 1, 1 + 0.03 * cr.wave(t))
    writhe(pose, t, calm=0.8)
    return pose


def fall(t):
    """Its legs give way one after another and it sinks onto its belly, rolling a little, its head dropping and the tendrils going limp."""
    s = cr.ease(t / 0.6)
    late = cr.ease((t - 0.3) / 0.5)
    pose = stance()
    pose["move"] = (0.15 * s, 0.1 * s, -CROUCH - 1.25 * s)
    add(pose, "Body", 2 * s, 12 * s, 3 * s)
    add(pose, "Chest", 6 * late, 4 * late, 0)
    add(pose, "Maw", 22 * late, 0, 10 * late)
    pose["scale"]["Belly_1"] = (1 - 0.1 * late, 1, 1 - 0.12 * late)
    pose["scale"]["Belly_0"] = (1 - 0.1 * late, 1, 1 - 0.12 * late)
    writhe(pose, t * 0.5, calm=1.4 * (1 - cr.ease(t / 0.9)), droop=late)
    for name, contact in CONTACT.items():
        out = Vector((1 if contact.x > 0 else -1, 0, 0))
        delay = {"FR": 0.0, "RL": 0.08, "FL": 0.15, "RR": 0.22}[name]
        give = cr.ease((t - delay) / 0.5)
        pose["feet"][name] = cr.target_for(name, contact + out * 0.7 * give + Vector((0, -0.3 * give, 0)))
    return pose


CLIPS = os.environ.get("HB_CLIPS", "Walk,Idle,Lash,Dead").split(",")
if "Walk" in CLIPS:
    cr.clip("Walk", round(SPAWN_SECONDS * FPS), walk)
if "Idle" in CLIPS:
    cr.clip("Idle", 2 * FPS, idle)
if "Lash" in CLIPS:
    cr.clip("Lash", round(SPAWN_SECONDS * FPS), lash)
if "Dead" in CLIPS:
    cr.clip("Dead", round(2.5 * FPS), fall, floor=cr.vertices_not_of("Tendril"))
slide = cr.walk_slide("Walk", list(CONTACT), SPEED, floor=0.1) if "Walk" in CLIPS else None

SIDE, QUARTER = (1, 0.05, 0.12), (0.8, -1, 0.4)
CENTRE = (0, -0.5, 1.6)
for clip_name, count in (("Walk", 12), ("Lash", 12), ("Dead", 8), ("Idle", 4)):
    if clip_name not in CLIPS or clip_name not in os.environ.get("HB_PICTURES", "Walk,Lash,Dead,Idle"):
        continue
    shares = [i / (count - 1) for i in range(count)] if clip_name == "Dead" else [i / count for i in range(count)]
    for view_name, view in (("side", SIDE), ("quarter", QUARTER)):
        cr.strip(cr.film(clip_name, f"{clip_name}_{view_name}", view, CENTRE, 17, shares=shares, size=(420, 320)), f"strip_{clip_name}_{view_name}", columns=4)
if "Walk" in CLIPS and "Belly" in os.environ.get("HB_PICTURES", "Walk,Lash,Dead,Idle,Belly"):
    # Close up from below and the side: the belly swelling, the wave of contraction, the maw gaping at the birth.
    shares = [BIRTH + d for d in (-0.24, -0.14, -0.1, -0.07, -0.04, 0.0, 0.03, 0.08)]
    cr.strip(cr.film("Walk", "Belly", (1, 0.1, -0.1), (0, 1.0, 1.6), 8, shares=shares, size=(420, 320)), "strip_Belly", columns=4)
if ONLY:
    sys.exit(0)
cr.finish({"moving": "Walk", "shooting": "Lash", "dead": "Dead", "still": "Idle"},
          {"speed": SPEED, "walk_slide": slide, "spawn_moment": BIRTH, "tendrils": len(TENDRILS)})
