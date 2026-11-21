// PlayerBuildTool.cs
// Builds a Windows player, used to check the game and measure real performance (M2 target:
// 300 vs 300 at 60 FPS) outside the editor.
//   Command line: -executeMethod Halobellum.EditorTools.PlayerBuildTool.BuildWindowsFromCommandLine
//                 -buildOutput C:\path\Halobellum.exe
// Run a -battleScene build with -benchmark (see BattleBenchmark), or a normal build with -autoplay
// (see MatchAutoplay). A normal build starts in the main menu; the test tools skip it (see GameFlow).
// With -testBuild its product name is "Halobellum Test". Unity keeps PlayerPrefs, and the window it remembers, under the product's name, so that nothing a run of that
// build does can reach the player's own settings and window. Only the build differs: the project's name goes back afterward.
// A normal build also writes the unit stats files (mod_json) into UnitStats next to the exe, over any there, with this version's values, so every
// version ships its own; a failure there only warns, as the game writes missing files itself at launch.
// Depends on: BattleSceneBuilder (scene path), UnitStatsSources, UnitStatsMods (folder name), Halobellum.Economy (UnitStatsFolder), UnityEditor.

using System;
using System.IO;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.Units;
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

                string product = PlayerSettings.productName;
                if (Array.IndexOf(args, "-testBuild") >= 0) PlayerSettings.productName = product + " Test";
                BuildReport report;
                try
                {
                    report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = Array.IndexOf(args, "-battleScene") >= 0
                            ? new[] { BattleSceneBuilder.ScenePath }
                            : BattleSceneBuilder.GameScenes,
                        locationPathName = args[index + 1],
                        target = BuildTarget.StandaloneWindows64,
                        options = BuildOptions.None,
                    });
                }
                finally
                {
                    PlayerSettings.productName = product;
                }
                Debug.Log($"Build {report.summary.result}: {report.summary.outputPath}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors.");
                if (report.summary.result == BuildResult.Succeeded)
                {
                    exitCode = 0;
                    if (Array.IndexOf(args, "-battleScene") < 0) WriteUnitStats(args[index + 1]);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Player build failed: " + e);
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Writes every unit stats file next to the built exe, over what is there. Returns how many it wrote (0 if it failed).</summary>
        public static int WriteUnitStats(string exePath)
        {
            try
            {
                var sources = AssetDatabase.LoadAssetAtPath<UnitStatsSources>(UnitStatsSources.AssetPath);
                if (sources == null) throw new InvalidOperationException("Missing " + UnitStatsSources.AssetPath);
                string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(exePath)) ?? ".", UnitStatsMods.FolderName);
                int written = UnitStatsFolder.WriteAll(folder, sources.DefaultGroups(), PlayerSettings.bundleVersion, overwrite: true);
                Debug.Log($"Unit stats written: {written} files to {folder}.");
                return written;
            }
            catch (Exception e)
            {
                Debug.LogWarning("The unit stats files were not written (the game writes missing ones at launch): " + e);
                return 0;
            }
        }
    }
}
