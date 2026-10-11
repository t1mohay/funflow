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
        const float TrailWidth = 0.24f;

        GameManager _game;
        Camera _cam;
        LineRenderer _line;
        readonly List<Vector3> _points = new List<Vector3>();
        readonly List<float> _times = new List<float>();
        bool _wasPressed;
        Vector2 _prev;
        float _swooshCooldown;

        public void Init(GameManager game, Camera cam)
        {
            _game = game;
            _cam = cam;

            _line = gameObject.AddComponent<LineRenderer>();
            _line.sharedMaterial = Art.LineMaterial;
            _line.useWorldSpace = true;
            _line.numCapVertices = 4;
            _line.numCornerVertices = 2;
            _line.sortingOrder = 5000;
            _line.positionCount = 0;
            _line.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, TrailWidth));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.75f, 0.9f, 1f), 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 1f) });
            _line.colorGradient = gradient;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _swooshCooldown -= dt;

            bool pressed = InputUtil.PointerHeld(out var screen);
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

            _line.positionCount = _points.Count;
            for (int i = 0; i < _points.Count; i++) _line.SetPosition(i, _points[i]);
        }
    }
}
