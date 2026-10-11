"""Промах v2: короткий системный «дин-дун» в духе звука ошибки ОС, ля минор."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib")); os.chdir(os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from synth import *

def chime(f, dur=0.45, bright=1.0):
    """Стеклянно-мягкий удар молоточка: FM-колокольчик + чистый тон + немного Rhodes."""
    t = t_axis(dur)
    idx = 2.2 * bright * np.exp(-t / 0.04)
    bell = np.sin(2 * np.pi * f * t + idx * np.sin(2 * np.pi * f * 3.5 * t))
    pure = np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * f * 2 * t) * np.exp(-t / 0.08)
    sig = (0.55 * bell + 0.6 * pure) * env_ad(len(t), 0.0015, 0.16)
    rel = int(0.2 * len(t)); sig[-rel:] *= np.linspace(1, 0, rel) ** 2
    return sig

# ноты в записи фа минора, TRANSPOSE -> ля минор
VARIANTS = {
    1: [("C", 5), ("F", 4)],    # ми5 -> ля4 (кварта вниз — самый «системный»)
    2: [("Eb", 5), ("Bb", 4)],  # соль5 -> ре5
    3: [("C", 5), ("Ab", 4)],   # ми5 -> до5
}
gap = 0.11
for v, notes in VARIANTS.items():
    tr = []
    for k, (nn, oc) in enumerate(notes):
        f = hz(nn, oc)
        tr.append((mono_to_st(chime(f, bright=1.0 - 0.3 * k), pan=0.0), k * gap, 1.0 if k == 0 else 1.1))
        tr.append((rhodes(f, dur=0.45, vel=0.4), k * gap, 0.25))  # тепло в стиле трека
    st = mix(*tr)
    st = reverb(st, mix=0.1, length=0.5, damp=5000)
    st = warm(st, cutoff=9000, drive=1.1)
    st = finish(st, peak_db=-4, trim_db=-42, fade_ms=60)
    save(f"out/04_miss/miss_{v:02d}.wav", st); print(v, f"{len(st)/SR:.2f}s")
