# The rocket launcher, for later: 11,795 triangles down to about 340, with a colour per face instead of a texture.
# solid() turned its round tubes and thin frame into shards, even at 300 triangles, so the whole gun is rebuilt by hand
# as plain prisms and plates laid over the source model, and takes its colours from it. Model_tools has nothing for
# that, hence the small helpers here. It came with its barrels along +X. Two tubes, one Muzzle between their mouths.
import math
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.geometry import tessellate_polygon
from mathutils.interpolate import poly_3d_calc
import model_tools as mt


def add_mesh(name, verts, faces):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    solid = bmesh.new()
    solid.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(solid, faces=solid.faces)
    solid.to_mesh(mesh)
    solid.free()
    o = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(o)
    return o


def prism(name, x, z, rings, sides=6, turn=0.0, squash=1.0, caps=(True, True)):
    """A round thing along Y. Rings are (y, radius), from one end to the other."""
    verts, faces = [], []
    for y, radius in rings:
        for i in range(sides):
            angle = math.radians(turn) + i * math.tau / sides
            verts.append((x + radius * math.cos(angle), y, z + radius * squash * math.sin(angle)))
    for r in range(len(rings) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((r * sides + i, r * sides + j, (r + 1) * sides + j, (r + 1) * sides + i))
    if caps[0]:
        faces.append(tuple(range(sides)))
    if caps[1]:
        faces.append(tuple((len(rings) - 1) * sides + i for i in range(sides)))
    return add_mesh(name, verts, faces)


def tapered_box(name, y0, a, y1, b, caps=(True, True)):
    """A box along Y, whose ends are (x low, x high, z low, z high)."""
    verts = []
    for y, (x0, x1, z0, z1) in ((y0, a), (y1, b)):
        verts += [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)]
    faces = [(i, (i + 1) % 4, (i + 1) % 4 + 4, i + 4) for i in range(4)]
    if caps[0]:
        faces.append((0, 1, 2, 3))
    if caps[1]:
        faces.append((4, 5, 6, 7))
    return add_mesh(name, verts, faces)


def slanted_bar(name, y0, y1, top, bottom, half):
    """A bar running across X and Z, between two (x, z) points."""
    d = (Vector(bottom) - Vector(top)).normalized()
    n = Vector((-d.y, d.x)) * half
    corners = [Vector(top) + n, Vector(top) - n, Vector(bottom) - n, Vector(bottom) + n]
    verts = [(c.x, y, c.y) for y in (y0, y1) for c in corners]
    faces = [(0, 1, 2, 3), (4, 5, 6, 7)] + [(i, (i + 1) % 4, (i + 1) % 4 + 4, i + 4) for i in range(4)]
    return add_mesh(name, verts, faces)


def plate(name, outline, holes, x0, x1):
    """A flat plate, cut to an outline of (y, z) points with holes in it, x0 to x1 thick."""
    loops = [outline] + holes
    flat = [p for loop in loops for p in loop]
    triangles = tessellate_polygon([[Vector((p[0], p[1], 0)) for p in loop] for loop in loops])
    verts = [(x, p[0], p[1]) for x in (x0, x1) for p in flat]
    count = len(flat)
    faces = [tuple(t) for t in triangles] + [tuple(i + count for i in t) for t in triangles]
    start = 0
    for loop in loops:
        for i in range(len(loop)):
            a, b = start + i, start + (i + 1) % len(loop)
            faces.append((a, b, b + count, a + count))
        start += len(loop)
    return add_mesh(name, verts, faces)


def px(x, y):
    """A point read off the side picture of the fitted model."""
    return ((x - 560) / 108, 1.6175 + (420 - y) / 108)


def paint_finely(o, high, grid=5):
    """As model_tools paints a weapon's faces, but reading each face at many points: these faces are big, and five points can all land on one light panel."""
    tree, points, faces, info = high
    attribute = o.data.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    for polygon in o.data.polygons:
        corners = [o.matrix_world @ o.data.vertices[v].co for v in polygon.vertices]
        samples = []
        for i in range(1, len(corners) - 1):
            a, b, c = corners[0], corners[i], corners[i + 1]
            for u in range(grid):
                for v in range(grid - u):
                    s, t = (u + 0.33) / grid, (v + 0.33) / grid
                    samples.append(a + (b - a) * s + (c - a) * t)
        total = np.zeros(3)
        for sample in samples:
            location, _, index, _ = tree.find_nearest(sample)
            uv, image, colour = info[index]
            if uv and image:
                p, q, r = (points[i] for i in faces[index])
                weights = poly_3d_calc([p, q, r], location)
                u = sum(uv[i][0] * weights[i] for i in range(3))
                v = sum(uv[i][1] * weights[i] for i in range(3))
                pixels = mt._pixels(image)
                total += pixels[int((v % 1) * pixels.shape[0]) % pixels.shape[0], int((u % 1) * pixels.shape[1]) % pixels.shape[1], :3]
            else:
                total += np.array(colour)
        result = (*(total / len(samples)), 1.0)
        for loop in polygon.loop_indices:
            attribute.data[loop].color = result


