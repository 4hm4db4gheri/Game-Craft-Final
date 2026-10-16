using System.Collections.Generic;
using UnityEngine;

namespace Hollow.Sim
{
    public sealed class CoinState
    {
        public int Id;
        public Vector2 Position;
        public Vector2 PrevPosition;
        public int Value = 1;
        /// <summary>Active-seconds tick when the coin dropped (lifetime runs on active time, G4).</summary>
        public long DroppedActiveTick;
        /// <summary>Tutorial coins never expire (GDD §6).</summary>
        public bool NeverExpires;
        public bool Magnetized;
        public bool Alive = true;
    }

    /// <summary>Wallet, XP and run statistics.</summary>
    public sealed class ProgressState
    {
        public int Wallet;
        public int TotalXp;
        /// <summary>XP gathered toward the next skill threshold (overflow is kept, GDD §9).</summary>
        public int XpTowardNext;
        public int ChoicesTaken;
        /// <summary>The rolled XP card waiting for the player, if any (saved exactly).</summary>
        public PendingChoice Pending;
        /// <summary>Tutorial gate: no XP card may overlap the first shop explanation (§6).</summary>
        public bool ChoicesBlocked;
        public List<Hollow.Data.SkillId> ChosenSkills = new();
        public int CoinsGenerated;
        public int CoinsCollected;
        public int Kills;
    }
}
