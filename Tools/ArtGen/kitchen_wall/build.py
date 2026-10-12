"""Шаг 2: нарисовать предметы на очищенной стене. Запуск после clean.py:
    python clean.py && python build.py   ->  Assets/Resources/Kitchen/wall.png (2048×1144)
Верх обрезан по низу полки, снизу дорисован стол (кадр экрана выбора заказа).
Качающиеся предметы (табличка «Выбор блюда», бокалы, плети плюща) в стену не впечатываются:
они сохраняются отдельными картинками в Kitchen/WallLayers, а их места и точки подвеса —
в Kitchen/wall_layers.json (игра покачивает их, см. GameUI.BuildWallLayers и WallSway)."""
import os, sys, json
import numpy as np, cv2
from PIL import Image
import objects as O
S = 2
COUNTER = 496.5 * S
SHELF_TOP = 119.5 * S
HERE = os.path.dirname(os.path.abspath(__file__))
import tempfile
WORK = os.path.join(tempfile.gettempdir(), "funflow_kitchen_wall")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "Kitchen", "wall.png")
bg = Image.open(os.path.join(WORK, "clean_base.png")).convert("RGBA")
clean = np.array(bg)
# Кадр экрана выбора заказа: верх обрезается на CROP пикселей (см. конец файла).
CROP = int(round(131.5 * S))
LAYER_DIR = os.path.join(os.path.dirname(OUT), "WallLayers")
os.makedirs(LAYER_DIR, exist_ok=True)
layers = []


def sway(c, name, pivot, amp, period, phase):
    """Отдельный слой вместо впечатывания: картинка + место в кадре и точка подвеса (в пикселях итоговой стены).
    amp — размах в градусах, period — период в секундах, phase — сдвиг фазы (доля периода)."""
    im, (px, py) = c.to_image()
    py -= CROP
    im.save(os.path.join(LAYER_DIR, name + ".png"))
    layers.append(dict(name=name, x=int(px), y=int(py), w=im.width, h=im.height,
                       pivotX=round(pivot[0] * S, 1), pivotY=round(pivot[1] * S - CROP, 1),
                       amp=amp, period=period, phase=phase))


def put(c, wall_shadow=None, dy=0):
    """wall_shadow: нижняя граница стены (px) — тень предмета на плитке, свет слева сверху.
    dy — сдвиг по вертикали в единицах исходника."""
    global bg
    im, (px, py) = c.to_image()
    py += int(round(dy * S))
    W, H = bg.size
    # обрезка по краям фона
    x0, y0 = max(0, -px), max(0, -py)
    x1, y1 = min(im.width, W - px), min(im.height, H - py)
    im = im.crop((x0, y0, x1, y1)); px += x0; py += y0
    if wall_shadow is not None:
        a = np.asarray(im)[..., 3].astype(np.float32) / 255
        pad = 16
        A = np.zeros((a.shape[0] + 2 * pad, a.shape[1] + 2 * pad), np.float32)
        A[pad + 6:pad + 6 + a.shape[0], pad + 8:pad + 8 + a.shape[1]] = a     # сдвиг вправо-вниз
        A = cv2.GaussianBlur(A, (0, 0), 4.0) * 0.42
        ys = np.arange(A.shape[0])[:, None] + py - pad
        A *= np.clip((wall_shadow - ys) / 3.0, 0, 1)                           # только на стене
        layer = np.zeros(A.shape + (4,), np.uint8)
        layer[..., :3] = (66, 76, 44)
        layer[..., 3] = np.clip(A * 255, 0, 255).astype(np.uint8)
        sh = Image.fromarray(layer, "RGBA")
        sx, sy = px - pad, py - pad
        cx0, cy0 = max(0, -sx), max(0, -sy)
        sh = sh.crop((cx0, cy0, min(sh.width, W - sx), min(sh.height, H - sy)))
        bg.alpha_composite(sh, (sx + cx0, sy + cy0))
    bg.alpha_composite(im, (px, py))


# рамки и доска опущены, чтобы после обрезки верха не упирались в край кадра
FRAMES_DY = 22
for c in O.wall_frames(): put(c, dy=FRAMES_DY)
for c in O.glass_holders(): put(c)
# Бокалы висят вверх ножкой — качаются вокруг подставки; у каждого свой период и фаза.
for i, (c, (gx, gy)) in enumerate(zip(O.glasses(), O.glass_feet())):
    sway(c, f"glass_{i:02d}", (gx, gy), amp=round(1.4 + 0.25 * (i % 3), 2), period=round(3.2 + 0.37 * ((i * 5) % 7), 2), phase=round((i * 0.173) % 1, 3))
# Табличка на двух цепочках — точка подвеса посередине между ними, у балки.
sway(O.hanging_sign(), "sign", O.SIGN_PIVOT, amp=1.1, period=5.2, phase=0.0)
back, front = O.shelf_objects()
for c in back: put(c, wall_shadow=SHELF_TOP)
# передняя перекладина поверх предметов полки
arr = np.array(bg)
arr[95*S:103*S, 206*S:828*S] = clean[95*S:103*S, 206*S:828*S]
bg = Image.fromarray(arr)
# Плети плюща свисают с полки: качаются вокруг края горшка, медленно.
for i, (c, piv) in enumerate(zip(front, O.ivy_pivots())):
    sway(c, f"ivy_{i}", piv, amp=1.6, period=round(6.4 + 0.9 * i, 2), phase=round(0.35 * i, 2))
for c in O.counter_objects(): put(c, wall_shadow=COUNTER)
# ── кадр для экрана выбора заказа: верх обрезан ровно по низу полки (полка уходит за край,
# в кадре — бокалы, плющ и табличка), снизу столько же дорисовано столешницей — пропорции прежние.
arr = np.array(bg.convert("RGB"))
H = arr.shape[0]
PLANK0, PLANK1 = int(525 * S), int(552 * S)          # одна доска передней стенки стола (со стыком)
plank = clean[PLANK0:PLANK1, :, :3].astype(np.float32)   # доска без предметов
period = PLANK1 - PLANK0
extra = []
y = PLANK1
while y < H + CROP:
    k = (y - PLANK1) // period
    extra.append(np.clip(plank * (0.97 - 0.035 * k), 0, 255))      # ниже — чуть темнее, как в тени
    y += period
ext = np.concatenate(extra, 0)[:H + CROP - PLANK1].astype(np.uint8)
arr = np.concatenate([arr[:PLANK1], ext], 0)[CROP:CROP + H]
Image.fromarray(arr).save(OUT)
with open(os.path.join(os.path.dirname(OUT), "wall_layers.json"), "w", encoding="utf-8") as f:
    json.dump({"width": arr.shape[1], "height": arr.shape[0], "layers": layers}, f, ensure_ascii=False, indent=1)
print("layers", len(layers), "->", os.path.normpath(LAYER_DIR))
print("saved", os.path.normpath(OUT), arr.shape[1], "x", arr.shape[0])
