using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>Бонусные способности. Значение — индекс слота в панели «Бонусы» и в Abilities.All.</summary>
    public enum AbilityType
    {
        None = -1,
        /// <summary>Замедление времени: всё летит медленнее, таймер раунда тоже.</summary>
        SlowMo = 0,
        /// <summary>Множитель очков: нужные ингредиенты и комбо дают вдвое больше.</summary>
        Multiplier = 1,
        /// <summary>Щит: нож проходит сквозь лишнее и перебор — без штрафа.</summary>
        Shield = 2,
    }

    public sealed class AbilityDef
    {
        public AbilityType Type;
        public string Name;
        /// <summary>Короткое пояснение для панели «Бонусы».</summary>
        public string Hint;
        /// <summary>Сколько длится, в реальных секундах (замедление не растягивает само себя).</summary>
        public float Duration;
        public Color Color;
        /// <summary>Летающий бонусный объект: разрезал — получил заряд способности.</summary>
        public IngredientDef Bonus;
    }

    /// <summary>
    /// Данные способностей. Бонусный объект редко вылетает вместе с ингредиентами; если его разрезать,
    /// в панели «Бонусы» появляется заряд. Применить заряд — горячей клавишей (Q / W / E) или кликом по слоту.
    /// </summary>
    public static class Abilities
    {
        public const int MaxCharges = 2;
        /// <summary>Скорость игры под замедлением.</summary>
        public const float SlowMoScale = 0.45f;
        public const int MultiplierFactor = 2;

        static AbilityDef Def(AbilityType type, string id, string name, string hint, float duration, string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return new AbilityDef
            {
                Type = type,
                Name = name,
                Hint = hint,
                Duration = duration,
                Color = color,
                Bonus = new IngredientDef(id, name, color, type),
            };
        }

        public static readonly AbilityDef[] All =
        {
            Def(AbilityType.SlowMo, "bonus_slow", "Замедление", "время ×0,5", 5f, "#7FC8FF"),
            Def(AbilityType.Multiplier, "bonus_double", "Очки ×2", "двойные очки", 8f, "#FFC93C"),
            Def(AbilityType.Shield, "bonus_shield", "Щит", "лишнее не режется", 6f, "#6FA8FF"),
        };

        public static AbilityDef Of(AbilityType type) => All[(int)type];
    }

    /// <summary>Заряды и активные способности одного раунда.</summary>
    public sealed class AbilityState
    {
        readonly int[] _charges = new int[Abilities.All.Length];
        readonly float[] _timeLeft = new float[Abilities.All.Length];

        /// <summary>Сколько бонусов разрезано и сколько способностей применено — для экрана результата.</summary>
        public int Caught { get; private set; }
        public int Used { get; private set; }

        public int Charges(AbilityType type) => _charges[(int)type];

        public bool IsActive(AbilityType type) => _timeLeft[(int)type] > 0f;

        /// <summary>Доля оставшегося времени активной способности, 0..1.</summary>
        public float ActiveFill(AbilityType type) => Mathf.Clamp01(_timeLeft[(int)type] / Abilities.Of(type).Duration);

        public float TimeLeft(AbilityType type) => _timeLeft[(int)type];

        /// <summary>Бонус разрезан. false — зарядов уже максимум (бонус всё равно засчитан как пойманный).</summary>
        public bool AddCharge(AbilityType type)
        {
            Caught++;
            if (_charges[(int)type] >= Abilities.MaxCharges) return false;
            _charges[(int)type]++;
            return true;
        }

        /// <summary>Потратить заряд. Нельзя, если зарядов нет или способность уже действует.</summary>
        public bool Activate(AbilityType type)
        {
            int i = (int)type;
            if (_charges[i] <= 0 || _timeLeft[i] > 0f) return false;
            _charges[i]--;
            _timeLeft[i] = Abilities.Of(type).Duration;
            Used++;
            return true;
        }

        /// <param name="realDt">Реальное время кадра: замедление не должно растягивать собственную длительность.</param>
        public void Tick(float realDt)
        {
            for (int i = 0; i < _timeLeft.Length; i++)
                if (_timeLeft[i] > 0f) _timeLeft[i] = Mathf.Max(0f, _timeLeft[i] - realDt);
        }

        /// <summary>Конец раунда: всё действующее выключается, заряды остаются для статистики.</summary>
        public void StopActive()
        {
            for (int i = 0; i < _timeLeft.Length; i++) _timeLeft[i] = 0f;
        }
    }
}
