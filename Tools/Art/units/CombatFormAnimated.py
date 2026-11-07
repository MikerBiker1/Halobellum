# The Combat Form, animated (D92): its static model (CombatForm.py, an infested Elite) rigged here, as Meshy refused it (it found no head), so
# the crowd baker can play it, for the Combat Form and for the Infested Elite, which is the same model holding a plasma rifle (D82).
# It lurches forward hunched, rolling from foot to foot, the Elite's head hanging off its back and lolling, the three whips of its left arm
# trailing and swinging, the infection form's feeler stalks bobbing on its chest; it fires its gun from the right hand, arm out, with a jolt
# each shot; up close it slashes with the right claw and lashes down with the whips in turn, one blow on each strike; it dies dropping to
# its knees and pitching forward onto its face. The Infested Elite walks slower (2 m/s against 2.4) with its rifle carried forward
# (WalkElite) and fires it two-handed in the right (FireElite). Sizes in metres at its height in the game (2.6 m), front toward -Y; its
# right is -X. The model came out of its preparation facing backwards, so it is turned.
#   blender --background --factory-startup --python Tools/Art/units/CombatFormAnimated.py
#   HB_ONLY=bones | weights | clips (stop after that), HB_CLIPS and HB_PICTURES (which clips to make and film), to lay it out.
import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import creature_rig as cr  # noqa: E402

ONLY = os.environ.get("HB_ONLY", "")
FPS = 30
MID = -0.25  # Its middle line: the stalks and whips make the model lopsided.

mesh = cr.load("CombatFormAnimated", "CombatForm", "Meshy/halo_ce_flood_Combat_form.glb", height=2.6, yaw=180, fps=FPS)
cr.double_sided()  # The fronds at the tips of its stalks.

spec = [
    ("Body", (MID, 0.05, 1.05), (MID, 0.05, 1.3), None, 0.25),
    ("Spine", (MID, 0.05, 1.3), (MID, 0.12, 1.6), "Body", 0.3),
    ("Chest", (MID, 0.12, 1.6), (MID, 0.1, 1.95), "Spine", 0.35),
    # The Elite's head hangs off the back of its right shoulder; the infection form's feeler stalks rise from the chest front.
    ("Neck", (MID, 0.12, 1.95), (-0.5, 0.3, 2.1), "Chest", 0.15),
    ("Head", (-0.5, 0.3, 2.1), (-0.6, 0.62, 2.05), "Neck", 0.2),
    ("Stalk_0_0", (-0.05, -0.15, 1.98), (0.08, -0.4, 2.3), "Chest", 0.06),
    ("Stalk_0_1", (0.08, -0.4, 2.3), (0.19, -0.6, 2.55), "Stalk_0_0", 0.06),
    ("Stalk_1_0", (-0.05, -0.15, 1.98), (0.06, -0.47, 2.14), "Chest", 0.06),
    ("Stalk_2_0", (-0.15, -0.15, 1.98), (-0.26, -0.5, 2.22), "Chest", 0.06),
    ("RightArm", (-0.62, 0.15, 1.8), (-0.78, 0.18, 1.35), "Chest", 0.12),
    ("RightForeArm", (-0.78, 0.18, 1.35), (-0.8, 0.15, 0.95), "RightArm", 0.11),
    ("RightHand", (-0.8, 0.15, 0.95), (-0.82, 0.14, 0.72), "RightForeArm", 0.1),
    ("LeftArm", (0.15, 0.15, 1.8), (0.27, 0.17, 1.4), "Chest", 0.12),
    ("LeftForeArm", (0.27, 0.17, 1.4), (0.32, 0.15, 1.0), "LeftArm", 0.11),
    ("LeftHand", (0.32, 0.15, 1.0), (0.35, 0.12, 0.72), "LeftForeArm", 0.1),
    ("Muzzle", (-0.83, -0.05, 0.85), (-0.83, -0.25, 0.85), "RightHand"),
]
# The three whips of its left arm, from the outside of the elbow down almost to the ground (traced from the model's vertices).
WHIPS = [[(0.42, 0.2, 1.3), (0.6, 0.23, 0.92), (0.62, 0.24, 0.6), (0.55, 0.23, 0.19)],
         [(0.45, 0.13, 1.28), (0.63, 0.12, 0.99), (0.67, 0.13, 0.67), (0.62, 0.14, 0.31)],
         [(0.48, 0.17, 1.3), (0.79, 0.17, 0.94), (0.87, 0.17, 0.54), (0.78, 0.17, 0.02)]]
for n, points in enumerate(WHIPS):
    spec += cr.chain(f"Whip_{n}", points, "LeftForeArm", radius=0.07)
