using System.Collections.Generic;
using Hollow.Data;
using Hollow.Sim;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>
    /// Plays every cue (T19, §14): pooled voices per bus, per-cue cooldowns and priorities, stereo position from
    /// the camera, music per phase with crossfade, ducking of music and effects for big moments while the
    /// Warnings bus (attack telegraphs, companion warning) is never ducked and has its own voices.
    /// Cues without an imported file play a named silent stub that is logged once.
    /// Buses are mixed in code: Unity's AudioMixer assets cannot be authored from scripts (D55).
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        sealed class Voice
        {
            public AudioSource Source;
            public int Priority;
            public float StartedAt;
            public float BaseVolume;
        }

        AudioCatalog catalog;
        readonly Dictionary<AudioBus, List<Voice>> voices = new();
        readonly Dictionary<AudioCue, float> lastPlayed = new();
        readonly Dictionary<AudioCue, AudioClip> stubs = new();
        AudioSource musicA, musicB;
        AudioCue? currentMusic;
        float musicVolume = 0.7f, effectsVolume = 0.8f;
        float duck = 1f, duckUntil;
        float musicFadeStart, musicFadeTime, musicOutStart;
        bool musicFading;

        /// <summary>Cues that have no audio file yet and play silent stubs.</summary>
        public readonly List<AudioCue> MissingCues = new();

        public static AudioDirector Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AudioDirector");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AudioDirector>();
            Instance.Init(Resources.Load<AudioCatalog>(AudioCatalog.ResourcePath));
            return Instance;
        }

        void Init(AudioCatalog c)
        {
            catalog = c != null ? c : ScriptableObject.CreateInstance<AudioCatalog>();
            CreateVoices(AudioBus.Effects, 18);
            CreateVoices(AudioBus.Warnings, 6);
            CreateVoices(AudioBus.Interface, 4);
            musicA = NewSource("MusicA", true);
            musicB = NewSource("MusicB", true);

            foreach (AudioCue cue in System.Enum.GetValues(typeof(AudioCue)))
            {
                var d = catalog.Get(cue);
                if (d == null || d.Clips.Count == 0 || d.Clips.TrueForAll(x => x == null)) MissingCues.Add(cue);
            }
            if (MissingCues.Count > 0)
                Debug.Log($"[Hollow audio] {MissingCues.Count} cue(s) use silent stubs until files are added: {string.Join(", ", MissingCues)}");
        }

        void CreateVoices(AudioBus bus, int count)
        {
            var list = new List<Voice>();
            for (int i = 0; i < count; i++) list.Add(new Voice { Source = NewSource($"{bus}_{i}", false) });
            voices[bus] = list;
        }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f; // 2D game: stereo position is set by hand
            return s;
        }

        /// <summary>Settings volumes (§11), 0–1.</summary>
        public void SetVolumes(float music, float effects)
        {
            musicVolume = Mathf.Clamp01(music);
            effectsVolume = Mathf.Clamp01(effects);
        }

        // ------------------------------------------------------------------ play

        public void Play(AudioCue cue, Vector2? worldPosition = null, float volumeScale = 1f)
        {
            var d = catalog.Get(cue);
            float now = Time.unscaledTime;
            float cooldown = d != null ? d.Cooldown : 0.05f;
            if (lastPlayed.TryGetValue(cue, out float last) && now - last < cooldown) return;
            lastPlayed[cue] = now;

            var clip = PickClip(cue, d);
            var bus = d != null ? d.Bus : AudioBus.Effects;
            var voice = TakeVoice(bus, d != null ? d.Priority : 50);
            if (voice == null) return;

            var s = voice.Source;
            s.Stop();
            s.clip = clip;
            s.pitch = 1f + Random.Range(-1f, 1f) * (d != null ? d.PitchJitter : 0f);
            voice.BaseVolume = (d != null ? d.Volume : 0.8f) * volumeScale;
            voice.Priority = d != null ? d.Priority : 50;
            voice.StartedAt = now;
            s.panStereo = worldPosition.HasValue ? Pan(worldPosition.Value) : 0f;
            s.volume = voice.BaseVolume * BusGain(bus);
            s.Play();
        }

        AudioClip PickClip(AudioCue cue, CueDef d)
        {
            if (d != null && d.Clips.Count > 0)
            {
                var c = d.Clips[Random.Range(0, d.Clips.Count)];
                if (c != null) return c;
            }
            if (!stubs.TryGetValue(cue, out var stub))
            {
                stub = AudioClip.Create($"STUB_{cue}", 441, 1, 44100, false); // 10 ms of silence
                stubs[cue] = stub;
            }
            return stub;
        }

        Voice TakeVoice(AudioBus bus, int priority)
        {
            var list = voices[bus == AudioBus.Music ? AudioBus.Effects : bus];
            Voice oldestLower = null;
            foreach (var v in list)
            {
                if (!v.Source.isPlaying) return v;
                if (v.Priority <= priority && (oldestLower == null || v.StartedAt < oldestLower.StartedAt)) oldestLower = v;
            }
            return oldestLower; // null: every voice plays something more important, so this cue is dropped
        }

        static float Pan(Vector2 p)
        {
            var cam = Camera.main;
            if (cam == null) return 0f;
            float half = cam.orthographicSize * cam.aspect;
            return Mathf.Clamp((p.x - cam.transform.position.x) / Mathf.Max(1f, half), -1f, 1f) * 0.6f;
        }

        static bool OnScreen(Vector2 p)
        {
            var cam = Camera.main;
            if (cam == null) return true;
            Vector2 d = p - (Vector2)cam.transform.position;
            return Mathf.Abs(d.x) <= cam.orthographicSize * cam.aspect && Mathf.Abs(d.y) <= cam.orthographicSize;
        }

        float BusGain(AudioBus bus) => bus switch
        {
            AudioBus.Music => musicVolume * duck,
            AudioBus.Effects => effectsVolume * duck,
            _ => effectsVolume, // Warnings and interface are never ducked
        };

        /// <summary>Briefly lowers music and effects (never warnings) under a big moment.</summary>
        public void Duck(float seconds) => duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + seconds);

        // ------------------------------------------------------------------ music

        public void PlayMusic(int phase)
        {
            var cue = phase switch { 1 => AudioCue.MusicPhase1, 2 => AudioCue.MusicPhase2, _ => AudioCue.MusicPhase3 };
            if (currentMusic == cue && (musicA.isPlaying || musicB.isPlaying)) return;
            currentMusic = cue;
            // Crossfade: the old loop fades out on B while the new one fades in on A.
            (musicA, musicB) = (musicB, musicA);
            musicOutStart = musicB.volume;
            var d = catalog.Get(cue);
            musicA.clip = PickClip(cue, d);
            musicA.volume = 0f;
            musicA.Play();
            musicFadeStart = Time.unscaledTime;
            musicFadeTime = 1.5f;
            musicFading = true;
        }

        public void StopMusic(float fadeSeconds)
        {
            currentMusic = null;
            (musicA, musicB) = (musicB, musicA);
            musicA.Stop();
            musicOutStart = musicB.volume;
            musicFadeStart = Time.unscaledTime;
            musicFadeTime = Mathf.Max(0.01f, fadeSeconds);
            musicFading = true;
        }

        /// <summary>The excessively cheerful victory jingle, then silence (§3).</summary>
        public void PlayVictory()
        {
            StopMusic(0.2f);
            foreach (var v in voices[AudioBus.Effects]) v.Source.Stop();
            Play(AudioCue.Victory);
        }

        void Update()
        {
            duck = Mathf.MoveTowards(duck, Time.unscaledTime < duckUntil ? catalog.DuckLevel : 1f, Time.unscaledDeltaTime * 3f);
            foreach (var kv in voices)
                foreach (var v in kv.Value)
                    if (v.Source.isPlaying) v.Source.volume = v.BaseVolume * BusGain(kv.Key);

            float target = currentMusic.HasValue ? (catalog.Get(currentMusic.Value)?.Volume ?? 0.6f) : 0f;
            if (musicFading)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - musicFadeStart) / musicFadeTime);
                musicA.volume = target * k * BusGain(AudioBus.Music);
                musicB.volume = musicB.isPlaying ? (1f - k) * musicOutStart : 0f;
                if (k >= 1f) { musicFading = false; musicB.Stop(); }
            }
            else if (musicA.isPlaying) musicA.volume = target * BusGain(AudioBus.Music);
        }

        // ------------------------------------------------------------------ simulation events

        public void OnSimEvent(World w, in SimEvent ev)
        {
            switch (ev.Type)
            {
                case SimEventType.ShotFired:
                    if (ev.EntityId == PlayerState.EntityId) Play(ShotCue((WeaponId)ev.Code), ev.Position);
                    else Play(AudioCue.CompanionShot, ev.Position, 0.7f);
                    break;
                case SimEventType.MeleeSwing: Play(AudioCue.MeleeSwing, ev.Position); break;
                case SimEventType.EnemyAttack when ev.Code >= 0 && w.Profile.Enemy((EnemyId)ev.Code).Role == EnemyRole.Ranged:
                    Play(AudioCue.EnemyShot, ev.Position, OnScreen(ev.Position) ? 1f : 0.5f);
                    break;
                case SimEventType.EnemyTelegraph:
                    Play(TelegraphCue(w, ev.EntityId), ev.Position);
                    break;
                case SimEventType.EnemyHit:
                    Play(IsMetal(w, ev.EntityId) ? AudioCue.ImpactMetal : AudioCue.ImpactFlesh, ev.Position);
                    break;
                case SimEventType.ProjectileImpact when ev.Value == 0f:
                    Play(AudioCue.ImpactWall, ev.Position, 0.6f);
                    break;
                case SimEventType.EnemyDied:
                    var f = w.Profile.Enemy((EnemyId)ev.Code).Faction;
                    Play(f == Faction.Animals ? AudioCue.DeathAnimal : f == Faction.Robots ? AudioCue.DeathRobot : AudioCue.DeathHuman, ev.Position);
                    break;
                case SimEventType.ReloadStarted: Play(AudioCue.Reload); break;
                case SimEventType.ReloadFinished: Play(AudioCue.ReloadDone); break;
                case SimEventType.WeaponSwapped: Play(AudioCue.WeaponSwap); break;
                case SimEventType.PlayerDashStarted: Play(AudioCue.Dash); break;
                case SimEventType.PlayerHit: Play(AudioCue.PlayerHurt); break;
                case SimEventType.CoinDropped: Play(AudioCue.CoinDrop, ev.Position, 0.6f); break;
                case SimEventType.CoinCollected: Play(AudioCue.CoinPickup, ev.Position); break;
                case SimEventType.CompanionHit:
                    // Rate-limited by the cue cooldown; louder when the companion is off screen (§4).
                    Play(AudioCue.CompanionWarning, ev.Position, OnScreen(ev.Position) ? 0.7f : 1f);
                    break;
                case SimEventType.Explosion: Play(AudioCue.Explosion, ev.Position); break;
                case SimEventType.ItemUsed: Play(ItemCue((ItemId)ev.Code)); break;
                case SimEventType.WaveStarted: Play(AudioCue.WaveStart); break;
                case SimEventType.WaveCompleted: Play(AudioCue.WaveClear); break;
                case SimEventType.BossStarted: Duck(2.5f); Play(AudioCue.BossEntry); break;
                case SimEventType.BossDefeated: Duck(2f); Play(AudioCue.BossDefeated); break;
            }
        }

        static AudioCue ShotCue(WeaponId id) => id switch
        {
            WeaponId.Pistol => AudioCue.ShotPistol,
            WeaponId.LightSmg => AudioCue.ShotSmg,
            WeaponId.DoubleBarrel or WeaponId.MagazineShotgun => AudioCue.ShotShotgun,
            WeaponId.IndustrialRifle => AudioCue.ShotRifle,
            WeaponId.EnergyRifle => AudioCue.ShotEnergy,
            WeaponId.EnergyShotgun => AudioCue.ShotEnergyShotgun,
            _ => AudioCue.MeleeSwing,
        };

        static AudioCue ItemCue(ItemId id) => id switch
        {
            ItemId.Grenade => AudioCue.GrenadeThrow,
            ItemId.PushPulse => AudioCue.Push,
            ItemId.Mine => AudioCue.MinePlace,
            ItemId.EmpPulse => AudioCue.Emp,
            _ => AudioCue.Shield,
        };

        static AudioCue TelegraphCue(World w, int entityId)
        {
            if (entityId == BossState.EntityId) return AudioCue.TelegraphBoss;
            foreach (var e in w.Enemies)
                if (e.Id == entityId)
                    return w.Profile.Enemy(e.Type).Faction switch
                    {
                        Faction.Animals => AudioCue.TelegraphAnimal,
                        Faction.Robots => AudioCue.TelegraphRobot,
                        _ => AudioCue.TelegraphHuman,
                    };
            return AudioCue.TelegraphHuman;
        }

        static bool IsMetal(World w, int entityId)
        {
            if (entityId == BossState.EntityId) return w.Boss.Id == BossId.IndustrialRobot;
            foreach (var e in w.Enemies) if (e.Id == entityId) return w.Profile.Enemy(e.Type).Faction == Faction.Robots;
            return false;
        }
    }
}
