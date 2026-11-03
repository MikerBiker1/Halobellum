# The Rhino: a model for a 3D printer with no colours, reduced and painted by Meshy (Tools/Meshy/texture_inputs.py, meshy_paint.py). Its turret
# stands on its ring and turns (Turret_0). Sizes are in metres, from texture_inputs.py. It faces -Y.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bpy
from mathutils import Matrix, Vector

METRES = 9.5 / 177.0
RING = (-53.27 * METRES, -43.45 * METRES)
BARREL_TIP = (RING[0], -141.0 * METRES, 39.0 * METRES)



def box_of(part):
    """Where this part was when it was sent to Meshy: its file sets the size and place."""
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(mt.SOURCE, "Prepared", "_Paint", part, "input.glb"))
    bpy.context.view_layer.update()
    new = [o for o in bpy.context.scene.objects if o not in before]
    box = mt._bounds([o for o in new if o.type == "MESH"])
    for o in new:
        bpy.data.objects.remove(o)
    return box


def fit_box(parts, low, high):
    a, b = mt._bounds(parts)
    size = max(b[i] - a[i] for i in range(3))
    wanted = max(high[i] - low[i] for i in range(3))
    for o in parts:
        o.data.transform(Matrix.Scale(wanted / size, 4))
        o.data.update()
    a, _ = mt._bounds(parts)
    for o in parts:
        o.data.transform(Matrix.Translation(Vector(low) - a))
        o.data.update()


mt.start("Rhino", "Prepared/_Paint/RhinoHull/painted.glb")
mt.source_size()
for o in mt.meshes():
    o.name = "Hull"
bpy.ops.import_scene.gltf(filepath=os.path.join(mt.SOURCE, "Prepared", "_Paint", "RhinoTurret", "painted.glb"))
bpy.context.view_layer.update()
turret = [o for o in mt.meshes() if o.name != "Hull"]
for o in turret:
    world = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = world
for o in list(bpy.context.scene.objects):
    if o.type != "MESH":
        bpy.data.objects.remove(o)
mt._select(turret)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
# Meshy gives each part back centred and 2 units long, so each goes back where it was.
fit_box(mt.meshes("Hull"), *box_of("RhinoHull"))
fit_box(turret, *box_of("RhinoTurret"))
mt.report("loaded")
mt.smooth(35)
mt.part("Turret_0", "^(?!Hull)", pivot=(RING[0], RING[1], 0.8))
mt.muzzle("Muzzle_0", BARREL_TIP, parent="Turret_0")
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=1024)
