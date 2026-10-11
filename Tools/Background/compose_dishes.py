"""Собирает картинки меню на том же фоне, что игра.

Из макетов дизайнера вырезаются кнопки, карточки и заголовок: стена вокруг них удаляется
заливкой от краёв вырезки (внутренние зелёные детали — салат, оливковая кнопка — не трогаются).

Результат:
  dishes.png     — фон игры + заголовок, карточки (салат → пицца → том ям) и «Назад»
  dishes_ui.png  — только эти элементы на прозрачном фоне (рисуются поверх без затемнения)
  main_ui.png    — кнопки главного меню на прозрачном фоне

python compose_dishes.py background.jpg dishes_mockup.jpg main_menu.jpg out_dir
Требуется: numpy, scipy, pymupdf.
"""
import os
import sys
import numpy as np
import pymupdf
from scipy import ndimage


def load(path):
    p = pymupdf.Pixmap(path)
    return np.frombuffer(p.samples, dtype=np.uint8).reshape(p.height, p.width, p.n)[..., :3].astype(np.float32)


def save(path, img):
    img = np.clip(img, 0, 255).astype(np.uint8)
    alpha = img.shape[2] == 4
    cs = pymupdf.csRGB
    pymupdf.Pixmap(cs, img.shape[1], img.shape[0], img.tobytes(), alpha).save(path)


def cutout(img, x0, y0, x1, y1, ref=None):
    """Вырезка с альфой.

    ref — та же картинка без элемента (фон): тогда прозрачно всё, что совпадает с фоном (точно).
    Без ref стена, связанная с краем вырезки, определяется по цвету.
    """
    part = img[y0:y1, x0:x1]
    # стена (и тени на ней) по цвету: зеленоватые пиксели, связанные с краем вырезки
    r, g, b = part[..., 0], part[..., 1], part[..., 2]
    wall = (g > r + 2) & (g >= b - 8) & (r + g + b > 150)
    labels, _ = ndimage.label(wall)
    border = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))) - {0}
    not_wall = ~np.isin(labels, list(border))
    if ref is not None:
        diff = np.abs(part - ref[y0:y1, x0:x1]).sum(axis=2)
        mask = ndimage.binary_fill_holes(ndimage.binary_opening(diff > 100, iterations=1))
        # шум JPEG даёт мелкие пятна — оставляем только крупные фигуры (сами кнопки)
        labels, n = ndimage.label(mask)
        sizes = ndimage.sum(mask, labels, range(1, n + 1))
        inside = np.isin(labels, [i + 1 for i, s in enumerate(sizes) if s > 3000]) & not_wall
    else:
        inside = not_wall
    # край срезаем на 2 пикселя и смягчаем — без светлых и зелёных ниточек
    alpha = ndimage.uniform_filter(ndimage.binary_erosion(inside, iterations=2).astype(np.float32), 3)
    return np.dstack([part, alpha])


def scale(rgba, k):
    return np.dstack([ndimage.zoom(rgba[..., c], k, order=1) for c in range(4)])


def paste(base, overlay, rgba, x, y):
    """Кладёт вырезку на фон (base, RGB) и на прозрачный слой (overlay, RGBA)."""
    h, w = rgba.shape[:2]
    a = np.clip(rgba[..., 3:4], 0, 1)
    if base is not None:
        base[y:y + h, x:x + w] = base[y:y + h, x:x + w] * (1 - a) + rgba[..., :3] * a
    o = overlay[y:y + h, x:x + w]
    oa = o[..., 3:4] / 255.0
    out_a = a + oa * (1 - a)
    o[..., :3] = np.where(out_a > 0, (rgba[..., :3] * a + o[..., :3] * oa * (1 - a)) / np.maximum(out_a, 1e-6), 0)
    o[..., 3:4] = out_a * 255.0
    print(f"  ({x}, {y}) – ({x + w}, {y + h})")


def main(bg_path, mock_path, main_path, out_dir):
    base = load(bg_path)
    mock = load(mock_path)
    menu = load(main_path)
    H, W = base.shape[:2]

    # ── Выбор блюда
    dishes_ui = np.zeros((H, W, 4), np.float32)
    title = scale(cutout(mock, 375, 75, 915, 168), 0.72)   # без мелкой подписи под заголовком
    salad = scale(cutout(mock, 797, 213, 1093, 554), 0.85)
    pizza = scale(cutout(mock, 185, 213, 489, 554), 0.85)
    tomyum = scale(cutout(mock, 498, 213, 787, 554), 0.85)
    back = cutout(mock, 20, 495, 172, 557)
    print("title"); paste(base, dishes_ui, title, 620 - title.shape[1] // 2, 175)
    x = 248
    for name, card in (("salad", salad), ("pizza", pizza), ("tomyum", tomyum)):
        print(name); paste(base, dishes_ui, card, x, 265)
        x += card.shape[1] + 12
    print("back"); paste(base, dishes_ui, back, 55, 475)
    save(os.path.join(out_dir, "dishes.png"), base)
    save(os.path.join(out_dir, "dishes_ui.png"), dishes_ui)

    # ── Главное меню: кнопки поверх фона в полную яркость
    main_ui = np.zeros((H, W, 4), np.float32)
    for name, (x0, y0, x1, y1) in (("start", (430, 186, 852, 297)), ("settings", (430, 303, 852, 412)),
                                    ("credits", (430, 418, 852, 525))):
        print(name); paste(None, main_ui, cutout(menu, x0, y0, x1, y1, ref=load(bg_path)), x0, y0)
    save(os.path.join(out_dir, "main_ui.png"), main_ui)


if __name__ == "__main__":
    main(*sys.argv[1:5])
