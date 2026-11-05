# The Mantis, from the owner's Meshy model: 1,091,042 triangles to about 9,000, painted afresh from its own colours. It stands on two
# legs and already faces -Y. Numbers are in a model 10 units tall (start() brings it to that), on the ground with its middle on the origin.
# The file is one closed skin, so there are no pieces to lift out: it is sliced level at the waist and across the thighs, just where
# each slice meets nothing but what it is meant to part (the arms hang free of the body down there), and the cuts get black caps.
# Above the waist, with both arms and the pack, it turns (Turret_0). Below the thighs its legs swing, 10 degrees at 0.7 cycles a second,
# so that at the unit's 1.5 m/s and 6 m tall its feet hardly slide. The hips between stay still.
# The chaingun is on its -X arm and the rockets are on its +X arm.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
from mathutils import Vector

BIG = 20
WAIST = 6.15  # Under the chest's overhang and the pack.
THIGHS = 4.8  # The highest level at which the legs still stand apart.
HIPS = 5.2
CAP_UV = (0.002, 0.002)  # A corner of the texture that nothing was painted on: black.


def slice_level(o, height):
    """Cuts the body in two at this height, leaving the arms whole, and closes both sides. Gives the middle of the cuts it made."""
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    near = [f for f in mesh.faces if all(abs(v.co.x) < 1.87 for v in f.verts)]  # The body is narrower than this and the arms are outside it, so they aren't cut for nothing.
    geom = near + list({e for f in near for e in f.edges}) + list({v for f in near for v in f.verts})
    cut = [e for e in bmesh.ops.bisect_plane(mesh, geom=geom, dist=1e-5, plane_co=(0, 0, height), plane_no=(0, 0, 1))["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    left = set(cut)
    rims = []
    while left:
        rim, stack = set(), [left.pop()]
        while stack:
            edge = stack.pop()
            rim.add(edge)
            for vert in edge.verts:
                for other in vert.link_edges:
                    if other in left:
                        left.remove(other)
                        stack.append(other)
        rims.append(rim)
    parted, middles = [], []
    for rim in rims:
        points = [v.co for e in rim for v in e.verts]
        low, high = mt._low(points), mt._high(points)
        middle = (low + high) / 2
        if abs(middle.x) < 1.7:  # An arm's ring is further out. It stays joined.
            parted.extend(rim)
            middles.append(middle)
            print(f"REPORT cut at {height}: from {mt._round(low)} to {mt._round(high)}, {len(rim)} edges")
    bmesh.ops.split_edges(mesh, edges=parted)
    open_edges = [e for e in mesh.edges if e.is_boundary]
    faces = bmesh.ops.holes_fill(mesh, edges=open_edges, sides=0)["faces"]
    faces = bmesh.ops.triangulate(mesh, faces=faces)["faces"]
    bmesh.ops.recalc_face_normals(mesh, faces=mesh.faces)
    layer = mesh.loops.layers.uv.active
    for face in faces:
        for loop in face.loops:
            loop[layer].uv = CAP_UV
    mesh.to_mesh(o.data)
    mesh.free()
    o.data.update()
    return middles


mt.start("Mantis", "Meshy/Meshy_AI_UNSC_Mantis.glb")
mt.centre_on_ground()
mt.reduce_and_repaint(8300, 1024, weld_distance=0.001, reach=0.05)
body = mt.meshes("^Body$")[0]
waist = slice_level(body, WAIST)
slice_level(body, THIGHS)
mt.smooth(40)
mt.islands("^Body$")

# Each piece goes by the middle of its box. All above the waist has its middle above 6.3. The tips of the thighs that come a little
# through the slice stay behind and go: they are hidden under the chest, and the hips below them have their own caps.
pivot = sum(waist, Vector()) / len(waist)
mt.take("Turret_0", (-BIG, -BIG, 6.3), (BIG, BIG, BIG), "^Body$", pivot=(pivot.x, pivot.y, WAIST))
mt.take("Swing_0_0_10_0.7", (-BIG, -BIG, -BIG), (-0.25, BIG, THIGHS), "^Body$", pivot=(-1.07, -0.3, HIPS))
mt.take("Swing_1_50_10_0.7", (0.25, -BIG, -BIG), (BIG, BIG, THIGHS), "^Body$", pivot=(1.07, -0.3, HIPS))
mt.remove_small_islands(30, "^Body$")

mt.muzzle("Muzzle_0", (-3.12, -2.05, 2.8), parent="Turret_0")
mt.muzzle("Muzzle_1", (3.29, -1.6, 3.05), parent="Turret_0")
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=512)
