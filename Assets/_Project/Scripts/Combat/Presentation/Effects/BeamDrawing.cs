// BeamDrawing.cs
// How a great beam looks (D98): the Scarab's focus beam in battle (CombatEffectsView) and in the main menu (MenuCinematic) are drawn by the
// same code, through an EffectKit. All of it is worked out from the clock each frame, so it costs no objects and nothing is left behind.
//   Charging: energy gathers at the mouth. Motes of light spiral in from all round, faster as the charge builds, each with a short tail;
//   three arcs of energy coil in toward it; a ring of light closes round it; the glow at its heart swells and brightens to white, green
//   at its edges, and the mouth's two sides (mandibles) glow green from within; a thin wavering line reaches toward what it will burn,
//   where a spot grows.
//   Firing: a thick beam with a white core; three coils of energy wind round it, turning as they go; bright pulses run down it from the
//   mouth to the target; sparks of energy peel off its length; at the mouth a flare with turning rays, the mandibles burning green;
//   at the far end a burning spot that throws sparks, fire and smoke, flares, and leaves the ground scorched.
// Looks only. Depends on: EffectKit, EffectRecipes, UnityEngine.

using UnityEngine;

namespace Halobellum.Presentation
{
    public static class BeamDrawing
    {
        /// <summary>The green that burns inside a Scarab's mouth as it charges and fires.</summary>
        public static readonly Color MouthGreen = new Color(0.42f, 1f, 0.36f);

        private const int Motes = 30;
        private const int CoilPoints = 40;
        private static readonly Vector3[] Points = new Vector3[EffectKit.StrandPoints];

        /// <summary>Two directions square to the beam and to each other, for drawing round it.</summary>
        private static void Around(Vector3 axis, out Vector3 u, out Vector3 v)
        {
            u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            v = Vector3.Cross(axis, u);
        }

