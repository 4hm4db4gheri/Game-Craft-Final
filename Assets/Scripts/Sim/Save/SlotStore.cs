using System;
using System.IO;
using UnityEngine;

namespace Hollow.Sim
{
    public readonly struct SaveResult
    {
        public readonly bool Ok;
        public readonly string Error;
        public SaveResult(bool ok, string error) { Ok = ok; Error = error; }
    }

    /// <summary>
    /// Three independent save slots as JSON files in persistentDataPath/saves (D34). Writes go to a temp
    /// file, are read back and checked, then replace the slot; success is reported only after that (§12).
    /// </summary>
    public static class SlotStore
    {
        public const int SlotCount = 3;

        /// <summary>Tests point this at a temporary folder.</summary>
        public static string RootOverride;

        /// <summary>Enter Play Mode without Domain Reload keeps statics between sessions: start every session clean.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => RootOverride = null; // a direct Game-scene session must not redirect the next menu session

        public static string Root => RootOverride ?? Path.Combine(Application.persistentDataPath, "saves");
        public static string PathOf(int slot) => Path.Combine(Root, $"slot{slot}.json");
        public static bool Exists(int slot) => File.Exists(PathOf(slot));

        public static SaveResult Write(int slot, SaveFile save)
        {
            if (slot < 1 || slot > SlotCount) return new SaveResult(false, $"No slot {slot}.");
            string path = PathOf(slot);
            string tmp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Root);
                string json = save.ToJson();
                File.WriteAllText(tmp, json);
                if (File.ReadAllText(tmp) != json) throw new IOException("Read-back of the temporary file did not match.");
                SaveFile.FromJson(json); // must parse
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                if (File.ReadAllText(path) != json) throw new IOException("Read-back of the slot did not match.");
                return new SaveResult(true, null);
            }
            catch (Exception e)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
                return new SaveResult(false, e.Message);
            }
        }

        public static SaveFile Read(int slot) => SaveFile.FromJson(File.ReadAllText(PathOf(slot)));

        /// <summary>Forget all progress, including tutorials and checkpoints stored inside each save.
        /// Only known slot files are removed; preferences and diagnostic files are preserved.</summary>
        public static SaveResult DeleteAll()
        {
            try
            {
                for (int slot = 1; slot <= SlotCount; slot++)
                {
                    File.Delete(PathOf(slot));
                    File.Delete(PathOf(slot) + ".tmp");
                }
                return new SaveResult(true, null);
            }
            catch (DirectoryNotFoundException)
            {
                // A first launch has no saves directory yet: progress is already clear.
                return new SaveResult(true, null);
            }
            catch (Exception e)
            {
                return new SaveResult(false, e.Message);
            }
        }

        public static SlotSummary TryReadSummary(int slot)
        {
            try { return Exists(slot) ? Read(slot).Summary : null; }
            catch { return null; }
        }
    }
}
