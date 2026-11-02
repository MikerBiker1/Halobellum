// CardScreens.cs
// Draws one card of the card screens (M10): a draft card, a spell, item, economy, buff or training card, and a
// reinforcement card. Each has its kind's frame and ribbon (CardArt) and a big picture (UnitIcons, or CardArt's emblems).
// MatchHud lays the cards out (CardLayout) and handles what a click does. The text shrinks to fit its place on a narrow
// card, down to a size still legible: Fit counts anything that still didn't fit, and the demo (CardScreensDemo) reports it.
// Drawn with Unity's immediate-mode GUI. These screens aren't on during a battle, so they may allocate a little.
// Depends on: CardArt, UnitIcons, HudTheme, Halobellum.Core, Halobellum.Economy, UnityEngine.

using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using UnityEngine;

namespace Halobellum.UI
{
    public static class CardScreens
    {
        /// <summary>How many labels could not fit their place, even at the smallest size, since this was last reset (for the demo).</summary>
        public static int TextThatDidNotFit;
        /// <summary>The last label that didn't fit (for the demo).</summary>
        public static string LastTextThatDidNotFit;
        /// <summary>Where the demo pretends the mouse is (HUD units), to take a picture of a hovered card. Null in the game.</summary>
        public static Vector2? DemoMouse;
        /// <summary>A sub-faction text for the demo, to check that it fits. Null in the game.</summary>
        public static string DemoSubFactionText;

        private const float ButtonHeight = 42f;
        /// <summary>A draft card's squads go side by side with their text from this width of contents up.</summary>
        private const float WideEnoughForSideBySide = 300f;

        private const float PairGap = 6f;
        /// <summary>The most a draft card gives to the text about its sub-faction.</summary>
        private const float BlurbHeight = 92f;

        private static readonly GUIContent Content = new GUIContent();
        private static GUIStyle _wrap, _line, _title, _ribbon, _button;
        private static int _styleVersion = -1;

        // ------------------------------------------------------------------ the cards

        /// <summary>
        /// A starting draft card: its faction's frame, the sub-faction on a big banner, what is special about it, and its
        /// squads. `description` is the sub-faction's own text, if it has one. Returns true when chosen.
        /// </summary>
        public static bool DrawDraftCard(Rect card, DraftCard draft, string description = null)
        {
            EnsureStyles();
            string faction = draft.Faction.Name;
            CardFrameKind kind = CardArt.FactionFrame(faction);
            Color accent = CardArt.Accent(kind);
            CardArt.DrawCard(card, kind, Hovered(card));
            Rect area = CardArt.ContentArea(card);
            float y = area.y + 2f;

            // The faction, with its emblem.
            float headerHeight = Mathf.Clamp(area.width * 0.17f, 34f, 50f);
            Texture2D emblem = CardArt.FactionEmblem(faction);
            if (emblem != null) DrawTinted(new Rect(area.x, y, headerHeight, headerHeight), emblem, accent);
            float textX = area.x + headerHeight + 8f;
            Fit(new Rect(textX, y, area.xMax - textX, headerHeight), faction, _title, 30, 16, TextAnchor.MiddleLeft, HudTheme.TitleColor, shadow: true);
            y += headerHeight + 6f;

            // The sub-faction: the thing to read first.
            float bannerHeight = Mathf.Clamp(area.width * 0.2f, 46f, 62f);
            var banner = new Rect(card.x + 8f, y, card.width - 16f, bannerHeight);
            CardArt.DrawRibbon(banner, kind);
            float bannerText = Mathf.Max(48f, bannerHeight * 0.8f);
            Fit(new Rect(banner.x + bannerText * 0.5f, banner.y, banner.width - bannerText, banner.height), draft.SubFaction.Name, _ribbon, 26, 14, TextAnchor.MiddleCenter, Color.white, shadow: true);
            y = banner.yMax + 4f;

            // What is special about it: its text and its own units. Standard cards have neither, and the squads get the room.
            float bottom = area.yMax - ButtonHeight - 6f;
            description = string.IsNullOrEmpty(description) ? DemoSubFactionText : description;
            string exclusive = draft.SubFaction.ExclusiveUnits.Count > 0
                ? "Exclusive: " + string.Join(", ", draft.SubFaction.ExclusiveUnits.ConvertAll(u => u.Name))
                : null;
            if (!string.IsNullOrEmpty(description) || exclusive != null)
            {
                float height = Mathf.Min(area.width < WideEnoughForSideBySide * 0.75f ? BlurbHeight * 1.3f : BlurbHeight, (bottom - y) * 0.3f);
                FitPair(new Rect(area.x, y, area.width, height), exclusive, 15, description, 14, TextAnchor.MiddleCenter);
                y += height + 2f;
            }

            Fit(new Rect(area.x, y, area.width, 22f), "Free starting squads", _line, 15, 12, TextAnchor.MiddleCenter, HudTheme.DimText);
            y += 24f;

            DrawSquads(new Rect(area.x, y, area.width, bottom - y), draft, faction, accent);

            return Button(new Rect(area.x, area.yMax - ButtonHeight, area.width, ButtonHeight), "Choose", true);
        }

