// AudioOwnerSoundsTests.cs
// Tests the owner's sound effects (D70, D71): each weapon or unit with a sound of its own gets it, read from the game's own data, and the
// rest keep their kind's; the M60 is every machine gun and chaingun, the autocannons have their own; the guns that fire fast loop; a UI
// file named without a faction serves every faction; the owner's files are turned down to their slot's loudness, and the generated ones
// play as they are; the Glassing Beam starts ahead of its strike; the nodes of light land by size; a drawn round has its stinger.
// Depends on: Halobellum.Presentation, Halobellum.Combat, Halobellum.Core, Halobellum.Units, NUnit, UnityEditor.

using Halobellum.Core;
using Halobellum.Presentation;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class AudioOwnerSoundsTests
    {
        private static UnitStats Unit(string path) => AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{path}.asset").ToStats();

        private static WeaponStats WeaponOf(UnitStats unit, string weaponName)
        {
            foreach (WeaponStats weapon in unit.Weapons)
            {
                if (weapon.Name == weaponName) return weapon;
            }
            Assert.Fail($"{unit.Name} has no {weaponName}");
            return null;
        }

        private static SfxCue Fire(string path, string weaponName, string faction = BattleFactions.Unsc)
        {
            UnitStats unit = Unit(path);
            return AudioCatalog.FireFor(WeaponOf(unit, weaponName), unit, faction);
        }

        [SetUp]
        public void SetUp() => AudioLibrary.Forget();

        [TestCase("UNSC/Marine", "MA5 Assault Rifle", "sfx_fire_assaultrifle")]
        [TestCase("FloodUNSC/InfestedMarine", "Scavenged Rifle", "sfx_fire_assaultrifle")]
        [TestCase("SpellUnits/ODST", "M7 SMG", "sfx_fire_smg")]
        [TestCase("UNSC/Warthog", "M41 Machine Gun", "sfx_fire_m60")]
        [TestCase("UNSC/Scorpion", "Coaxial Machine Gun", "sfx_fire_m60")]
        [TestCase("ArmoredDivision/Grizzly", "Coaxial Machine Gun", "sfx_fire_m60")]
        [TestCase("UNSC/Mantis", "Chaingun", "sfx_fire_m60")]
        [TestCase("Noble/SpartanHeavyWeapons", "M247H Chaingun", "sfx_fire_m60")]
        [TestCase("AerospaceCommand/Vulture", "Chin Gun", "sfx_fire_m60")]
        [TestCase("UNSC/Elephant", "Turret (left)", "sfx_fire_m60")]
        [TestCase("Banished/Marauder", "Spike Turret", "sfx_fire_m60")]
        [TestCase("FloodUNSC/InfestedWarthog", "Scavenged Machine Gun", "sfx_fire_m60")]
        [TestCase("UNSC/Pelican", "M370 Autocannon", "sfx_fire_autocannon")]
        [TestCase("AerospaceCommand/Sparrowhawk", "Heavy Autocannon", "sfx_fire_autocannon")]
        [TestCase("AerospaceCommand/CondorGunship", "Twin Autocannons", "sfx_fire_autocannon")]
        [TestCase("Banished/Chopper", "Chopper Autocannons", "sfx_fire_autocannon")]
        [TestCase("Flood/InfectedPhantom", "Plasma Turrets", "sfx_fire_plasmaturret")]
        [TestCase("Covenant/Scarab", "Plasma Turret (left)", "sfx_fire_plasmaturret")]
        [TestCase("Covenant/Scarab", "Plasma Turret (right)", "sfx_fire_plasmaturret")]
        [TestCase("Banished/Marauder", "Plasma Cannon", "sfx_fire_plasmaheavy")]
        [TestCase("Covenant/Ghost", "Plasma Bolts", "sfx_fire_ghost")]
        [TestCase("Covenant/Banshee", "Plasma Cannons", "sfx_fire_banshee")]
        [TestCase("Covenant/Banshee", "Fuel Rod Bomb", "sfx_fire_banshee_bomb")]
        [TestCase("UnggoyRebellion/GruntGoblin", "Goblin Plasma Cannons", "sfx_fire_plasma")]
        [TestCase("Covenant/FuelRodGrunt", "Fuel Rod Gun", "sfx_fire_fuelrod")]
        [TestCase("Covenant/HunterPair", "Assault Cannon", "sfx_fire_hunter")]
        [TestCase("Covenant/Wraith", "Plasma Mortar", "sfx_fire_mortar")]
        [TestCase("Noble/SpartanCQB", "M45 Shotgun", "sfx_fire_shotgun")]
        [TestCase("Noble/SpartanSniper", "SRS99 Sniper Rifle", "sfx_fire_sniper")]
        [TestCase("Noble/SpartanFireteam", "BR85 Battle Rifle", "sfx_fire_br85")]
        [TestCase("Covenant/Zealot", "Energy Sword", "sfx_fire_sword")]
        [TestCase("Covenant/Grunt", "Plasma Pistol", "sfx_fire_plasma")]
        [TestCase("UNSC/Scorpion", "M512 Cannon", "sfx_fire_cannon")]
        public void EachWeapon_HasItsOwnSound_OrItsKinds(string path, string weaponName, string slot)
        {
            Assert.AreEqual(slot, Fire(path, weaponName, path.StartsWith("Covenant") || path.StartsWith("Unggoy") ? BattleFactions.Covenant : BattleFactions.Unsc).Slot);
        }

        [Test]
        public void ThePhantomsTurrets_AndHeavyPlasma_AreTheOwnersPhantomFile()
        {
            SfxCue heavy = AudioCatalog.ForSlot("sfx_fire_plasmaheavy");
            Assert.IsTrue(heavy.Clips.IsOverride, "The Phantom's turret fire is the heavy plasma sound now.");
            Assert.AreEqual(heavy.Clips.Clips[0].length, AudioCatalog.FirePlasmaTurret.Clips.Clips[0].length, 1e-3f, "The same file.");
            Assert.AreNotEqual(heavy.Group, AudioCatalog.FirePlasmaTurret.Group, "Fast turrets mustn't take the fuel rods' voices.");
            var phantom = new UnitStats { Name = "Phantom" };
            Assert.AreEqual("sfx_fire_plasmaturret", AudioCatalog.FireFor(new WeaponStats { Name = "Plasma Turrets", Range = 14f, FireInterval = 0.3f }, phantom, BattleFactions.Covenant).Slot, "The Covenant's Phantom, by its weapon's name.");
        }

        [Test]
        public void TheBeamRifle_HasItsOwnSound_AndIsAShotNotTheScarabsBeam()
        {
            var rifle = new WeaponStats { Name = "Beam Rifle", Range = 30f, FireInterval = 2.5f, DamagePerShot = 60f };
            var jackal = new UnitStats { Name = "Jackal (Beam Rifle)" };
            Assert.AreEqual(WeaponFeelKind.Gauss, WeaponFeel.For(rifle), "One hard shot in a straight line.");
            SfxCue cue = AudioCatalog.FireFor(rifle, jackal, BattleFactions.Covenant);
            Assert.AreEqual("sfx_fire_beamrifle", cue.Slot);
            Assert.IsTrue(cue.Clips.IsOverride);
            Assert.IsFalse(cue.Loops);
            Assert.IsNull(AudioCatalog.LandingFor(rifle));
        }

        [Test]
        public void TheBr85sLongEcho_HoldsAVoiceOnlyForItsBurst()
        {
            Assert.Greater(AudioCatalog.FireBr85.Clips.Clips[0].length, 2f, "The owner's file rings for almost 3 s.");
            Assert.Greater(AudioCatalog.FireBr85.HoldSeconds, 0f);
            Assert.Less(AudioCatalog.FireBr85.HoldSeconds, 0.6f, "Under the Spartans' 0.6 s between shots.");
        }

        [Test]
        public void TheGlassingBeam_StartsAheadOfItsStrike_AndNothingElseDoes()
        {
            SpellStats glassing = AssetDatabase.LoadAssetAtPath<SpellDefinition>("Assets/_Project/Data/Spells/Covenant/GlassingBeam.asset").ToStats();
            SfxCue cue = AudioCatalog.ForSlot("sfx_spell_glassing");
            Assert.IsTrue(cue.Clips.IsOverride);
            Assert.AreEqual(AudioCatalog.GlassingStrikeSeconds, AudioCatalog.SpellLeadSeconds(glassing));
            Assert.LessOrEqual(AudioCatalog.GlassingStrikeSeconds, glassing.DelaySeconds, "The build-up fits in the wait before the beam.");
            Assert.Greater(cue.Clips.Clips[0].length, AudioCatalog.GlassingStrikeSeconds + glassing.Ticks * glassing.TickInterval, "The file outlasts the beam: it fades as the beam goes.");
            Assert.AreEqual(0f, AudioCatalog.SpellLeadSeconds(new SpellStats { Name = "MAC Strike", Effect = SpellEffect.Damage, Ticks = 1 }));
            Assert.AreEqual(0f, AudioCatalog.SpellLeadSeconds(new SpellStats { Name = "Plasma Storm", Effect = SpellEffect.Damage, Ticks = 6 }));
        }

        [Test]
        public void TheGlassingBeamsSound_StartsSoTheBeamStrikesWithIt_AtAnySpeed_AndFadesAsTheBeamGoes()
        {
            var glassing = new SpellStats { Name = "Glassing Beam", Effect = SpellEffect.Damage, DelaySeconds = 6f, Ticks = 5, TickInterval = 0.6f };
            const float lead = 1.85f;
            Assert.IsFalse(BattleAudio.SpellAhead(glassing, lead, 4f, 1f, out _, out _), "2 s before the hit is too early.");
            Assert.IsTrue(BattleAudio.SpellAhead(glassing, lead, 4.2f, 1f, out float skip, out float fadeAfter));
            Assert.AreEqual(0.05f, skip, 1e-3f);
            Assert.AreEqual(1.8f + 3.1f, fadeAfter, 1e-3f, "Its hits take 3 s, then it fades.");

            Assert.IsFalse(BattleAudio.SpellAhead(glassing, lead, 2f, 2f, out _, out _), "At double speed, 4 s of battle are 2 s of sound.");
            Assert.IsTrue(BattleAudio.SpellAhead(glassing, lead, 2.4f, 2f, out skip, out fadeAfter));
            Assert.AreEqual(0.05f, skip, 1e-3f);
            Assert.AreEqual(1.8f + 1.55f, fadeAfter, 1e-3f);

            var quick = new SpellStats { Name = "Glassing Beam", Effect = SpellEffect.Damage, DelaySeconds = 1f, Ticks = 5, TickInterval = 0.6f };
            Assert.IsTrue(BattleAudio.SpellAhead(quick, lead, 0f, 1f, out skip, out _));
            Assert.AreEqual(0.85f, skip, 1e-3f, "Too little time for all the build-up: it starts part way in, still striking with the beam.");
            Assert.IsFalse(BattleAudio.SpellAhead(glassing, 0f, 6f, 1f, out _, out _), "No lead, nothing ahead.");
        }

        [Test]
        public void ANodeOfLight_LandsWithTheSmallOrTheBigSound_BySize()
        {
            Assert.AreEqual("sfx_node_small", AudioCatalog.NodeHit(RoundEnding.SizeOf(1f), false).Slot, "A Marine.");
            Assert.AreEqual("sfx_node_small", AudioCatalog.NodeHit(RoundEnding.SizeOf(45f), false).Slot, "A Hunter Pair is medium.");
            Assert.AreEqual("sfx_node_big", AudioCatalog.NodeHit(RoundEnding.SizeOf(100f), false).Slot, "A Grizzly.");
            Assert.AreEqual("sfx_node_big", AudioCatalog.NodeHit(0.3f, true).Slot, "A Titan is always big.");
        }

        [Test]
        public void ADrawnRound_HasItsStinger_AndThePelicansCrash_IsTheUnscAirCrash()
        {
            AudioClipSet draw = AudioLibrary.Get(AudioSlots.RoundDraw);
            Assert.IsTrue(draw.IsOverride && !draw.IsEmpty);
            AudioClipSet crash = AudioCatalog.PelicanCrash.Clips, air = AudioCatalog.DeathAirUnsc.Clips;
            Assert.IsTrue(crash.IsOverride);
            Assert.AreEqual(air.Clips[0].length, crash.Clips[0].length, 1e-3f, "The same file.");
        }

        [Test]
        public void TheGunsThatFireFast_Loop_AndTheOthersPlayOnceAShot()
        {
            Assert.IsTrue(Fire("SpellUnits/ODST", "M7 SMG").Loops);
            Assert.IsTrue(Fire("UNSC/Warthog", "M41 Machine Gun").Loops);
            Assert.IsTrue(Fire("Covenant/Ghost", "Plasma Bolts", BattleFactions.Covenant).Loops);
            Assert.IsTrue(Fire("UNSC/Pelican", "M370 Autocannon").Loops, "The owner's autocannon is a short burst: looped, it keeps up with the gun.");
            Assert.IsTrue(Fire("Noble/SpartanHeavyWeapons", "M247H Chaingun").Loops);
            Assert.IsFalse(Fire("Noble/SpartanFireteam", "BR85 Battle Rifle").Loops);
            Assert.IsFalse(Fire("UNSC/Marine", "MA5 Assault Rifle").Loops, "The owner's assault rifle is one shot.");
            Assert.IsFalse(Fire("UNSC/Scorpion", "M512 Cannon").Loops);
        }

        [Test]
        public void TheScarabsBeam_IsPlayedByItsPhases_NotAsOneShot()
        {
            Assert.IsNull(Fire("Covenant/Scarab", "Focus Beam", BattleFactions.Covenant), "BattleAudio plays charge, in, loop and out.");
            Assert.IsTrue(AudioCatalog.BeamLoop.Loops);
        }

        [Test]
        public void ALanding_HasTheWeaponsOwnExplosion()
        {
            UnitStats banshee = Unit("Covenant/Banshee"), grunt = Unit("Covenant/FuelRodGrunt"), scorpion = Unit("UNSC/Scorpion");
            Assert.AreEqual("sfx_explosion_banshee", AudioCatalog.LandingFor(WeaponOf(banshee, "Fuel Rod Bomb")).Slot);
            Assert.AreEqual("sfx_explosion_fuelrod", AudioCatalog.LandingFor(WeaponOf(grunt, "Fuel Rod Gun")).Slot);
            Assert.AreEqual(2, AudioCatalog.ExplosionFuelRod.Clips.Clips.Length, "Two fuel rod explosions to pick from.");
            Assert.AreEqual("sfx_explosion_small", AudioCatalog.LandingFor(WeaponOf(scorpion, "M512 Cannon")).Slot, "Now the owner's frag grenade.");
            Assert.IsNull(AudioCatalog.LandingFor(WeaponOf(banshee, "Plasma Cannons")));
        }

        [Test]
        public void EveryOwnersSound_IsIn()
        {
            foreach (SfxCue cue in AudioCatalog.OwnerOnly)
            {
                Assert.IsTrue(cue.HasSound, "No file for " + cue.Slot);
                Assert.IsTrue(cue.Clips.IsOverride);
            }
            Assert.AreEqual(4, AudioCatalog.InfectionRise.Clips.Clips.Length, "Each rising unit howls one of the four.");
            Assert.AreEqual(3, AudioCatalog.CallSeeder.Clips.Clips.Length);
            Assert.AreEqual(3, AudioCatalog.CallSwarm.Clips.Clips.Length);
        }

        [Test]
        public void AUiFileWithoutAFaction_ServesEveryFaction_AndTheNeutralSet()
        {
            foreach (string faction in new[] { "UNSC", "Covenant", "Flood", null })
            {
                AudioClipSet click = AudioLibrary.Get(AudioSlots.Ui(faction, UiSound.Click));
                Assert.IsTrue(click.IsOverride, $"{faction ?? "neutral"}: the owner's ui_click.");
                Assert.IsTrue(AudioLibrary.Get(AudioSlots.Ui(faction, UiSound.Error)).IsOverride);
            }
            Assert.IsFalse(AudioLibrary.Get(AudioSlots.Ui("UNSC", UiSound.Hover)).IsOverride, "No hover file: the generated one stays.");
            Assert.AreEqual("ui_click", AudioLibrary.SharedUiSlot("ui_covenant_click"));
            Assert.IsNull(AudioLibrary.SharedUiSlot("sfx_fire_bullet"));
        }

        [Test]
        public void TheOwnersFiles_AreTurnedToTheirSlotsLoudness_AndTheGeneratedOnesPlayAsTheyAre()
        {
            SfxCue rocket = AudioCatalog.ForSlot("sfx_fire_rocket");
            Assert.IsTrue(rocket.Clips.IsOverride);
            float gain = rocket.Clips.Gain(0, rocket.Loudness);
            Assert.Less(gain, 0.4f, "The owner's rocket is about 9 dB louder than the mix wants.");
            Assert.Greater(gain, 0.2f);

            SfxCue laser = AudioCatalog.ForSlot("sfx_fire_laser");
            Assert.IsFalse(laser.Clips.IsOverride);
            Assert.AreEqual(1f, laser.Clips.Gain(0, laser.Loudness), "A generated sound is already mixed.");

            AudioClip half = AudioClip.Create("Half", 4410, 1, 44100, false);
            try
            {
                var samples = new float[4410];
                for (int i = 0; i < samples.Length; i++) samples[i] = i % 2 == 0 ? 0.5f : -0.5f;
                half.SetData(samples, 0);
                Assert.AreEqual(-6.02f, AudioLibrary.Loudness(half), 0.05f, "Half of full scale is 6 dB down.");
            }
            finally
            {
                Object.DestroyImmediate(half);
            }
        }

        [Test]
        public void TheSpartanLaser_IsHeardAsItWindsUp_NotWhenItFires()
        {
            UnitStats spartan = Unit("UNSC/Spartan");
            WeaponStats laser = WeaponOf(spartan, "Spartan Laser");
            Assert.AreEqual(2.5f, laser.WindUpSeconds, 1e-4f);
            Assert.AreEqual("sfx_fire_spartanlaser", AudioCatalog.WindUpFor(laser).Slot);
            Assert.IsNull(AudioCatalog.FireFor(laser, spartan, BattleFactions.Unsc), "Its shot is in the sound of its wind-up.");
            Assert.AreEqual(0f, AudioCatalog.FireSpartanLaser.PitchSpread, "A wandering pitch would move the shot in the file.");
            UnitStats captain = Unit("LekgoloColony/HunterCaptain");
            foreach (WeaponStats weapon in captain.Weapons) Assert.IsNull(AudioCatalog.WindUpFor(weapon), "No wind-up, no wind-up sound.");
        }

        [Test]
        public void TheSpartanLasersFile_HasItsShotAboutTwoAndAHalfSecondsIn()
        {
            AudioClip clip = AudioCatalog.FireSpartanLaser.Clips.Clips[0];
            var samples = new float[clip.samples * clip.channels];
            Assert.IsTrue(clip.GetData(samples, 0));
            // The loudness of each 10 ms. The charge swells into the shot: the loudest is the shot, and the log also says where the swell reaches half of it.
            int window = Mathf.Max(1, clip.frequency / 100) * clip.channels;
            var loudness = new System.Collections.Generic.List<float>();
            for (int start = 0; start + window <= samples.Length; start += window)
            {
                float sum = 0f;
                for (int i = start; i < start + window; i++) sum += samples[i] * samples[i];
                loudness.Add(Mathf.Sqrt(sum / window));
            }
            int loudest = 0;
            for (int i = 1; i < loudness.Count; i++) if (loudness[i] > loudness[loudest]) loudest = i;
            int attack = loudest;
            while (attack > 0 && loudest - attack < 30 && loudness[attack - 1] >= loudness[loudest] * 0.5f) attack--;
            float shotAt = attack * 0.01f;
            var line = new System.Text.StringBuilder();
            for (int i = 0; i < loudness.Count; i += 10) line.Append($" {i * 0.01f:0.0}s:{20f * Mathf.Log10(Mathf.Max(1e-5f, loudness[i])):0}");
            Debug.Log($"Spartan Laser file: {clip.length:0.00} s long, its loudest 10 ms at {loudest * 0.01f:0.00} s, the shot's attack at {shotAt:0.00} s. dB every 0.1 s:{line}");
            Assert.AreEqual(2.5f, loudest * 0.01f, 0.2f, "The shot (the loudest moment, 2.42 s in the owner's file) should come with the wind-up's end.");
        }

        [Test]
        public void TheLasersBang_MeetsItsShot_AtEveryBattleSpeed()
        {
            Assert.AreEqual(0f, BattleAudio.WindUpSkip(2.5f, 1f), 1e-4f, "At 1x the file starts with the wind-up.");
            Assert.AreEqual(2.42f - 1.25f, BattleAudio.WindUpSkip(2.5f, 2f), 1e-4f, "At 2x the shot comes 1.25 s later: start that much before the bang.");
            Assert.AreEqual(2.42f - 0.625f, BattleAudio.WindUpSkip(2.5f, 4f), 1e-4f);
        }
    }
}
