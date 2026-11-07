// Disclaimer.cs
// The owner's disclaimer (D96), shown once each time the game starts, before the main menu: white text on black, centred, and a glowing
// "Click anywhere to continue" that pulses. HALOBELLUM is written as the main menu writes it: its title font and colour, spaced, underlined in its accent. A click or a key goes on to the menu, which then fades in (MainMenuHud). The link to Microsoft's
// Game Content Usage Rules opens in the browser when clicked, and does not go on. The text shrinks to fit any window.
// It is skipped in batch runs (tests) and in the picture runs (a -...Demo or -gallery flag); -disclaimerPicture <png> shows it, saves a
// picture of it and quits.
// Depends on: MenuStyle, UnityEngine.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.UI
{
    public static class Disclaimer
    {
        public const string Link = "https://www.xbox.com/en-US/developers/rules";
        public const string Continue = "Click anywhere to continue";

        private enum Kind { Title, Brand, Heading, Text, Signature, Link }

        // The owner's words, as given (2026-11-07).
        private static readonly (Kind kind, string text)[] Lines =
        {
            (Kind.Title, "WELCOME TO"),
            (Kind.Brand, MainMenuHud.Title),
            (Kind.Heading, "Unofficial Fan Project"),
            (Kind.Text, "Halobellum is a free, noncommercial fan-made strategy game inspired by Halo and Mechabellum. It is not affiliated with, sponsored by, approved by, or endorsed by Microsoft, Halo Studios, or the developers of Mechabellum. All third-party intellectual property belongs to its respective owners."),
            (Kind.Heading, "AI-Assisted Development"),
            (Kind.Text, "Halobellum was primarily developed using Claude Opus 5.5, with planning assistance from ChatGPT. Game design, creative direction, testing, and balancing were guided by its creator."),
            (Kind.Heading, "Independent Project — No Guaranteed Support"),
            (Kind.Text, "This game is an independent hobby project, not a professionally supported product. Bugs, crashes, balance issues, and unfinished content may occur. Updates, fixes, and technical support are not guaranteed."),
            (Kind.Heading, "Public Availability"),
            (Kind.Text, "As an unofficial fan creation, Halobellum may be modified, withdrawn, or removed from public distribution at any time, including at the request of intellectual property rights holders. Please do not publicly share screenshots, recordings, gameplay videos, download links or game files without permission. The less attention this unofficial project attracts, the better its chances of remaining available."),
            (Kind.Text, "Thank you for playing, and enjoy the battle!"),
            (Kind.Signature, "Created by Miker · Free to play"),
            (Kind.Link, "Microsoft Game Content Usage Rules: " + Link),
        };

        /// <summary>The whole text, for tests: the owner's words in order.</summary>
        public static IEnumerable<string> Text
        {
            get { foreach (var line in Lines) yield return line.text; }
        }

        private static bool? _pending;

        /// <summary>True until the player has gone past it, once each time the game starts. Never in a batch run or a picture run.</summary>
        public static bool Pending
        {
            get
            {
                if (_pending == null) _pending = ShouldShow(Application.isBatchMode, Environment.GetCommandLineArgs());
                return _pending.Value;
            }
            set => _pending = value;
        }

        /// <summary>Whether a start of the game with these arguments shows it: not in a batch run, nor in a run that takes pictures.</summary>
        public static bool ShouldShow(bool batch, string[] args)
        {
            if (Array.IndexOf(args, "-disclaimerPicture") >= 0) return true;
            if (batch) return false;
            foreach (string arg in args)
            {
                if (!arg.StartsWith("-")) continue;
                if (arg.IndexOf("demo", StringComparison.OrdinalIgnoreCase) >= 0 || arg.IndexOf("gallery", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            }
            return true;
        }

        private static GUIStyle _title, _brand, _heading, _text, _signature, _link, _continue;
        private static int _sizedFor;

        /// <summary>Draws it over the whole window (in IMGUI's scaled units). Returns true when a click or a key asks to go on.</summary>
        public static bool Draw(float width, float height)
        {
            MenuStyle.Fill(new Rect(0f, 0f, width, height), Color.black);
            float column = Mathf.Min(1000f, width - 80f);
            float bottomRoom = 70f;

            // The largest text that fits: shrink every size together until the whole column fits above the prompt.
            float scale = 1f, total = 0f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Size(scale);
                total = Measure(column);
                if (total <= height - bottomRoom - 40f) break;
                scale *= 0.92f;
            }

            float x = (width - column) * 0.5f;
            float y = Mathf.Max(24f, (height - bottomRoom - total) * 0.5f);
            Event e = Event.current;
            bool onLink = false;
            foreach (var (kind, text) in Lines)
            {
                GUIStyle style = StyleOf(kind);
                y += GapBefore(kind) * scale;
                if (kind == Kind.Brand)
                {
                    // As the main menu's title: its font and colour, spaced, with the accent line under it.
                    float spacing = BrandSpacing(scale), wide = SpacedWidth(text, _brand, spacing), tall = BrandHeight();
                    float left = x + (column - wide) * 0.5f;
                    MenuStyle.SpacedText(left, y, text, _brand, spacing, Color.white);
                    MenuStyle.FadeRight(new Rect(left, y + tall + 4f * scale, wide, 2f), MenuStyle.Accent);
                    y += tall + 10f * scale;
                    continue;
                }
                float h = style.CalcHeight(new GUIContent(text), column);
                var rect = new Rect(x, y, column, h);
                if (kind == Kind.Link)
                {
                    Vector2 size = style.CalcSize(new GUIContent(text));
                    var hit = new Rect(x + (column - size.x) * 0.5f, y, size.x, h);
                    bool hover = hit.Contains(e.mousePosition);
                    GUI.Label(rect, text, style);
                    if (hover) MenuStyle.Fill(new Rect(hit.x, hit.yMax - 3f, hit.width, 1.5f), new Color(1f, 1f, 1f, 0.8f));
                    if (hover && e.type == EventType.MouseDown)
                    {
                        Application.OpenURL(Link);
                        e.Use();
                        onLink = true;
                    }
                }
                else
                {
                    GUI.Label(rect, text, style);
                }
                y += h;
            }

            // The prompt, glowing: a soft halo of copies under it, both pulsing.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            var prompt = new Rect(0f, height - bottomRoom, width, 40f);
            Color keep = GUI.color;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                GUI.color = new Color(0.55f, 0.8f, 1f, 0.12f + 0.16f * pulse);
                GUI.Label(new Rect(prompt.x + Mathf.Cos(angle) * 2.5f, prompt.y + Mathf.Sin(angle) * 2.5f, prompt.width, prompt.height), Continue, _continue);
            }
            GUI.color = new Color(1f, 1f, 1f, 0.55f + 0.45f * pulse);
            GUI.Label(prompt, Continue, _continue);
            GUI.color = keep;

            if (onLink) return false;
            if (e.type == EventType.MouseDown || e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
            {
                e.Use();
                return true;
            }
            return false;
        }

        private static float BrandSpacing(float scale) => 7f * scale * BrandSize / 108f; // The menu spaces its 108-point title by 7.

        private static float BrandHeight() => _brand.CalcSize(new GUIContent("H")).y;

        /// <summary>How wide MenuStyle.SpacedText draws this text.</summary>
        private static float SpacedWidth(string text, GUIStyle style, float spacing)
        {
            float width = 0f;
            foreach (char letter in text) width += style.CalcSize(new GUIContent(letter.ToString())).x + spacing;
            return width - spacing;
        }

        private const int BrandSize = 66;

        private static float GapBefore(Kind kind) => kind == Kind.Brand ? 4f : kind == Kind.Heading ? 22f : kind == Kind.Signature ? 22f : kind == Kind.Link ? 10f : kind == Kind.Text ? 6f : 0f;

        private static GUIStyle StyleOf(Kind kind) => kind == Kind.Title ? _title : kind == Kind.Heading ? _heading : kind == Kind.Signature ? _signature : kind == Kind.Link ? _link : _text;

        private static float Measure(float column)
        {
            float total = 0f;
            foreach (var (kind, text) in Lines) total += GapBefore(kind) + (kind == Kind.Brand ? BrandHeight() + 10f * _sizedFor / 1000f : StyleOf(kind).CalcHeight(new GUIContent(text), column));
            return total;
        }

        private static void Size(float scale)
        {
            int key = Mathf.RoundToInt(scale * 1000f);
            if (_title != null && key == _sizedFor) return;
            _sizedFor = key;
            GUIStyle Make(GUIStyle from, int size, FontStyle fontStyle, TextAnchor anchor)
            {
                var style = new GUIStyle(from ?? GUI.skin.label) { fontSize = Mathf.Max(10, Mathf.RoundToInt(size * scale)), fontStyle = fontStyle, alignment = anchor, wordWrap = true, richText = false };
                style.normal.textColor = Color.white;
                style.hover.textColor = Color.white;
                style.padding = new RectOffset(0, 0, 0, 0);
                style.margin = new RectOffset(0, 0, 0, 0);
                return style;
            }
            _title = Make(MenuStyle.Heading, 26, FontStyle.Normal, TextAnchor.UpperCenter);
            _brand = new GUIStyle(MenuStyle.Title) { fontSize = Mathf.Max(20, Mathf.RoundToInt(BrandSize * scale)) }; // The menu title's own font and colour (its ink).
            _heading = Make(MenuStyle.Heading, 23, FontStyle.Normal, TextAnchor.UpperCenter);
            _text = Make(MenuStyle.Body, 19, FontStyle.Normal, TextAnchor.UpperCenter);
            _signature = Make(MenuStyle.Body, 19, FontStyle.Bold, TextAnchor.UpperCenter);
            _link = Make(MenuStyle.Body, 16, FontStyle.Normal, TextAnchor.UpperCenter);
            _link.wordWrap = false;
            _continue = Make(MenuStyle.Heading, 24, FontStyle.Normal, TextAnchor.MiddleCenter);
            _continue.wordWrap = false;
        }
    }
}
