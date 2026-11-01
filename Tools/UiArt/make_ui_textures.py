# make_ui_textures.py
# Draws the faction UI textures (decision D28) into Assets/_Project/Art/UI/<Faction>/ and writes their
# .meta files (fixed GUIDs, so the theme assets in Data/UI keep pointing at them).
#   UNSC:     grey hull plating like a UNSC frigate: bevelled rim with cut corners, ribbed edges,
#             engraved plate seams, small window lights, amber accent stripes.
#   Covenant: violet hull with a metallic sheen, smooth rounded leaf-like corners, a dark crevice with
#             a glowing blue line, organic flowing engravings with blue light strips.
#   Flood:    a rim of lumpy grey-green flesh, a dark wet crevice, a sick yellow-green glow, and mottled
#             biomass with veins and boils inside (decision D38).
# Panels, rows and ornaments are drawn at double resolution (the HUD draws them at half size, so they
# stay sharp on large screens); buttons and bar fills are drawn at HUD size (Unity stretches them 1:1).
# Textures whose name ends in _Tile repeat (the HUD tiles them over panel interiors).
# Also the hexagons behind item icons (one look per faction) and the white item and card icons
# (Art/UI/Items, decision D33), which sit on those hexagons in the Inventory and over squads.
# Run:  python make_ui_textures.py   (needs numpy and Pillow; run again after changing anything here)

import hashlib
import math
import os

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "UI")
RNG = np.random.default_rng(1977)


# ---------------------------------------------------------------------------------------- canvas

class Canvas:
    """An RGBA image (straight alpha, floats 0..1) that layers are painted onto."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.rgb = np.zeros((h, w, 3))
        self.a = np.zeros((h, w))
        self.y, self.x = np.mgrid[0:h, 0:w].astype(float) + 0.5

    def paint(self, color, alpha):
        """Paints color (an RGB triple or an h x w x 3 array) with per-pixel alpha over the canvas."""
        alpha = np.clip(alpha, 0.0, 1.0)
        color = np.broadcast_to(np.asarray(color, dtype=float), self.rgb.shape)
        out_a = alpha + self.a * (1.0 - alpha)
        safe = np.maximum(out_a, 1e-6)[..., None]
        self.rgb = (color * alpha[..., None] + self.rgb * (self.a * (1.0 - alpha))[..., None]) / safe
        self.a = out_a

    def save(self, folder, name):
        os.makedirs(folder, exist_ok=True)
        data = np.dstack([np.clip(self.rgb, 0, 1), np.clip(self.a, 0, 1)])
        path = os.path.join(folder, name + ".png")
        Image.fromarray((data * 255 + 0.5).astype(np.uint8), "RGBA").save(path)
        write_texture_meta(path)
        print("wrote", os.path.relpath(path, ROOT))


def shape_distance(c, w, h, corners, kind, inset=0.0):
    """
    Signed distance (pixels, negative inside) to a w x h box placed at (inset, inset) with per-corner
    cuts or roundings: corners = (top-left, top-right, bottom-right, bottom-left) sizes.
    """
    x, y = c.x - inset, c.y - inset
    d = np.maximum(np.maximum(-x, x - w), np.maximum(-y, y - h))
    for (r, mx, my) in zip(corners, (False, True, True, False), (False, False, True, True)):
        if r <= 0:
            continue
        u = r - (w - x if mx else x)
        v = r - (h - y if my else y)
        zone = (u > 0) & (v > 0)
        if kind == "round":
            dc = np.sqrt(np.maximum(u, 0) ** 2 + np.maximum(v, 0) ** 2) - r
        else:
            dc = (u + v - r) * 0.70710678
        d = np.where(zone, np.maximum(d, dc), d)
    return d


def band(d, outer, inner, soft=0.75):
    """Coverage of the band outer > d > inner (both negative = inside), anti-aliased."""
    return np.clip((outer - d) / soft + 0.5, 0, 1) * np.clip((d - inner) / soft + 0.5, 0, 1)


def line(d, at, width):
    """Coverage of a line of this width centred on the contour d == at."""
    return np.clip(width * 0.5 + 0.5 - np.abs(d - at), 0, 1)


def facing(d, light=(-0.45, -0.89)):
    """How much the shape's outward normal faces the light (top-left): -1..1."""
    gy, gx = np.gradient(d)
    n = np.maximum(np.hypot(gx, gy), 1e-6)
    return (gx * light[0] + gy * light[1]) / n


def lerp(a, b, t):
    a, b = np.asarray(a, dtype=float), np.asarray(b, dtype=float)
    t = np.asarray(t, dtype=float)[..., None] if np.ndim(t) else t
    return a + (b - a) * t


def periodic_noise(c, size, terms, seed):
    """Smooth noise that tiles every `size` pixels (a sum of waves with whole-number frequencies)."""
    rng = np.random.default_rng(seed)
    total = np.zeros_like(c.x)
    for _ in range(terms):
        kx, ky = rng.integers(-4, 5, size=2)
        if kx == 0 and ky == 0:
            continue
        phase = rng.uniform(0, 2 * math.pi)
        total += np.sin(2 * math.pi * (kx * c.x + ky * c.y) / size + phase) / math.hypot(kx, ky)
    total -= total.min()
    return total / max(total.max(), 1e-6)


# ---------------------------------------------------------------------------------------- metas

def guid_for(path):
    rel = os.path.relpath(path, ROOT).replace("\\", "/")
    return hashlib.md5(("halobellum-ui-art-" + rel).encode()).hexdigest()


def write_texture_meta(path):
    meta = path + ".meta"
    if os.path.exists(meta):
        return
    tile = os.path.splitext(os.path.basename(path))[0].endswith("_Tile")
    wrap = 0 if tile else 1  # 0 = repeat, 1 = clamp
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


# ======================================================================================== UNSC

STEEL_DARK = (0.055, 0.062, 0.068)
STEEL = (0.30, 0.32, 0.335)
STEEL_LIGHT = (0.52, 0.55, 0.57)
AMBER = (1.0, 0.68, 0.16)
WINDOW = (1.0, 0.93, 0.76)
ORANGE_LIGHT = (1.0, 0.55, 0.16)


