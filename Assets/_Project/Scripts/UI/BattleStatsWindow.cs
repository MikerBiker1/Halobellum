// BattleStatsWindow.cs
// The Battle stats window at the end of a game (D69): a tab for each battle of the match. Each shows the field from straight
// above as that battle started (a picture MatchController took), with an icon where each squad, tower and spell stood, and
// beside it the player's army on the left and the enemy's on the right, sorted by damage dealt or by damage received. A row
// shows what it dealt (and its kills), what it took, and what its defenses stopped; how many of its bodies lived.
// Clicking an icon on the map picks out its row (and scrolls to it), and clicking a row picks out its icon.
// "Took" is what got through to shield and health; "stopped" is what armor, the shield type and defense arcs kept off, shots
// blocked included (BattleStats says how it is counted).
// The window keeps its own state; MatchHud draws it over the match-over screen while it is open.
// Depends on: Halobellum.Economy (Match, BattleStats), Halobellum.Core, HudTheme, UnitIcons, UiHitAreas (through the panel callback), UnityEngine.

using System;
using System.Collections.Generic;
using System.Globalization;
using Halobellum.Core;
using Halobellum.Economy;
using UnityEngine;

namespace Halobellum.UI
{
    public sealed class BattleStatsWindow
    {
        private const float RowHeight = 78f, IconSize = 58f, MapIcon = 30f, MapIconPicked = 46f, Gap = 16f, TabHeight = 34f;
        private static readonly Color DealtColor = new Color(1f, 0.66f, 0.2f), TakenColor = new Color(0.92f, 0.25f, 0.2f), StoppedColor = new Color(0.62f, 0.66f, 0.72f);

        public bool IsOpen { get; private set; }
        /// <summary>The battle shown, in Match.BattleHistory's order.</summary>
        public int Tab { get; private set; }
        /// <summary>Sorted by damage received (taken and stopped) instead of damage dealt.</summary>
        public bool ByReceived;
        /// <summary>The line picked out on the map and in its list, or null.</summary>
        public BattleStatsLine Picked { get; private set; }

        private Vector2 _leftScroll, _rightScroll;
        private float _scrollLeftTo = -1f, _scrollRightTo = -1f;
        private GUIStyle _title, _header, _name, _nameSmall, _note, _line, _dim, _tab, _tabOn, _tip;
        private int _styleVersion = -1;

        /// <summary>Opens on the last battle.</summary>
        public void Open(Match match)
        {
            IsOpen = true;
            ShowTab(match != null ? match.BattleHistory.Count - 1 : 0);
        }

        public void Close() => IsOpen = false;

        public void ShowTab(int tab)
        {
            Tab = Mathf.Max(0, tab);
            Picked = null;
            _leftScroll = _rightScroll = Vector2.zero;
        }

        /// <summary>Picks out a line, and scrolls its list to it.</summary>
        public void Pick(BattleStats stats, BattleStatsLine line)
        {
            Picked = line;
            if (line == null || stats == null) return;
            int index = stats.Side(line.Team, ByReceived).IndexOf(line);
            if (line.Team == Team.A) _scrollLeftTo = index * RowHeight;
            else _scrollRightTo = index * RowHeight;
        }

        /// <summary>The map's rect for this field inside a box, as large as fits.</summary>
        public static Rect MapRect(Rect box, float fieldWidth, float fieldLength)
        {
            float aspect = fieldLength > 0f ? fieldWidth / fieldLength : 1f;
            float width = Mathf.Min(box.width, box.height * aspect), height = width / aspect;
            return new Rect(box.x + (box.width - width) * 0.5f, box.y + (box.height - height) * 0.5f, width, height);
        }

        /// <summary>Where a spot on the field is on the map: the player's edge at the bottom, the enemy's at the top.</summary>
        public static Vector2 OnMap(Rect map, BattleStats stats, Vec2 spot)
        {
            float u = Mathf.Clamp01((spot.X + stats.FieldWidth * 0.5f) / stats.FieldWidth);
            float v = Mathf.Clamp01((spot.Y + stats.FieldLength * 0.5f) / stats.FieldLength);
            return new Vector2(map.x + u * map.width, map.y + (1f - v) * map.height);
        }

