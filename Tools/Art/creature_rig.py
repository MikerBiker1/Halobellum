# creature_rig.py
# Rigs and animates a creature whose model came with no usable rig (D90): the Spawner, the Pure Form Ranged and the Stalker Form.
# Meshy's rigging stretched the Stalker and refused the Ranged Form (morning item 19), so each gets a skeleton laid out by hand in its
# script, skin weights worked out here from that skeleton, and clips posed from waves and planted feet. A creature's script
# (Tools/Art/units/<name>.py) starts from the model its static preparation wrote (Art_Source/Prepared/<static>/) and writes
# Art_Source/Prepared/<name>/: <name>.fbx (the mesh, the rig "Rig" and its clips, which Unity names "Rig|<clip>"), <name>.png (one texture,
# with the fronds' see-through edges in its alpha), report.json and pictures in shots/.
#
# The skeleton: a still "Root" at the origin, which the crowd baker treats as the hips and must not move across the ground, and under it
# "Body", which carries everything and does all the swaying. Bones are given in metres at the game's size, front toward -Y, up +Z.
# A bone named Muzzle... moves no flesh: it marks where shots leave (the baker's muzzleBone).
#
# The weights: a vertex close to one bone and well away from every other is that bone's for certain. From those, each bone's share spreads
# over the surface, so a claw that hangs beside a knee still belongs to its arm: over the surface the two are far apart. Small separate
# pieces (spines, fronds, tendrils) go whole to one bone. Then each vertex near a joint is shared with the next bone up or down its chain,
# the weights are smoothed over the surface, and each vertex keeps its two strongest bones, as many as the crowd shader reads.
#
# The legs: each foot has a target. While a foot carries weight it stays where it was put, sliding back exactly as fast as the unit walks,
# so that in the game, where the unit moves forward at that speed, it stands still; while it swings it lifts and moves ahead. Two bones
# reach for the target (the upper bone and the lower one), the knee bending toward a given side, and the foot bone below them keeps its
# own angle to the world. walk_slide() measures how far a planted foot really slides once the body moves at the unit's speed.
#
# Needs only Blender's own Python; model_tools.py sits beside it.

import heapq
import json
import math
import os
import shutil
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import model_tools as mt  # noqa: E402

_c = {"name": None, "folder": None, "mesh": None, "arm": None, "fps": 30, "source": None, "legs": {}, "radius": {}, "clips": [], "cutout": False}


# ---- Loading ----

def load(name, static, source, height=None, length=None, lift=(1.0, 1.0, 1.0), fps=30, yaw=0.0):
    """
    Empties Blender and loads Art_Source/Prepared/<static>/<static>.fbx, the creature as its static preparation left it, as one mesh "Body":
    stood on the ground, centred, and scaled to its height (or its longest side across the ground) in the game. Its textures become one,
    stacked if it has several, each multiplied by lift (the static model's colour over its texture, as RealModelBuilder set it).
    yaw turns it about the vertical first, in degrees (the Combat Form came out of its preparation facing backwards). source is the
    original file, for the credits.
    """
    bpy.ops.wm.read_factory_settings(use_empty=True)
    folder = os.path.join(mt.PREPARED, name)
    shutil.rmtree(folder, ignore_errors=True)
    os.makedirs(os.path.join(folder, "shots"))
    mt._state.update(name=name, folder=folder, source=source, flags={}, high=None)
    _c.update(name=name, folder=folder, fps=fps, source=source, legs={}, radius={}, clips=[], cutout=False)

    static_folder = os.path.join(mt.PREPARED, static)
    bpy.ops.import_scene.fbx(filepath=os.path.join(static_folder, static + ".fbx"))
    bpy.context.scene.render.fps = fps  # After the import, which sets the scene to the file's own rate (24), and the clips are written at it.
    bpy.context.scene.render.fps_base = 1.0
    for o in list(bpy.context.scene.objects):
        if o.type != "MESH":
            bpy.data.objects.remove(o)
    parts = mt.meshes()
    for o in parts:
        world = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = world
    mt._select(parts)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(parts) > 1:
        bpy.ops.object.join()
    mesh = bpy.context.view_layer.objects.active
    mesh.name = "Body"
    mesh.data.name = "Body"

    if yaw:
        mesh.data.transform(Matrix.Rotation(math.radians(yaw), 4, "Z"))
        mesh.data.update()
    low, high = mt._bounds([mesh])
    span = high - low
    factor = height / span.z if height else length / max(span.x, span.y)
    centre = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    mesh.data.transform(Matrix.Scale(factor, 4) @ Matrix.Translation(-centre))
    mesh.data.update()
    _c["mesh"] = mesh
    low, high = mt._bounds([mesh])
    print(f"REPORT loaded {static}: {len(mesh.data.polygons)} faces, scaled by {factor:.4f} to {mt._round(high - low)}")

    with open(os.path.join(static_folder, "materials.json"), encoding="utf-8") as f:
        table = {m["name"]: m for m in json.load(f)["materials"]}
    _atlas(mesh, static_folder, table, lift)
    return mesh


