// SpellEffects.cs
// What spells look like when they go off (M10). Each spell is big and looks like nothing else:
//   MAC Strike: one white-blue line from orbit, a hard flash and a crown of thrown earth, no fire.
//   HAVOK Nuke: a white flash over half the field, a fireball, shock rings, a dust wave, and a mushroom cloud with a stem and a rolling cap (MushroomCloud).
//   Plasma Bombardment: a violet column from the sky and a splash of plasma.
//   Plasma Storm: a dark violet cloud overhead, bolts and lightning coming down from it for as long as it lasts.
//   Glassing Beam: a thick blue-white beam from orbit; the ground below it goes molten and stays glowing.
//   Missile Barrage: missiles coming in from the sky on smoke trails, each with its own blast.
//   Gravemind's Wrath, Spore Burst, Blight Rain: the ground erupts in flesh and spores, or sick rain falls.
//   EMP: blue rings and lightning. Buffs: a ring and rising motes. Smoke: a real cloud over its area. Drops: streaks and dust.
// In the seconds before a spell with a long delay goes off, a warning shows (a thin line growing from the sky, or stirring spores).
// It reads the simulation's SpellTriggered event; a running spell's later stages are kept in a fixed list on battle time. Its random numbers
// are Unity's, not the simulation's, so the battle's outcome can't change. The spell's gameplay is untouched. Any random spot is looks only.
// Depends on: EffectRecipes, EffectKit, MushroomCloud, WeaponLooks (SpellLooks), CameraShake, Halobellum.Combat, Halobellum.Core, UnityEngine.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class SpellEffects
    {
        private struct Running
        {
            public SpellLook Look;
            public Vector3 Position;
            public float Radius;
            public float Started;
            public float Until;
            public float NextStep;
            public Color Color;
            /// <summary>The way the camera faced when it started: a mushroom cloud's cap is narrowed along it.</summary>
            public Vector2 Facing;
        }

        private const int MostRunning = 24;
        private const float SkyHeight = 170f;
        private const float StormHeight = 18f;
        private const float WarnSeconds = 3f;
        private const float SmokeEverySeconds = 0.55f;

        private static readonly Color MacWhite = new Color(0.82f, 0.92f, 1f);
        private static readonly Color CovenantViolet = new Color(0.62f, 0.42f, 1f);
        private static readonly Color GlassBeam = new Color(0.45f, 0.95f, 1f);
        private static readonly Color Molten = new Color(1f, 0.55f, 0.15f, 0.85f);
        private static readonly Color StormCloud = new Color(0.1f, 0.07f, 0.18f, 0.72f);
        private static readonly Color FloodGreen = new Color(0.62f, 0.95f, 0.25f);
        private static readonly Color EmpBlue = new Color(0.5f, 0.92f, 1f);

        private readonly EffectRecipes _recipes;
        private readonly EffectKit _kit;
        private readonly Running[] _running = new Running[MostRunning];
        private int _runningCount;
        private BattleSimulation _shown;
        private float _nextSmoke;

        public SpellEffects(EffectRecipes recipes)
        {
            _recipes = recipes;
            _kit = recipes.Kit;
        }

        public void Show(BattleSimulation simulation)
        {
            if (_shown != null) _shown.Events.SpellTriggered -= OnSpellTriggered;
            _shown = simulation;
            _runningCount = 0;
            _nextSmoke = 0f;
            if (_shown != null) _shown.Events.SpellTriggered += OnSpellTriggered;
        }

        private static Vector3 Ground(Vec2 position, float height = 0.14f) => new Vector3(position.X, height, position.Y);

        /// <summary>A random spot inside a circle, for looks only.</summary>
        private static Vector3 Inside(Vector3 centre, float radius)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            return new Vector3(centre.x + offset.x, centre.y, centre.z + offset.y);
        }

        private void OnSpellTriggered(SimSpell spell)
        {
            SpellStats stats = spell.Stats;
            SpellLook look = SpellLooks.For(stats);
            Vector3 at = Ground(spell.Position);
            float radius = Mathf.Max(1f, stats.Radius);
            bool first = spell.TicksDone <= 1;

            switch (look)
            {
                case SpellLook.OrbitalStrike: OrbitalStrike(at, radius); break;
                case SpellLook.Nuke: Nuke(at, radius); break;
                case SpellLook.PlasmaStrike: PlasmaStrike(at, radius); break;
                case SpellLook.PlasmaStorm:
                    if (first) Start(look, at, radius, stats.Ticks * stats.TickInterval + 1.5f, CovenantViolet);
                    StormBolts(at, radius, 4, CovenantViolet);
                    break;
                case SpellLook.Glassing:
                    if (first) Start(look, at, radius, stats.Ticks * stats.TickInterval + 0.5f, GlassBeam);
                    GlassingTick(at, radius);
                    break;
                case SpellLook.MissileRain: MissilesIn(at, radius, 3); break;
                case SpellLook.BioEruption: BioEruption(at, radius, true); break;
                case SpellLook.BioBurst: BioEruption(at, radius, false); break;
                case SpellLook.BlightRain:
                    if (first) Start(look, at, radius, stats.Ticks * stats.TickInterval + 1.5f, FloodGreen);
                    StormBolts(at, radius, 6, FloodGreen);
                    break;
                case SpellLook.Emp: Emp(at, radius); break;
                case SpellLook.SporeWave: SporeWave(at, radius); break;
                case SpellLook.Buff: Buff(at, radius, stats.ShieldBonusShare > 0f ? new Color(0.4f, 0.8f, 1f) : new Color(1f, 0.82f, 0.3f)); break;
                case SpellLook.Drop: DropIn(at, radius); break;
                case SpellLook.Smoke:
                    break; // Drawn every moment in Tick.
            }
        }

        private void Start(SpellLook look, Vector3 at, float radius, float seconds, Color color)
        {
            if (_runningCount >= MostRunning) return;
            _running[_runningCount++] = new Running { Look = look, Position = at, Radius = radius, Started = _kit.BattleTime, Until = _kit.BattleTime + seconds, Color = color, Facing = CameraFacing() };
        }

        private static Vector2 CameraFacing()
        {
            Camera camera = Camera.main;
            return camera != null ? MushroomCloud.FacingOf(camera.transform.forward) : MushroomCloud.North;
        }

        // ------------------------------------------------------------------ every frame

        public void Tick()
        {
            float now = _kit.BattleTime;
            for (int i = _runningCount - 1; i >= 0; i--)
            {
                ref Running run = ref _running[i];
                if (now >= run.Until)
                {
                    _running[i] = _running[--_runningCount];
                    continue;
                }
                UpdateRunning(ref run, now);
            }
            if (_shown == null || _shown.IsFinished) return;
            Warnings(now);
            SmokeAreas(now);
        }

        private void UpdateRunning(ref Running run, float now)
        {
            switch (run.Look)
            {
                case SpellLook.Nuke:
                    DrawMushroom(run.Position, run.Radius, now - run.Started, (int)(run.Started * 97f), run.Facing);
                    break;
                case SpellLook.PlasmaStorm:
                case SpellLook.BlightRain:
                    if (now >= run.NextStep)
                    {
                        run.NextStep = now + 0.4f;
                        StormCloudStep(run.Position, run.Radius, run.Look == SpellLook.PlasmaStorm);
                    }
                    break;
                case SpellLook.Glassing:
                {
                    // The beam itself, flickering a little, thick, with a white core.
                    Vector3 top = run.Position + new Vector3(10f, SkyHeight, 25f);
                    float pulse = 0.9f + 0.1f * Mathf.Sin(now * 34f);
                    float fade = Mathf.Clamp01((run.Until - now) / 0.4f) * Mathf.Clamp01((now - run.Started) / 0.15f);
                    _kit.Line(top, run.Position, run.Radius * 1.5f * pulse * fade, new Color(run.Color.r, run.Color.g, run.Color.b, 0.75f * fade));
                    _kit.Line(top, run.Position, run.Radius * 0.55f * pulse * fade, new Color(0.9f, 1f, 1f, fade));
                    _kit.GroundHead(run.Position, run.Radius * 3.2f * fade, new Color(1f, 0.72f, 0.4f, 0.8f * fade));
                    _kit.HeadGlow(run.Position + Vector3.up * 2f, run.Radius * 2f * fade, new Color(0.8f, 1f, 1f, 0.4f * fade));
                    break;
                }
            }
        }

        /// <summary>Spells with a long delay warn in their last seconds.</summary>
        private void Warnings(float now)
        {
            float battle = _shown.ElapsedSeconds;
            IReadOnlyList<SimSpell> spells = _shown.Spells;
            for (int i = 0; i < spells.Count; i++)
            {
                SimSpell spell = spells[i];
                SpellStats stats = spell.Stats;
                if (stats.DelaySeconds < WarnSeconds || spell.TicksDone > 0) continue;
                float left = stats.DelaySeconds - battle;
                if (left > WarnSeconds || left <= 0f) continue;
                float closeness = 1f - left / WarnSeconds;
                Vector3 at = Ground(spell.Position);
                SpellLook look = SpellLooks.For(stats);
                switch (look)
                {
                    case SpellLook.Nuke:
                    {
                        float blink = Mathf.Repeat(now * Mathf.Lerp(2f, 7f, closeness), 1f) < 0.5f ? 1f : 0.2f;
                        _kit.Line(at + new Vector3(4f, SkyHeight, 12f), at, 0.3f * _kit.ReadScale, new Color(1f, 0.2f, 0.12f, 0.8f * blink));
                        _kit.GroundHead(at, 3f * _kit.ReadScale, new Color(1f, 0.15f, 0.1f, blink));
                        break;
                    }
                    case SpellLook.Glassing:
                    {
                        Vector3 top = at + new Vector3(10f, SkyHeight, 25f);
                        _kit.Line(top, at, Mathf.Lerp(0.15f, 1f, closeness) * _kit.ReadScale, new Color(GlassBeam.r, GlassBeam.g, GlassBeam.b, 0.3f + 0.6f * closeness));
                        _kit.GroundHead(at, stats.Radius * 2f * closeness, new Color(GlassBeam.r, GlassBeam.g, GlassBeam.b, 0.55f));
                        break;
                    }
                    case SpellLook.BioEruption:
                        _kit.GroundHead(at, stats.Radius * 2f * closeness, new Color(0.45f, 0.8f, 0.15f, 0.5f));
                        if (Random.value < 0.5f) _kit.Spore(EffectPriority.Small, Inside(at, stats.Radius), Vector3.up * Random.Range(1f, 3f), 0.5f * _kit.ReadScale, EffectRecipes.SporeGreen, 1.5f);
                        break;
                }
            }
        }

        /// <summary>Smoke bombs: a real cloud over the spell's area, kept up for as long as it hangs.</summary>
        private void SmokeAreas(float now)
        {
            if (now < _nextSmoke) return;
            _nextSmoke = now + SmokeEverySeconds;
            float battle = _shown.ElapsedSeconds;
            IReadOnlyList<SimSpell> spells = _shown.Spells;
            for (int i = 0; i < spells.Count; i++)
            {
                SimSpell spell = spells[i];
                SpellStats stats = spell.Stats;
                if (stats.Effect != SpellEffect.Smoke || spell.TicksDone == 0 || spell.IsOverAt(battle)) continue;
                bool spores = (stats.Name ?? "").IndexOf("spore", System.StringComparison.OrdinalIgnoreCase) >= 0;
                Color color = spores ? new Color(0.36f, 0.44f, 0.18f, 0.36f) : new Color(0.62f, 0.63f, 0.65f, 0.34f);
                float width = stats.IsRectangle ? stats.AreaWidth : stats.Radius * 2f;
                float depth = stats.IsRectangle ? stats.AreaDepth : stats.Radius * 2f;
                int puffs = Mathf.Clamp(Mathf.RoundToInt(width * depth / 22f), 2, 8);
                for (int p = 0; p < puffs; p++)
                {
                    Vec2 local = new Vec2(Random.Range(-0.5f, 0.5f) * width, Random.Range(-0.5f, 0.5f) * depth).Rotated(spell.AngleDegrees);
                    Vector3 spot = new Vector3(spell.Position.X + local.X, Random.Range(0.4f, 1.6f), spell.Position.Y + local.Y);
                    _kit.Smoke(EffectPriority.Medium, spot, Random.insideUnitSphere * 0.5f + Vector3.down * 1.5f, Random.Range(4.5f, 7f), color, Random.Range(3f, 4.2f));
                }
            }
        }

        // ------------------------------------------------------------------ strikes

        /// <summary>A kinetic round from orbit: narrow, white, and it throws the earth up instead of burning it.</summary>
        private void OrbitalStrike(Vector3 at, float radius)
        {
            const EffectPriority huge = EffectPriority.Huge;
            Vector3 top = at + new Vector3(-16f, SkyHeight, 44f);
            _kit.FlashLine(top, at, radius * 1.6f, MacWhite, 0.45f);
            _kit.FlashLine(top, at, radius * 0.5f, Color.white, 0.3f);
            _kit.Glow(huge, at + Vector3.up * 2f, radius * 6f, MacWhite, 0.22f);
            _kit.Glow(huge, at + Vector3.up, radius * 3f, Color.white, 0.12f);
            _kit.GroundGlow(huge, at, radius * 6f, new Color(0.65f, 0.82f, 1f, 0.9f), 0.5f);
            _kit.Ring(huge, at, radius * 2f, Color.white, 0.4f);
            _kit.Ring(huge, at, radius * 4.4f, new Color(0.75f, 0.88f, 1f, 0.7f), 0.75f);
            _kit.Scorch(huge, at, radius * 2.6f, EffectRecipes.ScorchBlack, 90f);
            // The crown: earth and dust straight up, then out.
            for (int i = 0; i < 34; i++)
            {
                Vector2 out2 = Random.insideUnitCircle * radius * 0.35f;
                var way = new Vector3(out2.x * 3f, Random.Range(22f, 44f), out2.y * 3f);
                _kit.Dust(huge, at + new Vector3(out2.x, 0.5f, out2.y), way, radius * Random.Range(0.6f, 1.2f), EffectRecipes.DirtBrown, Random.Range(1f, 2.1f));
            }
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                var out3 = new Vector3(Mathf.Cos(angle), 0.08f, Mathf.Sin(angle));
                _kit.Dust(huge, at + out3 * radius * 0.5f, out3 * radius * Random.Range(4f, 7f), radius * Random.Range(0.6f, 1.1f), EffectRecipes.DustTan, Random.Range(0.8f, 1.5f));
            }
            for (int i = 0; i < 36; i++)
            {
                Vector3 out3 = Random.onUnitSphere;
                out3.y = Mathf.Abs(out3.y) + 1f;
                _kit.Chunk(huge, at, out3 * Random.Range(9f, 26f), Random.Range(0.25f, 0.8f), i % 2 == 0 ? EffectRecipes.DirtBrown : EffectRecipes.MetalDark, Random.Range(2f, 4f));
            }
            for (int i = 0; i < 40; i++) _kit.Spark(huge, at, Random.onUnitSphere * Random.Range(12f, 34f) + Vector3.up * 16f, 0.1f * _kit.ReadScale, MacWhite, Random.Range(0.4f, 1.1f));
            CameraShake.Kick(at, 0.75f);
        }

        /// <summary>The nuke: nothing else in the game is this bright or this tall.</summary>
        private void Nuke(Vector3 at, float radius)
        {
            const EffectPriority huge = EffectPriority.Huge;
            Vector3 middle = at + Vector3.up * radius * 0.35f;
            _kit.Glow(huge, middle, radius * 12f, new Color(1f, 1f, 1f, 0.9f), 0.45f);
            _kit.Glow(huge, middle, radius * 6f, new Color(1f, 0.92f, 0.7f), 0.8f);
            _kit.GroundGlow(huge, at, radius * 9f, new Color(1f, 0.62f, 0.25f, 1f), 2.2f);
            _kit.Ring(huge, at, radius * 2f, Color.white, 0.5f);
            _kit.Ring(huge, at, radius * 4f, new Color(1f, 0.85f, 0.6f, 0.85f), 1.1f);
            _kit.Ring(huge, at, radius * 7f, new Color(1f, 0.8f, 0.6f, 0.45f), 1.9f);
            _kit.Scorch(huge, at, radius * 3f, EffectRecipes.ScorchBlack, 120f);

            for (int i = 0; i < 70; i++)
            {
                Vector3 out3 = Random.onUnitSphere;
                out3.y = Mathf.Abs(out3.y) * 0.8f + 0.25f;
                _kit.Fire(huge, at + out3 * radius * 0.3f, out3 * radius * Random.Range(1.5f, 3.6f), radius * Random.Range(0.6f, 1.15f), Random.Range(1f, 2.1f));
            }
            // The ground wave of dust running out.
            for (int i = 0; i < 44; i++)
            {
                float angle = i * Mathf.PI * 2f / 44f;
                var out3 = new Vector3(Mathf.Cos(angle), 0.05f, Mathf.Sin(angle));
                _kit.Dust(huge, at + out3 * radius * 0.4f, out3 * radius * Random.Range(5f, 8f), radius * Random.Range(0.4f, 0.75f), EffectRecipes.DustTan, Random.Range(1.2f, 2.2f));
            }
            for (int i = 0; i < 100; i++) _kit.Spark(huge, at + Vector3.up, Random.onUnitSphere * radius * Random.Range(2f, 5f) + Vector3.up * radius * 2f, 0.12f * _kit.ReadScale, EffectRecipes.SparkHot, Random.Range(0.8f, 2.4f));
            for (int i = 0; i < 50; i++)
            {
                Vector3 out3 = Random.onUnitSphere;
                out3.y = Mathf.Abs(out3.y) + 0.5f;
                _kit.Chunk(huge, at, out3 * Random.Range(12f, 34f), Random.Range(0.3f, 1.1f), i % 2 == 0 ? EffectRecipes.MetalHot : EffectRecipes.MetalDark, Random.Range(2.5f, 5f));
            }
            for (int i = 0; i < 6; i++) _kit.Heat(huge, at + Vector3.up * (radius * 0.4f + i * 3f) + Random.insideUnitSphere * radius * 0.5f, Vector3.up * 5f, radius * 3.4f, 1.6f);
            Start(SpellLook.Nuke, at, radius, MushroomCloud.Seconds, Color.white);
            CameraShake.Kick(at, 1f);
        }

        /// <summary>The mushroom as it is at this moment after the blast, redrawn each frame from MushroomCloud, so it holds its shape and rolls.</summary>
        private void DrawMushroom(Vector3 at, float radius, float age, int seed, Vector2 facing)
        {
            for (int i = 0; i < MushroomCloud.SmokePuffs; i++)
            {
                MushroomCloud.Puff puff = MushroomCloud.Smoke(i, age, radius, seed, facing);
                if (puff.Color.a > 0.004f) _kit.CloudSmoke(at + puff.Offset, puff.Size, puff.Color, puff.Degrees, puff.Seed);
            }
            for (int i = 0; i < MushroomCloud.FirePuffs; i++)
            {
                MushroomCloud.Puff puff = MushroomCloud.Fire(i, age, radius, seed, facing);
                if (puff.Color.a > 0.004f) _kit.CloudFire(at + puff.Offset, puff.Size, puff.Color, puff.Degrees, puff.Seed);
            }
        }

        private void PlasmaStrike(Vector3 at, float radius)
        {
            const EffectPriority huge = EffectPriority.Huge;
            Vector3 top = at + new Vector3(8f, SkyHeight, 20f);
            _kit.FlashLine(top, at, radius * 1.7f, CovenantViolet, 0.75f);
            _kit.FlashLine(top, at, radius * 0.6f, new Color(0.9f, 0.85f, 1f), 0.55f);
            _recipes.Blast(at + Vector3.up, radius, BlastStyle.Plasma, CovenantViolet, huge);
            _kit.Glow(huge, at + Vector3.up * 2f, radius * 5f, CovenantViolet, 0.5f);
            _kit.GroundGlow(huge, at, radius * 5f, new Color(0.5f, 0.32f, 1f, 0.9f), 2.4f);
            _kit.Scorch(huge, at, radius * 2.6f, new Color(0.04f, 0.03f, 0.08f, 0.85f), 90f);
            for (int i = 0; i < 40; i++) _kit.Orb(huge, at + Vector3.up, Random.onUnitSphere * radius * Random.Range(1.5f, 4f) + Vector3.up * radius * 2f, radius * Random.Range(0.12f, 0.3f), CovenantViolet, Random.Range(0.45f, 1f));
            for (int i = 0; i < 10; i++) _kit.Smoke(huge, Inside(at, radius) + Vector3.up, Vector3.up * Random.Range(3f, 7f), radius * Random.Range(0.8f, 1.3f), new Color(0.2f, 0.14f, 0.32f, 0.45f), Random.Range(1.5f, 3f));
            CameraShake.Kick(at, 0.65f);
        }

        /// <summary>Bolts of a storm (or drops of blight) coming down from the cloud, each one landing with a small blast.</summary>
        private void StormBolts(Vector3 at, float radius, int bolts, Color color)
        {
            bool blight = color == FloodGreen;
            const float height = StormHeight;
            for (int i = 0; i < bolts; i++)
            {
                Vector3 hit = Inside(at, radius);
                Vector3 from = hit + new Vector3(Random.Range(-3f, 3f), height, Random.Range(-3f, 3f));
                if (blight)
                {
                    _kit.Bolt(EffectPriority.Big, from, (hit - from).normalized * 58f, 0.5f * _kit.ReadScale, color, height / 58f);
                    _recipes.BlastAfter(height / 58f, hit + Vector3.up * 0.3f, radius * 0.2f, BlastStyle.Bio, color, EffectPriority.Medium);
                }
                else
                {
                    Lightning(from, hit, color, 0.8f * _kit.ReadScale, 0.22f);
                    _recipes.Blast(hit + Vector3.up * 0.3f, radius * 0.3f, BlastStyle.Plasma, color, EffectPriority.Big);
                }
            }
            _kit.Glow(EffectPriority.Big, at + Vector3.up * height, radius * 2.6f, color, 0.22f);
            CameraShake.Kick(at, blight ? 0.1f : 0.2f);
        }

        private void StormCloudStep(Vector3 at, float radius, bool plasma)
        {
            Color cloud = plasma ? StormCloud : new Color(0.16f, 0.22f, 0.06f, 0.72f);
            for (int i = 0; i < 5; i++)
            {
                Vector3 spot = Inside(at, radius * 1.1f) + Vector3.up * (StormHeight + Random.Range(-1.5f, 2.5f));
                _kit.Smoke(EffectPriority.Big, spot, Random.insideUnitSphere * 1.2f + Vector3.down, radius * Random.Range(0.8f, 1.25f), cloud, Random.Range(1.8f, 2.8f));
            }
            if (plasma && Random.value < 0.6f)
            {
                Vector3 a = Inside(at, radius) + Vector3.up * StormHeight, b = Inside(at, radius) + Vector3.up * (StormHeight - 1f);
                Lightning(a, b, CovenantViolet, 0.5f * _kit.ReadScale, 0.14f);
            }
        }

        /// <summary>A jagged line, made of three straight ones.</summary>
        private void Lightning(Vector3 from, Vector3 to, Color color, float width, float life)
        {
            Vector3 way = to - from;
            float wander = way.magnitude * 0.12f;
            Vector3 a = from + way * 0.33f + Random.insideUnitSphere * wander;
            Vector3 b = from + way * 0.66f + Random.insideUnitSphere * wander;
            _kit.FlashLine(from, a, width, color, life);
            _kit.FlashLine(a, b, width, color, life);
            _kit.FlashLine(b, to, width, color, life);
        }

        private void GlassingTick(Vector3 at, float radius)
        {
            const EffectPriority huge = EffectPriority.Huge;
            // The ground under the beam boils: fire and steam rise, and the ground stays molten for a while.
            _kit.Ring(huge, at, radius * 2f, new Color(0.7f, 1f, 1f, 0.9f), 0.5f);
            _kit.GroundGlow(huge, at, radius * 3.2f, Molten, 6f);
            _kit.Scorch(huge, at, radius * 2.5f, new Color(0.02f, 0.03f, 0.03f, 0.9f), 120f);
            for (int i = 0; i < 14; i++) _kit.Fire(huge, Inside(at, radius) + Vector3.up * 0.5f, Vector3.up * Random.Range(4f, 12f) + Random.insideUnitSphere * 3f, radius * Random.Range(0.28f, 0.5f), Random.Range(0.6f, 1.2f));
            for (int i = 0; i < 26; i++) _kit.Spark(huge, Inside(at, radius * 0.8f), Random.onUnitSphere * 12f + Vector3.up * 16f, 0.1f * _kit.ReadScale, EffectRecipes.SparkHot, Random.Range(0.5f, 1.3f));
            for (int i = 0; i < 7; i++) _kit.Smoke(huge, Inside(at, radius) + Vector3.up, Vector3.up * Random.Range(4f, 8f), radius * Random.Range(0.5f, 0.9f), new Color(0.72f, 0.74f, 0.76f, 0.3f), Random.Range(1.6f, 3.2f));
            _kit.Heat(huge, at + Vector3.up * 3f, Vector3.up * 3f, radius * 2.6f, 0.9f);
            CameraShake.Kick(at, 0.35f);
        }

        /// <summary>Missiles come in along smoke trails; each has its own blast when it lands.</summary>
        private void MissilesIn(Vector3 at, float radius, int missiles)
        {
            const float Flight = 0.34f;
            for (int i = 0; i < missiles; i++)
            {
                Vector3 hit = Inside(at, radius);
                Vector3 from = hit + new Vector3(Random.Range(-14f, 14f), 40f, Random.Range(8f, 22f));
                Vector3 velocity = (hit - from) / Flight;
                _kit.Streak(EffectPriority.Big, from, velocity.normalized * 110f, 0.45f * _kit.ReadScale, WeaponLooks.RocketOrange, Flight);
                _kit.Bolt(EffectPriority.Big, from, velocity, 0.7f * _kit.ReadScale, WeaponLooks.RocketOrange, Flight);
                for (int p = 0; p < 7; p++) _kit.Smoke(EffectPriority.Medium, Vector3.Lerp(from, hit, p / 7f), Random.insideUnitSphere * 0.3f, Random.Range(1f, 1.5f), EffectRecipes.SmokeThin, Random.Range(0.9f, 1.5f));
                _recipes.BlastAfter(Flight, hit + Vector3.up * 0.5f, Mathf.Max(2.6f, radius * 0.34f), BlastStyle.Fire, Color.white, EffectPriority.Big);
            }
            CameraShake.Kick(at, 0.2f);
        }

        /// <summary>The ground bursts open in flesh and spores; for the biggest, a green wave and a cloud that hangs.</summary>
        private void BioEruption(Vector3 at, float radius, bool wrath)
        {
            const EffectPriority huge = EffectPriority.Huge;
            float scale = wrath ? 1.6f : 1f;
            _kit.Glow(huge, at + Vector3.up * 2f, radius * 5f * scale, FloodGreen, 0.35f);
            _kit.GroundGlow(huge, at, radius * 4.6f * scale, new Color(0.45f, 0.85f, 0.15f, 0.85f), 1.8f);
            _kit.Ring(huge, at, radius * 2f, new Color(0.8f, 1f, 0.45f, 1f), 0.55f);
            if (wrath) _kit.Ring(huge, at, radius * 4.2f, new Color(0.6f, 0.9f, 0.3f, 0.6f), 1.2f);
            _kit.Scorch(huge, at, radius * 2.6f, EffectRecipes.SplatterDark, 120f);
            int chunks = wrath ? 60 : 26;
            for (int i = 0; i < chunks; i++)
            {
                Vector3 spot = Inside(at, radius * 0.9f);
                _kit.Chunk(huge, spot, Vector3.up * Random.Range(10f, 30f) + Random.insideUnitSphere * 8f, Random.Range(0.3f, 1f), i % 2 == 0 ? EffectRecipes.FleshPink : EffectRecipes.FleshSick, Random.Range(2f, 4f));
            }
            int spores = wrath ? 220 : 80;
            for (int i = 0; i < spores; i++) _kit.Spore(huge, Inside(at, radius) + Vector3.up * Random.Range(0.5f, 3f), Vector3.up * Random.Range(1f, 7f) + Random.insideUnitSphere * 3f, Random.Range(0.35f, 0.85f) * _kit.ReadScale, EffectRecipes.SporeGreen, Random.Range(2f, 6f));
            int clouds = wrath ? 40 : 16;
            for (int i = 0; i < clouds; i++) _kit.Smoke(huge, Inside(at, radius) + Vector3.up, Vector3.up * Random.Range(1.5f, 7f) + Random.insideUnitSphere * 3f, radius * Random.Range(0.5f, 0.9f), EffectRecipes.SporeCloud, Random.Range(2.6f, 6f));
            int spouts = wrath ? 12 : 5;
            for (int i = 0; i < spouts; i++)
            {
                Vector3 spot = Inside(at, radius);
                for (int s = 0; s < 6; s++) _kit.Bolt(huge, spot, Vector3.up * (10f + s * 7f) + Random.insideUnitSphere * 2f, 0.7f * _kit.ReadScale, FloodGreen, 0.7f);
            }
            CameraShake.Kick(at, wrath ? 1f : 0.5f);
        }

        // ------------------------------------------------------------------ everything else

        private void Emp(Vector3 at, float radius)
        {
            const EffectPriority huge = EffectPriority.Huge;
            _kit.Glow(huge, at + Vector3.up * 2f, radius * 2.4f, EmpBlue, 0.24f);
            _kit.Ring(huge, at, radius * 2f, EmpBlue, 0.55f);
            _kit.Ring(huge, at, radius * 2f, Color.white, 0.3f);
            _kit.GroundGlow(huge, at, radius * 2.4f, new Color(0.4f, 0.8f, 1f, 0.5f), 0.7f);
            for (int i = 0; i < 9; i++)
            {
                Vector3 a = Inside(at, radius) + Vector3.up * Random.Range(0.3f, 2.5f), b = Inside(at, radius) + Vector3.up * Random.Range(0.3f, 2.5f);
                Lightning(a, b, EmpBlue, 0.32f * _kit.ReadScale, 0.34f);
            }
            for (int i = 0; i < 40; i++) _kit.Spark(huge, Inside(at, radius) + Vector3.up, Random.onUnitSphere * 7f, 0.07f * _kit.ReadScale, EmpBlue, Random.Range(0.25f, 0.7f));
        }

        private void SporeWave(Vector3 at, float radius)
        {
            const EffectPriority huge = EffectPriority.Huge;
            _kit.Ring(huge, at, radius * 2f, new Color(0.7f, 1f, 0.35f, 0.9f), 0.7f);
            for (int i = 0; i < 70; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                var out3 = new Vector3(Mathf.Cos(angle), 0.1f, Mathf.Sin(angle));
                _kit.Spore(huge, at + Vector3.up * 0.6f + out3, out3 * radius * Random.Range(1.4f, 2.6f), Random.Range(0.35f, 0.7f) * _kit.ReadScale, EffectRecipes.SporeGreen, Random.Range(1.2f, 2.8f));
            }
            for (int i = 0; i < 10; i++) _kit.Smoke(huge, Inside(at, radius) + Vector3.up, Vector3.up * 1.5f, radius * Random.Range(0.4f, 0.7f), EffectRecipes.SporeCloud, Random.Range(1.5f, 3f));
        }

        private void Buff(Vector3 at, float radius, Color color)
        {
            const EffectPriority huge = EffectPriority.Huge;
            _kit.Ring(huge, at, radius * 2f, color, 0.9f);
            _kit.GroundGlow(huge, at, radius * 2.2f, new Color(color.r, color.g, color.b, 0.4f), 1.2f);
            for (int i = 0; i < 70; i++) _kit.Spore(huge, Inside(at, radius) + Vector3.up * 0.3f, Vector3.up * Random.Range(2.5f, 7f), Random.Range(0.25f, 0.5f) * _kit.ReadScale, color, Random.Range(1f, 2f));
        }

        /// <summary>Something lands from the sky: a streak and a ring of dust.</summary>
        private void DropIn(Vector3 at, float radius)
        {
            const EffectPriority huge = EffectPriority.Huge;
            Vector3 top = at + new Vector3(0f, SkyHeight, 12f);
            _kit.FlashLine(top, at, 0.7f * _kit.ReadScale, new Color(1f, 0.82f, 0.55f), 0.3f);
            _recipes.BlastAfter(0.2f, at, radius, BlastStyle.Dust, Color.white, huge);
        }
    }
}
