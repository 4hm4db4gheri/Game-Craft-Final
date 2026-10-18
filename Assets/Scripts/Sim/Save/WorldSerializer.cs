using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Hollow.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// Exact world serialisation (GDD §12): public fields only (computed properties are never written),
    /// floats round-trip exactly, enums by name. Non-saved context (profile, arena grid, events) is rebound on load.
    /// </summary>
    public static class WorldSerializer
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new FieldsOnlyResolver(),
            Converters = { new StringEnumConverter(), new Vector2Converter() },
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            FloatFormatHandling = FloatFormatHandling.String,
            Formatting = Formatting.None,
        };

        public static string ToJson(World w) => JsonConvert.SerializeObject(w, Settings);

        public static World FromJson(string json, BalanceProfile profile, ArenaLayout layout)
        {
            var w = JsonConvert.DeserializeObject<World>(json, Settings) ?? throw new FormatException("Empty world data.");
            if (w.Player.SkillRanks == null) w.Player.SkillRanks = new int[Enum.GetValues(typeof(SkillId)).Length];
            else if (w.Player.SkillRanks.Length < Enum.GetValues(typeof(SkillId)).Length)
                Array.Resize(ref w.Player.SkillRanks, Enum.GetValues(typeof(SkillId)).Length);
            var loadout = w.Player.Loadout;
            if (loadout.Reloading && !loadout.ReloadByRound && loadout.Active != null)
            {
                loadout.ReloadStartAmmo = loadout.Active.Ammo; // saves written before round-by-round reload
                loadout.ReloadByRound = true;
            }
            // Shop stock is kept per item in profile order; a save from before an item was removed (the push pulse)
            // has one entry too many, so drop the removed item's entry to keep the rest aligned.
            var stock = w.Shop.ItemStockLeft;
            if (stock.Count == profile.Items.Count + 1 && !profile.HasItem(ItemId.PushPulse)) stock.RemoveAt((int)ItemId.PushPulse);
            w.Bind(profile, layout);
            return w;
        }

        /// <summary>Hash of the complete saved state; equal hashes mean identical games.</summary>
        public static string Hash(World w)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(ToJson(w)));
            var sb = new StringBuilder(32);
            for (int i = 0; i < 16; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }

        sealed class FieldsOnlyResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var props = new List<JsonProperty>();
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (f.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
                    var p = CreateProperty(f, memberSerialization);
                    p.Readable = true;
                    p.Writable = true;
                    props.Add(p);
                }
                return props;
            }
        }

        sealed class Vector2Converter : JsonConverter<Vector2>
        {
            public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
            {
                writer.WriteStartArray();
                writer.WriteValue(value.x);
                writer.WriteValue(value.y);
                writer.WriteEndArray();
            }

            public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var arr = serializer.Deserialize<float[]>(reader);
                return arr != null && arr.Length == 2 ? new Vector2(arr[0], arr[1]) : default;
            }
        }
    }

    /// <summary>
    /// Death checkpoints (§12): captured at each wave start and boss start; restoring keeps pre-wave XP, money,
    /// purchases and equipment and undoes everything from the failed attempt. Player and companion health are
    /// filled to their current maximum so a retry never starts at critically low health (v0.19 §16).
    /// </summary>
    public static class Checkpoints
    {
        public static void Capture(World w)
        {
            w.CheckpointJson = null; // a checkpoint never contains an older checkpoint
            w.CheckpointJson = WorldSerializer.ToJson(w);
        }

        public static World Restore(World w)
        {
            if (string.IsNullOrEmpty(w.CheckpointJson)) throw new InvalidOperationException("No checkpoint captured.");
            var restored = WorldSerializer.FromJson(w.CheckpointJson, w.Profile, w.Layout);
            restored.CheckpointJson = w.CheckpointJson;
            restored.Retries = w.Retries + 1;
            restored.Player.Health = PlayerStats.MaxHealth(restored);
            if (restored.Companion.Present && !restored.Companion.Dead) restored.Companion.Health = restored.Companion.MaxHealth;
            return restored;
        }
    }
}
