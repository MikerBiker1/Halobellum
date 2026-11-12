# The Spartans', the Elite's and the Zealot's own walks and shots (D129), at the owner's word: Meshy's library clips, which they shared with
# the Marines, did not fit them. Each keeps Meshy's rig, weights and death; the new clips are added beside Meshy's in its file:
#   Spartan, Spartan Heavy Weapons  walk with the big weapon resting on the right shoulder, the left arm swinging free; when firing they stand
#                                   still and shoulder it, with a small kick on each shot;
#   Spartan Fireteam, Grenadier, Sniper  walk with the weapon held low in front of them, aimed at nothing; when firing they stand, shoulder it
#                                   and aim, with a small kick on each shot;
#   Spartan CQB                     runs low with the shotgun close to the right hip in both hands, and fires it from there;
#   Elite                           walks with the plasma rifle in the right hand at hip level, pointing forward; when firing the arm comes up,
#                                   straight out in front, with a small kick on each shot;
#   Zealot                          runs with the right arm straight, the sword pointing down a little behind it; it strikes with Meshy's slash.
# How: the poses are built from where things should be, not bone by bone. The feet are planted and slide back under the body at the unit's own
# speed (the battle moves it; the baker plays a moving clip on the spot), so they do not skate; the legs reach them by turning the hip and
# knee (two-bone IK, the knee towards the front), the hips dipping where a long step needs it. The arms reach the weapon the same way.
# The baker (AnimationBaker.cs) fixes the weapon to the right hand as the hand is halfway through the shooting clip: from the right wrist
# towards the left (along the forearm for the Elite and the Zealot), level. So the shooting clips are made first, and every other clip puts
# the right hand where that fixed weapon then lies as wanted (on the shoulder, held low, at the hip).
# A shooting clip lasts one shot of the unit's main weapon at its base rate, the kick near its start, and is still at its middle; the body's
# shotMoment keeps it on the shots.
# Writes Prepared/<unit>_Clips/: <unit>.fbx (Meshy's mesh, rig and clips, and these), report.json and pictures (shots/<body>_<role>.png).
#   HB_UNIT=Spartan blender --background --factory-startup --python Tools/Art/units/SoldierClips.py
#   HB_UNIT=Elite blender --background --factory-startup --python Tools/Art/units/SoldierClips.py
# HB_POSES_ONLY=1 writes only the pictures; HB_BODIES=SpartanCQB,... only those bodies' pictures.
# Facing -Y, feet on the origin, the character's left is +X; metres, Z up.
import json
import math
import os
import shutil
import sys

import bpy
import numpy as np
from mathutils import Euler, Matrix, Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt  # noqa: E402

UNIT = os.environ.get("HB_UNIT", "Spartan")
POSES_ONLY = os.environ.get("HB_POSES_ONLY") == "1"
ONLY = [c for c in os.environ.get("HB_BODIES", "").split(",") if c]
SOURCE = os.path.join(mt.SOURCE, "Prepared", UNIT + "_Rig", UNIT + ".fbx")  # Meshy's file as it came (rig_finish.py), never this script's output.
MODELS = os.path.join(mt.ROOT, "Assets", "_Project", "Art", "Imported", "Models")
FPS = 30
UP = Vector((0, 0, 1))

folder = os.path.join(mt.PREPARED, UNIT + "_Clips")
shutil.rmtree(folder, ignore_errors=True)
os.makedirs(os.path.join(folder, "shots"))

# --- Meshy's file, made ready: in metres and unscaled, as the other units' Blender files are (Meshy writes centimetres under a 0.01 scale).
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SOURCE)
bpy.context.view_layer.update()
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
mesh = next(o for o in bpy.context.scene.objects if o.type == "MESH")
arm.animation_data_create()
arm.animation_data.action = None
for pb in arm.pose.bones:
    pb.rotation_mode = "QUATERNION"
    pb.rotation_quaternion = (1, 0, 0, 0)
    pb.location = (0, 0, 0)
    pb.scale = (1, 1, 1)
scale = arm.scale[0]
for modifier in list(mesh.modifiers):
    mesh.modifiers.remove(modifier)
mt._select([mesh])
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
mt._select([mesh, arm])
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def fcurves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves


MESHY = []
for action in list(bpy.data.actions):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in [c for c in bag.fcurves if not c.data_path.startswith("pose.bones")]:
                    bag.fcurves.remove(curve)  # Meshy's clips also key the rig's own 0.01 scale, which would shrink it again.
    for curve in fcurves(action):
        if curve.data_path.endswith(".location"):  # A bone's place is in the rig's units, which were centimetres.
            for point in curve.keyframe_points:
                point.co.y *= scale
                point.handle_left.y *= scale
                point.handle_right.y *= scale
    action.name = action.name.split("|")[-1]
    action.use_fake_user = True
    MESHY.append(action.name)
mesh.parent = arm
mesh.modifiers.new("Rig", "ARMATURE").object = arm
bpy.context.view_layer.update()
low, high = mt._bounds([mesh])
HEIGHT = high.z - low.z
print(f"REPORT {UNIT}: {HEIGHT:.3f} m tall, from x {low.x:.3f} to {high.x:.3f}; Meshy's clips {MESHY}")

BONES = arm.data.bones
REST = {b.name: b.matrix_local.copy() for b in BONES}
OFFSET = {b.name: REST[b.parent.name].inverted() @ REST[b.name] for b in BONES if b.parent}
ORDER = []


def _walk(bone):
    ORDER.append(bone.name)
    for child in bone.children:
        _walk(child)


for b in BONES:
    if b.parent is None:
        _walk(b)


def at(name):
    return REST[name].translation.copy()


def rest_rot(name):
    return REST[name].to_3x3()


def turn(x=0.0, y=0.0, z=0.0):
    """A turn in the model's axes, degrees: x leans forward, y tilts to the left, z turns to the left."""
    return Euler((math.radians(x), math.radians(y), math.radians(z)), "XYZ").to_matrix()


def frame(forward, up=UP):
    """A turn whose second axis is forward and third is up (made square to forward); its first is then to the right."""
    f = forward.normalized()
    u = (up - f * up.dot(f)).normalized()
    return Matrix((f.cross(u), f, u)).transposed()


