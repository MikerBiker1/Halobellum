# The Longsword: 4,198 triangles, kept as they are, and one texture. Bombs leave its middle, under the hull.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("Longsword", "sketchfab.com/longsword_precursors_legacy.glb")
mt.source_size()
mt.centre_on_ground()
mt.smooth(40)
mt.bake_one(1024)
mt.muzzle("Muzzle_0", (0, 0, 1))
mt.motion("Hover")
mt.report("done")
mt.finish()
