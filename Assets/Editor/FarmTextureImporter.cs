using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Import rules for the game art.
    ///
    /// Unity's defaults compress sprites (DXT/ETC) and build a tight mesh, which chewed
    /// visible holes in the alpha edges of the painted tiles and crops. Everything under
    /// Resources/Art is imported uncompressed for the editor and ASTC on device, with a
    /// full-rect mesh so nothing is clipped.</summary>
    public class FarmTextureImporter : AssetPostprocessor
    {
        const string Root = "Assets/Resources/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;

            var im = (TextureImporter)assetImporter;
            im.textureType = TextureImporterType.Sprite;
            im.spriteImportMode = SpriteImportMode.Single;
            im.spritePixelsPerUnit = 100f;
            im.mipmapEnabled = false;
            im.alphaIsTransparency = true;
            im.filterMode = FilterMode.Bilinear;
            im.wrapMode = TextureWrapMode.Clamp;
            im.maxTextureSize = 2048;
            im.textureCompression = TextureImporterCompression.Uncompressed;
            im.npotScale = TextureImporterNPOTScale.None;

            // a tight mesh trims into soft edges; keep the full quad
            var settings = new TextureImporterSettings();
            im.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 1;
            im.SetTextureSettings(settings);

            // on device, ASTC keeps the quality while staying small
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var ps = im.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = 2048;
                ps.format = TextureImporterFormat.ASTC_4x4;
                ps.compressionQuality = 100;
                im.SetPlatformTextureSettings(ps);
            }
        }
    }
}
