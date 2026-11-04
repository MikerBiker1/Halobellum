// BattleAudio.cs
// Listens to the battle in a scene and asks for its sounds: each shot from its weapon kind and the faction firing it, hits, explosions,
// deaths by size and kind, Flood bursts, infection rising, spells, drops, the Infected Pelican's dive. Added at run time to the object with
// the BattleController (see AudioDirector). It only reads the simulation's events and never changes anything, so battles play out the
// same with or without it. What a weapon sounds like is worked out once per weapon and kept: the many shots of a battle allocate nothing.
// The battle's sounds stop while it stands still, by the battle's own speed or by the game's pause (GamePause).
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
        private readonly List<SimUnit> _divers = new List<SimUnit>();
        private float _nextHumCheck;
        private bool _wasPlaying;

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
        }

        private void OnBattleChanged(BattleSimulation simulation)
        {
            Listen(simulation);
            // Leaving a battle cuts its sounds. A deployment preview is rebuilt on every action: that must not cut the sound of the action.
            if (_wasPlaying) AudioDirector.Instance?.StopBattleSounds();
            _wasPlaying = _battle.IsPlaying;
            _divers.Clear();
            if (simulation == null) return;
            foreach (SimUnit unit in simulation.Units) NoteDiver(unit);
        }

        private void Listen(BattleSimulation simulation)
        {
            if (_shown != null)
            {
                BattleEvents events = _shown.Events;
                events.WeaponFired -= OnWeaponFired;
                events.ShotLanded -= OnShotLanded;
                events.UnitDamaged -= OnUnitDamaged;
                events.AttackBlocked -= OnAttackBlocked;
                events.UnitDied -= OnUnitDied;
                events.UnitInfected -= OnUnitInfected;
                events.UnitRose -= OnUnitRose;
                events.SpellTriggered -= OnSpellTriggered;
                events.ReinforcementDropped -= OnReinforcementDropped;
                events.UnitSpawned -= NoteDiver;
            }
            _shown = simulation;
            if (_shown != null)
            {
                BattleEvents events = _shown.Events;
                events.WeaponFired += OnWeaponFired;
                events.ShotLanded += OnShotLanded;
                events.UnitDamaged += OnUnitDamaged;
                events.AttackBlocked += OnAttackBlocked;
                events.UnitDied += OnUnitDied;
                events.UnitInfected += OnUnitInfected;
                events.UnitRose += OnUnitRose;
                events.SpellTriggered += OnSpellTriggered;
                events.ReinforcementDropped += OnReinforcementDropped;
                events.UnitSpawned += NoteDiver;
            }
        }

        private void OnBattleFinished(BattleResult result) => AudioDirector.Instance?.SetHums(0f, 0f);

        // ------------------------------------------------------------------ what happens in the battle

        private static Vector3 Place(SimUnit unit) => new Vector3(unit.Position.X, 0f, unit.Position.Y);

        /// <summary>The sound a weapon makes in the hands of a team's faction. Worked out once per weapon.</summary>
        public SfxCue FireCue(WeaponStats weapon, Team team) => FireCue(weapon, null, team);

        /// <summary>The same when the unit is known: in Unlock Skirmish it sounds like its own faction.</summary>
        public SfxCue FireCue(WeaponStats weapon, UnitStats shooter, Team team)
        {
            Dictionary<WeaponStats, SfxCue> known = team == Team.A ? _fireA : _fireB;
            if (known.TryGetValue(weapon, out SfxCue cue)) return cue;
            cue = AudioCatalog.Fire(WeaponFeel.For(weapon), BattleFactions.OfUnit(shooter, team));
            known[weapon] = cue;
            return cue;
        }

        private void OnWeaponFired(SimUnit attacker, int weaponIndex, SimUnit target)
        {
            SfxCue cue = FireCue(attacker.Weapons[weaponIndex].Stats, attacker.Stats, attacker.Team);
            if (cue != null) GameAudio.Sfx(cue, Place(attacker));
        }

        /// <summary>A rocket or a shell going off where it lands: by the weapon that fired it, once per shot, however many it hits.</summary>
        private void OnShotLanded(SimUnit attacker, WeaponStats weapon, Vec2 point)
        {
            if (!_landings.TryGetValue(weapon, out SfxCue cue))
            {
                cue = AudioCatalog.LandingOf(weapon);
                _landings[weapon] = cue;
            }
            if (cue != null) GameAudio.Sfx(cue, new Vector3(point.X, 0f, point.Y));
        }

        private void OnUnitDamaged(SimUnit target, SimUnit attacker, float amount)
        {
            if (amount <= 0f || attacker == null) return; // Damage with no attacker is a slow burn, not a hit.
            SfxCue cue;
            if (target.Shield > 0f) cue = AudioCatalog.ImpactShield;
            else if (IsFloodBody(target.Stats)) cue = AudioCatalog.ImpactWet;
            else cue = target.Stats.ArmorType == ArmorType.Unarmored ? AudioCatalog.ImpactFlesh : AudioCatalog.ImpactArmor;
            GameAudio.Sfx(cue, Place(target));
        }

        private void OnAttackBlocked(SimUnit target, SimUnit attacker) => GameAudio.Sfx(AudioCatalog.ImpactBlock, Place(target));

        private void OnUnitDied(SimUnit unit, SimUnit killer) => GameAudio.Sfx(DeathCue(unit), Place(unit));

        /// <summary>What a death sounds like, by what died.</summary>
        public static SfxCue DeathCue(SimUnit unit)
        {
            if (unit.HasDived) return AudioCatalog.PelicanCrash;
            bool flood = IsFloodBody(unit.Stats);
            if (flood && unit.Stats.DeathBurst != null) return AudioCatalog.FloodBurst;
            if (!flood && IsLekgolo(unit.Stats) && !AudioCatalog.DeathLekgolo.Clips.IsEmpty) return AudioCatalog.DeathLekgolo;
            bool living = IsLiving(unit.Stats);
            if (unit.IsAir && !living) return AudioCatalog.DeathAir;
            float radius = unit.Radius;
            if (living || radius < 1f)
            {
                if (flood) return radius >= 3f ? AudioCatalog.PelicanCrash : AudioCatalog.DeathFlood;
                return BattleFactions.OfUnit(unit.Stats, unit.Team) == BattleFactions.Covenant ? AudioCatalog.DeathInfantryCovenant : AudioCatalog.DeathInfantry;
            }
            if (flood) return radius >= 3f ? AudioCatalog.PelicanCrash : AudioCatalog.FloodBurst;
            if (radius >= 3f) return AudioCatalog.ExplosionLarge;
            return radius >= 1.6f ? AudioCatalog.ExplosionMedium : AudioCatalog.DeathVehicle;
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
            SfxCue cue = AudioCatalog.Spell(spell.Stats, spell.TicksDone, CasterFaction(spell.Stats, spell.Team));
            if (cue != null) GameAudio.Sfx(cue, new Vector3(spell.Position.X, 0f, spell.Position.Y));
        }

        /// <summary>Whose sound a spell has: its side's, but in Unlock Skirmish a drop sounds of what it drops, so ODST pods on the Flood's land don't land like Flood.</summary>
        public static string CasterFaction(SpellStats spell, Team team)
        {
            return BattleFactions.PerUnitLooks && spell.DropUnit != null ? BattleFactions.OfUnit(spell.DropUnit, team) : BattleFactions.For(team);
        }

        private void OnReinforcementDropped(SimUnit carrier, int squadId) => GameAudio.Sfx(AudioCatalog.DropIn, Place(carrier));

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
        }
    }
}
