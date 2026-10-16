using System.Collections.Generic;
using System.IO;
using Hollow.Presentation;
using Hollow.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Whitebox arena (D13: 48 × 32, four edge spawn zones, shop south of centre, one obstacle layout per phase)
    /// and the bake step that turns the painted Tilemaps into the simulation's ArenaLayout.json.
    /// </summary>
    public static class ArenaTools
    {
        public const string TilesDir = "Assets/Art/Placeholders/Tiles";
        public const string LayoutPath = "Assets/Resources/Arena/ArenaLayout.json";

        static readonly Color[] GroundColors =
        {
            new(0.20f, 0.22f, 0.17f), new(0.22f, 0.24f, 0.18f), new(0.19f, 0.21f, 0.16f),
        };
        static readonly Color WallColor = new(0.11f, 0.11f, 0.12f);
        static readonly Color[] ObstacleColors = { new(0.36f, 0.31f, 0.24f), new(0.33f, 0.37f, 0.40f), new(0.27f, 0.29f, 0.36f) };

        // Phase obstacle layouts in cell coordinates (x, y, width, height). Centre (24,16) and the shop stay clear.
        static readonly RectInt[][] PhaseObstacles =
        {
            new[] // Animals: hedges, rocks, trees, logs
            {
                new RectInt(9, 22, 3, 1), new RectInt(35, 9, 3, 1), new RectInt(14, 8, 2, 2), new RectInt(32, 22, 2, 2),
                new RectInt(8, 14, 1, 1), new RectInt(39, 17, 1, 1), new RectInt(20, 25, 1, 1), new RectInt(28, 6, 1, 1),
                new RectInt(16, 18, 2, 1), new RectInt(31, 13, 2, 1),
            },
            new[] // Robots: machine blocks, pipe barriers, crates, cable spools — cover for ranged threats
            {
                new RectInt(12, 20, 2, 2), new RectInt(34, 10, 2, 2), new RectInt(18, 8, 3, 1), new RectInt(27, 24, 3, 1),
                new RectInt(8, 10, 2, 1), new RectInt(38, 21, 2, 1), new RectInt(16, 26, 1, 1), new RectInt(31, 5, 1, 1),
                new RectInt(20, 15, 1, 2), new RectInt(28, 15, 1, 2),
            },
            new[] // Humans: barricades and sandbags — crossfire space
            {
                new RectInt(10, 12, 3, 1), new RectInt(35, 19, 3, 1), new RectInt(20, 24, 3, 1), new RectInt(25, 7, 3, 1),
                new RectInt(15, 20, 3, 1), new RectInt(30, 11, 3, 1), new RectInt(6, 22, 1, 1), new RectInt(41, 9, 1, 1),
            },
        };

        public static Tile LoadOrCreateTile(string name, Color color)
        {
            Directory.CreateDirectory(TilesDir);
            string path = $"{TilesDir}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = PlaceholderArt.Load("PH_Square");
            tile.color = color;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        /// <summary>Creates the Grid + Tilemaps + markers in the open scene and returns the authoring component.</summary>
        public static ArenaAuthoring BuildWhitebox()
        {
            var size = new Vector2Int(48, 32);
            var gridGo = new GameObject("Arena");
            gridGo.transform.position = new Vector3(-size.x / 2f, -size.y / 2f, 0);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var authoring = gridGo.AddComponent<ArenaAuthoring>();
            authoring.Grid = grid;
            authoring.Size = size;

            authoring.Ground = NewTilemap(gridGo, "Ground", -100, TilemapRenderer.Mode.Chunk);
            authoring.Walls = NewTilemap(gridGo, "Walls", 0, TilemapRenderer.Mode.Individual);
            authoring.Obstacles = new Tilemap[3];
            for (int i = 0; i < 3; i++) authoring.Obstacles[i] = NewTilemap(gridGo, $"Obstacles_P{i + 1}", 0, TilemapRenderer.Mode.Individual);

            var groundTiles = new Tile[GroundColors.Length];
            for (int i = 0; i < groundTiles.Length; i++) groundTiles[i] = LoadOrCreateTile($"PH_Tile_Ground_{i}", GroundColors[i]);
            var wall = LoadOrCreateTile("PH_Tile_Wall", WallColor);
            var rng = new System.Random(7);
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                authoring.Ground.SetTile(new Vector3Int(x, y, 0), groundTiles[rng.Next(groundTiles.Length)]);
                if (x == 0 || y == 0 || x == size.x - 1 || y == size.y - 1) authoring.Walls.SetTile(new Vector3Int(x, y, 0), wall);
            }

            for (int phase = 0; phase < 3; phase++)
            {
                var tile = LoadOrCreateTile($"PH_Tile_Obstacle_P{phase + 1}", ObstacleColors[phase]);
                foreach (var r in PhaseObstacles[phase])
                    for (int y = r.yMin; y < r.yMax; y++)
                    for (int x = r.xMin; x < r.xMax; x++)
                        authoring.Obstacles[phase].SetTile(new Vector3Int(x, y, 0), tile);
            }

            authoring.PlayerStart = Marker(gridGo, "PlayerStart", new Vector2(0, 0));
            authoring.CompanionStart = Marker(gridGo, "CompanionStart", new Vector2(-1.5f, -0.6f));
            authoring.Shop = Marker(gridGo, "ShopStation", new Vector2(0, -6.5f));
            authoring.ShopFootprint = new Vector2Int(2, 1);

            float hx = size.x / 2f, hy = size.y / 2f;
            authoring.SpawnZones = new List<SpawnZone>
            {
                new() { Side = SpawnSide.North, Min = new Vector2(-8, hy - 4), Max = new Vector2(8, hy - 2) },
                new() { Side = SpawnSide.East, Min = new Vector2(hx - 4, -6), Max = new Vector2(hx - 2, 6) },
                new() { Side = SpawnSide.South, Min = new Vector2(-8, -hy + 2), Max = new Vector2(8, -hy + 4) },
                new() { Side = SpawnSide.West, Min = new Vector2(-hx + 2, -6), Max = new Vector2(-hx + 4, 6) },
            };
            authoring.ShowPhase(1);
            return authoring;
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
            CollectCells(a.Walls, layout, layout.BaseBlocked);

            // The shop station is solid in every phase.
            var shopCell = Vector2Int.FloorToInt((Vector2)a.Shop.position - layout.Origin);
            for (int dy = 0; dy < a.ShopFootprint.y; dy++)
            for (int dx = 0; dx < a.ShopFootprint.x; dx++)
            {
                int x = shopCell.x - a.ShopFootprint.x / 2 + dx, y = shopCell.y + dy;
                if (x >= 0 && y >= 0 && x < layout.Width && y < layout.Height) layout.BaseBlocked.Add(layout.CellIndex(x, y));
            }

            foreach (var map in a.Obstacles)
            {
                var phase = new PhaseCells();
                CollectCells(map, layout, phase.Cells);
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
