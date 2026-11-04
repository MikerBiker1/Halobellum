// BloodLooks.cs
// What a body bleeds: red for humans, light blue for grunts, deep blue for elites, orange for hunters and Goliaths, purple for jackals,
// dark red for brutes, and a sickly brown for anything of the Flood, infested vehicles and risen bodies among them. Machines and towers
// have none. Every other unit is named in the table below, so that a new one is noticed by the tests instead of quietly having no blood.
// Plain data with no Unity objects but Color, so it can be tested. Looks only.
// Depends on: Halobellum.Core, UnityEngine (Color).

using System.Collections.Generic;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public enum BloodKind
    {
        None,
        Red,
        LightBlue,
        DeepBlue,
        Orange,
        SicklyBrown,
        Purple,
        DarkRed,
    }

    public static class BloodLooks
    {
        private static readonly Dictionary<string, BloodKind> ByName = new Dictionary<string, BloodKind>
        {
            // Humans.
            { "Marine", BloodKind.Red }, { "Flamethrower Trooper", BloodKind.Red }, { "ODST", BloodKind.Red }, { "Placeholder Infantry", BloodKind.Red },
            { "Spartan", BloodKind.Red }, { "Spartan Sniper", BloodKind.Red }, { "Spartan Heavy Weapons", BloodKind.Red }, { "Spartan Grenadier", BloodKind.Red }, { "Spartan CQB", BloodKind.Red }, { "Spartan Fireteam", BloodKind.Red },
            // Grunts.
            { "Grunt", BloodKind.LightBlue }, { "Fuel Rod Grunt", BloodKind.LightBlue }, { "Cannon Fodder", BloodKind.LightBlue }, { "Heavy Grunts", BloodKind.LightBlue },
            // Elites.
            { "Elite", BloodKind.DeepBlue }, { "Zealot", BloodKind.DeepBlue },
            // Hunters: worm colonies, and so is what the Goliath wears.
            { "Hunter Pair", BloodKind.Orange }, { "Hunter Captain", BloodKind.Orange }, { "Goliath", BloodKind.Orange },
            // Jackals and brutes.
            { "Jackal (Plasma Rifle)", BloodKind.Purple }, { "Jackal (Shield)", BloodKind.Purple }, { "Brute Grenadier", BloodKind.DarkRed },

            // Machines.
            { "Warthog", BloodKind.None }, { "Gauss Warthog", BloodKind.None }, { "Scorpion", BloodKind.None }, { "Wolverine", BloodKind.None }, { "Hornet", BloodKind.None }, { "Archer Missile Pod", BloodKind.None },
            { "Pelican", BloodKind.None }, { "Mantis", BloodKind.None }, { "Elephant", BloodKind.None }, { "Gremlin", BloodKind.None }, { "Cobra", BloodKind.None }, { "Rhino", BloodKind.None }, { "Grizzly", BloodKind.None },
            { "Sparrowhawk", BloodKind.None }, { "Longsword", BloodKind.None }, { "Vulture", BloodKind.None }, { "Condor Gunship", BloodKind.None },
            { "Ghost", BloodKind.None }, { "Wraith", BloodKind.None }, { "Vampire", BloodKind.None }, { "Banshee", BloodKind.None }, { "Scarab", BloodKind.None },
            { "Chopper", BloodKind.None }, { "Marauder", BloodKind.None }, { "Reaver", BloodKind.None }, { "Skitterer", BloodKind.None }, { "Grunt Goblin", BloodKind.None },
            { "Placeholder Light Vehicle", BloodKind.None }, { "Placeholder Heavy Tank", BloodKind.None },
        };

        /// <summary>What this body bleeds. A tower goes first and the Flood next: risen bodies have names of their own, and a table can't know them all.</summary>
        public static BloodKind For(UnitStats stats)
        {
            if (stats == null || stats.IsTower) return BloodKind.None;
            if (stats.IsFlood || stats.InfectedFrom != null) return BloodKind.SicklyBrown;
            return ByName.TryGetValue(stats.Name ?? "", out BloodKind kind) ? kind : BloodKind.None;
        }

        /// <summary>True if the table has decided about this name, blood or none (for tests).</summary>
        public static bool IsListed(string name) => ByName.ContainsKey(name ?? "");

        public static Color ColorOf(BloodKind kind)
        {
            switch (kind)
            {
                case BloodKind.Red: return new Color(0.62f, 0.03f, 0.03f);
                case BloodKind.LightBlue: return new Color(0.42f, 0.78f, 1f);
                case BloodKind.DeepBlue: return new Color(0.04f, 0.1f, 0.72f);
                case BloodKind.Orange: return new Color(1f, 0.48f, 0.04f);
                case BloodKind.SicklyBrown: return new Color(0.44f, 0.33f, 0.1f);
                case BloodKind.Purple: return new Color(0.46f, 0.09f, 0.72f);
                case BloodKind.DarkRed: return new Color(0.3f, 0.015f, 0.025f);
                default: return Color.clear;
            }
        }

        /// <summary>
        /// The chance that a hit taking this share of a body's health draws blood. A hit that takes a third or more always does, and small ones
        /// rarely, so that a flamethrower's many small licks don't fill the field with it.
        /// </summary>
        public static float HitChance(float shareOfHealth) => Mathf.Clamp01(shareOfHealth * 3f);
    }
}
