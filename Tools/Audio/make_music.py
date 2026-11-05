# make_music.py
# The owner's own music replaced all of this (D67), and its files are gone from the game. Running this writes them again, unused. Kept for its history.
# Composes and renders the placeholder music of Halobellum (M10): a main menu track, and a menu, a deployment and a
# battle track for each faction, plus victory and defeat stingers. Every note here was written for this game; no melody
# is taken from any existing work. Everything is rendered on a circular buffer: a note or a reverb tail that runs off
# the end comes back at the start, so the loops close without a click by construction.
# Usage: python make_music.py [track ...]    (no names: all of them). Output: Assets/_Project/Audio/Generated/Resources/GeneratedAudio.
# Depends on: numpy, synth.py.

import functools
import os
import sys
from multiprocessing import Pool

import numpy as np

import synth as s
from synth import samples

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Project", "Audio", "Generated", "Resources", "GeneratedAudio")

# ------------------------------------------------------------------ engine


class Mix:
    """Named stereo buses on a circular timeline of one exact length."""

    def __init__(self, seconds, sends):
        self.n = samples(seconds)
        self.sends = sends
        self.buses = {name: np.zeros((self.n, 2), dtype=np.float32) for name in sends}

    def add(self, bus, audio, when, gain=1.0, pan=0.0):
        if audio.ndim == 1:
            audio = s.pan_stereo(audio, pan)
        target = self.buses[bus]
        start = samples(when) % self.n
        end = start + len(audio)
        if end <= self.n:
            target[start:end] += audio * gain
        else:
            first = self.n - start
            target[start:] += audio[:first] * gain
            target[:end - self.n] += audio[first:] * gain

    def automate(self, bus, points):
        """Multiplies a bus by a curve of (seconds, gain) points, joined up with straight lines."""
        xs, ys = zip(*points)
        curve = np.interp(np.arange(self.n) / s.SR, xs, ys).astype(np.float32)
        self.buses[bus] *= curve[:, None]

    def render(self, hall_seconds, bright, target_lufs, stereo, hp_hz=30.0, seed=1, wet_gain=1.0):
        dry = np.zeros((self.n, 2))
        wet = np.zeros((self.n, 2))
        for name, send in self.sends.items():
            dry += self.buses[name]
            wet += self.buses[name] * send
        room = s.make_ir(hall_seconds, bright=bright, seed=seed)
        x = dry + s.convolve_circular(wet, room) * wet_gain
        for c in range(2):
            x[:, c] = s.fft_filter(x[:, c], lambda f: s.hp(f, hp_hz, 2), pad=False)
        if not stereo:
            x = x.mean(axis=1)
        x = x - np.mean(x, axis=0)
        return master(x, target_lufs)


def master(x, target_lufs, ceiling_db=-1.2):
    """Brings a whole track to a loudness, rounding off only the few peaks that would go over the ceiling."""
    ceiling = s.db(ceiling_db)
    knee = ceiling * 0.62
    for _ in range(4):
        x = x * s.db(target_lufs - s.lufs(x))
        over = np.abs(x) > knee
        if np.any(over):
            x = np.where(over, np.sign(x) * (knee + (ceiling - knee) * np.tanh((np.abs(x) - knee) / (ceiling - knee))), x)
    return s.limit_peak(x, ceiling_db)


class Score:
    def __init__(self, bpm, beats_per_bar, bars):
        self.beat = 60.0 / bpm
        self.bpb = beats_per_bar
        self.bars = bars
        self.seconds = bars * beats_per_bar * self.beat

    def t(self, bar, beat=0.0):
        return (bar * self.bpb + beat) * self.beat


@functools.lru_cache(maxsize=2048)
def _render(kind, m, dur, vel, seed, extra):
    fn = {"brass": s.brass, "strings": s.strings, "choir": s.choir, "spic": s.spiccato, "glass": s.glass, "snare": s.snare, "bass": s.bass_drum,
          "timp": s.timpani, "taiko": s.taiko, "frame": s.frame_drum, "gong": s.gong, "crash": s.crash, "glide": s.glide_tone, "heart": s.heartbeat,
          "growl": s.growl}[kind]
    args = dict(extra)
    if kind in ("snare", "bass", "crash", "frame", "gong", "heart"):
        out = fn(vel=vel, seed=seed, **args)
    elif kind == "glass":
        out = fn(m, dur=dur, vel=vel, seed=seed, **args)
    elif kind in ("timp", "taiko"):
        out = fn(m=m, vel=vel, seed=seed, **args)
    elif kind == "spic":
        out = fn(m, dur=dur, vel=vel, seed=seed, **args)
    else:
        out = fn(m, dur, vel=vel, seed=seed, **args)
    return (out * TRIM.get(kind, 1.0)).astype(np.float32)


# Evens the instruments out, so that the gains in the scores below mean something: a note of brass alone would bury a drum.
TRIM = {"brass": 0.3, "strings": 0.36, "choir": 0.8, "spic": 1.25}

_counter = [0]


def note(kind, m=0.0, dur=0.0, vel=0.7, variants=3, **extra):
    """Renders a note, re-using the same render when it comes again; the variant number keeps repeats from sounding identical."""
    _counter[0] = (_counter[0] + 1) % variants
    return _render(kind, round(float(m), 2), round(float(dur), 2), round(float(vel) * 20) / 20.0, _counter[0], tuple(sorted(extra.items())))


def play_phrase(mix, score, bus, kind, bar, phrase, transpose=0.0, vel=0.7, legato=0.96, gain=1.0, beat=0.0, **extra):
    """Plays (pitch, beats) or (pitch, beats, velocity) pairs one after the other; a pitch of None is a rest."""
    position = bar * score.bpb + beat
    for item in phrase:
        pitch, beats = item[0], item[1]
        v = item[2] if len(item) > 2 else vel
        if pitch is not None:
            mix.add(bus, note(kind, pitch + transpose, beats * score.beat * legato, v, **extra), position * score.beat, gain)
        position += beats


CHORDS = {"Dm": (2, 5, 9), "C": (0, 4, 7), "A": (9, 1, 4), "F": (5, 9, 0), "Bb": (10, 2, 5), "Gm": (7, 10, 2), "Eb": (3, 7, 10), "Cm": (0, 3, 7), "D": (2, 6, 9),
          "Em": (4, 7, 11), "G": (7, 11, 2), "Am": (9, 0, 4), "B": (11, 3, 6), "E": (4, 8, 11), "Fm": (5, 8, 0), "Bm": (11, 2, 6), "Ab": (8, 0, 3)}


def voiced(chord, low, high):
    """Every note of a chord between two pitches."""
    tones = CHORDS[chord] if isinstance(chord, str) else chord
    return [m for m in range(low, high + 1) if m % 12 in tones]


def root_of(chord, low=36):
    pc = CHORDS[chord][0]
    m = low
    while m % 12 != pc:
        m += 1
    return m


def under(m, chord, gap=3):
    """The nearest note of the chord at least a gap below a melody note, for a harmony part."""
    tones = CHORDS[chord]
    k = int(m) - gap
    while k % 12 not in tones:
        k -= 1
    return k


def harmonize(phrases, chords, gap=3):
    out = []
    for phrase, chord in zip(phrases, chords):
        out.append([(under(i[0], chord, gap) if i[0] is not None else None, *i[1:]) for i in phrase])
    return out


def add_chords(mix, score, bus, chords, start_bar, low, high, vel=0.5, kind="strings", gain=1.0, beats=None, **extra):
    for i, chord in enumerate(chords):
        length = (beats if beats else score.bpb) * score.beat
        for m in voiced(chord, low, high):
            mix.add(bus, note(kind, m, length * 1.02, vel, **extra), score.t(start_bar + i), gain)


# ------------------------------------------------------------------ UNSC: a military march in D minor (original themes)

