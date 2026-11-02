# synth.py
# The small synthesizer behind every generated sound and music track in Halobellum (M10). All of it is original:
# nothing here reads any audio file. Oscillators, envelopes, filters, a reverb, the instruments of the music
# (brass, strings, choir, drums, chimes, wet organic noises), loudness measuring and a WAV writer.
# Depends on: numpy.

import struct
import wave

import numpy as np

SR = 44100


def samples(seconds):
    return int(round(seconds * SR))


def time_axis(n):
    return np.arange(n, dtype=np.float64) / SR


def midi_hz(m):
    return 440.0 * 2.0 ** ((m - 69.0) / 12.0)


def db(x):
    return 10.0 ** (x / 20.0)


# ------------------------------------------------------------------ filters

def lp(f, fc, order=2):
    return 1.0 / np.sqrt(1.0 + (f / fc) ** (2 * order))


def hp(f, fc, order=2):
    r = np.maximum(f, 1e-3) / fc
    return r ** order / np.sqrt(1.0 + r ** (2 * order))


def bp(f, fc, width=1.0):
    """A bump around fc; width is in octaves (one standard deviation)."""
    return np.exp(-0.5 * (np.log2(np.maximum(f, 1.0) / fc) / width) ** 2)


def fft_filter(x, response, pad=True):
    """Filters by a magnitude response(f). pad=False wraps around, which is what a seamless loop wants."""
    x = np.asarray(x, dtype=np.float64)
    n = len(x)
    size = n + (min(n, 8192) if pad else 0)
    spectrum = np.fft.rfft(x, size)
    spectrum *= response(np.fft.rfftfreq(size, 1.0 / SR))
    return np.fft.irfft(spectrum, size)[:n]


def svf(x, cutoff, q=0.8, mode="lp"):
    """A causal state-variable filter whose cutoff can change over time. For sweeps in short sounds (it is a Python loop)."""
    cutoff = np.broadcast_to(np.asarray(cutoff, dtype=np.float64), x.shape)
    g = np.tan(np.pi * np.minimum(cutoff, SR * 0.45) / SR)
    k = 1.0 / q
    a1 = 1.0 / (1.0 + g * (g + k))
    a2 = g * a1
    a3 = g * a2
    out = np.zeros_like(x, dtype=np.float64)
    ic1 = ic2 = 0.0
    want = {"lp": 0, "bp": 1, "hp": 2}[mode]
    xs = x.tolist()
    a1s, a2s, a3s = a1.tolist(), a2.tolist(), a3.tolist()
    ks = k
    gs = g.tolist()
    result = [0.0] * len(xs)
    for i, v in enumerate(xs):
        v3 = v - ic2
        v1 = a1s[i] * ic1 + a2s[i] * v3
        v2 = ic2 + a2s[i] * ic1 + a3s[i] * v3
        ic1 = 2.0 * v1 - ic1
        ic2 = 2.0 * v2 - ic2
        if want == 0:
            result[i] = v2
        elif want == 1:
            result[i] = v1
        else:
            result[i] = v - ks * v1 - v2
    return np.array(result)


def soft_clip(x, drive=1.0):
    return np.tanh(x * drive) / np.tanh(drive) if drive > 0 else x


# ------------------------------------------------------------------ envelopes and oscillators

def env_adsr(n, attack, decay, sustain, release):
    a, d, r = samples(attack), samples(decay), samples(release)
    a = max(a, 1)
    r = max(r, 1)
    hold = max(n - a - d - r, 0)
    parts = [np.linspace(0.0, 1.0, a, endpoint=False)]
    if d:
        parts.append(np.linspace(1.0, sustain, d, endpoint=False))
    parts.append(np.full(hold, sustain))
    parts.append(np.linspace(sustain, 0.0, r))
    e = np.concatenate(parts)
    if len(e) < n:
        e = np.pad(e, (0, n - len(e)))
    return e[:n]


def env_exp(n, tau, attack=0.0005):
    t = time_axis(n)
    e = np.exp(-t / tau)
    a = max(samples(attack), 1)
    e[:a] *= np.linspace(0.0, 1.0, a, endpoint=False)
    return e


def fade_out(x, seconds=0.02):
    n = min(samples(seconds), len(x))
    if n > 0:
        x = np.array(x)
        shape = np.linspace(1.0, 0.0, n)
        if x.ndim == 1:
            x[-n:] *= shape
        else:
            x[-n:] *= shape[:, None]
    return x


