// ScreenDemo.cs
// Test tool for test builds: checks Settings > Screen with nobody at the keyboard. It does nothing unless the game is started with "-screenDemo <screenshot folder>". It presses the row's
// own buttons (through MenuStyle.PressedByTest, the same path as a mouse click, so no real mouse moves), and writes "ScreenDemo: ..." lines in the player's log, ending with how many went wrong.
//   As it stands, the real moves: this PC's screens as Unity lists them, then the window goes to each screen in turn, windowed and fullscreen, logging Screen.mainWindowDisplayInfo, the
//   window's place and the sizes Unity lists before and after each move, and ends where it began. Add "-screenDemoEndOn <n>" to end on the n-th screen of the row instead, so that Unity
//   remembers that one for the next start. Only run it on a test build: this is the one tool that really moves its window, and fullscreen covers each screen for a moment.
//   With "-screenDemoFake", the row's looks: made-up lists of screens (very long names, four screens, one screen, none) at every UI scale and at several window sizes, measuring that the
//   row's buttons and text fit. Nothing is moved.
// As in any test run, the settings stay in memory (GameSettings), so the choice it makes is not saved.
// Depends on: GameDisplays, GameFlow, GameSettings, MenuStyle, SettingsScreen, Halobellum.Core (DisplayChoice), UnityEngine.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Halobellum.Core;
using Halobellum.Presentation;
using UnityEngine;

namespace Halobellum.UI
{
    public sealed class ScreenDemo : MonoBehaviour
    {
        private const string FakeFlag = "-screenDemoFake";
        private const string EndOnFlag = "-screenDemoEndOn";

        private static readonly float[] UiScales = { 0.75f, 1f, 1.25f };
        private static readonly (int w, int h)[] WindowSizes = { (1600, 900), (1920, 1080), (1280, 1024), (1024, 768), (800, 500) };

