using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Hollow.Data
{
    public enum IssueSeverity { Info, Warning, Error }

    public readonly struct ValidationIssue
    {
        public readonly IssueSeverity Severity;
        public readonly string Path;
        public readonly string Message;

        public ValidationIssue(IssueSeverity severity, string path, string message)
        {
            Severity = severity; Path = path; Message = message;
        }

        public override string ToString() => $"[{Severity}] {Path}: {Message}";
    }

    /// <summary>Checks a profile for incomplete weapon levels, invalid numbers and inconsistent caps (GDD §13).</summary>
    public static class BalanceValidator
    {
        public static List<ValidationIssue> Validate(BalanceProfile p)
        {
            var issues = new List<ValidationIssue>();
            CheckNumbers(p, "Profile", issues, 0);
            CheckEnemies(p, issues);
            CheckWaves(p, issues);
            CheckBosses(p, issues);
            CheckWeapons(p, issues);
            CheckSkills(p, issues);
            CheckSpawnRules(p, issues);
            return issues;
        }

        /// <summary>A level can be sold only when its record is complete; unapproved ones only in dev builds (D21).</summary>
        public static bool IsLevelPurchasable(BalanceProfile p, WeaponData w, WeaponLevel l, bool devBuild) =>
            IsLevelComplete(w, l) && (l.Approved || (devBuild && p.Combat.DevBuildsSellUnapprovedLevels));

        public static bool IsLevelComplete(WeaponData w, WeaponLevel l)
        {
            if (l.Damage <= 0 || l.Rate <= 0 || l.Range <= 0 || l.Cost < 0) return false;
            if (w.Kind == WeaponKind.Melee) return l.ArcDeg > 0 && l.Knockback >= 0;
            return l.ProjectileCount >= 1 && l.Magazine >= 1 && l.ReloadTime > 0 && l.Targets >= 1
                   && l.ProjectileSpeed > 0 && l.SpreadDeg >= 0 && l.FalloffAtMaxRange > 0 && l.FalloffAtMaxRange <= 1;
        }

        static void CheckEnemies(BalanceProfile p, List<ValidationIssue> issues)
        {
            foreach (EnemyId id in Enum.GetValues(typeof(EnemyId)))
                if (!p.Enemies.Exists(e => e.Id == id))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, $"Enemies.{id}", "missing enemy record"));
            foreach (var e in p.Enemies)
            {
                if (e.Health <= 0) issues.Add(new ValidationIssue(IssueSeverity.Error, $"Enemies.{e.Id}.Health", "must be > 0"));
                if (e.BodyRadius <= 0) issues.Add(new ValidationIssue(IssueSeverity.Error, $"Enemies.{e.Id}.BodyRadius", "must be > 0"));
                if (e.HasChild && !p.Enemies.Exists(c => c.Id == e.Child))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, $"Enemies.{e.Id}.Child", $"child {e.Child} has no record"));
                if (e.HasChild && (e.ChildXpShare * e.ProduceMax > e.Xp || e.ChildCoinShare * e.ProduceMax > e.Coins))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, $"Enemies.{e.Id}", "children's shares exceed the producer pool"));
            }
        }

        static void CheckWaves(BalanceProfile p, List<ValidationIssue> issues)
        {
            var totals = BalanceCalculator.Waves(p);
            for (int i = 0; i < p.Waves.Count; i++)
            {
                var w = p.Waves[i];
                string path = $"Waves.P{w.Phase}W{w.Wave}";
                if (w.ActiveCap <= 0) issues.Add(new ValidationIssue(IssueSeverity.Error, path + ".ActiveCap", "must be > 0"));
                if (w.RangedCap > w.ActiveCap) issues.Add(new ValidationIssue(IssueSeverity.Error, path + ".RangedCap", $"ranged cap {w.RangedCap} is greater than the total cap {w.ActiveCap}"));
                if (w.SpawnGap <= 0) issues.Add(new ValidationIssue(IssueSeverity.Error, path + ".SpawnGap", "must be > 0"));
                if (w.GddMaxTotal > 0 && totals[i].MaxTotal != w.GddMaxTotal)
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, path, $"calculated max total {totals[i].MaxTotal} differs from the GDD table ({w.GddMaxTotal})"));
            }
        }

        static void CheckBosses(BalanceProfile p, List<ValidationIssue> issues)
        {
            foreach (var b in p.Bosses)
            {
                float last = 1f;
                foreach (var th in b.Thresholds)
                {
                    if (th.HealthFraction <= 0 || th.HealthFraction >= last)
                        issues.Add(new ValidationIssue(IssueSeverity.Error, $"Bosses.{b.Id}.Thresholds", "fractions must be descending inside (0, 1)"));
                    last = th.HealthFraction;
                }
                var phase = BalanceCalculator.Phases(p).Find(t => t.Phase == b.Phase);
                if (phase != null && (phase.BossOwnXp < 0 || phase.BossOwnCoins < 0))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, $"Bosses.{b.Id}", "reward pool is smaller than the adds' rewards"));
            }
        }

        static void CheckWeapons(BalanceProfile p, List<ValidationIssue> issues)
        {
            foreach (var w in p.Weapons)
            {
                for (int i = 0; i < w.Levels.Count; i++)
                {
                    var l = w.Levels[i];
                    string path = $"Weapons.{w.Id}.L{l.Level}";
                    if (l.Level != i + 1) issues.Add(new ValidationIssue(IssueSeverity.Error, path, $"levels must be numbered 1..n (found {l.Level} at position {i + 1})"));
                    if (!IsLevelComplete(w, l)) issues.Add(new ValidationIssue(IssueSeverity.Error, path, "incomplete record: cannot be purchased"));
                    else if (!l.Approved) issues.Add(new ValidationIssue(IssueSeverity.Warning, path, "placeholder level awaiting designer approval: not purchasable in a release build"));
                }
            }
        }

        static void CheckSkills(BalanceProfile p, List<ValidationIssue> issues)
        {
            foreach (SkillId id in Enum.GetValues(typeof(SkillId)))
            {
                var s = p.Skills.Find(x => x.Id == id);
                if (s == null) issues.Add(new ValidationIssue(IssueSeverity.Error, $"Skills.{id}", "missing skill"));
                else if (s.Ranks.Count == 0) issues.Add(new ValidationIssue(IssueSeverity.Error, $"Skills.{id}", "needs at least one rank"));
            }
        }

        static void CheckSpawnRules(BalanceProfile p, List<ValidationIssue> issues)
        {
            float sum = 0;
            foreach (var s in p.Spawning.BatchShares) sum += s;
            if (Math.Abs(sum - 1f) > 0.001f)
                issues.Add(new ValidationIssue(IssueSeverity.Error, "Spawning.BatchShares", $"shares add up to {sum}, expected 1"));
        }

        /// <summary>Every numeric field anywhere must be a finite, non-negative number.</summary>
        static void CheckNumbers(object node, string path, List<ValidationIssue> issues, int depth)
        {
            if (node == null || depth > 8) return;
            foreach (var f in node.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v = f.GetValue(node);
                string fp = $"{path}.{f.Name}";
                switch (v)
                {
                    case float fl when float.IsNaN(fl) || float.IsInfinity(fl):
                        issues.Add(new ValidationIssue(IssueSeverity.Error, fp, "not a finite number")); break;
                    case float fl when fl < 0:
                        issues.Add(new ValidationIssue(IssueSeverity.Error, fp, "negative value")); break;
                    case int n when n < 0:
                        issues.Add(new ValidationIssue(IssueSeverity.Error, fp, "negative value")); break;
                    case string _:
                        break;
                    case IList list:
                        for (int i = 0; i < list.Count; i++)
                        {
                            object item = list[i];
                            if (item is float lf && (float.IsNaN(lf) || lf < 0))
                                issues.Add(new ValidationIssue(IssueSeverity.Error, $"{fp}[{i}]", "invalid number"));
                            else if (item != null && !item.GetType().IsPrimitive && !item.GetType().IsEnum)
                                CheckNumbers(item, $"{fp}[{i}]", issues, depth + 1);
                        }
                        break;
                    default:
                        if (v != null && f.FieldType.IsClass) CheckNumbers(v, fp, issues, depth + 1);
                        else if (v != null && f.FieldType.IsValueType && !f.FieldType.IsPrimitive && !f.FieldType.IsEnum)
                            CheckNumbers(v, fp, issues, depth + 1);
                        break;
                }
            }
        }
    }
}
