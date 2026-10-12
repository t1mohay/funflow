using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RecipeSlicer
{
    /// <summary>
    /// Анимированная обводка кнопки при наведении: тонкая рамка, по которой течёт «жидкая краска»
    /// как в фоне главного меню (MenuSwirl) — тёмные и светлые разводы с прожилками и бликами
    /// в оттенках заливки кнопки, поток идёт по часовой стрелке.
    /// Рисуется своим мешем (без текстур), время — unscaled, поэтому работает и в паузе.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HoverRing : MaskableGraphic
    {
        /// <summary>Толщина линии, в единицах холста 1920×1080.</summary>
        public float Thickness = 4f;
        /// <summary>Радиус скругления углов рамки (для круглых кнопок берётся половина высоты).</summary>
        public float Radius = 30f;
        /// <summary>Примерная длина одного «разлива» краски по контуру, единиц холста.</summary>
        public float Wave = 220f;
        /// <summary>Скорость течения, радиан в секунду (≈ 60–80 единиц контура в секунду).</summary>
        public float Speed = 2.2f;
        /// <summary>Время появления и исчезновения, с.</summary>
        public float Fade = 0.15f;
        /// <summary>
        /// Насколько заливка заходит внутрь от линии рамки — до самого края кнопки (это отступ рамки),
        /// чтобы между кнопкой и обводкой не было просвета.
        /// </summary>
        public float Inner;

        const float Step = 2f;   // шаг разбиения контура — плавные переливы цвета

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
            ring.Inner = offset;
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
            _phase += dt;
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

            // целые частоты по периметру — шов незаметен; поток идёт по ходу обхода (по часовой)
            int bands = Mathf.Max(2, Mathf.RoundToInt(perimeter / Wave));
            int warp1 = Mathf.Max(1, Mathf.RoundToInt(bands * 0.6f));
            int warp2 = bands + 2;
            float t = _phase;

            // палитра из цвета кнопки, как у MenuSwirl: глубокий тон, основной, прожилки, блики
            var baseColor = color;
            Color deep = Color.Lerp(baseColor, Color.black, 0.32f);
            Color warm = baseColor;
            Color vein = Color.Lerp(baseColor, Color.white, 0.45f);
            Color gloss = Color.Lerp(baseColor, Color.white, 0.8f);
            deep.a = warm.a = vein.a = gloss.a = baseColor.a;

            float halfT = Thickness * 0.5f;
            // Внутренний край — у самой кнопки: от неё до наружной кромки рамки сплошная заливка.
            float inside = Mathf.Max(halfT, Inner);
            for (int i = 0; i < n; i++)
            {
                float a = dist[i] / perimeter * Mathf.PI * 2f;
                // искажение «текучей краски»: два медленных синуса сдвигают фазу основной волны
                float w = 0.9f * Mathf.Sin(warp1 * a + 0.8f * t) + 0.5f * Mathf.Sin(warp2 * a - 1.3f * t + 1.7f);
                float f1 = 0.5f + 0.5f * Mathf.Sin(bands * a - Speed * t + 1.6f * w);              // основа ↔ светлый тон
                float f2 = 0.5f + 0.5f * Mathf.Sin(bands * 2f * a - Speed * 1.3f * t + 2.2f * w);  // прожилки и тени

                var c = Color.Lerp(deep, warm, Smooth(0.25f, 0.75f, f1));
                c = Color.Lerp(c, vein, 0.8f * Gauss(f2, 0.5f, 0.07f) * Smooth(0.3f, 0.7f, f1));
                c = Color.Lerp(c, gloss, 0.7f * Gauss(f1, 0.92f, 0.04f));
                c.a *= _visible;
                Color32 inner = c;
                // наружная кромка чуть темнее — у тонкой линии появляется объём
                var outerColor = Color.Lerp(c, deep, 0.25f);
                outerColor.a = c.a;
                Color32 outer = outerColor;
                vh.AddVert(points[i] - normals[i] * inside, inner, Vector2.zero);
                vh.AddVert(points[i] + normals[i] * halfT, outer, Vector2.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int a0 = i * 2, a1 = a0 + 1;
                int b0 = ((i + 1) % n) * 2, b1 = b0 + 1;
                vh.AddTriangle(a0, a1, b1);
                vh.AddTriangle(a0, b1, b0);
            }
        }

        /// <summary>smoothstep как в шейдерах (Mathf.SmoothStep — это другая функция).</summary>
        static float Smooth(float edge0, float edge1, float x)
        {
            float k = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return k * k * (3f - 2f * k);
        }

        static float Gauss(float x, float mean, float width)
        {
            float d = (x - mean) / width;
            return Mathf.Exp(-d * d);
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
