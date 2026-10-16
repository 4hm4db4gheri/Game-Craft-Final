using System;
using System.Collections.Generic;

namespace Hollow.Core
{
    public enum GameState
    {
        Boot,
        MainMenu,
        Loading,
        Playing,
        XPChoice,
        Paused,
        Scene,
        BossPrep,
        Failure,
        PhaseTransition,
        Ending
    }

    /// <summary>
    /// Global game-state machine with an explicit table of legal transitions.
    /// Paused remembers the state it interrupted so Resume returns there.
    /// </summary>
    public sealed class GameStateMachine
    {
        static readonly Dictionary<GameState, GameState[]> Allowed = new()
        {
            [GameState.Boot] = new[] { GameState.MainMenu },
            [GameState.MainMenu] = new[] { GameState.Loading },
            [GameState.Loading] = new[] { GameState.Playing, GameState.XPChoice, GameState.BossPrep, GameState.Scene, GameState.Ending, GameState.MainMenu },
            [GameState.Playing] = new[] { GameState.XPChoice, GameState.Paused, GameState.Scene, GameState.BossPrep, GameState.Failure, GameState.PhaseTransition, GameState.Ending },
            [GameState.XPChoice] = new[] { GameState.Playing, GameState.Paused },
            [GameState.Paused] = new[] { GameState.Playing, GameState.XPChoice, GameState.BossPrep, GameState.Loading, GameState.MainMenu },
            [GameState.Scene] = new[] { GameState.Playing, GameState.PhaseTransition, GameState.Ending },
            [GameState.BossPrep] = new[] { GameState.Playing, GameState.Scene, GameState.Paused },
            [GameState.Failure] = new[] { GameState.Loading, GameState.MainMenu },
            [GameState.PhaseTransition] = new[] { GameState.Playing, GameState.Scene },
            [GameState.Ending] = new[] { GameState.MainMenu, GameState.Loading },
        };

        public GameState Current { get; private set; }
        public GameState Previous { get; private set; }

        /// <summary>The state Paused will resume into.</summary>
        public GameState ResumeState { get; private set; } = GameState.Playing;

        public event Action<GameState, GameState> Changed;

        public GameStateMachine(GameState initial = GameState.Boot)
        {
            Current = initial;
            Previous = initial;
        }

        public static bool IsAllowed(GameState from, GameState to)
        {
            return Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;
        }

        public static IEnumerable<GameState> AllowedTargets(GameState from)
        {
            return Allowed.TryGetValue(from, out var targets) ? targets : Array.Empty<GameState>();
        }

        public bool CanTransition(GameState to)
        {
            if (!IsAllowed(Current, to)) return false;
            // Leaving Paused back into gameplay must return to the interrupted state.
            if (Current == GameState.Paused && IsResumable(to) && to != ResumeState) return false;
            return true;
        }

        public bool TryTransition(GameState to)
        {
            if (!CanTransition(to)) return false;
            if (to == GameState.Paused) ResumeState = Current;
            Previous = Current;
            Current = to;
            Changed?.Invoke(Previous, Current);
            return true;
        }

        public void Transition(GameState to)
        {
            if (!TryTransition(to))
                throw new InvalidOperationException($"Illegal game-state transition {Current} -> {to}");
        }

        public bool Resume()
        {
            return Current == GameState.Paused && TryTransition(ResumeState);
        }

        static bool IsResumable(GameState s) =>
            s == GameState.Playing || s == GameState.XPChoice || s == GameState.BossPrep;
    }
}