        /// <summary>The squads of a draft card, one above the other.</summary>
        private static void DrawSquads(Rect area, DraftCard draft, string faction, Color accent)
        {
            List<UnitStats> squads = draft.Squads;
            int count = squads.Count;
            if (count == 0) return;
            const float gap = 4f;
            float height = (area.height - (count - 1) * gap) / count;
            for (int i = 0; i < count; i++)
            {
                DrawSquadCell(new Rect(area.x, area.y + i * (height + gap), area.width, height), squads[i], draft.SubFaction.ExclusiveUnits.Contains(squads[i]), faction, accent);
            }
        }

        /// <summary>One squad: on a wide card its picture beside its text, on a narrow one above it.</summary>
        private static void DrawSquadCell(Rect cell, UnitStats unit, bool exclusive, string faction, Color accent)
        {
            string numbers = (exclusive ? "Exclusive, " : "") + $"{RoleName(unit.Role)}\n{Bodies(unit.FormsInSquad)}, worth {unit.Cost}";
            if (cell.width >= WideEnoughForSideBySide)
            {
                float size = Mathf.Min(cell.height, cell.width * 0.55f);
                DrawUnitPicture(new Rect(cell.x, cell.center.y - size * 0.5f, size, size), unit, faction, accent);
                float x = cell.x + size + 6f;
                FitPair(new Rect(x, cell.y, cell.xMax - x, cell.height), unit.Name, 20, numbers, 15, TextAnchor.MiddleLeft, boldFirst: true);
            }
            else
            {
                const float textHeight = 74f;
                float size = Mathf.Min(cell.width * 0.86f, cell.height - textHeight);
                DrawUnitPicture(new Rect(cell.center.x - size * 0.5f, cell.y, size, size), unit, faction, accent);
                float y = cell.y + size;
                FitPair(new Rect(cell.x, y, cell.width, cell.yMax - y), unit.Name, 19, numbers, 15, TextAnchor.MiddleCenter, boldFirst: true);
            }
        }

        /// <summary>A spell, item, economy, buff or training card. Returns true when taken.</summary>
        public static bool DrawGeneralCard(Rect card, CardStats stats, int owned, Texture2D icon, int price, bool canAfford)
        {
            EnsureStyles();
            CardFrameKind kind = CardArt.KindOf(stats);
            Color accent = CardArt.Accent(kind);
            Rect area = DrawFrameAndRibbon(card, kind, CardArt.Label(kind));
            float y = area.y + TopSpace;

            float nameHeight = Mathf.Min(58f, area.height * 0.1f);
            Fit(new Rect(area.x, y, area.width, nameHeight), HudTheme.Title(stats.Name), _title, 26, 15, TextAnchor.MiddleCenter, HudTheme.TitleColor, shadow: true, wrap: true);
            y += nameHeight;
            Fit(new Rect(area.x, y, area.width, 24f), KindDetail(stats), _line, 16, 11, TextAnchor.MiddleCenter, Color.Lerp(accent, Color.white, 0.5f));
            y += 28f;

            // The big picture takes what the text leaves.
            string effects = Sentence(stats.EffectsText());
            float bottom = area.yMax - ButtonHeight - 6f;
            float footer = FooterLines(stats) * 22f;
            float room = bottom - footer - y;
            float text = PairHeight(effects, 18, stats.Description, 15, area.width);
            float size = Mathf.Clamp(room - text - 16f, room * 0.3f, Mathf.Min(area.width * 0.9f, room * 0.64f));
            var picture = new Rect(area.center.x - size * 0.5f, y, size, size);
            CardArt.DrawStage(picture, accent);
            Texture2D emblem = CardArt.EmblemFor(stats, icon);
            if (emblem != null)
            {
                float inset = size * 0.2f;
                DrawTinted(new Rect(picture.x + inset, picture.y + inset, size - 2f * inset, size - 2f * inset), emblem, Color.Lerp(accent, Color.white, 0.55f));
            }
            y = picture.yMax + 6f;

            FitPair(new Rect(area.x, y, area.width, bottom - footer - y), effects, 18, stats.Description, 15, TextAnchor.UpperCenter);

            float lineY = bottom - footer;
            if (stats.Spell != null) { Fit(new Rect(area.x, lineY, area.width, 22f), $"Recharge: {stats.Spell.CooldownRounds} round{(stats.Spell.CooldownRounds == 1 ? "" : "s")} after use", _line, 14, 11, TextAnchor.MiddleCenter, HudTheme.DimText); lineY += 22f; }
            if (stats.MaxOwnedCopies < 99) Fit(new Rect(area.x, lineY, area.width, 22f), $"Owned: {owned} of {stats.MaxOwnedCopies}", _line, 14, 11, TextAnchor.MiddleCenter, HudTheme.DimText);

            return Button(new Rect(area.x, area.yMax - ButtonHeight, area.width, ButtonHeight), PriceLabel(price), canAfford);
        }

