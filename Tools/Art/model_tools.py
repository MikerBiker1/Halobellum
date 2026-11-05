# model_tools.py
# What every unit's preparation script uses in Blender (M9): loads a source model from Art_Source, cleans it up,
# reduces it, marks the parts that move and where its weapons fire from, and writes what Unity needs to
# Art_Source/Prepared/<name>/: <name>.fbx, its textures, materials.json, report.json, and pictures in shots/.
#
# The rules of a prepared model (RealModelBuilder and AnimationBaker in Unity rely on them):
#   - It faces -Y in Blender (Blender's front view), with +Z up. That becomes +Z forward in Unity.
#   - A node "Turret_<weapon>" turns toward that weapon's target. Its origin is what it turns around.
#   - A node "Muzzle_<weapon>" or "Muzzle_<weapon><letter>" is where that weapon's shots start; several letters
#     take turns. Put a muzzle inside the turret that carries it (muzzle(..., parent="Turret_0")).
#   - A node "Wheel_<n>" spins while the unit moves, around the axis across the vehicle.
#   - A node "Swing_<n>_<phase>" swings to and fro while it moves, like a leg. Its origin is the joint. It can go on with
#     "_<degrees>" and "_<cycles per second>", for legs that aren't a big walker's.
#   - A node "Flap_L" or "Flap_R" beats up and down all the time, like a wing. Its origin is the wing's root.
#   - A node "Motion_Hover" or "Motion_Wobble" says how the whole body moves (motion("Hover")).
#   - Everything else is merged by material, to keep the number of draw calls down.
#
# A unit's script lives in Tools/Art/units/<name>.py and is run with:
#   blender --background --factory-startup --python Tools/Art/units/<name>.py
# Needs only Blender's own Python.

import json
import math
import os
import re
import shutil

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.interpolate import poly_3d_calc

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "Art_Source")
PREPARED = os.environ.get("HB_PREPARED") or os.path.join(SOURCE, "Prepared")  # A worktree shares Art_Source with the main project, so it can prepare elsewhere.

MOVING_PART = re.compile(r"^(Turret_|Wheel_|Swing_|Flap_)")
_state = {"name": None, "folder": None, "source": None, "tris_before": 0, "flags": {}, "high": None}


# ---- Loading and looking ----

def start(name, source, rest_pose=False):
    """Empties Blender and loads Art_Source/<source>. All parents and rigs are flattened into plain parts in place. With rest_pose, a rig's clip is dropped first, so it flattens in the rig's own rest pose."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = os.path.join(SOURCE, source)
    if path.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=path)
    elif path.lower().endswith(".stl"):
        bpy.ops.wm.stl_import(filepath=path)
    else:
        bpy.ops.import_scene.gltf(filepath=path)
    folder = os.path.join(PREPARED, name)
    shutil.rmtree(folder, ignore_errors=True)
    os.makedirs(os.path.join(folder, "shots"))
    _state.update(name=name, folder=folder, source=source, flags={}, high=None)

    if rest_pose:
        for o in bpy.context.scene.objects:
            if o.type == "ARMATURE":
                o.animation_data_clear()
                for bone in o.pose.bones:
                    bone.matrix_basis.identity()
    bpy.context.view_layer.update()
    for o in meshes():
        o.data = o.data.copy()  # Parts that share a mesh must not share what happens to it next.
    _select(meshes())
    bpy.ops.object.convert(target="MESH")  # Applies a rig's pose, if it has one.
    for o in meshes():
        world = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = world
    for o in list(bpy.context.scene.objects):
        if o.type != "MESH":
            bpy.data.objects.remove(o)
    _select(meshes())
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    _state["tris_before"] = tris()
    # A model can come in any size: first bring it to about 10 units, so that the pictures and welds work the same.
    size = _bounds()[1] - _bounds()[0]
    biggest = max(size) or 1.0
    _state["to_ten"] = 10.0 / biggest
    scale(10.0 / biggest)
    report("as loaded")
    look("before")


def source_size():
    """Back to the sizes in the file, so that a script can use the numbers read from it. Unity sets the final size anyway."""
    scale(1.0 / _state["to_ten"])
    _state["to_ten"] = 1.0


def meshes(pattern=None):
    found = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if pattern:
        found = [o for o in found if re.search(pattern, o.name)]
    return sorted(found, key=lambda o: o.name)


def tris(objects=None):
    total = 0
    for o in objects if objects is not None else meshes():
        total += sum(len(p.vertices) - 2 for p in o.data.polygons)
    return total


def report(label=""):
    """Prints every part, so that a script's author can see what to work with."""
    low, high = _bounds()
    print(f"REPORT [{label}] {tris()} triangles in {len(meshes())} parts, size {_round(high - low)}, from {_round(low)} to {_round(high)}")
    for o in sorted(meshes(), key=lambda m: -tris([m]))[:40]:
        a, b = _bounds([o])
        materials = [m.name if m else "none" for m in o.data.materials]
        print(f"REPORT   {o.name}: {tris([o])} tris, from {_round(a)} to {_round(b)}, materials {materials}")


