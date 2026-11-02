# make_sfx.py
# Designs and renders the placeholder sound effects and UI sounds of Halobellum (M10). All of it is synthesized here;
# nothing is read from any recording. Weapons follow their faction: UNSC ballistic and punchy, Covenant plasma and
# zaps, Flood wet and organic. The UI sets differ the same way: UNSC crisp clicks and data beeps, Covenant resonant
# chimes, Flood wet squelches.
# Each sound is brought to a set loudness, by its loudest moment, so that the sets match each other.
# Usage: python make_sfx.py [name prefix ...]    (no names: all). Output: Assets/_Project/Audio/Generated/Resources/GeneratedAudio.
# Depends on: numpy, synth.py.

import os
import sys
from multiprocessing import Pool

import numpy as np

import synth as s
from synth import samples

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Project", "Audio", "Generated", "Resources", "GeneratedAudio")


def R(seed):
    return np.random.default_rng(seed)


def mix(n, *parts):
    """Adds sounds of any length into one of n samples."""
    out = np.zeros(n)
    for part in parts:
        when, y = part if isinstance(part, tuple) else (0.0, part)
        i = samples(when)
        m = min(len(y), n - i)
        if m > 0:
            out[i:i + m] += y[:m]
    return out


def band_noise(n, rng, lo=None, hi=None, order=2):
    x = s.noise(n, rng)
    return s.fft_filter(x, lambda f: (s.hp(f, lo, order) if lo else 1.0) * (s.lp(f, hi, order) if hi else 1.0))


def thump(n, f_start, f_end, tau, curve=30.0):
    return s.sine_sweep(n, f_start, f_end, curve) * s.env_exp(n, tau, 0.0008)


def small_room(x, seconds=0.5, amount=0.18, seed=3):
    """A short slap of room around a dry sound, folded back to mono."""
    ir = s.make_ir(seconds, bright=0.4, seed=seed, predelay=0.006)
    wet = s.convolve_tail(x, ir).mean(axis=1)
    out = np.zeros(len(wet))
    out[:len(x)] = x
    return out + wet * amount * 5.0


def metal(n, rng, freqs, taus, gain=1.0):
    y = np.zeros(n)
    t = s.time_axis(n)
    for f, tau in zip(freqs, taus):
        y += np.sin(2 * np.pi * f * (1 + rng.uniform(-0.01, 0.01)) * t + rng.uniform(0, 6.28)) * np.exp(-t / tau)
    return y * gain / np.sqrt(len(freqs))


# ------------------------------------------------------------------ UNSC weapons: ballistic and punchy

def gunshot(seed, dur=0.26, crack=1.0, crack_hp=2600.0, body=1.0, body_lp=3000.0, body_tau=0.03, f0=210.0, f1=78.0, thump_gain=0.9, thump_tau=0.035, tail=0.35, tail_tau=0.075, drive=1.6):
    rng = R(seed)
    n = samples(dur)
    t = s.time_axis(n)
    c = band_noise(n, rng, crack_hp, None, 1) * np.exp(-t / 0.0028) * crack
    b = band_noise(n, rng, 180.0, body_lp, 2) * s.env_exp(n, body_tau, 0.0006) * body
    k = thump(n, f0 * rng.uniform(0.94, 1.06), f1, thump_tau) * thump_gain
    tl = band_noise(n, rng, 350.0, 1700.0, 2) * s.env_exp(n, tail_tau, 0.004) * tail
    return s.fade_out(s.soft_clip(c + b + k + tl, drive), 0.02)


def fire_bullet(seed):
    return gunshot(seed, dur=0.22)


def fire_bullet_flood(seed):
    # A scavenged rifle in wet hands: the same shot, duller, with a slap of something soft.
    rng = R(seed + 1)
    y = gunshot(seed, dur=0.24, crack=0.5, crack_hp=1900.0, body_lp=1700.0, tail=0.25, drive=1.3)
    return mix(len(y), y, (0.008, s.squelch(0.13, 900.0, 250.0, seed=seed) * 0.32))


def fire_heavybullet(seed):
    rng = R(seed + 5)
    y = gunshot(seed, dur=0.3, crack=1.1, crack_hp=2000.0, body_lp=2400.0, body_tau=0.042, f0=170.0, f1=58.0, thump_gain=1.25, thump_tau=0.055, tail=0.5, tail_tau=0.1, drive=1.9)
    # The clack of the action.
    action = metal(samples(0.03), rng, (2300.0, 3400.0, 5100.0), (0.006, 0.005, 0.004), 0.3)
    return mix(len(y), y, (0.035, action))


def fire_cannon(seed):
    rng = R(seed)
    n = samples(1.1)
    t = s.time_axis(n)
    boom = thump(n, 170.0, 34.0, 0.3, 16.0) * 1.4
    blast = band_noise(n, rng, 60.0, 3500.0, 2) * s.env_exp(n, 0.07, 0.001)
    crack = band_noise(n, rng, 1800.0, None, 1) * np.exp(-t / 0.004) * 1.2
    ring = metal(n, rng, (780.0, 1140.0, 1810.0, 2470.0), (0.09, 0.07, 0.05, 0.04), 0.3)
    rumble = band_noise(n, rng, 30.0, 260.0, 3) * s.env_exp(n, 0.35, 0.02) * 1.2
    return s.fade_out(s.soft_clip(boom + blast + crack + ring + rumble, 2.1), 0.15)


def fire_gauss(seed):
    rng = R(seed)
    n = samples(1.0)
    t = s.time_axis(n)
    # A very fast rising electric snap, then a hard metallic strike.
    pre = samples(0.05)
    rise = np.zeros(n)
    rise[:pre] = np.sin(s.phase_of(np.linspace(500.0, 6500.0, pre))) * np.linspace(0.1, 0.8, pre)
    strike_at = pre
    m = n - strike_at
    tt = t[:m]
    crack = band_noise(m, rng, 1500.0, None, 1) * np.exp(-tt / 0.006) * 1.5
    boom = thump(m, 150.0, 45.0, 0.2, 18.0) * 1.1
    ring = metal(m, rng, (1320.0, 1985.0, 2755.0, 4120.0, 5230.0), (0.28, 0.2, 0.16, 0.1, 0.07), 0.55)
    hiss = band_noise(m, rng, 3000.0, 9000.0, 2) * np.exp(-tt / 0.16) * 0.25
    return s.fade_out(mix(n, rise, (0.05, s.soft_clip(crack + boom + ring + hiss, 1.7))), 0.1)


def fire_rocket(seed):
    rng = R(seed)
    n = samples(0.95)
    t = s.time_axis(n)
    foomp = thump(n, 140.0, 55.0, 0.09, 14.0) + band_noise(n, rng, 80.0, 900.0, 2) * s.env_exp(n, 0.06, 0.001) * 0.9
    cut = np.interp(t, (0.0, 0.1, 0.9), (1400.0, 3600.0, 900.0))
    hiss = s.svf(s.noise(n, rng), cut, q=1.0, mode="bp") * 0.75 * s.env_adsr(n, 0.03, 0.25, 0.4, 0.5)
    return s.fade_out(s.soft_clip(foomp + hiss, 1.4), 0.1)


def fire_bomb(seed):
    # Released from high up: a short falling whistle, then the thud of the drop.
    rng = R(seed)
    n = samples(0.8)
    t = s.time_axis(n)
    whistle = np.sin(s.phase_of(np.interp(t, (0.0, 0.5), (1700.0, 620.0)))) * s.env_adsr(n, 0.04, 0.0, 1.0, 0.25) * 0.28
    wind = s.svf(s.noise(n, rng), np.interp(t, (0.0, 0.5), (2600.0, 700.0)), q=1.3, mode="bp") * s.env_adsr(n, 0.05, 0.0, 1.0, 0.3) * 0.4
    drop = thump(samples(0.3), 95.0, 38.0, 0.09, 18.0) * 1.3
    return s.fade_out(mix(n, whistle, wind, (0.42, drop)), 0.05)


def fire_flame(seed):
    rng = R(seed)
    n = samples(0.7)
    t = s.time_axis(n)
    body = band_noise(n, rng, 180.0, 2600.0, 2)
    flutter = 0.65 + 0.35 * np.interp(t, np.arange(0, 0.75, 0.018), rng.uniform(0.0, 1.0, len(np.arange(0, 0.75, 0.018))))
    roar = band_noise(n, rng, 40.0, 240.0, 2) * 1.3
    env = s.env_adsr(n, 0.03, 0.12, 0.8, 0.28)
    return s.fade_out(s.soft_clip((body * flutter + roar) * env, 1.5), 0.05)


