namespace Hollow.Data
{
    // Stable identifiers. They serialise by name, so never rename a member without a data migration.

    public enum EnemyId
    {
        A1_RunnerWolf, A2_QuillShooter, A3_ChargingBoar, A4_ArmoredTortoise, A5_HowlingWolf, A6_ExplosiveBeetle, A7_MotherSpider,
        R1_Crawler, R2_Shooter, R3_SawCharger, R4_ShieldRobot, R5_Repair, R6_Suicide, R7_DroneCarrier,
        H1_BatonGuard, H2_RifleGuard, H3_FastAssaulter, H4_ShieldGuard, H5_Medic, H6_Grenadier, H7_Engineer,
        A7c_Spiderling, R7c_Drone, H7c_Turret
    }

    public enum Faction { Animals, Robots, Humans }

    /// <summary>Behaviour family; decides which AI and which spawn caps apply.</summary>
    public enum EnemyRole { Melee, Ranged, Support, Producer, Explosive, Charger }

    public enum BossId { LargeBeast, IndustrialRobot, FormerCompanion }

    /// <summary>
    /// One boss attack (D86). Strike is only used when the player is close; otherwise the boss closes in with
    /// a pounce (or a charge if it has no pounce). Leap = off the map, the landing zone tracks the player, then a slam.
    /// </summary>
    public enum BossMove { Charge, Strike, Burst, Blast, Pounce, Leap }

    /// <summary>The playable AI (v0.19 §3). None = no character chosen (tests and tools only).</summary>
    public enum AiId { None, Gpt, Claude, Grok, DeepSeek }

    public enum WeaponId
    {
        Pistol, LightSmg, DoubleBarrel, Baton,
        IndustrialRifle, MagazineShotgun, IndustrialHammer,
        EnergyRifle, EnergyShotgun, EnergyBlade
    }

    public enum WeaponKind { Gun, Melee }

    public enum WeaponFamily { Sidearm, Direct, Spread, Melee }

    /// <summary>PushPulse was removed from the game; the value stays so saved item numbers keep their meaning.</summary>
    public enum ItemId { Grenade, PushPulse, Mine, EmpPulse, PersonalShield }

    public enum SkillId { WeaponDamage, MoveSpeed, ReloadMultiplier, DashCooldown, DashDamage, MaxHealth, MagnetRadius, DashMine }
}
