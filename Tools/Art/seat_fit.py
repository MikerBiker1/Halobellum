# seat_fit.py
# Sets the posed drivers (drivers.py) in their vehicles' seats and takes pictures, to find and check where each one sits (D99). Each vehicle is
# loaded from its folder in Art/Imported/Models and sized and placed as RealModelBuilder does: its longest side across the ground (or its height)
# made its size in the game, centred, on the ground. A seat is where the driver's hips go, in metres, the vehicle facing -Y as in Blender;
# RealModelBuilder takes it in Unity's axes, which this prints: (x, y, z) in Unity is (-x, z, -y) here.
#   blender --background --factory-startup --python Tools/Art/seat_fit.py
#   HB_SEATS=Warthog,Ghost (only those); HB_BARE=1 (the vehicles alone, on a one-metre grid, to measure them)
# Needs only Blender's own Python.

import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODELS = os.path.join(ROOT, "Assets", "_Project", "Art", "Imported", "Models")
DRIVERS = os.path.join(os.environ.get("HB_PREPARED") or os.path.join(ROOT, "Art_Source", "Prepared"), "Drivers")
OUT = os.path.join(DRIVERS, "shots")

# Each soldier's height standing, in metres in the game.
HEIGHT = {"Marine": 1.85, "Grunt": 1.4, "Brute": 2.55, "CombatForm": 2.15, "Elite": 2.4}

# Each vehicle: its model folder, its size (("length", m) or ("height", m)) as in RealModelBuilder, and its seats:
# (driver, hips (x, y, z) in metres here, yaw in degrees, the node it turns with or None).
VEHICLES = {
    "Warthog": ("Warthog", ("length", 4.6), [("Marine_Drive", (0.45, 0.2, 1.08), 0, None), ("Marine_Gun", (0.0, 1.75, 1.85), 0, "Turret_0")]),
    "Ghost": ("Ghost", ("length", 4.4), [("Grunt_Ride", (0.0, 1.0, 1.0), 0, None)]),
    "Marauder": ("Marauder", ("length", 5.8), [("Brute_Drive", (0.0, 0.55, 1.55), 0, None)]),
    # Its seat is the low saddle behind the tank (y 1.0 to 1.5, at 0.95 m), its foot rests either side ahead of it (0.6 m out, at 0.7 m).
    "Chopper": ("Chopper", ("length", 4.8), [("Brute_Chopper", (0.0, 1.22, 1.08), 0, None)]),
    "Elephant": ("Elephant", ("length", 10.0), [("Marine_Gun", (-1.61, -3.25, 2.72), 0, "Turret_2")]),
    "GaussWarthog": ("GaussWarthog", ("length", 4.6), [("Marine_Drive", (0.45, 0.2, 1.08), 0, None), ("Marine_Gun", (0.0, 1.75, 1.85), 0, "Turret_0")]),
    # Looked at and found closed (no seat to be seen), kept to look again: the Gremlin, the Grizzly, the Sparrowhawk, the Reaver; the Hornet's
    # canopy is opaque in the game, so its pilot could not be seen either.
    "Gremlin": ("Gremlin", ("length", 4.6), []),
    "Grizzly": ("Grizzly", ("length", 7.2), []),
    "Sparrowhawk": ("Sparrowhawk", ("length", 7.0), []),
    "Reaver": ("Reaver", ("height", 5.0), []),
    # Its hatch is the round disc on top of the front dome (part of the hull, so it doesn't turn), at 1.98 m.
    "Wraith": ("Wraith", ("length", 6.2), [("Elite_Hatch", (0.0, -0.48, 1.84), 0, None)]),
    "InfestedWraith": ("Wraith", ("length", 6.2), [("CombatForm_Hatch", (0.0, -0.48, 1.84), 0, None)]),
    "InfestedWarthog": ("Warthog", ("length", 4.6), [("CombatForm_Drive", (0.45, 0.25, 1.05), 0, None), ("CombatForm_Gun", (0.0, 1.8, 1.75), 0, "Turret_0")]),
    "InfestedGhost": ("Ghost", ("length", 4.4), [("CombatForm_Ride", (0.0, 1.05, 0.98), 0, None)]),
}


def load_vehicle(folder, size):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, folder, folder + ".fbx"))
    paint(os.path.join(MODELS, folder))
    objects = list(bpy.context.scene.objects)
    meshes = [o for o in objects if o.type == "MESH"]
    points = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    extent = high - low
    scale = size[1] / (max(extent.x, extent.y) if size[0] == "length" else extent.z)
    centre = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    holder = bpy.data.objects.new("Holder", None)
    bpy.context.scene.collection.objects.link(holder)
    for o in objects:
        if o.parent is None:
            o.parent = holder
    holder.matrix_world = Matrix.Scale(scale, 4) @ Matrix.Translation(-centre)
    bpy.context.view_layer.update()
    print(f"REPORT {folder}: scaled by {scale:.4f} to {tuple(round(v * scale, 2) for v in extent)}")
    for o in objects:
        if o.name.startswith(("Turret_", "Muzzle_")):
            print(f"REPORT {folder} node {o.name} at {tuple(round(v, 2) for v in o.matrix_world.translation)}")
    return extent * scale


