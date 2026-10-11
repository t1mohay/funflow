"""Собирает экран выбора блюда на том же фоне, что игра и главное меню.

Из макета дизайнера (dishes_mockup.jpg) вырезаются заголовок, карточки и кнопка «Назад»:
стена вокруг них удаляется заливкой от краёв вырезки (внутренние зелёные детали — салат — не трогаются).
Карточки ставятся в порядке салат → пицца → том ям.

python compose_dishes.py background.jpg dishes_mockup.jpg out.png
Требуется: numpy, scipy, pymupdf.
"""
import sys
import numpy as np
import pymupdf
from scipy import ndimage


def load(path):
    p = pymupdf.Pixmap(path)
    return np.frombuffer(p.samples, dtype=np.uint8).reshape(p.height, p.width, p.n)[..., :3].astype(np.float32)


def cutout(img, x0, y0, x1, y1):
    """Вырезка с альфой: стена, связанная с краем вырезки, становится прозрачной."""
    part = img[y0:y1, x0:x1]
    r, g, b = part[..., 0], part[..., 1], part[..., 2]
    wall = (g > r + 4) & (g >= b - 5) & (r + g + b > 250)
    labels, _ = ndimage.label(wall)
    border = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))) - {0}
    outside = np.isin(labels, list(border))
    alpha = (~outside).astype(np.float32)
    # мягкий край в 1–2 пикселя, без зелёного ореола
    alpha = ndimage.uniform_filter(ndimage.binary_erosion(alpha > 0.5, iterations=1).astype(np.float32), 3)
    return np.dstack([part, alpha])


def scale(rgba, k):
    return np.dstack([ndimage.zoom(rgba[..., c], k, order=1) for c in range(4)])


def paste(base, rgba, x, y):
    h, w = rgba.shape[:2]
    a = np.clip(rgba[..., 3:4], 0, 1)
    base[y:y + h, x:x + w] = base[y:y + h, x:x + w] * (1 - a) + rgba[..., :3] * a


def main(bg_path, mock_path, out_path):
    base = load(bg_path)
    mock = load(mock_path)

    title = scale(cutout(mock, 375, 75, 915, 200), 0.72)
    salad = scale(cutout(mock, 797, 213, 1093, 554), 0.85)
    pizza = scale(cutout(mock, 185, 213, 489, 554), 0.85)
    tomyum = scale(cutout(mock, 498, 213, 787, 554), 0.85)
    back = cutout(mock, 20, 495, 172, 557)

    paste(base, title, 620 - title.shape[1] // 2, 162)
    x = 248
    for card in (salad, pizza, tomyum):
        paste(base, card, x, 265)
        print("card", x, 265, x + card.shape[1], 265 + card.shape[0])
        x += card.shape[1] + 12
    paste(base, back, 55, 475)
    print("back", 55, 475, 55 + back.shape[1], 475 + back.shape[0])

    out = np.clip(base, 0, 255).astype(np.uint8)
    pymupdf.Pixmap(pymupdf.csRGB, out.shape[1], out.shape[0], out.tobytes(), False).save(out_path)


if __name__ == "__main__":
    main(*sys.argv[1:4])
