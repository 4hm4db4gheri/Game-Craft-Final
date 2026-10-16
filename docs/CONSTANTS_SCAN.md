# Hard-coded gameplay constants — scan report (T18)

Acceptance (T18): every gameplay number reachable from the balance panel is read from data, verified by a
repository-wide search for hard-coded gameplay constants.

## Method

- Every numeric literal in `Assets/Scripts/Sim` (all gameplay rules live there, D40) and in `Assets/Scripts/Game`
  (game flow) was listed with a regex (`0`, `1`, `2` and `0.5` are ignored as arithmetic).
- Each hit was classified. Gameplay values were moved to `BalanceProfile`; everything else is justified below.
- The scan is now a test: `TelemetryAndApplyTests.NoUnlistedNumberLiteralsInTheSimulation` fails on any number
  literal in `Assets/Scripts/Sim` that is not on its allow list, so a new gameplay constant cannot slip in.
- `Assets/Scripts/Presentation` and `Assets/Scripts/UI` only draw. Their feel numbers that affect play or
  readability (shake, flashes, budgets, light per phase, camera framing) are already data (`Feedback`,
  `Atmosphere`, `Camera`); the rest are layout and styling.

## Result (2026-10-16)

56 literal sites in the simulation, 8 in game flow. **10 gameplay values were still in code and are now data**:

| Was | Now (`Baseline.json`) | Default |
|---|---|---|
| boss strike chosen within `StrikeRadius × 1.5` | `Bosses[].StrikeTriggerFactor` | 1.5 |
| boss stops `1.5` u beyond its radius | `Bosses[].ApproachStopDistance` | 1.5 |
| former-companion blast flight `BlastWarning × 0.6` | `Bosses[].BlastFlightFraction` | 0.6 |
| keep-away band `KeepAwayDistance × 1.6` | `Enemies[].KeepAwayMaxFactor` | 1.6 |
| supports return beyond `AuraRadius × 0.6` | `Enemies[].AuraStayFraction` | 0.6 |
| offspring spawn angle `140° + 80° × random`, gap `0.2` u | `Spawning.ChildSpawnBackAngle / ChildSpawnAngleSpread / ChildSpawnGap` | 140 / 80 / 0.2 |
| grenade flight `FuseTime × 0.8` | `Items[].FlightTimeFraction` | 0.8 |
| mine placement clearance `0.2` u | `Items[].PlacementRadius` | 0.2 |
| coin placement clearance `0.1` u | `Economy.CoinRadius` | 0.1 |
| phase ends at most `4` s after the boss while coins fly in | `Economy.PhaseEndCoinWait` | 4 |

Values are unchanged, so behaviour is identical (all tests pass).

**Left in code on purpose** (each is on the guard test's allow list):

| Kind | Examples | Why not data |
|---|---|---|
| Numeric safety | `1e-6f`, `1e-9`, `Max(0.01f, …)` | Epsilons and division guards, not tuning |
| Algorithms | 4 collision iterations, `B² − 4AC`, `1.0 / rate`, hash shifts, 8 spawn-point attempts | Part of the method, not of the design |
| List capacities | `new List<>(32)` | Memory pre-sizing only |
| Layout fallback | boss spawn `up × 8` | Only if an arena has no north spawn zone (the shipped one has) |
| Coin expiry blink | 6 Hz, 35 % duty | Visual timing of the warning blink |
| Structure | 3 save slots (§12), 3 phases | Fixed by the GDD, not tunable |
| Telemetry | 100 (slowest 1 % of frames) | Definition of a statistic |
| Game flow staging | shop-visit positions, 3 s status message, music fade times | Staging and UI, not balance |

## Apply rules (§13), checked by tests

- Wave queue, caps and enemy health are captured when a wave (or boss fight) starts; panel edits apply at the
  next start. Offspring and boss adds now use the captured health too.
- Prices are captured when the shop opens; edits apply at the next opening.
- Completed purchases are never rewritten (nothing is charged or refunded, owned levels stay).
