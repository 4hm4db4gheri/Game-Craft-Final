# T20 — Tutorial and readability checklist

Verdicts: **pass** (checked by me, how is named), **fail**, **needs human test** (depends on what a person
perceives or understands; never marked passed without a playtest). Date: 2026-10-16. Tests: `Assets/Tests/EditMode`.

## Opening / tutorial (§6, §15 "Understand the start")

What the opening shows, without voice:
1. The opening dialogue (Enter / click to continue).
2. Wave 1 starts with four wolves only (cap 3 alive); the status line reads
   *"WASD to move · aim with the mouse · hold left click to fire · Space to dash"*.
3. When the wolves are dead: *"Collect the coins they dropped"* (eight tutorial coins that never expire; no XP
   card can interrupt this step).
4. Once the coins are collected: *"Follow the arrow to the shop and buy the SMG"* and a cyan arrow points to the
   shop (from the screen edge when the shop is off screen, bobbing above it when visible) — added in T20.
5. Near the shop: *"Press E to open the shop"*; the shop sells only the SMG at 8 coins until it is bought.
6. After the purchase two quill shooters arrive; the tutorial ends with the wave.

**Scripted run with real input only** (keys and mouse sent to the Input System, no dev shortcuts), 2026-10-17:
Enter through the opening → the four wolves killed by aiming the mouse and holding the button → walking with
WASD over the eight coins (wallet 8) → the arrow appeared over the shop (`docs/screenshots/T20_guide_arrow.png`)
→ "Press E" near the shop → E opened it with only the SMG for sale (`T20_tutorial_shop.png`) → a click on
"Buy Light SMG" → wallet 0, tutorial moved on to the quill shooters. Result: the opening can be completed
from the on-screen prompts alone.

Found and fixed during the run: (a) the shop was only described as "south" — now an arrow leads there;
(b) while the shop is open the hint says which button to click; (c) a bought weapon goes to slot 2 while the
pistol stays in hand, which nothing explained — the hint "Press 2 to use the Light SMG" now shows until the
player switches.

## §15 acceptance criteria

| # | Player need | Criterion | What was checked | Verdict |
|---|---|---|---|---|
| 1 | Understand the start | Complete movement, firing and first purchase without spoken guidance | Scripted run with real keys and mouse only (above); `WaveTests.TutorialRunsWolvesThenPurchaseThenQuills`, `ShopTests.TutorialSellsOnlyTheSmgAtEightUntilBought`, `XpAndCoinTests.TutorialGateHoldsTheCard`. Whether a new player *understands* it without help can only be seen in a playtest. | **needs human test** (flow completable: pass) |
| 2 | Responsive control | Animation does not obstruct direction changes, dash or aim | Animation is presentation only and never gates input (D40); `PlayerTests`: full speed quickly and short stop, diagonal not faster, dash follows movement not aim, post-hit invulnerability never blocks input, aim independent of movement. | **pass** (mechanics) · feel: **needs human test** |
| 3 | Understand upgrades | Explain a skill or weapon-level change from the before-and-after display | XP card shows rank *n → n+1* and value *before → after*; shop upgrade rows show *Lv n → n+1* and every changed stat *old→new*. Whether players can explain the change: playtest. | display: **pass** · understanding: **needs human test** |
| 4 | Rescue the companion | Understand directional warnings and identify companion death causes | Screen-edge danger arrow when the Operator is hurt off screen, red marker above him, rate-limited warning sound that is louder off screen (T19); failure screen names the cause ("The Operator is down" vs player death). `CompanionTests.CompanionDeathFailsTheEncounter`, `PlayerDeathFailsTheEncounterWithItsOwnReason`. | signals: **pass** · understanding: **needs human test** |
| 5 | Live shopping | Shop quickly with no unintended shot or item use and no world pause | `ShopTests.ShoppingNeverPausesTheWorld`, `EOpensInRangeAndDashOrLeavingCloses`; input router latches UI clicks (G5) — verified in Play mode (T11: a Buy click never fires a shot). | **pass** |
| 6 | Exact continuation | After full exit, each slot restores the exact moment without changing others | `SaveTests.SaveQuitLoadRestoresTheExactStateAndIdenticalFuture` (same hash and identical 1200-tick future after reading from disk), `SlotsAreIndependent`, `PendingCardIsRestoredWithoutReroll`; verified in Play mode (T17). | **pass** |
| 7 | Understand Hollow | Recognize AI empowerment and the human loss of function without designer explanation | Story beats exist (opening, phase transitions with the Operator's decline, former companion as final boss, dark ending with jingle then silence). Recognition is purely a perception question. | **needs human test** |

## Readability rules (§5, §6, §14)

| Rule | What was checked | Verdict |
|---|---|---|
| Relief at phase entry | `ReadabilityTests.EachPhaseStartsWithASmallerCrowdThanThePreviousPeak`: robot entry 18 enemies / cap 8 after the animal peak 96 / cap 24; human entry 24 / cap 10 after the robot peak 130 / cap 32. | **pass** (numbers) · felt relief: **needs human test** |
| Telegraphs visible at the largest crowd | `ReadabilityTests.EveryTelegraphIsDrawnAboveAllEffects` (every enemy and boss warning part, and enemy shots, sit on the top `Telegraphs` sorting layer above `Effects`); `EnemyRoleTests.EveryDamagingEnemyTelegraphsFirst` for all attackers; 40-enemy stress screenshot `docs/screenshots/T05_stress.png`; phase-3 darkness never dims warnings (unlit, D52). | **pass** (drawn and on top) · readable in a real crowd: **needs human test** |
| Distinct silhouettes | `ReadabilityTests.NoTwoEnemiesOfAPhaseShareAShape` (shape, size and colour differ within each phase); contact sheet `docs/screenshots/T20_enemies.png`. Note from the sheet: the human faction placeholders are all capsules told apart mainly by colour and the carried piece — the weakest group. Placeholders only: the final AI art must be checked again. | proxy: **pass** · at a glance: **needs human test** |
| Escape routes | `ArenaLayoutTests` (T16): no closed pockets, no one-cell slots, every spawn zone open with a 2-wide route, starts and shop clear. | **pass** |

## HUD at other resolutions (left over from T15)

Captured the HUD, shop and XP card at 1280×720, 1600×900 and 1920×1080 (`docs/screenshots/T20_*`). The UI
scales with the screen; no element leaves the screen. Two fixes: the XP counter sat under the item buttons at
1280×720 (moved beside the XP bar) and the shop's detail lines were very small (font 14 → 16). Whether text
is comfortable to read on a real monitor: **needs human test**.

## For the human playtest

Please check and note: (1) a first-time player gets through the opening without help; (2) movement, dash and
aim feel immediate; (3) the player can say what an upgrade changed; (4) the player notices the Operator being
hurt off screen and knows why a run failed; (5) phase starts feel like a breather; (6) warnings stay readable
in the biggest fights, also in phase 3; (7) enemies are told apart at a glance (again after the AI art);
(8) the player understands the story's point (AI gains, human loses) without explanation.
