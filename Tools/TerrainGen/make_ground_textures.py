#!/usr/bin/env python
"""make_ground_textures.py

Makes the tiled ground textures of the faction terrain (M10): three layers for each of UNSC, Covenant and Flood,
a grey "grain" tile for concrete and metal, and a soft macro tile that breaks up repetition. All of it is original,
made here from nothing but random numbers. Every texture tiles seamlessly: all noise is built in the frequency domain
or with wrapped distances, so left meets right and top meets bottom.

The alpha channel of a layer is its height: the ground shader uses it so that one ground gives way to another around its
stones and clumps rather than in a smooth fade.

Usage (from the repo root):
    python Tools/TerrainGen/make_ground_textures.py [output folder] [--sheet contact_sheet.png]
Output defaults to Assets/_Project/Resources/Terrain. Needs numpy and Pillow. Same seed, same pictures.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

SIZE = 1024


# ---------------------------------------------------------------------------- noise


def band_noise(seed, low, high, power=1.0, size=SIZE):
    """Seamless noise with its energy between two frequencies (cycles per tile), scaled to 0..1 by its 1st and 99th percentiles."""
    rng = np.random.default_rng(seed)
    spectrum = rng.normal(size=(size, size)) + 1j * rng.normal(size=(size, size))
    fx = np.fft.fftfreq(size, 1.0 / size)
    fy = np.fft.fftfreq(size, 1.0 / size)
    radius = np.hypot(*np.meshgrid(fx, fy, indexing="xy"))
    radius[0, 0] = 1.0
    weight = radius ** (-power)
    weight *= np.clip((radius - low * 0.7) / (low * 0.6 + 1e-3), 0, 1)
    weight *= np.clip((high * 1.3 - radius) / (high * 0.6 + 1e-3), 0, 1)
    weight[0, 0] = 0.0
    field = np.real(np.fft.ifft2(spectrum * weight))
    lo, hi = np.percentile(field, [1, 99])
    return np.clip((field - lo) / (hi - lo), 0, 1)


def ridged(noise):
    """Sharp ridges where the noise crosses its middle."""
    return 1.0 - np.abs(noise * 2.0 - 1.0)


def wrap_sample(image, dx, dy):
    """Reads image at (x + dx, y + dy), wrapping at the edges, with bilinear filtering. For warping."""
    size = image.shape[0]
    ys, xs = np.meshgrid(np.arange(size), np.arange(size), indexing="ij")
    x = (xs + dx) % size
    y = (ys + dy) % size
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    fx = x - x0
    fy = y - y0
    x1 = (x0 + 1) % size
    y1 = (y0 + 1) % size
    x0 %= size
    y0 %= size
    if image.ndim == 3:
        fx = fx[..., None]
        fy = fy[..., None]
    top = image[y0, x0] * (1 - fx) + image[y0, x1] * fx
    bottom = image[y1, x0] * (1 - fx) + image[y1, x1] * fx
    return top * (1 - fy) + bottom * fy


def cells(seed, count, size=SIZE):
    """Wrapped distances to the nearest and second nearest of count random points, in pixels: (F1, F2)."""
    rng = np.random.default_rng(seed)
    points = rng.random((count, 2)) * size
    ys, xs = np.meshgrid(np.arange(size, dtype=np.float32), np.arange(size, dtype=np.float32), indexing="ij")
    f1 = np.full((size, size), 1e9, dtype=np.float32)
    f2 = np.full((size, size), 1e9, dtype=np.float32)
    for px, py in points:
        ddx = np.abs(xs - px)
        ddy = np.abs(ys - py)
        ddx = np.minimum(ddx, size - ddx)
        ddy = np.minimum(ddy, size - ddy)
        d = np.sqrt(ddx * ddx + ddy * ddy)
        closer = d < f1
        f2 = np.where(closer, f1, np.minimum(f2, d))
        f1 = np.where(closer, d, f1)
    return f1, f2


def relief(height, strength, light=(-0.55, 0.8)):
    """A shading factor around 1 from the slope of a height map, lit from one side, so bumps read when zoomed in or out."""
    gx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * 0.5
    gy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * 0.5
    return np.clip(1.0 + (gx * light[0] + gy * light[1]) * strength, 0.25, 1.9)


def ramp(value, *stops):
    """Colours value (0..1) by evenly spaced colours, given as (r, g, b) in 0..255."""
    colors = np.array(stops, dtype=np.float32)
    position = np.clip(value, 0, 1) * (len(stops) - 1)
    index = np.clip(position.astype(int), 0, len(stops) - 2)
    t = (position - index)[..., None]
    return colors[index] * (1 - t) + colors[index + 1] * t


def blend(base, color, mask):
    return base * (1 - mask[..., None]) + np.array(color, dtype=np.float32) * mask[..., None]


def desaturate(color, amount, lift=1.0):
    """Moves a colour towards its own grey, and optionally brightens it."""
    grey = color.mean(axis=-1, keepdims=True)
    return (color * (1 - amount) + grey * amount) * lift


def smooth(low, high, value):
    t = np.clip((value - low) / (high - low), 0, 1)
    return t * t * (3 - 2 * t)


def dots(seed, count, radius_low, radius_high, size=SIZE):
    """A height map of round wrapped bumps."""
    rng = np.random.default_rng(seed)
    image = Image.new("F", (size, size), 0.0)
    draw = ImageDraw.Draw(image)
    for _ in range(count):
        x, y = rng.random(2) * size
        r = rng.uniform(radius_low, radius_high)
        v = float(rng.uniform(0.5, 1.0))
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                if -r < x + ox < size + r and -r < y + oy < size + r:
                    draw.ellipse((x + ox - r, y + oy - r, x + ox + r, y + oy + r), fill=v)
    return np.asarray(image, dtype=np.float32).copy()


def blur(image, radius):
    """A wrapped Gaussian blur done in the frequency domain."""
    size = image.shape[0]
    fx = np.fft.fftfreq(size)
    fy = np.fft.fftfreq(size)
    r2 = fx[None, :] ** 2 + fy[:, None] ** 2
    kernel = np.exp(-2.0 * (np.pi * radius) ** 2 * r2)
    return np.real(np.fft.ifft2(np.fft.fft2(image) * kernel))


def strokes(seed, count, length_low, length_high, width, color_of, size=SIZE, angle_spread=np.pi):
    """Short wrapped lines over a transparent picture: blades of grass, fibres."""
    rng = np.random.default_rng(seed)
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    for _ in range(count):
        x, y = rng.random(2) * size
        a = rng.uniform(-angle_spread, angle_spread) - np.pi / 2
        n = rng.uniform(length_low, length_high)
        ex, ey = x + np.cos(a) * n, y + np.sin(a) * n
        c = color_of(rng)
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                if max(x, ex) + ox < 0 or min(x, ex) + ox > size or max(y, ey) + oy < 0 or min(y, ey) + oy > size:
                    continue
                draw.line((x + ox, y + oy, ex + ox, ey + oy), fill=c, width=width)
    return np.asarray(image, dtype=np.float32) / 255.0


# ---------------------------------------------------------------------------- UNSC


def unsc_dirt():
    broad = band_noise(11, 2, 6)
    mid = band_noise(12, 8, 40)
    fine = band_noise(13, 60, 300, 0.6)
    height = broad * 0.35 + mid * 0.45 + fine * 0.2

    color = ramp(np.clip(broad * 0.5 + mid * 0.5, 0, 1), (88, 66, 46), (116, 90, 62), (142, 114, 80), (160, 136, 100))
    color *= (0.84 + 0.32 * fine)[..., None]

    # Dry cracks.
    f1, f2 = cells(14, 320)
    crack = 1.0 - smooth(0.0, 3.2, f2 - f1)
    crack *= smooth(0.45, 0.65, band_noise(15, 3, 10))
    color = blend(color, (58, 42, 30), crack * 0.75)
    height -= crack * 0.25

    # Stones: big ones sparse, gravel plenty.
    big = blur(dots(16, 260, 3, 8), 1.1)
    gravel = blur(dots(17, 7000, 0.8, 2.2), 0.6)
    stones = np.clip(big + gravel * 0.6, 0, 1)
    tone = band_noise(18, 30, 120)
    stone_color = ramp(tone, (84, 78, 70), (128, 120, 108), (178, 168, 150))
    color = color * (1 - stones[..., None] * 0.92) + stone_color * stones[..., None] * 0.92
    shadow = np.clip(np.roll(np.roll(big + gravel * 0.5, 2, 0), 1, 1) - stones, 0, 1)
    color *= (1 - shadow * 0.55)[..., None]
    height = np.clip(height * 0.75 + stones * 0.35, 0, 1)
    color *= relief(height, 7.0)[..., None]
    return color, height


def unsc_grass():
    broad = band_noise(21, 2, 7)
    mid = band_noise(22, 8, 30)
    color = ramp(broad * 0.6 + mid * 0.4, (42, 62, 28), (62, 88, 36), (84, 110, 44), (116, 128, 58))

    dry = smooth(0.55, 0.85, band_noise(23, 3, 10))
    color = blend(color, (140, 130, 74), dry * 0.55)

    def blade(rng):
        g = rng.uniform(0.6, 1.25)
        kind = rng.random()
        if kind < 0.12:
            return (int(168 * g), int(158 * g), int(92 * g), 255)
        if kind < 0.45:
            return (int(52 * g), int(84 * g), int(34 * g), 255)
        return (int(88 * g), int(128 * g), int(48 * g), 255)

    layer = strokes(24, 90000, 4, 13, 2, blade, angle_spread=0.75)
    color = color * (1 - layer[..., 3:] * 0.8) + layer[..., :3] * 255.0 * layer[..., 3:] * 0.8

    # Shadows between the tufts.
    tufts = band_noise(25, 25, 70)
    color *= (0.72 + 0.56 * tufts)[..., None]
    height = np.clip(tufts * 0.55 + mid * 0.45, 0, 1)
    height = np.clip(height * 0.85 + 0.15, 0, 1)
    return color, height


def unsc_rock():
    warp_x = band_noise(31, 3, 12) * 60 - 30
    warp_y = band_noise(32, 3, 12) * 60 - 30
    strata = wrap_sample(band_noise(33, 3, 30, 1.2), warp_x, warp_y)
    ridges = ridged(band_noise(34, 8, 40))
    fine = band_noise(35, 50, 300, 0.6)
    height = np.clip(strata * 0.55 + ridges * 0.3 + fine * 0.15, 0, 1)
    color = ramp(height, (70, 66, 62), (108, 102, 94), (144, 136, 124), (176, 168, 152))
    f1, f2 = cells(36, 140)
    crack = 1.0 - smooth(0.0, 4.0, f2 - f1)
    color = blend(color, (42, 38, 36), crack * 0.8)
    height = np.clip(height - crack * 0.3, 0, 1)
    moss = smooth(0.62, 0.9, band_noise(37, 4, 20)) * smooth(0.3, 0.7, 1 - height)
    color = blend(color, (78, 92, 48), moss * 0.6)
    color *= relief(height, 11.0)[..., None]
    return color, height


# ---------------------------------------------------------------------------- Covenant


def cov_rock():
    warp_x = band_noise(41, 2, 7) * 180 - 90
    warp_y = band_noise(42, 2, 7) * 180 - 90
    flow = wrap_sample(band_noise(43, 2, 18, 1.1), warp_x, warp_y)
    layers = np.abs(np.sin(flow * np.pi * 11.0))
    fine = band_noise(44, 40, 240, 0.7)
    height = np.clip(flow * 0.7 + layers * 0.2 + fine * 0.1, 0, 1)
    color = ramp(flow, (64, 54, 86), (84, 72, 112), (110, 96, 140), (134, 122, 160))
    color *= (0.94 + 0.09 * layers)[..., None]
    color *= (0.94 + 0.12 * fine)[..., None]

    # Smooth plates of rock with worn, rounded joints.
    f1, f2 = cells(45, 22)
    joint = 1.0 - smooth(0.0, 7.0, f2 - f1)
    color = blend(color, (48, 40, 68), joint * 0.5)
    height = np.clip(height - joint * 0.25, 0, 1)
    color *= relief(height, 3.5)[..., None]
    return desaturate(color, 0.42, 1.04), height


def cov_plate():
    warp_x = band_noise(51, 2, 5) * 120 - 60
    warp_y = band_noise(52, 2, 5) * 120 - 60
    field = wrap_sample(band_noise(53, 1, 5, 1.4), warp_x, warp_y)
    # Contour lines of a smooth field: curved engraved lines, nothing like a human grid.
    bands = np.abs(((field * 9.0) % 1.0) - 0.5) * 2.0
    grooves = 1.0 - smooth(0.0, 0.12, 1.0 - bands)
    fine = band_noise(54, 60, 300, 0.5)
    tone = band_noise(55, 3, 16)
    color = ramp(tone, (46, 38, 70), (60, 50, 88), (76, 66, 108))
    color = blend(color, (116, 104, 156), (1 - grooves) * 0.55)
    color *= (0.92 + 0.16 * fine)[..., None]
    gloss = smooth(0.6, 0.95, band_noise(56, 2, 8))
    color = blend(color, (104, 96, 148), gloss * 0.22)
    height = np.clip(0.6 + grooves * 0.25 - (1 - grooves) * 0.3 + (fine - 0.5) * 0.1, 0, 1)
    color *= relief(height, 6.0)[..., None]
    return desaturate(color, 0.38, 1.18), height


def cov_sand():
    warp = band_noise(61, 2, 6) * 150 - 75
    ys = np.arange(SIZE, dtype=np.float32)[:, None] * np.ones((1, SIZE), dtype=np.float32)
    xs = np.arange(SIZE, dtype=np.float32)[None, :] * np.ones((SIZE, 1), dtype=np.float32)
    # Ripples that wrap: a whole number of waves across the tile.
    phase = (ys * 62 + xs * 14) / SIZE * 2 * np.pi
    ripple = 0.5 + 0.5 * np.sin(phase + warp * 0.11)
    grain = band_noise(62, 80, 400, 0.4)
    broad = band_noise(63, 2, 8)
    mid = band_noise(65, 18, 70)
    color = ramp(broad * 0.45 + mid * 0.3 + 0.12, (110, 100, 144), (136, 126, 168), (160, 152, 188))
    color *= (0.94 + 0.11 * ripple)[..., None]
    color *= (0.9 + 0.2 * grain)[..., None]
    shards = blur(dots(64, 1600, 0.7, 2.0), 0.5)
    color = blend(color, (206, 216, 240), np.clip(shards, 0, 1) * 0.55)
    height = np.clip(ripple * 0.5 + broad * 0.3 + grain * 0.2, 0, 1) * 0.7
    return desaturate(color, 0.4, 0.92), height


# ---------------------------------------------------------------------------- Flood


def flood_mass():
    f1, f2 = cells(71, 420)
    lump = 1.0 - np.clip(f1 / 34.0, 0, 1)
    lump = lump * lump * (3 - 2 * lump)
    crease = 1.0 - smooth(0.0, 7.0, f2 - f1)
    big = band_noise(72, 2, 8)
    fine = band_noise(73, 50, 260, 0.6)
    mottled = band_noise(74, 10, 50)
    height = np.clip(lump * 0.7 + big * 0.25 + fine * 0.05 - crease * 0.3, 0, 1)
    color = ramp(big * 0.45 + mottled * 0.55, (72, 76, 44), (92, 96, 52), (112, 112, 62), (134, 128, 78))
    color = blend(color, (58, 42, 32), crease * 0.62)
    pale = smooth(0.55, 0.95, lump) * smooth(0.5, 0.8, mottled)
    color = blend(color, (156, 148, 96), pale * 0.25)
    color *= (0.92 + 0.16 * fine)[..., None]
    color *= relief(height, 9.0)[..., None]
    return color, height


def flood_rot():
    warp_x = band_noise(81, 3, 12) * 70 - 35
    warp_y = band_noise(82, 3, 12) * 70 - 35
    mud = wrap_sample(band_noise(83, 3, 40, 1.0), warp_x, warp_y)
    fine = band_noise(84, 50, 300, 0.6)
    height = np.clip(mud * 0.75 + fine * 0.25, 0, 1)
    color = ramp(mud, (54, 40, 30), (78, 58, 40), (100, 76, 50), (122, 96, 64))
    color *= (0.84 + 0.3 * fine)[..., None]

    def fibre(rng):
        g = rng.uniform(0.7, 1.2)
        return (int(66 * g), int(44 * g), int(30 * g), 200)

    layer = strokes(85, 5000, 10, 34, 2, fibre)
    color = color * (1 - layer[..., 3:] * 0.6) + layer[..., :3] * 255.0 * layer[..., 3:] * 0.6
    spores = blur(dots(86, 2200, 0.8, 2.6), 0.6)
    color = blend(color, (156, 152, 84), np.clip(spores, 0, 1) * 0.7)
    height = np.clip(height + spores * 0.15, 0, 1)
    color *= relief(height, 9.0)[..., None]
    return color, height


def flood_sinew():
    warp_x = band_noise(91, 2, 8) * 130 - 65
    warp_y = band_noise(92, 2, 8) * 130 - 65
    vein_a = wrap_sample(ridged(band_noise(93, 4, 12, 1.3)), warp_x, warp_y)
    vein_b = wrap_sample(ridged(band_noise(94, 8, 24, 1.3)), warp_y, warp_x)
    veins = np.clip(smooth(0.86, 0.98, vein_a) + smooth(0.88, 0.98, vein_b) * 0.7, 0, 1)
    tissue = band_noise(95, 4, 30)
    fine = band_noise(96, 60, 300, 0.5)
    color = ramp(tissue, (92, 72, 56), (110, 88, 64), (124, 102, 74), (138, 118, 86))
    color *= (0.92 + 0.16 * fine)[..., None]
    color = blend(color, (136, 82, 66), blur(veins, 2.0) * 0.4)
    color = blend(color, (84, 44, 40), veins * 0.55)

    # Pustules: a pale, wet head in a dark ring. Few and dull: they must not look like units.
    heads = blur(dots(97, 60, 5, 12), 1.6)
    ring = np.clip(blur(dots(97, 60, 7, 16), 2.2) - heads, 0, 1)
    color = blend(color, (70, 48, 36), ring * 0.6)
    color = blend(color, (172, 164, 96), np.clip(heads * 1.1, 0, 1) * 0.7)
    height = np.clip(tissue * 0.5 + veins * 0.25 + heads * 0.6, 0, 1)
    color *= relief(height, 7.0)[..., None]
    return color, height


# ---------------------------------------------------------------------------- shared


def grain_tile():
    """Grey concrete and metal grain around 128, for multiplying a colour. Stains, speckle, hairline cracks."""
    speckle = band_noise(101, 120, 480, 0.3)
    mid = band_noise(102, 12, 60)
    broad = band_noise(103, 2, 8)
    value = 118 + (speckle - 0.5) * 30 + (mid - 0.5) * 28 + (broad - 0.5) * 34
    f1, f2 = cells(104, 26)
    crack = 1.0 - smooth(0.0, 2.2, f2 - f1)
    crack *= smooth(0.4, 0.7, band_noise(105, 3, 9))
    value = value * (1 - crack * 0.5)
    stains = smooth(0.7, 0.95, band_noise(106, 3, 14))
    value = value * (1 - stains * 0.25)
    # Alpha: wear. Painted lines wear away where this is low.
    wear = np.clip(band_noise(107, 6, 60) * 0.6 + speckle * 0.4, 0, 1)
    return np.stack([value, value, value], axis=-1), wear


def macro_tile():
    """Soft light and dark patches in R, finer noise in G and B, for repetition-breaking, the sky and haze."""
    size = 512
    r = band_noise(111, 1, 5, 1.0, size)
    g = band_noise(112, 3, 14, 1.0, size)
    b = band_noise(113, 10, 50, 0.8, size)
    return np.stack([r, g, b], axis=-1) * 255.0, np.ones((size, size), dtype=np.float32)


LAYERS = {
    "UnscDirt": unsc_dirt,
    "UnscGrass": unsc_grass,
    "UnscRock": unsc_rock,
    "CovenantRock": cov_rock,
    "CovenantPlate": cov_plate,
    "CovenantSand": cov_sand,
    "FloodMass": flood_mass,
    "FloodRot": flood_rot,
    "FloodSinew": flood_sinew,
    "Grain": grain_tile,
    "Macro": macro_tile,
}


def save(path, color, alpha):
    rgba = np.concatenate([np.clip(color, 0, 255), np.clip(alpha, 0, 1)[..., None] * 255.0], axis=-1)
    Image.fromarray(rgba.astype(np.uint8), "RGBA").save(path, optimize=True)


def main():
    argv = sys.argv[1:]
    sheet_path = None
    if "--sheet" in argv:
        at = argv.index("--sheet")
        sheet_path = argv[at + 1]
        del argv[at:at + 2]
    out = argv[0] if argv else os.path.join("Assets", "_Project", "Resources", "Terrain")
    os.makedirs(out, exist_ok=True)
    pictures = []
    for name, make in LAYERS.items():
        color, alpha = make()
        path = os.path.join(out, "Ground" + name + ".png")
        save(path, color, alpha)
        print(name, os.path.getsize(path) // 1024, "KB", "mean", np.round(color.mean(axis=(0, 1))).astype(int))
        pictures.append(Image.fromarray(np.clip(color, 0, 255).astype(np.uint8), "RGB").resize((340, 340), Image.LANCZOS))
    if sheet_path:
        columns = 4
        rows = (len(pictures) + columns - 1) // columns
        sheet = Image.new("RGB", (340 * columns, 340 * rows))
        for i, picture in enumerate(pictures):
            sheet.paste(picture, (340 * (i % columns), 340 * (i // columns)))
        sheet.save(sheet_path)


if __name__ == "__main__":
    main()
