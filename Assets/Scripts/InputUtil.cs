using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RecipeSlicer
{
    /// <summary>Работает и с новым Input System, и со старым Input Manager — что включено в проекте.</summary>
    public static class InputUtil
    {
#if ENABLE_INPUT_SYSTEM
        public static bool PointerHeld(out Vector2 screenPos)
        {
            var pointer = Pointer.current;
            if (pointer == null)
            {
                screenPos = default;
                return false;
            }
            screenPos = pointer.position.ReadValue();
            return pointer.press.isPressed;
        }

        public static bool RestartPressed() => Down(Key.R);
        public static bool BackPressed() => Down(Key.Escape);
        public static bool DishPressed(int index) => Down(Key.Digit1 + index) || Down(Key.Numpad1 + index);

        static bool Down(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }
#else
        public static bool PointerHeld(out Vector2 screenPos)
        {
            screenPos = Input.mousePosition;
            return Input.GetMouseButton(0);
        }

        public static bool RestartPressed() => Input.GetKeyDown(KeyCode.R);
        public static bool BackPressed() => Input.GetKeyDown(KeyCode.Escape);
        public static bool DishPressed(int index) =>
            Input.GetKeyDown(KeyCode.Alpha1 + index) || Input.GetKeyDown(KeyCode.Keypad1 + index);
#endif
    }
}
