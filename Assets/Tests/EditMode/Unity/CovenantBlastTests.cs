// CovenantBlastTests.cs
// Tests that the Covenant's machines, and the Flood's infested ones, explode and burn light blue instead of orange, read from the
// game's own unit data, and that everyone else's still goes up in orange fire.
// Depends on: Halobellum.Presentation, Halobellum.Units, Halobellum.Core, NUnit, UnityEditor.

using Halobellum.Core;
using Halobellum.Presentation;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;

namespace Halobellum.Tests
{
    public class CovenantBlastTests
    {
        private static UnitStats Unit(string path) => AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{path}.asset").ToStats();

        [TestCase("Covenant/Ghost")]
        [TestCase("Covenant/Banshee")]
        [TestCase("Covenant/Wraith")]
        [TestCase("Covenant/Scarab")]
        [TestCase("Covenant/Vampire")]
        [TestCase("Flood/InfectedPhantom")]
        [TestCase("FloodCovenant/InfestedBanshee")]
        public void ACovenantMachine_ExplodesLightBlue(string path)
        {
            Assert.IsTrue(DeathEffects.ExplodesBlue(Unit(path)), path);
        }

        [TestCase("UNSC/Scorpion")]
        [TestCase("UNSC/Warthog")]
        [TestCase("UNSC/Pelican")]
        [TestCase("Flood/InfectedPelican")]
        [TestCase("Banished/Chopper")]
        [TestCase("Covenant/HunterPair")]
        [TestCase("Covenant/Elite")]
        public void EveryoneElse_KeepsOrangeFire(string path)
        {
            Assert.IsFalse(DeathEffects.ExplodesBlue(Unit(path)), path);
        }

        [Test]
        public void AnInfestedCopyOfACovenantMachine_StillExplodesBlue()
        {
            UnitStats ghost = Unit("Covenant/Ghost");
            UnitStats copy = Unit("Covenant/Ghost");
            copy.Name = "Some Infested Thing";
            copy.InfectedFrom = ghost;
            Assert.IsTrue(DeathEffects.ExplodesBlue(copy));
            Assert.IsFalse(DeathEffects.ExplodesBlue(null));
        }
    }
}
