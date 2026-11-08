# drivers.py
# The drivers and gunners seen in the open seats of vehicles (D99): a Marine at a Warthog's wheel or its gun, a Grunt astride a Ghost, a Brute
# on a Chopper or at a Marauder's controls, a Combat Form in an infested one. Each soldier's own rigged model (the same the crowd baker plays)
# is posed here, sitting, riding astride, standing at a gun or piloting, and the pose is frozen into a plain mesh, cut down to a couple of
# thousand triangles (a driver is small from the game's camera). It is written facing -Y, as every prepared model is, with its hips at the
# origin and its standing height 1, so that RealModelBuilder sets it in a seat by where its hips go and how tall the soldier is.
# Writes Art_Source/Prepared/Drivers/<soldier>_<pose>.fbx, <soldier>.png (its texture), report.json and pictures in shots/.
#   blender --background --factory-startup --python Tools/Art/drivers.py
#   HB_DRIVERS=Marine_Drive,Grunt_Ride (only those)
# Poses are directions in the world for each bone, so they don't depend on how each rig stands at rest (some in a T, some in an A).
# Needs only Blender's own Python.

import json
import math
import os
import shutil
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODELS = os.path.join(ROOT, "Assets", "_Project", "Art", "Imported", "Models")
OUT = os.path.join(os.environ.get("HB_PREPARED") or os.path.join(ROOT, "Art_Source", "Prepared"), "Drivers")
TRIANGLES = 2400

# Each soldier: its model, its texture, and its bones by role.
MESHY = {"hips": "Hips", "spine": ["Spine02", "Spine01", "Spine"], "neck": "neck", "head": "Head", "shoulder": "{s}Shoulder",
         "arm": "{s}Arm", "forearm": "{s}ForeArm", "hand": "{s}Hand", "thigh": "{s}UpLeg", "shin": "{s}Leg", "foot": "{s}Foot"}
FLOOD = {"hips": "Body", "spine": ["Spine", "Chest"], "neck": "Neck", "head": "Head", "shoulder": None,
         "arm": "{s}Arm", "forearm": "{s}ForeArm", "hand": "{s}Hand", "thigh": "{s}UpLeg", "shin": "{s}Leg", "foot": "{s}Foot"}
SOLDIERS = {
    "Marine": ("Marine/Meshy_AI_Armored_Space_Marine_biped_Meshy_Merged_Animations.fbx", "Marine/Meshy_AI_Armored_Space_Marine_biped_texture_0.png", MESHY),
    "Grunt": ("Grunt/Grunt.fbx", "Grunt/Grunt.png", MESHY),
    "Brute": ("BruteGrenadier/BruteGrenadier.fbx", "BruteGrenadier/BruteGrenadier.png", MESHY),
    "CombatForm": ("CombatFormAnimated/CombatFormAnimated.fbx", "CombatFormAnimated/CombatFormAnimated.png", FLOOD),
    "Elite": ("Elite/Elite.fbx", "Elite/Elite.png", MESHY),
}
# The seats one can see into: no cockpit under glass (the Hornet's, the Sparrowhawk's) shows its pilot, so Pilot is posed only on request.
DRIVERS = ["Marine_Drive", "Marine_Gun", "Grunt_Ride", "Brute_Drive", "Brute_Chopper", "CombatForm_Drive", "CombatForm_Gun", "CombatForm_Ride",
           "Elite_Hatch", "CombatForm_Hatch"]


def direction(forward, up, right, f=0.0, u=0.0, r=0.0):
    return (forward * f + up * u + right * r).normalized()


