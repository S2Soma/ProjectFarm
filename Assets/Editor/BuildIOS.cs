using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>The iOS build: an Xcode project, which is all Unity makes for iOS.
    ///
    ///     Menu:      Tools ▸ LQ Farm ▸ Build iOS (Xcode project)
    ///     Batchmode: Unity -batchmode -quit -projectPath . -buildTarget iOS
    ///                      -executeMethod LQFarm.EditorTools.BuildIOS.Build
    ///
    /// Turning the project into something an iPhone will install needs a Mac with Xcode and an
    /// Apple Developer team to sign it: open Unity-iPhone.xcodeproj, pick the team under Signing &amp;
    /// Capabilities, then Product ▸ Archive (TestFlight / Ad Hoc) or Run on a cabled device.
    ///
    /// Output: Builds/iOS/MATUFarm-Xcode/</summary>
    public static class BuildIOS
    {
        const string BundleId = "com.mitfarm.game";

        [MenuItem("Tools/LQ Farm/Build iOS (Xcode project)")]
        public static void BuildFromMenu() { Build(); }

        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                Debug.LogError("[BuildIOS] THẤT BẠI — chưa cài iOS Build Support (hoặc cài xong mà chưa khởi động lại Editor).");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

            AppIcons.Apply();
            PlayerSettings.productName = BuildAndroid.ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.iOS.targetOSVersionString = "15.0";   // the lowest Unity 6 builds for
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            // a landscape-only game must opt out of iPad split view, or App Store validation refuses it
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.development = false;

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "iOS", "MATUFarm-Xcode"));
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(Path.GetDirectoryName(dir));

            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = dir,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            });

            var sum = report.summary;
            bool project = Directory.Exists(Path.Combine(dir, "Unity-iPhone.xcodeproj"));
            if (sum.result == BuildResult.Succeeded && project)
                Debug.Log($"[BuildIOS] OK — {dir} ({sum.totalTime.TotalMinutes:0.0} phút)");
            else
            {
                Debug.LogError($"[BuildIOS] THẤT BẠI — {(project ? sum.result.ToString() : "không có Unity-iPhone.xcodeproj")}, {sum.totalErrors} lỗi");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
