// UnitStatsFolder.cs
// The UnitStats folder next to a built game (mod_json): one JSON file per faction's base roster and per sub-faction, with every unit's stats.
// Load, at every launch of a built game (Presentation's UnitStatsMods):
//   1. a missing file is written from the defaults (never over a file that is there: it is opened CreateNew);
//   2. every file is read and checked (UnitStatOverrides.Read), and a unit may be given in one file only;
//   3. with changes and no mistakes, a dry run builds every unit with the changes (rebuild) and checks what comes out: the simulation's own
//      checks (UnitStats.Validate, on everything a unit brings too) and that a squad still fits the deployment zone. Problems the defaults already
//      have don't count. Each problem is reported once, at the file, line and change that caused it (the copies of a unit that its carriers bring
//      don't repeat it);
//   4. any mistake anywhere: the defaults for everything (State Errors). Otherwise the changes (State Modded), or the defaults if nothing changed.
// It never writes to a file that exists. WriteAll with overwrite is for the player build, which puts each version's own values in the folder.
// Plain C# without the engine, so the test harness drives it with hand-made units. Depends on: UnitStatOverrides, DeploymentGrid, Halobellum.Core.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public enum UnitStatsState { Defaults, Modded, Errors }

    /// <summary>What the launch made of the folder.</summary>
    public sealed class UnitStatsLoadResult
    {
        public UnitStatsState State = UnitStatsState.Defaults;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Notices = new List<string>();
        /// <summary>The changes in force (State Modded), or null.</summary>
        public UnitStatOverrides Overrides;
        public string Folder;
        public long Milliseconds;

        public int ChangeCount => Overrides?.ChangeCount ?? 0;

        /// <summary>What the battle log says (unit_stats): "modded:N", or null for the defaults.</summary>
        public string LogLabel => State == UnitStatsState.Modded ? "modded:" + ChangeCount : null;
    }

    public static class UnitStatsFolder
    {
        /// <summary>The most mistakes listed; past that, one line says how many more there are.</summary>
        public const int MaxErrors = 200;

        /// <summary>Writes every group's file. Without overwrite, a file that is there is left alone. Returns how many files were written.</summary>
        public static int WriteAll(string folder, IReadOnlyList<UnitStatGroup> groups, string gameVersion, bool overwrite, List<string> notices = null)
        {
            Directory.CreateDirectory(folder);
            int written = 0;
            foreach (UnitStatGroup group in groups)
            {
                string path = Path.Combine(folder, group.FileName);
                if (!overwrite && File.Exists(path)) continue;
                try
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(UnitStatOverrides.Write(group.Title, group.Units, gameVersion));
                    using (var stream = new FileStream(path, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }
                    written++;
                    notices?.Add($"{group.FileName} was missing and was written with this version's values.");
                }
                catch (Exception e) when (!overwrite)
                {
                    notices?.Add($"{group.FileName} could not be written ({e.Message}); the game uses its own values for it.");
                }
            }
            return written;
        }

        /// <summary>
        /// Reads the folder (see the file header). defaults are the units as the assets make them; rebuild makes them again with
        /// UnitStatOverrides.Active set, for the dry run. UnitStatOverrides.Active is the same afterwards as before; the caller sets it from the result.
        /// </summary>
        public static UnitStatsLoadResult Load(string folder, IReadOnlyList<UnitStatGroup> defaults, string gameVersion, CombatRules rules, Func<IReadOnlyList<UnitStatGroup>> rebuild)
        {
            var clock = Stopwatch.StartNew();
            var result = new UnitStatsLoadResult { Folder = folder };
            try
            {
                LoadInto(result, folder, defaults, gameVersion, rules, rebuild);
            }
            catch (Exception e)
            {
                result.Errors.Add($"The unit stats could not be read: {e.Message}");
            }
            if (result.Errors.Count > 0)
            {
                result.State = UnitStatsState.Errors;
                result.Overrides = null;
                if (result.Errors.Count > MaxErrors)
                {
                    int more = result.Errors.Count - MaxErrors;
                    result.Errors.RemoveRange(MaxErrors, more);
                    result.Errors.Add($"...and {more} more.");
                }
            }
            else if (result.Overrides != null && result.Overrides.ChangeCount > 0) result.State = UnitStatsState.Modded;
            else
            {
                result.State = UnitStatsState.Defaults;
                result.Overrides = null;
            }
            result.Milliseconds = clock.ElapsedMilliseconds;
            return result;
        }

        private static void LoadInto(UnitStatsLoadResult result, string folder, IReadOnlyList<UnitStatGroup> defaults, string gameVersion, CombatRules rules, Func<IReadOnlyList<UnitStatGroup>> rebuild)
        {
            var byName = new Dictionary<string, UnitStats>(StringComparer.Ordinal);
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (UnitStatGroup group in defaults)
            {
                expected.Add(group.FileName);
                foreach (UnitStats unit in group.Units)
                {
                    if (unit != null && !byName.ContainsKey(unit.Name)) byName[unit.Name] = unit;
                }
            }

            // 1. Missing files from the defaults; existing ones are never touched.
            var present = new List<UnitStatGroup>();
            foreach (UnitStatGroup group in defaults)
            {
                if (File.Exists(Path.Combine(folder, group.FileName))) present.Add(group);
            }
            if (present.Count < defaults.Count)
            {
                try
                {
                    WriteAll(folder, defaults, gameVersion, overwrite: false, result.Notices);
                }
                catch (Exception e)
                {
                    result.Notices.Add($"The folder {folder} could not be made ({e.Message}); the game uses its own values for the missing files.");
                }
            }
            if (Directory.Exists(folder))
            {
                string[] files = Directory.GetFiles(folder, "*.json");
                Array.Sort(files, StringComparer.Ordinal);
                foreach (string path in files)
                {
                    string file = Path.GetFileName(path);
                    if (!expected.Contains(file)) result.Notices.Add($"{file} is not one of this version's files, so it is not read.");
                }
            }

            // 2. Read and check every file that was already there.
            var overrides = new UnitStatOverrides();
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (UnitStatGroup group in present)
            {
                string text;
                try
                {
                    text = File.ReadAllText(Path.Combine(folder, group.FileName), Encoding.UTF8);
                }
                catch (Exception e)
                {
                    result.Errors.Add($"{group.FileName} could not be read: {e.Message}");
                    continue;
                }
                overrides.Read(group.FileName, text, byName, gameVersion, result.Errors, result.Notices, seen);
            }
            if (result.Errors.Count > 0 || overrides.ChangeCount == 0) return;

            // 3. The dry run: every unit made with the changes, checked as the game will use it.
            UnitStatOverrides before = UnitStatOverrides.Active;
            IReadOnlyList<UnitStatGroup> modded;
            try
            {
                UnitStatOverrides.Active = overrides;
                modded = rebuild();
            }
            finally
            {
                UnitStatOverrides.Active = before;
            }
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (Problem problem in Collect(defaults, rules)) known.Add(problem.Plain);
            List<Problem> found = Collect(modded, rules);

            // A unit that is also listed on its own is checked there: the copies a carrier brings (a drop, a spawn, a death spawn, a seed form)
            // would only repeat the same problem under the carrier's file. Each problem is reported once, at the change that caused it.
            var listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (Problem problem in found)
            {
                if (problem.Via.Length == 0) listed.Add(problem.Subject + "\n" + problem.Text);
            }
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (Problem problem in found)
            {
                if (known.Contains(problem.Plain)) continue;
                string key = problem.Subject + "\n" + problem.Text;
                if (problem.Via.Length > 0 && listed.Contains(key)) continue;
                if (!reported.Add(key)) continue;
                result.Errors.Add(Describe(problem, overrides));
            }
            if (result.Errors.Count == 0) result.Overrides = overrides;
        }

        /// <summary>One problem the checks found on a built unit.</summary>
        private sealed class Problem
        {
            /// <summary>The file of the listed unit it was found under.</summary>
            public string File;
            /// <summary>That listed unit.</summary>
            public string Carrier;
            /// <summary>The unit whose own values the check refuses: the carrier itself, or what it brings.</summary>
            public string Subject;
            /// <summary>"" for the listed unit itself, else how the carrier brings the subject: "Pelican reinforcement: ".</summary>
            public string Via;
            /// <summary>The check's message about the subject, for example "Marine weapon 0 (MA5): minimum range must be ...".</summary>
            public string Text;
            public bool Footprint;

            public string Plain => File + ": " + Via + Text;
        }

        /// <summary>
        /// The message for a problem the changes caused: the file and line of the change that caused it (the subject's own change that fits the
        /// message best, or else the carrier's), the change, and what the game's check says.
        /// </summary>
        private static string Describe(Problem problem, UnitStatOverrides overrides)
        {
            // A copy a carrier brings that still has a problem of its own (it is not the listed unit's problem repeated) comes from the carrier's
            // body counts, if the carrier changed them; otherwise from the subject's own changes.
            bool own = overrides.SitesOf(problem.Subject).Count > 0;
            if (problem.Via.Length > 0)
            {
                foreach (UnitStatOverrides.ChangeSite carried in overrides.SitesOf(problem.Carrier))
                {
                    if (carried.Part == "reinforcement" || carried.Part == "spawn" || carried.Part == "deathBurst") { own = false; break; }
                }
            }
            string unit = own ? problem.Subject : problem.Carrier;
            UnitStatOverrides.ChangeSite site = Locate(overrides.SitesOf(unit), unit, problem, out bool matched);
            if (site == null) return problem.Plain;
            string text = own ? problem.Text : problem.Via + problem.Text;
            if (own && matched)
            {
                // The change already names the unit, weapon or tech; keep only the check's reason.
                int colon = problem.Text.IndexOf(": ", StringComparison.Ordinal);
                if (colon >= 0) text = problem.Text.Substring(colon + 2);
            }
            return $"{site.File} line {site.Line}: {site.Change}: {text}";
        }

        private static readonly string[][] KeyHints =
        {
            new[] { "minimum range", "minRange", "range" },
            new[] { "range must", "range" },
            new[] { "fire interval", "fireInterval" },
            new[] { "projectile speed", "projectileSpeed", "splashRadius" },
            new[] { "splash radius", "splashRadius", "projectileSpeed" },
            new[] { "squad", "squadSize", "bodies" },
            new[] { "health", "maxHealth" },
            new[] { "shield", "maxShield" },
            new[] { "move speed", "moveSpeed" },
            new[] { "turn time", "turnTime180" },
            new[] { "cone", "coneDegrees", "coneDegreesBonus" },
            new[] { "extra shots", "extraShotsPerAttack", "burstGapSeconds" },
            new[] { "interval", "intervalSeconds" },
        };

        private static readonly string[][] PartHints =
        {
            new[] { " reinforcement", "reinforcement" }, new[] { "drop", "reinforcement" }, new[] { "landing", "reinforcement" },
            new[] { "spawn", "spawn" }, new[] { "leap", "leap" }, new[] { "aura", "aura" }, new[] { "defense", "defenseArc" }, new[] { "block chance", "defenseArc" },
        };

        /// <summary>The change that fits the problem best: in the weapon, tech or section it names, by the field it names; else the unit's first change.</summary>
        private static UnitStatOverrides.ChangeSite Locate(IReadOnlyList<UnitStatOverrides.ChangeSite> sites, string unit, Problem problem, out bool matched)
        {
            matched = false;
            if (sites.Count == 0) return null;
            string text = problem.Text;
            string part = null;
            if (problem.Footprint) part = "";
            else if (text.StartsWith(unit + " weapon ", StringComparison.Ordinal))
            {
                int start = unit.Length + " weapon ".Length, end = text.IndexOf(' ', start);
                if (end > start) part = "weapon:" + text.Substring(start, end - start);
            }
            else if (text.StartsWith(unit + " tech '", StringComparison.Ordinal))
            {
                int start = unit.Length + " tech '".Length, end = text.IndexOf("':", start, StringComparison.Ordinal);
                if (end > start) part = "tech:" + text.Substring(start, end - start);
            }
            else if (text.StartsWith(unit + ": ", StringComparison.Ordinal))
            {
                part = "";
                foreach (string[] hint in PartHints)
                {
                    if (text.IndexOf(hint[0], unit.Length, StringComparison.OrdinalIgnoreCase) >= 0) { part = hint[1]; break; }
                }
            }
            if (part == null) return sites[0];

            UnitStatOverrides.ChangeSite inPart = null;
            foreach (UnitStatOverrides.ChangeSite site in sites)
            {
                if (site.Part != part) continue;
                if (inPart == null) inPart = site;
                foreach (string[] hint in KeyHints)
                {
                    if (text.IndexOf(hint[0], StringComparison.OrdinalIgnoreCase) < 0) continue;
                    for (int k = 1; k < hint.Length; k++)
                    {
                        if (site.Key == hint[k]) { matched = true; return site; }
                    }
                }
            }
            if (inPart != null)
            {
                matched = true;
                return inPart;
            }
            return sites[0];
        }

        /// <summary>
        /// The simulation's own checks on every unit and on what it brings (Validate itself only looks into a drop), and that every unit bought from a
        /// roster still fits the deployment zone one way or the other. Each problem starts with the unit's file.
        /// </summary>
        public static List<string> Check(IReadOnlyList<UnitStatGroup> groups, CombatRules rules)
        {
            var problems = new List<string>();
            foreach (Problem problem in Collect(groups, rules)) problems.Add(problem.Plain);
            return problems;
        }

        private static List<Problem> Collect(IReadOnlyList<UnitStatGroup> groups, CombatRules rules)
        {
            var problems = new List<Problem>();
            foreach (UnitStatGroup group in groups)
            {
                foreach (UnitStats unit in group.Units)
                {
                    if (unit == null) continue;
                    CheckUnit(unit, group.FileName, unit.Name, "", problems, 0);
                    if (rules == null || !group.Buyable.Contains(unit.Name) || unit.SquadSize < 1) continue;
                    DeploymentGrid.Footprint(unit, false, rules, out int width, out int depth);
                    bool fits = (width <= rules.ArenaWidthSquares && depth <= rules.DeploymentDepthSquares) || (depth <= rules.ArenaWidthSquares && width <= rules.DeploymentDepthSquares);
                    if (!fits)
                        problems.Add(new Problem
                        {
                            File = group.FileName, Carrier = unit.Name, Subject = unit.Name, Via = "", Footprint = true,
                            Text = $"{unit.Name}: a squad of {unit.SquadSize} takes {width} x {depth} squares and no longer fits the {rules.ArenaWidthSquares} x {rules.DeploymentDepthSquares} deployment zone (lower 'squadSize').",
                        });
                }
            }
            return problems;
        }

        /// <summary>
        /// Validate on this unit, with what it drops split off (Validate puts a drop's problems under "NAME reinforcement: "), so each problem is
        /// kept against the unit whose values it is about; then the same for what it drops, spawns, releases and becomes.
        /// </summary>
        private static void CheckUnit(UnitStats unit, string file, string carrier, string via, List<Problem> problems, int depth)
        {
            List<string> own = unit.Validate();
            string label = string.IsNullOrEmpty(unit.Name) ? "(unnamed unit)" : unit.Name;
            UnitStats drop = unit.Reinforcement ?? unit.LockedReinforcement;
            if (drop != null)
            {
                foreach (string problem in drop.Validate()) own.Remove($"{label} reinforcement: {problem}");
            }
            foreach (string problem in own) problems.Add(new Problem { File = file, Carrier = carrier, Subject = unit.Name, Via = via, Text = problem });
            if (drop != null) CheckUnit(drop, file, carrier, via + label + " reinforcement: ", problems, depth);
            if (depth >= 3) return;
            string prefix = via + unit.Name;
            if (unit.SpawnedUnit != null) CheckUnit(unit.SpawnedUnit, file, carrier, prefix + " spawn: ", problems, depth + 1);
            if (unit.AlternateSpawnedUnit != null) CheckUnit(unit.AlternateSpawnedUnit, file, carrier, prefix + " alternate spawn: ", problems, depth + 1);
            if (unit.DeathBurst?.Spawns != null)
            {
                foreach (SpawnEntry spawn in unit.DeathBurst.Spawns)
                {
                    if (spawn?.Unit != null) CheckUnit(spawn.Unit, file, carrier, prefix + " death spawn: ", problems, depth + 1);
                }
            }
            if (unit.SeedForms != null)
            {
                foreach (SeedForm form in unit.SeedForms)
                {
                    if (form?.Becomes != null) CheckUnit(form.Becomes, file, carrier, prefix + " seed form: ", problems, depth + 1);
                }
            }
        }
    }
}
