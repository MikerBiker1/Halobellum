# The Grunt's plasma pistol: 4,124 triangles down to about 230, with a colour per face instead of a texture.
# It already faces -Y: the open jaws are its front. The see-through display at the back is taken off before cutting.
# Model_tools reads a face's colour at five points, which let the small yellow inserts tint whole black panels brown,
# so this reads more points.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import numpy as np
from mathutils.interpolate import poly_3d_calc
import model_tools as mt


def paint_finely(o, high, grid=5):
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

mt.start("Weapon_PlasmaPistol", "sketchfab.com/halo_-_plasma_pistol.glb")
mt.fit()
mt.remove_box((-1.2, 3.55, 5.9), (1.2, 5.1, 7.6))  # The hologram display: thin shells that would turn into a lump.
mt.report("no display")
mt.snapshot()
mt.solid(230, voxel=0.05)
mt.smooth(30)
mt.report("solid")
mt.finish_weapon(grip=(0, -0.2, 4.3), tip=(0, -4.85, 4.85), length_note=0.45)
mt.look("after_back", "back")
mt.look("after_front", "front")
