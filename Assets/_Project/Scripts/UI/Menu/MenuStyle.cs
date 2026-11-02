// MenuStyle.cs
// How the main menu, settings and pause screens look and take clicks (M10): near-black panels with a fine edge, warm dusk accents, text
// buttons that light up from the left, thin sliders. It is its own look rather than a faction's theme, because these screens belong to
// no faction, and it leaves GUI.skin and HudTheme alone. Call Ensure at the start of OnGUI.
// Every button comes through Button, so that the test tool (-menuDemo) can press one by its label, down the same path as a mouse click.
// Clicked is raised for every press, for sound. Lay out in virtual units from HudScale.
// Depends on: MenuLook, UnityEngine.

using System;
using UnityEngine;

namespace Halobellum.UI
{
    public static class MenuStyle
    {
        public static readonly Color Accent = new Color(1f, 0.71f, 0.36f);
        public static readonly Color Ink = new Color(0.95f, 0.92f, 0.87f);
        public static readonly Color DimInk = new Color(0.68f, 0.65f, 0.62f);
        public static readonly Color PanelFill = new Color(0.035f, 0.04f, 0.055f, 0.9f);
        public static readonly Color Edge = new Color(1f, 0.85f, 0.65f, 0.26f);
        public static readonly Color Track = new Color(1f, 1f, 1f, 0.14f);

        /// <summary>Raised whenever any button here is pressed.</summary>
        public static event Action Clicked;

        /// <summary>A button a test tool has asked to press, by its label or key.</summary>
        public static string PressedByTest { get; set; }

        public static GUIStyle Title { get; private set; }
        public static GUIStyle Heading { get; private set; }
        public static GUIStyle Section { get; private set; }
        public static GUIStyle Body { get; private set; }
        public static GUIStyle BodyRight { get; private set; }
        public static GUIStyle Small { get; private set; }
        public static GUIStyle BigButton { get; private set; }
        public static GUIStyle SmallButton { get; private set; }

        private static GUIStyle _clear, _sliderThumb;
        private static Texture2D _fadeRight, _fadeDown;
        private static GUISkin _builtFor;
        private static MenuLook _look;

        /// <summary>Makes the styles. Call in OnGUI, where the skin they start from exists.</summary>
        public static void Ensure()
        {
            if (Title != null && _builtFor != null) return;
            _builtFor = GUI.skin;

            // Held in a static field, so that its fonts aren't unloaded along with a scene.
            _look = Resources.Load<MenuLook>(MenuLook.ResourcePath);
            Font titleFont = _look != null ? _look.titleFont : null;
            Font headingFont = _look != null ? _look.headingFont : null;
            Font textFont = _look != null ? _look.textFont : null;

            // Note: built on the skin's label, to keep its text clipping (see HudTheme).
            GUIStyle Text(Font font, int size, Color color, TextAnchor anchor, FontStyle fontStyle = FontStyle.Normal)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = size,
                    fontStyle = fontStyle,
                    alignment = anchor,
                    wordWrap = false,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    contentOffset = Vector2.zero,
                };
                style.normal.background = null;
                style.normal.textColor = color;
                style.hover.background = null;
                style.hover.textColor = color;
                style.active.background = null;
                style.active.textColor = color;
                return style;
            }

            Title = Text(titleFont, 108, Ink, TextAnchor.MiddleLeft);
            Heading = Text(headingFont, 36, Ink, TextAnchor.MiddleCenter);
            Section = Text(headingFont, 19, Accent, TextAnchor.MiddleLeft);
            Body = Text(textFont, 21, Ink, TextAnchor.MiddleLeft);
            BodyRight = Text(textFont, 19, DimInk, TextAnchor.MiddleRight);
            Small = Text(textFont, 16, DimInk, TextAnchor.MiddleLeft);
            BigButton = Text(headingFont, 28, Ink, TextAnchor.MiddleLeft);
            SmallButton = Text(textFont, 19, Ink, TextAnchor.MiddleCenter);
            _clear = new GUIStyle();
            _sliderThumb = new GUIStyle { fixedWidth = 20f, fixedHeight = 28f };

