# The Elephant: a big carrier with no guns of its own in this model. Weapon 0 goes on the swivelling crane mount, whose
# top half really is separate (Turret_0). Weapon 2 goes on the round cupola at the front right (Turret_2).
# Nothing on the front left can turn, so weapon 1 is a fixed muzzle on the corner of the cab.
import math
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bpy

mt.start("Elephant", "sketchfab.com/elephant.glb")
mt.turn(yaw=-90)  # It came with its cab along +X.
mt.fit()
mt.weld(0.003)

# Its lowered ramp reaches below the tracks and would leave them hanging in the air: rest the ramp's tip on the ground instead.
ground = min((o.matrix_world @ v.co).z for o in mt.meshes("treads") for v in o.data.vertices)
for o in mt.meshes():
    for v in o.data.vertices:
        if v.co.z < ground:
            v.co.z = ground
    o.data.update()
mt.centre_on_ground()
lift = -ground  # What centre_on_ground just did to every height below.

# Small gadgets that cost a material each and that the game camera never shows.
mt.remove("generator_turbine|beh_art|beh_lower_control")
mt.remove_box((-1.75, -0.3, 3.4 + lift), (-1.5, 0.0, 3.85 + lift), "military_equipment")

mt.smooth(35)

mt.take("Turret_0", (-2.4, -1.2, 3.2 + lift), (-1.4, 3.1, 4.6 + lift), pivot=(-1.967, -0.0375, 3.148 + lift))
mt.take("Turret_2", (-1.97, -3.87, 3.2 + lift), (-1.25, -3.14, 3.34 + lift), pivot=(-1.609, -3.5045, 3.208 + lift))
mt.muzzle("Muzzle_0", (-1.82, -1.13, 4.16 + lift), parent="Turret_0")
mt.muzzle("Muzzle_1", (1.3, -4.8, 3.3 + lift))
mt.muzzle("Muzzle_2", (-1.609, -3.87, 3.28 + lift), parent="Turret_2")
mt.flag("beh_glass", glass=True)

for o in mt.meshes():
    if not o.data.polygons:
        bpy.data.objects.remove(o)
mt.merge_rest()
mt.report("done")
mt.finish(512)
mt.look("after_back", "back")

# A last check that the mount turns without leaving a hole. The file is written already.
for name in ("Turret_0", "Turret_2"):
    node = bpy.data.objects[name]
    node.rotation_mode = "XYZ"
    node.rotation_euler[2] = math.radians(70)
bpy.context.view_layer.update()
mt.look("after_turned", "quarter")
