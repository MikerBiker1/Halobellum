# The Rhino (D184): the owner's new Meshy model, Meshy/rhino.glb, one closed skin of 3,319,898 triangles with its own textures. Its long
# artillery barrel points along -X in the file, so it is turned to face -Y. Reduced to about 20,000 triangles (as many as the old one had) and
# painted afresh from its own colours (reduce_and_repaint: reducing Meshy's seams as they are would tear them). Its turret stands on a round
# base: it is sliced off level just above the hull, inside the base's circle alone, the cuts get black caps, and it turns (Turret_0).
# Its shells leave the end of the barrel. Numbers are in a model 10 units long (start() brings it to that), on the ground with its middle on
# the origin.
#   blender --background --factory-startup --python Tools/Art/units/Rhino.py        (HB_ONLY=measure: print slices and stop)
import math
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import numpy as np

ONLY = os.environ.get("HB_ONLY", "")
BIG = 20
CUT = 1.92  # Just above the hull's deck, through the turret's round base.
AXIS = (0.0, 0.2)  # The middle of the base, which it turns about.
RADIUS = 1.4  # The base is 1.28 across from its middle: nothing else of the hull is cut.
CAP_UV = (0.002, 0.002)  # A corner of the baked texture with nothing painted on it: black.


def slice_turret(o):
    """Cuts level at CUT, through the faces inside RADIUS of the turret's axis alone, and closes both sides with caps."""
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    near = [f for f in mesh.faces if all(math.hypot(v.co.x - AXIS[0], v.co.y - AXIS[1]) < RADIUS for v in f.verts)]
    geom = near + list({e for f in near for e in f.edges}) + list({v for f in near for v in f.verts})
    cut = [e for e in bmesh.ops.bisect_plane(mesh, geom=geom, dist=1e-5, plane_co=(0, 0, CUT), plane_no=(0, 0, 1))["geom_cut"]
           if isinstance(e, bmesh.types.BMEdge)]
    print(f"REPORT cut at {CUT}: {len(cut)} edges")
    bmesh.ops.split_edges(mesh, edges=cut)
    faces = bmesh.ops.holes_fill(mesh, edges=[e for e in mesh.edges if e.is_boundary], sides=0)["faces"]
    faces = bmesh.ops.triangulate(mesh, faces=faces)["faces"]
    bmesh.ops.recalc_face_normals(mesh, faces=mesh.faces)
    layer = mesh.loops.layers.uv.active
    for face in faces:
        for loop in face.loops:
            loop[layer].uv = CAP_UV
    mesh.to_mesh(o.data)
    mesh.free()
    o.data.update()

mt.start("Rhino", "Meshy/rhino.glb")
mt.turn(yaw=90)
mt.centre_on_ground()

if ONLY == "measure":
    o = mt.meshes()[0]
    n = len(o.data.vertices)
    co = np.empty(n * 3, dtype=np.float32)
    o.data.vertices.foreach_get("co", co)
    co = co.reshape(n, 3) @ np.array(o.matrix_world)[:3, :3].T + np.array(o.matrix_world)[:3, 3]
    print("REPORT bounds", co.min(0).round(3), co.max(0).round(3))
    top = co[co[:, 2] > co[:, 2].max() - 0.2]
    print("REPORT barrel tip", top.mean(0).round(3), "front-most of it", top[top[:, 1].argmin()].round(3))
    for z0 in np.arange(0, co[:, 2].max(), 0.1):
        s = co[(co[:, 2] >= z0) & (co[:, 2] < z0 + 0.1)]
        if len(s):
            print(f"REPORT z {z0:4.1f}: n {len(s):7d} x {s[:, 0].min():6.2f}..{s[:, 0].max():6.2f} y {s[:, 1].min():6.2f}..{s[:, 1].max():6.2f}")
    sys.exit(0)

mt.reduce_and_repaint(20000, 2048, weld_distance=0.001, reach=0.05)
body = mt.meshes("^Body$")[0]
slice_turret(body)
mt.smooth(40)
mt.islands("^Body$", smallest=30)
mt.take("Turret_0", (-BIG, -BIG, CUT + 0.005), (BIG, BIG, BIG), "^Body$", pivot=(AXIS[0], AXIS[1], CUT))
mt.remove_small_islands(30, "^Body$")  # A crumb the cut leaves under a pin on the base.

# Its shells leave the end of the barrel: the middle of its furthest-forward part, a little ahead of it.
turret = mt.meshes("^Turret_0$")[0]
points = np.array([turret.matrix_world @ v.co for v in turret.data.vertices])
tip = points[points[:, 1] < points[:, 1].min() + 0.25]
end = (tip.min(axis=0) + tip.max(axis=0)) / 2
print(f"REPORT barrel end {tuple(round(float(v), 3) for v in end)}")
mt.muzzle("Muzzle_0", (float(end[0]), float(points[:, 1].min()) - 0.05, float(end[2])), parent="Turret_0")
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=1024)
