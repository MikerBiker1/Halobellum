// AudioPlacementTests.cs
// Where a sound is from the camera shapes it (the owner, 2026-11-06): nearer than the starting view is louder, further is quieter, off screen
// fades away; a node of light hitting a base is louder than other sounds and the music dips under it, in quickly and back gently.
// Depends on: Halobellum.Presentation, NUnit, UnityEngine.

using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Halobellum.Tests
{
    public class AudioPlacementTests
    {
        private static float Gain(Vector3 viewport, float metres)
        {
            AudioDirector.Placement(viewport, metres, out _, out float gain);
            return gain;
        }

        [Test]
        public void ZoomingIn_MakesASoundLouder_AndZoomingOut_Quieter()
        {
            var centre = new Vector3(0.5f, 0.5f, 1f);
            Assert.AreEqual(1f, Gain(centre, AudioDirector.ReferenceMetres), 1e-4f, "At the starting view, a sound plays at its own level.");
            Assert.Greater(Gain(centre, 40f), 1.5f, "Zoomed in close.");
            Assert.AreEqual(1.7f, Gain(centre, 5f), 1e-4f, "But no louder than 1.7 times.");
            Assert.Less(Gain(centre, 240f), 0.75f, "Zoomed right out.");
            Assert.AreEqual(0.4f, Gain(centre, 5000f), 1e-4f, "And never under 0.4 for distance alone.");
        }

        [Test]
        public void OffScreen_ASoundFades_ToAlmostNothing()
        {
            float onEdge = Gain(new Vector3(1f, 0.5f, 1f), AudioDirector.ReferenceMetres);
            float halfAScreenOut = Gain(new Vector3(1.5f, 0.5f, 1f), AudioDirector.ReferenceMetres);
            float behind = Gain(new Vector3(0.5f, 0.5f, -1f), AudioDirector.ReferenceMetres);
            Assert.AreEqual(1f, onEdge, 1e-4f);
            Assert.AreEqual(0.12f, halfAScreenOut, 1e-4f);
            Assert.AreEqual(0.12f, behind, 1e-4f);
            AudioDirector.Placement(new Vector3(0f, 0.5f, 1f), 100f, out float pan, out _);
            Assert.AreEqual(-0.75f, pan, 1e-4f, "Panned left from the left of the screen.");
        }

        [Test]
        public void NodesOfLight_AreLouderThanTheirOldLevel()
        {
            Assert.GreaterOrEqual(AudioCatalog.NodeSmall.Volume, 1.6f);
            Assert.GreaterOrEqual(AudioCatalog.NodeBig.Volume, 2f);
        }

        [Test]
        public void TheMusic_DipsUnderANodeHit_ThenComesBack()
        {
            var go = new GameObject("deck");
            try
            {
                var deck = new MusicDeck(go.AddComponent<AudioSource>(), go.AddComponent<AudioSource>(), go.AddComponent<AudioSource>());
                deck.Tick(0.02f, 1f);
                Assert.AreEqual(1f, deck.DipLevel, 1e-4f);
                deck.Dip(0.9f, 0.45f);
                for (int i = 0; i < 10; i++) deck.Tick(0.02f, 1f); // 0.2 s
                Assert.AreEqual(0.45f, deck.DipLevel, 1e-4f, "Down within 0.2 s.");
                deck.Dip(1.4f, 0.3f); // A big one while the dip lasts: deeper, and longer.
                for (int i = 0; i < 50; i++) deck.Tick(0.02f, 1f); // 1 s
                Assert.AreEqual(0.3f, deck.DipLevel, 1e-4f, "Held while the hits go on.");
                for (int i = 0; i < 100; i++) deck.Tick(0.02f, 1f); // 2 s more
                Assert.AreEqual(1f, deck.DipLevel, 1e-4f, "Back up after.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
