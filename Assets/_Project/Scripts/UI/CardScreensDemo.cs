// CardScreensDemo.cs
// Test tool for built games: takes a screenshot of every card screen, so that they can be judged from pictures (M10).
// It does nothing unless the game is started with "-cardsDemo <screenshot folder>", e.g.
//   Halobellum.exe -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -cardsDemo C:\temp\shots
// At each of several window sizes it goes through: the starting draft with 2, 3, 4 and 5 cards (with the long sub-faction
// names of the final game), the 5 wordiest real sub-factions with their own texts and exclusive units, and for each faction a general offer of each kind of card, a reinforcement offer, and the shop
// with the squad window. Then the sandbox's unit list at 16:9, 4:3 and 5:4. It logs how many labels could not fit at any size for each screen
// (CardScreens.TextThatDidNotFit), which screens have text that only fits at the smallest size (CardScreens.TextAtTheFloor), and a summary line at the end. To reach those screens it builds offers and sets the match's phase
// directly: this never happens in a real match. Looks only: nothing here runs without the flag.
// With "-cardsDemoLayout" as well it takes only the pictures for the narrowest windows and largest UI scales (without saving any of them): the deployment bar and shop, the Sandbox with its
// squad window at its tallest next to the Items panel, and its result line; and for each faction a close look at the selection ring on that faction's ground.
// With "-cardsDemoLevelAll" as well it takes only the pictures of the Level all button: for each faction 3 squads of a type with full XP bars, with enough credits, too few, with the selected squad
// not ready, and after pressing it (through the view's action, as a click does), and logs "Level all: ..." checks of the prices, the levels and that the buttons fit, at every UI scale.
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
        private const string LayoutFlag = "-cardsDemoLayout";
        private const string LevelAllFlag = "-cardsDemoLevelAll";

        private static readonly (int w, int h, float ui)[] LayoutSizes =
        {
            (1280, 1024, 1.25f), (1280, 1024, 1.1f), (1024, 768, 1.25f), (1024, 768, 1.1f), (1600, 900, 1.25f), (1920, 1080, 1f),
        };

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

            if (Array.IndexOf(Environment.GetCommandLineArgs(), LevelAllFlag) >= 0)
            {
                yield return LevelAllPictures();
                Application.Quit();
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), LayoutFlag) >= 0)
            {
                yield return LayoutPictures();
                Application.Quit();
                yield break;
            }

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
            foreach ((int w, int h) in new[] { (1024, 768), (1280, 1024) })
            {
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot($"{w}x{h}_sandbox_list");
            }
            // Unity remembers the last window size for the next run.
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(0.5f);

            Debug.Log($"Card demo finished: {_totalDidNotFit} screens have text that did not fit, {_totalAtTheFloor} have text only at the smallest size.\n" + string.Join("\n", _problems));
            Application.Quit();
        }

        /// <summary>The pictures of "-cardsDemoLevelAll".</summary>
        private IEnumerator LevelAllPictures()
        {
            int problems = 0;
            void Check(string what, bool ok)
            {
                Debug.Log($"Level all: {(ok ? "ok" : "PROBLEM")}: {what}");
                if (!ok) problems++;
            }

            var stats = FindFirstObjectByType<SquadStatsWindow>();
            BattleController battle = _match.GetComponent<BattleController>();
            SquadSelection selection = _match.GetComponent<SquadSelection>();
            int SimSquadOf(PlacedSquad squad) => battle.Simulation.Squads[_match.Match.Player.Squads.IndexOf(squad)].Id; // Simulation squad i is placed squad i, in deployment.
            void CheckRow(string where)
            {
                bool showing = stats.TryGetWindow(out Rect window);
                Rect up = stats.LevelUpShown, all = stats.LevelAllShown, line = stats.LevelLineShown;
                bool upShown = up.width > 0f;
                float tall = HudScale.ScaledHeight(HudScale.ScaleFor(Screen.width, Screen.height, GameSettings.UiScale));
                Check($"{where}: the window is showing, its top {window.y:0} of {tall:0} units", showing && window.y >= 0f);
                Check($"{where}: Level all is inside the window", all.width > 0f && all.xMin >= window.xMin && all.xMax <= window.xMax && all.yMax <= window.yMax);
                Check($"{where}: no overlap (line to {line.xMax:0}, up {up.xMin:0} to {up.xMax:0}, all {all.xMin:0} to {all.xMax:0}, own row {stats.LevelButtonsOwnRow})",
                    stats.LevelButtonsOwnRow ? line.yMax <= all.y + 2f : (!upShown || (line.xMax <= up.xMin && up.xMax <= all.xMin)) && line.xMax <= all.xMin);
            }

            foreach (string faction in new[] { "UNSC", "Covenant", "Flood" })
            {
                Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
                GameSettings.UseUiScaleForThisRun(1f);
                _match.NewMatch(1);
                SetDraft(5);
                _match.ChooseDraft(_match.Match.PlayerDraft.FindIndex(c => c.Faction.Name == faction));
                yield return new WaitForSecondsRealtime(1f);
                Match m = _match.Match;
                UnitStats type = m.Player.Squads[0].Type;
                m.Player.Credits = 5000;
                while (m.Player.Squads.Count(s => s.Type == type) < 3)
                {
                    m.Player.SquadsBoughtThisRound = 0;
                    if (!_match.Buy(type).Ok) break;
                }
                while (_match.IsDropping) yield return null;
                List<PlacedSquad> same = m.Player.Squads.Where(s => s.Type == type).ToList();
                Check($"{faction}: 3 squads of {type.Name}", same.Count == 3);
                UnitStats other = m.Player.Squads.FirstOrDefault(s => s.Type != type)?.Type;
                int price = 3 * m.LevelUpCost(same[0]);

                // The window is already up and showing no such button when the bars fill: nothing but its 5 refreshes a second can bring the button.
                selection.Select(SimSquadOf(same[0]));
                yield return new WaitForSecondsRealtime(0.3f);
                Check($"{faction}: no button before the bars are full", stats.LevelAllShown.width <= 0f);
                foreach (PlacedSquad squad in m.Player.Squads) squad.Xp = m.XpNeeded(squad);
                float shownAfter = Time.realtimeSinceStartup;
                while (stats.LevelAllShown.width <= 0f && Time.realtimeSinceStartup - shownAfter < 2f) yield return null;
                Check($"{faction}: the button appears {Time.realtimeSinceStartup - shownAfter:0.00} s after the bars are full", stats.LevelAllShown.width > 0f);
                SquadProgressView view = _match.DescribeProgress(SimSquadOf(same[0]));
                Check($"{faction}: it says {view.LevelAllLabel}, the price of 3 at {m.LevelUpCost(same[0])}", view.LevelAllLabel == $"Level all 3 ({price})" && view.LevelAllEnabled);
                yield return Shot($"{faction}_levelall_enough");
                foreach (float ui in new[] { 0.75f, 1f, 1.25f })
                {
                    foreach ((int w, int h) in new[] { (1600, 900), (1024, 768), (1280, 1024), (800, 500) })
                    {
                        Screen.SetResolution(w, h, FullScreenMode.Windowed);
                        GameSettings.UseUiScaleForThisRun(ui);
                        yield return new WaitForSecondsRealtime(0.5f);
                        CheckRow($"{faction} {w}x{h} ui {ui * 100f:0}%");
                        if (faction == "UNSC" || (w == 1024 && ui > 1f)) yield return Shot($"{faction}_levelall_{w}x{h}_ui{ui * 100f:0}");
                    }
                }
                Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
                GameSettings.UseUiScaleForThisRun(1f);
                yield return new WaitForSecondsRealtime(0.5f);

                m.Player.Credits = price - 1;
                yield return new WaitForSecondsRealtime(0.3f);
                Check($"{faction}: it is greyed out with {m.Player.Credits} credits", !_match.DescribeProgress(SimSquadOf(same[0])).LevelAllEnabled);
                yield return Shot($"{faction}_levelall_too_few_credits");

                m.Player.Credits = price + 40;
                same[0].Xp = 0f;
                yield return new WaitForSecondsRealtime(0.3f);
                view = _match.DescribeProgress(SimSquadOf(same[0]));
                Check($"{faction}: from a squad that isn't ready, it says {view.LevelAllLabel}", view.LevelAllLabel == $"Level all 2 ({2 * m.LevelUpCost(same[0])})" && view.LevelAllEnabled && !view.LevelUpEnabled);
                yield return Shot($"{faction}_levelall_selected_not_ready");

                same[0].Xp = m.XpNeeded(same[0]);
                PlacedSquad otherSquad = other != null ? m.Player.Squads.First(s => s.Type == other) : null;
                int otherLevel = otherSquad?.Level ?? 0;
                yield return new WaitForSecondsRealtime(0.3f);
                string heard = null;
                void Listen(string action, ActionResult result) => heard = action;
                _match.ActionTaken += Listen;
                _match.DescribeProgress(SimSquadOf(same[0])).OnLevelAll();
                _match.ActionTaken -= Listen;
                Check($"{faction}: all 3 are level 2, {m.Player.Credits} credits left: {_match.Message}", same.All(s => s.Level == 2 && s.Xp == 0f && s.LevelUpSpent == m.LevelUpCost(s)) && m.Player.Credits == 40);
                Check($"{faction}: the action path was {heard}", heard == nameof(MatchController.LevelUpAll));
                if (otherSquad != null) Check($"{faction}: {other.Name} is untouched", otherSquad.Level == otherLevel);
                yield return null;
                Check($"{faction}: the same squad is still selected", _match.SelectedPlacedSquad == same[0]);
                yield return new WaitForSecondsRealtime(0.05f);
                Check($"{faction}: the button has gone at once", stats.LevelAllShown.width <= 0f);
                yield return Shot($"{faction}_levelall_after");
            }
            Debug.Log($"Level all finished: {problems} problems.");
        }

        /// <summary>The pictures of "-cardsDemoLayout".</summary>
        private IEnumerator LayoutPictures()
        {
            foreach (string faction in new[] { "UNSC", "Covenant", "Flood" })
            {
                Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
                _match.NewMatch(1);
                SetDraft(5);
                _match.ChooseDraft(_match.Match.PlayerDraft.FindIndex(c => c.Faction.Name == faction));
                yield return new WaitForSecondsRealtime(1f);
                BattleController field = _match.GetComponent<BattleController>();
                SquadSelection chosen = _match.GetComponent<SquadSelection>();
                chosen.Select(field.Simulation.Squads[0].Id);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot(faction + "_ring_field");
                Camera view = Camera.main;
                var control = view.GetComponent<BattleCameraController>();
                control.enabled = false;
                Vector3 body = _match.GetComponent<UnitViewManager>().WorldPosition(chosen.FirstAliveOfSelected());
                view.transform.position = body + new Vector3(0f, 16f, -10f);
                view.transform.LookAt(body);
                yield return Shot(faction + "_ring_close");
                control.enabled = true;
            }

            foreach ((int w, int h, float ui) in LayoutSizes)
            {
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                GameSettings.UseUiScaleForThisRun(ui);
                yield return new WaitForSecondsRealtime(0.8f);
                _match.NewMatch(1);
                _match.ChooseDraft(0);
                yield return Shot($"{w}x{h}_ui{ui * 100f:0}_deployment");
            }

            SceneManager.LoadScene("Sandbox");
            yield return new WaitForSecondsRealtime(1.5f);
            var sandbox = FindFirstObjectByType<SandboxController>();
            var battle = sandbox.GetComponent<BattleController>();
            var selection = sandbox.GetComponent<SquadSelection>();
            UnitStats wordiest = sandbox.Units.Select(u => u.Stats).OrderByDescending(u => u.TechUpgrades?.Length ?? 0).ThenBy(u => u.Cost).First();
            UnitStats enemy = sandbox.Units.Select(u => u.Stats).First();
            sandbox.AddSquad(Team.A, wordiest);
            sandbox.AddSquad(Team.B, enemy);
            yield return null;
            int squad = battle.Simulation.Squads[0].Id;
            foreach ((int w, int h, float ui) in LayoutSizes)
            {
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                GameSettings.UseUiScaleForThisRun(ui);
                selection.Select(-1);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot($"{w}x{h}_ui{ui * 100f:0}_sandbox");
                selection.Select(squad);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot($"{w}x{h}_ui{ui * 100f:0}_sandbox_squad");
            }
            selection.Select(-1);
            battle.Speed = 16f;
            sandbox.Run();
            float limit = Time.realtimeSinceStartup + 120f;
            while (!battle.Simulation.IsFinished && Time.realtimeSinceStartup < limit) yield return null;
            foreach ((int w, int h, float ui) in LayoutSizes)
            {
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                GameSettings.UseUiScaleForThisRun(ui);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot($"{w}x{h}_ui{ui * 100f:0}_sandbox_result");
            }
            Debug.Log("Card demo's layout pictures finished.");
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
