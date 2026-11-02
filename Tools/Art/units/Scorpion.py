# The Scorpion: the owner's painted model. Its parts come shattered into thousands of loose faces, so it is welded first.
# The whole upper turret and cannon turn together (Turret_0). The rod on the turret's right side is taken as the coaxial machine gun.
# The gunner's gun on the hull front stays fixed, because it does not sit on the turret. Unity could make it a Turret_1 later.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import bpy
from mathutils import Vector

mt.start("Scorpion", "Scorpion/scorpion_painted.glb")

# The old look was too dark, and 2.5 times brighter came out lime green in the game. A little brighter, with less blue, stays olive.
for material in bpy.data.materials:
    shader = mt._shader(material)
    if shader is None or material.name.startswith(("marker", "tread")):
        continue
    r, g, b = shader.inputs["Base Color"].default_value[:3]
    shader.inputs["Base Color"].default_value = (r * 1.3, g * 1.2, b * 0.9, 1)

# Parts painted alike can share one material, which means fewer draw calls.
same = {"scorpion_wheels_left": "scorpion_wheels", "scorpion_wheels_right": "scorpion_wheels", "tread_right": "tread_left",
        "scorpion_turret_glass": "scorpion_glass", "scorpion_machinegun_heat": "scorpion_turrets_nodetail"}
for o in mt.meshes():
    if o.data.materials[0].name in same:
        o.data.materials[0] = bpy.data.materials[same[o.data.materials[0].name]]
    while o.data.uv_layers:  # Flat paint, no textures: UVs only get in the way of reducing.
        o.data.uv_layers.remove(o.data.uv_layers[0])
mt.flag("scorpion_glass", glass=True)

mt.weld(0.003)
before = mt._bounds()[0].copy()
mt.fit()
shift = mt._bounds()[0] - before
def at(x, y, z):
    """Coordinates measured on the model as loaded, moved to where fit() put it."""
    return Vector((x, y, z)) + shift

# What sits inside the cockpit can't be seen.
mt.remove_box(at(0.1, -0.35, 1.3), at(0.9, 0.6, 2.05), "Object_9")

# Every wheel is a separate piece, so every wheel can spin. The eight big end wheels bring a hub with them.
def boxes(pattern, smallest):
    found = []
    for o in mt.meshes(pattern):
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        for faces in mt._islands(mesh):
            if len(faces) >= smallest:
                points = [o.matrix_world @ v.co for f in faces for v in f.verts]
                found.append((mt._low(points), mt._high(points)))
        mesh.free()
    return sorted(found, key=lambda b: (b[0].y, b[0].x))

number = 0
for low, high in boxes("Object_1[68]", 100) + boxes("Object_1[45]", 500):
    mt.take(f"Wheel_{number}", low, high, "Object_1[4-8]", pivot=(low + high) / 2)
    number += 1
print(f"REPORT {number} wheels")

# Find the coaxial gun's tip before the turret is reduced.
rod_low, rod_high = at(-1.34, 0.08, 3.49), at(-1.23, 0.86, 3.61)
tip = []
for o in mt.meshes("Object_8"):
    for v in o.data.vertices:
        if mt._inside(v.co, rod_low, rod_high) and v.co.y < rod_low.y + 0.03:
            tip.append(v.co.copy())
coax = sum(tip, Vector()) / len(tip)
cannon = (at(0.0065, -4.112, 3.478))
print(f"REPORT coaxial tip {coax} from {len(tip)} vertices")

turret = mt.take("Turret_0", at(-2, -5, 2.55), at(2, 5, 4.5), "Object_8", pivot=at(0, 2.145, 2.2))

mt.reduce(2400, "Turret_0")
mt.reduce(960, "Wheel_(2[0-9]|3[0-9])$")
mt.reduce(880, r"Wheel_(\d|1\d)$")
mt.reduce(800, "Object_1[12]")
mt.reduce(450, "Object_13|Object_5")
mt.reduce(120, "Object_10|Object_4")
mt.reduce(4500, "Object_6|Object_7|Object_8|Object_9")
mt.smooth(35)

mt.muzzle("Muzzle_0", cannon, parent="Turret_0")
mt.muzzle("Muzzle_1", coax, parent="Turret_0")
for o in mt.meshes():
    if not o.data.polygons:
        bpy.data.objects.remove(o)
mt.merge_rest()
mt.report("done")
mt.finish()
mt.look("after_back", "back")

# A last check that the turret turns cleanly. The file is written already.
node = bpy.data.objects["Turret_0"]
node.rotation_mode = "XYZ"
node.rotation_euler[2] = 1.2
bpy.context.view_layer.update()
mt.look("after_turned", "quarter")
