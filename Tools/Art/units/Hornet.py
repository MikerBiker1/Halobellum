# The Hornet (D184): the owner's new model, vehicleshumanfightershornet.glb, which holds three Hornets side by side; the owner asked for the
# first (Object_3, its body, and Object_2, its rotors). It comes with 2,226 triangles and one texture, and already faces -Y, its nose hanging
# low and its tail fin high. Each wing tip carries a rotor: two discs, one over the other, painted with the streaks of spinning blades. They
# become Rotor_0 (its -X side) and Rotor_1, which spin all the time in the game (ModelParts), each about its hub.
# Its rockets leave the fronts of the two launchers on its wing roots.
# Numbers are in a model 10 units long (fit()), on the ground with its middle on the origin.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import numpy as np

BIG = 20

mt.start("Hornet", "sketchfab.com/vehicleshumanfightershornet.glb")
mt.remove("^Object_[456]$")  # The other two Hornets in the file and their glass.
mt.fit()
mt.report("fitted")

# Each rotor's two discs go together, turning about the middle of their hub.
discs = mt.meshes("^Object_2$")[0]
points = np.array([discs.matrix_world @ v.co for v in discs.data.vertices])
for i, (low, high) in enumerate(((-BIG, 0.0), (0.0, BIG))):
    side = points[(points[:, 0] >= low) & (points[:, 0] < high)]
    hub = (side.min(axis=0) + side.max(axis=0)) / 2
    print(f"REPORT rotor {i}: hub at {tuple(round(float(v), 3) for v in hub)}, radius {round(float(side[:, 0].max() - side[:, 0].min()) / 2, 2)}")
    mt.take(f"Rotor_{i}", (low, -BIG, -BIG), (high, BIG, BIG), "^Object_2$", pivot=tuple(float(v) for v in hub))

mt.remove("^Object_2$")  # Empty now: both rotors are taken out of it.
mt.smooth(35)
mt.muzzle("Muzzle_0a", (-0.83, -1.8, 3.3))
mt.muzzle("Muzzle_0b", (0.83, -1.8, 3.3))
mt.merge_rest()
mt.report("done")
mt.finish()
