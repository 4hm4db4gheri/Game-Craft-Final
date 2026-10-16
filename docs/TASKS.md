# Hollow — Task plan and status

Statuses: `todo` · `in progress` · `done` (acceptance verified) · `partial` (see notes) · `blocked`.
Stories: `docs/USER_STORIES.md` (approved, D37). Decisions: `docs/DECISIONS.md`.

## Honest scope estimate

The full list (T00–T22) is several weeks of normal work for one programmer; a two-day jam cannot hold all of it with full verification. The order below follows GDD §16 and puts the balancing tools early because the user asked for them (D38). The **vertical slice** (phase-1 opening playable end to end: move, shoot, dash, wolves and quill shooters, coins, XP choice, shop, companion, save) is the first cut line. Breadth content (all 21 enemies, 10 weapons, 5 items, 3 bosses) comes after it; I report a real estimate for the rest when the slice is done.

## Architecture in one paragraph (details in DECISIONS.md D40–D47)

Gameplay is a **plain C# simulation** (`Hollow.Sim`) stepped at a fixed 60 Hz in active gameplay time, with its own circle-vs-grid collision, one seedable RNG and fully serialisable state. Unity objects only **render** it (pooled views with interpolation) and feed it **input commands**. This makes exact saves, deterministic replays, headless bot playthroughs and EditMode unit tests possible. All numbers come from one `BalanceProfile` edited by the same reflection-driven panel in the Editor and in-game (F9), with apply/undo/reset/import/export.

## Tasks

| # | Task | GDD | Status | Notes |
|---|---|---|---|---|
| T00 | Foundation: folders, asmdefs, scenes, state machine, two clocks, input router, docs | §1 §13 §16 | done | 144 tests; Windows build of the empty game: Succeeded, 0 errors (package warnings only), 133 MB |
| T01 | Data layer: BalanceProfile, seed values, calculator, validation, JSON/CSV, test profiles | §13 §8 §9 §12 | done | every GDD total reproduced by tests (no mismatch found); save snapshot part lands with T17 |
| T01b | Balance panel core (Editor window + in-game F9 panel, live edit, apply/undo/reset, import/export) | §13 | done | verified in Play mode (live edit + undo); T18 adds dev panel F10 and telemetry |
| T02 | Player: movement, aim, dash, health | §2 §4 §10 | done | 10 tests; greybox playable; dash bar near player |
| T06 | Enemy framework + A1 runner wolf (+ A2 quill shooter for the tutorial) | §5 §6 §14 | done | loop, rewards for all 21 types, armour angles, telegraph-before-attack, view rule; temporary sandbox spawner until T10 |
| T03 | Guns: pistol, SMG, double-barrel first | §4 §8 §10 | done | all 7 guns work from data; 12 tests |
| T05 | Game-feel pipeline (8 layers, budgets) | §14 §4 | todo | basic layers in slice, rest later |
| T08 | Coins, XP, skills | §9 §10 | done | 13 tests; XP card UI verified in Play mode (no click leak); pending-card save lands with T17 |
| T09 | Companion | §4 §3 | done | 12 tests; failure screen names the cause; retry = restart until T17 |
| T11 | Shop and inventory | §8 §9 §11 | todo | |
| T17 | Exact saves and death recovery | §12 | todo | sim designed for it from T00 |
| T10 | Spawning, waves, tutorial | §6 | todo | |
| T16a | AI-art import pipeline (name-driven importer, magenta key-out, strip slicing, visual configs) | §14 | todo | serves `ART_MASTER_BRIEF.md` |
| T15 | HUD and all screens | §10 §11 §15 | todo | minimal HUD in slice |
| — | **Vertical slice cut line** | | | |
| T04 | Melee weapons | §8 §10 | done (early) | done together with T03; 8 tests |
| T07 | Remaining 20 enemies | §5 | todo | |
| T12 | Items and combat inventory | §9 §10 | todo | |
| T13 | Bosses and preparation flow | §7 | todo | |
| T14 | Story, dialogue, transitions, ending | §3 §7 §14 §15 | todo | |
| T16 | Arena (3 states) and placeholder art | §14 | todo | |
| T18 | Balance panel completion, dev tools (F10), telemetry | §13 | todo | |
| T19 | Audio (CC0 SFX + music) | §14 | todo | |
| T20 | Tutorial pass and readability checklist | §6 §15 | todo | |
| T21 | Stress test, performance, bot playthrough | §6 §13 §16 | todo | |
| T22 | Final verification, Windows build, release notes | §16 | todo | |
