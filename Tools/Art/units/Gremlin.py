# The Gremlin, from the owner's Meshy model: a six-wheeled scout car with a canopied cab, an EMP railgun on a post over its middle and a spare
# wheel on its tail. 14,116 triangles to about 8,000, its colours painted onto the new surface as Meshy models are. Its nose is along -X in the
# file. Sizes are in metres, at its length in the game (4.6 m, its 2.2 m width matching its collision radius of 1.1). The file is one skin:
# faces are parted by where their middles are. The railgun and the dish behind it turn on the post (Turret_0); the three wheels on each side
# spin (Wheel_<n>), each found by the tyre's own vertices. Antennas, the spare wheel and everything else stay with the body.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import bpy
from mathutils import Vector

LENGTH = 4.6
TYRE_X = 0.56  # The tyres stand outside this, either side.
TYRE = 0.41  # How far a tyre reaches from its axle.
AXLES_Y = (-1.135, 0.77, 1.66)  # Read off the file's loose pieces, front to back.
AXLE_Z = 0.415
TURRET = ((-0.35, -1.3, 1.95), (0.35, 1.6, 9.0))  # Narrow enough to leave the antennas on the body.
POST = (0.0, 0.3, 1.95)


def split_off(o, name, test):
    """Moves the faces whose middle passes the test to a new object."""
    copy = o.copy()
    copy.data = o.data.copy()
    copy.name = name
    bpy.context.scene.collection.objects.link(copy)
    for target, keep in ((copy, True), (o, False)):
        mesh = bmesh.new()
        mesh.from_mesh(target.data)
        doomed = [f for f in mesh.faces if test(target.matrix_world @ f.calc_center_median()) != keep]
        bmesh.ops.delete(mesh, geom=doomed, context="FACES_ONLY")
        bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
        mesh.to_mesh(target.data)
        mesh.free()
    print(f"REPORT {name}: {mt.tris([copy])} triangles")
    return copy


mt.start("Gremlin", "Meshy/Meshy_AI_Gremlin.glb")
mt.turn(yaw=90)
mt.fit(LENGTH)
mt.report("fitted")
mt.reduce_and_repaint(8000, 1024, reach=0.02)
mt.smooth(40)
body = bpy.data.objects["Body"]

# Each axle's true middle, from the tyre's vertices near the guess.
points = [body.matrix_world @ v.co for v in body.data.vertices]
axles = []
for side in (1, -1):
    for y in AXLES_Y:
        near = [p for p in points if p.x * side > TYRE_X and (Vector((p.y, p.z)) - Vector((y, AXLE_Z))).length < TYRE + 0.04]
        middle = Vector(((min(p.y for p in near) + max(p.y for p in near)) / 2, (min(p.z for p in near) + max(p.z for p in near)) / 2))
        axles.append((side, middle))
        print(f"REPORT axle {side} {y}: middle {mt._round(middle)} from {len(near)} vertices")

turret = split_off(body, "Turret_0", lambda p: mt._inside(p, Vector(TURRET[0]), Vector(TURRET[1])))
front = [turret.matrix_world @ v.co for v in turret.data.vertices]
nose = min(p.y for p in front)
tip = [p for p in front if p.y < nose + 0.05]
muzzle_at = Vector((sum(p.x for p in tip) / len(tip), nose, sum(p.z for p in tip) / len(tip)))
print(f"REPORT railgun tip {mt._round(muzzle_at)} from {len(tip)} vertices")
mt._set_origin(turret, POST)

for number, (side, middle) in enumerate(axles):
    wheel = split_off(body, f"Wheel_{number}", lambda p, s=side, m=middle: p.x * s > TYRE_X and (Vector((p.y, p.z)) - m).length < TYRE)
    low, high = mt._bounds([wheel])
    mt._set_origin(wheel, ((low.x + high.x) / 2, middle.x, middle.y))

mt.muzzle("Muzzle_0", muzzle_at, parent="Turret_0")
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=1024)
mt.look("after_back", "back")

node = bpy.data.objects["Turret_0"]
node.rotation_mode = "XYZ"
node.rotation_euler[2] = 1.0
bpy.context.view_layer.update()
mt.look("after_turned", "quarter")