def phase_of(freq):
    """The running phase of a frequency that may change over time."""
    return 2.0 * np.pi * np.cumsum(freq) / SR


def sine_sweep(n, f_start, f_end, curve=8.0):
    t = np.linspace(0.0, 1.0, n)
    f = f_end + (f_start - f_end) * np.exp(-t * curve)
    return np.sin(phase_of(f))


def noise(n, rng):
    return rng.standard_normal(n)


def pan_stereo(x, pan):
    """pan: -1 left, 0 centre, +1 right, keeping the power even."""
    a = (pan + 1.0) * np.pi / 4.0
    return np.stack((x * np.cos(a), x * np.sin(a)), axis=1)


# ------------------------------------------------------------------ additive instruments

def _harmonic_count(f0, nmax, top=13000.0):
    return max(1, min(nmax, int(top / max(f0, 20.0))))


def brass(m, dur, vel=0.7, players=3, seed=0, formant=1100.0, bright=1.0, attack=None, release=0.12, nmax=28, mute=False):
    """A brass section: harmonics grow brighter with effort, the attack scoops up into the note."""
    rng = np.random.default_rng(seed + int(m * 100) + 7919)
    n = samples(dur + release)
    f0 = midi_hz(m)
    out = np.zeros((n, 2))
    t = time_axis(n)
    attack = attack if attack is not None else 0.03 + 0.05 * (1.0 - vel)
    hold = samples(dur)
    for p in range(players):
        delay = rng.uniform(0.0, 0.016)
        detune = rng.uniform(-6.0, 6.0)
        vib_rate = rng.uniform(4.7, 5.6)
        vib = np.minimum(t / 0.5, 1.0) ** 2 * 7.0 * np.sin(2 * np.pi * vib_rate * t + rng.uniform(0, 6.28))
        scoop = -30.0 * np.exp(-t / 0.035)
        drift = np.interp(t, np.linspace(0, t[-1] + 1e-3, 8), rng.uniform(-3.0, 3.0, 8))
        f = f0 * 2.0 ** ((detune + vib + scoop + drift) / 1200.0)
        phase = phase_of(f)
        # Effort over time: blooms just after the attack, settles, falls away in the release.
        a = samples(attack + delay)
        level = np.ones(n)
        level[:a] = np.linspace(0.0, 1.0, max(a, 1)) ** 1.4
        level[hold:] = np.linspace(1.0, 0.0, n - hold) ** 1.5
        bloom = 1.0 + 0.25 * np.exp(-np.maximum(t - attack, 0.0) / 0.09)
        effort = np.clip(vel * level * bloom, 0.0, 1.3)
        slope = (2.7 - 1.5 * effort * bright)
        y = np.zeros(n)
        count = _harmonic_count(f0, nmax, 9000.0 if mute else 13000.0)
        for h in range(1, count + 1):
            hz = h * f0
            shape = (1.0 + 1.6 * np.exp(-0.5 * (np.log2(hz / formant) / 0.85) ** 2)) * lp(hz, 6500.0, 2)
            y += np.exp(-slope * np.log(h)) * shape * np.sin(h * phase + rng.uniform(0, 6.28))
        y *= level * (0.55 + 0.45 * vel)
        out += pan_stereo(y, rng.uniform(-0.45, 0.45) if players > 1 else 0.0)
    return out / np.sqrt(players)


