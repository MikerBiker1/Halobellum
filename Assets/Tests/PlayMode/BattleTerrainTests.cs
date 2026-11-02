// BattleTerrainTests.cs
// The faction terrain (M10): every faction has at least two layouts and the seed picks between them; a seed always gives the same map; the fighting floor stays flat and nothing solid stands on it or can hide it from the camera; a map
// stays under its triangle budget; and the real scenes show the terrain of the chosen factions, with the deployment squares fading out in battle. The simulation is not involved: the same battle plays the same on any map.
// Depends on: Halobellum.Presentation, Halobellum.Economy, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Collections.Generic;
using Halobellum.Combat;
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
        public void TheFloodsFloors_HaveNoSolidPatchesOfNearBlack()
        {
            // Dark Flood forms stand on these floors: a nearly black, nearly opaque patch hides them, and at a distance it looks like a vehicle.
            foreach (string layout in new[] { "Creep", "Wreck", "Hive" })
            {
                int index = 0;
                while (TerrainLayouts.For(BattleFactions.Flood)[index].Name != layout) index++;
                TerrainMap map = Build(BattleFactions.Flood, BattleFactions.Flood, 4242, index, index);
                Vector3[] vertices = map.Decals.vertices;
                Color32[] colors = map.Decals.colors32;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (Mathf.Abs(vertices[i].x) > HalfWidth || Mathf.Abs(vertices[i].z) > HalfLength) continue;
                    if (Mathf.Abs(vertices[i].z) < 21f) continue; // The shell holes of no man's land: every map has them, and they are small.
                    Color32 color = colors[i];
                    float light = 0.3f * color.r + 0.59f * color.g + 0.11f * color.b;
                    if (color.a >= 180) Assert.GreaterOrEqual(light, 50f, $"{layout}: {color} at {vertices[i]} is too dark and too solid.");
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

        [UnityTest]
        public IEnumerator SandboxScene_AnExclusiveUnit_GetsItsBaseFactionsLand()
        {
            yield return SceneManager.LoadSceneAsync("Sandbox", LoadSceneMode.Single);
            yield return null;
            var sandbox = Object.FindAnyObjectByType<SandboxController>();
            var field = Object.FindAnyObjectByType<BattlefieldView>();

            // Listed under its sub-faction's heading, but on its faction's land. Two sub-factions and the base roster count as one faction.
            SandboxController.SandboxUnit banished = sandbox.Units.Find(u => u.Faction == "Covenant: Banished");
            SandboxController.SandboxUnit lekgolo = sandbox.Units.Find(u => u.Faction == "Covenant: Lekgolo Colony");
            Assert.IsNotNull(banished.Stats);
            Assert.AreEqual(BattleFactions.Covenant, banished.BaseFaction);
            sandbox.AddSquad(Team.B, banished.Stats);
            sandbox.AddSquad(Team.A, lekgolo.Stats);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (field.ShownFactionA != BattleFactions.Covenant && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(BattleFactions.Covenant, field.ShownFactionA);
            Assert.AreEqual(BattleFactions.Covenant, field.ShownFactionB);

            SandboxController.SandboxUnit flood = sandbox.Units.Find(u => u.Faction == "Flood: Covenant-based");
            Assert.AreEqual(BattleFactions.Flood, flood.BaseFaction);
            sandbox.Clear(Team.A);
            sandbox.AddSquad(Team.A, flood.Stats);
            deadline = Time.realtimeSinceStartup + 3f;
            while (field.ShownFactionA != BattleFactions.Flood && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(BattleFactions.Flood, field.ShownFactionA);
        }

        [UnityTest]
        public IEnumerator SandboxScene_ASubFactionsArmyFightsOnItsFactionsGround()
        {
            yield return SceneManager.LoadSceneAsync("Sandbox", LoadSceneMode.Single);
            yield return null;
            var sandbox = Object.FindAnyObjectByType<SandboxController>();
            var field = Object.FindAnyObjectByType<BattlefieldView>();

            // Banished and plain Covenant squads count as one faction; a sub-faction's Flood is Flood.
            SandboxController.SandboxUnit banished = sandbox.Units.Find(u => u.Faction.EndsWith("Banished"));
            SandboxController.SandboxUnit covenant = sandbox.Units.Find(u => u.Faction == BattleFactions.Covenant);
            SandboxController.SandboxUnit unsc = sandbox.Units.Find(u => u.Faction == BattleFactions.Unsc);
            SandboxController.SandboxUnit infested = sandbox.Units.Find(u => u.Faction.StartsWith(BattleFactions.Flood + ":"));
            Assert.AreEqual(BattleFactions.Covenant, banished.BaseFaction);
            Assert.AreEqual(BattleFactions.Flood, infested.BaseFaction);
            sandbox.AddSquad(Team.A, banished.Stats);
            sandbox.AddSquad(Team.A, covenant.Stats);
            sandbox.AddSquad(Team.A, unsc.Stats);
            sandbox.AddSquad(Team.B, infested.Stats);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (field.ShownFactionB != BattleFactions.Flood && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(BattleFactions.Covenant, field.ShownFactionA, "2 Covenant squads, one of them Banished, against 1 UNSC.");
            Assert.AreEqual(BattleFactions.Flood, field.ShownFactionB);
        }

        [UnityTest]
        public IEnumerator AFlyersBlob_IsThrownByItsRealHeight_SoItClosesInAsItDives()
        {
            yield return SceneManager.LoadSceneAsync("Sandbox", LoadSceneMode.Single);
            yield return null;
            var sandbox = Object.FindAnyObjectByType<SandboxController>();
            var battle = Object.FindAnyObjectByType<BattleController>();
            var views = battle.GetComponent<UnitViewManager>();
            var blobs = Object.FindAnyObjectByType<UnitBlobShadows>();
            sandbox.AddSquad(Team.A, sandbox.Units.Find(u => u.Stats.Name == "Infected Pelican").Stats);
            sandbox.AddSquad(Team.B, sandbox.Units.Find(u => u.Stats.Name == "Marine").Stats);
            sandbox.AddSquad(Team.B, sandbox.Units.Find(u => u.Stats.Name == "Marine").Stats);
            yield return null;
            battle.Speed = 4f;
            sandbox.Run();
            yield return null;
            SimUnit pelican = null;
            foreach (SimUnit unit in battle.Simulation.Units) if (unit.Stats.Name == "Infected Pelican") pelican = unit;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (pelican.IsAlive && !(pelican.IsLeaping && pelican.LeapProgress > 0.5f) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(pelican.IsAlive && pelican.IsLeaping, "It is on its way down.");
            battle.Speed = 0f;
            yield return null;
            yield return null;

            Assert.IsTrue(views.TryBodyPosition(pelican, out Vector3 at));
            Assert.Less(at.y, views.AirHeight * 0.9f, "Well below its flying height.");
            // The blobs are in the order of the living bodies.
            int blob = 0;
            foreach (SimUnit unit in battle.Simulation.Units)
            {
                if (unit == pelican) break;
                if (unit.IsAlive) blob++;
            }
            Vector3[] vertices = GameObject.Find("Unit Blobs").GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector3 middle = (vertices[blob * 4] + vertices[blob * 4 + 2]) * 0.5f;
            Assert.AreEqual(at.x + blobs.ThrowPerMetre.x * at.y, middle.x, 0.01f);
            Assert.AreEqual(at.z + blobs.ThrowPerMetre.y * at.y, middle.z, 0.01f);
        }

        [Test]
        public void ARibbonWithTaperedEnds_ComesToNothingAtBothEnds()
        {
            var points = new List<Vector2>();
            for (int i = 0; i < 9; i++) points.Add(new Vector2(0f, i * 7f));
            var builder = new TerrainMeshBuilder();
            builder.FlatRibbon(0f, points, 4f, 4f, new Color32(10, 10, 10, 200), true, taperEnds: 14f);
            Mesh mesh = builder.ToMesh("ribbon");
            Color32[] colors = mesh.colors32;
            Vector3[] vertices = mesh.vertices;
            const int across = 4;
            Assert.AreEqual(9 * across, vertices.Length);
            Assert.AreEqual(0, colors[1].a, "The start has faded out.");
            Assert.AreEqual(0, colors[8 * across + 1].a, "So has the end.");
            Assert.AreEqual(200, colors[4 * across + 1].a, "The middle is as painted.");
            float WidthAt(int row) => Mathf.Abs(vertices[row * across].x - vertices[row * across + 3].x);
            Assert.Less(WidthAt(0), WidthAt(1));
            Assert.Less(WidthAt(1), WidthAt(2));
            Assert.AreEqual(4f, WidthAt(4), 0.001f);
            Object.Destroy(mesh);
        }
    }
}
