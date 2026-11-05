# make_blueprint_textures.py
# Draws the art of the neutral theme (D68) into Assets/_Project/Art/UI/Blueprint/ and writes their .meta files (fixed GUIDs, so that
# Data/UI/Neutral_Theme.asset keeps pointing at them): deep blue drafting paper with a grid, thin drawn lines, corner marks and dimension lines.
# Unlike make_ui_textures.py it needs nothing but Python itself.
# Panels, rows and ornaments are drawn at double resolution (the HUD draws them at half size); buttons and bar fills at HUD size.
# Textures whose name ends in _Tile repeat.
# Run:  python make_blueprint_textures.py   (it prints each texture's GUID)

import hashlib
import math
import os
import struct
import zlib

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FOLDER = os.path.join(ROOT, "Assets", "_Project", "Art", "UI", "Blueprint")

PAPER = (0.055, 0.2, 0.44)
PAPER_LIGHT = (0.1, 0.3, 0.58)
PAPER_DARK = (0.03, 0.12, 0.3)
INK = (0.9, 0.96, 1.0)
INK_CYAN = (0.55, 0.88, 1.0)
SHADOW = (0.0, 0.03, 0.1)


class Canvas:
    """An RGBA image, straight alpha, that layers are painted onto. A layer is a function from a pixel's middle to how much of the colour it gets."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.pixels = [[0.0, 0.0, 0.0, 0.0] for _ in range(w * h)]

    def paint(self, color, coverage):
        for y in range(self.h):
            for x in range(self.w):
                alpha = coverage(x + 0.5, y + 0.5)
                if alpha <= 0.0:
                    continue
                alpha = min(alpha, 1.0)
                p = self.pixels[y * self.w + x]
                out = alpha + p[3] * (1.0 - alpha)
                for i in range(3):
                    p[i] = (color[i] * alpha + p[i] * p[3] * (1.0 - alpha)) / out
                p[3] = out

    def save(self, name):
        os.makedirs(FOLDER, exist_ok=True)
        path = os.path.join(FOLDER, name + ".png")
        rows = bytearray()
        for y in range(self.h):
            rows.append(0)
            for x in range(self.w):
                for value in self.pixels[y * self.w + x]:
                    rows.append(int(max(0.0, min(1.0, value)) * 255 + 0.5))

        def chunk(kind, data):
            return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

        with open(path, "wb") as f:
            f.write(b"\x89PNG\r\n\x1a\n")
            f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", self.w, self.h, 8, 6, 0, 0, 0)))
            f.write(chunk(b"IDAT", zlib.compress(bytes(rows), 9)))
            f.write(chunk(b"IEND", b""))
        write_meta(path)
        print(name, guid_for(path))


def guid_for(path):
    rel = os.path.relpath(path, ROOT).replace("\\", "/")
    return hashlib.md5(("halobellum-ui-art-" + rel).encode()).hexdigest()


def write_meta(path):
    meta = path + ".meta"
    if os.path.exists(meta):
        return
    wrap = 0 if os.path.splitext(os.path.basename(path))[0].endswith("_Tile") else 1  # 0 = repeat, 1 = clamp
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(f"""fileFormatVersion: 2
guid: {guid_for(path)}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: {wrap}
    wrapV: {wrap}
    wrapW: {wrap}
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 2
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
  userData:
  assetBundleName:
  assetBundleVariant:
""")


def box_distance(w, h, inset):
    """Distance from a point to the edge of a box inset from the image's sides: negative inside."""
    def distance(x, y):
        return max(inset - x, x - (w - inset), inset - y, y - (h - inset))
    return distance


def edge_line(d, at, width):
    """Coverage of a line of this width along the contour d == at."""
    return lambda x, y: min(1.0, max(0.0, width * 0.5 + 0.5 - abs(d(x, y) - at)))


def inside(d, outer, inner, strength=1.0):
    """Coverage of the band between two contours."""
    def coverage(x, y):
        v = d(x, y)
        return strength * min(1.0, max(0.0, outer - v + 0.5)) * min(1.0, max(0.0, v - inner + 0.5))
    return coverage


def panel_frame():
    # 256 px at double resolution, border 48 (24 HUD px). Its middle is left empty. Two drawn lines, the outer one overshooting at each
    # corner as a draftsman's does, with a small registration mark inside each corner.
    S, inset = 256, 9
    c = Canvas(S, S)
    d = box_distance(S, S, inset)
    c.paint(SHADOW, lambda x, y: 0.5 * math.exp(-(d(x, y) / 3.0) ** 2) if d(x, y) > 0 else 0.0)
    c.paint(PAPER_DARK, inside(d, 0.5, -9.0, 0.55))
    c.paint(INK, lambda x, y: 0.95 * edge_line(d, -0.5, 2.2)(x, y))
    c.paint(INK_CYAN, lambda x, y: 0.6 * edge_line(d, -8.5, 1.3)(x, y))
    for ax, ay in ((inset, inset), (S - inset, inset), (inset, S - inset), (S - inset, S - inset)):
        c.paint(INK, lambda x, y, ax=ax, ay=ay: 0.95 if (abs(y - ay) < 1.1 and abs(x - ax) < inset) or (abs(x - ax) < 1.1 and abs(y - ay) < inset) else 0.0)
        mx, my = ax + (20 if ax < S / 2 else -20), ay + (20 if ay < S / 2 else -20)
        c.paint(INK_CYAN, lambda x, y, mx=mx, my=my: 0.85 * min(1.0, max(0.0, 1.2 - abs(math.hypot(x - mx, y - my) - 5.5))))
        c.paint(INK_CYAN, lambda x, y, mx=mx, my=my: 0.7 if (abs(x - mx) < 0.8 and abs(y - my) < 9) or (abs(y - my) < 0.8 and abs(x - mx) < 9) else 0.0)
    c.save("Blueprint_PanelFrame")


