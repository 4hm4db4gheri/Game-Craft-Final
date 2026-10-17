using System.Linq;
using Hollow.Core;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>T09 acceptance tests (GDD §4).</summary>
    public class CompanionTests
    {
        static Simulation WithCompanion()
        {
            var layout = SimTestUtil.OpenArena();
            layout.CompanionStart = new Vector2(-2f, -1f);
            return SimTestUtil.NewSim(layout, companion: true);
        }

        [Test]
        public void PhaseOneStats()
        {
            var w = WithCompanion().World;
            Assert.IsTrue(w.Companion.Present);
            Assert.AreEqual(140f, w.Companion.MaxHealth);
            Assert.AreEqual(140f, w.Companion.Health);
        }

        [Test]
        public void GodModeProtectsPlayerAndOperatorAndTurnsOffImmediately()
        {
            var w = WithCompanion().World;
            float playerHealth = w.Player.Health;
            float operatorHealth = w.Companion.Health;
            w.GodMode = true;

            Assert.IsFalse(PlayerSystem.Damage(w, 10, Vector2.left));
            Assert.IsFalse(CompanionSystem.Damage(w, 10, Vector2.left));
            Assert.AreEqual(playerHealth, w.Player.Health);
            Assert.AreEqual(operatorHealth, w.Companion.Health);
            Assert.AreEqual(FailureReason.None, w.Failure);
            Assert.IsFalse(WorldSerializer.ToJson(w).Contains("GodMode"), "developer mode must not enter saves");

            w.GodMode = false;
            Assert.IsTrue(PlayerSystem.Damage(w, 10, Vector2.left));
            Assert.IsTrue(CompanionSystem.Damage(w, 10, Vector2.left));
            Assert.Less(w.Player.Health, playerHealth);
            Assert.Less(w.Companion.Health, operatorHealth);
        }

        [Test]
        public void FollowsWithinALimitedDistance()
        {
            var sim = WithCompanion();
            var w = sim.World;
            sim.Run(60 * 3, SimTestUtil.Move(Vector2.right));
            float d = Vector2.Distance(w.Companion.Position, w.Player.Position);
            Assert.LessOrEqual(d, w.Profile.Companion.FollowDistanceMax + 0.5f);
            sim.Run(60 * 2, PlayerCommand.None);
            d = Vector2.Distance(w.Companion.Position, w.Player.Position);
            Assert.That(d, Is.InRange(w.Profile.Companion.FollowDistanceMin - 0.6f, w.Profile.Companion.FollowDistanceMax + 0.2f));
        }

        [Test]
        public void DoesNotBlockThePlayer()
        {
            var sim = WithCompanion();
            var w = sim.World;
            sim.Run(60, PlayerCommand.None);
            Vector2 start = w.Player.Position;
            // Walk straight through where the companion stands, back and forth.
            sim.Run(60, SimTestUtil.Move((w.Companion.Position - w.Player.Position).normalized));
            Assert.Greater(Vector2.Distance(start, w.Player.Position), 4f, "player kept full speed");
        }

        [Test]
        public void ShootsWithPhaseDamageAndIntervalAndKillsStillPayThePlayer()
        {
            var sim = WithCompanion();
            var w = sim.World;
            var target = w.AddEnemy(EnemyId.H1_BatonGuard, new Vector2(5f, 2f), health: 16f);
            target.StunnedUntilTick = long.MaxValue;
            w.Drain();
            sim.Run(60 * 3, PlayerCommand.None);
            var events = w.Drain();
            var hits = events.Where(e => e.Type == SimEventType.EnemyHit && e.Code == (int)DamageSource.Companion).ToList();
            Assert.GreaterOrEqual(hits.Count, 2);
            Assert.AreEqual(8f, hits[0].Value, 1e-4f, "phase-one shot damage");
            Assert.IsFalse(target.Alive);
            Assert.AreEqual(w.Profile.Enemy(EnemyId.H1_BatonGuard).Xp, w.Progress.TotalXp, "companion kills pay the player");
            int shots = events.Count(e => e.Type == SimEventType.ShotFired && e.EntityId == CompanionState.EntityId);
            Assert.LessOrEqual(shots, 3, "1.2 s interval");
        }

        [Test]
        public void AtMostThreeEnemiesTargetTheCompanion()
        {
            var sim = WithCompanion();
            var w = sim.World;
            w.Player.Position = w.Player.PrevPosition = new Vector2(12, 12);
            w.Companion.Position = new Vector2(-6, -6);
            w.Companion.Health = 100000;
            for (int i = 0; i < 8; i++)
                EnemySystem.Spawn(w, EnemyId.A1_RunnerWolf, new Vector2(-6, -6) + new Vector2(Mathf.Cos(i), Mathf.Sin(i)) * 2.5f, warning: false);
            int maxSeen = 0;
            for (int t = 0; t < 300; t++)
            {
                sim.Step(PlayerCommand.None);
                maxSeen = Mathf.Max(maxSeen, w.Enemies.Count(e => e.Alive && e.Target == TargetKind.Companion));
            }
            Assert.AreEqual(3, maxSeen);
        }

        [Test]
        public void AttackingAnEnemyDrawsItAwayFromTheCompanion()
        {
            var sim = WithCompanion();
            var w = sim.World;
            var e = w.AddEnemy(EnemyId.A1_RunnerWolf, new Vector2(-3f, -1f));
            e.Target = TargetKind.Companion;
            Combat.DamageEnemy(w, e, 1, DamageSource.Weapon, Vector2.left);
            Assert.AreEqual(TargetKind.Player, e.Target);
        }

        [Test]
        public void CompanionDeathFailsTheEncounter()
        {
            var sim = WithCompanion();
            var w = sim.World;
            CompanionSystem.Damage(w, 1000, Vector2.left);
            Assert.IsTrue(w.Companion.Dead);
            Assert.AreEqual(FailureReason.CompanionDied, w.Failure);
        }

        [Test]
        public void OperatorRegeneratesAfterDamageFreeDelayAndStopsAtMaximum()
        {
            var w = WithCompanion().World;
            w.Profile.Companion.RegenerationDelay = 2f;
            w.Profile.Companion.RegenerationRate = 6f;
            CompanionSystem.Damage(w, 20, Vector2.left);
            float hurt = w.Companion.Health;
            while (w.Clock.Tick < w.Ticks(2f)) { CompanionSystem.Step(w); w.Clock.Step(true); }
            Assert.AreEqual(hurt, w.Companion.Health, 0.001f);
            CompanionSystem.Step(w);
            Assert.Greater(w.Companion.Health, hurt);
            for (int i = 0; i < 60 * 10; i++) { CompanionSystem.Step(w); w.Clock.Step(true); }
            Assert.AreEqual(w.Companion.MaxHealth, w.Companion.Health, 0.001f);
        }

        [Test]
        public void PlayerDeathFailsTheEncounterWithItsOwnReason()
        {
            var sim = WithCompanion();
            PlayerSystem.Damage(sim.World, 1000, Vector2.left);
            Assert.AreEqual(FailureReason.PlayerDied, sim.World.Failure);
        }

        [Test]
        public void FreezesDuringTheXpPause()
        {
            var sim = WithCompanion();
            var w = sim.World;
            var states = new GameStateMachine(GameState.Loading);
            states.Transition(GameState.Playing);
            var driver = new SimDriver(states, () => sim.Step(SimTestUtil.Move(Vector2.right)));
            for (int i = 0; i < 30; i++) driver.Update(1 / 60.0);
            states.Transition(GameState.XPChoice);
            Vector2 before = w.Companion.Position;
            float hp = w.Companion.Health;
            for (int i = 0; i < 300; i++) driver.Update(1 / 60.0);
            Assert.AreEqual(before, w.Companion.Position);
            Assert.AreEqual(hp, w.Companion.Health);
        }

        [Test]
        public void PhaseTwoEntryRestoresToTheLowerMaximum()
        {
            var sim = WithCompanion();
            var w = sim.World;
            w.Companion.Health = 20;
            w.Phase = 2;
            CompanionSystem.SpawnForPhase(w, w.Companion.Position);
            Assert.AreEqual(100f, w.Companion.MaxHealth);
            Assert.AreEqual(100f, w.Companion.Health);
        }

        [Test]
        public void NoCompanionInPhaseThree()
        {
            var sim = WithCompanion();
            var w = sim.World;
            w.Phase = 3;
            CompanionSystem.SpawnForPhase(w, Vector2.zero);
            Assert.IsFalse(w.Companion.Present);
        }

        [Test]
        public void EnemyShotsCanHitTheCompanion()
        {
            var sim = WithCompanion();
            var w = sim.World;
            w.Companion.Health = 100000;
            sim.Run(120, PlayerCommand.None); // let the companion settle in its slot
            Vector2 c = w.Companion.Position;
            var shooter = EnemySystem.Spawn(w, EnemyId.A2_QuillShooter, c + new Vector2(0, 5f), warning: false);
            shooter.StunnedUntilTick = long.MaxValue;
            EnemySystem.FireEnemyProjectile(w, shooter, w.Profile.Enemy(EnemyId.A2_QuillShooter), Vector2.down);
            sim.Run(60, PlayerCommand.None);
            Assert.Less(w.Companion.Health, 100000f);
        }
    }
}
