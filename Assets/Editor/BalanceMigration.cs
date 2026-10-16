using System.IO;
using Hollow.Data;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>
    /// When the profile schema gains fields, adds them to Baseline.json with their seed values while keeping
    /// every value already in the file (designer edits are never overwritten).
    /// </summary>
    public static class BalanceMigration
    {
        [MenuItem("Hollow/Balance/Add New Fields From Seed (keeps edits)")]
        public static void AddMissingFieldsMenu()
        {
            int added = AddMissingFields();
            Debug.Log($"[Hollow] Balance migration added {added} missing field(s).");
        }

        public static int AddMissingFields()
        {
            var seed = JToken.Parse(BalanceJson.ToJson(BaselineSeed.Create()));
            var existing = JToken.Parse(File.ReadAllText(BaselineSeed.BaselinePath));
            int added = Merge(seed, existing);
            if (added > 0)
            {
                // Round-trip through the typed model to normalise formatting and validate.
                var profile = BalanceJson.FromJson(existing.ToString());
                File.WriteAllText(BaselineSeed.BaselinePath, BalanceJson.ToJson(profile));
                AssetDatabase.ImportAsset(BaselineSeed.BaselinePath);
            }
            return added;
        }

        /// <summary>Copies properties present in <paramref name="seed"/> but missing in <paramref name="target"/>.</summary>
        public static int Merge(JToken seed, JToken target)
        {
            int added = 0;
            if (seed is JObject so && target is JObject to)
            {
                foreach (var prop in so.Properties())
                {
                    if (!to.TryGetValue(prop.Name, out var existing)) { to[prop.Name] = prop.Value.DeepClone(); added++; }
                    else added += Merge(prop.Value, existing);
                }
            }
            else if (seed is JArray sa && target is JArray ta && sa.Count == ta.Count)
            {
                for (int i = 0; i < sa.Count; i++) added += Merge(sa[i], ta[i]);
            }
            return added;
        }
    }
}
