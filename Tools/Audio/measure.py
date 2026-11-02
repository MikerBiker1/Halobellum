# measure.py
# Checks the generated audio without ears (M10): sample rate, clipping, peak, DC offset, loudness; for music also whether the loop
# closes without a click, whether any second is close to silence, and how the level moves over the track (its development).
# With "--pictures <folder>" it also draws a spectrogram of each music track and stinger, and a contact sheet of them.
# Usage: python measure.py [--pictures folder] [file.wav ...]    (no files: everything in GeneratedAudio). It exits with 1 if a check fails.
# Depends on: numpy, Pillow, synth.py.

import glob
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

import synth as s

FOLDER = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Project", "Audio", "Generated", "Resources", "GeneratedAudio")
CEILING_DB = -1.0
MAX_DC = 0.002
# For a seamless loop, the step from the last sample back to the first must look like any other step.
MAX_LOOP_STEP_RATIO = 5.0
MIN_MUSIC_SECOND_DB = -48.0


def spectrogram(x, height=320, width=1100):
    if x.ndim > 1:
        x = x.mean(axis=1)
    size = 4096
    hop = max(1, (len(x) - size) // width)
    window = np.hanning(size)
    columns = []
    for start in range(0, max(len(x) - size, 1), hop):
        chunk = x[start:start + size]
        if len(chunk) < size:
            chunk = np.pad(chunk, (0, size - len(chunk)))
        columns.append(np.abs(np.fft.rfft(chunk * window)))
    power = np.array(columns[:width]).T
    freqs = np.fft.rfftfreq(size, 1.0 / s.SR)
    # A logarithmic frequency axis, 30 Hz to 16 kHz, low at the bottom.
    wanted = 30.0 * (16000.0 / 30.0) ** (np.arange(height)[::-1] / (height - 1))
    rows = np.searchsorted(freqs, wanted).clip(1, len(freqs) - 1)
    image = 20.0 * np.log10(power[rows] / size + 1e-9)
    image = np.clip((image + 90.0) / 75.0, 0.0, 1.0)
    colour = np.stack((image ** 0.6, image ** 1.4, 0.3 + image * 0.4 - image ** 3 * 0.3), axis=2)
    return Image.fromarray((colour.clip(0, 1) * 255).astype(np.uint8))


def seconds_rms(x):
    mono = x if x.ndim == 1 else x.mean(axis=1)
    n = len(mono) // s.SR
    if n == 0:
        return np.array([20 * np.log10(np.sqrt(np.mean(mono ** 2)) + 1e-9)])
    blocks = mono[:n * s.SR].reshape(n, s.SR)
    return 20 * np.log10(np.sqrt(np.mean(blocks ** 2, axis=1)) + 1e-9)


def check(path):
    name = os.path.splitext(os.path.basename(path))[0]
    x, rate = s.read_wav(path)
    problems = []
    is_music = name.startswith("music_")
    peak = s.peak_db(x)
    clipped = int(np.sum(np.abs(x) >= 0.9999))
    dc = float(np.max(np.abs(np.mean(x, axis=0))))
    if rate != 44100:
        problems.append(f"rate {rate}")
    if clipped:
        problems.append(f"{clipped} clipped samples")
    if peak > CEILING_DB:
        problems.append(f"peak {peak:.2f} dB")
    if dc > MAX_DC:
        problems.append(f"DC {dc:.4f}")
    row = dict(name=name, seconds=len(x) / rate, channels=1 if x.ndim == 1 else x.shape[1], peak=peak, dc=dc, lufs=s.lufs(x), loud=s.momentary_peak(x), problems=problems)
    if is_music or name.startswith("sfx_hum"):
        step = np.abs(x[0] - x[-1]).max() if x.ndim > 1 else abs(x[0] - x[-1])
        diffs = np.abs(np.diff(x, axis=0))
        typical = float(np.percentile(diffs, 99))
        row["loop"] = float(step / max(typical, 1e-6))
        if row["loop"] > MAX_LOOP_STEP_RATIO:
            problems.append(f"loop step {row['loop']:.1f}x")
    if is_music:
        rms = seconds_rms(x)
        row["quietest"] = float(rms.min())
        row["loudest"] = float(rms.max())
        row["profile"] = " ".join(f"{v:.0f}" for v in rms[::10])
        if rms.min() < MIN_MUSIC_SECOND_DB:
            problems.append(f"almost silent second at {int(rms.argmin())} s ({rms.min():.1f} dB)")
        if row["seconds"] < 90.0 - 1e-3:
            problems.append(f"only {row['seconds']:.1f} s")
    return row, x


def main():
    args = sys.argv[1:]
    pictures = None
    if "--pictures" in args:
        i = args.index("--pictures")
        pictures = args[i + 1]
        del args[i:i + 2]
        os.makedirs(pictures, exist_ok=True)
    files = args or sorted(glob.glob(os.path.join(FOLDER, "*.wav")))
    total = 0
    failed = 0
    sheet = []
    for path in files:
        row, x = check(path)
        total += os.path.getsize(path)
        line = f"{row['name']:34s} {row['seconds']:6.2f} s {row['channels']}ch peak {row['peak']:6.2f} dB  DC {row['dc']:.4f}  LUFS {row['lufs']:6.1f}  loudest moment {row['loud']:6.1f}"
        if "loop" in row:
            line += f"  loop step {row['loop']:.2f}x"
        if "quietest" in row:
            line += f"  seconds {row['quietest']:.0f}..{row['loudest']:.0f} dB\n    level every 10 s: {row['profile']}"
        print(line)
        if row["problems"]:
            failed += 1
            print("    PROBLEM:", "; ".join(row["problems"]))
        if pictures and (row["name"].startswith("music_") or row["name"].startswith("stinger_")):
            picture = spectrogram(x)
            picture.save(os.path.join(pictures, row["name"] + ".png"))
            sheet.append((row["name"], picture))
    print(f"{len(files)} files, {total / 1e6:.1f} MB, {failed} with problems")
    if sheet:
        rows = sum(1 for n, _ in sheet if n.startswith("music"))
        small = [(n, p.resize((900, 150))) for n, p in sheet if n.startswith("music")]
        board = Image.new("RGB", (900, (150 + 18) * rows), (20, 20, 24))
        draw = ImageDraw.Draw(board)
        for i, (n, p) in enumerate(small):
            draw.text((4, i * 168 + 3), n, fill=(230, 230, 230))
            board.paste(p, (0, i * 168 + 18))
        board.save(os.path.join(pictures, "music_sheet.png"))
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
