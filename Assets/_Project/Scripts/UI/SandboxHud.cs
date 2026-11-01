// SandboxHud.cs
// The sandbox screen (Milestone 4): a side switch (blue / red), the units of every faction to place
// on that side, both armies' totals, actions for the selected squad (Move, Turn, Remove), and Run /
// Reset, Clear side, Copy side, the battle seed and a button back to the match. Speed controls and
// the battle result line come from BattleHud; the squad stats window from SquadStatsWindow.
// Placeholder UI drawn with Unity's immediate-mode GUI; scales with the screen height.
// Depends on: Halobellum.Presentation (SandboxController, BattleController, UiHitAreas), HudScale,
//             Halobellum.Core, UnityEngine (SceneManagement).

using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Halobellum.UI
{
    public sealed class SandboxHud : MonoBehaviour
    {
        public const string MatchSceneName = "Match";

        [SerializeField] private SandboxController sandbox;
        [SerializeField] private BattleController battle;
        [SerializeField] private Color blueColor = new Color(0.35f, 0.55f, 1f);
        [SerializeField] private Color redColor = new Color(1f, 0.4f, 0.32f);

        private GUIStyle _text, _small, _smallCentered, _button, _bigButton, _header;
        private float _scale;
        private Vector2 _scroll;

        private void OnGUI()
        {
            if (sandbox == null) return;
            EnsureStyles();
            _scale = HudScale.Apply();
            float width = HudScale.ScaledWidth(_scale), height = HudScale.ScaledHeight(_scale);

            DrawTopBar(width);
            if (!sandbox.IsBattle)
            {
                DrawUnitList(width, height);
                DrawSelectedSquadActions(width, height);
            }
            if (!string.IsNullOrEmpty(sandbox.Message))
            {
                var message = new Rect(width * 0.5f - 400f, 164f, 800f, 30f);
                Panel(message);
                GUI.Label(message, sandbox.Message, _smallCentered);
            }
        }

        private void DrawTopBar(float width)
        {
            // Below BattleHud's speed controls and result line (top centre).
            var bar = new Rect(width * 0.5f - 560f, 92f, 1120f, 64f);
            Panel(bar);
            GUI.Label(new Rect(bar.x + 16f, bar.y + 6f, 160f, 26f), "Sandbox", _header);
            GUI.Label(new Rect(bar.x + 16f, bar.y + 34f, 160f, 24f), $"Seed {sandbox.Seed}", _small);
            if (!sandbox.IsBattle)
            {
                if (GUI.Button(new Rect(bar.x + 110f, bar.y + 34f, 30f, 24f), "-", _button)) sandbox.Seed--;
                if (GUI.Button(new Rect(bar.x + 144f, bar.y + 34f, 30f, 24f), "+", _button)) sandbox.Seed++;
            }

            ArmyLabel(new Rect(bar.x + 190f, bar.y + 6f, 300f, 26f), Team.A, blueColor);
            ArmyLabel(new Rect(bar.x + 190f, bar.y + 34f, 300f, 26f), Team.B, redColor);

            if (sandbox.IsBattle)
            {
                if (battle != null && battle.Simulation != null)
                {
                    GUI.Label(new Rect(bar.x + 500f, bar.y + 6f, 300f, 52f),
                        $"Alive: blue {battle.CountAlive(Team.A)}, red {battle.CountAlive(Team.B)}\nValue: blue {battle.AliveValue(Team.A):0}, red {battle.AliveValue(Team.B):0}", _small);
                }
                if (GUI.Button(new Rect(bar.xMax - 300f, bar.y + 10f, 140f, 44f), "Reset", _bigButton)) sandbox.ResetBattle();
            }
            else
            {
                if (GUI.Button(new Rect(bar.x + 500f, bar.y + 10f, 130f, 44f), $"Clear {SandboxController.SideName(sandbox.EditingTeam)}", _button)) sandbox.Clear(sandbox.EditingTeam);
                string other = SandboxController.SideName(sandbox.EditingTeam.Opponent());
                if (GUI.Button(new Rect(bar.x + 640f, bar.y + 10f, 170f, 44f), $"Copy to {other}", _button)) sandbox.CopyToOtherSide(sandbox.EditingTeam);
                if (GUI.Button(new Rect(bar.xMax - 300f, bar.y + 10f, 140f, 44f), "Run battle", _bigButton)) sandbox.Run();
            }
            if (GUI.Button(new Rect(bar.xMax - 150f, bar.y + 10f, 136f, 44f), "Back to match", _button)) SceneManager.LoadScene(MatchSceneName);
        }

        private void ArmyLabel(Rect rect, Team team, Color color)
        {
            PlayerState army = sandbox.Army(team);
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y + 6f, 14f, 14f), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(rect.x + 22f, rect.y, rect.width - 22f, rect.height),
                $"{(team == Team.A ? "Blue" : "Red")}: {army.Squads.Count} squads, {sandbox.ArmyCost(team)} credits", _small);
        }

        private void DrawUnitList(float width, float height)
        {
            const float panelWidth = 400f, rowHeight = 50f, headerHeight = 28f;
            // Right side, like the match shop (the squad stats window opens bottom-left).
            var panel = new Rect(width - panelWidth - 10f, 204f, panelWidth, height - 294f);
            Panel(panel);

            // Side switch: which zone new squads go to.
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, 120f, 30f), "Place for:", _text);
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = sandbox.EditingTeam == Team.A ? blueColor : Color.gray;
            if (GUI.Button(new Rect(panel.x + 130f, panel.y + 6f, 125f, 34f), "Blue", _button)) SetSide(Team.A);
            GUI.backgroundColor = sandbox.EditingTeam == Team.B ? redColor : Color.gray;
            if (GUI.Button(new Rect(panel.x + 263f, panel.y + 6f, 125f, 34f), "Red", _button)) SetSide(Team.B);
            GUI.backgroundColor = old;

            var view = new Rect(panel.x + 6f, panel.y + 48f, panel.width - 12f, panel.height - 54f);
            float contentHeight = 0f;
            string lastFaction = null;
            foreach (SandboxController.SandboxUnit unit in sandbox.Units)
            {
                if (unit.Faction != lastFaction) { contentHeight += headerHeight; lastFaction = unit.Faction; }
                contentHeight += rowHeight;
            }
            var content = new Rect(0f, 0f, view.width - 18f, contentHeight);
            _scroll = GUI.BeginScrollView(view, _scroll, content);
            float y = 0f;
            lastFaction = null;
            foreach (SandboxController.SandboxUnit unit in sandbox.Units)
            {
                if (unit.Faction != lastFaction)
                {
                    GUI.Label(new Rect(4f, y, content.width, headerHeight), unit.Faction, _header);
                    y += headerHeight;
                    lastFaction = unit.Faction;
                }
                UnitStats stats = unit.Stats;
                var row = new Rect(0f, y, content.width, rowHeight - 4f);
                bool placing = sandbox.PlacingType == stats;
                GUI.Box(row, GUIContent.none);
                if (placing) GUI.Box(row, GUIContent.none);
                GUI.Label(new Rect(row.x + 8f, row.y + 2f, row.width - 110f, 22f), stats.Name, _text);
                GUI.Label(new Rect(row.x + 8f, row.y + 24f, row.width - 110f, 20f), $"{RoleName(stats.Role)}, {Bodies(stats.SquadSize)}, {stats.Cost}", _small);
                if (GUI.Button(new Rect(row.xMax - 92f, row.y + 7f, 86f, 32f), placing ? "Placing" : "Place", _button))
                {
                    if (placing) sandbox.CancelPlacement();
                    else sandbox.BeginPlacing(stats);
                }
                y += rowHeight;
            }
            GUI.EndScrollView();
        }

        private void SetSide(Team team)
        {
            UnitStats placing = sandbox.PlacingType;
            sandbox.EditingTeam = team;
            if (placing != null) sandbox.BeginPlacing(placing); // Keep placing the same unit on the new side.
        }

        private void DrawSelectedSquadActions(float width, float height)
        {
            PlacedSquad squad = sandbox.SelectedPlacedSquad;
            if (squad == null || sandbox.IsPlacing) return;
            var panel = new Rect(width * 0.5f - 260f, height - 70f, 520f, 58f);
            Panel(panel);
            if (GUI.Button(new Rect(panel.x + 10f, panel.y + 10f, 160f, 38f), "Move (M)", _button)) sandbox.BeginMoving(squad);
            if (GUI.Button(new Rect(panel.x + 180f, panel.y + 10f, 160f, 38f), "Turn (R)", _button)) sandbox.ToggleRotation();
            if (GUI.Button(new Rect(panel.x + 350f, panel.y + 10f, 160f, 38f), "Remove (Del)", _button)) sandbox.Remove(squad);
        }

        private void Panel(Rect rect)
        {
            UiHitAreas.Add(HudScale.ToScreen(rect, _scale));
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);
        }

        private static string Bodies(int count) => count == 1 ? "1 body" : $"{count} bodies";

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
            if (_text != null) return;
            _text = new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = Color.white } };
            _small = new GUIStyle(_text) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            _smallCentered = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            _header = new GUIStyle(_text) { fontSize = 19, fontStyle = FontStyle.Bold };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            _bigButton = new GUIStyle(GUI.skin.button) { fontSize = 19, fontStyle = FontStyle.Bold };
        }
    }
}
