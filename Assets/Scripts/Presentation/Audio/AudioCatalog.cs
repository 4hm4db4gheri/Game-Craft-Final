using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>Every sound the game can play (GDD §14 audio priorities, T19 cue list).</summary>
    public enum AudioCue
    {
        // Firing per weapon family
        ShotPistol, ShotSmg, ShotShotgun, ShotRifle, ShotEnergy, ShotEnergyShotgun, MeleeSwing, CompanionShot, EnemyShot,
        // Impacts and deaths
        ImpactFlesh, ImpactMetal, ImpactWall, DeathAnimal, DeathRobot, DeathHuman,
        // Player
        Reload, ReloadDone, WeaponSwap, Dash, PlayerHurt,
        // Economy and progression
        CoinDrop, CoinPickup, Purchase, Denied, XpReady, Upgrade,
        // Companion
        CompanionWarning,
        // Attack windups / telegraphs per family
        TelegraphAnimal, TelegraphRobot, TelegraphHuman, TelegraphBoss,
        // Items
        Explosion, GrenadeThrow, Push, MinePlace, Emp, Shield,
        // Flow
        WaveStart, WaveClear, BossEntry, BossDefeated, UiClick, Victory,
        // Music (one loop per phase)
        MusicPhase1, MusicPhase2, MusicPhase3,
    }

    /// <summary>Mixing buses. Warnings are never ducked, so telegraph sounds always cut through (T19).</summary>
    public enum AudioBus { Music, Effects, Warnings, Interface, Voice }

    [Serializable]
    public sealed class CueDef
    {
        public AudioCue Cue;
        public List<AudioClip> Clips = new();
        public AudioBus Bus = AudioBus.Effects;
        [Range(0, 1)] public float Volume = 0.8f;
        /// <summary>Random pitch spread (± this fraction) so repeated shots do not sound identical.</summary>
        public float PitchJitter = 0.05f;
        /// <summary>Minimum real seconds between two plays of this cue (crowds, SMG bursts).</summary>
        public float Cooldown = 0.03f;
        /// <summary>When every voice is busy, a cue may take the voice of a lower-priority sound.</summary>
        public int Priority = 50;
    }

    /// <summary>
    /// Cue → clips and mix settings. Built by Hollow > Audio > Rebuild Catalog from the files in Assets/Audio
    /// (named &lt;Cue&gt;_&lt;n&gt;.ogg/.wav); licences are in docs/CREDITS.md. Cues without a file play a silent stub.
    /// </summary>
    [CreateAssetMenu(menuName = "Hollow/Audio Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        public const string ResourcePath = "Audio/AudioCatalog";

        public List<CueDef> Cues = new();
        /// <summary>How much music and effects drop under a big moment (boss entry, victory jingle).</summary>
        [Range(0, 1)] public float DuckLevel = 0.35f;

        Dictionary<AudioCue, CueDef> index;

        public CueDef Get(AudioCue cue)
        {
            if (index == null || index.Count != Cues.Count)
            {
                index = new Dictionary<AudioCue, CueDef>();
                foreach (var c in Cues) index[c.Cue] = c;
            }
            return index.TryGetValue(cue, out var d) ? d : null;
        }
    }
}