def fire_laser(seed):
    # The Spartan Laser: one hard white line of sound.
    rng = R(seed)
    n = samples(0.95)
    t = s.time_axis(n)
    f = 1500.0 + 2600.0 * np.exp(-t / 0.05)
    y = np.zeros(n)
    phase = s.phase_of(f)
    for h, g in ((1, 1.0), (2, 0.6), (3, 0.4), (5, 0.22)):
        y += g * np.sin(h * phase)
    body = np.sin(s.phase_of(np.full(n, 95.0))) * 1.1 + np.sin(s.phase_of(np.full(n, 143.0))) * 0.5
    e = s.env_exp(n, 0.28, 0.002)
    crack = band_noise(n, rng, 2500.0, None, 1) * np.exp(-t / 0.006) * 0.9
    return s.fade_out(s.soft_clip((y * 0.42 + body * 0.75) * e + crack, 1.8), 0.12)


# ------------------------------------------------------------------ Covenant weapons: plasma and zaps

def fire_plasma(seed):
    rng = R(seed)
    n = samples(0.3)
    t = s.time_axis(n)
    top = rng.uniform(1650.0, 2150.0)
    f = 240.0 + top * np.exp(-t / 0.045)
    # FM gives the buzzy, liquid edge of a bolt.
    mod = np.sin(s.phase_of(f * 2.41)) * 2.2 * np.exp(-t / 0.08)
    y = np.sin(s.phase_of(f) + mod)
    fizz = band_noise(n, rng, 2200.0, 8000.0, 2) * s.env_exp(n, 0.05, 0.001) * 0.32
    low = np.sin(s.phase_of(90.0 + 170.0 * np.exp(-t / 0.03))) * s.env_exp(n, 0.06) * 0.8
    return s.fade_out(s.soft_clip((y * s.env_exp(n, 0.085, 0.0012) * 0.9 + fizz + low), 1.5), 0.02)


def fire_plasmaheavy(seed):
    rng = R(seed)
    n = samples(1.05)
    t = s.time_axis(n)
    f = 58.0 + 620.0 * np.exp(-t / 0.13)
    mod = np.sin(s.phase_of(f * 1.51)) * 3.0 * np.exp(-t / 0.3)
    y = np.sin(s.phase_of(f) + mod) * s.env_exp(n, 0.3, 0.003)
    sub = np.sin(s.phase_of(np.full(n, 46.0))) * s.env_exp(n, 0.28, 0.006) * 1.0
    sizzle = s.svf(s.noise(n, rng), np.interp(t, (0, 1.0), (5200.0, 900.0)), q=1.4, mode="bp") * s.env_exp(n, 0.26, 0.004) * 0.45
    return s.fade_out(s.soft_clip(y + sub + sizzle, 1.9), 0.12)


def fire_needle(seed):
    # A shard of crystal let go: a high glassy ping with a thin zip behind it.
    rng = R(seed)
    n = samples(0.24)
    t = s.time_axis(n)
    base = rng.uniform(2700.0, 3600.0)
    y = np.zeros(n)
    for ratio, gain, tau in ((1.0, 1.0, 0.05), (1.87, 0.6, 0.035), (2.91, 0.35, 0.024)):
        y += gain * np.sin(s.phase_of(base * ratio * (1.0 + 0.5 * np.exp(-t / 0.012)))) * np.exp(-t / tau)
    zip_ = np.sin(s.phase_of(1200.0 + 5400.0 * np.exp(-t / 0.018))) * np.exp(-t / 0.022) * 0.5
    low = np.sin(s.phase_of(np.full(n, 310.0))) * s.env_exp(n, 0.02) * 0.6
    return s.fade_out(y * 0.55 + zip_ + low, 0.02)


def fire_beam(seed):
    # The Scarab's focus beam: two seconds of charging, then the beam itself (the battle has it charge for 2 s and fire for 3).
    rng = R(seed)
    charge, fire = 2.0, 3.0
    n = samples(charge + fire + 0.5)
    t = s.time_axis(n)
    pos = np.clip(t / charge, 0.0, 1.0)
    pitch = 70.0 + 500.0 * pos ** 2.2
    whine = np.sin(s.phase_of(pitch)) + 0.5 * np.sin(s.phase_of(pitch * 2.01))
    whine *= (pos ** 1.5 * 0.55) * (t < charge + 0.02)
    firing = t >= charge
    level = np.where(firing, np.minimum((t - charge) / 0.03, 1.0) * np.minimum((charge + fire + 0.5 - t) / 0.45, 1.0), 0.0)
    y = np.zeros(n)
    phase = s.phase_of(np.full(n, 82.0) * (1.0 + 0.012 * np.sin(2 * np.pi * 7.0 * t)))
    for h in range(1, 20):
        y += h ** -0.8 * np.sin(h * phase + rng.uniform(0, 6.28))
    roar = band_noise(n, rng, 200.0, 5200.0, 2) * 0.9
    sub = np.sin(s.phase_of(np.full(n, 41.0))) * 1.0
    return s.fade_out(s.soft_clip(whine + (y * 0.4 + roar + sub) * level, 1.6), 0.05)


def fire_melee(seed):
    # A heavy swing and a dull hit.
    rng = R(seed)
    n = samples(0.32)
    t = s.time_axis(n)
    cut = np.interp(t, (0.0, 0.12, 0.25), (500.0, 2400.0, 700.0))
    swish = s.svf(s.noise(n, rng), cut, q=1.5, mode="bp") * s.env_adsr(n, 0.05, 0.05, 0.6, 0.1) * 0.7
    hit = thump(samples(0.16), 180.0, 70.0, 0.04, 26.0) * 1.2
    return s.fade_out(mix(n, swish, (0.12, hit)), 0.03)


def fire_melee_covenant(seed):
    # An energy blade: a buzz that bends with the swing.
    rng = R(seed)
    n = samples(0.42)
    t = s.time_axis(n)
    swing = np.interp(t, (0.0, 0.15, 0.38), (0.75, 1.5, 0.9))
    y = np.zeros(n)
    phase = s.phase_of(118.0 * swing + 9.0 * np.sin(2 * np.pi * 42.0 * t))
    for h, g in ((1, 1.0), (2, 0.8), (3, 0.5), (4, 0.3), (6, 0.2), (8, 0.13)):
        y += g * np.sin(h * phase)
    air = s.svf(s.noise(n, rng), 1300.0 * swing, q=1.2, mode="bp") * 0.55
    return s.fade_out((y * 0.42 + air) * s.env_adsr(n, 0.04, 0.08, 0.65, 0.16), 0.03)


def fire_melee_flood(seed):
    rng = R(seed)
    n = samples(0.34)
    t = s.time_axis(n)
    swish = s.svf(s.noise(n, rng), np.interp(t, (0.0, 0.1, 0.22), (300.0, 1700.0, 500.0)), q=1.1, mode="bp") * s.env_adsr(n, 0.04, 0.04, 0.5, 0.1) * 0.4
    wet = s.squelch(0.22, rng.uniform(1100.0, 1900.0), rng.uniform(170.0, 300.0), q=2.2, seed=seed)
    return s.fade_out(mix(n, swish, (0.08, wet * 1.1), (0.08, thump(samples(0.12), 130.0, 60.0, 0.04, 26.0) * 0.8)), 0.03)


def fire_bio(seed):
    # Spat bone and spores: a wet cough with a hard little thing in it.
    rng = R(seed)
    n = samples(0.32)
    t = s.time_axis(n)
    wet = s.squelch(0.3, rng.uniform(1300.0, 2100.0), rng.uniform(220.0, 340.0), q=2.8, seed=seed, density=70.0)
    cough = band_noise(n, rng, 180.0, 1500.0, 2) * s.env_exp(n, 0.03, 0.0015) * 0.7
    tick = metal(samples(0.03), rng, (2100.0, 2900.0), (0.008, 0.005), 0.3)
    return s.fade_out(mix(n, wet, cough, tick), 0.02)


# ------------------------------------------------------------------ impacts

