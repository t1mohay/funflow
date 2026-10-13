using System.Collections;
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
        /// <summary>
        /// Доля ширины экрана под каждую боковую панель; спавнер не залезает под них.
        /// Обычно 18,5 %, но на узком экране панели расширяются, чтобы заголовки «НУЖНО» / «БОНУСЫ»
        /// всегда помещались в одну строку (LayoutSidePanels).
        /// </summary>
        public static float SidePanelFraction { get; private set; } = BaseSidePanelFraction;
        const float BaseSidePanelFraction = 0.185f;
        /// <summary>Шире не делаем: игровому полю нужно место.</summary>
        const float MaxSidePanelFraction = 0.3f;

        public static readonly Color TextColor = new Color(1f, 0.96f, 0.9f);
        public static readonly Color GoodColor = new Color(0.56f, 0.93f, 0.45f);
        public static readonly Color BadColor = new Color(1f, 0.4f, 0.35f);
        public static readonly Color AccentColor = new Color(1f, 0.8f, 0.3f);

        // HUD — панели в гамме проекта: тёплый графит в деревянной рамке, строки чуть светлее.
        static readonly Color PanelColor = new Color32(0x3B, 0x34, 0x30, 0xFF);    // тёплый графит
        // Боковые списки полупрозрачные — сквозь них видна кухня; рамка у них непрозрачная.
        static readonly Color SidePanelColor = new Color32(0x3B, 0x34, 0x30, 0xB8); // графит, 72 %
        static readonly Color RowColor = new Color32(0x4C, 0x43, 0x3D, 0xC0);
        static readonly Color RowDoneColor = new Color32(0x45, 0x61, 0x3D, 0xD0);  // глубокий шалфей
        static readonly Color RowWrongColor = new Color32(0x5C, 0x37, 0x31, 0xC0); // приглушённая терракота
        static readonly Color CardColor = new Color(0.27f, 0.18f, 0.13f, 0.96f);
        static readonly Color ButtonColor = new Color(0.36f, 0.62f, 0.27f, 1f);
        static readonly Color SecondaryButtonColor = new Color(0.4f, 0.3f, 0.24f, 1f);

        const float RowHeight = 96f;
        const float RowGap = 10f;
        /// <summary>Высота шапки боковой панели (заголовок) и нижний отступ — как в BuildSidePanel.</summary>
        const float SidePanelTop = 114f;
        const float SidePanelBottom = 28f;
        const float DishBadgeSize = 136f;
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

        sealed class AbilitySlot
        {
            public AbilityDef Def;
            public RectTransform Rect;
            public Image Background;
            public Image Icon;
            public Text Name;
            public Text Hint;
            public GameObject Badge;
            public Text BadgeText;
            public RectTransform Bar;
            public float Punch;
            public float Shake;
        }

        const float SlotHeight = 150f;
        const float SlotGap = 12f;
        static readonly Color SlotEmptyColor = new Color32(0x4C, 0x43, 0x3D, 0x90);

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
        /// <summary>Таблички «Лучший результат» под листами заказов — показываются, когда рекорд уже есть.</summary>
        readonly List<(RecipeDef Recipe, GameObject Tag, Text Score)> _bestTags = new List<(RecipeDef, GameObject, Text)>();
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
        RectTransform _neededList, _abilityList, _floatLayer, _timerRect;
        RectTransform _neededPanel, _abilityPanel;
        /// <summary>Слоты панели «Бонусы» (вместо списка «Не нужно»), по одному на способность.</summary>
        readonly List<AbilitySlot> _slots = new List<AbilitySlot>();
        AbilityState _abilities;
        /// <summary>Лёгкий цветной оттенок экрана, пока действует способность.</summary>
        Image _abilityTint;
        Image _dishBadge;
        GameObject _timerPlate;
        Text _timer, _score, _countdown, _banner;
        Image _flash;
        Color _bannerColor;
        float _bannerAge = 99f, _bannerDuration, _countdownAge = 99f;
        readonly List<Row> _rows = new List<Row>();
        readonly List<Floater> _floaters = new List<Floater>();
        Text _resTitle, _resScore, _resStars, _resDetails, _resVerdict, _resBest;
        Image _resIcon;

        /// <summary>Звук клика для всех кнопок (статические помощники кнопок не видят _game).</summary>
        static System.Action _uiClick;

        public void Init(GameManager game, Camera cam)
        {
            _game = game;
            _uiClick = () => _game.Sfx.PlayClick();
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
            _abilityTint = AddImage(Fill(NewRect("AbilityTint", _hud.transform)), Color.clear, null);
            _abilityTint.transform.SetAsFirstSibling();
            _flash = AddImage(Fill(NewRect("Flash", _root)), Color.clear, null);
            _floatLayer = Fill(NewRect("Floaters", _root));
            // Плашка — только между боковыми панелями, чтобы не залезала на списки.
            _banner = AddText(Stretch(NewRect("Banner", _root), new Vector2(BaseSidePanelFraction, 0.66f), new Vector2(1f - BaseSidePanelFraction, 0.66f),
                    new Vector2(20f, -60f), new Vector2(-20f, 60f)),
                "", 54, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            _countdown = AddText(Place(NewRect("Countdown", _root), new Vector2(0.5f, 0.48f), Vector2.zero, new Vector2(900f, 320f)),
                "", 210, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            // Отсчёт читается на любом фоне: толстая обводка тёплым графитом, деревянная кайма и тень вниз.
            _countdown.GetComponent<Shadow>().enabled = false;
            var countdownOutline = _countdown.gameObject.AddComponent<SmoothOutline>();
            countdownOutline.effectColor = PanelColor;
            countdownOutline.effectDistance = new Vector2(9f, -9f);
            var countdownRim = _countdown.gameObject.AddComponent<SmoothOutline>();
            countdownRim.effectColor = MenuOutline;
            countdownRim.effectDistance = new Vector2(3f, -3f);
            var countdownShadow = _countdown.gameObject.AddComponent<Shadow>();
            countdownShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            countdownShadow.effectDistance = new Vector2(0f, -12f);
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

        public void ShowRound(RoundState round, AbilityState abilities)
        {
            _abilities = abilities;
            foreach (var slot in _slots) FitSingleLine(slot.Name, 34, 18);
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

        /// <summary>
        /// Кнопка паузы на уровне: прячется, когда раунд уже закончен (секунда с баннером перед итогами) —
        /// пауза в это время не ставится, и кнопка нажималась бы впустую.
        /// </summary>
        public void SetPauseAvailable(bool available)
        {
            if (_pauseButton != null) _pauseButton.SetActive(available);
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
            if (round.MultiplierBonus > 0)
                sb.Append($"Очки ×2: +{round.MultiplierBonus}\n");
            if (_abilities != null && _abilities.Used > 0)
                sb.Append($"Способностей применено: {_abilities.Used}\n");
            _resDetails.text = sb.ToString();
            _resDetails.fontSize = 32;

            _resVerdict.text = Verdict(round);
            FitSingleLine(_resVerdict, 36, 20);
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
            FitSingleLine(_resVerdict, 36, 20);
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
            FitSingleLine(_banner, 54, 26);
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
            float dt = UiTime.Delta;
            LayoutMenuBackground();
            LayoutSidePanels();
            if (InputUtil.Listening != null && InputUtil.PollRebind()) RefreshSettingsUI();

            RefreshAbilitySlots(dt);

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

            _neededList = BuildSidePanel(hud, "Нужно", "НУЖНО", GoodColor, 0f, BaseSidePanelFraction, DishBadgeSize * 0.8f);
            _abilityList = BuildSidePanel(hud, "Бонусы", "БОНУСЫ", AccentColor, 1f - BaseSidePanelFraction, 1f, 0f);
            _neededPanel = (RectTransform)_neededList.parent;
            _abilityPanel = (RectTransform)_abilityList.parent;
            BuildAbilitySlots();

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
            Framed((RectTransform)_timerPlate.transform, PanelColor, 6f);
            _timerRect = Place(NewRect("Timer", hud), new Vector2(0.5f, 1f), new Vector2(plateX, -top - 56f), new Vector2(plateWidth - 40f, 100f));
            _timer = AddText(_timerRect, "1:00", 84, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            _score = AddText(Place(NewRect("Score", hud), new Vector2(0.5f, 1f), new Vector2(plateX, -top - plateHeight + 38f), new Vector2(plateWidth - 20f, 50f)),
                "", 34, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);

            // Кнопка паузы справа от плашки.
            var pause = Place(NewRect("PauseButton", hud), new Vector2(0.5f, 1f), new Vector2(pauseX, centerY), new Vector2(pauseSize, pauseSize));
            var pauseFace = Beveled(pause, SecondaryButtonColor, 6f);
            // Без обводки при наведении: кнопка стоит вплотную к плашке таймера, рамка налезала бы на неё.
            MakeButtonOn(pause, pauseFace, _game.Pause, hoverRing: false);
            _pauseButton = pause.gameObject;
            // Значок паузы — две скруглённые полоски (не текст: в рукописном шрифте «II» похоже на «11»).
            for (int side = -1; side <= 1; side += 2)
            {
                var bar = Place(NewRect("Bar", pauseFace.rectTransform), new Vector2(0.5f, 0.5f), new Vector2(side * pauseSize * 0.12f, 0f),
                    new Vector2(pauseSize * 0.16f, pauseSize * 0.5f));
                AddImage(bar, Color.white, Art.RoundedRect).pixelsPerUnitMultiplier = 4f;
            }
        }

        /// <param name="titleRight">Место справа от заголовка (под картинку блюда).</param>
        RectTransform BuildSidePanel(RectTransform parent, string name, string title, Color titleColor, float xMin, float xMax, float titleRight)
        {
            // По вертикали панель по центру экрана; высота задаётся в FitSidePanels по числу строк.
            var panel = Stretch(NewRect(name, parent), new Vector2(xMin, 0.5f), new Vector2(xMax, 0.5f),
                new Vector2(xMin > 0f ? 8f : 16f, -300f), new Vector2(xMin > 0f ? -16f : -8f, 300f));
            FramedClear(panel, SidePanelColor, 6f);
            var label = AddText(Stretch(NewRect("Title", panel), new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -SidePanelTop + 6f), new Vector2(-18f - titleRight, -14f)),
                title, 46, titleColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            // Минимальная ширина панели: заголовок в одну строку + поля + место под картинку блюда + отступы от краёв экрана.
            // preferredWidth — точная ширина шрифтом; запасной расчёт — на случай, если шрифт ещё не построил глифы.
            float titleWidth = Mathf.Max(label.preferredWidth, label.fontSize * 0.8f * title.Length);
            _sidePanelMinWidth = Mathf.Max(_sidePanelMinWidth, titleWidth + 36f + titleRight + 24f + 12f);
            return Stretch(NewRect("List", panel), Vector2.zero, Vector2.one, new Vector2(16f, SidePanelBottom), new Vector2(-16f, -SidePanelTop));
        }

        void BuildRows(RecipeDef recipe)
        {
            foreach (var row in _rows) Destroy(row.Rect.gameObject);
            _rows.Clear();
            for (int i = 0; i < recipe.Needed.Length; i++)
                _rows.Add(MakeRow(_neededList, i, recipe.Needed[i].Ingredient, recipe.Needed[i].Count, false));
            FitSidePanels(recipe.Needed.Length);

            var dish = Art.DishSprite(recipe);
            _dishBadge.sprite = dish;
            _dishBadge.enabled = dish != null;
        }

        float _sidePanelMinWidth;

        /// <summary>
        /// Ширина боковых панелей: не меньше базовой доли экрана и не меньше, чем нужно заголовкам.
        /// Обе панели одной ширины — игровое поле остаётся по центру. Плашка с подсказками — между ними.
        /// </summary>
        void LayoutSidePanels()
        {
            if (_neededPanel == null || _abilityPanel == null || _banner == null) return;
            float width = _root.rect.width;
            if (width <= 1f) return;
            float fraction = Mathf.Clamp(_sidePanelMinWidth / width, BaseSidePanelFraction, MaxSidePanelFraction);
            SidePanelFraction = fraction;   // спавнер (GameManager.UpdateLayout) не залезает под панели
            if (Mathf.Abs(fraction - _neededPanel.anchorMax.x) < 0.0005f) return;
            _neededPanel.anchorMax = new Vector2(fraction, _neededPanel.anchorMax.y);
            _abilityPanel.anchorMin = new Vector2(1f - fraction, _abilityPanel.anchorMin.y);
            var banner = _banner.rectTransform;
            banner.anchorMin = new Vector2(fraction, banner.anchorMin.y);
            banner.anchorMax = new Vector2(1f - fraction, banner.anchorMax.y);
        }

        /// <summary>Панель «Нужно» — по числу строк рецепта, по центру экрана по вертикали.</summary>
        void FitSidePanels(int rows)
        {
            float height = SidePanelTop + rows * (RowHeight + RowGap) - RowGap + SidePanelBottom;
            SetPanelHeight(_neededPanel, height);
        }

        static void SetPanelHeight(RectTransform panel, float height)
        {
            panel.offsetMin = new Vector2(panel.offsetMin.x, -height * 0.5f);
            panel.offsetMax = new Vector2(panel.offsetMax.x, height * 0.5f);
        }

        // ───────────────────────── Панель «Бонусы» ─────────────────────────

        /// <summary>
        /// Три слота способностей: иконка с числом зарядов, название, клавиша и что делает.
        /// Пока способность действует — полоска оставшегося времени. Клик по слоту тоже применяет.
        /// </summary>
        void BuildAbilitySlots()
        {
            var all = Abilities.All;
            for (int i = 0; i < all.Length; i++)
            {
                var def = all[i];
                var rt = NewRect(def.Name, _abilityList);
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(0f, SlotHeight);
                rt.anchoredPosition = new Vector2(0f, -i * (SlotHeight + SlotGap) - SlotHeight * 0.5f);
                var background = AddImage(rt, SlotEmptyColor, Art.RoundedRect);
                var type = def.Type;
                MakeButtonOn(rt, background, () => _game.ActivateAbility(type), click: false, hoverRing: false);

                var iconRect = Place(NewRect("Icon", rt), new Vector2(0f, 0.5f), new Vector2(58f, 6f), new Vector2(92f, 92f));
                var icon = AddImage(iconRect, Color.white, null);
                SetIcon(icon, def.Bonus);

                // Число зарядов — кружок в правом верхнем углу иконки.
                var badge = Place(NewRect("Badge", iconRect), Vector2.one, new Vector2(-4f, -4f), new Vector2(42f, 42f));
                AddImage(badge, def.Color * 0.55f + new Color(0f, 0f, 0f, 0.45f), Art.Circle).type = Image.Type.Simple;
                var badgeText = AddText(Fill(NewRect("Count", badge)), "1", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                badgeText.horizontalOverflow = HorizontalWrapMode.Overflow;

                var name = AddText(Stretch(NewRect("Name", rt), new Vector2(0f, 0.5f), Vector2.one, new Vector2(112f, -4f), new Vector2(-8f, -10f)),
                    def.Name, 34, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                FitSingleLine(name, 34, 18);
                var hint = AddText(Stretch(NewRect("Hint", rt), Vector2.zero, new Vector2(1f, 0.5f), new Vector2(112f, 18f), new Vector2(-8f, 2f)),
                    "", 24, TextColor, TextAnchor.MiddleLeft, FontStyle.Normal);
                hint.horizontalOverflow = HorizontalWrapMode.Overflow;

                // Полоска оставшегося времени — вдоль нижнего края слота.
                var track = Stretch(NewRect("Track", rt), Vector2.zero, new Vector2(1f, 0f), new Vector2(14f, 8f), new Vector2(-14f, 16f));
                AddImage(track, new Color(0f, 0f, 0f, 0.35f), null);
                var bar = Stretch(NewRect("Bar", track), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                AddImage(bar, def.Color, null);

                _slots.Add(new AbilitySlot
                {
                    Def = def, Rect = rt, Background = background, Icon = icon, Name = name, Hint = hint,
                    Badge = badge.gameObject, BadgeText = badgeText, Bar = bar,
                });
            }
            SetPanelHeight(_abilityPanel, SidePanelTop + all.Length * (SlotHeight + SlotGap) - SlotGap + SidePanelBottom);
        }

        /// <summary>Каждый кадр: заряды, активность, полоска времени и подсказка с клавишей.</summary>
        void RefreshAbilitySlots(float dt)
        {
            if (_abilities == null) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(UiTime.Now * 8f);
            bool anyActive = false;
            Color tint = Color.clear;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                var type = slot.Def.Type;
                int charges = _abilities.Charges(type);
                bool active = _abilities.IsActive(type);
                bool ready = charges > 0 && !active;
                string key = InputUtil.KeyName(HotkeyAction.Ability1 + i);

                slot.Background.color = active
                    ? Color.Lerp(SlotEmptyColor, slot.Def.Color, 0.35f + 0.25f * pulse)
                    : ready ? Color.Lerp(RowColor, slot.Def.Color, 0.28f) : SlotEmptyColor;
                slot.Icon.color = active || ready ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.55f);
                slot.Name.color = active || ready ? TextColor : new Color(TextColor.r, TextColor.g, TextColor.b, 0.55f);
                slot.Badge.SetActive(charges > 0);
                slot.BadgeText.text = charges.ToString();
                slot.Hint.text = active ? $"ещё {Mathf.CeilToInt(_abilities.TimeLeft(type))} с"
                    : ready ? $"[{key}] — {slot.Def.Hint}"
                    : $"[{key}] разрежь бонус";
                slot.Hint.color = active ? slot.Def.Color : new Color(TextColor.r, TextColor.g, TextColor.b, ready ? 0.9f : 0.5f);
                slot.Bar.parent.gameObject.SetActive(active);
                if (active) slot.Bar.anchorMax = new Vector2(_abilities.ActiveFill(type), 1f);

                if (active && !anyActive)
                {
                    anyActive = true;
                    tint = slot.Def.Color;
                }

                // Готовый к применению слот слегка «дышит», после событий — отскок и дрожь.
                slot.Punch = Mathf.Max(0f, slot.Punch - dt * 3f);
                slot.Shake = Mathf.Max(0f, slot.Shake - dt * 4f);
                float s = 1f + 0.18f * slot.Punch + (ready ? 0.025f * pulse : 0f);
                slot.Rect.localScale = new Vector3(s, s, 1f);
                float x = slot.Shake > 0f ? Mathf.Sin(UiTime.Now * 70f) * 10f * slot.Shake : 0f;
                slot.Rect.anchoredPosition = new Vector2(x, slot.Rect.anchoredPosition.y);
            }

            tint.a = anyActive ? 0.07f + 0.03f * pulse : 0f;
            _abilityTint.color = tint;
        }

        /// <summary>Разрезан бонус — его слот подпрыгивает.</summary>
        public void OnBonus(AbilityType type)
        {
            if (type != AbilityType.None) _slots[(int)type].Punch = 1f;
        }

        public void OnAbilityUsed(AbilityType type)
        {
            _slots[(int)type].Punch = 1f;
            Flash(Abilities.Of(type).Color);
        }

        /// <summary>Нечего применять — слот дрожит.</summary>
        public void OnAbilityDenied(AbilityType type) => _slots[(int)type].Shake = 1f;

        Row MakeRow(RectTransform list, int index, IngredientDef def, int need, bool wrong)
        {
            var rt = NewRect(def.Name, list);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0f, RowHeight);
            rt.anchoredPosition = new Vector2(0f, -index * (RowHeight + RowGap) - RowHeight * 0.5f);
            var background = AddImage(rt, wrong ? RowWrongColor : RowColor, Art.RoundedRect);

            var icon = Place(NewRect("Icon", rt), new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(70f, 70f));
            SetIcon(AddImage(icon, Color.white, null), def);

            // Высота поля — одна строка: при нехватке места название уменьшается, а не переносится.
            var name = AddText(Stretch(NewRect("Name", rt), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(86f, -30f), new Vector2(wrong ? -8f : -108f, 30f)),
                def.Name, 42, TextColor, TextAnchor.MiddleLeft, FontStyle.Normal);
            FitSingleLine(name, 42, 18);

            // У лишних только иконка и название — без счётчика.
            Text count = null;
            if (!wrong)
            {
                // Шире и без переноса: «10/12» не должно перескакивать на вторую строку.
                count = AddText(Stretch(NewRect("Count", rt), new Vector2(1f, 0f), Vector2.one, new Vector2(-112f, 0f), new Vector2(-10f, 0f)),
                    $"0/{need}", 42, TextColor, TextAnchor.MiddleRight, FontStyle.Bold);
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
        RectTransform _menuBackground, _dishBoard;
        GameObject _menuSwirl;
        Image _menuBackgroundImage;
        Sprite _kitchenSprite, _wallSprite;
        /// <summary>Качающиеся предметы стены выбора заказа (табличка, бокалы, плющ) — поверх картинки стены.</summary>
        GameObject _wallLayers;
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
            _menuBackgroundImage = bg;
            _kitchenSprite = background;
            // Для выбора заказа — своя стена кухни со стеллажом, рамками и табличкой «Выбор блюда»
            // (Tools/ArtGen/kitchen_wall). Пропорции у неё свои — LayoutMenuBackground берёт их из текущего спрайта.
            _wallSprite = Art.LoadSprite("Kitchen/wall");
            if (_wallSprite != null) BuildWallLayers();
            if (background == null) bg.color = CardColor;
            else _menuBackgroundAspect = background.rect.width / background.rect.height;

            // Текучий фон в духе Balatro (шейдер Resources/Shaders/MenuSwirl). Если шейдер не поддерживается — остаётся кухня.
            var swirl = SwirlMaterial();
            if (swirl != null)
            {
                var fluid = AddImage(Fill(NewRect("Swirl", menu)), Color.white, null);
                fluid.material = swirl;
                _menuSwirl = fluid.gameObject;
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

            // Доска заказов привязана к фону кухни (LayoutMenuBackground): висит на стене
            // и масштабируется вместе с фоном. Ноль по вертикали — ось перекладины.
            _dishBoard = NewRect("Board", dishes);
            _dishBoard.anchorMin = _dishBoard.anchorMax = new Vector2(0.5f, 1f);
            _dishBoard.pivot = new Vector2(0.5f, 0.5f);
            _dishBoard.sizeDelta = Vector2.zero;

            OrderRail(_dishBoard);
            // Заголовок теперь нарисован на самой стене — табличка «Выбор блюда» под стеллажом.

            var all = Recipes.All;
            for (int i = 0; i < all.Length; i++)
                OrderCard(_dishBoard, all[i], (i - (all.Length - 1) * 0.5f) * OrderSpacing, i);

            SmallButton(dishes, new Vector2(150f, 90f), new Vector2(240f, 90f), "← Назад", () => ShowDishSelect(false), Vector2.zero);
        }

        // Размеры доски заказов — в единицах фона шириной OrderBoardWidth (фон 16:9 с запасом 4%).
        const float OrderBoardWidth = 1920f * 1.04f;
        /// <summary>
        /// Высота оси перекладины на картинке фона, доля от её высоты: под бокалами и табличкой стеллажа.
        /// Верх стены обрезан по полке, поэтому листы висят на той же высоте экрана, что и раньше (~190 px из 1080).
        /// </summary>
        const float OrderRailHeight = 0.179f;
        /// <summary>Во сколько раз доска крупнее своих размеров ниже: листы помещаются между рамками на стене.</summary>
        const float OrderBoardScale = 0.98f;
        const float OrderSpacing = 400f;
        static readonly Vector2 OrderCardSize = new Vector2(300f, 340f);
        const float OrderCardTop = -28f;

        // Перекладина и крепления — тёмный металл, как у стеллажа на фоне (спрайты UI/order_*.png,
        // Tools/ArtGen/kitchen_wall/gen_order_rack.py). Фон на этом экране не затемняется — и они тоже.
        static readonly Color RailTint = Color.white;
        // Запасной вариант без спрайтов — деревянная перекладина.
        static readonly Color RodColor = new Color32(0x6C, 0x3D, 0x1B, 0xFF);
        static readonly Color RodDark = new Color32(0x4D, 0x26, 0x0E, 0xFF);
        static readonly Color RodLight = new Color32(0x86, 0x55, 0x2C, 0xFF);
        static readonly Color BracketColor = new Color32(0x35, 0x2B, 0x26, 0xFF);
        static readonly Color BracketLight = new Color32(0x55, 0x4A, 0x44, 0xFF);
        // Прищепки — светлый металл.
        static readonly Color ClipColor = new Color32(0x5C, 0x5C, 0x63, 0xFF);
        static readonly Color ClipDark = new Color32(0x2E, 0x2E, 0x33, 0xFF);
        static readonly Color ClipLight = new Color32(0x9A, 0x9A, 0xA3, 0xFF);
        static readonly Color PaperColor = new Color32(0xFC, 0xF9, 0xF2, 0xFF);
        static readonly Color PaperEdge = new Color32(0xE4, 0xDC, 0xCB, 0xFF);

        /// <summary>
        /// Деревянная перекладина во всю доску, прикрученная к стене: за ней — железные пластины на шурупах,
        /// перекладина проходит сквозь держатели, на плитку падает тень.
        /// </summary>
        static void OrderRail(RectTransform board)
        {
            if (OrderRailSprites(board)) return;
            const float halfLength = 620f, thickness = 22f;
            var center = new Vector2(0.5f, 0.5f);
            // Крайние пластины — за крайними листами, средние — посередине промежутков между листами.
            var mounts = new[] { -595f, -OrderSpacing * 0.5f, OrderSpacing * 0.5f, 595f };
            var plateSize = new Vector2(36f, 86f);
            const float plateY = -8f;

            // Тени на стене: мягкая полоса под перекладиной и под каждой пластиной.
            Rounded(Place(NewRect("RodShadowSoft", board), center, new Vector2(4f, -15f), new Vector2(halfLength * 2f + 10f, 30f)),
                new Color(0f, 0f, 0f, 0.12f), 15f);
            Rounded(Place(NewRect("RodShadow", board), center, new Vector2(3f, -12f), new Vector2(halfLength * 2f, 18f)),
                new Color(0f, 0f, 0f, 0.22f), 9f);
            foreach (float x in mounts)
                Rounded(Place(NewRect("PlateShadow", board), center, new Vector2(x + 5f, plateY - 7f), plateSize + new Vector2(6f, 4f)),
                    new Color(0f, 0f, 0f, 0.3f), 9f);

            // Пластины на стене — позади перекладины, по шурупу сверху и снизу.
            foreach (float x in mounts)
            {
                var plate = Place(NewRect("Plate", board), center, new Vector2(x, plateY), plateSize);
                Rounded(plate, ClipDark, 7f);
                var face = Rounded(Stretch(NewRect("Face", plate), Vector2.zero, Vector2.one, new Vector2(2f, 3f), new Vector2(-2f, -2f)), BracketColor, 6f);
                Rounded(Stretch(NewRect("Shine", face.rectTransform), new Vector2(0f, 1f), Vector2.one, new Vector2(4f, -5f), new Vector2(-4f, -2f)),
                    new Color(1f, 1f, 1f, 0.12f), 2f);
                foreach (float y in new[] { 30f, -30f })
                {
                    var screw = Place(NewRect("Screw", plate), center, new Vector2(0f, y), new Vector2(12f, 12f));
                    AddImage(screw, ClipDark, Art.Circle).type = Image.Type.Simple;
                    AddImage(Stretch(NewRect("Head", screw), Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f)), BracketLight, Art.Circle)
                        .type = Image.Type.Simple;
                }
            }

            // Сама перекладина с набалдашниками на концах.
            for (int side = -1; side <= 1; side += 2)
            {
                var knob = Place(NewRect("Knob", board), center, new Vector2(side * halfLength, 0f), new Vector2(30f, 30f));
                AddImage(knob, RodDark, Art.Circle).type = Image.Type.Simple;
                AddImage(Stretch(NewRect("Face", knob), Vector2.zero, Vector2.one, new Vector2(3f, 4f), new Vector2(-3f, -2f)), RodColor, Art.Circle)
                    .type = Image.Type.Simple;
                AddImage(Place(NewRect("Shine", knob), center, new Vector2(-3f, 5f), new Vector2(9f, 7f)), RodLight, Art.Circle).type = Image.Type.Simple;
            }
            var rod = Place(NewRect("Rod", board), center, Vector2.zero, new Vector2(halfLength * 2f - 20f, thickness));
            Rounded(rod, RodDark, thickness * 0.5f);
            Rounded(Stretch(NewRect("Face", rod), Vector2.zero, Vector2.one, new Vector2(2f, 4f), new Vector2(-2f, -1f)), RodColor, thickness * 0.4f);
            Rounded(Stretch(NewRect("Shine", rod), new Vector2(0f, 0.55f), Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, -4f)), RodLight, 3f);

            // Держатели: железное кольцо вокруг перекладины на каждой пластине.
            foreach (float x in mounts)
            {
                var holder = Place(NewRect("Holder", board), center, new Vector2(x, 0f), new Vector2(26f, thickness + 12f));
                Rounded(holder, ClipDark, 6f);
                var face = Rounded(Stretch(NewRect("Face", holder), Vector2.zero, Vector2.one, new Vector2(2f, 3f), new Vector2(-2f, -2f)), BracketColor, 5f);
                Rounded(Stretch(NewRect("Shine", face.rectTransform), new Vector2(0f, 0.6f), Vector2.one, new Vector2(3f, 0f), new Vector2(-3f, -2f)),
                    BracketLight, 2f);
            }
        }

        /// <summary>
        /// Металлическая перекладина в стиле стеллажа на фоне: пластины на шурупах за ней, хомуты поверх,
        /// шарики на концах, мягкая тень на плитке. Возвращает false, если спрайтов нет.
        /// </summary>
        static bool OrderRailSprites(RectTransform board)
        {
            var rod = Art.LoadSprite("UI/order_rod");
            var end = Art.LoadSprite("UI/order_rod_end");
            var mount = Art.LoadSprite("UI/order_mount");
            var saddle = Art.LoadSprite("UI/order_saddle");
            if (rod == null || end == null || mount == null || saddle == null) return false;

            const float halfLength = 520f, thickness = 15f;
            var center = new Vector2(0.5f, 0.5f);
            var mounts = new[] { -500f, -OrderSpacing * 0.5f, OrderSpacing * 0.5f, 500f };
            var mountSize = new Vector2(26f, 54f);

            Image PlaceSprite(string name, Sprite sprite, Vector2 position, Vector2 size)
            {
                var image = AddImage(Place(NewRect(name, board), center, position, size), RailTint, null);
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.raycastTarget = false;
                return image;
            }

            // Тени на стене (свет слева сверху, как на фоне).
            Rounded(Place(NewRect("RodShadow", board), center, new Vector2(5f, -8f), new Vector2(halfLength * 2f + 10f, 16f)),
                new Color(0f, 0f, 0f, 0.2f), 8f);
            foreach (float x in mounts)
                Rounded(Place(NewRect("MountShadow", board), center, new Vector2(x + 4f, -5f), mountSize + new Vector2(4f, 4f)),
                    new Color(0f, 0f, 0f, 0.25f), 8f);

            foreach (float x in mounts) PlaceSprite("Mount", mount, new Vector2(x, 0f), mountSize);
            PlaceSprite("Rod", rod, Vector2.zero, new Vector2(halfLength * 2f - 16f, thickness));
            for (int side = -1; side <= 1; side += 2) PlaceSprite("RodEnd", end, new Vector2(side * halfLength, 0f), new Vector2(24f, 24f));
            foreach (float x in mounts) PlaceSprite("Saddle", saddle, new Vector2(x, 0f), new Vector2(20f, 24f));
            return true;
        }

        /// <summary>
        /// Заказ: лист с картинкой блюда на прищепке и кнопка «Выбрать» под ним.
        /// Лист, прищепка, тень и кнопка висят на общем «подвесе», который качается вокруг кольца
        /// на перекладине (OrderCardAnim). И кнопка, и сам лист открывают лист рецепта (OpenRecipeSheet).
        /// </summary>
        void OrderCard(RectTransform board, RecipeDef recipe, float x, int index)
        {
            var center = new Vector2(0.5f, 0.5f);
            UnityAction open = () => OpenRecipeSheet(recipe);   // звук бумаги играет сам лист

            // Подвес с точкой вращения в кольце на перекладине.
            var hanger = Place(NewRect(recipe.Title + " (подвес)", board), center, new Vector2(x, 0f), Vector2.zero);

            // Лист с тенью — отдельной группой: когда лист рецепта «снимают» с доски, она прячется,
            // а прищепка и кнопка остаются на месте (OpenRecipeSheet / CloseRecipeSheet).
            var paperGroup = Place(NewRect("Paper", hanger), center, Vector2.zero, Vector2.zero);
            var paperFade = paperGroup.gameObject.AddComponent<CanvasGroup>();
            var cardPosition = new Vector2(0f, OrderCardTop - OrderCardSize.y * 0.5f);
            DropShadow(paperGroup, center, cardPosition, OrderCardSize, 20f);
            var card = Place(NewRect(recipe.Title, paperGroup), center, cardPosition, OrderCardSize);
            _orderPapers[recipe] = (card, paperFade);
            Image fill;
            var paper = Art.LoadSprite("UI/order_paper");
            if (paper != null)
            {
                // Лист нарисован в стиле блюд: мягкий контур, кремовый градиент, загнутый уголок.
                fill = AddImage(card, Color.white, null);
                fill.sprite = paper;
                fill.type = Image.Type.Simple;
            }
            else
            {
                fill = Framed(card, PaperEdge, 12f);
                Rounded(Stretch(NewRect("Paper", fill.rectTransform), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)), PaperColor, 6f);
            }
            MakeButtonOn(card, fill, open, click: false);

            // Картинка блюда — по центру листа, чуть ниже, чтобы её не закрывал зажим.
            RectTransform dishRect = null;
            var dish = Art.DishSprite(recipe);
            if (dish != null)
            {
                dishRect = Place(NewRect("Dish", card), center, new Vector2(0f, -10f), new Vector2(250f, 250f));
                var image = AddImage(dishRect, Color.white, dish);
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }

            // Прищепка: ножка уходит в кольцо на перекладине, зажим держит верх листа.
            var clip = Art.LoadSprite("UI/order_clip");
            if (clip != null)
            {
                var clipImage = AddImage(Place(NewRect("Clip", hanger), center, new Vector2(0f, -26f), new Vector2(100f, 68f)), Color.white, null);
                clipImage.sprite = clip;
                clipImage.type = Image.Type.Simple;
            }
            else
            {
                var stem = Place(NewRect("ClipStem", hanger), center, new Vector2(0f, -22f), new Vector2(20f, 48f));
                Rounded(stem, ClipDark, 4f);
                Rounded(Stretch(NewRect("Face", stem), Vector2.zero, Vector2.one, new Vector2(3f, 0f), new Vector2(-3f, 0f)), ClipColor, 3f);
                var clamp = Place(NewRect("Clamp", hanger), center, new Vector2(0f, OrderCardTop - 6f), new Vector2(96f, 46f));
                Rounded(clamp, ClipDark, 10f);
                var clampFace = Rounded(Stretch(NewRect("Face", clamp), Vector2.zero, Vector2.one, new Vector2(3f, 5f), new Vector2(-3f, -3f)), ClipColor, 8f);
                Rounded(Stretch(NewRect("Shine", clampFace.rectTransform), new Vector2(0f, 0.55f), Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, -3f)),
                    new Color(1f, 1f, 1f, 0.3f), 4f);
            }

            // Кнопка чуть заходит на низ листа, как на макете.
            var buttonPosition = new Vector2(0f, OrderCardTop - OrderCardSize.y - 30f);
            var buttonSize = new Vector2(260f, 80f);
            DropShadow(hanger, center, buttonPosition, buttonSize);
            var button = Place(NewRect("Choose", hanger), center, buttonPosition, buttonSize);
            var face = Beveled(button, MenuGreen, 6f);
            MakeButtonOn(button, face, open, click: false);
            MenuText(Fill(NewRect("Label", face.rectTransform)), "Выбрать ▶", 36, MenuLightText, TextAnchor.MiddleCenter, FontStyle.Bold)
                .GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.35f);

            BestTag(hanger, recipe, buttonPosition.y - buttonSize.y * 0.5f);

            // Кольцо на перекладине не качается — рисуется поверх ножки прищепки.
            var ringSprite = Art.LoadSprite("UI/order_ring");
            if (ringSprite != null)
            {
                var ringImage = AddImage(Place(NewRect("ClipRing", board), center, new Vector2(x, 0f), new Vector2(30f, 30f)), Color.white, null);
                ringImage.sprite = ringSprite;
                ringImage.type = Image.Type.Simple;
                ringImage.raycastTarget = false;
            }
            else
            {
                var ring = Place(NewRect("ClipRing", board), center, new Vector2(x, 0f), new Vector2(42f, 42f));
                AddImage(ring, ClipDark, Art.Circle).type = Image.Type.Simple;
                AddImage(Stretch(NewRect("Face", ring), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)), ClipColor, Art.Circle)
                    .type = Image.Type.Simple;
                AddImage(Place(NewRect("Rivet", ring), center, new Vector2(0f, 2f), new Vector2(14f, 14f)), ClipLight, Art.Circle).type = Image.Type.Simple;
            }

            // Покачивание, «подлёт» при открытии экрана и отклик на наведение.
            hanger.gameObject.AddComponent<OrderCardAnim>().Init(dishRect, index);
        }

        /// <summary>
        /// Табличка «Лучший результат» на двух шнурках под кнопкой «Выбрать». Висит на том же подвесе,
        /// что и лист (качается вместе с ним), и сама чуть покачивается. Видна, только когда рекорд уже есть.
        /// </summary>
        void BestTag(RectTransform hanger, RecipeDef recipe, float top)
        {
            const float cord = 14f;
            var tagSize = new Vector2(210f, 66f);
            // Точка подвеса — середина низа кнопки: вокруг неё табличка и качается.
            var holder = Place(NewRect("BestTag", hanger), new Vector2(0.5f, 0.5f), new Vector2(0f, top), new Vector2(tagSize.x, cord + tagSize.y));
            holder.pivot = new Vector2(0.5f, 1f);
            holder.anchoredPosition = new Vector2(0f, top);

            for (int side = -1; side <= 1; side += 2)
                Rounded(Place(NewRect("Cord", holder), new Vector2(0.5f, 1f), new Vector2(side * tagSize.x * 0.3f, -cord * 0.5f - 2f), new Vector2(3f, cord + 6f)),
                    MenuBrownText, 1.5f);
            var tagPosition = new Vector2(0f, -cord - tagSize.y * 0.5f);
            DropShadow(holder, new Vector2(0.5f, 1f), tagPosition, tagSize, 12f);
            var tag = Place(NewRect("Tag", holder), new Vector2(0.5f, 1f), tagPosition, tagSize);
            var face = Framed(tag, MenuCream, 5f, 14f);
            var title = MenuText(Stretch(NewRect("Title", face.rectTransform), new Vector2(0f, 0.5f), Vector2.one, new Vector2(8f, -2f), new Vector2(-8f, -3f)),
                "Лучший результат", 20, MenuBrownText, TextAnchor.MiddleCenter, FontStyle.Bold);
            FitSingleLine(title, 20, 14);
            var score = MenuText(Stretch(NewRect("Score", face.rectTransform), Vector2.zero, new Vector2(1f, 0.5f), new Vector2(8f, 2f), new Vector2(-8f, 2f)),
                "", 28, MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);

            var sway = holder.gameObject.AddComponent<WallSway>();
            sway.Amplitude = 1.5f;
            sway.Period = 3.4f;
            sway.Phase = _bestTags.Count * 0.37f;
            _bestTags.Add((recipe, holder.gameObject, score));
            holder.gameObject.SetActive(false);
        }

        /// <summary>Рекорды могли смениться после раунда — обновляем таблички при каждом открытии выбора заказа.</summary>
        void RefreshBestTags()
        {
            foreach (var (recipe, tag, score) in _bestTags)
            {
                int best = _game.BestScore(recipe);
                tag.SetActive(best >= 0);
                if (best >= 0) score.text = $"{best}/100";
            }
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
            title.font = Art.DefaultFont; // логотип — стандартным шрифтом, остальной интерфейс — Fonts/Main
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
        void SmallButton(RectTransform parent, Vector2 position, Vector2 size, string text, UnityAction onClick, Vector2? anchor = null, bool click = true)
        {
            var at = anchor ?? new Vector2(0.5f, 0.5f);
            DropShadow(parent, at, position, size);
            var rt = Place(NewRect(text, parent), at, position, size);
            var face = Beveled(rt, MenuCream, 6f);
            MakeButtonOn(rt, face, onClick, click);
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
            CloseRecipeSheet(silent: true);
            _dishScreen.SetActive(visible);
            _mainScreen.SetActive(!visible);
            if (visible) RefreshBestTags();
            // Выбор заказа — на кухне (одна стена из плитки и стол); главное меню — на текучем фоне.
            if (_wallLayers != null) _wallLayers.SetActive(visible);
            if (_kitchenSprite != null && _wallSprite != null)
            {
                _menuBackgroundImage.sprite = visible ? _wallSprite : _kitchenSprite;
                // Стена выбора заказа — без затемнения: на ней табличка «Выбор блюда» и детали кухни.
                _menuBackgroundImage.color = visible ? Color.white : MenuTint;
            }
            if (_menuSwirl != null)
            {
                _menuSwirl.SetActive(!visible);
                _menuBackground.gameObject.SetActive(visible);
            }
        }

        void ShowCredits(bool visible) => _creditsScreen.SetActive(visible);

        [System.Serializable]
        sealed class WallLayerInfo
        {
            public string name;
            public float x, y, w, h, pivotX, pivotY, amp, period, phase;
        }

        [System.Serializable]
        sealed class WallLayerList
        {
            public float width, height;
            public WallLayerInfo[] layers;
        }

        /// <summary>
        /// Табличка «Выбор блюда», бокалы и плети плюща нарисованы генератором стены отдельными картинками
        /// (Kitchen/WallLayers, места — Kitchen/wall_layers.json). Кладём их поверх стены в тех же местах,
        /// в пикселях картинки стены, и покачиваем вокруг точки подвеса (WallSway).
        /// </summary>
        void BuildWallLayers()
        {
            var json = Resources.Load<TextAsset>("Kitchen/wall_layers");
            if (json == null) return;
            var list = JsonUtility.FromJson<WallLayerList>(json.text);
            if (list?.layers == null || list.width <= 0f || list.height <= 0f) return;

            var container = Fill(NewRect("WallLayers", _menuBackground));
            _wallLayers = container.gameObject;
            foreach (var info in list.layers)
            {
                var sprite = Art.LoadSprite("Kitchen/WallLayers/" + info.name);
                if (sprite == null || info.w <= 0f || info.h <= 0f) continue;
                var rt = NewRect(info.name, container);
                // Якоря — прямоугольник слоя в долях стены (y картинки идёт сверху вниз), точка вращения — подвес.
                rt.anchorMin = new Vector2(info.x / list.width, 1f - (info.y + info.h) / list.height);
                rt.anchorMax = new Vector2((info.x + info.w) / list.width, 1f - info.y / list.height);
                rt.pivot = new Vector2((info.pivotX - info.x) / info.w, 1f - (info.pivotY - info.y) / info.h);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var image = AddImage(rt, Color.white, null);
                image.sprite = sprite;
                var sway = rt.gameObject.AddComponent<WallSway>();
                sway.Amplitude = info.amp;
                sway.Period = info.period;
                sway.Phase = info.phase;
            }
            _wallLayers.SetActive(false);
        }

        /// <summary>Esc в меню: закрыть верхний экран. Возвращает true, если что-то закрылось.</summary>
        public bool MenuBack()
        {
            if (CloseSettings()) return true;
            if (_creditsScreen.activeSelf) { ShowCredits(false); return true; }
            if (RecipeSheetOpen) { CloseRecipeSheet(); return true; }
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
            // Пропорции — у текущей картинки: у стены выбора заказа они свои (шире, 16:9).
            var sprite = _menuBackgroundImage != null ? _menuBackgroundImage.sprite : null;
            float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : _menuBackgroundAspect;
            float w = Mathf.Max(size.x, size.y * aspect) * 1.04f;
            _menuBackground.sizeDelta = new Vector2(w, w / aspect);
            _menuBackground.anchoredPosition = new Vector2(0f, size.y * 0.01f);
            // Доска заказов: ось перекладины — на нарисованной перекладине, масштаб — как у фона.
            _dishBoard.anchoredPosition = new Vector2(0f, size.y * 0.01f - _menuBackground.sizeDelta.y * OrderRailHeight);
            _dishBoard.localScale = Vector3.one * (w / OrderBoardWidth * OrderBoardScale);
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

        // ───────────────────────── Лист рецепта ─────────────────────────

        // Правило игры — цитатой «великого философа»: что делать — зелёным, чего не делать — красным.
        const string RulesIntro = "Правила просты, их завещал великий философ:";
        const string RulesQuote = "«<color=#3F7F1F>Как надо — делай</color>, <color=#B3362A>как не надо — не делай</color>»";
        const string RulesAuthor = "— Великий философ";
        static readonly Color QuoteBack = new Color32(0xEF, 0xE4, 0xCC, 0xFF);
        static readonly Vector2 RecipeSheetSize = new Vector2(1000f, 1000f);

        GameObject _recipeSheet;
        RecipeDef _sheetRecipe;
        RectTransform _sheetHolder;
        Image _sheetDim;
        /// <summary>Листы на доске заказов: откуда «достаётся» лист рецепта и куда возвращается.</summary>
        readonly Dictionary<RecipeDef, (RectTransform Card, CanvasGroup Group)> _orderPapers =
            new Dictionary<RecipeDef, (RectTransform, CanvasGroup)>();
        // Лист, который сейчас улетает обратно на доску.
        GameObject _closingSheet;
        Coroutine _closingRoutine, _openingRoutine;
        const float SheetDim = 0.55f;

        /// <summary>Открыт лист рецепта на экране выбора заказа.</summary>
        public bool RecipeSheetOpen => _recipeSheet != null;

        /// <summary>
        /// Лист рецепта поверх доски заказов: название, картинка и история блюда, нужные ингредиенты,
        /// правило-шутка и кнопки «Начать готовку» / «Назад». Строится заново при каждом открытии.
        /// </summary>
        void OpenRecipeSheet(RecipeDef recipe)
        {
            CloseRecipeSheet(silent: true);
            FinishClosing();
            _game.Sfx.PlayPaper();
            _sheetRecipe = recipe;
            var center = new Vector2(0.5f, 0.5f);

            var overlay = Fill(NewRect("RecipeSheet", (RectTransform)_dishScreen.transform));
            _recipeSheet = overlay.gameObject;
            // Затемнение ловит клики: нажатие мимо листа закрывает его. Лист — не его потомок,
            // иначе клик по листу тоже дошёл бы до этой кнопки.
            var dimRect = Fill(NewRect("Dim", overlay));
            _sheetDim = AddImage(dimRect, new Color(0f, 0f, 0f, SheetDim), null);
            _sheetDim.raycastTarget = true;
            dimRect.gameObject.AddComponent<Button>().onClick.AddListener(() => CloseRecipeSheet());   // звук бумаги

            var holder = Place(NewRect("Holder", overlay), center, new Vector2(0f, -10f), RecipeSheetSize);
            _sheetHolder = holder;
            var paperSprite = Art.LoadSprite("UI/recipe_paper");
            PaperSheet paperShadow = null;
            if (paperSprite != null)
            {
                // Тень той же формы, что лист: под загнутым уголком её нет, там видно только тень от самого уголка.
                paperShadow = Place(NewRect("Shadow", holder), center, new Vector2(0f, -12f), RecipeSheetSize + new Vector2(6f, 4f))
                    .gameObject.AddComponent<PaperSheet>();
                paperShadow.Texture = paperSprite.texture;   // берём только альфу: скруглённые углы как у листа
                paperShadow.color = new Color(0f, 0f, 0f, 0.35f);
                paperShadow.raycastTarget = false;
            }
            else DropShadow(holder, center, Vector2.zero, RecipeSheetSize);
            var sheet = Place(NewRect("Sheet", holder), center, Vector2.zero, RecipeSheetSize);
            RectTransform paper;
            if (paperSprite != null)
            {
                // Бумага как у листов заказа (Tools/ArtGen/gen_recipe_paper.py), угол загнут и медленно «дышит».
                var paperGraphic = sheet.gameObject.AddComponent<PaperSheet>();
                paperGraphic.Texture = paperSprite.texture;
                paperGraphic.raycastTarget = true; // клики по самому листу не закрывают его
                var curl = Fill(NewRect("Curl", sheet)).gameObject.AddComponent<PaperCurl>();
                curl.Sheet = paperGraphic;
                curl.Shadow = paperShadow;
                paper = Stretch(NewRect("Paper", sheet), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            }
            else
            {
                var edge = Framed(sheet, PaperEdge, 14f);
                edge.raycastTarget = true;
                paper = Rounded(Stretch(NewRect("Paper", edge.rectTransform), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)),
                    PaperColor, 8f).rectTransform;
            }

            // Зажим сверху — тот же, что держит листы на доске (без крючка), только крупнее: шарнир над краем, губки на листе.
            var clipSprite = Art.LoadSprite("UI/recipe_clip");
            if (clipSprite != null)
            {
                var clipImage = AddImage(Place(NewRect("Clip", holder), new Vector2(0.5f, 1f), new Vector2(0f, -9f), new Vector2(180f, 72f)), Color.white, null);
                clipImage.sprite = clipSprite;
                clipImage.type = Image.Type.Simple;
                clipImage.raycastTarget = false;
            }
            else
            {
                var clamp = Place(NewRect("Clamp", holder), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(170f, 64f));
                Rounded(clamp, ClipDark, 14f);
                var clampFace = Rounded(Stretch(NewRect("Face", clamp), Vector2.zero, Vector2.one, new Vector2(4f, 7f), new Vector2(-4f, -4f)), ClipColor, 11f);
                Rounded(Stretch(NewRect("Shine", clampFace.rectTransform), new Vector2(0f, 0.55f), Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, -4f)),
                    new Color(1f, 1f, 1f, 0.3f), 5f);
            }

            // ── верх: название и история слева, картинка справа
            var title = MenuText(TopLeft(paper, 60f, 48f, 330f, 84f), recipe.Title, 68, MenuGreenText, TextAnchor.MiddleLeft, FontStyle.Bold);
            FitSingleLine(title, 68, 36);
            var lore = MenuText(TopLeft(paper, 60f, 140f, 330f, 222f), recipe.Lore ?? recipe.Description, 30, MenuBrownText,
                TextAnchor.UpperLeft, FontStyle.Normal);
            lore.resizeTextForBestFit = true;
            lore.resizeTextMinSize = 20;
            lore.resizeTextMaxSize = 30;
            lore.lineSpacing = 1.05f;

            var dish = Art.DishSprite(recipe);
            if (dish != null)
            {
                var image = AddImage(Place(NewRect("Dish", paper), new Vector2(1f, 1f), new Vector2(-180f, -185f), new Vector2(250f, 250f)), Color.white, dish);
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }

            // Разделитель — тонкая линия, как сгиб бумаги.
            Rounded(TopLeft(paper, 50f, 370f, 50f, 4f), PaperEdge, 2f);

            // ── ингредиенты: в две колонки, с картинкой и количеством
            MenuText(TopLeft(paper, 60f, 380f, 60f, 54f), "Понадобится:", 38, MenuGreenText, TextAnchor.MiddleLeft, FontStyle.Bold);
            const float rowTop = 438f, rowHeight = 56f;
            int perColumn = (recipe.Needed.Length + 1) / 2;
            for (int i = 0; i < recipe.Needed.Length; i++)
            {
                var item = recipe.Needed[i];
                int column = i / perColumn, row = i % perColumn;
                var line = Stretch(NewRect(item.Ingredient.Name, paper), new Vector2(column * 0.5f, 1f), new Vector2(column * 0.5f + 0.5f, 1f),
                    new Vector2(column == 0 ? 70f : 30f, -rowTop - (row + 1) * rowHeight), new Vector2(column == 0 ? -30f : -70f, -rowTop - row * rowHeight));
                var icon = Place(NewRect("Icon", line), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(56f, 56f));
                var sprite = Art.IngredientSprite(item.Ingredient);
                var iconImage = AddImage(icon, sprite != null ? Color.white : item.Ingredient.Color, sprite != null ? sprite : Art.Circle);
                iconImage.type = Image.Type.Simple;
                iconImage.preserveAspect = true;
                var name = MenuText(Stretch(NewRect("Name", line), Vector2.zero, Vector2.one, new Vector2(70f, 0f), new Vector2(-90f, 0f)),
                    item.Ingredient.Name, 34, MenuBrownText, TextAnchor.MiddleLeft, FontStyle.Bold);
                FitSingleLine(name, 34, 20);
                MenuText(Stretch(NewRect("Count", line), new Vector2(1f, 0f), Vector2.one, new Vector2(-90f, 0f), Vector2.zero),
                    $"× {item.Count}", 34, MenuGreenText, TextAnchor.MiddleRight, FontStyle.Bold);
            }

            // ── правило-шутка: цитата великого философа на светлой подложке с зелёной полоской
            var quote = TopLeft(paper, 50f, 676f, 50f, 140f);
            Rounded(quote, QuoteBack, 18f);
            Rounded(Stretch(NewRect("Bar", quote), Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 12f), new Vector2(8f, -12f)), MenuGreen, 4f);
            MenuText(Stretch(NewRect("Intro", quote), new Vector2(0f, 1f), Vector2.one, new Vector2(36f, -46f), new Vector2(-24f, -10f)),
                RulesIntro, 26, MenuBrownText, TextAnchor.MiddleLeft, FontStyle.Italic);
            var words = MenuText(Stretch(NewRect("Quote", quote), new Vector2(0f, 1f), Vector2.one, new Vector2(36f, -104f), new Vector2(-24f, -48f)),
                RulesQuote, 38, MenuBrownText, TextAnchor.MiddleLeft, FontStyle.Bold);
            words.supportRichText = true;
            FitSingleLine(words, 38, 24);
            MenuText(Stretch(NewRect("Author", quote), Vector2.zero, new Vector2(1f, 0f), new Vector2(36f, 6f), new Vector2(-28f, 40f)),
                RulesAuthor, 24, MenuGreenText, TextAnchor.MiddleRight, FontStyle.Italic);

            // ── кнопки
            SmallButton(holder, new Vector2(-270f, -RecipeSheetSize.y * 0.5f + 85f), new Vector2(280f, 96f), "← Назад", () => CloseRecipeSheet(), click: false);
            MenuButton(holder, new Vector2(150f, -RecipeSheetSize.y * 0.5f + 85f), "Начать готовку ▶", true,
                () => _game.StartRound(recipe), new Vector2(500f, 110f));

            _openingRoutine = StartCoroutine(PullOut(overlay, holder, _sheetDim, recipe));
        }

        /// <param name="silent">Без звука бумаги — когда лист убирается вместе с экраном или сразу открывается другой.</param>
        void CloseRecipeSheet(bool silent = false)
        {
            if (_recipeSheet == null) return;
            FinishClosing();   // предыдущий лист, если он ещё летит на доску, — сразу на место
            if (_openingRoutine != null) StopCoroutine(_openingRoutine);
            _openingRoutine = null;
            var sheet = _recipeSheet;
            var recipe = _sheetRecipe;
            _recipeSheet = null;
            _sheetRecipe = null;
            if (silent || _sheetHolder == null || !isActiveAndEnabled)
            {
                Destroy(sheet);
                ShowOrderPapers();
                return;
            }
            _game.Sfx.PlayPaper();
            // Пока лист улетает, он больше не ловит клики.
            var fade = sheet.GetComponent<CanvasGroup>();
            if (fade == null) fade = sheet.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = false;
            _closingSheet = sheet;
            _closingRoutine = StartCoroutine(PutBack(sheet, _sheetHolder, _sheetDim, recipe));
        }

        /// <summary>Мгновенно доиграть возврат предыдущего листа (если открывают новый, пока старый улетает).</summary>
        void FinishClosing()
        {
            if (_closingRoutine != null) StopCoroutine(_closingRoutine);
            _closingRoutine = null;
            if (_closingSheet != null) Destroy(_closingSheet);
            _closingSheet = null;
            ShowOrderPapers();
        }

        /// <summary>Вернуть на доску все листы (кроме открытого сейчас).</summary>
        void ShowOrderPapers()
        {
            foreach (var pair in _orderPapers)
                if (pair.Value.Group != null && pair.Key != _sheetRecipe) pair.Value.Group.alpha = 1f;
        }

        /// <summary>Где на экране лист заказа: позиция в координатах overlay, масштаб относительно листа рецепта и наклон.</summary>
        bool CardPose(RecipeDef recipe, RectTransform overlay, out Vector3 position, out float scale, out float angle)
        {
            position = Vector3.zero; scale = 1f; angle = 0f;
            if (recipe == null || !_orderPapers.TryGetValue(recipe, out var entry) || entry.Card == null) return false;
            var card = entry.Card;
            position = overlay.InverseTransformPoint(card.TransformPoint(card.rect.center));
            float cardWidth = card.rect.width * card.lossyScale.x;
            float sheetWidth = RecipeSheetSize.x * overlay.lossyScale.x;
            scale = sheetWidth > 0f ? cardWidth / sheetWidth : 0.3f;
            angle = Mathf.DeltaAngle(0f, card.eulerAngles.z - overlay.eulerAngles.z);
            return true;
        }

        /// <summary>
        /// Лист «достают» с доски: он отходит от прищепки, по пологой дуге вырастает в центр и выравнивается,
        /// фон плавно темнеет. Если листа на доске нет (например, открыли клавишей с другого экрана) — выезжает снизу.
        /// </summary>
        IEnumerator PullOut(RectTransform overlay, RectTransform holder, Image dim, RecipeDef recipe)
        {
            const float duration = 0.55f;
            Vector3 end = holder.localPosition;
            CanvasGroup board = null;
            if (CardPose(recipe, overlay, out var start, out float startScale, out float startAngle))
                board = _orderPapers[recipe].Group;
            else
            {
                start = end + new Vector3(0f, -RecipeSheetSize.y * 1.1f, 0f);
                startScale = 0.92f;
                startAngle = 4f;
            }
            for (float t = 0f; t < duration && holder != null; t += UiTime.Delta)
            {
                float k = t / duration;
                float e = 1f - Mathf.Pow(1f - k, 3f);                       // плавное замедление в конце
                float arc = Mathf.Sin(e * Mathf.PI) * 40f;                   // лёгкий подъём по дуге
                holder.localPosition = Vector3.LerpUnclamped(start, end, e) + new Vector3(0f, arc, 0f);
                holder.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, e);
                // наклон листа на доске уходит в ноль с едва заметным покачиванием
                float angle = Mathf.Lerp(startAngle, 0f, e) - 1.5f * Mathf.Sin(e * Mathf.PI) * (1f - e);
                holder.localRotation = Quaternion.Euler(0f, 0f, angle);
                if (dim != null) dim.color = new Color(0f, 0f, 0f, SheetDim * Mathf.SmoothStep(0f, 1f, k / 0.6f));
                if (board != null) board.alpha = 1f - Mathf.Clamp01(t / 0.08f);  // лист «снят» с прищепки
                yield return null;
            }
            if (holder == null) yield break;
            holder.localPosition = end;
            holder.localScale = Vector3.one;
            holder.localRotation = Quaternion.identity;
            if (dim != null) dim.color = new Color(0f, 0f, 0f, SheetDim);
            if (board != null) board.alpha = 0f;
        }

        /// <summary>Лист возвращается на своё место на доске (или уезжает вниз), фон светлеет.</summary>
        IEnumerator PutBack(GameObject sheet, RectTransform holder, Image dim, RecipeDef recipe)
        {
            const float duration = 0.4f;
            var overlay = (RectTransform)sheet.transform;
            Vector3 start = holder.localPosition;
            float fromScale = holder.localScale.x;
            float fromAngle = Mathf.DeltaAngle(0f, holder.localEulerAngles.z);
            CanvasGroup board = null;
            if (CardPose(recipe, overlay, out var end, out float endScale, out float endAngle))
                board = _orderPapers[recipe].Group;
            else
            {
                end = start + new Vector3(0f, -RecipeSheetSize.y * 1.1f, 0f);
                endScale = 0.92f;
                endAngle = 4f;
            }
            float fromDim = dim != null ? dim.color.a : 0f;
            for (float t = 0f; t < duration && holder != null; t += UiTime.Delta)
            {
                float k = t / duration;
                float e = k * k * (3f - 2f * k);                             // мягко разгоняется и мягко садится
                float arc = Mathf.Sin(e * Mathf.PI) * 30f;
                holder.localPosition = Vector3.LerpUnclamped(start, end, e) + new Vector3(0f, arc, 0f);
                holder.localScale = Vector3.one * Mathf.Lerp(fromScale, endScale, e);
                holder.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromAngle, endAngle, e));
                if (dim != null) dim.color = new Color(0f, 0f, 0f, fromDim * (1f - e));
                // в самом конце лист снова «висит» на доске, а летящая копия исчезает
                if (board != null && k > 0.85f) board.alpha = (k - 0.85f) / 0.15f;
                yield return null;
            }
            if (sheet != null) Destroy(sheet);
            if (board != null) board.alpha = 1f;
            if (_closingSheet == sheet) { _closingSheet = null; _closingRoutine = null; }
        }

        /// <summary>
        /// Клавиша блюда (1/2/3) на экране выбора: открывает его лист, а если этот лист уже открыт — начинает готовку.
        /// </summary>
        public void DishHotkey(RecipeDef recipe)
        {
            if (_sheetRecipe == recipe) _game.StartRound(recipe);
            else OpenRecipeSheet(recipe);
        }

        /// <summary>Прямоугольник от верхнего края родителя: отступы слева/справа, отступ сверху и высота.</summary>
        static RectTransform TopLeft(RectTransform parent, float left, float top, float right, float height) =>
            Stretch(NewRect("Block", parent), new Vector2(0f, 1f), Vector2.one, new Vector2(left, -top - height), new Vector2(-right, -top));

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
                "Нажми на клавишу справа, затем — новую (Esc — отмена)", 30, MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);
            // Высота строк — чтобы все клавиши поместились в панель (их стало больше со способностями).
            const float rowGap = 8f, listTop = 90f, listBottom = 24f;
            var actions = InputUtil.Actions;
            float rowHeight = Mathf.Min(80f, (panelSize.y - listTop - listBottom + rowGap) / actions.Length - rowGap);
            for (int i = 0; i < actions.Length; i++)
                HotkeyRow(hotkeys, listTop + i * (rowHeight + rowGap), rowHeight, actions[i]);

            // ── справа: кнопки по центру высоты панели и строка состояния под ними
            MenuButton(screen, new Vector2(560f, 65f), "Назад", true, () => CloseSettings());
            MenuButton(screen, new Vector2(560f, -85f), "Сохранить", false, () =>
            {
                GameSettings.Save();
                InputUtil.SaveBindings();
                SaveGraphics();
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
            SyncGraphicsFromScreen();
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
            RevertGraphics();
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
            bool unsaved = GameSettings.HasUnsavedChanges() || InputUtil.HasUnsavedChanges() || GraphicsChanged;
            if (unsaved) _settingsJustSaved = false;
            _settingsStatus.text = unsaved ? "Есть несохранённые изменения" : _settingsJustSaved ? "Настройки сохранены" : "";
            _settingsStatus.color = unsaved ? new Color(1f, 0.72f, 0.6f) : MenuLightText;
        }

        void HotkeyRow(RectTransform panel, float top, float height, HotkeyAction action)
        {
            var row = Stretch(NewRect(action.ToString(), panel), new Vector2(0f, 1f), Vector2.one, new Vector2(60f, -top - height), new Vector2(-60f, -top));
            MenuText(Stretch(NewRect("Label", row), Vector2.zero, new Vector2(0.58f, 1f), Vector2.zero, Vector2.zero),
                HotkeyTitle(action), height < 70f ? 30 : 34, MenuBrownText, TextAnchor.MiddleLeft, FontStyle.Bold);

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
            var label = MenuText(Fill(NewRect("Label", face.rectTransform)), "", height < 70f ? 30 : 34, MenuGreenText, TextAnchor.MiddleCenter, FontStyle.Bold);
            _hotkeyLabels.Add((action, label));
        }

        static string HotkeyTitle(HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.Pause: return "Пауза / назад";
                case HotkeyAction.Restart: return "Начать уровень заново";
                case HotkeyAction.Ability1:
                case HotkeyAction.Ability2:
                case HotkeyAction.Ability3:
                    return $"Способность: {Abilities.All[action - HotkeyAction.Ability1].Name}";
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
        /// <summary>Кольцо (скруглённая рамка без заливки) толщиной width с радиусом radius, в единицах холста.</summary>
        static Image Ring(RectTransform rt, Color color, float radius, float width)
        {
            float scale = Art.RingRadius / Mathf.Max(radius, 1f);
            var image = AddImage(rt, color, Art.RoundedRing(Mathf.RoundToInt(width * scale)));
            image.pixelsPerUnitMultiplier = scale;
            return image;
        }

        /// <summary>
        /// Как Framed, но слои не лежат друг под другом: контур и деревянная рамка — кольца, заливка — только внутри.
        /// Поэтому заливку можно делать полупрозрачной: сквозь неё видно фон, а не дерево рамки.
        /// </summary>
        static Image FramedClear(RectTransform rt, Color fill, float frame, float radius = 24f)
        {
            Ring(rt, MenuOutline, radius, 3f);
            float inset = 3f + frame;
            // Заливка на полпикселя заходит под рамку — без просвета на стыке.
            var inner = Rounded(Stretch(NewRect("Fill", rt), Vector2.zero, Vector2.one, new Vector2(inset - 0.5f, inset - 0.5f), new Vector2(0.5f - inset, 0.5f - inset)),
                fill, Mathf.Max(radius - inset, 4f));
            Ring(Stretch(NewRect("Frame", rt), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f)), MenuFrame, radius - 3f, frame);
            return inner;
        }

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
        /// <param name="click">Играть общий звук клика. Выключено у кнопок листа рецепта — у них звук бумаги.</param>
        static void MakeButtonOn(RectTransform rt, Image fill, UnityAction onClick, bool click = true, bool hoverRing = true)
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
            if (click) button.onClick.AddListener(() => _uiClick?.Invoke());
            button.onClick.AddListener(onClick);
            // при наведении — рамка цвета кнопки с текучей «краской»
            if (hoverRing) HoverRing.Attach(rt, HoverRing.ColorFor(fill.color));
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
            var current = new Vector2Int(Screen.width, Screen.height);
            int index = _resolutions.IndexOf(current);
            if (index >= 0) return index;
            // Окно растянули мышью или размер не из списка монитора — показываем его как есть, а не «самое большое».
            if (current.x > 0 && current.y > 0)
            {
                _resolutions.Add(current);
                _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                return _resolutions.IndexOf(current);
            }
            return _resolutions.Count - 1;
        }

        // Разрешение и режим, с которыми открыли настройки (или последние сохранённые): «Назад» возвращает к ним.
        int _savedResolutionIndex, _savedWindowModeIndex;

        bool GraphicsChanged => _resolutions.Count > 0
            && (_resolutionIndex != _savedResolutionIndex || _windowModeIndex != _savedWindowModeIndex);

        /// <summary>
        /// При открытии настроек — взять фактические разрешение и режим: их могли сменить в обход меню
        /// (Alt+Enter, растягивание окна), и подписи иначе показывали бы устаревшее.
        /// </summary>
        void SyncGraphicsFromScreen()
        {
            CollectResolutions();
            SaveGraphics();
            RefreshGraphicsLabels();
        }

        void SaveGraphics()
        {
            _savedResolutionIndex = _resolutionIndex;
            _savedWindowModeIndex = _windowModeIndex;
        }

        /// <summary>«Назад» без сохранения: вернуть разрешение и режим, если их меняли.</summary>
        void RevertGraphics()
        {
            if (!GraphicsChanged) return;
            _resolutionIndex = _savedResolutionIndex;
            _windowModeIndex = _savedWindowModeIndex;
            var size = _resolutions[_resolutionIndex];
            Screen.SetResolution(size.x, size.y, WindowModes[_windowModeIndex].Mode);
            RefreshGraphicsLabels();
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
            RefreshSettingsStatus();
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

        /// <summary>
        /// Текст всегда в одну строку: если не влезает по ширине, шрифт уменьшается ровно настолько, насколько нужно
        /// (но не меньше minSize). Работает с любым шрифтом — широким или узким.
        /// </summary>
        static void FitSingleLine(Text text, int maxSize, int minSize)
        {
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.fontSize = maxSize;
            float width = text.rectTransform.rect.width;
            float preferred = text.preferredWidth;
            if (width > 0f && preferred > width)
                text.fontSize = Mathf.Max(minSize, Mathf.FloorToInt(maxSize * width / preferred));
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
    }
}
