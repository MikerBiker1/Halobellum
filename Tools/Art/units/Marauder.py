# The Banished Marauder (M10): the owner's Meshy hover tank, one mesh of 15,275 triangles in 594 loose pieces, split along its texture's
# seams. It already faces -Y, but it came nose-down, so it is levelled. Its head on the neck turns (Turret_0): the plasma cannon fires
# from the toothed maw (weapon 0) and the spikes from the two short tubes beside the jaw (weapon 1, in turns). The two engine pods at the
# back are whole pieces, and rock a little as it moves. It has no wheels or tracks, so it hovers, as the Wraith does.
import math
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bpy
from mathutils import Matrix, Vector

PITCH = -10.0

mt.start("Marauder", "Meshy/Meshy_AI_Marauder.glb")

# The head is told from the hull while the mesh is still in its loose pieces, because welded they are one. Numbers are the model as loaded, 10 long.
TOP = 3.0
for i, (low, high) in enumerate([((-1.6, 1.0, 1.55), (1.6, 5.2, TOP)),  # All that stands above the hull's back.
                                 ((-1.1, 2.9, 0.85), (1.1, 5.2, TOP)),  # The cheeks and the hood, down to the neck.
                                 ((-1.1, 2.5, 1.35), (1.1, 5.2, TOP))]):  # The throat under the jaw.
    mt.take(f"Head{i}", low, high, "geometry_0", pivot=(0, 0, 0))
mt.part("Head", r"^Head\d$", pivot=(0, 0, 0))
mt.report("head apart")

mt.turn(pitch=PITCH)
turned = mt._bounds()
mt.fit()
after = mt._bounds()
size = (after[1] - after[0]).length / (turned[1] - turned[0]).length
shift = after[0] - turned[0] * size
level = Matrix.Rotation(math.radians(PITCH), 4, "X")


def at(x, y, z):
    """Coordinates measured on the model as loaded, moved to where levelling and fit() put them."""
    return (level @ Vector((x, y, z))) * size + shift


mt.weld(0.002)
mt.report("welded")

# Each pod is one whole piece once welded: its middle is enough to find it, and each rocks around where it meets the hull.
for name, side in (("Swing_0_0_6_1", 1), ("Swing_1_50_6_1", -1)):
    middle = at(side * 2.24, 3.15, 0.05)
    mt.take(name, middle - Vector((0.4, 0.4, 0.4)), middle + Vector((0.4, 0.4, 0.4)), "geometry_0", pivot=at(side * 1.35, 3.15, 0.05))

mt.reduce(1900, "^Head$")
mt.reduce(6300, "geometry_0")
mt.reduce(380, "^Swing_")
mt.smooth(35)

mt.part("Turret_0", "^Head$", pivot=at(0, 3.75, 0.85))
mt.muzzle("Muzzle_0", at(0, 1.62, 2.18), parent="Turret_0")
mt.muzzle("Muzzle_1a", at(0.655, 2.42, 2.04), parent="Turret_0")
mt.muzzle("Muzzle_1b", at(-0.655, 2.42, 2.04), parent="Turret_0")
mt.motion("Hover")
mt.merge_rest()
mt.report("done")
mt.finish()
mt.look("after_back", "back")

# A last check that the head turns cleanly. The file is written already.
head = bpy.data.objects["Turret_0"]
head.rotation_mode = "XYZ"
head.rotation_euler[2] = 1.2
bpy.context.view_layer.update()
mt.look("after_turned", "quarter")
