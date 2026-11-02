# The SMG, for later: 16,012 triangles down to about 250, with a colour per face instead of a texture.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("Weapon_SMG", "sketchfab.com/halo_3_smg.glb")
mt.turn(yaw=-90)  # It came with its barrel along +X.
mt.fit()
mt.remove_box((0.33, -2.4, 2.4), (0.7, 2.0, 3.4))  # A thin rail standing off one side; reduction turns it into a blade.
mt.snapshot()
mt.solid(250, voxel=0.03)
mt.smooth(30)
mt.remove_small_islands(10)  # Two tiny slivers left over from the cut.
mt.report("solid")
mt.finish_weapon(grip=(-0.03, -0.25, 1.38), tip=(-0.03, -5.0, 2.98), length_note=0.65)
mt.look("after_back", "back")
mt.look("after_front", "front")
