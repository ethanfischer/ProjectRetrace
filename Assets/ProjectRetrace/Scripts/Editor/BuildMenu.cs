using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectRetrace.EditorTools
{
    /// <summary>The WebGL build as a menu item and a headless entry point, so a build
    /// can be made from a script with the editor closed.</summary>
    public static class BuildMenu
    {
        private const string OutputPath = "Builds/Web/ProjectRetrace";

        [MenuItem("ProjectRetrace/Build WebGL", false, 100)]
        public static void BuildWebGL()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(scenes, OutputPath, BuildTarget.WebGL, BuildOptions.None);
            var summary = report.summary;
            Debug.Log($"[ProjectRetrace] WebGL build {summary.result}: {summary.totalSize / 1048576} MB, {summary.totalErrors} errors, {summary.totalTime.TotalSeconds:0}s -> {OutputPath}");
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