def islands(pattern=None, smallest=0):
    """Prints the separate pieces inside the matching parts (each with its box), to find a turret in one big mesh."""
    for o in meshes(pattern):
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        for faces in _islands(mesh):
            if len(faces) < smallest:
                continue
            points = [o.matrix_world @ v.co for f in faces for v in f.verts]
            print(f"REPORT   island of {o.name}: {len(faces)} faces, from {_round(_low(points))} to {_round(_high(points))}")
        mesh.free()


def look(label, camera="quarter", size=(560, 420), samples=16):
    """Renders a picture to shots/<label>.png. Red balls are muzzles, blue balls are turning points, and the green arrow is forward."""
    markers = []
    low, high = _bounds()
    span = (high - low).length
    for o in bpy.context.scene.objects:
        if o.type == "EMPTY" and o.name.startswith("Muzzle"):
            markers.append(_ball(o.matrix_world.translation, span * 0.012, (1, 0, 0)))
        if o.type == "MESH" and MOVING_PART.match(o.name):
            markers.append(_ball(o.matrix_world.translation, span * 0.012, (0, 0.2, 1)))
    centre = (low + high) / 2
    ground = Vector((centre.x, low.y, low.z))
    for i in range(1, 6):
        markers.append(_ball(ground + Vector((0, -span * 0.05 * i, 0)), span * 0.012, (0, 1, 0)))

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = False
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.image_settings.file_format = "PNG"
    if scene.world is None:
        world = bpy.data.worlds.new("sky")
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.75, 0.78, 1)
        scene.world = world
    light = bpy.data.objects.new("look_sun", bpy.data.lights.new("look_sun", "SUN"))
    light.data.energy = 3
    light.rotation_euler = (math.radians(50), 0, math.radians(30))
    scene.collection.objects.link(light)
    eye = bpy.data.objects.new("look_eye", bpy.data.cameras.new("look_eye"))
    scene.collection.objects.link(eye)
    scene.camera = eye
    direction = {"quarter": (0.8, -1, 0.6), "back": (-0.8, 1, 0.6), "side": (1, 0, 0.05), "front": (0, -1, 0.05), "top": (0, -0.01, 1)}[camera]
    eye.location = centre + Vector(direction).normalized() * span * 1.35
    eye.rotation_euler = (centre - eye.location).to_track_quat("-Z", "Y").to_euler()
    eye.data.clip_start = span * 0.01
    eye.data.clip_end = span * 20
    scene.render.filepath = os.path.join(_state["folder"], "shots", label + ".png")
    bpy.ops.render.render(write_still=True)
    for o in markers + [light, eye]:
        bpy.data.objects.remove(o)
    print(f"REPORT picture {scene.render.filepath}")


# ---- Cleaning ----

def remove(pattern):
    """Deletes the parts whose name matches."""
    for o in meshes(pattern):
        bpy.data.objects.remove(o)


