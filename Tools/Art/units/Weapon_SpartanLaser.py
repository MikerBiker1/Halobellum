# The Spartan Laser, from the owner's weaponshumanspartan_laser.glb, for the Spartan (D85). The file holds five lasers side by side; the owner
# asked for the second, the green one with the small blue screen that faces the shooter (Object_5, with its lights and screen, Object_7 to 9).
# 11,767 triangles down to about 1,300 (the Spartan is one body, so it can afford them), with a colour per face instead of a texture. It lies along +X, its lens at +X, so it is turned to face -Y.
# The right hand takes the pistol grip under the middle, behind the trigger guard; the left hand goes to the front grip. Its shots start at the
# lens, the front of the round emitter under the top housing. Numbers are in a model 10 units long, after fit().
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import numpy as np
from mathutils import Vector

mt.start("Weapon_SpartanLaser", "Meshy/weaponshumanspartan_laser.glb")
mt.remove(r"^Object_(2|3|4|6)$")  # The other four lasers.
mt.turn(yaw=-90)
mt.fit()
mt.report("fitted")

# The lens: the frontmost point of the emitter, which sits at about two thirds of the height, under the top housing.
points = np.array([o.matrix_world @ v.co for o in mt.meshes("^Object_5$") for v in o.data.vertices])
band = points[(points[:, 2] > 1.75) & (points[:, 2] < 2.35) & (np.abs(points[:, 0] + 0.17) < 0.35)]
front = band[:, 1].min()
lens = band[band[:, 1] < front + 0.08]
tip = Vector((float(lens[:, 0].mean()), float(front), float(lens[:, 2].mean())))
print(f"REPORT tip {mt._round(tip)} from {len(lens)} vertices")

mt.snapshot()
# Made of many small panels, it turns to shards under reduce(): rebuilt as one closed skin first, as the plasma rifle is.
mt.solid(int(os.environ.get("HB_LASER_TRIS", "1400")), voxel=float(os.environ.get("HB_LASER_VOXEL", "0.025")))
mt.smooth(30)
mt.remove_small_islands(10)
mt.report("solid")
mt.finish_weapon(grip=(-0.17, 0.15, 0.75), tip=tip, length_note=1.4)
mt.look("after_back", "back")
mt.look("after_front", "front")
