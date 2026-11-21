// UnitStatOverridesTests.cs
// Tests the unit stats files a player may edit next to the built game (mod_json), without Unity: the strict JSON reader (lines, duplicate keys,
// comments), the checks on a file (unknown units and fields, wrong types, whole numbers, enum names, the weapon and tech lists, fields a unit
// doesn't have, limits), that an unedited file changes nothing, that a change reaches a unit and what it brings (the parent's body counts win),
// and the folder at launch: missing files written, existing ones never touched, any mistake means the defaults, and the dry run's checks.
// The units are made by hand, the way UnitDefinition.ToStats makes them (each copy gets its own unit's changes, then the parent's body count).
// Depends on: Halobellum.Core (StatJson, UnitStatOverrides), Halobellum.Economy (UnitStatsFolder), NUnit, TestRules.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class UnitStatOverridesTests
    {
        private const string Version = "0.399.0";

        private string _folder;

        [SetUp]
        public void SetUp() => _folder = Path.Combine(Path.GetTempPath(), "hb_unitstats_" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            UnitStatOverrides.Active = null;
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        // ------------------------------------------------------------------ units made like UnitDefinition.ToStats

        private static UnitStats MarineBase() => new UnitStats
        {
            Name = "Marine", Role = UnitRole.Chaff, Tier = 1, Cost = 100, SquadSize = 18, BaseDamage = 1.1111f, MaxHealth = 50f, MoveSpeed = 1.5f,
            CollisionRadius = 0.4f, TurnTime180 = 0.25f, TargetFilter = TargetFilter.Both,
            Weapons = new[] { new WeaponStats { Name = "MA5 Assault Rifle", DamagePerShot = 4f, FireInterval = 0.5f, Range = 12f, CanBeBlocked = true } },
            TechUpgrades = new[] { new TechUpgrade { Name = "Body Armor", Cost = 100, HealthMultiplier = 1.3f }, new TechUpgrade { Name = "Long Range", Cost = 300, RangeBonus = 8f } },
        };

        private static UnitStats Marine()
        {
            UnitStats marine = MarineBase();
            UnitStatOverrides.Active?.Apply(marine);
            return marine;
        }

        /// <summary>The Pelican and its locked drop of 6 Marines; noDrop leaves the drop out, as the depth guard does.</summary>
        private static UnitStats Pelican(bool noDrop = false)
        {
            var pelican = new UnitStats
            {
                Name = "Pelican", Tier = 2, Cost = 400, SquadSize = 1, MaxHealth = 800f, MoveSpeed = 5f, CollisionRadius = 1.5f, IsAir = true, TargetFilter = TargetFilter.Both,
                Weapons = new[] { new WeaponStats { Name = "Chin Gun", DamagePerShot = 10f, FireInterval = 0.25f, Range = 15f } },
                ReinforcementDelaySeconds = 10f, ReinforcementDrops = 1, ReinforcementLandSeconds = 2f,
                TechUpgrades = new[] { new TechUpgrade { Name = "Troop Bay", Cost = 150, UnlocksReinforcement = true } },
            };
            if (!noDrop)
            {
                UnitStats drop = Marine(); // the drop's own ToStats, with the Marine's changes...
                drop.SquadSize = 6;        // ...then the parent's body count
                pelican.LockedReinforcement = drop;
            }
            UnitStatOverrides.Active?.Apply(pelican); // the parent's own changes last
            return pelican;
        }

        private static UnitStats Jackal()
        {
            var jackal = new UnitStats
            {
                Name = "Jackal", Tier = 1, Cost = 150, SquadSize = 8, MaxHealth = 60f, MoveSpeed = 2f, CollisionRadius = 0.4f, TargetFilter = TargetFilter.Both,
                Weapons = new[] { new WeaponStats { Name = "Plasma Pistol", DamagePerShot = 6f, FireInterval = 0.6f, Range = 10f, DamageType = DamageType.Energy, CanBeBlocked = true } },
                DefenseArc = new DefenseArcStats { ArcDegrees = 120f, FrontDamageMultiplier = 0.5f, RearDamageMultiplier = 1f, BlockChance = 0.5f, BlockedDamageTypes = new[] { DamageType.Ballistic } },
                TechUpgrades = new TechUpgrade[0],
            };
            UnitStatOverrides.Active?.Apply(jackal);
            return jackal;
        }

        private static UnitStats Scarab()
        {
            var scarab = new UnitStats
            {
                Name = "Scarab", Tier = 4, Cost = 800, SquadSize = 1, MaxHealth = 9000f, MoveSpeed = 1f, CollisionRadius = 6f, TargetFilter = TargetFilter.Both,
                Weapons = new[] { new WeaponStats { Name = "Focus Cannon", DamagePerShot = 50f, FireInterval = 0.2f, Range = 30f, SplashRadius = 3f, BeamSeconds = 3f, BeamChargeSeconds = 1f } },
                TechUpgrades = new TechUpgrade[0],
            };
            UnitStatOverrides.Active?.Apply(scarab);
            return scarab;
        }

        private static List<UnitStatGroup> Groups()
        {
            var unsc = new UnitStatGroup { FileName = "UNSC.json", Title = "UNSC base roster", Units = { Marine(), Pelican() } };
            var covenant = new UnitStatGroup { FileName = "Covenant.json", Title = "Covenant base roster", Units = { Jackal(), Scarab() } };
            foreach (UnitStatGroup group in new[] { unsc, covenant })
            {
                foreach (UnitStats unit in group.Units) group.Buyable.Add(unit.Name);
            }
            return new List<UnitStatGroup> { unsc, covenant };
        }

        private static Dictionary<string, UnitStats> Defaults()
        {
            var byName = new Dictionary<string, UnitStats>();
            foreach (UnitStatGroup group in Groups())
            {
                foreach (UnitStats unit in group.Units) byName[unit.Name] = unit;
            }
            return byName;
        }

        private static UnitStatOverrides Read(string text, out List<string> errors, out List<string> notices, string file = "UNSC.json")
        {
            errors = new List<string>();
            notices = new List<string>();
            var overrides = new UnitStatOverrides();
            overrides.Read(file, text, Defaults(), Version, errors, notices, new Dictionary<string, string>());
            return overrides;
        }

        private static string Units(params string[] units) => "{\"gameVersion\":\"" + Version + "\",\"units\":[" + string.Join(",", units) + "]}";

        private static void AssertOneError(string text, string expected)
        {
            Read(text, out List<string> errors, out _);
            Assert.AreEqual(1, errors.Count, "Errors: " + string.Join(" | ", errors));
            StringAssert.Contains(expected, errors[0]);
        }

        // ------------------------------------------------------------------ writing and reading back

        [Test]
        public void RoundTrip_AnUnchangedFile_ChangesNothing()
        {
            foreach (UnitStatGroup group in Groups())
            {
                string text = UnitStatOverrides.Write(group.Title, group.Units, Version);
                UnitStatOverrides overrides = Read(text, out List<string> errors, out List<string> notices, group.FileName);
                CollectionAssert.IsEmpty(errors, group.FileName);
                CollectionAssert.IsEmpty(notices, group.FileName);
                Assert.AreEqual(0, overrides.ChangeCount, "An unedited file plays the assets bit for bit: " + string.Join(" | ", overrides.Changes));
            }
            string unsc = UnitStatOverrides.Write("UNSC base roster", Groups()[0].Units, Version);
            StringAssert.Contains("\"baseDamage\": 1.1111,", unsc);
            StringAssert.Contains("\"reinforcement\": {", unsc, "The Pelican's drop is written.");
            StringAssert.DoesNotContain("\"beamSeconds\"", unsc, "A rifle has no beam fields.");
            StringAssert.Contains("\"beamSeconds\": 3", UnitStatOverrides.Write("Covenant", Groups()[1].Units, Version));
            StringAssert.Contains("\"defenseArc\": {", UnitStatOverrides.Write("Covenant", Groups()[1].Units, Version));
        }

        [Test]
        public void AChange_ReachesTheUnit_AndWhatItBrings_TheParentsBodiesWin()
        {
            UnitStatOverrides overrides = Read(Units(
                "{\"name\":\"Marine\",\"cost\":150,\"squadSize\":20,\"weapons\":[{\"name\":\"MA5 Assault Rifle\",\"damagePerShot\":5}]}",
                "{\"name\":\"Pelican\",\"reinforcement\":{\"bodies\":8}}"), out List<string> errors, out _);
            CollectionAssert.IsEmpty(errors);
            Assert.AreEqual(4, overrides.ChangeCount, string.Join(" | ", overrides.Changes));
            CollectionAssert.Contains(overrides.Changes, "Marine: cost 100 -> 150");

            UnitStatOverrides.Active = overrides;
            UnitStats marine = Marine();
            Assert.AreEqual(150, marine.Cost);
            Assert.AreEqual(20, marine.SquadSize);
            Assert.AreEqual(5f, marine.Weapons[0].DamagePerShot);
            Assert.AreEqual(50f, marine.MaxHealth, "A field left out keeps its default.");

            UnitStats pelican = Pelican();
            Assert.AreEqual(8, pelican.LockedReinforcement.SquadSize, "The Pelican's own drop size wins over the Marine's squad size.");
            Assert.AreEqual(150, pelican.LockedReinforcement.Cost, "The dropped Marines have the Marine's changes.");
            Assert.AreEqual(5f, pelican.LockedReinforcement.Weapons[0].DamagePerShot);
            Assert.AreEqual(400, pelican.Cost);

            UnitStatOverrides.Active = null;
            Assert.AreEqual(100, Marine().Cost, "Without overrides, the defaults.");
        }

        [Test]
        public void Apply_ToACopyWithoutItsNestedUnits_DoesNotThrow()
        {
            UnitStatOverrides overrides = Read(Units("{\"name\":\"Pelican\",\"reinforcement\":{\"bodies\":8,\"delaySeconds\":12}}"), out List<string> errors, out _);
            CollectionAssert.IsEmpty(errors);
            UnitStatOverrides.Active = overrides;
            UnitStats pelican = null;
            Assert.DoesNotThrow(() => pelican = Pelican(noDrop: true));
            Assert.IsNull(pelican.LockedReinforcement);
            Assert.AreEqual(10f, pelican.ReinforcementDelaySeconds, "No drop at this depth: its timing is left alone.");
        }

        [Test]
        public void ATechChange_IsMadeOnTheUnitsOwnTech_AndItsTextFollows()
        {
            UnitStatOverrides overrides = Read(Units("{\"name\":\"Marine\",\"techs\":[{\"name\":\"Body Armor\",\"healthMultiplier\":1.5,\"cost\":90},{\"name\":\"Long Range\"}]}"), out List<string> errors, out _);
            CollectionAssert.IsEmpty(errors);
            UnitStatOverrides.Active = overrides;
            TechUpgrade armor = Marine().TechUpgrades[0];
            Assert.AreEqual(1.5f, armor.HealthMultiplier);
            Assert.AreEqual(90, armor.Cost);
            StringAssert.Contains("+50% health", armor.EffectsText());
            Assert.AreEqual(1.3f, MarineBase().TechUpgrades[0].HealthMultiplier, "A fresh tech each time: the defaults are untouched.");
        }

        [Test]
        public void TinyFloatDrift_IsNotAChange_UnderscoreKeysAreNotes_AndAnotherVersionIsANotice()
        {
            UnitStatOverrides overrides = Read("{\"_help\":\"x\",\"gameVersion\":\"0.1.0\",\"units\":[{\"name\":\"Marine\",\"_note\":\"mine\",\"baseDamage\":1.1111001,\"weapons\":[{\"name\":\"MA5 Assault Rifle\",\"damagePerShot\":4.0000001}]}]}",
                out List<string> errors, out List<string> notices);
            CollectionAssert.IsEmpty(errors);
            Assert.AreEqual(0, overrides.ChangeCount, string.Join(" | ", overrides.Changes));
            Assert.AreEqual(1, notices.Count);
            StringAssert.Contains("0.1.0", notices[0]);
        }

        // ------------------------------------------------------------------ mistakes

        [Test]
        public void ASyntaxError_NamesTheFileAndLine()
        {
            string text = "{\n  \"gameVersion\": \"0.399.0\",\n  \"units\": [\n    {\n      \"name\": \"Marine\",\n      \"cost\": 150\n      \"squadSize\": 20\n    }\n  ]\n}\n";
            AssertOneError(text, "UNSC.json line 7");
            AssertOneError("{\"units\":[]} // mine", "comments are not allowed");
            AssertOneError("{\"units\":[{\"name\":\"Marine\",\"cost\":150,}]}", "','");
            AssertOneError("{\"units\":[{\"name\":\"Marine\",\"cost\":NaN}]}", "NaN");
        }

        [Test]
        public void WrongTypes_UnknownFields_AndUnknownUnits_AreMistakes()
        {
            AssertOneError(Units("{\"name\":\"Marine\",\"cost\":\"150\"}"), "'cost' must be a whole number");
            AssertOneError(Units("{\"name\":\"Marine\",\"maxHealth\":true}"), "'maxHealth' must be a number");
            AssertOneError(Units("{\"name\":\"Marine\",\"weapons\":[{\"name\":\"MA5 Assault Rifle\",\"isAntiArmor\":1}]}"), "must be true or false");
            AssertOneError(Units("{\"name\":\"Marine\",\"speed\":3}"), "unknown field 'speed'");
            AssertOneError(Units("{\"name\":\"Marines\",\"cost\":3}"), "no unit called \"Marines\"");
            AssertOneError(Units("{\"cost\":3}"), "needs a \"name\"");
            AssertOneError("{\"units\":[],\"unitz\":[]}", "unknown field 'unitz'");
            AssertOneError("[1,2]", "one object");
        }

        [Test]
        public void AUnitGivenTwice_OrAKeyGivenTwice_IsAMistake()
        {
            AssertOneError(Units("{\"name\":\"Marine\"}", "{\"name\":\"Marine\"}"), "given twice in this file");
            AssertOneError(Units("{\"name\":\"Marine\",\"cost\":1,\"cost\":2}"), "\"cost\" is given twice");

            var errors = new List<string>();
            var seen = new Dictionary<string, string>();
            var overrides = new UnitStatOverrides();
            overrides.Read("UNSC.json", Units("{\"name\":\"Marine\"}"), Defaults(), Version, errors, new List<string>(), seen);
            overrides.Read("UNSC_Noble.json", Units("{\"name\":\"Marine\"}"), Defaults(), Version, errors, new List<string>(), seen);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("UNSC_Noble.json line 1: \"Marine\" is also given in UNSC.json", errors[0]);
        }

        [Test]
        public void Enums_AreNamesInAnyCase_NotNumbersOrLists()
        {
            UnitStatOverrides overrides = Read(Units("{\"name\":\"Marine\",\"targetFilter\":\"ground\",\"armorType\":\"Heavy\"}"), out List<string> errors, out _);
            CollectionAssert.IsEmpty(errors);
            UnitStatOverrides.Active = overrides;
            Assert.AreEqual(TargetFilter.Ground, Marine().TargetFilter);
            Assert.AreEqual(ArmorType.Heavy, Marine().ArmorType);
            AssertOneError(Units("{\"name\":\"Marine\",\"targetFilter\":\"2\"}"), "must be one of \"Ground\", \"Air\", \"Both\"");
            AssertOneError(Units("{\"name\":\"Marine\",\"targetFilter\":\"Ground, Air\"}"), "must be one of");
            AssertOneError(Units("{\"name\":\"Marine\",\"targetFilter\":2}"), "must be one of");
        }

        [Test]
        public void Numbers_MustBeFinite_AndWholeWhereWhole()
        {
            AssertOneError(Units("{\"name\":\"Marine\",\"maxHealth\":1e39}"), "too large");
            AssertOneError(Units("{\"name\":\"Marine\",\"squadSize\":2.5}"), "whole number");
            AssertOneError(Units("{\"name\":\"Marine\",\"cost\":1e2}"), "whole number");
            AssertOneError(Units("{\"name\":\"Marine\",\"cost\":99999999999999}"), "too large");
            UnitStatOverrides overrides = Read(Units("{\"name\":\"Marine\",\"maxHealth\":6e1}"), out List<string> errors, out _);
            CollectionAssert.IsEmpty(errors, "A float may use an exponent.");
            Assert.AreEqual(1, overrides.ChangeCount);
        }

        [Test]
        public void ChangedValues_OutsideTheLimits_AreMistakes()
        {
            AssertOneError(Units("{\"name\":\"Marine\",\"squadSize\":0}"), "'squadSize' must be from 1 to 100");
            AssertOneError(Units("{\"name\":\"Marine\",\"squadSize\":101}"), "'squadSize' must be from 1 to 100");
            AssertOneError(Units("{\"name\":\"Marine\",\"cost\":0}"), "'cost' must be from 1 to 100000");
            AssertOneError(Units("{\"name\":\"Marine\",\"maxHealth\":0}"), "'maxHealth' must be above 0");
            AssertOneError(Units("{\"name\":\"Marine\",\"moveSpeed\":-1}"), "'moveSpeed' must be from 0 to 30");
            AssertOneError(Units("{\"name\":\"Marine\",\"weapons\":[{\"name\":\"MA5 Assault Rifle\",\"fireInterval\":0.01}]}"), "'fireInterval' must be from 0.05");
            AssertOneError(Units("{\"name\":\"Marine\",\"weapons\":[{\"name\":\"MA5 Assault Rifle\",\"range\":200}]}"), "'range' must be above 0 and at most 150");
            AssertOneError(Units("{\"name\":\"Jackal\",\"defenseArc\":{\"blockChance\":1.5}}"), "'blockChance' must be from 0 to 1");
            AssertOneError(Units("{\"name\":\"Marine\",\"techs\":[{\"name\":\"Body Armor\",\"healthMultiplier\":0},{\"name\":\"Long Range\"}]}"), "'healthMultiplier' must be above 0");
        }

        [Test]
        public void TheWeaponAndTechLists_KeepTheirCountAndNames_AndFieldsAUnitLacksAreMistakes()
        {
            AssertOneError(Units("{\"name\":\"Marine\",\"weapons\":[]}"), "must list its 1 weapon(s)");
            AssertOneError(Units("{\"name\":\"Marine\",\"weapons\":[{\"name\":\"Laser\",\"damagePerShot\":9}]}"), "must be named \"MA5 Assault Rifle\"");
            AssertOneError(Units("{\"name\":\"Marine\",\"techs\":[{\"name\":\"Body Armor\"}]}"), "must list its 2 tech(s)");
            AssertOneError(Units("{\"name\":\"Marine\",\"weapons\":[{\"name\":\"MA5 Assault Rifle\",\"beamSeconds\":2}]}"), "'beamSeconds' is not used by this one");
            AssertOneError(Units("{\"name\":\"Marine\",\"defenseArc\":{\"blockChance\":0.5}}"), "Marine has no 'defenseArc'");
            AssertOneError(Units("{\"name\":\"Jackal\",\"reinforcement\":{\"bodies\":3}}"), "Jackal has no 'reinforcement'");
        }

        // ------------------------------------------------------------------ the folder at launch

        private UnitStatsLoadResult LoadFolder() => UnitStatsFolder.Load(_folder, Groups(), Version, TestRules.Default(), () => Groups());

        [Test]
        public void TheFirstLaunch_WritesEveryFile_AndPlaysTheDefaults()
        {
            UnitStatsLoadResult result = LoadFolder();
            Assert.AreEqual(UnitStatsState.Defaults, result.State, string.Join(" | ", result.Errors));
            Assert.IsTrue(File.Exists(Path.Combine(_folder, "UNSC.json")));
            Assert.IsTrue(File.Exists(Path.Combine(_folder, "Covenant.json")));
            Assert.IsNull(result.Overrides);
            Assert.IsNull(result.LogLabel);

            UnitStatsLoadResult again = LoadFolder();
            Assert.AreEqual(UnitStatsState.Defaults, again.State, "The written files read back with no changes: " + string.Join(" | ", again.Errors));
            CollectionAssert.IsEmpty(again.Notices);
        }

        [Test]
        public void AFileWithAMistake_IsNeverRewritten_TheMissingOneIsWritten_AndEveryUnitPlaysTheDefaults()
        {
            Directory.CreateDirectory(_folder);
            string corrupt = Path.Combine(_folder, "UNSC.json");
            File.WriteAllText(corrupt, "{ \"units\": [ { \"name\": \"Marine\", \"cost\": 150 ");
            DateTime written = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(corrupt, written);
            byte[] before = File.ReadAllBytes(corrupt);
            File.WriteAllText(Path.Combine(_folder, "Notes.json"), "{}");

            UnitStatsLoadResult result = LoadFolder();
            Assert.AreEqual(UnitStatsState.Errors, result.State);
            Assert.IsNull(result.Overrides);
            Assert.IsNull(UnitStatOverrides.Active, "Load leaves Active as it was; the caller sets it from the result.");
            StringAssert.StartsWith("UNSC.json line 1", result.Errors[0]);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(corrupt), "A player's file is never written to.");
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(corrupt));
            Assert.IsTrue(File.Exists(Path.Combine(_folder, "Covenant.json")), "A missing file is written from the defaults.");
            Assert.IsTrue(result.Notices.Any(n => n.StartsWith("Notes.json")), "Other files are left alone, with a notice.");
        }

        [Test]
        public void AGoodEdit_IsInForce_AndTheDryRunCatchesWhatTheSimulationWouldRefuse()
        {
            LoadFolder(); // writes the defaults
            string covenant = Path.Combine(_folder, "Covenant.json");
            string text = File.ReadAllText(covenant);
            File.WriteAllText(covenant, text.Replace("\"maxHealth\": 60,", "\"maxHealth\": 99,"));
            UnitStatsLoadResult modded = LoadFolder();
            Assert.AreEqual(UnitStatsState.Modded, modded.State, string.Join(" | ", modded.Errors));
            Assert.AreEqual(1, modded.ChangeCount);
            Assert.AreEqual("modded:1", modded.LogLabel);
            UnitStatOverrides.Active = modded.Overrides;
            Assert.AreEqual(99f, Jackal().MaxHealth);
            UnitStatOverrides.Active = null;

            // A minimum range past the range passes the reader's limits, but the simulation's own check refuses it.
            File.WriteAllText(covenant, text.Replace("\"minRange\": 0,", "\"minRange\": 40,"));
            UnitStatsLoadResult refused = LoadFolder();
            Assert.AreEqual(UnitStatsState.Errors, refused.State);
            Assert.IsTrue(refused.Errors.Any(e => e.StartsWith("Covenant.json line ") && e.Contains("Jackal, weapon 1 (Plasma Pistol): minRange 0 -> 40: minimum range")), string.Join(" | ", refused.Errors));

            // A squad too big for the deployment zone could never be bought: the message points at the squadSize line.
            string bigText = text.Replace("\"squadSize\": 1,", "\"squadSize\": 100,");
            File.WriteAllText(covenant, bigText);
            UnitStatsLoadResult tooBig = LoadFolder();
            Assert.AreEqual(UnitStatsState.Errors, tooBig.State);
            Assert.AreEqual(1, tooBig.Errors.Count, string.Join(" | ", tooBig.Errors));
            StringAssert.StartsWith($"Covenant.json line {LineOf(bigText, "\"squadSize\": 100,")}: Scarab: squadSize 1 -> 100: a squad of 100", tooBig.Errors[0]);
            StringAssert.Contains("deployment zone", tooBig.Errors[0]);
            Assert.IsNull(UnitStatOverrides.Active, "The dry run puts Active back.");
        }

        private static int LineOf(string text, string fragment)
        {
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(fragment)) return i + 1;
            }
            return -1;
        }

        /// <summary>The Marine in UNSC.json and the Pelican, which drops Marines, in a file of its own.</summary>
        private static List<UnitStatGroup> SplitGroups() => new List<UnitStatGroup>
        {
            new UnitStatGroup { FileName = "UNSC.json", Title = "UNSC base roster", Units = { Marine() }, Buyable = { "Marine" } },
            new UnitStatGroup { FileName = "UNSC_Air.json", Title = "UNSC air", Units = { Pelican() }, Buyable = { "Pelican" } },
        };

        [Test]
        public void ADryRunProblem_NamesTheEditedFileLineAndField_AndACarrierInAnotherFileDoesNotRepeatIt()
        {
            UnitStatsFolder.Load(_folder, SplitGroups(), Version, TestRules.Default(), SplitGroups); // writes the defaults
            string unsc = Path.Combine(_folder, "UNSC.json");
            string text = File.ReadAllText(unsc).Replace("\"minRange\": 0,", "\"minRange\": 40,");
            File.WriteAllText(unsc, text);

            UnitStatsLoadResult refused = UnitStatsFolder.Load(_folder, SplitGroups(), Version, TestRules.Default(), SplitGroups);
            Assert.AreEqual(UnitStatsState.Errors, refused.State);
            Assert.AreEqual(1, refused.Errors.Count, "The Pelican's dropped Marines don't repeat the Marine's problem: " + string.Join(" | ", refused.Errors));
            int line = LineOf(text, "\"minRange\": 40,");
            Assert.Greater(line, 1);
            Assert.AreEqual($"UNSC.json line {line}: Marine, weapon 1 (MA5 Assault Rifle): minRange 0 -> 40: minimum range must be 0 or more and below range.", refused.Errors[0]);
            Assert.IsFalse(refused.Errors.Any(e => e.Contains("UNSC_Air.json")), "The carrier's file was not edited.");

            // The defaults themselves have no problem, so the one above is the edit's.
            UnitStatOverrides.Active = null;
            CollectionAssert.IsEmpty(UnitStatsFolder.Check(SplitGroups(), TestRules.Default()));
        }
    }
}