        /// <summary>A reinforcement card: the faction's frame and the unit's picture, with how many and what level on it. Returns true when taken.</summary>
        public static bool DrawReinforcementCard(Rect card, ReinforcementCard reinforcement, string faction, bool unlocked, int price, bool canAfford)
        {
            EnsureStyles();
            CardFrameKind kind = CardArt.FactionFrame(faction);
            Color accent = CardArt.Accent(kind);
            UnitStats unit = reinforcement.Type;
            Rect area = DrawFrameAndRibbon(card, kind, "REINFORCEMENTS");
            float y = area.y + TopSpace;

            float nameHeight = Mathf.Min(58f, area.height * 0.1f);
            Fit(new Rect(area.x, y, area.width, nameHeight), HudTheme.Title(unit.Name), _title, 26, 15, TextAnchor.MiddleCenter, HudTheme.TitleColor, shadow: true, wrap: true);
            y += nameHeight + 2f;

            string squads = $"{reinforcement.Squads} squad{(reinforcement.Squads == 1 ? "" : "s")} at level {reinforcement.Level}";
            string numbers = $"Tier {unit.Tier}, {RoleName(unit.Role)}, {Bodies(unit.FormsInSquad)} each\nWorth {reinforcement.TotalWorth} ({reinforcement.SquadWorth} per squad)\n"
                             + (unlocked ? "Placed like squads bought this round, on top of your squad limit." : "Also unlocks the unit for the rest of the match.");

            // The big picture takes what the text leaves.
            float bottom = area.yMax - ButtonHeight - 6f;
            float room = bottom - y;
            float text = PairHeight(squads, 18, numbers, 15, area.width);
            float size = Mathf.Clamp(room - text - 12f, room * 0.3f, Mathf.Min(area.width, room * 0.7f));
            var picture = new Rect(area.center.x - size * 0.5f, y, size, size);
            DrawUnitPicture(picture, unit, faction, accent);

            // What's in the card, on the picture: how many squads, and their level.
            float chip = Mathf.Clamp(size * 0.16f, 28f, 42f);
            DrawChip(new Rect(area.x, y, Mathf.Clamp(area.width * 0.4f, 76f, 118f), chip), $"Level {reinforcement.Level}", kind, 17);
            DrawChip(new Rect(area.xMax - chip * 1.7f, y, chip * 1.7f, chip), $"x{reinforcement.Squads}", kind, 22);
            y = picture.yMax + 4f;

            FitPair(new Rect(area.x, y, area.width, bottom - y), squads, 18, numbers, 15, TextAnchor.UpperCenter);

            return Button(new Rect(area.x, area.yMax - ButtonHeight, area.width, ButtonHeight), PriceLabel(price), canAfford);
        }

        // ------------------------------------------------------------------ parts

        /// <summary>Space from the top of a card's contents to the first line, under its ribbon.</summary>
        private const float TopSpace = 22f;

        private static Rect DrawFrameAndRibbon(Rect card, CardFrameKind kind, string label)
        {
            CardArt.DrawCard(card, kind, Hovered(card));
            Rect area = CardArt.ContentArea(card);
            // The ribbon sits across the top of the frame.
            float height = Mathf.Clamp(card.width * 0.13f, 34f, 48f);
            var ribbon = new Rect(card.x + 26f, card.y + 6f, card.width - 52f, height);
            CardArt.DrawRibbon(ribbon, kind);
            float end = height * 0.7f;
            Fit(new Rect(ribbon.x + end, ribbon.y, ribbon.width - 2f * end, ribbon.height), label, _ribbon, 19, 11, TextAnchor.MiddleCenter, Color.white, shadow: true);
            return area;
        }

