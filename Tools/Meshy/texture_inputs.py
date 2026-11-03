# texture_inputs.py
# Prepares models that came with no colours at all, for Meshy to paint: reduced, facing -Y (+Z in glTF), in metres.
# Writes Art_Source/Prepared/_Paint/<name>/input.glb. meshy_paint.py sends it, and Tools/Art/units/<unit>.py picks up what comes back.
#   blender --background --factory-startup --python Tools/Meshy/texture_inputs.py
# The Rhino's file is two parts for a 3D printer, laid out flat: the turret stands 94 units off to the side of its ring, facing the
# same way as the hull. Its front is +X.
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "Art"))
import model_tools as mt

mt.PREPARED = os.path.join(mt.PREPARED, "_Paint")
RHINO = "sketchfab.com/1-100-unsc-rhino/"
TURRET_OFFSET_Y = 94.0
RING = (43.45, -53.27)
METRES = 9.5 / 177.0  # The hull's 177 units become 9.5 m. It only has to be the same for both parts.


def export(name):
    path = os.path.join(mt._state["folder"], "input.glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_yup=True, export_apply=True, export_animations=False,
                              export_skins=False, export_materials="NONE", export_cameras=False, export_lights=False)
    print(f"REPORT wrote {path}, {os.path.getsize(path)} bytes, {mt.tris()} triangles")


def stl_part(name, file, target, voxel):
    mt.start(name, RHINO + file)
    mt.source_size()
    # Its triangles are very uneven, and reducing those crushes the shape. An even skin reduces cleanly.
    o = mt.meshes()[0]
    mt._select([o])
    remesh = o.modifiers.new("skin", "REMESH")
    remesh.mode = "VOXEL"
    remesh.voxel_size = voxel
    remesh.use_smooth_shade = False
    bpy.ops.object.modifier_apply(modifier="skin")
    mt.report("even skin")
    mt.reduce(target, flat_angle=0)
    mt.turn(yaw=-90)


# The hull. Its ring's floor sets how high the turret sits.
stl_part("RhinoHull", "rhino_hull.stl", 14000, 0.4)
o = mt.meshes()[0]
mt.report("hull")
ring_x, ring_y = RING[1], -RING[0]  # After the turn.
o.data.calc_loop_triangles()
tree = BVHTree.FromPolygons([v.co for v in o.data.vertices], [t.vertices for t in o.data.loop_triangles])
hit = tree.ray_cast(Vector((ring_x, ring_y, 100)), Vector((0, 0, -1)))
floor = hit[0].z if hit[0] else 14.0
print(f"REPORT ring at {ring_x}, {ring_y}, floor {floor}")
mt.scale(METRES)
export("RhinoHull")

stl_part("RhinoTurret", "rhino_turret.stl", 6000, 0.25)
# After the turn, the file's y became x, so the turret is moved along x.
for piece in mt.meshes():
    piece.data.transform(Matrix.Translation(Vector((TURRET_OFFSET_Y, 0, floor))))
    piece.data.update()
mt.report("turret")
mt.scale(METRES)
export("RhinoTurret")

# Rhino first: stopping here keeps a model Meshy has already painted from being wiped.
if os.path.exists(os.path.join(mt.PREPARED, "ArcherMissilePod", "painted.glb")):
    sys.exit(0)
mt.start("ArcherMissilePod", "Meshy/Meshy_AI_Missile_pod.glb")
mt.source_size()
mt.turn(yaw=90)
mt.scale(3.6 / 0.96)
mt.report("pod")
export("ArcherMissilePod")
