using System.Collections;
using System.IO;
using UnityEngine;

namespace RecipeSlicer
{
    // Только для проверки: прогоняет меню и раунд и снимает скриншоты. Запуск: Game.exe -autotest <папка>
    public sealed class AutoTest : MonoBehaviour
    {
        string _dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-autotest");
            if (i < 0 || i + 1 >= args.Length) return;
            var go = new GameObject("AutoTest");
            go.AddComponent<AutoTest>()._dir = args[i + 1];
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            Shot("01_menu");
            yield return null;
            yield return null;
            var game = FindAnyObjectByType<GameManager>();
            game.StartRound(Recipes.All[2]);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("02_countdown");
            yield return new WaitForSecondsRealtime(3f);

            // Комбо: три нужных ингредиента подряд в соседних кадрах.
            var recipe = Recipes.All[2];
            var picked = new System.Collections.Generic.List<Ingredient>();
            while (picked.Count < 3)
            {
                picked.Clear();
                foreach (var ing in FindObjectsByType<Ingredient>())
                    if (!ing.IsSliced && recipe.NeededCount(ing.Def) > 0 && picked.Count < 3) picked.Add(ing);
                yield return null;
            }
            foreach (var ing in picked)
            {
                if (ing == null) continue;
                var p = ing.Position;
                game.OnSwipe(p + new Vector2(-1f, -0.4f), p + new Vector2(1f, 0.4f));
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.45f);
            Shot("combo");

            float end = Time.realtimeSinceStartup + 9f;
            bool shot = false;
            while (Time.realtimeSinceStartup < end)
            {
                foreach (var ing in FindObjectsByType<Ingredient>())
                {
                    if (ing == null || ing.IsSliced) continue;
                    if (ing.Position.y > -2.5f && Random.value < 0.04f)
                    {
                        var p = ing.Position;
                        game.OnSwipe(p + new Vector2(-1f, -0.4f), p + new Vector2(1f, 0.4f));
                    }
                }
                if (!shot && Time.realtimeSinceStartup > end - 5.85f) { Shot("03_round"); shot = true; }
                yield return null;
            }
            Shot("04_round_later");
            Time.timeScale = 6f;
            yield return new WaitForSecondsRealtime(9.5f);
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(0.8f);
            Shot("05_results");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
    }
}
