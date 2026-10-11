"""Нож для ползунков меню паузы — в стиле картинок ингредиентов:
мягкие градиенты, контур на тон темнее заливки, блики. Лезвием вниз.
Запуск: python gen_knife.py  ->  Assets/Resources/UI/knife.png"""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import os

S = 4                      # суперсэмплинг для сглаживания
W, H = 128, 320            # итоговый размер
w, h = W * S, H * S

def bez(p0, p1, p2, p3, n=40):
    t = np.linspace(0, 1, n)[:, None]
    p0, p1, p2, p3 = map(np.array, (p0, p1, p2, p3))
    pts = (1 - t) ** 3 * p0 + 3 * (1 - t) ** 2 * t * p1 + 3 * (1 - t) * t ** 2 * p2 + t ** 3 * p3
    return [tuple(p) for p in pts]

def P(x, y): return (x * w, y * h)

def mask_of(poly=None, rrect=None, radius=0, ellipse=None):
    m = Image.new("L", (w, h), 0); d = ImageDraw.Draw(m)
    if poly: d.polygon(poly, fill=255)
    if rrect: d.rounded_rectangle(rrect, radius=radius, fill=255)
    if ellipse: d.ellipse(ellipse, fill=255)
    return m

def gradient(c0, c1, horizontal=True, x0=0.0, x1=1.0):
    """Линейный градиент по x (или y) между долями x0..x1 холста."""
    n = w if horizontal else h
    t = np.clip((np.arange(n) / n - x0) / (x1 - x0), 0, 1)
    t = t * t * (3 - 2 * t)
    c0, c1 = np.array(c0, float), np.array(c1, float)
    line = (c0[None] * (1 - t[:, None]) + c1[None] * t[:, None])
    arr = np.repeat(line[None], h, 0) if horizontal else np.repeat(line[:, None], w, 1)
    a = np.dstack([arr, np.full((h, w), 255.0)]).astype(np.uint8)
    return Image.fromarray(a, "RGBA")

def layer(img, fill_img, mask):
    img.paste(fill_img, (0, 0), mask)

def stroke(mask, color, width):
    """Контур по маске: расширяем маску и вычитаем исходную (контур снаружи + немного внутрь)."""
    grow = mask.filter(ImageFilter.MaxFilter(width * 2 + 1))
    shrink = mask.filter(ImageFilter.MinFilter(3))
    ring = Image.fromarray(np.clip(np.array(grow, int) - np.array(shrink, int), 0, 255).astype(np.uint8))
    out = Image.new("RGBA", (w, h), color + (0,)); out.putalpha(ring)
    return out

img = Image.new("RGBA", (w, h), (0, 0, 0, 0))

# ── лезвие: обух слева прямой, режущая кромка справа плавно сходится к острию внизу
spine_x, edge_x, top_y, tip = 0.30, 0.76, 0.455, (0.33, 0.985)
blade = [P(spine_x, top_y), P(edge_x, top_y)]
blade += bez(P(edge_x, top_y), P(edge_x + 0.01, 0.70), P(0.70, 0.88), P(*tip))[1:]
blade += bez(P(*tip), P(0.31, 0.95), P(spine_x, 0.90), P(spine_x, 0.80))[1:]
bm = mask_of(poly=blade)
layer(img, gradient((236, 241, 246), (168, 179, 192), True, spine_x, edge_x), bm)
# светлая заточка вдоль кромки
edge_band = [P(edge_x - 0.07, top_y + 0.01), P(edge_x, top_y)]
edge_band += bez(P(edge_x, top_y), P(edge_x + 0.01, 0.70), P(0.70, 0.88), P(*tip))[1:]
edge_band += bez(P(*tip), P(0.58, 0.86), P(edge_x - 0.06, 0.70), P(edge_x - 0.07, top_y + 0.01))[1:]
em = Image.fromarray((np.array(mask_of(poly=edge_band)) * (np.array(bm) > 0)).astype(np.uint8))
layer(img, gradient((250, 252, 255), (214, 222, 231), False, 0.45, 1.0), em)
# блик-полоса вдоль обуха
hl = mask_of(rrect=[P(0.355, 0.48)[0], P(0.355, 0.48)[1], P(0.40, 0.83)[0], P(0.40, 0.83)[1]], radius=int(0.02 * w))
hl = hl.filter(ImageFilter.GaussianBlur(S * 1.5))
white = Image.new("RGBA", (w, h), (255, 255, 255, 0)); white.putalpha(Image.fromarray((np.array(hl) * 0.75).astype(np.uint8)))
img.alpha_composite(white)
img.alpha_composite(stroke(bm, (84, 95, 110), 3 * S))

# ── больстер (стальная шейка)
bol = [P(0.27, 0.415)[0], P(0.27, 0.415)[1], P(0.79, 0.465)[0], P(0.79, 0.465)[1]]
bolm = mask_of(rrect=bol, radius=int(0.03 * w))
layer(img, gradient((226, 232, 238), (150, 160, 172), False, 0.415, 0.465), bolm)
img.alpha_composite(stroke(bolm, (84, 95, 110), 3 * S))

# ── деревянная рукоять
hr = [P(0.26, 0.015)[0], P(0.26, 0.015)[1], P(0.80, 0.42)[0], P(0.80, 0.42)[1]]
hm = mask_of(rrect=hr, radius=int(0.22 * w))
layer(img, gradient((214, 150, 88), (150, 88, 42), True, 0.26, 0.80), hm)
# волокна дерева
rng = np.random.default_rng(3)
grain = Image.new("RGBA", (w, h), (0, 0, 0, 0)); gd = ImageDraw.Draw(grain)
for k in range(7):
    x = (0.32 + k * 0.07 + rng.uniform(-0.01, 0.01)) * w
    pts = [(x + np.sin(y / h * 9 + k) * 0.012 * w, y) for y in np.linspace(0.04 * h, 0.40 * h, 30)]
    gd.line(pts, fill=(120, 68, 30, 70), width=2 * S)
grain.putalpha(Image.fromarray((np.array(grain.split()[3]) * (np.array(hm) > 0)).astype(np.uint8)))
img.alpha_composite(grain)
# блик на рукояти
hh = mask_of(rrect=[P(0.33, 0.05)[0], P(0.33, 0.05)[1], P(0.41, 0.36)[0], P(0.41, 0.36)[1]], radius=int(0.05 * w)).filter(ImageFilter.GaussianBlur(S * 2))
hw = Image.new("RGBA", (w, h), (255, 236, 210, 0)); hw.putalpha(Image.fromarray((np.array(hh) * 0.55).astype(np.uint8)))
img.alpha_composite(hw)
# заклёпки
for cy in (0.11, 0.22, 0.33):
    cx, r = 0.53, 0.06
    rm = mask_of(ellipse=[P(cx - r, cy - r * w / h)[0], P(cx - r, cy - r * w / h)[1], P(cx + r, cy + r * w / h)[0], P(cx + r, cy + r * w / h)[1]])
    layer(img, gradient((245, 247, 250), (150, 160, 172), False, cy - 0.03, cy + 0.03), rm)
    img.alpha_composite(stroke(rm, (95, 70, 45), 2 * S))
img.alpha_composite(stroke(hm, (92, 52, 24), 3 * S))

out = img.resize((W, H), Image.LANCZOS)
dst = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "UI", "knife.png")
out.save(os.path.normpath(dst))
print("saved", os.path.normpath(dst), out.size)
