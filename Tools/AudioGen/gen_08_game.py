"""Музыка раунда: петля 16 тактов в том же стиле, что лобби (R&B / нео-соул), но бодрее и плотнее.
104 BPM, ля минор, своя гармония и мелодия. Пишет out/08_game/game_loop.wav (и .ogg, если есть soundfile)."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib")); os.chdir(os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from synth import *
def f_(n, o): return NOTE[n] * 2 ** (o - 4)
rng = np.random.default_rng(1014)

GAME_BPM = 104
B = 60 / GAME_BPM          # своя доля: быстрее, чем в лобби
BARS = 16
BAR = 4 * B
L = int(BARS * BAR * SR)
TAIL = int(3 * SR)
def buf(): return np.zeros((L + TAIL, 2))
def place(b, sig, beat, gain=1.0):
    i = int(beat * B * SR); n = min(len(sig), len(b) - i)
    if n > 0: b[i:i + n] += sig[:n] * gain

# ---------- Гармония: Dm9 | Bbmaj7 | C6/9 | Am9
CH = [
    ("D", 2, [("F", 3), ("A", 3), ("C", 4), ("E", 4)]),
    ("Bb", 1, [("D", 3), ("F", 3), ("A", 3), ("C", 4)]),
    ("C", 2, [("E", 3), ("G", 3), ("A", 3), ("D", 4)]),
    ("A", 1, [("G", 3), ("C", 4), ("E", 4), ("B", 4)]),
]

keys, pad_b, bass, drums, lead = buf(), buf(), buf(), buf(), buf()
kicks = []

def pad(freqs, dur, attack=0.25):
    t = t_axis(dur); out = np.zeros((len(t), 2))
    for f in freqs:
        for det, pn in [(-0.006, -0.7), (0.006, 0.7)]:
            ph = 2 * np.pi * f * (1 + det) * t + rng.uniform(0, 6)
            out += mono_to_st(np.sin(ph) + 0.35 * np.sin(2 * ph) + 0.15 * np.sin(3 * ph), pn)
    e = np.clip(t / attack, 0, 1) ** 2
    rel = int(0.3 * len(t)); e[-rel:] *= np.linspace(1, 0, rel) ** 1.5
    out *= e[:, None] / (2 * len(freqs))
    return np.stack([filt(out[:, c], "low", 2200) for c in range(2)], axis=1)

for bar in range(BARS):
    root, ro, voic = CH[bar % 4]
    b0 = bar * 4
    # Rhodes: синкопированные «стабы» — на 1, на «и» 2 и на 4
    for beat, dur, vel, g in [(0, 1.2, 0.65, 0.2), (1.5, 0.6, 0.5, 0.15), (3, 0.9, 0.55, 0.16)]:
        for n, o in voic:
            place(keys, rhodes(f_(n, o), dur=dur, vel=vel + rng.uniform(-0.05, 0.05)), b0 + beat + rng.uniform(0, 0.02), g)
    place(pad_b, pad([f_(n, o) for n, o in voic], BAR + 0.4), b0, 0.7)
    # бас: восьмые с синкопой и октавными прыжками
    fr = f_(root, ro)
    while fr > 110: fr /= 2
    for beat, mult, d in [(0, 1, 0.7), (0.75, 1, 0.3), (1.5, 2, 0.4), (2, 1, 0.6), (2.75, 1, 0.3), (3.5, 2, 0.4)]:
        t = t_axis(d * B + 0.04)
        s = np.sin(2 * np.pi * fr * mult * t) + 0.2 * np.sin(4 * np.pi * fr * mult * t)
        e = env_ad(len(t), 0.006, 0.35); rel = int(0.2 * len(t)); e[-rel:] *= np.linspace(1, 0, rel)
        place(bass, mono_to_st(np.tanh(1.8 * s * e)), b0 + beat, 0.5)

# ---------- Ударные: плотнее и ровнее, чем в лобби
def kick():
    t = t_axis(0.35); f = 48 + 90 * np.exp(-t / 0.025)
    s = np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(len(t), 0.002, 0.11)
    s += filt(rng.standard_normal(len(t)), "low", 4000) * env_ad(len(t), 0.0005, 0.004) * 0.35
    return mono_to_st(np.tanh(1.6 * s))
def clap():
    n = int(0.22 * SR); x = np.zeros(n)
    for k in range(4):
        i = int(k * 0.006 * SR); d = n - i
        x[i:] += filt(rng.standard_normal(d), "bp", (1000, 5000)) * env_ad(d, 0.0004, 0.005 if k < 3 else 0.06)
    return mono_to_st(x, rng.uniform(-0.05, 0.05))
def hat(open_=False):
    n = int((0.16 if open_ else 0.04) * SR)
    x = filt(rng.standard_normal(n), "high", 8000) * env_ad(n, 0.0005, 0.05 if open_ else 0.01)
    return mono_to_st(x, 0.3)
def shaker():
    n = int(0.07 * SR)
    x = filt(rng.standard_normal(n), "bp", (5000, 11000)) * env_ad(n, 0.01, 0.02)
    return mono_to_st(x, -0.3)

for bar in range(BARS):
    b0 = bar * 4
    for kb in ([0, 1.75, 2.5] if bar % 2 == 0 else [0, 0.75, 2, 2.5]):
        place(drums, kick(), b0 + kb, 0.85); kicks.append((b0 + kb) * B)
    for sb in (1, 3):
        place(drums, clap(), b0 + sb, 0.5)
    for i in range(16):
        if i == 14 and bar % 2 == 1:
            place(drums, hat(True), b0 + i * 0.25, 0.16); continue
        vel = (0.2 if i % 4 == 0 else 0.13 if i % 2 == 0 else 0.09) * rng.uniform(0.85, 1.1)
        place(drums, hat(), b0 + i * 0.25, vel)
    if bar >= 4:
        for i in range(1, 16, 2):
            place(drums, shaker(), b0 + i * 0.25, 0.06 * rng.uniform(0.8, 1.2))

# ---------- Мелодия (с 5-го такта): короткие бодрые фразы на пентатонике ля минор
PHRASE = [  # (доля внутри 4-тактовой фразы, нота, октава, длительность)
    (0.5, "A", 4, 0.5), (1.0, "C", 5, 0.5), (1.5, "D", 5, 0.5), (2.0, "E", 5, 1.0), (3.5, "D", 5, 0.5),
    (4.5, "C", 5, 0.5), (5.0, "A", 4, 1.0), (6.5, "G", 4, 0.5), (7.0, "A", 4, 1.0),
    (8.5, "E", 5, 0.5), (9.0, "G", 5, 0.5), (9.5, "E", 5, 0.5), (10.0, "D", 5, 1.0), (11.5, "C", 5, 0.5),
    (12.5, "D", 5, 0.5), (13.0, "E", 5, 0.5), (13.5, "D", 5, 0.5), (14.0, "C", 5, 0.5), (14.5, "A", 4, 1.5),
]
for rep in range(3):
    off = (4 + rep * 4) * 4
    for beat, n, o, d in PHRASE:
        place(lead, rhodes(f_(n, o), dur=max(0.5, d * B + 0.25), vel=0.8), off + beat, 0.26)

# дилей на мелодию (пунктирная восьмая)
d = int(0.75 * B * SR); dl = np.zeros_like(lead); src = lead.copy()
for k in range(1, 4):
    g = 0.33 ** k; sh = d * k
    tap = np.zeros_like(lead); tap[sh:] = src[:-sh] * g
    if k % 2: tap = tap[:, ::-1]
    dl += tap
lead = lead + np.stack([filt(dl[:, c], "low", 3500) for c in range(2)], axis=1) * 0.5

# ---------- Сайдчейн под бочку
gate = np.ones(L + TAIL)
for kt in kicks:
    i = int(kt * SR); n = min(int(0.3 * SR), len(gate) - i)
    gate[i:i + n] *= 1 - 0.4 * np.exp(-np.arange(n) / SR / 0.09)
pad_b *= gate[:, None]; keys *= (0.55 + 0.45 * gate)[:, None]

# ---------- Сведение
wet = reverb(keys + lead + pad_b * 0.5 + drums * np.array([0.1, 0.1]), mix=1.0, length=1.8, damp=4500)[:L + TAIL]
mixd = keys + pad_b + bass + drums + lead + wet * 0.18
mixd = np.stack([filt(mixd[:, c], "low", 13000) for c in range(2)], axis=1)
mixd = np.tanh(1.4 * mixd / np.abs(mixd).max()) / np.tanh(1.4)

# бесшовная петля
loop = mixd[:L].copy(); loop[:TAIL] += mixd[L:L + TAIL]
loop = loop / np.abs(loop).max() * 10 ** (-1 / 20)
os.makedirs("out/08_game", exist_ok=True)
save("out/08_game/game_loop.wav", loop)
try:
    import soundfile
    # float32 и запись кусками: с float64 одним вызовом Vorbis-кодировщик пишет битый файл
    pcm = loop.astype(np.float32)
    with soundfile.SoundFile("out/08_game/game_loop.ogg", "w", SR, 2, format="OGG", subtype="VORBIS") as f:
        for i in range(0, len(pcm), SR):
            f.write(pcm[i:i + SR])
except ImportError:
    print("soundfile не установлен — OGG не записан, конвертируйте WAV через ffmpeg")
rms = 20 * np.log10(np.sqrt((loop ** 2).mean()))
print(f"loop {L/SR:.2f}s, RMS {rms:.1f} dBFS, seam jump {np.abs(loop[0]-loop[-1]).max():.3f}")
