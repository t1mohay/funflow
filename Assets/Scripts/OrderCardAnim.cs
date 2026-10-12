using UnityEngine;
using UnityEngine.EventSystems;

namespace RecipeSlicer
{
    /// <summary>
    /// Анимация листа заказа на доске «Выбери заказ» (висит на «подвесе» с точкой вращения в кольце прищепки):
    /// — очень лёгкое и медленное покачивание, у каждого листа своя фаза;
    /// — при открытии экрана листы по очереди мягко доворачиваются из небольшого наклона (без отскока);
    /// — картинка блюда едва заметно «дышит»;
    /// — при наведении блюдо плавно чуть приподнимается, а качание затихает.
    /// Всё нарочно неброское: фон для выбора, а не аттракцион.
    /// Время — unscaled.
    /// </summary>
    public sealed class OrderCardAnim : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float SwayDegrees = 0.4f;     // амплитуда покачивания, градусы
        const float SwayPeriod = 4.8f;      // период покачивания, с
        const float EnterDegrees = 2.5f;    // начальный наклон при появлении экрана
        const float EnterDuration = 0.9f;   // за сколько лист мягко встаёт на место, с
        const float Stagger = 0.08f;        // задержка между листами, с
        const float BreathePeriod = 4f;
        const float BreatheAmount = 0.005f; // «дыхание» блюда: ±0,5 % масштаба
        const float HoverScale = 0.03f;     // блюдо при наведении: +3 %
        const float HoverLift = 3f;         // и на 3 единицы выше
        const float HoverFade = 0.3f;       // плавный разгон и торможение, с

        RectTransform _dish;
        Vector3 _dishScale = Vector3.one;
        Vector2 _dishPosition;
        int _index;
        float _phase, _enterTime, _hover;
        bool _hovered;

        public void Init(RectTransform dish, int index)
        {
            _dish = dish;
            _index = index;
            _phase = index * 2.1f;
            if (_dish != null)
            {
                _dishScale = _dish.localScale;
                _dishPosition = _dish.anchoredPosition;
            }
            _enterTime = Time.unscaledTime;
        }

        void OnEnable()
        {
            _enterTime = Time.unscaledTime;   // экран выбора открылся — листы «подлетают» заново
            _hovered = false;
            _hover = 0f;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        void Update()
        {
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            _hover = Mathf.MoveTowards(_hover, _hovered ? 1f : 0f, dt / HoverFade);

            // появление: наклон плавно уходит в ноль (ease-out), без перелёта через вертикаль
            float since = t - _enterTime - _index * Stagger;
            float k = Mathf.Clamp01(since / EnterDuration);
            float enter = EnterDegrees * (1f - k) * (1f - k) * (1f - k);
            // покачивание тоже включается плавно, чтобы не было рывка после появления
            float swayIn = Mathf.SmoothStep(0f, 1f, since / EnterDuration);
            float hover = Mathf.SmoothStep(0f, 1f, _hover);
            float sway = SwayDegrees * Mathf.Sin(t * 2f * Mathf.PI / SwayPeriod + _phase) * swayIn * (1f - 0.8f * hover);
            transform.localRotation = Quaternion.Euler(0f, 0f, enter + sway);

            if (_dish != null)
            {
                float breathe = 1f + BreatheAmount * Mathf.Sin(t * 2f * Mathf.PI / BreathePeriod + _phase);
                _dish.localScale = _dishScale * (breathe * (1f + HoverScale * hover));
                _dish.anchoredPosition = _dishPosition + new Vector2(0f, HoverLift * hover);
            }
        }
    }
}
