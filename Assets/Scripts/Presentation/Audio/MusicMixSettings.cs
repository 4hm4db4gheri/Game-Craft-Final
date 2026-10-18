using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>Live music mix levels, editable at Assets/Resources/Audio/MusicMixSettings.</summary>
    [CreateAssetMenu(menuName = "Hollow/Audio Music Mix")]
    public sealed class MusicMixSettings : ScriptableObject
    {
        public AudioClip MenuClip;
        public AudioClip Phase1Clip;
        public AudioClip Phase2Clip;
        public AudioClip Phase3Clip;
        public AudioClip BossClip;
        [Range(0f, 1f)] public float Menu = 0.6f;
        [Range(0f, 1f)] public float Phase1 = 0.65f;
        [Range(0f, 1f)] public float Phase2 = 0.65f;
        [Range(0f, 1f)] public float Phase3 = 0.65f;
        [Range(0f, 1f)] public float Boss = 0.75f;
        [Range(0f, 1f)] public float PauseMusicLevel = 0.18f;

        public float Level(AudioCue cue) => cue switch
        {
            AudioCue.MusicMenu => Menu,
            AudioCue.MusicPhase1 => Phase1,
            AudioCue.MusicPhase2 => Phase2,
            AudioCue.MusicPhase3 => Phase3,
            AudioCue.MusicBoss => Boss,
            _ => 0.65f,
        };

        public AudioClip Clip(AudioCue cue) => cue switch
        {
            AudioCue.MusicMenu => MenuClip,
            AudioCue.MusicPhase1 => Phase1Clip,
            AudioCue.MusicPhase2 => Phase2Clip,
            AudioCue.MusicPhase3 => Phase3Clip,
            AudioCue.MusicBoss => BossClip,
            _ => null,
        };
    }
}
