using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RecipeSlicer
{
    /// <summary>
    /// Ползунок, который с клавиатуры меняется на 1% диапазона, а не на 10%, как стандартный Slider.
    /// Влево/вправо — стрелками, а у выбранного (нажатого мышью) ползунка — ещё и клавишами «+» и «−».
    /// </summary>
    public sealed class StepSlider : Slider
    {
        const float StepFraction = 0.01f;

        float Step => (maxValue - minValue) * StepFraction;

        public override void OnMove(AxisEventData eventData)
        {
            bool horizontal = direction == Direction.LeftToRight || direction == Direction.RightToLeft;
            if (!IsActive() || !IsInteractable() || !horizontal
                || (eventData.moveDir != MoveDirection.Left && eventData.moveDir != MoveDirection.Right))
            {
                base.OnMove(eventData);
                return;
            }
            bool increase = (eventData.moveDir == MoveDirection.Right) == (direction == Direction.LeftToRight);
            value += increase ? Step : -Step;
        }

        protected override void Update()
        {
            base.Update();
            if (!IsActive() || !IsInteractable()) return;
            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject != gameObject) return;
            int delta = InputUtil.PlusMinusPressed();
            if (delta != 0) value += delta * Step;
        }
    }
}
