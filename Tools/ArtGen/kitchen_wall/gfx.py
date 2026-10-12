"""Мини-рендер предметов в стиле игровых спрайтов: мягкие объёмы, глянцевые блики,
тонкий тёмный контур того же оттенка. Рисуем в локальном холсте с суперсэмплингом K px на
единицу исходника, затем сжимаем до 2 px/ед. (масштаб фона 2048×1144)."""
import math
import numpy as np, cv2
from PIL import Image, ImageDraw

K = 8          # пикселей на единицу исходника при рисовании
OUT = 2        # пикселей на единицу в итоговом фоне
LIGHT = np.array([-0.55, -0.75, 0.75])
LIGHT = LIGHT / np.linalg.norm(LIGHT)


def rgb(c):
    return np.array(c[:3], np.float32) / 255.0


class Canvas:
    def __init__(self, x0, y0, x1, y1):
        self.ox, self.oy = int(math.floor(x0)), int(math.floor(y0))
        self.w = (int(math.ceil(x1)) - self.ox) * K
        self.h = (int(math.ceil(y1)) - self.oy) * K
        self.rgb = np.zeros((self.h, self.w, 3), np.float32)
        self.a = np.zeros((self.h, self.w), np.float32)

    # координаты исходника → пиксели холста
    def P(self, x, y):
        return ((x - self.ox) * K, (y - self.oy) * K)

    def pts(self, pts):
        return [self.P(x, y) for x, y in pts]

    def mask(self, fn):
        """fn(draw, canvas) рисует фигуры белым; возвращает маску 0..1."""
        im = Image.new("L", (self.w, self.h), 0)
        fn(ImageDraw.Draw(im), self)
        return np.asarray(im, np.float32) / 255.0

    def poly(self, pts):
        return self.mask(lambda d, c: d.polygon(c.pts(pts), fill=255))

    def ellipse(self, x0, y0, x1, y1):
        return self.mask(lambda d, c: d.ellipse([*c.P(x0, y0), *c.P(x1, y1)], fill=255))

    def rrect(self, x0, y0, x1, y1, r):
        return self.mask(lambda d, c: d.rounded_rectangle([*c.P(x0, y0), *c.P(x1, y1)], radius=r * K, fill=255))

    def line(self, pts, width):
        def f(d, c):
            q = c.pts(pts)
            d.line(q, fill=255, width=max(1, int(width * K)), joint="curve")
            rr = width * K / 2
            for x, y in (q[0], q[-1]):
                d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=255)
        return self.mask(f)

    def over(self, col, alpha):
        """Наложить слой (H,W,3) или цвет с альфой (H,W)."""
        col = np.broadcast_to(col, self.rgb.shape)
        a = np.clip(alpha, 0, 1)[..., None]
        out_a = a[..., 0] + self.a * (1 - a[..., 0])
        num = col * a + self.rgb * self.a[..., None] * (1 - a)
        self.rgb = np.where(out_a[..., None] > 1e-6, num / np.maximum(out_a[..., None], 1e-6), 0)
        self.a = out_a

    def tint(self, col, alpha):
        """Подмешать цвет поверх уже нарисованного, не меняя альфу (тени, блики внутри формы)."""
        a = np.clip(alpha, 0, 1)[..., None]
        self.rgb = self.rgb * (1 - a) + np.broadcast_to(col, self.rgb.shape) * a

    def to_image(self):
        """Сжать до итогового масштаба (премультиплицированно), вернуть RGBA и позицию в пикселях фона."""
        f = K // OUT
        pm = np.dstack([self.rgb * self.a[..., None], self.a])
        small = cv2.resize(pm, (self.w // f, self.h // f), interpolation=cv2.INTER_AREA)
        a = small[..., 3:4]
        col = np.where(a > 1e-4, small[..., :3] / np.maximum(a, 1e-4), 0)
        arr = np.dstack([np.clip(col, 0, 1), np.clip(a, 0, 1)])
        return Image.fromarray((arr * 255 + 0.5).astype(np.uint8), "RGBA"), (self.ox * OUT, self.oy * OUT)


# ───────────────────────── шейдинг
def blur(m, sigma_units):
    s = max(0.5, sigma_units * K)
    return cv2.GaussianBlur(m, (0, 0), s)


def dist_in(m):
    return cv2.distanceTransform((m > 0.5).astype(np.uint8), cv2.DIST_L2, 5) / K   # в единицах исходника


def row_extent(m):
    """u ∈ [0,1] поперёк формы для каждого пикселя (по строке)."""
    on = m > 0.5
    has = on.any(1)
    l = np.where(has, on.argmax(1), 0).astype(np.float32)
    r = np.where(has, m.shape[1] - 1 - on[:, ::-1].argmax(1), 1).astype(np.float32)
    xs = np.arange(m.shape[1], dtype=np.float32)[None]
    u = (xs - l[:, None]) / np.maximum(r - l, 1)[:, None]
    return np.clip(u, 0, 1)


def col_extent(m):
    return row_extent(m.T).T


def outline(c, m, base, width=0.55, dark=0.55, alpha=1.0):
    d = dist_in(m)
    t = np.clip((width - d) / 0.35 + 0.5, 0, 1) * (m > 0.02)
    c.tint(rgb(base) * dark, t * alpha)


def fill_cyl(c, m, base, amb=0.62, light_u=0.32, spec=0.55, spec_u=0.24, spec_w=0.09, alpha=1.0,
             vgrad=0.08, line=True, line_w=0.55, line_dark=0.55, rim=0.18, umask=None):
    """Цилиндр по горизонтали: яркость по u, вертикальный блик, тёмный контур.
    umask — форма, по которой считается «поперёк» (например, корпус для этикетки)."""
    u = row_extent(m if umask is None else umask)
    lam = np.clip(np.cos((u - light_u) * math.pi * 0.95), 0, 1)
    shade = amb + (1 - amb) * lam
    v = col_extent(m if umask is None else umask)
    shade = shade * (1 + vgrad * (0.5 - v))
    d = dist_in(m)
    shade = shade * (1 - rim * np.exp(-d / 1.2))
    col = np.clip(rgb(base)[None, None] * shade[..., None], 0, 1)
    c.over(col, m * alpha)
    if spec > 0:
        s = np.clip(1 - np.abs(u - spec_u) / spec_w, 0, 1) ** 1.5
        s *= np.clip((d - 0.8) / 1.5, 0, 1) * np.clip(np.minimum(v, 1 - v) / 0.12, 0, 1)
        c.tint(np.ones(3, np.float32), blur(s * m, 0.25) * spec)
    if line:
        outline(c, m, base, line_w, line_dark)


def fill_soft(c, m, base, height=2.5, amb=0.55, diff=0.55, spec=0.35, shin=18, alpha=1.0,
              line=True, line_w=0.55, line_dark=0.55, rim=0.15):
    """«Надутая» форма: нормали из размытой маски, ламберт + блик."""
    h = blur(m, height * 0.6) * height * K
    gy, gx = np.gradient(h)
    n = np.dstack([-gx, -gy, np.ones_like(h) * 1.0])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    lam = np.clip((n * LIGHT).sum(2), 0, 1)
    hv = LIGHT + np.array([0, 0, 1.0]); hv /= np.linalg.norm(hv)
    sp = np.clip((n * hv).sum(2), 0, 1) ** shin
    d = dist_in(m)
    shade = (amb + diff * lam) * (1 - rim * np.exp(-d / 1.0))
    col = np.clip(rgb(base)[None, None] * shade[..., None], 0, 1)
    c.over(col, m * alpha)
    if spec > 0:
        c.tint(np.ones(3, np.float32), sp * spec * (m > 0.5) * np.clip(d / 0.6, 0, 1))
    if line:
        outline(c, m, base, line_w, line_dark)


def highlight(c, m, pts_or_mask, alpha=0.6, soft=0.35):
    hm = pts_or_mask if isinstance(pts_or_mask, np.ndarray) else c.poly(pts_or_mask)
    c.tint(np.ones(3, np.float32), blur(hm, soft) * m * alpha)


def sprite(c, path, box, alpha_cut=40, fill=1.0, angle=0.0, tint=None, opacity=1.0, clip=None):
    """Вставить игровой спрайт в прямоугольник (единицы исходника)."""
    im = Image.open(path).convert("RGBA")
    al = np.asarray(im.split()[3]).copy(); al[al < alpha_cut] = 0
    im.putalpha(Image.fromarray(al)); im = im.crop(im.getbbox())
    if angle:
        im = im.rotate(angle, resample=Image.BICUBIC, expand=True)
        im = im.crop(im.getbbox())
    x0, y0, x1, y1 = box
    bw, bh = (x1 - x0) * K * fill, (y1 - y0) * K * fill
    k = min(bw / im.width, bh / im.height)
    im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
    cx, cy = c.P((x0 + x1) / 2, (y0 + y1) / 2)
    px, py = int(cx - im.width / 2), int(cy - im.height / 2)
    layer = np.zeros((c.h, c.w, 4), np.float32)
    arr = np.asarray(im, np.float32) / 255
    xa, ya = max(0, px), max(0, py)
    xb, yb = min(c.w, px + im.width), min(c.h, py + im.height)
    layer[ya:yb, xa:xb] = arr[ya - py:yb - py, xa - px:xb - px]
    col = layer[..., :3]
    if tint is not None:
        g = col.mean(2, keepdims=True)
        col = g * rgb(tint)[None, None] * 1.6
    a = layer[..., 3] * opacity
    if clip is not None:
        a = a * clip
    c.over(np.clip(col, 0, 1), a)


def leaf(c, bx, by, ang, length, width, base=(112, 178, 72), shape="ivy", line_w=0.45):
    """Лист: ang в градусах (0 — вправо, 90 — вниз). Половинки разной яркости + жилка."""
    t = np.linspace(0, 1, 40)
    if shape == "ivy":
        w = width * np.sin(np.pi * t ** 0.75) ** 0.9
    else:  # острый ланцетный
        w = width * np.sin(np.pi * t) ** 0.8 * (1 - 0.25 * t)
    ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    ax, ay = bx + ca * length * t, by + sa * length * t
    nx, ny = -sa, ca
    left = list(zip(ax + nx * w, ay + ny * w))
    right = list(zip(ax - nx * w, ay - ny * w))[::-1]
    m = c.poly(left + right)
    # освещённая половина — та, чья нормаль смотрит вверх-влево
    lit = 1 if (nx * LIGHT[0] + ny * LIGHT[1]) > 0 else -1
    H, W = m.shape
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    px, py = c.P(bx, by)
    side = ((xs - px) * nx + (ys - py) * ny) * lit
    along = np.clip(((xs - px) * ca + (ys - py) * sa) / (length * K), 0, 1)
    shade = np.where(side > 0, 1.08, 0.82) * (0.92 + 0.16 * along)
    col = np.clip(rgb(base)[None, None] * shade[..., None], 0, 1)
    c.over(col, m)
    # жилка
    vein = c.line([(bx + ca * length * 0.05, by + sa * length * 0.05), (bx + ca * length * 0.8, by + sa * length * 0.8)], 0.32)
    c.tint(rgb(base) * 1.35, vein * m * 0.55)
    # мягкий блик на светлой половине
    hl = np.clip(1 - np.abs(side / (width * K * 0.5) - 0.9), 0, 1) * (side > 0) * np.sin(np.pi * along) ** 2
    c.tint(np.ones(3, np.float32), blur(hl * m, 0.2) * 0.22)
    outline(c, m, base, line_w, 0.5)


def shadow_ellipse(c, cx, cy, rx, ry, alpha=0.35, color=(90, 50, 20), soft=1.6):
    m = c.ellipse(cx - rx, cy - ry, cx + rx, cy + ry)
    c.over(rgb(color), blur(m, soft) * alpha)


def lathe(cx, profile, n=200):
    """Силуэт тела вращения: profile — [(y, полуширина), ...] сверху вниз, сглаженная интерполяция."""
    ys = np.array([p[0] for p in profile], np.float64)
    ws = np.array([p[1] for p in profile], np.float64)
    yy = np.linspace(ys[0], ys[-1], n)
    try:
        from scipy.interpolate import PchipInterpolator
        ww = PchipInterpolator(ys, ws)(yy)
    except Exception:
        ww = np.interp(yy, ys, ws)
    left = list(zip(cx - ww, yy))
    right = list(zip(cx + ww, yy))[::-1]
    return left + right
