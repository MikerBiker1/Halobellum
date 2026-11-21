// Duel.cs
// --duel "A:n,B:m": a diagnostic battle on the exported game data (D146). n bodies of unit A (team A, south) against m of unit B (team B,
// north), 30 m apart, every body watched: each second it prints, for each of A's living bodies, its state, its target and its edge distance
// to it, so that a unit that never attacks shows why. Ends when one side is gone or after 60 s.
// Usage: Tournament <game_data.json> <matrix.csv> <unused> --duel "Carrier Form:4,Scarab:1"

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;

static class Duel
{
    public static int Run(string spec, List<FactionInfo> factions, CombatRules combat)
    {
        var all = new Dictionary<string, UnitStats>();
        foreach (FactionInfo faction in factions)
        {
            foreach (UnitStats unit in faction.BaseRoster) all[unit.Name] = unit;
            foreach (SubFactionInfo sub in faction.SubFactions) foreach (UnitStats unit in sub.ExclusiveUnits) all[unit.Name] = unit;
        }
        string[] sides = spec.Split(',');
        (UnitStats type, int count) Side(string s)
        {
            string[] parts = s.Split(':');
            if (!all.TryGetValue(parts[0], out UnitStats type)) throw new ArgumentException($"No unit named {parts[0]}.");
            return (type, parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 1);
        }
        // Side A may mix types with '+' ("Infection Forms:10+Carrier Form:4"); the first type stands in front. Only the last type is watched.
        string[] mix = sides[0].Split('+');
        var (a, na) = Side(mix[mix.Length - 1]);
        var (b, nb) = Side(sides[1]);
        var sim = new BattleSimulation(combat, 1);
        var ours = new List<SimUnit>();
        for (int m = 0; m < mix.Length; m++)
        {
            var (type, count) = Side(mix[m]);
            for (int i = 0; i < count; i++)
            {
                SimUnit body = sim.AddUnit(type, Team.A, new Vec2((i - (count - 1) * 0.5f) * 2f, -12f - 3f * m));
                if (m == mix.Length - 1) ours.Add(body);
            }
        }
        for (int i = 0; i < nb; i++) sim.AddUnit(b, Team.B, new Vec2((i - (nb - 1) * 0.5f) * 6f, 15f));
        int fired = 0, died = 0;
        float dealtByBurst = 0f;
        sim.Events.WeaponFired += (u, w, t) => { if (u.Team == Team.A && u.Stats == a) { fired++; Console.WriteLine($"      {u.Stats.Name} #{u.Id} fires at {t.Stats.Name}, edge {Targeting.EdgeDistance(u, t):0.00} m"); } };
        sim.Events.UnitDied += (u, killer) => { if (u.Team == Team.A && u.Stats == a) { died++; Console.WriteLine($"      {u.Stats.Name} #{u.Id} dies{(killer != null ? " to " + killer.Stats.Name : "")}"); } };
        sim.Events.UnitDamaged += (victim, by, amount) => { if (victim.Team == Team.B && by != null && by.Stats == a) { dealtByBurst += amount; Console.WriteLine($"      burst hits {victim.Stats.Name} for {amount:0} ({victim.Health:0} left)"); } };
        Console.WriteLine($"{na} x {a.Name} (radius {a.CollisionRadius}, main range {a.Weapons[0].Range}) vs {nb} x {b.Name} (radius {b.CollisionRadius}, air {b.IsAir})");
        int perSecond = (int)Math.Round(1f / sim.TickSeconds);
        for (int tick = 0; tick < 60 * perSecond && !sim.IsFinished; tick++)
        {
            sim.Step();
            if (tick % perSecond != perSecond - 1) continue;
            var lines = ours.Where(u => u.IsAlive).Select(u =>
            {
                SimUnit t = u.Weapons[u.MainWeaponIndex].Target ?? u.MoveTarget;
                string to = t == null ? "no target" : $"{t.Stats.Name} edge {Targeting.EdgeDistance(u, t):0.00} m";
                return $"{u.State}{(u.IsLeaping ? " leaping" : "")} -> {to}";
            });
            Console.WriteLine($"{(tick + 1) / perSecond,3} s: {string.Join(" | ", lines)}");
        }
        Console.WriteLine($"Left: {ours.Count(u => u.IsAlive)} of {na} {a.Name}; {fired} attacks, {died} deaths, {dealtByBurst:0} damage from bodies already dead (bursts); finished {sim.IsFinished}");
        return 0;
    }
}
