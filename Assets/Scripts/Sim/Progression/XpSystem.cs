using System;
using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>A rolled XP card: its exact options are saved so loading never rerolls (§12).</summary>
    public sealed class PendingChoice
    {
        public List<SkillId> Options = new();
    }

    /// <summary>
    /// XP thresholds (40, then +30 each), overflow kept, never spent as money; crossing several thresholds
    /// resolves as consecutive choices; three eligible skills, no duplicates, no maxed skills (§9).
    /// </summary>
    public static class XpSystem
    {
        static readonly List<SkillId> Eligible = new(8);

        public static int MaxRank(World w, SkillId id) => w.Profile.Skill(id).Ranks.Count;

        public static bool AllSkillsMaxed(World w)
        {
            foreach (var s in w.Profile.Skills)
                if (w.Player.SkillRank(s.Id) < s.Ranks.Count) return false;
            return true;
        }

        public static int NextThreshold(World w) =>
            BalanceCalculator.ThresholdCost(w.Progress.ChoicesTaken + 1, w.Profile.Xp);

        /// <summary>Rolls the next card when a threshold is reached and no card is waiting. Uses the sim RNG.</summary>
        public static void Step(World w)
        {
            var pr = w.Progress;
            if (pr.Pending != null || pr.ChoicesBlocked || AllSkillsMaxed(w)) return;
            int cost = NextThreshold(w);
            if (pr.XpTowardNext < cost) return;

            Eligible.Clear();
            foreach (var s in w.Profile.Skills)
                if (w.Player.SkillRank(s.Id) < s.Ranks.Count) Eligible.Add(s.Id);

            var card = new PendingChoice();
            int count = Mathf.Min(w.Profile.Xp.ChoicesOffered, Eligible.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = w.Rng.Range(0, Eligible.Count);
                card.Options.Add(Eligible[pick]);
                Eligible.RemoveAt(pick);
            }
            pr.XpTowardNext -= cost;
            pr.Pending = card;
        }

        /// <summary>
        /// v0.19 §3: the chosen AI starts with one rank of its skill. The rank sets the value once (ranks are
        /// absolute, never applied twice) and counts toward the cap, so only ranks two and three remain to earn.
        /// </summary>
        public static void ApplyCharacter(World w, AiId ai)
        {
            w.Player.Ai = ai;
            var c = w.Profile.Character(ai);
            if (ai == AiId.None || c == null) return;
            var p = w.Player;
            p.SkillRanks[(int)c.StartingSkill] = Mathf.Clamp(c.StartingRank, 0, MaxRank(w, c.StartingSkill));
            p.Health = PlayerStats.MaxHealth(w); // a new game starts at full health, whatever the bonus
        }

        /// <summary>Applies the chosen option of the pending card. Returns false if the choice is not on the card.</summary>
        public static bool Choose(World w, SkillId id)
        {
            var pr = w.Progress;
            if (pr.Pending == null || !pr.Pending.Options.Contains(id)) return false;
            ApplyRank(w, id);
            w.Emit(SimEventType.SkillChosen, PlayerState.EntityId, w.Player.Position, default, w.Player.SkillRank(id), (int)id);
            pr.Pending = null;
            pr.ChoicesTaken++;
            pr.ChosenSkills.Add(id);
            Step(w); // another threshold may already be crossed: consecutive choices
            return true;
        }

        static void ApplyRank(World w, SkillId id)
        {
            var p = w.Player;
            float oldMax = PlayerStats.MaxHealth(w);
            p.SkillRanks[(int)id] = Mathf.Min(p.SkillRanks[(int)id] + 1, MaxRank(w, id));
            if (id == SkillId.MaxHealth)
            {
                // Adds the difference to both maximum and current health; never a full heal (§9).
                float gain = PlayerStats.MaxHealth(w) - oldMax;
                p.Health = Mathf.Min(PlayerStats.MaxHealth(w), p.Health + gain);
            }
        }

        /// <summary>Value of a skill at the current rank and after taking one more rank (for the card's before/after).</summary>
        public static (float before, float after) Preview(World w, SkillId id)
        {
            int rank = w.Player.SkillRank(id);
            float baseValue = BaseValue(w, id);
            var ranks = w.Profile.Skill(id).Ranks;
            float before = rank == 0 ? baseValue : ranks[Math.Min(rank, ranks.Count) - 1];
            float after = rank >= ranks.Count ? before : ranks[rank];
            return (before, after);
        }

        public static float BaseValue(World w, SkillId id) => id switch
        {
            SkillId.MoveSpeed => w.Profile.Player.MoveSpeed,
            SkillId.DashCooldown => w.Profile.Player.DashCooldown,
            SkillId.DashDamage => w.Profile.Player.DashDamage,
            SkillId.MaxHealth => w.Profile.Player.MaxHealth,
            SkillId.MagnetRadius => w.Profile.Player.MagnetRadius,
            _ => 1f,
        };
    }
}
