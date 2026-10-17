# Hollow — animation addendum (art wave 2)

Wave 1 gave every character state as **one still frame**. The game now makes single frames step and rock while they move, but real animation needs frame sheets. This addendum asks the image AI for those sheets, starting from the frames we already have.

How to use it:

1. In a new conversation with the image AI, paste the **MASTER BRIEF** from `docs/ART_MASTER_BRIEF.md` (section 1) unchanged, then the **ANIMATION ADDENDUM** below (section 1 here) unchanged.
2. For each sheet, attach the existing frame from `Assets/Art/Incoming/` as the reference image (the file named in the table, section 3) and paste one **ANIMATION REQUEST** (section 2) filled from the table.
3. Save the result under the exact file name from the request into `Assets/Art/Incoming/`. Keep the old single frame: when both exist, the game uses the sheet. Then run **Hollow > Art > Apply Incoming Art**.
4. Check the sheet before saving: same character in every cell, feet on the same line, nothing cut at the cell edges, no grid lines. A bad cell ruins the loop; ask for a redo of the whole sheet rather than fixing one cell.

**Sheet layouts the game reads.** The frame count is in the file name (`_f6`). Most image tools cannot make a 6:1 image, so a grid is fine:

| Frames | Image shape | Layout (read left→right, top→bottom) |
|---|---|---|
| 4 | square 1:1 (e.g. 2048×2048 or 1024×1024) | 2 columns × 2 rows |
| 6 | 3:2 landscape (e.g. 1536×1024 or 3072×2048) | 3 columns × 2 rows |
| 8 | 2:1 landscape | 4 columns × 2 rows |
| any | one long row (N:1), if the tool can | N × 1 |

Every cell must be square. Do not use other frame counts with a 1:1 or 3:2 image (for example 3 or 5 frames), because the cells would not be square.

---

## 1. ANIMATION ADDENDUM (paste right after the master brief)

```text
ANIMATION ADDENDUM — applies to every request from now on in this conversation

WHAT I NEED
Animation sheets for characters that already exist. Each request comes with a REFERENCE IMAGE: that image IS the character. Copy its design exactly in every frame: same species/model, same proportions, same colors, same outline thickness, same lighting from the upper left, same level of detail, same size. Only the pose changes. Do not redesign, restyle, recolor, add or remove parts, or change the camera angle.

SHEET LAYOUT (the game slices the image automatically; this must be exact)
- The image is a grid of equal SQUARE cells with the column × row count given in the request. Frames are read left to right, then top to bottom.
- No gaps, no borders, no grid lines, no frame numbers, no labels. The cells are invisible: only the character in each one.
- In EVERY cell: the character is drawn at the same scale as the reference, centered horizontally, and its ground point (between the feet, or the center of the base; for hovering things, the point under them) sits at 10% of the cell height above the cell's bottom edge. The feet of standing frames all touch the same ground line.
- Nothing may cross into a neighbouring cell or touch a cell edge: keep at least 4% empty margin on all sides of each cell. If a pose is too wide or tall (a lunge, raised arms, a fall), make the character slightly more compact in that pose rather than cropping it.
- The character animates IN PLACE: it does not travel across the cell. The game moves it.
- Facing right in every frame, exactly like the reference.

MOTION RULES
- Looping states (idle, move): the last frame must flow into the first. Even timing: the game plays every sheet at 10 frames per second.
- Move cycles: a full cycle of steps in the given number of frames, with clear weight (body dips when a foot lands, rises on the push-off). Legs and arms must alternate correctly and stay attached.
- Windup (attack anticipation): a clear, readable build-up that ends in the most extreme pose; the last frame is held by the game until the attack fires.
- Attack: the strike itself, then a short follow-through. Do not draw projectiles, muzzle trails, quill streaks, speed lines or impact flashes: the game adds its own.
- Death: the character collapses; the LAST frame is the corpse lying on the ground and must match the reference death image if one is given.
- Keep the silhouette readable at small size in every frame; avoid poses where limbs overlap into a blob.

BACKGROUND: transparent, or flat pure magenta #FF00FF if transparency is impossible (same rules as the master brief). No ground, no shadow, no dust clouds.

REPLY FORMAT
The image, then one ASSET CARD:
ASSET_CARD {"file":"<file name>","frames":<number>,"layout":"<cols>x<rows>","cellPx":<cell size in pixels>,"background":"transparent|magenta","notes":"<one short line>"}
```

---

## 2. ANIMATION REQUEST (one per message, reference image attached)

