// WindowSlot.cs
// -windowSlot 0|1 (M11 Stage 4): a window half the screen wide, slot 0 on the left and slot 1 on the right, so two copies of the game
// can play a LAN match side by side on one PC. Set as the first scene loads; the move waits a few frames, until the window has its
// new size. Nothing happens in the editor or without the flag. GameFlow.CommandLineSetsWindow keeps the saved window from undoing it.
// Depends on: Halobellum.Net (LanDemoArgs), UnityEngine.

using System;
using System.Collections;
using Halobellum.Net;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class WindowSlot : MonoBehaviour
    {
        private int _slot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyAtStart()
        {
            if (Application.isEditor) return;
            int slot = LanDemoArgs.Parse(Environment.GetCommandLineArgs()).WindowSlot;
            if (slot < 0) return;
            var host = new GameObject("Window Slot");
            DontDestroyOnLoad(host);
            host.AddComponent<WindowSlot>()._slot = slot;
        }

        private IEnumerator Start()
        {
            DisplayInfo display = Screen.mainWindowDisplayInfo;
            int screenWidth = display.width > 0 ? display.width : Screen.currentResolution.width;
            int screenHeight = display.height > 0 ? display.height : Screen.currentResolution.height;
            LanDemoArgs.SlotRect(_slot, screenWidth, screenHeight, out int x, out int y, out int width, out int height);
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            for (int i = 0; i < 3; i++) yield return null;
            Screen.MoveMainWindowTo(display, new Vector2Int(x, y));
            Debug.Log($"WindowSlot: slot {_slot}, {width}x{height} at {x},{y} on a {screenWidth}x{screenHeight} screen.");
            Destroy(gameObject);
        }
    }
}