THEME_A = [
    [(62, 1.5), (62, 0.5), (65, 1), (69, 1)],
    [(67, 1.5), (65, 0.5), (64, 1), (60, 1)],
    [(62, 1.5), (65, 0.5), (69, 1), (72, 1)],
    [(70, 1), (69, 1), (67, 0.5), (64, 0.5), (69, 1)],
    [(69, 1.5), (70, 0.5), (72, 1), (74, 1)],
    [(74, 1.5), (72, 0.5), (70, 1), (65, 1)],
    [(67, 1), (70, 1), (74, 1.5), (72, 0.5)],
    [(73, 2), (69, 1), (64, 1)],
]
CHORDS_A = ["Dm", "C", "Dm", "A", "F", "Bb", "Gm", "A"]

THEME_B = [
    [(65, 1), (69, 1), (72, 2)],
    [(70, 1.5), (69, 0.5), (67, 2)],
    [(69, 1), (72, 1), (77, 2)],
    [(74, 1.5), (72, 0.5), (69, 1), (65, 1)],
    [(70, 1), (74, 1), (77, 1.5), (76, 0.5)],
    [(72, 3), (67, 1)],
    [(70, 1.5), (72, 0.5), (74, 1), (70, 1)],
    [(69, 2), (73, 1), (76, 1)],
]
CHORDS_B = ["F", "C", "F", "Dm", "Bb", "C", "Gm", "A"]

SNARE_P1 = [(0, 1.0), (0.5, 0.45), (0.75, 0.45), (1, 0.7), (2, 0.95), (2.5, 0.45), (2.75, 0.45), (3, 0.6), (3.5, 0.5)]
SNARE_P2 = [(0, 1.0), (0.25, 0.35), (0.5, 0.5), (1, 0.7), (1.5, 0.4), (1.75, 0.4), (2, 0.95), (2.5, 0.55), (3, 0.8), (3.25, 0.4), (3.5, 0.5), (3.75, 0.55)]
SNARE_FILL = [(0, 1.0), (0.5, 0.5), (1, 0.6), (1.25, 0.5), (1.5, 0.6), (1.75, 0.6), (2, 0.75), (2.25, 0.65), (2.5, 0.75), (2.75, 0.7), (3, 0.85), (3.25, 0.8), (3.5, 0.9), (3.75, 0.95)]


def snare_bar(mix, score, bus, bar, pattern, gain=1.0):
    for beat, vel in pattern:
        when = score.t(bar, beat)
        mix.add(bus, note("snare", vel=vel, variants=5), when, gain)
        if vel >= 0.9:  # A flam on the accents.
            mix.add(bus, note("snare", vel=0.3, variants=5), when - 0.028, gain)


def phrase_transposed(phrases, semitones):
    return [[(i[0] + semitones if i[0] is not None else None, *i[1:]) for i in p] for p in phrases]


def play_section(mix, score, bus, kind, bar, phrases, **kw):
    for i, phrase in enumerate(phrases):
        play_phrase(mix, score, bus, kind, bar + i, phrase, **kw)


def unsc_deployment():
    sc = Score(96, 4, 36)
    mix = Mix(sc.seconds, {"low": 0.25, "str": 0.4, "horn": 0.35, "tpt": 0.3, "bone": 0.3, "snare": 0.14, "drum": 0.25, "cym": 0.3})
    # The shape of the 36 bars: 4 alone, 8 theme A on horns, 8 theme B on trumpets, 8 theme A up a fourth with everyone, 8 of falling away and building back.
    a_low = ["Dm"] * 4 + CHORDS_A + CHORDS_B + ["Gm", "F", "Gm", "D", "Bb", "Eb", "Cm", "D"] + ["Dm"] * 4 + ["Bb", "C", "Dm", "A"]
    for bar, chord in enumerate(a_low):
        root = root_of(chord, 34)
        for beat, vel in ((0, 0.9), (0.75, 0.45), (1, 0.6), (2, 0.85), (2.75, 0.45), (3, 0.6)):
            mix.add("low", note("spic", root, 0.2, vel, players=3), sc.t(bar, beat))
            if bar >= 12:
                mix.add("low", note("spic", root + 12, 0.16, vel * 0.6, players=2), sc.t(bar, beat), 0.8)
        mix.add("low", note("strings", root, sc.bpb * sc.beat * 1.02, 0.45 if bar < 20 else 0.6, players=3, nmax=16, attack=0.12), sc.t(bar), 0.5 if bar >= 4 else 0.8)
        fifth = root + 7
        if bar >= 4:
            mix.add("drum", note("timp", root if root >= 38 else root + 12, vel=0.75, dur=1.6, variants=2), sc.t(bar))
            mix.add("drum", note("timp", fifth if fifth <= 47 else fifth - 12, vel=0.55, dur=1.4, variants=2), sc.t(bar, 2))
    # Snare: alone at first, then steadier and busier.
    for bar in range(36):
        if bar < 4:
            snare_bar(mix, sc, "snare", bar, SNARE_P1, 1.0)
        elif bar < 12:
            snare_bar(mix, sc, "snare", bar, SNARE_P1, 0.55)
        elif bar < 28:
            snare_bar(mix, sc, "snare", bar, SNARE_FILL if bar % 8 == 3 else SNARE_P2, 0.7)
        elif bar < 32:
            snare_bar(mix, sc, "snare", bar, SNARE_P1, 0.45)
        elif bar < 35:
            snare_bar(mix, sc, "snare", bar, SNARE_P2, 0.6)
    mix.add("snare", s.snare_roll(sc.bpb * sc.beat, 0.25, 0.85), sc.t(35), 0.8)
    mix.add("cym", s.cymbal_swell(sc.t(2), peak=0.35), sc.t(34))
    # Upper strings: thin, then full.
    add_chords(mix, sc, "str", CHORDS_A, 4, 57, 69, vel=0.35, players=4, nmax=18)
    add_chords(mix, sc, "str", CHORDS_B, 12, 57, 74, vel=0.5, players=4, nmax=18)
    gm = ["Gm", "F", "Gm", "D", "Bb", "Eb", "Cm", "D"]
    add_chords(mix, sc, "str", gm, 20, 55, 79, vel=0.7, players=4, nmax=18)
    add_chords(mix, sc, "str", ["Bb", "C", "Dm", "A"], 32, 57, 74, vel=0.6, players=4, nmax=18)
    # Horns: theme A, softly.
    horn = dict(formant=750.0, bright=0.75, players=3)
    play_section(mix, sc, "horn", "brass", 4, THEME_A, vel=0.55, **horn)
    # Trumpets: theme B, with horns a third or so below.
    tpt = dict(formant=1500.0, bright=1.0, players=3)
    play_section(mix, sc, "tpt", "brass", 12, THEME_B, vel=0.7, gain=0.85, **tpt)
    play_section(mix, sc, "horn", "brass", 12, harmonize(THEME_B, CHORDS_B), vel=0.6, **horn)
    # Everyone: theme A up a fourth, trombones and horns under it, a string line above.
    up = phrase_transposed(THEME_A, 5)
    play_section(mix, sc, "tpt", "brass", 20, up, vel=0.85, **tpt)
    play_section(mix, sc, "horn", "brass", 20, harmonize(up, gm), vel=0.75, **horn)
    play_section(mix, sc, "bone", "brass", 20, phrase_transposed(up, -12), vel=0.7, formant=900.0, bright=0.9, players=3, gain=0.7)
    counter = [(86, 1), (84, 0.5), (82, 0.5), (81, 1), (79, 1)]
    for bar in (21, 23, 25):
        play_phrase(mix, sc, "str", "strings", bar, counter, vel=0.55, transpose=-12 if bar == 25 else 0, players=4, attack=0.05, release=0.15, nmax=14, gain=0.6)
    mix.add("cym", s.crash(0.55, dur=3.0), sc.t(20))
    # Falling away: one horn remembers the start of the theme.
    play_section(mix, sc, "horn", "brass", 28, THEME_A[:2] + [[(None, 4)]] + THEME_A[2:3], vel=0.42, formant=750.0, bright=0.7, players=2)
    # Building back to the start, on the dominant so that the loop lands.
    build = [[(70, 1), (74, 1), (77, 2)], [(72, 1), (76, 1), (79, 2)], [(74, 1), (77, 1), (81, 2)], [(76, 2), (73, 1), (69, 1)]]
    play_section(mix, sc, "tpt", "brass", 32, build, vel=0.72, gain=0.8, **tpt)
    play_section(mix, sc, "horn", "brass", 32, harmonize(build, ["Bb", "C", "Dm", "A"]), vel=0.66, **horn)
    t = sc.t
    mix.automate("low", [(0, 1.0), (t(4), 0.8), (t(12), 0.85), (t(20), 1.0), (t(28), 0.6), (t(32), 0.8), (t(36), 1.0)])
    mix.automate("str", [(0, 0.7), (t(12), 0.85), (t(20), 1.0), (t(28), 0.7), (t(36), 1.0)])
    return mix.render(2.8, 0.55, -19.0, True)


