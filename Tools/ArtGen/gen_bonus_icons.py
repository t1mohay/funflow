"""Иконки бонусных объектов (способностей) — в стиле картинок ингредиентов:
мягкие градиенты, контур на тон темнее заливки, блики.
Запуск: python gen_bonus_icons.py  ->  Assets/Resources/Ingredients/bonus_*.png"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = 4                    # суперсэмплинг для сглаживания
N = 512                  # итоговый размер, как у ингредиентов
W = N * S
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "Ingredients")


def P(x, y):
    return (x * W, y * W)


def mask(draw_fn):
    m = Image.new("L", (W, W), 0)
    draw_fn(ImageDraw.Draw(m))
    return m


def radial(c_center, c_edge, cx=0.42, cy=0.38, r=0.6):
    """Радиальный градиент: светлая середина ближе к верхнему левому краю — свет сверху слева."""
    y, x = np.mgrid[0:W, 0:W] / W
    t = np.clip(np.hypot(x - cx, y - cy) / r, 0, 1)
    t = t * t * (3 - 2 * t)
    c0, c1 = np.array(c_center, float), np.array(c_edge, float)
    arr = c0[None, None] * (1 - t[..., None]) + c1[None, None] * t[..., None]
    a = np.dstack([arr, np.full((W, W), 255.0)]).astype(np.uint8)
    return Image.fromarray(a, "RGBA")


def vertical(c_top, c_bottom, y0=0.0, y1=1.0):
    t = np.clip((np.arange(W) / W - y0) / (y1 - y0), 0, 1)
    c0, c1 = np.array(c_top, float), np.array(c_bottom, float)
    line = c0[None] * (1 - t[:, None]) + c1[None] * t[:, None]
    arr = np.repeat(line[:, None], W, 1)
    a = np.dstack([arr, np.full((W, W), 255.0)]).astype(np.uint8)
    return Image.fromarray(a, "RGBA")


def stroke(m, color, width):
    grow = m.filter(ImageFilter.MaxFilter(width * 2 + 1))
    shrink = m.filter(ImageFilter.MinFilter(3))
    ring = Image.fromarray(np.clip(np.array(grow, int) - np.array(shrink, int), 0, 255).astype(np.uint8))
    out = Image.new("RGBA", (W, W), color + (0,))
    out.putalpha(ring)
    return out


def shine(img, box, alpha=110, blur=10):
    """Мягкий белый блик-эллипс."""
    m = mask(lambda d: d.ellipse([c * W for c in box], fill=alpha))
    m = m.filter(ImageFilter.GaussianBlur(blur * S))
    white = Image.new("RGBA", (W, W), (255, 255, 255, 0))
    white.putalpha(m)
    img.alpha_composite(white)


def fill(img, src, m):
    img.paste(src, (0, 0), m)


def save(img, name):
    img = img.resize((N, N), Image.LANCZOS)
    path = os.path.join(OUT, name + ".png")
    img.save(path)
    print("saved", os.path.normpath(path))


# ───────────── Замедление: песочные часы ─────────────
def hourglass():
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    # стекло: две колбы, сходящиеся к горлышку
    glass_poly = [P(0.27, 0.2), P(0.73, 0.2), P(0.72, 0.3), P(0.53, 0.48), P(0.53, 0.52), P(0.72, 0.70),
                  P(0.73, 0.8), P(0.27, 0.8), P(0.28, 0.7), P(0.47, 0.52), P(0.47, 0.48), P(0.28, 0.3)]
    gm = mask(lambda d: d.polygon(glass_poly, fill=255)).filter(ImageFilter.GaussianBlur(S * 2))
    gm = gm.point(lambda v: 255 if v > 127 else 0)
    glass = Image.new("RGBA", (W, W), (190, 225, 255, 120))
    fill(img, glass, gm)
    # песок: верхняя кучка и нижняя горка + струйка
    sand_top = [P(0.36, 0.36), P(0.64, 0.36), P(0.52, 0.47), P(0.48, 0.47)]
    sand_bottom = [P(0.29, 0.775), P(0.71, 0.775), P(0.66, 0.70), P(0.5, 0.62), P(0.34, 0.70)]
    sm = mask(lambda d: (d.polygon(sand_top, fill=255), d.polygon(sand_bottom, fill=255),
                         d.rectangle([*P(0.493, 0.47), *P(0.507, 0.66)], fill=255)))
    fill(img, vertical((255, 214, 120), (226, 150, 50), 0.35, 0.8), sm)
    img.alpha_composite(stroke(gm, (70, 120, 160), 3 * S))
    shine(img, (0.31, 0.23, 0.38, 0.33), 150, 3)
    shine(img, (0.31, 0.62, 0.36, 0.74), 110, 3)
    # деревянные крышки сверху и снизу и стойки
    for y0, y1 in ((0.13, 0.21), (0.79, 0.87)):
        cm = mask(lambda d: d.rounded_rectangle([*P(0.2, y0), *P(0.8, y1)], radius=0.03 * W, fill=255))
        fill(img, vertical((196, 120, 64), (122, 66, 30), y0, y1), cm)
        img.alpha_composite(stroke(cm, (82, 42, 16), 3 * S))
        shine(img, (0.26, y0 + 0.012, 0.6, y0 + 0.035), 120, 2)
    for x in (0.23, 0.73):
        pm = mask(lambda d: d.rounded_rectangle([*P(x, 0.2), *P(x + 0.04, 0.8)], radius=0.02 * W, fill=255))
        fill(img, vertical((176, 104, 52), (120, 64, 28)), pm)
        img.alpha_composite(stroke(pm, (82, 42, 16), 2 * S))
    save(img, "bonus_slow")


# ───────────── Множитель: золотая монета «×2» ─────────────
def coin():
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    outer = mask(lambda d: d.ellipse([*P(0.1, 0.1), *P(0.9, 0.9)], fill=255))
    fill(img, radial((255, 236, 140), (214, 140, 20)), outer)
    inner = mask(lambda d: d.ellipse([*P(0.19, 0.19), *P(0.81, 0.81)], fill=255))
    fill(img, radial((255, 214, 90), (232, 160, 30), 0.55, 0.6, 0.55), inner)
    img.alpha_composite(stroke(inner, (176, 110, 10), 3 * S))
    img.alpha_composite(stroke(outer, (130, 78, 6), 4 * S))
    # «×2» — толстыми линиями, без шрифта: крестик и двойка
    tm = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(tm)
    lw = int(0.07 * W)
    cross = [(0.255, 0.42), (0.395, 0.6), (0.395, 0.42), (0.255, 0.6)]
    d.line([P(*cross[0]), P(*cross[1])], fill=255, width=lw)
    d.line([P(*cross[2]), P(*cross[3])], fill=255, width=lw)
    # двойка — одна ломаная: дуга сверху (200°…380°), диагональ вниз влево, основание вправо.
    # Всё внутри внутреннего круга монеты, по высоте — по центру вместе с крестиком.
    cx, cy, r = 0.575, 0.415, 0.105
    two = [(cx + r * np.cos(np.radians(a)), cy + r * np.sin(np.radians(a))) for a in np.linspace(200, 380, 28)]
    two += [(0.465, 0.665), (0.70, 0.665)]
    d.line([P(*p) for p in two], fill=255, width=lw, joint="curve")
    for c in [P(*p) for p in cross] + [P(*two[0]), P(*two[-2]), P(*two[-1])]:
        r = lw / 2
        d.ellipse([c[0] - r, c[1] - r, c[0] + r, c[1] + r], fill=255)
    shadow = Image.new("RGBA", (W, W), (150, 80, 0, 0))
    shadow.putalpha(tm.point(lambda v: v * 0.8))
    img.alpha_composite(shadow, (int(0.012 * W), int(0.016 * W)))
    fill(img, vertical((255, 255, 236), (255, 236, 170), 0.3, 0.7), tm)
    img.alpha_composite(stroke(tm, (168, 96, 8), 2 * S))
    shine(img, (0.22, 0.16, 0.55, 0.32), 120, 8)
    save(img, "bonus_double")


# ───────────── Щит ─────────────
def shield():
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))

    def shape(m, k):
        """Контур щита, сжатый к центру на долю k."""
        cx, cy = 0.5, 0.5

        def Q(x, y):
            return P(cx + (x - cx) * k, cy + (y - cy) * k)
        pts = [Q(0.5, 0.1), Q(0.66, 0.16), Q(0.85, 0.18), Q(0.84, 0.46), Q(0.78, 0.64), Q(0.66, 0.78),
               Q(0.5, 0.9), Q(0.34, 0.78), Q(0.22, 0.64), Q(0.16, 0.46), Q(0.15, 0.18), Q(0.34, 0.16)]
        ImageDraw.Draw(m).polygon(pts, fill=255)
        return m.filter(ImageFilter.GaussianBlur(S * 3)).point(lambda v: 255 if v > 127 else 0)

    outer = shape(Image.new("L", (W, W), 0), 1.0)
    inner = shape(Image.new("L", (W, W), 0), 0.8)
    fill(img, radial((230, 236, 245), (130, 145, 165)), outer)
    fill(img, radial((110, 190, 255), (30, 90, 180), 0.42, 0.35, 0.55), inner)
    img.alpha_composite(stroke(inner, (30, 60, 120), 3 * S))
    img.alpha_composite(stroke(outer, (70, 80, 100), 4 * S))
    # белая галочка — «всё лишнее отскакивает»
    cm = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(cm)
    lw = int(0.08 * W)
    pts = [P(0.34, 0.48), P(0.46, 0.61), P(0.67, 0.36)]
    d.line(pts, fill=255, width=lw, joint="curve")
    for c in (pts[0], pts[2]):
        r = lw / 2
        d.ellipse([c[0] - r, c[1] - r, c[0] + r, c[1] + r], fill=255)
    fill(img, Image.new("RGBA", (W, W), (255, 255, 255, 255)), cm)
    img.alpha_composite(stroke(cm, (30, 60, 120), 2 * S))
    shine(img, (0.24, 0.2, 0.46, 0.4), 110, 8)
    save(img, "bonus_shield")


if __name__ == "__main__":
    hourglass()
    coin()
    shield()
