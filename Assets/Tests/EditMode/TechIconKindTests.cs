// TechIconKindTests.cs
// Which icon a tech shows over its squads during deployment (D145): picked from what it does, the most telling effect first.

using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class TechIconKindTests
    {
        [Test]
        public void EachEffect_ShowsItsOwnIcon()
        {
            Assert.AreEqual(TechIconKind.Range, TechIconKinds.Of(new TechUpgrade { RangeBonus = 4f }));
            Assert.AreEqual(TechIconKind.FireRate, TechIconKinds.Of(new TechUpgrade { FireRateMultiplier = 1.25f }));
            Assert.AreEqual(TechIconKind.Damage, TechIconKinds.Of(new TechUpgrade { DamageMultiplier = 1.2f }));
            Assert.AreEqual(TechIconKind.Health, TechIconKinds.Of(new TechUpgrade { HealthMultiplier = 1.3f }));
            Assert.AreEqual(TechIconKind.Speed, TechIconKinds.Of(new TechUpgrade { MoveSpeedMultiplier = 1.2f }));
            Assert.AreEqual(TechIconKind.Shield, TechIconKinds.Of(new TechUpgrade { ShieldBonus = 100f }));
            Assert.AreEqual(TechIconKind.Splash, TechIconKinds.Of(new TechUpgrade { SplashRadiusBonus = 1f }));
            Assert.AreEqual(TechIconKind.Cone, TechIconKinds.Of(new TechUpgrade { ConeDegreesBonus = 15f }));
            Assert.AreEqual(TechIconKind.AntiAir, TechIconKinds.Of(new TechUpgrade { AddsAirTargeting = true }));
            Assert.AreEqual(TechIconKind.Drop, TechIconKinds.Of(new TechUpgrade { UnlocksReinforcement = true }));
            Assert.AreEqual(TechIconKind.ExtraShots, TechIconKinds.Of(new TechUpgrade { ExtraShotsPerAttack = 1 }));
            Assert.AreEqual(TechIconKind.Reload, TechIconKinds.Of(new TechUpgrade { ReloadTimeMultiplier = 0.8f }));
            Assert.AreEqual(TechIconKind.Other, TechIconKinds.Of(new TechUpgrade()));
        }

        [Test]
        public void ATechWithSeveralEffects_ShowsTheMostTellingOne()
        {
            Assert.AreEqual(TechIconKind.Range, TechIconKinds.Of(new TechUpgrade { RangeBonus = 3f, HealthMultiplier = 1.25f }));
            Assert.AreEqual(TechIconKind.AntiAir, TechIconKinds.Of(new TechUpgrade { AddsAirTargeting = true, DamageMultiplier = 1.2f }));
        }
    }
}
