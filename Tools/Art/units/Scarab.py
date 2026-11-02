# The Scarab. 69,836 triangles in 412 parts with 30 materials, and every texture file is missing.
# - Its file is tilted about 7 degrees in yaw and 4 in pitch, so it is straightened by its own flat deck first.
# - The rooms inside the hull (about 28,000 triangles) cannot be seen from outside at all, so they go, and so does
#   every face of what remains that no ray from outside can reach.
# - Its four legs are clean separate pieces, so they swing. The gun on top turns and carries both plasma weapons.
# - The gun at the stern points backward and sits in the hull, so it could not turn to face forward: it stays fixed.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bmesh
import bpy
import math
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

INTERIOR = r"^Object_(2|3|4|5|6|8|9|10|11|22|24|25|26|27|28|29)$"
TOP_GUN = r"^Object_47\.(06[6-9]|0[7-9]\d|1\d\d)$"
REAR_GUN = r"^Object_47(\.0[0-5]\d|\.06[0-5])?$"


def make_material(name, colour, glow=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    node = m.node_tree.nodes["Principled BSDF"]
    node.inputs["Base Color"].default_value = (*colour, 1)
    if glow:
        mt.flag(name, glow=glow)
        node.inputs["Emission Color"].default_value = (*colour, 1)  # Only so that the pictures show it.
        node.inputs["Emission Strength"].default_value = 2
    return m


def paint_islands(o, rule):
    """Gives each separate piece of a part the material rule(faces, low, high) picks."""
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    slots = []
    o.data.materials.clear()
    for faces in mt._islands(mesh):
        points = [v.co for f in faces for v in f.verts]
        chosen = rule(len(faces), mt._low(points), mt._high(points))
        if chosen not in slots:
            slots.append(chosen)
            o.data.materials.append(chosen)
        for f in faces:
            f.material_index = slots.index(chosen)
    mesh.to_mesh(o.data)
    mesh.free()


def paint_all(pattern, material):
    for o in mt.meshes(pattern):
        o.data.materials.clear()
        o.data.materials.append(material)


def join(pattern, name):
    parts = mt.meshes(pattern)
    mt._select(parts)
    if len(parts) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    return o


def biggest_two_centres(o):
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    rows = []
    for faces in mt._islands(mesh):
        corners = {v.index: v.co.copy() for f in faces for v in f.verts}
        rows.append((len(faces), sum(corners.values(), Vector()) / len(corners)))
    mesh.free()
    rows.sort(key=lambda r: -r[0])
    return rows[0][1], rows[1][1]


def straighten():
    """The flat deck says which way is up; the two mirrored halves of the hull say which way is across. The nose becomes -Y."""
    deck = bpy.data.objects["Object_15"]
    up = max(deck.data.polygons, key=lambda p: p.area).normal.copy().normalized()
    a, b = biggest_two_centres(bpy.data.objects["Object_12"])
    across = (a - b) if a.y > b.y else (b - a)
    across = (across - up * across.dot(up)).normalized()
    nose = across.cross(up).normalized()
    rotation = Matrix((across, -nose, up)).to_4x4()
    for o in mt.meshes():
        o.data.transform(rotation)
        o.data.update()


def cull_hidden(parts):
    """Deletes the faces no ray from outside can reach, and a margin of two rings of faces around those it can."""
    points, faces, owner = [], [], []
    for index, o in enumerate(parts):
        o.data.calc_loop_triangles()
        base = len(points)
        points.extend(v.co.copy() for v in o.data.vertices)
        for t in o.data.loop_triangles:
            faces.append(tuple(base + i for i in t.vertices))
            owner.append((index, t.polygon_index))
    tree = BVHTree.FromPolygons(points, faces)
    low, high = mt._bounds(parts)
    centre = (low + high) / 2
    radius = (high - low).length / 2 + 0.2
    hits = [set() for _ in parts]
    wrong_way = [0 for _ in parts]
    right_way = [0 for _ in parts]
    directions, grid = 150, 200
    step = 2 * radius / grid
    for i in range(directions):
        z = 1 - 2 * (i + 0.5) / directions
        r = math.sqrt(max(0, 1 - z * z))
        phi = i * math.pi * (3 - math.sqrt(5))
        d = Vector((r * math.cos(phi), r * math.sin(phi), z))
        u = d.orthogonal().normalized()
        v = d.cross(u).normalized()
        corner = centre + d * radius - (u + v) * radius
        for a in range(grid):
            row = corner + u * ((a + 0.5) * step)
            for b in range(grid):
                _, normal, index, _ = tree.ray_cast(row + v * ((b + 0.5) * step), -d)
                if index is not None:
                    part, polygon = owner[index]
                    hits[part].add(polygon)
                    if normal.dot(d) < 0:
                        wrong_way[part] += 1
                    else:
                        right_way[part] += 1
    for index, o in enumerate(parts):
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        mesh.faces.ensure_lookup_table()
        keep = {mesh.faces[i] for i in hits[index]}
        for _ in range(2):
            keep |= {other for f in keep for v in f.verts for other in v.link_faces}
        doomed = [f for f in mesh.faces if f not in keep]
        print(f"REPORT cull {o.name}: {len(mesh.faces)} faces, {len(hits[index])} seen, {len(keep)} kept; hits on the back {wrong_way[index]} of {wrong_way[index] + right_way[index]}")
        bmesh.ops.delete(mesh, geom=doomed, context="FACES_ONLY")
        bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
        mesh.to_mesh(o.data)
        mesh.free()


def tip_of(pattern):
    """The middle of the frontmost end of a part."""
    points = [v.co.copy() for o in mt.meshes(pattern) for v in o.data.vertices]
    front = min(p.y for p in points)
    near = [p for p in points if p.y < front + 0.06]
    return sum(near, Vector()) / len(near)


mt.start("Scarab", "sketchfab.com/modified scarab.fbx")
ARMOUR = make_material("armour", (0.042, 0.027, 0.13))
RIBS = make_material("ribs", (0.08, 0.055, 0.22))
MACHINE = make_material("machine", (0.30, 0.27, 0.43))
DECK = make_material("deck", (0.14, 0.12, 0.24))
CANNON = make_material("cannon", (0.1, 0.85, 1.0), glow=(0.12, 1.2, 1.5))
LIGHTS = make_material("lights", (0.95, 0.12, 0.85), glow=(1.5, 0.2, 1.3))
straighten()
mt.remove(INTERIOR)
mt.report("straightened, without the rooms")

# ---- Paint, while the pieces are still as the file made them ----
paint_islands(bpy.data.objects["Object_19"], lambda n, a, b: MACHINE if n < 150 or 275 <= n <= 290 else ARMOUR)  # Legs
paint_islands(bpy.data.objects["Object_12"], lambda n, a, b: ARMOUR if n > 1000 else MACHINE)  # Hull, with its ribs
paint_islands(bpy.data.objects["Object_18"], lambda n, a, b: ARMOUR if n >= 88 else CANNON)  # The cannon's claws, lit along their inner strips
paint_all(r"^Object_21$", CANNON)  # The cannon's core
paint_all(r"^Object_23", RIBS)  # The arches and everything on top
paint_all(r"^Object_(13|14|20|7)$", MACHINE)  # Columns and underside of the roof
paint_all(r"^Object_(15|16|17)$", DECK)
paint_all(r"^Object_32$", LIGHTS)
paint_all(r"^Object_47", MACHINE)  # Both guns

# The top gun's two upper barrels, whose ends are read once the model has its final size.
gun_tips = {"a": r"^Object_47\.(069|07\d|08\d)$", "b": r"^Object_47\.(09\d|10\d|110)$"}
top_base = bpy.data.objects["Object_47.067"]

# ---- Join what the file tore into scraps, and weld ----
join(r"^Object_23", "Arches")
mt.weld(0.002)
mt.remove_small_islands(6, r"^Object_19$")
mt.remove_small_islands(3, r"^(Arches|Object_12|Object_18|Object_13)$")

# ---- Size ----
mt.fit()
mt.report("fitted")

# ---- Where things are, read at the final size ----
tips = {k: tip_of(p) for k, p in gun_tips.items()}
base_low, base_high = mt._bounds([top_base])
top_pivot = ((base_low.x + base_high.x) / 2, (base_low.y + base_high.y) / 2, base_low.z)
head_low, head_high = mt._bounds(mt.meshes("^Object_18$"))
claw_ends = [v.co.z for v in bpy.data.objects["Object_18"].data.vertices if v.co.y < head_low.y + 0.3]
cannon_tip = ((head_low.x + head_high.x) / 2, head_low.y, (min(claw_ends) + max(claw_ends)) / 2)  # In the mouth, between the claws' ends.
print(f"REPORT claw ends from z {min(claw_ends):.2f} to {max(claw_ends):.2f}")
join(TOP_GUN, "TopGun")
join(REAR_GUN, "RearGun")

# ---- Faces nobody can see. The legs and the top gun move, so they hide nothing and nothing is taken from them. ----
still = [o for o in mt.meshes() if o.name not in ("Object_19", "TopGun")]
cull_hidden(still)

# ---- The legs: the pivot of each is the middle of the ring at its hip, read from the model at this size. ----
# Seen from the front, the Scarab's left is +X. Diagonal pairs swing together.
BIG = 20
mt.take("Swing_0_0", (0, -BIG, -BIG), (BIG, 0, BIG), "^Object_19$", pivot=(1.4, -0.662, 4.927))
mt.take("Swing_1_50", (-BIG, -BIG, -BIG), (0, 0, BIG), "^Object_19$", pivot=(-1.4, -0.662, 4.927))
mt.take("Swing_2_50", (0, 0, -BIG), (BIG, BIG, BIG), "^Object_19$", pivot=(1.4, 0.662, 4.927))
mt.take("Swing_3_0", (-BIG, 0, -BIG), (0, BIG, BIG), "^Object_19$", pivot=(-1.4, 0.606, 4.926))  # The file has this leg a little off.
mt.remove("^Object_19$")

# ---- Fewer triangles ----
for name in ("Swing_0_0", "Swing_1_50", "Swing_2_50", "Swing_3_0"):
    mt.reduce(1800, "^" + name + "$")
mt.reduce(4500, "^Arches$")
mt.reduce(2600, "^Object_12$")
mt.reduce(1800, "^Object_18$")
mt.reduce(350, "^Object_21$")
mt.reduce(280, "^Object_13$")
mt.reduce(180, "^Object_14$")
mt.reduce(120, "^Object_7$")
mt.smooth(38)
mt.report("reduced")

# ---- The guns ----
mt.part("Turret_1", "^TopGun$", pivot=top_pivot)
left, right = (tips["a"], tips["b"]) if tips["a"].x > tips["b"].x else (tips["b"], tips["a"])
mt.muzzle("Muzzle_1", left, parent="Turret_1")
mt.muzzle("Muzzle_2", right, parent="Turret_1")
mt.muzzle("Muzzle_0", cannon_tip)
mt.merge_rest()
mt.finish()
