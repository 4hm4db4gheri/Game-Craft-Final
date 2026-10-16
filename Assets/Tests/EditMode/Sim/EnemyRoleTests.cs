using System;
using System.Collections.Generic;
using System.Linq;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using Hollow.Tests.Data;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>T07 acceptance tests (GDD §5, §6).</summary>
    public class EnemyRoleTests
    {
        static Simulation Solo() => SimTestUtil.NewSim();

        static void Kill(World w, EnemyState e) => Combat.DamageEnemy(w, e, 1e6f, DamageSource.Weapon, Vector2.right);

        // ---------------------------------------------------------------- producers

        static readonly EnemyId[] Producers = { EnemyId.A7_MotherSpider, EnemyId.R7_DroneCarrier, EnemyId.H7_Engineer };

        public static IEnumerable<TestCaseData> ProducerCases()
        {
            foreach (var p in Producers)
            foreach (var when in new[] { "early", "midway", "late", "children-first" })
                yield return new TestCaseData(p, when).SetName($"Producer pool {p} killed {when}");
        }

        [TestCaseSource(nameof(ProducerCases))]
        public void ProducerRewardPoolIsPaidExactlyOnce(EnemyId producerId, string when)
        {
            var sim = Solo();
            var w = sim.World;
            w.Player.InvulnerableUntilTick = long.MaxValue;
            w.Player.Position = w.Player.PrevPosition = new Vector2(-15, -15);
            var d = w.Profile.Enemy(producerId);
            var producer = EnemySystem.Spawn(w, producerId, new Vector2(8, 8), warning: false);

            int ticksToAll = (int)(60 * (d.ProduceDelay * (d.ProduceMax + 1) + 2));
            switch (when)
            {
                case "early": Kill(w, producer); break;
                case "midway":
                    for (int t = 0; t < ticksToAll && producer.ChildrenProduced < 1; t++) sim.Step(PlayerCommand.None);
                    Kill(w, producer);
                    break;
                case "late":
                    sim.Run(ticksToAll, PlayerCommand.None);
                    Assert.AreEqual(d.ProduceMax, producer.ChildrenProduced, "never more than the maximum");
                    Kill(w, producer);
                    break;
                case "children-first":
                    sim.Run(ticksToAll, PlayerCommand.None);
                    foreach (var c in w.Enemies.Where(x => x.ParentId == producer.Id).ToList()) Kill(w, c);
                    Kill(w, producer);
                    break;
            }
            sim.Run(60 * 30, PlayerCommand.None); // production must stop after the producer died
            int childrenAlive = w.Enemies.Count(x => x.Alive && x.ParentId == producer.Id);
            Assert.LessOrEqual(w.Progress.TotalXp, d.Xp, "never more than the pool while children live");
            foreach (var c in w.Enemies.Where(x => x.ParentId == producer.Id).ToList()) Kill(w, c);
            Assert.AreEqual(d.Xp, w.Progress.TotalXp, $"XP pool exact (children alive before: {childrenAlive})");
            Assert.AreEqual(d.Coins, w.Progress.CoinsGenerated, "coin pool exact");
        }

        [Test]
        public void ProducerDeathCancelsUnspawnedProduction()
        {
            var sim = Solo();
            var w = sim.World;
            w.Player.InvulnerableUntilTick = long.MaxValue;
            var spider = EnemySystem.Spawn(w, EnemyId.A7_MotherSpider, new Vector2(8, 8), warning: false);
            for (int t = 0; t < 60 * 20 && spider.ChildrenProduced < 1; t++) sim.Step(PlayerCommand.None);
            Kill(w, spider);
            sim.Run(60 * 20, PlayerCommand.None);
            Assert.AreEqual(1, w.Enemies.Count(x => x.Type == EnemyId.A7c_Spiderling));
        }

        // ---------------------------------------------------------------- charger

        [Test]
        public void ChargerMarksALaneThenChargesAndHitsOnce()
        {
            var sim = Solo();
            var w = sim.World;
            var boar = EnemySystem.Spawn(w, EnemyId.A3_ChargingBoar, new Vector2(5, 0), warning: false);
            w.Drain();
            bool laneSeen = false, charged = false;
            for (int t = 0; t < 60 * 4 && !(charged && boar.Ai == EnemyAi.Recover); t++)
            {
                sim.Step(PlayerCommand.None);
                foreach (var ev in w.Drain())
                    if (ev.Type == SimEventType.EnemyTelegraph && ev.Code == (int)TelegraphShape.Lane) laneSeen = true;
                if (boar.Ai == EnemyAi.Charge) { Assert.IsTrue(laneSeen, "lane telegraph before the charge"); charged = true; }
            }
            Assert.IsTrue(charged);
            Assert.AreEqual(100f - w.Profile.Enemy(EnemyId.A3_ChargingBoar).Damage, w.Player.Health, 1e-3f, "one hit");
            Assert.AreEqual(EnemyAi.Recover, boar.Ai, "stops after the charge (punish window)");
        }

        [Test]
        public void SideStepAvoidsTheCharge()
        {
            var sim = Solo();
            var w = sim.World;
            var boar = EnemySystem.Spawn(w, EnemyId.A3_ChargingBoar, new Vector2(5, 0), warning: false);
            for (int t = 0; t < 120 && boar.Ai != EnemyAi.Windup; t++) sim.Step(PlayerCommand.None);
            sim.Run(30, SimTestUtil.Move(Vector2.up));
            sim.Run(60, PlayerCommand.None);
            Assert.AreEqual(100f, w.Player.Health);
        }

        // ---------------------------------------------------------------- explosive

        [Test]
        public void BeetleTelegraphsThenExplodes()
        {
            var sim = Solo();
            var w = sim.World;
            var beetle = EnemySystem.Spawn(w, EnemyId.A6_ExplosiveBeetle, new Vector2(4, 0), warning: false);
            for (int t = 0; t < 300 && beetle.Ai != EnemyAi.Windup; t++) sim.Step(PlayerCommand.None);
            Assert.AreEqual(EnemyAi.Windup, beetle.Ai);
            Assert.AreEqual(100f, w.Player.Health, "no damage during the fuse");
            sim.Run(90, PlayerCommand.None);
            Assert.IsFalse(beetle.Alive);
            Assert.AreEqual(100f - w.Profile.Enemy(EnemyId.A6_ExplosiveBeetle).Damage, w.Player.Health, 1e-3f);
        }

        [Test]
        public void KillingTheBeetleBeforeTheFusePreventsTheBlast()
        {
            var sim = Solo();
            var w = sim.World;
            var beetle = EnemySystem.Spawn(w, EnemyId.A6_ExplosiveBeetle, new Vector2(4, 0), warning: false);
            for (int t = 0; t < 300 && beetle.Ai != EnemyAi.Windup; t++) sim.Step(PlayerCommand.None);
            Kill(w, beetle);
            sim.Run(120, PlayerCommand.None);
            Assert.AreEqual(100f, w.Player.Health);
        }

        [Test]
        public void LeavingTheBlastRadiusAvoidsDamage()
        {
            var sim = Solo();
            var w = sim.World;
            var bot = EnemySystem.Spawn(w, EnemyId.R6_Suicide, new Vector2(4, 0), warning: false);
            for (int t = 0; t < 300 && bot.Ai != EnemyAi.Windup; t++) sim.Step(PlayerCommand.None);
            sim.Run(100, SimTestUtil.Move(Vector2.left));
            Assert.IsFalse(bot.Alive, "it detonated");
            Assert.AreEqual(100f, w.Player.Health, "out of range");
        }

        // ---------------------------------------------------------------- support

        [Test]
        public void HowlSpeedsUpNearbyAnimalsWithoutStacking()
        {
            var sim = Solo();
            var w = sim.World;
            w.Player.InvulnerableUntilTick = long.MaxValue;
            w.Player.Position = w.Player.PrevPosition = new Vector2(-15, 0);
            var howler = EnemySystem.Spawn(w, EnemyId.A5_HowlingWolf, new Vector2(10, 0), warning: false);
            var wolf = EnemySystem.Spawn(w, EnemyId.A1_RunnerWolf, new Vector2(10, 1.5f), warning: false);
            wolf.StunnedUntilTick = 0;
            for (int t = 0; t < 60 * 6 && wolf.SpeedBoostUntilTick <= w.Clock.Tick; t++) sim.Step(PlayerCommand.None);
            Assert.Greater(wolf.SpeedBoostUntilTick, w.Clock.Tick, "boosted");
            sim.Step(PlayerCommand.None);
            float expected = w.Profile.Enemy(EnemyId.A1_RunnerWolf).MoveSpeed * (1f + w.Profile.Enemy(EnemyId.A5_HowlingWolf).AuraSpeedBonus);
            Assert.LessOrEqual(wolf.Velocity.magnitude, expected + 1e-3f, "15 % at most, never stacked");
        }

        [Test]
        public void RepairIsLimitedPerHealer()
        {
            var sim = Solo();
            var w = sim.World;
            w.Player.Position = w.Player.PrevPosition = new Vector2(-15, -15);
            w.Player.InvulnerableUntilTick = long.MaxValue;
            var repair = EnemySystem.Spawn(w, EnemyId.R5_Repair, new Vector2(10, 10), warning: false);
            var tank = w.AddEnemy(EnemyId.R4_ShieldRobot, new Vector2(11, 10), health: 1000);
            tank.Health = 1;
            tank.StunnedUntilTick = long.MaxValue;
            sim.Run(60 * 60, PlayerCommand.None);
            var d = w.Profile.Enemy(EnemyId.R5_Repair);
            Assert.AreEqual(1 + d.HealTotalCap, tank.Health, 1e-3f);
            Assert.AreEqual(d.HealTotalCap, repair.HealGiven, 1e-3f);
        }

        // ---------------------------------------------------------------- grenadier

        [Test]
        public void GrenadierMarksTheZoneAndTheExplosiveIsDelayed()
        {
            var sim = Solo();
            var w = sim.World;
            var g = EnemySystem.Spawn(w, EnemyId.H6_Grenadier, new Vector2(6, 0), warning: false);
            for (int t = 0; t < 300 && g.Ai != EnemyAi.Windup; t++) sim.Step(PlayerCommand.None);
            Assert.AreEqual(EnemyAi.Windup, g.Ai, "zone marked first");
            Vector2 zone = g.AttackPoint;
            for (int t = 0; t < 300 && w.Hazards.Count == 0; t++) sim.Step(PlayerCommand.None);
            Assert.AreEqual(1, w.Hazards.Count);
            Assert.AreEqual(zone, w.Hazards[0].Position);
            Assert.AreEqual(100f, w.Player.Health, "delayed: no damage yet");
            sim.Run(30, SimTestUtil.Move(Vector2.down));
            sim.Run(60, PlayerCommand.None);
            Assert.AreEqual(100f, w.Player.Health, "leaving the marked zone avoids the blast");
            Assert.AreEqual(0, w.Hazards.Count);
        }

        // ---------------------------------------------------------------- armour

        [Test]
        public void ArmoredEnemiesTurnSlowlySoTheyCanBeFlanked()
        {
            var sim = Solo();
            var w = sim.World;
            var tortoise = EnemySystem.Spawn(w, EnemyId.A4_ArmoredTortoise, new Vector2(4, 0), warning: false);
            tortoise.Facing = Vector2.left;
            w.Player.Position = w.Player.PrevPosition = new Vector2(4, 3); // suddenly at its side
            sim.Step(PlayerCommand.None);
            float turned = Vector2.Angle(Vector2.left, tortoise.Facing);
            Assert.LessOrEqual(turned, w.Profile.Enemy(EnemyId.A4_ArmoredTortoise).TurnRateDeg / 60f + 1e-3f);
            Assert.AreEqual(1f, Combat.ArmorMultiplier(w, tortoise, Vector2.down, false), "side hit is unarmoured");
        }

        // ---------------------------------------------------------------- every type is telegraphed

        public static IEnumerable<EnemyId> AttackingTypes() =>
            Enum.GetValues(typeof(EnemyId)).Cast<EnemyId>().Where(id => TestData.Baseline().Enemy(id).Damage > 0);

        [TestCaseSource(nameof(AttackingTypes))]
        public void EveryDamagingEnemyTelegraphsFirst(EnemyId id)
        {
            var sim = Solo();
            var w = sim.World;
            w.Player.Health = 1e6f;
            var e = EnemySystem.Spawn(w, id, new Vector2(5, 1), warning: false);
            long lastTelegraph = long.MinValue;
            int damagingAttacks = 0;
            float hp = w.Player.Health;
            for (int t = 0; t < 60 * 20; t++)
            {
                long tick = w.Clock.Tick;
                sim.Step(PlayerCommand.None);
                foreach (var ev in w.Drain())
                {
                    if (ev.EntityId != e.Id) continue;
                    if (ev.Type == SimEventType.EnemyTelegraph) lastTelegraph = tick;
                }
                if (w.Player.Health < hp)
                {
                    damagingAttacks++;
                    Assert.Greater(lastTelegraph, long.MinValue, $"{id} hurt the player without any telegraph");
                    Assert.GreaterOrEqual(tick - lastTelegraph, Mathf.Min(w.Ticks(w.Profile.Enemy(id).AttackWindup), 1), $"{id} hit too soon after its telegraph");
                    hp = w.Player.Health;
                }
                if (!e.Alive) break;
            }
            Assert.Greater(damagingAttacks, 0, $"{id} never attacked in 20 s");
        }
    }
}
