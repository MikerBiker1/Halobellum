# The Jackal's energy shield, from the owner's halo_5_jackal_shield.glb: a round sheet of 375 triangles with a notch for the gun. It is see-through
# and glowing in the file, and it goes on a baked body like a weapon, so it stays as it is and takes a colour per face from its texture. The file has
# the sheet upright facing +X with the notch toward -Y: turned to face -Y, the notch comes to -X, which is the side of the body's right hand, where the gun is.
# Unity sets its size (BakedBodyDefinition.shieldDiameter) and draws it see-through.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("Weapon_JackalShield", "Meshy/halo_5_jackal_shield.glb")
mt.source_size()
mt.turn(yaw=-90)
mt.report("as it came")
mt.snapshot()
mt.finish_weapon(grip=(0, -0.07, 0), tip=(0, -0.1, 0), length_note="1.2 across in the file")
mt.look("after_front", "front")