def impact_flesh(seed):
    rng = R(seed)
    n = samples(0.1)
    t = s.time_axis(n)
    return s.fade_out(band_noise(n, rng, 240.0, 2100.0, 2) * s.env_exp(n, 0.016, 0.0006) + thump(n, 190.0, 95.0, 0.018) * 0.9, 0.01)


def impact_armor(seed):
    rng = R(seed)
    n = samples(0.2)
    t = s.time_axis(n)
    scale = rng.uniform(0.85, 1.25)
    ring = metal(n, rng, np.array((1720.0, 2630.0, 3840.0, 5400.0)) * scale, (0.05, 0.035, 0.025, 0.02), 1.2)
    tick = band_noise(n, rng, 2800.0, None, 1) * np.exp(-t / 0.002)
    return s.fade_out(ring + tick, 0.02)


def impact_shield(seed):
    # An energy shield takes the hit: a quick glassy sizzle with a downward bend.
    rng = R(seed)
    n = samples(0.24)
    t = s.time_axis(n)
    f = rng.uniform(1500.0, 2300.0) * (0.55 + 0.45 * np.exp(-t / 0.05))
    y = np.sin(s.phase_of(f) + 1.8 * np.sin(s.phase_of(f * 1.41))) * s.env_exp(n, 0.06, 0.001)
    sizzle = band_noise(n, rng, 3400.0, 9500.0, 2) * s.env_exp(n, 0.07, 0.001) * 0.55
    return s.fade_out(y * 0.8 + sizzle, 0.02)


def impact_block(seed):
    # A shot that bounced off a held shield.
    rng = R(seed)
    n = samples(0.3)
    t = s.time_axis(n)
    f = rng.uniform(3000.0, 3800.0) * (0.72 + 0.28 * np.exp(-t / 0.11))
    ping = np.sin(s.phase_of(f)) * s.env_exp(n, 0.1, 0.001)
    return s.fade_out(mix(n, ping * 0.7, impact_shield(seed + 9) * 0.6), 0.02)


def impact_wet(seed):
    rng = R(seed)
    return s.fade_out(s.squelch(0.14, rng.uniform(700.0, 1500.0), rng.uniform(180.0, 300.0), seed=seed, density=70.0) + thump(samples(0.14), 150.0, 70.0, 0.025) * 0.55, 0.01)


# ------------------------------------------------------------------ explosions and deaths

def explosion(seed, size=1.0, dur=1.2, debris=10, metal_gain=0.0):
    rng = R(seed)
    n = samples(dur)
    t = s.time_axis(n)
    crack = band_noise(n, rng, 1400.0, None, 1) * np.exp(-t / (0.005 * size)) * 1.2
    boom = thump(n, 135.0 / size ** 0.4, 28.0 + 12.0 / size, 0.22 * size, 14.0) * 1.5
    cut = 240.0 + (5200.0 / size ** 0.5) * np.exp(-t / (0.09 * size))
    blast = s.svf(s.noise(n, rng), cut, q=0.8, mode="lp") * s.env_exp(n, 0.16 * size, 0.001) * 1.3
    rumble = band_noise(n, rng, 25.0, 170.0, 3) * s.env_exp(n, 0.4 * size, 0.03) * 1.3
    y = crack + boom + blast + rumble
    for _ in range(debris):
        when = rng.uniform(0.08, dur * 0.65)
        piece = band_noise(samples(0.03), rng, 900.0, 6000.0, 2) * s.env_exp(samples(0.03), 0.006) * rng.uniform(0.1, 0.35)
        y = mix(n, y, (when, piece))
    if metal_gain > 0:
        y = y + metal(n, rng, (620.0, 910.0, 1430.0, 2100.0), (0.18, 0.14, 0.1, 0.08), metal_gain)
    return s.fade_out(s.soft_clip(y, 2.2), dur * 0.2)


def explosion_small(seed):
    return explosion(seed, 0.55, 0.65, 5)


def explosion_medium(seed):
    return explosion(seed, 1.0, 1.25, 12, 0.25)


def explosion_large(seed):
    return small_room(explosion(seed, 1.8, 2.2, 22, 0.3), 0.8, 0.12)


def death_infantry(seed):
    # A short breathy cry, not a word, and the fall.
    rng = R(seed)
    n = samples(0.42)
    t = s.time_axis(n)
    f0 = rng.uniform(150.0, 215.0)
    f = f0 * np.interp(t, (0.0, 0.05, 0.22), (1.25, 1.1, 0.72))
    phase = s.phase_of(f)
    y = np.zeros(n)
    for h in range(1, 18):
        hz = h * f0
        gain = np.exp(-0.5 * ((hz - 700.0) / 150.0) ** 2) + 0.6 * np.exp(-0.5 * ((hz - 1250.0) / 170.0) ** 2) + 0.05
        y += gain * np.sin(h * phase)
    cry = y * s.env_adsr(n, 0.02, 0.05, 0.55, 0.16) * (t < 0.26) * 0.5
    fall = thump(samples(0.16), 120.0, 62.0, 0.05, 20.0) * 0.85
    gear = band_noise(samples(0.08), rng, 800.0, 4500.0, 2) * s.env_exp(samples(0.08), 0.018) * 0.35
    return s.fade_out(mix(n, cry, (0.24, fall), (0.24, gear)), 0.03)


def death_infantry_covenant(seed):
    # A shrill alien squeal for the small ones, then the fall.
    rng = R(seed)
    n = samples(0.4)
    t = s.time_axis(n)
    f = rng.uniform(620.0, 880.0) * np.interp(t, (0.0, 0.05, 0.24), (0.82, 1.28, 0.62))
    phase = s.phase_of(f + 40.0 * np.sin(2 * np.pi * 31.0 * t))
    y = np.sin(phase) + 0.5 * np.sin(2 * phase) + 0.25 * np.sin(3 * phase)
    cry = y * s.env_adsr(n, 0.012, 0.06, 0.6, 0.13) * (t < 0.26) * 0.45
    return s.fade_out(mix(n, cry, (0.24, thump(samples(0.13), 130.0, 70.0, 0.04, 22.0) * 0.8)), 0.03)


def death_flood(seed):
    rng = R(seed)
    n = samples(0.55)
    splat = s.squelch(0.42, rng.uniform(1100.0, 2000.0), rng.uniform(130.0, 240.0), q=2.2, seed=seed, density=90.0) * 1.2
    thud = thump(samples(0.2), 120.0, 52.0, 0.06, 22.0)
    drip = s.bubbles(0.4, 28.0, 240.0, 1100.0, seed=seed, tau=0.02, gain=0.35)
    return s.fade_out(mix(n, splat, thud, (0.12, drip)), 0.04)


def flood_burst(seed):
    # A carrier form going off: it swells for an instant, then tears.
    rng = R(seed)
    n = samples(0.8)
    swell = s.bubbles(0.1, 180.0, 260.0, 1300.0, seed=seed, tau=0.012, gain=0.35)
    tear = band_noise(n, rng, 180.0, 3800.0, 2) * s.env_exp(n, 0.045, 0.0008) * 1.4
    boom = thump(n, 150.0, 44.0, 0.12, 18.0) * 1.3
    splat = s.squelch(0.55, 2200.0, 140.0, q=1.8, seed=seed, density=120.0) * 1.1
    rain = s.bubbles(0.55, 45.0, 280.0, 1500.0, seed=seed + 1, tau=0.018, gain=0.4)
    hit = s.soft_clip(mix(n, tear, boom, splat, (0.1, rain)), 1.8)
    return s.fade_out(mix(n, swell, (0.07, hit)), 0.06)


def infection_start(seed):
    # Something finds its way in.
    rng = R(seed)
    n = samples(0.5)
    t = s.time_axis(n)
    worm = s.svf(s.noise(n, rng), 500.0 + 650.0 * np.sin(2 * np.pi * rng.uniform(9.0, 15.0) * t) ** 2, q=5.0, mode="bp") * s.env_adsr(n, 0.05, 0.05, 0.75, 0.15)
    return s.fade_out(worm + s.bubbles(0.5, 55.0, 140.0, 800.0, seed=seed, tau=0.02, gain=0.5), 0.03)


