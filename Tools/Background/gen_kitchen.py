"""Генератор фона-кухни в плоском векторном стиле (без контуров)."""
import sys

W, H = 1920, 1080
parts = []
add = parts.append


def rect(x, y, w, h, fill, rx=0, op=None):
    o = f' fill-opacity="{op}"' if op is not None else ""
    add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}"{o}/>')


def poly(points, fill, op=None):
    o = f' fill-opacity="{op}"' if op is not None else ""
    pts = " ".join(f"{x},{y}" for x, y in points)
    add(f'<polygon points="{pts}" fill="{fill}"{o}/>')


def ellipse(cx, cy, rx, ry, fill, op=None):
    o = f' fill-opacity="{op}"' if op is not None else ""
    add(f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="{fill}"{o}/>')


def circle(cx, cy, r, fill, op=None):
    ellipse(cx, cy, r, r, fill, op)


# ── Потолок ──
rect(0, 0, W, 200, "#5E82BC")
poly([(0, 0), (520, 0), (430, 120), (0, 170)], "#4C6DA6")
poly([(1400, 0), (W, 0), (W, 170), (1490, 120)], "#4C6DA6")
poly([(560, 0), (1360, 0), (1300, 70), (620, 70)], "#6C91CB")
for cx in (700, 1220):
    ellipse(cx, 92, 92, 18, "#3F5E93")
    ellipse(cx, 88, 72, 12, "#FFE9A8")
    ellipse(cx, 88, 120, 34, "#FFF4C9", 0.18)

# ── Окна с небом ──
rect(0, 200, W, 130, "#3F5E93")
for x in range(60, W, 300):
    rect(x, 212, 270, 108, "#A9D3F5")
    ellipse(x + 80, 290, 70, 26, "#FFFFFF", 0.9)
    ellipse(x + 140, 280, 60, 30, "#FFFFFF", 0.9)
    ellipse(x + 200, 296, 50, 20, "#FFFFFF", 0.9)
    poly([(x + 150, 212), (x + 190, 212), (x + 110, 320), (x + 70, 320)], "#FFFFFF", 0.25)
rect(0, 320, W, 14, "#2F4A78")

# ── Белый кафель ──
WALL0, WALL1, T = 334, 690, 64
rect(0, WALL0, W, WALL1 - WALL0, "#F4F2ED")
for row, y in enumerate(range(WALL0, WALL1, T)):
    for col, x in enumerate(range(0, W, T)):
        if (row * 7 + col * 3) % 11 == 0:
            rect(x + 3, y + 3, T - 6, T - 6, "#ECE8E0")
for x in range(0, W + 1, T):
    rect(x - 1.5, WALL0, 3, WALL1 - WALL0, "#DAD5CB")
for y in range(WALL0, WALL1 + 1, T):
    rect(0, y - 1.5, W, 3, "#DAD5CB")
# солнечные полосы
poly([(380, WALL0), (560, WALL0), (300, WALL1), (120, WALL1)], "#FFFFFF", 0.35)
poly([(1500, WALL0), (1600, WALL0), (1420, WALL1), (1320, WALL1)], "#FFFFFF", 0.3)

# ── Деревянная балка с лампами и посудой ──
for x in (430, 960, 1490):
    rect(x - 2, 0, 4, 470 if x == 960 else 420, "#2E3A52")
rect(0, 348, W, 16, "#B07A4C")
rect(0, 362, W, 6, "#8A5B35")
# лампы-абажуры
for x, y in ((430, 420), (1490, 420)):
    poly([(x - 18, y - 40), (x + 18, y - 40), (x + 60, y), (x - 60, y)], "#D5DCE4")
    poly([(x - 18, y - 40), (x, y - 40), (x - 20, y), (x - 60, y)], "#BAC3CD")
    ellipse(x, y, 60, 8, "#FFE9A8")
# крючки и посуда
def ladle(x):
    rect(x - 5, 364, 10, 110, "#C9D1D8")
    add(f'<path d="M{x-30} 470 a30 24 0 0 0 60 0 z" fill="#C9D1D8"/>')

def pan(x, r, body, inner, light):
    rect(x - 8, 364, 16, 50, "#3D3A44")
    circle(x, 414 + r, r, body)
    circle(x, 414 + r, r * 0.78, inner)
    add(f'<path d="M{x-r*0.55} {414+r*0.6} a{r*0.6} {r*0.6} 0 0 1 {r*0.5} {-r*0.35}" '
        f'fill="none" stroke="{light}" stroke-width="7" stroke-linecap="round"/>')

ladle(570)
pan(690, 50, "#D9803F", "#EC9D5E", "#FFD2A6")
rect(1205, 364, 10, 90, "#B9844F")
rect(1188, 450, 44, 60, "#5E8C6A", rx=10)
pan(1340, 56, "#D9534F", "#E8706B", "#FFB3AE")

# ── Вытяжка над плитой ──
poly([(900, 470), (1020, 470), (1110, 560), (810, 560)], "#C8D0D8")
poly([(900, 470), (960, 470), (900, 560), (810, 560)], "#B3BCC6")
rect(800, 556, 320, 14, "#9AA5B1", rx=4)

# ── Пол ──
FL0, FL1 = 690, 900
rect(0, FL0, W, FL1 - FL0, "#9A8F96")
for i, y in enumerate((700, 730, 770, 820, 880)):
    rect(0, y, W, 3, "#857A82")
vx, vy = 960, 380
for x in range(-1600, W + 1700, 220):
    # линии к точке схода
    y0, y1 = FL0, FL1
    x0 = vx + (x - vx) * (y0 - vy) / (FL1 - vy)
    add(f'<line x1="{x0:.1f}" y1="{y0}" x2="{x}" y2="{y1}" stroke="#857A82" stroke-width="3"/>')
rect(0, FL0, W, 10, "#7D7279")

# ── Ресторанная стойка на заднем плане ──
ellipse(960, 862, 560, 22, "#000000", 0.15)
rect(430, 640, 1060, 220, "#8EA2BC")          # корпус
rect(430, 640, 1060, 14, "#7D90AA")
# шкафчики слева
rect(450, 670, 170, 170, "#9FB2CA", rx=4)
rect(630, 670, 170, 170, "#9FB2CA", rx=4)
rect(600, 740, 8, 40, "#5B6B85", rx=4)
rect(642, 740, 8, 40, "#5B6B85", rx=4)
# духовка
rect(820, 662, 280, 190, "#D3DAE2", rx=6)
for i, x in enumerate((870, 925, 980, 1035)):
    circle(x, 684, 11, "#3D4A63")
rect(845, 710, 230, 120, "#3D4A63", rx=8)
poly([(900, 710), (940, 710), (880, 830), (845, 830), (845, 800)], "#56657F")
rect(860, 700, 200, 6, "#AAB4BF", rx=3)
# полки с посудой справа
rect(1120, 670, 350, 170, "#7287A3", rx=4)
for y in (712, 790):
    rect(1130, y + 18, 330, 6, "#9FB2CA")
    for i in range(5):
        add(f'<path d="M{1145+i*62} {y+18} a22 18 0 0 1 44 0 z" fill="#E48A4E"/>')
# столешница
rect(410, 620, 1100, 26, "#F1F3F5", rx=6)
rect(410, 640, 1100, 6, "#D5DAE0")
# плита
rect(830, 608, 260, 14, "#2F3A4D", rx=4)
ellipse(890, 608, 34, 5, "#4A5770")
ellipse(1030, 608, 34, 5, "#4A5770")
# предметы на стойке
add('<path d="M500 620 l10 -44 h70 l10 44 z" fill="#D9534F"/>')
circle(526, 568, 20, "#6FB35A"); circle(556, 560, 24, "#7FC46A"); circle(540, 545, 18, "#6FB35A")
rect(640, 560, 120, 50, "#3D4A63", rx=12)
rect(630, 552, 140, 14, "#556380", rx=7)
rect(660, 540, 80, 14, "#556380", rx=7)
rect(1140, 560, 46, 60, "#2F3A4D", rx=10)
rect(1150, 548, 26, 14, "#556380", rx=5)
rect(1210, 540, 50, 80, "#9E3B3B", rx=8)
rect(1220, 528, 30, 14, "#C9D1D8", rx=5)
add('<path d="M1300 620 a70 26 0 0 1 140 0 z" fill="#F1F3F5"/>')
ellipse(1370, 596, 50, 10, "#E48A4E")
# стулья у стойки
for x in (360, 1560):
    rect(x - 60, 760, 120, 22, "#D98E4A", rx=10)
    rect(x - 60, 778, 120, 8, "#B87436")
    rect(x - 48, 786, 10, 90, "#8A5B35")
    rect(x + 38, 786, 10, 90, "#8A5B35")

# ── Большой стол перед игроком ──
rect(0, 880, W, 30, "#000000", op=0.12)
rect(0, 892, W, 188, "#D9A066")
rect(0, 892, W, 12, "#EDBE88")
for y in (946, 1000):
    rect(0, y, W, 4, "#C48A52")
for x, y in ((220, 924), (760, 930), (1380, 922), (460, 972), (1100, 976), (1620, 970), (320, 1026), (900, 1024), (1480, 1028)):
    add(f'<path d="M{x} {y} q45 -9 90 0" fill="none" stroke="#C48A52" stroke-width="4" stroke-linecap="round"/>')
rect(0, 1040, W, 40, "#A86D3D")
rect(0, 1040, W, 6, "#8E5A30")

svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">\n' + "\n".join(parts) + "\n</svg>\n"
open(sys.argv[1], "w", encoding="utf-8").write(svg)
print(len(parts), "elements")