def poses(F, U, R):
    """
    The poses, as world directions for each bone (head to tail). L/R are the soldier's own left and right; out is away from the middle.
    Drive: sitting upright in a seat, thighs level, shins down, hands forward on a wheel. Pilot: the same, a little reclined, hands low on
    the controls. Ride: astride a seat, leaning over the bars, knees wide and bent, hands forward on the grips. Gun: standing at a gun's
    grips, feet apart, knees a little bent, both hands forward at the chest.
    """
    def side(s):
        return R if s == "Right" else -R

    def both(fn):
        out = {}
        for s in ("Left", "Right"):
            out.update(fn(s, side(s)))
        return out

    drive = {"spine": direction(F, U, R, f=-0.08, u=1), "neck": direction(F, U, R, f=0.1, u=1), "head": direction(F, U, R, f=0.15, u=1)}
    drive.update(both(lambda s, o: {
        f"thigh{s}": direction(F, U, R, f=1, u=-0.02) + o * 0.1, f"shin{s}": direction(F, U, R, f=0.7, u=-1), f"foot{s}": direction(F, U, R, f=1, u=0.1),
        f"arm{s}": direction(F, U, R, f=0.55, u=-0.8) + o * -0.05, f"forearm{s}": direction(F, U, R, f=1, u=0.15) - o * 0.35, f"hand{s}": direction(F, U, R, f=1, u=0.05) - o * 0.3}))

    pilot = {"spine": direction(F, U, R, f=-0.2, u=1), "neck": direction(F, U, R, f=0.05, u=1), "head": direction(F, U, R, f=0.12, u=1)}
    pilot.update(both(lambda s, o: {
        f"thigh{s}": direction(F, U, R, f=1, u=0.05) + o * 0.1, f"shin{s}": direction(F, U, R, f=1, u=-0.55), f"foot{s}": direction(F, U, R, f=1, u=0.3),
        f"arm{s}": direction(F, U, R, f=0.3, u=-1) + o * 0.05, f"forearm{s}": direction(F, U, R, f=1, u=-0.35) - o * 0.15, f"hand{s}": direction(F, U, R, f=1, u=-0.3)}))

    ride = {"spine": direction(F, U, R, f=0.45, u=1), "neck": direction(F, U, R, f=0.35, u=1), "head": direction(F, U, R, f=0.5, u=1)}
    ride.update(both(lambda s, o: {
        f"thigh{s}": direction(F, U, R, f=1, u=-0.45) + o * 0.42, f"shin{s}": direction(F, U, R, f=-0.25, u=-1) + o * 0.1, f"foot{s}": direction(F, U, R, f=1, u=-0.2),
        f"arm{s}": direction(F, U, R, f=0.85, u=-0.55) + o * 0.08, f"forearm{s}": direction(F, U, R, f=1, u=0.0) - o * 0.12, f"hand{s}": direction(F, U, R, f=1)}))

    gun = {"spine": direction(F, U, R, f=0.15, u=1), "neck": direction(F, U, R, f=0.1, u=1), "head": direction(F, U, R, f=0.12, u=1)}
    gun.update(both(lambda s, o: {
        f"thigh{s}": direction(F, U, R, f=0.12, u=-1) + o * 0.1, f"shin{s}": direction(F, U, R, f=-0.12, u=-1), f"foot{s}": direction(F, U, R, f=1, u=-0.2),
        f"arm{s}": direction(F, U, R, f=0.6, u=-0.75) + o * 0.05, f"forearm{s}": direction(F, U, R, f=1, u=0.05) - o * 0.4, f"hand{s}": direction(F, U, R, f=1) - o * 0.3}))
    # Chopper: sitting back on a low saddle behind the tank (the owner, D103), knees up and wide, feet forward on the rests either side,
    # leaning over the tank to the bars.
    chopper = {"spine": direction(F, U, R, f=0.38, u=1), "neck": direction(F, U, R, f=0.25, u=1), "head": direction(F, U, R, f=0.4, u=1)}
    chopper.update(both(lambda s, o: {
        f"thigh{s}": direction(F, U, R, f=0.8, u=0.12) + o * 0.55, f"shin{s}": direction(F, U, R, f=0.35, u=-1) + o * 0.15, f"foot{s}": direction(F, U, R, f=1, u=-0.1),
        f"arm{s}": direction(F, U, R, f=0.9, u=-0.3) + o * 0.12, f"forearm{s}": direction(F, U, R, f=1, u=0.05) - o * 0.1, f"hand{s}": direction(F, U, R, f=1)}))

    # Hatch: sitting upright in a round hatch (the Wraith's, D103), the legs down inside it, the forearms resting forward on its rim.
    hatch = {"spine": direction(F, U, R, f=0.05, u=1), "neck": direction(F, U, R, f=0.12, u=1), "head": direction(F, U, R, f=0.15, u=1)}
    hatch.update(both(lambda s, o: {
        f"thigh{s}": direction(F, U, R, f=0.85, u=-0.5) + o * 0.1, f"shin{s}": direction(F, U, R, f=-0.2, u=-1), f"foot{s}": direction(F, U, R, f=1, u=-0.25),
        f"arm{s}": direction(F, U, R, f=0.35, u=-0.9) + o * 0.12, f"forearm{s}": direction(F, U, R, f=1, u=-0.05) - o * 0.25, f"hand{s}": direction(F, U, R, f=1, u=-0.1)}))
    return {"Drive": drive, "Pilot": pilot, "Ride": ride, "Gun": gun, "Chopper": chopper, "Hatch": hatch}


def aim(arm, name, want):
    """Turns a posed bone in the world so that it points along want, about its head, with the least twist."""
    bone = arm.pose.bones[name]
    world = arm.matrix_world @ bone.matrix
    head = world.translation.copy()
    now = (world.to_3x3() @ Vector((0, 1, 0))).normalized()
    turn = now.rotation_difference(want.normalized()).to_matrix().to_4x4()
    world = Matrix.Translation(head) @ turn @ Matrix.Translation(-head) @ world
    bone.matrix = arm.matrix_world.inverted() @ world
    bpy.context.view_layer.update()


def depth(bone):
    d = 0
    while bone.parent:
        bone, d = bone.parent, d + 1
    return d


def world_points(objects):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    points = []
    for o in objects:
        evaluated = o.evaluated_get(depsgraph)
        data = evaluated.to_mesh()
        points.extend(o.matrix_world @ v.co for v in data.vertices)
        evaluated.to_mesh_clear()
    return points


