using System;
using Hollow.Data;
using Newtonsoft.Json;

namespace Hollow.Sim
{
    /// <summary>What the slot picker shows without loading the whole game (§11).</summary>
    public sealed class SlotSummary
    {
        public int Phase;
        public int Wave;
        public string Encounter;
        public double PlaytimeSeconds;
        public string SavedAtUtc;
        public WeaponId Weapon;
        public int WeaponLevel;
        public bool Completed;
        public AiId Ai;
    }

    /// <summary>
    /// One save slot: the exact world (with its death checkpoint inside), the balance-profile snapshot and
    /// version it was played with (later panel edits never alter an old save, §12), and a summary.
    /// </summary>
    public sealed class SaveFile
    {
        public const int CurrentFormat = 1;

        public int Format = CurrentFormat;
        public SlotSummary Summary = new();
        public string ProfileHash;
        public int ProfileRevision;
        public BalanceProfile Profile;
        /// <summary>World JSON (kept as text so the world serialiser's rules apply to it exactly).</summary>
        public string WorldJson;

        public static SaveFile Create(World w, double playtimeSeconds)
        {
            var L = w.Player.Loadout;
            var active = L.Active;
            return new SaveFile
            {
                Summary = new SlotSummary
                {
                    Phase = w.Phase, Wave = w.Waves.Wave, Encounter = w.Waves.Kind.ToString(),
                    PlaytimeSeconds = playtimeSeconds, SavedAtUtc = DateTime.UtcNow.ToString("o"),
                    Weapon = active?.Id ?? WeaponId.Pistol, WeaponLevel = active?.Level ?? 1, Completed = w.Completed, Ai = w.Player.Ai,
                },
                ProfileHash = BalanceJson.Hash(w.Profile),
                ProfileRevision = w.Profile.Revision,
                Profile = BalanceJson.Clone(w.Profile),
                WorldJson = WorldSerializer.ToJson(w),
            };
        }

        public static readonly JsonSerializerSettings Settings = BalanceJson.Settings;

        public string ToJson() => JsonConvert.SerializeObject(this, Settings);

        public static SaveFile FromJson(string json)
        {
            var s = JsonConvert.DeserializeObject<SaveFile>(json, Settings) ?? throw new FormatException("Empty save file.");
            if (s.Format > CurrentFormat) throw new FormatException($"Save format {s.Format} is newer than this build.");
            s.Profile?.BuildIndex();
            return s;
        }

        /// <summary>Rebuilds the world with the save's own balance snapshot.</summary>
        public World LoadWorld(ArenaLayout layout) => WorldSerializer.FromJson(WorldJson, Profile, layout);
    }
}