def paint(folder):
    """Gives the imported materials their textures from materials.json, as RealModelBuilder does: the prepared files carry none."""
    path = os.path.join(folder, "materials.json")
    if not os.path.exists(path):
        return
    table = {m["name"]: m for m in json.load(open(path, encoding="utf-8"))["materials"]}
    for material in bpy.data.materials:
        entry = table.get(material.name) or next((m for n, m in table.items() if material.name.startswith(n)), None)
        if not entry or not entry.get("texture"):
            continue
        material.use_nodes = True
        nodes = material.node_tree.nodes
        image = nodes.new("ShaderNodeTexImage")
        image.image = bpy.data.images.load(os.path.join(folder, entry["texture"]), check_existing=True)
        nodes.active = image
        shader = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
        if shader:
            material.node_tree.links.new(image.outputs["Color"], shader.inputs["Base Color"])


def add_driver(name, hips, yaw):
    soldier = name.split("_")[0]
    bpy.ops.import_scene.fbx(filepath=os.path.join(DRIVERS, name + ".fbx"))
    texture = os.path.join(DRIVERS, name.split("_")[0] + ".png")
    for material in bpy.context.selected_objects[0].data.materials:
        material.use_nodes = True
        image = material.node_tree.nodes.new("ShaderNodeTexImage")
        image.image = bpy.data.images.load(texture, check_existing=True)
        material.node_tree.nodes.active = image
    body = bpy.context.selected_objects[0]
    body.matrix_world = Matrix.Translation(Vector(hips)) @ Matrix.Rotation(math.radians(yaw), 4, "Z") @ Matrix.Scale(HEIGHT[soldier], 4) @ body.matrix_world
    return body


def grid(extent):
    """A one-metre grid on the ground, and upright ones across and along, to measure by."""
    mesh = bpy.data.meshes.new("grid")
    lines, verts = [], []
    reach = int(max(extent.x, extent.y) / 2) + 2
    for i in range(-reach, reach + 1):
        for a, b in (((i, -reach, 0.01), (i, reach, 0.01)), ((-reach, i, 0.01), (reach, i, 0.01)), ((-reach, 0, i + reach), (reach, 0, i + reach)),
                     ((i, 0, 0), (i, 0, 2 * reach)), ((0, -reach, i + reach), (0, reach, i + reach)), ((0, i, 0), (0, i, 2 * reach))):
            if a[2] < 0 or b[2] < 0 or a[2] > extent.z + 1.5 or b[2] > extent.z + 1.5:
                continue
            verts += [a, b]
            lines.append((len(verts) - 2, len(verts) - 1))
    mesh.from_pydata(verts, lines, [])
    o = bpy.data.objects.new("grid", mesh)
    bpy.context.scene.collection.objects.link(o)
    wire = o.modifiers.new("wire", "SKIN")
    for v in o.data.skin_vertices[0].data:
        v.radius = (0.008, 0.008)


def pictures(label, extent):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = os.environ.get("HB_COLOURS", "TEXTURE")  # RANDOM: each object its own colour, the drivers standing out.
    scene.render.resolution_x, scene.render.resolution_y = 640, 480
    data = bpy.data.cameras.new("cam")
    data.type = "ORTHO"
    camera = bpy.data.objects.new("cam", data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    span = max(extent.x, extent.y) * 1.15
    middle = Vector((0, 0, extent.z / 2))
    for view, at, scale in (("side", Vector((1, 0, 0)), span), ("top", Vector((0, 0.001, 1)), span), ("quarter", Vector((0.9, -0.9, 0.7)), span),
                            ("close", Vector((0.7, -0.5, 0.8)), span * 0.55)):
        data.ortho_scale = scale
        camera.location = middle + at.normalized() * 30
        camera.rotation_euler = (middle - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(OUT, f"seat_{label}_{view}.png")
        bpy.ops.render.render(write_still=True)
    print(f"REPORT pictures seat_{label}: across the side and top views {span:.2f} m, 640 px; middle at height {extent.z / 2:.2f}")


def probe(extent):
    """The surface under points across the vehicle: down its middle every 10 cm and across at HB_ACROSS (x values), printing height and part."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    span = extent.y / 2
    for x in [float(v) for v in os.environ.get("HB_ACROSS", "0").split(",")]:
        line = []
        steps = int(span * 2 / 0.1)
        for i in range(steps + 1):
            y = -span + i * 0.1
            hit, where, _, _, thing, _ = bpy.context.scene.ray_cast(depsgraph, Vector((x, y, extent.z + 5)), Vector((0, 0, -1)))
            line.append(f"{y:+.1f}:{where.z:.2f}{'' if not hit else ('/' + thing.name[:10])}" if hit else f"{y:+.1f}:-")
        print(f"REPORT probe x={x}: " + " ".join(line))


chosen = os.environ.get("HB_SEATS", "").split(",") if os.environ.get("HB_SEATS") else list(VEHICLES)
bare = bool(os.environ.get("HB_BARE"))
os.makedirs(OUT, exist_ok=True)
for key in chosen:
    folder, size, seats = VEHICLES[key]
    extent = load_vehicle(folder, size)
    if os.environ.get("HB_PROBE"):
        probe(extent)
        continue
    if bare:
        grid(extent)
    else:
        for name, hips, yaw, node in seats:
            add_driver(name, hips, yaw)
            unity = (-hips[0], hips[2], -hips[1])
            print(f"REPORT seat {key}: {name} hips at {hips} here, Unity ({unity[0]:.2f}f, {unity[1]:.2f}f, {unity[2]:.2f}f), yaw {yaw}, turns with {node}")
    pictures(key + ("_bare" if bare else ""), extent)
