# The Spawner, from the owner's Meshy model: 15,725 triangles to under 9,000 and one texture. It came facing -Y: the mass of tendrils is its maw.
# The file's surface is split along its texture's seams, so it is welded, reduced and painted afresh from the detailed model, as
# reduce_and_repaint does. But once welded it is one big body with 54 separate tendrils, so the two are handled apart:
#   - Every tendril is first thickened to a least width, in the detailed model too so the paint still lands, since hairs thinner than a pixel flicker.
#   - The body takes most of the cut. The long tendrils take less, and the smallest are left whole, so that none loses its tip.
# Its four legs are one skin with its body, and Swing_ turns a leg against the world and not against a wobbling body, so they stay in the body
# and the whole creature wobbles (Motion_Wobble), as the other big Flood forms do. Spore Lash leaves from the maw (Muzzle_0).
# Numbers are the file's own sizes.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import bpy
import math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

BODY_TRIS = 5500
TENDRIL_TRIS = 3300
KEEP_WHOLE_BELOW = 60  # A tendril with fewer faces than this has none to spare.
LEAST_RADIUS = 0.0028  # About 5 cm across at the suggested size in the game.
WELD = 0.00003  # A larger one would close up the thinnest hairs.
REACH = 0.008
BAKED = 1024
TEXTURE = 512
TENDRIL_PAPER = 0.45
UV_ANGLE = 72
MAW = (-0.005, -0.2, 0.02)


def thicken_tendrils(o):
    """Pushes the surface of every piece but the biggest outward wherever it is thinner than twice LEAST_RADIUS, measuring across from each vertex."""
    welded = bmesh.new()
    welded.from_mesh(o.data)
    bmesh.ops.remove_doubles(welded, verts=welded.verts, dist=WELD)
    welded.normal_update()
    welded.verts.ensure_lookup_table()
    tree = BVHTree.FromBMesh(welded)
    pieces = sorted(mt._islands(welded), key=len)
    push = {}
    for faces in pieces[:-1]:
        for v in {v for f in faces for v in f.verts}:
            hit = tree.ray_cast(v.co - v.normal * 1e-5, -v.normal, 1.0)
            across = hit[3] if hit[0] is not None else 0.0
            push[v.index] = v.normal * max(0.0, LEAST_RADIUS - across / 2)
    # Once over the neighbours, so that one odd vertex doesn't make a spike.
    even = {}
    for index, amount in push.items():
        around = [push[e.other_vert(welded.verts[index]).index] for e in welded.verts[index].link_edges]
        even[index] = (amount + sum(around, Vector())) / (1 + len(around))
    spots = KDTree(len(even))
    for index in even:
        spots.insert(welded.verts[index].co, index)
    spots.balance()
    moved = 0
    for v in o.data.vertices:
        _, index, distance = spots.find(v.co)
        if distance <= WELD * 2:
            v.co = v.co + even[index]
            moved += 1
    o.data.update()
    print(f"REPORT thickened {moved} of {len(o.data.vertices)} vertices, in {len(pieces) - 1} tendrils")
    welded.free()


def boxes(o):
    """The size of each separate piece, largest first, to compare before and after."""
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    bmesh.ops.remove_doubles(mesh, verts=mesh.verts, dist=WELD)
    result = []
    for faces in mt._islands(mesh):
        points = [v.co for f in faces for v in f.verts]
        result.append(round((mt._high(points) - mt._low(points)).length, 4))
    mesh.free()
    return sorted(result, reverse=True)


def reduce_apart(low):
    """Cuts the body and the long tendrils each to its own number of triangles. Returns the whole, joined again."""
    mt._select([low])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    pieces = sorted([o for o in mt.meshes() if o.name != "High"], key=lambda o: mt.tris([o]))
    pieces[-1].name = "Main"
    kept = 0
    for i, o in enumerate(pieces[:-1]):
        if mt.tris([o]) < KEEP_WHOLE_BELOW:
            o.name = f"TendrilWhole_{i}"
            kept += mt.tris([o])
        else:
            o.name = f"TendrilLong_{i}"
    print(f"REPORT tendrils kept whole: {kept} triangles")
    mt.reduce(BODY_TRIS, "^Main$", flat_angle=0)
    mt.reduce(TENDRIL_TRIS - kept, "^TendrilLong_", flat_angle=0)
    pieces = [o for o in mt.meshes() if o.name != "High"]
    mt._select(pieces)
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = "Body"
    return joined


