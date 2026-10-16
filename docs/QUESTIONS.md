# Hollow — Open questions

Status: **open, awaiting user answers** (asked 2026-10-16, **revised the same day after the user decided the game is 2D** — see DECISIONS.md D1). Nothing below is a decision yet unless marked *Answered*.
Each question has a recommended default (*Rec*). Reply with "defaults OK except Qx, Qy …" to answer fast.
★ = blocking: no safe default; I need your call before the related task starts.

Source of truth: `docs/GDD.md` (converted from `Hollow_Game_Design_EN.docx`, v0.12).

---

## Inspection findings (read-only, 2026-10-16)

- Existing Unity project in the working directory: **Unity 6000.6.0f1**, **URP 17.6 with the 2D Renderer** (2D template), **new Input System 1.20**, **Test Framework 1.8**, **uGUI 2.6** (TextMeshPro is built into uGUI 2.x). **Cinemachine and AI Navigation are not installed.** Extra template packages: Visual Scripting, AI Assistant / AI Inference, Collab proxy, IET tutorial framework, `com.unity.pipeline` (experimental; it lets the `unity` CLI drive the open Editor), 2D Animation / PSD / Aseprite / SpriteShape / Tilemap.
- Only content: `Assets/Scenes/SampleScene.unity`, template `Assets/Welcome/`. No gameplay code.
- git: branch `main`, remote `origin` = GitHub `4hm4db4gheri/Game-Craft-Final`; `agent.md` has an uncommitted change from before this session.
- Unity Editor 6000.6.0f1 is **running with this project open**; the `unity` CLI is installed (recompile, test, command, build).
- Network: kenney.nl and opengameart.org reachable (HTTP 200). No pandoc / ffmpeg; I converted the GDD with a small Python script and viewed the videos through a local browser contact sheet.
- Unrelated files next to the inputs, **not opened**: `Hollow_Game_Design_FA.docx`, `Hollow_Prototype_Offline.zip`.

### GDD number check (done by script, not by eye)
All GDD totals reproduce exactly from the tables:
- Enemies: Animals 226 / 238 / 31 / 269, Robots 322 / 340 / 39 / 379, Humans 446 / 454 / 43 / 497, Total 994 / 1032 / 113 / 1145.
- XP per phase 1116 / 1990 / 3246 (regular kills + boss pool); cumulative choices 7 / 13 / 19.
- Pre-boss coins 804 / 1534 / 2614 = regular kill coins (674 / 1304 / 2264) + wave bonuses (130 / 230 / 350). This only works if **wave bonuses are coins only**.
- Boss pools minus the table rewards of their adds leave a **boss own share of 100 XP / 100 coins, 180 / 180, 260 / 260**.
- The 80 % pickup column = 80 % of ground coins + 100 % of bonuses (bonuses are credited directly).
- Note: 19 XP choices are reachable in a full run versus 21 skill ranks, so a player can never max every skill. Fine, just flagging.

---

## A. Project, tools, process

- **Q1** (seed Q2) Work inside this existing project and keep Unity 6000.6.0f1 / URP / new Input System. Target 1920×1080 16:9, must also work at 1600×900 and 1280×720. *Rec:* yes.
- **Q2** *Answered by D1/D2:* keep URP's **2D Renderer**; no 3D renderer is added.
- **Q3** Template leftovers: delete `Assets/Welcome/` and `SampleScene`, replace with Boot / MainMenu / Game scenes. Keep the 2D packages (now useful) and leave the other extra packages installed. *Rec:* yes.
- **Q4** Cinemachine is not installed. *Rec:* don't add it; write a small 2D follow camera (easy to save/restore exactly), plus URP's built-in Pixel Perfect Camera if the art is pixel art (Q10).
- **Q5** ★ Pathfinding around obstacles. Unity's NavMesh does not support 2D out of the box (the 2D add-on "NavMeshPlus" is third-party code from GitHub). *Rec:* I write a **flow field on the arena grid** (one path map toward the player, shared by all enemies, cheap for 42+ units) plus simple local steering; no new package.
- **Q6** Git: one commit per task on `main`, **never push unless you ask**. The pre-existing `agent.md` change goes into the first commit. *Rec:* yes.
- **Q7** The Editor is open with this project. May I drive it through the `unity` CLI (recompile, run tests, enter Play mode, edit scenes, take screenshots)? Please don't edit the same scenes at the same time. *Rec:* yes.
- **Q8** ★ `Hollow_Game_Design_FA.docx` and `Hollow_Prototype_Offline.zip` sit next to the files you gave me. Is the EN GDD binding over the FA one? Should I look at the prototype? *Rec:* EN is binding; ignore both unless you say otherwise.
- **Q8b** docs/ in English; `agent.md` stays in Persian as it is now. *Rec:* yes.

