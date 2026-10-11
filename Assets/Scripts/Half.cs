using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>Половинка разрезанного ингредиента: падает, крутится и тает.</summary>
    public sealed class Half : MonoBehaviour
    {
        const float FadeStart = 0.6f;
        const float FadeTime = 0.6f;

        SpriteRenderer _renderer;
        Vector2 _velocity;
        float _gravity;
        float _speed;
        float _spin;
        float _bottom;
        float _age;

        public void Init(Vector2 velocity, float gravity, float speed, float spin, float bottom)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _velocity = velocity;
            _gravity = gravity;
            _speed = speed;
            _spin = spin;
            _bottom = bottom;
        }

        void Update()
        {
            float dt = Time.deltaTime * _speed;
            _age += Time.deltaTime;
            _velocity.y -= _gravity * dt;
            transform.position += (Vector3)(_velocity * dt);
            transform.Rotate(0f, 0f, _spin * dt);

            if (_age > FadeStart)
            {
                var c = _renderer.color;
                c.a = Mathf.Clamp01(1f - (_age - FadeStart) / FadeTime);
                _renderer.color = c;
            }

            if (_age > FadeStart + FadeTime || transform.position.y < _bottom)
                Destroy(gameObject);
        }

        // Спрайт половинки создан под этот разрез — удаляем вместе с объектом.
        void OnDestroy()
        {
            if (_renderer != null && _renderer.sprite != null) Destroy(_renderer.sprite);
        }
    }
}
