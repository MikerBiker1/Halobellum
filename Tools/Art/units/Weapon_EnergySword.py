# The Zealot's energy sword, for a baked body: to about 500 triangles, with a colour per face. It lies along Y in the file, with its grip
# near y 0.1 and its blades toward -Y. Unity sets its length.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

mt.start("Weapon_EnergySword", "sketchfab.com/For Later/halo_3_energy_sword_by_abisv.glb")
mt.source_size()
mt.snapshot()
mt.reduce(500, flat_angle=0)
mt.report("reduced")
mt.finish_weapon(grip=(0, 0.1, 0), tip=(0, -1.14, 0), length_note="about 1.3 m")
