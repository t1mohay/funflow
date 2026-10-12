"""Перекладина, крепления, кольца и зажимы доски заказов — в стиле нового фона кухни
(тёмный металл как у стеллажа, серебристые детали как у цепочек и держателей бокалов).
Единица рисования = 1 единица доски заказов в GameUI; спрайты — 2 px на единицу.
Запуск: python gen_order_rack.py [папка UI]  ->  order_rod.png, order_rod_end.png, order_mount.png,
order_saddle.png, order_ring.png, order_clip.png, recipe_clip.png"""
import os, sys
import numpy as np
from PIL import Image
from gfx import *

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                                              "..", "..", "..", "Assets", "Resources", "UI"))
IRON = (62, 64, 70)          # как перекладины стеллажа
STEEL = (178, 182, 190)      # как цепочки таблички
SCREW = (160, 164, 172)


def save(c, name, rotate=0):
    im, _ = c.to_image()
    if rotate:
        im = im.rotate(rotate, expand=True)
    im.save(os.path.join(OUT_DIR, name))
    print(name, im.size)


def rod():
    """Кусок перекладины 32×15: тянется по ширине (контур только сверху и снизу).
    Рисуем вертикально (fill_cyl затеняет поперёк строки) и поворачиваем."""
    c = Canvas(0, -4, 15, 36)
    m = c.rrect(0, -8, 15, 40, 0)
    fill_cyl(c, m, IRON, amb=0.55, light_u=0.3, spec=0.6, spec_u=0.24, spec_w=0.1, line_w=0.9, line_dark=0.45, vgrad=0)
    # обрезаем по длине до 32, чтобы края были ровными
    c.rgb = c.rgb[4 * K:36 * K]; c.a = c.a[4 * K:36 * K]; c.h = c.rgb.shape[0]; c.oy = 0
    im, _ = c.to_image()
    im = im.rotate(90, expand=True).transpose(Image.FLIP_TOP_BOTTOM)   # свет сверху
    im.save(os.path.join(OUT_DIR, "order_rod.png")); print("order_rod.png", im.size)


def rod_end():
    """Набалдашник на конце: шарик с воротничком, 24×24."""
    c = Canvas(0, 0, 24, 24)
    collar = c.rrect(8.5, 4.5, 15.5, 19.5, 1.5)
    fill_cyl(c, collar, IRON, amb=0.55, spec=0.5, line_w=0.7, line_dark=0.45)
    ball = c.ellipse(4, 4, 20, 20)
    fill_soft(c, ball, IRON, height=4.0, amb=0.5, diff=0.65, spec=0.6, shin=22, line_w=0.8, line_dark=0.45)
    save(c, "order_rod_end.png")


def mount():
    """Настенная пластина за перекладиной: 26×54, два серебристых шурупа."""
    c = Canvas(0, 0, 26, 54)
    m = c.rrect(1, 1, 25, 53, 5)
    fill_soft(c, m, IRON, height=1.6, amb=0.58, diff=0.5, spec=0.25, line_w=0.8, line_dark=0.45)
    for y in (9, 45):
        s = c.ellipse(13 - 3.6, y - 3.6, 13 + 3.6, y + 3.6)
        fill_soft(c, s, SCREW, height=1.0, amb=0.6, diff=0.6, spec=0.5, line_w=0.5, line_dark=0.45)
        slot = c.line([(13 - 2.2, y - 1.4), (13 + 2.2, y + 1.4)], 0.8)
        c.tint(rgb((70, 72, 78)), slot * s)
    save(c, "order_mount.png")


def saddle():
    """Хомут поверх перекладины на креплении: 20×24."""
    c = Canvas(0, 0, 20, 24)
    m = c.rrect(1, 1, 19, 23, 3.5)
    fill_cyl(c, m, IRON, amb=0.55, spec=0.55, spec_w=0.12, line_w=0.8, line_dark=0.42)
    for y in (6, 18):
        band = c.rrect(1.5, y - 0.4, 18.5, y + 0.4, 0.2) * m
        c.tint(rgb(IRON) * 0.55, band * 0.7)
    save(c, "order_saddle.png")


