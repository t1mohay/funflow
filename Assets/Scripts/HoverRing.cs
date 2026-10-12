using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RecipeSlicer
{
    /// <summary>
    /// Анимированная обводка кнопки при наведении: тонкая пунктирная рамка вокруг кнопки,
    /// штрихи бегут по контуру по часовой стрелке. Цвет — как у заливки кнопки.
    /// Рисуется своим мешем (без текстур), время — unscaled, поэтому работает и в паузе.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HoverRing : MaskableGraphic
    {
        /// <summary>Толщина линии, в единицах холста 1920×1080.</summary>
        public float Thickness = 3f;
        /// <summary>Радиус скругления углов рамки (для круглых кнопок берётся половина высоты).</summary>
        public float Radius = 30f;
        /// <summary>Длина штриха + промежутка (подгоняется, чтобы по периметру уложилось целое число).</summary>
        public float Period = 34f;
        /// <summary>Доля штриха в периоде.</summary>
        public float Duty = 0.6f;
        /// <summary>Скорость бега штрихов, единиц холста в секунду.</summary>
        public float Speed = 60f;
        /// <summary>Время появления и исчезновения, с.</summary>
        public float Fade = 0.15f;

        const float Step = 4f;   // шаг разбиения контура — штрихи с мягкими краями

        float _phase, _visible;
        bool _hover;

        /// <summary>Добавляет рамку вокруг кнопки и обработчик наведения на саму кнопку.</summary>
        /// <param name="offset">Отступ рамки наружу от края кнопки.</param>
        public static HoverRing Attach(RectTransform button, Color color, float offset = 7f)
        {
            var go = new GameObject("HoverRing", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(button, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-offset, -offset);
            rt.offsetMax = new Vector2(offset, offset);

            var ring = go.AddComponent<HoverRing>();
            ring.color = color;
            ring.raycastTarget = false;
            button.gameObject.AddComponent<HoverRingTrigger>().Ring = ring;
            return ring;
        }

        /// <summary>Цвет рамки по цвету заливки кнопки: тот же оттенок, чуть светлее, непрозрачный.</summary>
        public static Color ColorFor(Color fill)
        {
            var c = Color.Lerp(fill, Color.white, 0.25f);
            c.a = 1f;
            return c;
        }

        public void SetHover(bool hover, bool instant = false)
        {
            _hover = hover;
            if (instant)
            {
                _visible = hover ? 1f : 0f;
                SetVerticesDirty();
            }
        }

        void Update()
        {
            if (!_hover && _visible <= 0f) return;
            float dt = UiTime.Delta;
            _visible = Mathf.MoveTowards(_visible, _hover ? 1f : 0f, dt / Mathf.Max(Fade, 0.01f));
            _phase += Speed * dt;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_visible <= 0f) return;

            var r = rectTransform.rect;
            if (r.width <= 1f || r.height <= 1f) return;
            float half = Mathf.Min(r.width, r.height) * 0.5f;
            // круглая кнопка (стрелки настроек) — круглая рамка
            float radius = Mathf.Abs(r.width - r.height) < 2f ? half : Mathf.Min(Radius, half);

            // ── точки контура по часовой стрелке, с нормалями наружу
            var points = new System.Collections.Generic.List<Vector2>(512);
            var normals = new System.Collections.Generic.List<Vector2>(512);
            float xMin = r.xMin, xMax = r.xMax, yMin = r.yMin, yMax = r.yMax;
            Line(points, normals, new Vector2(xMin + radius, yMax), new Vector2(xMax - radius, yMax), Vector2.up);
            Arc(points, normals, new Vector2(xMax - radius, yMax - radius), radius, 90f, 0f);
            Line(points, normals, new Vector2(xMax, yMax - radius), new Vector2(xMax, yMin + radius), Vector2.right);
            Arc(points, normals, new Vector2(xMax - radius, yMin + radius), radius, 0f, -90f);
            Line(points, normals, new Vector2(xMax - radius, yMin), new Vector2(xMin + radius, yMin), Vector2.down);
            Arc(points, normals, new Vector2(xMin + radius, yMin + radius), radius, -90f, -180f);
            Line(points, normals, new Vector2(xMin, yMin + radius), new Vector2(xMin, yMax - radius), Vector2.left);
            Arc(points, normals, new Vector2(xMin + radius, yMax - radius), radius, 180f, 90f);

            int n = points.Count;
            if (n < 3) return;

            // длина по контуру до каждой точки
            var dist = new float[n + 1];
            for (int i = 1; i <= n; i++) dist[i] = dist[i - 1] + Vector2.Distance(points[i - 1], points[i % n]);
            float perimeter = dist[n];

            // целое число штрихов по периметру — шов незаметен
            int count = Mathf.Max(4, Mathf.RoundToInt(perimeter / Period));
            float period = perimeter / count;
            float dash = period * Duty;
            const float soft = 3f;   // мягкие края штриха

            float halfT = Thickness * 0.5f;
            var baseColor = color;
            for (int i = 0; i < n; i++)
            {
                // штрихи бегут по часовой стрелке: узор сдвигается вперёд по ходу обхода
                float u = Mathf.Repeat(dist[i] - _phase, period);
                float a = u < dash ? Mathf.Clamp01(Mathf.Min(u, dash - u) / soft) : 0f;
                var c = baseColor;
                c.a *= a * _visible;
                Color32 c32 = c;
                vh.AddVert(points[i] - normals[i] * halfT, c32, Vector2.zero);
                vh.AddVert(points[i] + normals[i] * halfT, c32, Vector2.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int a0 = i * 2, a1 = a0 + 1;
                int b0 = ((i + 1) % n) * 2, b1 = b0 + 1;
                vh.AddTriangle(a0, a1, b1);
                vh.AddTriangle(a0, b1, b0);
            }
        }

        static void Line(System.Collections.Generic.List<Vector2> pts, System.Collections.Generic.List<Vector2> nrm,
            Vector2 from, Vector2 to, Vector2 normal)
        {
            float len = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / Step));
            for (int k = 0; k < steps; k++)
            {
                pts.Add(Vector2.Lerp(from, to, k / (float)steps));
                nrm.Add(normal);
            }
        }

        static void Arc(System.Collections.Generic.List<Vector2> pts, System.Collections.Generic.List<Vector2> nrm,
            Vector2 center, float radius, float fromDeg, float toDeg)
        {
            if (radius <= 0.5f) return;
            float len = Mathf.Abs(toDeg - fromDeg) * Mathf.Deg2Rad * radius;
            int steps = Mathf.Max(2, Mathf.CeilToInt(len / Step));
            for (int k = 0; k < steps; k++)
            {
                float ang = Mathf.Lerp(fromDeg, toDeg, k / (float)steps) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                pts.Add(center + dir * radius);
                nrm.Add(dir);
            }
        }
    }
}
