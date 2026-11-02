# The Warthog with the gauss cannon: the right of the two jeeps in the file, the one with the long single barrel. The gun and
# its post stand on a round ring in the bed and really are separate, so they turn together around the ring's centre (Turret_0).
import math
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bpy
from mathutils import Vector

mt.start("GaussWarthog", "sketchfab.com/vehicleshumangroundwarthogshalo_2.glb")
mt.keep_box((3.1, -9, -9), (20, 9, 9))  # The other jeep, at x below 2, has the short chaingun.
mt.fit()
mt.weld(0.004)
mt.remove("Object_6")  # The dashboard's dials: 18 triangles that cost a material of their own.
# The file's see-through windshield shards, which show as solid violet here. They go, and leave the open frame.
for low, high in [((-0.2, -2.5, 3.2), (0.2, -2.1, 3.5)), ((0.6, -1.32, 3.7), (0.85, -1.0, 3.86)), ((-0.85, -1.32, 3.7), (-0.6, -1.0, 3.86))]:
    mt.remove_box(low, high, "Object_5")
mt.smooth(40)

# The middle of the barrel's end, which sits off to one side of the mount.
barrel = mt.meshes("Object_3")[0]
end = [v.co for v in barrel.data.vertices if v.co.y < -1.0]
tip = sum(end, Vector()) / len(end)
print(f"REPORT barrel end {tip} from {len(end)} vertices")

mt.part("Turret_0", "Object_3|Object_4", pivot=(0.006, 2.7685, 2.14))
mt.muzzle("Muzzle_0", tip, parent="Turret_0")

# The four tyres, each turning around its own middle.
for i, (x, y) in enumerate([(1.975, -3.4), (-1.975, -3.4), (1.975, 3.217), (-1.975, 3.217)]):
    mt.take(f"Wheel_{i}", (x - 0.6, y - 1.1, 0), (x + 0.6, y + 1.1, 2.1), "Object_7", pivot=(x, y, 1.018))

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
node.rotation_euler[2] = math.radians(70)
bpy.context.view_layer.update()
mt.look("after_turned", "quarter")