for side, x in (("Right", -0.45), ("Left", -0.04)):
    spec += [
        (f"{side}UpLeg", (x, 0.0, 1.15), (x, -0.22, 0.82), "Body", 0.13),
        (f"{side}Leg", (x, -0.22, 0.82), (x, 0.14, 0.36), f"{side}UpLeg", 0.11),
        (f"{side}Foot", (x, 0.14, 0.36), (x, -0.15, 0.02), f"{side}Leg", 0.13),
    ]
arm = cr.skeleton(spec)
if ONLY in ("bones", ""):
    cr.bone_picture("bones")
if ONLY == "bones":
    sys.exit(0)

print("REPORT vertices per bone:", cr.weigh(blend=0.3, smooth=3, whole_below=20))
for name, direction in (("weights_front", (0.3, -1, 0.2)), ("weights_back", (-0.6, 1, 0.3)), ("weights_side", (1, 0, 0.1))):
    cr.weight_picture(name, direction, (MID, 0, 1.3), 6.5)
if ONLY == "weights":
    sys.exit(0)

# ---- Clips ----
for side in ("Right", "Left"):
    cr.leg(side, f"{side}UpLeg", f"{side}Leg", f"{side}Foot", pole=(0, -1, 0))  # An Elite's knees bend forward, its ankles back.

CROUCH = 0.08  # Hunched a little lower than its model.
CONTACT = {"Right": Vector((-0.45, -0.1, 0.02)), "Left": Vector((-0.04, -0.1, 0.02))}
CYCLE = 0.8  # Seconds per stride.
DUTY = 0.5  # Each foot down half the stride: a lurching run.
SPEEDS = {"Walk": 2.4, "WalkElite": 2.0}  # The Combat Form's move speed and the Infested Elite's.
CLAW_SECONDS = 0.8  # The Claw Swipe's interval; the Elite's Scythe Claw (0.7 s) plays the same clip a little faster, as the game keeps time.
GUN_SECONDS = {"Fire": 0.4, "FireElite": 0.5}  # The Scavenged Rifle's interval and the plasma rifle's: three shots to a clip.


def add(pose, bone, x=0.0, y=0.0, z=0.0):
    old = pose["turn"].get(bone, (0, 0, 0))
    pose["turn"][bone] = (old[0] + x, old[1] + y, old[2] + z)


def stance():
    """Hunched forward, the head hanging, arms loose, the whips trailing a little behind."""
    pose = {"move": (0, 0, -CROUCH), "turn": {}, "feet": {}, "foot_turn": {}}
    add(pose, "Spine", 8)
    add(pose, "Chest", 6)
    add(pose, "RightArm", -6, 0, -4)
    add(pose, "RightForeArm", -14)
    add(pose, "LeftArm", -4, 0, 4)
    add(pose, "LeftForeArm", -10)
    for name, contact in CONTACT.items():
        pose["feet"][name] = cr.target_for(name, contact)
        pose["foot_turn"][name] = (0, 0, 0)
    return pose


def whips(pose, t, swing=0.0, lash=0.0, size=1.0):
    """The three whips, each on its own rhythm, a wave running down; swing throws them back (forward if negative), lash forward and up."""
    for n in range(3):
        for i in range(3):
            lag = 0.12 * i + 0.21 * n
            sway = size * (6 + 5 * i) * cr.wave(t * (1 + n % 2) - lag)
            add(pose, f"Whip_{n}_{i}", 14 * swing * (0.6 + 0.4 * i) - lash * (35 + 15 * i) + sway, 0, size * (4 + 3 * i) * cr.wave(t * 2 - lag - 0.1))


def stalks(pose, t, bob=1.0):
    for name, k in (("Stalk_0_0", 1.0), ("Stalk_0_1", 1.4), ("Stalk_1_0", 0.8), ("Stalk_2_0", 0.9)):
        add(pose, name, bob * 8 * k * cr.wave(t * 2 - k * 0.2), 0, bob * 6 * k * cr.wave(t - k * 0.3))


def head_loll(pose, t, size=1.0):
    add(pose, "Neck", size * 5 * cr.wave(t * 2 - 0.25), size * 6 * cr.wave(t - 0.3), 0)
    add(pose, "Head", size * 8 * cr.wave(t * 2 - 0.35), size * 10 * cr.wave(t - 0.4), size * 5 * cr.wave(t - 0.2))


