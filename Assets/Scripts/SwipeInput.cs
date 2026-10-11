using System.Collections.Generic;
using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>Свайп мышью или пальцем: рисует след и отдаёт отрезки движения в GameManager.</summary>
    public sealed class SwipeInput : MonoBehaviour
    {
        const float MinSliceSpeed = 7f;     // юнитов в секунду: медленное вождение не режет
        const float SwooshSpeed = 16f;
        const float TrailLifetime = 0.12f;
        /// <summary>Толщина белого ядра «клинка» в самом широком месте.</summary>
        const float CoreWidth = 0.16f;
        /// <summary>Толщина голубоватого свечения вокруг ядра.</summary>
        const float GlowWidth = 0.34f;
        /// <summary>Сколько промежуточных точек добавлять между соседними точками мыши (Catmull-Rom).</summary>
        const int Subdivisions = 4;

        GameManager _game;
        Camera _cam;
        LineRenderer _line, _glow;
        readonly List<Vector3> _smooth = new List<Vector3>();
        readonly List<Vector3> _points = new List<Vector3>();
        readonly List<float> _times = new List<float>();
        bool _wasPressed;
        Vector2 _prev;
        float _swooshCooldown;

        public void Init(GameManager game, Camera cam)
        {
            _game = game;
            _cam = cam;

            // След как у клинка во Fruit Ninja: хвост сходится в иглу, голова — острый кончик,
            // тонкое белое ядро поверх мягкого голубоватого свечения.
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(transform, false);
            _glow = NewTrail(glowGo, 4999, GlowWidth, 0.85f, 0.2f,
                new Color(0.62f, 0.85f, 1f), new Color(0.85f, 0.95f, 1f), 0f, 0.35f);
            _line = NewTrail(gameObject, 5000, CoreWidth, 0.88f, 0f,
                new Color(0.88f, 0.96f, 1f), Color.white, 0.15f, 1f);
        }

        /// <summary>
        /// Линия следа: ширина растёт от нуля в хвосте до максимума у peak (доля длины) и снова сужается к голове
        /// до headWidth (доля от максимума). Концы без скруглений — поэтому след выглядит острым.
        /// </summary>
        static LineRenderer NewTrail(GameObject go, int order, float width, float peak, float headWidth,
            Color tailColor, Color headColor, float tailAlpha, float headAlpha)
        {
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Art.LineMaterial;
            line.useWorldSpace = true;
            line.numCapVertices = 0;
            line.numCornerVertices = 3;
            line.sortingOrder = order;
            line.positionCount = 0;
            line.widthMultiplier = width;
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 1.6f),
                new Keyframe(peak, 1f, 0f, 0f),
                new Keyframe(1f, headWidth));
            line.widthCurve = curve;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(tailColor, 0f), new GradientColorKey(headColor, 1f) },
                new[] { new GradientAlphaKey(tailAlpha, 0f), new GradientAlphaKey(headAlpha, 0.6f), new GradientAlphaKey(headAlpha, 1f) });
            line.colorGradient = gradient;
            return line;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _swooshCooldown -= dt;

            // В паузе мышь нужна для меню: след не рисуем, звук взмаха не играем.
            bool pressed = InputUtil.PointerHeld(out var screen) && !_game.IsPaused;
            if (pressed)
            {
                Vector2 world = _cam.ScreenToWorldPoint(screen);
                if (!_wasPressed)
                {
                    _points.Clear();
                    _times.Clear();
                }
                else
                {
                    float distance = Vector2.Distance(_prev, world);
                    float speed = distance / dt;
                    if (speed >= MinSliceSpeed)
                    {
                        _game.OnSwipe(_prev, world);
                        if (speed >= SwooshSpeed && _swooshCooldown <= 0f)
                        {
                            _game.Sfx.PlaySwoosh();
                            _swooshCooldown = 0.25f;
                        }
                    }
                }
                _prev = world;
                _points.Add(world);
                _times.Add(now);
            }
            _wasPressed = pressed;

            while (_times.Count > 0 && now - _times[0] > TrailLifetime)
            {
                _times.RemoveAt(0);
                _points.RemoveAt(0);
            }

            BuildSmooth();
            SetTrail(_line);
            SetTrail(_glow);
        }

        void SetTrail(LineRenderer line)
        {
            line.positionCount = _smooth.Count;
            for (int i = 0; i < _smooth.Count; i++) line.SetPosition(i, _smooth[i]);
        }

        /// <summary>Сглаживает след: между точками мыши — кривая Catmull-Rom, чтобы на быстрых дугах не было изломов.</summary>
        void BuildSmooth()
        {
            _smooth.Clear();
            int n = _points.Count;
            if (n < 3)
            {
                _smooth.AddRange(_points);
                return;
            }
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = _points[Mathf.Max(i - 1, 0)];
                Vector3 p1 = _points[i];
                Vector3 p2 = _points[i + 1];
                Vector3 p3 = _points[Mathf.Min(i + 2, n - 1)];
                for (int k = 0; k < Subdivisions; k++)
                {
                    float t = k / (float)Subdivisions;
                    float t2 = t * t, t3 = t2 * t;
                    _smooth.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
                }
            }
            _smooth.Add(_points[n - 1]);
        }
    }
}