def about(axis, degrees):
    return Matrix.Rotation(math.radians(degrees), 3, axis.normalized())


def placed(rot, position):
    m = rot.to_4x4()
    m.translation = position
    return m


def unit(x, y, z):
    return Vector((x, y, z)).normalized()


# --- A pose: world turns for some bones (rest orientation included) and the hips' place; everything else follows its parent.
def fk(rots, hips_at):
    posed = {}
    for name in ORDER:
        b = BONES[name]
        if b.parent:
            inherited = posed[b.parent.name] @ OFFSET[name]
        else:
            inherited = REST[name].copy()
            inherited.translation = hips_at
        r = rots.get(name)
        posed[name] = inherited if r is None else placed(r, inherited.translation)
    return posed


def limb(upper, lower, end, root, target, pole, end_rot=None, end_rel=None, twist_share=0.0):
    """Two-bone IK: world turns for upper, lower and end so that end's joint reaches target (as near as it can), the middle joint towards
    pole. end_rot: the end's world turn; end_rel: its turn against the lower bone; else as at rest. A share of the end's twist goes to the
    lower bone, as a forearm turns with the hand. Returns the turns and how far short of the target it stayed."""
    a = (at(lower) - at(upper)).length
    b = (at(end) - at(lower)).length
    u0 = (at(lower) - at(upper)).normalized()
    f0 = (at(end) - at(lower)).normalized()
    d0 = (at(end) - at(upper)).normalized()
    m0 = (u0 - d0 * u0.dot(d0)).normalized()
    hinge0 = u0.cross(f0).normalized()
    sign = 1.0 if hinge0.dot(d0.cross(m0)) >= 0 else -1.0
    d = target - root
    reach = min(max(d.length, abs(a - b) + 1e-4), (a + b) * 0.9995)
    dh = d.normalized()
    m = pole - dh * pole.dot(dh)
    if m.length < 1e-6:
        m = Vector((0, 0, -1)) - dh * (-dh.z)
    m.normalize()
    cos_a = max(-1.0, min(1.0, (a * a + reach * reach - b * b) / (2 * a * reach)))
    sin_a = math.sqrt(1 - cos_a * cos_a)
    elbow = root + (dh * cos_a + m * sin_a) * a
    wrist = root + dh * reach
    u = (elbow - root).normalized()
    f = (wrist - elbow).normalized()
    hinge = (dh.cross(m) * sign).normalized()
    turn_u = Matrix((u, hinge, u.cross(hinge))).transposed() @ Matrix((u0, hinge0, u0.cross(hinge0)))
    turn_f = Matrix((f, hinge, f.cross(hinge))).transposed() @ Matrix((f0, hinge0, f0.cross(hinge0)))
    lower_rot = turn_f @ rest_rot(lower)
    if end_rel is not None:
        end_rot = lower_rot @ end_rel
    elif end_rot is None:
        end_rot = turn_f @ rest_rot(end)
    elif twist_share:
        rel = (lower_rot.inverted() @ end_rot) @ (rest_rot(lower).inverted() @ rest_rot(end)).inverted()
        q = rel.to_quaternion()
        twist = 2 * math.degrees(math.atan2(q.y, q.w))  # About the lower bone's own length, its y.
        twist = (twist + 180) % 360 - 180
        lower_rot = about(f, twist * twist_share) @ lower_rot
    return {upper: turn_u @ rest_rot(upper), lower: lower_rot, end: end_rot}, (target - wrist).length


def bend(rots, lower, end):
    """How far a wrist is bent beyond its rest, in degrees."""
    rel = (rots[lower].inverted() @ rots[end]) @ (rest_rot(lower).inverted() @ rest_rot(end)).inverted()
    return math.degrees(rel.to_quaternion().angle)


def arm_ik(side, root, spec):
    """An arm to its target; with a hand turn given, the elbow is swung about the shoulder-wrist line (up to swivel degrees from the pole)
    to bend the wrist least."""
    names = (side + "Arm", side + "ForeArm", side + "Hand")
    hand, rel, swivel = spec.get("hand"), spec.get("hand_rel"), spec.get("swivel", 0.0)
    target, pole = spec["target"], spec["pole"]
    axis = (target - root).normalized()
    best = None
    for s in (np.linspace(-swivel, swivel, 13) if hand is not None and swivel else [0.0]):
        rots, short = limb(*names, root, target, about(axis, s) @ pole, hand, rel, twist_share=0.5 if hand is not None else 0.0)
        cost = (bend(rots, names[1], names[2]) if hand is not None else 0.0) + abs(s) * 0.15
        if best is None or cost < best[0]:
            best = (cost, rots, short)
    return best[1], best[2]


# --- The feet: on the ground they slide back at the unit's speed; then they swing forward through the air.
ANKLE = {s: at(s + "Foot") for s in ("Left", "Right")}
BALL = {s: at(s + "ToeBase") for s in ("Left", "Right")}
LEG = {s: (at(s + "Leg") - at(s + "UpLeg")).length + (at(s + "Foot") - at(s + "Leg")).length for s in ("Left", "Right")}


def foot(side, forward, lift=0.0, pitch=0.0, wide=0.0, yaw=0.0):
    """The ankle's target and the foot's and toes' turns: forward of its rest place (metres), lifted, pitched (degrees: heel up when positive,
    about the ball, the toes staying flat; toes up when negative, about the heel), moved outwards and turned outwards."""
    out = 1.0 if side == "Left" else -1.0
    ankle, ball = ANKLE[side], BALL[side]
    heel = Vector((ankle.x, ankle.y + (ankle.y - ball.y) * 0.3, 0.0))
    pivot = ball if pitch >= 0 else heel
    r = turn(x=pitch)  # A turn forward about x lifts what is behind the pivot.
    spin = turn(z=yaw * out)
    target = spin @ (pivot + r @ (ankle - pivot) - ball) + ball + Vector((wide * out, -forward, lift))
    return target, spin @ r, (spin if pitch >= 0 else spin @ r)


