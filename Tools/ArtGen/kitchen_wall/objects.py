"""Предметы кухни в стиле игры. Каждая функция возвращает Canvas (координаты исходника 1024×572)."""
import math
import numpy as np
from gfx import *

import os
R = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "Assets", "Resources")) + os.sep
ING = R + "Ingredients/"
CREAM = (250, 240, 214)


# ───────────────────────── общие детали
def label_on(c, body, x0, y0, x1, y1, icon=None, color=CREAM, r=1.6, icon_fill=0.86, border=None):
    lm = c.rrect(x0, y0, x1, y1, r) * body
    fill_cyl(c, lm, color, amb=0.72, spec=0.25, umask=body, line=False)
    if border is not None:
        inner = c.rrect(x0 + 0.9, y0 + 0.9, x1 - 0.9, y1 - 0.9, max(0.3, r - 0.8))
        c.tint(rgb(border), (lm - inner).clip(0, 1) * 0.9)
    outline(c, lm, color, 0.35, 0.72)
    if icon:
        sprite(c, icon, (x0 + 0.8, y0 + 0.8, x1 - 0.8, y1 - 0.8), fill=icon_fill, clip=lm)


def squeeze_bottle(cx, top, bottom, hw, body, cap, icon=None, label=True):
    """Мягкая бутылка-дозатор (кетчуп, горчица): корпус + конусный носик."""
    c = Canvas(cx - hw - 3, top - 2, cx + hw + 3, bottom + 2)
    h = bottom - top
    sh = top + h * 0.33                        # начало корпуса
    prof = [(sh - h * 0.06, hw * 0.55), (sh, hw * 0.88), (sh + h * 0.07, hw), (bottom - 2.5, hw), (bottom - 0.6, hw * 0.93), (bottom, hw * 0.8)]
    bm = c.poly(lathe(cx, prof))
    fill_cyl(c, bm, body, amb=0.6, spec=0.6, spec_w=0.08)
    # колпачок: кольцо + конус
    ring_top = sh - h * 0.1
    cm = c.poly(lathe(cx, [(ring_top, hw * 0.62), (ring_top + 0.6, hw * 0.66), (sh - h * 0.02, hw * 0.66), (sh + 0.4, hw * 0.6)]))
    fill_cyl(c, cm, cap, amb=0.58, spec=0.45)
    nm = c.poly(lathe(cx, [(top, hw * 0.06), (top + 1.0, hw * 0.12), (top + h * 0.12, hw * 0.24), (ring_top + 0.3, hw * 0.5)]))
    fill_cyl(c, nm, cap, amb=0.6, spec=0.5)
    if label:
        label_on(c, bm, cx - hw * 0.72, sh + h * 0.2, cx + hw * 0.72, bottom - h * 0.12, icon)
    return c


def glass_bottle(cx, top, bottom, prof, liquid, level, glass=(214, 236, 226), cap=None, cap_h=0, label=None, icon=None,
                 glass_alpha=0.32, cap_kind="screw"):
    """Стеклянная бутылка: жидкость внутри, стекло, этикетка, блики."""
    hw = max(p[1] for p in prof)
    c = Canvas(cx - hw - 3, top - 2, cx + hw + 3, bottom + 2)
    bm = c.poly(lathe(cx, prof))
    inner_prof = [(y, max(0.0, w - 0.9)) for y, w in prof]
    im = c.poly(lathe(cx, inner_prof))
    H, W = bm.shape
    ys = (np.arange(H) / K + c.oy)[:, None]
    lm = im * (ys >= level)
    # пустая часть — светлое стекло
    fill_cyl(c, bm, glass, amb=0.85, spec=0, alpha=glass_alpha + 0.12, line=False)
    fill_cyl(c, lm, liquid, amb=0.62, spec=0.0, line=False, umask=bm)
    # мениск
    men = c.ellipse(cx - hw + 1.2, level - 0.7, cx + hw - 1.2, level + 0.7) * im
    c.tint(rgb(liquid) * 1.25, men * 0.7)
    if label:
        x0, y0, x1, y1 = label
        label_on(c, bm, x0, y0, x1, y1, icon)
    # блики стекла
    u = row_extent(bm)
    v = col_extent(bm)
    s1 = np.clip(1 - np.abs(u - 0.2) / 0.07, 0, 1) * np.clip(np.minimum(v - 0.05, 0.95 - v) / 0.08, 0, 1)
    s2 = np.clip(1 - np.abs(u - 0.8) / 0.035, 0, 1) * np.clip(np.minimum(v - 0.3, 0.9 - v) / 0.08, 0, 1)
    c.tint(np.ones(3, np.float32), blur((s1 * 0.75 + s2 * 0.35) * bm, 0.2))
    outline(c, bm, liquid, 0.5, 0.45)
    if cap:
        ct = top
        cm = c.rrect(cx - prof[0][1] - 0.7, ct, cx + prof[0][1] + 0.7, ct + cap_h, 0.8)
        fill_cyl(c, cm, cap, amb=0.6, spec=0.45)
        if cap_kind == "screw":
            for k in range(1, 4):
                yy = ct + cap_h * k / 4
                rib = c.rrect(cx - prof[0][1] - 0.5, yy - 0.15, cx + prof[0][1] + 0.5, yy + 0.15, 0.1) * cm
                c.tint(rgb(cap) * 0.6, rib * 0.6)
    return c