def keep_box(low, high, pattern=None):
    """Keeps only the separate pieces whose middle is inside the box (for a file that holds two vehicles, say)."""
    for o in meshes(pattern):
        _delete_islands(o, lambda c: not _inside(c, low, high))
    for o in meshes(pattern):
        if not o.data.polygons:
            bpy.data.objects.remove(o)


def remove_box(low, high, pattern=None):
    """Deletes the separate pieces whose middle is inside the box (a stray sphere, a decal)."""
    for o in meshes(pattern):
        _delete_islands(o, lambda c: _inside(c, low, high))
    for o in meshes(pattern):
        if not o.data.polygons:
            bpy.data.objects.remove(o)


def remove_small_islands(smaller_than, pattern=None):
    """Deletes separate pieces with fewer faces than this: bolts and rivets nobody sees from the game camera."""
    for o in meshes(pattern):
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        doomed = [f for faces in _islands(mesh) if len(faces) < smaller_than for f in faces]
        bmesh.ops.delete(mesh, geom=doomed, context="FACES_ONLY")
        bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
        mesh.to_mesh(o.data)
        mesh.free()


def turn(yaw=0.0, pitch=0.0, roll=0.0):
    """Turns the whole model around the origin, in degrees: yaw around Z, pitch around X, roll around Y."""
    rotation = Matrix.Rotation(math.radians(yaw), 4, "Z") @ Matrix.Rotation(math.radians(pitch), 4, "X") @ Matrix.Rotation(math.radians(roll), 4, "Y")
    for o in meshes():
        o.data.transform(rotation)
        o.data.update()


def scale(factor):
    """Scales the whole model around the origin."""
    for o in meshes():
        o.data.transform(Matrix.Scale(factor, 4))
        o.data.update()


def centre_on_ground():
    """Puts the middle of the model on the origin and its lowest point on Z = 0."""
    low, high = _bounds()
    shift = Vector((-(low.x + high.x) / 2, -(low.y + high.y) / 2, -low.z))
    for o in meshes():
        o.data.transform(Matrix.Translation(shift))
        o.data.update()


def fit(length=10.0):
    """Scales so that the model is this long across the ground, then stands it on the origin. Unity sets its final size."""
    low, high = _bounds()
    scale(length / max(high.x - low.x, high.y - low.y))
    centre_on_ground()


def paint(pattern, colour, material_name=None, glass=False):
    """Gives the matching parts one plain colour (linear RGB, 0 to 1), replacing any material or texture."""
    material_name = material_name or "paint_" + "".join(f"{int(c * 255):02x}" for c in colour)
    material = bpy.data.materials.get(material_name) or bpy.data.materials.new(material_name)
    material.use_nodes = True
    material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*colour, 1)
    if glass:
        flag(material_name, glass=True)
    for o in meshes(pattern):
        o.data.materials.clear()
        o.data.materials.append(material)


def flag(material_name, glass=None, cutout=None, glow=None):
    """Tells Unity how to show a material: glass stays shiny, cutout has see-through holes, glow is a colour that emits."""
    flags = _state["flags"].setdefault(material_name, {})
    if glass is not None:
        flags["glass"] = glass
    if cutout is not None:
        flags["cutout"] = cutout
    if glow is not None:
        flags["glow"] = list(glow)


# ---- Reducing ----

def weld(distance=0.002, pattern=None):
    """Joins vertices at the same place, so that reduction doesn't tear the surface. Distance in units of a 10-unit model."""
    for o in meshes(pattern):
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        bmesh.ops.remove_doubles(mesh, verts=mesh.verts, dist=distance)
        mesh.to_mesh(o.data)
        mesh.free()


def fix_normals(pattern=None):
    """Turns every face of each connected piece the same way, outward. A file made for double-sided drawing can have them any way round, and the game hides back faces."""
    for o in meshes(pattern):
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        before = [f.normal.copy() for f in mesh.faces]
        bmesh.ops.recalc_face_normals(mesh, faces=mesh.faces)
        mesh.normal_update()
        flipped = sum(1 for f, n in zip(mesh.faces, before) if f.normal.dot(n) < 0)
        mesh.to_mesh(o.data)
        mesh.free()
        print(f"REPORT {o.name}: turned {flipped} of {len(before)} faces")


