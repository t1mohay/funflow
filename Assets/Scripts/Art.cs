using System.Collections.Generic;
using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>Спрайты и шрифт: загрузка из Resources и процедурные заглушки.</summary>
    public static class Art
    {
        static Sprite _circle;
        static Sprite _roundedRect;
        static readonly Dictionary<int, Sprite> Rings = new Dictionary<int, Sprite>();
        static Sprite _gradient;
        static Sprite[] _splats;
        static Sprite _gloss;
        static Sprite _softGlow;
        static Sprite _ring;
        static Font _font;
        static Font _defaultFont;
        static Material _lineMaterial;
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        // Fast Enter Play Mode не перезагружает домен — сбрасываем кэши вручную.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _circle = null;
            _roundedRect = null;
            Rings.Clear();
            _gradient = null;
            _splats = null;
            _gloss = null;
            _softGlow = null;
            _ring = null;
            _font = null;
            _defaultFont = null;
            _lineMaterial = null;
            Sprites.Clear();
        }

        /// <summary>Белый круг диаметром 1 юнит.</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle == null) _circle = MakeCircle(128);
                return _circle;
            }
        }

        /// <summary>Скруглённый прямоугольник для UI (9-slice).</summary>
        public static Sprite RoundedRect
        {
            get
            {
                if (_roundedRect == null) _roundedRect = MakeRoundedRect(64, 22);
                return _roundedRect;
            }
        }

        public static Font UIFont
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/Main");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "Segoe UI" }, 32);
                return _font;
            }
        }

        /// <summary>Встроенный шрифт Unity — для логотипа.</summary>
        public static Font DefaultFont
        {
            get
            {
                if (_defaultFont != null) return _defaultFont;
                _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_defaultFont == null) _defaultFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "Segoe UI" }, 32);
                return _defaultFont;
            }
        }

        public static Material LineMaterial
        {
            get
            {
                if (_lineMaterial == null) _lineMaterial = new Material(Shader.Find("Sprites/Default"));
                return _lineMaterial;
            }
        }

        /// <summary>Спрайт ингредиента из Resources/Ingredients или null, если картинки ещё нет.</summary>
        public static Sprite IngredientSprite(IngredientDef def) => LoadSprite("Ingredients/" + def.Id);

        /// <summary>Картинка блюда из Resources/Dishes или null.</summary>
        public static Sprite DishSprite(RecipeDef recipe) => LoadSprite("Dishes/" + recipe.Id);

        /// <summary>Спрайт из Resources по пути или null.</summary>
        public static Sprite LoadSprite(string path)
        {
            if (Sprites.TryGetValue(path, out var sprite)) return sprite;

            sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                // PNG мог импортироваться как обычная текстура — тогда делаем спрайт сами.
                var texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
            Sprites[path] = sprite;
            return sprite;
        }

        /// <summary>
        /// Половинка спрайта, отрезанная прямой через центр. Картинка не поворачивается:
        /// меняется только геометрия, поэтому разрез идёт ровно по линии свайпа.
        /// </summary>
        /// <param name="localDirection">Направление разреза в локальных осях спрайта.</param>
        /// <param name="positiveSide">Какую половину оставить: слева (+) или справа (−) от направления.</param>
        public static Sprite CutHalf(Sprite source, Vector2 localDirection, bool positiveSide)
        {
            // textureRect — реальные пиксели текстуры (после ужатия при импорте), rect — исходные.
            var rect = source.textureRect;
            var pivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
            float ppu = source.pixelsPerUnit * rect.width / source.rect.width;
            var half = Sprite.Create(source.texture, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
            half.name = source.name + "_half";

            var size = rect.size;
            var center = size * 0.5f;
            var normal = new Vector2(-localDirection.y, localDirection.x);
            if (!positiveSide) normal = -normal;

            var corners = new[] { Vector2.zero, new Vector2(size.x, 0f), size, new Vector2(0f, size.y) };
            var polygon = new List<Vector2>(5);
            for (int i = 0; i < corners.Length; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % corners.Length];
                float da = Vector2.Dot(a - center, normal);
                float db = Vector2.Dot(b - center, normal);
                if (da >= 0f) polygon.Add(a);
                if (da * db < 0f) polygon.Add(Vector2.Lerp(a, b, da / (da - db)));
            }

            var vertices = new Vector2[polygon.Count];
            for (int i = 0; i < polygon.Count; i++)
                vertices[i] = new Vector2(Mathf.Clamp(polygon[i].x, 0f, size.x), Mathf.Clamp(polygon[i].y, 0f, size.y));
            var triangles = new ushort[(vertices.Length - 2) * 3];
            for (int i = 0; i < vertices.Length - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (ushort)(i + 1);
                triangles[i * 3 + 2] = (ushort)(i + 2);
            }
            half.OverrideGeometry(vertices, triangles);
            return half;
        }

        /// <summary>Сколько разных клякс-брызг (Splat).</summary>
        public const int SplatVariants = 4;

        /// <summary>
        /// Белая клякса сока — пятно с неровным краем, острыми «лучами» и каплями вокруг.
        /// Красится цветом ингредиента (SpriteRenderer.color). Диаметр — 1 юнит.
        /// </summary>
        public static Sprite Splat(int variant)
        {
            if (_splats == null) _splats = new Sprite[SplatVariants];
            variant = Mathf.Abs(variant) % SplatVariants;
            if (_splats[variant] == null) _splats[variant] = MakeSplat(256, 1234 + variant * 7919);
            return _splats[variant];
        }

        static Sprite MakeSplat(int size, int seed)
        {
            var rng = new System.Random(seed);
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // Край пятна: радиус зависит от угла (несколько синусоид) плюс острые лучи-потёки.
            const int waves = 4;
            var waveK = new int[waves];
            var waveAmp = new float[waves];
            var wavePhase = new float[waves];
            for (int i = 0; i < waves; i++)
            {
                waveK[i] = 2 + i * 2 + rng.Next(2);
                waveAmp[i] = Rand(0.008f, 0.022f);
                wavePhase[i] = Rand(0f, Mathf.PI * 2f);
            }
            int spikes = 7 + rng.Next(5);
            var spikeAngle = new float[spikes];
            var spikeLength = new float[spikes];
            var spikeWidth = new float[spikes];
            for (int i = 0; i < spikes; i++)
            {
                spikeAngle[i] = Rand(0f, Mathf.PI * 2f);
                spikeLength[i] = Rand(0.05f, 0.17f);
                spikeWidth[i] = Rand(0.12f, 0.3f);
            }
            // Капли: часть — продолжение лучей, часть — просто вокруг пятна.
            int drops = 14 + rng.Next(8);
            var drop = new Vector3[drops];
            for (int i = 0; i < drops; i++)
            {
                float angle = i < spikes ? spikeAngle[i] + Rand(-0.08f, 0.08f) : Rand(0f, Mathf.PI * 2f);
                float dist = Rand(0.36f, 0.46f);
                drop[i] = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, Rand(0.01f, 0.032f));
            }

            const float baseRadius = 0.27f;
            float px = 1f / size;
            var tex = NewTexture(size, size, "Splat");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) * px - 0.5f;
                float v = (y + 0.5f) * px - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v);
                float theta = Mathf.Atan2(v, u);

                float edge = baseRadius;
                for (int i = 0; i < waves; i++) edge += waveAmp[i] * Mathf.Sin(waveK[i] * theta + wavePhase[i]);
                for (int i = 0; i < spikes; i++)
                {
                    float d = Mathf.Abs(Mathf.DeltaAngle(theta * Mathf.Rad2Deg, spikeAngle[i] * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    float k = Mathf.Clamp01(1f - d / spikeWidth[i]);
                    edge += spikeLength[i] * k * k * k;
                }
                float a = Mathf.Clamp01((edge - r) / (1.5f * px) + 0.5f);

                for (int i = 0; i < drops; i++)
                {
                    float du = u - drop[i].x, dv = v - drop[i].y;
                    float dd = Mathf.Sqrt(du * du + dv * dv);
                    a = Mathf.Max(a, Mathf.Clamp01((drop[i].z - dd) / (1.5f * px) + 0.5f));
                }
                // Сок в середине чуть плотнее, к краям — прозрачнее.
                a *= Mathf.Lerp(1f, 0.93f, Mathf.Clamp01(r / 0.45f));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeCircle(int size)
        {
            var tex = NewTexture(size, size, "Circle");
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - 1f - d + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>Мягкое свечение диаметром 1 юнит: яркая середина плавно гаснет к краю (ореол бонуса).</summary>
        public static Sprite SoftGlow
        {
            get
            {
                if (_softGlow == null) _softGlow = MakeRadial(128, "SoftGlow", d => (1f - d) * (1f - d));
                return _softGlow;
            }
        }

        /// <summary>Тонкое кольцо диаметром 1 юнит с мягкими краями — ударная волна.</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring == null) _ring = MakeRadial(256, "Ring", d => 1f - Mathf.Abs(d - 0.88f) / 0.1f);
                return _ring;
            }
        }

        /// <param name="alpha">Непрозрачность по доле радиуса: 0 — центр, 1 — край.</param>
        static Sprite MakeRadial(int size, string name, System.Func<float, float> alpha)
        {
            var tex = NewTexture(size, size, name);
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                float a = d >= 1f ? 0f : Mathf.Clamp01(alpha(d));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>Вертикальный градиент высотой 1 юнит для фона.</summary>
        public static Sprite Gradient(Color bottom, Color top)
        {
            if (_gradient != null) return _gradient;

            const int height = 256;
            var tex = NewTexture(2, height, "Gradient");
            var pixels = new Color[2 * height];
            for (int y = 0; y < height; y++)
            {
                var c = Color.Lerp(bottom, top, y / (height - 1f));
                pixels[y * 2] = c;
                pixels[y * 2 + 1] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _gradient = Sprite.Create(tex, new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f), height);
            return _gradient;
        }

        /// <summary>Радиус скругления у колец RoundedRing, в пикселях текстуры.</summary>
        public const int RingRadius = 44;

        /// <summary>
        /// Скруглённая рамка без заливки (9-slice), толщиной thickness пикселей при радиусе RingRadius.
        /// Нужна полупрозрачным панелям: под рамкой нет второго слоя, который просвечивал бы сквозь заливку.
        /// </summary>
        public static Sprite RoundedRing(int thickness)
        {
            thickness = Mathf.Clamp(thickness, 1, RingRadius);
            if (!Rings.TryGetValue(thickness, out var sprite) || sprite == null)
                Rings[thickness] = sprite = MakeRoundedRing(128, RingRadius, thickness);
            return sprite;
        }

        static Sprite MakeRoundedRing(int size, int radius, int thickness)
        {
            var tex = NewTexture(size, size, "RoundedRing");
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Знаковое расстояние до края скруглённого прямоугольника (внутри — отрицательное).
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float dist = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float a = Mathf.Clamp01(0.5f - dist) * Mathf.Clamp01(dist + thickness + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            float border = Mathf.Max(radius, thickness) + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        static Sprite MakeRoundedRect(int size, int radius)
        {
            var tex = NewTexture(size, size, "RoundedRect");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float cx = Mathf.Clamp(px, radius, size - radius);
                float cy = Mathf.Clamp(py, radius, size - radius);
                float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            float border = radius + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>
        /// Мягкий блик для кнопок: белое пятно, ярче всего чуть ниже верхнего края и плавно гаснущее
        /// вниз и к бокам — без резких краёв и углов. Растягивается картинкой типа Simple.
        /// </summary>
        public static Sprite Gloss
        {
            get
            {
                if (_gloss == null) _gloss = MakeGloss(128, 64);
                return _gloss;
            }
        }

        static Sprite MakeGloss(int width, int height)
        {
            var tex = NewTexture(width, height, "Gloss");
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / width - 0.5f) * 2f; // 0 в центре, 1 у боков
                float v = 1f - (y + 0.5f) / height;                 // 0 сверху, 1 снизу
                float sides = 1f - Mathf.SmoothStep(0.55f, 1f, u);
                float top = Mathf.SmoothStep(0f, 0.18f, v);
                float bottom = 1f - Mathf.SmoothStep(0.2f, 1f, v);
                float a = sides * top * bottom;
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        static Texture2D NewTexture(int width, int height, string name)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }
    }
}
