# make_card_art.py
# Draws the art of the card screens (M10), all of it original and made here with numpy and Pillow:
#   Resources/CardArt/Frames/Frame_<kind>.png   the card borders, one per kind of card (see FRAMES below)
#   Resources/CardArt/Frames/Ribbon_<kind>.png  the label banner of each kind
#   Resources/CardArt/Glow.png, Stage.png, Wash.png   hover glow, the light behind a card's big picture, the card's inner tint
#   Resources/CardArt/Emblems/*.png             white emblems: spells, kinds of card, factions (tinted when drawn)
#   Resources/CardArt/Layers/Body_*.png, Role_*.png   what UnitIcons builds a unit's fallback picture from
# Frames are 512 x 512 and are drawn at half size by CardArt.DrawFrame: the outer 160 pixels are the corners, and the
# middle 192 pixels of each edge REPEAT along the edge instead of stretching. So everything on an edge is periodic
# with period 192 (dents, rivets, growths, teeth), and the corners hold the big shapes. Nothing depends on a card's size.
# Unity makes the .meta files (never written by hand). CardArtImport sets the import settings.
# Run:  python make_card_art.py [--sheet out.png]    (numpy and Pillow; run again after changing anything here)

import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "CardArt")

SIZE = 512
B = 160     # Corner size in texture pixels.
P = SIZE - 2 * B  # The repeating middle of each edge.
SS = 2      # Everything is drawn at twice the size and scaled down.
C = SIZE / 2.0

_yy, _xx = np.mgrid[0:SIZE * SS, 0:SIZE * SS].astype(np.float32)
X = (_xx + 0.5) / SS
Y = (_yy + 0.5) / SS
ALONG = np.where(np.abs(X - C) > np.abs(Y - C), Y, X)   # Position along the nearest edge.
ON_SIDE = np.abs(X - C) > np.abs(Y - C)                 # True on the left and right edges.
FROM_EDGE_MIDDLE = np.abs(ALONG - C)                    # How far along an edge from its middle.


def on_edge(extra=0.0):
    """True on the repeating middle of each edge, and `extra` pixels on into the corners."""
    return FROM_EDGE_MIDDLE <= P / 2.0 + extra


def whole_repeats(period, phase=0.0, extra=0.0):
    """True where a shape repeating every `period` pixels sits wholly on the edge: it is kept or dropped as a whole, never cut in half."""
    centre = B + (np.floor((ALONG - B) / period + phase) + 0.5 - phase) * period
    return np.abs(centre - C) <= P / 2.0 + extra


def fade_into_corners(start, end):
    """1 on the middle of each edge, fading to 0 between `start` and `end` pixels beyond its repeating part."""
    return 1.0 - smooth(P / 2.0 + start, P / 2.0 + end, FROM_EDGE_MIDDLE)


