using System.Linq;
using Hollow.App;
using Hollow.Game;
using Hollow.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Reproducible project setup: scenes, build list, 2D renderer sorting. Safe to run again.
    /// </summary>
    public static class ProjectSetup
    {
        const string ScenesDir = "Assets/Scenes";
        const string RendererPath = "Assets/Settings/Renderer2D.asset";

        public static string ScenePath(string name) => $"{ScenesDir}/{name}.unity";

        [MenuItem("Hollow/Setup/Run Full Setup")]
        public static void RunAll()
        {
            RemoveTemplateLeftovers();
            ConfigureRenderer();
            BuildBootScene();
            BuildMainMenuScene();
            BuildGameScene();
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Hollow] Project setup complete.");
        }

        [MenuItem("Hollow/Setup/Remove Template Leftovers")]
        public static void RemoveTemplateLeftovers()
        {
            foreach (var path in new[] { "Assets/Welcome", ScenePath("SampleScene") })
                if (AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path))
                    AssetDatabase.DeleteAsset(path);
        }

        /// <summary>Characters lower on screen draw in front (sorting by feet, D16).</summary>
        [MenuItem("Hollow/Setup/Configure 2D Renderer")]
        public static void ConfigureRenderer()
        {
            var data = AssetDatabase.LoadMainAssetAtPath(RendererPath);
            if (data == null) { Debug.LogError($"[Hollow] Missing {RendererPath}"); return; }
            var so = new SerializedObject(data);
            so.FindProperty("m_TransparencySortMode").enumValueIndex = (int)TransparencySortMode.CustomAxis;
            so.FindProperty("m_TransparencySortAxis").vector3Value = new Vector3(0f, 1f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }

        static void BuildBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Boot").AddComponent<BootLoader>();
            EditorSceneManager.SaveScene(scene, ScenePath(SceneNames.Boot));
        }

        static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera("MenuCamera");
            new GameObject("MainMenu").AddComponent<MainMenuPlaceholder>();
            EditorSceneManager.SaveScene(scene, ScenePath(SceneNames.MainMenu));
        }

        [MenuItem("Hollow/Setup/Rebuild Game Scene")]
        public static void BuildGameScene()
        {
            PlaceholderArt.GenerateAll();
            var playerPrefab = PrefabBuilder.BuildPlayer();
            var reticlePrefab = PrefabBuilder.BuildReticle();
            var shopPrefab = PrefabBuilder.BuildShop();
            var companionPrefab = PrefabBuilder.BuildCompanion();
            var catalog = EnemyPrefabBuilder.BuildAll();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = CreateCamera("WorldCamera");
            cam.gameObject.AddComponent<CinemachineBrain>();

            var arena = ArenaTools.BuildWhitebox();
            var shop = (GameObject)PrefabUtility.InstantiatePrefab(shopPrefab);
            shop.transform.position = arena.Shop.position + Vector3.down * 0.5f;

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            var companion = (GameObject)PrefabUtility.InstantiatePrefab(companionPrefab);
            var reticle = (GameObject)PrefabUtility.InstantiatePrefab(reticlePrefab);

            // Camera: follow target + Cinemachine camera confined to the arena.
            var target = new GameObject("CameraFollowTarget").transform;
            var boundsGo = new GameObject("CameraBounds");
            var bounds = boundsGo.AddComponent<PolygonCollider2D>();
            bounds.isTrigger = true;
            float hx = arena.Size.x / 2f, hy = arena.Size.y / 2f;
            bounds.points = new[] { new Vector2(-hx, -hy), new Vector2(hx, -hy), new Vector2(hx, hy), new Vector2(-hx, hy) };
            var vcamGo = new GameObject("CM FollowCamera");
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Target.TrackingTarget = target;
            var lens = vcam.Lens;
            lens.OrthographicSize = 7.5f;
            vcam.Lens = lens;
            var composer = vcamGo.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            var confiner = vcamGo.AddComponent<CinemachineConfiner2D>();
            confiner.BoundingShape2D = bounds;
            vcamGo.AddComponent<CinemachineImpulseListener>();

            var rig = new GameObject("CameraRig").AddComponent<CameraRig>();
            Wire(rig, ("followTarget", target), ("virtualCamera", vcam), ("composer", composer));

            // Reload by path: creating the scene can unload assets made earlier in this call.
            catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(EnemyPrefabBuilder.CatalogPath);
            var presenter = new GameObject("WorldPresenter").AddComponent<WorldPresenter>();
            Wire(presenter, ("catalog", catalog), ("playerView", player.GetComponent<PlayerView>()), ("companionView", companion.GetComponent<CompanionView>()));

            var session = new GameObject("GameSession").AddComponent<GameSession>();
            Wire(session, ("worldCamera", cam), ("arena", arena), ("presenter", presenter),
                ("cameraRig", rig), ("reticle", reticle.GetComponent<ReticleView>()));

            var uiRoot = new GameObject("UI").AddComponent<Hollow.UI.UIRoot>();
            Wire(uiRoot, ("theme", LoadOrCreateTheme()));
            Wire(session, ("ui", uiRoot));

            var sandbox = new GameObject("DevSandboxSpawner (temporary)").AddComponent<DevSandboxSpawner>();
            Wire(sandbox, ("session", session));

            ArenaTools.Bake(arena);
            EditorSceneManager.SaveScene(scene, ScenePath(SceneNames.Game));
        }

        const string ThemePath = "Assets/Art/Placeholders/UITheme.asset";
        const string DefaultFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        public static Hollow.UI.UITheme LoadOrCreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<Hollow.UI.UITheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<Hollow.UI.UITheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            if (theme.Font == null) theme.Font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(DefaultFontPath);
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            return theme;
        }

        static void Wire(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                var prop = so.FindProperty(field);
                if (prop == null) { Debug.LogError($"[Hollow] {target.GetType().Name} has no field '{field}'"); continue; }
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static Camera CreateCamera(string name)
        {
            var go = new GameObject(name) { tag = "MainCamera" };
            go.transform.position = new Vector3(0, 0, -10);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.09f, 0.1f);
            return cam;
        }

        static void RegisterBuildScenes()
        {
            EditorBuildSettings.scenes = new[] { SceneNames.Boot, SceneNames.MainMenu, SceneNames.Game }
                .Select(n => new EditorBuildSettingsScene(ScenePath(n), true))
                .ToArray();
        }
    }
}