def _atlas(mesh, static_folder, table, lift):
    """One texture for the whole creature: each material's texture stacked under the last, its UVs moved onto its place, then one material."""
    images = []
    for material in mesh.data.materials:
        entry = table.get(material.name) or next(iter(table.values()))
        image = bpy.data.images.load(os.path.join(static_folder, entry["texture"]))
        images.append((image, entry.get("cutout", False)))
    width = max(image.size[0] for image, _ in images)
    height = sum(image.size[1] for image, _ in images)
    pixels = np.ones((height, width, 4), dtype=np.float32)
    places = []
    row = 0
    for image, cutout in images:
        w, h = image.size
        data = np.empty(w * h * 4, dtype=np.float32)
        image.pixels.foreach_get(data)
        data = data.reshape(h, w, 4)
        data[:, :, :3] = np.clip(data[:, :, :3] * np.array(lift, dtype=np.float32), 0.0, 1.0)
        if not cutout:
            data[:, :, 3] = 1.0
        else:
            _c["cutout"] = True
        pixels[row:row + h, :w] = data
        places.append((row, w, h))
        row += h
    # Where the stacked textures leave a gap, their average colour: dark texels would bleed into the edges when the game draws it small.
    filled = np.zeros((height, width), dtype=bool)
    for (row, w, h) in places:
        filled[row:row + h, :w] = True
    pixels[~filled, :3] = pixels[filled, :3].mean(axis=0)

    outside = 0
    _c["cutout_faces"] = [p.index for p in mesh.data.polygons if images[p.material_index][1]]
    uv = mesh.data.uv_layers.active.data
    for polygon in mesh.data.polygons:
        row, w, h = places[polygon.material_index]
        loops = list(polygon.loop_indices)
        shift_u = math.floor(min(uv[i].uv.x for i in loops))
        shift_v = math.floor(min(uv[i].uv.y for i in loops))
        for i in loops:
            u, v = uv[i].uv.x - shift_u, uv[i].uv.y - shift_v
            if u > 1.0001 or v > 1.0001:
                outside += 1
            uv[i].uv = (u * w / width, (row + v * h) / height)
    image = bpy.data.images.new(_c["name"], width, height, alpha=True)
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw = os.path.join(_c["folder"], _c["name"] + ".png")
    image.file_format = "PNG"
    image.save()

    material = bpy.data.materials.new(_c["name"])
    material.use_nodes = True
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    shader = mt._shader(material)
    material.node_tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])
    if _c["cutout"]:
        material.node_tree.links.new(node.outputs["Alpha"], shader.inputs["Alpha"])
    shader.inputs["Roughness"].default_value = 0.9
    mesh.data.materials.clear()
    mesh.data.materials.append(material)
    for polygon in mesh.data.polygons:
        polygon.material_index = 0
    print(f"REPORT texture {width}x{height} from {len(images)}, see-through: {_c['cutout']}, {outside} UV corners outside one tile")


def double_sided():
    """Copies the see-through faces (the fronds), turned the other way: the crowd shader hides back faces, and a frond is a flat card."""
    mesh = _c["mesh"]
    data = bmesh.new()
    data.from_mesh(mesh.data)
    data.faces.ensure_lookup_table()
    faces = [data.faces[i] for i in _c.get("cutout_faces", [])]
    copied = bmesh.ops.duplicate(data, geom=faces)
    new_faces = [g for g in copied["geom"] if isinstance(g, bmesh.types.BMFace)]
    bmesh.ops.reverse_faces(data, faces=new_faces, flip_multires=False)
    data.to_mesh(mesh.data)
    data.free()
    mesh.data.update()
    print(f"REPORT {len(faces)} faces made double-sided")


# ---- The skeleton ----

def skeleton(spec):
    """
    Makes the armature "Rig" from (name, head, tail, parent[, radius]) in metres; a parent of None means Body. A radius (default: a third of
    the bone's length, at least 0.08 m) says how far around the bone its flesh reaches, which matters only where bones compete for a vertex.
    Root and Body come first, from the given Body spec.
    """
    data = bpy.data.armatures.new("Rig")
    arm = bpy.data.objects.new("Rig", data)
    bpy.context.scene.collection.objects.link(arm)
    mt._select([arm])
    bpy.ops.object.mode_set(mode="EDIT")
    root = data.edit_bones.new("Root")
    root.head, root.tail = (0, 0, 0), (0, 0, 0.3)
    for entry in spec:
        name, head, tail, parent = entry[:4]
        bone = data.edit_bones.new(name)
        bone.head, bone.tail = Vector(head), Vector(tail)
        bone.parent = data.edit_bones["Root" if name == "Body" else (parent or "Body")]
        bone.use_connect = False
        _c["radius"][name] = entry[4] if len(entry) > 4 else max(0.08, (Vector(tail) - Vector(head)).length / 3)
    bpy.ops.object.mode_set(mode="OBJECT")
    for bone in arm.pose.bones:
        bone.rotation_mode = "QUATERNION"
    _c["arm"] = arm
    print(f"REPORT skeleton of {len(data.bones)} bones")
    return arm


