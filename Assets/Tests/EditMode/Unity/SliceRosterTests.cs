// SliceRosterTests.cs
// Checks the slice roster assets (Milestone 4, Bible Section 8): all 12 UNSC and 13 Covenant units (the Covenant Phantom came later)
// exist with the Bible's role, tier, unlock price, squad size, cost and health; their data is
// valid; base damage follows the Bible default; each faction covers every role of Section 7; the
// abilities named in Section 8 are set up; every unit has a placeholder prefab; and the faction
// assets use these rosters.
// Needs the Unity editor (AssetDatabase), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Units, Halobellum.EditorTools (HeadlessRunnerTool), Halobellum.Core, NUnit, UnityEditor.

using System;
using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.EditorTools;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;

namespace Halobellum.Tests
{
    public class SliceRosterTests
    {
        // Bible Section 8: name, role, tier, unlock, squad size, cost, health (per body).
        private static readonly (string name, UnitRole role, int tier, int unlock, int squad, int cost, float health)[] Unsc =
        {
            ("Marine", UnitRole.Chaff, 1, 0, 18, 100, 50), // Health 60 until D140.
            ("Flamethrower Trooper", UnitRole.ChaffClear, 1, 0, 6, 150, 95), // 90 until the balance pass (D138). Health 120 until D140.
            ("Warthog", UnitRole.Flanker, 1, 0, 2, 200, 380), // 300 until tuned for the Insane AI (D125). Health 360 until D140.
            ("Gauss Warthog", UnitRole.SingleTargetDps, 2, 50, 1, 250, 300),
            ("Scorpion", UnitRole.Tank, 2, 50, 1, 300, 1200),
            ("Wolverine", UnitRole.AntiAir, 2, 50, 1, 300, 480), // Health 500 until D140.
            ("Hornet", UnitRole.Aerial, 2, 50, 2, 300, 505), // 400 until D138. Health 500 until D140, 490 until D141.
            ("Archer Missile Pod", UnitRole.Artillery, 2, 50, 2, 300, 350),
            ("Pelican", UnitRole.Aerial, 2, 50, 1, 300, 900), // Health 1200 until D140, 1380 until D141, 1550 until D155, 1400 until D179.
            ("Spartan", UnitRole.SingleTargetDps, 3, 250, 1, 500, 1250),
            ("Mantis", UnitRole.Tank, 3, 250, 1, 500, 3360), // 2500 until D138. Health 2700 until D140.
            ("Elephant", UnitRole.Titan, 4, 150, 1, 800, 8240), // Unlock 350; health 8000 until D140, 8580 until D141.
        };

        private static readonly (string name, UnitRole role, int tier, int unlock, int squad, int cost, float health)[] Covenant =
        {
            ("Grunt", UnitRole.Chaff, 1, 0, 20, 100, 40),
            ("Fuel Rod Grunt", UnitRole.ChaffClear, 1, 0, 4, 150, 80),
            ("Jackal (Beam Rifle)", UnitRole.SingleTargetDps, 1, 0, 4, 175, 70), // Snipers since 2026-11-05: was Jackal (Plasma Rifle), 8 to a squad at 150; 175 after the tournament (D72).
            ("Jackal (Shield)", UnitRole.Tank, 1, 0, 6, 150, 80), // 70 until D138.
            ("Ghost", UnitRole.Flanker, 1, 0, 2, 200, 250),
            ("Elite", UnitRole.SingleTargetDps, 2, 50, 3, 300, 230), // 200 until D138.
            ("Wraith", UnitRole.Artillery, 2, 50, 1, 300, 900),
            ("Vampire", UnitRole.AntiAir, 2, 50, 1, 300, 800), // 500 until D138, 550 until D141, 570 until D149, 700 until D155.
            ("Banshee", UnitRole.Aerial, 2, 50, 2, 300, 440), // Health 400 until D140, 410 until D141.
            ("Covenant Phantom", UnitRole.Aerial, 2, 50, 1, 300, 1000), // Added 2026-11-05: the Pelican's mirror. 800 until D151, 1550 until D179.
            ("Hunter Pair", UnitRole.Tank, 3, 225, 2, 450, 1440), // Health 1500 until D140.
            ("Zealot", UnitRole.SingleTargetDps, 3, 200, 1, 400, 1400),
            ("Scarab", UnitRole.Titan, 4, 150, 1, 800, 9050), // Unlock 350, 50 until D141; health 8500 until D140, 8780 until D141.
        };

