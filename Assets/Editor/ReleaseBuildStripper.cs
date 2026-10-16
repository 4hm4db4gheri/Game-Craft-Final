using Hollow.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollow.EditorTools
{
    /// <summary>Release builds (not development) ship without the F10 dev panel (§13, T18).</summary>
    public sealed class ReleaseBuildStripper : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!BuildPipeline.isBuildingPlayer || EditorUserBuildSettings.development) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var panel in root.GetComponentsInChildren<DevPanel>(true))
                {
                    Debug.Log($"[Hollow] Release build: removed {panel.name} from {scene.name}.");
                    Object.DestroyImmediate(panel.gameObject);
                }
        }
    }
}
