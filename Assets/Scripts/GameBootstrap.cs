using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>
    /// Точка входа: после загрузки любой сцены создаёт игру, если её ещё нет.
    /// Поэтому в редакторе ничего настраивать не надо — открыл сцену, нажал Play.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("Game").AddComponent<GameManager>();
        }
    }
}
