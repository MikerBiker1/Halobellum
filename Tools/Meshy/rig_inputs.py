# rig_inputs.py
# Prepares the models that Meshy rigs: facing -Y in Blender (+Z in glTF), upright, on the ground, at their real
# height, with one material and one base colour texture of at most 1024.
# Writes Art_Source/Prepared/<Unit>_Rig/input.glb, <Unit>.png and shots/. It wipes that folder first, so it refuses to
# run for a unit that Meshy has already rigged, unless told --force.
#   blender --background --factory-startup --python Tools/Meshy/rig_inputs.py -- [Unit ...] [--force]
import json
import os
import re
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "Art"))
import model_tools as mt

MODELS = {
    "Grunt": {"source": "Grunt/grunt.glb", "height": 1.3},
    "StalkerForm": {"source": "sketchfab.com/enemieshalo_3the_floodpure_stalker.glb", "height": 2.4},
    "PureFormRanged": {"source": "sketchfab.com/enemieshalo_3the_floodpure_ranged.glb", "height": 2.9},
    "PureFormTank": {"source": "sketchfab.com/enemieshalo_3the_floodpure_tank.glb", "height": 3.8},
    # M11: "keep" picks the figure out of a file that holds several, "tris" reduces it, and "bake" joins several materials into one texture.
    "Spartan": {"source": "Meshy/Meshy_AI_halo_Spartan/Meshy_AI_halo_biped_Character_output.fbx", "height": 2.3, "tris": 6000},
    "Elite": {"source": "sketchfab.com/enemieshalo_3covenantelitesminor.glb", "height": 2.6, "tris": 7000, "bake": True},
    "Hunter": {"source": "sketchfab.com/enemiesmulticovenanthunters.glb", "height": 3.6, "keep": r"^Object_(10|11|12|13|14|15)$", "bake": True},
    "HunterCaptain": {"source": "sketchfab.com/enemiesmulticovenanthunters.glb", "height": 3.8, "keep": r"^Object_(27|28|29|30|31)$", "tris": 8000, "bake": True},
    "GruntGoblin": {"source": "sketchfab.com/enemiesbanishedgoblin.glb", "height": 4.4, "tris": 8000, "bake": True},
    # The third batch. "yaw", "pitch" and "roll" turn a figure that faces elsewhere to face -Y and stand up, "rest" flattens a rigged file in its rest pose and not its clip's pose, "pose" turns its bones first,
    # "drop" leaves out parts by name (see-through cards bake to black squares),
    # "box" keeps the separate pieces whose middle lies between these X values of the file (for figures that share objects, as these Brutes do),
    # and "feet" stands it with its feet on the origin, where a big arm or a tail would pull the middle of its box aside.
    "InfestedMarine": {"source": "Meshy/halo_2_anniversary_human_flood_form.glb", "height": 2.0, "pose": {"neck_30": (35, 30, 0)}, "drop": "^Object_7$", "tris": 5000, "bake": True, "feet": True},
    "ODST": {"source": "Meshy/halo_2_odst.glb", "height": 2.0, "bake": True, "rest": True, "yaw": 4.7, "pitch": -45.5, "roll": -90, "feet": True},  # Its rest pose lies at a slant: these come from its bones.
    "Jackal": {"source": "Meshy/jackal_halo_2.glb", "height": 2.0, "bake": True, "yaw": -90, "feet": True},
    "BruteGrenadier": {"source": "Meshy/enemieshalo_3covenantbrutes.glb", "height": 2.8, "box": (-3.48, -1.13), "tris": 7000, "bake": True, "feet": True},  # The fifth from the left: the red crest and loincloth.
    "CombatForm": {"source": "Meshy/halo_ce_flood_Combat_form.glb", "height": 2.6, "drop": "fronds", "yaw": 90, "bake": True, "feet": True},
    "Goliath": {"source": "Meshy/Meshy_AI_Goliath.glb", "height": 6.0, "tris": 8000, "feet": True},
}
SIZE = 1024


def read(image, width, height):
    """The image's pixels at this size, as stored (still sRGB), bottom row first."""
    copy = image.copy()
    if tuple(copy.size) != (width, height):
        copy.scale(width, height)
    data = np.empty(width * height * 4, dtype=np.float32)
    copy.pixels.foreach_get(data)
    bpy.data.images.remove(copy)
    return data.reshape(height, width, 4)


def write_png(pixels, path):
    height, width = pixels.shape[:2]
    image = bpy.data.images.new("base_colour", width, height, alpha=False)
    pixels = pixels.copy()
    pixels[:, :, 3] = 1.0
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return image


def remap_uvs(objects, scale_u, scale_v, shift_u, shift_v):
    for o in objects:
        uvs = o.data.uv_layers.active.data
        coords = np.empty(len(uvs) * 2, dtype=np.float32)
        uvs.foreach_get("uv", coords)
        print(f"REPORT   {o.name} uv range u {coords[0::2].min():.3f} to {coords[0::2].max():.3f}, v {coords[1::2].min():.3f} to {coords[1::2].max():.3f}")
        coords[0::2] = coords[0::2] * scale_u + shift_u
        coords[1::2] = coords[1::2] * scale_v + shift_v
        uvs.foreach_set("uv", coords)


