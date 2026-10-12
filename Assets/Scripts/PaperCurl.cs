using UnityEngine;
using UnityEngine.UI;

namespace RecipeSlicer
{
    /// <summary>
    /// Загнутый правый нижний уголок листа (PaperSheet): мягкая тень на бумаге под ним, сам клапан
    /// с объёмной заливкой «тыльной стороны» и тёплый контур, как у листов заказа.
    /// Уголок медленно и едва заметно «дышит» — приподнимается и опускается; время — UiTime (не зависит от паузы).
    /// Растягивается на тот же прямоугольник, что и лист.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperCurl : MaskableGraphic
    {
        public PaperSheet Sheet;
        /// <summary>Тень листа той же формы — угол у неё вырезан так же (может быть null).</summary>
        public PaperSheet Shadow;
        /// <summary>Средний размер уголка, единиц холста.</summary>
        public float BaseCurl = 64f;
        /// <summary>Насколько уголок приподнимается и опускается (медленная и быстрая составляющие).</summary>
        public float Breath = 6f, Flutter = 1.6f;
        /// <summary>Периоды «дыхания», с — некратные, чтобы движение не казалось механическим.</summary>
        public float BreathPeriod = 6.5f, FlutterPeriod = 2.7f;

        static readonly Color PaperDark = new Color32(0xDE, 0xCF, 0xB0, 0xFF);
        static readonly Color PaperLight = new Color32(0xFB, 0xF5, 0xE7, 0xFF);
        static readonly Color Outline = new Color32(0xB0, 0x8A, 0x58, 0xFF);
        static readonly Color ShadowColor = new Color(0.35f, 0.24f, 0.1f, 0.26f);

        float _curl, _lift;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            _curl = BaseCurl;
        }

        void Update()
        {
            float t = UiTime.Now;
            float breath = Mathf.Sin(t * Mathf.PI * 2f / BreathPeriod);
            float flutter = Mathf.Sin(t * Mathf.PI * 2f / FlutterPeriod + 1.3f);
            _curl = BaseCurl + Breath * breath + Flutter * flutter;
            // чем больше загиб, тем сильнее кончик отходит от бумаги (тень длиннее)
            _lift = 0.5f + 0.5f * breath;
            if (Sheet != null) Sheet.Curl = _curl;
            if (Shadow != null) Shadow.Curl = _curl;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (r.width <= 1f || r.height <= 1f) return;
            float c = Mathf.Clamp(_curl, 1f, Mathf.Min(r.width, r.height) * 0.5f);

            var corner = new Vector2(r.xMax, r.yMin);
            var a = new Vector2(r.xMax - c, r.yMin);      // на нижнем крае
            var b = new Vector2(r.xMax, r.yMin + c);      // на правом крае
            var mid = (a + b) * 0.5f;
            // кончик клапана: почти отражение угла через сгиб, чуть «не долёг» — так уголок выглядит загнутым, а не сложенным
            var tip = mid + (mid - corner) * 0.9f;

            // ── тень на бумаге под клапаном: тянется дальше кончика, мягко гаснет к краям
            var shadowTip = mid + (mid - corner) * (1.25f + 0.2f * _lift);
            float soft = 6f + 0.12f * c;
            var shadow = ShadowColor;
            var clear = new Color(shadow.r, shadow.g, shadow.b, 0f);
            Triangle(vh, a, b, shadowTip, shadow, shadow, clear);
            Fringe(vh, a, shadowTip, b, soft, shadow, clear, r);
            Fringe(vh, shadowTip, b, a, soft, clear, shadow, r);

            // ── клапан: полосы от сгиба к кончику, объём как у свёрнутого листа
            const int rows = 8;
            for (int i = 0; i < rows; i++)
            {
                float v0 = i / (float)rows, v1 = (i + 1) / (float)rows;
                var l0 = Vector2.Lerp(a, tip, v0); var r0 = Vector2.Lerp(b, tip, v0);
                var l1 = Vector2.Lerp(a, tip, v1); var r1 = Vector2.Lerp(b, tip, v1);
                Color c0 = FlapColor(v0), c1 = FlapColor(v1);
                int start = vh.currentVertCount;
                vh.AddVert(l0, c0, Vector2.zero);
                vh.AddVert(r0, c0, Vector2.zero);
                vh.AddVert(r1, c1, Vector2.zero);
                vh.AddVert(l1, c1, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }

            // ── контур: края клапана и тонкая линия сгиба
            Stroke(vh, a, tip, 2.6f, Outline);
            Stroke(vh, tip, b, 2.6f, Outline);
            var crease = Outline; crease.a = 0.55f;
            Stroke(vh, a, b, 1.6f, crease);
        }

        /// <summary>Цвет тыльной стороны: у сгиба — тень, к середине светлеет, у кончика чуть темнее.</summary>
        static Color FlapColor(float v)
        {
            float k = Mathf.Sin(Mathf.Clamp01(v * 0.85f + 0.1f) * Mathf.PI);
            return Color.Lerp(PaperDark, PaperLight, Mathf.Pow(k, 0.8f));
        }

        static void Triangle(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Color c0, Color c1, Color c2)
        {
            int s = vh.currentVertCount;
            vh.AddVert(p0, c0, Vector2.zero);
            vh.AddVert(p1, c1, Vector2.zero);
            vh.AddVert(p2, c2, Vector2.zero);
            vh.AddTriangle(s, s + 1, s + 2);
        }

        /// <summary>Мягкая кромка тени наружу от ребра p0–p1 (в сторону от третьей вершины); не выходит за край листа.</summary>
        static void Fringe(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 opposite, float width, Color c0, Color c1, Rect r)
        {
            var e = p1 - p0;
            var outward = new Vector2(-e.y, e.x).normalized;
            if (Vector2.Dot(outward, opposite - p0) > 0f) outward = -outward;
            var q0 = Clamp(p0 + outward * width, r);
            var q1 = Clamp(p1 + outward * width, r);
            var t0 = new Color(c0.r, c0.g, c0.b, 0f);
            var t1 = new Color(c1.r, c1.g, c1.b, 0f);
            int s = vh.currentVertCount;
            vh.AddVert(p0, c0, Vector2.zero);
            vh.AddVert(p1, c1, Vector2.zero);
            vh.AddVert(q1, t1, Vector2.zero);
            vh.AddVert(q0, t0, Vector2.zero);
            vh.AddTriangle(s, s + 1, s + 2);
            vh.AddTriangle(s, s + 2, s + 3);
        }

        static Vector2 Clamp(Vector2 p, Rect r) =>
            new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));

        static void Stroke(VertexHelper vh, Vector2 p0, Vector2 p1, float width, Color color)
        {
            var d = (p1 - p0).normalized;
            var n = new Vector2(-d.y, d.x) * (width * 0.5f);
            // концы чуть продлены, чтобы стыки рёбер не давали щелей
            p0 -= d * (width * 0.5f);
            p1 += d * (width * 0.5f);
            int s = vh.currentVertCount;
            Color32 c = color;
            vh.AddVert(p0 - n, c, Vector2.zero);
            vh.AddVert(p0 + n, c, Vector2.zero);
            vh.AddVert(p1 + n, c, Vector2.zero);
            vh.AddVert(p1 - n, c, Vector2.zero);
            vh.AddTriangle(s, s + 1, s + 2);
            vh.AddTriangle(s, s + 2, s + 3);
        }
    }
}