def pot(c, cx, top, bottom, hw_top, hw_bot, color, rim_h=4.5, rim_out=1.6):
    body = c.poly(lathe(cx, [(top + rim_h * 0.6, hw_top), (bottom - 1.2, hw_bot), (bottom, hw_bot - 1.0)]))
    fill_cyl(c, body, color, amb=0.62, spec=0.35, spec_w=0.1)
    rim = c.rrect(cx - hw_top - rim_out, top, cx + hw_top + rim_out, top + rim_h, 1.4)
    fill_cyl(c, rim, tuple(min(255, int(v * 1.06)) for v in color), amb=0.66, spec=0.4)
    # тень под ободом
    sh = c.rrect(cx - hw_top, top + rim_h, cx + hw_top, top + rim_h + 1.6, 0.6) * body
    c.tint(rgb(color) * 0.6, blur(sh, 0.4) * 0.6)
    # земля
    soil = c.ellipse(cx - hw_top + 0.6, top - 0.6, cx + hw_top - 0.6, top + 1.4)
    fill_soft(c, soil, (92, 62, 40), height=0.6, spec=0.05, line=False)


def leaf_fan(c, cx, base_y, leaves, base=(104, 172, 70)):
    for ang, ln, wd, dx in leaves:
        leaf(c, cx + dx, base_y, ang, ln, wd, base=base, shape="lance")


def vine(c, pts, leaves, base=(112, 178, 72), stem=(70, 120, 50)):
    sm = c.line(pts, 0.7)
    fill_cyl(c, sm, stem, amb=0.7, spec=0, line=False)
    for (x, y, ang, ln, wd) in leaves:
        leaf(c, x, y, ang, ln, wd, base=base, shape="ivy")


# ───────────────────────── полка (пол полки y=119.6)
SHELF = 119.6
TERRA = (206, 112, 70)
POT_CREAM = (238, 226, 204)


def trailing_plant(cx, mirror=False):
    """Горшок с плющом; длинные плети свисают через край полки."""
    sgn = -1 if mirror else 1
    x0 = cx - 46 if not mirror else cx - 30
    x1 = cx + 30 if not mirror else cx + 46
    pc = Canvas(cx - 22, 84, cx + 22, SHELF + 2)
    pot(pc, cx, 90, SHELF, 16, 12, TERRA)
    c = Canvas(cx - 46, 55, cx + 46, 212)
    rng = np.random.default_rng(7 if not mirror else 11)
    # шапка листьев над горшком
    crown = []
    ccx, ccy = cx - sgn * 6, 84
    for k in range(34):
        ang = rng.uniform(-200, 20)
        rr = math.sqrt(rng.random())
        bx = ccx + math.cos(math.radians(ang)) * 24 * rr
        by = ccy + math.sin(math.radians(ang)) * 11 * rr
        out = math.degrees(math.atan2(by - (ccy + 6), (bx - ccx) * 0.6)) + rng.normal(0, 18)
        ln = 10 + 3.5 * rng.random()
        crown.append((by, bx, by, out, ln))
    for _, bx, by, a, ln in sorted(crown):
        leaf(c, bx, by, a, ln, ln * 0.52, base=(108 + int(rng.integers(-14, 14)), 178 + int(rng.integers(-10, 8)), 70), shape="ivy")
    # плети: от края горшка наружу и вниз
    edge = cx - sgn * 15
    paths = [
        [(edge, 94), (edge - sgn * 12, 104), (edge - sgn * 17, 128), (edge - sgn * 20, 160), (edge - sgn * 24, 198)],
        [(edge + sgn * 1, 97), (edge - sgn * 6, 114), (edge - sgn * 7, 142), (edge - sgn * 11, 176)],
        [(edge + sgn * 9, 100), (edge + sgn * 8, 118), (edge + sgn * 3, 134)],
    ]
    for p in paths:
        pts = np.array(p, float)
        leaves = []
        tot = len(pts) - 1
        n = 13
        for k in range(1, n + 1):
            t = k / n
            seg = min(int(t * tot), tot - 1); f = t * tot - seg
            x, y = pts[seg] * (1 - f) + pts[seg + 1] * f
            side = 1 if k % 2 else -1
            sz = 10.5 - 3.5 * t
            leaves.append((x, y, 90 + side * 58 + rng.normal(0, 10), sz, sz * 0.52))
        vine(c, [tuple(q) for q in pts], leaves)
    return pc, c


def fan_plant(cx, top):
    """Кремовый горшок с ланцетными листьями (как базилик/спатифиллум)."""
    c = Canvas(cx - 36, top - 4, cx + 36, SHELF + 2)
    by = 91
    leaves = [(-172, 22, 8.5, -4), (-8, 22, 8.5, 4), (-150, 27, 9.5, -3), (-30, 27, 9.5, 3),
              (-128, 31, 10, -2), (-52, 31, 10, 2), (-108, 34, 10, -1), (-72, 34, 10, 1), (-90, 36, 10.5, 0)]
    leaf_fan(c, cx, by, leaves)
    pot(c, cx, 88, SHELF, 20, 15, POT_CREAM)
    return c


def olive_oil():
    cx = 428.5
    prof = [(36.5, 4.3), (50, 4.3), (56, 9.5), (61, 14.6), (64, 15.2), (117.5, 15.2), (119.6, 14.0)]
    return glass_bottle(cx, 32, SHELF, prof, liquid=(178, 170, 40), level=63, glass=(196, 222, 180),
                        cap=(120, 82, 54), cap_h=6.2, label=(cx - 12, 74, cx + 12, 101), icon=ING + "olive.png", cap_kind="cork")


