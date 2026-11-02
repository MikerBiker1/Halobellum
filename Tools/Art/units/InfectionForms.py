# The Infection Form: 966 triangles down to about 250, since fifty of them make one squad and each is tiny on screen.
# A static model that the game wobbles. Its fronds use a texture with holes.
# Facing: the head is the end with the eye sockets, the two mandible hooks and the frond stalks, which came along +X.
# The ribbed end with the stinger, at -X, is its tail. The Pure forms carry the same fronds at the front of the face.
import os
import re
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bpy
import model_tools as mt


def reduce_each_piece(pattern, budget):
    """Each separate piece gets its own target, so that the thin stalks and tentacles keep enough triangles to stay round next to the bulb."""
    for o in mt.meshes(pattern):
        mt._select([o])
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.separate(type="LOOSE")
        bpy.ops.object.mode_set(mode="OBJECT")
    for o in mt.meshes(pattern):
        only = "^" + re.escape(o.name) + "$"
        mt.weld(0.005, only)  # Inside the piece only: welding first would fuse the stalks onto the bulb and lose their budget.
        mt.reduce(budget(mt.tris([o])), only, flat_angle=0)


def budget(faces):
    if faces > 200:
        return 90  # The bulb
    if faces > 40:
        return 18  # The three frond stalks
    if faces > 20:
        return 10  # The six tentacles
    return 4  # The two mandible hooks


mt.start("InfectionForms", "sketchfab.com/enemieshalo_3the_floodinfection.glb")
mt.turn(yaw=-90)
mt.fit()
mt.report("turned")
mt.remove_small_islands(10, "flood_infection_0")  # Single faces under the mouth, two stubs among the tentacles, the stinger's hair.
reduce_each_piece("flood_infection_0", budget)  # The fronds are 48 crossed cards already, and they are the silhouette: left as they are.
mt.smooth(60, "flood_infection_0")
mt.flag("flood_fronds", cutout=True)
mt.motion("Wobble")
mt.merge_rest()
mt.finish()
mt.look("after_back", "back")
mt.look("close_quarter", "quarter", size=(900, 700), samples=32)
mt.look("close_side", "side", size=(900, 700), samples=32)
