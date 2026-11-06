// Tutorial.cs
// The tutorial (D77): a scripted match that teaches the game's loop by doing it, with every way to see stats and both towers.
// TutorialSetup is how the match is fixed: a seed, 600 base health, one draft card for each side (UNSC Noble with Marines against
// Banished), the enemy's army round by round (TutorialAi), and the card offers of rounds 2 and 3. TutorialDirector walks through
// TutorialStep's in order on the scene's MatchController: each has a title, text, what it points at (a HUD button's key, an area of
// the HUD, the player's squads), and what moves it on (something done, or Next). Ready stays locked until a round's steps allow it.
// MatchHud draws the panel and the outline (TutorialHud); the director only keeps the state, so tests can drive it without a screen.
// Depends on: MatchController, SquadSelection, GameFlow, Halobellum.Economy, Halobellum.Core, UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class TutorialStep
    {
        public string Title;
        public string Text;
        /// <summary>What to outline: "key:Ready" (a HUD button by its key), "area:shop" (an area MatchHud names), "squad" (the player's squads), or null.</summary>
        public string Target;
        /// <summary>True once the step is done; null = the player presses Next.</summary>
        public Func<bool> Done;
        /// <summary>The battle may start during this step.</summary>
        public bool ReadyAllowed;
        /// <summary>Said under the text while the step waits for something to be done.</summary>
        public string Doing;
    }

    /// <summary>How the tutorial's match is fixed (D77).</summary>
    public static class TutorialSetup
    {
        public const int Seed = 4077;
        public const int BaseHealth = 600;
        public const string PlayerFaction = "UNSC", PlayerSub = "Noble", EnemyFaction = "Covenant", EnemySub = "Banished";
        public const string StarterUnit = "Marine", ShopUnit = "Flamethrower Trooper";

        public static MatchSetup Setup() => new MatchSetup { Mode = MatchMode.Faction, BaseHealth = BaseHealth, Cards = CardFilter.AllCards };

        /// <summary>The enemy's buys, round by round: a small Banished army that grows.</summary>
        public static TutorialAi Enemy() => new TutorialAi(new Dictionary<int, string[]>
        {
            [2] = new[] { "Jackal (Beam Rifle)" },
            [3] = new[] { "Ghost", "Grunt" },
            [4] = new[] { "Grunt", "Jackal (Shield)" },
            [5] = new[] { "Brute Grenadier" },
        });

        /// <summary>One draft card for each side, in place of the drawn ones: the same start for everyone.</summary>
        public static void FixDrafts(Match match, IReadOnlyList<FactionInfo> factions)
        {
            FactionInfo player = FactionNamed(factions, PlayerFaction), enemy = FactionNamed(factions, EnemyFaction);
            DraftCard mine = player != null ? Build(player, PlayerSub, StarterUnit, StarterUnit) : null;
            DraftCard theirs = enemy != null ? Build(enemy, EnemySub, "Grunt", "Jackal (Shield)") : null;
            if (mine != null)
            {
                match.PlayerDraft.Clear();
                match.PlayerDraft.Add(mine);
            }
            if (theirs != null)
            {
                match.EnemyDraft.Clear();
                match.EnemyDraft.Add(theirs);
            }
        }

        /// <summary>A draft card for a faction of the match by name (null if the match has no such faction or sub-faction).</summary>
        public static DraftCard Build(FactionInfo faction, string sub, params string[] squads)
        {
            SubFactionInfo subFaction = faction.SubFactions.Find(s => s.Name == sub);
            if (subFaction == null) return null;
            var card = new DraftCard { Faction = faction, SubFaction = subFaction };
            List<UnitStats> roster = faction.RosterFor(subFaction);
            foreach (string name in squads)
            {
                UnitStats unit = roster.Find(u => u.Name == name);
                if (unit != null) card.Squads.Add(unit);
            }
            return card.Squads.Count == squads.Length ? card : null;
        }

        /// <summary>The offer the tutorial gives in this round: round 2 a spell, an item and training; round 3 an item first. Null: the drawn one stays.</summary>
        public static CardOffer OfferFor(PlayerState player, int round, CardOffer drawn)
        {
            if (player.Faction == null || drawn == null || drawn.Kind != CardKind.General) return null;
            List<CardStats> cards = player.Faction.Cards;
            CardStats spell = Cheapest(cards, c => c.Type == CardType.Spell && c.Spell != null && c.Spell.Offensive && c.Spell.Effect == SpellEffect.Damage);
            CardStats item = Cheapest(cards, c => c.Type == CardType.Item);
            CardStats training = Cheapest(cards, c => c.Type == CardType.Training);
            CardStats economy = Cheapest(cards, c => c.Type == CardType.Economy);
            var offer = new CardOffer { Kind = CardKind.General, Round = round, SkipBonus = drawn.SkipBonus };
            if (round == 2) Add(offer, spell, item, training);
            else if (round == 3) Add(offer, item, economy, training);
            else return null;
            return offer.Cards.Count == 3 ? offer : null;
        }

        private static void Add(CardOffer offer, params CardStats[] cards)
        {
            foreach (CardStats card in cards) if (card != null) offer.Cards.Add(card);
        }

        private static CardStats Cheapest(List<CardStats> cards, Predicate<CardStats> fits)
        {
            CardStats best = null;
            foreach (CardStats card in cards) if (card != null && fits(card) && (best == null || card.Cost < best.Cost)) best = card;
            return best;
        }

        /// <summary>The faction of this name among the match's, or null.</summary>
        public static FactionInfo FactionNamed(IEnumerable<FactionInfo> factions, string name)
        {
            if (factions == null) return null;
            foreach (FactionInfo faction in factions) if (faction != null && faction.Name == name) return faction;
            return null;
        }
    }

    public sealed class TutorialDirector : MonoBehaviour
    {
        private readonly List<TutorialStep> _steps = new List<TutorialStep>();
        private readonly HashSet<string> _done = new HashSet<string>();
        private readonly HashSet<string> _signals = new HashSet<string>();
        private readonly HashSet<int> _offersSet = new HashSet<int>();
        private MatchController _match;
        private int _round = -1;

        public int Index { get; private set; }
        public int Count => _steps.Count;
        public TutorialStep Current => Index < _steps.Count ? _steps[Index] : null;
        public bool Finished => Index >= _steps.Count;
        /// <summary>True if the battle may start now.</summary>
        public bool ReadyAllowed => Finished || (Current?.ReadyAllowed ?? true);

        public void Begin(MatchController match)
        {
            _match = match;
            _match.ActionTaken += OnAction;
            _steps.Clear();
            _steps.AddRange(TutorialScript.Build(this, match));
            Index = 0;
        }

        private void OnDestroy()
        {
            if (_match != null) _match.ActionTaken -= OnAction;
        }

        private void OnAction(string action, ActionResult result)
        {
            if (result.Ok) _done.Add(action);
        }

        /// <summary>The HUD says something happened that the match doesn't know of: a unit's stats opened from the shop, Battle stats opened.</summary>
        public void Signal(string what) => _signals.Add(what);

        /// <summary>True if this action worked since the current step began.</summary>
        public bool Did(string action) => _done.Contains(action);

        public bool Saw(string signal) => _signals.Contains(signal);

        /// <summary>The Next button of a step that only explains.</summary>
        public void Next()
        {
            if (Current != null && Current.Done == null) Advance();
        }

        /// <summary>Leaves the tutorial for the main menu.</summary>
        public void Skip() => GameFlow.OpenMainMenu();

        private void Advance()
        {
            Index++;
            _done.Clear();
            _signals.Clear();
        }

        private void Update()
        {
            if (_match == null || _match.Match == null) return;
            Match m = _match.Match;
            // The round's card offer, once it has been drawn: the one the lessons talk about.
            if (m.Phase == MatchPhase.Cards && !_offersSet.Contains(m.Round))
            {
                _offersSet.Add(m.Round);
                CardOffer offer = TutorialSetup.OfferFor(m.Player, m.Round, m.Player.Offer);
                if (offer != null) m.Player.Offer = offer;
            }
            if (m.Round != _round) _round = m.Round;
            // Several steps can be done at once (a player ahead of the text): move through them all.
            for (int guard = 0; guard < 4 && Current?.Done != null && Current.Done(); guard++) Advance();
        }
    }
}
