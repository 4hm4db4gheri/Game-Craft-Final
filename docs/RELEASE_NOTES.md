# Hollow — Release notes (jam build)

Build: Windows x64 release, clean (output folder deleted first), `Builds/Windows/Hollow.exe` — **Succeeded, 0 errors**, 132.8 MB, 63 s. The 496 build warnings are all third-party: 491 shader warnings from the AI Inference (Sentis) package that came with the template and is not used by the game, 5 from the Editor's account service; none from game code. The dev panel was removed from the release scene at build time (build log: "Release build: removed DevPanel"). A development build (`Builds/WindowsDev`) was used for the stress test.
Tests: 398 EditMode tests, 398 passed (includes the full-game bot, arena, readability, telemetry, apply-rule and constants-guard tests).
Date: 2026-10-17. Machine used for every measurement: i7-8700, GTX 1650 SUPER, 16 GB RAM, Windows 11 Pro.

This document is honest by design (T22): nothing here claims that balance or feel has been validated —
there has been **no human playtest** and no measurement on another machine.

## What works (verified by tests and/or in Play mode)

- **Whole game, start to finish**: main menu, three save slots, opening scene, 15 waves and 3 bosses across
  three phases, boss preparation with shop visit, phase transitions with story scenes, the dark ending with
  the cheerful jingle and silence, results screen. A scripted bot plays all of it with a fixed seed: no
  soft-locks, every wave and boss pays exactly the calculated XP and coins, saves restore the exact state
  (`docs/PERFORMANCE_REPORT.md`).
- **Combat**: movement independent of aim, dash (through enemies, invulnerable, never through walls), 10
  weapons (7 guns, 3 melee) from data, reload rules, 24 enemy types with telegraphs before every attack,
  producers with fixed reward pools, 3 bosses with patterns, threshold adds, heal cap and EMP rules.
- **Economy and progression**: coins on the ground with magnet and optional lifetime, XP cards with a full
  pause and before → after values, live shop (never pauses, clicks never fire a shot), 5 items with previews,
  healing purchases.
- **Companion**: follows, fights, can die (run fails with the cause named), off-screen danger arrow and
  warning sound, weaker each phase, the former companion as final boss.
- **Saves**: exact save/load, checkpoints at wave and boss start, atomic writes, slots independent.
- **Arena**: one 48 × 32 arena in three states (ground, decor, named props), escape routes guaranteed by tests,
  phase 3 darker but readable, fully dark ending.
- **Feel**: muzzle flashes, sparks, hit flash and squash, corpses and decals (capped), damage numbers
  (rate-limited, toggle), camera shake (slider), effect budget setting; warnings always drawn on top.
- **Audio**: 46 cues with CC0 files (Kenney + OpenGameArt, `docs/CREDITS.md`), music per phase, warnings
  never ducked.
- **Tools**: balance panel (Editor window and F9 in-game) with apply / undo / group reset / import / export,
  live edits apply by the §13 rules; dev panel F10 (start any wave or boss, starting state, coins, XP, seed,
  god mode) — removed from release builds; telemetry JSON per run; AI-art importer
  (`docs/ART_MASTER_BRIEF.md`).

## What is placeholder

- **All visuals** are generated shapes (`PH_*` prefabs) waiting for the AI art; the importer swaps them in.
- **Weapon levels 2 and 3** use my placeholder numbers (`Approved = false`): they are sold only in the Editor
  and development builds. Level-1 values are the GDD's provisional numbers.
- Every number marked `[Placeholder]` in `BalanceProfile` (feel, atmosphere, AI tuning, item details) is my
  reasonable default, not a designed value.
- Sound choices were made by file name and length, not by listening.
- UI styling is the placeholder theme; the font is Liberation Sans.

## What is untested

- **No human playtest at all**: feel, difficulty, pacing, readability in real crowds, whether the tutorial
  teaches without help, whether the story lands (`docs/T20_CHECKLIST.md`, 4 of 7 criteria need a human).
- **Release-build frame rate** (the stress runner exists only in development builds), other machines, lower-end
  GPUs, laptops, long sessions, loading times.
- The build on a second machine or a clean Windows profile: **not tested** — no second machine or clean Windows profile was available. The release build was launched on this machine: it reaches the main menu (`docs/screenshots/T22_release_menu.png`), keeps responding, and its log has no errors (one harmless Direct3D 12 debug-layer message).
- The 1145-enemy ceiling as a design number (the bot run killed 1097; producers die early).

## Known issues

- TextMesh Pro's dynamic font assets are modified when the game runs in the Editor; revert them before
  committing (`git checkout -- "Assets/TextMesh Pro/Resources/Fonts & Materials/"`).
- In the Editor the profiler shows about 25 KB of managed allocation per frame in the worst case; the game's
  own loop allocates about 1.7 KB (mostly the ammo counter text). See the performance report for the build
  figure.
