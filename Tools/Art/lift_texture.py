# lift_texture.py
# Brightens a baked body's texture, for one that reads as a black shape from the game's camera: a baked body has no tint that lifts its colours
# as a real model's material does. Colours keep their proportions. Run it once on the file in Art/Imported/Models, as bring_in.py copies over it:
#   blender --background --factory-startup --python Tools/Art/lift_texture.py -- <png> <factor>
# The ODST's came with a median brightness of 0.15, and is lifted by 1.7.
import sys

import bpy
import numpy as np

path, factor = sys.argv[sys.argv.index("--") + 1:][:2]
image = bpy.data.images.load(path)
image.colorspace_settings.name = "Non-Color"  # The numbers as they are in the file.
pixels = np.array(image.pixels[:], dtype=np.float32)
pixels[0::4] = np.clip(pixels[0::4] * float(factor), 0, 1)
pixels[1::4] = np.clip(pixels[1::4] * float(factor), 0, 1)
pixels[2::4] = np.clip(pixels[2::4] * float(factor), 0, 1)
image.pixels[:] = pixels.tolist()
image.update()
image.save(filepath=path)
print("lifted", path, "by", factor)
