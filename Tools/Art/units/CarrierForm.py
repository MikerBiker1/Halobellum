# The Carrier Form: a bloated sack on two legs that bursts. A static model that the game wobbles; every triangle kept.
# It came facing -Y: the egg cluster, the two trailing arms and the toes are on that side. Unlike the other forms it has no fronds.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt


def make_lit(material):
    """This file's material is unlit (an emission shader with no Principled node), so model_tools can't find its texture. Rebuilds it as a normal one."""
    tree = material.node_tree
    image = next(n.image for n in tree.nodes if n.type == "TEX_IMAGE")
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    shader = tree.nodes.new("ShaderNodeBsdfPrincipled")
    texture = tree.nodes.new("ShaderNodeTexImage")
    texture.image = image
    tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    material.blend_method = "OPAQUE"


mt.start("CarrierForm", "sketchfab.com/halo_ce_flood_carrier_form.glb")
mt.fit()
mt.report("fitted")
mt.weld(0.002)  # The file splits its vertices at texture seams: without this, smoothing leaves a crease along each seam.
mt.smooth(60)
for o in mt.meshes():
    for m in o.data.materials:
        make_lit(m)
mt.motion("Wobble")
mt.merge_rest()
mt.finish()
mt.look("after_back", "back")
# The usual frame crops anything taller than it is long, so the whole creature is shown again in portrait.
for camera in ("quarter", "side", "front", "back"):
    mt.look("tall_" + camera, camera, size=(420, 560))