        private string _folder;
        private int _problems;
        private readonly Dictionary<string, Rect> _drawn = new Dictionary<string, Rect>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfAsked()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, GameFlow.ScreenDemoFlag);
            if (index < 0) return;
            var host = new GameObject("Screen Demo");
            DontDestroyOnLoad(host);
            var demo = host.AddComponent<ScreenDemo>();
            demo._folder = index + 1 < args.Length ? args[index + 1] : "ScreenShots";
            Directory.CreateDirectory(demo._folder);
        }

        private IEnumerator Start()
        {
            yield return Wait(2f);
            MenuStyle.ButtonDrawnForTest += (key, rect) => _drawn[key] = rect;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), FakeFlag) >= 0) yield return RowLooks();
            else yield return RealMoves();
            Debug.Log($"ScreenDemo finished: {_problems} problems.");
            Application.Quit();
        }

        // ------------------------------------------------------------------ the real moves

        private IEnumerator RealMoves()
        {
            var raw = new List<DisplayInfo>();
            Screen.GetDisplayLayout(raw);
            for (int i = 0; i < raw.Count; i++)
            {
                DisplayInfo d = raw[i];
                Debug.Log($"ScreenDemo: Unity's list, place {i}: {d.name} {d.width}x{d.height}, {d.refreshRate.value:0.##} Hz, work area {d.workArea}.");
            }
            GameDisplays.WindowsMainScreenSize(out int mainWidth, out int mainHeight);
            List<DisplayOption> displays = GameDisplays.List();
            Debug.Log($"ScreenDemo: Windows says its main screen is {mainWidth}x{mainHeight}, so the main one is {(displays.Count > 0 ? GameDisplays.Describe(displays[DisplayChoice.MainIndex(displays)]) : "none")}.");
            for (int i = 0; i < displays.Count; i++) Debug.Log($"ScreenDemo: the row, {i + 1}: {DisplayChoice.Label(displays, i)}");
            LogWhere("as it started");
            int startedOn = GameDisplays.CurrentIn(displays);
            Check("the window's screen is one of the list", startedOn >= 0);

            yield return Press("Settings");
            yield return Wait(0.5f);
            yield return Shot("settings_as_started");

            for (int pass = 0; pass < 2; pass++)
            {
                string mode = pass == 0 ? "windowed" : "fullscreen";
                if (pass == 1 || Screen.fullScreenMode != FullScreenMode.Windowed)
                {
                    yield return Press(pass == 0 ? "Window/Windowed" : "Window/Fullscreen");
                    yield return Wait(1.2f);
                    LogWhere("after changing to " + mode);
                }
                if (pass == 1)
                {
                    // At a size of its own, which it should keep on any screen that can show it; a screen's own size is what follows from screen to screen below.
                    GameSettings.SetWindow(1280, 720, true);
                    yield return Wait(1.2f);
                    yield return PressAndArrive("Screen/>");
                    LogWhere("fullscreen at 1280x720, after a move");
                    Check("fullscreen at a chosen size keeps it", Screen.width == 1280 && Screen.height == 720 && Screen.fullScreenMode == FullScreenMode.FullScreenWindow);
                    int here = GameDisplays.CurrentIn(displays);
                    GameSettings.SetWindow(displays[here].Width, displays[here].Height, true);
                    yield return Wait(1.2f);
                    LogWhere("fullscreen at the screen's own size");
                }
                for (int step = 0; step < displays.Count; step++)
                {
                    int from = GameDisplays.CurrentIn(displays);
                    int to = (from + 1) % displays.Count;
                    LogWhere($"{mode}, before going to {displays[to].Name}");
                    yield return PressAndArrive("Screen/>");
                    LogWhere($"{mode}, after going to {displays[to].Name}");
                    Check($"{mode}: the window is on {displays[to].Name}", GameDisplays.CurrentIn(displays) == to);
                    if (pass == 0) Check($"{mode}: its size fits {displays[to].Name}", Screen.width <= displays[to].Width && Screen.height <= displays[to].Height);
                    else Check($"{mode}: it takes the size of {displays[to].Name}", Screen.width == displays[to].Width && Screen.height == displays[to].Height);
                    Check($"{mode}: the saved choice is {displays[to].Name}, in memory", DisplayChoice.FindSaved(displays, GameSettings.ChosenScreen) == to);
                    string[] sizes = Screen.resolutions.Select(r => $"{r.width}x{r.height}").Distinct().ToArray();
                    Debug.Log($"ScreenDemo: {mode}: Unity lists {sizes.Length} sizes on {displays[to].Name}: {string.Join(", ", sizes)}.");
                    yield return Shot($"{mode}_{to + 1}_{Safe(displays[to].Name)}");
                }
            }

            yield return Press("Window/Windowed");
            yield return Wait(1.2f);
            int endOn = EndOnArgument(displays.Count, startedOn);
            int safety = displays.Count;
            while (GameDisplays.CurrentIn(displays) != endOn && safety-- > 0) yield return PressAndArrive("Screen/>");
            LogWhere("at the end");
            Check("it ended on the screen it should", GameDisplays.CurrentIn(displays) == endOn);
            yield return Press("Back");
            yield return Wait(0.3f);
        }

        private static int EndOnArgument(int count, int fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, EndOnFlag);
            return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int n) && n >= 1 && n <= count ? n - 1 : fallback;
        }

        private IEnumerator PressAndArrive(string key)
        {
            int moves = GameDisplays.MovesFinished;
            float pressedAt = Time.realtimeSinceStartup;
            yield return Press(key);
            float limit = pressedAt + 10f;
            while (GameDisplays.MovesFinished == moves && Time.realtimeSinceStartup < limit) yield return null;
            Check($"the move of {key} finished ({Time.realtimeSinceStartup - pressedAt:0.00} s)", GameDisplays.MovesFinished != moves);
            yield return Wait(1f);
        }

        private static void LogWhere(string when)
        {
            DisplayInfo info = Screen.mainWindowDisplayInfo;
            Debug.Log($"ScreenDemo: {when}: on {info.name} {info.width}x{info.height} (work area {info.workArea}), window at {Screen.mainWindowPosition}, {Screen.width}x{Screen.height} {Screen.fullScreenMode}.");
        }

        // ------------------------------------------------------------------ how the row looks

        private IEnumerator RowLooks()
        {
            var lists = new (string name, List<DisplayOption> list)[]
            {
                ("three", DisplayChoice.MarkMain(new[]
                {
                    new DisplayOption("LG ULTRAWIDE", 2560, 1080, 2560, 1032, 0),
                    new DisplayOption("DELL S3222HG", 1920, 1080, 1920, 1032, 1),
                    new DisplayOption("HP E242", 1920, 1200, 1920, 1152, 2),
                }, 1920, 1080)),
                ("longnames", DisplayChoice.MarkMain(new[]
                {
                    new DisplayOption("Generic PnP Monitor With A Very Long Name From Its Maker, Model 34WQHD-Extra", 3840, 2160, 3840, 2112, 0),
                    new DisplayOption("Generic PnP Monitor With A Very Long Name From Its Maker, Model 34WQHD-Extra", 3840, 2160, 3840, 2112, 1),
                    new DisplayOption("", 1280, 1024, 1280, 976, 2),
                    new DisplayOption("HP E242", 1920, 1200, 1920, 1152, 3),
                }, 3840, 2160)),
                ("one", DisplayChoice.MarkMain(new[] { new DisplayOption("Generic PnP Monitor With A Very Long Name From Its Maker", 1920, 1080, 1920, 1032, 0) }, 1920, 1080)),
                ("none", new List<DisplayOption>()),
            };

            foreach ((int w, int h) in WindowSizes)
            {
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                yield return Wait(0.8f);
                foreach (float ui in UiScales)
                {
                    GameSettings.UseUiScaleForThisRun(ui);
                    foreach ((string name, List<DisplayOption> list) in lists)
                    {
                        if (name == "none" && w != 1600) continue;
                        GameDisplays.FakeForTest = list;
                        yield return Press("Settings");
                        _drawn.Clear();
                        yield return Wait(0.3f);
                        CheckFits($"{w}x{h} ui {ui * 100f:0}% {name}", list.Count);
                        // Go through the whole list: it must come round to where it began.
                        if (list.Count >= 2)
                        {
                            int began = GameDisplays.CurrentIn(GameDisplays.List());
                            yield return Press("Screen/>");
                            yield return Wait(0.1f);
                            Check($"{name}: > goes to the next", GameDisplays.CurrentIn(GameDisplays.List()) == (began + 1) % list.Count);
                            yield return Press("Screen/<");
                            yield return Press("Screen/<");
                            yield return Wait(0.1f);
                            Check($"{name}: < comes round the other way", GameDisplays.CurrentIn(GameDisplays.List()) == (began + list.Count - 1) % list.Count);
                        }
                        yield return Shot($"{w}x{h}_ui{ui * 100f:0}_{name}");
                        yield return Press("Back");
                        yield return Wait(0.1f);
                    }
                }
            }
            GameDisplays.FakeForTest = null;
        }

        private void CheckFits(string where, int screens)
        {
            var window = new Rect(0f, 0f, Screen.width, Screen.height);
            bool Inside(Rect r) => r.xMin >= -0.5f && r.yMin >= -0.5f && r.xMax <= window.xMax + 0.5f && r.yMax <= window.yMax + 0.5f;
            bool drawn = _drawn.TryGetValue("Back", out Rect back) & _drawn.TryGetValue("Reset to defaults", out Rect reset) & _drawn.TryGetValue("Window/Windowed", out Rect windowRow);
            Check($"{where}: the panel's buttons are drawn", drawn);
            Check($"{where}: Back and Reset are inside the window", Inside(back) && Inside(reset));
            Check($"{where}: the row's text fits (widest {SettingsScreen.WidestScreenLabel:0} of {SettingsScreen.ScreenLabelRoom:0} units)", SettingsScreen.WidestScreenLabel <= SettingsScreen.ScreenLabelRoom);
            if (screens < 2)
            {
                Check($"{where}: with {screens} screen(s) the row has no arrows", !_drawn.ContainsKey("Screen/<") && !_drawn.ContainsKey("Screen/>"));
                return;
            }
            bool arrows = _drawn.TryGetValue("Screen/<", out Rect left) & _drawn.TryGetValue("Screen/>", out Rect right);
            Check($"{where}: the row's arrows are drawn", arrows);
            Check($"{where}: the row is inside the window, and above the next row", Inside(left) && Inside(right) && right.yMax <= windowRow.yMin + 0.5f);
        }

        // ------------------------------------------------------------------ helpers

        private void Check(string what, bool ok)
        {
            Debug.Log($"ScreenDemo: {(ok ? "ok" : "PROBLEM")}: {what}");
            if (!ok) _problems++;
        }

        private static string Safe(string name) => string.Concat((name ?? "").Where(char.IsLetterOrDigit));

        private static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

        private IEnumerator Press(string label)
        {
            MenuStyle.PressedByTest = label;
            float limit = Time.realtimeSinceStartup + 3f;
            while (MenuStyle.PressedByTest == label && Time.realtimeSinceStartup < limit) yield return null;
            if (MenuStyle.PressedByTest == label)
            {
                MenuStyle.PressedByTest = null;
                Check("found a button called " + label, false);
            }
            yield return null;
        }

        private IEnumerator Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"));
            yield return null;
            yield return null;
            yield return Wait(0.15f);
        }
    }
}
