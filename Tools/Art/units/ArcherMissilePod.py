# The Archer Missile Pod, from the owner's Meshy model with no colours, painted by Meshy (Tools/Meshy/texture_inputs.py, meshy_paint.py). Everything
# above its tripod turns toward the target (Turret_0). Sizes are in metres, from texture_inputs.py. It faces -Y.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("ArcherMissilePod", "Prepared/_Paint/ArcherMissilePod/painted.glb")
mt.source_size()
mt.scale(3.567 / 2.0)  # Meshy gives it back 2 units tall, and centred as it was.
mt.report("loaded")
mt.smooth(35)
mt.take("Turret_0", (-5, -5, -0.75), (5, 5, 5), pivot=(0, 0, -0.8), whole_pieces=False)
mt.muzzle("Muzzle_0", (0, -0.94, 1.72), parent="Turret_0")
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=1024)
