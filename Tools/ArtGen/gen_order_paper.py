"""Лист заказа и прищепка для доски «Выбери заказ» — в стиле картинок блюд и ингредиентов:
мягкие градиенты, тёплый контур на тон темнее заливки, скруглённые формы, блики.
Запуск: python gen_order_paper.py  ->  Assets/Resources/UI/order_paper.png, order_clip.png"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "UI"))
SS = 4                               # суперсэмплинг

def grad(w, h, c0, c1, vertical=True):
    t = np.linspace(0, 1, h if vertical else w)
    t = t * t * (3 - 2 * t)
    c0, c1 = np.array(c0, float), np.array(c1, float)
    line = c0 + (c1 - c0) * t[:, None]
    arr = np.repeat(line[:, None], w, 1) if vertical else np.repeat(line[None], h, 0)
    return Image.fromarray(np.dstack([arr, np.full((h, w), 255.0)]).astype(np.uint8), "RGBA")

def masked(img, mask):
    out = img.copy(); out.putalpha(mask); return out

def ring_of(mask, width):
    grow = mask.filter(ImageFilter.MaxFilter(width * 2 + 1))
    a = np.clip(np.array(grow, int) - np.array(mask.filter(ImageFilter.MinFilter(3)), int), 0, 255)
    return Image.fromarray(a.astype(np.uint8))

def solid(size, color, alpha_mask):
    im = Image.new("RGBA", size, color + (0,)); im.putalpha(alpha_mask); return im

def paper(W=600, H=680):
    w, h = W * SS, H * SS
    R, CURL, PAD = 40 * SS, 64 * SS, 6 * SS
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    # силуэт: скруглённый лист без правого нижнего угла (там загнутый уголок)
    m = Image.new("L", (w, h), 0); d = ImageDraw.Draw(m)
    d.rounded_rectangle([PAD, PAD, w - PAD, h - PAD], R, fill=255)
    d.polygon([(w - PAD - CURL, h), (w, h), (w, h - PAD - CURL)], fill=0)
    # заливка: тёплый кремовый градиент сверху вниз + мягкая виньетка + лёгкое зерно
    body = grad(w, h, (255, 253, 247), (244, 234, 214))
    yy, xx = np.mgrid[0:h, 0:w]
    vig = 1 - 0.06 * (((xx - w / 2) / (w / 2)) ** 2 + ((yy - h / 2) / (h / 2)) ** 2)
    rng = np.random.default_rng(5)
    grain = rng.normal(0, 2.2, (h // SS, w // SS)).repeat(SS, 0).repeat(SS, 1)
    b = np.array(body).astype(float)
    b[..., :3] = np.clip(b[..., :3] * vig[..., None] + grain[..., None], 0, 255)
    img.alpha_composite(masked(Image.fromarray(b.astype(np.uint8)), m))
    # загнутый уголок: треугольник-клапан с тенью под ним
    fold = [(w - PAD - CURL, h - PAD), (w - PAD, h - PAD - CURL), (w - PAD - CURL * 0.92, h - PAD - CURL * 0.92)]
    sh = Image.new("L", (w, h), 0); ImageDraw.Draw(sh).polygon([(x - 6 * SS, y - 6 * SS) for x, y in fold], fill=110)
    sh = sh.filter(ImageFilter.GaussianBlur(7 * SS))
    sh = Image.fromarray(np.minimum(np.array(sh), np.array(m)).astype(np.uint8))
    img.alpha_composite(solid((w, h), (120, 90, 50), sh))
    fm = Image.new("L", (w, h), 0); ImageDraw.Draw(fm).polygon(fold, fill=255)
    img.alpha_composite(masked(grad(w, h, (246, 238, 222), (226, 210, 180), vertical=False), fm))
    # контур — тёплый, на тон темнее бумаги (как у ингредиентов)
    full = Image.fromarray(np.maximum(np.array(m), np.array(fm)).astype(np.uint8))
    img.alpha_composite(solid((w, h), (176, 138, 88), ring_of(full, 3 * SS)))
    img.alpha_composite(solid((w, h), (176, 138, 88), ring_of(fm, 2 * SS)))
    # блик сверху
    hl = Image.new("L", (w, h), 0); ImageDraw.Draw(hl).rounded_rectangle([PAD + 26 * SS, PAD + 14 * SS, w - PAD - 26 * SS, PAD + 24 * SS], 6 * SS, fill=150)
    img.alpha_composite(solid((w, h), (255, 255, 255), hl.filter(ImageFilter.GaussianBlur(4 * SS))))
    return img.resize((W, H), Image.LANCZOS)

def clip(W=200, H=136):
    """Прищепка: ножка (уходит в кольцо на перекладине) и зажим, держащий верх листа. 2 px = 1 ед. холста."""
    w, h = W * SS, H * SS
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    OUT = (40, 42, 48)
    # ножка
    sm = Image.new("L", (w, h), 0); ImageDraw.Draw(sm).rounded_rectangle([w / 2 - 18 * SS, 0, w / 2 + 18 * SS, 50 * SS], 9 * SS, fill=255)
    img.alpha_composite(masked(grad(w, h, (96, 100, 110), (150, 155, 166), vertical=False), sm))
    img.alpha_composite(solid((w, h), OUT, ring_of(sm, 3 * SS)))
    # зажим — скруглённая «подушка» с выпуклым верхом
    cm = Image.new("L", (w, h), 0); d = ImageDraw.Draw(cm)
    d.rounded_rectangle([6 * SS, 40 * SS, w - 6 * SS, h - 6 * SS], 26 * SS, fill=255)
    body = np.array(grad(w, h, (150, 155, 166), (84, 88, 98))).astype(float)
    yy, xx = np.mgrid[0:h, 0:w]
    body[..., :3] *= (1 - 0.18 * ((xx - w / 2) / (w / 2)) ** 2)[..., None]   # объём по бокам
    img.alpha_composite(masked(Image.fromarray(np.clip(body, 0, 255).astype(np.uint8)), cm))
    # блик и линия-«губа» зажима
    hl = Image.new("L", (w, h), 0); ImageDraw.Draw(hl).rounded_rectangle([30 * SS, 52 * SS, w - 30 * SS, 66 * SS], 7 * SS, fill=190)
    img.alpha_composite(solid((w, h), (255, 255, 255), hl.filter(ImageFilter.GaussianBlur(3 * SS))))
    lip = Image.new("L", (w, h), 0); ImageDraw.Draw(lip).rounded_rectangle([20 * SS, h - 34 * SS, w - 20 * SS, h - 28 * SS], 3 * SS, fill=140)
    img.alpha_composite(solid((w, h), (40, 42, 48), lip.filter(ImageFilter.GaussianBlur(1 * SS))))
    img.alpha_composite(solid((w, h), OUT, ring_of(cm, 3 * SS)))
    return img.resize((W, H), Image.LANCZOS)

if __name__ == "__main__":
    os.makedirs(ROOT, exist_ok=True)
    paper().save(os.path.join(ROOT, "order_paper.png")); clip().save(os.path.join(ROOT, "order_clip.png"))
    print("saved order_paper.png, order_clip.png ->", ROOT)
