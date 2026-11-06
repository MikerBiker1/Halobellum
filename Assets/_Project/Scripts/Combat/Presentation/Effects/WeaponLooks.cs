// WeaponLooks.cs
// What each kind of weapon looks like when it fires (M10): colours, sizes and how fast a shot appears to fly. Colour says who fired:
// UNSC shots are warm yellow, Covenant plasma blue with green fuel rods, pink needles and pink-purple beam rifles, Flood shots a sick green. If both sides
// could fire the same colour (a mirror match, or the same unit type on both sides in the sandbox), the second side's are pushed to red.
// Also here: what kind of death a unit gets, and what each spell looks like. All of it is plain data with no Unity objects but Color,
// read from WeaponFeel and the unit's stats, so it can be tested. Looks only.
// Depends on: WeaponFeel, Halobellum.Core, UnityEngine (Color).

using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    /// <summary>What a shot does when it lands.</summary>
    public enum ImpactStyle
    {
        /// <summary>Sparks on armour, a puff of dust on anything softer.</summary>
        Bullet,
        Plasma,
        Needle,
        Laser,
        Bio,
        Slash,
        None,
    }

    /// <summary>What kind of blast a splash weapon makes.</summary>
    public enum BlastStyle
    {
        Fire,
        Plasma,
        Bio,
        /// <summary>A ground slam: dust and a ring, no fire.</summary>
        Dust,
    }

    public struct WeaponLook
    {
        public WeaponFeelKind Kind;
        public Color Shot;
        public Color Flash;
        /// <summary>Metres across, at the normal game camera.</summary>
        public float FlashSize;
        public float ShotWidth;
        /// <summary>How fast a shot the simulation hits with at once appears to fly, metres per second.</summary>
        public float DrawnSpeed;
        public ImpactStyle Impact;
        public BlastStyle Blast;
        /// <summary>A heavy gun: smoke and sparks at the muzzle too.</summary>
        public bool HeavyMuzzle;
        /// <summary>A big glowing ball in flight rather than a streak.</summary>
        public bool Ball;
        /// <summary>Leaves a smoke trail.</summary>
        public bool SmokeTrail;
    }

    public static class WeaponLooks
    {
        public static readonly Color UnscTracer = new Color(1f, 0.84f, 0.42f);
        public static readonly Color FloodTracer = new Color(0.78f, 1f, 0.32f);
        public static readonly Color CovenantPlasma = new Color(0.3f, 0.62f, 1f);
        public static readonly Color FloodPlasma = new Color(0.62f, 1f, 0.2f);
        public static readonly Color FuelRodGreen = new Color(0.3f, 1f, 0.38f);
        public static readonly Color MortarBlue = new Color(0.4f, 0.55f, 1f);
        public static readonly Color NeedlePink = new Color(1f, 0.3f, 0.78f);
        public static readonly Color BeamRiflePink = new Color(0.95f, 0.42f, 1f);
        public static readonly Color LaserRed = new Color(1f, 0.1f, 0.12f);
        public static readonly Color GaussWhite = new Color(0.72f, 0.9f, 1f);
        public static readonly Color RocketOrange = new Color(1f, 0.62f, 0.22f);
        public static readonly Color BurningKeg = new Color(1f, 0.48f, 0.1f);
        public static readonly Color ScarabBeam = new Color(0.42f, 0.78f, 1f);
        public static readonly Color Bone = new Color(1f, 0.92f, 0.72f);
        public static readonly Color MirrorRed = new Color(1f, 0.2f, 0.16f);

        /// <summary>The look of one weapon on one unit, for the side that fires it.</summary>
        /// <param name="mirror">True if both sides could otherwise fire the same colours.</param>
        public static WeaponLook For(WeaponStats weapon, UnitStats shooter, string faction, Team team, bool mirror)
        {
            WeaponFeelKind kind = WeaponFeel.For(weapon);
            // A cannon the simulation hits with at once can't have a slow shell in flight.
            if (kind == WeaponFeelKind.Cannon && weapon.IsInstant) kind = WeaponFeelKind.HeavyBullet;
            string name = (weapon.Name ?? "").ToLowerInvariant();
            bool flood = (shooter != null && shooter.IsFlood) || (shooter != null && shooter.InfectedFrom != null) || faction == BattleFactions.Flood;
            WeaponLook look = Base(kind, name, flood);
            look.Kind = kind;
            if (mirror && team == Team.B)
            {
                look.Shot = Mirrored(look.Shot);
                look.Flash = Mirrored(look.Flash);
            }
            return look;
        }

        /// <summary>The second side's colour when both sides would otherwise look the same.</summary>
        public static Color Mirrored(Color color) => Color.Lerp(color, MirrorRed, 0.55f);

        private static WeaponLook Base(WeaponFeelKind kind, string name, bool flood)
        {
            Color tracer = flood ? FloodTracer : UnscTracer;
            Color plasma = flood ? FloodPlasma : CovenantPlasma;
            switch (kind)
            {
                case WeaponFeelKind.HeavyBullet:
                    if (name.Contains("shotgun"))
                        return new WeaponLook { Shot = tracer, Flash = tracer, FlashSize = 2.6f, ShotWidth = 0.34f, DrawnSpeed = 90f, Impact = ImpactStyle.Bullet, HeavyMuzzle = true };
                    return new WeaponLook { Shot = tracer, Flash = tracer, FlashSize = 1.7f, ShotWidth = 0.2f, DrawnSpeed = 130f, Impact = ImpactStyle.Bullet };
                case WeaponFeelKind.Cannon:
                    return new WeaponLook { Shot = new Color(1f, 0.78f, 0.42f), Flash = new Color(1f, 0.75f, 0.4f), FlashSize = 4.5f, ShotWidth = 0.42f, Impact = ImpactStyle.Bullet, Blast = BlastStyle.Fire, HeavyMuzzle = true, SmokeTrail = true };
                case WeaponFeelKind.Gauss:
                    // A sniper's round is a gun's, not a railgun's: the same straight line, but thin and the colour of its side's tracers.
                    if (name.Contains("sniper"))
                        return new WeaponLook { Shot = Color.Lerp(tracer, Color.white, 0.45f), Flash = tracer, FlashSize = 2.4f, ShotWidth = 0.2f, Impact = ImpactStyle.Bullet, HeavyMuzzle = true };
                    // A Covenant beam rifle: the same thin straight line, bright pink-purple, and it burns where it lands.
                    if (name.Contains("beam rifle"))
                        return new WeaponLook { Shot = Color.Lerp(BeamRiflePink, Color.white, 0.3f), Flash = BeamRiflePink, FlashSize = 2f, ShotWidth = 0.17f, Impact = ImpactStyle.Plasma };
                    return new WeaponLook { Shot = GaussWhite, Flash = GaussWhite, FlashSize = 3.5f, ShotWidth = 0.34f, Impact = ImpactStyle.Bullet, HeavyMuzzle = true };
                case WeaponFeelKind.Rocket:
                    return new WeaponLook { Shot = RocketOrange, Flash = RocketOrange, FlashSize = 2.2f, ShotWidth = 0.5f, Impact = ImpactStyle.Bullet, Blast = BlastStyle.Fire, SmokeTrail = true };
                case WeaponFeelKind.Bomb:
                    // A burning keg, dripping embers on its way down.
                    if (name.Contains("methane"))
                        return new WeaponLook { Shot = BurningKeg, Flash = RocketOrange, FlashSize = 2f, ShotWidth = 1.1f, Impact = ImpactStyle.Bullet, Blast = BlastStyle.Fire, Ball = true };
                    if (name.Contains("fuel rod"))
                        return new WeaponLook { Shot = flood ? FloodPlasma : FuelRodGreen, Flash = flood ? FloodPlasma : FuelRodGreen, FlashSize = 1.8f, ShotWidth = 1.5f, Impact = ImpactStyle.Plasma, Blast = BlastStyle.Plasma, Ball = true };
                    // Flood bombs (Spore Bombs) burst in gore and spores, not fire.
                    if (flood)
                        return new WeaponLook { Shot = FloodPlasma, Flash = FloodPlasma, FlashSize = 1.2f, ShotWidth = 0.55f, Impact = ImpactStyle.Bio, Blast = BlastStyle.Bio };
                    return new WeaponLook { Shot = RocketOrange, Flash = RocketOrange, FlashSize = 1.2f, ShotWidth = 0.55f, Impact = ImpactStyle.Bullet, Blast = BlastStyle.Fire, SmokeTrail = true };
                case WeaponFeelKind.Flame:
                    return new WeaponLook { Shot = new Color(1f, 0.6f, 0.2f), Flash = new Color(1f, 0.65f, 0.25f), FlashSize = 1.6f, ShotWidth = 0.4f, Impact = ImpactStyle.None };
                case WeaponFeelKind.Plasma:
                    return new WeaponLook { Shot = plasma, Flash = plasma, FlashSize = 1.5f, ShotWidth = 0.55f, DrawnSpeed = 60f, Impact = ImpactStyle.Plasma, Blast = BlastStyle.Plasma };
                case WeaponFeelKind.PlasmaHeavy:
                    Color heavy = flood ? FloodPlasma : name.Contains("mortar") ? MortarBlue : name.Contains("plasma") ? plasma : FuelRodGreen;
                    return new WeaponLook { Shot = heavy, Flash = heavy, FlashSize = 2.8f, ShotWidth = name.Contains("mortar") ? 2.6f : 1.7f, Impact = ImpactStyle.Plasma, Blast = BlastStyle.Plasma, Ball = true };
                case WeaponFeelKind.Needle:
                    return new WeaponLook { Shot = NeedlePink, Flash = NeedlePink, FlashSize = 1.4f, ShotWidth = 0.3f, DrawnSpeed = 75f, Impact = ImpactStyle.Needle };
                case WeaponFeelKind.Laser:
                    return new WeaponLook { Shot = LaserRed, Flash = LaserRed, FlashSize = 2.4f, ShotWidth = 0.5f, Impact = ImpactStyle.Laser };
                case WeaponFeelKind.Beam:
                    return new WeaponLook { Shot = flood ? FloodPlasma : ScarabBeam, Flash = flood ? FloodPlasma : ScarabBeam, FlashSize = 3f, ShotWidth = 1.6f, Impact = ImpactStyle.None };
                case WeaponFeelKind.Melee:
                    if (name.Contains("sword"))
                        return new WeaponLook { Shot = new Color(0.45f, 0.75f, 1f), Flash = new Color(0.45f, 0.75f, 1f), FlashSize = 3f, ShotWidth = 3f, Impact = ImpactStyle.Slash, Blast = BlastStyle.Dust };
                    return new WeaponLook { Shot = flood ? new Color(0.85f, 1f, 0.55f) : new Color(1f, 0.95f, 0.8f), Flash = Color.white, FlashSize = 2f, ShotWidth = 2f, Impact = ImpactStyle.Slash, Blast = BlastStyle.Dust };
                case WeaponFeelKind.Bio:
                    return new WeaponLook { Shot = Bone, Flash = new Color(0.72f, 0.9f, 0.3f), FlashSize = 1.1f, ShotWidth = 0.34f, DrawnSpeed = 45f, Impact = ImpactStyle.Bio, Blast = BlastStyle.Bio, SmokeTrail = false };
                case WeaponFeelKind.Burst:
                    return new WeaponLook { Shot = FloodPlasma, Flash = FloodPlasma, FlashSize = 0f, ShotWidth = 0f, Impact = ImpactStyle.None, Blast = BlastStyle.Bio };
                default:
                    return new WeaponLook { Shot = tracer, Flash = tracer, FlashSize = 1.1f, ShotWidth = 0.13f, DrawnSpeed = 110f, Impact = ImpactStyle.Bullet };
            }
        }
    }

    /// <summary>The ways a unit can die on screen.</summary>
    public enum DeathStyle
    {
        /// <summary>It falls with a puff of dust.</summary>
        Infantry,
        /// <summary>Fireball, smoke, sparks, debris, a scorch mark and a burning wreck.</summary>
        Vehicle,
        /// <summary>Blows up in the air and falls burning to the ground.</summary>
        Air,
        /// <summary>Bursts in gore and spores.</summary>
        FloodBurst,
        /// <summary>A flying Flood body that bursts in the air (Swarm Form).</summary>
        FloodAirBurst,
        /// <summary>A big Flood flier: blows up and falls burning, spilling spores (Infected Phantom).</summary>
        FloodAir,
        /// <summary>About a titan's blast: a flier with a death burst comes down on the enemy (Infected Pelican).</summary>
        CrashBurst,
    }

    public static class DeathLooks
    {
        public static DeathStyle For(UnitStats stats)
        {
            bool flood = stats.IsFlood || stats.InfectedFrom != null;
            if (stats.IsAir)
            {
                if (stats.DeathBurst != null && stats.DeathBurst.Radius > 0f) return DeathStyle.CrashBurst;
                if (flood) return stats.CollisionRadius >= 2f ? DeathStyle.FloodAir : DeathStyle.FloodAirBurst;
                return DeathStyle.Air;
            }
            if (stats.IsFlood) return DeathStyle.FloodBurst;
            return stats.ArmorType == ArmorType.Unarmored ? DeathStyle.Infantry : DeathStyle.Vehicle;
        }

        /// <summary>A body so big that it goes up in a chain of blasts.</summary>
        public static bool IsTitan(UnitStats stats) => stats.Role == UnitRole.Titan || stats.CollisionRadius >= 4f;
    }

    /// <summary>What a spell looks like when it goes off. Spells are told apart by name, then by what they do.</summary>
    public enum SpellLook
    {
        OrbitalStrike,
        Nuke,
        PlasmaStrike,
        PlasmaStorm,
        Glassing,
        MissileRain,
        BioEruption,
        BioBurst,
        BlightRain,
        Emp,
        SporeWave,
        Buff,
        Smoke,
        Drop,
    }

    public static class SpellLooks
    {
        public static SpellLook For(SpellStats stats)
        {
            string name = (stats.Name ?? "").ToLowerInvariant();
            switch (stats.Effect)
            {
                case SpellEffect.Buff: return SpellLook.Buff;
                case SpellEffect.Smoke:
                case SpellEffect.Oil: return SpellLook.Smoke;
                case SpellEffect.DropSquad: return SpellLook.Drop;
                case SpellEffect.Disrupt: return name.Contains("spore") ? SpellLook.SporeWave : SpellLook.Emp;
            }
            if (name.Contains("mac")) return SpellLook.OrbitalStrike;
            if (name.Contains("nuke") || name.Contains("havok")) return SpellLook.Nuke;
            if (name.Contains("glass")) return SpellLook.Glassing;
            if (name.Contains("storm")) return SpellLook.PlasmaStorm;
            if (name.Contains("plasma")) return SpellLook.PlasmaStrike;
            if (name.Contains("missile")) return SpellLook.MissileRain;
            if (name.Contains("gravemind")) return SpellLook.BioEruption;
            if (name.Contains("spore")) return SpellLook.BioBurst;
            if (name.Contains("blight")) return SpellLook.BlightRain;

            // A spell that is new to these names: go by what it does.
            bool energy = stats.DamageType == DamageType.Energy;
            if (stats.InfectsKills) return stats.Ticks > 1 ? SpellLook.BlightRain : stats.Radius >= 10f ? SpellLook.BioEruption : SpellLook.BioBurst;
            if (stats.Ticks > 1) return energy ? SpellLook.PlasmaStorm : SpellLook.MissileRain;
            if (energy) return SpellLook.PlasmaStrike;
            return stats.Radius >= 10f ? SpellLook.Nuke : SpellLook.OrbitalStrike;
        }
    }
}
