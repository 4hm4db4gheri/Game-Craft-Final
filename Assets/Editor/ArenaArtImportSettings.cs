using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>Preserve authored resolution and alpha for arena art; all runtime crops are normalized too.</summary>
    public sealed class ArenaArtImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/ArenaArt/")) return;
            var importer = (TextureImporter)assetImporter;
            bool detail = System.IO.Path.GetFileName(assetPath).StartsWith("Detail");
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = detail;
            importer.alphaIsTransparency = !detail;
            importer.wrapMode = detail ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = detail ? FilterMode.Trilinear : FilterMode.Bilinear;
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.maxTextureSize = 4096;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
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
