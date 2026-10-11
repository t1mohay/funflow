"""Лобби: оригинальная петля 16 тактов в стиле late-night R&B / нео-соул. 86 BPM, ля минор."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib")); os.chdir(os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from synth import *
def f_(n, o): return NOTE[n] * 2 ** (o - 4)
rng = np.random.default_rng(2026)

BARS = 16
BAR = 4 * BEAT
L = int(BARS * BAR * SR)
TAIL = int(4 * SR)
def buf(): return np.zeros((L + TAIL, 2))
def place(b, sig, beat, gain=1.0):
    i = int(beat * BEAT * SR); n = min(len(sig), len(b) - i)
    if n > 0: b[i:i + n] += sig[:n] * gain

# ---------- Гармония (своя прогрессия): Am9 | Fmaj9 | Dm9 | Esus7 -> E7(b9)
CH = [
    ("A", 2, [("G", 3), ("C", 4), ("E", 4), ("B", 4)]),
    ("F", 2, [("E", 3), ("A", 3), ("C", 4), ("G", 4)]),
    ("D", 2, [("F", 3), ("A", 3), ("C", 4), ("E", 4)]),
    ("E", 2, [("D", 3), ("A", 3), ("B", 3), ("E", 4)]),
]
E7b9 = [("D", 3), ("Ab", 3), ("B", 3), ("F", 4)]

keys, pad_b, bass, drums, lead = buf(), buf(), buf(), buf(), buf()
kicks = []

def pad(freqs, dur, attack=0.5):
    t = t_axis(dur); out = np.zeros((len(t), 2))
    for f in freqs:
        for det, pn in [(-0.005, -0.7), (0.005, 0.7)]:
            ph = 2 * np.pi * f * (1 + det) * t + rng.uniform(0, 6)
            out += mono_to_st(np.sin(ph) + 0.3 * np.sin(2 * ph) + 0.12 * np.sin(3 * ph), pn)
    e = np.clip(t / attack, 0, 1) ** 2
    rel = int(0.35 * len(t)); e[-rel:] *= np.linspace(1, 0, rel) ** 1.5
    out *= e[:, None] / (2 * len(freqs))
    return np.stack([filt(out[:, c], "low", 1600) for c in range(2)], axis=1)

for bar in range(BARS):
    root, ro, voic = CH[bar % 4]
    b0 = bar * 4
    # Rhodes: аккорд на 1, мягкий повтор на «и» третьей доли; на E-такте смена на E7(b9)
    v = voic
    for n, o in v:
        place(keys, rhodes(f_(n, o), dur=2.2, vel=0.6 + rng.uniform(-0.05, 0.05)), b0 + rng.uniform(0, 0.03), 0.22)
    v2 = E7b9 if bar % 4 == 3 else voic
    for n, o in v2:
        place(keys, rhodes(f_(n, o), dur=1.4, vel=0.45), b0 + 2.5 + rng.uniform(0, 0.03), 0.16)
    # пэд
    place(pad_b, pad([f_(n, o) for n, o in voic], BAR + 0.6), b0, 0.9)
    # суб-бас: корень на 1, подход на «и» 2, октава на 4
    fr = f_(root, ro) / 2 if f_(root, ro) > 90 else f_(root, ro)
    for beat, mult, d in [(0, 1, 1.4), (1.5, 1, 0.5), (3, 2 if bar % 2 else 1, 0.8)]:
        t = t_axis(d * BEAT + 0.05)
        s = np.sin(2 * np.pi * fr * mult * t) + 0.15 * np.sin(4 * np.pi * fr * mult * t)
        e = env_ad(len(t), 0.008, 0.6); rel = int(0.15 * len(t)); e[-rel:] *= np.linspace(1, 0, rel)
        place(bass, mono_to_st(np.tanh(1.5 * s * e)), b0 + beat, 0.5)

# ---------- Ударные
def kick():
    t = t_axis(0.45); f = 45 + 75 * np.exp(-t / 0.03)
    s = np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(len(t), 0.002, 0.14)
    s += filt(rng.standard_normal(len(t)), "low", 3000) * env_ad(len(t), 0.0005, 0.004) * 0.3
    return mono_to_st(np.tanh(1.4 * s))
def snap():
    n = int(0.25 * SR); x = np.zeros(n)
    for k in range(3):  # несколько микро-всплесков = щелчок пальцами/клэп
        i = int(k * 0.007 * SR); d = n - i
        x[i:] += filt(rng.standard_normal(d), "bp", (1100, 4500)) * env_ad(d, 0.0004, 0.006 if k < 2 else 0.05)
    return mono_to_st(x * 0.9, rng.uniform(-0.05, 0.05))
def hat(open_=False):
    n = int((0.18 if open_ else 0.05) * SR)
    x = filt(rng.standard_normal(n), "high", 7500) * env_ad(n, 0.0005, 0.06 if open_ else 0.012)
    return mono_to_st(x, 0.25)

SW = 0.58  # свинг шестнадцатых
def sixteenth(i):  # позиция i-й шестнадцатой в долях со свингом
    base = i // 2 * 0.5; return base + (0 if i % 2 == 0 else SW * 0.5)

for bar in range(BARS):
    b0 = bar * 4
    full = bar >= 8
    for kb in ([0, 2.75] if bar % 2 == 0 else [0, 1.75, 2.5]):
        place(drums, kick(), b0 + kb, 0.8); kicks.append((b0 + kb) * BEAT)
    for sb in (1, 3):
        place(drums, snap(), b0 + sb, 0.55)
    if full:
        for i in range(16):
            if i in (14,) and bar % 4 == 3:
                place(drums, hat(True), b0 + sixteenth(i), 0.18); continue
            vel = (0.22 if i % 4 == 0 else 0.13 if i % 2 == 0 else 0.08) * rng.uniform(0.8, 1.1)
            if i % 2 == 1 and rng.random() < 0.35: continue
            place(drums, hat(), b0 + sixteenth(i), vel)
    else:
        for i in range(0, 16, 2):
            place(drums, hat(), b0 + sixteenth(i), 0.07 * rng.uniform(0.8, 1.1))

# ---------- Мелодия Rhodes (такты 9–16), своя фраза на пентатонике ля минор
PHRASE = [  # (доля внутри 4-тактовой фразы, нота, октава, длительность)
    (1.0, "E", 5, 0.5), (1.5, "D", 5, 0.5), (2.0, "C", 5, 1.0), (3.5, "A", 4, 1.5),
    (5.0, "G", 4, 1.0), (6.0, "A", 4, 0.5), (6.5, "C", 5, 1.5),
    (9.0, "A", 4, 0.5), (9.5, "C", 5, 0.5), (10.0, "D", 5, 0.5), (10.5, "E", 5, 1.0), (11.5, "G", 5, 1.0),
    (13.0, "E", 5, 1.0), (14.0, "D", 5, 0.5), (14.5, "C", 5, 0.5), (15.0, "D", 5, 2.0),
]
for rep in range(2):
    off = (8 + rep * 4) * 4
    for beat, n, o, d in PHRASE:
        if rep == 1 and beat >= 13:  # вторая фраза заканчивается по-другому
            continue
        place(lead, rhodes(f_(n, o), dur=max(0.8, d * BEAT + 0.4), vel=0.75), off + beat, 0.28)
    if rep == 1:
        for beat, n, o in [(13.0, "C", 5), (13.5, "A", 4), (14.0, "G", 4), (15.0, "A", 4)]:
            place(lead, rhodes(f_(n, o), dur=1.2, vel=0.7), off + beat, 0.28)

# пинг-понг дилей на мелодию (пунктирная восьмая)
d = int(0.75 * BEAT * SR); dl = np.zeros_like(lead); src = lead.copy()
for k in range(1, 5):
    g = 0.38 ** k; sh = d * k
    tap = np.zeros_like(lead); tap[sh:] = src[:-sh] * g
    if k % 2: tap = tap[:, ::-1]
    dl += tap
dl = np.stack([filt(dl[:, c], "low", 3000) for c in range(2)], axis=1)
lead = lead + dl * 0.6

# ---------- «Дыхание» под бочку (сайдчейн) для пэда и клавиш
gate = np.ones(L + TAIL)
for kt in kicks:
    i = int(kt * SR); n = min(int(0.4 * SR), len(gate) - i)
    gate[i:i + n] *= 1 - 0.35 * np.exp(-np.arange(n) / SR / 0.12)
pad_b *= gate[:, None]; keys *= (0.5 + 0.5 * gate)[:, None]

# ---------- Атмосфера «спальни»: тихий шум винила
n = L + TAIL
hiss = filt(rng.standard_normal(n), "bp", (800, 6000)) * 0.004
cr = np.zeros(n); idx = rng.integers(0, n - 200, 600)
for i in idx: cr[i:i + 40] += rng.standard_normal(40) * np.exp(-np.arange(40) / 8) * rng.uniform(0.01, 0.04)
noise = mono_to_st(hiss + filt(cr, "low", 5000))

# ---------- Сведение
wet = reverb(keys * 1.0 + lead * 1.0 + pad_b * 0.6 + drums * np.array([0.15, 0.15]), mix=1.0, length=2.4, damp=4000)[:L + TAIL]
mixd = keys + pad_b + bass + drums + lead + noise + wet * 0.22
mixd = np.stack([filt(mixd[:, c], "low", 12000) for c in range(2)], axis=1)
mixd = np.tanh(1.3 * mixd / np.abs(mixd).max()) / np.tanh(1.3)

# бесшовная петля: хвост после конца добавляем в начало
loop = mixd[:L].copy(); loop[:TAIL] += mixd[L:L + TAIL]
loop = loop / np.abs(loop).max() * 10 ** (-1 / 20)
save("out/09_lobby/lobby_loop.wav", loop)
save("out/09_lobby/_preview_2x_loop.wav", np.vstack([loop, loop]))
rms = 20 * np.log10(np.sqrt((loop ** 2).mean()))
print(f"loop {L/SR:.2f}s, RMS {rms:.1f} dBFS, seam jump {np.abs(loop[0]-loop[-1]).max():.3f}")
