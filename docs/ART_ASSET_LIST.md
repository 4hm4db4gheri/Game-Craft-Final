# Hollow — Art asset list

Use with `docs/ART_MASTER_BRIEF.md`. Every row gives what to put in the ASSET REQUEST.
Looks are my default proposals (the GDD leaves the style open); change any "Look" freely, but keep IDs, cells and heights unless you also tell me, because the gameplay hitboxes are separate data and won't follow the art.

**Priority:** P0 = needed for the playable slice (phase 1 start) · P1 = rest of phase 1 + items + UI · P2 = phases 2–3, bosses, ending.
**States:** a static `idle` (no `_f`) is the minimum for every entity. Frame counts are the recommended full set.
Standard character states: `idle_f4` (loop), `move_f6` (loop), `windup_f3`, `attack_f4`, `death_f6` (last frame = corpse, stays on the ground).

## Player and companion

| ID / file base | Cell | Height | States | Pri | Look |
|---|---|---|---|---|---|
| `CHR_PLY_Core` | 2 | 1.1 (floats; ground point = where its shadow is) | idle_f4, move_f4 (leans into motion), death_f6 | P0 | Small spherical AI drone, smooth cold-white shell, one round cyan "eye" light, thin rotating ring around it; looks helpful and slightly too perfect. No arms: weapons float beside it. |
| `CHR_OPR_P1` | 2 | 1.7 | idle_f4, move_f6, attack_f2 (fires old rifle), death_f6 | P0 | The Operator, phase 1: tired middle-aged facility worker, faded work-blue overalls, dull ochre safety vest, rolled sleeves, coffee-stain tiredness, holds an old worn rifle. |
| `CHR_OPR_P2` | 2 | 1.6 (slouched) | same as P1 | P2 | Same person, clearly weaker: slouched, bandaged arm, greyer skin, vest torn, rifle held low. Must be recognizably the same character. |

## Animals (phase 1)

| ID / file base | Cell | Height | Extra states | Pri | Look |
|---|---|---|---|---|---|
| `ENM_A1_RunnerWolf` | 2 | 0.9 | windup = crouch, attack = bite lunge | P0 | Lean mangy grey-brown wolf, ribs showing, ears back. |
| `ENM_A2_QuillShooter` | 2 | 1.0 | windup = quills raise and back arches, attack = quill burst | P0 | Porcupine-like creature with a spray of long bone-white quills along its back. |
| `ENM_A3_ChargingBoar` | 3 | 1.3 | windup = paw the ground, attack = full charge pose, `recover_f3` (dazed) | P1 | Heavy tusked boar, muddy dark-brown bristles, broken tusk. |
| `ENM_A4_ArmoredTortoise` | 3 | 1.2 | windup, attack = head-butt | P1 | Big tortoise with a thick plated FRONT shield of shell over the head; sides and back softer and visibly exposed. |
| `ENM_A5_HowlingWolf` | 2 | 1.0 | `howl_f4` instead of attack | P1 | Darker wolf with a scarred bone-white mane, head raised when howling. |
| `ENM_A6_ExplosiveBeetle` | 1 | 0.6 | windup = abdomen swells, `explode` handled by FX | P1 | Bloated beetle with a translucent sickly yellow-green glowing abdomen sac. |
| `ENM_A7_MotherSpider` | 3 | 1.2 | `produce_f4` (egg sac pulses) instead of attack | P1 | Large hairy spider carrying a pale egg sac. |
| `ENM_A7c_Spiderling` | 1 | 0.4 | idle, move_f4, attack_f3, death_f4 | P1 | Tiny pale spider hatched from the sac. |

## Robots (phase 2)

| ID / file base | Cell | Height | Extra states | Pri | Look |
|---|---|---|---|---|---|
| `ENM_R1_Crawler` | 2 | 0.9 | attack = claw snap | P2 | Low four-legged maintenance crawler with pincers, scuffed steel. |
| `ENM_R2_Shooter` | 2 | 1.5 | windup = barrel arm raises and charges, attack = recoil | P2 | Bipedal sentry with one long barrel arm and a single acid-green lens. |
| `ENM_R3_SawCharger` | 2 | 1.0 | windup = saw spins up, attack = charge, `recover_f3` | P2 | Wheeled robot with a big circular saw on its front. |
| `ENM_R4_ShieldRobot` | 3 | 1.8 | windup, attack = shield bash | P2 | Bulky robot holding a heavy riot plate in FRONT; exposed cable-filled sides and back. |
| `ENM_R5_Repair` | 2 | 1.1 (hovers) | `repair_f4` (welding beam pose) | P2 | Small hovering repair bot with two thin arms and a welding torch. |
| `ENM_R6_Suicide` | 1 | 0.7 | windup = lights blink faster | P2 | Small round rolling bomb-bot with a blank display panel on top (the game draws the digits). |
| `ENM_R7_DroneCarrier` | 3 | 2.0 | `produce_f4` (bay opens) | P2 | Large four-legged walker with an open drone bay on its back. |
| `ENM_R7c_Drone` | 1 | 0.6 (hovers) | idle, move_f4, attack_f2, death_f4 | P2 | Small quad-rotor drone with one underslung barrel. |