        /// <summary>The line whose icon is under this point of the map (the nearest, if icons overlap), or null.</summary>
        public static BattleStatsLine LineAt(Rect map, BattleStats stats, Vector2 point)
        {
            BattleStatsLine best = null;
            float bestDistance = MapIcon * 0.7f;
            foreach (BattleStatsLine line in stats.Lines)
            {
                float distance = Vector2.Distance(OnMap(map, stats, line.Start), point);
                if (distance <= bestDistance)
                {
                    best = line;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>Draws the window in a screen of this virtual size. panel draws a panel's box and keeps clicks off the field.</summary>
        public void Draw(float screenWidth, float screenHeight, Match match, Func<int, Texture2D> pictureOf, Func<SpellStats, Texture2D> spellIcon,
            Color playerColor, Color enemyColor, Action<Rect> panel)
        {
            if (!IsOpen || match == null) return;
            EnsureStyles();
            IReadOnlyList<BattleStats> history = match.BattleHistory;
            var window = new Rect(20f, 20f, screenWidth - 40f, screenHeight - 40f);
            if (window.width > 1600f) window = new Rect((screenWidth - 1600f) * 0.5f, window.y, 1600f, window.height);
            if (window.height > 1040f) window = new Rect(window.x, (screenHeight - 1040f) * 0.5f, window.width, 1040f);
            panel(window);

            GUI.Label(new Rect(window.x + 22f, window.y + 12f, 400f, 40f), HudTheme.Title("Battle stats"), _title);
            if (GUI.Button(new Rect(window.xMax - 142f, window.y + 14f, 120f, 36f), "Close")) Close();
            float sortX = window.xMax - 142f - 16f - 2f * 160f;
            GUI.Label(new Rect(sortX - 70f, window.y + 14f, 66f, 36f), "Sort by", _dim);
            if (GUI.Button(new Rect(sortX, window.y + 14f, 156f, 36f), "Damage dealt", ByReceived ? _tab : _tabOn) && ByReceived) ByReceived = false;
            if (GUI.Button(new Rect(sortX + 160f, window.y + 14f, 156f, 36f), "Damage received", ByReceived ? _tabOn : _tab) && !ByReceived) ByReceived = true;

            if (history.Count == 0)
            {
                GUI.Label(new Rect(window.x + 22f, window.y + 70f, window.width - 44f, 30f), "No battle was fought.", _line);
                return;
            }
            if (Tab >= history.Count) ShowTab(history.Count - 1);

            // A tab per battle, on as many rows as they need.
            float x = window.x + 22f, y = window.y + 62f, tabWidth = 104f;
            for (int i = 0; i < history.Count; i++)
            {
                if (x + tabWidth > window.xMax - 22f)
                {
                    x = window.x + 22f;
                    y += TabHeight + 4f;
                }
                if (GUI.Button(new Rect(x, y, tabWidth - 4f, TabHeight), $"Round {history[i].Round}", i == Tab ? _tabOn : _tab) && i != Tab) ShowTab(i);
                x += tabWidth;
            }
            y += TabHeight + 12f;

            BattleStats stats = history[Tab];
            var content = new Rect(window.x + 22f, y, window.width - 44f, window.yMax - 18f - y);
            float listWidth = Mathf.Clamp((content.width - 2f * Gap) * 0.24f, 300f, 400f);
            var mapBox = new Rect(content.x + listWidth + Gap, content.y, content.width - 2f * (listWidth + Gap), content.height - 30f);
            Rect map = MapRect(mapBox, stats.FieldWidth, stats.FieldLength);

            float most = 1f, mostReceived = 1f;
            foreach (BattleStatsLine line in stats.Lines)
            {
                most = Mathf.Max(most, line.DamageDealt);
                mostReceived = Mathf.Max(mostReceived, line.DamageReceived);
            }

            DrawSide(new Rect(content.x, content.y, listWidth, content.height), stats, Team.A, "Your army", playerColor, match.Player.Faction?.Name, spellIcon, most, mostReceived, ref _leftScroll, ref _scrollLeftTo);
            DrawSide(new Rect(content.xMax - listWidth, content.y, listWidth, content.height), stats, Team.B, "Enemy army", enemyColor, match.Enemy.Faction?.Name, spellIcon, most, mostReceived, ref _rightScroll, ref _scrollRightTo);
            DrawMap(map, stats, pictureOf(Tab), match, spellIcon, playerColor, enemyColor);
            GUI.Label(new Rect(mapBox.x, mapBox.yMax + 2f, mapBox.width, 28f),
                stats.Finished ? "Took: what got through to shield and health.  Stopped: what armor, shield type and defense arcs kept off." : "This battle hasn't ended.", _dim);
        }

        private void DrawSide(Rect area, BattleStats stats, Team team, string title, Color color, string faction, Func<SpellStats, Texture2D> spellIcon,
            float most, float mostReceived, ref Vector2 scroll, ref float scrollTo)
        {
            HudTheme.RowBox(new Rect(area.x, area.y, area.width, 34f));
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(area.x, area.y, 6f, 34f), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(area.x + 14f, area.y, area.width - 20f, 34f), HudTheme.Title(title), _header);

            List<BattleStatsLine> lines = stats.Side(team, ByReceived);
            var view = new Rect(area.x, area.y + 40f, area.width, area.height - 40f);
            if (lines.Count == 0)
            {
                GUI.Label(new Rect(view.x + 8f, view.y, view.width - 16f, 30f), "Nothing in this battle.", _dim);
                return;
            }
            float inner = lines.Count * RowHeight > view.height ? view.width - 18f : view.width;
            if (scrollTo >= 0f)
            {
                scroll.y = Mathf.Clamp(scrollTo - view.height * 0.5f + RowHeight * 0.5f, 0f, Mathf.Max(0f, lines.Count * RowHeight - view.height));
                scrollTo = -1f;
            }
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, inner, lines.Count * RowHeight));
            for (int i = 0; i < lines.Count; i++)
            {
                var row = new Rect(0f, i * RowHeight, inner, RowHeight - 4f);
                if (row.yMax < scroll.y || row.y > scroll.y + view.height) continue;
                if (DrawRow(row, lines[i], color, faction, spellIcon, most, mostReceived)) Picked = Picked == lines[i] ? null : lines[i];
            }
            GUI.EndScrollView();
        }

