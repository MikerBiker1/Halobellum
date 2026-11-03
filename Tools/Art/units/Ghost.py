# The Ghost, from the owner's Meshy model: 856,188 triangles to 6,000, with its colours painted onto the new surface. Its nose is along -X in the file. It hovers, and
# its two guns fire in turn. Numbers are the file's own sizes, after the turn.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("Ghost", "Meshy/Meshy_AI_Ghost.glb")
mt.source_size()
mt.turn(yaw=90)
mt.reduce_and_repaint(6000, 1024, reach=0.015)
mt.smooth(50)
mt.muzzle("Muzzle_0a", (0.3, -0.93, 0.0))
mt.muzzle("Muzzle_0b", (-0.3, -0.93, 0.0))
mt.motion("Hover")
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=1024)
