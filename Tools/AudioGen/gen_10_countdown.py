"""Отсчёт 3-2-1-старт: тики на 0/1/2 с, старт ровно на 3,0 с. Ля минор."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib")); os.chdir(os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from synth import *
def f_(n, o): return NOTE[n] * 2 ** (o - 4)
OUT = "out/10_countdown"
rng = np.random.default_rng(33)

def bell(f, dur=0.6, idx0=1.6, decay=0.15):
    t = t_axis(dur)
    s = np.sin(2 * np.pi * f * t + idx0 * np.exp(-t / 0.04) * np.sin(2 * np.pi * f * 3.5 * t)) * env_ad(len(t), 0.0015, decay)
    rel = int(0.2 * len(t)); s[-rel:] *= np.linspace(1, 0, rel) ** 2
    return mono_to_st(s)
def snap(g=1.0):
    n = int(0.12 * SR); x = np.zeros(n)
    for k in range(3):
        i = int(k * 0.006 * SR); d = n - i
        x[i:] += filt(rng.standard_normal(d), "bp", (1200, 5000)) * env_ad(d, 0.0004, 0.006 if k < 2 else 0.03)
    return mono_to_st(x * g)
def kick(g=1.0):
    t = t_axis(0.5); f = 45 + 80 * np.exp(-t / 0.03)
    return mono_to_st(np.tanh(1.4 * np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(len(t), 0.002, 0.16)) * g)

def tick():
    f = f_("E", 5)
    return mix((bell(f), 0, 0.7), (rhodes(f_("E", 4), 0.7, 0.6), 0, 0.4), (snap(0.5), 0, 1.0), (kick(0.5), 0, 1.0))

def riser(dur=0.9):
    n = int(dur * SR); x = np.linspace(0, 1, n)
    s = svf_sweep(rng.standard_normal(n) * x ** 2.2, 300, 3500, q=1.2, curve=1.0)
    return mono_to_st(filt(s, "low", 5000) * 0.8)

def go():
    tr = [(kick(1.0), 0, 1.0), (snap(1.0), 0, 1.0), (bell(f_("A", 5), 1.6, 1.8, 0.45), 0, 0.6)]
    for n, o in [("A", 2), ("E", 3), ("G", 3), ("C", 4), ("E", 4), ("B", 4)]:   # Am9 — светлый старт
        tr.append((rhodes(f_(n, o), 1.8, 0.85), 0, 0.3))
    t = t_axis(0.9); s = np.sin(2 * np.pi * f_("A", 1) * t) * env_ad(len(t), 0.005, 0.3)
    rel = int(0.2 * len(t)); s[-rel:] *= np.linspace(1, 0, rel)
    tr.append((mono_to_st(s), 0, 0.8))
    return mix(*tr)

full = mix((tick(), 0.0, 1.0), (tick(), 1.0, 1.0), (tick(), 2.0, 1.0), (riser(), 2.1, 0.5), (go(), 3.0, 1.0))
full = finish(warm(reverb(full, mix=0.18, length=1.4), cutoff=11000, drive=1.2), peak_db=-2, trim_db=-45, fade_ms=200)
save(f"{OUT}/countdown_3s_full.wav", full); print("full", f"{len(full)/SR:.2f}s")
save(f"{OUT}/countdown_tick.wav", finish(warm(reverb(tick(), mix=0.15, length=0.8)), peak_db=-4, trim_db=-42, fade_ms=60))
save(f"{OUT}/countdown_go.wav", finish(warm(reverb(go(), mix=0.2, length=1.4)), peak_db=-2, trim_db=-45, fade_ms=200))
# проверка таймингов: максимумы огибающей в окнах около 0,1,2,3 с
a = np.abs(full).max(axis=1)
for s0 in (0, 1, 2, 3):
    w = a[int((s0 - 0.05) * SR) if s0 else 0:int((s0 + 0.1) * SR)]
    print(s0, "s peak at +", round(np.argmax(w) / SR - (0.05 if s0 else 0), 3))