def infection_rise(seed):
    # A corpse gets up: wet tearing, then a low rising moan.
    rng = R(seed)
    n = samples(1.05)
    t = s.time_axis(n)
    f = np.interp(t, (0.0, 0.8), (62.0, 118.0))
    phase = s.phase_of(f + 5.0 * np.sin(2 * np.pi * 24.0 * t))
    moan = np.zeros(n)
    for h in range(1, 24):
        hz = h * 90.0
        moan += (np.exp(-0.5 * ((hz - 480.0) / 180.0) ** 2) + 0.4 * np.exp(-0.5 * ((hz - 1100.0) / 240.0) ** 2) + 0.04) * np.sin(h * phase)
    moan = moan * s.env_adsr(n, 0.25, 0.1, 0.8, 0.35) * 0.65
    tear = s.squelch(0.6, 1500.0, 200.0, q=2.5, seed=seed, density=90.0) * 0.9
    return s.fade_out(mix(n, moan, tear), 0.05)


def death_vehicle(seed):
    rng = R(seed)
    y = explosion(seed, 1.15, 1.45, 16, 0.5)
    clatter = sum(mix(len(y), (rng.uniform(0.3, 1.0), metal(samples(0.12), rng, rng.uniform(800.0, 3600.0, 3), (0.04, 0.03, 0.02), 0.28))) for _ in range(5))
    return s.fade_out(y + clatter, 0.2)


def death_air(seed):
    # Hit, a falling whine, the crash.
    rng = R(seed)
    n = samples(1.9)
    t = s.time_axis(n)
    hit = explosion(seed, 0.6, 0.6, 3)
    f = np.interp(t, (0.1, 1.15), (rng.uniform(800.0, 1050.0), 220.0))
    whine = (np.sin(s.phase_of(f)) + 0.5 * np.sin(s.phase_of(f * 1.5))) * np.where(t < 1.2, np.minimum(t / 0.12, 1.0) * 0.22, 0.0)
    crash = explosion(seed + 1, 1.1, 0.72, 10, 0.35)
    return s.fade_out(mix(n, hit, whine, (1.15, crash * 0.9)), 0.1)


def pelican_dive(seed):
    # The Infected Pelican drops out of the sky: engines screaming down, wet flapping in it.
    rng = R(seed)
    n = samples(1.5)
    t = s.time_axis(n)
    f = np.interp(t, (0.0, 1.4), (340.0, 1050.0))
    wail = (np.sin(s.phase_of(f)) + 0.6 * np.sin(s.phase_of(f * 1.51)) + 0.35 * np.sin(s.phase_of(f * 2.03))) * 0.32
    wind = s.svf(s.noise(n, rng), np.interp(t, (0.0, 1.4), (500.0, 3800.0)), q=0.9, mode="bp") * 0.8
    flap = np.abs(np.sin(2 * np.pi * 15.0 * t)) ** 3
    wet = band_noise(n, rng, 160.0, 900.0, 2) * flap * 0.5
    return s.fade_out((wail + wind + wet) * np.minimum(t / 0.5, 1.0) ** 1.4, 0.03)


def pelican_crash(seed):
    y = explosion(seed, 1.7, 2.1, 22, 0.75)
    return s.fade_out(mix(len(y), y, s.squelch(1.2, 1800.0, 110.0, q=1.8, seed=seed, density=110.0) * 0.8, (0.3, s.bubbles(1.2, 40.0, 180.0, 1000.0, seed=seed, tau=0.025, gain=0.3))), 0.3)


def drop_in(seed):
    # A falling squad: the bought squad takes 0.6 s to land, so the thud is there.
    rng = R(seed)
    n = samples(1.05)
    t = s.time_axis(n)
    whoosh = s.svf(s.noise(n, rng), np.interp(t, (0.0, 0.6), (700.0, 3000.0)), q=1.2, mode="bp") * np.minimum(t / 0.5, 1.0) ** 2 * (t < 0.6) * 0.55
    thud = thump(samples(0.4), 110.0, 42.0, 0.11, 22.0) * 1.3
    dust = band_noise(samples(0.4), rng, 240.0, 2700.0, 2) * s.env_exp(samples(0.4), 0.05, 0.0005) * 0.75
    return s.fade_out(mix(n, whoosh, (0.59, thud), (0.59, dust)), 0.05)


# ------------------------------------------------------------------ spells

def spell_mac(seed):
    # The ship's gun: a far-off crack, the round arriving, and everything going.
    rng = R(seed)
    n = samples(2.6)
    t = s.time_axis(n)
    pre = np.sin(s.phase_of(np.interp(t, (0.0, 0.22), (4200.0, 240.0)))) * np.where(t < 0.24, 0.45 * np.minimum(t / 0.02, 1.0), 0.0)
    wham = explosion(seed, 2.0, 2.3, 22, 0.75)
    return s.fade_out(mix(n, pre, (0.22, wham)), 0.3)


def spell_nuke(seed):
    rng = R(seed)
    n = samples(3.0)
    t = s.time_axis(n)
    flash = band_noise(n, rng, 800.0, None, 1) * np.exp(-t / 0.04) * 1.2
    boom = thump(n, 90.0, 22.0, 0.7, 7.0) * 1.8
    blast = s.svf(s.noise(n, rng), 120.0 + 3200.0 * np.exp(-t / 0.3), q=0.8, mode="lp") * s.env_exp(n, 0.65, 0.002) * 1.4
    roll = band_noise(n, rng, 20.0, 130.0, 3) * s.env_adsr(n, 0.4, 1.2, 0.3, 1.2) * 1.5
    return s.fade_out(s.soft_clip(flash + boom + blast + roll, 2.0), 0.5)


def spell_glassing(seed):
    rng = R(seed)
    n = samples(2.4)
    t = s.time_axis(n)
    tone = sum(g * np.sin(s.phase_of(np.interp(t, (0.0, 0.5, 2.3), (900.0, 300.0, 210.0)) * r)) for r, g in ((1.0, 1.0), (1.5, 0.6), (2.01, 0.5), (3.02, 0.3)))
    sear = band_noise(n, rng, 500.0, 9000.0, 2) * 0.9
    sub = np.sin(s.phase_of(np.full(n, 42.0))) * 1.2
    return s.fade_out(s.soft_clip((tone * 0.35 + sear + sub) * s.env_adsr(n, 0.05, 0.3, 0.75, 0.7), 1.6), 0.15)


def spell_missiles(seed):
    rng = R(seed)
    n = samples(1.6)
    parts = []
    for k in range(6):
        when = k * 0.14 + rng.uniform(-0.02, 0.02)
        parts.append((when, fire_rocket(seed + k)[:samples(0.32)] * 0.55))
        parts.append((when + 0.62, explosion_small(seed + 10 + k) * 0.7))
    return s.fade_out(s.soft_clip(mix(n, *parts), 1.3), 0.1)


def spell_plasma(seed):
    # A plasma round from orbit: a long falling buzz and a burst.
    rng = R(seed)
    n = samples(1.1)
    t = s.time_axis(n)
    f = np.interp(t, (0.0, 0.42), (1900.0, 170.0))
    fall = np.sin(s.phase_of(f) + 2.4 * np.sin(s.phase_of(f * 1.41))) * np.where(t < 0.44, 0.45, 0.0)
    return s.fade_out(mix(n, fall, (0.4, fire_plasmaheavy(seed)[:samples(0.7)] * 1.1), (0.4, explosion(seed, 0.6, 0.6, 3) * 0.7)), 0.08)


def spell_storm_tick(seed):
    rng = R(seed)
    n = samples(0.5)
    zap = fire_plasma(seed) * 0.8
    return s.fade_out(mix(n, zap, explosion(seed, 0.42, 0.42, 1) * 0.65), 0.04)


def spell_spore(seed):
    # Spores raining down: soft wet pops over a hiss.
    rng = R(seed)
    n = samples(0.8)
    hiss = band_noise(n, rng, 1200.0, 6000.0, 2) * s.env_adsr(n, 0.1, 0.2, 0.5, 0.3) * 0.28
    pops = s.bubbles(0.75, 38.0, 170.0, 880.0, seed=seed, tau=0.03, gain=0.9)
    return s.fade_out(mix(n, hiss, pops), 0.05)


