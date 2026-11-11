// BattleAudio.cs
// Listens to the battle in a scene and asks for its sounds: each shot from its weapon kind and the faction firing it, hits, explosions,
// deaths by size and kind, Flood bursts, infection rising, spells, drops, the Infected Pelican's dive. Added at run time to the object with
// the BattleController (see AudioDirector). It only reads the simulation's events and never changes anything, so battles play out the
// same with or without it. What a weapon sounds like is worked out once per weapon and kept: the many shots of a battle allocate nothing.
// The battle's sounds stop while it stands still, by the battle's own speed or by the game's pause (GamePause).
// With the owner's files in, some sounds go by more than a shot (each only once its file is in; until then the old sound plays):
//   - flamethrowers loop while any of them fire, louder the more do (the SMG, the machine guns and the Ghost play every shot, overlapping: D114, D117);
//   - an aircraft that blows up and falls is heard diving as it falls (the Infected Pelican's dive), and its crash comes as it hits the ground (D117);
//   - the Archer missiles and the HAVOK warhead are heard coming down, ending as they hit (D117), and so is every UNSC rocket and bomb in flight (D118);
//   - a Wraith's shell is heard in the air until it lands;
//   - the Scarab's beam: its charge as it starts charging (heard to its end), then "in" as it fires, the loop after that, and "out" as it stops;
//   - a Scarab walking is a loop;
//   - every Zealot readies its sword as the battle starts;
//   - Seeders and Swarm Forms call, each one 3 to 6 s after its last call ended;
//   - an energy shield starting to recharge is heard (one at a time, as there may be many);
//   - a Spartan Laser's sound starts with its wind-up, so that the shot in the file comes with the shot in the battle.
//   - the Glassing Beam's long sound starts ahead of the beam, so that the beam strikes with it, and fades out as the beam goes (D71).
// Depends on: AudioDirector, AudioCatalog, WeaponFeel, GamePause, BattleController, Halobellum.Combat (BattleSimulation, SimUnit, SimSpell), Halobellum.Core, UnityEngine.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class BattleAudio : MonoBehaviour
    {
        private BattleController _battle;
        private BattleSimulation _shown;
        private readonly Dictionary<WeaponStats, SfxCue> _fireA = new Dictionary<WeaponStats, SfxCue>();
        private readonly Dictionary<WeaponStats, SfxCue> _fireB = new Dictionary<WeaponStats, SfxCue>();
        private readonly Dictionary<WeaponStats, SfxCue> _landings = new Dictionary<WeaponStats, SfxCue>();
        private readonly Dictionary<WeaponStats, SfxCue> _windUps = new Dictionary<WeaponStats, SfxCue>();
        private readonly Dictionary<WeaponStats, bool> _comesDownA = new Dictionary<WeaponStats, bool>();
        private readonly Dictionary<WeaponStats, bool> _comesDownB = new Dictionary<WeaponStats, bool>();
        private readonly List<SimUnit> _divers = new List<SimUnit>();
        private float _nextHumCheck;
        private bool _wasPlaying;

        /// <summary>A loop's sources: a unit firing (by its id), or a shell in the air (by a negative number), until a time of the battle.</summary>
        private struct Source
        {
            public float Until;
            public Vector3 Place;
        }

        private readonly Dictionary<SfxCue, Dictionary<int, Source>> _loops = new Dictionary<SfxCue, Dictionary<int, Source>>(8);
        private readonly List<int> _ended = new List<int>(64);
        private int _shells;

        private struct Beam
        {
            public BeamPhase Phase;
            public float LoopFrom;
            public Vector3 Place;
            public int Frame;
        }

        private readonly Dictionary<int, Beam> _beams = new Dictionary<int, Beam>(8);
        private readonly List<BeamShot> _beamShots = new List<BeamShot>(8);
        private readonly Dictionary<WeaponStats, bool> _isBeam = new Dictionary<WeaponStats, bool>();
        private readonly Dictionary<UnitStats, SfxCue> _calls = new Dictionary<UnitStats, SfxCue>();
        private readonly Dictionary<UnitStats, bool> _swords = new Dictionary<UnitStats, bool>();
        private readonly Dictionary<int, float> _nextCall = new Dictionary<int, float>(128);
        private readonly HashSet<int> _recharging = new HashSet<int>();
        /// <summary>Spells whose sound started ahead of their first hit (the Glassing Beam), and how far ahead each kind's must start.</summary>
        private readonly HashSet<SimSpell> _spellsAhead = new HashSet<SimSpell>();
        private readonly Dictionary<SpellStats, float> _spellLeads = new Dictionary<SpellStats, float>();
        /// <summary>Strikes heard coming down already (AudioCatalog.FallingIn).</summary>
        private readonly HashSet<SimSpell> _fallingIn = new HashSet<SimSpell>();

        private struct Later
        {
            public float At;
            public SfxCue Cue;
            public Vector3 Place;
        }

        private readonly List<Later> _later = new List<Later>(16);
        private float _nextScan;

        private void Awake()
        {
            _battle = GetComponent<BattleController>();
            _battle.BattleChanged += OnBattleChanged;
            _battle.BattleFinished += OnBattleFinished;
            BattleFactions.Changed += OnFactionsChanged;
            OnBattleChanged(_battle.Simulation);
        }

        private void OnDestroy()
        {
            Listen(null);
            if (_battle != null)
            {
                _battle.BattleChanged -= OnBattleChanged;
                _battle.BattleFinished -= OnBattleFinished;
            }
            BattleFactions.Changed -= OnFactionsChanged;
        }

        private void OnFactionsChanged()
        {
            _fireA.Clear();
            _fireB.Clear();
            _comesDownA.Clear();
            _comesDownB.Clear();
        }

        private void OnBattleChanged(BattleSimulation simulation)
        {
            Listen(simulation);
            // Leaving a battle cuts its sounds. A deployment preview is rebuilt on every action: that must not cut the sound of the action.
            if (_wasPlaying) AudioDirector.Instance?.StopBattleSounds();
            _wasPlaying = _battle.IsPlaying;
            _divers.Clear();
            ForgetWhatGoesOn();
            if (simulation == null) return;
            foreach (SimUnit unit in simulation.Units) NoteDiver(unit);
            if (_battle.IsPlaying) ReadySwords(simulation);
        }

        /// <summary>The loops, beams, calls and shields of the battle that was shown.</summary>
        private void ForgetWhatGoesOn()
        {
            foreach (var pair in _loops) pair.Value.Clear();
            _beams.Clear();
            _nextCall.Clear();
            _recharging.Clear();
            _spellsAhead.Clear();
            _fallingIn.Clear();
            _later.Clear();
            _shells = 0;
        }

        /// <summary>Each Zealot readies its sword as the battle starts, a moment apart from the others.</summary>
        private void ReadySwords(BattleSimulation simulation)
        {
            AudioDirector director = AudioDirector.Instance;
            if (director == null || !AudioCatalog.SwordReady.HasSound) return;
            foreach (SimUnit unit in simulation.Units)
            {
                if (!unit.IsAlive || !HasSword(unit.Stats)) continue;
                _later.Add(new Later { At = director.BattleClock + 0.1f + 0.4f * director.NextRandom(), Cue = AudioCatalog.SwordReady, Place = Place(unit) });
            }
        }

        private bool HasSword(UnitStats stats)
        {
            if (_swords.TryGetValue(stats, out bool sword)) return sword;
            sword = false;
            foreach (WeaponStats weapon in stats.Weapons)
            {
                if (weapon != null && AudioCatalog.OwnFire(weapon, stats) == AudioCatalog.FireSword) sword = true;
            }
            _swords[stats] = sword;
            return sword;
        }

        private void Listen(BattleSimulation simulation)
        {
            if (_shown != null)
            {
                BattleEvents events = _shown.Events;
                events.WeaponFired -= OnWeaponFired;
                events.WeaponWindingUp -= OnWeaponWindingUp;
                events.ShotLanded -= OnShotLanded;
                events.UnitDied -= OnUnitDied;
                events.UnitInfected -= OnUnitInfected;
                events.UnitRose -= OnUnitRose;
                events.SpellTriggered -= OnSpellTriggered;
                events.ReinforcementDropped -= OnReinforcementDropped;
                events.UnitLeapt -= OnUnitLeapt;
                events.UnitSpawned -= NoteDiver;
            }
            _shown = simulation;
            if (_shown != null)
            {
                BattleEvents events = _shown.Events;
                events.WeaponFired += OnWeaponFired;
                events.WeaponWindingUp += OnWeaponWindingUp;
                events.ShotLanded += OnShotLanded;
                events.UnitDied += OnUnitDied;
                events.UnitInfected += OnUnitInfected;
                events.UnitRose += OnUnitRose;
                events.SpellTriggered += OnSpellTriggered;
                events.ReinforcementDropped += OnReinforcementDropped;
                events.UnitLeapt += OnUnitLeapt;
                events.UnitSpawned += NoteDiver;
            }
        }

        private void OnBattleFinished(BattleResult result)
        {
            AudioDirector director = AudioDirector.Instance;
            if (director == null) return;
            director.SetHums(0f, 0f);
            // A spell whose build-up started ahead but which never struck (the battle ended first): its strike would be heard with no beam.
            foreach (SimSpell spell in _spellsAhead)
            {
                if (spell.TicksDone > 0) continue;
                director.FadeShaped(0.4f);
                break;
            }
            // The battle's time stops here, so nothing that waits on it would ever end: the guns and beams fall silent now.
            foreach (var pair in _loops)
            {
                pair.Value.Clear();
                director.SetLoop(pair.Key, 0f, Vector3.zero);
            }
            foreach (var pair in _beams)
            {
                if (pair.Value.Phase == BeamPhase.Firing) GameAudio.Sfx(AudioCatalog.BeamOut, pair.Value.Place);
            }
            _beams.Clear();
            director.SetLoop(AudioCatalog.BeamLoop, 0f, Vector3.zero);
            director.SetLoop(AudioCatalog.WalkScarab, 0f, Vector3.zero);
        }

        // ------------------------------------------------------------------ what happens in the battle

        private static Vector3 Place(SimUnit unit) => new Vector3(unit.Position.X, 0f, unit.Position.Y);

        /// <summary>The sound a weapon makes in the hands of its side's faction, or in Unlock Skirmish of the unit's own. Worked out once per weapon.</summary>
        public SfxCue FireCue(WeaponStats weapon, UnitStats shooter, Team team)
        {
            Dictionary<WeaponStats, SfxCue> known = team == Team.A ? _fireA : _fireB;
            if (known.TryGetValue(weapon, out SfxCue cue)) return cue;
            cue = AudioCatalog.FireFor(weapon, shooter, BattleFactions.OfUnit(shooter, team));
            known[weapon] = cue;
            return cue;
        }

        private void OnWeaponFired(SimUnit attacker, int weaponIndex, SimUnit target)
        {
            WeaponStats weapon = attacker.Weapons[weaponIndex].Stats;
            SfxCue cue = FireCue(weapon, attacker.Stats, attacker.Team);
            if (cue == null) return;
            if (!cue.Loops) GameAudio.Sfx(cue, Place(attacker));
            else Keep(cue, attacker.Id, _shown.ElapsedSeconds + Mathf.Max(0.25f, weapon.FireInterval * 1.6f), Place(attacker)); // Until it would have fired again, and a little more.

            if (target != null && ComesDown(weapon, attacker.Stats, attacker.Team))
            {
                // Heard coming down for as long as it flies, so that the sound ends as it lands (D118).
                float flight = Vec2.Distance(attacker.Position, target.Position) / weapon.ProjectileSpeed / Mathf.Max(0.05f, _battle.Speed);
                if (flight >= AudioCatalog.ShortestIncomingSeconds)
                {
                    float length = AudioCatalog.Incoming.Clips.AverageLength;
                    GameAudio.SfxShaped(AudioCatalog.Incoming, Place(target), Mathf.Max(0f, length - flight), flight, 0.05f);
                }
            }

            if (cue == AudioCatalog.FireMortar && AudioCatalog.FlightMortar.HasSound && target != null && weapon.ProjectileSpeed > 0f)
            {
                float flight = Vec2.Distance(attacker.Position, target.Position) / weapon.ProjectileSpeed;
                Keep(AudioCatalog.FlightMortar, --_shells, _shown.ElapsedSeconds + flight, (Place(attacker) + Place(target)) * 0.5f);
            }
        }

        /// <summary>Whether this weapon's shots are heard coming down (AudioCatalog.ComesDown), by the side's faction. Worked out once per weapon.</summary>
        private bool ComesDown(WeaponStats weapon, UnitStats shooter, Team team)
        {
            if (!AudioCatalog.Incoming.HasSound) return false;
            Dictionary<WeaponStats, bool> known = team == Team.A ? _comesDownA : _comesDownB;
            if (known.TryGetValue(weapon, out bool down)) return down;
            down = AudioCatalog.ComesDown(weapon, BattleFactions.OfUnit(shooter, team));
            known[weapon] = down;
            return down;
        }

        /// <summary>A weapon starts its wind-up: its own sound, if it has one, carries the shot at the end of it.</summary>
        private void OnWeaponWindingUp(SimUnit attacker, int weaponIndex, SimUnit target, float seconds)
        {
            WeaponStats weapon = attacker.Weapons[weaponIndex].Stats;
            if (!_windUps.TryGetValue(weapon, out SfxCue cue))
            {
                cue = AudioCatalog.WindUpFor(weapon);
                _windUps[weapon] = cue;
            }
            if (cue == null) return;
            // The file plays in real time and the wind-up in battle time: faster than 1x, start it part way in, so its bang still meets the shot.
            float skip = WindUpSkip(seconds, _battle.Speed);
            if (skip > 0.02f) GameAudio.SfxShaped(cue, Place(attacker), skip, 99f, 0.05f);
            else GameAudio.Sfx(cue, Place(attacker));
        }

        /// <summary>How far into the Spartan Laser's file to start, so that its shot lands with a wind-up of these battle seconds at this battle speed.</summary>
        public static float WindUpSkip(float windUpSeconds, float speed) =>
            Mathf.Max(0f, AudioCatalog.SpartanLaserShotSeconds - windUpSeconds / Mathf.Max(0.05f, speed));

        /// <summary>Keeps a loop going for this source until then (in the battle's time).</summary>
        private void Keep(SfxCue cue, int source, float until, Vector3 place)
        {
            if (!_loops.TryGetValue(cue, out Dictionary<int, Source> sources))
            {
                sources = new Dictionary<int, Source>(256);
                _loops[cue] = sources;
            }
            sources[source] = new Source { Until = until, Place = place };
        }

        /// <summary>A rocket or a shell going off where it lands: by the weapon that fired it, once per shot, however many it hits.</summary>
        private void OnShotLanded(SimUnit attacker, WeaponStats weapon, Vec2 point)
        {
            if (!_landings.TryGetValue(weapon, out SfxCue cue))
            {
                cue = AudioCatalog.LandingFor(weapon);
                _landings[weapon] = cue;
            }
            if (cue != null) GameAudio.Sfx(cue, new Vector3(point.X, 0f, point.Y));
        }

        // Hits and blocked shots make no sound of their own: the generated stand-ins are gone (the owner, 2026-11-07, D82).

        private void OnUnitDied(SimUnit unit, SimUnit killer)
        {
            if (DeathEffects.GrowsIntoFlood(_battle.Simulation, unit)) return; // Taken over by a seed, not destroyed: only the infection is heard (D122).
            SfxCue death = DeathCue(unit);
            AudioDirector director = AudioDirector.Instance;
            if (director != null && DriftsAsItDies(unit.Stats) && AudioCatalog.GhostKnockedOut.HasSound)
            {
                // A Ghost drifts away knocked out before it blows up (DeathEffects, D120): the owner's knock-out now, the blast as it goes off.
                // The knock-out is loudest at its end, made for a drift of 2.1 s: faster, its start is skipped so that it ends as the blast comes,
                // or the blast would be lost under it (D121); slower, it waits.
                float drift = DeathEffects.GhostDriftSeconds / Mathf.Max(0.05f, _battle.Speed);
                float early = DeathEffects.GhostDriftSeconds - drift;
                if (early >= 0f) GameAudio.SfxShaped(AudioCatalog.GhostKnockedOut, Place(unit), early, drift, 0.08f);
                else _later.Add(new Later { At = director.BattleClock - early, Cue = AudioCatalog.GhostKnockedOut, Place = Place(unit) });
                SfxCue blast = AudioCatalog.ExplosionTurret.HasSound ? AudioCatalog.ExplosionTurret : death;
                if (blast != null) _later.Add(new Later { At = director.BattleClock + drift, Cue = blast, Place = Place(unit) });
                return;
            }
            if (!FallsAsItDies(unit) || director == null)
            {
                GameAudio.Sfx(death, Place(unit));
                return;
            }
            float fall = AirFallRealSeconds(_battle.Speed);
            if (IsBanshee(unit.Stats) && AudioCatalog.DeathBanshee.HasSound)
            {
                // The owner's file falls for 1.3 s and then blows up (D120): started so that its blast comes as the wreck lands, whatever the battle speed.
                float early = AudioCatalog.BansheeFallInFile - fall;
                if (early >= 0f) GameAudio.SfxShaped(AudioCatalog.DeathBanshee, Place(unit), early, 10f, 0.1f); // Played out to its end.
                else _later.Add(new Later { At = director.BattleClock - early, Cue = AudioCatalog.DeathBanshee, Place = Place(unit) });
                return;
            }
            // It blows up in the air and falls burning (DeathEffects): the dive while it falls, fading as it lands, then its crash (D117).
            // A Flood creature (the Seeder) falls with the Infection Form's leap instead (the owner, 2026-11-10).
            GameAudio.SfxShaped(FallCue(unit.Stats), Place(unit), 0f, fall, 0.15f);
            if (death != null) _later.Add(new Later { At = director.BattleClock + fall, Cue = death, Place = Place(unit) });
        }

        /// <summary>What a dying aircraft sounds like as it falls: the dive, or for a Flood creature that isn't a taken machine (the Seeder) the Infection Form's leap.</summary>
        public static SfxCue FallCue(UnitStats stats)
        {
            bool creature = IsFloodBody(stats) && !AudioCatalog.TakenVehicle(stats, out _, out _);
            return creature && AudioCatalog.FloodCreatureFall.HasSound ? AudioCatalog.FloodCreatureFall : AudioCatalog.AirFall;
        }

        /// <summary>The Banshee and the Infested Banshee, whose fall and crash are one file of the owner's (D120).</summary>
        public static bool IsBanshee(UnitStats stats) => stats.Name == "Banshee" || stats.Name == "Infested Banshee";

        /// <summary>A Ghost or an Infested Ghost, which drifts away knocked out before it blows up (DeathEffects.Drifts, D120, D121).</summary>
        public static bool DriftsAsItDies(UnitStats stats) => DeathEffects.Drifts(stats);

        /// <summary>An aircraft that blows up in the air and falls to the ground as it dies (DeathStyle.Air, FloodAir). The Infected Pelican, already down from its dive, does not.</summary>
        public static bool FallsAsItDies(SimUnit unit)
        {
            if (unit.HasDived || !unit.IsAir) return false;
            DeathStyle style = DeathLooks.For(unit.Stats);
            return style == DeathStyle.Air || style == DeathStyle.FloodAir;
        }

        /// <summary>Real seconds a dead aircraft takes to reach the ground at this battle speed (its fall runs on battle time: UnitViewManager.AirFallSeconds).</summary>
        public static float AirFallRealSeconds(float speed) => UnitViewManager.AirFallSeconds / Mathf.Max(0.05f, speed);

        /// <summary>What a death sounds like, by what died.</summary>
        public static SfxCue DeathCue(SimUnit unit)
        {
            if (unit.HasDived) return AudioCatalog.DeathAirUnsc.HasSound ? AudioCatalog.DeathAirUnsc : AudioCatalog.PelicanCrash; // A UNSC airframe hitting the ground.
            if (unit.Stats.Name == "Scarab" && AudioCatalog.DeathScarab.HasSound) return AudioCatalog.DeathScarab;
            if (unit.Stats.Name.IndexOf("Infection Form", System.StringComparison.Ordinal) >= 0 && AudioCatalog.DeathInfectionForm.HasSound) return AudioCatalog.DeathInfectionForm;
            bool flood = IsFloodBody(unit.Stats);
            // A death burst with no reach only leaves bodies behind (the Infected Phantom): nothing goes off.
            if (flood && unit.Stats.DeathBurst != null && unit.Stats.DeathBurst.Radius > 0f) return AudioCatalog.FloodBurst;
            if (!flood && IsLekgolo(unit.Stats) && !AudioCatalog.DeathLekgolo.Clips.IsEmpty) return AudioCatalog.DeathLekgolo;
            SfxCue own = flood ? null : OwnDeath(unit.Stats);
            if (own != null && own.HasSound) return own;
            bool living = IsLiving(unit.Stats);
            if (unit.IsAir && !living)
            {
                // A taken aircraft goes down as it would have before (D119): the Infested Hornet and the Infected Pelican crash as UNSC aircraft.
                bool taken = AudioCatalog.TakenVehicle(unit.Stats, out _, out string was);
                string faction = taken ? was : BattleFactions.OfUnit(unit.Stats, unit.Team);
                if (AudioCatalog.DeathAirUnsc.HasSound && faction == BattleFactions.Unsc) return AudioCatalog.DeathAirUnsc;
                // Any other machine goes up with the owner's turret explosion (D120); a Flood creature (the Seeder) isn't one.
                if ((!flood || taken) && AudioCatalog.ExplosionTurret.HasSound) return AudioCatalog.ExplosionTurret;
                return AudioCatalog.DeathAir;
            }
            float radius = unit.Radius;
            if (flood)
            {
                // The owner's Flood deaths by size (D91): a form, a big creature or taken vehicle, the biggest.
                SfxCue bySize = radius >= 3f ? AudioCatalog.DeathFloodHuge : living || radius < 1f ? AudioCatalog.DeathFloodSmall : AudioCatalog.DeathFloodBig;
                if (bySize.HasSound) return bySize;
            }
            if (living || radius < 1f)
            {
                if (flood) return radius >= 3f ? AudioCatalog.PelicanCrash : AudioCatalog.DeathFlood;
                return BattleFactions.OfUnit(unit.Stats, unit.Team) == BattleFactions.Covenant ? AudioCatalog.DeathInfantryCovenant : AudioCatalog.DeathInfantry;
            }
            if (flood) return radius >= 3f ? AudioCatalog.PelicanCrash : AudioCatalog.FloodBurst;
            if (radius >= 3f) return AudioCatalog.ExplosionLarge;
            if (radius >= 1.6f) return AudioCatalog.ExplosionMedium;
            return AudioCatalog.ExplosionTurret.HasSound ? AudioCatalog.ExplosionTurret : AudioCatalog.DeathVehicle; // The owner's, D120.
        }

        /// <summary>The owner's death of one kind of soldier (D91), by its name, or null: humans, Spartans, Elites, Grunts, Jackals, Brutes.</summary>
        public static SfxCue OwnDeath(UnitStats stats)
        {
            string n = stats.Name ?? "";
            bool Has(string part) => n.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (Has("Goblin")) return null; // A Grunt in a walker: it goes up as a machine.
            if (Has("Spartan")) return AudioCatalog.DeathSpartan;
            if (Has("Marine") || Has("ODST") || Has("Flamethrower")) return AudioCatalog.DeathHuman;
            if (Has("Elite") || Has("Zealot")) return AudioCatalog.DeathElite;
            if (Has("Grunt") || Has("Cannon Fodder")) return AudioCatalog.DeathGrunt;
            if (Has("Jackal")) return AudioCatalog.DeathJackal;
            if (Has("Brute")) return AudioCatalog.DeathBrute;
            return null;
        }

        /// <summary>A leap's sound (D91): a Hunter's charge, or a Flood form pouncing. A dive has its own (Update); others leap silently.</summary>
        private void OnUnitLeapt(SimUnit unit)
        {
            if (unit.IsAir) return;
            SfxCue cue = LeapCue(unit.Stats);
            if (cue != null && cue.HasSound) GameAudio.Sfx(cue, Place(unit));
        }

        /// <summary>What a unit's leap sounds like, or null: Infection Forms have their own (the owner's, D108).</summary>
        public static SfxCue LeapCue(UnitStats stats)
        {
            if (stats.LeapCharges) return AudioCatalog.HunterCharge;
            if (stats.Name == "Infection Forms") return AudioCatalog.InfectionFormLeap;
            return IsFloodBody(stats) ? AudioCatalog.FloodLeap : null;
        }

        /// <summary>Worm colonies: the Hunters and what the Goliath is.</summary>
        private static bool IsLekgolo(UnitStats stats) => stats.Name == "Goliath" || stats.Name.IndexOf("Hunter", System.StringComparison.Ordinal) >= 0;

        /// <summary>A Flood form or something it has taken over, whichever side it is on: in the Sandbox, an army can be a mix.</summary>
        private static bool IsFloodBody(UnitStats stats) => stats.IsFlood || stats.InfectedFrom != null;

        /// <summary>Flesh rather than a machine: it has no engine and doesn't go up in flames. Whatever flies unarmored is a creature (Swarm Form); a Hunter is worms in armour plates.</summary>
        private static bool IsLiving(UnitStats stats)
        {
            return stats.ArmorType == ArmorType.Unarmored || stats.Name.IndexOf("Hunter", System.StringComparison.Ordinal) >= 0;
        }

        private void OnUnitInfected(SimUnit corpse) => GameAudio.Sfx(AudioCatalog.InfectionStart, Place(corpse));

        private void OnUnitRose(SimUnit corpse, SimUnit risen) => GameAudio.Sfx(AudioCatalog.InfectionRise, Place(risen));

        private void OnSpellTriggered(SimSpell spell)
        {
            if (_spellsAhead.Contains(spell)) return; // Its sound is already under way (StartSpellsAhead).
            SfxCue cue = AudioCatalog.Spell(spell.Stats, spell.TicksDone, CasterFaction(spell.Stats, spell.Team));
            if (cue != null) GameAudio.Sfx(cue, new Vector3(spell.Position.X, 0f, spell.Position.Y));
        }

        /// <summary>Whose sound a spell has: its side's, but in Unlock Skirmish a drop sounds of what it drops, so ODST pods on the Flood's land don't land like Flood.</summary>
        public static string CasterFaction(SpellStats spell, Team team)
        {
            return BattleFactions.PerUnitLooks && spell.DropUnit != null ? BattleFactions.OfUnit(spell.DropUnit, team) : BattleFactions.For(team);
        }

        /// <summary>A squad landing from a carrier. One hatched by a body on the ground (the Spawner's Swarm Brood, D113) comes with no landing thud.</summary>
        private void OnReinforcementDropped(SimUnit carrier, int squadId)
        {
            if (carrier.IsAir) GameAudio.Sfx(AudioCatalog.DropIn, Place(carrier));
        }

        private void NoteDiver(SimUnit unit)
        {
            if (unit.IsAir && unit.Stats.LeapRange > 0f) _divers.Add(unit);
        }

        // ------------------------------------------------------------------ every frame

        private void Update()
        {
            AudioDirector director = AudioDirector.Instance;
            if (director == null || _battle == null) return;
            // Either pause stops the battle's sound: the game's (Esc) and BattleHud's own (speed 0).
            director.SetBattlePaused(GamePause.IsPaused || (_battle.IsPlaying && _battle.Speed <= 0f));
            // Before the battle's end stops the rest (D121): the last aircraft still crashes after it, the last Ghost still blows up.
            if (!director.BattlePaused) PlayWhatIsDue(director);
            if (!_battle.IsPlaying) return;

            // A flying unit that has started its dive has no event of its own.
            for (int i = _divers.Count - 1; i >= 0; i--)
            {
                if (!_divers[i].HasDived) continue;
                GameAudio.Sfx(AudioCatalog.PelicanDive, Place(_divers[i]));
                _divers.RemoveAt(i);
            }

            if (Time.unscaledTime >= _nextHumCheck)
            {
                _nextHumCheck = Time.unscaledTime + 0.5f;
                UpdateHums(director);
            }
            if (director.BattlePaused) return;
            if (_shown.IsFinished) return; // A finished battle's time has stopped: OnBattleFinished silenced what it had going.
            UpdateLoops(director);
            if (AudioCatalog.BeamCharge.HasSound) UpdateBeams(director);
            StartSpellsAhead();
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.25f;
                UpdateCalls(director);
                UpdateShields(director);
            }
        }

        /// <summary>Each loop as loud as how many keep it going, from where they are on average; silent once none do.</summary>
        private void UpdateLoops(AudioDirector director)
        {
            float now = _shown.ElapsedSeconds;
            foreach (var pair in _loops)
            {
                Dictionary<int, Source> sources = pair.Value;
                int count = 0;
                Vector3 sum = Vector3.zero;
                _ended.Clear();
                foreach (var source in sources)
                {
                    if (source.Value.Until <= now) _ended.Add(source.Key);
                    else
                    {
                        count++;
                        sum += source.Value.Place;
                    }
                }
                foreach (int key in _ended) sources.Remove(key);
                director.SetLoop(pair.Key, count == 0 ? 0f : Mathf.Min(1f, 0.65f + 0.12f * (Mathf.Sqrt(count) - 1f)), count == 0 ? Vector3.zero : sum / count);
            }
        }

        private bool IsScarabBeam(WeaponStats weapon)
        {
            if (_isBeam.TryGetValue(weapon, out bool beam)) return beam;
            beam = WeaponFeel.For(weapon) == WeaponFeelKind.Beam;
            _isBeam[weapon] = beam;
            return beam;
        }

        /// <summary>The Scarab's beam in order: the charge as it starts charging, "in" as it fires, then the loop, and "out" as it stops.</summary>
        private void UpdateBeams(AudioDirector director)
        {
            _shown.GetBeams(_beamShots);
            float clock = director.BattleClock;
            int frame = Time.frameCount, firing = 0;
            Vector3 loopPlace = Vector3.zero;
            foreach (BeamShot shot in _beamShots)
            {
                if (!IsScarabBeam(shot.Attacker.Weapons[shot.WeaponIndex].Stats)) continue;
                int key = shot.Attacker.Id * 8 + shot.WeaponIndex;
                BeamPhase phase = shot.Firing ? BeamPhase.Firing : BeamPhase.Charging;
                Vector3 place = Place(shot.Attacker);
                if (!_beams.TryGetValue(key, out Beam beam)) beam = new Beam { Phase = BeamPhase.Off };
                if (phase == BeamPhase.Charging && beam.Phase != BeamPhase.Charging) GameAudio.Sfx(AudioCatalog.BeamCharge, place);
                if (phase == BeamPhase.Firing && beam.Phase != BeamPhase.Firing)
                {
                    GameAudio.Sfx(AudioCatalog.BeamIn, place);
                    beam.LoopFrom = clock + AudioCatalog.BeamIn.Clips.AverageLength * 0.9f;
                }
                beam.Phase = phase;
                beam.Place = place;
                beam.Frame = frame;
                _beams[key] = beam;
                if (phase == BeamPhase.Firing && clock >= beam.LoopFrom)
                {
                    firing++;
                    loopPlace = place;
                }
            }
            _ended.Clear();
            foreach (var pair in _beams)
            {
                if (pair.Value.Frame == frame) continue;
                if (pair.Value.Phase == BeamPhase.Firing) GameAudio.Sfx(AudioCatalog.BeamOut, pair.Value.Place);
                _ended.Add(pair.Key);
            }
            foreach (int key in _ended) _beams.Remove(key);
            director.SetLoop(AudioCatalog.BeamLoop, firing > 0 ? 1f : 0f, loopPlace);
        }

        /// <summary>
        /// A spell whose sound builds up before it strikes (the owner's Glassing Beam) starts that long before its first hit, by the battle's speed, and fades
        /// out as its beam goes (SpellEffects draws it for its hits and half a second more). If the battle starts too close to the hit, the build-up is skipped.
        /// </summary>
        private void StartSpellsAhead()
        {
            IReadOnlyList<SimSpell> spells = _shown.Spells;
            if (spells.Count == 0) return;
            float speed = _battle.Speed, now = _shown.ElapsedSeconds;
            for (int i = 0; i < spells.Count; i++)
            {
                SimSpell spell = spells[i];
                if (spell.TicksDone == 0 && !_fallingIn.Contains(spell)) HearComingDown(spell, now, speed);
                if (spell.TicksDone > 0 || _spellsAhead.Contains(spell)) continue;
                if (!_spellLeads.TryGetValue(spell.Stats, out float lead))
                {
                    lead = AudioCatalog.SpellLeadSeconds(spell.Stats);
                    _spellLeads[spell.Stats] = lead;
                }
                if (!SpellAhead(spell.Stats, lead, now, speed, out float skip, out float fadeAfter)) continue;
                _spellsAhead.Add(spell);
                SfxCue cue = AudioCatalog.Spell(spell.Stats, 1, CasterFaction(spell.Stats, spell.Team));
                if (cue != null) GameAudio.SfxShaped(cue, new Vector3(spell.Position.X, 0f, spell.Position.Y), skip, fadeAfter, AudioCatalog.GlassingFadeSeconds);
            }
        }

        /// <summary>A strike from the sky (AudioCatalog.FallsIn) is heard coming down, so that the sound ends as it hits; started part way in if the hit is closer than the sound is long.</summary>
        private void HearComingDown(SimSpell spell, float now, float speed)
        {
            if (!AudioCatalog.FallingIn.HasSound || !AudioCatalog.FallsIn(spell.Stats)) return;
            float length = AudioCatalog.FallingIn.Clips.AverageLength;
            if (!SpellAhead(spell.Stats, length, now, speed, out float skip, out _)) return;
            _fallingIn.Add(spell);
            GameAudio.SfxShaped(AudioCatalog.FallingIn, new Vector3(spell.Position.X, 0f, spell.Position.Y), skip, Mathf.Max(0f, length - skip - 0.05f), 0.05f);
        }

        /// <summary>
        /// Whether a spell's sound, which must start lead seconds (real time) before its first hit, starts now, at this moment of the battle and at this speed.
        /// If so: how far into the clip it starts (skip: the battle began too close to the hit) and after how long it fades (its hits, and a little more).
        /// </summary>
        public static bool SpellAhead(SpellStats spell, float lead, float now, float speed, out float skip, out float fadeAfter)
        {
            speed = Mathf.Max(0.05f, speed);
            float left = Mathf.Max(0f, (spell.DelaySeconds - now) / speed); // Real seconds to its first hit.
            skip = Mathf.Max(0f, lead - left);
            fadeAfter = left + (Mathf.Max(1, spell.Ticks) * spell.TickInterval + 0.1f) / speed;
            return lead > 0f && left <= lead;
        }

        private void PlayWhatIsDue(AudioDirector director)
        {
            for (int i = _later.Count - 1; i >= 0; i--)
            {
                if (director.BattleClock < _later[i].At) continue;
                GameAudio.Sfx(_later[i].Cue, _later[i].Place);
                _later.RemoveAt(i);
            }
        }

        private SfxCue CallOf(UnitStats stats)
        {
            if (_calls.TryGetValue(stats, out SfxCue cue)) return cue;
            cue = stats.Name == "Seeder" ? AudioCatalog.CallSeeder : stats.Name == "Swarm Form" ? AudioCatalog.CallSwarm : null;
            _calls[stats] = cue;
            return cue;
        }

        /// <summary>Seeders and Swarm Forms call: each one 3 to 6 s after its last call ended, one of its sounds at random.</summary>
        private void UpdateCalls(AudioDirector director)
        {
            float clock = director.BattleClock;
            IReadOnlyList<SimUnit> units = _shown.Units;
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                if (!unit.IsAlive || unit.NotArrived) continue;
                SfxCue cue = CallOf(unit.Stats);
                if (cue == null || !cue.HasSound) continue;
                if (!_nextCall.TryGetValue(unit.Id, out float at))
                {
                    _nextCall[unit.Id] = clock + 1f + 5f * director.NextRandom();
                    continue;
                }
                if (clock < at) continue;
                GameAudio.Sfx(cue, Place(unit));
                _nextCall[unit.Id] = clock + cue.Clips.AverageLength + 3f + 3f * director.NextRandom();
            }
        }

        /// <summary>An energy shield starting to recharge (the voice limit lets one be heard at a time).</summary>
        private void UpdateShields(AudioDirector director)
        {
            if (!AudioCatalog.ShieldRegen.HasSound) return;
            float delay = _battle.Rules.ShieldRegenDelaySeconds;
            IReadOnlyList<SimUnit> units = _shown.Units;
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                if (!unit.Stats.HasShield) continue;
                bool recharging = unit.IsAlive && unit.Shield < unit.Stats.MaxShield - 0.5f && unit.SecondsSinceDamaged >= delay;
                if (!recharging)
                {
                    _recharging.Remove(unit.Id);
                    continue;
                }
                if (_recharging.Add(unit.Id)) GameAudio.Sfx(AudioCatalog.ShieldRegen, Place(unit));
            }
        }

        /// <summary>One engine sound for all the ground vehicles, one hover sound for what flies or floats. Each only as loud as how many there are.</summary>
        private void UpdateHums(AudioDirector director)
        {
            int engines = 0, hovers = 0;
            foreach (SimUnit unit in _shown.Units)
            {
                if (!unit.IsAlive || IsLiving(unit.Stats) || unit.Stats.IsTower) continue;
                if (unit.IsAir) hovers++;
                else if (unit.Radius >= 1f)
                {
                    string faction = BattleFactions.OfUnit(unit.Stats, unit.Team);
                    if (faction == BattleFactions.Unsc) engines++;
                    else if (faction == BattleFactions.Covenant) hovers++;
                }
            }
            director.SetHums(Mathf.Sqrt(engines) * 0.3f, Mathf.Sqrt(hovers) * 0.3f);

            // A Scarab walking.
            if (!AudioCatalog.WalkScarab.HasSound) return;
            int walking = 0;
            Vector3 place = Vector3.zero;
            IReadOnlyList<SimUnit> units = _shown.Units;
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                if (!unit.IsAlive || unit.Stats.Name != "Scarab" || unit.Velocity.LengthSquared < 0.09f) continue;
                walking++;
                place = Place(unit);
            }
            director.SetLoop(AudioCatalog.WalkScarab, walking > 0 ? 0.8f : 0f, place);
        }
    }
}
