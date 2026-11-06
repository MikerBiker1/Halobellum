# The plasma rifle, from the owner's halo_-_plasma_rifle.glb, for the Elite: 13,522 triangles down to about 300, with a colour per face instead of
# a texture. It already faces -Y, upright: two jaws one above the other, joined by the grip at the back of the gap between them, where the hand goes.
# Its shots start between the jaws' front ends. Numbers are in a model 10 units long, after fit().
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
from mathutils import Vector

mt.start("Weapon_PlasmaRifle", "Meshy/halo_-_plasma_rifle.glb")
mt.fit()
mt.report("fitted")
low, high = mt._bounds()
front = [o.matrix_world @ v.co for o in mt.meshes() for v in o.data.vertices if (o.matrix_world @ v.co).y < low.y + 0.6]
tip = Vector((0.0, low.y, (min(p.z for p in front) + max(p.z for p in front)) / 2))  # Halfway between the jaws' tips.
print(f"REPORT tip {mt._round(tip)}")
mt.snapshot()
mt.solid(300, voxel=0.05)
mt.smooth(30)
mt.remove_small_islands(10)
mt.report("solid")
mt.finish_weapon(grip=(0, 0.3, 3.1), tip=tip, length_note=0.85)
mt.look("after_back", "back")
mt.look("after_front", "front")