def unsc_panel_frame(folder):
    # 256 px at double resolution: 9-slice border 48 (24 HUD px). Interior transparent: the HUD fills
    # it with the panel colour and the hull tile first, then draws this frame on top.
    S = 256
    c = Canvas(S, S)
    corners = (30, 12, 30, 12)
    d = shape_distance(c, S - 12, S - 12, corners, "cut", inset=6)
    lit = facing(d)

    # Soft shadow outside, so the panel stands off the battlefield.
    outside = np.clip(d, 0, None)
    c.paint((0, 0, 0), np.where(d > 0, 0.45 * np.exp(-(outside / 3.5) ** 2), 0))
    # Dark outline.
    c.paint(STEEL_DARK, band(d, 0.5, -2.0))
    # Bevelled rim: lighter where it faces the light, ribbed along its length.
    rim = band(d, -1.5, -12.5)
    base = lerp(STEEL, STEEL_LIGHT, np.clip(lit * 0.5 + 0.35, 0, 1))
    gy, gx = np.gradient(d)
    horizontal_edge = np.abs(gy) > np.abs(gx) * 1.4
    vertical_edge = np.abs(gx) > np.abs(gy) * 1.4
    ribs = np.where(horizontal_edge, (np.floor(c.y) % 3 == 0), np.where(vertical_edge, (np.floor(c.x) % 3 == 0), False))
    base = base * np.where(ribs, 0.78, 1.0)[..., None]
    c.paint(base, rim)
    # Highlight on the outer lip, shadow on the inner lip of the rim.
    c.paint(STEEL_LIGHT, line(d, -2.6, 1.2) * np.clip(lit * 0.8 + 0.4, 0.1, 1))
    c.paint(STEEL_DARK, line(d, -12.0, 1.5) * 0.8)
    # Engraved seam, then a thin light line under it.
    c.paint((0.03, 0.035, 0.04), band(d, -12.5, -15.5))
    c.paint((0.45, 0.48, 0.5), line(d, -16.0, 1.0) * 0.55)
    # Inner shadow falling onto the plate.
    depth = np.clip(-d - 16.0, 0, None)
    c.paint((0, 0, 0), np.where(d < -16.0, 0.38 * np.exp(-depth / 6.0), 0))

    # Amber stripes along the two big cut corners (top-left and bottom-right).
    for corner, flip in (("tl", False), ("br", True)):
        u = (S - c.x) if flip else c.x
        v = (S - c.y) if flip else c.y
        diag = (u + v) * 0.70710678
        stripe = np.clip(1.6 - np.abs(diag - 37.5), 0, 1) * rim
        c.paint(AMBER, stripe * 0.95)
        stripe2 = np.clip(1.0 - np.abs(diag - 42.0), 0, 1) * rim
        c.paint(AMBER, stripe2 * 0.55)
    # Three small window lights on the top rim near the left corner, two near the bottom right.
    for (lx, ly, col) in ((44, 10, WINDOW), (52, 10, WINDOW), (60, 10, ORANGE_LIGHT),
                          (S - 56, S - 11, WINDOW), (S - 48, S - 11, WINDOW)):
        dot = np.clip(1.8 - np.maximum(np.abs(c.x - lx), np.abs(c.y - ly)), 0, 1)
        c.paint(col, dot)
        glow = np.exp(-((c.x - lx) ** 2 + (c.y - ly) ** 2) / 18.0) * 0.35
        c.paint(col, glow * rim)
    # Rivets in the small corners (top-right and bottom-left).
    for (rx, ry) in ((S - 22, 22), (S - 30, 14), (22, S - 22), (14, S - 30)):
        r = np.hypot(c.x - rx, c.y - ry)
        c.paint(STEEL_DARK, np.clip(2.8 - r, 0, 1))
        c.paint(STEEL_LIGHT, np.clip(1.6 - np.hypot(c.x - rx + 0.6, c.y - ry + 0.6), 0, 1))
    c.save(folder, "UNSC_PanelFrame")


def unsc_hull_tile(folder):
    # A seamless 512 px patch of hull plating (drawn at half size, so it repeats every 256 HUD px).
    # Only seams, ribs, vents and lights; the panel colour underneath shows through.
    T = 512
    c = Canvas(T, T)
    rng = np.random.default_rng(7)
    rows = [88, 56, 120, 72, 104, 72]
    assert sum(rows) == T
    y0 = 0
    seams = np.zeros((T, T), bool)
    highlights = np.zeros((T, T), bool)
    for row_index, height in enumerate(rows):
        offset = int(rng.integers(0, T))
        widths = []
        while sum(widths) < T:
            widths.append(int(rng.choice([64, 96, 128, 160, 192])))
        widths[-1] -= sum(widths) - T
        if widths[-1] < 32:
            widths[-2] += widths.pop()
        x0 = offset
        for w in widths:
            kind = rng.choice(["plain", "plain", "ribs", "ribs", "vent", "lights", "stencil"])
            xs = (np.arange(x0, x0 + w)) % T
            ys = np.arange(y0, y0 + height)
            block = np.ix_(ys, xs)
            # Seam on the plate's left and top edges (2 px dark), light line just inside.
            seams[np.ix_(ys, [xs[0], xs[1 % len(xs)]])] = True
            seams[np.ix_([y0, y0 + 1], xs)] = True
            highlights[np.ix_(ys[2:], [xs[2 % len(xs)]])] = True
            highlights[np.ix_([y0 + 2], xs[2:])] = True
            local_y = c.y[block] - y0
            local_x = (c.x[block] - x0) % T
            if kind == "ribs":
                # The ribbed look of the frigate's hull: fine horizontal grooves.
                groove = (np.floor(local_y) % 6 == 0) & (local_y > 6) & (local_y < height - 4)
                sub = np.zeros((T, T)); sub[block] = np.where(groove, 0.20, 0.0)
                c.paint((0, 0, 0), sub)
                light = (np.floor(local_y) % 6 == 1) & (local_y > 6) & (local_y < height - 4)
                sub = np.zeros((T, T)); sub[block] = np.where(light, 0.07, 0.0)
                c.paint((1, 1, 1), sub)
            elif kind == "vent" and w >= 96:
                # A recessed grille of vertical slots.
                vx0, vx1, vy0, vy1 = 16, min(w - 16, 16 + 80), 14, height - 14
                inside = (local_x > vx0) & (local_x < vx1) & (local_y > vy0) & (local_y < vy1)
                slot = inside & (np.floor(local_x - vx0) % 8 < 4)
                sub = np.zeros((T, T)); sub[block] = np.where(inside, 0.16, 0.0) + np.where(slot, 0.2, 0.0)
                c.paint((0, 0, 0), sub)
                rim = ((np.abs(local_x - vx0) < 1) | (np.abs(local_x - vx1) < 1)) & (local_y > vy0) & (local_y < vy1)
                rim |= ((np.abs(local_y - vy1) < 1)) & (local_x > vx0) & (local_x < vx1)
                sub = np.zeros((T, T)); sub[block] = np.where(rim, 0.12, 0.0)
                c.paint((1, 1, 1), sub)
            elif kind == "lights":
                # A row of small windows.
                count = int(rng.integers(2, 5))
                ly = int(rng.integers(14, height - 14))
                lx0 = int(rng.integers(12, max(13, w - count * 12 - 12)))
                sub = np.zeros((T, T))
                for i in range(count):
                    win = (np.abs(local_x - (lx0 + i * 12)) < 1.6) & (np.abs(local_y - ly) < 1.2)
                    sub[block] = np.maximum(sub[block], np.where(win, 0.38, 0.0))
                warm = ORANGE_LIGHT if rng.random() < 0.25 else WINDOW
                c.paint(warm, sub)
            elif kind == "stencil" and w >= 128:
                # Hull markings: blocks that read as stencilled text from a distance.
                sx, sy = int(rng.integers(12, 30)), int(rng.integers(12, max(13, height - 26)))
                sub = np.zeros((T, T))
                cursor = sx
                for _ in range(int(rng.integers(6, 12))):
                    gw = int(rng.integers(3, 7))
                    if cursor + gw > w - 10:
                        break
                    glyph = (local_x >= cursor) & (local_x < cursor + gw) & (local_y >= sy) & (local_y < sy + 7)
                    sub[block] = np.maximum(sub[block], np.where(glyph, 0.16, 0.0))
                    cursor += gw + 2
                bar = (local_x >= sx) & (local_x < cursor) & (local_y >= sy + 10) & (local_y < sy + 12)
                sub[block] = np.maximum(sub[block], np.where(bar, 0.12, 0.0))
                c.paint((0.85, 0.88, 0.9), sub)
            x0 += w
        y0 += height
    c.paint((0, 0, 0), np.where(seams, 0.42, 0.0))
    c.paint((1, 1, 1), np.where(highlights, 0.09, 0.0))
    # Brushed-metal streaks (horizontal, faint).
    streak = np.repeat(rng.random((T, 1)), T, axis=1)
    c.paint((1, 1, 1), np.where(streak > 0.93, 0.025, 0.0))
    c.save(folder, "UNSC_Hull_Tile")


