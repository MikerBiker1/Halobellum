# The Spartan Fireteam's battle rifle (D131): the owner's Meshy/br55_-_halo_battle_rifle.glb, in place of the assault rifle it borrowed.
# 21,160 triangles down to about 1,300, with a colour per face instead of a texture. It already lies along Y, its muzzle at -Y. The hand takes
# the pistol grip behind the trigger, in front of the magazine; its shots leave the end of the barrel.
#   blender --background --factory-startup --python Tools/Art/units/Weapon_BattleRifle.py
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


mt.start("Weapon_BattleRifle", "Meshy/br55_-_halo_battle_rifle.glb")
p = np.array([o.matrix_world @ v.co for o in mt.meshes() for v in o.data.vertices])
grip_file = middle_of(p[(p[:, 1] > 1.0) & (p[:, 1] < 2.0) & (p[:, 2] < -1.0)], "grip")
tip_file = middle_of(p[p[:, 1] < p[:, 1].min() + 0.2], "muzzle")

# fit() only scales and stands it on the origin: the two points read above go the same way.
before_low, before_high = mt._bounds()
mt.fit()
after_low, after_high = mt._bounds()
s = (after_high.y - after_low.y) / (before_high.y - before_low.y)


def moved(q):
    return (Vector([float(v) for v in q]) - before_low) * s + after_low


grip, tip = moved(grip_file), moved(tip_file)
mt.report("fitted")
mt.snapshot()
mt.weld(0.002)
mt.reduce(1300, flat_angle=2.0)
mt.smooth(30)
mt.report("reduced")
mt.finish_weapon(grip=grip, tip=tip, length_note=0.95)
mt.look("after_front", "front")
