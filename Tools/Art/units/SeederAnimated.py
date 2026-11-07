# The Seeder, animated (D93): its static model (Seeder.py, the owner's Meshy model) rigged here so the crowd baker can play it. A floating,
# jellyfish-like Flood organism: its bone-white head drifts and bobs, and each of its long ribbon tendrils has a chain of bones of its own
# and undulates in flowing waves to its own rhythm, the red branching fronds at their tips gently contracting and expanding. To fire a
# spore its body pulses and its tentacles lash forward, then trail back as it recoils. It dies limp, rolling over, its tendrils going
# slack (the game drops the body to the ground as it falls). Sizes in metres at its length in the game (8 m), front toward -Y; centred on
# its middle, as the game flies it by its middle. Its tendrils are welded together in bundles at their roots, so they are found as
# strands (creature_rig.strands): each split of a bundle starts a new strand, its chain hanging from the one it split from.
#   blender --background --factory-startup --python Tools/Art/units/SeederAnimated.py
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
SPORE_SECONDS = 2.5  # Seed Spore's fire interval: the firing clip is one shot long.
SHOT_MOMENT = 0.35  # The share of the firing clip at which the spore leaves: as the body squeezes and the tendrils lash.
HEAD_BACK = -2.3  # A piece that ends ahead of this is part of the head (a wisp or a jaw), and follows it.

mesh = cr.load("SeederAnimated", "Seeder", "Meshy/Meshy_AI_Seeder.glb", length=8.0, fps=FPS, air=True)
cr.join_seams(False)  # Its seams were welded in its preparation; its tendrils touch only at points, inside the head.

spec = [("Body", (-0.1, -2.6, 0.0), (-0.1, -3.95, 0.0), None, 0.5),
        ("Muzzle", (-0.1, -3.95, -0.05), (-0.1, -4.15, -0.05), "Body")]
piece_bones = {}
TENDRILS = []
for p, members in enumerate(cr.pieces()):
    ys = [mesh.data.vertices[v].co.y for v in members]
    if max(ys) < HEAD_BACK or len(members) < 10:
        piece_bones[min(members)] = ["Body"]
        continue
    found = cr.strands(members, axis=1, step=0.35)
    names_of = {}
    owned = ["Body"]
    for s, (points, branch_of, at) in enumerate(found):
        if branch_of >= 0:
            points = [found[branch_of][0][at]] + points
        length = sum((points[i + 1] - points[i]).length for i in range(len(points) - 1))
        if length < 0.8 or len(points) < 3:
            continue
        count = max(2, min(5, round(length / 1.3)))
        # Even steps along the strand.
        marks, total, picked = [0.0], 0.0, [points[0]]
        for i in range(len(points) - 1):
            total += (points[i + 1] - points[i]).length
            marks.append(total)
        for k in range(1, count + 1):
            target = total * k / count
            i = next(j for j in range(1, len(marks)) if marks[j] >= target - 1e-6)
            f = (target - marks[i - 1]) / max(marks[i] - marks[i - 1], 1e-6)
            picked.append(points[i - 1].lerp(points[i], f))
        root_parent = "Body"
        if branch_of >= 0 and branch_of in names_of:
            # The bone of the parent strand that holds the split.
            parent_names, parent_marks = names_of[branch_of]
            along = sum((found[branch_of][0][i + 1] - found[branch_of][0][i]).length for i in range(at))
            root_parent = parent_names[min(len(parent_names) - 1, int(along / max(parent_marks, 1e-6) * len(parent_names)))]
        names = [f"Tendril_{p}_{s}_{i}" for i in range(count)]
        for i in range(count):
            spec.append((names[i], tuple(picked[i]), tuple(picked[i + 1]), root_parent if i == 0 else names[i - 1], 0.12))
        names_of[s] = (names, total)
        owned += names
        TENDRILS.append({"names": names, "length": total, "root": picked[0], "tip": picked[-1]})
    piece_bones[min(members)] = owned
print(f"REPORT {len(TENDRILS)} tendril strands, {sum(len(t['names']) for t in TENDRILS)} bones")

arm = cr.skeleton(spec)
if ONLY in ("bones", ""):
    cr.bone_picture("bones", planes=("top", "side"), step=0.5)
if ONLY == "bones":
    sys.exit(0)

print("REPORT vertices per bone:", {k: v for k, v in cr.weigh(blend=0.3, smooth=3, whole_below=0, piece_bones=piece_bones).items() if not k.startswith("Tendril")})
for name, direction in (("weights_top", (0.05, 0.1, 1)), ("weights_quarter", (0.8, -1, 0.6))):
    cr.weight_picture(name, direction, (0, 0, 0), 14)
if ONLY == "weights":
    sys.exit(0)

# ---- Clips ----
rng = np.random.default_rng(93)
for tendril in TENDRILS:
    # Each its own rhythm, in whole cycles of a 4 s clip so the clips loop; its own phase, size, and frond pulse.
    tendril["cycles"] = int(rng.integers(1, 3))
    tendril["phase"] = float(rng.uniform(0, 1))
    tendril["size"] = float(rng.uniform(6, 11))
    tendril["side"] = 1.0 if tendril["tip"].x >= tendril["root"].x else -1.0
    tendril["frond"] = float(rng.uniform(0, 1))


def add(pose, bone, x=0.0, y=0.0, z=0.0):
    old = pose["turn"].get(bone, (0, 0, 0))
    pose["turn"][bone] = (old[0] + x, old[1] + y, old[2] + z)