def ring():
    """Серебристое кольцо, надетое на перекладину: 30×30, внутри прозрачно (видна перекладина)."""
    c = Canvas(0, 0, 30, 30)
    m = np.clip(c.ellipse(2, 2, 28, 28) - c.ellipse(7.2, 7.2, 22.8, 22.8), 0, 1)
    fill_soft(c, m, STEEL, height=2.4, amb=0.55, diff=0.6, spec=0.7, shin=24, line_w=0.7, line_dark=0.48)
    save(c, "order_ring.png")


def clip(hook=True):
    """Зажим листа, 100×68 (как прежний спрайт): серебристый крючок от кольца
    (центр кольца — 8 единиц от верха) и чёрный канцелярский зажим, держащий верх листа.
    hook=False — только сам зажим (100×40, верх на y=26) для большого листа рецепта: recipe_clip.png."""
    c = Canvas(0, 0, 100, 68) if hook else Canvas(0, 26, 100, 66)
    if hook:
        # проволочная петля-крючок: от кольца вниз к шарниру зажима
        for side in (-1, 1):
            w = c.line([(50 + side * 1.6, 8), (50 + side * 9, 22), (50 + side * 15, 33)], 2.6)
            fill_soft(c, w, STEEL, height=1.0, amb=0.6, diff=0.55, spec=0.6, line_w=0.5, line_dark=0.5)
        stem = c.rrect(47.2, 6, 52.8, 14, 2.6)
        fill_cyl(c, stem, STEEL, amb=0.6, spec=0.6, line_w=0.5, line_dark=0.5)
    # корпус зажима: трапеция, расширяется книзу, скруглённые углы
    xs_top, xs_bot, y0, y1 = 28, 40, 30, 62
    body = c.mask(lambda d, cc: d.rounded_rectangle([*cc.P(50 - xs_bot, y0), *cc.P(50 + xs_bot, y1)], radius=5 * K, fill=255))
    taper = c.poly([(50 - xs_bot - 1, y0 - 1), (50 - xs_top, y0 - 1), (50 - xs_bot - 1, y1 - 8)])
    taper2 = c.poly([(50 + xs_bot + 1, y0 - 1), (50 + xs_top, y0 - 1), (50 + xs_bot + 1, y1 - 8)])
    body = np.clip(body - taper - taper2, 0, 1)
    body = (blur(body, 0.6) > 0.5).astype(np.float32)
    fill_soft(c, body, IRON, height=3.0, amb=0.5, diff=0.6, spec=0.45, shin=16, line_w=0.9, line_dark=0.4)
    # губки: светлая кромка внизу
    lip = c.rrect(50 - xs_bot + 1.5, y1 - 6.5, 50 + xs_bot - 1.5, y1 - 4.6, 0.8) * body
    c.tint(rgb(IRON) * 1.8, lip * 0.55)
    # шарнир: серебристый пруток поперёк
    hinge = c.rrect(50 - xs_top - 4, y0 + 1.5, 50 + xs_top + 4, y0 + 6.5, 2.5)
    fill_soft(c, hinge, STEEL, height=1.6, amb=0.55, diff=0.6, spec=0.55, line_w=0.5, line_dark=0.5)
    hl = c.rrect(50 - xs_top - 2, y0 + 2.4, 50 + xs_top + 2, y0 + 3.4, 0.5) * hinge
    c.tint(np.ones(3, np.float32), hl * 0.5)
    # заклёпка по центру
    rv = c.ellipse(50 - 3.4, 46 - 3.4, 50 + 3.4, 46 + 3.4)
    fill_soft(c, rv, SCREW, height=1.0, amb=0.6, diff=0.6, spec=0.5, line_w=0.5, line_dark=0.45)
    save(c, "order_clip.png" if hook else "recipe_clip.png")


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    rod(); rod_end(); mount(); saddle(); ring(); clip(); clip(hook=False)
