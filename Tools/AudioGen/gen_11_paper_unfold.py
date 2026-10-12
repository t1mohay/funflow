"""Звуки интерфейса: развёртывание бумаги (paper_unfold_01..02 → Assets/Resources/Audio/UI). Деревянный клик здесь — черновик, в игре используется gen_11_click_soft.py."""
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib"))
import numpy as np, zlib
from synth import *
OUT = "out/11_ui"  # mkdir -p out/11_ui перед запуском
def f_(n, o): return NOTE[n] * 2 ** (o - 4)   # прямые частоты (ля минор)

# ── клик: деревянный «ток» + тихая нота-колокольчик
def wood_tock(rng, pitch=1.0):
    n = int(0.12 * SR); t = np.arange(n) / SR
    modes = [(520, 1.0, 0.030), (1180, 0.55, 0.018), (2350, 0.35, 0.010), (3900, 0.18, 0.006)]
    body = sum(a * np.sin(2 * np.pi * f * pitch * t + rng.uniform(0, 6)) * np.exp(-t / d) for f, a, d in modes)
    click = filt(rng.standard_normal(n), "bp", (2000, 7000)) * env_ad(n, 0.0003, 0.0025) * 0.8
    thump = np.sin(2 * np.pi * 150 * pitch * t) * env_ad(n, 0.001, 0.02) * 0.5
    return body + click + thump

def chime(f, dur=0.35):
    t = t_axis(dur)
    s = np.sin(2 * np.pi * f * t + 1.2 * np.exp(-t / 0.03) * np.sin(2 * np.pi * f * 3.5 * t)) * env_ad(len(t), 0.001, 0.08)
    rel = int(0.2 * len(t)); s[-rel:] *= np.linspace(1, 0, rel) ** 2
    return s

for v, (note, octv, pitch) in enumerate([("A", 5, 1.0), ("C", 6, 1.06), ("E", 6, 0.95)], 1):
    rng = np.random.default_rng(zlib.crc32(f"click{v}".encode()))
    st = mix((mono_to_st(wood_tock(rng, pitch)), 0.0, 1.0), (mono_to_st(chime(f_(note, octv))), 0.004, 0.22))
    st = reverb(st, mix=0.10, length=0.35, damp=6000)
    st = warm(st, cutoff=12000, drive=1.15)
    st = finish(st, peak_db=-4, trim_db=-45, fade_ms=30)
    save(f"{OUT}/click_{v:02d}.wav", st); print("click", v, f"{len(st)/SR:.2f}s")

# ── развёртывание бумаги: шорох скольжения + хруст сгибов + мягкий «хлоп» в конце
def paper_unfold(rng, dur):
    n = int(dur * SR); t = np.arange(n) / SR; out = np.zeros(n)
    # шорох скольжения: полосовой шум с «дыханием» движения
    slide = filt(rng.standard_normal(n), "bp", (1500, 7500))
    motion = np.clip(np.sin(np.pi * np.clip(t / (dur * 0.85), 0, 1)) ** 1.5, 0, 1)
    wobble = 1 + 0.35 * filt(rng.standard_normal(n), "low", 12) / 0.02
    out += slide * motion * np.clip(wobble, 0.3, 1.8) * 0.08
    # хруст: много коротких щелчков, плотнее на сгибах (2–3 «волны»)
    folds = sorted(rng.uniform(0.05, dur * 0.75, rng.integers(2, 4)))
    for _ in range(int(dur * 260)):
        c = folds[rng.integers(len(folds))]
        tc = c + rng.normal(0, 0.06)
        if not 0 <= tc < dur - 0.02: continue
        d = int(rng.uniform(0.0015, 0.007) * SR); i = int(tc * SR)
        lo = rng.uniform(1500, 3500)
        out[i:i + d] += filt(rng.standard_normal(d), "bp", (lo, min(lo * 3, 12000))) * env_ad(d, 0.0002, d / SR / 4) * rng.uniform(0.15, 0.7)
    # финальный мягкий «хлоп» раскрытого листа
    i = int((dur - 0.12) * SR); m = int(0.1 * SR)
    flap = filt(rng.standard_normal(m), "bp", (300, 2500)) * env_ad(m, 0.002, 0.03) * 0.9
    flap += np.sin(2 * np.pi * 110 * t[:m]) * env_ad(m, 0.002, 0.03) * 0.4
    out[i:i + m] += flap
    return out

for v, dur in enumerate([0.75, 0.95], 1):
    rng = np.random.default_rng(zlib.crc32(f"paper{v}".encode()))
    x = paper_unfold(rng, dur)
    st = np.stack([x * (1 + 0.1 * np.sin(np.linspace(0, 3, len(x)))), x * (1 - 0.1 * np.sin(np.linspace(0, 3, len(x))))], 1)  # лёгкое движение в стерео
    st = reverb(st, mix=0.12, length=0.5, damp=5000)
    st = warm(st, cutoff=11000, drive=1.1)
    st = finish(st, peak_db=-5, trim_db=-45, fade_ms=60)
    save(f"{OUT}/paper_unfold_{v:02d}.wav", st); print("paper", v, f"{len(st)/SR:.2f}s")
