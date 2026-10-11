"""Разрез v3: в духе аркадных слайсеров — резкий взмах лезвия + сочный шлепок с брызгами."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib")); os.chdir(os.path.dirname(os.path.abspath(__file__)))
import numpy as np, zlib
from synth import *
OUT = "out/01_slice"

def blade_swish(rng):
    d = rng.uniform(0.09, 0.12); n = int(d * SR); t = np.arange(n) / SR
    e = (t / d) ** 0.5 * np.exp(-((t / d) ** 2) * 3)
    air = svf_sweep(rng.standard_normal(n) * e, rng.uniform(5500, 7500), rng.uniform(1800, 2500), q=2.5, curve=0.8)
    sheen = sum(a * np.sin(2 * np.pi * f * (1 - 0.08 * t / d) * t) for f, a in [(3150, 0.5), (4730, 0.35), (6280, 0.25)])
    sheen *= np.exp(-t / 0.05) * (t / d) ** 0.3 * 0.25
    return air + sheen, d

def splat(rng, size=1.0):
    n = int(0.4 * SR); t = np.arange(n) / SR; out = np.zeros(n)
    m = int(0.09 * SR)
    out[:m] += svf_sweep(rng.standard_normal(m) * env_ad(m, 0.002, 0.03), 1800, 350, q=3.5) * 1.3 * size
    tt = t_axis(0.09)
    out[:len(tt)] += np.sin(2 * np.pi * (170 * tt - 500 * tt ** 2)) * env_ad(len(tt), 0.001, 0.03) * 0.9 * size
    for _ in range(int(rng.integers(14, 22) * size)):
        s = rng.exponential(0.05); i = int(s * SR); dd = int(rng.uniform(0.004, 0.015) * SR)
        if i + dd > n: continue
        out[i:i + dd] += filt(rng.standard_normal(dd), "bp", (1500, 6500)) * env_ad(dd, 0.0005, dd / SR / 3) * rng.uniform(0.2, 0.6) * np.exp(-s / 0.1)
    for _ in range(int(rng.integers(3, 6) * size)):
        s = rng.uniform(0.01, 0.15); dd = rng.uniform(0.02, 0.04); ttt = t_axis(dd); f0 = rng.uniform(600, 1400)
        b = np.sin(2 * np.pi * f0 * (ttt + 6 * ttt ** 2)) * env_ad(len(ttt), 0.002, dd / 3)
        i = int(s * SR); out[i:i + len(b)] += b * 0.25
    return out

def crackle(rng, count=6):
    n = int(0.08 * SR); out = np.zeros(n)
    for _ in range(count):
        i = int(rng.uniform(0, 0.7) * n); dd = int(rng.uniform(0.003, 0.008) * SR)
        out[i:i + dd] += filt(rng.standard_normal(dd), "bp", (2000, 7000)) * env_ad(dd, 0.0003, dd / SR / 4) * rng.uniform(0.5, 1)
    return out

def knock(rng):
    n = int(0.2 * SR); t = np.arange(n) / SR; b = rng.uniform(0.9, 1.1)
    return sum(a * np.sin(2 * np.pi * f * b * t) * np.exp(-t / dc) for f, a, dc in [(380, 0.9, 0.05), (950, 0.5, 0.03), (2100, 0.3, 0.015)])

def impact(name, rng):
    if name == "juicy":   return splat(rng, 1.0)
    if name == "soft":    return filt(splat(rng, 0.8), "low", 2500) * 1.3
    if name == "crunchy":
        s = splat(rng, 0.5); c = crackle(rng, int(rng.integers(5, 9))); s[:len(c)] += c; return s
    s = splat(rng, 0.45); k = knock(rng); s[:len(k)] += k * 0.9; return s  # hard

NOTES = {"juicy": [("C", 5), ("Eb", 5), ("F", 5)], "crunchy": [("Ab", 4), ("Bb", 4), ("C", 5)],
         "soft": [("F", 4), ("Ab", 4), ("Bb", 3)], "hard": [("Eb", 4), ("F", 4), ("C", 4)]}

for name, notes in NOTES.items():
    for v, (nn, oc) in enumerate(notes, 1):
        rng = np.random.default_rng(zlib.crc32(f"v3{name}{v}".encode()))
        sw, d = blade_swish(rng)
        pan = rng.uniform(-0.5, 0.5)
        st = mix((mono_to_st(sw, pan), 0.0, 0.8),
                 (mono_to_st(impact(name, rng), pan * 0.4), d * 0.55, 1.0),
                 (rhodes(hz(nn, oc), dur=0.9, vel=0.6), d * 0.55, 0.16))
        st = reverb(st, mix=0.1, length=0.6, damp=6000)
        st = warm(st, cutoff=14000, drive=1.4)
        st = finish(st, peak_db=-3, trim_db=-42, fade_ms=100)
        save(f"{OUT}/slice_{name}_{v:02d}.wav", st)
        print(name, v, f"{len(st)/SR:.2f}s")
