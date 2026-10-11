"""5-секундный таймер перед завершением уровня. Тики на 0,1,2,3,4 с; финал ровно на 5,0 с."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib")); os.chdir(os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from synth import *
def f_(n, o): return NOTE[n] * 2 ** (o - 4)
OUT = "out/07_timer5"

def bell(f, dur=0.5, idx0=1.4):
    t = t_axis(dur)
    s = np.sin(2 * np.pi * f * t + idx0 * np.exp(-t / 0.04) * np.sin(2 * np.pi * f * 3.5 * t)) * env_ad(len(t), 0.0015, 0.12)
    rel = int(0.2 * len(t)); s[-rel:] *= np.linspace(1, 0, rel) ** 2
    return mono_to_st(s)

def thump(f=f_("A", 1) * 2, dur=0.3, g=1.0):
    t = t_axis(dur); s = np.sin(2 * np.pi * (f * t - 20 * t ** 2)) * env_ad(len(t), 0.004, 0.08) * g
    rel = int(0.2 * len(t)); s[-rel:] *= np.linspace(1, 0, rel)
    return mono_to_st(s)

def snap(seed, g=1.0):
    r = np.random.default_rng(seed); n = int(0.06 * SR)
    return mono_to_st(filt(r.standard_normal(n), "bp", (1500, 5500)) * env_ad(n, 0.0005, 0.01) * g)

TICKS = [("A", 4), ("B", 4), ("C", 5), ("D", 5), ("E", 5)]  # подъём по ля минору

def tick(i):
    n, o = TICKS[i]; f = f_(n, o)
    st = mix((bell(f), 0, 0.7), (rhodes(f, dur=0.6, vel=0.5), 0, 0.35),
             (thump(g=0.6 + 0.1 * i), 0, 0.7), (snap(10 + i, 0.4 + 0.08 * i), 0, 1.0))
    return st

def half_tick(i):
    return mix((snap(30 + i, 0.35), 0, 1.0), (bell(f_("E", 6), 0.25, 0.8), 0, 0.12))

def final():
    tr = [(thump(f_("A", 1) * 2, 0.8, 1.2), 0, 0.9), (snap(99, 1.0), 0, 1.0)]
    for n, o in [("A", 3), ("E", 4), ("A", 4), ("C", 5), ("E", 5)]:   # аккорд Am(add) — финал
        tr.append((rhodes(f_(n, o), dur=1.6, vel=0.8), 0, 0.4))
    tr.append((bell(f_("A", 5), 1.4), 0, 0.45))
    return mix(*tr)

# полная дорожка
tr = []
for i in range(5):
    tr.append((tick(i), float(i), 1.0))
    if i >= 3: tr.append((half_tick(i), i + 0.5, 1.0))
tr.append((final(), 5.0, 1.0))
st = mix(*tr)
st = reverb(st, mix=0.18, length=1.2, damp=4500)
st = warm(st, cutoff=11000, drive=1.15)
st = finish(st, peak_db=-3, trim_db=-45, fade_ms=200)
save(f"{OUT}/timer_5s_full.wav", st); print("full", f"{len(st)/SR:.2f}s")

# отдельные куски для синхронизации в коде
for i in range(5):
    s = finish(warm(reverb(tick(i), mix=0.18, length=0.8)), peak_db=-4, trim_db=-42, fade_ms=60)
    save(f"{OUT}/timer_tick_{5 - i}.wav", s)
s = finish(warm(reverb(final(), mix=0.2, length=1.2)), peak_db=-3, trim_db=-45, fade_ms=200)
save(f"{OUT}/timer_end.wav", s)
s = finish(warm(reverb(half_tick(0), mix=0.15, length=0.5)), peak_db=-10, trim_db=-40, fade_ms=40)
save(f"{OUT}/timer_halftick.wav", s)
# проверка: пики атак в полной дорожке
a = np.abs(st).max(axis=1); env = np.convolve(a, np.ones(441) / 441, "same")
on = [round(k / SR, 2) for k in range(1, len(env)) if env[k] > 0.12 and env[k - 1] <= 0.12]
print("onsets:", on)
