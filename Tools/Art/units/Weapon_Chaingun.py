# The Spartan Heavy Weapons' M247H machine gun (D132): the owner's Meshy/Meshy_AI_UNSC_machinegun.glb, the Halo turret gun with its shield
# and its mount, kept whole as the owner asked (not one triangle removed: 10,421, a colour per face instead of a texture). It lies along X,
# its barrel at -X and its two spade handles at +X, so it is turned to face -Y. The hand takes the middle of the two handles at the back;
# its shots leave the end of the barrel.
#   blender --background --factory-startup --python Tools/Art/units/Weapon_Chaingun.py
import os
import sys

import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt  # noqa: E402


def middle_of(points, label):
    if len(points) == 0:
        raise ValueError("Nothing found for the " + label)
    m = points.mean(axis=0)
    print(f"REPORT {label} from {len(points)} vertices: {tuple(round(float(v), 3) for v in m)}")
    return m


mt.start("Weapon_Chaingun", "Meshy/Meshy_AI_UNSC_machinegun.glb")
p = np.array([o.matrix_world @ v.co for o in mt.meshes() for v in o.data.vertices])
grip_file = middle_of(p[p[:, 0] > p[:, 0].max() - 0.8], "grip (the handles)")
grip_file[1] = 0.0  # Between the two.
tip_file = middle_of(p[p[:, 0] < p[:, 0].min() + 0.2], "muzzle")

# Turned to face -Y (yaw 90: what was at -X goes to -Y), then fit(): the two points read above go the same way.
before_low, before_high = mt._bounds()
mt.turn(yaw=90)
mt.fit()
after_low, after_high = mt._bounds()
s = (after_high.y - after_low.y) / (before_high.x - before_low.x)
turned_low = Vector((-before_high.y, before_low.x, before_low.z))


def moved(q):
    x, y, z = (float(v) for v in q)
    return (Vector((-y, x, z)) - turned_low) * s + after_low


grip, tip = moved(grip_file), moved(tip_file)
mt.report("fitted")
mt.finish_weapon(grip=grip, tip=tip, length_note=1.4)
mt.look("after_front", "front")
