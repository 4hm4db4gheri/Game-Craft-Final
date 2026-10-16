# Hollow — AI art master brief

How to use it:

1. Start every conversation with an image AI by pasting the **MASTER BRIEF** block (section 1) unchanged.
2. Paste **one ASSET REQUEST** (section 2) filled from `docs/ART_ASSET_LIST.md`. One asset per request gives the most consistent results.
3. After the first approved character and first approved environment piece, attach them as **style reference images** to every later request (most tools accept a reference image). This keeps the whole game in one style.
4. Save the image under the exact file name from the request into `Assets/Art/Incoming/`. The project's importer reads the name and sets scale, pivot, slicing and background removal automatically. Nothing else needs to be done by hand.
5. If the tool cannot make a transparent background, the brief asks it for a flat pure magenta (#FF00FF) background; the importer removes it.

The brief is written for any image model (GPT image, Midjourney, Gemini, Stable Diffusion/Scenario, Leonardo…). If a tool ignores the ASSET CARD text part, that's fine: the file name is what the project needs.

---

## 1. MASTER BRIEF (paste this first, every time)

```text
MASTER BRIEF — "HOLLOW" GAME ART (read fully; these rules apply to every image I ask for in this conversation)

PROJECT
Hollow is a 2D PC top-down shooter with a dark-comedy story. The player is a small floating AI drone that helps a tired human worker ("the Operator") make his facility "the most productive in the world". The AI fights through three phases — Animals, then Robots, then Humans — and wins while the world goes dark. Tone: gloomy, foggy, desaturated, quietly absurd. Never cute-cartoon, never gory-realistic.

STYLE (must stay identical for every asset)
- Hand-painted 2D digital illustration, clean readable shapes, soft painterly shading, limited texture noise.
- NOT pixel art. No visible pixels, no dithering, no 3D render look, no photo look.
- Thin dark outline around every character and object (color #151318, about 2-4% of the subject height), so silhouettes read on a dark foggy ground.
- Muted, desaturated colors with a few strong accent lights (see COLOR RULES).
- Soft key light from the upper left; gentle rim light allowed. NO cast shadow on the ground and NO ground under characters or props: the game adds its own soft shadow.
- Detail must stay readable when the asset is shown small (one game unit is about 72 pixels on a 1080p screen). Prefer big clear shapes over fine detail.

CAMERA / PERSPECTIVE
- 3/4 top-down view, like the game "Kill The Crows": the camera looks down at about 55 degrees, so we see the top AND the front of things. Not a side view, not straight top-down, not a diamond isometric view.
- Orthographic: no perspective distortion, no vanishing points, vertical edges stay vertical.
- Characters face RIGHT (toward the right edge, three-quarter front). The game mirrors them for left. Do not draw a left-facing version unless asked.

SCALE AND FRAMING (resolution-independent; the importer depends on this)
- 1 game unit = 128 px at final size. Each request gives a CELL size in units and the subject HEIGHT in units.
- Characters and props: each frame is a square cell. The point where the subject touches the ground (between the feet, or the center of the base) sits exactly at the horizontal center of the cell and at 10% of the cell height above the bottom edge. The subject's top reaches (10% + HEIGHT/CELL) of the cell height. Keep at least 3% empty margin on the left and right.
- Any square resolution of at least 1024 px per cell is fine; the importer downsizes it.
- Animation: frames side by side in ONE horizontal row of equal square cells, no gaps, no borders, no numbers. Same character, same size, same ground point in every frame. Looping states must loop smoothly (last frame flows into the first).

BACKGROUND
- Transparent background (PNG with alpha) whenever the tool supports it.
- If transparency is not possible: a perfectly flat pure magenta #FF00FF background, no gradient, no shadow, no texture, and never use magenta or pink inside the subject.
- Exceptions are stated in the request: ground textures are full-bleed and seamless; visual-effect sheets may use pure black #000000; key art is a full scene.

COLOR RULES (gameplay readability depends on these)
- RESERVED, do not use in any asset unless the request says "danger": saturated red-orange (around #FF4A1C) — it means "enemy attack / danger" in the game.
- RESERVED: bright gold (around #FFC83D) is only for coins. Pure magenta #FF00FF is only for the removable background.
- Player AI: cold white body with a cyan glow (#7FE7FF). Player projectiles: cyan-white.
- Operator (human companion): dull ochre and faded work-blue clothes, tired skin tones.
- Animals faction: muted earth browns, moss greens, bone, dull rust fur; dull amber eyes.
- Robots faction: brushed steel grey and gunmetal with acid-green status lights (#7CFF6B).
- Humans faction (security forces): navy, charcoal and khaki uniforms, white visor reflections, small cold-blue lights.
- Blood and oil are dark and desaturated (deep maroon #4A1418, black oil), never bright red.

FORBIDDEN IN EVERY IMAGE
- Text, letters, numbers, logos, watermarks, signatures, UI frames, borders, grids, labels (unless the request explicitly asks for a logo).
- Extra characters or objects that were not requested; multiple poses in one cell; cropped heads, feet or weapon tips.
- Baked ground, cast shadows, background scenery behind characters or props.
- Health bars, targeting circles or glows that look like gameplay markers (the game draws those).

REPLY FORMAT
For every request reply with: (1) the image; (2) one ASSET CARD in a code block exactly like this, with the values you actually used:
ASSET_CARD {"file":"<file name from the request>","cellUnits":<number>,"heightUnits":<number>,"frames":<number>,"background":"transparent|magenta|black|full","notes":"<one short line>"}
Do not explain or describe the image beyond that.
```

---

## 2. ASSET REQUEST (paste after the master brief; one per message)

```text
ASSET REQUEST
File: <FILE NAME from ART_ASSET_LIST.md, e.g. ENM_A1_RunnerWolf_move_f6.png>
Type: <character | weapon | projectile | prop | ground | decor | pickup | icon | ui | fx | keyart>
Subject: <one or two sentences from the asset list "Look" column>
Cell: <N> units square   Height: <H> units   Frames: <F> (<state name>)
Pose / motion: <what happens in this state, frame by frame if it is an animation>
Background: <transparent | black | full-bleed seamless | full scene>
Style reference: <"use the attached image(s) as the style reference" or "none yet">
```

Type-specific rules to add to the request when relevant (copy the matching line):

- **character**: "Ground point at center, 10% from the bottom; facing right; no shadow; no weapon unless stated." For enemies that attack, the **windup** frames must show a clear readable anticipation pose (crouch, raise, pull back) — the game also adds its own warning marker.
- **weapon** (held by the player drone): "Canvas 2:1 (twice as wide as tall). Weapon lies horizontal, muzzle pointing RIGHT, side view slightly from above. Grip point at 25% from the left edge, vertically centered. No hand, no arm." Length is given in units; the canvas width is 2 × length.
- **projectile**: "Canvas 2:1, pointing RIGHT, centered, glowing core with a short soft tail. Player = cyan-white. Enemy = danger red-orange core with a white-hot center."
- **ground**: "Square, perfectly seamless on all four edges, seen from straight above (no perspective, no objects that look like obstacles, no strong single-direction lighting). Even detail density, no single focal point." Size in units is given (usually 8 × 8 units).
- **prop / obstacle**: "3/4 view, ground point = center of the front-bottom edge of the base, at 10% from the bottom. Footprint W × D units (the base area on the ground). No ground, no shadow."
- **decor** (walk-over ground details: grass tufts, cracks, puddles, cables): "Seen from above with a slight 3/4 tilt, very flat, low contrast so it never looks like an obstacle."
- **pickup** (coin): "Small, very readable, gold is allowed here."
- **icon**: "Square 1:1, subject centered and filling about 80%, slight 3/4 angle, strong silhouette readable at 48 px, no frame, no background."
- **ui** (9-slice panels and buttons): "Square, decorative border of exactly 12.5% of the width on every side, plain flat center, corners and edges designed to stretch, no text, no icons."
- **fx**: "Horizontal strip of equal square cells on pure black #000000 (it will be blended additively) unless the request says transparent. Effect centered in each cell."
- **keyart**: "16:9 full scene, 1920 × 1080 or larger, same painting style, room for a title in the upper third (leave it empty)."

---

## 3. File-name convention (the importer reads it)

```
<CAT>_<ID>_<Name>_<state>[_f<frames>][_<variant>].png
```

| Part | Values |
|---|---|
| CAT | `CHR` player/companion · `ENM` enemy · `BOS` boss · `WPN` held weapon · `PRJ` projectile · `PKP` pickup / world item · `ENV` ground, props, decor · `FX` effects · `ICO` icons · `UI` interface · `KEY` key art |
| ID | the ID from the asset list (`A1`, `R7c`, `B2`, `SMG`, `P1` …) |
| state | `idle`, `move`, `windup`, `attack`, `death`, `held`, `icon`, `ground`, `prop`, … |
| `_f<frames>` | number of frames in the horizontal strip; omit for a single image |
| variant | `a`, `b`, `c` … for alternative versions (decals, decor) |

Examples: `ENM_A1_RunnerWolf_move_f6.png`, `CHR_PLY_Core_idle_f4.png`, `WPN_SMG_LightSMG_held.png`, `ENV_P1_Hedge_prop.png`, `ENV_P1_Ground_ground.png`, `FX_GEN_Explosion_burst_f8.png`, `ICO_SKL_Damage_icon.png`.

## 4. What the game does by itself (do not ask the AI for these)

Attack telegraphs and warning zones, health bars, shadows, hit flashes, outlines on hover, damage numbers, dash trails, pulse and shield rings, the aiming reticle trajectory, all text, and the countdown digits on the suicide robot. These are drawn by code so they stay exact and readable.

## 5. Minimum versus full set

Every entity works with a **single static frame** (`_idle` without `_f`): the game animates it with bobbing, squash, tilt and flashes. Animation strips are an upgrade. Generate in this order: the P0 list in `ART_ASSET_LIST.md` (enough for the playable slice), then P1, then P2.
