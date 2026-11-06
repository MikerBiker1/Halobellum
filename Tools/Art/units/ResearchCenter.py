# The Research Center, from the owner's Meshy model: a big striped sphere ringed with antennas beside a lower dome, on a round base with a
# door at -Y. 13,567 triangles to about 6,000 and one texture. Its cyan lights are taken out of the texture into a mask (colour_mask), which
# the game lights in the colour of the faction that owns the tower (TowerArt). The rest of the texture is left as it was.
# Numbers are the file's own sizes.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

TRIANGLES = 6000
BAKED = 2048
TEXTURE = 1024
CYAN = 190  # The lights' hue, in degrees.

mt.start("ResearchCenter", "Meshy/Meshy_AI_Research center.glb")
mt.source_size()
mt.centre_on_ground()
image = mt.reduce_and_repaint(TRIANGLES, BAKED, reach=0.01)
mt.fill_gaps(image)
mt.colour_mask(image, CYAN, 40, 0.1, 0.3)
mt.smooth(40)
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=TEXTURE, glow_size=TEXTURE)
mt.look("after_back", "back")
for camera in ("quarter", "top"):
    mt.look("far_" + camera, camera, size=(160, 120), samples=32)
