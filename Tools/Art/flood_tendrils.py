# flood_tendrils.py
# Flood tendrils on the infested vehicles' textures (the owner, 2026-11-07, D82: "the green colour on infested vehicles is not good, it should be
# flood colour, sickly brown/beige, in tendrils"). The infested vehicles share their model and texture with the clean ones, and were only washed
# green by RealModelBuilder. This bakes a copy of each texture, <texture>_Flood.png beside it, with a network of beige veins rimmed in dark brown
# and fleshy patches here and there, over the vehicle's own paint made dirtier. The veins are laid out in 3D, in metres along the vehicle as the
# game sizes it, and baked into the texture, so they run on across the texture's seams. RealModelBuilder uses the copies for the infested ones.
#   blender --background --factory-startup --python Tools/Art/flood_tendrils.py -- Warthog:4.6 Pelican:10 ...
# Each argument is a model folder in Art/Imported/Models and the vehicle's length in metres, as in RealModelBuilder.
import json
import os
import sys

import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODELS = os.path.join(ROOT, "Assets", "_Project", "Art", "Imported", "Models")

CELL = 1.25         # Metres across one cell of the big veins.
FINE_CELL = 0.5     # And of the finer ones, which grow only where the Flood has taken hold.
PATCH = 2.8         # Metres across the noise that places fleshy patches and decides where the veins are thick.
BEIGE = (0.62, 0.50, 0.32)       # Linear: a sickly beige, about #CDB892 on screen.
PALE = (0.78, 0.66, 0.46)        # Its paler streaks.
BROWN = (0.10, 0.055, 0.025)     # The dark rim, about #5A3F2B.
GRIME = (1.0, 0.86, 0.66)        # The vehicle's own paint, warmed and darkened, as if stained.


def socket(sockets, name, kind=None):
    for s in sockets:
        if s.name == name and s.enabled and (kind is None or s.type == kind):
            return s
    raise KeyError(name)


def node(tree, kind, **values):
    n = tree.nodes.new(kind)
    for key, value in values.items():
        setattr(n, key, value)
    return n


def map_range(tree, value, low, high, invert=False):
    m = node(tree, "ShaderNodeMapRange", interpolation_type="SMOOTHSTEP")
    tree.links.new(value, m.inputs["Value"])
    m.inputs["From Min"].default_value, m.inputs["From Max"].default_value = low, high
    m.inputs["To Min"].default_value, m.inputs["To Max"].default_value = (1.0, 0.0) if invert else (0.0, 1.0)
    return m.outputs["Result"]


def maths(tree, op, a, b):
    m = node(tree, "ShaderNodeMath", operation=op)
    for i, v in enumerate((a, b)):
        if isinstance(v, float):
            m.inputs[i].default_value = v
        else:
            tree.links.new(v, m.inputs[i])
    return m.outputs[0]


def mix(tree, factor, a, b, blend="MIX"):
    m = node(tree, "ShaderNodeMix", data_type="RGBA", blend_type=blend)
    if isinstance(factor, float):
        socket(m.inputs, "Factor", "VALUE").default_value = factor
    else:
        tree.links.new(factor, socket(m.inputs, "Factor", "VALUE"))
    for name, v in (("A", a), ("B", b)):
        s = socket(m.inputs, name, "RGBA")
        if isinstance(v, tuple):
            s.default_value = (*v, 1.0)
        else:
            tree.links.new(v, s)
    return socket(m.outputs, "Result", "RGBA")


def edge_distance(tree, position, cell):
    v = node(tree, "ShaderNodeTexVoronoi", voronoi_dimensions="3D", feature="DISTANCE_TO_EDGE")
    v.inputs["Scale"].default_value = 1.0 / cell
    tree.links.new(position, v.inputs["Vector"])
    return v.outputs["Distance"]