def chain(prefix, points, parent, radius=None):
    """Bones <prefix>_0, _1, ... along a list of points, each the child of the one before; returns their spec entries."""
    entries = []
    for i in range(len(points) - 1):
        entry = (f"{prefix}_{i}", points[i], points[i + 1], parent if i == 0 else f"{prefix}_{i - 1}")
        entries.append(entry + ((radius,) if radius else ()))
    return entries


# ---- The weights ----

def _graph(points):
    """The surface as a graph: each vertex's neighbours with the distance to them (vertices at one place, split by a texture seam, are joined),
    which piece each vertex is in, and the pieces as lists of vertices."""
    mesh = _c["mesh"]
    count = len(points)
    edges = np.array([e.vertices[:] for e in mesh.data.edges])
    neighbours = [[] for _ in range(count)]
    for a, b in edges:
        length = float(np.linalg.norm(points[a] - points[b]))
        neighbours[a].append((b, length))
        neighbours[b].append((a, length))
    # The same place twice (a seam in the texture): joined for spreading, as the surface goes on there.
    keyed = {}
    for v in range(count):
        keyed.setdefault(tuple(np.round(points[v], 4)), []).append(v)
    for same in keyed.values():
        for a in same:
            for b in same:
                if a != b:
                    neighbours[a].append((b, 0.0))

    # Separate pieces.
    piece = -np.ones(count, dtype=int)
    pieces = []
    for v in range(count):
        if piece[v] >= 0:
            continue
        stack, members = [v], []
        piece[v] = len(pieces)
        while stack:
            a = stack.pop()
            members.append(a)
            for b, _ in neighbours[a]:
                if piece[b] < 0:
                    piece[b] = len(pieces)
                    stack.append(b)
        pieces.append(members)

    return neighbours, piece, pieces


def pieces():
    """The separate pieces of the mesh, largest first, each a list of vertex indices: how weigh() sees them."""
    points = np.array([v.co for v in _c["mesh"].data.vertices])
    return sorted(_graph(points)[2], key=len, reverse=True)


