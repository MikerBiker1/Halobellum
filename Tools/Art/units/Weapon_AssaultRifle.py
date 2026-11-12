# The MA5B assault rifle of the Marines, the Infested Marines and the Combat Forms' scavenged rifles (D131): the owner's
# Meshy/ma5b_remaster.glb, in place of the Halo CE rifle from Sketchfab (D41). 36,400 triangles down to about 900, with a colour per face
# instead of a texture: the Marines come 18 to a squad. It lies along X, its muzzle at -X, so it is turned to face -Y. The hand takes the
# pistol grip under the back of the body (the frame round the trigger); its shots leave the end of the barrel.
#   blender --background --factory-startup --python Tools/Art/units/Weapon_AssaultRifle.py
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


mt.start("Weapon_AssaultRifle", "Meshy/ma5b_remaster.glb")
p = np.array([o.matrix_world @ v.co for o in mt.meshes() for v in o.data.vertices])
grip_file = middle_of(p[(p[:, 0] > 1.5) & (p[:, 0] < 2.6) & (p[:, 2] < -0.8) & (p[:, 2] > -1.6)], "grip")  # The handle's middle.
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
mt.snapshot()
mt.solid(900, voxel=0.035)  # Its seven parts overlap: reduce tore them apart, so it is rebuilt as one closed skin first.
mt.smooth(30)
mt.report("reduced")
mt.finish_weapon(grip=grip, tip=tip, length_note=0.8)
mt.look("after_front", "front")
