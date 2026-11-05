# The Combat Form: a Flood-infested Elite, from the owner's file (halo_ce_flood_Combat_form.glb). Meshy refused to rig it (pose estimation
# failed, it has no head to find), so it is a static model that the game wobbles, as the Stalker and Pure Forms are; every triangle kept.
# It came facing -X and with unlit materials. Its scale in the file is 78 units tall; Unity sets its final size.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("CombatForm", "Meshy/halo_ce_flood_Combat_form.glb")
mt.turn(yaw=90)
mt.plain_materials()
mt.fit()
mt.report("fitted")
mt.weld(0.002, "skin")  # The file splits its vertices at texture seams: without this, smoothing leaves a crease along each seam.
mt.smooth(60, "skin")
mt.flag("flood_fronds", cutout=True)
mt.motion("Wobble")
mt.muzzle("Muzzle_1", (4.7, -0.9, 4.8))  # Its Scavenged Rifle, from the claw on its +X side. The Claw Swipe needs no muzzle.
mt.merge_rest()
mt.finish()
# The usual frame crops anything taller than it is long, so the whole creature is shown again in portrait.
for camera in ("quarter", "side", "front", "back"):
    mt.look("tall_" + camera, camera, size=(420, 560))
