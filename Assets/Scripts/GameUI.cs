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
        /// <summary>Высота шапки боковой панели (заголовок) и нижний отступ — как в BuildSidePanel.</summary>
        const float SidePanelTop = 92f;
        const float SidePanelBottom = 12f;
        const float DishBadgeSize = 120f;
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
        RectTransform _neededPanel, _wrongPanel;
        Image _dishBadge;
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
            _pauseTitle.text = "Пауза";
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
            bool hurry = round.TimeLeft > 0f && round.TimeLeft <= 5f;
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
            LayoutMenuBackground();

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

            _neededList = BuildSidePanel(hud, "Нужно", "НУЖНО", GoodColor, 0f, SidePanelFraction, DishBadgeSize * 0.8f);
            _wrongList = BuildSidePanel(hud, "НеНужно", "НЕ НУЖНО", BadColor, 1f - SidePanelFraction, 1f, 0f);
            _neededPanel = (RectTransform)_neededList.parent;
            _wrongPanel = (RectTransform)_wrongList.parent;

            // Картинка блюда — в правом верхнем углу панели «Нужно», над колонкой счётчиков.
            _dishBadge = AddImage(Place(NewRect("Dish", _neededPanel), Vector2.one, new Vector2(-DishBadgeSize * 0.4f, -8f),
                new Vector2(DishBadgeSize, DishBadgeSize)), Color.white, null);
            _dishBadge.preserveAspect = true;

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

        /// <param name="titleRight">Место справа от заголовка (под картинку блюда).</param>
        RectTransform BuildSidePanel(RectTransform parent, string name, string title, Color titleColor, float xMin, float xMax, float titleRight)
        {
            // По вертикали панель по центру экрана; высота задаётся в FitSidePanels по числу строк.
            var panel = Stretch(NewRect(name, parent), new Vector2(xMin, 0.5f), new Vector2(xMax, 0.5f),
                new Vector2(xMin > 0f ? 8f : 16f, -300f), new Vector2(xMin > 0f ? -16f : -8f, 300f));
            AddImage(panel, PanelColor, Art.RoundedRect);
            AddText(Stretch(NewRect("Title", panel), new Vector2(0f, 1f), Vector2.one, new Vector2(12f, -80f), new Vector2(-12f - titleRight, -14f)),
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
            FitSidePanels(Mathf.Max(recipe.Needed.Length, recipe.Wrong.Length));

            var dish = Art.DishSprite(recipe);
            _dishBadge.sprite = dish;
            _dishBadge.enabled = dish != null;
        }

        /// <summary>Обе боковые панели одной высоты (по самому длинному списку) и по центру экрана — выровнены друг с другом.</summary>
        void FitSidePanels(int rows)
        {
            float height = SidePanelTop + rows * (RowHeight + RowGap) - RowGap + SidePanelBottom;
            foreach (var panel in new[] { _neededPanel, _wrongPanel })
            {
                panel.offsetMin = new Vector2(panel.offsetMin.x, -height * 0.5f);
                panel.offsetMax = new Vector2(panel.offsetMax.x, height * 0.5f);
            }
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

        // ───────────────────────── Меню: фон игры + интерфейс в стиле паузы ─────────────────────────

        /// <summary>Затемнение фона меню — как у фона игры.</summary>
        static readonly Color MenuTint = new Color(0.6f, 0.6f, 0.64f);

        /// <summary>Команда для экрана «Разработчики».</summary>
        static readonly string[] Credits =
        {
            "Игра для FunFlow Game Jam × ТПУ",
            "",
            "Геймдизайн и код — Юрьев Тимофей, Клименко Станислав и Макурин Артём",
            "Иллюстрации — Лушников Дмитрий",
            "Звук и музыка — Миков Кирилл",
            "",
            "Unity 6.6",
        };

        GameObject _mainScreen, _dishScreen, _creditsScreen;
        RectTransform _menuBackground;
        float _menuBackgroundAspect = 1280f / 853f;

        /// <summary>Открыт выбор блюда (работают клавиши 1/2/3).</summary>
        public bool DishSelectOpen => _menu.activeSelf && _dishScreen.activeSelf;

        void BuildMenu()
        {
            var menu = Fill(NewRect("Menu", _root));
            _menu = menu.gameObject;
            AddImage(menu, Color.black, null).raycastTarget = true;

            // Тот же фон, что в игре, затемнённый; размер и положение — как у фона игры (LayoutMenuBackground).
            _menuBackground = Place(NewRect("Background", menu), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1920f, 1280f));
            _menuBackground.pivot = new Vector2(0.5f, 1f);
            var background = Art.LoadSprite("Kitchen/background");
            var bg = AddImage(_menuBackground, MenuTint, null);
            bg.sprite = background;
            if (background == null) bg.color = CardColor;
            else _menuBackgroundAspect = background.rect.width / background.rect.height;

            BuildMainScreen(menu);
            BuildDishScreen(menu);
            BuildCredits(menu);
        }

        void BuildMainScreen(RectTransform menu)
        {
            var main = Fill(NewRect("Main", menu));
            _mainScreen = main.gameObject;
            BigTitle(main, "Режь по рецепту!", new Vector2(0f, 330f));
            MenuButton(main, new Vector2(0f, 110f), "▶  Начать игру", true, () => ShowDishSelect(true));
            MenuButton(main, new Vector2(0f, -40f), "Настройки", false, ShowSettings);
            MenuButton(main, new Vector2(0f, -190f), "Разработчики", false, () => ShowCredits(true));
        }

        void BuildDishScreen(RectTransform menu)
        {
            var dishes = Fill(NewRect("Dishes", menu));
            _dishScreen = dishes.gameObject;
            BigTitle(dishes, "Выбери блюдо", new Vector2(0f, 380f));

            var all = Recipes.All;
            const float cardWidth = 400f, gap = 50f;
            for (int i = 0; i < all.Length; i++)
            {
                var recipe = all[i];
                float x = (i - (all.Length - 1) * 0.5f) * (cardWidth + gap);
                DishCard(dishes, recipe, new Vector2(x, 20f), new Vector2(cardWidth, 520f));
                var best = Place(NewRect("Best", dishes), new Vector2(0.5f, 0.5f), new Vector2(x, -290f), new Vector2(cardWidth, 50f));
                _bestLabels.Add(AddText(best, "", 32, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold));
            }

            SmallButton(dishes, new Vector2(-760f, -400f), new Vector2(240f, 90f), "← Назад", () => ShowDishSelect(false));
            AddText(Stretch(NewRect("Help", dishes), new Vector2(0.15f, 0.04f), new Vector2(0.95f, 0.11f), Vector2.zero, Vector2.zero),
                "Зажми левую кнопку мыши и проведи по ингредиенту   •   1 / 2 / 3 — выбор блюда   •   Esc — пауза",
                30, TextColor, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        /// <summary>Карточка блюда: рамка, картинка, название и кнопка «Готовить». Кликается вся карточка.</summary>
        void DishCard(RectTransform parent, RecipeDef recipe, Vector2 position, Vector2 size)
        {
            var card = Place(NewRect(recipe.Title, parent), new Vector2(0.5f, 0.5f), position, size);
            var fill = Framed(card, MenuCream, 10f);
            MakeButtonOn(card, fill, () =>
            {
                _game.Sfx.PlayClick();
                _game.StartRound(recipe);
            });

            var dish = Art.DishSprite(recipe);
            if (dish != null)
            {
                var image = AddImage(Place(NewRect("Dish", card), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(290f, 290f)), Color.white, null);
                image.sprite = dish;
                image.preserveAspect = true;
            }
            MenuText(Place(NewRect("Name", card), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(size.x - 40f, 70f)),
                recipe.Title, 52, MenuBrownText, TextAnchor.MiddleCenter, FontStyle.Bold);

            // «Готовить» — оформление; нажатие ловит вся карточка.
            var cook = Place(NewRect("Cook", card), new Vector2(0.5f, 0f), new Vector2(0f, 68f), new Vector2(size.x - 90f, 84f));
            Framed(cook, MenuGreen, 6f);
            MenuText(Fill(NewRect("Label", cook)), "▶  Готовить", 38, MenuLightText, TextAnchor.MiddleCenter, FontStyle.Bold);
            foreach (var graphic in cook.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        }

        /// <summary>Крупный заголовок, как «Пауза»: тёмно-зелёный с кремовой обводкой.</summary>
        void BigTitle(RectTransform parent, string text, Vector2 position)
        {
            var title = MenuText(Place(NewRect("Title", parent), new Vector2(0.5f, 0.5f), position, new Vector2(1400f, 150f)),
                text, 110, MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.GetComponent<Shadow>().enabled = false;
            var outline = title.gameObject.AddComponent<SmoothOutline>();
            outline.effectColor = MenuCream;
            outline.effectDistance = new Vector2(4f, -4f);
        }

        /// <summary>Маленькая кремовая кнопка в рамке.</summary>
        void SmallButton(RectTransform parent, Vector2 position, Vector2 size, string text, UnityAction onClick)
        {
            var rt = Place(NewRect(text, parent), new Vector2(0.5f, 0.5f), position, size);
            var fill = Framed(rt, MenuCream, 6f);
            MakeButtonOn(rt, fill, () =>
            {
                _game.Sfx.PlayClick();
                onClick();
            });
            MenuText(Fill(NewRect("Label", rt)), text, 36, MenuBrownText, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        void BuildCredits(RectTransform parent)
        {
            var credits = Fill(NewRect("Credits", parent));
            _creditsScreen = credits.gameObject;
            AddImage(credits, new Color(0f, 0f, 0f, 0.55f), null).raycastTarget = true;
            BigTitle(credits, "Разработчики", new Vector2(0f, 380f));
            var panel = Place(NewRect("Panel", credits), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1100f, 520f));
            Framed(panel, MenuCream, 10f);
            AddText(Stretch(NewRect("Text", panel), Vector2.zero, Vector2.one, new Vector2(40f, 40f), new Vector2(-40f, -40f)),
                string.Join("\n", Credits), 36, MenuBrownText, TextAnchor.MiddleCenter, FontStyle.Normal).GetComponent<Shadow>().enabled = false;
            MenuButton(credits, new Vector2(0f, -340f), "Назад", true, () => ShowCredits(false));
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
            _pauseTitle.text = "Настройки";
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

        /// <summary>
        /// Фон меню: покрыть экран с сохранением пропорций, прижать к верху. Те же масштаб (+4%) и сдвиг
        /// вверх (1% высоты), что у фона игры в GameManager, — при переходе меню → игра ничего не сдвигается.
        /// </summary>
        void LayoutMenuBackground()
        {
            var size = _root.rect.size;
            float w = Mathf.Max(size.x, size.y * _menuBackgroundAspect) * 1.04f;
            _menuBackground.sizeDelta = new Vector2(w, w / _menuBackgroundAspect);
            _menuBackground.anchoredPosition = new Vector2(0f, size.y * 0.01f);
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

        // Палитра в стиле главного меню: кремовые панели в деревянной рамке, оливковые кнопки.
        static readonly Color MenuCream = new Color32(0xF6, 0xEB, 0xD3, 0xFF);
        static readonly Color MenuCreamDark = new Color32(0xE6, 0xD3, 0xAE, 0xFF);
        static readonly Color MenuFrame = new Color32(0xC9, 0x81, 0x3F, 0xFF);
        static readonly Color MenuOutline = new Color32(0x7A, 0x45, 0x1C, 0xFF);
        static readonly Color MenuGreen = new Color32(0x6E, 0x84, 0x43, 0xFF);
        static readonly Color MenuGreenText = new Color32(0x45, 0x55, 0x22, 0xFF);
        static readonly Color MenuBrownText = new Color32(0x5A, 0x3D, 0x22, 0xFF);
        static readonly Color MenuLightText = new Color32(0xFF, 0xF6, 0xE3, 0xFF);

        void BuildPause()
        {
            var pause = Fill(NewRect("Pause", _root));
            _pause = pause.gameObject;
            // Затемнение ловит клики, чтобы под паузой ничего не нажималось.
            AddImage(pause, new Color(0f, 0f, 0f, 0.55f), null).raycastTarget = true;

            var title = MenuText(Place(NewRect("Title", pause), new Vector2(0.5f, 0.5f), new Vector2(0f, 430f), new Vector2(900f, 130f)),
                "Пауза", 100, MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.GetComponent<Shadow>().enabled = false;
            _pauseTitle = title;
            var outline = title.gameObject.AddComponent<SmoothOutline>();
            outline.effectColor = MenuCream;
            outline.effectDistance = new Vector2(4f, -4f);

            // ── слева: блоки «Звук» и «Графика» в рамках
            var sound = Place(NewRect("Sound", pause), new Vector2(0.5f, 0.5f), new Vector2(-250f, 175f), new Vector2(1000f, 330f));
            Framed(sound, MenuCream, 10f);
            SectionTitle(sound, "Звук");
            SliderRow(sound, 92f, "Общая громкость", 0f, 1f, GameSettings.Master, GameSettings.SetMaster, Percent);
            SliderRow(sound, 166f, "Музыка", 0f, 1f, GameSettings.Music, GameSettings.SetMusic, Percent);
            SliderRow(sound, 240f, "Эффекты", 0f, 1f, GameSettings.Effects, GameSettings.SetEffects, Percent);

            var graphics = Place(NewRect("Graphics", pause), new Vector2(0.5f, 0.5f), new Vector2(-250f, -200f), new Vector2(1000f, 330f));
            Framed(graphics, MenuCream, 10f);
            SectionTitle(graphics, "Графика");
            CollectResolutions();
            _resolutionLabel = CyclerRow(graphics, 92f, "Разрешение", () => ChangeResolution(-1), () => ChangeResolution(1));
            _windowModeLabel = CyclerRow(graphics, 166f, "Режим", ToggleWindowMode, ToggleWindowMode);
            SliderRow(graphics, 240f, "Яркость", GameSettings.MinBrightness, GameSettings.MaxBrightness, GameSettings.Brightness,
                v =>
                {
                    GameSettings.SetBrightness(v);
                    ApplyBrightness();
                }, Percent);

            // ── справа: кнопки, как в главном меню. Игровые — в паузе, «Назад» — в настройках из главного меню.
            var gameButtons = Fill(NewRect("GameButtons", pause));
            _pauseGameButtons = gameButtons.gameObject;
            MenuButton(gameButtons, new Vector2(560f, 180f), "Продолжить", true, _game.Resume);
            MenuButton(gameButtons, new Vector2(560f, 30f), "Заново", false, _game.Restart);
            MenuButton(gameButtons, new Vector2(560f, -120f), "В главное меню", false, _game.BackToMenu);

            var settingsBack = Fill(NewRect("SettingsBack", pause));
            _settingsBack = settingsBack.gameObject;
            MenuButton(settingsBack, new Vector2(560f, 180f), "Назад", true, HideSettings);
            _settingsBack.SetActive(false);

            _pause.SetActive(false);
        }

        static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        /// <summary>
        /// Рамка в стиле главного меню: тёмный контур, деревянная (оранжево-коричневая) рамка и заливка.
        /// Возвращает картинку заливки.
        /// </summary>
        static Image Framed(RectTransform rt, Color fill, float frame)
        {
            AddImage(rt, MenuOutline, Art.RoundedRect);
            AddImage(Stretch(NewRect("Frame", rt), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)), MenuFrame, Art.RoundedRect);
            float inset = 3f + frame;
            var inner = AddImage(Stretch(NewRect("Fill", rt), Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset)),
                fill, Art.RoundedRect);
            return inner;
        }

        void SectionTitle(RectTransform block, string text)
        {
            MenuText(Stretch(NewRect("Title", block), new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -78f), new Vector2(-40f, -18f)),
                text, 46, MenuGreenText, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        /// <summary>Тёмный текст на кремовом фоне: вместо чёрной тени — светлая подложка.</summary>
        Text MenuText(RectTransform rt, string text, int size, Color color, TextAnchor anchor, FontStyle style)
        {
            var label = AddText(rt, text, size, color, anchor, style);
            var shadow = label.GetComponent<Shadow>();
            shadow.effectColor = new Color(1f, 1f, 1f, 0.45f);
            shadow.effectDistance = new Vector2(1f, -2f);
            return label;
        }

        /// <summary>Большая кнопка в рамке: основная — оливковая со светлым текстом, остальные — кремовые.</summary>
        void MenuButton(RectTransform parent, Vector2 position, string text, bool primary, UnityAction onClick)
        {
            var rt = Place(NewRect(text, parent), new Vector2(0.5f, 0.5f), position, new Vector2(460f, 120f));
            var fill = Framed(rt, primary ? MenuGreen : MenuCream, 8f);
            MakeButtonOn(rt, fill, onClick);
            MenuText(Fill(NewRect("Label", rt)), text, 44, primary ? MenuLightText : MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        /// <summary>Кнопка на уже нарисованной рамке: подсвечивается заливка, клики ловит вся рамка.</summary>
        static void MakeButtonOn(RectTransform rt, Image fill, UnityAction onClick)
        {
            var hit = rt.GetComponent<Image>();
            hit.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.93f, 0.8f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(onClick);
        }

        /// <summary>Строка «подпись — ползунок с ножом — значение».</summary>
        void SliderRow(RectTransform block, float top, string title, float min, float max, float value,
            System.Action<float> onChange, System.Func<float, string> format)
        {
            var row = Stretch(NewRect(title, block), new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -top - 60f), new Vector2(-40f, -top));
            MenuText(Stretch(NewRect("Label", row), Vector2.zero, new Vector2(0.36f, 1f), Vector2.zero, Vector2.zero),
                title, 34, MenuBrownText, TextAnchor.MiddleLeft, FontStyle.Bold);
            var valueText = MenuText(Stretch(NewRect("Value", row), new Vector2(0.87f, 0f), Vector2.one, Vector2.zero, Vector2.zero),
                format(value), 34, MenuGreenText, TextAnchor.MiddleRight, FontStyle.Bold);

            // Дорожка — «разделочная доска»: тёмный контур, кремово-бежевая заливка.
            var area = Stretch(NewRect("Slider", row), new Vector2(0.38f, 0.3f), new Vector2(0.85f, 0.7f), Vector2.zero, Vector2.zero);
            AddImage(area, MenuOutline, Art.RoundedRect).raycastTarget = true;
            AddImage(Stretch(NewRect("Track", area), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)), MenuCreamDark, Art.RoundedRect);

            // Заливка — уже «нарезанная» часть: оливковая, как основная кнопка.
            var fillArea = Stretch(NewRect("FillArea", area), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            // Slider двигает только горизонтальные якоря — по высоте заливка растянута сама.
            var fill = Fill(NewRect("Fill", fillArea));
            AddImage(fill, MenuGreen, Art.RoundedRect);

            // Ручка — нож лезвием вниз, «режет» дорожку в точке значения.
            var handleArea = Stretch(NewRect("HandleArea", area), Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-14f, 0f));
            var handle = NewRect("Handle", handleArea);
            handle.pivot = new Vector2(0.5f, 0.42f);
            handle.sizeDelta = new Vector2(30f, 76f);
            var knife = Art.LoadSprite("UI/knife");
            var handleImage = AddImage(handle, Color.white, knife != null ? knife : Art.Circle);
            handleImage.type = Image.Type.Simple;
            handleImage.preserveAspect = true;
            handleImage.raycastTarget = true;

            var slider = area.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            var colors = slider.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.85f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            slider.colors = colors;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v =>
            {
                onChange(v);
                valueText.text = format(v);
            });
        }

        /// <summary>Строка «подпись — [&lt;] значение [&gt;]». Возвращает текст значения.</summary>
        Text CyclerRow(RectTransform block, float top, string title, UnityAction onPrev, UnityAction onNext)
        {
            var row = Stretch(NewRect(title, block), new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -top - 60f), new Vector2(-40f, -top));
            MenuText(Stretch(NewRect("Label", row), Vector2.zero, new Vector2(0.36f, 1f), Vector2.zero, Vector2.zero),
                title, 34, MenuBrownText, TextAnchor.MiddleLeft, FontStyle.Bold);

            ArrowButton(Stretch(NewRect("Prev", row), new Vector2(0.38f, 0f), new Vector2(0.45f, 1f), Vector2.zero, Vector2.zero), "<", onPrev);
            ArrowButton(Stretch(NewRect("Next", row), new Vector2(0.93f, 0f), Vector2.one, Vector2.zero, Vector2.zero), ">", onNext);

            return MenuText(Stretch(NewRect("Value", row), new Vector2(0.45f, 0f), new Vector2(0.93f, 1f), Vector2.zero, Vector2.zero),
                "", 34, MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        void ArrowButton(RectTransform rt, string arrow, UnityAction onClick)
        {
            var fill = Framed(rt, MenuGreen, 4f);
            MakeButtonOn(rt, fill, onClick);
            MenuText(Fill(NewRect("Label", rt)), arrow, 38, MenuLightText, TextAnchor.MiddleCenter, FontStyle.Bold);
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
