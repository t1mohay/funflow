using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>
    /// Время интерфейса и ножа: как unscaled-время, не останавливается паузой. Но при записи кадров
    /// (Time.captureDeltaTime, см. Dev/Trailer) идёт ровно по кадрам — иначе в записи нож, след и анимации
    /// интерфейса жили бы в реальном времени и не совпадали с игрой.
    /// </summary>
    public static class UiTime
    {
        static bool Capturing => Time.captureDeltaTime > 0f;

        public static float Delta => Capturing ? Time.captureDeltaTime : Time.unscaledDeltaTime;

        public static float Now => Capturing ? Time.frameCount * Time.captureDeltaTime : Time.unscaledTime;
    }
}