def unsc_button(folder, name, top, bottom, edge, edge_alpha, tick_alpha, pressed=False):
    # 128 x 44 at HUD size; 9-slice border 12.
    W, H = 128, 44
    c = Canvas(W, H)
    d = shape_distance(c, W - 2, H - 2, (7, 2, 7, 2), "cut", inset=1)
    t = np.clip((c.y - 2) / (H - 4), 0, 1)
    body = lerp(top, bottom, t if not pressed else 1 - t)
    c.paint(STEEL_DARK, band(d, 0.5, -60))
    c.paint(body, band(d, -1.0, -60))
    if not pressed:
        c.paint(STEEL_LIGHT, line(d, -1.6, 1.0) * np.clip(facing(d) + 0.2, 0, 1) * 0.8)
    else:
        c.paint((0, 0, 0), np.where(d < -1, 0.35 * np.exp(-(c.y - 2) / 4.0), 0))
    c.paint(edge, line(d, -0.8, 1.2) * edge_alpha)
    # Amber tick on the left.
    tick = (c.x > 4) & (c.x < 7) & (c.y > 9) & (c.y < H - 9)
    c.paint(AMBER, np.where(tick, tick_alpha, 0.0))
    c.save(folder, name)


def unsc_row(folder):
    # Recessed plate for list rows: 128 x 80 at double resolution, border 20 (10 HUD px).
    W, H = 128, 80
    c = Canvas(W, H)
    d = shape_distance(c, W - 2, H - 2, (2, 2, 14, 2), "cut", inset=1)
    c.paint((0.045, 0.05, 0.055), band(d, 0.5, -100) * 0.88)
    depth_top = np.clip(c.y - 1, 0, None)
    c.paint((0, 0, 0), np.where(d < 0, 0.35 * np.exp(-depth_top / 5.0), 0))
    c.paint((0.5, 0.53, 0.55), line(d, -1.0, 1.4) * np.clip(-facing(d), 0, 1) * 0.5)
    c.paint((0.02, 0.02, 0.025), line(d, -0.6, 1.4) * np.clip(facing(d) + 0.3, 0, 1))
    # Amber corner bracket at the top left, grey at the bottom right.
    bracket = (((c.x > 5) & (c.x < 24) & (c.y > 5) & (c.y < 8.5)) | ((c.x > 5) & (c.x < 8.5) & (c.y > 5) & (c.y < 24)))
    c.paint(AMBER, np.where(bracket, 0.85, 0.0))
    c.save(folder, "UNSC_Row")


def unsc_bar_fill(folder):
    # One segment of a segmented bar (14 x 16 at HUD size, repeats horizontally), white so the HUD tints it.
    W, H = 14, 16
    c = Canvas(W, H)
    block = (c.x > 0) & (c.x < 11)
    shade = 1.0 - 0.3 * (c.y / H)
    col = np.dstack([shade, shade, shade])
    c.paint(col, np.where(block, 1.0, 0.0))
    c.paint((1, 1, 1), np.where(block & (c.y < 2.5), 0.6, 0.0))
    c.save(folder, "UNSC_BarSegment_Tile")


def unsc_ornament(folder):
    # A tab that hangs from the middle of a panel's top edge: 220 x 28 at double resolution.
    W, H = 220, 28
    c = Canvas(W, H)
    # Trapezoid: full width at the top, narrower at the bottom.
    half_top, half_bottom = W / 2 - 2, W / 2 - 26
    half = half_top + (half_bottom - half_top) * np.clip((c.y - 2) / (H - 4), 0, 1)
    inside = (np.abs(c.x - W / 2) < half) & (c.y > 2) & (c.y < H - 2)
    edge_d = half - np.abs(c.x - W / 2)
    c.paint(STEEL_DARK, np.where((np.abs(c.x - W / 2) < half + 1.5) & (c.y > 1) & (c.y < H - 0.5), 1.0, 0.0))
    body = lerp((0.36, 0.38, 0.40), (0.2, 0.22, 0.235), np.clip(c.y / H, 0, 1))
    c.paint(body, np.where(inside, 1.0, 0.0))
    c.paint(AMBER, np.where(inside & (c.y > H - 6) & (c.y < H - 3.5) & (edge_d > 4), 0.95, 0.0))
    for i, col in enumerate((WINDOW, WINDOW, ORANGE_LIGHT)):
        lx = W / 2 - 10 + i * 10
        c.paint(col, np.clip(2.4 - np.maximum(np.abs(c.x - lx), np.abs(c.y - 11)), 0, 1))
    # Stencil blocks on both sides of the lights.
    for side in (-1, 1):
        for i in range(5):
            gx = W / 2 + side * (26 + i * 9)
            glyph = (np.abs(c.x - gx) < 3) & (c.y > 8) & (c.y < 15)
            c.paint((0.8, 0.83, 0.85), np.where(glyph & inside, 0.35, 0.0))
    c.save(folder, "UNSC_Ornament")


