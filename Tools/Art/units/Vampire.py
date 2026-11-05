# The Vampire, from the owner's Meshy model: 15,951 triangles to about 8,800 and one texture of 512. Its surface is split along its
# texture's seams into 802 pieces, which welding joins into 3, so it is reduced whole and repainted. Its nose is along -X in the file:
# the canopy and the open mouth between its two claws are there, and its crests and fin sweep back toward +X. Its crescent wings are
# one solid body with the hull, so nothing beats, and it hovers. The Needle Cannons fire in turn from the front of the glowing strips
# inside its claws. Numbers below are the file's own sizes, after the turn.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import numpy as np

mt.start("Vampire", "Meshy/Meshy_AI_Vampire.glb")
mt.source_size()
mt.turn(yaw=90)
mt.centre_on_ground()
image = mt.reduce_and_repaint(8800, 1024, reach=0.008)
# Its unwrap is in many small pieces with black between them, and from afar black seeps into them. So the unused parts of the texture
# take the model's average colour instead.
pixels = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
image.pixels.foreach_get(pixels)
pixels = pixels.reshape(-1, 4)
empty = pixels[:, :3].sum(axis=1) < 0.001
pixels[empty, :3] = pixels[~empty, :3].mean(axis=0)
image.pixels.foreach_set(pixels.ravel())
image.update()
image.pack()
print(f"REPORT filled {int(empty.sum())} of {len(pixels)} texture pixels")
mt.smooth(40)
mt.muzzle("Muzzle_0a", (0.125, -0.42, 0.228))
mt.muzzle("Muzzle_0b", (-0.125, -0.42, 0.228))
mt.motion("Hover")
mt.report("done")
mt.finish(texture_size=512)
mt.look("after_back", "back")
