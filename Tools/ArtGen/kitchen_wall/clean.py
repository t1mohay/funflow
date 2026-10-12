"""Шаг 1: убрать со стены/стола/полки все предметы, которые перерисовываются.
Стена (плитка + бежевая полоса) синтезируется по замеренной сетке, столешница копируется
со свободного центра, перекладины стеллажа — с чистых участков. Выход: clean_base.png (2048×1144)."""
import numpy as np, cv2
import os, tempfile
HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(tempfile.gettempdir(), "funflow_kitchen_wall")
os.makedirs(WORK, exist_ok=True)
from PIL import Image, ImageFilter

S = 2
src = Image.open(os.path.join(HERE, "source_sketch.png")).convert("RGB")
s = np.array(src).astype(np.float32)
H, W = s.shape[:2]
base = np.array(src.resize((W * S, H * S), Image.LANCZOS)
                .filter(ImageFilter.UnsharpMask(radius=2, percent=60, threshold=2))).astype(np.float32)

# ── синтез стены в координатах 2x
TILE = np.array([176, 183, 139], np.float32)
BAND = np.array([223, 203, 179], np.float32)
# профили затирки (смещение от тёмной линии, пиксели исходника)
VPROF = np.array([[177, 180, 137], [151, 153, 110], [187, 185, 146], [225, 223, 184], [222, 219, 181], [184, 184, 144], [176, 179, 138]], np.float32)
HPROF = np.array([[180, 182, 139], [157, 159, 117], [170, 170, 128], [220, 219, 178], [222, 220, 179], [189, 188, 147], [174, 180, 136]], np.float32)
BPROF = {33: [203, 183, 157], 34: [124, 111, 79], 35: [171, 162, 129], 36: [185, 183, 145]}


def prof_weight(u, starts, pitch, prof):
    """u — координата в пикселях исходника (центр пикселя); возвращает цвет профиля и вес (0 вне затирки)."""
    k = np.round((u - starts) / pitch)
    d = u - (starts + k * pitch) + 1.0          # 0 → пиксель до тёмной линии
    inside = (d >= 0) & (d <= len(prof) - 1)
    dc = np.clip(d, 0, len(prof) - 1.001)
    i = np.floor(dc).astype(int); f = (dc - i)[..., None]
    col = prof[i] * (1 - f) + prof[i + 1] * f
    return col, inside


def wall(y0, y1, x0, x1):
    """Синтетическая стена для области (в пикселях 2x)."""
    ys = (np.arange(y0, y1) + 0.5) / S - 0.5
    xs = (np.arange(x0, x1) + 0.5) / S - 0.5
    out = np.empty((len(ys), len(xs), 3), np.float32); out[:] = TILE
    vc, vin = prof_weight(xs, 339.0, 57.0, VPROF)
    out[:, vin] = vc[vin][None, :, :]
    hc, hin = prof_weight(ys, 235.0, 58.36, HPROF)
    out[hin] = np.where(True, hc[hin][:, None, :], 0)
    # пересечения: светлее из двух
    both = hin[:, None] & vin[None, :]
    out[both] = np.maximum(hc[:, None, :].repeat(len(xs), 1), vc[None].repeat(len(ys), 0))[both]
    # бежевая полоса сверху
    for yi, y in enumerate(ys):
        if y < 32.5:
            out[yi] = BAND
        elif y < 36.5:
            yy = int(np.floor(y)); f = y - yy
            a = np.array(BPROF.get(yy, TILE), np.float32); b = np.array(BPROF.get(yy + 1, TILE), np.float32)
            out[yi] = a * (1 - f) + b * f
    return out


def put_wall(b):
    x0, y0, x1, y1 = [int(round(v * S)) for v in b]
    base[y0:y1, x0:x1] = wall(y0, y1, x0, x1)


def counter_copy(b, ref_x=330, period=300):
    x0, y0, x1, y1 = [int(round(v * S)) for v in b]
    xs = np.arange(x0, x1)
    srcx = ref_x * S + (xs - x0) % (period * S)
    base[y0:y1, x0:x1] = base[y0:y1, srcx]


COUNTER_TOP = 496   # первая строка столешницы (исходник)

# ── 1. стол: прямоугольники с предметами
for b in [(10, 372, 218, 538), (762, 438, 1014, 538)]:
    x0, y0, x1, y1 = b
    put_wall((x0, y0, x1, COUNTER_TOP))
    counter_copy((x0, COUNTER_TOP, x1, y1))

# ── 2. рамки и доска: убрать полностью (+ поля под тени)
for b in [(28, 149, 124, 276), (131, 209, 215, 322), (60, 288, 124, 362),
          (903, 138, 988, 252), (808, 209, 894, 280), (803, 303, 890, 374), (898, 263, 998, 401)]:
    put_wall(b)

# ── 3. полка: предметы между перекладинами (до верха нижней полки y=119)
RAIL = (95, 103)          # передняя перекладина, строки исходника [95,103)
rail_rows = base[RAIL[0] * S:RAIL[1] * S, 305 * S:305 * S + 1].copy()   # чистый столбец перекладины
shelf_boxes = [(206, 56, 296, 120), (312, 48, 386, 120), (410, 29, 448, 120), (446, 29, 486, 120),
               (484, 66, 513, 120), (511, 74, 543, 120), (543, 51, 578, 120), (578, 47, 612, 120),
               (642, 47, 713, 120), (709, 72, 743, 120), (741, 62, 828, 120)]
