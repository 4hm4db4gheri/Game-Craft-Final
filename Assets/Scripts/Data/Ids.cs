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

    public enum WeaponId
    {
        Pistol, LightSmg, DoubleBarrel, Baton,
        IndustrialRifle, MagazineShotgun, IndustrialHammer,
        EnergyRifle, EnergyShotgun, EnergyBlade
    }

    public enum WeaponKind { Gun, Melee }

    public enum WeaponFamily { Sidearm, Direct, Spread, Melee }

    public enum ItemId { Grenade, PushPulse, Mine, EmpPulse, PersonalShield }

    public enum SkillId { WeaponDamage, MoveSpeed, ReloadMultiplier, DashCooldown, DashDamage, MaxHealth, MagnetRadius }
}
