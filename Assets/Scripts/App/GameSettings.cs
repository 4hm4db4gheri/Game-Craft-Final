using UnityEngine;

namespace Hollow.App
{
    /// <summary>
    /// Player settings (§11): music and effects volume, shake intensity, window or fullscreen, plus damage
    /// numbers and effect budget (D34). Stored in PlayerPrefs; these are preferences, not gameplay data.
    /// </summary>
    public static class GameSettings
    {
        const string KMusic = "hollow.music", KEffects = "hollow.effects", KShake = "hollow.shake",
            KFullscreen = "hollow.fullscreen", KNumbers = "hollow.damageNumbers", KBudget = "hollow.effectBudget";

        public static float MusicVolume { get => PlayerPrefs.GetFloat(KMusic, 0.7f); set => PlayerPrefs.SetFloat(KMusic, Mathf.Clamp01(value)); }
        public static float EffectsVolume { get => PlayerPrefs.GetFloat(KEffects, 0.8f); set => PlayerPrefs.SetFloat(KEffects, Mathf.Clamp01(value)); }
        /// <summary>0 = no camera shake, 1 = full data-defined shake.</summary>
        public static float ShakeIntensity { get => PlayerPrefs.GetFloat(KShake, 1f); set => PlayerPrefs.SetFloat(KShake, Mathf.Clamp01(value)); }
        public static bool Fullscreen { get => PlayerPrefs.GetInt(KFullscreen, 1) == 1; set { PlayerPrefs.SetInt(KFullscreen, value ? 1 : 0); ApplyWindow(); } }
        public static bool DamageNumbers { get => PlayerPrefs.GetInt(KNumbers, 1) == 1; set => PlayerPrefs.SetInt(KNumbers, value ? 1 : 0); }
        /// <summary>0 low, 1 medium, 2 high: scales the effect caps from the balance data.</summary>
        public static int EffectBudget { get => PlayerPrefs.GetInt(KBudget, 2); set => PlayerPrefs.SetInt(KBudget, Mathf.Clamp(value, 0, 2)); }

        public static void ApplyWindow()
        {
            if (Application.isEditor) return;
            Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