            _fadeRight = MakeFade(256, 1, true);
            _fadeDown = MakeFade(1, 256, false);
        }

        /// <summary>A soft ramp of alpha: solid at the start, gone at the end. Smooth at both ends, so no edge shows.</summary>
        private static Texture2D MakeFade(int width, int height, bool across)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Menu fade",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            int steps = across ? width : height;
            var pixels = new Color[width * height];
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);
                float alpha = 1f - t * t * (3f - 2f * t);
                if (across) pixels[i] = new Color(1f, 1f, 1f, alpha);
                else pixels[(height - 1 - i) * width] = new Color(1f, 1f, 1f, alpha); // Texture rows start at the bottom.
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // ------------------------------------------------------------------ drawing

        public static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>Colour at the rect's left edge, fading to nothing at its right edge.</summary>
        public static void FadeRight(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _fadeRight);
            GUI.color = old;
        }

        /// <summary>Colour at the rect's top edge, fading to nothing at its bottom edge.</summary>
        public static void FadeDown(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _fadeDown);
            GUI.color = old;
        }

        /// <summary>A panel: nearly black, a fine edge, and a short bright line at its top left.</summary>
        public static void Panel(Rect rect)
        {
            Fill(rect, PanelFill);
            Outline(rect, Edge);
            Fill(new Rect(rect.x, rect.y, 96f, 3f), Accent);
        }

        public static void Outline(Rect rect, Color color)
        {
            const float thickness = 1.5f;
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        /// <summary>Text with wide letter spacing and a soft shadow, as the big title has. Returns its width.</summary>
        public static float SpacedText(float x, float y, string text, GUIStyle style, float spacing, Color color)
        {
            float start = x;
            float height = style.CalcSize(new GUIContent("H")).y;
            foreach (char letter in text)
            {
                var content = new GUIContent(letter.ToString());
                float width = style.CalcSize(content).x;
                Color old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.55f * color.a);
                GUI.Label(new Rect(x + 3f, y + 4f, width + 4f, height), content, style);
                GUI.color = color;
                GUI.Label(new Rect(x, y, width + 4f, height), content, style);
                GUI.color = old;
                x += width + spacing;
            }
            return x - spacing - start;
        }

        // ------------------------------------------------------------------ buttons and controls

        /// <summary>GUI.Button that a test tool can also press. Use it for any button the tool must reach. The key defaults to the label.</summary>
        public static bool Button(Rect rect, string label, GUIStyle style, string key = null)
        {
            bool pressed = GUI.Button(rect, label, style) || TestPressed(key ?? label);
            if (pressed) Clicked?.Invoke();
            return pressed;
        }

        private static bool TestPressed(string key)
        {
            if (PressedByTest != key || Event.current.type != EventType.Repaint || !GUI.enabled) return false;
            PressedByTest = null;
            return true;
        }

        /// <summary>A big text button of a menu's column: on hover a warm light runs in from the left, and a bar and the text brighten.</summary>
        public static bool MenuButton(Rect rect, string label)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                FadeRight(rect, new Color(Accent.r, Accent.g, Accent.b, hover ? 0.3f : 0.07f));
                Fill(new Rect(rect.x, rect.y + 4f, hover ? 5f : 2f, rect.height - 8f), hover ? Accent : Edge);
                GUIStyle style = BigButton;
                Color old = style.normal.textColor;
                style.normal.textColor = hover ? Accent : Ink;
                style.Draw(new Rect(rect.x + (hover ? 30f : 24f), rect.y, rect.width - 30f, rect.height), label, false, false, false, false);
                style.normal.textColor = old;
            }
            return Button(rect, label, _clear);
        }

        /// <summary>A small framed button, lit when selected or hovered.</summary>
        public static bool SmallFramedButton(Rect rect, string label, bool selected, string key = null)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                Fill(rect, selected ? new Color(Accent.r, Accent.g, Accent.b, 0.26f) : new Color(1f, 1f, 1f, hover ? 0.1f : 0.035f));
                Outline(rect, selected ? Accent : hover ? new Color(Accent.r, Accent.g, Accent.b, 0.7f) : Edge);
                GUIStyle style = SmallButton;
                Color old = style.normal.textColor;
                style.normal.textColor = selected || hover ? Ink : DimInk;
                style.Draw(rect, label, false, false, false, false);
                style.normal.textColor = old;
            }
            return Button(rect, label, _clear, key);
        }

        /// <summary>Side-by-side choices. Returns the one selected now. The key of each is its row's name and its label.</summary>
        public static int Choice(Rect rect, string row, string[] labels, int selected)
        {
            float width = (rect.width - 6f * (labels.Length - 1)) / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                var button = new Rect(rect.x + i * (width + 6f), rect.y, width, rect.height);
                if (SmallFramedButton(button, labels[i], i == selected, row + "/" + labels[i])) selected = i;
            }
            return selected;
        }

        /// <summary>A value with an arrow on each side. Returns -1, 0 or +1.</summary>
        public static int Stepper(Rect rect, string row, string value)
        {
            const float arrow = 44f;
            int step = 0;
            if (SmallFramedButton(new Rect(rect.x, rect.y, arrow, rect.height), "<", false, row + "/<")) step = -1;
            if (SmallFramedButton(new Rect(rect.xMax - arrow, rect.y, arrow, rect.height), ">", false, row + "/>")) step = 1;
            GUI.Label(new Rect(rect.x + arrow, rect.y, rect.width - 2f * arrow, rect.height), value, SmallButton);
            return step;
        }

        /// <summary>A thin slider from 0 to 1. Returns the value.</summary>
        public static float Slider(Rect rect, float value)
        {
            float middle = rect.center.y;
            float thumbTravel = rect.width - _sliderThumb.fixedWidth;
            float at = rect.x + _sliderThumb.fixedWidth * 0.5f + thumbTravel * value;
            bool hover = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                Fill(new Rect(rect.x, middle - 2f, rect.width, 4f), Track);
                Fill(new Rect(rect.x, middle - 2f, at - rect.x, 4f), Accent);
                Fill(new Rect(at - 4f, middle - 11f, 8f, 22f), hover ? Color.white : Ink);
            }
            return GUI.HorizontalSlider(rect, value, 0f, 1f, _clear, _sliderThumb);
        }
    }
}
