// AudioCatalog.cs
// Every sound effect of a battle, with how it is played: its slot, which group it counts against (see VoiceLimiter), how loud,
// how much its pitch may wander, how much it matters when too much is playing at once. This is the place to tune the mix.
// A cue finds its clips the first time it is played. Nothing here allocates once a cue has been asked for, because the shots
// of a battle come through here. Sound only.
// Depends on: AudioSlots, AudioLibrary, WeaponFeel, Halobellum.Core (SpellStats).

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Presentation
{
    /// <summary>The groups that share a limit on how many may play at once.</summary>
    public enum SfxGroup
    {
        SmallArms,
        HeavyArms,
        BigGuns,
        Rockets,
        Flame,
        Plasma,
        HeavyPlasma,
        Needles,
        Beams,
        Melee,
        Bio,
        Impacts,
        Shields,
        Explosions,
        InfantryDeaths,
        BigDeaths,
        Bursts,
        Infection,
        Spells,
        Drops,
        Loops,
        Count,
    }

    /// <summary>One sound effect and how to play it.</summary>
    public sealed class SfxCue
    {
        public readonly string Slot;
        public readonly SfxGroup Group;
        public readonly float Volume;
        /// <summary>How far its pitch may wander either way (0.05 = 5%).</summary>
        public readonly float PitchSpread;
        /// <summary>Higher matters more: it may take the voice of something lower when all are busy.</summary>
        public readonly int Priority;
        private AudioClipSet _set;

        public SfxCue(string slot, SfxGroup group, float volume, float pitchSpread, int priority)
        {
            Slot = slot;
            Group = group;
            Volume = volume;
            PitchSpread = pitchSpread;
            Priority = priority;
        }

        public AudioClipSet Clips => _set ?? (_set = AudioLibrary.Get(Slot));

        public void Forget() => _set = null;
    }

    public static class AudioCatalog
    {
        private static readonly List<SfxCue> All = new List<SfxCue>();
        private static readonly Dictionary<string, SfxCue> BySlot = new Dictionary<string, SfxCue>();

        private static SfxCue Cue(string slot, SfxGroup group, float volume, float pitch, int priority)
        {
            var cue = new SfxCue(slot, group, volume, pitch, priority);
            All.Add(cue);
            BySlot[slot] = cue;
            return cue;
        }

        // Weapons. Each kind is a cue; a few kinds have a faction's own version (see Fire).
        private static readonly SfxCue FireBullet = Cue("sfx_fire_bullet", SfxGroup.SmallArms, 0.6f, 0.08f, 1);
        private static readonly SfxCue FireBulletFlood = Cue("sfx_fire_bullet_flood", SfxGroup.SmallArms, 0.6f, 0.1f, 1);
        private static readonly SfxCue FireHeavyBullet = Cue("sfx_fire_heavybullet", SfxGroup.HeavyArms, 0.7f, 0.06f, 2);
        private static readonly SfxCue FireCannon = Cue("sfx_fire_cannon", SfxGroup.BigGuns, 0.95f, 0.05f, 5);
        private static readonly SfxCue FireGauss = Cue("sfx_fire_gauss", SfxGroup.BigGuns, 0.9f, 0.04f, 5);
        private static readonly SfxCue FireRocket = Cue("sfx_fire_rocket", SfxGroup.Rockets, 0.8f, 0.07f, 4);
        private static readonly SfxCue FireBomb = Cue("sfx_fire_bomb", SfxGroup.Rockets, 0.85f, 0.06f, 4);
        private static readonly SfxCue FireFlame = Cue("sfx_fire_flame", SfxGroup.Flame, 0.65f, 0.08f, 2);
        private static readonly SfxCue FirePlasma = Cue("sfx_fire_plasma", SfxGroup.Plasma, 0.6f, 0.09f, 1);
        private static readonly SfxCue FirePlasmaHeavy = Cue("sfx_fire_plasmaheavy", SfxGroup.HeavyPlasma, 0.9f, 0.05f, 5);
        private static readonly SfxCue FireNeedle = Cue("sfx_fire_needle", SfxGroup.Needles, 0.55f, 0.1f, 1);
        private static readonly SfxCue FireLaser = Cue("sfx_fire_laser", SfxGroup.Beams, 1f, 0.02f, 7);
        private static readonly SfxCue FireBeam = Cue("sfx_fire_beam", SfxGroup.Beams, 1f, 0f, 8);
        private static readonly SfxCue FireMelee = Cue("sfx_fire_melee", SfxGroup.Melee, 0.6f, 0.09f, 1);
        private static readonly SfxCue FireMeleeCovenant = Cue("sfx_fire_melee_covenant", SfxGroup.Melee, 0.6f, 0.08f, 1);
        private static readonly SfxCue FireMeleeFlood = Cue("sfx_fire_melee_flood", SfxGroup.Melee, 0.6f, 0.1f, 1);
        private static readonly SfxCue FireBio = Cue("sfx_fire_bio", SfxGroup.Bio, 0.65f, 0.1f, 1);

        // What a hit sounds like.
        public static readonly SfxCue ImpactFlesh = Cue("sfx_impact_flesh", SfxGroup.Impacts, 0.4f, 0.1f, 0);
        public static readonly SfxCue ImpactArmor = Cue("sfx_impact_armor", SfxGroup.Impacts, 0.4f, 0.1f, 0);
        public static readonly SfxCue ImpactShield = Cue("sfx_impact_shield", SfxGroup.Shields, 0.5f, 0.1f, 0);
        public static readonly SfxCue ImpactBlock = Cue("sfx_impact_block", SfxGroup.Shields, 0.6f, 0.1f, 1);
        public static readonly SfxCue ImpactWet = Cue("sfx_impact_wet", SfxGroup.Impacts, 0.4f, 0.12f, 0);

        // Explosions by size, and deaths.
        public static readonly SfxCue ExplosionSmall = Cue("sfx_explosion_small", SfxGroup.Explosions, 0.75f, 0.08f, 3);
        public static readonly SfxCue ExplosionMedium = Cue("sfx_explosion_medium", SfxGroup.Explosions, 0.9f, 0.06f, 6);
        public static readonly SfxCue ExplosionLarge = Cue("sfx_explosion_large", SfxGroup.BigDeaths, 1f, 0.04f, 9);
        public static readonly SfxCue DeathInfantry = Cue("sfx_death_infantry", SfxGroup.InfantryDeaths, 0.55f, 0.1f, 2);
        public static readonly SfxCue DeathInfantryCovenant = Cue("sfx_death_infantry_covenant", SfxGroup.InfantryDeaths, 0.55f, 0.12f, 2);
        public static readonly SfxCue DeathFlood = Cue("sfx_death_flood", SfxGroup.InfantryDeaths, 0.6f, 0.12f, 2);
        public static readonly SfxCue DeathVehicle = Cue("sfx_death_vehicle", SfxGroup.BigDeaths, 0.95f, 0.05f, 8);
        public static readonly SfxCue DeathAir = Cue("sfx_death_air", SfxGroup.BigDeaths, 0.95f, 0.05f, 8);
        public static readonly SfxCue FloodBurst = Cue("sfx_flood_burst", SfxGroup.Bursts, 0.85f, 0.1f, 6);
        public static readonly SfxCue InfectionStart = Cue("sfx_infection_start", SfxGroup.Infection, 0.55f, 0.1f, 3);
        public static readonly SfxCue InfectionRise = Cue("sfx_infection_rise", SfxGroup.Infection, 0.75f, 0.1f, 4);
        public static readonly SfxCue PelicanDive = Cue("sfx_pelican_dive", SfxGroup.BigDeaths, 1f, 0.03f, 9);
        public static readonly SfxCue PelicanCrash = Cue("sfx_pelican_crash", SfxGroup.BigDeaths, 1f, 0.03f, 10);
        public static readonly SfxCue DropIn = Cue("sfx_drop_in", SfxGroup.Drops, 0.8f, 0.05f, 6);

        // Long loops, one for all the engines and one for all the hovering, at the level of how many are alive.
        public static readonly SfxCue HumEngine = Cue("sfx_hum_engine", SfxGroup.Loops, 0.5f, 0f, 0);
        public static readonly SfxCue HumHover = Cue("sfx_hum_hover", SfxGroup.Loops, 0.5f, 0f, 0);

        /// <summary>Every spell's slot (see AudioSlots.Spell).</summary>
        public static readonly string[] SpellSlots =
        {
            "sfx_spell_mac", "sfx_spell_nuke", "sfx_spell_glassing", "sfx_spell_missiles", "sfx_spell_plasma", "sfx_spell_storm_tick", "sfx_spell_spore", "sfx_spell_wrath",
            "sfx_spell_buff_unsc", "sfx_spell_buff_covenant", "sfx_spell_buff_flood", "sfx_spell_shield", "sfx_spell_drop_pods", "sfx_spell_drop_air", "sfx_spell_drop_flood",
            "sfx_spell_emp", "sfx_spell_smoke",
        };

        static AudioCatalog()
        {
            foreach (string slot in SpellSlots) Cue(slot, SfxGroup.Spells, 1f, 0.02f, 10);
        }

        public static IReadOnlyList<SfxCue> Cues => All;

        public static void SetLimits(VoiceLimiter limiter)
        {
            Limit(limiter, SfxGroup.SmallArms, 4, 0.07f);
            Limit(limiter, SfxGroup.HeavyArms, 3, 0.07f);
            Limit(limiter, SfxGroup.BigGuns, 3, 0.14f);
            Limit(limiter, SfxGroup.Rockets, 3, 0.12f);
            Limit(limiter, SfxGroup.Flame, 2, 0.2f);
            Limit(limiter, SfxGroup.Plasma, 4, 0.07f);
            Limit(limiter, SfxGroup.HeavyPlasma, 3, 0.15f);
            Limit(limiter, SfxGroup.Needles, 3, 0.06f);
            Limit(limiter, SfxGroup.Beams, 2, 0.2f);
            Limit(limiter, SfxGroup.Melee, 3, 0.08f);
            Limit(limiter, SfxGroup.Bio, 3, 0.07f);
            Limit(limiter, SfxGroup.Impacts, 3, 0.1f);
            Limit(limiter, SfxGroup.Shields, 2, 0.1f);
            Limit(limiter, SfxGroup.Explosions, 4, 0.1f);
            Limit(limiter, SfxGroup.InfantryDeaths, 3, 0.11f);
            Limit(limiter, SfxGroup.BigDeaths, 4, 0.1f);
            Limit(limiter, SfxGroup.Bursts, 3, 0.1f);
            Limit(limiter, SfxGroup.Infection, 3, 0.15f);
            Limit(limiter, SfxGroup.Spells, 4, 0.08f);
            Limit(limiter, SfxGroup.Drops, 2, 0.12f);
            Limit(limiter, SfxGroup.Loops, 2, 0f);
        }

        private static void Limit(VoiceLimiter limiter, SfxGroup group, int voices, float gap) => limiter.SetLimit((int)group, voices, gap);

        /// <summary>The cue of a slot, or null.</summary>
        public static SfxCue ForSlot(string slot) => slot != null && BySlot.TryGetValue(slot, out SfxCue cue) ? cue : null;

        /// <summary>What a weapon kind sounds like in the hands of a faction. Null: it has no firing sound (a burst is heard when the unit goes off).</summary>
        public static SfxCue Fire(WeaponFeelKind kind, string faction)
        {
            string key = AudioSlots.FactionKey(faction);
            switch (kind)
            {
                case WeaponFeelKind.Bullet: return key == "flood" ? FireBulletFlood : FireBullet;
                case WeaponFeelKind.HeavyBullet: return FireHeavyBullet;
                case WeaponFeelKind.Cannon: return FireCannon;
                case WeaponFeelKind.Gauss: return FireGauss;
                case WeaponFeelKind.Rocket: return FireRocket;
                case WeaponFeelKind.Bomb: return FireBomb;
                case WeaponFeelKind.Flame: return FireFlame;
                case WeaponFeelKind.Plasma: return FirePlasma;
                case WeaponFeelKind.PlasmaHeavy: return FirePlasmaHeavy;
                case WeaponFeelKind.Needle: return FireNeedle;
                case WeaponFeelKind.Laser: return FireLaser;
                case WeaponFeelKind.Beam: return FireBeam;
                case WeaponFeelKind.Melee: return key == "flood" ? FireMeleeFlood : key == "covenant" ? FireMeleeCovenant : FireMelee;
                case WeaponFeelKind.Bio: return FireBio;
                default: return null;
            }
        }

        /// <summary>The explosion a shot from this weapon makes where it lands, or null. A plasma shot with a blast is drawn going off, so it is heard going off too.</summary>
        public static SfxCue LandingOf(WeaponStats weapon)
        {
            WeaponFeelKind kind = WeaponFeel.For(weapon);
            return kind == WeaponFeelKind.Plasma && weapon.SplashRadius > 0f ? ExplosionSmall : LandingOf(kind);
        }

        /// <summary>The explosion a hit from this weapon kind makes where it lands, or null.</summary>
        public static SfxCue LandingOf(WeaponFeelKind kind)
        {
            switch (kind)
            {
                case WeaponFeelKind.Rocket:
                case WeaponFeelKind.Bomb:
                case WeaponFeelKind.Cannon:
                case WeaponFeelKind.PlasmaHeavy: return ExplosionSmall;
                default: return null;
            }
        }

        /// <summary>The cue of a spell's effect, or null if this effect makes no new sound.</summary>
        public static SfxCue Spell(SpellStats spell, int tick, string faction) => ForSlot(AudioSlots.Spell(spell, tick, faction));

        /// <summary>Forgets the clips of every cue, so that they are looked up again.</summary>
        public static void ForgetClips()
        {
            foreach (SfxCue cue in All) cue.Forget();
        }
    }
}
