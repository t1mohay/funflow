# Генераторы звуков

Все звуки и музыка игры синтезированы этими скриптами (Python 3, numpy, scipy) — без сэмплов и чужих записей.
Стиль — ночной R&B / нео-соул: Rhodes, тёплые пэды, суб-бас, снэпы. Тональность ля минор, 86 BPM.

```bash
pip install numpy scipy
mkdir -p out/01_slice out/02_launch out/03_counter out/04_miss out/04_swipe_empty out/06_task_complete out/07_timer5 out/09_lobby out/10_countdown
python gen_01_slice_v3.py      # разрез еды: juicy / crunchy / soft / hard × 3
python gen_02_launch_v2.py     # вылет еды × 3
python gen_03_counter.py       # счётчик: 10 ступеней вверх
python gen_04_miss_v2.py       # промах × 3
python gen_04_swipe_empty.py   # взмах по пустоте × 3 (запасной вариант)
python gen_04_swipe_wind.py whistlingwindstead54.mp3   # взмах: петля ветра в ля миноре → Swipe/swipe_loop.wav
python gen_06_task.py          # выполнение задания: full / short
python gen_07_timer.py         # таймер 5 секунд
python gen_09_lobby.py         # музыка лобби (петля 16 тактов)
python gen_10_countdown.py     # отсчёт 3-2-1-старт
```

Готовые файлы раскладываются в `Assets/Resources/Audio/<Папка>/` — имена см. в комментарии в начале `Assets/Scripts/Sfx.cs`.
Музыку лобби перед копированием в игру конвертировать в OGG: `ffmpeg -i out/09_lobby/lobby_loop.wav -c:a libvorbis -q:a 6 lobby_loop.ogg`.
