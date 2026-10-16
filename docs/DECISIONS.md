# Hollow — Decision log

Every decision with its source: **User** (answer in chat), **GDD §N** (recorded decision in the GDD), or **Default** (my assumption, logged as "assumed, awaiting confirmation" until confirmed). "User: Rec" means the user accepted my recommended default from `QUESTIONS.md`.

Answers given 2026-10-16. The user accepted every recommended default except where a row says otherwise.

## Project and process

| ID | Decision | Source |
|---|---|---|
| D1 | The game is **2D**; all final art is 2D. No 3D scene. | User |
| D2 | Stay in the existing 2D URP project (2D Renderer, Sprite, Tilemap). Gameplay on the XY plane with Physics2D. | User (Q2) |
| D3 | Reference GDD: `Assets/GDD/Hollow_Game_Design_EN.docx` (same file, same hash as the one converted to `docs/GDD.md`). The FA docx and the prototype zip are ignored. | User (Q8) |
| D4 | Delete `Assets/Welcome/` (Unity template content) and `SampleScene`; scenes are Boot, MainMenu, Game. | User (Q3 + note 4) |
| D5 | Pathfinding: own **flow field on the arena grid** + local steering. The AI Navigation package the user installed stays installed but is not used. | User (Q5: Rec) |
| D6 | Camera: the user installed **Cinemachine 6.6**, so the follow camera, arena confiner and shake use Cinemachine instead of a custom camera (my Q4 recommendation assumed it was absent). | Default — assumed, awaiting confirmation |
| D7 | One commit per task on `main`; never push unless asked. docs/ in English, `agent.md` in Persian. | User (Q6, Q8b: Rec) |
| D8 | I may drive the open Editor through the `unity` CLI. | User (Q7: Rec) |

## View, art and arena

| ID | Decision | Source |
|---|---|---|
| D10 | View and movement **like video A**: 3/4 top-down (not a diamond isometric grid), rectangular Tilemap, W moves up on screen. | User (Q9, Q11, note 1) |
| D11 | **Not pixel art.** Art is AI-generated hi-res 2D. Authoring scale **128 px per game unit**; orthographic camera size 7.5 → about 26.7 × 15 units visible at 16:9. | User (Q10) + Default for the numbers |
| D12 | Camera follows the player with a small look-ahead toward the mouse, clamped to the arena. | User (Q11) |
| D13 | Arena **48 × 32 units** on a rectangular Tilemap (1 unit cells): ground, decoration, and a separate invisible **collision Tilemap** that the designer paints; four edge spawn gates; shop south of centre; one obstacle layout per phase. | User (Q12) |
| D14 | Placeholders: Unity built-in 2D sprites + faction palettes; telegraphs drawn procedurally in a reserved red-orange that art never uses. | User (Q13: Rec) |
| D15 | Every visual sits behind a per-entity visual config so AI art replaces placeholders without code changes. AI art follows `docs/ART_MASTER_BRIEF.md`. | User (note 2) |
| D16 | Sorting by feet (custom transparency sort axis Y). All gameplay collision uses a ground circle at the feet; sprite size never changes a hitbox. | User (Q72: Rec) |
| D17 | Every obstacle blocks movement and shots. | User (Q73: Rec) |
| D18 | Atmosphere via URP 2D lights + generated fog overlay. Corpses and decals persist (capped) and are cosmetic, not saved exactly. | User (Q74, Q15: Rec) |

## Controls and combat

| ID | Decision | Source |
|---|---|---|
| D20 | G cycle, right-click cancel, click-to-use, "place in first slot" as proposed; melee in the primary slot; hold left click = continuous fire for every weapon; field swap only pistol ↔ primary; keyboard + mouse only; one dash charge; dash passes through and damages bosses once. | User (Q16–Q20: Rec) |
| D21 | Weapon levels 2–3: CSV template + my placeholder values marked `approved=false`, buyable only in editor/dev builds. | User (Q21: Rec; "write a sensible default for the guns") |
| D22 | **Pistol swap stays 0.15 s** (no instant swap). Pistol advantage = its reload is not cancelled by dash, plus its low spread. | User (Q22) — the dash part is Default, assumed, awaiting confirmation |
| D23 | Guns fire fast visible pooled projectiles; shotgun falloff linear 100 % → 50 %; energy-shotgun pellets each hit two; obstacles block shots and melee; upgrades keep the current magazine; frontal armour = 120° cone, applies to everything from the front, energy blade ignores it. | User (Q23–Q28: Rec) |
| D24 | All missing enemy, child and boss numbers are filled by me with reasonable values flagged `PLACEHOLDER` in data. | User (Q29) |
| D25 | Enemy rules: no regular enemy outruns the player except during telegraphed charges; damage only via telegraphed attacks; opportunistic companion targeting rule as in Q32; medics share the 10 % boss-healing cap; EMP on the human boss = 0.8 s interrupt + 6 s resistance. | User (Q30–Q34: Rec) |
| D26 | Boss own reward = pool − add shares (100/100, 180/180, 260/260); at boss death all ground coins are pulled to the player before the transition. | User (Q35) |