# ======================================================================================== Covenant

VIOLET_DEEP = (0.07, 0.035, 0.14)
VIOLET = (0.34, 0.23, 0.6)
VIOLET_LIGHT = (0.62, 0.52, 0.92)
GLOW_BLUE = (0.25, 0.62, 1.0)
GLOW_CORE = (0.8, 0.97, 1.0)


def covenant_panel_frame(folder):
    # 256 px at double resolution, border 60 (30 HUD px). Interior transparent (see unsc_panel_frame).
    S = 256
    c = Canvas(S, S)
    corners = (36, 14, 36, 14)
    d = shape_distance(c, S - 16, S - 16, corners, "round", inset=8)
    lit = facing(d)
    outside = np.clip(d, 0, None)

    # Outer halo: violet-blue light around the hull.
    c.paint((0.4, 0.3, 1.0), np.where(d > 0, 0.32 * np.exp(-(outside / 4.5) ** 2), 0))
    c.paint(VIOLET_DEEP, band(d, 0.6, -1.5))
    # Hull rim: a rounded tube of polished violet metal (bright crest, sheen toward the light).
    t = np.clip((d + 12.0) / 11.0, 0, 1)  # 0 at the inner edge of the rim, 1 at the outer edge.
    crest = np.sin(np.pi * t) ** 3
    body = lerp(lerp(VIOLET_DEEP, VIOLET, 0.35 + 0.65 * crest), VIOLET_LIGHT, np.clip(crest ** 6 * 0.7 + lit * 0.25, 0, 1))
    c.paint(body, band(d, -1.0, -12.0))
    # Specular streak along the top-left of the crest.
    c.paint((0.9, 0.85, 1.0), line(d, -4.5, 1.5) * np.clip(lit, 0, 1) ** 2 * 0.55)
    # Dark crevice with the glowing line inside it.
    c.paint((0.03, 0.01, 0.07), band(d, -11.5, -16.5))
    glow = np.exp(-((d + 14.0) / 4.0) ** 2)
    c.paint(GLOW_BLUE, glow * 0.75 * (d < -7))
    c.paint(GLOW_CORE, line(d, -14.0, 1.6))
    # Faint inner light falling onto the interior.
    depth = np.clip(-d - 16.5, 0, None)
    c.paint(GLOW_BLUE, np.where(d < -16.5, 0.12 * np.exp(-depth / 7.0), 0))
    # Armoured swoops over the two big corners: a tapered crescent of brighter metal around the
    # corner with a glowing strip along it (like the fins and light strips of a Covenant hull).
    radius, half_max, span = 30.0, 8.0, 1.5  # Crescent centre line, half thickness, length in quarter turns.
    for (ccx, ccy, start) in ((8 + 36, 8 + 36, -math.pi), (S - 8 - 36, S - 8 - 36, 0.0)):
        r = np.hypot(c.x - ccx, c.y - ccy)
        ang = np.arctan2(c.y - ccy, c.x - ccx)
        # 0..1 along the crescent, which runs a little past the corner on both sides.
        t = np.mod(ang - start + (span - 1) * math.pi / 4, 2 * math.pi) / (span * math.pi / 2)
        on_arc = t <= 1
        taper = np.where(on_arc, np.sin(np.pi * np.clip(t, 0, 1)) ** 0.9, 0)
        half = half_max * taper
        fin_d = np.abs(r - radius) - half  # Negative inside the crescent.
        fin = np.clip(0.5 - fin_d, 0, 1) * on_arc * (taper > 0.02)
        c.paint((0.03, 0.01, 0.07), np.clip(1.6 - fin_d, 0, 1) * on_arc * (taper > 0.08))
        across = np.clip((r - radius) / np.maximum(half, 1e-3), -1, 1)  # -1 inner edge .. 1 outer edge.
        metal = lerp(VIOLET, VIOLET_LIGHT, np.clip(0.6 - across * 0.5, 0, 1) ** 1.4)
        c.paint(metal, fin)
        c.paint(GLOW_BLUE, np.exp(-((r - radius) / 2.4) ** 2) * fin * (taper > 0.3) * 0.95)
        c.paint(GLOW_CORE, np.clip(0.9 - np.abs(r - radius), 0, 1) * (taper > 0.4) * fin)
    c.save(folder, "Covenant_PanelFrame")


def covenant_hull_tile(folder):
    # Seamless 512 px organic hull patch: pearly mottling, flowing engraved lines, blue light strips.
    T = 512
    c = Canvas(T, T)
    n = periodic_noise(c, T, 14, seed=3)
    m = periodic_noise(c, T, 10, seed=11)
    tint = lerp((0.55, 0.35, 1.0), (0.3, 0.55, 1.0), m)
    c.paint(tint, 0.03 + 0.07 * n)

    tau = 2 * math.pi
    # Flowing streamlines: level sets of a field that wraps every T pixels in both directions.
    g = (4 * c.y / T
         + 0.22 * np.sin(tau * 2 * c.x / T + 0.7)
         + 0.10 * np.sin(tau * (3 * c.x + c.y) / T + 2.1)
         + 0.05 * np.sin(tau * (c.x - 2 * c.y) / T + 4.0))
    gy, gx = np.gradient(g)
    grad = np.maximum(np.hypot(gx, gy), 1e-6)
    dist = np.abs(g - np.round(g)) / grad  # Pixels to the nearest line.
    c.paint((0.02, 0.0, 0.06), np.clip(1.6 - dist, 0, 1) * 0.55)
    below = np.abs(g - np.round(g) + 2.2 * grad) / grad
    c.paint((0.75, 0.65, 1.0), np.clip(1.0 - below, 0, 1) * 0.14)
    # Second, finer family of lines between them.
    g2 = g * 2 + 0.5
    dist2 = np.abs(g2 - np.round(g2)) / (2 * grad)
    c.paint((0.02, 0.0, 0.06), np.clip(0.9 - dist2, 0, 1) * 0.16)
    # Glowing strips: parts of some lines light up blue.
    which = np.round(g) % 4
    mask = (np.sin(tau * c.x / T + 1.3 * which) > 0.72) & (which % 2 == 0)
    strip = np.where(mask, np.exp(-(dist / 3.2) ** 2), 0)
    c.paint(GLOW_BLUE, strip * 0.42)
    c.paint(GLOW_CORE, np.where(mask, np.clip(1.0 - dist, 0, 1), 0) * 0.5)
    c.save(folder, "Covenant_Hull_Tile")