def vinegar():
    cx = 465
    prof = [(42, 3.6), (55, 3.6), (60, 8), (66, 12.6), (69, 13.4), (117.5, 13.4), (119.6, 12.4)]
    c = glass_bottle(cx, 36, SHELF, prof, liquid=(214, 150, 60), level=72, glass=(232, 226, 210),
                     cap=(170, 176, 186), cap_h=6.5, label=(cx - 10.5, 82, cx + 10.5, 105), icon=ING + "apple.png")
    # носик-дозатор
    sp = c.poly([(cx - 1.0, 36.5), (cx - 0.6, 30.5), (cx + 1.6, 29.5), (cx + 1.4, 36.5)])
    fill_cyl(c, sp, (170, 176, 186), amb=0.6, spec=0.5)
    return c


def soy():
    cx = 498.5
    prof = [(72, 3.6), (77, 3.6), (84, 5.0), (96, 8.5), (110, 11.6), (118, 12.0), (119.6, 11.0)]
    c = glass_bottle(cx, 69, SHELF, prof, liquid=(70, 36, 22), level=79, glass=(222, 214, 205),
                     cap=(210, 52, 40), cap_h=7.0, glass_alpha=0.3)
    # белая ромбовидная наклейка
    m = c.poly(lathe(cx, [(96, 8.5), (110, 11.6)])) * 0
    lab = c.poly([(cx, 96), (cx + 6, 104), (cx, 112), (cx - 6, 104)])
    fill_soft(c, lab, CREAM, height=0.6, spec=0.1, line_w=0.3)
    return c


def sprite_obj(path, box):
    x0, y0, x1, y1 = box
    c = Canvas(x0 - 2, y0 - 2, x1 + 2, y1 + 2)
    sprite(c, path, box, fill=1.0)
    return c


def cookie_jar(cx=725.5):
    top, hw = 75, 13.6
    c = Canvas(cx - hw - 3, top - 2, cx + hw + 3, SHELF + 2)
    body = c.rrect(cx - hw, top + 7, cx + hw, SHELF, 3.2)
    fill_cyl(c, body, (226, 240, 236), amb=0.85, spec=0, alpha=0.42, line=False)
    # печенье внутри — игровой спрайт
    inner = c.rrect(cx - hw + 1.0, top + 8, cx + hw - 1.0, SHELF - 1.0, 2.4)
    for (x, y, a, s) in [(cx - 5.5, SHELF - 6.8, 15, 11.5), (cx + 5.5, SHELF - 7.5, -25, 11.5), (cx, SHELF - 15.5, 5, 11.5),
                         (cx - 6, SHELF - 23, -35, 11), (cx + 6.2, SHELF - 22.5, 30, 11)]:
        sprite(c, ING + "cookie.png", (x - s / 2, y - s / 2, x + s / 2, y + s / 2), angle=a, clip=inner)
    # стекло поверх
    u = row_extent(body); v = col_extent(body)
    s1 = np.clip(1 - np.abs(u - 0.18) / 0.07, 0, 1) * np.clip(np.minimum(v - 0.06, 0.94 - v) / 0.1, 0, 1)
    s2 = np.clip(1 - np.abs(u - 0.84) / 0.04, 0, 1) * np.clip(np.minimum(v - 0.25, 0.85 - v) / 0.1, 0, 1)
    c.tint(np.ones(3, np.float32), blur((s1 * 0.7 + s2 * 0.35) * body, 0.2))
    c.tint(rgb((210, 232, 228)), body * 0.12)
    outline(c, body, (150, 175, 175), 0.5, 0.75)
    # крышка
    lid = c.rrect(cx - hw + 1.2, top, cx + hw - 1.2, top + 7.6, 2.2)
    fill_cyl(c, lid, (216, 86, 62), amb=0.6, spec=0.45)
    knob = c.rrect(cx - 3, top - 2.2, cx + 3, top + 0.8, 1.2)
    fill_cyl(c, knob, (216, 86, 62), amb=0.6, spec=0.4)
    return c


def shelf_objects():
    """Возвращает (фон_за_перекладиной, перед_перекладиной) — списки холстов."""
    back = [
        fan_plant(346.5, 50),
        olive_oil(), vinegar(), soy(),
        sprite_obj(R + "UI/salt_mill.png", (514, 74, 540, SHELF)),
        squeeze_bottle(560, 53, SHELF, 14.4, (246, 196, 44), (250, 206, 66), ING + "chili.png"),
        squeeze_bottle(595, 49, SHELF, 14.8, (218, 46, 36), (226, 58, 46), ING + "tomato.png"),
        fan_plant(676.5, 50),
        cookie_jar(),
    ]
    lp, lf = trailing_plant(262)
    rp, rf = trailing_plant(769, mirror=True)
    return [lp, rp] + back, [lf, rf]


# ───────────────────────── стол
WOOD = (214, 156, 94)
WOOD_DARK = (176, 118, 66)
SHADOW = (150, 78, 28)


def counter_shadow(c, cx, bottom, hw, alpha=0.5):
    """Тень на столешнице: падающая вправо-назад (свет слева сверху) + плотная контактная."""
    shadow_ellipse(c, cx + hw * 0.75, bottom - 1.2, hw * 1.4, 4.4, alpha=alpha * 0.9, color=SHADOW, soft=1.6)
    shadow_ellipse(c, cx + 1.0, bottom - 0.3, hw * 1.08, 2.4, alpha=alpha * 1.3, color=(120, 60, 20), soft=0.8)


