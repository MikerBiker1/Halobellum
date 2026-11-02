// CardScreensDemo.cs
// Test tool for built games: takes a screenshot of every card screen, so that they can be judged from pictures (M10).
// It does nothing unless the game is started with "-cardsDemo <screenshot folder>", e.g.
//   Halobellum.exe -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -cardsDemo C:\temp\shots
// At each of several window sizes it goes through: the starting draft with 2, 3, 4 and 5 cards (with the long sub-faction
// names of the final game), the 5 wordiest real sub-factions with their own texts and exclusive units, and for each faction a general offer of each kind of card, a reinforcement offer, and the shop
// with the squad window. Then the sandbox's unit list. It logs how many labels could not fit at any size for each screen
// (CardScreens.TextThatDidNotFit), which screens have text that only fits at the smallest size (CardScreens.TextAtTheFloor), and a summary line at the end. To reach those screens it builds offers and sets the match's phase
// directly: this never happens in a real match. Looks only: nothing here runs without the flag.
// Depends on: CardScreens, Halobellum.Presentation (MatchController, BattleController, SquadSelection), Halobellum.Economy, UnityEngine.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Halobellum.UI
{
    public sealed class CardScreensDemo : MonoBehaviour
    {
        public const string Flag = "-cardsDemo";

        private static readonly (int w, int h, bool full)[] Sizes =
        {
            (1600, 900, true), (1920, 1080, true), (1280, 720, false), (1024, 768, false), (2560, 1080, false), (1280, 1024, false), (800, 500, false),
        };

        private static readonly Dictionary<string, string[]> SubFactionNames = new Dictionary<string, string[]>
        {
            { "UNSC", new[] { "Noble", "Armored Division", "Aerospace Command" } },
            { "Covenant", new[] { "Banished", "Lekgolo Colony", "Unggoy Rebellion" } },
            { "Flood", new[] { "UNSC-based", "Covenant-based" } },
        };

        private string _folder;
        private MatchController _match;
        private int _totalDidNotFit, _totalAtTheFloor;
        private readonly List<string> _problems = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0 || index + 1 >= args.Length) return;
            var holder = new GameObject("Card screens demo");
            DontDestroyOnLoad(holder);
            holder.AddComponent<CardScreensDemo>()._folder = args[index + 1];
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(_folder);
            yield return new WaitForSecondsRealtime(1f);
            _match = FindFirstObjectByType<MatchController>();
            if (_match == null) { Debug.LogError("Card demo: no MatchController in this scene."); yield break; }
            while (_match.Match == null) yield return null;

            foreach ((int w, int h, bool full) in Sizes)
            {
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(0.8f);
                string prefix = $"{w}x{h}_";
                for (int count = full ? 2 : 5; count <= 5; count++)
                {
                    _match.NewMatch(1);
                    SetDraft(count);
                    yield return Shot(prefix + $"draft{count}");
                }
                // The real sub-factions with the most to say, as the game draws them.
                _match.NewMatch(1);
                SetWordiestDraft();
                yield return Shot(prefix + "draft5_exclusive");
                foreach (string faction in full ? new[] { "UNSC", "Covenant", "Flood" } : new[] { "Flood" })
                {
                    _match.NewMatch(1);
                    SetDraft(5);
                    Match m = _match.Match;
                    _match.ChooseDraft(m.PlayerDraft.FindIndex(c => c.Faction.Name == faction));
                    m.Player.Credits = 400;

                    ShowOffer(m, CardKind.General, Pick(m, c => c.Type == CardType.Spell && c.Spell.Offensive), Pick(m, c => c.Type == CardType.Spell && !c.Spell.Offensive), Pick(m, c => c.Type == CardType.Item));
                    yield return Shot(prefix + faction + "_offer_spell_spell_item");
                    if (full)
                    {
                        ShowOffer(m, CardKind.General, Pick(m, c => c.Type == CardType.Economy), Pick(m, c => c.Type == CardType.Buff), Pick(m, c => c.Type == CardType.Training));
                        yield return Shot(prefix + faction + "_offer_econ_buff_training");
                        // Every spell name, for the emblems: three at a time would be many pictures, so only the rest of the spells in one row.
                        CardStats[] others = m.Player.Faction.Cards.Where(c => c.Type == CardType.Spell).Take(5).ToArray();
                        ShowOffer(m, CardKind.General, others);
                        CardScreens.DemoMouse = new Vector2(HudScale.ScaledWidth(HudScale.Apply()) * 0.5f, HudScale.ScaledHeight(HudScale.Apply()) * 0.5f);
                        yield return Shot(prefix + faction + "_offer_spells_hover");
                        CardScreens.DemoMouse = null;
                    }

                    ShowReinforcements(m);
                    yield return Shot(prefix + faction + "_reinforcements");

                    if (full)
                    {
                        // The shop, and the squad window of a free squad.
                        SetPhase(m, MatchPhase.Deployment);
                        m.Player.Offer = null;
                        yield return new WaitForSecondsRealtime(0.3f);
                        BattleController battle = _match.GetComponent<BattleController>();
                        SquadSelection selection = _match.GetComponent<SquadSelection>();
                        if (battle.Simulation != null && battle.Simulation.Squads.Count > 0 && selection != null) selection.Select(battle.Simulation.Squads[0].Id);
                        yield return Shot(prefix + faction + "_shop_and_squad");
                    }
                }
            }

            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            SceneManager.LoadScene("Sandbox");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("1600x900_sandbox_list");

            Debug.Log($"Card demo finished: {_totalDidNotFit} screens have text that did not fit, {_totalAtTheFloor} have text only at the smallest size.\n" + string.Join("\n", _problems));
            Application.Quit();
        }

        private IEnumerator Shot(string name)
        {
            CardScreens.TextThatDidNotFit = 0;
            CardScreens.LastTextThatDidNotFit = null;
            CardScreens.TextAtTheFloor = 0;
            CardScreens.LastTextAtTheFloor = null;
            yield return new WaitForSecondsRealtime(0.35f);
            yield return new WaitForEndOfFrame();
            if (CardScreens.TextThatDidNotFit > 0)
            {
                _totalDidNotFit++;
                _problems.Add($"{name}: did not fit: {CardScreens.LastTextThatDidNotFit}");
            }
            if (CardScreens.TextAtTheFloor > 0)
            {
                _totalAtTheFloor++;
                _problems.Add($"{name}: only at the smallest size: {CardScreens.LastTextAtTheFloor}");
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"));
            yield return null;
            yield return null;
        }

        /// <summary>A draw of this many cards, one per faction first, with the final game's sub-faction names.</summary>
        private void SetDraft(int count)
        {
            Match m = _match.Match;
            var factions = m.PlayerDraft.Select(c => c.Faction).Distinct().ToList();
            var cards = new List<DraftCard>();
            for (int i = 0; i < count; i++)
            {
                FactionInfo faction = factions[i % factions.Count];
                int nth = i / factions.Count;
                string[] names = SubFactionNames.TryGetValue(faction.Name, out string[] list) ? list : new[] { "Standard" };
                var squads = faction.BaseRoster.Where(u => u.Tier == 1).OrderByDescending(u => u.Name.Length).Skip(nth).Take(2).ToList();
                var subFaction = new SubFactionInfo { Name = names[nth % names.Length] };
                cards.Add(new DraftCard { Faction = faction, SubFaction = subFaction, Squads = squads });
            }
            m.PlayerDraft.Clear();
            m.PlayerDraft.AddRange(cards);
        }

        /// <summary>A draw of the 5 real sub-factions with the most to say, with their own texts and exclusive units.</summary>
        private void SetWordiestDraft()
        {
            Match m = _match.Match;
            var wordiest = m.PlayerDraft.Select(c => c.Faction).Distinct()
                .SelectMany(f => f.SubFactions.Select(s => (faction: f, subFaction: s)))
                .OrderByDescending(p => p.subFaction.Description.Length + p.subFaction.ExclusiveUnits.Sum(u => u.Name.Length + 2))
                .Take(5).ToList();
            var cards = wordiest.Select(p => new DraftCard
            {
                Faction = p.faction,
                SubFaction = p.subFaction,
                Squads = p.subFaction.ExclusiveUnits.Concat(p.faction.BaseRoster).Where(u => u.Tier == 1).Take(2).ToList(),
            }).ToList();
            m.PlayerDraft.Clear();
            m.PlayerDraft.AddRange(cards);
        }

        /// <summary>A card of this kind, the one with the most to say.</summary>
        private static CardStats Pick(Match m, Func<CardStats, bool> kind) =>
            m.Player.Faction.Cards.Where(kind).OrderByDescending(c => c.EffectsText().Length + c.Description.Length).FirstOrDefault();

        private static void ShowOffer(Match m, CardKind kind, params CardStats[] cards)
        {
            var offer = new CardOffer { Kind = kind, Round = 3, SkipBonus = 50 };
            offer.Cards.AddRange(cards.Where(c => c != null));
            m.Player.Offer = offer;
            SetPhase(m, MatchPhase.Cards);
        }

        private static void ShowReinforcements(Match m)
        {
            var offer = new CardOffer { Kind = CardKind.Reinforcement, Round = 4, SkipBonus = 100 };
            List<UnitStats> roster = m.Player.Roster;
            UnitStats big = roster.OrderByDescending(u => u.Cost).FirstOrDefault();
            UnitStats longName = roster.OrderByDescending(u => u.Name.Length).FirstOrDefault();
            UnitStats small = roster.OrderBy(u => u.Cost).FirstOrDefault(u => !m.Player.Unlocked.Contains(u)) ?? roster[0];
            offer.Reinforcements.Add(ReinforcementCard.Make(longName, 2, 400));
            offer.Reinforcements.Add(ReinforcementCard.Make(small, 1, 300));
            offer.Reinforcements.Add(ReinforcementCard.Make(big, 3, 400));
            m.Player.Offer = offer;
            SetPhase(m, MatchPhase.Cards);
        }

        private static void SetPhase(Match m, MatchPhase phase)
        {
            typeof(Match).GetProperty(nameof(Match.Phase), BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod(true)?.Invoke(m, new object[] { phase });
        }
    }
}