def stride(phase, stance, length, lift, strike=8.0, push=22.0):
    """One foot through a stride: phase 0 is its heel striking, stance the share of the stride it is on the ground. Returns forward, lift,
    pitch for foot() and whether it is planted."""
    phase %= 1.0
    if phase < stance:
        s = phase / stance
        pitch = -strike * max(0.0, 1 - s / 0.15) + push * max(0.0, (s - 0.7) / 0.3) ** 2
        return length * (0.5 - s), 0.0, pitch, True
    s = (phase - stance) / (1 - stance)
    forward = length * (-0.5 + (0.5 - 0.5 * math.cos(math.pi * s)))
    up = lift * math.sin(math.pi * s) ** 1.3
    pitch = push * (1 - s) ** 2 * (1 - s) - strike * s ** 3 + 25 * math.sin(math.pi * s) * (1 - s) ** 0.5
    return forward, up, pitch, False


# --- A whole pose from a description.
TORSO = ["Spine02", "Spine01", "Spine"]


def body_turns(p):
    """World turns of the hips and spine, the chest's turn, and the hips' place."""
    rots = {}
    t = turn(*p.get("hips_turn", (0, 0, 0)))
    rots["Hips"] = t @ rest_rot("Hips")
    chest = t
    for name, d in zip(TORSO, p.get("spine", [(0, 0, 0)] * 3)):
        chest = chest @ turn(*d)
        rots[name] = chest @ rest_rot(name)
    return rots, chest, at("Hips") + Vector((0, 0, p.get("hips", 0.0)))


def chest_move(p):
    """The chest's rigid move from rest in pose p, for what it carries; with the dip the feet will force (see pose)."""
    rots, _, hips_at = body_turns(p)
    hips_at.z -= dip(p, fk(rots, hips_at))
    posed = fk(rots, hips_at)
    return posed["Spine"] @ REST["Spine"].inverted()


def dip(p, posed):
    """How far the hips must come down for every planted foot to be within reach."""
    lower = 0.0
    for side, (target, _, _) in p.get("feet", {}).items():
        if p.get("planted", {}).get(side, True):
            gap = target - posed[side + "UpLeg"].translation
            limit = LEG[side] * 0.985
            flat = Vector((gap.x, gap.y, 0)).length
            if gap.length > limit and flat < limit:
                lower = max(lower, -gap.z - math.sqrt(limit * limit - flat * flat))
    return lower


def pose(p):
    """p: hips (dz), hips_turn and spine (three turns, each on the last), head (a world turn: it looks ahead), feet {side: foot()},
    planted {side: bool}, arms {side: {target, pole, hand | hand_rel, swivel}} (targets in the world). Returns the posed bones and how far
    each limb stayed short of its target."""
    rots, chest, hips_at = body_turns(p)
    head = turn(*p.get("head", (0, 0, 0)))
    rots["neck"] = chest.to_quaternion().slerp(head.to_quaternion(), 0.5).to_matrix() @ rest_rot("neck")
    rots["Head"] = head @ rest_rot("Head")
    posed = fk(rots, hips_at)
    lower = dip(p, posed)
    if lower > 0:
        hips_at.z -= lower
        posed = fk(rots, hips_at)
    shorts = {}
    for side, (target, rot, toe) in p.get("feet", {}).items():
        out = 1.0 if side == "Left" else -1.0
        got, short = limb(side + "UpLeg", side + "Leg", side + "Foot", posed[side + "UpLeg"].translation, target, unit(0.15 * out, -1, 0),
                          rot @ rest_rot(side + "Foot"))
        rots.update(got)
        rots[side + "ToeBase"] = toe @ rest_rot(side + "ToeBase")
        shorts[side + " foot"] = short
    for side, spec in p.get("arms", {}).items():
        got, short = arm_ik(side, posed[side + "Arm"].translation, spec)
        rots.update(got)
        shorts[side + " hand"] = short
    return fk(rots, hips_at), shorts


# --- Keys.
CLIPS, SHORTS = {}, {}


def key_pose(posed, frame_no, previous):
    for name in ORDER:
        b = BONES[name]
        inherited = (posed[b.parent.name] @ OFFSET[name]) if b.parent else REST[name]
        basis = inherited.inverted() @ posed[name]
        pb = arm.pose.bones[name]
        q = basis.to_quaternion()
        if name in previous and previous[name].dot(q) < 0:
            q.negate()
        previous[name] = q
        pb.rotation_quaternion = q
        pb.keyframe_insert("rotation_quaternion", frame=frame_no)
        if not b.use_connect:
            pb.location = basis.translation
            pb.keyframe_insert("location", frame=frame_no)


def make(name, frames, description):
    """A clip of frames + 1 keys (the last as the first, for a loop) from description(t), t from 0 to 1."""
    action = bpy.data.actions.new(name)
    arm.animation_data.action = action
    previous, worst = {}, {}
    for f in range(frames + 1):
        posed, shorts = pose(description(f / frames))
        key_pose(posed, f + 1, previous)
        for k, v in shorts.items():
            worst[k] = max(worst.get(k, 0.0), v)
    action.use_fake_user = True
    action.use_frame_range = True
    action.frame_start = 1
    action.frame_end = frames + 1
    CLIPS[name] = frames
    SHORTS[name] = {k: round(v, 3) for k, v in worst.items() if v > 0.005}
    print(f"REPORT clip {name}: {frames} frames ({frames / FPS:.2f} s); short of a target by over 5 mm: {SHORTS[name]}")


def sin(t):
    return math.sin(t * math.pi * 2)


def cos(t):
    return math.cos(t * math.pi * 2)


def kick(seconds, length):
    """A shot's kick, 0 to 1, at a tenth of a clip of length seconds: up in 0.05 s, back down before the clip's middle."""
    at_s = 0.1 * length
    recover = min(0.35, 0.5 * length - at_s - 0.03)
    s = seconds - at_s
    if s < 0 or s > recover:
        return 0.0
    rise = 0.05
    if s < rise:
        return math.sin(s / rise * math.pi / 2)
    return 0.5 * (1 + math.cos((s - rise) / (recover - rise) * math.pi))


