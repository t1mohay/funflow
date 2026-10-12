# Фон экрана выбора блюда: только стена из плитки (без окна, утвари, полки) и стол из исходного фона.
import sys
import numpy as np
from PIL import Image

src, out = sys.argv[1], sys.argv[2]
im = np.asarray(Image.open(src).convert('RGB')).astype(np.float32)
H, W, _ = im.shape
TABLE = 549  # стена — до этой строки
EDGE = 4     # ровная светлая кромка столешницы; ниже неё — стол из оригинала (его край неровный, 547…550)

L = im.mean(2)

def grout_lines(profile, lo, hi):
    d = profile - np.convolve(profile, np.ones(15) / 15, 'same')
    return [i + lo for i in range(5, len(profile) - 5) if d[i] > 4 and d[i] == d[i - 3:i + 4].max()]

rows = grout_lines(L[0:540, 640:800].mean(1), 0, 540)
cols = grout_lines(L[250:540, :].mean(0), 0, W)
rows = [r for r in rows if r < 525]
print('rows', rows)
print('cols', cols)

CW, CH = 88, 75
cells = []
for r0, r1 in zip(rows, rows[1:]):
    for c0, c1 in zip(cols, cols[1:]):
        cell = Image.fromarray(im[r0:r1, c0:c1].astype(np.uint8)).resize((CW, CH), Image.BICUBIC)
        cells.append(np.asarray(cell).astype(np.float32))
cells = np.array(cells)

# Выравниваем освещение: у каждой ячейки — одинаковый средний цвет внутренней части плитки.
inner = (slice(12, CH - 12), slice(12, CW - 12))
means = cells[:, inner[0], inner[1]].mean((1, 2))
# Цвет — как на образце плитки (он снят с экрана, где фон затемнён тонировкой 0.6/0.6/0.64).
target = np.array([122, 124, 100], np.float32) / np.array([0.6, 0.6, 0.64], np.float32)
norm = cells / means[:, None, None, :] * target

# Отбрасываем ячейки с предметами и краями лучей: сильное отличие от медианы.
for _ in range(3):
    med = np.median(norm, 0)
    err = np.abs(norm - med).mean((1, 2, 3))
    keep = err <= np.percentile(err, 45)
    norm, err = norm[keep], err[keep]
print('clean cells', len(norm))
med = np.median(norm, 0)

rng = np.random.default_rng(7)
wall = np.zeros((TABLE, W, 3), np.float32)
y0 = rows[-1] % CH - CH  # фаза сетки как на исходном фоне (по нижней линии затирки)
x0 = cols[0] % CW - CW
for y in range(y0, TABLE, CH):
    for x in range(x0, W, CW):
        # Немного живости: реальная плитка, смешанная с «идеальной».
        cell = 0.55 * med + 0.45 * norm[rng.integers(len(norm))]
        ys, xs = max(y, 0), max(x, 0)
        ye, xe = min(y + CH, TABLE), min(x + CW, W)
        wall[ys:ye, xs:xe] = cell[ys - y:ye - y, xs - x:xe - x]

# Мягкое освещение: чуть светлее в центре и сверху, темнее к углам и у самого стола.
yy, xx = np.mgrid[0:TABLE, 0:W].astype(np.float32)
light = 1.0 - 0.10 * (((xx - W / 2) / (W / 2)) ** 2) - 0.05 * (yy / TABLE)
light -= 0.10 * np.clip(1 - (TABLE - yy) / 40, 0, 1) ** 2
wall *= light[..., None]

result = im.copy()
result[:TABLE] = wall
# Кромка: светлая полоса с бликом сверху и чуть темнее к дереву.
edge = np.array([[252, 236, 170], [250, 226, 156], [244, 210, 138], [236, 192, 118]], np.float32)
result[TABLE:TABLE + EDGE] = edge[:, None, :]
Image.fromarray(np.clip(result, 0, 255).astype(np.uint8)).save(out)
print('saved', out)
