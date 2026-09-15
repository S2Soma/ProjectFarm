using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>A test APK for a phone.
    ///
    ///     Menu:      Tools ▸ LQ Farm ▸ Build Android (APK thử)
    ///     Batchmode: Unity -batchmode -quit -projectPath . -buildTarget Android
    ///                      -executeMethod LQFarm.EditorTools.BuildAndroid.Build
    ///
    /// IL2CPP / ARM64: phones sold since 2023 increasingly refuse 32-bit apps, and Mono on Android
    /// only builds 32-bit. Signed with Unity's debug key, so it installs by sideloading (adb or a
    /// file copy) but is not for the store. Landscape only, as the game is laid out.
    ///
    /// Output: Builds/Android/MATUFarm-&lt;version&gt;-&lt;yyyyMMdd-HHmm&gt;.apk
    ///
    /// The game is called MATU FArM; "LQFarm" survives only in code (namespace, save file name,
    /// the Tools menu), where renaming would cost saves and buy nothing a player sees.</summary>
    public static class BuildAndroid
    {
        public const string ProductName = "MATU FArM";
        /// <summary>Changed from com.lqfarm.game with the rename: a phone treats it as a new app,
        /// installed beside the old test build, with its own fresh save.</summary>
        const string BundleId = "com.mitfarm.game";

        [MenuItem("Tools/LQ Farm/Build Android (APK thử)")]
        public static void BuildFromMenu() { Build(); }

        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            AppIcons.Apply();
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            // Accounts and cloud save talk to Supabase. Unity adds INTERNET on its own when it sees
            // UnityWebRequest, but a stripped build must not be left to that guess.
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android"));
            Directory.CreateDirectory(dir);
            string apk = Path.Combine(dir, $"MATUFarm-{PlayerSettings.bundleVersion}-{System.DateTime.Now:yyyyMMdd-HHmm}.apk");

            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });

            // The report can say Succeeded with no APK on disk: when the Android module is installed
            // while this Editor is running, the postprocess step throws "Build target 'Android' not
            // supported" after the data is written, and the summary never hears of it. The file is
            // the only honest answer. (Fix: restart the Editor so it loads the module.)
            var sum = report.summary;
            if (sum.result == BuildResult.Succeeded && File.Exists(apk))
                Debug.Log($"[BuildAndroid] OK — {apk} ({new FileInfo(apk).Length / (1024f * 1024f):0.0} MB, {sum.totalTime.TotalMinutes:0.0} phút)");
            else
            {
                string why = sum.result == BuildResult.Succeeded
                    ? "không có file APK (khởi động lại Editor nếu vừa cài module Android)"
                    : $"{sum.result}, {sum.totalErrors} lỗi";
                Debug.LogError($"[BuildAndroid] THẤT BẠI — {why}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