def spell_wrath(seed):
    # Gravemind's Wrath: a huge wet roar from under the ground.
    rng = R(seed)
    n = samples(2.6)
    y = s.growl(29.0, 2.5, vel=1.0, seed=seed) * 1.3
    boom = thump(n, 70.0, 24.0, 0.6, 8.0) * 1.2
    splat = s.squelch(1.5, 1800.0, 110.0, q=1.6, seed=seed, density=140.0) * 1.0
    return s.fade_out(s.soft_clip(mix(n, y, boom, (0.1, splat)), 1.6), 0.3)


def spell_buff_unsc(seed):
    # A rally: a short rising bugle call, a command over a radio.
    y = np.zeros(samples(1.1))
    for when, m, length in ((0.0, 62, 0.12), (0.14, 69, 0.12), (0.28, 74, 0.5)):
        y = mix(len(y), y, (when, s.brass(m, length, vel=0.85, players=2, formant=1500.0, seed=seed, nmax=16).mean(axis=1) * 1.4))
    return s.fade_out(y, 0.08)


def spell_buff_covenant(seed):
    y = np.zeros(samples(1.6))
    for when, m in ((0.0, 74), (0.09, 75), (0.2, 78), (0.33, 81), (0.5, 87)):
        y = mix(len(y), y, (when, s.glass(m, dur=1.2, vel=0.7, seed=seed)))
    hum = s.choir(62, 1.1, vowel="oh", to_vowel="ah", vel=0.65, voices=4, attack=0.3, release=0.45, seed=seed, nmax=14).mean(axis=1) * 1.2
    return s.fade_out(mix(len(y), y, hum), 0.1)


def spell_buff_flood(seed):
    # A frenzy spreading: the pulse quickens, all wet.
    y = np.zeros(samples(1.5))
    when = 0.0
    gap = 0.3
    while when < 1.1:
        y = mix(len(y), y, (when, s.heartbeat(0.75, seed=seed, f0=58.0)[:samples(0.28)]))
        when += gap
        gap *= 0.82
    return s.fade_out(mix(len(y), y, s.bubbles(1.3, 60.0, 180.0, 1000.0, seed=seed, tau=0.03, gain=0.55)), 0.1)


def spell_shield(seed):
    # A dome of energy snaps up and hums.
    rng = R(seed)
    n = samples(1.5)
    t = s.time_axis(n)
    f = np.interp(t, (0.0, 0.2), (180.0, 620.0))
    y = (np.sin(s.phase_of(f)) + 0.5 * np.sin(s.phase_of(f * 1.5)) + 0.3 * np.sin(s.phase_of(f * 2.5))) * s.env_adsr(n, 0.02, 0.3, 0.45, 0.85)
    sparkle = band_noise(n, rng, 3500.0, 10000.0, 2) * s.env_exp(n, 0.3, 0.05) * 0.3
    return s.fade_out(y * 0.55 + sparkle, 0.1)


def spell_drop_pods(seed):
    # Pods from orbit: a screech down, hard impacts in a stagger.
    rng = R(seed)
    n = samples(1.6)
    t = s.time_axis(n)
    screech = s.svf(s.noise(n, rng), np.interp(t, (0.0, 0.5), (5500.0, 1200.0)), q=2.5, mode="bp") * np.where(t < 0.52, np.minimum(t / 0.2, 1.0) * 0.8, 0.0)
    parts = [screech]
    for k, when in enumerate((0.48, 0.66, 0.92)):
        parts.append((when, explosion(seed + k, 0.75, 0.68, 4, 0.5) * 0.9))
    return s.fade_out(s.soft_clip(mix(n, *parts), 1.3), 0.1)


def spell_drop_air(seed):
    # Banshees arriving: a tearing hover whine passing over.
    rng = R(seed)
    n = samples(1.5)
    t = s.time_axis(n)
    f = np.interp(t, (0.0, 0.7, 1.45), (320.0, 700.0, 430.0))
    y = sum(g * np.sin(s.phase_of(f * r)) for r, g in ((1.0, 1.0), (1.5, 0.5), (2.02, 0.4), (3.03, 0.2)))
    air = s.svf(s.noise(n, rng), f * 3.0, q=1.2, mode="bp") * 0.7
    return s.fade_out((y * 0.4 + air) * s.env_adsr(n, 0.35, 0.3, 0.75, 0.5), 0.05)


def spell_drop_flood(seed):
    y = np.zeros(samples(1.3))
    for k, when in enumerate((0.0, 0.2, 0.45, 0.7)):
        y = mix(len(y), y, (when, flood_burst(seed + k)[:samples(0.6)] * 0.65))
    return s.fade_out(y, 0.1)


def spell_emp(seed):
    # Everything electrical chokes: a sharp pulse, then a falling, stuttering buzz.
    rng = R(seed)
    n = samples(1.3)
    t = s.time_axis(n)
    pulse = np.sin(s.phase_of(np.interp(t, (0.0, 0.08), (6000.0, 300.0)))) * np.exp(-t / 0.05) * 1.1
    f = np.interp(t, (0.0, 1.2), (500.0, 60.0))
    buzz = np.sign(np.sin(s.phase_of(f))) * 0.28 * np.exp(-t / 0.5)
    chop = (np.sin(2 * np.pi * np.interp(t, (0.0, 1.2), (38.0, 7.0)) * t) > -0.2)
    return s.fade_out(pulse + s.fft_filter(buzz * chop, lambda g: s.lp(g, 3800.0, 2)), 0.08)


def spell_smoke(seed):
    rng = R(seed)
    n = samples(1.4)
    t = s.time_axis(n)
    pop = thump(n, 120.0, 60.0, 0.05, 25.0) * 0.8
    hiss = s.svf(s.noise(n, rng), np.interp(t, (0.0, 1.3), (6500.0, 2100.0)), q=0.9, mode="lp") * s.env_adsr(n, 0.06, 0.4, 0.5, 0.8) * 0.55
    return s.fade_out(pop + hiss, 0.1)


# ------------------------------------------------------------------ engine and hover loops (one per kind, level follows how many are alive)

def loopable(y, overlap=0.5):
    """Folds the end of a sound over its start with a crossfade, so that it loops without a click."""
    k = samples(overlap)
    fade = np.linspace(0.0, 1.0, k)
    out = y[:-k].copy()
    out[:k] = y[:k] * np.sqrt(fade) + y[-k:] * np.sqrt(1.0 - fade)
    return out


def hum_engine(seed):
    # A diesel idle, seen from far up: slow, uneven low throbs.
    rng = R(seed)
    n = samples(4.5)
    t = s.time_axis(n)
    rate = 26.0
    y = np.zeros(n)
    for h, g in ((1, 1.0), (2, 0.75), (3, 0.42), (4, 0.3), (6, 0.15), (8, 0.08)):
        y += g * np.sin(2 * np.pi * rate * h * t + rng.uniform(0, 6.28))
    chug = 0.72 + 0.28 * np.sin(2 * np.pi * 6.5 * t + 1.3 * np.sin(2 * np.pi * 1.1 * t))
    grit = band_noise(n, rng, 110.0, 620.0, 2) * 0.35
    return loopable((y * 0.5 + grit) * chug)


def hum_hover(seed):
    # An anti-gravity hum: steady and glassy, slowly beating.
    rng = R(seed)
    n = samples(4.5)
    t = s.time_axis(n)
    y = np.zeros(n)
    for f, g in ((58.0, 1.0), (58.6, 0.8), (116.3, 0.45), (348.0, 0.18), (349.3, 0.18), (1044.0, 0.05)):
        y += g * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28))
    air = band_noise(n, rng, 800.0, 3200.0, 2) * 0.05
    return loopable(y * 0.6 + air)


# ------------------------------------------------------------------ UI: small parts

def tick(seed, length=0.006, lo=2200.0, gain=1.0):
    rng = R(seed)
    n = samples(length + 0.004)
    return band_noise(n, rng, lo, None, 1) * s.env_exp(n, length * 0.35, 0.0002) * gain


def beep(f, length, gain=1.0, harmonics=((1, 1.0), (3, 0.28), (5, 0.1)), attack=0.0015, release=0.012):
    n = samples(length)
    t = s.time_axis(n)
    y = sum(g * np.sin(2 * np.pi * f * h * t) for h, g in harmonics)
    return y * s.env_adsr(n, attack, 0.0, 1.0, release) * gain


def latch(seed, gain=1.0):
    """A heavy mechanism closing."""
    rng = R(seed)
    n = samples(0.12)
    return mix(n, tick(seed, 0.005, 1500.0, 0.8), (0.03, thump(samples(0.08), 240.0, 110.0, 0.022, 26.0) * 0.9), (0.03, tick(seed + 1, 0.009, 900.0, 0.6))) * gain


