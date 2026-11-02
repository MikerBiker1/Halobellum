# The Pelican: kept at its full detail, with what cannot be seen taken away. Its rig has no animations, so it stays in
# its rest pose: landing legs down and the rear ramp lowered. The Infected Pelican uses this same model, tinted.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import bpy
from mathutils import Vector

mt.start("Pelican", "sketchfab.com/halo_2_d77-tc_pelican.glb")
mt.remove("Icosphere")  # A rigging helper at the origin, with no material.
mt.remove("Object_94")  # The cargo net on the bay's ceiling: inside the hull, and its texture alpha would cut it away in Unity anyway.
mt.report("cleaned")

# Parts that use the same texture can share one material, which saves draw calls.
hull = bpy.data.materials["pelican_hull"]
bay = bpy.data.materials["pelican_bay"]
for pattern, material in (("Object_96", hull), ("Object_91|Object_93", bay)):
    for o in mt.meshes(pattern):
        o.data.materials.clear()
        o.data.materials.append(material)

# The windows: a blended texture in the source, which only speckles. Plain tinted glass reads better from far away.
mt.paint("Object_97", (0.03, 0.08, 0.12), "pelican_tinted_glass", glass=True)
mt._shader(bpy.data.materials["pelican_tinted_glass"]).inputs["Roughness"].default_value = 0.15

mt.turn(yaw=-90)  # It came with its nose along +X.
mt.fit()
mt.report("turned")

# The windows come twice, face to face, one copy looking out and one looking in, and the two fight in a render.
# Keep the copy that looks out. Everything that looks out faces forward or up.
for o in mt.meshes("Object_97"):
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    mesh.normal_update()
    inward = [f for f in mesh.faces if f.normal.dot(Vector((0, -1, 0.7))) < 0]
    bmesh.ops.delete(mesh, geom=inward, context="FACES_ONLY")
    bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
    mesh.to_mesh(o.data)
    mesh.free()
    print(f"REPORT windows: dropped {len(inward)} faces that look inward, {len(o.data.polygons)} left")

# The small lights are quads of the hull's atlas showing a coloured dot on black, blended in the source. Without blending
# the black shows as a square, so shrink each quad to the dot and pin its UVs to the dot's colour.
for o in mt.meshes("Object_96"):
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    layer = mesh.loops.layers.uv.active
    for faces in mt._islands(mesh):
        verts = {v for f in faces for v in f.verts}
        loops = [l for f in faces for l in f.loops]
        centre = sum((v.co for v in verts), Vector()) / len(verts)
        uv = sum((l[layer].uv for l in loops), Vector((0, 0))) / len(loops)
        for v in verts:
            v.co = centre.lerp(v.co, 0.5)
        for l in loops:
            l[layer].uv = uv.lerp(l[layer].uv, 0.05)
    mesh.to_mesh(o.data)
    mesh.free()

# The end of the autocannon's barrel under the chin: the farthest forward of its vertices, at the middle of the barrel's height.
points = [v.co for o in mt.meshes("Object_90") for v in o.data.vertices if abs(v.co.x) < 0.02 and v.co.y < -3.5]
tip = min((p for p in points if p.z < 0.40), key=lambda p: p.y)
print("REPORT barrel tip", tip)
mt.muzzle("Muzzle_0", (0, tip.y - 0.02, tip.z + 0.016))

mt.weld(0.002, "Object_90")
mt.smooth(40)
mt.merge_rest()
mt.finish()
mt.look("after_back", "back")
