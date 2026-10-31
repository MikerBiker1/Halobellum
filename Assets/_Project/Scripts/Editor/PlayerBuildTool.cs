// PlayerBuildTool.cs
// Builds a Windows player, used to check the game and measure real performance (M2 target:
// 300 vs 300 at 60 FPS) outside the editor.
//   Command line: -executeMethod Halobellum.EditorTools.PlayerBuildTool.BuildWindowsFromCommandLine
//                 -buildOutput C:\path\Halobellum.exe
// Run a -battleScene build with -benchmark (see BattleBenchmark), or a normal build with -autoplay
// (see MatchAutoplay).
// Depends on: BattleSceneBuilder (scene path), UnityEditor.

using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Halobellum.EditorTools
{
    public static class PlayerBuildTool
    {
        public static void BuildWindowsFromCommandLine()
        {
            int exitCode = 1;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-buildOutput");
                if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Pass -buildOutput <path to .exe>.");

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Array.IndexOf(args, "-battleScene") >= 0
                        ? new[] { BattleSceneBuilder.ScenePath }
                        : new[] { BattleSceneBuilder.MatchScenePath, BattleSceneBuilder.ScenePath },
                    locationPathName = args[index + 1],
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                Debug.Log($"Build {report.summary.result}: {report.summary.outputPath}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors.");
                if (report.summary.result == BuildResult.Succeeded) exitCode = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("Player build failed: " + e);
            }
            EditorApplication.Exit(exitCode);
        }
    }
}