        private static readonly (string name, UnitRole role, int tier, int unlock, int squad, int cost, float health)[] Flood =
        {
            ("Infection Forms", UnitRole.Chaff, 1, 0, 10, 100, 290), // 228 until D138. Health 235 until D140, 310 until D141.
            ("Carrier Form", UnitRole.ChaffClear, 1, 0, 4, 150, 255), // 140 until D125, 170 until D138. Health 190 until D140, 230 until D141.
            ("Combat Form", UnitRole.SingleTargetDps, 2, 50, 6, 200, 280), // 260 until D138.
            ("Stalker Form", UnitRole.Flanker, 2, 50, 3, 250, 450),
            ("Pure Form Ranged", UnitRole.Artillery, 2, 50, 2, 250, 400),
            ("Swarm Form", UnitRole.AntiAir, 2, 50, 4, 250, 275), // 200 until D138. Health 230 until D140, 240 until D141.
            ("Pure Form Tank", UnitRole.Tank, 3, 200, 1, 400, 3400), // 3200 until D138.
            ("Seeder", UnitRole.SingleTargetDps, 3, 200, 2, 475, 900), // 800 until D138.
            ("Infected Pelican", UnitRole.Aerial, 3, 200, 1, 400, 1500), // 900 until D134, 1350 until D138.
            ("Infected Phantom", UnitRole.Aerial, 3, 200, 1, 400, 1400), // 1100 until D125. Health 1250 until D140, 1350 until D141.
            ("Flood Abomination", UnitRole.Titan, 4, 150, 1, 800, 10570), // 8500 until D125. Unlock 350, 250 until D141; health 10000 until D140.
        };

        private static Dictionary<string, UnitDefinition> Load(string folder) =>
            HeadlessRunnerTool.LoadUnitsInFolder(folder).ToDictionary(u => u.unitName);

