using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RecipeSlicer
{
    /// <summary>
    /// Гладкая обводка для UI.Text. Стандартный Outline рисует 4 копии текста по диагоналям,
    /// поэтому у толстой обводки квадратные углы и «ступеньки». Здесь копии стоят по кругу.
    /// Радиус обводки — effectDistance (берётся большая из координат).
    /// </summary>
    [AddComponentMenu("UI/Effects/Smooth Outline")]
    public sealed class SmoothOutline : Shadow
    {
        /// <summary>Сколько копий по кругу: 8 — уже лучше Outline, 16 — гладко для крупных заголовков.</summary>
        [Range(4, 32)] public int Samples = 16;

        static readonly List<UIVertex> Verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;

            Verts.Clear();
            vh.GetUIVertexStream(Verts);
            int needed = Verts.Count * (Samples + 1);
            if (Verts.Capacity < needed) Verts.Capacity = needed;

            float radius = Mathf.Max(Mathf.Abs(effectDistance.x), Mathf.Abs(effectDistance.y));
            // Как в Outline: каждая копия смещается, а исходные вершины переезжают в конец списка
            // и в итоге рисуются поверх всех копий.
            int start = 0;
            int end = Verts.Count;
            for (int i = 0; i < Samples; i++)
            {
                float angle = i * Mathf.PI * 2f / Samples;
                ApplyShadowZeroAlloc(Verts, effectColor, start, Verts.Count, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                start = end;
                end = Verts.Count;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(Verts);
        }
    }
}
