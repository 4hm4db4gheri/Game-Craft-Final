# Arena composition — 2026-10-18

The two reference screenshots use upright three-quarter props on a composed orthographic floor. Their depth comes from visible top/front faces, ground contact, connected paths and foreground overlap. Rotating the Unity camera over coplanar sprites would also flatten the upright character art. This pass keeps the existing coordinate system and replaces the repeating floor presentation.

## Implemented

- `Resources/ArenaArt/FloorForest.png`: full-arena 3:2 ground artwork, connected dirt lanes and grass islands. No 8×8 texture repeat.
- `Resources/ArenaArt/FloorIndustry.png`: full-arena industrial floor with maintenance lanes and embedded cable channels. Phase three uses a colder tinted version of this floor.
- Each image covers the existing 48×32 arena in world coordinates. Obstacles, starts, spawn zones, aiming and saved positions keep their existing coordinates. Painted ground details are decoration and do not block movement.
- Smaller footprint-based prop contact shadows replace oversized dark ovals.
- `Resources/ArenaArt/ForestProps.png`: original transparent three-quarter stump, log pile and excavator sprites replace the three remaining forest prop placeholders. Their alpha bounds were inspected and the atlas rectangles keep each asset separate; bottom-center pivots anchor them to existing obstacle footprints.
- Tall foreground props fade smoothly while covering the player, retaining the depth overlap without hiding the controlled character.
- `Hollow > Visual Tuning`: `Composed arena floor` switches back to the previous tiles for comparison; `Prop contact shadows` controls contact opacity. Apply during Play Mode to compare; Save stores defaults for new games.

## تست در یونیتی

1. Play فعلی را متوقف کنید و صبر کنید import و compile تمام شود. یک بازی تازه شروع کنید.
2. نزدیک درخت، سنگ و فروشگاه حرکت کنید. طرح مسیر باید پیوسته باشد و در هر چند قدم مثل فرش تکرار نشود؛ پایهٔ اجسام باید روی زمین بنشیند.
3. از پشت یک درخت رد شوید: تصویر درخت باید نرم محو شود و بعد از خروج دوباره برگردد.
4. `Hollow > Visual Tuning` را باز کنید؛ `Composed arena floor` را خاموش و Apply کنید تا زمین قدیمی دیده شود. دوباره روشن و Apply کنید؛ برای نگه‌داشتن تنظیمات Save را بزنید.
5. در F10 فاز صنعتی و انسانی را ببینید؛ جا و برخورد موانع و نشانه‌گیری را بررسی کنید.

Code compilation was checked with the installed Unity Roslyn compiler. Actual Play Mode rendering is not confirmed because the current Editor connection is unavailable. The new art itself was inspected. This is a composition and prop-grounding pass, not a conversion to a 3D/isometric simulation.

## Generation record

Built-in imagegen was used. Original generated outputs remain under Codex generated_images; the copies consumed by the project are under `Assets/Resources/ArenaArt`.

Forest prompt: Production game asset, not a screenshot; wide 3:2 full-arena floor texture for Hollow, hand-painted high three-quarter overhead forest extraction outpost, muted olive grass islands and warm dusty earth. Cover a 48×32 arena without repeating tiles. Broad asymmetrical winding dirt lanes connect four sides and an open central clearing. Grass and roots toward margins, small flat stones, quiet midtones, diffuse upper-left light, vertically compressed surface motifs matching upright three-quarter cutout props. No horizon, grid, characters, tall trees, buildings, barriers, kiosks, text, UI, pits, water, stairs, raised terrain, black shadow blobs, photorealism or pixel art. Opaque walkable edge-to-edge artwork.

Industry prompt: Production game floor asset, wide 3:2, hand-painted high three-quarter overhead industrial shooter yard. Asymmetric worn grey concrete with branching dusty service lanes, irregular dead-grass islands toward margins, embedded cable channels, old muted ochre maintenance markings without text and tiny flat debris. Generous open central combat clearing with four connected exits. Quiet slate grey/brown/olive midtones, upper-left diffuse light. No structures, machines, characters, obstacles, cast-shadow blobs, repeating tiles, UI, text, pits, water, platforms or stairs. All artwork is walkable ground.

Prop prompt: Production 2D game prop atlas, exactly three separate cells in one horizontal row on a transparent background. Left: low cluster of two cut stumps and wood chips. Middle: low stacked rough timber logs with visible ends and side grain. Right: compact abandoned yellow excavator with grounded tracks, cab, folded boom and bucket contained in its cell. Hand-painted three-quarter overhead art with visible top/front faces, upright verticals, diffuse upper-left light, muted weathered olive/brown wood and ochre metal, dark fine outlines and clean cutout silhouettes. No ground plane, shadow blobs, text, UI, border, photorealism or pixel art.
