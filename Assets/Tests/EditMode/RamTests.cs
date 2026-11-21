// RamTests.cs
// D160 (the owner): the Infested Warthog's ram hits once, then again only after 0.5 s of driving to build up speed, so against a squad it
// rams the first body and has no speed for the rest.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class RamTests
    {
        private static UnitStats Warthog()
        {
            WeaponStats ram = TestUnits.Weapon(damage: 60f, range: 1.5f, type: DamageType.Melee, interval: 1f);
            ram.RamRechargeSeconds = 0.5f;
            return TestUnits.Unit(name: "Infested Warthog", health: 1e5f, speed: 5f, radius: 1.2f, filter: TargetFilter.Ground, weapon: ram);
        }

        private static UnitStats Body(float health) =>
            TestUnits.Unit(name: "Marine", health: health, speed: 0f, radius: 0.5f, weapon: TestUnits.Weapon(damage: 0f, range: 0.5f));

        private static int Rams(BattleSimulation sim, float seconds)
        {
            int rams = 0;
            sim.Events.WeaponFired += (a, w, t) => { if (a.Team == Team.A && w == 0) rams++; };
            int ticks = (int)System.Math.Round(seconds / sim.TickSeconds);
            for (int i = 0; i < ticks; i++) sim.Step();
            return rams;
        }

        [Test]
        public void AgainstASquadStandingTogether_ItRamsOnce()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Warthog(), Team.A, new Vec2(0f, -10f));
            for (int i = 0; i < 3; i++) sim.AddUnit(Body(1000f), Team.B, new Vec2(-1.2f + 1.2f * i, 0f));
            Assert.AreEqual(1, Rams(sim, 5f), "One ram, then no speed for another while it stands among them.");
        }

        [Test]
        public void AfterDrivingOnToTheNextTarget_ItRamsAgain()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Warthog(), Team.A, new Vec2(0f, -10f));
            sim.AddUnit(Body(50f), Team.B, Vec2.Zero);
            sim.AddUnit(Body(1000f), Team.B, new Vec2(0f, 12f));
            Assert.AreEqual(2, Rams(sim, 6f), "The first dies to its ram; driving the 10 m to the next builds the speed for a second.");
        }
    }
}
