# The Goliath, from the owner's Meshy model: a great beast that walks on its knuckles and on two short hind legs, spined all along its back.
# It faces -Y. Meshy rigged it (5 credits), but as a man: its first walk tore its armour apart, a stretch of 6 with 12% of its edges pulled past
# half again their length, so it is a static model with four legs that swing. Numbers are in a model 10 units long, on the ground with its middle
# on the origin. The file is cut into 260 plates along its texture seams, and each limb is the plates whose middle is in its box.
# The swing is set so that its feet hardly slide at the unit's 1.1 m/s and 6 m tall: at 0.5 cycles a second, 9 degrees for an arm that
# hangs 3.4 m and 18 for a leg of 1.8 m. Diagonal pairs step together.
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import model_tools as mt

BIG = 20
ARM_TOP = 4.6  # Under the shoulder caps, which stay on the body.
ARM_INSIDE = 1.3  # Further out than the chest.
ARM_BACK = 0.4
LEG_TOP = 3.0
LEG_INSIDE = 0.25
ARM_BACK_RIGHT = -0.5  # Its -X hind foot stands well forward, right behind that arm.
LEG_FRONT = 0.3
LEG_BACK_RIGHT = 2.2  # Spines hang down behind this.

mt.start("Goliath", "Meshy/Meshy_AI_Goliath.glb")
mt.centre_on_ground()
mt.take("Swing_0_0_9_0.5", (-BIG, -BIG, -BIG), (-ARM_INSIDE, ARM_BACK_RIGHT, ARM_TOP), pivot=(-2.1, -1.2, 5.0))
mt.take("Swing_1_50_9_0.5", (ARM_INSIDE, -BIG, -BIG), (BIG, ARM_BACK, ARM_TOP), pivot=(2.1, -1.2, 5.0))
mt.take("Swing_2_50_18_0.5", (-BIG, ARM_BACK_RIGHT, -BIG), (-LEG_INSIDE, LEG_BACK_RIGHT, LEG_TOP), pivot=(-1.2, 0.3, 3.0))
mt.take("Swing_3_0_18_0.5", (LEG_INSIDE, LEG_FRONT, -BIG), (BIG, BIG, LEG_TOP), pivot=(1.2, 1.0, 3.0))
mt.weld(0.002)  # Only now: the separate plates are what the boxes above pick from, and each limb must stay apart from the body.
mt.reduce(8000, flat_angle=0)
mt.smooth(40)
mt.muzzle("Muzzle_0", (0, -4.9, 3.0))  # Fist Slam.
mt.muzzle("Muzzle_1", (0, -4.9, 3.6))  # Fuel Rod Cannon: from the head, as the arms are its feet.
mt.merge_rest()
mt.report("done")
mt.finish(texture_size=1024)
for camera in ("quarter", "side", "front", "back"):
    mt.look("tall_" + camera, camera, size=(560, 560))
