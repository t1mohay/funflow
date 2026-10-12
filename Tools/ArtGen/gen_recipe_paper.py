"""Большой лист рецепта (экран «Выбор блюда» → лист с историей и ингредиентами) в стиле листов заказа:
кремовый градиент, виньетка, лёгкое зерно, тёплый контур на тон темнее, блик сверху.
Уголка здесь нет — его загибает и анимирует код (PaperSheet + PaperCurl), он вырезает угол из этой картинки.
Запуск: python gen_recipe_paper.py  ->  Assets/Resources/UI/recipe_paper.png (1000×1000, 1 px = 1 ед. холста)"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from gen_order_paper import ROOT, SS, grad, masked, ring_of, solid


def recipe_paper(W=1000, H=1000):
    w, h = W * SS, H * SS
    R, PAD = 30 * SS, 4 * SS
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle([PAD, PAD, w - PAD, h - PAD], R, fill=255)
    body = grad(w, h, (255, 253, 247), (245, 236, 217))
    yy, xx = np.mgrid[0:h, 0:w]
    vig = 1 - 0.05 * (((xx - w / 2) / (w / 2)) ** 2 + ((yy - h / 2) / (h / 2)) ** 2)
    rng = np.random.default_rng(7)
    grain = rng.normal(0, 2.0, (h // SS, w // SS)).repeat(SS, 0).repeat(SS, 1)
    b = np.array(body).astype(float)
    b[..., :3] = np.clip(b[..., :3] * vig[..., None] + grain[..., None], 0, 255)
    img.alpha_composite(masked(Image.fromarray(b.astype(np.uint8)), m))
    # контур — тот же тёплый, что у листов заказа и уголка
    img.alpha_composite(solid((w, h), (176, 138, 88), ring_of(m, 3 * SS)))
    # мягкий блик вдоль верхнего края
    hl = Image.new("L", (w, h), 0)
    ImageDraw.Draw(hl).rounded_rectangle([PAD + 40 * SS, PAD + 16 * SS, w - PAD - 40 * SS, PAD + 26 * SS], 6 * SS, fill=140)
    img.alpha_composite(solid((w, h), (255, 255, 255), hl.filter(ImageFilter.GaussianBlur(5 * SS))))
    return img.resize((W, H), Image.LANCZOS)


if __name__ == "__main__":
    os.makedirs(ROOT, exist_ok=True)
    recipe_paper().save(os.path.join(ROOT, "recipe_paper.png"))
    print("saved recipe_paper.png ->", ROOT)
