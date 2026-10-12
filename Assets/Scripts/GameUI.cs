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
        /// <summary>Внутри _pause: экран паузы (4 кнопки) и экран настроек.</summary>
        GameObject _pauseScreen, _settingsScreen;
        /// <summary>Настройки открыты из паузы (иначе — из главного меню): туда и вернёт «Назад».</summary>
        bool _settingsFromPause;
        /// <summary>Вкладки настроек: панель и две версии кнопки-вкладки (выбрана / не выбрана).</summary>
        readonly List<(GameObject Panel, GameObject On, GameObject Off)> _settingsTabs = new List<(GameObject, GameObject, GameObject)>();
        /// <summary>Обновить ползунки после «Сбросить» или отмены изменений.</summary>
        readonly List<System.Action> _settingsRefreshers = new List<System.Action>();
        Text _settingsStatus;
        bool _settingsJustSaved;
        /// <summary>Сколько ползунков создано — чтобы чередовать перечницу и солонку.</summary>
        int _sliderCount;
        readonly List<(HotkeyAction Action, Text Label)> _hotkeyLabels = new List<(HotkeyAction, Text)>();
        Text _resolutionLabel, _windowModeLabel;
        readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
        int _resolutionIndex;
        /// <summary>
        /// Режим окна храним сами: Screen.fullScreenMode меняется только со следующего кадра
        /// (а в редакторе не меняется вовсе), и переключатель по нему «залипал».
        /// </summary>
        int _windowModeIndex;
        static readonly (FullScreenMode Mode, string Title)[] WindowModes =
        {
            (FullScreenMode.FullScreenWindow, "Полный экран"),
            (FullScreenMode.Windowed, "В окне"),
        };
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
            // Выход из паузы при открытых настройках (например, клавишей «заново») отменяет несохранённое.
            if (_settingsScreen.activeSelf) RevertSettings();
            _settingsScreen.SetActive(false);
            _pauseScreen.SetActive(true);
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
            _score.text = $"Качество блюда: {round.Score}/100";
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            LayoutMenuBackground();
            if (InputUtil.Listening != null && InputUtil.PollRebind()) RefreshSettingsUI();

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

            // Верхний блок: плашка с таймером и качеством блюда + квадратная кнопка паузы справа.
            // Вся группа по центру экрана, кнопка — по центру высоты плашки, отступы сверху и снизу одинаковые.
            const float plateWidth = 460f, plateHeight = 170f, top = 14f, gap = 14f, pauseSize = 112f;
            float groupLeft = -(plateWidth + gap + pauseSize) * 0.5f;
            float plateX = groupLeft + plateWidth * 0.5f;
            float pauseX = groupLeft + plateWidth + gap + pauseSize * 0.5f;
            float centerY = -top - plateHeight * 0.5f;

            // Тёмная плашка под таймером и счётом — чтобы читались на светлом фоне.
            _timerPlate = Place(NewRect("TimerPlate", hud), new Vector2(0.5f, 1f), new Vector2(plateX, centerY), new Vector2(plateWidth, plateHeight)).gameObject;
            AddImage((RectTransform)_timerPlate.transform, PanelColor, Art.RoundedRect);
            _timerRect = Place(NewRect("Timer", hud), new Vector2(0.5f, 1f), new Vector2(plateX, -top - 56f), new Vector2(plateWidth - 40f, 100f));
            _timer = AddText(_timerRect, "1:00", 84, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            _score = AddText(Place(NewRect("Score", hud), new Vector2(0.5f, 1f), new Vector2(plateX, -top - plateHeight + 38f), new Vector2(plateWidth - 20f, 50f)),
                "", 34, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            // Кнопка паузы справа от плашки.
            var pause = Place(NewRect("PauseButton", hud), new Vector2(0.5f, 1f), new Vector2(pauseX, centerY), new Vector2(pauseSize, pauseSize));
            MakeButton(pause, SecondaryButtonColor, _game.Pause);
            _pauseButton = pause.gameObject;
            AddText(Fill(NewRect("Label", pause)), "II", 52, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
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

            var name = AddText(Stretch(NewRect("Name", rt), Vector2.zero, Vector2.one, new Vector2(82f, 0f), new Vector2(wrong ? -10f : -108f, 0f)),
                def.Name, 28, TextColor, TextAnchor.MiddleLeft, FontStyle.Normal);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 16;
            name.resizeTextMaxSize = 28;
            name.verticalOverflow = VerticalWrapMode.Truncate;

            // У лишних только иконка и название — без счётчика.
            Text count = null;
            if (!wrong)
            {
                // Шире и без переноса: «10/12» не должно перескакивать на вторую строку.
                count = AddText(Stretch(NewRect("Count", rt), new Vector2(1f, 0f), Vector2.one, new Vector2(-104f, 0f), new Vector2(-8f, 0f)),
                    $"0/{need}", 32, TextColor, TextAnchor.MiddleRight, FontStyle.Bold);
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            return new Row { Def = def, Need = need, Wrong = wrong, Rect = rt, Background = background, Count = count };
        }

        // ───────────────────────── Меню: фон игры + интерфейс в стиле паузы ─────────────────────────

        /// <summary>Затемнение фона меню — как у фона игры.</summary>
        static readonly Color MenuTint = new Color(0.6f, 0.6f, 0.64f);

        /// <summary>Экран «Разработчики»: подзаголовок и роли команды.</summary>
        const string CreditsSubtitle = "Игра для FunFlow Game Jam × ТПУ";
        static readonly (string Role, string Names)[] Credits =
        {
            ("Геймдизайн механик и Код", "Юрьев Тимофей  •  Клименко Станислав  •  Макурин Артём"),
            ("Иллюстрации и Оформление", "Лушников Дмитрий"),
            ("Звуковое сопровождение и Пользовательский интерфейс", "Миков Кирилл"),
            ("Игровой движок", "Unity 6.6"),
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

            // Текучий фон в духе Balatro (шейдер Resources/Shaders/MenuSwirl). Если шейдер не поддерживается — остаётся кухня.
            var swirl = SwirlMaterial();
            if (swirl != null)
            {
                var fluid = AddImage(Fill(NewRect("Swirl", menu)), Color.white, null);
                fluid.material = swirl;
                _menuBackground.gameObject.SetActive(false);
            }

            BuildMainScreen(menu);
            BuildDishScreen(menu);
            BuildCredits(menu);
        }

        void BuildMainScreen(RectTransform menu)
        {
            var main = Fill(NewRect("Main", menu));
            _mainScreen = main.gameObject;
            LogoTitle(main, new Vector2(0f, 320f));
            MenuButton(main, new Vector2(0f, 110f), "▶  Начать игру", true, () => ShowDishSelect(true));
            MenuButton(main, new Vector2(0f, -40f), "Настройки", false, () => OpenSettings(false));
            MenuButton(main, new Vector2(0f, -190f), "Разработчики", false, () => ShowCredits(true));
            MenuButton(main, new Vector2(0f, -340f), "Выйти из игры", false, QuitGame);
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            // В редакторе Application.Quit ничего не делает — останавливаем Play.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
        }

        /// <summary>Карточка блюда: рамка, картинка, название и кнопка «Готовить». Кликается вся карточка.</summary>
        void DishCard(RectTransform parent, RecipeDef recipe, Vector2 position, Vector2 size)
        {
            DropShadow(parent, new Vector2(0.5f, 0.5f), position, size);
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
            var cookPosition = new Vector2(0f, 68f);
            var cookSize = new Vector2(size.x - 90f, 84f);
            DropShadow(card, new Vector2(0.5f, 0f), cookPosition, cookSize);
            var cook = Place(NewRect("Cook", card), new Vector2(0.5f, 0f), cookPosition, cookSize);
            var face = Beveled(cook, MenuGreen, 6f);
            MenuText(Fill(NewRect("Label", face.rectTransform)), "▶  Готовить", 38, MenuLightText, TextAnchor.MiddleCenter, FontStyle.Bold);
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

        /// <summary>
        /// Название игры в главном меню: «Поварской» — зелёным, «срез» — томатно-красным; объёмные буквы,
        /// кремовая и тёмно-коричневая обводки, тень, нож рядом и лёгкое «дыхание».
        /// </summary>
        void LogoTitle(RectTransform parent, Vector2 position)
        {
            var logo = Place(NewRect("Logo", parent), new Vector2(0.5f, 0.5f), position, new Vector2(1300f, 200f));
            logo.gameObject.AddComponent<LogoBreathe>();

            var title = AddText(Fill(NewRect("Title", logo)), "Поварской <color=#E0502E>срез</color>", 136,
                new Color32(0x7C, 0x9C, 0x36, 0xFF), TextAnchor.MiddleCenter, FontStyle.Bold);
            title.supportRichText = true;
            title.horizontalOverflow = HorizontalWrapMode.Overflow; // название всегда в одну строку
            title.GetComponent<Shadow>().enabled = false;
            // Порядок эффектов важен: объём букв → кремовая обводка → тёмная внешняя обводка → тень.
            title.gameObject.AddComponent<VerticalShade>();
            var inner = title.gameObject.AddComponent<SmoothOutline>();
            inner.effectColor = MenuCream;
            inner.effectDistance = new Vector2(5f, -5f);
            var outer = title.gameObject.AddComponent<SmoothOutline>();
            outer.effectColor = MenuOutline;
            outer.effectDistance = new Vector2(4f, -4f);
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -9f);

            // Нож «врезается» в слово «срез» справа.
            var knife = Art.LoadSprite("UI/knife");
            if (knife == null) return;
            var knifeImage = AddImage(Place(NewRect("Knife", logo), new Vector2(0.5f, 0.5f), new Vector2(560f, 12f), new Vector2(72f, 180f)),
                Color.white, knife);
            knifeImage.type = Image.Type.Simple;
            knifeImage.preserveAspect = true;
            knifeImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -32f);
        }

        /// <summary>Маленькая кремовая кнопка в рамке.</summary>
        void SmallButton(RectTransform parent, Vector2 position, Vector2 size, string text, UnityAction onClick)
        {
            DropShadow(parent, new Vector2(0.5f, 0.5f), position, size);
            var rt = Place(NewRect(text, parent), new Vector2(0.5f, 0.5f), position, size);
            var face = Beveled(rt, MenuCream, 6f);
            MakeButtonOn(rt, face, () =>
            {
                _game.Sfx.PlayClick();
                onClick();
            });
            MenuText(Fill(NewRect("Label", face.rectTransform)), text, 36, MenuBrownText, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        void BuildCredits(RectTransform parent)
        {
            var credits = Fill(NewRect("Credits", parent));
            _creditsScreen = credits.gameObject;
            AddImage(credits, new Color(0f, 0f, 0f, 0.55f), null).raycastTarget = true;
            // Подложка под заголовком, панелью и кнопкой — не путается с заголовком главного меню под окном.
            PopupBackdrop(credits, new Vector2(0f, 20f), new Vector2(1260f, 900f));
            BigTitle(credits, "Разработчики", new Vector2(0f, 380f));
            var panelPosition = new Vector2(0f, 10f);
            var panelSize = new Vector2(1100f, 560f);
            DropShadow(credits, new Vector2(0.5f, 0.5f), panelPosition, panelSize);
            var panel = Place(NewRect("Panel", credits), new Vector2(0.5f, 0.5f), panelPosition, panelSize);
            Framed(panel, MenuCream, 10f);
            MenuText(Stretch(NewRect("Subtitle", panel), new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -80f), new Vector2(-40f, -22f)),
                CreditsSubtitle, 32, MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Italic);

            // Каждая роль — бежевая плашка: сверху роль (зелёным), под ней имена (коричневым).
            const float plateTop = 88f, plateHeight = 98f, plateGap = 10f;
            for (int i = 0; i < Credits.Length; i++)
            {
                var (role, names) = Credits[i];
                float top = plateTop + i * (plateHeight + plateGap);
                var plate = Stretch(NewRect(role, panel), new Vector2(0f, 1f), Vector2.one,
                    new Vector2(36f, -top - plateHeight), new Vector2(-36f, -top));
                Rounded(plate, MenuCreamDark, 14f);
                MenuText(Stretch(NewRect("Role", plate), new Vector2(0f, 0.5f), Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, -6f)),
                    role, 27, MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);
                var namesText = MenuText(Stretch(NewRect("Names", plate), Vector2.zero, new Vector2(1f, 0.5f), new Vector2(20f, 6f), new Vector2(-20f, 0f)),
                    names, 34, MenuBrownText, TextAnchor.MiddleCenter, FontStyle.Bold);
                // Длинная строка имён не переносится, а слегка уменьшается — чтобы не налезать на роль.
                namesText.horizontalOverflow = HorizontalWrapMode.Wrap;
                namesText.verticalOverflow = VerticalWrapMode.Truncate;
                namesText.resizeTextForBestFit = true;
                namesText.resizeTextMinSize = 22;
                namesText.resizeTextMaxSize = 34;
            }

            MenuButton(credits, new Vector2(0f, -350f), "Назад", true, () => ShowCredits(false));
            _creditsScreen.SetActive(false);
        }

        void ShowDishSelect(bool visible)
        {
            _dishScreen.SetActive(visible);
            _mainScreen.SetActive(!visible);
        }

        void ShowCredits(bool visible) => _creditsScreen.SetActive(visible);

        /// <summary>Esc в меню: закрыть верхний экран. Возвращает true, если что-то закрылось.</summary>
        public bool MenuBack()
        {
            if (CloseSettings()) return true;
            if (_creditsScreen.activeSelf) { ShowCredits(false); return true; }
            if (_dishScreen.activeSelf) { ShowDishSelect(false); return true; }
            return false;
        }

        /// <summary>Материал текучего фона меню или null, если шейдера нет или видеокарта его не поддерживает.</summary>
        static Material SwirlMaterial()
        {
            var shader = Resources.Load<Shader>("Shaders/MenuSwirl");
            if (shader == null || !shader.isSupported) return null;
            return new Material(shader) { name = "MenuSwirl" };
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
            PopupBackdrop(results, Vector2.zero, new Vector2(1080f, 910f));

            // Карточка в объёмной деревянной рамке, как в меню; цвета итогов прежние.
            DropShadow(results, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 830f));
            var panel = Place(NewRect("Panel", results), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 830f));
            Framed(panel, CardColor, 10f);

            _resTitle = AddText(Top(panel, 30f, 90f), "", 64, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            _resIcon = AddImage(Place(NewRect("Dish", panel), new Vector2(0.5f, 1f), new Vector2(-380f, -150f), new Vector2(150f, 150f)), Color.white, null);
            _resIcon.preserveAspect = true;
            _resScore = AddText(Top(panel, 120f, 170f), "", 150, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            _resStars = AddText(Top(panel, 290f, 90f), "", 80, AccentColor, TextAnchor.MiddleCenter, FontStyle.Normal);
            _resDetails = AddText(Top(panel, 395f, 170f), "", 32, TextColor, TextAnchor.UpperCenter, FontStyle.Normal);
            _resVerdict = AddText(Top(panel, 570f, 60f), "", 36, GoodColor, TextAnchor.MiddleCenter, FontStyle.Italic);
            _resBest = AddText(Top(panel, 630f, 44f), "", 30, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            // «Ещё раз» — тот же уровень заново, «Другое блюдо» — экран выбора блюда.
            ResultButton(panel, -220f, "Ещё раз", ButtonColor, _game.Restart);
            ResultButton(panel, 220f, "Другое блюдо", SecondaryButtonColor, _game.ToDishSelect);
        }

        /// <summary>Объёмная кнопка внизу карточки итогов, в цветах итогов.</summary>
        void ResultButton(RectTransform panel, float x, string text, Color color, UnityAction onClick)
        {
            var anchor = new Vector2(0.5f, 0f);
            var position = new Vector2(x, 84f);
            var size = new Vector2(400f, 104f);
            DropShadow(panel, anchor, position, size);
            var rt = Place(NewRect(text, panel), anchor, position, size);
            var face = Beveled(rt, color, 6f);
            MakeButtonOn(rt, face, onClick);
            AddText(Fill(NewRect("Label", face.rectTransform)), text, 38, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
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
        static readonly Color MenuWarnText = new Color32(0xA8, 0x3A, 0x2A, 0xFF);

        void BuildPause()
        {
            var pause = Fill(NewRect("Pause", _root));
            _pause = pause.gameObject;
            // Затемнение ловит клики, чтобы под паузой ничего не нажималось.
            AddImage(pause, new Color(0f, 0f, 0f, 0.55f), null).raycastTarget = true;

            BuildPauseScreen(pause);
            BuildSettingsScreen(pause);
            _pause.SetActive(false);
        }

        /// <summary>Крупный заголовок окна паузы/настроек: тёмно-зелёный с кремовой обводкой.</summary>
        void PopupTitle(RectTransform parent, string text, Vector2 position)
        {
            var title = MenuText(Place(NewRect("Title", parent), new Vector2(0.5f, 0.5f), position, new Vector2(900f, 130f)),
                text, 100, MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.GetComponent<Shadow>().enabled = false;
            var outline = title.gameObject.AddComponent<SmoothOutline>();
            outline.effectColor = MenuCream;
            outline.effectDistance = new Vector2(4f, -4f);
        }

        /// <summary>Пауза на уровне: четыре кнопки по центру.</summary>
        void BuildPauseScreen(RectTransform pause)
        {
            var screen = Fill(NewRect("PauseScreen", pause));
            _pauseScreen = screen.gameObject;
            PopupBackdrop(screen, new Vector2(0f, 60f), new Vector2(620f, 840f));
            PopupTitle(screen, "Пауза", new Vector2(0f, 390f));
            MenuButton(screen, new Vector2(0f, 210f), "Продолжить", true, _game.Resume);
            MenuButton(screen, new Vector2(0f, 60f), "Заново", false, _game.Restart);
            MenuButton(screen, new Vector2(0f, -90f), "Настройки", false, () => OpenSettings(true));
            MenuButton(screen, new Vector2(0f, -240f), "Главное меню", false, _game.BackToMenu);
        }

        // ───────────────────────── Настройки ─────────────────────────

        /// <summary>
        /// Настройки: слева вкладки «Звук», «Графика», «Горячие клавиши» и панель выбранной вкладки,
        /// справа — «Назад», «Сохранить», «Сбросить» и строка состояния.
        /// </summary>
        void BuildSettingsScreen(RectTransform pause)
        {
            var screen = Fill(NewRect("SettingsScreen", pause));
            _settingsScreen = screen.gameObject;
            // Заголовок поднят над вкладками с запасом, подложка чуть выше, чтобы его вместить.
            PopupBackdrop(screen, new Vector2(20f, 55f), new Vector2(1620f, 940f));
            PopupTitle(screen, "Настройки", new Vector2(0f, 430f));

            // Панель вкладки: слева, под полосой вкладок.
            var panelPosition = new Vector2(-250f, -85f);
            var panelSize = new Vector2(1000f, 600f);

            // Строки звука и графики — по центру высоты панели (сверху 150…450 из 600).
            var sound = SettingsTab(screen, 0, "Звук", panelPosition, panelSize);
            SliderRow(sound, 150f, "Общая громкость", 0f, 1f, () => GameSettings.Master, GameSettings.SetMaster, Percent);
            SliderRow(sound, 270f, "Музыка", 0f, 1f, () => GameSettings.Music, GameSettings.SetMusic, Percent);
            SliderRow(sound, 390f, "Эффекты", 0f, 1f, () => GameSettings.Effects, GameSettings.SetEffects, Percent);

            var graphics = SettingsTab(screen, 1, "Графика", panelPosition, panelSize);
            CollectResolutions();
            _resolutionLabel = CyclerRow(graphics, 150f, "Разрешение", () => ChangeResolution(-1), () => ChangeResolution(1));
            _windowModeLabel = CyclerRow(graphics, 270f, "Режим", () => ChangeWindowMode(-1), () => ChangeWindowMode(1));
            SliderRow(graphics, 390f, "Яркость", GameSettings.MinBrightness, GameSettings.MaxBrightness, () => GameSettings.Brightness,
                v =>
                {
                    GameSettings.SetBrightness(v);
                    ApplyBrightness();
                }, Percent);

            var hotkeys = SettingsTab(screen, 2, "Горячие клавиши", panelPosition, panelSize);
            MenuText(Stretch(NewRect("Hint", hotkeys), new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -76f), new Vector2(-40f, -20f)),
                "Нажми на клавишу справа, затем — новую клавишу", 30, MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);
            const float rowHeight = 80f, rowGap = 12f;
            var actions = InputUtil.Actions;
            for (int i = 0; i < actions.Length; i++)
                HotkeyRow(hotkeys, 90f + i * (rowHeight + rowGap), rowHeight, actions[i]);

            // ── справа: кнопки по центру высоты панели и строка состояния под ними
            MenuButton(screen, new Vector2(560f, 65f), "Назад", true, () => CloseSettings());
            MenuButton(screen, new Vector2(560f, -85f), "Сохранить", false, () =>
            {
                GameSettings.Save();
                InputUtil.SaveBindings();
                _settingsJustSaved = true;
                RefreshSettingsUI();
            });
            MenuButton(screen, new Vector2(560f, -235f), "Сбросить", false, () =>
            {
                GameSettings.ResetToDefaults();
                InputUtil.ResetBindings();
                ApplyBrightness();
                ResetGraphics();
                RefreshSettingsUI();
            });
            _settingsStatus = MenuText(Place(NewRect("Status", screen), new Vector2(0.5f, 0.5f), new Vector2(560f, -340f), new Vector2(500f, 70f)),
                "", 28, MenuLightText, TextAnchor.MiddleCenter, FontStyle.Bold);
            _settingsStatus.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.5f);

            ShowSettingsTab(0);
            _settingsScreen.SetActive(false);
        }

        /// <summary>Вкладка настроек: кнопка в полосе над панелью и сама панель. Возвращает панель.</summary>
        RectTransform SettingsTab(RectTransform screen, int index, string title, Vector2 panelPosition, Vector2 panelSize)
        {
            const float tabWidth = 320f, tabHeight = 84f, tabGap = 20f;
            var center = new Vector2(0.5f, 0.5f);
            var tabPosition = new Vector2(panelPosition.x + (index - 1) * (tabWidth + tabGap),
                panelPosition.y + panelSize.y * 0.5f + tabGap + tabHeight * 0.5f);
            var tabSize = new Vector2(tabWidth, tabHeight);

            // Две версии кнопки: выбранная — зелёная, остальные — кремовые; переключаются в ShowSettingsTab.
            var tab = Fill(NewRect(title + " Tab", screen));
            GameObject TabButton(string name, bool selected)
            {
                var holder = Fill(NewRect(name, tab));
                DropShadow(holder, center, tabPosition, tabSize);
                var rt = Place(NewRect("Button", holder), center, tabPosition, tabSize);
                var face = Beveled(rt, selected ? MenuGreen : MenuCream, 6f);
                MakeButtonOn(rt, face, () => ShowSettingsTab(index));
                MenuText(Fill(NewRect("Label", face.rectTransform)), title, 32, selected ? MenuLightText : MenuGreenText,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                return holder.gameObject;
            }
            var on = TabButton("On", true);
            var off = TabButton("Off", false);

            // Панель вместе с тенью — в одном контейнере, чтобы прятать их разом.
            var holder = Fill(NewRect(title, screen));
            DropShadow(holder, center, panelPosition, panelSize);
            var panel = Place(NewRect("Panel", holder), center, panelPosition, panelSize);
            Framed(panel, MenuCream, 10f);
            _settingsTabs.Add((holder.gameObject, on, off));
            return panel;
        }

        void ShowSettingsTab(int index)
        {
            InputUtil.CancelRebind();
            for (int i = 0; i < _settingsTabs.Count; i++)
            {
                var (panel, on, off) = _settingsTabs[i];
                panel.SetActive(i == index);
                on.SetActive(i == index);
                off.SetActive(i != index);
            }
            RefreshSettingsUI();
        }

        /// <param name="fromPause">Из паузы на уровне — «Назад» вернёт в паузу; иначе — в главное меню.</param>
        void OpenSettings(bool fromPause)
        {
            _settingsFromPause = fromPause;
            _settingsJustSaved = false;
            RefreshGraphicsLabels();
            ShowSettingsTab(0);
            _pauseScreen.SetActive(false);
            _settingsScreen.SetActive(true);
            _pause.SetActive(true);
        }

        /// <summary>«Назад» / Esc в настройках: отменить несохранённое и вернуться. true — настройки были открыты.</summary>
        public bool CloseSettings()
        {
            if (_settingsScreen == null || !_settingsScreen.activeSelf || !_pause.activeSelf) return false;
            RevertSettings();
            _settingsScreen.SetActive(false);
            if (_settingsFromPause) _pauseScreen.SetActive(true);
            else _pause.SetActive(false);
            return true;
        }

        /// <summary>Вернуть сохранённые громкость, яркость и клавиши.</summary>
        void RevertSettings()
        {
            GameSettings.Load();
            InputUtil.RevertBindings();
            ApplyBrightness();
            _settingsJustSaved = false;
            RefreshSettingsUI();
        }

        /// <summary>Ползунки, клавиши и строка «есть несохранённые изменения / сохранено».</summary>
        void RefreshSettingsUI()
        {
            foreach (var refresh in _settingsRefreshers) refresh();
            foreach (var (action, label) in _hotkeyLabels)
                label.text = InputUtil.Listening == action ? "Нажми клавишу…" : InputUtil.KeyName(action);
            RefreshSettingsStatus();
        }

        void RefreshSettingsStatus()
        {
            if (_settingsStatus == null) return;
            bool unsaved = GameSettings.HasUnsavedChanges() || InputUtil.HasUnsavedChanges();
            if (unsaved) _settingsJustSaved = false;
            _settingsStatus.text = unsaved ? "Есть несохранённые изменения" : _settingsJustSaved ? "Настройки сохранены" : "";
            _settingsStatus.color = unsaved ? new Color(1f, 0.72f, 0.6f) : MenuLightText;
        }

        void HotkeyRow(RectTransform panel, float top, float height, HotkeyAction action)
        {
            var row = Stretch(NewRect(action.ToString(), panel), new Vector2(0f, 1f), Vector2.one, new Vector2(60f, -top - height), new Vector2(-60f, -top));
            MenuText(Stretch(NewRect("Label", row), Vector2.zero, new Vector2(0.58f, 1f), Vector2.zero, Vector2.zero),
                HotkeyTitle(action), 34, MenuBrownText, TextAnchor.MiddleLeft, FontStyle.Bold);

            var anchor = new Vector2(0.79f, 0.5f);
            var keySize = new Vector2(340f, height - 8f);
            DropShadow(row, anchor, Vector2.zero, keySize);
            var key = Place(NewRect("Key", row), anchor, Vector2.zero, keySize);
            var face = Beveled(key, MenuCream, 6f);
            MakeButtonOn(key, face, () =>
            {
                if (InputUtil.Listening == action) InputUtil.CancelRebind();
                else InputUtil.BeginRebind(action);
                RefreshSettingsUI();
            });
            var label = MenuText(Fill(NewRect("Label", face.rectTransform)), "", 34, MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);
            _hotkeyLabels.Add((action, label));
        }

        static string HotkeyTitle(HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.Pause: return "Пауза / назад";
                case HotkeyAction.Restart: return "Начать уровень заново";
                default:
                    int index = action - HotkeyAction.Dish1;
                    var all = Recipes.All;
                    return index < all.Length ? $"Выбрать блюдо: {all[index].Title}" : $"Выбрать блюдо {index + 1}";
            }
        }

        static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        /// <summary>Радиус скругления, заложенный в Art.RoundedRect, в единицах холста.</summary>
        const float SpriteCornerRadius = 22f;
        /// <summary>Затемнение «торца» — нижней кромки объёмных рамок и кнопок.</summary>
        static readonly Color LipShade = new Color(0.72f, 0.72f, 0.7f, 1f);

        /// <summary>
        /// Скруглённый прямоугольник с заданным радиусом углов. Вложенным слоям радиус уменьшаем на отступ —
        /// тогда рамка одинаковой толщины и на углах, и углы не выглядят обрубленными.
        /// </summary>
        static Image Rounded(RectTransform rt, Color color, float radius)
        {
            var image = AddImage(rt, color, Art.RoundedRect);
            image.pixelsPerUnitMultiplier = SpriteCornerRadius / Mathf.Max(radius, 1f);
            return image;
        }

        /// <summary>
        /// Серая полупрозрачная подложка со светлой каймой под всплывающим окном (пауза, настройки,
        /// «Разработчики», результат): визуально отделяет окно от того, что под ним.
        /// </summary>
        static void PopupBackdrop(RectTransform parent, Vector2 position, Vector2 size)
        {
            var backdrop = Place(NewRect("Backdrop", parent), new Vector2(0.5f, 0.5f), position, size);
            Rounded(backdrop, new Color(1f, 1f, 1f, 0.25f), 32f);
            Rounded(Stretch(NewRect("Fill", backdrop), Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f)),
                new Color(0.17f, 0.17f, 0.19f, 0.85f), 28f);
        }

        /// <summary>
        /// Рамка в стиле главного меню: тёмный контур, объёмная деревянная рамка (тёмная нижняя кромка,
        /// светлая лицевая часть, блик сверху) и заливка. Возвращает картинку заливки.
        /// </summary>
        static Image Framed(RectTransform rt, Color fill, float frame, float radius = 24f)
        {
            Rounded(rt, MenuOutline, radius);
            float woodRadius = radius - 3f;
            var wood = Rounded(Stretch(NewRect("Frame", rt), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)),
                MenuFrame * LipShade, woodRadius);
            float lip = frame >= 6f ? 3f : 2f;
            var woodFace = Rounded(Stretch(NewRect("Face", wood.rectTransform), Vector2.zero, Vector2.one, new Vector2(0f, lip), Vector2.zero),
                MenuFrame, woodRadius);
            // Блик — светлая полоска по верхней планке рамки.
            Rounded(Stretch(NewRect("Shine", woodFace.rectTransform), new Vector2(0f, 1f), Vector2.one,
                    new Vector2(woodRadius, -1f - frame * 0.5f), new Vector2(-woodRadius, -1f)),
                new Color(1f, 1f, 1f, 0.3f), frame * 0.25f);
            float inset = 3f + frame;
            return Rounded(Stretch(NewRect("Fill", rt), Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset)),
                fill, Mathf.Max(radius - inset, 4f));
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
        void MenuButton(RectTransform parent, Vector2 position, string text, bool primary, UnityAction onClick, Vector2? buttonSize = null)
        {
            var size = buttonSize ?? new Vector2(480f, 124f);
            DropShadow(parent, new Vector2(0.5f, 0.5f), position, size);
            var rt = Place(NewRect(text, parent), new Vector2(0.5f, 0.5f), position, size);
            var face = Beveled(rt, primary ? MenuGreen : MenuCream, 8f);
            MakeButtonOn(rt, face, onClick);
            MenuText(Fill(NewRect("Label", face.rectTransform)), text, 42, primary ? MenuLightText : MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        /// <summary>
        /// Мягкая тень под кнопкой или панелью — она «лежит» над фоном, а не нарисована на нём.
        /// Создавать до самой кнопки/панели, с теми же якорем, позицией и размером.
        /// </summary>
        static void DropShadow(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size, float radius = 24f)
        {
            float offset = Mathf.Clamp(size.y * 0.075f, 3f, 12f);
            Rounded(Place(NewRect("Shadow", parent), anchor, position + new Vector2(0f, -offset), size + new Vector2(6f, 4f)),
                new Color(0f, 0f, 0f, 0.4f), radius + 2f);
        }

        /// <summary>
        /// Объёмная заливка кнопки внутри рамки: тёмный «торец» снизу, лицевая часть над ним и блик сверху.
        /// Возвращает лицевую часть — её подсвечивает кнопка и на неё кладётся надпись.
        /// </summary>
        static Image Beveled(RectTransform rt, Color color, float frame, float radius = 24f)
        {
            var lip = Framed(rt, color * LipShade, frame, radius);
            float faceRadius = Mathf.Max(radius - 3f - frame, 4f);
            float lipHeight = frame >= 6f ? 6f : 4f;
            var face = Rounded(Stretch(NewRect("Face", lip.rectTransform), Vector2.zero, Vector2.one, new Vector2(0f, lipHeight), Vector2.zero),
                color, faceRadius);
            // Блик — мягкое пятно света в верхней половине: плавно гаснет вниз и к бокам, без резких краёв.
            var gloss = AddImage(Stretch(NewRect("Gloss", face.rectTransform), new Vector2(0f, 0.45f), Vector2.one,
                    new Vector2(faceRadius * 0.5f, 0f), new Vector2(-faceRadius * 0.5f, -2f)),
                new Color(1f, 1f, 1f, 0.35f), Art.Gloss);
            gloss.type = Image.Type.Simple;
            return face;
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

        /// <summary>Строка «подпись — ползунок с мельницей — значение».</summary>
        void SliderRow(RectTransform block, float top, string title, float min, float max, System.Func<float> current,
            System.Action<float> onChange, System.Func<float, string> format)
        {
            float value = current();
            var row = Stretch(NewRect(title, block), new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -top - 60f), new Vector2(-40f, -top));
            MenuText(Stretch(NewRect("Label", row), Vector2.zero, new Vector2(0.36f, 1f), Vector2.zero, Vector2.zero),
                title, 34, MenuBrownText, TextAnchor.MiddleLeft, FontStyle.Bold);
            var valueText = MenuText(Stretch(NewRect("Value", row), new Vector2(0.87f, 0f), Vector2.one, Vector2.zero, Vector2.zero),
                format(value), 34, MenuGreenText, TextAnchor.MiddleRight, FontStyle.Bold);

            // Дорожка — «разделочная доска»: тёмный контур, кремово-бежевая заливка.
            var area = Stretch(NewRect("Slider", row), new Vector2(0.38f, 0.3f), new Vector2(0.85f, 0.7f), Vector2.zero, Vector2.zero);
            // Концы дорожки — полукруги: радиус равен половине высоты.
            float trackRadius = area.rect.height * 0.5f;
            Rounded(area, MenuOutline, trackRadius).raycastTarget = true;
            Rounded(Stretch(NewRect("Track", area), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)), MenuCreamDark, trackRadius - 3f);

            // Ручка ходит почти до самых краёв дорожки — на 0% и 100% она стоит на скруглённом торце.
            const float handleInset = 8f;
            // Ручка — мельница: солонка и перечница по очереди. Высота меньше шага строк (120),
            // чтобы мельницы соседних ползунков не налезали друг на друга.
            const float handleHeight = 84f;

            // Заливка — оливковая, как основная кнопка.
            // Область заливки совпадает с ходом ручки, и Slider двигает её правый край ровно в центр мельницы.
            // Сама заливка — маска: под ней лежит зелёная полоса во всю длину дорожки, поэтому слева она
            // скруглена как дорожка, а справа ровно «отрезана» в любом положении — и этот срез закрывает мельница.
            var fillArea = Stretch(NewRect("FillArea", area), Vector2.zero, Vector2.one, new Vector2(handleInset, 3f), new Vector2(-handleInset, -3f));
            // Slider двигает только горизонтальные якоря — отступы и высота заливки сохраняются.
            var fill = Stretch(NewRect("Fill", fillArea), Vector2.zero, Vector2.one, new Vector2(3f - handleInset, 0f), Vector2.zero);
            fill.gameObject.AddComponent<RectMask2D>();
            var stripe = NewRect("Stripe", fill);
            stripe.anchorMin = Vector2.zero;
            stripe.anchorMax = new Vector2(0f, 1f);
            stripe.pivot = new Vector2(0f, 0.5f);
            stripe.offsetMin = Vector2.zero;
            stripe.offsetMax = new Vector2(area.rect.width - 6f, 0f);
            Rounded(stripe, MenuGreen, trackRadius - 3f);

            // Ручка — мельница по центру дорожки. Slider растягивает ручку по высоте области (высота дорожки),
            // sizeDelta.y добавляет к ней недостающее до handleHeight.
            var handleArea = Stretch(NewRect("HandleArea", area), Vector2.zero, Vector2.one, new Vector2(handleInset, 0f), new Vector2(-handleInset, 0f));
            var handle = NewRect("Handle", handleArea);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(40f, handleHeight - area.rect.height); // ширина под пропорции шейкера (~0.46)
            var mill = Art.LoadSprite(_sliderCount++ % 2 == 0 ? "UI/pepper_mill" : "UI/salt_mill");
            var handleImage = AddImage(handle, Color.white, mill != null ? mill : Art.Circle);
            handleImage.type = Image.Type.Simple;
            handleImage.preserveAspect = true;
            handleImage.raycastTarget = true;

            var slider = area.gameObject.AddComponent<StepSlider>();
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
                RefreshSettingsStatus();
            });
            _settingsRefreshers.Add(() =>
            {
                slider.SetValueWithoutNotify(current());
                valueText.text = format(slider.value);
            });
        }

        /// <summary>Строка «подпись — [&lt;] значение [&gt;]». Возвращает текст значения.</summary>
        Text CyclerRow(RectTransform block, float top, string title, UnityAction onPrev, UnityAction onNext)
        {
            var row = Stretch(NewRect(title, block), new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -top - 60f), new Vector2(-40f, -top));
            MenuText(Stretch(NewRect("Label", row), Vector2.zero, new Vector2(0.36f, 1f), Vector2.zero, Vector2.zero),
                title, 34, MenuBrownText, TextAnchor.MiddleLeft, FontStyle.Bold);

            ArrowButton(row, new Vector2(0.415f, 0.5f), "<", onPrev);
            ArrowButton(row, new Vector2(0.965f, 0.5f), ">", onNext);

            return MenuText(Stretch(NewRect("Value", row), new Vector2(0.45f, 0f), new Vector2(0.93f, 1f), Vector2.zero, Vector2.zero),
                "", 34, MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        /// <summary>Круглая объёмная кнопка со стрелкой.</summary>
        void ArrowButton(RectTransform row, Vector2 anchor, string arrow, UnityAction onClick)
        {
            const float size = 58f;
            const float radius = size * 0.5f;
            DropShadow(row, anchor, Vector2.zero, new Vector2(size, size), radius);
            var rt = Place(NewRect(arrow, row), anchor, Vector2.zero, new Vector2(size, size));
            var face = Beveled(rt, MenuGreen, 4f, radius);
            MakeButtonOn(rt, face, onClick);
            MenuText(Fill(NewRect("Label", face.rectTransform)), arrow, 38, MenuLightText, TextAnchor.MiddleCenter, FontStyle.Bold);
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
            _windowModeIndex = Screen.fullScreenMode == FullScreenMode.Windowed ? 1 : 0;
        }

        int FindCurrentResolution()
        {
            int index = _resolutions.IndexOf(new Vector2Int(Screen.width, Screen.height));
            return index >= 0 ? index : _resolutions.Count - 1;
        }

        /// <summary>По кругу: за последним значением снова первое, перед первым — последнее.</summary>
        static int Wrap(int index, int count) => (index % count + count) % count;

        void ChangeResolution(int delta)
        {
            _resolutionIndex = Wrap(_resolutionIndex + delta, _resolutions.Count);
            ApplyGraphics();
        }

        void ChangeWindowMode(int delta)
        {
            _windowModeIndex = Wrap(_windowModeIndex + delta, WindowModes.Length);
            ApplyGraphics();
        }

        /// <summary>«Сбросить»: самое большое разрешение (обычно родное для экрана) и полный экран.</summary>
        void ResetGraphics()
        {
            _resolutionIndex = _resolutions.Count - 1;
            _windowModeIndex = 0;
            ApplyGraphics();
        }

        void ApplyGraphics()
        {
            var size = _resolutions[_resolutionIndex];
            Screen.SetResolution(size.x, size.y, WindowModes[_windowModeIndex].Mode);
            _game.Sfx.PlayClick();
            RefreshGraphicsLabels();
        }

        void RefreshGraphicsLabels()
        {
            if (_resolutionLabel == null) return;
            var size = _resolutions[_resolutionIndex];
            _resolutionLabel.text = $"{size.x} x {size.y}";
            _windowModeLabel.text = WindowModes[_windowModeIndex].Title;
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
