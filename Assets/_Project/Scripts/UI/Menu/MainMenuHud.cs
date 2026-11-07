// MainMenuHud.cs
// The main menu's screen (M10), in the MainMenu scene over MenuBackdrop's picture: the title, and two pages of buttons. The first has Play, Settings and Quit.
// Play's page has Faction mode, Unlock Skirmish mode, Sandbox and Back: a chosen mode shows a panel on the right with its settings and Start, and the Sandbox leaves at once.
// The first time it shows after the game starts, the owner's disclaimer (Disclaimer, D96) comes first, and the menu fades in once it is clicked away.
// Its version is in small print in the lower left (D100). It fades in from black and fades out before a scene change. Esc steps back: the card list, then Play's page, and it closes Settings. It scales to any window (HudScale.ApplyFit).
// Difficulty and base health are remembered (GameSettings). The card filter starts at All cards each time this screen loads, and no mode is chosen.
// The buttons call plain public methods, which a batch run's tests can call too, since a batch run draws no IMGUI.
// Depends on: Disclaimer, GameFlow, GameSettings, MenuStyle, SettingsScreen, Halobellum.Economy, Unity Input System.

using System;
using Halobellum.Economy;
using Halobellum.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Halobellum.UI
{
    public sealed class MainMenuHud : MonoBehaviour
    {
        public const string Title = "HALOBELLUM";

        public enum Page { Main, Play }

        /// <summary>How long after the card list closes a press on the Play page takes nothing: Windows's usual time for a double-click.</summary>
        public const float ClosedListGuardSeconds = 0.5f;

        private const float FadeInSeconds = 1.6f, FadeOutSeconds = 0.35f;
        private const float ButtonWidth = 400f, ButtonHeight = 64f, ButtonGap = 6f;
        private const float PanelGap = 24f, PanelWidth = 560f, PanelMargin = 24f, RowHeight = 52f, RowLabelWidth = 140f, ControlHeight = 36f;
        private const float StepperWidth = 170f, StartHeight = 48f;
        private static readonly string[] Difficulties = { "Easy", "Normal", "Hard" };
        private static readonly string[] CardLabels = { "All cards", "No reinforcement cards", "Only reinforcement cards" }; // In CardFilter's order.

        private readonly SettingsScreen _settings = new SettingsScreen();
        private bool _showSettings;
        private float _shownAt;
        private Action _leaving;
        private float _leavingAt;
        private Page _page = Page.Main;
        private MatchMode? _selectedMode;
        private CardFilter _cards = CardFilter.AllCards;
        private bool _cardListOpen;
        private float _ignorePressesUntil;

        /// <summary>True while the settings panel is showing (for the test tool).</summary>
        public bool ShowingSettings => _showSettings;

        public Page CurrentPage => _page;

        /// <summary>The mode whose panel is showing, or null.</summary>
        public MatchMode? SelectedMode => _selectedMode;

        /// <summary>Which cards an Unlock Skirmish match started from here offers.</summary>
        public CardFilter Cards { get => _cards; set => _cards = value; }

        /// <summary>True while the list of the card filter is open.</summary>
        public bool CardListOpen { get => _cardListOpen; set => _cardListOpen = value; }

        /// <summary>True once a button has started the fade out of this scene.</summary>
        public bool IsLeaving => _leaving != null;

        /// <summary>The base health at this place of the slider, from 0 to 1.</summary>
        public static int BaseHealthFromSlider(float position) =>
            MatchSetup.SnapBaseHealth(Mathf.RoundToInt(Mathf.Lerp(MatchSetup.LeastBaseHealth, MatchSetup.MostBaseHealth, position)));

        /// <summary>Where the slider stands for this base health, from 0 to 1.</summary>
        public static float SliderFromBaseHealth(int health) => Mathf.InverseLerp(MatchSetup.LeastBaseHealth, MatchSetup.MostBaseHealth, health);

        public void OpenPlayPage() => _page = Page.Play;

        /// <summary>Shows this mode's panel.</summary>
        public void SelectMode(MatchMode mode)
        {
            _selectedMode = mode;
            _cardListOpen = false;
        }

        /// <summary>Leaves for a match of the selected mode. With none selected, nothing happens.</summary>
        public void StartSelected()
        {
            if (_selectedMode == null) return;
            GameSettings.Save();
            if (_selectedMode == MatchMode.Faction)
            {
                Leave(GameFlow.NewMatch);
                return;
            }
            var setup = new MatchSetup { Mode = MatchMode.UnlockSkirmish, BaseHealth = GameSettings.SkirmishBaseHealth, Cards = _cards };
            Leave(() => GameFlow.NewSkirmish(setup));
        }

        public void OpenSandbox() => Leave(GameFlow.OpenSandbox);

        /// <summary>The tutorial's scripted match (D77).</summary>
        public void OpenTutorial() => Leave(GameFlow.StartTutorial);

        /// <summary>
        /// Draws this screen under whatever Event.current is now, with the mouse at these pixels of IMGUI's screen: how the test tool sends IMGUI's own mouse events to it, with
        /// nothing going outside the game. The place is set after the scale, because setting GUI.matrix puts an event's mouse back where the real one is.
        /// </summary>
        public void DrawForTest(Vector2 mousePixels)
        {
            MenuStyle.Ensure();
            float scale = ApplyScale();
            Event.current.mousePosition = mousePixels / scale;
            DrawScaled(scale);
        }

        /// <summary>From Play's page back to the first one.</summary>
        public void Back()
        {
            _page = Page.Main;
            _selectedMode = null;
            _cardListOpen = false;
        }

        /// <summary>What Esc does: one step back. Returns false if there was nothing to step back from.</summary>
        public bool HandleEscape()
        {
            if (_leaving != null) return false;
            if (_showSettings)
            {
                CloseSettings();
                return true;
            }
            if (_cardListOpen)
            {
                _cardListOpen = false;
                return true;
            }
            if (_page != Page.Play) return false;
            Back();
            return true;
        }

        private void Start() => _shownAt = Time.unscaledTime;

        private float _pictureAt = -1f;

        private void Update()
        {
            if (Disclaimer.Pending)
            {
                TakeDisclaimerPicture();
                return; // Its own clicks and keys go on to the menu (OnGUI).
            }
            if (_leaving != null && Time.unscaledTime - _leavingAt >= FadeOutSeconds)
            {
                Action leave = _leaving;
                _leaving = null;
                leave();
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) HandleEscape();
        }

        /// <summary>With -disclaimerPicture &lt;png&gt;: a picture of the disclaimer after a moment, then one of the menu once it has faded in (&lt;png&gt; with _menu), then the game quits.</summary>
        private void TakeDisclaimerPicture()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-disclaimerPicture");
            if (at < 0 || at + 1 >= args.Length) return;
            if (_pictureAt < 0f) _pictureAt = Time.unscaledTime + 2f;
            else if (_pictureAt > 0f && Time.unscaledTime >= _pictureAt)
            {
                ScreenCapture.CaptureScreenshot(args[at + 1]);
                _pictureAt = 0f;
                Invoke(nameof(GoOnForPicture), 1f);
            }
        }

        private void GoOnForPicture()
        {
            Disclaimer.Pending = false;
            _shownAt = Time.unscaledTime;
            Invoke(nameof(MenuPicture), FadeInSeconds + 1.5f);
        }

        private void MenuPicture()
        {
            string[] args = Environment.GetCommandLineArgs();
            string path = args[Array.IndexOf(args, "-disclaimerPicture") + 1];
            ScreenCapture.CaptureScreenshot(System.IO.Path.ChangeExtension(path, null) + "_menu.png");
            Invoke(nameof(QuitAfterPicture), 1f);
        }

        private void QuitAfterPicture() => Application.Quit();

        private void CloseSettings()
        {
            _settings.Close();
            _showSettings = false;
        }

        private void Leave(Action action)
        {
            if (_leaving != null) return;
            _leaving = action;
            _leavingAt = Time.unscaledTime;
        }

        private void OnGUI()
        {
            MenuStyle.Ensure();
            DrawScaled(ApplyScale());
        }

        private static float ApplyScale() => HudScale.ApplyFit(Mathf.Max(1120f, SettingsScreen.PanelWidth + 20f), SettingsScreen.PanelHeight + 20f);

        private void DrawScaled(float scale)
        {
            float width = HudScale.ScaledWidth(scale), height = HudScale.ScaledHeight(scale);

            if (Disclaimer.Pending)
            {
                if (Disclaimer.Draw(width, height))
                {
                    Disclaimer.Pending = false;
                    _shownAt = Time.unscaledTime; // The menu fades in from the black.
                }
                return;
            }
            if (_showSettings)
            {
                MenuStyle.Fill(new Rect(0f, 0f, width, height), new Color(0f, 0f, 0f, 0.35f));
                if (_settings.Draw(width, height)) _showSettings = false;
            }
            else
            {
                DrawMenu(width, height);
            }
            DrawFade(width, height);
        }

        private void DrawMenu(float width, float height)
        {
            // A soft darkness on the left, for the title and buttons to stand on; the picture stays clear on the right.
            MenuStyle.FadeRight(new Rect(0f, 0f, width * 0.68f, height), new Color(0.015f, 0.015f, 0.03f, 0.86f));

            float left = Mathf.Max(56f, width * 0.05f);
            float top = height * 0.13f;
            float titleWidth = MenuStyle.SpacedText(left, top, Title, MenuStyle.Title, 7f, Color.white);
            float underTitle = top + MenuStyle.Title.CalcSize(new GUIContent("H")).y + 6f;
            MenuStyle.FadeRight(new Rect(left, underTitle, titleWidth, 2f), MenuStyle.Accent);

            float y = Mathf.Max(underTitle + 40f, height * 0.42f);
            if (_page == Page.Play) DrawPlayPage(left, y);
            else DrawMainPage(left, y);
            GUI.enabled = true;

            // The game's version, small, in the lower left (the owner, D100): Unity's Player Settings version.
            GUI.Label(new Rect(16f, height - 30f, 300f, 22f), VersionLabel, MenuStyle.Small);
        }

        /// <summary>What the lower left of the menu says: the version set in Unity's Player Settings.</summary>
        public static string VersionLabel => "Version " + Application.version;

        private void DrawMainPage(float left, float y)
        {
            GUI.enabled = _leaving == null;
            if (ColumnButton(left, ref y, "Play")) OpenPlayPage();
            if (ColumnButton(left, ref y, "Settings"))
            {
                _settings.Open();
                _showSettings = true;
            }
            if (ColumnButton(left, ref y, "Quit")) Leave(GameFlow.QuitGame);
        }

        private void DrawPlayPage(float left, float y)
        {
            // The list is drawn last and is the only thing that takes a click while open. Read once, so that every part of this pass agrees.
            bool enabled = _leaving == null;
            GUI.enabled = enabled && !_cardListOpen;

            // The second click of a double-click that closed the list would land on whatever lay under the list, which may be Start, so it is thrown away here.
            if (Event.current.type == EventType.MouseDown && Time.unscaledTime < _ignorePressesUntil) Event.current.Use();

            float panelTop = y;
            if (ColumnButton(left, ref y, "Faction mode", _selectedMode == MatchMode.Faction)) SelectMode(MatchMode.Faction);
            if (ColumnButton(left, ref y, "Unlock Skirmish mode", _selectedMode == MatchMode.UnlockSkirmish)) SelectMode(MatchMode.UnlockSkirmish);
            if (ColumnButton(left, ref y, "Tutorial")) OpenTutorial();
            if (ColumnButton(left, ref y, "Sandbox")) OpenSandbox();
            if (ColumnButton(left, ref y, "Back")) Back();

            float panelLeft = left + ButtonWidth + PanelGap;
            if (_selectedMode == MatchMode.Faction) DrawFactionPanel(panelLeft, panelTop);
            else if (_selectedMode == MatchMode.UnlockSkirmish) DrawSkirmishPanel(panelLeft, panelTop, enabled);
        }

        private bool ColumnButton(float left, ref float y, string label, bool selected = false)
        {
            bool pressed = MenuStyle.MenuButton(new Rect(left, y, ButtonWidth, ButtonHeight), label, selected);
            y += ButtonHeight + ButtonGap;
            return pressed;
        }

        private void DrawFactionPanel(float x, float y)
        {
            Rect panel = PanelFor(x, y, 1);
            float row = PanelHeading(panel, "FACTION MODE", "Fight with a faction's units, chosen by your starting card.");
            DrawDifficulty(panel, row);
            DrawStart(panel);
        }

        private void DrawSkirmishPanel(float x, float y, bool enabled)
        {
            Rect panel = PanelFor(x, y, 3);
            float row = PanelHeading(panel, "UNLOCK SKIRMISH MODE", "No factions. Unlock any unit; every spell card can appear.");
            DrawDifficulty(panel, row);
            row += RowHeight;
            DrawBaseHealth(panel, row);
            row += RowHeight;
            RowLabel(panel, row, "Cards");
            DrawStart(panel);

            GUI.enabled = enabled;
            Rect box = ControlRect(panel, row);
            bool wasOpen = _cardListOpen;
            _cards = (CardFilter)MenuStyle.Dropdown(box, "Cards", CardLabels, (int)_cards, ref _cardListOpen);
            if (wasOpen && !_cardListOpen) _ignorePressesUntil = Time.unscaledTime + ClosedListGuardSeconds;
        }

        /// <summary>A panel with room for this many rows, and its Start at the bottom.</summary>
        private static Rect PanelFor(float x, float y, int rows)
        {
            var panel = new Rect(x, y, PanelWidth, 84f + rows * RowHeight + 30f + StartHeight + PanelMargin);
            MenuStyle.Panel(panel);
            return panel;
        }

        /// <summary>Writes a panel's heading and its line of text, and gives the top of its first row.</summary>
        private static float PanelHeading(Rect panel, string heading, string text)
        {
            GUI.Label(new Rect(panel.x + PanelMargin, panel.y + 14f, panel.width - 2f * PanelMargin, 30f), heading, MenuStyle.Section);
            GUI.Label(new Rect(panel.x + PanelMargin, panel.y + 46f, panel.width - 2f * PanelMargin, 24f), text, MenuStyle.Small);
            return panel.y + 84f;
        }

        private static void RowLabel(Rect panel, float y, string label) =>
            GUI.Label(new Rect(panel.x + PanelMargin, y, RowLabelWidth, ControlHeight), label, MenuStyle.Body);

        private static Rect ControlRect(Rect panel, float y) =>
            new Rect(panel.x + PanelMargin + RowLabelWidth, y, panel.width - 2f * PanelMargin - RowLabelWidth, ControlHeight);

        /// <summary>The AI's difficulty, which the draft screen's match will use.</summary>
        private static void DrawDifficulty(Rect panel, float y)
        {
            RowLabel(panel, y, "Difficulty");
            int current = Mathf.Clamp((int)GameSettings.Difficulty, 0, Difficulties.Length - 1);
            int wanted = MenuStyle.Choice(ControlRect(panel, y), "Difficulty", Difficulties, current);
            if (wanted != current) GameSettings.Difficulty = (AiDifficulty)wanted;
        }

        /// <summary>A slider for the whole range, and arrows for one step at a time.</summary>
        private static void DrawBaseHealth(Rect panel, float y)
        {
            RowLabel(panel, y, "Base health");
            Rect control = ControlRect(panel, y);
            int health = GameSettings.SkirmishBaseHealth;

            float position = SliderFromBaseHealth(health);
            float moved = MenuStyle.Slider(new Rect(control.x, control.y, control.width - StepperWidth - 12f, control.height), position);
            int wanted = Mathf.Approximately(moved, position) ? health : BaseHealthFromSlider(moved);

            int step = MenuStyle.Stepper(new Rect(control.xMax - StepperWidth, control.y, StepperWidth, control.height), "Base health", wanted.ToString());
            wanted += step * MatchSetup.BaseHealthStep;
            if (wanted != health) GameSettings.SkirmishBaseHealth = wanted;
        }

        private void DrawStart(Rect panel)
        {
            var start = new Rect(panel.xMax - PanelMargin - 200f, panel.yMax - PanelMargin - StartHeight, 200f, StartHeight);
            if (MenuStyle.SmallFramedButton(start, "Start", true)) StartSelected();
        }

        private void DrawFade(float width, float height)
        {
            float alpha = 1f - Mathf.Clamp01((Time.unscaledTime - _shownAt) / FadeInSeconds);
            alpha *= alpha;
            if (_leaving != null) alpha = Mathf.Max(alpha, Mathf.Clamp01((Time.unscaledTime - _leavingAt) / FadeOutSeconds));
            if (alpha > 0f) MenuStyle.Fill(new Rect(0f, 0f, width, height), new Color(0f, 0f, 0f, alpha));
        }
    }
}
