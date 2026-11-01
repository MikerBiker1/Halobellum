// SquadStatsWindow.cs
// The bottom-left stats window (Bible Section 13). It appears whenever a squad is selected and
// shows its stats from the unit data: squad size (and bodies still alive), cost, tier, role,
// health, armor type, shield, move speed, collision size, target filter, and for each weapon its
// damage, rate of fire, range, splash radius and damage type, plus any defense arc.
// Below the stats it shows the squad's level, XP bar, level-up button with its price and its tech
// upgrades with Buy buttons (Milestone 6), from the scene's ISquadProgression (the Match or the
// Sandbox); the opponent's squads only show their level.
// Placeholder UI drawn with Unity's immediate-mode GUI.
// Depends on: Halobellum.Presentation (BattleController, SquadSelection, ISquadProgression, UiHitAreas), Halobellum.Core.

using System.Text;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Presentation;
using UnityEngine;

namespace Halobellum.UI
{
    public sealed class SquadStatsWindow : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private float windowWidth = 440f;

        private SquadSelection _selection;
        private ISquadProgression _progression;
        private GUIStyle _title, _text, _small, _center, _techName;
        private readonly StringBuilder _builder = new StringBuilder();

        private void Start()
        {
            if (battle != null)
            {
                _selection = battle.GetComponent<SquadSelection>();
                _progression = battle.GetComponent<ISquadProgression>();
            }
        }

        private void OnGUI()
        {
            if (battle == null || battle.Simulation == null || _selection == null || _selection.SelectedSquadId < 0) return;
            HudTheme.Apply();
            EnsureStyles();
            float scale = HudScale.Apply();

            SimUnit sample = FindAnyOfSquad(_selection.SelectedSquadId, out int alive, out float totalHealth);
            if (sample == null) return;
            UnitStats s = sample.Stats;

            string body = BuildText(s, alive, totalHealth);
            float textHeight = _text.CalcHeight(new GUIContent(body), windowWidth - 24f);
            SquadProgressView progress = _progression?.DescribeProgress(_selection.SelectedSquadId);
            float height = 44f + textHeight + ProgressHeight(progress);
            var window = new Rect(10f, HudScale.ScaledHeight(scale) - height - 10f, windowWidth, height);
            UiHitAreas.Add(HudScale.ToScreen(window, scale));

            HudTheme.PanelBox(window);
            GUI.Label(new Rect(window.x + 12f, window.y + 8f, windowWidth - 50f, 26f), $"{s.Name}  ({progress?.SideLabel ?? $"Team {sample.Team}"})", _title);
            if (GUI.Button(new Rect(window.xMax - 34f, window.y + 8f, 24f, 22f), "x")) _selection.Select(-1);
            GUI.Label(new Rect(window.x + 12f, window.y + 38f, windowWidth - 24f, textHeight), body, _text);
            if (progress != null) DrawProgress(progress, window.x + 12f, window.y + 42f + textHeight, windowWidth - 24f);
        }

        private const float LineHeight = 24f, TechRowHeight = 44f;

        private static float ProgressHeight(SquadProgressView p)
        {
            if (p == null) return 0f;
            float height = 10f + 30f; // Gap, level line (with buttons).
            if (p.ShowXp) height += 22f;
            if (!string.IsNullOrEmpty(p.Note)) height += LineHeight;
            if (p.Techs.Count > 0) height += LineHeight + p.Techs.Count * TechRowHeight;
            return height;
        }

        /// <summary>Level, XP bar, level-up button and tech upgrades.</summary>
        private void DrawProgress(SquadProgressView p, float x, float y, float width)
        {
            y += 10f;
            GUI.Label(new Rect(x, y, 170f, LineHeight + 6f), $"Level {p.Level} of {p.MaxLevel}", _title);
            float buttonX = x + width;
            if (p.LevelUpLabel != null)
            {
                buttonX -= 150f;
                GUI.enabled = p.LevelUpEnabled;
                if (GUI.Button(new Rect(buttonX, y, 150f, 28f), p.LevelUpLabel) && p.OnLevelUp != null) p.OnLevelUp();
                GUI.enabled = true;
            }
            if (p.OnLevelDown != null)
            {
                buttonX -= 96f;
                if (GUI.Button(new Rect(buttonX, y, 90f, 28f), "Level -")) p.OnLevelDown();
            }
            y += 30f;

            if (p.ShowXp)
            {
                var bar = new Rect(x, y + 3f, width, 16f);
                HudTheme.Bar(bar, p.XpNeeded > 0f ? p.Xp / p.XpNeeded : 0f, HudTheme.Accent);
                GUI.Label(bar, $"XP {p.Xp:0} / {p.XpNeeded:0}", _center);
                y += 22f;
            }
            if (!string.IsNullOrEmpty(p.Note))
            {
                GUI.Label(new Rect(x, y, width, LineHeight), p.Note, _text);
                y += LineHeight;
            }
            if (p.Techs.Count == 0) return;

            GUI.Label(new Rect(x, y, width, LineHeight), "Tech upgrades (every squad of this type):", _text);
            y += LineHeight;
            foreach (TechLine line in p.Techs)
            {
                string name = line.Owned ? $"{line.Tech.Name}  (researched)" : line.Tech.Name;
                GUI.Label(new Rect(x, y, width - 110f, 22f), name, _techName);
                GUI.Label(new Rect(x, y + 20f, width - 110f, 22f), line.Tech.EffectsText(), _small);
                if (line.ButtonLabel != null)
                {
                    GUI.enabled = line.ButtonEnabled;
                    if (GUI.Button(new Rect(x + width - 100f, y + 6f, 100f, 30f), line.ButtonLabel) && line.OnButton != null) line.OnButton();
                    GUI.enabled = true;
                }
                y += TechRowHeight;
            }
        }

