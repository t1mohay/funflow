using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>
    /// Половинка разрезанного ингредиента: сначала разлетается от удара, потом по дуге летит в ёмкость
    /// блюда и исчезает в ней. Если ёмкости нет — просто падает, крутится и тает.
    /// </summary>
    public sealed class Half : MonoBehaviour
    {
        const float PopTime = 0.14f;
        const float FlyTime = 0.5f;
        const float EndScale = 0.35f;
        const float FadeStart = 0.6f;
        const float FadeTime = 0.6f;

        SpriteRenderer _renderer;
        Container _container;
        SliceResult _result;
        Color _juice;
        Vector2 _velocity;
        float _gravity;
        float _speed;
        float _spin;
        float _bottom;
        float _age;

        bool _flying;
        Vector3 _from, _control, _to;
        float _startScale;

        public void Init(Vector2 velocity, float gravity, float speed, float spin, float bottom,
            Container container, SliceResult result, Color juice)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _velocity = velocity;
            _gravity = gravity;
            _speed = speed;
            _spin = spin;
            _bottom = bottom;
            _container = container != null && container.isActiveAndEnabled ? container : null;
            _result = result;
            _juice = juice;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            transform.Rotate(0f, 0f, _spin * dt * _speed);

            if (_flying)
            {
                Fly();
                return;
            }

            float sdt = dt * _speed;
            _velocity.y -= _gravity * sdt;
            transform.position += (Vector3)(_velocity * sdt);

            if (_container != null)
            {
                if (_age >= PopTime) StartFlight();
                return;
            }

            if (_age > FadeStart)
            {
                var c = _renderer.color;
                c.a = Mathf.Clamp01(1f - (_age - FadeStart) / FadeTime);
                _renderer.color = c;
            }
            if (_age > FadeStart + FadeTime || transform.position.y < _bottom)
                Destroy(gameObject);
        }

        void StartFlight()
        {
            _flying = true;
            _from = transform.position;
            _to = _container.MouthPosition + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.1f, 0.1f), 0f);
            // Дуга поднимается над ёмкостью: кусок падает в неё сверху, а не влетает сбоку.
            float top = Mathf.Max(_from.y, _to.y) + 1.6f;
            _control = new Vector3((_from.x + _to.x) * 0.5f + _velocity.x * 0.12f, top, 0f);
            _startScale = transform.localScale.x;
        }

        void Fly()
        {
            float t = Mathf.Clamp01((_age - PopTime) / FlyTime);
            float e = t * t; // разгоняется к ёмкости
            float u = 1f - e;
            transform.position = u * u * _from + 2f * u * e * _control + e * e * _to;
            transform.localScale = Vector3.one * (_startScale * Mathf.Lerp(1f, EndScale, e));
            if (t > 0.8f)
            {
                var c = _renderer.color;
                c.a = 1f - (t - 0.8f) / 0.2f;
                _renderer.color = c;
            }

            if (t >= 1f)
            {
                if (_container != null) _container.Absorb(_result, _juice);
                Destroy(gameObject);
            }
        }

        // Спрайт половинки создан под этот разрез — удаляем вместе с объектом.
        void OnDestroy()
        {
            if (_renderer != null && _renderer.sprite != null) Destroy(_renderer.sprite);
        }
    }
}
