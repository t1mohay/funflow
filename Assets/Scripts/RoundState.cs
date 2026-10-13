using System.Collections.Generic;
using UnityEngine;

namespace RecipeSlicer
{
    public enum SliceResult
    {
        Needed,
        /// <summary>Нужный ингредиент, но его уже хватает: перебор, небольшой штраф.</summary>
        Overflow,
        Wrong,
    }

    /// <summary>Состояние одного раунда: что нарезано, штрафы и итоговая оценка 0–100.</summary>
    public sealed class RoundState
    {
        /// <summary>Очки за нужный ингредиент. В каждом рецепте ровно 50 штук, поэтому максимум — 100.</summary>
        public const int PointsPerIngredient = 2;
        public const int WrongPenalty = 5;
        public const int OverflowPenalty = 4;
        /// <summary>Бонус за каждый ингредиент комбо сверх второго: ×3 → +5, ×4 → +10.</summary>
        public const int ComboBonusPerExtra = 5;
        public const int ComboMin = 3;

        public int ComboCount { get; private set; }
        public int ComboBonus { get; private set; }
        /// <summary>Дополнительные очки от способности «Очки ×2».</summary>
        public int MultiplierBonus { get; private set; }

        public readonly RecipeDef Recipe;
        public float TimeLeft;

        public int WrongCount { get; private set; }
        public int OverflowCount { get; private set; }
        public IReadOnlyDictionary<IngredientDef, int> WrongByType => _wrong;

        readonly Dictionary<IngredientDef, int> _sliced = new Dictionary<IngredientDef, int>();
        readonly Dictionary<IngredientDef, int> _wrong = new Dictionary<IngredientDef, int>();

        public RoundState(RecipeDef recipe)
        {
            Recipe = recipe;
            TimeLeft = recipe.RoundTime;
        }

        public int Collected(IngredientDef def)
        {
            _sliced.TryGetValue(def, out int sliced);
            return Mathf.Min(sliced, Recipe.NeededCount(def));
        }

        public int TotalCollected
        {
            get
            {
                int sum = 0;
                foreach (var item in Recipe.Needed) sum += Collected(item.Ingredient);
                return sum;
            }
        }

        public bool IsComplete => TotalCollected >= Recipe.TotalNeeded;

        /// <summary>Сколько секунд прошло с начала раунда.</summary>
        public float Elapsed => Recipe.RoundTime - TimeLeft;

        public int Score
        {
            get
            {
                int score = PointsPerIngredient * TotalCollected + ComboBonus + MultiplierBonus
                            - WrongPenalty * WrongCount - OverflowPenalty * OverflowCount;
                // Потолка нет: больше 100 — «божественное блюдо».
                return Mathf.Max(score, 0);
            }
        }

        public int Stars
        {
            get
            {
                int score = Score;
                if (score >= 90) return 3;
                if (score >= 70) return 2;
                if (score >= 40) return 1;
                return 0;
            }
        }

        /// <summary>Чем станет разрез этого ингредиента — без изменения состояния (для щита).</summary>
        public SliceResult Peek(IngredientDef def)
        {
            int need = Recipe.NeededCount(def);
            if (need == 0) return SliceResult.Wrong;
            _sliced.TryGetValue(def, out int sliced);
            return sliced >= need ? SliceResult.Overflow : SliceResult.Needed;
        }

        /// <summary>Очки сверх обычных за время действия «Очки ×2».</summary>
        public void AddMultiplierBonus(int points) => MultiplierBonus += points;

        public SliceResult Register(IngredientDef def)
        {
            int need = Recipe.NeededCount(def);
            if (need == 0)
            {
                WrongCount++;
                _wrong.TryGetValue(def, out int wrong);
                _wrong[def] = wrong + 1;
                return SliceResult.Wrong;
            }

            _sliced.TryGetValue(def, out int sliced);
            _sliced[def] = sliced + 1;
            if (sliced >= need)
            {
                OverflowCount++;
                return SliceResult.Overflow;
            }
            return SliceResult.Needed;
        }

        /// <summary>Засчитать комбо из size нужных ингредиентов одним движением; возвращает бонус.</summary>
        public int AddCombo(int size)
        {
            int bonus = ComboBonusPerExtra * (size - ComboMin + 1);
            ComboCount++;
            ComboBonus += bonus;
            return bonus;
        }

        /// <summary>Код Конами: весь рецепт собран без единой ошибки — ровно 100 очков.</summary>
        public void CompletePerfectly()
        {
            foreach (var item in Recipe.Needed) _sliced[item.Ingredient] = item.Count;
            _wrong.Clear();
            WrongCount = 0;
            OverflowCount = 0;
            ComboCount = 0;
            ComboBonus = 0;
            MultiplierBonus = 0;
        }

        /// <summary>Самый частый лишний ингредиент, для вердикта шефа.</summary>
        public IngredientDef WorstWrong()
        {
            IngredientDef worst = null;
            int max = 0;
            foreach (var pair in _wrong)
            {
                if (pair.Value <= max) continue;
                max = pair.Value;
                worst = pair.Key;
            }
            return worst;
        }
    }
}
