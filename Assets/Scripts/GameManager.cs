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
        /// <summary>«Стоп-кадр» при разрезе бонуса: на миг почти замирает, потом плавно разгоняется.</summary>
        const float HitStopDuration = 0.45f;
        const float HitStopScale = 0.08f;
        /// <summary>Радиус и сила толчка: соседние ингредиенты разлетаются от взорванного бонуса.</summary>
        const float BonusPushRadius = 3f;
        const float BonusPushForce = 4f;

        public Sfx Sfx { get; private set; }
        /// <summary>Игра на паузе: время остановлено, открыто меню паузы.</summary>
        public bool IsPaused { get; private set; }
        /// <summary>Идёт раунд и можно резать (не пауза, не отсчёт, не результат).</summary>
        public bool IsSlicing => _state == State.Playing && !IsPaused;
        /// <summary>Бонусный объект сейчас в воздухе — спавнер не выпускает второй.</summary>
        public bool BonusInFlight
        {
            get
            {
                foreach (var ingredient in _active)
                    if (ingredient != null && ingredient.Def.IsBonus) return true;
                return false;
            }
        }
        public Rect PlayArea { get; private set; }
        public float ViewHalfHeight => _cam.orthographicSize;

        Camera _cam;
        SpriteRenderer _background;
        bool _backgroundIsPicture;
        GameUI _ui;
        Fx _fx;
        Spawner _spawner;
        readonly List<Ingredient> _active = new List<Ingredient>();

        State _state;
        RecipeDef _recipe;
        RoundState _round;
        AbilityState _abilities = new AbilityState();
        float _hitStop;
        /// <summary>Time.timeScale сейчас задаём мы (замедление / стоп-кадр) — по окончании вернуть 1.</summary>
        bool _timeScaleOwned;
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
            _fx.ClearSplats();
            _recipe = recipe;
            _round = new RoundState(recipe);
            _abilities = new AbilityState();
            ResetTimeEffects();
            _spawner.Reset();
            _lastTickSecond = -1;
            _comboSize = 0;
            _cheated = false;
            InputUtil.ResetKonami();
            _comboAge = ComboWindow;
            _countdownStep = -1;
            _stateTimer = 0f;
            _state = State.Countdown;
            _ui.ShowRound(_round, _abilities);
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
            Sfx.SetSlowMo(false);
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
            Time.timeScale = 1f;
            _ui.ShowPause(false);
        }

        public int BestScore(RecipeDef recipe) => PlayerPrefs.GetInt(BestKey(recipe), -1);

        static string BestKey(RecipeDef recipe) => "best_" + recipe.Id;

        void GoToMenu(bool dishSelect = false)
        {
            ClearPause();
            ResetTimeEffects();
            ClearIngredients();
            _fx.ClearSplats();
            _state = State.Menu;
            _ui.ShowMenu(dishSelect);
            Sfx.PlayMusic(MusicTrack.Lobby);
        }

        void Update()
        {
            UpdateLayout();
            UpdateTimeScale();

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
                    HandleAbilityKeys();
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
            _abilities.Tick(UiTime.Delta);
            float progress = Mathf.Clamp01(1f - _round.TimeLeft / _recipe.RoundTime);
            _spawner.Tick(Time.deltaTime, progress, _round, _abilities);
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
            // Под «Очки ×2» комбо тоже удваивается.
            if (_abilities.IsActive(AbilityType.Multiplier))
            {
                int extra = bonus * (Abilities.MultiplierFactor - 1);
                _round.AddMultiplierBonus(extra);
                bonus += extra;
            }
            Sfx.PlayCombo();
            _fx.Shake(0.1f, 0.12f);
            _ui.Floating(_comboPosition + Vector3.up * 1.2f, $"КОМБО ×{size}! +{bonus}", GameUI.AccentColor, 64);
            _ui.OnSliced(null, _round);
        }

        /// <summary>Конец попытки: короткая пауза с плашкой, потом экран результата.</summary>
        void FinishRound(string banner, Color color)
        {
            FlushCombo();
            // Способности и замедление на итоговой плашке уже не нужны.
            _abilities.StopActive();
            _hitStop = 0f;
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
                // Щит: нож проходит сквозь лишнее и перебор.
                if (ShieldPasses(ingredient.Def)) continue;
                _active.RemoveAt(i);
                if (ingredient.Def.IsBonus) SliceBonus(ingredient, direction);
                else Slice(ingredient, direction);
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
                    Sfx.PlayGood(_round.Collected(def) / (float)_recipe.NeededCount(def));
                    int points = RoundState.PointsPerIngredient;
                    if (_abilities.IsActive(AbilityType.Multiplier))
                    {
                        int extra = points * (Abilities.MultiplierFactor - 1);
                        _round.AddMultiplierBonus(extra);
                        points += extra;
                    }
                    _ui.Floating(textPos, $"{def.Name} +{points}",
                        points > RoundState.PointsPerIngredient ? GameUI.AccentColor : GameUI.GoodColor);
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

            if (result == SliceResult.Needed && _round.IsComplete)
            {
                Sfx.PlayComplete();
                FinishRound("Блюдо готово!", GameUI.GoodColor);
            }
        }

        // ───────────────────────── Бонусы и способности ─────────────────────────

        /// <summary>Под щитом нож не режет лишнее и то, чего уже хватает.</summary>
        public bool ShieldPasses(IngredientDef def)
        {
            if (_round == null || def.IsBonus || !_abilities.IsActive(AbilityType.Shield)) return false;
            return _round.Peek(def) != SliceResult.Needed;
        }

        /// <summary>
        /// Разрезан бонусный объект: заряд способности, стоп-кадр, искры, ударная волна, тряска,
        /// и соседние ингредиенты разлетаются в стороны.
        /// </summary>
        void SliceBonus(Ingredient ingredient, Vector2 direction)
        {
            var def = ingredient.Def;
            var ability = Abilities.Of(def.Ability);
            Vector2 position = ingredient.Position;
            bool added = _abilities.AddCharge(def.Ability);
            ingredient.Slice(direction, 2.2f);

            _hitStop = HitStopDuration;
            _fx.Sparkle(position, def.Color, 36);
            _fx.Burst(position, Color.white, 12);
            _fx.Shockwave(position, def.Color, 7f);
            _fx.Shockwave(position, Color.white, 4f, 0.3f);
            _fx.Shake(0.28f, 0.35f);
            _ui.Flash(def.Color);
            Sfx.PlayChop();
            Sfx.PlayBonus();

            foreach (var other in _active)
            {
                if (other == null || other.IsSliced) continue;
                Vector2 away = other.Position - position;
                float distance = away.magnitude;
                if (distance > BonusPushRadius) continue;
                float strength = BonusPushForce * (1f - distance / BonusPushRadius);
                other.Push((distance > 0.01f ? away / distance : Vector2.up) * strength + Vector2.up * (strength * 0.3f));
            }

            Vector3 textPos = position + Vector2.up * 0.8f;
            string key = InputUtil.KeyName(HotkeyAction.Ability1 + (int)def.Ability);
            if (added)
            {
                _ui.Floating(textPos, $"+{ability.Name}!", def.Color, 60);
                _ui.ShowBanner($"Бонус «{ability.Name}»: жми {key}!", def.Color, 1.8f);
            }
            else
            {
                _ui.Floating(textPos, $"{ability.Name}: уже максимум", def.Color, 48);
            }
            _ui.OnBonus(def.Ability);
        }

        /// <summary>Горячие клавиши способностей во время раунда.</summary>
        void HandleAbilityKeys()
        {
            for (int i = 0; i < Abilities.All.Length; i++)
                if (InputUtil.AbilityPressed(i)) ActivateAbility(Abilities.All[i].Type);
        }

        /// <summary>Применить способность: по клавише или клику по слоту панели «Бонусы».</summary>
        public void ActivateAbility(AbilityType type)
        {
            if (!IsSlicing) return;
            var ability = Abilities.Of(type);
            if (!_abilities.Activate(type))
            {
                // Нет заряда или уже действует — слот коротко «дёргается».
                _ui.OnAbilityDenied(type);
                return;
            }
            Sfx.PlayAbility();
            _fx.Shake(0.12f, 0.15f);
            _ui.ShowBanner($"{ability.Name}!", ability.Color, 1.2f);
            _ui.OnAbilityUsed(type);
        }

        void ResetTimeEffects()
        {
            _hitStop = 0f;
            _abilities.StopActive();
            if (_timeScaleOwned && !IsPaused) Time.timeScale = 1f;
            _timeScaleOwned = false;
            Sfx.SetSlowMo(false);
        }

        /// <summary>
        /// Скорость игры: замедление от способности и стоп-кадр от бонуса. Пауза главнее (там 0).
        /// Time.timeScale трогаем, только пока эффект действует, — чтобы не мешать dev-скриптам.
        /// </summary>
        void UpdateTimeScale()
        {
            if (IsPaused) return;
            float scale = 1f;
            bool slow = _abilities.IsActive(AbilityType.SlowMo);
            if (slow) scale = Abilities.SlowMoScale;
            if (_hitStop > 0f)
            {
                _hitStop = Mathf.Max(0f, _hitStop - UiTime.Delta);
                // Резко замирает и плавно (квадратично) возвращается к обычной скорости.
                float k = _hitStop / HitStopDuration;
                scale = Mathf.Min(scale, Mathf.Lerp(1f, HitStopScale, k * k));
            }
            bool owned = scale < 1f;
            if (owned || _timeScaleOwned) Time.timeScale = scale;
            _timeScaleOwned = owned;
            Sfx.SetSlowMo(slow);
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
