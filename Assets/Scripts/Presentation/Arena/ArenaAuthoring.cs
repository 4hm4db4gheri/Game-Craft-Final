using System.Collections.Generic;
using Hollow.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;

namespace Hollow.Presentation
{
    /// <summary>
    /// Arena authoring in the Game scene: the designer paints Tilemaps, moves props and markers; the Editor bakes
    /// them into Resources/Arena/ArenaLayout.json, which is what the simulation uses (D13).
    /// Walls: blocked in every phase. Per phase i+1: ground layer, decor, props (blocking) and an optional
    /// obstacle Tilemap for extra painted blocking cells.
    /// </summary>
    public sealed class ArenaAuthoring : MonoBehaviour
    {
        public Grid Grid;
        public Tilemap Walls;
        public Tilemap[] Grounds = new Tilemap[3];
        public Tilemap[] Obstacles = new Tilemap[3];
        public Transform[] Props = new Transform[3];
        public Transform[] Decor = new Transform[3];
        /// <summary>Imported border art around the walls, per phase (visual only; the Walls Tilemap blocks).</summary>
        public Transform[] Borders = new Transform[3];
        public Vector2Int Size = new(48, 32);

        public Transform PlayerStart;
        public Transform CompanionStart;
        public Transform Shop;
        /// <summary>More service stations (v0.19 §13: two by default; count and place are the artist's).</summary>
        public List<Transform> ExtraShops = new();
        public Vector2Int ShopFootprint = new(2, 1);

        public IEnumerable<Transform> AllShops()
        {
            if (Shop != null) yield return Shop;
            foreach (var s in ExtraShops) if (s != null) yield return s;
        }

        public List<SpawnZone> SpawnZones = new();
        readonly SpriteRenderer[] composedFloors = new SpriteRenderer[3];
        readonly List<Sprite> ownedFloorSprites = new();
        readonly List<Material> ownedGroundMaterials = new();
        readonly List<(SpriteRenderer Renderer, Transform Anchor, Color Color)> foreground = new();
        bool landmarksBuilt;

        public void BuildLandmarks(ArenaLayout layout)
        {
            if (landmarksBuilt || layout.Landmarks == null) return;
            landmarksBuilt = true;
            foreach (var spec in layout.Landmarks)
            {
                if (spec.Phase < 1 || spec.Phase > Props.Length || Props[spec.Phase - 1] == null) continue;
                var texture = Resources.Load<Texture2D>("ArenaArt/Landmarks/ENV_P1_" + spec.Id + "_prop");
                if (texture == null) { Debug.LogWarning("[Hollow] Missing landmark art: " + spec.Id); continue; }
                var root = new GameObject(spec.Id);
                root.transform.SetParent(Props[spec.Phase - 1], false);
                root.transform.position = (Vector3)(layout.Origin + (Vector2)spec.Cell + new Vector2(spec.Footprint.x * 0.5f, 0));
                root.AddComponent<SortingGroup>();
                var prop = root.AddComponent<ArenaProp>();
                prop.Phase = spec.Phase; prop.PropName = spec.Id; prop.Cell = spec.Cell; prop.Footprint = spec.Footprint;
                // This box matches the simulation grid footprint, not the roof/smoke above it.
                for (int y = 0; y < spec.Footprint.y; y++)
                {
                    int x = 0;
                    while (x < spec.Footprint.x)
                    {
                        if (!spec.Occupies(x, y)) { x++; continue; }
                        int first = x++;
                        while (x < spec.Footprint.x && spec.Occupies(x, y)) x++;
                        var collider = root.AddComponent<BoxCollider2D>();
                        collider.offset = new Vector2((first + x - spec.Footprint.x) * 0.5f, y + 0.5f);
                        collider.size = new Vector2(x - first, 1);
                    }
                }
                var c = spec.ArtCrop;
                var crop = Rect.MinMaxRect(c.x * texture.width, c.y * texture.height,
                    Mathf.Min(texture.width, (c.x + c.z) * texture.width), Mathf.Min(texture.height, (c.y + c.w) * texture.height));
                var sprite = Sprite.Create(texture, crop, new Vector2(0.5f, 0), crop.width / spec.ArtWidth, 0, SpriteMeshType.FullRect);
                ownedFloorSprites.Add(sprite);
                var body = new GameObject("Body"); body.transform.SetParent(root.transform, false);
                body.transform.localPosition = Vector3.down * spec.GroundInset;
                var renderer = body.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                var groundRenderer = Grounds[spec.Phase - 1].GetComponent<TilemapRenderer>();
                if (groundRenderer.sharedMaterial != null) renderer.sharedMaterial = groundRenderer.sharedMaterial;
            }
        }

