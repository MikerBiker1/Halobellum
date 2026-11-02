# The Banished Chopper (M10): one mesh of 3,700 triangles, light enough as it is. Its great front wheel spins (Wheel_0), and the
# two guns beside the wheel fire its autocannons (weapon 1; weapon 0 is the ram, which has no muzzle).
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import bpy
from mathutils import Vector

mt.start("Chopper", "sketchfab.com/For Later/vehiclescovenantgroundchopper.glb")
mt.turn(yaw=-90)  # It came with its nose along +X.
mt.fit()
mt.report("turned")

# List the biggest pieces, to see which of them is the wheel.
for o in mt.meshes():
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    for faces in sorted(mt._islands(mesh), key=len, reverse=True)[:8]:
        points = [o.matrix_world @ v.co for f in faces for v in f.verts]
        print(f"REPORT piece of {len(faces)} faces from {mt._low(points)} to {mt._high(points)}")
    mesh.free()

mt.weld(0.003)  # The file's faces come loose and any way round, and the game hides back faces.
mt.fix_normals()
mt.smooth(40)
mt.muzzle("Muzzle_1a", (1.45, -4.15, 2.0))
mt.muzzle("Muzzle_1b", (-1.45, -4.15, 2.0))
mt.merge_rest()
mt.report("done")
mt.finish()
