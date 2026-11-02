# The Grunt's fuel rod gun: 8,264 triangles down to about 330, with a colour per face instead of a texture.
# It came with its mouth along -X. Its body is thin open panels, which solid() turns into a shredded double skin, so the
# whole gun is rebuilt by hand as a few smooth lofted pieces laid over the source, and takes its colours from it. The
# green rods are real prisms, so they stay visible. Model_tools has nothing for building shapes, nor for reading colours
# more finely than five points a face, hence the helpers here.
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


def px(x, y):
    """A point read off the side picture of the fitted model, as (y, z)."""
    return ((x - 560) / 105, 2.24 + (420 - y) / 105)


def loft(name, stations, sides=6):
    """A long body along Y. Stations are (picture x, picture top, picture bottom, half width), from front to back."""
    verts, faces = [], []
    for x, top, bottom, width in stations:
        y, z1 = px(x, top)
        z0 = px(x, bottom)[1]
        if sides == 6:
            a, b = z1 - 0.3 * (z1 - z0), z0 + 0.3 * (z1 - z0)
            ring = [(0, z1), (width, a), (width, b), (0, z0), (-width, b), (-width, a)]
        else:
            ring = [(0, z1), (width, (z0 + z1) / 2), (0, z0), (-width, (z0 + z1) / 2)]
        verts += [(cx, y, cz) for cx, cz in ring]
    for r in range(len(stations) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((r * sides + i, r * sides + j, (r + 1) * sides + j, (r + 1) * sides + i))
    faces.append(tuple(range(sides)))
    faces.append(tuple((len(stations) - 1) * sides + i for i in range(sides)))
    return add_mesh(name, verts, faces)


def plate(name, outline, holes, x0, x1):
    """A flat plate, cut to an outline with holes in it, x0 to x1 thick. Points are read off the side picture."""
    loops = [[px(*p) for p in loop] for loop in [outline] + holes]
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


def rod(name, z):
    verts = [(0.125 * math.cos(math.radians(30 + 60 * i)), y, z + 0.125 * math.sin(math.radians(30 + 60 * i))) for y in (1.95, 4.0) for i in range(6)]
    faces = [tuple(range(6)), tuple(range(6, 12))] + [(i, (i + 1) % 6, (i + 1) % 6 + 6, i + 6) for i in range(6)]
    return add_mesh(name, verts, faces)


def paint_finely(o, high, grid=5):
    """As model_tools paints a weapon's faces, but reading each face at many points, so that a small bright patch can't tint a whole face."""
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

mt.start("Weapon_FuelRod", "sketchfab.com/halo_fangame_fuelrod.glb")
mt.turn(yaw=90)
mt.fit()
mt.report("fit")
mt.snapshot()
mt.remove(".")

loft("Upper", [(36, 345, 365, 0.25), (180, 325, 372, 0.3), (340, 275, 380, 0.42), (470, 225, 395, 0.6), (600, 203, 415, 0.72),
               (700, 222, 405, 0.72), (960, 263, 385, 0.5), (1040, 300, 385, 0.35), (1083, 358, 385, 0.1)])
loft("Fin", [(52, 418, 435, 0.2), (270, 428, 480, 0.3), (400, 465, 555, 0.3), (530, 540, 625, 0.28), (690, 625, 650, 0.2)], sides=4)
loft("Swoop", [(668, 575, 582, 0.2), (715, 490, 632, 0.3), (800, 400, 545, 0.4), (900, 390, 432, 0.45), (1075, 385, 395, 0.4)], sides=4)
plate("Spine", [(36, 372), (1060, 372), (1083, 392), (790, 400), (720, 440), (700, 500), (668, 575), (530, 560), (400, 480), (270, 430), (36, 412)],
      [[(480, 480), (565, 465), (530, 530), (490, 528)], [(620, 455), (695, 455), (680, 520), (640, 540)]], -0.28, 0.28)
for i, z in enumerate((4.325, 4.055, 3.785)):
    rod(f"Rod{i}", z)
band = [(x, y, z) for y in (3.5, 3.7) for x, z in ((-0.15, 3.65), (0.15, 3.65), (0.15, 4.48), (-0.15, 4.48))]
add_mesh("Band", band, [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)])

mt.smooth(70, "Rod|Upper|Fin|Swoop")
mt.smooth(30, "Spine|Band")
mt.report("kit")
mt.finish_weapon(grip=(0, 1.55, 1.2), tip=(0, -4.95, 2.48), length_note=1.4)
mt.look("after_back", "back")
mt.look("after_front", "front")
