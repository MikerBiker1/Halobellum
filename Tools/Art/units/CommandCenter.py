# The Command Center, from the owner's Meshy model: a domed hall with a radar dish, a side tower with a mast, and a door at -Y. 15,686
# triangles to about 6,000 and one texture. Its red trim is taken out of the texture into a mask (colour_mask), which the game paints in the
# colour of the faction that owns the tower (TowerArt). The rest of the texture is left as it was. Numbers are the file's own sizes.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

TRIANGLES = 6000
BAKED = 2048
TEXTURE = 1024
RED = 0  # The trim's hue, in degrees.

mt.start("CommandCenter", "Meshy/Meshy_AI_command_center.glb")
mt.source_size()
mt.centre_on_ground()
image = mt.reduce_and_repaint(TRIANGLES, BAKED, reach=0.01)
mt.fill_gaps(image)
mt.colour_mask(image, RED, 22, 0.45, 0.12)
mt.smooth(40)
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=TEXTURE, glow_size=TEXTURE)
mt.look("after_back", "back")
for camera in ("quarter", "top"):
    mt.look("far_" + camera, camera, size=(160, 120), samples=32)
