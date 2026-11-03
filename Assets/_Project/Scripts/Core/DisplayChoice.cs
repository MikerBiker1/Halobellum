// DisplayChoice.cs
// Which of the PC's screens the game goes on (Settings > Screen). Plain C#, so that tests can choose from lists of made-up screens; GameDisplays gets the real list from Unity.
//   - Unity doesn't say which screen is Windows's main one: on Windows every work area it gives starts at 0,0, wherever the screen is. So GameDisplays asks Windows for the main screen's
//     size, and the main one is the first of that size in Unity's list. Failing that, it is the first in Unity's list, which was the main one on the one PC this was tried on.
//   - A choice is saved as "name|width|height|place": the order of the list can change from one start to the next, and two screens can be the same model. On reading, the same name, size
//     and place comes first, then the same name and size, then the same name at another size, since a screen's resolution can change. A saved screen that is gone means the main one.
//     So does no choice at all, which is the first start.
//   - The list shows the main screen first, then the others by name.
//   - The size the game takes on a screen: a window stays inside the work area, leaving room for its title bar; fullscreen takes the screen's own size if it had the old screen's.
// Depends on: nothing.

using System;
using System.Collections.Generic;

namespace Halobellum.Core
{
    /// <summary>One of the PC's screens. LayoutIndex is its place in the list it came from.</summary>
    public readonly struct DisplayOption
    {
        public readonly string Name;
        public readonly int Width, Height;
        public readonly int WorkWidth, WorkHeight;
        public readonly int LayoutIndex;
        public readonly bool IsMain;

        public DisplayOption(string name, int width, int height, int workWidth, int workHeight, int layoutIndex = 0, bool isMain = false)
        {
            Name = name;
            Width = width;
            Height = height;
            WorkWidth = workWidth;
            WorkHeight = workHeight;
            LayoutIndex = layoutIndex;
            IsMain = isMain;
        }

        public DisplayOption AsMain() => new DisplayOption(Name, Width, Height, WorkWidth, WorkHeight, LayoutIndex, true);
    }

    public static class DisplayChoice
    {
        /// <summary>What a window's title bar and frame take from a screen's height, in pixels.</summary>
        public const int TitleBarRoom = 40;

        private const char Separator = '|';

        /// <summary>The list with its main screen marked: the first of the size Windows gives for it, or the first of all if there is no such size (0 x 0: Windows wasn't asked).</summary>
        public static List<DisplayOption> MarkMain(IReadOnlyList<DisplayOption> displays, int mainWidth, int mainHeight)
        {
            var marked = new List<DisplayOption>(displays);
            if (marked.Count == 0) return marked;
            int main = marked.FindIndex(d => d.Width == mainWidth && d.Height == mainHeight);
            if (main < 0) main = 0;
            marked[main] = marked[main].AsMain();
            return marked;
        }

        /// <summary>The place of the main screen in this list, or -1 if the list is empty.</summary>
        public static int MainIndex(IReadOnlyList<DisplayOption> displays)
        {
            for (int i = 0; i < displays.Count; i++) if (displays[i].IsMain) return i;
            return displays.Count > 0 ? 0 : -1;
        }

        /// <summary>The list as the Settings row shows it: the main screen, then the others by name. The same whatever order they came in.</summary>
        public static List<DisplayOption> Ordered(IReadOnlyList<DisplayOption> displays)
        {
            var ordered = new List<DisplayOption>(displays);
            ordered.Sort((a, b) =>
            {
                if (a.IsMain != b.IsMain) return a.IsMain ? -1 : 1;
                int names = string.CompareOrdinal(a.Name ?? "", b.Name ?? "");
                return names != 0 ? names : a.LayoutIndex.CompareTo(b.LayoutIndex);
            });
            return ordered;
        }

        /// <summary>The form a choice is saved in.</summary>
        public static string SavedForm(DisplayOption display) =>
            string.Join(Separator.ToString(), display.Name ?? "", display.Width, display.Height, display.LayoutIndex);

