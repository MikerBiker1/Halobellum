# icon_sheet.py
# A contact sheet of the unit icons, to look at them outside the game: every PNG in Resources/UnitIcons, on a dark card colour,
# each with its name. Run: python icon_sheet.py out.png   (Pillow)
import glob
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
files = sorted(glob.glob(os.path.join(ROOT, "Assets", "_Project", "Resources", "UnitIcons", "*.png")))
cell, columns = 200, 8
rows = (len(files) + columns - 1) // columns
sheet = Image.new("RGBA", (columns * cell, rows * (cell + 18)), (24, 26, 30, 255))
draw = ImageDraw.Draw(sheet)
for i, path in enumerate(files):
    icon = Image.open(path).convert("RGBA").resize((cell, cell), Image.LANCZOS)
    x, y = (i % columns) * cell, (i // columns) * (cell + 18)
    sheet.alpha_composite(icon, (x, y))
    draw.text((x + 4, y + cell + 2), os.path.splitext(os.path.basename(path))[0], fill=(220, 220, 220, 255))
sheet.convert("RGB").save(sys.argv[1])
