#!/usr/bin/env python
"""make_interior_flood.py

Makes the ground layers of the Flood-infested ship (D80): the deck of the hull the Flood has taken, in two kinds
(a UNSC ship's grimy gunmetal deck plates, and a Covenant ship's tarnished purple metal with a geometric inlay), the
living biomass spreading over it, and a darker spongy fungal rot. All original: made here from random numbers and
simple geometry, no pictures from anywhere, no markings, no text.

Every texture tiles seamlessly (all noise is wrapped, all plate lines fall on the tile's grid). The alpha of each layer
is its height: raised plates and lumps are high, seams and pits low, so one ground gives way to another around them.
Tones stay mid on purpose: dark Flood forms and pale Infection Forms both stand on these floors.

Usage (from the repo root):
    python Tools/TerrainGen/make_interior_flood.py [output folder] [--preview folder]
Output defaults to Assets/_Project/Resources/Terrain. Needs numpy and Pillow; reuses make_ground_textures.py.
A .meta is written next to each new picture (copied from GroundUnscDirt.png.meta with a fresh guid) only when none
exists yet, so running it again keeps Unity's references.
"""
import os
import sys
import uuid

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_ground_textures import (  # noqa: E402
    SIZE, band_noise, blend, blur, cells, dots, relief, ramp, ridged, save, smooth, strokes, wrap_sample)

YS, XS = np.meshgrid(np.arange(SIZE, dtype=np.float32) + 0.5, np.arange(SIZE, dtype=np.float32) + 0.5, indexing="ij")


# ---------------------------------------------------------------------------- helpers


def aniso_noise(seed, sx, sy):
    """Seamless white noise blurred more along one axis than the other: brushed metal, streaks. sx, sy in pixels."""
    rng = np.random.default_rng(seed)
    white = rng.normal(size=(SIZE, SIZE))
    fx = np.fft.fftfreq(SIZE)
    fy = np.fft.fftfreq(SIZE)
    kernel = np.exp(-2.0 * np.pi ** 2 * ((sx * fx[None, :]) ** 2 + (sy * fy[:, None]) ** 2))
    field = np.real(np.fft.ifft2(np.fft.fft2(white) * kernel))
    lo, hi = np.percentile(field, [1, 99])
    return np.clip((field - lo) / (hi - lo), 0, 1)


def wrapped(v, period):
    """Distance from v to the nearest multiple of period."""
    m = np.mod(v, period)
    return np.minimum(m, period - m)


def stamp(points, radius, size=SIZE, value=1.0):
    """A height map of discs at points (wrapped)."""
    image = Image.new("F", (size, size), 0.0)
    draw = ImageDraw.Draw(image)
    for x, y in points:
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                cx, cy = x + ox, y + oy
                if -radius < cx < size + radius and -radius < cy < size + radius:
                    draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=value)
    return np.asarray(image, dtype=np.float32).copy()


def lerp_color(a, b, t):
    return np.array(a, np.float32) * (1 - t[..., None]) + np.array(b, np.float32) * t[..., None]


def creep_in_seams(color, height, d, seed, reach, lumps_color=(132, 84, 76), runs_from=0.36, runs_px=8.0):
    """Biomass that has crept out of the deck's seams: long runs filling the lines, and here and there a blob spreading out
    over the plate, with a soft irregular front and a darker wet rim. d is each pixel's distance to the nearest seam.
    runs_from (0..1) is where along the noise the runs begin, higher for fewer; runs_px how far they spread out of a line."""
    runs = band_noise(seed, 3, 16)
    patches = band_noise(seed + 4, 2, 7)
    ragged = band_noise(seed + 5, 24, 110)
    bumps = band_noise(seed + 2, 20, 80)
    fine = band_noise(seed + 1, 40, 200, 0.6)
    extent = smooth(runs_from, runs_from + 0.3, runs) * runs_px + smooth(0.72, 0.97, patches) * reach
    front = extent - d + (ragged - 0.5) * 18.0 * smooth(2.0, 10.0, extent)
    mask = smooth(-1.0, 5.0, front) * smooth(0.5, 3.0, extent)
    lumps = blur(dots(seed + 6, 2200, 2.0, 7.0), 2.5)
    lumps = np.clip(lumps / (lumps.max() + 1e-6), 0, 1)
    mass = lerp_color(lumps_color, (184, 132, 114), np.clip(bumps * 0.6 + lumps * 0.5, 0, 1))
    mass *= (0.9 + 0.2 * fine)[..., None]
    rim = np.clip(mask * (1.0 - mask) * 4.0, 0, 1)
    color = color * (1 - mask[..., None]) + mass * mask[..., None]
    color = blend(color, (76, 46, 52), rim * 0.5)
    height = height * (1 - mask) + (0.4 + 0.25 * bumps + 0.25 * lumps) * mask
    return color, height, mask


