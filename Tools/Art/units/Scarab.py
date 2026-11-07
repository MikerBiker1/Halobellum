# The Scarab, from the owner's updated model (Scarab update.glb, D84), in place of the one of D45 (modified scarab.fbx, whose textures were missing).
# The file holds four Scarabs in a row. The last and biggest, the purple one, is the one used: 13,762 triangles in 7 parts, painted, and it already
# faces -Y. It is kept whole: nothing is reduced.
# - Each leg is cut in two where it really bends. The upper part (Leg_<n>_<phase>) turns at the hip, the outer end of the axle that comes out of the
#   hull; the lower part (Knee_<n>) turns at the knee, the hinge whose two discs show either side of the leg; Foot_<n> marks the tip of the spike.
#   The front feet stand 3 units off the ground in the file and the legs are nearly straight: the game crouches the body a little to plant them.
#   ModelParts walks them, each foot planted while it carries weight (D84). Islands are sorted by the middle of their box: further out than
#   LEG_SIDE they are a leg's, and lower than KNEE_CUT the lower leg's.
# - Phases follow a four-legged walk: rear left, front left, rear right, front right, a quarter of the cycle apart. Seen from the front, the
#   Scarab's left is +X.
# - The cap on the mast at the back turns (Turret_1) and carries both plasma guns at its front corners. The focus beam leaves the mouth at the front.
# Numbers are in the file's own units, with the Scarab centred and standing on Z = 0. The game makes it 14 m long (RealModelBuilder).
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt
import bpy
import numpy as np
from mathutils import Vector

BIG = 1000.0
LEG_SIDE = 29.0
KNEE_CUT = 33.0
HIP = {"front": Vector((29.5, -19.55, 51.1)), "rear": Vector((31.6, 18.0, 73.05))}
KNEE = {"front": Vector((47.7, -43.6, 29.6)), "rear": Vector((50.6, 45.95, 29.9))}
# (number, phase in percent, which end, side: +1 the Scarab's left)
LEGS = [(0, 0, "rear", 1), (1, 25, "front", 1), (2, 50, "rear", -1), (3, 75, "front", -1)]

mt.start("Scarab", "Meshy/Scarab update.glb")
mt.source_size()
mt.keep_box(Vector((280, -BIG, -BIG)), Vector((470, BIG, BIG)))
mt.centre_on_ground()
mt.report("the fourth Scarab, centred")

mt.plain_materials()
for material in bpy.data.materials:
    image = mt._input_image(material, "Base Color")
    if image is not None:
        print(f"REPORT {material.name} colour {image.name} {image.size[0]}x{image.size[1]}: mean {mt._pixels(image)[:, :, :3].mean():.3f}")
# Its lights have no texture: the red ones at the back and the green one in the cap's front edge.
mt.flag("engine_core_lights", glow=(1.6, 0.18, 0.12))
mt.flag("scarab_engine", glow=(0.25, 1.6, 0.4))

# The file splits every piece at its texture seams. Welding joins them again, so that a piece is sorted whole.
mt.weld(0.02)


def mirrored(point, side):
    return Vector((point.x * side, point.y, point.z))


def attach(child, parent):
    """Puts child under parent where it stands, with a plain local transform: the FBX writer misplaces a grandchild that keeps Blender's parent inverse."""
    bpy.context.view_layer.update()
    world = child.matrix_world.copy()
    child.parent = parent
    child.matrix_parent_inverse.identity()
    child.matrix_basis = parent.matrix_world.inverted() @ world
    bpy.context.view_layer.update()


# ---- The legs ----
for number, phase, end, side in LEGS:
    front = end == "front"
    ys = (-BIG, 0.0) if front else (0.0, BIG)
    xs = (LEG_SIDE, BIG) if side > 0 else (-BIG, -LEG_SIDE)
    shin = mt.take(f"Knee_{number}", (xs[0], ys[0], -BIG), (xs[1], ys[1], KNEE_CUT), pivot=mirrored(KNEE[end], side))
    thigh = mt.take(f"Leg_{number}_{phase}", (xs[0], ys[0], KNEE_CUT), (xs[1], ys[1], BIG), pivot=mirrored(HIP[end], side))
    # The foot: the middle of the spike's lowest few vertices, on the ground.
    points = np.array([shin.matrix_world @ v.co for v in shin.data.vertices])
    lowest = points[:, 2].min()
    tip = points[points[:, 2] < lowest + 0.6].mean(axis=0)
    foot = mt.muzzle(f"Foot_{number}", (tip[0], tip[1], lowest))  # Left at the top: ModelParts reads where it is, against the knee, once.
    attach(shin, thigh)
    hip, knee = thigh.matrix_world.translation, shin.matrix_world.translation
    at = foot.matrix_world.translation
    print(f"REPORT leg {number} ({end}, side {side}): hip {mt._round(hip)}, knee {mt._round(knee)}, foot {mt._round(at)}; "
          f"upper {(knee - hip).length:.1f}, lower {(at - knee).length:.1f}, hip to foot {(at - hip).length:.1f}; "
          f"{len(thigh.data.polygons)} and {len(shin.data.polygons)} faces")

# ---- The cap on the mast, with both plasma guns ----
turret = mt.take("Turret_1", (-25, 0, 108), (25, 70, BIG), pivot=(0, 40.85, 109.5))
mt.muzzle("Muzzle_1", (11.5, 8.4, 121.0), parent="Turret_1")
mt.muzzle("Muzzle_2", (-11.5, 8.4, 121.0), parent="Turret_1")
mt.muzzle("Muzzle_0", (0, -74.0, 47.5))

for o in mt.meshes():
    if not o.data.polygons:
        bpy.data.objects.remove(o)
mt.smooth(35)
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=1024)
mt.look("after_back", "back")

# A last look with the legs bent as in a step and the cap turned, to check where each part was cut. The file is written already.
for number, angle in ((0, -0.25), (1, 0.2), (2, 0.15), (3, -0.2)):
    leg = next(o for o in mt.meshes() if o.name.startswith(f"Leg_{number}_"))
    knee = bpy.data.objects[f"Knee_{number}"]
    leg.rotation_mode = knee.rotation_mode = "XYZ"
    leg.rotation_euler[0] = angle
    knee.rotation_euler[0] = -angle * 1.5
bpy.data.objects["Turret_1"].rotation_mode = "XYZ"
bpy.data.objects["Turret_1"].rotation_euler[2] = 0.8
bpy.context.view_layer.update()
mt.look("after_posed", "quarter")
mt.look("after_posed_side", "side")