        /// <summary>The place in this list of the screen that was saved, or -1 if it is gone or the text means nothing.</summary>
        public static int FindSaved(IReadOnlyList<DisplayOption> displays, string saved)
        {
            if (string.IsNullOrEmpty(saved)) return -1;
            // Numbers from the end: a screen's name could have the separator in it.
            string[] parts = saved.Split(Separator);
            if (parts.Length < 4) return -1;
            if (!int.TryParse(parts[parts.Length - 3], out int width) || !int.TryParse(parts[parts.Length - 2], out int height)) return -1;
            if (!int.TryParse(parts[parts.Length - 1], out int place)) return -1;
            string name = string.Join(Separator.ToString(), parts, 0, parts.Length - 3);

            int sameSize = -1, sameName = -1;
            for (int i = 0; i < displays.Count; i++)
            {
                DisplayOption d = displays[i];
                if ((d.Name ?? "") != name) continue;
                if (sameName < 0) sameName = i;
                if (d.Width != width || d.Height != height) continue;
                if (d.LayoutIndex == place) return i;
                if (sameSize < 0) sameSize = i;
            }
            if (sameSize >= 0) return sameSize;
            return string.IsNullOrEmpty(name) ? -1 : sameName; // A screen with no name is only known by its size.
        }

        /// <summary>The screen to be on: the saved one if it is here, otherwise the main one (never chosen, or gone). -1 if the list is empty.</summary>
        public static int Choose(IReadOnlyList<DisplayOption> displays, string saved)
        {
            int found = FindSaved(displays, saved);
            return found >= 0 ? found : MainIndex(displays);
        }

        /// <summary>What the Settings row says for a screen, with a name of at most nameLimit letters. A second screen of the same model and size gets a number.</summary>
        public static string Label(IReadOnlyList<DisplayOption> displays, int index, int nameLimit = int.MaxValue)
        {
            DisplayOption d = displays[index];
            string name = string.IsNullOrWhiteSpace(d.Name) ? $"Screen {index + 1}" : d.Name.Trim();
            if (name.Length > nameLimit) name = name.Substring(0, Math.Max(1, nameLimit - 3)).TrimEnd() + "...";
            int sameBefore = 0, same = 0;
            for (int i = 0; i < displays.Count; i++)
            {
                if ((displays[i].Name ?? "") != (d.Name ?? "") || displays[i].Width != d.Width || displays[i].Height != d.Height) continue;
                same++;
                if (i < index) sameBefore++;
            }
            string number = same > 1 ? $" #{sameBefore + 1}" : "";
            string main = d.IsMain ? "  (main)" : "";
            return $"{name}{number}  {d.Width} x {d.Height}{main}";
        }

        /// <summary>The size the game takes on this screen. wasOldScreensOwnSize: it was fullscreen at the size of the screen it comes from.</summary>
        public static (int width, int height) SizeOn(DisplayOption display, int width, int height, bool fullscreen, bool wasOldScreensOwnSize)
        {
            if (fullscreen)
                return wasOldScreensOwnSize || width > display.Width || height > display.Height ? (display.Width, display.Height) : (width, height);
            int mostWidth = display.WorkWidth > 0 ? display.WorkWidth : display.Width;
            int mostHeight = (display.WorkHeight > 0 ? display.WorkHeight : display.Height) - TitleBarRoom;
            return (Math.Min(width, mostWidth), Math.Min(height, mostHeight));
        }

        /// <summary>Where a window with this much inside its frame goes, from the screen's top left corner: in the middle of the work area, with its title bar on the screen.</summary>
        public static (int x, int y) WindowPlace(DisplayOption display, int width, int height)
        {
            int workHeight = display.WorkHeight > 0 ? display.WorkHeight : display.Height;
            return (Math.Max(0, (display.Width - width) / 2), Math.Max(0, (workHeight - height - TitleBarRoom) / 2));
        }
    }
}
