// AudioSlots.cs
// The names of every sound and music slot in the game (M10), and which slot goes with what: a weapon kind, a faction, a
// spell, a screen. A slot is a name such as "music_unsc_battle". AudioLibrary finds the file behind a name: the owner's
// file in Art/Imported/Audio/Resources/AudioOverride if there is one, otherwise the generated one. The sound effects
// have more settings than a name, so they are listed in AudioCatalog. Sound only; nothing here touches the simulation.
// Depends on: Halobellum.Core (SpellStats), Halobellum.Economy (MatchPhase).

using System;
using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;

namespace Halobellum.Presentation
{
    /// <summary>What a UI sound means. Each faction has its own sound for each of these.</summary>
    public enum UiSound
    {
        Click,
        Hover,
        Buy,
        Sell,
        Unlock,
        Error,
        CardPick,
        Skip,
        SpellPlaced,
        LevelUp,
        BattleStart,
        RoundWon,
        RoundLost,
        MatchWon,
        MatchLost,
    }

    public enum MusicMood
    {
        Menu,
        Deployment,
        Battle,
    }

    public static class AudioSlots
    {
        public const string MainMenuMusic = "music_mainmenu";
        /// <summary>The scene that plays the main menu's music.</summary>
        public const string MainMenuScene = "MainMenu";

        public static readonly string[] FactionKeys = { "unsc", "covenant", "flood" };
        /// <summary>The UI set used before a side is chosen. It has only the sounds a menu needs; the rest fall back to UNSC.</summary>
        public const string NeutralKey = "neutral";

        private static readonly string[] UiEventNames = { "click", "hover", "buy", "sell", "unlock", "error", "cardpick", "skip", "spellplaced", "levelup", "battlestart", "roundwon", "roundlost", "matchwon", "matchlost" };
        private static readonly string[] MoodNames = { "menu", "deployment", "battle" };

        /// <summary>The short lower-case key of a faction's name. Anything unknown counts as UNSC, so a new faction still sounds of something.</summary>
        public static string FactionKey(string faction)
        {
            if (string.IsNullOrEmpty(faction)) return FactionKeys[0];
            if (faction.IndexOf("flood", StringComparison.OrdinalIgnoreCase) >= 0) return FactionKeys[2];
            if (faction.IndexOf("covenant", StringComparison.OrdinalIgnoreCase) >= 0) return FactionKeys[1];
            return FactionKeys[0];
        }

        public static string Music(string faction, MusicMood mood) => $"music_{FactionKey(faction)}_{MoodNames[(int)mood]}";

        public static string Stinger(string faction, bool victory) => $"stinger_{FactionKey(faction)}_{(victory ? "victory" : "defeat")}";

        /// <summary>The slot of a UI sound for a faction; a null faction means no side has been chosen yet.</summary>
        public static string Ui(string faction, UiSound sound)
        {
            if (faction == null && HasNeutral(sound)) return $"ui_{NeutralKey}_{UiEventNames[(int)sound]}";
            return $"ui_{FactionKey(faction)}_{UiEventNames[(int)sound]}";
        }

        public static bool HasNeutral(UiSound sound) => sound == UiSound.Click || sound == UiSound.Hover || sound == UiSound.Error || sound == UiSound.CardPick;

        /// <summary>
        /// The music of a match screen. There is no faction yet at the draft, so it keeps the main menu's music. The screens
        /// between battles have the faction's quiet menu track.
        /// </summary>
        public static string MusicForPhase(MatchPhase phase, string playerFaction)
        {
            switch (phase)
            {
                case MatchPhase.Draft: return MainMenuMusic;
                case MatchPhase.Deployment: return Music(playerFaction, MusicMood.Deployment);
                case MatchPhase.Battle: return Music(playerFaction, MusicMood.Battle);
                default: return Music(playerFaction, MusicMood.Menu);
            }
        }

        /// <summary>The slot of a spell's sound, from its name, or else from what it does; null if this effect adds no new sound. tick counts its effects from 1.</summary>
        public static string Spell(SpellStats spell, int tick, string casterFaction)
        {
            string name = (spell.Name ?? "").ToLowerInvariant();
            bool repeats = spell.Effect == SpellEffect.Damage && spell.Ticks > 1;
            if (name.Contains("mac strike")) return "sfx_spell_mac";
            if (name.Contains("nuke") || name.Contains("havok")) return "sfx_spell_nuke";
            if (name.Contains("glassing")) return tick <= 1 ? "sfx_spell_glassing" : null; // One long sound covers all its hits.
            if (name.Contains("missile")) return tick <= 1 ? "sfx_spell_missiles" : null;
            if (name.Contains("wrath")) return "sfx_spell_wrath";
            if (name.Contains("plasma")) return repeats ? "sfx_spell_storm_tick" : "sfx_spell_plasma";
            if (spell.Effect == SpellEffect.Smoke) return "sfx_spell_smoke";
            if (spell.Effect == SpellEffect.Disrupt) return "sfx_spell_emp";
            if (name.Contains("blight") || name.Contains("spore")) return "sfx_spell_spore";
            if (name.Contains("shield") || name.Contains("dome")) return "sfx_spell_shield";
            if (name.Contains("banshee")) return "sfx_spell_drop_air";
            switch (spell.Effect)
            {
                case SpellEffect.Buff: return "sfx_spell_buff_" + FactionKey(casterFaction);
                case SpellEffect.DropSquad: return FactionKey(casterFaction) == "flood" ? "sfx_spell_drop_flood" : "sfx_spell_drop_pods";
                default: return repeats ? "sfx_spell_storm_tick" : "sfx_spell_plasma";
            }
        }

        /// <summary>Every slot of music and UI, for the owner's list and for the tests. The effects are in AudioCatalog.Cues.</summary>
        public static IEnumerable<string> MusicAndUiSlots()
        {
            yield return MainMenuMusic;
            foreach (string faction in FactionKeys)
            {
                foreach (MusicMood mood in Enum.GetValues(typeof(MusicMood))) yield return Music(faction, mood);
                yield return Stinger(faction, true);
                yield return Stinger(faction, false);
                foreach (UiSound sound in Enum.GetValues(typeof(UiSound))) yield return Ui(faction, sound);
            }
            foreach (UiSound sound in Enum.GetValues(typeof(UiSound)))
            {
                if (HasNeutral(sound)) yield return Ui(null, sound);
            }
        }
    }
}
