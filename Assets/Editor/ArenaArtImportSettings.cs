using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Arena art keeps its alpha and is crunched to keep the build small. Images whose sides are not a multiple of
    /// 4 (1254 × 1254 landmarks) cannot be block-compressed, so they import at 1024; runtime crops are normalized
    /// to the imported size (ImportedAtlasRect).
    /// </summary>
    public sealed class ArenaArtImportSettings : AssetPostprocessor
    {
        const int Quality = 75;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/ArenaArt/")) return;
            var importer = (TextureImporter)assetImporter;
            bool detail = System.IO.Path.GetFileName(assetPath).StartsWith("Detail");
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            int max = w % 4 != 0 || h % 4 != 0 ? 1024 : 4096;
            importer.maxTextureSize = max;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = Quality;
            importer.mipmapEnabled = detail;
            importer.alphaIsTransparency = !detail;
            importer.wrapMode = detail ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = detail ? FilterMode.Trilinear : FilterMode.Bilinear;
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.maxTextureSize = max;
            platform.textureCompression = TextureImporterCompression.Compressed;
            platform.crunchedCompression = true;
            platform.compressionQuality = Quality;
            importer.SetPlatformTextureSettings(platform);
        }

        [MenuItem("Hollow/Art/Reimport Arena Artwork")]
        public static void Reimport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/ArenaArt" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
    }
}