def with_shadow(fn, cx, bottom, hw, *a, **kw):
    """Обёртка: холст объекта + контактная тень под ним (тень рисуется первой)."""
    c = fn(*a, **kw)
    sh = Canvas(c.ox, c.oy, c.ox + c.w / K + 24, bottom + 6)
    counter_shadow(sh, cx, bottom, hw)
    h, w = min(c.h, sh.h), c.w
    rgb_ = np.zeros_like(sh.rgb); a_ = np.zeros_like(sh.a)
    rgb_[:h, :w] = c.rgb[:h]; a_[:h, :w] = c.a[:h]
    sh.over(rgb_, a_)
    return sh


def utensil_crock():
    cx, top, bottom = 67.5, 447, 528
    c = Canvas(8, 370, 140, bottom + 6)
    counter_shadow(c, cx, bottom, 31)
    # горловина: тёмная внутренняя часть
    hole = c.ellipse(cx - 28.5, top - 3.2, cx + 28.5, top + 4.2)
    fill_soft(c, hole, (96, 74, 58), height=1.2, spec=0, line=False)
    # лопатка
    ang = math.radians(-24)
    bcx, bcy, bw, bh = 34, 411, 19, 34
    def rot(px, py):
        dx, dy = px - bcx, py - bcy
        return (bcx + dx * math.cos(ang) - dy * math.sin(ang), bcy + dx * math.sin(ang) + dy * math.cos(ang))
    blade_pts = []
    for t in np.linspace(0, 1, 30):           # верх — скруглённый
        a = math.pi + math.pi * t
        blade_pts.append(rot(bcx + bw / 2 * math.cos(a), bcy - bh / 2 + 6 + 6 * math.sin(a)))
    blade_pts += [rot(bcx + bw / 2, bcy + bh / 2 - 6), rot(bcx + 3.2, bcy + bh / 2 + 2), rot(bcx - 3.2, bcy + bh / 2 + 2), rot(bcx - bw / 2, bcy + bh / 2 - 6)]
    hm = c.line([rot(bcx, bcy + bh / 2), (58, 462)], 5.0)
    fill_soft(c, hm, WOOD, height=2.0, spec=0.3)
    bm = c.poly(blade_pts)
    fill_soft(c, bm, WOOD, height=2.6, spec=0.35)
    for sx in (-5, 0, 5):
        sm = c.poly([rot(bcx + sx - 1.3, bcy - 9), rot(bcx + sx + 1.3, bcy - 9), rot(bcx + sx + 1.3, bcy + 9), rot(bcx + sx - 1.3, bcy + 9)])
        sm = c.mask(lambda d, cc: [d.line(cc.pts([rot(bcx + sx, bcy - 9), rot(bcx + sx, bcy + 9)]), fill=255, width=int(2.6 * K), joint="curve"),
                                   [d.ellipse([q[0] - 1.3 * K, q[1] - 1.3 * K, q[0] + 1.3 * K, q[1] + 1.3 * K], fill=255) for q in cc.pts([rot(bcx + sx, bcy - 9), rot(bcx + sx, bcy + 9)])]])
        c.tint(rgb(WOOD_DARK) * 0.55, sm * 0.95)
    # венчик
    wcx = 70
    hm = c.line([(wcx, 440), (wcx - 0.5, 470)], 5.4)
    fill_cyl(c, hm, (60, 64, 70), amb=0.55, spec=0.5)
    ferr = c.rrect(wcx - 3.4, 436, wcx + 3.4, 442, 1.2)
    fill_cyl(c, ferr, (190, 196, 204), amb=0.6, spec=0.6)
    for k, a in enumerate([14.5, 10.5, 6, 1.8]):
        t = np.linspace(0, 1, 80)
        ys = 437 - 60 * t
        xs = wcx + a * np.sin(np.pi * t ** 0.62)
        xs2 = wcx - a * np.sin(np.pi * t ** 0.62)
        pts = list(zip(xs, ys)) + list(zip(xs2, ys))[::-1]
        wm = c.mask(lambda d, cc: d.line(cc.pts(pts + [pts[0]]), fill=255, width=int(1.15 * K), joint="curve"))
        fill_cyl(c, wm, (200, 206, 214), amb=0.75, spec=0, line=False)
        outline(c, wm, (150, 156, 166), 0.3, 0.6, 0.8)
    # ложка
    sang = 22
    hm = c.line([(96, 426), (80, 470)], 4.4)
    fill_soft(c, hm, WOOD, height=1.8, spec=0.3)
    sb = c.mask(lambda d, cc: None)
    pts = []
    for t in np.linspace(0, 2 * math.pi, 80):
        x, y = 8.6 * math.cos(t), 13.5 * math.sin(t)
        a = math.radians(sang)
        pts.append((100 + x * math.cos(a) - y * math.sin(a), 410 + x * math.sin(a) + y * math.cos(a)))
    sb = c.poly(pts)
    fill_soft(c, sb, WOOD, height=3.2, spec=0.4)
    bowl = c.poly([(100 + (x - 100) * 0.72, 410 + (y - 410) * 0.75) for x, y in pts])
    c.tint(rgb(WOOD_DARK), blur(bowl, 1.0) * 0.45)
    # сам горшок-подставка
    body = c.poly(lathe(cx, [(top, 30.5), (top + 4, 31.3), (top + 6, 29.4), (top + 10, 29.8), (top + 30, 32.4), (bottom - 8, 32.2), (bottom - 1.5, 30.4), (bottom, 29.0)]))
    fill_cyl(c, body, (242, 234, 216), amb=0.66, spec=0.55, spec_w=0.07, line_dark=0.62)
    for yy, hh in [(top + 15, 2.0), (top + 19.5, 1.0)]:
        band = c.rrect(cx - 40, yy, cx + 40, yy + hh, 0.3) * body
        fill_cyl(c, band, TERRA, amb=0.62, spec=0.2, umask=body, line=False)
    # ободок спереди
    lip = c.rrect(cx - 31.3, top, cx + 31.3, top + 4.2, 2) * (1 - hole * (c.ellipse(cx - 27.5, top - 3.2, cx + 27.5, top + 2.4)))
    fill_cyl(c, lip, (248, 242, 228), amb=0.7, spec=0.5, line=False)
    outline(c, body, (242, 234, 216), 0.5, 0.62)
    return c


