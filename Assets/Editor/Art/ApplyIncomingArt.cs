using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hollow.Data;
using Hollow.Presentation;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Puts imported AI art into the game: builds one SpriteSet per entity (frames per state) and swaps the
    /// placeholder shapes in the PH_ prefabs and the visual catalog. Re-run after adding or replacing files.
    /// Gameplay is untouched: hitboxes and numbers stay in the balance data.
    /// </summary>
    public static class ApplyIncomingArt
    {
        const string SetsDir = "Assets/Art/SpriteSets";

        static readonly Dictionary<string, WeaponId> WeaponIds = new()
        {
            ["PST"] = WeaponId.Pistol, ["SMG"] = WeaponId.LightSmg, ["DBS"] = WeaponId.DoubleBarrel, ["BAT"] = WeaponId.Baton,
            ["IRF"] = WeaponId.IndustrialRifle, ["MSG"] = WeaponId.MagazineShotgun, ["HAM"] = WeaponId.IndustrialHammer,
            ["ERF"] = WeaponId.EnergyRifle, ["ESG"] = WeaponId.EnergyShotgun, ["EBL"] = WeaponId.EnergyBlade,
        };

        static readonly Dictionary<string, BossId> BossIds = new()
        {
            ["B1"] = BossId.LargeBeast, ["B2"] = BossId.IndustrialRobot, ["B3"] = BossId.FormerCompanion,
        };

        /// <summary>All imported sprites grouped as key → state → frames (in frame order).</summary>
        public static Dictionary<string, Dictionary<string, List<Sprite>>> Collect()
        {
            var map = new Dictionary<string, Dictionary<string, List<Sprite>>>();
            if (!AssetDatabase.IsValidFolder(ArtRules.IncomingDir)) return map;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRules.IncomingDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!ArtName.TryParse(path, out var n)) continue;
                // The companion has P1 and P2 looks, and each phase has several props: their keys include the name.
                string key = n.Category is "CHR" or "ENV" or "PRJ" or "FX" or "UI" or "ICO" ? $"{n.Key}_{n.Name}" : n.Key;
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                    .OrderBy(s => FrameIndex(s.name)).ToList();
                if (sprites.Count == 0) continue;
                if (!map.TryGetValue(key, out var states)) map[key] = states = new Dictionary<string, List<Sprite>>();
                string state = n.Variant != null ? $"{n.State}_{n.Variant}" : n.State;
                // An animated strip replaces the single frame of the same state, whichever file is found first.
                if (!states.TryGetValue(state, out var existing) || sprites.Count > existing.Count) states[state] = sprites;
            }
            return map;
        }

        static int FrameIndex(string spriteName)
        {
            int u = spriteName.LastIndexOf('_');
            return u >= 0 && int.TryParse(spriteName.Substring(u + 1), out int i) ? i : 0;
        }

        [MenuItem("Hollow/Art/Apply Incoming Art")]
        public static void Apply()
        {
            var map = Collect();
            Directory.CreateDirectory(SetsDir);
            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(EnemyPrefabBuilder.CatalogPath);
            int applied = 0;
            bool ground = false;
            SpriteSet companionPhaseTwo = null;
            foreach (var (key, states) in map)
            {
                string[] k = key.Split('_');
                string cat = k[0], id = k[1];
                switch (cat)
                {
                    case "CHR" when id == "PLY":
                        applied += Wire($"{PrefabBuilder.Dir}/PH_Player.prefab", "Body/Core", Set(key, states), playerFloatToZero: true);
                        break;
                    case "CHR" when id == "OPR" && k.Length > 2 && k[2] == "P1":
                        applied += Wire($"{PrefabBuilder.Dir}/PH_Companion.prefab", "Body/Overalls", Set(key, states));
                        break;
                    case "CHR" when id == "OPR" && k.Length > 2 && k[2] == "P2":
                        companionPhaseTwo = Set(key, states);
                        applied++;
                        break;
                    case "ENM":
                        var enemy = Enum.GetNames(typeof(EnemyId)).FirstOrDefault(e => e.StartsWith(id + "_"));
                        if (enemy != null) applied += Wire($"{PrefabBuilder.Dir}/PH_Enemy_{enemy}.prefab", "Body/Shape", Set(key, states));
                        break;
                    case "BOS":
                        if (BossIds.TryGetValue(id, out var boss)) applied += Wire($"{PrefabBuilder.Dir}/PH_Boss_{boss}.prefab", "Body/Shape", Set(key, states));
                        break;
                    case "ENV" when id == "SHOP" && k.Length > 2 && k[2] == "Kiosk":
                        applied += Wire($"{PrefabBuilder.Dir}/PH_ShopStation.prefab", "Visual/Body", Set(key, states));
                        break;
                    case "ENV" when k.Length > 2:
                        if (states.ContainsKey("ground")) { ground = true; applied++; }
                        else if (k[2] is "BorderH" or "BorderV") { ground = true; applied++; } // placed in the scene with the ground
                        else if (states.ContainsKey("prop")) applied += Wire($"{PrefabBuilder.Dir}/PH_Prop_{id}_{k[2]}.prefab", "Body/Shape", Set(key, states));
                        else if (states.Keys.Any(s => s.StartsWith("decor"))) applied += Wire($"{PrefabBuilder.Dir}/PH_Decor_{id}_{k[2]}.prefab", "Body/Shape", Set(key, states));
                        break;
                    case "PKP" when id == "COIN":
                        applied += Wire($"{PrefabBuilder.Dir}/PH_Coin.prefab", "Coin", Set(key, states));
                        break;
                    case "PRJ" when catalog != null:
                    {
                        var sprite = states.Values.First()[0];
                        switch (id, k.Length > 2 ? k[2] : "")
                        {
                            case ("PLY", "Bullet"): catalog.PlayerBulletArt = sprite; break;
                            case ("PLY", "Pellet"): catalog.PlayerPelletArt = sprite; break;
                            case ("ENM", "Quill"): SetEnemyShot(catalog, 0, sprite); break;
                            case ("ENM", "Bolt"): SetEnemyShot(catalog, 1, sprite); break;
                            case ("ENM", "Bullet"): SetEnemyShot(catalog, 2, sprite); break;
                            default: continue;
                        }
                        EditorUtility.SetDirty(catalog);
                        applied++;
                        break;
                    }
                    case "FX" when catalog != null && k.Length > 2:
                    {
                        // Frame rates are cosmetic: a flash is over in a tenth of a second, an explosion lingers.
                        var set = Set(key, states, k[2] switch { "MuzzleFlash" => 30f, "Impact" => 24f, _ => 16f });
                        if (k[2] == "MuzzleFlash") catalog.MuzzleFlashFx = set;
                        else if (k[2] == "Impact") catalog.ImpactFx = set;
                        else if (k[2] == "Explosion") catalog.ExplosionFx = set;
                        else continue;
                        EditorUtility.SetDirty(catalog);
                        applied++;
                        break;
                    }
                    case "UI" when k.Length > 2 && k[2] == "Reticle":
                        applied += SetReticle(states.Values.First()[0]);
                        break;
                    case "WPN":
                        if (catalog != null && WeaponIds.TryGetValue(id, out var weapon) && states.TryGetValue("held", out var held))
                        {
                            var wv = catalog.Weapon(weapon);
                            if (wv != null)
                            {
                                wv.Sprite = held[0];
                                wv.Color = Color.white;
                                wv.Size = held[0].bounds.size;
                                wv.PivotAtGrip = true;
                                wv.MuzzleLocal = BarrelTip(held[0]);
                                EditorUtility.SetDirty(catalog);
                                applied++;
                            }
                        }
                        break;
                }
            }
            if (companionPhaseTwo != null) SetCompanionPhaseTwo(companionPhaseTwo);
            HidePlaceholderDecor(map);
            if (ground) RepaintGroundInOpenScene();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Hollow art] Applied {applied} art set(s) from {ArtRules.IncomingDir}.");
        }

        /// <summary>Ground art lives in the scene's Tilemaps, not in a prefab: repaint them if the Game scene is open.</summary>
        static void RepaintGroundInOpenScene()
        {
            var arena = UnityEngine.Object.FindAnyObjectByType<ArenaAuthoring>(FindObjectsInactive.Include);
            if (arena == null) { Debug.Log("[Hollow art] Ground art found; open the Game scene and apply again (or rebuild the scene) to paint it."); return; }
            ArenaTools.PaintGround(arena);
            ArenaTools.PlaceBorders(arena);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(arena.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(arena.gameObject.scene);
        }

        /// <summary>
        /// Placeholder decor (flat tinted blobs) clashes with painted ground: on a phase whose ground has art, decor
        /// without art is hidden. Decor is visual only; props without art stay, since they block.
        /// </summary>
        static void HidePlaceholderDecor(Dictionary<string, Dictionary<string, List<Sprite>>> map)
        {
            for (int phase = 1; phase <= 3; phase++)
            {
                int groundPhase = map.Keys.Any(k => k.StartsWith($"ENV_P{phase}_") && map[k].ContainsKey("ground")) ? phase
                    : phase == 2 && map.Keys.Any(k => k.StartsWith("ENV_P3_") && map[k].ContainsKey("ground")) ? 3 : 0;
                if (groundPhase == 0) continue;
                foreach (var path in Directory.GetFiles(PrefabBuilder.Dir, $"PH_Decor_P{phase}_*.prefab"))
                {
                    string name = Path.GetFileNameWithoutExtension(path).Substring($"PH_Decor_P{phase}_".Length);
                    if (map.ContainsKey($"ENV_P{phase}_{name}")) continue;
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = false;
                        PrefabUtility.SaveAsPrefabAsset(root, path.Replace('\\', '/'));
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            }
        }

        /// <summary>
        /// Where the drawn barrel ends: the rightmost opaque column of the sprite (weapons point right), at the
        /// middle of its opaque pixels, in units from the pivot. AI canvases leave empty space past the barrel.
        /// </summary>
        static Vector2 BarrelTip(Sprite sprite)
        {
            var tex = sprite.texture;
            var r = sprite.rect;
            var px = tex.GetPixels32();
            int w = tex.width;
            for (int x = (int)r.xMax - 1; x >= (int)r.xMin; x--)
            {
                int sum = 0, count = 0;
                for (int y = (int)r.yMin; y < (int)r.yMax; y++)
                    if (px[y * w + x].a > 128) { sum += y; count++; }
                if (count == 0) continue;
                float tipX = x + 1 - r.xMin, tipY = sum / (float)count - r.yMin;
                return new Vector2(tipX - sprite.pivot.x, tipY - sprite.pivot.y) / sprite.pixelsPerUnit;
            }
            return Vector2.zero;
        }

        static void SetEnemyShot(VisualCatalog catalog, int phaseIndex, Sprite sprite)
        {
            if (catalog.EnemyShotArt == null || catalog.EnemyShotArt.Length != 3) catalog.EnemyShotArt = new Sprite[3];
            catalog.EnemyShotArt[phaseIndex] = sprite;
        }

        /// <summary>The reticle art, shown at the placeholder's size.</summary>
        static int SetReticle(Sprite sprite)
        {
            string path = $"{PrefabBuilder.Dir}/PH_Reticle.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var r = root.transform.Find("Reticle")?.GetComponent<SpriteRenderer>();
                if (r == null) return 0;
                float size = r.bounds.size.x > 0 ? r.bounds.size.x : 0.45f;
                if (r.sprite != sprite)
                {
                    r.sprite = sprite;
                    float s = size * 1.4f / sprite.bounds.size.x; // a thin ring reads larger than the old dot
                    r.transform.localScale = new Vector3(s, s, 1);
                }
                r.color = Color.white;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return 1;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void SetCompanionPhaseTwo(SpriteSet set)
        {
            string path = $"{PrefabBuilder.Dir}/PH_Companion.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<CompanionView>();
                if (view == null) return;
                var so = new SerializedObject(view);
                so.FindProperty("phaseTwoSet").objectReferenceValue = set;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static SpriteSet Set(string key, Dictionary<string, List<Sprite>> states, float framesPerSecond = 10f)
        {
            string path = $"{SetsDir}/{key}.asset";
            var set = AssetDatabase.LoadAssetAtPath<SpriteSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SpriteSet>();
                AssetDatabase.CreateAsset(set, path);
            }
            set.States.Clear();
            set.FramesPerSecond = framesPerSecond;
            foreach (var (state, frames) in states)
                set.States.Add(new SpriteState { State = state, Frames = new List<Sprite>(frames), Loop = state is "idle" or "move" or "spin" });
            EditorUtility.SetDirty(set);
            return set;
        }

        /// <summary>Replaces the placeholder shape with the art and hides the other placeholder pieces of the body.</summary>
        static int Wire(string prefabPath, string rendererPath, SpriteSet set, bool playerFloatToZero = false)
        {
            if (!File.Exists(prefabPath)) { Debug.LogWarning($"[Hollow art] Missing prefab {prefabPath}"); return 0; }
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var target = root.transform.Find(rendererPath)?.GetComponent<SpriteRenderer>();
                if (target == null) { Debug.LogWarning($"[Hollow art] {prefabPath} has no {rendererPath}"); return 0; }
                var parent = target.transform.parent;
                foreach (var r in parent.GetComponentsInChildren<SpriteRenderer>(true))
                    if (r != target) r.enabled = false;
                target.transform.localPosition = Vector3.zero;
                target.transform.localScale = Vector3.one;
                target.transform.localRotation = Quaternion.identity;
                target.color = Color.white;
                target.enabled = true;
                var first = set.Resolve("idle");
                if (first != null) target.sprite = first.Frames[0];

                // Not '??': in the Editor a missing component comes back as a fake-null object.
                var anim = root.GetComponent<SpriteSetAnimator>();
                if (anim == null) anim = root.AddComponent<SpriteSetAnimator>();
                var so = new SerializedObject(anim);
                so.FindProperty("set").objectReferenceValue = set;
                so.FindProperty("target").objectReferenceValue = target;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (playerFloatToZero)
                {
                    var view = root.GetComponent<PlayerView>();
                    var vso = new SerializedObject(view);
                    vso.FindProperty("floatHeight").floatValue = 0f; // the art already floats above its ground point
                    vso.FindProperty("normalColor").colorValue = Color.white; // the art has its own colours
                    vso.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var tr in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(tr.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return 1;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