BACK = (26, 37)           # задняя верхняя перекладина
back_rows = base[BACK[0] * S:BACK[1] * S, 520 * S:520 * S + 1].copy()
for b in shelf_boxes:
    put_wall(b)
    x0, x1 = int(b[0] * S), int(b[2] * S)
    base[RAIL[0] * S:RAIL[1] * S, x0:x1] = rail_rows
    if b[1] < BACK[1]:
        base[BACK[0] * S:BACK[1] * S, x0:x1] = back_rows
# вертикальные стойки за вьющимися растениями — копия строки, где стойка видна целиком
POSTS = [(197, 208, 45), (232, 243, 45), (781, 792, 50), (809, 825, 50)]
for px0, px1, ry in POSTS:
    for (bx0, by0, bx1, by1) in [shelf_boxes[0], shelf_boxes[-1]]:
        a, b2 = max(px0, bx0), min(px1, bx1)
        if a >= b2:
            continue
        row = np.array(src.resize((W * S, H * S), Image.LANCZOS)).astype(np.float32)[ry * S, a * S:b2 * S]
        base[by0 * S:by1 * S, a * S:b2 * S] = row[None]
        # перекладина поверх задней стойки
        base[RAIL[0] * S:RAIL[1] * S, a * S:b2 * S] = rail_rows

# ── 4. свисающий плющ под полкой: маска зелёного, полка — копией столбца, низ — стеной, стык — inpaint
g = s[..., 1]; r = s[..., 0]; bl = s[..., 2]
green = ((g - r > 18) & (g - bl > 35)) | ((g - r > 8) & (g < 120) & (g - bl > 15))
zone = np.zeros_like(green)
zone[119:212, 196:258] = True
zone[119:212, 772:832] = True
lum = s.mean(-1)
neutral = cv2.dilate((((r - bl) < 14) & (lum > 140)).astype(np.uint8), np.ones((3, 3), np.uint8), iterations=2).astype(bool)   # бокалы
ivy = (green & zone).astype(np.uint8)
ivy = cv2.dilate(ivy, np.ones((3, 3), np.uint8), iterations=2).astype(bool) & zone
protect = neutral & (np.arange(H)[:, None] >= 132)
ivy = ivy & ~protect
ivy2 = cv2.resize(ivy.astype(np.uint8), (W * S, H * S), interpolation=cv2.INTER_NEAREST).astype(bool)
protect2 = cv2.resize(protect.astype(np.uint8), (W * S, H * S), interpolation=cv2.INTER_NEAREST).astype(bool)
yy, xx = np.mgrid[0:H * S, 0:W * S]
ysrc = yy / S
# полка: строки 119..131 копируем со столбца x=300
plate = ivy2 & (ysrc >= 119) & (ysrc < 131) & (xx >= 206 * S) & (xx < 816 * S)
base[plate] = base[yy[plate], 300 * S]
# стена ниже 149
low = ivy2 & (ysrc >= 149)
wfull = wall(0, H * S, 0, W * S)
base[low] = wfull[low]
# остальное (торцы, низ полки) — inpaint
rest = ivy2 & ~plate & ~low
# торцы под полкой (за плющом) — чистая стена, без размазанных остатков
for bx0, bx1 in [(190, 245), (779, 836)]:
    sl = (slice(132 * S, 215 * S), slice(bx0 * S, bx1 * S))
    pr = protect2[sl] if bx0 < 500 else np.zeros_like(protect2[sl])
    base[sl] = np.where(pr[..., None], base[sl], wfull[sl])
    rest[sl] = False
# вся стена — синтетическая и чёткая; оставляем исходник только там, где он заметно отличается (стеллаж, бокалы)
diff = np.abs(base - wfull).sum(-1) > 45
diff = cv2.dilate(diff.astype(np.uint8), np.ones((5, 5), np.uint8), iterations=2)
keep = cv2.GaussianBlur(diff.astype(np.float32), (0, 0), 1.2)[..., None]
wall_zone = (yy < COUNTER_TOP * S)[..., None]
base = np.where(wall_zone, base * keep + wfull * (1 - keep), base)
# старые бокалы убираем — рисуются заново (ниже кронштейнов целиком, выше — только светлое стекло)
glass_px = cv2.dilate((((r - bl) < 14) & (lum > 150)).astype(np.uint8), np.ones((3, 3), np.uint8), iterations=2).astype(bool)
for gx0, gx1 in [(243, 392), (633, 783)]:
    sl = (slice(132 * S, 226 * S), slice(gx0 * S, gx1 * S))
    base[sl] = wfull[sl]
    rest[sl] = False
b8 = np.clip(base, 0, 255).astype(np.uint8)
b8 = cv2.inpaint(b8, rest.astype(np.uint8) * 255, 5, cv2.INPAINT_TELEA)
Image.fromarray(b8).save(os.path.join(WORK, "clean_base.png"))
print("ok")
