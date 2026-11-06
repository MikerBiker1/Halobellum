// TutorialDemo.cs
// Test tool for built games (D77): plays the tutorial by itself and takes a picture of every step, so its panel, text and outlines can
// be judged from pictures. It does nothing unless the game is started with "-tutorialDemo <screenshot folder>", e.g.
//   Halobellum.exe -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -tutorialDemo C:\temp\shots
// From the main menu it starts the tutorial, then for each step waits a moment, saves "NN Title.png", and does what the step asks
// through the match's own actions (as TutorialTests does). It logs "Tutorial demo: ..." lines, and quits at the end. Nothing goes
// outside the game: no input is sent to the PC.
// Depends on: Halobellum.Presentation (GameFlow, MatchController, TutorialDirector, BattleController), MatchHud, Halobellum.Economy.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using UnityEngine;

namespace Halobellum.UI
{
    public sealed class TutorialDemo : MonoBehaviour
    {
        public const string Flag = "-tutorialDemo";
        private string _folder;
        private MatchController _match;
        private BattleController _battle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0 || index + 1 >= args.Length) return;
            var holder = new GameObject("Tutorial demo");
            DontDestroyOnLoad(holder);
            holder.AddComponent<TutorialDemo>()._folder = args[index + 1];
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(_folder);
            yield return new WaitForSecondsRealtime(1.5f);
            GameFlow.StartTutorial();
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((_match = FindFirstObjectByType<MatchController>()) == null || _match.Tutorial == null)
            {
                if (Time.realtimeSinceStartup > deadline) { Debug.LogError("Tutorial demo: the tutorial did not start."); Quit(); yield break; }
                yield return null;
            }
            _battle = FindFirstObjectByType<BattleController>();
            TutorialDirector t = _match.Tutorial;
            int shots = 0, stuck = 0;
            while (!t.Finished && stuck < 3)
            {
                int index = t.Index;
                yield return new WaitForSecondsRealtime(0.8f);
                string name = $"{index + 1:00} {Clean(t.Current.Title)}.png";
                ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name));
                shots++;
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Do(t);
                deadline = Time.realtimeSinceStartup + 150f;
                while (t.Index == index && !t.Finished && Time.realtimeSinceStartup < deadline) yield return null;
                if (t.Index == index) { stuck++; Debug.LogWarning($"Tutorial demo: stuck at step {index + 1}, {t.Current?.Title}."); }
            }
            Debug.Log($"Tutorial demo: {shots} pictures, finished {t.Finished}, round {_match.Match.Round}, our base {_match.Match.Player.BaseHealth}, theirs {_match.Match.Enemy.BaseHealth}.");
            Quit();
        }

        private static string Clean(string title) => new string(title.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());

        private IEnumerator Do(TutorialDirector t)
        {
            Match m = _match.Match;
            PlayerState me = m.Player;
            switch (t.Current.Title)
            {
                case "Your starting card": _match.ChooseDraft(0); break;
                case "The shop: unlocking": _match.Unlock(me.Roster.First(u => u.Name == TutorialSetup.ShopUnit)); break;
                case "The shop: buying": _match.Buy(me.Roster.First(u => u.Name == TutorialSetup.ShopUnit)); break;
                case "Selecting a squad": _match.Selection.Select(_battle.Simulation.Squads[0].Id); break;
                case "Stats before buying": t.Signal("ShopUnit"); break;
                case "Moving a squad":
                    PlacedSquad squad = me.Squads[0];
                    for (int dx = -6; dx <= 6; dx += 2)
                    {
                        _match.BeginMoving(squad);
                        if (_match.PlaceAt(squad.Column + dx, squad.Row + 1).Ok) break;
                    }
                    break;
                case "Undo": _match.Undo(); break;
                case "The Command Center": _match.Selection.SelectTower(Team.A, TowerKind.CommandCenter); break;
                case "The Research Center": _match.Selection.SelectTower(Team.A, TowerKind.ResearchCenter); break;
                case "Ready": _battle.Speed = 1f; _match.Ready(); break;
                case "The battle":
                    yield return new WaitForSecondsRealtime(3f);
                    _battle.Speed = 16f;
                    break;
                case "The round's result": _battle.Speed = 1f; _match.Continue(); break;
                case "Cards":
                case "Items":
                    CardType wanted = t.Current.Title == "Cards" ? CardType.Spell : CardType.Item;
                    int pick = me.Offer?.Cards.FindIndex(c => c.Type == wanted) ?? -1;
                    if (pick >= 0) _match.ChooseCard(pick);
                    else _match.SkipCards();
                    break;
                case "Spells":
                    if (me.Spells.Count > 0) { _match.BeginPlacingSpell(me.Spells[0]); _match.PlaceSpellAt(new Vec2(0f, 30f)); }
                    break;
                case "Placing an item":
                    if (me.Inventory.Count > 0 && me.Squads.Count > 0) { _match.BeginPlacingItem(me.Inventory[0]); _match.PlaceItemOn(me.Squads[0]); }
                    break;
                case "Next battle":
                case "Finish them":
                    while (m.Phase != MatchPhase.MatchOver && t.Current != null && (t.Current.Title == "Next battle" || t.Current.Title == "Finish them"))
                    {
                        if (m.Phase == MatchPhase.Cards) _match.SkipCards();
                        if (m.Phase == MatchPhase.Deployment)
                        {
                            Buy(me);
                            _battle.Speed = 16f;
                            _match.Ready();
                        }
                        while (m.Phase == MatchPhase.Battle) yield return null;
                        if (m.Phase == MatchPhase.Result) { yield return new WaitForSecondsRealtime(0.2f); _match.Continue(); }
                        yield return null;
                        if (t.Current?.Title == "Next battle" && m.Round >= 3) break;
                    }
                    _battle.Speed = 1f;
                    break;
                case "Battle stats": FindFirstObjectByType<MatchHud>()?.OpenBattleStats(); break;
                default:
                    if (t.Current.Done == null) t.Next();
                    break;
            }
        }

        private void Buy(PlayerState me)
        {
            UnitStats scorpion = me.Roster.First(u => u.Name == "Scorpion");
            if (!me.Unlocked.Contains(scorpion) && me.Credits >= scorpion.UnlockCost + scorpion.Cost) _match.Unlock(scorpion);
            foreach (string name in new[] { "Scorpion", TutorialSetup.ShopUnit, "Marine", "Scorpion" })
            {
                UnitStats unit = me.Roster.First(u => u.Name == name);
                if (me.Unlocked.Contains(unit)) _match.Buy(unit);
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}
