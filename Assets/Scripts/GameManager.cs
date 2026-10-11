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
        static readonly Color BackgroundTop = new Color32(0x4A, 0x33, 0x26, 0xFF);
        static readonly Color BackgroundBottom = new Color32(0x1E, 0x14, 0x0F, 0xFF);
        /// <summary>Затемнение картинки-фона: меньше — темнее, ингредиенты контрастнее.</summary>
        static readonly Color BackgroundTint = new Color(0.6f, 0.6f, 0.64f);

        public Sfx Sfx { get; private set; }
        /// <summary>Игра на паузе: время остановлено, открыто меню паузы.</summary>
        public bool IsPaused { get; private set; }
        public Rect PlayArea { get; private set; }
        public float ViewHalfHeight => _cam.orthographicSize;

        Camera _cam;
        SpriteRenderer _background;
        bool _backgroundIsPicture;
        GameUI _ui;
        Fx _fx;
        Container _container;
        Spawner _spawner;
        readonly List<Ingredient> _active = new List<Ingredient>();

        State _state;
        RecipeDef _recipe;
        RoundState _round;
        float _stateTimer;
        int _countdownStep;
        int _lastTickSecond;
        int _comboSize;
        float _comboAge;
        Vector3 _comboPosition;

        void Awake()
        {
            GameSettings.Load();
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
            _container = AddChild<Container>("Container");
            _container.Init(_fx);
            AddChild<SwipeInput>("Swipe").Init(this, _cam);
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
            _cam.orthographicSize = 5f;
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
            _recipe = recipe;
            _round = new RoundState(recipe);
            _spawner.Reset();
            _lastTickSecond = -1;
            _comboSize = 0;
            _comboAge = ComboWindow;
            _countdownStep = -1;
            _stateTimer = 0f;
            _state = State.Countdown;
            _ui.ShowRound(_round);
            _container.SetRecipe(recipe);
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

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        void ClearPause()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            _ui.ShowPause(false);
        }

        public int BestScore(RecipeDef recipe) => PlayerPrefs.GetInt(BestKey(recipe), -1);

        static string BestKey(RecipeDef recipe) => "best_" + recipe.Id;

        void GoToMenu(bool dishSelect = false)
        {
            ClearPause();
            ClearIngredients();
            _state = State.Menu;
            _ui.ShowMenu(dishSelect);
            _container.Hide();
            Sfx.PlayMusic(MusicTrack.Lobby);
        }

        void Update()
        {
            UpdateLayout();

            if (IsPaused)
            {
                if (InputUtil.BackPressed()) Resume();
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
                        StartRound(Recipes.All[i]);
                        break;
                    }
                    break;

                case State.Countdown:
                    if (HandleRoundKeys()) return;
                    UpdateCountdown();
                    break;

                case State.Playing:
                    if (HandleRoundKeys()) return;
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
            _stateTimer = 1.3f;
            _ui.ShowBanner(banner, color, 1.3f);
            _ui.UpdateTimer(_round);
        }

        void ShowResults()
        {
            ClearIngredients();
            int best = BestScore(_recipe);
            bool newBest = false;
            // Рекорд засчитывается только за готовое блюдо.
            if (_round.IsComplete)
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
                && _round.Collected(ingredient.Def) < _recipe.NeededCount(ingredient.Def))
                Sfx.PlayMiss();
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
            ingredient.Slice(direction, _container, result);

            _fx.Burst(position, def.Color, result == SliceResult.Wrong ? 10 : 16);
            Sfx.PlayChop(def.Id);
            Vector3 textPos = position + Vector2.up * 0.6f;

            switch (result)
            {
                case SliceResult.Needed:
                    Sfx.PlayGood(_round.Collected(def) / (float)_recipe.NeededCount(def));
                    _ui.Floating(textPos, $"{def.Name} +{RoundState.PointsPerIngredient}", GameUI.GoodColor);
                    // Нужные подряд с промежутком меньше ComboWindow — одно движение.
                    _comboSize = _comboAge < ComboWindow ? _comboSize + 1 : 1;
                    _comboAge = 0f;
                    _comboPosition = textPos;
                    break;

                case SliceResult.Overflow:
                    _comboSize = 0;
                    Sfx.PlayOverflow();
                    _fx.Shake(0.12f, 0.15f);
                    _ui.Floating(textPos, $"Уже хватит! −{RoundState.OverflowPenalty}", GameUI.AccentColor);
                    break;

                case SliceResult.Wrong:
                    _comboSize = 0;
                    Sfx.PlayBad();
                    _fx.Shake(0.35f, 0.3f);
                    _ui.Flash(GameUI.BadColor);
                    _ui.Floating(textPos, $"{def.Name}?! −{RoundState.WrongPenalty}", GameUI.BadColor);
                    break;
            }

            _ui.OnSliced(def, _round);
            _container.SetProgress(_round.TotalCollected / (float)_recipe.TotalNeeded);

            if (result == SliceResult.Needed && _round.IsComplete)
            {
                Sfx.PlayComplete();
                FinishRound("Блюдо готово!", GameUI.GoodColor);
            }
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
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;
            float side = 2f * halfW * GameUI.SidePanelFraction;
            PlayArea = Rect.MinMaxRect(-halfW + side, -halfH, halfW - side, halfH);
            _container.Place(halfH);

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
