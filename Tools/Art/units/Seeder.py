# The Seeder, from the owner's Meshy model: a bone-white head trailing long ribbons that end in red fronds. 15,821 triangles to about
# 8,800 and one texture. It came with its nose at -X. Once its texture seams are welded, every tendril is its own closed piece rooted
# inside the back of the head, so the long ones can move without cutting anything. Numbers are the file's own sizes, centred.
import math
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bmesh
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
import model_tools as mt

BIG = 5
HUB = Vector((0, -0.36, 0.085))  # Where the tendrils come together, inside the back of the head.
SHORT = -0.25  # A piece whose middle is ahead of this is the head or a short wisp, and stays still.
# The tendrils fan out upward in the file, which from the game's steep camera lines them up one behind the other, a thin streak when the
# unit flies up the screen. Turning the fan around the nose-to-tail line lays it out flat, so that the fronds show apart. TAIL_LEVEL first
# brings the fan's middle level with the head, or it would lie off to one side. Setting both to 0 keeps the file's own pose.
TAIL_ROLL = 90
TAIL_LEVEL = -6.9


def sheets(mesh):
    """The pieces of a bmesh joined along edges. Pieces that only touch at a point can face opposite ways, and these keep them apart."""
    seen, result = set(), []
    for first in mesh.faces:
        if first.index in seen:
            continue
        stack, faces = [first], []
        seen.add(first.index)
        while stack:
            face = stack.pop()
            faces.append(face)
            for edge in face.edges:
                if len(edge.link_faces) == 2:
                    for other in edge.link_faces:
                        if other.index not in seen:
                            seen.add(other.index)
                            stack.append(other)
        result.append(faces)
    return result


def crossings(tree, start, direction):
    """How many times a line going this way from just off a face crosses the mesh."""
    count, at = 0, start + direction * 1e-5
    while count < 50:
        hit = tree.ray_cast(at, direction)[0]
        if hit is None:
            return count
        count, at = count + 1, hit + direction * 1e-5
    return count


def outward_and_laid_out():
    """Turns any sheet that is inside out (a whole frond came out that way), since the game hides back faces, then lays out the tail."""
    body = mt.meshes("^Body$")[0]
    mesh = bmesh.new()
    mesh.from_mesh(body.data)
    mesh.faces.ensure_lookup_table()
    tree = BVHTree.FromBMesh(mesh)
    for faces in sheets(mesh):
        # A line going out from a face that points outward crosses the model's skin an even number of times. Most of a sheet's faces vote.
        step = max(1, len(faces) // 40)
        inward = sum(crossings(tree, f.calc_center_median(), f.normal) % 2 for f in faces[::step])
        if inward * 2 > len(faces[::step]):
            print(f"REPORT turned a sheet of {len(faces)} faces the right way out")
            bmesh.ops.reverse_faces(mesh, faces=faces)
    turn = Matrix.Translation(HUB) @ Matrix.Rotation(math.radians(TAIL_ROLL), 4, "Y") @ Matrix.Rotation(math.radians(TAIL_LEVEL), 4, "X") @ Matrix.Translation(-HUB)
    for faces in mt._islands(mesh):
        if mt._middle(body, faces).y > SHORT:
            for v in {v for f in faces for v in f.verts}:
                v.co = turn @ v.co
    mesh.to_mesh(body.data)
    mesh.free()


def fill_gaps(image):
    """Spreads the colours into the texture's unused parts, which are black. Far away the game reads smaller copies of a texture, where black would darken this many small islands."""
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    pixels = pixels.reshape(height, width, 4)
    done = pixels[:, :, :3].sum(axis=2) > 0.0005
    print(f"REPORT {100 - 100 * done.mean():.0f}% of the texture was unused")
    while not done.all():
        total = np.zeros((height, width, 3), dtype=np.float32)
        count = np.zeros((height, width, 1), dtype=np.float32)
        for axis, step in ((0, 1), (0, -1), (1, 1), (1, -1)):
            total += np.roll(pixels[:, :, :3] * done[:, :, None], step, axis)
            count += np.roll(done[:, :, None], step, axis)
        grown = ~done & (count[:, :, 0] > 0)
        pixels[grown, :3] = total[grown] / count[grown]
        done |= grown
    pixels[:, :, 3] = 1
    image.pixels.foreach_set(pixels.ravel())
    image.update()
    image.pack()


mt.start("Seeder", "Meshy/Meshy_AI_Seeder.glb")
mt.source_size()
mt.turn(yaw=90)
mt.centre_on_ground()
mt.report("turned")
fill_gaps(mt.reduce_and_repaint(8800, 1024, reach=0.008))
mt.smooth(60)
outward_and_laid_out()
low = mt._bounds()[0]
mt.centre_on_ground()
hub = HUB + mt._bounds()[0] - low
mt.report("laid out")
# The two sides of the tail beat as wings do. The short wisps and the head stay in the body.
mt.take("Flap_L", (hub.x, SHORT, -BIG), (BIG, BIG, BIG), pivot=hub)
mt.take("Flap_R", (-BIG, SHORT, -BIG), (hub.x, BIG, BIG), pivot=hub)
mt.muzzle("Muzzle_0", hub + Vector((0, -0.11, -0.01)))  # Seed Spore leaves between the jaws, at the tip of the head.
mt.motion("Hover")
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=512)
mt.look("after_back", "back")
