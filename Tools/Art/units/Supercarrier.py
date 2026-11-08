# The Covenant supercarrier, from the owner's Meshy model (Meshy_AI_covenant_supercarrier.glb, D104): the warship far off over the main
# menu's hills, glassing the land beyond them. It is only ever seen as a distant shape against the sky, so it is cut down hard, to about
# 12,000 triangles, and painted afresh from its own colours on one texture. Its belly, where the glassing beam leaves, is marked Muzzle_0.
#   blender --background --factory-startup --python Tools/Art/units/Supercarrier.py
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("Supercarrier", "Meshy/Meshy_AI_covenant_supercarrier.glb")
mt.source_size()
mt.report("as made")
mt.look("before", "quarter")
mt.centre_on_ground()
mt.reduce_and_repaint(12000, 1024, weld_distance=0.001, reach=0.05)
mt.smooth(40)
low, high = mt._bounds()
# The beam leaves the middle of its underside.
mt.muzzle("Muzzle_0", ((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
mt.report("done")
mt.finish(texture_size=1024)
