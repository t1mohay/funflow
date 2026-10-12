using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>
    /// Покачивание подвешенного предмета (табличка, бокал, плеть плюща) вокруг точки подвеса — pivot.
    /// Две синусоиды с некратными периодами: движение не выглядит механическим и не повторяется в такт.
    /// Время — unscaled: меню работает и при остановленном времени.
    /// </summary>
    public sealed class WallSway : MonoBehaviour
    {
        /// <summary>Размах в градусах.</summary>
        public float Amplitude = 1.2f;
        /// <summary>Период основного покачивания, с.</summary>
        public float Period = 4f;
        /// <summary>Сдвиг фазы — доля периода, чтобы соседние предметы качались вразнобой.</summary>
        public float Phase;

        void Update()
        {
            float t = Time.unscaledTime / Mathf.Max(Period, 0.1f) + Phase;
            float main = Mathf.Sin(t * Mathf.PI * 2f);
            float drift = Mathf.Sin((t * 1.87f + Phase * 3.1f) * Mathf.PI * 2f);
            transform.localEulerAngles = new Vector3(0f, 0f, Amplitude * (main + 0.3f * drift) / 1.3f);
        }
    }
}
