# The Vulture: 25,224 triangles to about 9,000 and one texture. The rooms inside and the pilot's screen go, since nobody sees them
# from the game's camera. Its nose is at -Y. Its missiles leave the wing roots and its small gun leaves the chin.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("Vulture", "sketchfab.com/vehicleshumanstransportsvulture.glb")
mt.source_size()
mt.remove("Object_5|Object_6")
mt.fix_normals()
mt.reduce(9000, flat_angle=0)
mt.smooth(40)
mt.bake_one(1024)
mt.muzzle("Muzzle_0a", (6.5, -9, 6))
mt.muzzle("Muzzle_0b", (-6.5, -9, 6))
mt.muzzle("Muzzle_1", (0, -19, 3))
mt.motion("Hover")
mt.report("done")
mt.finish()