def unsc_battle():
    sc = Score(128, 4, 48)
    mix = Mix(sc.seconds, {"low": 0.14, "str": 0.25, "horn": 0.25, "tpt": 0.25, "snare": 0.1, "drum": 0.16, "cym": 0.25})
    # Bars: 4 of drive, 8 of stabs, 8 theme, 8 theme up a tone, 4 of break, 8 of theme B high, 8 to the end.
    roots = ["Dm"] * 4 + ["Dm", "Dm", "Bb", "C"] * 2 + CHORDS_A
    up_chords = ["Em", "D", "Em", "B", "G", "C", "Am", "B"]
    roots += up_chords + ["Dm"] * 4 + CHORDS_B + ["Dm", "C", "Dm", "A", "F", "Bb", "Gm", "A"]
    drive = [(0.0, 1.0), (0.25, 0.5), (0.5, 0.55), (0.75, 0.75), (1.0, 0.5), (1.25, 0.55), (1.5, 0.8), (1.75, 0.5),
             (2.0, 0.95), (2.25, 0.5), (2.5, 0.55), (2.75, 0.75), (3.0, 0.5), (3.25, 0.7), (3.5, 0.8), (3.75, 0.6)]
    for bar, chord in enumerate(roots):
        root = root_of(chord, 34)
        for beat, vel in drive:
            mix.add("low", note("spic", root, 0.13, vel, players=2), sc.t(bar, beat))
            if vel > 0.7 and not 28 <= bar < 32:
                mix.add("low", note("spic", root + 12, 0.12, vel * 0.7, players=2), sc.t(bar, beat), 0.7)
        # Bass drum and snare.
        for beat in (0, 1.5, 2, 3.5):
            mix.add("drum", note("bass", vel=0.9, variants=2), sc.t(bar, beat), 0.9)
        if bar >= 4:
            snare_bar(mix, sc, "snare", bar, SNARE_FILL if bar % 8 == 7 else [(1, 0.95), (3, 0.95), (3.5, 0.45), (3.75, 0.55), (1.75, 0.35)], 0.7)
        else:
            snare_bar(mix, sc, "snare", bar, [(1, 0.8), (3, 0.8)], 0.55)
        if bar % 2 == 0 and bar >= 4:
            mix.add("drum", note("timp", root if root >= 38 else root + 12, vel=0.85, dur=1.2, variants=2), sc.t(bar))
        if bar % 8 == 4 and bar >= 12:
            mix.add("cym", s.crash(0.5, dur=2.4, seed=bar), sc.t(bar))
    horn = dict(formant=800.0, bright=0.9, players=3)
    tpt = dict(formant=1500.0, bright=1.1, players=3)
    # Stabs on the chords, in a rhythm of three against the drive.
    for bar in range(4, 12):
        chord = roots[bar]
        for beat, length, vel in ((0, 0.5, 0.85), (1.5, 0.5, 0.75), (3, 0.75, 0.8)):
            for m in voiced(chord, 57, 69):
                mix.add("horn", note("brass", m, length * sc.beat, vel, attack=0.015, release=0.06, **horn), sc.t(bar, beat), 0.8)
    # Theme A.
    play_section(mix, sc, "tpt", "brass", 12, THEME_A, vel=0.85, **tpt)
    play_section(mix, sc, "horn", "brass", 12, harmonize(THEME_A, CHORDS_A), vel=0.75, **horn)
    add_chords(mix, sc, "str", CHORDS_A, 12, 62, 74, vel=0.55, players=3, nmax=14)
    # Up a whole tone, higher strings.
    up = phrase_transposed(THEME_A, 2)
    play_section(mix, sc, "tpt", "brass", 20, up, vel=0.92, **tpt)
    play_section(mix, sc, "horn", "brass", 20, harmonize(up, up_chords), vel=0.82, **horn)
    add_chords(mix, sc, "str", up_chords, 20, 64, 79, vel=0.65, players=3, nmax=14)
    # The break: drums and rising stabs.
    for i, bar in enumerate(range(28, 32)):
        for beat in (0, 0.75, 1.5, 2.25):
            for m in (50 + i, 57 + i, 62 + i):
                mix.add("horn", note("brass", m, 0.2, 0.75 + i * 0.05, attack=0.012, release=0.05, **horn), sc.t(bar, beat), 0.8)
    mix.add("snare", s.snare_roll(sc.bpb * sc.beat, 0.3, 1.0, seed=3), sc.t(31), 0.75)
    mix.add("cym", s.cymbal_swell(sc.bpb * sc.beat, peak=0.4), sc.t(31))
    # Theme B an octave up, then back down.
    high = phrase_transposed(THEME_B, 12)
    play_section(mix, sc, "tpt", "brass", 32, high, vel=0.9, gain=0.8, **tpt)
    play_section(mix, sc, "tpt", "brass", 32, THEME_B, vel=0.85, **tpt)
    play_section(mix, sc, "horn", "brass", 32, harmonize(THEME_B, CHORDS_B), vel=0.8, **horn)
    add_chords(mix, sc, "str", CHORDS_B, 32, 62, 81, vel=0.72, players=3, nmax=14)
    # The end: the first theme in the low brass, the strings climbing over it.
    play_section(mix, sc, "horn", "brass", 40, phrase_transposed(THEME_A, -12), vel=0.95, formant=800.0, bright=1.0, players=4)
    add_chords(mix, sc, "str", CHORDS_A, 40, 69, 86, vel=0.65, players=3, nmax=12)
    mix.add("snare", s.snare_roll(sc.bpb * sc.beat, 0.25, 0.95, seed=4), sc.t(47), 0.75)
    t = sc.t
    mix.automate("low", [(0, 0.9), (t(32), 1.0), (t(48), 1.0)])
    return mix.render(1.9, 0.5, -18.0, False)