def bbq_bottle():
    return with_shadow(squeeze_bottle, 127, 521, 15.4, 127, 447, 521, 15.4, (132, 66, 38), (246, 240, 228), ING + "bacon.png")


def mustard_bottle():
    return with_shadow(squeeze_bottle, 192.5, 520, 16.5, 192.5, 422, 520, 16.6, (242, 190, 44), (250, 246, 234), ING + "carrot.png")


def ketchup_glass():
    cx = 160.5
    prof = [(447, 6.0), (452, 5.6), (468, 6.6), (476, 11), (483, 15.6), (500, 18.2), (528, 19.6), (531, 18.6)]
    def mk():
        return glass_bottle(cx, 440, 531, prof, liquid=(206, 36, 30), level=450, glass=(240, 220, 214),
                            cap=(248, 244, 236), cap_h=7.6, label=(cx - 14, 492, cx + 14, 519), icon=ING + "tomato.png")
    return with_shadow(mk, cx, 531, 19)


def spice_jar(cx, hw, lid, contents, icon, top=453, bottom=524, seed=1):
    c = Canvas(cx - hw - 3, top - 2, cx + hw + 26, bottom + 6)
    counter_shadow(c, cx, bottom, hw)
    body = c.rrect(cx - hw, top + 11, cx + hw, bottom, 3.6)
    neck = c.rrect(cx - hw + 2, top + 9, cx + hw - 2, top + 14, 1)
    jar = np.maximum(body, neck)
    fill_cyl(c, jar, (232, 240, 238), amb=0.85, spec=0, alpha=0.45, line=False)
    inner = c.rrect(cx - hw + 1.1, top + 19, cx + hw - 1.1, bottom - 1.1, 2.6)
    fill_cyl(c, inner, contents, amb=0.62, spec=0, line=False, umask=jar)
    rng = np.random.default_rng(seed)
    H, W = inner.shape
    spk = np.zeros((H, W), np.float32)
    for _ in range(260):
        x, y = rng.integers(0, W), rng.integers(0, H)
        r = rng.integers(3, 7)
        spk[max(0, y - r):y + r, max(0, x - r):x + r] = rng.choice([-1, 1])
    spk = blur(spk, 0.15) * inner
    c.tint(rgb(contents) * 0.6, np.clip(-spk, 0, 1) * 0.6)
    c.tint(rgb(contents) * 1.3, np.clip(spk, 0, 1) * 0.5)
    label_on(c, jar, cx - hw + 2.2, top + 26, cx + hw - 2.2, bottom - 9, icon, color=CREAM, r=1.4, border=lid)
    u = row_extent(jar); v = col_extent(jar)
    s1 = np.clip(1 - np.abs(u - 0.16) / 0.06, 0, 1) * np.clip(np.minimum(v - 0.05, 0.95 - v) / 0.1, 0, 1)
    s2 = np.clip(1 - np.abs(u - 0.86) / 0.035, 0, 1) * np.clip(np.minimum(v - 0.2, 0.85 - v) / 0.1, 0, 1)
    c.tint(np.ones(3, np.float32), blur((s1 * 0.6 + s2 * 0.3) * jar, 0.2))
    outline(c, jar, (140, 160, 160), 0.5, 0.8)
    lm = c.rrect(cx - hw - 0.6, top, cx + hw + 0.6, top + 10.5, 2.2)
    fill_cyl(c, lm, lid, amb=0.58, spec=0.5)
    for k in range(1, 6):
        xx = cx - hw + 2 * hw * k / 6
        rib = c.rrect(xx - 0.25, top + 1.2, xx + 0.25, top + 9.3, 0.2) * lm
        c.tint(rgb(lid) * 0.7, rib * 0.5)
    return c


def napkin_holder(dx=0.0):
    c = Canvas(934 + dx, 455, 1024, 536)
    counter_shadow(c, 971 + dx, 529, 31)
    # салфетки
    dx0 = dx
    for k in range(6):
        dx, dy = -2.4 + k * 0.9, k * 0.55
        m = c.rrect(944 + dx0 + dx, 462 + dy, 998 + dx0 + dx, 518, 0.8)
        fill_soft(c, m, (250, 250, 246), height=0.8, amb=0.8, diff=0.25, spec=0.05, line_w=0.35, line_dark=0.78)
    # держатель: передняя стенка-«домик»
    xs = np.linspace(940, 1002, 60)
    ys = 495 - 19 * np.sin(np.pi * (xs - 940) / 62) ** 0.9
    pts = [(x + dx0, y) for x, y in zip(xs, ys)] + [(1002 + dx0, 529), (940 + dx0, 529)]
    front = c.poly(pts)
    hole = c.ellipse(964 + dx0, 491, 978 + dx0, 505)
    front = np.clip(front - hole, 0, 1)
    fill_soft(c, front, WOOD, height=3.0, spec=0.3, line_dark=0.58)
    # волокна дерева
    for k in range(6):
        yy = 486 + k * 6.5
        gx = np.linspace(941 + dx0, 1001 + dx0, 50)
        gpts = list(zip(gx, yy + 0.6 * np.sin(gx / 7 + k)))
        gm = c.mask(lambda d, cc: d.line(cc.pts(gpts), fill=255, width=max(1, int(0.35 * K))))
        c.tint(rgb(WOOD_DARK), gm * front * 0.3)
    # тень салфеток в окошке
    c.tint(rgb((200, 196, 186)), blur(hole, 0.5) * hole * 0.5)
    base = c.rrect(937 + dx0, 524, 1005 + dx0, 530, 1.4)
    fill_cyl(c, base, WOOD_DARK, amb=0.66, spec=0.3)
    return c