def smooth(edge0, edge1, v):
    t = np.clip((v - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def inside(d, soft=0.6):
    """Coverage of a shape from its signed distance (negative inside)."""
    return np.clip(0.5 - d / (2.0 * soft), 0.0, 1.0)


def box(inset, r=0.0, chamfer=0.0):
    """Signed distance to a centred square, with rounded or cut corners."""
    h = C - inset
    qx, qy = np.abs(X - C) - h, np.abs(Y - C) - h
    if chamfer > 0.0:
        return np.maximum(np.maximum(qx, qy), (qx + qy + chamfer) / math.sqrt(2.0))
    if r > 0.0:
        qx, qy = qx + r, qy + r
        return np.hypot(np.maximum(qx, 0.0), np.maximum(qy, 0.0)) + np.minimum(np.maximum(qx, qy), 0.0) - r
    return np.maximum(qx, qy)


def periodic_noise(seed, terms=26, kmax=7):
    """Smooth noise that repeats every P pixels in both directions, so edges tile. About -1 to 1."""
    rng = np.random.default_rng(seed)
    out = np.zeros_like(X)
    total = 0.0
    for _ in range(terms):
        kx, ky = rng.integers(-kmax, kmax + 1, 2)
        if kx == 0 and ky == 0:
            continue
        amp = 1.0 / math.hypot(kx, ky)
        phase = rng.uniform(0.0, math.tau)
        out += amp * np.cos(math.tau * (kx * X + ky * Y) / P + phase)
        total += amp
    return out / (total * 0.45)


def line_along(period, phase=0.0):
    """0 to 1 sawtooth position along the edge, repeating `period` pixels (must divide P)."""
    return ((ALONG - B) / period + phase) % 1.0


def mirrored(fn):
    """A coverage made of one corner's shape repeated in all four corners (fn gets distances from the top-left corner)."""
    x = np.minimum(X, SIZE - X)
    y = np.minimum(Y, SIZE - Y)
    return fn(x, y)


def polygon_mask(points_list):
    """Coverage of polygons given in texture pixels."""
    image = Image.new("L", (SIZE * SS, SIZE * SS), 0)
    d = ImageDraw.Draw(image)
    for points in points_list:
        d.polygon([(px * SS, py * SS) for px, py in points], fill=255)
    return np.asarray(image, dtype=np.float32) / 255.0


def gauss(a, sigma):
    """Gaussian blur (FFT; sigma in texture pixels)."""
    sigma = sigma * SS
    n = a.shape[0]
    f = np.fft.fftfreq(n)
    g = np.exp(-2.0 * (math.pi * sigma * f) ** 2)
    spec = np.fft.rfft2(a)
    return np.fft.irfft2(spec * g[:, None] * g[: n // 2 + 1][None, :], s=a.shape).astype(np.float32)


def shade(height, strength=26.0, spec_power=40.0):
    """Diffuse and specular light on a height field, lit from the top left."""
    gy, gx = np.gradient(height)
    nx, ny, nz = -gx * strength, -gy * strength, np.ones_like(height)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    nx, ny, nz = nx / length, ny / length, nz / length
    lx, ly, lz = -0.48, -0.66, 0.58
    diffuse = np.clip(nx * lx + ny * ly + nz * lz, 0.0, 1.0)
    hx, hy, hz = lx, ly, lz + 1.0
    hl = math.sqrt(hx * hx + hy * hy + hz * hz)
    spec = np.clip((nx * hx + ny * hy + nz * hz) / hl, 0.0, 1.0) ** spec_power
    return diffuse, spec


class Canvas:
    """A straight-alpha picture layers are painted onto."""

    def __init__(self):
        self.rgb = np.zeros((SIZE * SS, SIZE * SS, 3), dtype=np.float32)
        self.a = np.zeros((SIZE * SS, SIZE * SS), dtype=np.float32)

    def paint(self, color, alpha):
        alpha = np.clip(alpha, 0.0, 1.0)
        color = np.broadcast_to(np.asarray(color, dtype=np.float32), self.rgb.shape)
        out = alpha + self.a * (1.0 - alpha)
        safe = np.maximum(out, 1e-5)[..., None]
        self.rgb = (color * alpha[..., None] + self.rgb * (self.a * (1.0 - alpha))[..., None]) / safe
        self.a = out

    def add_light(self, color, amount):
        """Brightens what is there (glow lines, sparks); makes it opaque enough to show."""
        amount = np.clip(amount, 0.0, 1.0)
        self.rgb = np.clip(self.rgb + np.asarray(color, dtype=np.float32) * amount[..., None], 0.0, 1.0)
        self.a = np.maximum(self.a, amount)

    def save(self, path, fringe):
        premultiplied = self.rgb * self.a[..., None]
        data = np.dstack([premultiplied, self.a])
        h, w = data.shape[:2]
        small = data.reshape(h // SS, SS, w // SS, SS, 4).mean(axis=(1, 3))
        alpha = small[..., 3]
        rgb = small[..., :3] / np.maximum(alpha, 1e-4)[..., None]
        # Fully clear pixels keep the frame's colour: bilinear scaling then leaves no dark fringe.
        rgb[alpha < 0.004] = fringe
        out = np.dstack([np.clip(rgb, 0, 1), np.clip(alpha, 0, 1)])
        os.makedirs(os.path.dirname(path), exist_ok=True)
        Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA").save(path)
        print("wrote", os.path.relpath(path, ROOT))


def rgb(hex_color):
    return tuple(int(hex_color[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def metal(canvas, mask, height, dark, light, noise=None, strength=26.0, spec=0.7, spec_power=40.0, spec_color=(1, 1, 1)):
    """Paints a bevelled metal surface: colour from the light on its height field, with a highlight."""
    diffuse, highlight = shade(height, strength, spec_power)
    t = np.clip(diffuse * 1.35 + (0.0 if noise is None else noise * 0.09), 0.0, 1.0)[..., None]
    surface = np.asarray(dark, np.float32) * (1.0 - t) + np.asarray(light, np.float32) * t
    surface = surface + np.asarray(spec_color, np.float32) * (highlight * spec)[..., None]
    canvas.paint(np.clip(surface, 0, 1), mask)


def inner_shadow(canvas, window_d, width=16.0, strength=0.85):
    """The band's shadow falling on the card: a dark edge inside the window, so that text stays clear of it."""
    canvas.paint((0.0, 0.0, 0.0), inside(window_d) * (1.0 - smooth(0.0, width, -window_d)) * strength)


def bevel(d, width):
    """Height rising from a shape's edge, over `width` pixels."""
    return smooth(0.0, width, -d)


# ---------------------------------------------------------------------------------------- the frames

def frame_unsc(path):
    """UNSC reinforcement: cut corners, steel plates with seams and rivets, and hazard stripes."""
    outer, window = box(24.0, chamfer=62.0), box(58.0, chamfer=40.0)
    band = inside(outer) * (1.0 - inside(window))
    noise = periodic_noise(11)
    plate_seams = (np.abs(((ALONG - B) % (P / 2.0)) - 12.0) < 1.2) & on_edge(0.0)
    mid = np.abs(-outer - 13.0) < 0.9
    height = bevel(outer, 7.0) * bevel(-window, 6.0) - 0.34 * mid - 0.5 * plate_seams
    c = Canvas()
    metal(c, band, height, rgb("23282c"), rgb("9aa6ad"), noise, strength=30.0, spec=0.55, spec_power=26.0)
    # Scratches and wear.
    wear = np.clip(periodic_noise(12, kmax=7) * 0.5 + periodic_noise(13, kmax=7) * 0.5 - 0.72, 0.0, 1.0)
    c.paint(rgb("c9d0d2"), band * wear * 0.65)

    # Hazard stripes: on a strip along the inside of the band, at the top and the bottom.
    stripes = (((X + Y) / 32.0) % 1.0) < 0.5
    hazard_color = np.where(stripes[..., None], np.asarray(rgb("e0a81c"), np.float32), np.asarray(rgb("15161a"), np.float32))
    strip = inside(np.abs(outer + 26.0) - 5.0) * smooth(C - 92.0, C - 70.0, np.maximum(np.abs(X - C), np.abs(Y - C)))
    strip = strip * (np.abs(Y - C) > np.abs(X - C))  # Strips along the top and bottom edges.
    hazard = strip * band
    c.paint(hazard_color, hazard)
    diffuse, _ = shade(height, 30.0)
    c.paint((0, 0, 0), hazard * np.clip(0.55 - diffuse, 0.0, 0.5))

    # Rivets along the edges, and larger bolts in the corners.
    rivet_u = (((ALONG - B) / 48.0) % 1.0 - 0.5) * 48.0
    rivet = inside(np.hypot(rivet_u, -outer - 6.0) - 2.6) * on_edge(26.0)
    c.paint(rgb("c6ced1"), rivet * band)
    bolts = mirrored(lambda x, y: inside(np.hypot(x - 92.0, y - 41.0) - 4.2) + inside(np.hypot(x - 41.0, y - 92.0) - 4.2))
    c.paint(rgb("dfe5e7"), np.clip(bolts, 0, 1))
    inner_shadow(c, window)
    c.save(path, rgb("5a666d"))


def frame_covenant(path):
    """Covenant reinforcement: sweeping violet alloy with long curved tips and lines of blue light."""
    outer_core = box(30.0, r=64.0)
    # Blades that sweep away from two opposite corners, like a hull's fins.
    blades = []
    for flip in (False, True):
        fin = []
        for t in np.linspace(0.0, 1.0, 40):
            fin.append((6.0 + 146.0 * t ** 1.8, 6.0 + 22.0 * t))   # Stays inside the corner: the edge beyond it repeats.
        for t in np.linspace(1.0, 0.0, 40):
            fin.append((8.0 + 22.0 * t, 6.0 + 146.0 * t ** 1.8))
        blades.append([(SIZE - x, SIZE - y) for x, y in fin] if flip else fin)
    blade_mask = polygon_mask(blades)
    blade_d = np.minimum(outer_core, (0.5 - gauss(blade_mask, 1.2)) * 8.0)
    outer = blade_d
    window = box(62.0, r=48.0)
    band = inside(outer) * (1.0 - inside(window))
    noise = periodic_noise(21)
    height = bevel(outer, 10.0) * bevel(-window, 12.0) + noise * 0.02
    c = Canvas()
    # Soft blue halo outside the hull.
    halo = gauss(inside(outer), 7.0) * (1.0 - inside(outer))
    c.paint(rgb("5fc4ff"), halo * 0.7)
    metal(c, band, height, rgb("1b0a33"), rgb("9a68dd"), noise, strength=24.0, spec=0.85, spec_power=22.0, spec_color=(0.8, 0.7, 1.0))
    # Sheen: a hue shift that flows along the band, as on polished alloy.
    sheen = 0.5 + 0.5 * np.cos(math.tau * (X + Y) / P + noise * 1.4)
    c.paint(rgb("d47bf2"), band * sheen * 0.16)

    # The main line of light along the middle of the band, and a thin wave beside it.
    middle = np.abs(box(46.0, r=56.0))
    glow = inside(middle - 1.1, soft=0.8) * band
    halo_line = gauss(glow, 3.5) * band
    c.add_light(rgb("53b8ff"), halo_line * 1.5)
    c.add_light(rgb("d8f6ff"), glow)
    wave_d = np.abs(box(55.0, r=52.0) + 3.5 * np.sin(math.tau * (ALONG - B) / 96.0))
    c.add_light(rgb("7a64ff"), inside(wave_d - 0.5, soft=0.7) * band * 0.75)
    # Lights at the corners.
    nodes = mirrored(lambda x, y: inside(np.hypot(x - 76.0, y - 76.0) - 8.0))
    node_ring = mirrored(lambda x, y: inside(np.abs(np.hypot(x - 76.0, y - 76.0) - 15.0) - 1.3))
    c.paint(rgb("1b0a33"), mirrored(lambda x, y: inside(np.hypot(x - 76.0, y - 76.0) - 20.0)) * 0.9)
    c.add_light(rgb("d8f6ff"), nodes)
    c.add_light(rgb("53b8ff"), node_ring * 0.9)
    inner_shadow(c, window, 18.0, 0.8)
    c.save(path, rgb("6a48aa"))


def frame_flood(path):
    """Flood reinforcement: a rim of lumpy flesh that grows over the card, veins, pustules and a sick glow."""
    rng = np.random.default_rng(31)
    lump = periodic_noise(32, terms=30, kmax=5)
    # Growths in the corners, a few outward, more over the card.
    corner_blobs = np.full_like(X, 999.0)
    x, y = np.minimum(X, SIZE - X), np.minimum(Y, SIZE - Y)
    for bx, by, br in ((42, 42, 30), (82, 28, 20), (28, 82, 22), (72, 72, 26), (112, 40, 13), (40, 112, 14), (100, 66, 14), (66, 104, 15)):
        corner_blobs = np.minimum(corner_blobs, np.hypot(x - bx, y - by) - br)
    outer = np.minimum(box(30.0, r=40.0) + lump * 10.0, corner_blobs + lump * 3.0)
    window = np.maximum(box(62.0, r=50.0) - periodic_noise(33, terms=30, kmax=5) * 11.0, -(corner_blobs - 4.0 + lump * 3.0))
    band = inside(outer) * (1.0 - inside(window))
    pattern = periodic_noise(34, terms=40, kmax=7)
    # Ridged veins: lines where a noise crosses zero.
    veins = np.clip(1.0 - np.abs(periodic_noise(35, terms=30, kmax=6)) * 7.0, 0.0, 1.0) ** 2
    height = bevel(outer, 14.0) * bevel(-window, 14.0) * (0.75 + 0.25 * pattern) + veins * 0.1
    c = Canvas()
    glow = gauss(inside(outer), 7.0) * (1.0 - inside(outer))
    c.paint(rgb("b8c43a"), glow * 0.5)
    metal(c, band, height, rgb("23220f"), rgb("9d9b63"), pattern, strength=34.0, spec=0.32, spec_power=18.0, spec_color=(0.9, 1.0, 0.6))
    c.paint(rgb("5b2f2a"), band * smooth(0.25, 0.9, periodic_noise(36, terms=26, kmax=4)) * 0.55)
    c.paint(rgb("c9d14a"), band * veins * 0.42)
    c.add_light(rgb("c6d83a"), inside(-(window) - 3.0, soft=4.0) * band * (1.0 - inside(-(window) - 9.0, soft=4.0)) * 0.26)

    # Pustules: on the edges (repeating, so that they tile) and in the corners.
    def pustule(px, py, r):
        d = np.hypot(X - px, Y - py) - r
        cover = inside(d)
        c.paint(rgb("3c3b10"), inside(d - 2.0) * 0.8)
        c.paint(rgb("a6ad2e"), cover)
        c.add_light(rgb("e4ec5e"), cover * smooth(r, 0.0, np.hypot(X - px + r * 0.25, Y - py + r * 0.3)) * 0.7)
        c.paint((1, 1, 0.8), inside(np.hypot(X - px + r * 0.35, Y - py + r * 0.4) - r * 0.2) * 0.9)

    for along, across, r in ((B + 26, 40, 6), (B + 74, 46, 8), (B + 132, 36, 5), (B + 172, 44, 6)):
        for shift in (0.0, -P, P):
            pustule(along + shift, across, r)
            pustule(along + 11 + shift, SIZE - across - 2, r)
            pustule(across + 1, along + 30 + shift, r)
            pustule(SIZE - across, along - 22 + shift, r)
    for px, py, r in ((40, 44, 11), (84, 30, 7), (28, 90, 8), (70, 74, 9), (112, 42, 5), (42, 116, 5)):
        pustule(px, py, r)
        pustule(SIZE - px, py + 3, r * 0.85)
        pustule(px + 2, SIZE - py, r * 0.9)
        pustule(SIZE - px - 4, SIZE - py - 2, r)
    inner_shadow(c, window, 18.0, 0.85)
    c.save(path, rgb("6e6e3a"))


def frame_spell_offensive(path):
    """Offensive spell: charred black iron with a red-hot edge, saw teeth all round, and spikes at the corners."""
    tooth = np.abs(line_along(48.0) - 0.5) * 2.0
    teeth = (tooth - 0.5) * 18.0
    outer_body = box(30.0, chamfer=30.0) + teeth * fade_into_corners(8.0, 40.0)
    spikes = []
    for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
        def corner(px, py):
            return (C + sx * (C - px), C + sy * (C - py))
        spikes.append([corner(4, 4), corner(96, 34), corner(70, 46), corner(46, 70), corner(34, 96)])
        spikes.append([corner(18, 56), corner(10, 124), corner(40, 96), corner(46, 66)])
        spikes.append([corner(56, 18), corner(124, 10), corner(96, 40), corner(66, 46)])
    spike_mask = polygon_mask(spikes)
    outer = np.minimum(outer_body, (0.5 - gauss(spike_mask, 0.7)) * 6.0)
    window = box(60.0, chamfer=18.0)
    band = inside(outer) * (1.0 - inside(window))
    noise = periodic_noise(41, terms=36, kmax=7)
    height = bevel(outer, 6.0) * bevel(-window, 7.0) + noise * 0.05
    c = Canvas()
    fire = gauss(inside(outer), 8.0) * (1.0 - inside(outer))
    c.paint(rgb("ff3a12"), fire * 0.75)
    metal(c, band, height, rgb("150606"), rgb("7d2a22"), noise, strength=30.0, spec=0.5, spec_power=22.0, spec_color=(1.0, 0.55, 0.35))
    # Cracks with heat in them.
    cracks = np.clip(1.0 - np.abs(periodic_noise(42, terms=30, kmax=7)) * 9.0, 0.0, 1.0) ** 2
    c.add_light(rgb("ff5a1c"), band * cracks * 0.6)
    # The red-hot inner edge.
    hot = inside(np.abs(window) - 2.2, soft=0.9)
    c.add_light(rgb("ff4a14"), gauss(hot, 2.0) * 1.1 * (1.0 - inside(window) * 0.6))
    c.add_light(rgb("ffd27a"), hot * band * 0.9)
    inner_shadow(c, window, 14.0, 0.75)
    c.save(path, rgb("7a1d10"))


def frame_spell_defensive(path):
    """Defensive spell: a smooth blue shield: rounded, two rings with a gap of light, and shields at the corners."""
    outer, gap_out, gap_in, window = box(26.0, r=76.0), box(36.0, r=66.0), box(42.0, r=60.0), box(60.0, r=46.0)
    ring = inside(outer) * (1.0 - inside(gap_out)) + inside(gap_in) * (1.0 - inside(window))
    ring = np.clip(ring, 0.0, 1.0)
    height = np.minimum(bevel(outer, 5.0) + bevel(gap_in, 8.0), 1.0) * bevel(-window, 8.0)
    noise = periodic_noise(51, terms=20, kmax=4)
    c = Canvas()
    halo = gauss(inside(outer), 9.0) * (1.0 - inside(outer))
    c.paint(rgb("3f8cff"), halo * 0.8)
    # The light between the rings: energy, with a faint hex pattern.
    gap = inside(gap_out) * (1.0 - inside(gap_in))
    c.paint(rgb("7ec8ff"), gap * 0.9)
    metal(c, ring, height, rgb("0a1c4c"), rgb("6c9ff0"), noise, strength=20.0, spec=0.95, spec_power=30.0, spec_color=(0.75, 0.9, 1.0))
    # A honeycomb of light on the inner ring.
    u, v = (X - B) / 24.0, (Y - B) / 24.0
    hex_a = np.abs(np.cos(math.pi * u))
    hex_b = np.abs(np.cos(math.pi * (u * 0.5 + v)))
    hex_c = np.abs(np.cos(math.pi * (u * 0.5 - v)))
    cells = np.clip(1.0 - np.minimum(np.minimum(hex_a, hex_b), hex_c) * 9.0, 0.0, 1.0)
    inner_ring = inside(gap_in) * (1.0 - inside(window))
    c.add_light(rgb("7ec8ff"), cells * inner_ring * 0.35)
    # A small shield in each corner.
    shields = []
    for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
        cx, cy = C + sx * (C - 73), C + sy * (C - 73)
        pts = []
        for t in np.linspace(-1.0, 1.0, 32):
            pts.append((cx + t * 18.0, cy - 15.0 - 3.0 * (1.0 - t * t)))
        for t in np.linspace(1.0, -1.0, 32):
            pts.append((cx + t * 18.0 * (1.0 - abs(t) * 0.05), cy + 22.0 - 21.0 * abs(t) ** 1.6))
        shields.append(pts)
    mask = polygon_mask(shields)
    c.paint(rgb("08163c"), gauss(mask, 2.5) * 1.1)
    c.paint(rgb("c6e6ff"), mask)
    smaller = gauss(mask, 3.0)
    c.paint(rgb("2b6fdc"), smooth(0.75, 0.9, smaller))
    inner_shadow(c, window, 16.0, 0.7)
    c.save(path, rgb("2d6ad8"))


def frame_economy(path):
    """Economy: deep green plating with a gold trim, gold coins along the edges and a big coin in each corner."""
    outer, window = box(26.0, r=22.0), box(60.0, r=14.0)
    band = inside(outer) * (1.0 - inside(window))
    noise = periodic_noise(61, terms=20, kmax=5)
    height = bevel(outer, 5.0) * bevel(-window, 5.0)
    c = Canvas()
    metal(c, band, height, rgb("052a16"), rgb("2fa563"), noise, strength=22.0, spec=0.45, spec_power=30.0)
    trim_a = inside(np.abs(outer + 4.0) - 1.4) * inside(outer)
    trim_b = inside(np.abs(window - 3.0) - 1.4) * (1.0 - inside(window))
    c.paint(rgb("f4cf5a"), np.clip(trim_a + trim_b, 0, 1))
    # Coins along the edges, repeating.
    coin_u = (line_along(64.0) - 0.5) * 64.0
    coin_v = -outer - 17.0
    coin_d = np.hypot(coin_u, coin_v)
    away = whole_repeats(64.0, extra=32.0)
    coin = inside(coin_d - 11.0) * away
    c.paint(rgb("3d2c05"), inside(coin_d - 12.5) * away * 0.8)
    c.paint(rgb("f2c842"), coin)
    c.paint(rgb("9d7a1a"), inside(np.abs(coin_d - 7.5) - 1.0) * away)
    c.paint(rgb("fff0a8"), inside(np.hypot(coin_u + 3.0, coin_v + 3.0) - 2.2) * away)
    # A large coin in each corner.
    def big(x, y):
        return np.hypot(x - 58.0, y - 58.0)
    big_d = mirrored(big)
    c.paint(rgb("1a1403"), inside(big_d - 38.0) * 0.9)
    c.paint(rgb("c8981f"), inside(big_d - 34.0))
    c.paint(rgb("f6d560"), inside(big_d - 30.0))
    c.paint(rgb("b48a1e"), inside(np.abs(big_d - 24.0) - 1.6))
    # Reeded rim.
    angle = mirrored(lambda x, y: np.arctan2(y - 58.0, x - 58.0))
    c.paint(rgb("8c6a12"), inside(big_d - 34.0) * (1.0 - inside(big_d - 30.0)) * (np.sin(angle * 26.0) > 0.0))
    # A credit sign: two bars.
    bars = mirrored(lambda x, y: inside(np.maximum(np.abs(x - 58.0) - 11.0, np.abs(np.abs(y - 58.0) - 6.0) - 2.6)))
    c.paint(rgb("7a5a0e"), bars)
    inner_shadow(c, window, 14.0, 0.75)
    c.save(path, rgb("1f7a48"))


def frame_buff(path):
    """Buff: orchid alloy with a crown of rising points on top, chevrons rising up the sides, and swept-up top corners."""
    crown_wave = np.abs(line_along(48.0) - 0.5) * 2.0
    top_edge = (~ON_SIDE) & (Y < C)
    outer_body = box(32.0, chamfer=20.0) - top_edge * (1.0 - crown_wave) * 16.0 * fade_into_corners(10.0, 40.0)
    wings = []
    for sx in (1, -1):
        def corner(px, py):
            return (C + sx * (C - px), py)
        wings.append([corner(6, 8), corner(30, 42), corner(66, 34), corner(112, 30), corner(62, 66), corner(34, 118), corner(28, 74)])
    wing_mask = polygon_mask(wings)
    outer = np.minimum(outer_body, (0.5 - gauss(wing_mask, 0.8)) * 6.0)
    window = box(60.0, chamfer=14.0)
    band = inside(outer) * (1.0 - inside(window))
    noise = periodic_noise(71, terms=20, kmax=5)
    height = bevel(outer, 5.0) * bevel(-window, 6.0)
    c = Canvas()
    halo = gauss(inside(outer), 6.0) * (1.0 - inside(outer))
    c.paint(rgb("f07de8"), halo * 0.6)
    metal(c, band, height, rgb("350c3b"), rgb("e38ae0"), noise, strength=22.0, spec=0.85, spec_power=30.0, spec_color=(1.0, 0.85, 1.0))
    # Chevrons rising up the left and right edges.
    u = (((Y - B) / 32.0) % 1.0) * 32.0
    across = np.abs(np.minimum(X, SIZE - X) - 44.0)
    chevron = (np.abs((u + across * 0.75) % 32.0 - 11.0) < 4.2) & (across < 11.0) & ON_SIDE & on_edge(36.0)
    c.paint(rgb("ffe0f8"), chevron.astype(np.float32) * band * 0.95)
    # A double line on the top and bottom edges.
    top_bottom = (~ON_SIDE) & on_edge(30.0)
    line_d = np.abs(np.minimum(Y, SIZE - Y) - 46.0)
    c.paint(rgb("ffe0f8"), inside(np.abs(line_d - 3.5) - 1.1) * top_bottom * band)
    # A rising star in each top corner, a small point in each bottom corner.
    star = []
    for sx in (1, -1):
        cx, cy = C + sx * (C - 58), 58
        pts = []
        for i in range(16):
            r = 19.0 if i % 2 == 0 else 6.5
            a = math.tau * i / 16.0 - math.pi / 2.0
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
        star.append(pts)
    c.paint(rgb("fff4fd"), polygon_mask(star))
    inner_shadow(c, window, 14.0, 0.75)
    c.save(path, rgb("b04fb4"))


def _hexagon_d(x, y, cx, cy, r):
    """Distance to a flat-topped hexagon."""
    qx, qy = np.abs(x - cx), np.abs(y - cy)
    return np.maximum(qy - r * 0.866, qx * 0.866 + qy * 0.5 - r * 0.866)


def frame_item(path):
    """Item: brass and gunmetal like a machine part: notched cut corners, hex bolts along the edges, a hex nut in each corner."""
    outer_plain = box(26.0, chamfer=46.0)
    notch_u = np.abs(line_along(96.0) - 0.5) * 96.0
    notches = (notch_u < 14.0) & on_edge(20.0)
    outer = np.where(notches, outer_plain + 7.0, outer_plain)
    window = box(60.0, chamfer=24.0)
    band = inside(outer) * (1.0 - inside(window))
    noise = periodic_noise(81, terms=20, kmax=6)
    # Machined grooves running along the edges.
    grooves = (np.sin(-outer * 1.6) > 0.82) * 0.03
    height = bevel(outer, 5.0) * bevel(-window, 5.0) + grooves
    c = Canvas()
    metal(c, band, height, rgb("2b1c06"), rgb("e3b84e"), noise, strength=24.0, spec=0.75, spec_power=24.0, spec_color=(1.0, 0.92, 0.65))
    # Gunmetal inner step.
    step = inside(window - 9.0) * (1.0 - inside(window))
    c.paint(rgb("23262a"), step * 0.95)
    c.paint(rgb("f0cf73"), inside(np.abs(window - 9.0) - 0.9) * band)
    # Hex bolts on the edges, repeating.
    u = (line_along(96.0, 0.5) - 0.5) * 96.0
    v = -outer_plain - 15.0
    away = whole_repeats(96.0, phase=0.5, extra=0.5)
    bolt = _hexagon_d(u, v, 0.0, 0.0, 10.0)
    c.paint(rgb("1b1204"), inside(bolt - 2.0) * away * 0.8)
    c.paint(rgb("cfd3d6"), inside(bolt) * away)
    c.paint(rgb("3d4247"), inside(np.hypot(u, v) - 3.4) * away)
    # A hex nut in each corner.
    x, y = np.minimum(X, SIZE - X), np.minimum(Y, SIZE - Y)
    nut = _hexagon_d(x, y, 60.0, 60.0, 31.0)
    c.paint(rgb("120c02"), inside(nut - 4.0) * 0.85)
    nut_height = bevel(nut, 8.0)
    metal(c, inside(nut), nut_height, rgb("54585c"), rgb("e4e8ea"), None, strength=20.0, spec=0.7, spec_power=24.0)
    hole = np.hypot(x - 60.0, y - 60.0)
    c.paint(rgb("0d0e10"), inside(hole - 11.5))
    c.paint(rgb("e3b84e"), inside(np.abs(hole - 11.5) - 1.5))
    inner_shadow(c, window, 12.0, 0.7)
    c.save(path, rgb("b4902e"))


def frame_training(path):
    """Training: a stitched teal uniform patch: rounded, a dashed seam all round, and rank chevrons in each corner."""
    outer, window = box(28.0, r=30.0), box(60.0, r=20.0)
    band = inside(outer) * (1.0 - inside(window))
    # Woven cloth rather than metal.
    weave = (np.sin(X * math.tau / 4.0) * np.sin(Y * math.tau / 4.0)) * 0.04
    noise = periodic_noise(91, terms=16, kmax=4)
    height = bevel(outer, 4.0) * bevel(-window, 4.0) + weave
    c = Canvas()
    metal(c, band, height, rgb("06302f"), rgb("46cdbb"), noise, strength=16.0, spec=0.12, spec_power=12.0)
    # Stitches.
    dash = (((ALONG - B) / 16.0) % 1.0) < 0.58
    seam = inside(np.abs(outer + 7.0) - 1.5) * dash
    c.paint(rgb("ecfffb"), seam * band)
    seam2 = inside(np.abs(window - 6.0) - 1.3) * dash
    c.paint(rgb("ecfffb"), seam2 * band * 0.85)
    # Three rank chevrons in each corner, pointing into the corner.
    x, y = np.minimum(X, SIZE - X), np.minimum(Y, SIZE - Y)
    cover = np.zeros_like(X)
    for offset in (0.0, 15.0, 30.0):
        d = np.maximum(x, y)  # A right-angled chevron with its point towards the card's corner.
        cover = np.maximum(cover, inside(np.abs(d - (46.0 + offset)) - 4.4) * (np.minimum(x, y) > 32.0) * (d < 100.0))
    c.paint(rgb("052826"), gauss(cover, 1.5) * 0.9)
    c.paint(rgb("ffe28a"), cover)
    inner_shadow(c, window, 12.0, 0.7)
    c.save(path, rgb("1f9c90"))


def frame_neutral(path):
    """Anything else (a faction the art doesn't know yet): plain grey plating."""
    outer, window = box(28.0, chamfer=26.0), box(60.0, chamfer=14.0)
    band = inside(outer) * (1.0 - inside(window))
    noise = periodic_noise(101, terms=20, kmax=5)
    height = bevel(outer, 5.0) * bevel(-window, 5.0)
    c = Canvas()
    metal(c, band, height, rgb("1e2024"), rgb("90949c"), noise, strength=22.0, spec=0.4, spec_power=30.0)
    inner_shadow(c, window, 12.0, 0.7)
    c.save(path, rgb("5a5e66"))


FRAMES = {
    "SpellOffensive": frame_spell_offensive,
    "SpellDefensive": frame_spell_defensive,
    "Economy": frame_economy,
    "Buff": frame_buff,
    "Item": frame_item,
    "Training": frame_training,
    "UNSC": frame_unsc,
    "Covenant": frame_covenant,
    "Flood": frame_flood,
    "Neutral": frame_neutral,
}


# ---------------------------------------------------------------------------------------- ribbons, glow, stage, wash

# Each ribbon: top colour, bottom colour, edge colour, end shape.
RIBBONS = {
    "SpellOffensive": ("c02a14", "6a0f08", "ffb06a", "tail"),
    "SpellDefensive": ("2d6be0", "12317e", "bfe2ff", "round"),
    "Economy": ("1f9a52", "0a4d29", "f4cf5a", "round"),
    "Buff": ("b84ab8", "5f1a66", "ffe0f8", "point"),
    "Item": ("b98a23", "664708", "ffe7a3", "cut"),
    "Training": ("1d9c8e", "0b504b", "ffe28a", "tail"),
    "UNSC": ("566169", "262c31", "e0a81c", "cut"),
    "Covenant": ("6c3bb3", "2d1259", "7ad2ff", "point"),
    "Flood": ("6d7230", "2e3112", "d0dc55", "round"),
    "Neutral": ("565a62", "2a2c31", "c9ccd2", "cut"),
}


def ribbon(path, top, bottom, edge, shape):
    """A banner: 256 x 72, drawn at half size; its ends (48 pixels each) stay fixed, its middle stretches."""
    w, h, s = 256, 72, 4
    image = Image.new("L", (w * s, h * s), 0)
    d = ImageDraw.Draw(image)
    m, W, H = 6 * s, w * s, h * s
    if shape == "cut":
        pts = [(m + 18 * s, m), (W - m - 18 * s, m), (W - m, m + 18 * s), (W - m, H - m - 18 * s), (W - m - 18 * s, H - m), (m + 18 * s, H - m), (m, H - m - 18 * s), (m, m + 18 * s)]
        d.polygon(pts, fill=255)
    elif shape == "tail":
        pts = [(m, m), (W - m, m), (W - m - 24 * s, H / 2), (W - m, H - m), (m, H - m), (m + 24 * s, H / 2)]
        d.polygon(pts, fill=255)
    elif shape == "point":
        pts = [(m + 30 * s, m), (W - m - 30 * s, m), (W - m, H / 2), (W - m - 30 * s, H - m), (m + 30 * s, H - m), (m, H / 2)]
        d.polygon(pts, fill=255)
    else:
        d.rounded_rectangle((m, m, W - m, H - m), radius=(H - 2 * m) // 2, fill=255)
    mask = np.asarray(image, dtype=np.float32) / 255.0
    edge_band = mask - np.asarray(image.filter(ImageFilter.MinFilter(9)), dtype=np.float32) / 255.0
    yy = np.linspace(0.0, 1.0, H, dtype=np.float32)[:, None, None]
    color = np.asarray(rgb(top), np.float32) * (1.0 - yy) + np.asarray(rgb(bottom), np.float32) * yy
    # A highlight along the top, as on bent metal.
    color = color + smooth(0.34, 0.2, yy) * 0.08
    color = color * (1.0 - edge_band[..., None]) + np.asarray(rgb(edge), np.float32) * edge_band[..., None]
    data = np.dstack([np.clip(color, 0, 1) * mask[..., None], mask])
    small = data.reshape(h, s, w, s, 4).mean(axis=(1, 3))
    alpha = small[..., 3]
    out_rgb = small[..., :3] / np.maximum(alpha, 1e-4)[..., None]
    out_rgb[alpha < 0.004] = rgb(top)
    out = np.dstack([np.clip(out_rgb, 0, 1), alpha])
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA").save(path)
    print("wrote", os.path.relpath(path, ROOT))


def save_alpha(path, alpha):
    """A white picture with this alpha."""
    out = np.dstack([np.ones_like(alpha), np.ones_like(alpha), np.ones_like(alpha), np.clip(alpha, 0, 1)])
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA").save(path)
    print("wrote", os.path.relpath(path, ROOT))


def make_common():
    # The glow around a hovered card: a soft ring, 9-sliced with fixed 48-pixel corners.
    n = 128
    y, x = np.mgrid[0:n, 0:n].astype(np.float32) + 0.5
    qx, qy = np.abs(x - n / 2.0) - (n / 2.0 - 40.0), np.abs(y - n / 2.0) - (n / 2.0 - 40.0)
    d = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0)
    glow = np.clip(1.0 - d / 38.0, 0.0, 1.0) ** 2 * smooth(-30.0, -6.0, d)
    save_alpha(os.path.join(OUT, "Glow.png"), glow)

    # The light behind a card's big picture: a soft disc and a thin ring.
    n = 256
    y, x = np.mgrid[0:n, 0:n].astype(np.float32) + 0.5
    r = np.hypot(x - n / 2.0, y - n / 2.0) / (n / 2.0)
    stage = np.clip(1.0 - r, 0.0, 1.0) ** 1.5 * 0.85
    ring = np.exp(-((r - 0.78) / 0.008) ** 2) * 0.5
    ticks = np.exp(-((r - 0.84) / 0.012) ** 2) * (np.sin(np.arctan2(y - n / 2.0, x - n / 2.0) * 24.0) > 0.86) * 0.5
    save_alpha(os.path.join(OUT, "Stage.png"), np.clip(stage + ring + ticks, 0.0, 1.0))

    # The tint inside a card: strong at the top, fading out by two thirds of the way down.
    n = 256
    yy = (np.arange(n, dtype=np.float32) + 0.5) / n
    wash = np.clip(1.0 - yy / 0.72, 0.0, 1.0) ** 1.4
    save_alpha(os.path.join(OUT, "Wash.png"), np.tile(wash[:, None], (1, 8)))


# ---------------------------------------------------------------------------------------- emblems

WHITE = (255, 255, 255, 255)
CLEAR = (255, 255, 255, 0)


def star_points(cx, cy, outer, inner, count, turn=-math.pi / 2.0):
    pts = []
    for i in range(count * 2):
        r = outer if i % 2 == 0 else inner
        a = turn + math.pi * i / count
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def puff(d, cx, cy, r, color=WHITE):
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=color)


def thick_line(d, points, width, color=WHITE):
    d.line(points, fill=color, width=int(width), joint="curve")
    r = width / 2.0
    for px, py in (points[0], points[-1]):
        d.ellipse((px - r, py - r, px + r, py + r), fill=color)


def blast_rings(d, s, cx, cy, count=2, base=0.12):
    for i in range(count):
        r = s * (base + i * 0.09)
        d.ellipse((cx - r, cy - r * 0.42, cx + r, cy + r * 0.42), outline=WHITE, width=int(s * 0.03))


def e_strike(d, s):
    # A bolt coming down onto a point.
    d.polygon([(s * 0.56, s * 0.08), (s * 0.3, s * 0.48), (s * 0.48, s * 0.48), (s * 0.38, s * 0.76), (s * 0.7, s * 0.38), (s * 0.52, s * 0.38), (s * 0.68, s * 0.08)], fill=WHITE)
    blast_rings(d, s, s * 0.46, s * 0.84, 2, 0.14)


def e_mac(d, s):
    # A long slug falling from orbit, with its trail, onto an impact.
    d.polygon([(s * 0.5, s * 0.72), (s * 0.42, s * 0.56), (s * 0.42, s * 0.2), (s * 0.58, s * 0.2), (s * 0.58, s * 0.56)], fill=WHITE)
    for dx, top in ((-0.13, 0.04), (0.0, 0.0), (0.13, 0.08)):
        d.line([(s * (0.5 + dx), s * top), (s * (0.5 + dx), s * 0.16)], fill=WHITE, width=int(s * 0.03))
    blast_rings(d, s, s * 0.5, s * 0.82, 3, 0.11)


def e_missile(d, s, cx, cy, size, angle):
    pts = [(0, -1.0), (0.28, -0.55), (0.28, 0.45), (0.62, 0.95), (0.28, 0.72), (-0.28, 0.72), (-0.62, 0.95), (-0.28, 0.45), (-0.28, -0.55)]
    ca, sa = math.cos(angle), math.sin(angle)
    d.polygon([(cx + (px * ca - py * sa) * size, cy + (px * sa + py * ca) * size) for px, py in pts], fill=WHITE)


def e_missiles(d, s):
    a = math.radians(180 + 30)
    for cx, cy in ((0.24, 0.3), (0.56, 0.2), (0.44, 0.66), (0.78, 0.54)):
        e_missile(d, s, s * cx, s * cy, s * 0.22, a)


def e_nuke(d, s):
    # A mushroom cloud: a billowing cap, a stem, a ground ring.
    for cx, cy, r in ((0.5, 0.26, 0.2), (0.3, 0.32, 0.14), (0.7, 0.32, 0.14), (0.4, 0.2, 0.12), (0.6, 0.2, 0.12), (0.22, 0.38, 0.08), (0.78, 0.38, 0.08)):
        puff(d, s * cx, s * cy, s * r)
    d.rectangle((s * 0.18, s * 0.4, s * 0.82, s * 0.46), fill=CLEAR)
    d.polygon([(s * 0.42, s * 0.42), (s * 0.58, s * 0.42), (s * 0.63, s * 0.82), (s * 0.37, s * 0.82)], fill=WHITE)
    d.ellipse((s * 0.14, s * 0.76, s * 0.86, s * 0.94), fill=WHITE)
    d.ellipse((s * 0.34, s * 0.81, s * 0.66, s * 0.89), fill=CLEAR)


def e_plasma_shot(d, s):
    # One round plasma bolt with a tail, falling.
    d.polygon([(s * 0.72, s * 0.06), (s * 0.3, s * 0.52), (s * 0.58, s * 0.74)], fill=WHITE)
    puff(d, s * 0.42, s * 0.62, s * 0.2)
    puff(d, s * 0.42, s * 0.62, s * 0.11, CLEAR)
    puff(d, s * 0.42, s * 0.62, s * 0.05)


def e_plasma_storm(d, s):
    # Several bolts raining out of a dark swirl.
    for cx, r in ((0.3, 0.1), (0.46, 0.13), (0.63, 0.11), (0.76, 0.08)):
        puff(d, s * cx, s * 0.24, s * r)
    d.rectangle((s * 0.2, s * 0.26, s * 0.84, s * 0.32), fill=WHITE)
    for cx, cy, r in ((0.26, 0.5, 0.05), (0.46, 0.6, 0.06), (0.66, 0.5, 0.05), (0.36, 0.78, 0.055), (0.58, 0.82, 0.05), (0.8, 0.72, 0.045)):
        d.polygon([(s * (cx + 0.06), s * (cy - 0.14)), (s * (cx - r * 0.8), s * cy), (s * (cx + r * 0.8), s * (cy + r * 0.4))], fill=WHITE)
        puff(d, s * cx, s * cy, s * r)


def e_beam(d, s):
    # A wide beam coming straight down, with a molten disc under it.
    d.polygon([(s * 0.4, s * 0.02), (s * 0.6, s * 0.02), (s * 0.58, s * 0.74), (s * 0.42, s * 0.74)], fill=WHITE)
    d.ellipse((s * 0.12, s * 0.66, s * 0.88, s * 0.94), fill=WHITE)
    d.ellipse((s * 0.26, s * 0.74, s * 0.74, s * 0.88), fill=CLEAR)
    d.polygon([(s * 0.42, s * 0.72), (s * 0.58, s * 0.72), (s * 0.57, s * 0.82), (s * 0.43, s * 0.82)], fill=WHITE)
    for cx, cy in ((0.2, 0.42), (0.82, 0.5), (0.28, 0.2), (0.74, 0.24)):
        d.polygon(star_points(s * cx, s * cy, s * 0.06, s * 0.016, 4), fill=WHITE)


def e_drop_pod(d, s):
    # A drop pod coming in nose down, trailing flame.
    d.polygon([(s * 0.5, s * 0.86), (s * 0.34, s * 0.62), (s * 0.34, s * 0.3), (s * 0.66, s * 0.3), (s * 0.66, s * 0.62)], fill=WHITE)
    d.rectangle((s * 0.34, s * 0.44, s * 0.66, s * 0.48), fill=CLEAR)
    d.polygon([(s * 0.38, s * 0.28), (s * 0.46, s * 0.02), (s * 0.5, s * 0.2), (s * 0.56, s * 0.0), (s * 0.62, s * 0.28)], fill=WHITE)
    d.polygon([(s * 0.3, s * 0.34), (s * 0.34, s * 0.34), (s * 0.34, s * 0.56), (s * 0.24, s * 0.64)], fill=WHITE)
    d.polygon([(s * 0.7, s * 0.34), (s * 0.66, s * 0.34), (s * 0.66, s * 0.56), (s * 0.76, s * 0.64)], fill=WHITE)
    blast_rings(d, s, s * 0.5, s * 0.92, 1, 0.22)


def e_air_ambush(d, s):
    # A swooping flyer with long sharp wings, coming down.
    d.polygon([(s * 0.5, s * 0.86), (s * 0.4, s * 0.5), (s * 0.05, s * 0.2), (s * 0.34, s * 0.3), (s * 0.44, s * 0.16), (s * 0.56, s * 0.16), (s * 0.66, s * 0.3), (s * 0.95, s * 0.2), (s * 0.6, s * 0.5)], fill=WHITE)
    d.polygon([(s * 0.46, s * 0.34), (s * 0.54, s * 0.34), (s * 0.52, s * 0.52), (s * 0.48, s * 0.52)], fill=CLEAR)


def e_pulse(d, s):
    # A jagged bolt through expanding circles.
    for r, w in ((0.42, 0.03), (0.3, 0.03)):
        d.ellipse((s * (0.5 - r), s * (0.5 - r), s * (0.5 + r), s * (0.5 + r)), outline=WHITE, width=int(s * w))
    d.polygon([(s * 0.58, s * 0.02), (s * 0.3, s * 0.54), (s * 0.48, s * 0.54), (s * 0.4, s * 0.98), (s * 0.72, s * 0.42), (s * 0.54, s * 0.42), (s * 0.68, s * 0.02)], fill=CLEAR)
    d.polygon([(s * 0.58, s * 0.08), (s * 0.34, s * 0.52), (s * 0.5, s * 0.52), (s * 0.44, s * 0.9), (s * 0.68, s * 0.44), (s * 0.52, s * 0.44), (s * 0.64, s * 0.08)], fill=WHITE)


def e_smoke(d, s):
    # Billows spreading along a line.
    for cx, cy, r in ((0.2, 0.56, 0.13), (0.38, 0.42, 0.17), (0.6, 0.38, 0.19), (0.78, 0.52, 0.14), (0.5, 0.58, 0.16), (0.3, 0.64, 0.11), (0.68, 0.66, 0.11)):
        puff(d, s * cx, s * cy, s * r)
    d.rectangle((s * 0.08, s * 0.82, s * 0.92, s * 0.87), fill=WHITE)
    for cx, cy, r in ((0.46, 0.36, 0.08), (0.65, 0.38, 0.06)):
        d.arc((s * (cx - r), s * (cy - r), s * (cx + r), s * (cy + r)), 180, 360, fill=CLEAR, width=int(s * 0.025))


def e_dome(d, s):
    # A dome over the ground.
    for r, w in ((0.42, 0.05), (0.3, 0.035)):
        d.arc((s * (0.5 - r), s * (0.74 - r), s * (0.5 + r), s * (0.74 + r)), 180, 360, fill=WHITE, width=int(s * w))
    d.rectangle((s * 0.06, s * 0.76, s * 0.94, s * 0.82), fill=WHITE)
    for cx in (0.36, 0.5, 0.64):
        d.polygon([(s * cx, s * 0.56), (s * (cx + 0.05), s * 0.74), (s * (cx - 0.05), s * 0.74)], fill=WHITE)


def e_bubble(d, s):
    # A sphere with a highlight, and hexagons on it.
    d.ellipse((s * 0.1, s * 0.1, s * 0.9, s * 0.9), outline=WHITE, width=int(s * 0.05))
    d.arc((s * 0.2, s * 0.2, s * 0.8, s * 0.8), 200, 280, fill=WHITE, width=int(s * 0.05))
    for cx, cy in ((0.5, 0.42), (0.36, 0.62), (0.64, 0.62)):
        pts = [(s * (cx + 0.09 * math.cos(math.tau * i / 6)), s * (cy + 0.09 * math.sin(math.tau * i / 6))) for i in range(6)]
        d.polygon(pts, outline=WHITE, width=int(s * 0.022))


def e_rally(d, s):
    # A banner on a pole, with chevrons below.
    d.rectangle((s * 0.26, s * 0.06, s * 0.32, s * 0.94), fill=WHITE)
    d.polygon([(s * 0.32, s * 0.1), (s * 0.84, s * 0.1), (s * 0.7, s * 0.27), (s * 0.84, s * 0.44), (s * 0.32, s * 0.44)], fill=WHITE)
    for i in range(2):
        y = s * (0.56 + i * 0.17)
        d.line([(s * 0.42, y + s * 0.1), (s * 0.6, y), (s * 0.78, y + s * 0.1)], fill=WHITE, width=int(s * 0.055), joint="curve")


def e_blessing(d, s):
    # A radiant star over outstretched lines.
    d.polygon(star_points(s * 0.5, s * 0.42, s * 0.38, s * 0.1, 8), fill=WHITE)
    puff(d, s * 0.5, s * 0.42, s * 0.09, CLEAR)
    puff(d, s * 0.5, s * 0.42, s * 0.05)
    d.arc((s * 0.1, s * 0.54, s * 0.9, s * 1.14), 200, 340, fill=WHITE, width=int(s * 0.045))


def e_shell(d, s):
    # A thick lumpy wall curling round a small core.
    for cx, cy, r in ((0.5, 0.17, 0.16), (0.27, 0.27, 0.17), (0.74, 0.3, 0.15), (0.15, 0.52, 0.14), (0.86, 0.56, 0.13), (0.26, 0.78, 0.17), (0.7, 0.8, 0.16), (0.48, 0.88, 0.12)):
        puff(d, s * cx, s * cy, s * r)
    d.ellipse((s * 0.28, s * 0.3, s * 0.72, s * 0.74), fill=CLEAR)
    puff(d, s * 0.5, s * 0.52, s * 0.11)


def e_rain(d, s):
    # A spore cloud dripping.
    for cx, cy, r in ((0.26, 0.28, 0.14), (0.46, 0.2, 0.17), (0.68, 0.26, 0.15), (0.52, 0.34, 0.14)):
        puff(d, s * cx, s * cy, s * r)
    for cx, cy, r in ((0.22, 0.56, 0.05), (0.42, 0.64, 0.06), (0.62, 0.58, 0.05), (0.8, 0.66, 0.045), (0.3, 0.84, 0.05), (0.56, 0.88, 0.055)):
        d.polygon([(s * cx, s * (cy - r * 2.6)), (s * (cx - r), s * cy), (s * (cx + r), s * cy)], fill=WHITE)
        puff(d, s * cx, s * cy, s * r)


def e_pods_flood(d, s):
    # Two bulbous pods with stalks, opening.
    for cx, cy, r in ((0.32, 0.36, 0.2), (0.7, 0.54, 0.17)):
        puff(d, s * cx, s * cy, s * r)
        puff(d, s * (cx + 0.05), s * (cy - 0.04), s * r * 0.42, CLEAR)
        puff(d, s * (cx + 0.05), s * (cy - 0.04), s * r * 0.18)
    thick_line(d, [(s * 0.32, s * 0.56), (s * 0.36, s * 0.76), (s * 0.5, s * 0.92)], s * 0.05)
    thick_line(d, [(s * 0.7, s * 0.7), (s * 0.66, s * 0.82), (s * 0.5, s * 0.92)], s * 0.05)


def e_wrath(d, s):
    # A great maw of teeth, seen from the front.
    d.ellipse((s * 0.06, s * 0.14, s * 0.94, s * 0.86), fill=WHITE)
    d.ellipse((s * 0.2, s * 0.3, s * 0.8, s * 0.7), fill=CLEAR)
    for i in range(9):
        a = math.tau * i / 9.0
        cx, cy = 0.5 + 0.3 * math.cos(a), 0.5 + 0.2 * math.sin(a)
        tip_x, tip_y = 0.5 + 0.12 * math.cos(a), 0.5 + 0.08 * math.sin(a)
        side = 0.055
        d.polygon([(s * (cx - side * math.sin(a)), s * (cy + side * math.cos(a))), (s * (cx + side * math.sin(a)), s * (cy - side * math.cos(a))), (s * tip_x, s * tip_y)], fill=WHITE)


def e_frenzy(d, s):
    # Three slashes.
    for x0, y0, x1, y1 in ((0.72, 0.04, 0.2, 0.86), (0.9, 0.14, 0.42, 0.94), (0.5, 0.06, 0.08, 0.68)):
        nx, ny = (y1 - y0) * 0.075, -(x1 - x0) * 0.075
        d.polygon([(s * x0, s * y0), (s * ((x0 + x1) / 2 + nx), s * ((y0 + y1) / 2 + ny)), (s * x1, s * y1), (s * ((x0 + x1) / 2 - nx), s * ((y0 + y1) / 2 - ny))], fill=WHITE)


def e_spore_burst(d, s):
    # A pod bursting: fragments flying out around a core.
    puff(d, s * 0.5, s * 0.5, s * 0.17)
    for i in range(10):
        a = math.tau * i / 10.0
        r0, r1 = 0.24, 0.42 if i % 2 == 0 else 0.34
        d.line([(s * (0.5 + r0 * math.cos(a)), s * (0.5 + r0 * math.sin(a))), (s * (0.5 + r1 * math.cos(a)), s * (0.5 + r1 * math.sin(a)))], fill=WHITE, width=int(s * 0.04))
        puff(d, s * (0.5 + (r1 + 0.04) * math.cos(a)), s * (0.5 + (r1 + 0.04) * math.sin(a)), s * 0.035)


def e_nerve(d, s):
    # A spore with a jolt through it.
    for i in range(12):
        a = math.tau * i / 12.0
        d.line([(s * (0.5 + 0.3 * math.cos(a)), s * (0.5 + 0.3 * math.sin(a))), (s * (0.5 + 0.44 * math.cos(a)), s * (0.5 + 0.44 * math.sin(a)))], fill=WHITE, width=int(s * 0.03))
    puff(d, s * 0.5, s * 0.5, s * 0.32)
    d.polygon([(s * 0.56, s * 0.2), (s * 0.34, s * 0.54), (s * 0.48, s * 0.54), (s * 0.42, s * 0.82), (s * 0.68, s * 0.44), (s * 0.54, s * 0.44), (s * 0.64, s * 0.2)], fill=CLEAR)


def e_spell_generic(d, s):
    d.polygon(star_points(s * 0.5, s * 0.5, s * 0.46, s * 0.14, 6), fill=WHITE)
    puff(d, s * 0.5, s * 0.5, s * 0.09, CLEAR)


def e_buff_generic(d, s):
    # Three chevrons rising.
    for i in range(3):
        y = s * (0.2 + i * 0.22)
        d.line([(s * 0.16, y + s * 0.22), (s * 0.5, y), (s * 0.84, y + s * 0.22)], fill=WHITE, width=int(s * 0.1), joint="curve")


def e_economy_generic(d, s):
    d.ellipse((s * 0.1, s * 0.1, s * 0.9, s * 0.9), fill=WHITE)
    d.ellipse((s * 0.2, s * 0.2, s * 0.8, s * 0.8), fill=CLEAR)
    d.rectangle((s * 0.34, s * 0.36, s * 0.66, s * 0.44), fill=WHITE)
    d.rectangle((s * 0.34, s * 0.56, s * 0.66, s * 0.64), fill=WHITE)


def e_item_generic(d, s):
    pts = [(s * (0.5 + 0.44 * math.cos(math.tau * i / 6)), s * (0.5 + 0.44 * math.sin(math.tau * i / 6))) for i in range(6)]
    d.polygon(pts, fill=WHITE)
    puff(d, s * 0.5, s * 0.5, s * 0.2, CLEAR)


def e_training_generic(d, s):
    for i in range(3):
        y = s * (0.56 - i * 0.22)
        d.line([(s * 0.14, y), (s * 0.5, y + s * 0.2), (s * 0.86, y)], fill=WHITE, width=int(s * 0.09), joint="curve")


# Faction emblems: plain abstract marks made for this game, on purpose nothing like any existing insignia.

def e_faction_unsc(d, s):
    # A square stencil of two chevrons and a bar.
    d.polygon([(s * 0.5, s * 0.08), (s * 0.92, s * 0.38), (s * 0.92, s * 0.56), (s * 0.5, s * 0.26), (s * 0.08, s * 0.56), (s * 0.08, s * 0.38)], fill=WHITE)
    d.polygon([(s * 0.5, s * 0.36), (s * 0.8, s * 0.58), (s * 0.8, s * 0.74), (s * 0.5, s * 0.52), (s * 0.2, s * 0.74), (s * 0.2, s * 0.58)], fill=WHITE)
    d.rectangle((s * 0.2, s * 0.82, s * 0.8, s * 0.92), fill=WHITE)


def e_faction_covenant(d, s):
    # Two crescents sweeping around a point of light.
    d.ellipse((s * 0.06, s * 0.08, s * 0.82, s * 0.84), fill=WHITE)
    d.ellipse((s * 0.18, s * 0.02, s * 0.9, s * 0.74), fill=CLEAR)
    d.ellipse((s * 0.22, s * 0.2, s * 0.96, s * 0.94), fill=WHITE)
    d.ellipse((s * 0.12, s * 0.28, s * 0.84, s * 1.0), fill=CLEAR)
    d.polygon(star_points(s * 0.5, s * 0.48, s * 0.17, s * 0.05, 4), fill=WHITE)


def e_faction_flood(d, s):
    # A misshapen spore with a dark heart, and tendrils reaching out from it unevenly.
    for a, length, width in ((0.3, 0.47, 0.05), (1.25, 0.38, 0.04), (2.1, 0.46, 0.055), (3.0, 0.36, 0.04), (3.85, 0.48, 0.05), (4.7, 0.34, 0.04), (5.5, 0.44, 0.05)):
        bend = a + 0.45
        thick_line(d, [(s * 0.5, s * 0.5), (s * (0.5 + length * 0.6 * math.cos(a)), s * (0.5 + length * 0.6 * math.sin(a))), (s * (0.5 + length * math.cos(bend)), s * (0.5 + length * math.sin(bend)))], s * width)
    for cx, cy, r in ((0.5, 0.5, 0.25), (0.34, 0.42, 0.14), (0.64, 0.4, 0.13), (0.62, 0.64, 0.14), (0.38, 0.64, 0.12)):
        puff(d, s * cx, s * cy, s * r)
    puff(d, s * 0.5, s * 0.51, s * 0.11, CLEAR)
    puff(d, s * 0.5, s * 0.51, s * 0.045)


EMBLEMS = {
    # Spells, by name (spaces and other signs dropped). A spell with none falls back on its effect's emblem.
    "Spell_MACStrike": e_mac,
    "Spell_HAVOKNuke": e_nuke,
    "Spell_ArcherMissileBarrage": e_missiles,
    "Spell_ODSTDropPods": e_drop_pod,
    "Spell_SmokeBomb": e_smoke,
    "Spell_SporeCloud": e_smoke,
    "Spell_ElectromagneticPulse": e_pulse,
    "Spell_NerveSpores": e_nerve,
    "Spell_BubbleShield": e_bubble,
    "Spell_BattlefieldRally": e_rally,
    "Spell_PlasmaBombardment": e_plasma_shot,
    "Spell_PlasmaStorm": e_plasma_storm,
    "Spell_GlassingBeam": e_beam,
    "Spell_BansheeAmbush": e_air_ambush,
    "Spell_EnergyShieldDome": e_dome,
    "Spell_ProphetsBlessing": e_blessing,
    "Spell_BiomassShell": e_shell,
    "Spell_BlightRain": e_rain,
    "Spell_DispersalPods": e_pods_flood,
    "Spell_GravemindsWrath": e_wrath,
    "Spell_HiveFrenzy": e_frenzy,
    "Spell_SporeBurst": e_spore_burst,
    # Spell effects.
    "Effect_Damage": e_strike,
    "Effect_Buff": e_dome,
    "Effect_DropSquad": e_drop_pod,
    "Effect_Disrupt": e_pulse,
    "Effect_Smoke": e_smoke,
    # Kinds of card.
    "Kind_Spell": e_spell_generic,
    "Kind_Buff": e_buff_generic,
    "Kind_Economy": e_economy_generic,
    "Kind_Item": e_item_generic,
    "Kind_Training": e_training_generic,
    # Factions.
    "Faction_UNSC": e_faction_unsc,
    "Faction_Covenant": e_faction_covenant,
    "Faction_Flood": e_faction_flood,
}


def save_emblem(name, draw_function, size=256, big=None):
    big = big or size * 4
    image = Image.new("RGBA", (big, big), CLEAR)
    draw_function(ImageDraw.Draw(image), big)
    image = image.resize((size, size), Image.LANCZOS)
    path = os.path.join(OUT, "Emblems", name + ".png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save(path)
    print("wrote", os.path.relpath(path, ROOT))


def make_emblems():
    for name, draw in EMBLEMS.items():
        save_emblem(name, draw)
    # The existing item and card icons, drawn again at a size that suits a big card (Art/UI/Items keeps its small ones).
    sys.path.insert(0, os.path.join(ROOT, "Tools", "UiArt"))
    import make_ui_textures
    for name, draw in make_ui_textures.ICONS.items():
        save_emblem(name, draw, big=384)  # Those functions draw for a canvas of exactly this size.


# ---------------------------------------------------------------------------------------- the role emblems and body shapes

def r_chaff(d, s):
    # Many small dots.
    for cx, cy in ((0.3, 0.3), (0.5, 0.26), (0.7, 0.3), (0.22, 0.5), (0.42, 0.48), (0.62, 0.48), (0.8, 0.5), (0.32, 0.7), (0.52, 0.7), (0.72, 0.7)):
        puff(d, s * cx, s * cy, s * 0.075)


def r_chaff_clear(d, s):
    # A blast fan.
    d.polygon([(s * 0.12, s * 0.5), (s * 0.84, s * 0.14), (s * 0.92, s * 0.5), (s * 0.84, s * 0.86)], fill=WHITE)
    d.polygon([(s * 0.3, s * 0.5), (s * 0.74, s * 0.3), (s * 0.8, s * 0.5), (s * 0.74, s * 0.7)], fill=CLEAR)


def r_single_target(d, s):
    # A crosshair.
    d.ellipse((s * 0.16, s * 0.16, s * 0.84, s * 0.84), outline=WHITE, width=int(s * 0.08))
    for box_ in ((0.46, 0.04, 0.54, 0.32), (0.46, 0.68, 0.54, 0.96), (0.04, 0.46, 0.32, 0.54), (0.68, 0.46, 0.96, 0.54)):
        d.rectangle(tuple(v * s for v in box_), fill=WHITE)
    puff(d, s * 0.5, s * 0.5, s * 0.07)


def r_tank(d, s):
    # A shield.
    d.polygon([(s * 0.5, s * 0.06), (s * 0.88, s * 0.2), (s * 0.84, s * 0.56), (s * 0.5, s * 0.94), (s * 0.16, s * 0.56), (s * 0.12, s * 0.2)], fill=WHITE)
    d.polygon([(s * 0.5, s * 0.2), (s * 0.5, s * 0.78), (s * 0.27, s * 0.52), (s * 0.25, s * 0.29)], fill=CLEAR)


def r_flanker(d, s):
    # A curved arrow going round.
    d.arc((s * 0.14, s * 0.2, s * 0.86, s * 1.2), 180, 330, fill=WHITE, width=int(s * 0.12))
    d.polygon([(s * 0.88, s * 0.26), (s * 0.94, s * 0.64), (s * 0.6, s * 0.46)], fill=WHITE)


def r_artillery(d, s):
    # An arc ending in a burst.
    d.arc((s * 0.06, s * 0.2, s * 0.72, s * 1.2), 180, 335, fill=WHITE, width=int(s * 0.07))
    d.polygon(star_points(s * 0.76, s * 0.7, s * 0.22, s * 0.08, 7), fill=WHITE)


def r_anti_air(d, s):
    # Two chevrons pointing up at a dot.
    puff(d, s * 0.5, s * 0.14, s * 0.09)
    for i in range(2):
        y = s * (0.34 + i * 0.26)
        d.line([(s * 0.14, y + s * 0.26), (s * 0.5, y), (s * 0.86, y + s * 0.26)], fill=WHITE, width=int(s * 0.1), joint="curve")


def r_aerial(d, s):
    # A wing.
    d.polygon([(s * 0.04, s * 0.42), (s * 0.5, s * 0.22), (s * 0.96, s * 0.42), (s * 0.58, s * 0.5), (s * 0.54, s * 0.84), (s * 0.5, s * 0.92), (s * 0.46, s * 0.84), (s * 0.42, s * 0.5)], fill=WHITE)


def r_titan(d, s):
    # A heavy crown of three points.
    d.polygon([(s * 0.08, s * 0.82), (s * 0.08, s * 0.26), (s * 0.3, s * 0.5), (s * 0.5, s * 0.1), (s * 0.7, s * 0.5), (s * 0.92, s * 0.26), (s * 0.92, s * 0.82)], fill=WHITE)


ROLES = {
    "Chaff": r_chaff, "ChaffClear": r_chaff_clear, "SingleTargetDps": r_single_target, "Tank": r_tank, "Flanker": r_flanker,
    "Artillery": r_artillery, "AntiAir": r_anti_air, "Aerial": r_aerial, "Titan": r_titan,
}


K = (0, 0, 0, 255)


def humanoid(d, s, width=1.0, height=1.0, cx=0.5, ground=0.96, gun=True, bulk=1.0):
    """A soldier in a firm stance, rifle levelled to the right: rounded limbs on an armoured torso. Width and height scale it."""
    def p(x, y):
        return (s * (cx + (x - 0.5) * width), s * (ground - (0.96 - y) * height))

    def limb(points, w):
        thick_line(d, [p(x, y) for x, y in points], s * w * bulk * width, K)

    # Legs and boots, the back one first.
    limb([(0.45, 0.58), (0.41, 0.76), (0.36, 0.93)], 0.085)
    limb([(0.54, 0.58), (0.61, 0.75), (0.64, 0.93)], 0.09)
    d.polygon([p(0.3, 0.9), p(0.42, 0.9), p(0.43, 0.96), p(0.28, 0.96)], fill=K)
    d.polygon([p(0.59, 0.9), p(0.7, 0.9), p(0.76, 0.96), p(0.58, 0.96)], fill=K)
    # Torso: wide shoulders, narrow waist, a pack behind.
    d.polygon([p(0.34, 0.27), p(0.62, 0.26), p(0.67, 0.33), p(0.62, 0.5), p(0.6, 0.62), p(0.38, 0.62), p(0.37, 0.5), p(0.31, 0.34)], fill=K)
    d.polygon([p(0.27, 0.3), p(0.34, 0.28), p(0.36, 0.5), p(0.29, 0.5)], fill=K)
    # Head: a helmet with a brim.
    hx, hy = p(0.5, 0.17)
    rx, ry = s * 0.082 * width, s * 0.086 * height
    d.ellipse((hx - rx, hy - ry, hx + rx, hy + ry), fill=K)
    d.polygon([p(0.42, 0.18), p(0.62, 0.18), p(0.6, 0.23), p(0.44, 0.25)], fill=K)
    if gun:
        # The rifle, and the arms that hold it.
        d.polygon([p(0.36, 0.385), p(0.5, 0.37), p(0.98, 0.335), p(0.98, 0.375), p(0.74, 0.4), p(0.72, 0.5), p(0.66, 0.5), p(0.66, 0.415), p(0.5, 0.425), p(0.42, 0.47), p(0.34, 0.44)], fill=K)
        limb([(0.37, 0.31), (0.42, 0.46), (0.6, 0.41)], 0.065)
        limb([(0.6, 0.31), (0.7, 0.4), (0.8, 0.37)], 0.06)
    else:
        limb([(0.37, 0.31), (0.33, 0.46), (0.38, 0.58)], 0.065)
        limb([(0.6, 0.31), (0.7, 0.4), (0.78, 0.38)], 0.06)


def b_trooper(d, s):
    humanoid(d, s)


def b_heavy_trooper(d, s):
    # Taller and broader, with big shoulder plates and a crest.
    humanoid(d, s, width=1.04, height=1.0, bulk=1.2)
    for x0, x1 in ((0.24, 0.42), (0.58, 0.74)):
        d.polygon([(s * x0, s * 0.31), (s * (x0 + 0.03), s * 0.23), (s * (x1 - 0.03), s * 0.21), (s * x1, s * 0.31), (s * (x1 - 0.02), s * 0.38), (s * (x0 + 0.02), s * 0.38)], fill=K)
    d.polygon([(s * 0.43, s * 0.12), (s * 0.5, s * 0.01), (s * 0.57, s * 0.12)], fill=K)


def b_shield_trooper(d, s):
    # A slim figure holding a tall curved shield in front of it.
    humanoid(d, s, width=0.86, height=0.92, cx=0.38, gun=False)
    d.polygon([(s * 0.52, s * 0.16), (s * 0.72, s * 0.06), (s * 0.92, s * 0.12), (s * 0.98, s * 0.48), (s * 0.9, s * 0.86), (s * 0.72, s * 0.94), (s * 0.54, s * 0.82), (s * 0.6, s * 0.5)], fill=K)
    d.polygon([(s * 0.66, s * 0.3), (s * 0.84, s * 0.26), (s * 0.84, s * 0.34), (s * 0.66, s * 0.38)], fill=CLEAR)


def b_hulk(d, s):
    # A huge hunched armoured body, spines on its back, one enormous arm.
    d.polygon([(s * 0.2, s * 0.96), (s * 0.24, s * 0.64), (s * 0.12, s * 0.48), (s * 0.2, s * 0.22), (s * 0.5, s * 0.1), (s * 0.74, s * 0.16), (s * 0.84, s * 0.4), (s * 0.76, s * 0.66), (s * 0.82, s * 0.96), (s * 0.6, s * 0.96), (s * 0.54, s * 0.72), (s * 0.44, s * 0.74), (s * 0.42, s * 0.96)], fill=K)
    d.polygon([(s * 0.66, s * 0.3), (s * 0.98, s * 0.46), (s * 0.99, s * 0.76), (s * 0.8, s * 0.8), (s * 0.68, s * 0.58)], fill=K)
    for x0, y0, x1, y1 in ((0.26, 0.22, 0.14, 0.03), (0.38, 0.17, 0.32, 0.0), (0.52, 0.13, 0.54, 0.0)):
        d.polygon([(s * (x0 - 0.06), s * y0), (s * (x0 + 0.06), s * y0), (s * x1, s * y1)], fill=K)


def b_speeder(d, s):
    # A low hover bike, nose to the right: a swept body, a raised seat, two forward prongs.
    d.polygon([(s * 0.02, s * 0.46), (s * 0.16, s * 0.42), (s * 0.3, s * 0.32), (s * 0.5, s * 0.34), (s * 0.68, s * 0.46), (s * 0.99, s * 0.5), (s * 0.99, s * 0.54), (s * 0.72, s * 0.6), (s * 0.97, s * 0.66), (s * 0.97, s * 0.7), (s * 0.62, s * 0.72), (s * 0.3, s * 0.72), (s * 0.08, s * 0.62)], fill=K)
    d.polygon([(s * 0.3, s * 0.34), (s * 0.34, s * 0.22), (s * 0.46, s * 0.24), (s * 0.5, s * 0.36)], fill=K)
    d.polygon([(s * 0.12, s * 0.66), (s * 0.3, s * 0.72), (s * 0.5, s * 0.72), (s * 0.44, s * 0.86), (s * 0.2, s * 0.84)], fill=K)


def b_tank(d, s):
    # Tracks with road wheels, a sloped hull, a turret and a long gun.
    d.rounded_rectangle((s * 0.04, s * 0.64, s * 0.88, s * 0.88), radius=int(s * 0.11), fill=K)
    for cx in (0.16, 0.31, 0.46, 0.61, 0.76):
        puff(d, s * cx, s * 0.76, s * 0.045, CLEAR)
        puff(d, s * cx, s * 0.76, s * 0.022, K)
    d.polygon([(s * 0.06, s * 0.64), (s * 0.13, s * 0.5), (s * 0.76, s * 0.5), (s * 0.92, s * 0.64)], fill=K)
    d.polygon([(s * 0.22, s * 0.5), (s * 0.28, s * 0.33), (s * 0.58, s * 0.33), (s * 0.66, s * 0.5)], fill=K)
    d.polygon([(s * 0.58, s * 0.38), (s * 0.99, s * 0.34), (s * 0.99, s * 0.4), (s * 0.6, s * 0.45)], fill=K)
    d.rectangle((s * 0.36, s * 0.26, s * 0.46, s * 0.34), fill=K)


def b_walker(d, s):
    # A heavy biped machine: bent legs with big feet, a blocky cabin, a gun on one arm and a missile rack on the other.
    thick_line(d, [(s * 0.38, s * 0.5), (s * 0.24, s * 0.68), (s * 0.3, s * 0.9)], s * 0.1, K)
    thick_line(d, [(s * 0.62, s * 0.5), (s * 0.76, s * 0.68), (s * 0.7, s * 0.9)], s * 0.1, K)
    d.polygon([(s * 0.14, s * 0.9), (s * 0.42, s * 0.9), (s * 0.44, s * 0.97), (s * 0.1, s * 0.97)], fill=K)
    d.polygon([(s * 0.58, s * 0.9), (s * 0.86, s * 0.9), (s * 0.9, s * 0.97), (s * 0.56, s * 0.97)], fill=K)
    d.polygon([(s * 0.3, s * 0.22), (s * 0.7, s * 0.22), (s * 0.77, s * 0.36), (s * 0.66, s * 0.54), (s * 0.34, s * 0.54), (s * 0.23, s * 0.36)], fill=K)
    d.polygon([(s * 0.4, s * 0.1), (s * 0.6, s * 0.1), (s * 0.63, s * 0.24), (s * 0.37, s * 0.24)], fill=K)
    d.polygon([(s * 0.74, s * 0.27), (s * 0.99, s * 0.27), (s * 0.99, s * 0.33), (s * 0.74, s * 0.37)], fill=K)
    d.rectangle((s * 0.03, s * 0.2, s * 0.26, s * 0.38), fill=K)
    for y in (0.25, 0.33):
        for x in (0.08, 0.15, 0.22):
            puff(d, s * x, s * y, s * 0.02, CLEAR)


def b_flyer(d, s):
    # Long swept wings with engines, seen from ahead and above.
    d.polygon([(s * 0.5, s * 0.18), (s * 0.6, s * 0.4), (s * 0.99, s * 0.62), (s * 0.97, s * 0.68), (s * 0.66, s * 0.58), (s * 0.58, s * 0.8), (s * 0.5, s * 0.86), (s * 0.42, s * 0.8), (s * 0.34, s * 0.58), (s * 0.03, s * 0.68), (s * 0.01, s * 0.62), (s * 0.4, s * 0.4)], fill=K)
    d.polygon([(s * 0.45, s * 0.3), (s * 0.55, s * 0.3), (s * 0.53, s * 0.1), (s * 0.47, s * 0.1)], fill=K)
    for cx in (0.24, 0.76):
        d.rounded_rectangle((s * (cx - 0.04), s * 0.46, s * (cx + 0.04), s * 0.7), radius=int(s * 0.03), fill=K)


def b_pod(d, s):
    # A box of missile tubes tilted up on a mount.
    d.polygon([(s * 0.06, s * 0.5), (s * 0.68, s * 0.1), (s * 0.97, s * 0.42), (s * 0.34, s * 0.8)], fill=K)
    for i in range(3):
        for j in range(2):
            cx, cy = 0.28 + i * 0.17 + j * 0.1, 0.46 - i * 0.11 + j * 0.11
            puff(d, s * cx, s * cy, s * 0.05, CLEAR)
    d.polygon([(s * 0.34, s * 0.66), (s * 0.58, s * 0.6), (s * 0.74, s * 0.97), (s * 0.18, s * 0.97)], fill=K)


def b_flood_form(d, s):
    # A hunched, broken body: a lump of a head low on its chest, one arm become a mass of whips.
    thick_line(d, [(s * 0.44, s * 0.58), (s * 0.34, s * 0.76), (s * 0.3, s * 0.95)], s * 0.075, K)
    thick_line(d, [(s * 0.52, s * 0.58), (s * 0.62, s * 0.76), (s * 0.58, s * 0.95)], s * 0.075, K)
    d.polygon([(s * 0.36, s * 0.62), (s * 0.28, s * 0.42), (s * 0.34, s * 0.2), (s * 0.56, s * 0.14), (s * 0.68, s * 0.3), (s * 0.62, s * 0.62)], fill=K)
    puff(d, s * 0.34, s * 0.3, s * 0.09, K)
    puff(d, s * 0.6, s * 0.22, s * 0.075, K)
    thick_line(d, [(s * 0.34, s * 0.32), (s * 0.16, s * 0.44), (s * 0.12, s * 0.66)], s * 0.055, K)
    d.polygon([(s * 0.08, s * 0.64), (s * 0.18, s * 0.64), (s * 0.2, s * 0.82), (s * 0.13, s * 0.74), (s * 0.04, s * 0.82)], fill=K)
    thick_line(d, [(s * 0.62, s * 0.28), (s * 0.8, s * 0.32), (s * 0.95, s * 0.5), (s * 0.92, s * 0.72)], s * 0.05, K)
    thick_line(d, [(s * 0.72, s * 0.3), (s * 0.86, s * 0.18), (s * 0.98, s * 0.2)], s * 0.03, K)
    thick_line(d, [(s * 0.76, s * 0.36), (s * 0.8, s * 0.58), (s * 0.74, s * 0.74)], s * 0.03, K)
    thick_line(d, [(s * 0.48, s * 0.16), (s * 0.42, s * 0.05)], s * 0.03, K)
    thick_line(d, [(s * 0.56, s * 0.16), (s * 0.66, s * 0.04)], s * 0.025, K)


def b_flood_beast(d, s):
    # A great mass of flesh on thick legs, tentacles rising from it.
    for cx, cy, r in ((0.5, 0.54, 0.3), (0.28, 0.62, 0.21), (0.73, 0.62, 0.2), (0.42, 0.34, 0.17), (0.62, 0.36, 0.15)):
        puff(d, s * cx, s * cy, s * r, K)
    thick_line(d, [(s * 0.3, s * 0.74), (s * 0.24, s * 0.95)], s * 0.13, K)
    thick_line(d, [(s * 0.72, s * 0.74), (s * 0.78, s * 0.95)], s * 0.13, K)
    thick_line(d, [(s * 0.2, s * 0.5), (s * 0.07, s * 0.36), (s * 0.12, s * 0.14)], s * 0.06, K)
    thick_line(d, [(s * 0.82, s * 0.5), (s * 0.94, s * 0.32), (s * 0.84, s * 0.12)], s * 0.06, K)
    thick_line(d, [(s * 0.5, s * 0.22), (s * 0.54, s * 0.03)], s * 0.045, K)
    thick_line(d, [(s * 0.36, s * 0.22), (s * 0.3, s * 0.06)], s * 0.035, K)
    thick_line(d, [(s * 0.66, s * 0.26), (s * 0.72, s * 0.08)], s * 0.035, K)


def b_flood_swarm(d, s):
    # A cluster of small crawling bulbs.
    for cx, cy, r in ((0.3, 0.42, 0.12), (0.62, 0.34, 0.14), (0.46, 0.66, 0.13), (0.76, 0.62, 0.11), (0.2, 0.72, 0.09)):
        puff(d, s * cx, s * cy, s * r, K)
        for i in range(5):
            a = math.pi * (0.1 + 0.2 * i)
            thick_line(d, [(s * cx, s * cy), (s * (cx + r * 1.6 * math.cos(a)), s * (cy + r * 1.5 * math.sin(a)))], s * 0.018, K)


BODIES = {
    "Trooper": b_trooper, "HeavyTrooper": b_heavy_trooper, "ShieldTrooper": b_shield_trooper, "Hulk": b_hulk, "Speeder": b_speeder,
    "Tank": b_tank, "Walker": b_walker, "Flyer": b_flyer, "Pod": b_pod, "FloodForm": b_flood_form, "FloodBeast": b_flood_beast, "FloodSwarm": b_flood_swarm,
}


def make_layers():
    folder = os.path.join(OUT, "Layers")
    os.makedirs(folder, exist_ok=True)
    for name, draw in ROLES.items():
        big = 128 * 4
        image = Image.new("RGBA", (big, big), CLEAR)
        draw(ImageDraw.Draw(image), big)
        path = os.path.join(folder, "Role_" + name + ".png")
        image.resize((128, 128), Image.LANCZOS).save(path)
        print("wrote", os.path.relpath(path, ROOT))
    # Body shapes: the alpha is the shape; the grey is an edge of light on its top and left (UnitIcons colours it by faction).
    for name, draw in BODIES.items():
        size, big = 256, 1024
        image = Image.new("RGBA", (big, big), (0, 0, 0, 0))
        draw(ImageDraw.Draw(image), big)
        small = image.resize((size, size), Image.LANCZOS)
        alpha = np.asarray(small.getchannel("A"), dtype=np.float32) / 255.0
        moved = Image.fromarray((alpha * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(2.5))
        moved = np.roll(np.roll(np.asarray(moved, dtype=np.float32) / 255.0, 3, axis=0), 3, axis=1)
        rim = np.clip(alpha - moved, 0.0, 1.0)
        height = np.linspace(0.2, 0.03, size, dtype=np.float32)[:, None]  # A faint lift towards the top, so the body isn't a black hole.
        rim = np.clip(rim * 1.5 + height, 0.0, 1.0)
        out = np.dstack([rim, rim, rim, alpha])
        path = os.path.join(folder, "Body_" + name + ".png")
        Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA").save(path)
        print("wrote", os.path.relpath(path, ROOT))


# ---------------------------------------------------------------------------------------- a contact sheet, to look at the art outside Unity

def nine_slice(texture, width, height):
    """What CardArt.DrawFrame does: fixed corners, edges that repeat their middle. Sizes in texture pixels."""
    t = np.asarray(texture.convert("RGBA"), dtype=np.float32)
    n = t.shape[0]
    b, p = B, n - 2 * B
    cols = [t[:, :b]]
    middle_w = width - 2 * b
    count = max(1, round(middle_w / p))
    cols += [np.asarray(Image.fromarray(t[:, b:b + p].astype(np.uint8), "RGBA").resize((max(1, round(middle_w / count)), n), Image.LANCZOS), dtype=np.float32)] * count
    cols.append(t[:, n - b:])
    wide = np.concatenate(cols, axis=1)
    rows = [wide[:b]]
    middle_h = height - 2 * b
    count = max(1, round(middle_h / p))
    rows += [np.asarray(Image.fromarray(wide[b:b + p].astype(np.uint8), "RGBA").resize((wide.shape[1], max(1, round(middle_h / count))), Image.LANCZOS), dtype=np.float32)] * count
    rows.append(wide[n - b:])
    return Image.fromarray(np.concatenate(rows, axis=0).astype(np.uint8), "RGBA")


def contact_sheet(path):
    names = list(FRAMES)
    w, h = 640, 960
    sheet = Image.new("RGBA", (5 * (w // 2 + 16) + 16, 2 * (h // 2 + 80) + 16), (28, 30, 34, 255))
    for i, name in enumerate(names):
        frame = Image.open(os.path.join(OUT, "Frames", "Frame_" + name + ".png"))
        card = nine_slice(frame, w, h).resize((w // 2, h // 2), Image.LANCZOS)
        x, y = 16 + (i % 5) * (w // 2 + 16), 16 + (i // 5) * (h // 2 + 80)
        back = Image.new("RGBA", card.size, (0, 0, 0, 0))
        fill = ImageDraw.Draw(back)
        fill.rectangle((22, 22, card.size[0] - 22, card.size[1] - 22), fill=(16, 18, 22, 255))
        sheet.alpha_composite(back, (x, y))
        sheet.alpha_composite(card, (x, y))
        ribbon_image = Image.open(os.path.join(OUT, "Frames", "Ribbon_" + name + ".png")).resize((192, 54), Image.LANCZOS)
        sheet.alpha_composite(ribbon_image, (x + (w // 2 - 192) // 2, y + h // 2 + 6))
    sheet.convert("RGB").save(path)
    print("wrote", path)


def main():
    only = None
    args = sys.argv[1:]
    if "--sheet" in args:
        contact_sheet(args[args.index("--sheet") + 1])
        return
    if "--frames" in args:
        only = "frames"
    for name, draw in FRAMES.items():
        draw(os.path.join(OUT, "Frames", "Frame_" + name + ".png"))
    for name, (top, bottom, edge, shape) in RIBBONS.items():
        ribbon(os.path.join(OUT, "Frames", "Ribbon_" + name + ".png"), top, bottom, edge, shape)
    if only == "frames":
        return
    make_common()
    make_emblems()
    make_layers()


if __name__ == "__main__":
    main()