def strings(m, dur, vel=0.6, players=5, seed=0, attack=0.14, release=0.3, nmax=26, bright=1.0, vibrato=1.0, sul_pont=False):
    """A string section: a bowed saw shape with a wooden body, each player with their own vibrato."""
    rng = np.random.default_rng(seed + int(m * 100) + 104729)
    n = samples(dur + release)
    f0 = midi_hz(m)
    t = time_axis(n)
    out = np.zeros((n, 2))
    env = env_adsr(n, attack, 0.0, 1.0, release)
    for p in range(players):
        detune = rng.uniform(-9.0, 9.0)
        rate = rng.uniform(4.8, 6.2)
        depth = rng.uniform(9.0, 18.0) * vibrato
        vib = depth * np.minimum(t / 0.35, 1.0) * np.sin(2 * np.pi * rate * t + rng.uniform(0, 6.28))
        f = f0 * 2.0 ** ((detune + vib) / 1200.0)
        phase = phase_of(f)
        y = np.zeros(n)
        count = _harmonic_count(f0, nmax)
        for h in range(1, count + 1):
            hz = h * f0
            body = 1.0 + 0.9 * bp(hz, 320.0, 0.45) + 0.7 * bp(hz, 850.0, 0.4) + 0.8 * bp(hz, 2600.0, 0.5)
            amp = h ** (-(1.15 if not sul_pont else 0.55) / bright) * body * lp(hz, 7000.0 * bright, 2)
            y += amp * np.sin(h * phase + rng.uniform(0, 6.28))
        wobble = 1.0 + 0.1 * np.sin(2 * np.pi * rng.uniform(0.3, 1.1) * t + rng.uniform(0, 6.28))
        out += pan_stereo(y * env * wobble * (0.4 + 0.6 * vel), rng.uniform(-0.7, 0.7))
    return out / np.sqrt(players) * 0.8


def spiccato(m, dur=0.16, vel=0.8, players=3, seed=0):
    """Short, bouncing low-string notes for an ostinato."""
    rng = np.random.default_rng(seed + int(m * 100) + 15485863)
    n = samples(dur + 0.12)
    f0 = midi_hz(m)
    t = time_axis(n)
    out = np.zeros((n, 2))
    env = env_exp(n, dur * 0.65, attack=0.004)
    for p in range(players):
        phase = phase_of(np.full(n, f0 * 2.0 ** (rng.uniform(-8.0, 8.0) / 1200.0)))
        y = np.zeros(n)
        count = _harmonic_count(f0, 22, 7000.0)
        for h in range(1, count + 1):
            hz = h * f0
            decay = np.exp(-t * (1.0 + h * 1.4))
            y += h ** -0.9 * (1.0 + 0.8 * bp(hz, 400.0, 0.5)) * decay * np.sin(h * phase + rng.uniform(0, 6.28))
        out += pan_stereo(y * env * vel, rng.uniform(-0.5, 0.5))
    return fade_out(out / np.sqrt(players), 0.02)


VOWELS = {
    "ah": [(800, 90, 1.0), (1150, 100, 0.6), (2900, 130, 0.18), (3900, 160, 0.09)],
    "oh": [(450, 80, 1.0), (800, 90, 0.5), (2830, 130, 0.1), (3500, 150, 0.05)],
    "oo": [(325, 70, 1.0), (700, 80, 0.3), (2530, 120, 0.05)],
    "eh": [(530, 80, 1.0), (1840, 110, 0.45), (2480, 120, 0.2), (3500, 150, 0.08)],
    "mm": [(260, 70, 1.0), (1000, 200, 0.08)],
}


def _vowel_curve(hz, vowel):
    total = np.zeros_like(hz) + 0.012
    for freq, width, gain in VOWELS[vowel]:
        total = total + gain * np.exp(-0.5 * ((hz - freq) / width) ** 2)
    return total


def choir(m, dur, vowel="ah", to_vowel=None, vel=0.6, voices=6, seed=0, attack=0.7, release=0.9, spread=0.8, breath=0.035, nmax=30):
    """A choir: harmonics shaped by the formants of a vowel, which can slowly change into another."""
    rng = np.random.default_rng(seed + int(m * 100) + 32452843)
    n = samples(dur + release)
    f0 = midi_hz(m)
    t = time_axis(n)
    env = env_adsr(n, attack, 0.0, 1.0, release) * (0.4 + 0.6 * vel)
    out = np.zeros((n, 2))
    morph = np.minimum(t / max(dur, 0.01), 1.0)
    count = _harmonic_count(f0, nmax, 6500.0)
    hz = f0 * np.arange(1, count + 1)
    curve_a = _vowel_curve(hz, vowel)
    curve_b = _vowel_curve(hz, to_vowel or vowel)
    for v in range(voices):
        detune = rng.uniform(-14.0, 14.0)
        rate = rng.uniform(4.3, 5.7)
        depth = rng.uniform(10.0, 24.0)
        vib = depth * np.minimum(t / 0.8, 1.0) * np.sin(2 * np.pi * rate * t + rng.uniform(0, 6.28))
        drift = np.interp(t, np.linspace(0, t[-1] + 1e-3, 6), rng.uniform(-8.0, 8.0, 6))
        phase = phase_of(f0 * 2.0 ** ((detune + vib + drift) / 1200.0))
        y = np.zeros(n)
        for h in range(1, count + 1):
            amp = ((1.0 - morph) * curve_a[h - 1] + morph * curve_b[h - 1]) * h ** -0.5
            y += amp * np.sin(h * phase + rng.uniform(0, 6.28))
        out += pan_stereo(y * env, rng.uniform(-spread, spread))
    if breath > 0:
        air = fft_filter(noise(n, rng), lambda f: _vowel_curve(f, vowel) * hp(f, 300.0))
        air /= max(np.max(np.abs(air)), 1e-6)
        wide = np.stack((air, np.roll(air, 311)), axis=1)
        out += wide * (env * breath * np.sqrt(voices))[:, None]
    return out / np.sqrt(voices)


