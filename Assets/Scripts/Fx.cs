using System.Collections.Generic;
using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>Брызги-частицы, кляксы сока на стене и тряска камеры.</summary>
    public sealed class Fx : MonoBehaviour
    {
        const int ParticleSortingOrder = 4000;
        /// <summary>Кляксы — на стене: над фоном (−100), под ингредиентами (от 10).</summary>
        const int SplatSortingOrder = -50;
        const int MaxSplats = 24;
        const float SplatGrow = 0.12f;
        const float SplatHold = 5f;
        const float SplatFade = 1.5f;

        sealed class Splat
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Color Color;
            public float Size;
            public float Age;
        }

        readonly List<Splat> _splats = new List<Splat>();
        readonly Stack<Splat> _splatPool = new Stack<Splat>();

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

        /// <summary>
        /// Клякса сока на стене за разрезанным ингредиентом, в его цвете. Быстро «шлёпается»,
        /// держится несколько секунд и медленно исчезает. Самые старые убираются, если их слишком много.
        /// </summary>
        public void SplatAt(Vector2 position, Color color, float size)
        {
            if (_splats.Count >= MaxSplats) RemoveSplat(0);
            var s = _splatPool.Count > 0 ? _splatPool.Pop() : NewSplat();
            s.Renderer.sprite = Art.Splat(Random.Range(0, Art.SplatVariants));
            // Сок на стене чуть темнее самого ингредиента — как фон, он тоже затемнён.
            s.Color = Color.Lerp(color, Color.black, 0.18f);
            s.Color.a = 0.55f; // полупрозрачная: сквозь сок видна плитка
            s.Size = size * Random.Range(0.85f, 1.15f);
            s.Age = 0f;
            s.Transform.position = new Vector3(position.x, position.y, 0f);
            s.Transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            s.Transform.localScale = Vector3.one * (s.Size * 0.6f);
            s.Renderer.color = s.Color;
            s.Transform.gameObject.SetActive(true);
            _splats.Add(s);
        }

        /// <summary>Убрать все кляксы — в начале раунда и при выходе в меню.</summary>
        public void ClearSplats()
        {
            while (_splats.Count > 0) RemoveSplat(_splats.Count - 1);
        }

        Splat NewSplat()
        {
            var go = new GameObject("Splat");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SplatSortingOrder;
            return new Splat { Transform = go.transform, Renderer = sr };
        }

        void RemoveSplat(int index)
        {
            var s = _splats[index];
            s.Transform.gameObject.SetActive(false);
            _splats.RemoveAt(index);
            _splatPool.Push(s);
        }

        void UpdateSplats(float dt)
        {
            for (int i = _splats.Count - 1; i >= 0; i--)
            {
                var s = _splats[i];
                s.Age += dt;
                if (s.Age >= SplatGrow + SplatHold + SplatFade)
                {
                    RemoveSplat(i);
                    continue;
                }
                // Шлепок: чуть больше конечного размера и обратно.
                float grow = Mathf.Clamp01(s.Age / SplatGrow);
                float scale = grow < 1f ? Mathf.Lerp(0.6f, 1.06f, grow) : Mathf.Lerp(1.06f, 1f, Mathf.Clamp01((s.Age - SplatGrow) / 0.1f));
                s.Transform.localScale = Vector3.one * (s.Size * scale);
                float fade = Mathf.Clamp01((s.Age - SplatGrow - SplatHold) / SplatFade);
                var c = s.Color;
                c.a *= 1f - fade * fade;
                s.Renderer.color = c;
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
            UpdateSplats(dt);
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
