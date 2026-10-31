// PlayerBuildTool.cs
// Builds a Windows player of the battle scene, used to measure real game performance (M2 target:
// 300 vs 300 at 60 FPS) outside the editor.
//   Command line: -executeMethod Halobellum.EditorTools.PlayerBuildTool.BuildWindowsFromCommandLine
//                 -buildOutput C:\path\Halobellum.exe
// Run the result with -benchmark (see BattleBenchmark) to get a frame-time report.
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
                    scenes = new[] { BattleSceneBuilder.ScenePath },
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