def atlas_grunt(folder):
    """
    The Grunt has three textures. The main one (skin and armour, 1024 square) is squeezed to 768 wide. The strip beside it
    holds the mask's 256 square and the glowing parts' 256 by 128 at full size. The glowing parts stop glowing.
    """
    groups = {}
    for o in mt.meshes():
        groups.setdefault(mt._input_image(o.data.materials[0], "Base Color"), []).append(o)
    by_size = {tuple(image.size): (image, objects) for image, objects in groups.items()}
    main, main_parts = by_size[(1024, 1024)]
    mask, mask_parts = by_size[(256, 256)]
    glow, glow_parts = by_size[(256, 128)]
    canvas = np.zeros((SIZE, SIZE, 4), dtype=np.float32)
    canvas[0:1024, 0:768] = read(main, 768, 1024)
    canvas[0:256, 768:1024] = read(mask, 256, 256)
    # Twice, one above the other, because some of its faces' UVs run past the top of the texture (to 1.238) and wrap.
    canvas[256:384, 768:1024] = read(glow, 256, 128)
    canvas[384:512, 768:1024] = canvas[256:384, 768:1024]
    remap_uvs(main_parts, 0.75, 1.0, 0.0, 0.0)
    remap_uvs(mask_parts, 0.25, 0.25, 0.75, 0.0)
    remap_uvs(glow_parts, 0.25, 0.125, 0.75, 0.25)
    return write_png(canvas, os.path.join(folder, "Grunt.png"))


def one_material(image):
    """Joins everything into one mesh with one material that has only a base colour texture."""
    parts = mt.meshes()
    for o in parts:
        o.data.uv_layers.active.name = "UVMap"
        o.data.materials.clear()
    mt._select(parts)
    if len(parts) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = "Body"
    material = bpy.data.materials.new("Body")
    material.use_nodes = True
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    shader = material.node_tree.nodes["Principled BSDF"]
    material.node_tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])
    shader.inputs["Roughness"].default_value = 0.8
    o.data.materials.append(material)
    for p in o.data.polygons:
        p.material_index = 0
    return o


def stand(height, feet=False):
    mt.centre_on_ground()
    low, high = mt._bounds()
    mt.scale(height / (high.z - low.z))
    mt.centre_on_ground()
    if feet:
        points = [o.matrix_world @ v.co for o in mt.meshes() for v in o.data.vertices]
        low_points = [p for p in points if p.z < height * 0.1]
        shift = Vector((-(min(p.x for p in low_points) + max(p.x for p in low_points)) / 2, -(min(p.y for p in low_points) + max(p.y for p in low_points)) / 2, 0))
        for o in mt.meshes():
            o.data.transform(Matrix.Translation(shift))
            o.data.update()
        print(f"REPORT   feet moved to the origin, by {shift.x:.3f}, {shift.y:.3f}")


def prepare(unit, spec):
    name = unit + "_Rig"
    mt.start(name, spec["source"], rest_pose=spec.get("rest", False), pose=spec.get("pose"))
    folder = mt._state["folder"]
    mt.remove("Icosphere")
    if "drop" in spec:
        mt.remove(spec["drop"])
    if "yaw" in spec or "pitch" in spec or "roll" in spec:
        mt.turn(yaw=spec.get("yaw", 0), pitch=spec.get("pitch", 0), roll=spec.get("roll", 0))
    if "box" in spec:
        factor = mt._state["to_ten"]
        mt.keep_box((spec["box"][0] * factor, -1e6, -1e6), (spec["box"][1] * factor, 1e6, 1e6))
    if "keep" in spec:
        for o in mt.meshes():
            if not re.match(spec["keep"], o.name):
                bpy.data.objects.remove(o)
    if "tris" in spec:
        mt.weld(0.002)
        mt.reduce(spec["tris"], flat_angle=0)
    if unit == "Grunt":
        image = atlas_grunt(folder)
    elif spec.get("bake"):
        mt.plain_materials()
        baked = mt.bake_one(SIZE)
        image = write_png(read(baked, SIZE, SIZE), os.path.join(folder, unit + ".png"))
    else:
        mt.remove("fronds")
        source = mt._input_image(mt.meshes()[0].data.materials[0], "Base Color")
        print(f"REPORT   {unit} texture {tuple(source.size)}")
        width, height = (min(n, SIZE) for n in source.size)
        image = write_png(read(source, width, height), os.path.join(folder, unit + ".png"))
    one_material(image)
    mt.weld(0.002)
    if unit == "Grunt":
        mt.reduce(4000)
    mt.smooth(60)
    stand(spec["height"], spec.get("feet", False))
    mt.report("ready")
    image.pack()
    path = os.path.join(folder, "input.glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_yup=True, export_apply=True, export_animations=False,
                              export_skins=False, export_materials="EXPORT", export_image_format="AUTO", export_cameras=False, export_lights=False)
    for camera in ("front", "side", "back", "quarter"):
        mt.look("input_" + camera, camera, size=(420, 560))
    print(f"REPORT wrote {path}, {os.path.getsize(path)} bytes, {mt.tris()} triangles")


arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
state_path = os.path.join(HERE, "rig_state.json")
state = json.load(open(state_path, encoding="utf-8")) if os.path.exists(state_path) else {}
for unit in [a for a in arguments if not a.startswith("--")] or list(MODELS):
    if state.get(unit, {}).get("rig") and "--force" not in arguments:
        print(f"REPORT skipping {unit}: Meshy has already rigged it")
        continue
    prepare(unit, MODELS[unit])