def glass(m, dur=2.4, vel=0.6, seed=0):
    """A resonant chime: inharmonic partials, each a slowly beating pair. The higher partials die first."""
    rng = np.random.default_rng(seed + int(m * 100) + 49979687)
    n = samples(dur)
    f0 = midi_hz(m)
    t = time_axis(n)
    y = np.zeros(n)
    for ratio, gain, tau in ((1.0, 1.0, 1.0), (2.32, 0.55, 0.6), (4.25, 0.3, 0.32), (6.63, 0.16, 0.16), (9.38, 0.08, 0.09)):
        hz = f0 * ratio
        if hz > 15000:
            continue
        beat = rng.uniform(0.3, 1.8)
        pair = np.sin(2 * np.pi * hz * t + rng.uniform(0, 6.28)) + 0.8 * np.sin(2 * np.pi * (hz + beat) * t + rng.uniform(0, 6.28))
        y += gain * pair * np.exp(-t / (tau * dur / 2.4))
    y[:samples(0.002)] *= np.linspace(0.0, 1.0, samples(0.002))
    return fade_out(y * vel * 0.5, 0.05)


# ------------------------------------------------------------------ drums

def snare(vel=0.8, seed=0, tight=True):
    rng = np.random.default_rng(seed + 179424673)
    n = samples(0.3)
    t = time_axis(n)
    wires = fft_filter(noise(n, rng), lambda f: bp(f, 3800.0, 1.1) * hp(f, 900.0)) * 2.2
    wires *= env_exp(n, 0.06 if tight else 0.1)
    shell = np.sin(2 * np.pi * (250.0 if tight else 190.0) * t) * np.exp(-t / 0.035) + 0.6 * np.sin(2 * np.pi * (410.0 if tight else 330.0) * t) * np.exp(-t / 0.022)
    crack = fft_filter(noise(n, rng), lambda f: hp(f, 2500.0)) * np.exp(-t / 0.004) * 0.5
    y = (wires * (0.55 + 0.5 * vel) + shell * 0.55 + crack * vel)
    return fade_out(soft_clip(y * vel * 0.55, 1.3), 0.02)


def bass_drum(vel=0.8, seed=0):
    rng = np.random.default_rng(seed + 198491317)
    n = samples(0.55)
    t = time_axis(n)
    body = sine_sweep(n, 125.0, 48.0, 24.0) * np.exp(-t / 0.2)
    beater = fft_filter(noise(n, rng), lambda f: lp(f, 1600.0, 2)) * np.exp(-t / 0.012) * 0.5
    return fade_out(soft_clip((body + beater) * vel * 1.2, 1.2), 0.03)


def timpani(m=38, vel=0.8, seed=0, dur=2.2):
    rng = np.random.default_rng(seed + int(m * 100) + 217645199)
    n = samples(dur)
    f0 = midi_hz(m)
    t = time_axis(n)
    y = np.zeros(n)
    for ratio, gain, tau in ((1.0, 1.0, 0.75), (1.504, 0.55, 0.5), (1.742, 0.3, 0.38), (2.0, 0.22, 0.3), (2.245, 0.14, 0.24), (2.494, 0.1, 0.18)):
        f = f0 * ratio * (1.0 + 0.02 * np.exp(-t / 0.05))
        y += gain * np.sin(phase_of(f)) * np.exp(-t / tau)
    mallet = fft_filter(noise(n, rng), lambda f: lp(f, 2000.0, 2)) * np.exp(-t / 0.01) * 0.5
    return fade_out((y + mallet) * vel * 0.8, 0.1)


