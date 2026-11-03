# The Skitterer: 10,344 triangles to about 2,600, as a squad has six. Its four legs scuttle quickly (a large swing, fast, the
# diagonal pairs together), and its two side pods fire. It already faces -Y. Numbers are the file's own sizes.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("Skitterer", "sketchfab.com/enemiesbanishedskitterer.glb")
mt.source_size()
mt.reduce(2600, flat_angle=0)
mt.smooth(40)

for i, (sx, sy, phase) in enumerate([(1, -1, 0), (-1, 1, 0), (-1, -1, 50), (1, 1, 50)]):
    low = (min(0.03 * sx, 0.8 * sx), min(0.03 * sy, 0.8 * sy), -0.1)
    high = (max(0.03 * sx, 0.8 * sx), max(0.03 * sy, 0.8 * sy), 0.42)
    mt.take(f"Swing_{i}_{phase}_16_2.5", low, high, pivot=(0.22 * sx, 0.22 * sy, 0.38))
mt.muzzle("Muzzle_0a", (0.38, -0.33, 0.66))
mt.muzzle("Muzzle_0b", (-0.38, -0.33, 0.66))
mt.merge_rest()
mt.report("done")
mt.finish()