def reduce(target, pattern=None, flat_angle=1.0):
    """Reduces the matching parts to about this many triangles in total: first flat areas, then everywhere evenly."""
    parts = meshes(pattern)
    for o in parts:
        _select([o])
        if flat_angle > 0:
            modifier = o.modifiers.new("flat", "DECIMATE")
            modifier.decimate_type = "DISSOLVE"
            modifier.angle_limit = math.radians(flat_angle)
            modifier.delimit = {"UV", "MATERIAL", "SHARP"}
            bpy.ops.object.modifier_apply(modifier="flat")
    now = tris(parts)
    if now > target:
        ratio = target / now
        for o in parts:
            _select([o])
            modifier = o.modifiers.new("less", "DECIMATE")
            modifier.decimate_type = "COLLAPSE"
            modifier.ratio = ratio
            bpy.ops.object.modifier_apply(modifier="less")
    print(f"REPORT reduced {now} to {tris(parts)} triangles (asked for {target})")


def solid(target, voxel=0.05, pattern=None):
    """
    For cuts too hard for reduce (a weapon to 3% of its triangles): rebuilds the parts as one closed skin, then reduces that.
    It loses UVs, so it is for models that take their colours from snapshot() afterwards. Voxel is in units of a 10-unit model.
    """
    parts = meshes(pattern)
    _select(parts)
    if len(parts) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    modifier = o.modifiers.new("skin", "REMESH")
    modifier.mode = "VOXEL"
    modifier.voxel_size = voxel
    modifier.use_smooth_shade = False
    bpy.ops.object.modifier_apply(modifier="skin")
    modifier = o.modifiers.new("flat", "DECIMATE")
    modifier.decimate_type = "DISSOLVE"
    modifier.angle_limit = math.radians(2.0)
    bpy.ops.object.modifier_apply(modifier="flat")
    now = tris([o])
    if now > target:
        modifier = o.modifiers.new("less", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = target / now
        bpy.ops.object.modifier_apply(modifier="less")
    print(f"REPORT solid: {now} to {tris([o])} triangles (asked for {target})")


def smooth(angle=35.0, pattern=None):
    """Smooth shading with sharp edges past this angle, in place of whatever normals the model came with."""
    for o in meshes(pattern):
        _select([o])
        if o.data.has_custom_normals:
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
        o.data.shade_smooth()
        o.data.set_sharp_from_angle(angle=math.radians(angle))


def bake_one(size=1024, angle=66.0, island_margin=0.004):
    """
    Joins everything and paints all its materials' colours into one new texture, on a new UV map. It costs one draw call, and it is
    what Meshy's rigging asks for. Glow and normal maps are left behind. Do it after reduce, and before cutting out moving parts.
    """
    parts = meshes()
    _select(parts)
    if len(parts) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = "Body"
    old = o.data.uv_layers.active
    old_name = old.name if old else None
    baked = o.data.uv_layers.new(name="Baked")
    o.data.uv_layers.active = baked
    if old_name:
        o.data.uv_layers[old_name].active_render = True
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=island_margin)
    bpy.ops.object.mode_set(mode="OBJECT")
    image = bpy.data.images.new(_state["name"] + "_baked", size, size, alpha=False)
    for m in o.data.materials:
        if m is None:
            continue
        m.use_nodes = True
        node = m.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = image
        node.select = True
        m.node_tree.nodes.active = node
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    settings = scene.render.bake
    settings.use_pass_direct = False
    settings.use_pass_indirect = False
    settings.use_pass_color = True
    settings.margin = 8
    bpy.ops.object.bake(type="DIFFUSE")
    image.pack()
    print(f"REPORT baked {len(o.data.materials)} materials into one {size} texture")
    material = bpy.data.materials.new("baked")
    material.use_nodes = True
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    material.node_tree.links.new(node.outputs["Color"], material.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    o.data.materials.clear()
    o.data.materials.append(material)
    for polygon in o.data.polygons:
        polygon.material_index = 0
    for layer in [l for l in o.data.uv_layers if l.name != "Baked"]:
        o.data.uv_layers.remove(layer)
    o.data.uv_layers["Baked"].active_render = True
    return image


def reduce_and_repaint(target, size=1024, weld_distance=0.0004, reach=0.02):
    """
    For a model with far too many triangles whose surface is split along its texture's seams, as Meshy's are: reducing that tears
    cracks along every seam. So it is welded whole, reduced, unwrapped afresh, and the detailed model's colours are painted onto it.
    Distances are in the model's present size.
    """
    parts = meshes()
    _select(parts)
    if len(parts) > 1:
        bpy.ops.object.join()
    high = bpy.context.view_layer.objects.active
    high.name = "High"
    low = high.copy()
    low.data = high.data.copy()
    low.name = "Body"
    bpy.context.scene.collection.objects.link(low)
    for layer in list(low.data.uv_layers):
        low.data.uv_layers.remove(layer)
    low.data.materials.clear()
    weld(weld_distance, "^Body$")
    _select([low])
    modifier = low.modifiers.new("less", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = min(1.0, target / max(1, tris([low])))
    bpy.ops.object.modifier_apply(modifier="less")
    print(f"REPORT repainting: {tris([high])} to {tris([low])} triangles")
    low.data.uv_layers.new(name="Baked")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    image = bpy.data.images.new(_state["name"] + "_baked", size, size, alpha=False)
    material = bpy.data.materials.new("baked")
    material.use_nodes = True
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    node.select = True
    material.node_tree.nodes.active = node
    material.node_tree.links.new(node.outputs["Color"], material.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    low.data.materials.append(material)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    settings = scene.render.bake
    settings.use_pass_direct = False
    settings.use_pass_indirect = False
    settings.use_pass_color = True
    settings.margin = 8
    settings.use_selected_to_active = True
    settings.cage_extrusion = reach
    settings.max_ray_distance = reach * 4
    bpy.ops.object.select_all(action="DESELECT")
    high.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    bpy.ops.object.bake(type="DIFFUSE")
    settings.use_selected_to_active = False
    image.pack()
    bpy.data.objects.remove(high)
    return image


# ---- Moving parts and muzzles ----

def part(name, pattern, pivot=None):
    """Makes one named part out of the matching parts. Pivot is what it turns around; by default the middle of its base."""
    found = meshes(pattern)
    if not found:
        raise ValueError(f"No part matches {pattern}")
    _select(found)
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    _set_origin(o, pivot)
    return o


def take(name, low, high, pattern=None, pivot=None, whole_pieces=True):
    """Makes a named part out of whatever is inside the box: whole separate pieces, or single faces if told so."""
    taken = []
    for o in meshes(pattern):
        if MOVING_PART.match(o.name):
            continue
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        groups = _islands(mesh) if whole_pieces else [[f] for f in mesh.faces]
        chosen = [f for faces in groups if _inside(_middle(o, faces), low, high) for f in faces]
        if not chosen:
            mesh.free()
            continue
        copy = o.copy()
        copy.data = o.data.copy()
        bpy.context.scene.collection.objects.link(copy)
        other = bmesh.new()
        other.from_mesh(o.data)
        other.faces.ensure_lookup_table()
        keep = {f.index for f in chosen}
        bmesh.ops.delete(other, geom=[f for f in other.faces if f.index in keep], context="FACES_ONLY")
        bmesh.ops.delete(other, geom=[v for v in other.verts if not v.link_faces], context="VERTS")
        other.to_mesh(o.data)
        other.free()
        bmesh.ops.delete(mesh, geom=[f for f in mesh.faces if f.index not in keep], context="FACES_ONLY")
        bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
        mesh.to_mesh(copy.data)
        mesh.free()
        taken.append(copy)
    if not taken:
        raise ValueError(f"Nothing inside the box for {name}")
    _select(taken)
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    _set_origin(o, pivot)
    print(f"REPORT took {name}: {tris([o])} triangles")
    return o


def muzzle(name, at, parent=None):
    """Marks where a weapon's shots start. Goes inside the turret that carries it, if any."""
    empty = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(empty)
    empty.location = Vector(at)
    if parent:
        carrier = bpy.data.objects[parent]
        bpy.context.view_layer.update()
        empty.parent = carrier
        empty.matrix_parent_inverse = carrier.matrix_world.inverted()
    return empty


def motion(kind):
    """Marks how the whole body moves in battle: "Hover" (a little bob) or "Wobble" (sways and squashes, for Flood forms)."""
    empty = bpy.data.objects.new("Motion_" + kind, None)
    bpy.context.scene.collection.objects.link(empty)
    return empty


def merge_rest():
    """Joins everything that doesn't move, by material."""
    still = [o for o in meshes() if not MOVING_PART.match(o.name)]
    for o in still:
        if len(o.data.materials) > 1:
            _select([o])
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.select_all(action="SELECT")
            bpy.ops.mesh.separate(type="MATERIAL")
            bpy.ops.object.mode_set(mode="OBJECT")
    groups = {}
    for o in meshes():
        if not MOVING_PART.match(o.name):
            groups.setdefault(o.data.materials[0].name if o.data.materials else "", []).append(o)
    for i, (_, group) in enumerate(sorted(groups.items())):
        _select(group)
        bpy.ops.object.join()
        bpy.context.view_layer.objects.active.name = f"Body_{i}"


# ---- Finishing ----

def finish(texture_size=512):
    """Writes the FBX, the textures and materials.json, then pictures from four sides."""
    name, folder = _state["name"], _state["folder"]
    bpy.context.view_layer.update()
    table = _write_materials(texture_size)
    _export(os.path.join(folder, name + ".fbx"))
    for camera in ("quarter", "side", "front", "top"):
        look("after_" + camera, camera)
    _write_report(table)


def snapshot():
    """Remembers the detailed model now, so that a weapon's colours can be read from it after reduction."""
    _state["high"] = _triangles_of(meshes())


def finish_weapon(grip, tip, length_note):
    """
    A weapon goes in the hand of a baked body, so it is one mesh with a colour per face and no texture.
    Its origin becomes the grip, it points along -Y, and a node "Muzzle" marks the tip of its barrel. Unity sets its size.
    """
    name, folder = _state["name"], _state["folder"]
    _select(meshes())
    if len(meshes()) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = "Weapon"
    high = _state["high"] or _triangles_of([o])
    _paint_faces_from(o, high)
    shift = -Vector(grip)
    o.data.transform(Matrix.Translation(shift))
    o.data.update()
    muzzle("Muzzle", Vector(tip) + shift)
    _export(os.path.join(folder, name + ".fbx"), colours=True)
    _flat_preview(o)
    for camera in ("quarter", "side", "top"):
        look("after_" + camera, camera)
    _write_report({}, {"length": length_note})


# ---- Inside ----

def _round(v):
    return tuple(round(x, 3) for x in v)


def _low(points):
    return Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))


def _high(points):
    return Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))


