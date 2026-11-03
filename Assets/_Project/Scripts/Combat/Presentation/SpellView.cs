// SpellView.cs
// Spells in the Match scene (Bible Sections 10 and 13, Milestone 7). MatchController adds it to the
// Battle object.
//   Deployment: each of the player's placed spells shows its area as a transparent disc with a ring,
//     red for offensive spells (anywhere on the field) and blue for defensive ones (your own side).
//     While a spell is being placed, its outline follows the mouse (grey where it cannot go):
//     left-click places it, a right-click (not a drag: RightClick) or Escape cancels. The enemy's spells are never shown here.
//   Battle: when the battle starts, every spell of both sides shows its outline (they are revealed
//     now) until it has gone off. Each effect flashes: strikes and zone ticks a bright burst in the
//     colour of their damage type, buffs and EMPs a ring pulse; zones glow while they last; smoke is
//     a grey cloud for as long as it hangs. Drop-pod squads fall from the sky (UnitViewManager).
//   Line-shaped spells (smoke, later a napalm strike) are placed in two clicks (decision D35): the
//     first sets one end, the other end then follows the mouse, a second click confirms.
// SpellEffects (run by CombatEffectsView, so that spells also show in a sandbox or test scene) draws what each strike looks like: M10.
// Discs are flat quads with a texture drawn in code and the built-in Sprites/Default shader (always
// included in builds), so no material assets are needed. Looks only; the simulation is unchanged.
// Depends on: MatchController, BattleController, RightClick, UiHitAreas, PlaceholderArt, Halobellum.Combat,
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
        private Material _discMaterial, _glowMaterial, _rectMaterial;
        private readonly List<Renderer> _rectOutlines = new List<Renderer>();
        private int _rectsUsed;
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
        public int VisibleOutlines => _outlinesUsed + _rectsUsed;

        private void Start()
        {
            _match = GetComponent<MatchController>();
            _battle = GetComponent<BattleController>();
            _block = new MaterialPropertyBlock();
            Shader shader = Shader.Find("Sprites/Default");
            _discMaterial = new Material(shader) { name = "Spell Outline", mainTexture = MakeDiscTexture() };
            _glowMaterial = new Material(shader) { name = "Spell Flash", mainTexture = MakeGlowTexture() };
            _rectMaterial = new Material(shader) { name = "Spell Rectangle", mainTexture = MakeRectTexture() };
            _battle.BattleChanged += OnBattleChanged;
            OnBattleChanged(_battle.Simulation);
        }

        private void OnDestroy()
        {
            if (_battle != null) _battle.BattleChanged -= OnBattleChanged;
            if (_shown != null) _shown.Events.SpellTriggered -= OnSpellTriggered;
            if (_discMaterial != null) Destroy(_discMaterial);
            if (_glowMaterial != null) Destroy(_glowMaterial);
            if (_rectMaterial != null) Destroy(_rectMaterial);
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
            _rectsUsed = 0;
            if (_match != null && _match.Match != null)
            {
                if (_match.IsDeploying) UpdateDeployment();
                else if (_shown != null && _shown.Spells.Count > 0 && _match.Match.Phase == MatchPhase.Battle) UpdateBattle();
            }
            for (int i = _outlinesUsed; i < _outlines.Count; i++)
            {
                if (_outlines[i].gameObject.activeSelf) _outlines[i].gameObject.SetActive(false);
            }
            for (int i = _rectsUsed; i < _rectOutlines.Count; i++)
            {
                if (_rectOutlines[i].gameObject.activeSelf) _rectOutlines[i].gameObject.SetActive(false);
            }
            UpdateFlashes();
        }

        // ------------------------------------------------------------------ deployment

        private void UpdateDeployment()
        {
            if (_match.PlacingItem != null)
            {
                Keyboard itemKeyboard = Keyboard.current;
                if ((itemKeyboard != null && itemKeyboard.escapeKey.wasPressedThisFrame) || RightClick.WasClicked)
                    _match.CancelItemPlacement();
            }

            foreach (OwnedSpell spell in _match.Match.Player.Spells)
            {
                if (spell.IsPlaced && spell.Id != _match.PlacingSpellId) ShowOutline(spell.Position, spell.Stats, spell.AngleDegrees, ColorFor(spell.Stats), 1f);
            }

            OwnedSpell placing = _match.PlacingSpell;
            if (placing == null) return;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || RightClick.WasClicked)
            {
                if (_match.PlacingLineStart != null) _match.ClearLineStart(); // Back one step.
                else _match.CancelSpellPlacement();
                return;
            }
            if (mouse == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (UiHitAreas.Contains(pointer) || !PointerOnGround(pointer, out Vec2 position)) return;
            bool click = mouse.leftButton.wasPressedThisFrame;
            if (!placing.Stats.IsRectangle)
            {
                bool valid = _match.CanPlaceSpellAt(position);
                ShowOutline(position, placing.Stats, 0f, valid ? ColorFor(placing.Stats) : BlockedColor, 1f);
                if (click && valid) _match.PlaceSpellAt(position);
                return;
            }

            // A line: first the end that stays, then the end that follows the mouse.
            if (_match.PlacingLineStart == null)
            {
                bool valid = _match.CanPlaceSpellAt(position);
                ShowDisc(position, placing.Stats.AreaDepth * 0.5f, valid ? ColorFor(placing.Stats) : BlockedColor, 1f);
                if (click && valid) _match.SetLineStart(position);
                return;
            }
            Vec2 centre = placing.Stats.LineFrom(_match.PlacingLineStart.Value, position, out float angle);
            bool lineValid = _match.CanPlaceSpellAt(centre);
            ShowOutline(centre, placing.Stats, angle, lineValid ? ColorFor(placing.Stats) : BlockedColor, 1f);
            if (click && lineValid) _match.PlaceLineTowards(position);
        }

        /// <summary>The point on the ground under the pointer, seen from wherever the camera is now.</summary>
        public static bool PointerOnGround(Vector2 pointer, out Vec2 position)
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
            IReadOnlyList<SimSpell> spells = _shown.Spells; // By index: this runs every frame, and foreach here would box an enumerator.
            for (int i = 0; i < spells.Count; i++)
            {
                SimSpell spell = spells[i];
                if (spell.IsOverAt(now)) continue;
                // Revealed at the start: the area is outlined until it goes off; a running zone pulses.
                bool active = spell.TicksDone > 0;
                bool smoke = spell.Stats.Lingers;
                float pulse = active && !smoke ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
                Color color = active ? EffectColor(spell.Stats) : ColorFor(spell.Stats);
                ShowOutline(spell.Position, spell.Stats, spell.AngleDegrees, color, pulse);
            }
        }

        private void OnSpellTriggered(SimSpell spell)
        {
            SpellStats stats = spell.Stats;
            switch (stats.Effect)
            {
                case SpellEffect.Damage:
                    break; // SpellEffects draws the strike itself (M10).
                case SpellEffect.Buff:
                case SpellEffect.Disrupt:
                    AddFlash(spell.Position, stats.Radius, EffectColor(stats), true);
                    break;
                case SpellEffect.Smoke:
                case SpellEffect.Oil:
                    break; // The area is the effect.
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
            if (stats.Effect == SpellEffect.Smoke) return new Color(0.72f, 0.74f, 0.76f);
            if (stats.Effect == SpellEffect.Oil) return new Color(0.3f, 0.24f, 0.14f);
            if (stats.Effect == SpellEffect.Disrupt) return new Color(0.5f, 0.95f, 1f);
            switch (stats.IsAntiArmor ? DamageType.Explosive : stats.DamageType)
            {
                case DamageType.Energy: return new Color(0.55f, 0.6f, 1f);
                case DamageType.Fire: return new Color(1f, 0.45f, 0.1f);
                case DamageType.Explosive: return new Color(1f, 0.65f, 0.25f);
                default: return new Color(1f, 0.95f, 0.8f);
            }
        }

        private void ShowOutline(Vec2 position, SpellStats stats, float angleDegrees, Color color, float alpha)
        {
            if (stats.IsRectangle)
            {
                if (_rectsUsed == _rectOutlines.Count) _rectOutlines.Add(CreateDisc("Spell Rectangle", _rectMaterial));
                Renderer rectangle = _rectOutlines[_rectsUsed++];
                // The field's Y is the world's Z, and a turn about the world's up axis runs the other way.
                rectangle.transform.SetPositionAndRotation(new Vector3(position.X, DiscHeight, position.Y), Quaternion.Euler(90f, -angleDegrees, 0f));
                rectangle.transform.localScale = new Vector3(stats.AreaWidth, stats.AreaDepth, 1f);
                color.a = alpha;
                SetColor(rectangle, color);
                if (!rectangle.gameObject.activeSelf) rectangle.gameObject.SetActive(true);
                return;
            }
            ShowDisc(position, Mathf.Max(1f, stats.Radius), color, alpha);
        }

        private void ShowDisc(Vec2 position, float radius, Color color, float alpha)
        {
            if (_outlinesUsed == _outlines.Count) _outlines.Add(CreateDisc("Spell Outline", _discMaterial));
            Renderer disc = _outlines[_outlinesUsed++];
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

        /// <summary>A white rectangle: a soft fill with a brighter edge (smoke; stretched to its area).</summary>
        private static Texture2D MakeRectTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Spell Rectangle", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, edge < 2 ? 0.95f : 0.4f);
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
