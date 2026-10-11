"""Вылет v2: мягкий воздушный «фух» вверх, без щелчка и резонансного свиста."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib")); os.chdir(os.path.dirname(os.path.abspath(__file__)))
import numpy as np, zlib
from synth import *
OUT = "out/02_launch"
for v in range(1, 4):
    rng = np.random.default_rng(zlib.crc32(f"launch_v2_{v}".encode()))
    dur = rng.uniform(0.28, 0.34); n = int(dur * SR); x = np.linspace(0, 1, n)
    e = np.sin(np.pi * x ** 0.8) ** 2          # плавный подъём и спад, без резкой атаки
    wh = svf_sweep(rng.standard_normal(n) * e, rng.uniform(250, 320), rng.uniform(1100, 1500), q=0.9, curve=1.1)
    wh = filt(wh, "low", 2500)
    t = t_axis(0.22)
    sub = np.sin(2 * np.pi * (hz("F", 2) * t - 15 * t ** 2)) * env_ad(len(t), 0.02, 0.06)  # ля, 110 Гц
    st = mix((mono_to_st(sub), 0.0, 0.35), (mono_to_st(wh, rng.uniform(-0.25, 0.25)), 0.0, 1.0))
    st = reverb(st, mix=0.18, length=0.6, damp=3000)
    st = warm(st, cutoff=5000, drive=1.05)
    st = finish(st, peak_db=-7, trim_db=-40, fade_ms=80)
    save(f"{OUT}/launch_{v:02d}.wav", st); print(v, f"{len(st)/SR:.2f}s")