## Waves, economy, story, saves, feel, delivery

| ID | Decision | Source |
|---|---|---|
| D30 | Between waves: 8 s safe interval (data) with countdown; coin lifetime frozen; Enter starts early. | User (Q36) |
| D31 | Tutorial: cap 3 kept, no batching, profile multipliers ignored, shop shown with arrow + one-line prompt; spawn never from the gate nearest the player; 4 s read time for a new type; multipliers don't change children. | User (Q37–Q42: Rec) |
| D32 | Wave bonuses are coins; coin lifetime on (60 s + 5 s blink); XP card pops over the shop; "per encounter" = per wave or boss fight; grenade explodes 0.8 s after throw at the clamped cursor; mines hit bosses; push pulse ignores projectiles. | User (Q43–Q49: Rec) |
| D33 | Companion, names, dialogue box, scene skipping, phase-two warning line, no extra final-boss story mechanics, ~15 s skippable ending montage as recommended. | User (Q50–Q55: Rec) |
| D34 | JSON saves in `persistentDataPath/saves/`, atomic write, save disabled during short scenes; Esc on XP card opens pause; failure screen then retry; playtime excludes pause menus; ending stats as listed; English only; settings incl. damage numbers and effect budget. | User (Q56–Q61: Rec) |
| D35 | Damage numbers on by default; burning visual only; 60 fps target, measured not claimed; one free music loop per phase; Kenney CC0 first for SFX; no timer or kill counter. | User (Q62–Q67: Rec) |
| D36 | Windows x64 build only in `Builds/Windows/`; provisional GDD values = initial profile; test profiles dev-only; vertical slice T00–T09 first, then a real estimate. | User (Q68–Q70: Rec) |
| D37 | User stories in `docs/USER_STORIES.md` approved as drafted. | User (Q71: Rec) |
| D38 | Balancing tools are a priority: every hand-tunable number must be editable from an in-game panel **during play** as well as in the editor. | User (note 3) |

## Architecture (my defaults — assumed, awaiting confirmation)

