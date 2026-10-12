# Мини-трейлер для README: склеивает кадры, записанные игрой, в GIF.
#
# 1. Собрать игру и записать кадры:  Game.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -trailer <кадры>
#    (сценарий — Assets/Scripts/Dev/Trailer.cs: меню → выбор блюда → лист рецепта → нарезка → результат)
# 2. Смонтировать:  python make_trailer.py <кадры> Docs/trailer.gif   (нужен Pillow: pip install pillow)
#
# Части s1_*, s2_*, s3_* склеиваются с плавным переходом, в конце — титр на экране результата.
import glob
import os
import sys

from PIL import Image, ImageDraw, ImageFont

FPS = 20
WIDTH = 600          # ширина GIF
CROSSFADE = 6        # кадров перехода между частями
TITLE_HOLD = 2.0     # секунд держать финальный кадр с титром
COLORS = 160         # цветов в общей палитре

frames_dir, out_path = sys.argv[1], sys.argv[2]
root = os.path.dirname(os.path.abspath(__file__))
font_path = os.path.join(root, '..', '..', 'Assets', 'Resources', 'Fonts', 'Main.ttf')


def load(segment):
    files = sorted(glob.glob(os.path.join(frames_dir, f's{segment}_*.png')))
    return [Image.open(f).convert('RGB') for f in files]


def scaled(im):
    h = round(im.height * WIDTH / im.width)
    return im.resize((WIDTH, h), Image.LANCZOS)


segments = [s for s in (load(1), load(2), load(3)) if s]
frames = []
for seg in segments:
    seg = [scaled(f) for f in seg]
    if frames and len(seg) > CROSSFADE:
        # Последние кадры прошлой части плавно перетекают в первые кадры новой.
        tail = frames[-CROSSFADE:]
        del frames[-CROSSFADE:]
        for i in range(CROSSFADE):
            k = (i + 1) / (CROSSFADE + 1)
            frames.append(Image.blend(tail[i], seg[i], k))
        seg = seg[CROSSFADE:]
    frames.extend(seg)

# Титр: тёмная полоса снизу с названием игры, проявляется на финальном кадре.
last = frames[-1]
band_h = round(last.height * 0.2)
title_font = ImageFont.truetype(font_path, round(last.height * 0.085))
sub_font = ImageFont.truetype(font_path, round(last.height * 0.042))
steps = round(TITLE_HOLD * FPS)
for i in range(steps):
    k = min(1.0, (i + 1) / 8)
    im = last.copy().convert('RGBA')
    overlay = Image.new('RGBA', im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    d.rectangle([0, im.height - band_h, im.width, im.height], fill=(20, 14, 10, round(215 * k)))
    a = round(255 * k)
    d.text((im.width / 2, im.height - band_h * 0.62), 'Поварской срез', font=title_font, fill=(255, 205, 80, a), anchor='mm')
    d.text((im.width / 2, im.height - band_h * 0.22), 'FunFlow Game Jam × ТПУ · «Разруби!»', font=sub_font, fill=(255, 246, 227, a), anchor='mm')
    frames.append(Image.alpha_composite(im, overlay).convert('RGB'))

# Одна палитра на все кадры (по выборке кадров) и без дизеринга: тогда неизменные участки кадров совпадают
# и GIF хранит только то, что поменялось, — файл в разы меньше.
sample = frames[::max(1, len(frames) // 24)]
strip = Image.new('RGB', (WIDTH, sum(f.height for f in sample)))
y = 0
for f in sample:
    strip.paste(f, (0, y))
    y += f.height
palette = strip.quantize(colors=COLORS, method=Image.Quantize.MEDIANCUT)
palette_frames = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]
durations = [round(1000 / FPS)] * len(palette_frames)
durations[-1] = 1500  # пауза перед повтором
palette_frames[0].save(out_path, save_all=True, append_images=palette_frames[1:], duration=durations, loop=0, optimize=True)
print(f'{out_path}: {len(palette_frames)} кадров, {len(palette_frames) / FPS:.1f} с, {os.path.getsize(out_path) / 1e6:.1f} МБ')
