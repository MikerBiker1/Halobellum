// LogReplay.cs
// --replay <battle log>: plays one of the owner's battle logs again, headless, on the exported game data (D145), and prints the AI's army
// at each battle start next to the one in the log, and each battle's outcome. The player's actions are done again as logged
// (MatchLogReplay.Apply); the AI decides anew, so this shows what a changed AI would have bought against the same player. With
// --old-insane the Insane plays as before D145. A log from before a balance change drifts; the drift is printed at the end.
// Usage: Tournament <game_data.json> <matrix.csv> <unused> --replay <log.jsonl> [--old-insane]

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using Match = Halobellum.Economy.Match;

static class LogReplay
{
    public static int Run(string path, List<FactionInfo> factions, MatchRules rules, CombatRules combat, MatchupTable table, bool oldInsane,
        Func<List<FactionInfo>, FactionInfo> skirmishField)
    {
        MatchLogReplay replay = MatchLogReplay.Read(File.ReadAllLines(path));
        replay.Setup.ApplyTo(rules);
        // The Match scene lists the factions UNSC, Covenant, Flood (the export sorts them by name); the draft's offer depends on that order.
        string[] sceneOrder = (Environment.GetEnvironmentVariable("HB_REPLAY_FACTIONS") ?? "UNSC,Covenant,Flood").Split(',');
        factions = factions.OrderBy(f => Array.IndexOf(sceneOrder, f.Name) < 0 ? 99 : Array.IndexOf(sceneOrder, f.Name)).ToList();
        IReadOnlyList<FactionInfo> offered = rules.Mode == MatchMode.UnlockSkirmish ? new[] { skirmishField(factions) } : factions;
        var ai = new RuleBasedAi(replay.AiDifficulty, replay.Seed, table) { ReadsUpgrades = !oldInsane };
        var match = new Match(rules, combat, offered, replay.Seed, ai);
        var hands = new MatchHands(match);
        Console.WriteLine($"{Path.GetFileName(path)}: seed {replay.Seed}, {replay.Mode}, {replay.Difficulty}, played on {replay.GameVersion}; Insane {(oldInsane ? "before D145" : "with D145")}");
        ActionResult drafted = hands.ChooseDraft(replay.DraftChosen);
        if (!drafted.Ok) Console.WriteLine($"  the starting card: {drafted.Message}");
        int failed = 0, playerWins = 0, aiWins = 0;
        foreach (ReplayLine line in replay.Lines)
        {
            if (match.Phase == MatchPhase.MatchOver) break;
            switch (line.Type)
            {
                case "action":
                    if (!MatchLogReplay.IsReplayed(line)) break;
                    if (!MatchLogReplay.Apply(hands, line).Ok) failed++;
                    break;
                case "battle_start":
                    if (match.Phase != MatchPhase.Deployment) break;
                    Console.WriteLine($"r{match.Round} AI now : {Army(match.Enemy)}");
                    Console.WriteLine($"   AI log : {LoggedArmy(line)}");
                    string farmed = string.Join(", ", match.Enemy.Roster.Where(u => ai.FarmedShare(u) > 0f).Select(u => $"{u.Name} {ai.FarmedShare(u):0.00}"));
                    Console.WriteLine($"   farmed : {farmed} | carries {string.Join(", ", ai.Carries.Select(u => u.Name))}");
                    if (Environment.GetEnvironmentVariable("HB_REPLAY_DECISION") == "1") Console.WriteLine($"   decided: {ai.LastDecision}");
                    Halobellum.Combat.BattleSimulation sim = match.StartBattle();
                    string watch = Environment.GetEnvironmentVariable("HB_REPLAY_WATCH");
                    if (watch == null) sim.Run();
                    else Watch(sim, watch);
                    break;
                case "battle_end":
                    if (match.Phase != MatchPhase.Battle) break;
                    RoundSummary summary = match.FinishBattle();
                    bool playerWon = summary.BaseDamageByA > summary.BaseDamageByB;
                    if (playerWon) playerWins++; else if (summary.BaseDamageByB > summary.BaseDamageByA) aiWins++;
                    Console.WriteLine($"   -> base damage: player {summary.BaseDamageByA}, AI {summary.BaseDamageByB} (bases {match.Player.BaseHealth} / {match.Enemy.BaseHealth})");
                    if (match.Phase == MatchPhase.Result) match.ContinueAfterResult();
                    break;
            }
        }
        Console.WriteLine($"Rounds: player {playerWins}, AI {aiWins}; winner {(match.Winner == null ? "none yet" : match.Winner == Team.A ? "player" : "AI")}; {failed} of the player's actions no longer worked (drift).");
        return 0;
    }