## B. Camera, art, arena

- **Q9** *Answered (D1):* 2D. ★ **Follow-up: which kind of 2D view?** Video A (Kill The Crows) is not true isometric: it is a **3/4 top-down** view (rectangular map, buildings and hedges drawn from slightly above and in front, like Enter the Gungeon or Nuclear Throne). True **isometric** means a diamond grid (2:1 tiles, map rotated 45°, like Hades or Diablo). The choice changes the tilemap type, how the artist draws every asset, and what W/A/S/D mean on screen. *Rec:* 3/4 top-down exactly like Video A; W always moves up on the screen.
- **Q10** ★ Pixel art or not, and its scale (the artist's call). *Rec:* pixel art like Video A, reference resolution 640×360 at **24 pixels per game unit** → about **26.7 × 15 units visible** (scales exactly ×2 to 1280×720 and ×3 to 1920×1080; 1600×900 gets thin black bars or slight blur). Player body radius 0.35 u ≈ 17 px wide.
- **Q11** ★ Video A's camera **follows the player** across a map bigger than the screen; the GDD says "fixed overhead camera". *Rec:* fixed view that follows the player with a small look-ahead toward the mouse, clamped to the arena edges. With Q10's size, the pistol range (14) reaches about the screen edge and the energy rifle (18) goes past it.
- **Q12** ★ (seed Q9) Arena. *Rec:* **48 × 32 units** walled rectangle built with **Tilemaps** (ground, obstacles with colliders, decoration) so the designer and artist can paint layouts; four edge spawn gates (N/E/S/W); shop station south of centre; one obstacle Tilemap per phase. Do you have dimensions or a sketch?
- **Q13** (seed Q8) Telling 21 enemies + 3 bosses apart without art. *Rec:* Unity's built-in 2D sprites (square, circle, capsule, triangle, hexagon, diamond) combined per type + a faction palette (animals earthy brown/green, robots steel/cyan, humans navy/khaki); all telegraphs in one reserved warning colour (red-orange) used nowhere else, drawn as flat ground sprites or LineRenderers under the characters.
- **Q14** (seed Q6 part) Player look. *Rec:* small glowing circle "core" floating (bobbing) above its shadow with a separate weapon sprite rotating toward the aim (GDD §14). Companion: tall capsule in a friendly colour that desaturates and shrinks slightly in phase two.
- **Q15** Persistent aftermath (Video A): corpses and blood/scorch decals stay, capped (oldest fade). They are cosmetic, so manual load does **not** restore them exactly. *Rec:* yes.
- **Q72** Depth sorting and hitboxes in a 3/4 view. *Rec:* characters are sorted by their feet (lower on screen = drawn in front). Every gameplay collision uses a **ground circle at the feet** (body radius 0.35 etc. from the GDD), so a taller sprite never enlarges the hitbox. Bullets travel on the same ground plane; the grenade fakes its arc with a shadow on the ground.
- **Q73** Low versus tall obstacles. In a 3/4 view, hedges, fences and graves look low and walls and buildings look tall. *Rec:* for the jam, every obstacle blocks both movement and shots (one rule, easy to read). Alternative: low cover blocks movement only.
- **Q74** Atmosphere (fog, street lamps, darkness in phase three and the lights-out ending like Video A). *Rec:* URP **2D lights** (built into the current renderer, cheap) plus a generated soft-noise fog overlay; no downloaded art.

## C. Controls and input

- **Q16** (seed Q10) Implement G cycle, right-click cancel, click-to-use and "place item in first slot" as proposed. Melee weapons use the primary slot (key 2); left click attacks. *Rec:* yes.
- **Q17** Firing: holding left click fires continuously at the weapon's rate for every weapon (pistol and melee included). *Rec:* yes.
- **Q18** In the field you only swap pistol ↔ current primary; choosing which owned weapon is primary happens at the shop (Equip, free). *Rec:* yes.
- **Q19** Keyboard + mouse only, no gamepad. *Rec:* yes.
- **Q20** Dash: one charge with a 2.5 s cooldown (Video A shows segmented bars, the GDD has a single dash). Dash passes through bosses and damages them once like any enemy. *Rec:* yes.

## D. Weapons

- **Q21** ★ (seed Q3) Weapon levels 2 and 3. *Rec:* I ship a CSV template (one row per weapon level, every field). Until the designer fills it, I generate rows marked `PLACEHOLDER / approved=false` that are buyable **only in editor/dev builds**, never in a release build.
- **Q22** ★ (seed Q4) Pistol advantage. *Rec:* "quick readiness": swapping **to** the pistol is instant (0 s instead of 0.15 s) and its reload is not cancelled by dash; both are data fields.
- **Q23** Hitscan or projectile. *Rec:* fast visible pooled projectiles for every gun (speed per weapon level in data, placeholder 35 u/s). Matches the trail/core feedback layers, and penetration "first + one behind" is natural. Projectile speed is not in the GDD, so it will be a flagged placeholder.
- **Q24** Shotgun falloff curve. *Rec:* linear from 100 % at 0 to 50 % at max range (the end points in data).
- **Q25** Energy shotgun "two targets": each pellet can hit two. *Rec:* yes.
- **Q26** Obstacles block player and enemy projectiles and melee arcs. *Rec:* yes.
- **Q27** Buying an upgrade keeps the current magazine count (clamped to the new size), no free reload; owned weapons that are not equipped can be upgraded. *Rec:* yes.
- **Q28** Frontal armour: front cone width is not in the GDD. Does it also reduce dash, grenade, mine and companion damage? *Rec:* 120° front cone (data); it applies to everything coming from the front, judged by direction from the source (explosion centre for blasts); only the energy blade ignores it.

## E. Enemies and bosses: missing numbers and rules

- **Q29** ★ The GDD gives health, damage, XP and coins only. Missing for all 21 enemies, children and bosses: movement speed, attack range, attack interval, windup/telegraph time, projectile speed, charge length, burst size, explosion radius and fuse, howl radius and duration, repair/heal amount, rate and total limit, children's health/damage/speed (spider offspring, drone, turret), EMP stun times except those given, boss move speed, strike radius, burst pattern. *Rec:* I fill these with values flagged `PLACEHOLDER` in data, list each in DECISIONS.md as "assumed, awaiting confirmation", and the designer tunes them in the panel.
- **Q30** Rule for placeholder speeds: no regular enemy is faster than the player (5) except during telegraphed charges/rushes. *Rec:* yes.
- **Q31** Melee enemies deal damage only through telegraphed attacks (short windup), never by body contact. *Rec:* yes (this guarantees "a telegraph before every damaging attack").
- **Q32** Opportunistic companion attacks. *Rec:* a melee enemy may switch to the companion when it is closer to the companion than to the player, within 3 units, and fewer than 3 enemies already target the companion; it switches back to the player when the player damages it.
- **Q33** Repair robot and medic healing a boss: the 10 % total cap is stated only for the robot boss. *Rec:* the same 10 % total cap applies to medics healing the human boss.
- **Q34** EMP versus the human boss "machine equipment": what does it do mechanically? *Rec:* interrupts the current attack for 0.8 s, then 6 s resistance (same as the machine boss); never stuns movement; also stuns turrets.
- **Q35** ★ Boss rewards: boss own share = pool − add shares (100/100, 180/180, 260/260). Boss and add coins drop on the ground; when the boss dies and the field is cleared, are the remaining ground coins auto-collected before the phase transition (otherwise they are lost)? *Rec:* boss share as computed; at boss death all ground coins are pulled to the player before the transition.

## F. Waves, tutorial, spawning

- **Q36** ★ What happens **between waves**? The GDD mentions "safe intervals" and wave-start checkpoints but no rule for starting the next wave. *Rec:* after a wave ends, a safe interval with an on-screen countdown (8 s, data) during which coin lifetime freezes and the shop works; Enter starts the next wave early.
- **Q37** Tutorial: "spawn four wolves first" but wave 1's active cap is 3. *Rec:* keep the cap at 3 (the fourth wolf enters when one dies).
- **Q38** The tutorial ignores the 30/30/40 batching; the 1.5 s gap still applies; profile multipliers never touch it. *Rec:* yes.
- **Q39** How is the shop "explained" without spoken guidance? *Rec:* when the 4 wolves are dead: a ground arrow to the shop, a one-line prompt "Press E at the shop", the SMG card highlighted at price 8. (Wave 1 only gives 20 XP, under the first 40 XP threshold, so XP and shop can't overlap; I still add a guard.)
- **Q40** Escape-route rule for the four zones. *Rec:* each spawn uses one of the zones that is **not** the one nearest the player, rotating; never all four in the same batch.
- **Q41** "About four seconds to read a new type". *Rec:* when the first unit of a newly introduced type spawns, the rest of that type waits 4 active seconds. The 8-second support/explosive separation in wave 4 is a separate data field.
- **Q42** Profile count multiplier applies to base composition only; children per producer stay fixed. *Rec:* yes.

## G. Economy, XP, shop, items

- **Q43** Wave completion bonuses are coins only (the GDD budgets require it). *Rec:* yes.
- **Q44** Coin lifetime flag default. *Rec:* enabled (60 s + 5 s blink), as the GDD test baseline.
- **Q45** An XP threshold crossed while the shop panel is open: the XP card appears at once (world pauses), shop panel hides and comes back after. Crossed during a scene, boss preparation or dialogue: deferred until combat resumes. *Rec:* yes.
- **Q46** "Per encounter" for healing and item stock = per wave or per boss fight (resets with the stock refresh). *Rec:* yes.
- **Q47** Grenade: lands at the cursor point clamped to 9 units and explodes 0.8 s after the throw (flight included). *Rec:* yes.
- **Q48** Mines are triggered by bosses too and damage them. *Rec:* yes.
- **Q49** Push pulse does not affect enemy projectiles. *Rec:* yes.

## H. Companion, story, dialogue

- **Q50** (seed Q5) Companion. *Rec:* keeps 2.5–4 units behind/beside the player, steers out of the player's movement line, auto-fires at the nearest enemy within 8 units (range = placeholder data). Phase-two weakening is visual (desaturated, slightly smaller, slower walk animation) plus the GDD numbers.
- **Q51** (seed Q6) Names and display. *Rec:* keep "Operator" / "Assistant" as data strings; subtitle box bottom-centre above the HUD with the speaker name; a short synthetic blip per line, no voice.
- **Q52** (seed Q7) Dialogue never locks combat controls except in non-combat scenes (opening, phase transitions, boss entrances, ending); every scene is skippable once seen in that slot. *Rec:* yes.
- **Q53** Phase-two warning sign. *Rec:* the Assistant's line cuts in before the Operator finishes, then "Operator approval: unnecessary." appears.
- **Q54** Final boss: anything beyond the §7 mechanics and §3 lines? *Rec:* no extra story mechanics.
- **Q55** Ending montage. *Rec:* ~15 s, camera pulls up, arena lights go out one by one, then rows of distant "city light" quads go dark, then the two announcements and "You won."; skippable.

## I. Saves and screens

- **Q56** (seed Q11) JSON under `Application.persistentDataPath/saves/` (slot file + checkpoint file), temp file then replace. Save is **disabled** (greyed) during short scenes/transitions, not queued. *Rec:* yes.
- **Q57** Esc on the XP card opens the pause menu; saving there stores the pending card. *Rec:* yes.
- **Q58** Death shows the failure screen (time frozen); Retry restores the checkpoint. *Rec:* yes.
- **Q59** Playtime counts gameplay and shop time, not pause menus. *Rec:* yes.
- **Q60** Ending stats. *Rec:* playtime, enemies defeated per phase, coins earned/spent, total XP, chosen skills, final weapons and levels, retries.
- **Q61** (seed Q12) English only. Settings: music, effects, shake intensity, window/fullscreen, plus damage numbers on/off and effect budget Low/Med/High. Rebinding stays out. *Rec:* yes.

## J. Feel and audio

- **Q62** Damage numbers (Video B layer 8) are not in the GDD. *Rec:* include, on by default, toggle in settings.
- **Q63** Any burning/DoT "aftermath" is visual only. *Rec:* yes.
- **Q64** Frame-rate target. *Rec:* 60 fps at 1080p on this machine; I report measured numbers only.
- **Q65** (seed Q15) Music: one free (CC0 preferred) loop per phase, silent ending. *Rec:* yes.
- **Q66** SFX sources: Kenney (CC0) first, then OpenGameArt CC0, Freesound CC0; OGG mono 44.1 kHz for effects. *Rec:* yes.
- **Q67** Video A shows a kill counter and timer; the GDD HUD has neither. *Rec:* follow the GDD; no timer, no kill counter.

## K. Deliverables and scope

- **Q68** (seed Q13) Windows x64 build only, output in `Builds/Windows/` (git-ignored), no WebGL. *Rec:* yes.
- **Q69** (seed Q14) All Provisional values become the initial data profile; Quiet / Baseline / Intense are dev-only. *Rec:* yes.
- **Q70** ★ Scope. T00–T22 is far more than one jam programmer can finish in two days, and more than I can fully verify in one sitting. *Rec:* the vertical slice T00–T09 is the first cut line; after it I give you a real estimate for the rest before continuing.
- **Q71** User stories: drafts in `docs/USER_STORIES.md`. *Rec:* approve as written or comment per task.

---

## Answers

(empty — filled in once answered)
