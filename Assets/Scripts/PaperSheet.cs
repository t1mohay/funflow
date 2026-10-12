using UnityEngine;
using UnityEngine.UI;

namespace RecipeSlicer
{
    /// <summary>
    /// Лист бумаги с вырезанным правым нижним углом: на его месте PaperCurl рисует загнутый уголок.
    /// Картинка листа (UI/recipe_paper) натягивается на весь прямоугольник, угол отрезается по диагонали
    /// на Curl единиц от вершины — так уголок можно плавно анимировать, не перерисовывая текстуру.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperSheet : MaskableGraphic
    {
        public Texture Texture;
        [SerializeField] float _curl = 70f;

        public override Texture mainTexture => Texture != null ? Texture : s_WhiteTexture;

        /// <summary>Сколько единиц от угла загнуто (по каждой из сторон).</summary>
        public float Curl
        {
            get => _curl;
            set
            {
                if (Mathf.Abs(value - _curl) < 0.01f) return;
                _curl = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (r.width <= 1f || r.height <= 1f) return;
            float c = Mathf.Clamp(_curl, 0f, Mathf.Min(r.width, r.height) * 0.5f);

            // выпуклый пятиугольник: прямоугольник без треугольника в правом нижнем углу
            var points = new[]
            {
                new Vector2(r.xMin, r.yMin),
                new Vector2(r.xMax - c, r.yMin),
                new Vector2(r.xMax, r.yMin + c),
                new Vector2(r.xMax, r.yMax),
                new Vector2(r.xMin, r.yMax),
            };
            Color32 c32 = color;
            foreach (var p in points)
                vh.AddVert(p, c32, new Vector2((p.x - r.xMin) / r.width, (p.y - r.yMin) / r.height));
            for (int i = 1; i < points.Length - 1; i++)
                vh.AddTriangle(0, i, i + 1);
        }
    }
}