mt._paint_faces_from = paint_finely

mt.start("Weapon_RocketLauncher", "sketchfab.com/rocket_launcher.glb")
mt.remove("Icosphere")  # A stray sphere around the whole model.
mt.turn(yaw=-90)
mt.fit()
mt.remove("Object_8")  # See-through decals: lettering floating over the tubes would spoil their colours.
mt.report("fit")
mt.snapshot()
mt.remove("Object_")

# The two tubes, with their flared mouths and a dark band in front of the receiver. Six sides, smooth-shaded, look round from afar.
for name, x, z in (("TubeA", -0.24, 2.32), ("TubeB", 0.225, 1.99)):
    prism(name, x, z, [(-5.0, 0.34), (-4.4, 0.32), (-1.8, 0.29), (-1.0, 0.29)], turn=30, caps=(True, False))
    prism(name + "Back", x, z, [(1.16, 0.29), (3.0, 0.29)], turn=30, caps=(False, False))
xa, xb, za, zb, c = -0.87, 0.59, 1.4, 2.95, 0.18
corners = [(xa + c, za), (xb - c, za), (xb, za + c), (xb, zb - c), (xb - c, zb), (xa + c, zb), (xa, zb - c), (xa, za + c)]
add_mesh("Receiver", [(x, y, z) for y in (-1.0, 1.16) for x, z in corners],
         [tuple(range(8)), tuple(range(8, 16))] + [(i, (i + 1) % 8, (i + 1) % 8 + 8, i + 8) for i in range(8)])
prism("Drum", -0.01, 2.17, [(2.96, 0.67), (3.75, 0.67), (3.96, 0.67)], sides=8, turn=22.5, squash=0.93, caps=(True, False))
tapered_box("RearCap", 3.94, (-0.42, 0.42, 1.66, 2.68), 5.0, (-0.2, 0.2, 1.84, 2.47), caps=(False, True))
prism("Scope", -0.015, 3.1, [(-1.43, 0.14), (-0.5, 0.14)], turn=30, caps=(True, False))
tapered_box("Handle", -0.02, (0.41, 0.58, 2.9, 3.14), 0.74, (0.41, 0.58, 2.9, 3.14))
tapered_box("Rail", -4.4, (0.34, 0.45, 2.19, 2.3), -1.0, (0.34, 0.45, 2.19, 2.3), caps=(True, False))

# The frame under the tubes: a plate with the hand's hole in it, the rail back to the drum, and the fore-grip out to the side.
outline = [px(300, 418), px(640, 418), px(700, 440), px(665, 460), px(642, 500), px(640, 580), px(625, 597), px(545, 597),
           px(545, 585), px(452, 585), px(452, 568), px(368, 515), px(355, 462), px(300, 460)]
hole = [px(475, 460), px(545, 460), px(545, 497), px(497, 505), px(475, 485)]
plate("Frame", outline, [hole], -0.15, 0.13)
tapered_box("RailLow", 1.0, (-0.14, 0.12, 1.39, 1.6), 4.14, (-0.14, 0.12, 1.39, 1.6))
tapered_box("RailEnd", 3.14, (-0.22, 0.2, 0.98, 1.39), 4.05, (-0.22, 0.2, 0.98, 1.39))
tapered_box("GripMount", -3.25, (-0.17, 0.14, 1.25, 1.55), -2.36, (-0.17, 0.14, 1.25, 1.55), caps=(True, False))
slanted_bar("ForeGrip", -3.02, -2.64, (0.1, 1.32), (0.82, 0.68), 0.13)

mt.smooth(70, "Tube|Scope")
mt.smooth(50, "Drum")
mt.smooth(30, "Receiver|RearCap|Handle|Rail|Frame|GripMount|ForeGrip")
mt.report("kit")
mt.finish_weapon(grip=(-0.01, 0.1, 0.78), tip=(-0.01, -5.0, 2.155), length_note=1.4)
mt.look("after_back", "back")
mt.look("after_front", "front")
