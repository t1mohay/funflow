using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>Выбрасывает ингредиенты снизу по дуге; частота, количество и доля лишних растут к концу раунда.</summary>
    public sealed class Spawner
    {
        const float Gravity = 9f;
        const float Radius = 0.72f;
        const float CompletedWeight = 0.6f; // уже собранное летит заметно — ловушка перебора
        const int MaxSameSide = 2;
        const float ClusterChance = 0.5f;
        const float ClusterStep = 1.3f;

        readonly GameManager _game;
        float _timer;
        int _sortingCounter;
        bool _lastFromLeft;
        int _sameSideStreak;

        public Spawner(GameManager game)
        {
            _game = game;
        }

        public void Reset()
        {
            _timer = 0.2f;
            _sameSideStreak = 0;
        }

        /// <param name="progress">0..1 — доля прошедшего времени раунда.</param>
        public void Tick(float dt, float progress, RoundState round)
        {
            _timer -= dt;
            if (_timer > 0f) return;

            var d = round.Recipe.Difficulty;
            _timer = Mathf.Lerp(d.IntervalStart, d.IntervalEnd, progress) * Random.Range(0.85f, 1.15f);
            int maxBurst = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, d.MaxBurst, progress)));
            int count = Random.Range(1, maxBurst + 1);
            float speed = Mathf.Lerp(d.SpeedStart, d.SpeedEnd, progress);
            float wrongChance = Mathf.Lerp(d.WrongChanceStart, d.WrongChanceEnd, progress);

            // Иногда пачка летит «гроздью»: с одной стороны столбиком — её можно срезать одним свайпом (комбо).
            bool cluster = count >= 2 && Random.value < ClusterChance;
            var area = _game.PlayArea;
            bool clusterLeft = ChooseSide();
            float clusterY = Random.Range(area.yMin + 1f, area.yMin + 3.5f - ClusterStep * (count - 1));
            float clusterApex = Random.Range(area.yMax - 4f, area.yMax - 1.3f - ClusterStep * (count - 1));
            float clusterTravel = Random.Range(0.55f, 0.95f);

            // В одной пачке не больше одного лишнего ингредиента.
            bool wrongSpawned = false;
            for (int i = 0; i < count; i++)
            {
                bool wrong = !wrongSpawned && Random.value < wrongChance;
                wrongSpawned |= wrong;
                var def = wrong ? PickWrong(round.Recipe) : PickNeeded(round);
                if (cluster)
                    Spawn(def, speed, clusterLeft, clusterY + ClusterStep * i, clusterApex + ClusterStep * i, clusterTravel);
                else
                    Spawn(def, speed, i == 0 ? clusterLeft : ChooseSide(), Random.Range(area.yMin + 1f, area.yMin + 3.5f),
                        Random.Range(area.yMax - 4f, area.yMax - 1.3f), Random.Range(0.55f, 0.95f));
            }
            _game.Sfx.PlayLaunch();
        }

        /// <summary>Сторона случайная, но не больше MaxSameSide подряд с одной стороны.</summary>
        bool ChooseSide()
        {
            bool fromLeft = _sameSideStreak >= MaxSameSide ? !_lastFromLeft : Random.value < 0.5f;
            _sameSideStreak = fromLeft == _lastFromLeft ? _sameSideStreak + 1 : 1;
            _lastFromLeft = fromLeft;
            return fromLeft;
        }

        static IngredientDef PickWrong(RecipeDef recipe) => recipe.Wrong[Random.Range(0, recipe.Wrong.Length)];

        static IngredientDef PickNeeded(RoundState round)
        {
            var recipe = round.Recipe;
            // Чего не хватает больше — летит чаще; уже собранное летит редко (ловушка «перебора»).
            float total = 0f;
            foreach (var item in recipe.Needed) total += Weight(round, item);
            float roll = Random.value * total;
            foreach (var item in recipe.Needed)
            {
                roll -= Weight(round, item);
                if (roll <= 0f) return item.Ingredient;
            }
            return recipe.Needed[recipe.Needed.Length - 1].Ingredient;
        }

        static float Weight(RoundState round, RecipeItem item)
        {
            int remaining = item.Count - round.Collected(item.Ingredient);
            return remaining > 0 ? 1f + remaining : CompletedWeight;
        }

        /// <param name="travelShare">Какую долю ширины поля ингредиент пролетит по горизонтали.</param>
        void Spawn(IngredientDef def, float speed, bool fromLeft, float y, float apex, float travelShare)
        {
            var area = _game.PlayArea;
            float x = fromLeft ? area.xMin - Radius : area.xMax + Radius;

            // Парабола ветвями вниз: подъём до вершины и падение уже на другой половине поля.
            float vy = Mathf.Sqrt(2f * Gravity * (apex - y));
            float airTime = 2f * vy / Gravity;
            float travel = area.width * travelShare;
            float vx = (fromLeft ? travel : -travel) / airTime;

            var ingredient = new GameObject(def.Name).AddComponent<Ingredient>();
            _sortingCounter = (_sortingCounter + 1) % 600;
            ingredient.Init(_game, def, new Vector2(x, y), new Vector2(vx, vy), Gravity, speed, Radius,
                10 + _sortingCounter * 6);
            _game.Register(ingredient);
        }
    }
}
