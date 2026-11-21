// UnitStatsMods.cs
// The unit stats files a player may edit (mod_json), read at every launch of a built game by the Boot scene (BootLoader), before the main menu: the UnitStats folder next to
// Halobellum.exe (or the folder given with -unitStats <folder>). A missing file is written from the defaults; a file that is there is never
// written to. With every file right, the changed values are put in force (UnitStatOverrides.Active), so every unit made from then on has them.
// With any mistake, the game uses its own values for every unit and the main menu says what is wrong (MainMenuHud). Every mistake and notice also
// goes to Player.log.
// Never in the editor, and never in a run of a test tool (GameFlow.IsTestRun) unless it is given -unitStats: development, tests, the tournament
// and replays always play the assets. The battle log says when a game was played with changed stats (LogLabel, unit_stats).
// Depends on: UnitStatsSources, Halobellum.Economy (UnitStatsFolder), Halobellum.Core (UnitStatOverrides), GameFlow, UnityEngine.

using System;
using System.IO;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Units;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class UnitStatsMods
    {
        public const string FolderFlag = "-unitStats";
        /// <summary>The folder's name next to the game's exe.</summary>
        public const string FolderName = "UnitStats";

        /// <summary>What this launch made of the files. Defaults until a built game has read them.</summary>
        public static UnitStatsLoadResult Status { get; private set; } = new UnitStatsLoadResult();

        /// <summary>For the battle log: "modded:N" while changed stats are in force, otherwise null.</summary>
        public static string LogLabel => Status?.LogLabel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            UnitStatOverrides.Active = null;
            Status = new UnitStatsLoadResult();
        }

        /// <summary>The Boot scene's call: reads the folder for this launch, if any, from the sources it holds.</summary>
        public static void LoadAtLaunch(UnitStatsSources sources)
        {
            string folder = FolderFor(Application.isEditor, Environment.GetCommandLineArgs(), Application.dataPath);
            if (folder != null) Load(folder, sources);
        }

        /// <summary>The folder to read, or null to use the assets: never in the editor; in a test tool's run only with -unitStats.</summary>
        public static string FolderFor(bool editor, string[] args, string dataPath)
        {
            if (editor) return null;
            int at = Array.IndexOf(args, FolderFlag);
            if (at >= 0 && at + 1 < args.Length) return args[at + 1];
            if (GameFlow.IsTestRun(args)) return null;
            return Path.Combine(Path.GetDirectoryName(dataPath) ?? ".", FolderName);
        }

        /// <summary>Reads the folder, puts the result in force and remembers it for the menu.</summary>
        public static UnitStatsLoadResult Load(string folder, UnitStatsSources sources)
        {
            UnitStatsLoadResult result;
            if (sources == null)
            {
                result = new UnitStatsLoadResult { Folder = folder };
                result.Notices.Add("The Boot scene holds no unit stats sources, so the unit stats files are not read.");
            }
            else
            {
                try
                {
                    CombatRules rules = sources.combatRules != null ? sources.combatRules.ToRules() : null;
                    result = UnitStatsFolder.Load(folder, sources.DefaultGroups(), Application.version, rules, sources.BuildGroups);
                }
                catch (Exception e)
                {
                    result = new UnitStatsLoadResult { Folder = folder, State = UnitStatsState.Errors };
                    result.Errors.Add("The unit stats could not be read: " + e.Message);
                }
            }
            UnitStatOverrides.Active = result.State == UnitStatsState.Modded ? result.Overrides : null;
            Status = result;
            Report(result);
            return result;
        }

        private static void Report(UnitStatsLoadResult result)
        {
            foreach (string error in result.Errors) Debug.LogWarning("Unit stats: " + error);
            foreach (string notice in result.Notices) Debug.Log("Unit stats: " + notice);
            if (result.Overrides != null)
            {
                foreach (string change in result.Overrides.Changes) Debug.Log("Unit stats changed: " + change);
            }
            string state = result.State == UnitStatsState.Modded ? $"modded ({result.ChangeCount} changes)"
                : result.State == UnitStatsState.Errors ? $"defaults ({result.Errors.Count} mistakes in the files)" : "defaults";
            Debug.Log($"Unit stats: {state}, from {result.Folder}, in {result.Milliseconds} ms.");
        }
    }
}
