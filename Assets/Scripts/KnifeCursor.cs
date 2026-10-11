using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RecipeSlicer
{
    /// <summary>
    /// Курсор-нож: картинка Resources/UI/knife, нарисованная поверх всего и привязанная к мыши.
    /// Системный курсор Windows ограничен ~32 px, поэтому крупный нож рисуем сами — в масштабе экрана,
    /// чтобы рядом с ингредиентами он выглядел как настоящий нож.
    /// </summary>
    public sealed class KnifeCursor : MonoBehaviour
    {
        /// <summary>Длина ножа в единицах холста 1920×1080 (ингредиент — около 170).</summary>
        const float Length = 190f;
        /// <summary>Наклон от вертикали, как у стрелки Windows: остриё смотрит вверх-влево.</summary>
        const float Tilt = 32f;
        /// <summary>Кончик лезвия в картинке knife.png (доли ширины/высоты, от левого нижнего угла).</summary>
        static readonly Vector2 Tip = new Vector2(0.33f, 0.015f);

        RectTransform _root;
        RectTransform _knife;

        void Awake()
        {
            var sprite = Art.LoadSprite("UI/knife");
            if (sprite == null)
            {
                enabled = false;
                return;
            }

            var canvasGo = new GameObject("CursorCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000; // выше интерфейса и слоя яркости
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)canvasGo.transform;

            var go = new GameObject("Knife", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false; // клики проходят к кнопкам под ножом
            _knife = (RectTransform)go.transform;
            _knife.anchorMin = _knife.anchorMax = Vector2.zero;
            _knife.pivot = Tip; // точка клика — кончик лезвия
            var size = sprite.rect.size;
            _knife.sizeDelta = new Vector2(Length * size.x / size.y, Length);
            _knife.localEulerAngles = new Vector3(0f, 0f, 180f + Tilt);
        }

        void Update()
        {
            bool visible = MousePosition(out var screen)
                && screen.x >= 0f && screen.y >= 0f && screen.x <= Screen.width && screen.y <= Screen.height;
            // Системную стрелку прячем только пока над окном есть наш нож.
            Cursor.visible = !visible;
            _knife.gameObject.SetActive(visible);
            if (!visible) return;

            float scale = _root.localScale.x;
            _knife.anchoredPosition = scale > 0f ? screen / scale : screen;
        }

        void OnDisable() => Cursor.visible = true;

        /// <summary>Позиция мыши; на сенсорных устройствах без мыши курсор не нужен.</summary>
        static bool MousePosition(out Vector2 screen)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            screen = mouse != null ? mouse.position.ReadValue() : default;
            return mouse != null;
#else
            screen = Input.mousePosition;
            return Input.mousePresent;
#endif
        }
    }
}
