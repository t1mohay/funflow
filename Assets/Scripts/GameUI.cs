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

        GameObject _menu, _hud, _results;
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
        }

        // ───────────────────────── Экраны ─────────────────────────

        public void ShowMenu()
        {
            _menu.SetActive(true);
            _hud.SetActive(false);
            _results.SetActive(false);
            _banner.text = "";
            _countdown.text = "";

            var all = Recipes.All;
            for (int i = 0; i < all.Length; i++)
            {
                int best = _game.BestScore(all[i]);
                _bestLabels[i].text = best >= 0 ? $"Рекорд: {best}/100" : "Ещё не готовили";
            }
        }

        public void ShowRound(RoundState round)
        {
            _menu.SetActive(false);
            _results.SetActive(false);
            _hud.SetActive(true);
            _timerRect.gameObject.SetActive(true);
            _timerPlate.SetActive(true);
            _score.gameObject.SetActive(true);
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

        void BuildMenu()
        {
            var menu = Fill(NewRect("Menu", _root));
            _menu = menu.gameObject;
            AddImage(menu, new Color(0f, 0f, 0f, 0.25f), null);

            AddText(Place(NewRect("Title", menu), new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(1600f, 150f)),
                "РЕЖЬ ПО РЕЦЕПТУ!", 104, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddText(Place(NewRect("Subtitle", menu), new Vector2(0.5f, 0.74f), Vector2.zero, new Vector2(1600f, 70f)),
                "Выбери блюдо и режь только те ингредиенты, которые в него входят", 38, TextColor, TextAnchor.MiddleCenter, FontStyle.Normal);

            var all = Recipes.All;
            for (int i = 0; i < all.Length; i++)
            {
                var recipe = all[i];
                float x = (i - (all.Length - 1) * 0.5f) * 500f;
                var card = Place(NewRect(recipe.Title, menu), new Vector2(0.5f, 0.41f), new Vector2(x, 0f), new Vector2(450f, 530f));
                MakeButton(card, CardColor, () => _game.StartRound(recipe));

                var dish = Art.DishSprite(recipe);
                if (dish != null)
                {
                    var dishIcon = AddImage(Place(NewRect("Dish", card), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(124f, 124f)), Color.white, null);
                    dishIcon.sprite = dish;
                    dishIcon.preserveAspect = true;
                }

                AddText(Top(card, 146f, 70f), recipe.Title, 56, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                AddText(Top(card, 214f, 50f), new string('★', recipe.Stars) + new string('☆', 3 - recipe.Stars),
                    40, AccentColor, TextAnchor.MiddleCenter, FontStyle.Normal);
                AddText(Top(card, 268f, 80f), recipe.Description, 27, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, FontStyle.Normal);

                int n = recipe.Needed.Length;
                const float iconStep = 56f;
                for (int j = 0; j < n; j++)
                {
                    var icon = Place(NewRect("Icon", card), new Vector2(0.5f, 1f), new Vector2((j - (n - 1) * 0.5f) * iconStep, -390f), new Vector2(52f, 52f));
                    SetIcon(AddImage(icon, Color.white, null), recipe.Needed[j].Ingredient);
                }

                _bestLabels.Add(AddText(Top(card, 428f, 40f), "", 28, AccentColor, TextAnchor.MiddleCenter, FontStyle.Normal));
                AddText(Top(card, 470f, 36f), $"клавиша {i + 1}", 22, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter, FontStyle.Normal);
            }

            AddText(Place(NewRect("Help", menu), new Vector2(0.5f, 0.09f), Vector2.zero, new Vector2(1700f, 60f)),
                "Зажми левую кнопку мыши и проведи по ингредиенту   •   R — заново   •   Esc — в меню",
                30, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter, FontStyle.Normal);
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
            MakeButton(back, SecondaryButtonColor, _game.BackToMenu);
            AddText(Fill(NewRect("Label", back)), "Другое блюдо  [Esc]", 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
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
