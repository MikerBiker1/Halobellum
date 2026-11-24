// MainMenuHud.cs
// The main menu's screen (M10), in the MainMenu scene over MenuBackdrop's picture: the title, and pages of buttons. The first has Play, LAN Game, Settings and Quit.
// Play's page has Faction mode, Unlock Skirmish mode, Sandbox and Back: a chosen mode shows a panel on the right with its settings and Start, and the Sandbox leaves at once.
// The first time it shows after the game starts, the owner's disclaimer (Disclaimer, D96) comes first, and the menu fades in once it is clicked away,
// with a button's click and only then the menu's music (D101).
// Its version is in small print in the lower left (D100). It fades in from black and fades out before a scene change. Esc steps back: the card list, then Play's page, and it closes Settings. It scales to any window (HudScale.ApplyFit).
// Difficulty and base health are remembered (GameSettings). The card filter starts at All cards each time this screen loads, and no mode is chosen.
// The buttons call plain public methods, which a batch run's tests can call too, since a batch run draws no IMGUI.
// After the disclaimer, when the unit stats files next to the game have mistakes (UnitStatsMods, mod_json), a panel lists them before the menu shows,
// once a launch; while changed stats are in force, or the files have mistakes, a line above the version says so.
// LAN Game's page (M11 Stage 4) has Host game, Join game and Back, and a panel for each, a thin view over LanLobby (Halobellum.Net):
//   Host: your name, the match's mode, and in Unlock Skirmish its base health and cards (the LAN match's own, not the Play page's saved
//         ones); Host starts hosting ("Waiting for a player...", then the other player's name), Start (once someone joined) and Cancel,
//         which stops hosting and keeps the panel. The name can be changed until Host is pressed.
//   Join: your name, the games found on the network (a click joins), an address to type and Join, and what happens (refusals in plain
//         words; no answer: a hint about Windows Firewall). When the host starts, this game follows by itself.
//   Either hands GameFlow a LanLaunch (the connection, the team, the host's settings) and the Match scene's LanMatch plays it.
//   The name and the last address are remembered (GameSettings, not in a test run). Esc or Back leaves the page and tells the other game.
//   -lanDemo host|join (a test run): the page hosts, or joins 127.0.0.1 (trying again until the host is there), and starts by itself.
// Depends on: Disclaimer, GameFlow, GameSettings, MenuStyle, SettingsScreen, UnitStatsMods, LanGameIdentity, Halobellum.Economy,
//             Halobellum.Net (LanLobby, LanDemoArgs), Unity Input System.

