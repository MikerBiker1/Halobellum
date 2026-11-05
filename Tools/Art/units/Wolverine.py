# The Wolverine: the owner's Meshy model with no colours, painted by Meshy (Tools/Meshy/meshy_paint.py). In the file its nose and the tips of its
# missiles point along -X. A half-track: two big wheels in front, tracks behind. Everything above the hull's top turns around the round column
# under the missile pods (Turret_0), and the two wheels spin. The tracks' small wheels are lumps in one skin with the tracks, so they stay still.
#   blender --background --factory-startup --python Tools/Art/units/Wolverine.py -- input     writes what Meshy paints (_Paint/Wolverine/input.glb)
#   python Tools/Meshy/meshy_paint.py Wolverine "<what it should look like>"
#   blender --background --factory-startup --python Tools/Art/units/Wolverine.py              prepares the painted model
# Sizes are in metres. The file is one skin. Cut apart at the start, its pieces are capped, reduced each with its own number of triangles, and sent
# with a small gap between them, which tells them apart again when they come back from Meshy with their surface split along its seams.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

LENGTH = 5.5
CUT_Z = 2.05  # Just above the top of the hull: only the two columns under the turret cross it.
RING = (0.0, 2.1)  # The rear column, the one under the pods.
WHEEL_X, WHEEL_Y, WHEEL_Z, WHEEL_REACH = 1.10, -1.98, 0.64, 0.74
GAP = 0.05
BUDGET = {"Hull": 5400, "Turret": 2400, "WheelL": 400, "WheelR": 400}


def in_wheel(p, side):
    return p.x * side >= WHEEL_X and (Vector((p.y, p.z)) - Vector((WHEEL_Y, WHEEL_Z))).length <= WHEEL_REACH


def split_off(o, name, test):
    """Moves the faces whose middle passes the test to a new object."""
    copy = o.copy()
    copy.data = o.data.copy()
    copy.name = name
    bpy.context.scene.collection.objects.link(copy)
    for target, keep in ((copy, True), (o, False)):
        mesh = bmesh.new()
        mesh.from_mesh(target.data)
        doomed = [f for f in mesh.faces if test(f.calc_center_median()) != keep]
        bmesh.ops.delete(mesh, geom=doomed, context="FACES_ONLY")
        bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
        mesh.to_mesh(target.data)
        mesh.free()
    return copy


def cut(o):
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    bmesh.ops.bisect_plane(mesh, geom=mesh.verts[:] + mesh.edges[:] + mesh.faces[:], plane_co=(0, 0, CUT_Z), plane_no=(0, 0, 1))
    for side in (1, -1):
        near = [f for f in mesh.faces if abs(f.calc_center_median().y - WHEEL_Y) < 1.0 and f.calc_center_median().z < 1.6]
        edges = list({e for f in near for e in f.edges})
        verts = list({v for f in near for v in f.verts})
        bmesh.ops.bisect_plane(mesh, geom=verts + edges + near, plane_co=(WHEEL_X * side, 0, 0), plane_no=(1, 0, 0))
    mesh.to_mesh(o.data)
    mesh.free()
    o.name = "Hull"
    split_off(o, "Turret", lambda p: p.z > CUT_Z)
    split_off(o, "WheelL", lambda p: in_wheel(p, 1))
    split_off(o, "WheelR", lambda p: in_wheel(p, -1))


def reduce_and_cap(o, target):
    mt._select([o])
    modifier = o.modifiers.new("less", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = min(1.0, target / mt.tris([o]))
    bpy.ops.object.modifier_apply(modifier="less")
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    open_edges = [e for e in mesh.edges if e.is_boundary]
    made = bmesh.ops.holes_fill(mesh, edges=open_edges, sides=0)["faces"]
    bmesh.ops.triangulate(mesh, faces=made)
    bmesh.ops.recalc_face_normals(mesh, faces=mesh.faces)
    mesh.to_mesh(o.data)
    mesh.free()
    print(f"REPORT {o.name}: {mt.tris([o])} triangles, {len(open_edges)} open edges capped")


def make_input():
    """Mt.start empties its folder, so this won't run over a model already painted."""
    if os.path.exists(os.path.join(mt.PREPARED, "_Paint", "Wolverine", "painted.glb")):
        print("REPORT already painted: input.glb left as it is")
        return
    mt.PREPARED = os.path.join(mt.PREPARED, "_Paint")
    mt.start("Wolverine", "Meshy/Meshy_AI_Wolverine.glb")
    mt.source_size()
    mt.turn(yaw=90)
    mt.weld(0.0002)
    mt.fix_normals()
    mt.fit(LENGTH)
    cut(mt.meshes()[0])
    for name, target in BUDGET.items():
        reduce_and_cap(bpy.data.objects[name], target)
    bpy.data.objects["Turret"].data.transform(Matrix.Translation((0, 0, GAP)))
    bpy.data.objects["WheelL"].data.transform(Matrix.Translation((GAP, 0, 0)))
    bpy.data.objects["WheelR"].data.transform(Matrix.Translation((-GAP, 0, 0)))
    mt.report("input")
    bpy.context.scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.2, 0.2, 0.22, 1)  # A white model on a pale sky shows nothing.
    for camera in ("quarter", "side", "front", "back"):
        mt.look("input_" + camera, camera, size=(900, 700))
    mt._select(mt.meshes())
    bpy.ops.object.join()
    path = os.path.join(mt._state["folder"], "input.glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_yup=True, export_apply=True, export_animations=False,
                              export_skins=False, export_materials="NONE", export_cameras=False, export_lights=False)
    print(f"REPORT wrote {path}, {os.path.getsize(path)} bytes, {mt.tris()} triangles")


