# The Juggernaut, from the owner's Meshy model: 15,157 triangles to about 8,000 and one new texture. A pure form that wobbles (Motion_Wobble),
# whose two scythe arms are cut off at the shoulder and swing while it runs. The thin wisps at its mouth are 70 separate pieces, about 860
# triangles that nobody could see from the game's camera, so they go. It faces -Y as it came, and is tipped 3 degrees to stand on both feet.
# Numbers are the file's own sizes. Its longest side is from one arm to the other, well past its body.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bpy
import model_tools as mt

SWING_LEFT = "Swing_0_0_12_1.1"  # 12 degrees, 1.1 times a second: about the wobble's beat when it runs. Much more, and a blade reaches the ground.
SWING_RIGHT = "Swing_1_50_12_1.1"


def overlap(name, low, high):
    """A copy of the body's faces just inside a cut goes with the arm too, so that no gap can be seen through when the arm turns."""
    body = bpy.data.objects["Body"]
    spare = body.copy()
    spare.data = body.data.copy()
    spare.name = "Spare"
    bpy.context.scene.collection.objects.link(spare)
    mt.take(name, low, high, "^Spare$", pivot=(0, 0, 0), whole_pieces=False)
    bpy.data.objects.remove(spare)


def tip(name):
    """The forward end of an arm's blade."""
    arm = bpy.data.objects[name]
    return min((arm.matrix_world @ v.co for v in arm.data.vertices), key=lambda p: p.y)


mt.start("Juggernaut", "Meshy/Meshy_AI_Juggernaut_flood.glb")
mt.source_size()
mt.weld(0.0004)  # The file splits its vertices along the texture's seams: only welded can its really separate pieces be told apart.
mt.remove_small_islands(100)
mt.turn(pitch=1.85, roll=-2.2)
mt.report("clean")
mt.reduce_and_repaint(8000, 1024, reach=0.012)
mt.smooth(60)
mt.centre_on_ground()
mt.report("reduced")

# Each arm is cut where it is one tube, past the head and the two strands that the arm at -X leaves its shoulder in. That arm's blade curls back in
# past the cut, so it takes a second box. Both arms hang above the feet, so a height limit keeps those out. A swing turns around the X axis, so a cut
# across X mostly turns on itself: each pivot is the middle of its cut.
mt.take("ArmA", (-2, -2, 0.065), (-0.26, 2, 2), whole_pieces=False, pivot=(0, 0, 0))
mt.take("ArmB", (-0.26, -2, 0.065), (-0.08, -0.2, 2), whole_pieces=False, pivot=(0, 0, 0))
overlap("ArmC", (-0.26, -0.1, 0.065), (-0.235, 2, 2))
mt.part(SWING_LEFT, "^Arm[ABC]$", pivot=(-0.26, 0.145, 0.272))
mt.take("ArmD", (0.2, -2, 0.31), (2, 2, 2), whole_pieces=False, pivot=(0, 0, 0))
overlap("ArmE", (0.175, -2, 0.31), (0.2, 2, 2))
mt.part(SWING_RIGHT, "^Arm[DE]$", pivot=(0.2, 0.173, 0.387))

# Crushing Charge, weapon 0: the two blades take turns.
mt.muzzle("Muzzle_0a", tip(SWING_LEFT), parent=SWING_LEFT)
mt.muzzle("Muzzle_0b", tip(SWING_RIGHT), parent=SWING_RIGHT)
mt.motion("Wobble")
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=512)
mt.look("after_back", "back")
mt.look("far", "quarter", size=(160, 120))
