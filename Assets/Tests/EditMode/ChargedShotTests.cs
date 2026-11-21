// ChargedShotTests.cs
// D151: the Jackal's plasma pistol. At a target with shield it charges 1 s, then fires a big, slow bolt that strips 100 shield points and
// nothing else; at a target with no shield it fires as usual. Also D151: a type's own squad gap (the Elites stand 1.5 m apart).

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class ChargedShotTests
    {
        private static UnitStats Jackal()
        {
            WeaponStats pistol = TestUnits.Weapon(damage: 6f, range: 10f, type: DamageType.Energy, interval: 0.6f);
            pistol.ShieldBreak = 100f;
            pistol.ShieldBreakChargeSeconds = 1f;
            pistol.ShieldBreakProjectileSpeed = 12f;
            return TestUnits.Unit(name: "Jackal", health: 1000f, speed: 0f, weapon: pistol);
        }

        private static UnitStats Target(float shield) =>
            TestUnits.Unit(name: "Target", health: 1000f, speed: 0f, shield: shield, weapon: TestUnits.Weapon(damage: 0f, range: 1f));

        private static void Run(BattleSimulation sim, float seconds)
        {
            int ticks = (int)System.Math.Round(seconds / sim.TickSeconds);
            for (int i = 0; i < ticks; i++) sim.Step();
        }

        [Test]
        public void AtAShieldedTarget_ItCharges1s_ThenABoltStrips100Shield_AndNoHealth()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Jackal(), Team.A, Vec2.Zero);
            SimUnit target = sim.AddUnit(Target(400f), Team.B, new Vec2(0f, 7f));
            var fired = new List<float>();
            sim.Events.ChargedShotFired += (a, w, t) => fired.Add(sim.ElapsedSeconds);
            int plain = 0;
            sim.Events.WeaponFired += (a, w, t) => plain++;

            Run(sim, 0.8f);
            Assert.AreEqual(0, fired.Count, "Still charging.");
            Assert.AreEqual(400f, target.Shield, 0.01f);
            Run(sim, 1.2f); // 2 s in: fired at about 1 s, the bolt flies about 6 m at 12 m/s.
            Assert.AreEqual(1, fired.Count);
            Assert.AreEqual(1f, fired[0], 0.15f, "It fires once its 1 s charge is over.");
            Assert.AreEqual(0, plain, "Only charged bolts at a shielded target.");
            Assert.AreEqual(300f, target.Shield, 0.01f, "One bolt has landed: 100 shield gone.");
            Assert.AreEqual(1000f, target.Health, 0.01f, "Nothing taken from health.");
        }

        [Test]
        public void AtATargetWithNoShield_ItFiresAsUsual()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Jackal(), Team.A, Vec2.Zero);
            SimUnit target = sim.AddUnit(Target(0f), Team.B, new Vec2(0f, 7f));
            int charged = 0;
            sim.Events.ChargedShotFired += (a, w, t) => charged++;
            Run(sim, 2f);
            Assert.AreEqual(0, charged);
            Assert.Less(target.Health, 1000f, "Plain shots hit at once.");
        }

        [Test]
        public void ItWindsUpWhileCharging_SoTheTipGlows()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Jackal(), Team.A, Vec2.Zero);
            sim.AddUnit(Target(400f), Team.B, new Vec2(0f, 7f));
            Run(sim, 0.5f);
            var windUps = new List<WindUpShot>();
            sim.GetWindUps(windUps);
            Assert.AreEqual(1, windUps.Count, "The charge is drawn as a wind-up, a glow at the pistol's tip.");
            Assert.Greater(windUps[0].Charge, 0.2f);
            Assert.Less(windUps[0].Charge, 0.8f);
        }

        [Test]
        public void ASquadGap_SpacesSquadmatesThatFarApart_EdgeToEdge()
        {
            UnitStats elite = TestUnits.Unit(name: "Elite", radius: 0.6f, squadSize: 3);
            elite.SquadGap = 1.5f;
            List<Vec2> offsets = SquadFormation.Offsets(elite, 0.5f, false);
            Assert.AreEqual(0.6f * 2f + 1.5f, Vec2.Distance(offsets[0], offsets[1]), 1e-4f);
            UnitStats grunt = TestUnits.Unit(name: "Grunt", radius: 0.6f, squadSize: 3);
            Assert.AreEqual(0.6f * 2f + 0.5f, Vec2.Distance(SquadFormation.Offsets(grunt, 0.5f, false)[0], SquadFormation.Offsets(grunt, 0.5f, false)[1]), 1e-4f, "No gap of its own: the rules'.");
        }

        [Test]
        public void SquadmatesWithAGap_KeepItWhileTheyWalk()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats elite = TestUnits.Unit(name: "Elite", radius: 0.6f, speed: 3.5f, squadSize: 3, weapon: TestUnits.Weapon(damage: 1f, range: 5f));
            elite.SquadGap = 1.5f;
            sim.AddSquad(elite, Team.A, new Vec2(0f, -20f));
            sim.AddUnit(Target(0f), Team.B, new Vec2(0f, 20f));
            Run(sim, 5f);
            var bodies = new List<SimUnit>();
            foreach (SimUnit u in sim.Units) if (u.Team == Team.A) bodies.Add(u);
            for (int i = 0; i < bodies.Count; i++)
                for (int j = i + 1; j < bodies.Count; j++)
                    Assert.Greater(Vec2.Distance(bodies[i].Position, bodies[j].Position) - 1.2f, 1.2f, "Still well apart after 5 s of walking.");
        }
    }
}