def unsc_menu():
    sc = Score(80, 4, 30)
    mix = Mix(sc.seconds, {"low": 0.4, "str": 0.5, "horn": 0.55, "snare": 0.5, "drum": 0.4})
    chords = ["Dm", "Bb", "F", "C", "Dm", "Bb", "Gm", "A"]
    prog = (chords * 4)[:30]
    for bar, chord in enumerate(prog):
        add_chords(mix, sc, "str", [chord], bar, 57, 72, vel=0.38, players=4, nmax=16, attack=0.5, release=0.8)
        root = root_of(chord, 34)
        mix.add("low", note("strings", root, sc.bpb * sc.beat * 1.03, 0.4, players=3, nmax=14, attack=0.5, release=0.8), sc.t(bar), 0.6)
    # A lone horn, the march theme at half speed and far away.
    slow = [[(i[0], i[1] * 2) for i in p] for p in THEME_A]
    horn = dict(formant=700.0, bright=0.65, players=1)
    for k, phrase in enumerate(slow[:4]):
        play_phrase(mix, sc, "horn", "brass", 6 + k * 2, phrase, vel=0.48, **horn)
    # The strings answer warmly, in the major.
    warm = [[(i[0] + 12, i[1] * 2) for i in p] for p in THEME_B[:4]]
    for k, phrase in enumerate(warm):
        play_phrase(mix, sc, "str", "strings", 14 + k * 2, phrase, vel=0.42, players=4, nmax=14, attack=0.25, release=0.5, gain=0.7)
    # A march on the far side of the base, heard between phrases.
    for bar in range(22, 28):
        snare_bar(mix, sc, "snare", bar, SNARE_P1, 0.18)
    for k, phrase in enumerate(slow[4:6]):
        play_phrase(mix, sc, "horn", "brass", 24 + k * 2, phrase, vel=0.45, **horn)
    for bar in (0, 8, 16, 24):
        mix.add("drum", s.timpani_roll(38, sc.beat * 7, 0.08, 0.45, seed=bar), sc.t(bar))
    mix.automate("str", [(0, 0.8), (sc.t(14), 1.0), (sc.t(22), 1.0), (sc.t(30), 0.8)])
    return mix.render(3.4, 0.45, -20.0, False)


# ------------------------------------------------------------------ Covenant: choir-like pads, deep drums, a strange scale
# The scale is D Eb F# G A Bb C# with some notes a little flat of it. The chords are stacked fourths and clusters, not tonal harmony.

COV_CHORDS = {
    "X1": (50, 57, 63, 69),
    "X2": (49, 56, 61, 67),
    "X3": (51, 58, 62, 70),
    "X4": (46, 53, 62, 66),
    "X5": (43, 55, 58, 66),
    "X6": (45, 52, 58, 63),
}
# A wandering line for the high voices (float pitches are between the keys).
COV_LINE = [(74, 3), (75, 1.5), (73, 1.5), (74, 4), (78, 2), (79, 1), (75.6, 3), (74, 2),
            (70, 2), (73, 1), (75, 3), (74, 2), (69, 3), (70, 1), (66, 2), (67, 4)]
COV_CELL = [86, 87, 90, 81, 82]  # The chimes: five notes that go around against the beat.


def cov_pads(mix, sc, bus, chords, bar, bars_each=1, vel=0.55, vowel="oh", to_vowel="ah", attack=1.2, gain=1.0, shift=0.0, voices=5):
    for i, name in enumerate(chords):
        length = bars_each * sc.bpb * sc.beat
        for m in COV_CHORDS[name]:
            mix.add(bus, note("choir", m + shift, length + 0.6, vel, vowel=vowel, to_vowel=to_vowel, attack=attack, release=1.4, voices=voices, nmax=24, variants=2),
                    sc.t(bar + i * bars_each), gain)


def covenant_deployment():
    sc = Score(72, 4, 27)
    mix = Mix(sc.seconds, {"drone": 0.5, "choir": 0.75, "high": 0.9, "chime": 0.85, "drum": 0.45, "frame": 0.35, "gong": 0.7})
    # A low, slow tone under everything.
    for k in range(0, 27, 3):
        length = 3 * sc.bpb * sc.beat + 2.0
        mix.add("drone", s.glide_tone(38 + (0.0 if k % 6 == 0 else 1.0), 38 + (1.0 if k % 6 == 0 else 0.0), length, vel=0.6, seed=k, nmax=10, attack=1.5, release=1.5), sc.t(k))
    # Bars 0 to 5: the low choir alone, then the chimes.
    cov_pads(mix, sc, "choir", ["X1", "X2", "X1", "X3", "X1", "X2"], 0)
    # Bars 6 to 13: deep drums come in.
    cov_pads(mix, sc, "choir", ["X1", "X3", "X2", "X1", "X4", "X3", "X2", "X1"], 6, vel=0.62)
    # Bars 14 to 19: the harmony moves away; the high voices enter.
    cov_pads(mix, sc, "choir", ["X4", "X5", "X3", "X6", "X4", "X5"], 14, vel=0.72, vowel="ah", to_vowel="oh")
    play_phrase(mix, sc, "high", "choir", 14, COV_LINE, vel=0.55, legato=1.05, vowel="oo", to_vowel="ah", voices=5, attack=0.55, release=0.9, nmax=14, variants=2)
    # Bars 20 to 24: everything thins out, one line bending between the notes.
    cov_pads(mix, sc, "choir", ["X1", "X3", "X1", "X2", "X1"], 20, vel=0.48, vowel="mm", to_vowel="oh")
    bent = [(74, 4), (73.6, 4), (70, 3), (69.65, 5), (66, 4), (67, 4)]
    play_phrase(mix, sc, "high", "choir", 20, bent, vel=0.42, legato=1.05, vowel="oo", voices=3, attack=0.7, release=1.0, nmax=12, variants=2)
    # Bars 25 and 26: the swell back to the start.
    cov_pads(mix, sc, "choir", ["X2", "X3"], 25, vel=0.68, vowel="oh", to_vowel="ah")
    mix.add("gong", s.cymbal_swell(sc.bpb * sc.beat * 2, peak=0.22), sc.t(25))
    # Chimes: five notes against four beats, so the pattern keeps shifting.
    step = 1.5
    pos = 3 * sc.bpb
    i = 0
    while pos < 25 * sc.bpb:
        bar = pos / sc.bpb
        if not (9.5 < bar < 12 or 19 < bar < 20):
            mix.add("chime", note("glass", COV_CELL[i % 5] - (12 if 14 <= bar < 20 else 0), 2.8, 0.42 + 0.15 * ((i % 5) == 0), variants=3), pos * sc.beat, pan=0.5 * np.sin(i * 1.3))
        pos += step
        i += 1
    # Deep drums.
    pattern = [(0, 36, 0.9), (2.5, 36, 0.6), (3, 33, 0.8)]
    for bar in range(6, 20):
        for beat, m, vel in pattern:
            mix.add("drum", note("taiko", m, vel=vel), sc.t(bar, beat))
        if bar >= 14:
            for beat, vel in ((1, 0.5), (1.5, 0.35), (1.75, 0.4), (3.5, 0.45), (3.75, 0.55)):
                mix.add("frame", note("frame", vel=vel, variants=3), sc.t(bar, beat), 0.6, pan=-0.3)
    for bar in (25, 26):
        mix.add("drum", note("taiko", 36, vel=0.8), sc.t(bar))
        mix.add("drum", note("taiko", 33, vel=0.85), sc.t(bar, 3))
    # Gongs mark the big turns.
    for bar, vel in ((0, 0.55), (14, 0.6), (20, 0.45)):
        mix.add("gong", note("gong", vel=vel, variants=2), sc.t(bar))
    t = sc.t
    mix.automate("drum", [(0, 1.0), (t(6), 0.7), (t(14), 1.0), (t(27), 1.0)])
    return mix.render(5.5, 0.45, -19.0, True, seed=2)


