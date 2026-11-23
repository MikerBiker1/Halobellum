// UnitIconStatsTests.cs
// The stats window of a hovered unit icon, on the real Match scene's own screens, with IMGUI's own events: Unlock Skirmish's unit bar and
// "Unlock units" page, the starting draft cards and a reinforcement card (in any mode), and the "i" corner of a bar or page cell, which pins
// the window as a click on a shop row does in faction mode, while a click on the rest of the cell still buys. A batch run calls no OnGUI, so
// the test opens an IMGUI pass of its own (InGui) and draws MatchHud in it under an event it makes. In a batch run IMGUI takes the mouse from
// the real cursor again as soon as GUI.matrix is set (HudScale.Apply), so an event's mouse never reaches the HUD (Calibrate measures it and logs
// it); the tests then hover and press through the test tool's stand-ins for the mouse, which the screens read in its place: MatchHud.DemoHover,
// CardScreens.DemoMouse and MenuStyle.PressedByTest. Where the mouse does get through (a run with a screen), they use the mouse itself.
// In a batch run, then, the pointer maths of the bar and page (inView, onScreen) and a real click's way to the "i" before the cell are not exercised:
// the draw-order test checks the property the click relies on (the "i" drawn before its cell's button); the rest is checked by reading or a play test.
// Depends on: Halobellum.UI, Halobellum.Presentation, Halobellum.Economy, Halobellum.Core, NUnit, Unity Test Framework.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Halobellum.Tests
{
    public class UnitIconStatsTests
    {
        private MatchController _match;
        private MatchHud _hud;
        private BattleController _battle;
        private SquadStatsWindow _stats;
        private readonly Dictionary<string, Rect> _buttons = new Dictionary<string, Rect>();
        private float _scale;

        private static object _guiState;

        [TearDown]
        public void LeaveNothingBehind()
        {
            MenuStyle.ButtonDrawnForTest -= NoteButton;
            MenuStyle.PressedByTest = null;
            CardScreens.DemoMouse = null;
            GUIUtility.hotControl = 0;
            GameFlow.TakeMatchSetup();
            BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant); // These are static.
        }

        private readonly List<string> _drawnInOrder = new List<string>();

        private void NoteButton(string key, Rect pixels)
        {
            _buttons[key] = pixels;
            _drawnInOrder.Add(key); // ButtonDrawnForTest fires on Repaint, in draw order.
        }

        // ------------------------------------------------------------------ an IMGUI pass of the test's own

        private static int GuiDepth() => (int)typeof(GUIUtility).GetProperty("guiDepth", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        private static MethodInfo GuiMethod(Type type, string name, int parameters)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (method.Name == name && method.GetParameters().Length == parameters) return method;
            }
            Assert.Fail($"{type.Name}.{name} is not in this Unity.");
            return null;
        }

        /// <summary>
        /// Runs `draw` inside an IMGUI pass under this event, as IMGUIContainer opens one: GUI functions may only be called in one. How the event's mouse
        /// reaches the HUD through GUI.matrix depends on the order things are set up in, which is native code: Calibrate tries each way (`how`) and keeps one
        /// that works.
        /// </summary>
        private static void InGui(Event e, Action draw, int how)
        {
            Event shown = Event.current;
            Type clip = typeof(GUIUtility).Assembly.GetType("UnityEngine.GUIClip");
            bool master = how == 2 || how == 3, eventFirst = how < 4, push = how % 2 == 0;
            Event masterEvent = master ? (Event)typeof(Event).GetField("s_MasterEvent", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) : null;
            if (master)
            {
                Assert.IsNotNull(masterEvent, "No master event.");
                masterEvent.type = e.type;
                masterEvent.mousePosition = e.mousePosition;
                masterEvent.button = e.button;
                masterEvent.clickCount = e.clickCount;
                e = masterEvent;
            }
            if (eventFirst) Event.current = e;
            bool own = GuiDepth() <= 0;
            if (own)
            {
                if (_guiState == null) _guiState = Activator.CreateInstance(typeof(GUIUtility).Assembly.GetType("UnityEngine.ObjectGUIState"), true);
                GuiMethod(typeof(GUIUtility), "BeginContainer", 1).Invoke(null, new[] { _guiState });
            }
            if (!eventFirst) Event.current = e;
            if (push) GuiMethod(clip, "Internal_PushParentClip", 2).Invoke(null, new object[] { Matrix4x4.identity, new Rect(0f, 0f, Screen.width, Screen.height) });
            try
            {
                Assert.Greater(GuiDepth(), 0, "The test could not open an IMGUI pass.");
                draw();
            }
            finally
            {
                GUI.matrix = Matrix4x4.identity;
                if (push) GuiMethod(clip, "Internal_PopParentClip", 0).Invoke(null, null);
                if (own) GuiMethod(typeof(GUIUtility), "EndContainer", 0).Invoke(null, null);
                Event.current = shown;
            }
        }

        private const int Ways = 6;
        private int _how = -1;

        /// <summary>Where the HUD reads the mouse, in its own units, for a mouse at this point of an event, set up this way.</summary>
        private Vector2 SeenByTheHud(Vector2 eventMouse, int how, out Vector2 raw)
        {
            Vector2 seen = default, before = default;
            InGui(new Event { type = EventType.Repaint, mousePosition = eventMouse }, () =>
            {
                before = Event.current.mousePosition;
                GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f)); // As HudScale.Apply sets it.
                seen = Event.current.mousePosition;
            }, how);
            raw = before;
            return seen;
        }

        /// <summary>
        /// How IMGUI moves an event's mouse into the HUD's units, measured: in the game it goes through GUI.matrix. The test then puts in its events whatever
        /// makes the HUD read the point it means. If no way gets the mouse through, the tests fall back on the test tool's own stand-ins for the mouse
        /// (MatchHud.DemoHover, CardScreens.DemoMouse, MenuStyle.PressedByTest), which the screens read in its place.
        /// </summary>
        private void Calibrate()
        {
            _scale = HudScale.ScaleFor(Screen.width, Screen.height, GameSettings.UiScale);
            _how = -1;
            for (int how = 0; how < Ways; how++)
            {
                Vector2 a = SeenByTheHud(new Vector2(100f, 100f), how, out Vector2 rawA), b = SeenByTheHud(new Vector2(300f, 300f), how, out Vector2 rawB);
                var gain = new Vector2((b.x - a.x) / 200f, (b.y - a.y) / 200f);
                Debug.Log($"UnitIconStatsTests: way {how}: the event's mouse 100 and 300 read {rawA} and {rawB} before GUI.matrix, {a} and {b} after it");
                if (_how >= 0 || Mathf.Abs(gain.x) < 0.01f || Mathf.Abs(gain.y) < 0.01f) continue;
                _how = how;
                _mouseGain = gain;
                _mouseShift = a - 100f * gain;
            }
            Debug.Log($"UnitIconStatsTests: screen {Screen.width}x{Screen.height}, HUD scale {_scale}; " + (_how >= 0 ? $"way {_how}: the HUD reads an event's mouse m as m * {_mouseGain} + {_mouseShift}" : "no way gets an event's mouse to the HUD: the tests use the test tool's stand-ins for the mouse"));
        }

        private bool MouseWorks => _how >= 0;
        private Vector2 _mouseGain = Vector2.one, _mouseShift;

        /// <summary>Draws the HUD once, as IMGUI would for this event, with the mouse at this point in HUD units.</summary>
        private void Draw(EventType type, Vector2 hudPoint)
        {
            var mouse = new Vector2((hudPoint.x - _mouseShift.x) / _mouseGain.x, (hudPoint.y - _mouseShift.y) / _mouseGain.y);
            InGui(new Event { type = type, button = 0, clickCount = 1, mousePosition = mouse }, () => _hud.DrawForTest(), Mathf.Max(0, _how));
        }

        /// <summary>Hovers this point (and, without a mouse, the unit it is the cell of) and draws a frame of the HUD.</summary>
        private void Hover(Vector2 hudPoint, UnitStats cellOf = null)
        {
            if (MouseWorks)
            {
                Draw(EventType.Repaint, hudPoint);
                return;
            }
            _hud.DemoHover = cellOf;
            CardScreens.DemoMouse = cellOf == null ? hudPoint : (Vector2?)null;
            Draw(EventType.Repaint, Away);
            _hud.DemoHover = null;
            CardScreens.DemoMouse = null;
        }

        private static readonly Vector2 Away = new Vector2(-500f, -500f);

        /// <summary>Every button the HUD draws, by key, in HUD units (relative to its scroll view's contents, for one inside a scroll view).</summary>
        private Rect Button(string key)
        {
            Assert.IsTrue(_buttons.TryGetValue(key, out Rect pixels), key + " was never drawn.");
            return new Rect(pixels.position / _scale, pixels.size / _scale);
        }

        /// <summary>A press and release of the left button here, as IMGUI's own events; without a mouse, the test tool's press of the button with this key.</summary>
        private IEnumerator Click(Vector2 hudPoint, string key)
        {
            if (!MouseWorks)
            {
                MenuStyle.PressedByTest = key;
                Draw(EventType.Repaint, Away);
                Assert.IsNull(MenuStyle.PressedByTest, key + " was pressed.");
                yield return null;
                yield break;
            }
            Draw(EventType.MouseDown, hudPoint);
            Assert.AreNotEqual(0, GUIUtility.hotControl, "Some button took the press.");
            Draw(EventType.MouseUp, hudPoint);
            GUIUtility.hotControl = 0;
            yield return null;
        }

        // ------------------------------------------------------------------ setting up

        private IEnumerator Load(MatchMode mode, bool choose = true)
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MatchScene, LoadSceneMode.Single);
            yield return null;
            _match = Object.FindAnyObjectByType<MatchController>();
            _hud = Object.FindAnyObjectByType<MatchHud>();
            _battle = Object.FindAnyObjectByType<BattleController>();
            _stats = Object.FindAnyObjectByType<SquadStatsWindow>();
            Assert.IsNotNull(_match, "The Match scene has no MatchController.");
            Assert.IsNotNull(_hud, "The Match scene has no MatchHud.");
            Assert.IsNotNull(_stats, "The Match scene has no SquadStatsWindow.");
            _match.NewMatch(3, new MatchSetup { Mode = mode });
            if (choose) Assert.IsTrue(_match.ChooseDraft(0).Ok, _match.Message);
            yield return null;
            yield return null;
            Calibrate();
            MenuStyle.ButtonDrawnForTest += NoteButton;
        }

        /// <summary>Leaves the first few units of the shop's order unlocked, so that the bar is one row with no scroll bar.</summary>
        private List<UnitStats> UnlockFirst(int count)
        {
            PlayerState player = _match.Match.Player;
            var sorted = new List<UnitStats>();
            MatchHud.FillShopRows(player.Roster, sorted);
            player.Unlocked.Clear();
            for (int i = 0; i < count; i++) player.Unlocked.Add(sorted[i]);
            player.Credits = 5000;
            return sorted;
        }

        private void AssertTheWindowShows(UnitStats unit, string where)
        {
            Assert.AreEqual(unit, _stats.HoverUnit, $"Hovering {unit.Name} {where} tells the stats window of it.");
            Assert.IsTrue(_stats.TryGetWindow(out Rect window) && window.height > 0f, $"Hovering {unit.Name} {where} draws the stats window.");
        }

        // ------------------------------------------------------------------ hover

        [UnityTest]
        public IEnumerator HoveringAUnitInTheSkirmishBar_ShowsItsStatsWindow()
        {
            yield return Load(MatchMode.UnlockSkirmish);
            List<UnitStats> sorted = UnlockFirst(3);
            Draw(EventType.Repaint, Away);
            yield return null;
            yield return null;
            Assert.IsFalse(_stats.TryGetWindow(out _), "Nothing hovered, nothing shown.");
            Assert.IsNotNull(_battle.Simulation, "Deployment has its preview battle.");

            UnitStats unit = sorted[1];
            Rect cell = Button(MatchHud.BarCellKey(unit));
            Hover(new Vector2(cell.center.x, cell.center.y + 20f), unit);
            AssertTheWindowShows(unit, "in the bar");
            _stats.TryGetWindow(out Rect window);
            Assert.IsFalse(window.Overlaps(_hud.UnitBarShown), $"The window {window} keeps clear of the bar {_hud.UnitBarShown}.");
        }

        [UnityTest]
        public IEnumerator HoveringAUnitOnTheUnlockPage_ShowsItsStatsWindow()
        {
            yield return Load(MatchMode.UnlockSkirmish);
            List<UnitStats> sorted = UnlockFirst(3);
            _hud.OpenUnlockPage();
            Draw(EventType.Repaint, Away);
            yield return null;
            yield return null;

            UnitStats unit = sorted[0]; // The first cell of the list, under its tier's header, with the list scrolled to the top.
            Rect inList = Button(MatchHud.PageCellKey(unit));
            Rect list = _hud.UnlockPageListShown;
            var cell = new Rect(list.position + inList.position, inList.size);
            Hover(new Vector2(cell.center.x, cell.center.y + 20f), unit);
            AssertTheWindowShows(unit, "on the unlock page");
            _stats.TryGetWindow(out Rect window);
            Assert.LessOrEqual(window.xMax, _hud.UnlockPageShown.x + 0.5f, "Beside the page.");
        }

        /// <summary>Moves the mouse down the screen at this x, and returns each unit that counted as hovered on a card, checking its window the first time.</summary>
        private IEnumerator HoverDown(float x, float top, float bottom, List<UnitStats> found, List<Rect> cards)
        {
            for (float y = top; y < bottom; y += 6f)
            {
                Hover(new Vector2(x, y));
                UnitStats unit = CardScreens.HoveredUnit;
                if (unit == null || found.Contains(unit)) continue;
                found.Add(unit);
                AssertTheWindowShows(unit, "on a card");
                Rect card = CardScreens.HoveredUnitCard;
                cards.Add(card);
                Assert.IsTrue(card.Contains(new Vector2(x, y)), $"The card {card} is under the mouse.");
                _stats.TryGetWindow(out Rect window);
                float width = HudScale.ScaledWidth(_scale);
                bool room = width - 10f - (card.xMax + 6f) >= window.width || card.x - 6f - 10f >= window.width; // A small window can have a card in the middle with room on neither side.
                if (room) Assert.IsFalse(window.Overlaps(card), $"The window {window} is beside the card {card}, not over it.");
                Assert.GreaterOrEqual(window.x, 0f, "On the screen.");
                Assert.LessOrEqual(window.xMax, width + 0.5f, "On the screen.");
                Assert.GreaterOrEqual(window.y, 0f);
                yield return null; // A new frame, so that the next unit's window is its own.
            }
        }

        [UnityTest]
        public IEnumerator HoveringASquadOnAStartingDraftCard_ShowsItsStatsWindow([Values(MatchMode.UnlockSkirmish, MatchMode.Faction)] MatchMode mode)
        {
            yield return Load(mode, choose: false);
            Match m = _match.Match;
            Assert.AreEqual(MatchPhase.Draft, m.Phase);
            Assert.IsNull(_battle.Simulation, "The draft has no battle on the field: a hovered unit needs none.");
            Draw(EventType.Repaint, Away);
            yield return null;
            yield return null;

            float width = HudScale.ScaledWidth(_scale), height = HudScale.ScaledHeight(_scale);
            CardRow row = CardLayout.Arrange(m.PlayerDraft.Count, width, 10f + 84f + 14f, height - 14f); // As MatchHud.DrawDraft lays it out.
            Rect first = row.CardAt(0);
            var found = new List<UnitStats>();
            var cards = new List<Rect>();
            yield return HoverDown(first.center.x, first.y, first.yMax, found, cards);
            CollectionAssert.AreEquivalent(m.PlayerDraft[0].Squads, found, "Each squad of the card shows its own stats.");
            foreach (Rect card in cards) Assert.AreEqual(first, card);
        }

        [UnityTest]
        public IEnumerator HoveringAReinforcementCard_ShowsItsUnitsStatsWindow()
        {
            yield return Load(MatchMode.UnlockSkirmish);
            Match m = _match.Match;
            _battle.Speed = 16f;
            _match.Ready();
            float deadline = Time.realtimeSinceStartup + 120f;
            while (m.Phase == MatchPhase.Battle && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreNotEqual(MatchPhase.Battle, m.Phase, "The battle did not finish in time.");
            yield return null;
            _match.Continue();
            yield return null;
            Assert.AreEqual(MatchPhase.Cards, m.Phase);

            UnitStats locked = m.Player.Roster.Find(u => !m.Player.Unlocked.Contains(u));
            Assert.IsNotNull(locked);
            m.Player.Offer = new CardOffer { Kind = CardKind.Reinforcement, Round = m.Round, SkipBonus = 50, Reinforcements = { ReinforcementCard.Make(locked, 2, 0) } };
            Draw(EventType.Repaint, Away);
            yield return null;
            yield return null;

            float width = HudScale.ScaledWidth(_scale), height = HudScale.ScaledHeight(_scale);
            var found = new List<UnitStats>();
            var cards = new List<Rect>();
            yield return HoverDown(width * 0.5f, 0f, height, found, cards); // The one card is in the middle.
            CollectionAssert.AreEqual(new[] { locked }, found, "The card's unit, still locked, shows its stats.");
            Assert.AreEqual(width * 0.5f, cards[0].center.x, 0.5f);
        }

        // ------------------------------------------------------------------ the "i" corner

        [UnityTest]
        public IEnumerator TheBarCellsICorner_PinsTheStatsWindow_AndTheRestOfTheCellStillBuys()
        {
            yield return Load(MatchMode.UnlockSkirmish);
            List<UnitStats> sorted = UnlockFirst(3);
            PlayerState player = _match.Match.Player;
            Draw(EventType.Repaint, Away);
            yield return null;

            UnitStats unit = sorted[0];
            Rect corner = Button(MatchHud.BarInfoKey(unit));
            Assert.IsTrue(Button(MatchHud.BarCellKey(unit)).Contains(corner.center), "The corner is on its cell.");
            int bought = player.SquadsBoughtThisRound;
            yield return Click(corner.center, MatchHud.BarInfoKey(unit));
            Assert.AreEqual(unit, _hud.PinnedUnit, "A click on the i pins the unit's stats window, as a click on a shop row does in faction mode.");
            Assert.AreEqual(bought, player.SquadsBoughtThisRound, "It buys nothing.");

            yield return Click(corner.center, MatchHud.BarInfoKey(unit));
            Assert.IsNull(_hud.PinnedUnit, "A second click closes it.");

            Rect cell = Button(MatchHud.BarCellKey(unit));
            yield return Click(new Vector2(cell.center.x, cell.yMax - 10f), MatchHud.BarCellKey(unit));
            Assert.AreEqual(bought + 1, player.SquadsBoughtThisRound, "A click on the rest of the cell still buys.");
            Assert.IsNull(_hud.PinnedUnit);
        }

        [UnityTest]
        public IEnumerator ThePageCellsICorner_PinsTheStatsWindow_OverThePage_AndUnlocksNothing()
        {
            yield return Load(MatchMode.UnlockSkirmish);
            List<UnitStats> sorted = UnlockFirst(3);
            PlayerState player = _match.Match.Player;
            _hud.OpenUnlockPage();
            Draw(EventType.Repaint, Away);
            yield return null;

            UnitStats unit = sorted.Find(u => !player.Unlocked.Contains(u));
            Rect list = _hud.UnlockPageListShown;
            Rect corner = Button(MatchHud.PageInfoKey(unit));
            corner.position += list.position;
            Assume.That(list.Contains(corner.center), "The unit's cell is in the list's first screen.");
            int unlocks = player.UnlocksThisRound;
            yield return Click(corner.center, MatchHud.PageInfoKey(unit));
            Assert.AreEqual(unit, _hud.PinnedUnit, "A click on the i pins the unit's stats window.");
            Assert.AreEqual(unlocks, player.UnlocksThisRound, "It unlocks nothing.");
            Assert.IsTrue(_hud.IsUnlockPageOpen, "The page stays open.");

            yield return null;
            yield return null;
            Draw(EventType.Repaint, Away);
            Assert.IsTrue(_stats.TryGetWindow(out Rect window), "The pinned window is drawn over the page's dim, by MatchHud.");
            Assert.LessOrEqual(window.xMax, _hud.UnlockPageShown.x + 0.5f, "In the column left of the page.");
        }

        /// <summary>
        /// Without a real mouse the two tests above press the "i" by its key, so they can't tell whether a real click on the corner reaches it before the
        /// cell's full-size button under it. In IMGUI the control drawn first takes the MouseDown (and hotControl), so what keeps a click on the "i" from
        /// buying or unlocking is the draw order: the corner's button before the cell's. This checks that order for every cell, with no mouse needed.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryCellsICorner_IsDrawnBeforeTheCellsOwnButton_SoAClickOnItIsItsOwn()
        {
            yield return Load(MatchMode.UnlockSkirmish);
            UnlockFirst(3);
            PlayerState player = _match.Match.Player;

            var barRows = new List<UnitStats>();
            MatchHud.FillUnitBarRows(player.Roster, player.Unlocked, barRows);
            Assert.IsNotEmpty(barRows);
            _drawnInOrder.Clear();
            Draw(EventType.Repaint, Away);
            AssertTheCornerComesFirst(barRows, MatchHud.BarInfoKey, MatchHud.BarCellKey, "bar");

            var pageRows = new List<UnitStats>();
            MatchHud.FillShopRows(player.Roster, pageRows);
            Assert.IsNotEmpty(pageRows);
            _hud.OpenUnlockPage();
            _drawnInOrder.Clear();
            Draw(EventType.Repaint, Away);
            AssertTheCornerComesFirst(pageRows, MatchHud.PageInfoKey, MatchHud.PageCellKey, "page");
        }

        private void AssertTheCornerComesFirst(List<UnitStats> units, Func<UnitStats, string> cornerKey, Func<UnitStats, string> cellKey, string where)
        {
            foreach (UnitStats unit in units)
            {
                int corner = _drawnInOrder.IndexOf(cornerKey(unit)), cell = _drawnInOrder.IndexOf(cellKey(unit));
                Assert.GreaterOrEqual(corner, 0, $"{unit.Name}'s \"i\" is drawn in the {where}.");
                Assert.GreaterOrEqual(cell, 0, $"{unit.Name}'s cell button is drawn in the {where}.");
                Assert.Less(corner, cell, $"{unit.Name}'s \"i\" in the {where} is drawn before its cell's button, so a click on it takes the press first and buys or unlocks nothing.");
            }
        }

        // ------------------------------------------------------------------ the Sandbox's unit list (D143)

        [UnityTest]
        public IEnumerator EveryRowOfTheSandboxList_FitsItsRoleBodiesAndPrice_OnOneLine()
        {
            // Wrapped, "Single-target DPS, 4 bodies, 150" took two lines in a box of one and was hidden (the owner, 0.415.1).
            yield return SceneManager.LoadSceneAsync("Sandbox", LoadSceneMode.Single);
            yield return null;
            var hud = Object.FindAnyObjectByType<SandboxHud>();
            Assert.IsNotNull(hud, "The Sandbox scene has no SandboxHud.");
            yield return null;
            CardScreens.TextThatDidNotFit = 0;
            CardScreens.LastTextThatDidNotFit = null;
            InGui(new Event { type = EventType.Repaint, mousePosition = Away }, () => hud.DrawForTest(), 0);
            Assert.AreEqual(0, CardScreens.TextThatDidNotFit, "Did not fit: " + CardScreens.LastTextThatDidNotFit);
        }

        [UnityTest]
        public IEnumerator ClickingAUnitInTheSandboxList_OpensItsStatsWindow_AndASecondClickClosesIt()
        {
            yield return SceneManager.LoadSceneAsync("Sandbox", LoadSceneMode.Single);
            yield return null;
            var sandbox = Object.FindAnyObjectByType<SandboxController>();
            var hud = Object.FindAnyObjectByType<SandboxHud>();
            _stats = Object.FindAnyObjectByType<SquadStatsWindow>();
            Assert.IsNotNull(hud, "The Sandbox scene has no SandboxHud.");
            Assert.IsNotNull(_stats, "The Sandbox scene has no SquadStatsWindow.");
            yield return null;
            UnitStats unit = sandbox.Units[1].Stats;
            Assert.IsNull(_stats.ShopUnit);
            foreach (UnitStats expected in new[] { unit, null })
            {
                MenuStyle.PressedByTest = SandboxHud.UnitRowKey(unit);
                InGui(new Event { type = EventType.Repaint, button = 0, mousePosition = Away }, () => hud.DrawForTest(), 0);
                Assert.IsNull(MenuStyle.PressedByTest, "The row of " + unit.Name + " was drawn as a button and pressed.");
                Assert.AreEqual(expected, _stats.ShopUnit, expected != null ? "The click opens its stats window." : "A second click closes it.");
                Assert.IsNull(sandbox.PlacingType, "A click on the row is not a click on Place.");
                yield return null;
            }
        }
    }
}