def make(soldier, pose_name):
    path, texture, names = SOLDIERS[soldier]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, path))
    arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if arm.animation_data:
        arm.animation_data.action = None
    for bone in arm.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = (1, 0, 0, 0)
        bone.location = (0, 0, 0)
        bone.scale = (1, 1, 1)
    bpy.context.view_layer.update()

    def world_bone(name):
        return arm.matrix_world @ arm.pose.bones[name].head, arm.matrix_world @ arm.pose.bones[name].tail

    # Which way it faces: along its feet. Its height, standing as made.
    feet = Vector()
    for s in ("Left", "Right"):
        head, tail = world_bone(names["foot"].format(s=s))
        feet += tail - head
    forward = Vector((feet.x, feet.y, 0)).normalized()
    up = Vector((0, 0, 1))
    right = forward.cross(up).normalized()
    rest = world_points(meshes)
    standing = max(p.z for p in rest) - min(p.z for p in rest)

    # Bones by role, both sides; then pose them parents first.
    wanted = poses(forward, up, right)[pose_name]
    targets = {}
    for role, want in wanted.items():
        if role == "spine":
            for name in names["spine"]:
                targets[name] = want
        elif role in ("neck", "head"):
            targets[names[role]] = want
        else:
            side = "Left" if role.endswith("Left") else "Right"
            key = role[: -len(side)]
            targets[names[key].format(s=side)] = want
    for bone in sorted(arm.pose.bones, key=depth):
        if bone.name in targets:
            aim(arm, bone.name, targets[bone.name])

    # Freeze the pose into one plain mesh, its hips at the origin, facing -Y, its standing height 1.
    hips = arm.matrix_world @ arm.pose.bones[names["hips"]].head
    depsgraph = bpy.context.evaluated_depsgraph_get()
    frozen = []
    for o in meshes:
        data = bpy.data.meshes.new_from_object(o.evaluated_get(depsgraph), preserve_all_data_layers=True, depsgraph=depsgraph)
        data.transform(o.matrix_world)
        copy = bpy.data.objects.new(f"{soldier}_{pose_name}", data)
        bpy.context.scene.collection.objects.link(copy)
        frozen.append(copy)
    for o in [arm] + meshes:
        bpy.data.objects.remove(o)
    bpy.ops.object.select_all(action="DESELECT")
    for o in frozen:
        o.select_set(True)
    bpy.context.view_layer.objects.active = frozen[0]
    if len(frozen) > 1:
        bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = f"{soldier}_{pose_name}"
    turn = math.atan2(-forward.x, forward.y) + math.pi  # Turns its facing onto -Y.
    body.data.transform(Matrix.Scale(1.0 / standing, 4) @ Matrix.Rotation(turn, 4, "Z") @ Matrix.Translation(-hips))
    faces = sum(len(p.vertices) - 2 for p in body.data.polygons)
    if faces > TRIANGLES:
        cut = body.modifiers.new("Fewer", "DECIMATE")
        cut.ratio = TRIANGLES / faces
        bpy.ops.object.modifier_apply(modifier=cut.name)
    faces = sum(len(p.vertices) - 2 for p in body.data.polygons)
    points = [v.co for v in body.data.vertices]
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))

    os.makedirs(os.path.join(OUT, "shots"), exist_ok=True)
    picture(body, f"{soldier}_{pose_name}")
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"{soldier}_{pose_name}.fbx"), use_selection=True, object_types={"MESH"},
                             axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", path_mode="STRIP",
                             embed_textures=False, bake_anim=False)
    shutil.copy(os.path.join(MODELS, texture), os.path.join(OUT, f"{soldier}.png"))
    print(f"REPORT {soldier}_{pose_name}: {faces} triangles, standing {standing:.3f} in its file, posed from {tuple(round(v, 2) for v in low)} to {tuple(round(v, 2) for v in high)}")
    return {"triangles": faces, "low": list(low), "high": list(high), "texture": f"{soldier}.png", "source": path}


def picture(body, label):
    """A quarter view and a side view of the posed driver, on a floor at its feet."""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x, scene.render.resolution_y = 360, 360
    scene.render.film_transparent = False
    camera_data = bpy.data.cameras.new("cam")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 1.4
    camera = bpy.data.objects.new("cam", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    for view, at in (("quarter", Vector((1.6, -1.6, 0.6))), ("side", Vector((2.2, 0, 0.1)))):
        camera.location = at
        camera.rotation_euler = (Vector((0, 0, -0.1)) - at).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(OUT, "shots", f"{label}_{view}.png")
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera)


if __name__ == "__main__":
    chosen = os.environ.get("HB_DRIVERS", "").split(",") if os.environ.get("HB_DRIVERS") else DRIVERS
    report_path = os.path.join(OUT, "report.json")
    report = json.load(open(report_path, encoding="utf-8")) if os.path.exists(report_path) else {}
    for name in chosen:
        soldier, pose_name = name.split("_")
        report[name] = make(soldier, pose_name)
    os.makedirs(OUT, exist_ok=True)
    with open(report_path, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)
