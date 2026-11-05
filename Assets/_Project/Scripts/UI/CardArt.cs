// CardArt.cs
// The art of the cards (M10), drawn by Tools/UI/make_card_art.py into Resources/CardArt: a border and a label ribbon for
// each kind of card, emblems, the hover glow. Looks only.
//   Kinds: offensive spell, defensive spell, economy, buff, item, training, and one per faction for reinforcements and
//   the starting draft (UNSC, Covenant, Flood). A faction the art doesn't know gets the neutral frame.
// DrawCard draws a border at any size: fixed corners, and edges that repeat their middle part instead of stretching it,
// so that rivets and growths stay their shape and the lines stay crisp. EmblemFor finds a card's big picture: a large
// version of its icon (Art/UI/Items has the small ones), the spell's own emblem, its effect's, or its kind's.
// Depends on: UnitIcons (faction colours), Halobellum.Core (CardStats, SpellStats), UnityEngine.

using System.Collections.Generic;
using System.Text;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.UI
{
    public enum CardFrameKind
    {
        SpellOffensive,
        SpellDefensive,
        Economy,
        Buff,
        Item,
        Training,
        UNSC,
        Covenant,
        Flood,
        Neutral,
    }

    public static class CardArt
    {
        private const string Folder = "CardArt/";
        /// <summary>Texture pixels: the corners of a frame, and the part of each edge that repeats.</summary>
        private const int FrameBorder = 160, FrameMiddle = 192;
        /// <summary>Frames are drawn at half their texture's size, or smaller on a narrow card.</summary>
        private const float FullScale = 0.5f, LeastScale = 0.3f;
        /// <summary>A card narrower than this draws its frame smaller, so that the corners leave room for the text.</summary>
        private const float WidthForFullScale = 340f;
        /// <summary>Texture pixels from a frame's outside to the inside of its band.</summary>
        private const float WindowInset = 60f;
        private const int RibbonEnd = 48;

        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        private static Texture2D Load(string path)
        {
            if (Textures.TryGetValue(path, out Texture2D texture) && texture != null) return texture;
            texture = Resources.Load<Texture2D>(Folder + path);
            Textures[path] = texture;
            return texture;
        }

        // ------------------------------------------------------------------ what a card is

        public static CardFrameKind KindOf(CardStats card)
        {
            switch (card?.Type)
            {
                case CardType.Spell: return card.Spell != null && !card.Spell.Offensive ? CardFrameKind.SpellDefensive : CardFrameKind.SpellOffensive;
                case CardType.Item: return CardFrameKind.Item;
                case CardType.Economy: return CardFrameKind.Economy;
                case CardType.Training: return CardFrameKind.Training;
                case CardType.Buff: return CardFrameKind.Buff;
                default: return CardFrameKind.Neutral;
            }
        }

        public static CardFrameKind FactionFrame(string faction)
        {
            switch (faction)
            {
                case "UNSC": return CardFrameKind.UNSC;
                case "Covenant": return CardFrameKind.Covenant;
                case "Flood": return CardFrameKind.Flood;
                default: return CardFrameKind.Neutral;
            }
        }

        /// <summary>The word on a card's ribbon.</summary>
        public static string Label(CardFrameKind kind)
        {
            switch (kind)
            {
                case CardFrameKind.SpellOffensive: return "OFFENSIVE SPELL";
                case CardFrameKind.SpellDefensive: return "DEFENSIVE SPELL";
                case CardFrameKind.Economy: return "ECONOMY";
                case CardFrameKind.Buff: return "ARMY BUFF";
                case CardFrameKind.Item: return "ITEM";
                case CardFrameKind.Training: return "TRAINING";
                default: return "REINFORCEMENTS";
            }
        }

        /// <summary>The kind's colour: its emblem, its text, the glow around it.</summary>
        public static Color Accent(CardFrameKind kind)
        {
            switch (kind)
            {
                case CardFrameKind.SpellOffensive: return new Color(1f, 0.42f, 0.26f);
                case CardFrameKind.SpellDefensive: return new Color(0.42f, 0.72f, 1f);
                case CardFrameKind.Economy: return new Color(0.45f, 0.95f, 0.55f);
                case CardFrameKind.Buff: return new Color(0.95f, 0.62f, 0.95f);
                case CardFrameKind.Item: return new Color(1f, 0.82f, 0.38f);
                case CardFrameKind.Training: return new Color(0.42f, 0.92f, 0.82f);
                case CardFrameKind.UNSC: return UnitIcons.FactionColor("UNSC");
                case CardFrameKind.Covenant: return new Color(0.6f, 0.72f, 1f);
                case CardFrameKind.Flood: return UnitIcons.FactionColor("Flood");
                case CardFrameKind.Neutral: return new Color(0.55f, 0.88f, 1f);
                default: return UnitIcons.FactionColor(null);
            }
        }

        /// <summary>The dark inside of a card: dark enough for any text, and still its kind's colour.</summary>
        private static Color Inside(CardFrameKind kind)
        {
            switch (kind)
            {
                case CardFrameKind.SpellOffensive: return new Color(0.085f, 0.03f, 0.03f);
                case CardFrameKind.SpellDefensive: return new Color(0.035f, 0.055f, 0.11f);
                case CardFrameKind.Economy: return new Color(0.03f, 0.075f, 0.05f);
                case CardFrameKind.Buff: return new Color(0.075f, 0.035f, 0.085f);
                case CardFrameKind.Item: return new Color(0.08f, 0.06f, 0.03f);
                case CardFrameKind.Training: return new Color(0.03f, 0.07f, 0.07f);
                case CardFrameKind.UNSC: return new Color(0.055f, 0.065f, 0.07f);
                case CardFrameKind.Covenant: return new Color(0.055f, 0.035f, 0.1f);
                case CardFrameKind.Flood: return new Color(0.055f, 0.06f, 0.03f);
                default: return new Color(0.04f, 0.15f, 0.34f);
            }
        }

        // ------------------------------------------------------------------ big pictures

        /// <summary>A card's big picture. `icon` is the card's own small icon, if it has one.</summary>
        public static Texture2D EmblemFor(CardStats card, Texture2D icon)
        {
            if (icon != null)
            {
                Texture2D large = Load("Emblems/" + icon.name);
                return large != null ? large : icon;
            }
            if (card?.Spell != null)
            {
                Texture2D own = Load("Emblems/Spell_" + Compact(card.Spell.Name));
                if (own != null) return own;
                Texture2D effect = Load("Emblems/Effect_" + card.Spell.Effect);
                return effect != null ? effect : Load("Emblems/Kind_Spell");
            }
            switch (card?.Type)
            {
                case CardType.Item: return Load("Emblems/Kind_Item");
                case CardType.Economy: return Load("Emblems/Kind_Economy");
                case CardType.Training: return Load("Emblems/Kind_Training");
                case CardType.Buff: return Load("Emblems/Kind_Buff");
                default: return Load("Emblems/Kind_Spell");
            }
        }

        public static Texture2D FactionEmblem(string faction) => Load("Emblems/Faction_" + faction);

        /// <summary>A name with only its letters and digits: "Prophet's Blessing" is "ProphetsBlessing".</summary>
        public static string Compact(string name)
        {
            var builder = new StringBuilder(name?.Length ?? 0);
            foreach (char c in name ?? "")
            {
                if (char.IsLetterOrDigit(c)) builder.Append(c);
            }
            return builder.ToString();
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>How big a frame is drawn on a card this wide.</summary>
        public static float FrameScale(float cardWidth) => Mathf.Clamp(FullScale * cardWidth / WidthForFullScale, LeastScale, FullScale);

        /// <summary>The inside of a card's frame: where its contents go.</summary>
        public static Rect ContentArea(Rect card)
        {
            float inset = WindowInset * FrameScale(card.width) + 5f;
            return new Rect(card.x + inset, card.y + inset, card.width - 2f * inset, card.height - 2f * inset);
        }

        /// <summary>A card's background and border. A hovered card has a glow around it, and a brighter border.</summary>
        public static void DrawCard(Rect card, CardFrameKind kind, bool hovered)
        {
            if (Event.current.type != EventType.Repaint) return;
            float scale = FrameScale(card.width);
            Color old = GUI.color;
            Color accent = Accent(kind);

            if (hovered)
            {
                Texture2D glow = Load("Glow");
                if (glow != null)
                {
                    const float spread = 26f;
                    GUI.color = new Color(accent.r, accent.g, accent.b, 0.95f);
                    NineSlice(new Rect(card.x - spread, card.y - spread, card.width + 2f * spread, card.height + 2f * spread), glow, RibbonEnd, 1f);
                }
            }

            // Inside the frame: its dark colour, then a wash of the kind's colour from the top.
            float inset = (WindowInset - 8f) * scale;
            var inner = new Rect(card.x + inset, card.y + inset, card.width - 2f * inset, card.height - 2f * inset);
            GUI.color = Inside(kind);
            GUI.DrawTexture(inner, Texture2D.whiteTexture);
            // A neutral card is the same drafting paper as the neutral theme's panels (D68).
            Texture2D paper = kind == CardFrameKind.Neutral && HudTheme.Current != null ? HudTheme.Current.panelDetail : null;
            if (paper != null)
            {
                GUI.color = Color.white;
                float tile = paper.width * HudTheme.Current.textureScale;
                GUI.DrawTextureWithTexCoords(inner, paper, new Rect(0f, 0f, inner.width / tile, inner.height / tile));
            }
            Texture2D wash = Load("Wash");
            if (wash != null)
            {
                GUI.color = new Color(accent.r, accent.g, accent.b, hovered ? 0.3f : 0.22f);
                GUI.DrawTexture(inner, wash, ScaleMode.StretchToFill);
            }

            float shade = hovered ? 1f : 0.88f;
            GUI.color = new Color(shade, shade, shade, 1f);
            Texture2D frame = Load("Frames/Frame_" + kind);
            if (frame != null) TiledFrame(card, frame, scale);
            GUI.color = old;
        }

        /// <summary>A kind's label ribbon, with nothing on it.</summary>
        public static void DrawRibbon(Rect rect, CardFrameKind kind)
        {
            if (Event.current.type != EventType.Repaint) return;
            Texture2D ribbon = Load("Frames/Ribbon_" + kind);
            if (ribbon != null) NineSlice(rect, ribbon, RibbonEnd, Mathf.Min(0.75f, rect.height / 72f));
        }

        /// <summary>A soft light behind a card's big picture.</summary>
        public static void DrawStage(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Texture2D stage = Load("Stage");
            if (stage == null) return;
            Color old = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, 0.55f);
            GUI.DrawTexture(rect, stage, ScaleMode.StretchToFill);
            GUI.color = old;
        }

        /// <summary>Fixed corners; the edges repeat their middle a whole number of times, each a little wider or narrower to fit.</summary>
        private static void TiledFrame(Rect rect, Texture2D texture, float scale)
        {
            float border = FrameBorder * scale;
            float u = FrameBorder / (float)texture.width, v = FrameBorder / (float)texture.height;
            float xMin = Snap(rect.xMin), xLeft = Snap(rect.xMin + border), xRight = Snap(rect.xMax - border), xMax = Snap(rect.xMax);
            float yMin = Snap(rect.yMin), yTop = Snap(rect.yMin + border), yBottom = Snap(rect.yMax - border), yMax = Snap(rect.yMax);
            var left = new Rect(0f, v, u, 1f - 2f * v);        // Texture coordinates run upward.
            var right = new Rect(1f - u, v, u, 1f - 2f * v);
            var top = new Rect(u, 1f - v, 1f - 2f * u, v);
            var bottom = new Rect(u, 0f, 1f - 2f * u, v);

            // Corners.
            GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(xMin, yMin, xLeft, yTop), texture, new Rect(0f, 1f - v, u, v));
            GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(xRight, yMin, xMax, yTop), texture, new Rect(1f - u, 1f - v, u, v));
            GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(xMin, yBottom, xLeft, yMax), texture, new Rect(0f, 0f, u, v));
            GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(xRight, yBottom, xMax, yMax), texture, new Rect(1f - u, 0f, u, v));

            // Top and bottom edges.
            float width = rect.width - 2f * border;
            if (width > 0.5f)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(width / (FrameMiddle * scale)));
                float step = width / count;
                for (int i = 0; i < count; i++)
                {
                    float from = Snap(xLeft + i * step), to = i == count - 1 ? xRight : Snap(xLeft + (i + 1) * step);
                    GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(from, yMin, to, yTop), texture, top);
                    GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(from, yBottom, to, yMax), texture, bottom);
                }
            }

            // Left and right edges.
            float height = rect.height - 2f * border;
            if (height > 0.5f)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(height / (FrameMiddle * scale)));
                float step = height / count;
                for (int i = 0; i < count; i++)
                {
                    float from = Snap(yTop + i * step), to = i == count - 1 ? yBottom : Snap(yTop + (i + 1) * step);
                    GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(xMin, from, xLeft, to), texture, left);
                    GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(xRight, from, xMax, to), texture, right);
                }
            }
        }

        /// <summary>
        /// A position moved to a whole screen pixel. Pieces of a frame that meet between two pixels leave a hairline:
        /// the GUI rounds each piece's position and size on its own.
        /// </summary>
        private static float Snap(float position)
        {
            float scale = GUI.matrix.m00;
            return scale > 0f ? Mathf.Round(position * scale) / scale : position;
        }

        /// <summary>Fixed corners and stretched edges, for ribbons and the glow (their edges are plain along their length).</summary>
        private static void NineSlice(Rect rect, Texture2D texture, int border, float scale)
        {
            float b = Mathf.Min(border * scale, Mathf.Min(rect.width, rect.height) * 0.5f);
            float u = border / (float)texture.width, v = Mathf.Min(0.5f, border / (float)texture.height);
            float[] xs = { Snap(rect.xMin), Snap(rect.xMin + b), Snap(rect.xMax - b), Snap(rect.xMax) };
            float[] ys = { Snap(rect.yMin), Snap(rect.yMin + b), Snap(rect.yMax - b), Snap(rect.yMax) };
            float[] us = { 0f, u, 1f - u, 1f };
            float[] vs = { 1f, 1f - v, v, 0f };
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    Rect destination = Rect.MinMaxRect(xs[column], ys[row], xs[column + 1], ys[row + 1]);
                    if (destination.width <= 0f || destination.height <= 0f) continue;
                    GUI.DrawTextureWithTexCoords(destination, texture, Rect.MinMaxRect(us[column], vs[row + 1], us[column + 1], vs[row]));
                }
            }
        }
    }
}
