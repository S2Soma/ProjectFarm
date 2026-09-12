using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Fails the build when the state architecture regresses.
    ///
    /// Both rules below exist to keep the online-friends retrofit cheap. They are the kind of
    /// rule that decays within a sprint if it lives only in a document, and each is a single
    /// scan — so they are enforced here rather than written down and hoped for.</summary>
    public class ArchitectureGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        /// <summary>The only statics GS may expose. Anything else means mutable player state
        /// has crept back onto a static, which is precisely what PlayerState exists to prevent.</summary>
        static readonly HashSet<string> AllowedGsMembers = new HashSet<string>
        { "Local", "Viewing", "Now", "PlotCount", "Save", "Load", "Reset" };

        /// <summary>Directories whose code must reach state through a FarmContext, never through
        /// the local player — otherwise "water my friend's crop" cannot be expressed without
        /// duplicating the economy.</summary>
        static readonly string[] ContextOnlyDirs = { "Assets/Scripts/Farm" };

        static readonly Regex GsMember = new Regex(@"\bGS\.([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

        public void OnPreprocessBuild(BuildReport report)
        {
            var problems = Check();
            if (problems.Count == 0) return;
            throw new BuildFailedException(
                "Kiến trúc trạng thái bị vi phạm:\n  " + string.Join("\n  ", problems));
        }

        [MenuItem("Tools/LQ Farm/Kiểm tra kiến trúc")]
        static void Run()
        {
            var problems = Check();
            if (problems.Count == 0) Debug.Log("Kiến trúc OK — không vi phạm nào.");
            else foreach (var p in problems) Debug.LogError(p);
        }

        static List<string> Check()
        {
            var problems = new List<string>();
            if (!Directory.Exists("Assets/Scripts")) return problems;

            foreach (var path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                var rel = path.Replace('\\', '/');
                var lines = File.ReadAllLines(path);

                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Match m in GsMember.Matches(lines[i]))
                    {
                        string member = m.Groups[1].Value;

                        // Rule 1 — GS keeps a fixed, tiny static surface.
                        if (!AllowedGsMembers.Contains(member))
                            problems.Add($"{rel}:{i + 1}  GS.{member} — GS chỉ được có: " +
                                         string.Join(", ", AllowedGsMembers.OrderBy(x => x)) +
                                         ". Trạng thái người chơi thuộc về PlayerState.");

                        // Rule 2 — the world layer is context-routed.
                        if (member == "Local" && ContextOnlyDirs.Any(d => rel.StartsWith(d)))
                            problems.Add($"{rel}:{i + 1}  GS.Local — lớp thế giới phải đi qua " +
                                         "FarmContext (Ctx.owner / Ctx.actor), không đọc thẳng người chơi cục bộ.");
                    }
                }
            }
            return problems;
        }
    }
}
