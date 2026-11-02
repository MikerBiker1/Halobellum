// BattleTerrainTests.cs
// The faction terrain (M10): every faction has at least two layouts and the seed picks between them; a seed always gives the same map; the fighting floor stays flat and nothing solid stands on it or can hide it from the camera; a map
// stays under its triangle budget; and the real scenes show the terrain of the chosen factions, with the deployment squares fading out in battle. The simulation is not involved: the same battle plays the same on any map.
// Depends on: Halobellum.Presentation, Halobellum.Economy, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class BattleTerrainTests
    {
        private static readonly string[] Factions = { BattleFactions.Unsc, BattleFactions.Covenant, BattleFactions.Flood };

        private const float HalfWidth = 80f;
        private const float ZoneDepth = 80f;
        private const float Gap = 4f;
        private const float HalfLength = ZoneDepth + Gap * 0.5f;

        private static TerrainMap Build(string a, string b, int seed, int layoutA = -1, int layoutB = -1) => TerrainBuilder.Build(new TerrainRequest
        {
            FactionA = a,
            FactionB = b,
            Seed = seed,
            LayoutA = layoutA,
            LayoutB = layoutB,
            HalfWidth = HalfWidth,
            ZoneDepth = ZoneDepth,
            Gap = Gap,
        });

        /// <summary>Every layout of every faction, on either side, with a varied enemy.</summary>
        private static IEnumerable<(string a, int layoutA, string b, int layoutB)> AllLayouts()
        {
            for (int f = 0; f < Factions.Length; f++)
            {
                string other = Factions[(f + 1) % Factions.Length];
                for (int l = 0; l < TerrainLayouts.For(Factions[f]).Count; l++)
                {
                    yield return (Factions[f], l, other, l % TerrainLayouts.For(other).Count);
                    yield return (other, l % TerrainLayouts.For(other).Count, Factions[f], l);
                }
            }
        }

        [Test]
        public void EveryFaction_HasAtLeastTwoLayouts_AndSeedsReachThemAll()
        {
            foreach (string faction in Factions)
            {
                int count = TerrainLayouts.For(faction).Count;
                Assert.GreaterOrEqual(count, 2, faction);
                var seen = new HashSet<int>();
                for (int seed = 0; seed < 200; seed++)
                {
                    int picked = TerrainLayouts.Pick(faction, seed, Team.A);
                    Assert.That(picked, Is.InRange(0, count - 1));
                    seen.Add(picked);
                }
                Assert.AreEqual(count, seen.Count, faction + ": some layout is never picked.");
            }
        }

        [Test]
        public void SameSeed_GivesTheSameMap_OtherSeedsDiffer()
        {
            TerrainMap first = Build(BattleFactions.Unsc, BattleFactions.Flood, 77, 0, 0);
            TerrainMap second = Build(BattleFactions.Unsc, BattleFactions.Flood, 77, 0, 0);
            TerrainMap other = Build(BattleFactions.Unsc, BattleFactions.Flood, 78, 0, 0);
            CollectionAssert.AreEqual(first.Solid.vertices, second.Solid.vertices);
            CollectionAssert.AreEqual(first.Ground.vertices, second.Ground.vertices);
            CollectionAssert.AreEqual(first.Control.GetPixels32(), second.Control.GetPixels32());
            CollectionAssert.AreNotEqual(first.Solid.vertices, other.Solid.vertices);
            first.Destroy();
            second.Destroy();
            other.Destroy();
        }

        [Test]
        public void TheFloorStaysFlatAndClear_OnEveryLayout()
        {
            foreach ((string a, int layoutA, string b, int layoutB) in AllLayouts())
            {
                TerrainMap map = Build(a, b, 4242, layoutA, layoutB);
                string name = $"{a} {map.LayoutA.Name} against {b} {map.LayoutB.Name}";

                foreach (Vector3 v in map.Ground.vertices)
                {
                    if (Mathf.Abs(v.x) <= HalfWidth + 1f && Mathf.Abs(v.z) <= HalfLength + 1f)
                        Assert.AreEqual(TerrainCanvas.GroundHeight, v.y, 0.001f, $"{name}: the ground rises over the arena at {v}.");
                }
                foreach (Vector3 v in map.Decals.vertices)
                {
                    if (Mathf.Abs(v.x) <= HalfWidth && Mathf.Abs(v.z) <= HalfLength)
                        Assert.Less(v.y, 0f, $"{name}: paint on the floor stands proud at {v}.");
                }
                foreach (Vector3 v in map.Solid.vertices)
                {
                    bool overFloor = Mathf.Abs(v.x) < HalfWidth && Mathf.Abs(v.z) < HalfLength;
                    Assert.IsFalse(overFloor && v.y > -0.5f, $"{name}: something solid stands on the floor at {v}.");
                }
                map.Destroy();
            }
        }

        [Test]
        public void NothingToTheSouth_CanHideTheFloorFromTheCamera()
        {
            // The camera stands to the south of the floor, inside its width, and looks north. Solid things south of the floor and in line with it must stand back.
            const float tolerance = 1.5f;
            foreach ((string a, int layoutA, string b, int layoutB) in AllLayouts())
            {
                TerrainMap map = Build(a, b, 909, layoutA, layoutB);
                string name = $"{a} {map.LayoutA.Name} against {b} {map.LayoutB.Name}";
                foreach (Vector3 v in map.Solid.vertices)
                {
                    if (v.z > -HalfLength || Mathf.Abs(v.x) >= HalfWidth) continue;
                    float back = -v.z - HalfLength;
                    Assert.LessOrEqual(v.y * TerrainCanvas.SouthLean, back + tolerance, $"{name}: {v} could hide the floor from the camera.");
                }
                foreach (Vector3 v in map.Ground.vertices)
                {
                    if (v.z > -HalfLength || Mathf.Abs(v.x) >= HalfWidth) continue;
                    float back = -v.z - HalfLength;
                    Assert.LessOrEqual(v.y * TerrainCanvas.SouthLean, back + tolerance, $"{name}: a hill at {v} could hide the floor from the camera.");
                }
                map.Destroy();
            }
        }

        [Test]
        public void EveryMap_IsUnderTheTriangleBudget()
        {
            foreach ((string a, int layoutA, string b, int layoutB) in AllLayouts())
            {
                TerrainMap map = Build(a, b, 31337, layoutA, layoutB);
                Assert.Less(map.TriangleCount, 150000, $"{a} {map.LayoutA.Name} against {b} {map.LayoutB.Name}.");
                map.Destroy();
            }
        }

        [Test]
        public void EveryLayout_PutsSomethingUp_AndPaintsTheFloor()
        {
            foreach ((string a, int layoutA, string b, int layoutB) in AllLayouts())
            {
                TerrainMap map = Build(a, b, 5, layoutA, layoutB);
                Assert.Greater(map.Solid.vertexCount, 1000, map.LayoutA.Name);
                Assert.Greater(map.Decals.vertexCount, 100, map.LayoutA.Name);
                map.Destroy();
            }
        }

        [Test]
        public void TheSideLine_RunsAcrossNoMansLand()
        {
            for (float x = -80f; x <= 80f; x += 7f)
            {
                Assert.AreEqual(0f, TerrainBuilder.SideAt(x, -60f, 13f, 29f), 0.001f, "Deep in Team A's zone the ground is all Team A's.");
                Assert.AreEqual(1f, TerrainBuilder.SideAt(x, 60f, 13f, 29f), 0.001f, "Deep in Team B's zone the ground is all Team B's.");
            }
        }

        [UnityTest]
        public IEnumerator MatchScene_ShowsTheChosenFactions_AndSquaresFadeInBattle()
        {
            yield return SceneManager.LoadSceneAsync("Match", LoadSceneMode.Single);
            yield return null;
            var match = Object.FindAnyObjectByType<MatchController>();
            var battle = Object.FindAnyObjectByType<BattleController>();
            var field = Object.FindAnyObjectByType<BattlefieldView>();
            Assert.IsNotNull(field);
            match.NewMatch(3);
            yield return null;
            Assert.IsNotNull(field.Map, "There is a backdrop at the draft.");
            Assert.IsNotNull(GameObject.Find("Battlefield"));

            int pick = match.Match.PlayerDraft.FindIndex(c => c.Faction.Name == BattleFactions.Flood);
            match.ChooseDraft(pick >= 0 ? pick : 0);
            yield return null;
            yield return null;
            Assert.AreEqual(match.Match.Player.Faction.Name, field.ShownFactionA);
            Assert.AreEqual(match.Match.Enemy.Faction.Name, field.ShownFactionB);
            Assert.AreEqual(TerrainThemes.For(field.ShownFactionA).Faction, field.Map.ThemeA.Faction);

            match.SkipCards();
            yield return null;
            Assert.Greater(field.SquaresShown, 0.9f, "The squares are clear during deployment.");

            battle.Speed = 4f;
            match.Ready();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (field.SquaresShown > 0.01f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.LessOrEqual(field.SquaresShown, 0.01f, "The squares fade out in battle.");
            var blobs = Object.FindAnyObjectByType<UnitBlobShadows>();
            Assert.IsNotNull(blobs);
            Assert.Greater(blobs.Shown, 0, "Units have blobs under them.");
        }

        [UnityTest]
        public IEnumerator SandboxScene_FollowsTheFactionsInPlay()
        {
            yield return SceneManager.LoadSceneAsync("Sandbox", LoadSceneMode.Single);
            yield return null;
            var sandbox = Object.FindAnyObjectByType<SandboxController>();
            var field = Object.FindAnyObjectByType<BattlefieldView>();
            Assert.IsNotNull(field);
            Assert.AreEqual(BattleFactions.Unsc, field.ShownFactionA, "The default before anything is placed.");
            Assert.AreEqual(BattleFactions.Covenant, field.ShownFactionB);

            SandboxController.SandboxUnit flood = sandbox.Units.Find(u => u.Faction == BattleFactions.Flood);
            Assert.IsNotNull(flood);
            sandbox.AddSquad(Team.A, flood.Stats);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (field.ShownFactionA != BattleFactions.Flood && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(BattleFactions.Flood, field.ShownFactionA, "Blue is all Flood now.");
        }
    }
}