def covenant_button(folder, name, top, bottom, edge, edge_alpha, inner_glow):
    # 128 x 44 at HUD size; 9-slice border 14.
    W, H = 128, 44
    c = Canvas(W, H)
    d = shape_distance(c, W - 4, H - 4, (12, 5, 12, 5), "round", inset=2)
    t = np.clip((c.y - 2) / (H - 4), 0, 1)
    c.paint(GLOW_BLUE, np.where(d > 0, inner_glow * 0.6 * np.exp(-(np.clip(d, 0, None) / 1.6) ** 2), 0))
    c.paint(VIOLET_DEEP, band(d, 0.6, -60))
    c.paint(lerp(top, bottom, t), band(d, -0.8, -60))
    # Sheen across the upper part.
    sheen = np.exp(-((c.y - 9) / 4.0) ** 2) * 0.22
    c.paint((0.95, 0.9, 1.0), sheen * band(d, -2.0, -60))
    c.paint(GLOW_BLUE, np.where(d < 0, inner_glow * np.exp(-np.clip(-d, 0, None) / 3.0), 0))
    c.paint(edge, line(d, -1.0, 1.3) * edge_alpha)
    c.save(folder, name)


def covenant_row(folder):
    W, H = 128, 80
    c = Canvas(W, H)
    d = shape_distance(c, W - 4, H - 4, (18, 6, 18, 6), "round", inset=2)
    c.paint((0.06, 0.03, 0.12), band(d, 0.5, -100) * 0.8)
    c.paint((0, 0, 0), np.where(d < 0, 0.3 * np.exp(-np.clip(c.y - 2, 0, None) / 6.0), 0))
    c.paint(GLOW_BLUE, line(d, -2.5, 1.2) * 0.45)
    c.paint(GLOW_BLUE, np.where(d < 0, 0.12 * np.exp(-np.clip(-d, 0, None) / 4.0), 0))
    c.paint(VIOLET_LIGHT, line(d, -0.5, 1.0) * np.clip(facing(d), 0, 1) * 0.5)
    strip = (np.abs(c.y - 6.5) < 1.2) & (c.x > 16) & (c.x < 40)
    c.paint(GLOW_BLUE, np.exp(-((c.y - 6.5) / 2.5) ** 2) * ((c.x > 13) & (c.x < 43)) * 0.6)
    c.paint(GLOW_CORE, np.where(strip, 0.9, 0.0))
    c.save(folder, "Covenant_Row")


def covenant_bar_fill(folder):
    # Smooth glowing bar body (8 x 20 at HUD size, stretched), white so the HUD tints it.
    W, H = 8, 20
    c = Canvas(W, H)
    t = c.y / H
    shade = 0.7 + 0.3 * np.exp(-((t - 0.3) / 0.18) ** 2) - 0.25 * t
    c.paint(np.dstack([shade, shade, shade]), np.ones_like(t))
    c.paint((1, 1, 1), np.exp(-((t - 0.25) / 0.07) ** 2) * 0.55)
    c.save(folder, "Covenant_BarFill")


def covenant_ornament(folder):
    # A glowing lens set in a swept violet casing: 200 x 34 at double resolution.
    W, H = 200, 34
    c = Canvas(W, H)
    cx, cy = W / 2, H / 2
    # Casing: tapered toward both ends (leaf shape).
    u = (c.x - cx) / (W / 2 - 4)
    half_height = (H / 2 - 3) * np.clip(1 - u ** 2, 0, 1) ** 0.8
    dist = np.abs(c.y - cy) - half_height
    c.paint((0.4, 0.3, 1.0), np.where(dist > 0, 0.3 * np.exp(-(np.clip(dist, 0, None) / 2.5) ** 2), 0) * (np.abs(u) < 1))
    casing = np.clip(0.5 - dist, 0, 1) * (np.abs(u) < 1)
    t = np.clip((c.y - (cy - half_height)) / np.maximum(2 * half_height, 1), 0, 1)
    c.paint(lerp(VIOLET_LIGHT, VIOLET_DEEP, t), casing)
    # The lens.
    e = np.hypot((c.x - cx) / 30.0, (c.y - cy) / 7.0)
    c.paint(GLOW_BLUE, np.exp(-(e / 1.3) ** 2) * 0.9)
    c.paint(GLOW_CORE, np.clip((1.0 - e) * 3, 0, 1))
    # Thin glowing lines running out along the casing.
    for sign in (-1, 1):
        run = (np.abs(c.y - cy) < 1.0) & (sign * (c.x - cx) > 32) & (sign * (c.x - cx) < 80)
        c.paint(GLOW_CORE, np.where(run, 0.8 * (1 - np.abs(c.x - cx) / 90.0), 0.0))
    c.save(folder, "Covenant_Ornament")


# ======================================================================================== Flood

FLESH_DEEP = (0.055, 0.06, 0.03)
FLESH = (0.31, 0.33, 0.17)
FLESH_LIGHT = (0.62, 0.62, 0.36)
BILE = (0.62, 0.8, 0.14)
BILE_CORE = (0.93, 1.0, 0.5)
BLOOD = (0.42, 0.12, 0.1)


def flood_panel_frame(folder):
    # 256 px at double resolution, border 60 (30 HUD px). Interior transparent (see unsc_panel_frame).
    S = 256
    c = Canvas(S, S)
    # A lumpy outline: the distance to the box is pushed in and out by smooth noise.
    lumps = periodic_noise(c, S, 16, seed=31)
    fine = periodic_noise(c, S, 24, seed=37)
    d = shape_distance(c, S - 18, S - 18, (26, 26, 26, 26), "round", inset=9) + (lumps - 0.5) * 7.0
    lit = facing(d)
    outside = np.clip(d, 0, None)
    c.paint(BILE, np.where(d > 0, 0.22 * np.exp(-(outside / 4.5) ** 2), 0))
    c.paint(FLESH_DEEP, band(d, 0.6, -1.5))
    # The rim: a swollen tube of flesh.
    t = np.clip((d + 13.0) / 12.0, 0, 1)
    crest = np.sin(np.pi * t) ** 2
    body = lerp(lerp(FLESH_DEEP, FLESH, 0.3 + 0.7 * crest), FLESH_LIGHT, np.clip(crest ** 5 * 0.5 + lit * 0.22 + (fine - 0.5) * 0.5, 0, 1))
    c.paint(body, band(d, -1.0, -13.0))
    # Veins running across the rim.
    veins = np.clip(1.4 - np.abs(lumps - 0.5) * 90.0, 0, 1)
    c.paint(BLOOD, veins * band(d, -2.0, -12.0) * 0.8)
    c.paint((0.95, 0.95, 0.7), line(d, -5.0, 1.5) * np.clip(lit, 0, 1) ** 2 * 0.35)
    # The wet crevice with its sick glow.
    c.paint((0.02, 0.025, 0.005), band(d, -12.5, -17.5))
    c.paint(BILE, np.exp(-((d + 15.0) / 3.5) ** 2) * 0.6 * (d < -8))
    c.paint(BILE_CORE, line(d, -15.0, 1.2) * (0.35 + 0.65 * fine))
    depth = np.clip(-d - 17.5, 0, None)
    c.paint(BILE, np.where(d < -17.5, 0.1 * np.exp(-depth / 7.0), 0))
    # Boils on the two big corners.
    for (bx, by, br) in ((30, 30, 13), (52, 21, 7), (21, 52, 8), (S - 30, S - 30, 13), (S - 52, S - 21, 7), (S - 21, S - 52, 8)):
        e = np.hypot(c.x - bx, c.y - by) / br
        c.paint(FLESH_DEEP, np.clip((1.18 - e) * 6, 0, 1))
        c.paint(lerp(BILE, FLESH, np.clip(e, 0, 1)), np.clip((1.0 - e) * 6, 0, 1))
        highlight = np.hypot(c.x - bx + br * 0.3, c.y - by + br * 0.3) / (br * 0.4)
        c.paint(BILE_CORE, np.clip(1.0 - highlight, 0, 1) * 0.8)
    c.save(folder, "Flood_PanelFrame")