using System;
using System.Collections.Generic;
using System.Text;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Net;
using Halobellum.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Halobellum.UI
{
    public sealed class MainMenuHud : MonoBehaviour
    {
        public const string Title = "HALOBELLUM";

        public enum Page { Main, Play, Lan }

        /// <summary>Which panel of the LAN Game page shows.</summary>
        public enum LanPanel { None, Host, Join }

        /// <summary>How long after the card list closes a press on the Play page takes nothing: Windows's usual time for a double-click.</summary>
        public const float ClosedListGuardSeconds = 0.5f;

        private const float FadeInSeconds = 1.6f, FadeOutSeconds = 0.35f;
        private const float ButtonWidth = 400f, ButtonHeight = 64f, ButtonGap = 6f;
        private const float PanelGap = 24f, PanelWidth = 560f, PanelMargin = 24f, RowHeight = 52f, RowLabelWidth = 140f, ControlHeight = 36f;
        private const float StepperWidth = 170f, StartHeight = 48f;
        private static readonly string[] Difficulties = { "Easy", "Normal", "Hard", "Insane" };
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
        private LanLobby _lobby;
        private LanPanel _lanPanel;
        private string _lanName = "";
        private string _lanAddress = "";
        private MatchMode _lanMode = MatchMode.Faction;
        private bool _lanCardListOpen;
        private int _lanBaseHealth = GameSettings.DefaultSkirmishBaseHealth;
        private CardFilter _lanCards = CardFilter.AllCards;
        private LanDemoArgs _demo = new LanDemoArgs();
        private float _demoJoinAt = -1f;
        private LanLaunch _launch;

        /// <summary>Ports and addresses of the LAN lobby (tests use the loopback address and ports the system picks); null = the game's.</summary>
        public static LanLobbyOptions LobbyOptions { get; set; }

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

        // ------------------------------------------------------------------ the LAN Game page (M11 Stage 4)

        /// <summary>The LAN lobby behind the page, or null before the page was opened.</summary>
        public LanLobby Lobby => _lobby;

        /// <summary>The LAN match this screen is leaving for (StartLanGame, or the guest following its host), or null.</summary>
        public LanLaunch LeavingForLan => _launch;

        /// <summary>The LAN panel showing.</summary>
        public LanPanel CurrentLanPanel => _lanPanel;

        /// <summary>The name typed on the LAN page.</summary>
        public string LanName { get => _lanName; set => _lanName = value ?? ""; }

        /// <summary>The address typed on the join panel.</summary>
        public string LanAddress { get => _lanAddress; set => _lanAddress = value ?? ""; }

        /// <summary>The mode the host's match will have.</summary>
        public MatchMode LanMode { get => _lanMode; set => _lanMode = value; }

        /// <summary>The LAN Unlock Skirmish's base health (its own: the Play page's saved one is not touched).</summary>
        public int LanBaseHealth { get => _lanBaseHealth; set => _lanBaseHealth = MatchSetup.SnapBaseHealth(value); }

        /// <summary>The LAN Unlock Skirmish's cards (its own: the Play page's are not touched).</summary>
        public CardFilter LanCards { get => _lanCards; set => _lanCards = value; }

        /// <summary>True while the host panel's card list is open.</summary>
        public bool LanCardListOpen { get => _lanCardListOpen; set => _lanCardListOpen = value; }

        /// <summary>Opens the LAN Game page, with the name and address remembered from last time.</summary>
        public void OpenLanPage()
        {
            _page = Page.Lan;
            _lanPanel = LanPanel.None;
            _lanName = GameSettings.LanPlayerName;
            _lanAddress = GameSettings.LanAddress;
            // A test run joins 127.0.0.1 and lists no games, so a second copy on the PC does not listen on the network.
            // A test run also listens on 127.0.0.1 only, so Windows Firewall does not ask (unless -lanDemoNetwork, for two PCs).
            if (_lobby == null) _lobby = new LanLobby(LobbyOptions ?? new LanLobbyOptions
            {
                Log = LanGameIdentity.Log,
                Browse = _demo.Role != LanDemoRole.Join,
                BindAddress = _demo.IsDemo && !_demo.ListenOnNetwork ? System.Net.IPAddress.Loopback : null,
            });
        }

        /// <summary>Shows the host panel: the name and the match's settings, and Host. Nothing is hosted yet, so the name can still change.</summary>
        public void OpenHostPanel()
        {
            if (_page != Page.Lan) OpenLanPage();
            if (_lobby.IsJoinSide) _lobby.Close(); // leaves the join side (a host joined is told)
            _lanPanel = LanPanel.Host;
        }

        /// <summary>The host panel's Host: starts hosting with the name typed (Waiting for a player...).</summary>
        public void HostLanGame()
        {
            if (_page != Page.Lan) OpenLanPage();
            _lanPanel = LanPanel.Host;
            RememberLanName();
            _lobby.Host(LanGameIdentity.Hello(_lanName));
        }

        /// <summary>The join panel: lists the games heard on the network.</summary>
        public void OpenJoinPanel()
        {
            if (_page != Page.Lan) OpenLanPage();
            if (_lobby.IsHosting) _lobby.Close(); // stops hosting (a player who joined is told)
            _lanPanel = LanPanel.Join;
            _lobby.Browse(LanGameIdentity.Hello(_lanName));
        }

        /// <summary>Joins the game at this address (the one typed if null).</summary>
        public void JoinLanGame(string address = null, int port = 0)
        {
            if (_page != Page.Lan) OpenLanPage();
            _lanPanel = LanPanel.Join;
            if (address != null) _lanAddress = address;
            RememberLanName();
            if (!_demo.IsDemo) GameSettings.LanAddress = _lanAddress.Trim();
            _lobby.Join(_lanAddress, port, LanGameIdentity.Hello(_lanName));
        }

        /// <summary>Joins a game from the list.</summary>
        public void JoinLanGame(LanGameInfo game)
        {
            if (game == null) return;
            JoinLanGame(game.Address, game.Port);
        }

        /// <summary>Host: starts the match once a player has joined. Nothing happens before.</summary>
        public void StartLanGame()
        {
            if (_lobby == null || !_lobby.CanStart || _leaving != null) return;
            string opponent = _lobby.PeerName;
            LanMatchSettings settings = LanSettingsForHost();
            ILanChannel channel = _lobby.TakeChannel();
            var launch = new LanLaunch { Channel = channel, LocalTeam = Team.A, Settings = settings, OpponentName = opponent, Demo = _demo.IsDemo };
            GameSettings.Save();
            _launch = launch;
            Leave(() => GameFlow.StartLanMatch(launch));
        }

        /// <summary>The host's match: a new seed (or -lanDemoSeed) and the skirmish settings of the mode chosen (Faction mode: the rules as they are).</summary>
        public LanMatchSettings LanSettingsForHost()
        {
            int seed = _demo.Seed ?? (Environment.TickCount & 0x7FFFFFFF);
            MatchSetup setup = _lanMode == MatchMode.UnlockSkirmish ? new MatchSetup { Mode = MatchMode.UnlockSkirmish, BaseHealth = _lanBaseHealth, Cards = _lanCards } : null;
            return LanMatchSettings.From(seed, setup);
        }

        /// <summary>True while the join panel's button leaves a host (Leave) rather than closing the panel (Close).</summary>
        private bool JoinedOrJoining => _lobby != null && (_lobby.State == LanLobbyState.JoinConnecting || _lobby.State == LanLobbyState.JoinWaiting);

        /// <summary>
        /// Cancel: the host stops hosting and the host panel stays, with Host again; the guest leaves the host it joined (and stays on its
        /// list); otherwise (Close) the panel closes.
        /// </summary>
        public void CancelLan()
        {
            if (_lobby == null) return;
            _lanCardListOpen = false;
            if (_lobby.IsHosting || JoinedOrJoining)
            {
                _lobby.Cancel();
                return;
            }
            _lobby.Close();
            _lanPanel = LanPanel.None;
        }

        private void RememberLanName()
        {
            _lanName = LanIdentity.CleanName(_lanName);
            if (!_demo.IsDemo) GameSettings.LanPlayerName = _lanName;
        }

        /// <summary>One frame of the LAN page (Update calls it): the lobby moves on, and the guest follows the host into the match once the host's MatchStart is here.</summary>
        public void UpdateLan()
        {
            if (_lobby == null || _leaving != null) return;
            _lobby.Update();
            if (_lobby.State == LanLobbyState.GuestStarting)
            {
                string opponent = _lobby.PeerName;
                ILanChannel channel = _lobby.TakeChannel();
                var launch = new LanLaunch { Channel = channel, LocalTeam = Team.B, OpponentName = opponent, Demo = _demo.IsDemo };
                GameSettings.Save();
                _launch = launch;
                Leave(() => GameFlow.StartLanMatch(launch));
                return;
            }
            if (_demo.Role == LanDemoRole.Host && _lobby.CanStart) StartLanGame();
            else if (_demo.Role == LanDemoRole.Join && (_lobby.State == LanLobbyState.JoinFailed || _lobby.State == LanLobbyState.Browsing || _lobby.State == LanLobbyState.Idle))
            {
                // The host may not be there yet: try again every two seconds.
                if (_demoJoinAt < 0f) _demoJoinAt = Time.unscaledTime + (_lobby.State == LanLobbyState.JoinFailed ? 2f : 0f);
                if (Time.unscaledTime >= _demoJoinAt)
                {
                    _demoJoinAt = -1f;
                    JoinLanGame(_demo.Address);
                }
            }
        }

        /// <summary>-lanDemo: opens the page and hosts, or joins.</summary>
        private void StartLanDemo()
        {
            _demo = LanDemoArgs.Parse(Environment.GetCommandLineArgs());
            if (!_demo.IsDemo) return;
            OpenLanPage();
            _lanName = _demo.Role == LanDemoRole.Host ? "Demo host" : "Demo guest";
            _lanMode = MatchMode.Faction;
            if (_demo.Role == LanDemoRole.Host) HostLanGame();
            else JoinLanGame(_demo.Address);
        }

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

        /// <summary>From Play's page (or LAN Game's, which leaves any game hosted or joined) back to the first one.</summary>
        public void Back()
        {
            if (_page == Page.Lan) _lobby?.Close();
            _lanPanel = LanPanel.None;
            _lanCardListOpen = false;
            _page = Page.Main;
            _selectedMode = null;
            _cardListOpen = false;
        }

        /// <summary>What Esc does: one step back. Returns false if there was nothing to step back from.</summary>
        public bool HandleEscape()
        {
            if (_leaving != null) return false;
            if (StatsErrorsPending)
            {
                DismissStatsErrors();
                return true;
            }
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
            if (_lanCardListOpen)
            {
                _lanCardListOpen = false;
                return true;
            }
            if (_page == Page.Main) return false;
            Back();
            return true;
        }

        // The disclaimer holds the menu's music back until it is clicked away (the owner, D101). Awake comes before the scene's music is chosen.
        private void Awake()
        {
            if (Disclaimer.Pending) AudioDirector.HoldMenuMusic = true;
        }

        private void Start()
        {
            _shownAt = Time.unscaledTime;
            StartLanDemo();
        }

        private void OnDestroy() => _lobby?.Close(); // A game hosted or joined from here, but not started, is left.

        private void OnApplicationQuit() => _lobby?.Close();

        /// <summary>The disclaimer clicked away: a button's click, the menu's music, and the menu fading in from the black.</summary>
        private void PastDisclaimer()
        {
            Disclaimer.Pending = false;
            _shownAt = Time.unscaledTime;
            UiAudio.Click();
            if (AudioDirector.Instance != null) AudioDirector.Instance.ReleaseMenuMusic();
            else AudioDirector.HoldMenuMusic = false;
        }

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
            UpdateLan();
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
            PastDisclaimer();
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
                if (Disclaimer.Draw(width, height)) PastDisclaimer();
                return;
            }
            if (StatsErrorsPending)
            {
                DrawStatsErrors(width, height);
                DrawFade(width, height);
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
            else if (_page == Page.Lan) DrawLanPage(left, y);
            else DrawMainPage(left, y);
            GUI.enabled = true;

            // The game's version, small, in the lower left (the owner, D100): Unity's Player Settings version.
            GUI.Label(new Rect(16f, height - 30f, 300f, 22f), VersionLabel, MenuStyle.Small);
            string statsLine = StatsLine(UnitStatsMods.Status.State, UnitStatsMods.Status.ChangeCount);
            if (statsLine != null) GUI.Label(new Rect(16f, height - 52f, 600f, 22f), statsLine, MenuStyle.Small);
        }

        // ------------------------------------------------------------------ the unit stats files (mod_json)

        /// <summary>The most mistakes the panel lists; Player.log has them all.</summary>
        public const int StatsErrorsShown = 8;

        private static bool _statsErrorsDismissed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatsErrors() => _statsErrorsDismissed = false;

        /// <summary>True while the panel about mistakes in the unit stats files shows (once a launch, until OK or Esc).</summary>
        public static bool StatsErrorsPending => UnitStatsMods.Status.State == UnitStatsState.Errors && !_statsErrorsDismissed;

        /// <summary>Closes the panel about the unit stats files.</summary>
        public void DismissStatsErrors() => _statsErrorsDismissed = true;

        /// <summary>What the menu says above its version about the unit stats files, or null when the game plays its own values as shipped.</summary>
        public static string StatsLine(UnitStatsState state, int changes)
        {
            if (state == UnitStatsState.Modded) return $"Unit stats: modded ({changes} {(changes == 1 ? "change" : "changes")})";
            if (state == UnitStatsState.Errors) return "Unit stats: defaults (files have mistakes)";
            return null;
        }

        /// <summary>The panel's list: the mistakes, at most StatsErrorsShown of them, then how many more.</summary>
        public static string StatsErrorsText(IReadOnlyList<string> errors)
        {
            var text = new StringBuilder();
            int shown = Mathf.Min(StatsErrorsShown, errors.Count);
            for (int i = 0; i < shown; i++) text.Append("• ").Append(errors[i]).Append('\n');
            if (errors.Count > shown) text.Append($"...and {errors.Count - shown} more (see Player.log)\n");
            return text.ToString().TrimEnd('\n');
        }

        private void DrawStatsErrors(float width, float height)
        {
            MenuStyle.Fill(new Rect(0f, 0f, width, height), new Color(0f, 0f, 0f, 0.6f));
            UnitStatsLoadResult status = UnitStatsMods.Status;
            float panelWidth = Mathf.Min(1000f, width - 80f), inner = panelWidth - 64f;
            string list = StatsErrorsText(status.Errors);
            const string after = "The game is using its own values for every unit. Your files were not changed.";
            float listHeight = MenuStyle.Body.CalcHeight(new GUIContent(list), inner);
            float afterHeight = MenuStyle.Body.CalcHeight(new GUIContent(after), inner);
            float panelHeight = Mathf.Min(height - 40f, 32f + 44f + 12f + listHeight + 18f + afterHeight + 24f + StartHeight + 32f);
            var panel = new Rect((width - panelWidth) * 0.5f, (height - panelHeight) * 0.5f, panelWidth, panelHeight);
            MenuStyle.Panel(panel);

            float x = panel.x + 32f, y = panel.y + 32f;
            GUI.Label(new Rect(x, y, inner, 44f), "Unit stats files have mistakes", MenuStyle.Heading);
            y += 44f + 12f;
            GUI.Label(new Rect(x, y, inner, listHeight), list, MenuStyle.Body);
            y += listHeight + 18f;
            GUI.Label(new Rect(x, y, inner, afterHeight), after, MenuStyle.Body);

            float buttonsY = panel.yMax - 32f - StartHeight;
            GUI.enabled = _leaving == null;
            if (!string.IsNullOrEmpty(status.Folder) && MenuStyle.SmallFramedButton(new Rect(x, buttonsY, 220f, StartHeight), "Open folder", false))
                Application.OpenURL("file:///" + status.Folder.Replace('\\', '/'));
            if (MenuStyle.SmallFramedButton(new Rect(panel.xMax - 32f - 160f, buttonsY, 160f, StartHeight), "OK", true, "Unit stats OK")) DismissStatsErrors();
            GUI.enabled = true;
        }

        /// <summary>What the lower left of the menu says: the version set in Unity's Player Settings.</summary>
        public static string VersionLabel => "Version " + Application.version;

        private void DrawMainPage(float left, float y)
        {
            GUI.enabled = _leaving == null;
            if (ColumnButton(left, ref y, "Play")) OpenPlayPage();
            if (ColumnButton(left, ref y, LanGameLabel)) OpenLanPage();
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

        /// <summary>The main page's LAN item.</summary>
        public const string LanGameLabel = "LAN Game";

        private const float LanStatusHeight = 64f, LanGameRowHeight = 36f, LanGameRows = 3, LanJoinAtHeight = 28f;

        /// <summary>The host panel's line with this PC's addresses, while it waits for a player; "" otherwise.</summary>
        public string HostJoinAtText => _lobby != null && _lobby.State == LanLobbyState.HostWaiting ? _lobby.JoinAt : "";

        /// <summary>The join list's text while no game is listed (looking, then the firewall and address hint).</summary>
        public string NoGamesText => _lobby != null ? _lobby.NoGamesText : LanLobby.LookingText;

        private static GUIStyle _wrappedSmall, _wrappedFrom;

        /// <summary>The small text, wrapping from the top left (the LAN page's status), made again if the menu's styles were.</summary>
        private static GUIStyle WrappedSmall
        {
            get
            {
                if (_wrappedSmall == null || _wrappedFrom != MenuStyle.Small)
                {
                    _wrappedFrom = MenuStyle.Small;
                    _wrappedSmall = new GUIStyle(MenuStyle.Small) { wordWrap = true, alignment = TextAnchor.UpperLeft };
                }
                return _wrappedSmall;
            }
        }

        private void DrawLanPage(float left, float y)
        {
            // As on the Play page: the card list is drawn last and is the only thing that takes a click while open.
            bool enabled = _leaving == null;
            GUI.enabled = enabled && !_lanCardListOpen;

            // The second click of a double-click that closed the list would land on what lay under it (Start), so it is thrown away.
            if (Event.current.type == EventType.MouseDown && Time.unscaledTime < _ignorePressesUntil) Event.current.Use();

            float panelTop = y;
            if (ColumnButton(left, ref y, "Host game", _lanPanel == LanPanel.Host) && _lanPanel != LanPanel.Host) OpenHostPanel();
            if (ColumnButton(left, ref y, "Join game", _lanPanel == LanPanel.Join) && _lanPanel != LanPanel.Join) OpenJoinPanel();
            if (ColumnButton(left, ref y, "Back")) Back();

            float panelLeft = left + ButtonWidth + PanelGap;
            if (_lanPanel == LanPanel.Host) DrawHostPanel(panelLeft, panelTop, enabled);
            else if (_lanPanel == LanPanel.Join) DrawJoinPanel(panelLeft, panelTop);
            else DrawLanIntro(panelLeft, panelTop);
        }

        private static Rect LanPanelRect(float x, float y, float rowsHeight)
        {
            var panel = new Rect(x, y, PanelWidth, 84f + rowsHeight + LanStatusHeight + StartHeight + 2f * PanelMargin);
            MenuStyle.Panel(panel);
            return panel;
        }

        private void DrawLanIntro(float x, float y)
        {
            Rect panel = LanPanelRect(x, y, RowHeight);
            float row = PanelHeading(panel, "LAN GAME", "Play one other person on the same network.");
            DrawLanName(panel, row);
            GUI.Label(new Rect(panel.x + PanelMargin, row + RowHeight + 8f, panel.width - 2f * PanelMargin, LanStatusHeight),
                "One of you hosts, the other joins. Both need the same version of the game.", MenuStyle.Small);
        }

        private void DrawLanName(Rect panel, float row)
        {
            RowLabel(panel, row, "Your name");
            bool was = GUI.enabled;
            GUI.enabled = was && (_lobby == null || !_lobby.Busy);
            _lanName = MenuStyle.TextField(ControlRect(panel, row), _lanName, LanIdentity.NameLength, "LAN/name");
            GUI.enabled = was;
        }

        /// <summary>The host panel's line before Host is pressed.</summary>
        public const string PressHostText = "Set your name and the match, then press Host. The other player can join after that.";

        /// <summary>What the LAN panel's status box says now.</summary>
        public string LanStatusText
        {
            get
            {
                if (_lobby == null) return "";
                if (_lanPanel == LanPanel.Host && !_lobby.IsHosting && _lobby.State != LanLobbyState.Started) return PressHostText;
                return _lobby.Status;
            }
        }

        private void DrawLanStatus(Rect panel, float y)
        {
            var box = new Rect(panel.x + PanelMargin, y, panel.width - 2f * PanelMargin, LanStatusHeight);
            GUI.Label(box, LanStatusText, WrappedSmall);
        }

        private void DrawHostPanel(float x, float y, bool enabled)
        {
            Rect panel = LanPanelRect(x, y, 4f * RowHeight + LanJoinAtHeight);
            float row = PanelHeading(panel, "HOST A LAN GAME", "The other player joins from the same network.");
            DrawLanName(panel, row);
            bool canChange = _lobby != null && _lobby.State != LanLobbyState.Started; // the settings go with Start
            bool settingsOpen = GUI.enabled && canChange;
            row += RowHeight;
            RowLabel(panel, row, "Mode");
            bool was = GUI.enabled;
            GUI.enabled = settingsOpen;
            int mode = MenuStyle.Choice(ControlRect(panel, row), "LAN mode", new[] { "Faction", "Unlock Skirmish" }, _lanMode == MatchMode.UnlockSkirmish ? 1 : 0);
            _lanMode = mode == 1 ? MatchMode.UnlockSkirmish : MatchMode.Faction;
            row += RowHeight;
            bool skirmish = _lanMode == MatchMode.UnlockSkirmish;
            if (skirmish) _lanBaseHealth = BaseHealthControl(panel, row, _lanBaseHealth);
            else GUI.Label(new Rect(panel.x + PanelMargin, row, panel.width - 2f * PanelMargin, ControlHeight), "Each player's starting card picks a faction.", MenuStyle.Small);
            row += RowHeight;
            float cardsRow = row;
            if (skirmish) RowLabel(panel, row, "Cards");
            row += RowHeight;
            GUI.enabled = was;
            DrawLanStatus(panel, row);
            string joinAt = HostJoinAtText; // M11: what the other player types when the join list stays empty.
            if (joinAt.Length > 0) GUI.Label(new Rect(panel.x + PanelMargin, row + LanStatusHeight, panel.width - 2f * PanelMargin, LanJoinAtHeight), joinAt, WrappedSmall);

            var cancel = new Rect(panel.x + PanelMargin, panel.yMax - PanelMargin - StartHeight, 200f, StartHeight);
            bool hosting = _lobby != null && _lobby.IsHosting;
            if (MenuStyle.SmallFramedButton(cancel, hosting ? "Cancel" : "Host", !hosting, hosting ? "LAN/Cancel" : "LAN/Host"))
            {
                if (hosting) CancelLan();
                else HostLanGame();
            }
            var start = new Rect(panel.xMax - PanelMargin - 200f, panel.yMax - PanelMargin - StartHeight, 200f, StartHeight);
            GUI.enabled = was && _lobby != null && _lobby.CanStart;
            if (MenuStyle.SmallFramedButton(start, "Start", true, "LAN/Start")) StartLanGame();
            GUI.enabled = was;

            if (!skirmish) return;
            // Drawn last, with the page's own enabled (not this pass's, which is off while the list is open), so its items take a click.
            GUI.enabled = enabled && canChange;
            Rect box = ControlRect(panel, cardsRow);
            bool wasOpen = _lanCardListOpen;
            _lanCards = (CardFilter)MenuStyle.Dropdown(box, "LAN cards", CardLabels, (int)_lanCards, ref _lanCardListOpen);
            if (wasOpen && !_lanCardListOpen) _ignorePressesUntil = Time.unscaledTime + ClosedListGuardSeconds;
            GUI.enabled = was;
        }

        private void DrawJoinPanel(float x, float y)
        {
            float gamesHeight = LanGameRows * (LanGameRowHeight + 4f);
            Rect panel = LanPanelRect(x, y, 2f * RowHeight + gamesHeight + 12f);
            float row = PanelHeading(panel, "JOIN A LAN GAME", "Pick a game found on the network, or type its address.");
            DrawLanName(panel, row);
            row += RowHeight;

            RowLabel(panel, row, "Games");
            Rect list = ControlRect(panel, row);
            IReadOnlyList<LanGameInfo> games = _lobby != null ? _lobby.Games : (IReadOnlyList<LanGameInfo>)new LanGameInfo[0];
            bool canJoin = GUI.enabled && _lobby != null && _lobby.State != LanLobbyState.JoinConnecting && _lobby.State != LanLobbyState.JoinWaiting && _lobby.State != LanLobbyState.GuestStarting;
            bool was = GUI.enabled;
            if (games.Count == 0)
            {
                GUI.Label(new Rect(list.x, list.y, list.width, gamesHeight), NoGamesText, WrappedSmall);
            }
            for (int i = 0; i < games.Count && i < LanGameRows; i++)
            {
                LanGameInfo game = games[i];
                var line = new Rect(list.x, list.y + i * (LanGameRowHeight + 4f), list.width, LanGameRowHeight);
                GUI.enabled = canJoin;
                string label = game.Name + "   " + game.Address + (game.Version != Application.version ? "   (version " + game.Version + ")" : "");
                if (MenuStyle.SmallFramedButton(line, label, false, "LAN/game/" + i)) JoinLanGame(game);
                GUI.enabled = was;
            }
            row += gamesHeight + 12f;

            RowLabel(panel, row, "Address");
            Rect field = ControlRect(panel, row);
            GUI.enabled = canJoin;
            _lanAddress = MenuStyle.TextField(new Rect(field.x, field.y, field.width - 130f, field.height), _lanAddress, 64, "LAN/address");
            if (MenuStyle.SmallFramedButton(new Rect(field.xMax - 118f, field.y, 118f, field.height), "Join", true, "LAN/Join")) JoinLanGame();
            GUI.enabled = was;
            row += RowHeight;
            DrawLanStatus(panel, row);

            bool joined = JoinedOrJoining;
            var cancel = new Rect(panel.x + PanelMargin, panel.yMax - PanelMargin - StartHeight, 200f, StartHeight);
            if (MenuStyle.SmallFramedButton(cancel, joined ? "Leave" : "Close", false, "LAN/Cancel")) CancelLan();
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

        /// <summary>The Play page's base health, saved in the settings.</summary>
        private static void DrawBaseHealth(Rect panel, float y)
        {
            int health = GameSettings.SkirmishBaseHealth;
            int wanted = BaseHealthControl(panel, y, health);
            if (wanted != health) GameSettings.SkirmishBaseHealth = wanted;
        }

        /// <summary>A slider for the whole range, and arrows for one step at a time. Gives the base health wanted now.</summary>
        private static int BaseHealthControl(Rect panel, float y, int health)
        {
            RowLabel(panel, y, "Base health");
            Rect control = ControlRect(panel, y);

            float position = SliderFromBaseHealth(health);
            float moved = MenuStyle.Slider(new Rect(control.x, control.y, control.width - StepperWidth - 12f, control.height), position);
            int wanted = Mathf.Approximately(moved, position) ? health : BaseHealthFromSlider(moved);

            int step = MenuStyle.Stepper(new Rect(control.xMax - StepperWidth, control.y, StepperWidth, control.height), "Base health", wanted.ToString());
            wanted += step * MatchSetup.BaseHealthStep;
            return MatchSetup.SnapBaseHealth(wanted);
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
