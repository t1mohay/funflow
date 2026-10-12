using System.Collections.Generic;
using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>Брызги-частицы и тряска камеры.</summary>
    public sealed class Fx : MonoBehaviour
    {
        const int ParticleSortingOrder = 4000;

        sealed class Particle
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public Color Color;
            public float Age;
            public float Life;
            public float Size;
        }

        readonly List<Particle> _live = new List<Particle>();
        readonly Stack<Particle> _pool = new Stack<Particle>();

        Camera _cam;
        Vector3 _camBase;
        float _shakeTime;
        float _shakeDuration;
        float _shakeAmp;

        public void Init(Camera cam)
        {
            _cam = cam;
            _camBase = cam.transform.position;
        }

        public void Burst(Vector2 position, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var p = Get();
                float angle = Random.value * Mathf.PI * 2f;
                float speed = Random.Range(2f, 7.5f);
                p.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed + Vector2.up * 1.5f;
                p.Color = Color.Lerp(color, Color.white, Random.value * 0.35f);
                p.Age = 0f;
                p.Life = Random.Range(0.35f, 0.75f);
                p.Size = Random.Range(0.08f, 0.24f);
                p.Transform.position = position;
                p.Transform.localScale = Vector3.one * p.Size;
                p.Renderer.color = p.Color;
                p.Transform.gameObject.SetActive(true);
                _live.Add(p);
            }
        }

        public void Shake(float amplitude, float duration)
        {
            float current = _shakeDuration > 0f ? _shakeAmp * Mathf.Clamp01(_shakeTime / _shakeDuration) : 0f;
            if (amplitude < current) return;
            _shakeAmp = amplitude;
            _shakeDuration = duration;
            _shakeTime = duration;
        }

        Particle Get()
        {
            if (_pool.Count > 0) return _pool.Pop();

            var go = new GameObject("Particle");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Circle;
            sr.sortingOrder = ParticleSortingOrder;
            return new Particle { Transform = go.transform, Renderer = sr };
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Transform.gameObject.SetActive(false);
                    _pool.Push(p);
                    _live[i] = _live[_live.Count - 1];
                    _live.RemoveAt(_live.Count - 1);
                    continue;
                }

                p.Velocity.y -= 14f * dt;
                p.Velocity *= 1f - 2f * dt;
                p.Transform.position += (Vector3)(p.Velocity * dt);
                float k = p.Age / p.Life;
                p.Transform.localScale = Vector3.one * (p.Size * (1f - k * 0.6f));
                var c = p.Color;
                c.a = 1f - k * k;
                p.Renderer.color = c;
            }
        }

        void LateUpdate()
        {
            if (_shakeTime > 0f)
            {
                _shakeTime -= UiTime.Delta;
                float k = Mathf.Clamp01(_shakeTime / _shakeDuration);
                _cam.transform.position = _camBase + (Vector3)(Random.insideUnitCircle * (_shakeAmp * k));
            }
            else
            {
                _cam.transform.position = _camBase;
            }
        }
    }
}
