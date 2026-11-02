# The Wraith: a hull with its two guns as separate parts, so that both turn toward the target. Its one weapon is the
# mortar on top; the small gun at the nose just follows it.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("Wraith", "sketchfab.com/halo_fangame_vehicle_wraith.glb")
mt.turn(yaw=-90)  # It came with its nose along +X.
mt.fit()
mt.report("turned")
mt.reduce(6000, "wraith_wraith_material")
mt.reduce(1200, "main_gun")
mt.reduce(700, "small_gun")
mt.smooth(40)
gun = mt.part("Turret_0", "main_gun")
small = mt.part("Turret_0b", "small_gun")
low, high = mt._bounds([gun])
mt.muzzle("Muzzle_0", ((low.x + high.x) / 2, low.y, (low.z + high.z) / 2 + 0.1), parent="Turret_0")
mt.motion("Hover")
mt.merge_rest()
mt.finish()