def timpani_roll(m, dur, start=0.15, end=0.9, seed=0):
    rng = np.random.default_rng(seed + int(m * 100) + 413158511)
    n = samples(dur + 1.2)
    out = np.zeros(n)
    pos = 0.0
    while pos < dur:
        vel = start + (end - start) * pos / dur
        hit = timpani(m, vel * rng.uniform(0.85, 1.05), seed=int(pos * 100) % 4, dur=1.2)
        i = samples(pos)
        out[i:i + len(hit)] += hit[:max(n - i, 0)]
        pos += rng.uniform(0.085, 0.105)
    return out * 0.6


def snare_roll(dur, start=0.2, end=0.9, seed=0):
    rng = np.random.default_rng(seed + 553105243)
    n = samples(dur + 0.3)
    out = np.zeros(n)
    pos = 0.0
    while pos < dur:
        vel = (start + (end - start) * pos / dur) * rng.uniform(0.8, 1.05)
        hit = snare(vel, seed=int(rng.integers(0, 6)))
        i = samples(pos)
        out[i:i + len(hit)] += hit[:max(n - i, 0)]
        pos += rng.uniform(0.042, 0.052)
    return out * 0.6


def taiko(m=36, vel=0.85, seed=0, dur=1.6):
    """A deep drum: the pitch falls fast, a slap of skin on top, a long low body."""
    rng = np.random.default_rng(seed + int(m * 100) + 694847539)
    n = samples(dur)
    f0 = midi_hz(m)
    t = time_axis(n)
    f = f0 * (1.0 + 0.7 * np.exp(-t / 0.03))
    body = np.sin(phase_of(f)) * np.exp(-t / (0.32 + 0.1 * vel))
    over = 0.35 * np.sin(phase_of(f * 1.59)) * np.exp(-t / 0.14) + 0.2 * np.sin(phase_of(f * 2.14)) * np.exp(-t / 0.08)
    slap = fft_filter(noise(n, rng), lambda g: lp(g, 1100.0, 2) * hp(g, 90.0)) * np.exp(-t / 0.02) * 0.6
    return fade_out(soft_clip((body + over + slap) * vel * 1.15, 1.1), 0.1)


def frame_drum(vel=0.7, seed=0, f0=165.0):
    rng = np.random.default_rng(seed + 961748941)
    n = samples(0.4)
    t = time_axis(n)
    f = f0 * (1.0 + 0.25 * np.exp(-t / 0.012))
    body = np.sin(phase_of(f)) * np.exp(-t / 0.09) + 0.4 * np.sin(phase_of(f * 1.76)) * np.exp(-t / 0.05)
    slap = fft_filter(noise(n, rng), lambda g: bp(g, 1500.0, 1.0)) * np.exp(-t / 0.012) * 0.5
    return fade_out((body + slap) * vel, 0.03)


def gong(dur=7.0, vel=0.7, seed=0, f_low=70.0):
    """A big tam-tam: many inharmonic partials; the high ones bloom a moment after the hit."""
    rng = np.random.default_rng(seed + 1299709)
    n = samples(dur)
    t = time_axis(n)
    y = np.zeros(n)
    for i in range(46):
        hz = f_low * rng.uniform(1.0, 2.0) * 2.0 ** (i * 0.135)
        if hz > 9000:
            continue
        tau = dur * rng.uniform(0.12, 0.42) * (1.0 - min(hz / 14000.0, 0.8))
        bloom = 1.0 - np.exp(-t / (0.02 + hz / 9000.0 * 0.35))
        y += (hz / f_low) ** -0.35 * bloom * np.exp(-t / tau) * np.sin(2 * np.pi * hz * t + rng.uniform(0, 6.28))
    hit = fft_filter(noise(n, rng), lambda f: lp(f, 500.0, 2)) * np.exp(-t / 0.05) * 1.4
    y = y / 3.5 + hit
    return fade_out(y * vel, 0.5)


def cymbal_swell(dur, seed=0, peak=0.5):
    rng = np.random.default_rng(seed + 2750159)
    n = samples(dur + 0.6)
    y = fft_filter(noise(n, rng), lambda f: hp(f, 3500.0, 2) * lp(f, 12000.0, 1))
    y /= np.max(np.abs(y))
    shape = np.linspace(0.0, 1.0, samples(dur)) ** 2.6
    tail = np.exp(-np.arange(n - samples(dur)) / SR / 0.2)
    return y * np.concatenate((shape, tail)) * peak


