using System.Collections.Generic;
using System.IO;
using Hollow.Data;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>
    /// DATA SEED, not gameplay code: writes the balance profile from the GDD tables (v0.12, updated to v0.19 for
    /// weapon levels, enemy timing and roles, children, bosses and effects) plus programmer placeholders where the
    /// GDD has no number (D24, D21).
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
            var p = new BalanceProfile { Schema = BalanceProfile.CurrentSchema, Revision = 12, Name = "Baseline (GDD v0.19)" };

            // §4 player. Acceleration/deceleration: placeholders giving ~0.06 s to full speed and ~0.05 s to stop.
            p.Player = new PlayerData
            {
                MaxHealth = 100, MoveSpeed = 5, Acceleration = 80, Deceleration = 100,
                DashDistance = 3, DashDuration = 0.18f, DashCooldown = 2.5f, DashDamage = 50, DashMineDelay = 0.7f, DashMineRadius = 1.8f,
                PostHitInvulnerability = 0.5f, BodyRadius = 0.35f, MagnetRadius = 1,
                CoinPullSpeed = 9, ShopInteractRange = 2.2f, CrowdPushShare = 0.3f,
            };

            p.Companion = new CompanionData
            {
                Phases = new List<CompanionPhase>
                {
                    new() { Phase = 1, MaxHealth = 140, ShotDamage = 8, ShotInterval = 1.2f },
                    new() { Phase = 2, MaxHealth = 260, DamageTakenMultiplier = 0.35f, ShotDamage = 6, ShotInterval = 1.6f },
                },
                AttackerCap = 3, FireRange = 8, ProjectileSpeed = 22, MoveSpeed = 4.6f, BodyRadius = 0.4f,
                FollowDistanceMin = 2.5f, FollowDistanceMax = 4, OpportunisticRange = 3, WarningSoundCooldown = 2.5f,
                ProjectileRange = 10, FollowSideBias = 0.6f, FollowArriveRadius = 0.5f, PathClearance = 0.3f, DangerIndicatorTime = 1.5f, CatchUpSpeedFactor = 1.15f,
                RegenerationDelay = 8f, RegenerationRate = 2f,
            };

            // v0.19 §3: each AI starts with one rank of one skill (the start value is that skill's rank-one value).
            p.Characters = new List<CharacterData>
            {
                new() { Id = AiId.Gpt, DisplayName = "GPT", StartingSkill = SkillId.ReloadMultiplier, StartingRank = 1 },
                new() { Id = AiId.Claude, DisplayName = "Claude", StartingSkill = SkillId.MagnetRadius, StartingRank = 1 },
                new() { Id = AiId.Grok, DisplayName = "Grok", StartingSkill = SkillId.DashDamage, StartingRank = 1 },
                new() { Id = AiId.DeepSeek, DisplayName = "DeepSeek", StartingSkill = SkillId.MoveSpeed, StartingRank = 1 },
            };

            p.Enemies = Enemies();

            p.Spawning = new SpawnRules
            {
                BatchShares = new List<float> { 0.3f, 0.3f, 0.4f }, NextBatchAtCapFraction = 0.5f, SupportCap = 2,
                SpawnWarning = 1, NewTypeReadTime = 4, SupportExplosiveSeparation = 8,
                MinDistancePlayerBodyWidths = 4, MinDistanceCompanionBodyWidths = 3,
                SafeIntervalSeconds = 15, NoSafePointRetryDelay = 0.25f, TutorialFirstGroupCount = 4,
                TutorialGrenadeTargetDistance = 3.5f,
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
                GiftWeapon = WeaponId.IndustrialRifle, GiftAfterPhase = 1,
            };
            p.Items = Items();

            p.Xp = new XpData { FirstThreshold = 40, ThresholdIncrement = 30, ChoicesOffered = 3 };
            p.Skills = new List<SkillData>
            {
                Skill(SkillId.WeaponDamage, "Weapon damage", 1.08f, 1.16f, 1.24f),
                // D89: +6/12/18 % speed and -8/16/24 % reload were not felt; now +12/24/36 % and -20/38/55 % (the reload floor still holds).
                Skill(SkillId.MoveSpeed, "Movement speed", 5.6f, 6.2f, 6.8f),
                Skill(SkillId.ReloadMultiplier, "Reload time", 0.8f, 0.62f, 0.45f),
                Skill(SkillId.DashCooldown, "Dash cooldown", 2.25f, 2f, 1.75f),
                Skill(SkillId.DashDamage, "Dash damage", 60, 70, 80),
                Skill(SkillId.MaxHealth, "Maximum health", 120, 140, 160),
                Skill(SkillId.MagnetRadius, "Magnet radius", 1.3f, 1.6f, 2f),
                Skill(SkillId.DashMine, "Dash kill mine", 40f, 65f, 90f),
            };

            p.Feedback = new FeedbackData
            {
                ShakeOnShot = 0.09f, ShakeOnMelee = 0.12f, ShakeOnDash = 0.06f, ShakeOnPlayerDeath = 0.6f, ShakeOnPlayerHit = 0.25f, ShakeOnExplosion = 0.35f, ShakeOnBossImpact = 0.5f,
                HitFlashDuration = 0.08f, HitKnockback = 0.12f, DamageNumbersDefaultOn = true, DamageNumbersMaxPerSecond = 40,
                MaxParticles = 1500, MaxDecals = 20, MaxCorpses = 30,
                ShakeOnBossDefeated = 0.7f, ShotShakeMinInterval = 0.08f, ShakeDuration = 0.18f, MuzzleFlashTime = 0.05f,
                ImpactSparks = 5, DeathSparks = 22, DeathGore = 26, ExplosionSparks = 24, DamageNumberLifetime = 0.65f, MaxDamageNumbersOnScreen = 40,
                HitSquash = 0.14f, BudgetLowScale = 0.3f, BudgetMediumScale = 0.6f,
            };

            p.Camera = new CameraData { OrthographicSize = 7.5f, LookAheadFactor = 0.15f, LookAheadMax = 3f, FollowDamping = 0.12f, ViewAspect = 16f / 9f, ViewMargin = 0.75f };
            p.Atmosphere = new AtmosphereData
            {
                LightPhase1 = 1f, LightPhase2 = 0.82f, LightPhase3 = 0.55f, PlayerLightRadius = 8f, PlayerLightIntensity = 0.75f,
                FogPhase1 = 0.12f, FogPhase2 = 0.10f, FogPhase3 = 0.2f, PhaseFadeTime = 1.5f, EndingFadeTime = 4f,
                LightColorPhase1 = "#FFF3DE", LightColorPhase2 = "#DFECF1", LightColorPhase3 = "#D2DCF0",
                ContrastPhase1 = 8f, ContrastPhase2 = 12f, ContrastPhase3 = 16f,
                DesaturationPhase1 = 0f, DesaturationPhase2 = 6f, DesaturationPhase3 = 14f,
                VignettePhase1 = 0.2f, VignettePhase2 = 0.24f, VignettePhase3 = 0.32f,
                BloomIntensity = 0.45f, BloomThreshold = 0.85f, BloomScatter = 0.7f, GroundVariation = 0.12f,
                ComposedGround = true, ContactShadowStrength = 0.55f,
                FogColorPhase1 = "#D9E4CC", FogColorPhase2 = "#CBD3DA", FogColorPhase3 = "#B9C7DD",
                HighFogPhase1 = 0.03f, HighFogPhase2 = 0.05f, HighFogPhase3 = 0.1f, FogBreath = 0.45f,
                LightPulsePhase1 = 0.04f, LightPulsePhase2 = 0.05f, LightPulsePhase3 = 0.16f, LightPulseSpeed = 0.2f,
                FlickersPerMinutePhase1 = 0f, FlickersPerMinutePhase2 = 2f, FlickersPerMinutePhase3 = 7f, FlickerDepth = 0.55f,
                SearchlightsPhase1 = 0, SearchlightsPhase2 = 0, SearchlightsPhase3 = 2, SearchlightIntensity = 1.3f,
                ShadowTintPhase1 = "#56735E", ShadowTintPhase2 = "#4C6078", ShadowTintPhase3 = "#3F4C7C",
                HighlightTintPhase1 = "#CDAA74", HighlightTintPhase2 = "#C69C6C", HighlightTintPhase3 = "#92B6CC",
                GrainPhase1 = 0.12f, GrainPhase2 = 0.18f, GrainPhase3 = 0.3f, ChromaticAberration = 0.06f,
                HurtPulse = 0.2f, LowHealthFraction = 0.3f,
            };

            p.BuildIndex();
            return p;
        }

        static SkillData Skill(SkillId id, string name, params float[] ranks) =>
            new() { Id = id, DisplayName = name, Ranks = new List<float>(ranks) };

        // ---------------- Enemies (§5 health/damage/coins; v0.19 §9 XP, speed, reach, interval, windup, recovery,
        // shot speed, role parameters and children). Body radius and child recovery remain placeholders. ----------------

        static List<EnemyData> Enemies()
        {
            var list = new List<EnemyData>();

            // Animals
            list.Add(Melee(EnemyId.A1_RunnerWolf, Faction.Animals, 30, 8, 3, 2, speed: 2.6f, radius: 0.35f, range: 1.0f, windup: 0.35f, recovery: 0.35f, interval: 1.0f, companion: true));
            list.Add(Ranged(EnemyId.A2_QuillShooter, Faction.Animals, 36, 7, 4, 3, speed: 1.7f, radius: 0.35f, range: 8f, windup: 0.55f, recovery: 0.45f, interval: 1.8f, projSpeed: 8f, burst: 1, burstGap: 0f));
            list.Add(Charger(EnemyId.A3_ChargingBoar, Faction.Animals, 75, 14, 5, 4, speed: 2.1f, radius: 0.55f, trigger: 9f, telegraph: 0.8f, chargeSpeed: 8f, recovery: 1.2f, interval: 3.2f));
            var tortoise = Melee(EnemyId.A4_ArmoredTortoise, Faction.Animals, 100, 10, 6, 5, speed: 1.3f, radius: 0.6f, range: 1.2f, windup: 0.55f, recovery: 0.65f, interval: 1.5f, companion: true);
            tortoise.FrontalArmor = true; tortoise.KnockbackResistance = 0.6f; tortoise.TurnRateDeg = 90; list.Add(tortoise);
            var howler = Base(EnemyId.A5_HowlingWolf, Faction.Animals, EnemyRole.Support, 45, 0, 5, 4, speed: 1.8f, radius: 0.35f);
            howler.AttackRange = 5f; howler.AttackWindup = 0.7f; howler.AttackRecovery = 0.8f; howler.AttackInterval = 4f;
            howler.AuraRadius = 5f; howler.AuraSpeedBonus = 0.15f; howler.AuraDuration = 3f;
            howler.CountsAsSupport = true; list.Add(howler);
            list.Add(Exploder(EnemyId.A6_ExplosiveBeetle, Faction.Animals, 25, 18, 4, 3, speed: 2.9f, radius: 0.3f, trigger: 1.5f, fuse: 0.8f, blast: 2f));
            var spider = Melee(EnemyId.A7_MotherSpider, Faction.Animals, 110, 10, 10, 8, speed: 1.4f, radius: 0.55f, range: 1.1f, windup: 0.6f, recovery: 0.6f, interval: 2.2f, companion: false);
            Producer(spider, EnemyId.A7c_Spiderling, max: 2, delay: 4, xpShare: 2, coinShare: 1); list.Add(spider);
            var spiderling = Melee(EnemyId.A7c_Spiderling, Faction.Animals, 16, 5, 0, 0, speed: 3.4f, radius: 0.22f, range: 0.9f, windup: 0.3f, recovery: 0.5f, interval: 1.1f, companion: true);
            spiderling.IsChild = true; list.Add(spiderling);

            // Robots
            list.Add(Machine(Melee(EnemyId.R1_Crawler, Faction.Robots, 55, 12, 4, 3, speed: 2.8f, radius: 0.4f, range: 1.1f, windup: 0.35f, recovery: 0.35f, interval: 1f, companion: true)));
            list.Add(Machine(Ranged(EnemyId.R2_Shooter, Faction.Robots, 65, 10, 5, 4, speed: 2f, radius: 0.4f, range: 10f, windup: 0.5f, recovery: 0.5f, interval: 1.6f, projSpeed: 9f, burst: 1, burstGap: 0f)));
            list.Add(Machine(Charger(EnemyId.R3_SawCharger, Faction.Robots, 105, 18, 6, 5, speed: 2.3f, radius: 0.45f, trigger: 10f, telegraph: 0.7f, chargeSpeed: 8f, recovery: 1f, interval: 2.8f)));
            var shieldBot = Machine(Melee(EnemyId.R4_ShieldRobot, Faction.Robots, 150, 14, 7, 6, speed: 1.7f, radius: 0.6f, range: 1.3f, windup: 0.5f, recovery: 0.6f, interval: 1.6f, companion: true));
            shieldBot.FrontalArmor = true; shieldBot.KnockbackResistance = 0.6f; shieldBot.TurnRateDeg = 100; list.Add(shieldBot);
            var repair = Machine(Base(EnemyId.R5_Repair, Faction.Robots, EnemyRole.Support, 75, 0, 6, 5, speed: 1.8f, radius: 0.4f));
            Healer(repair, radius: 5f, interval: 3f, windup: 0.65f, recovery: 0.7f); list.Add(repair);
            list.Add(Machine(Exploder(EnemyId.R6_Suicide, Faction.Robots, 40, 24, 5, 4, speed: 3.1f, radius: 0.35f, trigger: 1.7f, fuse: 0.9f, blast: 2.4f)));
            var carrier = Machine(Melee(EnemyId.R7_DroneCarrier, Faction.Robots, 180, 12, 12, 10, speed: 1.5f, radius: 0.65f, range: 1.2f, windup: 0.65f, recovery: 0.7f, interval: 2f, companion: false));
            carrier.KnockbackResistance = 0.7f;
            Producer(carrier, EnemyId.R7c_Drone, max: 3, delay: 5, xpShare: 2, coinShare: 2); list.Add(carrier);
            // v0.19: the drone is a melee child (shot speed 0).
            var drone = Machine(Melee(EnemyId.R7c_Drone, Faction.Robots, 28, 7, 0, 0, speed: 3.5f, radius: 0.25f, range: 1f, windup: 0.35f, recovery: 0.6f, interval: 1.2f, companion: true));
            drone.IsChild = true; list.Add(drone);

            // Humans
            list.Add(Melee(EnemyId.H1_BatonGuard, Faction.Humans, 85, 14, 6, 4, speed: 3f, radius: 0.35f, range: 1.1f, windup: 0.35f, recovery: 0.35f, interval: 0.95f, companion: true));
            list.Add(Ranged(EnemyId.H2_RifleGuard, Faction.Humans, 90, 12, 6, 5, speed: 2.2f, radius: 0.35f, range: 11f, windup: 0.55f, recovery: 0.75f, interval: 1.8f, projSpeed: 10f, burst: 2, burstGap: 0.18f));
            list.Add(Charger(EnemyId.H3_FastAssaulter, Faction.Humans, 140, 22, 7, 6, speed: 3.4f, radius: 0.35f, trigger: 8f, telegraph: 0.65f, chargeSpeed: 7.5f, recovery: 1f, interval: 2.7f));
            var shieldGuard = Melee(EnemyId.H4_ShieldGuard, Faction.Humans, 200, 16, 8, 7, speed: 1.9f, radius: 0.55f, range: 1.3f, windup: 0.5f, recovery: 0.6f, interval: 1.5f, companion: true);
            shieldGuard.FrontalArmor = true; shieldGuard.KnockbackResistance = 0.6f; shieldGuard.TurnRateDeg = 120; list.Add(shieldGuard);
            var medic = Base(EnemyId.H5_Medic, Faction.Humans, EnemyRole.Support, 100, 0, 7, 6, speed: 2f, radius: 0.35f);
            Healer(medic, radius: 5f, interval: 3.5f, windup: 0.7f, recovery: 0.8f); list.Add(medic);
            // Shot speed is the throw speed of the marked grenade (v0.19 lists 0: an ability); placeholder for the flight.
            var grenadier = Ranged(EnemyId.H6_Grenadier, Faction.Humans, 110, 20, 6, 5, speed: 2.1f, radius: 0.35f, range: 10f, windup: 0.7f, recovery: 0.8f, interval: 3.5f, projSpeed: 9f, burst: 1, burstGap: 0f);
            grenadier.FuseTime = 1.1f; grenadier.ExplosionRadius = 2.4f; list.Add(grenadier);
            var engineer = Base(EnemyId.H7_Engineer, Faction.Humans, EnemyRole.Producer, 220, 0, 14, 12, speed: 1.8f, radius: 0.45f);
            engineer.AttackRange = 1.1f; engineer.AttackWindup = 0.6f; engineer.AttackRecovery = 0.6f; engineer.AttackInterval = 2f;
            Producer(engineer, EnemyId.H7c_Turret, max: 1, delay: 3, xpShare: 4, coinShare: 3); list.Add(engineer);
            var turret = Machine(Ranged(EnemyId.H7c_Turret, Faction.Humans, 70, 8, 0, 0, speed: 0f, radius: 0.45f, range: 9f, windup: 0.45f, recovery: 0.6f, interval: 1.3f, projSpeed: 9f, burst: 1, burstGap: 0f));
            turret.IsChild = true; turret.KnockbackResistance = 1f; list.Add(turret);

            foreach (var e in list) e.HitHeight = HitHeight(BodyTops[e.Id], e.BodyRadius);
            return list;
        }

        /// <summary>
        /// Top of each drawn body above its feet, in units (measured on the imported art; placeholders by cell size).
        /// Shots fly 0.35 above their ground line (ProjectileView), so the hit capsule rises to the top minus that lift
        /// and the body radius.
        /// </summary>
        static readonly Dictionary<EnemyId, float> BodyTops = new()
        {
            [EnemyId.A1_RunnerWolf] = 0.9f, [EnemyId.A2_QuillShooter] = 1f, [EnemyId.A3_ChargingBoar] = 1.3f, [EnemyId.A4_ArmoredTortoise] = 1.2f,
            [EnemyId.A5_HowlingWolf] = 0.95f, [EnemyId.A6_ExplosiveBeetle] = 0.5f, [EnemyId.A7_MotherSpider] = 1.3f, [EnemyId.A7c_Spiderling] = 0.4f,
            [EnemyId.R1_Crawler] = 0.9f, [EnemyId.R2_Shooter] = 1.5f, [EnemyId.R3_SawCharger] = 1.2f, [EnemyId.R4_ShieldRobot] = 2f,
            [EnemyId.R5_Repair] = 1.3f, [EnemyId.R6_Suicide] = 0.6f, [EnemyId.R7_DroneCarrier] = 1.9f, [EnemyId.R7c_Drone] = 0.7f,
            [EnemyId.H1_BatonGuard] = 1.75f, [EnemyId.H2_RifleGuard] = 1.75f, [EnemyId.H3_FastAssaulter] = 1.75f, [EnemyId.H4_ShieldGuard] = 1.75f,
            [EnemyId.H5_Medic] = 1.75f, [EnemyId.H6_Grenadier] = 1.75f, [EnemyId.H7_Engineer] = 1.75f, [EnemyId.H7c_Turret] = 1f,
        };
        const float ShotLift = 0.35f;

        static float HitHeight(float bodyTop, float radius) => Mathf.Max(0f, Mathf.Round((bodyTop - ShotLift - radius) * 100f) / 100f);

        static EnemyData Base(EnemyId id, Faction f, EnemyRole role, float hp, float dmg, int xp, int coins, float speed, float radius) =>
            new() { Id = id, Faction = f, Role = role, Health = hp, Damage = dmg, Xp = xp, Coins = coins, MoveSpeed = speed, BodyRadius = radius,
                TurnRateDeg = 720, KeepAwayDistance = role == EnemyRole.Support || role == EnemyRole.Producer ? 5f : 0f };

        static EnemyData Melee(EnemyId id, Faction f, float hp, float dmg, int xp, int coins, float speed, float radius,
            float range, float windup, float recovery, float interval, bool companion)
        {
            var e = Base(id, f, EnemyRole.Melee, hp, dmg, xp, coins, speed, radius);
            e.AttackRange = range; e.AttackWindup = windup; e.AttackRecovery = recovery; e.AttackInterval = interval;
            e.CanTargetCompanion = companion;
            return e;
        }

        static EnemyData Ranged(EnemyId id, Faction f, float hp, float dmg, int xp, int coins, float speed, float radius,
            float range, float windup, float recovery, float interval, float projSpeed, int burst, float burstGap)
        {
            var e = Base(id, f, EnemyRole.Ranged, hp, dmg, xp, coins, speed, radius);
            e.AttackRange = range; e.AttackWindup = windup; e.AttackRecovery = recovery; e.AttackInterval = interval;
            e.ProjectileSpeed = projSpeed; e.ProjectileRange = range + 2f; e.BurstCount = burst; e.BurstInterval = burstGap;
            e.CountsAsRanged = true; e.CanTargetCompanion = true;
            return e;
        }

        static EnemyData Charger(EnemyId id, Faction f, float hp, float dmg, int xp, int coins, float speed, float radius,
            float trigger, float telegraph, float chargeSpeed, float recovery, float interval)
        {
            var e = Base(id, f, EnemyRole.Charger, hp, dmg, xp, coins, speed, radius);
            e.AttackRange = trigger; e.AttackWindup = telegraph; e.ChargeSpeed = chargeSpeed;
            e.AttackRecovery = recovery; e.AttackInterval = interval; e.KnockbackResistance = 0.4f;
            return e;
        }

        static EnemyData Exploder(EnemyId id, Faction f, float hp, float dmg, int xp, int coins, float speed, float radius,
            float trigger, float fuse, float blast)
        {
            var e = Base(id, f, EnemyRole.Explosive, hp, dmg, xp, coins, speed, radius);
            e.AttackRange = trigger; e.FuseTime = fuse; e.ExplosionRadius = blast; e.AttackWindup = fuse; e.AttackInterval = 1f;
            e.CountsAsExplosive = true;
            return e;
        }

        /// <summary>v0.19 §9 repair/medic: radius 5, 8% of the target's maximum per use, 25% budget per target.</summary>
        static void Healer(EnemyData e, float radius, float interval, float windup, float recovery)
        {
            e.AttackRange = radius; e.AuraRadius = radius; e.HealInterval = interval; e.AttackInterval = interval;
            e.AttackWindup = windup; e.AttackRecovery = recovery;
            e.HealFraction = 0.08f; e.HealBudgetFraction = 0.25f;
            e.CountsAsSupport = true;
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

            w.Add(Wave(3, 1, 10, 3, 0.85f, bonus, 24, (EnemyId.H1_BatonGuard, 16), (EnemyId.H2_RifleGuard, 8)));
            w.Add(Wave(3, 2, 14, 4, 0.6f, bonus, 62, (EnemyId.H1_BatonGuard, 30), (EnemyId.H2_RifleGuard, 18), (EnemyId.H3_FastAssaulter, 14)));
            w.Add(Wave(3, 3, 18, 5, 0.48f, bonus, 88, (EnemyId.H1_BatonGuard, 36), (EnemyId.H2_RifleGuard, 24), (EnemyId.H3_FastAssaulter, 16), (EnemyId.H4_ShieldGuard, 12)));
            w.Add(Wave(3, 4, 22, 6, 0.38f, bonus, 116, (EnemyId.H1_BatonGuard, 42), (EnemyId.H2_RifleGuard, 28), (EnemyId.H3_FastAssaulter, 20), (EnemyId.H5_Medic, 8), (EnemyId.H6_Grenadier, 18)));
            w.Add(Wave(3, 5, 26, 7, 0.32f, bonus, 164, (EnemyId.H1_BatonGuard, 48), (EnemyId.H2_RifleGuard, 32), (EnemyId.H3_FastAssaulter, 24), (EnemyId.H4_ShieldGuard, 18), (EnemyId.H5_Medic, 8), (EnemyId.H6_Grenadier, 18), (EnemyId.H7_Engineer, 8)));
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
                    // D86: more health and armour than v0.19 (2600), and a pounce that closes the distance.
                    Id = BossId.LargeBeast, Phase = 1, Health = 3400, Damage = 20, ActiveCap = 24, RewardXp = 210, RewardCoins = 180,
                    OpeningAdds = Adds((EnemyId.A1_RunnerWolf, 8), (EnemyId.A2_QuillShooter, 4)),
                    Thresholds = new List<BossThreshold>
                    {
                        new() { HealthFraction = 0.65f, Adds = Adds((EnemyId.A1_RunnerWolf, 6), (EnemyId.A3_ChargingBoar, 2)) },
                        new() { HealthFraction = 0.35f, Adds = Adds((EnemyId.A2_QuillShooter, 4), (EnemyId.A5_HowlingWolf, 2), (EnemyId.A6_ExplosiveBeetle, 4)) },
                    },
                    MoveSpeed = 2.4f, BodyRadius = 1.4f, HitHeight = HitHeight(3.5f, 1.4f), AttackInterval = 3.5f, LowHealthFraction = 0.35f, LowHealthAttackInterval = 3f,
                    ChargeTelegraph = 0.8f, ChargeSpeed = 9, ChargeRecovery = 1.2f,
                    StrikeWindup = 0.9f, StrikeRadius = 2.8f, StrikeRecovery = 1.3f, AddSpawnGap = 0.4f,
                    // D87: a fresh pack every 8 s while the boss lives (no reward; the pool above stays fixed).
                    Reinforcements = Adds((EnemyId.A1_RunnerWolf, 3), (EnemyId.A2_QuillShooter, 1), (EnemyId.A3_ChargingBoar, 1)), ReinforceInterval = 8f,
                    Moves = Moves(BossMove.Charge, BossMove.Strike, BossMove.Pounce), Armor = 0.3f, ExposedArmor = 0f,
                    PounceTelegraph = 0.6f, PounceFlightTime = 0.5f, PounceRange = 8f, PounceRadius = 2.6f, PounceDamage = 22f, PounceRecovery = 1f, PounceChainLowHealth = 2,
                },
                new()
                {
                    // D86: v0.19 had 6500 health and a 30° single-bullet sweep that one step sideways escaped.
                    Id = BossId.IndustrialRobot, Phase = 2, Health = 8000, Damage = 28, ActiveCap = 32, RewardXp = 358, RewardCoins = 324, IsMachine = true,
                    OpeningAdds = Adds((EnemyId.R1_Crawler, 6), (EnemyId.R2_Shooter, 4)),
                    Thresholds = new List<BossThreshold>
                    {
                        new() { HealthFraction = 0.7f, Adds = Adds((EnemyId.R2_Shooter, 4), (EnemyId.R3_SawCharger, 4), (EnemyId.R5_Repair, 2)) },
                        new() { HealthFraction = 0.35f, Adds = Adds((EnemyId.R3_SawCharger, 4), (EnemyId.R6_Suicide, 6), (EnemyId.R7_DroneCarrier, 2)) },
                    },
                    MoveSpeed = 1.8f, BodyRadius = 1.6f, HitHeight = HitHeight(4.15f, 1.6f), AttackInterval = 4f, LowHealthFraction = 0.35f, LowHealthAttackInterval = 3.5f,
                    BurstWarning = 0.8f, BurstDuration = 1.8f, BurstRecovery = 1.3f, BurstShotInterval = 0.15f, BurstSpread = 70, ProjectileSpeed = 10,
                    BurstFanCount = 3, BurstFanSpacing = 9f, BurstSweeps = 2, BurstTrackRate = 0f,
                    BlastWarning = 1.0f, BlastRadius = 3, BlastThrowRange = 9, BlastRecovery = 1.5f, HealCapFraction = 0.1f,
                    BurstShotDamage = 18, ProjectileRange = 16, AddSpawnGap = 0.35f, EmpAffects = true,
                    Reinforcements = Adds((EnemyId.R1_Crawler, 3), (EnemyId.R2_Shooter, 2), (EnemyId.R3_SawCharger, 1)), ReinforceInterval = 8f,
                    Moves = Moves(BossMove.Burst, BossMove.Blast, BossMove.Burst, BossMove.Blast, BossMove.Leap), Armor = 0.4f, ExposedArmor = 0f,
                    LeapRiseTime = 0.45f, LeapTrackTime = 1.8f, LeapTrackSpeed = 7f, LeapLockTime = 0.9f, LeapRadius = 3.4f, LeapDamage = 40f, LeapRecovery = 1.6f,
                },
                new()
                {
                    // Burst: three shots 0.2 apart (duration 0.6). Grenade: thrown 0.6 after marking, one-second fuse → 1.6 marked.
                    // D86: v0.19 had 11000 health; the final boss now uses every kind of move.
                    Id = BossId.FormerCompanion, Phase = 3, Health = 13500, Damage = 32, ActiveCap = 40, RewardXp = 356, RewardCoins = 486,
                    OpeningAdds = Adds((EnemyId.H1_BatonGuard, 8), (EnemyId.H2_RifleGuard, 6)),
                    Thresholds = new List<BossThreshold>
                    {
                        new() { HealthFraction = 0.7f, Adds = Adds((EnemyId.H2_RifleGuard, 6), (EnemyId.H5_Medic, 2), (EnemyId.H6_Grenadier, 4)) },
                        new() { HealthFraction = 0.35f, Adds = Adds((EnemyId.H3_FastAssaulter, 6), (EnemyId.H4_ShieldGuard, 6), (EnemyId.H7_Engineer, 2)) },
                    },
                    MoveSpeed = 2.2f, BodyRadius = 0.9f, HitHeight = HitHeight(3.0f, 0.9f), AttackInterval = 3.5f, LowHealthFraction = 0.35f, LowHealthAttackInterval = 3f,
                    // D89: a longer burst whose cone turns to the player quickly and whose bullets bend after them (same range).
                    BurstWarning = 0.7f, BurstDuration = 1f, BurstRecovery = 1.2f, BurstShotInterval = 0.2f, BurstSpread = 20, ProjectileSpeed = 11,
                    BurstShotDamage = 26, ProjectileRange = 15, AddSpawnGap = 0.35f, EmpAffects = true, BurstTrackRate = 160f, BurstHoming = 50f,
                    // D89: three grenades 0.3 s apart around where the player is heading, never on them (see BossSystem.ThrowNextBlast).
                    BlastWarning = 1.4f, BlastFlightFraction = 0.5f, BlastRadius = 2.4f, BlastThrowRange = 9, BlastRecovery = 0f, HealCapFraction = 0.1f,
                    BlastCount = 3, BlastGap = 0.3f, BlastLead = 0.9f, BlastPlayerGap = 0.35f, BlastSideAngle = 100f,
                    Reinforcements = Adds((EnemyId.H1_BatonGuard, 2), (EnemyId.H2_RifleGuard, 2), (EnemyId.H3_FastAssaulter, 1)), ReinforceInterval = 8f,
                    // D89: pounces twice as often, two in a row (three at low health), and quicker.
                    Moves = Moves(BossMove.Burst, BossMove.Pounce, BossMove.Blast, BossMove.Pounce, BossMove.Leap), Armor = 0.45f, ExposedArmor = 0f,
                    PounceTelegraph = 0.45f, PounceFlightTime = 0.35f, PounceRange = 8f, PounceRadius = 2.2f, PounceDamage = 28f, PounceRecovery = 0.7f,
                    PounceChain = 2, PounceChainLowHealth = 3,
                    LeapRiseTime = 0.4f, LeapTrackTime = 1.6f, LeapTrackSpeed = 7.5f, LeapLockTime = 0.85f, LeapRadius = 3f, LeapDamage = 40f, LeapRecovery = 1.4f,
                },
            };
        }

        static List<BossMove> Moves(params BossMove[] moves) => new(moves);

        static List<EnemyCount> Adds(params (EnemyId id, int n)[] adds)
        {
            var list = new List<EnemyCount>();
            foreach (var (id, n) in adds) list.Add(new EnemyCount(id, n));
            return list;
        }

        // ---------------- Weapons (v0.19 §6: every level designer-defined). Projectile speed 60 direct, 45 shotgun. ----------------
        // Level 1 cost is the purchase price; level 2/3 costs are the sequential upgrade prices.
        // D86: automatic rifles (SMG, industrial, energy) lost damage, magazine and accuracy under held fire (bloom), so
        // safe long-range fire no longer out-damages the close-range shotguns; the pistol and shotguns gained a little.

        static List<WeaponData> Weapons()
        {
            const float Direct = 60, Shot = 45, Fall = 0.6f;
            return new List<WeaponData>
            {
                Gun(WeaponId.Pistol, "Pistol", WeaponFamily.Sidearm, 0, reloadSurvivesDash: true,
                    G(1, true, 0, 18, 3, 1, 2, 8, 0.9f, 14, 1, Direct, 1),
                    G(2, true, 25, 24, 3.3f, 1, 1.6f, 10, 0.85f, 15, 1, Direct, 1),
                    G(3, true, 50, 32, 3.5f, 1, 1.2f, 12, 0.8f, 16, 1, Direct, 1)),
                Bloom(Gun(WeaponId.LightSmg, "Light SMG", WeaponFamily.Direct, 1, false,
                    G(1, true, 80, 10, 9, 1, 8, 24, 1.4f, 10, 1, Direct, 1),
                    G(2, true, 60, 12, 9.5f, 1, 7, 28, 1.3f, 10.5f, 1, Direct, 1),
                    G(3, true, 120, 14, 10, 1, 6, 30, 1.25f, 11, 1, Direct, 1)), perShot: 0.8f, max: 10f, recovery: 30f, delay: 0.2f),
                Gun(WeaponId.DoubleBarrel, "Double-barrel shotgun", WeaponFamily.Spread, 1, false,
                    G(1, true, 120, 10, 1.5f, 8, 26, 4, 1.5f, 7, 1, Shot, Fall),
                    G(2, true, 90, 12, 1.6f, 8, 24, 6, 1.4f, 7.5f, 1, Shot, Fall),
                    G(3, true, 180, 14, 1.7f, 10, 22, 6, 1.3f, 8, 1, Shot, Fall)),
                Melee(WeaponId.Baton, "Baton", 1,
                    M(1, true, 90, 50, 2.2f, 1.7f, 100, 0.8f),
                    M(2, true, 60, 65, 2.35f, 1.85f, 110, 1f),
                    M(3, true, 120, 80, 2.5f, 2f, 120, 1.2f)),
                Bloom(Gun(WeaponId.IndustrialRifle, "Industrial rifle", WeaponFamily.Direct, 2, false,
                    G(1, true, 260, 24, 6.5f, 1, 3, 24, 1.6f, 16, 1, Direct, 1),
                    G(2, true, 130, 28, 6.8f, 1, 2.5f, 26, 1.5f, 17, 1, Direct, 1),
                    G(3, true, 240, 32, 7, 1, 2, 30, 1.4f, 18, 1, Direct, 1)), perShot: 0.6f, max: 7f, recovery: 25f, delay: 0.25f),
                Gun(WeaponId.MagazineShotgun, "Magazine shotgun", WeaponFamily.Spread, 2, false,
                    G(1, true, 320, 18, 1.8f, 7, 26, 8, 1.8f, 8, 1, Shot, Fall),
                    G(2, true, 160, 21, 1.9f, 8, 24, 10, 1.65f, 8.5f, 1, Shot, Fall),
                    G(3, true, 280, 24, 2f, 9, 22, 12, 1.5f, 9, 1, Shot, Fall)),
                Melee(WeaponId.IndustrialHammer, "Industrial hammer", 2,
                    M(1, true, 280, 125, 1.2f, 2f, 120, 1.4f),
                    M(2, true, 140, 155, 1.3f, 2.15f, 130, 1.6f),
                    M(3, true, 250, 190, 1.35f, 2.3f, 140, 1.8f)),
                Bloom(Gun(WeaponId.EnergyRifle, "Energy rifle", WeaponFamily.Direct, 3, false,
                    G(1, true, 650, 38, 7, 1, 2, 24, 1.7f, 18, 2, Direct, 1),
                    G(2, true, 280, 42, 7.5f, 1, 1.7f, 26, 1.6f, 19, 2, Direct, 1),
                    G(3, true, 450, 46, 8, 1, 1.4f, 28, 1.5f, 20, 2, Direct, 1)), perShot: 0.5f, max: 6f, recovery: 22f, delay: 0.25f),
                Gun(WeaponId.EnergyShotgun, "Energy shotgun", WeaponFamily.Spread, 3, false,
                    G(1, true, 750, 24, 2, 8, 30, 8, 1.8f, 9, 2, Shot, Fall),
                    G(2, true, 330, 27, 2.1f, 9, 29, 10, 1.65f, 9.5f, 2, Shot, Fall),
                    G(3, true, 520, 31, 2.2f, 10, 28, 12, 1.55f, 10, 2, Shot, Fall)),
                Melee(WeaponId.EnergyBlade, "Energy blade", 3,
                    M(1, true, 680, 180, 1.8f, 2.3f, 150, 0.6f),
                    M(2, true, 300, 210, 1.95f, 2.45f, 165, 0.75f),
                    M(3, true, 480, 245, 2.1f, 2.6f, 180, 0.9f)),
            };
        }

        static WeaponData Bloom(WeaponData w, float perShot, float max, float recovery, float delay)
        {
            w.BloomPerShot = perShot; w.BloomMax = max; w.BloomRecovery = recovery; w.BloomRecoveryDelay = delay;
            return w;
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
                new() { Id = ItemId.Mine, DisplayName = "Mine", UnlockPhase = 2, Price = 30, Carry = 2, StockPerEncounter = 2, Damage = 260, Radius = 2.7f, ArmingTime = 0.6f, TriggerRadius = 1.2f, MaxActivePerPhase = 4 },
                new() { Id = ItemId.EmpPulse, DisplayName = "EMP pulse", UnlockPhase = 2, Price = 45, Carry = 2, StockPerEncounter = 2, Radius = 4, StunRegular = 2, StunBoss = 0.8f, BossResistance = 6 },
                new() { Id = ItemId.PersonalShield, DisplayName = "Personal shield", UnlockPhase = 3, Price = 80, Carry = 1, StockPerEncounter = 1, Duration = 3 },
            };
        }
    }
}
