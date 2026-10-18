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
    /// <summary>T06 acceptance tests (GDD §5, §6, §14).</summary>
    public class EnemyTests
    {
        [Test]
        public void RunnerWolfFullLoop_SpawnChaseBiteDieCoinsXp()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            var wolfData = w.Profile.Enemy(EnemyId.A1_RunnerWolf);
            var wolf = EnemySystem.Spawn(w, EnemyId.A1_RunnerWolf, new Vector2(5, 0));

            // Spawn warning: inactive for the warning time.
            sim.Run(30, PlayerCommand.None);
            Assert.AreEqual(EnemyAi.Spawning, wolf.Ai);
            Assert.AreEqual(5f, wolf.Position.x, 1e-4f, "does not move while the spawn warning shows");

            // Chase and bite.
            bool telegraphed = false;
            for (int i = 0; i < 300 && w.Player.Health == 100f; i++)
            {
                sim.Step(PlayerCommand.None);
                telegraphed |= w.Events.Any(e => e.Type == SimEventType.EnemyTelegraph && e.EntityId == wolf.Id);
            }
            Assert.IsTrue(telegraphed, "telegraph before the bite");
            Assert.AreEqual(100f - wolfData.Damage, w.Player.Health, 1e-4f);

            // Kill it: XP is automatic, coins fall on the ground.
            Combat.DamageEnemy(w, wolf, 1000, DamageSource.Weapon, Vector2.left);
            sim.Step(PlayerCommand.None);
            Assert.IsFalse(w.Enemies.Contains(wolf));
            Assert.AreEqual(wolfData.Xp, w.Progress.TotalXp);
            Assert.AreEqual(wolfData.Coins, w.Coins.Count + w.Progress.CoinsCollected);
            Assert.AreEqual(wolfData.Coins, w.Progress.CoinsGenerated);

            // Walk over the coins to collect them.
            for (int i = 0; i < 120 && w.Coins.Count > 0; i++)
            {
                var target = w.Coins[0].Position;
                sim.Step(new PlayerCommand { Move = (target - w.Player.Position).normalized });
            }
            Assert.AreEqual(wolfData.Coins, w.Progress.Wallet);
        }

        public static IEnumerable<EnemyId> BaseEnemies() =>
            Enum.GetValues(typeof(EnemyId)).Cast<EnemyId>().Where(id => !TestData.Baseline().Enemy(id).IsChild);

        [TestCaseSource(nameof(BaseEnemies))]
        public void KillRewardsMatchTheTable(EnemyId id)
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            var e = w.AddEnemy(id, new Vector2(6, 6), health: 1);
            Combat.DamageEnemy(w, e, 10, DamageSource.Weapon, Vector2.left);
            var d = w.Profile.Enemy(id);
            Assert.AreEqual(d.Xp, w.Progress.TotalXp);
            Assert.AreEqual(d.Coins, w.Coins.Count);
        }

        [TestCase(-1, 0, 0.3f, TestName = "Armor: hit from the front takes 30%")]
        [TestCase(0, 1, 1f, TestName = "Armor: hit from the side takes 100%")]
        [TestCase(0, -1, 1f, TestName = "Armor: hit from the other side takes 100%")]
        [TestCase(1, 0, 1f, TestName = "Armor: hit from behind takes 100%")]
        public void FrontalArmorDependsOnHitDirection(float travelX, float travelY, float expectedShare)
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            var tortoise = w.AddEnemy(EnemyId.A4_ArmoredTortoise, new Vector2(5, 5));
            tortoise.Facing = Vector2.right; // faces +x, so shots travelling -x hit its front
            float dealt = Combat.DamageEnemy(w, tortoise, 10, DamageSource.Weapon, new Vector2(travelX, travelY));
            Assert.AreEqual(10f * expectedShare, dealt, 1e-4f);
        }

        [Test]
        public void OnlyHitsOnTheShieldAreReportedAsBlocked()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            var tortoise = w.AddEnemy(EnemyId.A4_ArmoredTortoise, new Vector2(5, 5));
            tortoise.Facing = Vector2.right;
            w.Events.Clear();
            Combat.DamageEnemy(w, tortoise, 10, DamageSource.Weapon, Vector2.left);
            int blocked = w.Events.FindIndex(e => e.Type == SimEventType.ArmorBlocked);
            Assert.GreaterOrEqual(blocked, 0, "a frontal hit is a shield hit");
            Assert.AreEqual(tortoise.Id, w.Events[blocked].EntityId);
            Assert.AreEqual(w.Profile.Combat.FrontalArmorReduction, w.Events[blocked].Value, 1e-4f);
            Assert.AreEqual(SimEventType.EnemyHit, w.Events[blocked + 1].Type, "the presentation pairs it with the next hit");

            w.Events.Clear();
            Combat.DamageEnemy(w, tortoise, 10, DamageSource.Weapon, Vector2.right);
            Assert.IsFalse(w.Events.Exists(e => e.Type == SimEventType.ArmorBlocked), "a hit from behind is not");
        }

        [Test]
        public void ArmorDoesNotApplyToUnarmoredEnemiesOrWhenIgnored()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            var wolf = w.AddEnemy(EnemyId.A1_RunnerWolf, new Vector2(5, 5));
            wolf.Facing = Vector2.right;
            Assert.AreEqual(10f, Combat.DamageEnemy(w, wolf, 10, DamageSource.Weapon, Vector2.left), 1e-4f);
            var tortoise = w.AddEnemy(EnemyId.A4_ArmoredTortoise, new Vector2(-5, 5));
            tortoise.Facing = Vector2.right;
            Assert.AreEqual(10f, Combat.DamageEnemy(w, tortoise, 10, DamageSource.Melee, Vector2.left, ignoresArmor: true), 1e-4f);
        }

        [Test]
        public void EveryDamagingAttackIsPrecededByItsTelegraph()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            w.Player.Health = 100000; // survive the whole test
            var ids = new[] { EnemyId.A1_RunnerWolf, EnemyId.A2_QuillShooter, EnemyId.A1_RunnerWolf, EnemyId.A2_QuillShooter };
            for (int i = 0; i < ids.Length; i++)
                EnemySystem.Spawn(w, ids[i], new Vector2(Mathf.Cos(i * 1.6f), Mathf.Sin(i * 1.6f)) * 6f);

            var lastTelegraph = new Dictionary<int, long>();
            int attacks = 0;
            for (int t = 0; t < 60 * 20; t++)
            {
                long tick = w.Clock.Tick;
                sim.Step(PlayerCommand.None);
                foreach (var ev in w.Drain())
                {
                    if (ev.Type == SimEventType.EnemyTelegraph) lastTelegraph[ev.EntityId] = tick;
                    if (ev.Type == SimEventType.EnemyAttack)
                    {
                        attacks++;
                        Assert.IsTrue(lastTelegraph.TryGetValue(ev.EntityId, out long tt), $"enemy {ev.EntityId} attacked without a telegraph");
                        var type = (EnemyId)ev.Code;
                        long windup = w.Ticks(w.Profile.Enemy(type).AttackWindup);
                        Assert.GreaterOrEqual(tick - tt, windup, "attack came before the telegraph finished");
                    }
                }
                // Consume telegraphs once used: each attack (or burst) needs a fresh one.
                foreach (var e in w.Enemies)
                    if (e.Ai == EnemyAi.Recover) lastTelegraph.Remove(e.Id);
            }
            Assert.Greater(attacks, 10, "the scenario must actually produce attacks");
        }

        [Test]
        public void RangedEnemyNeverStartsAnAttackFromOutsideTheView()
        {
            var profile = TestData.Baseline();
            var quill = profile.Enemy(EnemyId.A2_QuillShooter);
            quill.AttackRange = 30; quill.ProjectileRange = 32; quill.MoveSpeed = 0;
            var sim = SimTestUtil.NewSim(profile: profile);
            var w = sim.World;
            var outside = EnemySystem.Spawn(w, EnemyId.A2_QuillShooter, new Vector2(16f, 0), warning: false);
            sim.Run(600, PlayerCommand.None);
            Assert.IsFalse(w.Events.Any(e => e.Type == SimEventType.EnemyTelegraph && e.EntityId == outside.Id));
            Assert.IsEmpty(w.Projectiles);

            var inside = EnemySystem.Spawn(w, EnemyId.A2_QuillShooter, new Vector2(6f, 0), warning: false);
            sim.Run(120, PlayerCommand.None);
            Assert.IsTrue(w.Events.Any(e => e.Type == SimEventType.EnemyTelegraph && e.EntityId == inside.Id));
        }

        [Test]
        public void QuillShotFliesAlongTheTelegraphedLineAndCanBeDodged()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            var shooter = EnemySystem.Spawn(w, EnemyId.A2_QuillShooter, new Vector2(5f, 0), warning: false);
            // Wait for the telegraph, then sidestep out of the line.
            for (int i = 0; i < 300 && shooter.Ai != EnemyAi.Windup; i++) sim.Step(PlayerCommand.None);
            Assert.AreEqual(EnemyAi.Windup, shooter.Ai);
            sim.Run(40, SimTestUtil.Move(Vector2.up));
            sim.Run(40, PlayerCommand.None);
            Assert.AreEqual(100f, w.Player.Health, "leaving the firing line avoids the quill");
        }

        [Test]
        public void WallsBlockEnemyProjectiles()
        {
            var layout = SimTestUtil.OpenArena();
            int wallX = Mathf.FloorToInt(2 - layout.Origin.x);
            for (int y = 1; y < layout.Height - 1; y++) layout.BaseBlocked.Add(layout.CellIndex(wallX, y));
            var sim = SimTestUtil.NewSim(layout);
            var w = sim.World;
            var shooter = EnemySystem.Spawn(w, EnemyId.A2_QuillShooter, new Vector2(5f, 0), warning: false);
            shooter.Ai = EnemyAi.Attack; shooter.AttackDir = Vector2.left; shooter.BurstLeft = 1; shooter.NextBurstTick = 0;
            sim.Run(120, PlayerCommand.None);
            Assert.AreEqual(100f, w.Player.Health);
        }

        [Test]
        public void EnemiesDoNotStackOnTopOfEachOther()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            for (int i = 0; i < 8; i++) EnemySystem.Spawn(w, EnemyId.A1_RunnerWolf, new Vector2(8f, 0.01f * i), warning: false);
            sim.Run(30, PlayerCommand.None);
            for (int i = 0; i < w.Enemies.Count; i++)
            for (int j = i + 1; j < w.Enemies.Count; j++)
            {
                float d = Vector2.Distance(w.Enemies[i].Position, w.Enemies[j].Position);
                Assert.Greater(d, (w.Enemies[i].Radius + w.Enemies[j].Radius) * 0.8f);
            }
        }

        [Test]
        public void EnemiesPathAroundObstacles()
        {
            var layout = SimTestUtil.OpenArena();
            // A wall between the wolf and the player with a gap at the top.
            int wallX = Mathf.FloorToInt(3 - layout.Origin.x);
            for (int y = 1; y < layout.Height - 8; y++) layout.BaseBlocked.Add(layout.CellIndex(wallX, y));
            var sim = SimTestUtil.NewSim(layout);
            var w = sim.World;
            w.Player.Health = 100000;
            var wolf = EnemySystem.Spawn(w, EnemyId.A1_RunnerWolf, new Vector2(8f, 0), warning: false);
            for (int i = 0; i < 60 * 25 && w.Player.Health == 100000; i++) sim.Step(PlayerCommand.None);
            Assert.Less(w.Player.Health, 100000f, "the wolf found its way around the wall");
        }
    }
}