def counter_objects():
    return [
        utensil_crock(), bbq_bottle(), mustard_bottle(), ketchup_glass(),
        spice_jar(790, 16.5, (98, 168, 72), (98, 132, 52), ING + "lettuce.png", seed=3),
        spice_jar(830, 16.5, (214, 66, 50), (204, 84, 40), ING + "pepper.png", seed=5),
        with_shadow(sprite_obj, 871, 524, 16, R + "UI/pepper_mill.png", (853, 444, 889, 524)),
        with_shadow(sprite_obj, 915, 526, 16, R + "UI/salt_mill.png", (897, 446, 933, 526)),
        napkin_holder(dx=5),
    ]


# ───────────────────────── рамки и меловая доска
from PIL import ImageFont
FONT = R + "Fonts/Main.ttf"
WALL_SHADOW = (70, 78, 46)


def frame(fr, mat, panel, frame_col, panel_col, art, art_fill=0.82, mat_col=(248, 244, 232)):
    x0, y0, x1, y1 = fr
    c = Canvas(x0 - 3, y0 - 3, x1 + 5, y1 + 6)
    # тень на стене
    sh = c.rrect(x0 + 1.4, y0 + 2.2, x1 + 1.4, y1 + 2.2, 1.6)
    c.over(rgb(WALL_SHADOW), blur(sh, 1.3) * 0.42)
    outer = c.rrect(x0, y0, x1, y1, 1.6)
    m_in = c.rrect(*mat, 0.5)
    ring = np.clip(outer - m_in, 0, 1)
    fill_soft(c, ring, frame_col, height=1.6, amb=0.62, diff=0.6, spec=0.3, shin=12, line_w=0.45, line_dark=0.5)
    # внутренняя фаска рамки — тонкая светлая кромка у паспарту
    edge = np.clip(c.rrect(mat[0] - 0.7, mat[1] - 0.7, mat[2] + 0.7, mat[3] + 0.7, 0.6) - m_in, 0, 1)
    c.tint(rgb(frame_col) * 0.55, edge * 0.8)
    p_in = c.rrect(*panel, 0.3)
    mat_ring = np.clip(m_in - p_in, 0, 1)
    fill_cyl(c, mat_ring, mat_col, amb=0.93, spec=0, line=False, vgrad=0.05)
    # паспарту отбрасывает лёгкую тень на картинку
    fill_cyl(c, p_in, panel_col, amb=0.9, spec=0, line=False, vgrad=0.08, rim=0.0)
    H, W = p_in.shape
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    pcx, pcy = c.P((panel[0] + panel[2]) / 2, (panel[1] + panel[3]) / 2)
    rr = np.hypot((xs - pcx) / ((panel[2] - panel[0]) * K / 2), (ys - pcy) / ((panel[3] - panel[1]) * K / 2))
    c.tint(np.ones(3, np.float32), np.clip(1 - rr, 0, 1) * p_in * 0.14)
    inset = np.clip(p_in - np.roll(np.roll(p_in, int(0.9 * K), 0), int(0.9 * K), 1), 0, 1)
    c.tint(rgb(panel_col) * 0.55, blur(inset, 0.35) * 0.55)
    # картинка: тень + спрайт
    pw, ph = panel[2] - panel[0], panel[3] - panel[1]
    s = min(pw, ph) * art_fill
    acx, acy = (panel[0] + panel[2]) / 2, (panel[1] + panel[3]) / 2
    sh2 = c.ellipse(acx - s * 0.42, acy + s * 0.36, acx + s * 0.42, acy + s * 0.5)
    c.tint(rgb(panel_col) * 0.6, blur(sh2, 1.2) * 0.45 * p_in)
    sprite(c, art, (acx - s / 2, acy - s / 2, acx + s / 2, acy + s / 2), clip=p_in)
    return c


def chalk_text(c, text, x, y, size, anchor="mm", alpha=0.95, seed=0):
    font = ImageFont.truetype(FONT, int(size * K))
    m = c.mask(lambda d, cc: d.text(cc.P(x, y), text, font=font, fill=255, anchor=anchor))
    rng = np.random.default_rng(seed)
    noise = blur(rng.random(m.shape).astype(np.float32), 0.12)
    noise = (noise - noise.min()) / (noise.max() - noise.min() + 1e-6)
    c.tint(rgb((240, 242, 232)), m * (0.62 + 0.38 * noise) * alpha)


