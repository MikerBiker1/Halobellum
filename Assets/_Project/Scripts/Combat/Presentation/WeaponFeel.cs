// WeaponFeel.cs
// What kind of thing a weapon is to the eye and ear, so that effects and sounds agree (M10). It is read from the weapon's name,
// falling back on what it does, so that a new weapon gets something sensible without any data. Looks and sound only.
// Depends on: Halobellum.Core.

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
            if (named.HasValue) return named.Value;
            return weapon.Range <= 3f ? WeaponFeelKind.Melee : WeaponFeelKind.Bullet;
        }

        /// <summary>The kind a name says, or null if it says nothing.</summary>
        public static WeaponFeelKind? FromName(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("crash") || n == "burst" || n.Contains("explo") || n.Contains("self")) return WeaponFeelKind.Burst;
            if (n.Contains("fuel rod bomb") || n.Contains("bomb")) return WeaponFeelKind.Bomb;
            if (n.Contains("fuel rod") || n.Contains("mortar") || n.Contains("assault cannon")) return WeaponFeelKind.PlasmaHeavy; // The Hunter's assault cannon is a heavy plasma gun, whatever its name says.
            if (n.Contains("needle")) return WeaponFeelKind.Needle;
            if (n.Contains("plasma")) return WeaponFeelKind.Plasma;
            if (n.Contains("flame") || n.Contains("fire") || n.Contains("m7057")) return WeaponFeelKind.Flame;
            if (n.Contains("laser")) return WeaponFeelKind.Laser;
            if (n.Contains("beam")) return WeaponFeelKind.Beam;
            if (n.Contains("gauss") || n.Contains("rail")) return WeaponFeelKind.Gauss;
            if (n.Contains("rocket") || n.Contains("missile")) return WeaponFeelKind.Rocket;
            if (n.Contains("autocannon")) return WeaponFeelKind.HeavyBullet; // Before "cannon", which it also contains.
            if (n.Contains("cannon") || n.Contains("grenade")) return WeaponFeelKind.Cannon;
            if (n.Contains("chaingun") || n.Contains("machine gun") || n.Contains("turret") || n.Contains("minigun")) return WeaponFeelKind.HeavyBullet;
            if (n.Contains("rifle") || n.Contains("smg") || n.Contains("pistol") || n.Contains("sniper")) return WeaponFeelKind.Bullet;
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

        public static string For(Team team) => team == Team.A ? TeamA : TeamB;

        public static void Set(string teamA, string teamB)
        {
            string a = string.IsNullOrEmpty(teamA) ? Unsc : teamA, b = string.IsNullOrEmpty(teamB) ? Covenant : teamB;
            if (a == TeamA && b == TeamB) return;
            TeamA = a;
            TeamB = b;
            Changed?.Invoke();
        }
    }
}
