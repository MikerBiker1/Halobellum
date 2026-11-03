// SquadStatsWindow.cs
// The bottom-left stats window (Bible Section 13). It appears whenever a squad is selected and
// shows its stats from the unit data: squad size (and bodies still alive), cost, tier, role,
// health, armor type, shield, move speed, collision size, target filter, and for each weapon its
// damage, rate of fire, range, splash radius and damage type, plus any defense arc.
// Below the stats it shows the squad's level, XP bar, level-up button with its price and its tech
// upgrades with Buy buttons (Milestone 6), from the scene's ISquadProgression (the Match or the
// Sandbox); the opponent's squads only show their level. Beside Level up, a button that levels every
// ready squad of the type, when there are 2 or more. The two share the row with the level line, and
// go on a row of their own if a theme's fonts leave no room for that.
// The unit's picture (UnitIcons) is at the top. Placeholder UI drawn with Unity's immediate-mode GUI.
// Depends on: Halobellum.Presentation (BattleController, SquadSelection, ISquadProgression, UiHitAreas), Halobellum.Core, UnitIcons.

using System.Collections.Generic;
using System.Text;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Presentation;
using UnityEngine;

namespace Halobellum.UI
{
    public sealed class SquadStatsWindow : MonoBehaviour
    {
        private const float ScreenLeft = 10f, DefaultWidth = 440f;

        [SerializeField] private BattleController battle;
        [SerializeField] private float windowWidth = DefaultWidth;

        private SquadSelection _selection;
        private ISquadProgression _progression;
        private GUIStyle _title, _text, _small, _center, _techName;
        private readonly StringBuilder _builder = new StringBuilder();

        // OnGUI runs at least twice a frame, and what it shows here means a lot of new strings and a new view from the Match or Sandbox.
        // So all of that is kept, and worked out again a few times a second, or at once if something a click could have changed.
        private const float RefreshSeconds = 0.2f;
        private float _nextRefresh;
        private int _shownSquad = -1, _shownStyles = -1;
        private UnitStats _shownStats;
        private BattleSimulation _shownBattle;
        private string _body, _side, _levelLine, _xpLine;
        private float _textHeight;
        private SquadProgressView _progress;
        private readonly GUIContent _measure = new GUIContent();
        private readonly List<string> _techNames = new List<string>(), _techEffects = new List<string>();
        private Rect _window;
        private int _windowFrame = -1;
        private bool _levelButtonsOwnRow;
        private float _levelUpWidth, _levelAllWidth, _levelLineWidth;

        /// <summary>For a test tool: where the level line and the two buttons were last drawn, in HUD units. Width 0 when not drawn.</summary>
        public Rect LevelLineShown { get; private set; }
        public Rect LevelUpShown { get; private set; }
        public Rect LevelAllShown { get; private set; }
        /// <summary>For a test tool: the two buttons are on a row of their own.</summary>
        public bool LevelButtonsOwnRow => _levelButtonsOwnRow;

        /// <summary>
        /// The left edge of the Move / Turn / Sell panel, which comes with this window: centred at the bottom of the screen, or right of this window where it would run under it.
        /// </summary>
        public static float ActionsPanelLeft(float screenWidth, float panelWidth) => Mathf.Max((screenWidth - panelWidth) * 0.5f, ScreenLeft + DefaultWidth + 6f);

        /// <summary>Where the window is, in HUD units, if it is showing (the Sandbox's Items panel keeps clear of it).</summary>
        public bool TryGetWindow(out Rect window)
        {
            window = _window;
            return _windowFrame >= 0 && Time.frameCount - _windowFrame <= 1;
        }

        private void Awake() => useGUILayout = false; // Nothing here uses GUILayout. With the layout pass on, Unity makes new layout groups for this script every frame, which is garbage at every frame rate.

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

            if (Time.unscaledTime >= _nextRefresh || _selection.SelectedSquadId != _shownSquad || s != _shownStats || _shownBattle != battle.Simulation || _shownStyles != _styleVersion)
                Refresh(sample, alive, totalHealth);
            string body = _body;
            float textHeight = _textHeight;
            SquadProgressView progress = _progress;
            float height = HeaderHeight + textHeight + ProgressHeight(progress) + (progress != null && _levelButtonsOwnRow ? LevelRowHeight : 0f);
            var window = new Rect(ScreenLeft, HudScale.ScaledHeight(scale) - height - 10f, windowWidth, height);
            UiHitAreas.Add(HudScale.ToScreen(window, scale));
            _window = window;
            _windowFrame = Time.frameCount;

