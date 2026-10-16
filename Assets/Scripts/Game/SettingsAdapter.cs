using Hollow.App;
using Hollow.UI;

namespace Hollow.Game
{
    /// <summary>Exposes the persisted <see cref="GameSettings"/> to the settings screen.</summary>
    public sealed class SettingsAdapter : ISettingsModel
    {
        public float MusicVolume { get => GameSettings.MusicVolume; set => GameSettings.MusicVolume = value; }
        public float EffectsVolume { get => GameSettings.EffectsVolume; set => GameSettings.EffectsVolume = value; }
        public float ShakeIntensity { get => GameSettings.ShakeIntensity; set => GameSettings.ShakeIntensity = value; }
        public bool Fullscreen { get => GameSettings.Fullscreen; set => GameSettings.Fullscreen = value; }
        public bool DamageNumbers { get => GameSettings.DamageNumbers; set => GameSettings.DamageNumbers = value; }
        public int EffectBudget { get => GameSettings.EffectBudget; set => GameSettings.EffectBudget = value; }
        public void Save() => GameSettings.Save();
    }
}
