"""Общая библиотека синтеза: ночной R&B / нео-соул, F минор, 90 BPM."""
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve
from scipy.io import wavfile

SR = 44100
BPM = 86  # по анализу трека
BEAT = 60 / BPM
# Тональность трека — ля минор. Ноты в генераторах записаны в фа миноре, TRANSPOSE переносит их в ля минор
NOTE = {n: 440 * 2 ** ((i - 9) / 12) for i, n in enumerate(
    ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"])}

TRANSPOSE = 4  # фа минор -> ля минор (+4 полутона)

def hz(name, octave):
    return NOTE[name] * 2 ** (octave - 4 + TRANSPOSE / 12)

def t_axis(dur):
    return np.arange(int(dur * SR)) / SR

def env_ad(n, attack, decay, sr=SR):
    t = np.arange(n) / sr
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    return a * np.exp(-np.maximum(t - attack, 0) / decay)

def filt(x, kind, f, order=2):
    if kind == "bp":
        sos = butter(order, [f[0] / (SR / 2), f[1] / (SR / 2)], btype="band", output="sos")
    else:
        sos = butter(order, f / (SR / 2), btype=kind, output="sos")
    return sosfilt(sos, x)

def svf_sweep(x, f_start, f_end, q=2.0, curve=1.0):
    """Резонансный полосовой фильтр с плавающей частотой (взмах/свист)."""
    n = len(x)
    fr = f_start * (f_end / f_start) ** ((np.arange(n) / n) ** curve)
    out = np.zeros(n)
    low = band = 0.0
    damp = 1 / q
    for i in range(n):
        f = 2 * np.sin(np.pi * fr[i] / SR)
        high = x[i] - low - damp * band
        band += f * high
        low += f * band
        out[i] = band
    return out

def rhodes(freq, dur=1.6, vel=1.0, rng=None):
    """Электропиано Rhodes: FM-тело + звенящий «зубец» + лёгкое тремоло."""
    t = t_axis(dur)
    idx = (1.8 * vel) * np.exp(-t / 0.25) + 0.35
    body = np.sin(2 * np.pi * freq * t + idx * np.sin(2 * np.pi * freq * t))
    tine = 0.25 * vel * np.sin(2 * np.pi * freq * 14.0 * t) * np.exp(-t / 0.03)
    bark = 0.12 * vel * np.sin(2 * np.pi * freq * 2 * t) * np.exp(-t / 0.15)
    sig = (body + tine + bark) * env_ad(len(t), 0.002, 0.55 + 0.25 * (220 / freq) ** 0.3)
    sig = np.tanh(1.3 * sig) / np.tanh(1.3)
    rel = int(0.25 * len(t)); sig[-rel:] *= np.linspace(1, 0, rel) ** 2  # плавный релиз, без щелчка
    # стерео-тремоло (автопан), как у Rhodes Suitcase
    lfo = 0.5 + 0.18 * np.sin(2 * np.pi * 4.2 * t)
    return np.stack([sig * lfo, sig * (1 - lfo)], axis=1)

_IR_CACHE = {}
def reverb(st, mix=0.25, length=1.6, damp=4500, seed=7):
    key = (length, damp, seed)
    if key not in _IR_CACHE:
        r = np.random.default_rng(seed)
        n = int(length * SR)
        t = np.arange(n) / SR
        ir = []
        for ch in range(2):
            noise = r.standard_normal(n) * np.exp(-t / (length / 6.9))
            noise = filt(noise, "low", damp)
            noise[: int(0.012 * SR)] *= np.linspace(0, 1, int(0.012 * SR))  # pre-delay feel
            ir.append(noise / np.sqrt(np.sum(noise ** 2)))
        _IR_CACHE[key] = ir
    ir = _IR_CACHE[key]
    pad = np.zeros((len(ir[0]), 2))
    dry = np.vstack([st, pad])
    wet = np.stack([fftconvolve(dry[:, c], ir[c])[: len(dry)] for c in range(2)], axis=1)
    return (1 - mix) * dry + mix * wet * 1.6

def mono_to_st(x, pan=0.0):
    l = np.cos((pan + 1) * np.pi / 4)
    r = np.sin((pan + 1) * np.pi / 4)
    return np.stack([x * l, x * r], axis=1)

def mix(*tracks):
    n = max(len(tr[0]) + int(tr[1] * SR) for tr in tracks)
    out = np.zeros((n, 2))
    for sig, offset, gain in tracks:
        o = int(offset * SR)
        out[o:o + len(sig)] += sig * gain
    return out

def warm(st, cutoff=11000, drive=1.2):
    st = np.stack([filt(st[:, c], "low", cutoff) for c in range(2)], axis=1)
    return np.tanh(drive * st) / np.tanh(drive)

def finish(st, peak_db=-3.0, fade_ms=40, trim_db=-60):
    st = st - st.mean(axis=0)
    st = st / (np.abs(st).max() + 1e-9) * 10 ** (peak_db / 20)
    # обрезать хвост тише порога
    a = np.abs(st).max(axis=1)
    idx = np.where(a > 10 ** ((trim_db + peak_db) / 20))[0]
    st = st[: idx[-1] + 1] if len(idx) else st
    f = min(int(fade_ms / 1000 * SR), len(st))
    st[-f:] *= np.linspace(1, 0, f)[:, None]
    return st

def save(path, st):
    wavfile.write(path, SR, (np.clip(st, -1, 1) * 32767).astype(np.int16))
