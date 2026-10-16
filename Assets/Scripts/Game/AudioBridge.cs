using Hollow.App;
using Hollow.Presentation;
using Hollow.UI;

namespace Hollow.Game
{
    /// <summary>Connects the audio director to the player's volume settings and to UI button clicks.</summary>
    public static class AudioBridge
    {
        static bool attached;

        public static AudioDirector Attach()
        {
            var audio = AudioDirector.Ensure();
            audio.SetVolumes(GameSettings.MusicVolume, GameSettings.EffectsVolume);
            if (attached) return audio;
            attached = true;
            GameSettings.Changed += () => audio.SetVolumes(GameSettings.MusicVolume, GameSettings.EffectsVolume);
            UIFactory.ButtonClicked += () => audio.Play(AudioCue.UiClick);
            return audio;
        }
    }
}