            HudTheme.PanelBox(window);
            CardScreens.DrawUnitPicture(new Rect(window.x + 10f, window.y + 6f, PictureSize, PictureSize), s, null, HudTheme.Accent);
            GUI.Label(new Rect(window.x + PictureSize + 18f, window.y + 6f, windowWidth - PictureSize - 56f, 30f), s.Name, _title);
            GUI.Label(new Rect(window.x + PictureSize + 18f, window.y + 34f, windowWidth - PictureSize - 56f, 24f), _side, _small);
            if (GUI.Button(new Rect(window.xMax - 34f, window.y + 8f, 24f, 22f), "x")) _selection.Select(-1);
            GUI.Label(new Rect(window.x + TextInset, window.y + HeaderHeight - 6f, windowWidth - 2f * TextInset, textHeight), body, _text);
            if (progress != null) DrawProgress(progress, window.x + TextInset, window.y + HeaderHeight - 2f + textHeight, windowWidth - 2f * TextInset);
            if (Event.current.type == EventType.MouseUp || Event.current.type == EventType.Used) _nextRefresh = 0f;
        }

        private void Refresh(SimUnit sample, int alive, float totalHealth)
        {
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            _shownSquad = _selection.SelectedSquadId;
            _shownStats = sample.Stats;
            _shownBattle = battle.Simulation;
            _shownStyles = _styleVersion;
            _body = BuildText(sample.Stats, alive, totalHealth);
            _measure.text = _body;
            _textHeight = _text.CalcHeight(_measure, windowWidth - 2f * TextInset);
            _progress = _progression?.DescribeProgress(_shownSquad);
            _side = _progress?.SideLabel ?? $"Team {sample.Team}";
            _techNames.Clear();
            _techEffects.Clear();
            _levelButtonsOwnRow = false;
            if (_progress == null) return;
            _levelLine = $"Level {_progress.Level} of {_progress.MaxLevel}";
            _xpLine = $"XP {_progress.Xp:0} / {_progress.XpNeeded:0}";
            foreach (TechLine line in _progress.Techs)
            {
                _techNames.Add(line.Owned ? $"{line.Tech.Name}  (researched)" : line.Tech.Name);
                _techEffects.Add(line.Tech.EffectsText());
            }
            MeasureLevelRow(_progress, windowWidth - 2f * TextInset);
        }

        /// <summary>The widths of the level line and buttons, and whether they all fit on one row. Sizes differ from theme to theme, so this measures them.</summary>
        private void MeasureLevelRow(SquadProgressView p, float width)
        {
            _levelUpWidth = LevelUpWidth;
            _levelAllWidth = 0f;
            _measure.text = _levelLine;
            _levelLineWidth = _title.CalcSize(_measure).x + 4f;
            if (p.LevelAllLabel == null) return;

            _measure.text = p.LevelAllLabel;
            _levelAllWidth = GUI.skin.button.CalcSize(_measure).x + ButtonRoom;
            _levelUpWidth = 0f;
            if (p.LevelUpLabel != null)
            {
                _measure.text = p.LevelUpLabel;
                _levelUpWidth = GUI.skin.button.CalcSize(_measure).x + ButtonRoom;
            }
            float taken = _levelLineWidth + RowGap + _levelAllWidth + (_levelUpWidth > 0f ? RowGap + _levelUpWidth : 0f);
            _levelButtonsOwnRow = taken > width;
            if (!_levelButtonsOwnRow) return;
            float each = _levelUpWidth > 0f ? (width - RowGap) * 0.5f : width;
            _levelAllWidth = each;
            if (_levelUpWidth > 0f) _levelUpWidth = each;
        }

        private const float LevelUpWidth = 150f, LevelRowHeight = 30f, RowGap = 6f, ButtonRoom = 12f;
        private const float LineHeight = 24f, TechRowHeight = 44f, PictureSize = 56f, HeaderHeight = 72f;
        /// <summary>Room under the last line of the progress part, and at the sides of the text, so that the window's frame doesn't cover them.</summary>
        private const float BottomPadding = 14f, TextInset = 18f;

