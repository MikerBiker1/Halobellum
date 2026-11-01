// MatchHud.cs
// The match screens (Bible Section 13), one per phase:
//   Draft: the starting draft cards (faction, sub-faction, 2 free squads) to choose from, and a
//     button to the Sandbox.
//   Deployment: credits and next income, squads bought (x of 2), unlocks (x of 1), both bases, the
//     unit shop (Buy / Unlock), actions for the selected squad (Move, Turn, Sell) and Ready. No timer.
//   Battle: both base health bars and a live army-value comparison (speed controls are in BattleHud).
//   Result: base damage dealt by each side, income, survivors, top damage dealers, what killed what.
//   Match over: the winner and a New match button.
// The look follows the player's faction (HudTheme: UNSC or Covenant style); the draft screen is neutral.
// Drawn with Unity's immediate-mode GUI; scales with the screen height.
// Depends on: Halobellum.Presentation (MatchController, BattleController, UiHitAreas),
//             Halobellum.Economy (Match, PlayerState, RoundSummary), HudScale.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Halobellum.UI
{
    public sealed class MatchHud : MonoBehaviour
    {
        public const string SandboxSceneName = "Sandbox";

        [SerializeField] private MatchController match;
        [SerializeField] private BattleController battle;
        [Tooltip("The look of the draft screen (and the fallback when a faction has no theme).")]
        [SerializeField] private UiThemeDefinition neutralTheme;
        [SerializeField] private Color playerColor = new Color(0.35f, 0.55f, 1f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.4f, 0.32f);

        private GUIStyle _title, _big, _text, _small, _smallCentered, _button, _bigButton, _centered, _header, _barLabel;
        private float _scale;
        private Vector2 _shopScroll;

        private void OnGUI()
        {
            if (match == null || match.Match == null) return;
            HudTheme.Use(match.Match.Phase == MatchPhase.Draft ? neutralTheme : match.PlayerTheme ?? neutralTheme);
            HudTheme.Apply();
            EnsureStyles();
            _scale = HudScale.Apply();

            switch (match.Match.Phase)
            {
                case MatchPhase.Draft: DrawDraft(); break;
                case MatchPhase.Deployment: DrawDeployment(); break;
                case MatchPhase.Battle: DrawBattle(); break;
                case MatchPhase.Result: DrawBattle(); DrawResult(); break;
                case MatchPhase.MatchOver: DrawMatchOver(); break;
            }
        }

        // ------------------------------------------------------------------ draft

        private void DrawDraft()
        {
            Match m = match.Match;
            float width = HudScale.ScaledWidth(_scale), height = HudScale.ScaledHeight(_scale);
            const float cardWidth = 320f, cardHeight = 260f, gap = 24f;
            int count = m.PlayerDraft.Count;
            float total = count * cardWidth + (count - 1) * gap;
            float x = (width - total) * 0.5f, y = height * 0.5f - cardHeight * 0.5f;

            var title = new Rect(width * 0.5f - 400f, y - 90f, 800f, 70f);
            Panel(title);
            GUI.Label(new Rect(title.x, title.y + 6f, title.width, 34f), "Starting draft", _centered);
            GUI.Label(new Rect(title.x, title.y + 38f, title.width, 26f), "Pick a faction and sub-faction. Its 2 squads are free and start unlocked.", _smallCentered);

            for (int i = 0; i < count; i++)
            {
                DraftCard card = m.PlayerDraft[i];
                var rect = new Rect(x + i * (cardWidth + gap), y, cardWidth, cardHeight);
                Panel(rect);
                GUI.Label(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, 34f), card.Faction.Name, _big);
                GUI.Label(new Rect(rect.x + 16f, rect.y + 48f, rect.width - 32f, 24f), "Sub-faction: " + card.SubFaction.Name, _text);
                GUI.Label(new Rect(rect.x + 16f, rect.y + 84f, rect.width - 32f, 24f), "Starting squads:", _text);
                for (int s = 0; s < card.Squads.Count; s++)
                {
                    UnitStats unit = card.Squads[s];
                    GUI.Label(new Rect(rect.x + 28f, rect.y + 110f + s * 44f, rect.width - 44f, 44f),
                        $"{unit.Name}\n{RoleName(unit.Role)}, {Bodies(unit.SquadSize)}, worth {unit.Cost}", _small);
                }
                if (GUI.Button(new Rect(rect.x + 16f, rect.yMax - 52f, rect.width - 32f, 38f), "Choose", _bigButton)) match.ChooseDraft(i);
            }

            // AI difficulty (Milestone 5): only changes how well the AI decides, never what it knows or earns.
            var difficulty = new Rect(width * 0.5f - 300f, y - 150f, 600f, 50f);
            Panel(difficulty);
            GUI.Label(new Rect(difficulty.x + 14f, difficulty.y + 10f, 140f, 30f), "AI difficulty:", _text);
            string[] names = { "Easy", "Normal", "Hard" };
            for (int d = 0; d < names.Length; d++)
            {
                bool selected = (int)match.Difficulty == d;
                Color old = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
                if (GUI.Button(new Rect(difficulty.x + 160f + d * 145f, difficulty.y + 7f, 135f, 36f), selected ? $"[ {names[d]} ]" : names[d], _button))
                    match.SetDifficulty((AiDifficulty)d);
                GUI.backgroundColor = old;
            }

            // The sandbox (Milestone 4): place any two armies and watch them fight.
            var sandbox = new Rect(width * 0.5f - 200f, y + cardHeight + 30f, 400f, 50f);
            Panel(sandbox);
            if (GUI.Button(new Rect(sandbox.x + 8f, sandbox.y + 6f, sandbox.width - 16f, 38f), "Sandbox: test any two armies", _button))
                SceneManager.LoadScene(SandboxSceneName);
        }

        // ------------------------------------------------------------------ deployment

        private void DrawDeployment()
        {
            Match m = match.Match;
            PlayerState player = m.Player;
            float width = HudScale.ScaledWidth(_scale), height = HudScale.ScaledHeight(_scale);

            // Top bar.
            var bar = new Rect(width * 0.5f - 560f, 8f, 1120f, 64f);
            Panel(bar);
            GUI.Label(new Rect(bar.x + 16f, bar.y + 6f, 220f, 26f), HudTheme.Title($"Round {m.Round}: Deployment"), _header);
            GUI.Label(new Rect(bar.x + 16f, bar.y + 32f, 260f, 26f), $"Credits {player.Credits}  (+{m.NextIncome} next round)", _text);
            GUI.Label(new Rect(bar.x + 290f, bar.y + 6f, 220f, 26f), $"Squads bought: {player.SquadsBoughtThisRound} of {m.Rules.NewSquadsPerRound}", _text);
            GUI.Label(new Rect(bar.x + 290f, bar.y + 32f, 220f, 26f), $"Unlocks: {player.UnlocksThisRound} of {m.Rules.UnlocksPerRound}", _text);
            BaseBar(new Rect(bar.x + 520f, bar.y + 10f, 220f, 20f), "Your base", player.BaseHealth, m.Rules.BaseHealth, playerColor);
            BaseBar(new Rect(bar.x + 520f, bar.y + 36f, 220f, 20f), "Enemy base", m.Enemy.BaseHealth, m.Rules.BaseHealth, enemyColor);
            GUI.Label(new Rect(bar.x + 760f, bar.y + 6f, 200f, 26f), $"{player.Faction.Name} vs {m.Enemy.Faction?.Name ?? "?"}", _small);
            GUI.Label(new Rect(bar.x + 760f, bar.y + 32f, 200f, 26f), m.EnemyAi.Label, _small);
            if (GUI.Button(new Rect(bar.xMax - 150f, bar.y + 10f, 136f, 44f), "Ready", _bigButton)) match.Ready();

            if (!string.IsNullOrEmpty(match.Message))
            {
                var message = new Rect(width * 0.5f - 400f, bar.yMax + 6f, 800f, 30f);
                Panel(message);
                GUI.Label(message, match.Message, _smallCentered);
            }

            DrawShop(width, height);
            DrawSelectedSquadActions(width, height);
        }

        private void DrawShop(float width, float height)
        {
            Match m = match.Match;
            PlayerState player = m.Player;
            List<UnitStats> roster = player.Roster;
            const float panelWidth = 420f, rowHeight = 58f;
            float panelHeight = Mathf.Min(height - 200f, 50f + roster.Count * rowHeight);
            var panel = new Rect(width - panelWidth - 10f, 110f, panelWidth, panelHeight);
            Panel(panel);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 28f), HudTheme.Title($"Shop: {player.Faction.Name}"), _header);

            var view = new Rect(panel.x + 6f, panel.y + 40f, panel.width - 12f, panel.height - 46f);
            var content = new Rect(0f, 0f, view.width - 18f, roster.Count * rowHeight);
            _shopScroll = GUI.BeginScrollView(view, _shopScroll, content);
            for (int i = 0; i < roster.Count; i++)
            {
                UnitStats unit = roster[i];
                var row = new Rect(0f, i * rowHeight, content.width, rowHeight - 4f);
                GUI.Box(row, GUIContent.none);
                GUI.Label(new Rect(row.x + 8f, row.y + 4f, row.width - 130f, 24f), unit.Name, _text);
                GUI.Label(new Rect(row.x + 8f, row.y + 28f, row.width - 130f, 22f), $"Tier {unit.Tier}, {RoleName(unit.Role)}, {Bodies(unit.SquadSize)}", _small);

                var button = new Rect(row.xMax - 118f, row.y + 9f, 110f, 36f);
                if (player.Unlocked.Contains(unit))
                {
                    bool canBuy = player.Credits >= unit.Cost && player.SquadsBoughtThisRound < m.Rules.NewSquadsPerRound;
                    GUI.enabled = canBuy;
                    if (GUI.Button(button, $"Buy {unit.Cost}", _button)) match.Buy(unit);
                }
                else
                {
                    bool canUnlock = player.Credits >= unit.UnlockCost && player.UnlocksThisRound < m.Rules.UnlocksPerRound;
                    GUI.enabled = canUnlock;
                    if (GUI.Button(button, unit.UnlockCost > 0 ? $"Unlock {unit.UnlockCost}" : "Unlock (free)", _button)) match.Unlock(unit);
                }
                GUI.enabled = true;
            }
            GUI.EndScrollView();
        }

        private void DrawSelectedSquadActions(float width, float height)
        {
            PlacedSquad squad = match.SelectedPlacedSquad;
            if (squad == null || match.IsPlacing) return;
            bool movable = match.Match.CanMove(squad);
            var panel = new Rect(width * 0.5f - 260f, height - 70f, 520f, 58f);
            Panel(panel);
            GUI.enabled = movable;
            if (GUI.Button(new Rect(panel.x + 10f, panel.y + 10f, 150f, 38f), "Move (M)", _button)) match.BeginMoving(squad);
            if (GUI.Button(new Rect(panel.x + 170f, panel.y + 10f, 150f, 38f), "Turn (R)", _button)) match.ToggleRotation();
            GUI.enabled = true;
            if (GUI.Button(new Rect(panel.x + 330f, panel.y + 10f, 180f, 38f), $"Sell +{match.Match.SellValue(squad)} (Del)", _button)) match.Sell(squad);
        }

        // ------------------------------------------------------------------ battle

        private void DrawBattle()
        {
            Match m = match.Match;
            float width = HudScale.ScaledWidth(_scale);
            var left = new Rect(10f, 8f, 280f, 64f);
            Panel(left);
            BaseBar(new Rect(left.x + 12f, left.y + 10f, 256f, 20f), "Your base", m.Player.BaseHealth, m.Rules.BaseHealth, playerColor);
            GUI.Label(new Rect(left.x + 12f, left.y + 34f, 256f, 24f), $"Army value {battle.AliveValue(Team.A):0}", _small);

            var right = new Rect(width - 290f, 40f, 280f, 64f);
            Panel(right);
            BaseBar(new Rect(right.x + 12f, right.y + 10f, 256f, 20f), "Enemy base", m.Enemy.BaseHealth, m.Rules.BaseHealth, enemyColor);
            GUI.Label(new Rect(right.x + 12f, right.y + 34f, 256f, 24f), $"Army value {battle.AliveValue(Team.B):0}", _small);
        }

        // ------------------------------------------------------------------ result and match over

        private void DrawResult()
        {
            RoundSummary s = match.Match.LastSummary;
            if (s == null) return;
            Rect panel = CenteredPanel(640f, 520f);
            float y = panel.y + 12f;
            GUI.Label(new Rect(panel.x, y, panel.width, 34f), HudTheme.Title($"Round {s.Round} result"), _centered);
            y += 44f;
            y = SummaryLines(s, panel, y);

            string footer = (s.Tie ? "Exact tie: both bases are restored. " : "")
                            + $"Income: +{s.IncomeAwarded} credits for next round.";
            GUI.Label(new Rect(panel.x + 20f, panel.yMax - 96f, panel.width - 40f, 26f), footer, _text);
            if (s.PlayerSquadsReadyToLevel.Count > 0)
            {
                GUI.Label(new Rect(panel.x + 20f, panel.yMax - 122f, panel.width - 40f, 26f),
                    "Ready to level up: " + string.Join(", ", s.PlayerSquadsReadyToLevel.Distinct()) + " (select it next round)", _small);
            }
            if (GUI.Button(new Rect(panel.x + panel.width * 0.5f - 110f, panel.yMax - 58f, 220f, 44f), "Continue", _bigButton)) match.Continue();
        }

        private void DrawMatchOver()
        {
            Match m = match.Match;
            RoundSummary s = m.LastSummary;
            Rect panel = CenteredPanel(640f, 560f);
            float y = panel.y + 12f;
            bool won = m.Winner == Team.A;
            GUI.Label(new Rect(panel.x, y, panel.width, 40f), HudTheme.Title(won ? "Victory!" : "Defeat"), _title);
            y += 52f;
            GUI.Label(new Rect(panel.x + 20f, y, panel.width - 40f, 26f),
                won ? $"The enemy base fell in round {m.Round}." : $"Your base fell in round {m.Round}.", _text);
            y += 34f;
            if (s != null) SummaryLines(s, panel, y);
            if (GUI.Button(new Rect(panel.x + panel.width * 0.5f - 110f, panel.yMax - 58f, 220f, 44f), "New match", _bigButton)) match.NewMatch();
        }

        /// <summary>Base damage, bases, survivors, top damage dealers and what killed what. Returns the next y.</summary>
        private float SummaryLines(RoundSummary s, Rect panel, float y)
        {
            float x = panel.x + 20f, w = panel.width - 40f;
            GUI.Label(new Rect(x, y, w, 26f), $"Base damage: you dealt {s.BaseDamageByA}, the enemy dealt {s.BaseDamageByB}", _text); y += 28f;
            GUI.Label(new Rect(x, y, w, 26f), $"Bases: yours {s.BaseHealthA}, enemy {s.BaseHealthB}", _text); y += 28f;
            GUI.Label(new Rect(x, y, w, 26f),
                $"Survivors: yours {s.Battle.TeamA.Survivors} of {s.Battle.TeamA.Deployed}, enemy {s.Battle.TeamB.Survivors} of {s.Battle.TeamB.Deployed}" +
                (s.Battle.TimedOut ? "  (time limit)" : ""), _text);
            y += 38f;

            GUI.Label(new Rect(x, y, w, 24f), "Top damage dealers", _text); y += 26f;
            foreach (UnitTypeResult t in TopDealers(s.Battle, 4))
            {
                GUI.Label(new Rect(x + 12f, y, w - 12f, 22f), $"{(t.Team == Team.A ? "Your" : "Enemy")} {t.UnitName}: {t.DamageDealt:0} damage, {t.Kills} kills", _small);
                y += 22f;
            }
            y += 10f;
            GUI.Label(new Rect(x, y, w, 24f), "What killed what", _text); y += 26f;
            foreach (KillCount k in s.Kills.Take(5))
            {
                GUI.Label(new Rect(x + 12f, y, w - 12f, 22f), $"{(k.KillerTeam == Team.A ? "Your" : "Enemy")} {k.KillerType} killed {k.Kills} {k.VictimType}", _small);
                y += 22f;
            }
            if (s.Kills.Count == 0) { GUI.Label(new Rect(x + 12f, y, w - 12f, 22f), "Nobody was killed.", _small); y += 22f; }
            return y;
        }

        private static IEnumerable<UnitTypeResult> TopDealers(BattleResult result, int count)
        {
            return result.TeamA.UnitTypes.Concat(result.TeamB.UnitTypes)
                .Where(t => t.DamageDealt > 0f)
                .OrderByDescending(t => t.DamageDealt)
                .Take(count);
        }

        // ------------------------------------------------------------------ helpers

        private Rect CenteredPanel(float w, float h)
        {
            float width = HudScale.ScaledWidth(_scale), height = HudScale.ScaledHeight(_scale);
            var rect = new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);
            Panel(rect);
            return rect;
        }

        /// <summary>A dark box that also blocks clicks from reaching the battlefield.</summary>
        private void Panel(Rect rect)
        {
            UiHitAreas.Add(HudScale.ToScreen(rect, _scale));
            HudTheme.PanelBox(rect);
        }

        private void BaseBar(Rect rect, string label, int health, int maxHealth, Color color)
        {
            float fraction = maxHealth > 0 ? Mathf.Clamp01(health / (float)maxHealth) : 0f;
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fraction, rect.height), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(rect, $"{label}: {Mathf.Max(0, health)} / {maxHealth}", _barLabel);
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

        private int _styleVersion = -1;

        private void EnsureStyles()
        {
            if (_text != null && _styleVersion == HudTheme.Version) return;
            _styleVersion = HudTheme.Version;
            _text = new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = HudTheme.Text } };
            _small = new GUIStyle(_text) { fontSize = 15, alignment = TextAnchor.MiddleLeft, normal = { textColor = HudTheme.DimText } };
            _big = new GUIStyle(_text) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = HudTheme.TitleColor } };
            _centered = new GUIStyle(_text) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = HudTheme.TitleColor } };
            _title = new GUIStyle(_centered) { fontSize = 34 };
            _header = new GUIStyle(_text) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = HudTheme.TitleColor } };
            _barLabel = new GUIStyle(_text) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            _bigButton = new GUIStyle(GUI.skin.button) { fontSize = 19, fontStyle = FontStyle.Bold };
            _smallCentered = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
        }
    }
}
