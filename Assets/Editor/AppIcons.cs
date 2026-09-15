using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Icon art imports uncompressed and without mipmaps: Unity resamples it down to every
    /// launcher size itself, and a compressed 1024 source turned the plant's outline to mush at
    /// 48 px ("Compressed texture app_icon is used as icon" on every build).</summary>
    public class AppIconImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/AppIcon/")) return;
            var im = (TextureImporter)assetImporter;
            im.textureType = TextureImporterType.Default;
            im.textureCompression = TextureImporterCompression.Uncompressed;
            im.mipmapEnabled = false;
            im.alphaIsTransparency = true;
            im.npotScale = TextureImporterNPOTScale.None;
            im.maxTextureSize = 1024;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var ps = im.GetPlatformTextureSettings(platform);
                ps.overridden = false;
                im.SetPlatformTextureSettings(ps);
            }
        }
    }

    /// <summary>The launcher icon and the launch screen, applied to Player Settings.
    ///
    /// Art is Tools/gen_appicon.py → Assets/Art/AppIcon (outside Resources, so it never ships as
    /// game data). Android gets the adaptive pair — sky behind, island and plant in front inside
    /// the safe circle — because a launcher that masks a flat legacy icon either crops the plant
    /// or shrinks the whole square onto a white plate. Every other kind gets the full-bleed one.
    ///
    /// The launch screen shows no Unity logo and paints the game's sky colour, so the first
    /// frame after tapping the icon is already the game's own colour instead of a black flash.
    ///
    /// Run from Tools ▸ LQ Farm, and by <see cref="BuildAndroid.Build"/> before every build.</summary>
    public static class AppIcons
    {
        const string Dir = "Assets/Art/AppIcon/";
        static readonly Color SkyTop = new Color(92 / 255f, 176 / 255f, 232 / 255f, 1f);

        [MenuItem("Tools/LQ Farm/Đặt icon & màn khởi động")]
        public static void ApplyFromMenu() { Apply(); }

        public static bool Apply()
        {
            // re-import under AppIconImporter's rules (a no-op once they have been applied)
            foreach (var n in new[] { "app_icon.png", "icon_back.png", "icon_fore.png" })
            {
                var ti = AssetImporter.GetAtPath(Dir + n) as TextureImporter;
                if (ti != null && ti.textureCompression != TextureImporterCompression.Uncompressed)
                    AssetDatabase.ImportAsset(Dir + n, ImportAssetOptions.ForceUpdate);
            }
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "app_icon.png");
            var back = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "icon_back.png");
            var fore = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "icon_fore.png");
            if (full == null || back == null || fore == null)
            {
                Debug.LogError("[AppIcons] Thiếu ảnh icon ở " + Dir + " — chạy Tools/gen_appicon.py trước.");
                return false;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);

            var android = NamedBuildTarget.Android;
            int set = 0;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(android))
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (var icon in icons)
                {
                    if (icon.maxLayerCount >= 2) icon.SetTextures(back, fore);
                    else icon.SetTexture(full);
                    set++;
                }
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }

            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = SkyTop;

            AssetDatabase.SaveAssets();
            Debug.Log($"[AppIcons] Đã đặt icon ({set} ô Android) và màn khởi động.");
            return true;
        }
    }
}
