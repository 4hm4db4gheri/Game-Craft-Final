# Hollow — art wave 2 (additional files)

Copy `Assets/Art/Incoming/` into the project next to wave 1, then run Hollow > Art > Apply Incoming Art. 43 PNGs + ASSET_CARDS.txt.

New enemies (single frames, same framing as wave 1):
A3 ChargingBoar (idle, move, windup, attack=charge, recover, death) · A4 ArmoredTortoise · A5 HowlingWolf (howl instead of attack) · A6 ExplosiveBeetle (no attack frame, explodes in code) · A7c Spiderling · H1 BatonGuard.

First animation sheets: `ENM_A1_RunnerWolf_move_f6.png` (3x2) and `ENM_A7c_Spiderling_move_f8.png` (4x2), cells 1024 px, scaled to match the single frames.

New file names to wire up: PRJ_ENM_Grenade_proj, FX_DEC_Blood_decal_a/b, FX_DEC_Oil_decal_a/b, UI_PRT_Operator_P1_ui, UI_PRT_Operator_P2_ui, UI_PRT_AI_ui, ICO_APP_Hollow_icon, ENV_P1/P2/P3_Entrance_prop (footprint 2x1).

Known issues: BatonGuard windup baton touches the top edge; ExplosiveBeetle frames have a few magenta pixels at the edges; entrance props are rough cut-outs.

Still missing (enemies shown as cones): A7 MotherSpider, R3–R7, R7c, H3–H7, H7c, Boss 3.