def walk_at(speed, elite):
    def walk(t):
        pose = stance()
        w = cr.wave
        # It lurches from foot to foot, rolling onto each, dipping as it lands, its spine twisting against its legs.
        pose["move"] = (0.07 * w(t + 0.25), 0, -CROUCH - 0.05 * math.cos(4 * math.pi * (t - 0.12)))
        add(pose, "Body", 3 * math.cos(4 * math.pi * (t - 0.1)), 7 * w(t + 0.25), 6 * w(t))
        add(pose, "Spine", 2, -3 * w(t + 0.2), -5 * w(t - 0.05))
        add(pose, "Chest", 2 * math.cos(4 * math.pi * (t - 0.2)), -3 * w(t + 0.1), -6 * w(t - 0.1))
        head_loll(pose, t, 1.3)
        stalks(pose, t)
        whips(pose, t, swing=0.8 + 0.4 * w(t - 0.1))
        if elite:
            # The plasma rifle carried forward in the right hand, bobbing with the stride.
            add(pose, "RightArm", -28 + 4 * w(t), 0, 6)
            add(pose, "RightForeArm", -58 + 5 * w(t - 0.1))
        else:
            add(pose, "RightArm", -26 * w(t), 0, -5 * w(t))
            add(pose, "RightForeArm", -18 - 14 * w(t - 0.08))
        add(pose, "LeftArm", 22 * w(t), 0, 6 * w(t))
        add(pose, "LeftForeArm", -10 - 10 * w(t - 0.1))
        stride = speed * CYCLE * DUTY
        for name, contact in CONTACT.items():
            phase = 0.0 if name == "Right" else 0.5
            offset, planted = cr.step(t, phase, DUTY, stride, 0.22)
            u = ((t - phase) % 1.0 - DUTY) / (1 - DUTY)
            turn = (0, 0, 0) if planted else (-25 * math.sin(math.pi * u), 0, 0)
            pose["feet"][name] = cr.target_for(name, contact + offset, turn)
            pose["foot_turn"][name] = turn
        return pose
    return walk


def idle(t):
    pose = stance()
    w = cr.wave
    pose["move"] = (0, 0, -CROUCH - 0.02 * w(t))
    add(pose, "Chest", 2 * w(t), 0, 2 * w(t * 0.5))
    head_loll(pose, t, 0.8)
    stalks(pose, t, 0.8)
    whips(pose, t, size=0.8)
    twitch = math.exp(-((t - 0.6) % 1.0) * 30) * math.sin(((t - 0.6) % 1.0) * 90)
    add(pose, "RightHand", 25 * twitch)
    add(pose, "RightForeArm", 4 * w(t + 0.3))
    return pose


SHOT_MOMENT = 0.225  # The first shot of a firing clip and the first blow of the swipe: a body has one shot moment for both clips.


def kick(t, shots):
    """1 at each shot (the first at SHOT_MOMENT, then evenly), falling away fast."""
    u = (t * shots - SHOT_MOMENT * shots) % 1.0
    return math.exp(-u * 9)


def fire_with(elite, shots=3):
    def fire(t):
        pose = stance()
        k = kick(t, shots)
        # The right arm held out at the target, the body braced behind it, a jolt back with each shot.
        pose["move"] = (0, 0.03 * k, -CROUCH - 0.03)
        add(pose, "Spine", 3 - 3 * k)
        add(pose, "Chest", -4 - 4 * k, 0, 8)
        add(pose, "RightArm", -78 + 10 * k, 0, 10)
        add(pose, "RightForeArm", -8 - (0 if elite else 6) + 10 * k)
        add(pose, "RightHand", 0 if elite else -20)
        add(pose, "LeftArm", -12, 0, 8)
        head_loll(pose, t * 3, 0.6 + 0.6 * k)
        stalks(pose, t * 3, 0.8)
        whips(pose, t * 3, swing=0.3)
        return pose
    return fire


def blow(u):
    """One blow, u from 0 to 1: drawn back (to -1), struck in a few frames (1 at u = 0.45), and back."""
    if u < 0.3:
        return -cr.ease(u / 0.3)
    if u < 0.45:
        return -1 + 2 * ((u - 0.3) / 0.15) ** 0.7
    return 1 - cr.ease((u - 0.45) / 0.55)