def piece_axis(members, towards):
    """
    Points along a long thin piece (a tendril), from the end nearest towards to the other: its vertices are ordered along their main
    direction, and the middle of each sixth of them but the end ones gives a point, with the two ends added: six points. Returns them and its length.
    """
    points = np.array([_c["mesh"].data.vertices[v].co for v in members])
    middle = points.mean(axis=0)
    _, _, axes = np.linalg.svd(points - middle, full_matrices=False)
    along = (points - middle) @ axes[0]
    order = np.argsort(along)
    first, last = points[order[: max(1, len(order) // 12)]].mean(axis=0), points[order[-max(1, len(order) // 12):]].mean(axis=0)
    if np.linalg.norm(last - np.array(towards)) < np.linalg.norm(first - np.array(towards)):
        order, first, last = order[::-1], last, first
    path = [first]
    for chunk in np.array_split(order, 6)[1:-1]:
        path.append(points[chunk].mean(axis=0))
    path.append(last)
    length = sum(np.linalg.norm(path[i + 1] - path[i]) for i in range(len(path) - 1))
    return [Vector(p) for p in path], float(length)


def weigh(allowed=None, blend=0.3, smooth=4, whole_below=40, margin=1.3, pieces_only=(), piece_bones=None):
    """
    Skin weights from the skeleton (see the top of this file). allowed(point) may return the names of the bones a vertex may belong to,
    or None for any; it keeps, say, the toes of a hind foot from the front claw beside them. Pieces with fewer than whole_below vertices go
    whole to the bone nearest their middle. Bones in pieces_only take only such whole pieces (a jaw that is its own piece), none of the rest.
    piece_bones maps a piece (by its lowest vertex index) to the only bones it may follow: a tendril and its own chain. Those bones then
    take nothing else.
    """
    mesh, arm = _c["mesh"], _c["arm"]
    names = [b.name for b in arm.data.bones if b.name != "Root" and not b.name.startswith("Muzzle")]  # A muzzle bone moves no flesh.
    index = {n: i for i, n in enumerate(names)}
    heads = np.array([arm.data.bones[n].head_local for n in names])
    tails = np.array([arm.data.bones[n].tail_local for n in names])
    radius = np.array([_c["radius"][n] for n in names])
    points = np.array([v.co for v in mesh.data.vertices])
    count = len(points)

    along = tails - heads
    lengths = np.maximum((along ** 2).sum(axis=1), 1e-9)
    share = np.clip(((points[:, None, :] - heads[None]) * along[None]).sum(axis=2) / lengths[None], 0.0, 1.0)
    nearest = heads[None] + share[:, :, None] * along[None]
    distance = np.sqrt(((points[:, None, :] - nearest) ** 2).sum(axis=2)) / radius[None]
    if allowed is not None:
        for v in range(count):
            names_here = allowed(Vector(points[v]))
            if names_here is not None:
                mask = np.ones(len(names), dtype=bool)
                mask[[index[n] for n in names_here if n in index]] = False
                distance[v, mask] = np.inf

    neighbours, piece, pieces = _graph(points)

    for members in pieces:
        if len(members) >= whole_below:
            for name in pieces_only:
                distance[np.ix_(members, [index[name]])] = np.inf
    if piece_bones:
        owned = {index[n] for names_of in piece_bones.values() for n in names_of if n in index}
        for members in pieces:
            names_of = piece_bones.get(min(members))
            keep = {index[n] for n in names_of if n in index} if names_of is not None else set(range(len(names))) - owned
            mask = np.array([i not in keep for i in range(len(names))])
            distance[np.ix_(members, np.nonzero(mask)[0])] = np.inf
    order = np.argsort(distance, axis=1)
    first = distance[np.arange(count), order[:, 0]]
    second = distance[np.arange(count), order[:, 1]] if len(names) > 1 else np.full(count, np.inf)
    label = -np.ones(count, dtype=int)
    reach = np.full(count, np.inf)
    queue = []
    for v in range(count):
        if second[v] > first[v] * margin and np.isfinite(first[v]):
            label[v] = order[v, 0]
            reach[v] = 0.0
            heapq.heappush(queue, (0.0, v))
    while queue:
        d, a = heapq.heappop(queue)
        if d > reach[a]:
            continue
        for b, length in neighbours[a]:
            step = d + length
            if step < reach[b] and np.isfinite(distance[b, label[a]]):
                reach[b] = step
                label[b] = label[a]
                heapq.heappush(queue, (step, b))
    whole = 0
    for members in pieces:
        if len(members) < whole_below or (label[members] < 0).any():
            middle = points[members].mean(axis=0)
            candidates = allowed(Vector(middle)) if allowed else None
            d = np.sqrt(((middle - (heads + np.clip(((middle - heads) * along).sum(axis=1) / lengths, 0, 1)[:, None] * along)) ** 2).sum(axis=1)) / radius
            if candidates is not None:
                d[[i for n, i in index.items() if n not in candidates]] = np.inf
            if len(members) < whole_below:
                label[members] = int(np.argmin(d))
                whole += 1
            else:
                for v in members:
                    if label[v] < 0:
                        label[v] = order[v, 0]

    parents = [index.get(arm.data.bones[n].parent.name, -1) if arm.data.bones[n].parent else -1 for n in names]
    children = [[i for i, p in enumerate(parents) if p == b] for b in range(len(names))]
    weights = np.zeros((count, len(names)))
    for v in range(count):
        b = label[v]
        t = share[v, b]
        if t < blend and parents[b] >= 0 and np.isfinite(distance[v, parents[b]]):
            w = 0.5 + 0.5 * t / blend
            weights[v, b] = w
            weights[v, parents[b]] = 1 - w
        elif t > 1 - blend and children[b]:
            near = min(children[b], key=lambda c: distance[v, c])
            if np.isfinite(distance[v, near]):
                w = 0.5 + 0.5 * (1 - t) / blend
                weights[v, b] = w
                weights[v, near] = 1 - w
            else:
                weights[v, b] = 1.0
        else:
            weights[v, b] = 1.0
    rigid = np.zeros(count, dtype=bool)
    for members in pieces:
        if len(members) < whole_below:
            rigid[members] = True
            weights[members] = 0.0
            weights[members, label[members[0]]] = 1.0

    pairs = np.array([(a, b) for a in range(count) for b, _ in neighbours[a]])
    degree = np.bincount(pairs[:, 0], minlength=count).astype(float)
    for _ in range(smooth):
        total = np.zeros_like(weights)
        np.add.at(total, pairs[:, 0], weights[pairs[:, 1]])
        smoothed = 0.5 * weights + 0.5 * total / np.maximum(degree, 1)[:, None]
        smoothed[rigid] = weights[rigid]
        smoothed[~np.isfinite(distance)] = 0.0
        weights = _two_strongest(smoothed)

    for n in names:
        mesh.vertex_groups.new(name=n)
    groups = {g.name: g for g in mesh.vertex_groups}
    for v in range(count):
        for b in np.nonzero(weights[v] > 1e-4)[0]:
            groups[names[b]].add([v], float(weights[v, b]), "REPLACE")
    mesh.parent = arm
    modifier = mesh.modifiers.new("Rig", "ARMATURE")
    modifier.object = arm
    used = sorted({names[b] for b in label})
    print(f"REPORT weights: {count} vertices, {len(pieces)} pieces ({whole} whole), {len(used)} of {len(names)} bones used")
    return {names[i]: int((label == i).sum()) for i in range(len(names))}


def _two_strongest(weights):
    keep = np.argsort(weights, axis=1)[:, -2:]
    result = np.zeros_like(weights)
    rows = np.arange(len(weights))[:, None]
    result[rows, keep] = weights[rows, keep]
    total = result.sum(axis=1, keepdims=True)
    return np.where(total > 0, result / np.maximum(total, 1e-9), result)


# ---- Posing ----

def leg(name, upper, lower, foot=None, pole=(0, -1, 0)):
    """A leg that reaches for a target: the end of its lower bone goes there, the knee bends toward pole, the foot keeps its own angle."""
    arm = _c["arm"]
    _c["legs"][name] = {
        "upper": upper, "lower": lower, "foot": foot, "pole": Vector(pole),
        "rest": Vector(arm.data.bones[lower].tail_local),
        "a": (arm.data.bones[upper].tail_local - arm.data.bones[upper].head_local).length,
        "b": (arm.data.bones[lower].tail_local - arm.data.bones[lower].head_local).length,
        "gap": (arm.data.bones[lower].head_local - arm.data.bones[upper].tail_local).length,
    }
    return _c["legs"][name]


def rest_of(leg_name):
    """Where a leg's target is at rest."""
    return _c["legs"][leg_name]["rest"].copy()


def contact_of(leg_name):
    """Where a leg's foot (the end of its foot bone) is at rest: the point that stands on the ground."""
    leg_data = _c["legs"][leg_name]
    return Vector(_c["arm"].data.bones[leg_data["foot"]].tail_local) if leg_data["foot"] else leg_data["rest"].copy()


def target_for(leg_name, contact, foot_turn=(0, 0, 0)):
    """The target for a leg's lower bone that puts the end of its foot at contact, the foot turned by foot_turn (degrees, world axes)."""
    leg_data = _c["legs"][leg_name]
    if not leg_data["foot"]:
        return Vector(contact)
    bone = _c["arm"].data.bones[leg_data["foot"]]
    turn = Euler([math.radians(v) for v in foot_turn], "XYZ").to_matrix()
    return Vector(contact) - turn @ (bone.tail_local - bone.head_local)


def world_turn(bone_name, degrees):
    """A turn about the model's axes (x across, y front to back, z up), as a pose bone's rotation: about its parent's frame, once posed."""
    rest = _c["arm"].data.bones[bone_name].matrix_local.to_3x3().to_quaternion()
    turn = Euler([math.radians(d) for d in degrees], "XYZ").to_quaternion()
    return rest.inverted() @ turn @ rest


def apply(pose):
    """
    Puts the rig in a pose: {"move": (x, y, z) for Body in metres, "turn": {bone: (x, y, z) degrees}, "scale": {bone: (x, y, z)},
    "feet": {leg: target}, "foot_turn": {leg: (x, y, z) degrees for its foot against the world},
    "aim": {leg: (direction, share)}: the leg reaching from its hip along direction to that share of its whole length (a strike)}.
    """
    arm = _c["arm"]
    for bone in arm.pose.bones:
        bone.rotation_quaternion = (1, 0, 0, 0)
        bone.location = (0, 0, 0)
        bone.scale = (1, 1, 1)
    body = arm.pose.bones["Body"]
    body.location = arm.data.bones["Body"].matrix_local.to_3x3().inverted() @ Vector(pose.get("move", (0, 0, 0)))
    for name, degrees in pose.get("turn", {}).items():
        arm.pose.bones[name].rotation_quaternion = world_turn(name, degrees)
    for name, size in pose.get("scale", {}).items():
        arm.pose.bones[name].scale = size
    bpy.context.view_layer.update()
    for name, target in pose.get("feet", {}).items():
        _reach(_c["legs"][name], Vector(target), pose.get("foot_turn", {}).get(name, (0, 0, 0)))
    for name, (direction, share) in pose.get("aim", {}).items():
        leg_data = _c["legs"][name]
        hip = _c["arm"].pose.bones[leg_data["upper"]].head
        target = hip + Vector(direction).normalized() * share * (leg_data["a"] + leg_data["b"] + leg_data["gap"])
        _reach(leg_data, target, pose.get("foot_turn", {}).get(name, (0, 0, 0)))


def _aim(bone, direction, head):
    """A bone's posed matrix turned so that it points along direction, with the least twist, its head at head."""
    now = bone.matrix.to_3x3()
    turn = now.col[1].rotation_difference(direction.normalized())
    matrix = (turn.to_matrix() @ now).to_4x4()
    matrix.translation = head
    return matrix


def _reach(leg, target, foot_turn):
    arm = _c["arm"]
    upper, lower = arm.pose.bones[leg["upper"]], arm.pose.bones[leg["lower"]]
    hip = upper.head.copy()
    a, b = leg["a"], leg["b"] + leg["gap"]
    to = target - hip
    d = min(max(to.length, abs(a - b) + 1e-4), (a + b) * 0.9995)
    along = to.normalized()
    x = (a * a - b * b + d * d) / (2 * d)
    h = math.sqrt(max(a * a - x * x, 0.0))
    side = leg["pole"] - along * leg["pole"].dot(along)
    knee = hip + along * x + side.normalized() * h
    leg["stretch"] = to.length / (a + b)
    upper.matrix = _aim(upper, knee - hip, hip)
    bpy.context.view_layer.update()
    lower.matrix = _aim(lower, hip + along * d - knee, lower.head.copy())
    bpy.context.view_layer.update()
    if leg["foot"]:
        foot = arm.pose.bones[leg["foot"]]
        rest = foot.bone.matrix_local.to_3x3()
        turn = Euler([math.radians(v) for v in foot_turn], "XYZ").to_matrix()
        matrix = (turn @ rest).to_4x4()
        matrix.translation = foot.head.copy()
        foot.matrix = matrix
        bpy.context.view_layer.update()


def lowest_point(only=None):
    """The lowest point of the posed mesh, or of only those vertices (indices)."""
    mesh = _c["mesh"]
    evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    data = evaluated.to_mesh()
    vertices = data.vertices if only is None else [data.vertices[i] for i in only]
    result = min(v.co.z for v in vertices)
    evaluated.to_mesh_clear()
    return result


def vertices_not_of(prefix):
    """The vertices that mostly follow a bone whose name does not start with prefix (for floor: limp tendrils may lie on the ground)."""
    mesh = _c["mesh"]
    names = [g.name for g in mesh.vertex_groups]
    return [v.index for v in mesh.data.vertices if v.groups and not names[max(v.groups, key=lambda g: g.weight).group].startswith(prefix)]


def clip(name, frames, pose_at, floor=False):
    """
    A clip of frames + 1 keys (a loop's last key repeats its first; the baker drops it). pose_at(t) gives the pose at t from 0 to 1.
    With floor, a frame whose lowest point sinks below the ground is lifted onto it (a body falling over); floor may also be a list of
    the vertices that count.
    """
    arm = _c["arm"]
    action = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = action
    stretched = 0.0
    for frame in range(frames + 1):
        pose = pose_at(frame / frames)
        apply(pose)
        if floor is not False and floor is not None:
            below = lowest_point(None if floor is True else floor)
            if below < 0:
                x, y, z = pose.get("move", (0, 0, 0))
                pose["move"] = (x, y, z - below)
                apply(pose)
        stretched = max([stretched] + [leg.get("stretch", 0.0) for leg in _c["legs"].values()])
        for bone in arm.pose.bones:
            bone.keyframe_insert("rotation_quaternion", frame=frame + 1)
            bone.keyframe_insert("location", frame=frame + 1)
            bone.keyframe_insert("scale", frame=frame + 1)
    action.use_fake_user = True
    action.use_frame_range = True
    action.frame_start = 1
    action.frame_end = frames + 1
    arm.animation_data.action = None
    _c["clips"].append(name)
    print(f"REPORT clip {name}: {frames} frames, {frames / _c['fps']:.2f} s, legs stretched to {100 * stretched:.0f}% at most")
    return action


def use(action_name, frame):
    """Shows a clip's frame (1 is its first)."""
    arm = _c["arm"]
    action = bpy.data.actions[action_name]
    arm.animation_data.action = action
    if action.slots and arm.animation_data.action_slot is None:
        arm.animation_data.action_slot = action.slots[0]
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()


# ---- Gaits ----

def ease(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def wave(t):
    return math.sin(t * math.pi * 2)


def step(t, phase, duty, stride, lift):
    """
    Where a foot is at t (0 to 1 of a cycle) as an offset from its middle place: planted for duty of the cycle, moving back a stride at
    an even pace, then swinging forward a stride, lifted at most lift, for the rest. Front is -Y. Returns (offset, planted).
    """
    u = (t - phase) % 1.0
    if u < duty:
        s = u / duty
        return Vector((0, -stride / 2 + stride * s, 0)), True
    s = (u - duty) / (1 - duty)
    k = ease(s)
    return Vector((0, stride / 2 - stride * k, lift * math.sin(math.pi * s) ** 0.8)), False


def walk_slide(action_name, legs, speed, floor=0.0):
    """
    How far each planted foot moves over the ground once the body walks forward at speed (m/s), read from the posed rig: the end of the
    leg's foot bone (or of its lower bone, if it has no foot) counts as planted while it is within 1.5 cm of floor. Returns the worst
    slide in metres, per leg, and prints the share of the clip each foot is planted.
    """
    arm = _c["arm"]
    action = bpy.data.actions[action_name]
    first, last = (int(f) for f in action.frame_range)
    tracks = {name: [] for name in legs}
    for frame in range(first, last + 1):
        use(action_name, frame)
        seconds = (frame - first) / _c["fps"]
        for name in legs:
            leg_data = _c["legs"][name]
            point = arm.pose.bones[leg_data["foot"] or leg_data["lower"]].tail
            tracks[name].append(Vector((point.x, point.y - speed * seconds, point.z)))
    result, planted = {}, {}
    for name in legs:
        worst, anchor, down = 0.0, None, 0
        for p in tracks[name]:
            if p.z < floor + 0.015:
                anchor = anchor or p.copy()
                worst = max(worst, Vector((p.x - anchor.x, p.y - anchor.y)).length)
                down += 1
            else:
                anchor = None
        result[name] = round(worst, 3)
        planted[name] = round(down / len(tracks[name]), 2)
    arm.animation_data.action = None
    print(f"REPORT {action_name}: planted feet slide at most {result} m at {speed} m/s; share of the clip planted {planted}")
    return result


# ---- Pictures ----

def _scene_for_pictures(size):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if scene.world is None:
        world = bpy.data.worlds.new("sky")
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.75, 0.78, 1)
        scene.world = world
    if "pic_sun" not in bpy.data.objects:
        sun = bpy.data.objects.new("pic_sun", bpy.data.lights.new("pic_sun", "SUN"))
        sun.data.energy = 3.5
        sun.rotation_euler = (math.radians(50), 0, math.radians(30))
        scene.collection.objects.link(sun)
    if "pic_eye" not in bpy.data.objects:
        eye = bpy.data.objects.new("pic_eye", bpy.data.cameras.new("pic_eye"))
        scene.collection.objects.link(eye)
        scene.camera = eye
    if "pic_ground" not in bpy.data.objects:
        bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, -0.005))
        ground = bpy.context.object
        ground.name = "pic_ground"
        material = bpy.data.materials.new("pic_ground")
        material.use_nodes = True
        mt._shader(material).inputs["Base Color"].default_value = (0.42, 0.4, 0.36, 1)
        ground.data.materials.append(material)
    return scene, bpy.data.objects["pic_eye"]


def picture(label, direction, centre, distance, size=(480, 360), lens=50):
    """Renders shots/<label>.png from a direction (looking at centre from that side), in the rig's current pose."""
    scene, eye = _scene_for_pictures(size)
    eye.data.type = "PERSP"
    eye.data.lens = lens
    eye.location = Vector(centre) + Vector(direction).normalized() * distance
    eye.rotation_euler = (Vector(centre) - eye.location).to_track_quat("-Z", "Y").to_euler()
    eye.data.clip_start = 0.05
    eye.data.clip_end = distance * 10
    scene.render.filepath = os.path.join(_c["folder"], "shots", label + ".png")
    bpy.ops.render.render(write_still=True)
    return scene.render.filepath


def film(action_name, label, direction, centre, distance, shares=(0, 0.125, 0.25, 0.375, 0.5, 0.625, 0.75, 0.875), size=(320, 280), lens=50):
    """Pictures of a clip at the given shares of its length: shots/<label>_<percent>.png."""
    action = bpy.data.actions[action_name]
    first, last = (int(f) for f in action.frame_range)
    paths = []
    for share in shares:
        use(action_name, int(round(first + (last - first) * share)))
        paths.append(picture(f"{label}_{int(round(share * 1000)):04d}", direction, centre, distance, size, lens))
    _c["arm"].animation_data.action = None
    return paths


def strip(paths, out_name, columns=None):
    """Lays pictures side by side (rows of columns) into shots/<out_name>.png."""
    images = [bpy.data.images.load(p) for p in paths]
    w, h = images[0].size
    columns = columns or len(images)
    rows = math.ceil(len(images) / columns)
    sheet = np.ones((rows * h, columns * w, 4), dtype=np.float32)
    for i, image in enumerate(images):
        data = np.empty(w * h * 4, dtype=np.float32)
        image.pixels.foreach_get(data)
        r, c = i // columns, i % columns
        sheet[(rows - 1 - r) * h:(rows - r) * h, c * w:(c + 1) * w] = data.reshape(h, w, 4)
    out = bpy.data.images.new(out_name, columns * w, rows * h, alpha=True)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = os.path.join(_c["folder"], "shots", out_name + ".png")
    out.file_format = "PNG"
    out.save()
    for image in images:
        bpy.data.images.remove(image)
    return out.filepath_raw


def bone_picture(label, planes=("side", "front", "top"), step=0.25):
    """The skeleton over the model, seen straight from each side through a see-through body, with a grid of step metres (red every metre)."""
    mesh, arm = _c["mesh"], _c["arm"]
    made = []
    low, high = mt._bounds([mesh])
    pad = 0.4
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "OBJECT"
    scene.display.shading.show_xray = True
    scene.display.shading.xray_alpha = 0.45
    mesh.color = (0.75, 0.75, 0.75, 1)
    palette = [(1, 0.1, 0.1), (0.1, 0.7, 0.1), (0.1, 0.3, 1), (1, 0.7, 0), (0.8, 0, 0.8), (0, 0.8, 0.8)]
    for i, bone in enumerate(arm.data.bones):
        head, tail = bone.head_local, bone.tail_local
        bpy.ops.mesh.primitive_cylinder_add(radius=0.025 * max(1, (high - low).length / 4), depth=(tail - head).length, location=(head + tail) / 2, vertices=6)
        stick = bpy.context.object
        stick.rotation_euler = (tail - head).to_track_quat("Z", "Y").to_euler()
        stick.color = (*palette[i % len(palette)], 1)
        made.append(stick)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.04 * max(1, (high - low).length / 4), location=head, segments=8, ring_count=6)
        ball = bpy.context.object
        ball.color = (0, 0, 0, 1)
        made.append(ball)
    eye = bpy.data.objects.new("bone_eye", bpy.data.cameras.new("bone_eye"))
    scene.collection.objects.link(eye)
    scene.camera = eye
    eye.data.type = "ORTHO"
    paths = []
    for plane in planes:
        u, v, depth = {"side": (1, 2, 0), "front": (0, 2, 1), "top": (0, 1, 2)}[plane]
        lo = [math.floor((low[i] - pad) / step) * step for i in range(3)]
        hi = [math.ceil((high[i] + pad) / step) * step for i in range(3)]
        lines = []
        for axis_index, other in ((u, v), (v, u)):
            at = lo[axis_index]
            while at <= hi[axis_index] + 1e-6:
                a, b = [0.0, 0.0, 0.0], [0.0, 0.0, 0.0]
                a[axis_index] = b[axis_index] = at
                a[other], b[other] = lo[other], hi[other]
                a[depth] = b[depth] = (lo[depth] - 5) if plane != "front" else (hi[depth] + 5)
                metre = abs(at - round(at)) < 1e-6
                bpy.ops.mesh.primitive_cylinder_add(radius=step * (0.025 if metre else 0.012), depth=(Vector(b) - Vector(a)).length, location=(Vector(a) + Vector(b)) / 2, vertices=4)
                line = bpy.context.object
                line.rotation_euler = (Vector(b) - Vector(a)).to_track_quat("Z", "Y").to_euler()
                line.color = (0.9, 0.2, 0.2, 1) if metre else (0.35, 0.45, 1, 1)
                lines.append(line)
                at += step
        su, sv = hi[u] - lo[u], hi[v] - lo[v]
        eye.data.ortho_scale = max(su, sv)
        scene.render.resolution_x = int(1000 * su / max(su, sv))
        scene.render.resolution_y = int(1000 * sv / max(su, sv))
        cu, cv = (lo[u] + hi[u]) / 2, (lo[v] + hi[v]) / 2
        if plane == "side":
            eye.location, eye.rotation_euler = (40, cu, cv), (math.radians(90), 0, math.radians(90))
        elif plane == "front":
            eye.location, eye.rotation_euler = (cu, -40, cv), (math.radians(90), 0, 0)
        else:
            eye.location, eye.rotation_euler = (cu, cv, 40), (0, 0, 0)
        eye.data.clip_end = 200
        scene.render.filepath = os.path.join(_c["folder"], "shots", f"{label}_{plane}.png")
        bpy.ops.render.render(write_still=True)
        paths.append(scene.render.filepath)
        print(f"REPORT bone picture {plane}: across {lo[u]:.2f} to {hi[u]:.2f}, up {lo[v]:.2f} to {hi[v]:.2f}")
        for line in lines:
            bpy.data.objects.remove(line)
    for o in made + [eye]:
        bpy.data.objects.remove(o)
    scene.display.shading.show_xray = False
    return paths


