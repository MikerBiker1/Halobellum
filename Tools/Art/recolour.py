# recolour.py
# Makes the armour colours that bodies sharing a rig wear (M11): the five Noble Spartans from the Spartan's texture, the Zealot's gold from
# the Elite's. Writes Art_Source/Prepared/<Model>_Rig/<Model>_<Colour>.png, next to the model's own texture. Needs numpy and Pillow.
#   python Tools/Art/recolour.py
import colorsys
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PREPARED = os.path.join(ROOT, "Art_Source", "Prepared")

# The Spartan's armour is a pale tan, nearly without colour, so it is repainted by its brightness. Its orange visor is left alone.
NOBLE = {"Sniper": (0.36, 0.5, 0.72), "HeavyWeapons": (0.68, 0.2, 0.17), "Grenadier": (0.86, 0.56, 0.16), "CQB": (0.3, 0.3, 0.34), "Fireteam": (0.3, 0.5, 0.28)}


def hsv(rgb):
    high, low = rgb.max(axis=2), rgb.min(axis=2)
    delta = high - low
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    hue = np.zeros_like(high)
    safe = delta > 1e-6
    hue = np.where(safe & (high == r), ((g - b) / np.maximum(delta, 1e-6)) % 6, hue)
    hue = np.where(safe & (high == g), (b - r) / np.maximum(delta, 1e-6) + 2, hue)
    hue = np.where(safe & (high == b), (r - g) / np.maximum(delta, 1e-6) + 4, hue)
    return hue * 60, delta / np.maximum(high, 1e-6), high


def spartan():
    folder = os.path.join(PREPARED, "Spartan_Rig")
    pixels = np.asarray(Image.open(os.path.join(folder, "Spartan.png")).convert("RGB"), dtype=np.float32) / 255
    hue, saturation, value = hsv(pixels)
    visor = (saturation > 0.55) & (hue < 60)
    brightness = pixels @ np.array([0.3, 0.59, 0.11])
    # How much of a pixel is paint: dark rubber under the plates keeps its colour.
    paint = np.clip((brightness - 0.12) / 0.2, 0, 1)
    paint[visor] = 0
    for name, colour in NOBLE.items():
        tinted = (brightness / 0.62)[:, :, None] * np.array(colour, dtype=np.float32)
        out = pixels * (1 - paint[:, :, None]) + tinted * paint[:, :, None]
        Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8)).save(os.path.join(folder, f"Spartan_{name}.png"))
        print("wrote Spartan", name)


def zealot():
    folder = os.path.join(PREPARED, "Elite_Rig")
    pixels = np.asarray(Image.open(os.path.join(folder, "Elite.png")).convert("RGB"), dtype=np.float32) / 255
    hue, saturation, value = hsv(pixels)
    # The Minor's blue armour turns gold. The dark suit and the skin stay.
    armour = np.clip((saturation - 0.15) / 0.15, 0, 1) * ((hue > 170) & (hue < 270))
    brightness = pixels @ np.array([0.3, 0.59, 0.11])
    gold = np.clip((brightness * 2.1)[:, :, None] * np.array([1.0, 0.76, 0.28], dtype=np.float32), 0, 1)
    out = pixels * (1 - armour[:, :, None]) + gold * armour[:, :, None]
    Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8)).save(os.path.join(folder, "Elite_Zealot.png"))
    print("wrote Zealot")


spartan()
zealot()
