// PlayerBuildTool.cs
// Builds a Windows player, used to check the game and measure real performance (M2 target:
// 300 vs 300 at 60 FPS) outside the editor.
//   Command line: -executeMethod Halobellum.EditorTools.PlayerBuildTool.BuildWindowsFromCommandLine
//                 -buildOutput C:\path\Halobellum.exe
// Run a -battleScene build with -benchmark (see BattleBenchmark), or a normal build with -autoplay
// (see MatchAutoplay). A normal build starts in the main menu; the test tools skip it (see GameFlow).
// With -testBuild its product name is "Halobellum Test". Unity keeps PlayerPrefs, and the window it remembers, under the product's name, so that nothing a run of that
// build does can reach the player's own settings and window. Only the build differs: the project's name goes back afterward.
// The build's version is its build number (the owner, D101): how many commits main has, so it goes up with every change pushed. It comes from
// -buildNumber N (the build script passes it, as its copy of the project has no Git), or else from Git in the project's own folder; it is the
// version only for the build (Application.version, shown on the main menu), and the project's version setting goes back afterward.
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

                string product = PlayerSettings.productName, version = PlayerSettings.bundleVersion;
                if (Array.IndexOf(args, "-testBuild") >= 0) PlayerSettings.productName = product + " Test";
                string number = BuildNumber(args);
                if (number != null) PlayerSettings.bundleVersion = number;
                Debug.Log("Build number: " + (number ?? "none, version " + version));
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
                    PlayerSettings.bundleVersion = version;
                }
                Debug.Log($"Build {report.summary.result}: {report.summary.outputPath}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors.");
                if (report.summary.result == BuildResult.Succeeded) exitCode = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("Player build failed: " + e);
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>The build's number: -buildNumber N, or how many commits Git counts in the project's folder, or null if neither is there.</summary>
        public static string BuildNumber(string[] args)
        {
            int at = Array.IndexOf(args, "-buildNumber");
            if (at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out int given) && given > 0) return given.ToString();
            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("git", "rev-list --count HEAD")
                {
                    WorkingDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (var git = System.Diagnostics.Process.Start(start))
                {
                    string output = git.StandardOutput.ReadToEnd().Trim();
                    git.WaitForExit(10000);
                    return git.ExitCode == 0 && int.TryParse(output, out int count) && count > 0 ? count.ToString() : null;
                }
            }
            catch (Exception)
            {
                return null; // No Git here.
            }
        }
    }
}