if "input" in sys.argv:
    make_input()
    sys.exit(0)


def pieces(o):
    """The separate pieces of a model whose surface is split along its seams: faces that touch at a corner, however many copies of that corner there are."""
    points = np.array([v.co[:] for v in o.data.vertices])
    keys = {}
    corner = [keys.setdefault(tuple(k), len(keys)) for k in np.round(points / 0.002).astype(np.int64).tolist()]
    parent = list(range(len(keys)))

    def root(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    for polygon in o.data.polygons:
        first = root(corner[polygon.vertices[0]])
        for v in polygon.vertices[1:]:
            parent[root(corner[v])] = first
    faces = {}
    for polygon in o.data.polygons:
        faces.setdefault(root(corner[polygon.vertices[0]]), []).append(polygon.index)
    return [[list(o.data.polygons[i].vertices) for i in group] + [group] for group in faces.values()], points


def owner_of(group, points):
    """Which part a separate piece belongs to, from where it is. Each came back with the gap it was sent with."""
    at = points[sorted({v for face in group[:-1] for v in face})]
    middle = at.mean(axis=0)
    if at[:, 2].min() > CUT_Z + GAP / 2:
        return "Turret_0"
    reach = np.hypot(middle[1] - WHEEL_Y, middle[2] - WHEEL_Z)
    if reach < 0.78 and abs(at[:, 0]).min() > WHEEL_X + GAP / 2:
        return "Wheel_0" if middle[0] > 0 else "Wheel_1"
    return "Body"


def take_apart(o):
    groups, points = pieces(o)
    owners = {}
    for group in groups:
        for index in group[-1]:
            owners[index] = owner_of(group, points)
    print(f"REPORT {len(groups)} separate pieces came back")
    for name in sorted(set(owners.values()) - {"Body"}):
        copy = o.copy()
        copy.data = o.data.copy()
        copy.name = name
        bpy.context.scene.collection.objects.link(copy)
        mesh = bmesh.new()
        mesh.from_mesh(copy.data)
        mesh.faces.ensure_lookup_table()
        bmesh.ops.delete(mesh, geom=[f for f in mesh.faces if owners[f.index] != name], context="FACES_ONLY")
        bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
        mesh.to_mesh(copy.data)
        mesh.free()
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    mesh.faces.ensure_lookup_table()
    bmesh.ops.delete(mesh, geom=[f for f in mesh.faces if owners[f.index] != "Body"], context="FACES_ONLY")
    bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
    mesh.to_mesh(o.data)
    mesh.free()
    o.name = "Body"


def fender_off_wheel(name):
    """The first cut was rough: what is past the tyre belongs to the fender, and must not spin. It joins the body."""
    o = bpy.data.objects[name]
    outside = lambda p: (Vector((p.y, p.z)) - Vector(AXLE)).length > TYRE
    split_off(o, "Fender_" + name, outside)
    print(f"REPORT {name}: {mt.tris([bpy.data.objects['Fender_' + name]])} triangles of fender moved to the body")


# Where the missiles' tips are, on the pod on the side of +X, the other pod being its mirror image: five above, three below.
TOP = [0.85, 1.01, 1.19, 1.35, 1.51]
BELOW = [1.01, 1.23, 1.41]
TOP_Z, BELOW_Z, TIP_Y = 2.71, 2.54, 1.08
AXLE, TYRE = (-1.99, 0.62), 0.665  # The tyre's middle and reach, measured on it.

mt.start("Wolverine", "Prepared/_Paint/Wolverine/painted.glb")
mt.source_size()
mt.fit(LENGTH)
mt.report("loaded")
for image in bpy.data.images:
    if image.size[0] > 512:
        image.scale(512, 512)  # So that the pictures show what the game gets.
take_apart(mt.meshes()[0])
mt.report("apart")
for o in mt.meshes():
    print(f"REPORT {o.name}: {mt._round(mt._bounds([o])[0])} to {mt._round(mt._bounds([o])[1])}")
bpy.data.objects["Turret_0"].data.transform(Matrix.Translation((0, 0, -GAP)))
bpy.data.objects["Wheel_0"].data.transform(Matrix.Translation((-GAP, 0, 0)))
bpy.data.objects["Wheel_1"].data.transform(Matrix.Translation((GAP, 0, 0)))
for o in mt.meshes():
    o.data.update()
fender_off_wheel("Wheel_0")
fender_off_wheel("Wheel_1")
mt.smooth(35)
mt._set_origin(bpy.data.objects["Turret_0"], (RING[0], RING[1], CUT_Z))
for name in ("Wheel_0", "Wheel_1"):
    o = bpy.data.objects[name]
    low, high = mt._bounds([o])
    mt._set_origin(o, ((low.x + high.x) / 2, AXLE[0], AXLE[1]))

# Two pods take turns, each from the top row and the bottom row in turn.
tips = [(x, TOP_Z) for x in TOP] + [(x, BELOW_Z) for x in BELOW]
tips = [tips[i] for i in (2, 6, 0, 5, 4, 7, 1, 3)]
for i, (x, z) in enumerate(tips):
    mt.muzzle("Muzzle_0" + "abcdefghijklmnop"[i * 2], (x, TIP_Y, z), parent="Turret_0")
    mt.muzzle("Muzzle_0" + "abcdefghijklmnop"[i * 2 + 1], (-x, TIP_Y, z), parent="Turret_0")

mt.merge_rest()
mt.report("done")
mt.finish(texture_size=512)
