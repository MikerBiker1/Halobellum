# The Banshee: 11,544 triangles to about 6,000. The source shows it parked, with its long hood hinged wide open, which
# would fly with its tail five units in the air. So the hood is swung shut around its hinge at the nose first.
import math
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

mt.start("Banshee", "sketchfab.com/halo_banshee.glb")

HINGE = Vector((0, -3.5, -0.5))
CLOSE = 37  # Degrees, judged from side pictures: its underside then rests on the body.


def is_hood(low, high):
    centre = (low + high) / 2
    if centre.z > 4:
        return True  # The tip of its tail
    if abs(centre.x) > 1.4 or low.y > 1.7:
        return False  # Wings, cannons, the two rear pods
    if high.z > 1.1:
        return True  # The shell
    if centre.y < -3.9:
        return True  # The grey crest on its front
    return abs(centre.x) > 0.9 and low.z > 0.75 and centre.y < -3.1  # The rivets on its sides


def split(o, name, faces):
    """Moves these faces of a part into a new part of that name."""
    copy = o.copy()
    copy.data = o.data.copy()
    copy.name = name
    bpy.context.scene.collection.objects.link(copy)
    for target, drop in ((o, faces), (copy, {p.index for p in o.data.polygons} - faces)):
        mesh = bmesh.new()
        mesh.from_mesh(target.data)
        mesh.faces.ensure_lookup_table()
        bmesh.ops.delete(mesh, geom=[mesh.faces[i] for i in drop], context="FACES_ONLY")
        bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
        mesh.to_mesh(target.data)
        mesh.free()
    return copy


body = mt.meshes("Banshee_Banshee")[0]
mesh = bmesh.new()
mesh.from_mesh(body.data)
hood_faces = set()
for faces in mt._islands(mesh):
    points = [v.co for f in faces for v in f.verts]
    if is_hood(mt._low(points), mt._high(points)):
        hood_faces.update(f.index for f in faces)
mesh.free()
hood = split(body, "Hood", hood_faces)
hood.data.transform(Matrix.Translation(HINGE) @ Matrix.Rotation(math.radians(-CLOSE), 4, "X") @ Matrix.Translation(-HINGE))
hood.data.update()
mt.report("hood closed")

# The source is a specular-gloss material. Its Base Color is Image_0, a glow map that is black but for the lights; its
# real purple and maroon is Image_1, plugged into Specular Tint, which Unity doesn't use. So Image_1 becomes the colour,
# and Image_0 stays as the emission so that the lights glow. Without its normal and gloss maps it looks as Unity will show it.
material = bpy.data.materials["Banshee"]
shader = mt._shader(material)
colour = next(n for n in material.node_tree.nodes if n.type == "TEX_IMAGE" and n.image.name == "Image_1")
for socket in ("Base Color", "Specular Tint", "Roughness", "Normal"):
    for link in list(shader.inputs[socket].links):
        material.node_tree.links.remove(link)
material.node_tree.links.new(colour.outputs["Color"], shader.inputs["Base Color"])
colour.image.alpha_mode = "NONE"  # Its alpha holds its gloss, which an opaque material has no use for: this saves it opaque.
shader.inputs["IOR"].default_value = 1.5  # The source has 1000, a perfect mirror, which would wash out the pictures.
shader.inputs["Roughness"].default_value = 0.85
shader.inputs["Specular IOR Level"].default_value = 0.2
# The headlights use the same textures, so they can share the one material.
for o in mt.meshes("Headlights"):
    o.data.materials.clear()
    o.data.materials.append(material)
    o.name = "Banshee_Lights"

# The two plasma cannons on the wing roots: the rings at the ends of their barrels, before fit moves everything.
before = mt._bounds()[0]
mt.fit()
shift = mt._bounds()[0] - before
mt.report("fitted")
tips = [Vector((x, -2.416, 0.118)) + shift for x in (-1.958, 1.958)]

mt.weld(0.002)
wing_pods = mt.take("WingPods", (-9, -9, -9), (-4, 9, 9), "Banshee_Banshee")
other_pod = mt.take("WingPods2", (4, -9, -9), (9, 9, 9), "Banshee_Banshee")
guns_left = mt.take("Guns", (-2.3, -2.6 + shift.y, -9), (-1.5, -1.5 + shift.y, 9), "Banshee_Banshee")
guns_right = mt.take("Guns2", (1.5, -2.6 + shift.y, -9), (2.3, -1.5 + shift.y, 9), "Banshee_Banshee")
mt.report("grouped")


def turn_right_side_out(pattern):
    """Reverses the closed pieces that are inside out. The source's material is double-sided, which hides them; Unity culls them."""
    for o in mt.meshes(pattern):
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        mesh.normal_update()
        for faces in mt._islands(mesh):
            area = sum(f.calc_area() for f in faces)
            facing = sum((f.normal * f.calc_area() for f in faces), Vector())
            volume = sum(f.calc_center_median().dot(f.normal) * f.calc_area() for f in faces) / 3
            if facing.length < 0.02 * area and volume < 0:
                bmesh.ops.reverse_faces(mesh, faces=faces)
                print(f"REPORT turned right side out in {o.name}: {len(faces)} faces, area {area:.3f}")
        mesh.to_mesh(o.data)
        mesh.free()
        o.data.update()


# The round discs on the wing roots, under the plasma cannons.
turn_right_side_out("Guns")

mt.reduce(700, "WingPods")
mt.reduce(340, "Guns")
mt.reduce(1500, "Hood")
mt.reduce(3400, "Banshee_Banshee")
mt.smooth(45)
mt.report("reduced")

mt.muzzle("Muzzle_0a", tips[0] + Vector((0, -0.02, 0)))
mt.muzzle("Muzzle_0b", tips[1] + Vector((0, -0.02, 0)))

# The fuel rod gun fires from the front of the nose, on the centre line.
bpy.context.view_layer.update()
height = -0.2 + shift.z
hit, at, _, _, _, _ = bpy.context.scene.ray_cast(bpy.context.evaluated_depsgraph_get(), (0, -9, height), (0, 1, 0))
print("REPORT nose hit", hit, at)
mt.muzzle("Muzzle_1", (0, at.y - 0.1, height))

mt.merge_rest()
mt.finish()
mt.look("after_back", "back")

# What the game gets, read back from the file: this should be purple, not the near-black of the source's Base Color.
image = bpy.data.images.load(os.path.join(mt._state["folder"], "Banshee_0.png"))
pixels = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
image.pixels.foreach_get(pixels)
print("REPORT Banshee_0.png mean", pixels.reshape(-1, 4)[:, :3].mean(axis=0).round(3).tolist(), "alpha mean", round(float(pixels[3::4].mean()), 3))