        private static float ProgressHeight(SquadProgressView p)
        {
            if (p == null) return 0f;
            float height = 10f + 30f + BottomPadding; // Gap, level line (with buttons).
            if (p.ShowXp) height += 22f;
            if (!string.IsNullOrEmpty(p.Note)) height += LineHeight;
            if (!string.IsNullOrEmpty(p.ItemText)) height += LineHeight * 2f;
            if (p.Techs.Count > 0) height += LineHeight + p.Techs.Count * TechRowHeight;
            return height;
        }

        /// <summary>Level, XP bar, level-up button and tech upgrades.</summary>
        private void DrawProgress(SquadProgressView p, float x, float y, float width)
        {
            y += 10f;
            var levelLine = new Rect(x, y, p.LevelAllLabel != null ? _levelLineWidth : 170f, LineHeight + 6f);
            GUI.Label(levelLine, _levelLine, _title);
            LevelLineShown = levelLine;
            if (_levelButtonsOwnRow) y += LevelRowHeight;
            float buttonX = x + width;
            LevelAllShown = default;
            LevelUpShown = default;
            if (p.LevelAllLabel != null)
            {
                buttonX -= _levelAllWidth;
                var levelAll = new Rect(buttonX, y, _levelAllWidth, 28f);
                buttonX -= RowGap;
                GUI.enabled = p.LevelAllEnabled;
                if (GUI.Button(levelAll, p.LevelAllLabel) && p.OnLevelAll != null) p.OnLevelAll();
                GUI.enabled = true;
                LevelAllShown = levelAll;
            }
            if (p.LevelUpLabel != null)
            {
                buttonX -= _levelUpWidth;
                var levelUp = new Rect(buttonX, y, _levelUpWidth, 28f);
                GUI.enabled = p.LevelUpEnabled;
                if (GUI.Button(levelUp, p.LevelUpLabel) && p.OnLevelUp != null) p.OnLevelUp();
                GUI.enabled = true;
                LevelUpShown = levelUp;
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
                GUI.Label(bar, _xpLine, _center);
                y += 22f;
            }
            if (!string.IsNullOrEmpty(p.Note))
            {
                GUI.Label(new Rect(x, y, width, LineHeight), p.Note, _text);
                y += LineHeight;
            }
            if (!string.IsNullOrEmpty(p.ItemText))
            {
                bool removable = p.OnRemoveItem != null;
                GUI.Label(new Rect(x, y, width - (removable ? 100f : 0f), LineHeight * 2f), p.ItemText, _text);
                if (removable && GUI.Button(new Rect(x + width - 90f, y + 6f, 90f, 30f), "Remove")) p.OnRemoveItem();
                y += LineHeight * 2f;
            }
            if (p.Techs.Count == 0) return;

            GUI.Label(new Rect(x, y, width, LineHeight), "Tech upgrades (every squad of this type):", _text);
            y += LineHeight;
            for (int i = 0; i < p.Techs.Count; i++)
            {
                TechLine line = p.Techs[i];
                GUI.Label(new Rect(x, y, width - 110f, 26f), _techNames[i], _techName); // Taller than its row's line: tails of letters (g, p) would be cut.
                GUI.Label(new Rect(x, y + 20f, width - 110f, 22f), _techEffects[i], _small);
                if (line.ButtonLabel != null)
                {
                    GUI.enabled = line.ButtonEnabled;
                    if (GUI.Button(new Rect(x + width - 100f, y + 6f, 100f, 30f), line.ButtonLabel) && line.OnButton != null) line.OnButton();
                    GUI.enabled = true;
                }
                y += TechRowHeight;
            }
        }

