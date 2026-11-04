// MatchAudio.cs
// The sound of a match: which music plays on which screen, which UI sound goes with what the player does (buy, sell, unlock, a card, a
// spell, a level, a move, a mistake) and with how a round or a match ends. Added at run time to the object with the MatchController (see AudioDirector).
// In Unlock Skirmish no side has a faction: the music is the land's, and the UI sounds are the neutral ones, or UNSC's where there is no neutral one.
// Depends on: AudioDirector, AudioSlots, MatchController, Halobellum.Economy (Match, RoundSummary), UnityEngine.

using Halobellum.Core;
using Halobellum.Economy;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class MatchAudio : MonoBehaviour
    {
        private MatchController _match;
        private MatchPhase _lastPhase = (MatchPhase)(-1);

        private void Awake()
        {
            _match = GetComponent<MatchController>();
            _match.PhaseChanged += OnPhaseChanged;
            _match.ActionTaken += OnAction;
            if (_match.Match != null) OnPhaseChanged();
        }

        private void OnDestroy()
        {
            if (_match == null) return;
            _match.PhaseChanged -= OnPhaseChanged;
            _match.ActionTaken -= OnAction;
        }

        private void OnPhaseChanged()
        {
            AudioDirector director = AudioDirector.Instance;
            if (director == null || _match.Match == null) return;
            Match match = _match.Match;
            MatchPhase phase = match.Phase;
            string faction = MusicFaction(match.Rules.Mode, match.Player.Faction?.Name);
            if (phase == MatchPhase.Draft && _lastPhase != MatchPhase.Draft) director.Music.ForgetPositions();
            if (phase != MatchPhase.MatchOver && _lastPhase == MatchPhase.MatchOver) director.Music.StopStinger(); // A new match, in the same scene.
            director.UiFaction = UiFactionFor(match.Rules.Mode, phase, match.Player.Faction?.Name);
            director.Music.Play(AudioSlots.MusicForPhase(phase, faction), phase == MatchPhase.Battle ? 1.5f : 2.5f);

            if (phase != _lastPhase)
            {
                if (phase == MatchPhase.Battle) GameAudio.Ui(UiSound.BattleStart);
                else if (phase == MatchPhase.Result) RoundEnded(match);
                else if (phase == MatchPhase.MatchOver) MatchEnded(director, match, faction);
            }
            _lastPhase = phase;
        }

        /// <summary>
        /// Whose music and stingers play. This asks the match, not BattleFactions.PerUnitLooks, which is still true from a Skirmish before this match
        /// until a card has been chosen.
        /// </summary>
        public static string MusicFaction(MatchMode mode, string playerFaction) => mode == MatchMode.UnlockSkirmish ? BattleFactions.TeamA : playerFaction;

        /// <summary>Whose UI sounds play: none yet at the draft, and never anyone's in Unlock Skirmish, where the player's "faction" is only the name of the card list.</summary>
        public static string UiFactionFor(MatchMode mode, MatchPhase phase, string playerFaction)
        {
            if (phase == MatchPhase.Draft || mode == MatchMode.UnlockSkirmish) return null;
            return playerFaction ?? BattleFactions.TeamA;
        }

        private static void RoundEnded(Match match)
        {
            RoundSummary summary = match.LastSummary;
            if (summary == null || summary.Tie || summary.BaseDamageByA == summary.BaseDamageByB) return;
            GameAudio.Ui(summary.BaseDamageByA > summary.BaseDamageByB ? UiSound.RoundWon : UiSound.RoundLost);
        }

        private static void MatchEnded(AudioDirector director, Match match, string faction)
        {
            bool won = match.Winner == Team.A;
            GameAudio.Ui(won ? UiSound.MatchWon : UiSound.MatchLost);
            director.Music.PlayStinger(AudioSlots.Stinger(faction, won), 0.55f);
        }

        /// <summary>What the player just did, by the name of the MatchController's method.</summary>
        private void OnAction(string action, ActionResult result)
        {
            if (!result.Ok)
            {
                GameAudio.Ui(UiSound.Error);
                return;
            }
            switch (action)
            {
                case nameof(MatchController.Buy):
                    GameAudio.Ui(UiSound.Buy);
                    GameAudio.Sfx(AudioCatalog.DropIn);
                    break;
                case nameof(MatchController.Sell): GameAudio.Ui(UiSound.Sell); break;
                case nameof(MatchController.Unlock):
                case nameof(MatchController.BuyExtraSlot):
                case nameof(MatchController.BuyOilZone):
                case nameof(MatchController.RapidResupply):
                case nameof(MatchController.BuyEliteRecruitment):
                case nameof(MatchController.BuyTech): GameAudio.Ui(UiSound.Unlock); break;
                case nameof(MatchController.LevelUp):
                case nameof(MatchController.LevelUpTower):
                case nameof(MatchController.BuyResearch):
                case nameof(MatchController.LevelUpAll):
                case nameof(MatchController.UseTraining): GameAudio.Ui(UiSound.LevelUp); break;
                case nameof(MatchController.ChooseCard):
                case nameof(MatchController.ChooseDraft): GameAudio.Ui(UiSound.CardPick); break;
                case nameof(MatchController.SkipCards): GameAudio.Ui(UiSound.Skip); break;
                case nameof(MatchController.PlaceSpellAt):
                case nameof(MatchController.PlaceItemOn): GameAudio.Ui(UiSound.SpellPlaced); break;
                case nameof(MatchController.PlaceAt): UiAudio.SquadMoved(); break;
                case nameof(MatchController.ToggleRotation): UiAudio.SquadTurned(); break;
                case nameof(MatchController.UnplaceSpell): UiAudio.SpellTakenBack(); break;
            }
        }
    }
}
