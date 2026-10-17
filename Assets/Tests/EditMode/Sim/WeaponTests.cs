using System.Linq;
using Hollow.Core.Input;
using Hollow.Data;
using Hollow.Sim;
using Hollow.Tests.Data;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    /// <summary>T03 acceptance tests (GDD §4, §8).</summary>
    public class WeaponTests
    {
        static readonly Vector2 AimRight = new(10, 0);
        static PlayerCommand Hold => new() { FireHeld = true, AimWorld = AimRight };
        static PlayerCommand Idle => new() { AimWorld = AimRight };

        static Simulation WithSmg(out OwnedWeapon smg, BalanceProfile profile = null)
        {
            var sim = SimTestUtil.NewSim(profile: profile);
            smg = WeaponSystem.Give(sim.World, WeaponId.LightSmg);
            sim.Step(new PlayerCommand { SelectWeaponSlot = 2, AimWorld = AimRight });
            sim.Run(12, Idle); // swap time
            return sim;
        }

        [Test]
        public void HoldingFireKeepsTheWeaponRate()
        {
            var sim = WithSmg(out var smg);
            int before = smg.Ammo;
            sim.Run(60, Hold);
            Assert.AreEqual(9, before - smg.Ammo, 1, "≈ 9 shots per second");
        }

        [Test]
        public void ReloadIsCancelledBySwapAndRestartsWhenReturningToTheEmptyGun()
        {
            var sim = WithSmg(out var smg);
            var L = sim.World.Player.Loadout;
            for (int i = 0; i < 400 && smg.Ammo > 0; i++) sim.Step(Hold);
            Assert.AreEqual(0, smg.Ammo);
            Assert.IsTrue(L.Reloading, "last round starts the automatic reload");

            sim.Step(new PlayerCommand { SelectWeaponSlot = 1, AimWorld = AimRight });
            Assert.IsFalse(L.Reloading, "swap cancels the reload");
            sim.Run(200, Idle);
            Assert.AreEqual(0, smg.Ammo, "cancelled reload never fills the magazine");

            sim.Step(new PlayerCommand { SelectWeaponSlot = 2, AimWorld = AimRight });
            sim.Run(12, Idle);
            Assert.IsTrue(L.Reloading, "returning to the empty gun restarts the automatic reload");
            sim.Run(80, Idle);
            Assert.AreEqual(sim.World.Profile.Weapon(WeaponId.LightSmg).Levels[0].Magazine, smg.Ammo);
        }

        [Test]
        public void MagazineStatePersistsAcrossSwaps()
        {
            var sim = WithSmg(out var smg);
            var pistol = sim.World.Player.Loadout.Get(WeaponId.Pistol);
            sim.Run(30, Hold);
            int smgLeft = smg.Ammo;
            sim.Step(new PlayerCommand { SelectWeaponSlot = 1, AimWorld = AimRight });
            sim.Run(12, Idle);
            sim.Step(new PlayerCommand { FirePressed = true, AimWorld = AimRight });
            Assert.AreEqual(7, pistol.Ammo);
            sim.Step(new PlayerCommand { CycleWeapon = 1, AimWorld = AimRight });
            sim.Run(12, Idle);
            Assert.AreEqual(smgLeft, smg.Ammo);
            Assert.AreEqual(7, pistol.Ammo);
        }

        [Test]
        public void ManualReloadIsCancelledByDash()
        {
            var sim = WithSmg(out var smg);
            sim.Step(new PlayerCommand { FirePressed = true, AimWorld = AimRight });
            sim.Step(new PlayerCommand { ReloadPressed = true, AimWorld = AimRight });
            Assert.IsTrue(sim.World.Player.Loadout.Reloading);
            sim.Step(new PlayerCommand { DashPressed = true, Move = Vector2.up, AimWorld = AimRight });
            Assert.IsFalse(sim.World.Player.Loadout.Reloading);
            sim.Run(120, Idle);
            Assert.AreEqual(23, smg.Ammo);
        }

        [Test]
        public void PistolReloadSurvivesDash()
        {
            var sim = SimTestUtil.NewSim();
            var pistol = sim.World.Player.Loadout.Get(WeaponId.Pistol);
            sim.Step(new PlayerCommand { FirePressed = true, AimWorld = AimRight });
            sim.Step(new PlayerCommand { ReloadPressed = true, AimWorld = AimRight });
            sim.Step(new PlayerCommand { DashPressed = true, Move = Vector2.up, AimWorld = AimRight });
            Assert.IsTrue(sim.World.Player.Loadout.Reloading);
            sim.Run(60, Idle);
            Assert.AreEqual(8, pistol.Ammo);
        }

        [Test]
        public void SwapTimeBlocksFiringBriefly()
        {
            var sim = SimTestUtil.NewSim();
            var w = sim.World;
            WeaponSystem.Give(w, WeaponId.LightSmg);
            var pistol = w.Player.Loadout.Get(WeaponId.Pistol);
            sim.Step(new PlayerCommand { SelectWeaponSlot = 2, AimWorld = AimRight });
            sim.Step(new PlayerCommand { SelectWeaponSlot = 1, AimWorld = AimRight });
            sim.Run(5, Hold);
            Assert.AreEqual(8, pistol.Ammo, "no shot during the 0.15 s swap");
            sim.Run(10, Hold);
            Assert.Less(pistol.Ammo, 8);
        }

        [Test]
        public void ShotgunPelletsFallToAboutHalfAtMaxRange()
        {
            float NearAndFar(float distance)
            {
                var profile = TestData.Baseline();
                profile.Weapon(WeaponId.DoubleBarrel).Levels[0].SpreadDeg = 0;
                var sim = SimTestUtil.NewSim(profile: profile);
                var w = sim.World;
                WeaponSystem.Give(w, WeaponId.DoubleBarrel);
                sim.Step(new PlayerCommand { SelectWeaponSlot = 2, AimWorld = AimRight });
                sim.Run(12, Idle);
                var target = w.AddEnemy(EnemyId.H1_BatonGuard, new Vector2(distance, 0), 100000);
                target.StunnedUntilTick = long.MaxValue;
                w.Drain();
                sim.Step(new PlayerCommand { FirePressed = true, AimWorld = AimRight });
                sim.Run(30, Idle);
                var hits = w.Drain().Where(e => e.Type == SimEventType.EnemyHit).Select(e => e.Value).ToList();
                Assert.AreEqual(8, hits.Count, "all pellets hit");
                return hits.Average();
            }

            float pellet = 9f;
            float near = NearAndFar(1.0f);
            float mid = NearAndFar(3.3f); // hits the body edge at ~2.95, before half range (3.5): still full (v0.19 §6)
            float far = NearAndFar(7.2f); // pellets meet the body edge just inside the 7-unit range
            Assert.AreEqual(pellet, near, 1e-3f);
            Assert.AreEqual(pellet, mid, 1e-3f);
            Assert.AreEqual(pellet * 0.5f, far, pellet * 0.06f);
        }

        [Test]
        public void EnergyRifleHitsTheFirstTargetAndOneBehind()
        {
            var profile = TestData.Baseline();
            profile.Weapon(WeaponId.EnergyRifle).Levels[0].SpreadDeg = 0;
            var sim = SimTestUtil.NewSim(profile: profile);
            var w = sim.World;
            WeaponSystem.Give(w, WeaponId.EnergyRifle);
            sim.Step(new PlayerCommand { SelectWeaponSlot = 2, AimWorld = AimRight });
            sim.Run(12, Idle);
            var a = w.AddEnemy(EnemyId.H1_BatonGuard, new Vector2(3, 0));
            var b = w.AddEnemy(EnemyId.H1_BatonGuard, new Vector2(5, 0));
            var c = w.AddEnemy(EnemyId.H1_BatonGuard, new Vector2(7, 0));
            foreach (var e in new[] { a, b, c }) e.StunnedUntilTick = long.MaxValue;
            sim.Step(new PlayerCommand { FirePressed = true, AimWorld = AimRight });
            sim.Run(30, Idle);
            Assert.Less(a.Health, 1000f);
            Assert.Less(b.Health, 1000f);
            Assert.AreEqual(1000f, c.Health);
            float retained = w.Profile.Combat.PierceDamageRetained;
            Assert.AreEqual((1000f - a.Health) * retained, 1000f - b.Health, 1e-3f, "the target behind takes 75% of what remains (v0.19 §6)");
        }

        [Test]
        public void GeneralDamageSkillIsAppliedExactlyOnce()
        {
            var profile = TestData.Baseline();
            profile.Weapon(WeaponId.Pistol).Levels[0].SpreadDeg = 0;
            var sim = SimTestUtil.NewSim(profile: profile);
            var w = sim.World;
            w.Player.SkillRanks[(int)SkillId.WeaponDamage] = 3;
            var target = w.AddEnemy(EnemyId.H1_BatonGuard, new Vector2(4, 0));
            target.StunnedUntilTick = long.MaxValue;
            w.Drain();
            sim.Step(new PlayerCommand { FirePressed = true, AimWorld = AimRight });
            sim.Run(30, Idle);
            var hit = w.Drain().Single(e => e.Type == SimEventType.EnemyHit);
            Assert.AreEqual(18f * 1.24f, hit.Value, 1e-3f);
        }

        [Test]
        public void ReloadNeverGoesBelowTheFloor()
        {
            var profile = TestData.Baseline();
            profile.Skill(SkillId.ReloadMultiplier).Ranks[0] = 0.1f;
            var sim = SimTestUtil.NewSim(profile: profile);
            var w = sim.World;
            w.Player.SkillRanks[(int)SkillId.ReloadMultiplier] = 1;
            var l = w.Profile.Weapon(WeaponId.Pistol).Levels[0];
            Assert.AreEqual(0.45f, WeaponSystem.ReloadSeconds(w, l), 1e-5f);
            w.Player.SkillRanks[(int)SkillId.ReloadMultiplier] = 0;
            Assert.AreEqual(0.9f, WeaponSystem.ReloadSeconds(w, l), 1e-5f);
        }

        [Test]
        public void UpgradeKeepsCurrentRoundsClampedToTheNewMagazine()
        {
            var sim = WithSmg(out var smg);
            sim.Run(20, Hold);
            int left = smg.Ammo;
            WeaponSystem.SetLevel(sim.World, WeaponId.LightSmg, 2);
            Assert.AreEqual(left, smg.Ammo);
            sim.World.Profile.Weapon(WeaponId.LightSmg).Levels[1].Magazine = 5;
            WeaponSystem.SetLevel(sim.World, WeaponId.LightSmg, 2);
            Assert.AreEqual(5, smg.Ammo);
        }

        [Test]
        public void WallsStopPlayerShots()
        {
            var layout = SimTestUtil.OpenArena();
            int wallX = Mathf.FloorToInt(2 - layout.Origin.x);
            for (int y = 1; y < layout.Height - 1; y++) layout.BaseBlocked.Add(layout.CellIndex(wallX, y));
            var sim = SimTestUtil.NewSim(layout);
            var w = sim.World;
            var target = w.AddEnemy(EnemyId.H1_BatonGuard, new Vector2(5, 0));
            target.StunnedUntilTick = long.MaxValue;
            sim.Run(60, Hold);
            Assert.AreEqual(1000f, target.Health);
        }
    }
}