def chalkboard():
    fr, board = (901, 266.5, 993, 397), (908, 274, 986, 390)
    x0, y0, x1, y1 = fr
    c = Canvas(x0 - 3, y0 - 3, x1 + 5, y1 + 6)
    sh = c.rrect(x0 + 1.4, y0 + 2.2, x1 + 1.4, y1 + 2.2, 1.8)
    c.over(rgb(WALL_SHADOW), blur(sh, 1.3) * 0.42)
    outer = c.rrect(x0, y0, x1, y1, 2.0)
    bm = c.rrect(*board, 0.6)
    fill_soft(c, np.clip(outer - bm, 0, 1), (199, 148, 105), height=1.8, amb=0.62, diff=0.6, spec=0.3, shin=12, line_w=0.45, line_dark=0.52)
    fill_cyl(c, bm, (62, 82, 70), amb=0.95, spec=0, line=False, vgrad=0.1, rim=0)
    rng = np.random.default_rng(4)
    smudge = blur(rng.random(bm.shape).astype(np.float32), 2.5)
    smudge = (smudge - smudge.mean()) / (smudge.std() + 1e-6)
    c.tint(rgb((150, 165, 150)), np.clip(smudge * 0.05 + 0.05, 0, 0.15) * bm)
    inset = np.clip(bm - np.roll(np.roll(bm, int(1.0 * K), 0), int(1.0 * K), 1), 0, 1)
    c.tint(np.zeros(3, np.float32), blur(inset, 0.4) * 0.35)
    chalk_text(c, "МЕНЮ", 947, 287, 12.5, seed=1)
    line = c.line([(919, 297.5), (976, 297.2)], 0.8)
    c.tint(rgb((236, 238, 226)), line * 0.8)
    for k, (p, name) in enumerate([("Dishes/salad.png", "Салат"), ("Dishes/pizza.png", "Пицца"), ("Dishes/tomyum.png", "Том ям")]):
        yy = 315 + k * 26
        sprite(c, R + p, (912.5, yy - 9.5, 931.5, yy + 9.5), tint=(205, 210, 196), opacity=0.92)
        chalk_text(c, name, 935.5, yy + 0.5, 8.2, anchor="lm", seed=10 + k)
    # мелок на полочке
    ch = c.rrect(960, 385.5, 974, 388.5, 1.2)
    fill_cyl(c, ch, (246, 246, 240), amb=0.7, spec=0.3, line_w=0.3)
    return c


def wall_frames():
    D, I = R + "Dishes/", ING
    dark, brown = (82, 78, 74), (168, 120, 84)
    return [
        frame((31, 152, 120, 272), (37, 157.5, 114.5, 266.5), (44, 165, 108, 261), dark, (172, 180, 138), D + "salad.png", 0.86),
        frame((134, 212, 211, 318), (140, 217.5, 206.5, 312.5), (145, 222, 201, 308), brown, (236, 224, 200), D + "pizza.png", 0.88),
        frame((63, 291, 120, 358.5), (68, 296, 115.5, 353.5), (73.5, 301, 111.5, 349), brown, (228, 190, 108), D + "tomyum.png", 0.86),
        frame((906, 141, 983, 247), (911, 147, 978, 242), (917, 152.5, 973, 237.5), brown, (228, 190, 108), I + "tomato.png", 0.84),
        frame((811, 212.5, 889, 275), (816, 218, 885, 271), (821, 223, 880, 266), brown, (236, 224, 200), I + "cheese.png", 0.86),
        frame((806, 306.5, 885, 370), (812, 312, 880.5, 365.5), (817, 318, 875, 360), dark, (236, 224, 200), I + "shrimp.png", 0.86),
        chalkboard(),
    ]


# ───────────────────────── бокалы под полкой и подвесная табличка
METAL = (64, 66, 72)
FRONT_GLASSES = [261.5, 312.5, 363.5, 660.5, 710.5, 761.5]


def glass_holders():
    out = []
    for gx0, gx1, centers, back_dx in [(244, 391, FRONT_GLASSES[:3], 10.5), (634, 782, FRONT_GLASSES[3:], -9.5)]:
        c = Canvas(gx0 - 2, 129, gx1 + 2, 150)
        beam = c.rrect(gx0, 130.5, gx1, 135.5, 0.6)
        fill_soft(c, beam, METAL, height=1.0, amb=0.6, diff=0.5, spec=0.25, line_w=0.45, line_dark=0.45)
        for cx in centers:
            for dx in (0, back_dx):
                x = cx + dx
                for sgn in (-1, 1):
                    post = c.rrect(x + sgn * 9.6 - 0.8, 134.5, x + sgn * 9.6 + 0.8, 142.6, 0.4)
                    fill_cyl(c, post, METAL, amb=0.6, spec=0.35, line_w=0.35, line_dark=0.45)
                    lip = c.rrect(min(x + sgn * 2.4, x + sgn * 10.4), 141.2, max(x + sgn * 2.4, x + sgn * 10.4), 143.2, 0.6)
                    fill_soft(c, lip, METAL, height=0.6, amb=0.62, diff=0.5, spec=0.3, line_w=0.35, line_dark=0.45)
        out.append(c)
    return out


