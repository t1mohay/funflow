using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using KeyId = UnityEngine.InputSystem.Key;
#else
using KeyId = UnityEngine.KeyCode;
#endif

namespace RecipeSlicer
{
    /// <summary>Действия на горячих клавишах; клавиши игрок может переназначить в меню паузы.</summary>
    public enum HotkeyAction
    {
        Pause,
        Restart,
        Dish1,
        Dish2,
        Dish3,
    }

    /// <summary>Работает и с новым Input System, и со старым Input Manager — что включено в проекте.</summary>
    public static class InputUtil
    {
        public static readonly HotkeyAction[] Actions =
            { HotkeyAction.Pause, HotkeyAction.Restart, HotkeyAction.Dish1, HotkeyAction.Dish2, HotkeyAction.Dish3 };

#if ENABLE_INPUT_SYSTEM
        static readonly KeyId[] Defaults = { Key.Escape, Key.R, Key.Digit1, Key.Digit2, Key.Digit3 };
#else
        static readonly KeyId[] Defaults = { KeyCode.Escape, KeyCode.R, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
#endif
        static readonly KeyId[] Bindings = (KeyId[])Defaults.Clone();

        static HotkeyAction? _listening;
        static int _blockedFrame = -1;

        // Fast Enter Play Mode не перезагружает домен — сбрасываем состояние вручную.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Defaults.CopyTo(Bindings, 0);
            _listening = null;
            _blockedFrame = -1;
            _konamiStep = 0;
        }

        public static bool RestartPressed() => Pressed(HotkeyAction.Restart);
        public static bool BackPressed() => Pressed(HotkeyAction.Pause);
        public static bool DishPressed(int index) => index >= 0 && index < 3 && Pressed(HotkeyAction.Dish1 + index);

        /// <summary>
        /// Пока ждём новую клавишу (и в кадр, когда её назначили), горячие клавиши не срабатывают —
        /// иначе нажатие для переназначения сразу выполнило бы действие.
        /// </summary>
        static bool Pressed(HotkeyAction action) =>
            _listening == null && Time.frameCount != _blockedFrame && Down(Bindings[(int)action]);

        // ───────────────────────── Переназначение ─────────────────────────

        static string PrefsKey(HotkeyAction action) => "hotkey_" + action;

        public static void LoadBindings()
        {
            foreach (var action in Actions)
            {
                int saved = PlayerPrefs.GetInt(PrefsKey(action), (int)Defaults[(int)action]);
                Bindings[(int)action] = System.Enum.IsDefined(typeof(KeyId), saved) ? (KeyId)saved : Defaults[(int)action];
            }
        }

        /// <summary>Действие, для которого ждём новую клавишу, или null.</summary>
        public static HotkeyAction? Listening => _listening;

        public static void BeginRebind(HotkeyAction action) => _listening = action;

        public static void CancelRebind()
        {
            if (_listening == null) return;
            _listening = null;
            _blockedFrame = Time.frameCount;
        }

        /// <summary>
        /// Вызывать каждый кадр, пока ждём клавишу. true — клавиша нажата и назначена.
        /// Новая клавиша действует сразу, но на диск попадает только после SaveBindings.
        /// </summary>
        public static bool PollRebind()
        {
            if (_listening == null || !AnyKeyDown(out var key)) return false;
            int target = (int)_listening.Value;
            // Клавиша уже занята другим действием — меняем их местами, чтобы ничего не осталось без клавиши.
            for (int i = 0; i < Bindings.Length; i++)
            {
                if (i == target || !Bindings[i].Equals(key)) continue;
                Bindings[i] = Bindings[target];
            }
            Bindings[target] = key;
            _listening = null;
            _blockedFrame = Time.frameCount;
            return true;
        }

        /// <summary>Клавиши по умолчанию (без сохранения — его делает SaveBindings).</summary>
        public static void ResetBindings()
        {
            CancelRebind();
            Defaults.CopyTo(Bindings, 0);
        }

        public static void SaveBindings()
        {
            CancelRebind();
            foreach (var action in Actions) PlayerPrefs.SetInt(PrefsKey(action), (int)Bindings[(int)action]);
            PlayerPrefs.Save();
        }

        /// <summary>Отменить несохранённые изменения — вернуть сохранённые клавиши.</summary>
        public static void RevertBindings()
        {
            CancelRebind();
            LoadBindings();
        }