        /// <summary>Full-arena artwork creates connected paths instead of a repeating carpet of tiles.</summary>
        public void ApplyComposition(bool enabled, float shadowStrength)
        {
            var propAtlas = Resources.Load<Texture2D>("ArenaArt/ForestProps");
            for (int i = 0; i < Grounds.Length; i++)
            {
                var map = Grounds[i];
                if (map == null) continue;
                var tiles = map.GetComponent<TilemapRenderer>();
                if (enabled && composedFloors[i] == null)
                {
                    string asset = i == 0 ? "Forest" : "Industry";
                    var texture = Resources.Load<Texture2D>("ArenaArt/Floor" + asset);
                    if (texture != null)
                    {
                        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                        ownedFloorSprites.Add(sprite);
                        var go = new GameObject("Composed arena floor");
                        go.transform.SetParent(map.transform, false);
                        go.transform.localPosition = new Vector3(Size.x * 0.5f, Size.y * 0.5f, 0);
                        go.transform.localScale = new Vector3(Size.x / sprite.bounds.size.x, Size.y / sprite.bounds.size.y, 1);
                        var renderer = go.AddComponent<SpriteRenderer>();
                        renderer.sprite = sprite;
                        if (tiles.sharedMaterial != null) renderer.sharedMaterial = tiles.sharedMaterial;
                        var template = Resources.Load<Material>("ArenaArt/DetailedGround");
                        if (template != null && template.shader.isSupported)
                        {
                            var material = new Material(template);
                            var detail = Resources.Load<Texture2D>("ArenaArt/Detail" + asset);
                            if (detail != null) material.SetTexture("_DetailTex", detail);
                            renderer.sharedMaterial = material;
                            ownedGroundMaterials.Add(material);
                        }
                        renderer.sortingLayerID = tiles.sortingLayerID;
                        renderer.sortingOrder = tiles.sortingOrder;
                        renderer.color = i == 0 ? new Color(0.88f, 0.9f, 0.87f) : i == 2 ? new Color(0.87f, 0.88f, 0.9f) : Color.white;
                        composedFloors[i] = renderer;
                    }
                }
                bool useArt = enabled && composedFloors[i] != null;
                tiles.enabled = !useArt;
                if (composedFloors[i] != null) composedFloors[i].gameObject.SetActive(useArt);
            }
            // Keep the ground contact small; the old tall ovals made props appear to float over black stains.
            foreach (var phaseProps in Props)
            {
                if (phaseProps == null) continue;
                foreach (var prop in phaseProps.GetComponentsInChildren<ArenaProp>(true))
                {
                    ApplyPropArt(prop, propAtlas);
                    var shadow = prop.transform.Find("Shadow");
                    if (shadow == null) continue;
                    shadow.localPosition = new Vector3(0, prop.Footprint.y * 0.22f, 0);
                    shadow.localScale = new Vector3(prop.Footprint.x * 1.15f, prop.Footprint.y * 0.72f, 1);
                    var renderer = shadow.GetComponent<SpriteRenderer>();
                    if (renderer != null) renderer.color = new Color(1, 1, 1, Mathf.Clamp01(shadowStrength));
                }
            }
            if (foreground.Count == 0)
                foreach (var phaseProps in Props)
                {
                    if (phaseProps == null) continue;
                    foreach (var prop in phaseProps.GetComponentsInChildren<ArenaProp>(true))
                    {
                        var body = prop.transform.Find("Body");
                        if (body == null) continue;
                        foreach (var renderer in body.GetComponentsInChildren<SpriteRenderer>(true))
                            if (renderer.enabled && renderer.sprite != null && renderer.bounds.size.y > 1.5f)
                                foreground.Add((renderer, prop.transform, renderer.color));
                    }
                }
        }