# --- The weapon as the baker fixes it (AnimationBaker.BuildMesh): at the right wrist, pointing to the left wrist (or along the forearm), level.
def weapon_frame(posed, along_forearm):
    wrist = posed["RightHand"].translation
    direction = (wrist - posed["RightForeArm"].translation) if along_forearm else (posed["LeftHand"].translation - wrist)
    return placed(frame(direction), wrist)


def grip_of(posed, along_forearm):
    """The weapon's turn against the right hand: what the baker keeps fixed."""
    return posed["RightHand"].to_3x3().inverted() @ weapon_frame(posed, along_forearm).to_3x3()


def hand_for(weapon_rot, grip):
    """The right hand's world turn that puts the fixed weapon at weapon_rot."""
    return weapon_rot @ grip.inverted()


def on_line(origin, direction, shoulder, near, far):
    """Where the left hand takes a weapon lying from origin along direction: the point of the line nearest its shoulder, kept between near
    and far."""
    s = max(near, min(far, (shoulder - origin).dot(direction)))
    return origin + direction * s


def walker(t, speed, period, stance, lift, hips=-0.05, bob=0.02, lean=3.0, sway=6.0, counter=9.0, roll=2.0, wide=0.0, toes=6.0,
           strike=8.0, push=22.0, centre=0.03, run=False):
    """The legs and body of a walk or run cycle at speed (m/s); t 0 is the left heel striking."""
    length = speed * period * stance
    feet, planted = {}, {}
    for side, offset in (("Left", 0.0), ("Right", 0.5)):
        forward, up, pitch, down = stride(t - offset, stance, length, lift, strike, push)
        feet[side] = foot(side, forward + centre, up, pitch, wide, toes)
        planted[side] = down
    wave = math.cos(4 * math.pi * (t - stance / 2))
    dz = hips + (-bob * wave if run else bob * wave)
    yaw = -sway * cos(t)
    tilt = -roll * math.cos(2 * math.pi * (t - stance / 2))
    each = ((counter + sway) * cos(t)) / 3
    return {
        "hips": dz,
        "hips_turn": (lean * 0.4, tilt, yaw),
        "spine": [(lean * 0.2, -tilt / 3, each)] * 3,
        "head": (lean * 0.4, 0, 0),
        "feet": feet,
        "planted": planted,
    }


def swinging_arm(side, chest, amount, out=0.10, drop=0.55):
    """A free arm hanging and swinging: amount forward (metres at the wrist), carried by the chest."""
    o = 1.0 if side == "Left" else -1.0
    root = at(side + "Arm")
    lift = 0.12 * max(0.0, amount) / 0.2
    target = chest @ (root + Vector((out * o, -amount, -drop + lift)))
    return {"target": target, "pole": chest.to_3x3() @ unit(0.25 * o, 1, -0.2)}


BODIES = []  # Each: name, weapon file, weapon length, along forearm, clips by role, shot moment.
GRIPS = {}   # Shooting clip: the weapon's turn against the hand at its middle.


def body(name, weapon, length, along, moving, shooting, still, shot=None):
    BODIES.append(dict(name=name, weapon=weapon, length=length, along=along, moving=moving, shooting=shooting, still=still, shot=shot))


