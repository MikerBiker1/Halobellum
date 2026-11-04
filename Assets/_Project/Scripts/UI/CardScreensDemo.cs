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
// squad window at its tallest next to the Items panel, and its result line; and for each faction a close look at the selection ring on that faction's ground. Also Unlock Skirmish's bar, unlock
// page and hover stats at 1080p and at UI scale 1.25, and "Skirmish HUD: ..." checks of its buttons and of a real Esc sent through the Input System. Nothing goes outside the game.
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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Halobellum.UI
{
    public sealed class CardScreensDemo : MonoBehaviour
    {
        public const string Flag = "-cardsDemo";
        private const string LayoutFlag = "-cardsDemoLayout";
        private const string LevelAllFlag = "-cardsDemoLevelAll";
        private const string PlaytestFlag = "-cardsDemoPlaytest";

        private static readonly (int w, int h, float ui)[] LayoutSizes =
        {
            (1280, 1024, 1.25f), (1280, 1024, 1.1f), (1024, 768, 1.25f), (1024, 768, 1.1f), (1600, 900, 1.25f), (1920, 1080, 1f),
        };

        private static readonly (int w, int h, float ui)[] SkirmishSizes =
        {
            (1920, 1080, 1f), (1920, 1080, 1.25f), (1280, 1024, 1.25f),
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
        private MatchHud _hud;
        private int _rightClickProbe = -1, _probedControl, _skirmishProblems;

        private void Awake() => useGUILayout = false;

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

            if (Array.IndexOf(Environment.GetCommandLineArgs(), PlaytestFlag) >= 0)
            {
                yield return PlaytestPictures();
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

        /// <summary>
        /// The pictures of "-cardsDemoPlaytest", of the owner's notes from playing (D58): real draws, the enemy's starting squads in grey, the
        /// Command Center's window, a unit from the shop, and an item with a level-up arrow on the same squad.
        /// </summary>
        private IEnumerator PlaytestPictures()
        {
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            GameSettings.UseUiScaleForThisRun(1f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return FlankPictures();
            yield return PelicanPictures();
            foreach (int seed in new[] { 1, 2, 3 })
            {
                _match.NewMatch(seed);
                foreach (DraftCard card in _match.Match.PlayerDraft)
                    Debug.Log($"Playtest draw {seed}: {card.Squads.Sum(s => s.Cost)} {card}");
                yield return Shot($"draft_seed{seed}");
            }

            BattleController battle = _match.GetComponent<BattleController>();
            SquadSelection selection = _match.GetComponent<SquadSelection>();
            SquadStatsWindow stats = FindFirstObjectByType<SquadStatsWindow>();
            foreach (string faction in new[] { "UNSC", "Covenant", "Flood" })
            {
                _match.NewMatch(1);
                SetDraft(5);
                Match m = _match.Match;
                _match.ChooseDraft(m.PlayerDraft.FindIndex(c => c.Faction.Name == faction));
                yield return new WaitForSecondsRealtime(1f);
                Debug.Log($"Playtest {faction}: the enemy's grey squads: {string.Join(", ", m.SeenArmy(Team.B).Select(s => $"{s.Type.Name} at {s.Column},{s.Row}"))}. It really has: {string.Join(", ", m.Enemy.Squads.Select(s => $"{s.Type.Name} at {s.Column},{s.Row}"))}");
                yield return Shot(faction + "_round1_enemy_in_grey");

                selection.SelectTower(Team.A, TowerKind.CommandCenter);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(faction + "_command_center");
                selection.SelectTower(Team.A, TowerKind.ResearchCenter);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(faction + "_research_center");
                selection.ClearTower();

                stats.ShopUnit = m.Player.Roster.OrderByDescending(u => u.TechUpgrades?.Length ?? 0).ThenByDescending(u => u.Cost).First();
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(faction + "_shop_unit");
                stats.ShopUnit = null;

                PlacedSquad squad = m.Player.Squads[0];
                CardStats item = m.Player.Faction.Cards.FirstOrDefault(c => c.Type == CardType.Item);
                squad.Item = item?.Item;
                squad.Xp = m.XpNeeded(squad);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(faction + "_item_and_arrow");
                Camera view = Camera.main;
                var control = view.GetComponent<BattleCameraController>();
                control.enabled = false;
                Vector3 body = _match.GetComponent<UnitViewManager>().WorldPosition(battle.Simulation.Units[0]);
                view.transform.position = body + new Vector3(0f, 22f, -14f);
                view.transform.LookAt(body);
                yield return Shot(faction + "_item_and_arrow_close");
                control.enabled = true;
            }
        }

        /// <summary>The flank zones during deployment with a Pelican in one, and the same Pelican arriving 10 seconds into the battle.</summary>
        private IEnumerator FlankPictures()
        {
            _match.NewMatch(1);
            SetDraft(5);
            Match m = _match.Match;
            _match.ChooseDraft(m.PlayerDraft.FindIndex(c => c.Faction.Name == "UNSC"));
            yield return new WaitForSecondsRealtime(0.5f);
            m.Player.Credits = 5000;
            UnitStats pelican = m.Player.Roster.First(u => u.Name == "Pelican");
            _match.Unlock(pelican);
            _match.Buy(pelican);
            while (_match.IsDropping) yield return null;
            PlacedSquad squad = m.Player.Squads[m.Player.Squads.Count - 1];
            _match.BeginMoving(squad);
            ActionResult placed = _match.PlaceAt(-5, -20);
            Debug.Log($"Playtest flank: placed in a flank zone: {placed.Ok} {placed.Message}");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("Flank_deployment");
            Camera view = Camera.main;
            var control = view.GetComponent<BattleCameraController>();
            control.enabled = false;
            view.transform.position = new Vector3(-88f, 70f, -10f);
            view.transform.LookAt(new Vector3(-70f, 0f, 40f));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("Flank_deployment_close");
            _match.Ready();
            yield return null;
            BattleController battle = _match.GetComponent<BattleController>();
            battle.Speed = 2f;
            SimUnit body = battle.Simulation.Squads[m.Player.Squads.IndexOf(squad)].Members[0];
            Debug.Log($"Playtest flank: waiting {body.NotArrived} at {body.Position.X:0},{body.Position.Y:0}");
            yield return Shot("Flank_battle_waiting");
            float deadline = Time.realtimeSinceStartup + 30f;
            while (body.NotArrived && Time.realtimeSinceStartup < deadline) yield return null;
            Debug.Log($"Playtest flank: arrived at {battle.Simulation.ElapsedSeconds:0.0} s, at {body.Position.X:0},{body.Position.Y:0}");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("Flank_battle_arrived");
            control.enabled = true;
            battle.Speed = 1f;
        }

        /// <summary>A Pelican with its Troop Bay in a real battle, coming down and with its Marines out, and the Flood's swarm of forms.</summary>
        private IEnumerator PelicanPictures()
        {
            _match.NewMatch(1);
            SetDraft(5);
            Match m = _match.Match;
            _match.ChooseDraft(m.PlayerDraft.FindIndex(c => c.Faction.Name == "UNSC"));
            yield return new WaitForSecondsRealtime(0.5f);
            m.Player.Credits = 5000;
            UnitStats pelican = m.Player.Roster.First(u => u.Name == "Pelican");
            _match.Unlock(pelican);
            _match.Buy(pelican);
            _match.BuyTech(pelican, pelican.TechUpgrades.First(t => t.UnlocksReinforcement));
            while (_match.IsDropping) yield return null;
            // Enough of an enemy for the battle to last for all 3 drops.
            m.Enemy.Credits = 5000;
            UnitStats tough = m.Enemy.Roster.Where(u => u.TargetFilter == TargetFilter.Ground && u.Tier < 4).OrderByDescending(u => u.MaxHealth * u.SquadSize).First();
            m.Enemy.UnlocksThisRound = 0;
            m.Unlock(Team.B, tough);
            for (int i = 0; i < 2; i++)
            {
                m.Enemy.SquadsBoughtThisRound = 0;
                m.Buy(Team.B, tough);
            }
            _match.Ready();
            yield return null;
            BattleController battle = _match.GetComponent<BattleController>();
            battle.Speed = 2f;
            float deadline = Time.realtimeSinceStartup + 40f;
            SimUnit carrier = null;
            Camera view = Camera.main;
            var control = view.GetComponent<BattleCameraController>();
            control.enabled = false;
            int shots = 0;
            while (Time.realtimeSinceStartup < deadline && shots < 4)
            {
                yield return null;
                if (battle.Simulation == null) continue;
                carrier = battle.Simulation.Units.FirstOrDefault(u => u.Stats.Name == "Pelican" && u.Team == Team.A);
                if (carrier == null) continue;
                Vector3 body = _match.GetComponent<UnitViewManager>().WorldPosition(carrier);
                view.transform.position = new Vector3(body.x, 34f, body.z) + new Vector3(0f, 0f, -26f);
                view.transform.LookAt(new Vector3(body.x, 2f, body.z));
                bool down = carrier.DropPhase != DropRunPhase.Flying;
                if (down && ((shots == 0 && carrier.DropPhase == DropRunPhase.Descending && carrier.Altitude < 0.5f) || (shots == 1 && carrier.DropPhase == DropRunPhase.Ascending && carrier.Altitude < 0.25f)
                    || (shots == 2 && carrier.DropsMade == 2 && carrier.DropPhase == DropRunPhase.Ascending) || (shots == 3 && carrier.DropsMade == 3 && carrier.DropPhase == DropRunPhase.Ascending)))
                {
                    Debug.Log($"Playtest Pelican: picture {shots} at {battle.Simulation.ElapsedSeconds:0.0} s, drops made {carrier.DropsMade}, at {carrier.Position.X:0},{carrier.Position.Y:0}, facing {carrier.Facing.X:0.0},{carrier.Facing.Y:0.0}, altitude {carrier.Altitude:0.00}, Marines at {string.Join(" ", battle.Simulation.Units.Where(u => u.Stats.Name == "Marine" && u.Team == Team.A && u.IsAlive).Take(4).Select(u => $"{u.Position.X:0},{u.Position.Y:0}"))}, enemy's rearmost at {battle.Simulation.Units.Where(u => u.Team == Team.B && u.IsAlive && !u.IsAir && !u.Stats.IsTower).Select(u => u.Position.Y).DefaultIfEmpty(0f).Max():0}");
                    yield return Shot($"Pelican_{shots}");
                    shots++;
                }
            }
            Debug.Log($"Playtest Pelican: at the end, {carrier?.DropsMade} drops made, alive {carrier?.IsAlive}, at {battle.Simulation?.ElapsedSeconds:0.0} s");
            control.enabled = true;
            battle.Speed = 1f;
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

            yield return SkirmishHudButtons();
            yield return SkirmishHudPictures();

            GameSettings.UseUiScaleForThisRun(1f);
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

        private void SkirmishCheck(string what, bool ok)
        {
            Debug.Log($"Skirmish HUD: {(ok ? "ok" : "PROBLEM")}: {what}");
            if (!ok) _skirmishProblems++;
        }

        /// <summary>Unlock Skirmish's bar, unlock page and hover stats.</summary>
        private IEnumerator SkirmishHudPictures()
        {
            SquadStatsWindow stats = FindFirstObjectByType<SquadStatsWindow>();
            var setup = new MatchSetup { Mode = MatchMode.UnlockSkirmish };
            void Check(string what, bool ok) => SkirmishCheck(what, ok);

            string tallestName = null;
            foreach ((int w, int h, float ui) in SkirmishSizes)
            {
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                GameSettings.UseUiScaleForThisRun(ui);
                yield return new WaitForSecondsRealtime(0.8f);
                string prefix = $"{w}x{h}_ui{ui * 100f:0}_skirmish_";
                float hudWidth = HudScale.ScaledWidth(HudScale.ScaleFor(Screen.width, Screen.height, ui));
                float hudHeight = HudScale.ScaledHeight(HudScale.ScaleFor(Screen.width, Screen.height, ui));
                _match.NewMatch(1, setup);
                yield return Shot(prefix + "draft");
                _match.ChooseDraft(0);
                yield return new WaitForSecondsRealtime(0.5f);
                PlayerState player = _match.Match.Player;
                player.Credits = 5000;
                yield return Shot(prefix + "bar_start");
                Check($"{prefix} the bar is on the screen, at {_hud.UnitBarShown}", _hud.UnitBarShown.xMax <= hudWidth && _hud.UnitBarShown.yMax <= hudHeight);

                var sorted = new List<UnitStats>();
                MatchHud.FillShopRows(player.Roster, sorted);
                if (tallestName == null)
                {
                    // The tallest window of them all, measured rather than guessed.
                    var heights = new List<(float height, UnitStats unit)>();
                    foreach (UnitStats unit in sorted)
                    {
                        player.Unlocked.Add(unit);
                        _hud.DemoHover = unit;
                        for (int frame = 0; frame < 3 && (stats.HoverUnit != unit || !stats.TryGetWindow(out Rect _)); frame++) yield return null;
                        yield return null;
                        stats.TryGetWindow(out Rect window);
                        heights.Add((window.height, unit));
                    }
                    _hud.DemoHover = null;
                    heights.Sort((a, b) => b.height.CompareTo(a.height));
                    tallestName = heights[0].unit.Name;
                    Debug.Log("Skirmish HUD: the tallest hover windows: " + string.Join(", ", heights.Take(5).Select(p => $"{p.unit.Name} {p.height:0}")) + $"; the shortest {heights[heights.Count - 1].unit.Name} {heights[heights.Count - 1].height:0}");
                    yield return Shot(prefix + "bar_every_unit");
                }

                UnitStats tallest = sorted.First(u => u.Name == tallestName); // Each match has its own.
                int columns = MatchHud.UnitBarColumns(hudWidth);
                player.Unlocked.Clear();
                player.Unlocked.Add(tallest);
                foreach (UnitStats unit in sorted.Take(3 * columns - 1)) player.Unlocked.Add(unit);
                yield return Shot(prefix + "bar_3_rows");

                UnitStats shortOne = sorted[0];
                _hud.DemoHover = shortOne;
                yield return Shot(prefix + "bar_hover");
                stats.TryGetWindow(out Rect hoverWindow);
                Check($"{prefix} hovering {shortOne.Name}: window at {hoverWindow}", hoverWindow.height > 0f && !hoverWindow.Overlaps(_hud.UnitBarShown) && hoverWindow.y >= 0f);
                _hud.DemoHover = tallest;
                yield return Shot(prefix + "bar_hover_tallest");
                stats.TryGetWindow(out hoverWindow);
                Check($"{prefix} hovering {tallest.Name}: window at {hoverWindow}, not over the bar, top on the screen", !hoverWindow.Overlaps(_hud.UnitBarShown) && hoverWindow.y >= 0f && hoverWindow.yMax <= hudHeight);

                // Spells on the left, for the hover window to keep clear of or sit above.
                foreach (CardStats card in player.Faction.Cards.Where(c => c.Type == CardType.Spell).Take(4))
                    player.Spells.Add(new OwnedSpell { Id = 1000 + player.Spells.Count, Stats = card.Spell, ReadyRound = 1 });
                yield return Shot(prefix + "bar_hover_tallest_with_spells");
                _hud.DemoHover = null;

                _hud.OpenUnlockPage();
                yield return Shot(prefix + "page");
                _hud.DemoHover = tallest;
                yield return Shot(prefix + "page_hover");
                stats.TryGetWindow(out hoverWindow);
                Check($"{prefix} hovering {tallest.Name} on the page: window at {hoverWindow}, beside the page", hoverWindow.xMax <= _hud.UnlockPageShown.x && hoverWindow.y >= 0f);
                _hud.DemoHover = null;
                player.Credits = 120;
                yield return Shot(prefix + "page_few_credits");
                _hud.CloseUnlockPage();
                yield return Shot(prefix + "bar_few_credits");
            }
            Debug.Log($"Skirmish HUD finished: {_skirmishProblems} problems.");
        }

        /// <summary>The buttons and keys of Unlock Skirmish's bar and page, pressed by the test tool's way (MenuStyle) and a real Esc through the Input System.</summary>
        private IEnumerator SkirmishHudButtons()
        {
            _hud = FindFirstObjectByType<MatchHud>();
            SquadStatsWindow stats = FindFirstObjectByType<SquadStatsWindow>();
            void Check(string what, bool ok) => SkirmishCheck(what, ok);

            // A window started from a script may not have the focus, and without it no key arrives. This is the game's own setting, only in memory.
            InputSettings.BackgroundBehavior keptBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Debug.Log($"Skirmish HUD: the window has the focus: {Application.isFocused}");

            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            GameSettings.UseUiScaleForThisRun(1f);
            yield return new WaitForSecondsRealtime(0.8f);

            // What GUI.Button does with a right-click, and what the buttons' guard makes of it.
            _rightClickProbe = 0;
            while (_rightClickProbe >= 0) yield return null;

            _match.NewMatch(1, new MatchSetup { Mode = MatchMode.UnlockSkirmish });
            _match.ChooseDraft(0);
            yield return new WaitForSecondsRealtime(0.5f);
            PlayerState me = _match.Match.Player;
            me.Credits = 5000;

            yield return PressOnHud(MatchHud.UnlockUnitsLabel);
            Check("the Unlock units button opens the page", _hud.IsUnlockPageOpen);

            UnitStats locked = me.Roster.First(u => !me.Unlocked.Contains(u) && u.UnlockCost > 0);
            int credits = me.Credits;
            yield return PressOnHud(MatchHud.PageCellKey(locked));
            Check($"a cell on the page unlocks {locked.Name} for {locked.UnlockCost}, and closes the page", me.Unlocked.Contains(locked) && me.Credits == credits - locked.UnlockCost && !_hud.IsUnlockPageOpen);
            yield return Shot("skirmish_bar_after_unlock");

            yield return PressOnHud(MatchHud.UnlockUnitsLabel);
            UnitStats second = me.Roster.First(u => !me.Unlocked.Contains(u) && u.UnlockCost > 0);
            yield return PressOnHud(MatchHud.PageCellKey(second));
            Check($"a second unlock in the round is refused, and the page stays: {_match.Message}", !me.Unlocked.Contains(second) && _hud.IsUnlockPageOpen);
            yield return Shot("skirmish_page_second_unlock_refused");
            yield return PressOnHud(MatchHud.UnlockPageCloseKey);
            Check("its x closes the page", !_hud.IsUnlockPageOpen);

            var rows = new List<UnitStats>();
            MatchHud.FillUnitBarRows(me.Roster, me.Unlocked, rows);
            UnitStats toBuy = rows[0], other = rows[rows.Count - 1];
            int squads = me.Squads.Count;
            yield return PressOnHud(MatchHud.BarCellKey(toBuy));
            Check($"a cell in the bar buys a squad of {toBuy.Name}", me.Squads.Count == squads + 1);

            // It lands with the page open: it selects itself, and the page clears that.
            yield return PressOnHud(MatchHud.UnlockUnitsLabel);
            Check("the page opens while a squad is dropping", _hud.IsUnlockPageOpen && _match.IsDropping);
            SquadSelection selection = _match.GetComponent<SquadSelection>();
            while (_match.IsDropping) yield return null;
            yield return null;
            yield return null;
            Check("a squad that lands behind the page is not left selected", selection.SelectedSquadId < 0);

            yield return PressEscape();
            Check("Esc closes the page, and doesn't pause", !_hud.IsUnlockPageOpen && !GamePause.IsPaused);
            yield return PressEscape();
            Check("the next Esc pauses", GamePause.IsPaused);
            yield return PressEscape();
            Check("and the next one resumes", !GamePause.IsPaused);

            _hud.OpenUnlockPage();
            yield return null;
            PauseMenu.Current.Open();
            yield return null;
            yield return Shot("skirmish_page_behind_pause_menu");
            yield return PressEscape();
            Check("with the page behind the pause menu, Esc only closes the menu", !GamePause.IsPaused && _hud.IsUnlockPageOpen);
            yield return PressEscape();
            Check("and the next Esc closes the page", !GamePause.IsPaused && !_hud.IsUnlockPageOpen);
            if (GamePause.IsPaused) PauseMenu.Current.Close();
            _hud.CloseUnlockPage();
            InputSystem.settings.backgroundBehavior = keptBehavior;

            // A squad selected in the field, with a unit hovered in the bar: the window is the hovered unit's, with nothing of that squad.
            yield return PressOnHud(MatchHud.BarCellKey(toBuy));
            while (_match.IsDropping) yield return null;
            yield return new WaitForSecondsRealtime(0.3f);
            Check("a squad that lands with the page shut is selected", selection.SelectedSquadId >= 0);
            yield return Shot("skirmish_bar_squad_selected");
            _hud.DemoHover = other;
            yield return Shot("skirmish_bar_hover_squad_selected");
            Check($"hovering {other.Name} with a squad selected shows no level-up button of that squad", stats.HoverUnit == other && stats.LevelUpShown.width <= 0f && stats.LevelAllShown.width <= 0f);
            _hud.DemoHover = null;
        }

        /// <summary>Presses a button of the HUD by its key, down the same path as a mouse click (MenuStyle).</summary>
        private IEnumerator PressOnHud(string key)
        {
            MenuStyle.PressedByTest = key;
            for (int frame = 0; frame < 30 && MenuStyle.PressedByTest != null; frame++) yield return null;
            if (MenuStyle.PressedByTest != null) SkirmishCheck($"{key} was never drawn", false);
            MenuStyle.PressedByTest = null;
            yield return null;
            yield return null;
        }

        /// <summary>A press and release of the right button, then the left, on a GUI.Button of this script's own, as IMGUI's own events (nothing goes outside the game).</summary>
        private void OnGUI()
        {
            if (_rightClickProbe < 0 || Event.current.type != EventType.Repaint) return;
            int button = _rightClickProbe < 2 ? 1 : 0;
            bool release = _rightClickProbe % 2 == 1;
            var rect = new Rect(0f, 0f, 40f, 40f);
            Event real = Event.current;
            if (release) GUIUtility.hotControl = _probedControl; // The press was a frame ago, and the real mouse isn't down.
            Event.current = new Event { type = release ? EventType.MouseUp : EventType.MouseDown, button = button, mousePosition = rect.center, clickCount = 1 };
            bool fired = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            bool guarded = fired && Event.current.button == 0;
            if (release)
            {
                Debug.Log($"Skirmish HUD: GUI.Button on mouse button {button}: it fired {fired}, and with the guard {guarded}");
                SkirmishCheck($"the guard takes mouse button {button} as a click: {guarded}", guarded == (button == 0) && fired);
            }
            else _probedControl = GUIUtility.hotControl;
            GUIUtility.hotControl = 0;
            Event.current = real;
            _rightClickProbe = _rightClickProbe == 3 ? -1 : _rightClickProbe + 1;
        }

        private static IEnumerator PressEscape()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) yield break;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
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
