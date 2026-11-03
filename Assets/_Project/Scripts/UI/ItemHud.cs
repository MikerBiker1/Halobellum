// ItemHud.cs
// The item UI shared by the match and the sandbox (decisions D33 and D35): hexagon icons, a grid of
// items to pick from, and the slots over squads (the item a squad carries, or an empty hexagon while
// an item is selected). Items are dragged from the grid onto a hexagon: pressing an item selects it
// and shows the hexagons, releasing over one places it, releasing elsewhere cancels. A plain click
// also works: the item stays selected until a hexagon is clicked. Where hexagons overlap, the one
// whose centre is nearest the mouse is used. Drawn with Unity's immediate-mode GUI, in the look of
// the current theme.
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
    /// <summary>The state of one press on an item: what its release means.</summary>
    public sealed class ItemDrag
    {
        public enum Outcome
        {
            /// <summary>No item was being pressed.</summary>
            None,
            /// <summary>Dragged onto a hexagon: place the item there.</summary>
            Place,
            /// <summary>A plain click: the item stays selected, waiting for a click on a hexagon.</summary>
            KeepSelected,
            /// <summary>Dropped elsewhere, or clicked again while selected: deselect it.</summary>
            Cancel,
        }

        /// <summary>Moving the mouse less than this between press and release counts as a click.</summary>
        public const float ClickDistance = 8f;

        private Vector2 _start;
        private bool _wasSelected;

        public ItemStats Item { get; private set; }
        public bool IsDragging => Item != null;

        public void Press(ItemStats item, Vector2 position, bool alreadySelected)
        {
            Item = item;
            _start = position;
            _wasSelected = alreadySelected;
        }

        public Outcome Release(Vector2 position, bool overHex)
        {
            if (Item == null) return Outcome.None;
            Item = null;
            bool moved = Vector2.Distance(position, _start) > ClickDistance;
            if (!moved) return _wasSelected ? Outcome.Cancel : Outcome.KeepSelected;
            return overHex ? Outcome.Place : Outcome.Cancel;
        }
    }

    public static class ItemHud
    {
        private const float GridIcon = 56f, GridGap = 6f;
        private const int GridPerRow = 5;

        private static readonly ItemDrag Drag = new ItemDrag();

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
        /// A grid of item icons at (x, y). Pressing one starts dragging it and calls onSelect. Returns the
        /// text of the item under the mouse, or null.
        /// </summary>
        public static string DrawItemGrid(float x, float y, IReadOnlyList<ItemStats> items, ItemStats selected, Func<ItemStats, Texture2D> iconFor, Action<ItemStats> onSelect)
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
                DrawHexIcon(rect, iconFor(item));
                Event current = Event.current;
                if (rect.Contains(current.mousePosition))
                {
                    hovered = Describe(item);
                    if (current.type == EventType.MouseDown && current.button == 0)
                    {
                        Drag.Press(item, current.mousePosition, selected == item);
                        UiAudio.Click();
                        if (selected != item) onSelect(item);
                        current.Use();
                    }
                }
            }
            return hovered;
        }

        public static string Describe(ItemStats item) => $"{item.Name}: {item.EffectsText()}";

        /// <summary>
        /// On each squad: the item it carries, or (while an item is selected) an empty hexagon. Also ends a
        /// drag or a click: onPlace when the mouse is released on a hexagon, onCancel when an item is
        /// dropped elsewhere. onScreen gives a squad's screen position in pixels (origin bottom-left),
        /// false if it isn't visible. Call it before the panels.
        /// </summary>
        public static void DrawSquadSlots(IEnumerable<PlacedSquad> squads, ItemStats placing, float scale, OnScreen onScreen, Func<ItemStats, Texture2D> iconFor,
            Action<PlacedSquad> onPlace, Action onCancel)
        {
            Event current = Event.current;
            PlacedSquad nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (PlacedSquad squad in squads)
            {
                if (squad.Item == null && placing == null) continue;
                if (!onScreen(squad, out Vector3 screen)) continue;
                // Screen pixels (origin bottom-left) to HUD coordinates (origin top-left, scaled).
                var centre = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
                const float size = 40f;
                var rect = new Rect(centre.x - size * 0.5f, centre.y + 2f, size, size); // On the squad: the level-up arrow stands right above its top.
                if (squad.Item != null)
                {
                    DrawHexIcon(rect, iconFor(squad.Item));
                    continue;
                }
                UiHitAreas.Add(HudScale.ToScreen(rect, scale));
                UiThemeDefinition theme = HudTheme.Current;
                if (theme != null && theme.hexEmpty != null) GUI.DrawTexture(rect, theme.hexEmpty, ScaleMode.StretchToFill);
                if (rect.Contains(current.mousePosition))
                {
                    float distance = Vector2.Distance(rect.center, current.mousePosition);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = squad;
                    }
                }
            }

            if (current.type != EventType.MouseUp || current.button != 0) return;
            if (Drag.IsDragging)
            {
                switch (Drag.Release(current.mousePosition, nearest != null))
                {
                    case ItemDrag.Outcome.Place: onPlace(nearest); break;
                    case ItemDrag.Outcome.Cancel: onCancel(); break;
                }
                current.Use();
            }
            else if (placing != null && nearest != null)
            {
                onPlace(nearest); // Selected by a click earlier; now a click on a hexagon.
                current.Use();
            }
        }

        /// <summary>The item being dragged, under the mouse. Call it last, so it is on top of everything.</summary>
        public static void DrawDragged(Func<ItemStats, Texture2D> iconFor)
        {
            if (!Drag.IsDragging || Event.current.type != EventType.Repaint) return;
            const float size = 56f;
            Vector2 mouse = Event.current.mousePosition;
            DrawHexIcon(new Rect(mouse.x - size * 0.5f, mouse.y - size * 0.5f, size, size), iconFor(Drag.Item));
        }

        public delegate bool OnScreen(PlacedSquad squad, out Vector3 screen);
    }
}
