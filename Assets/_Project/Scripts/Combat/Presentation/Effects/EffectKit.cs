// EffectKit.cs
// The drawing tools of the battle effects (M10). One shared ParticleSystem per kind of particle (glow, streak, fire, smoke...), so that
// every effect on the field is only a few Emit calls: no objects are made during battle, and each kind is one draw call however many
// effects use it. Particles run on battle time, so they pause and speed up with the battle.
// Some things are redrawn every frame instead, from whatever the simulation says now: "heads" (a shell in flight), flat "ground heads"
// (the burning spot of a beam), "cloud puffs" (a nuke's cloud, which holds a shape no flying particle could), and lines (beams, lasers,
// lightning), which are a pool of LineRenderers.
// EffectBudget decides what may still start; its limits are per layer. Nothing in here knows what a weapon or a unit is.
// High quality has more dust than Low. The quality can change in the middle of a battle (the pause menu) while a layer's size is fixed when
// it is built, so the dust layer is built big enough for High, and how much of it may be used follows the quality level as it is now.
// Depends on: EffectAssets, EffectBudget, UnityEngine (ParticleSystem module).

using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Halobellum.Presentation
{
    public sealed class EffectKit
    {
        private sealed class Layer
        {
            public ParticleSystem System;
            public Material Material;
            /// <summary>How many it may hold now. Never more than the system's own size, which is fixed.</summary>
            public int Max;
            public float Fill;
            public float Step;
        }

        private struct FlashedLine
        {
            public Vector3 From, To;
            public float Width;
            public Color Color;
            public float Age, Life;
        }

        private const int HeadCapacity = 400;
        private const int GroundHeadCapacity = 40;
        /// <summary>The most puffs of big clouds at once.</summary>
        public const int CloudSmokeCapacity = 320;
        public const int CloudFireCapacity = 140;
        private const int LinePool = 64;
        private const int FlashedLineCapacity = 40;

        /// <summary>The dust layer's size at a ParticleScale of 1.</summary>
        public const int DustPerScale = 700;
        /// <summary>How many times more dust High quality has than Low.</summary>
        public const float HighQualityScale = 4f;
        /// <summary>A rate alone doesn't show from the game's camera (D62), so on High a moving vehicle's puff is also this much bigger, longer-lived and thicker.</summary>
        public const float HighDustSize = 1.7f, HighDustLife = 1.8f, HighDustThickness = 1.5f;
        /// <summary>Compares settings of the scale on one build, for this run only: "-particleScale 4" instead of what the quality level says. See EffectsGallery's benchmark.</summary>
        public const string ParticleScaleFlag = "-particleScale";
        private const int BloodCapacity = 500;

        // Length of a stretched particle in metres: its width + its speed x this.
        private const float StreakStretch = 0.032f;
        private const float BoltStretch = 0.04f;
        private const float SparkStretch = 0.055f;
        private const float HeadStretch = 1f;

        private readonly Transform _root;
        private readonly Layer _glow, _orb, _streak, _bolt, _sparks, _fire, _flame, _smoke, _dust, _blood, _ring, _groundGlow, _scorch, _chunks, _heat, _slash, _spores;
        private readonly Layer[] _layers;
        private readonly Layer _headGlow, _headStreak, _groundHead, _cloudSmoke, _cloudFire;
        private readonly ParticleSystem.Particle[] _headGlowData = new ParticleSystem.Particle[HeadCapacity];
        private readonly ParticleSystem.Particle[] _headStreakData = new ParticleSystem.Particle[HeadCapacity];
        private readonly ParticleSystem.Particle[] _groundHeadData = new ParticleSystem.Particle[GroundHeadCapacity];
        private readonly ParticleSystem.Particle[] _cloudSmokeData = new ParticleSystem.Particle[CloudSmokeCapacity];
        private readonly ParticleSystem.Particle[] _cloudFireData = new ParticleSystem.Particle[CloudFireCapacity];
        private int _headGlowCount, _headStreakCount, _groundHeadCount, _cloudSmokeCount, _cloudFireCount;
        private int _headGlowShown, _headStreakShown, _groundHeadShown, _cloudSmokeShown, _cloudFireShown;

        private readonly LineRenderer[] _lines = new LineRenderer[LinePool];
        private readonly Material _lineMaterial, _chunkMaterial;
        private int _linesUsed, _linesShown;
        private readonly FlashedLine[] _flashed = new FlashedLine[FlashedLineCapacity];
        private int _nextFlashed;

        private Camera _camera;
        private float _speedShown = -1f;
        private readonly float[] _scaleOfLevel;
        private readonly float _forcedScale;
        private int _levelShown = -1;

        public readonly EffectBudget Budget = new EffectBudget();

        /// <summary>True draws nothing at all (the benchmark's measure of a battle without effects).</summary>
        public bool Muted { get; set; }

        /// <summary>The battle's clock for effects: stands still when the battle is paused.</summary>
        public float BattleTime { get; private set; }

        /// <summary>
        /// Small things (a tracer's width, a muzzle flash) are drawn bigger the further away the camera is, so that they can
        /// still be seen from the game camera and don't look like searchlights close up.
        /// </summary>
        public float ReadScale { get; private set; } = 1f;

        /// <summary>The most particles alive at once since the battle was shown (for the benchmark).</summary>
        public int PeakLive { get; private set; }

        /// <summary>How many times more dust than on Low there is now: by the quality level as it is at the moment, unless the command line says otherwise.</summary>
        public float ParticleScale { get; private set; } = 1f;

        /// <summary>The most dust the layer may hold now (for tests).</summary>
        public int DustLimit => _dust.Max;

        /// <summary>How many puffs of dust are in the air now (for the benchmark).</summary>
        public int LiveDust => _dust.System.particleCount;

        /// <summary>The scale a quality level gives, by its name: "PC" is the one the settings show as High and "Mobile" the one they show as Low.</summary>
        public static float ParticleScaleFor(string qualityName) => qualityName == "PC" ? HighQualityScale : 1f;

        /// <summary>The scale this command line asks for, or 0 if it doesn't. It can't go below Low's.</summary>
        public static float ParticleScaleFromCommandLine(string[] args)
        {
            int index = Array.IndexOf(args, ParticleScaleFlag);
            if (index < 0 || index + 1 >= args.Length) return 0f;
            return float.TryParse(args[index + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scale) ? Mathf.Max(1f, scale) : 0f;
        }

        public int LiveParticles
        {
            get
            {
                int live = 0;
                foreach (Layer layer in _layers) live += layer.System.particleCount;
                return live;
            }
        }

        public EffectKit(Transform parent)
        {
            _root = new GameObject("Effect Layers").transform;
            _root.SetParent(parent, false);

            _forcedScale = ParticleScaleFromCommandLine(Environment.GetCommandLineArgs());
            string[] levels = QualitySettings.names; // This call makes a new array, so it is read once.
            _scaleOfLevel = new float[levels.Length];
            for (int i = 0; i < levels.Length; i++) _scaleOfLevel[i] = ParticleScaleFor(levels[i]);

            // What is drawn over what: marks on the ground, shimmer, smoke, fire, then everything that glows.
            _scorch = Flat("Scorch", EffectAssets.ParticleMaterial("Effect Scorch", EffectAssets.Scorch, 1f, 1f, 0f, 2960), 96);
            _heat = Billboard("Heat", EffectAssets.HeatMaterial(2970), 160);
            _groundGlow = Flat("Ground Glow", EffectAssets.ParticleMaterial("Effect Ground Glow", EffectAssets.Soft, 1.6f, 0f, 0f, 2980), 96);
            _ring = Flat("Shock Ring", EffectAssets.ParticleMaterial("Effect Ring", EffectAssets.Ring, 2.2f, 0f, 0f, 2985), 96);
            _groundHead = Flat("Ground Heads", EffectAssets.ParticleMaterial("Effect Ground Head", EffectAssets.Soft, 2.4f, 0f, 0f, 2981), GroundHeadCapacity);
            _smoke = Billboard("Smoke", EffectAssets.ParticleMaterial("Effect Smoke", EffectAssets.Smoke, 1f, 1f, 2f, 3000), 900);
            _dust = Billboard("Dust", EffectAssets.ParticleMaterial("Effect Dust", EffectAssets.Smoke, 1f, 1f, 0.8f, 3001), Mathf.RoundToInt(DustPerScale * Mathf.Max(HighQualityScale, _forcedScale)));
            _blood = Billboard("Blood", EffectAssets.ParticleMaterial("Effect Blood", EffectAssets.Soft, 1f, 1f, 0f, 3005), BloodCapacity);
            _spores = Billboard("Spores", EffectAssets.ParticleMaterial("Effect Spores", EffectAssets.Soft, 1.5f, 0.1f, 0f, 3010), 600);
            _fire = Billboard("Fire", EffectAssets.ParticleMaterial("Effect Fire", EffectAssets.Flame, 2.2f, 0.55f, 0.6f, 3020), 1300);
            _flame = Billboard("Flame", EffectAssets.ParticleMaterial("Effect Flame", EffectAssets.Flame, 1.9f, 0.4f, 0.3f, 3021), 1500);
            _glow = Billboard("Glow", EffectAssets.ParticleMaterial("Effect Glow", EffectAssets.Soft, 3f, 0f, 0f, 3030, true), 1500);
            _orb = Billboard("Orb", EffectAssets.ParticleMaterial("Effect Orb", EffectAssets.Soft, 3f, 0f, 0f, 3031), 500);
            _bolt = Stretched("Bolt", EffectAssets.ParticleMaterial("Effect Bolt", EffectAssets.Streak, 3.4f, 0f, 0f, 3032), 900, BoltStretch);
            _streak = Stretched("Streak", EffectAssets.ParticleMaterial("Effect Streak", EffectAssets.Streak, 3.6f, 0f, 0f, 3033), 1500, StreakStretch);
            _sparks = Stretched("Sparks", EffectAssets.ParticleMaterial("Effect Sparks", EffectAssets.Streak, 3.6f, 0f, 0f, 3034), 1600, SparkStretch);
            _slash = Billboard("Slash", EffectAssets.ParticleMaterial("Effect Slash", EffectAssets.Slash, 2.6f, 0f, 0f, 3035), 240);
            _headGlow = Billboard("Head Glow", EffectAssets.ParticleMaterial("Effect Head Glow", EffectAssets.Soft, 3f, 0f, 0f, 3036, true), HeadCapacity);
            _headStreak = Stretched("Head Streak", EffectAssets.ParticleMaterial("Effect Head Streak", EffectAssets.Streak, 3.4f, 0f, 0f, 3037), HeadCapacity, HeadStretch);
            _cloudSmoke = Cloud("Cloud Smoke", EffectAssets.ParticleMaterial("Effect Cloud Smoke", EffectAssets.Smoke, 1f, 1f, 2f, 3002), CloudSmokeCapacity);
            _cloudFire = Cloud("Cloud Fire", EffectAssets.ParticleMaterial("Effect Cloud Fire", EffectAssets.Flame, 2.2f, 0.55f, 0.6f, 3022), CloudFireCapacity);
            _chunkMaterial = EffectAssets.ChunkMaterial();
            _chunks = MakeChunks("Chunks", _chunkMaterial, 320);

            _layers = new[] { _scorch, _heat, _groundGlow, _ring, _smoke, _dust, _blood, _spores, _fire, _flame, _glow, _orb, _bolt, _streak, _sparks, _slash, _chunks };

            Fades(_glow, Fade(1f, 0.25f, 0.8f, 0f), Grow(0.55f, 0.2f, 1f));
            Fades(_orb, Fade(1f, 0.85f, 1f, 0f), null);
            Fades(_streak, Fade(1f, 0.85f, 1f, 0.4f), null);
            Fades(_bolt, Fade(1f, 0.85f, 1f, 0.6f), null);
            Fades(_sparks, Fade(1f, 0.5f, 1f, 0f), null);
            Drag(_sparks, 1.4f);
            Gravity(_sparks, 1.5f);

            var fire = new Gradient();
            fire.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.92f, 0.65f), 0f), new GradientColorKey(new Color(1f, 0.58f, 0.14f), 0.25f), new GradientColorKey(new Color(0.42f, 0.11f, 0.03f), 0.55f), new GradientColorKey(new Color(0.05f, 0.03f, 0.025f), 0.85f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.05f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            ColorOverLife(_fire, fire);
            SizeOverLife(_fire, Grow(0.45f, 0.45f, 1f));
            Drag(_fire, 2.4f);
            Gravity(_fire, -0.25f);
            Sheet(_fire);
            Spin(_fire, 1.2f);

            // Flamethrower fire: small at the muzzle, growing all the way to the end of its range.
            var flame = new Gradient();
            flame.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.86f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.12f), 0.35f), new GradientColorKey(new Color(0.65f, 0.16f, 0.03f), 0.7f), new GradientColorKey(new Color(0.08f, 0.04f, 0.03f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.06f), new GradientAlphaKey(0.7f, 0.55f), new GradientAlphaKey(0f, 1f) });
            ColorOverLife(_flame, flame);
            SizeOverLife(_flame, Ease(new Keyframe(0f, 0.12f), new Keyframe(1f, 1f)));
            Drag(_flame, 2.4f);
            Gravity(_flame, -0.35f);
            Sheet(_flame);
            Spin(_flame, 1.6f);

            ColorOverLife(_smoke, Rise(0.07f, 0.3f));
            SizeOverLife(_smoke, Grow(0.35f, 0.45f, 0.82f));
            Drag(_smoke, 1.1f);
            Gravity(_smoke, -0.1f);
            Sheet(_smoke);
            Spin(_smoke, 0.5f);
            Wander(_smoke, 0.5f, 0.3f);

            ColorOverLife(_dust, Rise(0.06f, 0.5f));
            SizeOverLife(_dust, Grow(0.4f, 0.4f, 0.85f));
            Drag(_dust, 2.2f);
            Sheet(_dust);
            Spin(_dust, 0.4f);

            ColorOverLife(_blood, Fade(1f, 0.75f, 1f, 0f));
            Gravity(_blood, 1.8f);

            ColorOverLife(_spores, Rise(0.15f, 0.7f));
            Drag(_spores, 1.4f);
            Gravity(_spores, -0.015f);
            Wander(_spores, 0.8f, 0.6f);

            ColorOverLife(_ring, Fade(1f, 0.3f, 0.7f, 0f));
            SizeOverLife(_ring, Ease(new Keyframe(0f, 0.03f), new Keyframe(0.14f, 0.6f), new Keyframe(0.42f, 0.9f), new Keyframe(1f, 1f)));
            ColorOverLife(_groundGlow, Fade(1f, 0.25f, 0.7f, 0f));
            SizeOverLife(_groundGlow, Grow(0.6f, 0.15f, 1f));
            ColorOverLife(_scorch, Rise(0.02f, 0.85f));
            ColorOverLife(_slash, Fade(1f, 0.4f, 1f, 0f));
            ColorOverLife(_heat, Rise(0.15f, 0.5f));
            SizeOverLife(_heat, Grow(0.6f, 0.5f, 1f));

            _lineMaterial = EffectAssets.ParticleMaterial("Effect Line", EffectAssets.Beam, 3.2f, 0f, 0f, 3040);
            var lines = new GameObject("Lines").transform;
            lines.SetParent(_root, false);
            for (int i = 0; i < LinePool; i++) _lines[i] = MakeLine(lines);

            FollowQualityLevel();
            foreach (Layer layer in _layers) layer.System.Play();
            _headGlow.System.Play();
            _headStreak.System.Play();
            _groundHead.System.Play();
            _cloudSmoke.System.Play();
            _cloudFire.System.Play();
        }

        public void Destroy()
        {
            foreach (Layer layer in _layers) if (layer.Material != null) Object.Destroy(layer.Material);
            if (_headGlow.Material != null) Object.Destroy(_headGlow.Material);
            if (_headStreak.Material != null) Object.Destroy(_headStreak.Material);
            if (_groundHead.Material != null) Object.Destroy(_groundHead.Material);
            if (_cloudSmoke.Material != null) Object.Destroy(_cloudSmoke.Material);
            if (_cloudFire.Material != null) Object.Destroy(_cloudFire.Material);
            if (_lineMaterial != null) Object.Destroy(_lineMaterial);
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        // ------------------------------------------------------------------ the frame

        /// <summary>Call first thing each frame: keeps the particles on battle time and refreshes how full each layer is.</summary>
        public void BeginFrame(float battleSpeed, float deltaTime, float unscaledDelta)
        {
            BattleTime += deltaTime * battleSpeed;
            if (!Mathf.Approximately(battleSpeed, _speedShown))
            {
                _speedShown = battleSpeed;
                foreach (Layer layer in _layers)
                {
                    ParticleSystem.MainModule main = layer.System.main;
                    main.simulationSpeed = battleSpeed;
                }
            }
            Budget.BeginFrame(unscaledDelta);
            FollowQualityLevel();

            int live = 0;
            foreach (Layer layer in _layers)
            {
                int count = layer.System.particleCount;
                live += count;
                layer.Fill = (float)count / layer.Max;
            }
            if (live > PeakLive) PeakLive = live;

            if (_camera == null) _camera = Camera.main;
            if (_camera != null) ReadScale = ReadScaleFor(_camera.transform.position.y, _camera.transform.forward.y);

            _headGlowCount = _headStreakCount = _groundHeadCount = _cloudSmokeCount = _cloudFireCount = 0;
            _linesUsed = 0;
        }

        /// <summary>The quality can change at any moment: what the dust may use goes with it, whatever is already in the air.</summary>
        private void FollowQualityLevel()
        {
            int level = QualitySettings.GetQualityLevel();
            if (level == _levelShown) return;
            _levelShown = level;
            ParticleScale = _forcedScale > 0f ? _forcedScale : level >= 0 && level < _scaleOfLevel.Length ? _scaleOfLevel[level] : 1f;
            _dust.Max = Mathf.RoundToInt(DustPerScale * ParticleScale);
            _dust.Step = 1f / _dust.Max;
            Budget.SmallLimit = Mathf.RoundToInt(EffectBudget.SmallPerFrame * ParticleScale);
        }

        /// <summary>
        /// ReadScale for a camera this high, looking this far down (forward.y). It goes by how far away the ground is rather than by height, so that tilting the camera doesn't
        /// resize things. At the default tilt it is the height over 90 m.
        /// </summary>
        public static float ReadScaleFor(float height, float forwardY)
        {
            float sinDefaultTilt = Mathf.Sin(BattleCameraController.DefaultPitch * Mathf.Deg2Rad);
            return Mathf.Clamp(height * sinDefaultTilt / Mathf.Max(0.55f, -forwardY) / 90f, 0.5f, 1.9f);
        }

        /// <summary>Call last thing each frame: hands over what is redrawn every frame.</summary>
        public void EndFrame()
        {
            float battleDelta = _speedShown * Time.deltaTime;
            for (int i = 0; i < _flashed.Length; i++)
            {
                ref FlashedLine line = ref _flashed[i];
                if (line.Life <= 0f) continue;
                line.Age += battleDelta;
                if (line.Age >= line.Life)
                {
                    line.Life = 0f;
                    continue;
                }
                float left = 1f - line.Age / line.Life;
                Color color = line.Color;
                color.a *= left;
                Line(line.From, line.To, line.Width * Mathf.Lerp(0.4f, 1f, left), color);
            }
            for (int i = _linesUsed; i < _linesShown; i++) _lines[i].enabled = false;
            _linesShown = _linesUsed;

            Hand(_headGlow, _headGlowData, _headGlowCount, ref _headGlowShown);
            Hand(_headStreak, _headStreakData, _headStreakCount, ref _headStreakShown);
            Hand(_groundHead, _groundHeadData, _groundHeadCount, ref _groundHeadShown);
            Hand(_cloudSmoke, _cloudSmokeData, _cloudSmokeCount, ref _cloudSmokeShown);
            Hand(_cloudFire, _cloudFireData, _cloudFireCount, ref _cloudFireShown);
        }

        private static void Hand(Layer layer, ParticleSystem.Particle[] data, int count, ref int shown)
        {
            if (count == 0 && shown == 0) return;
            layer.System.SetParticles(data, count);
            shown = count;
        }

        /// <summary>Throws away everything on the field (a new battle is shown).</summary>
        public void Clear()
        {
            foreach (Layer layer in _layers) layer.System.Clear();
            for (int i = 0; i < _flashed.Length; i++) _flashed[i].Life = 0f;
            BattleTime = 0f;
            PeakLive = 0;
            Budget.Reset();
        }

        // ------------------------------------------------------------------ particles that live on their own

        public void Glow(EffectPriority priority, Vector3 position, float size, Color color, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, startSize = size, startColor = color, startLifetime = life };
            Emit(_glow, priority, ref emit);
        }

        /// <summary>A glowing ball that flies.</summary>
        public void Orb(EffectPriority priority, Vector3 position, Vector3 velocity, float size, Color color, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, velocity = velocity, startSize = size, startColor = color, startLifetime = life };
            Emit(_orb, priority, ref emit);
        }

        /// <summary>A long hot streak (bullets, gauss, needles). The faster it flies, the longer it looks.</summary>
        public void Streak(EffectPriority priority, Vector3 position, Vector3 velocity, float width, Color color, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, velocity = velocity, startSize = width, startColor = color, startLifetime = life };
            Emit(_streak, priority, ref emit);
        }

        /// <summary>A short fat streak (plasma bolts, spat bone).</summary>
        public void Bolt(EffectPriority priority, Vector3 position, Vector3 velocity, float width, Color color, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, velocity = velocity, startSize = width, startColor = color, startLifetime = life };
            Emit(_bolt, priority, ref emit);
        }

        /// <summary>A spark: it falls and slows down.</summary>
        public void Spark(EffectPriority priority, Vector3 position, Vector3 velocity, float width, Color color, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, velocity = velocity, startSize = width, startColor = color, startLifetime = life };
            Emit(_sparks, priority, ref emit);
        }

        public void Fire(EffectPriority priority, Vector3 position, Vector3 velocity, float size, float life) => Fire(priority, position, velocity, size, life, Color.white);

        /// <summary>A puff of a flamethrower's flame: it grows as it flies.</summary>
        public void Flame(EffectPriority priority, Vector3 position, Vector3 velocity, float size, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, velocity = velocity, startSize = size, startColor = Color.white, startLifetime = life, rotation = Random.value * 360f };
            Emit(_flame, priority, ref emit);
        }

        /// <summary>A lick of fire, white-hot through orange to dark red. The tint can make it plasma or sick green.</summary>
        public void Fire(EffectPriority priority, Vector3 position, Vector3 velocity, float size, float life, Color tint)
        {
            var emit = new ParticleSystem.EmitParams { position = position, velocity = velocity, startSize = size, startColor = tint, startLifetime = life, rotation = Random.value * 360f };
            Emit(_fire, priority, ref emit);
        }

        public void Smoke(EffectPriority priority, Vector3 position, Vector3 velocity, float size, Color color, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, velocity = velocity, startSize = size, startColor = color, startLifetime = life, rotation = Random.value * 360f };
            Emit(_smoke, priority, ref emit);
        }

        public void Dust(EffectPriority priority, Vector3 position, Vector3 velocity, float size, Color color, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, velocity = velocity, startSize = size, startColor = color, startLifetime = life, rotation = Random.value * 360f };
            Emit(_dust, priority, ref emit);
        }

        /// <summary>A drop of blood: it is thrown out and falls.</summary>
        public void Blood(EffectPriority priority, Vector3 position, Vector3 velocity, float size, Color color, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, velocity = velocity, startSize = size, startColor = color, startLifetime = life };
            Emit(_blood, priority, ref emit);
        }

        public void Spore(EffectPriority priority, Vector3 position, Vector3 velocity, float size, Color color, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, velocity = velocity, startSize = size, startColor = color, startLifetime = life };
            Emit(_spores, priority, ref emit);
        }

        /// <summary>A shock ring lying flat, growing quickly to this diameter.</summary>
        public void Ring(EffectPriority priority, Vector3 position, float diameter, Color color, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, startSize = diameter, startColor = color, startLifetime = life, rotation3D = Vector3.zero };
            Emit(_ring, priority, ref emit);
        }

        /// <summary>Light on the ground, like the light of an explosion.</summary>
        public void GroundGlow(EffectPriority priority, Vector3 position, float diameter, Color color, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, startSize = diameter, startColor = color, startLifetime = life, rotation3D = Vector3.zero };
            Emit(_groundGlow, priority, ref emit);
        }

        /// <summary>A burn mark (or a splatter) on the ground.</summary>
        public void Scorch(EffectPriority priority, Vector3 position, float diameter, Color color, float life)
        {
            position.y = 0.04f;
            var emit = new ParticleSystem.EmitParams { position = position, startSize = diameter, startColor = color, startLifetime = life, rotation3D = new Vector3(0f, Random.value * 360f, 0f) };
            Emit(_scorch, priority, ref emit);
        }

        /// <summary>A solid chunk that tumbles, falls, and bounces on the ground.</summary>
        public void Chunk(EffectPriority priority, Vector3 position, Vector3 velocity, float size, Color color, float life)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = position, velocity = velocity, startSize = size, startColor = color, startLifetime = life,
                rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, Random.value * 360f),
                angularVelocity3D = new Vector3(Random.Range(-540f, 540f), Random.Range(-540f, 540f), Random.Range(-540f, 540f)),
            };
            Emit(_chunks, priority, ref emit);
        }

        /// <summary>Shimmering air.</summary>
        public void Heat(EffectPriority priority, Vector3 position, Vector3 velocity, float size, float life)
        {
            var emit = new ParticleSystem.EmitParams { position = position, velocity = velocity, startSize = size, startColor = Color.white, startLifetime = life, rotation = Random.value * 360f };
            Emit(_heat, priority, ref emit);
        }

        /// <summary>A quick crescent, turned by this many degrees.</summary>
        public void Slash(EffectPriority priority, Vector3 position, float size, Color color, float life, float degrees)
        {
            var emit = new ParticleSystem.EmitParams { position = position, startSize = size, startColor = color, startLifetime = life, rotation = degrees };
            Emit(_slash, priority, ref emit);
        }

        private void Emit(Layer layer, EffectPriority priority, ref ParticleSystem.EmitParams emit)
        {
            if (Muted || !Budget.Allow(priority, layer.Fill)) return;
            layer.Fill += layer.Step;
            layer.System.Emit(emit, 1);
        }

        // ------------------------------------------------------------------ redrawn every frame

        /// <summary>A glow for this frame only.</summary>
        public void HeadGlow(Vector3 position, float size, Color color)
        {
            if (Muted || _headGlowCount >= HeadCapacity) return;
            ref ParticleSystem.Particle head = ref _headGlowData[_headGlowCount++];
            head.position = position;
            head.velocity = Vector3.zero;
            head.startSize = size;
            head.startColor = color;
            head.startLifetime = 10f;
            head.remainingLifetime = 10f;
            head.rotation = 0f;
        }

        /// <summary>A streak for this frame only, pointing along direction, this long and this wide (metres).</summary>
        public void HeadStreak(Vector3 position, Vector3 direction, float length, float width, Color color)
        {
            if (Muted || _headStreakCount >= HeadCapacity) return;
            ref ParticleSystem.Particle head = ref _headStreakData[_headStreakCount++];
            head.position = position;
            // The renderer stretches a particle by its speed: this speed gives this length.
            head.velocity = direction * (Mathf.Max(0f, length - width) / HeadStretch);
            head.startSize = width;
            head.startColor = color;
            head.startLifetime = 10f;
            head.remainingLifetime = 10f;
        }

        /// <summary>A flat glow on the ground for this frame only.</summary>
        public void GroundHead(Vector3 position, float diameter, Color color)
        {
            if (Muted || _groundHeadCount >= GroundHeadCapacity) return;
            ref ParticleSystem.Particle head = ref _groundHeadData[_groundHeadCount++];
            head.position = position;
            head.velocity = Vector3.zero;
            head.startSize = diameter;
            head.startColor = color;
            head.startLifetime = 10f;
            head.remainingLifetime = 10f;
            head.rotation3D = Vector3.zero;
        }

        /// <summary>A puff of a big cloud's smoke for this frame only. The same seed keeps the same shape of puff.</summary>
        public void CloudSmoke(Vector3 position, float size, Color color, float degrees, uint seed)
        {
            if (Muted || _cloudSmokeCount >= CloudSmokeCapacity) return;
            SetCloudPuff(ref _cloudSmokeData[_cloudSmokeCount++], position, size, color, degrees, seed);
        }

        /// <summary>A lick of a big cloud's fire for this frame only.</summary>
        public void CloudFire(Vector3 position, float size, Color color, float degrees, uint seed)
        {
            if (Muted || _cloudFireCount >= CloudFireCapacity) return;
            SetCloudPuff(ref _cloudFireData[_cloudFireCount++], position, size, color, degrees, seed);
        }

        private void SetCloudPuff(ref ParticleSystem.Particle puff, Vector3 position, float size, Color color, float degrees, uint seed)
        {
            // A puff the camera is inside fades, so that zooming in on a cloud doesn't turn the whole screen to smoke.
            if (_camera != null)
            {
                float distance = Vector3.Distance(position, _camera.transform.position);
                color.a *= Mathf.Clamp01((distance - size * 0.55f) / (size * 0.75f));
            }
            puff.position = position;
            puff.velocity = Vector3.zero;
            puff.startSize = size;
            puff.startColor = color;
            puff.startLifetime = 10f;
            puff.remainingLifetime = 10f;
            puff.rotation = degrees;
            puff.randomSeed = seed;
        }

        /// <summary>A glowing line for this frame only.</summary>
        public void Line(Vector3 from, Vector3 to, float width, Color color)
        {
            if (Muted || _linesUsed >= LinePool) return;
            LineRenderer line = _lines[_linesUsed++];
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = color;
            if (!line.enabled) line.enabled = true;
        }

        /// <summary>A glowing line that thins and fades by itself over this much battle time (laser, gauss, lightning).</summary>
        public void FlashLine(Vector3 from, Vector3 to, float width, Color color, float life)
        {
            _flashed[_nextFlashed] = new FlashedLine { From = from, To = to, Width = width, Color = color, Life = life };
            _nextFlashed = (_nextFlashed + 1) % _flashed.Length;
        }

        // ------------------------------------------------------------------ building the layers

        private Layer NewLayer(string name, Material material, int max)
        {
            var body = new GameObject(name);
            body.transform.SetParent(_root, false);
            ParticleSystem system = body.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.startSize = 1f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            var renderer = body.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 8f;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.enableGPUInstancing = false;
            return new Layer { System = system, Material = material, Max = max, Step = 1f / max };
        }

        private Layer Billboard(string name, Material material, int max)
        {
            Layer layer = NewLayer(name, material, max);
            layer.System.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
            return layer;
        }

        private Layer Stretched(string name, Material material, int max, float stretch)
        {
            Layer layer = NewLayer(name, material, max);
            var renderer = layer.System.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1f;
            renderer.velocityScale = stretch;
            renderer.cameraVelocityScale = 0f;
            return layer;
        }

        /// <summary>Puffs of a cloud held in shape from outside. They overlap heavily, so the far ones are drawn first.</summary>
        private Layer Cloud(string name, Material material, int max)
        {
            Layer layer = Billboard(name, material, max);
            layer.System.GetComponent<ParticleSystemRenderer>().sortMode = ParticleSystemSortMode.Distance;
            Sheet(layer);
            return layer;
        }

        private Layer Flat(string name, Material material, int max)
        {
            Layer layer = NewLayer(name, material, max);
            ParticleSystem.MainModule main = layer.System.main;
            main.startRotation3D = true;
            var renderer = layer.System.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = EffectAssets.FlatQuad;
            renderer.alignment = ParticleSystemRenderSpace.World;
            return layer;
        }

        private Layer MakeChunks(string name, Material material, int max)
        {
            Layer layer = NewLayer(name, material, max);
            ParticleSystem.MainModule main = layer.System.main;
            main.startRotation3D = true;
            main.gravityModifier = 2f;
            var renderer = layer.System.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.SetMeshes(EffectAssets.Chunks);
            renderer.meshDistribution = ParticleSystemMeshDistribution.UniformRandom;
            renderer.alignment = ParticleSystemRenderSpace.World;

            // They bounce on the ground. Planes are cheap: no physics objects are involved.
            var ground = new GameObject("Ground Plane").transform;
            ground.SetParent(_root, false);
            ground.position = new Vector3(0f, 0.05f, 0f);
            ParticleSystem.CollisionModule collision = layer.System.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, ground);
            collision.bounce = 0.35f;
            collision.dampen = 0.45f;
            collision.lifetimeLoss = 0f;
            collision.radiusScale = 0.4f;
            collision.sendCollisionMessages = false;

            SizeOverLife(layer, Ease(new Keyframe(0f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));
            return layer;
        }

        private LineRenderer MakeLine(Transform parent)
        {
            var body = new GameObject("Line");
            body.transform.SetParent(parent, false);
            LineRenderer line = body.AddComponent<LineRenderer>();
            line.sharedMaterial = _lineMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            line.enabled = false;
            return line;
        }

        private static void Fades(Layer layer, Gradient colour, AnimationCurve size)
        {
            ColorOverLife(layer, colour);
            if (size != null) SizeOverLife(layer, size);
        }

        private static void ColorOverLife(Layer layer, Gradient gradient)
        {
            ParticleSystem.ColorOverLifetimeModule module = layer.System.colorOverLifetime;
            module.enabled = true;
            module.color = gradient;
        }

        private static void SizeOverLife(Layer layer, AnimationCurve curve)
        {
            ParticleSystem.SizeOverLifetimeModule module = layer.System.sizeOverLifetime;
            module.enabled = true;
            module.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void Drag(Layer layer, float drag)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule module = layer.System.limitVelocityOverLifetime;
            module.enabled = true;
            module.limit = 1000f;
            module.dampen = 0f;
            module.drag = drag;
            module.multiplyDragByParticleSize = false;
            module.multiplyDragByParticleVelocity = false;
        }

        private static void Gravity(Layer layer, float gravity)
        {
            ParticleSystem.MainModule main = layer.System.main;
            main.gravityModifier = gravity;
        }

        /// <summary>Each particle picks one of the texture's four tiles and keeps it.</summary>
        private static void Sheet(Layer layer)
        {
            ParticleSystem.TextureSheetAnimationModule module = layer.System.textureSheetAnimation;
            module.enabled = true;
            module.numTilesX = 2;
            module.numTilesY = 2;
            module.animation = ParticleSystemAnimationType.WholeSheet;
            module.frameOverTime = 0f;
            module.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            module.cycleCount = 1;
        }

        private static void Spin(Layer layer, float radiansPerSecond)
        {
            ParticleSystem.RotationOverLifetimeModule module = layer.System.rotationOverLifetime;
            module.enabled = true;
            module.z = new ParticleSystem.MinMaxCurve(-radiansPerSecond, radiansPerSecond);
        }

        /// <summary>A little aimless drifting.</summary>
        private static void Wander(Layer layer, float strength, float frequency)
        {
            ParticleSystem.NoiseModule module = layer.System.noise;
            module.enabled = true;
            module.strength = strength;
            module.frequency = frequency;
            module.damping = true;
            module.octaveCount = 1;
            module.quality = ParticleSystemNoiseQuality.Low;
        }

        private static AnimationCurve Grow(float from, float at, float settled) => Ease(new Keyframe(0f, from), new Keyframe(at, settled), new Keyframe(1f, 1f));

        private static AnimationCurve Ease(params Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        /// <summary>White, its alpha going from start through a1 (at time t1) to end.</summary>
        private static Gradient Fade(float start, float t1, float a1, float end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(start, 0f), new GradientAlphaKey(a1, t1), new GradientAlphaKey(end, 1f) });
            return gradient;
        }

        /// <summary>White, appearing up to t1, holding, and fading from t2.</summary>
        private static Gradient Rise(float t1, float t2)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, t1), new GradientAlphaKey(1f, t2), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }
}
