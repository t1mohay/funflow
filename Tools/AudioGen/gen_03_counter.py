"""Счётчик: нота Rhodes, каждый шаг — следующая ступень F-минорной пентатоники."""
import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib")); os.chdir(os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from synth import *
OUT = "out/03_counter"
SCALE = [("F", 4), ("Ab", 4), ("Bb", 4), ("C", 5), ("Eb", 5), ("F", 5), ("Ab", 5), ("Bb", 5), ("C", 6), ("Eb", 6)]
rng = np.random.default_rng(3)
for i, (nn, oc) in enumerate(SCALE, 1):
    f = hz(nn, oc)
    note = rhodes(f, dur=0.55, vel=0.75 + 0.02 * i)
    oct_ = rhodes(f * 2, dur=0.35, vel=0.4)  # лёгкая октава сверху для «искры»
    n = int(0.03 * SR)
    click = mono_to_st(filt(rng.standard_normal(n), "bp", (2500, 6000)) * env_ad(n, 0.0003, 0.004))
    st = mix((note, 0, 1.0), (oct_, 0, 0.25), (click, 0, 0.15))
    st = reverb(st, mix=0.18, length=0.8)
    st = warm(st, cutoff=12000, drive=1.2)
    st = finish(st, peak_db=-5, trim_db=-40, fade_ms=80)
    save(f"{OUT}/counter_step_{i:02d}.wav", st)
# демо: серия из 10 начислений подряд на 1/16 при 90 BPM
demo = mix(*[(rhodes(hz(nn, oc), 0.55, 0.8), i * BEAT / 4, 1.0) for i, (nn, oc) in enumerate(SCALE)])
save(f"{OUT}/_demo_counter_run.wav", finish(warm(reverb(demo, mix=0.18, length=0.8)), peak_db=-4, fade_ms=100, trim_db=-40))
print("ok")