def crash(vel=0.6, seed=0, dur=3.0):
    rng = np.random.default_rng(seed + 3121)
    n = samples(dur)
    t = time_axis(n)
    y = fft_filter(noise(n, rng), lambda f: hp(f, 2400.0, 2) * lp(f, 13000.0, 1))
    y /= np.max(np.abs(y))
    return fade_out(y * np.exp(-t / (dur * 0.26)) * vel, 0.2)


# ------------------------------------------------------------------ wet organic noises (the Flood)

def bubbles(dur, density=30.0, lo=120.0, hi=900.0, seed=0, tau=0.03, gain=1.0):
    """Many tiny rising chirps at random moments, which the ear takes for liquid."""
    rng = np.random.default_rng(seed + 15731)
    n = samples(dur)
    out = np.zeros(n)
    for _ in range(int(dur * density)):
        start = rng.integers(0, max(n - 1, 1))
        length = samples(rng.uniform(0.6, 2.0) * tau * 3.0)
        length = min(length, n - start)
        if length < 8:
            continue
        hz = np.exp(rng.uniform(np.log(lo), np.log(hi)))
        f = hz * (1.0 + np.linspace(0.0, rng.uniform(0.15, 0.7), length))
        e = np.exp(-np.arange(length) / SR / (tau * rng.uniform(0.5, 1.4)))
        e[:min(8, length)] *= np.linspace(0.0, 1.0, min(8, length))
        out[start:start + length] += np.sin(phase_of(f)) * e * rng.uniform(0.2, 1.0)
    return out * gain


def squelch(dur, f_start=1400.0, f_end=240.0, q=2.5, seed=0, density=40.0):
    """A wet, fleshy squeeze: noise through a closing resonance, with bubbles inside."""
    rng = np.random.default_rng(seed + 27644437)
    n = samples(dur)
    t = np.linspace(0.0, 1.0, n)
    cut = f_end + (f_start - f_end) * np.exp(-t * 3.5)
    wob = 1.0 + 0.2 * np.sin(2 * np.pi * rng.uniform(14.0, 28.0) * time_axis(n))
    y = svf(noise(n, rng), cut * wob, q=q, mode="bp")
    y /= max(np.max(np.abs(y)), 1e-6)
    y += bubbles(dur, density, min(f_start, f_end) * 0.7, max(f_start, f_end) * 0.7, seed=seed, tau=0.016, gain=0.4)
    return y * np.minimum(t / 0.02, 1.0) * np.exp(-t * 2.8)


def heartbeat(vel=0.8, seed=0, f0=46.0):
    """A heavy double thud with something wet in it."""
    rng = np.random.default_rng(seed + 6700417)
    n = samples(0.95)
    t = time_axis(n)
    out = np.zeros(n)
    for offset, gain in ((0.0, 1.0), (0.24, 0.65)):
        i = samples(offset)
        m = n - i
        tt = t[:m]
        thud = np.sin(phase_of(f0 * (1.0 + 0.6 * np.exp(-tt / 0.025)))) * np.exp(-tt / 0.11)
        slosh = fft_filter(noise(m, rng), lambda f: lp(f, 500.0, 2)) * np.exp(-tt / 0.05) * 0.5
        out[i:] += (thud + slosh) * gain
    return fade_out(soft_clip(out * vel * 1.2, 1.1), 0.08)


def glide_tone(m0, m1, dur, vel=0.5, seed=0, nmax=14, attack=0.8, release=1.0, bend="linear", rough=0.0):
    """A sustained tone sliding between pitches (dissonant, unstable): the Flood's drones."""
    rng = np.random.default_rng(seed + int(m0 * 37 + m1 * 53) + 2147483)
    n = samples(dur)
    t = time_axis(n)
    env = env_adsr(n, attack, 0.0, 1.0, release) * vel
    x = t / max(t[-1], 1e-3)
    if bend == "ease":
        x = x * x * (3.0 - 2.0 * x)
    midi = m0 + (m1 - m0) * x
    out = np.zeros((n, 2))
    for voice in range(3):
        wander = np.interp(t, np.linspace(0, t[-1] + 1e-3, 9), rng.uniform(-0.15, 0.15, 9))
        phase = phase_of(midi_hz(midi + wander + rng.uniform(-0.1, 0.1)))
        y = np.zeros(n)
        f0 = midi_hz(min(m0, m1))
        count = _harmonic_count(f0, nmax, 5000.0)
        for h in range(1, count + 1):
            y += h ** -1.2 * np.sin(h * phase + rng.uniform(0, 6.28))
        if rough > 0:
            flutter = 1.0 + rough * np.sin(2 * np.pi * rng.uniform(19.0, 37.0) * t + rng.uniform(0, 6.28))
            y *= flutter
        out += pan_stereo(y * env, rng.uniform(-0.6, 0.6))
    return out / np.sqrt(3.0)