def unwrap(low):
    """A new UV map. The tendrils are nearly all one flat red, so each is shrunk around its middle while unwrapping: the UV map goes by size, and the body gets their share of the texture."""
    mesh = bmesh.new()
    mesh.from_mesh(low.data)
    mesh.verts.ensure_lookup_table()
    where = np.empty(len(low.data.vertices) * 3)
    low.data.vertices.foreach_get("co", where)
    for faces in sorted(mt._islands(mesh), key=len)[:-1]:
        verts = {v for f in faces for v in f.verts}
        middle = sum((v.co for v in verts), Vector()) / len(verts)
        for v in verts:
            low.data.vertices[v.index].co = middle + (v.co - middle) * TENDRIL_PAPER
    mesh.free()
    low.data.update()
    mt._select([low])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(UV_ANGLE), island_margin=0.002)
    bpy.context.scene.tool_settings.use_uv_select_sync = True
    bpy.ops.uv.pack_islands(rotate=True, margin=0.002, shape_method="CONCAVE")
    bpy.ops.object.mode_set(mode="OBJECT")
    low.data.vertices.foreach_set("co", where)
    low.data.update()


def fill_unpainted(image):
    """The texture between the pieces stays black, and would darken every seam when the game draws it small. It gets the texture's average colour."""
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    pixels = pixels.reshape(-1, 4)
    empty = pixels[:, :3].sum(axis=1) == 0
    pixels[empty, :3] = pixels[~empty, :3].mean(axis=0)
    image.pixels.foreach_set(pixels.ravel())
    print(f"REPORT the texture is {100 - 100 * empty.mean():.0f}% used")


def paint_from(high, low):
    """Unwraps the reduced model afresh and paints the detailed model's colours onto it, at twice the size wanted and then scaled down, so that its edges are soft."""
    material = high.data.materials[0]
    for link in list(material.node_tree.links):
        if link.to_socket.name == "Metallic":  # A metal's colour doesn't count as diffuse, and would come out black.
            material.node_tree.links.remove(link)
    mt._shader(material).inputs["Metallic"].default_value = 0.0
    mt._select([low])
    low.data.uv_layers.new(name="Baked")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    unwrap(low)
    image = bpy.data.images.new("Spawner_baked", BAKED, BAKED, alpha=False)
    baked = bpy.data.materials.new("baked")
    baked.use_nodes = True
    node = baked.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    node.select = True
    baked.node_tree.nodes.active = node
    baked.node_tree.links.new(node.outputs["Color"], baked.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    low.data.materials.append(baked)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    settings = scene.render.bake
    settings.use_pass_direct = False
    settings.use_pass_indirect = False
    settings.use_pass_color = True
    settings.margin = 8
    settings.use_selected_to_active = True
    settings.cage_extrusion = REACH
    settings.max_ray_distance = REACH * 4
    bpy.ops.object.select_all(action="DESELECT")
    high.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    bpy.ops.object.bake(type="DIFFUSE")
    settings.use_selected_to_active = False
    fill_unpainted(image)
    image.scale(TEXTURE, TEXTURE)
    image.pack()


mt.start("Spawner", "Meshy/Meshy_AI_Spawner.glb")
mt.source_size()
high = mt.meshes()[0]
high.name = "High"
before = boxes(high)
thicken_tendrils(high)

low = high.copy()
low.data = high.data.copy()
low.name = "Low"
bpy.context.scene.collection.objects.link(low)
for layer in list(low.data.uv_layers):
    low.data.uv_layers.remove(layer)
low.data.materials.clear()
mt.weld(WELD, "^Low$")
low = reduce_apart(low)
paint_from(high, low)
bpy.data.objects.remove(high)
after = boxes(low)
print(f"REPORT pieces {len(before)} to {len(after)}. Sizes before: {before[:12]}. After: {after[:12]}. Smallest before: {before[-5:]}. After: {after[-5:]}")

mt.smooth(60)
ground_before = mt._bounds()[0]
mt.centre_on_ground()
shift = mt._bounds()[0] - ground_before
mt.muzzle("Muzzle_0", Vector(MAW) + shift)
mt.motion("Wobble")
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=TEXTURE)
mt.look("after_back", "back")
# How it reads from far off: the size of a unit on the game's screen.
for camera in ("quarter", "side"):
    mt.look("far_" + camera, camera, size=(160, 120), samples=32)
