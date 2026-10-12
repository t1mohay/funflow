using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RecipeSlicer
{
    /// <summary>
    /// Объём для UI.Text: верх букв светлее, низ темнее. Цвет букв (в том числе из rich text)
    /// не заменяется, а умножается — поэтому разноцветные слова сохраняют свои цвета.
    /// Ставить первым эффектом, до обводок, чтобы обводка оставалась ровного цвета.
    /// </summary>
    [AddComponentMenu("UI/Effects/Vertical Shade")]
    public sealed class VerticalShade : BaseMeshEffect
    {
        public float Top = 1.3f;
        public float Bottom = 0.75f;

        static readonly List<UIVertex> Verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            Verts.Clear();
            vh.GetUIVertexStream(Verts);
            // Каждая буква — 6 вершин (2 треугольника); затеняем по высоте самой буквы.
            for (int i = 0; i + 5 < Verts.Count; i += 6)
            {
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int k = i; k < i + 6; k++)
                {
                    minY = Mathf.Min(minY, Verts[k].position.y);
                    maxY = Mathf.Max(maxY, Verts[k].position.y);
                }
                for (int k = i; k < i + 6; k++)
                {
                    var v = Verts[k];
                    float t = maxY > minY ? (v.position.y - minY) / (maxY - minY) : 0.5f;
                    float m = Mathf.Lerp(Bottom, Top, t);
                    Color c = v.color;
                    v.color = new Color(Mathf.Clamp01(c.r * m), Mathf.Clamp01(c.g * m), Mathf.Clamp01(c.b * m), c.a);
                    Verts[k] = v;
                }
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(Verts);
        }
    }

    /// <summary>Лёгкое «дыхание» логотипа: медленно чуть увеличивается и покачивается.</summary>
    public sealed class LogoBreathe : MonoBehaviour
    {
        public float Scale = 0.025f;
        public float Swing = 1.2f;
        public float Tilt = 3f;

        void Update()
        {
            float t = UiTime.Now;
            float s = 1f + Scale * Mathf.Sin(t * 1.8f);
            transform.localScale = new Vector3(s, s, 1f);
            transform.localEulerAngles = new Vector3(0f, 0f, Tilt + Swing * Mathf.Sin(t * 1.1f));
        }
    }
}