def breath_noise(dur, lo=250.0, hi=1600.0, seed=0):
    """Slow, breathing filtered noise."""
    rng = np.random.default_rng(seed + 433494437)
    n = samples(dur)
    x = fft_filter(noise(n, rng), lambda f: hp(f, lo, 1) * lp(f, hi, 2))
    x /= np.max(np.abs(x))
    return x * np.sin(np.pi * np.linspace(0.0, 1.0, n)) ** 2


def growl(m, dur, vel=0.6, seed=0):
    """A low, rough, throaty tone: harmonics chopped at an uneven rate, formants on top."""
    rng = np.random.default_rng(seed + int(m * 100) + 271501)
    n = samples(dur)
    t = time_axis(n)
    f0 = midi_hz(m)
    wander = np.interp(t, np.linspace(0, t[-1] + 1e-3, 7), rng.uniform(-0.4, 0.4, 7))
    phase = phase_of(midi_hz(m + wander))
    y = np.zeros(n)
    count = _harmonic_count(f0, 40, 4000.0)
    for h in range(1, count + 1):
        hz = h * f0
        y += h ** -0.7 * (1.0 + 1.6 * bp(hz, 500.0, 0.5) + 1.0 * bp(hz, 1300.0, 0.4)) * np.sin(h * phase + rng.uniform(0, 6.28))
    chop = np.interp(t, np.arange(0, t[-1] + 0.05, 0.028), rng.uniform(0.25, 1.0, len(np.arange(0, t[-1] + 0.05, 0.028))))
    y = soft_clip(y / 6.0 * chop, 1.5)
    return y * env_adsr(n, 0.12, 0.0, 1.0, 0.25) * vel


# ------------------------------------------------------------------ reverb

def make_ir(seconds_long, bright=0.5, seed=0, predelay=0.018):
    """A stereo hall made of decaying noise: the high frequencies die first. Used by convolution."""
    rng = np.random.default_rng(seed + 83)
    n = samples(seconds_long)
    t = time_axis(n)
    ir = np.zeros((n, 2))
    edges = [(0, 300, 1.0), (300, 1200, 0.72), (1200, 4000, 0.42 * (0.6 + bright)), (4000, 16000, 0.2 * (0.4 + bright))]
    for channel in range(2):
        for lo, hi, life in edges:
            tail = noise(n, rng)
            tail = fft_filter(tail, lambda f: (hp(f, max(lo, 10), 3)) * lp(f, hi, 3))
            ir[:, channel] += tail * np.exp(-t / (life * seconds_long / 6.9))
    delay = samples(predelay)
    ir = np.vstack((np.zeros((delay, 2)), ir))[:n]
    onset = samples(0.012)
    ir[:delay + onset] *= np.linspace(0.0, 1.0, delay + onset)[:, None]
    ir[-samples(0.2):] *= np.linspace(1.0, 0.0, samples(0.2))[:, None]
    return ir / np.sqrt(np.sum(ir ** 2, axis=0, keepdims=True))


def convolve_circular(signal, ir):
    """Convolves a stereo signal around its own length, so a tail that runs off the end comes back at the start: a loop stays seamless."""
    n = len(signal)
    out = np.zeros_like(signal)
    for channel in range(2):
        out[:, channel] = np.fft.irfft(np.fft.rfft(signal[:, channel]) * np.fft.rfft(ir[:, channel], n), n)
    return out


def convolve_tail(signal, ir, tail_seconds=None):
    """Plain convolution with room for the tail, for sounds that end (returns the longer signal)."""
    if signal.ndim == 1:
        signal = np.stack((signal, signal), axis=1)
    n = len(signal) + len(ir)
    out = np.zeros((n, 2))
    for channel in range(2):
        out[:, channel] = np.fft.irfft(np.fft.rfft(signal[:, channel], n) * np.fft.rfft(ir[:, channel], n), n)
    return out


