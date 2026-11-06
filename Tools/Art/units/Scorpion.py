# The Scorpion, from the owner's updated model (Scorpion update.glb, M12), in place of the one of D44 and D45. It comes painted, with three
# textures (hull, turret and the track pods' covers) and already faces -Y. 68,382 triangles in 28 parts, brought to about 11,000.
# The turret's shell, the cannon and the small parts on them turn together (Turret_0). The short gun on the turret's left, beside the cannon,
# is taken as the coaxial machine gun. The road wheels are small hubs on both faces of each of the four track pods: each hub spins (Wheel_<n>),
# as the old model's wheels did. The treads themselves stay still. Numbers are in a model 10 units long, as loaded.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import bpy
import numpy as np
from mathutils import Vector

mt.start("Scorpion", "Meshy/Scorpion update.glb")
for material in bpy.data.materials:
    for socket, label in (("Base Color", "colour"), ("Emission Color", "glow")):
        image = mt._input_image(material, socket)
        if image is not None:
            print(f"REPORT {material.name} {label} {image.name}: mean {mt._pixels(image)[:, :, :3].mean():.3f}")
mt.plain_materials()
# Its paint is dark (the means above are linear), and read as near black from the game's camera: lifted by 1.35 as stored, about 1.9 in light.
# Its glow maps are empty, so they are dropped.
for material in bpy.data.materials:
    shader = mt._shader(material)
    if shader is None:
        continue
    for link in list(shader.inputs["Emission Color"].links):
        material.node_tree.links.remove(link)
    shader.inputs["Emission Color"].default_value = (0, 0, 0, 1)
    shader.inputs["Emission Strength"].default_value = 0.0
    image = mt._input_image(material, "Base Color")
    if image is not None and not image.get("lifted"):
        pixels = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
        pixels = pixels.reshape(-1, 4)
        pixels[:, :3] = np.clip(pixels[:, :3] * 1.35, 0, 1)
        image.pixels.foreach_set(pixels.ravel())
        image["lifted"] = True

TURRET = r"defaultMaterial(\.00[123]|\.02[56])?$"  # The shell, the cannon, the bits on top and the coaxial gun.
COAX = "defaultMaterial.003"
CANNON = "defaultMaterial.025"

# Where things are, read before anything is cut.
low, high = mt._bounds([bpy.data.objects[CANNON]])
cannon_tip = Vector(((low.x + high.x) / 2, low.y, (low.z + high.z) / 2))
low, high = mt._bounds([bpy.data.objects[COAX]])
coax_tip = Vector(((low.x + high.x) / 2, low.y, (low.z + high.z) / 2))
shell = bpy.data.objects["defaultMaterial.026"]
base = [shell.matrix_world @ v.co for v in shell.data.vertices]
bottom = min(p.z for p in base)
foot = [p for p in base if p.z < bottom + 0.25]
pivot = Vector(((min(p.x for p in foot) + max(p.x for p in foot)) / 2, (min(p.y for p in foot) + max(p.y for p in foot)) / 2, bottom))
print(f"REPORT cannon tip {mt._round(cannon_tip)}, coaxial tip {mt._round(coax_tip)}, turret pivot {mt._round(pivot)}")

mt.weld(0.002)
turret = mt.part("Turret_0", TURRET, pivot=pivot)

# The hubs: each is a few pieces (two discs and their rims) on one face of a pod. Pieces whose boxes touch make one hub.
o = bpy.data.objects["defaultMaterial.012"]
mesh = bmesh.new()
mesh.from_mesh(o.data)
hubs = []
for faces in mt._islands(mesh):
    points = [o.matrix_world @ v.co for f in faces for v in f.verts]
    box = [mt._low(points) - Vector((0.01, 0.01, 0.01)), mt._high(points) + Vector((0.01, 0.01, 0.01))]
    for hub in [h for h in hubs if all(h[0][i] <= box[1][i] and box[0][i] <= h[1][i] for i in range(3))]:
        hubs.remove(hub)
        box = [Vector(np.minimum(box[0], hub[0])), Vector(np.maximum(box[1], hub[1]))]
    hubs.append(box)
mesh.free()
print(f"REPORT {len(hubs)} hubs")
for number, (low, high) in enumerate(sorted(hubs, key=lambda b: (round(b[0].y, 1), b[0].x, b[0].z))):
    mt.take(f"Wheel_{number}", low, high, "defaultMaterial.012", pivot=(low + high) / 2)

mt.reduce(2300, "Turret_0")
mt.reduce(2400, r"defaultMaterial\.0(17|18|19|20)$")  # The treads, with every link modelled.
mt.reduce(1400, r"defaultMaterial\.02[1-4]$")  # The pods' covers.
mt.reduce(600, r"defaultMaterial\.01[3-6]$")  # The skirts.
mt.reduce(500, r"defaultMaterial\.011$")
mt.reduce(450, r"defaultMaterial\.007$")
mt.reduce(300, r"defaultMaterial\.009$")
mt.reduce(400, r"defaultMaterial\.006$")
mt.reduce(200, r"defaultMaterial\.00[45]$")
mt.reduce(1000, r"Wheel_")
for o in mt.meshes():
    if not o.data.polygons:
        bpy.data.objects.remove(o)
mt.smooth(35)
mt.muzzle("Muzzle_0", cannon_tip, parent="Turret_0")
mt.muzzle("Muzzle_1", coax_tip, parent="Turret_0")
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=1024)
mt.look("after_back", "back")

# A last check that the turret turns cleanly. The file is written already.
node = bpy.data.objects["Turret_0"]
node.rotation_mode = "XYZ"
node.rotation_euler[2] = 1.2
bpy.context.view_layer.update()
mt.look("after_turned", "quarter")
