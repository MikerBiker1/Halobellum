// OwnerSoundsD91Tests.cs
// The owner's third batch of sounds (D91): each kind of soldier dies with its own sound, the Flood by size, a Flood form's leap and a Hunter's
// charge have theirs, the Hunters' bash and the Hunter Captain's laser theirs, and moving an item into a hexagon has its own UI sound. The
// Captain's laser is a laser held as a beam: green, not the Scarab's beam.
// Depends on: Halobellum.Presentation (AudioCatalog, BattleAudio, AudioSlots, WeaponFeel, WeaponLooks), Halobellum.Units, NUnit, UnityEditor.

using Halobellum.Core;
using Halobellum.Presentation;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;

namespace Halobellum.Tests
{
    public class OwnerSoundsD91Tests
    {
        private static UnitStats Unit(string path) => AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{path}.asset").ToStats();

        [TestCase("UNSC/Marine", "sfx_death_human")]
        [TestCase("UNSC/FlamethrowerTrooper", "sfx_death_human")]
        [TestCase("Noble/SpartanSniper", "sfx_death_spartan")]
        [TestCase("UNSC/Spartan", "sfx_death_spartan")]
        [TestCase("Covenant/Elite", "sfx_death_elite")]
        [TestCase("Covenant/Zealot", "sfx_death_elite")]
        [TestCase("Covenant/Grunt", "sfx_death_grunt")]
        [TestCase("UnggoyRebellion/CannonFodder", "sfx_death_grunt")]
        [TestCase("Covenant/JackalShield", "sfx_death_jackal")]
        [TestCase("Banished/BruteGrenadier", "sfx_death_brute")]
        public void EachKindOfSoldier_DiesWithItsOwnSound(string unit, string slot)
        {
            SfxCue cue = BattleAudio.OwnDeath(Unit(unit));
            Assert.IsNotNull(cue);
            Assert.AreEqual(slot, cue.Slot);
            AudioLibrary.Forget();
            cue.Forget();
            Assert.IsTrue(cue.HasSound, "The owner's file is in.");
        }

        [Test]
        public void AGruntInAWalker_GoesUpAsAMachine() => Assert.IsNull(BattleAudio.OwnDeath(Unit("UnggoyRebellion/GruntGoblin")));

        [Test]
        public void TheNewSlots_AllHaveTheOwnersFile()
        {
            AudioLibrary.Forget();
            AudioCatalog.ForgetClips();
            foreach (SfxCue cue in new[] { AudioCatalog.DeathFloodSmall, AudioCatalog.DeathFloodBig, AudioCatalog.DeathFloodHuge, AudioCatalog.FloodLeap,
                         AudioCatalog.HunterCharge, AudioCatalog.FireHunterBash, AudioCatalog.FireSiphon, AudioCatalog.DeathLekgolo })
                Assert.IsTrue(cue.HasSound && cue.Clips.IsOverride, cue.Slot);
            Assert.AreEqual(3, AudioLibrary.Get("sfx_fire_melee_flood").Clips.Length, "Three Flood blows, one at random.");
            Assert.IsTrue(AudioLibrary.Get("sfx_fire_melee_flood").IsOverride);
            foreach (string faction in new[] { "UNSC", "Covenant", "Flood" })
                Assert.IsTrue(AudioLibrary.Get(AudioSlots.Ui(faction, UiSound.ItemPlaced)).IsOverride, "Moving an item into a hexagon, " + faction);
        }

        [Test]
        public void Leaps_SoundAsAChargeOrAFloodPounce()
        {
            Assert.AreSame(AudioCatalog.HunterCharge, BattleAudio.LeapCue(Unit("Covenant/HunterPair")));
            Assert.AreSame(AudioCatalog.HunterCharge, BattleAudio.LeapCue(Unit("LekgoloColony/HunterCaptain")));
            Assert.AreSame(AudioCatalog.FloodLeap, BattleAudio.LeapCue(Unit("Flood/StalkerForm")));
            Assert.IsNull(BattleAudio.LeapCue(Unit("Covenant/Zealot")), "A Zealot leaps without a sound of its own.");
        }

        [Test]
        public void TheCaptainsLaser_IsAGreenLaserBeam_WithItsOwnSound()
        {
            UnitStats captain = Unit("LekgoloColony/HunterCaptain");
            WeaponStats laser = captain.Weapons[0];
            Assert.IsTrue(laser.IsBeam);
            Assert.AreEqual(1f, laser.BeamSeconds, 0.001f, "A 1 s beam.");
            Assert.AreEqual(WeaponFeelKind.Laser, WeaponFeel.For(laser), "A laser, not the Scarab's beam.");
            Assert.AreSame(AudioCatalog.FireSiphon, AudioCatalog.OwnFire(laser, captain));
            WeaponLook look = WeaponLooks.For(laser, captain, "Covenant", Team.A, false);
            Assert.Greater(look.Shot.g, 0.9f);
            Assert.Less(look.Shot.r, 0.5f, "Green.");
            Assert.AreSame(AudioCatalog.FireHunterBash, AudioCatalog.OwnFire(captain.Weapons[1], captain));
            Assert.AreEqual(WeaponFeelKind.Beam, WeaponFeel.For(Unit("Covenant/Scarab").Weapons[0]), "The Scarab's is still its beam.");
        }
    }
}