        public static bool HasUnsavedChanges()
        {
            foreach (var action in Actions)
                if (PlayerPrefs.GetInt(PrefsKey(action), (int)Defaults[(int)action]) != (int)Bindings[(int)action]) return true;
            return false;
        }

        // ───────────────────────── Код Конами ─────────────────────────

#if ENABLE_INPUT_SYSTEM
        static readonly KeyId[] Konami =
            { Key.UpArrow, Key.UpArrow, Key.DownArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.LeftArrow, Key.RightArrow, Key.B, Key.A };
#else
        static readonly KeyId[] Konami =
            { KeyCode.UpArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
              KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.B, KeyCode.A };
#endif
        static int _konamiStep;

        /// <summary>
        /// ↑ ↑ ↓ ↓ ← → ← → B A. Вызывать каждый кадр; true — в этом кадре код введён целиком.
        /// Неверная клавиша сбрасывает ввод (но если это начало кода — засчитывается как первый шаг).
        /// </summary>
        public static bool KonamiEntered()
        {
            if (_listening != null || !AnyKeyDown(out var key)) return false;
            if (key.Equals(Konami[_konamiStep])) _konamiStep++;
            // ↑ ↑ ↑ — последние два «вверх» всё ещё валидное начало кода.
            else if (key.Equals(Konami[0])) _konamiStep = _konamiStep == 2 ? 2 : 1;
            else _konamiStep = 0;

            if (_konamiStep < Konami.Length) return false;
            _konamiStep = 0;
            return true;
        }

        public static void ResetKonami() => _konamiStep = 0;

        /// <summary>Клавиши «+» и «−» (основные и на цифровом блоке): +1, −1 или 0, если не нажаты.</summary>
        public static int PlusMinusPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Down(Key.Equals) || Down(Key.NumpadPlus)) return 1;
            if (Down(Key.Minus) || Down(Key.NumpadMinus)) return -1;
#else
            if (Down(KeyCode.Equals) || Down(KeyCode.Plus) || Down(KeyCode.KeypadPlus)) return 1;
            if (Down(KeyCode.Minus) || Down(KeyCode.KeypadMinus)) return -1;
#endif
            return 0;
        }

        /// <summary>Название клавиши для интерфейса.</summary>
        public static string KeyName(HotkeyAction action)
        {
            var key = Bindings[(int)action];
            switch (key)
            {
#if ENABLE_INPUT_SYSTEM
                case Key.Escape: return "Esc";
                case Key.Space: return "Пробел";
                case Key.Enter: return "Enter";
                case Key.Backspace: return "Backspace";
                case Key.Tab: return "Tab";
            }
            // Подпись с учётом раскладки клавиатуры (например, «R»), иначе — имя из перечисления.
            var keyboard = Keyboard.current;
            string display = null;
            if (keyboard != null)
            {
                try { display = keyboard[key].displayName; }
                catch (System.ArgumentException) { }
            }
            return string.IsNullOrEmpty(display) ? key.ToString() : display.ToUpperInvariant();
#else
                case KeyCode.Escape: return "Esc";
                case KeyCode.Space: return "Пробел";
                case KeyCode.Return: return "Enter";
            }
            string name = key.ToString();
            if (name.StartsWith("Alpha")) return name.Substring(5);
            if (name.StartsWith("Keypad")) return "Num " + name.Substring(6);
            return name;
#endif
        }

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

        static bool Down(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        static bool AnyKeyDown(out Key key)
        {
            key = Key.None;
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            foreach (var control in keyboard.allKeys)
            {
                if (control == null || !control.wasPressedThisFrame || control.keyCode == Key.None) continue;
                key = control.keyCode;
                return true;
            }
            return false;
        }
#else
        public static bool PointerHeld(out Vector2 screenPos)
        {
            screenPos = Input.mousePosition;
            return Input.GetMouseButton(0);
        }

        static bool Down(KeyCode key) => Input.GetKeyDown(key);

        static bool AnyKeyDown(out KeyCode key)
        {
            key = KeyCode.None;
            if (!Input.anyKeyDown) return false;
            foreach (KeyCode code in System.Enum.GetValues(typeof(KeyCode)))
            {
                // Кнопки мыши и джойстика не назначаем — только клавиатуру.
                if (code == KeyCode.None || code >= KeyCode.Mouse0 || !Input.GetKeyDown(code)) continue;
                key = code;
                return true;
            }
            return false;
        }
#endif
    }
}
