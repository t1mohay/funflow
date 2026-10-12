# Фон экрана выбора блюда: только стена из плитки (без окна, утвари, полки) и стол из исходного фона.
import sys
import numpy as np
from PIL import Image

src, out = sys.argv[1], sys.argv[2]
im = np.asarray(Image.open(src).convert('RGB')).astype(np.float32)
H, W, _ = im.shape
TABLE = 549  # стена — до этой строки
# Края теней и лучей на столе — отрезки от (x, y) до (x, y) исходного фона.
SHADOW_EDGES = [
    ((870, 556), (1280, 676)),  # тень справа
    ((0, 667), (380, 853)),     # тень слева внизу
    ((207, 556), (194, 645)),   # левый край светлого луча
    ((380, 556), (372, 645)),   # правый край светлого луча
]
EDGE_BAND = 14
SHADE_PASSES = [(10, 30), (4, 9)]  # размытие для поиска лучей на столе: поперёк и вдоль досок
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

# Стол без солнечных лучей и теней: крупные пятна яркости (размытие сильнее вдоль досок) выравниваем
# к обычной яркости своей строки — доски, их стыки и волокна дерева остаются.
from scipy.ndimage import gaussian_filter
top = TABLE + EDGE
# Два прохода: крупный убирает сами пятна, мелкий — следы их краёв.
for sy, sx in SHADE_PASSES:
    table = result[top:]
    low = np.stack([gaussian_filter(table[..., c], sigma=(sy, sx), mode='nearest') for c in range(3)], -1)
    target = np.median(low, axis=1, keepdims=True)
    result[top:] = table * target / np.maximum(low, 1.0)

# Резкие края теней (прямые на исходном фоне) после выравнивания остаются тонкими линиями.
# Волокна дерева идут вдоль строк, поэтому полосу вокруг края заполняем, протягивая каждую строку
# от левого края полосы к правому.
for (xa, ya), (xb, yb) in SHADOW_EDGES:
    for y in range(max(ya, top), min(yb, H)):
        x0 = int(round(xa + (xb - xa) * (y - ya) / (yb - ya)))
        l, r = x0 - EDGE_BAND, x0 + EDGE_BAND
        if r <= 2 or l >= W - 3:
            continue
        l, r = max(l, 2), min(r, W - 3)
        left = result[y, l - 2:l + 1].mean(0)
        right = result[y, r:r + 3].mean(0)
        k = np.linspace(0.0, 1.0, r - l)[:, None]
        result[y, l:r] = left * (1 - k) + right * k
Image.fromarray(np.clip(result, 0, 255).astype(np.uint8)).save(out)
print('saved', out)
