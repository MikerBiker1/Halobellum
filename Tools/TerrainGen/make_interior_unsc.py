#!/usr/bin/env python
"""make_interior_unsc.py

Makes the three ground layers of the UNSC installation interior look (D80): a hangar deck.

  InteriorUnscDeck.png   layer 0: gunmetal steel deck plates, 4 m square (four by four in a tile of 16 m, each plate with its own split,
                         hatch, vent, tread or inset, so nothing repeats closer than 16 m), with seams, bolt rows and faint scuffs.
  InteriorUnscGrate.png  layer 1: heavy floor grating in framed 4 m panels, with a dark sub-floor showing through, and a few vented panels.
  InteriorUnscWorn.png   layer 2: worn, oil-stained, scraped steel with tread marks and a little rust, for traffic lanes and around machinery.

All of it is original, made here from random numbers and simple shapes: no logos, no markings, no text.
Every texture tiles seamlessly. Alpha is height (raised plates high, seams and holes low), which the ground shader uses
for blending and to keep the gloss off seams and grime.

The ground shader turns layer 1 by a 3-4-5 rotation (cos 0.8, sin 0.6) before sampling it. The grate is therefore drawn on a lattice turned the
other way, with a period that fits the tile exactly (one fifth of it), so that in the world its bars run straight along x and z.
Layer 2 is turned by an angle that fits no lattice, so it has no straight lines at all.

Usage (from the repo root):
    python Tools/TerrainGen/make_interior_unsc.py [output folder] [--preview folder]
Output defaults to Assets/_Project/Resources/Terrain. Needs numpy and Pillow. Same seed, same pictures.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_ground_textures import SIZE, band_noise, blend, blur, dots, ramp, relief, save, smooth  # noqa: E402


def coords():
    ys, xs = np.meshgrid(np.arange(SIZE, dtype=np.float32), np.arange(SIZE, dtype=np.float32), indexing="ij")
    return xs + 0.5, ys + 0.5


def line_mask(distance, half_width, soft=0.75):
    """1 inside a line of this half width (pixels), with an antialiased edge."""
    return np.clip((half_width + soft - distance) / (2.0 * soft), 0, 1)


def wrapped_draw(size, draw_fn, mode="F"):
    """Draws a shape nine times, offset by the tile, so anything crossing an edge comes back on the other side."""
    image = Image.new(mode, (size, size), 0.0 if mode == "F" else 0)
    draw = ImageDraw.Draw(image)
    for ox in (-size, 0, size):
        for oy in (-size, 0, size):
            draw_fn(draw, ox, oy)
    return np.asarray(image, dtype=np.float32).copy()


def aniso_noise(seed, along, size=SIZE):
    """Brushed-metal streaks: fine noise blurred along x only (wrapped)."""
    base = band_noise(seed, 80, 500, 0.3, size)
    out = np.zeros_like(base)
    for k in range(-along, along + 1):
        out += np.roll(base, k, axis=1)
    out /= 2 * along + 1
    lo, hi = np.percentile(out, [1, 99])
    return np.clip((out - lo) / (hi - lo), 0, 1)


def scuffs(seed, count, length_low, length_high, width, size=SIZE):
    """Short thin wrapped streaks, as a 0..1 mask with random strengths."""
    rng = np.random.default_rng(seed)
    items = []
    for _ in range(count):
        x, y = rng.random(2) * size
        a = rng.uniform(0, np.pi)
        n = rng.uniform(length_low, length_high)
        items.append((x, y, x + np.cos(a) * n, y + np.sin(a) * n, float(rng.uniform(0.3, 1.0))))

    def draw_all(draw, ox, oy):
        for x0, y0, x1, y1, v in items:
            draw.line((x0 + ox, y0 + oy, x1 + ox, y1 + oy), fill=v, width=width)

    return wrapped_draw(size, draw_all)


# ---------------------------------------------------------------------------- layer 0: deck plates


DECK_PLATES = 4  # Plates along each side of the tile: a tile of 16 m holds four by four plates of 4 m.

# What each plate of the tile carries, so that no feature repeats closer than 16 m. The order is shuffled by a fixed seed.
DECK_KINDS = ["plain", "plain", "plain", "plain", "split_h", "split_h", "split_v", "split_v",
              "hatch", "hatch", "vent", "vent", "tread", "quad", "inset", "split_v"]


def deck():
    xs, ys = coords()
    plate = SIZE / DECK_PLATES  # 256 px = 4 m, 64 px a metre.
    lx = xs % plate
    ly = ys % plate
    pid = (ys // plate).astype(int) * DECK_PLATES + (xs // plate).astype(int)
    edge = np.minimum(np.minimum(lx, plate - lx), np.minimum(ly, plate - ly))

    rng = np.random.default_rng(220)
    kinds = [DECK_KINDS[k] for k in rng.permutation(len(DECK_KINDS))]
    tones = rng.uniform(0.92, 1.08, len(kinds))

    def on(kind):
        mask = np.zeros_like(xs, dtype=bool)
        for k, name in enumerate(kinds):
            if name == kind:
                mask |= pid == k
        return mask

    broad = band_noise(201, 1, 6)
    mottle = band_noise(202, 10, 50)
    fine = band_noise(203, 120, 480, 0.4)
    brushed = aniso_noise(204, 6)

    tone = np.asarray(tones, dtype=np.float32)[pid]
    steel = np.array((100, 104, 111), dtype=np.float32)
    color = np.ones((SIZE, SIZE, 3), dtype=np.float32) * steel
    color *= (tone * (0.95 + 0.1 * broad) * (0.965 + 0.07 * mottle))[..., None]
    color *= (0.975 + 0.05 * brushed + 0.03 * (fine - 0.5))[..., None]
    height = np.full((SIZE, SIZE), 0.72, dtype=np.float32)

    # Sub-seams: plates split across, down, or both ways.
    half = plate * 0.5
    split_h, split_v, quad = on("split_h"), on("split_v"), on("quad")
    sub = np.full_like(xs, 1e9)
    sub = np.where(split_h | quad, np.minimum(sub, np.abs(ly - half)), sub)
    sub = np.where(split_v | quad, np.minimum(sub, np.abs(lx - half)), sub)
    seam_sub = line_mask(sub, 1.0, 0.7)

    # The main seam: a groove 4 px wide (about 7 cm), wide enough that the 4 m plates still read at the game camera.
    seam = line_mask(edge, 2.2, 0.7)
    groove = np.maximum(seam, seam_sub * 0.8)
    color = blend(color, (60, 63, 68), groove * 0.85)
    height -= groove * 0.62

    # Grime gathered along the seams, fading out over a few centimetres.
    grime = (1.0 - smooth(1.0, 12.0, np.minimum(edge, sub))) * (0.55 + 0.45 * band_noise(209, 12, 60))
    color *= (1.0 - grime * 0.14)[..., None]

    # A shallow inset line inside each plate, 15 px in.
    inset = line_mask(np.abs(edge - 15.0), 0.6, 0.7)
    color *= (1.0 - inset * 0.11)[..., None]
    height -= inset * 0.12

    # Bolt rows along the inset, every 32 px (half a metre), 8 px in from each edge.
    def ring(d):
        return line_mask(d, 2.5, 0.7)

    along_x = (lx % 32.0) - 16.0
    along_y = (ly % 32.0) - 16.0
    row = np.minimum(np.abs(ly - 8.0), np.abs(ly - (plate - 8.0)))
    col = np.minimum(np.abs(lx - 8.0), np.abs(lx - (plate - 8.0)))
    bolts = np.maximum(ring(np.sqrt(along_x ** 2 + row ** 2)), ring(np.sqrt(along_y ** 2 + col ** 2)))
    # Bolts beside the sub-seams.
    sub_row = np.abs(np.abs(ly - half) - 7.0)
    sub_col = np.abs(np.abs(lx - half) - 7.0)
    bolts = np.maximum(bolts, ring(np.sqrt(along_x ** 2 + sub_row ** 2)) * (split_h | quad) * (edge > 12))
    bolts = np.maximum(bolts, ring(np.sqrt(along_y ** 2 + sub_col ** 2)) * (split_v | quad) * (edge > 12))

    # Access hatches: a small square plate with its own seam, four bolts and a handle slot, set off-centre, each in its own place.
    for k, name in enumerate(kinds):
        if name != "hatch":
            continue
        onk = pid == k
        hs = float(rng.uniform(30.0, 40.0))
        cx = float(rng.uniform(60.0, plate - 60.0))
        cy = float(rng.uniform(60.0, plate - 60.0))
        hx, hy = np.abs(lx - cx), np.abs(ly - cy)
        box = np.maximum(hx, hy)
        hatch = line_mask(np.abs(box - hs), 1.1, 0.7) * onk
        color = blend(color, (70, 73, 78), hatch * 0.8)
        height -= hatch * 0.45
        inner = (box < hs - 1.5) & onk
        color *= np.where(inner, 1.02, 1.0)[..., None]
        corner = np.sqrt((hx - (hs - 8.0)) ** 2 + (hy - (hs - 8.0)) ** 2)
        bolts = np.maximum(bolts, ring(corner) * onk)
        # A hinge line along one side, inside the seam (no handle slot: a dash in a square reads as a sign).
        hinge = line_mask(np.abs(ly - (cy - hs + 7.0)), 0.8, 0.6) * (hx < hs - 10.0) * onk
        color = blend(color, (78, 81, 86), hinge * 0.6)
        height -= hinge * 0.2

    # Vent grilles: a framed rectangle of narrow dark slots, lying one way or the other.
    for k, name in enumerate(kinds):
        if name != "vent":
            continue
        onk = pid == k
        long_way = bool(rng.random() < 0.5)
        a, b = (lx, ly) if long_way else (ly, lx)
        ca = float(rng.uniform(100.0, plate - 100.0))
        cb = float(rng.uniform(56.0, plate - 56.0))
        da, db = np.abs(a - ca), np.abs(b - cb)
        frame_edge = np.maximum(da / 74.0, db / 24.0)
        inside = (frame_edge < 1.0) & onk
        rim = line_mask(np.abs(np.maximum(da - 74.0, db - 24.0)), 1.0, 0.7) * onk
        slats = line_mask(np.abs(((a - ca + 4.0) % 8.0) - 4.0), 1.4, 0.6)
        slot = inside & (da < 70.0) & (db < 20.0)
        color = np.where(slot[..., None], blend(color, (56, 59, 64), slats * 0.75), color)
        height = np.where(slot, height - slats * 0.4, height)
        color = blend(color, (66, 69, 74), rim * 0.8)
        height -= rim * 0.4

    # Tread plate: faint raised dashes on a diagonal lattice, alternating their direction, over the inset area.
    tread_on = on("tread") & (edge > 22)
    du, dw = lx + ly, lx - ly
    fu, fw = (du % 24.0) - 12.0, (dw % 24.0) - 12.0
    checker = ((np.floor(du / 24.0) + np.floor(dw / 24.0)) % 2) == 0
    dash_a = line_mask(np.abs(fu), 1.6, 0.6) * (np.abs(fw) < 7.0)
    dash_b = line_mask(np.abs(fw), 1.6, 0.6) * (np.abs(fu) < 7.0)
    lozenge = np.where(checker, dash_a, dash_b) * tread_on
    color *= (1.0 + lozenge * 0.07)[..., None]
    height += lozenge * 0.1

    # A plate with a smaller plate set into it: its own seam and a bolt at each corner.
    inset_on = on("inset")
    inner_edge = np.abs(edge - 52.0)
    inner_seam = line_mask(inner_edge, 1.0, 0.7) * inset_on
    color = blend(color, (66, 69, 74), inner_seam * 0.75)
    height -= inner_seam * 0.4
    color *= np.where(inset_on & (edge > 53.0), 0.975, 1.0)[..., None]
    corner_d = np.sqrt((np.minimum(lx, plate - lx) - 62.0) ** 2 + (np.minimum(ly, plate - ly) - 62.0) ** 2)
    bolts = np.maximum(bolts, ring(corner_d) * inset_on)

    # Bolt heads: a lit top, a dark rim.
    bolt_core = blur(bolts, 0.6)
    color = blend(color, (138, 143, 150), bolt_core * 0.55)
    height += bolts * 0.16

    # Scuffs and faint wear: lighter scrapes, a few darker smudges, small dents.
    scrape = blur(scuffs(205, 1500, 10, 55, 1), 0.4) * (0.4 + 0.6 * smooth(0.4, 0.7, band_noise(210, 3, 12)))
    color *= (1.0 + scrape * 0.1)[..., None]
    walk = smooth(0.55, 0.85, band_noise(211, 3, 10))
    color *= (1.0 - walk * 0.04)[..., None]
    smudge = smooth(0.6, 0.95, band_noise(206, 2, 8)) * (0.7 + 0.3 * band_noise(207, 30, 120))
    color *= (1.0 - smudge * 0.07)[..., None]
    dents = blur(dots(208, 80, 2, 5), 1.6)
    height -= np.clip(dents, 0, 1) * 0.03

    height = np.clip(height, 0, 1)
    color *= relief(height, 7.0)[..., None]
    return color, height


# ---------------------------------------------------------------------------- layer 1: grating


def grate():
    xs, ys = coords()
    u = xs
    v = SIZE - ys  # The texture's v runs up the picture.
    # World-aligned coordinates through the shader's 3-4-5 turn of this layer.
    p = 0.8 * u + 0.6 * v
    q = -0.6 * u + 0.8 * v
    panel = SIZE / 5.0  # 204.8 px: one fifth of the tile fits the lattice exactly.
    i = np.floor(p / panel).astype(int)
    j = np.floor(q / panel).astype(int)
    lp = p - i * panel
    lq = q - j * panel
    # A per-panel class that is the same wherever the tile repeats: (i + 18 j) mod 25 vanishes on the lattice steps (4, -3) and (3, 4).
    kind = (i + 18 * j) % 25
    edge = np.minimum(np.minimum(lp, panel - lp), np.minimum(lq, panel - lq))

    # The sub-floor seen through the grate: dark, with soft shapes of pipes and beams below, and a little cool light.
    deep_broad = band_noise(301, 2, 8)
    deep_mid = band_noise(302, 10, 40)
    pipes_p = 0.5 + 0.5 * np.cos(p / panel * 2.0 * np.pi * 2.0 + deep_broad * 2.0)
    under = ramp(deep_broad * 0.5 + deep_mid * 0.3 + pipes_p * 0.2, (30, 32, 36), (40, 43, 48), (52, 56, 62), (60, 64, 70))

    # Bearing bars every 12.8 px (sixteen to a panel) along one way, cross rods every 51.2 px the other. Alternate panels turn.
    turned = (kind % 2) == 1
    a = np.where(turned, lq, lp)
    b = np.where(turned, lp, lq)
    bar_d = np.abs(((a + 6.4) % 12.8) - 6.4)
    rod_d = np.abs(((b + 25.6) % 51.2) - 25.6)
    bar = line_mask(bar_d, 2.2)
    rod = line_mask(rod_d, 1.5) * (1.0 - bar)
    # Shadow of the bars on the sub-floor, beside each bar.
    near_bar = 1.0 - smooth(1.5, 6.0, bar_d)
    near_rod = 1.0 - smooth(1.0, 5.0, rod_d)

    steel_top = band_noise(303, 20, 120)
    bar_color = ramp(steel_top, (98, 102, 108), (112, 117, 123), (124, 129, 135))
    color = under * (1.0 - near_bar * 0.45 - near_rod * 0.25)[..., None]
    color = color * (1 - rod[..., None]) + bar_color * 0.86 * rod[..., None]
    color = color * (1 - bar[..., None]) + bar_color * bar[..., None]
    height = 0.12 + bar * 0.55 + rod * 0.42

    # Vented panels (six of the twenty-five classes): a solid plate with rows of rounded slots.
    vented = (kind == 3) | (kind == 8) | (kind == 12) | (kind == 17) | (kind == 21) | (kind == 24)
    slot_rows = np.abs(((lq + 14.0) % 28.0) - 14.0)
    slot_len = np.abs(lp - panel * 0.5) - (panel * 0.5 - 36.0)
    slot_shape = np.sqrt(np.maximum(slot_len, 0) ** 2 + slot_rows ** 2)
    # Break each row into three slots.
    seg = np.abs(((lp - 30.0) % ((panel - 60.0) / 3.0)) - (panel - 60.0) / 6.0)
    seg_gap = (seg > (panel - 60.0) / 6.0 - 5.0)
    in_slot = line_mask(slot_shape, 6.0) * (1.0 - seg_gap) * ((lq > 22) & (lq < panel - 22))
    plate_color = np.ones_like(color) * np.array((104, 108, 114), dtype=np.float32)
    plate_color *= (0.96 + 0.08 * steel_top)[..., None]
    vent_color = blend(plate_color, (38, 40, 45), in_slot)
    vent_height = 0.62 - in_slot * 0.5
    color = np.where(vented[..., None], vent_color, color)
    height = np.where(vented, vent_height, height)

    # The frame round each panel: solid steel, a seam between frames, and a bolt at each corner.
    frame = edge < 13.0
    frame_color = np.ones_like(color) * np.array((108, 112, 118), dtype=np.float32) * (0.95 + 0.1 * steel_top)[..., None]
    color = np.where(frame[..., None], frame_color, color)
    height = np.where(frame, 0.7, height)
    seam = line_mask(edge, 1.6)
    color = blend(color, (46, 48, 53), seam * 0.9)
    height -= seam * 0.5
    # Inner lip of the frame: a dark line where the frame drops to the grating.
    lip = line_mask(np.abs(edge - 13.0), 1.2)
    color = blend(color, (40, 42, 46), lip * 0.6 * (1 - vented))
    corner = np.sqrt((np.minimum(lp, panel - lp) - 7.0) ** 2 + (np.minimum(lq, panel - lq) - 7.0) ** 2)
    bolt = line_mask(corner, 3.0)
    color = blend(color, (140, 145, 152), bolt * 0.6)
    height += bolt * 0.12

    # Grime: darker in patches, a few scuffs on the steel tops.
    grime = smooth(0.55, 0.9, band_noise(304, 3, 14))
    color *= (1.0 - grime * 0.18)[..., None]
    fine = band_noise(305, 120, 480, 0.4)
    color *= (0.96 + 0.08 * fine)[..., None]

    height = np.clip(height, 0, 1)
    color *= relief(height, 5.0)[..., None]
    return color, height


# ---------------------------------------------------------------------------- layer 2: worn steel


def tread_tracks(seed, count, size=SIZE):
    """Pairs of tyre and track marks along gently curving wrapped paths. Returns a 0..1 mask."""
    rng = np.random.default_rng(seed)
    tracks = []
    for _ in range(count):
        x, y = rng.random(2) * size
        heading = rng.uniform(0, 2 * np.pi)
        turn = rng.uniform(-0.012, 0.012)
        gauge = rng.uniform(26, 44)
        width = rng.uniform(9, 15)
        steps = int(rng.uniform(140, 260))
        strength = float(rng.uniform(0.45, 1.0))
        block = rng.random() < 0.5
        pts = []
        for _ in range(steps):
            pts.append((x, y, heading))
            heading += turn + rng.normal(0, 0.004)
            x += np.cos(heading) * 6.0
            y += np.sin(heading) * 6.0
        tracks.append((pts, gauge, width, strength, block))

    def draw_all(draw, ox, oy):
        for pts, gauge, width, strength, block in tracks:
            for k, (x, y, h) in enumerate(pts):
                cx, cy = np.cos(h), np.sin(h)
                sx, sy = -cy, cx
                for side in (-1, 1):
                    mx = x + sx * gauge * 0.5 * side + ox
                    my = y + sy * gauge * 0.5 * side + oy
                    if block and k % 2 == 1:
                        continue
                    # A short tread block across the track.
                    hw = width * 0.5
                    hl = 2.4 if block else 3.4
                    poly = [
                        (mx - sx * hw - cx * hl, my - sy * hw - cy * hl),
                        (mx + sx * hw - cx * hl, my + sy * hw - cy * hl),
                        (mx + sx * hw + cx * hl, my + sy * hw + cy * hl),
                        (mx - sx * hw + cx * hl, my - sy * hw + cy * hl),
                    ]
                    draw.polygon(poly, fill=strength * (0.75 if block else 0.5))

    return wrapped_draw(size, draw_all)


def worn():
    broad = band_noise(401, 1, 5)
    mid = band_noise(402, 6, 28)
    fine = band_noise(403, 120, 480, 0.4)
    blotch = band_noise(404, 1, 5)

    color = ramp(broad * 0.75 + mid * 0.25, (90, 92, 96), (99, 101, 105), (107, 109, 112), (115, 116, 119))
    color *= (0.95 + 0.1 * fine)[..., None]
    # A fine mesh of tiny scratches all over, as on steel that has been walked and driven on for years.
    hairlines = blur(scuffs(414, 6000, 6, 26, 1), 0.3)
    color *= (1.0 + (hairlines - 0.25) * 0.08)[..., None]
    # Height stays smooth and broad, so that the ground shader's height blend lays worn steel down in soft drifts (and into seams first), never in speckles.
    height = np.clip(0.3 + broad * 0.3 + mid * 0.1, 0, 1)
    shading = np.clip(0.5 + (mid - 0.5) * 0.3 + (fine - 0.5) * 0.06, 0, 1)

    # Oil: soft dark stains, darkest at their hearts, some with a faint sheen ring. Kept mid-dark, never black.
    edge_noise = band_noise(405, 10, 60)
    oil_field = smooth(0.6, 0.85, blotch * 0.8 + edge_noise * 0.2)
    oil_core = smooth(0.75, 0.95, blotch * 0.8 + edge_noise * 0.2)
    drips = np.clip(blur(dots(406, 22, 8, 26), 6.0), 0, 1) * smooth(0.3, 0.6, edge_noise)
    oil = np.clip(oil_field * 0.6 + oil_core * 0.4 + drips * 0.5, 0, 1)
    color = blend(color, (70, 68, 66), oil * 0.6)
    height -= oil * 0.08
    shading -= oil * 0.12

    # Tread marks.
    tracks = blur(tread_tracks(407, 7), 0.8)
    tracks *= 0.5 + 0.5 * smooth(0.2, 0.7, band_noise(408, 2, 10))  # Fading in and out.
    color = blend(color, (64, 64, 64), np.clip(tracks, 0, 1) * 0.55)
    # Rubber smeared along the tracks, softly.
    color = blend(color, (84, 84, 84), np.clip(blur(tracks, 6.0) * 1.4, 0, 1) * 0.25)

    # Bright scrapes where steel shows through, in clusters.
    cluster = smooth(0.45, 0.8, band_noise(409, 3, 12))
    scrape = blur(scuffs(410, 1400, 12, 70, 1), 0.35) * cluster
    color = blend(color, (160, 163, 166), np.clip(scrape, 0, 1) * 0.5)
    shading += scrape * 0.04

    # A little rust: orange-brown speckle where oil and grime gather.
    rust_zone = smooth(0.7, 0.9, band_noise(411, 2, 8)) * smooth(0.25, 0.6, oil_field + 0.2)
    rust_mask = rust_zone * smooth(0.45, 0.85, band_noise(412, 60, 300, 0.5))
    color = blend(color, (118, 80, 54), rust_mask * 0.5)
    color = blend(color, (100, 82, 68), rust_zone * 0.18)
    shading -= rust_mask * 0.04

    # Pits and gouges.
    pits = blur(dots(413, 300, 0.8, 1.8), 0.6)
    color *= (1.0 - np.clip(pits, 0, 1) * 0.14)[..., None]
    shading -= np.clip(pits, 0, 1) * 0.06

    height = np.clip(height, 0, 1)
    color *= relief(np.clip(shading, 0, 1), 4.0)[..., None]
    return color, height


LAYERS = {
    "InteriorUnscDeck": deck,
    "InteriorUnscGrate": grate,
    "InteriorUnscWorn": worn,
}


def main():
    argv = sys.argv[1:]
    preview = None
    if "--preview" in argv:
        at = argv.index("--preview")
        preview = argv[at + 1]
        del argv[at:at + 2]
    out = argv[0] if argv else os.path.join("Assets", "_Project", "Resources", "Terrain")
    os.makedirs(out, exist_ok=True)
    for name, make in LAYERS.items():
        color, alpha = make()
        path = os.path.join(out, name + ".png")
        save(path, color, alpha)
        rgb = np.clip(color, 0, 255)
        lum = (rgb[..., 0] * 0.3 + rgb[..., 1] * 0.59 + rgb[..., 2] * 0.11)
        print(name, os.path.getsize(path) // 1024, "KB", "mean", np.round(rgb.mean(axis=(0, 1))).astype(int),
              "lum p5/p50/p95", np.round(np.percentile(lum, [5, 50, 95])).astype(int))
        if preview:
            os.makedirs(preview, exist_ok=True)
            tile = Image.fromarray(rgb.astype(np.uint8), "RGB")
            big = Image.new("RGB", (SIZE * 2, SIZE * 2))
            for ox in (0, SIZE):
                for oy in (0, SIZE):
                    big.paste(tile, (ox, oy))
            big.resize((1024, 1024), Image.LANCZOS).save(os.path.join(preview, name + "_2x2.png"))
            tile.crop((0, 0, 512, 512)).save(os.path.join(preview, name + "_crop.png"))


if __name__ == "__main__":
    main()
