# The Cobra: 2,468 triangles, kept as they are. The upper hull with its two long guns turns (Turret_0), and its six wheels spin.
# Its nose is along +X in the file. Numbers below are the file's own sizes, after the turn.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("Cobra", "sketchfab.com/vehicleshumansgroundcobra.glb")
mt.source_size()
mt.turn(yaw=-90)
mt.smooth(40)

mt.take("Turret_0", (-1.6, -3.1, 2.62), (1.6, 2.1, 4.0), "Object_2", pivot=(0, 0.4, 2.5))
mt.muzzle("Muzzle_0a", (0.69, -2.89, 3.07), parent="Turret_0")
mt.muzzle("Muzzle_0b", (-0.69, -2.89, 3.07), parent="Turret_0")

for i, (x, y) in enumerate([(1.82, -2.87), (-1.82, -2.87), (1.85, 0.91), (-1.85, 0.91), (1.85, 3.0), (-1.85, 3.0)]):
    mt.take(f"Wheel_{i}", (x - 0.5, y - 1.0, -0.2), (x + 0.5, y + 1.0, 1.9), "Object_3", pivot=(x, y, 0.83))

mt.merge_rest()
mt.report("done")
mt.finish()
