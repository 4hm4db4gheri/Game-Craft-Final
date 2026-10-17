# Hollow — art wave 1 (debug build)

Drop `Assets/Art/Incoming/` into the Unity project. 104 PNGs + `ASSET_CARDS.txt` (one ASSET_CARD per file: cell, height, frames, background).

## Conventions used (from ART_MASTER_BRIEF)
- Characters/props: 1024 px square per cell, ground point at horizontal center, 10% from bottom, facing right, transparent.
- Every character state is a **single frame** (no `_f`), e.g. `ENM_A1_RunnerWolf_windup.png`. Hand-drawn strips come later.
- Weapons 1024×512 (2:1), muzzle right, grip at 25%. Projectiles 512×256. Icons 512×512.
- FX strips on black (additive). Grounds 1024×1024 seamless = 8×8 units.
- Coin is a real 6-frame strip: `PKP_COIN_Coin_spin_f6.png`.

## Scope decisions (team, following the GDD)
- One player AI (egg drone). 2 enemies per phase (A1, A2, R1, R2, H1, H2), 3 bosses, 5 weapons, grenade only.
- The Operator is the woman from the GDD concept. P2 = rifle donated, sidearm only.

## Non-standard things to know
- `CHR_PLY_Core_*`: drone hovers; ground point = shadow point. Extra state `hit`.
- `CHR_OPR_P1_hit`, `ENM_*` all have idle/move/windup/attack/death.
- `BOS_B2_IndustrialRobot_*` drawn **4.15 units** high, not 4.5 (raised arm must fit the cell). Hitbox unchanged.
- `ENV_P1_BorderH_prop` is opaque (painted forest panel); `ENV_P1_BorderV_prop` is transparent. They don't match yet.
- `ENV_P3_Ground_ground` is the cracked-concrete ground. There is **no P2 ground yet**: reuse P3 for now.
- UI buttons are wide (not square) — set 9-slice borders by hand.

## Known placeholders / issues (art will replace)
- Boss 1 uses the older, darker sheet. Boss 2 `burst` frame is rust-coloured. Some boss frames have small clipped glow/smoke edges.
- Quill shooter `attack` still has painted quill streaks (game also spawns the projectile).
- `ICO_GEN_Coin/Lock/Infinity` are tiny sources upscaled → blurry.
- Cutouts are automatic: expect slight dark fringe on some edges.

## Not in this wave
H1 Baton guard, Boss 3 (former companion), industrial + energy rifles, phase 2 ground/props/fence, blood/oil decals, AI "off" pose for C00.
