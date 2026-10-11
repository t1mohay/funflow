"""Петля взмаха из записи ветра (whistlingwindstead54.mp3), подстроенная под музыку игры.
- опускаем на 2 полутона: свист (~375 Гц, нет в ля миноре) встаёт на ми — квинту аккорда;
- берём ровный участок 10.25–16.25 с (без порывов свиста) и делаем бесшовную петлю кроссфейдом 1 с;
- срезаем низ < 280 Гц (там бас и бочка музыки) и верх > 7 кГц;
- добавляем мягкие резонансы на нотах аккорда ля минор (E4, A4, C5, E5, A5): свист «звучит в тональности»;
- приводим громкость к −22 dBFS RMS: в игре множитель громкости задаёт Sfx.SetSwipe.
Запуск: python gen_04_swipe_wind.py путь/к/whistlingwindstead54.mp3"""
import os, subprocess, sys, tempfile
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfiltfilt, iirpeak, filtfilt, resample_poly

SR = 44100
START, LENGTH, XFADE = 10.25, 6.0, 1.0
CHORD = [329.63, 440.00, 523.25, 659.26, 880.00]   # ля минор: E4, A4, C5, E5, A5
SHIFT = (449, 400)  # замедление в 1.1225 раза = на 2 полутона ниже: свист ~375 Гц (≈ фа-диез) встаёт на ми (330 Гц)

src = sys.argv[1] if len(sys.argv) > 1 else "whistlingwindstead54.mp3"
tmp = os.path.join(tempfile.mkdtemp(), "wind.wav")
subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", src, "-ar", str(SR), "-ac", "2", tmp], check=True)
_, x = wavfile.read(tmp); x = x.astype(float) / 32768
x = np.stack([resample_poly(x[:, c], *SHIFT) for c in range(2)], 1)
START *= SHIFT[0] / SHIFT[1]

a, n, xf = int(START * SR), int(LENGTH * SR), int(XFADE * SR)
seg = x[a:a + n + xf].copy()

# EQ: место для баса/бочки и для звонких звуков (счётчик, комбо)
hp = butter(4, 280 / (SR / 2), "high", output="sos")
lp = butter(2, 7000 / (SR / 2), "low", output="sos")
seg = np.stack([sosfiltfilt(lp, sosfiltfilt(hp, seg[:, c])) for c in range(2)], 1)

# резонансы на нотах аккорда — ветер «подпевает» музыке, не споря с ней
tuned = np.zeros_like(seg)
for k, f0 in enumerate(CHORD):
    b, aa = iirpeak(f0, Q=30, fs=SR)
    tuned += np.stack([filtfilt(b, aa, seg[:, c]) for c in range(2)], 1) * (0.7 if f0 > 800 else 1.0)
seg = 0.55 * seg + 2.2 * tuned

# бесшовная петля: хвост после конца плавно накладывается на начало (равная мощность)
loop = seg[:n].copy()
ramp = np.linspace(0, np.pi / 2, xf)[:, None]
loop[:xf] = seg[:xf] * np.sin(ramp) + seg[n:n + xf] * np.cos(ramp)

loop *= 10 ** (-22 / 20) / np.sqrt((loop ** 2).mean())
peak = np.abs(loop).max()
if peak > 10 ** (-6 / 20): loop *= 10 ** (-6 / 20) / peak

out = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "Audio", "Swipe", "swipe_loop.wav"))
wavfile.write(out, SR, (loop * 32767).astype(np.int16))
jump = np.abs(loop[0] - loop[-1]).max(); step = np.abs(np.diff(loop, axis=0)).mean()
print(f"saved {out}: {LENGTH}s, RMS {20*np.log10(np.sqrt((loop**2).mean())):.1f} dBFS, seam jump {jump:.4f} vs step {step:.4f}")
