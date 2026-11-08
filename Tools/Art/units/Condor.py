# The Condor: the owner's model, 22,481 triangles in 14 parts, 845 units long, to under 9,000 and one material. Its nose is at -Y.
# All 13 of its materials show one 1024 texture, so they become one and it keeps its UVs. What cannot be seen from any side goes.
# Under the belly hangs a long twin-railed gun on a round mount, and it becomes the turret. The file has it aimed out to the +X side,
# with the barrel 33 degrees down, and a turret must rest looking forward, so it is turned a quarter round on its mount. Its rails
# are where the Twin Autocannons fire from. What looked like five jointed landing legs are gun pods (the owner, 2026-11-08, D111): each a
# twin-barrelled gun on a round mount of its own, as the belly gun is. Each becomes a turret too, turned on its mount to rest looking
# forward, its muzzles at its two barrel tips: weapons 1 to 5, the light Spartan Lasers.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import bpy
import math
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

GUN = "^Object_(3|9)$"
LEGS = "^Object_(2|4|6|10|13)$"  # The gun pods' guns.
MOUNTS = "^Object_(5|7|8|11|12)$"  # Their mounts.
HULL = "^Object_(14|15)$"
STILL = HULL

mt.start("Condor", "Meshy/unsc_condor.glb")

material = mt.meshes()[0].data.materials[0]
for o in mt.meshes():
    o.data.materials.clear()
    o.data.materials.append(material)

# The numbers below are in the 10-unit size that start() gives, read from its report. They are the same for every run.
PIVOT = Vector((0.0115, 0.911, 1.2))  # The middle of the round mount, up against the hull.
RAILS = (0.77, 1.073)  # Where the two rails run across the barrel, before it is turned.
TIP = (2.44, -1.065)  # How far out the rails end, and at what height, before it is turned.


# The gun pods, weapons 1 to 5 in this order: each pod's gun and mount, the middle of its mount up against the hull, which way its barrels
# point across the ground (degrees from forward, toward +X), and its two barrel tips, all before it is turned. Measured in the same size.
PODS = [
    ("^Object_(2|7)$", (0.027, -3.645, 0.86), -13, (-0.229, -4.65, -0.252), (-0.152, -4.661, -0.233)),  # The nose.
    ("^Object_(6|8)$", (-0.544, -0.713, 0.96), -75, (-1.517, -0.946, -0.158), (-1.489, -1.021, -0.134)),  # Front right (-X).
    ("^Object_(10|11)$", (0.575, -0.723, 0.96), 48, (1.294, -1.401, -0.153), (1.342, -1.338, -0.133)),  # Front left (+X).
    ("^Object_(13|5)$", (-0.507, 2.808, 1.35), -149, (-1.002, 3.663, 0.241), (-1.066, 3.61, 0.257)),  # Rear right.
    ("^Object_(4|12)$", (0.468, 2.8, 1.35), 121, (1.319, 3.289, 0.236), (1.275, 3.355, 0.253)),  # Rear left.
]


def around(pivot, degrees, point):
    """Where a point goes when it is turned this many degrees about the upright through pivot (toward +Y from +X)."""
    return Vector(pivot) + Matrix.Rotation(math.radians(degrees), 3, "Z") @ (Vector(point) - Vector(pivot))


def turn(pattern, pivot, degrees):
    pivot = Vector(pivot)
    matrix = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(degrees), 4, "Z") @ Matrix.Translation(-pivot)
    for o in mt.meshes(pattern):
        o.data.transform(matrix)
        o.data.update()


def turned(point):
    """Where a point of the gun goes when it is turned from looking at +X to looking at -Y."""
    return around(PIVOT, -90, point)


turn(GUN, PIVOT, -90)
for pattern, pivot, heading, _, _ in PODS:
    turn(pattern, pivot, -heading)  # From pointing that way to pointing forward.

mt.weld(0.002)
mt.report("welded")


def directions(count=300):
    """Evenly spread directions all around."""
    found = []
    for i in range(count):
        z = 1 - 2 * (i + 0.5) / count
        side = math.sqrt(1 - z * z)
        found.append(Vector((side * math.cos(i * 2.399963), side * math.sin(i * 2.399963), z)))
    return found