```text
ANIMATION REQUEST
File: <file name from the table, e.g. ENM_A1_RunnerWolf_move_f6.png>
Reference image: attached (<name of the attached file>) — this is the character; copy it exactly.
Frames: <F>   Layout: <cols> columns × <rows> rows of square cells   Image shape: <1:1 | 3:2 | 2:1>
Loop: <yes | no>
Motion, frame by frame:
<paste the motion text from the table>
```

---

## 3. What to ask for

Priority: **A** first (what players see all the time), then **B**, then **C**. Reference = the file in `Assets/Art/Incoming/` to attach (use the `idle` frame as the main reference and add the state's own frame when it exists, so the pose idea carries over).

### Phase 1 animals

| File | Pri | Reference | Motion, frame by frame |
|---|---|---|---|
| `ENM_A1_RunnerWolf_move_f6.png` (3×2, loop) | A | `ENM_A1_RunnerWolf_move.png` + `_idle.png` | Hungry trot-gallop: 1 front legs reach forward, 2 front paws land and the body dips, 3 hind legs swing under the body, 4 hind legs push off and the back stretches, 5 short flight with all legs tucked, 6 front legs reaching again. Head low, ears back, tail streaming. |
| `ENM_A1_RunnerWolf_idle_f4.png` (2×2, loop) | B | `_idle.png` | Standing, breathing: 1 neutral, 2 ribs expand and head lifts a little, 3 ears twitch, 4 back to neutral. Small changes only. |
| `ENM_A1_RunnerWolf_windup_f4.png` (2×2) | B | `_windup.png` | 1 stops and lowers the head, 2 crouches with weight on the hind legs, 3 lips pull back over the teeth, 4 deepest crouch, coiled to spring. |
| `ENM_A1_RunnerWolf_attack_f4.png` (2×2) | B | `_attack.png` | 1 explosive lunge forward, 2 jaws wide at full reach, 3 jaws snap shut, 4 lands and recovers balance. |
| `ENM_A1_RunnerWolf_death_f6.png` (3×2) | C | `_death.png` | 1 hit recoil, 2 legs buckle, 3 falls on its side, 4 slides, 5 twitches, 6 still corpse = the reference death pose. |
| `ENM_A2_QuillShooter_move_f6.png` (3×2, loop) | A | `ENM_A2_QuillShooter_move.png` + `_idle.png` | Heavy waddle: 1 front-left paw forward, 2 body rocks onto it, 3 hind-right paw steps, 4 front-right paw forward, 5 body rocks the other way, 6 hind-left paw steps. Quills sway with each rock. |
| `ENM_A2_QuillShooter_idle_f4.png` (2×2, loop) | B | `_idle.png` | Sniffing: 1 neutral, 2 nose lifts, 3 quills ripple slightly, 4 neutral. |
| `ENM_A2_QuillShooter_windup_f4.png` (2×2) | B | `_windup.png` | Quills rise step by step: 1 back arches, 2 quills half raised, 3 quills fully fanned, 4 maximum fan with the body pulled back. |
| `ENM_A2_QuillShooter_attack_f4.png` (2×2) | B | `_attack.png` but WITHOUT the painted quill streaks | 1 sharp forward jerk that launches the quills, 2 recoil, 3 quills lower, 4 back to normal posture. Draw no flying quills. |
| `ENM_A2_QuillShooter_death_f6.png` (3×2) | C | `_death.png` | Rolls onto its side, quills flatten; last frame = reference death pose. |

### Phase 2 and 3 enemies

| File | Pri | Reference | Motion, frame by frame |
|---|---|---|---|
| `ENM_R1_Crawler_move_f6.png` (3×2, loop) | A | `ENM_R1_Crawler_move.png` + `_idle.png` | Mechanical scuttle, legs move in diagonal pairs: 1 pair A lifts, 2 pair A steps forward, 3 pair A lands and the body jolts slightly, 4 pair B lifts, 5 pair B steps forward, 6 pair B lands. Pincers bob a little. |
| `ENM_R2_Shooter_move_f6.png` (3×2, loop) | A | `ENM_R2_Shooter_move.png` + `_idle.png` | Stiff bipedal robot walk, barrel arm held steady: 1 left foot forward lands, 2 body settles, 3 right leg passes, 4 right foot forward lands, 5 body settles, 6 left leg passes. Lens stays lit. |
| `ENM_R2_Shooter_windup_f4.png` (2×2) | B | `_windup.png` | Barrel arm raises to aim (1, 2), the barrel tip heats up in danger red-orange (3), brightest charge (4). |
| `ENM_R2_Shooter_attack_f4.png` (2×2) | B | `_attack.png` without the muzzle flash | 1 recoil kick, 2 body pushed back, 3 recovering, 4 barrel lowers to ready. |
| `ENM_H2_RifleGuard_move_f6.png` (3×2, loop) | A | `ENM_H2_RifleGuard_move.png` + `_idle.png` | Tactical walk, rifle at low ready: a normal 6-frame walk cycle (contact, down, pass, contact, down, pass), arms stay on the rifle. |
| `ENM_H2_RifleGuard_windup_f4.png` (2×2) | B | `_windup.png` | Raises the rifle to the shoulder (1–3), aims steady (4). |
| `ENM_H2_RifleGuard_attack_f4.png` (2×2) | B | `_attack.png` without the muzzle flash | Three-shot burst: recoil, recover, recoil, settle. |
| Crawler, Shooter, Rifle guard: `idle_f4` and `death_f6` | C | their own frames | Same idea as the wolf: small breathing/servo motion; collapse ending in the reference death pose. |

### Player, Operator, bosses

| File | Pri | Reference | Motion, frame by frame |
|---|---|---|---|
| `CHR_OPR_P1_move_f6.png` (3×2, loop) | A | `CHR_OPR_P1_move.png` + `_idle.png` | Tired walk carrying the rifle in both hands: a 6-frame walk cycle (contact, down, pass, contact, down, pass), shoulders slumped, small head bob. |
| `CHR_OPR_P1_attack_f4.png` (2×2) | A | `CHR_OPR_P1_attack.png` | Already aiming: 1 fires (recoil into the shoulder), 2 recovers, 3 fires again, 4 settles. No muzzle flash. |
| `CHR_OPR_P1_idle_f4.png` (2×2, loop) | B | `_idle.png` | Exhausted breathing, small sway, a glance aside on frame 3. |
| `CHR_OPR_P2_move_f6.png`, `_attack_f4.png`, `_death_f6.png`, `_hit.png` | B | `CHR_OPR_P2_idle.png` (and the P1 sheet of the same state for the pose) | The phase-2 Operator only has an idle frame. Same motions as P1 but slower and weaker: limping walk, sidearm instead of the rifle, injured arm kept close. |
| `CHR_PLY_Core_idle_f4.png` (2×2, loop) | B | `CHR_PLY_Core_idle.png` | Hovering drone: the ring rotates a quarter turn over the 4 frames, the eye light pulses brighter on frame 2. Keep the shadow point fixed. |
| `CHR_PLY_Core_move_f4.png` (2×2, loop) | B | `CHR_PLY_Core_move.png` | Leaning into motion; ring rotates faster, small thruster shimmer under it. |
| `BOS_B1_LargeBeast_move_f6.png` (3×2, loop) | A | `BOS_B1_LargeBeast_idle.png` | **Missing state.** Heavy four-legged lumber, head swinging low, stakes in its hide wobbling. Note: wave 1 mixes a bear and a boar between states; use THIS idle image as the one true design for all boss 1 sheets. |
| `BOS_B1_LargeBeast_` `idle_f4`, `windup_charge_f4`, `charge_f4`, `recover_f4`, `windup_strike_f4`, `strike_f4`, `death_f6` | C | `_idle.png` + that state's frame | Redraw every state from the same design as the idle. Windup charge = paws the ground, head down; charge = full gallop (loop-like); recover = dazed, shaking its head; windup strike = rears up; strike = slams down. |
| `BOS_B2_IndustrialRobot_move_f6.png` (3×2, loop) | B | `BOS_B2_IndustrialRobot_move.png` + `_idle.png` | Heavy mech stomp: each footfall shakes the frame slightly; arms swing a little. |
| `BOS_B2_IndustrialRobot_recover_f4.png` (2×2) | B | `_idle.png` | **Missing state.** After an attack: vents steam, arms lower, core dims then relights. |
| `BOS_B2_IndustrialRobot_` `windup_burst_f4`, `burst_f4`, `windup_blast_f4`, `blast_f4`, `stunned_f4`, `death_f6` | C | `_idle.png` + that state's frame | Keep the idle's gunmetal grey and acid-green colors in EVERY state (wave 1 made `blast` all green and `burst` rust-colored). Burst = gatling arm fires with recoil; windup blast = arm raised, glow building; blast = slams the arm into the ground; stunned = sparks, sagging. |

The game adds its own warning markers, flashes, projectiles, muzzle flashes, shadows and dust, so none of these belong in the sheets.
