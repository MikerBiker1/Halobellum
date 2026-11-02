// EffectRecipes.cs
// How each effect is put together from EffectKit's particles (M10): muzzle flashes, shots, impacts, blasts, fireballs and deaths.
// Sizes are in metres; things that must be readable from far away are scaled with EffectKit.ReadScale. Splash weapons' shock rings
// are exactly as wide as their splash. Anything that happens a moment later (a bullet's impact once it has arrived, a Scarab's chain
// of blasts) waits in a fixed-size list that runs on battle time. Nothing allocates during battle.
// Depends on: EffectKit, WeaponLooks, CameraShake, Halobellum.Core (ArmorType), UnityEngine.

using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class EffectRecipes
    {
        private enum DelayedKind
        {
            Impact,
            Blast,
            Fireball,
        }

        private struct Delayed
        {
            public float At;
            public DelayedKind Kind;
            public Vector3 Position;
            public Vector3 Direction;
            public float Size;
            public Color Color;
            public ImpactStyle Impact;
            public BlastStyle Blast;
            public ArmorType Armor;
            public bool Shielded;
            public EffectPriority Priority;
        }

        private const int DelayedCapacity = 700;

        public static readonly Color SmokeDark = new Color(0.1f, 0.095f, 0.09f, 0.72f);
        public static readonly Color SmokeGrey = new Color(0.34f, 0.33f, 0.32f, 0.5f);
        public static readonly Color SmokeThin = new Color(0.42f, 0.41f, 0.4f, 0.3f);
        public static readonly Color DustTan = new Color(0.62f, 0.56f, 0.46f, 0.42f);
        public static readonly Color DirtBrown = new Color(0.3f, 0.25f, 0.19f, 0.7f);
        public static readonly Color FireGlow = new Color(1f, 0.72f, 0.38f);
        public static readonly Color GroundFire = new Color(1f, 0.5f, 0.18f, 0.8f);
        public static readonly Color SparkHot = new Color(1f, 0.82f, 0.45f);
        public static readonly Color ShieldBlue = new Color(0.4f, 0.72f, 1f);
        public static readonly Color ScorchBlack = new Color(0.03f, 0.028f, 0.025f, 0.88f);
        public static readonly Color MetalDark = new Color(0.14f, 0.14f, 0.13f);
        public static readonly Color MetalHot = new Color(0.55f, 0.28f, 0.12f);
        public static readonly Color FleshPink = new Color(0.62f, 0.32f, 0.26f);
        public static readonly Color FleshSick = new Color(0.56f, 0.6f, 0.26f);
        public static readonly Color SporeGreen = new Color(0.72f, 0.95f, 0.3f, 0.75f);
        public static readonly Color SporeCloud = new Color(0.34f, 0.42f, 0.16f, 0.48f);
        public static readonly Color SplatterDark = new Color(0.1f, 0.12f, 0.03f, 0.8f);

        private readonly EffectKit _kit;
        private readonly Delayed[] _delayed = new Delayed[DelayedCapacity];
        private int _delayedCount;

        public EffectRecipes(EffectKit kit)
        {
            _kit = kit;
        }

        public EffectKit Kit => _kit;

        public void Clear() => _delayedCount = 0;

        // ------------------------------------------------------------------ the waiting list

        /// <summary>Runs everything that is due by now.</summary>
        public void RunDue()
        {
            float now = _kit.BattleTime;
            for (int i = _delayedCount - 1; i >= 0; i--)
            {
                if (_delayed[i].At > now) continue;
                Delayed item = _delayed[i];
                _delayed[i] = _delayed[--_delayedCount];
                switch (item.Kind)
                {
                    case DelayedKind.Impact: Impact(item.Position, item.Direction, item.Impact, item.Armor, item.Shielded, item.Color); break;
                    case DelayedKind.Blast: Blast(item.Position, item.Size, item.Blast, item.Color, item.Priority); break;
                    case DelayedKind.Fireball: Fireball(item.Position, item.Size, item.Priority); break;
                }
            }
        }

        private void Add(in Delayed item)
        {
            // When the list is full, a new impact is the thing to drop; a blast takes a place from it.
            if (_delayedCount >= DelayedCapacity)
            {
                if (item.Kind == DelayedKind.Impact) return;
                for (int i = 0; i < _delayedCount; i++)
                {
                    if (_delayed[i].Kind != DelayedKind.Impact) continue;
                    _delayed[i] = item;
                    return;
                }
                return;
            }
            _delayed[_delayedCount++] = item;
        }

        public void ImpactAfter(float seconds, Vector3 position, Vector3 direction, ImpactStyle style, ArmorType armor, bool shielded, Color color)
        {
            Add(new Delayed { At = _kit.BattleTime + seconds, Kind = DelayedKind.Impact, Position = position, Direction = direction, Impact = style, Armor = armor, Shielded = shielded, Color = color });
        }

        public void BlastAfter(float seconds, Vector3 position, float radius, BlastStyle style, Color color, EffectPriority priority)
        {
            Add(new Delayed { At = _kit.BattleTime + seconds, Kind = DelayedKind.Blast, Position = position, Size = radius, Blast = style, Color = color, Priority = priority });
        }

        public void FireballAfter(float seconds, Vector3 position, float radius, EffectPriority priority)
        {
            Add(new Delayed { At = _kit.BattleTime + seconds, Kind = DelayedKind.Fireball, Position = position, Size = radius, Priority = priority });
        }

        // ------------------------------------------------------------------ shooting

        public void Muzzle(Vector3 position, Vector3 direction, in WeaponLook look)
        {
            float read = _kit.ReadScale;
            float size = look.FlashSize * read;
            if (size <= 0f) return;
            _kit.Glow(EffectPriority.Small, position, size, look.Flash, 0.075f);
            if (!look.HeavyMuzzle) return;

            // A heavy gun: a hard flash, a push of smoke along the barrel and a spray of sparks.
            _kit.Glow(EffectPriority.Medium, position + direction * 0.8f, size * 0.7f, Color.white, 0.05f);
            for (int i = 0; i < 3; i++) _kit.Smoke(EffectPriority.Small, position + direction * 0.6f, direction * Random.Range(4f, 8f) + Random.insideUnitSphere * 1.2f, Random.Range(1.2f, 2f), SmokeGrey, Random.Range(0.6f, 1.1f));
            for (int i = 0; i < 2; i++) _kit.Fire(EffectPriority.Small, position + direction * 0.5f, direction * Random.Range(5f, 10f), Random.Range(0.8f, 1.3f), 0.16f);
            for (int i = 0; i < 4; i++) _kit.Spark(EffectPriority.Small, position, direction * Random.Range(10f, 20f) + Random.insideUnitSphere * 5f, 0.05f * read, look.Flash, 0.22f);
        }

        /// <summary>
        /// Draws a shot the simulation has already hit with, flying from the muzzle to the target. Returns how long it appears to fly;
        /// the impact should come that much later.
        /// </summary>
        public float InstantShot(Vector3 from, Vector3 to, in WeaponLook look)
        {
            Vector3 line = to - from;
            float distance = line.magnitude;
            if (distance < 0.01f) return 0f;
            Vector3 direction = line / distance;
            float read = _kit.ReadScale;

            switch (look.Kind)
            {
                case WeaponFeelKind.Laser:
                    // A red laser: one straight line, there at once, with a white-hot middle.
                    _kit.FlashLine(from, to, 0.42f * read, look.Shot, 0.34f);
                    _kit.FlashLine(from, to, 0.14f * read, Color.Lerp(look.Shot, Color.white, 0.7f), 0.2f);
                    return 0f;
                case WeaponFeelKind.Plasma:
                {
                    float flight = distance / look.DrawnSpeed;
                    Vector3 velocity = direction * look.DrawnSpeed;
                    _kit.Bolt(EffectPriority.Small, from, velocity, look.ShotWidth * read, look.Shot, flight);
                    _kit.Orb(EffectPriority.Small, from, velocity, look.ShotWidth * 1.6f * read, new Color(look.Shot.r, look.Shot.g, look.Shot.b, 0.55f), flight);
                    return flight;
                }
                case WeaponFeelKind.Bio:
                {
                    float flight = distance / look.DrawnSpeed;
                    _kit.Bolt(EffectPriority.Small, from, direction * look.DrawnSpeed, look.ShotWidth * read, look.Shot, flight);
                    _kit.Spore(EffectPriority.Small, from, direction * 3f, 0.5f * read, SporeGreen, 0.6f);
                    return flight;
                }
                default:
                {
                    float flight = distance / Mathf.Max(1f, look.DrawnSpeed);
                    _kit.Streak(EffectPriority.Small, from, direction * look.DrawnSpeed, look.ShotWidth * read, look.Shot, flight);
                    return flight;
                }
            }
        }

        /// <summary>One puff of a flamethrower: a real cone of fire from the muzzle to the end of its range, with shimmering air over it.</summary>
        public void FlameShot(Vector3 from, Vector3 to, float coneDegrees, float range)
        {
            Vector3 direction = (to - from).normalized;
            float spread = Mathf.Tan(Mathf.Max(6f, coneDegrees) * 0.5f * Mathf.Deg2Rad);
            float speed = range * 2.9f; // With the fire's drag it comes to rest at the end of its range.
            for (int i = 0; i < 3; i++)
            {
                Vector3 way = (direction + Random.insideUnitSphere * spread).normalized;
                _kit.Fire(EffectPriority.Medium, from + direction * 0.3f, way * speed * Random.Range(0.75f, 1.05f), Random.Range(1.3f, 2.5f), Random.Range(0.5f, 0.72f));
            }
            _kit.Glow(EffectPriority.Small, from, 1.3f * _kit.ReadScale, FireGlow, 0.09f);
            if (Random.value < 0.35f) _kit.Heat(EffectPriority.Small, from + direction * range * Random.Range(0.3f, 0.8f) + Vector3.up * 0.6f, Vector3.up * 2.5f, Random.Range(2.5f, 4f), 0.6f);
            if (Random.value < 0.25f) _kit.Spark(EffectPriority.Small, from + direction, (direction + Random.insideUnitSphere * spread) * speed * 0.5f + Vector3.up * 3f, 0.05f * _kit.ReadScale, SparkHot, 0.5f);
        }

        /// <summary>A quick slash at the point of contact.</summary>
        public void MeleeHit(Vector3 position, in WeaponLook look, ArmorType armor)
        {
            float read = Mathf.Max(1f, _kit.ReadScale * 0.8f);
            _kit.Slash(EffectPriority.Small, position, look.ShotWidth * read, look.Shot, 0.16f, Random.Range(0f, 360f));
            _kit.Glow(EffectPriority.Small, position, look.ShotWidth * 0.5f * read, look.Shot, 0.08f);
            if (armor == ArmorType.Unarmored) return;
            for (int i = 0; i < 3; i++) _kit.Spark(EffectPriority.Small, position, Random.insideUnitSphere * 7f + Vector3.up * 3f, 0.05f * read, SparkHot, 0.28f);
        }

        // ------------------------------------------------------------------ what a shot does when it lands

        public void Impact(Vector3 position, Vector3 direction, ImpactStyle style, ArmorType armor, bool shielded, Color color)
        {
            float read = _kit.ReadScale;
            if (shielded && style != ImpactStyle.None)
            {
                ShieldHit(position);
                return;
            }
            switch (style)
            {
                case ImpactStyle.Bullet:
                    if (armor == ArmorType.Unarmored)
                    {
                        _kit.Dust(EffectPriority.Small, position, Vector3.up * 1.2f + Random.insideUnitSphere * 0.8f, 0.8f * read, DustTan, 0.45f);
                        break;
                    }
                    // Sparks on armour, thrown back the way the shot came.
                    _kit.Glow(EffectPriority.Small, position, 0.8f * read, SparkHot, 0.06f);
                    for (int i = 0; i < 4; i++) _kit.Spark(EffectPriority.Small, position, (-direction + Random.insideUnitSphere) * Random.Range(4f, 10f) + Vector3.up * 2f, 0.045f * read, SparkHot, Random.Range(0.14f, 0.3f));
                    break;
                case ImpactStyle.Plasma:
                    _kit.Glow(EffectPriority.Small, position, 1.5f * read, color, 0.13f);
                    for (int i = 0; i < 4; i++) _kit.Spark(EffectPriority.Small, position, (-direction + Random.insideUnitSphere * 1.2f) * Random.Range(3f, 7f) + Vector3.up * 2.5f, 0.07f * read, color, Random.Range(0.2f, 0.42f));
                    break;
                case ImpactStyle.Needle:
                    _kit.Glow(EffectPriority.Small, position, 1.1f * read, color, 0.1f);
                    for (int i = 0; i < 5; i++) _kit.Spark(EffectPriority.Small, position, Random.onUnitSphere * Random.Range(3f, 8f), 0.07f * read, color, Random.Range(0.12f, 0.28f));
                    break;
                case ImpactStyle.Laser:
                    _kit.Glow(EffectPriority.Medium, position, 2.2f * read, color, 0.26f);
                    for (int i = 0; i < 9; i++) _kit.Spark(EffectPriority.Medium, position, Random.onUnitSphere * Random.Range(4f, 12f) + Vector3.up * 3f, 0.06f * read, SparkHot, Random.Range(0.2f, 0.5f));
                    _kit.Smoke(EffectPriority.Small, position, Vector3.up * 2f, 1.6f, SmokeGrey, 1f);
                    break;
                case ImpactStyle.Bio:
                    _kit.Glow(EffectPriority.Small, position, 0.8f * read, new Color(0.6f, 0.85f, 0.25f), 0.08f);
                    for (int i = 0; i < 3; i++) _kit.Spore(EffectPriority.Small, position, Random.insideUnitSphere * 2.2f + Vector3.up * 0.8f, Random.Range(0.25f, 0.5f) * read, SporeGreen, Random.Range(0.8f, 1.4f));
                    break;
            }
        }

        /// <summary>A flash in the shield's blue, instead of damage showing on the body.</summary>
        public void ShieldHit(Vector3 position)
        {
            float read = _kit.ReadScale;
            _kit.Glow(EffectPriority.Small, position, 1.6f * read, ShieldBlue, 0.12f);
            for (int i = 0; i < 2; i++) _kit.Spark(EffectPriority.Small, position, Random.onUnitSphere * 4f, 0.05f * read, ShieldBlue, 0.18f);
        }

        // ------------------------------------------------------------------ blasts

        /// <summary>The blast of a splash weapon: its shock ring grows to exactly this radius.</summary>
        public void Blast(Vector3 position, float radius, BlastStyle style, Color color, EffectPriority priority)
        {
            Vector3 ground = new Vector3(position.x, 0.12f, position.z);
            bool grounded = position.y < 3f;
            Color ring = style == BlastStyle.Fire ? new Color(1f, 0.82f, 0.55f, 0.9f) : style == BlastStyle.Dust ? new Color(0.8f, 0.74f, 0.62f, 0.75f) : color;
            _kit.Ring(priority, grounded ? ground : position, radius * 2f, ring, 0.5f);
            int count = Mathf.Clamp(Mathf.RoundToInt(radius * 3f) + 4, 6, 20);
            EffectPriority lesser = priority == EffectPriority.Huge ? EffectPriority.Big : priority == EffectPriority.Big ? EffectPriority.Medium : EffectPriority.Small;

            switch (style)
            {
                case BlastStyle.Fire:
                    _kit.Glow(priority, position, radius * 2.8f, FireGlow, 0.18f);
                    if (grounded) _kit.GroundGlow(priority, ground, radius * 3.6f, GroundFire, 0.4f);
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 out3 = Random.onUnitSphere;
                        out3.y = Mathf.Abs(out3.y) * 0.8f + 0.2f;
                        _kit.Fire(priority, position + out3 * radius * 0.2f, out3 * radius * Random.Range(2f, 4f), radius * Random.Range(0.7f, 1.15f), Random.Range(0.34f, 0.62f));
                    }
                    for (int i = 0; i < count / 3 + 1; i++) _kit.Smoke(lesser, position + Random.insideUnitSphere * radius * 0.4f, Random.onUnitSphere * radius * 0.7f + Vector3.up * radius * 0.9f, radius * Random.Range(1.2f, 1.9f), SmokeDark, Random.Range(1.1f, 2.1f));
                    for (int i = 0; i < count; i++) _kit.Spark(lesser, position, Random.onUnitSphere * radius * Random.Range(3f, 7f) + Vector3.up * radius * 1.5f, 0.075f * _kit.ReadScale, SparkHot, Random.Range(0.3f, 0.8f));
                    for (int i = 0; i < count / 4; i++) _kit.Chunk(lesser, position, Random.onUnitSphere * radius * 3f + Vector3.up * radius * 3f, Random.Range(0.14f, 0.34f), MetalDark, Random.Range(1f, 1.8f));
                    if (grounded) _kit.Scorch(lesser, ground, radius * 1.7f, ScorchBlack, 45f);
                    _kit.Heat(lesser, position + Vector3.up * radius * 0.5f, Vector3.up * 2f, radius * 3f, 0.6f);
                    break;

                case BlastStyle.Plasma:
                    _kit.Glow(priority, position, radius * 2.6f, color, 0.26f);
                    _kit.Glow(priority, position, radius * 1.2f, Color.Lerp(color, Color.white, 0.6f), 0.14f);
                    if (grounded) _kit.GroundGlow(priority, ground, radius * 3f, new Color(color.r, color.g, color.b, 0.8f), 0.7f);
                    for (int i = 0; i < count; i++) _kit.Orb(lesser, position, Random.onUnitSphere * radius * Random.Range(1.2f, 2.8f) + Vector3.up * radius, radius * Random.Range(0.2f, 0.42f), new Color(color.r, color.g, color.b, 0.7f), Random.Range(0.25f, 0.5f));
                    for (int i = 0; i < count; i++) _kit.Spark(lesser, position, Random.onUnitSphere * radius * Random.Range(2.5f, 5f) + Vector3.up * radius * 1.6f, 0.085f * _kit.ReadScale, color, Random.Range(0.35f, 0.8f));
                    if (grounded) _kit.Scorch(lesser, ground, radius * 1.4f, new Color(0.04f, 0.05f, 0.07f, 0.72f), 35f);
                    break;

                case BlastStyle.Bio:
                    _kit.Glow(priority, position, radius * 2.2f, new Color(0.62f, 0.9f, 0.25f), 0.2f);
                    if (grounded) _kit.GroundGlow(priority, ground, radius * 2.6f, new Color(0.5f, 0.8f, 0.2f, 0.55f), 0.5f);
                    for (int i = 0; i < count; i++) _kit.Chunk(lesser, position, Random.onUnitSphere * radius * Random.Range(2f, 4.5f) + Vector3.up * radius * 2.5f, Random.Range(0.14f, 0.36f), i % 2 == 0 ? FleshPink : FleshSick, Random.Range(0.9f, 1.8f));
                    for (int i = 0; i < count; i++) _kit.Spore(lesser, position + Random.insideUnitSphere * radius * 0.6f, Random.insideUnitSphere * radius * 1.3f + Vector3.up * 1f, Random.Range(0.3f, 0.65f) * _kit.ReadScale, SporeGreen, Random.Range(1.2f, 2.4f));
                    for (int i = 0; i < count / 3 + 1; i++) _kit.Smoke(lesser, position, Random.onUnitSphere * radius * 0.9f + Vector3.up * radius * 0.4f, radius * Random.Range(1f, 1.6f), SporeCloud, Random.Range(1.2f, 2.4f));
                    if (grounded) _kit.Scorch(lesser, ground, radius * 1.5f, SplatterDark, 30f);
                    break;

                case BlastStyle.Dust:
                    for (int i = 0; i < count; i++)
                    {
                        float angle = i * Mathf.PI * 2f / count;
                        var out3 = new Vector3(Mathf.Cos(angle), 0.15f, Mathf.Sin(angle));
                        _kit.Dust(priority, ground + out3 * radius * 0.25f, out3 * radius * Random.Range(1.6f, 2.8f), radius * Random.Range(0.45f, 0.8f), DustTan, Random.Range(0.5f, 0.9f));
                    }
                    for (int i = 0; i < count / 3; i++) _kit.Chunk(lesser, ground, Random.onUnitSphere * radius * 1.5f + Vector3.up * radius * 2.2f, Random.Range(0.12f, 0.28f), DirtBrown, Random.Range(0.7f, 1.2f));
                    break;
            }
        }

        /// <summary>A rolling fireball: the heart of every vehicle's death. Radius is how far the fire reaches.</summary>
        public void Fireball(Vector3 position, float radius, EffectPriority priority)
        {
            EffectPriority lesser = priority == EffectPriority.Huge ? EffectPriority.Big : priority == EffectPriority.Big ? EffectPriority.Medium : EffectPriority.Small;
            _kit.Glow(priority, position, radius * 3.6f, FireGlow, 0.24f);
            _kit.Glow(priority, position, radius * 1.8f, Color.white, 0.1f);
            int fire = Mathf.Clamp(Mathf.RoundToInt(radius * 5f) + 8, 12, 46);
            for (int i = 0; i < fire; i++)
            {
                Vector3 out3 = Random.onUnitSphere;
                out3.y = Mathf.Abs(out3.y) * 0.9f + 0.1f;
                _kit.Fire(priority, position + out3 * radius * Random.Range(0.1f, 0.5f), out3 * radius * Random.Range(1.5f, 3.6f), radius * Random.Range(0.75f, 1.3f), Random.Range(0.5f, 1.05f));
            }
            int smoke = Mathf.Clamp(Mathf.RoundToInt(radius * 2f) + 4, 6, 22);
            for (int i = 0; i < smoke; i++) _kit.Smoke(lesser, position + Random.insideUnitSphere * radius * 0.5f, Random.onUnitSphere * radius * 0.8f + Vector3.up * radius * Random.Range(0.8f, 1.6f), radius * Random.Range(1.1f, 1.9f), SmokeDark, Random.Range(1.6f, 3.2f));
            int sparks = Mathf.Clamp(Mathf.RoundToInt(radius * 7f), 12, 60);
            for (int i = 0; i < sparks; i++) _kit.Spark(lesser, position, Random.onUnitSphere * radius * Random.Range(3f, 8f) + Vector3.up * radius * 2f, 0.085f * _kit.ReadScale, SparkHot, Random.Range(0.4f, 1.1f));
            _kit.Heat(lesser, position + Vector3.up * radius, Vector3.up * 3f, radius * 4f, 0.9f);
        }

        // ------------------------------------------------------------------ deaths

        /// <summary>A body falls with a puff of dust.</summary>
        public void InfantryDeath(Vector3 position, float bodyRadius)
        {
            Vector3 ground = new Vector3(position.x, 0.25f, position.z);
            float size = Mathf.Max(0.8f, bodyRadius * 2f);
            for (int i = 0; i < 3; i++) _kit.Dust(EffectPriority.Medium, ground + Random.insideUnitSphere * 0.3f, Random.insideUnitSphere * 1.4f + Vector3.up * 0.6f, size * Random.Range(1f, 1.5f), DustTan, Random.Range(0.45f, 0.8f));
        }

        /// <summary>A vehicle blows up: fireball, smoke, sparks, flying debris, a shock ring and a scorch mark. The wreck goes on burning (DeathEffects).</summary>
        public void VehicleDeath(Vector3 position, float bodyRadius, bool titan)
        {
            Vector3 middle = position + Vector3.up * Mathf.Max(0.8f, bodyRadius * 0.45f);
            float radius = bodyRadius * 1.7f + 1f;
            EffectPriority priority = titan ? EffectPriority.Huge : EffectPriority.Big;
            Fireball(middle, radius, priority);
            Vector3 ground = new Vector3(position.x, 0.12f, position.z);
            _kit.Ring(priority, ground, radius * 3.4f, new Color(1f, 0.8f, 0.55f, 0.85f), 0.65f);
            _kit.GroundGlow(priority, ground, radius * 5f, GroundFire, 0.7f);
            _kit.Scorch(priority, ground, radius * 3.2f, ScorchBlack, 60f);
            int chunks = Mathf.Clamp(Mathf.RoundToInt(bodyRadius * 7f) + 6, 8, 40);
            for (int i = 0; i < chunks; i++)
            {
                Vector3 out3 = Random.onUnitSphere;
                out3.y = Mathf.Abs(out3.y) + 0.5f;
                Color color = i % 3 == 0 ? MetalHot : MetalDark;
                _kit.Chunk(priority, middle + out3 * bodyRadius * 0.3f, out3 * Random.Range(7f, 18f), Random.Range(0.25f, 0.5f + bodyRadius * 0.14f), color, Random.Range(1.8f, 3.6f));
            }
            CameraShake.Kick(position, titan ? 0.8f : 0.3f);
            if (!titan) return;
            // A giant goes up in a chain of blasts along its body.
            for (int i = 0; i < 6; i++)
            {
                Vector3 spot = position + new Vector3(Random.Range(-bodyRadius, bodyRadius), Random.Range(1f, bodyRadius), Random.Range(-bodyRadius, bodyRadius));
                FireballAfter(0.2f + i * 0.28f, spot, bodyRadius * Random.Range(0.45f, 0.75f), EffectPriority.Big);
            }
            FireballAfter(2f, position + Vector3.up * bodyRadius * 0.6f, bodyRadius * 1.5f, EffectPriority.Huge);
        }

        /// <summary>An air unit blows up where it flies; burning pieces rain down. The body itself falls too (DeathEffects).</summary>
        public void AirDeath(Vector3 position, float bodyRadius, bool flood)
        {
            float radius = bodyRadius * 1.5f + 0.8f;
            Fireball(position, radius, EffectPriority.Big);
            _kit.Ring(EffectPriority.Big, position, radius * 3f, new Color(1f, 0.8f, 0.55f, 0.6f), 0.45f);
            int chunks = Mathf.Clamp(Mathf.RoundToInt(bodyRadius * 8f) + 4, 6, 30);
            for (int i = 0; i < chunks; i++)
            {
                Color color = flood ? (i % 2 == 0 ? FleshPink : FleshSick) : i % 3 == 0 ? MetalHot : MetalDark;
                _kit.Chunk(EffectPriority.Medium, position, Random.onUnitSphere * Random.Range(5f, 14f), Random.Range(0.2f, 0.55f), color, Random.Range(1.6f, 3f));
            }
            if (flood) for (int i = 0; i < 16; i++) _kit.Spore(EffectPriority.Medium, position, Random.insideUnitSphere * 5f, Random.Range(0.35f, 0.7f) * _kit.ReadScale, SporeGreen, Random.Range(1.5f, 3f));
            CameraShake.Kick(position, 0.22f);
        }

        /// <summary>The trail of something falling on fire: call it again and again along the way.</summary>
        public void BurningTrail(Vector3 position, float bodyRadius, EffectPriority priority)
        {
            _kit.Fire(priority, position + Random.insideUnitSphere * bodyRadius * 0.4f, Random.insideUnitSphere * 1.5f + Vector3.up * 1.5f, bodyRadius * Random.Range(0.8f, 1.4f), Random.Range(0.4f, 0.7f));
            _kit.Smoke(priority, position, Vector3.up * 1.5f + Random.insideUnitSphere, bodyRadius * Random.Range(1f, 1.7f), SmokeDark, Random.Range(1.4f, 2.6f));
            if (Random.value < 0.5f) _kit.Spark(EffectPriority.Small, position, Random.onUnitSphere * 5f, 0.07f * _kit.ReadScale, SparkHot, 0.5f);
        }

        /// <summary>What falls from the sky hits the ground.</summary>
        public void GroundCrash(Vector3 position, float bodyRadius)
        {
            Vector3 ground = new Vector3(position.x, 0.5f, position.z);
            float radius = bodyRadius * 1.3f + 0.8f;
            Fireball(ground, radius, EffectPriority.Big);
            _kit.Ring(EffectPriority.Big, new Vector3(position.x, 0.12f, position.z), radius * 3f, new Color(1f, 0.8f, 0.55f, 0.8f), 0.55f);
            _kit.GroundGlow(EffectPriority.Big, new Vector3(position.x, 0.12f, position.z), radius * 4f, GroundFire, 0.55f);
            _kit.Scorch(EffectPriority.Big, ground, radius * 2.6f, ScorchBlack, 50f);
            CameraShake.Kick(position, 0.3f);
        }

        /// <summary>A Flood body bursts in gore and spores.</summary>
        public void FloodBurst(Vector3 position, float bodyRadius, bool inAir)
        {
            bool small = bodyRadius < 1.3f;
            EffectPriority priority = small ? EffectPriority.Medium : EffectPriority.Big;
            Vector3 middle = inAir ? position : position + Vector3.up * Mathf.Max(0.5f, bodyRadius * 0.6f);
            float read = _kit.ReadScale;
            _kit.Glow(priority, middle, Mathf.Max(1.2f, bodyRadius * 2.4f), new Color(0.55f, 0.8f, 0.22f), 0.14f);
            int chunks = small ? 5 : Mathf.Clamp(Mathf.RoundToInt(bodyRadius * 8f), 8, 36);
            for (int i = 0; i < chunks; i++)
            {
                Vector3 out3 = Random.onUnitSphere;
                out3.y = Mathf.Abs(out3.y) + 0.3f;
                _kit.Chunk(priority, middle, out3 * Random.Range(3f, 9f + bodyRadius), Random.Range(0.12f, 0.2f + bodyRadius * 0.14f), i % 2 == 0 ? FleshPink : FleshSick, Random.Range(0.8f, 1.8f));
            }
            int spores = small ? 5 : Mathf.Clamp(Mathf.RoundToInt(bodyRadius * 10f), 10, 60);
            for (int i = 0; i < spores; i++) _kit.Spore(priority, middle + Random.insideUnitSphere * bodyRadius * 0.5f, Random.insideUnitSphere * (2f + bodyRadius) + Vector3.up * 0.8f, Random.Range(0.25f, 0.6f) * read, SporeGreen, Random.Range(1f, 2.6f));
            int clouds = small ? 2 : Mathf.Clamp(Mathf.RoundToInt(bodyRadius * 2f), 3, 14);
            for (int i = 0; i < clouds; i++) _kit.Smoke(priority, middle, Random.insideUnitSphere * bodyRadius + Vector3.up * 0.8f, Mathf.Max(1.2f, bodyRadius * Random.Range(1.3f, 2f)), SporeCloud, Random.Range(0.9f, 2.2f));
            if (!inAir) _kit.Scorch(small ? EffectPriority.Small : EffectPriority.Big, position, Mathf.Max(1.4f, bodyRadius * 2.6f), SplatterDark, 25f);
            if (!small) CameraShake.Kick(position, Mathf.Clamp(bodyRadius * 0.1f, 0.1f, 0.5f));
        }

        /// <summary>
        /// The Infected Pelican comes down: the biggest thing on the field. burstRadius is the reach of its death burst, the size of
        /// the ring; the fire and the cloud go far beyond it.
        /// </summary>
        public void PelicanCrash(Vector3 position, float bodyRadius, float burstRadius)
        {
            Vector3 ground = new Vector3(position.x, 0.14f, position.z);
            Vector3 low = new Vector3(position.x, 1.5f, position.z);
            float radius = Mathf.Max(burstRadius, bodyRadius * 2f);
            const EffectPriority huge = EffectPriority.Huge;

            _kit.Glow(huge, low + Vector3.up * radius * 0.4f, radius * 7f, Color.white, 0.14f);
            _kit.Glow(huge, low, radius * 5f, new Color(0.85f, 1f, 0.5f), 0.4f);
            _kit.GroundGlow(huge, ground, radius * 7f, new Color(1f, 0.6f, 0.2f, 0.9f), 1.1f);
            _kit.GroundGlow(huge, ground, radius * 4.5f, new Color(0.55f, 0.95f, 0.2f, 0.7f), 2f);
            _kit.Ring(huge, ground, radius * 2f, new Color(1f, 0.9f, 0.65f, 1f), 0.45f);
            _kit.Ring(huge, ground, radius * 3.6f, new Color(0.8f, 1f, 0.5f, 0.7f), 0.85f);
            _kit.Scorch(huge, ground, radius * 3.2f, ScorchBlack, 90f);
            _kit.Scorch(huge, ground, radius * 2f, SplatterDark, 90f);

            for (int i = 0; i < 56; i++)
            {
                Vector3 out3 = Random.onUnitSphere;
                out3.y = Mathf.Abs(out3.y) * 0.75f + 0.25f;
                _kit.Fire(huge, low + out3 * radius * 0.25f, out3 * radius * Random.Range(1.6f, 4.2f), radius * Random.Range(0.45f, 0.95f), Random.Range(0.7f, 1.5f));
            }
            for (int i = 0; i < 26; i++)
            {
                Vector3 out3 = Random.onUnitSphere;
                out3.y = Mathf.Abs(out3.y);
                _kit.Smoke(huge, low + out3 * radius * 0.4f, out3 * radius * 0.9f + Vector3.up * radius * Random.Range(0.7f, 1.6f), radius * Random.Range(0.8f, 1.4f), SmokeDark, Random.Range(2.5f, 5f));
            }
            for (int i = 0; i < 22; i++) _kit.Smoke(huge, low + Random.insideUnitSphere * radius, Random.insideUnitSphere * radius * 0.6f + Vector3.up * radius * 0.35f, radius * Random.Range(0.8f, 1.3f), SporeCloud, Random.Range(3.5f, 7f));
            for (int i = 0; i < 150; i++) _kit.Spore(huge, low + Random.insideUnitSphere * radius, Random.insideUnitSphere * radius * 1.3f + Vector3.up * Random.Range(1f, 5f), Random.Range(0.35f, 0.8f) * _kit.ReadScale, SporeGreen, Random.Range(2f, 6f));
            for (int i = 0; i < 90; i++) _kit.Spark(huge, low, Random.onUnitSphere * radius * Random.Range(2f, 6f) + Vector3.up * radius * 2.5f, 0.1f * _kit.ReadScale, i % 3 == 0 ? new Color(0.75f, 1f, 0.35f) : SparkHot, Random.Range(0.6f, 1.8f));
            for (int i = 0; i < 46; i++)
            {
                Vector3 out3 = Random.onUnitSphere;
                out3.y = Mathf.Abs(out3.y) + 0.6f;
                Color color = i % 3 == 0 ? MetalHot : i % 3 == 1 ? MetalDark : FleshSick;
                _kit.Chunk(huge, low, out3 * Random.Range(10f, 28f), Random.Range(0.3f, 1.05f), color, Random.Range(2.2f, 4.5f));
            }
            for (int i = 0; i < 4; i++) _kit.Heat(huge, low + Vector3.up * radius * (0.4f + i * 0.5f) + Random.insideUnitSphere * radius * 0.5f, Vector3.up * 4f, radius * 3f, 1.2f);

            // The blast goes on rolling for a moment.
            for (int i = 0; i < 4; i++)
            {
                Vector3 spot = position + new Vector3(Random.Range(-radius, radius), Random.Range(1f, radius * 0.6f), Random.Range(-radius, radius)) * 0.7f;
                FireballAfter(0.18f + i * 0.2f, spot, radius * Random.Range(0.35f, 0.55f), EffectPriority.Big);
            }
            CameraShake.Kick(position, 1f);
        }
    }
}
