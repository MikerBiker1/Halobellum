// SpellView.cs
// Spells in the Match scene (Bible Sections 10 and 13, Milestone 7). MatchController adds it to the
// Battle object.
//   Deployment: each of the player's placed spells shows its area as a transparent disc with a ring,
//     red for offensive spells (anywhere on the field) and blue for defensive ones (your own side).
//     While a spell is being placed, its outline follows the mouse (grey where it cannot go):
//     left-click places it, right-click or Escape cancels. The enemy's spells are never shown here.
//   Battle: when the battle starts, every spell of both sides shows its outline (they are revealed
//     now) until it has gone off. Each effect flashes: strikes and zone ticks a bright burst in the
//     colour of their damage type, buffs a ring pulse; zones glow while they last. Drop-pod squads
//     fall from the sky (UnitViewManager).
// Discs are flat quads with a texture drawn in code and the built-in Sprites/Default shader (always
// included in builds), so no material assets are needed. Looks only; the simulation is unchanged.
// Depends on: MatchController, BattleController, UiHitAreas, PlaceholderArt, Halobellum.Combat,
//             Halobellum.Economy (OwnedSpell), Halobellum.Core (SpellStats), Unity Input System.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Halobellum.Presentation
{
    public sealed class SpellView : MonoBehaviour
    {
        public static readonly Color OffensiveColor = new Color(1f, 0.25f, 0.2f);
        public static readonly Color DefensiveColor = new Color(0.25f, 0.6f, 1f);
        private static readonly Color BlockedColor = new Color(0.6f, 0.6f, 0.6f);
        private const float DiscHeight = 0.08f;
        private const float FlashSeconds = 0.9f;

        private MatchController _match;
        private BattleController _battle;
        private Material _discMaterial, _glowMaterial;
        private MaterialPropertyBlock _block;
        private readonly List<Renderer> _outlines = new List<Renderer>();
        private int _outlinesUsed;
        private BattleSimulation _shown;

        private sealed class Flash
        {
            public Renderer Renderer;
            public float Start;
            public float Radius;
            public Color Color;
            public bool Ring;
        }
        private readonly List<Flash> _flashes = new List<Flash>();

        /// <summary>Outlines drawn this frame (for tests).</summary>
        public int VisibleOutlines => _outlinesUsed;

        private void Start()
        {
            _match = GetComponent<MatchController>();
            _battle = GetComponent<BattleController>();
            _block = new MaterialPropertyBlock();
            Shader shader = Shader.Find("Sprites/Default");
            _discMaterial = new Material(shader) { name = "Spell Outline", mainTexture = MakeDiscTexture() };
            _glowMaterial = new Material(shader) { name = "Spell Flash", mainTexture = MakeGlowTexture() };
            _battle.BattleChanged += OnBattleChanged;
            OnBattleChanged(_battle.Simulation);
        }

        private void OnDestroy()
        {
            if (_battle != null) _battle.BattleChanged -= OnBattleChanged;
            if (_shown != null) _shown.Events.SpellTriggered -= OnSpellTriggered;
            if (_discMaterial != null) Destroy(_discMaterial);
            if (_glowMaterial != null) Destroy(_glowMaterial);
        }

        private void OnBattleChanged(BattleSimulation simulation)
        {
            if (_shown != null) _shown.Events.SpellTriggered -= OnSpellTriggered;
            _shown = simulation;
            if (_shown != null) _shown.Events.SpellTriggered += OnSpellTriggered;
        }

        private void Update()
        {
            _outlinesUsed = 0;
            if (_match != null && _match.Match != null)
            {
                if (_match.IsDeploying) UpdateDeployment();
                else if (_shown != null && _shown.Spells.Count > 0 && _match.Match.Phase == MatchPhase.Battle) UpdateBattle();
            }
            for (int i = _outlinesUsed; i < _outlines.Count; i++)
            {
                if (_outlines[i].gameObject.activeSelf) _outlines[i].gameObject.SetActive(false);
            }
            UpdateFlashes();
        }

        // ------------------------------------------------------------------ deployment

        private void UpdateDeployment()
        {
            foreach (OwnedSpell spell in _match.Match.Player.Spells)
            {
                if (spell.IsPlaced && spell.Id != _match.PlacingSpellId) ShowOutline(spell.Position, spell.Stats, ColorFor(spell.Stats), 1f);
            }

            OwnedSpell placing = _match.PlacingSpell;
            if (placing == null) return;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                _match.CancelSpellPlacement();
                return;
            }
            if (mouse == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (UiHitAreas.Contains(pointer) || !PointerOnGround(pointer, out Vec2 position)) return;
            bool valid = _match.CanPlaceSpellAt(position);
            ShowOutline(position, placing.Stats, valid ? ColorFor(placing.Stats) : BlockedColor, 1f);
            if (mouse.leftButton.wasPressedThisFrame && valid) _match.PlaceSpellAt(position);
        }

        private static bool PointerOnGround(Vector2 pointer, out Vec2 position)
        {
            position = Vec2.Zero;
            Camera camera = Camera.main;
            if (camera == null) return false;
            Ray ray = camera.ScreenPointToRay(pointer);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
            float distance = -ray.origin.y / ray.direction.y;
            if (distance < 0f) return false;
            Vector3 hit = ray.origin + ray.direction * distance;
            position = new Vec2(hit.x, hit.z);
            return true;
        }

        // ------------------------------------------------------------------ battle

        private void UpdateBattle()
        {
            float now = _shown.ElapsedSeconds;
            foreach (SimSpell spell in _shown.Spells)
            {
                if (spell.IsDone) continue;
                // Revealed at the start: the area is outlined until it goes off; a running zone pulses.
                bool active = spell.TicksDone > 0;
                float pulse = active ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
                Color color = active ? EffectColor(spell.Stats) : ColorFor(spell.Stats);
                ShowOutline(spell.Position, spell.Stats, color, pulse);
            }
        }

        private void OnSpellTriggered(SimSpell spell)
        {
            SpellStats stats = spell.Stats;
            switch (stats.Effect)
            {
                case SpellEffect.Damage:
                    // A single strike gets one big burst; zone ticks smaller ones at random spots inside.
                    if (stats.Ticks <= 1) AddFlash(spell.Position, stats.Radius * 1.15f, EffectColor(stats), false);
                    else
                    {
                        float angle = spell.TicksDone * 2.4f, distance = stats.Radius * 0.5f * ((spell.TicksDone * 37 % 10) / 10f);
                        var offset = new Vec2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
                        AddFlash(spell.Position + offset, stats.Radius * 0.55f, EffectColor(stats), false);
                    }
                    break;
                case SpellEffect.Buff:
                    AddFlash(spell.Position, stats.Radius, EffectColor(stats), true);
                    break;
                default:
                    AddFlash(spell.Position, Mathf.Max(3f, stats.Radius), new Color(1f, 0.85f, 0.5f), false);
                    break;
            }
        }

        // ------------------------------------------------------------------ drawing

        public static Color ColorFor(SpellStats stats) => stats.Offensive ? OffensiveColor : DefensiveColor;

        /// <summary>The colour of the effect itself: explosions orange, plasma blue-violet, buffs by kind.</summary>
        private static Color EffectColor(SpellStats stats)
        {
            if (stats.Effect == SpellEffect.Buff) return stats.ShieldBonusShare > 0f ? new Color(0.4f, 0.8f, 1f) : new Color(1f, 0.85f, 0.3f);
            switch (stats.IsAntiArmor ? DamageType.Explosive : stats.DamageType)
            {
                case DamageType.Energy: return new Color(0.55f, 0.6f, 1f);
                case DamageType.Fire: return new Color(1f, 0.45f, 0.1f);
                case DamageType.Explosive: return new Color(1f, 0.65f, 0.25f);
                default: return new Color(1f, 0.95f, 0.8f);
            }
        }

        private void ShowOutline(Vec2 position, SpellStats stats, Color color, float alpha)
        {
            if (_outlinesUsed == _outlines.Count) _outlines.Add(CreateDisc("Spell Outline", _discMaterial));
            Renderer disc = _outlines[_outlinesUsed++];
            float radius = Mathf.Max(1f, stats.Radius);
            disc.transform.SetPositionAndRotation(new Vector3(position.X, DiscHeight, position.Y), Quaternion.Euler(90f, 0f, 0f));
            disc.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            color.a = alpha;
            SetColor(disc, color);
            if (!disc.gameObject.activeSelf) disc.gameObject.SetActive(true);
        }

        private void AddFlash(Vec2 position, float radius, Color color, bool ring)
        {
            Flash flash = _flashes.Find(f => !f.Renderer.gameObject.activeSelf);
            if (flash == null)
            {
                flash = new Flash();
                _flashes.Add(flash);
            }
            if (flash.Renderer == null || flash.Ring != ring)
            {
                if (flash.Renderer != null) Destroy(flash.Renderer.gameObject);
                flash.Renderer = CreateDisc(ring ? "Spell Pulse" : "Spell Flash", ring ? _discMaterial : _glowMaterial);
            }
            flash.Start = Time.unscaledTime;
            flash.Radius = radius;
            flash.Color = color;
            flash.Ring = ring;
            flash.Renderer.transform.SetPositionAndRotation(new Vector3(position.X, DiscHeight + 0.02f, position.Y), Quaternion.Euler(90f, 0f, 0f));
            flash.Renderer.gameObject.SetActive(true);
        }

        private void UpdateFlashes()
        {
            foreach (Flash flash in _flashes)
            {
                if (!flash.Renderer.gameObject.activeSelf) continue;
                float t = (Time.unscaledTime - flash.Start) / FlashSeconds;
                if (t >= 1f)
                {
                    flash.Renderer.gameObject.SetActive(false);
                    continue;
                }
                // Grows quickly, then fades.
                float size = flash.Radius * 2f * (flash.Ring ? Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(t)) : Mathf.Lerp(0.4f, 1f, Mathf.Min(1f, t * 4f)));
                flash.Renderer.transform.localScale = new Vector3(size, size, 1f);
                Color color = flash.Color;
                color.a = Mathf.Pow(1f - t, 1.5f);
                SetColor(flash.Renderer, color);
            }
        }

        private Renderer CreateDisc(string name, Material material)
        {
            Transform disc = PlaceholderArt.CreatePart(name, transform, PlaceholderArt.Quad, material);
            disc.gameObject.SetActive(false);
            return disc.GetComponent<Renderer>();
        }

        private void SetColor(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor("_Color", color);
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>A white disc: a faint fill with a bright ring at the edge (tinted per spell).</summary>
        private static Texture2D MakeDiscTexture()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Spell Disc", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = 1f - r; // 0 at the rim.
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(edge - 0.025f) / 0.025f);
                    float fill = r < 1f ? 0.22f + 0.1f * r * r : 0f;
                    float alpha = Mathf.Clamp01(Mathf.Max(fill, ring * 0.95f)) * Mathf.Clamp01(edge * size * 0.5f + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>A soft white glow, bright in the middle (explosion bursts).</summary>
        private static Texture2D MakeGlowTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Spell Glow", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - r);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha * (3f - 2f * alpha));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