def wine_glass(cx, top=138.6, scale=1.0, back=False):
    """Бокал вверх ножкой: подставка → ножка → чаша, кромка внизу."""
    s = scale
    c = Canvas(cx - 18, top - 1, cx + 18, top + 80 * s)
    foot = c.ellipse(cx - 8.6 * s, top, cx + 8.6 * s, top + 2.8 * s)
    stem = c.poly(lathe(cx, [(top + 1.5 * s, 2.0 * s), (top + 4 * s, 1.2 * s), (top + 26 * s, 1.25 * s), (top + 31 * s, 2.4 * s), (top + 33 * s, 3.4 * s)]))
    bowl_prof = [(top + 32.5 * s, 2.6 * s), (top + 36 * s, 8.8 * s), (top + 43 * s, 13.4 * s), (top + 54 * s, 15.3 * s), (top + 65 * s, 14.8 * s),
                 (top + 73.5 * s, 13.6 * s), (top + 76.5 * s, 13.2 * s)]
    bowl = c.poly(lathe(cx, bowl_prof))
    tint = (232, 244, 246) if not back else (206, 222, 224)
    edge_col = (110, 136, 142)
    fill_cyl(c, bowl, tint, amb=0.86, spec=0, alpha=0.72 if not back else 0.62, line=False, rim=0.08)
    # кромка внизу — плотнее
    rim = c.ellipse(cx - 13.2 * s, top + 74.6 * s, cx + 13.2 * s, top + 78.2 * s)
    rim = np.clip(rim - c.ellipse(cx - 12 * s, top + 74.9 * s, cx + 12 * s, top + 77.2 * s), 0, 1)
    c.over(rgb((236, 246, 248)), rim * 0.85)
    u = row_extent(bowl); v = col_extent(bowl)
    s1 = np.clip(1 - np.abs(u - 0.2) / 0.07, 0, 1) * np.clip(np.minimum(v - 0.12, 0.9 - v) / 0.12, 0, 1)
    s2 = np.clip(1 - np.abs(u - 0.82) / 0.035, 0, 1) * np.clip(np.minimum(v - 0.35, 0.85 - v) / 0.1, 0, 1)
    c.tint(np.ones(3, np.float32), blur((s1 * 0.85 + s2 * 0.45) * bowl, 0.25))
    outline(c, bowl, edge_col, 0.45, 1.0, 0.75)
    fill_cyl(c, stem, (222, 236, 238), amb=0.8, spec=0.6, spec_w=0.2, alpha=0.9, line_w=0.35, line_dark=0.62)
    fill_soft(c, foot, (226, 240, 242), height=0.8, amb=0.75, diff=0.35, spec=0.5, alpha=0.92, line_w=0.35, line_dark=0.6)
    return c


def glasses():
    out = []
    for i, cx in enumerate(FRONT_GLASSES):
        out.append(wine_glass(cx + (10.5 if i < 3 else -9.5), top=139.4, scale=1.0, back=True))
    for cx in FRONT_GLASSES:
        out.append(wine_glass(cx))
    return out


def hanging_sign(text="Выбор блюда"):
    x0, x1, y0, y1 = 420, 604, 163, 200
    c = Canvas(x0 - 4, 140, x1 + 6, y1 + 6)
    # цепочки от балки
    for hx in (x0 + 24, x1 - 24):
        for k in range(5):
            yy = 147.5 + k * 3.4
            link = c.ellipse(hx - 1.3, yy - 1.0, hx + 1.3, yy + 2.6) - c.ellipse(hx - 0.55, yy - 0.2, hx + 0.55, yy + 1.8)
            fill_soft(c, np.clip(link, 0, 1), (150, 154, 162), height=0.4, amb=0.6, diff=0.6, spec=0.5, line_w=0.25, line_dark=0.5)
    # тень на стене
    sh = c.rrect(x0 + 1.6, y0 + 2.4, x1 + 1.6, y1 + 2.4, 3)
    c.over(rgb(WALL_SHADOW), blur(sh, 1.4) * 0.4)
    board = c.rrect(x0, y0, x1, y1, 3)
    fill_soft(c, board, (234, 180, 104), height=2.2, amb=0.68, diff=0.45, spec=0.25, shin=10, line_w=0.6, line_dark=0.55)
    # светлая внутренняя кромка, как у таблички на экране заказов
    inner = c.rrect(x0 + 2.4, y0 + 2.4, x1 - 2.4, y1 - 2.4, 2)
    ring = np.clip(inner - c.rrect(x0 + 3.2, y0 + 3.2, x1 - 3.2, y1 - 3.2, 1.6), 0, 1)
    c.tint(rgb((250, 214, 150)), ring * 0.8)
    # волокна дерева
    rng = np.random.default_rng(2)
    for k in range(7):
        yy = y0 + 6 + k * 4.4 + rng.normal(0, 0.6)
        xs = np.linspace(x0 + 4, x1 - 4, 60)
        pts = list(zip(xs, yy + 0.5 * np.sin(xs / (9 + k) + k)))
        gm = c.mask(lambda d, cc: d.line(cc.pts(pts), fill=255, width=max(1, int(0.35 * K))))
        c.tint(rgb((196, 134, 70)), gm * inner * 0.28)
    # шурупы
    for sx in (x0 + 9, x1 - 9):
        sc = c.ellipse(sx - 2.4, (y0 + y1) / 2 - 2.4, sx + 2.4, (y0 + y1) / 2 + 2.4)
        fill_soft(c, sc, (160, 164, 172), height=0.8, amb=0.6, diff=0.6, spec=0.5, line_w=0.4, line_dark=0.4)
        slot = c.line([(sx - 1.4, (y0 + y1) / 2 - 1.0), (sx + 1.4, (y0 + y1) / 2 + 1.0)], 0.5)
        c.tint(rgb((70, 72, 78)), slot * sc)
    # надпись: тёмно-оливковая с лёгкой светлой «выжженной» кромкой снизу
    font = ImageFont.truetype(FONT, int(19 * K))
    tx, ty = (x0 + x1) / 2, (y0 + y1) / 2 + 0.3
    hl = c.mask(lambda d, cc: d.text(cc.P(tx, ty + 0.7), text, font=font, fill=255, anchor="mm"))
    c.tint(rgb((252, 222, 166)), blur(hl, 0.1) * 0.8)
    tm = c.mask(lambda d, cc: d.text(cc.P(tx, ty), text, font=font, fill=255, anchor="mm"))
    c.tint(rgb((92, 90, 38)), tm)
    return c
