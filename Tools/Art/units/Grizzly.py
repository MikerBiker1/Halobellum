# The Armored Division's Grizzly (M10): 11,708 triangles brought under 9,000. The turret with both long barrels and its machine gun
# turns as one (Turret_0), around its ring. The treads are part of the hull: they don't spin, as the file has no wheels to turn.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bpy
from mathutils import Vector

mt.start("Grizzly", "sketchfab.com/For Later/vehicleshumansgroundgrizzly.glb")
mt.turn(yaw=-90)  # It came with its nose along +X.
mt.fit()
mt.report("turned")
mt.remove("Object_4")  # The inside of the cockpit, which nobody sees.
mt.flag("cockpit_glass", glass=True)
mt.weld(0.003)  # The file's faces come loose and any way round, and the game hides back faces.
mt.fix_normals()
mt.smooth(40)

# Everything above the hull's top line, near the middle, is the turret.
turret = mt.take("Turret_0", (-1.95, -5, 2.13), (1.95, 3.4, 5), "Object_3|Object_5", pivot=(0, 1.1, 2.1), whole_pieces=False)


def tip(low, high):
    """The middle of the vertices at the front end of what lies inside this box."""
    points = [turret.matrix_world @ v.co for v in turret.data.vertices]
    points = [p for p in points if mt._inside(p, Vector(low), Vector(high))]
    front = min(p.y for p in points)
    near = [p for p in points if p.y < front + 0.08]
    return sum(near, Vector()) / len(near)


cannon_right = tip((0.05, -5, 2.5), (1, -3, 3.4))
cannon_left = tip((-1, -5, 2.5), (-0.05, -3, 3.4))
print(f"REPORT cannon tips {cannon_right} {cannon_left}")
gun = tip((-2, -1.3, 2.7), (2, -0.55, 3.5))
print(f"REPORT machine gun tip {gun}")

mt.reduce(2500, "Turret_0")
mt.reduce(5600, "Object_3")
mt.reduce(1300, "Object_5")

mt.muzzle("Muzzle_0a", cannon_right, parent="Turret_0")
mt.muzzle("Muzzle_0b", cannon_left, parent="Turret_0")
mt.muzzle("Muzzle_1", gun, parent="Turret_0")
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
