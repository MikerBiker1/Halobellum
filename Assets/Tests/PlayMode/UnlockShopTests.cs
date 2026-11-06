// UnlockShopTests.cs
// Unlock Skirmish's bar and unlock page in the real Match scene, through the plain methods the buttons call (a batch run draws no IMGUI): what the
// bar lists, what an unlock costs and that a second one in the round is refused, how the page closes, that Esc never reaches the pause menu on the
// same press, that nothing stays selected behind the page, and that the whole roster fits the page at 1080p. The buttons themselves are pressed by
// the test tool: CardScreensDemo, with -cardsDemoLayout.
// Depends on: Halobellum.UI, Halobellum.Presentation, Halobellum.Economy, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class UnlockShopTests
    {
        private MatchController _match;
        private MatchHud _hud;
        private SquadSelection _selection;

        [TearDown]
        public void LeaveNothingBehind()
        {
            RightClick.ReadForTests = null;
            GameFlow.TakeMatchSetup();
            BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant); // These are static.
        }

        /// <summary>The Match scene, in deployment of round 1.</summary>
        private IEnumerator StartMatch(MatchMode mode)
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MatchScene, LoadSceneMode.Single);
            yield return null;
            _match = Object.FindAnyObjectByType<MatchController>();
            _hud = Object.FindAnyObjectByType<MatchHud>();
            Assert.IsNotNull(_match, "The Match scene has no MatchController.");
            Assert.IsNotNull(_hud, "The Match scene has no MatchHud.");
            _selection = _match.GetComponent<SquadSelection>();
            _match.NewMatch(3, new MatchSetup { Mode = mode });
            Assert.IsTrue(_match.ChooseDraft(0).Ok, _match.Message);
            yield return null;
        }

        private IEnumerator Skirmish() => StartMatch(MatchMode.UnlockSkirmish);

        private UnitStats ALockedUnitWithAPrice()
        {
            PlayerState player = _match.Match.Player;
            UnitStats unit = player.Roster.Find(u => !player.Unlocked.Contains(u) && u.UnlockCost > 0);
            Assert.IsNotNull(unit);
            return unit;
        }

        [UnityTest]
        public IEnumerator TheBar_ListsOnlyTheUnlockedUnits_ByTierThenPrice()
        {
            yield return Skirmish();
            PlayerState player = _match.Match.Player;
            player.Unlocked.Clear();
            var sorted = new List<UnitStats>();
            MatchHud.FillShopRows(player.Roster, sorted);
            UnitStats last = sorted[sorted.Count - 1], middle = sorted[sorted.Count / 2], first = sorted[0];
            player.Unlocked.Add(last); // Added in the wrong order: the set must not decide what is shown.
            player.Unlocked.Add(first);
            player.Unlocked.Add(middle);

            var rows = new List<UnitStats>();
            MatchHud.FillUnitBarRows(player.Roster, player.Unlocked, rows);
            CollectionAssert.AreEqual(new[] { first, middle, last }, rows);
            Assert.AreEqual(70, player.Roster.Count, "The roster itself is untouched.");
        }

        [UnityTest]
        public IEnumerator UnlockingFromThePage_AddsToTheBar_CostsTheUnlockPrice_AndClosesThePage()
        {
            yield return Skirmish();
            PlayerState player = _match.Match.Player;
            player.Credits = 5000;
            UnitStats unit = ALockedUnitWithAPrice();
            _hud.OpenUnlockPage();
            Assert.IsTrue(_hud.IsUnlockPageOpen);

            Assert.IsTrue(_hud.UnlockFromPage(unit).Ok, _match.Message);
            Assert.AreEqual(5000 - unit.UnlockCost, player.Credits);
            Assert.IsFalse(_hud.IsUnlockPageOpen);
            var rows = new List<UnitStats>();
            MatchHud.FillUnitBarRows(player.Roster, player.Unlocked, rows);
            CollectionAssert.Contains(rows, unit);
        }

        [UnityTest]
        public IEnumerator ASecondUnlockInTheRound_IsRefused_AndThePageStays()
        {
            yield return Skirmish();
            PlayerState player = _match.Match.Player;
            player.Credits = 5000;
            Assert.IsTrue(_hud.UnlockFromPage(ALockedUnitWithAPrice()).Ok, _match.Message);
            int credits = player.Credits;

            UnitStats second = ALockedUnitWithAPrice();
            _hud.OpenUnlockPage();
            Assert.IsFalse(_hud.UnlockFromPage(second).Ok);
            Assert.IsTrue(_hud.IsUnlockPageOpen, "It stays, with the message saying why.");
            Assert.IsFalse(player.Unlocked.Contains(second));
            Assert.AreEqual(credits, player.Credits);
            StringAssert.Contains("already unlocked a unit", _match.Message);
        }

        [UnityTest]
        public IEnumerator HandleEscape_ClosesThePage_AndThePauseMenuLeavesThatFrameAlone()
        {
            yield return Skirmish();
            yield return null; // The pause menu's Start.
            PauseMenu pause = PauseMenu.Current;
            Assert.IsNotNull(pause);
            Assert.IsFalse(pause.EscapeHasOtherWork(), "Nothing is selected yet.");

            _hud.OpenUnlockPage();
            Assert.IsTrue(pause.EscapeHasOtherWork(), "While it is open, Esc is the page's.");
            Assert.IsTrue(_hud.HandleEscape());
            Assert.IsFalse(_hud.IsUnlockPageOpen);
            Assert.IsTrue(pause.EscapeHasOtherWork(), "It runs before or after the page, so it stays out all this frame.");
            Assert.IsFalse(_hud.HandleEscape(), "With the page shut there is nothing for it to do.");

            yield return null;
            Assert.IsFalse(pause.EscapeHasOtherWork(), "The next Esc pauses.");
        }

        [UnityTest]
        public IEnumerator ARightClick_ClosesThePage()
        {
            yield return Skirmish();
            _hud.OpenUnlockPage();
            yield return null;
            Assert.IsTrue(_hud.IsUnlockPageOpen);

            RightClick.ReadForTests = () => new RightButtonReading { Pressed = true, Held = true };
            yield return null;
            Assert.IsTrue(_hud.IsUnlockPageOpen, "A press isn't a click yet.");
            RightClick.ReadForTests = () => new RightButtonReading { Released = true };
            yield return null;
            yield return null;
            Assert.IsFalse(_hud.IsUnlockPageOpen);
        }

        [UnityTest]
        public IEnumerator Opening_ClearsTheSelectionAndAnythingBeingPlaced()
        {
            yield return Skirmish();
            PlacedSquad squad = _match.Match.Player.Squads[0];
            _match.BeginMoving(squad);
            Assert.IsTrue(_match.IsPlacing);
            _hud.OpenUnlockPage();
            Assert.IsFalse(_match.IsPlacing);
            Assert.AreEqual(-1, _selection.SelectedSquadId);
            Assert.IsNull(_selection.SelectedTower);
            Assert.IsTrue(_selection.InputEnabled, "Placing had switched it off.");
        }

        [UnityTest]
        public IEnumerator ASquadSelectedWhileThePageIsOpen_IsClearedTheNextFrame()
        {
            yield return Skirmish();
            _hud.OpenUnlockPage();
            yield return null;
            int id = _match.GetComponent<BattleController>().Simulation.Squads[0].Id;
            _selection.Select(id); // As a squad does when it lands.
            Assert.AreEqual(id, _selection.SelectedSquadId);
            yield return null;
            Assert.AreEqual(-1, _selection.SelectedSquadId);

            _hud.CloseUnlockPage();
            _selection.Select(id);
            yield return null;
            Assert.AreEqual(id, _selection.SelectedSquadId, "With the page shut, selecting works again.");
        }

        [UnityTest]
        public IEnumerator ThePage_ShutsWhenThePhaseLeavesDeployment()
        {
            yield return Skirmish();
            _hud.OpenUnlockPage();
            _match.Ready();
            yield return null;
            Assert.IsFalse(_hud.IsUnlockPageOpen);
        }

        [UnityTest]
        public IEnumerator ThePage_FitsTheWholeRosterAt1080p()
        {
            yield return Skirmish();
            var rows = new List<UnitStats>();
            MatchHud.FillShopRows(_match.Match.Player.Roster, rows);
            Rect list = MatchHud.UnlockPageListRect(MatchHud.UnlockPageRect(1920f, 1080f), PauseMenu.CornerButtonPixels(1920f, 1080f, 1f).y);
            Assert.AreEqual(16, MatchHud.UnlockPageColumns(list.width));
            Assert.AreEqual(874f, MatchHud.UnlockPageContentHeight(rows, 16), "Tiers of 20, 24, 22 and 3 units.");
            Assert.LessOrEqual(MatchHud.UnlockPageContentHeight(rows, 16), list.height, "No scroll bar at 1080p, with the list above the Menu button.");
        }

        [UnityTest]
        public IEnumerator FactionMode_HasNoPage()
        {
            yield return StartMatch(MatchMode.Faction);
            _hud.OpenUnlockPage();
            Assert.IsFalse(_hud.IsUnlockPageOpen);
        }
    }
}
