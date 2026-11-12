# The Zealot's energy sword (D131): the owner's twinblade, Meshy/Meshy_AI_Twinblade_of_the_Azur_1112012036_texture.glb, in place of the
# Sketchfab sword (D71), whose blades were a few thin triangles. It lies along X, its two blades toward -X one above the other, the hilt at +X;
# so it is turned to face -Y, the blades still one above the other. Of its 10,479 triangles the blades are kept whole, as the owner asked, and
# only the hilt is reduced. A colour per face instead of a texture. The blades are the light cyan faces of its texture; they are marked to
# glow (model_tools.finish_weapon's glow), and the Zealot's body (weaponGlows) draws them as a white core in a cyan halo. The hand takes the
# middle of the hilt; its "muzzle" is between the two blade points.
#   blender --background --factory-startup --python Tools/Art/units/Weapon_EnergySword.py
import os
import sys

import bmesh
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt  # noqa: E402

mt.start("Weapon_EnergySword", "Meshy/Meshy_AI_Twinblade_of_the_Azur_1112012036_texture.glb")
whole = mt.meshes()[0]

# Each face's colour from the texture, to tell the blades (light cyan) from the hilt (dark and deep blue, silver).
mt._paint_faces_from(whole, mt._triangles_of([whole]))
colours = whole.data.color_attributes["Col"]
blade = []
for polygon in whole.data.polygons:
    r, g, b = (sum(colours.data[loop].color[i] for loop in polygon.loop_indices) / len(polygon.loop_indices) for i in range(3))
    blade.append(g > 0.3 and g - r > 0.12 and b - r > 0.12)  # Linear colour: light cyan, not blue, not grey.
whole.data.color_attributes.remove(colours)
print(f"REPORT blades {sum(blade)} of {len(blade)} faces")


def keep(o, wanted):
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    mesh.faces.ensure_lookup_table()
    bmesh.ops.delete(mesh, geom=[f for f in mesh.faces if blade[f.index] != wanted], context="FACES")
    mesh.to_mesh(o.data)
    mesh.free()


hilt = whole.copy()
hilt.data = whole.data.copy()
whole.users_collection[0].objects.link(hilt)
keep(whole, True)
keep(hilt, False)
whole.name, hilt.name = "Blade", "Hilt"

points = np.array([hilt.matrix_world @ v.co for v in hilt.data.vertices])
low, high = points.min(axis=0), points.max(axis=0)
grip_file = (low + high) / 2  # The middle of the hilt, where the hand closes.
tips = np.array([whole.matrix_world @ v.co for v in whole.data.vertices])
tip_file = tips[tips[:, 0] < tips[:, 0].min() + 0.3].mean(axis=0)
tip_file[2] = grip_file[2]  # Between the two points, in line with the hand.
print(f"REPORT hilt from {np.round(low, 2)} to {np.round(high, 2)}; grip {np.round(grip_file, 3)}, tip {np.round(tip_file, 3)}")

# Turned to face -Y (yaw 90: what was at -X goes to -Y), then fit(): the two points read above go the same way.
before_low, before_high = mt._bounds()
mt.turn(yaw=90)
mt.fit()
after_low, after_high = mt._bounds()
s = (after_high.y - after_low.y) / (before_high.x - before_low.x)
turned_low = Vector((-before_high.y, before_low.x, before_low.z))


def moved(p):
    x, y, z = (float(v) for v in p)
    return (Vector((-y, x, z)) - turned_low) * s + after_low


grip, tip = moved(grip_file), moved(tip_file)
mt.report("fitted")
mt.snapshot()
mt.weld(0.002, pattern="^Hilt$")
mt.reduce(900, pattern="^Hilt$", flat_angle=2.0)
mt.smooth(30)
mt.report("hilt reduced")
mt.finish_weapon(grip=grip, tip=tip, length_note=1.4, glow="^Blade$")
mt.look("after_front", "front")