def covenant_battle():
    # Seven beats in a bar, grouped 3 + 2 + 2: it never quite settles.
    sc = Score(140, 7, 30)
    mix = Mix(sc.seconds, {"drone": 0.3, "low": 0.25, "choir": 0.55, "high": 0.6, "chime": 0.6, "drum": 0.25, "frame": 0.2, "gong": 0.5, "horn": 0.4})
    for k in range(0, 30, 2):
        mix.add("drone", s.glide_tone(38, 38 + (0.6 if k % 4 else -0.6), 2 * sc.bpb * sc.beat + 1.5, vel=0.7, seed=k, nmax=10, attack=0.5, release=1.0, rough=0.25), sc.t(k))
    bass_cell = [38, 38, 39, 38, 37, 38, 41]  # One note per beat, slowly shifting.
    for bar in range(30):
        shift = 1 if 24 <= bar < 26 else 2 if 26 <= bar < 28 else 3 if 28 <= bar < 30 else 0
        # Drums: the three groups, with a roll of small drums pulling at them.
        for beat, m, vel in ((0, 36, 1.0), (1.5, 36, 0.4), (3, 33, 0.85), (5, 36, 0.8), (6, 33, 0.55), (6.5, 33, 0.65)):
            if 20 <= bar < 24 and beat % 3:
                continue
            mix.add("drum", note("taiko", m, vel=vel), sc.t(bar, beat))
        for i in range(14):
            vel = (0.68 if i % 6 == 0 else 0.5 if i % 4 == 0 else 0.3) + 0.05 * (i % 3)
            mix.add("frame", note("frame", vel=vel, variants=3, f0=165.0 if i % 3 else 230.0), sc.t(bar, i * 0.5), 0.55, pan=0.35 * np.sin(i * 1.7))
        if bar >= 4 and not 20 <= bar < 24:
            for i, m in enumerate(bass_cell):
                mix.add("low", note("spic", m + shift, 0.24, 0.85 if i % 7 in (0, 3, 5) else 0.55, players=3), sc.t(bar, i))
    # Choir: hard stabs on the accents.
    stab = {"X1": 0, "X2": 1, "X3": 2}
    order = ["X1", "X1", "X2", "X1", "X3", "X1", "X4", "X2"]
    for bar in range(4, 20):
        name = order[(bar - 4) % 8]
        for beat, length in ((0, 1.1), (3, 0.9), (5, 0.9)):
            for m in COV_CHORDS[name]:
                mix.add("choir", note("choir", m, length * sc.beat, 0.85, vowel="ah", attack=0.03, release=0.25, voices=4, nmax=20, breath=0.05, variants=2), sc.t(bar, beat), 0.9)
    # Sustained high voices over it.
    for start in (12, 16):
        play_phrase(mix, sc, "high", "choir", start, [(74, 7), (75, 7), (73, 7), (74, 7)], vel=0.7, legato=1.05, vowel="ah", attack=0.4, release=0.7, voices=5, nmax=14, variants=2, gain=0.8)
    # A low, dark horn line: a tune of its own.
    horn_line = [(50, 3), (51, 2), (49, 2), (50, 3), (54, 2), (55, 2), (51, 3), (50, 2), (46, 2), (49, 7)]
    dark = dict(formant=600.0, bright=0.8, players=4)
    play_phrase(mix, sc, "horn", "brass", 12, horn_line, vel=0.75, **dark)
    play_phrase(mix, sc, "horn", "brass", 16, horn_line, vel=0.85, transpose=0, **dark)
    # The break: small drums and things in the air, high up.
    for k, bar in enumerate(range(20, 24)):
        mix.add("high", s.glide_tone(80 + k, 92 + k, sc.bpb * sc.beat, vel=0.2, seed=bar, nmax=4, attack=0.5, release=0.3), sc.t(bar))
        for i in range(7):
            mix.add("chime", note("glass", COV_CELL[(i * 2 + k) % 5] - 12, 1.2, 0.5, variants=3), sc.t(bar, i), pan=0.6 * np.sin(i * 2.1 + k))
    # The climax rises by a half step every two bars.
    for bar in range(24, 30):
        shift = 1 if bar < 26 else 2 if bar < 28 else 3
        for beat, length in ((0, 1.2), (3, 1.0), (5, 1.0)):
            for m in COV_CHORDS["X1"]:
                mix.add("choir", note("choir", m + shift, length * sc.beat, 0.95, vowel="ah", attack=0.03, release=0.25, voices=4, nmax=20, breath=0.05, variants=2), sc.t(bar, beat))
        play_phrase(mix, sc, "horn", "brass", bar, [(50 + shift, 3), (57 + shift, 2), (63 + shift, 2)], vel=0.9, **dark)
        for i in range(7):
            mix.add("chime", note("glass", COV_CELL[i % 5] + shift, 1.2, 0.5, variants=3), sc.t(bar, i), pan=0.6 * np.sin(i * 1.1))
    for bar in (0, 12, 24):
        mix.add("gong", note("gong", vel=0.7, variants=2), sc.t(bar))
    mix.add("gong", s.cymbal_swell(sc.bpb * sc.beat, peak=0.3), sc.t(29))
    return mix.render(3.2, 0.45, -18.0, False, seed=3)


def covenant_menu():
    sc = Score(64, 4, 24)
    mix = Mix(sc.seconds, {"drone": 0.55, "choir": 0.85, "high": 1.0, "chime": 0.95, "drum": 0.5, "gong": 0.8})
    for k in range(0, 24, 4):
        mix.add("drone", s.glide_tone(38, 38.3 if k % 8 else 37.7, 4 * sc.bpb * sc.beat + 2.0, vel=0.5, seed=k + 40, nmax=8, attack=2.0, release=2.0), sc.t(k))
    for k, name in enumerate(["X1", "X3", "X1", "X2", "X4", "X3", "X5", "X1", "X2", "X3", "X1", "X2"]):
        cov_pads(mix, sc, "choir", [name], k * 2, bars_each=2, vel=0.48 + 0.1 * np.sin(k * 0.7), vowel="mm" if k % 3 == 0 else "oo", to_vowel="oh", attack=2.0)
    play_phrase(mix, sc, "high", "choir", 8, [(i[0], i[1] * 2) for i in COV_LINE[:6]], vel=0.42, legato=1.05, vowel="oo", voices=4, attack=1.0, release=1.2, nmax=12, variants=2)
    i = 0
    pos = sc.bpb * 2.0
    while pos < 23 * sc.bpb:
        mix.add("chime", note("glass", COV_CELL[(i * 3) % 5] - (12 if i % 4 == 3 else 0), 3.4, 0.34, variants=3), pos * sc.beat, pan=0.6 * np.sin(i * 0.9))
        pos += 2.5 if i % 3 else 3.5
        i += 1
    for bar in (0, 8, 16):
        mix.add("gong", note("gong", vel=0.42, variants=2), sc.t(bar))
    for bar in (4, 6, 12, 14, 20, 22):
        mix.add("drum", note("taiko", 33, vel=0.45), sc.t(bar))
    return mix.render(6.0, 0.4, -20.0, False, seed=4)


# ------------------------------------------------------------------ Flood: dissonant, slow, organic pulses and drones

def add_bubbles(mix, bus, start, dur, density, seed, lo=90.0, hi=700.0, gain=1.0):
    y = s.bubbles(dur, density, lo, hi, seed=seed, tau=0.035, gain=0.5)
    n = len(y)
    y *= np.sin(np.pi * np.linspace(0.0, 1.0, n)) ** 0.5
    mix.add(bus, np.stack((y, np.roll(y, 1900)), axis=1).astype(np.float32), start, gain)