# ---------------------------------------------------------------------------- the UNSC deck


def hull_unsc():
    """Gunmetal deck plates, 1024 px = 8 m: four 4 m blocks, each one plate, two, four or a plate with a vent strip."""
    rng = np.random.default_rng(301)
    kinds = [0, 1, 2, 3]
    rng.shuffle(kinds)
    rects = []  # x0, y0, x1, y1, kind (0 plain, 1 tread, 2 vent)
    for i, (by, bx) in enumerate([(0, 0), (0, 1), (1, 0), (1, 1)]):
        x0, y0 = bx * 512, by * 512
        k = kinds[i]
        if k == 0:
            rects.append((x0, y0, x0 + 512, y0 + 512, 0))
        elif k == 1:
            rects += [(x0, y0, x0 + 256, y0 + 512, 0), (x0 + 256, y0, x0 + 512, y0 + 512, 1)]
        elif k == 2:
            rects += [(x0, y0, x0 + 256, y0 + 256, 1), (x0 + 256, y0, x0 + 512, y0 + 256, 0),
                      (x0, y0 + 256, x0 + 256, y0 + 512, 0), (x0 + 256, y0 + 256, x0 + 512, y0 + 512, 0)]
        else:
            rects += [(x0, y0, x0 + 512, y0 + 368, 0), (x0, y0 + 368, x0 + 512, y0 + 512, 2)]

    d = np.full((SIZE, SIZE), 1e6, np.float32)
    tone = np.zeros((SIZE, SIZE), np.float32)
    kind = np.zeros((SIZE, SIZE), np.int32)
    brushed_h = aniso_noise(302, 60, 1.5)
    brushed_v = aniso_noise(303, 1.5, 60)
    brushed = np.zeros((SIZE, SIZE), np.float32)
    detail = np.zeros((SIZE, SIZE), np.float32)  # treads and vent slots, as height
    slot = np.zeros((SIZE, SIZE), np.float32)
    bolts = []
    for (x0, y0, x1, y1, k) in rects:
        sl = (slice(y0, y1), slice(x0, x1))
        lx = XS[sl] - x0
        ly = YS[sl] - y0
        w, h = x1 - x0, y1 - y0
        d[sl] = np.minimum(np.minimum(lx, w - lx), np.minimum(ly, h - ly))
        tone[sl] = rng.uniform(-1, 1)
        kind[sl] = k
        brushed[sl] = (brushed_h if w >= h else brushed_v)[sl]
        if k == 1:
            # Raised tread: short bars at alternating angles in a 32 px grid.
            cx = np.floor(lx / 32)
            cy = np.floor(ly / 32)
            fu = lx - cx * 32 - 16
            fv = ly - cy * 32 - 16
            flip = np.mod(cx + cy, 2) == 0
            across = np.where(flip, np.abs(fu - fv), np.abs(fu + fv)) / 1.414
            along = np.where(flip, np.abs(fu + fv), np.abs(fu - fv)) / 1.414
            bar = smooth(3.4, 1.6, across) * smooth(11, 8, along)
            inset = smooth(14, 22, np.minimum(np.minimum(lx, w - lx), np.minimum(ly, h - ly)))
            detail[sl] = bar * inset
        elif k == 2:
            # A vent: rows of slots in a recessed frame.
            fv = np.mod(ly - 20, 22)
            seg = np.mod(lx - 24, 118)
            inside = (lx > 24) & (lx < w - 24) & (ly > 20) & (ly < h - 20)
            s = smooth(2.0, 4.0, fv) * smooth(14.0, 12.0, fv) * smooth(0, 3, seg) * smooth(104, 101, seg)
            slot[sl] = s * inside
        for (bx, by) in ((x0 + 14, y0 + 14), (x1 - 14, y0 + 14), (x0 + 14, y1 - 14), (x1 - 14, y1 - 14)):
            bolts.append((bx, by))
        if max(w, h) >= 512:
            if w >= h:
                bolts += [(x0 + w / 2, y0 + 14), (x0 + w / 2, y1 - 14)]
            else:
                bolts += [(x0 + 14, y0 + h / 2), (x1 - 14, y0 + h / 2)]

    bevel = smooth(1.5, 9.0, d)
    bolt = blur(stamp(bolts, 4.2), 1.0)
    broad = band_noise(304, 2, 8)
    mid = band_noise(305, 8, 40)
    fine = band_noise(306, 60, 300, 0.6)

    height = 0.18 + 0.52 * bevel + 0.06 * mid + detail * 0.12 - slot * 0.45 + bolt * 0.12
    base = np.array((112, 117, 124), np.float32)
    color = np.broadcast_to(base, (SIZE, SIZE, 3)).copy()
    color *= (1 + tone * 0.06)[..., None]
    color *= (0.95 + 0.1 * brushed)[..., None]
    color *= (0.94 + 0.12 * fine)[..., None]
    # Worn tread tops are a little brighter.
    color *= (1 + detail * 0.12)[..., None]
    # The seam itself: a dark groove.
    groove = smooth(3.5, 1.0, d)
    color = blend(color, (52, 52, 52), groove * 0.8)
    color = blend(color, (46, 46, 46), slot * 0.75)
    # Grime: in broad patches, and gathered along the seams and around the bolts.
    seam_near = smooth(26, 2, d)
    grime = np.clip(smooth(0.45, 0.85, broad * 0.6 + mid * 0.4) * 0.5 + seam_near * 0.35 * mid, 0, 1)
    color = color * (1 - grime[..., None] * 0.4) + np.array((72, 68, 60), np.float32) * grime[..., None] * 0.4
    # Rust-brown and organic stains.
    stain = smooth(0.58, 0.86, band_noise(307, 3, 14)) * smooth(0.3, 0.8, mid)
    color = blend(color, (96, 82, 62), stain * 0.42)
    # Broad dirt from plate to plate and a darker band along every seam.
    color *= (0.84 + 0.2 * smooth(0.2, 0.8, band_noise(310, 1, 4)))[..., None]
    color *= (1 - 0.14 * smooth(30, 3, d))[..., None]
    oil = smooth(0.7, 0.9, aniso_noise(311, 4, 40)) * smooth(0.5, 0.8, band_noise(312, 2, 6))
    color = blend(color, (60, 58, 54), oil * 0.35)
    # Scratches.

    def scratch(r):
        v = int(r.uniform(140, 175))
        return (v, v, v + 4, int(r.uniform(40, 90)))

    lines = strokes(308, 900, 8, 60, 1, scratch)
    color = color * (1 - lines[..., 3:] * 0.5) + lines[..., :3] * 255.0 * lines[..., 3:] * 0.5
    # Biomass creeping along the seams.
    color, height, _ = creep_in_seams(color, height, d, 309, 34.0, runs_from=0.5, runs_px=7.0)
    color *= relief(height, 7.0)[..., None]
    return color, np.clip(height, 0, 1)