        void ApplyPropArt(ArenaProp prop, Texture2D atlas)
        {
            if (atlas == null || prop.Phase != 1) return;
            var body = prop.transform.Find("Body");
            if (body == null || body.Find("RefinedProp") != null) return;
            Rect crop;
            float width;
            switch (prop.PropName)
            {
                case "Stumps": crop = new Rect(22, 81, 654, 393); width = 1.3f; break;
                case "LogPile": crop = new Rect(724, 78, 675, 426); width = 2.2f; break;
                case "Excavator": crop = new Rect(1438, 83, 713, 601); width = 3.6f; break;
                default: return;
            }
            Material material = null;
            foreach (var old in body.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (material == null) material = old.sharedMaterial;
            }
            crop = ImportedAtlasRect(crop, atlas.width, atlas.height);
            var sprite = Sprite.Create(atlas, crop, new Vector2(0.5f, 0), crop.width / width, 0, SpriteMeshType.FullRect);
            foreach (var old in body.GetComponentsInChildren<SpriteRenderer>(true)) old.enabled = false;
            ownedFloorSprites.Add(sprite);
            var go = new GameObject("RefinedProp");
            go.transform.SetParent(body, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (material != null) renderer.sharedMaterial = material;
        }

        /// <summary>Unity may downscale the atlas for quality/platform settings. Keep crops in source coordinates.</summary>
        public static Rect ImportedAtlasRect(Rect source, int width, int height)
        {
            float sx = width / 2172f, sy = height / 724f;
            return Rect.MinMaxRect(Mathf.Clamp(source.xMin * sx, 0, width), Mathf.Clamp(source.yMin * sy, 0, height),
                Mathf.Clamp(source.xMax * sx, 0, width), Mathf.Clamp(source.yMax * sy, 0, height));
        }

        /// <summary>Keep the depth overlap while revealing the player behind a tall foreground prop.</summary>
        public void SyncForeground(Vector2 playerPosition)
        {
            foreach (var entry in foreground)
            {
                var renderer = entry.Renderer;
                if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
                var bounds = renderer.bounds;
                Vector2 probe = playerPosition + Vector2.up * 0.55f;
                bool behind = playerPosition.y > entry.Anchor.position.y &&
                    probe.x > bounds.min.x - 0.2f && probe.x < bounds.max.x + 0.2f &&
                    probe.y > bounds.min.y && probe.y < bounds.max.y;
                var color = entry.Color;
                color.a = Mathf.MoveTowards(renderer.color.a, entry.Color.a * (behind ? 0.38f : 1f), Time.unscaledDeltaTime * 4f);
                renderer.color = color;
            }
        }

        void OnDestroy()
        {
            foreach (var sprite in ownedFloorSprites) if (sprite != null) Destroy(sprite);
            foreach (var material in ownedGroundMaterials) if (material != null) Destroy(material);
        }

        long brokenSignature = long.MinValue;
        readonly List<(Tilemap Map, Vector3Int Cell, TileBase Tile)> removedTiles = new();
        readonly HashSet<int> brokenSet = new();
        readonly List<GameObject> hiddenProps = new();

        /// <summary>
        /// D87: hides the props and painted obstacle tiles a boss charge broke (the simulation's
        /// <see cref="World.BrokenCells"/>), and brings them back when a checkpoint or another phase no longer has them broken.
        /// </summary>
        public void SyncBroken(int phase, List<int> broken, int width)
        {
            long signature = phase * 1000003L + broken.Count;
            foreach (int c in broken) signature = signature * 31 + c;
            if (signature == brokenSignature) return;
            brokenSignature = signature;
            brokenSet.Clear();
            foreach (int c in broken) brokenSet.Add(c);

            foreach (var (map, cell, tile) in removedTiles) if (map != null) map.SetTile(cell, tile);
            removedTiles.Clear();
            foreach (var go in hiddenProps) if (go != null) go.SetActive(true);
            hiddenProps.Clear();
            if (brokenSet.Count == 0) return;
            for (int i = 0; i < Props.Length; i++)
            {
                if (Props[i] == null) continue;
                foreach (var prop in Props[i].GetComponentsInChildren<ArenaProp>(true))
                {
                    if (prop.Phase != phase || !prop.gameObject.activeSelf) continue;
                    foreach (var c in prop.Rect.allPositionsWithin)
                        if (brokenSet.Contains(c.y * width + c.x))
                        {
                            prop.gameObject.SetActive(false);
                            hiddenProps.Add(prop.gameObject);
                            break;
                        }
                }
            }
            var obstacles = phase >= 1 && phase <= Obstacles.Length ? Obstacles[phase - 1] : null;
            if (obstacles == null) return;
            foreach (int c in brokenSet)
            {
                var cell = new Vector3Int(c % width, c / width, 0);
                var tile = obstacles.GetTile(cell);
                if (tile == null) continue;
                removedTiles.Add((obstacles, cell, tile));
                obstacles.SetTile(cell, null);
            }
        }

        /// <summary>Shows the ground, decor, props, border and obstacle layer of the given phase only.</summary>
        public void ShowPhase(int phase)
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = i == phase - 1;
                if (Grounds.Length > i && Grounds[i] != null) Grounds[i].gameObject.SetActive(on);
                if (Obstacles.Length > i && Obstacles[i] != null) Obstacles[i].gameObject.SetActive(on);
                if (Props.Length > i && Props[i] != null) Props[i].gameObject.SetActive(on);
                if (Decor.Length > i && Decor[i] != null) Decor[i].gameObject.SetActive(on);
                if (Borders != null && Borders.Length > i && Borders[i] != null) Borders[i].gameObject.SetActive(on);
            }
        }

        /// <summary>Breaks the exact 8-by-8 art repeat with smooth, non-periodic ground brightness.</summary>
        public void ApplyGroundVariation(float strength)
        {
            strength = Mathf.Clamp(strength, 0f, 0.22f);
            foreach (var map in Grounds)
            {
                if (map == null) continue;
                var bounds = map.cellBounds;
                foreach (var cell in bounds.allPositionsWithin)
                {
                    if (!map.HasTile(cell)) continue;
                    float broad = Mathf.PerlinNoise((cell.x + 43f) * 0.075f, (cell.y + 97f) * 0.075f);
                    float detail = Mathf.PerlinNoise((cell.x + 211f) * 0.19f, (cell.y + 17f) * 0.19f);
                    float value = 1f + ((broad * 0.75f + detail * 0.25f) - 0.5f) * 2f * strength;
                    map.SetTileFlags(cell, TileFlags.None);
                    map.SetColor(cell, new Color(value, value, value, 1f));
                }
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            foreach (var z in SpawnZones)
                Gizmos.DrawCube((Vector3)z.Center, (Vector3)(z.Max - z.Min));
        }
    }
}
