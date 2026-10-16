using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hollow.Presentation;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Audio import rules and the cue catalog (T19). Files live in Assets/Audio/Sfx and Assets/Audio/Music,
    /// named &lt;Cue&gt;_&lt;n&gt; (several files per cue are played at random). Each file's source and licence are in
    /// docs/CREDITS.md. Cues without a file keep a silent stub at runtime.
    /// </summary>
    public static class AudioSetup
    {
        public const string AudioDir = "Assets/Audio";
        public const string CatalogPath = "Assets/Resources/Audio/AudioCatalog.asset";

        // Mix defaults per cue: bus, volume, cooldown (s), priority. Warnings are never ducked and keep their own voices.
        static readonly Dictionary<AudioCue, (AudioBus Bus, float Volume, float Cooldown, int Priority)> Mix = new()
        {
            [AudioCue.ShotPistol] = (AudioBus.Effects, 0.55f, 0.03f, 60),
            [AudioCue.ShotSmg] = (AudioBus.Effects, 0.45f, 0.045f, 60),
            [AudioCue.ShotShotgun] = (AudioBus.Effects, 0.65f, 0.05f, 65),
            [AudioCue.ShotRifle] = (AudioBus.Effects, 0.6f, 0.05f, 62),
            [AudioCue.ShotEnergy] = (AudioBus.Effects, 0.55f, 0.05f, 62),
            [AudioCue.ShotEnergyShotgun] = (AudioBus.Effects, 0.6f, 0.05f, 65),
            [AudioCue.MeleeSwing] = (AudioBus.Effects, 0.6f, 0.05f, 60),
            [AudioCue.CompanionShot] = (AudioBus.Effects, 0.4f, 0.1f, 40),
            [AudioCue.EnemyShot] = (AudioBus.Effects, 0.5f, 0.06f, 55),
            [AudioCue.ImpactFlesh] = (AudioBus.Effects, 0.5f, 0.04f, 30),
            [AudioCue.ImpactMetal] = (AudioBus.Effects, 0.5f, 0.04f, 30),
            [AudioCue.ImpactWall] = (AudioBus.Effects, 0.35f, 0.06f, 20),
            [AudioCue.DeathAnimal] = (AudioBus.Effects, 0.6f, 0.05f, 45),
            [AudioCue.DeathRobot] = (AudioBus.Effects, 0.6f, 0.05f, 45),
            [AudioCue.DeathHuman] = (AudioBus.Effects, 0.6f, 0.05f, 45),
            [AudioCue.Reload] = (AudioBus.Effects, 0.6f, 0.1f, 70),
            [AudioCue.ReloadDone] = (AudioBus.Effects, 0.5f, 0.1f, 70),
            [AudioCue.WeaponSwap] = (AudioBus.Effects, 0.5f, 0.05f, 70),
            [AudioCue.Dash] = (AudioBus.Effects, 0.6f, 0.1f, 75),
            [AudioCue.PlayerHurt] = (AudioBus.Warnings, 0.8f, 0.15f, 90),
            [AudioCue.CoinDrop] = (AudioBus.Effects, 0.35f, 0.06f, 20),
            [AudioCue.CoinPickup] = (AudioBus.Effects, 0.5f, 0.035f, 35),
            [AudioCue.Purchase] = (AudioBus.Interface, 0.7f, 0.05f, 80),
            [AudioCue.Denied] = (AudioBus.Interface, 0.6f, 0.15f, 80),
            [AudioCue.XpReady] = (AudioBus.Interface, 0.7f, 0.2f, 80),
            [AudioCue.Upgrade] = (AudioBus.Interface, 0.8f, 0.1f, 80),
            [AudioCue.CompanionWarning] = (AudioBus.Warnings, 0.9f, 1.2f, 95),
            [AudioCue.TelegraphAnimal] = (AudioBus.Warnings, 0.75f, 0.12f, 90),
            [AudioCue.TelegraphRobot] = (AudioBus.Warnings, 0.75f, 0.12f, 90),
            [AudioCue.TelegraphHuman] = (AudioBus.Warnings, 0.75f, 0.12f, 90),
            [AudioCue.TelegraphBoss] = (AudioBus.Warnings, 1f, 0.3f, 100),
            [AudioCue.Explosion] = (AudioBus.Effects, 0.8f, 0.05f, 80),
            [AudioCue.GrenadeThrow] = (AudioBus.Effects, 0.6f, 0.1f, 70),
            [AudioCue.Push] = (AudioBus.Effects, 0.7f, 0.1f, 70),
            [AudioCue.MinePlace] = (AudioBus.Effects, 0.6f, 0.1f, 70),
            [AudioCue.Emp] = (AudioBus.Effects, 0.8f, 0.1f, 70),
            [AudioCue.Shield] = (AudioBus.Effects, 0.7f, 0.1f, 70),
            [AudioCue.WaveStart] = (AudioBus.Interface, 0.6f, 0.5f, 85),
            [AudioCue.WaveClear] = (AudioBus.Interface, 0.7f, 0.5f, 85),
            [AudioCue.BossEntry] = (AudioBus.Interface, 1f, 1f, 100),
            [AudioCue.BossDefeated] = (AudioBus.Interface, 0.9f, 1f, 100),
            [AudioCue.UiClick] = (AudioBus.Interface, 0.5f, 0.03f, 60),
            [AudioCue.Victory] = (AudioBus.Interface, 1f, 1f, 100),
            [AudioCue.MusicPhase1] = (AudioBus.Music, 0.55f, 0f, 100),
            [AudioCue.MusicPhase2] = (AudioBus.Music, 0.55f, 0f, 100),
            [AudioCue.MusicPhase3] = (AudioBus.Music, 0.55f, 0f, 100),
        };

        [MenuItem("Hollow/Audio/Rebuild Catalog")]
        public static AudioCatalog BuildCatalog()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var clips = new List<(string Name, AudioClip Clip)>();
            if (AssetDatabase.IsValidFolder(AudioDir))
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    clips.Add((Path.GetFileNameWithoutExtension(path), AssetDatabase.LoadAssetAtPath<AudioClip>(path)));
                }

            catalog.Cues.Clear();
            var missing = new List<AudioCue>();
            foreach (AudioCue cue in System.Enum.GetValues(typeof(AudioCue)))
            {
                (AudioBus Bus, float Volume, float Cooldown, int Priority) mix = Mix.TryGetValue(cue, out var m) ? m : (AudioBus.Effects, 0.7f, 0.05f, 50);
                var def = new CueDef
                {
                    Cue = cue, Bus = mix.Bus, Volume = mix.Volume, Cooldown = mix.Cooldown, Priority = mix.Priority,
                    PitchJitter = mix.Bus == AudioBus.Effects ? 0.06f : 0f,
                };
                string name = cue.ToString();
                def.Clips.AddRange(clips.Where(c => c.Name == name || c.Name.StartsWith(name + "_")).OrderBy(c => c.Name).Select(c => c.Clip));
                if (def.Clips.Count == 0) missing.Add(cue);
                catalog.Cues.Add(def);
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Hollow audio] Catalog: {catalog.Cues.Count - missing.Count} cue(s) with files, {missing.Count} silent stub(s): {string.Join(", ", missing)}");
            return catalog;
        }
    }

    /// <summary>Import settings: effects mono and decompressed on load (short, played often); music stereo and streamed.</summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioSetup.AudioDir)) return;
            var imp = (AudioImporter)assetImporter;
            bool music = assetPath.Contains("/Music/");
            imp.forceToMono = !music;
            imp.loadInBackground = music;
            var s = imp.defaultSampleSettings;
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.5f : 0.6f;
            s.preloadAudioData = !music;
            imp.defaultSampleSettings = s;
        }
    }
}