    /// <summary>
    /// HB_REPLAY_WATCH="Carrier Form": runs the battle tick by tick and reports how the player's bodies of that type end: bursting (firing),
    /// killed, or alive at the end; and any that stood within reach of an enemy for over a second without attacking (D146).
    /// </summary>
    private static void Watch(Halobellum.Combat.BattleSimulation sim, string typeName)
    {
        int fired = 0, killed = 0;
        var near = new Dictionary<Halobellum.Combat.SimUnit, float>();
        var lingered = new Dictionary<string, int>();
        var targets = new Dictionary<string, int>();
        sim.Events.WeaponFired += (u, w, t) => { if (u.Stats.Name == typeName && u.Team == Team.A) { fired++; targets.TryGetValue(t.Stats.Name, out int n); targets[t.Stats.Name] = n + 1; } };
        sim.Events.UnitDied += (u, k) => { if (u.Stats.Name == typeName && u.Team == Team.A && k != null) killed++; };
        while (!sim.IsFinished)
        {
            sim.Step();
            foreach (Halobellum.Combat.SimUnit u in sim.Units)
            {
                if (u.Stats.Name != typeName || u.Team != Team.A || !u.IsAlive) continue;
                Halobellum.Combat.SimUnit closest = null;
                float best = float.MaxValue;
                foreach (Halobellum.Combat.SimUnit e in sim.Units)
                {
                    if (e.Team == u.Team || !e.IsAlive || e.IsAir || e.NotArrived) continue;
                    float d = Halobellum.Combat.Targeting.EdgeDistance(u, e);
                    if (d < best) { best = d; closest = e; }
                }
                if (closest != null && best <= u.Weapons[u.MainWeaponIndex].Stats.Range)
                {
                    near.TryGetValue(u, out float t);
                    near[u] = t + sim.TickSeconds;
                    if (Math.Abs(near[u] - 1f) < sim.TickSeconds * 0.5f)
                    {
                        string key = $"{closest.Stats.Name} (state {u.State}, its target {(u.MoveTarget?.Stats.Name ?? "none")}, leaping {u.IsLeaping})";
                        lingered.TryGetValue(key, out int n); lingered[key] = n + 1;
                    }
                }
                else near.Remove(u);
            }
        }
        Console.WriteLine($"   watch {typeName}: {fired} attacks ({string.Join(", ", targets.Select(p => $"{p.Key} {p.Value}"))}), {killed} killed first");
        foreach (var pair in lingered) Console.WriteLine($"      in reach of {pair.Key} for 1 s without attacking: {pair.Value}");
    }

    private static string Army(PlayerState side)
    {
        var parts = side.Squads.GroupBy(s => $"{s.Type.Name} L{s.Level}").OrderBy(g => g.Key).Select(g => g.Count() > 1 ? $"{g.Count()}x {g.Key}" : g.Key);
        string techs = string.Join("; ", side.Techs.Where(p => p.Value.Count > 0).Select(p => $"{p.Key.Name}: {string.Join(", ", p.Value.Select(t => t.Name))}"));
        return string.Join(", ", parts) + (techs.Length > 0 ? $" | {techs}" : "");
    }

    private static string LoggedArmy(ReplayLine line)
    {
        if (!line.Json.TryGetValue("enemy", out object enemy) || !(enemy is Dictionary<string, object> side) || !side.TryGetValue("squads", out object squads)) return "?";
        var names = new List<string>();
        foreach (object squad in (System.Collections.IEnumerable)squads)
        {
            if (squad is Dictionary<string, object> s) names.Add($"{LogJson.Text(s, "unit")} L{LogJson.Int(s, "level")}");
        }
        return string.Join(", ", names.GroupBy(n => n).OrderBy(g => g.Key).Select(g => g.Count() > 1 ? $"{g.Count()}x {g.Key}" : g.Key));
    }
}
