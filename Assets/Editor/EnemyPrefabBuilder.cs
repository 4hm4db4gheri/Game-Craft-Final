using System.Collections.Generic;
using System.IO;
using Hollow.Data;
using Hollow.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Placeholder enemy prefabs (PH_Enemy_*): a distinct primitive silhouette per type in a faction palette
    /// (D14). Bodies are sized from the AI-art asset list heights so swapped art keeps the same footprint.
    /// </summary>
    public static class EnemyPrefabBuilder
    {
        public const string CatalogPath = "Assets/Art/Placeholders/VisualCatalog.asset";
        const string SpriteMaterialPath = PrefabBuilder.UnlitMaterialPath;

        static readonly Color Animal = new(0.45f, 0.38f, 0.30f);
        static readonly Color AnimalDark = new(0.30f, 0.27f, 0.22f);
        static readonly Color AnimalPale = new(0.78f, 0.74f, 0.64f);
        static readonly Color Robot = new(0.55f, 0.58f, 0.62f);
        static readonly Color RobotDark = new(0.36f, 0.38f, 0.42f);
        static readonly Color RobotLight = new(0.49f, 1f, 0.42f);
        static readonly Color Human = new(0.20f, 0.24f, 0.38f);
        static readonly Color HumanKhaki = new(0.55f, 0.50f, 0.36f);
        static readonly Color HumanCharcoal = new(0.25f, 0.25f, 0.27f);

        struct Look
        {
            public string Sprite;
            public Vector2 Size;
            public Color Color;
            public bool FrontPlate;
            public string Accent;
            public Color AccentColor;
            public Vector2 AccentSize;
            public Vector2 AccentOffset;
        }

        static Look L(string sprite, float w, float h, Color c, bool plate = false, string accent = null, Color accentColor = default, float aw = 0, float ah = 0, float ax = 0, float ay = 0) =>
            new() { Sprite = sprite, Size = new Vector2(w, h), Color = c, FrontPlate = plate, Accent = accent, AccentColor = accentColor, AccentSize = new Vector2(aw, ah), AccentOffset = new Vector2(ax, ay) };

        static readonly Dictionary<EnemyId, Look> Looks = new()
        {
            [EnemyId.A1_RunnerWolf] = L("PH_Capsule", 0.9f, 0.5f, Animal, accent: "PH_Triangle", accentColor: AnimalDark, aw: 0.25f, ah: 0.25f, ax: 0.4f, ay: 0.1f),
            [EnemyId.A2_QuillShooter] = L("PH_Circle", 0.75f, 0.65f, AnimalPale, accent: "PH_Diamond", accentColor: AnimalPale * 0.8f, aw: 0.9f, ah: 0.5f, ay: 0.25f),
            [EnemyId.A3_ChargingBoar] = L("PH_Capsule", 1.3f, 0.75f, AnimalDark, accent: "PH_Triangle", accentColor: AnimalPale, aw: 0.3f, ah: 0.2f, ax: 0.65f),
            [EnemyId.A4_ArmoredTortoise] = L("PH_Circle", 1.2f, 0.9f, new Color(0.30f, 0.36f, 0.26f), plate: true),
            [EnemyId.A5_HowlingWolf] = L("PH_Capsule", 0.9f, 0.55f, new Color(0.25f, 0.24f, 0.24f), accent: "PH_Circle", accentColor: AnimalPale, aw: 0.35f, ah: 0.35f, ax: 0.3f, ay: 0.25f),
            [EnemyId.A6_ExplosiveBeetle] = L("PH_Circle", 0.6f, 0.5f, new Color(0.65f, 0.78f, 0.32f), accent: "PH_Circle", accentColor: new Color(0.85f, 1f, 0.5f), aw: 0.35f, ah: 0.3f, ay: 0.05f),
            [EnemyId.A7_MotherSpider] = L("PH_Circle", 1.2f, 0.9f, new Color(0.28f, 0.22f, 0.24f), accent: "PH_Circle", accentColor: AnimalPale, aw: 0.5f, ah: 0.45f, ax: -0.35f, ay: 0.2f),
            [EnemyId.A7c_Spiderling] = L("PH_Circle", 0.45f, 0.35f, new Color(0.45f, 0.38f, 0.40f)),

            [EnemyId.R1_Crawler] = L("PH_Square", 0.8f, 0.5f, Robot, accent: "PH_Triangle", accentColor: RobotLight, aw: 0.2f, ah: 0.2f, ax: 0.35f),
            [EnemyId.R2_Shooter] = L("PH_Square", 0.6f, 1.0f, Robot, accent: "PH_Square", accentColor: RobotDark, aw: 0.6f, ah: 0.14f, ax: 0.4f, ay: 0.2f),
            [EnemyId.R3_SawCharger] = L("PH_Triangle", 1.0f, 0.8f, RobotDark, accent: "PH_Ring", accentColor: Robot, aw: 0.5f, ah: 0.5f, ax: 0.4f),
            [EnemyId.R4_ShieldRobot] = L("PH_Square", 1.0f, 1.1f, RobotDark, plate: true),
            [EnemyId.R5_Repair] = L("PH_Diamond", 0.7f, 0.7f, Robot, accent: "PH_Circle", accentColor: RobotLight, aw: 0.2f, ah: 0.2f, ay: 0.1f),
            [EnemyId.R6_Suicide] = L("PH_Circle", 0.6f, 0.6f, RobotDark, accent: "PH_Circle", accentColor: RobotLight, aw: 0.25f, ah: 0.25f, ay: 0.1f),
            [EnemyId.R7_DroneCarrier] = L("PH_Square", 1.4f, 1.1f, Robot, accent: "PH_Square", accentColor: RobotDark, aw: 0.8f, ah: 0.3f, ay: 0.35f),
            [EnemyId.R7c_Drone] = L("PH_Diamond", 0.45f, 0.4f, Robot, accent: "PH_Circle", accentColor: RobotLight, aw: 0.12f, ah: 0.12f),

            [EnemyId.H1_BatonGuard] = L("PH_Capsule", 0.55f, 1.1f, Human, accent: "PH_Square", accentColor: HumanCharcoal, aw: 0.1f, ah: 0.5f, ax: 0.32f, ay: 0.1f),
            [EnemyId.H2_RifleGuard] = L("PH_Capsule", 0.55f, 1.1f, HumanKhaki, accent: "PH_Square", accentColor: HumanCharcoal, aw: 0.55f, ah: 0.1f, ax: 0.35f, ay: 0.15f),
            [EnemyId.H3_FastAssaulter] = L("PH_Capsule", 0.5f, 1.05f, HumanCharcoal, accent: "PH_Triangle", accentColor: Human, aw: 0.3f, ah: 0.25f, ax: 0.25f, ay: 0.25f),
            [EnemyId.H4_ShieldGuard] = L("PH_Capsule", 0.7f, 1.15f, Human, plate: true),
            [EnemyId.H5_Medic] = L("PH_Capsule", 0.55f, 1.05f, new Color(0.75f, 0.78f, 0.80f), accent: "PH_Square", accentColor: new Color(0.3f, 0.75f, 0.4f), aw: 0.25f, ah: 0.25f, ay: 0.2f),
            [EnemyId.H6_Grenadier] = L("PH_Capsule", 0.6f, 1.1f, HumanKhaki, accent: "PH_Circle", accentColor: HumanCharcoal, aw: 0.3f, ah: 0.3f, ax: -0.25f, ay: 0.1f),
            [EnemyId.H7_Engineer] = L("PH_Capsule", 0.7f, 1.1f, HumanCharcoal, accent: "PH_Square", accentColor: HumanKhaki, aw: 0.4f, ah: 0.3f, ax: 0.3f, ay: -0.15f),
            [EnemyId.H7c_Turret] = L("PH_Square", 0.8f, 0.5f, HumanCharcoal, accent: "PH_Triangle", accentColor: new Color(0.55f, 0.7f, 1f), aw: 0.5f, ah: 0.3f, ax: 0.2f, ay: 0.35f),
        };

        public static VisualCatalog BuildAll()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
                catalog = ScriptableObject.CreateInstance<VisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var arcPrefab = BuildArc();
            catalog.Arc = arcPrefab.GetComponent<ArcView>();
            catalog.Enemies.Clear();
            foreach (var kv in Looks)
            {
                var prefab = BuildEnemy(kv.Key, kv.Value, arcPrefab);
                catalog.Enemies.Add(new EnemyVisual { Id = kv.Key, Prefab = prefab.GetComponent<EnemyView>() });
            }
            catalog.PlayerProjectile = BuildProjectile("PH_Projectile_Player", new Vector2(0.28f, 0.1f), true).GetComponent<ProjectileView>();
            catalog.EnemyProjectile = BuildProjectile("PH_Projectile_Enemy", new Vector2(0.3f, 0.14f), true, "Telegraphs").GetComponent<ProjectileView>();
            catalog.Coin = BuildCoin().GetComponent<CoinView>();
            catalog.Weapons = WeaponVisuals();
            catalog.Bosses = new List<BossVisual>
            {
                new() { Id = BossId.LargeBeast, Prefab = BuildBoss(BossId.LargeBeast, "PH_Capsule", new Vector2(4.6f, 2.6f), new Color(0.32f, 0.26f, 0.2f), "PH_Triangle", new Color(0.8f, 0.76f, 0.66f), new Vector2(0.9f, 0.6f), new Vector2(2.1f, 0.4f), arcPrefab) },
                new() { Id = BossId.IndustrialRobot, Prefab = BuildBoss(BossId.IndustrialRobot, "PH_Square", new Vector2(3.4f, 4.2f), new Color(0.42f, 0.44f, 0.4f), "PH_Circle", new Color(0.49f, 1f, 0.42f), new Vector2(0.9f, 0.9f), new Vector2(0, 1f), arcPrefab) },
                new() { Id = BossId.FormerCompanion, Prefab = BuildBoss(BossId.FormerCompanion, "PH_Capsule", new Vector2(1.5f, 2.6f), new Color(0.32f, 0.36f, 0.42f), "PH_Square", new Color(0.78f, 0.62f, 0.22f), new Vector2(1.4f, 0.8f), new Vector2(0, 0.3f), arcPrefab) },
            };
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        static List<WeaponVisual> WeaponVisuals()
        {
            Color cyan = new(0.6f, 0.95f, 1f);
            WeaponVisual V(WeaponId id, float len, float thick, Color c, Color shot) =>
                new() { Id = id, Sprite = PlaceholderArt.Load("PH_Square"), Size = new Vector2(len, thick), Color = c, ProjectileColor = shot };
            return new List<WeaponVisual>
            {
                V(WeaponId.Pistol, 0.32f, 0.1f, new Color(0.85f, 0.87f, 0.9f), cyan),
                V(WeaponId.LightSmg, 0.45f, 0.11f, new Color(0.55f, 0.6f, 0.65f), cyan),
                V(WeaponId.DoubleBarrel, 0.6f, 0.13f, new Color(0.5f, 0.38f, 0.28f), new Color(1f, 0.95f, 0.8f)),
                V(WeaponId.Baton, 0.55f, 0.08f, new Color(0.3f, 0.3f, 0.35f), cyan),
                V(WeaponId.IndustrialRifle, 0.65f, 0.13f, new Color(0.45f, 0.45f, 0.4f), cyan),
                V(WeaponId.MagazineShotgun, 0.6f, 0.15f, new Color(0.35f, 0.35f, 0.4f), new Color(1f, 0.95f, 0.8f)),
                V(WeaponId.IndustrialHammer, 0.65f, 0.2f, new Color(0.5f, 0.45f, 0.4f), cyan),
                V(WeaponId.EnergyRifle, 0.7f, 0.12f, new Color(0.9f, 0.95f, 1f), new Color(0.4f, 1f, 1f)),
                V(WeaponId.EnergyShotgun, 0.65f, 0.16f, new Color(0.9f, 0.95f, 1f), new Color(0.4f, 1f, 1f)),
                V(WeaponId.EnergyBlade, 0.7f, 0.08f, new Color(0.55f, 1f, 1f), new Color(0.4f, 1f, 1f)),
            };
        }

        static GameObject BuildEnemy(EnemyId id, Look look, GameObject arcPrefab)
        {
            var root = new GameObject($"PH_Enemy_{id}");
            root.AddComponent<SortingGroup>();
            var view = root.AddComponent<EnemyView>();
            float lift = look.Size.y * 0.5f;

            PrefabBuilder.AddSprite(root.transform, "Shadow", "PH_Shadow", Color.white, Vector3.zero, new Vector3(look.Size.x * 1.1f, look.Size.x * 0.9f, 1), -10);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var renderers = new List<SpriteRenderer>
            {
                PrefabBuilder.AddSprite(body, "Shape", look.Sprite, look.Color, new Vector3(0, lift, 0), Scale(look.Sprite, look.Size), 0),
            };
            if (look.Accent != null)
                renderers.Add(PrefabBuilder.AddSprite(body, "Accent", look.Accent, look.AccentColor,
                    new Vector3(look.AccentOffset.x, lift + look.AccentOffset.y, 0), Scale(look.Accent, look.AccentSize), 1));

            Transform plate = null;
            if (look.FrontPlate)
            {
                plate = new GameObject("FrontPlate").transform;
                plate.SetParent(root.transform, false);
                plate.localPosition = new Vector3(0, lift * 0.8f, 0);
                renderers.Add(PrefabBuilder.AddSprite(plate, "Plate", "PH_Square", new Color(0.75f, 0.78f, 0.8f),
                    new Vector3(look.Size.x * 0.5f, 0, 0), new Vector3(0.18f / 1f, look.Size.y * 0.9f, 1), 2));
            }

            float barY = look.Size.y + 0.2f;
            var hpBack = PrefabBuilder.AddSprite(root.transform, "HpBack", "PH_Bar", new Color(0, 0, 0, 0.6f), new Vector3(0, barY, 0), new Vector3(1.2f, 0.5f, 1), 20);
            var hpFill = PrefabBuilder.AddSprite(root.transform, "HpFill", "PH_Bar", new Color(0.9f, 0.85f, 0.8f), new Vector3(0, barY, 0), new Vector3(1.2f, 0.5f, 1), 21);
            var ring = PrefabBuilder.AddSprite(root.transform, "SpawnRing", "PH_Ring", Color.white, Vector3.zero, Vector3.one, -9, unlit: true);
            var line = PrefabBuilder.AddSprite(root.transform, "TelegraphLine", "PH_Square", Color.white, Vector3.zero, Vector3.one, 500);
            line.transform.SetParent(null, true); // drawn in world space; re-parented below to keep the prefab self-contained
            line.transform.SetParent(root.transform, true);
            var arc = (GameObject)PrefabUtility.InstantiatePrefab(arcPrefab, root.transform);
            arc.name = "TelegraphArc";
            PrefabBuilder.OnTopLayer(line.gameObject);
            PrefabBuilder.OnTopLayer(arc);

            var so = new SerializedObject(view);
            so.FindProperty("body").objectReferenceValue = body;
            var arr = so.FindProperty("bodyRenderers");
            arr.arraySize = renderers.Count;
            for (int i = 0; i < renderers.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.FindProperty("frontPlate").objectReferenceValue = plate;
            so.FindProperty("hpBack").objectReferenceValue = hpBack;
            so.FindProperty("hpFill").objectReferenceValue = hpFill;
            so.FindProperty("spawnRing").objectReferenceValue = ring;
            so.FindProperty("telegraphLine").objectReferenceValue = line;
            so.FindProperty("telegraphArc").objectReferenceValue = arc.GetComponent<ArcView>();
            if (id == EnemyId.R6_Suicide)
            {
                // Visible countdown (§5 R6).
                var label = new GameObject("Countdown");
                label.transform.SetParent(root.transform, false);
                label.transform.localPosition = new Vector3(0, look.Size.y + 0.55f, 0);
                var tmp = label.AddComponent<TMPro.TextMeshPro>();
                tmp.fontSize = 6;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.rectTransform.sizeDelta = new Vector2(2, 1);
                tmp.sortingOrder = 600;
                tmp.enabled = false;
                PrefabBuilder.OnTopLayer(label);
                so.FindProperty("countdown").objectReferenceValue = tmp;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabBuilder.Save(root, $"PH_Enemy_{id}");
        }

        static BossView BuildBoss(BossId id, string sprite, Vector2 size, Color color, string accent, Color accentColor, Vector2 accentSize, Vector2 accentOffset, GameObject arcPrefab)
        {
            var root = new GameObject($"PH_Boss_{id}");
            root.AddComponent<SortingGroup>();
            var view = root.AddComponent<BossView>();
            PrefabBuilder.AddSprite(root.transform, "Shadow", "PH_Shadow", Color.white, Vector3.zero, new Vector3(size.x * 1.1f, size.x * 0.8f, 1), -10);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var main = PrefabBuilder.AddSprite(body, "Shape", sprite, color, new Vector3(0, size.y * 0.5f, 0), Scale(sprite, size), 0);
            var acc = PrefabBuilder.AddSprite(body, "Accent", accent, accentColor, new Vector3(accentOffset.x, size.y * 0.5f + accentOffset.y, 0), Scale(accent, accentSize), 1);
            var lane = PrefabBuilder.AddSprite(root.transform, "Lane", "PH_Square", Color.white, Vector3.zero, Vector3.one, 500);
            lane.enabled = false;
            var arc = (GameObject)PrefabUtility.InstantiatePrefab(arcPrefab, root.transform);
            PrefabBuilder.OnTopLayer(lane.gameObject);
            PrefabBuilder.OnTopLayer(arc);
            var so = new SerializedObject(view);
            so.FindProperty("body").objectReferenceValue = body;
            var arr = so.FindProperty("bodyRenderers");
            arr.arraySize = 2;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = main;
            arr.GetArrayElementAtIndex(1).objectReferenceValue = acc;
            so.FindProperty("laneLine").objectReferenceValue = lane;
            so.FindProperty("area").objectReferenceValue = arc.GetComponent<ArcView>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabBuilder.Save(root, $"PH_Boss_{id}").GetComponent<BossView>();
        }

        static Vector3 Scale(string sprite, Vector2 size)
        {
            var s = PlaceholderArt.Load(sprite);
            var b = s.bounds.size;
            return new Vector3(size.x / b.x, size.y / b.y, 1);
        }

        static GameObject BuildArc()
        {
            var root = new GameObject("PH_Arc");
            root.AddComponent<MeshFilter>();
            var mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            root.AddComponent<ArcView>();
            return PrefabBuilder.Save(root, "PH_Arc");
        }

        /// <summary>Enemy shots are attacks: they go on the top layer so no effect ever covers them.</summary>
        static GameObject BuildProjectile(string name, Vector2 size, bool trail, string layer = "Default")
        {
            var root = new GameObject(name);
            var view = root.AddComponent<ProjectileView>();
            var core = PrefabBuilder.AddSprite(root.transform, "Core", "PH_Capsule", Color.white, new Vector3(0, 0.35f, 0), Vector3.one, 300);
            core.transform.localRotation = Quaternion.Euler(0, 0, 90);
            core.transform.localScale = new Vector3(size.y / 1f, size.x / 2f, 1);
            TrailRenderer tr = null;
            if (trail)
            {
                tr = core.gameObject.AddComponent<TrailRenderer>();
                tr.time = 0.08f;
                tr.startWidth = size.y;
                tr.endWidth = 0f;
                tr.minVertexDistance = 0.05f;
                tr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
                tr.sortingOrder = 299;
                tr.sortingLayerName = layer;
            }
            core.sortingLayerName = layer;
            var so = new SerializedObject(view);
            so.FindProperty("core").objectReferenceValue = core;
            so.FindProperty("trail").objectReferenceValue = tr;
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabBuilder.Save(root, name);
        }

        static GameObject BuildCoin()
        {
            var root = new GameObject("PH_Coin");
            root.AddComponent<SortingGroup>();
            var view = root.AddComponent<CoinView>();
            PrefabBuilder.AddSprite(root.transform, "Shadow", "PH_Shadow", Color.white, Vector3.zero, new Vector3(0.25f, 0.25f, 1), -10);
            var coin = PrefabBuilder.AddSprite(root.transform, "Coin", "PH_Circle", new Color(1f, 0.78f, 0.24f), new Vector3(0, 0.12f, 0), new Vector3(0.22f, 0.22f, 1), 0, unlit: true);
            var so = new SerializedObject(view);
            so.FindProperty("coin").objectReferenceValue = coin;
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabBuilder.Save(root, "PH_Coin");
        }
    }
}
