// MatchAudio.cs
// The sound of a match: which music plays on which screen (a random song for each round's cards and deployment, another for its battle), which UI sound goes with what the
// player does (buy, sell, unlock, a card, a spell, a level, a move, a mistake), and what sounds when a round or the whole game ends (a round drawn has its own), with their own sounds against Flood.
// Added at run time to the object with the MatchController (see AudioDirector). In Unlock Skirmish no side has a faction: the UI sounds are the neutral ones, or UNSC's where there is no neutral one.
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
            _match.EndingHit += OnEndingHit;
            if (_match.Match != null) OnPhaseChanged();
        }

        private void OnDestroy()
        {
            if (_match == null) return;
            _match.PhaseChanged -= OnPhaseChanged;
            _match.ActionTaken -= OnAction;
            _match.EndingHit -= OnEndingHit;
        }

        private void OnPhaseChanged()
        {
            AudioDirector director = AudioDirector.Instance;
            if (director == null || _match.Match == null) return;
            Match match = _match.Match;
            MatchPhase phase = match.Phase;
            if (phase == MatchPhase.Draft && _lastPhase != MatchPhase.Draft) director.Music.ForgetPositions();
            if (phase != _lastPhase && (_lastPhase == MatchPhase.MatchOver || _lastPhase == MatchPhase.Result)) director.Music.StopStinger(); // Its screen was left.
            director.UiFaction = UiFactionFor(match.Rules.Mode, phase, match.Player.Faction?.Name);

            switch (phase)
            {
                case MatchPhase.Draft:
                    director.Music.Play(AudioSlots.MainMenuMusic);
                    break;
                case MatchPhase.Cards:
                case MatchPhase.Deployment:
                    director.Music.Play(AudioSlots.DeploymentMusic); // Chosen when the cards appear, and it goes on through the deployment.
                    break;
                case MatchPhase.Battle:
                    if (_lastPhase != MatchPhase.Battle) director.Music.Play(AudioSlots.CombatSong(FloodOn(match, Team.A) || FloodOn(match, Team.B), director.NextRandom()), 1.5f);
                    break;
                default:
                    director.Music.Stop(); // The end of a round or a game has its own sound.
                    break;
            }

            if (phase != _lastPhase)
            {
                if (phase == MatchPhase.Battle) GameAudio.Ui(UiSound.BattleStart);
                else if (phase == MatchPhase.Result) RoundEnded(director, match);
                else if (phase == MatchPhase.MatchOver) GameEnded(director, match);
            }
            _lastPhase = phase;
        }

        /// <summary>Whose song the end of a lost game is, by faction. In Unlock Skirmish nobody has one: the land's goes for it.</summary>
        public static string MusicFaction(MatchMode mode, string playerFaction) => mode == MatchMode.UnlockSkirmish ? BattleFactions.TeamA : playerFaction;

        /// <summary>Whose UI sounds play: none yet at the draft, and never anyone's in Unlock Skirmish, where the player's "faction" is only the name of the card list.</summary>
        public static string UiFactionFor(MatchMode mode, MatchPhase phase, string playerFaction)
        {
            if (phase == MatchPhase.Draft || mode == MatchMode.UnlockSkirmish) return null;
            return playerFaction ?? BattleFactions.TeamA;
        }

        /// <summary>Flood is on this side: by its faction, or by the units in the battle where a side has no faction.</summary>
        private static bool FloodOn(Match match, Team team)
        {
            PlayerState player = team == Team.A ? match.Player : match.Enemy;
            if (match.Rules.Mode != MatchMode.UnlockSkirmish && AudioSlots.FactionKey(player.Faction?.Name) == "flood") return true;
            return AudioSlots.FloodIn(match.CurrentBattle, team);
        }

        private static void RoundEnded(AudioDirector director, Match match)
        {
            RoundSummary summary = match.LastSummary;
            if (summary == null) return;
            Team? winner = summary.RoundWinner;
            if (winner == null) director.Music.PlayStinger(AudioSlots.RoundDraw); // Neither side won the round.
            else if (winner == Team.A) director.Music.PlayStinger(AudioSlots.RoundVictory);
            else director.Music.PlayStinger(AudioSlots.RoundDefeatSound(FloodOn(match, Team.B), director.NextRandom(), AudioLibrary.Get(AudioSlots.RoundDefeat).Clips.Length));
        }

        private static void GameEnded(AudioDirector director, Match match)
        {
            string faction = MusicFaction(match.Rules.Mode, match.Player.Faction?.Name);
            director.Music.PlayStinger(AudioSlots.GameOverSong(match.Winner == Team.A, FloodOn(match, Team.B), faction));
        }

        /// <summary>A node of light hit a base: the owner's small or big sound, by its size (AudioCatalog.NodeHit).</summary>
        /// <summary>A node of light hitting a base: the music dips under it for a moment, deeper for a big one (the owner, 2026-11-06).</summary>
        private static void OnEndingHit(RoundEnding.Node node)
        {
            SfxCue cue = AudioCatalog.NodeHit(node.Size, node.IsTitan);
            GameAudio.Sfx(cue);
            GameAudio.DipMusic(cue == AudioCatalog.NodeBig ? 1.4f : 0.9f, cue == AudioCatalog.NodeBig ? 0.3f : 0.45f);
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
