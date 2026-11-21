// SubFactionRosterTests.cs
// Checks the 8 sub-factions on the real assets (M10, Bible Section 8.4): each has exactly its own exclusive units, listed in its
// faction's asset; prices follow the Bible's tier bands and unlock table; every unit has valid data, techs and a prefab; names
// don't clash with what the simulation calls a body that rises; the abilities that make each unit what it is are set up; and
// a real draw gives 5 different sub-factions with 2 Tier 1 squads each.
// Needs the Unity editor (AssetDatabase), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Units, Halobellum.EditorTools (HeadlessRunnerTool), Halobellum.Economy, Halobellum.Core, NUnit, UnityEditor.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.EditorTools;
using Halobellum.UI;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class SubFactionRosterTests
    {
        // Faction asset, sub-faction, folder under Data/Units, and the units the Bible lists (Flood: infested variants plus pure forms).
        private static readonly (string faction, string sub, string folder, string[] units)[] Table =
        {
            ("UNSC_Standard", "Noble", "Noble", new[] { "Spartan Sniper", "Spartan Heavy Weapons", "Spartan Grenadier", "Spartan CQB", "Spartan Fireteam" }),
            ("UNSC_Standard", "Armored Division", "ArmoredDivision", new[] { "Grizzly", "Cobra", "Rhino", "Gremlin" }),
            ("UNSC_Standard", "Aerospace Command", "AerospaceCommand", new[] { "Sparrowhawk", "Vulture", "Longsword", "Condor Gunship" }),
            ("Covenant_Standard", "Banished", "Banished", new[] { "Chopper", "Marauder", "Reaver", "Brute Grenadier" }),
            ("Covenant_Standard", "Lekgolo Colony", "LekgoloColony", new[] { "Goliath", "Skitterer", "Hunter Captain" }),
            ("Covenant_Standard", "Unggoy Rebellion", "UnggoyRebellion", new[] { "Cannon Fodder", "Heavy Grunts", "Grunt Goblin" }),
            ("Flood_Standard", "UNSC-based", "FloodUNSC", new[] { "Infested Marine", "Infested Warthog", "Infested Scorpion", "Infested Hornet", "Juggernaut" }),
            ("Flood_Standard", "Covenant-based", "FloodCovenant", new[] { "Infested Ghost", "Infested Elite", "Infested Wraith", "Infested Banshee", "Spawner" }),
        };

        private static FactionDefinition Faction(string asset) => AssetDatabase.LoadAssetAtPath<FactionDefinition>($"Assets/_Project/Data/Factions/{asset}.asset");

        [TestCase("UNSC_Standard")]
        [TestCase("Covenant_Standard")]
        [TestCase("Flood_Standard")]
        public void EveryShopRow_FitsOnOneLine_InItsFactionsFont(string asset)
        {
            FactionDefinition faction = Faction(asset);
            FactionInfo info = faction.ToInfo(new Dictionary<UnitDefinition, UnitStats>());
            Assert.IsNotNull(faction.uiTheme.fontAsset);
            var style = new GUIStyle { font = faction.uiTheme.fontAsset, fontSize = Mathf.RoundToInt(MatchHud.ShopLineLeastSize * faction.uiTheme.fontScale), wordWrap = false };
            string widest = null;
            float widestWidth = 0f;
            void Check(UnitStats unit, bool exclusive)
            {
                string line = MatchHud.ShopLine(unit, exclusive);
                float needed = style.CalcSize(new GUIContent(line)).x;
                if (needed > widestWidth) { widestWidth = needed; widest = line; }
            }
            foreach (UnitStats unit in info.BaseRoster) Check(unit, false);
            foreach (SubFactionInfo sub in info.SubFactions)
            {
                foreach (UnitStats unit in sub.ExclusiveUnits) Check(unit, true);
            }
            Assert.Greater(widestWidth, 0f, "The font measured something.");
            TestContext.WriteLine($"{asset}: widest is {widest}, {widestWidth} of {MatchHud.ShopTextWidth}");
            Assert.LessOrEqual(widestWidth, MatchHud.ShopTextWidth, widest);
        }

        private static List<UnitDefinition> Exclusive() => Table.SelectMany(row => HeadlessRunnerTool.LoadUnitsInFolder("Assets/_Project/Data/Units/" + row.folder)).ToList();

        private static Dictionary<string, UnitStats> Stats() => HeadlessRunnerTool.LoadSliceUnits().ToDictionary(u => u.unitName, u => u.ToStats());

        [Test]
        public void EveryFaction_ListsItsSubFactions_WithExactlyTheirUnits()
        {
            foreach (var row in Table)
            {
                FactionDefinition faction = Faction(row.faction);
                FactionDefinition.SubFactionEntry entry = faction.subFactions.SingleOrDefault(s => s.subFactionName == row.sub);
                Assert.IsNotNull(entry, row.sub);
                Assert.IsNotEmpty(entry.description, row.sub + " has a line for the draft card.");
                // Base Flood units that one sub-faction alone has (the owner, D157): their files stay in the Flood folder.
                string[] alsoExclusive = row.sub == "UNSC-based" ? new[] { "Infected Pelican" } : row.sub == "Covenant-based" ? new[] { "Infected Phantom" } : new string[0];
                CollectionAssert.AreEquivalent(row.units.Concat(alsoExclusive), entry.exclusiveUnits.Select(u => u.unitName), row.sub);
                CollectionAssert.AreEquivalent(row.units, HeadlessRunnerTool.LoadUnitsInFolder("Assets/_Project/Data/Units/" + row.folder).Select(u => u.unitName), row.sub + "'s folder");
            }
            foreach (string asset in new[] { "UNSC_Standard", "Covenant_Standard", "Flood_Standard" })
                Assert.IsFalse(Faction(asset).subFactions.Any(s => s.subFactionName == "Standard"), "The temporary Standard cards are gone.");
            Assert.AreEqual(3, Faction("UNSC_Standard").subFactions.Count);
            Assert.AreEqual(3, Faction("Covenant_Standard").subFactions.Count);
            Assert.AreEqual(2, Faction("Flood_Standard").subFactions.Count);
        }

        [Test]
        public void Prices_FollowTheBible()
        {
            foreach (UnitDefinition unit in Exclusive())
            {
                string name = unit.unitName;
                switch (unit.tier)
                {
                    case 1:
                        Assert.AreEqual(0, unit.unlockCost, name);
                        Assert.That(unit.cost, Is.InRange(100, 200), name);
                        break;
                    case 2:
                        Assert.AreEqual(50, unit.unlockCost, name);
                        Assert.That(unit.cost, Is.InRange(200, 300), name);
                        break;
                    case 3:
                        // Half its price to unlock; the Spartan Grenadier keeps its 200 at a price of 350 (the owner asked for the price only, D162).
                        Assert.AreEqual(name == "Spartan Grenadier" ? 200 : unit.cost / 2, unit.unlockCost, name);
                        // The Spartan CQB costs 300, under the band: the owner's choice once it could no longer shoot aircraft (D133); the Spartan Grenadier 350 (D162).
                        Assert.That(unit.cost, Is.InRange(name == "Spartan CQB" ? 300 : name == "Spartan Grenadier" ? 350 : 400, 500), name);
                        break;
                    default:
                        Assert.AreEqual(4, unit.tier, name);
                        Assert.AreEqual(350, unit.unlockCost, name);
                        Assert.AreEqual(800, unit.cost, name);
                        break;
                }
            }
            foreach (UnitDefinition spartan in HeadlessRunnerTool.LoadUnitsInFolder("Assets/_Project/Data/Units/Noble"))
                Assert.AreEqual(3, spartan.tier, spartan.unitName + ": Noble is expensive to unlock.");
        }

        [Test]
        public void EveryUnit_HasValidData_TechsAPrefabAndNotes()
        {
            var names = new HashSet<string>();
            foreach (UnitDefinition unit in Exclusive())
            {
                string name = unit.unitName;
                Assert.IsTrue(names.Add(name), name + " is there twice.");
                List<string> problems = unit.ToStats().Validate();
                Assert.IsEmpty(problems, string.Join("\n", problems));
                Assert.IsNotNull(unit.prefab, name + " needs a prefab, even a placeholder.");
                StringAssert.Contains("Best counters", unit.notes, name + ": Bible Section 7.");
                Assert.That(unit.techUpgrades.Count, Is.InRange(3, UnitUpgrades.MaxTechsPerUnit), name); // A fourth on some (D114).
                Assert.IsFalse(name.StartsWith("Infected "), name + ": that is what the simulation calls a body that rises, so the two would be mixed up.");
                if (name != "Goliath") Assert.AreEqual(unit.cost / (float)unit.squadSize / 5f, unit.baseDamage, 1e-3f, name);
            }
            Assert.AreEqual(33, names.Count); // No Infested Grunt (D116).
        }

        [Test]
        public void TheAbilities_AreSetUp()
        {
            Dictionary<string, UnitStats> u = Stats();
            foreach (string spartan in Table[0].units)
            {
                Assert.IsTrue(u[spartan].HasShield, spartan);
                Assert.IsTrue(u[spartan].ImmuneToInfection, spartan + ": Spartans cannot be infected.");
                // Light armor under the Heavy Weapons' shield (the owner, D160); the other Spartans are unarmored under theirs.
                Assert.AreEqual(spartan == "Spartan Heavy Weapons" ? ArmorType.Light : ArmorType.Unarmored, u[spartan].ArmorType, spartan);
            }
            Assert.AreEqual(0f, u["Spartan CQB"].LeapRange, "No leap: it stops at its range and shoots (the owner, D130).");
            Assert.IsFalse(u["Spartan CQB"].CanTarget(true), "A shotgun can't reach aircraft (the owner, D133).");
            Assert.IsTrue(u["Spartan CQB"].CanTarget(false));
            Assert.AreEqual(3, u["Spartan Fireteam"].SquadSize);
            Assert.Greater(u["Spartan Sniper"].Weapons[0].Range, u["Spartan"].Weapons[0].Range);

            Assert.Greater(u["Gremlin"].Weapons[0].HitSlowSeconds, 0f, "Its EMP.");
            Assert.IsTrue(u["Cobra"].Weapons[0].IsAntiArmor);
            Assert.IsTrue(u["Rhino"].Weapons[0].Lobbed);
            Assert.Greater(u["Rhino"].Weapons[0].Range, u["Wraith"].Weapons[0].Range, "The longest gun.");
            Assert.AreEqual(2, u["Grizzly"].Weapons.Length);

            foreach (string flyer in Table[2].units) Assert.IsTrue(u[flyer].IsAir, flyer);
            Assert.IsFalse(u["Longsword"].CanTarget(true), "A bomber cannot fight aircraft.");
            Assert.IsNull(u["Condor Gunship"].Reinforcement, "It drops no Marines any more (D111).");
            Assert.AreEqual(5, u["Condor Gunship"].Weapons.Count(w => w.Name.Contains("Spartan Laser")), "Five light Spartan Lasers, one a gun pod.");
            foreach (WeaponStats laser in u["Condor Gunship"].Weapons.Where(w => w.Name.Contains("Spartan Laser")))
                Assert.AreEqual(60f, laser.DamagePerShot, 1e-3f, laser.Name + ": 60 each, so it deals like the other 500-credit units (19.5 until D142).");

            Assert.Less(u["Chopper"].Weapons[0].Range, 3f, "It rams: its main weapon is melee.");
            Assert.Greater(u["Reaver"].Weapons[0].AirDamageMultiplier, 1f);
            Assert.IsTrue(u["Brute Grenadier"].Weapons[0].Lobbed);

            Assert.AreEqual(250f, u["Goliath"].BaseDamage, 1e-3f, "A building-killer.");
            Assert.IsTrue(u["Goliath"].HasDefenseArc);
            // Melee only, with a charge like the Hunters' (the owner, D142; a Fuel Rod Cannon and a 120 slam until D142).
            Assert.AreEqual(1, u["Goliath"].Weapons.Length, "Melee only (the owner, D142).");
            Assert.AreEqual("Fist Slam", u["Goliath"].Weapons[0].Name);
            Assert.AreEqual(DamageType.Melee, u["Goliath"].Weapons[0].DamageType);
            Assert.AreEqual(240f, u["Goliath"].Weapons[0].DamagePerShot, 1e-3f, "Doubled to make up for the cannon.");
            Assert.LessOrEqual(u["Goliath"].Weapons[0].Range, UnitUpgrades.MeleeRange);
            Assert.IsTrue(u["Goliath"].LeapCharges, "It charges like the Hunters.");
            Assert.Greater(u["Goliath"].LeapRange, u["Goliath"].Weapons[0].Range, "A charge starts beyond the fist's reach.");
            Assert.GreaterOrEqual(u["Goliath"].LeapRange - u["Goliath"].CollisionRadius, 3.5f, "Its charge covers about what a Hunter's does beyond its body (5 - 1.2 = 3.8 m).");
            Assert.IsFalse(u["Goliath"].CanTarget(true), "Fists can't reach aircraft.");
            Assert.Greater(u["Hunter Captain"].LifestealShare, 0f, "Its siphon beam.");
            Assert.Greater(u["Hunter Captain"].AuraHealPerSecondShare, 0f);

            Assert.AreEqual(30, u["Cannon Fodder"].SquadSize, "The largest squad.");
            Assert.Greater(u["Grunt Goblin"].AuraSpeedMultiplier, 1f);

            foreach (string flood in Table[6].units.Concat(Table[7].units)) Assert.IsTrue(u[flood].IsFlood, flood);
            foreach (string flood in Table[6].units.Concat(Table[7].units)) Assert.AreNotEqual(4, u[flood].Tier, flood + ": the Abomination is the Flood's only Titan.");
            Assert.AreEqual("Infection Forms", u["Spawner"].SpawnedUnit.Name);
            foreach (string flood in Table[6].units.Concat(Table[7].units)) Assert.IsFalse(u[flood].Infects, flood + ": only Infection Forms infect.");
        }

        /// <summary>The Skitterer's spitter reaches 12 m (7 until D142), a ranged gun, and its Long Range tech still adds 8 m.</summary>
        [Test]
        public void Skitterer_ShootsFrom12Metres_AsARangedGun()
        {
            UnitStats s = Stats()["Skitterer"];
            Assert.AreEqual(1, s.Weapons.Length);
            WeaponStats spit = s.Weapons[0];
            Assert.AreEqual(12f, spit.Range, 1e-3f, "The owner, D142.");
            Assert.Greater(spit.Range, UnitUpgrades.MeleeRange, "A gun, not a melee weapon.");
            Assert.AreNotEqual(DamageType.Melee, spit.DamageType);
            Assert.IsTrue(spit.IsInstant);
            TechUpgrade longRange = s.TechUpgrades.SingleOrDefault(t => t.Name == "Long Range");
            Assert.IsNotNull(longRange, "Its Long Range tech is kept.");
            Assert.AreEqual(20f, UnitUpgrades.Apply(s, 1, new[] { longRange }).Weapons[0].Range, 1e-3f);
        }

        /// <summary>The Spartan Heavy Weapons' chaingun 15 -> 13, as it grew too strong with levels (the owner, D142).</summary>
        [Test]
        public void SpartanHeavyWeapons_ChaingunCutForLevels()
        {
            UnitStats h = Stats()["Spartan Heavy Weapons"];
            Assert.AreEqual(14f, h.Weapons[0].DamagePerShot, 1e-3f, "15 until D142, 13 until D163.");
            UnitStats top = UnitUpgrades.Apply(h, 5, null);
            Assert.AreEqual(70f, top.Weapons[0].DamagePerShot, 1e-3f, "Level 5 multiplies its damage by 5.");
            Assert.AreEqual(250f, top.MaxShield, 1e-3f);
            Assert.AreEqual(5f * 1930f, top.MaxHealth, 1e-3f);
        }

        [Test]
        public void RealDraws_GiveFiveDifferentSubFactions_WithTierOneSquads_AndOfferExclusives()
        {
            var cache = new Dictionary<UnitDefinition, UnitStats>();
            var infos = new[] { "UNSC_Standard", "Covenant_Standard", "Flood_Standard" }.Select(a => Faction(a).ToInfo(cache)).ToList();
            var seen = new HashSet<string>();
            bool offeredExclusive = false, factionMissing = false;
            for (int seed = 1; seed <= 200; seed++)
            {
                List<DraftCard> cards = StartingDraft.Draw(infos, TestMatch.Rules(), new SimRandom(seed));
                Assert.AreEqual(5, cards.Count);
                Assert.AreEqual(5, cards.Select(c => c.SubFaction).Distinct().Count(), "The same sub-faction never twice in a draw.");
                if (cards.Select(c => c.Faction).Distinct().Count() < 3) factionMissing = true;
                foreach (DraftCard card in cards)
                {
                    seen.Add(card.SubFaction.Name);
                    Assert.AreEqual(2, card.Squads.Count);
                    Assert.AreEqual(2, card.Squads.Distinct().Count());
                    List<UnitStats> roster = card.Faction.RosterFor(card.SubFaction);
                    foreach (UnitStats squad in card.Squads)
                    {
                        Assert.AreEqual(1, squad.Tier, squad.Name);
                        CollectionAssert.Contains(roster, squad);
                        if (card.SubFaction.ExclusiveUnits.Contains(squad)) offeredExclusive = true;
                    }
                }
            }
            Assert.AreEqual(8, seen.Count, "All 8 appear in time.");
            Assert.IsTrue(offeredExclusive);
            Assert.IsTrue(factionMissing, "A faction may not appear in a draw at all (Bible Section 5).");
        }
    }
}