        /// <summary>One row. Returns true if it was clicked.</summary>
        private bool DrawRow(Rect row, BattleStatsLine line, Color color, string faction, Func<SpellStats, Texture2D> spellIcon, float most, float mostReceived)
        {
            bool picked = line == Picked;
            HudTheme.RowBox(row);
            Color old = GUI.color;
            if (picked)
            {
                GUI.color = new Color(HudTheme.Accent.r, HudTheme.Accent.g, HudTheme.Accent.b, 0.28f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                GUI.color = HudTheme.Accent;
                Outline(row, 2f);
            }
            GUI.color = old;

            var icon = new Rect(row.x + 8f, row.y + (row.height - IconSize) * 0.5f, IconSize, IconSize);
            DrawIcon(icon, line, color, faction, spellIcon, picked);
            float textX = icon.xMax + 10f, textWidth = row.xMax - 8f - textX;
            string note = Note(line), title = Title(line);
            float noteWidth = note.Length > 0 ? _note.CalcSize(new GUIContent(note)).x : 0f;
            GUI.Label(new Rect(row.xMax - 8f - noteWidth, row.y + 2f, noteWidth, 22f), note, _note);
            float nameWidth = textWidth - noteWidth - 8f;
            GUI.Label(new Rect(textX, row.y + 2f, nameWidth, 22f), title, _name.CalcSize(new GUIContent(title)).x > nameWidth ? _nameSmall : _name);

            // Each number over its bar: dealt, then taken and stopped.
            string kills = line.Kills == 1 ? "1 kill" : $"{line.Kills} kills";
            GUI.Label(new Rect(textX, row.y + 23f, textWidth, 17f), line.Kind == BattleStatsKind.Spell && line.DamageDealt <= 0f ? "No damage" : $"Dealt {Number(line.DamageDealt)}, {kills}", _line);
            var dealtBar = new Rect(textX, row.y + 40f, textWidth, 6f);
            Fill(dealtBar, new Color(0f, 0f, 0f, 0.45f));
            Fill(new Rect(dealtBar.x, dealtBar.y, dealtBar.width * Mathf.Clamp01(line.DamageDealt / most), dealtBar.height), DealtColor);

            if (line.Kind != BattleStatsKind.Spell)
            {
                GUI.Label(new Rect(textX, row.y + 48f, textWidth, 17f), $"Took {Number(line.DamageTaken)}, stopped {Number(line.DamageStopped)}", _line);
                var receivedBar = new Rect(textX, row.y + 65f, textWidth, 6f);
                Fill(receivedBar, new Color(0f, 0f, 0f, 0.45f));
                float taken = receivedBar.width * Mathf.Clamp01(line.DamageTaken / mostReceived);
                float stopped = receivedBar.width * Mathf.Clamp01(line.DamageStopped / mostReceived);
                Fill(new Rect(receivedBar.x, receivedBar.y, taken, receivedBar.height), TakenColor);
                Fill(new Rect(receivedBar.x + taken, receivedBar.y, Mathf.Min(stopped, receivedBar.width - taken), receivedBar.height), StoppedColor);
            }
            return GUI.Button(row, GUIContent.none, GUIStyle.none);
        }

        private void DrawMap(Rect map, BattleStats stats, Texture2D picture, Match match, Func<SpellStats, Texture2D> spellIcon, Color playerColor, Color enemyColor)
        {
            Color old = GUI.color;
            if (picture != null) GUI.DrawTexture(map, picture, ScaleMode.StretchToFill);
            else
            {
                // No picture (a run that can't render): the field and its halves.
                Fill(map, new Color(0.13f, 0.15f, 0.12f));
                Fill(new Rect(map.x, map.center.y - 1f, map.width, 2f), new Color(1f, 1f, 1f, 0.25f));
            }
            GUI.color = HudTheme.Accent;
            Outline(map, 2f);
            // The flank zones (D61): a strip each side, beyond the field's edge; each side's squads flank beside the other's area.
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector2 near = OnMap(map, stats, new Vec2(sign * stats.FlankZoneFrom, 0f)), far = OnMap(map, stats, new Vec2(sign * stats.FlankZoneTo, 0f));
                Outline(new Rect(Mathf.Min(near.x, far.x), map.y + 2f, Mathf.Abs(far.x - near.x), map.height - 4f), 1f);
            }
            GUI.color = old;

            Event current = Event.current;
            BattleStatsLine hovered = map.Contains(current.mousePosition) ? LineAt(map, stats, current.mousePosition) : null;
            if (current.type == EventType.MouseDown && current.button == 0 && map.Contains(current.mousePosition))
            {
                Pick(stats, hovered == Picked ? null : hovered);
                current.Use();
            }

            // Spells first, then towers and squads, and the picked one on top of all.
            foreach (BattleStatsKind kind in new[] { BattleStatsKind.Spell, BattleStatsKind.Tower, BattleStatsKind.Squad })
            {
                foreach (BattleStatsLine line in stats.Lines)
                {
                    if (line.Kind != kind || line == Picked) continue;
                    DrawMapIcon(map, stats, line, line == hovered ? MapIcon * 1.2f : MapIcon, match, spellIcon, playerColor, enemyColor, false);
                }
            }
            if (Picked != null && stats.Lines.Contains(Picked)) DrawMapIcon(map, stats, Picked, MapIconPicked, match, spellIcon, playerColor, enemyColor, true);

            if (hovered != null)
            {
                Vector2 spot = OnMap(map, stats, hovered.Start);
                string text = Title(hovered) + (hovered.ArrivesLate ? ": flanked, and came late" : "");
                float width = _tip.CalcSize(new GUIContent(text)).x + 16f;
                var tip = new Rect(Mathf.Clamp(spot.x - width * 0.5f, map.x, map.xMax - width), spot.y - MapIcon - 26f, width, 24f);
                if (tip.y < map.y) tip.y = spot.y + MapIcon * 0.7f;
                Fill(tip, new Color(0f, 0f, 0f, 0.8f));
                GUI.Label(tip, text, _tip);
            }
        }

