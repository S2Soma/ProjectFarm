using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>The web build, for GitHub Pages.
    ///
    ///     Menu:   Tools ▸ LQ Farm ▸ Build Web (GitHub Pages)
    ///     Deploy: Tools/deploy_web.sh   (pushes the output to the gh-pages branch)
    ///
    /// Output: Builds/WebGL/MATUFarm/ — index.html at the root, as Pages serves it.
    ///
    /// Two settings are what make it work on Pages rather than on a configured server:
    /// - Brotli with decompression fallback: Pages cannot send "Content-Encoding: br", so the loader
    ///   decompresses in JavaScript. Without the fallback the page fails with an encoding error.
    /// - The MATU template sets autoSyncPersistentDataPath, so the farm file is written through to
    ///   IndexedDB and survives a reload.</summary>
    public static class BuildWebGL
    {
        public const string OutputDir = "Builds/WebGL/MATUFarm";

        [MenuItem("Tools/LQ Farm/Build Web (GitHub Pages)")]
        public static void BuildFromMenu() { Build(); }

        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("[BuildWebGL] THẤT BẠI — chưa cài Web Build Support (hoặc cài xong mà chưa khởi động lại Editor).");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            PlayerSettings.productName = BuildAndroid.ProductName;
            PlayerSettings.WebGL.template = "PROJECT:MATU";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.runInBackground = true;
            EditorUserBuildSettings.development = false;

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDir));
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(Path.GetDirectoryName(dir));

            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = dir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            var sum = report.summary;
            string index = Path.Combine(dir, "index.html");
            if (sum.result == BuildResult.Succeeded && File.Exists(index))
            {
                // GitHub Pages runs Jekyll unless told not to; it would skip any path starting with "_".
                File.WriteAllText(Path.Combine(dir, ".nojekyll"), "");
                long total = 0, biggest = 0;
                string biggestName = "";
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    long len = new FileInfo(f).Length;
                    total += len;
                    if (len > biggest) { biggest = len; biggestName = Path.GetFileName(f); }
                }
                Debug.Log($"[BuildWebGL] OK — {dir} ({total / (1024f * 1024f):0.0} MB, lớn nhất {biggestName} {biggest / (1024f * 1024f):0.0} MB, {sum.totalTime.TotalMinutes:0.0} phút)");
                // Pages refuses files over 100 MB
                if (biggest > 95L * 1024 * 1024) Debug.LogError("[BuildWebGL] " + biggestName + " vượt ~100 MB, GitHub Pages sẽ từ chối.");
            }
            else
            {
                Debug.LogError($"[BuildWebGL] THẤT BẠI — {(File.Exists(index) ? sum.result.ToString() : "không có index.html")}, {sum.totalErrors} lỗi");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