| ID | Decision | Why |
|---|---|---|
| D40 | Gameplay runs in a **plain C# simulation** (`Hollow.Sim`) at a fixed 60 Hz tick in active gameplay time; MonoBehaviours only render and send input. | Exact save/load and "identical outcome after load" (§12, G6) are not achievable with Unity physics state; pure C# is unit-testable and lets a bot play the whole game headless (T21). |
| D41 | Own collision: circles for bodies, a 1-unit blocker grid (painted collision Tilemap) for obstacles, segment tests for projectiles, a spatial hash for crowds. Physics2D is not used for gameplay. | Deterministic, serialisable, cheap at 42+ enemies and hundreds of projectiles (G7). |
| D42 | One `SimRandom` (xorshift128+, state saved) for all gameplay randomness. Cosmetic effects use a separate unsaved random. | G6. |
| D43 | Data: `BalanceProfile` plain serialisable classes stored as **JSON** in `Assets/Resources/Balance/Baseline.json` (no ScriptableObject: JSON is the one format shared by the Editor window, the in-game panel, import/export and save snapshots). Newtonsoft JSON (added to the manifest). CSV for weapon levels (`docs/WeaponLevels_Template.csv`). The file was first generated by the Editor-only data seed `Assets/Editor/BaselineSeed.cs` (GDD v0.12 values + placeholders); since then the JSON is the source of truth. | One source for editor, runtime panel and saves (§13, §12). |
| D44 | Balance panel = one reflection-driven IMGUI panel used both in an Editor window and in-game (F9, also in builds). Edits go to a draft; Apply pushes it live; undo stack of snapshots; group reset to baseline. Systems that must not change mid-wave (queues, spawned health, purchase records) read values at their natural moment (wave start, spawn, shop opening). | D38, §13 apply rules. |
| D45 | Input: Input System actions → `InputRouter` builds one `PlayerCommand` per tick; UI-owned clicks are latched until release so they never reach the world. Commands can be recorded/replayed. | G5, T21. |
| D46 | Saves: Newtonsoft JSON of the whole sim state, written to a temp file then replaced, read back and verified before reporting success. | §12. |
| D47 | Assemblies: `Hollow.Core` (state machine, clocks, RNG, events), `Hollow.Data`, `Hollow.Sim`, `Hollow.Presentation` (views, feel, audio, camera), `Hollow.UI`, `Hollow.App` (bootstrap, save IO), `Hollow.Dev` (panels), `Hollow.Editor`, `Hollow.Tests.EditMode`, `Hollow.Tests.PlayMode`. | G8. |
| D48 | The balance panel is in the Editor (Hollow > Balance Panel, Ctrl+Shift+B) and in-game on F9 in the Editor and development builds (release builds only with the HOLLOW_BALANCE_PANEL define). In-game "Save as start profile" writes persistentDataPath/balance/active.json, which builds load at start; in the Editor, Save writes Baseline.json. | Default — assumed, awaiting confirmation |
| D49 | Arena props are **named prefabs** (`PH_Prop_P<n>_<Name>`, footprints and heights from `ART_ASSET_LIST.md`) with an `ArenaProp` component; their footprints are what the bake turns into blocked cells. The per-phase obstacle Tilemaps stay for extra painted blocking. Ground: one Tilemap per phase (placeholder grey-scale tiles tinted per phase plus soft patches, or the imported 8 × 8 ground art); decor prefabs per phase are walk-over only. | §14 "more than a palette change"; final rule 3 (art swappable without touching gameplay). Default |
| D50 | Layout rules, checked by tests on the baked layout for all three phases: every free cell reachable, no one-cell slots (every free cell is part of a free 2 × 2 square), spawn zones plus a one-cell margin open and joined to the centre by 2-wide routes, starts and the shop front clear, phases share less than half their cover. | T16 acceptance ("every spawn zone leaves an escape route"). Default |
| D51 | Anything that appears or is moved by the game (phase change, spawns, children, coin drops, shop entry) is placed with `ArenaGrid.NearestFree` (the nearest free-cell centre where the body fits). `Resolve` remains for movement only: it only fixes small overlaps and failed from the middle of a 3-wide obstacle (found by the T16 transition test). | §14 "never place a character inside a new obstacle". |
| D52 | Light per phase is data (`Atmosphere`: global light 1.0 / 0.82 / 0.6, player glow radius 8 and intensity 0.7 scaled by darkness, mist, fade times) so it can be tuned live like the rest. Attack warnings, bars, projectiles, the reticle, coins and the player core use the unlit sprite material (sorting order 20+ or flagged), so darkness never hides them. Lamp posts and lit machines carry their own 2D lights. The ending fades every light (lamps included) to black during the last scene. | §14: phase three darker but readable, full darkness for the ending; D18. Default |
| D53 | Game feel is one `FeedbackDirector` fed by simulation events (cosmetic only, never saved): muzzle flash, pooled sparks (one particle system), hit flash plus squash, capped corpses and blood / oil / scorch decals (ring buffers, oldest replaced), damage numbers merged per enemy per frame and rate-limited, Cinemachine impulse shake (uniform, short, scaled by the shake setting, shot shake at most every 0.08 s, no hit-stop). Every number is in `Feedback` data; the effect budget setting scales the caps (Low 0.3, Medium 0.6, High 1). Aftermath is cleared on load, retry and phase change. | T05, §14, D18. Default |
| D54 | Sorting layers `Default` → `Effects` → `Telegraphs`. Attack telegraphs, enemy shots, hazard zones, item previews and the reticle are on `Telegraphs` (telegraphs inside a character use a nested sorting group with Sort At Root), so no effect can cover a warning. Particles use the unlit sprite material with a sprite sheet (the 2D renderer does not draw the 3D particle shaders). | §14 "telegraphs never hidden by effects". |