# ------------------------------------------------------------------ loudness and files

def _k_response(f):
    """The K weighting of ITU-R BS.1770, approximated by its magnitude response: a high-frequency shelf and a low cut."""
    f = np.maximum(f, 1.0)
    shelf = 1.0 + (db(4.0) - 1.0) * (f / 1680.0) ** 2 / (1.0 + (f / 1680.0) ** 2)
    low = (f / 38.0) ** 2 / np.sqrt(1.0 + (f / 38.0) ** 4)
    return shelf * low


def k_weighted(x):
    if x.ndim == 1:
        return fft_filter(x, _k_response, pad=False)
    return np.stack([fft_filter(x[:, c], _k_response, pad=False) for c in range(x.shape[1])], axis=1)


def lufs(x):
    """Integrated loudness with the gates of BS.1770 (0.4 s blocks, 75% overlap)."""
    k = k_weighted(x)
    # A mono file reaches both speakers, so it counts as two channels; otherwise it would play 3 dB louder than a stereo file of the same reading.
    energy = 2.0 * k ** 2 if k.ndim == 1 else np.sum(k ** 2, axis=1)
    size, hop = samples(0.4), samples(0.1)
    if len(energy) < size:
        return -0.691 + 10.0 * np.log10(np.mean(energy) + 1e-12)
    cumulative = np.concatenate(([0.0], np.cumsum(energy)))
    blocks = (cumulative[size:] - cumulative[:-size])[::hop] / size
    blocks = blocks[blocks > 10 ** ((-70.0 + 0.691) / 10.0)]
    if len(blocks) == 0:
        return -70.0
    relative = np.mean(blocks) * 0.1
    gated = blocks[blocks > relative]
    if len(gated) == 0:
        gated = blocks
    return -0.691 + 10.0 * np.log10(np.mean(gated))


def momentary_peak(x, window=0.1):
    """The loudest short window of a sound, K-weighted, in dB. Short sounds are levelled by this, because their average depends on their length."""
    k = k_weighted(x)
    energy = k ** 2 if k.ndim == 1 else np.sum(k ** 2, axis=1)
    size = min(samples(window), len(energy))
    cumulative = np.concatenate(([0.0], np.cumsum(energy)))
    return -0.691 + 10.0 * np.log10(np.max(cumulative[size:] - cumulative[:-size]) / size + 1e-12)


def peak_db(x):
    return 20.0 * np.log10(np.max(np.abs(x)) + 1e-12)


def limit_peak(x, ceiling_db=-1.2):
    peak = np.max(np.abs(x))
    ceiling = db(ceiling_db)
    return x * (ceiling / peak) if peak > ceiling else x


def level_short(x, target=-16.0, ceiling_db=-1.2):
    """Brings a short sound to a loudness by its loudest moment, saturating gently if it would go over the ceiling."""
    x = np.asarray(x, dtype=np.float64)
    gain = db(target - momentary_peak(x))
    y = x * gain
    ceiling = db(ceiling_db)
    peak = np.max(np.abs(y))
    if peak > ceiling:
        # Round off the loudest instant rather than lower the whole sound, as far as is gentle.
        drive = min(peak / ceiling, 3.0)
        y = np.tanh(y / peak * drive) / np.tanh(drive) * ceiling
    return limit_peak(y, ceiling_db)


def remove_dc(x):
    return x - np.mean(x, axis=0)


def write_wav(path, x):
    """16-bit, 44.1 kHz, with light dither so that quiet tails don't turn gritty."""
    x = np.asarray(x, dtype=np.float64)
    channels = 1 if x.ndim == 1 else x.shape[1]
    rng = np.random.default_rng(len(x))
    tpdf = (rng.random(x.shape) - rng.random(x.shape)) / 32768.0
    data = np.clip((x + tpdf) * 32767.0, -32768, 32767).astype("<i2")
    with wave.open(path, "wb") as f:
        f.setnchannels(channels)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes(data.tobytes())


def read_wav(path):
    with wave.open(path, "rb") as f:
        rate = f.getframerate()
        channels = f.getnchannels()
        raw = f.readframes(f.getnframes())
    x = np.frombuffer(raw, dtype="<i2").astype(np.float64) / 32768.0
    if channels > 1:
        x = x.reshape(-1, channels)
    return x, rate