if UNIT == "Spartan":
    SHOULDER = {s: at(s + "Arm") for s in ("Left", "Right")}

    # ---- Standing and firing: feet apart, left foot forward, the body turned a little to the right behind the weapon, the head upright.
    def aim_body(k=0.0):
        return {
            "hips": -0.07,
            "hips_turn": (2, 0, -14),
            "spine": [(2, 0, -6), (2, 0, -6), (2 - 4 * k, 0, -4)],
            "head": (-2, -5, 0),
            "feet": {"Left": foot("Left", 0.17, wide=0.03, yaw=-4), "Right": foot("Right", -0.16, wide=0.06, yaw=28)},
        }

    AIM_AT = Vector((-0.11, -0.25, 1.86))  # The right wrist, the weapon's grip, in front of the right shoulder at the height of the eyes.

    AIM_LINE = [Vector((0, -1, 0))]  # Where the weapon lies at the still middle: wrist to wrist as the arms reach it (found below).

    def aim_weapon(k):
        """The weapon level and straight ahead, pushed back and up by a kick."""
        direction = about(Vector((-1, 0, 0)), 6 * k) @ AIM_LINE[0]
        return placed(frame(direction), AIM_AT + Vector((0, 0.05 * k, 0.012 * k)))

    def aim_arms(w, grip, k=0.0, near=0.22, far=0.40):
        direction = w.to_3x3() @ Vector((0, 1, 0))
        p = aim_body()
        shoulder = chest_move(p) @ SHOULDER["Left"]
        arms = {"Left": {"target": on_line(w.translation, direction, shoulder, near, far), "pole": unit(0.3, 0.2, -1)}}
        right = {"target": w.translation, "pole": unit(-1, 0.4, -0.6)}
        if grip is not None and k > 0:
            right.update(hand=hand_for(w.to_3x3(), grip))
        arms["Right"] = right
        return arms

    # The grip is found from the still middle of the firing pose: the hand as the arm brings it, the weapon from wrist to wrist.
    p0 = aim_body()
    p0["arms"] = aim_arms(aim_weapon(0), None)
    posed0, short0 = pose(p0)
    GRIP_AIM = grip_of(posed0, False)
    AIM_LINE[0] = (posed0["LeftHand"].translation - posed0["RightHand"].translation).normalized()
    print(f"REPORT aim pose: short {[(k, round(v, 3)) for k, v in short0.items()]}; weapon from wrist to wrist "
          f"{tuple(round(v, 3) for v in (posed0['LeftHand'].translation - posed0['RightHand'].translation))}")

    def firing(length, shots=1, size=1.0):
        def description(t):
            seconds = t * length
            k = size * sum(kick((seconds + i * length / shots) % length, length) for i in range(shots)) if shots > 1 else size * kick(seconds, length)
            p = aim_body(k)
            p["arms"] = aim_arms(aim_weapon(k), GRIP_AIM, k)
            return p
        return description

    make("Laser_Fire", round(3.5 * FPS), firing(3.5, size=1.0))          # The Spartan Laser, one every 3.5 s.
    make("Rifle_Fire", round(0.6 * FPS), firing(0.6, size=0.6))          # The Fireteam's battle rifle, 0.6 s.
    make("Rocket_Fire", round(1.5 * FPS), firing(1.5, size=1.0))         # The Grenadier's rockets, 1.5 s.
    make("Sniper_Fire", round(3.0 * FPS), firing(3.0, size=0.9))         # The sniper rifle, 3 s.

    def chaingun(t):
        # The Heavy Weapons' chaingun fires every 0.12 s: a fast rattle, not kept on its shots, still at its middle like the others.
        k = sum(math.exp(-((((t - m) % 1.0) - 0.06) / 0.05) ** 2) for m in (0.55, 0.8, 0.05, 0.3)) * 0.25
        p = aim_body(k)
        p["arms"] = aim_arms(aim_weapon(k), GRIP_AIM, k if k > 1e-3 else 0.0)
        return p

    make("Chaingun_Fire", 16, chaingun)
    GRIPS.update({c: GRIP_AIM for c in ("Laser_Fire", "Rifle_Fire", "Rocket_Fire", "Sniper_Fire", "Chaingun_Fire")})

    # ---- Walking with the weapon on the right shoulder, pointing ahead and a little up, the left arm swinging; chest out, head up.
    CARRY_AT = Vector((-0.31, -0.20, 1.87))
    CARRY = placed(frame(unit(0.04, -1, 0.27)), CARRY_AT)

    def carry_arms(chest):
        w = chest @ CARRY
        return {"Right": {"target": w.translation, "pole": chest.to_3x3() @ unit(-0.8, 0.2, -1), "hand": hand_for(w.to_3x3(), GRIP_AIM),
                          "swivel": 60}}

    def carrying(speed, period):
        def description(t):
            p = walker(t, speed, period, 0.58, 0.13, hips=-0.05, bob=0.025, lean=-2, sway=7, counter=10, toes=8)
            chest = chest_move(p)
            p["arms"] = carry_arms(chest)
            p["arms"]["Left"] = swinging_arm("Left", chest, -0.20 * cos(t))
            return p
        return description

    make("Carry_Walk", 28, carrying(1.75, 28 / FPS))     # The Spartan, 1.75 m/s.
    make("Carry_Walk_Slow", 32, carrying(1.3, 32 / FPS))  # Heavy Weapons, 1.3 m/s.

    def carry_stand(t):
        p = {"hips": -0.02, "hips_turn": (0, 0, -4), "spine": [(-1 + 0.5 * sin(t), 0, 2)] * 3, "head": (-2, 0, 0),
             "feet": {"Left": foot("Left", 0.04, wide=0.04, yaw=8), "Right": foot("Right", -0.04, wide=0.05, yaw=12)}}
        chest = chest_move(p)
        p["arms"] = carry_arms(chest)
        p["arms"]["Left"] = swinging_arm("Left", chest, 0.0)
        return p

    make("Carry_Stand", 60, carry_stand)

    # ---- Walking with the weapon held low in front, pointing ahead and down to the left, both hands on it.
    READY_AT = Vector((-0.08, -0.34, 1.67))
    READY = placed(frame(unit(0.5, -0.75, -0.42)), READY_AT)

    def ready_arms(chest):
        w = chest @ READY
        direction = w.to_3x3() @ Vector((0, 1, 0))
        return {"Right": {"target": w.translation, "pole": chest.to_3x3() @ unit(-0.6, 0.4, -1), "hand": hand_for(w.to_3x3(), GRIP_AIM),
                          "swivel": 60},
                "Left": {"target": on_line(w.translation, direction, chest @ SHOULDER["Left"], 0.18, 0.32),
                         "pole": chest.to_3x3() @ unit(0.6, 0.3, -1)}}

    def readied(t):
        p = walker(t, 1.75, 28 / FPS, 0.58, 0.12, hips=-0.06, bob=0.02, lean=5, sway=6, counter=4, toes=7)
        p["spine"] = [(s[0], s[1], s[2] - 4) for s in p["spine"]]
        p["arms"] = ready_arms(chest_move(p))
        return p

    make("Ready_Walk", 28, readied)

    def ready_stand(t):
        p = {"hips": -0.03, "hips_turn": (1, 0, -4), "spine": [(1.5 + 0.5 * sin(t), 0, -2)] * 3, "head": (-1, 0, 0),
             "feet": {"Left": foot("Left", 0.06, wide=0.04, yaw=6), "Right": foot("Right", -0.05, wide=0.05, yaw=14)}}
        p["arms"] = ready_arms(chest_move(p))
        return p

    make("Ready_Stand", 60, ready_stand)

    # ---- CQB: the shotgun at the right hip, pointing ahead. It fires from there in a low, forward-leaning crouch, both hands on it; it
    # runs with it in the right hand, the left arm pumping.
    def hip_body(k=0.0):
        return {
            "hips": -0.17,
            "hips_turn": (8, 0, -16),
            "spine": [(4, 0, -6), (4, 0, -6), (3 - 3 * k, 0, -4)],
            "head": (-10, -3, 0),
            "feet": {"Left": foot("Left", 0.22, wide=0.08, yaw=-2), "Right": foot("Right", -0.2, wide=0.10, yaw=26)},
        }

    HIP_AT = Vector((-0.16, -0.36, 1.30))
    HIP_LINE = [unit(0.06, -1, 0)]

    def hip_weapon(k):
        direction = about(Vector((-1, 0, 0)), 9 * k) @ HIP_LINE[0]
        return placed(frame(direction), HIP_AT + Vector((0, 0.06 * k, 0.02 * k)))

    def hip_arms(w, grip, k=0.0):
        direction = w.to_3x3() @ Vector((0, 1, 0))
        shoulder = chest_move(hip_body()) @ SHOULDER["Left"]
        right = {"target": w.translation, "pole": unit(-0.8, 0.6, -0.4)}
        if grip is not None and k > 0:
            right.update(hand=hand_for(w.to_3x3(), grip))
        return {"Right": right, "Left": {"target": on_line(w.translation, direction, shoulder, 0.2, 0.36), "pole": unit(0.3, 0.3, -1)}}

    for attempt in range(2):  # The left hand stops a little short of the line, so the line is found again from where it took it.
        p0 = hip_body()
        p0["arms"] = hip_arms(hip_weapon(0), None)
        posed0, short0 = pose(p0)
        GRIP_HIP = grip_of(posed0, False)
        HIP_LINE[0] = (posed0["LeftHand"].translation - posed0["RightHand"].translation).normalized()
    print(f"REPORT hip pose: short {[(k, round(v, 3)) for k, v in short0.items()]}")

    def shotgun(t):
        k = kick(t * 0.8, 0.8)
        p = hip_body(k)
        p["arms"] = hip_arms(hip_weapon(k), GRIP_HIP, k)
        return p

    make("Shotgun_Fire", 24, shotgun)  # The M45, one every 0.8 s.
    GRIPS["Shotgun_Fire"] = GRIP_HIP

    def hip_held(p, base):
        """Both hands on the shotgun at the hip, as when it fires, the body as in base; the weapon swaying with the chest's sway in p."""
        neutral = dict(p, hips_turn=base["hips_turn"], spine=base["spine"])
        chest = chest_move(p)
        w = chest @ chest_move(neutral).inverted() @ hip_weapon(0)
        direction = w.to_3x3() @ Vector((0, 1, 0))
        return {"Right": {"target": w.translation, "pole": unit(-0.8, 0.6, -0.4), "hand": hand_for(w.to_3x3(), GRIP_HIP), "swivel": 40},
                "Left": {"target": on_line(w.translation, direction, chest @ SHOULDER["Left"], 0.2, 0.36), "pole": unit(0.3, 0.3, -1)}}

    def hip_run(t):
        # Running low with the shotgun at the hip in both hands (the owner, 2026-11-11): the upper body as when it fires, swaying with the
        # stride.
        p = walker(t, 3.5, 20 / FPS, 0.38, 0.22, hips=-0.14, bob=0.03, lean=0, sway=7, counter=5, toes=6, strike=4, push=28, run=True)
        base = hip_body()
        p["hips_turn"] = tuple(a + b for a, b in zip(p["hips_turn"], base["hips_turn"]))
        p["spine"] = [tuple(a + b for a, b in zip(s, d)) for s, d in zip(p["spine"], base["spine"])]
        p["head"] = base["head"]
        p["arms"] = hip_held(p, base)
        return p

    make("Hip_Run", 20, hip_run)  # 3.5 m/s.

    def hip_stand(t):
        base = hip_body()
        p = dict(base, hips=-0.11, spine=[(d[0] + 0.5 * sin(t), d[1], d[2]) for d in base["spine"]],
                 feet={"Left": foot("Left", 0.14, wide=0.07, yaw=0), "Right": foot("Right", -0.12, wide=0.08, yaw=22)})
        p["arms"] = hip_held(p, dict(base, hips=-0.11, feet=p["feet"]))
        return p

    make("Hip_Stand", 60, hip_stand)

    body("Spartan", "Weapon_SpartanLaser", 1.4, False, "Carry_Walk", "Laser_Fire", "Carry_Stand", shot=0.1)
    body("SpartanHeavyWeapons", "Weapon_RocketLauncher", 1.2, False, "Carry_Walk_Slow", "Chaingun_Fire", "Carry_Stand")
    body("SpartanFireteam", "Weapon_AssaultRifle", 0.95, False, "Ready_Walk", "Rifle_Fire", "Ready_Stand", shot=0.1)
    body("SpartanGrenadier", "Weapon_RocketLauncher", 0.95, False, "Ready_Walk", "Rocket_Fire", "Ready_Stand", shot=0.1)
    body("SpartanSniper", "Weapon_SniperRifle", 1.5, False, "Ready_Walk", "Sniper_Fire", "Ready_Stand", shot=0.1)
    body("SpartanCQB", "Weapon_Shotgun", 1.0, False, "Hip_Run", "Shotgun_Fire", "Hip_Stand", shot=0.1)

