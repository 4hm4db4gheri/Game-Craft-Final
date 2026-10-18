using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>Story stills (Resources/Cutscenes, made by Tools/ArtGen/cutscenes.py) keep their full size and shape.</summary>
    public sealed class CutsceneImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Cutscenes/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = 80;
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.maxTextureSize = 2048;
            platform.textureCompression = TextureImporterCompression.Compressed;
            platform.crunchedCompression = true;
            platform.compressionQuality = 80;
            importer.SetPlatformTextureSettings(platform);
        }
    }
}