# ---------------------------------------------------------------------------- the Covenant deck


def hull_covenant():
    """Tarnished purple metal with a geometric inlay, 1024 px = 10 m: four 5 m panels, a chamfered octagon in each,
    rings around the points where four meet, and short ties between them."""
    cell = 512.0
    lx = np.mod(XS, cell) - cell / 2
    ly = np.mod(YS, cell) - cell / 2
    ax, ay = np.abs(lx), np.abs(ly)
    edge = np.minimum(cell / 2 - ax, cell / 2 - ay)  # distance to the panel's edge
    octagon = np.maximum(np.maximum(ax, ay) - 168.0, (ax + ay) / 1.4142 - 196.0)
    ring_d = np.hypot(wrapped(XS, cell), wrapped(YS, cell))
    ring = np.abs(ring_d - 92.0)
    ring2 = np.abs(ring_d - 112.0)
    diag = np.abs(ax - ay) / 1.4142  # the panel's diagonals
    tie = diag * ((ax + ay) / 1.4142 > 196.0) + 1e3 * ((ax + ay) / 1.4142 <= 196.0)
    tie = np.where(ring_d < 112.0, 1e3, tie)
    # The middle: a small diamond in two panels, a short bar in the other two.
    which = (np.floor(XS / cell) + np.floor(YS / cell)) % 2
    diamond = np.abs((ax + ay) - 52.0)
    bar = np.maximum(ax - 60.0, ay - 4.0)
    centre = np.where(which == 0, diamond / 1.4142, np.abs(bar))

    lines = np.minimum.reduce([np.abs(octagon), np.abs(octagon + 20.0) * 1.6, ring, ring2 * 1.6, tie, centre])
    groove = smooth(5.5, 2.0, lines)
    seam = smooth(3.0, 0.8, edge)
    bevel = smooth(1.0, 10.0, edge)

    broad = band_noise(404, 2, 7)
    mid = band_noise(405, 8, 36)
    fine = band_noise(406, 60, 300, 0.6)
    streak = aniso_noise(407, 2.0, 30)

    base = np.array((112, 86, 142), np.float32)
    color = np.broadcast_to(base, (SIZE, SIZE, 3)).copy()
    # Each of the four panels a slightly different tone, so the grid repeats less plainly.
    panel_tone = np.array([[1.04, 0.95], [0.97, 1.03]], np.float32)
    color *= panel_tone[(YS // cell).astype(int) % 2, (XS // cell).astype(int) % 2][..., None]
    # A soft sheen towards each panel's middle, as polished metal.
    sheen = 1.0 - np.clip(np.hypot(lx, ly) / 360.0, 0, 1)
    color *= (0.92 + 0.14 * sheen)[..., None]
    color *= (0.97 + 0.04 * streak + 0.06 * (fine - 0.5))[..., None]
    color *= (0.84 + 0.2 * smooth(0.2, 0.8, band_noise(410, 1, 4)))[..., None]
    # Inside the octagon a slightly deeper tone.
    inside = smooth(2, -6, octagon)
    color *= (1 - inside * 0.06)[..., None]
    # Inlay: a paler lilac metal down the grooves, dark at their very bottom.
    color = blend(color, (150, 126, 176), groove * 0.45)
    color = blend(color, (62, 44, 74), smooth(2.5, 0.5, lines) * 0.6)
    color = blend(color, (50, 36, 60), seam * 0.8)
    # Tarnish: grey-brown patches dulling the purple, heavier near edges.
    tarnish = np.clip(smooth(0.42, 0.8, broad * 0.55 + mid * 0.45) * 0.7 + smooth(30, 0, edge) * 0.25 * mid, 0, 1)
    grey = color.mean(axis=-1, keepdims=True)
    dull = grey * np.array((1.02, 0.96, 0.96), np.float32) * 0.9
    color = color * (1 - tarnish[..., None] * 0.38) + dull * tarnish[..., None] * 0.38
    stain = smooth(0.64, 0.88, band_noise(408, 4, 16)) * smooth(0.3, 0.8, mid)
    color = blend(color, (98, 78, 66), stain * 0.32)

    height = 0.2 + 0.55 * bevel - groove * 0.35 + 0.06 * mid + 0.04 * streak
    # Biomass in the inlay lines and creeping in from the panel seams.
    color, height, _ = creep_in_seams(color, height, np.minimum(lines, edge), 409, 22.0, runs_from=0.5, runs_px=6.0)
    color *= relief(height, 7.0)[..., None]
    return color, np.clip(height, 0, 1)


# ---------------------------------------------------------------------------- biomass and rot


def cell_ids(seed, count, warp_x, warp_y, size=SIZE):
    """Wrapped Worley cells on warped coordinates: (F1, F2, index of the nearest point). Warping makes the cells blobby, not polygons."""
    rng = np.random.default_rng(seed)
    points = rng.random((count, 2)) * size
    xs = XS + warp_x
    ys = YS + warp_y
    f1 = np.full((size, size), 1e9, np.float32)
    f2 = np.full((size, size), 1e9, np.float32)
    idx = np.zeros((size, size), np.int32)
    for i, (px, py) in enumerate(points):
        ddx = np.abs(np.mod(xs - px + size / 2, size) - size / 2)
        ddy = np.abs(np.mod(ys - py + size / 2, size) - size / 2)
        d = np.sqrt(ddx * ddx + ddy * ddy)
        closer = d < f1
        f2 = np.where(closer, f1, np.minimum(f2, d))
        idx = np.where(closer, i, idx)
        f1 = np.where(closer, d, f1)
    return f1, f2, idx


def cell_middle(f1, f2):
    """0 at a cell's border, 1 at its middle."""
    edge = (f2 - f1) * 0.5
    return np.clip(edge / np.maximum(f1 + edge, 1.0), 0, 1)


def cell_dome(f1, f2, power=0.6):
    """A round swelling over each cell: 1 at its seed, falling smoothly to 0 at its border, with no crease along the cell's middle."""
    r = np.clip(2.0 * f1 / np.maximum(f1 + f2, 1.0), 0, 1)
    return (1.0 - r * r) ** power


def biomass():
    """Living flesh: broad swollen cells and blisters packed together, tan, salmon-pink and muted red-brown, with dark purple-brown creases
    between them, soft folds and wrinkles over them, veins running along the creases and across the swellings, and wet highlights on their tops.
    Mid-toned overall, in broad shapes rather than busy noise, so that it reads as flesh from the battle camera and units read on it."""
    rng = np.random.default_rng(520)
    warp_x = (band_noise(521, 2, 6) - 0.5) * 46 + (band_noise(522, 6, 14) - 0.5) * 14
    warp_y = (band_noise(523, 2, 6) - 0.5) * 46 + (band_noise(524, 6, 14) - 0.5) * 14
    # Broad cells, about a metre and a bit across (1024 px is 8 m), and a smaller crop of blisters in between and on them.
    n_big = 40
    f1, f2, idx = cell_ids(525, n_big, warp_x, warp_y)
    t = cell_middle(f1, f2)
    swell_of = rng.uniform(0.65, 1.0, n_big).astype(np.float32)
    tone_of = rng.random(n_big).astype(np.float32)
    blister_of = (rng.random(n_big) < 0.22).astype(np.float32)
    swell = swell_of[idx]
    dome = np.clip(blur(cell_dome(f1, f2, 0.55), 7.0) * 1.08, 0, 1)  # blurred, so no ridges run from its middle to its corners
    n_small = 150
    s1, s2, sidx = cell_ids(526, n_small, warp_x * 0.6, warp_y * 0.6)
    st = cell_middle(s1, s2)
    small_on = (rng.random(n_small) < 0.25).astype(np.float32)[sidx] * smooth(0.25, 0.7, band_noise(527, 2, 7))
    sdome = cell_dome(s1, s2, 0.5) * small_on
    crease = smooth(0.1, 0.0, t)
    screase = smooth(0.14, 0.0, st) * small_on * smooth(0.1, 0.4, t)

    # Folds: soft wrinkles across the swellings, in a warped flow, stronger near the creases where the skin bunches.
    flow = wrap_sample(band_noise(528, 5, 16), warp_x * 1.5, warp_y * 1.5)
    folds = smooth(0.72, 0.98, ridged(flow)) * (0.35 + 0.65 * smooth(0.6, 0.15, t))
    broad = band_noise(529, 1, 4)
    fine = band_noise(530, 60, 260, 0.6)

    height = dome * swell * 0.8 + sdome * 0.08 - crease * 0.12 - screase * 0.02 - folds * 0.025 + fine * 0.02
    height = (height - height.min()) / (height.max() - height.min())

    # Colour per cell from a flesh palette, lighter on the swelling's top and deeper towards its border.
    tone = np.clip(tone_of[idx] * 0.75 + band_noise(531, 2, 6) * 0.25 - 0.08, 0, 1)
    lift = np.clip(dome * 0.75 + sdome * 0.15 + broad * 0.2, 0, 1)
    tan = ramp(lift, (118, 84, 66), (150, 112, 88), (176, 138, 108), (194, 160, 128))
    salmon = ramp(lift, (122, 72, 64), (156, 100, 88), (186, 128, 110), (204, 152, 132))
    pink = ramp(lift, (114, 68, 70), (148, 94, 96), (176, 120, 120), (198, 148, 144))
    redbrown = ramp(lift, (90, 52, 46), (114, 68, 58), (138, 88, 72), (160, 110, 92))
    w = np.stack([smooth(0.0, 0.3, tone) * smooth(0.55, 0.3, tone),     # tan
                  smooth(0.3, 0.5, tone) * smooth(0.78, 0.6, tone),     # salmon
                  smooth(0.62, 0.82, tone),                             # pink
                  smooth(0.25, 0.0, tone)], axis=-1)                    # red-brown
    w = w / np.maximum(w.sum(axis=-1, keepdims=True), 1e-4)
    color = tan * w[..., 0:1] + salmon * w[..., 1:2] + pink * w[..., 2:3] + redbrown * w[..., 3:4]
    # Blisters: paler, pinker, tighter skin.
    blister = blister_of[idx] * smooth(0.05, 0.4, t)
    color = blend(color, (206, 164, 150), blister * dome * 0.35)
    color = blend(color, (196, 150, 136), sdome * 0.3)
    # Creases: dark purple-brown, wet.
    color = blend(color, (104, 64, 64), smooth(0.3, 0.0, t) * 0.3)
    color = blend(color, (70, 42, 50), crease * 0.6)
    color = blend(color, (96, 58, 62), screase * 0.35)
    color = blend(color, (126, 82, 76), folds * 0.06)

    # Veins: cords of dark red-brown running along the creases and branching across the swellings, with a paler wet line along them.
    vf = ridged(wrap_sample(band_noise(532, 3, 10), warp_x * 2.0, warp_y * 2.0))
    vein = smooth(0.955, 0.99, vf) * smooth(0.35, 0.6, band_noise(533, 2, 6))
    vf2 = ridged(wrap_sample(band_noise(534, 8, 22), warp_x, warp_y))
    vein = np.maximum(vein, smooth(0.975, 0.995, vf2) * smooth(0.55, 0.8, band_noise(535, 3, 9)) * 0.45)
    along_crease = smooth(0.1, 0.03, t) * smooth(0.3, 0.6, band_noise(536, 3, 10))
    vein = np.maximum(vein, along_crease * 0.8)
    color = blend(color, (110, 44, 44), vein * 0.75)
    color = blend(color, (168, 96, 90), smooth(0.99, 0.999, vf) * vein * 0.5)
    height = np.clip(height + vein * 0.06, 0, 1)

    # Volume: the broad swellings shaded softly, the fine wrinkles on top, then a little fine grain in the skin.
    shade = np.clip(relief(blur(height, 4.0), 30.0) * relief(height, 2.0), 0.66, 1.34)
    color *= shade[..., None]
    color *= (0.95 + 0.1 * fine)[..., None]
    color *= (0.92 + 0.12 * broad)[..., None]
    # Wet: a soft sheen on the tops of the swellings facing the light, a tight highlight on their crowns, and small glints.
    wet = smooth(1.06, 1.26, shade) * smooth(0.5, 0.9, height)
    color = color + np.array((40, 30, 28), np.float32) * wet[..., None]
    crown = smooth(0.8, 0.97, dome) * swell * smooth(1.0, 1.2, shade)
    color = blend(color, (236, 212, 202), crown * 0.3)
    glints = blur(dots(537, 520, 0.8, 2.0), 0.6) * smooth(0.45, 0.85, height)
    color = blend(color, (240, 222, 214), np.clip(glints, 0, 1) * 0.3)
    return color, height


def rot():
    """Old, decaying mass: a darker red-brown and purple-brown crust of collapsed cells and pits, wet in the hollows, with threads of mould,
    pale fungal specks and small pustules. Mid-dark, never black."""
    warp_x = (band_noise(601, 3, 12) - 0.5) * 60
    warp_y = (band_noise(602, 3, 12) - 0.5) * 60
    mud = wrap_sample(band_noise(603, 3, 30, 1.0), warp_x, warp_y)
    fine = band_noise(604, 50, 300, 0.6)
    f1, f2, _ = cell_ids(605, 90, warp_x, warp_y)
    t = cell_middle(f1, f2)
    # Collapsed cells: a low rim and a sunken middle.
    sunk = smooth(0.35, 0.95, t)
    rimc = smooth(0.05, 0.2, t) * smooth(0.45, 0.25, t)
    p1, _ = cells(606, 900)
    pit = 1.0 - smooth(2.0, 8.0, p1)
    pits_on = smooth(0.4, 0.7, band_noise(607, 3, 10))
    height = np.clip(0.45 + rimc * 0.3 - sunk * 0.22 + mud * 0.2 + fine * 0.08 - pit * pits_on * 0.3, 0, 1)
    color = ramp(np.clip(mud * 0.6 + rimc * 0.4, 0, 1), (70, 44, 42), (88, 56, 52), (106, 70, 62), (124, 88, 76))
    color = blend(color, (84, 58, 74), smooth(0.5, 0.8, band_noise(608, 2, 8)) * 0.45)     # purple-brown bruising
    color = blend(color, (110, 90, 62), smooth(0.62, 0.85, band_noise(609, 3, 12)) * 0.3)  # dull ochre mould
    color = blend(color, (62, 40, 44), sunk * 0.35)
    color = blend(color, (54, 34, 38), pit * pits_on * 0.65)
    color = blend(color, (140, 104, 92), rimc * 0.22)
    color *= (0.9 + 0.2 * fine)[..., None]

    def fibre(r):
        g = r.uniform(0.85, 1.2)
        return (int(150 * g), int(128 * g), int(110 * g), 120)

    threads = strokes(610, 1800, 6, 24, 1, fibre)
    color = color * (1 - threads[..., 3:] * 0.4) + threads[..., :3] * 255.0 * threads[..., 3:] * 0.4
    spores = blur(dots(611, 2400, 0.6, 1.6), 0.5) * smooth(0.4, 0.7, band_noise(612, 2, 8))
    color = blend(color, (196, 182, 150), np.clip(spores, 0, 1) * 0.5)
    # Pustules: a pale wet head in a dark ring.
    rng = np.random.default_rng(613)
    centres = rng.random((22, 2)) * SIZE
    heads = np.concatenate([c + rng.normal(0, 18, (4, 2)) for c in centres])
    ring = np.clip(blur(stamp(heads, 9.0), 1.2) - blur(stamp(heads, 6.0), 1.0), 0, 1)
    head = blur(stamp(heads, 6.0), 1.0)
    hi = blur(stamp(heads - 2.0, 2.2), 0.6)
    color = blend(color, (60, 34, 34), ring * 0.6)
    color = blend(color, (196, 150, 128), head * 0.85)
    color = blend(color, (236, 214, 200), hi * 0.7)
    height = np.clip(height + spores * 0.06 + head * 0.3, 0, 1)
    color *= relief(height, 6.0)[..., None]
    return color, height


LAYERS = {
    "InteriorFloodHullUnsc": hull_unsc,
    "InteriorFloodHullCovenant": hull_covenant,
    "InteriorFloodBiomass": biomass,
    "InteriorFloodRot": rot,
}


def write_meta(png_path):
    meta = png_path + ".meta"
    if os.path.exists(meta):
        return
    here = os.path.dirname(os.path.abspath(__file__))
    template = os.path.join(here, "..", "..", "Assets", "_Project", "Resources", "Terrain", "GroundUnscDirt.png.meta")
    with open(template, "r", encoding="utf-8") as f:
        lines = f.read().split("\n")
    lines = ["guid: " + uuid.uuid4().hex if line.startswith("guid:") else line for line in lines]
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


def main():
    argv = sys.argv[1:]
    preview = None
    if "--preview" in argv:
        at = argv.index("--preview")
        preview = argv[at + 1]
        del argv[at:at + 2]
    here = os.path.dirname(os.path.abspath(__file__))
    out = argv[0] if argv else os.path.join(here, "..", "..", "Assets", "_Project", "Resources", "Terrain")
    out = os.path.normpath(out)
    only = [a for a in argv[1:]] if len(argv) > 1 else None
    for name, make in LAYERS.items():
        if only and name not in only:
            continue
        color, alpha = make()
        path = os.path.join(out, name + ".png")
        save(path, color, alpha)
        write_meta(path)
        c = np.clip(color, 0, 255)
        mean = c.mean(axis=(0, 1))
        lum = 0.3 * c[..., 0] + 0.59 * c[..., 1] + 0.11 * c[..., 2]
        print(name, "mean", np.round(mean).astype(int), "lum mean %.0f p5 %.0f p95 %.0f" % (lum.mean(), np.percentile(lum, 5), np.percentile(lum, 95)),
              "height %.2f" % alpha.mean())
        if preview:
            os.makedirs(preview, exist_ok=True)
            img = Image.fromarray(c.astype(np.uint8), "RGB")
            tiled = Image.new("RGB", (SIZE * 2, SIZE * 2))
            for i in range(2):
                for j in range(2):
                    tiled.paste(img, (i * SIZE, j * SIZE))
            tiled.resize((1024, 1024), Image.LANCZOS).save(os.path.join(preview, name + "_tiled.png"))
            img.save(os.path.join(preview, name + "_full.png"))
            Image.fromarray((np.clip(alpha, 0, 1) * 255).astype(np.uint8), "L").resize((512, 512)).save(os.path.join(preview, name + "_height.png"))


if __name__ == "__main__":
    main()
