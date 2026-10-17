using System.IO;
using Hollow.App;
using Hollow.Data;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>A focused art-direction dashboard over the Atmosphere section of the balance profile.</summary>
    public sealed class VisualTuningWindow : EditorWindow
    {
        AtmosphereData draft;
        int phase;
        string status = "";
        Vector2 scroll;

        [MenuItem("Hollow/Visual Tuning")]
        public static void Open() => GetWindow<VisualTuningWindow>("Hollow Visuals");

        void OnEnable()
        {
            minSize = new Vector2(430, 520);
            Reload();
        }

        void OnInspectorUpdate() => Repaint();

        void Reload()
        {
            if (Application.isPlaying && GameApp.Instance != null)
                draft = Copy(GameApp.Instance.Balance.Live.Atmosphere);
            else if (File.Exists(BaselineSeed.BaselinePath))
                draft = Copy(BalanceJson.FromJson(File.ReadAllText(BaselineSeed.BaselinePath)).Atmosphere);
            else draft = Copy(BaselineSeed.Create().Atmosphere);
        }

        static AtmosphereData Copy(AtmosphereData source) =>
            JsonUtility.FromJson<AtmosphereData>(JsonUtility.ToJson(source));

        void OnGUI()
        {
            if (draft == null) Reload();
            EditorGUILayout.LabelField("Hollow · Visual Tuning", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Edit the look without changing camera angle or combat rules. In Play Mode, Apply previews the changes immediately; Save writes them to Baseline.json for future runs.", MessageType.Info);
            phase = GUILayout.Toolbar(phase, new[] { "Forest", "Industry", "Human Base" });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawPhase(phase + 1);
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Shared effects", EditorStyles.boldLabel);
            draft.BloomIntensity = EditorGUILayout.Slider(new GUIContent("Bloom", "Soft glow on bright sources; keep warning silhouettes sharp."), draft.BloomIntensity, 0f, 1f);
            draft.BloomThreshold = EditorGUILayout.Slider("Bloom threshold", draft.BloomThreshold, 0f, 2f);
            draft.PlayerLightRadius = EditorGUILayout.Slider("Player glow radius", draft.PlayerLightRadius, 2f, 14f);
            draft.PlayerLightIntensity = EditorGUILayout.Slider("Player glow intensity", draft.PlayerLightIntensity, 0f, 1.5f);
            draft.GroundVariation = EditorGUILayout.Slider(new GUIContent("Ground variation", "Broad, smooth brightness variation breaks up the repeated 8 x 8 floor art."), draft.GroundVariation, 0f, 0.22f);
            draft.PhaseFadeTime = EditorGUILayout.Slider("Phase transition seconds", draft.PhaseFadeTime, 0f, 5f);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply to running game", GUILayout.Height(30))) ApplyPreview();
            if (GUILayout.Button("Save as default", GUILayout.Height(30))) SaveDefault();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload current")) Reload();
            if (GUILayout.Button("Reset to suggested look")) draft = Copy(BaselineSeed.Create().Atmosphere);
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
        }

        void DrawPhase(int p)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(p == 1 ? "Living forest" : p == 2 ? "Steel and machinery" : "Cold human base", EditorStyles.boldLabel);
            string hex = draft.LightColor(p);
            if (!ColorUtility.TryParseHtmlString(hex, out var color)) color = Color.white;
            color = EditorGUILayout.ColorField("Ambient light color", color);
            hex = "#" + ColorUtility.ToHtmlStringRGB(color);
            float light = EditorGUILayout.Slider("Ambient brightness", draft.Light(p), 0.35f, 1.3f);
            float fog = EditorGUILayout.Slider("Ground mist", draft.Fog(p), 0f, 0.22f);
            float contrast = EditorGUILayout.Slider("Contrast", draft.Contrast(p), 0f, 25f);
            float desaturation = EditorGUILayout.Slider("Desaturation", draft.Desaturation(p), 0f, 30f);
            float vignette = EditorGUILayout.Slider("Edge shading", draft.Vignette(p), 0f, 0.3f);
            if (p == 1)
            {
                draft.LightColorPhase1 = hex; draft.LightPhase1 = light; draft.FogPhase1 = fog;
                draft.ContrastPhase1 = contrast; draft.DesaturationPhase1 = desaturation; draft.VignettePhase1 = vignette;
            }
            else if (p == 2)
            {
                draft.LightColorPhase2 = hex; draft.LightPhase2 = light; draft.FogPhase2 = fog;
                draft.ContrastPhase2 = contrast; draft.DesaturationPhase2 = desaturation; draft.VignettePhase2 = vignette;
            }
            else
            {
                draft.LightColorPhase3 = hex; draft.LightPhase3 = light; draft.FogPhase3 = fog;
                draft.ContrastPhase3 = contrast; draft.DesaturationPhase3 = desaturation; draft.VignettePhase3 = vignette;
            }
            EditorGUILayout.HelpBox("Keep enemies, coins and red attack telegraphs readable at actual Game view size. Use small adjustments, then compare a combat screenshot.", MessageType.None);
        }

        void ApplyPreview()
        {
            var service = GameApp.Instance != null ? GameApp.Instance.Balance : null;
            if (!Application.isPlaying || service == null)
            {
                status = "Enter Play Mode in the Game scene to preview, or Save as default for the next run.";
                return;
            }
            service.Draft.Atmosphere = Copy(draft);
            service.NotifyDraftEdited();
            status = service.Apply() ? "Applied to the running game. Changes can be undone in Balance Panel." : "The balance validator rejected these values.";
        }

        void SaveDefault()
        {
            if (!File.Exists(BaselineSeed.BaselinePath))
            {
                status = "Baseline.json was not found.";
                return;
            }
            var profile = BalanceJson.FromJson(File.ReadAllText(BaselineSeed.BaselinePath));
            profile.Atmosphere = Copy(draft);
            var errors = BalanceValidator.Validate(profile).FindAll(issue => issue.Severity == IssueSeverity.Error);
            if (errors.Count > 0)
            {
                status = "Save blocked: " + errors[0];
                return;
            }
            File.WriteAllText(BaselineSeed.BaselinePath, BalanceJson.ToJson(profile));
            AssetDatabase.ImportAsset(BaselineSeed.BaselinePath);
            status = "Saved. A new run uses this look; the current run needs Apply to running game.";
        }
    }
}