def weight_picture(label, direction, centre, distance, size=(560, 420)):
    """The model coloured by the bone each vertex mostly follows, to check the weights."""
    mesh, arm = _c["mesh"], _c["arm"]
    names = [g.name for g in mesh.vertex_groups]
    rng = np.random.default_rng(7)
    colours = {n: (*rng.uniform(0.15, 1.0, 3), 1.0) for n in names}
    attribute = mesh.data.color_attributes.new("bones", "FLOAT_COLOR", "POINT")
    for v in mesh.data.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        attribute.data[v.index].color = colours[names[best.group]] if best else (0, 0, 0, 1)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "VERTEX"
    eye = bpy.data.objects.new("weight_eye", bpy.data.cameras.new("weight_eye"))
    scene.collection.objects.link(eye)
    scene.camera = eye
    eye.location = Vector(centre) + Vector(direction).normalized() * distance
    eye.rotation_euler = (Vector(centre) - eye.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.filepath = os.path.join(_c["folder"], "shots", label + ".png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(eye)
    mesh.data.color_attributes.remove(attribute)
    return scene.render.filepath


# ---- Writing ----

def finish(clips, extra=None):
    """Writes <name>.fbx with every clip, and report.json, which names the clips for the baked body."""
    mesh, arm = _c["mesh"], _c["arm"]
    for o in [o for o in bpy.context.scene.objects if o.name.startswith("pic_")]:
        bpy.data.objects.remove(o)
    arm.animation_data.action = None
    bpy.context.scene.frame_set(1)
    for bone in arm.pose.bones:
        bone.rotation_quaternion = (1, 0, 0, 0)
        bone.location = (0, 0, 0)
        bone.scale = (1, 1, 1)
    mt._select([arm, mesh])
    bpy.ops.export_scene.fbx(filepath=os.path.join(_c["folder"], _c["name"] + ".fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
                             axis_forward="-Z", axis_up="Y", bake_space_transform=False, apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE",
                             path_mode="STRIP", embed_textures=False, add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True,
                             bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    triangles = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    data = {"name": _c["name"], "source": _c["source"], "triangles": triangles, "bones": len(arm.data.bones), "fps": _c["fps"],
            "see_through": _c["cutout"], "clips": {role: "Rig|" + clip_name for role, clip_name in clips.items()}}
    data.update(extra or {})
    with open(os.path.join(_c["folder"], "report.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    print(f"REPORT wrote {_c['name']}.fbx: {triangles} triangles, {len(arm.data.bones)} bones, clips {list(clips.values())}")
