// ContentFingerprint.cs
// A stable 64-bit fingerprint of the game content a match is played with (Milestone 11): the factions in order (rosters, sub-factions,
// every unit's stats with its weapons, techs and whatever units it brings, the cards with their spells and items), the match rules and the
// combat rules with the damage table. Two LAN machines compare it before a match: if one plays with other unit stats (its own unit stats
// files, another build), the match is refused. It walks every public instance field by reflection, sorted by name (properties are worked
// out from fields, so they are left out); a class already being walked (a cycle) is written as a mark instead. Floats go in as their bits,
// strings as UTF-8, through StableHasher, so the number is the same on every machine and in every run.
// Depends on: FactionInfo, MatchRules, StableHasher, Halobellum.Core (CombatRules, DamageTable, the enums).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public static class ContentFingerprint
    {
        private const int MaxDepth = 64;
        private static readonly Dictionary<Type, FieldInfo[]> FieldsByType = new Dictionary<Type, FieldInfo[]>();

        /// <summary>The fingerprint of these factions (in this order) with these rules.</summary>
        public static ulong Compute(IReadOnlyList<FactionInfo> factions, MatchRules rules, CombatRules combatRules)
        {
            var hash = new StableHasher().Text("Halobellum.Content.1");
            var walker = new Walker(hash);
            hash.Int(factions?.Count ?? -1);
            if (factions != null) foreach (FactionInfo faction in factions) walker.Value(faction, 0);
            walker.Value(rules, 0);
            walker.Value(combatRules, 0);
            return hash.Value;
        }

        /// <summary>The fingerprint of any one object (a unit, a card, the rules) the same way.</summary>
        public static ulong Of(object content)
        {
            var hash = new StableHasher().Text("Halobellum.Content.1");
            new Walker(hash).Value(content, 0);
            return hash.Value;
        }

        private static FieldInfo[] FieldsOf(Type type)
        {
            lock (FieldsByType)
            {
                if (FieldsByType.TryGetValue(type, out FieldInfo[] fields)) return fields;
                fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
                FieldsByType[type] = fields;
                return fields;
            }
        }

        private sealed class Walker
        {
            private readonly StableHasher _hash;
            // The classes being walked right now (for cycles), by reference.
            private readonly List<object> _stack;

            public Walker(StableHasher hash, List<object> stack = null)
            {
                _hash = hash;
                _stack = stack ?? new List<object>();
            }

            public void Value(object value, int depth)
            {
                if (value == null) { _hash.Byte(0); return; }
                if (depth > MaxDepth) { _hash.Byte(1); return; }
                switch (value)
                {
                    case string s: _hash.Byte(2).Text(s); return;
                    case bool b: _hash.Byte(3).Bool(b); return;
                    case float f: _hash.Byte(4).Float(f); return;
                    case double d: _hash.Byte(5).Double(d); return;
                    case Enum e: _hash.Byte(6).Long(Convert.ToInt64(e, System.Globalization.CultureInfo.InvariantCulture)); return;
                    case int i: _hash.Byte(7).Long(i); return;
                    case long l: _hash.Byte(7).Long(l); return;
                    case short sh: _hash.Byte(7).Long(sh); return;
                    case byte by: _hash.Byte(7).Long(by); return;
                    case uint ui: _hash.Byte(7).Long(ui); return;
                    case ulong ul: _hash.Byte(7).ULong(ul); return;
                    case char c: _hash.Byte(7).Long(c); return;
                    case Delegate _: _hash.Byte(8); return;
                    case DamageTable table: _hash.Byte(9); Table(table); return;
                }

                Type type = value.GetType();
                bool isClass = !type.IsValueType;
                if (isClass)
                {
                    foreach (object open in _stack)
                    {
                        if (ReferenceEquals(open, value)) { _hash.Byte(10).Text(type.Name); return; }
                    }
                    _stack.Add(value);
                }
                try
                {
                    if (value is IDictionary dictionary) Dictionary(dictionary, depth);
                    else if (value is IList list) List(list, depth);
                    else if (value is IEnumerable set) Set(set, depth);
                    else Object(value, type, depth);
                }
                finally
                {
                    if (isClass) _stack.RemoveAt(_stack.Count - 1);
                }
            }

            private void Object(object value, Type type, int depth)
            {
                _hash.Byte(11).Text(type.Name);
                FieldInfo[] fields = FieldsOf(type);
                _hash.Int(fields.Length);
                foreach (FieldInfo field in fields)
                {
                    _hash.Text(field.Name);
                    Value(field.GetValue(value), depth + 1);
                }
            }

            private void List(IList list, int depth)
            {
                _hash.Byte(12).Int(list.Count);
                foreach (object item in list) Value(item, depth + 1);
            }

            /// <summary>A collection with no order of its own (a HashSet): its items' own fingerprints, sorted.</summary>
            private void Set(IEnumerable items, int depth)
            {
                var hashes = new List<ulong>();
                foreach (object item in items) hashes.Add(Sub(item, depth));
                hashes.Sort();
                _hash.Byte(13).Int(hashes.Count);
                foreach (ulong h in hashes) _hash.ULong(h);
            }

            private void Dictionary(IDictionary dictionary, int depth)
            {
                var pairs = new List<(ulong key, ulong value)>();
                foreach (DictionaryEntry entry in dictionary) pairs.Add((Sub(entry.Key, depth), Sub(entry.Value, depth)));
                pairs.Sort((a, b) => a.key != b.key ? a.key.CompareTo(b.key) : a.value.CompareTo(b.value));
                _hash.Byte(14).Int(pairs.Count);
                foreach ((ulong key, ulong value) in pairs) _hash.ULong(key).ULong(value);
            }

            private ulong Sub(object item, int depth)
            {
                var hash = new StableHasher();
                new Walker(hash, _stack).Value(item, depth + 1);
                return hash.Value;
            }

            /// <summary>The damage table keeps its numbers private: read them through its getters, for every damage and armor type.</summary>
            private void Table(DamageTable table)
            {
                Array damageTypes = Enum.GetValues(typeof(DamageType)), armorTypes = Enum.GetValues(typeof(ArmorType));
                foreach (DamageType damage in damageTypes)
                {
                    foreach (ArmorType armor in armorTypes) _hash.Float(table.Get(damage, armor));
                    _hash.Float(table.GetShield(damage));
                }
                foreach (ArmorType armor in armorTypes) _hash.Float(table.GetAntiArmor(armor));
                _hash.Float(table.GetAntiArmorShield());
            }
        }
    }
}
