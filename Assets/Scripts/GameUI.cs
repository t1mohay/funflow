using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace RecipeSlicer
{
    /// <summary>Весь интерфейс строится кодом: меню, панели рецепта, таймер, всплывающий текст, результат.</summary>
    public sealed class GameUI : MonoBehaviour
    {
        /// <summary>Доля ширины экрана под каждую боковую панель; спавнер не залезает под них.</summary>
        public const float SidePanelFraction = 0.17f;

        public static readonly Color TextColor = new Color(1f, 0.96f, 0.9f);
        public static readonly Color GoodColor = new Color(0.56f, 0.93f, 0.45f);
        public static readonly Color BadColor = new Color(1f, 0.4f, 0.35f);
        public static readonly Color AccentColor = new Color(1f, 0.8f, 0.3f);

        static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color RowColor = new Color(1f, 1f, 1f, 0.07f);
        static readonly Color RowDoneColor = new Color(0.4f, 0.85f, 0.3f, 0.3f);
        static readonly Color RowWrongColor = new Color(1f, 0.3f, 0.25f, 0.12f);
        static readonly Color CardColor = new Color(0.27f, 0.18f, 0.13f, 0.96f);
        static readonly Color ButtonColor = new Color(0.36f, 0.62f, 0.27f, 1f);
        static readonly Color SecondaryButtonColor = new Color(0.4f, 0.3f, 0.24f, 1f);

        const float RowHeight = 76f;
        const float RowGap = 8f;
        const float FloaterLife = 0.9f;
        const float CountdownLife = 0.65f;

        sealed class Row
        {
            public IngredientDef Def;
            public int Need;
            public bool Wrong;
            public RectTransform Rect;
            public Image Background;
            public Text Count;
            public float Punch;
        }

        sealed class Floater
        {
            public RectTransform Rect;
            public Text Text;
            public Vector2 Start;
            public Color Color;
            public float Age;
        }

        GameManager _game;
        Camera _cam;
        Font _font;
        RectTransform _root;

        GameObject _menu, _hud, _results, _pause;
        Image _brightness;
        GameObject _pauseButton;
        GameObject _pauseGameButtons, _settingsBack;
        Text _pauseTitle;
        bool _settingsMode;
        Text _resolutionLabel, _windowModeLabel;
        readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
        int _resolutionIndex;
        RectTransform _neededList, _wrongList, _floatLayer, _timerRect;
        GameObject _timerPlate;
        Text _timer, _score, _countdown, _banner;
        Image _flash;
        Color _bannerColor;
        float _bannerAge = 99f, _bannerDuration, _countdownAge = 99f;
        readonly List<Row> _rows = new List<Row>();
        readonly List<Floater> _floaters = new List<Floater>();
        readonly List<Text> _bestLabels = new List<Text>();
        Text _resTitle, _resScore, _resStars, _resDetails, _resVerdict, _resBest;
        Image _resIcon;

        public void Init(GameManager game, Camera cam)
        {
            _game = game;
            _cam = cam;
            _font = Art.UIFont;
            EnsureEventSystem();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)canvasGo.transform;

            // Порядок создания = порядок отрисовки.
            BuildHud();
            _flash = AddImage(Fill(NewRect("Flash", _root)), Color.clear, null);
            _floatLayer = Fill(NewRect("Floaters", _root));
            _banner = AddText(Place(NewRect("Banner", _root), new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(1300f, 120f)),
                "", 54, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            _countdown = AddText(Place(NewRect("Countdown", _root), new Vector2(0.5f, 0.48f), Vector2.zero, new Vector2(900f, 320f)),
                "", 200, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            BuildMenu();
            BuildResults();
            BuildPause();
            BuildBrightnessOverlay();
        }

        // ───────────────────────── Экраны ─────────────────────────

        /// <param name="dishSelect">Сразу открыть выбор блюда (после раунда), иначе — главное меню.</param>
        public void ShowMenu(bool dishSelect = false)
        {
            _menu.SetActive(true);
            _hud.SetActive(false);
            _results.SetActive(false);
            _banner.text = "";
            _countdown.text = "";
            _creditsScreen.SetActive(false);
            ShowDishSelect(dishSelect);

            var all = Recipes.All;
            for (int i = 0; i < all.Length; i++)
            {
                int best = _game.BestScore(all[i]);
                _bestLabels[i].text = best >= 0 ? $"Рекорд: {best}/100" : "Ещё не готовили";
            }
        }

        public void ShowPause(bool visible)
        {
            if (_pause == null) return;
            _settingsMode = false;
            _pauseTitle.text = "ПАУЗА";
            _pauseGameButtons.SetActive(true);
            _settingsBack.SetActive(false);
            if (visible) RefreshGraphicsLabels();
            _pause.SetActive(visible);
        }

        public void ShowRound(RoundState round)
        {
            _menu.SetActive(false);
            _results.SetActive(false);
            _hud.SetActive(true);
            _timerRect.gameObject.SetActive(true);
            _timerPlate.SetActive(true);
            _score.gameObject.SetActive(true);
            _pauseButton.SetActive(true);
            BuildRows(round.Recipe);
            RefreshRows(round);
            UpdateTimer(round);
        }

        public void ShowResults(RoundState round, int best, bool newBest)
        {
            _results.SetActive(true);
            _timerRect.gameObject.SetActive(false);
            _timerPlate.SetActive(false);
            _score.gameObject.SetActive(false);
            _pauseButton.SetActive(false);

            var dish = Art.DishSprite(round.Recipe);
            _resIcon.sprite = dish;
            _resIcon.enabled = dish != null;

            if (round.IsComplete) ShowWin(round, best, newBest);
            else ShowLoss(round, best);
        }

        void ShowWin(RoundState round, int best, bool newBest)
        {
            int stars = round.Stars;
            var scoreColor = round.Score > 100 ? AccentColor : stars >= 2 ? GoodColor : stars == 1 ? AccentColor : BadColor;

            _resTitle.text = round.Recipe.ReadyText;
            _resScore.text = $"{round.Score}/100";
            _resScore.fontSize = 150;
            _resScore.color = scoreColor;
            _resStars.text = new string('★', stars) + new string('☆', 3 - stars);

            var sb = new StringBuilder();
            sb.Append($"Весь рецепт собран за {Mathf.CeilToInt(round.Elapsed)} с!\n");
            if (round.WrongCount > 0)
                sb.Append($"Лишнее: {WrongList(round)}  (−{round.WrongCount * RoundState.WrongPenalty})\n");
            else
                sb.Append("Ничего лишнего — чистая работа!\n");
            if (round.OverflowCount > 0)
                sb.Append($"Перебор: {round.OverflowCount}  (−{round.OverflowCount * RoundState.OverflowPenalty})\n");
            if (round.ComboCount > 0)
                sb.Append($"Комбо: {round.ComboCount}  (+{round.ComboBonus})\n");
            _resDetails.text = sb.ToString();
            _resDetails.fontSize = 32;

            _resVerdict.text = Verdict(round);
            _resVerdict.color = scoreColor;
            _resBest.text = newBest ? "Новый рекорд!" : $"Рекорд: {best}/100";
            _resBest.color = newBest ? AccentColor : new Color(1f, 1f, 1f, 0.6f);
        }

        void ShowLoss(RoundState round, int best)
        {
            _resTitle.text = round.Recipe.FailText;
            _resScore.text = "Не успели!";
            _resScore.fontSize = 110;
            _resScore.color = BadColor;
            _resStars.text = "";

            var sb = new StringBuilder();
            sb.Append($"Собрано по рецепту: {round.TotalCollected}/{round.Recipe.TotalNeeded}\n");
            sb.Append("Не хватило: ");
            bool first = true;
            foreach (var item in round.Recipe.Needed)
            {
                int missing = item.Count - round.Collected(item.Ingredient);
                if (missing <= 0) continue;
                if (!first) sb.Append(", ");
                sb.Append($"{item.Ingredient.Name} ×{missing}");
                first = false;
            }
            sb.Append('\n');
            if (round.WrongCount > 0) sb.Append($"Лишнее: {WrongList(round)}\n");
            _resDetails.text = sb.ToString();
            _resDetails.fontSize = 28;

            _resVerdict.text = "Шеф не дождался... Собери весь рецепт за минуту!";
            _resVerdict.color = AccentColor;
            _resBest.text = best >= 0 ? $"Рекорд: {best}/100" : "";
            _resBest.color = new Color(1f, 1f, 1f, 0.6f);
        }

        static string WrongList(RoundState round)
        {
            var sb = new StringBuilder();
            foreach (var pair in round.WrongByType)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append($"{pair.Key.Name} ×{pair.Value}");
            }
            return sb.ToString();
        }

        static string Verdict(RoundState round)
        {
            int score = round.Score;
            if (score > 100) return "Божественное блюдо! Шеф плачет от счастья.";
            if (score == 100) return "Идеально! Ни одной ошибки.";
            var worst = round.WorstWrong();
            if (worst != null && score < 70) return $"{worst.Name} в блюде «{round.Recipe.Title}»?! Шеф в шоке.";
            if (score >= 90) return "Восхитительно! Можно подавать гостям.";
            if (score >= 70) return "Вкусно! Почти как у бабушки.";
            if (score >= 40) return "Съедобно... если не принюхиваться.";
            return "Это есть нельзя. Даже коту.";
        }

        // ───────────────────────── Во время раунда ─────────────────────────

        public void UpdateTimer(RoundState round)
        {
            int seconds = Mathf.CeilToInt(round.TimeLeft);
            _timer.text = $"{seconds / 60}:{seconds % 60:00}";
            bool hurry = round.TimeLeft > 0f && round.TimeLeft <= 10f;
            _timer.color = hurry ? BadColor : TextColor;
            float pulse = hurry ? 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.time * 6f)) : 1f;
            _timerRect.localScale = new Vector3(pulse, pulse, 1f);
        }

        public void OnSliced(IngredientDef def, RoundState round)
        {
            RefreshRows(round);
            foreach (var row in _rows)
                if (row.Def == def) row.Punch = 1f;
        }

        public void SetCountdown(string text)
        {
            _countdown.text = text;
            _countdownAge = 0f;
        }

        public void ShowBanner(string text, Color color, float duration)
        {
            _banner.text = text;
            _bannerColor = color;
            _bannerAge = 0f;
            _bannerDuration = duration;
        }

        public void Flash(Color color)
        {
            _flash.color = new Color(color.r, color.g, color.b, 0.3f);
        }

        public void Floating(Vector3 worldPos, string text, Color color, int size = 42)
        {
            Vector2 screen = _cam.WorldToScreenPoint(worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_floatLayer, screen, null, out var local);
            var rt = Place(NewRect("Floater", _floatLayer), new Vector2(0.5f, 0.5f), local, new Vector2(600f, 80f));
            var label = AddText(rt, text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _floaters.Add(new Floater { Rect = rt, Text = label, Start = local, Color = color });
        }

        void RefreshRows(RoundState round)
        {
            foreach (var row in _rows)
            {
                if (row.Wrong) continue;
                int got = round.Collected(row.Def);
                bool done = got >= row.Need;
                row.Count.text = done ? "✓" : $"{got}/{row.Need}";
                row.Count.color = done ? GoodColor : TextColor;
                row.Background.color = done ? RowDoneColor : RowColor;
            }
            _score.text = $"Блюдо: {round.Score}/100";
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            LayoutCovers();

            foreach (var row in _rows)
            {
                if (row.Punch <= 0f) continue;
                row.Punch = Mathf.Max(0f, row.Punch - dt * 4f);
                float s = 1f + 0.15f * row.Punch;
                row.Rect.localScale = new Vector3(s, s, 1f);
            }

            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                var f = _floaters[i];
                f.Age += dt;
                float k = f.Age / FloaterLife;
                if (k >= 1f)
                {
                    Destroy(f.Rect.gameObject);
                    _floaters.RemoveAt(i);
                    continue;
                }
                f.Rect.anchoredPosition = f.Start + new Vector2(0f, 110f * (1f - (1f - k) * (1f - k)));
                float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 1f, (k - 0.15f) / 0.85f);
                f.Rect.localScale = new Vector3(pop, pop, 1f);
                var c = f.Color;
                c.a = 1f - k * k;
                f.Text.color = c;
            }

            if (_flash.color.a > 0f)
            {
                var c = _flash.color;
                c.a = Mathf.Max(0f, c.a - dt * 1.5f);
                _flash.color = c;
            }

            _countdownAge += dt;
            AnimatePop(_countdown, TextColor, _countdownAge, CountdownLife);

            _bannerAge += dt;
            AnimatePop(_banner, _bannerColor, _bannerAge, _bannerDuration);
        }

        static void AnimatePop(Text text, Color color, float age, float life)
        {
            if (age > life)
            {
                if (text.text.Length > 0) text.text = "";
                return;
            }
            float scale = age < 0.15f ? Mathf.Lerp(1.5f, 1f, age / 0.15f) : 1f;
            text.rectTransform.localScale = new Vector3(scale, scale, 1f);
            float fadeStart = Mathf.Max(life - 0.3f, life * 0.6f);
            color.a = age < fadeStart ? 1f : 1f - (age - fadeStart) / (life - fadeStart);
            text.color = color;
        }

        // ───────────────────────── Построение ─────────────────────────

        void BuildHud()
        {
            var hud = Fill(NewRect("HUD", _root));
            _hud = hud.gameObject;

            _neededList = BuildSidePanel(hud, "Нужно", "НУЖНО", GoodColor, 0f, SidePanelFraction);
            _wrongList = BuildSidePanel(hud, "НеНужно", "НЕ НУЖНО", BadColor, 1f - SidePanelFraction, 1f);

            // Тёмная плашка под таймером и счётом — чтобы читались на светлом фоне.
            _timerPlate = Place(NewRect("TimerPlate", hud), new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(460f, 176f)).gameObject;
            AddImage((RectTransform)_timerPlate.transform, PanelColor, Art.RoundedRect);
            _timerRect = Place(NewRect("Timer", hud), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(400f, 110f));
            _timer = AddText(_timerRect, "1:00", 84, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            _score = AddText(Place(NewRect("Score", hud), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(600f, 60f)),
                "", 36, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            // Кнопка паузы справа от таймера (то же, что Esc).
            var pause = Place(NewRect("PauseButton", hud), new Vector2(0.5f, 1f), new Vector2(290f, -70f), new Vector2(96f, 96f));
            MakeButton(pause, SecondaryButtonColor, _game.Pause);
            _pauseButton = pause.gameObject;
            AddText(Fill(NewRect("Label", pause)), "II", 46, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        RectTransform BuildSidePanel(RectTransform parent, string name, string title, Color titleColor, float xMin, float xMax)
        {
            var panel = Stretch(NewRect(name, parent), new Vector2(xMin, 0f), new Vector2(xMax, 1f),
                new Vector2(xMin > 0f ? 8f : 16f, 16f), new Vector2(xMin > 0f ? -16f : -8f, -16f));
            AddImage(panel, PanelColor, Art.RoundedRect);
            AddText(Stretch(NewRect("Title", panel), new Vector2(0f, 1f), Vector2.one, new Vector2(12f, -80f), new Vector2(-12f, -14f)),
                title, 40, titleColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            return Stretch(NewRect("List", panel), Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -92f));
        }

        void BuildRows(RecipeDef recipe)
        {
            foreach (var row in _rows) Destroy(row.Rect.gameObject);
            _rows.Clear();
            for (int i = 0; i < recipe.Needed.Length; i++)
                _rows.Add(MakeRow(_neededList, i, recipe.Needed[i].Ingredient, recipe.Needed[i].Count, false));
            for (int i = 0; i < recipe.Wrong.Length; i++)
                _rows.Add(MakeRow(_wrongList, i, recipe.Wrong[i], 0, true));
        }

        Row MakeRow(RectTransform list, int index, IngredientDef def, int need, bool wrong)
        {
            var rt = NewRect(def.Name, list);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0f, RowHeight);
            rt.anchoredPosition = new Vector2(0f, -index * (RowHeight + RowGap) - RowHeight * 0.5f);
            var background = AddImage(rt, wrong ? RowWrongColor : RowColor, Art.RoundedRect);

            var icon = Place(NewRect("Icon", rt), new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(60f, 60f));
            SetIcon(AddImage(icon, Color.white, null), def);

            var name = AddText(Stretch(NewRect("Name", rt), Vector2.zero, Vector2.one, new Vector2(82f, 0f), new Vector2(wrong ? -10f : -78f, 0f)),
                def.Name, 28, TextColor, TextAnchor.MiddleLeft, FontStyle.Normal);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 16;
            name.resizeTextMaxSize = 28;
            name.verticalOverflow = VerticalWrapMode.Truncate;

            // У лишних только иконка и название — без счётчика.
            Text count = null;
            if (!wrong)
            {
                count = AddText(Stretch(NewRect("Count", rt), new Vector2(1f, 0f), Vector2.one, new Vector2(-84f, 0f), new Vector2(-10f, 0f)),
                    $"0/{need}", 34, TextColor, TextAnchor.MiddleRight, FontStyle.Bold);
            }

            return new Row { Def = def, Need = need, Wrong = wrong, Rect = rt, Background = background, Count = count };
        }

        // ───────────────────────── Меню (картинки дизайнера + невидимые кнопки) ─────────────────────────

        /// <summary>Затемнение картинок меню — как у фона игры.</summary>
        static readonly Color MenuTint = new Color(0.6f, 0.6f, 0.64f);

        // Кнопки в долях картинки 1280×853: (x0, y0, x1, y1), y — сверху вниз.
        static readonly Rect StartButton = PixelRect(437, 193, 845, 290);
        static readonly Rect SettingsButton = PixelRect(437, 310, 845, 405);
        static readonly Rect CreditsButton = PixelRect(437, 425, 845, 518);
        static readonly Rect BackButton = PixelRect(55, 475, 207, 537);
        // Карточки блюд слева направо — в том же порядке, что Recipes.All (салат, пицца, том ям).
        static readonly Rect[] DishCards = { PixelRect(248, 265, 500, 555), PixelRect(512, 265, 770, 555), PixelRect(782, 265, 1028, 555) };

        /// <summary>Команда для экрана «Разработчики».</summary>
        static readonly string[] Credits =
        {
            "Игра для FunFlow Game Jam × ТПУ",
            "",
            "Геймдизайн и код — команда «Режь по рецепту!»",
            "Иллюстрации ингредиентов — дизайнер команды",
            "Звук и музыка — звукорежиссёр команды",
            "",
            "Unity 6.6  •  картинки блюд: Noto Emoji (Apache 2.0)",
        };

        static Rect PixelRect(float x0, float y0, float x1, float y1)
        {
            const float w = 1280f, h = 853f;
            return Rect.MinMaxRect(x0 / w, 1f - y1 / h, x1 / w, 1f - y0 / h);
        }

        GameObject _mainScreen, _dishScreen, _creditsScreen;
        readonly List<RectTransform> _covers = new List<RectTransform>();
        readonly List<float> _coverAspects = new List<float>();

        /// <summary>Открыт выбор блюда (работают клавиши 1/2/3).</summary>
        public bool DishSelectOpen => _menu.activeSelf && _dishScreen.activeSelf;

        void BuildMenu()
        {
            var menu = Fill(NewRect("Menu", _root));
            _menu = menu.gameObject;
            AddImage(menu, Color.black, null).raycastTarget = true;

            // Главное меню
            var main = BuildScreen(menu, "Main", "Menu/main");
            _mainScreen = main.parent.gameObject;
            HotSpot(main, StartButton, () => ShowDishSelect(true));
            HotSpot(main, SettingsButton, ShowSettings);
            HotSpot(main, CreditsButton, () => ShowCredits(true));

            // Выбор блюда
            var dishes = BuildScreen(menu, "Dishes", "Menu/dishes");
            _dishScreen = dishes.parent.gameObject;
            var all = Recipes.All;
            for (int i = 0; i < all.Length && i < DishCards.Length; i++)
            {
                var recipe = all[i];
                HotSpot(dishes, DishCards[i], () => _game.StartRound(recipe));
                var card = DishCards[i];
                var best = Stretch(NewRect("Best", dishes), new Vector2(card.xMin, card.yMin - 0.07f), new Vector2(card.xMax, card.yMin - 0.01f),
                    Vector2.zero, Vector2.zero);
                _bestLabels.Add(AddText(best, "", 30, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold));
            }
            HotSpot(dishes, BackButton, () => ShowDishSelect(false));
            // Подсказка привязана к экрану, а не к картинке: низ картинки на широких экранах обрезается.
            AddText(Stretch(NewRect("Help", dishes.parent), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.12f), Vector2.zero, Vector2.zero),
                "Зажми левую кнопку мыши и проведи по ингредиенту   •   1 / 2 / 3 — выбор блюда   •   Esc — пауза",
                30, TextColor, TextAnchor.MiddleCenter, FontStyle.Normal);

            BuildCredits(menu);
        }

        /// <summary>Экран-картинка, которая заполняет экран без искажений и прижата к верху. Возвращает её прямоугольник.</summary>
        RectTransform BuildScreen(RectTransform parent, string name, string spritePath)
        {
            var screen = Fill(NewRect(name, parent));
            var image = Place(NewRect("Image", screen), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1920f, 1280f));
            image.pivot = new Vector2(0.5f, 1f);
            var sprite = Art.LoadSprite(spritePath);
            var img = AddImage(image, MenuTint, null);
            img.sprite = sprite;
            if (sprite == null) img.color = CardColor;
            _covers.Add(image);
            _coverAspects.Add(sprite != null ? sprite.rect.width / sprite.rect.height : 1280f / 853f);
            return image;
        }

        /// <summary>Невидимая кнопка поверх нарисованной: подсвечивается при наведении и темнеет при нажатии.</summary>
        void HotSpot(RectTransform image, Rect area, UnityAction onClick)
        {
            var rt = Stretch(NewRect("HotSpot", image), area.min, area.max, Vector2.zero, Vector2.zero);
            var overlay = AddImage(rt, Color.white, Art.RoundedRect);
            overlay.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = overlay;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
            colors.pressedColor = new Color(0f, 0f, 0f, 0.25f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => _game.Sfx.PlayClick());
            button.onClick.AddListener(onClick);
        }

        void BuildCredits(RectTransform parent)
        {
            var credits = Fill(NewRect("Credits", parent));
            _creditsScreen = credits.gameObject;
            AddImage(credits, new Color(0f, 0f, 0f, 0.6f), null).raycastTarget = true;
            var panel = Place(NewRect("Panel", credits), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 700f));
            AddImage(panel, CardColor, Art.RoundedRect);
            AddText(Top(panel, 30f, 90f), "РАЗРАБОТЧИКИ", 64, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddText(Top(panel, 140f, 400f), string.Join("\n", Credits), 34, TextColor, TextAnchor.UpperCenter, FontStyle.Normal);
            var back = Place(NewRect("Back", panel), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(360f, 96f));
            MakeButton(back, SecondaryButtonColor, () => ShowCredits(false));
            AddText(Fill(NewRect("Label", back)), "Назад  [Esc]", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _creditsScreen.SetActive(false);
        }

        void ShowDishSelect(bool visible)
        {
            _dishScreen.SetActive(visible);
            _mainScreen.SetActive(!visible);
        }

        void ShowCredits(bool visible) => _creditsScreen.SetActive(visible);

        /// <summary>Настройки из главного меню — та же панель, что в паузе, без игровых кнопок.</summary>
        void ShowSettings()
        {
            _settingsMode = true;
            _pauseTitle.text = "НАСТРОЙКИ";
            _pauseGameButtons.SetActive(false);
            _settingsBack.SetActive(true);
            RefreshGraphicsLabels();
            _pause.SetActive(true);
        }

        void HideSettings()
        {
            _settingsMode = false;
            _pause.SetActive(false);
        }

        /// <summary>Esc в меню: закрыть верхний экран. Возвращает true, если что-то закрылось.</summary>
        public bool MenuBack()
        {
            if (_settingsMode) { HideSettings(); return true; }
            if (_creditsScreen.activeSelf) { ShowCredits(false); return true; }
            if (_dishScreen.activeSelf) { ShowDishSelect(false); return true; }
            return false;
        }

        /// <summary>Картинки меню: покрыть экран с сохранением пропорций, прижать к верху.</summary>
        void LayoutCovers()
        {
            // Те же масштаб (+4%) и сдвиг вверх (1% высоты), что у фона игры в GameManager,
            // чтобы при переходе меню → игра стена, полки и посуда не сдвигались.
            var size = _root.rect.size;
            for (int i = 0; i < _covers.Count; i++)
            {
                float aspect = _coverAspects[i];
                float w = Mathf.Max(size.x, size.y * aspect) * 1.04f;
                _covers[i].sizeDelta = new Vector2(w, w / aspect);
                _covers[i].anchoredPosition = new Vector2(0f, size.y * 0.01f);
            }
        }

        void BuildResults()
        {
            var results = Fill(NewRect("Results", _root));
            _results = results.gameObject;
            AddImage(results, new Color(0f, 0f, 0f, 0.55f), null);

            var panel = Place(NewRect("Panel", results), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 830f));
            AddImage(panel, CardColor, Art.RoundedRect);

            _resTitle = AddText(Top(panel, 30f, 90f), "", 64, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            _resIcon = AddImage(Place(NewRect("Dish", panel), new Vector2(0.5f, 1f), new Vector2(-380f, -150f), new Vector2(150f, 150f)), Color.white, null);
            _resIcon.preserveAspect = true;
            _resScore = AddText(Top(panel, 120f, 170f), "", 150, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            _resStars = AddText(Top(panel, 290f, 90f), "", 80, AccentColor, TextAnchor.MiddleCenter, FontStyle.Normal);
            _resDetails = AddText(Top(panel, 395f, 170f), "", 32, TextColor, TextAnchor.UpperCenter, FontStyle.Normal);
            _resVerdict = AddText(Top(panel, 570f, 60f), "", 36, GoodColor, TextAnchor.MiddleCenter, FontStyle.Italic);
            _resBest = AddText(Top(panel, 630f, 44f), "", 30, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            var again = Place(NewRect("Again", panel), new Vector2(0.5f, 0f), new Vector2(-220f, 80f), new Vector2(400f, 96f));
            MakeButton(again, ButtonColor, _game.Restart);
            AddText(Fill(NewRect("Label", again)), "Ещё раз  [R]", 36, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            var back = Place(NewRect("Back", panel), new Vector2(0.5f, 0f), new Vector2(220f, 80f), new Vector2(400f, 96f));
            MakeButton(back, SecondaryButtonColor, _game.ToDishSelect);
            AddText(Fill(NewRect("Label", back)), "Другое блюдо  [Esc]", 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        // ───────────────────────── Меню паузы ─────────────────────────

        void BuildPause()
        {
            var pause = Fill(NewRect("Pause", _root));
            _pause = pause.gameObject;
            // Затемнение ловит клики, чтобы под паузой ничего не нажималось.
            AddImage(pause, new Color(0f, 0f, 0f, 0.6f), null).raycastTarget = true;

            var panel = Place(NewRect("Panel", pause), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 940f));
            AddImage(panel, CardColor, Art.RoundedRect);

            _pauseTitle = AddText(Top(panel, 24f, 90f), "ПАУЗА", 72, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            AddText(Top(panel, 124f, 50f), "Звук", 40, GoodColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            SliderRow(panel, 182f, "Общая громкость", 0f, 1f, GameSettings.Master, GameSettings.SetMaster, Percent);
            SliderRow(panel, 252f, "Музыка", 0f, 1f, GameSettings.Music, GameSettings.SetMusic, Percent);
            SliderRow(panel, 322f, "Эффекты", 0f, 1f, GameSettings.Effects, GameSettings.SetEffects, Percent);

            AddText(Top(panel, 410f, 50f), "Графика", 40, GoodColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            CollectResolutions();
            _resolutionLabel = CyclerRow(panel, 468f, "Разрешение", () => ChangeResolution(-1), () => ChangeResolution(1));
            _windowModeLabel = CyclerRow(panel, 538f, "Режим", ToggleWindowMode, ToggleWindowMode);
            SliderRow(panel, 608f, "Яркость", GameSettings.MinBrightness, GameSettings.MaxBrightness, GameSettings.Brightness,
                v =>
                {
                    GameSettings.SetBrightness(v);
                    ApplyBrightness();
                }, Percent);

            const float buttonY = 78f;
            // Игровые кнопки (в паузе) и «Назад» (в настройках из главного меню).
            var gameButtons = Fill(NewRect("GameButtons", panel));
            _pauseGameButtons = gameButtons.gameObject;
            var settingsBack = Place(NewRect("SettingsBack", panel), new Vector2(0.5f, 0f), new Vector2(0f, buttonY), new Vector2(360f, 96f));
            MakeButton(settingsBack, ButtonColor, HideSettings);
            AddText(Fill(NewRect("Label", settingsBack)), "Назад  [Esc]", 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _settingsBack = settingsBack.gameObject;
            _settingsBack.SetActive(false);
            panel = gameButtons;

            var resume = Place(NewRect("Resume", panel), new Vector2(0.5f, 0f), new Vector2(-310f, buttonY), new Vector2(290f, 96f));
            MakeButton(resume, ButtonColor, _game.Resume);
            AddText(Fill(NewRect("Label", resume)), "Продолжить  [Esc]", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            var restart = Place(NewRect("Restart", panel), new Vector2(0.5f, 0f), new Vector2(0f, buttonY), new Vector2(290f, 96f));
            MakeButton(restart, SecondaryButtonColor, _game.Restart);
            AddText(Fill(NewRect("Label", restart)), "Заново  [R]", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            var menu = Place(NewRect("ToMenu", panel), new Vector2(0.5f, 0f), new Vector2(310f, buttonY), new Vector2(290f, 96f));
            MakeButton(menu, SecondaryButtonColor, _game.BackToMenu);
            AddText(Fill(NewRect("Label", menu)), "В главное меню", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            _pause.SetActive(false);
        }

        static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        /// <summary>Строка «подпись — ползунок — значение».</summary>
        void SliderRow(RectTransform panel, float top, string title, float min, float max, float value,
            System.Action<float> onChange, System.Func<float, string> format)
        {
            var row = Top(panel, top, 56f);
            AddText(Stretch(NewRect("Label", row), Vector2.zero, new Vector2(0.38f, 1f), Vector2.zero, Vector2.zero),
                title, 32, TextColor, TextAnchor.MiddleLeft, FontStyle.Normal);
            var valueText = AddText(Stretch(NewRect("Value", row), new Vector2(0.86f, 0f), Vector2.one, Vector2.zero, Vector2.zero),
                format(value), 30, AccentColor, TextAnchor.MiddleRight, FontStyle.Bold);

            var area = Stretch(NewRect("Slider", row), new Vector2(0.4f, 0.2f), new Vector2(0.84f, 0.8f), Vector2.zero, Vector2.zero);
            var background = AddImage(Fill(NewRect("Background", area)), new Color(0f, 0f, 0f, 0.45f), Art.RoundedRect);
            background.raycastTarget = true;

            var fillArea = Stretch(NewRect("FillArea", area), Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            // Slider двигает только горизонтальные якоря — по высоте заливка растянута сама.
            var fill = Fill(NewRect("Fill", fillArea));
            AddImage(fill, AccentColor, Art.RoundedRect);

            var handleArea = Stretch(NewRect("HandleArea", area), Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            var handle = NewRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(38f, 38f);
            var handleImage = AddImage(handle, Color.white, Art.Circle);
            handleImage.type = Image.Type.Simple;
            handleImage.raycastTarget = true;

            var slider = area.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v =>
            {
                onChange(v);
                valueText.text = format(v);
            });
        }

        /// <summary>Строка «подпись — [&lt;] значение [&gt;]». Возвращает текст значения.</summary>
        Text CyclerRow(RectTransform panel, float top, string title, UnityAction onPrev, UnityAction onNext)
        {
            var row = Top(panel, top, 56f);
            AddText(Stretch(NewRect("Label", row), Vector2.zero, new Vector2(0.38f, 1f), Vector2.zero, Vector2.zero),
                title, 32, TextColor, TextAnchor.MiddleLeft, FontStyle.Normal);

            var prev = Stretch(NewRect("Prev", row), new Vector2(0.4f, 0f), new Vector2(0.47f, 1f), Vector2.zero, Vector2.zero);
            MakeButton(prev, SecondaryButtonColor, onPrev);
            AddText(Fill(NewRect("Label", prev)), "<", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            var next = Stretch(NewRect("Next", row), new Vector2(0.93f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            MakeButton(next, SecondaryButtonColor, onNext);
            AddText(Fill(NewRect("Label", next)), ">", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            return AddText(Stretch(NewRect("Value", row), new Vector2(0.47f, 0f), new Vector2(0.93f, 1f), Vector2.zero, Vector2.zero),
                "", 32, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        // ───────────────────────── Графика ─────────────────────────

        void CollectResolutions()
        {
            _resolutions.Clear();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!_resolutions.Contains(size)) _resolutions.Add(size);
            }
            _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            if (_resolutions.Count == 0) _resolutions.Add(new Vector2Int(Screen.width, Screen.height));
            _resolutionIndex = FindCurrentResolution();
        }

        int FindCurrentResolution()
        {
            int index = _resolutions.IndexOf(new Vector2Int(Screen.width, Screen.height));
            return index >= 0 ? index : _resolutions.Count - 1;
        }

        void ChangeResolution(int delta)
        {
            _resolutionIndex = Mathf.Clamp(_resolutionIndex + delta, 0, _resolutions.Count - 1);
            var size = _resolutions[_resolutionIndex];
            Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
            _game.Sfx.PlayClick();
            RefreshGraphicsLabels();
        }

        void ToggleWindowMode()
        {
            bool windowed = Screen.fullScreenMode == FullScreenMode.Windowed;
            var mode = windowed ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            var size = _resolutions[_resolutionIndex];
            Screen.SetResolution(size.x, size.y, mode);
            _game.Sfx.PlayClick();
            RefreshGraphicsLabels(mode);
        }

        void RefreshGraphicsLabels() => RefreshGraphicsLabels(Screen.fullScreenMode);

        void RefreshGraphicsLabels(FullScreenMode mode)
        {
            if (_resolutionLabel == null) return;
            var size = _resolutions[_resolutionIndex];
            _resolutionLabel.text = $"{size.x} x {size.y}";
            _windowModeLabel.text = mode == FullScreenMode.Windowed ? "В окне" : "Полный экран";
        }

        /// <summary>Яркость — полупрозрачный слой поверх всего (и игры, и интерфейса).</summary>
        void BuildBrightnessOverlay()
        {
            var go = new GameObject("BrightnessCanvas", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            _brightness = AddImage(Fill(NewRect("Brightness", go.transform)), Color.clear, null);
            ApplyBrightness();
        }

        void ApplyBrightness()
        {
            float b = GameSettings.Brightness;
            // < 1 — затемняем чёрным, > 1 — осветляем белым (мягче, чтобы не выбеливать картинку).
            _brightness.color = b < 1f
                ? new Color(0f, 0f, 0f, (1f - b) * 1.2f)
                : new Color(1f, 1f, 1f, (b - 1f) * 0.35f);
        }

        // ───────────────────────── Хелперы ─────────────────────────

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static RectTransform Fill(RectTransform rt) => Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        static RectTransform Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        /// <summary>Полоса во всю ширину родителя, отступ сверху top, высота height.</summary>
        static RectTransform Top(RectTransform parent, float top, float height) =>
            Stretch(NewRect("Line", parent), new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -top - height), new Vector2(-24f, -top));

        static Image AddImage(RectTransform rt, Color color, Sprite sprite)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        static void SetIcon(Image image, IngredientDef def)
        {
            var sprite = Art.IngredientSprite(def);
            image.sprite = sprite != null ? sprite : Art.Circle;
            image.color = sprite != null ? Color.white : def.Color;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }

        Text AddText(RectTransform rt, string text, int size, Color color, TextAnchor anchor, FontStyle style)
        {
            var label = rt.gameObject.AddComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.fontStyle = style;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        void MakeButton(RectTransform rt, Color color, UnityAction onClick)
        {
            var image = AddImage(rt, color, Art.RoundedRect);
            image.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.9f, 0.7f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(onClick);
        }
    }
}
