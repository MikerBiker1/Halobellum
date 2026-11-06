// WeaponFeel.cs
// What kind of thing a weapon is to the eye and ear, so that effects and sounds agree (M10). It is read from the weapon's name,
// falling back on what it does, so that a new weapon gets something sensible without any data. Looks and sound only.
// BattleFactions below says which faction a side looks like. In Unlock Skirmish both sides stand on one land, and each unit
// looks like its own real faction; because both sides then count as the same faction, Team B's shots keep their tint.
// Depends on: Halobellum.Core.

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Presentation
{
    public enum WeaponFeelKind
    {
        Bullet,
        HeavyBullet,
        Cannon,
        Gauss,
        Rocket,
        Bomb,
        Flame,
        Plasma,
        PlasmaHeavy,
        Needle,
        Laser,
        Beam,
        Melee,
        Bio,
        Burst,
    }

    public static class WeaponFeel
    {
        public static WeaponFeelKind For(WeaponStats weapon)
        {
            if (weapon.IsBeam) return WeaponFeelKind.Beam;
            WeaponFeelKind? named = FromName(weapon.Name);
            // A plasma shot that flies and bursts is a heavy gun, not a pistol (the Blisterback's missiles, the Marauder's cannon).
            if (named == WeaponFeelKind.Plasma && !weapon.IsInstant && weapon.SplashRadius > 0f) return WeaponFeelKind.PlasmaHeavy;
            if (named.HasValue) return named.Value;
            return weapon.Range <= 3f ? WeaponFeelKind.Melee : WeaponFeelKind.Bullet;
        }

        /// <summary>The kind a name says, or null if it says nothing.</summary>
        public static WeaponFeelKind? FromName(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("crash") || n == "burst" || n.Contains("explo") || n.Contains("self")) return WeaponFeelKind.Burst;
            if (n.Contains("fuel rod bomb") || n.Contains("bomb") || n.Contains("methane")) return WeaponFeelKind.Bomb; // The Methane Wagon's mortar lobs burning kegs, not plasma.
            if (n.Contains("fuel rod") || n.Contains("mortar") || n.Contains("assault cannon")) return WeaponFeelKind.PlasmaHeavy; // The Hunter's assault cannon is a heavy plasma gun, whatever its name says.
            if (n.Contains("needle")) return WeaponFeelKind.Needle;
            if (n.Contains("plasma")) return WeaponFeelKind.Plasma;
            if (n.Contains("flame") || n.Contains("fire") || n.Contains("m7057")) return WeaponFeelKind.Flame;
            if (n.Contains("laser")) return WeaponFeelKind.Laser;
            if (n.Contains("beam rifle")) return WeaponFeelKind.Gauss; // A sniper's one hard shot in a straight line (the Jackal's), not a Scarab's beam.
            if (n.Contains("beam")) return WeaponFeelKind.Beam;
            if (n.Contains("gauss") || n.Contains("rail") || n.Contains("sniper")) return WeaponFeelKind.Gauss; // One hard shot and a straight line. Before "rifle", which a sniper rifle also contains.
            if (n.Contains("rocket") || n.Contains("missile")) return WeaponFeelKind.Rocket;
            if (n.Contains("autocannon")) return WeaponFeelKind.HeavyBullet; // Before "cannon", which it also contains.
            if (n.Contains("cannon") || n.Contains("grenade")) return WeaponFeelKind.Cannon;
            if (n.Contains("chaingun") || n.Contains("machine gun") || n.Contains("turret") || n.Contains("minigun") || n.Contains("chin gun") || n.Contains("shotgun")) return WeaponFeelKind.HeavyBullet;
            if (n.Contains("rifle") || n.Contains("smg") || n.Contains("pistol")) return WeaponFeelKind.Bullet;
            if (n.Contains("bone") || n.Contains("spore") || n.Contains("spit")) return WeaponFeelKind.Bio;
            if (n.Contains("sword") || n.Contains("claw") || n.Contains("talon") || n.Contains("slam") || n.Contains("whip") || n.Contains("bite") || n.Contains("hammer") || n.Contains("melee") || n.Contains("punch")) return WeaponFeelKind.Melee;
            return null;
        }
    }

    /// <summary>Which faction each side of the shown battle belongs to, for terrain, effects and sound. Looks and sound only.</summary>
    public static class BattleFactions
    {
        public const string Unsc = "UNSC", Covenant = "Covenant", Flood = "Flood";

        /// <summary>Raised when either side's faction changes.</summary>
        public static event System.Action Changed;

        public static string TeamA { get; private set; } = Unsc;
        public static string TeamB { get; private set; } = Covenant;

        /// <summary>Each unit's own faction by name, in Unlock Skirmish; otherwise null.</summary>
        private static IReadOnlyDictionary<string, string> _factionOfUnit;

        /// <summary>True in Unlock Skirmish: a unit looks and sounds like its own faction, not its side's.</summary>
        public static bool PerUnitLooks => _factionOfUnit != null;

        public static string For(Team team) => team == Team.A ? TeamA : TeamB;

        /// <summary>The faction a unit looks and sounds like: its own in Unlock Skirmish, if it has one, otherwise its side's.</summary>
        public static string OfUnit(UnitStats stats, Team team)
        {
            if (_factionOfUnit == null || stats == null) return For(team);
            // A risen body's name ("Infected Marine") is in no roster, and whatever it was before, it is the Flood's now.
            if (stats.IsFlood || stats.InfectedFrom != null) return Flood;
            return _factionOfUnit.TryGetValue(stats.Name, out string faction) ? faction : For(team);
        }

        public static void Set(string teamA, string teamB)
        {
            // This class is static, so a Skirmish's map would otherwise outlive its match.
            bool hadMap = PerUnitLooks;
            _factionOfUnit = null;
            string a = string.IsNullOrEmpty(teamA) ? Unsc : teamA, b = string.IsNullOrEmpty(teamB) ? Covenant : teamB;
            if (a == TeamA && b == TeamB && !hadMap) return;
            TeamA = a;
            TeamB = b;
            Changed?.Invoke();
        }

        /// <summary>Unlock Skirmish: both sides stand on one land, and each unit has its own faction.</summary>
        public static void SetSkirmish(string land, IReadOnlyDictionary<string, string> factionOfUnit)
        {
            bool hadMap = PerUnitLooks;
            _factionOfUnit = factionOfUnit;
            string both = string.IsNullOrEmpty(land) ? Unsc : land;
            if (both == TeamA && both == TeamB && hadMap) return;
            TeamA = both;
            TeamB = both;
            Changed?.Invoke();
        }
    }
}
