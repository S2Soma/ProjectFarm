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

        /// <summary>Bumping this makes Unity re-run the rule over every texture it touched, so a
        /// change here reaches existing art without anyone reimporting by hand. 2: mipmaps.
        /// 3: rings and circles (the HUD weather ring is always shown at half size).
        /// 4: the bridge folder, and U-repeat on the bridge strip.</summary>
        public override uint GetVersion() { return 4; }

        /// <summary>Art that is drawn MINIFIED gets mipmaps.
        ///
        /// This rule used to force mipmaps off for everything, and it runs on every import — so
        /// the <c>enableMipMap: 1</c> written into those .meta files by the art generators was
        /// silently put back to 0 on the next import. The islands, beds and crops are drawn from
        /// 0.14x (whole archipelago) to 1.35x; without mips they shimmer and break up zoomed out.
        /// Icons are drawn at 18-150 px from 256 px sources, and the material shapes are always
        /// shown smaller than they are baked (see Chrome.Shape).
        ///
        /// Everything else — the Kenney skins, weather glyphs, backgrounds shown near 1:1 — stays
        /// without, since a trilinear blend there only softens it.</summary>
        static bool WantsMips(string path)
        {
            string[] dirs = { "beds/", "islands/", "gen/", "farm/", "crops_gen/", "crop/", "items/", "tiles/", "bridge/", "fx/", "pets/" };
            foreach (var d in dirs)
                if (path.StartsWith(Root + d)) return true;
            // the arrival cinematic: the heaps and puffs start as specks and end larger than the
            // screen, so they need mips; the veil and the light shafts are only ever drawn large
            if (path.StartsWith(Root + "cine/"))
            {
                string file = System.IO.Path.GetFileName(path);
                return file.StartsWith("cine_mass") || file.StartsWith("cine_puff");
            }
            if (path.StartsWith(Root + "chrome/"))
            {
                string file = System.IO.Path.GetFileName(path);
                return file.StartsWith("shape_") || file.StartsWith("soft_") || file.StartsWith("top_")
                    || file.StartsWith("ring_") || file.StartsWith("circle");
            }
            return false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;

            var im = (TextureImporter)assetImporter;
            im.textureType = TextureImporterType.Sprite;
            im.spriteImportMode = SpriteImportMode.Single;
            im.spritePixelsPerUnit = 100f;
            bool mips = WantsMips(assetPath);
            im.mipmapEnabled = mips;
            im.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            im.alphaIsTransparency = true;
            im.filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear;
            im.wrapMode = TextureWrapMode.Clamp;
            // The bridge strip is laid along a path of any length by BridgeRibbon and repeats
            // horizontally; its rows are separated vertically, so V must still clamp.
            if (assetPath.EndsWith("bridge/bridge_strip.png"))
            {
                im.wrapModeU = TextureWrapMode.Repeat;
                im.wrapModeV = TextureWrapMode.Clamp;
            }
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