def bells(notes, dur=1.6, vel=0.6, seed=0):
    """Chimes: (time, midi) pairs."""
    n = samples(max(w for w, _ in notes) + dur)
    return mix(n, *[(w, s.glass(m, dur=dur, vel=vel, seed=seed + i)) for i, (w, m) in enumerate(notes)])


def hall(y, seconds=1.4, amount=0.3, seed=5):
    return small_room(y, seconds, amount, seed)


def horn(m, length, vel=0.8, players=3, formant=1300.0, seed=0, attack=None):
    return s.brass(m, length, vel=vel, players=players, formant=formant, seed=seed, nmax=20, attack=attack).mean(axis=1) * 1.6


def low_choir(m, length, vowel="oh", to_vowel=None, vel=0.7, seed=0):
    return s.choir(m, length, vowel=vowel, to_vowel=to_vowel, vel=vel, voices=4, attack=min(0.3, length * 0.3), release=0.5, seed=seed, nmax=18).mean(axis=1) * 1.4


# ------------------------------------------------------------------ UI: UNSC, crisp military clicks and data beeps

def ui_unsc(event, seed=0):
    if event == "click":
        return s.fade_out(mix(samples(0.07), tick(seed), (0.002, beep(1850.0, 0.032, 0.6))), 0.01)
    if event == "hover":
        return s.fade_out(mix(samples(0.03), tick(seed, 0.004, 4500.0, 0.8), beep(3400.0, 0.012, 0.25)), 0.005)
    if event == "buy":
        return s.fade_out(mix(samples(0.3), beep(1240.0, 0.055, 0.7), (0.07, beep(1860.0, 0.08, 0.75)), (0.15, latch(seed, 1.2))), 0.02)
    if event == "sell":
        return s.fade_out(mix(samples(0.3), latch(seed, 1.0), (0.09, beep(1660.0, 0.05, 0.65)), (0.16, beep(1110.0, 0.09, 0.7))), 0.02)
    if event == "unlock":
        rng = R(seed)
        n = samples(0.55)
        servo = np.sin(s.phase_of(np.interp(s.time_axis(n), (0.0, 0.26), (480.0, 1150.0)))) * np.where(s.time_axis(n) < 0.27, 0.3, 0.0)
        return s.fade_out(mix(n, servo, (0.02, beep(990.0, 0.05, 0.5)), (0.11, beep(1320.0, 0.05, 0.55)), (0.2, beep(1760.0, 0.05, 0.6)), (0.28, latch(seed, 1.4)), (0.31, beep(2640.0, 0.15, 0.55))), 0.03)
    if event == "error":
        square = ((1, 1.0), (3, 0.33), (5, 0.2), (7, 0.14), (9, 0.11))
        return s.fade_out(mix(samples(0.34), beep(196.0, 0.11, 0.8, square), (0.15, beep(185.0, 0.15, 0.85, square))), 0.02)
    if event == "cardpick":
        rng = R(seed)
        n = samples(0.32)
        snap = band_noise(n, rng, 900.0, 7000.0, 2) * s.env_exp(n, 0.012, 0.001) * 1.0
        return s.fade_out(mix(n, snap, (0.04, beep(1480.0, 0.05, 0.6)), (0.1, beep(2220.0, 0.12, 0.65))), 0.02)
    if event == "skip":
        rng = R(seed)
        n = samples(0.26)
        slide = s.svf(s.noise(n, rng), np.interp(s.time_axis(n), (0.0, 0.2), (3200.0, 700.0)), q=1.2, mode="bp") * s.env_adsr(n, 0.01, 0.06, 0.4, 0.1) * 0.7
        return s.fade_out(mix(n, slide, (0.13, beep(880.0, 0.08, 0.5))), 0.02)
    if event == "spellplaced":
        return s.fade_out(mix(samples(0.62), beep(1760.0, 0.04, 0.55), (0.08, beep(1760.0, 0.04, 0.55)), (0.16, beep(1760.0, 0.04, 0.6)), (0.24, beep(2350.0, 0.3, 0.7, attack=0.002, release=0.1)), (0.23, latch(seed, 0.8))), 0.03)
    if event == "levelup":
        y = mix(samples(0.95), (0.0, horn(62, 0.1, seed=seed)), (0.11, horn(66, 0.1, seed=seed)), (0.22, horn(69, 0.1, seed=seed)), (0.33, horn(74, 0.48, 0.9, seed=seed)), (0.33, beep(2940.0, 0.09, 0.15)))
        return s.fade_out(y, 0.05)
    if event == "battlestart":
        # A klaxon, then the drums go.
        klaxon = sum(beep(f, 0.55, 0.28, ((1, 1.0), (2, 0.6), (3, 0.4), (5, 0.2)), attack=0.02, release=0.08) for f in (310.0, 465.0))
        y = mix(samples(1.5), klaxon, (0.7, klaxon * 0.9), (0.62, s.snare_roll(0.5, 0.4, 1.0, seed=seed) * 0.7), (1.12, s.timpani(38, 1.0, seed=seed, dur=0.38) * 1.2))
        return s.fade_out(y, 0.1)
    if event == "roundwon":
        y = mix(samples(1.5), (0.0, horn(62, 0.16, seed=seed)), (0.18, horn(69, 0.16, seed=seed)), (0.36, horn(74, 0.75, 0.9, seed=seed)), (0.36, horn(69, 0.75, 0.75, seed=seed + 1)), (0.36, horn(66, 0.75, 0.75, seed=seed + 2)), (0.36, s.timpani(38, 0.7, seed=seed, dur=1.0)))
        return s.fade_out(hall(y, 0.9, 0.08)[:samples(1.8)], 0.15)
    if event == "roundlost":
        y = mix(samples(1.5), (0.0, horn(62, 0.32, 0.55, formant=700.0, seed=seed)), (0.36, horn(58, 0.32, 0.55, formant=700.0, seed=seed)), (0.72, horn(57, 0.6, 0.5, formant=700.0, seed=seed)), (0.72, s.timpani(33, 0.55, seed=seed, dur=0.8)))
        return s.fade_out(hall(y, 0.9, 0.08)[:samples(1.8)], 0.15)
    if event == "matchwon":
        y = mix(samples(1.0), *[(0.0, horn(m, 0.75, 0.95, seed=seed + m)) for m in (50, 62, 66, 69, 74)], (0.0, s.timpani(38, 1.0, seed=seed, dur=0.95) * 1.3), (0.0, s.crash(0.6, seed=seed, dur=0.95)))
        return s.fade_out(y, 0.15)
    if event == "matchlost":
        y = mix(samples(1.0), *[(0.0, horn(m, 0.72, 0.7, formant=700.0, seed=seed + m)) for m in (38, 50, 53, 57)], (0.0, s.timpani(33, 1.0, seed=seed, dur=0.95) * 1.3))
        return s.fade_out(y, 0.15)
    raise ValueError(event)


# ------------------------------------------------------------------ UI: Covenant, resonant alien chimes

