using UnityEngine;
using UnityEngine.EventSystems;

namespace RecipeSlicer
{
    /// <summary>Наведение на кнопку включает её HoverRing; скрытие кнопки сразу прячет рамку.</summary>
    public sealed class HoverRingTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public HoverRing Ring;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Ring != null) Ring.SetHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Ring != null) Ring.SetHover(false);
        }

        void OnDisable()
        {
            if (Ring != null) Ring.SetHover(false, instant: true);
        }
    }
}
