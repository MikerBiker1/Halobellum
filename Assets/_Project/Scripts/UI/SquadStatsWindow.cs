// SquadStatsWindow.cs
// The bottom-left stats window (Bible Section 13). It appears whenever a squad is selected and
// shows its stats from the unit data: squad size (and bodies still alive), cost, tier, role,
// health, armor type, shield, move speed, collision size, target filter, and for each weapon its
// damage, rate of fire, range, splash radius and damage type, plus any defense arc.
// Level, XP, the level-up button and tech upgrades are added with leveling in Milestone 6.
// Placeholder UI drawn with Unity's immediate-mode GUI.
// Depends on: Halobellum.Presentation (BattleController, SquadSelection, UiHitAreas), Halobellum.Core.

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
        [SerializeField] private float windowWidth = 380f;

        private SquadSelection _selection;
        private GUIStyle _title, _text;
        private readonly StringBuilder _builder = new StringBuilder();

        private void Start()
        {
            if (battle != null) _selection = battle.GetComponent<SquadSelection>();
        }

        private void OnGUI()
        {
            if (battle == null || battle.Simulation == null || _selection == null || _selection.SelectedSquadId < 0) return;
            EnsureStyles();
            float scale = HudScale.Apply();

            SimUnit sample = FindAnyOfSquad(_selection.SelectedSquadId, out int alive, out float totalHealth);
            if (sample == null) return;
            UnitStats s = sample.Stats;

            string body = BuildText(s, alive, totalHealth);
            float height = 44f + _text.CalcHeight(new GUIContent(body), windowWidth - 24f);
            var window = new Rect(10f, HudScale.ScaledHeight(scale) - height - 10f, windowWidth, height);
            UiHitAreas.Add(HudScale.ToScreen(window, scale));

            GUI.Box(window, GUIContent.none);
            GUI.Box(window, GUIContent.none); // Drawn twice for a darker background.
            GUI.Label(new Rect(window.x + 12f, window.y + 8f, windowWidth - 60f, 26f), $"{s.Name}  (Team {sample.Team})", _title);
            if (GUI.Button(new Rect(window.xMax - 34f, window.y + 8f, 24f, 22f), "x")) _selection.Select(-1);
            GUI.Label(new Rect(window.x + 12f, window.y + 38f, windowWidth - 24f, height - 44f), body, _text);
        }

        private string BuildText(UnitStats s, int alive, float totalHealth)
        {
            StringBuilder b = _builder.Clear();
            b.AppendLine($"Role: {RoleName(s.Role)}    Tier {s.Tier}    Cost {s.Cost}");
            b.AppendLine($"Squad size: {s.SquadSize}  ({alive} alive, {totalHealth:0} health left)");
            b.AppendLine($"Health: {s.MaxHealth:0} per body    Armor: {s.ArmorType}");
            b.AppendLine(s.HasShield ? $"Shield: {s.MaxShield:0} (regen {s.ShieldRegenPerSecond:0.#}/s)" : "Shield: none");
            b.AppendLine($"Move speed: {s.MoveSpeed:0.#} m/s    Collision size: {s.CollisionRadius * 2f:0.#} m");
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

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, normal = { textColor = new Color(0.92f, 0.92f, 0.92f) } };
        }
    }
}
