// AudioCatalog.cs
// Every sound effect of a battle, with how it is played: its slot, which group it counts against (see VoiceLimiter), how loud,
// how much its pitch may wander, how much it matters when too much is playing at once. This is the place to tune the mix.
// Each cue also says how loud its loudest moment should be (Loudness, in dB below full scale, about where its generated sound sits):
// an owner's file is turned to that level, as they come far louder than the game's mix (AudioClipSet.Gain).
// Some sounds belong to one weapon or unit and are the owner's only (OwnerOnly): they have no generated sound, and until their file is
// in the folder the weapon's kind is heard as before (FireFor, LandingFor, BattleAudio.DeathCue). A few loop while their guns fire
// rather than playing once a shot (LoopsWhileFiring; BattleAudio plays them). The end of a round's nodes of light have the owner's
// sounds too, by size (NodeHit), and the Glassing Beam's long sound starts ahead of the beam so that it strikes with it (SpellLeadSeconds).
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
        Lasers,
        Beams,
        Melee,
        Bio,
        Explosions,
        InfantryDeaths,
        BigDeaths,
        Bursts,
        Infection,
        Spells,
        Drops,
        Loops,
        Calls,
        Ready,
        ShieldRegen,
        PlasmaTurrets,
        Nodes,
        /// <summary>The SMG's shots, one a shot (D117): a group of their own, so that ten a second don't take every small arms voice.</summary>
        Smg,
        /// <summary>Aircraft falling as they die (D117).</summary>
        AirFalls,
        /// <summary>UNSC rockets and bombs heard coming down before they land (D118).</summary>
        Incoming,
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
        /// <summary>Where an owner's file is turned to: its loudest 50 ms, in dB below full scale.</summary>
        public readonly float Loudness;
        /// <summary>The owner's file loops for as long as its guns keep firing, rather than playing once a shot.</summary>
        public readonly bool LoopsWhileFiring;
        /// <summary>For a file with a long quiet tail: after this many seconds a voice stops counting against its group's limit, though its tail rings on (0: the whole clip).</summary>
        public readonly float HoldSeconds;
        private AudioClipSet _set;

        public SfxCue(string slot, SfxGroup group, float volume, float pitchSpread, int priority, float loudness = -15f, bool loopsWhileFiring = false, float holdSeconds = 0f)
        {
            HoldSeconds = holdSeconds;
            Slot = slot;
            Group = group;
            Volume = volume;
            PitchSpread = pitchSpread;
            Priority = priority;
            Loudness = loudness;
            LoopsWhileFiring = loopsWhileFiring;
        }

        /// <summary>True if the owner's file is in, and loops while its guns fire.</summary>
        public bool Loops => LoopsWhileFiring && Clips.IsOverride && !Clips.IsEmpty;

        /// <summary>True if this cue has something to play.</summary>
        public bool HasSound => !Clips.IsEmpty;

        public AudioClipSet Clips => _set ?? (_set = AudioLibrary.Get(Slot));

        public void Forget() => _set = null;
    }

    public static class AudioCatalog
    {
        private static readonly List<SfxCue> All = new List<SfxCue>();
        private static readonly Dictionary<string, SfxCue> BySlot = new Dictionary<string, SfxCue>();

        private static readonly List<SfxCue> Owners = new List<SfxCue>();

        private static SfxCue Cue(string slot, SfxGroup group, float volume, float pitch, int priority, float loudness = -15f, bool loops = false)
        {
            var cue = new SfxCue(slot, group, volume, pitch, priority, loudness, loops);
            All.Add(cue);
            BySlot[slot] = cue;
            return cue;
        }

        /// <summary>A sound of one weapon or unit that only the owner's file gives: until it is in, the weapon's kind is heard instead.</summary>
        private static SfxCue OwnerCue(string slot, SfxGroup group, float volume, float pitch, int priority, float loudness, bool loops = false, float hold = 0f)
        {
            var cue = new SfxCue(slot, group, volume, pitch, priority, loudness, loops, hold);
            Owners.Add(cue);
            BySlot[slot] = cue;
            return cue;
        }

        /// <summary>
        /// A slot waiting for the owner's own file: it has no generated sound, so it stands outside the list of Cues, which all have one.
        /// Until a file is dropped in, nothing asks for it (see BattleAudio.DeathCue).
        /// </summary>
        private static SfxCue WaitingCue(string slot, SfxGroup group, float volume, float pitch, int priority, float loudness = -15f)
        {
            var cue = new SfxCue(slot, group, volume, pitch, priority, loudness);
            BySlot[slot] = cue;
            return cue;
        }

        // Weapons. Each kind is a cue; a few kinds have a faction's own version (see Fire).
        private static readonly SfxCue FireBullet = Cue("sfx_fire_bullet", SfxGroup.SmallArms, 0.6f, 0.08f, 1, -16.5f);
        private static readonly SfxCue FireBulletFlood = Cue("sfx_fire_bullet_flood", SfxGroup.SmallArms, 0.6f, 0.1f, 1, -16f);
        private static readonly SfxCue FireHeavyBullet = Cue("sfx_fire_heavybullet", SfxGroup.HeavyArms, 0.7f, 0.06f, 2, -14.6f);
        private static readonly SfxCue FireCannon = Cue("sfx_fire_cannon", SfxGroup.BigGuns, 0.95f, 0.05f, 5, -12.4f);
        private static readonly SfxCue FireGauss = Cue("sfx_fire_gauss", SfxGroup.BigGuns, 0.9f, 0.04f, 5, -12.7f);
        private static readonly SfxCue FireRocket = Cue("sfx_fire_rocket", SfxGroup.Rockets, 0.8f, 0.07f, 4, -15.2f);
        private static readonly SfxCue FireBomb = Cue("sfx_fire_bomb", SfxGroup.Rockets, 0.85f, 0.06f, 4, -12.2f);
        private static readonly SfxCue FireFlame = Cue("sfx_fire_flame", SfxGroup.Flame, 0.65f, 0.08f, 2, -18.8f, loops: true);
        private static readonly SfxCue FirePlasma = Cue("sfx_fire_plasma", SfxGroup.Plasma, 0.6f, 0.09f, 1, -18.8f);
        private static readonly SfxCue FirePlasmaHeavy = Cue("sfx_fire_plasmaheavy", SfxGroup.HeavyPlasma, 0.9f, 0.05f, 5, -14f);
        private static readonly SfxCue FireNeedle = Cue("sfx_fire_needle", SfxGroup.Needles, 0.55f, 0.1f, 1, -19.6f);
        private static readonly SfxCue FireLaser = Cue("sfx_fire_laser", SfxGroup.Lasers, 1f, 0.02f, 7, -14f);
        private static readonly SfxCue FireBeam = Cue("sfx_fire_beam", SfxGroup.Beams, 1f, 0f, 8, -12.5f);
        private static readonly SfxCue FireMelee = Cue("sfx_fire_melee", SfxGroup.Melee, 0.6f, 0.09f, 1, -18.3f);
        private static readonly SfxCue FireMeleeCovenant = Cue("sfx_fire_melee_covenant", SfxGroup.Melee, 0.6f, 0.08f, 1, -19.2f);
        private static readonly SfxCue FireMeleeFlood = Cue("sfx_fire_melee_flood", SfxGroup.Melee, 0.6f, 0.1f, 1, -17.5f);
        private static readonly SfxCue FireBio = Cue("sfx_fire_bio", SfxGroup.Bio, 0.65f, 0.1f, 1, -18.2f);

        // Hits have no sound (the generated stand-ins were taken out, the owner, 2026-11-07, D82).

        // Explosions by size, and deaths.
        public static readonly SfxCue ExplosionSmall = Cue("sfx_explosion_small", SfxGroup.Explosions, 0.75f, 0.08f, 3, -14.3f);
        public static readonly SfxCue ExplosionMedium = Cue("sfx_explosion_medium", SfxGroup.Explosions, 0.9f, 0.06f, 6, -11.7f);
        public static readonly SfxCue ExplosionLarge = Cue("sfx_explosion_large", SfxGroup.BigDeaths, 1f, 0.04f, 9, -7.5f);
        public static readonly SfxCue DeathInfantry = Cue("sfx_death_infantry", SfxGroup.InfantryDeaths, 0.55f, 0.1f, 2, -21.2f);
        public static readonly SfxCue DeathInfantryCovenant = Cue("sfx_death_infantry_covenant", SfxGroup.InfantryDeaths, 0.55f, 0.12f, 2, -20f);
        public static readonly SfxCue DeathFlood = Cue("sfx_death_flood", SfxGroup.InfantryDeaths, 0.6f, 0.12f, 2, -17.6f);
        public static readonly SfxCue DeathVehicle = Cue("sfx_death_vehicle", SfxGroup.BigDeaths, 0.95f, 0.05f, 8, -11.8f);
        public static readonly SfxCue DeathAir = Cue("sfx_death_air", SfxGroup.BigDeaths, 0.95f, 0.05f, 8, -11.3f);
        public static readonly SfxCue FloodBurst = Cue("sfx_flood_burst", SfxGroup.Bursts, 0.85f, 0.1f, 6, -13f);
        /// <summary>A Hunter's or a Goliath's death, once the owner has a sound for it.</summary>
        public static readonly SfxCue DeathLekgolo = WaitingCue("sfx_death_lekgolo", SfxGroup.BigDeaths, 0.95f, 0.05f, 8, -11f);
        public static readonly SfxCue InfectionStart = Cue("sfx_infection_start", SfxGroup.Infection, 0.55f, 0.1f, 3, -22f);
        public static readonly SfxCue InfectionRise = Cue("sfx_infection_rise", SfxGroup.Infection, 0.75f, 0.1f, 4, -19f);
        public static readonly SfxCue PelicanDive = Cue("sfx_pelican_dive", SfxGroup.BigDeaths, 1f, 0.03f, 9, -16f);
        public static readonly SfxCue PelicanCrash = Cue("sfx_pelican_crash", SfxGroup.BigDeaths, 1f, 0.03f, 10, -9.9f);
        public static readonly SfxCue DropIn = Cue("sfx_drop_in", SfxGroup.Drops, 0.8f, 0.05f, 6, -15f);

        // One weapon's or unit's own sound, from the owner (see OwnerCue). The guns that fire fast loop while they fire.
        public static readonly SfxCue FireAssaultRifle = OwnerCue("sfx_fire_assaultrifle", SfxGroup.SmallArms, 0.6f, 0.06f, 1, -17f);
        /// <summary>
        /// One SMG shot, played for every shot (the owner, 2026-11-08; D117): the file is one shot and 0.7 s of quiet, so looped it gave one shot a second
        /// to an ODST firing ten. Only its first 0.06 s holds a voice, so the next shot starts over the last one's tail.
        /// </summary>
        public static readonly SfxCue FireSmg = OwnerCue("sfx_fire_smg", SfxGroup.Smg, 0.55f, 0.05f, 1, -19f, hold: 0.06f);
        /// <summary>
        /// One M60 shot with a half-second tail, played for every shot (the owner, 2026-11-08; D114): looped, it gave about 2 shots a second to a Warthog
        /// firing 5. Only its first 0.1 s holds a voice, so the next shot starts over the last one's tail.
        /// </summary>
        public static readonly SfxCue FireM60 = OwnerCue("sfx_fire_m60", SfxGroup.HeavyArms, 0.6f, 0.04f, 2, -16.5f, hold: 0.1f);
        /// <summary>The Ghost's bolt, played for every shot (D117), as the SMG: looped, its file gave one bolt a second to a Ghost firing four.</summary>
        public static readonly SfxCue FireGhost = OwnerCue("sfx_fire_ghost", SfxGroup.Plasma, 0.6f, 0.05f, 2, -18f, hold: 0.12f);
        public static readonly SfxCue FireBanshee = OwnerCue("sfx_fire_banshee", SfxGroup.Plasma, 0.6f, 0.06f, 2, -18.5f);
        public static readonly SfxCue FireBansheeBomb = OwnerCue("sfx_fire_banshee_bomb", SfxGroup.Rockets, 0.85f, 0.05f, 4, -13f);
        public static readonly SfxCue ExplosionBanshee = OwnerCue("sfx_explosion_banshee", SfxGroup.Explosions, 0.8f, 0.06f, 3, -13.5f);
        public static readonly SfxCue FireFuelRod = OwnerCue("sfx_fire_fuelrod", SfxGroup.HeavyPlasma, 0.9f, 0.05f, 5, -14f);
        public static readonly SfxCue FireHunter = OwnerCue("sfx_fire_hunter", SfxGroup.HeavyPlasma, 0.9f, 0.04f, 5, -13.5f);
        public static readonly SfxCue ExplosionFuelRod = OwnerCue("sfx_explosion_fuelrod", SfxGroup.Explosions, 0.8f, 0.06f, 3, -14f);
        public static readonly SfxCue FireMortar = OwnerCue("sfx_fire_mortar", SfxGroup.HeavyPlasma, 0.9f, 0.04f, 5, -13.5f);
        public static readonly SfxCue FlightMortar = OwnerCue("sfx_flight_mortar", SfxGroup.Loops, 0.5f, 0f, 0, -20f, true);
        public static readonly SfxCue FireBruteShot = OwnerCue("sfx_fire_bruteshot", SfxGroup.Rockets, 0.8f, 0.05f, 4, -14f);
        public static readonly SfxCue FireShotgun = OwnerCue("sfx_fire_shotgun", SfxGroup.HeavyArms, 0.75f, 0.05f, 3, -14f);
        public static readonly SfxCue FireSniper = OwnerCue("sfx_fire_sniper", SfxGroup.BigGuns, 0.85f, 0.03f, 5, -13f);
        /// <summary>The Spartan Laser's charge with its shot about 2.5 s in: played as its wind-up starts (WindUpFor). No pitch spread, which would move the shot.</summary>
        /// <summary>Where the shot is in the owner's Spartan Laser file: its loudest moment, measured (the wind-up is 2.5 s).</summary>
        public const float SpartanLaserShotSeconds = 2.42f;
        public static readonly SfxCue FireSpartanLaser = OwnerCue("sfx_fire_spartanlaser", SfxGroup.Lasers, 1f, 0f, 7, -14f);
        /// <summary>The same file a little quieter, for the Condor's five light lasers (the owner, 2026-11-08; D114). Not in the slot table: the Spartan Laser's cue owns the slot.</summary>
        public static readonly SfxCue FireLightSpartanLaser = new SfxCue("sfx_fire_spartanlaser", SfxGroup.Lasers, 0.7f, 0f, 6, -14f);
        public static readonly SfxCue FireSword = OwnerCue("sfx_fire_sword", SfxGroup.Melee, 0.65f, 0.08f, 2, -17.5f);
        public static readonly SfxCue SwordReady = OwnerCue("sfx_sword_ready", SfxGroup.Ready, 0.6f, 0.06f, 3, -17f);
        public static readonly SfxCue BeamCharge = OwnerCue("sfx_beam_charge", SfxGroup.Beams, 1f, 0f, 8, -13f);
        public static readonly SfxCue BeamIn = OwnerCue("sfx_beam_in", SfxGroup.Beams, 1f, 0f, 8, -12f);
        public static readonly SfxCue BeamLoop = OwnerCue("sfx_beam_loop", SfxGroup.Loops, 1f, 0f, 8, -13f, true);
        public static readonly SfxCue BeamOut = OwnerCue("sfx_beam_out", SfxGroup.Beams, 1f, 0f, 8, -13f);
        public static readonly SfxCue WalkScarab = OwnerCue("sfx_walk_scarab", SfxGroup.Loops, 0.6f, 0f, 0, -17f, true);
        public static readonly SfxCue DeathScarab = OwnerCue("sfx_death_scarab", SfxGroup.BigDeaths, 1f, 0.02f, 10, -8f);
        public static readonly SfxCue DeathAirUnsc = OwnerCue("sfx_death_air_unsc", SfxGroup.BigDeaths, 0.95f, 0.05f, 8, -11.3f);
        /// <summary>The owner's turret explosion (D120): every machine whose death had a generated sound, on the ground (sfx_death_vehicle) or in the air (sfx_death_air); also the Ghost's blast at the end of its drift.</summary>
        public static readonly SfxCue ExplosionTurret = OwnerCue("sfx_explosion_turret", SfxGroup.BigDeaths, 0.95f, 0.05f, 8, -11.5f);
        /// <summary>The Banshee's and the Infested Banshee's fall and crash in one file (the owner, D120): falling for its first BansheeFallInFile seconds, then the blast.</summary>
        public static readonly SfxCue DeathBanshee = OwnerCue("sfx_death_banshee", SfxGroup.BigDeaths, 0.95f, 0.05f, 8, -11.3f);
        /// <summary>Seconds of falling at the start of the Banshee's death file, before its blast: as long as a dead aircraft takes to fall (UnitViewManager.AirFallSeconds).</summary>
        public const float BansheeFallInFile = 1.3f;
        /// <summary>A Ghost knocked out, heard as it starts its drift (the owner's ghost_initial_destroyed, D120); its blast comes after (ExplosionTurret).</summary>
        public static readonly SfxCue GhostKnockedOut = OwnerCue("sfx_death_ghost_start", SfxGroup.BigDeaths, 0.85f, 0.05f, 7, -13f);
        public static readonly SfxCue DeathInfectionForm = OwnerCue("sfx_death_infectionform", SfxGroup.InfantryDeaths, 0.5f, 0.12f, 1, -21f);
        public static readonly SfxCue CallSeeder = OwnerCue("sfx_call_seeder", SfxGroup.Calls, 0.55f, 0.06f, 2, -20f);
        public static readonly SfxCue CallSwarm = OwnerCue("sfx_call_swarm", SfxGroup.Calls, 0.5f, 0.08f, 1, -21f);
        public static readonly SfxCue ShieldRegen = OwnerCue("sfx_shield_regen", SfxGroup.ShieldRegen, 0.45f, 0.05f, 0, -24f);
        // The owner's second batch (D71).
        /// <summary>The autocannon's short burst, played for every shot and overlapping the last one (D114), as the M60.</summary>
        public static readonly SfxCue FireAutocannon = OwnerCue("sfx_fire_autocannon", SfxGroup.HeavyArms, 0.65f, 0.04f, 2, -16f, hold: 0.15f);
        /// <summary>The BR85's file is a burst with almost 3 s of echo: a Spartan fires again long before it ends, so only its burst holds a voice.</summary>
        public static readonly SfxCue FireBr85 = OwnerCue("sfx_fire_br85", SfxGroup.SmallArms, 0.65f, 0.04f, 2, -17f, hold: 0.5f);
        /// <summary>The Phantoms' and the Scarab's plasma turrets: the owner's heavy plasma file, in a group of its own so that their fast fire can't take every heavy plasma voice from the fuel rods and mortars.</summary>
        public static readonly SfxCue FirePlasmaTurret = OwnerCue("sfx_fire_plasmaturret", SfxGroup.PlasmaTurrets, 0.75f, 0.06f, 2, -16.5f);
        public static readonly SfxCue FireBeamRifle = OwnerCue("sfx_fire_beamrifle", SfxGroup.BigGuns, 0.85f, 0.03f, 5, -13.5f, hold: 0.6f); // 3 s, mostly echo: four Jackals would hold every big gun voice.
        // Louder than the rest, about 6 dB, with the music dipping under them: a base being hit must be heard (the owner, 2026-11-06).
        public static readonly SfxCue NodeSmall = OwnerCue("sfx_node_small", SfxGroup.Nodes, 1.6f, 0.08f, 6, -15f);
        public static readonly SfxCue NodeBig = OwnerCue("sfx_node_big", SfxGroup.Nodes, 2f, 0.03f, 9, -10.5f);
        // The owner's third batch (D91): deaths by who dies, the Flood's leap and deaths by size, the Hunters' charge and bash, the Hunter Captain's laser.
        public static readonly SfxCue DeathHuman = OwnerCue("sfx_death_human", SfxGroup.InfantryDeaths, 0.6f, 0.1f, 2, -19f);
        public static readonly SfxCue DeathSpartan = OwnerCue("sfx_death_spartan", SfxGroup.InfantryDeaths, 0.65f, 0.06f, 3, -18f);
        public static readonly SfxCue DeathElite = OwnerCue("sfx_death_elite", SfxGroup.InfantryDeaths, 0.65f, 0.08f, 3, -18f);
        public static readonly SfxCue DeathGrunt = OwnerCue("sfx_death_grunt", SfxGroup.InfantryDeaths, 0.55f, 0.1f, 2, -19.5f);
        public static readonly SfxCue DeathJackal = OwnerCue("sfx_death_jackal", SfxGroup.InfantryDeaths, 0.6f, 0.08f, 2, -19f);
        public static readonly SfxCue DeathBrute = OwnerCue("sfx_death_brute", SfxGroup.InfantryDeaths, 0.65f, 0.06f, 3, -18f);
        public static readonly SfxCue DeathFloodSmall = OwnerCue("sfx_death_flood_small", SfxGroup.InfantryDeaths, 0.6f, 0.1f, 2, -18f);
        public static readonly SfxCue DeathFloodBig = OwnerCue("sfx_death_flood_big", SfxGroup.BigDeaths, 0.85f, 0.06f, 6, -14f);
        public static readonly SfxCue DeathFloodHuge = OwnerCue("sfx_death_flood_huge", SfxGroup.BigDeaths, 1f, 0.03f, 9, -10.5f);
        public static readonly SfxCue FloodLeap = OwnerCue("sfx_flood_leap", SfxGroup.Calls, 0.55f, 0.1f, 2, -19f);
        /// <summary>Infection Forms leaping, the owner's own sound for them (D108): they leap by the dozen, so it is quiet and low in priority, the group's voices shared.</summary>
        public static readonly SfxCue InfectionFormLeap = OwnerCue("sfx_infectionform_leap", SfxGroup.Calls, 0.5f, 0.12f, 1, -19.5f);
        public static readonly SfxCue HunterCharge = OwnerCue("sfx_hunter_charge", SfxGroup.Ready, 0.8f, 0.05f, 5, -15f);
        public static readonly SfxCue FireHunterBash = OwnerCue("sfx_fire_hunterbash", SfxGroup.Melee, 0.75f, 0.06f, 3, -16f);
        /// <summary>The Hunter Captain's siphon laser, a 1 s beam: once as each beam starts (the owner's file runs about as long), and any other laser beam like it.</summary>
        public static readonly SfxCue FireSiphon = OwnerCue("sfx_fire_siphon", SfxGroup.Lasers, 1f, 0.03f, 7, -14f);
        // The owner's fourth batch (D117).
        /// <summary>The Longsword's carpet bombs going out: a loud second and a long tail, so only the first second holds a voice.</summary>
        public static readonly SfxCue FireLongsword = OwnerCue("sfx_fire_longsword", SfxGroup.Rockets, 0.85f, 0.04f, 4, -13f, hold: 1f);
        /// <summary>
        /// Something coming down from the sky, louder as it nears (the owner's "rocket or bomb flying down"): played ahead of the strikes that fall from
        /// orbit (FallsIn), so that it ends as they hit. No pitch spread, which would move its end.
        /// </summary>
        public static readonly SfxCue FallingIn = OwnerCue("sfx_falling_in", SfxGroup.Spells, 0.9f, 0f, 9, -14f);
        /// <summary>
        /// The Infected Pelican's dive, a little quieter, for any aircraft falling to the ground as it dies (the owner, 2026-11-08; D117): it plays while the
        /// wreck falls and fades as it hits, where its crash is heard (BattleAudio). Not in the slot table: the Pelican's dive owns the slot.
        /// </summary>
        public static readonly SfxCue AirFall = new SfxCue("sfx_pelican_dive", SfxGroup.AirFalls, 0.75f, 0.05f, 6, -16f);
        /// <summary>
        /// The owner's Infection Form leap, louder, for a Flood creature falling as it dies (the Seeder; the owner, 2026-11-10) in place of the dive above.
        /// Not in the slot table: the Infection Forms' leap owns the slot.
        /// </summary>
        public static readonly SfxCue FloodCreatureFall = new SfxCue("sfx_infectionform_leap", SfxGroup.AirFalls, 0.8f, 0.08f, 6, -16f);
        /// <summary>
        /// The same file, quieter, for each UNSC rocket or bomb in flight (ComesDown; the owner, 2026-11-08; D118): its end, as long as the shot flies, so
        /// that it ends as the shot lands. Not in the slot table: the strikes' cue owns the slot.
        /// </summary>
        public static readonly SfxCue Incoming = new SfxCue("sfx_falling_in", SfxGroup.Incoming, 0.55f, 0f, 2, -14f);

        /// <summary>
        /// A shot heard coming down before it lands (Incoming): what the UNSC fires as a rocket, a missile or a bomb that flies (the Longsword's carpet bombs,
        /// the M41s, the Hornet's, the Wolverine's, the Mantis's, the Vulture's, the Hydra pod, the Archer pod). Not the Flood's scavenged rockets, nor the Banished's.
        /// </summary>
        public static bool ComesDown(WeaponStats weapon, string faction)
        {
            if (weapon == null || weapon.ProjectileSpeed <= 0f || faction != BattleFactions.Unsc) return false;
            WeaponFeelKind kind = WeaponFeel.For(weapon);
            return kind == WeaponFeelKind.Rocket || kind == WeaponFeelKind.Bomb;
        }

        /// <summary>A shot must fly at least this long (real seconds) for its coming down to be heard.</summary>
        public const float ShortestIncomingSeconds = 0.12f;

        /// <summary>The strikes that fall from the sky as a rocket or a bomb would, and are heard coming (FallingIn): the Archer missiles and the HAVOK warhead.</summary>
        public static bool FallsIn(SpellStats spell)
        {
            if (spell == null || spell.Effect != SpellEffect.Damage) return false;
            string n = (spell.Name ?? "").ToLowerInvariant();
            return n.Contains("missile") || n.Contains("nuke") || n.Contains("havok");
        }

        /// <summary>The owner's sounds of one weapon or unit (they have no generated sound).</summary>
        public static IReadOnlyList<SfxCue> OwnerOnly => Owners;

        // Long loops, one for all the engines and one for all the hovering, at the level of how many are alive.
        public static readonly SfxCue HumEngine = Cue("sfx_hum_engine", SfxGroup.Loops, 0.5f, 0f, 0, -18f);
        public static readonly SfxCue HumHover = Cue("sfx_hum_hover", SfxGroup.Loops, 0.5f, 0f, 0, -18f);

        /// <summary>Every spell's slot (see AudioSlots.Spell).</summary>
        public static readonly string[] SpellSlots =
        {
            "sfx_spell_mac", "sfx_spell_nuke", "sfx_spell_glassing", "sfx_spell_missiles", "sfx_spell_plasma", "sfx_spell_storm_tick", "sfx_spell_spore", "sfx_spell_wrath",
            "sfx_spell_buff_unsc", "sfx_spell_buff_covenant", "sfx_spell_buff_flood", "sfx_spell_shield", "sfx_spell_drop_pods", "sfx_spell_drop_air", "sfx_spell_drop_flood",
            "sfx_spell_emp", "sfx_spell_smoke",
        };

        static AudioCatalog()
        {
            foreach (string slot in SpellSlots) Cue(slot, SfxGroup.Spells, 1f, 0.02f, 10, -12.5f);
        }

        public static IReadOnlyList<SfxCue> Cues => All;

        public static void SetLimits(VoiceLimiter limiter)
        {
            Limit(limiter, SfxGroup.SmallArms, 4, 0.07f);
            Limit(limiter, SfxGroup.HeavyArms, 6, 0.04f); // Every shot of the machine guns and autocannons is heard, over the tail of the last (D114).
            Limit(limiter, SfxGroup.BigGuns, 3, 0.14f);
            Limit(limiter, SfxGroup.Rockets, 3, 0.12f);
            Limit(limiter, SfxGroup.Flame, 2, 0.2f);
            Limit(limiter, SfxGroup.Plasma, 4, 0.07f);
            Limit(limiter, SfxGroup.HeavyPlasma, 3, 0.15f);
            Limit(limiter, SfxGroup.Needles, 3, 0.06f);
            Limit(limiter, SfxGroup.Lasers, 3, 0.2f);
            Limit(limiter, SfxGroup.Beams, 4, 0.2f); // The owner's beam has a long charge and a long stop, which mustn't keep the next charge out. Apart from the lasers: a limit doesn't look at priority, and a Scarab's one long beam must not lose its only start to Siphon Lasers overlapping themselves.
            Limit(limiter, SfxGroup.Melee, 3, 0.08f);
            Limit(limiter, SfxGroup.Bio, 3, 0.07f);
            Limit(limiter, SfxGroup.Explosions, 4, 0.1f);
            Limit(limiter, SfxGroup.InfantryDeaths, 3, 0.11f);
            Limit(limiter, SfxGroup.BigDeaths, 4, 0.1f);
            Limit(limiter, SfxGroup.Bursts, 3, 0.1f);
            Limit(limiter, SfxGroup.Infection, 3, 0.15f);
            Limit(limiter, SfxGroup.Spells, 4, 0.08f);
            Limit(limiter, SfxGroup.Drops, 2, 0.12f);
            Limit(limiter, SfxGroup.Loops, 2, 0f);
            Limit(limiter, SfxGroup.Calls, 2, 0.4f);
            Limit(limiter, SfxGroup.Ready, 6, 0.04f);
            Limit(limiter, SfxGroup.ShieldRegen, 1, 1.2f);
            Limit(limiter, SfxGroup.PlasmaTurrets, 3, 0.15f);
            Limit(limiter, SfxGroup.Nodes, 5, 0.05f); // A crowd's nodes land 0.08 s apart at the closest.
            Limit(limiter, SfxGroup.Smg, 6, 0.05f); // One ODST is heard shot for shot; a crowd of them, 20 shots a second at most.
            Limit(limiter, SfxGroup.AirFalls, 3, 0.15f);
            Limit(limiter, SfxGroup.Incoming, 4, 0.12f);
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

        /// <summary>
        /// The sound of this weapon on this unit: its own (the owner's file) if it has one and it is in, otherwise its kind's (Fire). Null: no sound
        /// when it fires (a burst; or the Scarab's beam once its own sounds are in, which BattleAudio plays by the beam's phases).
        /// </summary>
        public static SfxCue FireFor(WeaponStats weapon, UnitStats shooter, string faction)
        {
            if (WindUpFor(weapon) != null) return null; // Its shot is in the sound of its wind-up.
            SfxCue own = OwnFire(weapon, shooter);
            if (own != null && own.HasSound) return own;
            // A taken vehicle's gun sounds as it did on the vehicle (D119): the Infested Banshee's Spore Bomb as the Banshee's Fuel Rod Bomb.
            if (TakenVehicle(shooter, out _, out string wasFaction) && WeaponItWas.TryGetValue(weapon.Name ?? "", out string was))
            {
                if (!Before.TryGetValue(weapon, out WeaponStats before))
                {
                    before = weapon.Clone();
                    before.Name = was;
                    Before[weapon] = before;
                }
                own = OwnFire(before, shooter);
                if (own != null && own.HasSound) return own;
                weapon = before;
                faction = wasFaction;
            }
            WeaponFeelKind kind = WeaponFeel.For(weapon);
            if (kind == WeaponFeelKind.Beam && BeamCharge.HasSound) return null;
            return Fire(kind, faction);
        }

        /// <summary>The Flood's vehicles and what each was before it was taken, with that vehicle's faction (D119): they sound as it did, not as generated stand-ins.</summary>
        private static readonly Dictionary<string, (string Was, string Faction)> TakenVehicles = new Dictionary<string, (string, string)>
        {
            { "Infested Banshee", ("Banshee", BattleFactions.Covenant) },
            { "Infested Ghost", ("Ghost", BattleFactions.Covenant) },
            { "Infested Wraith", ("Wraith", BattleFactions.Covenant) },
            { "Infected Phantom", ("Covenant Phantom", BattleFactions.Covenant) },
            { "Infested Hornet", ("Hornet", BattleFactions.Unsc) },
            { "Infested Scorpion", ("Scorpion", BattleFactions.Unsc) },
            { "Infested Warthog", ("Warthog", BattleFactions.Unsc) },
            { "Infected Pelican", ("Pelican", BattleFactions.Unsc) },
        };

        /// <summary>A taken vehicle's weapons by the vehicle's weapon each was (D119).</summary>
        private static readonly Dictionary<string, string> WeaponItWas = new Dictionary<string, string>
        {
            { "Spore Bomb", "Fuel Rod Bomb" }, // The Infested Banshee.
            { "Scavenged Plasma Cannons", "Plasma Cannons" },
            { "Scavenged Plasma Bolts", "Plasma Bolts" },
            { "Scavenged Mortar", "Plasma Mortar" },
            { "Spore Bombs", "Bombs" }, // The Infested Hornet.
            { "Scavenged Rockets", "Rockets" },
            { "Scavenged Cannon", "M512 Cannon" },
            { "Scavenged Machine Gun", "M41 Machine Gun" },
        };

        /// <summary>A taken vehicle's weapon as it was on the vehicle, made once each.</summary>
        private static readonly Dictionary<WeaponStats, WeaponStats> Before = new Dictionary<WeaponStats, WeaponStats>();

        /// <summary>True for one of the Flood's vehicles that was another faction's (the Infested Hornet was a UNSC Hornet), with what it was.</summary>
        public static bool TakenVehicle(UnitStats stats, out string was, out string faction)
        {
            if (stats != null && stats.Name != null && TakenVehicles.TryGetValue(stats.Name, out (string Was, string Faction) taken))
            {
                was = taken.Was;
                faction = taken.Faction;
                return true;
            }
            was = faction = null;
            return false;
        }

        /// <summary>
        /// The sound a weapon with a wind-up makes as it starts winding up, its shot inside it (the Spartan Laser), if its file is in.
        /// Null otherwise: its shot is then heard as it fires (FireFor).
        /// </summary>
        public static SfxCue WindUpFor(WeaponStats weapon)
        {
            if (weapon == null || !weapon.HasWindUp) return null;
            string name = weapon.Name ?? "";
            SfxCue own = name.IndexOf("spartan laser", System.StringComparison.OrdinalIgnoreCase) < 0 ? null
                : name.StartsWith("Light ", System.StringComparison.OrdinalIgnoreCase) ? FireLightSpartanLaser : FireSpartanLaser;
            return own != null && own.HasSound ? own : null;
        }

        /// <summary>
        /// The weapon's or unit's own firing sound by its names, or null if it has none. The owner's M60 is every machine gun and chaingun (D71):
        /// whatever is a heavy bullet gun by its name, but the shotguns and autocannons, which have their own.
        /// </summary>
        public static SfxCue OwnFire(WeaponStats weapon, UnitStats shooter)
        {
            string n = (weapon.Name ?? "").ToLowerInvariant();
            string unit = (shooter?.Name ?? "").ToLowerInvariant();
            if (n.Contains("assault rifle") || n == "scavenged rifle") return FireAssaultRifle;
            if (n.Contains("smg")) return FireSmg;
            if (n.Contains("br85")) return FireBr85;
            if (n.Contains("beam rifle")) return FireBeamRifle;
            if (n.Contains("plasma turret")) return FirePlasmaTurret; // "Plasma Turrets" (the Phantoms), "Plasma Turret (left)" (the Scarab).
            if (n.Contains("autocannon")) return FireAutocannon;
            if (n.Contains("plasma bolts")) return FireGhost;
            if (unit.Contains("banshee") && n.Contains("plasma cannons")) return FireBanshee;
            if (n == "fuel rod bomb") return FireBansheeBomb;
            if (n.Contains("fuel rod")) return FireFuelRod;
            if (n == "assault cannon") return FireHunter;
            if (n.Contains("mortar") && unit.Contains("wraith")) return FireMortar;
            if (n.Contains("spike grenade")) return FireBruteShot;
            if (n.Contains("shotgun")) return FireShotgun;
            if (n.Contains("sniper")) return FireSniper;
            if (n.Contains("energy sword")) return FireSword;
            if (n.Contains("carpet bomb")) return FireLongsword;
            if (n.Contains("siphon") || (weapon.IsBeam && n.Contains("laser"))) return FireSiphon;
            if (n == "shield bash") return FireHunterBash;
            if (WeaponFeel.FromName(weapon.Name) == WeaponFeelKind.HeavyBullet) return FireM60;
            return null;
        }

        /// <summary>Where a shot lands: the weapon's own explosion (the owner's file) if it has one and it is in, otherwise as LandingOf.</summary>
        public static SfxCue LandingFor(WeaponStats weapon)
        {
            string n = (weapon.Name ?? "").ToLowerInvariant();
            SfxCue own = n == "fuel rod bomb" ? ExplosionBanshee : n.Contains("fuel rod") || n == "assault cannon" ? ExplosionFuelRod : null;
            return own != null && own.HasSound ? own : LandingOf(weapon);
        }

        /// <summary>The explosion a shot from this weapon makes where it lands, or null. A plasma shot with a blast is drawn going off, so it is heard going off too; so is a lobbed Flood shot (Bone Shards) or a Spore Bomb, wetly.</summary>
        public static SfxCue LandingOf(WeaponStats weapon)
        {
            WeaponFeelKind kind = WeaponFeel.For(weapon);
            if (kind == WeaponFeelKind.Bomb && weapon.Name.IndexOf("spore", System.StringComparison.OrdinalIgnoreCase) >= 0) return FloodBurst;
            if (weapon.SplashRadius > 0f)
            {
                if (kind == WeaponFeelKind.Plasma) return ExplosionSmall;
                if (kind == WeaponFeelKind.Bio && !weapon.IsInstant) return FloodBurst;
            }
            return LandingOf(kind);
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

        /// <summary>
        /// Where in the owner's Glassing Beam the beam strikes, in seconds: the file builds up for this long first (measured from the file, D71).
        /// The sound starts this long before the first hit, so that the two meet.
        /// </summary>
        public const float GlassingStrikeSeconds = 1.85f;

        /// <summary>How long the Glassing Beam's sound takes to fade once its beam is gone: the owner's file goes on burning for 8 s, the beam for 3.</summary>
        public const float GlassingFadeSeconds = 1.2f;

        /// <summary>How long before a spell's first hit its sound must start, in seconds of real time: 0 for every sound that starts with the hit.</summary>
        public static float SpellLeadSeconds(SpellStats spell)
        {
            if (spell == null || AudioSlots.Spell(spell, 1, null) != "sfx_spell_glassing") return 0f;
            AudioClipSet clips = ForSlot("sfx_spell_glassing").Clips;
            return clips.IsOverride && !clips.IsEmpty ? GlassingStrikeSeconds : 0f; // The generated sound starts with the beam.
        }

        /// <summary>Nodes of light at least this big (0 to 1, RoundEnding.Node.Size, about 60 damage to a base and up), and every Titan's, land with the big sound.</summary>
        public const float BigNodeSize = 0.75f;

        /// <summary>
        /// The sound of a node of light hitting a base at the end of a round: the owner's small and big files, by the node's size. Until they are in, the
        /// battle's own hit and explosions stand in, by size.
        /// </summary>
        public static SfxCue NodeHit(float size, bool titan)
        {
            if (NodeSmall.HasSound && NodeBig.HasSound) return titan || size >= BigNodeSize ? NodeBig : NodeSmall;
            return size < 0.75f ? ExplosionSmall : size < 0.92f ? ExplosionMedium : ExplosionLarge;
        }

        /// <summary>Forgets the clips of every cue, so that they are looked up again.</summary>
        public static void ForgetClips()
        {
            foreach (SfxCue cue in All) cue.Forget();
            foreach (SfxCue cue in Owners) cue.Forget();
            FireLightSpartanLaser.Forget();
            DeathLekgolo.Forget();
        }
    }
}
