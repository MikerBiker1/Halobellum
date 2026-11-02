# The Tank Pure Form: a huge melee form with one great club of an arm. A static model that the game wobbles; every triangle kept.
# It came facing -Y: the tusks and the fronds are on that side.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("PureFormTank", "sketchfab.com/enemieshalo_3the_floodpure_tank.glb")
mt.fit()
mt.report("fitted")
mt.weld(0.002, "flood_tank_0")  # The file splits its vertices at texture seams: without this, smoothing leaves a crease along each seam.
mt.smooth(60, "flood_tank_0")
mt.flag("flood_fronds", cutout=True)
mt.motion("Wobble")
mt.merge_rest()
mt.finish()
mt.look("after_back", "back")
# The usual frame crops anything taller than it is long, so the whole creature is shown again in portrait.
for camera in ("quarter", "side", "front", "back"):
    mt.look("tall_" + camera, camera, size=(420, 560))
