using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hollow.Data
{
    /// <summary>
    /// CSV exchange for weapon levels (one row per weapon level, every editable field) so the designer
    /// can fill level 2–3 records in a spreadsheet (D21).
    /// </summary>
    public static class WeaponLevelCsv
    {
        public static readonly string[] Columns =
        {
            "Weapon", "Level", "Approved", "Cost", "Damage", "Rate", "ProjectileCount", "SpreadDeg", "Magazine",
            "ReloadTime", "Range", "Targets", "ProjectileSpeed", "FalloffAtMaxRange", "ArcDeg", "Knockback"
        };

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Export(BalanceProfile p)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Columns));
            foreach (var w in p.Weapons)
            foreach (var l in w.Levels)
            {
                sb.AppendLine(string.Join(",",
                    w.Id, l.Level, l.Approved ? "true" : "false", l.Cost,
                    F(l.Damage), F(l.Rate), l.ProjectileCount, F(l.SpreadDeg), l.Magazine,
                    F(l.ReloadTime), F(l.Range), l.Targets, F(l.ProjectileSpeed), F(l.FalloffAtMaxRange),
                    F(l.ArcDeg), F(l.Knockback)));
            }
            return sb.ToString();
        }

        /// <summary>Applies rows to the profile (adds or replaces levels). Nothing changes if any row is invalid.</summary>
        public static List<string> Import(BalanceProfile p, string csv)
        {
            var errors = new List<string>();
            var parsed = new List<(WeaponData weapon, WeaponLevel level)>();
            var lines = csv.Replace("\r", "").Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != string.Join(",", Columns))
            {
                errors.Add("Header row does not match the template: " + string.Join(",", Columns));
                return errors;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                var c = line.Split(',');
                if (c.Length != Columns.Length) { errors.Add($"Row {i + 1}: expected {Columns.Length} columns, found {c.Length}"); continue; }
                if (!Enum.TryParse(c[0], out WeaponId id)) { errors.Add($"Row {i + 1}: unknown weapon '{c[0]}'"); continue; }
                var weapon = p.Weapons.Find(w => w.Id == id);
                if (weapon == null) { errors.Add($"Row {i + 1}: weapon {id} is not in the profile"); continue; }
                try
                {
                    var l = new WeaponLevel
                    {
                        Level = I(c[1]), Approved = bool.Parse(c[2]), Cost = I(c[3]), Damage = Fl(c[4]), Rate = Fl(c[5]),
                        ProjectileCount = I(c[6]), SpreadDeg = Fl(c[7]), Magazine = I(c[8]), ReloadTime = Fl(c[9]),
                        Range = Fl(c[10]), Targets = I(c[11]), ProjectileSpeed = Fl(c[12]), FalloffAtMaxRange = Fl(c[13]),
                        ArcDeg = Fl(c[14]), Knockback = Fl(c[15]),
                    };
                    if (l.Level < 1) throw new FormatException("level must be >= 1");
                    parsed.Add((weapon, l));
                }
                catch (Exception e) { errors.Add($"Row {i + 1}: {e.Message}"); }
            }

            if (errors.Count > 0) return errors;
            foreach (var (weapon, level) in parsed)
            {
                int idx = weapon.Levels.FindIndex(x => x.Level == level.Level);
                if (idx >= 0) weapon.Levels[idx] = level;
                else weapon.Levels.Add(level);
                weapon.Levels.Sort((a, b) => a.Level.CompareTo(b.Level));
            }
            return errors;
        }

        static string F(float v) => v.ToString("0.###", Inv);
        static float Fl(string s) => float.Parse(s.Trim(), NumberStyles.Float, Inv);
        static int I(string s) => int.Parse(s.Trim(), NumberStyles.Integer, Inv);
    }
}