        private string BuildText(UnitStats s, int alive, float totalHealth)
        {
            StringBuilder b = _builder.Clear();
            b.AppendLine($"Role: {RoleName(s.Role)}    Tier {s.Tier}    Cost {s.Cost}{(s.Level > 1 ? $"    Level {s.Level}" : "")}");
            b.AppendLine($"Squad size: {s.SquadSize}  ({alive} alive, {totalHealth:0} health left)");
            b.AppendLine($"Health: {s.MaxHealth:0} per body    Armor: {s.ArmorType}");
            b.AppendLine(s.HasShield ? $"Shield: {s.MaxShield:0} (regen {s.ShieldRegenPerSecond:0.#}/s)" : "Shield: none");
            b.AppendLine($"Move speed: {s.MoveSpeed:0.#} m/s    Collision size: {s.CollisionRadius * 2f:0.#} m");
            b.AppendLine(s.TurnTime180 > 0f ? $"Turn time: {s.TurnTime180:0.##} s for 180 degrees" : "Turn time: instant");
            b.AppendLine($"Targets: {s.TargetFilter}{(s.IsAir ? "    (air unit)" : "")}");
            for (int i = 0; i < s.Weapons.Length; i++)
            {
                WeaponStats w = s.Weapons[i];
                string main = s.Weapons.Length > 1 && i == 0 ? " (main)" : "";
                b.AppendLine();
                b.AppendLine($"{w.Name}{main}");
                b.AppendLine($"  Damage {w.DamagePerShot:0.#} {w.DamageType}{(w.IsAntiArmor ? ", anti-armor" : "")}    Rate {w.ShotsPerSecond:0.##}/s");
                string minRange = w.MinRange > 0f ? $" (min {w.MinRange:0.#})" : "";
                string splash = w.SplashRadius > 0f ? $"{w.SplashRadius:0.#} m" : "none";
                b.AppendLine($"  Range {w.Range:0.#} m{minRange}    Splash {splash}");
            }
            if (s.HasDefenseArc)
            {
                DefenseArcStats arc = s.DefenseArc;
                b.AppendLine();
                b.AppendLine($"Defense arc: {arc.ArcDegrees:0} degrees, front x{arc.FrontDamageMultiplier:0.##}, rear x{arc.RearDamageMultiplier:0.##}");
                if (arc.BlockChance > 0f) b.AppendLine($"  Blocks {arc.BlockChance * 100f:0}% of {string.Join("/", arc.BlockedDamageTypes)} projectiles");
            }
            return b.ToString().TrimEnd();
        }

        private SimUnit FindAnyOfSquad(int squadId, out int alive, out float totalHealth)
        {
            SimUnit sample = null;
            alive = 0;
            totalHealth = 0f;
            foreach (SimUnit unit in battle.Simulation.Units)
            {
                if (unit.SquadId != squadId) continue;
                if (sample == null) sample = unit;
                if (!unit.IsAlive) continue;
                alive++;
                totalHealth += unit.Health;
            }
            return sample;
        }

        private static string RoleName(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.ChaffClear: return "Chaff clear";
                case UnitRole.SingleTargetDps: return "Single-target DPS";
                case UnitRole.AntiAir: return "Anti-air";
                default: return role.ToString();
            }
        }

        private int _styleVersion = -1;

        private void EnsureStyles()
        {
            if (_title != null && _styleVersion == HudTheme.Version) return;
            _styleVersion = HudTheme.Version;
            _title = new GUIStyle(GUI.skin.label) { font = HudTheme.TitleFont, fontSize = HudTheme.TitleSize(17), fontStyle = HudTheme.TitleFontStyle, normal = { textColor = HudTheme.TitleColor } };
            _text = new GUIStyle(GUI.skin.label) { fontSize = HudTheme.Size(15), wordWrap = true, normal = { textColor = HudTheme.Text } };
            _small = new GUIStyle(_text) { fontSize = HudTheme.Size(13), wordWrap = false, normal = { textColor = HudTheme.DimText } };
            _center = new GUIStyle(_text) { fontSize = HudTheme.Size(13), alignment = TextAnchor.MiddleCenter, wordWrap = false, normal = { textColor = Color.white } };
            _techName = new GUIStyle(_text) { fontStyle = FontStyle.Bold, wordWrap = false };
            HudTheme.Fit(_title, _text, _small, _center, _techName);
        }
    }
}
