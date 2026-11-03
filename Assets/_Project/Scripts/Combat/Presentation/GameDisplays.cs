// GameDisplays.cs
// Puts the game on the player's screen (Settings > Screen; DisplayChoice has the rules). Screen.GetDisplayLayout lists the screens and Screen.MoveMainWindowTo moves the window. The move
// takes some frames, so a change of the window's size waits for it. The Settings row and the test tool (-screenDemo) both go through here.
// When the game starts (not in a test run: see GameSettings) it goes to the saved screen, or to Windows's main screen if none was ever chosen, so a start needs no setting to be right.
// Unity remembers a monitor of its own by its place in Unity's list (UnitySelectMonitor) and opens the window there before any game code runs, so a move at start is seen for a moment.
// A test tool can give a made-up list (FakeForTest): choosing from it moves nothing. In the editor the list is empty, so that pressing Play never moves anything.
// Unity doesn't say which screen is the main one, so this asks Windows for its size (read only: it sends nothing to any window).
// Depends on: GameSettings, Halobellum.Core (DisplayChoice), UnityEngine, user32.dll on Windows.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class GameDisplays
    {
        /// <summary>A move that has not finished after this long no longer holds up the Settings row.</summary>
        private const float MoveGivesUpSeconds = 5f;

        private static readonly List<DisplayInfo> Layout = new List<DisplayInfo>();
        private static List<DisplayOption> _fake;
        private static int _fakeCurrentPlace;
        private static float _moveStarted = -1f;

        /// <summary>A test tool's list, instead of the PC's. Choosing from it moves nothing.</summary>
        public static List<DisplayOption> FakeForTest
        {
            get => _fake;
            set
            {
                _fake = value;
                _fakeCurrentPlace = value != null && value.Count > 0 ? value[DisplayChoice.MainIndex(value)].LayoutIndex : -1;
                MovesFinished++;
            }
        }

        /// <summary>Goes up each time a move has ended: what was listed for the old screen, such as its window sizes, is out of date.</summary>
        public static int MovesFinished { get; private set; }

        /// <summary>True while a move is under way.</summary>
        public static bool Moving => _moveStarted >= 0f && Time.realtimeSinceStartup - _moveStarted < MoveGivesUpSeconds;

        /// <summary>The PC's screens as Settings shows them: the main one first.</summary>
        public static List<DisplayOption> List()
        {
            if (_fake != null) return DisplayChoice.Ordered(_fake);
            Layout.Clear();
            if (Application.isEditor) return new List<DisplayOption>();
            Screen.GetDisplayLayout(Layout);
            var options = new List<DisplayOption>(Layout.Count);
            for (int i = 0; i < Layout.Count; i++) options.Add(ToOption(Layout[i], i));
            WindowsMainScreenSize(out int mainWidth, out int mainHeight);
            return DisplayChoice.Ordered(DisplayChoice.MarkMain(options, mainWidth, mainHeight));
        }

        /// <summary>The place in this list of the screen the game's window is on, or -1 if it can't be told.</summary>
        public static int CurrentIn(List<DisplayOption> displays)
        {
            int place = _fakeCurrentPlace;
            if (_fake == null)
            {
                if (Layout.Count == 0) List();
                if (Layout.Count == 0) return -1;
                place = Layout.IndexOf(Screen.mainWindowDisplayInfo);
            }
            for (int i = 0; i < displays.Count; i++) if (displays[i].LayoutIndex == place) return i;
            return -1;
        }

        /// <summary>The player chose this screen: it is remembered, and the game goes there at once.</summary>
        public static void ChooseByPlayer(List<DisplayOption> displays, int index)
        {
            DisplayOption target = displays[index];
            GameSettings.ChosenScreen = DisplayChoice.SavedForm(target);
            if (_fake != null)
            {
                _fakeCurrentPlace = target.LayoutIndex;
                MovesFinished++;
                return;
            }
            int current = CurrentIn(displays);
            FullScreenMode mode = Screen.fullScreenMode;
            bool fullscreen = mode != FullScreenMode.Windowed;
            bool ownSize = fullscreen && current >= 0 && Screen.width == displays[current].Width && Screen.height == displays[current].Height;
            (int width, int height) = DisplayChoice.SizeOn(target, Screen.width, Screen.height, fullscreen, ownSize);
            MoveTo(target, width, height, mode, () =>
            {
                if (width != Screen.width || height != Screen.height) GameSettings.SetWindow(width, height, fullscreen);
            });
        }

        /// <summary>When the game starts: the screen it should be on, with the window at this size if it fits there. needsSetting: the player's saved size or mode has to be applied, moved or not.</summary>
        public static void GoToChosenAtStart(string saved, int width, int height, FullScreenMode mode, bool needsSetting)
        {
            List<DisplayOption> displays = List();
            int target = DisplayChoice.Choose(displays, saved);
            if (target < 0)
            {
                if (needsSetting) Screen.SetResolution(width, height, mode);
                return;
            }
            int current = CurrentIn(displays);
            bool fullscreen = mode != FullScreenMode.Windowed;
            bool ownSize = fullscreen && current >= 0 && current != target && width == displays[current].Width && height == displays[current].Height;
            (int fitWidth, int fitHeight) = DisplayChoice.SizeOn(displays[target], width, height, fullscreen, ownSize);
            string why = DisplayChoice.FindSaved(displays, saved) >= 0 ? "the saved one" : string.IsNullOrEmpty(saved) ? "the main one, as none was ever chosen" : "the main one, as the saved one is gone";
            string from = current >= 0 ? Describe(displays[current]) : "a screen not in the list";
            Debug.Log($"GameDisplays: at start the window is on {from}. The screen to be on is {Describe(displays[target])}, {why}. Window {fitWidth}x{fitHeight} {mode}.");

            if (target == current && fitWidth == Screen.width && fitHeight == Screen.height)
            {
                if (needsSetting) Screen.SetResolution(fitWidth, fitHeight, mode);
                return;
            }
            MoveTo(displays[target], fitWidth, fitHeight, mode, () =>
            {
                if (fitWidth != Screen.width || fitHeight != Screen.height || mode != Screen.fullScreenMode) Screen.SetResolution(fitWidth, fitHeight, mode);
                Debug.Log($"GameDisplays: at start the window went to {Screen.mainWindowDisplayInfo.name}, at {Screen.mainWindowPosition}, {fitWidth}x{fitHeight}.");
            });
        }

        /// <summary>A screen in words, for logs.</summary>
        public static string Describe(DisplayOption d) => $"{d.Name} {d.Width}x{d.Height} (work area {d.WorkWidth}x{d.WorkHeight}, place {d.LayoutIndex} in Unity's list)";

        public static DisplayOption ToOption(DisplayInfo info, int layoutIndex) =>
            new DisplayOption(info.name, info.width, info.height, info.workArea.width, info.workArea.height, layoutIndex);

        /// <summary>The size Windows gives for its main screen, or 0 x 0 if it can't be asked.</summary>
        public static void WindowsMainScreenSize(out int width, out int height)
        {
            width = height = 0;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (!GetMonitorInfo(MonitorFromPoint(new PointXY(), MonitorDefaultToPrimary), ref info)) return;
                width = info.MonitorRight - info.MonitorLeft;
                height = info.MonitorBottom - info.MonitorTop;
            }
            catch (Exception e)
            {
                Debug.LogWarning("GameDisplays: could not ask Windows for its main screen: " + e.Message);
            }
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const uint MonitorDefaultToPrimary = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct PointXY
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public int MonitorLeft, MonitorTop, MonitorRight, MonitorBottom;
            public int WorkLeft, WorkTop, WorkRight, WorkBottom;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(PointXY point, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
#endif

        private static void MoveTo(DisplayOption target, int width, int height, FullScreenMode mode, Action arrived)
        {
            if (target.LayoutIndex >= Layout.Count) return;
            (int x, int y) = mode == FullScreenMode.Windowed ? DisplayChoice.WindowPlace(target, width, height) : (0, 0);
            var place = new Vector2Int(x, y);
            DisplayInfo info = Layout[target.LayoutIndex];
            bool resizes = mode == FullScreenMode.Windowed && (width != Screen.width || height != Screen.height);
            _moveStarted = Time.realtimeSinceStartup;
            AsyncOperation move = Screen.MoveMainWindowTo(in info, place);
            move.completed += _ =>
            {
                _moveStarted = -1f;
                arrived();
                if (resizes) AfterSomeFrames(() => Screen.MoveMainWindowTo(in info, place));
                MovesFinished++;
            };
        }

        /// <summary>A window that shrinks does not stay where it was put (24 pixels lower on the PC tried, its bottom under the taskbar), and the change of size only comes some frames later.</summary>
        private static void AfterSomeFrames(Action action)
        {
            int frames = 5;
            void Tick()
            {
                if (--frames > 0) return;
                Application.onBeforeRender -= Tick;
                action();
            }
            Application.onBeforeRender += Tick;
        }
    }
}
