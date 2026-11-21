// SubFactionPriceTests.cs
// D158 (the owner): "Hunter pair should be 400c only for lekgolo colony". A sub-faction can buy a base roster unit at its own price: its
// roster has one copy of the unit at that price, the same copy every time; other sub-factions keep the unit as it is. Also D158: every shot
// of the Spartan Sniper's magazine is heard.

using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;

namespace Halobellum.Tests
{
    public class SubFactionPriceTests
    {
        private static FactionInfo Covenant() =>
            AssetDatabase.LoadAssetAtPath<FactionDefinition>("Assets/_Project/Data/Factions/Covenant_Standard.asset").ToInfo(new System.Collections.Generic.Dictionary<UnitDefinition, UnitStats>());

        [Test]
        public void TheLekgoloColony_BuysTheHunterPairAt400_OthersAt450()
        {
            FactionInfo covenant = Covenant();
            SubFactionInfo lekgolo = covenant.SubFactions.Single(s => s.Name == "Lekgolo Colony");
            SubFactionInfo banished = covenant.SubFactions.Single(s => s.Name == "Banished");
            UnitStats lekgoloPair = covenant.RosterFor(lekgolo).Single(u => u.Name == "Hunter Pair");
            UnitStats banishedPair = covenant.RosterFor(banished).Single(u => u.Name == "Hunter Pair");
            Assert.AreEqual(400, lekgoloPair.Cost);
            Assert.AreEqual(450, banishedPair.Cost);
            Assert.AreSame(lekgoloPair, covenant.RosterFor(lekgolo).Single(u => u.Name == "Hunter Pair"), "One copy, the same every time the roster is asked for.");
            Assert.AreEqual(banishedPair.MaxHealth, lekgoloPair.MaxHealth, "Only its price differs.");
            Assert.AreSame(banishedPair.Weapons, lekgoloPair.Weapons);
        }

        [Test]
        public void ASubFactionWithNoPrices_KeepsTheBaseUnitsThemselves()
        {
            FactionInfo covenant = Covenant();
            SubFactionInfo unggoy = covenant.SubFactions.Single(s => s.Name == "Unggoy Rebellion");
            foreach (UnitStats unit in covenant.BaseRoster) CollectionAssert.Contains(covenant.RosterFor(unggoy), unit);
        }

        [Test]
        public void EverySniperShot_FreesItsVoiceSoon_SoTheNextIsHeard()
        {
            Assert.Greater(AudioCatalog.FireSniper.HoldSeconds, 0f);
            Assert.Less(AudioCatalog.FireSniper.HoldSeconds, 0.5f, "Shorter than the 0.5 s between its shots.");
        }
    }
}