def flood_hull_tile(folder):
    # Seamless 512 px biomass: dark mottling, branching veins, scattered boils.
    T = 512
    c = Canvas(T, T)
    n = periodic_noise(c, T, 18, seed=41)
    m = periodic_noise(c, T, 12, seed=43)
    c.paint(lerp((0.5, 0.55, 0.2), (0.45, 0.2, 0.15), m), 0.04 + 0.12 * n)
    gy, gx = np.gradient(n)
    grad = np.maximum(np.hypot(gx, gy), 1e-6)
    for level, strength in ((0.5, 0.5), (0.34, 0.28), (0.66, 0.28)):
        dist = np.abs(n - level) / grad
        c.paint((0.02, 0.025, 0.0), np.clip(1.8 - dist, 0, 1) * strength)
        c.paint(BLOOD, np.clip(1.0 - np.abs(dist - 2.0), 0, 1) * strength * 0.6)
    rng = np.random.default_rng(47)
    for _ in range(14):
        bx, by, br = rng.uniform(0, T), rng.uniform(0, T), rng.uniform(5, 13)
        dx = np.minimum(np.abs(c.x - bx), T - np.abs(c.x - bx))
        dy = np.minimum(np.abs(c.y - by), T - np.abs(c.y - by))
        e = np.hypot(dx, dy) / br
        c.paint((0.02, 0.025, 0.0), np.clip((1.2 - e) * 5, 0, 1) * 0.5)
        c.paint(BILE, np.clip((1.0 - e) * 3, 0, 1) * 0.2)
    c.save(folder, "Flood_Hull_Tile")


def flood_button(folder, name, top, bottom, edge, edge_alpha, inner_glow):
    # 128 x 44 at HUD size; 9-slice border 14.
    W, H = 128, 44
    c = Canvas(W, H)
    d = shape_distance(c, W - 4, H - 4, (9, 9, 9, 9), "round", inset=2)
    t = np.clip((c.y - 2) / (H - 4), 0, 1)
    c.paint(BILE, np.where(d > 0, inner_glow * 0.5 * np.exp(-(np.clip(d, 0, None) / 1.6) ** 2), 0))
    c.paint(FLESH_DEEP, band(d, 0.6, -60))
    c.paint(lerp(top, bottom, t), band(d, -0.8, -60))
    c.paint((0.95, 0.95, 0.7), np.exp(-((c.y - 9) / 4.0) ** 2) * 0.13 * band(d, -2.0, -60))
    c.paint(BILE, np.where(d < 0, inner_glow * np.exp(-np.clip(-d, 0, None) / 3.0), 0))
    c.paint(edge, line(d, -1.0, 1.3) * edge_alpha)
    c.save(folder, name)


def flood_row(folder):
    W, H = 128, 80
    c = Canvas(W, H)
    d = shape_distance(c, W - 4, H - 4, (12, 12, 12, 12), "round", inset=2)
    c.paint((0.05, 0.055, 0.02), band(d, 0.5, -100) * 0.82)
    c.paint((0, 0, 0), np.where(d < 0, 0.3 * np.exp(-np.clip(c.y - 2, 0, None) / 6.0), 0))
    c.paint(BILE, line(d, -2.5, 1.2) * 0.42)
    c.paint(BILE, np.where(d < 0, 0.08 * np.exp(-np.clip(-d, 0, None) / 4.0), 0))
    c.paint(FLESH_LIGHT, line(d, -0.5, 1.0) * np.clip(facing(d), 0, 1) * 0.5)
    c.save(folder, "Flood_Row")


def flood_bar_fill(folder):
    # A wet, rounded bar body (8 x 20 at HUD size, stretched), white so the HUD tints it.
    W, H = 8, 20
    c = Canvas(W, H)
    t = c.y / H
    shade = 0.65 + 0.35 * np.exp(-((t - 0.35) / 0.22) ** 2) - 0.25 * t
    c.paint(np.dstack([shade, shade, shade]), np.ones_like(t))
    c.paint((1, 1, 1), np.exp(-((t - 0.22) / 0.06) ** 2) * 0.45)
    c.save(folder, "Flood_BarFill")


def flood_ornament(folder):
    # A swollen pustule in a ridge of flesh: 200 x 34 at double resolution.
    W, H = 200, 34
    c = Canvas(W, H)
    cx, cy = W / 2, H / 2
    u = (c.x - cx) / (W / 2 - 4)
    half_height = (H / 2 - 3) * np.clip(1 - u ** 2, 0, 1) ** 0.6
    dist = np.abs(c.y - cy) - half_height
    ridge = np.clip(0.5 - dist, 0, 1) * (np.abs(u) < 1)
    t = np.clip((c.y - (cy - half_height)) / np.maximum(2 * half_height, 1), 0, 1)
    c.paint(lerp(FLESH_LIGHT, FLESH_DEEP, t), ridge)
    e = np.hypot((c.x - cx) / 18.0, (c.y - cy) / 10.0)
    c.paint(FLESH_DEEP, np.clip((1.15 - e) * 5, 0, 1))
    c.paint(BILE, np.clip((1.0 - e) * 3, 0, 1))
    c.paint(BILE_CORE, np.clip((0.5 - np.hypot((c.x - cx + 5) / 7.0, (c.y - cy + 3) / 4.0)) * 3, 0, 1))
    for bx in (-44, 44):
        s = np.hypot(c.x - cx - bx, (c.y - cy)) / 5.0
        c.paint(BILE, np.clip((1.0 - s) * 3, 0, 1) * 0.8)
    c.save(folder, "Flood_Ornament")