## Humans (phase 3)

| ID / file base | Cell | Height | Extra states | Pri | Look |
|---|---|---|---|---|---|
| `ENM_H1_BatonGuard` | 2 | 1.75 | windup = baton raised, attack = swing | P2 | Security guard, navy uniform, white helmet visor, baton. |
| `ENM_H2_RifleGuard` | 2 | 1.75 | windup = shoulder rifle, attack = burst recoil | P2 | Same uniform, khaki vest, compact rifle. |
| `ENM_H3_FastAssaulter` | 2 | 1.7 | windup = crouched sprint start, attack = rush | P2 | Light armor, face mask, lean and fast-looking. |
| `ENM_H4_ShieldGuard` | 2 | 1.8 | windup, attack = shield shove | P2 | Heavy riot gear, big transparent riot shield in FRONT. |
| `ENM_H5_Medic` | 2 | 1.7 | `heal_f4` | P2 | Guard with a medical backpack and a GREEN cross patch (never red). |
| `ENM_H6_Grenadier` | 2 | 1.75 | windup = arm back, attack = throw | P2 | Guard with a grenade bandolier and padded armor. |
| `ENM_H7_Engineer` | 2 | 1.7 | `build_f4` | P2 | Technician in charcoal coveralls with a big toolbox. |
| `ENM_H7c_Turret` | 2 | 1.0 | idle, windup = barrel turns, attack_f2, death_f4 | P2 | Tripod security turret with a small cold-blue sensor light. |

## Bosses

| ID / file base | Cell | Height | States | Pri | Look |
|---|---|---|---|---|---|
| `BOS_B1_LargeBeast` | 6 | 3.5 | idle_f4, move_f6, `windup_charge_f3`, `charge_f4`, `recover_f3`, `windup_strike_f3`, `strike_f4`, death_f8 | P2 | Huge scarred bear-boar beast, broken survey stakes and cut cables stuck in its hide — a victim of the extraction. |
| `BOS_B2_IndustrialRobot` | 6 | 4.5 | idle_f4, move_f6, `windup_burst_f3`, `burst_f4`, `recover_f3`, `windup_blast_f3`, `blast_f4`, `stunned_f2`, death_f8 | P2 | Massive regional protector robot, heavy industrial frame, warning stripes in dull khaki (not red), acid-green core light. |
| `BOS_B3_FormerCompanion` | 4 | 2.8 | idle_f4, move_f6, `windup_burst_f3`, `burst_f4`, `windup_throw_f3`, `throw_f4`, `stunned_f2`, death_f8 | P2 | The Operator inside a makeshift security exo-rig: arm cannon, shoulder grenade launcher, his tired face visible; equipment strong, body weak. |

## Weapons held by the player (canvas 2:1, muzzle right, grip at 25 %)

| File | Length (units) | Pri | Look |
|---|---|---|---|
| `WPN_PST_Pistol_held` | 0.6 | P0 | Compact clean white-and-grey service pistol. |
| `WPN_SMG_LightSMG_held` | 0.9 | P0 | Light SMG, short barrel, side magazine. |
| `WPN_DBS_DoubleBarrel_held` | 1.2 | P0 | Sawn-off double-barrel shotgun, worn wood. |
| `WPN_BAT_Baton_held` | 1.0 | P1 | Stun baton with a dull grip. |
| `WPN_IRF_IndustrialRifle_held` | 1.3 | P2 | Heavy industrial rifle, bolted plates. |
| `WPN_MSG_MagShotgun_held` | 1.2 | P2 | Box-magazine combat shotgun. |
| `WPN_HAM_Hammer_held` | 1.3 | P2 | Industrial sledgehammer with a piston head. |
| `WPN_ERF_EnergyRifle_held` | 1.4 | P2 | Sleek white rifle with cyan energy coils. |
| `WPN_ESG_EnergyShotgun_held` | 1.3 | P2 | Wide-mouth energy shotgun with cyan vents. |
| `WPN_EBL_EnergyBlade_held` | 1.3 | P2 | Hilt with a cyan-white energy blade. |

Each weapon also needs an icon: `ICO_<ID>_<Name>_icon` (same look, 1:1).

## Projectiles, pickups, items

| File | Cell / canvas | Pri | Look |
|---|---|---|---|
| `PRJ_PLY_Bullet_proj` | 2:1, 0.4 long | P0 | Cyan-white tracer. |
| `PRJ_PLY_Pellet_proj` | 2:1, 0.25 long | P0 | Small cyan-white pellet. |
| `PRJ_PLY_Energy_proj` | 2:1, 0.6 long | P2 | Bright cyan energy bolt. |
| `PRJ_ENM_Quill_proj` | 2:1, 0.5 long | P0 | Bone-white quill with a danger red-orange glow. |
| `PRJ_ENM_Bolt_proj` | 2:1, 0.4 long | P2 | Robot shot, danger red-orange. |
| `PRJ_ENM_Bullet_proj` | 2:1, 0.3 long | P2 | Human rifle round, danger red-orange. |
| `PKP_COIN_Coin_spin_f6` | 0.5 cell, 0.35 high | P0 | Small gold coin/chip with an embossed gear, spinning. |
| `PKP_GRN_Grenade_world` | 1 | P1 | Grenade (thrown, in flight). |
| `PKP_MIN_Mine_world` | 1 | P2 | Flat round mine with a cyan arming light. |
| `ICO_GRN_Grenade_icon`, `ICO_PSH_PushPulse_icon`, `ICO_MIN_Mine_icon`, `ICO_EMP_EmpPulse_icon`, `ICO_SHD_Shield_icon` | 1:1 | P1 | Item icons (pulses and shield: a small device emitting the effect). |

