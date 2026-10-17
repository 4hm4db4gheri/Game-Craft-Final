# Hollow — Credits and asset licences

Every third-party file in the project, with source, licence and where it is used (G9, final rule 4).
Sound effects and music are listed in the Audio section below.

| File(s) | Title | Author | Source | Licence | Date added | Used for |
|---|---|---|---|---|---|---|
| `Assets/TextMesh Pro/` (LiberationSans.ttf, SDF font assets, shaders, settings) | TMP Essential Resources (ships with Unity's uGUI 2.6 package) | Unity Technologies; Liberation Sans font by Red Hat / Pascal Ascolese (Ascender Corp.) | Unity package `com.unity.ugui` → `Package Resources/TMP Essential Resources.unitypackage` | Unity Companion License (TMP resources); SIL Open Font License 1.1 for Liberation Sans (`LiberationSans - OFL.txt` included) | 2026-10-16 | Default UI font until a final font is chosen |
| `Assets/Fonts/Vazirmatn/Vazirmatn-Regular.ttf`, `Vazirmatn-Bold.ttf`; generated `Assets/Resources/Fonts/Vazirmatn SDF.asset` | Vazirmatn | Saber Rastikerdar and contributors | https://github.com/rastikerdar/vazirmatn (bundled copy in the persian-writing skill) | SIL Open Font License 1.1 (`LICENSE.txt` beside the fonts) | 2026-10-17 | Persian UI, subtitles and world-space numbers (T24) |
| `Assets/Plugins/RTLTMPro/` (Scripts, Ranges) | RTLTMPro v4.0.0 | Mohamad Narimani and contributors | https://github.com/pnarimani/RTLTMPro (tag v4.0.0, user's choice) | MIT (`LICENSE.txt` included) | 2026-10-17 | Right-to-left Persian text in TextMeshPro. Local changes: Unity 6 define in `RTLTextMeshPro3D.cs`; digits keep their separators in `LigatureFixer.cs` (D64). The repo's Segoe UI fonts and demo scenes were not copied. |

Enemy sounds in `Assets/Audio/Enemies/` (111 files, T31) are synthesised in the project by `Assets/Editor/EnemySoundSynth.cs` (no third-party material); they are placeholders for the audio owner.

All other visuals in the project are generated placeholders (simple shapes made by `Assets/Editor/PlaceholderArt.cs`), not downloaded assets.

## Audio (T19)

Downloaded 2026-10-16 with the user’s approval. The licence of each pack or file was checked on its page and, for the Kenney packs, in the `License.txt` inside the download ("Creative Commons Zero, CC0"). Files were copied unchanged and renamed to their cue; Unity import settings make effects mono and stream the music (no re-encoding tool was available).

### Music

| File | Title | Author | Source | Licence | Date | Used for |
|---|---|---|---|---|---|---|
| `Assets/Audio/Music/MusicPhase1_1.ogg` | dark_cavern_ambient_002.ogg (Dark Cavern Ambient) | Paul Wortmann | https://opengameart.org/content/dark-cavern-ambient | CC0 1.0 | 2026-10-16 | Phase 1 (animals) loop |
| `Assets/Audio/Music/MusicPhase2_1.mp3` | Infestation in the Control Room (Ambience Pack 1 -- Sci Fi Horror) | Joth | https://opengameart.org/content/ambience-pack-1-sci-fi-horror | CC0 1.0 | 2026-10-16 | Phase 2 (robots) loop |
| `Assets/Audio/Music/MusicPhase3_1.ogg` | dark_place.ogg (Dark Place (loop)) | SkyleTheFrench | https://opengameart.org/node/159643 | CC0 1.0 | 2026-10-16 | Phase 3 (humans) loop |

### Sound effects (all by Kenney, www.kenney.nl, CC0 1.0, downloaded 2026-10-16)

| File | Original file | Pack (source) | Used for (cue) |
|---|---|---|---|
| `Assets/Audio/Sfx/ShotPistol_1.ogg` | laserSmall_000.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | ShotPistol |
| `Assets/Audio/Sfx/ShotPistol_2.ogg` | laserSmall_001.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | ShotPistol |
| `Assets/Audio/Sfx/ShotPistol_3.ogg` | laserSmall_002.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | ShotPistol |
| `Assets/Audio/Sfx/ShotSmg_1.ogg` | laserSmall_003.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | ShotSmg |
| `Assets/Audio/Sfx/ShotSmg_2.ogg` | laserSmall_004.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | ShotSmg |
| `Assets/Audio/Sfx/ShotShotgun_1.ogg` | laserLarge_000.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | ShotShotgun |
| `Assets/Audio/Sfx/ShotShotgun_2.ogg` | laserLarge_001.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | ShotShotgun |
| `Assets/Audio/Sfx/ShotRifle_1.ogg` | laserLarge_002.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | ShotRifle |
| `Assets/Audio/Sfx/ShotRifle_2.ogg` | laserLarge_003.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | ShotRifle |
| `Assets/Audio/Sfx/ShotEnergy_1.ogg` | laserLarge_004.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | ShotEnergy |
| `Assets/Audio/Sfx/ShotEnergyShotgun_1.ogg` | forceField_000.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | ShotEnergyShotgun |
| `Assets/Audio/Sfx/EnemyShot_1.ogg` | laserRetro_000.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | EnemyShot |
| `Assets/Audio/Sfx/EnemyShot_2.ogg` | laserRetro_001.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | EnemyShot |
| `Assets/Audio/Sfx/EnemyShot_3.ogg` | laserRetro_002.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | EnemyShot |
| `Assets/Audio/Sfx/CompanionShot_1.ogg` | laserRetro_003.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | CompanionShot |
| `Assets/Audio/Sfx/CompanionShot_2.ogg` | laserRetro_004.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | CompanionShot |
| `Assets/Audio/Sfx/MeleeSwing_1.ogg` | knifeSlice.ogg | RPG Audio (https://kenney.nl/assets/rpg-audio) | MeleeSwing |
| `Assets/Audio/Sfx/MeleeSwing_2.ogg` | knifeSlice2.ogg | RPG Audio (https://kenney.nl/assets/rpg-audio) | MeleeSwing |
| `Assets/Audio/Sfx/ImpactFlesh_1.ogg` | impactPunch_medium_000.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | ImpactFlesh |
| `Assets/Audio/Sfx/ImpactFlesh_2.ogg` | impactPunch_medium_001.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | ImpactFlesh |
| `Assets/Audio/Sfx/ImpactFlesh_3.ogg` | impactPunch_medium_002.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | ImpactFlesh |
| `Assets/Audio/Sfx/ImpactMetal_1.ogg` | impactMetal_light_000.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | ImpactMetal |
| `Assets/Audio/Sfx/ImpactMetal_2.ogg` | impactMetal_light_001.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | ImpactMetal |
| `Assets/Audio/Sfx/ImpactMetal_3.ogg` | impactMetal_light_002.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | ImpactMetal |
| `Assets/Audio/Sfx/ImpactWall_1.ogg` | impactGeneric_light_000.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | ImpactWall |
| `Assets/Audio/Sfx/ImpactWall_2.ogg` | impactGeneric_light_001.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | ImpactWall |
| `Assets/Audio/Sfx/ImpactWall_3.ogg` | impactGeneric_light_002.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | ImpactWall |
| `Assets/Audio/Sfx/DeathAnimal_1.ogg` | impactSoft_heavy_000.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | DeathAnimal |
| `Assets/Audio/Sfx/DeathAnimal_2.ogg` | impactSoft_heavy_001.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | DeathAnimal |
| `Assets/Audio/Sfx/DeathRobot_1.ogg` | impactMetal_000.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | DeathRobot |
| `Assets/Audio/Sfx/DeathRobot_2.ogg` | impactMetal_001.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | DeathRobot |
| `Assets/Audio/Sfx/DeathHuman_1.ogg` | impactPunch_heavy_000.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | DeathHuman |
| `Assets/Audio/Sfx/DeathHuman_2.ogg` | impactPunch_heavy_001.ogg | Impact Sounds 1.0 (https://kenney.nl/assets/impact-sounds) | DeathHuman |
| `Assets/Audio/Sfx/Reload_1.ogg` | metalLatch.ogg | RPG Audio (https://kenney.nl/assets/rpg-audio) | Reload |
| `Assets/Audio/Sfx/ReloadDone_1.ogg` | metalClick.ogg | RPG Audio (https://kenney.nl/assets/rpg-audio) | ReloadDone |
| `Assets/Audio/Sfx/WeaponSwap_1.ogg` | beltHandle1.ogg | RPG Audio (https://kenney.nl/assets/rpg-audio) | WeaponSwap |
| `Assets/Audio/Sfx/Dash_1.ogg` | thrusterFire_000.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | Dash |
| `Assets/Audio/Sfx/PlayerHurt_1.ogg` | glitch_001.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | PlayerHurt |
| `Assets/Audio/Sfx/PlayerHurt_2.ogg` | glitch_002.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | PlayerHurt |
| `Assets/Audio/Sfx/CoinDrop_1.ogg` | chip-lay-1.ogg | Casino Audio 1.1 (https://kenney.nl/assets/casino-audio) | CoinDrop |
| `Assets/Audio/Sfx/CoinDrop_2.ogg` | chip-lay-2.ogg | Casino Audio 1.1 (https://kenney.nl/assets/casino-audio) | CoinDrop |
| `Assets/Audio/Sfx/CoinPickup_1.ogg` | chips-collide-1.ogg | Casino Audio 1.1 (https://kenney.nl/assets/casino-audio) | CoinPickup |
| `Assets/Audio/Sfx/CoinPickup_2.ogg` | chips-collide-2.ogg | Casino Audio 1.1 (https://kenney.nl/assets/casino-audio) | CoinPickup |
| `Assets/Audio/Sfx/CoinPickup_3.ogg` | chips-collide-3.ogg | Casino Audio 1.1 (https://kenney.nl/assets/casino-audio) | CoinPickup |
| `Assets/Audio/Sfx/Purchase_1.ogg` | handleCoins.ogg | RPG Audio (https://kenney.nl/assets/rpg-audio) | Purchase |
| `Assets/Audio/Sfx/Purchase_2.ogg` | handleCoins2.ogg | RPG Audio (https://kenney.nl/assets/rpg-audio) | Purchase |
| `Assets/Audio/Sfx/Denied_1.ogg` | error_004.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | Denied |
| `Assets/Audio/Sfx/XpReady_1.ogg` | maximize_006.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | XpReady |
| `Assets/Audio/Sfx/Upgrade_1.ogg` | confirmation_002.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | Upgrade |
| `Assets/Audio/Sfx/CompanionWarning_1.ogg` | bong_001.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | CompanionWarning |
| `Assets/Audio/Sfx/TelegraphAnimal_1.ogg` | scratch_001.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | TelegraphAnimal |
| `Assets/Audio/Sfx/TelegraphAnimal_2.ogg` | scratch_002.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | TelegraphAnimal |
| `Assets/Audio/Sfx/TelegraphRobot_1.ogg` | computerNoise_000.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | TelegraphRobot |
| `Assets/Audio/Sfx/TelegraphRobot_2.ogg` | computerNoise_001.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | TelegraphRobot |
| `Assets/Audio/Sfx/TelegraphHuman_1.ogg` | toggle_001.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | TelegraphHuman |
| `Assets/Audio/Sfx/TelegraphHuman_2.ogg` | toggle_002.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | TelegraphHuman |
| `Assets/Audio/Sfx/TelegraphBoss_1.ogg` | forceField_002.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | TelegraphBoss |
| `Assets/Audio/Sfx/Explosion_1.ogg` | explosionCrunch_000.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | Explosion |
| `Assets/Audio/Sfx/Explosion_2.ogg` | explosionCrunch_001.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | Explosion |
| `Assets/Audio/Sfx/Explosion_3.ogg` | explosionCrunch_002.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | Explosion |
| `Assets/Audio/Sfx/GrenadeThrow_1.ogg` | drop_001.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | GrenadeThrow |
| `Assets/Audio/Sfx/Push_1.ogg` | forceField_003.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | Push |
| `Assets/Audio/Sfx/MinePlace_1.ogg` | tick_002.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | MinePlace |
| `Assets/Audio/Sfx/Emp_1.ogg` | lowFrequency_explosion_001.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | Emp |
| `Assets/Audio/Sfx/Shield_1.ogg` | forceField_004.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | Shield |
| `Assets/Audio/Sfx/WaveStart_1.ogg` | jingles_HIT00.ogg | Music Jingles (https://kenney.nl/assets/music-jingles) | WaveStart |
| `Assets/Audio/Sfx/WaveClear_1.ogg` | jingles_HIT05.ogg | Music Jingles (https://kenney.nl/assets/music-jingles) | WaveClear |
| `Assets/Audio/Sfx/BossEntry_1.ogg` | lowFrequency_explosion_000.ogg | Sci-Fi Sounds 1.0 (https://kenney.nl/assets/sci-fi-sounds) | BossEntry |
| `Assets/Audio/Sfx/BossDefeated_1.ogg` | jingles_HIT14.ogg | Music Jingles (https://kenney.nl/assets/music-jingles) | BossDefeated |
| `Assets/Audio/Sfx/UiClick_1.ogg` | click_002.ogg | Interface Sounds 1.0 (https://kenney.nl/assets/interface-sounds) | UiClick |
| `Assets/Audio/Sfx/Victory_1.ogg` | jingles_SAX07.ogg | Music Jingles (https://kenney.nl/assets/music-jingles) | Victory |

Total audio in the project: 5.50 MB on disk (71 effect files, 3 music loops).