def flood_hexagons(folder):
    S = 128
    filled = Canvas(S, S)
    d = hex_distance(filled, S, 54)
    filled.paint(BILE, np.where(d > 0, 0.35 * np.exp(-(np.clip(d, 0, None) / 4.0) ** 2), 0))
    t = np.clip(filled.y / S, 0, 1)
    filled.paint(lerp((0.27, 0.29, 0.13), (0.09, 0.1, 0.04), t), band(d, 0.6, -80))
    filled.paint(BILE_CORE, line(d, -2.0, 2.0))
    filled.save(folder, "Flood_HexFilled")

    empty = Canvas(S, S)
    d = hex_distance(empty, S, 54)
    empty.paint(BILE, np.where(d > 0, 0.35 * np.exp(-(np.clip(d, 0, None) / 4.0) ** 2), 0))
    empty.paint((0.07, 0.08, 0.03), band(d, 0.6, -80) * 0.45)
    empty.paint(BILE_CORE, line(d, -2.0, 3.0))
    empty.save(folder, "Flood_HexEmpty")


# ======================================================================================== hexagons and icons

def hex_distance(c, size, apothem):
    """Signed distance to a pointy-topped hexagon centred in a size x size image."""
    x, y = np.abs(c.x - size / 2), np.abs(c.y - size / 2)
    return np.maximum(x, 0.8660254 * y + 0.5 * x) - apothem


def unsc_hexagons(folder):
    S = 128
    filled = Canvas(S, S)
    d = hex_distance(filled, S, 56)
    filled.paint((0, 0, 0), np.where(d > 0, 0.4 * np.exp(-(np.clip(d, 0, None) / 3.0) ** 2), 0))
    t = np.clip(filled.y / S, 0, 1)
    filled.paint(lerp((0.25, 0.28, 0.3), (0.1, 0.115, 0.125), t), band(d, 0.6, -80))
    filled.paint(STEEL_LIGHT, line(d, -1.5, 2.0))
    filled.paint(AMBER, line(d, -6.0, 2.0) * 0.9)
    filled.save(folder, "UNSC_HexFilled")

    empty = Canvas(S, S)
    d = hex_distance(empty, S, 56)
    empty.paint((0.05, 0.06, 0.065), band(d, 0.6, -80) * 0.45)
    empty.paint(AMBER, line(d, -2.0, 4.0))
    empty.save(folder, "UNSC_HexEmpty")


def covenant_hexagons(folder):
    S = 128
    filled = Canvas(S, S)
    d = hex_distance(filled, S, 54)
    filled.paint(GLOW_BLUE, np.where(d > 0, 0.45 * np.exp(-(np.clip(d, 0, None) / 4.0) ** 2), 0))
    t = np.clip(filled.y / S, 0, 1)
    filled.paint(lerp((0.3, 0.19, 0.52), (0.1, 0.05, 0.2), t), band(d, 0.6, -80))
    filled.paint(GLOW_CORE, line(d, -2.0, 2.0))
    filled.save(folder, "Covenant_HexFilled")

    empty = Canvas(S, S)
    d = hex_distance(empty, S, 54)
    empty.paint(GLOW_BLUE, np.where(d > 0, 0.45 * np.exp(-(np.clip(d, 0, None) / 4.0) ** 2), 0))
    empty.paint((0.08, 0.04, 0.16), band(d, 0.6, -80) * 0.45)
    empty.paint(GLOW_CORE, line(d, -2.0, 3.0))
    empty.save(folder, "Covenant_HexEmpty")


def save_icon(folder, name, draw_function):
    """Draws a white icon at 4x and scales it down to 96 x 96, so its edges are smooth."""
    big = 384
    image = Image.new("RGBA", (big, big), (255, 255, 255, 0))
    draw_function(ImageDraw.Draw(image), big)
    image = image.resize((96, 96), Image.LANCZOS)
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name + ".png")
    image.save(path)
    write_texture_meta(path)
    print("wrote", os.path.relpath(path, ROOT))


WHITE = (255, 255, 255, 255)
CLEAR = (255, 255, 255, 0)


def icon_laser(d, s):
    # A crosshair.
    m = s / 2
    d.ellipse([m - 110, m - 110, m + 110, m + 110], outline=WHITE, width=26)
    d.ellipse([m - 24, m - 24, m + 24, m + 24], fill=WHITE)
    for a, b in (((m, 20), (m, 110)), ((m, s - 110), (m, s - 20)), ((20, m), (110, m)), ((s - 110, m), (s - 20, m))):
        d.line([a, b], fill=WHITE, width=26)


def icon_absorb(d, s):
    # A shield with a drop cut out of it.
    d.polygon([(70, 60), (s - 70, 60), (s - 70, 210), (s / 2, 340), (70, 210)], fill=WHITE)
    m = s / 2
    d.polygon([(m, 95), (m + 52, 195), (m - 52, 195)], fill=CLEAR)
    d.ellipse([m - 52, 150, m + 52, 254], fill=CLEAR)


def icon_core(d, s):
    # A core with rays.
    m = s / 2
    for i in range(8):
        a = i * math.pi / 4
        d.line([(m + 80 * math.cos(a), m + 80 * math.sin(a)), (m + 165 * math.cos(a), m + 165 * math.sin(a))], fill=WHITE, width=30)
    d.ellipse([m - 66, m - 66, m + 66, m + 66], fill=WHITE)


def icon_deploy(d, s):
    # Four arrows: move anywhere.
    m = s / 2
    d.line([(m, 80), (m, s - 80)], fill=WHITE, width=32)
    d.line([(80, m), (s - 80, m)], fill=WHITE, width=32)
    w = 62
    d.polygon([(m, 16), (m - w, 100), (m + w, 100)], fill=WHITE)
    d.polygon([(m, s - 16), (m - w, s - 100), (m + w, s - 100)], fill=WHITE)
    d.polygon([(16, m), (100, m - w), (100, m + w)], fill=WHITE)
    d.polygon([(s - 16, m), (s - 100, m - w), (s - 100, m + w)], fill=WHITE)


def icon_haste(d, s):
    # Double chevrons pointing up.
    for y in (70, 190):
        d.polygon([(s / 2, y), (s - 60, y + 110), (s - 60, y + 180), (s / 2, y + 70), (60, y + 180), (60, y + 110)], fill=WHITE)


def icon_armor(d, s):
    # A solid shield with a bar.
    d.polygon([(60, 50), (s - 60, 50), (s - 60, 210), (s / 2, 350), (60, 210)], fill=WHITE)
    d.rectangle([s / 2 - 90, 130, s / 2 + 90, 164], fill=CLEAR)


