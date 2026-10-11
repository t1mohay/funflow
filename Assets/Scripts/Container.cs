using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>
    /// Ёмкость текущего блюда на столе (миска, тесто, кастрюля): в неё летят все разрезанные ингредиенты.
    /// Поверх пустой ёмкости постепенно проявляется готовое блюдо — по мере сбора рецепта.
    /// Картинки: Resources/Dishes/&lt;id&gt;_empty и Resources/Dishes/&lt;id&gt;.
    /// </summary>
    public sealed class Container : MonoBehaviour
    {
        public const int SortingOrder = 5;

        /// <summary>Ширина ёмкости в юнитах (экран — 10 юнитов по высоте).</summary>
        const float Width = 4.8f;
        /// <summary>Где стоит дно ёмкости — над нижним краем экрана (на столешнице).</summary>
        const float BottomAboveScreenEdge = 0.55f;
        /// <summary>Прозрачные поля снизу у картинок ёмкостей — доля размера.</summary>
        const float ImageBottomPadding = 0.1f;

        static readonly Color WrongTint = new Color(1f, 0.45f, 0.4f);

        Fx _fx;
        Transform _visual;
        SpriteRenderer _empty, _full;
        float _progress, _shownProgress;
        float _bounceAge = 99f, _bounceAmp, _flash;

        /// <summary>Куда падают куски — чуть выше центра ёмкости.</summary>
        public Vector3 MouthPosition => transform.position + new Vector3(0f, Width * 0.12f, 0f);

        public void Init(Fx fx)
        {
            _fx = fx;
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            _empty = NewPart("Empty", SortingOrder);
            _full = NewPart("Full", SortingOrder + 1);
            gameObject.SetActive(false);
        }

        SpriteRenderer NewPart(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_visual, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Поставить ёмкость нужного блюда, пустую.</summary>
        public void SetRecipe(RecipeDef recipe)
        {
            SetSprite(_empty, Art.LoadSprite("Dishes/" + recipe.Id + "_empty"));
            SetSprite(_full, Art.DishSprite(recipe));
            _progress = _shownProgress = 0f;
            _bounceAge = 99f;
            _flash = 0f;
            gameObject.SetActive(_empty.sprite != null || _full.sprite != null);
            ApplyColors();
        }

        static void SetSprite(SpriteRenderer sr, Sprite sprite)
        {
            sr.sprite = sprite;
            if (sprite != null) sr.transform.localScale = Vector3.one * (Width / sprite.bounds.size.x);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>0..1 — насколько собран рецепт; столько готового блюда видно в ёмкости.</summary>
        public void SetProgress(float progress) => _progress = Mathf.Clamp01(progress);

        /// <summary>Поставить дном на столешницу у нижнего края экрана.</summary>
        public void Place(float viewHalfHeight)
        {
            float bottom = -viewHalfHeight + BottomAboveScreenEdge;
            transform.position = new Vector3(0f, bottom + (0.5f - ImageBottomPadding) * Width, 0f);
        }

        /// <summary>Кусок упал в ёмкость.</summary>
        public void Absorb(SliceResult result, Color juice)
        {
            bool wrong = result == SliceResult.Wrong;
            _bounceAge = 0f;
            _bounceAmp = wrong ? 0.12f : 0.06f;
            if (wrong) _flash = 1f;
            _fx.Burst(MouthPosition, juice, 5);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _shownProgress = Mathf.MoveTowards(_shownProgress, _progress, dt * 1.5f);

            _bounceAge += dt;
            float s = Mathf.Sin(_bounceAge * 26f) * Mathf.Exp(-_bounceAge * 7f) * _bounceAmp;
            _visual.localScale = new Vector3(1f + s, 1f - s, 1f);

            _flash = Mathf.MoveTowards(_flash, 0f, dt * 2.5f);
            ApplyColors();
        }

        void ApplyColors()
        {
            var tint = Color.Lerp(Color.white, WrongTint, _flash);
            _empty.color = tint;
            var full = tint;
            full.a = _shownProgress;
            _full.color = full;
        }
    }
}