def _bounds(objects=None):
    points = [o.matrix_world @ v.co for o in (objects if objects is not None else meshes()) for v in o.data.vertices]
    return (_low(points), _high(points)) if points else (Vector((0, 0, 0)), Vector((1, 1, 1)))


def _inside(point, low, high):
    return all(low[i] <= point[i] <= high[i] for i in range(3))


def _middle(o, faces):
    points = [o.matrix_world @ v.co for f in faces for v in f.verts]
    return (_low(points) + _high(points)) / 2


def _select(objects):
    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in objects:
        o.select_set(True)
    if objects:
        bpy.context.view_layer.objects.active = objects[0]


def _islands(mesh):
    """The separate pieces of a bmesh, as lists of faces."""
    seen, result = set(), []
    for start_face in mesh.faces:
        if start_face.index in seen:
            continue
        stack, faces = [start_face], []
        seen.add(start_face.index)
        while stack:
            face = stack.pop()
            faces.append(face)
            for vert in face.verts:
                for other in vert.link_faces:
                    if other.index not in seen:
                        seen.add(other.index)
                        stack.append(other)
        result.append(faces)
    return result


def _delete_islands(o, doomed):
    mesh = bmesh.new()
    mesh.from_mesh(o.data)
    faces = [f for group in _islands(mesh) if doomed(_middle(o, group)) for f in group]
    bmesh.ops.delete(mesh, geom=faces, context="FACES_ONLY")
    bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
    mesh.to_mesh(o.data)
    mesh.free()
    o.data.update()


