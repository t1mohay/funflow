"""Взмах по пустоте (без попадания по еде)."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib")); os.chdir(os.path.dirname(os.path.abspath(__file__)))
import numpy as np, zlib
from synth import *

def blade_swish(rng):  # тот же характер, что в разрезе v3
    d = rng.uniform(0.10, 0.14); n = int(d * SR); t = np.arange(n) / SR
    e = (t / d) ** 0.5 * np.exp(-((t / d) ** 2) * 3)
    air = svf_sweep(rng.standard_normal(n) * e, rng.uniform(5000, 6500), rng.uniform(1500, 2200), q=1.8, curve=0.8)
    sheen = sum(a * np.sin(2 * np.pi * f * (1 - 0.08 * t / d) * t) for f, a in [(3150, 0.5), (4730, 0.35), (6280, 0.25)])
    sheen *= np.exp(-t / 0.05) * (t / d) ** 0.3 * 0.15
    return air + sheen

# --- взмах по пустоте
for v in range(1, 4):
    rng = np.random.default_rng(zlib.crc32(f"empty{v}".encode()))
    st = mono_to_st(blade_swish(rng), rng.uniform(-0.5, 0.5))
    st = reverb(st, mix=0.15, length=0.5, damp=5000)
    st = warm(st, cutoff=9000, drive=1.1)
    st = finish(st, peak_db=-8, trim_db=-40, fade_ms=60)   # тише разреза
    save(f"out/04_swipe_empty/swipe_empty_{v:02d}.wav", st); print("empty", v, f"{len(st)/SR:.2f}s")