def ui_covenant(event, seed=0):
    if event == "click":
        return s.fade_out(mix(samples(0.35), s.glass(86, dur=0.34, vel=0.8, seed=seed), s.glass(74, dur=0.28, vel=0.35, seed=seed + 1)), 0.03)
    if event == "hover":
        return s.fade_out(s.glass(93, dur=0.12, vel=0.45, seed=seed), 0.03)
    if event == "buy":
        return s.fade_out(hall(bells([(0.0, 74), (0.07, 78), (0.14, 81)], dur=0.7, seed=seed), 0.6, 0.06)[:samples(1.0)], 0.1)
    if event == "sell":
        return s.fade_out(hall(bells([(0.0, 81), (0.08, 75), (0.16, 70)], dur=0.7, seed=seed), 0.6, 0.06)[:samples(1.0)], 0.1)
    if event == "unlock":
        y = bells([(0.0, 70), (0.08, 74), (0.16, 75), (0.24, 78), (0.32, 81), (0.4, 87)], dur=1.0, vel=0.55, seed=seed)
        return s.fade_out(mix(samples(1.5), hall(y, 0.7, 0.06), low_choir(50, 0.7, "oo", "ah", 0.45, seed)), 0.15)
    if event == "error":
        # Two notes a half step apart, from low: a wrong, sour bell.
        return s.fade_out(mix(samples(0.5), s.glass(49, dur=0.45, vel=1.0, seed=seed), s.glass(50, dur=0.45, vel=1.0, seed=seed + 1), low_choir(38, 0.26, "mm", vel=0.7, seed=seed)), 0.04)
    if event == "cardpick":
        return s.fade_out(hall(bells([(0.0, 78), (0.09, 86)], dur=0.8, seed=seed), 0.6, 0.06)[:samples(1.0)], 0.1)
    if event == "skip":
        return s.fade_out(hall(s.glass(70, dur=0.5, vel=0.7, seed=seed), 0.5, 0.06)[:samples(0.7)], 0.08)
    if event == "spellplaced":
        rng = R(seed)
        n = samples(0.8)
        lift = s.svf(s.noise(n, rng), np.interp(s.time_axis(n), (0.0, 0.5), (500.0, 4200.0)), q=3.5, mode="bp") * s.env_adsr(n, 0.1, 0.2, 0.3, 0.4) * 0.4
        return s.fade_out(mix(n, lift, (0.12, bells([(0.0, 81), (0.1, 87)], dur=0.6, seed=seed)), low_choir(62, 0.45, "ah", vel=0.45, seed=seed)), 0.08)
    if event == "levelup":
        y = bells([(0.0, 74), (0.1, 78), (0.2, 81), (0.3, 86), (0.42, 90)], dur=1.1, vel=0.6, seed=seed)
        return s.fade_out(mix(samples(1.4), hall(y, 0.8, 0.07), (0.28, low_choir(62, 0.6, "ah", vel=0.7, seed=seed))), 0.12)
    if event == "battlestart":
        y = mix(samples(1.8), s.gong(1.7, 0.75, seed=seed), low_choir(38, 1.1, "oh", "ah", 0.95, seed), low_choir(45, 1.1, "oh", "ah", 0.75, seed + 1), (0.75, s.taiko(33, 1.0, seed=seed, dur=0.9) * 1.3), (1.05, s.taiko(36, 1.0, seed=seed, dur=0.7) * 1.2))
        return s.fade_out(y, 0.2)
    if event == "roundwon":
        y = mix(samples(1.6), (0.0, low_choir(62, 0.9, "oh", "ah", 0.75, seed)), (0.0, low_choir(69, 0.9, "oh", "ah", 0.7, seed + 1)), (0.0, low_choir(75, 0.9, "oh", "ah", 0.6, seed + 2)), (0.2, bells([(0.0, 81), (0.12, 87), (0.24, 90)], dur=1.0, vel=0.45, seed=seed)))
        return s.fade_out(y, 0.2)
    if event == "roundlost":
        y = mix(samples(1.6), (0.0, low_choir(50, 1.0, "ah", "mm", 0.65, seed)), (0.0, low_choir(51, 1.0, "ah", "mm", 0.55, seed + 1)), (0.0, s.taiko(33, 0.8, seed=seed, dur=1.0)), (0.3, bells([(0.0, 75), (0.25, 70)], dur=0.9, vel=0.42, seed=seed)))
        return s.fade_out(y, 0.2)
    if event == "matchwon":
        y = mix(samples(1.0), *[(0.0, low_choir(m, 0.7, "ah", vel=0.85, seed=seed + m)) for m in (50, 57, 62, 69)], (0.0, s.gong(0.95, 0.7, seed=seed)), (0.0, s.taiko(33, 1.0, seed=seed, dur=0.9) * 1.3))
        return s.fade_out(y, 0.15)
    if event == "matchlost":
        y = mix(samples(1.0), *[(0.0, low_choir(m, 0.7, "ah", "mm", 0.7, seed + m)) for m in (38, 39, 45, 51)], (0.0, s.taiko(31, 1.0, seed=seed, dur=0.9) * 1.3))
        return s.fade_out(y, 0.15)
    raise ValueError(event)


# ------------------------------------------------------------------ UI: Flood, wet fleshy squelches

def ui_flood(event, seed=0):
    if event == "click":
        return s.fade_out(s.squelch(0.09, 1250.0, 340.0, q=3.0, seed=seed, density=30.0), 0.01)
    if event == "hover":
        return s.fade_out(s.bubbles(0.05, 80.0, 420.0, 1000.0, seed=seed, tau=0.01) * 0.8, 0.01)
    if event == "buy":
        return s.fade_out(mix(samples(0.45), s.squelch(0.18, 800.0, 220.0, seed=seed), (0.12, s.squelch(0.26, 1300.0, 260.0, q=2.8, seed=seed + 1)), (0.12, thump(samples(0.15), 130.0, 66.0, 0.05, 24.0) * 0.7)), 0.03)
    if event == "sell":
        return s.fade_out(mix(samples(0.45), s.squelch(0.28, 1700.0, 150.0, q=2.0, seed=seed), (0.15, s.bubbles(0.26, 70.0, 160.0, 560.0, seed=seed, tau=0.02, gain=0.45))), 0.03)
    if event == "unlock":
        n = samples(0.9)
        tear = s.squelch(0.65, 700.0, 2200.0, q=2.6, seed=seed, density=70.0)
        pulse = s.heartbeat(0.7, seed=seed, f0=64.0)[:samples(0.55)]
        return s.fade_out(mix(n, tear, (0.3, pulse)), 0.05)
    if event == "error":
        rng = R(seed)
        n = samples(0.4)
        gag = s.svf(s.noise(n, rng), 280.0 + 120.0 * np.sign(np.sin(2 * np.pi * 26.0 * s.time_axis(n))), q=5.0, mode="bp") * s.env_adsr(n, 0.01, 0.1, 0.65, 0.12)
        return s.fade_out(mix(n, gag, thump(samples(0.12), 90.0, 58.0, 0.05, 22.0) * 0.9), 0.03)
    if event == "cardpick":
        return s.fade_out(mix(samples(0.42), s.squelch(0.34, 1500.0, 200.0, q=2.4, seed=seed), (0.05, s.bubbles(0.3, 90.0, 320.0, 1200.0, seed=seed, tau=0.015, gain=0.45))), 0.03)
    if event == "skip":
        return s.fade_out(mix(samples(0.3), s.squelch(0.26, 420.0, 130.0, q=2.0, seed=seed, density=25.0)), 0.03)
    if event == "spellplaced":
        return s.fade_out(mix(samples(0.65), s.heartbeat(0.6, seed=seed, f0=60.0)[:samples(0.4)], (0.14, s.squelch(0.42, 1900.0, 260.0, q=2.2, seed=seed)), (0.2, s.bubbles(0.35, 70.0, 220.0, 950.0, seed=seed, tau=0.02, gain=0.45))), 0.05)
    if event == "levelup":
        n = samples(1.0)
        rising = s.squelch(0.8, 400.0, 2800.0, q=3.0, seed=seed, density=120.0)
        return s.fade_out(mix(n, rising, (0.1, s.growl(38, 0.75, vel=0.6, seed=seed)), (0.62, thump(samples(0.25), 100.0, 56.0, 0.07, 20.0) * 0.9)), 0.08)
    if event == "battlestart":
        y = mix(samples(1.7), (0.0, s.growl(31, 1.35, vel=1.0, seed=seed) * 1.2), (0.0, s.heartbeat(0.9, seed=seed)), (0.55, s.heartbeat(1.0, seed=seed + 1)), (1.0, s.squelch(0.65, 2000.0, 130.0, q=1.9, seed=seed, density=110.0) * 1.2), (1.0, thump(samples(0.5), 90.0, 40.0, 0.14, 15.0) * 1.2))
        return s.fade_out(y, 0.15)
    if event == "roundwon":
        y = mix(samples(1.5), (0.0, s.growl(38, 1.15, vel=0.8, seed=seed)), (0.0, s.heartbeat(0.75, seed=seed)), (0.35, s.heartbeat(0.75, seed=seed + 1)), (0.65, s.heartbeat(0.8, seed=seed + 2)), (0.1, s.bubbles(1.2, 55.0, 180.0, 950.0, seed=seed, tau=0.025, gain=0.45)))
        return s.fade_out(y, 0.15)
    if event == "roundlost":
        y = mix(samples(1.5), (0.0, s.squelch(0.9, 700.0, 90.0, q=2.0, seed=seed, density=45.0)), (0.0, s.heartbeat(0.7, seed=seed)), (0.75, s.heartbeat(0.42, seed=seed + 1)))
        return s.fade_out(y, 0.15)
    if event == "matchwon":
        y = mix(samples(1.0), (0.0, s.growl(29, 0.9, vel=1.0, seed=seed) * 1.2), (0.0, s.growl(36, 0.9, vel=0.75, seed=seed + 1)), (0.0, s.heartbeat(1.0, seed=seed)), (0.0, s.squelch(0.7, 2500.0, 150.0, q=1.7, seed=seed, density=120.0)))
        return s.fade_out(y, 0.15)
    if event == "matchlost":
        y = mix(samples(1.0), (0.0, s.squelch(0.95, 500.0, 70.0, q=1.6, seed=seed, density=30.0)), (0.0, s.heartbeat(0.9, seed=seed, f0=36.0)))
        return s.fade_out(y, 0.15)
    raise ValueError(event)


