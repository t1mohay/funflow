using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RecipeSlicer
{
    // Только для README: записывает кадры мини-трейлера, управляя виртуальной мышью.
    // Запуск: Game.exe -trailer <папка>; монтаж в GIF — Tools/Trailer/make_trailer.py.
    public sealed class Trailer : MonoBehaviour
    {
        const int Fps = 20;
        const float SwipeLength = 2.8f;

        string _dir;
        Mouse _mouse;
        Vector2 _pos;
        bool _down;
        bool _recording;
        int _segment, _frame;

        GameManager _game;
        GameUI _ui;
        Camera _cam;
        readonly Dictionary<Ingredient, Vector2> _lastPos = new Dictionary<Ingredient, Vector2>();
        readonly Dictionary<Ingredient, Vector2> _velocity = new Dictionary<Ingredient, Vector2>();
        readonly Dictionary<Ingredient, Vector2> _accel = new Dictionary<Ingredient, Vector2>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-trailer") continue;
                Application.runInBackground = true;
                new GameObject("Trailer").AddComponent<Trailer>()._dir = args[i + 1];
            }
        }

        void Awake()
        {
            _mouse = InputSystem.AddDevice<Mouse>("TrailerMouse");
            // Игровое время идёт ровно 1/Fps за кадр, как бы долго ни сохранялись снимки.
            Time.captureFramerate = Fps;
        }

        void Update()
        {
            _mouse.MakeCurrent();
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = _pos }.WithButton(MouseButton.Left, _down));
            TrackIngredients();
            if (_recording) ScreenCapture.CaptureScreenshot(Path.Combine(_dir, $"s{_segment}_{_frame++:0000}.png"));
        }

        IEnumerator Start()
        {
            yield return Frames(12);
            _game = FindAnyObjectByType<GameManager>();
            _ui = FindAnyObjectByType<GameUI>();
            _cam = Camera.main;
            _pos = new Vector2(Screen.width * 0.8f, Screen.height * 0.22f);
            yield return Frames(4);

            // 1. Меню → доска заказов → лист Том Яма → «Начать готовку».
            Record(1);
            yield return Wait(0.8f);
            yield return MoveTo(ScreenOf(GameObject.Find("▶  Начать игру").transform), 0.6f);
            yield return Click();
            yield return Wait(0.9f);
            yield return MoveTo(ScreenOf(ChooseButton(2)), 0.6f);
            yield return Click();
            yield return Wait(1.6f);
            yield return MoveTo(ScreenOf(GameObject.Find("Начать готовку ▶").transform), 0.5f);
            yield return Wait(0.2f);
            // Кнопка срабатывает при отпускании — запись обрывается на нажатой кнопке.
            _down = true;
            yield return Frames(3);
            _recording = false;
            _down = false;
            yield return Frames(3);

            // 2. Середина раунда: половина рецепта уже собрана, ингредиенты летят плотнее.
            var round = Round();
            round.TimeLeft = 34f;
            foreach (var item in round.Recipe.Needed)
                for (int k = round.Collected(item.Ingredient); k < item.Count / 2; k++) round.Register(item.Ingredient);
            _ui.OnSliced(null, round);
            yield return Wait(1.25f); // начало отсчёта пропускаем
            Record(2);
            while (!_game.IsSlicing) yield return null; // досмотреть отсчёт «1… Режь!»
            yield return Bot(9f, 5f, untilDone: true);

            // 3. Остался один ингредиент: последний разрез, «Блюдо готово!» и результат.
            // (Если бот успел собрать рецепт сам — просто продолжаем запись до результата.)
            if (_game.IsSlicing)
            {
                _recording = false;
                var last = round.Recipe.Needed[0].Ingredient;
                foreach (var item in round.Recipe.Needed)
                {
                    int target = item.Ingredient == last ? item.Count - 1 : item.Count;
                    for (int k = round.Collected(item.Ingredient); k < target; k++) round.Register(item.Ingredient);
                }
                _ui.OnSliced(null, round);
                yield return Frames(2);
                Record(3);
                yield return Bot(8f, -1f, untilDone: true);
            }
            yield return Wait(3.4f);
            _recording = false;
            yield return Frames(4);
            Application.Quit();
        }

        // ───────────────────────── Мышь и запись ─────────────────────────

        void Record(int segment)
        {
            _segment = segment;
            _frame = 0;
            _recording = true;
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        static IEnumerator Wait(float seconds) => Frames(Mathf.RoundToInt(seconds * Fps));

        IEnumerator MoveTo(Vector2 target, float seconds)
        {
            Vector2 from = _pos;
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps));
            for (int i = 1; i <= n; i++)
            {
                float k = i / (float)n;
                _pos = Vector2.Lerp(from, target, k * k * (3f - 2f * k));
                yield return null;
            }
        }

        IEnumerator Click()
        {
            _down = true;
            yield return Frames(3);
            _down = false;
            yield return Frames(2);
        }

        static Vector2 ScreenOf(Transform t) => t.position;

        /// <summary>Кнопка «Выбрать» под листом номер index на доске заказов.</summary>
        static Transform ChooseButton(int index)
        {
            var board = GameObject.Find("Board").transform;
            int found = 0;
            // Кнопки лежат внутри подвесов листов, поэтому ищем по всей доске, а не только среди прямых детей.
            foreach (var child in board.GetComponentsInChildren<Transform>())
                if (child.name == "Choose" && found++ == index) return child;
            return board;
        }

        RoundState Round() =>
            (RoundState)typeof(GameManager).GetField("_round", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_game);

        // ───────────────────────── Бот-повар ─────────────────────────

        void TrackIngredients()
        {
            float dt = 1f / Fps;
            foreach (var ing in FindObjectsByType<Ingredient>())
            {
                Vector2 p = ing.Position;
                if (_lastPos.TryGetValue(ing, out var last))
                {
                    var v = (p - last) / dt;
                    if (_velocity.TryGetValue(ing, out var lastV)) _accel[ing] = (v - lastV) / dt;
                    _velocity[ing] = v;
                }
                _lastPos[ing] = p;
            }
        }

        Vector2 Predict(Ingredient ing, float t)
        {
            Vector2 p = ing.Position;
            _velocity.TryGetValue(ing, out var v);
            _accel.TryGetValue(ing, out var a);
            return p + v * t + 0.5f * a * t * t;
        }

        /// <param name="mistakeAt">Через сколько секунд один раз нарочно разрезать лишнее (меньше нуля — никогда).</param>
        /// <param name="untilDone">Закончить, когда раунд завершится.</param>
        IEnumerator Bot(float duration, float mistakeAt, bool untilDone = false)
        {
            float start = Time.time;
            bool mistakeDone = mistakeAt < 0f;
            float rest = 0.1f;
            while (Time.time - start < duration)
            {
                if (untilDone && !_game.IsSlicing) yield break;
                if (rest > 0f)
                {
                    rest -= 1f / Fps;
                    yield return null;
                    continue;
                }

                bool mistake = !mistakeDone && Time.time - start >= mistakeAt;
                if (!PlanSwipe(mistake, out var a, out var b, out int frames))
                {
                    yield return null;
                    continue;
                }
                if (mistake) mistakeDone = true;

                // Подвести нож к началу разреза, провести и отпустить.
                yield return MoveTo(_cam.WorldToScreenPoint(a), 0.06f);
                Vector2 sa = _cam.WorldToScreenPoint(a), sb = _cam.WorldToScreenPoint(b);
                _down = true;
                for (int i = 1; i <= frames; i++)
                {
                    _pos = Vector2.Lerp(sa, sb, i / (float)frames);
                    yield return null;
                }
                _down = false;
                rest = Random.Range(0.04f, 0.16f);
            }
        }

        bool PlanSwipe(bool mistake, out Vector2 a, out Vector2 b, out int frames)
        {
            a = b = default;
            frames = 5;
            var round = Round();
            var recipe = round.Recipe;
            var area = _game.PlayArea;
            const float lead = 0.2f; // подлёт ножа + половина разреза

            var good = new List<(Ingredient ing, Vector2 p)>();
            var bad = new List<(Ingredient ing, Vector2 p)>();
            foreach (var ing in FindObjectsByType<Ingredient>())
            {
                if (ing == null || ing.IsSliced || !_velocity.ContainsKey(ing)) continue;
                var p = Predict(ing, lead);
                bool needed = round.Collected(ing.Def) < recipe.NeededCount(ing.Def);
                bool visible = p.x > area.xMin + 0.3f && p.x < area.xMax - 0.3f && p.y > area.yMin + 0.8f && p.y < area.yMax - 0.5f;
                if (needed && visible) good.Add((ing, p));
                else bad.Add((ing, p));
            }

            if (mistake)
            {
                foreach (var (ing, p) in bad)
                {
                    if (recipe.NeededCount(ing.Def) > 0) continue;
                    if (p.x < area.xMin + 0.5f || p.x > area.xMax - 0.5f || p.y < area.yMin + 1.5f || p.y > area.yMax - 1.2f) continue;
                    var dir = Quaternion.Euler(0f, 0f, 20f) * Vector2.right;
                    a = p - (Vector2)dir * SwipeLength * 0.5f;
                    b = p + (Vector2)dir * SwipeLength * 0.5f;
                    return true;
                }
                return false;
            }

            // Ближе к ножу — раньше; если рядом есть ещё нужные — режем их одним движением (комбо).
            Vector2 knife = _cam.ScreenToWorldPoint(_pos);
            good.Sort((x, y) => Vector2.Distance(x.p, knife).CompareTo(Vector2.Distance(y.p, knife)));
            foreach (var (ing, p) in good)
            {
                foreach (var (other, q) in good)
                {
                    if (other == ing || Vector2.Distance(p, q) > 2.4f) continue;
                    var dir = (q - p).normalized;
                    var from = p - dir * 1.1f;
                    var to = q + dir * 1.1f;
                    if (Clear(from, to, bad))
                    {
                        a = from;
                        b = to;
                        frames = 6;
                        return true;
                    }
                }
                foreach (float angle in new[] { 15f, -15f, 40f, -40f, 65f, -65f, 90f })
                {
                    var dir = (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.right);
                    if (Random.value < 0.5f) dir = -dir;
                    var from = p - dir * SwipeLength * 0.5f;
                    var to = p + dir * SwipeLength * 0.5f;
                    if (!Clear(from, to, bad)) continue;
                    a = from;
                    b = to;
                    return true;
                }
            }
            return false;
        }

        static bool Clear(Vector2 from, Vector2 to, List<(Ingredient ing, Vector2 p)> bad)
        {
            foreach (var (ing, p) in bad)
                if (Distance(p, from, to) < ing.HitRadius + 0.2f) return false;
            return true;
        }

        static float Distance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