def icon_repair(d, s):
    # A medical cross.
    m = s / 2
    d.rectangle([m - 50, 50, m + 50, s - 50], fill=WHITE)
    d.rectangle([50, m - 50, s - 50, m + 50], fill=WHITE)


def icon_firepower(d, s):
    # A shell pointing up with a blast line.
    m = s / 2
    d.polygon([(m, 24), (m + 62, 130), (m - 62, 130)], fill=WHITE)
    d.rectangle([m - 62, 130, m + 62, 270], fill=WHITE)
    d.rectangle([m - 92, 296, m + 92, 340], fill=WHITE)


def icon_supply(d, s):
    # A crate.
    d.rectangle([60, 110, s - 60, s - 60], fill=WHITE)
    d.polygon([(60, 110), (120, 50), (s - 120, 50), (s - 60, 110)], fill=WHITE)
    d.rectangle([s / 2 - 22, 130, s / 2 + 22, s - 60], fill=CLEAR)


def icon_super_supply(d, s):
    # A crate with an up arrow.
    d.rectangle([70, 200, s - 70, s - 40], fill=WHITE)
    m = s / 2
    d.polygon([(m, 20), (m + 90, 120), (m - 90, 120)], fill=WHITE)
    d.rectangle([m - 36, 120, m + 36, 180], fill=WHITE)


def icon_offense(d, s):
    # Crossed blades.
    d.line([(70, 70), (s - 90, s - 90)], fill=WHITE, width=40)
    d.line([(s - 70, 70), (90, s - 90)], fill=WHITE, width=40)
    d.line([(50, 300), (130, 220)], fill=WHITE, width=34)
    d.line([(s - 50, 300), (s - 130, 220)], fill=WHITE, width=34)


def icon_defense(d, s):
    # A shield outline with a smaller shield inside.
    d.polygon([(60, 50), (s - 60, 50), (s - 60, 210), (s / 2, 350), (60, 210)], fill=WHITE)
    d.polygon([(100, 88), (s - 100, 88), (s - 100, 195), (s / 2, 298), (100, 195)], fill=CLEAR)
    d.polygon([(140, 130), (s - 140, 130), (s - 140, 185), (s / 2, 245), (140, 185)], fill=WHITE)


def icon_targeting(d, s):
    # Target rings.
    m = s / 2
    d.ellipse([m - 150, m - 150, m + 150, m + 150], outline=WHITE, width=28)
    d.ellipse([m - 84, m - 84, m + 84, m + 84], outline=WHITE, width=28)
    d.ellipse([m - 26, m - 26, m + 26, m + 26], fill=WHITE)


def icon_slot(d, s):
    # A plus inside a square slot.
    d.rectangle([56, 56, s - 56, s - 56], outline=WHITE, width=28)
    m = s / 2
    d.rectangle([m - 24, 110, m + 24, s - 110], fill=WHITE)
    d.rectangle([110, m - 24, s - 110, m + 24], fill=WHITE)


def icon_training(d, s):
    # A rising bar chart with a chevron: rank up.
    for i, h in enumerate((90, 170, 250)):
        x = 60 + i * 96
        d.rectangle([x, s - 50 - h, x + 72, s - 50], fill=WHITE)
    d.polygon([(240, 30), (340, 30), (340, 130)], fill=WHITE)


ICONS = {
    "Item_LaserSights": icon_laser, "Item_AbsorptionModule": icon_absorb, "Item_AmplifyingCore": icon_core,
    "Item_DeploymentModule": icon_deploy, "Item_HasteStims": icon_haste, "Item_HeavyArmor": icon_armor,
    "Item_RepairKit": icon_repair, "Item_ImprovedFirepower": icon_firepower,
    "Card_SupplyEnhancement": icon_supply, "Card_SuperSupply": icon_super_supply, "Card_AdvancedOffense": icon_offense,
    "Card_AdvancedDefense": icon_defense, "Card_AdvancedTargeting": icon_targeting, "Card_DeploymentSlot": icon_slot,
    "Card_IntensiveTraining": icon_training,
}


def main():
    items = os.path.join(OUT, "Items")
    for name, draw in ICONS.items():
        save_icon(items, name, draw)

    unsc = os.path.join(OUT, "UNSC")
    unsc_hexagons(unsc)
    unsc_panel_frame(unsc)
    unsc_hull_tile(unsc)
    unsc_button(unsc, "UNSC_Button", (0.25, 0.28, 0.30), (0.14, 0.16, 0.175), STEEL_DARK, 1.0, 0.55)
    unsc_button(unsc, "UNSC_ButtonHover", (0.34, 0.38, 0.40), (0.2, 0.23, 0.25), AMBER, 0.95, 1.0)
    unsc_button(unsc, "UNSC_ButtonPressed", (0.09, 0.1, 0.11), (0.15, 0.165, 0.18), AMBER, 0.9, 1.0, pressed=True)
    unsc_row(unsc)
    unsc_bar_fill(unsc)
    unsc_ornament(unsc)

    cov = os.path.join(OUT, "Covenant")
    covenant_hexagons(cov)
    covenant_panel_frame(cov)
    covenant_hull_tile(cov)
    covenant_button(cov, "Covenant_Button", (0.32, 0.2, 0.52), (0.17, 0.09, 0.3), GLOW_BLUE, 0.55, 0.0)
    covenant_button(cov, "Covenant_ButtonHover", (0.42, 0.28, 0.66), (0.24, 0.13, 0.42), GLOW_CORE, 1.0, 0.35)
    covenant_button(cov, "Covenant_ButtonPressed", (0.12, 0.06, 0.22), (0.18, 0.09, 0.32), GLOW_CORE, 1.0, 0.6)
    covenant_row(cov)
    covenant_bar_fill(cov)
    covenant_ornament(cov)


def main_flood():
    folder = os.path.join(OUT, "Flood")
    flood_hexagons(folder)
    flood_panel_frame(folder)
    flood_hull_tile(folder)
    flood_button(folder, "Flood_Button", (0.27, 0.29, 0.13), (0.13, 0.14, 0.05), BILE, 0.5, 0.08)
    flood_button(folder, "Flood_ButtonHover", (0.37, 0.4, 0.17), (0.2, 0.22, 0.08), BILE_CORE, 0.7, 0.16)
    flood_button(folder, "Flood_ButtonPressed", (0.1, 0.11, 0.04), (0.17, 0.19, 0.07), BILE, 0.7, 0.2)
    flood_row(folder)
    flood_bar_fill(folder)
    flood_ornament(folder)


if __name__ == "__main__":
    main()
    main_flood()
