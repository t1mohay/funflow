using UnityEngine;

namespace RecipeSlicer
{
    public sealed class IngredientDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly Color Color;

        public IngredientDef(string id, string name, Color color)
        {
            Id = id;
            Name = name;
            Color = color;
        }
    }

    public readonly struct RecipeItem
    {
        public readonly IngredientDef Ingredient;
        public readonly int Count;

        public RecipeItem(IngredientDef ingredient, int count)
        {
            Ingredient = ingredient;
            Count = count;
        }
    }

    /// <summary>Кривая сложности: значения в начале и в конце раунда, между ними линейная интерполяция.</summary>
    public sealed class DifficultyCurve
    {
        public float IntervalStart = 1.1f;
        public float IntervalEnd = 0.5f;
        public int MaxBurst = 3;
        public float WrongChanceStart = 0.15f;
        public float WrongChanceEnd = 0.3f;
        public float SpeedStart = 1f;
        public float SpeedEnd = 1.35f;
    }

    public sealed class RecipeDef
    {
        public string Id;
        public string Title;
        public string ReadyText;
        public string FailText;
        public string Description;
        /// <summary>Краткая история блюда — для листа рецепта перед готовкой.</summary>
        public string Lore;
        public int Stars;
        public float RoundTime = 60f;
        public RecipeItem[] Needed;
        public IngredientDef[] Wrong;
        public DifficultyCurve Difficulty = new DifficultyCurve();

        public int TotalNeeded
        {
            get
            {
                int sum = 0;
                foreach (var item in Needed) sum += item.Count;
                return sum;
            }
        }

        public int NeededCount(IngredientDef def)
        {
            foreach (var item in Needed)
                if (item.Ingredient == def) return item.Count;
            return 0;
        }
    }

    /// <summary>
    /// Все данные игры. Картинка ингредиента берётся из Assets/Resources/Ingredients/&lt;Id&gt;.png,
    /// если её нет, рисуется цветной кружок с подписью.
    /// </summary>
    public static class Recipes
    {
        static IngredientDef I(string id, string name, string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return new IngredientDef(id, name, color);
        }

        public static readonly IngredientDef Tomato = I("tomato", "Помидор", "#E63946");
        public static readonly IngredientDef Cucumber = I("cucumber", "Огурец", "#3FA34D");
        public static readonly IngredientDef Lettuce = I("lettuce", "Салат", "#9BE564");
        public static readonly IngredientDef Onion = I("onion", "Лук", "#EBD9B4");
        public static readonly IngredientDef Pepper = I("pepper", "Перец", "#E53935");
        public static readonly IngredientDef Cheese = I("cheese", "Сыр", "#FFD166");
        public static readonly IngredientDef Mushroom = I("mushroom", "Грибы", "#B08968");
        public static readonly IngredientDef Bacon = I("bacon", "Бекон", "#D35D6E");
        public static readonly IngredientDef Olive = I("olive", "Оливки", "#3A2F3A");
        public static readonly IngredientDef Shrimp = I("shrimp", "Креветка", "#FF8C69");
        public static readonly IngredientDef Chili = I("chili", "Чили", "#C1121F");
        public static readonly IngredientDef Lime = I("lime", "Лайм", "#A7C957");
        public static readonly IngredientDef Coconut = I("coconut", "Кокос", "#E9DCC9");
        public static readonly IngredientDef Lemongrass = I("lemongrass", "Лемонграсс", "#5DA03A");
        public static readonly IngredientDef Galangal = I("galangal", "Галангал", "#E9C46A");

        public static readonly IngredientDef Chocolate = I("chocolate", "Шоколад", "#7B4B2A");
        public static readonly IngredientDef Donut = I("donut", "Пончик", "#8B5A2B");
        public static readonly IngredientDef Banana = I("banana", "Банан", "#FFE135");
        public static readonly IngredientDef Candy = I("candy", "Конфета", "#FF4FD8");
        public static readonly IngredientDef Fish = I("fish", "Рыба", "#5DADE2");
        public static readonly IngredientDef Sock = I("sock", "Носок", "#8E9AAF");
        public static readonly IngredientDef Pineapple = I("pineapple", "Ананас", "#F9C74F");
        public static readonly IngredientDef Strawberry = I("strawberry", "Клубника", "#FF2E4C");
        public static readonly IngredientDef IceCream = I("icecream", "Мороженое", "#FFF1E6");
        public static readonly IngredientDef Cookie = I("cookie", "Печенье", "#C68B59");
        public static readonly IngredientDef Potato = I("potato", "Картошка", "#C8A165");
        public static readonly IngredientDef Carrot = I("carrot", "Морковь", "#FF8C1A");
        public static readonly IngredientDef Bread = I("bread", "Хлеб", "#DDA15E");
        public static readonly IngredientDef Milk = I("milk", "Молоко", "#F8F9FA");
        public static readonly IngredientDef Apple = I("apple", "Яблоко", "#E5383B");

        public static readonly RecipeDef Salad = new RecipeDef
        {
            Id = "salad",
            Title = "Салат",
            ReadyText = "Салат готов!",
            FailText = "Салат не готов!",
            Description = "Свежий овощной.\nПроще простого.",
            Lore = "Учёные до сих пор спорят, кто придумал салат: диетолог или человек со сломанной плитой. " +
                   "Одно известно точно — шоколад туда клали лишь однажды, и того повара больше никто не видел.",
            Stars = 1,
            Needed = new[]
            {
                new RecipeItem(Tomato, 12),
                new RecipeItem(Cucumber, 12),
                new RecipeItem(Lettuce, 10),
                new RecipeItem(Onion, 8),
                new RecipeItem(Pepper, 8),
            },
            Wrong = new[] { Chocolate, Donut, Banana, Candy, Fish, Sock },
            Difficulty = new DifficultyCurve
            {
                IntervalStart = 0.95f, IntervalEnd = 0.5f, MaxBurst = 3,
                WrongChanceStart = 0.3f, WrongChanceEnd = 0.5f,
                SpeedStart = 0.95f, SpeedEnd = 1.25f,
            },
        };

        public static readonly RecipeDef Pizza = new RecipeDef
        {
            Id = "pizza",
            Title = "Пицца",
            ReadyText = "Пицца готова!",
            FailText = "Пицца не готова!",
            Description = "Сырная, с грибами.\nАнанас? Подумай.",
            Lore = "Пицца родом из Неаполя, где на ананас на тесте смотрят так, будто ты оскорбил чью-то бабушку. " +
                   "Сыр, томаты, грибы — классика. Клубника — это уже десерт, и нет, это не «фьюжн».",
            Stars = 2,
            Needed = new[]
            {
                new RecipeItem(Cheese, 10),
                new RecipeItem(Tomato, 9),
                new RecipeItem(Mushroom, 9),
                new RecipeItem(Bacon, 8),
                new RecipeItem(Olive, 7),
                new RecipeItem(Pepper, 7),
            },
            Wrong = new[] { Pineapple, Strawberry, IceCream, Cucumber, Cookie, Banana },
            Difficulty = new DifficultyCurve
            {
                IntervalStart = 0.95f, IntervalEnd = 0.5f, MaxBurst = 3,
                WrongChanceStart = 0.3f, WrongChanceEnd = 0.5f,
                SpeedStart = 1f, SpeedEnd = 1.35f,
            },
        };

        public static readonly RecipeDef TomYum = new RecipeDef
        {
            Id = "tomyum",
            Title = "Том Ям",
            ReadyText = "Том Ям готов!",
            FailText = "Том Ям не готов!",
            Description = "Острый тайский суп.\nКартошку не клади!",
            Lore = "Том Ям сначала обжигает язык, потом душу, а потом просит добавки. " +
                   "Тайцы варят его веками и ни разу не додумались положить туда картошку. Не будь первым.",
            Stars = 3,
            Needed = new[]
            {
                new RecipeItem(Shrimp, 9),
                new RecipeItem(Mushroom, 8),
                new RecipeItem(Chili, 7),
                new RecipeItem(Lime, 7),
                new RecipeItem(Coconut, 7),
                new RecipeItem(Lemongrass, 6),
                new RecipeItem(Galangal, 6),
            },
            Wrong = new[] { Potato, Carrot, Cheese, Bread, Milk, Apple, Bacon },
            Difficulty = new DifficultyCurve
            {
                IntervalStart = 1f, IntervalEnd = 0.45f, MaxBurst = 4,
                WrongChanceStart = 0.38f, WrongChanceEnd = 0.6f,
                SpeedStart = 1.05f, SpeedEnd = 1.45f,
            },
        };

        public static readonly RecipeDef[] All = { Salad, Pizza, TomYum };
    }
}
