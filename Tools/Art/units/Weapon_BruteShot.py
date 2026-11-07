# The Brute Shot, from the owner's sketchfab.com/weaponscovenantbrute_shot.glb, for the Brute Grenadier (D86). The file holds two; the owner asked
# for the first (Object_3, "brute_shot"). 2,206 triangles down to about 840, with a colour per face instead of a texture. It lies along X with its
# curved blade and muzzle at -X, so it is turned to face -Y. The hand takes the slanted grip under the back; its grenades leave the front of the
# housing above the blade, where its round drum turns.
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

mt.start("Weapon_BruteShot", "sketchfab.com/weaponscovenantbrute_shot.glb")
mt.source_size()
mt.keep_box(Vector((-100, -0.3, -100)), Vector((100, 0.3, 100)))  # The first of the two.
at = mapper(90)
p = vertices()
grip = middle_of(p[(p[:, 0] > 0.28) & (p[:, 0] < 0.46) & (p[:, 2] < -0.05)], "grip")
housing = p[(p[:, 2] > 0.12) & (p[:, 2] < 0.3) & (p[:, 0] < 0.0)]
front = housing[:, 0].min()
tip = middle_of(housing[housing[:, 0] < front + 0.03], "muzzle")
mt.turn(yaw=90)
mt.fit()
mt.report("fitted")
grip, tip = at(*grip), at(front, 0.0, tip[2])
mt.snapshot()
mt.weld(0.002)
mt.reduce(840, flat_angle=2.0)  # The Brute is already near the 7,500 triangles a soldier may have.
mt.smooth(30)
mt.report("reduced")
mt.finish_weapon(grip=grip, tip=tip, length_note=1.4)
mt.look("after_back", "back")
mt.look("after_front", "front")
