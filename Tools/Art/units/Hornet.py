# The Hornet: 19,240 triangles to about 8,000, most of it saved on four handrails of 2,306 triangles each.
# The source has one texture atlas whose glass areas are marked by alpha 0.22 in the texture. That alpha is what made
# the glass look speckled, and with one material for everything it would also turn the whole model into a cutout in
# Unity. So the faces over those areas become their own plain tinted glass material, and the rest is opaque.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import bpy
import numpy as np
from mathutils import Vector

mt.start("Hornet", "sketchfab.com/halo_downfall_-_hornet.glb")
mt.fit()
mt.report("fitted")


def split_glass():
    """Moves every face whose texture alpha is low into its own part with a plain glass material."""
    image = bpy.data.images["Image_0"]
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    alpha = pixels.reshape(height, width, 4)[:, :, 3]
    glass = bpy.data.materials.new("hornet_glass")
    glass.use_nodes = True
    shader = glass.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (0.05, 0.13, 0.19, 1)
    shader.inputs["Roughness"].default_value = 0.15
    mt.flag("hornet_glass", glass=True)
    for o in list(mt.meshes()):
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        layer = mesh.loops.layers.uv.active
        doomed = []
        for face in mesh.faces:
            u = sum(l[layer].uv.x for l in face.loops) / len(face.loops)
            v = sum(l[layer].uv.y for l in face.loops) / len(face.loops)
            if alpha[int((v % 1) * height) % height, int((u % 1) * width) % width] < 0.5:
                doomed.append(face.index)
        mesh.free()
        if not doomed:
            continue
        copy = o.copy()
        copy.data = o.data.copy()
        copy.name = "Glass_" + o.name
        bpy.context.scene.collection.objects.link(copy)
        for target, drop in ((o, set(doomed)), (copy, {f.index for f in o.data.polygons} - set(doomed))):
            mesh = bmesh.new()
            mesh.from_mesh(target.data)
            mesh.faces.ensure_lookup_table()
            bmesh.ops.delete(mesh, geom=[mesh.faces[i] for i in drop], context="FACES_ONLY")
            bmesh.ops.delete(mesh, geom=[x for x in mesh.verts if not x.link_faces], context="VERTS")
            mesh.to_mesh(target.data)
            mesh.free()
        copy.data.materials.clear()
        copy.data.materials.append(glass)
        print(f"REPORT glass from {o.name}: {len(doomed)} faces")


def clean_glass(o):
    """
    Each canopy pane is modelled as four thin shells less than 0.002 apart: outward, inward, outward, inward. Welding
    would fuse them into a jumble of opposite triangles, and Unity culls the reversed ones, leaving holes. So only the
    outermost shell (facing out) and the innermost (facing in, for a look through the open cockpit) are kept, each with
    all its faces turned the same way. These parts must not be welded or reduced afterwards.
    """
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    mesh.normal_update()
    shells = mt._islands(mesh)
    points = [v.co for faces in shells for f in faces for v in f.verts]
    low, high = mt._low(points), mt._high(points)
    inside = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z - 0.4))  # Under the canopy

    # Shells over the same part of the pane lie on top of each other, so they have about the same middle.
    stacks = []
    for faces in shells:
        area = sum(f.calc_area() for f in faces)
        middle = sum((f.calc_center_median() * f.calc_area() for f in faces), Vector()) / area
        for stack in stacks:
            if (stack[0][1] - middle).length < 0.05:
                stack.append((faces, middle))
                break
        else:
            stacks.append([(faces, middle)])

    doomed = []
    for stack in stacks:
        outward = (stack[0][1] - inside).normalized()
        stack.sort(key=lambda shell: shell[1].dot(outward))
        for faces, _ in stack[1:-1]:
            doomed.extend(faces)
        for (faces, _), wanted in ((stack[-1], 1), (stack[0], -1)):
            bmesh.ops.recalc_face_normals(mesh, faces=faces)
            facing = sum(f.normal.dot(f.calc_center_median() - inside) * f.calc_area() for f in faces)
            if facing * wanted < 0:
                bmesh.ops.reverse_faces(mesh, faces=faces)
    print(f"REPORT glass {o.name}: {len(shells)} shells in {len(stacks)} stacks, {len(mesh.faces) - len(doomed)} faces kept")
    bmesh.ops.delete(mesh, geom=doomed, context="FACES_ONLY")
    bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
    mesh.to_mesh(o.data)
    mesh.free()
    o.data.update()


split_glass()
for part in mt.meshes("^Glass_"):
    clean_glass(part)
body = bpy.data.materials["1001"]
shader = mt._shader(body)
for link in list(shader.inputs["Alpha"].links):
    body.node_tree.links.remove(link)
shader.inputs["Alpha"].default_value = 1.0
body.blend_method = "OPAQUE"
mt.flag("1001", cutout=False)

# The glass parts ("Glass_...") stay out of these: see clean_glass.
mt.weld(0.003, "^defaultMaterial")
mt.reduce(480, r"^defaultMaterial\.0(08|09|10|11)$")  # Handrails
mt.reduce(300, r"^defaultMaterial\.01[23]$")  # Struts
mt.reduce(200, r"^defaultMaterial\.00[47]$")  # Small grey fittings
mt.reduce(500, r"^defaultMaterial\.003$")
mt.reduce(400, r"^defaultMaterial\.001$")
mt.reduce(360, r"^defaultMaterial\.020$")
mt.report("reduced")
mt.smooth(35)

# Rockets from the two grey pods under the skids, at their front faces.
pods = [(-1.518, -2.42, 0.16), (1.515, -2.42, 0.16)]
mt.muzzle("Muzzle_0a", pods[0])
mt.muzzle("Muzzle_0b", pods[1])

# Bombs from under the belly, on the centre line, just below the lowest skin there.
bpy.context.view_layer.update()
hit, at, _, _, _, _ = bpy.context.scene.ray_cast(bpy.context.evaluated_depsgraph_get(), (0, -0.6, -1), (0, 0, 1))
print("REPORT belly panel hit", hit, at)
mt.muzzle("Muzzle_1", (0, -0.6, at.z - 0.08))

mt.merge_rest()
mt.finish()
