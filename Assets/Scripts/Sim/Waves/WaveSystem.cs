using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// Wave runner (GDD §6): batches of ~30/30/40 % with the exact total kept, next batch when the queue is
    /// exhausted and active enemies are at half the cap or lower, active/ranged/support caps, four edge zones
    /// with rotation that keeps the zone nearest the player free, safe distances, ~1 s spawn warning, a read
    /// pause for each newly introduced type, the first-wave tutorial and the wave completion bonus.
    /// </summary>
    public static class WaveSystem
    {
        const int SpawnAttemptsPerZone = 8;

        public static int WaveCount(World w) => w.Profile.WavesOfPhase(w.Phase).Count;

        public static void StartWave(World w, int wave)
        {
            var data = w.Profile.Wave(w.Phase, wave);
            var ws = w.Waves;
            var p = w.Profile;
            ws.Wave = wave;
            ws.Kind = EncounterKind.Wave;
            w.SafeInterval = false;

            var test = p.ActiveProfile();
            bool scale = test != null && !data.IsTutorial;
            var comp = scale ? Rounding.ScaleCounts(data.Composition, test.CountMultiplier) : new List<EnemyCount>(data.Composition);
            ws.ActiveCap = scale ? Rounding.ScaleCap(data.ActiveCap, test.ActiveCapMultiplier) : data.ActiveCap;
            ws.RangedCap = Mathf.Min(ws.ActiveCap, scale ? Rounding.ScaleCap(data.RangedCap, test.ActiveCapMultiplier) : data.RangedCap);
            ws.SpawnGap = data.SpawnGap * (scale ? test.SpawnGapMultiplier : 1f);

            ws.Queue.Clear();
            foreach (var c in comp) for (int i = 0; i < c.Count; i++) ws.Queue.Add(c.Id);
            if (data.IsTutorial)
            {
                // Tutorial: the first group (wolves) comes first, in order; the rest waits for the purchase.
                ws.Queue.Sort((a, b) => a.CompareTo(b));
                w.Tutorial = TutorialStage.FirstGroup;
                w.Progress.ChoicesBlocked = true;
            }
            else Shuffle(w, ws.Queue);

            ws.PlannedTotal = ws.Queue.Count;
            ws.BatchSizes = data.IsTutorial ? new List<int> { ws.Queue.Count } : new List<int>(Rounding.SplitByShares(ws.Queue.Count, p.Spawning.BatchShares));
            ws.CurrentBatch = 0;
            ws.SpawnedInBatch = 0;
            ws.SpawnedTotal = 0;
            ws.NextSpawnActiveTick = w.Clock.ActiveTicks;
            ws.BonusPaid = false;
            ws.HealthSnapshot.Clear();
            foreach (EnemyId id in System.Enum.GetValues(typeof(EnemyId))) ws.HealthSnapshot.Add(p.Enemy(id).Health);

            ShopSystem.RefreshStock(w);
            w.Emit(SimEventType.WaveStarted, 0, Vector2.zero, default, wave, w.Phase);
        }

        public static void Step(World w)
        {
            var ws = w.Waves;
            switch (ws.Kind)
            {
                case EncounterKind.Wave:
                    StepTutorial(w);
                    StepSpawning(w);
                    CheckWaveEnd(w);
                    break;
                case EncounterKind.SafeInterval:
                    if (w.Clock.Tick >= ws.SafeIntervalEndTick) StartNextEncounter(w);
                    break;
            }
        }

        /// <summary>Enter during the safe interval starts the next wave early (D30).</summary>
        public static void SkipSafeInterval(World w)
        {
            if (w.Waves.Kind == EncounterKind.SafeInterval) StartNextEncounter(w);
        }

        public static void StartNextEncounter(World w)
        {
            var ws = w.Waves;
            if (ws.Wave >= WaveCount(w))
            {
                ws.Kind = EncounterKind.BossPrep;
                w.SafeInterval = true;
                ShopSystem.RefreshStock(w);
                return;
            }
            StartWave(w, ws.Wave + 1);
        }

        public static float SafeIntervalRemaining(World w) =>
            Mathf.Max(0, w.Waves.SafeIntervalEndTick - w.Clock.Tick) * Core.SimClock.Dt;

        // ---------------------------------------------------------------- tutorial

        static bool IsTutorialWave(World w) => w.Profile.Wave(w.Phase, w.Waves.Wave).IsTutorial;

        static void StepTutorial(World w)
        {
            if (!IsTutorialWave(w)) return;
            var ws = w.Waves;
            if (w.Tutorial == TutorialStage.FirstGroup && ws.SpawnedTotal >= w.Profile.Spawning.TutorialFirstGroupCount && AliveCount(w) == 0)
                w.Tutorial = TutorialStage.Purchase;
            if (w.Tutorial == TutorialStage.SecondGroup) w.Progress.ChoicesBlocked = false;
        }

        // ---------------------------------------------------------------- spawning

        static void StepSpawning(World w)
        {
            var ws = w.Waves;
            if (ws.Queue.Count == 0) return;
            long now = w.Clock.ActiveTicks;
            if (now < ws.NextSpawnActiveTick) return;

            if (IsTutorialWave(w))
            {
                if (w.Tutorial == TutorialStage.Purchase) return;
                if (w.Tutorial == TutorialStage.FirstGroup && ws.SpawnedTotal >= w.Profile.Spawning.TutorialFirstGroupCount) return;
            }

            int active = AliveCount(w);
            if (active >= ws.ActiveCap) return; // the queue waits when the cap is full

            if (ws.SpawnedInBatch >= ws.BatchSizes[ws.CurrentBatch])
            {
                if (ws.CurrentBatch + 1 >= ws.BatchSizes.Count) return;
                if (active > ws.ActiveCap * w.Profile.Spawning.NextBatchAtCapFraction) return;
                ws.CurrentBatch++;
                ws.SpawnedInBatch = 0;
            }

            int index = PickEligible(w, now);
            if (index < 0) return;
            var type = ws.Queue[index];
            var data = w.Profile.Enemy(type);
            if (!FindSpawnPoint(w, data.BodyRadius, out Vector2 pos))
            {
                ws.NextSpawnActiveTick = now + System.Math.Max(1, w.Ticks(w.Profile.Spawning.NoSafePointRetryDelay));
                return;
            }

            var e = EnemySystem.Spawn(w, type, pos);
            if ((int)type < ws.HealthSnapshot.Count) e.Health = e.MaxHealth = ws.HealthSnapshot[(int)type];
            ws.Queue.RemoveAt(index);
            ws.SpawnedInBatch++;
            ws.SpawnedTotal++;
            ws.NextSpawnActiveTick = now + System.Math.Max(1, w.Ticks(ws.SpawnGap));

            if (!ws.Introduced.Contains(type))
            {
                ws.Introduced.Add(type);
                ws.HeldType = type;
                ws.HeldUntilActiveTick = now + w.Ticks(w.Profile.Spawning.NewTypeReadTime);
                if (data.CountsAsSupport) ws.LastSupportIntroActiveTick = now;
                if (data.CountsAsExplosive) ws.LastExplosiveIntroActiveTick = now;
            }
        }

        static int PickEligible(World w, long now)
        {
            var ws = w.Waves;
            CountRoles(w, out int ranged, out int support);
            long separation = w.Ticks(w.Profile.Spawning.SupportExplosiveSeparation);
            for (int i = 0; i < ws.Queue.Count; i++)
            {
                var t = ws.Queue[i];
                var d = w.Profile.Enemy(t);
                if (t == ws.HeldType && now < ws.HeldUntilActiveTick && ws.Introduced.Contains(t)) continue;
                if (d.CountsAsRanged && ranged >= ws.RangedCap) continue;
                if (d.CountsAsSupport && support >= w.Profile.Spawning.SupportCap) continue;
                if (!ws.Introduced.Contains(t))
                {
                    // Support and explosive introductions are kept apart (§6, wave four).
                    if (d.CountsAsSupport && now < ws.LastExplosiveIntroActiveTick + separation) continue;
                    if (d.CountsAsExplosive && now < ws.LastSupportIntroActiveTick + separation) continue;
                    // Also let the previous new type be read before introducing another.
                    if (now < ws.HeldUntilActiveTick) continue;
                }
                return i;
            }
            return -1;
        }

        static void CountRoles(World w, out int ranged, out int support)
        {
            ranged = support = 0;
            foreach (var e in w.Enemies)
            {
                if (!e.Alive) continue;
                var d = w.Profile.Enemy(e.Type);
                if (d.CountsAsRanged) ranged++;
                if (d.CountsAsSupport) support++;
            }
        }

        public static int AliveCount(World w)
        {
            int n = 0;
            foreach (var e in w.Enemies) if (e.Alive) n++;
            return n;
        }

        /// <summary>
        /// A safe point in a spawn zone: never in the zone nearest the player (escape route), never inside an
        /// obstacle or another enemy, at least N player-body widths from the player and M companion widths
        /// from the companion. Zones rotate.
        /// </summary>
        public static bool FindSpawnPoint(World w, float radius, out Vector2 point)
        {
            var zones = w.Layout.SpawnZones;
            point = default;
            if (zones.Count == 0) return false;

            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < zones.Count; i++)
            {
                float d = (zones[i].Center - w.Player.Position).sqrMagnitude;
                if (d < best) { best = d; nearest = i; }
            }

            var rules = w.Profile.Spawning;
            float minPlayer = rules.MinDistancePlayerBodyWidths * 2f * w.Profile.Player.BodyRadius;
            float minCompanion = rules.MinDistanceCompanionBodyWidths * 2f * w.Profile.Companion.BodyRadius;
            var ws = w.Waves;
            for (int z = 0; z < zones.Count; z++)
            {
                int zi = (ws.ZoneCursor + z) % zones.Count;
                if (zi == nearest && zones.Count > 1) continue;
                var zone = zones[zi];
                for (int attempt = 0; attempt < SpawnAttemptsPerZone; attempt++)
                {
                    var p = new Vector2(w.Rng.Range(zone.Min.x, zone.Max.x), w.Rng.Range(zone.Min.y, zone.Max.y));
                    if (!IsSafe(w, p, radius, minPlayer, minCompanion)) continue;
                    point = p;
                    ws.ZoneCursor = (zi + 1) % zones.Count;
                    return true;
                }
            }
            return false;
        }

        public static bool IsSafe(World w, Vector2 p, float radius, float minPlayer, float minCompanion)
        {
            if (w.Arena.CircleOverlaps(p, radius)) return false;
            if (Vector2.Distance(p, w.Player.Position) < minPlayer) return false;
            if (CompanionSystem.Active(w) && Vector2.Distance(p, w.Companion.Position) < minCompanion) return false;
            foreach (var e in w.Enemies)
                if (e.Alive && Vector2.Distance(p, e.Position) < radius + e.Radius) return false;
            return true;
        }

        // ---------------------------------------------------------------- wave end

        /// <summary>Ends when the base queue is spent, production has finished and no enemy is alive (§7).</summary>
        static void CheckWaveEnd(World w)
        {
            var ws = w.Waves;
            if (ws.Queue.Count > 0 || AliveCount(w) > 0) return;
            if (IsTutorialWave(w) && w.Tutorial != TutorialStage.SecondGroup && w.Tutorial != TutorialStage.None) return;

            if (!ws.BonusPaid)
            {
                int bonus = w.Profile.Wave(w.Phase, ws.Wave).CompletionBonus;
                w.Progress.Wallet += bonus;          // credited directly, ground coins are not auto-collected
                w.Progress.CoinsGenerated += bonus;
                w.Progress.CoinsCollected += bonus;
                ws.BonusPaid = true;
                w.Emit(SimEventType.WaveCompleted, 0, w.Player.Position, default, bonus, ws.Wave);
            }
            if (IsTutorialWave(w)) { w.Tutorial = TutorialStage.None; w.Progress.ChoicesBlocked = false; }
            ws.Kind = EncounterKind.SafeInterval;
            w.SafeInterval = true;
            ws.SafeIntervalEndTick = w.Clock.Tick + w.Ticks(w.Profile.Spawning.SafeIntervalSeconds);
        }

        static void Shuffle(World w, List<EnemyId> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = w.Rng.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
