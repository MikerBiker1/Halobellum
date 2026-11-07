# The shotgun, from the owner's sketchfab.com/weaponshumanshotgun.glb, for the Spartan CQB (D86). The file holds nine in a row; the owner asked
# for the third, the darkest: black with red marks (Object_2 with its light strip and shell port). 2,249 triangles down to about 900, with a colour
# per face instead of a texture. It lies along X with its muzzle at +X, so it is turned to face -Y. The hand takes the pistol grip under the stock's
# front; its shots leave the end of the upper barrel.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import numpy as np
from mathutils import Vector


def mapper(yaw):
    """Turns a point read in the file's own units into where it lands after turn(yaw) and fit(), for a weapon lying along X."""
    low, high = mt._bounds()
    s = 10.0 / (high.x - low.x)
    cx, cy = (low.x + high.x) / 2, (low.y + high.y) / 2
    if yaw < 0:
        return lambda x, y, z: Vector((s * (y - cy), s * (cx - x), s * (z - low.z)))
    return lambda x, y, z: Vector((s * (cy - y), s * (x - cx), s * (z - low.z)))


def middle_of(points, label):
    if len(points) == 0:
        raise ValueError("Nothing found for the " + label)
    m = points.mean(axis=0)
    print(f"REPORT {label} from {len(points)} vertices: {tuple(round(float(v), 3) for v in m)}")
    return m


def vertices():
    return np.array([o.matrix_world @ v.co for o in mt.meshes() for v in o.data.vertices])

mt.start("Weapon_Shotgun", "sketchfab.com/weaponshumanshotgun.glb")
mt.source_size()
mt.keep_box(Vector((-100, 1.4, -100)), Vector((100, 2.3, 100)))  # The third, the darkest.
at = mapper(-90)
p = vertices()
grip = middle_of(p[(p[:, 0] > -0.53) & (p[:, 0] < -0.37) & (p[:, 2] < -0.06)], "grip")
end = p[:, 0].max()
tip = middle_of(p[p[:, 0] > end - 0.02], "muzzle")
mt.turn(yaw=-90)
mt.fit()
mt.report("fitted")
grip, tip = at(*grip), at(*tip)
mt.snapshot()
mt.weld(0.002)
mt.reduce(900, flat_angle=2.0)
mt.smooth(30)
mt.report("reduced")
mt.finish_weapon(grip=grip, tip=tip, length_note=1.0)
mt.look("after_back", "back")
mt.look("after_front", "front")
