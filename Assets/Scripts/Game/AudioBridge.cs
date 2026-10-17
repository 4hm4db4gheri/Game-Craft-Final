using Hollow.App;
using Hollow.Presentation;
using Hollow.UI;
using UnityEngine;

namespace Hollow.Game
{
    /// <summary>Connects the audio director to the player's volume settings and to UI button clicks.</summary>
    public static class AudioBridge
    {
        static bool attached;

        public static AudioDirector Attach()
        {
            var audio = AudioDirector.Ensure();
            ApplyVolumes();
            if (attached) return audio;
            attached = true;
            // Look the director up when the event fires: a captured one may belong to an earlier Play session.
            GameSettings.Changed += ApplyVolumes;
            UIFactory.ButtonClicked += () => { var a = AudioDirector.Instance; if (a != null) a.Play(AudioCue.UiClick); };
            return audio;
        }

        static void ApplyVolumes()
        {
            var a = AudioDirector.Instance;
            if (a != null) a.SetVolumes(GameSettings.MusicVolume, GameSettings.EffectsVolume, GameSettings.VoiceVolume);
        }

        /// <summary>Enter Play Mode without Domain Reload keeps statics between sessions: start every session clean.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => attached = false;
    }
}