## Skills (icons)

`ICO_SKL_Damage_icon`, `ICO_SKL_MoveSpeed_icon`, `ICO_SKL_Reload_icon`, `ICO_SKL_DashCooldown_icon`, `ICO_SKL_DashDamage_icon`, `ICO_SKL_MaxHealth_icon`, `ICO_SKL_Magnet_icon` — 1:1, P1, cyan-white symbolic icons on transparent background.

## Environment (1 unit = 128 px; footprints in units W × D)

Ground (full-bleed seamless, 8 × 8 units): `ENV_P1_Ground_ground` (dry soil and patchy grass, cut roots) P0 · `ENV_P2_Ground_ground` (metal plates and oily concrete) P2 · `ENV_P3_Ground_ground` (cracked concrete, dead grass, ash) P2.

Arena border (repeatable, transparent): `ENV_Px_BorderH_prop` 4 wide × 2 tall, tiles horizontally · `ENV_Px_BorderV_prop` 1 wide × 4 tall, tiles vertically (x = 1, 2, 3 per phase: tree line / industrial fence / security wall). P1 for phase 1, P2 for others.

| Phase | Props (blocking, footprint) | Decor (walk-over, `_a/_b/_c` variants) | Pri |
|---|---|---|---|
| P1 Animals | `Tree` 1×1 (h 4), `Rock` 2×1, `Hedge` 3×1, `Stumps` 1×1, `LogPile` 2×1, `Excavator` 3×2 (rusty, abandoned) | `GrassTuft`, `Roots`, `Leaves`, `Puddle` | P0: Tree, Rock, Hedge, GrassTuft. Rest P1 |
| P2 Robots | `CableSpool` 1×1, `MachineLit` 2×2 (lit panels), `CrateStack` 2×1, `PipeBarrier` 3×1, `Generator` 2×1 | `FloorCable`, `OilStain`, `Bolts` | P2 |
| P3 Humans | `Barricade` 3×1, `MachineBroken` 2×2, `Sandbags` 3×1, `DeadTree` 1×1, `LightPost` 1×1 (h 3) | `Ash`, `Cracks`, `Papers` | P2 |

Name them `ENV_<phase>_<Name>_prop` / `ENV_<phase>_<Name>_decor_<a|b|c>`.

Shop station: `ENV_SHOP_Kiosk_prop` 2×1, height 2.5 — a vending-terminal kiosk with a screen (no text on the screen), cyan accent light. P0.

## Effects (black background, additive) and decals (transparent)

| File | Pri | Look |
|---|---|---|
| `FX_GEN_MuzzleFlash_burst_f3` (1 cell) | P0 | Small cyan-white muzzle flash, pointing right. |
| `FX_GEN_Impact_burst_f4` (1 cell) | P0 | Spark burst. |
| `FX_GEN_Explosion_burst_f8` (3 cells) | P1 | Smoky explosion, warm but not red-orange-dominant. |
| `FX_GEN_DustPuff_burst_f5` (1 cell) | P1 | Dust puff for dash and landing. |
| `FX_DEC_Blood_decal_a..d` (1 cell, transparent) | P0 | Dark maroon splats seen from above. |
| `FX_DEC_Oil_decal_a..d` | P2 | Black oil splats. |
| `FX_DEC_Scorch_decal_a..c` | P1 | Burn marks. |

## Interface

| File | Pri | Look |
|---|---|---|
| `UI_GEN_Panel_ui` (9-slice) | P1 | Dark translucent panel, thin cyan-white border, industrial corners. |
| `UI_GEN_Button_ui_<normal/hover/pressed/disabled>` (9-slice) | P1 | Matching buttons. |
| `UI_GEN_Slot_ui` | P1 | Square item/weapon slot frame. |
| `UI_GEN_Reticle_ui` (1:1) | P0 | Thin cyan aiming reticle, open center. |
| `ICO_GEN_Coin_icon`, `ICO_GEN_Lock_icon`, `ICO_GEN_Infinity_icon` | P1 | HUD symbols. |

## Key art

| File | Pri | Look |
|---|---|---|
| `KEY_MENU_Background_keyart` | P2 | The small facility at the edge of the woods at dusk, the Operator and the AI drone in front, fog. Empty upper third for the title. |
| `KEY_END_CityLightsOn_keyart` | P2 | Wide night view of a city full of lights. |
| `KEY_END_CityLightsOff_keyart` | P2 | **The same image** with every light off (make it by editing the first image, same framing). |

The title text "HOLLOW" is set in the game with a font, not generated by AI.