- The human enemy placeholders are all capsules told apart mainly by colour (weakest silhouettes).
- The dev bot skips content; its encounter lengths say nothing about a human's pace.
- The window title and the save folder use the Unity product name "Game Craft Final" (`%USERPROFILE%/AppData/LocalLow/DefaultCompany/Game Craft Final`). Renaming the product to "Hollow" would move that folder; it was left as is.
- Games started directly from the Game scene (Editor tests) save to a separate `dev_saves` folder since T21; before that, my test runs saved into slot 1 of the player save folder.

## User stories — review (docs/USER_STORIES.md)

"Met" = every testable criterion is covered by an automated test or a recorded Play-mode check; "(human)"
criteria are listed separately and are **not** claimed.

| Story | Testable criteria | Human criteria |
|---|---|---|
| T00 Foundation | met — state machine, clock and input-router tests | — |
| T01 Data layer | met — calculator reproduces every GDD total; unapproved levels flagged; validator rules tested | — |
| T02 Player | met — `PlayerTests` (diagonal, dash distance/time, walls, one hit per enemy, invulnerability never blocks input) | "feels responsive": not tested |
| T03 Guns | met — `WeaponTests` | — |
| T04 Melee | met — `MeleeTests` | — |
| T05 Feel | met — all layers for pistol, SMG, shotgun; budget caps; telegraphs on top (test); 40-enemy and worst-case stress measured | "feels good": not tested |
| T06 Enemy framework | met — `EnemyTests`, `EnemyRoleTests.EveryDamagingEnemyTelegraphsFirst` | — |
| T07 20 enemies | met — producer pools exact in 12 kill orders; per-role tests | — |
| T08 Coins, XP, skills | met — `XpAndCoinTests`, pending card saved | can explain a change: not tested |
| T09 Companion | met — `CompanionTests`, failure screen names the cause | understands warnings: not tested |
| T10 Waves | met — `WaveTests`, full-game bot never sticks | — |
| T11 Shop | met — `ShopTests`; Buy click never fires (Play mode, real input) | — |
| T12 Items | met — `ItemTests` | — |
| T13 Bosses | met — `BossTests`; reward pools exact in the full-game bot | — |
| T14 Story | met — scenes and ending played in Play mode, skippable on retry | theme recognised: not tested |
| T15 HUD & screens | met — every screen reached in Play mode; 1920×1080, 1600×900, 1280×720 checked | — |
| T16 Arena | met — screenshots, `ArenaLayoutTests` (escape routes, transition push-out) | — |
| T17 Saves | met — `SaveTests` (hash and 1200-tick future identical, slots independent, atomic write, failure shown) | — |
| T18 Panel & telemetry | met — scan report + guard test, dev panel stripped from release (build log), telemetry file per run | — |
| T19 Audio | met — every cue played in Play mode, warnings bus never ducked, CREDITS complete, 5.5 MB | sounds fit: not tested |
| T20 Tutorial & readability | met — `docs/T20_CHECKLIST.md` | 4 of 7 §15 criteria need a human |
| T21 Stress & bot | met — `docs/PERFORMANCE_REPORT.md` | — |
| T22 Build | met — see the build and test lines at the top | — |

## Open questions and assumed decisions

All 74 questions in `docs/QUESTIONS.md` were answered (most with my recommendation). These decisions are my
defaults and are still **assumed, awaiting confirmation** (`docs/DECISIONS.md`):

- D6 Cinemachine for the follow camera, confiner and shake.
- D22 (part) the pistol's advantage includes "reload not cancelled by dash".
- D40–D48 architecture (plain C# simulation, own collision, one RNG, JSON balance data, panel design, input
  routing, save format, assemblies, F9 panel availability).
- D49–D59 my defaults for arena props, layout rules, atmosphere values, feedback pipeline, sorting layers,
  audio mixing, telemetry format, apply rules and tutorial hints.

## To-do for the designer

1. **Weapon levels 2 and 3** for all ten weapons (`docs/WeaponLevels_Template.csv`, then set `Approved`).
2. Play the whole game and collect telemetry (`%USERPROFILE%/AppData/LocalLow/DefaultCompany/Game Craft Final/
   telemetry`): wave and boss durations, deaths and causes, damage to player and companion, first-purchase
   time, coins generated vs collected, chosen skills, weapons and levels, item use, healing spend. Compare the
   observed numbers with the calculated ones stored beside them.
3. Tune from those observations with the balance panel (F9 in-game or Hollow > Balance Panel): wave
   compositions and caps, enemy health, prices, XP thresholds, boss patterns, coin lifetime.
4. Confirm or change the assumed decisions above.
5. Do the human checks in `docs/T20_CHECKLIST.md` (tutorial without help, upgrade understanding, companion
   warnings, phase relief, warnings in big crowds, enemy recognition, the story's point).
6. Produce the AI art with `docs/ART_MASTER_BRIEF.md` / `ART_ASSET_LIST.md` and check silhouettes again.
7. Listen to every sound cue and replace any that does not fit (`docs/CREDITS.md` lists the files).
