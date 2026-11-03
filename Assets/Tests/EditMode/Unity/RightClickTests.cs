// RightClickTests.cs
// What the right mouse button means now that holding it and moving the mouse turns the camera: a short press with hardly any movement is a click, at its release, and anything else
// is not; a drag turns the camera unless it started over a HUD window. Also what a tool's command line asks of the camera, and the things that went by a camera fixed to the north.
// Needs Unity's Vector2, so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Presentation (RightClickTracker, BattleCameraController, EffectKit, MushroomCloud), NUnit, UnityEngine.

using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Halobellum.Tests
{
    public class RightClickTests
    {
        private static readonly Vector2 Here = new Vector2(400f, 300f);

        private static RightButtonReading Press(Vector2 at) => new RightButtonReading { Pressed = true, Held = true, Position = at };
        private static RightButtonReading Hold(Vector2 at, Vector2 moved) => new RightButtonReading { Held = true, Position = at, Delta = moved };
        private static RightButtonReading Release(Vector2 at) => new RightButtonReading { Released = true, Position = at };

        [Test]
        public void AShortPressWithHardlyAnyMovement_IsAClick_AtItsRelease_ForOneStep()
        {
            var right = new RightClickTracker();
            right.Step(Press(Here), 1f, false);
            Assert.IsFalse(right.Clicked, "Not at the press: it may yet turn out to be a drag.");
            right.Step(Hold(Here + new Vector2(2f, 1f), new Vector2(2f, 1f)), 1.05f, false);
            Assert.IsFalse(right.Clicked);
            Assert.IsFalse(right.IsRotating, "A hand's tremble turns nothing.");
            right.Step(Release(Here + new Vector2(2f, 1f)), 1.1f, false);
            Assert.IsTrue(right.Clicked);
            right.Step(default, 1.15f, false);
            Assert.IsFalse(right.Clicked, "One click, once.");
        }

        [Test]
        public void AClickWithTheMouseStillMoving_IsStillAClick_SinceThePressFramesMovementDoesntCount()
        {
            var right = new RightClickTracker();
            right.Step(new RightButtonReading { Pressed = true, Held = true, Position = Here, Delta = new Vector2(40f, -25f) }, 1f, false);
            Assert.IsFalse(right.IsDrag, "That movement ended at the press, and none has come since.");
            Assert.IsFalse(right.IsRotating);
            right.Step(Release(Here), 1.05f, false);
            Assert.IsTrue(right.Clicked);
        }

        [Test]
        public void ThePressFramesMovement_IsNoHeadStartTowardsADrag()
        {
            var right = new RightClickTracker();
            right.Step(new RightButtonReading { Pressed = true, Held = true, Position = Here, Delta = new Vector2(5f, 0f) }, 1f, false);
            right.Step(Hold(Here + new Vector2(3f, 0f), new Vector2(3f, 0f)), 1.05f, false);
            Assert.IsFalse(right.IsDrag, "3 pixels since the press, not 8.");
            right.Step(Hold(Here + new Vector2(6f, 0f), new Vector2(3f, 0f)), 1.1f, false);
            Assert.IsTrue(right.IsDrag, "From then on it counts as ever.");
        }

        [Test]
        public void PressedAndReleasedInOneFrame_IsStillAClick()
        {
            var right = new RightClickTracker();
            right.Step(new RightButtonReading { Pressed = true, Released = true, Position = Here }, 1f, false);
            Assert.IsTrue(right.Clicked);
            Assert.IsFalse(right.IsDown);
        }

        [Test]
        public void ADrag_TurnsTheCamera_AndIsNoClick()
        {
            var right = new RightClickTracker();
            right.Step(Press(Here), 1f, false);
            Vector2 at = Here + new Vector2(RightClickTracker.DragPixels, 0f);
            right.Step(Hold(at, new Vector2(RightClickTracker.DragPixels, 0f)), 1.05f, false);
            Assert.IsTrue(right.IsRotating);
            right.Step(Release(at), 1.1f, false);
            Assert.IsFalse(right.Clicked);
            Assert.IsFalse(right.IsRotating);
        }

        [Test]
        public void ADragThatComesBackToWhereItStarted_IsStillADrag()
        {
            var right = new RightClickTracker();
            right.Step(Press(Here), 1f, false);
            right.Step(Hold(Here + new Vector2(30f, 0f), new Vector2(30f, 0f)), 1.05f, false);
            right.Step(Hold(Here, new Vector2(-30f, 0f)), 1.1f, false);
            Assert.IsTrue(right.IsRotating);
            right.Step(Release(Here), 1.15f, false);
            Assert.IsFalse(right.Clicked);
        }

        [Test]
        public void ADragAtTheEdgeOfTheScreen_CountsBy_WhatTheMouseMoved()
        {
            // The pointer can't go further, while the mouse goes on moving.
            var right = new RightClickTracker();
            right.Step(Press(Here), 1f, false);
            right.Step(Hold(Here, new Vector2(10f, 0f)), 1.05f, false);
            Assert.IsTrue(right.IsRotating);
        }

        [Test]
        public void ALongPress_IsNeitherAClickNorADrag()
        {
            var right = new RightClickTracker();
            right.Step(Press(Here), 1f, false);
            right.Step(Hold(Here, Vector2.zero), 1f + RightClickTracker.MostClickSeconds, false);
            Assert.IsFalse(right.IsRotating);
            right.Step(Release(Here), 1.1f + RightClickTracker.MostClickSeconds, false);
            Assert.IsFalse(right.Clicked);
        }

        [Test]
        public void ADragThatStartsOverAHudWindow_TurnsNothing_EvenOnceOutOfIt()
        {
            var right = new RightClickTracker();
            right.Step(Press(Here), 1f, true);
            right.Step(Hold(Here + new Vector2(100f, 0f), new Vector2(100f, 0f)), 1.05f, false);
            Assert.IsFalse(right.IsRotating);
            right.Step(Release(Here + new Vector2(100f, 0f)), 1.1f, false);
            Assert.IsFalse(right.Clicked);
        }

        [Test]
        public void AShortClickOverAHudWindow_IsStillAClick_AsItWasBefore()
        {
            var right = new RightClickTracker();
            right.Step(Press(Here), 1f, true);
            right.Step(Release(Here), 1.05f, false);
            Assert.IsTrue(right.Clicked);
        }

        [Test]
        public void AReleaseNobodySaw_IsNoClick()
        {
            // The tracker isn't moved on while the pause menu has the camera and the input switched off.
            var right = new RightClickTracker();
            right.Step(Press(Here), 1f, false);
            right.Step(default, 1.1f, false);
            Assert.IsFalse(right.Clicked);
            Assert.IsFalse(right.IsDown);
        }

        [Test]
        public void APressNobodySawStarting_CountsFromWhenItIsSeen()
        {
            var right = new RightClickTracker();
            right.Step(Hold(Here, Vector2.zero), 5f, false);
            Assert.IsTrue(right.IsDown);
            right.Step(Release(Here), 5.05f, false);
            Assert.IsTrue(right.Clicked);
        }

        [Test]
        public void ToolsGetTheDefaultView_UnlessTheCommandLineSaysOtherwise()
        {
            BattleCameraController.ToolView(new[] { "-terrainGallery", "x" }, out float yaw, out float tilt);
            Assert.AreEqual(0f, yaw);
            Assert.AreEqual(BattleCameraController.DefaultPitch, tilt);
            BattleCameraController.ToolView(new[] { "-sandboxDemo", "x", "-cameraYaw", "135", "-cameraPitch", "37.5" }, out yaw, out tilt);
            Assert.AreEqual(135f, yaw);
            Assert.AreEqual(37.5f, tilt);
        }

        [Test]
        public void ReadScale_IsWhatItWasAtTheDefaultTilt_AndGoesByDistance()
        {
            float sin55 = Mathf.Sin(55f * Mathf.Deg2Rad);
            Assert.AreEqual(150f * sin55 / 90f, EffectKit.ReadScaleFor(150f * sin55, -sin55), 1e-4f);
            float sin35 = Mathf.Sin(35f * Mathf.Deg2Rad), sin75 = Mathf.Sin(75f * Mathf.Deg2Rad);
            Assert.AreEqual(EffectKit.ReadScaleFor(150f * sin55, -sin55), EffectKit.ReadScaleFor(150f * sin35, -sin35), 1e-4f, "The same distance at the lowest tilt.");
            Assert.AreEqual(EffectKit.ReadScaleFor(150f * sin55, -sin55), EffectKit.ReadScaleFor(150f * sin75, -sin75), 1e-4f, "The same distance at the steepest tilt.");
        }

        [Test]
        public void TheNukesCap_IsNarrowAlongTheWayTheCameraFacedAtTheBlast()
        {
            const float radius = 14f, age = 4f;
            int rimStart = MushroomCloud.StemPuffs + MushroomCloud.SkirtPuffs;
            foreach (float yaw in new[] { 0f, 40f, 90f, 135f, 180f, 290f })
            {
                Vector2 facing = MushroomCloud.FacingOf(Quaternion.Euler(55f, yaw, 0f) * Vector3.forward);
                Assert.AreEqual(Mathf.Sin(yaw * Mathf.Deg2Rad), facing.x, 1e-4f);
                Assert.AreEqual(Mathf.Cos(yaw * Mathf.Deg2Rad), facing.y, 1e-4f);
                var across = new Vector2(facing.y, -facing.x);
                float mostAlong = 0f, mostAcross = 0f;
                for (int i = rimStart; i < rimStart + MushroomCloud.RimAround * MushroomCloud.RimAcross; i++)
                {
                    Vector3 offset = MushroomCloud.Smoke(i, age, radius, 3, facing).Offset;
                    var flat = new Vector2(offset.x, offset.z);
                    mostAlong = Mathf.Max(mostAlong, Mathf.Abs(Vector2.Dot(flat, facing)));
                    mostAcross = Mathf.Max(mostAcross, Mathf.Abs(Vector2.Dot(flat, across)));
                }
                Assert.AreEqual(MushroomCloud.CapDepthShare, mostAlong / mostAcross, 0.09f, $"Facing {yaw} degrees.");
            }
        }

        [Test]
        public void TheNukesCap_FacingNorth_IsTheOneTheGameAlwaysHad()
        {
            for (int i = 0; i < MushroomCloud.SmokePuffs; i++)
            {
                Vector3 plain = MushroomCloud.Smoke(i, 3f, 14f, 3).Offset, north = MushroomCloud.Smoke(i, 3f, 14f, 3, new Vector2(0f, 1f)).Offset;
                Assert.AreEqual(plain, north);
            }
            int rim = MushroomCloud.StemPuffs + MushroomCloud.SkirtPuffs;
            Vector3 offset = MushroomCloud.Smoke(rim + 2, 4f, 14f, 3).Offset;
            Assert.LessOrEqual(Mathf.Abs(offset.z), MushroomCloud.CapDepthShare * (MushroomCloud.RimRadius(4f, 14f) + MushroomCloud.RimThickness(4f, 14f)) + 1e-3f);
        }

        [Test]
        public void ALookingStraightDownCamera_FacesNorth()
        {
            Assert.AreEqual(MushroomCloud.North, MushroomCloud.FacingOf(Vector3.down));
        }
    }
}