else:
    SHOULDER = {s: at(s + "Arm") for s in ("Left", "Right")}

    # ---- The Elite firing: the right arm straight out in front at the height of its shoulder, the rifle along the forearm, a kick each shot.
    def elite_aim_body(k=0.0):
        return {
            "hips": -0.06,
            "hips_turn": (3, 0, -8),
            "spine": [(2, 0, -4), (2, 0, -4), (1 - 3 * k, 0, -2)],
            "head": (2, 0, 0),
            "feet": {"Left": foot("Left", 0.2, wide=0.03, yaw=-2), "Right": foot("Right", -0.18, wide=0.05, yaw=18)},
        }

    def elite_aim_arms(k, grip):
        p = elite_aim_body(k)
        chest = chest_move(p)
        wrist = Vector((-0.30, -0.56, 1.98)) + Vector((0, 0.05 * k, 0.015 * k))
        right = {"target": wrist, "pole": unit(-0.4, 0.3, -1)}
        if grip is not None and k > 0:
            # The rifle lies along the forearm only at the clip's still middle, where the hand is as the arm brings it (the grip is found
            # there); a kick tips it up from there.
            right.update(hand=hand_for(about(Vector((-1, 0, 0)), 7 * k) @ PLASMA_AIM, grip))
        left = {"target": chest @ (SHOULDER["Left"] + Vector((0.16, 0.12, -0.7))), "pole": chest.to_3x3() @ unit(0.3, 1, -0.2)}
        return {"Right": right, "Left": left}

    p0 = elite_aim_body()
    p0["arms"] = elite_aim_arms(0, None)
    posed0, short0 = pose(p0)
    GRIP_PLASMA = grip_of(posed0, True)
    PLASMA_AIM = weapon_frame(posed0, True).to_3x3()
    print(f"REPORT Elite aim: short {[(k, round(v, 3)) for k, v in short0.items()]}; rifle along "
          f"{tuple(round(v, 3) for v in (weapon_frame(posed0, True).to_3x3() @ Vector((0, 1, 0))))}")

    def plasma(t):
        k = kick(t * 0.5, 0.5) * 0.8
        p = elite_aim_body(k)
        p["arms"] = elite_aim_arms(k, GRIP_PLASMA)
        return p

    make("Plasma_Fire", 15, plasma)  # The plasma rifle, one every 0.5 s.
    GRIPS["Plasma_Fire"] = GRIP_PLASMA

    # ---- The Elite walking: the rifle at hip level in front, pointing ahead and a little down, the left arm swinging. Its legs bend
    # backwards at the hock: the IK reaches the hock, the knee forward; the toes stay flat.
    ELITE_LOW = Vector((-0.36, -0.36, 1.46))

    def elite_arms(chest):
        w = chest @ placed(frame(unit(0.04, -1, -0.25)), ELITE_LOW)
        return {"Right": {"target": w.translation, "pole": chest.to_3x3() @ unit(-0.3, 1, -0.3), "hand": hand_for(w.to_3x3(), GRIP_PLASMA),
                          "swivel": 50}}

    def elite_walk(t):
        p = walker(t, 1.75, 34 / FPS, 0.6, 0.16, hips=-0.05, bob=0.025, lean=4, sway=6, counter=8, toes=4, strike=0.0, push=14, centre=0.0)
        chest = chest_move(p)
        p["arms"] = elite_arms(chest)
        p["arms"]["Left"] = swinging_arm("Left", chest, -0.24 * cos(t), out=0.14, drop=0.72)
        return p

    make("Elite_Walk", 34, elite_walk)  # 1.75 m/s.

    def elite_stand(t):
        p = {"hips": -0.04, "hips_turn": (2, 0, -3), "spine": [(1 + 0.6 * sin(t), 0, 1)] * 3, "head": (1, 0, 0),
             "feet": {"Left": foot("Left", 0.05, wide=0.03, yaw=4), "Right": foot("Right", -0.04, wide=0.04, yaw=10)}}
        chest = chest_move(p)
        p["arms"] = elite_arms(chest)
        p["arms"]["Left"] = swinging_arm("Left", chest, 0.0, out=0.14, drop=0.72)
        return p

    make("Elite_Stand", 60, elite_stand)

    # ---- The Zealot: its strike stays Meshy's slash. Its sword is fixed to the hand as the hand is halfway through it, along the forearm; so
    # its run keeps the hand as it is against the forearm there, and the sword lies along the straight arm, down and behind.
    slash = bpy.data.actions["Right_Hand_Sword_Slash"]
    arm.animation_data.action = slash
    if slash.slots and arm.animation_data.action_slot is None:
        arm.animation_data.action_slot = slash.slots[0]
    first, last = slash.frame_range
    middle = first + (last - first) / 2
    bpy.context.scene.frame_set(int(middle), subframe=middle - int(middle))
    bpy.context.view_layer.update()
    pb = arm.pose.bones
    SLASH_REL = pb["RightForeArm"].matrix.to_3x3().inverted() @ pb["RightHand"].matrix.to_3x3()
    slashed = {n: pb[n].matrix.copy() for n in ("RightHand", "RightForeArm")}
    GRIP_SWORD = slashed["RightHand"].to_3x3().inverted() @ frame(slashed["RightHand"].translation - slashed["RightForeArm"].translation)
    arm.animation_data.action = None
    GRIPS["Right_Hand_Sword_Slash"] = GRIP_SWORD
    print(f"REPORT slash middle at frame {middle}; wrist bent {math.degrees((SLASH_REL @ (rest_rot('RightForeArm').inverted() @ rest_rot('RightHand')).inverted()).to_quaternion().angle):.0f} degrees")

    SWORD_DOWN = unit(-0.16, 0.62, -0.77)  # From the shoulder: down, out a little, and some 38 degrees behind.

    def sword_arm(chest, lift=0.0):
        root = chest @ SHOULDER["Right"]
        direction = (SWORD_DOWN + Vector((0, -lift, lift * 0.3))).normalized()
        return {"target": root + direction * 0.83, "pole": unit(-0.3, 1, 0.2), "hand_rel": SLASH_REL}

    def zealot_run(t):
        p = walker(t, 3.0, 22 / FPS, 0.38, 0.26, hips=-0.1, bob=0.04, lean=14, sway=7, counter=6, toes=4, strike=0.0, push=22, centre=0.02,
                   run=True)
        chest = chest_move(p)
        p["arms"] = {"Right": sword_arm(chest, 0.04 * sin(t)),
                     "Left": swinging_arm("Left", chest, -0.32 * cos(t), out=0.12, drop=0.5)}
        return p

    make("Zealot_Run", 22, zealot_run)  # 3 m/s.

    def zealot_stand(t):
        p = {"hips": -0.06, "hips_turn": (4, 0, -6), "spine": [(2 + 0.6 * sin(t), 0, 1)] * 3, "head": (-2, 0, 0),
             "feet": {"Left": foot("Left", 0.12, wide=0.05, yaw=2), "Right": foot("Right", -0.10, wide=0.06, yaw=16)}}
        chest = chest_move(p)
        p["arms"] = {"Right": sword_arm(chest, 0.1), "Left": swinging_arm("Left", chest, 0.04, out=0.14, drop=0.7)}
        return p

    make("Zealot_Stand", 60, zealot_stand)

    body("Elite", "Weapon_PlasmaRifle", 0.85, True, "Elite_Walk", "Plasma_Fire", "Elite_Stand", shot=0.1)
    body("Zealot", "Weapon_EnergySword", 1.4, True, "Zealot_Run", "Right_Hand_Sword_Slash", "Zealot_Stand")

