using System.Linq;
using Hollow.App;
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

        static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = CreateCamera("WorldCamera");
            var root = new GameObject("GameSceneRoot").AddComponent<GameSceneRoot>();
            var so = new SerializedObject(root);
            so.FindProperty("worldCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath(SceneNames.Game));
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