        private static Dictionary<string, UnitStats> AllStats() =>
            HeadlessRunnerTool.LoadSliceUnits().ToDictionary(u => u.unitName, u => u.ToStats());

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Roster_MatchesSection8(int factionIndex)
        {
            var table = factionIndex == 0 ? Unsc : factionIndex == 1 ? Covenant : Flood;
            Dictionary<string, UnitDefinition> units = Load(HeadlessRunnerTool.SliceFolders[factionIndex]);
            Assert.AreEqual(table.Length, units.Count, "Exactly the units of the Section 8 table.");
            foreach (var row in table)
            {
                Assert.IsTrue(units.TryGetValue(row.name, out UnitDefinition unit), "Missing " + row.name);
                Assert.AreEqual(row.role, unit.role, row.name);
                Assert.AreEqual(row.tier, unit.tier, row.name);
                Assert.AreEqual(row.unlock, unit.unlockCost, row.name);
                Assert.AreEqual(row.squad, unit.squadSize, row.name);
                Assert.AreEqual(row.cost, unit.cost, row.name);
                Assert.AreEqual(row.health, unit.maxHealth, 1e-3f, row.name);
                Assert.AreEqual(unit.cost / (float)unit.squadSize / 5f, unit.baseDamage, 1e-3f, row.name + ": base damage = (cost / squad size) / 5.");
                Assert.IsNotNull(unit.prefab, row.name + " needs a placeholder prefab.");
                Assert.IsNotEmpty(unit.notes, row.name + ": list its best counters in the notes (Section 7).");
                List<string> problems = unit.ToStats().Validate();
                Assert.IsEmpty(problems, string.Join("\n", problems));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EveryFaction_CoversEveryRole(int factionIndex)
        {
            var roles = Load(HeadlessRunnerTool.SliceFolders[factionIndex]).Values.Select(u => u.role).ToList();
            foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
            {
                Assert.Contains(role, roles, $"No {role} unit.");
            }
        }

        [Test]
        public void Section8Abilities_AreSetUp()
        {
            Dictionary<string, UnitStats> u = AllStats();

            // Armor, shields and air units named in Section 8.
            Assert.AreEqual(ArmorType.Light, u["Warthog"].ArmorType);
            Assert.AreEqual(ArmorType.Heavy, u["Scorpion"].ArmorType);
            Assert.AreEqual(ArmorType.Heavy, u["Wraith"].ArmorType);
            foreach (string shielded in new[] { "Spartan", "Elite", "Zealot" })
            {
                Assert.IsTrue(u[shielded].HasShield, shielded + " has an energy shield.");
                Assert.AreEqual(ArmorType.Unarmored, u[shielded].ArmorType, shielded + " has an Unarmored body.");
            }
            Assert.Greater(u["Elite"].ShieldRegenPerSecond, 0f, "Regenerating shield.");
            Assert.Greater(u["Zealot"].MaxShield, u["Elite"].MaxShield, "Zealots have a strong shield.");
            foreach (string flyer in new[] { "Hornet", "Pelican", "Banshee", "Vampire" }) Assert.IsTrue(u[flyer].IsAir, flyer + " flies.");

            // Bonus damage against air for both anti-air units, which can still hit ground.
            foreach (string antiAir in new[] { "Wolverine", "Vampire" })
            {
                WeaponStats main = u[antiAir].Weapons[0];
                Assert.Greater(main.AirDamageMultiplier, 1f, antiAir);
                Assert.IsTrue(main.CanTarget(u[antiAir], targetIsAir: false), antiAir + " can still hit ground.");
            }

            // Weapons and damage types.
            Assert.IsTrue(u["Gauss Warthog"].Weapons[0].IsAntiArmor, "Anti-armor gauss cannon.");
            Assert.AreEqual(DamageType.Fire, u["Flamethrower Trooper"].Weapons[0].DamageType);
            Assert.IsTrue(u["Flamethrower Trooper"].Weapons[0].IsCone, "Splash cone.");
            Assert.LessOrEqual(u["Flamethrower Trooper"].Weapons[0].ConeDegrees, 15f, "A narrow cone (D22).");
            Assert.LessOrEqual(u["Flamethrower Trooper"].Weapons[0].FireInterval, 0.1f, "Low damage, very fast ticks (D22).");
            Assert.Greater(u["Archer Missile Pod"].Weapons[0].MinRange, 0f, "Artillery has a minimum range.");
            Assert.Greater(u["Wraith"].Weapons[0].SplashRadius, 0f, "Mortar splash.");
            Assert.AreEqual(DamageType.Explosive, u["Scorpion"].Weapons[0].DamageType, "Cannon (explosive) plus coax.");
            Assert.AreEqual(2, u["Scorpion"].Weapons.Length);
            Assert.AreEqual(1, u["Hornet"].Weapons.Length, "Its rockets only: no bombs since D155.");
            Assert.AreEqual(1f, u["Hornet"].Weapons[0].SplashRadius, 1e-4f, "Its rockets' 1 m splash (D155).");
            Assert.IsTrue(u["Pelican"].Weapons[0].CanTarget(u["Pelican"], true) && u["Pelican"].Weapons[0].CanTarget(u["Pelican"], false),
                "Pelican autocannon hits air and ground.");
            Assert.AreEqual(32f, u["Pelican"].Weapons[0].DamagePerShot, 1e-3f, "25 from D142 until D179, when its health came down to 900.");
            Assert.AreEqual(1f, u["Pelican"].Weapons[0].SplashRadius, 1e-3f, "A 1 m splash (the owner, D142).");
            Assert.AreEqual(DamageType.Ballistic, u["Pelican"].Weapons[0].DamageType);

            // Pelican: a plain air unit until its Troop Bay tech (decision D22), which drops 6 Marines at 5 s, then at least 10 s apart.
            Assert.IsFalse(u["Pelican"].HasReinforcement, "The Pelican only carries Marines after its Troop Bay tech.");
            Assert.IsNotNull(u["Pelican"].LockedReinforcement);
            Assert.AreEqual("Marine", u["Pelican"].LockedReinforcement.Name);
            Assert.AreEqual(6, u["Pelican"].LockedReinforcement.SquadSize);
            Assert.AreEqual(10f, u["Pelican"].ReinforcementDelaySeconds, 1e-3f);
            Assert.AreEqual(5f, u["Pelican"].ReinforcementFirstDropSeconds, 1e-3f);
            TechUpgrade bay = u["Pelican"].TechUpgrades.Single(t => t.UnlocksReinforcement);
            Assert.IsTrue(UnitUpgrades.Apply(u["Pelican"], 1, new[] { bay }).HasReinforcement);
            StringAssert.StartsWith("It lands where it is to drop 6 Marines 5 seconds into the battle", bay.Description);
            StringAssert.DoesNotContain("behind enemy lines", bay.Description, "It never lands since D143.");
            Assert.AreEqual(1.5f, u["Pelican"].ReinforcementLandSeconds, 1e-3f, "It comes down to let its Marines out, where it is (D143).");

            // The Covenant Phantom: Grunts after its Gravity Lift tech, dropped from the air like the Infected Phantom's; it never lands (D142).
            UnitStats phantom = u["Covenant Phantom"];
            Assert.IsTrue(phantom.IsAir);
            Assert.IsFalse(phantom.HasReinforcement);
            Assert.AreEqual("Grunt", phantom.LockedReinforcement.Name);
            Assert.AreEqual(7, phantom.LockedReinforcement.SquadSize);
            Assert.AreEqual(u["Pelican"].ReinforcementDrops, phantom.ReinforcementDrops);
            Assert.AreEqual(0f, phantom.ReinforcementLandSeconds, 1e-3f, "It never lands, like the Infected Phantom (landed for 1.5 s until D142).");
            Assert.AreEqual(u["Pelican"].ReinforcementFirstDropSeconds, phantom.ReinforcementFirstDropSeconds, 1e-3f);
            Assert.AreEqual(10f, phantom.ReinforcementDelaySeconds, 1e-3f);
            TechUpgrade lift = phantom.TechUpgrades.Single(t => t.UnlocksReinforcement);
            Assert.IsTrue(UnitUpgrades.Apply(phantom, 1, new[] { lift }).HasReinforcement);
            StringAssert.DoesNotContain("behind enemy lines", lift.Description);
            StringAssert.DoesNotContain("lands", lift.Description);
            Assert.AreEqual("Plasma Turrets", phantom.Weapons.Single().Name, "Its sound goes by this name.");

            // Jackal snipers: few to a squad, slow hard shots that land at once, twice the reach of the plasma rifle they had.
            WeaponStats beamRifle = u["Jackal (Beam Rifle)"].Weapons.Single();
            Assert.AreEqual("Beam Rifle", beamRifle.Name);
            Assert.IsTrue(beamRifle.IsInstant);
            Assert.GreaterOrEqual(beamRifle.Range, 24f);
            Assert.GreaterOrEqual(beamRifle.FireInterval, 2f);
            Assert.AreEqual("Plasma Pistol", u["Jackal (Shield)"].Weapons.Single().Name, "The shield Jackals keep their pistols.");

            // Directional defense: Shield Jackals (120 degrees, block 80% of ballistic projectiles) and Hunters (240 degrees).
            DefenseArcStats jackal = u["Jackal (Shield)"].DefenseArc;
            Assert.IsTrue(u["Jackal (Shield)"].HasDefenseArc);
            Assert.AreEqual(120f, jackal.ArcDegrees, 1e-3f);
            Assert.AreEqual(0.8f, jackal.BlockChance, 1e-3f);
            Assert.IsTrue(jackal.Blocks(DamageType.Ballistic));
            DefenseArcStats hunter = u["Hunter Pair"].DefenseArc;
            Assert.AreEqual(240f, hunter.ArcDegrees, 1e-3f);
            Assert.Less(hunter.FrontDamageMultiplier, 1f, "Very resistant inside the arc.");
            Assert.AreEqual(1f, hunter.RearDamageMultiplier, 1e-4f, "No weaker from behind (the owner, D155; x1.5 until then).");

            // Titans have several weapons (turrets for chaff, a main gun for armor).
            Assert.GreaterOrEqual(u["Elephant"].Weapons.Length, 2);
            Assert.GreaterOrEqual(u["Scarab"].Weapons.Length, 2);
        }

        [Test]
        public void OneFocusBeam_KillsALevelOneScorpion()
        {
            Dictionary<string, UnitStats> u = AllStats();
            UnitStats scarab = u["Scarab"];
            scarab.Weapons = new[] { scarab.Weapons[0] }; // The beam alone, without the turrets.
            Assert.IsTrue(scarab.Weapons[0].IsBeam);

            var sim = new BattleSimulation(HeadlessRunnerTool.LoadRules(), 1);
            sim.AddUnit(scarab, Team.A, Vec2.Zero);
            SimUnit scorpion = sim.AddUnit(u["Scorpion"], Team.B, new Vec2(0f, 25f));
            for (int i = 0; i < 57; i++) sim.Step(); // Just under 2 s: still charging.
            Assert.AreEqual(scorpion.Stats.MaxHealth, scorpion.Health, "Nothing hits before the beam fires.");
            for (int i = 0; i < 95; i++) sim.Step(); // The rest of the charge and the 3 s beam.
            Assert.IsFalse(scorpion.IsAlive);
        }

        [Test]
        public void Artillery_IsLobbed()
        {
            Dictionary<string, UnitStats> u = AllStats();
            Assert.IsTrue(u["Wraith"].Weapons[0].Lobbed);
            Assert.IsTrue(u["Archer Missile Pod"].Weapons[0].Lobbed);
            Assert.IsFalse(u["Scorpion"].Weapons[0].Lobbed, "Tank cannons fire straight.");
            Assert.IsFalse(u["Elephant"].Weapons[0].Lobbed);
        }

        [Test]
        public void EveryFloodVehicle_ReleasesAGroupOf6InfectionForms_OnDeath()
        {
            var all = new Dictionary<string, UnitStats>();
            foreach (string guid in AssetDatabase.FindAssets("t:UnitDefinition"))
            {
                var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                all[unit.unitName] = unit.ToStats();
            }
            foreach (string name in new[] { "Infested Scorpion", "Infested Hornet", "Infested Ghost", "Infested Wraith", "Infested Banshee", "Infected Phantom" })
            {
                SpawnEntry spawn = all[name].DeathBurst.Spawns.Single();
                Assert.AreEqual("Infection Forms", spawn.Unit.Name, name);
                Assert.AreEqual(6, spawn.Bodies * spawn.Unit.FormsPerBody, name);
            }
        }

        [Test]
        public void OnlyThePelicanAndAerospaceAircraft_CanFlank()
        {
            var flankers = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:UnitDefinition"))
            {
                var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (unit.canFlank) flankers.Add(unit.unitName);
            }
            CollectionAssert.AreEquivalent(new[] { "Pelican", "Sparrowhawk", "Longsword", "Vulture", "Condor Gunship" }, flankers);
        }

        [TestCase("Covenant/HunterPair")]
        [TestCase("LekgoloColony/HunterCaptain")]
        public void Hunters_ShootAircraft_ButBashOnlyTheGround(string path)
        {
            // The owner, D148: "make hunters and the captain be able to target air units". The shield bash stays a ground blow.
            UnitStats hunter = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{path}.asset").ToStats();
            Assert.IsTrue(hunter.Weapons[0].CanTarget(hunter, true) && hunter.Weapons[0].CanTarget(hunter, false), hunter.Weapons[0].Name);
            Assert.IsTrue(!hunter.Weapons[1].CanTarget(hunter, true) && hunter.Weapons[1].CanTarget(hunter, false), hunter.Weapons[1].Name);
        }

        [Test]
        public void EveryFlankerGoesForTowers_ButTheSpartanCQB_Fights()
        {
            // D138: Flankers head for the nearest tower. D147, the owner: "CQB should definitely not just run to the towers though, they should fight".
            var flankers = new List<string>();
            var towerGoers = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:UnitDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Placeholder/")) continue; // The prototype stand-ins, not generated from the roster.
                var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
                if (unit.role == UnitRole.Flanker) flankers.Add(unit.unitName);
                if (unit.goesForTowers) towerGoers.Add(unit.unitName);
            }
            CollectionAssert.Contains(flankers, "Spartan CQB");
            CollectionAssert.AreEquivalent(flankers.Where(n => n != "Spartan CQB"), towerGoers);
        }

        [Test]
        public void ArcherMissilePods_CannotMove()
        {
            var pod = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/UNSC/ArcherMissilePod.asset");
            Assert.AreEqual(0f, pod.ToStats().MoveSpeed);
        }

        [Test]
        public void ThePelican_LandsWhereItIs_ThreeTimes()
        {
            UnitStats pelican = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/UNSC/Pelican.asset").ToStats();
            Assert.AreEqual(6f, pelican.MoveSpeed, 1e-3f, "Twice its old 3 m/s.");
            Assert.AreEqual(3, pelican.ReinforcementDrops);
            Assert.Greater(pelican.ReinforcementLandSeconds, 0f, "It comes down to let its Marines out.");
            Assert.IsNull(pelican.Reinforcement, "Only with its tech.");
            Assert.AreEqual("Marine", pelican.LockedReinforcement.Name);
        }

        [Test]
        public void FloodAbilities_AreSetUp()
        {
            Dictionary<string, UnitStats> u = AllStats();
            UnitStats forms = u["Infection Forms"];
            Assert.AreEqual(6, forms.FormsPerBody);
            Assert.AreEqual(60, forms.FormsInSquad, "60 forms in a squad, in packs of 6.");
            Assert.IsTrue(forms.Infects);
            Assert.AreEqual(6f, forms.LeapRange, 1e-3f, "They pounce from 6 m.");
            Assert.Greater(forms.LeapSpeed, forms.MoveSpeed);
            foreach (var row in Flood)
            {
                Assert.IsTrue(u[row.name].IsFlood, row.name);
                if (row.name != "Infection Forms") Assert.IsFalse(u[row.name].Infects, row.name + ": only Infection Forms infect.");
            }

            UnitStats carrier = u["Carrier Form"];
            Assert.IsTrue(carrier.SelfDestructs);
            Assert.AreEqual("Infection Forms", carrier.DeathBurst.Spawns[0].Unit.Name);
            Assert.Greater(carrier.DeathBurst.Damage, 0f);

            UnitStats pelican = u["Infected Pelican"];
            Assert.IsTrue(pelican.IsAir && pelican.SelfDestructs && pelican.SeeksDensestCluster);
            Assert.AreEqual(30, pelican.DeathBurst.Spawns.Where(s => s.Unit.Name == "Infection Forms").Sum(s => s.Bodies * s.Unit.FormsPerBody), "D60: 30 Infection Forms.");
            Assert.AreEqual(5, pelican.DeathBurst.Spawns.Where(s => s.Unit.Name == "Combat Form").Sum(s => s.Bodies), "Bible: 5 Combat Forms.");
            Assert.AreEqual(1200f, pelican.DeathBurst.Damage, "D134: the owner's harder blast (900 before), so its dive is worth the 400.");

            Assert.IsNotNull(u["Infected Phantom"].Reinforcement, "It carries Combat Forms.");
            Assert.AreEqual("Combat Form", u["Infected Phantom"].Reinforcement.Name);
            Assert.Greater(u["Seeder"].Weapons[0].SeedSeconds, 0f);
            Assert.IsTrue(u["Pure Form Ranged"].Weapons[0].Lobbed);
            Assert.IsTrue(u["Swarm Form"].IsAir);
            Assert.AreEqual(5, u["Swarm Form"].FormsPerBody);

            UnitStats abomination = u["Flood Abomination"];
            Assert.AreEqual("Infection Forms", abomination.SpawnedUnit.Name);
            Assert.Greater(abomination.AuraRadius, 0f);
            Assert.Greater(abomination.AuraSpeedMultiplier, 1f);
        }

        [Test]
        public void Infection_OnlyWorksOnTheRightUnits()
        {
            Dictionary<string, UnitStats> u = AllStats();
            Assert.IsTrue(u["Spartan"].ImmuneToInfection, "Bible: Spartans cannot be infected.");
            foreach (string name in new[] { "Marine", "Grunt", "Elite", "Zealot", "Flamethrower Trooper" })
            {
                Assert.AreEqual(ArmorType.Unarmored, u[name].ArmorType, name);
                Assert.IsFalse(u[name].ImmuneToInfection, name);
            }
            foreach (string name in new[] { "Warthog", "Scorpion", "Hunter Pair", "Wraith", "Elephant" })
                Assert.AreNotEqual(ArmorType.Unarmored, u[name].ArmorType, name);
        }

        [Test]
        public void EveryUnit_HasThreeValidTechUpgrades()
        {
            foreach (UnitDefinition unit in HeadlessRunnerTool.LoadSliceUnits())
            {
                UnitStats stats = unit.ToStats();
                // Three, and a fourth on some: the flat range tech or one that changes how the unit plays (D114).
                Assert.That(stats.TechUpgrades.Length, Is.InRange(3, UnitUpgrades.MaxTechsPerUnit), unit.unitName);
                foreach (TechUpgrade tech in stats.TechUpgrades)
                {
                    Assert.IsNotNull(tech, unit.unitName + " has an empty tech slot.");
                    Assert.Greater(tech.Cost, 0, tech.Name);
                    Assert.IsNotEmpty(tech.EffectsText(), tech.Name + " does nothing.");
                }
            }
            UnitStats flamer = HeadlessRunnerTool.LoadSliceUnits().Single(d => d.unitName == "Flamethrower Trooper").ToStats();
            Assert.IsTrue(flamer.TechUpgrades.Any(t => t.ConeDegreesBonus > 0f), "A later upgrade widens the flamethrower cone (D22).");
        }

        [Test]
        public void PlaceholderUnits_NeverAppearInMatches()
        {
            var placeholders = new HashSet<UnitDefinition>(HeadlessRunnerTool.LoadUnitsInFolder(HeadlessRunnerTool.PlaceholderFolder));
            Assert.IsNotEmpty(placeholders);
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(FactionDefinition), new[] { "Assets/_Project/Data/Factions" }))
            {
                var faction = AssetDatabase.LoadAssetAtPath<FactionDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                IEnumerable<UnitDefinition> units = faction.baseRoster.Concat(faction.subFactions.SelectMany(s => s.exclusiveUnits));
                foreach (UnitDefinition unit in units)
                {
                    Assert.IsFalse(placeholders.Contains(unit), $"{unit.name} is a placeholder unit in the {faction.factionName} faction (decision D24).");
                }
            }
        }

        [Test]
        public void FactionAssets_UseTheSliceRosters()
        {
            var unsc = AssetDatabase.LoadAssetAtPath<FactionDefinition>("Assets/_Project/Data/Factions/UNSC_Standard.asset");
            var covenant = AssetDatabase.LoadAssetAtPath<FactionDefinition>("Assets/_Project/Data/Factions/Covenant_Standard.asset");
            CollectionAssert.AreEquivalent(Unsc.Select(r => r.name), unsc.baseRoster.Select(u => u.unitName));
            CollectionAssert.AreEquivalent(Covenant.Select(r => r.name), covenant.baseRoster.Select(u => u.unitName));
            var flood = AssetDatabase.LoadAssetAtPath<FactionDefinition>("Assets/_Project/Data/Factions/Flood_Standard.asset");
            // The Infected Pelican and the Infected Phantom belong to one sub-faction each since D157 (the owner): not the base roster.
            CollectionAssert.AreEquivalent(Flood.Select(r => r.name).Except(new[] { "Infected Pelican", "Infected Phantom" }), flood.baseRoster.Select(u => u.unitName));
        }
    }
}