def flood_deployment():
    secs = 90.0
    mix = Mix(secs, {"drone": 0.45, "cluster": 0.65, "pulse": 0.3, "wet": 0.35, "breath": 0.6, "low": 0.5, "high": 0.9})
    rng = np.random.default_rng(11)
    # Under it all: three low tones a rubbing distance apart that drift around each other.
    for k in range(0, 90, 15):
        for m0, m1, seed in ((26.0, 26.4, 1), (27.0, 26.7, 2), (33.0, 32.6, 3)):
            mix.add("drone", s.glide_tone(m0 + rng.uniform(-0.1, 0.1), m1, 19.0, vel=0.55, seed=seed + k, nmax=12, attack=3.0, release=3.0, rough=0.08), k)
    # The heartbeat: every two seconds, softly; it gets closer and more urgent, then falls back.
    t = 0.0
    while t < secs - 0.5:
        strength = 0.35 + 0.4 * np.exp(-((t - 66.0) / 14.0) ** 2)
        mix.add("pulse", note("heart", vel=strength, variants=3), t)
        speed = 2.0 - 0.75 * np.exp(-((t - 66.0) / 12.0) ** 2)
        t += speed
    # Second pulse in another time, from bar 30 on: five against the heart.
    t = 30.0
    while t < 80.0:
        mix.add("pulse", note("taiko", 31, vel=0.42, variants=2), t, 0.8)
        t += 1.6
    # Wet life moving around, thicker in the middle.
    for k in range(0, 90, 10):
        add_bubbles(mix, "wet", k, 14.0, 9.0 + 16.0 * np.exp(-((k - 58.0) / 24.0) ** 2), seed=k)
    # Breath: slow in, slow out.
    for k in range(4, 90, 11):
        y = s.breath_noise(9.0, 180.0, 1300.0, seed=k) * 0.32
        mix.add("breath", np.stack((y, np.roll(y, 777)), axis=1).astype(np.float32), k)
    # The crawl: a low tone that keeps falling a little, then again, never landing on a note.
    crawl = [(50.0, 47.5, 9.0, 8.0), (49.0, 46.2, 9.0, 26.0), (51.0, 47.6, 8.0, 44.0), (49.5, 45.5, 9.0, 58.0), (52.0, 48.7, 8.0, 72.0)]
    for m0, m1, dur, when in crawl:
        mix.add("low", s.glide_tone(m0, m1, dur, vel=0.55, seed=int(when), nmax=18, attack=1.0, release=1.5, bend="ease", rough=0.2), when)
    # Bars 45 to 80: a dissonant cloud of strings gathers.
    for when, chord in ((36.0, (62.0, 63.0, 68.4)), (50.0, (61.0, 62.3, 67.0, 72.6)), (62.0, (63.0, 64.2, 69.5, 70.3, 76.0)), (72.0, (60.5, 61.6, 66.9, 73.1))):
        for m in chord:
            mix.add("cluster", s.glide_tone(m, m + rng.uniform(-0.6, 0.6), 13.0, vel=0.2, seed=int(m * 10 + when), nmax=8, attack=3.5, release=4.0), when)
    # Now and then something cries out, far away and high up.
    for when, m0, m1 in ((20.0, 88.0, 83.0), (55.0, 90.0, 84.5), (70.0, 86.0, 92.0), (84.0, 89.0, 85.0)):
        mix.add("high", s.glide_tone(m0, m1, 3.2, vel=0.1, seed=int(when), nmax=4, attack=0.5, release=1.0), when)
    mix.automate("drone", [(0, 0.8), (40, 0.95), (66, 1.1), (90, 0.8)])
    return mix.render(4.2, 0.2, -19.0, False, seed=5, hp_hz=40.0)


