// ClipBeatTests.cs
// The timing of baked clips that keep time with the battle (D90): each blow of the Stalker Form's three-blow lunge on one strike of its
// Talons, the Pure Form Ranged's shard at the end of its thrust even when it reloads faster, the Spawner's birth with each of its spawns.
// Depends on: Halobellum.Presentation (ClipBeat), NUnit.

using Halobellum.Presentation;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class ClipBeatTests
    {
        /// <summary>Where in its clip (seconds of the clip) a clip timed so is, at a moment of the battle.</summary>
        private static float ClipTime(float pace, float start, float at, float seconds) => ((at - start) * pace % seconds + seconds) % seconds;

        [Test]
        public void ALunge_ThreeStrikesLong_LandsEachBlowOnAStrike()
        {
            // 1.2 s holding three blows, the first a sixth of the way in; strikes 0.4 s apart, the next one 0.25 s from now.
            ClipBeat.Time(1.2f, 0.4f, 1f / 6f, 10f, 0.25f, out float pace, out float start);
            Assert.AreEqual(1f, pace, 1e-4f, "Three blows fit three strikes: it plays at its own speed.");
            for (int strike = 0; strike < 6; strike++)
            {
                float at = 10.25f + strike * 0.4f;
                float share = ClipTime(pace, start, at, 1.2f) / 1.2f;
                float blow = (share - 1f / 6f) * 3f; // 0, 1 or 2 at a blow.
                Assert.AreEqual(System.Math.Round(blow), blow, 1e-3, $"Strike {strike} comes with a blow.");
            }
        }

        [Test]
        public void AShot_FromAFasterReload_StillLeavesAtTheEndOfTheThrust()
        {
            // The Ranged Form's 3 s shot with its reload cut to 2.4 s: the clip plays faster so the thrust keeps coming with the shot.
            ClipBeat.Time(3f, 2.4f, 0.6f, 0f, 1f, out float pace, out float start);
            Assert.AreEqual(1.25f, pace, 1e-4f);
            for (int shot = 0; shot < 4; shot++)
                Assert.AreEqual(0.6f * 3f, ClipTime(pace, start, 1f + shot * 2.4f, 3f), 1e-3f, $"Shot {shot} leaves at the thrust.");
        }

        [Test]
        public void TheSpawnersWalk_GivesBirthWithEachSpawn()
        {
            ClipBeat.Time(4f, 4f, 0.64f, 7.5f, 3.2f, out float pace, out float start);
            Assert.AreEqual(1f, pace, 1e-4f, "One spawn to the clip: its stride stays matched to its speed.");
            Assert.AreEqual(0.64f * 4f, ClipTime(pace, start, 7.5f + 3.2f, 4f), 1e-3f);
            Assert.AreEqual(0.64f * 4f, ClipTime(pace, start, 7.5f + 3.2f + 8f, 4f), 1e-3f);
        }

        [Test]
        public void OfStartsAWholeBeatApart_TheNearestIsTaken()
        {
            Assert.AreEqual(14f, ClipBeat.Nearest(10f, 13.9f, 4f), 1e-4f);
            Assert.AreEqual(9.6f, ClipBeat.Nearest(10f, 9.7f, 0.4f), 1e-4f);
            Assert.AreEqual(10f, ClipBeat.Nearest(10f, 10.1f, 3f), 1e-4f);
        }
    }
}