def ui_neutral(event, seed=0):
    # Before a side is chosen: plain and soft, belonging to nobody.
    if event == "click":
        return s.fade_out(mix(samples(0.06), tick(seed, 0.005, 1700.0, 0.8), beep(1180.0, 0.03, 0.55, ((1, 1.0), (2, 0.25)))), 0.01)
    if event == "hover":
        return s.fade_out(beep(2360.0, 0.014, 0.3, ((1, 1.0),)), 0.005)
    if event == "error":
        return s.fade_out(mix(samples(0.22), beep(233.0, 0.08, 0.7, ((1, 1.0), (2, 0.4), (3, 0.3))), (0.1, beep(220.0, 0.1, 0.7, ((1, 1.0), (2, 0.4), (3, 0.3))))), 0.02)
    if event == "cardpick":
        return s.fade_out(mix(samples(0.3), beep(880.0, 0.06, 0.55, ((1, 1.0), (2, 0.25))), (0.07, beep(1320.0, 0.17, 0.6, ((1, 1.0), (2, 0.2)), release=0.08))), 0.02)
    raise ValueError(event)


# ------------------------------------------------------------------ the list of every clip: name, make, loudness target

UI_EVENTS = {
    "click": -24.0, "hover": -32.0, "buy": -20.0, "sell": -20.0, "unlock": -19.0, "error": -19.0, "cardpick": -20.0, "skip": -22.0,
    "spellplaced": -20.0, "levelup": -18.0, "battlestart": -15.0, "roundwon": -16.0, "roundlost": -16.0, "matchwon": -14.0, "matchlost": -14.0,
}


def clip_list():
    out = []

    def add(name, fn, target, variants=1, seed=0):
        if variants == 1:
            out.append((name, fn, target, seed))
        else:
            for v in range(variants):
                out.append((f"{name}_{v + 1}", fn, target, seed + v * 13))

    add("sfx_fire_bullet", fire_bullet, -19.0, 3)
    add("sfx_fire_bullet_flood", fire_bullet_flood, -19.0, 2)
    add("sfx_fire_heavybullet", fire_heavybullet, -17.0, 3)
    add("sfx_fire_cannon", fire_cannon, -13.0, 2)
    add("sfx_fire_gauss", fire_gauss, -13.0, 2)
    add("sfx_fire_rocket", fire_rocket, -16.0, 2)
    add("sfx_fire_bomb", fire_bomb, -15.0, 2)
    add("sfx_fire_flame", fire_flame, -18.0, 2)
    add("sfx_fire_plasma", fire_plasma, -19.0, 3)
    add("sfx_fire_plasmaheavy", fire_plasmaheavy, -14.0, 2)
    add("sfx_fire_needle", fire_needle, -20.0, 3)
    add("sfx_fire_laser", fire_laser, -14.0, 1)
    add("sfx_fire_beam", fire_beam, -13.0, 1)
    add("sfx_fire_melee", fire_melee, -20.0, 3)
    add("sfx_fire_melee_covenant", fire_melee_covenant, -20.0, 3)
    add("sfx_fire_melee_flood", fire_melee_flood, -20.0, 3)
    add("sfx_fire_bio", fire_bio, -19.0, 3)
    add("sfx_impact_flesh", impact_flesh, -25.0, 3)
    add("sfx_impact_armor", impact_armor, -25.0, 3)
    add("sfx_impact_shield", impact_shield, -24.0, 3)
    add("sfx_impact_block", impact_block, -22.0, 2)
    add("sfx_impact_wet", impact_wet, -24.0, 3)
    add("sfx_explosion_small", explosion_small, -15.0, 3)
    add("sfx_explosion_medium", explosion_medium, -12.0, 2)
    add("sfx_explosion_large", explosion_large, -10.0, 2)
    add("sfx_death_infantry", death_infantry, -22.0, 4)
    add("sfx_death_infantry_covenant", death_infantry_covenant, -22.0, 3)
    add("sfx_death_flood", death_flood, -20.0, 3)
    add("sfx_death_vehicle", death_vehicle, -12.0, 2)
    add("sfx_death_air", death_air, -12.0, 2)
    add("sfx_flood_burst", flood_burst, -14.0, 3)
    add("sfx_infection_start", infection_start, -22.0, 2)
    add("sfx_infection_rise", infection_rise, -19.0, 2)
    add("sfx_pelican_dive", pelican_dive, -14.0)
    add("sfx_pelican_crash", pelican_crash, -10.0)
    add("sfx_drop_in", drop_in, -17.0, 2)
    add("sfx_spell_mac", spell_mac, -9.0)
    add("sfx_spell_nuke", spell_nuke, -9.0)
    add("sfx_spell_glassing", spell_glassing, -12.0)
    add("sfx_spell_missiles", spell_missiles, -13.0)
    add("sfx_spell_plasma", spell_plasma, -13.0, 2)
    add("sfx_spell_storm_tick", spell_storm_tick, -17.0, 3)
    add("sfx_spell_spore", spell_spore, -18.0, 3)
    add("sfx_spell_wrath", spell_wrath, -10.0)
    add("sfx_spell_buff_unsc", spell_buff_unsc, -16.0)
    add("sfx_spell_buff_covenant", spell_buff_covenant, -16.0)
    add("sfx_spell_buff_flood", spell_buff_flood, -16.0)
    add("sfx_spell_shield", spell_shield, -16.0)
    add("sfx_spell_drop_pods", spell_drop_pods, -12.0)
    add("sfx_spell_drop_air", spell_drop_air, -15.0)
    add("sfx_spell_drop_flood", spell_drop_flood, -13.0)
    add("sfx_spell_emp", spell_emp, -13.0)
    add("sfx_spell_smoke", spell_smoke, -18.0)
    add("sfx_hum_engine", hum_engine, -22.0)
    add("sfx_hum_hover", hum_hover, -22.0)
    for faction, fn in (("unsc", ui_unsc), ("covenant", ui_covenant), ("flood", ui_flood)):
        for event, target in UI_EVENTS.items():
            add(f"ui_{faction}_{event}", _bind(fn, event), target)
    for event in ("click", "hover", "error", "cardpick"):
        add(f"ui_neutral_{event}", _bind(ui_neutral, event), UI_EVENTS[event])
    return out


class _bind:
    def __init__(self, fn, event):
        self.fn, self.event = fn, event

    def __call__(self, seed):
        return self.fn(self.event, seed)


def render(item):
    name, fn, target, seed = item
    y = s.remove_dc(np.asarray(fn(seed + 1), dtype=np.float64))
    y = s.level_short(y, target)
    y = s.remove_dc(y)
    if not name.startswith("sfx_hum"):
        y = s.fade_out(y, 0.004)
    s.write_wav(os.path.join(OUT, name + ".wav"), y)
    return name, len(y) / s.SR, s.peak_db(y), s.momentary_peak(y)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    wanted = sys.argv[1:]
    items = [c for c in clip_list() if not wanted or any(c[0].startswith(w) for w in wanted)]
    with Pool(16) as pool:
        rows = pool.map(render, items)
    for name, seconds, peak, loud in rows:
        print(f"{name:34s} {seconds:5.2f} s  peak {peak:6.1f} dB  loudest moment {loud:6.1f}")
    print(len(rows), "clips,", round(sum(r[1] for r in rows) * s.SR * 2 / 1e6, 1), "MB")
