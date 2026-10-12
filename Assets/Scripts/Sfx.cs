using System.Collections.Generic;
using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>Фоновая музыка: лобби (меню) или раунд.</summary>
    public enum MusicTrack
    {
        None,
        Lobby,
        Round,
    }

    /// <summary>
    /// Все звуки и музыка игры. Клипы берутся из Assets/Resources/Audio/...
    /// Если файла нет — играет процедурная заглушка, поэтому игра работает и без ассетов.
    ///
    /// Список звуков (функция → папка Resources/Audio):
    ///  1. Разрез еды по типу       PlayChop(id)           Slice/slice_{juicy|crunchy|soft|hard}_NN
    ///  2. Вылет еды                PlayLaunch()           Launch/launch_NN
    ///  3. Счётчик очков            PlayGood(fill)         Counter/counter_step_01..10
    ///  4. Промах (еда упала)       PlayMiss()             Miss/miss_NN
    ///     Взмах (пока зажата мышь) SetSwipe(held, speed)  Swipe/swipe_loop (петля); без неё — PlaySwoosh(): Swipe/swipe_empty_NN (лежат в Reserve/Audio/Swipe)
    ///     Перебор «уже хватит»     PlayOverflow()         Overflow/overflow_NN (пока — Miss)
    ///  5. Ошибка (лишний ингр.)    PlayBad()              Error/error_NN (пока — заглушка)
    ///  6. Выполнение задания       PlayComplete()         Task/task_complete_full
    ///     Комбо                    PlayCombo()            Task/task_complete_short
    ///  7. Таймер 5 секунд          PlayTimerTick(sec)     Timer/timer_tick_5..1
    ///     Время вышло              PlayTimeUp()           Timer/timer_end
    ///  8. Музыка в игре            PlayMusic(MusicTrack.Round)       Music/game_loop
    ///  9. Музыка в лобби           PlayMusic(MusicTrack.Lobby)       Music/lobby_loop
    /// 10. Отсчёт 3 секунды         PlayCountdownTick()    Countdown/countdown_tick
    ///                              PlayCountdownGo()      Countdown/countdown_go
    ///     Клик по кнопке           PlayClick()            UI/click_01..03 (мягкий синтезаторный «плак», случайная вариация)
    ///     Лист рецепта             PlayPaper()            UI/paper_unfold_01..02 (открытие и закрытие листа)
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        const float TwoPi = Mathf.PI * 2f;
        const float MusicVolume = 0.3f;
        const float MusicFade = 1.2f;
        /// <summary>Во сколько раз приглушается музыка, пока звучит таймер или отсчёт.</summary>
        const float MusicDuck = 0.35f;
        const string Root = "Audio/";

        /// <summary>Тип звука разреза по Id ингредиента. Неизвестные — «soft».</summary>
        static readonly Dictionary<string, string> SliceKind = new Dictionary<string, string>
        {
            // сочное
            { "tomato", "juicy" }, { "lime", "juicy" }, { "pineapple", "juicy" }, { "strawberry", "juicy" },
            { "olive", "juicy" }, { "milk", "juicy" },
            // хрустящее
            { "cucumber", "crunchy" }, { "lettuce", "crunchy" }, { "onion", "crunchy" }, { "pepper", "crunchy" },
            { "chili", "crunchy" }, { "lemongrass", "crunchy" }, { "carrot", "crunchy" }, { "apple", "crunchy" },
            { "cookie", "crunchy" },
            // мягкое
            { "cheese", "soft" }, { "mushroom", "soft" }, { "bacon", "soft" }, { "shrimp", "soft" },
            { "donut", "soft" }, { "banana", "soft" }, { "fish", "soft" }, { "sock", "soft" },
            { "icecream", "soft" }, { "bread", "soft" },
            // твёрдое
            { "coconut", "hard" }, { "galangal", "hard" }, { "chocolate", "hard" }, { "candy", "hard" },
            { "potato", "hard" },
        };

        AudioSource[] _sources;
        int _next;

        // процедурные заглушки
        AudioClip _swoosh, _chop, _good, _overflow, _bad, _tick, _go, _complete, _lose, _click, _launch;

        // клипы из Resources
        readonly Dictionary<string, AudioClip[]> _slice = new Dictionary<string, AudioClip[]>();
        // взмах: одна тихая петля «воздуха» на всё время зажатия мыши
        AudioSource _swipeSource;
        AudioLowPassFilter _swipeFilter;
        AudioClip _swipeLoop;
        float _swipeVolume, _swipeCutoff = SwipeCutoffSlow;
        AudioClip[] _paperClips;
        float _lastClickTime = -1f;
        AudioClip[] _launchClips, _swipeClips, _missClips, _overflowClips, _errorClips, _counterSteps, _clickClips;
        AudioClip[] _timerTicks; // индекс = секунд осталось (1..5)
        AudioClip _timerEnd, _countdownTick, _countdownGo, _taskFull, _taskShort, _lobbyMusic, _roundMusic;

        // музыка: два источника для плавной смены треков
        AudioSource _musicA, _musicB;
        // таймер и отсчёт: свой источник с высшим приоритетом, чтобы их не перебили другие звуки
        AudioSource _cueSource;
        float _duckUntil;
        MusicTrack _music = MusicTrack.None;
        // где остановился каждый трек — после паузы музыка раунда продолжается с того же места
        readonly Dictionary<AudioClip, float> _musicPositions = new Dictionary<AudioClip, float>();

        void Awake()
        {
            _sources = new AudioSource[16];
            for (int i = 0; i < _sources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _sources[i] = source;
            }
            _musicA = NewMusicSource();
            _musicB = NewMusicSource();
            _cueSource = gameObject.AddComponent<AudioSource>();
            _cueSource.playOnAwake = false;
            _cueSource.spatialBlend = 0f;
            _cueSource.priority = 0;
            // Свой объект: фильтр AudioLowPassFilter действует на все источники объекта, а нужен только взмаху.
            var swipeGo = new GameObject("Swipe");
            swipeGo.transform.SetParent(transform, false);
            _swipeSource = swipeGo.AddComponent<AudioSource>();
            _swipeFilter = swipeGo.AddComponent<AudioLowPassFilter>();
            _swipeFilter.cutoffFrequency = SwipeCutoffSlow;
            _swipeSource.playOnAwake = false;
            _swipeSource.loop = true;
            _swipeSource.spatialBlend = 0f;
            _swipeSource.volume = 0f;

            _swoosh = MakeSwoosh();
            _chop = MakeChop();
            _good = MakeGood();
            _overflow = MakeSweep("overflow", 0.16f, 240f, 150f, 20f);
            _bad = MakeBuzz();
            _tick = MakeTone("tick", 0.06f, 1000f, 60f);
            _go = MakeSweep("go", 0.25f, 880f, 1760f, 8f);
            _complete = MakeArpeggio("complete", new[] { 523f, 659f, 784f, 1046f }, 0.09f);
            _lose = MakeArpeggio("lose", new[] { 392f, 330f, 262f }, 0.2f);
            _click = MakeTone("click", 0.035f, 1500f, 80f);
            _launch = MakeSweep("launch", 0.09f, 180f, 420f, 25f);

            LoadClips();
        }

        void LoadClips()
        {
            foreach (var kind in new[] { "juicy", "crunchy", "soft", "hard" })
                _slice[kind] = LoadSet("Slice/slice_" + kind + "_", 3);
            _launchClips = LoadSet("Launch/launch_", 3);
            _swipeClips = LoadSet("Swipe/swipe_empty_", 3);
            _swipeLoop = Load("Swipe/swipe_loop");
            _swipeSource.clip = _swipeLoop;
            _missClips = LoadSet("Miss/miss_", 3);
            _overflowClips = LoadSet("Overflow/overflow_", 3);
            _errorClips = LoadSet("Error/error_", 3);
            _clickClips = LoadSet("UI/click_", 3);
            _paperClips = LoadSet("UI/paper_unfold_", 2);
            _counterSteps = LoadSet("Counter/counter_step_", 10, keepGaps: true);

            _timerTicks = new AudioClip[6];
            for (int s = 1; s <= 5; s++) _timerTicks[s] = Load("Timer/timer_tick_" + s);
            _timerEnd = Load("Timer/timer_end");
            _countdownTick = Load("Countdown/countdown_tick");
            _countdownGo = Load("Countdown/countdown_go");
            _taskFull = Load("Task/task_complete_full");
            _taskShort = Load("Task/task_complete_short");
            _lobbyMusic = Load("Music/lobby_loop");
            _roundMusic = Load("Music/game_loop");
        }

        /// <summary>Загружает клип и сразу подгружает его данные, чтобы первый запуск не опаздывал.</summary>
        static AudioClip Load(string path)
        {
            var clip = Resources.Load<AudioClip>(Root + path);
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            return clip;
        }

        /// <summary>Загружает prefix01..prefixNN. Без keepGaps отсутствующие файлы пропускаются.</summary>
        static AudioClip[] LoadSet(string prefix, int count, bool keepGaps = false)
        {
            var list = new List<AudioClip>();
            for (int i = 1; i <= count; i++)
            {
                var clip = Load(prefix + i.ToString("00"));
                if (clip != null || keepGaps) list.Add(clip);
            }
            return list.ToArray();
        }

        // ───────────────────────── 1. Разрез ─────────────────────────

        /// <summary>Разрез ингредиента: звук зависит от типа еды (сочная, хрустящая, мягкая, твёрдая).</summary>
        public void PlayChop(string ingredientId)
        {
            string kind = ingredientId != null && SliceKind.TryGetValue(ingredientId, out var k) ? k : "soft";
            if (!PlayRandom(_slice[kind], 0.7f)) Play(_chop, 0.55f, 0.12f);
        }

        public void PlayChop() => PlayChop(null);

        // ───────────────────────── 2. Вылет ─────────────────────────

        public void PlayLaunch()
        {
            if (!PlayRandom(_launchClips, 0.35f)) Play(_launch, 0.12f, 0.2f);
        }

        // ───────────────────────── 3. Счётчик ─────────────────────────

        /// <param name="fill">0..1 — насколько собран ингредиент; нота идёт вверх по гамме.</param>
        public void PlayGood(float fill)
        {
            int step = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(fill) * 9f), 0, 9);
            if (step < _counterSteps.Length && _counterSteps[step] != null) Play(_counterSteps[step], 0.45f, 0f);
            else Play(_good, 0.4f, 0.02f, 1f + 0.3f * fill);
        }

        // ───────────────────────── 4. Промах / взмах / перебор ─────────────────────────

        /// <summary>Нужный ингредиент упал, его не успели разрезать.</summary>
        public void PlayMiss()
        {
            if (!PlayRandom(_missClips, 0.4f)) Play(_lose, 0.25f, 0f, 1.3f);
        }

        /// <summary>Громкость петли взмаха на максимальной скорости (до множителя «Эффекты»).</summary>
        const float SwipeMaxVolume = 0.18f;
        /// <summary>Яркость «ветра»: медленно — глухой (фильтр 1,2 кГц), быстро — открытый (6 кГц).</summary>
        const float SwipeCutoffSlow = 1200f;
        const float SwipeCutoffFast = 6000f;
        /// <summary>Во сколько раз тише взмах, пока звучат комбо, таймер или отсчёт.</summary>
        const float SwipeDuck = 0.35f;

        /// <summary>Есть петля взмаха — тогда вместо отдельных «шшш» звучит непрерывный «воздух».</summary>
        public bool HasSwipeLoop => _swipeLoop != null;

        /// <summary>
        /// Взмах ножом, вызывается каждый кадр: пока мышь зажата, тихо звучит ветер,
        /// громче и ярче при быстром движении; после отпускания плавно затихает.
        /// Высота тона не меняется: петля настроена в ля минор под музыку.
        /// </summary>
        /// <param name="held">Зажата ли мышь (во время раунда).</param>
        /// <param name="speed">Скорость курсора, юнитов в секунду.</param>
        public void SetSwipe(bool held, float speed)
        {
            if (_swipeLoop == null) return;
            float k = held ? Mathf.Clamp01((speed - 2f) / 28f) : 0f;
            // Даже без движения слышен едва заметный фон — 20 % от максимума.
            _swipeVolume = held ? Mathf.Lerp(0.2f, 1f, Mathf.Sqrt(k)) * SwipeMaxVolume : 0f;
            _swipeCutoff = Mathf.Lerp(SwipeCutoffSlow, SwipeCutoffFast, k);
            if (held && !_swipeSource.isPlaying)
            {
                _swipeSource.volume = 0f;
                _swipeSource.Play();
                // Каждый раз с разного места петли — звук не повторяется одинаково.
                _swipeSource.time = Random.Range(0f, _swipeLoop.length * 0.9f);
            }
        }

        void UpdateSwipe(float dt)
        {
            if (!_swipeSource.isPlaying) return;
            float target = _swipeVolume * GameSettings.Effects;
            // Не перебиваем важные сигналы: на время комбо, таймера и отсчёта ветер приглушается.
            if (Time.unscaledTime < _duckUntil) target *= SwipeDuck;
            // Нарастает быстро (~80 мс), затихает мягче (~200 мс) — без щелчков и «шипения» рывками.
            float speed = target > _swipeSource.volume ? 12f : 5f;
            _swipeSource.volume = Mathf.Lerp(_swipeSource.volume, target, 1f - Mathf.Exp(-speed * dt));
            _swipeFilter.cutoffFrequency = Mathf.Lerp(_swipeFilter.cutoffFrequency, _swipeCutoff, 1f - Mathf.Exp(-8f * dt));
            if (target <= 0f && _swipeSource.volume < 0.002f) _swipeSource.Stop();
        }

        /// <summary>Быстрый взмах ножом — отдельный звук; используется, только если нет петли swipe_loop.</summary>
        public void PlaySwoosh()
        {
            if (!PlayRandom(_swipeClips, 0.35f)) Play(_swoosh, 0.3f, 0.15f);
        }

        /// <summary>Нужного уже хватает — перебор.</summary>
        public void PlayOverflow()
        {
            if (PlayRandom(_overflowClips, 0.5f)) return;
            if (!PlayRandom(_missClips, 0.5f)) Play(_overflow, 0.5f, 0.05f);
        }

        // ───────────────────────── 5. Ошибка ─────────────────────────

        /// <summary>Разрезан лишний ингредиент.</summary>
        public void PlayBad()
        {
            if (!PlayRandom(_errorClips, 0.55f)) Play(_bad, 0.45f, 0.03f);
        }

        // ───────────────────────── 6. Задание / комбо ─────────────────────────

        /// <summary>Блюдо собрано.</summary>
        public void PlayComplete()
        {
            if (_taskFull != null) Play(_taskFull, 0.6f, 0f);
            else Play(_complete, 0.45f, 0f);
        }

        /// <summary>Комбо: 3+ нужных одним движением.</summary>
        public void PlayCombo()
        {
            // Громче и с приглушением музыки, чтобы комбо было отчётливо слышно.
            PlayCue(_taskShort != null ? _taskShort : _go, _taskShort != null ? 0.95f : 0.5f, 1.2f);
        }

        // ───────────────────────── 7. Таймер конца раунда ─────────────────────────

        /// <param name="secondsLeft">5..1 — каждая секунда звучит на ступень выше.</param>
        public void PlayTimerTick(int secondsLeft)
        {
            var clip = secondsLeft >= 1 && secondsLeft <= 5 ? _timerTicks[secondsLeft] : null;
            PlayCue(clip != null ? clip : _tick, clip != null ? 0.9f : 0.5f, 1.1f);
        }

        /// <summary>Время раунда вышло.</summary>
        public void PlayTimeUp()
        {
            PlayCue(_timerEnd != null ? _timerEnd : _lose, _timerEnd != null ? 0.85f : 0.45f, 2.2f);
        }

        // ───────────────────────── 10. Отсчёт перед раундом ─────────────────────────

        public void PlayCountdownTick()
        {
            PlayCue(_countdownTick != null ? _countdownTick : _tick, _countdownTick != null ? 0.8f : 0.4f, 0.8f);
        }

        public void PlayCountdownGo()
        {
            PlayCue(_countdownGo != null ? _countdownGo : _go, _countdownGo != null ? 0.85f : 0.4f, 1.5f);
        }

        /// <summary>Важный сигнал: свой источник (не обрывается другими звуками) и приглушение музыки на duck секунд.</summary>
        void PlayCue(AudioClip clip, float volume, float duck)
        {
            if (clip == null) return;
            _cueSource.PlayOneShot(clip, volume * GameSettings.Effects);
            _duckUntil = Mathf.Max(_duckUntil, Time.unscaledTime + duck);
        }

        // ───────────────────────── Интерфейс ─────────────────────────

        /// <summary>
        /// Клик по кнопке — случайная из трёх вариаций. Кнопки играют его сами (GameUI.MakeButtonOn),
        /// а действия вроде «Заново» вызывают ещё раз — повтор в пределах 50 мс пропускается, чтобы не было двойного звука.
        /// </summary>
        public void PlayClick()
        {
            if (Time.unscaledTime - _lastClickTime < 0.05f) return;
            _lastClickTime = Time.unscaledTime;
            if (!PlayRandom(_clickClips, 0.5f)) Play(_click, 0.35f, 0.05f);
        }

        /// <summary>Шелест разворачиваемого (или сворачиваемого) листа рецепта.</summary>
        public void PlayPaper()
        {
            if (!PlayRandom(_paperClips, 0.6f)) PlayClick();
        }

        // ───────────────────────── 8–9. Музыка ─────────────────────────

        /// <summary>
        /// Плавно переключает фоновую музыку. Без файла game_loop в раунде играет лобби.
        /// Трек продолжается с места, где его остановили (например, после паузы),
        /// а restart: true запускает его с начала (новый раунд).
        /// </summary>
        public void PlayMusic(MusicTrack music, bool restart = false)
        {
            AudioClip clip = null;
            if (music == MusicTrack.Lobby) clip = _lobbyMusic;
            else if (music == MusicTrack.Round) clip = _roundMusic != null ? _roundMusic : _lobbyMusic;
            if (restart && clip != null) _musicPositions.Remove(clip);

            if (music == _music) return;
            _music = music;

            // Тот же трек уже играет (раунд без своей музыки) — не перезапускаем.
            if (clip != null && _musicA.clip == clip && _musicA.isPlaying) return;

            if (_musicA.clip != null && _musicA.isPlaying) _musicPositions[_musicA.clip] = _musicA.time;

            // A — текущий, B — затухающий
            (_musicA, _musicB) = (_musicB, _musicA);
            _musicA.clip = clip;
            _musicA.volume = 0f;
            if (clip == null)
            {
                _musicA.Stop();
                return;
            }
            _musicA.Play();
            if (_musicPositions.TryGetValue(clip, out float position) && position < clip.length) _musicA.time = position;
        }

        void Update()
        {
            UpdateSwipe(Time.unscaledDeltaTime);
            float step = Time.unscaledDeltaTime * MusicVolume / MusicFade;
            float level = MusicVolume * GameSettings.Music;
            float target = Time.unscaledTime < _duckUntil ? level * MusicDuck : level;
            // вниз — быстро (0,15 с), чтобы сигнал сразу был слышен; вверх — плавно
            float rate = _musicA.volume > target ? MusicVolume / 0.15f * Time.unscaledDeltaTime : step;
            if (_musicA.clip != null) _musicA.volume = Mathf.MoveTowards(_musicA.volume, target, rate);
            if (_musicB.isPlaying)
            {
                _musicB.volume = Mathf.MoveTowards(_musicB.volume, 0f, step);
                if (_musicB.volume <= 0f) _musicB.Stop();
            }
        }

        AudioSource NewMusicSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = 0;
            return source;
        }

        // ───────────────────────── Воспроизведение ─────────────────────────

        bool PlayRandom(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0) return false;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return false;
            // Без сдвига высоты: звуки настроены в тональность музыки (ля минор).
            Play(clip, volume, 0f);
            return true;
        }

        void Play(AudioClip clip, float volume, float pitchJitter, float pitch = 1f)
        {
            var source = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            source.clip = clip;
            source.volume = volume * GameSettings.Effects;
            source.pitch = pitch + Random.Range(-pitchJitter, pitchJitter);
            source.Play();
        }

        static AudioClip MakeSwoosh()
        {
            int n = Samples(0.22f);
            var data = new float[n];
            var rng = new System.Random(1);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float p = i / (float)n;
                float env = Mathf.Sin(Mathf.PI * p);
                float cut = Mathf.Lerp(0.3f, 0.05f, p);
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp1 += (noise - lp1) * cut;
                lp2 += (lp1 - lp2) * cut;
                data[i] = lp2 * env * env;
            }
            return Clip("swoosh", data);
        }

        static AudioClip MakeChop()
        {
            int n = Samples(0.15f);
            var data = new float[n];
            var rng = new System.Random(2);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 45f);
                phase += TwoPi * Mathf.Lerp(190f, 90f, t / 0.15f) / Rate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 30f);
                data[i] = noise * 0.6f + thump;
            }
            return Clip("chop", data);
        }

        static AudioClip MakeGood()
        {
            int n = Samples(0.2f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                bool second = t >= 0.06f;
                float f = second ? 1175f : 784f;
                float local = second ? t - 0.06f : t;
                phase += TwoPi * f / Rate;
                data[i] = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f)) * Mathf.Exp(-local * 20f);
            }
            return Clip("good", data);
        }

        static AudioClip MakeBuzz()
        {
            int n = Samples(0.32f);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float p = i / (float)n;
                float sq = Mathf.Sign(Mathf.Sin(TwoPi * 118f * t)) + Mathf.Sign(Mathf.Sin(TwoPi * 125f * t));
                lp += (sq - lp) * 0.25f;
                data[i] = lp * Mathf.Pow(1f - p, 1.5f);
            }
            return Clip("bad", data);
        }

        static AudioClip MakeTone(string name, float duration, float freq, float decay)
        {
            int n = Samples(duration);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = Mathf.Sin(TwoPi * freq * t) * Mathf.Exp(-t * decay);
            }
            return Clip(name, data);
        }

        static AudioClip MakeSweep(string name, float duration, float from, float to, float decay)
        {
            int n = Samples(duration);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                phase += TwoPi * Mathf.Lerp(from, to, i / (float)n) / Rate;
                data[i] = Mathf.Sin(phase) * Mathf.Exp(-t * decay);
            }
            return Clip(name, data);
        }

        static AudioClip MakeArpeggio(string name, float[] notes, float step)
        {
            float duration = step * (notes.Length - 1) + 0.35f;
            int n = Samples(duration);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = Samples(step * k);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)Rate;
                    data[i] += Mathf.Sin(TwoPi * notes[k] * t) * Mathf.Exp(-t * 9f);
                }
            }
            return Clip(name, data);
        }

        static int Samples(float seconds) => Mathf.CeilToInt(seconds * Rate);

        static AudioClip Clip(string name, float[] data)
        {
            float max = 0.0001f;
            foreach (float s in data) max = Mathf.Max(max, Mathf.Abs(s));
            float gain = 0.9f / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;

            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
