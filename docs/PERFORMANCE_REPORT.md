# T21 — Bot playthrough, stress test and performance report

Date: 2026-10-17. Machine: Intel Core i7-8700 (6 cores / 12 threads, 3.2 GHz), NVIDIA GeForce GTX 1650 SUPER,
16 GB RAM, Windows 11 Pro. Unity 6000.6.0f1, URP 2D. Any figure that was not measured says **not measured**.

## 1. Full playthrough by a scripted bot (headless)

`FullRunTests.BotPlaysTheWholeGameWithoutSoftLocksRewardErrorsOrDrift` (EditMode, runs in every test pass):
the simulation with the real arena and baseline data, fixed seed 2026. The bot aims and fires at the nearest
enemy with real player commands; enemies alive longer than 4 s are removed so the run always advances; it
buys the tutorial SMG, takes the first card option, skips safe intervals and starts each boss at once.

| Check | Result |
|---|---|
| All 15 waves and 3 bosses completed | yes — 8.4 simulated minutes (30 120 ticks), 1.5 s of real time |
| Soft-locks / stuck encounters (none may last 8 simulated minutes) | none; longest encounter phase 3 wave 5, 35.3 s |
| Rewards per wave and per boss vs the balance calculator | all 18 encounters exact; totals XP 6352 = 6352, coins 5942 = 5942; 1097 kills |
| Save/load drift (saved through the real save format at 6 points, the loaded copy played beside the original for 900 ticks) | identical state hash at phase 1 wave 3, phase 1 boss, phase 2 wave 2, phase 2 boss, phase 3 wave 4, phase 3 boss |

Bug found by the bot: `CoinSystem.Collect` paid a coin again when two code paths reached the same coin in one
tick (the bot's phase-end sweep after its own pickup). The game's phase-end sweep only sees live coins, so
players were not affected, but `Collect` is now idempotent.

Encounter lengths (bot, simulated seconds): P1 19.1 / 24.2 / 25.6 / 27.3 / 30.3, boss 19.5 · P2 22.8 / 28.4 /
30.3 / 30.3 / 31.8, boss 26.5 · P3 24.5 / 31.5 / 33.0 / 32.8 / 35.3, boss 28.9. These say nothing about a
human's pace: the bot kills anything after 4 s.

The 1145-enemy ceiling of the GDD is a maximum including every possible offspring; the bot run killed 1097
(producers killed before they finished producing). The ceiling itself remains untested as a design number.

## 2. Worst-case stress (phase 3 wave 5: 164 enemies, active cap 42)

`StressRun` (dev builds only; `-hollowStress`): jumps to phase 3 wave 5, the player invulnerable and firing the
SMG at the nearest enemy, all effects at the High budget, damage numbers on, XP cards held back, the wave
restarted if it ends. 25 s warm-up (or until 38 enemies are alive), then 30 s measured with `FrameProbe`.

| Measurement | Editor Play mode | Windows development build | Windows release build |
|---|---|---|---|
| Enemies alive (avg / max) | 41.8 / 42 | 37.6 / 42 (VSync) · 40.6 / 42 (uncapped) | not measured |
| Frame time avg | 7.82 ms (128 fps) | 16.67 ms = 60 fps, VSync-locked · **1.65 ms (605 fps)** uncapped | not measured |
| Frame time p95 / p99 | 10.80 / 14.25 ms | 16.67 / 16.68 ms (VSync) · 2.44 / 3.11 ms uncapped | not measured |
| Worst frame | 149 ms (includes the screenshot call) | 16.69 ms (VSync) · 4.46 ms uncapped | not measured |
| GC allocation per frame in combat | 25 KB (includes Editor overhead) | 3.3 KB (VSync) · 2.7 KB uncapped | not measured |
| Gen-0 collections in 30 s | 5 | 5 at 60 fps · 43 at 605 fps (≈ one per 420 frames; no frame above 4.5 ms) | not measured |
| Memory peak (Unity total / managed heap) | 1227 MB / 122 MB (Editor process) | 185 MB / 9 MB | not measured |
| Resolution | 808×454 Game view | 1920×1080 windowed | — |

Development build runs: `Hollow.exe -hollowStress` (VSync on, the shipped setting) and `-hollowStress -uncapped`
(VSync off, to show headroom). Result: the worst case holds 60 fps with no frame above the VSync interval,
and uncapped it has more than 10× headroom on this machine.

Release build: the stress runner is dev-only by design (no dev code in release, §13), so release numbers are
**not measured**; a release build only removes development overhead, so it should not be slower.

Earlier 40-enemy SMG stress in phase 1 (T05, Editor): avg 7.3 ms, p95 9.9 ms.

## 3. Allocation work done

Measured per section of the game's frame loop (Editor, worst case, managed-heap deltas): simulation ≈ 0.16 KB
per frame, presentation ≈ 0, HUD and camera ≈ 2.1 KB → 1.5 KB after the change, game flow 0.19 KB → 0. The HUD
and the top line now rebuild their strings only when a shown value changes; what remains is the ammo counter,
which changes with every SMG shot. In the build the whole frame allocates 2.7–3.3 KB in the worst case (the
25 KB seen in the Editor is Editor overhead); garbage collection never produced a visible hitch (worst frame
4.46 ms uncapped).

## 4. Not measured

- Release-build frame rate (stress runner is dev-only).
- Any other machine, lower-end GPU or laptop; any resolution other than listed.
- Long sessions (memory over a full 30-60 minute human run).
- Loading times.
