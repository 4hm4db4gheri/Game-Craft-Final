using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hollow.Presentation;
using Hollow.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Hollow.EditorTools
{
    /// <summary>
    /// The arena in three states (D13, §14): 48 × 32 cells, four edge spawn zones, shop south of centre, and per
    /// phase its own ground, walk-over decor and named blocking props (docs/ART_ASSET_LIST.md). Props are prefabs
    /// (PH_Prop_P&lt;n&gt;_&lt;Name&gt;) so imported AI art replaces them; their footprints are baked into
    /// ArenaLayout.json, which is the only thing the simulation reads.
    /// </summary>
    public static class ArenaTools
    {
        public const string TilesDir = "Assets/Art/Placeholders/Tiles";
        public const string ArtTilesDir = "Assets/Art/Tiles";
        public const string LayoutPath = "Assets/Resources/Arena/ArenaLayout.json";
        /// <summary>The second service station: a spot free in all three phases, 8 units from the east entrance (T28).</summary>
        public const string SecondShopName = "ShopStation2";
        public static readonly Vector2 SecondShopPosition = new(12f, 0.5f);

        /// <summary>T28: adds the second service station (marker and kiosk) to the open Game scene, then bakes.</summary>
        [MenuItem("Hollow/Arena/Add Second Service Station")]
        public static string AddSecondShopToOpenScene()
        {
            var a = Object.FindAnyObjectByType<ArenaAuthoring>(FindObjectsInactive.Include);
            if (a == null) return "No ArenaAuthoring in the open scene.";
            if (a.ExtraShops.Exists(s => s != null)) return "A second station already exists.";
            var marker = Marker(a.Shop.parent.gameObject, SecondShopName, SecondShopPosition);
            a.ExtraShops = new List<Transform> { marker };
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Placeholders/PH_ShopStation.prefab");
            var kiosk = FindKiosk(a.Shop.position);
            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, kiosk != null ? kiosk.parent : null);
                go.name = "PH_ShopStation (2)";
                go.transform.position = marker.position + Vector3.down * 0.5f;
            }
            EditorUtility.SetDirty(a);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(a.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(a.gameObject.scene);
            Bake(a);
            return $"Second station at {marker.position}; prefab {(prefab != null ? "placed" : "missing")}.";
        }

        static Transform FindKiosk(Vector3 shop)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name.StartsWith("PH_ShopStation") && Vector2.Distance(t.position, shop) < 1.5f) return t;
            return null;
        }
        static readonly Vector2Int ArenaSize = new(48, 32);

        enum Shape { Box, Round, Tree, Post }

        sealed class PropSpec
        {
            public string Name;
            public int W, D;
            public float Height;
            public Shape Shape;
            public Color Main, Top;
            public Color? Glow;
        }

        static PropSpec P(string name, int w, int d, float h, Shape s, Color main, Color top, Color? glow = null) =>
            new() { Name = name, W = w, D = d, Height = h, Shape = s, Main = main, Top = top, Glow = glow };

        // Footprints and heights from docs/ART_ASSET_LIST.md.
        static readonly PropSpec[][] Props =
        {
            new[] // Animals: soil, grass, rocks, trees, limited extraction
            {
                P("Tree", 1, 1, 4f, Shape.Tree, new(0.33f, 0.25f, 0.18f), new(0.24f, 0.34f, 0.2f)),
                P("Rock", 2, 1, 1.2f, Shape.Round, new(0.42f, 0.41f, 0.38f), new(0.55f, 0.54f, 0.5f)),
                P("Hedge", 3, 1, 1.4f, Shape.Box, new(0.2f, 0.3f, 0.17f), new(0.28f, 0.4f, 0.22f)),
                P("Stumps", 1, 1, 0.6f, Shape.Round, new(0.4f, 0.3f, 0.2f), new(0.62f, 0.52f, 0.36f)),
                P("LogPile", 2, 1, 0.9f, Shape.Box, new(0.38f, 0.28f, 0.19f), new(0.55f, 0.43f, 0.3f)),
                P("Excavator", 3, 2, 2.4f, Shape.Box, new(0.5f, 0.36f, 0.16f), new(0.62f, 0.46f, 0.22f)),
            },
            new[] // Robots: cables, metal, lit machines
            {
                P("CableSpool", 1, 1, 1f, Shape.Round, new(0.3f, 0.26f, 0.22f), new(0.42f, 0.38f, 0.32f)),
                P("MachineLit", 2, 2, 2.2f, Shape.Box, new(0.34f, 0.37f, 0.4f), new(0.46f, 0.49f, 0.52f), new Color(0.49f, 1f, 0.42f)),
                P("CrateStack", 2, 1, 1.6f, Shape.Box, new(0.42f, 0.4f, 0.34f), new(0.55f, 0.52f, 0.44f)),
                P("PipeBarrier", 3, 1, 1.1f, Shape.Box, new(0.36f, 0.38f, 0.4f), new(0.5f, 0.52f, 0.55f)),
                P("Generator", 2, 1, 1.4f, Shape.Box, new(0.3f, 0.33f, 0.3f), new(0.42f, 0.45f, 0.4f), new Color(0.49f, 1f, 0.42f)),
            },
            new[] // Humans: security state, barricades, damaged machines, withered nature
            {
                P("Barricade", 3, 1, 1.2f, Shape.Box, new(0.26f, 0.27f, 0.3f), new(0.38f, 0.39f, 0.42f)),
                P("MachineBroken", 2, 2, 1.8f, Shape.Box, new(0.27f, 0.27f, 0.28f), new(0.36f, 0.35f, 0.34f)),
                P("Sandbags", 3, 1, 0.9f, Shape.Box, new(0.45f, 0.41f, 0.3f), new(0.56f, 0.52f, 0.39f)),
                P("DeadTree", 1, 1, 3.2f, Shape.Tree, new(0.26f, 0.23f, 0.2f), new(0.3f, 0.27f, 0.23f)),
                P("LightPost", 1, 1, 3f, Shape.Post, new(0.22f, 0.23f, 0.26f), new(0.7f, 0.85f, 1f), new Color(0.65f, 0.8f, 1f)),
            },
        };

        /// <summary>
        /// Prop placement per phase (lower-left cell). Rules checked by ArenaLayoutTests: at least two free cells
        /// between any two obstacles and between an obstacle and the wall, spawn zones, starts and the shop.
        /// </summary>
        static readonly (string Name, int X, int Y)[][] Layout =
        {
            new[] // Animals: open readable tutorial space, cover at the edges
            {
                ("Hedge", 9, 22), ("Hedge", 36, 9), ("Rock", 14, 7), ("Rock", 32, 23), ("Tree", 8, 15), ("Tree", 40, 17),
                ("Tree", 20, 25), ("Tree", 28, 6), ("Stumps", 17, 19), ("LogPile", 30, 13), ("Excavator", 37, 21),
            },
            new[] // Robots: lanes and cover for ranged threats
            {
                ("MachineLit", 12, 20), ("MachineLit", 34, 10), ("PipeBarrier", 17, 8), ("PipeBarrier", 27, 24),
                ("CrateStack", 8, 10), ("CrateStack", 38, 21), ("CableSpool", 16, 26), ("CableSpool", 36, 5),
                ("Generator", 19, 15), ("Generator", 28, 16),
            },
            new[] // Humans: barricade lines for crossfire
            {
                ("Barricade", 10, 12), ("Barricade", 35, 19), ("Barricade", 20, 24), ("Barricade", 28, 6),
                ("Sandbags", 15, 19), ("Sandbags", 30, 11), ("MachineBroken", 38, 25), ("DeadTree", 6, 25),
                ("DeadTree", 41, 6), ("LightPost", 13, 4), ("LightPost", 36, 28), ("LightPost", 8, 6), ("LightPost", 23, 21),
            },
        };

        // Ground: base tint per phase plus soft-edged patches (grass, oily concrete, ash) laid on top as sprites,
        // so areas read as organic shapes instead of a checkerboard of cells.
        static readonly (string Sprite, Color Base, Color Patch)[] Grounds =
        {
            ("PH_Speckle", new(0.27f, 0.23f, 0.17f), new(0.2f, 0.27f, 0.14f)),  // dry soil, patchy grass
            ("PH_Plate", new(0.27f, 0.29f, 0.31f), new(0.12f, 0.12f, 0.12f)),   // metal plates, oily concrete
            ("PH_Speckle", new(0.17f, 0.17f, 0.18f), new(0.25f, 0.23f, 0.16f)), // cracked concrete, ash, dead grass
        };

        static readonly (string Name, string Sprite, Color Color, float Size, int Count)[][] DecorSpecs =
        {
            new[] { ("GrassTuft", "PH_Tuft", new Color(0.34f, 0.44f, 0.24f), 0.7f, 40), ("Roots", "PH_Crack", new Color(0.36f, 0.27f, 0.18f), 1.4f, 12), ("Leaves", "PH_Blob", new Color(0.45f, 0.3f, 0.17f, 0.7f), 0.5f, 18), ("Puddle", "PH_Blob", new Color(0.16f, 0.2f, 0.24f, 0.7f), 1.3f, 6) },
            new[] { ("FloorCable", "PH_Crack", new Color(0.1f, 0.1f, 0.1f), 2f, 16), ("OilStain", "PH_Blob", new Color(0.05f, 0.05f, 0.06f, 0.55f), 1.5f, 14), ("Bolts", "PH_Circle", new Color(0.55f, 0.57f, 0.6f), 0.12f, 30) },
            new[] { ("Ash", "PH_Blob", new Color(0.32f, 0.31f, 0.3f, 0.6f), 1.6f, 16), ("Cracks", "PH_Crack", new Color(0.07f, 0.07f, 0.08f), 1.8f, 22), ("Papers", "PH_Square", new Color(0.7f, 0.68f, 0.6f), 0.22f, 18) },
        };

        static readonly Color WallColor = new(0.08f, 0.08f, 0.09f);

        public static Tile LoadOrCreateTile(string name, string sprite, Color color) => LoadOrCreateTile(TilesDir, name, PlaceholderArt.Load(sprite), color);

        static Tile LoadOrCreateTile(string dir, string name, Sprite sprite, Color color)
        {
            Directory.CreateDirectory(dir);
            string path = $"{dir}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.color = color;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        // ------------------------------------------------------------------ build

        /// <summary>Creates the Grid, Tilemaps, props, decor and markers in the open scene.</summary>
        public static ArenaAuthoring BuildWhitebox()
        {
            var size = ArenaSize;
            var gridGo = new GameObject("Arena");
            gridGo.transform.position = new Vector3(-size.x / 2f, -size.y / 2f, 0);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var a = gridGo.AddComponent<ArenaAuthoring>();
            a.Grid = grid;
            a.Size = size;

            a.Walls = NewTilemap(gridGo, "Walls", 0, TilemapRenderer.Mode.Individual);
            var wall = LoadOrCreateTile("PH_Tile_Wall", "PH_Square", WallColor);
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                if (x == 0 || y == 0 || x == size.x - 1 || y == size.y - 1) a.Walls.SetTile(new Vector3Int(x, y, 0), wall);

            a.Grounds = new Tilemap[3];
            a.Obstacles = new Tilemap[3];
            a.Props = new Transform[3];
            a.Decor = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                a.Grounds[i] = NewTilemap(gridGo, $"Ground_P{i + 1}", -100, TilemapRenderer.Mode.Chunk);
                a.Obstacles[i] = NewTilemap(gridGo, $"Obstacles_P{i + 1} (extra painted blocking)", 0, TilemapRenderer.Mode.Individual);
                a.Decor[i] = new GameObject($"Decor_P{i + 1}").transform;
                a.Decor[i].SetParent(gridGo.transform, false);
                a.Props[i] = new GameObject($"Props_P{i + 1}").transform;
                a.Props[i].SetParent(gridGo.transform, false);
            }
            PaintGround(a);

            var prefabs = BuildPropPrefabs();
            for (int phase = 1; phase <= 3; phase++)
                foreach (var (name, x, y) in Layout[phase - 1])
                {
                    var spec = Props[phase - 1].First(p => p.Name == name);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[(phase, name)], a.Props[phase - 1]);
                    var prop = go.GetComponent<ArenaProp>();
                    prop.Cell = new Vector2Int(x, y);
                    go.name = $"{name} ({x},{y})";
                    go.transform.position = CellToWorld(a, x + spec.W / 2f, y);
                }

            a.PlayerStart = Marker(gridGo, "PlayerStart", new Vector2(0, 0));
            a.CompanionStart = Marker(gridGo, "CompanionStart", new Vector2(-1.5f, -0.6f));
            a.Shop = Marker(gridGo, "ShopStation", new Vector2(0, -6.5f));
            a.ExtraShops = new List<Transform> { Marker(gridGo, SecondShopName, SecondShopPosition) }; // v0.19 §13
            a.ShopFootprint = new Vector2Int(2, 1);

            float hx = size.x / 2f, hy = size.y / 2f;
            a.SpawnZones = new List<SpawnZone>
            {
                new() { Side = SpawnSide.North, Min = new Vector2(-8, hy - 4), Max = new Vector2(8, hy - 2) },
                new() { Side = SpawnSide.East, Min = new Vector2(hx - 4, -6), Max = new Vector2(hx - 2, 6) },
                new() { Side = SpawnSide.South, Min = new Vector2(-8, -hy + 2), Max = new Vector2(8, -hy + 4) },
                new() { Side = SpawnSide.West, Min = new Vector2(-hx + 2, -6), Max = new Vector2(-hx + 4, 6) },
            };
            ScatterDecor(a, prefabs);
            a.ShowPhase(1);
            return a;
        }

        static Vector3 CellToWorld(ArenaAuthoring a, float x, float y) => a.Grid.transform.position + new Vector3(x, y, 0);

        /// <summary>
        /// Paints each phase's ground: imported art (ENV_P&lt;n&gt;_Ground_ground, 8 × 8 tiles) when present,
        /// otherwise tinted placeholder tiles plus soft placeholder patches (removed again once art exists).
        /// </summary>
        public static void PaintGround(ArenaAuthoring a)
        {
            for (int i = 0; i < 3; i++)
            {
                var map = a.Grounds[i];
                map.ClearAllTiles();
                var art = GroundArtTiles(i + 1);
                var g = Grounds[i];
                var baseTiles = new Tile[3];
                for (int k = 0; k < 3; k++)
                    baseTiles[k] = LoadOrCreateTile($"PH_Tile_P{i + 1}_Base_{k}", g.Sprite, Shade(g.Base, 0.98f + k * 0.02f));
                var rng = new System.Random(17 + i);
                for (int y = 0; y < a.Size.y; y++)
                for (int x = 0; x < a.Size.x; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    map.SetTile(cell, art != null ? art[x % 8, y % 8] : baseTiles[rng.Next(3)]);
                }

                var old = map.transform.Find("PlaceholderPatches");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                if (art != null) continue;
                var patches = new GameObject("PlaceholderPatches").transform;
                patches.SetParent(map.transform, false);
                var blob = PlaceholderArt.Load("PH_Blob");
                for (int k = 0; k < 34; k++)
                {
                    var go = new GameObject($"Patch_{k}");
                    go.transform.SetParent(patches, false);
                    go.transform.localPosition = new Vector3((float)rng.NextDouble() * a.Size.x, (float)rng.NextDouble() * a.Size.y, 0);
                    go.transform.localRotation = Quaternion.Euler(0, 0, rng.Next(360));
                    float s = 3f + (float)rng.NextDouble() * 5f;
                    go.transform.localScale = new Vector3(s * (1.2f + (float)rng.NextDouble() * 0.6f), s, 1);
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sprite = blob;
                    var c = g.Patch;
                    r.color = new Color(c.r, c.g, c.b, 0.55f + (float)rng.NextDouble() * 0.3f);
                    r.sortingOrder = -99;
                }
            }
        }

        static Color Shade(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);

        /// <summary>Tiles cut from an imported ground texture, or null when there is none.</summary>
        static Tile[,] GroundArtTiles(int phase)
        {
            if (!AssetDatabase.IsValidFolder(ArtRules.IncomingDir)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRules.IncomingDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!ArtName.TryParse(path, out var n) || n.Category != "ENV" || n.Id != $"P{phase}" || n.State != "ground") continue;
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
                var tiles = new Tile[8, 8];
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    if (!sprites.TryGetValue($"{n.Name}_{x}_{y}", out var s)) return null;
                    tiles[x, y] = LoadOrCreateTile(ArtTilesDir, $"ENV_P{phase}_{n.Name}_{x}_{y}", s, Color.white);
                }
                return tiles;
            }
            return null;
        }

        static void ScatterDecor(ArenaAuthoring a, Dictionary<(int, string), GameObject> prefabs)
        {
            for (int phase = 1; phase <= 3; phase++)
            {
                var blocked = new HashSet<Vector2Int>();
                foreach (var (name, x, y) in Layout[phase - 1])
                {
                    var s = Props[phase - 1].First(p => p.Name == name);
                    for (int dy = -1; dy <= s.D; dy++)
                    for (int dx = -1; dx <= s.W; dx++)
                        blocked.Add(new Vector2Int(x + dx, y + dy));
                }
                var rng = new System.Random(101 + phase);
                foreach (var d in DecorSpecs[phase - 1])
                    for (int k = 0; k < d.Count; k++)
                    {
                        Vector2Int cell;
                        int tries = 0;
                        do cell = new Vector2Int(rng.Next(2, a.Size.x - 2), rng.Next(2, a.Size.y - 2));
                        while (blocked.Contains(cell) && ++tries < 20);
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[(phase, d.Name)], a.Decor[phase - 1]);
                        go.transform.position = CellToWorld(a, cell.x + (float)rng.NextDouble(), cell.y + (float)rng.NextDouble());
                        go.transform.localRotation = Quaternion.Euler(0, 0, d.Name is "GrassTuft" ? 0 : rng.Next(360));
                        float s = 0.75f + (float)rng.NextDouble() * 0.5f;
                        go.transform.localScale = new Vector3(rng.Next(2) == 0 ? s : -s, s, 1);
                    }
            }
        }

        // ------------------------------------------------------------------ prefabs

        /// <summary>One prefab per prop and decor kind: the art importer swaps the "Body/Shape" sprite.</summary>
        static Dictionary<(int, string), GameObject> BuildPropPrefabs()
        {
            var result = new Dictionary<(int, string), GameObject>();
            for (int phase = 1; phase <= 3; phase++)
            {
                foreach (var spec in Props[phase - 1]) result[(phase, spec.Name)] = BuildProp(phase, spec);
                foreach (var d in DecorSpecs[phase - 1])
                {
                    var root = new GameObject($"PH_Decor_P{phase}_{d.Name}");
                    var body = new GameObject("Body").transform;
                    body.SetParent(root.transform, false);
                    PrefabBuilder.AddSprite(body, "Shape", d.Sprite, d.Color, Vector3.zero, new Vector3(d.Size, d.Size, 1), -90);
                    result[(phase, d.Name)] = PrefabBuilder.Save(root, root.name);
                }
            }
            return result;
        }

        static GameObject BuildProp(int phase, PropSpec s)
        {
            var root = new GameObject($"PH_Prop_P{phase}_{s.Name}");
            root.AddComponent<SortingGroup>();
            var prop = root.AddComponent<ArenaProp>();
            prop.Phase = phase;
            prop.PropName = s.Name;
            prop.Footprint = new Vector2Int(s.W, s.D);

            float w = s.W, d = s.D, top = d * 0.6f;
            PrefabBuilder.AddSprite(root.transform, "Shadow", "PH_Shadow", Color.white, new Vector3(0, d * 0.5f, 0), new Vector3(w * 1.2f, d * 2.2f, 1), -10);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            switch (s.Shape)
            {
                case Shape.Box:
                {
                    float front = Mathf.Max(0.3f, s.Height - top);
                    PrefabBuilder.AddSprite(body, "Shape", "PH_Square", s.Main, new Vector3(0, front * 0.5f, 0), new Vector3(w * 0.96f, front, 1), 0);
                    PrefabBuilder.AddSprite(body, "Top", "PH_Square", s.Top, new Vector3(0, front + top * 0.5f, 0), new Vector3(w * 0.96f, top, 1), 1);
                    if (s.Glow.HasValue)
                        PrefabBuilder.AddSprite(body, "Lights", "PH_Square", s.Glow.Value, new Vector3(0, front * 0.6f, 0), new Vector3(w * 0.5f, 0.12f, 1), 2);
                    break;
                }
                case Shape.Round:
                    PrefabBuilder.AddSprite(body, "Shape", "PH_Circle", s.Main, new Vector3(0, s.Height * 0.5f, 0), new Vector3(w * 0.95f, s.Height, 1), 0);
                    PrefabBuilder.AddSprite(body, "Top", "PH_Circle", s.Top, new Vector3(-w * 0.08f, s.Height * 0.68f, 0), new Vector3(w * 0.55f, s.Height * 0.45f, 1), 1);
                    break;
                case Shape.Tree:
                    PrefabBuilder.AddSprite(body, "Shape", "PH_Square", s.Main, new Vector3(0, s.Height * 0.3f, 0), new Vector3(0.3f, s.Height * 0.6f, 1), 0);
                    var crown = PrefabBuilder.AddSprite(body, "Top", s.Name == "DeadTree" ? "PH_Triangle" : "PH_Circle", s.Top,
                        new Vector3(0, s.Height * 0.72f, 0), s.Name == "DeadTree" ? new Vector3(1.6f, 1.2f, 1) : new Vector3(2.4f, 2.2f, 1), 1);
                    if (s.Name == "DeadTree") crown.transform.localRotation = Quaternion.Euler(0, 0, 90); // bare crown pointing up
                    break;
                case Shape.Post:
                    PrefabBuilder.AddSprite(body, "Shape", "PH_Square", s.Main, new Vector3(0, s.Height * 0.5f, 0), new Vector3(0.18f, s.Height, 1), 0);
                    PrefabBuilder.AddSprite(body, "Top", "PH_Circle", s.Top, new Vector3(0, s.Height, 0), new Vector3(0.45f, 0.3f, 1), 1);
                    break;
            }

            if (s.Glow.HasValue)
            {
                // Lit props also light the ground around them (helps the darker phases stay readable).
                var lightGo = new GameObject("Light");
                lightGo.SetActive(false);
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = new Vector3(0, s.Shape == Shape.Post ? 1f : d * 0.5f, 0);
                var l = lightGo.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = s.Glow.Value;
                l.intensity = s.Shape == Shape.Post ? 0.9f : 0.45f;
                l.pointLightOuterRadius = s.Shape == Shape.Post ? 4.5f : 2.5f;
                l.pointLightInnerRadius = 0.3f;
                l.falloffIntensity = 0.6f;
                lightGo.SetActive(true);
            }
            return PrefabBuilder.Save(root, root.name);
        }

        static Tilemap NewTilemap(GameObject parent, string name, int order, TilemapRenderer.Mode mode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var map = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            r.sortingOrder = order;
            r.mode = mode;
            return map;
        }

        static Transform Marker(GameObject parent, string name, Vector2 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, true);
            go.transform.position = worldPos;
            return go.transform;
        }

        // ------------------------------------------------------------------ bake

        [MenuItem("Hollow/Arena/Bake Layout")]
        public static void BakeFromOpenScene()
        {
            var authoring = Object.FindAnyObjectByType<ArenaAuthoring>(FindObjectsInactive.Include);
            if (authoring == null) { Debug.LogError("[Hollow] No ArenaAuthoring in the open scene."); return; }
            Bake(authoring);
        }

        public static ArenaLayout Bake(ArenaAuthoring a)
        {
            var layout = new ArenaLayout
            {
                Width = a.Size.x,
                Height = a.Size.y,
                Origin = a.Grid.transform.position,
                PlayerStart = a.PlayerStart.position,
                CompanionStart = a.CompanionStart.position,
                ShopPosition = a.Shop.position,
                SpawnZones = new List<SpawnZone>(a.SpawnZones),
            };
            foreach (var shop in a.AllShops()) layout.ShopPositions.Add(shop.position);
            CollectCells(a.Walls, layout, layout.BaseBlocked);

            // Every service station is solid in every phase.
            foreach (var shop in a.AllShops())
            {
                var shopCell = Vector2Int.FloorToInt((Vector2)shop.position - layout.Origin);
                for (int dy = 0; dy < a.ShopFootprint.y; dy++)
                for (int dx = 0; dx < a.ShopFootprint.x; dx++)
                {
                    int x = shopCell.x - a.ShopFootprint.x / 2 + dx, y = shopCell.y + dy;
                    if (x >= 0 && y >= 0 && x < layout.Width && y < layout.Height) layout.BaseBlocked.Add(layout.CellIndex(x, y));
                }
            }

            for (int i = 0; i < 3; i++)
            {
                var cells = new HashSet<int>();
                var painted = new List<int>();
                CollectCells(a.Obstacles[i], layout, painted);
                cells.UnionWith(painted);
                if (a.Props[i] != null)
                    foreach (var prop in a.Props[i].GetComponentsInChildren<ArenaProp>(true))
                        foreach (var c in prop.Rect.allPositionsWithin)
                            if (c.x >= 0 && c.y >= 0 && c.x < layout.Width && c.y < layout.Height) cells.Add(layout.CellIndex(c.x, c.y));
                var phase = new PhaseCells();
                phase.Cells.AddRange(cells.OrderBy(c => c));
                layout.PhaseBlocked.Add(phase);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);
            File.WriteAllText(LayoutPath, ArenaLayoutStore.ToJson(layout));
            AssetDatabase.ImportAsset(LayoutPath);
            Debug.Log($"[Hollow] Baked arena layout: {layout.BaseBlocked.Count} base cells, phases {layout.PhaseBlocked[0].Cells.Count}/{layout.PhaseBlocked[1].Cells.Count}/{layout.PhaseBlocked[2].Cells.Count}");
            return layout;
        }

        static void CollectCells(Tilemap map, ArenaLayout layout, List<int> into)
        {
            if (map == null) return;
            var bounds = map.cellBounds;
            foreach (var pos in bounds.allPositionsWithin)
            {
                if (!map.HasTile(pos)) continue;
                if (pos.x < 0 || pos.y < 0 || pos.x >= layout.Width || pos.y >= layout.Height) continue;
                into.Add(layout.CellIndex(pos.x, pos.y));
            }
        }
    }
}