        private void DrawMapIcon(Rect map, BattleStats stats, BattleStatsLine line, float size, Match match, Func<SpellStats, Texture2D> spellIcon, Color playerColor, Color enemyColor, bool picked)
        {
            Vector2 spot = OnMap(map, stats, line.Start);
            var rect = new Rect(spot.x - size * 0.5f, spot.y - size * 0.5f, size, size);
            Color color = line.Team == Team.A ? playerColor : enemyColor;
            string faction = (line.Team == Team.A ? match.Player : match.Enemy).Faction?.Name;
            DrawIcon(rect, line, color, faction, spellIcon, picked);
        }

        /// <summary>An icon in its side's colour; a spell's has a thinner edge, and a squad that arrives late (from a flank zone) is dimmed.</summary>
        private static void DrawIcon(Rect rect, BattleStatsLine line, Color color, string faction, Func<SpellStats, Texture2D> spellIcon, bool picked)
        {
            Color old = GUI.color;
            float edge = line.Kind == BattleStatsKind.Spell ? 1.5f : Mathf.Max(2f, rect.width * 0.07f);
            if (picked)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f), Texture2D.whiteTexture);
            }
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            var inner = new Rect(rect.x + edge, rect.y + edge, rect.width - 2f * edge, rect.height - 2f * edge);
            GUI.color = new Color(0.05f, 0.06f, 0.08f);
            GUI.DrawTexture(inner, Texture2D.whiteTexture);
            Texture2D picture = line.Kind == BattleStatsKind.Spell ? spellIcon?.Invoke(line.Spell) ?? SpellEmblem(line.Spell) : UnitIcons.For(line.Type, faction);
            GUI.color = line.ArrivesLate ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            if (picture != null) GUI.DrawTexture(inner, picture, ScaleMode.ScaleToFit);
            else if (line.Kind == BattleStatsKind.Spell)
            {
                GUI.color = color;
                GUI.DrawTexture(new Rect(inner.center.x - inner.width * 0.2f, inner.center.y - inner.height * 0.2f, inner.width * 0.4f, inner.height * 0.4f), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }

        private static readonly Dictionary<string, Texture2D> SpellEmblems = new Dictionary<string, Texture2D>();

        /// <summary>The big picture its card shows (spell cards have no icon of their own).</summary>
        private static Texture2D SpellEmblem(SpellStats spell)
        {
            if (spell == null) return null;
            if (!SpellEmblems.TryGetValue(spell.Name, out Texture2D emblem))
            {
                emblem = CardArt.EmblemFor(CardStats.FromSpell(spell), null);
                SpellEmblems[spell.Name] = emblem;
            }
            return emblem;
        }

        private static string Title(BattleStatsLine line)
        {
            switch (line.Kind)
            {
                case BattleStatsKind.Spell: return $"{line.Name} (spell)";
                case BattleStatsKind.Tower: return $"{line.Name}, level {line.Level}";
                default: return line.Level > 1 ? $"{line.Name}, level {line.Level}" : line.Name;
            }
        }

        private static string Note(BattleStatsLine line)
        {
            if (line.Kind == BattleStatsKind.Spell) return "";
            if (line.Kind == BattleStatsKind.Tower) return line.Survivors > 0 ? "Stood" : "Fell";
            return $"{line.Survivors}/{line.Bodies} lived";
        }

        // No thousands separator: "Dealt 1,234, 3 kills" read as a list (the owner, 2026-11-06).
        private static string Number(float value) => Mathf.Round(value).ToString("0", CultureInfo.InvariantCulture);

        private static void Fill(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>A frame in GUI.color.</summary>
        private static void Outline(Rect rect, float width)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - width, rect.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, width, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - width, rect.y, width, rect.height), Texture2D.whiteTexture);
        }

        private void EnsureStyles()
        {
            if (_title != null && _styleVersion == HudTheme.Version) return;
            _styleVersion = HudTheme.Version;
            _title = new GUIStyle(GUI.skin.label) { font = HudTheme.TitleFont, fontSize = HudTheme.TitleSize(30), fontStyle = HudTheme.TitleFontStyle, alignment = TextAnchor.MiddleLeft, normal = { textColor = HudTheme.TitleColor } };
            _header = new GUIStyle(_title) { fontSize = HudTheme.TitleSize(18) };
            _name = new GUIStyle(GUI.skin.label) { fontSize = HudTheme.Size(16), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip, normal = { textColor = HudTheme.Text } };
            _nameSmall = new GUIStyle(_name) { fontSize = HudTheme.Size(13) };
            _line = new GUIStyle(GUI.skin.label) { fontSize = HudTheme.Size(13), alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip, normal = { textColor = HudTheme.Text } };
            _dim = new GUIStyle(_line) { fontSize = HudTheme.Size(14), normal = { textColor = HudTheme.DimText } };
            _note = new GUIStyle(_dim) { alignment = TextAnchor.MiddleRight };
            _tab = new GUIStyle(GUI.skin.button) { fontSize = HudTheme.Size(14) };
            _tabOn = new GUIStyle(_tab) { fontStyle = FontStyle.Bold, normal = { textColor = HudTheme.Accent, background = _tab.active.background }, hover = { textColor = HudTheme.Accent, background = _tab.active.background } };
            _tip = new GUIStyle(_line) { fontSize = HudTheme.Size(14), alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            HudTheme.Fit(_title, _header, _name, _nameSmall, _note, _line, _dim, _tab, _tabOn, _tip);
        }
    }
}
