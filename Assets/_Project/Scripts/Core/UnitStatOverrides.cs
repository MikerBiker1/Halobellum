// UnitStatOverrides.cs
// Unit stats a player changed in the JSON files next to the built game (mod_json): units, their weapons and their techs.
// The assets stay the defaults. A built game reads the files at launch (Presentation's UnitStatsMods with Economy's UnitStatsFolder); when every
// file is right, the values that differ from the defaults are kept here, and UnitDefinition.ToStats applies them to every copy of the unit it makes
// (Apply), so they reach the simulation, the AI's prices and every stats window. Active is null everywhere else: in the editor, in tests, in the
// tournament and the counter web, and in a game whose files have a mistake.
//   Write: one file's text from the default stats: every moddable field, in a fixed order, one per line.
//   Read: checks one file against the defaults (syntax, unknown or repeated units, unknown fields, wrong types, whole numbers, enum names, the
//         weapon and tech lists, fields the unit doesn't have, and limits on changed values) and adds what changed. Messages name the file, line
//         and field: "UNSC.json line 42: Marine, weapon 1 (MA5 Assault Rifle): 'damagePerShot' must be a number."
// Only a value that really differs counts (floats within a millionth are the same), so an unedited file changes nothing and plays bit-identical.
// Limits apply to changed values only, so the shipped values can never make an unedited file wrong.
// Plain C# 9 without the engine. Depends on: StatJson, UnitStats, TechUpgrade, CombatEnums.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Halobellum.Core
{
    /// <summary>The units of one stats file (one faction's base roster or one sub-faction), as the defaults made them.</summary>
    public sealed class UnitStatGroup
    {
        /// <summary>The file's name, for example "UNSC_Noble.json".</summary>
        public string FileName = "";
        /// <summary>What the file holds, for its help line, for example "UNSC, Noble exclusives".</summary>
        public string Title = "";
        public List<UnitStats> Units = new List<UnitStats>();
        /// <summary>Names of the units a player buys from a roster (the deploy footprint check is for these).</summary>
        public HashSet<string> Buyable = new HashSet<string>(StringComparer.Ordinal);
    }

    public sealed class UnitStatOverrides
    {
        /// <summary>The overrides in force, or null for the defaults. Only a built game's launch sets it (UnitStatsMods).</summary>
        public static UnitStatOverrides Active;

        /// <summary>A float counts as changed only beyond this share of its default (at least this much absolutely).</summary>
        public const double FloatTolerance = 1e-6;

        private readonly Dictionary<string, List<Action<UnitStats>>> _byUnit = new Dictionary<string, List<Action<UnitStats>>>(StringComparer.Ordinal);

        /// <summary>One line per changed value, in file order: "Marine: cost 100 -> 150".</summary>
        public readonly List<string> Changes = new List<string>();

        public int ChangeCount => Changes.Count;

        /// <summary>True if the files change this unit.</summary>
        public bool ChangesUnit(string unitName) => unitName != null && _byUnit.ContainsKey(unitName);

        /// <summary>Sets this unit's changed values on a freshly made copy of it. Every nested target is checked, as the depth guard can leave it null.</summary>
        public void Apply(UnitStats stats)
        {
            if (stats == null || stats.Name == null || !_byUnit.TryGetValue(stats.Name, out List<Action<UnitStats>> sets)) return;
            foreach (Action<UnitStats> set in sets) set(stats);
        }

        /// <summary>Where one change was made, so a later check on the built unit (UnitStatsFolder's dry run) can name the file, line and field.</summary>
        public sealed class ChangeSite
        {
            public string File;
            public int Line;
            /// <summary>"" for the unit's own fields, "weapon:I" (I counted from 0, as UnitStats.Validate counts), "tech:NAME", or a section's name.</summary>
            public string Part;
            /// <summary>The key changed, for example "minRange".</summary>
            public string Key;
            /// <summary>The change as Changes lists it: "Marine, weapon 1 (MA5 Assault Rifle): damagePerShot 4 -> 5".</summary>
            public string Change;
        }

        private static readonly ChangeSite[] NoSites = new ChangeSite[0];
        private readonly Dictionary<string, List<ChangeSite>> _sites = new Dictionary<string, List<ChangeSite>>(StringComparer.Ordinal);

        /// <summary>This unit's changes with their file and line, in file order (empty if the files don't change it).</summary>
        public IReadOnlyList<ChangeSite> SitesOf(string unitName) =>
            unitName != null && _sites.TryGetValue(unitName, out List<ChangeSite> sites) ? sites : (IReadOnlyList<ChangeSite>)NoSites;

        private void Add(string unit, Action<UnitStats> set, string change, Reader reader, JsonNode at, string part, string key)
        {
            if (!_byUnit.TryGetValue(unit, out List<Action<UnitStats>> sets))
            {
                sets = new List<Action<UnitStats>>();
                _byUnit[unit] = sets;
            }
            sets.Add(set);
            Changes.Add(change);
            if (!_sites.TryGetValue(unit, out List<ChangeSite> sites))
            {
                sites = new List<ChangeSite>();
                _sites[unit] = sites;
            }
            sites.Add(new ChangeSite { File = reader.File, Line = at.Line, Part = part, Key = key, Change = change });
        }

        // ------------------------------------------------------------------ the fields

        private enum Kind { Int, Float, Bool, Enum }

        private sealed class Field<T>
        {
            public string Name;
            public Kind Kind;
            public Type EnumType;
            public double Min, Max;
            public bool MinExclusive;
            /// <summary>Null = always there. Otherwise the field is written, and allowed, only when the default has it.</summary>
            public Func<T, bool> Present;
            public Func<T, object> Get;
            public Action<T, object> Set;

            public bool IsPresent(T target) => Present == null || Present(target);
        }

        private sealed class Section
        {
            public string Name;
            public Func<UnitStats, bool> Present;
            public Field<UnitStats>[] Fields;
        }

        private const double Big = 1e6;

        private static Field<T> F<T>(string name, double min, double max, Func<T, float> get, Action<T, float> set, Func<T, bool> present = null, bool above = false) =>
            new Field<T> { Name = name, Kind = Kind.Float, Min = min, Max = max, MinExclusive = above, Present = present, Get = t => get(t), Set = (t, v) => set(t, (float)v) };

        private static Field<T> I<T>(string name, int min, int max, Func<T, int> get, Action<T, int> set, Func<T, bool> present = null) =>
            new Field<T> { Name = name, Kind = Kind.Int, Min = min, Max = max, Present = present, Get = t => get(t), Set = (t, v) => set(t, (int)v) };

        private static Field<T> B<T>(string name, Func<T, bool> get, Action<T, bool> set) =>
            new Field<T> { Name = name, Kind = Kind.Bool, Get = t => get(t), Set = (t, v) => set(t, (bool)v) };

        private static Field<T> E<T>(string name, Type type, Func<T, int> get, Action<T, int> set, Func<T, bool> present = null) =>
            new Field<T> { Name = name, Kind = Kind.Enum, EnumType = type, Present = present, Get = t => get(t), Set = (t, v) => set(t, (int)v) };

        private static readonly Field<UnitStats>[] UnitFields =
        {
            I<UnitStats>("cost", 1, 100000, s => s.Cost, (s, v) => s.Cost = v),
            I<UnitStats>("unlockCost", 0, 100000, s => s.UnlockCost, (s, v) => s.UnlockCost = v),
            I<UnitStats>("squadSize", 1, 100, s => s.SquadSize, (s, v) => s.SquadSize = v),
            I<UnitStats>("tier", 1, 4, s => s.Tier, (s, v) => s.Tier = v),
            F<UnitStats>("baseDamage", 0, Big, s => s.BaseDamage, (s, v) => s.BaseDamage = v),
            F<UnitStats>("maxHealth", 0, Big, s => s.MaxHealth, (s, v) => s.MaxHealth = v, above: true),
            E<UnitStats>("armorType", typeof(ArmorType), s => (int)s.ArmorType, (s, v) => s.ArmorType = (ArmorType)v),
            F<UnitStats>("maxShield", 0, Big, s => s.MaxShield, (s, v) => s.MaxShield = v),
            F<UnitStats>("shieldRegenPerSecond", 0, Big, s => s.ShieldRegenPerSecond, (s, v) => s.ShieldRegenPerSecond = v),
            F<UnitStats>("moveSpeed", 0, 30, s => s.MoveSpeed, (s, v) => s.MoveSpeed = v),
            F<UnitStats>("turnTime180", 0, 60, s => s.TurnTime180, (s, v) => s.TurnTime180 = v),
            E<UnitStats>("targetFilter", typeof(TargetFilter), s => (int)s.TargetFilter, (s, v) => s.TargetFilter = (TargetFilter)v),
            F<UnitStats>("lifestealShare", 0, 1, s => s.LifestealShare, (s, v) => s.LifestealShare = v),
        };

        private static readonly Field<WeaponStats>[] WeaponFields =
        {
            F<WeaponStats>("damagePerShot", 0, Big, w => w.DamagePerShot, (w, v) => w.DamagePerShot = v),
            F<WeaponStats>("fireInterval", 0.05, 1000, w => w.FireInterval, (w, v) => w.FireInterval = v),
            F<WeaponStats>("range", 0, 150, w => w.Range, (w, v) => w.Range = v, above: true),
            F<WeaponStats>("minRange", 0, 150, w => w.MinRange, (w, v) => w.MinRange = v),
            F<WeaponStats>("projectileSpeed", 0, 10000, w => w.ProjectileSpeed, (w, v) => w.ProjectileSpeed = v),
            F<WeaponStats>("splashRadius", 0, 30, w => w.SplashRadius, (w, v) => w.SplashRadius = v),
            F<WeaponStats>("airDamageMultiplier", 0, 100, w => w.AirDamageMultiplier, (w, v) => w.AirDamageMultiplier = v),
            E<WeaponStats>("damageType", typeof(DamageType), w => (int)w.DamageType, (w, v) => w.DamageType = (DamageType)v),
            B<WeaponStats>("isAntiArmor", w => w.IsAntiArmor, (w, v) => w.IsAntiArmor = v),
            B<WeaponStats>("canBeBlocked", w => w.CanBeBlocked, (w, v) => w.CanBeBlocked = v),
            E<WeaponStats>("targetFilter", typeof(TargetFilter), w => (int)w.TargetFilter, (w, v) => w.TargetFilter = (TargetFilter)v, w => w.HasOwnTargetFilter),
            F<WeaponStats>("coneDegrees", 0, 360, w => w.ConeDegrees, (w, v) => w.ConeDegrees = v, w => w.ConeDegrees > 0f),
            F<WeaponStats>("beamSeconds", 0, 10000, w => w.BeamSeconds, (w, v) => w.BeamSeconds = v, w => w.IsBeam),
            F<WeaponStats>("beamChargeSeconds", 0, 10000, w => w.BeamChargeSeconds, (w, v) => w.BeamChargeSeconds = v, w => w.IsBeam),
            F<WeaponStats>("beamRestSeconds", 0, 10000, w => w.BeamRestSeconds, (w, v) => w.BeamRestSeconds = v, w => w.IsBeam),
            F<WeaponStats>("beamSweepSpeed", 0, 10000, w => w.BeamSweepSpeed, (w, v) => w.BeamSweepSpeed = v, w => w.IsBeam),
            F<WeaponStats>("seedSeconds", 0, 10000, w => w.SeedSeconds, (w, v) => w.SeedSeconds = v, w => w.SeedSeconds > 0f),
            F<WeaponStats>("seedDamageSharePerSecond", 0, 1, w => w.SeedDamageSharePerSecond, (w, v) => w.SeedDamageSharePerSecond = v, w => w.SeedSeconds > 0f),
            F<WeaponStats>("seedSlowShare", 0, 1, w => w.SeedSlowShare, (w, v) => w.SeedSlowShare = v, w => w.SeedSeconds > 0f),
            F<WeaponStats>("hitSlowSeconds", 0, 10000, w => w.HitSlowSeconds, (w, v) => w.HitSlowSeconds = v, w => w.HitSlowSeconds > 0f),
            F<WeaponStats>("hitSlowShare", 0, 1, w => w.HitSlowShare, (w, v) => w.HitSlowShare = v, w => w.HitSlowSeconds > 0f),
            F<WeaponStats>("windUpSeconds", 0, 10000, w => w.WindUpSeconds, (w, v) => w.WindUpSeconds = v, w => w.WindUpSeconds > 0f),
            F<WeaponStats>("staggerSeconds", 0, 10000, w => w.StaggerSeconds, (w, v) => w.StaggerSeconds = v, w => w.StaggerSeconds > 0f),
        };

        private static readonly Field<TechUpgrade>[] TechFields =
        {
            I<TechUpgrade>("cost", 0, 100000, t => t.Cost, (t, v) => t.Cost = v),
            F<TechUpgrade>("healthMultiplier", 0, 100, t => t.HealthMultiplier, (t, v) => t.HealthMultiplier = v, above: true),
            F<TechUpgrade>("damageMultiplier", 0, 100, t => t.DamageMultiplier, (t, v) => t.DamageMultiplier = v, above: true),
            F<TechUpgrade>("fireRateMultiplier", 0, 100, t => t.FireRateMultiplier, (t, v) => t.FireRateMultiplier = v, above: true),
            F<TechUpgrade>("moveSpeedMultiplier", 0, 100, t => t.MoveSpeedMultiplier, (t, v) => t.MoveSpeedMultiplier = v, above: true),
            F<TechUpgrade>("airDamageMultiplier", 0, 100, t => t.AirDamageMultiplier, (t, v) => t.AirDamageMultiplier = v, above: true),
            F<TechUpgrade>("reloadTimeMultiplier", 0, 100, t => t.ReloadTimeMultiplier, (t, v) => t.ReloadTimeMultiplier = v, above: true),
            F<TechUpgrade>("rangeBonus", 0, 150, t => t.RangeBonus, (t, v) => t.RangeBonus = v),
            F<TechUpgrade>("shieldBonus", 0, Big, t => t.ShieldBonus, (t, v) => t.ShieldBonus = v),
            F<TechUpgrade>("shieldRegenBonus", 0, Big, t => t.ShieldRegenBonus, (t, v) => t.ShieldRegenBonus = v),
            F<TechUpgrade>("splashRadiusBonus", 0, 30, t => t.SplashRadiusBonus, (t, v) => t.SplashRadiusBonus = v),
            F<TechUpgrade>("coneDegreesBonus", 0, 360, t => t.ConeDegreesBonus, (t, v) => t.ConeDegreesBonus = v),
            I<TechUpgrade>("extraShotsPerAttack", 0, 10, t => t.ExtraShotsPerAttack, (t, v) => t.ExtraShotsPerAttack = v),
            F<TechUpgrade>("burstGapSeconds", 0, 60, t => t.BurstGapSeconds, (t, v) => t.BurstGapSeconds = v),
        };

        private static UnitStats DropOf(UnitStats s) => s.Reinforcement ?? s.LockedReinforcement;

        private static readonly Section[] Sections =
        {
            new Section
            {
                Name = "defenseArc", Present = s => s.DefenseArc != null, Fields = new[]
                {
                    F<UnitStats>("arcDegrees", 0, 360, s => s.DefenseArc.ArcDegrees, (s, v) => { if (s.DefenseArc != null) s.DefenseArc.ArcDegrees = v; }),
                    F<UnitStats>("frontDamageMultiplier", 0, 100, s => s.DefenseArc.FrontDamageMultiplier, (s, v) => { if (s.DefenseArc != null) s.DefenseArc.FrontDamageMultiplier = v; }),
                    F<UnitStats>("rearDamageMultiplier", 0, 100, s => s.DefenseArc.RearDamageMultiplier, (s, v) => { if (s.DefenseArc != null) s.DefenseArc.RearDamageMultiplier = v; }),
                    F<UnitStats>("blockChance", 0, 1, s => s.DefenseArc.BlockChance, (s, v) => { if (s.DefenseArc != null) s.DefenseArc.BlockChance = v; }),
                },
            },
            new Section
            {
                Name = "deathBurst", Present = s => s.DeathBurst != null, Fields = new[]
                {
                    F<UnitStats>("damage", 0, Big, s => s.DeathBurst.Damage, (s, v) => { if (s.DeathBurst != null) s.DeathBurst.Damage = v; }),
                    F<UnitStats>("radius", 0, 30, s => s.DeathBurst.Radius, (s, v) => { if (s.DeathBurst != null) s.DeathBurst.Radius = v; }),
                },
            },
            new Section
            {
                Name = "spawn", Present = s => s.SpawnedUnit != null, Fields = new[]
                {
                    I<UnitStats>("bodies", 1, 100, s => s.SpawnedUnit.SquadSize, (s, v) => { if (s.SpawnedUnit != null) s.SpawnedUnit.SquadSize = v; }),
                    F<UnitStats>("intervalSeconds", 0, 10000, s => s.SpawnIntervalSeconds, (s, v) => s.SpawnIntervalSeconds = v, above: true),
                    I<UnitStats>("alternateBodies", 1, 100, s => s.AlternateSpawnedUnit.SquadSize, (s, v) => { if (s.AlternateSpawnedUnit != null) s.AlternateSpawnedUnit.SquadSize = v; }, s => s.AlternateSpawnedUnit != null),
                },
            },
            new Section
            {
                Name = "reinforcement", Present = s => DropOf(s) != null, Fields = new[]
                {
                    I<UnitStats>("bodies", 1, 100, s => DropOf(s).SquadSize, (s, v) => { if (DropOf(s) != null) DropOf(s).SquadSize = v; }),
                    F<UnitStats>("delaySeconds", 0, 10000, s => s.ReinforcementDelaySeconds, (s, v) => { if (DropOf(s) != null) s.ReinforcementDelaySeconds = v; }),
                    I<UnitStats>("drops", 1, 20, s => s.ReinforcementDrops, (s, v) => { if (DropOf(s) != null) s.ReinforcementDrops = v; }),
                    F<UnitStats>("landSeconds", 0, 10000, s => s.ReinforcementLandSeconds, (s, v) => { if (DropOf(s) != null) s.ReinforcementLandSeconds = v; }),
                    F<UnitStats>("firstDropSeconds", 0, 10000, s => s.ReinforcementFirstDropSeconds, (s, v) => { if (DropOf(s) != null) s.ReinforcementFirstDropSeconds = v; }),
                },
            },
            new Section
            {
                Name = "aura", Present = s => s.AuraRadius > 0f, Fields = new[]
                {
                    F<UnitStats>("radius", 0, 100, s => s.AuraRadius, (s, v) => s.AuraRadius = v),
                    F<UnitStats>("speedMultiplier", 0, 100, s => s.AuraSpeedMultiplier, (s, v) => s.AuraSpeedMultiplier = v, above: true),
                    F<UnitStats>("healPerSecondShare", 0, 1, s => s.AuraHealPerSecondShare, (s, v) => s.AuraHealPerSecondShare = v),
                },
            },
            new Section
            {
                Name = "leap", Present = s => s.LeapRange > 0f, Fields = new[]
                {
                    F<UnitStats>("range", 0, 150, s => s.LeapRange, (s, v) => s.LeapRange = v),
                    F<UnitStats>("speed", 0, 10000, s => s.LeapSpeed, (s, v) => s.LeapSpeed = v, above: true),
                    F<UnitStats>("cooldownSeconds", 0, 10000, s => s.LeapCooldownSeconds, (s, v) => s.LeapCooldownSeconds = v),
                },
            },
        };

        private const string SpawnBodiesKey = "spawnBodies";

        // ------------------------------------------------------------------ writing

        /// <summary>What the top of every file says.</summary>
        public static string HelpText(string title) =>
            $"Halobellum unit stats: {title}. Change numbers, then restart the game. If anything is wrong, the main menu says which file, line and field, " +
            "and the game uses its own values for every unit; it never changes this file. Delete this file to get this version's values back. " +
            "A new version of the game writes its own values over these files, so keep a copy of your changes.";

        /// <summary>One file's text: the help line, the game's version and every moddable field of these units, one per line.</summary>
        public static string Write(string title, IEnumerable<UnitStats> units, string gameVersion)
        {
            var text = new StringBuilder();
            text.Append("{\n");
            text.Append("  \"_help\": ").Append(StatJson.Quote(HelpText(title))).Append(",\n");
            text.Append("  \"gameVersion\": ").Append(StatJson.Quote(gameVersion ?? "")).Append(",\n");
            text.Append("  \"units\": [");
            bool firstUnit = true;
            foreach (UnitStats unit in units)
            {
                if (unit == null) continue;
                text.Append(firstUnit ? "\n" : ",\n");
                firstUnit = false;
                WriteUnit(text, unit);
            }
            text.Append(firstUnit ? "]\n" : "\n  ]\n");
            text.Append("}\n");
            return text.ToString();
        }

        private static void WriteUnit(StringBuilder text, UnitStats unit)
        {
            const string indent = "      ";
            var lines = new List<string> { indent + "\"name\": " + StatJson.Quote(unit.Name) };
            AddFieldLines(lines, indent, unit, UnitFields);
            foreach (Section section in Sections)
            {
                if (!section.Present(unit)) continue;
                var inner = new List<string>();
                AddFieldLines(inner, indent + "  ", unit, section.Fields);
                if (section.Name == "deathBurst" && unit.DeathBurst.Spawns != null && unit.DeathBurst.Spawns.Length > 0)
                {
                    var bodies = new List<string>();
                    foreach (SpawnEntry spawn in unit.DeathBurst.Spawns) bodies.Add(StatJson.Number(spawn?.Bodies ?? 0));
                    inner.Add(indent + "  " + StatJson.Quote(SpawnBodiesKey) + ": [" + string.Join(", ", bodies) + "]");
                }
                lines.Add(indent + StatJson.Quote(section.Name) + ": {\n" + string.Join(",\n", inner) + "\n" + indent + "}");
            }
            lines.Add(indent + "\"weapons\": " + ListText(indent, unit.Weapons, (w, i) => ObjectLines(i, w.Name, w, WeaponFields)));
            lines.Add(indent + "\"techs\": " + ListText(indent, unit.TechUpgrades, (t, i) => ObjectLines(i, t.Name, t, TechFields)));
            text.Append("    {\n").Append(string.Join(",\n", lines)).Append("\n    }");
        }

        private static List<string> ObjectLines<T>(string indent, string name, T target, Field<T>[] fields)
        {
            var lines = new List<string> { indent + "\"name\": " + StatJson.Quote(name) };
            AddFieldLines(lines, indent, target, fields);
            return lines;
        }

        private static string ListText<T>(string indent, T[] items, Func<T, string, List<string>> lines) where T : class
        {
            if (items == null || items.Length == 0) return "[]";
            var parts = new List<string>();
            foreach (T item in items)
            {
                if (item == null) parts.Add(indent + "  null");
                else parts.Add(indent + "  {\n" + string.Join(",\n", lines(item, indent + "    ")) + "\n" + indent + "  }");
            }
            return "[\n" + string.Join(",\n", parts) + "\n" + indent + "]";
        }

        private static void AddFieldLines<T>(List<string> lines, string indent, T target, Field<T>[] fields)
        {
            foreach (Field<T> field in fields)
            {
                if (field.IsPresent(target)) lines.Add(indent + StatJson.Quote(field.Name) + ": " + ValueText(field, field.Get(target)));
            }
        }

        private static string ValueText<T>(Field<T> field, object value)
        {
            switch (field.Kind)
            {
                case Kind.Int: return StatJson.Number((int)value);
                case Kind.Float: return StatJson.Number((float)value);
                case Kind.Bool: return (bool)value ? "true" : "false";
                default: return StatJson.Quote(Enum.GetName(field.EnumType, (int)value) ?? ((int)value).ToString(CultureInfo.InvariantCulture));
            }
        }

        // ------------------------------------------------------------------ reading

        /// <summary>
        /// Checks one file against the defaults and adds the values it changes. Problems go to errors (any error means the game uses the defaults),
        /// harmless things to notices. seen maps each unit already read, in this file or an earlier one, to its file, so a unit is given only once.
        /// </summary>
        public void Read(string fileName, string text, IReadOnlyDictionary<string, UnitStats> defaults, string gameVersion,
            List<string> errors, List<string> notices, Dictionary<string, string> seen)
        {
            JsonNode root;
            try
            {
                root = StatJson.Parse(text, fileName);
            }
            catch (StatJsonException e)
            {
                errors.Add(e.Message);
                return;
            }
            var reader = new Reader { File = fileName, Errors = errors };
            if (root.Kind != JsonKind.Object)
            {
                reader.Error(root, "the file must be one object { } holding \"units\": [ ... ].");
                return;
            }
            JsonNode units = null;
            foreach (var pair in root.Fields)
            {
                if (pair.Key.StartsWith("_", StringComparison.Ordinal)) continue;
                if (pair.Key == "units") units = pair.Value;
                else if (pair.Key == "gameVersion")
                {
                    if (pair.Value.Kind != JsonKind.String) reader.Error(pair.Value, "'gameVersion' must be text in quotes.");
                    else if (!string.IsNullOrEmpty(gameVersion) && pair.Value.Str != gameVersion)
                        notices.Add($"{fileName} was written by version {pair.Value.Str}; this game is {gameVersion}. Delete it to get this version's values.");
                }
                else reader.Error(pair.Value, $"unknown field '{pair.Key}' (the file holds \"gameVersion\" and \"units\"; names starting with _ are notes).");
            }
            if (units == null)
            {
                reader.Error(root, "the file has no \"units\": [ ... ] list.");
                return;
            }
            if (units.Kind != JsonKind.Array)
            {
                reader.Error(units, "'units' must be a list [ ].");
                return;
            }
            foreach (JsonNode unit in units.Items) ReadUnit(reader, unit, defaults, seen);
        }

        private sealed class Reader
        {
            public string File;
            public List<string> Errors;

            public void Error(JsonNode at, string message) => Errors.Add($"{File} line {at.Line}: {message}");
        }

        private void ReadUnit(Reader reader, JsonNode node, IReadOnlyDictionary<string, UnitStats> defaults, Dictionary<string, string> seen)
        {
            if (node.Kind != JsonKind.Object)
            {
                reader.Error(node, $"each entry of 'units' must be an object {{ }}, not {JsonNode.KindName(node.Kind)}.");
                return;
            }
            JsonNode nameNode = node.Field("name");
            if (nameNode == null || nameNode.Kind != JsonKind.String)
            {
                reader.Error(nameNode ?? node, "a unit needs a \"name\" in quotes.");
                return;
            }
            string name = nameNode.Str;
            if (!defaults.TryGetValue(name, out UnitStats def) || def == null)
            {
                reader.Error(nameNode, $"there is no unit called \"{name}\" in this version (names must match exactly).");
                return;
            }
            if (seen.TryGetValue(name, out string otherFile))
            {
                reader.Error(nameNode, otherFile == reader.File ? $"\"{name}\" is given twice in this file." : $"\"{name}\" is also given in {otherFile}; give each unit once.");
                return;
            }
            seen[name] = reader.File;

            var handled = new HashSet<string>(StringComparer.Ordinal) { "name", "weapons", "techs" };
            foreach (Section section in Sections) handled.Add(section.Name);
            ReadFields(reader, node, def, UnitFields, name, handled, (field, value, change, at) => Add(name, s => field.Set(s, value), $"{name}: {change}", reader, at, "", field.Name));

            foreach (Section section in Sections)
            {
                JsonNode sectionNode = node.Field(section.Name);
                if (sectionNode == null) continue;
                string where = $"{name}, {section.Name}";
                if (!section.Present(def))
                {
                    reader.Error(sectionNode, $"{name} has no '{section.Name}' in this version.");
                    continue;
                }
                if (sectionNode.Kind != JsonKind.Object)
                {
                    reader.Error(sectionNode, $"{where}: must be an object {{ }}.");
                    continue;
                }
                bool hasSpawns = section.Name == "deathBurst" && def.DeathBurst.Spawns != null && def.DeathBurst.Spawns.Length > 0;
                var skip = new HashSet<string>(StringComparer.Ordinal);
                if (hasSpawns) skip.Add(SpawnBodiesKey);
                ReadFields(reader, sectionNode, def, section.Fields, where, skip, (field, value, change, at) => Add(name, s => field.Set(s, value), $"{where}: {change}", reader, at, section.Name, field.Name));
                if (hasSpawns) ReadSpawnBodies(reader, sectionNode.Field(SpawnBodiesKey), def, name);
            }

            ReadList(reader, node.Field("weapons"), def.Weapons, "weapons", "weapon", name, WeaponFields, w => w.Name,
                (index, field, value) => s => { if (s.Weapons != null && index < s.Weapons.Length && s.Weapons[index] != null) field.Set(s.Weapons[index], value); });
            ReadList(reader, node.Field("techs"), def.TechUpgrades, "techs", "tech", name, TechFields, t => t.Name,
                (index, field, value) => s => { if (s.TechUpgrades != null && index < s.TechUpgrades.Length && s.TechUpgrades[index] != null) field.Set(s.TechUpgrades[index], value); });
        }

        private void ReadSpawnBodies(Reader reader, JsonNode node, UnitStats def, string unit)
        {
            if (node == null) return;
            SpawnEntry[] spawns = def.DeathBurst.Spawns;
            string where = $"{unit}, deathBurst";
            if (node.Kind != JsonKind.Array || node.Items.Count != spawns.Length)
            {
                reader.Error(node, $"{where}: '{SpawnBodiesKey}' must be a list of {spawns.Length} whole number(s), one per unit it releases, in order.");
                return;
            }
            for (int i = 0; i < spawns.Length; i++)
            {
                JsonNode item = node.Items[i];
                int index = i;
                int old = spawns[i]?.Bodies ?? 0;
                if (!TryInt(item, out long value, out string why))
                {
                    reader.Error(item, $"{where}: '{SpawnBodiesKey}' entry {i + 1} {why}");
                    continue;
                }
                if (value == old) continue;
                if (value < 1 || value > 100)
                {
                    reader.Error(item, $"{where}: '{SpawnBodiesKey}' entry {i + 1} must be from 1 to 100.");
                    continue;
                }
                int bodies = (int)value;
                Add(unit, s => { if (s.DeathBurst?.Spawns != null && index < s.DeathBurst.Spawns.Length && s.DeathBurst.Spawns[index] != null) s.DeathBurst.Spawns[index].Bodies = bodies; },
                    $"{where}: {SpawnBodiesKey}[{i + 1}] {old} -> {bodies}", reader, item, "deathBurst", SpawnBodiesKey);
            }
        }

        private void ReadList<T>(Reader reader, JsonNode node, T[] defaults, string key, string what, string unit, Field<T>[] fields, Func<T, string> nameOf,
            Func<int, Field<T>, object, Action<UnitStats>> setter) where T : class
        {
            if (node == null) return;
            int count = defaults?.Length ?? 0;
            if (node.Kind != JsonKind.Array)
            {
                reader.Error(node, $"{unit}: '{key}' must be a list [ ].");
                return;
            }
            if (node.Items.Count != count)
            {
                reader.Error(node, $"{unit}: '{key}' must list its {count} {what}(s) of this version, in order (it lists {node.Items.Count}).");
                return;
            }
            for (int i = 0; i < count; i++)
            {
                JsonNode item = node.Items[i];
                T def = defaults[i];
                if (def == null)
                {
                    if (item.Kind != JsonKind.Null) reader.Error(item, $"{unit}: {what} {i + 1} is empty in this version and must stay null.");
                    continue;
                }
                string defName = nameOf(def) ?? "";
                string where = $"{unit}, {what} {i + 1} ({defName})";
                if (item.Kind != JsonKind.Object)
                {
                    reader.Error(item, $"{where}: must be an object {{ }}, not {JsonNode.KindName(item.Kind)}.");
                    continue;
                }
                JsonNode nameNode = item.Field("name");
                if (nameNode == null || nameNode.Kind != JsonKind.String || nameNode.Str != defName)
                {
                    reader.Error(nameNode ?? item, $"{unit}: {what} {i + 1} must be named \"{defName}\" (the list keeps this version's order; names cannot be changed).");
                    continue;
                }
                int index = i;
                string part = what == "tech" ? "tech:" + defName : what + ":" + i.ToString(CultureInfo.InvariantCulture);
                ReadFields(reader, item, def, fields, where, new HashSet<string>(StringComparer.Ordinal) { "name" },
                    (field, value, change, at) => Add(unit, setter(index, field, value), $"{where}: {change}", reader, at, part, field.Name));
            }
        }

        private static void ReadFields<T>(Reader reader, JsonNode obj, T def, Field<T>[] fields, string where, HashSet<string> handled, Action<Field<T>, object, string, JsonNode> changed)
        {
            foreach (var pair in obj.Fields)
            {
                string key = pair.Key;
                JsonNode value = pair.Value;
                if (key.StartsWith("_", StringComparison.Ordinal) || handled.Contains(key)) continue;
                Field<T> field = null;
                foreach (Field<T> candidate in fields)
                {
                    if (candidate.Name == key) { field = candidate; break; }
                }
                if (field == null)
                {
                    reader.Error(value, $"{where}: unknown field '{key}' (names are case-sensitive; see the list this version writes).");
                    continue;
                }
                if (!field.IsPresent(def))
                {
                    reader.Error(value, $"{where}: '{key}' is not used by this one in this version, so it cannot be set here.");
                    continue;
                }
                object old = field.Get(def);
                object parsed;
                switch (field.Kind)
                {
                    case Kind.Int:
                        if (!TryInt(value, out long whole, out string why)) { reader.Error(value, $"{where}: '{key}' {why}"); continue; }
                        if (whole == (int)old) continue;
                        if (whole < field.Min || whole > field.Max) { reader.Error(value, $"{where}: '{key}' must be from {Text(field.Min)} to {Text(field.Max)}."); continue; }
                        parsed = (int)whole;
                        break;
                    case Kind.Float:
                        if (value.Kind != JsonKind.Number) { reader.Error(value, $"{where}: '{key}' must be a number, not {JsonNode.KindName(value.Kind)}."); continue; }
                        if (!double.TryParse(value.Raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(number) > float.MaxValue)
                        {
                            reader.Error(value, $"{where}: '{key}' is too large a number.");
                            continue;
                        }
                        float oldFloat = (float)old;
                        if (Math.Abs(number - oldFloat) <= FloatTolerance * Math.Max(1.0, Math.Abs(oldFloat))) continue;
                        bool belowMin = field.MinExclusive ? number <= field.Min : number < field.Min;
                        if (belowMin || number > field.Max)
                        {
                            reader.Error(value, $"{where}: '{key}' must be {(field.MinExclusive ? "above " + Text(field.Min) : "from " + Text(field.Min))} {(field.MinExclusive ? "and at most" : "to")} {Text(field.Max)}.");
                            continue;
                        }
                        parsed = (float)number;
                        break;
                    case Kind.Bool:
                        if (value.Kind != JsonKind.Bool) { reader.Error(value, $"{where}: '{key}' must be true or false, not {JsonNode.KindName(value.Kind)}."); continue; }
                        if (value.Bool == (bool)old) continue;
                        parsed = value.Bool;
                        break;
                    default:
                        string[] names = Enum.GetNames(field.EnumType);
                        int found = -1;
                        if (value.Kind == JsonKind.String)
                        {
                            for (int n = 0; n < names.Length; n++)
                            {
                                if (string.Equals(names[n], value.Str.Trim(), StringComparison.OrdinalIgnoreCase)) { found = n; break; }
                            }
                        }
                        if (found < 0) { reader.Error(value, $"{where}: '{key}' must be one of \"{string.Join("\", \"", names)}\"."); continue; }
                        int chosen = (int)Enum.Parse(field.EnumType, names[found]);
                        if (chosen == (int)old) continue;
                        parsed = chosen;
                        break;
                }
                changed(field, parsed, $"{key} {ValueText(field, old)} -> {ValueText(field, parsed)}", value);
            }
        }

        private static bool TryInt(JsonNode value, out long whole, out string why)
        {
            whole = 0;
            if (value.Kind != JsonKind.Number)
            {
                why = $"must be a whole number, not {JsonNode.KindName(value.Kind)}.";
                return false;
            }
            string raw = value.Raw;
            for (int i = 0; i < raw.Length; i++)
            {
                if (!(raw[i] >= '0' && raw[i] <= '9') && !(i == 0 && raw[i] == '-'))
                {
                    why = "must be a whole number (no '.' or 'e').";
                    return false;
                }
            }
            if (raw.Length > 12 || !long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out whole))
            {
                why = "is too large a number.";
                return false;
            }
            why = null;
            return true;
        }

        private static string Text(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