        /// <summary>The Flood's special rules (D38), and whether Infection Forms can infect this unit.</summary>
        private static void AppendFloodLines(StringBuilder b, UnitStats s)
        {
            if (s.FormsPerBody > 1) b.AppendLine("Area damage hits every form in a pack");
            if (s.Infects) b.AppendLine("Infects: Unarmored enemies killed near it rise as Flood");
            if (s.LeapRange > 0f) b.AppendLine($"Leaps at enemies within {s.LeapRange:0} m, and over anything in its way");
            if (s.SelfDestructs) b.AppendLine("Explodes when it reaches an enemy");
            if (s.SeeksDensestCluster) b.AppendLine("Dives at the thickest group of enemies");
            if (s.DeathBurst != null)
            {
                string released = "";
                foreach (SpawnEntry spawn in s.DeathBurst.Spawns) released += $", releases {spawn.Unit.Name}";
                b.AppendLine($"On death: {s.DeathBurst.Damage:0} damage in {s.DeathBurst.Radius:0.#} m{released}");
            }
            if (s.SpawnedUnit != null) b.AppendLine($"Releases {s.SpawnedUnit.Name} every {s.SpawnIntervalSeconds:0.#} s");
            if (s.AuraRadius > 0f)
            {
                string aura = s.AuraSpeedMultiplier > 1f && s.AuraHealPerSecondShare > 0f ? "faster, and they heal" : s.AuraSpeedMultiplier > 1f ? "faster" : "they heal";
                b.AppendLine($"Friends within {s.AuraRadius:0} m: {aura}");
            }
            if (s.LifestealShare > 0f) b.AppendLine($"{s.LifestealShare * 100f:0}% of the damage it deals comes back as health");
            if (!s.IsFlood) b.AppendLine(s.ArmorType == ArmorType.Unarmored && !s.ImmuneToInfection ? "Can be infected once its shield is down" : "Infection Forms cannot infect it");
        }

        private string BuildText(UnitStats s, int alive, float totalHealth)
        {
            StringBuilder b = _builder.Clear();
            b.AppendLine($"Role: {RoleName(s.Role)}    Tier {s.Tier}    Cost {s.Cost}{(s.Level > 1 ? $"    Level {s.Level}" : "")}");
            string packs = s.FormsPerBody > 1 ? $" in {s.SquadSize} packs" : "";
            b.AppendLine($"Squad size: {s.FormsInSquad}{packs}  ({alive} alive, {totalHealth:0} health left)");
            b.AppendLine($"Health: {s.MaxHealth:0} per {(s.FormsPerBody > 1 ? "pack" : "body")}    Armor: {s.ArmorType}");
            b.AppendLine(s.HasShield ? $"Shield: {s.MaxShield:0} (regen {s.ShieldRegenPerSecond:0.#}/s)" : "Shield: none");
            b.AppendLine($"Move speed: {s.MoveSpeed:0.#} m/s    Collision size: {s.CollisionRadius * 2f:0.#} m");
            b.AppendLine(s.TurnTime180 > 0f ? $"Turn time: {s.TurnTime180:0.##} s for 180 degrees" : "Turn time: instant");
            b.AppendLine($"Targets: {s.TargetFilter}{(s.IsAir ? "    (air unit)" : "")}");
            AppendFloodLines(b, s);
            for (int i = 0; i < s.Weapons.Length; i++)
            {
                WeaponStats w = s.Weapons[i];
                if (s.SelfDestructs && w.DamagePerShot <= 0f) continue; // Its damage is the death burst, listed above.
                string main = s.Weapons.Length > 1 && i == 0 ? " (main)" : "";
                b.AppendLine();
                b.AppendLine($"{w.Name}{main}");
                if (w.IsBeam)
                {
                    b.AppendLine($"  Beam {w.DamagePerShot * w.ShotsPerSecond:0.#} damage/s {w.DamageType}{(w.IsAntiArmor ? ", anti-armor" : "")}");
                    b.AppendLine($"  Charges {w.BeamChargeSeconds:0.#} s, fires {w.BeamSeconds:0.#} s, rests {w.BeamRestSeconds:0.#} s");
                }
                else
                {
                    b.AppendLine($"  Damage {w.DamagePerShot:0.#} {w.DamageType}{(w.IsAntiArmor ? ", anti-armor" : "")}    Rate {w.ShotsPerSecond:0.##}/s");
                }
                if (w.Lobbed) b.AppendLine("  Lobbed: aims ahead of its target, can miss");
                if (w.SeedSeconds > 0f) b.AppendLine($"  Seeds its target for {w.SeedSeconds:0.#} s: a host that dies rises as Flood");
                if (w.HitSlowSeconds > 0f) b.AppendLine($"  A hit slows its target {w.HitSlowShare * 100f:0}% for {w.HitSlowSeconds:0.#} s (not Titans)");
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
            IReadOnlyList<SimUnit> units = battle.Simulation.Units; // By index: this runs every frame, and foreach here would box an enumerator.
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
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
