using System.IO;
using Hollow.App;
using Hollow.Data;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>
    /// A focused art-direction dashboard over the Atmosphere section of the balance profile. Every edit is applied to
    /// the running game and saved to Baseline.json a moment after the slider stops, so it stays for later runs (D96).
    /// </summary>
    public sealed class VisualTuningWindow : EditorWindow
    {
        AtmosphereData draft;
        int phase;
        string status = "";
        Vector2 scroll;
        double editedAt = -1;
        const double SaveDelay = 0.4;

        [MenuItem("Hollow/Visual Tuning")]
        public static void Open() => GetWindow<VisualTuningWindow>("Hollow Visuals");

        void OnEnable()
        {
            minSize = new Vector2(430, 520);
            Reload();
        }

        void OnInspectorUpdate()
        {
            if (editedAt >= 0 && EditorApplication.timeSinceStartup - editedAt > SaveDelay)
            {
                editedAt = -1;
                Commit();
            }
            Repaint();
        }

        void OnDisable()
        {
            if (editedAt >= 0) { editedAt = -1; Commit(); }
        }

        /// <summary>The running game shows the edit (which also saves it); outside Play it is saved straight away.</summary>
        void Commit()
        {
            if (Application.isPlaying && GameApp.Instance != null) ApplyPreview();
            else SaveDefault();
        }

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
            EditorGUILayout.HelpBox("Edit the look without changing camera angle or combat rules. Changes show in the running game and are saved to Baseline.json automatically, before or during Play.", MessageType.Info);
            phase = GUILayout.Toolbar(phase, new[] { "Forest", "Industry", "Human Base" });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            DrawPhase(phase + 1);
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Shared effects", EditorStyles.boldLabel);
            draft.ComposedGround = EditorGUILayout.Toggle("Composed arena floor", draft.ComposedGround);
            draft.ContactShadowStrength = EditorGUILayout.Slider("Prop contact shadows", draft.ContactShadowStrength, 0f, 1f);
            draft.BloomIntensity = EditorGUILayout.Slider(new GUIContent("Bloom", "Soft glow on bright sources; keep warning silhouettes sharp."), draft.BloomIntensity, 0f, 1.5f);
            draft.BloomThreshold = EditorGUILayout.Slider("Bloom threshold", draft.BloomThreshold, 0f, 2f);
            draft.BloomScatter = EditorGUILayout.Slider("Bloom spread", draft.BloomScatter, 0f, 1f);
            draft.ChromaticAberration = EditorGUILayout.Slider("Lens fringe", draft.ChromaticAberration, 0f, 0.5f);
            draft.HurtPulse = EditorGUILayout.Slider(new GUIContent("Hurt pulse", "Red edge flash on a hit and the low-health heartbeat."), draft.HurtPulse, 0f, 0.5f);
            draft.LowHealthFraction = EditorGUILayout.Slider("Heartbeat below health", draft.LowHealthFraction, 0f, 0.6f);
            draft.FogBreath = EditorGUILayout.Slider("Mist breathing", draft.FogBreath, 0f, 1f);
            draft.LightPulseSpeed = EditorGUILayout.Slider("Light swell speed", draft.LightPulseSpeed, 0f, 1f);
            draft.FlickerDepth = EditorGUILayout.Slider("Brownout depth", draft.FlickerDepth, 0f, 1f);
            draft.SearchlightIntensity = EditorGUILayout.Slider("Searchlight brightness", draft.SearchlightIntensity, 0f, 1.5f);
            draft.PlayerLightRadius = EditorGUILayout.Slider("Player glow radius", draft.PlayerLightRadius, 2f, 14f);
            draft.PlayerLightIntensity = EditorGUILayout.Slider("Player glow intensity", draft.PlayerLightIntensity, 0f, 1.5f);
            draft.GroundVariation = EditorGUILayout.Slider(new GUIContent("Ground variation", "Broad, smooth brightness variation breaks up the repeated 8 x 8 floor art."), draft.GroundVariation, 0f, 0.22f);
            draft.PhaseFadeTime = EditorGUILayout.Slider("Phase transition seconds", draft.PhaseFadeTime, 0f, 5f);
            if (EditorGUI.EndChangeCheck()) editedAt = EditorApplication.timeSinceStartup;
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply to running game", GUILayout.Height(30))) ApplyPreview();
            if (GUILayout.Button("Save as default", GUILayout.Height(30))) SaveDefault();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload current")) { editedAt = -1; Reload(); }
            if (GUILayout.Button("Reset to suggested look"))
            {
                draft = Copy(BaselineSeed.Create().Atmosphere);
                editedAt = EditorApplication.timeSinceStartup;
            }
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
            float fog = EditorGUILayout.Slider("Ground mist", draft.Fog(p), 0f, 0.5f);
            float highFog = EditorGUILayout.Slider(new GUIContent("Mist over bodies", "Drifts over the characters: keep it faint."), draft.HighFog(p), 0f, 0.3f);
            if (!ColorUtility.TryParseHtmlString(draft.FogColor(p), out var fogColor)) fogColor = Color.white;
            string fogHex = "#" + ColorUtility.ToHtmlStringRGB(EditorGUILayout.ColorField("Mist color", fogColor));
            float pulse = EditorGUILayout.Slider("Light swell", draft.LightPulse(p), 0f, 0.4f);
            float flickers = EditorGUILayout.Slider("Brownouts per minute", draft.FlickersPerMinute(p), 0f, 20f);
            int searchlights = EditorGUILayout.IntSlider("Searchlights", draft.Searchlights(p), 0, 3);
            if (!ColorUtility.TryParseHtmlString(draft.ShadowTint(p), out var shadowTint)) shadowTint = Color.gray;
            if (!ColorUtility.TryParseHtmlString(draft.HighlightTint(p), out var highlightTint)) highlightTint = Color.gray;
            string shadowHex = "#" + ColorUtility.ToHtmlStringRGB(EditorGUILayout.ColorField("Shadow tint", shadowTint));
            string highlightHex = "#" + ColorUtility.ToHtmlStringRGB(EditorGUILayout.ColorField("Highlight tint", highlightTint));
            float grainAmount = EditorGUILayout.Slider("Film grain", draft.Grain(p), 0f, 1f);
            float contrast = EditorGUILayout.Slider("Contrast", draft.Contrast(p), 0f, 30f);
            float desaturation = EditorGUILayout.Slider("Desaturation", draft.Desaturation(p), 0f, 30f);
            float vignette = EditorGUILayout.Slider("Edge shading", draft.Vignette(p), 0f, 0.5f);
            if (p == 1)
            {
                draft.LightColorPhase1 = hex; draft.LightPhase1 = light; draft.FogPhase1 = fog;
                draft.ContrastPhase1 = contrast; draft.DesaturationPhase1 = desaturation; draft.VignettePhase1 = vignette;
                draft.HighFogPhase1 = highFog; draft.FogColorPhase1 = fogHex; draft.LightPulsePhase1 = pulse; draft.FlickersPerMinutePhase1 = flickers;
                draft.SearchlightsPhase1 = searchlights; draft.ShadowTintPhase1 = shadowHex; draft.HighlightTintPhase1 = highlightHex; draft.GrainPhase1 = grainAmount;
            }
            else if (p == 2)
            {
                draft.LightColorPhase2 = hex; draft.LightPhase2 = light; draft.FogPhase2 = fog;
                draft.ContrastPhase2 = contrast; draft.DesaturationPhase2 = desaturation; draft.VignettePhase2 = vignette;
                draft.HighFogPhase2 = highFog; draft.FogColorPhase2 = fogHex; draft.LightPulsePhase2 = pulse; draft.FlickersPerMinutePhase2 = flickers;
                draft.SearchlightsPhase2 = searchlights; draft.ShadowTintPhase2 = shadowHex; draft.HighlightTintPhase2 = highlightHex; draft.GrainPhase2 = grainAmount;
            }
            else
            {
                draft.LightColorPhase3 = hex; draft.LightPhase3 = light; draft.FogPhase3 = fog;
                draft.ContrastPhase3 = contrast; draft.DesaturationPhase3 = desaturation; draft.VignettePhase3 = vignette;
                draft.HighFogPhase3 = highFog; draft.FogColorPhase3 = fogHex; draft.LightPulsePhase3 = pulse; draft.FlickersPerMinutePhase3 = flickers;
                draft.SearchlightsPhase3 = searchlights; draft.ShadowTintPhase3 = shadowHex; draft.HighlightTintPhase3 = highlightHex; draft.GrainPhase3 = grainAmount;
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
            status = service.Apply() ? "Applied to the running game and saved to Baseline.json." : "The balance validator rejected these values.";
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
            BalanceAutoSave.Write(profile, out _);
            status = "Saved to Baseline.json; every run uses this look.";
        }
    }
}
