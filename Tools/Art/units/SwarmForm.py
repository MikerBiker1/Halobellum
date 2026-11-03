# The Swarm Form, from the owner's Meshy model: 341,742 triangles to 1,500, as four bodies of five forms each fly at once. Its two
# wings are cut off at the shoulders and beat (Flap_L, Flap_R). Its nose is at -Y. Numbers are the file's own sizes.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("SwarmForm", "Meshy/Meshy_AI_Swarmform/Meshy_AI_swarm_form_glb.glb")
mt.source_size()
mt.reduce_and_repaint(1500, 512, reach=0.012)
mt.smooth(60)
SHOULDER = 0.17
mt.take("Flap_L", (SHOULDER, -1, -1), (2, 1, 1), pivot=(SHOULDER, 0, 0.15), whole_pieces=False)
mt.take("Flap_R", (-2, -1, -1), (-SHOULDER, 1, 1), pivot=(-SHOULDER, 0, 0.15), whole_pieces=False)
mt.muzzle("Muzzle_0", (0, -0.2, 0.05))
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=512)