def flood_material(material, texture, metres):
    """Rebuilds the material as its texture with the Flood over it, and adds the image the bake goes into."""
    tree = material.node_tree
    tree.nodes.clear()
    out = node(tree, "ShaderNodeOutputMaterial")
    bsdf = node(tree, "ShaderNodeBsdfPrincipled")
    socket(bsdf.inputs, "Metallic").default_value = 0.0
    tree.links.new(bsdf.outputs[0], out.inputs["Surface"])

    image = node(tree, "ShaderNodeTexImage", image=texture, interpolation="Closest")
    # World position in metres, bent by noise so the cells are not straight-edged.
    geometry = node(tree, "ShaderNodeNewGeometry")
    scale = node(tree, "ShaderNodeVectorMath", operation="SCALE")
    tree.links.new(geometry.outputs["Position"], scale.inputs[0])
    socket(scale.inputs, "Scale").default_value = metres
    position = scale.outputs[0]
    warp = node(tree, "ShaderNodeTexNoise", noise_dimensions="3D")
    warp.inputs["Scale"].default_value = 0.9
    warp.inputs["Detail"].default_value = 3.0
    tree.links.new(position, warp.inputs["Vector"])
    centred = node(tree, "ShaderNodeVectorMath", operation="MULTIPLY_ADD")
    tree.links.new(warp.outputs["Color"], centred.inputs[0])
    centred.inputs[1].default_value = (0.9, 0.9, 0.9)
    centred.inputs[2].default_value = (-0.45, -0.45, -0.45)
    bent = node(tree, "ShaderNodeVectorMath", operation="ADD")
    tree.links.new(position, bent.inputs[0])
    tree.links.new(centred.outputs[0], bent.inputs[1])
    bent = bent.outputs[0]

    big = node(tree, "ShaderNodeTexNoise", noise_dimensions="3D")
    big.inputs["Scale"].default_value = 1.0 / PATCH
    big.inputs["Detail"].default_value = 4.0
    tree.links.new(position, big.inputs["Vector"])
    taken = big.outputs["Fac"]  # How far the Flood has taken hold here, 0 to 1.

    e1 = edge_distance(tree, bent, CELL)
    e2 = edge_distance(tree, bent, FINE_CELL)
    # The veins are thicker where the Flood has taken hold, thin threads where it has barely reached.
    width = map_range(tree, taken, 0.3, 0.65)
    core1 = map_range(tree, maths(tree, "DIVIDE", e1, maths(tree, "ADD", maths(tree, "MULTIPLY", width, 0.06), 0.02)), 0.6, 1.0, invert=True)
    rim1 = map_range(tree, maths(tree, "DIVIDE", e1, maths(tree, "ADD", maths(tree, "MULTIPLY", width, 0.11), 0.04)), 0.6, 1.0, invert=True)
    fine_here = map_range(tree, taken, 0.45, 0.6)
    core2 = maths(tree, "MULTIPLY", map_range(tree, e2, 0.012, 0.03, invert=True), fine_here)
    rim2 = maths(tree, "MULTIPLY", map_range(tree, e2, 0.025, 0.055, invert=True), fine_here)
    patch_core = map_range(tree, taken, 0.69, 0.73)
    patch_rim = map_range(tree, taken, 0.65, 0.7)
    # Some cell edges fade out, so the veins branch and end like tendrils instead of closing into cells like cracked paint.
    breaks = node(tree, "ShaderNodeTexNoise", noise_dimensions="3D")
    breaks.inputs["Scale"].default_value = 1.0 / 1.6
    breaks.inputs["Detail"].default_value = 2.0
    tree.links.new(position, breaks.inputs["Vector"])
    strands = map_range(tree, breaks.outputs["Fac"], 0.47, 0.53)
    core1, rim1 = maths(tree, "MULTIPLY", core1, strands), maths(tree, "MULTIPLY", rim1, strands)
    core = maths(tree, "MAXIMUM", maths(tree, "MAXIMUM", core1, core2), patch_core)
    rim = maths(tree, "MAXIMUM", maths(tree, "MAXIMUM", rim1, rim2), patch_rim)

    # The paint under it: half its colour gone, warmed and darkened.
    paint = node(tree, "ShaderNodeHueSaturation")
    paint.inputs["Saturation"].default_value = 0.45
    paint.inputs["Value"].default_value = 0.9
    tree.links.new(image.outputs["Color"], paint.inputs["Color"])
    stained = mix(tree, 1.0, paint.outputs["Color"], GRIME, "MULTIPLY")
    rimmed = mix(tree, maths(tree, "MULTIPLY", rim, 0.85), stained, BROWN)
    streaks = node(tree, "ShaderNodeTexNoise", noise_dimensions="3D")
    streaks.inputs["Scale"].default_value = 3.0
    streaks.inputs["Detail"].default_value = 2.0
    tree.links.new(position, streaks.inputs["Vector"])
    flesh = mix(tree, map_range(tree, streaks.outputs["Fac"], 0.45, 0.7), BEIGE, PALE)
    tree.links.new(mix(tree, core, rimmed, flesh), bsdf.inputs["Base Color"])

    target = bpy.data.images.new(texture.name + "_Flood", texture.size[0], texture.size[1], alpha=False)
    baked = node(tree, "ShaderNodeTexImage", image=target)
    tree.nodes.active = baked
    return target