        private static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f) * 43758.5453f, 1f);

        private static Color Alpha(Color color, float alpha) => new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));

        /// <summary>
        /// The charge, at 0 to 1. from is the mouth, to the spot it will burn; mandibles are the two glowing places at the mouth's sides (or
        /// null); read scales the small things for the camera's distance. emit carries particles over from frame to frame.
        /// </summary>
        public static void Charge(EffectKit kit, Vector3 from, Vector3 to, float charge, float radius, Color color, float read, float time,
            float delta, ref float emit, Vector3[] mandibles = null)
        {
            float c = Mathf.Clamp01(charge);
            Vector3 axis = to - from;
            if (axis.sqrMagnitude < 1e-4f) axis = Vector3.forward;
            axis.Normalize();
            Around(axis, out Vector3 u, out Vector3 v);
            float throb = 0.82f + 0.18f * Mathf.Sin(time * Mathf.Lerp(12f, 40f, c));

            // The thin wavering line toward the target, and the spot that grows there.
            kit.Line(from, to, Mathf.Lerp(0.05f, 0.32f, c) * read * throb, Alpha(color, 0.18f + 0.4f * c));
            kit.GroundHead(to, radius * 2.6f * c, Alpha(color, 0.7f));

            // The heart of it: swelling, white within, green at the rim.
            kit.HeadGlow(from, Mathf.Lerp(1.2f, 6f, c * c) * read * throb, Alpha(color, 0.55f + 0.35f * c));
            kit.HeadGlow(from, Mathf.Lerp(1.6f, 4.2f, c) * read, Alpha(MouthGreen, 0.25f + 0.45f * c));
            kit.HeadGlow(from, Mathf.Lerp(0.35f, 2.2f, c * c) * read * throb, Color.white);
            Mandibles(kit, mandibles, (0.25f + 0.75f * c) * throb, read);

            // Motes spiralling in from all round, each on its own path, faster as the charge builds.
            float pace = Mathf.Lerp(0.55f, 1.6f, c);
            for (int i = 0; i < Motes; i++)
            {
                float seed = Hash(i);
                float age = Mathf.Repeat(time * pace * (0.8f + 0.4f * seed) + seed * 7.13f, 1f);
                Vector3 now = Spiral(from, axis, u, v, i, seed, age, c, time, read);
                Vector3 before = Spiral(from, axis, u, v, i, seed, Mathf.Max(0f, age - 0.06f), c, time, read);
                float fade = Mathf.Sin(age * Mathf.PI) * (0.35f + 0.65f * c);
                Color mote = Color.Lerp(MouthGreen, color, seed);
                kit.HeadGlow(now, (0.28f + 0.3f * c) * read, Alpha(mote, fade));
                Vector3 tail = now - before;
                if (tail.sqrMagnitude > 1e-4f) kit.HeadStreak(now - tail * 0.5f, tail.normalized, tail.magnitude + 0.2f * read, 0.12f * read, Alpha(Color.Lerp(mote, Color.white, 0.35f), fade * 0.9f));
            }

            // Three arcs of energy coiling in toward the mouth, turning.
            for (int s = 0; s < 3; s++)
            {
                float start = s * Mathf.PI * 2f / 3f + time * Mathf.Lerp(2.5f, 7f, c);
                for (int k = 0; k < CoilPoints; k++)
                {
                    float f = k / (CoilPoints - 1f); // 0 at the outer end, 1 at the mouth.
                    float reach = Mathf.Lerp(4.2f, 0.15f, f) * read * (0.75f + 0.25f * c);
                    float angle = start + f * Mathf.PI * 2.2f;
                    Points[k] = from + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * reach - axis * (1f - f) * 1.4f * read;
                }
                kit.Strand(Points, CoilPoints, (0.1f + 0.22f * c) * read, Alpha(Color.Lerp(color, MouthGreen, 0.35f + 0.3f * Mathf.Sin(time * 3f + s)), 0.25f + 0.55f * c));
            }

            // A ring of light closing round the mouth, again and again, faster as the charge builds.
            float closing = Mathf.Repeat(time * Mathf.Lerp(0.9f, 2.4f, c), 1f);
            Ring(kit, from + axis * 0.2f, u, v, Mathf.Lerp(3.2f, 0.4f, closing) * read, (0.08f + 0.14f * c) * read, Alpha(Color.Lerp(color, Color.white, 0.4f), (1f - closing) * (0.3f + 0.6f * c)));

            // And loose sparks of it, drawn in.
            emit += delta * Mathf.Lerp(14f, 40f, c);
            while (emit >= 1f)
            {
                emit -= 1f;
                Vector3 away = Random.onUnitSphere * Random.Range(2.2f, 4.5f) * read;
                Vector3 swirl = Vector3.Cross(axis, away).normalized * Random.Range(2f, 5f);
                kit.Orb(EffectPriority.Medium, from + away, -away * 2.2f + swirl, 0.22f * read, Random.value < 0.4f ? MouthGreen : color, 0.42f);
            }
        }

        /// <summary>Where mote i is at age 0 to 1 of its fall into the mouth: round and round, closing in.</summary>
        private static Vector3 Spiral(Vector3 from, Vector3 axis, Vector3 u, Vector3 v, int i, float seed, float age, float charge, float time, float read)
        {
            float reach = Mathf.Lerp(4.6f, 0.15f, age * age) * (0.7f + 0.5f * seed) * read;
            float angle = seed * Mathf.PI * 2f + age * Mathf.Lerp(4f, 9f, charge) + time * 0.8f;
            float ahead = (Hash(i + 57) - 0.5f) * 3f * (1f - age) * read; // Some come from in front of the mouth, some from behind.
            return from + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * reach + axis * ahead;
        }

        private static void Ring(EffectKit kit, Vector3 middle, Vector3 u, Vector3 v, float reach, float width, Color color)
        {
            const int count = 33;
            for (int k = 0; k < count; k++)
            {
                float angle = k / (count - 1f) * Mathf.PI * 2f;
                Points[k] = middle + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * reach;
            }
            kit.Strand(Points, count, width, color);
        }

        private static void Mandibles(EffectKit kit, Vector3[] mandibles, float strength, float read)
        {
            if (mandibles == null) return;
            foreach (Vector3 place in mandibles)
            {
                kit.HeadGlow(place, 2.2f * read * strength, Alpha(MouthGreen, 0.55f * strength));
                kit.HeadGlow(place, 0.9f * read * strength, Alpha(Color.Lerp(MouthGreen, Color.white, 0.5f), 0.8f * strength));
            }
        }

        /// <summary>
        /// The beam itself, from the mouth to the burning spot. width is the weapon's own; radius the spot's. emit carries particles over
        /// from frame to frame; lastScorch is where the ground was last scorched.
        /// </summary>
        public static void Fire(EffectKit kit, Vector3 from, Vector3 to, float radius, float width, Color color, float read, float time,
            float delta, ref float emit, ref Vector3 lastScorch, Vector3[] mandibles = null)
        {
            Vector3 span = to - from;
            float length = span.magnitude;
            if (length < 0.01f) return;
            Vector3 axis = span / length;
            Around(axis, out Vector3 u, out Vector3 v);
            float pulse = 0.88f + 0.12f * Mathf.Sin(time * 38f) * Mathf.Sin(time * 23f);

            // The beam: a wide glow, a bright body and a white core.
            kit.Line(from, to, width * 3.8f * pulse * read, Alpha(color, 0.4f));
            kit.Line(from, to, width * 1.8f * pulse * read, Alpha(color, 0.9f));
            kit.Line(from, to, width * 0.75f * pulse * read, new Color(0.92f, 0.98f, 1f, 1f));

            // Three coils of energy winding round it, turning as they go, a little wider near the mouth. A turn every 7 m, or longer on a long
            // beam, so that each turn has a dozen points and stays round.
            int count = EffectKit.StrandPoints;
            float turns = length / Mathf.Max(7f, length * 12f / (count - 1));
            for (int s = 0; s < 3; s++)
            {
                float start = s * Mathf.PI * 2f / 3f - time * 9f;
                for (int k = 0; k < count; k++)
                {
                    float f = k / (count - 1f);
                    float angle = start + f * turns * Mathf.PI * 2f;
                    float reach = width * read * Mathf.Lerp(2.1f, 1.25f, f) * (0.9f + 0.1f * Mathf.Sin(time * 17f + f * 20f + s));
                    Points[k] = from + span * f + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * reach;
                }
                Color coil = s == 1 ? Color.Lerp(color, MouthGreen, 0.55f) : Color.Lerp(color, Color.white, 0.45f);
                kit.Strand(Points, count, width * 0.35f * read, Alpha(coil, 0.75f));
            }

            // Bright pulses running down it, from the mouth to the target.
            for (int k = 0; k < 6; k++)
            {
                float f = Mathf.Repeat(time * 1.9f + k / 6f, 1f);
                Vector3 at = from + span * f;
                kit.HeadGlow(at, width * 3.2f * read * (0.7f + 0.3f * Mathf.Sin(time * 30f + k)), Alpha(Color.Lerp(color, Color.white, 0.5f), 0.85f * Mathf.Sin(f * Mathf.PI) + 0.15f));
                kit.HeadStreak(at, axis, width * 6f * read, width * 1.1f * read, Alpha(Color.white, 0.6f));
            }

            // At the mouth: a flare with turning rays, and the mandibles burning green.
            kit.HeadGlow(from, 5.5f * pulse * read, color);
            kit.HeadGlow(from, 3.2f * pulse * read, Alpha(MouthGreen, 0.65f));
            kit.HeadGlow(from, 1.8f * read, Color.white);
            for (int r = 0; r < 6; r++)
            {
                float angle = r * Mathf.PI / 3f + time * 2.4f;
                Vector3 out1 = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                kit.HeadStreak(from + out1 * 1.4f * read, out1, (2.2f + 0.8f * Mathf.Sin(time * 11f + r)) * read, 0.22f * read, Alpha(Color.Lerp(color, Color.white, 0.5f), 0.7f));
            }
            Mandibles(kit, mandibles, pulse, read);

            // The burning spot.
            kit.HeadGlow(to + Vector3.up * 0.6f, radius * 3.4f * pulse, new Color(1f, 0.88f, 0.65f, 0.95f));
            kit.HeadGlow(to + Vector3.up * 0.4f, radius * 2.2f * pulse, Alpha(color, 0.8f));
            kit.GroundHead(to, radius * 4.5f * pulse, new Color(1f, 0.52f, 0.16f, 1f));

            emit += delta * 75f;
            while (emit >= 1f)
            {
                emit -= 1f;
                float roll = Random.value;
                if (roll < 0.22f)
                {
                    // A spark of energy peeling off the beam's length.
                    Vector3 on = from + span * Random.value;
                    Vector3 off = (u * Random.Range(-1f, 1f) + v * Random.Range(-1f, 1f)).normalized;
                    kit.Orb(EffectPriority.Medium, on + off * width * read, off * Random.Range(2f, 6f) + axis * Random.Range(4f, 12f), 0.18f * read, Random.value < 0.35f ? MouthGreen : color, Random.Range(0.25f, 0.5f));
                    continue;
                }
                Vector2 around = Random.insideUnitCircle * radius;
                Vector3 spot = to + new Vector3(around.x, 0.2f, around.y);
                if (roll < 0.62f) kit.Spark(EffectPriority.Big, spot, Random.onUnitSphere * 9f + Vector3.up * 11f, 0.085f * kit.ReadScale, EffectRecipes.SparkHot, Random.Range(0.35f, 0.9f));
                else if (roll < 0.88f) kit.Fire(EffectPriority.Big, spot, Vector3.up * Random.Range(3f, 8f) + Random.insideUnitSphere * 2f, radius * Random.Range(0.6f, 1f), Random.Range(0.35f, 0.7f));
                else kit.Smoke(EffectPriority.Medium, spot, Vector3.up * Random.Range(3f, 6f), radius * Random.Range(1f, 1.5f), EffectRecipes.SmokeDark, Random.Range(1.2f, 2.4f));
            }
            if ((to - lastScorch).sqrMagnitude > 1.5f)
            {
                lastScorch = to;
                kit.Scorch(EffectPriority.Medium, to, radius * 2.2f, EffectRecipes.ScorchBlack, 35f);
            }
        }
    }
}