def tidy(pattern):
    """
    Deletes the faces no ray from outside can reach, from any side, and turns round the few that are seen from behind, since the
    game hides back faces. A face counts as hidden only if its middle and all its corners are.
    """
    around = directions()
    parts = mt.meshes(pattern)
    points, faces = [], []
    for o in parts:
        base = len(points)
        points.extend(v.co.copy() for v in o.data.vertices)
        faces.extend([base + i for i in p.vertices] for p in o.data.polygons)
    tree = BVHTree.FromPolygons(points, faces)

    def escapes(at, normal, way):
        return tree.ray_cast(at + (normal if way.dot(normal) > 0 else -normal) * 0.003, way, 100)[0] is None

    gone, flipped = 0, 0
    for o in parts:
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        mesh.normal_update()
        doomed, backward = [], []
        for f in mesh.faces:
            middle = f.calc_center_median()
            front = back = 0
            for way in around:
                if escapes(middle, f.normal, way):
                    if way.dot(f.normal) > 0:
                        front += 1
                    else:
                        back += 1
            if front + back == 0:
                if not any(escapes(middle.lerp(v.co, 0.8), f.normal, way) for v in f.verts for way in around):
                    doomed.append(f)
            elif back > front * 1.5:
                backward.append(f)
        gone += sum(len(f.verts) - 2 for f in doomed)
        flipped += len(backward)
        if backward:
            bmesh.ops.reverse_faces(mesh, faces=backward)
        bmesh.ops.delete(mesh, geom=doomed, context="FACES_ONLY")
        bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
        mesh.to_mesh(o.data)
        mesh.free()
    print(f"REPORT tidied {pattern}: {gone} hidden triangles gone, {flipped} faces turned round")


def remove_tiny(smaller_than, pattern):
    """Deletes separate pieces shorter than this every way: bolts and cables nobody sees from the game's camera."""
    for o in mt.meshes(pattern):
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        doomed = []
        for faces in mt._islands(mesh):
            corners = [v.co for f in faces for v in f.verts]
            if max(mt._high(corners) - mt._low(corners)) < smaller_than:
                doomed.extend(faces)
        bmesh.ops.delete(mesh, geom=doomed, context="FACES_ONLY")
        bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
        mesh.to_mesh(o.data)
        mesh.free()


# The guns turn, so none must hide anything for good and nothing may be hidden from them for good: each is looked at on its own.
tidy(STILL)
tidy(GUN)
for pattern, _, _, _, _ in PODS:
    tidy(pattern)
remove_tiny(0.12, LEGS)
remove_tiny(0.08, HULL)
remove_tiny(0.06, GUN)
mt.report("tidied")

mt.reduce(5800, HULL)
mt.reduce(1600, LEGS)
mt.reduce(500, MOUNTS)
mt.reduce(1000, GUN)
mt.report("reduced")


def inner_sides(pattern, least=6):
    """
    The hull is an open shell: through the gaps by the engines and intakes one sees the backs of its faces, and the game hides those,
    which would leave holes. Faces whose back can be seen from above get a second, reversed copy. The game's camera is above an air
    unit, so a back seen only from below stays as it is.
    """
    above = [way for way in directions() if way.z > -0.25]
    parts = mt.meshes(pattern)
    points, faces = [], []
    for o in parts:
        base = len(points)
        points.extend(v.co.copy() for v in o.data.vertices)
        faces.extend([base + i for i in p.vertices] for p in o.data.polygons)
    tree = BVHTree.FromPolygons(points, faces)
    added = 0
    for o in parts:
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        mesh.normal_update()
        seen = []
        for f in mesh.faces:
            middle = f.calc_center_median()
            start = middle - f.normal * 0.003
            if sum(1 for way in above if way.dot(f.normal) < 0 and tree.ray_cast(start, way, 100)[0] is None) >= least:
                seen.append(f)
        if seen:
            copies = bmesh.ops.duplicate(mesh, geom=seen)["geom"]
            bmesh.ops.reverse_faces(mesh, faces=[g for g in copies if isinstance(g, bmesh.types.BMFace)])
        added += sum(len(f.verts) - 2 for f in seen)
        mesh.to_mesh(o.data)
        mesh.free()
    print(f"REPORT inner sides for {pattern}: {added} triangles added")


inner_sides(STILL)
mt.report("with inner sides")

low, high = mt._bounds()
shift = Vector((-(low.x + high.x) / 2, -(low.y + high.y) / 2, -low.z))
mt.centre_on_ground()
mt.smooth(40)

mt.part("Turret_0", GUN, pivot=PIVOT + shift)
mt.muzzle("Muzzle_0a", turned((TIP[0], RAILS[0], TIP[1])) + shift, parent="Turret_0")
mt.muzzle("Muzzle_0b", turned((TIP[0], RAILS[1], TIP[1])) + shift, parent="Turret_0")
for k, (pattern, pivot, heading, tip_a, tip_b) in enumerate(PODS, 1):
    mt.part(f"Turret_{k}", pattern, pivot=Vector(pivot) + shift)
    mt.muzzle(f"Muzzle_{k}a", around(pivot, -heading, tip_a) + shift, parent=f"Turret_{k}")
    mt.muzzle(f"Muzzle_{k}b", around(pivot, -heading, tip_b) + shift, parent=f"Turret_{k}")
mt.motion("Hover")
mt.merge_rest()
mt.report("done")
mt.finish()
mt.look("after_back", "back")
mt.look("after_far", "quarter", size=(200, 150))
