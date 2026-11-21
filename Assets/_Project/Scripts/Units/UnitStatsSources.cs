// UnitStatsSources.cs
// Where the unit stats files come from (mod_json): the three factions and the combat rules, in Resources/UnitStatsSources.asset, so a built game
// can find every unit at launch before any scene has loaded. It makes one file per faction's base roster and one per sub-faction
// (UNSC.json, UNSC_Noble.json, ... Flood_Covenantbased.json: the names with everything but letters and digits taken out). A roster unit goes in
// its roster's file. A unit only reached through a spell's drop, a release, a death spawn, a drop or a seed form (the ODST, the Infection Forms)
// goes in the base file of the faction that reaches it first. Every unit is written once, from its own asset (ToStats at the top level).
// Two different unit assets with one name would both take the same changes, so that throws.
// Depends on: FactionDefinition, UnitDefinition, SpellDefinition, CombatRulesDefinition, Halobellum.Core (UnitStatGroup, UnitStatOverrides), UnityEngine.

using System;
using System.Collections.Generic;
using System.Text;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Units
{
    [CreateAssetMenu(menuName = "Halobellum/Unit Stats Sources", fileName = "UnitStatsSources")]
    public sealed class UnitStatsSources : ScriptableObject
    {
        /// <summary>Its place under Resources, for Resources.Load.</summary>
        public const string ResourcePath = "UnitStatsSources";
        public const string AssetPath = "Assets/_Project/Resources/UnitStatsSources.asset";

        public List<FactionDefinition> factions = new List<FactionDefinition>();
        [Tooltip("For the check that a modded squad still fits the deployment zone.")]
        public CombatRulesDefinition combatRules;

        /// <summary>The files as the assets make them, whatever overrides are in force.</summary>
        public List<UnitStatGroup> DefaultGroups()
        {
            UnitStatOverrides before = UnitStatOverrides.Active;
            UnitStatOverrides.Active = null;
            try
            {
                return BuildGroups();
            }
            finally
            {
                UnitStatOverrides.Active = before;
            }
        }

        /// <summary>The files' units made now, with UnitStatOverrides.Active as it is (the dry run).</summary>
        public List<UnitStatGroup> BuildGroups()
        {
            var groups = new List<UnitStatGroup>();
            var placed = new Dictionary<UnitDefinition, UnitStatGroup>();
            var byName = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
            var baseGroups = new List<UnitStatGroup>();

            foreach (FactionDefinition faction in factions)
            {
                if (faction == null) continue;
                string factionName = string.IsNullOrEmpty(faction.factionName) ? faction.name : faction.factionName;
                var baseGroup = new UnitStatGroup { FileName = FileNameFor(factionName, null), Title = factionName + " base roster" };
                groups.Add(baseGroup);
                baseGroups.Add(baseGroup);
                foreach (UnitDefinition unit in faction.baseRoster) Place(unit, baseGroup, true, placed, byName);
                foreach (FactionDefinition.SubFactionEntry sub in faction.subFactions)
                {
                    if (sub == null) continue;
                    var group = new UnitStatGroup { FileName = FileNameFor(factionName, sub.subFactionName), Title = $"{factionName}, {sub.subFactionName} exclusives" };
                    groups.Add(group);
                    foreach (UnitDefinition unit in sub.exclusiveUnits) Place(unit, group, true, placed, byName);
                }
            }

            // What the rosters and spells bring, once every roster unit has its own file.
            int index = 0;
            foreach (FactionDefinition faction in factions)
            {
                if (faction == null) continue;
                UnitStatGroup baseGroup = baseGroups[index++];
                var queue = new List<UnitDefinition>();
                queue.AddRange(faction.baseRoster);
                foreach (FactionDefinition.SubFactionEntry sub in faction.subFactions)
                {
                    if (sub != null) queue.AddRange(sub.exclusiveUnits);
                }
                foreach (SpellDefinition spell in faction.spells)
                {
                    if (spell == null || spell.dropUnit == null) continue;
                    Place(spell.dropUnit, baseGroup, false, placed, byName);
                    queue.Add(spell.dropUnit);
                }
                var walked = new HashSet<UnitDefinition>();
                for (int i = 0; i < queue.Count; i++)
                {
                    UnitDefinition unit = queue[i];
                    if (unit == null || !walked.Add(unit)) continue;
                    foreach (UnitDefinition brought in Brings(unit))
                    {
                        Place(brought, baseGroup, false, placed, byName);
                        queue.Add(brought);
                    }
                }
            }
            return groups;
        }

        /// <summary>The units this one brings into a battle.</summary>
        private static IEnumerable<UnitDefinition> Brings(UnitDefinition unit)
        {
            if (unit.spawnedUnit != null) yield return unit.spawnedUnit;
            if (unit.alternateSpawnedUnit != null) yield return unit.alternateSpawnedUnit;
            if (unit.deathSpawns != null)
            {
                foreach (SpawnDefinition spawn in unit.deathSpawns)
                {
                    if (spawn?.unit != null) yield return spawn.unit;
                }
            }
            if (unit.reinforcementUnit != null) yield return unit.reinforcementUnit;
            if (unit.seedForms != null)
            {
                foreach (SeedFormDefinition form in unit.seedForms)
                {
                    if (form?.becomes != null) yield return form.becomes;
                }
            }
        }

        private static void Place(UnitDefinition unit, UnitStatGroup group, bool buyable, Dictionary<UnitDefinition, UnitStatGroup> placed, Dictionary<string, UnitDefinition> byName)
        {
            if (unit == null) return;
            if (placed.TryGetValue(unit, out UnitStatGroup already))
            {
                if (!buyable) return;
                foreach (var pair in byName)
                {
                    if (pair.Value == unit) already.Buyable.Add(pair.Key); // order doesn't matter: a unit asset has one name
                }
                return;
            }
            UnitStats stats = unit.ToStats();
            if (byName.TryGetValue(stats.Name, out UnitDefinition other) && other != unit)
                throw new InvalidOperationException($"Two unit assets are both called '{stats.Name}' ({other.name} and {unit.name}); unit names must be different for the unit stats files.");
            byName[stats.Name] = unit;
            placed[unit] = group;
            group.Units.Add(stats);
            if (buyable) group.Buyable.Add(stats.Name);
        }

        /// <summary>A file's name: the faction and the sub-faction, with everything but letters and digits taken out ("Flood_UNSCbased.json").</summary>
        public static string FileNameFor(string faction, string subFaction)
        {
            string name = Clean(faction);
            if (!string.IsNullOrEmpty(subFaction)) name += "_" + Clean(subFaction);
            return name + ".json";
        }

        private static string Clean(string text)
        {
            var clean = new StringBuilder();
            foreach (char c in text ?? "")
            {
                if (c < 128 && char.IsLetterOrDigit(c)) clean.Append(c);
            }
            return clean.Length > 0 ? clean.ToString() : "Units";
        }
    }
}
