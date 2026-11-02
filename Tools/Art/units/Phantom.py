# The Phantom: 10,846 triangles to about 8,000. The Infected Phantom uses this same model, tinted.
# Its textures average about 1.5% brightness, and only look navy in a render because its metal mirrors the sky. Unity shows
# models matte, where that is black, and a tint multiplied onto black stays black. So its textures are lifted here.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bpy
import numpy as np
from mathutils import Vector

LIFT = 0.65  # Each sRGB value is raised to this power: dark values rise most, and hues stay.

mt.start("Phantom", "sketchfab.com/space_phantom_hr.glb")

# The volumes of see-through blue light under its belly are an effect, and can't be shown by a solid material.
mt.remove_box((-2.1, -2, -2), (1.5, 2, 1), "Object_2")
# Hundreds of tiny light quads, nearly all black in the texture.
mt.remove("Object_6")
mt.report("cleaned")

# The two glows left, the panels by the rear engines and the rings under the belly, become one plain glowing colour.
# The panels fade out in the source, so without blending only their brightest part stays: the third nearest the hull.
for o in mt.meshes("Object_2"):
    for v in o.data.vertices:
        v.co.x = -1.997 + (v.co.x + 1.997) * 0.35
mt.paint("Object_2|Object_4", (0.0, 0.42, 0.55), "phantom_glow")
mt.flag("phantom_glow", glow=(0.0, 0.42, 0.55))
glow = mt._shader(bpy.data.materials["phantom_glow"])
glow.inputs["Emission Color"].default_value = (0.0, 0.42, 0.55, 1)
glow.inputs["Emission Strength"].default_value = 1.0

# The two kinds of "parts" use the same texture, so they can share one material.
for o in mt.meshes("Object_8"):
    o.data.materials.clear()
    o.data.materials.append(bpy.data.materials["space_phantom_parts_metal"])

for name in ("Image_2", "Image_3", "Image_6"):
    image = bpy.data.images[name]
    pixels = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    pixels = pixels.reshape(-1, 4)
    print("REPORT", name, "mean before", pixels[:, :3].mean(axis=0).round(3).tolist())
    pixels[:, :3] = np.power(pixels[:, :3], LIFT)
    print("REPORT", name, "mean after", pixels[:, :3].mean(axis=0).round(3).tolist())
    image.pixels.foreach_set(pixels.ravel())
    image.update()

# The pictures should show what Unity will: matte, with no normal maps.
for material in bpy.data.materials:
    shader = mt._shader(material)
    if shader is not None and material.name != "phantom_glow":
        shader.inputs["Metallic"].default_value = 0
        shader.inputs["Roughness"].default_value = 0.85
        shader.inputs["Specular IOR Level"].default_value = 0.2
        for link in list(shader.inputs["Metallic"].links) + list(shader.inputs["Roughness"].links) + list(shader.inputs["Normal"].links):
            material.node_tree.links.remove(link)

mt.turn(yaw=-90)  # It came with its nose along +X.
before = mt._bounds()[0]
mt.fit()
shift = mt._bounds()[0] - before
mt.report("turned")

mt.weld(0.002)
mt.reduce(2700, "Object_5")
mt.reduce(2600, "Object_3")
mt.reduce(2350, "Object_7|Object_8")
mt.reduce(100, "Object_4")
mt.smooth(40)
mt.report("reduced")

# The plasma turrets are the two grey cones under the sides of its nose, sitting in recesses of the hull: they stay fixed,
# because turning they would pass through it. Their tips, in the source's coordinates, are x 3.415, y +-1.735, z 0.468.
for letter, side in (("a", -1), ("b", 1)):
    mt.muzzle("Muzzle_0" + letter, Vector((side * 1.735, -3.435, 0.468)) + shift)

mt.merge_rest()
mt.finish()
mt.look("after_back", "back")

# finish() saves through image.copy(), which goes back to the file packed in the source and so loses the lift above.
# The lift above is only what the pictures show; this is the one the game gets. It reads the PNGs back to be sure.
for name in os.listdir(mt._state["folder"]):
    if not name.endswith(".png"):
        continue
    path = os.path.join(mt._state["folder"], name)
    image = bpy.data.images.load(path)
    pixels = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    pixels = pixels.reshape(-1, 4)
    pixels[:, :3] = np.power(pixels[:, :3], LIFT)
    image.pixels.foreach_set(pixels.ravel())
    image.update()
    image.save()
    bpy.data.images.remove(image)
    image = bpy.data.images.load(path)
    image.pixels.foreach_get(pixels.ravel())
    print("REPORT", name, "saved, mean", pixels[:, :3].mean(axis=0).round(3).tolist())
    bpy.data.images.remove(image)
