# The Ranged Pure Form: lobs bone shards from the spines on its back. A static model that the game wobbles; every triangle kept.
# It came facing -Y: the tusks, the fronds and the spines' points are on that side.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bmesh
import model_tools as mt


def spine_tips(pattern):
    """The points of the bone spines. Each spine is a separate piece of 12 faces, high on the back, and points toward -Y."""
    tips = []
    for o in mt.meshes(pattern):
        mesh = bmesh.new()
        mesh.from_mesh(o.data)
        for faces in mt._islands(mesh):
            points = [o.matrix_world @ v.co for f in faces for v in f.verts]
            if len(faces) == 12 and mt._high(points).y - mt._low(points).y > 1.0 and min(p.z for p in points) > 3.5:
                tips.append(min(points, key=lambda p: p.y))
        mesh.free()
    return tips


mt.start("PureFormRanged", "sketchfab.com/enemieshalo_3the_floodpure_ranged.glb")
mt.fit()
mt.report("fitted")
tips = spine_tips("flood_ranged_0")  # Found before welding, while each spine is still its own piece.
tips.sort(key=lambda p: -p.z)
axis = sum(p.x for p in tips) / len(tips)  # The spines stand in two rows, either side of the back. The arms make the model lopsided, so this isn't x = 0.
top_one = tips[0]
top_other = next(p for p in tips[1:] if (p.x - axis) * (top_one.x - axis) < 0)
print(f"REPORT {len(tips)} spines, axis at x = {axis:.3f}, highest in each row {mt._round(top_one)} and {mt._round(top_other)}")
mt.weld(0.002, "flood_ranged_0")  # The file splits its vertices at texture seams: without this, smoothing leaves a crease along each seam.
mt.smooth(60, "flood_ranged_0")
mt.flag("flood_fronds", cutout=True)
mt.muzzle("Muzzle_0", (top_one + top_other) / 2)  # Between the tops of the two rows, level with the tips.
mt.motion("Wobble")
mt.merge_rest()
mt.finish()
mt.look("after_back", "back")
# The usual frame crops anything taller than it is long, so the whole creature is shown again in portrait.
for camera in ("quarter", "side", "front", "back"):
    mt.look("tall_" + camera, camera, size=(420, 560))