def run(folder, length):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, folder, folder + ".fbx"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    size = max(max(c[i] for c in corners) - min(c[i] for c in corners) for i in range(3))
    # The game sizes a vehicle by its length, the longer of its two flat sides; near enough, the largest side of its box.
    metres = length / size
    info = json.load(open(os.path.join(MODELS, folder, "materials.json"), encoding="utf-8"))
    textures = {m["name"]: m["texture"] for m in info["materials"] if m.get("texture")}
    targets = []
    for material in {s.material for o in meshes for s in o.material_slots if s.material}:
        name = material.name.split(".")[0]
        if name not in textures:
            continue
        texture = bpy.data.images.load(os.path.join(MODELS, folder, textures[name]))
        material.use_nodes = True
        targets.append((flood_material(material, texture, metres), textures[name]))
    if not targets:
        print(f"{folder}: no textured material")
        return
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    scene.render.bake.use_pass_direct = False
    scene.render.bake.use_pass_indirect = False
    scene.render.bake.use_pass_color = True
    scene.render.bake.margin = 8
    for o in bpy.context.scene.objects:
        o.select_set(o in meshes)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_clear=True, margin=8)
    for image, source in targets:
        path = os.path.join(MODELS, folder, source[:-4] + "_Flood.png")
        image.filepath_raw = path
        image.file_format = "PNG"
        image.save()
        print(f"{folder}: wrote {os.path.relpath(path, ROOT)}")
    if PREVIEW:
        preview(meshes, targets, os.path.join(PREVIEW, folder + ".png"))


def preview(meshes, targets, path):
    """A picture of the vehicle wearing its baked textures, from above at the game's angle, to judge them by."""
    for image, _ in targets:
        for material in bpy.data.materials:
            if material.node_tree and any(n.type == "TEX_IMAGE" and n.image == image for n in material.node_tree.nodes):
                tree = material.node_tree
                tree.nodes.clear()
                out = node(tree, "ShaderNodeOutputMaterial")
                bsdf = node(tree, "ShaderNodeBsdfPrincipled")
                socket(bsdf.inputs, "Roughness").default_value = 0.85
                tree.links.new(node(tree, "ShaderNodeTexImage", image=image).outputs["Color"], bsdf.inputs["Base Color"])
                tree.links.new(bsdf.outputs[0], out.inputs["Surface"])
    scene = bpy.context.scene
    corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    centre = sum(corners, Vector()) / len(corners)
    size = max((c - centre).length for c in corners)
    camera = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    scene.collection.objects.link(camera)
    camera.location = centre + Vector((1.0, -1.3, 1.2)).normalized() * size * 3.2
    camera.rotation_euler = (centre - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 50
    scene.camera = camera
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (0.7, 0.2, 0.6)
    scene.collection.objects.link(sun)
    scene.world = bpy.data.worlds.new("World")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.25, 0.25, 0.27, 1)
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 640, 480
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


ARGS = sys.argv[sys.argv.index("--") + 1:]
PREVIEW = ARGS[ARGS.index("--preview") + 1] if "--preview" in ARGS else None  # A folder for pictures, if wanted.
for argument in [a for a in ARGS if ":" in a and a != PREVIEW]:
    folder, length = argument.split(":")
    run(folder, float(length))
