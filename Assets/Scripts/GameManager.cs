using System.Collections.Generic;
using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>Главный цикл: Меню → Отсчёт → Раунд → Результат. Собирает сцену при старте.</summary>
    public sealed class GameManager : MonoBehaviour
    {
        enum State
        {
            Menu,
            Countdown,
            Playing,
            Finishing,
            Results,
        }

        static readonly string[] CountdownSteps = { "3", "2", "1", "Режь!" };
        /// <summary>Максимальная пауза между нужными разрезами внутри одного комбо, в секундах.</summary>
        const float ComboWindow = 0.25f;
        /// <summary>Высота половины кадра в мире. Камера может приближаться (Fx.SetZoom), а поле и фон считаются от этой величины.</summary>
        const float CameraSize = 5f;
        /// <summary>Серия нужных разрезов без ошибок, с которой клинок золотой и огненный.</summary>
        const int HotStreak = 5, FireStreak = 12;
        /// <summary>Комбо из стольких нужных и больше — короткое замедление времени с приближением.</summary>
        const int SlowMoCombo = 4;
        const float SlowMoDuration = 0.6f;
        const float SlowMoScale = 0.3f;
        /// <summary>«Без чего» для плашки «Клиент передумал: без …!».</summary>
        static readonly Dictionary<string, string> WithoutName = new Dictionary<string, string>
        {
            { "tomato", "помидоров" }, { "cucumber", "огурцов" }, { "lettuce", "салата" }, { "onion", "лука" },
            { "pepper", "перца" }, { "cheese", "сыра" }, { "mushroom", "грибов" }, { "bacon", "бекона" },
            { "olive", "оливок" }, { "shrimp", "креветок" }, { "chili", "чили" }, { "lime", "лайма" },
            { "coconut", "кокоса" }, { "lemongrass", "лемонграсса" }, { "galangal", "галангала" },
        };
        static readonly Color BackgroundTop = new Color32(0x4A, 0x33, 0x26, 0xFF);
        static readonly Color BackgroundBottom = new Color32(0x1E, 0x14, 0x0F, 0xFF);
        /// <summary>Затемнение картинки-фона: меньше — темнее, ингредиенты контрастнее.</summary>
        static readonly Color BackgroundTint = new Color(0.6f, 0.6f, 0.64f);

        public Sfx Sfx { get; private set; }
        /// <summary>Игра на паузе: время остановлено, открыто меню паузы.</summary>
        public bool IsPaused { get; private set; }
        /// <summary>Идёт раунд и можно резать (не пауза, не отсчёт, не результат).</summary>
        public bool IsSlicing => _state == State.Playing && !IsPaused;
        public Rect PlayArea { get; private set; }
        public float ViewHalfHeight => CameraSize;

        Camera _cam;
        SpriteRenderer _background;
        bool _backgroundIsPicture;
        GameUI _ui;
        Fx _fx;
        Spawner _spawner;
        SwipeInput _swipe;
        int _streak;
        /// <summary>Когда (доля раунда) клиент передумает; новое значение каждый раунд.</summary>
        float _changeMindAt;
        bool _mindChecked;
        float _slowMoTime;
        Vector2 _slowMoFocus;
        readonly List<Ingredient> _active = new List<Ingredient>();

        State _state;
        RecipeDef _recipe;
        RoundState _round;
        float _stateTimer;
        int _countdownStep;
        int _lastTickSecond;
        int _comboSize;
        /// <summary>Раунд пройден кодом Конами — рекорд за него не записываем.</summary>
        bool _cheated;
        float _comboAge;
        Vector3 _comboPosition;

        void Awake()
        {
            GameSettings.Load();
            InputUtil.LoadBindings();
            SetupCamera();

            _background = new GameObject("Background").AddComponent<SpriteRenderer>();
            _background.transform.SetParent(transform, false);
            // Картинка кухни, если есть; иначе — градиент-заглушка.
            var kitchen = Art.LoadSprite("Kitchen/background");
            _backgroundIsPicture = kitchen != null;
            _background.sprite = kitchen != null ? kitchen : Art.Gradient(BackgroundBottom, BackgroundTop);
            // Фон приглушаем, чтобы яркие ингредиенты на нём выделялись.
            if (kitchen != null) _background.color = BackgroundTint;
            _background.sortingOrder = -100;

            Sfx = AddChild<Sfx>("Sfx");
            _fx = AddChild<Fx>("Fx");
            _fx.Init(_cam);
            _swipe = AddChild<SwipeInput>("Swipe");
            _swipe.Init(this, _cam);
            _ui = AddChild<GameUI>("UI");
            _ui.Init(this, _cam);
            AddChild<KnifeCursor>("Cursor");
            _spawner = new Spawner(this);

            UpdateLayout();
            GoToMenu();
        }

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
            }
            if (FindAnyObjectByType<AudioListener>() == null) _cam.gameObject.AddComponent<AudioListener>();

            _cam.orthographic = true;
            _cam.orthographicSize = CameraSize;
            _cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = BackgroundBottom;
        }

        T AddChild<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        // ───────────────────────── Состояния ─────────────────────────

        public void StartRound(RecipeDef recipe)
        {
            ClearPause();
            ClearIngredients();
            _fx.ClearSplats();
            _recipe = recipe;
            _round = new RoundState(recipe);
            _spawner.Reset();
            _lastTickSecond = -1;
            _comboSize = 0;
            ResetStreak();
            _changeMindAt = Random.Range(0.42f, 0.56f);
            _mindChecked = false;
            _cheated = false;
            InputUtil.ResetKonami();
            _comboAge = ComboWindow;
            _countdownStep = -1;
            _stateTimer = 0f;
            _state = State.Countdown;
            _ui.ShowRound(_round);
            _ui.ShowBanner("Режь только то, что в списке «НУЖНО»!", GameUI.AccentColor, 2.6f);
            Sfx.PlayClick();
            Sfx.PlayMusic(MusicTrack.Round, restart: true);
        }

        public void Restart()
        {
            if (_recipe != null) StartRound(_recipe);
        }

        public void BackToMenu()
        {
            Sfx.PlayClick();
            GoToMenu();
        }

        /// <summary>С экрана результата — сразу к выбору блюда.</summary>
        public void ToDishSelect()
        {
            Sfx.PlayClick();
            GoToMenu(dishSelect: true);
        }

        // ───────────────────────── Пауза ─────────────────────────

        /// <summary>Пауза доступна во время отсчёта и раунда.</summary>
        public void Pause()
        {
            if (IsPaused || (_state != State.Countdown && _state != State.Playing)) return;
            IsPaused = true;
            Time.timeScale = 0f;
            _ui.ShowPause(true);
            Sfx.PlayClick();
            // В паузе играет музыка меню; музыка раунда запомнит место и продолжится после паузы.
            Sfx.PlayMusic(MusicTrack.Lobby);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            ClearPause();
            Sfx.PlayClick();
            Sfx.PlayMusic(MusicTrack.Round);
        }

        void ClearPause()
        {
            IsPaused = false;
            StopSlowMo();
            _ui.ShowPause(false);
        }

        public int BestScore(RecipeDef recipe) => PlayerPrefs.GetInt(BestKey(recipe), -1);

        static string BestKey(RecipeDef recipe) => "best_" + recipe.Id;

        void GoToMenu(bool dishSelect = false)
        {
            ClearPause();
            ClearIngredients();
            _fx.ClearSplats();
            _state = State.Menu;
            _ui.ShowMenu(dishSelect);
            Sfx.PlayMusic(MusicTrack.Lobby);
        }

        void Update()
        {
            UpdateLayout();
            UpdateSlowMo();

            if (IsPaused)
            {
                // Esc сначала закрывает настройки (возврат в паузу), потом снимает паузу.
                if (InputUtil.BackPressed())
                {
                    if (!_ui.CloseSettings()) Resume();
                }
                else if (InputUtil.RestartPressed()) Restart();
                return;
            }

            switch (_state)
            {
                case State.Menu:
                    if (InputUtil.BackPressed()) _ui.MenuBack();
                    if (!_ui.DishSelectOpen) break;
                    for (int i = 0; i < Recipes.All.Length; i++)
                    {
                        if (!InputUtil.DishPressed(i)) continue;
                        _ui.DishHotkey(Recipes.All[i]); // первое нажатие — лист рецепта, второе — готовка
                        break;
                    }
                    break;

                case State.Countdown:
                    if (HandleRoundKeys()) return;
                    if (InputUtil.KonamiEntered())
                    {
                        CompleteWithKonami();
                        return;
                    }
                    UpdateCountdown();
                    break;

                case State.Playing:
                    if (HandleRoundKeys()) return;
                    if (InputUtil.KonamiEntered())
                    {
                        CompleteWithKonami();
                        return;
                    }
                    UpdatePlaying();
                    break;

                case State.Finishing:
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0f) ShowResults();
                    break;

                case State.Results:
                    HandleRoundKeys();
                    break;
            }
        }

        bool HandleRoundKeys()
        {
            if (InputUtil.RestartPressed())
            {
                Restart();
                return true;
            }
            if (InputUtil.BackPressed())
            {
                if (_state == State.Countdown || _state == State.Playing) Pause();
                else ToDishSelect();
                return true;
            }
            return false;
        }

        void UpdateCountdown()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f) return;

            _countdownStep++;
            _ui.SetCountdown(CountdownSteps[_countdownStep]);
            if (_countdownStep < CountdownSteps.Length - 1)
            {
                Sfx.PlayCountdownTick();
                _stateTimer = 0.65f;
            }
            else
            {
                Sfx.PlayCountdownGo();
                _state = State.Playing;
            }
        }

        void UpdatePlaying()
        {
            _round.TimeLeft -= Time.deltaTime;
            float progress = Mathf.Clamp01(1f - _round.TimeLeft / _recipe.RoundTime);
            _spawner.Tick(Time.deltaTime, progress, _round);
            if (_round.Cancelled == null && !_mindChecked && progress >= _changeMindAt)
            {
                ChangeMind();
                if (_state != State.Playing) return;
            }
            UpdateCombo(Time.deltaTime);
            int second = Mathf.CeilToInt(_round.TimeLeft);
            if (second <= 5 && second > 0 && second != _lastTickSecond)
            {
                _lastTickSecond = second;
                Sfx.PlayTimerTick(second);
            }

            // Не успел собрать рецепт за отведённое время — проигрыш.
            if (_round.TimeLeft <= 0f)
            {
                _round.TimeLeft = 0f;
                Sfx.PlayTimeUp();
                FinishRound("Время вышло!", GameUI.BadColor);
            }

            _ui.UpdateTimer(_round);
        }

        void UpdateCombo(float dt)
        {
            _comboAge += dt;
            if (_comboSize > 0 && _comboAge >= ComboWindow) FlushCombo();
        }

        /// <summary>Серия закончилась: если в ней 3+ нужных — начислить бонус.</summary>
        void FlushCombo()
        {
            int size = _comboSize;
            _comboSize = 0;
            if (size < RoundState.ComboMin) return;

            if (size >= SlowMoCombo) StartSlowMo(_comboPosition);
            int bonus = _round.AddCombo(size);
            Sfx.PlayCombo();
            _fx.Shake(0.1f, 0.12f);
            _ui.Floating(_comboPosition + Vector3.up * 1.2f, $"КОМБО ×{size}! +{bonus}", GameUI.AccentColor, 64);
            _ui.OnSliced(null, _round);
        }

        /// <summary>Конец попытки: короткая пауза с плашкой, потом экран результата.</summary>
        void FinishRound(string banner, Color color)
        {
            FlushCombo();
            _state = State.Finishing;
            _ui.SetPauseAvailable(false);
            _stateTimer = 1.3f;
            _ui.ShowBanner(banner, color, 1.3f);
            _ui.UpdateTimer(_round);
        }

        /// <summary>↑ ↑ ↓ ↓ ← → ← → B A: уровень пропускается, показывается идеальный результат.</summary>
        void CompleteWithKonami()
        {
            _cheated = true;
            _comboSize = 0;
            ClearIngredients();
            _round.CompletePerfectly();
            _ui.OnSliced(null, _round);
            Sfx.PlayComplete();
            FinishRound("Код Конами! Идеальное блюдо", GameUI.AccentColor);
        }

        void ShowResults()
        {
            StopSlowMo();
            ClearIngredients();
            int best = BestScore(_recipe);
            bool newBest = false;
            // Рекорд засчитывается только за готовое блюдо и не за код Конами.
            if (_round.IsComplete && !_cheated)
            {
                int score = _round.Score;
                newBest = score > best && score > 0;
                if (score > best)
                {
                    best = score;
                    PlayerPrefs.SetInt(BestKey(_recipe), best);
                    PlayerPrefs.Save();
                }
            }
            _state = State.Results;
            _ui.ShowResults(_round, best, newBest);
            if (_round.IsComplete && _round.Stars >= 2) Sfx.PlayComplete();
        }

        // ───────────────────────── Ингредиенты ─────────────────────────

        public void Register(Ingredient ingredient) => _active.Add(ingredient);

        public void OnIngredientLost(Ingredient ingredient)
        {
            // Промах: упал ингредиент, которого рецепту ещё не хватает.
            if (_state == State.Playing && !ingredient.IsSliced
                && _round.Collected(ingredient.Def) < _round.Need(ingredient.Def) && ingredient.Def != _round.Cancelled)
            {
                Sfx.PlayMiss();
                ResetStreak(); // упустил нужный — клинок остывает
            }
            _active.Remove(ingredient);
            Destroy(ingredient.gameObject);
        }

        void ClearIngredients()
        {
            foreach (var ingredient in _active)
                if (ingredient != null) Destroy(ingredient.gameObject);
            _active.Clear();
        }

        /// <summary>Отрезок свайпа за кадр (в мировых координатах).</summary>
        public void OnSwipe(Vector2 from, Vector2 to)
        {
            if (_state != State.Playing || IsPaused) return;

            Vector2 direction = (to - from).normalized;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                // Рецепт мог собраться на предыдущем ингредиенте этого же свайпа — дальше не режем.
                if (_state != State.Playing) return;
                var ingredient = _active[i];
                if (ingredient.IsSliced) continue;
                if (DistanceToSegment(ingredient.Position, from, to) > ingredient.HitRadius) continue;
                _active.RemoveAt(i);
                Slice(ingredient, direction);
            }
        }

        void Slice(Ingredient ingredient, Vector2 direction)
        {
            var def = ingredient.Def;
            Vector2 position = ingredient.Position;
            var result = _round.Register(def);
            ingredient.Slice(direction);

            _fx.Burst(position, def.Color, result == SliceResult.Wrong ? 10 : 16);
            // Клякса сока на стене за ингредиентом — в его цвете (как в Fruit Ninja).
            _fx.SplatAt(position, def.Color, ingredient.HitRadius * 3.2f);
            Sfx.PlayChop(def.Id);
            Vector3 textPos = position + Vector2.up * 0.6f;

            switch (result)
            {
                case SliceResult.Needed:
                    Sfx.PlayGood(_round.Collected(def) / (float)Mathf.Max(1, _round.Need(def)));
                    _ui.Floating(textPos, $"{def.Name} +{RoundState.PointsPerIngredient}", GameUI.GoodColor);
                    // Нужные подряд с промежутком меньше ComboWindow — одно движение.
                    _comboSize = _comboAge < ComboWindow ? _comboSize + 1 : 1;
                    _comboAge = 0f;
                    _comboPosition = textPos;
                    AddStreak(textPos);
                    break;

                case SliceResult.Overflow:
                    _comboSize = 0;
                    ResetStreak();
                    Sfx.PlayOverflow();
                    _fx.Shake(0.12f, 0.15f);
                    _ui.Floating(textPos, $"Уже хватит! −{RoundState.OverflowPenalty}", GameUI.AccentColor);
                    break;

                case SliceResult.Wrong:
                    _comboSize = 0;
                    ResetStreak();
                    Sfx.PlayBad();
                    _fx.Shake(0.35f, 0.3f);
                    _ui.Flash(GameUI.BadColor);
                    _ui.Floating(textPos, $"{def.Name}?! −{RoundState.WrongPenalty}", GameUI.BadColor);
                    break;
            }

            _ui.OnSliced(def, _round);

            if (result == SliceResult.Needed && _round.IsComplete)
            {
                Sfx.PlayComplete();
                FinishRound("Блюдо готово!", GameUI.GoodColor);
            }
        }

        /// <summary>
        /// Клиент передумал: один недособранный ингредиент (осталось нарезать хотя бы два) больше не нужен —
        /// он переезжает в «НЕ НУЖНО», нарезанное засчитано. Если без него рецепт уже собран — блюдо готово.
        /// </summary>
        void ChangeMind()
        {
            _mindChecked = true;
            var candidates = new List<IngredientDef>();
            foreach (var item in _recipe.Needed)
                if (item.Count - _round.Collected(item.Ingredient) >= 2) candidates.Add(item.Ingredient);
            if (candidates.Count == 0) return;

            var def = candidates[Random.Range(0, candidates.Count)];
            _round.Cancel(def);
            string without = WithoutName.TryGetValue(def.Id, out var name) ? name : def.Name.ToLowerInvariant();
            _ui.OnMindChanged(_round);
            _ui.ShowBanner($"Клиент передумал: без {without}!", GameUI.AccentColor, 2.6f);
            _ui.Flash(GameUI.AccentColor);
            Sfx.PlayCountdownGo();

            if (_round.IsComplete)
            {
                Sfx.PlayComplete();
                FinishRound("Блюдо готово!", GameUI.GoodColor);
            }
        }

        // ───────────────────────── Серия и замедление ─────────────────────────

        static int HeatLevel(int streak) => streak >= FireStreak ? 2 : streak >= HotStreak ? 1 : 0;

        /// <summary>Ещё один нужный подряд: на 5-м клинок раскаляется, на 12-м — огненный.</summary>
        void AddStreak(Vector3 textPos)
        {
            int before = HeatLevel(_streak);
            _streak++;
            int level = HeatLevel(_streak);
            _swipe.SetHeat(level);
            if (level == before) return;
            if (level == 1) _ui.Floating(textPos + Vector3.up * 0.7f, $"Серия ×{_streak}! Клинок раскалён", GameUI.AccentColor, 48);
            else _ui.Floating(textPos + Vector3.up * 0.7f, "Огненный клинок!", new Color(1f, 0.55f, 0.2f), 56);
        }

        void ResetStreak()
        {
            _streak = 0;
            if (_swipe != null) _swipe.SetHeat(0);
        }

        void StartSlowMo(Vector2 focus)
        {
            _slowMoTime = SlowMoDuration;
            _slowMoFocus = focus;
        }

        /// <summary>Замедление: время резко проседает и плавно возвращается; камера в это время чуть ближе к комбо.</summary>
        void UpdateSlowMo()
        {
            if (_slowMoTime <= 0f || IsPaused) return;
            _slowMoTime -= UiTime.Delta;
            if (_slowMoTime <= 0f)
            {
                StopSlowMo();
                return;
            }
            float k = 1f - _slowMoTime / SlowMoDuration; // 0 → 1
            float depth = k < 0.15f ? k / 0.15f : 1f - Mathf.SmoothStep(0f, 1f, (k - 0.15f) / 0.85f);
            Time.timeScale = Mathf.Lerp(1f, SlowMoScale, depth);
            _fx.SetZoom(depth, _slowMoFocus);
        }

        void StopSlowMo()
        {
            _slowMoTime = 0f;
            Time.timeScale = IsPaused ? 0f : 1f;
            if (_fx != null) _fx.SetZoom(0f, Vector2.zero);
        }

        static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq) : 0f;
            return Vector2.Distance(point, a + ab * t);
        }

        // ───────────────────────── Раскладка ─────────────────────────

        void UpdateLayout()
        {
            float halfH = CameraSize;
            float halfW = halfH * _cam.aspect;
            float side = 2f * halfW * GameUI.SidePanelFraction;
            PlayArea = Rect.MinMaxRect(-halfW + side, -halfH, halfW - side, halfH);

            // С запасом 10%, чтобы при тряске камеры не было видно краёв.
            var size = _background.sprite.bounds.size;
            if (_backgroundIsPicture)
            {
                // Картинку не искажаем: заполняем экран с сохранением пропорций (+4% под тряску), лишнее обрезается.
                float scale = Mathf.Max(2f * halfW / size.x, 2f * halfH / size.y) * 1.04f;
                _background.transform.localScale = new Vector3(scale, scale, 1f);
                // Прижимаем к верху: полки и посуда на стене важнее, чем нижний край стола.
                float top = halfH * 1.02f;
                _background.transform.localPosition = new Vector3(0f, top - size.y * scale * 0.5f, 0f);
            }
            else
            {
                _background.transform.localScale = new Vector3(2.2f * halfW / size.x, 2.2f * halfH / size.y, 1f);
            }
        }
    }
}
