#!/usr/bin/env python
"""make_interior_covenant.py

Makes the three ground layers of the Covenant warship interior (D80), all original and made from random numbers here:
  InteriorCovenantHull.png   layer 0, the main deck: smooth, polished blue-violet metal, mid-toned, crossed by a few great curved inlaid seams
                             (arcs of wide circles meeting in lens and teardrop panels), never a square grid.
  InteriorCovenantInlay.png  layer 1, ringed and decorative zones: darker, polished blue-indigo metal in rows of overlapping domed scales,
                             each with fine engraved rings.
  InteriorCovenantPlate.png  layer 2, raised dais and walkway areas: pale lavender-silver hexagonal plating with bevelled edges.
Every texture tiles seamlessly (wrapped distances and frequency-domain noise) and its alpha is its height: the ground shader lets
the higher layer push through, and polished plates shine more than their seams.

Usage (from the repo root):
    python Tools/TerrainGen/make_interior_covenant.py [output folder] [--preview folder]
Output defaults to Assets/_Project/Resources/Terrain. Needs numpy and Pillow. Same seed, same pictures.
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_ground_textures import SIZE, band_noise, blend, blur, ramp, relief, save, smooth  # noqa: E402

YS, XS = np.meshgrid(np.arange(SIZE, dtype=np.float32), np.arange(SIZE, dtype=np.float32), indexing="ij")


def wrapped_distance(px, py):
    """Distance in pixels from every texel to a point, across the wrapped edges."""
    dx = np.abs(XS - px)
    dy = np.abs(YS - py)
    dx = np.minimum(dx, SIZE - dx)
    dy = np.minimum(dy, SIZE - dy)
    return np.sqrt(dx * dx + dy * dy)


def streaks(seed, length, strength):
    """Fine polish marks running one way: noise smeared along x by a wrapped box blur."""
    noise = band_noise(seed, 90, 400, 0.4)
    k = int(length)
    total = np.zeros_like(noise)
    for s in range(-k, k + 1, 2):
        total += np.roll(noise, s, axis=1)
    total /= len(range(-k, k + 1, 2))
    lo, hi = np.percentile(total, [1, 99])
    return 1.0 + (np.clip((total - lo) / (hi - lo), 0, 1) - 0.5) * 2.0 * strength


def arc_distance(p1, p2, sag):
    """Distance from every texel to a circular arc from p1 to p2 that bulges sag pixels to one side (its sign picks the side), across the wrapped edges."""
    best = np.full((SIZE, SIZE), 1e9, dtype=np.float32)
    p1 = np.array(p1, dtype=np.float64)
    p2 = np.array(p2, dtype=np.float64)
    chord = p2 - p1
    length = np.linalg.norm(chord)
    u = chord / length
    n = np.array([-u[1], u[0]])
    radius = (length * length / 4 + sag * sag) / (2 * abs(sag))
    centre = (p1 + p2) / 2 + n * np.sign(sag) * (radius - abs(sag))
    a1 = np.arctan2(p1[1] - centre[1], p1[0] - centre[0])
    span = np.arctan2(p2[1] - centre[1], p2[0] - centre[0]) - a1
    span = (span + np.pi) % (2 * np.pi) - np.pi  # The short way round: the arc is never more than half a circle.
    for ox in (-SIZE, 0, SIZE):
        for oy in (-SIZE, 0, SIZE):
            px, py = XS - centre[0] - ox, YS - centre[1] - oy
            d = np.sqrt(px * px + py * py)
            along = (np.arctan2(py, px) - a1 + np.pi) % (2 * np.pi) - np.pi
            on = (along * np.sign(span) >= 0) & (along * np.sign(span) <= abs(span))
            best = np.where(on, np.minimum(best, np.abs(d - radius)), best)
    return best


def hull():
    """The main deck. Two chains of long lens-shaped panels run down the tile (along the field, like a corridor's runners), staggered,
    and from every pinch between two lenses a pair of shallow arcs sweeps out to the next chain: pointed, teardrop-like panels between.
    Every seam is a groove; the lenses are inlaid a little darker and bluer, with a fine lighter line inside them."""
    broad = band_noise(802, 1, 4)
    color = ramp(broad, (84, 88, 128), (92, 96, 140), (100, 104, 152))
    cool = smooth(0.45, 0.8, band_noise(804, 1, 3))
    color = blend(color, (92, 94, 142), cool * 0.65)

    bulge, length = 92.0, 512.0
    chains = [(256.0, 0.0), (768.0, 256.0)]  # Middle x and the y of a pinch.
    groove = np.zeros((SIZE, SIZE), dtype=np.float32)
    inner = np.zeros_like(groove)
    lens = np.zeros_like(groove)
    nearest = np.full_like(groove, 1e9)
    for xc, phase in chains:
        a = np.pi * (YS - phase) / length
        s = np.abs(np.sin(a))
        slope = np.abs(np.pi / length * bulge * np.cos(a))
        dx = ((XS - xc + SIZE / 2) % SIZE) - SIZE / 2
        for k in (1, -1):
            e = np.abs(dx - k * bulge * s) / np.sqrt(1 + slope * slope)
            nearest = np.minimum(nearest, e)
            groove = np.maximum(groove, 1 - smooth(1.4, 3.2, e))
            e2 = np.abs(dx - k * bulge * 0.72 * s) / np.sqrt(1 + (0.72 * slope) ** 2)
            inner = np.maximum(inner, (1 - smooth(1.0, 2.6, e2)) * smooth(0.25, 0.4, s))
        lens = np.maximum(lens, (np.abs(dx) < bulge * s).astype(np.float32))

    pinches = []
    for xc, phase in chains:
        for y in (phase, phase + length):
            pinches.append((xc, y % SIZE))
    near_node = np.full_like(groove, 1e9)
    for x, y in pinches:
        near_node = np.minimum(near_node, wrapped_distance(x, y))
    for x1, y1 in pinches:
        for dy, sag in ((256.0, 60.0), (-256.0, -60.0)):
            e = arc_distance((x1, y1), (x1 + 512.0, y1 + dy), sag)
            # The arcs leave from the node ring.
            e = np.where(near_node < 22.0, 1e9, e)
            nearest = np.minimum(nearest, e)
            groove = np.maximum(groove, 1 - smooth(1.4, 3.2, e))
    nodes = 1 - smooth(1.5, 3.5, np.abs(near_node - 21.0))
    fill = 1 - smooth(16.0, 19.0, near_node)

    # Panels swell gently away from their seams, like pressed metal.
    swell = smooth(0.0, 60.0, nearest)
    color *= (0.93 + 0.09 * swell)[..., None]
    color = blend(color, (70, 82, 140), lens * 0.45)
    polish = streaks(805, 24, 0.014)
    fine = band_noise(806, 120, 480, 0.5)
    color *= polish[..., None] * (0.985 + 0.03 * fine)[..., None]
    color = blend(color, (136, 126, 180), inner * 0.6)
    color = blend(color, (122, 112, 170), fill * 0.5)
    color = blend(color, (64, 56, 96), np.maximum(groove, nodes) * 0.6)

    height = 0.7 + 0.1 * swell + (fine - 0.5) * 0.015 - lens * 0.05 + inner * 0.06
    height = np.clip(height - groove * 0.5 - nodes * 0.3, 0, 1)
    color *= relief(blur(height, 1.2), 6.0)[..., None]
    return color, height


def inlay():
    """Ringed zones: rows of overlapping domed scales of dark indigo metal, each with engraved rings, later rows over earlier ones."""
    spacing = 128.0
    radius = spacing * 0.64
    rows = int(SIZE / (spacing * 0.5))  # 16: even, so the staggered rows wrap.
    near_d, near_i = [], []
    for j in range(rows):
        cy = j * spacing * 0.5
        offset = (j % 2) * spacing * 0.5
        best = np.full((SIZE, SIZE), 1e9, dtype=np.float32)
        best_i = np.zeros((SIZE, SIZE), dtype=np.int64)
        for i in range(int(SIZE / spacing)):
            d = wrapped_distance(i * spacing + offset, cy)
            closer = d < best
            best = np.where(closer, d, best)
            best_i = np.where(closer, i, best_i)
        near_d.append(best)
        near_i.append(best_i)
    # Each row lies over the one before it, round the wrap too: a texel belongs to the covering row whose next row doesn't cover it.
    owner_d = np.full((SIZE, SIZE), radius, dtype=np.float32)
    owner_id = np.zeros((SIZE, SIZE), dtype=np.int64)
    for j in range(rows):
        top = (near_d[j] < radius) & ~(near_d[(j + 1) % rows] < radius)
        owner_d = np.where(top, near_d[j], owner_d)
        owner_id = np.where(top, j * 64 + near_i[j], owner_id)

    t = owner_d / radius
    tone = ((owner_id * 2246822519) % 1000).astype(np.float32) / 1000.0
    broad = band_noise(811, 1, 4)
    base = ramp(broad, (62, 64, 108), (68, 70, 118), (76, 76, 128))
    base = blend(base, (66, 54, 108), smooth(0.5, 0.85, band_noise(812, 1, 3)) * 0.5)
    # A domed scale: lighter at its crown, darker into its rim, tone varied a little from scale to scale.
    dome = 1.08 - 0.22 * t * t
    color = base * (dome * (0.95 + 0.1 * tone))[..., None]

    rim = 1.0 - smooth(0.0, 0.035, 1.0 - t)  # The edge where the next scale overlaps.
    ring1 = 1.0 - smooth(0.006, 0.02, np.abs(t - 0.74))
    ring2 = 1.0 - smooth(0.006, 0.02, np.abs(t - 0.46))
    core = 1.0 - smooth(0.12, 0.16, t)
    color = blend(color, (122, 122, 182), ring1 * 0.6)
    color = blend(color, (104, 100, 160), ring2 * 0.45)
    color = blend(color, (84, 86, 140), core * 0.5)
    color = blend(color, (30, 28, 52), rim * 0.85)

    polish = streaks(813, 16, 0.014)
    fine = band_noise(814, 120, 480, 0.5)
    color *= polish[..., None] * (0.985 + 0.03 * fine)[..., None]
    height = np.clip(0.95 - 0.45 * t * t - rim * 0.4 + ring1 * 0.05 + (fine - 0.5) * 0.015, 0, 1)
    color *= relief(blur(height, 1.0), 4.0)[..., None]
    return color, height


def plate():
    """Pale lavender-silver hexagonal plates, a little squashed so that the lattice wraps (8 across, 5 rows of pairs), with bevels and dark seams."""
    cols, pairs = 8, 5
    sx = SIZE / cols
    sy = SIZE / (pairs * 2)
    points = []
    for j in range(pairs * 2):
        for i in range(cols):
            points.append(((i + 0.5 * (j % 2)) * sx, (j + 0.5) * sy))
    f1 = np.full((SIZE, SIZE), 1e9, dtype=np.float32)
    f2 = np.full((SIZE, SIZE), 1e9, dtype=np.float32)
    owner = np.zeros((SIZE, SIZE), dtype=np.int64)
    for k, (px, py) in enumerate(points):
        d = wrapped_distance(px, py)
        closer = d < f1
        f2 = np.where(closer, f1, np.minimum(f2, d))
        owner = np.where(closer, k, owner)
        f1 = np.where(closer, d, f1)
    edge = (f2 - f1) * 0.5  # Distance to the plate's border, near enough.

    rng = np.random.default_rng(821)
    tones = rng.uniform(0.93, 1.07, len(points))
    inset = rng.random(len(points)) < 0.4
    broad = band_noise(822, 1, 4)
    color = ramp(broad, (138, 132, 164), (150, 144, 176), (160, 154, 186))
    color = blend(color, (140, 146, 178), smooth(0.5, 0.85, band_noise(823, 1, 3)) * 0.4)
    color *= tones[owner][..., None]

    seam = 1.0 - smooth(1.0, 2.6, edge)
    bevel = smooth(2.0, 12.0, edge)
    inner = (1.0 - smooth(0.7, 1.9, np.abs(edge - 20.0))) * inset[owner]
    stud = np.zeros((SIZE, SIZE), dtype=np.float32)
    for k, (px, py) in enumerate(points):
        if not inset[k]:
            stud = np.maximum(stud, 1.0 - smooth(4.0, 6.0, wrapped_distance(px, py)))

    polish = streaks(824, 20, 0.014)
    fine = band_noise(825, 120, 480, 0.5)
    color *= polish[..., None] * (0.985 + 0.03 * fine)[..., None]
    color = blend(color, (112, 106, 140), inner * 0.6)
    color = blend(color, (118, 112, 148), stud * 0.6)
    color = blend(color, (78, 72, 104), seam * 0.85)

    height = np.clip(0.3 + 0.55 * bevel - inner * 0.12 + stud * 0.1 + (fine - 0.5) * 0.015, 0, 1)
    color *= relief(blur(height, 0.8), 5.0)[..., None]
    return color, height


LAYERS = {
    "InteriorCovenantHull": hull,
    "InteriorCovenantInlay": inlay,
    "InteriorCovenantPlate": plate,
}


def main():
    argv = sys.argv[1:]
    preview = None
    if "--preview" in argv:
        at = argv.index("--preview")
        preview = argv[at + 1]
        del argv[at:at + 2]
    here = os.path.dirname(os.path.abspath(__file__))
    out = argv[0] if argv else os.path.join(here, "..", "..", "Assets", "_Project", "Resources", "Terrain")
    os.makedirs(out, exist_ok=True)
    for name, make in LAYERS.items():
        color, alpha = make()
        color = np.clip(color, 0, 255)
        path = os.path.join(out, name + ".png")
        save(path, color, alpha)
        mean = color.mean(axis=(0, 1))
        lum = 0.3 * mean[0] + 0.59 * mean[1] + 0.11 * mean[2]
        print(name, os.path.getsize(path) // 1024, "KB", "mean", np.round(mean).astype(int), "lum", round(float(lum)),
              "std", round(float(color.mean(axis=-1).std()), 1), "alpha", round(float(alpha.mean()), 2))
        if preview:
            os.makedirs(preview, exist_ok=True)
            rgb = Image.fromarray(color.astype(np.uint8), "RGB")
            tiled = Image.new("RGB", (SIZE * 2, SIZE * 2))
            for ox in (0, SIZE):
                for oy in (0, SIZE):
                    tiled.paste(rgb, (ox, oy))
            tiled.resize((1024, 1024), Image.LANCZOS).save(os.path.join(preview, name + "_tiled.png"))


if __name__ == "__main__":
    main()
