# The Flood's growths, from the owner's Meshy model (decision D37: "one or a few pieces that can be placed on any vehicle"). The file is a
# cluster of fleshy pods and bubbles on a brown block, 15,063 triangles. Once its texture seams are welded, the block is one piece and each
# pod or bubble another (74 of them). The block goes, and the pods are reduced and painted afresh. Kept are the BIG largest pods (the ones
# split open at the top) as Growth_Big_<n>, and SMALL bubbles as Growth_Small_<n>. Each is turned so that the way it grew out of the block
# is up (+Z), and its origin is where it meets a surface: at the bottom of the pod, raised by SINK of its height so that it sinks into
# whatever it stands on. RealModelBuilder sets a few clumps of them on every infested vehicle, wherever its surface is.
# Numbers are the file's own sizes.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
import model_tools as mt

WELD = 0.0005
TRIANGLES = 4000  # For all 74 pods, before most are left out: about 130 for a big pod.
BAKED = 1024
TEXTURE = 512
BIG = 5
SMALL = 5
SINK = 0.25


def split_off_block():
    """Takes the biggest piece (the block the pods grow on) out of the model, and returns a tree of its surface to find which way each pod grew."""
    o = mt.meshes()[0]
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    pieces = sorted(mt._islands(mesh), key=len)
    block = bmesh.new()
    points = {}
    for face in pieces[-1]:
        corners = []
        for v in face.verts:
            if v.index not in points:
                points[v.index] = block.verts.new(o.matrix_world @ v.co)
            corners.append(points[v.index])
        block.faces.new(corners)
    block.normal_update()
    tree = BVHTree.FromBMesh(block)
    bmesh.ops.delete(mesh, geom=pieces[-1], context="FACES_ONLY")
    bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
    mesh.to_mesh(o.data)
    mesh.free()
    print(f"REPORT took off the block ({len(pieces[-1])} faces); {len(pieces) - 1} pods are left")
    return tree, block


def take(body, faces, name):
    """A new part made of these faces of the body."""
    copy = body.copy()
    copy.data = body.data.copy()
    bpy.context.scene.collection.objects.link(copy)
    mesh = bmesh.new()
    mesh.from_mesh(copy.data)
    mesh.faces.ensure_lookup_table()
    keep = set(faces)
    bmesh.ops.delete(mesh, geom=[f for f in mesh.faces if f.index not in keep], context="FACES_ONLY")
    bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
    mesh.to_mesh(copy.data)
    mesh.free()
    copy.name = name
    return copy


def pods(tree):
    """Makes the kept pods, each standing up on its own origin."""
    body = mt.meshes("^Body$")[0]
    mesh = bmesh.new()
    mesh.from_mesh(body.data)
    found = []
    for faces in mt._islands(mesh):
        points = [body.matrix_world @ v.co for f in faces for v in f.verts]
        low, high = mt._low(points), mt._high(points)
        found.append(((high - low).length, (low + high) / 2, [f.index for f in faces]))
    mesh.free()
    found.sort(key=lambda p: -p[0])
    # The bubbles are many and alike: a few from the middle of the rest, not the tiniest.
    rest = found[BIG:]
    chosen = [("Big", i, p) for i, p in enumerate(found[:BIG])]
    chosen += [("Small", i, rest[int(len(rest) * (0.1 + 0.4 * i / max(1, SMALL - 1)))]) for i in range(SMALL)]
    for kind, i, (size, middle, faces) in chosen:
        part = take(body, faces, f"Growth_{kind}_{i}")
        location, normal, _, _ = tree.find_nearest(middle)
        if (middle - location).dot(normal) < 0:
            normal = -normal
        turn = normal.rotation_difference(Vector((0, 0, 1))).to_matrix().to_4x4()
        part.data.transform(turn @ Matrix.Translation(-middle))
        points = [v.co for v in part.data.vertices]
        low, high = mt._low(points), mt._high(points)
        part.data.transform(Matrix.Translation(Vector((0, 0, -low.z - SINK * (high.z - low.z)))))
        part.data.update()
        a, b = mt._bounds([part])
        print(f"REPORT {part.name}: {mt.tris([part])} triangles, size {mt._round(b - a)}, grew out along {mt._round(normal)}")
    bpy.data.objects.remove(body)


mt.start("Growths", "Meshy/Meshy_AI_growths.glb")
mt.source_size()
mt.weld(WELD)
tree, block = split_off_block()
image = mt.reduce_and_repaint(TRIANGLES, BAKED, weld_distance=WELD, reach=0.006)
mt.fill_gaps(image)
mt.smooth(60)
pods(tree)
block.free()
mt.report("done")
mt.finish(texture_size=TEXTURE)
# All the pieces stand on the origin: side by side for a picture of them.
for i, part in enumerate(mt.meshes("^Growth_")):
    part.location.x = (i % 5 - 2) * 0.2
    part.location.y = (i // 5) * 0.25
bpy.context.view_layer.update()
mt.look("pieces", "quarter")
