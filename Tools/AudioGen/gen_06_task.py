"""Выполнение задания: арпеджио Rhodes вверх -> светлый аккорд Cmaj9 (ля минор -> параллельный мажор)."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib")); os.chdir(os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from synth import *

def f_(name, octv):  # прямые частоты, без транспонирования
    return NOTE[name] * 2 ** (octv - 4)

def pad(freqs, dur, attack=0.25):
    t = t_axis(dur); out = np.zeros((len(t), 2))
    for f in freqs:
        for det, pan in [(-0.006, -0.6), (0.006, 0.6), (0.0, 0.0)]:
            ph = 2 * np.pi * f * (1 + det) * t
            s = np.sin(ph) + 0.35 * np.sin(2 * ph) + 0.15 * np.sin(3 * ph)
            out += mono_to_st(s, pan) / 3
    a = np.clip(t / attack, 0, 1) ** 2
    e = a * np.exp(-np.maximum(t - attack, 0) / (dur * 0.45))
    rel = int(0.3 * len(t)); e[-rel:] *= np.linspace(1, 0, rel) ** 2
    out *= e[:, None] / len(freqs)
    return np.stack([filt(out[:, c], "low", 2500) for c in range(2)], axis=1)

def snap(seed=5):
    r = np.random.default_rng(seed); n = int(0.08 * SR)
    x = filt(r.standard_normal(n), "bp", (1200, 5000)) * env_ad(n, 0.0005, 0.014)
    return mono_to_st(x)

def sub(f, dur=0.6):
    t = t_axis(dur); s = np.sin(2 * np.pi * f * t) * env_ad(len(t), 0.004, 0.22)
    rel = int(0.2 * len(t)); s[-rel:] *= np.linspace(1, 0, rel)
    return mono_to_st(s)

def bell(f, dur=1.2):
    t = t_axis(dur)
    s = np.sin(2 * np.pi * f * t + 1.5 * np.exp(-t / 0.05) * np.sin(2 * np.pi * f * 3.5 * t)) * env_ad(len(t), 0.002, 0.35)
    rel = int(0.2 * len(t)); s[-rel:] *= np.linspace(1, 0, rel) ** 2
    return mono_to_st(s, 0.2)

step = BEAT / 4                                    # шестнадцатые на 86 BPM
ARP = [("A", 3), ("C", 4), ("E", 4), ("G", 4), ("B", 4)]           # Am9 вверх
CHORD = [("C", 3), ("E", 4), ("G", 4), ("B", 4), ("D", 5)]         # Cmaj9

def build(full=True):
    tr = []
    arp = ARP if full else ARP[2:]
    for i, (n, o) in enumerate(arp):
        tr.append((rhodes(f_(n, o), dur=0.8, vel=0.55 + 0.06 * i), i * step, 0.5))
    hit = len(arp) * step
    for n, o in CHORD:
        tr.append((rhodes(f_(n, o), dur=1.8 if full else 1.1, vel=0.8), hit, 0.45))
    tr.append((bell(f_("E", 5), 1.4 if full else 0.9), hit, 0.35))
    tr.append((snap(), hit, 0.5))
    tr.append((sub(f_("C", 2)), hit, 0.6))
    if full:
        tr.append((pad([f_(n, o) for n, o in CHORD[1:]], 2.0), hit - 0.05, 0.9))
    st = mix(*tr)
    st = reverb(st, mix=0.25, length=1.6, damp=4500)
    st = warm(st, cutoff=11000, drive=1.2)
    return finish(st, peak_db=-3, trim_db=-45, fade_ms=200)

for name, full in [("task_complete_full", True), ("task_complete_short", False)]:
    st = build(full); save(f"out/06_task_complete/{name}.wav", st); print(name, f"{len(st)/SR:.2f}s")
