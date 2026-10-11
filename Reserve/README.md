# Reserve — запасные картинки и звуки

Здесь лежат альтернативные и запасные ресурсы, которые игра сейчас не использует.
Папка вне `Assets`, поэтому Unity их не импортирует и в сборку они не попадают.

| Что | Откуда | Как вернуть в игру |
|---|---|---|
| `ArtAlt/Noto3D` — ингредиенты в объёмном стиле Noto Emoji | [Noto Emoji](https://github.com/googlefonts/noto-emoji), Apache License 2.0 | скопировать нужные PNG в `Assets/Resources/Ingredients` (имя файла = id ингредиента) |
| `ArtAlt/Noto2D` — ингредиенты в плоском стиле Noto Emoji | [Noto Emoji](https://github.com/googlefonts/noto-emoji), Apache License 2.0 | так же, в `Assets/Resources/Ingredients` |
| `Audio/Swipe/swipe_empty_01..03.wav` — отдельные взмахи по пустоте | `Tools/AudioGen/gen_04_swipe_empty.py` | скопировать в `Assets/Resources/Audio/Swipe`; играют, только если нет петли `swipe_loop.wav` |