def undulate(pose, t, calm=1.0, lash=0.0, trail=0.0, limp=0.0, fronds=1.0):
    """
    The tendrils: a wave runs down each, side to side and a little up and down, to its own rhythm. lash throws them up and forward over the
    body, trail lets them stream back and down, limp makes them hang slack. Their fronds (the last bone) swell and shrink.
    """
    for tendril in TENDRILS:
        count = len(tendril["names"])
        for i, name in enumerate(tendril["names"]):
            lag = 0.13 * i
            k = tendril["size"] * calm * (0.55 + 0.35 * i)
            side = k * cr.wave(tendril["cycles"] * t + tendril["phase"] - lag)
            rise = 0.5 * k * cr.wave(tendril["cycles"] * t + tendril["phase"] + 0.25 - lag)
            add(pose, name, rise + (26 * lash - 14 * trail - 22 * limp) * (1.0 if i == 0 else 0.3), 0, side)
        pulse = 1 + fronds * 0.14 * cr.wave(2 * t + tendril["frond"])
        pose["scale"][tendril["names"][-1]] = (pulse, 1 + 0.5 * (pulse - 1), pulse)


def drift(t):
    """Floating: the head bobs and nods, its bell pulsing gently, as the tendrils undulate behind it."""
    w = cr.wave
    pose = {"move": (0.06 * w(t + 0.3), 0.08 * w(2 * t), 0.18 * w(t)), "turn": {}, "scale": {}, "feet": {}, "foot_turn": {}}
    add(pose, "Body", 4 * w(t - 0.15), 3 * w(t + 0.2), 3 * w(t * 2 + 0.1))
    bell = 1 + 0.05 * w(2 * t)
    pose["scale"]["Body"] = (bell, 1, bell)
    undulate(pose, t)
    return pose


def hover(t):
    pose = drift(t)
    pose["move"] = tuple(0.6 * c for c in pose["move"])
    return pose


def fire(t):
    """It swells, squeezes hard as it spits the spore (SHOT_MOMENT), the tendrils lashing forward, then is pushed back and drifts on."""
    w = cr.wave
    swell = cr.ease(t / 0.25) * (1 - cr.ease((t - 0.27) / 0.06)) if t < 0.33 else 0.0
    squeeze = math.exp(-((t - SHOT_MOMENT) / 0.05) ** 2)
    lash = math.exp(-((t - (SHOT_MOMENT + 0.04)) / 0.07) ** 2)
    recoil = cr.ease((t - SHOT_MOMENT) / 0.12) * (1 - cr.ease((t - 0.55) / 0.4)) if t > SHOT_MOMENT else 0.0
    pose = {"move": (0.04 * w(t), 0.45 * recoil, 0.12 * w(t) + 0.08 * recoil), "turn": {}, "scale": {}, "feet": {}, "foot_turn": {}}
    add(pose, "Body", -14 * recoil + 6 * squeeze + 3 * w(t), 0, 0)
    size = 1 + 0.14 * swell - 0.2 * squeeze
    pose["scale"]["Body"] = (size, 1 - 0.08 * squeeze, size)
    undulate(pose, t, calm=0.8, lash=lash, trail=0.7 * recoil)
    return pose


def fall(t):
    """Struck dead: a last spasm, then it rolls over, nose down, the tendrils going slack and the fronds closing."""
    spasm = math.exp(-((t - 0.08) / 0.06) ** 2)
    s = cr.ease((t - 0.1) / 0.7)
    pose = {"move": (0, 0, -0.3 * s), "turn": {}, "scale": {}, "feet": {}, "foot_turn": {}}
    add(pose, "Body", 35 * s - 10 * spasm, 110 * s, 15 * s)
    shrink = 1 - 0.2 * s
    pose["scale"]["Body"] = (shrink + 0.15 * spasm, 1, shrink + 0.15 * spasm)
    undulate(pose, t * 0.5, calm=1.5 * (1 - s) + 0.3, lash=0.6 * spasm, limp=s, fronds=1 - s)
    for tendril in TENDRILS:
        size = 1 - 0.3 * s
        pose["scale"][tendril["names"][-1]] = (size, size, size)
    return pose


CLIPS = os.environ.get("HB_CLIPS", "Drift,Hover,Fire,Dead").split(",")
if "Drift" in CLIPS:
    cr.clip("Drift", 4 * FPS, drift)
if "Hover" in CLIPS:
    cr.clip("Hover", 4 * FPS, hover)
if "Fire" in CLIPS:
    cr.clip("Fire", round(SPORE_SECONDS * FPS), fire)
if "Dead" in CLIPS:
    cr.clip("Dead", round(2.0 * FPS), fall)

TOP, QUARTER, SIDE = (0.05, 0.25, 1), (0.8, -1, 0.7), (1, 0.05, 0.15)
for clip_name, count in (("Drift", 8), ("Fire", 8), ("Dead", 6)):
    if clip_name not in CLIPS or clip_name not in os.environ.get("HB_PICTURES", "Drift,Fire,Dead").split(","):
        continue
    shares = [i / (count - 1) for i in range(count)] if clip_name == "Dead" else [i / count for i in range(count)]
    for view_name, view in (("quarter", QUARTER), ("side", SIDE)):
        cr.strip(cr.film(clip_name, f"{clip_name}_{view_name}", view, (0, 0.3, 0), 13, shares=shares, size=(400, 320)), f"strip_{clip_name}_{view_name}", columns=4)
if ONLY:
    sys.exit(0)
cr.finish({"moving": "Drift", "shooting": "Fire", "dead": "Dead", "still": "Hover"}, {"shot_moment": SHOT_MOMENT, "strands": len(TENDRILS)})
