using UnityEngine;

namespace RecipeSlicer
{
    /// <summary>
    /// Настройки игрока: громкость (общая, музыка, эффекты) и яркость.
    /// Изменения действуют сразу, но в PlayerPrefs попадают только по Save (кнопка «Сохранить»);
    /// Revert возвращает сохранённые значения. Разрешение и режим окна Unity запоминает сам.
    /// </summary>
    public static class GameSettings
    {
        public const float MinBrightness = 0.5f;
        public const float MaxBrightness = 1.5f;

        const string MasterKey = "settings_master";
        const string MusicKey = "settings_music";
        const string SfxKey = "settings_sfx";
        const string BrightnessKey = "settings_brightness";

        /// <summary>Общая громкость 0..1 (AudioListener.volume).</summary>
        public static float Master { get; private set; } = 1f;
        /// <summary>Громкость музыки 0..1 — множитель в Sfx.</summary>
        public static float Music { get; private set; } = 1f;
        /// <summary>Громкость звуковых эффектов 0..1 — множитель в Sfx.</summary>
        public static float Effects { get; private set; } = 1f;
        /// <summary>Яркость 0.5..1.5, 1 — без изменений.</summary>
        public static float Brightness { get; private set; } = 1f;

        /// <summary>Загрузить сохранённые значения (и отменить несохранённые изменения).</summary>
        public static void Load()
        {
            Master = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
            Music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
            Effects = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f));
            Brightness = Mathf.Clamp(PlayerPrefs.GetFloat(BrightnessKey, 1f), MinBrightness, MaxBrightness);
            AudioListener.volume = Master;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(MasterKey, Master);
            PlayerPrefs.SetFloat(MusicKey, Music);
            PlayerPrefs.SetFloat(SfxKey, Effects);
            PlayerPrefs.SetFloat(BrightnessKey, Brightness);
            PlayerPrefs.Save();
        }

        /// <summary>Значения по умолчанию (без сохранения).</summary>
        public static void ResetToDefaults()
        {
            SetMaster(1f);
            SetMusic(1f);
            SetEffects(1f);
            SetBrightness(1f);
        }

        public static bool HasUnsavedChanges() =>
            !Mathf.Approximately(Master, Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f)))
            || !Mathf.Approximately(Music, Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f)))
            || !Mathf.Approximately(Effects, Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f)))
            || !Mathf.Approximately(Brightness, Mathf.Clamp(PlayerPrefs.GetFloat(BrightnessKey, 1f), MinBrightness, MaxBrightness));

        public static void SetMaster(float value)
        {
            Master = Mathf.Clamp01(value);
            AudioListener.volume = Master;
        }

        public static void SetMusic(float value) => Music = Mathf.Clamp01(value);

        public static void SetEffects(float value) => Effects = Mathf.Clamp01(value);

        public static void SetBrightness(float value) => Brightness = Mathf.Clamp(value, MinBrightness, MaxBrightness);
    }
}
