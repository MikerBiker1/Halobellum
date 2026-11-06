# The beam rifle, from the owner's halo_-_beam_rifle.glb, for the Jackal that carries it: a long sniper rifle, 5,702 triangles down to about 400,
# with a colour per face instead of a texture. It already faces -Y, upright, its forked muzzle in front. A long thin weapon loses its prongs to
# solid(), so it is reduced as it is. The hand goes through the loop behind the round part. Numbers are in a model 10 units long, after fit().
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
from mathutils import Vector

mt.start("Weapon_BeamRifle", "Meshy/halo_-_beam_rifle.glb")
mt.fit()
mt.report("fitted")
low, high = mt._bounds()
front = [o.matrix_world @ v.co for o in mt.meshes() for v in o.data.vertices if (o.matrix_world @ v.co).y < low.y + 0.15]
tip = Vector((0.0, low.y, sum(p.z for p in front) / len(front)))
print(f"REPORT tip {mt._round(tip)}")
mt.snapshot()
mt.weld(0.002)
mt.reduce(400, flat_angle=2.0)
mt.smooth(30)
mt.report("reduced")
mt.finish_weapon(grip=(0, 2.05, 1.7), tip=tip, length_note=1.5)
mt.look("after_back", "back")
mt.look("after_front", "front")