def flood_battle():
    sc = Score(96, 4, 36)
    mix = Mix(sc.seconds, {"drone": 0.3, "cluster": 0.5, "pulse": 0.18, "wet": 0.22, "growl": 0.3, "high": 0.7, "low": 0.3})
    rng = np.random.default_rng(12)
    # An uneven pulse: 3 + 3 + 2 eighths, heavy and wet, with something tearing above it from bar 12.
    cell = [(0, 1.0), (1.5, 0.75), (3, 0.9), (3.5, 0.55)]
    for bar in range(36):
        for beat, vel in cell:
            mix.add("pulse", note("taiko", 31, vel=vel * (0.85 if bar < 12 else 1.0), variants=3), sc.t(bar, beat))
            if vel > 0.8:
                mix.add("pulse", note("heart", vel=0.75, variants=3), sc.t(bar, beat), 0.8)
        if bar >= 12 and not 24 <= bar < 28:
            for beat in (0.75, 2.25, 2.75, 3.75):
                y = s.squelch(0.28, rng.uniform(900.0, 1800.0), rng.uniform(150.0, 320.0), seed=bar * 7 + int(beat * 4))
                mix.add("wet", y.astype(np.float32) * 0.75, sc.t(bar, beat), pan=rng.uniform(-0.6, 0.6))
    # The drones thicken section by section.
    for k in range(0, 36, 4):
        base = 26.0 + (0.0, 0.4, -0.5, 1.0, 0.7, -0.3, 1.3, 0.2, 1.8)[k // 4]
        length = 4 * sc.bpb * sc.beat + 1.5
        mix.add("drone", s.glide_tone(base, base + rng.uniform(-0.5, 0.5), length, vel=0.75, seed=k, nmax=16, attack=1.0, release=1.5, rough=0.35), sc.t(k))
        mix.add("drone", s.glide_tone(base + 1.0, base + 0.5, length, vel=0.6, seed=k + 100, nmax=16, attack=1.0, release=1.5, rough=0.35), sc.t(k))
    # A throat in the dark.
    for bar, m in ((8, 34), (12, 35), (16, 33), (20, 36), (28, 35), (30, 34), (32, 37), (34, 38)):
        mix.add("growl", s.growl(m, 2 * sc.bpb * sc.beat * 0.9, vel=0.7, seed=bar).astype(np.float32), sc.t(bar), pan=rng.uniform(-0.4, 0.4))
    # Falling low tones, from bar 4.
    for bar in range(4, 36, 3):
        m0 = 52.0 + rng.uniform(-1.5, 1.5)
        mix.add("low", s.glide_tone(m0, m0 - rng.uniform(3.0, 6.0), 2.5 * sc.bpb * sc.beat, vel=0.55, seed=bar, nmax=18, attack=0.3, release=0.8, bend="ease", rough=0.3), sc.t(bar))
    # Screaming clusters: only in the hard sections.
    for bar in (16, 20, 28, 32):
        for m in (79.0, 80.1, 85.6, 86.4):
            mix.add("cluster", s.glide_tone(m, m + rng.uniform(-2.0, 2.0), 4 * sc.bpb * sc.beat, vel=0.15, seed=int(m * 10) + bar, nmax=6, attack=1.5, release=1.5, rough=0.5), sc.t(bar))
    # The middle: the pulse falls back, the wet things come forward.
    for bar in range(24, 28):
        add_bubbles(mix, "wet", sc.t(bar), sc.bpb * sc.beat + 0.5, 60.0, seed=bar, lo=120.0, hi=1100.0, gain=0.8)
    t = sc.t
    mix.automate("pulse", [(0, 0.85), (t(12), 1.0), (t(24), 1.0), (t(24.5), 0.4), (t(27.5), 0.5), (t(28), 1.0), (t(36), 0.85)])
    return mix.render(2.6, 0.2, -18.0, False, seed=6, hp_hz=40.0)


def flood_menu():
    secs = 90.0
    mix = Mix(secs, {"drone": 0.55, "breath": 0.75, "pulse": 0.45, "wet": 0.5, "high": 1.0, "low": 0.6})
    rng = np.random.default_rng(13)
    for k in range(0, 90, 18):
        for m0, m1, seed in ((26.0, 25.6, 1), (32.6, 33.1, 2), (38.5, 38.0, 3)):
            mix.add("drone", s.glide_tone(m0, m1, 23.0, vel=0.42, seed=seed + k + 300, nmax=9, attack=4.0, release=4.0), k)
    for k in range(0, 90, 6):
        y = s.breath_noise(5.4, 120.0 + 40.0 * (k % 12 == 0), 800.0, seed=k + 500) * 0.3
        mix.add("breath", np.stack((y, np.roll(y, 1200)), axis=1).astype(np.float32), k)
    t = 3.0
    while t < secs - 1.0:
        mix.add("pulse", note("heart", vel=0.3 + 0.12 * np.sin(t * 0.21), variants=3), t)
        t += 3.0 if int(t) % 4 else 4.5
    for k in range(0, 90, 15):
        add_bubbles(mix, "wet", k, 16.0, 5.0, seed=k + 700, gain=0.8)
    for when, m0, m1 in ((12.0, 84.0, 80.0), (37.0, 86.5, 83.0), (63.0, 82.0, 87.0), (79.0, 85.0, 81.0)):
        mix.add("high", s.glide_tone(m0, m1, 6.0, vel=0.09, seed=int(when) + 900, nmax=3, attack=1.5, release=2.0), when)
    for when, m0, m1 in ((22.0, 47.0, 44.6), (51.0, 46.0, 42.7), (73.0, 48.0, 45.2)):
        mix.add("low", s.glide_tone(m0, m1, 10.0, vel=0.32, seed=int(when) + 1100, nmax=12, attack=2.0, release=3.0, bend="ease"), when)
    return mix.render(5.0, 0.2, -20.0, False, seed=7, hp_hz=40.0)


# ------------------------------------------------------------------ the main menu: a theme of its own, belonging to no side

MENU_THEME = [
    [(64, 2), (67, 1), (71, 1)],
    [(72, 2), (71, 1), (67, 1)],
    [(69, 1.5), (71, 0.5), (72, 1), (76, 1)],
    [(75, 3), (71, 1)],
    [(76, 2), (74, 1), (71, 1)],
    [(72, 2), (67, 1), (64, 1)],
    [(66, 1), (69, 1), (74, 2)],
    [(71, 4)],
]
MENU_CHORDS = ["Em", "C", "Am", "B", "Em", "C", "D", "B"]


def main_menu():
    sc = Score(64, 4, 24)
    mix = Mix(sc.seconds, {"low": 0.45, "str": 0.6, "choir": 0.85, "horn": 0.55, "tpt": 0.45, "drum": 0.4, "cym": 0.5})
    # Low strings the whole way, moving slowly.
    all_chords = ["Em", "Em", "C", "Em"] + MENU_CHORDS + ["Em", "G", "C", "D", "Am", "B"] + ["Em", "C", "Am", "B"] + ["Em", "B"]
    for bar, chord in enumerate(all_chords):
        root = root_of(chord, 28)
        vel = 0.6 if bar < 12 else 0.65
        mix.add("low", note("strings", root, sc.bpb * sc.beat * 1.03, vel, players=3, nmax=14, attack=0.6, release=0.8, variants=2), sc.t(bar))
        mix.add("low", note("strings", root + 12, sc.bpb * sc.beat * 1.03, vel * 0.85, players=3, nmax=16, attack=0.6, release=0.8, variants=2), sc.t(bar), 0.7)
    # Bars 0 to 3: a far-off choir hanging over the low strings.
    for bar, chord in enumerate(all_chords[:4]):
        for m in voiced(chord, 64, 76):
            mix.add("choir", note("choir", m, sc.bpb * sc.beat + 0.8, 0.6, vowel="oo", to_vowel="ah", attack=1.6, release=1.6, voices=5, nmax=14, variants=2), sc.t(bar), 1.3)
    # Bars 4 to 11: the theme on one horn.
    add_chords(mix, sc, "str", MENU_CHORDS, 4, 59, 72, vel=0.38, players=4, nmax=16, attack=0.5, release=0.8)
    play_section(mix, sc, "horn", "brass", 4, MENU_THEME, vel=0.58, formant=720.0, bright=0.72, players=2)
    # Bars 12 to 17: strings take it over, an octave up, the horns under them, drums and choir growing.
    extra_chords = ["Em", "G", "C", "D", "Am", "B"]
    add_chords(mix, sc, "str", extra_chords, 12, 59, 79, vel=0.55, players=4, nmax=16, attack=0.35, release=0.7)
    second = [(76, 2), (79, 1), (83, 1)], [(83, 2), (81, 1), (79, 1)], [(79, 1.5), (81, 0.5), (84, 1), (88, 1)], [(86, 2), (83, 1), (81, 1)], [(84, 1), (83, 1), (81, 1), (79, 1)], [(78, 2), (75, 1), (71, 1)]
    play_section(mix, sc, "str", "strings", 12, list(second), vel=0.62, players=5, nmax=12, attack=0.12, release=0.4, gain=0.8)
    play_section(mix, sc, "horn", "brass", 12, harmonize(phrase_transposed(list(second), -12), extra_chords), vel=0.6, formant=720.0, bright=0.8, players=3)
    for bar in range(12, 18):
        mix.add("drum", note("timp", 40, vel=0.65, dur=1.8, variants=2), sc.t(bar))
        mix.add("drum", note("timp", 35 + 12 if bar % 2 else 40, vel=0.45, dur=1.5, variants=2), sc.t(bar, 2.5))
        for m in voiced(extra_chords[bar - 12], 60, 72):
            mix.add("choir", note("choir", m, sc.bpb * sc.beat + 0.5, 0.5, vowel="ah", attack=0.8, release=1.2, voices=5, nmax=14, variants=2), sc.t(bar))
    mix.add("cym", s.cymbal_swell(sc.bpb * sc.beat * 2, peak=0.3), sc.t(16))
    # Bars 18 to 21: the top of the hill, brass and everything.
    top = MENU_THEME[:3] + [[(75, 2), (78, 1), (83, 1)]]
    top_chords = ["Em", "C", "Am", "B"]
    add_chords(mix, sc, "str", top_chords, 18, 59, 84, vel=0.72, players=4, nmax=14, attack=0.2, release=0.7)
    play_section(mix, sc, "tpt", "brass", 18, top, vel=0.82, formant=1400.0, bright=1.0, players=3, transpose=12, gain=0.7)
    play_section(mix, sc, "horn", "brass", 18, top, vel=0.85, formant=760.0, bright=0.95, players=4)
    for bar in range(18, 22):
        for beat, vel in ((0, 0.9), (1.5, 0.5), (2, 0.65), (3, 0.5), (3.5, 0.65)):
            mix.add("drum", note("timp", 40 if beat != 2 else 47, vel=vel, dur=1.5, variants=2), sc.t(bar, beat))
        for m in voiced(top_chords[bar - 18], 60, 76):
            mix.add("choir", note("choir", m, sc.bpb * sc.beat + 0.5, 0.65, vowel="ah", attack=0.5, release=1.2, voices=5, nmax=14, variants=2), sc.t(bar))
    mix.add("cym", s.crash(0.5, dur=3.0), sc.t(18))
    # Bars 22 and 23: letting go, back to the beginning.
    play_phrase(mix, sc, "horn", "brass", 22, [(76, 2), (71, 2), (67, 2), (66, 2)], vel=0.5, formant=720.0, bright=0.7, players=2)
    for m in (71, 76, 79):
        mix.add("choir", note("choir", m, 2 * sc.bpb * sc.beat, 0.42, vowel="ah", to_vowel="oo", attack=1.0, release=2.0, voices=5, nmax=14, variants=2), sc.t(22))
    t = sc.t
    mix.automate("low", [(0, 1.2), (t(12), 1.0), (t(18), 1.1), (t(22), 1.0), (t(24), 1.2)])
    return mix.render(3.6, 0.5, -19.0, True, seed=8)


# ------------------------------------------------------------------ stingers

def finish_stinger(mix, seconds, hall, bright, seed):
    dry = sum(mix.buses[name] for name in mix.sends)
    wet = sum(mix.buses[name] * mix.sends[name] for name in mix.sends)
    x = dry.astype(np.float64) + s.convolve_circular(wet.astype(np.float64), s.make_ir(hall, bright=bright, seed=seed))
    x = x.mean(axis=1)
    x = s.fft_filter(x, lambda f: s.hp(f, 30.0, 2))
    x = x - np.mean(x)
    x = s.fade_out(x, 0.6)
    return master(x, -16.0)


def stinger(name):
    seconds = 7.0
    sc = Score(96, 4, 4)
    mix = Mix(seconds + 3.0, {"a": 0.4, "b": 0.5, "c": 0.3, "d": 0.2})
    if name == "unsc_victory":
        # Rising brass, ending on a bright, open D major.
        phrase = [(62, 0.5), (65, 0.5), (69, 1), (74, 1), (72, 0.5), (74, 0.5), (78, 4)]
        play_phrase(mix, sc, "a", "brass", 0, phrase, vel=0.9, formant=1500.0, bright=1.1, players=3)
        play_phrase(mix, sc, "a", "brass", 0, [(i[0] - 12, i[1]) for i in phrase], vel=0.85, formant=800.0, bright=0.9, players=3)
        for m in (50, 57, 62, 66, 69):
            mix.add("b", note("brass", m, 2.6, 0.85, formant=800.0, players=3), sc.t(0, 4))
            mix.add("b", note("strings", m + 12, 2.8, 0.7, players=4, nmax=16, attack=0.1, release=1.0), sc.t(0, 4))
        mix.add("c", s.snare_roll(sc.t(0, 4), 0.3, 0.9), 0.0, 0.8)
        mix.add("d", note("timp", 38, vel=1.0, dur=2.2, variants=1), sc.t(0, 4))
        mix.add("c", s.crash(0.6, dur=3.0), sc.t(0, 4))
    elif name == "unsc_defeat":
        phrase = [(62, 2), (60, 1), (58, 1), (57, 2), (53, 1), (50, 3)]
        play_phrase(mix, sc, "a", "brass", 0, phrase, vel=0.55, formant=700.0, bright=0.65, players=2)
        for i, chord in enumerate(("Dm", "Bb", "Gm", "Dm")):
            for m in voiced(chord, 45, 62):
                mix.add("b", note("strings", m, 2.5 if i < 3 else 3.0, 0.42, players=4, nmax=14, attack=0.3, release=1.2), i * 1.25)
        for when, vel in ((0.0, 0.75), (1.25, 0.6), (2.5, 0.5), (3.75, 0.7)):
            mix.add("d", note("timp", 38, vel=vel, dur=2.0, variants=2), when)
        mix.add("c", s.snare_roll(0.6, 0.25, 0.1), 3.1, 0.5)
    elif name == "covenant_victory":
        for k, (shift, vel) in enumerate(((0, 0.6), (1, 0.75))):
            for m in COV_CHORDS["X1" if k == 0 else "X3"]:
                mix.add("a", note("choir", m + 12, 2.2 if k == 0 else 4.0, vel, vowel="oh", to_vowel="ah", attack=0.7, release=1.5, voices=5, nmax=18, variants=2), k * 1.8)
        for m in (38, 50, 57, 62, 69, 74):
            mix.add("a", note("choir", m + 12, 3.0, 0.85, vowel="ah", attack=0.25, release=2.0, voices=5, nmax=18, variants=2), 3.3)
        for i, m in enumerate((81, 82, 86, 87, 90, 93, 98)):
            mix.add("b", note("glass", m, 2.8, 0.45, variants=2), 2.6 + i * 0.13, pan=0.6 * np.sin(i * 1.4))
        mix.add("b", note("gong", vel=0.8, variants=1), 3.3)
        for when, vel in ((1.8, 0.7), (2.6, 0.8), (3.0, 0.85), (3.3, 1.0)):
            mix.add("d", note("taiko", 33, vel=vel, variants=2), when)
    elif name == "covenant_defeat":
        for m, drop in ((50, 2.5), (57, 3.5), (63, 6.0), (69, 8.5)):
            y = s.glide_tone(m + 12, m + 12 - drop, 6.0, vel=0.35, seed=m, nmax=14, attack=0.5, release=2.0, bend="ease")
            mix.add("a", y.astype(np.float32), 0.0)
            mix.add("a", note("choir", m, 2.6, 0.5, vowel="ah", to_vowel="mm", attack=0.3, release=1.6, voices=5, nmax=16, variants=1), 0.0)
        for i, m in enumerate((90, 87, 86, 82, 81, 78)):
            mix.add("b", note("glass", m, 2.4, 0.32, variants=2), 0.3 + i * 0.5, pan=-0.5 + i * 0.2)
        mix.add("d", note("taiko", 33, vel=0.9, variants=1), 0.0)
        mix.add("d", note("taiko", 31, vel=0.7, variants=1), 2.2)
        mix.add("d", note("taiko", 29, vel=0.5, variants=1), 4.2)
        mix.add("b", note("gong", vel=0.5, variants=1), 0.0)
    elif name == "flood_victory":
        for m0, m1 in ((26.0, 26.0), (27.0, 33.0), (45.0, 38.0), (63.0, 50.0), (75.0, 57.0)):
            mix.add("a", s.glide_tone(m0, m1, 6.5, vel=0.5, seed=int(m0), nmax=14, attack=1.0, release=2.0, bend="ease", rough=0.25).astype(np.float32), 0.0)
        for when in (0.0, 1.1, 2.0, 2.7, 3.2, 3.6, 4.0, 4.3):
            mix.add("d", note("heart", vel=0.8, variants=3), when)
        mix.add("c", s.growl(36, 3.6, vel=0.9, seed=1).astype(np.float32), 2.2)
        y = s.bubbles(6.0, 45.0, 90.0, 1100.0, seed=3, tau=0.035, gain=0.5)
        mix.add("b", np.stack((y, np.roll(y, 1300)), axis=1).astype(np.float32), 0.5)
    else:  # flood_defeat
        for m0, m1 in ((33.0, 21.0), (39.0, 26.0), (57.0, 39.0)):
            mix.add("a", s.glide_tone(m0, m1, 5.5, vel=0.5, seed=int(m0), nmax=12, attack=0.2, release=2.2, bend="ease", rough=0.3).astype(np.float32), 0.0)
        pos = 0.0
        vel = 0.9
        gap = 0.9
        while pos < 5.5:
            mix.add("d", note("heart", vel=vel, variants=3), pos)
            pos += gap
            gap *= 1.35
            vel *= 0.78
        y = s.bubbles(4.5, 26.0, 70.0, 420.0, seed=5, tau=0.05, gain=0.6)
        y *= np.linspace(1.0, 0.1, len(y))
        mix.add("b", np.stack((y, np.roll(y, 900)), axis=1).astype(np.float32), 0.3)
        mix.add("c", s.squelch(1.4, 900.0, 80.0, seed=9).astype(np.float32) * 0.9, 0.2)
    x = finish_stinger(mix, seconds, 2.6, 0.45, seed=9)
    return x[:samples(seconds + 1.0)]


# ------------------------------------------------------------------ main

TRACKS = {
    "music_mainmenu": main_menu,
    "music_unsc_menu": unsc_menu,
    "music_unsc_deployment": unsc_deployment,
    "music_unsc_battle": unsc_battle,
    "music_covenant_menu": covenant_menu,
    "music_covenant_deployment": covenant_deployment,
    "music_covenant_battle": covenant_battle,
    "music_flood_menu": flood_menu,
    "music_flood_deployment": flood_deployment,
    "music_flood_battle": flood_battle,
}
for _name in ("unsc_victory", "unsc_defeat", "covenant_victory", "covenant_defeat", "flood_victory", "flood_defeat"):
    TRACKS["stinger_" + _name] = functools.partial(stinger, _name)


def run(name):
    x = TRACKS[name]()
    if name.startswith("stinger"):
        x = s.fade_out(x, 0.3)
    s.write_wav(os.path.join(OUT, name + ".wav"), x)
    return name, len(x) / s.SR, x.ndim


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    names = sys.argv[1:] or list(TRACKS)
    with Pool(min(10, len(names))) as pool:
        for track, seconds, dims in pool.imap_unordered(run, names):
            print(f"{track}: {seconds:.2f} s, {'stereo' if dims == 2 else 'mono'}", flush=True)
