// UnitStatsModAssetTests.cs
// Tests the unit stats files (mod_json) against the real assets: the sources asset lists every faction, the files hold every roster, spell, release,
// drop, death-spawn and seed-form unit exactly once under unique names, the real files read back with no change and no mistake (the dry run's
// checks and the deployment footprint included), and a change in force reaches a unit everywhere ToStats makes it (the Pelican's dropped Marines).
// It also checks the menu's line about the files, and that a built game reads them but the editor and the test tools never do.
// Depends on: Halobellum.Units (UnitStatsSources), Halobellum.Presentation (UnitStatsMods), Halobellum.UI (MainMenuHud), Halobellum.Economy, NUnit, UnityEditor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.UI;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class UnitStatsModAssetTests
    {
        private const string Version = "0.0.0-test";

        [TearDown]
        public void TearDown() => UnitStatOverrides.Active = null;

        private static UnitStatsSources Sources()
        {
            var sources = AssetDatabase.LoadAssetAtPath<UnitStatsSources>(UnitStatsSources.AssetPath);
            Assert.IsNotNull(sources, UnitStatsSources.AssetPath);
            Assert.AreSame(sources, Resources.Load<UnitStatsSources>(UnitStatsSources.ResourcePath), "A built game finds it in Resources.");
            return sources;
        }

        private static UnitDefinition Unit(string path) => AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{path}.asset");

        [Test]
        public void TheSources_ListEveryFaction_AndTheCombatRules()
        {
            UnitStatsSources sources = Sources();
            foreach (string guid in AssetDatabase.FindAssets("t:FactionDefinition", new[] { "Assets/_Project/Data/Factions" }))
                CollectionAssert.Contains(sources.factions, AssetDatabase.LoadAssetAtPath<FactionDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
            Assert.IsNotNull(sources.combatRules);
        }

        [Test]
        public void TheFiles_HoldEveryUnitOnce_UnderUniqueNames()
        {
            List<UnitStatGroup> groups = Sources().DefaultGroups();
            CollectionAssert.AreEqual(new[]
            {
                "UNSC.json", "UNSC_Noble.json", "UNSC_ArmoredDivision.json", "UNSC_AerospaceCommand.json",
                "Covenant.json", "Covenant_Banished.json", "Covenant_LekgoloColony.json", "Covenant_UnggoyRebellion.json",
                "Flood.json", "Flood_UNSCbased.json", "Flood_Covenantbased.json",
            }, groups.Select(g => g.FileName).ToArray());

            var names = groups.SelectMany(g => g.Units).Select(u => u.Name).ToList();
            CollectionAssert.AllItemsAreUnique(names);

            // Every unit a match can meet is in a file: rosters, spell drops, and what they bring.
            var expected = new HashSet<string>();
            void Walk(UnitStats unit, int depth)
            {
                if (unit == null || depth > 3) return;
                expected.Add(unit.Name);
                Walk(unit.SpawnedUnit, depth + 1);
                Walk(unit.AlternateSpawnedUnit, depth + 1);
                Walk(unit.Reinforcement, depth + 1);
                Walk(unit.LockedReinforcement, depth + 1);
                if (unit.DeathBurst?.Spawns != null) foreach (SpawnEntry spawn in unit.DeathBurst.Spawns) Walk(spawn?.Unit, depth + 1);
                if (unit.SeedForms != null) foreach (SeedForm form in unit.SeedForms) Walk(form?.Becomes, depth + 1);
            }
            var cache = new Dictionary<UnitDefinition, UnitStats>();
            foreach (FactionDefinition faction in Sources().factions)
            {
                FactionInfo info = faction.ToInfo(cache);
                foreach (UnitStats unit in info.BaseRoster) Walk(unit, 0);
                foreach (SubFactionInfo sub in info.SubFactions) foreach (UnitStats unit in sub.ExclusiveUnits) Walk(unit, 0);
                foreach (CardStats card in info.Cards) Walk(card.Spell?.DropUnit, 0);
            }
            CollectionAssert.IsSubsetOf(expected, names);
            Assert.Contains(Unit("SpellUnits/ODST").ToStats().Name, names, "The ODST drop pods' unit.");
            Assert.IsTrue(groups[0].Buyable.Contains("Marine"));
        }

        [Test]
        public void TheRealFiles_ReadBackWithNoChange_AndPassEveryCheck()
        {
            UnitStatsSources sources = Sources();
            List<UnitStatGroup> groups = sources.DefaultGroups();
            CollectionAssert.IsEmpty(UnitStatsFolder.Check(groups, sources.combatRules.ToRules()), "The assets themselves pass the dry run's checks.");

            var byName = groups.SelectMany(g => g.Units).ToDictionary(u => u.Name);
            var errors = new List<string>();
            var notices = new List<string>();
            var overrides = new UnitStatOverrides();
            var seen = new Dictionary<string, string>();
            foreach (UnitStatGroup group in groups)
                overrides.Read(group.FileName, UnitStatOverrides.Write(group.Title, group.Units, Version), byName, Version, errors, notices, seen);
            CollectionAssert.IsEmpty(errors);
            CollectionAssert.IsEmpty(notices);
            Assert.AreEqual(0, overrides.ChangeCount, "An unedited file changes nothing: " + string.Join(" | ", overrides.Changes));
        }

        [Test]
        public void AChangeInForce_ReachesTheUnitEverywhereItIsMade()
        {
            UnitStatsSources sources = Sources();
            string folder = Path.Combine(Path.GetTempPath(), "hb_unitstats_" + Guid.NewGuid().ToString("N"));
            try
            {
                List<UnitStatGroup> defaults = sources.DefaultGroups();
                UnitStatsFolder.WriteAll(folder, defaults, Version, overwrite: true);
                string unsc = Path.Combine(folder, "UNSC.json");
                float health = Unit("UNSC/Marine").maxHealth;
                string marineHealth = $"\"maxHealth\": {StatJson.Number(health)},";
                string text = File.ReadAllText(unsc);
                int at = text.IndexOf(marineHealth, text.IndexOf("\"name\": \"Marine\"", StringComparison.Ordinal), StringComparison.Ordinal);
                Assert.Greater(at, 0);
                File.WriteAllText(unsc, text.Substring(0, at) + "\"maxHealth\": 99," + text.Substring(at + marineHealth.Length));

                UnitStatsLoadResult result = UnitStatsFolder.Load(folder, defaults, Version, sources.combatRules.ToRules(), sources.BuildGroups);
                Assert.AreEqual(UnitStatsState.Modded, result.State, string.Join(" | ", result.Errors));
                Assert.AreEqual(1, result.ChangeCount);

                UnitStatOverrides.Active = result.Overrides;
                Assert.AreEqual(99f, Unit("UNSC/Marine").ToStats().MaxHealth);
                UnitStats pelican = Unit("UNSC/Pelican").ToStats();
                Assert.AreEqual(99f, pelican.LockedReinforcement.MaxHealth, "The Pelican's dropped Marines have the change.");
                Assert.AreEqual(Unit("UNSC/Pelican").reinforcementBodies, pelican.LockedReinforcement.SquadSize, "The Pelican's own drop size stands.");
                var cache = new Dictionary<UnitDefinition, UnitStats>();
                FactionInfo info = sources.factions[0].ToInfo(cache);
                Assert.AreEqual(99f, info.BaseRoster.First(u => u.Name == "Marine").MaxHealth, "A match's roster has it, and so its stats windows.");

                UnitStatOverrides.Active = null;
                Assert.AreEqual(health, Unit("UNSC/Marine").ToStats().MaxHealth, "Without overrides, the asset.");
            }
            finally
            {
                UnitStatOverrides.Active = null;
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void OnlyABuiltGame_ReadsTheFiles()
        {
            Assert.IsNull(UnitStatsMods.FolderFor(true, new[] { "Halobellum.exe" }, "C:/Game/Halobellum_Data"), "Never in the editor.");
            Assert.IsNull(UnitStatsMods.FolderFor(false, new[] { "Halobellum.exe", GameFlow.AutoplayFlag }, "C:/Game/Halobellum_Data"), "Never in a test tool's run.");
            Assert.AreEqual(Path.Combine(Path.GetDirectoryName("C:/Game/Halobellum_Data"), "UnitStats"), UnitStatsMods.FolderFor(false, new[] { "Halobellum.exe" }, "C:/Game/Halobellum_Data"));
            Assert.AreEqual("D:/mine", UnitStatsMods.FolderFor(false, new[] { "Halobellum.exe", GameFlow.MenuDemoFlag, UnitStatsMods.FolderFlag, "D:/mine" }, "C:/Game/Halobellum_Data"));
            Assert.IsNull(UnitStatOverrides.Active, "The editor plays the assets.");
        }

        [Test]
        public void TheMenu_SaysWhenTheStatsAreModdedOrWrong()
        {
            Assert.IsNull(MainMenuHud.StatsLine(UnitStatsState.Defaults, 0));
            Assert.AreEqual("Unit stats: modded (3 changes)", MainMenuHud.StatsLine(UnitStatsState.Modded, 3));
            Assert.AreEqual("Unit stats: modded (1 change)", MainMenuHud.StatsLine(UnitStatsState.Modded, 1));
            Assert.AreEqual("Unit stats: defaults (files have mistakes)", MainMenuHud.StatsLine(UnitStatsState.Errors, 0));
            string text = MainMenuHud.StatsErrorsText(Enumerable.Range(1, 10).Select(i => "mistake " + i).ToList());
            StringAssert.Contains("mistake 8", text);
            StringAssert.DoesNotContain("mistake 9", text);
            StringAssert.Contains("...and 2 more (see Player.log)", text);
            Assert.IsFalse(MainMenuHud.StatsErrorsPending, "No panel in the editor.");
        }
    }
}
