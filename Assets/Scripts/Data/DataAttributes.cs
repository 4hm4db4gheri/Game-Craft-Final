using System;

namespace Hollow.Data
{
    /// <summary>The value comes from a GDD table (provisional baseline, not playtested).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GddAttribute : Attribute
    {
        public readonly string Section;
        public GddAttribute(string section) { Section = section; }
    }

    /// <summary>The GDD has no value; the current number is a programmer placeholder awaiting the designer.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class PlaceholderAttribute : Attribute { }

    /// <summary>Unit shown next to the field in the balance panel.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UnitAttribute : Attribute
    {
        public readonly string Unit;
        public UnitAttribute(string unit) { Unit = unit; }
    }

    /// <summary>Panel group (GDD §13): Player and companion, Waves and bosses, Weapons and levels, Economy and items, Skills and feedback.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class PanelGroupAttribute : Attribute
    {
        public readonly string Group;
        public PanelGroupAttribute(string group) { Group = group; }
    }

    public static class PanelGroups
    {
        public const string PlayerCompanion = "Player and companion";
        public const string WavesBosses = "Waves and bosses";
        public const string Weapons = "Weapons and levels";
        public const string EconomyItems = "Economy and items";
        public const string SkillsFeedback = "Skills and feedback";
        public static readonly string[] All = { PlayerCompanion, WavesBosses, Weapons, EconomyItems, SkillsFeedback };
    }
}