        /// <summary>A unit's picture with a soft light behind it.</summary>
        public static void DrawUnitPicture(Rect rect, UnitStats unit, string faction, Color light)
        {
            float spread = rect.width * 0.08f;
            CardArt.DrawStage(new Rect(rect.x - spread, rect.y - spread, rect.width + 2f * spread, rect.height + 2f * spread), light);
            Texture2D icon = UnitIcons.For(unit, faction);
            if (icon != null) GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);
        }

        private static void DrawChip(Rect rect, string text, CardFrameKind kind, int fontSize)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Color old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.72f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                Color accent = CardArt.Accent(kind);
                GUI.color = accent;
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
                GUI.color = old;
            }
            Fit(rect, text, _ribbon, fontSize, 12, TextAnchor.MiddleCenter, Color.white);
        }

        private static void DrawTinted(Rect rect, Texture2D texture, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        private static bool Button(Rect rect, string label, bool enabled)
        {
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && enabled;
            bool clicked = GUI.Button(rect, label, _button);
            UiAudio.Watch(rect, wasEnabled && enabled);
            GUI.enabled = wasEnabled;
            return clicked;
        }

        private static bool Hovered(Rect card) => card.Contains(DemoMouse ?? Event.current.mousePosition);

        private static string PriceLabel(int price) => price > 0 ? $"Take ({price})" : "Take (free)";

        private static int FooterLines(CardStats stats) => (stats.Spell != null ? 1 : 0) + (stats.MaxOwnedCopies < 99 ? 1 : 0);

        private static string KindDetail(CardStats card)
        {
            switch (card.Type)
            {
                case CardType.Spell: return card.Spell != null && card.Spell.Offensive ? "Anywhere on the field" : "On your own side";
                case CardType.Item: return "Goes on one squad";
                case CardType.Economy: return "More credits";
                case CardType.Training: return "Use it on a squad";
                default: return "For your whole army";
            }
        }

        public static string Bodies(int count) => count == 1 ? "1 body" : $"{count} bodies";

        public static string RoleName(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.ChaffClear: return "Chaff clear";
                case UnitRole.SingleTargetDps: return "Single-target DPS";
                case UnitRole.AntiAir: return "Anti-air";
                default: return role.ToString();
            }
        }

        // ------------------------------------------------------------------ text that fits

        /// <summary>A label at the largest size from `size` down to `least` at which it fits its place.</summary>
        public static void Fit(Rect rect, string text, GUIStyle style, int size, int least, TextAnchor anchor, Color color, bool shadow = false, bool bold = false, bool wrap = false)
        {
            if (string.IsNullOrEmpty(text) || rect.width <= 0f || rect.height <= 0f) return;
            Content.text = text;
            style.alignment = anchor;
            style.normal.textColor = color;
            if (bold) style.fontStyle = FontStyle.Bold;
            bool wrapping = style.wordWrap;
            if (wrap) style.wordWrap = true;
            int s = SizeThatFits(rect, style, HudTheme.Size(size), HudTheme.Size(least));
            style.fontSize = s;
            HudTheme.Fit(style);
            if (shadow && Event.current.type == EventType.Repaint)
            {
                style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), Content, style);
                style.normal.textColor = color;
            }
            GUI.Label(rect, Content, style);
            if (bold) style.fontStyle = FontStyle.Normal;
            style.wordWrap = wrapping;
        }

        private static int SizeThatFits(Rect rect, GUIStyle style, int size, int least)
        {
            for (int s = size; s >= least; s--)
            {
                style.fontSize = s;
                if (Fits(rect, style)) return s;
            }
            style.fontSize = least;
            if (Event.current.type == EventType.Repaint)
            {
                TextThatDidNotFit++;
                LastTextThatDidNotFit = Content.text;
            }
            return least;
        }

        private static bool Fits(Rect rect, GUIStyle style)
        {
            if (style.wordWrap)
            {
                if (style.CalcHeight(Content, rect.width) > rect.height + 0.5f) return false;
                string text = Content.text;
                bool words = WordsFit(text, style, style.fontSize, rect.width, style.fontStyle == FontStyle.Bold);
                Content.text = text;
                return words;
            }
            Vector2 needed = style.CalcSize(Content);
            return needed.x <= rect.width + 0.5f && needed.y <= rect.height + 0.5f;
        }

        /// <summary>
        /// Two blocks of text, one under the other: the first bright, the second dimmer. They shrink together until both fit.
        /// A middle anchor centres the pair in its place, top to bottom.
        /// </summary>
        private static void FitPair(Rect rect, string first, int firstSize, string second, int secondSize, TextAnchor anchor, bool boldFirst = false)
        {
            GUIStyle style = _wrap;
            int a = HudTheme.Size(firstSize), b = HudTheme.Size(secondSize), leastA = HudTheme.Size(12), leastB = HudTheme.Size(11);
            style.fontStyle = boldFirst ? FontStyle.Bold : FontStyle.Normal;
            float firstHeight = HeightAt(first, style, a, rect.width);
            style.fontStyle = FontStyle.Normal;
            float secondHeight = HeightAt(second, style, b, rect.width);
            while (firstHeight + PairGap + secondHeight > rect.height || !WordsFit(first, style, a, rect.width, boldFirst))
            {
                if (a <= leastA && b <= leastB)
                {
                    if (Event.current.type == EventType.Repaint)
                    {
                        TextThatDidNotFit++;
                        LastTextThatDidNotFit = first + " / " + second;
                    }
                    break;
                }
                if (a > leastA) a--;
                if (b > leastB) b--;
                style.fontStyle = boldFirst ? FontStyle.Bold : FontStyle.Normal;
                firstHeight = HeightAt(first, style, a, rect.width);
                style.fontStyle = FontStyle.Normal;
                secondHeight = HeightAt(second, style, b, rect.width);
            }

            bool middle = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.MiddleRight;
            float y = middle ? rect.y + Mathf.Max(0f, (rect.height - firstHeight - PairGap - secondHeight) * 0.5f) : rect.y;
            style.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft ? TextAnchor.UpperLeft : TextAnchor.UpperCenter;
            if (!string.IsNullOrEmpty(first))
            {
                Content.text = first;
                style.fontSize = a;
                style.fontStyle = boldFirst ? FontStyle.Bold : FontStyle.Normal;
                style.normal.textColor = HudTheme.Text;
                HudTheme.Fit(style);
                GUI.Label(new Rect(rect.x, y, rect.width, firstHeight + 2f), Content, style);
                style.fontStyle = FontStyle.Normal;
            }
            if (!string.IsNullOrEmpty(second))
            {
                Content.text = second;
                style.fontSize = b;
                style.normal.textColor = HudTheme.DimText;
                HudTheme.Fit(style);
                GUI.Label(new Rect(rect.x, y + firstHeight + PairGap, rect.width, secondHeight + 2f), Content, style);
            }
        }

        /// <summary>How tall a pair of blocks is at their full size.</summary>
        private static float PairHeight(string first, int firstSize, string second, int secondSize, float width) =>
            HeightAt(first, _wrap, HudTheme.Size(firstSize), width) + PairGap + HeightAt(second, _wrap, HudTheme.Size(secondSize), width);

        private static float HeightAt(string text, GUIStyle style, int size, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            Content.text = text;
            style.fontSize = size;
            return style.CalcHeight(Content, width);
        }

        /// <summary>False if a word of the text is wider than the place: it would break in the middle of the word.</summary>
        private static bool WordsFit(string text, GUIStyle style, int size, float width, bool bold)
        {
            if (string.IsNullOrEmpty(text)) return true;
            style.fontSize = size;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            bool wrapping = style.wordWrap;
            style.wordWrap = false;
            bool fits = true;
            foreach (string word in text.Split(' ', '\n'))
            {
                Content.text = word;
                if (style.CalcSize(Content).x > width) { fits = false; break; }
            }
            style.wordWrap = wrapping;
            style.fontStyle = FontStyle.Normal;
            return fits;
        }

        /// <summary>The text with a capital first letter (effects are written as lists: "can move in every deployment phase").</summary>
        private static string Sentence(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>A plain style for a HUD's own Fit labels: one line, the body font.</summary>
        public static GUIStyle LineStyle { get { EnsureStyles(); return _line; } }

        /// <summary>A plain style for a HUD's own Fit labels: wrapped, the body font.</summary>
        public static GUIStyle WrapStyle { get { EnsureStyles(); return _wrap; } }

        /// <summary>A plain style for a HUD's own Fit labels: the title font.</summary>
        public static GUIStyle TitleStyle { get { EnsureStyles(); return _title; } }

        private static void EnsureStyles()
        {
            if (_wrap != null && _styleVersion == HudTheme.Version && _button != null && _button.normal.background == GUI.skin.button.normal.background) return;
            _styleVersion = HudTheme.Version;
            _line = new GUIStyle(GUI.skin.label) { wordWrap = false };
            _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            _title = new GUIStyle(GUI.skin.label) { font = HudTheme.TitleFont, fontStyle = HudTheme.TitleFontStyle, wordWrap = false };
            _ribbon = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false };
            _button = new GUIStyle(GUI.skin.button) { fontSize = HudTheme.Size(19), fontStyle = FontStyle.Bold };
            HudTheme.Fit(_button);
        }
    }
}
