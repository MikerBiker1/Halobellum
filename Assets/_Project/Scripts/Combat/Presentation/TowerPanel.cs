// TowerPanel.cs
// What the window of a tower shows (D55), worked out by MatchController.DescribeTower and drawn by MatchHud: lines of text, and
// what the tower sells.
// Depends on: nothing (plain C#).

using System;
using System.Collections.Generic;

namespace Halobellum.Presentation
{
    public sealed class TowerButton
    {
        public string Label;
        public string Note;
        public bool Enabled;
        public Action OnPress;
    }

    public sealed class TowerPanel
    {
        public string Title;
        public string Side;
        public readonly List<string> Lines = new List<string>();
        public readonly List<TowerButton> Buttons = new List<TowerButton>();
    }
}
