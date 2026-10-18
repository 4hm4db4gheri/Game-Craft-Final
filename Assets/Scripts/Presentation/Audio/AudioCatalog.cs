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
        // Music (menu, phase and boss loops)
        MusicPhase1, MusicPhase2, MusicPhase3, MusicMenu, MusicBoss,
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
    /// <summary>The five event groups of an enemy type (v0.19 §20), plus a second warning for boss area attacks.</summary>
    public enum EnemySound { Presence, Warning, AreaWarning, Attack, Hit, Death }

    [Serializable]
    public sealed class EnemySoundDef
    {
        public List<AudioClip> Clips = new();
        [Range(0, 1)] public float Volume = 0.6f;
        /// <summary>Hit sounds reuse the shared impacts; this pitch makes each type recognisable.</summary>
        public float Pitch = 1f;
    }

    /// <summary>
    /// One audio profile per enemy type, boss and spawned type (27, v0.19 §20). Key: the EnemyId name, or
    /// "Boss_" + the BossId name. Built by Hollow > Audio > Rebuild Catalog from Assets/Audio/Enemies.
    /// </summary>
    [Serializable]
    public sealed class EnemyAudioProfile
    {
        public string Key;
        public EnemySoundDef Presence = new(), Warning = new(), AreaWarning = new(), Attack = new(), Hit = new(), Death = new();
        /// <summary>Average seconds between two presence sounds of one visible enemy.</summary>
        public float PresenceInterval = 3f;

        public EnemySoundDef Get(EnemySound s) => s switch
        {
            EnemySound.Presence => Presence, EnemySound.Warning => Warning, EnemySound.AreaWarning => AreaWarning,
            EnemySound.Attack => Attack, EnemySound.Hit => Hit, _ => Death,
        };
    }

    [CreateAssetMenu(menuName = "Hollow/Audio Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        public const string ResourcePath = "Audio/AudioCatalog";

        public List<CueDef> Cues = new();
        public List<EnemyAudioProfile> Enemies = new();

        /// <summary>v0.19 §20 mix defaults (the audio owner tunes them): at most this many world one-shots…</summary>
        public int MaxOneShots = 24;
        /// <summary>…of which this many are reserved for important warnings at once…</summary>
        public int MaxWarnings = 4;
        /// <summary>…and one enemy type repeats its presence sound at most this often (seconds).</summary>
        public float PresenceGap = 0.15f;
        /// <summary>How much music and effects drop under a big moment (boss entry, victory jingle).</summary>
        [Range(0, 1)] public float DuckLevel = 0.35f;

        Dictionary<AudioCue, CueDef> index;
        Dictionary<string, EnemyAudioProfile> enemyIndex;

        public EnemyAudioProfile Enemy(string key)
        {
            if (enemyIndex == null || enemyIndex.Count != Enemies.Count)
            {
                enemyIndex = new Dictionary<string, EnemyAudioProfile>();
                foreach (var e in Enemies) enemyIndex[e.Key] = e;
            }
            return key != null && enemyIndex.TryGetValue(key, out var p) ? p : null;
        }

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
