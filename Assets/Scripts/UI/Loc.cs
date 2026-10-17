using System.Collections.Generic;
using System.Globalization;
using Hollow.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace Hollow.UI
{
    /// <summary>
    /// Every player-facing string (GDD v0.19 §1, §15: the game is Persian). Texts live in
    /// <c>Resources/Localization/fa.json</c> as flat "key": "text" pairs so the artist and designer can edit
    /// wording without code. Format strings use {0}, {1}… and are formatted with invariant numbers; the RTL
    /// text component turns the digits into Persian digits. A missing key shows as [key] and a test lists
    /// every key the code uses.
    /// </summary>
    public static class Loc
    {
        public const string ResourcePath = "Localization/fa";
        static Dictionary<string, string> table;

        static Dictionary<string, string> Table
        {
            get
            {
                if (table != null) return table;
                var asset = Resources.Load<TextAsset>(ResourcePath);
                table = asset != null ? JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text) : new Dictionary<string, string>();
                return table;
            }
        }

        /// <summary>For tests and the editor: use this table instead of the resource.</summary>
        public static void Override(Dictionary<string, string> strings) => table = strings;

        public static bool Has(string key) => Table.ContainsKey(key);

        public static string T(string key) => Table.TryGetValue(key, out var s) ? s : $"[{key}]";

        public static string F(string key, params object[] args) => PersianSeparators(string.Format(CultureInfo.InvariantCulture, T(key), args));

        /// <summary>Persian decimal separator (٫) between digits and the Persian percent sign (٪) after a digit.</summary>
        public static string PersianSeparators(string s)
        {
            if (s.IndexOf('.') < 0 && s.IndexOf('%') < 0) return s;
            var c = s.ToCharArray();
            for (int i = 1; i < c.Length; i++)
            {
                bool afterDigit = char.IsDigit(c[i - 1]);
                if (c[i] == '.' && afterDigit && i + 1 < c.Length && char.IsDigit(c[i + 1])) c[i] = '٫';
                else if (c[i] == '%' && afterDigit) c[i] = '٪';
            }
            return new string(c);
        }

        public static string Weapon(WeaponId id) => T("weapon." + id);
        public static string Item(ItemId id) => T("item." + id);
        public static string Skill(SkillId id) => T("skill." + id);
        public static string Boss(BossId id) => T("boss." + id);

        /// <summary>Numbers with up to two decimals, invariant (the RTL component localises the digits).</summary>
        public static string N(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
