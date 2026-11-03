// BattleCameraTests.cs
// The battle camera that turns, in the real scenes. Turning keeps the point looked at in the middle of the screen, tilting stays within its limits, panning follows the way the camera
// faces, and the five places where a right-click cancels something still do so for a click and not for a drag. Picking with the mouse goes by the turned camera, and a light goes with
// what it hangs on where that would hide the floor. No mouse is used: the tests feed RightClick what a mouse would have read (RightClick.ReadForTests), and call the methods the mouse would have led to.
// Depends on: Halobellum.Presentation, Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Linq;
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
    public class BattleCameraTests
    {
        private Camera _camera;
        private BattleCameraController _control;
        private RightButtonReading _next;
        private int _nextFrame = -1;

        private IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
            _camera = Camera.main;
            _control = _camera.GetComponent<BattleCameraController>();
            Assert.IsNotNull(_control, scene + " has no battle camera.");
            RightClick.ReadForTests = Reading;
            yield return null;
        }

        [TearDown]
        public void LetGoOfTheMouse() => RightClick.ReadForTests = null;

        /// <summary>What the mouse reads: edges and movement only in the one frame they were meant for, as a real one's are.</summary>
        private RightButtonReading Reading()
        {
            if (Time.frameCount == _nextFrame) return _next;
            return new RightButtonReading { Held = _next.Held, Position = _next.Position };
        }

        /// <summary>The mouse does this at the start of the next frame, before any script asks. Comes back when all of them have seen it.</summary>
        private IEnumerator Mouse(bool pressed, bool released, Vector2 position, Vector2 moved = default)
        {
            _next = new RightButtonReading { Pressed = pressed, Released = released, Held = !released, Position = position, Delta = moved };
            _nextFrame = Time.frameCount + 1;
            yield return null;
            yield return null;
        }

        private IEnumerator RightClickAt(Vector2 at)
        {
            yield return Mouse(true, false, at);
            yield return Mouse(false, true, at);
        }

        private IEnumerator RightDragFrom(Vector2 at)
        {
            yield return Mouse(true, false, at);
            yield return Mouse(false, false, at + new Vector2(20f, 0f), new Vector2(20f, 0f));
            yield return Mouse(false, false, at + new Vector2(60f, 10f), new Vector2(40f, 10f));
            yield return Mouse(false, true, at + new Vector2(60f, 10f));
        }

        private static Vector2 OpenGround => new Vector2(Screen.width * 0.4f, Screen.height * 0.3f);

        private SandboxController Sandbox => Object.FindAnyObjectByType<SandboxController>();

        private static UnitStats Unit(SandboxController sandbox, string name) => sandbox.Units.Single(u => u.Stats.Name == name).Stats;

        private void AssertFocusInTheMiddle(string when)
        {
            Vector3 view = _camera.WorldToViewportPoint(new Vector3(_control.Focus.x, 0f, _control.Focus.y));
            Assert.AreEqual(0.5f, view.x, 1e-3f, when);
            Assert.AreEqual(0.5f, view.y, 1e-3f, when);
            Assert.AreEqual(_control.Distance, view.z, _control.Distance * 0.001f, when);
        }

        // ------------------------------------------------------------------ the scenes

        [UnityTest]
        public IEnumerator EveryBattleScene_StartsAtTheDefaultView([Values("Match", "Sandbox", "Battle")] string scene)
        {
            yield return Load(scene);
            Assert.AreEqual(0f, _control.Yaw);
            Assert.AreEqual(BattleCameraController.DefaultPitch, _control.Pitch);
            Assert.AreEqual(150f, _control.Distance);
            Assert.AreEqual(new Vector2(0f, -5f), _control.Focus);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(55f, 0f, 0f), _camera.transform.rotation), 0.01f);
        }

        // ------------------------------------------------------------------ turning

        [UnityTest]
        public IEnumerator APressWithTheMouseStillMoving_TurnsNothing_AndIsStillAClick()
        {
            yield return Load("Sandbox");
            _control.SetView(Vector2.zero, 40f, 60f, 120f);
            yield return Mouse(true, false, OpenGround, new Vector2(30f, 12f));
            Assert.AreEqual(40f, _control.Yaw, "It moved up to the press, not during it.");
            Assert.AreEqual(60f, _control.Pitch);
            _next = new RightButtonReading { Released = true, Position = OpenGround };
            _nextFrame = Time.frameCount + 1;
            yield return null;
            Assert.IsTrue(RightClick.WasClicked);
            yield return null;
            Assert.AreEqual(40f, _control.Yaw);
        }

        [UnityTest]
        public IEnumerator Turning_KeepsTheFocusPoint_InTheMiddleOfTheScreen()
        {
            yield return Load("Sandbox");
            _control.SetView(new Vector2(12f, -20f), 0f, 55f, 120f);
            Vector2 focus = _control.Focus;
            float distance = _control.Distance;
            for (int i = 0; i < 24; i++)
            {
                _control.Orbit(new Vector2(83f, i % 2 == 0 ? 30f : -30f));
                Assert.AreEqual(focus, _control.Focus, "It turns around the point, and never moves it.");
                Assert.AreEqual(distance, _control.Distance, "Nor zooms.");
                AssertFocusInTheMiddle($"Turn {i}, at {_control.Yaw} degrees.");
                Assert.Less(_camera.transform.forward.y, -0.5f, "It always looks down.");
            }
        }

        [UnityTest]
        public IEnumerator Turning_GoesAllTheWayRound()
        {
            yield return Load("Sandbox");
            _control.SetView(Vector2.zero, 0f, 55f, 100f);
            float turned = 0f, last = _control.Yaw;
            for (int i = 0; i < 40; i++)
            {
                _control.Orbit(new Vector2(100f, 0f));
                turned += Mathf.DeltaAngle(last, _control.Yaw);
                last = _control.Yaw;
                Assert.GreaterOrEqual(_control.Yaw, 0f);
                Assert.Less(_control.Yaw, 360f);
            }
            Assert.Greater(turned, 360f);
        }

        [UnityTest]
        public IEnumerator TheTilt_StaysWithinItsLimits()
        {
            yield return Load("Sandbox");
            _control.Orbit(new Vector2(0f, 100000f));
            Assert.AreEqual(_control.MinPitch, _control.Pitch);
            Assert.AreEqual(35f, _control.MinPitch);
            Assert.AreEqual(-Mathf.Sin(35f * Mathf.Deg2Rad), _camera.transform.forward.y, 1e-3f);
            _control.Orbit(new Vector2(0f, -100000f));
            Assert.AreEqual(_control.MaxPitch, _control.Pitch);
            Assert.AreEqual(75f, _control.MaxPitch);
            _control.SetView(Vector2.zero, 10f, 5f, 100f);
            Assert.AreEqual(_control.MinPitch, _control.Pitch, "SetView has the same limits.");

            // The top of the view stays below the horizon at the lowest tilt, so that the view always points at the ground.
            Assert.Greater(_control.MinPitch, _camera.fieldOfView * 0.5f);
        }

        [UnityTest]
        public IEnumerator Panning_FollowsTheWayTheCameraFaces()
        {
            yield return Load("Sandbox");

            // Facing north, as ever.
            _control.SetView(Vector2.zero, 0f, 55f, 100f);
            _control.Pan(new Vector2(0f, 10f));
            AssertNear(new Vector2(0f, 10f), _control.Focus, "Forwards is north.");
            _control.SetView(Vector2.zero, 0f, 55f, 100f);
            _control.Pan(new Vector2(10f, 0f));
            AssertNear(new Vector2(10f, 0f), _control.Focus, "To the right is east.");

            // Facing east.
            _control.SetView(Vector2.zero, 90f, 55f, 100f);
            _control.Pan(new Vector2(0f, 10f));
            AssertNear(new Vector2(10f, 0f), _control.Focus, "Forwards is east.");
            _control.SetView(Vector2.zero, 90f, 55f, 100f);
            _control.Pan(new Vector2(10f, 0f));
            AssertNear(new Vector2(0f, -10f), _control.Focus, "To the right is south.");

            // Facing south: the middle mouse button grabs the ground, whichever way that is.
            _control.SetView(Vector2.zero, 0f, 55f, 100f);
            _control.DragGround(new Vector2(50f, 0f));
            float north = _control.Focus.x;
            Assert.Less(north, 0f, "Moving the mouse right moves the view left.");
            _control.SetView(Vector2.zero, 180f, 55f, 100f);
            _control.DragGround(new Vector2(50f, 0f));
            Assert.AreEqual(-north, _control.Focus.x, 1e-3f, "The same drag from the other side goes the other way.");

            // Whichever way it faces, the screen's right is where a pan to the right goes.
            foreach (float yaw in new[] { 30f, 135f, 222f, 300f })
            {
                _control.SetView(Vector2.zero, yaw, 55f, 100f);
                _control.Pan(new Vector2(3f, 4f));
                var moved = new Vector3(_control.Focus.x, 0f, _control.Focus.y);
                Vector3 right = _camera.transform.right, ahead = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
                Assert.AreEqual(3f, Vector3.Dot(moved, right), 1e-3f);
                Assert.AreEqual(4f, Vector3.Dot(moved, ahead), 1e-3f);
            }
        }

        private static void AssertNear(Vector2 expected, Vector2 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-3f, what);
            Assert.AreEqual(expected.y, actual.y, 1e-3f, what);
        }

        [UnityTest]
        public IEnumerator TheFocusPoint_StaysInTheArena_FromEverySide()
        {
            yield return Load("Sandbox");
            CombatRules rules = Object.FindAnyObjectByType<BattleController>().Rules;
            foreach (float yaw in new[] { 0f, 45f, 90f, 180f, 270f, 315f })
            {
                _control.SetView(Vector2.zero, yaw, 55f, 100f);
                _control.Pan(new Vector2(10000f, 10000f));
                Assert.LessOrEqual(Mathf.Abs(_control.Focus.x), rules.ArenaWidth * 0.5f + 1e-3f);
                Assert.LessOrEqual(Mathf.Abs(_control.Focus.y), rules.ArenaLength * 0.5f + 1e-3f);
                AssertFocusInTheMiddle($"At the edge, facing {yaw}.");
            }
        }

        [UnityTest]
        public IEnumerator Zooming_StillWorks_FromAnySide()
        {
            yield return Load("Sandbox");
            _control.SetView(new Vector2(5f, 5f), 200f, 40f, 100f);
            _control.Wheel(1f, OpenGround);
            yield return null;
            yield return null;
            Assert.Less(_control.Distance, 100f);
            Assert.AreEqual(200f, _control.Yaw);
            AssertFocusInTheMiddle("After zooming.");
        }

        [UnityTest]
        public IEnumerator ResetView_GoesBackToTheStart()
        {
            yield return Load("Sandbox");
            _control.SetView(new Vector2(30f, 30f), 222f, 40f, 60f);
            _control.ResetView();
            Assert.AreEqual(0f, _control.Yaw);
            Assert.AreEqual(55f, _control.Pitch);
            Assert.AreEqual(150f, _control.Distance);
            Assert.AreEqual(new Vector2(0f, -5f), _control.Focus);
        }

        [UnityTest]
        public IEnumerator CameraShake_WorksOnTopOfTheTurn()
        {
            yield return Load("Sandbox");
            bool wasOn = CameraShake.ShakeEnabled;
            CameraShake.ShakeEnabled = true;
            _control.SetView(new Vector2(10f, 10f), 130f, 40f, 100f);
            Quaternion turned = _camera.transform.rotation;
            Vector3 still = new Vector3(10f, 0f, 10f) - turned * Vector3.forward * 100f;
            yield return null;
            Assert.Less(Vector3.Distance(still, _camera.transform.position), 0.001f, "Still before the blast.");
            CameraShake.Kick(new Vector3(10f, 0f, 10f), 1f);
            float most = 0f;
            for (int i = 0; i < 8; i++)
            {
                yield return null;
                Vector3 off = _camera.transform.position - still;
                most = Mathf.Max(most, off.magnitude);
                Assert.Less(Quaternion.Angle(turned, _camera.transform.rotation), 0.01f, "It doesn't disturb the turn.");
                Assert.AreEqual(0f, Vector3.Dot(off, turned * Vector3.forward), 0.01f, "It shakes across the view, whichever way that is.");
                Assert.LessOrEqual(off.magnitude, 100f * CameraShake.ShakeShare(1f) * 1.5f, "A sliver of the distance, whichever the tilt.");
            }
            Assert.Greater(most, 0f, "It does shake.");
            CameraShake.ShakeEnabled = wasOn;
        }

        // ------------------------------------------------------------------ the right button through RightClick

        [UnityTest]
        public IEnumerator EveryScript_GetsTheSameAnswer_AllOfTheFrameOfAClick()
        {
            yield return Load("Sandbox");
            yield return Mouse(true, false, OpenGround);
            Assert.IsFalse(RightClick.WasClicked);
            _next = new RightButtonReading { Released = true, Position = OpenGround };
            _nextFrame = Time.frameCount + 1;
            yield return null;
            Assert.AreEqual(_nextFrame, Time.frameCount);
            Assert.IsTrue(RightClick.WasClicked, "One script asks...");
            Assert.IsTrue(RightClick.WasClicked, "...and so does another, later in the same frame.");
            yield return null;
            Assert.IsFalse(RightClick.WasClicked, "The next frame it's over.");
        }

        [UnityTest]
        public IEnumerator ADrag_TurnsTheCamera_AndAClickDoesnt()
        {
            yield return Load("Sandbox");
            _control.SetView(Vector2.zero, 0f, 55f, 100f);
            Vector2 focus = _control.Focus;
            yield return RightDragFrom(OpenGround);
            Assert.Greater(_control.Yaw, 1f, "The drag turned it.");
            Assert.Less(_control.Yaw, 90f);
            Assert.AreEqual(focus, _control.Focus);
            Assert.AreNotEqual(55f, _control.Pitch);

            float yaw = _control.Yaw;
            yield return RightClickAt(OpenGround);
            Assert.AreEqual(yaw, _control.Yaw, "A click turns nothing.");
        }

        [UnityTest]
        public IEnumerator ADragThatStartsOverAHudWindow_DoesntTurnTheCamera()
        {
            yield return Load("Sandbox");
            _control.SetView(Vector2.zero, 0f, 55f, 100f);
            var over = new Vector2(50f, Screen.height - 50f);
            UiHitAreas.Add(new Rect(0f, 0f, 100f, 100f)); // The Rect's rows run from the top, the pointer's from the bottom.
            Assert.IsTrue(UiHitAreas.Contains(over));
            yield return RightDragFrom(over);
            Assert.AreEqual(0f, _control.Yaw);
            Assert.AreEqual(55f, _control.Pitch);
        }

        [UnityTest]
        public IEnumerator PlacingASquad_ADragKeepsIt_AClickCancelsIt()
        {
            yield return Load("Sandbox");
            SandboxController sandbox = Sandbox;
            sandbox.BeginPlacing(Unit(sandbox, "Scorpion"));
            yield return RightDragFrom(OpenGround);
            Assert.IsTrue(sandbox.IsPlacing, "A drag doesn't cancel it.");
            Assert.Greater(_control.Yaw, 1f, "It turned the camera instead.");
            yield return Mouse(true, false, OpenGround);
            Assert.IsTrue(sandbox.IsPlacing, "Nor does the press of a click: it waits for the release.");
            yield return Mouse(false, true, OpenGround);
            Assert.IsFalse(sandbox.IsPlacing, "A click cancels it.");
        }

        [UnityTest]
        public IEnumerator PlacingAnItemInTheSandbox_ADragKeepsIt_AClickCancelsIt()
        {
            yield return Load("Sandbox");
            SandboxController sandbox = Sandbox;
            sandbox.BeginPlacingItem(sandbox.Items[0]);
            yield return RightDragFrom(OpenGround);
            Assert.IsNotNull(sandbox.PlacingItem, "A drag doesn't cancel it.");
            yield return RightClickAt(OpenGround);
            Assert.IsNull(sandbox.PlacingItem, "A click cancels it.");
        }

        [UnityTest]
        public IEnumerator ASelectedSquad_ADragKeepsIt_AClickClearsIt()
        {
            yield return Load("Sandbox");
            SandboxController sandbox = Sandbox;
            sandbox.AddSquad(Team.A, Unit(sandbox, "Scorpion"));
            yield return null;
            var selection = sandbox.GetComponent<SquadSelection>();
            int squad = sandbox.GetComponent<BattleController>().Simulation.Squads[0].Id;
            selection.Select(squad);
            yield return RightDragFrom(OpenGround);
            Assert.AreEqual(squad, selection.SelectedSquadId, "A drag doesn't lose the selection.");
            yield return RightClickAt(OpenGround);
            Assert.AreEqual(-1, selection.SelectedSquadId, "A click clears it.");
        }

        [UnityTest]
        public IEnumerator PlacingASpellInAMatch_ADragKeepsIt_AClickGoesBackAStep()
        {
            yield return Load("Match");
            var match = Object.FindAnyObjectByType<MatchController>();
            match.NewMatch(3);
            yield return null;
            match.ChooseDraft(0);
            yield return null;
            Match m = match.Match;
            SpellStats smoke = m.Player.Faction.Cards.Find(c => c.Spell != null && c.Spell.Effect == SpellEffect.Smoke).Spell;
            var owned = new OwnedSpell { Id = 900, Stats = smoke, ReadyRound = m.Round };
            m.Player.Spells.Add(owned);

            match.BeginPlacingSpell(owned);
            yield return RightDragFrom(OpenGround);
            Assert.AreSame(owned, match.PlacingSpell, "A drag doesn't cancel it.");
            match.SetLineStart(new Vec2(0f, -30f));
            yield return RightDragFrom(OpenGround);
            Assert.IsNotNull(match.PlacingLineStart, "A drag doesn't go back a step either.");
            yield return RightClickAt(OpenGround);
            Assert.IsNull(match.PlacingLineStart, "A click goes back a step.");
            Assert.AreSame(owned, match.PlacingSpell);
            yield return RightClickAt(OpenGround);
            Assert.IsNull(match.PlacingSpell, "Another click cancels it.");
        }

        [UnityTest]
        public IEnumerator PlacingAnItemInAMatch_ADragKeepsIt_AClickCancelsIt()
        {
            yield return Load("Match");
            var match = Object.FindAnyObjectByType<MatchController>();
            match.NewMatch(3);
            yield return null;
            match.ChooseDraft(0);
            yield return null;
            ItemStats item = match.Match.Player.Faction.Cards.Find(c => c.Item != null).Item;
            match.Match.Player.Inventory.Add(item);
            match.BeginPlacingItem(item);
            Assert.IsNotNull(match.PlacingItem);
            yield return RightDragFrom(OpenGround);
            Assert.IsNotNull(match.PlacingItem, "A drag doesn't cancel it.");
            yield return RightClickAt(OpenGround);
            Assert.IsNull(match.PlacingItem, "A click cancels it.");
        }

        // ------------------------------------------------------------------ the mouse on the ground, from every side

        [UnityTest]
        public IEnumerator PlacingWithTheMouse_LandsUnderThePointer_FromEverySide()
        {
            yield return Load("Sandbox");
            SandboxController sandbox = Sandbox;
            var input = sandbox.GetComponent<DeploymentInput>();
            sandbox.BeginPlacing(Unit(sandbox, "Scorpion"));
            yield return null;
            float square = sandbox.GetComponent<BattleController>().Rules.SquareSize;
            var wanted = new Vector3(14f, 0f, -40f);
            Transform ghost = null;
            foreach (float yaw in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
            {
                foreach (float tilt in new[] { 35f, 55f, 75f })
                {
                    _control.SetView(new Vector2(10f, -30f), yaw, tilt, 90f);
                    input.PointAt(_camera.WorldToScreenPoint(wanted), false);
                    ghost = ghost != null ? ghost : GameObject.Find("Placement Ghost").transform;
                    Assert.LessOrEqual(Mathf.Abs(ghost.position.x - wanted.x), square, $"Facing {yaw}, tilted {tilt}.");
                    Assert.LessOrEqual(Mathf.Abs(ghost.position.z - wanted.z), square, $"Facing {yaw}, tilted {tilt}.");
                }
            }
        }

        [UnityTest]
        public IEnumerator PickingASquadAndASpot_GoByTheTurnedCamera()
        {
            yield return Load("Sandbox");
            SandboxController sandbox = Sandbox;
            sandbox.AddSquad(Team.A, Unit(sandbox, "Scorpion"));
            yield return null;
            var selection = sandbox.GetComponent<SquadSelection>();
            BattleSimulation simulation = sandbox.GetComponent<BattleController>().Simulation;
            SimUnit body = simulation.Units.First();
            Vector3 at = sandbox.GetComponent<UnitViewManager>().WorldPosition(body) + Vector3.up * body.Radius;
            foreach (float yaw in new[] { 0f, 60f, 135f, 180f, 250f, 330f })
            {
                _control.SetView(new Vector2(at.x, at.z) + new Vector2(5f, 3f), yaw, yaw < 180f ? 35f : 75f, 80f);
                SimUnit picked = selection.BodyAt(_camera.WorldToScreenPoint(at));
                Assert.IsNotNull(picked, $"Facing {yaw}.");
                Assert.AreEqual(body.SquadId, picked.SquadId);
                Assert.IsNull(selection.BodyAt(_camera.WorldToScreenPoint(at + new Vector3(25f, 0f, 25f))), "Nothing there.");

                var spot = new Vector3(-20f, 0f, 35f);
                Assert.IsTrue(SpellView.PointerOnGround(_camera.WorldToScreenPoint(spot), out Vec2 ground));
                Assert.AreEqual(spot.x, ground.X, 0.01f);
                Assert.AreEqual(spot.z, ground.Y, 0.01f);
            }
        }

        // ------------------------------------------------------------------ what used to face south

        [UnityTest]
        public IEnumerator TheView_NeverRunsOutOfLand_OrPastTheFarClip_FromAnySide()
        {
            yield return Load("Sandbox");
            float[] corners = { 0f, 1f };
            foreach (float yaw in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
            {
                foreach (Vector2 focus in new[] { Vector2.zero, new Vector2(-Mathf.Sin(yaw * Mathf.Deg2Rad), -Mathf.Cos(yaw * Mathf.Deg2Rad)) * 500f })
                {
                    _control.SetView(focus, yaw, _control.MinPitch, _control.MaxDistance);
                    foreach (float x in corners)
                    {
                        foreach (float y in corners)
                        {
                            Ray ray = _camera.ViewportPointToRay(new Vector3(x, y, 0f));
                            Assert.Less(ray.direction.y, 0f, $"The corner of the view looks down, facing {yaw}.");
                            float along = -ray.origin.y / ray.direction.y;
                            Vector3 hit = ray.GetPoint(along);
                            Assert.Less(along, _camera.farClipPlane, $"The ground is nearer than the far clip, facing {yaw}.");
                            Assert.Less(Mathf.Max(Mathf.Abs(hit.x), Mathf.Abs(hit.z)), TerrainAxis.FarReach, $"There is land there, facing {yaw}.");
                        }
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator TheTerrainsShaders_KnowWhereTheFloorIs()
        {
            yield return Load("Sandbox");
            CombatRules rules = Object.FindAnyObjectByType<BattleController>().Rules;
            Vector4 floor = Shader.GetGlobalVector("_HBFloor");
            Assert.AreEqual(rules.ArenaWidth * 0.5f, floor.x, 1e-3f);
            Assert.AreEqual(rules.ArenaLength * 0.5f, floor.y, 1e-3f);
        }

        [UnityTest]
        public IEnumerator HalosAndSpecks_AreBuiltToFaceTheCamera()
        {
            yield return Load("Sandbox");
            Mesh glow = Object.FindAnyObjectByType<BattlefieldView>().Map.Glow;
            var faces = new System.Collections.Generic.List<Vector2>();
            glow.GetUVs(1, faces);
            Assert.AreEqual(glow.vertexCount, faces.Count, "Every vertex says how it faces the camera.");
            Assert.IsTrue(faces.Any(f => f.sqrMagnitude > 0f), "Some of the glow turns to face the camera: the specks of dust on every map at least.");
        }

        [UnityTest]
        public IEnumerator ALight_GoesWhereItWouldHideTheFloor_AlongWithWhatItHangsOn()
        {
            yield return Load("Sandbox");
            float edge = Shader.GetGlobalVector("_HBFloor").y;
            // Beyond the floor's north edge. A camera 80 m up and 60 m beyond that edge looks through it at the floor; from the south, it looks through it at the land behind.
            var north = new Vector3(0f, 80f, edge + 60f);
            var south = new Vector3(0f, 80f, -edge - 60f);
            Vector3 lamp = new Vector3(0f, 20f, edge + 10f), halfway = new Vector3(0f, 17.5f, edge + 10f);

            float whole = GlowSeen(south, lamp, false);
            Assert.Greater(whole, 0.01f, "From the default side it shines.");
            Assert.Less(GlowSeen(north, lamp, false), whole * 0.001f, "Its sight line lands 6.7 m inside the floor: gone.");
            Assert.AreEqual(0.5f, GlowSeen(north, halfway, false) / GlowSeen(south, halfway, false), 0.05f, "Its sight line lands 4 m inside: half way through the thinning, as what stands there is.");

            float speck = GlowSeen(south, lamp, true);
            Assert.Greater(speck, 0.001f);
            Assert.Greater(GlowSeen(north, lamp, true) / speck, 0.5f, "A drifting speck hangs on nothing. It may pulse between the two pictures, by less than half.");
        }

        /// <summary>The brightest light a camera at this place sees of one halo, or of one drifting speck, drawn by TerrainGlow alone.</summary>
        private static float GlowSeen(Vector3 from, Vector3 at, bool speck)
        {
            const int layer = 30, size = 128;
            var builder = new TerrainMeshBuilder();
            if (speck) builder.Mote(at, 3f, new Color32(200, 200, 200, 200), new Vector3(0.001f, 0.001f, 0.001f), 0f);
            else builder.Halo(at, 3f, new Color32(200, 200, 200, 120));
            Mesh mesh = builder.ToMesh("Test glow");
            var material = new Material(Resources.Load<Shader>("Terrain/TerrainGlow"));
            var light = new GameObject("Test glow", typeof(MeshFilter), typeof(MeshRenderer)) { layer = layer };
            light.GetComponent<MeshFilter>().sharedMesh = mesh;
            light.GetComponent<MeshRenderer>().sharedMaterial = material;
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var eye = new GameObject("Test eye").AddComponent<Camera>();
            eye.transform.position = from;
            eye.transform.LookAt(at);
            eye.fieldOfView = 15f;
            eye.farClipPlane = 1000f;
            eye.cullingMask = 1 << layer;
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = Color.black;
            eye.targetTexture = target;
            eye.Render();

            var pixels = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = null;
            float brightest = pixels.GetPixels().Max(p => p.r);

            // At once, since a light still there would show in the next picture.
            eye.targetTexture = null;
            Object.DestroyImmediate(eye.gameObject);
            Object.DestroyImmediate(light);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(mesh);
            return brightest;
        }
    }
}
