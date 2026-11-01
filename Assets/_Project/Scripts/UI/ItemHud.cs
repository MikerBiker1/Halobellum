// ItemHud.cs
// The item UI shared by the match and the sandbox (decision D33): hexagon icons, a grid of items to
// pick from, and the slots over squads (the item a squad carries, or an empty hexagon to click while
// an item is selected). Drawn with Unity's immediate-mode GUI, in the look of the current theme.
// Depends on: HudTheme, HudScale, Halobellum.Presentation (UiHitAreas), Halobellum.Core (ItemStats),
//             Halobellum.Economy (PlacedSquad), Halobellum.Units (UiThemeDefinition), UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.Units;
using UnityEngine;

namespace Halobellum.UI
{
    public static class ItemHud
    {
        private const float GridIcon = 56f, GridGap = 6f;
        private const int GridPerRow = 5;

        /// <summary>A hexagon with an icon on it; returns true if clicked when clickable.</summary>
        public static bool DrawHexIcon(Rect rect, Texture2D icon, bool clickable = false)
        {
            UiThemeDefinition theme = HudTheme.Current;
            if (theme != null && theme.hexFilled != null) GUI.DrawTexture(rect, theme.hexFilled, ScaleMode.StretchToFill);
            if (icon != null) GUI.DrawTexture(new Rect(rect.x + rect.width * 0.2f, rect.y + rect.height * 0.2f, rect.width * 0.6f, rect.height * 0.6f), icon, ScaleMode.ScaleToFit);
            return clickable && GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        /// <summary>Height of the item grid for this many items.</summary>
        public static float GridHeight(int count) => Mathf.CeilToInt(count / (float)GridPerRow) * (GridIcon + GridGap);

        /// <summary>
        /// A grid of item icons at (x, y). Clicking one calls onClick. Returns the text of the item under
        /// the mouse, or null.
        /// </summary>
        public static string DrawItemGrid(float x, float y, IReadOnlyList<ItemStats> items, ItemStats selected, Func<ItemStats, Texture2D> iconFor, Action<ItemStats> onClick)
        {
            string hovered = null;
            for (int i = 0; i < items.Count; i++)
            {
                ItemStats item = items[i];
                var rect = new Rect(x + (i % GridPerRow) * (GridIcon + GridGap), y + (i / GridPerRow) * (GridIcon + GridGap), GridIcon, GridIcon);
                if (selected == item)
                {
                    Color old = GUI.color;
                    GUI.color = HudTheme.Accent;
                    UiThemeDefinition theme = HudTheme.Current;
                    GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), theme != null && theme.hexEmpty != null ? theme.hexEmpty : Texture2D.whiteTexture, ScaleMode.StretchToFill);
                    GUI.color = old;
                }
                if (DrawHexIcon(rect, iconFor(item), clickable: true)) onClick(item);
                if (rect.Contains(Event.current.mousePosition)) hovered = Describe(item);
            }
            return hovered;
        }

        public static string Describe(ItemStats item) => $"{item.Name}: {item.EffectsText()}";

        /// <summary>
        /// Over each squad: the item it carries, or (while an item is selected) an empty hexagon to click.
        /// onScreen gives a squad's screen position in pixels (origin bottom-left), false if it isn't visible.
        /// </summary>
        public static void DrawSquadSlots(IEnumerable<PlacedSquad> squads, ItemStats placing, float scale, OnScreen onScreen, Func<ItemStats, Texture2D> iconFor, Action<PlacedSquad> onPlace)
        {
            foreach (PlacedSquad squad in squads)
            {
                if (squad.Item == null && placing == null) continue;
                if (!onScreen(squad, out Vector3 screen)) continue;
                // Screen pixels (origin bottom-left) to HUD coordinates (origin top-left, scaled).
                var centre = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
                const float size = 40f;
                var rect = new Rect(centre.x - size * 0.5f, centre.y - size - 6f, size, size);
                if (squad.Item != null)
                {
                    DrawHexIcon(rect, iconFor(squad.Item));
                    continue;
                }
                UiHitAreas.Add(HudScale.ToScreen(rect, scale));
                UiThemeDefinition theme = HudTheme.Current;
                if (theme != null && theme.hexEmpty != null) GUI.DrawTexture(rect, theme.hexEmpty, ScaleMode.StretchToFill);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) onPlace(squad);
            }
        }

        public delegate bool OnScreen(PlacedSquad squad, out Vector3 screen);
    }
}
