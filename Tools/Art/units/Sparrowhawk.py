# The Sparrowhawk: 29,085 triangles in 59 parts with 40 materials, to about 8,000 and one texture. Its nose is at -Y. The file
# stands off the origin, so positions below are measured from its middle. Its two chin cannons fire in turn.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("Sparrowhawk", "sketchfab.com/sparrowhawk_-_halo.glb")
mt.source_size()
mt.centre_on_ground()
low, high = mt._bounds()
mt.reduce(8000, flat_angle=0)
mt.smooth(40)
mt.bake_one(1024)
# The cannons' tips, in the file at x -21 and -33.7, y -7.1, z 5.5, with its middle at x -27.2, y 4.6 and floor at z 1.2.
mt.muzzle("Muzzle_0a", (6.2, -11.7, 4.3))
mt.muzzle("Muzzle_0b", (-6.5, -11.7, 4.3))
mt.motion("Hover")
mt.report("done")
mt.finish()
