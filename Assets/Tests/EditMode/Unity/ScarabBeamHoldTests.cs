// ScarabBeamHoldTests.cs
// D107 (the owner, 2026-11-07), with the game's own unit data: a unit whose beam is charging or firing holds its ground and faces the beam's
// spot. A Scarab's beam goes on sweeping toward the next target after the first falls; the Scarab used to walk and turn toward that
// target at once, so the beam left it sideways. It moves on once the beam ends.
// Depends on: Halobellum.Combat, Halobellum.Core, Halobellum.EditorTools, NUnit.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.EditorTools;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class ScarabBeamHoldTests
    {
        [Test]
        public void AScarab_HoldsStillAndFacesItsBeam_UntilTheBeamEnds()
        {
            Dictionary<string, UnitStats> u = HeadlessRunnerTool.LoadSliceUnits().ToDictionary(x => x.unitName, x => x.ToStats());
            UnitStats marine = u["Marine"].Clone();
            marine.MoveSpeed = 0f;
            var sim = new BattleSimulation(HeadlessRunnerTool.LoadRules(), 1);
            SimUnit scarab = sim.AddUnit(u["Scarab"], Team.A, new Vec2(0f, -20f));
            SimUnit first = sim.AddUnit(marine, Team.B, new Vec2(0f, 0f));      // In the beam's reach.
            SimUnit next = sim.AddUnit(marine, Team.B, new Vec2(25f, 10f));     // Off to one side, out of its reach: it must walk to it.
            SimWeapon beam = scarab.Weapons[0];
            Assert.IsTrue(beam.Stats.IsBeam);

            int ticks = 0;
            while (first.IsAlive && ticks++ < 30 * 15) sim.Step();
            Assert.IsFalse(first.IsAlive, "The beam burns down the first Marine.");
            Assert.AreNotEqual(BeamPhase.Off, beam.BeamPhase, "Its beam is still on.");

            Vec2 heldAt = scarab.Position;
            int held = 0;
            while (beam.BeamPhase != BeamPhase.Off && held++ < 30 * 10)
            {
                sim.Step();
                Assert.Less(Vec2.Distance(scarab.Position, heldAt), 0.01f, "It holds its ground while the beam is on.");
                Assert.AreNotEqual(UnitState.Move, scarab.State);
            }
            Assert.Greater(held, 10, "The beam swept on for a while after the first Marine fell.");
            Assert.Less(Vec2.AngleDegrees(scarab.Facing, beam.BeamPoint - scarab.Position), 5f, "Facing its beam's spot, not the next target.");
            Assert.Greater(Vec2.AngleDegrees(scarab.Facing, next.Position - scarab.Position), 15f, "The next target is well off to the side.");

            for (int i = 0; i < 30 * 3; i++) sim.Step();
            Assert.Greater(Vec2.Distance(scarab.Position, heldAt), 0.3f, "Once the beam ends it walks on toward the next target.");
        }
    }
}
