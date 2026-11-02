# The Stalker Form: a low crouching melee form with a sack for a tail. A static model that the game wobbles; every triangle kept.
# It came facing -Y: the tusks and the fronds under them are on that side, the sack is behind.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("StalkerForm", "sketchfab.com/enemieshalo_3the_floodpure_stalker.glb")
mt.fit()
mt.report("fitted")
mt.weld(0.002, "flood_stalker_0")  # The file splits its vertices at texture seams: without this, smoothing leaves a crease along each seam.
mt.smooth(60, "flood_stalker_0")
mt.flag("flood_fronds", cutout=True)
mt.motion("Wobble")
mt.merge_rest()
mt.finish()
mt.look("after_back", "back")
