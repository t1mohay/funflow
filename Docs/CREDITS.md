# Сторонние ресурсы

| Что | Откуда | Лицензия |
|---|---|---|
| Картинки ингредиентов (`Assets/Resources/Ingredients`) | дизайнер команды | свои |
| Картинки блюд (`Assets/Resources/Dishes`) и запасные ингредиенты (`Reserve/ArtAlt/Noto3D`) | [Noto Emoji](https://github.com/googlefonts/noto-emoji), стиль 3D, PNG 512 px | Apache License 2.0 (текст: `Assets/Resources/NOTO_EMOJI_LICENSE.txt`) |
| Фон-кухня (`Assets/Resources/Kitchen`) | нарисован командой, исходник — `Tools/Background/background.jpg` | свой |
| Запасной плоский стиль (`Reserve/ArtAlt/Noto2D`) | [Noto Emoji](https://github.com/googlefonts/noto-emoji), стиль 2D | Apache License 2.0 |
| Нож на ползунках меню паузы (`Assets/Resources/UI/knife.png`) | нарисован скриптом `Tools/ArtGen/gen_knife.py` в стиле картинок ингредиентов | свой |
| Ветер для звука взмаха (`Assets/Resources/Audio/Swipe/swipe_loop.wav`) | запись «Whistling Wind; Steady, Light…» (`whistlingwindstead54.mp3`), обработка — `Tools/AudioGen/gen_04_swipe_wind.py` | **уточнить источник и лицензию** |
| Текучий фон меню (`Assets/Resources/Shaders/MenuSwirl.shader`) | свой шейдер в духе меню Balatro (код не заимствован), превью — `Tools/ArtGen/menu_swirl_preview.py` | свой |
| Шрифт интерфейса (`Assets/Resources/Fonts/Main.ttf`) | [Caveat](https://fonts.google.com/specimen/Caveat), начертание Bold (700), из переменного шрифта | SIL Open Font License 1.1 (текст: `Assets/Resources/Fonts/CAVEAT_OFL.txt`) |
| Звуки и музыка (`Assets/Resources/Audio`) | синтезированы скриптами `Tools/AudioGen` (без сэмплов); если файла нет, `Sfx.cs` генерирует заглушку | свои |

Соответствие эмодзи и ингредиентов:
- 🍅 помидор, 🥒 огурец, 🥬 салат, 🧅 лук, 🫑 перец;
- 🧀 сыр, 🍄‍🟫 грибы, 🥓 бекон, 🫒 оливки;
- 🦐 креветка, 🌶️ чили, 🍋‍🟩 лайм, 🥥 кокос, 🌿 лемонграсс, 🫚 галангал;
- 🍫 🍩 🍌 🍬 🐟 🧦 🍍 🍓 🍦 🍪 🥔 🥕 🍞 🥛 🍎 — лишние;
- 🥗 🍕 🍲 — блюда.
