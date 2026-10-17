using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Builds the Persian UI font (D64): a dynamic TextMeshPro font asset from Vazirmatn, with the old Latin
    /// font as fallback for symbols Vazirmatn does not have (▲, ∞). RTLTMPro turns Persian text into Arabic
    /// presentation forms, so those code points are pre-added and checked.
    /// </summary>
    public static class PersianFont
    {
        public const string TtfPath = "Assets/Fonts/Vazirmatn/Vazirmatn-Regular.ttf";
        public const string AssetPath = "Assets/Resources/Fonts/Vazirmatn SDF.asset";
        const string FallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const string StringsPath = "Assets/Resources/Localization/fa.json";
        const string DialoguePath = "Assets/Resources/Narrative/Dialogue.json";

        [MenuItem("Hollow/UI/Build Persian Font")]
        public static void BuildMenu() => Debug.Log("[Hollow] " + Build());

        public static string Build()
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            if (ttf == null) return "Vazirmatn TTF missing at " + TtfPath;

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (existing != null) AssetDatabase.DeleteAsset(AssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath)!);

            var asset = TMP_FontAsset.CreateFontAsset(ttf, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = "Vazirmatn SDF";
            AssetDatabase.CreateAsset(asset, AssetPath);
            asset.atlasTexture.name = "Vazirmatn SDF Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            asset.material.name = "Vazirmatn SDF Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
            asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (fallback != null) asset.fallbackFontAssetTable.Add(fallback);

            string needed = NeededCharacters();
            asset.TryAddCharacters(needed, out string missing);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            var theme = ProjectSetup.LoadOrCreateTheme();
            theme.Font = asset;
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();

            var report = new StringBuilder();
            foreach (char c in missing ?? "") report.Append($"U+{(int)c:X4} ");
            return $"Built {AssetPath}: {needed.Length} characters, missing from Vazirmatn: {(report.Length == 0 ? "none" : report.ToString())}";
        }

        /// <summary>ASCII, Persian letters and digits, the presentation forms RTLTMPro emits, and every UI string.</summary>
        public static string NeededCharacters()
        {
            var set = new SortedSet<char>();
            void Range(int a, int b) { for (int c = a; c <= b; c++) set.Add((char)c); }
            Range(0x20, 0x7E);
            Range(0x060C, 0x060C); Range(0x061B, 0x061B); Range(0x061F, 0x061F);
            Range(0x0621, 0x063A); Range(0x0641, 0x064A); Range(0x064B, 0x0652); // letters and harakat
            Range(0x066A, 0x066C); Range(0x067E, 0x067E); Range(0x0686, 0x0686); Range(0x0698, 0x0698);
            Range(0x06A9, 0x06A9); Range(0x06AF, 0x06AF); Range(0x06CC, 0x06CC); Range(0x06F0, 0x06F9);
            Range(0x200C, 0x200C); Range(0x00AB, 0x00AB); Range(0x00BB, 0x00BB); Range(0x00D7, 0x00D7);
            Range(0x00B0, 0x00B0); Range(0x00B7, 0x00B7); Range(0x221E, 0x221E); // °, ·, ∞
            foreach (char c in PresentationForms()) set.Add(c);
            foreach (var path in new[] { StringsPath, DialoguePath })
                if (File.Exists(path))
                    foreach (char c in File.ReadAllText(path)) if (c >= 0x20) set.Add(c);
            var sb = new StringBuilder();
            foreach (char c in set) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>The isolated/initial/medial/final forms RTLTMPro can produce for Persian letters.</summary>
        static IEnumerable<char> PresentationForms()
        {
            // Persian-specific letters (Forms-A) plus the Arabic letter forms (Forms-B) and lam-alef ligatures.
            int[] formsA = { 0xFB56, 0xFB57, 0xFB58, 0xFB59, 0xFB7A, 0xFB7B, 0xFB7C, 0xFB7D, 0xFB8A, 0xFB8B,
                0xFB8E, 0xFB8F, 0xFB90, 0xFB91, 0xFB92, 0xFB93, 0xFB94, 0xFB95, 0xFBFC, 0xFBFD, 0xFBFE, 0xFBFF };
            foreach (int c in formsA) yield return (char)c;
            for (int c = 0xFE80; c <= 0xFEFC; c++) yield return (char)c;
        }
    }
}
