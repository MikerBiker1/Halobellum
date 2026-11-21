// TechIcons.cs
// The small tech icons over squads during deployment (D145): a white sign on a dark disc with a light rim, one per kind of effect
// (TechIconKind): a sniper's reticle for range, three bullets for rate of fire, a bullet through a wall for damage, an armour plate for
// health, running legs for speed, a dome for a shield, a blast for splash, a flame's cone, an aircraft in a reticle for anti-air, a squad
// coming down for a drop, a bullet and a plus for an extra shot, a clock for reload, a gear for anything else. Each is drawn once, in code, from
// simple shapes on a 512-unit square (4 x 4 samples a pixel for smooth edges), into a 64 x 64 texture, and kept.
// DrawRow draws a squad's icons in a row centred just above a point (a squad's top on screen).
// Depends on: Halobellum.Core (TechUpgrade, TechIconKind), UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.UI
{
    public static class TechIcons
    {
        private const int Size = 64;
        private const float U = 512f; // Drawing units across the icon.
        private const float M = U / 2f;
        private static readonly Dictionary<TechIconKind, Texture2D> Cache = new Dictionary<TechIconKind, Texture2D>();

        /// <summary>The icon for this kind, drawn the first time it is asked for.</summary>
        public static Texture2D Icon(TechIconKind kind)
        {
            if (Cache.TryGetValue(kind, out Texture2D texture) && texture != null) return texture;
            texture = Draw(kind);
            Cache[kind] = texture;
            return texture;
        }

        public static Texture2D Icon(TechUpgrade tech) => Icon(TechIconKinds.Of(tech));

        /// <summary>
        /// A squad's tech icons in a row, centred on x, their bottom at bottom (HUD coordinates). Returns the row's rectangle (empty if
        /// there are none). Hovering an icon shows the tech's name through tooltip, when given.
        /// </summary>
        public static Rect DrawRow(float x, float bottom, IReadOnlyList<TechUpgrade> techs, float size, List<(Rect rect, TechUpgrade tech)> drawn = null)
        {
            if (techs == null || techs.Count == 0) return Rect.zero;
            const float gap = 2f;
            float width = techs.Count * size + (techs.Count - 1) * gap;
            float left = x - width * 0.5f;
            for (int i = 0; i < techs.Count; i++)
            {
                var rect = new Rect(left + i * (size + gap), bottom - size, size, size);
                GUI.DrawTexture(rect, Icon(techs[i]), ScaleMode.StretchToFill, alphaBlend: true);
                drawn?.Add((rect, techs[i]));
            }
            return new Rect(left, bottom - size, width, size);
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>One shape: whether a point (drawing units, y down) is inside, and how much white it lays there (0 cuts it out).</summary>
        private readonly struct Shape
        {
            public readonly Func<float, float, bool> Inside;
            public readonly float Alpha;
            public Shape(Func<float, float, bool> inside, float alpha) { Inside = inside; Alpha = alpha; }
        }

        private static Texture2D Draw(TechIconKind kind)
        {
            var shapes = new List<Shape>();
            Glyph(kind, shapes);
            var pixels = new Color32[Size * Size];
            const int samples = 4;
            const float inner = 0.72f; // The sign fills this share of the disc.
            float offset = U * (1f - inner) * 0.5f;
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    float discA = 0f, rimA = 0f, glyphA = 0f;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float x = (px + (sx + 0.5f) / samples) * U / Size, y = (py + (sy + 0.5f) / samples) * U / Size;
                            float r = Mathf.Sqrt((x - M) * (x - M) + (y - M) * (y - M));
                            if (r <= M - 8f) discA += 1f;
                            if (r <= M - 8f && r >= M - 26f) rimA += 1f;
                            glyphA += Coverage(shapes, (x - offset) / inner, (y - offset) / inner);
                        }
                    }
                    float n = samples * samples;
                    discA /= n; rimA /= n; glyphA /= n;
                    // Dark disc, its light rim, the white sign over both.
                    float a = discA * 0.88f;
                    float white = Mathf.Clamp01(rimA * 0.6f + glyphA);
                    float alpha = Mathf.Clamp01(a + white * (1f - a));
                    float lum = alpha > 0f ? Mathf.Lerp(0.08f, 1f, Mathf.Clamp01(white / alpha)) : 0f; // The white's share of what covers the pixel.
                    byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(lum) * 255f);
                    // Texture rows go up; drawing rows go down.
                    pixels[(Size - 1 - py) * Size + px] = new Color32(g, g, (byte)Mathf.Min(255, g + 6), (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: true) { name = "Tech_" + kind, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: false); // Readable: tiny, and its test saves a sheet of them.
            return texture;
        }

        /// <summary>How much white lies at this point: each shape in turn lays its alpha there, or a cut (alpha 0) clears it.</summary>
        private static float Coverage(List<Shape> shapes, float x, float y)
        {
            float cover = 0f;
            foreach (Shape shape in shapes)
            {
                if (!shape.Inside(x, y)) continue;
                cover = shape.Alpha <= 0f ? 0f : Mathf.Max(cover, shape.Alpha);
            }
            return cover;
        }

        // Shapes (drawing units, y down).
        private static Func<float, float, bool> Circle(float cx, float cy, float r) => (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
        private static Func<float, float, bool> Ring(float cx, float cy, float r, float width) =>
            (x, y) => Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r) <= width * 0.5f;
        private static Func<float, float, bool> Box(float x0, float y0, float x1, float y1) => (x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
        private static Func<float, float, bool> RoundRect(float x0, float y0, float x1, float y1, float radius) => (x, y) =>
        {
            float dx = Mathf.Max(x0 + radius - x, 0f, x - (x1 - radius)), dy = Mathf.Max(y0 + radius - y, 0f, y - (y1 - radius));
            return x >= x0 && x <= x1 && y >= y0 && y <= y1 && dx * dx + dy * dy <= radius * radius;
        };
        private static Func<float, float, bool> Line(float ax, float ay, float bx, float by, float width) => (x, y) =>
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / Mathf.Max(1e-4f, vx * vx + vy * vy));
            float dx = x - (ax + vx * t), dy = y - (ay + vy * t);
            return dx * dx + dy * dy <= width * width * 0.25f;
        };
        private static Func<float, float, bool> Polygon(params Vector2[] points) => (x, y) =>
        {
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[i], b = points[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        };

        /// <summary>A bullet standing on (x, y), w wide and h tall, its nose up, turned clockwise by angle (radians).</summary>
        private static Func<float, float, bool> Bullet(float x, float y, float w, float h, float angle = 0f)
        {
            Vector2[] shape = { new Vector2(-w / 2, 0), new Vector2(w / 2, 0), new Vector2(w / 2, -h * 0.62f), new Vector2(w * 0.3f, -h * 0.85f),
                                new Vector2(0, -h), new Vector2(-w * 0.3f, -h * 0.85f), new Vector2(-w / 2, -h * 0.62f) };
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            var turned = new Vector2[shape.Length];
            for (int i = 0; i < shape.Length; i++) turned[i] = new Vector2(x + shape[i].x * c - shape[i].y * s, y + shape[i].x * s + shape[i].y * c);
            return Polygon(turned);
        }

        private static Vector2 P(float x, float y) => new Vector2(x, y);

        private static void Glyph(TechIconKind kind, List<Shape> s)
        {
            void Add(Func<float, float, bool> f, float alpha = 1f) => s.Add(new Shape(f, alpha));
            void Cut(Func<float, float, bool> f) => s.Add(new Shape(f, 0f));
            switch (kind)
            {
                case TechIconKind.Range: // A sniper's reticle.
                    Add(Ring(M, M, 137f, 26f));
                    Add(Line(M, M - 200, M, M - 60, 24)); Add(Line(M, M + 60, M, M + 200, 24));
                    Add(Line(M - 200, M, M - 60, M, 24)); Add(Line(M + 60, M, M + 200, M, 24));
                    foreach (float t in new[] { -100f, 100f })
                    {
                        Add(Line(M + t, M - 18, M + t, M + 18, 14));
                        Add(Line(M - 18, M + t, M + 18, M + t, 14));
                    }
                    Add(Circle(M, M, 16));
                    break;
                case TechIconKind.FireRate: // Three bullets.
                    for (int i = 0; i < 3; i++) Add(Bullet(M - 110 + i * 110, M + 150, 70, 300));
                    break;
                case TechIconKind.Damage: // A wall broken in the middle, shards flying, and a bullet through the gap (the owner's picture, D145).
                {
                    // The wall's top half, its broken end splintered; the bottom half is its mirror.
                    Vector2[] top = { P(M - 160, M - 235), P(M - 85, M - 235), P(M - 85, M - 100), P(M - 50, M - 72), P(M - 92, M - 74), P(M - 108, M - 52),
                                      P(M - 124, M - 82), P(M - 175, M - 58), P(M - 160, M - 96) };
                    Vector2[][] shards =
                    {
                        new[] { P(M - 52, M - 112), P(M - 18, M - 150), P(M - 32, M - 96) },
                        new[] { P(M - 22, M - 82), P(M + 28, M - 128), P(M + 2, M - 70) },
                        new[] { P(M + 30, M - 84), P(M + 58, M - 96), P(M + 44, M - 70) },
                    };
                    foreach (bool mirrored in new[] { false, true })
                    {
                        Vector2 F(Vector2 v) => mirrored ? P(v.x, 2f * M - v.y) : v;
                        Add(Polygon(System.Array.ConvertAll(top, F)));
                        foreach (Vector2[] shard in shards) Add(Polygon(System.Array.ConvertAll(shard, F)));
                    }
                    Add(Bullet(M - 60, M, 76, 285, Mathf.PI / 2f)); // Nose to the right.
                    break;
                }
                case TechIconKind.Health: // An armour plate with rivets and a cross.
                    Add(RoundRect(M - 160, M - 170, M + 160, M + 170, 40));
                    foreach (float x in new[] { M - 120, M + 120 }) foreach (float y in new[] { M - 130, M + 130 }) Cut(Circle(x, y, 16));
                    Cut(Box(M - 30, M - 110, M + 30, M + 110));
                    Cut(Box(M - 110, M - 30, M + 110, M + 30));
                    break;
                case TechIconKind.Speed: // Running legs, with speed lines.
                {
                    Vector2 hip = P(M + 10, M - 120);
                    Add(Circle(hip.x, hip.y, 40));
                    (Vector2 knee, Vector2 foot, Vector2 toe)[] legs = { (P(M + 110, M), P(M + 60, M + 150), P(M + 130, M + 150)), (P(M - 50, M - 10), P(M - 170, M + 90), P(M - 120, M + 130)) };
                    foreach (var leg in legs)
                    {
                        Add(Line(hip.x, hip.y, leg.knee.x, leg.knee.y, 56));
                        Add(Line(leg.knee.x, leg.knee.y, leg.foot.x, leg.foot.y, 50));
                        Add(Line(leg.foot.x, leg.foot.y, leg.toe.x, leg.toe.y, 44));
                    }
                    for (int k = 0; k < 3; k++) Add(Line(M - 220, M - 140 + k * 55, M - 120, M - 140 + k * 55, 16));
                    break;
                }
                case TechIconKind.Shield: // A dome over a figure.
                {
                    Func<float, float, bool> ring = Ring(M, M + 20, 173, 34);
                    Add((x, y) => y <= M + 20 && ring(x, y));
                    Add(Line(M - 200, M + 20, M + 200, M + 20, 24));
                    Add(Circle(M, M - 46, 34));
                    Add(RoundRect(M - 44, M - 8, M + 44, M + 14, 10));
                    break;
                }
                case TechIconKind.Splash: // A blast.
                {
                    var points = new Vector2[16];
                    for (int i = 0; i < 16; i++)
                    {
                        float r = i % 2 == 0 ? 200f : 95f, a = i * Mathf.PI / 8f - Mathf.PI / 2f;
                        points[i] = P(M + r * Mathf.Cos(a), M + r * Mathf.Sin(a));
                    }
                    Add(Polygon(points));
                    Cut(Circle(M, M, 55));
                    break;
                }
                case TechIconKind.Cone: // A nozzle and a widening cone of flame, its far edge in tongues.
                {
                    Add(Box(M - 215, M - 32, M - 135, M + 32));
                    var flame = new List<Vector2> { P(M - 135, M - 22) };
                    for (int i = 0; i <= 6; i++)
                    {
                        float y = M - 175 + i * (350f / 6f);
                        flame.Add(P(i % 2 == 0 ? M + 215 : M + 130, y));
                    }
                    flame.Add(P(M - 135, M + 22));
                    Add(Polygon(flame.ToArray()));
                    Cut(Polygon(P(M - 60, M), P(M + 90, M - 70), P(M + 60, M), P(M + 90, M + 70))); // A dark tongue inside, so it reads as fire.
                    break;
                }
                case TechIconKind.AntiAir: // An aircraft in a reticle.
                    Add(Ring(M, M, 179, 22));
                    Add(Polygon(P(M, M - 150), P(M + 22, M - 60), P(M + 150, M + 10), P(M + 150, M + 45), P(M + 22, M + 15), P(M + 18, M + 95), P(M + 70, M + 130),
                                P(M + 70, M + 150), P(M, M + 125), P(M - 70, M + 150), P(M - 70, M + 130), P(M - 18, M + 95), P(M - 22, M + 15), P(M - 150, M + 45),
                                P(M - 150, M + 10), P(M - 22, M - 60)));
                    break;
                case TechIconKind.Drop: // Three troops under an arrow coming down.
                    Add(Polygon(P(M - 70, M - 200), P(M + 70, M - 200), P(M + 70, M - 90), P(M + 140, M - 90), P(M, M + 20), P(M - 140, M - 90), P(M - 70, M - 90)));
                    foreach (float x in new[] { M - 130, M, M + 130 })
                    {
                        Add(Circle(x, M + 80, 30));
                        Add(RoundRect(x - 40, M + 115, x + 40, M + 200, 18));
                    }
                    break;
                case TechIconKind.ExtraShots: // One more shot: a bullet and a plus.
                    Add(Bullet(M - 80, M + 175, 110, 350));
                    Cut(Box(M - 135, M + 110, M - 25, M + 128));
                    Add(Line(M + 60, M - 20, M + 200, M - 20, 44));
                    Add(Line(M + 130, M - 90, M + 130, M + 50, 44));
                    break;
                case TechIconKind.Reload: // A clock.
                    Add(Ring(M, M, 165, 30));
                    Add(Line(M, M, M, M - 120, 30));
                    Add(Line(M, M, M + 90, M + 50, 30));
                    Add(Circle(M, M, 26));
                    break;
                default: // A gear.
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Add(Line(M + 120 * Mathf.Cos(a), M + 120 * Mathf.Sin(a), M + 190 * Mathf.Cos(a), M + 190 * Mathf.Sin(a), 70));
                    }
                    Add(Circle(M, M, 140));
                    Cut(Circle(M, M, 60));
                    break;
            }
        }
    }
}
