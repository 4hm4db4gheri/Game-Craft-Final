using System.IO;
using Hollow.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Builds the placeholder prefabs (PH_*) from generated shapes. Artists replace sprites inside these
    /// prefabs; gameplay never references the visuals directly (final rule 3).
    /// </summary>
    public static class PrefabBuilder
    {
        public const string Dir = "Assets/Prefabs/Placeholders";

        public static GameObject Save(GameObject root, string name)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        /// <summary>Sorting orders from here up are overlays (bars, warnings, projectiles, reticle).</summary>
        public const int OverlayOrder = 20;

        /// <summary>
        /// Adds a placeholder sprite. Overlays and <paramref name="unlit"/> sprites ignore the arena light, so the
        /// darker phases never hide attack warnings, projectiles, coins or the player core (§14).
        /// </summary>
        public static SpriteRenderer AddSprite(Transform parent, string name, string sprite, Color color, Vector3 localPos, Vector3 scale, int order = 0, bool unlit = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = PlaceholderArt.Load(sprite);
            r.color = color;
            r.sortingOrder = order;
            if (unlit || order >= OverlayOrder) r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            return r;
        }

        public static GameObject BuildPlayer()
        {
            var root = new GameObject("PH_Player");
            root.AddComponent<SortingGroup>();
            var view = root.AddComponent<PlayerView>();
            var shadow = AddSprite(root.transform, "Shadow", "PH_Shadow", Color.white, Vector3.zero, new Vector3(0.7f, 0.7f, 1), -10);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var core = AddSprite(body, "Core", "PH_Circle", new Color(0.85f, 0.95f, 1f), Vector3.zero, new Vector3(0.6f, 0.6f, 1), 1, unlit: true);
            AddSprite(body, "Eye", "PH_Circle", new Color(0.5f, 0.92f, 1f), new Vector3(0.08f, 0.05f, 0), new Vector3(0.2f, 0.2f, 1), 2, unlit: true);
            AddSprite(body, "Ring", "PH_Ring", new Color(0.5f, 0.92f, 1f, 0.6f), Vector3.zero, new Vector3(0.8f, 0.3f, 1), 0, unlit: true);
            var pivot = new GameObject("WeaponPivot").transform;
            pivot.SetParent(root.transform, false);
            var weapon = AddSprite(pivot, "Weapon", "PH_Square", new Color(0.75f, 0.78f, 0.82f), new Vector3(0.42f, 0, 0), new Vector3(0.45f, 0.12f, 1), 3);

            var barBack = AddSprite(root.transform, "DashBarBack", "PH_Bar", new Color(0, 0, 0, 0.6f), new Vector3(0, -0.28f, 0), new Vector3(1.2f, 0.5f, 1), 20);
            var barFill = AddSprite(root.transform, "DashBarFill", "PH_Bar", new Color(0.5f, 0.92f, 1f), new Vector3(0, -0.28f, 0), new Vector3(1.2f, 0.5f, 1), 21);

            var so = new SerializedObject(view);
            so.FindProperty("dashBarBack").objectReferenceValue = barBack;
            so.FindProperty("dashBarFill").objectReferenceValue = barFill;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("bodyRenderer").objectReferenceValue = core;
            so.FindProperty("weaponPivot").objectReferenceValue = pivot;
            so.FindProperty("weaponRenderer").objectReferenceValue = weapon;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, "PH_Player");
        }

        public static GameObject BuildCompanion()
        {
            var root = new GameObject("PH_Companion");
            root.AddComponent<SortingGroup>();
            var view = root.AddComponent<CompanionView>();
            AddSprite(root.transform, "Shadow", "PH_Shadow", Color.white, Vector3.zero, new Vector3(0.8f, 0.7f, 1), -10);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var overalls = AddSprite(body, "Overalls", "PH_Capsule", new Color(0.32f, 0.42f, 0.58f), new Vector3(0, 0.75f, 0), new Vector3(0.55f, 0.75f, 1), 0);
            var vest = AddSprite(body, "Vest", "PH_Square", new Color(0.78f, 0.62f, 0.22f), new Vector3(0, 0.95f, 0), new Vector3(0.5f, 0.45f, 1), 1);
            var head = AddSprite(body, "Head", "PH_Circle", new Color(0.86f, 0.72f, 0.6f), new Vector3(0, 1.5f, 0), new Vector3(0.38f, 0.38f, 1), 2);
            var gun = AddSprite(body, "Rifle", "PH_Square", new Color(0.35f, 0.3f, 0.26f), new Vector3(0.3f, 0.95f, 0), new Vector3(0.5f, 0.09f, 1), 3);
            var danger = AddSprite(root.transform, "Danger", "PH_Diamond", new Color(1f, 0.29f, 0.11f), new Vector3(0, 1.85f, 0), new Vector3(0.3f, 0.3f, 1), 50);
            var so = new SerializedObject(view);
            so.FindProperty("body").objectReferenceValue = body;
            var arr = so.FindProperty("bodyRenderers");
            var list = new[] { overalls, vest, head, gun };
            arr.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.FindProperty("danger").objectReferenceValue = danger;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, "PH_Companion");
        }

        public static GameObject BuildReticle()
        {
            var root = new GameObject("PH_Reticle");
            var view = root.AddComponent<ReticleView>();
            var r = AddSprite(root.transform, "Reticle", "PH_Reticle", new Color(0.6f, 0.95f, 1f, 0.9f), Vector3.zero, new Vector3(0.45f, 0.45f, 1), 1000);
            var so = new SerializedObject(view);
            so.FindProperty("reticle").objectReferenceValue = r;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, "PH_Reticle");
        }

        public static GameObject BuildShop()
        {
            var root = new GameObject("PH_ShopStation");
            root.AddComponent<SortingGroup>();
            AddSprite(root.transform, "Shadow", "PH_Shadow", Color.white, Vector3.zero, new Vector3(2.4f, 1.2f, 1), -10);
            var visual = new GameObject("Visual").transform; // ENV_SHOP_Kiosk art replaces "Visual/Body"
            visual.SetParent(root.transform, false);
            AddSprite(visual, "Body", "PH_Square", new Color(0.18f, 0.25f, 0.30f), new Vector3(0, 0.75f, 0), new Vector3(2f, 1.5f, 1), 0);
            AddSprite(visual, "Screen", "PH_Square", new Color(0.4f, 0.9f, 1f, 0.85f), new Vector3(0, 1.05f, 0), new Vector3(1.4f, 0.6f, 1), 1, unlit: true);
            return Save(root, "PH_ShopStation");
        }
    }
}
