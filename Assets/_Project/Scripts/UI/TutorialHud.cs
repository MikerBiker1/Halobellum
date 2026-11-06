// TutorialHud.cs
// The tutorial's panel and outline (D77), drawn by MatchHud over everything else when the match has a TutorialDirector.
// The HUD marks where the things a step can point at are drawn (Mark: a button by its key, an area by its name), in screen space,
// on every repaint while the tutorial runs; the outline goes round the current step's target and pulses. "squad" outlines the
// player's squads on the field. The panel sits at the top centre, or at the bottom when the target is up there; it says the step,
// what to do, and has Next (for a step that only explains) and Skip tutorial.
// Depends on: Halobellum.Presentation (MatchController, TutorialDirector, TutorialStep, UiHitAreas), HudScale, HudTheme, UnityEngine.

using System.Collections.Generic;
using Halobellum.Economy;
using Halobellum.Presentation;
using UnityEngine;

namespace Halobellum.UI
{
    public static class TutorialHud
    {
        public const string NextLabel = "Next", SkipLabel = "Skip tutorial";
        private const float PanelWidth = 640f;
        private static readonly Dictionary<string, (Rect rect, int frame)> Marks = new Dictionary<string, (Rect, int)>();

        /// <summary>True while a tutorial runs: the HUD only marks then.</summary>
        public static bool Active { get; set; }

        /// <summary>Where something a step can point at is drawn: "key:..." or "area:...", with a rect in the current GUI space.</summary>
        public static void Mark(string target, Rect rect)
        {
            if (!Active || Event.current == null || Event.current.type != EventType.Repaint) return;
            Marks[target] = (GUIUtility.GUIToScreenRect(rect), Time.frameCount);
        }

        /// <summary>The same, for a rect worked out by hand in the HUD's scaled space (inside a scroll view, where Unity's own conversion goes wrong).</summary>
        public static void MarkScaled(string target, Rect rect, float scale)
        {
            if (!Active || Event.current == null || Event.current.type != EventType.Repaint) return;
            Marks[target] = (HudScale.ToScreen(rect, scale), Time.frameCount);
        }

        /// <summary>The target's rect on screen (GUI space at scale 1), if it was drawn just now.</summary>
        public static bool TryFind(MatchController match, string target, out Rect rect)
        {
            rect = default;
            if (string.IsNullOrEmpty(target)) return false;
            if (target == "squad") return SquadsOnScreen(match, out rect);
            if (!Marks.TryGetValue(target, out var mark) || mark.frame < Time.frameCount - 2) return false;
            rect = mark.rect;
            return true;
        }

        private static bool SquadsOnScreen(MatchController match, out Rect rect)
        {
            rect = default;
            bool any = false;
            foreach (PlacedSquad squad in match.Match.Player.Squads)
            {
                if (!match.SquadOnScreen(squad, out Vector3 screen)) continue;
                var around = new Rect(screen.x - 36f, Screen.height - screen.y - 36f, 72f, 72f);
                rect = any ? Union(rect, around) : around;
                any = true;
            }
            return any;
        }

        private static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        /// <summary>The panel and the outline. GUI.matrix is the HUD's scale on entry, and is left so.</summary>
        public static void Draw(MatchController match, float scale, GUIStyle title, GUIStyle text, GUIStyle small, GUIStyle button)
        {
            TutorialDirector tutorial = match.Tutorial;
            TutorialStep step = tutorial?.Current;
            if (step == null) return;
            float width = HudScale.ScaledWidth(scale), height = HudScale.ScaledHeight(scale);

            bool found = TryFind(match, step.Target, out Rect target);
            if (found && Event.current.type == EventType.Repaint)
            {
                Matrix4x4 old = GUI.matrix;
                GUI.matrix = Matrix4x4.identity;
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
                Outline(new Rect(target.x - 6f, target.y - 6f, target.width + 12f, target.height + 12f), new Color(1f, 0.82f, 0.25f, pulse), 3f);
                GUI.matrix = old;
            }

            string doing = step.Done != null && !string.IsNullOrEmpty(step.Doing) ? step.Doing : null;
            // Out of the way of what it points at: the top centre, else the bottom centre, else narrower in an upper corner; failing all, where it covers least.
            float wide = Mathf.Min(PanelWidth, width - 40f), narrow = Mathf.Min(470f, width - 40f);
            Rect[] spots =
            {
                Spot(wide, (width - wide) * 0.5f, 104f, false, step, doing, text, height),
                Spot(wide, (width - wide) * 0.5f, 0f, true, step, doing, text, height),
                Spot(narrow, 20f, 104f, false, step, doing, text, height),
                Spot(narrow, width - narrow - 20f, 104f, false, step, doing, text, height),
            };
            Rect panel = spots[0];
            if (found)
            {
                float least = float.MaxValue;
                foreach (Rect spot in spots)
                {
                    float covered = Covered(HudScale.ToScreen(spot, scale), target);
                    if (covered < least - 1f) { least = covered; panel = spot; }
                    if (covered <= 0f) break;
                }
            }
            float inner = panel.width - 32f;
            float textHeight = text.CalcHeight(new GUIContent(step.Text), inner);
            UiHitAreas.Add(HudScale.ToScreen(panel, scale));

            HudTheme.PanelBox(panel);
            float y = panel.y + 10f;
            GUI.Label(new Rect(panel.x + 16f, y, inner, 22f), $"Tutorial - step {tutorial.Index + 1} of {tutorial.Count}", small);
            y += 24f;
            GUI.Label(new Rect(panel.x + 16f, y, inner, 32f), step.Title, title);
            y += 36f;
            GUI.Label(new Rect(panel.x + 16f, y, inner, textHeight), step.Text, text);
            y += textHeight + 4f;
            if (doing != null)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.35f);
                GUI.Label(new Rect(panel.x + 16f, y, inner, text.CalcHeight(new GUIContent(doing), inner)), doing, text);
                GUI.color = old;
            }
            var skip = new Rect(panel.x + 16f, panel.yMax - 46f, 170f, 36f);
            if (MenuStyle.Button(skip, SkipLabel, button)) tutorial.Skip();
            if (step.Done == null)
            {
                var next = new Rect(panel.xMax - 16f - 150f, panel.yMax - 46f, 150f, 36f);
                if (MenuStyle.Button(next, tutorial.Index + 1 == tutorial.Count ? "Finish" : NextLabel, button)) tutorial.Next();
            }
        }

        /// <summary>A panel of this width at x, at the top (y) or above the bottom, tall enough for the step's text.</summary>
        private static Rect Spot(float panelWidth, float x, float y, bool atBottom, TutorialStep step, string doing, GUIStyle text, float height)
        {
            float textHeight = text.CalcHeight(new GUIContent(step.Text), panelWidth - 32f);
            float doingHeight = doing != null ? text.CalcHeight(new GUIContent(doing), panelWidth - 32f) + 6f : 0f;
            float panelHeight = 30f + 34f + textHeight + doingHeight + 54f;
            return new Rect(x, atBottom ? height - panelHeight - 160f : y, panelWidth, panelHeight);
        }

        private static float Covered(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0f && h > 0f ? w * h : 0f;
        }

        private static void Outline(Rect r, Color color, float thickness)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
