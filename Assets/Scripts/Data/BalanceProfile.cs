using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Hollow.Data
{
    /// <summary>
    /// The single source of every gameplay number (GDD §13). Plain data, serialised to JSON.
    /// Code never holds gameplay constants; it reads them from here.
    /// Fields marked [Gdd] come from the GDD tables (provisional); [Placeholder] fields have no GDD value yet.
    /// </summary>
    [Serializable]
    public sealed class BalanceProfile
    {
        public const int CurrentSchema = 1;

        public int Schema;
        public int Revision;
        public string Name;

        [PanelGroup(PanelGroups.PlayerCompanion)] public PlayerData Player = new();
        [PanelGroup(PanelGroups.PlayerCompanion)] public CompanionData Companion = new();
        [PanelGroup(PanelGroups.WavesBosses)] public SpawnRules Spawning = new();
        [PanelGroup(PanelGroups.WavesBosses)] public List<WaveData> Waves = new();
        [PanelGroup(PanelGroups.WavesBosses)] public List<BossData> Bosses = new();
        [PanelGroup(PanelGroups.WavesBosses)] public List<EnemyData> Enemies = new();
        [PanelGroup(PanelGroups.WavesBosses)] public List<TestProfile> TestProfiles = new();
        [PanelGroup(PanelGroups.WavesBosses)] public string ActiveTestProfile;
        [PanelGroup(PanelGroups.Weapons)] public CombatRules Combat = new();
        [PanelGroup(PanelGroups.Weapons)] public List<WeaponData> Weapons = new();
        [PanelGroup(PanelGroups.EconomyItems)] public EconomyData Economy = new();
        [PanelGroup(PanelGroups.EconomyItems)] public List<ItemData> Items = new();
        [PanelGroup(PanelGroups.SkillsFeedback)] public XpData Xp = new();
        [PanelGroup(PanelGroups.SkillsFeedback)] public List<SkillData> Skills = new();
        [PanelGroup(PanelGroups.SkillsFeedback)] public FeedbackData Feedback = new();

        [JsonIgnore, NonSerialized] Dictionary<EnemyId, EnemyData> enemyIndex;
        [JsonIgnore, NonSerialized] Dictionary<WeaponId, WeaponData> weaponIndex;

        public EnemyData Enemy(EnemyId id)
        {
            if (enemyIndex == null || enemyIndex.Count != Enemies.Count) BuildIndex();
            return enemyIndex.TryGetValue(id, out var e) ? e : throw new KeyNotFoundException($"No enemy data for {id}");
        }

        public WeaponData Weapon(WeaponId id)
        {
            if (weaponIndex == null || weaponIndex.Count != Weapons.Count) BuildIndex();
            return weaponIndex.TryGetValue(id, out var w) ? w : throw new KeyNotFoundException($"No weapon data for {id}");
        }

        public ItemData Item(ItemId id) => Items.Find(i => i.Id == id) ?? throw new KeyNotFoundException($"No item data for {id}");
        public SkillData Skill(SkillId id) => Skills.Find(s => s.Id == id) ?? throw new KeyNotFoundException($"No skill data for {id}");
        public BossData Boss(int phase) => Bosses.Find(b => b.Phase == phase) ?? throw new KeyNotFoundException($"No boss for phase {phase}");
        public WaveData Wave(int phase, int wave) => Waves.Find(w => w.Phase == phase && w.Wave == wave) ?? throw new KeyNotFoundException($"No wave {phase}-{wave}");
        public List<WaveData> WavesOfPhase(int phase) => Waves.FindAll(w => w.Phase == phase);
        public CompanionPhase CompanionFor(int phase) => Companion.Phases.Find(p => p.Phase == phase);
        public TestProfile ActiveProfile() => TestProfiles.Find(t => t.Name == ActiveTestProfile);

        /// <summary>Call after replacing lists so lookups see the new data.</summary>
        public void BuildIndex()
        {
            enemyIndex = new Dictionary<EnemyId, EnemyData>();
            foreach (var e in Enemies) enemyIndex[e.Id] = e;
            weaponIndex = new Dictionary<WeaponId, WeaponData>();
            foreach (var w in Weapons) weaponIndex[w.Id] = w;
        }
    }

    [Serializable]
    public sealed class PlayerData
    {
        [Gdd("§4")] public float MaxHealth;
        [Gdd("§4"), Unit("u/s")] public float MoveSpeed;
        [Placeholder, Unit("u/s²")] public float Acceleration;
        [Placeholder, Unit("u/s²")] public float Deceleration;
        [Gdd("§4"), Unit("u")] public float DashDistance;
        [Gdd("§4"), Unit("s")] public float DashDuration;
        [Gdd("§4"), Unit("s")] public float DashCooldown;
        [Gdd("§4")] public float DashDamage;
        [Gdd("§4"), Unit("s")] public float PostHitInvulnerability;
        [Gdd("§4"), Unit("u")] public float BodyRadius;
        [Gdd("§4"), Unit("u")] public float MagnetRadius;
        [Placeholder, Unit("u/s")] public float CoinPullSpeed;
        [Placeholder, Unit("u")] public float ShopInteractRange;
    }

    [Serializable]
    public sealed class CompanionData
    {
        [Gdd("§4")] public List<CompanionPhase> Phases = new();
        [Gdd("§4")] public int AttackerCap;
        [Placeholder, Unit("u")] public float FireRange;
        [Placeholder, Unit("u/s")] public float ProjectileSpeed;
        [Placeholder, Unit("u/s")] public float MoveSpeed;
        [Placeholder, Unit("u")] public float BodyRadius;
        [Placeholder, Unit("u")] public float FollowDistanceMin;
        [Placeholder, Unit("u")] public float FollowDistanceMax;
        [Placeholder, Unit("u")] public float OpportunisticRange;
        [Placeholder, Unit("s")] public float WarningSoundCooldown;
    }

    [Serializable]
    public sealed class CompanionPhase
    {
        public int Phase;
        [Gdd("§4")] public float MaxHealth;
        [Gdd("§4")] public float ShotDamage;
        [Gdd("§4"), Unit("s")] public float ShotInterval;
    }

    [Serializable]
    public sealed class EnemyData
    {
        public EnemyId Id;
        public Faction Faction;
        public EnemyRole Role;
        [Gdd("§5")] public float Health;
        [Gdd("§5")] public float Damage;
        [Gdd("§5")] public int Xp;
        [Gdd("§5")] public int Coins;

        [Placeholder, Unit("u/s")] public float MoveSpeed;
        [Placeholder, Unit("u")] public float BodyRadius;
        [Placeholder, Unit("u")] public float AttackRange;
        [Placeholder, Unit("s")] public float AttackWindup;
        [Placeholder, Unit("s")] public float AttackRecovery;
        [Placeholder, Unit("s")] public float AttackCooldown;
        [Placeholder, Unit("u/s")] public float ProjectileSpeed;
        [Placeholder, Unit("u")] public float ProjectileRange;
        [Placeholder] public int BurstCount;
        [Placeholder, Unit("s")] public float BurstInterval;
        [Placeholder, Unit("u/s")] public float ChargeSpeed;
        [Placeholder, Unit("u")] public float ChargeDistance;
        [Placeholder, Unit("u")] public float ExplosionRadius;
        [Placeholder, Unit("s")] public float FuseTime;
        [Placeholder, Unit("u")] public float AuraRadius;
        [Gdd("§5")] public float AuraSpeedBonus;
        [Placeholder, Unit("s")] public float AuraDuration;
        [Placeholder] public float HealAmount;
        [Placeholder, Unit("s")] public float HealInterval;
        [Placeholder] public float HealTotalCap;
        [Placeholder] public float KnockbackResistance;

        public bool HasChild;
        public EnemyId Child;
        [Gdd("§6")] public int ProduceMax;
        [Gdd("§6"), Unit("s")] public float ProduceDelay;
        [Gdd("§6")] public int ChildXpShare;
        [Gdd("§6")] public int ChildCoinShare;

        [Gdd("§5")] public bool FrontalArmor;
        public bool IsMachine;
        public bool CountsAsRanged;
        public bool CountsAsSupport;
        public bool CountsAsExplosive;
        public bool CanTargetCompanion;
        public bool IsChild;
    }

    [Serializable]
    public struct EnemyCount
    {
        public EnemyId Id;
        public int Count;

        public EnemyCount(EnemyId id, int count) { Id = id; Count = count; }
    }

    [Serializable]
    public sealed class WaveData
    {
        public int Phase;
        public int Wave;
        public bool IsTutorial;
        [Gdd("§6")] public List<EnemyCount> Composition = new();
        [Gdd("§6")] public int ActiveCap;
        [Gdd("§6")] public int RangedCap;
        [Gdd("§6"), Unit("active s")] public float SpawnGap;
        [Gdd("§9")] public int CompletionBonus;
        [Gdd("§6")] public int GddMaxTotal;
    }

    [Serializable]
    public sealed class SpawnRules
    {
        [Gdd("§6")] public List<float> BatchShares = new();
        [Gdd("§6")] public float NextBatchAtCapFraction;
        [Gdd("§6")] public int SupportCap;
        [Gdd("§6"), Unit("s")] public float SpawnWarning;
        [Gdd("§6"), Unit("active s")] public float NewTypeReadTime;
        [Gdd("§6"), Unit("active s")] public float SupportExplosiveSeparation;
        [Gdd("§6")] public float MinDistancePlayerBodyWidths;
        [Gdd("§6")] public float MinDistanceCompanionBodyWidths;
        [Placeholder, Unit("s")] public float SafeIntervalSeconds;
        [Placeholder, Unit("s")] public float NoSafePointRetryDelay;
        [Gdd("§6")] public int TutorialFirstGroupCount;
    }

    [Serializable]
    public sealed class BossData
    {
        public BossId Id;
        public int Phase;
        [Gdd("§7")] public float Health;
        [Gdd("§7")] public float Damage;
        [Gdd("§7")] public List<EnemyCount> OpeningAdds = new();
        [Gdd("§7")] public List<BossThreshold> Thresholds = new();
        [Gdd("§7")] public int ActiveCap;
        [Gdd("§9")] public int RewardXp;
        [Gdd("§9")] public int RewardCoins;
        public bool IsMachine;

        [Placeholder, Unit("u/s")] public float MoveSpeed;
        [Placeholder, Unit("u")] public float BodyRadius;
        [Placeholder, Unit("s")] public float RestTime;
        [Gdd("§7")] public float LowHealthFraction;
        [Placeholder] public float LowHealthRestMultiplier;

        [Gdd("§7"), Unit("s")] public float ChargeTelegraph;
        [Placeholder, Unit("u/s")] public float ChargeSpeed;
        [Placeholder, Unit("u")] public float ChargeDistance;
        [Gdd("§7"), Unit("s")] public float ChargeRecovery;
        [Gdd("§7"), Unit("s")] public float StrikeWindup;
        [Placeholder, Unit("u")] public float StrikeRadius;
        [Placeholder, Unit("s")] public float StrikeRecovery;

        [Gdd("§7"), Unit("s")] public float BurstWarning;
        [Gdd("§7"), Unit("s")] public float BurstDuration;
        [Gdd("§7"), Unit("s")] public float BurstRecovery;
        [Placeholder, Unit("s")] public float BurstShotInterval;
        [Placeholder, Unit("deg")] public float BurstSpread;
        [Placeholder, Unit("u/s")] public float ProjectileSpeed;
        [Gdd("§7"), Unit("s")] public float BlastWarning;
        [Placeholder, Unit("u")] public float BlastRadius;
        [Placeholder, Unit("u")] public float BlastThrowRange;

        [Gdd("§7")] public float HealCapFraction;
    }

    [Serializable]
    public sealed class BossThreshold
    {
        [Gdd("§7")] public float HealthFraction;
        [Gdd("§7")] public List<EnemyCount> Adds = new();
    }

    [Serializable]
    public sealed class CombatRules
    {
        [Gdd("§5")] public float FrontalArmorReduction;
        [Placeholder, Unit("deg")] public float FrontalArmorArc;
        [Gdd("§9"), Unit("s")] public float ReloadFloor;
        [Gdd("§4"), Unit("s")] public float WeaponSwapTime;
        [Placeholder, Unit("s")] public float KnockbackDuration;
        /// <summary>D21: unapproved (placeholder) weapon levels can be bought in editor/dev builds only.</summary>
        public bool DevBuildsSellUnapprovedLevels;
    }

    [Serializable]
    public sealed class WeaponData
    {
        public WeaponId Id;
        public string DisplayName;
        public WeaponKind Kind;
        public WeaponFamily Family;
        [Gdd("§8")] public int UnlockPhase;
        /// <summary>D22: the pistol's reload is not cancelled by dash.</summary>
        public bool ReloadSurvivesDash;
        [Gdd("§8")] public List<WeaponLevel> Levels = new();
    }

    /// <summary>One independent level record (GDD §8): complete statistics plus the cost to reach it.</summary>
    [Serializable]
    public sealed class WeaponLevel
    {
        public int Level;
        public bool Approved;
        [Gdd("§8")] public int Cost;
        [Gdd("§8")] public float Damage;
        [Gdd("§8"), Unit("/s")] public float Rate;
        [Gdd("§8")] public int ProjectileCount;
        [Gdd("§8"), Unit("deg")] public float SpreadDeg;
        [Gdd("§8")] public int Magazine;
        [Gdd("§8"), Unit("s")] public float ReloadTime;
        [Gdd("§8"), Unit("u")] public float Range;
        [Gdd("§8")] public int Targets;
        [Placeholder, Unit("u/s")] public float ProjectileSpeed;
        [Gdd("§8")] public float FalloffAtMaxRange;
        [Gdd("§8"), Unit("deg")] public float ArcDeg;
        [Gdd("§8"), Unit("u")] public float Knockback;
    }

    [Serializable]
    public sealed class EconomyData
    {
        public WeaponId TutorialWeapon;
        [Gdd("§8")] public int TutorialWeaponPrice;
        [Gdd("§9")] public int PlayerHealPrice;
        [Gdd("§9")] public float PlayerHealAmount;
        [Gdd("§9")] public int PlayerHealsPerEncounter;
        [Gdd("§9")] public int CompanionHealPrice;
        [Gdd("§9")] public float CompanionHealAmount;
        [Gdd("§9")] public int CompanionHealsPerEncounter;
        [Gdd("§9")] public bool CoinLifetimeEnabled;
        [Gdd("§9"), Unit("active s")] public float CoinLifetime;
        [Gdd("§9"), Unit("s")] public float CoinBlinkTime;
        [Placeholder, Unit("u")] public float CoinScatterRadius;
        [Gdd("§9"), Unit("s")] public float ItemUseGap;
        /// <summary>D26: at boss death every ground coin flies to the player before the transition.</summary>
        public bool BossDeathCollectsGroundCoins;
    }

    [Serializable]
    public sealed class ItemData
    {
        public ItemId Id;
        public string DisplayName;
        [Gdd("§9")] public int UnlockPhase;
        [Gdd("§9")] public int Price;
        [Gdd("§9")] public int Carry;
        [Gdd("§9")] public int StockPerEncounter;
        [Gdd("§9")] public float Damage;
        [Gdd("§9"), Unit("u")] public float Radius;
        [Gdd("§9"), Unit("s")] public float FuseTime;
        [Gdd("§9"), Unit("u")] public float ThrowRange;
        [Gdd("§9"), Unit("u")] public float Knockback;
        [Gdd("§9"), Unit("s")] public float ArmingTime;
        [Gdd("§9"), Unit("u")] public float TriggerRadius;
        [Gdd("§9")] public int MaxActivePerPhase;
        [Gdd("§9"), Unit("s")] public float StunRegular;
        [Gdd("§9"), Unit("s")] public float StunBoss;
        [Gdd("§9"), Unit("s")] public float BossResistance;
        [Gdd("§9"), Unit("s")] public float Duration;
    }

    [Serializable]
    public sealed class XpData
    {
        [Gdd("§9")] public int FirstThreshold;
        [Gdd("§9")] public int ThresholdIncrement;
        [Gdd("§9")] public int ChoicesOffered;
    }

    /// <summary>Absolute rank values (rank 1..3). The base value is the matching player stat (or 1 for multipliers).</summary>
    [Serializable]
    public sealed class SkillData
    {
        public SkillId Id;
        public string DisplayName;
        [Gdd("§9")] public List<float> Ranks = new();
    }

    [Serializable]
    public sealed class FeedbackData
    {
        [Placeholder] public float ShakeOnShot;
        [Placeholder] public float ShakeOnPlayerHit;
        [Placeholder] public float ShakeOnExplosion;
        [Placeholder] public float ShakeOnBossImpact;
        [Placeholder, Unit("s")] public float HitFlashDuration;
        [Placeholder, Unit("u")] public float HitKnockback;
        [Placeholder] public bool DamageNumbersDefaultOn;
        [Placeholder, Unit("/s")] public int DamageNumbersMaxPerSecond;
        [Placeholder] public int MaxParticles;
        [Placeholder] public int MaxDecals;
        [Placeholder] public int MaxCorpses;
    }

    [Serializable]
    public sealed class TestProfile
    {
        public string Name;
        [Gdd("§13")] public float CountMultiplier;
        [Gdd("§13")] public float ActiveCapMultiplier;
        [Gdd("§13")] public float SpawnGapMultiplier;
    }
}
