"""Мягкие синтезаторные клики: плавная атака, мало верхов, тише. Ля минор."""
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib"))
import numpy as np
from synth import *
OUT = "out/11_ui"  # mkdir -p out/11_ui перед запуском
def f_(n, o): return NOTE[n] * 2 ** (o - 4)

def soft_pluck(f, dur=0.34):
    t = t_axis(dur); n = len(t)
    # треугольник (мягкие гармоники) + немного пилы, слегка расстроены
    tri = sum(((-1) ** k) * np.sin(2 * np.pi * f * (2 * k + 1) * t) / (2 * k + 1) ** 2 for k in range(8))
    saw = sum(np.sin(2 * np.pi * f * 1.003 * k * t) / k for k in range(1, 10))
    x = 0.8 * tri + 0.2 * saw
    cutoff = 500 + 1800 * np.exp(-t / 0.05)                     # фильтр открывается меньше и закрывается мягче
    y = np.zeros(n); s1 = s2 = 0.0
    for i in range(n):                                          # двухполюсный ФНЧ — плавнее срез верхов
        a = 1 - np.exp(-2 * np.pi * cutoff[i] / SR); s1 += a * (x[i] - s1); s2 += a * (s1 - s2); y[i] = s2
    y = y * 2.2 + 0.35 * np.sin(2 * np.pi * f / 2 * t)         # суб на октаву ниже
    return y * env_ad(n, 0.006, 0.08)                           # атака 6 мс — без щелчка

def soft_pop(f, dur=0.3):
    t = t_axis(dur); n = len(t)
    bend = 1 + 0.08 * np.exp(-t / 0.01)                          # едва заметный подскок высоты
    ph = 2 * np.pi * np.cumsum(f * bend) / SR
    y = np.sin(ph + 0.6 * np.exp(-t / 0.025) * np.sin(2 * ph))   # слабый FM — почти чистый тон
    y += 0.15 * np.sin(2 * ph) * np.exp(-t / 0.03)
    return y * env_ad(n, 0.005, 0.06)

NOTES = [("A", 4), ("C", 5), ("E", 5)]
for name, fn in [("click_soft_pluck", soft_pluck), ("click_soft_pop", soft_pop)]:
    for v, (nn, oc) in enumerate(NOTES, 1):
        dry = mono_to_st(fn(f_(nn, oc)))
        st = mix((dry, 0.0, 1.0), (dry[:, ::-1], 0.1, 0.12))   # тихое эхо
        st = reverb(st, mix=0.14, length=0.45, damp=4500)
        st = warm(st, cutoff=7000, drive=1.05)                   # ещё мягче сверху
        st = finish(st, peak_db=-8, trim_db=-45, fade_ms=40)
        save(f"{OUT}/{name}_{v:02d}.wav", st); print(name, v, f"{len(st)/SR:.2f}s")
