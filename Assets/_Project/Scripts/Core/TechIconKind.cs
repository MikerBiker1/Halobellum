// TechIconKind.cs
// Which small icon a tech shows over its squads during deployment (D145): one kind per effect, picked from what the tech does.
// A tech with several effects shows its most telling one, in this order: it brings a squad mid-battle, it shoots air, it fires
// more shots an attack, its flame's cone, its splash, a shield, range, rate of fire, damage, health, speed, reload; anything else
// shows the gear. The pictures themselves are drawn by TechIcons (Scripts/UI).
// Depends on: TechUpgrade.

namespace Halobellum.Core
{
    public enum TechIconKind
    {
        Range, FireRate, Damage, Health, Speed, Shield, Splash, Cone, AntiAir, Drop, ExtraShots, Reload, Other,
    }

    public static class TechIconKinds
    {
        /// <summary>The icon for this tech (see the header for the order).</summary>
        public static TechIconKind Of(TechUpgrade tech)
        {
            if (tech == null) return TechIconKind.Other;
            if (tech.UnlocksReinforcement) return TechIconKind.Drop;
            if (tech.AddsAirTargeting || tech.AirDamageMultiplier > 1f || tech.AirRangeBonus > 0f) return TechIconKind.AntiAir;
            if (tech.ExtraShotsPerAttack > 0) return TechIconKind.ExtraShots;
            if (tech.ConeDegreesBonus > 0f) return TechIconKind.Cone;
            if (tech.SplashRadiusBonus > 0f) return TechIconKind.Splash;
            if (tech.ShieldBonus > 0f || tech.ShieldRegenBonus > 0f) return TechIconKind.Shield;
            if (tech.RangeBonus > 0f) return TechIconKind.Range;
            if (tech.FireRateMultiplier > 1f) return TechIconKind.FireRate;
            if (tech.DamageMultiplier > 1f) return TechIconKind.Damage;
            if (tech.HealthMultiplier > 1f) return TechIconKind.Health;
            if (tech.MoveSpeedMultiplier > 1f) return TechIconKind.Speed;
            if (tech.ReloadTimeMultiplier < 1f) return TechIconKind.Reload;
            return TechIconKind.Other;
        }
    }
}
