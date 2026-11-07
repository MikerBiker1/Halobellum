// AudioSlots.cs
// The names of every sound and music slot in the game (M10), and which slot goes with what: a weapon kind, a faction, a
// spell, a screen. A slot is a name such as "music_combat". AudioLibrary finds the file behind a name: the owner's
// file in Art/Imported/Audio/Resources/AudioOverride if there is one, otherwise the generated one. The sound effects
// have more settings than a name, so they are listed in AudioCatalog. Sound only; nothing here touches the simulation.
// Depends on: Halobellum.Core (SpellStats), Halobellum.Combat (BattleSimulation).

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;

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
        /// <summary>An item put in a squad's hexagon (D91; it was the spell's sound).</summary>
        ItemPlaced,
    }

    public static class AudioSlots
    {
        public const string MainMenuMusic = "music_mainmenu";
        /// <summary>The scene that plays the main menu's music.</summary>
        public const string MainMenuScene = "MainMenu";

        /// <summary>Several songs each: one is picked at random. The cards and deployment of a round share one song.</summary>
        public const string DeploymentMusic = "music_deployment", CombatMusic = "music_combat";
        /// <summary>Joins the combat songs when Flood is on either side.</summary>
        public const string CombatFloodMusic = "music_combat_flood_present";
        /// <summary>How often the Flood's song plays when Flood is present. It is one song against several, and this is more than any of them gets.</summary>
        public const float CombatFloodShare = 0.4f;

        public const string RoundVictory = "stinger_match_victory", RoundDefeat = "stinger_match_defeat", RoundDefeatFlood = "stinger_match_defeat_flood_present";
        /// <summary>A round drawn: neither side won it. The owner's file only; without it, nothing plays.</summary>
        public const string RoundDraw = "stinger_match_draw";
        public const string GameVictory = "stinger_game_victory", GameDefeatFlood = "stinger_game_defeat_flood_present";

        public static readonly string[] FactionKeys = { "unsc", "covenant", "flood" };
        /// <summary>The UI set used before a side is chosen. It has only the sounds a menu needs; the rest fall back to UNSC.</summary>
        public const string NeutralKey = "neutral";

        private static readonly string[] UiEventNames = { "click", "hover", "buy", "sell", "unlock", "error", "cardpick", "skip", "spellplaced", "levelup", "battlestart", "roundwon", "roundlost", "matchwon", "matchlost", "itemplaced" };

        /// <summary>The short lower-case key of a faction's name. Anything unknown counts as UNSC, so a new faction still sounds of something.</summary>
        public static string FactionKey(string faction)
        {
            if (string.IsNullOrEmpty(faction)) return FactionKeys[0];
            if (faction.IndexOf("flood", StringComparison.OrdinalIgnoreCase) >= 0) return FactionKeys[2];
            if (faction.IndexOf("covenant", StringComparison.OrdinalIgnoreCase) >= 0) return FactionKeys[1];
            return FactionKeys[0];
        }

        /// <summary>The song of a battle. roll is any number from 0 up to 1.</summary>
        public static string CombatSong(bool floodPresent, float roll) => floodPresent && roll < CombatFloodShare ? CombatFloodMusic : CombatMusic;

        /// <summary>The sound of a lost round. Against Flood its own sound joins the others, with the same chance as each of them. roll is any number from 0 up to 1.</summary>
        public static string RoundDefeatSound(bool floodEnemy, float roll, int plainSounds) => floodEnemy && roll * (plainSounds + 1) < 1f ? RoundDefeatFlood : RoundDefeat;

        /// <summary>What plays when the whole game is over. Losing to Flood has its own song, whoever you played.</summary>
        public static string GameOverSong(bool won, bool floodEnemy, string playerFaction)
        {
            if (won) return GameVictory;
            return floodEnemy ? GameDefeatFlood : GameDefeat(playerFaction);
        }

        public static string GameDefeat(string faction) => $"stinger_game_defeat_{FactionKey(faction)}";

        /// <summary>True if Flood is in this battle: on one team, or on either if none is given.</summary>
        public static bool FloodIn(BattleSimulation battle, Team? team = null)
        {
            if (battle == null) return false;
            IReadOnlyList<SimUnit> units = battle.Units;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Stats.IsFlood && (team == null || units[i].Team == team)) return true;
            }
            return false;
        }

        /// <summary>The slot of a UI sound for a faction; a null faction means no side has been chosen yet.</summary>
        public static string Ui(string faction, UiSound sound)
        {
            if (faction == null && HasNeutral(sound)) return $"ui_{NeutralKey}_{UiEventNames[(int)sound]}";
            return $"ui_{FactionKey(faction)}_{UiEventNames[(int)sound]}";
        }

        public static bool HasNeutral(UiSound sound) => sound == UiSound.Click || sound == UiSound.Hover || sound == UiSound.Error || sound == UiSound.CardPick;

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
            if (spell.Lingers) return "sfx_spell_smoke";
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
            yield return DeploymentMusic;
            yield return CombatMusic;
            yield return CombatFloodMusic;
            yield return RoundVictory;
            yield return RoundDefeat;
            yield return RoundDefeatFlood;
            yield return RoundDraw;
            yield return GameVictory;
            yield return GameDefeatFlood;
            foreach (string faction in FactionKeys)
            {
                yield return GameDefeat(faction);
                foreach (UiSound sound in Enum.GetValues(typeof(UiSound))) yield return Ui(faction, sound);
            }
            foreach (UiSound sound in Enum.GetValues(typeof(UiSound)))
            {
                if (HasNeutral(sound)) yield return Ui(null, sound);
            }
        }
    }
}
