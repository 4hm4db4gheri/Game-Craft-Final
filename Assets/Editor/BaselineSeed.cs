using System.Collections.Generic;
using System.IO;
using Hollow.Data;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>
    /// DATA SEED, not gameplay code: writes the first version of the balance profile from the GDD v0.12 tables
    /// (provisional values) plus programmer placeholders where the GDD has no number (D24, D21).
    /// After seeding, Assets/Resources/Balance/Baseline.json is the single source of truth and is edited
    /// with the balance panel; this seed only runs again through an explicit, confirmed menu command.
    /// </summary>
    public static class BaselineSeed
    {
        public const string BaselinePath = "Assets/Resources/Balance/Baseline.json";
        public const string CsvTemplatePath = "docs/WeaponLevels_Template.csv";

        [MenuItem("Hollow/Balance/Write Baseline From GDD Seed (overwrites)")]
        static void WriteWithConfirm()
        {
            if (File.Exists(BaselinePath) && !EditorUtility.DisplayDialog("Overwrite baseline?",
                    "This replaces Baseline.json with the GDD seed and loses every edit made in the panel.", "Overwrite", "Cancel"))
                return;
            Write();
        }

        public static void Write()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath)!);
            var profile = Create();
            File.WriteAllText(BaselinePath, BalanceJson.ToJson(profile));
            File.WriteAllText(CsvTemplatePath, WeaponLevelCsv.Export(profile));
            AssetDatabase.ImportAsset(BaselinePath);
            Debug.Log($"[Hollow] Wrote {BaselinePath} and {CsvTemplatePath}");
        }

        public static BalanceProfile Create()
        {
            var p = new BalanceProfile { Schema = BalanceProfile.CurrentSchema, Revision = 1, Name = "Baseline (GDD v0.12)" };

            // §4 player. Acceleration/deceleration: placeholders giving ~0.06 s to full speed and ~0.05 s to stop.
            p.Player = new PlayerData
            {
                MaxHealth = 100, MoveSpeed = 5, Acceleration = 80, Deceleration = 100,
                DashDistance = 3, DashDuration = 0.18f, DashCooldown = 2.5f, DashDamage = 50,
                PostHitInvulnerability = 0.5f, BodyRadius = 0.35f, MagnetRadius = 1,
                CoinPullSpeed = 9, ShopInteractRange = 2, CrowdPushShare = 0.3f,
            };

            p.Companion = new CompanionData
            {
                Phases = new List<CompanionPhase>
                {
                    new() { Phase = 1, MaxHealth = 140, ShotDamage = 8, ShotInterval = 1.2f },
                    new() { Phase = 2, MaxHealth = 100, ShotDamage = 6, ShotInterval = 1.6f },
                },
                AttackerCap = 3, FireRange = 8, ProjectileSpeed = 22, MoveSpeed = 4.6f, BodyRadius = 0.4f,
                FollowDistanceMin = 2.5f, FollowDistanceMax = 4, OpportunisticRange = 3, WarningSoundCooldown = 2.5f,
                ProjectileRange = 10, FollowSideBias = 0.6f, FollowArriveRadius = 0.5f, PathClearance = 0.3f, DangerIndicatorTime = 1.5f, CatchUpSpeedFactor = 1.15f,
            };

            p.Enemies = Enemies();

            p.Spawning = new SpawnRules
            {
                BatchShares = new List<float> { 0.3f, 0.3f, 0.4f }, NextBatchAtCapFraction = 0.5f, SupportCap = 2,
                SpawnWarning = 1, NewTypeReadTime = 4, SupportExplosiveSeparation = 8,
                MinDistancePlayerBodyWidths = 4, MinDistanceCompanionBodyWidths = 3,
                SafeIntervalSeconds = 8, NoSafePointRetryDelay = 0.25f, TutorialFirstGroupCount = 4,
            };

            p.Waves = Waves();
            p.Bosses = Bosses();

            p.TestProfiles = new List<TestProfile>
            {
                new() { Name = "Quiet", CountMultiplier = 0.75f, ActiveCapMultiplier = 0.8f, SpawnGapMultiplier = 1.2f },
                new() { Name = "Current baseline", CountMultiplier = 1, ActiveCapMultiplier = 1, SpawnGapMultiplier = 1 },
                new() { Name = "Intense", CountMultiplier = 1.25f, ActiveCapMultiplier = 1.15f, SpawnGapMultiplier = 0.85f },
            };
            p.ActiveTestProfile = "Current baseline";

            p.Combat = new CombatRules
            {
                FrontalArmorReduction = 0.7f, FrontalArmorArc = 120, ReloadFloor = 0.45f, WeaponSwapTime = 0.15f,
                KnockbackDuration = 0.12f, DevBuildsSellUnapprovedLevels = true,
                EnemyMeleeHitHalfAngle = 60, EnemyMeleeHitTolerance = 0.15f, EnemyProjectileRadius = 0.12f,
                PlayerProjectileRadius = 0.1f, RangedHoldFraction = 0.85f,
            };
            p.Weapons = Weapons();

            p.Economy = new EconomyData
            {
                TutorialWeapon = WeaponId.LightSmg, TutorialWeaponPrice = 8,
                PlayerHealPrice = 35, PlayerHealAmount = 40, PlayerHealsPerEncounter = 2,
                CompanionHealPrice = 30, CompanionHealAmount = 40, CompanionHealsPerEncounter = 1,
                CoinLifetimeEnabled = true, CoinLifetime = 60, CoinBlinkTime = 5, CoinScatterRadius = 0.6f,
                ItemUseGap = 0.5f, BossDeathCollectsGroundCoins = true,
            };
            p.Items = Items();

            p.Xp = new XpData { FirstThreshold = 40, ThresholdIncrement = 30, ChoicesOffered = 3 };
            p.Skills = new List<SkillData>
            {
                Skill(SkillId.WeaponDamage, "Weapon damage", 1.08f, 1.16f, 1.24f),
                Skill(SkillId.MoveSpeed, "Movement speed", 5.3f, 5.6f, 5.9f),
                Skill(SkillId.ReloadMultiplier, "Reload time", 0.92f, 0.84f, 0.76f),
                Skill(SkillId.DashCooldown, "Dash cooldown", 2.25f, 2f, 1.75f),
                Skill(SkillId.DashDamage, "Dash damage", 60, 70, 80),
                Skill(SkillId.MaxHealth, "Maximum health", 120, 140, 160),
                Skill(SkillId.MagnetRadius, "Magnet radius", 1.3f, 1.6f, 2f),
            };

            p.Feedback = new FeedbackData
            {
                ShakeOnShot = 0.04f, ShakeOnPlayerHit = 0.25f, ShakeOnExplosion = 0.35f, ShakeOnBossImpact = 0.5f,
                HitFlashDuration = 0.08f, HitKnockback = 0.12f, DamageNumbersDefaultOn = true, DamageNumbersMaxPerSecond = 40,
                MaxParticles = 1500, MaxDecals = 300, MaxCorpses = 150,
            };

            p.Camera = new CameraData { OrthographicSize = 7.5f, LookAheadFactor = 0.15f, LookAheadMax = 3f, FollowDamping = 0.12f, ViewAspect = 16f / 9f, ViewMargin = 0.75f };
            p.Atmosphere = new AtmosphereData
            {
                LightPhase1 = 1f, LightPhase2 = 0.82f, LightPhase3 = 0.6f, PlayerLightRadius = 8f, PlayerLightIntensity = 0.7f,
                FogPhase1 = 0.10f, FogPhase2 = 0.08f, FogPhase3 = 0.12f, PhaseFadeTime = 1.5f, EndingFadeTime = 4f,
            };

            p.BuildIndex();
            return p;
        }

        static SkillData Skill(SkillId id, string name, params float[] ranks) =>
            new() { Id = id, DisplayName = name, Ranks = new List<float>(ranks) };

        // ---------------- Enemies (§5 health/damage/XP/coins; everything else placeholder) ----------------

        static List<EnemyData> Enemies()
        {
            var list = new List<EnemyData>();

            // Animals
            list.Add(Melee(EnemyId.A1_RunnerWolf, Faction.Animals, 30, 8, 3, 2, speed: 3.6f, radius: 0.35f, range: 0.9f, windup: 0.35f, recovery: 0.5f, cooldown: 1.0f, companion: true));
            list.Add(Ranged(EnemyId.A2_QuillShooter, Faction.Animals, 36, 7, 4, 3, speed: 2.6f, radius: 0.35f, range: 7f, windup: 0.6f, recovery: 0.6f, cooldown: 2.2f, projSpeed: 9f, burst: 1, burstGap: 0f));
            list.Add(Charger(EnemyId.A3_ChargingBoar, Faction.Animals, 75, 14, 5, 4, speed: 2.8f, radius: 0.55f, trigger: 6f, telegraph: 0.8f, chargeSpeed: 11f, chargeDist: 7f, recovery: 1.0f, cooldown: 3f));
            var tortoise = Melee(EnemyId.A4_ArmoredTortoise, Faction.Animals, 100, 10, 6, 5, speed: 1.8f, radius: 0.6f, range: 1.1f, windup: 0.6f, recovery: 0.8f, cooldown: 1.6f, companion: true);
            tortoise.FrontalArmor = true; tortoise.KnockbackResistance = 0.6f; tortoise.TurnRateDeg = 90; list.Add(tortoise);
            var howler = Base(EnemyId.A5_HowlingWolf, Faction.Animals, EnemyRole.Support, 45, 0, 5, 4, speed: 3.0f, radius: 0.35f);
            howler.AttackWindup = 0.8f; howler.AttackCooldown = 4f; howler.AuraRadius = 4f; howler.AuraSpeedBonus = 0.15f; howler.AuraDuration = 4.5f;
            howler.CountsAsSupport = true; list.Add(howler);
            list.Add(Exploder(EnemyId.A6_ExplosiveBeetle, Faction.Animals, 25, 18, 4, 3, speed: 3.2f, radius: 0.3f, trigger: 1.4f, fuse: 1.0f, blast: 1.8f));
            var spider = Melee(EnemyId.A7_MotherSpider, Faction.Animals, 110, 10, 10, 8, speed: 1.9f, radius: 0.55f, range: 1.1f, windup: 0.5f, recovery: 0.7f, cooldown: 1.8f, companion: false);
            Producer(spider, EnemyId.A7c_Spiderling, max: 2, delay: 4, xpShare: 2, coinShare: 1); list.Add(spider);
            var spiderling = Melee(EnemyId.A7c_Spiderling, Faction.Animals, 14, 4, 0, 0, speed: 3.4f, radius: 0.22f, range: 0.7f, windup: 0.3f, recovery: 0.5f, cooldown: 1.0f, companion: true);
            spiderling.IsChild = true; list.Add(spiderling);

            // Robots
            list.Add(Machine(Melee(EnemyId.R1_Crawler, Faction.Robots, 55, 12, 4, 3, speed: 3.0f, radius: 0.4f, range: 1.0f, windup: 0.4f, recovery: 0.6f, cooldown: 1.1f, companion: true)));
            list.Add(Machine(Ranged(EnemyId.R2_Shooter, Faction.Robots, 65, 10, 5, 4, speed: 2.2f, radius: 0.4f, range: 8f, windup: 0.7f, recovery: 0.9f, cooldown: 2.4f, projSpeed: 11f, burst: 1, burstGap: 0f)));
            list.Add(Machine(Charger(EnemyId.R3_SawCharger, Faction.Robots, 105, 18, 6, 5, speed: 3.0f, radius: 0.45f, trigger: 6f, telegraph: 0.7f, chargeSpeed: 12f, chargeDist: 7f, recovery: 1.0f, cooldown: 3f)));
            var shieldBot = Machine(Melee(EnemyId.R4_ShieldRobot, Faction.Robots, 150, 14, 7, 6, speed: 1.9f, radius: 0.6f, range: 1.2f, windup: 0.6f, recovery: 0.8f, cooldown: 1.6f, companion: true));
            shieldBot.FrontalArmor = true; shieldBot.KnockbackResistance = 0.6f; shieldBot.TurnRateDeg = 100; list.Add(shieldBot);
            var repair = Machine(Base(EnemyId.R5_Repair, Faction.Robots, EnemyRole.Support, 75, 0, 6, 5, speed: 2.6f, radius: 0.4f));
            repair.HealAmount = 12; repair.HealInterval = 2f; repair.AuraRadius = 4f; repair.HealTotalCap = 60; repair.AttackWindup = 0.5f;
            repair.CountsAsSupport = true; list.Add(repair);
            list.Add(Machine(Exploder(EnemyId.R6_Suicide, Faction.Robots, 40, 24, 5, 4, speed: 3.4f, radius: 0.35f, trigger: 1.4f, fuse: 1.5f, blast: 2.0f)));
            var carrier = Machine(Melee(EnemyId.R7_DroneCarrier, Faction.Robots, 180, 12, 12, 10, speed: 1.7f, radius: 0.65f, range: 1.3f, windup: 0.6f, recovery: 0.9f, cooldown: 2.0f, companion: false));
            carrier.KnockbackResistance = 0.7f;
            Producer(carrier, EnemyId.R7c_Drone, max: 3, delay: 5, xpShare: 2, coinShare: 2); list.Add(carrier);
            var drone = Machine(Ranged(EnemyId.R7c_Drone, Faction.Robots, 20, 5, 0, 0, speed: 3.6f, radius: 0.25f, range: 5f, windup: 0.5f, recovery: 0.6f, cooldown: 2.0f, projSpeed: 10f, burst: 1, burstGap: 0f));
            drone.IsChild = true; list.Add(drone);

            // Humans
            list.Add(Melee(EnemyId.H1_BatonGuard, Faction.Humans, 85, 14, 5, 4, speed: 3.1f, radius: 0.35f, range: 1.1f, windup: 0.45f, recovery: 0.5f, cooldown: 1.1f, companion: true));
            list.Add(Ranged(EnemyId.H2_RifleGuard, Faction.Humans, 90, 12, 6, 5, speed: 2.6f, radius: 0.35f, range: 9f, windup: 0.6f, recovery: 1.0f, cooldown: 2.6f, projSpeed: 14f, burst: 3, burstGap: 0.12f));
            list.Add(Charger(EnemyId.H3_FastAssaulter, Faction.Humans, 140, 22, 7, 6, speed: 3.3f, radius: 0.35f, trigger: 5.5f, telegraph: 0.6f, chargeSpeed: 12f, chargeDist: 6f, recovery: 0.8f, cooldown: 2.5f));
            var shieldGuard = Melee(EnemyId.H4_ShieldGuard, Faction.Humans, 200, 16, 8, 7, speed: 2.2f, radius: 0.55f, range: 1.2f, windup: 0.55f, recovery: 0.7f, cooldown: 1.5f, companion: true);
            shieldGuard.FrontalArmor = true; shieldGuard.KnockbackResistance = 0.6f; shieldGuard.TurnRateDeg = 120; list.Add(shieldGuard);
            var medic = Base(EnemyId.H5_Medic, Faction.Humans, EnemyRole.Support, 100, 0, 7, 6, speed: 2.8f, radius: 0.35f);
            medic.HealAmount = 15; medic.HealInterval = 2.5f; medic.AuraRadius = 4.5f; medic.HealTotalCap = 75; medic.AttackWindup = 0.5f;
            medic.CountsAsSupport = true; list.Add(medic);
            var grenadier = Ranged(EnemyId.H6_Grenadier, Faction.Humans, 110, 20, 6, 5, speed: 2.4f, radius: 0.35f, range: 8f, windup: 0.8f, recovery: 1.0f, cooldown: 3.2f, projSpeed: 9f, burst: 1, burstGap: 0f);
            grenadier.FuseTime = 1.0f; grenadier.ExplosionRadius = 1.8f; list.Add(grenadier);
            var engineer = Base(EnemyId.H7_Engineer, Faction.Humans, EnemyRole.Producer, 220, 0, 14, 12, speed: 2.3f, radius: 0.45f);
            Producer(engineer, EnemyId.H7c_Turret, max: 1, delay: 3, xpShare: 4, coinShare: 3); list.Add(engineer);
            var turret = Machine(Ranged(EnemyId.H7c_Turret, Faction.Humans, 120, 8, 0, 0, speed: 0f, radius: 0.45f, range: 8.5f, windup: 0.7f, recovery: 1.2f, cooldown: 2.2f, projSpeed: 13f, burst: 3, burstGap: 0.15f));
            turret.IsChild = true; turret.KnockbackResistance = 1f; list.Add(turret);

            return list;
        }

        static EnemyData Base(EnemyId id, Faction f, EnemyRole role, float hp, float dmg, int xp, int coins, float speed, float radius) =>
            new() { Id = id, Faction = f, Role = role, Health = hp, Damage = dmg, Xp = xp, Coins = coins, MoveSpeed = speed, BodyRadius = radius,
                TurnRateDeg = 720, KeepAwayDistance = role == EnemyRole.Support || role == EnemyRole.Producer ? 5f : 0f };

        static EnemyData Melee(EnemyId id, Faction f, float hp, float dmg, int xp, int coins, float speed, float radius,
            float range, float windup, float recovery, float cooldown, bool companion)
        {
            var e = Base(id, f, EnemyRole.Melee, hp, dmg, xp, coins, speed, radius);
            e.AttackRange = range; e.AttackWindup = windup; e.AttackRecovery = recovery; e.AttackCooldown = cooldown;
            e.CanTargetCompanion = companion;
            return e;
        }

        static EnemyData Ranged(EnemyId id, Faction f, float hp, float dmg, int xp, int coins, float speed, float radius,
            float range, float windup, float recovery, float cooldown, float projSpeed, int burst, float burstGap)
        {
            var e = Base(id, f, EnemyRole.Ranged, hp, dmg, xp, coins, speed, radius);
            e.AttackRange = range; e.AttackWindup = windup; e.AttackRecovery = recovery; e.AttackCooldown = cooldown;
            e.ProjectileSpeed = projSpeed; e.ProjectileRange = range + 2f; e.BurstCount = burst; e.BurstInterval = burstGap;
            e.CountsAsRanged = true; e.CanTargetCompanion = true;
            return e;
        }

        static EnemyData Charger(EnemyId id, Faction f, float hp, float dmg, int xp, int coins, float speed, float radius,
            float trigger, float telegraph, float chargeSpeed, float chargeDist, float recovery, float cooldown)
        {
            var e = Base(id, f, EnemyRole.Charger, hp, dmg, xp, coins, speed, radius);
            e.AttackRange = trigger; e.AttackWindup = telegraph; e.ChargeSpeed = chargeSpeed; e.ChargeDistance = chargeDist;
            e.AttackRecovery = recovery; e.AttackCooldown = cooldown; e.KnockbackResistance = 0.4f;
            return e;
        }

        static EnemyData Exploder(EnemyId id, Faction f, float hp, float dmg, int xp, int coins, float speed, float radius,
            float trigger, float fuse, float blast)
        {
            var e = Base(id, f, EnemyRole.Explosive, hp, dmg, xp, coins, speed, radius);
            e.AttackRange = trigger; e.FuseTime = fuse; e.ExplosionRadius = blast; e.AttackWindup = fuse;
            e.CountsAsExplosive = true;
            return e;
        }

        static EnemyData Machine(EnemyData e) { e.IsMachine = true; return e; }

        static void Producer(EnemyData e, EnemyId child, int max, float delay, int xpShare, int coinShare)
        {
            e.Role = EnemyRole.Producer;
            e.KeepAwayDistance = e.Damage > 0 ? 0f : 5f;
            e.HasChild = true; e.Child = child; e.ProduceMax = max; e.ProduceDelay = delay;
            e.ChildXpShare = xpShare; e.ChildCoinShare = coinShare;
        }

        // ---------------- Waves (§6) and completion bonuses (§9) ----------------

        static List<WaveData> Waves()
        {
            var w = new List<WaveData>();
            int[][] bonus = { new[] { 15, 20, 25, 30, 40 }, new[] { 25, 35, 45, 55, 70 }, new[] { 40, 55, 70, 85, 100 } };

            w.Add(Wave(1, 1, 3, 1, 1.5f, bonus, 6, (EnemyId.A1_RunnerWolf, 4), (EnemyId.A2_QuillShooter, 2)));
            w[0].IsTutorial = true;
            w.Add(Wave(1, 2, 10, 3, 0.65f, bonus, 32, (EnemyId.A1_RunnerWolf, 18), (EnemyId.A2_QuillShooter, 8), (EnemyId.A3_ChargingBoar, 6)));
            w.Add(Wave(1, 3, 14, 4, 0.5f, bonus, 44, (EnemyId.A1_RunnerWolf, 22), (EnemyId.A2_QuillShooter, 10), (EnemyId.A3_ChargingBoar, 6), (EnemyId.A4_ArmoredTortoise, 6)));
            w.Add(Wave(1, 4, 18, 5, 0.4f, bonus, 60, (EnemyId.A1_RunnerWolf, 26), (EnemyId.A2_QuillShooter, 12), (EnemyId.A3_ChargingBoar, 8), (EnemyId.A5_HowlingWolf, 4), (EnemyId.A6_ExplosiveBeetle, 10)));
            w.Add(Wave(1, 5, 24, 7, 0.32f, bonus, 96, (EnemyId.A1_RunnerWolf, 30), (EnemyId.A2_QuillShooter, 16), (EnemyId.A3_ChargingBoar, 10), (EnemyId.A4_ArmoredTortoise, 8), (EnemyId.A5_HowlingWolf, 4), (EnemyId.A6_ExplosiveBeetle, 10), (EnemyId.A7_MotherSpider, 6)));

            w.Add(Wave(2, 1, 8, 2, 0.85f, bonus, 18, (EnemyId.R1_Crawler, 12), (EnemyId.R2_Shooter, 6)));
            w.Add(Wave(2, 2, 14, 4, 0.55f, bonus, 44, (EnemyId.R1_Crawler, 22), (EnemyId.R2_Shooter, 12), (EnemyId.R3_SawCharger, 10)));
            w.Add(Wave(2, 3, 20, 6, 0.42f, bonus, 64, (EnemyId.R1_Crawler, 26), (EnemyId.R2_Shooter, 16), (EnemyId.R3_SawCharger, 12), (EnemyId.R4_ShieldRobot, 10)));
            w.Add(Wave(2, 4, 26, 8, 0.32f, bonus, 84, (EnemyId.R1_Crawler, 32), (EnemyId.R2_Shooter, 20), (EnemyId.R3_SawCharger, 14), (EnemyId.R5_Repair, 6), (EnemyId.R6_Suicide, 12)));
            w.Add(Wave(2, 5, 32, 10, 0.25f, bonus, 130, (EnemyId.R1_Crawler, 36), (EnemyId.R2_Shooter, 24), (EnemyId.R3_SawCharger, 16), (EnemyId.R4_ShieldRobot, 12), (EnemyId.R5_Repair, 6), (EnemyId.R6_Suicide, 12), (EnemyId.R7_DroneCarrier, 6)));

            w.Add(Wave(3, 1, 10, 3, 0.75f, bonus, 24, (EnemyId.H1_BatonGuard, 16), (EnemyId.H2_RifleGuard, 8)));
            w.Add(Wave(3, 2, 18, 5, 0.45f, bonus, 62, (EnemyId.H1_BatonGuard, 30), (EnemyId.H2_RifleGuard, 18), (EnemyId.H3_FastAssaulter, 14)));
            w.Add(Wave(3, 3, 26, 7, 0.33f, bonus, 88, (EnemyId.H1_BatonGuard, 36), (EnemyId.H2_RifleGuard, 24), (EnemyId.H3_FastAssaulter, 16), (EnemyId.H4_ShieldGuard, 12)));
            w.Add(Wave(3, 4, 34, 9, 0.25f, bonus, 116, (EnemyId.H1_BatonGuard, 42), (EnemyId.H2_RifleGuard, 28), (EnemyId.H3_FastAssaulter, 20), (EnemyId.H5_Medic, 8), (EnemyId.H6_Grenadier, 18)));
            w.Add(Wave(3, 5, 42, 12, 0.2f, bonus, 164, (EnemyId.H1_BatonGuard, 48), (EnemyId.H2_RifleGuard, 32), (EnemyId.H3_FastAssaulter, 24), (EnemyId.H4_ShieldGuard, 18), (EnemyId.H5_Medic, 8), (EnemyId.H6_Grenadier, 18), (EnemyId.H7_Engineer, 8)));
            return w;
        }

        static WaveData Wave(int phase, int wave, int cap, int ranged, float gap, int[][] bonus, int gddMax, params (EnemyId id, int n)[] comp)
        {
            var w = new WaveData
            {
                Phase = phase, Wave = wave, ActiveCap = cap, RangedCap = ranged, SpawnGap = gap,
                CompletionBonus = bonus[phase - 1][wave - 1], GddMaxTotal = gddMax,
            };
            foreach (var (id, n) in comp) w.Composition.Add(new EnemyCount(id, n));
            return w;
        }

        // ---------------- Bosses (§7) and pools (§9) ----------------

        static List<BossData> Bosses()
        {
            return new List<BossData>
            {
                new()
                {
                    Id = BossId.LargeBeast, Phase = 1, Health = 2200, Damage = 20, ActiveCap = 18, RewardXp = 210, RewardCoins = 180,
                    OpeningAdds = Adds((EnemyId.A1_RunnerWolf, 8), (EnemyId.A2_QuillShooter, 4)),
                    Thresholds = new List<BossThreshold>
                    {
                        new() { HealthFraction = 0.65f, Adds = Adds((EnemyId.A1_RunnerWolf, 6), (EnemyId.A3_ChargingBoar, 2)) },
                        new() { HealthFraction = 0.35f, Adds = Adds((EnemyId.A2_QuillShooter, 4), (EnemyId.A5_HowlingWolf, 2), (EnemyId.A6_ExplosiveBeetle, 4)) },
                    },
                    MoveSpeed = 2.4f, BodyRadius = 1.4f, RestTime = 1.0f, LowHealthFraction = 0.35f, LowHealthRestMultiplier = 0.7f,
                    ChargeTelegraph = 0.8f, ChargeSpeed = 13, ChargeDistance = 10, ChargeRecovery = 1.2f,
                    StrikeWindup = 0.9f, StrikeRadius = 3, StrikeRecovery = 1.0f, AddSpawnGap = 0.5f,
                },
                new()
                {
                    Id = BossId.IndustrialRobot, Phase = 2, Health = 5000, Damage = 28, ActiveCap = 26, RewardXp = 358, RewardCoins = 324, IsMachine = true,
                    OpeningAdds = Adds((EnemyId.R1_Crawler, 6), (EnemyId.R2_Shooter, 4)),
                    Thresholds = new List<BossThreshold>
                    {
                        new() { HealthFraction = 0.7f, Adds = Adds((EnemyId.R2_Shooter, 4), (EnemyId.R3_SawCharger, 4), (EnemyId.R5_Repair, 2)) },
                        new() { HealthFraction = 0.35f, Adds = Adds((EnemyId.R3_SawCharger, 4), (EnemyId.R6_Suicide, 6), (EnemyId.R7_DroneCarrier, 2)) },
                    },
                    MoveSpeed = 1.8f, BodyRadius = 1.6f, RestTime = 1.0f, LowHealthFraction = 0.35f, LowHealthRestMultiplier = 0.75f,
                    BurstWarning = 0.7f, BurstDuration = 1.2f, BurstRecovery = 1.3f, BurstShotInterval = 0.1f, BurstSpread = 30, ProjectileSpeed = 13,
                    BlastWarning = 1.0f, BlastRadius = 3, BlastThrowRange = 9, HealCapFraction = 0.1f,
                    BurstShotDamage = 8, ProjectileRange = 16, AddSpawnGap = 0.45f, EmpAffects = true,
                },
                new()
                {
                    Id = BossId.FormerCompanion, Phase = 3, Health = 7000, Damage = 32, ActiveCap = 34, RewardXp = 528, RewardCoins = 486,
                    OpeningAdds = Adds((EnemyId.H1_BatonGuard, 8), (EnemyId.H2_RifleGuard, 6)),
                    Thresholds = new List<BossThreshold>
                    {
                        new() { HealthFraction = 0.7f, Adds = Adds((EnemyId.H2_RifleGuard, 6), (EnemyId.H5_Medic, 2), (EnemyId.H6_Grenadier, 4)) },
                        new() { HealthFraction = 0.35f, Adds = Adds((EnemyId.H3_FastAssaulter, 6), (EnemyId.H4_ShieldGuard, 6), (EnemyId.H7_Engineer, 2)) },
                    },
                    MoveSpeed = 2.2f, BodyRadius = 0.9f, RestTime = 1.0f, LowHealthFraction = 0.35f, LowHealthRestMultiplier = 0.75f,
                    BurstWarning = 0.7f, BurstDuration = 1.0f, BurstRecovery = 1.2f, BurstShotInterval = 0.12f, BurstSpread = 20, ProjectileSpeed = 14,
                    BlastWarning = 1.0f, BlastRadius = 2.2f, BlastThrowRange = 9, HealCapFraction = 0.1f,
                    BurstShotDamage = 7, ProjectileRange = 15, AddSpawnGap = 0.4f, EmpAffects = true,
                },
            };
        }

        static List<EnemyCount> Adds(params (EnemyId id, int n)[] adds)
        {
            var list = new List<EnemyCount>();
            foreach (var (id, n) in adds) list.Add(new EnemyCount(id, n));
            return list;
        }

        // ---------------- Weapons (§8). Level 1 = GDD; levels 2–3 = placeholders (approved=false, D21) ----------------

        static List<WeaponData> Weapons()
        {
            return new List<WeaponData>
            {
                Gun(WeaponId.Pistol, "Pistol", WeaponFamily.Sidearm, 0, reloadSurvivesDash: true,
                    G(1, true, 0, 18, 3, 1, 2, 8, 0.9f, 14, 1, 30, 1),
                    G(2, false, 25, 22, 3, 1, 2, 10, 0.9f, 14, 1, 30, 1),
                    G(3, false, 50, 26, 3.5f, 1, 2, 12, 0.8f, 14, 1, 30, 1)),
                Gun(WeaponId.LightSmg, "Light SMG", WeaponFamily.Direct, 1, false,
                    G(1, true, 80, 12, 9, 1, 8, 24, 1.2f, 10, 1, 28, 1),
                    G(2, false, 60, 14, 9, 1, 8, 30, 1.2f, 10, 1, 28, 1),
                    G(3, false, 120, 16, 10, 1, 7, 36, 1.2f, 10, 1, 28, 1)),
                Gun(WeaponId.DoubleBarrel, "Double-barrel shotgun", WeaponFamily.Spread, 1, false,
                    G(1, true, 120, 9, 1.5f, 8, 28, 4, 1.6f, 7, 1, 24, 0.5f),
                    G(2, false, 90, 11, 1.5f, 8, 28, 4, 1.4f, 7, 1, 24, 0.5f),
                    G(3, false, 180, 13, 1.7f, 10, 28, 4, 1.4f, 7.5f, 1, 24, 0.5f)),
                Melee(WeaponId.Baton, "Baton", 1,
                    M(1, true, 90, 50, 2.2f, 1.7f, 100, 0.8f),
                    M(2, false, 60, 62, 2.2f, 1.7f, 110, 0.8f),
                    M(3, false, 120, 75, 2.5f, 1.9f, 110, 0.8f)),
                Gun(WeaponId.IndustrialRifle, "Industrial rifle", WeaponFamily.Direct, 2, false,
                    G(1, true, 260, 28, 7, 1, 3, 24, 1.4f, 16, 1, 34, 1),
                    G(2, false, 130, 33, 7, 1, 3, 30, 1.4f, 16, 1, 34, 1),
                    G(3, false, 240, 38, 8, 1, 3, 30, 1.25f, 16, 1, 34, 1)),
                Gun(WeaponId.MagazineShotgun, "Magazine shotgun", WeaponFamily.Spread, 2, false,
                    G(1, true, 320, 18, 1.8f, 7, 26, 8, 1.8f, 8, 1, 24, 0.5f),
                    G(2, false, 160, 21, 1.8f, 7, 26, 10, 1.8f, 8, 1, 24, 0.5f),
                    G(3, false, 280, 24, 2.1f, 8, 26, 10, 1.8f, 8, 1, 24, 0.5f)),
                Melee(WeaponId.IndustrialHammer, "Industrial hammer", 2,
                    M(1, true, 280, 125, 1.2f, 2f, 120, 1.4f),
                    M(2, false, 140, 150, 1.2f, 2f, 120, 1.6f),
                    M(3, false, 250, 180, 1.35f, 2.2f, 120, 1.6f)),
                Gun(WeaponId.EnergyRifle, "Energy rifle", WeaponFamily.Direct, 3, false,
                    G(1, true, 650, 48, 8, 1, 2, 28, 1.5f, 18, 2, 40, 1),
                    G(2, false, 280, 56, 8, 1, 2, 34, 1.5f, 18, 2, 40, 1),
                    G(3, false, 450, 64, 9, 1, 2, 34, 1.5f, 18, 3, 40, 1)),
                Gun(WeaponId.EnergyShotgun, "Energy shotgun", WeaponFamily.Spread, 3, false,
                    G(1, true, 750, 24, 2, 8, 30, 8, 1.8f, 9, 2, 28, 0.5f),
                    G(2, false, 330, 28, 2, 8, 30, 10, 1.8f, 9, 2, 28, 0.5f),
                    G(3, false, 520, 32, 2.3f, 10, 30, 10, 1.8f, 9, 2, 28, 0.5f)),
                Melee(WeaponId.EnergyBlade, "Energy blade", 3,
                    M(1, true, 680, 180, 1.8f, 2.3f, 150, 0.6f),
                    M(2, false, 300, 210, 1.8f, 2.3f, 160, 0.6f),
                    M(3, false, 480, 245, 2f, 2.5f, 160, 0.6f)),
            };
        }

        static WeaponData Gun(WeaponId id, string name, WeaponFamily family, int phase, bool reloadSurvivesDash, params WeaponLevel[] levels) =>
            new() { Id = id, DisplayName = name, Kind = WeaponKind.Gun, Family = family, UnlockPhase = phase, ReloadSurvivesDash = reloadSurvivesDash, Levels = new List<WeaponLevel>(levels) };

        static WeaponData Melee(WeaponId id, string name, int phase, params WeaponLevel[] levels) =>
            new() { Id = id, DisplayName = name, Kind = WeaponKind.Melee, Family = WeaponFamily.Melee, UnlockPhase = phase, Levels = new List<WeaponLevel>(levels), IgnoresFrontalArmor = id == WeaponId.EnergyBlade };

        static WeaponLevel G(int level, bool approved, int cost, float dmg, float rate, int count, float spread, int mag,
            float reload, float range, int targets, float projSpeed, float falloff) =>
            new()
            {
                Level = level, Approved = approved, Cost = cost, Damage = dmg, Rate = rate, ProjectileCount = count, SpreadDeg = spread,
                Magazine = mag, ReloadTime = reload, Range = range, Targets = targets, ProjectileSpeed = projSpeed, FalloffAtMaxRange = falloff,
            };

        static WeaponLevel M(int level, bool approved, int cost, float dmg, float rate, float reach, float arc, float knockback) =>
            new() { Level = level, Approved = approved, Cost = cost, Damage = dmg, Rate = rate, Range = reach, ArcDeg = arc, Knockback = knockback };

        // ---------------- Items (§9) ----------------

        static List<ItemData> Items()
        {
            return new List<ItemData>
            {
                new() { Id = ItemId.Grenade, DisplayName = "Grenade", UnlockPhase = 1, Price = 20, Carry = 3, StockPerEncounter = 3, Damage = 160, Radius = 2.4f, FuseTime = 0.8f, ThrowRange = 9 },
                new() { Id = ItemId.PushPulse, DisplayName = "Push pulse", UnlockPhase = 1, Price = 15, Carry = 2, StockPerEncounter = 2, Radius = 3, Knockback = 2.5f },
                new() { Id = ItemId.Mine, DisplayName = "Mine", UnlockPhase = 2, Price = 30, Carry = 2, StockPerEncounter = 2, Damage = 260, Radius = 2.7f, ArmingTime = 0.6f, TriggerRadius = 1.2f, MaxActivePerPhase = 4 },
                new() { Id = ItemId.EmpPulse, DisplayName = "EMP pulse", UnlockPhase = 2, Price = 45, Carry = 2, StockPerEncounter = 2, Radius = 4, StunRegular = 2, StunBoss = 0.8f, BossResistance = 6 },
                new() { Id = ItemId.PersonalShield, DisplayName = "Personal shield", UnlockPhase = 3, Price = 80, Carry = 1, StockPerEncounter = 1, Duration = 3 },
            };
        }
    }
}