arm.animation_data.action = None

# --- Checks: the grip the baker will find, from each body's shooting clip as it plays, against the one its other clips were made for.
scene = bpy.context.scene
scene.render.fps = FPS


def use(action_name):
    action = bpy.data.actions[action_name]
    arm.animation_data.action = action
    if action.slots:
        arm.animation_data.action_slot = action.slots[0]
    return action


def at_share(action, share):
    first, last = action.frame_range
    f = first + (last - first) * share
    scene.frame_set(int(f), subframe=f - int(f))
    bpy.context.view_layer.update()


GRIP_CHECK = {}
for b in BODIES:
    action = use(b["shooting"])
    at_share(action, 0.5)
    posed = {n: arm.pose.bones[n].matrix.copy() for n in ("RightHand", "RightForeArm", "LeftHand")}
    found = posed["RightHand"].to_3x3().inverted() @ weapon_frame(posed, b["along"]).to_3x3()
    off = math.degrees((found @ GRIPS[b["shooting"]].inverted()).to_quaternion().angle)
    GRIP_CHECK[b["name"]] = round(off, 2)
    print(f"REPORT {b['name']}: the grip from its {b['shooting']} as played is {off:.2f} degrees from the one its clips were made for")
arm.animation_data.action = None


# --- Pictures: each body's three clips with its weapon in the hand, fixed as the baker fixes it.
def load_weapon(name, length):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, name, name + ".fbx"))
    new = [o for o in bpy.data.objects if o not in before]
    bpy.context.view_layer.update()
    tip = next(o for o in new if o.name.startswith("Muzzle")).matrix_world.translation.copy()
    parts = [o for o in new if o.type == "MESH"]
    points = [o.matrix_world @ v.co for o in parts for v in o.data.vertices]
    forward = tip.normalized()  # The grip is at the origin.
    extent = max(p.dot(forward) for p in points) - min(p.dot(forward) for p in points)
    holder = bpy.data.objects.new("holder_" + name, None)
    scene.collection.objects.link(holder)
    for o in new:
        if o.parent is None:
            o.parent = holder
    # In the holder's space the weapon lies along +Y (forward), up +Z, its grip at the origin, as long as the body's weapon.
    align = Matrix.Scale(length / extent, 4) @ placed(frame(forward), Vector()).inverted()
    return holder, align