def _set_origin(o, pivot):
    low, high = _bounds([o])
    point = Vector(pivot) if pivot is not None else Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    o.data.transform(Matrix.Translation(-point))
    o.data.update()
    o.matrix_world = Matrix.Translation(point)


def _ball(at, radius, colour):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=at, segments=12, ring_count=8)
    ball = bpy.context.object
    material = bpy.data.materials.new("marker")
    material.use_nodes = True
    node = material.node_tree.nodes["Principled BSDF"]
    node.inputs["Base Color"].default_value = (*colour, 1)
    node.inputs["Emission Color"].default_value = (*colour, 1)
    node.inputs["Emission Strength"].default_value = 2
    ball.data.materials.append(material)
    ball.name = "look_marker"
    return ball


def _upstream_image(node, seen=None):
    """The image feeding a node, looking back through whatever sits between them."""
    seen = seen or set()
    if node in seen:
        return None
    seen.add(node)
    if node.type == "TEX_IMAGE" and node.image:
        return node.image
    for socket in node.inputs:
        for link in socket.links:
            image = _upstream_image(link.from_node, seen)
            if image:
                return image
    return None


def _shader(material):
    if not material or not material.use_nodes:
        return None
    return next((n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)


def _input_image(material, socket_name):
    shader = _shader(material)
    if shader is None or socket_name not in shader.inputs or not shader.inputs[socket_name].links:
        return None
    image = _upstream_image(shader.inputs[socket_name].links[0].from_node)
    return image if image and image.size[0] > 0 else None


def _save_scaled(image, path, size):
    copy = image.copy()
    if copy.size[0] > size or copy.size[1] > size:
        factor = size / max(copy.size)
        copy.scale(max(1, int(copy.size[0] * factor)), max(1, int(copy.size[1] * factor)))
    copy.filepath_raw = path
    copy.file_format = "PNG"
    copy.save()
    bpy.data.images.remove(copy)


def _write_materials(texture_size):
    """Each material gets a plain name, and what Unity needs to know about it goes in materials.json."""
    name, folder = _state["name"], _state["folder"]
    used = []
    for o in meshes():
        for m in o.data.materials:
            if m and m not in used:
                used.append(m)
    table = {}
    for i, m in enumerate(used):
        flags = _state["flags"].get(m.name, {})
        new_name = f"{name}_{i}"
        shader = _shader(m)
        entry = {"colour": [1, 1, 1], "glass": flags.get("glass", False), "cutout": flags.get("cutout", False)}
        if shader is not None:
            entry["colour"] = [round(c, 3) for c in shader.inputs["Base Color"].default_value[:3]]
        albedo = _input_image(m, "Base Color")
        if albedo is not None:
            _save_scaled(albedo, os.path.join(folder, new_name + ".png"), texture_size)
            entry["texture"] = new_name + ".png"
            entry["colour"] = [1, 1, 1]
            alpha = shader.inputs["Alpha"]
            if alpha.links and "cutout" not in flags and "glass" not in flags:
                entry["cutout"] = True
        emission = _input_image(m, "Emission Color")
        if emission is not None:
            _save_scaled(emission, os.path.join(folder, new_name + "_emit.png"), min(texture_size, 256))
            entry["glowTexture"] = new_name + "_emit.png"
        if "glow" in flags:
            entry["glow"] = flags["glow"]
        m.name = new_name
        table[new_name] = entry
    with open(os.path.join(folder, "materials.json"), "w", encoding="utf-8") as f:
        json.dump({"materials": [dict(name=k, **v) for k, v in table.items()]}, f, indent=1)
    return table


def _export(path, colours=False):
    bpy.ops.export_scene.fbx(filepath=path, object_types={"MESH", "EMPTY"}, axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, bake_anim=False,
                             colors_type="LINEAR" if colours else "NONE")


def _write_report(table, extra=None):
    low, high = _bounds()
    data = {
        "name": _state["name"], "source": _state["source"], "tris_before": _state["tris_before"], "tris_after": tris(),
        "parts": [{"name": o.name, "tris": tris([o])} for o in meshes()],
        "nodes": [o.name for o in bpy.context.scene.objects if o.type == "EMPTY"],
        "materials": table, "size": _round(high - low),
    }
    data.update(extra or {})
    with open(os.path.join(_state["folder"], "report.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    print(f"REPORT finished {data['name']}: {data['tris_before']} to {data['tris_after']} triangles, {len(data['parts'])} parts, {len(table)} materials")


def _pixels(image):
    if image.name not in _pixels.cache:
        width, height = image.size
        data = np.empty(width * height * 4, dtype=np.float32)
        image.pixels.foreach_get(data)
        data = data.reshape(height, width, 4)
        if image.colorspace_settings.name.lower().startswith("srgb") and not image.is_float:
            rgb = data[:, :, :3]
            data[:, :, :3] = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
        _pixels.cache[image.name] = data
    return _pixels.cache[image.name]


_pixels.cache = {}


def _triangles_of(objects):
    """A plain copy of a model's triangles with their UVs and images or colours, to read colours from later."""
    points, faces, info = [], [], []
    for o in objects:
        o.data.calc_loop_triangles()
        uvs = o.data.uv_layers.active.data if o.data.uv_layers.active else None
        for t in o.data.loop_triangles:
            base = len(points)
            points.extend(o.matrix_world @ o.data.vertices[v].co for v in t.vertices)
            faces.append((base, base + 1, base + 2))
            material = o.data.materials[t.material_index] if o.data.materials else None
            image = _input_image(material, "Base Color")
            shader = _shader(material)
            colour = tuple(shader.inputs["Base Color"].default_value[:3]) if shader is not None else (0.5, 0.5, 0.5)
            info.append((tuple(uvs[i].uv.copy() for i in t.loops) if uvs else None, image, colour))
    return BVHTree.FromPolygons(points, faces), points, faces, info


def _paint_faces_from(o, high):
    """Gives each face of the reduced weapon the average colour of the detailed model under it."""
    tree, points, faces, info = high
    attribute = o.data.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    for polygon in o.data.polygons:
        corners = [o.matrix_world @ o.data.vertices[v].co for v in polygon.vertices]
        middle = sum(corners, Vector()) / len(corners)
        samples = [middle] + [middle.lerp(c, 0.6) for c in corners]
        total = np.zeros(3)
        for sample in samples:
            location, _, index, _ = tree.find_nearest(sample)
            uv, image, colour = info[index]
            if uv and image:
                a, b, c = (points[i] for i in faces[index])
                weights = poly_3d_calc([a, b, c], location)
                u = sum(uv[i][0] * weights[i] for i in range(3))
                v = sum(uv[i][1] * weights[i] for i in range(3))
                pixels = _pixels(image)
                total += pixels[int((v % 1) * pixels.shape[0]) % pixels.shape[0], int((u % 1) * pixels.shape[1]) % pixels.shape[1], :3]
            else:
                total += np.array(colour)
        result = (*(total / len(samples)), 1.0)
        for loop in polygon.loop_indices:
            attribute.data[loop].color = result


def _flat_preview(o):
    """Shows the face colours in the pictures, as the game will."""
    material = bpy.data.materials.new("face_colours")
    material.use_nodes = True
    node = material.node_tree.nodes.new("ShaderNodeVertexColor")
    node.layer_name = "Col"
    material.node_tree.links.new(node.outputs["Color"], material.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    o.data.materials.clear()
    o.data.materials.append(material)