def swipe(t):
    """Two blows to a clip, one on each strike: the right claw slashes across, then the whips of the left arm lash down."""
    pose = stance()
    half = 0 if t < 0.5 else 1
    b = blow((t * 2) % 1.0)
    back, hit = max(-b, 0), max(b, 0)
    pose["move"] = (0, -0.18 * hit + 0.05 * back, -CROUCH - 0.06 * hit)
    if half == 0:
        add(pose, "Spine", 4 * hit, 0, 18 * back - 22 * hit)
        add(pose, "Chest", 6 * hit, 0, 12 * back - 16 * hit)
        add(pose, "RightArm", 40 * back - 85 * hit, 0, -30 * back + 40 * hit)
        add(pose, "RightForeArm", -30 * back - 10 * hit)
        add(pose, "RightHand", -20 * hit)
        whips(pose, t, swing=0.6)
    else:
        add(pose, "Spine", 6 * hit, 0, -14 * back + 16 * hit)
        add(pose, "Chest", 8 * hit, 0, -10 * back + 12 * hit)
        add(pose, "LeftArm", -95 * back - 30 * hit, 0, 20 * back - 25 * hit)
        add(pose, "LeftForeArm", -30 * back + 10 * hit)
        whips(pose, t, swing=-0.8 * back, lash=-1.2 * back + 1.6 * hit)
        add(pose, "RightArm", 15 * hit, 0, 0)
    head_loll(pose, t * 2, 0.6 + 0.8 * hit)
    stalks(pose, t * 2)
    return pose


def fall(t):
    """Its legs give, it drops to its knees, and pitches forward onto its face, the whips and head flopping."""
    knees = cr.ease(t / 0.4)
    pitch = cr.ease((t - 0.3) / 0.5)
    pose = stance()
    pose["move"] = (0.05 * pitch, -0.35 * pitch, -CROUCH - 0.55 * knees - 0.45 * pitch)
    add(pose, "Body", 70 * pitch, 8 * pitch, 6 * pitch)
    add(pose, "Spine", 10 * knees, 0, 0)
    add(pose, "Chest", 10 * pitch, 0, 0)
    add(pose, "Neck", 20 * pitch, 15 * knees, 0)
    add(pose, "Head", 25 * knees, 10 * pitch, 15 * pitch)
    add(pose, "RightArm", -40 * pitch, 0, -20 * pitch)
    add(pose, "RightForeArm", -30 * pitch)
    add(pose, "LeftArm", -30 * pitch, 0, 25 * pitch)
    whips(pose, t, swing=1.2 * pitch, size=1 - knees)
    stalks(pose, t, 1 - pitch)
    for name, contact in CONTACT.items():
        pose["feet"][name] = cr.target_for(name, contact + Vector((0, 0.2 * knees, 0)), (-40 * knees, 0, 0))
        pose["foot_turn"][name] = (-40 * knees, 0, 0)
    return pose


CLIPS = os.environ.get("HB_CLIPS", "Walk,WalkElite,Idle,Fire,FireElite,Swipe,Dead").split(",")
for name, speed in SPEEDS.items():
    if name in CLIPS:
        cr.clip(name, round(CYCLE * FPS), walk_at(speed, name == "WalkElite"))
if "Idle" in CLIPS:
    cr.clip("Idle", 2 * FPS, idle)
for name, seconds in GUN_SECONDS.items():
    if name in CLIPS:
        cr.clip(name, round(3 * seconds * FPS), fire_with(name == "FireElite"))
if "Swipe" in CLIPS:
    cr.clip("Swipe", round(2 * CLAW_SECONDS * FPS), swipe)
if "Dead" in CLIPS:
    cr.clip("Dead", round(1.6 * FPS), fall, floor=True)
slides = {name: cr.walk_slide(name, list(CONTACT), speed, floor=0.02) for name, speed in SPEEDS.items() if name in CLIPS}

SIDE, QUARTER = (1, 0.05, 0.1), (0.8, -1, 0.3)
CENTRE = (MID, -0.1, 1.2)
for clip_name, count in (("Walk", 8), ("WalkElite", 8), ("Fire", 6), ("FireElite", 6), ("Swipe", 8), ("Dead", 8), ("Idle", 4)):
    if clip_name not in CLIPS or clip_name not in os.environ.get("HB_PICTURES", "Walk,Fire,Swipe,Dead").split(","):
        continue
    shares = [i / (count - 1) for i in range(count)] if clip_name == "Dead" else [i / count for i in range(count)]
    for view_name, view in (("side", SIDE), ("quarter", QUARTER)):
        cr.strip(cr.film(clip_name, f"{clip_name}_{view_name}", view, CENTRE, 6.5, shares=shares, size=(360, 400)), f"strip_{clip_name}_{view_name}", columns=4)
if ONLY:
    sys.exit(0)
cr.finish({"moving": "Walk", "moving_elite": "WalkElite", "shooting": "Fire", "shooting_elite": "FireElite", "melee": "Swipe", "dead": "Dead", "still": "Idle"},
          {"speeds": SPEEDS, "walk_slide": slides, "shot_moment": SHOT_MOMENT})
