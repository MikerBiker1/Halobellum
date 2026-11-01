// SpellAssetTests.cs
// Checks the spell card data (Milestone 7, decision D31): every faction has spells (more than one
// offer's worth), every spell is valid, the faction-only spells stay with their faction (ODST Drop
// Pods UNSC, Glassing Beam Covenant), high-impact spells can be owned once, buffs are defensive, and
// the ODST unit only exists for its spell (in no roster, out of the counter web folders).
// Needs the Unity editor (AssetDatabase), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Units, Halobellum.Core, NUnit, UnityEditor.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;

namespace Halobellum.Tests
{
    public class SpellAssetTests
    {
        private static FactionDefinition Faction(string asset) => AssetDatabase.LoadAssetAtPath<FactionDefinition>($"Assets/_Project/Data/Factions/{asset}.asset");

        private static List<SpellStats> Spells(string asset)
        {
            var cache = new Dictionary<UnitDefinition, UnitStats>();
            return Faction(asset).spells.Select(s => s.ToStats(cache)).ToList();
        }

        private static List<CardStats> Cards(string asset) =>
            Faction(asset).ToInfo(new Dictionary<UnitDefinition, UnitStats>()).Cards;

        [TestCase("UNSC_Standard")]
        [TestCase("Covenant_Standard")]
        public void EveryFaction_OffersItemsEconomyBuffsAndTraining(string asset)
        {
            List<CardStats> cards = Cards(asset);
            foreach (CardStats card in cards) CollectionAssert.IsEmpty(card.Validate(), card.Name);
            Assert.AreEqual(8, cards.Count(c => c.Type == CardType.Item), "The 8 items.");
            Assert.AreEqual(2, cards.Count(c => c.Type == CardType.Economy), "Supply Enhancement and Super Supply.");
            Assert.AreEqual(2, cards.Single(c => c.Type == CardType.Training).MaxOwnedCopies, "Intensive Training can be taken twice.");
            Assert.AreEqual(2f, cards.Single(c => c.Name == "Laser Sights").Item.RangeBonus, 1e-3f);
            Assert.AreEqual(4, cards.Count(c => c.Type == CardType.Buff));
            foreach (CardDefinition definition in Faction(asset).cards)
                Assert.IsNotNull(definition.icon, $"{definition.name} has an icon.");
            Assert.IsTrue(cards.Where(c => c.Type == CardType.Buff && c.ExtraSquadsPerRound == 0).All(c => c.MinRound >= 7), "Powerful buffs from round 7.");
            Assert.AreEqual(5, cards.Single(c => c.ExtraSquadsPerRound > 0).MinRound, "The extra slot from round 5.");
        }

        [Test]
        public void TheBigSpells_OnlyAppearFromRound7_WithALongDelay()
        {
            foreach (string name in new[] { "UNSC_Standard:HAVOK Nuke", "Covenant_Standard:Glassing Beam" })
            {
                string[] parts = name.Split(':');
                SpellStats spell = Spells(parts[0]).Single(s => s.Name == parts[1]);
                Assert.GreaterOrEqual(spell.MinRound, 7, spell.Name);
                Assert.AreEqual(6f, spell.DelaySeconds, 1e-3f, spell.Name);
            }
        }

        [Test]
        public void BothFactions_HaveSmokeAndEmp_OnlyTheCovenantHasTheBansheeAmbush()
        {
            foreach (string asset in new[] { "UNSC_Standard", "Covenant_Standard" })
            {
                List<SpellStats> spells = Spells(asset);
                SpellStats smoke = spells.Single(s => s.Effect == SpellEffect.Smoke);
                Assert.AreEqual(15f, smoke.AreaWidth, 1e-3f);
                Assert.AreEqual(4f, smoke.AreaDepth, 1e-3f);
                Assert.AreEqual(0f, smoke.DurationSeconds, "It lasts the whole round.");
                Assert.AreEqual(100, smoke.Cost);
                Assert.AreEqual(3, smoke.CooldownRounds);
                SpellStats emp = spells.Single(s => s.Effect == SpellEffect.Disrupt);
                Assert.AreEqual(10000f, emp.ShieldDamage, 1e-3f);
                Assert.AreEqual(2, emp.CooldownRounds);
            }
            SpellStats ambush = Spells("Covenant_Standard").Single(s => s.Name == "Banshee Ambush");
            Assert.AreEqual("Banshee", ambush.DropUnit.Name);
            Assert.AreEqual(2, ambush.DropSquads);
            CollectionAssert.DoesNotContain(Spells("UNSC_Standard").Select(s => s.Name).ToList(), "Banshee Ambush");
        }

        [TestCase("UNSC_Standard")]
        [TestCase("Covenant_Standard")]
        public void EveryFaction_HasValidSpells_MoreThanOneOffer(string asset)
        {
            FactionDefinition faction = Faction(asset);
            Assert.IsNotNull(faction);
            Assert.IsFalse(faction.spells.Any(s => s == null), "No empty spell slots.");
            List<SpellStats> spells = Spells(asset);
            Assert.Greater(spells.Count, 3, "More spells than one offer shows.");
            foreach (SpellStats spell in spells)
            {
                CollectionAssert.IsEmpty(spell.Validate(), spell.Name);
                if (spell.Effect == SpellEffect.Buff) Assert.IsFalse(spell.Offensive, $"{spell.Name}: buffs go on your own side.");
            }
            Assert.IsTrue(spells.Any(s => s.Offensive) && spells.Any(s => !s.Offensive), "Both offensive and defensive spells.");
            Assert.IsTrue(spells.Any(s => s.CooldownRounds >= 4 && s.MaxOwnedCopies == 1), "A slow, high-impact spell that can be owned once.");
            Assert.IsTrue(spells.Any(s => s.CooldownRounds <= 1 && s.MaxOwnedCopies > 1), "A quick, low-impact spell that can be owned several times.");
        }

        [Test]
        public void FactionSpells_StayWithTheirFaction()
        {
            List<string> unsc = Spells("UNSC_Standard").Select(s => s.Name).ToList();
            List<string> covenant = Spells("Covenant_Standard").Select(s => s.Name).ToList();
            CollectionAssert.Contains(unsc, "ODST Drop Pods");
            CollectionAssert.DoesNotContain(covenant, "ODST Drop Pods");
            CollectionAssert.Contains(covenant, "Glassing Beam");
            CollectionAssert.DoesNotContain(unsc, "Glassing Beam");
        }

        [Test]
        public void TheOdstUnit_OnlyLandsFromItsSpell()
        {
            SpellStats pods = Spells("UNSC_Standard").Single(s => s.Effect == SpellEffect.DropSquad);
            Assert.AreEqual("ODST", pods.DropUnit.Name);
            CollectionAssert.IsEmpty(pods.DropUnit.Validate());
            Assert.AreEqual(12, pods.DropUnit.SquadSize, "A squad of 12 (D33).");
            Assert.IsTrue(pods.DropUnit.SwitchesWeapons, "SMG or rocket launcher, by target.");
            foreach (string asset in new[] { "UNSC_Standard", "Covenant_Standard" })
            {
                Assert.IsFalse(Faction(asset).baseRoster.Any(u => u.unitName == "ODST"), "Not buyable.");
            }
            string path = AssetDatabase.GetAssetPath(Faction("UNSC_Standard").spells.First(s => s.effect == SpellEffect.DropSquad).dropUnit);
            StringAssert.StartsWith("Assets/_Project/Data/Units/SpellUnits/", path, "Kept out of the counter web's unit folders.");
        }
    }
}