def contact_sheet(tiles, columns, path):
    images = []
    for t in tiles:
        img = bpy.data.images.load(t)
        w, h = img.size
        px = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        images.append(px.reshape(h, w, 4))
        bpy.data.images.remove(img)
    h, w = images[0].shape[:2]
    rows = (len(images) + columns - 1) // columns
    sheet = np.ones((rows * h, columns * w, 4), dtype=np.float32)
    for i, px in enumerate(images):
        r, c = divmod(i, columns)
        sheet[(rows - 1 - r) * h:(rows - r) * h, c * w:(c + 1) * w] = px  # Blender's rows run from the bottom.
    out = bpy.data.images.new("sheet", columns * w, rows * h, alpha=True)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    for t in tiles:
        os.remove(t)


for material in mesh.data.materials:  # The Elite's texture keeps an alpha the game ignores; the pictures ignore it too.
    if material and material.node_tree:
        for link in [l for l in material.node_tree.links if l.to_socket.name == "Alpha"]:
            material.node_tree.links.remove(link)
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "TEXTURE"
scene.render.resolution_x, scene.render.resolution_y = 300, 360
scene.world = bpy.data.worlds.new("w")
scene.world.color = (0.62, 0.64, 0.68)
camera = bpy.data.objects.new("camera", bpy.data.cameras.new("camera"))
scene.collection.objects.link(camera)
scene.camera = camera
camera.data.clip_start = 0.01
camera.data.clip_end = 100
VIEWS = (("quarter", (0.75, -1, 0.22), 1.55), ("side", (1, 0, 0.05), 1.55), ("front", (0, -1, 0.05), 1.55), ("game", (0.45, -1, 1.25), 2.2))
centre = Vector((0, -0.15, HEIGHT * 0.48))

for b in BODIES:
    if ONLY and b["name"] not in ONLY:
        continue
    holder, align = load_weapon(b["weapon"], b["length"])
    grip = GRIPS[b["shooting"]]
    for role in ("moving", "shooting", "still"):
        action = use(b[role])
        shares = (0.0, 0.25, 0.5, 0.75) if role != "still" else (0.0, 0.5)
        tiles = []
        for view, direction, distance in VIEWS:
            for share in shares:
                at_share(action, share)
                hand = arm.pose.bones["RightHand"].matrix
                holder.matrix_world = placed(hand.to_3x3() @ grip, hand.translation) @ align
                bpy.context.view_layer.update()
                d = Vector(direction).normalized()
                camera.location = centre + d * HEIGHT * distance
                camera.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
                scene.render.filepath = os.path.join(folder, "shots", f"tile_{len(tiles)}.png")
                bpy.ops.render.render(write_still=True)
                tiles.append(scene.render.filepath)
        contact_sheet(tiles, len(shares), os.path.join(folder, "shots", f"{b['name']}_{role}.png"))
    for o in [holder] + list(holder.children_recursive):
        bpy.data.objects.remove(o)
arm.animation_data.action = None
scene.frame_set(1)

if not POSES_ONLY:
    for o in list(scene.objects):
        if o not in (arm, mesh):
            bpy.data.objects.remove(o)
    scene.render.fps, scene.render.fps_base = FPS, 1.0  # Importing a weapon for the pictures set the scene to its file's 24 a second.
    mt._select([arm, mesh])
    bpy.ops.export_scene.fbx(filepath=os.path.join(folder, UNIT + ".fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
                             axis_forward="-Z", axis_up="Y", bake_space_transform=False, apply_scale_options="FBX_SCALE_ALL",
                             mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    with open(os.path.join(folder, "report.json"), "w", encoding="utf-8") as f:
        json.dump({"source": os.path.relpath(SOURCE, mt.ROOT), "height": round(HEIGHT, 3), "meshy_clips": MESHY, "clips": CLIPS,
                   "short_of_targets": SHORTS, "grip_check_degrees": GRIP_CHECK,
                   "bodies": {b["name"]: {"moving": b["moving"], "shooting": b["shooting"], "still": b["still"], "shotMoment": b["shot"]}
                              for b in BODIES}}, f, indent=1)
    print(f"REPORT wrote {UNIT}.fbx with {len(CLIPS)} new clips")
