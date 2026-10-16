using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// Entering a phase: switch the obstacle layout, push characters out of any new obstacle (§14: never
    /// leave anyone inside one), restore or remove the companion, and start wave one.
    /// The transition scene itself is presentation (T14).
    /// </summary>
    public static class PhaseSystem
    {
        public static void EnterPhase(World w, int phase)
        {
            w.Phase = phase;
            ItemSystem.ClearMines(w);
            w.Arena = ArenaGrid.FromLayout(w.Layout, phase);
            w.PlayerField = new FlowField(w.Arena);
            w.CompanionField = new FlowField(w.Arena);

            var p = w.Player;
            p.Position = p.PrevPosition = w.Arena.NearestFree(p.Position, w.Profile.Player.BodyRadius);
            foreach (var e in w.Enemies) e.Position = e.PrevPosition = w.Arena.NearestFree(e.Position, e.Radius);
            foreach (var coin in w.Coins) coin.Position = coin.PrevPosition = w.Arena.NearestFree(coin.Position, 0.1f);

            Vector2 companionAt = w.Companion.Present ? w.Companion.Position : w.Layout.CompanionStart;
            CompanionSystem.SpawnForPhase(w, w.Arena.NearestFree(companionAt, w.Profile.Companion.BodyRadius));

            w.Waves.Wave = 0;
            WaveSystem.StartWave(w, 1);
        }
    }
}
