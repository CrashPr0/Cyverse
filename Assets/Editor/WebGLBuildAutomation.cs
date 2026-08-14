using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Cyverse.Editor
{
    /// <summary>Repeatable menu and command-line WebGL build entry point.</summary>
    public static class WebGLBuildAutomation
    {
        private const string OutputEnvironmentVariable = "CYVERSE_WEBGL_BUILD_PATH";

        [MenuItem("CyVerse/Build/Build WebGL")]
        public static void BuildFromMenu()
        {
            Build(false);
        }

        /// <summary>
        /// Command-line entry point. Override the destination with
        /// CYVERSE_WEBGL_BUILD_PATH; otherwise Build/WebGL/WebGL is used.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            Build(true);
        }

        private static void Build(bool commandLine)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes are configured in Build Settings.");

            string configuredPath = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
            string outputPath = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "WebGL", "WebGL")
                : Path.GetFullPath(configuredPath);

            Directory.CreateDirectory(outputPath);
            Debug.Log($"[WEBGL BUILD] Building {scenes.Length} scenes to {outputPath}");

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            BuildSummary summary = report.summary;
            Debug.Log($"[WEBGL BUILD] Result={summary.result}, size={summary.totalSize} bytes, " +
                      $"warnings={summary.totalWarnings}, errors={summary.totalErrors}, " +
                      $"duration={summary.totalTime}");

            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"WebGL build failed with result {summary.result}.");

            if (commandLine)
                EditorApplication.Exit(0);
        }
    }
}
