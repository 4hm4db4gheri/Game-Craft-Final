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
    /// Buses are mixed in code; MusicMixSettings exposes their levels in the Unity Inspector.
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
            /// <summary>The enemy (or boss) that made this sound: its death cancels pending warnings.</summary>
            public int OwnerId = int.MinValue;
        }

        AudioCatalog catalog;
        readonly Dictionary<AudioBus, List<Voice>> voices = new();
        readonly Dictionary<AudioCue, float> lastPlayed = new();
        readonly Dictionary<AudioCue, AudioClip> stubs = new();
        AudioSource musicA, musicB;
        MusicMixSettings mix;
        AudioCue? currentMusic;
        float musicVolume = 0.7f, effectsVolume = 0.8f;
        float duck = 1f, duckUntil;
        float musicFadeStart, musicFadeTime, musicOutStart;
        bool musicFading;
        float pauseGain = 1f, storyGain = 1f;
        bool paused, story;

        /// <summary>Cues that have no audio file yet and play silent stubs.</summary>
        public readonly List<AudioCue> MissingCues = new();
        /// <summary>Every cue played since start (checks that each cue is reachable in game).</summary>
        public readonly HashSet<AudioCue> Played = new();

        /// <summary>Enter Play Mode without Domain Reload keeps statics between sessions: start every session clean.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public static AudioDirector Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AudioDirector");
            DontDestroyOnLoad(go);
            // The game's one listener lives here, so every scene can be heard (the scene cameras are built
            // in code without one). Sounds are 2D and panned by hand, so where it sits does not matter.
            if (FindObjectsByType<AudioListener>().Length == 0) go.AddComponent<AudioListener>();
            Instance = go.AddComponent<AudioDirector>();
            Instance.Init(Resources.Load<AudioCatalog>(AudioCatalog.ResourcePath));
            return Instance;
        }

        void Init(AudioCatalog c)
        {
            catalog = c != null ? c : ScriptableObject.CreateInstance<AudioCatalog>();
            mix = Resources.Load<MusicMixSettings>("Audio/MusicMixSettings");
            // v0.19 §20: at most MaxOneShots world sounds, MaxWarnings of them important warnings.
            CreateVoices(AudioBus.Effects, Mathf.Max(1, catalog.MaxOneShots - catalog.MaxWarnings));
            CreateVoices(AudioBus.Warnings, Mathf.Max(1, catalog.MaxWarnings));
            CreateVoices(AudioBus.Interface, 4);
            voiceSource = NewSource("VoiceLine", false); // one spoken line at a time (v0.19 §20)
            musicA = NewSource("MusicA", true);
            musicB = NewSource("MusicB", true);

            foreach (AudioCue cue in System.Enum.GetValues(typeof(AudioCue)))
            {
                var d = catalog.Get(cue);
                if ((d == null || d.Clips.Count == 0 || d.Clips.TrueForAll(x => x == null)) &&
                    (mix == null || mix.Clip(cue) == null)) MissingCues.Add(cue);
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

        /// <summary>Settings volumes (§11, v0.19 §15), 0–1.</summary>
        public void SetVolumes(float music, float effects, float voice)
        {
            musicVolume = Mathf.Clamp01(music);
            effectsVolume = Mathf.Clamp01(effects);
            voiceVolume = Mathf.Clamp01(voice);
            if (voiceSource != null) voiceSource.volume = voiceVolume;
        }

        float voiceVolume = 1f;
        AudioSource voiceSource;

        /// <summary>
        /// Plays the recorded line at <paramref name="resourcePath"/> (Resources/Dialogez/&lt;n&gt; for the story scenes)
        /// if it exists. Lines without a file are silent: the subtitle carries them.
        /// </summary>
        public bool PlayVoiceLine(string resourcePath)
        {
            voiceSource?.Stop();
            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null || voiceSource == null) return false;
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.volume = voiceVolume;
            voiceSource.Play();
            return true;
        }

        public bool VoicePlaying => voiceSource != null && voiceSource.isPlaying;

        /// <summary>Skipping a scene or a cancelled line stops its audio at once.</summary>
        public void StopVoice()
        {
            if (voiceSource != null) voiceSource.Stop();
        }

        // ------------------------------------------------------------------ play

        public void Play(AudioCue cue, Vector2? worldPosition = null, float volumeScale = 1f) => PlayPitched(cue, worldPosition, 1f, volumeScale);

        void PlayPitched(AudioCue cue, Vector2? worldPosition, float pitch, float volumeScale = 1f)
        {
            var d = catalog.Get(cue);
            float now = Time.unscaledTime;
            float cooldown = d != null ? d.Cooldown : 0.05f;
            if (lastPlayed.TryGetValue(cue, out float last) && now - last < cooldown) return;
            lastPlayed[cue] = now;
            Played.Add(cue);

            var clip = PickClip(cue, d);
            var bus = d != null ? d.Bus : AudioBus.Effects;
            var voice = TakeVoice(bus, d != null ? d.Priority : 50);
            if (voice == null) return;

            var s = voice.Source;
            s.Stop();
            s.clip = clip;
            s.pitch = pitch * (1f + Random.Range(-1f, 1f) * (d != null ? d.PitchJitter : 0f));
            voice.OwnerId = int.MinValue;
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
            AudioBus.Music => musicVolume * duck * pauseGain * storyGain,
            AudioBus.Effects => effectsVolume * duck,
            _ => effectsVolume, // Warnings and interface are never ducked
        };

        /// <summary>Briefly lowers music and effects (never warnings) under a big moment.</summary>
        public void Duck(float seconds) => duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + seconds);

        // ------------------------------------------------------------------ story typing

        readonly Dictionary<string, AudioClip> typeClips = new();

        /// <summary>
        /// A letter typed in a story scene (D98), in the speaker's voice: a soft key for people, a digital blip for
        /// the AI, a harsh buzz for alarms, a crackle over the radio; "Cut" is the low hit of a hard cut.
        /// Synthesised here, so they need no files.
        /// </summary>
        public void PlayTypeTick(string style)
        {
            if (!typeClips.TryGetValue(style ?? "", out var clip))
            {
                clip = SynthTypeClip(style ?? "");
                typeClips[style ?? ""] = clip;
            }
            var voice = TakeVoice(AudioBus.Interface, 5);
            if (voice == null) return;
            var s = voice.Source;
            s.Stop();
            s.clip = clip;
            s.pitch = style == "Cut" ? 1f : Random.Range(0.92f, 1.1f);
            voice.OwnerId = int.MinValue;
            voice.BaseVolume = style switch { "Cut" => 0.7f, "Machine" => 0.16f, "Alarm" => 0.14f, "Radio" => 0.16f, _ => 0.22f };
            voice.Priority = 5;
            voice.StartedAt = Time.unscaledTime;
            s.panStereo = 0f;
            s.volume = voice.BaseVolume * BusGain(AudioBus.Interface);
            s.Play();
        }

        static AudioClip SynthTypeClip(string style)
        {
            const int rate = 44100;
            float seconds = style switch { "Cut" => 0.45f, "Alarm" => 0.045f, "Machine" => 0.04f, _ => 0.03f };
            int n = (int)(rate * seconds);
            var samples = new float[n];
            var rng = new System.Random(style.GetHashCode());
            float lowPass = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                float v = style switch
                {
                    "Machine" => (Mathf.Sin(2 * Mathf.PI * 1560f * t) * 0.7f + Mathf.Sin(2 * Mathf.PI * 3120f * t) * 0.2f) * Mathf.Exp(-t / 0.012f),
                    "Alarm" => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 520f * t)) * 0.45f * Mathf.Exp(-t / 0.018f),
                    "Radio" => noise * (rng.NextDouble() < 0.3 ? 1f : 0.35f) * Mathf.Exp(-t / 0.01f),
                    "Cut" => (Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(70f, 34f, t / seconds) * t) * 0.9f + noise * 0.25f * Mathf.Exp(-t / 0.03f)) * Mathf.Exp(-t / 0.16f),
                    _ => (noise * 0.8f + Mathf.Sin(2 * Mathf.PI * 880f * t) * 0.25f) * Mathf.Exp(-t / 0.005f),
                };
                lowPass += (v - lowPass) * (style == "Cut" ? 0.2f : 0.55f); // takes the hiss off
                samples[i] = lowPass;
            }
            var clip = AudioClip.Create("Type_" + style, n, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ------------------------------------------------------------------ music

        public void PlayMusic(int phase)
        {
            var cue = phase switch { 1 => AudioCue.MusicPhase1, 2 => AudioCue.MusicPhase2, _ => AudioCue.MusicPhase3 };
            StartMusic(cue);
        }

        public void PlayMenuMusic() => StartMusic(AudioCue.MusicMenu);
        public void PlayBossMusic(bool restart = false) => StartMusic(AudioCue.MusicBoss, restart);
        public void SetPaused(bool value) => paused = value;
        /// <summary>No music under a story scene: it fades out while the scene plays and back in after.</summary>
        public void SetStoryPlaying(bool value) => story = value;

        void StartMusic(AudioCue cue, bool restart = false)
        {
            if (!restart && currentMusic == cue && (musicA.isPlaying || musicB.isPlaying)) return;
            if (restart) { musicA.Stop(); musicB.Stop(); }
            currentMusic = cue;
            // Crossfade: the old loop fades out on B while the new one fades in on A.
            (musicA, musicB) = (musicB, musicA);
            musicOutStart = musicB.volume;
            musicA.clip = MusicClip(cue);
            Played.Add(cue);
            musicA.volume = 0f;
            musicA.Play();
            musicFadeStart = Time.unscaledTime;
            musicFadeTime = 1.5f;
            musicFading = true;
        }

        AudioClip MusicClip(AudioCue cue)
        {
            var clip = mix != null ? mix.Clip(cue) : null;
            return clip != null ? clip : PickClip(cue, catalog.Get(cue));
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
            pauseGain = Mathf.MoveTowards(pauseGain, paused ? (mix != null ? mix.PauseMusicLevel : 0.18f) : 1f,
                Time.unscaledDeltaTime * 2.5f);
            storyGain = Mathf.MoveTowards(storyGain, story ? 0f : 1f, Time.unscaledDeltaTime * (story ? 3f : 1f));
            duck = Mathf.MoveTowards(duck, Time.unscaledTime < duckUntil ? catalog.DuckLevel : 1f, Time.unscaledDeltaTime * 3f);
            foreach (var kv in voices)
                foreach (var v in kv.Value)
                    if (v.Source.isPlaying) v.Source.volume = v.BaseVolume * BusGain(kv.Key);

            float target = currentMusic.HasValue ? MusicGain(currentMusic.Value) : 0f;
            if (musicFading)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - musicFadeStart) / musicFadeTime);
                musicA.volume = target * k * BusGain(AudioBus.Music);
                musicB.volume = musicB.isPlaying ? (1f - k) * musicOutStart : 0f;
                if (k >= 1f) { musicFading = false; musicB.Stop(); }
            }
            else if (musicA.isPlaying) musicA.volume = target * BusGain(AudioBus.Music);
        }

        float MusicGain(AudioCue cue) => mix != null ? mix.Level(cue) : 0.65f;

        // ------------------------------------------------------------------ enemy profiles (v0.19 §20)

        static readonly string[] EnemyKeys = System.Enum.GetNames(typeof(EnemyId));
        static readonly string[] BossKeys = System.Array.ConvertAll(System.Enum.GetNames(typeof(BossId)), n => "Boss_" + n);
        readonly Dictionary<string, float> lastEnemySound = new();
        readonly Dictionary<int, float> nextPresence = new();
        readonly HashSet<int> detonated = new();

        static string KeyOf(World w, int entityId)
        {
            if (entityId == BossState.EntityId) return w.Boss.Active || w.Boss.Dead ? BossKeys[(int)w.Boss.Id] : null;
            foreach (var e in w.Enemies) if (e.Id == entityId) return EnemyKeys[(int)e.Type];
            return null;
        }

        /// <summary>Plays one event group of an enemy type. False when the type has no file for it (a fallback cue plays).</summary>
        bool PlayEnemy(string key, EnemySound sound, Vector2 position, int ownerId, float volumeScale = 1f)
        {
            var profile = catalog.Enemy(key);
            var def = profile?.Get(sound);
            if (def == null || def.Clips.Count == 0) return false;
            float now = Time.unscaledTime;
            string gapKey = key + (int)sound;
            float gap = sound == EnemySound.Presence ? catalog.PresenceGap : 0.04f;
            if (lastEnemySound.TryGetValue(gapKey, out float last) && now - last < gap) return true;
            lastEnemySound[gapKey] = now;

            bool warning = sound == EnemySound.Warning || sound == EnemySound.AreaWarning;
            var bus = warning ? AudioBus.Warnings : AudioBus.Effects;
            int priority = sound switch
            {
                EnemySound.Warning or EnemySound.AreaWarning => key.StartsWith("Boss_") ? 100 : 90,
                EnemySound.Attack => 55, EnemySound.Death => 45, EnemySound.Hit => 30, _ => 15,
            };
            var voice = TakeVoice(bus, priority);
            if (voice == null) return true;
            var s = voice.Source;
            s.Stop();
            s.clip = def.Clips[Random.Range(0, def.Clips.Count)];
            s.pitch = def.Pitch * (1f + Random.Range(-0.04f, 0.04f));
            voice.BaseVolume = def.Volume * volumeScale;
            voice.Priority = priority;
            voice.StartedAt = now;
            voice.OwnerId = ownerId;
            s.panStereo = Pan(position);
            s.volume = voice.BaseVolume * BusGain(bus);
            s.Play();
            return true;
        }

        /// <summary>v0.19 §20: death or a cancelled attack also cancels that enemy's pending warning sound.</summary>
        void CancelOwner(int ownerId)
        {
            foreach (var v in voices[AudioBus.Warnings])
                if (v.OwnerId == ownerId && v.Source.isPlaying) v.Source.Stop();
        }

        /// <summary>
        /// Presence sounds of visible enemies (steps, servos, breath), in simulation time so they pause with the
        /// world during XP choices and keep going while shopping. Each type repeats at most every PresenceGap.
        /// </summary>
        public void Ambient(World w)
        {
            float now = w.Clock.Tick * Hollow.Core.SimClock.Dt;
            foreach (var e in w.Enemies)
            {
                if (!e.IsActive || !OnScreen(e.Position)) continue;
                string key = EnemyKeys[(int)e.Type];
                var p = catalog.Enemy(key);
                if (p == null) continue;
                if (!nextPresence.TryGetValue(e.Id, out float next)) { nextPresence[e.Id] = now + Random.Range(0.2f, p.PresenceInterval); continue; }
                if (now < next) continue;
                nextPresence[e.Id] = now + p.PresenceInterval * Random.Range(0.7f, 1.3f);
                PlayEnemy(key, EnemySound.Presence, e.Position, e.Id);
            }
            var b = w.Boss;
            if (b.Active && !b.Dead && OnScreen(b.Position))
            {
                string key = BossKeys[(int)b.Id];
                var p = catalog.Enemy(key);
                if (p != null)
                {
                    if (!nextPresence.TryGetValue(BossState.EntityId, out float next)) nextPresence[BossState.EntityId] = now + p.PresenceInterval;
                    else if (now >= next)
                    {
                        nextPresence[BossState.EntityId] = now + p.PresenceInterval * Random.Range(0.8f, 1.2f);
                        PlayEnemy(key, EnemySound.Presence, b.Position, BossState.EntityId);
                    }
                }
            }
            Footsteps(w);
        }

        // Footsteps of Leila and the human enemies, one per stride walked. Faster walkers take longer strides, so
        // the cadence stays near a natural 2–3 steps a second. Display only; nothing here touches the simulation.
        const float StrideBase = 0.45f, StridePerSpeed = 0.22f, StrideMin = 0.8f, StrideMax = 1.5f, StepTeleport = 1.5f;
        readonly Dictionary<int, (Vector2 Pos, float Walked)> walkers = new();
        readonly List<int> gone = new();

        void Footsteps(World w)
        {
            float dt = Time.deltaTime;
            gone.Clear();
            foreach (var id in walkers.Keys) gone.Add(id);

            var c = w.Companion;
            if (c.Present && !c.Dead)
                Walk(CompanionState.EntityId, c.Position, dt, w.Phase == 1 ? AudioCue.StepSoft : AudioCue.StepHard);
            foreach (var e in w.Enemies)
            {
                if (!e.Alive || e.Ai == EnemyAi.Spawning || w.Profile.Enemy(e.Type).Faction != Faction.Humans) continue;
                if (OnScreen(e.Position)) Walk(e.Id, e.Position, dt, AudioCue.StepBoots);
            }
            foreach (var id in gone) walkers.Remove(id);
        }

        void Walk(int id, Vector2 at, float dt, AudioCue cue)
        {
            gone.Remove(id);
            if (!walkers.TryGetValue(id, out var s)) { walkers[id] = (at, Random.Range(0f, StrideMin)); return; }
            float moved = (at - s.Pos).magnitude;
            if (moved > StepTeleport) moved = 0f; // load, retry or phase change
            float speed = dt > 1e-4f ? moved / dt : 0f;
            float stride = Mathf.Clamp(StrideBase + speed * StridePerSpeed, StrideMin, StrideMax);
            float walked = s.Walked + moved;
            if (walked >= stride)
            {
                walked %= stride;
                Play(cue, at);
            }
            walkers[id] = (at, walked);
        }

        /// <summary>Forget per-enemy timers (load, retry, phase change).</summary>
        public void ResetWorldSounds()
        {
            nextPresence.Clear();
            detonated.Clear();
            walkers.Clear();
            foreach (var v in voices[AudioBus.Warnings]) v.Source.Stop();
        }

        // ------------------------------------------------------------------ simulation events

        public void OnSimEvent(World w, in SimEvent ev)
        {
            switch (ev.Type)
            {
                case SimEventType.EnemyTelegraph:
                {
                    string key = KeyOf(w, ev.EntityId);
                    bool area = ev.EntityId == BossState.EntityId && ev.Code == (int)TelegraphShape.Circle;
                    if (!PlayEnemy(key, area ? EnemySound.AreaWarning : EnemySound.Warning, ev.Position, ev.EntityId))
                        Play(TelegraphCue(w, ev.EntityId), ev.Position);
                    return;
                }
                case SimEventType.EnemyAttack:
                {
                    string key = ev.Code >= 0 ? EnemyKeys[ev.Code] : KeyOf(w, ev.EntityId);
                    if (ev.Code >= 0 && w.Profile.Enemy((EnemyId)ev.Code).Role == EnemyRole.Explosive) detonated.Add(ev.EntityId);
                    float scale = OnScreen(ev.Position) ? 1f : 0.5f;
                    if (!PlayEnemy(key, EnemySound.Attack, ev.Position, ev.EntityId, scale)
                        && ev.Code >= 0 && w.Profile.Enemy((EnemyId)ev.Code).Role == EnemyRole.Ranged)
                        Play(AudioCue.EnemyShot, ev.Position, scale);
                    return;
                }
                case SimEventType.ArmorBlocked:
                    // A bright metallic ting instead of the body's hit sound: the shot hit the shield.
                    shieldHitId = ev.EntityId;
                    PlayPitched(AudioCue.ImpactMetal, ev.Position, 1.45f);
                    return;
                case SimEventType.EnemyHit:
                {
                    if (ev.EntityId == shieldHitId) { shieldHitId = int.MinValue; return; }
                    // Shared impact layer, pitched per type so each enemy sounds distinct (v0.19 §20).
                    var cue = IsMetal(w, ev.EntityId) ? AudioCue.ImpactMetal : AudioCue.ImpactFlesh;
                    var p = catalog.Enemy(KeyOf(w, ev.EntityId));
                    if (p != null && p.Hit.Clips.Count > 0) PlayEnemy(p.Key, EnemySound.Hit, ev.Position, ev.EntityId);
                    else PlayPitched(cue, ev.Position, p != null ? p.Hit.Pitch : 1f);
                    return;
                }
                case SimEventType.EnemyDied:
                {
                    CancelOwner(ev.EntityId);
                    nextPresence.Remove(ev.EntityId);
                    bool blew = detonated.Remove(ev.EntityId); // a successful detonation was already heard
                    if (blew) return;
                    if (!PlayEnemy(EnemyKeys[ev.Code], EnemySound.Death, ev.Position, ev.EntityId))
                    {
                        var f = w.Profile.Enemy((EnemyId)ev.Code).Faction;
                        Play(f == Faction.Animals ? AudioCue.DeathAnimal : f == Faction.Robots ? AudioCue.DeathRobot : AudioCue.DeathHuman, ev.Position);
                    }
                    return;
                }
                case SimEventType.BossDefeated:
                    CancelOwner(BossState.EntityId);
                    PlayEnemy(BossKeys[ev.Code], EnemySound.Death, ev.Position, BossState.EntityId);
                    break;
            }
            switch (ev.Type)
            {
                case SimEventType.ShotFired:
                    if (ev.EntityId == PlayerState.EntityId) Play(ShotCue((WeaponId)ev.Code), ev.Position);
                    else Play(AudioCue.CompanionShot, ev.Position, 0.7f);
                    break;
                case SimEventType.MeleeSwing: Play(AudioCue.MeleeSwing, ev.Position); break;
                case SimEventType.ProjectileImpact when ev.Value == 0f:
                    Play(AudioCue.ImpactWall, ev.Position, 0.6f);
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
                case SimEventType.ObstacleBroken: Play(AudioCue.ImpactWall, ev.Position); Play(AudioCue.Explosion, ev.Position, 0.5f); break;
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

        /// <summary>The enemy whose next EnemyHit was already voiced as a shield ting.</summary>
        int shieldHitId = int.MinValue;

        static bool IsMetal(World w, int entityId)
        {
            if (entityId == BossState.EntityId) return w.Boss.Id == BossId.IndustrialRobot;
            foreach (var e in w.Enemies) if (e.Id == entityId) return w.Profile.Enemy(e.Type).Faction == Faction.Robots;
            return false;
        }
    }
}
