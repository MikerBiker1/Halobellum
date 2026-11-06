// AudioTests.cs
// The sound system without playing anything (M10): the voice limits, that every slot has a file behind it, which slot goes with
// what, that the owner's files replace the generated ones by just being dropped in (with a real file and a real import), and how
// the files are imported. How it sounds is for the owner's ears; the levels of the files are measured by Tools/Audio/measure.py.
// Depends on: Halobellum.Presentation, Halobellum.Core, Halobellum.Economy, Halobellum.Units, NUnit, UnityEditor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class AudioTests
    {
        private const string GeneratedFolder = "Assets/_Project/Audio/Generated/Resources/GeneratedAudio/";
        private const string OverrideFolder = "Assets/_Project/Art/Imported/Audio/Resources/AudioOverride/";

        // ------------------------------------------------------------------ voice limits

        [Test]
        public void Limiter_StopsAtItsMost_AndLetsOneInWhenAVoiceEnds()
        {
            var limiter = new VoiceLimiter(1);
            limiter.SetLimit(0, 2, 0f);
            Assert.IsTrue(limiter.TryStart(0, 0f));
            Assert.IsTrue(limiter.TryStart(0, 0f));
            Assert.IsFalse(limiter.TryStart(0, 0f), "A third voice is over the limit.");
            limiter.Release(0);
            Assert.IsTrue(limiter.TryStart(0, 0f));
        }

        [Test]
        public void Limiter_KeepsItsGapBetweenStarts()
        {
            var limiter = new VoiceLimiter(1);
            limiter.SetLimit(0, 10, 0.05f);
            Assert.IsTrue(limiter.TryStart(0, 1f));
            Assert.IsFalse(limiter.TryStart(0, 1.01f));
            Assert.IsTrue(limiter.TryStart(0, 1.06f));
        }

        [Test]
        public void Limiter_TwoHundredRiflesPlayOnlyAFew()
        {
            var limiter = new VoiceLimiter((int)SfxGroup.Count);
            AudioCatalog.SetLimits(limiter);
            int started = 0;
            for (int i = 0; i < 200; i++)
            {
                if (limiter.TryStart((int)SfxGroup.SmallArms, 5f)) started++;
            }
            Assert.AreEqual(1, started, "All in one instant: only one gets through the gap.");
            for (int i = 0; i < 200; i++)
            {
                if (limiter.TryStart((int)SfxGroup.SmallArms, 5f + i * 0.01f)) started++;
            }
            Assert.LessOrEqual(limiter.ActiveIn((int)SfxGroup.SmallArms), 4);
        }

        [Test]
        public void LasersOverlappingThemselves_LeaveRoomForABeam()
        {
            var limiter = new VoiceLimiter((int)SfxGroup.Count);
            AudioCatalog.SetLimits(limiter);
            SfxCue laser = AudioCatalog.Fire(WeaponFeelKind.Laser, BattleFactions.Covenant), beam = AudioCatalog.Fire(WeaponFeelKind.Beam, BattleFactions.Covenant);
            Assert.AreNotEqual(laser.Group, beam.Group);

            // Siphon Lasers: a sound of 0.95 s every 0.75 s from each of them, for as long as it takes to fill the group.
            float now = 0f;
            int refused = 0;
            for (int i = 0; i < 40; i++, now += 0.1f)
            {
                if (!limiter.TryStart((int)laser.Group, now)) refused++;
            }
            Assert.Greater(refused, 0, "The lasers should have filled their own group.");
            Assert.IsTrue(limiter.TryStart((int)beam.Group, now), "The Scarab's one long beam has one chance to start.");
        }

        // ------------------------------------------------------------------ going over a button

        [Test]
        public void Hover_IsHeardOnce_WhenThePointerMovesOntoAButton()
        {
            var watch = new HoverWatch();
            int frame = 100;
            float now = 1f;
            bool Step(float x, int button = 7)
            {
                frame++;
                now += 0.02f;
                watch.NotePointer(x, 0f, frame);
                return watch.PointerOver(button, frame, now);
            }
            watch.NotePointer(0f, 0f, frame);
            Assert.IsTrue(Step(5f), "It moved onto the button.");
            Assert.IsFalse(Step(5f), "Resting on it.");
            for (int i = 0; i < 300; i++) Assert.IsFalse(Step(5f), "Resting on it, however long.");
            Assert.IsFalse(Step(6f), "Moving within it.");

            // Off it for a while, and back.
            for (int i = 0; i < 10; i++)
            {
                frame++;
                watch.NotePointer(50f, 0f, frame);
            }
            now += 1f;
            Assert.IsTrue(Step(5f));
            Assert.IsFalse(Step(5f));
        }

        [Test]
        public void Hover_IsSilent_WhenAButtonOpensUnderAPointerStandingStill()
        {
            var watch = new HoverWatch();
            watch.NotePointer(5f, 5f, 1);
            Assert.IsFalse(watch.PointerOver(7, 1, 1f), "The very first frame: nothing has moved.");
            watch.NotePointer(5f, 5f, 2);
            watch.PointerOver(7, 2, 1.02f);
            watch.NotePointer(5f, 5f, 3);
            Assert.IsFalse(watch.PointerOver(8, 3, 1.04f), "A screen changed under it.");

            // A scene opening: even if the mouse was on its way, what is under it didn't come there by moving.
            watch.ForgetPointer();
            watch.NotePointer(90f, 90f, 4);
            Assert.IsFalse(watch.PointerOver(9, 4, 2f));
            watch.NotePointer(91f, 90f, 5);
            Assert.IsFalse(watch.PointerOver(9, 5, 2.02f), "Still on the same button.");
            watch.NotePointer(120f, 90f, 6);
            Assert.IsTrue(watch.PointerOver(10, 6, 2.04f), "Now it moved onto one.");
        }

        [Test]
        public void Hover_TwoOverlappingButtons_DoNotSetEachOtherOff()
        {
            var watch = new HoverWatch();
            watch.NotePointer(0f, 0f, 1);
            int heard = 0;
            float now = 1f;
            for (int frame = 2; frame < 200; frame++, now += 0.02f)
            {
                watch.NotePointer(frame, 0f, frame); // The mouse keeps moving, which is the worst case.
                if (watch.PointerOver(1, frame, now)) heard++;
                if (watch.PointerOver(2, frame, now)) heard++;
            }
            Assert.LessOrEqual(heard, 2);
        }

        [Test]
        public void Hover_SweptAcrossARowOfSmallButtons_KeepsAGap()
        {
            var watch = new HoverWatch();
            watch.NotePointer(0f, 0f, 1);
            int heard = 0;
            for (int frame = 2; frame < 62; frame++) // A second, at 60 frames a second, a new button every frame.
            {
                watch.NotePointer(frame, 0f, frame);
                if (watch.PointerOver(frame, frame, frame / 60f)) heard++;
            }
            Assert.LessOrEqual(heard, 1f / HoverWatch.MinGapSeconds + 1f);
            Assert.Greater(heard, 5);
        }

        // ------------------------------------------------------------------ the slots have files

        [Test]
        public void EveryMusicAndUiSlot_HasAFile()
        {
            AudioLibrary.Forget();
            foreach (string slot in AudioSlots.MusicAndUiSlots())
                Assert.IsFalse(AudioLibrary.Get(slot).IsEmpty, "No file for " + slot);
        }

        [Test]
        public void TheOwnersSongs_AreAllThere()
        {
            AudioLibrary.Forget();
            Assert.AreEqual(8, AudioLibrary.Get(AudioSlots.DeploymentMusic).Clips.Length);
            Assert.AreEqual(4, AudioLibrary.Get(AudioSlots.CombatMusic).Clips.Length);
            Assert.AreEqual(5, AudioLibrary.Get(AudioSlots.RoundDefeat).Clips.Length);
            Assert.IsTrue(AudioLibrary.Get(AudioSlots.MainMenuMusic).IsOverride);
        }

        [Test]
        public void EveryCue_HasAGeneratedFile()
        {
            foreach (SfxCue cue in AudioCatalog.Cues)
            {
                bool one = File.Exists(GeneratedFolder + cue.Slot + ".wav");
                bool several = File.Exists(GeneratedFolder + cue.Slot + "_1.wav");
                Assert.IsTrue(one || several, "No file for " + cue.Slot);
            }
        }

        [Test]
        public void EveryCue_LoadsAClipThroughTheLibrary()
        {
            AudioLibrary.Forget();
            foreach (SfxCue cue in AudioCatalog.Cues)
            {
                Assert.IsFalse(cue.Clips.IsEmpty, "Nothing loads for " + cue.Slot);
                Assert.AreEqual(cue.Slot, cue.Clips.Slot);
            }
        }

        [Test]
        public void TheOwnersList_NamesEverySlot()
        {
            string text = File.ReadAllText("Assets/_Project/Art/Imported/Audio/README.txt");
            foreach (string slot in AudioSlots.MusicAndUiSlots().Concat(AudioCatalog.Cues.Select(c => c.Slot)))
                StringAssert.Contains(slot, text, "README.txt doesn't list " + slot);
        }

        // ------------------------------------------------------------------ a slot waiting for the owner's file

        [Test]
        public void TheLekgoloSlot_IsKnown_ButStandsOutsideTheCuesThatMustHaveAFile()
        {
            Assert.AreSame(AudioCatalog.DeathLekgolo, AudioCatalog.ForSlot("sfx_death_lekgolo"));
            CollectionAssert.DoesNotContain(AudioCatalog.Cues, AudioCatalog.DeathLekgolo);
        }

        // ------------------------------------------------------------------ Unlock Skirmish

        [Test]
        public void InASkirmish_TheUi_IsNeutral_OrUnscWhereThereIsNoNeutralSound()
        {
            foreach (MatchPhase phase in Enum.GetValues(typeof(MatchPhase)))
            {
                string faction = MatchAudio.UiFactionFor(MatchMode.UnlockSkirmish, phase, SkirmishFaction.Name);
                Assert.IsNull(faction, phase.ToString());
                Assert.AreEqual("ui_neutral_click", AudioSlots.Ui(faction, UiSound.Click));
                Assert.AreEqual("ui_neutral_hover", AudioSlots.Ui(faction, UiSound.Hover));
                Assert.AreEqual("ui_neutral_error", AudioSlots.Ui(faction, UiSound.Error));
                Assert.AreEqual("ui_neutral_cardpick", AudioSlots.Ui(faction, UiSound.CardPick));
                Assert.AreEqual("ui_unsc_buy", AudioSlots.Ui(faction, UiSound.Buy));
                Assert.AreEqual("ui_unsc_sell", AudioSlots.Ui(faction, UiSound.Sell));
                Assert.AreEqual("ui_unsc_unlock", AudioSlots.Ui(faction, UiSound.Unlock));
                Assert.AreEqual("ui_unsc_battlestart", AudioSlots.Ui(faction, UiSound.BattleStart));
            }
        }

        [Test]
        public void InASkirmish_ALostGame_HasTheLandsSong()
        {
            try
            {
                BattleFactions.SetSkirmish(BattleFactions.Covenant, new Dictionary<string, string>());
                string faction = MatchAudio.MusicFaction(MatchMode.UnlockSkirmish, SkirmishFaction.Name);
                Assert.AreEqual("stinger_game_defeat_covenant", AudioSlots.GameOverSong(false, false, faction));
            }
            finally
            {
                BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant);
            }
        }

        [Test]
        public void AFactionMatchAfterASkirmish_GoesByItsOwnFaction_ThoughTheSkirmishsMapIsStillThere()
        {
            try
            {
                BattleFactions.SetSkirmish(BattleFactions.Flood, new Dictionary<string, string>());
                Assert.IsTrue(BattleFactions.PerUnitLooks, "No card has been chosen yet in the new match.");
                Assert.IsNull(MatchAudio.MusicFaction(MatchMode.Faction, null), "At its draft.");
                Assert.IsNull(MatchAudio.UiFactionFor(MatchMode.Faction, MatchPhase.Draft, null));
                Assert.AreEqual("Covenant", MatchAudio.MusicFaction(MatchMode.Faction, "Covenant"));
                Assert.AreEqual("Covenant", MatchAudio.UiFactionFor(MatchMode.Faction, MatchPhase.Deployment, "Covenant"));
            }
            finally
            {
                BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant);
            }
        }

        // ------------------------------------------------------------------ how much dust

        [TestCase("Mobile", 1f)]
        [TestCase("PC", 4f)]
        [TestCase("Something New", 1f)]
        public void TheParticleScale_GoesByTheQualityLevelsRealName(string level, float scale) => Assert.AreEqual(scale, EffectKit.ParticleScaleFor(level));

        [TestCase(new string[0], 0f)]
        [TestCase(new[] { "-sandboxDemo", "x" }, 0f)]
        [TestCase(new[] { "-particleScale" }, 0f)]
        [TestCase(new[] { "-particleScale", "many" }, 0f)]
        [TestCase(new[] { "-particleScale", "4" }, 4f)]
        [TestCase(new[] { "-particleScale", "2.5", "-effectsBench" }, 2.5f)]
        [TestCase(new[] { "-particleScale", "0.5" }, 1f)]
        public void TheParticleScaleFlag(string[] arguments, float scale) => Assert.AreEqual(scale, EffectKit.ParticleScaleFromCommandLine(arguments));

        // ------------------------------------------------------------------ which slot goes with what

        [TestCase("UNSC", "unsc")]
        [TestCase("Covenant", "covenant")]
        [TestCase("Flood", "flood")]
        [TestCase(null, "unsc")]
        [TestCase("Someone new", "unsc")]
        public void FactionKey(string faction, string key) => Assert.AreEqual(key, AudioSlots.FactionKey(faction));

        [TestCase(false, 0f, "music_combat")]
        [TestCase(false, 0.99f, "music_combat")]
        [TestCase(true, 0.39f, "music_combat_flood_present")]
        [TestCase(true, 0.41f, "music_combat")]
        public void TheFloodsCombatSong_PlaysMoreOften_OnlyWithFloodPresent(bool flood, float roll, string slot) => Assert.AreEqual(slot, AudioSlots.CombatSong(flood, roll));

        [Test]
        public void TheFloodsCombatSong_PlaysMoreThanAnyOtherSong()
        {
            int songs = AudioLibrary.Get(AudioSlots.CombatMusic).Clips.Length;
            Assert.Greater(AudioSlots.CombatFloodShare, (1f - AudioSlots.CombatFloodShare) / songs);
        }

        [TestCase(false, 0f, "stinger_match_defeat")]
        [TestCase(true, 0.1f, "stinger_match_defeat_flood_present")]
        [TestCase(true, 0.2f, "stinger_match_defeat")]
        public void ALostRound_AgainstFlood_MayHaveItsOwnSound(bool flood, float roll, string slot) => Assert.AreEqual(slot, AudioSlots.RoundDefeatSound(flood, roll, 5));

        [TestCase(true, false, "UNSC", "stinger_game_victory")]
        [TestCase(true, true, "Flood", "stinger_game_victory")]
        [TestCase(false, false, "UNSC", "stinger_game_defeat_unsc")]
        [TestCase(false, false, "Covenant", "stinger_game_defeat_covenant")]
        [TestCase(false, false, "Flood", "stinger_game_defeat_flood")]
        [TestCase(false, true, "UNSC", "stinger_game_defeat_flood_present")]
        [TestCase(false, true, "Flood", "stinger_game_defeat_flood_present")]
        public void TheEndOfAGame_HasTheRightSong(bool won, bool floodEnemy, string faction, string slot) => Assert.AreEqual(slot, AudioSlots.GameOverSong(won, floodEnemy, faction));

        [Test]
        public void UiSounds_DifferByFaction()
        {
            foreach (UiSound sound in Enum.GetValues(typeof(UiSound)))
            {
                string[] slots = { AudioSlots.Ui("UNSC", sound), AudioSlots.Ui("Covenant", sound), AudioSlots.Ui("Flood", sound) };
                Assert.AreEqual(3, slots.Distinct().Count(), sound.ToString());
            }
            Assert.AreEqual("ui_neutral_click", AudioSlots.Ui(null, UiSound.Click));
            Assert.AreEqual("ui_unsc_buy", AudioSlots.Ui(null, UiSound.Buy), "With no side chosen, a sound the neutral set lacks comes from UNSC.");
        }

        [Test]
        public void EveryWeaponKind_HasASound_ExceptABurst()
        {
            foreach (WeaponFeelKind kind in Enum.GetValues(typeof(WeaponFeelKind)))
            {
                foreach (string faction in new[] { "UNSC", "Covenant", "Flood" })
                {
                    SfxCue cue = AudioCatalog.Fire(kind, faction);
                    if (kind == WeaponFeelKind.Burst) Assert.IsNull(cue, "A burst is heard when the unit goes off.");
                    else Assert.IsNotNull(cue, $"{kind} of {faction}");
                }
            }
        }

        [Test]
        public void WeaponsSoundOfTheirFaction()
        {
            Assert.AreEqual("sfx_fire_bullet", AudioCatalog.Fire(WeaponFeel.FromName("MA5 Assault Rifle").Value, "UNSC").Slot);
            Assert.AreEqual("sfx_fire_plasma", AudioCatalog.Fire(WeaponFeel.FromName("Plasma Rifle").Value, "Covenant").Slot);
            Assert.AreEqual("sfx_fire_needle", AudioCatalog.Fire(WeaponFeel.FromName("Needle Cannons").Value, "Covenant").Slot);
            Assert.AreEqual("sfx_fire_melee_covenant", AudioCatalog.Fire(WeaponFeel.FromName("Energy Sword").Value, "Covenant").Slot);
            Assert.AreEqual("sfx_fire_melee_flood", AudioCatalog.Fire(WeaponFeel.FromName("Claw Swipe").Value, "Flood").Slot);
            Assert.AreEqual("sfx_fire_bio", AudioCatalog.Fire(WeaponFeel.FromName("Spat Bone").Value, "Flood").Slot);
            Assert.AreEqual("sfx_fire_bullet_flood", AudioCatalog.Fire(WeaponFeel.FromName("Scavenged Rifle").Value, "Flood").Slot);
        }

        [Test]
        public void Autocannons_SoundLikeHeavyBullets_NotLikeATankCannon()
        {
            foreach (string name in new[] { "M370 Autocannon", "Heavy Autocannon", "Twin Autocannons", "Chopper Autocannons" })
            {
                WeaponFeelKind kind = WeaponFeel.FromName(name).Value;
                Assert.AreEqual(WeaponFeelKind.HeavyBullet, kind, name);
                Assert.AreEqual("sfx_fire_heavybullet", AudioCatalog.Fire(kind, "UNSC").Slot, name);
                Assert.IsNull(AudioCatalog.LandingOf(kind), name + ": no explosion on every hit.");
            }
            Assert.AreEqual("sfx_fire_cannon", AudioCatalog.Fire(WeaponFeel.FromName("M512 Cannon").Value, "UNSC").Slot, "A real cannon stays one.");
            Assert.AreEqual(WeaponFeelKind.Plasma, WeaponFeel.FromName("Plasma Cannons"), "Other cannons keep their own kinds.");
            Assert.AreEqual(WeaponFeelKind.Needle, WeaponFeel.FromName("Needle Cannons"));
        }

        [Test]
        public void EverySpellInTheGame_HasASound()
        {
            string[] guids = AssetDatabase.FindAssets("t:SpellDefinition");
            Assert.Greater(guids.Length, 10);
            var seen = new HashSet<string>();
            foreach (string guid in guids)
            {
                var definition = AssetDatabase.LoadAssetAtPath<SpellDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                SpellStats stats = definition.ToStats();
                string slot = AudioSlots.Spell(stats, 1, "UNSC");
                Assert.IsNotNull(slot, stats.Name);
                Assert.IsNotNull(AudioCatalog.ForSlot(slot), $"{stats.Name}: {slot} is not in the catalog.");
                seen.Add(slot);
            }
            Assert.Greater(seen.Count, 8, "Spells should not all sound alike.");
        }

        [Test]
        public void Spells_OneLongSoundCoversRepeatedHits_ButAStormSoundsEachOne()
        {
            var glassing = new SpellStats { Name = "Glassing Beam", Effect = SpellEffect.Damage, Ticks = 5 };
            Assert.AreEqual("sfx_spell_glassing", AudioSlots.Spell(glassing, 1, "Covenant"));
            Assert.IsNull(AudioSlots.Spell(glassing, 2, "Covenant"));
            var storm = new SpellStats { Name = "Plasma Storm", Effect = SpellEffect.Damage, Ticks = 8 };
            Assert.AreEqual("sfx_spell_storm_tick", AudioSlots.Spell(storm, 3, "Covenant"));
        }

        [Test]
        public void Clips_ThePickDoesNotRepeat()
        {
            var clips = new[] { AudioClip.Create("a", 10, 1, 44100, false), AudioClip.Create("b", 10, 1, 44100, false), AudioClip.Create("c", 10, 1, 44100, false) };
            var set = new AudioClipSet("x", clips, false);
            AudioClip last = null;
            var random = new System.Random(3);
            for (int i = 0; i < 50; i++)
            {
                AudioClip clip = set.Pick((float)random.NextDouble());
                Assert.AreNotSame(last, clip);
                last = clip;
            }
        }

        // ------------------------------------------------------------------ the owner's files

        [Test]
        public void Library_OwnersFileWins_AndAMissingOneIsJustSilent()
        {
            Func<string, AudioClip> real = AudioLibrary.Loader;
            AudioClip generated = AudioClip.Create("generated", 10, 1, 44100, false), owners = AudioClip.Create("owners", 10, 1, 44100, false);
            try
            {
                AudioLibrary.Forget();
                AudioLibrary.Loader = path => path == "AudioOverride/slot_a" ? owners : path == "GeneratedAudio/slot_a" || path == "GeneratedAudio/slot_b" ? generated : null;
                AudioClipSet a = AudioLibrary.Get("slot_a");
                Assert.AreSame(owners, a.Clips.Single());
                Assert.IsTrue(a.IsOverride);
                AudioClipSet b = AudioLibrary.Get("slot_b");
                Assert.AreSame(generated, b.Clips.Single());
                Assert.IsFalse(b.IsOverride);
                Assert.IsTrue(AudioLibrary.Get("slot_c").IsEmpty);
            }
            finally
            {
                AudioLibrary.Loader = real;
                AudioLibrary.Forget();
            }
        }

        /// <summary>A short real WAV, written and imported as the owner's drop would be.</summary>
        private static void DropWav(string name, float seconds)
        {
            int samples = (int)(44100 * seconds);
            using (var file = new BinaryWriter(File.Create(OverrideFolder + name + ".wav")))
            {
                file.Write(new[] { 'R', 'I', 'F', 'F' });
                file.Write(36 + samples * 2);
                file.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                file.Write(16);
                file.Write((short)1);
                file.Write((short)1);
                file.Write(44100);
                file.Write(88200);
                file.Write((short)2);
                file.Write((short)16);
                file.Write(new[] { 'd', 'a', 't', 'a' });
                file.Write(samples * 2);
                for (int i = 0; i < samples; i++) file.Write((short)(Mathf.Sin(i * 0.05f) * 8000f));
            }
            AssetDatabase.ImportAsset(OverrideFolder + name + ".wav", ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void DroppingARealFileInTheFolder_ReplacesTheSlot()
        {
            string[] dropped = { "sfx_fire_bullet", "music_unsc_battle_1", "music_unsc_battle_2" };
            try
            {
                AudioLibrary.Forget();
                Assert.IsFalse(AudioLibrary.Get("sfx_fire_bullet").IsOverride);
                Assert.Greater(AudioLibrary.Get("sfx_fire_bullet").Clips.Length, 1, "The generated sound has variants.");

                DropWav("sfx_fire_bullet", 0.5f);
                DropWav("music_unsc_battle_1", 0.25f);
                DropWav("music_unsc_battle_2", 0.5f);
                AudioLibrary.Forget();

                AudioClipSet bullet = AudioLibrary.Get("sfx_fire_bullet");
                Assert.IsTrue(bullet.IsOverride);
                Assert.AreEqual(0.5f, bullet.Clips.Single().length, 0.01f);
                Assert.AreSame(bullet, AudioCatalog.ForSlot("sfx_fire_bullet").Clips.Slot == "sfx_fire_bullet" ? AudioCatalog.ForSlot("sfx_fire_bullet").Clips : null, "A cue plays what the library found.");

                AudioClipSet music = AudioLibrary.Get("music_unsc_battle");
                Assert.IsTrue(music.IsOverride);
                Assert.AreEqual(2, music.Clips.Length, "Several files for one slot take turns.");

                var importer = (AudioImporter)AssetImporter.GetAtPath(OverrideFolder + "music_unsc_battle_1.wav");
                Assert.AreEqual(AudioClipLoadType.Streaming, importer.defaultSampleSettings.loadType, "The owner's music is treated like the generated music.");
            }
            finally
            {
                foreach (string name in dropped) AssetDatabase.DeleteAsset(OverrideFolder + name + ".wav");
                AudioLibrary.Forget();
            }
            Assert.IsFalse(AudioLibrary.Get("sfx_fire_bullet").IsOverride, "Taking the file out brings the generated sound back.");
        }

        // ------------------------------------------------------------------ how files are imported

        [Test]
        public void Music_IsStreamedAndCompressed_EffectsAreReadyInMemory()
        {
            var music = (AudioImporter)AssetImporter.GetAtPath(OverrideFolder + "music_mainmenu.mp3");
            Assert.AreEqual(AudioClipLoadType.Streaming, music.defaultSampleSettings.loadType);
            Assert.AreEqual(AudioCompressionFormat.Vorbis, music.defaultSampleSettings.compressionFormat);
            var gameEnd = (AudioImporter)AssetImporter.GetAtPath(OverrideFolder + "stinger_game_victory.mp3");
            Assert.AreEqual(AudioClipLoadType.Streaming, gameEnd.defaultSampleSettings.loadType, "A whole song, not a short piece.");
            var roundEnd = (AudioImporter)AssetImporter.GetAtPath(OverrideFolder + "stinger_match_victory.mp3");
            Assert.AreEqual(AudioClipLoadType.CompressedInMemory, roundEnd.defaultSampleSettings.loadType);
            var sfx = (AudioImporter)AssetImporter.GetAtPath(GeneratedFolder + "sfx_fire_bullet_1.wav");
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, sfx.defaultSampleSettings.loadType);
            var ui = (AudioImporter)AssetImporter.GetAtPath(GeneratedFolder + "ui_flood_click.wav");
            Assert.AreEqual(AudioCompressionFormat.PCM, ui.defaultSampleSettings.compressionFormat);
        }

        // ------------------------------------------------------------------ silence in the room

        [TestCase(new string[0], false, false)]
        [TestCase(new string[0], true, true)]
        [TestCase(new[] { "-autoplay" }, false, true)]
        [TestCase(new[] { "-sandboxDemo", "x" }, false, true)]
        [TestCase(new[] { "-autoplay", "-audioOn" }, false, false)]
        [TestCase(new[] { "-audioCapture", "x.wav" }, true, false)]
        [TestCase(new[] { "-crowdBench", "x.txt" }, false, true)]
        [TestCase(new[] { "-menuDemo", "x" }, false, true)]
        [TestCase(new[] { "-cardsDemo", "x" }, false, true)]
        [TestCase(new[] { "-terrainGallery", "x" }, false, true)]
        [TestCase(new[] { "-sandboxDemo", "x", "-effectsBench" }, false, true)]
        [TestCase(new[] { "-crowdBenchmark" }, false, false)] // Not a flag of the game.
        [TestCase(new[] { "-menuDemo", "x", "-audioOn" }, false, false)]
        [TestCase(new[] { "-menuDemo", "x", "-audioCapture", "x.wav" }, false, false)] // The capture silences the room itself, and needs the mix.
        [TestCase(new[] { "-crowdBench", "x.txt", "-audioCapture", "x.wav" }, true, false)]
        public void TestRunsMakeNoNoise(string[] arguments, bool batchMode, bool muted) => Assert.AreEqual(muted, AudioDirector.ShouldMute(arguments, batchMode));

        /// <summary>
        /// Goes by the code, not by a list of its own: every flag a test tool starts on must silence a run, and every flag that silences a
        /// run must be one the game knows. (It said -crowdBenchmark for a tool called -crowdBench, and had never heard of three others.)
        /// </summary>
        [Test]
        public void EveryTestToolOnTheCommandLine_IsSilent()
        {
            // These only change what a tool that has already been started does, or record it.
            string[] notTools = { "-effects", "-effectsBench", "-menuDemoShort", "-menuDemoWatch", "-menuDemoSounds", "-menuDemoUiScale", "-cardsDemoLayout", "-cardsDemoLevelAll", "-cardsDemoPlaytest", "-screenDemoFake", "-screenDemoEndOn", EffectKit.ParticleScaleFlag, AudioCapture.Flag, BattleLogFile.FolderFlag, "-galleryTowers" };
            var flags = new HashSet<string>();
            var declared = new Regex(@"const string \w*Flag\w* = ""(-\w+)""");
            var checkedByName = new Regex(@"Array\.IndexOf\(args, ""(-\w+)""\) < 0\)");
            foreach (string file in Directory.GetFiles("Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                foreach (System.Text.RegularExpressions.Match match in declared.Matches(text)) flags.Add(match.Groups[1].Value);
                foreach (System.Text.RegularExpressions.Match match in checkedByName.Matches(text)) flags.Add(match.Groups[1].Value);
            }
            CollectionAssert.IsSubsetOf(notTools, flags, "A flag named here no longer exists: this list is out of date.");
            CollectionAssert.IsSubsetOf(AudioDirector.TestRunFlags, flags, "A flag that silences a run but that no tool starts on.");
            foreach (string flag in flags.Except(notTools))
                Assert.IsTrue(AudioDirector.ShouldMute(new[] { flag, "x" }, false), flag + " would play aloud in the room.");
            Assert.GreaterOrEqual(flags.Except(notTools).Count(), 8);
        }
    }
}
