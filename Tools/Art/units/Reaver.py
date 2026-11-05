# The Banished Reaver: the owner's Meshy model, 15,612 triangles and a 2048 texture, to about 8,800 and a 512 texture.
# It is a two-legged walker that already faces -Y, with six rocket tubes in each shoulder pod. The whole upper body turns (Turret_0) and
# carries all twelve tubes, and its two legs swing in turn from the hips. The body and legs are one skin, so they are cut along the line
# read from cross-sections of the model: the cut lies inside the hip cowl, where it doesn't show. Numbers are for the model at 10 units wide.
# It keeps the file's own texture layout, which is well packed: the fresh unwrap of reduce_and_repaint broke it into specks and washed out its
# red at this size. Welded first, its surface doesn't tear; the few faces that reduction leaves across two texture islands are mended here.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.geometry import barycentric_transform

# A leg seen from the side, as (y, z). Further out than OUTER_FROM the shape takes in the back of the hip cowl too; further in, that is the tail.
LEG = [(-1.6, -0.1), (-1.6, 1.6), (-0.45, 2.3), (-0.35, 3.6), (0.1, 4.2), (0.6, 5.05), (1.5, 5.46), (2.25, 4.85), (2.4, 4.0), (3.0, 3.2), (3.0, -0.1)]
LEG_OUTER = LEG[:7] + [(2.35, 5.2), (2.7, 4.6), (2.7, 4.0), (3.0, 3.2), (3.0, -0.1)]
OUTER_FROM = 1.7
HIP = (1.9, 1.5, 4.55)
TURN_POINT = (0, 1.2, 4.8)
SWING = "8_1.2"  # Degrees and cycles per second: more than this and its toes sink well into the ground.


def in_shape(shape, y, z):
    crossings = False
    for a, b in zip(shape, shape[1:] + shape[:1]):
        if (a[1] > z) != (b[1] > z) and y < a[0] + (z - a[1]) / (b[1] - a[1]) * (b[0] - a[0]):
            crossings = not crossings
    return crossings


def is_leg(point):
    x = abs(point.x)
    if x <= (0.8 if point.z < 4.2 else 1.2):
        return False
    return in_shape(LEG_OUTER if x > OUTER_FROM else LEG, point.y, point.z)


def cut_faces(source, name, chosen):
    """Moves the faces whose middle passes the test into a new part of this name."""
    copy = source.copy()
    copy.data = source.data.copy()
    copy.name = name
    bpy.context.scene.collection.objects.link(copy)
    for o, doomed in ((copy, lambda f: not chosen(f)), (source, chosen)):
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        bmesh.ops.delete(mesh, geom=[f for f in mesh.faces if doomed(f)], context="FACES_ONLY")
        bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
        mesh.to_mesh(o.data)
        mesh.free()
    return copy


def tube_tips(o):
    """The open end of each rocket tube: they are the only separate pieces up in the pods. A tube's long axis gives its two ends; the open end points forward."""
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    bmesh.ops.remove_doubles(mesh, verts=mesh.verts, dist=0.0004)
    tips = []
    for faces in mt._islands(mesh):
        points = np.array(sorted({tuple(v.co) for f in faces for v in f.verts}))
        if len(faces) > 1000 or points[:, 2].min() < 6.0:
            continue
        middle = points.mean(axis=0)
        axis = np.linalg.svd(points - middle)[2][0]
        axis = axis if axis[1] < 0 else -axis
        along = (points - middle) @ axis
        rim = points[along > along.max() - 0.12]
        tips.append(tuple(float(c) for c in (rim.max(axis=0) + rim.min(axis=0)) / 2))
    mesh.free()
    return tips


def mend_texture(o, high, reach=0.01, difference=0.1):
    """
    A reduced face whose middle shows other colours than the detailed surface under it spans two texture islands: it goes back on the island under it.
    It looks from outside along the face's normal, as the plates are thin and the nearest surface can be the far side of one. Only the middle: testing
    the corners too moved 127 faces rather than 15, and left new dark nicks.
    """
    tree, points, faces, info = high
    pixels = mt._pixels(info[0][1])
    rows, columns = pixels.shape[:2]

    def colour(uv):
        return pixels[int((uv.y % 1) * rows) % rows, int((uv.x % 1) * columns) % columns, :3]

    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    layer = mesh.loops.layers.uv.active
    mended = 0
    for face in mesh.faces:
        middle = face.calc_center_median()
        _, _, index, _ = tree.ray_cast(middle + face.normal * 0.06, -face.normal, 0.2)
        if index is None:
            _, _, index, _ = tree.find_nearest(middle)
        a, b, c = (points[i] for i in faces[index])
        uv_a, uv_b, uv_c = (uv.to_3d() for uv in info[index][0])
        right = barycentric_transform(middle, a, b, c, uv_a, uv_b, uv_c).to_2d()
        shown = sum((loop[layer].uv for loop in face.loops), Vector((0, 0))) / len(face.loops)
        if (shown - right).length < reach or np.abs(colour(shown) - colour(right)).max() < difference:
            continue
        for loop in face.loops:
            loop[layer].uv = barycentric_transform(loop.vert.co, a, b, c, uv_a, uv_b, uv_c).to_2d()
        mended += 1
    mesh.to_mesh(o.data)
    mesh.free()
    print(f"REPORT mended the texture of {mended} faces")


mt.start("Reaver", "Meshy/Meshy_AI_Reaver.glb")
mt.centre_on_ground()
tips = tube_tips(mt.meshes()[0])
print(f"REPORT {len(tips)} tubes at {[tuple(round(c, 2) for c in t) for t in tips]}")

high = mt._triangles_of(mt.meshes())
mt.weld(0.0004)
mt.reduce(8800, flat_angle=0)
mt.smooth(40)
body = mt.meshes()[0]
body.name = "Body"
mend_texture(body, high)
mt._input_image(body.data.materials[0], "Base Color").scale(512, 512)  # Now, so that the pictures show what the game gets.
mt.report("reduced")

for side, phase in ((1, 0), (-1, 50)):
    name = f"Swing_{0 if side > 0 else 1}_{phase}_{SWING}"
    cut_faces(body, name, lambda f, s=side: f.calc_center_median().x * s > 0 and is_leg(f.calc_center_median()))
    mt._set_origin(bpy.data.objects[name], (HIP[0] * side, HIP[1], HIP[2]))

mt.part("Turret_0", "^Body$", pivot=TURN_POINT)
# The left and right pods take turns, each from its top tube down.
right = sorted((t for t in tips if t[0] > 0), key=lambda t: -t[2])
left = sorted((t for t in tips if t[0] < 0), key=lambda t: -t[2])
order = [t for pair in zip(right, left) for t in pair]
for letter, tip in zip("abcdefghijkl", order):
    mt.muzzle(f"Muzzle_0{letter}", tip, parent="Turret_0")

mt.report("done")
mt.finish(texture_size=512)
