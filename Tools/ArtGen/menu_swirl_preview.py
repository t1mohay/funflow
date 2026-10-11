"""Превью шейдера RecipeSlicer/MenuSwirl (Assets/Resources/Shaders/MenuSwirl.shader) — та же математика на numpy.
Удобно подбирать цвета и параметры без Unity. Запуск: python menu_swirl_preview.py  ->  menu_swirl.gif"""
import math
import numpy as np
from PIL import Image

def hexc(h): return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], float) / 255
DEEP, WARM, VEIN, SHADOW, GLOSS = map(hexc, ["#4F6B4A", "#C8643B", "#D9A441", "#3B3430", "#F6EBD3"])
SCALE, SWIRL, WARP = 4.5, 3.0, 0.7
LOOP = 120.0                      # секунд: всё движение бесшовно повторяется
SPIN = 2 * math.pi / LOOP         # один оборот за петлю
FLOW = 2 * math.pi * 7 / LOOP     # волны течения: 8, 6 и 9 циклов за петлю (коэффициенты 8/7, 6/7, 9/7)

def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)

def gauss(x, c, w): return np.exp(-((x - c) / w) ** 2)

def frame(t, W=480, H=270):
    y, x = np.mgrid[0:H, 0:W]
    px = (x / W - 0.5) * (W / H) * SCALE; py = (0.5 - y / H) * SCALE
    r = np.hypot(px, py); a = np.arctan2(py, px) + SWIRL / (r + 0.7) + t * SPIN
    qx, qy = np.cos(a) * r, np.sin(a) * r; ft = t * FLOW
    for k in range(6):
        qx, qy = qx + WARP * np.sin(qy * 2.0 + ft * 8 / 7 + k * 1.3), qy + WARP * np.cos(qx * 1.8 - ft * 6 / 7 + k * 0.7)
    f1 = 0.5 + 0.5 * np.sin(qx * 1.6 + qy * 1.2)
    f2 = 0.5 + 0.5 * np.sin(np.hypot(qx, qy) * 2.24 - ft * 9 / 7)
    m = smoothstep(0.45, 0.55, f1)[..., None]; col = DEEP * (1 - m) + WARM * m
    v = (gauss(f2, 0.5, 0.06) * smoothstep(0.3, 0.7, f1))[..., None]; col = col + (VEIN - col) * 0.8 * v
    sh = (smoothstep(0.0, 0.4, f2) * smoothstep(1.0, 0.6, f2))[..., None]; col = SHADOW + (col - SHADOW) * (0.5 + 0.5 * sh)
    g = (gauss(f1, 0.9, 0.03) * 0.5)[..., None]; col = col + (GLOSS - col) * g
    col *= (1 - 0.35 * np.clip(r / (SCALE * 0.9), 0, 1) ** 2)[..., None]
    return Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8))

if __name__ == "__main__":
    fr = [frame(t) for t in np.arange(0, 16, 0.2)]
    fr[0].save("menu_swirl.gif", save_all=True, append_images=fr[1:], duration=80, loop=0)
    print("menu_swirl.gif", len(fr), "frames")
