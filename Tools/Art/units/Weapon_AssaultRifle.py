# The Marine's assault rifle: 5,625 triangles down to about 250, with a colour per face instead of a texture.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("Weapon_AssaultRifle", "sketchfab.com/halo_ce_assault_rifle.glb")
mt.snapshot()
low, high = mt._bounds(mt.meshes("Barrel"))
tip = (0, low.y, (low.z + high.z) / 2)
mt.remove("Port_|SafetySwitch|CockingHandle")
mt.solid(250, voxel=0.04)
mt.smooth(30)
mt.finish_weapon(grip=(0, 1.85, -0.6), tip=tip, length_note=0.8)