def grid_tile():
    # Drafting paper: a fine grid every 16 HUD px, a stronger line every 80, at double resolution. Its size is a whole number of both, so it has no seam.
    T = 320
    c = Canvas(T, T)
    c.paint(INK_CYAN, lambda x, y: 0.11 if int(x) % 32 < 2 or int(y) % 32 < 2 else 0.0)
    c.paint(INK_CYAN, lambda x, y: 0.2 if int(x) % 160 < 2 or int(y) % 160 < 2 else 0.0)
    c.save("Blueprint_Grid_Tile")


def button(name, fill, edge, edge_alpha, ticks):
    # 128 x 44 at HUD size; 9-slice border 12. A drawn box with corner ticks standing off it.
    W, H = 128, 44
    c = Canvas(W, H)
    d = box_distance(W, H, 3)
    c.paint(fill, inside(d, 0.5, -60.0))
    c.paint(edge, lambda x, y: edge_alpha * edge_line(d, -0.5, 1.5)(x, y))
    if ticks:
        for ax, ay in ((0, 0), (W, 0), (0, H), (W, H)):
            c.paint(INK, lambda x, y, ax=ax, ay=ay: 1.0 if (abs(x - ax) < 10 and abs(y - ay) < 1.5) or (abs(y - ay) < 10 and abs(x - ax) < 1.5) else 0.0)
    c.save(name)


def row():
    # A list row: darker paper in a thin line. 128 x 80 at double resolution, border 20.
    W, H = 128, 80
    c = Canvas(W, H)
    d = box_distance(W, H, 1)
    c.paint(PAPER_DARK, inside(d, 0.5, -100.0, 0.72))
    c.paint(INK_CYAN, lambda x, y: 0.6 * edge_line(d, -1.0, 1.5)(x, y))
    c.save("Blueprint_Row")


def bar_fill():
    # Hatching: one slanted stroke that repeats (14 x 16 at HUD size), white so that the HUD can tint it. Its slant meets itself across the 14 px repeat.
    W, H = 14, 16
    c = Canvas(W, H)
    c.paint((0.72, 0.72, 0.72), lambda x, y: 1.0)
    c.paint((1.0, 1.0, 1.0), lambda x, y: max(0.0, 1.0 - abs((x + y) % 14 - 7.0) / 3.5))
    c.save("Blueprint_Hatch_Tile")


def ornament():
    # A dimension line, as on a drawing: |<---- o ---->| on the panel's top edge. 220 x 28 at double resolution.
    W, H = 220, 28
    cy, left, right = H / 2, 10.0, W - 10.0
    c = Canvas(W, H)
    c.paint(INK, lambda x, y: 0.95 if abs(y - cy) < 1.0 and left < x < right and abs(x - W / 2) > 16 else 0.0)
    for at, sign in ((left, 1), (right, -1)):
        c.paint(INK, lambda x, y, at=at: 0.95 if abs(x - at) < 1.0 and abs(y - cy) < 10 else 0.0)
        c.paint(INK, lambda x, y, at=at, sign=sign: 1.0 if 0 < (x - at) * sign < 14 and abs(y - cy) < (x - at) * sign * 0.33 else 0.0)
    c.paint(INK_CYAN, lambda x, y: min(1.0, max(0.0, 1.2 - abs(math.hypot(x - W / 2, y - cy) - 7.0))))
    c.paint(INK_CYAN, lambda x, y: min(1.0, max(0.0, 2.6 - math.hypot(x - W / 2, y - cy))))
    c.save("Blueprint_Ornament")


def hexagons():
    S, apothem = 128, 56

    def d(x, y):
        ax, ay = abs(x - S / 2), abs(y - S / 2)
        return max(ax, 0.8660254 * ay + 0.5 * ax) - apothem

    filled = Canvas(S, S)
    filled.paint(SHADOW, lambda x, y: 0.4 * math.exp(-(d(x, y) / 3.0) ** 2) if d(x, y) > 0 else 0.0)
    filled.paint(PAPER, inside(d, 0.6, -80.0))
    filled.paint(INK, edge_line(d, -1.5, 2.0))
    filled.paint(INK_CYAN, lambda x, y: 0.7 * edge_line(d, -6.0, 1.3)(x, y))
    filled.save("Blueprint_HexFilled")

    empty = Canvas(S, S)
    empty.paint(PAPER_DARK, inside(d, 0.6, -80.0, 0.45))
    empty.paint(INK, edge_line(d, -2.0, 3.0))
    empty.save("Blueprint_HexEmpty")


if __name__ == "__main__":
    hexagons()
    panel_frame()
    grid_tile()
    button("Blueprint_Button", PAPER_DARK, INK_CYAN, 0.75, True)
    button("Blueprint_ButtonHover", PAPER_LIGHT, INK, 1.0, True)
    button("Blueprint_ButtonPressed", PAPER, INK, 1.0, False)
    row()
    bar_fill()
    ornament()
