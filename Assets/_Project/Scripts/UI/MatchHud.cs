// MatchHud.cs
// The match screens (Bible Section 13), one per phase:
//   Draft: the starting draft cards (faction, sub-faction, 2 free squads) to choose from, and a
//     button to the Sandbox. Up to 5 cards fit (CardLayout); each is drawn by CardScreens.
//   Deployment: credits and next income, squads bought (x of 2), unlocks (x of 1), both bases, the
//     unit shop (Buy / Unlock), actions for the selected squad (Move, Turn, Sell) and Ready. No timer.
//   Cards (from round 2, Milestone 7): 3 cards with their prices (spells, items, economy, buffs,
//     training, or reinforcements every other round from round 4), and Skip with its bonus. Each card has
//     its kind's border and a big picture (CardScreens, CardArt, UnitIcons).
//     During deployment a spell bar (left) lists owned spells: Place / Move, take back, or how many
//     rounds until recharged. Under it the Inventory holds items: drag one, and an empty hexagon
//     appears over every squad without an item; drop it on one (squads with an item show it; see
//     ItemHud). Intensive Training is
//     an icon at the top left: select a squad, click it, confirm (decision D33).
//     Each tower (D55) has a small button above it, and clicking it or the tower opens its window at the bottom
//     left: its level, what its fall does, and what it sells.
//   Battle: both base health bars and a live army-value comparison (speed controls are in BattleHud), and how long
//     a fallen tower's shockwave still weakens each army.
//   Result: base damage dealt by each side, income, survivors, top damage dealers, what killed what.
//   Match over: the winner, a New match button and a Main Menu button.
// The look follows the player's faction (HudTheme: UNSC or Covenant style); the draft screen is neutral.
// Drawn with Unity's immediate-mode GUI; scales with the screen height.
// Depends on: Halobellum.Presentation (MatchController, BattleController, UiHitAreas),
//             Halobellum.Economy (Match, PlayerState, RoundSummary), HudScale, CardLayout, CardScreens, UnitIcons.

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

        private GUIStyle _title, _big, _text, _small, _smallCentered, _button, _bigButton, _centered, _header, _barLabel, _wrap, _wrapDim, _shopLine;
        private float _scale;
        private Vector2 _shopScroll;
        private readonly List<UnitStats> _shopRows = new List<UnitStats>();
        private readonly HudLine _yourBaseLine = new HudLine(), _enemyBaseLine = new HudLine(), _yourValueLine = new HudLine(), _enemyValueLine = new HudLine();
        private readonly HudLine _yourShockwaveLine = new HudLine(), _enemyShockwaveLine = new HudLine();
        private SquadSelection _selection;
        private TowerPanel _towerPanel;
        private float _towerPanelRefresh;
        private readonly List<Rect> _panelsDrawn = new List<Rect>();
        private SquadStatsWindow _squadWindow;
        private Rect _towerWindow;

        private void Awake() => useGUILayout = false; // Nothing here uses GUILayout. With the layout pass on, Unity makes new layout groups for this script every frame, which is garbage at every frame rate.

        private void OnGUI()
        {
            if (match == null || match.Match == null) return;
            HudTheme.Use(match.Match.Phase == MatchPhase.Draft ? neutralTheme : match.PlayerTheme ?? neutralTheme);
            HudTheme.Apply();
            EnsureStyles();
            _scale = HudScale.Apply();
            _panelsDrawn.Clear();

            switch (match.Match.Phase)
            {
                case MatchPhase.Draft: DrawDraft(); break;
                case MatchPhase.Deployment: DrawDeployment(); break;
                case MatchPhase.Cards: DrawCards(); break;
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
            DimTheField(width, height);

            // Top bar: what this screen is, and the AI's difficulty (Milestone 5: it only changes how well the
            // AI decides, never what it knows or earns).
            float barWidth = Mathf.Min(1180f, width - 28f);
            var bar = new Rect((width - barWidth) * 0.5f, 10f, barWidth, 84f);
            Panel(bar);
            const float difficultyWidth = 590f;
            float titleWidth = bar.width - difficultyWidth - 34f;
            CardScreens.Fit(new Rect(bar.x + 18f, bar.y + 8f, titleWidth, 36f), "Starting draft", CardScreens.TitleStyle, 28, 18, TextAnchor.MiddleLeft, HudTheme.TitleColor);
            CardScreens.Fit(new Rect(bar.x + 18f, bar.y + 44f, titleWidth, 32f), "Its squads are free and start unlocked. Dearer squads mean a weaker base.", CardScreens.LineStyle, 15, 11, TextAnchor.MiddleLeft, HudTheme.DimText);
            float difficultyX = bar.xMax - difficultyWidth - 8f;
            GUI.Label(new Rect(difficultyX, bar.y + 26f, 140f, 30f), "AI difficulty:", _text);
            string[] names = { "Easy", "Normal", "Hard" };
            for (int d = 0; d < names.Length; d++)
            {
                bool selected = (int)match.Difficulty == d;
                Color old = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
                if (GUI.Button(new Rect(difficultyX + 145f + d * 148f, bar.y + 22f, 138f, 40f), selected ? $"[ {names[d]} ]" : names[d], _button))
                    match.SetDifficulty((AiDifficulty)d);
                GUI.backgroundColor = old;
            }

            // The sandbox (Milestone 4): place any two armies and watch them fight.
            var sandbox = new Rect(width * 0.5f - 200f, height - 62f, 400f, 52f);
            Panel(sandbox);
            if (GUI.Button(new Rect(sandbox.x + 8f, sandbox.y + 6f, sandbox.width - 16f, 40f), "Sandbox: test any two armies", _button))
                SceneManager.LoadScene(SandboxSceneName);

            int count = m.PlayerDraft.Count;
            CardRow row = CardLayout.Arrange(count, width, bar.yMax + 14f, sandbox.y - 14f);
            DraftRowText text = count > 0 ? CardScreens.MeasureDraftRow(m.PlayerDraft, row.CardAt(0)) : default;
            for (int i = 0; i < count; i++)
            {
                if (CardScreens.DrawDraftCard(row.CardAt(i), m.PlayerDraft[i], text, m.Rules.StartingBaseHealth(m.PlayerDraft[i].Value))) match.ChooseDraft(i);
            }
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
            GUI.Label(new Rect(bar.x + 16f, bar.y + 6f, 220f, 30f), HudTheme.Title($"Round {m.Round}: Deployment"), _header);
            GUI.Label(new Rect(bar.x + 16f, bar.y + 32f, 260f, 26f), $"Credits {player.Credits}  (+{m.NextIncomeFor(Team.A)} next round)", _text);
            GUI.Label(new Rect(bar.x + 290f, bar.y + 6f, 220f, 26f), $"Squads bought: {player.SquadsBoughtThisRound} of {m.SquadsPerRound(Team.A)}", _text);
            GUI.Label(new Rect(bar.x + 290f, bar.y + 32f, 220f, 26f), $"Unlocks: {player.UnlocksThisRound} of {m.Rules.UnlocksPerRound}", _text);
            BaseBar(new Rect(bar.x + 520f, bar.y + 10f, 220f, 20f), _yourBaseLine, "Your base", player.BaseHealth, player.StartingBaseHealth, playerColor);
            BaseBar(new Rect(bar.x + 520f, bar.y + 36f, 220f, 20f), _enemyBaseLine, "Enemy base", m.Enemy.BaseHealth, m.Enemy.StartingBaseHealth, enemyColor);
            GUI.Label(new Rect(bar.x + 760f, bar.y + 6f, 200f, 26f), $"{player.Faction.Name} vs {m.Enemy.Faction?.Name ?? "?"}", _small);
            GUI.Label(new Rect(bar.x + 760f, bar.y + 32f, 200f, 26f), m.EnemyAi.Label, _small);
            if (GUI.Button(new Rect(bar.xMax - 150f, bar.y + 10f, 136f, 44f), "Ready", _bigButton)) match.Ready();
            UiAudio.Watch(new Rect(bar.xMax - 150f, bar.y + 10f, 136f, 44f));

            if (!string.IsNullOrEmpty(match.Message))
            {
                var message = new Rect(width * 0.5f - 400f, bar.yMax + 6f, 800f, 30f);
                Panel(message);
                GUI.Label(message, match.Message, _smallCentered);
            }

            DrawSquadItemSlots();
            DrawShop(width, height);
            DrawInventory(DrawSpellBar());
            DrawTrainingIcons();
            DrawSelectedSquadActions(width, height);
            DrawTowerButtons(height);
            DrawTowerWindow(height);
            ItemHud.DrawDragged(match.IconFor);
        }

        // ------------------------------------------------------------------ cards (Milestone 7)

        private void DrawCards()
        {
            Match m = match.Match;
            CardOffer offer = m.Player.Offer;
            if (offer == null) return;
            float width = HudScale.ScaledWidth(_scale), height = HudScale.ScaledHeight(_scale);
            DimTheField(width, height);
            bool general = offer.Kind == CardKind.General;

            float barWidth = Mathf.Min(760f, width - 28f);
            var bar = new Rect((width - barWidth) * 0.5f, 10f, barWidth, 84f);
            Panel(bar);
            GUI.Label(new Rect(bar.x, bar.y + 8f, bar.width, 40f), HudTheme.Title(general ? $"Round {m.Round}: choose a card" : $"Round {m.Round}: choose reinforcements"), _centered);
            GUI.Label(new Rect(bar.x, bar.y + 48f, bar.width, 26f), $"Credits {m.Player.Credits}", _smallCentered);

            var skip = new Rect(width * 0.5f - 150f, height - 62f, 300f, 52f);
            if (GUI.Button(skip, $"Skip (+{offer.SkipBonus} credits)", _bigButton)) match.SkipCards();
            float bottom = skip.y - 12f;
            if (!string.IsNullOrEmpty(match.Message))
            {
                var message = new Rect(width * 0.5f - 400f, skip.y - 36f, 800f, 30f);
                Panel(message);
                GUI.Label(message, match.Message, _smallCentered);
                bottom = message.y - 8f;
            }

            if (offer.Count == 0)
                GUI.Label(new Rect(0f, height * 0.5f - 20f, width, 40f), "No cards left to offer.", _centered);
            string faction = m.Player.Faction?.Name;
            CardRow row = CardLayout.Arrange(offer.Count, width, bar.yMax + 14f, bottom);
            for (int i = 0; i < offer.Count; i++)
            {
                Rect card = row.CardAt(i);
                int price = offer.PriceOf(i);
                bool canAfford = m.Player.Credits >= price;
                bool taken = general
                    ? CardScreens.DrawGeneralCard(card, offer.Cards[i], m.Player.CopiesOwned(offer.Cards[i]), match.IconFor(offer.Cards[i]), price, canAfford)
                    : CardScreens.DrawReinforcementCard(card, offer.Reinforcements[i], faction, m.Player.Unlocked.Contains(offer.Reinforcements[i].Type), price, canAfford);
                if (taken) match.ChooseCard(i);
            }
        }

        /// <summary>Darkens the battlefield behind a card screen, and keeps clicks from reaching it.</summary>
        private void DimTheField(float width, float height)
        {
            var screen = new Rect(0f, 0f, width, height);
            UiHitAreas.Add(HudScale.ToScreen(screen, _scale));
            if (Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            GUI.color = new Color(0.01f, 0.012f, 0.02f, 0.72f);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // ------------------------------------------------------------------ items and training (D33)

        /// <summary>The Inventory: items waiting to be placed. Click one, then the hexagon over a squad.</summary>
        private void DrawInventory(float top)
        {
            List<ItemStats> items = match.Match.Player.Inventory;
            if (items.Count == 0) return;
            var panel = new Rect(10f, top, 330f, 44f + ItemHud.GridHeight(items.Count) + 44f);
            Panel(panel);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 28f), HudTheme.Title("Inventory"), _header);
            string hovered = ItemHud.DrawItemGrid(panel.x + 12f, panel.y + 40f, items, match.PlacingItem, match.IconFor, match.BeginPlacingItem);
            string caption = hovered ?? (match.PlacingItem != null ? ItemHud.Describe(match.PlacingItem) : "Drag an item onto a squad's hexagon.");
            GUI.Label(new Rect(panel.x + 12f, panel.yMax - 46f, panel.width - 24f, 42f), caption, _wrapDim);
        }

        private void DrawSquadItemSlots()
        {
            ItemHud.DrawSquadSlots(match.Match.Player.Squads, match.PlacingItem, _scale, match.SquadOnScreen, match.IconFor, squad => match.PlaceItemOn(squad), match.CancelItemPlacement);
        }

        private bool _confirmTraining;

        /// <summary>Intensive Training, top left (one icon per copy): select a squad, press a ready icon, confirm. Each icon shows its own recharge.</summary>
        private void DrawTrainingIcons()
        {
            Match m = match.Match;
            List<OwnedTraining> copies = m.Player.Trainings;
            if (copies.Count == 0) return;
            const float size = 64f, gap = 6f;
            float right = 14f;
            bool clickedReady = false;
            for (int i = 0; i < copies.Count; i++)
            {
                OwnedTraining copy = copies[i];
                var rect = new Rect(14f + i * (size + gap), 8f, size, size);
                right = rect.xMax;
                UiHitAreas.Add(HudScale.ToScreen(rect, _scale));
                int roundsLeft = copy.RoundsLeft(m.Round);
                Color old = GUI.color;
                if (roundsLeft > 0) GUI.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                bool clicked = ItemHud.DrawHexIcon(rect, match.IconFor(copy.Card), clickable: true);
                GUI.color = old;
                if (roundsLeft > 0) GUI.Label(rect, roundsLeft.ToString(), _centered);
                else if (clicked) clickedReady = true;
                if (rect.Contains(Event.current.mousePosition))
                    GUI.Label(new Rect(14f + copies.Count * (size + gap) + 2f, 10f, 300f, 60f),
                        roundsLeft > 0 ? $"{copy.Card.Name}: recharging, {roundsLeft} round(s)" : $"{copy.Card.Name}: select a squad, then click to fill its XP bar", _wrapDim);
            }

            if (clickedReady)
            {
                _confirmTraining = match.SelectedPlacedSquad != null;
                if (!_confirmTraining) match.UseTraining(null); // Shows "Select a squad first".
            }
            PlacedSquad squad = match.SelectedPlacedSquad;
            if (!_confirmTraining || squad == null)
            {
                _confirmTraining = false;
                return;
            }
            var panel = new Rect(right + 8f, 8f, 300f, 92f);
            Panel(panel);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 40f), $"Fill the XP bar of {squad.Type.Name}?", _wrap);
            if (GUI.Button(new Rect(panel.x + 12f, panel.yMax - 42f, 130f, 34f), "Confirm", _button))
            {
                match.UseTraining(squad);
                _confirmTraining = false;
            }
            if (GUI.Button(new Rect(panel.xMax - 142f, panel.yMax - 42f, 130f, 34f), "Cancel", _button)) _confirmTraining = false;
        }

        /// <summary>The player's spells during deployment: place, move or take back ready ones; recharging ones show how long.</summary>
        private float DrawSpellBar()
        {
            Match m = match.Match;
            List<OwnedSpell> spells = m.Player.Spells;
            if (spells.Count == 0) return 110f;
            const float rowHeight = 52f;
            var panel = new Rect(10f, 110f, 330f, 44f + spells.Count * rowHeight);
            Panel(panel);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 28f), HudTheme.Title("Spells"), _header);
            for (int i = 0; i < spells.Count; i++)
            {
                OwnedSpell spell = spells[i];
                var row = new Rect(panel.x + 6f, panel.y + 40f + i * rowHeight, panel.width - 12f, rowHeight - 4f);
                HudTheme.RowBox(row);
                Color old = GUI.color;
                GUI.color = SpellView.ColorFor(spell.Stats);
                GUI.DrawTexture(new Rect(row.x + 8f, row.y + 10f, 6f, row.height - 20f), Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(new Rect(row.x + 22f, row.y + 3f, row.width - 130f, 24f), spell.Stats.Name, _text);
                bool ready = spell.IsReady(m.Round);
                string status = !ready ? $"Recharging: {spell.RoundsLeft(m.Round)} round(s)"
                    : match.PlacingSpellId == spell.Id ? "Click on the field" : spell.IsPlaced ? "Placed" : "Ready";
                GUI.Label(new Rect(row.x + 22f, row.y + 25f, row.width - 130f, 20f), status, _small);
                if (!ready) continue;
                if (match.PlacingSpellId == spell.Id)
                {
                    if (GUI.Button(new Rect(row.xMax - 104f, row.y + 8f, 96f, 32f), "Cancel", _button)) match.CancelSpellPlacement();
                }
                else if (spell.IsPlaced)
                {
                    if (GUI.Button(new Rect(row.xMax - 104f, row.y + 8f, 62f, 32f), "Move", _button)) match.BeginPlacingSpell(spell);
                    if (GUI.Button(new Rect(row.xMax - 38f, row.y + 8f, 30f, 32f), "X", _button)) match.UnplaceSpell(spell);
                }
                else if (GUI.Button(new Rect(row.xMax - 104f, row.y + 8f, 96f, 32f), "Place", _button)) match.BeginPlacingSpell(spell);
            }
            return panel.yMax + 6f;
        }

        private const float ShopPanelWidth = 500f, ShopPicture = 50f;
        /// <summary>How wide the text of a shop row can be: the panel without its margins and scroll bar, the picture and the button.</summary>
        public const float ShopTextWidth = ShopPanelWidth - 30f - ShopPicture - 134f;
        /// <summary>The smallest a shop row's second line may shrink to, to stay on one line.</summary>
        public const int ShopLineLeastSize = 13;

        /// <summary>The second line of a shop row.</summary>
        public static string ShopLine(UnitStats unit, bool exclusive) => $"Tier {unit.Tier}, {RoleName(unit.Role)}, {Bodies(unit.FormsInSquad)}{(exclusive ? ", exclusive" : "")}";

        /// <summary>
        /// The shop's rows from the top, cheapest first (the owner). At the same price, the sub-faction's own units come first, since they are the
        /// reason to pick that card, and then the rest as the roster has them. For the display only: unlocking, prices, card offers and the AI go by
        /// the roster itself. It runs for every OnGUI event, so it sorts in place, and keeps the order of equals.
        /// </summary>
        public static void FillShopRows(List<UnitStats> roster, List<UnitStats> exclusive, List<UnitStats> rows)
        {
            rows.Clear();
            foreach (UnitStats unit in roster) if (exclusive.Contains(unit)) rows.Add(unit);
            foreach (UnitStats unit in roster) if (!exclusive.Contains(unit)) rows.Add(unit);
            for (int i = 1; i < rows.Count; i++)
            {
                UnitStats unit = rows[i];
                int at = i;
                for (; at > 0 && rows[at - 1].Cost > unit.Cost; at--) rows[at] = rows[at - 1];
                rows[at] = unit;
            }
        }

        private void DrawShop(float width, float height)
        {
            Match m = match.Match;
            PlayerState player = m.Player;
            List<UnitStats> roster = _shopRows;
            FillShopRows(player.Roster, player.SubFaction.ExclusiveUnits, roster);
            const float panelWidth = ShopPanelWidth, rowHeight = 58f, picture = ShopPicture;
            float panelHeight = Mathf.Min(height - 200f, 50f + roster.Count * rowHeight);
            var panel = new Rect(width - panelWidth - 10f, 110f, panelWidth, panelHeight);
            Panel(panel);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 28f), HudTheme.Title($"Shop: {player.Label}"), _header);

            var view = new Rect(panel.x + 6f, panel.y + 40f, panel.width - 12f, panel.height - 46f);
            var content = new Rect(0f, 0f, view.width - 18f, roster.Count * rowHeight);
            _shopScroll = GUI.BeginScrollView(view, _shopScroll, content);
            for (int i = 0; i < roster.Count; i++)
            {
                UnitStats unit = roster[i];
                var row = new Rect(0f, i * rowHeight, content.width, rowHeight - 4f);
                HudTheme.RowBox(row);
                CardScreens.DrawUnitPicture(new Rect(row.x + 4f, row.y + 2f, picture, picture), unit, player.Faction.Name, UnitIcons.FactionColor(player.Faction.Name));
                float textX = row.x + picture + 8f, textWidth = ShopTextWidth;
                int price = m.SquadPrice(Team.A, unit);
                GUI.Label(new Rect(textX, row.y + 2f, textWidth, 28f), $"{unit.Name} - {price} cr", _text);
                CardScreens.Fit(new Rect(textX, row.y + 25f, textWidth, 28f), ShopLine(unit, player.SubFaction.ExclusiveUnits.Contains(unit)), _shopLine, 15, ShopLineLeastSize, TextAnchor.MiddleLeft, HudTheme.DimText);

                var button = new Rect(row.xMax - 118f, row.y + 9f, 110f, 36f);
                // The row's picture and text open the stats window, on a level 1 squad of the type with no techs.
                var about = new Rect(row.x, row.y, button.x - row.x - 4f, row.height);
                if (GUI.Button(about, GUIContent.none, GUIStyle.none)) ShowShopUnit(unit);
                UiAudio.Watch(about);
                if (player.Unlocked.Contains(unit))
                {
                    bool canBuy = player.Credits >= price && player.SquadsBoughtThisRound < m.SquadsPerRound(Team.A);
                    GUI.enabled = canBuy;
                    if (GUI.Button(button, $"Buy {price}", _button)) match.Buy(unit);
                    UiAudio.Watch(button, canBuy);
                }
                else
                {
                    bool canUnlock = player.Credits >= unit.UnlockCost && player.UnlocksThisRound < m.Rules.UnlocksPerRound;
                    GUI.enabled = canUnlock;
                    if (GUI.Button(button, unit.UnlockCost > 0 ? $"Unlock {unit.UnlockCost}" : "Unlock (free)", _button)) match.Unlock(unit);
                    UiAudio.Watch(button, canUnlock);
                }
                GUI.enabled = true;
            }
            GUI.EndScrollView();
        }

        private SquadStatsWindow StatsWindow => _squadWindow != null ? _squadWindow : (_squadWindow = FindAnyObjectByType<SquadStatsWindow>());

        /// <summary>A second click on the same row closes it again.</summary>
        private void ShowShopUnit(UnitStats unit)
        {
            if (StatsWindow == null) return;
            Selection.Select(-1);
            Selection.ClearTower();
            StatsWindow.ShopUnit = StatsWindow.ShopUnit == unit ? null : unit;
        }

        private void DrawSelectedSquadActions(float width, float height)
        {
            PlacedSquad squad = match.SelectedPlacedSquad;
            if (squad == null || match.IsPlacing) return;
            bool movable = match.Match.CanMove(squad);
            var panel = new Rect(SquadStatsWindow.ActionsPanelLeft(width, 520f), height - 70f, 520f, 58f);
            Panel(panel);
            GUI.enabled = movable;
            if (GUI.Button(new Rect(panel.x + 10f, panel.y + 10f, 150f, 38f), "Move (M)", _button)) match.BeginMoving(squad);
            if (GUI.Button(new Rect(panel.x + 170f, panel.y + 10f, 150f, 38f), "Turn (R)", _button)) match.ToggleRotation();
            GUI.enabled = true;
            if (GUI.Button(new Rect(panel.x + 330f, panel.y + 10f, 180f, 38f), $"Sell +{match.Match.SellValue(squad)} (Del)", _button)) match.Sell(squad);
        }

        // ------------------------------------------------------------------ towers (D55)

        private SquadSelection Selection => _selection != null ? _selection : (_selection = battle.GetComponent<SquadSelection>());

        /// <summary>A small button floating over each tower, so that they can be found. The tower itself can be clicked too.</summary>
        private void DrawTowerButtons(float height)
        {
            Camera camera = Camera.main;
            if (camera == null || battle.Simulation == null || match.IsPlacing || match.PlacingSpell != null || match.PlacingItem != null) return;
            IReadOnlyList<SimUnit> units = battle.Simulation.Units;
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                if (!unit.Stats.IsTower) continue;
                Vector3 screen = camera.WorldToScreenPoint(new Vector3(unit.Position.X, 12f, unit.Position.Y));
                if (screen.z <= 0f) continue;
                var rect = new Rect(screen.x / _scale - 80f, height - screen.y / _scale - 16f, 160f, 28f);
                if (IsUnderAWindow(rect)) continue;
                UiHitAreas.Add(HudScale.ToScreen(rect, _scale));
                if (GUI.Button(rect, unit.Stats.Name, _button)) Selection.SelectTower(unit.Team, unit.Stats.Shockwave.Kind);
            }
        }

        /// <summary>True if something on the HUD covers this spot. A button there would be out of sight and would still take that window's clicks.</summary>
        private bool IsUnderAWindow(Rect rect)
        {
            foreach (Rect panel in _panelsDrawn) if (panel.Overlaps(rect)) return true;
            if (Selection.SelectedTower != null && _towerWindow.Overlaps(rect)) return true;
            return StatsWindow != null && StatsWindow.TryGetWindow(out Rect squad) && squad.Overlaps(rect);
        }

        /// <summary>The selected tower's window, where the squad window goes: what it is, and what it sells.</summary>
        private void DrawTowerWindow(float height)
        {
            if (!(Selection.SelectedTower is (Team team, TowerKind kind) selected)) return;
            if (Time.unscaledTime >= _towerPanelRefresh || _towerPanel == null || _towerPanelShown != selected)
            {
                _towerPanel = match.DescribeTower(team, kind);
                _towerPanelShown = selected;
                _towerPanelRefresh = Time.unscaledTime + 0.2f;
            }
            TowerPanel view = _towerPanel;
            if (view == null) return;

            const float windowWidth = 440f, inset = 18f, buttonHeight = 30f, rowGap = 4f;
            float textWidth = windowWidth - 2f * inset;
            float textHeight = 0f;
            foreach (string line in view.Lines) textHeight += _wrap.CalcHeight(new GUIContent(line), textWidth) + 4f;
            foreach (TowerButton button in view.Buttons) textHeight += buttonHeight + _wrapDim.CalcHeight(new GUIContent(button.Note), textWidth) + rowGap;
            float windowHeight = 62f + textHeight + 14f;
            var window = new Rect(10f, height - windowHeight - 10f, windowWidth, windowHeight);
            _towerWindow = window;
            Panel(window);
            GUI.Label(new Rect(window.x + inset, window.y + 8f, textWidth - 30f, 28f), HudTheme.Title(view.Title), _header);
            GUI.Label(new Rect(window.x + inset, window.y + 34f, textWidth - 30f, 22f), view.Side, _small);
            if (GUI.Button(new Rect(window.xMax - 36f, window.y + 8f, 24f, 22f), "x", _button)) Selection.ClearTower();

            float y = window.y + 60f;
            foreach (string line in view.Lines)
            {
                float lineHeight = _wrap.CalcHeight(new GUIContent(line), textWidth);
                GUI.Label(new Rect(window.x + inset, y, textWidth, lineHeight), line, _wrap);
                y += lineHeight + 4f;
            }
            foreach (TowerButton button in view.Buttons)
            {
                var rect = new Rect(window.x + inset, y, textWidth, buttonHeight);
                GUI.enabled = button.Enabled;
                if (GUI.Button(rect, button.Label, _button))
                {
                    button.OnPress?.Invoke();
                    _towerPanelRefresh = 0f;
                }
                UiAudio.Watch(rect, button.Enabled);
                GUI.enabled = true;
                float noteHeight = _wrapDim.CalcHeight(new GUIContent(button.Note), textWidth);
                GUI.Label(new Rect(window.x + inset, y + buttonHeight, textWidth, noteHeight), button.Note, _wrapDim);
                y += buttonHeight + noteHeight + rowGap;
            }
        }

        private (Team team, TowerKind kind)? _towerPanelShown;

        /// <summary>A line under a side's panel while a fallen tower's shockwave weakens its army.</summary>
        private void DrawShockwave(Rect under, HudLine line, Team team)
        {
            BattleSimulation simulation = battle.Simulation;
            ShockwaveStats shockwave = simulation?.ActiveShockwave(team);
            if (shockwave == null) return;
            int seconds = Mathf.CeilToInt(simulation.ShockwaveSecondsLeft(team));
            if (line.Changed(seconds)) line.Text = $"Shockwave: weakened for {seconds} s";
            var rect = new Rect(under.x, under.yMax + 4f, under.width, 30f);
            Panel(rect);
            GUI.Label(rect, line.Text, _smallCentered);
        }

        // ------------------------------------------------------------------ battle

        private void DrawBattle()
        {
            Match m = match.Match;
            float width = HudScale.ScaledWidth(_scale);
            var left = new Rect(10f, 8f, 280f, 64f);
            Panel(left);
            BaseBar(new Rect(left.x + 12f, left.y + 10f, 256f, 20f), _yourBaseLine, "Your base", m.Player.BaseHealth, m.Player.StartingBaseHealth, playerColor);
            GUI.Label(new Rect(left.x + 12f, left.y + 34f, 256f, 24f), ArmyValueText(_yourValueLine, Team.A), _small);
            DrawShockwave(left, _yourShockwaveLine, Team.A);

            var right = new Rect(width - 290f, 40f, 280f, 64f);
            Panel(right);
            BaseBar(new Rect(right.x + 12f, right.y + 10f, 256f, 20f), _enemyBaseLine, "Enemy base", m.Enemy.BaseHealth, m.Enemy.StartingBaseHealth, enemyColor);
            GUI.Label(new Rect(right.x + 12f, right.y + 34f, 256f, 24f), ArmyValueText(_enemyValueLine, Team.B), _small);
            DrawShockwave(right, _enemyShockwaveLine, Team.B);
            DrawTowerWindow(HudScale.ScaledHeight(_scale));
        }

        private string ArmyValueText(HudLine line, Team team)
        {
            float value = battle.AliveValue(team);
            if (line.Changed(HudLine.Whole(value))) line.Text = $"Army value {value:0}";
            return line.Text;
        }

        // ------------------------------------------------------------------ result and match over

        private void DrawResult()
        {
            RoundSummary s = match.Match.LastSummary;
            if (s == null) return;
            Rect panel = CenteredPanel(640f, 550f);
            float y = panel.y + 12f;
            GUI.Label(new Rect(panel.x, y, panel.width, 34f), HudTheme.Title($"Round {s.Round} result"), _centered);
            y += 44f;
            y = SummaryLines(s, panel, y);
            if (s.PlayerSquadsReadyToLevel.Count > 0)
            {
                GUI.Label(new Rect(panel.x + 20f, y + 8f, panel.width - 40f, 26f),
                    "Ready to level up: " + string.Join(", ", s.PlayerSquadsReadyToLevel.Distinct()) + " (select it next round)", _text);
            }

            string footer = (s.Tie ? "Exact tie: both bases are restored. " : "")
                            + $"Income: +{s.IncomeAwarded} credits for next round.";
            GUI.Label(new Rect(panel.x + 20f, panel.yMax - 96f, panel.width - 40f, 26f), footer, _text);
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
            if (GUI.Button(new Rect(panel.x + panel.width * 0.5f - 230f, panel.yMax - 58f, 220f, 44f), "New match", _bigButton)) match.NewMatch();
            if (MenuStyle.Button(new Rect(panel.x + panel.width * 0.5f + 10f, panel.yMax - 58f, 220f, 44f), "Main Menu", _bigButton)) GameFlow.OpenMainMenu();
        }

        /// <summary>Base damage, bases, survivors, top damage dealers and what killed what. Returns the next y.</summary>
        private float SummaryLines(RoundSummary s, Rect panel, float y)
        {
            float x = panel.x + 20f, w = panel.width - 40f;
            GUI.Label(new Rect(x, y, w, 26f), $"Base damage: you dealt {s.BaseDamageByA}, the enemy dealt {s.BaseDamageByB}", _text); y += 28f;
            GUI.Label(new Rect(x, y, w, 26f), $"Bases: yours {Mathf.Max(0, s.BaseHealthA)}, enemy {Mathf.Max(0, s.BaseHealthB)}", _text); y += 28f;
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
            return result.TeamA.UnitTypes.Concat(result.TeamB.UnitTypes).Concat(result.TeamA.Spells).Concat(result.TeamB.Spells)
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
            _panelsDrawn.Add(rect);
            UiHitAreas.Add(HudScale.ToScreen(rect, _scale));
            HudTheme.PanelBox(rect);
        }

        private void BaseBar(Rect rect, HudLine line, string label, int health, int maxHealth, Color color)
        {
            HudTheme.Bar(rect, maxHealth > 0 ? health / (float)maxHealth : 0f, color);
            if (line.Changed(health, maxHealth)) line.Text = $"{label}: {Mathf.Max(0, health)} / {maxHealth}";
            GUI.Label(rect, line.Text, _barLabel);
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
            _text = new GUIStyle(GUI.skin.label) { fontSize = HudTheme.Size(17), normal = { textColor = HudTheme.Text } };
            _small = new GUIStyle(_text) { fontSize = HudTheme.Size(15), alignment = TextAnchor.MiddleLeft, normal = { textColor = HudTheme.DimText } };
            _big = new GUIStyle(_text) { font = HudTheme.TitleFont, fontSize = HudTheme.TitleSize(24), fontStyle = HudTheme.TitleFontStyle, normal = { textColor = HudTheme.TitleColor } };
            _centered = new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter };
            _title = new GUIStyle(_centered) { fontSize = HudTheme.TitleSize(34) };
            _header = new GUIStyle(_text) { font = HudTheme.TitleFont, fontSize = HudTheme.TitleSize(18), fontStyle = HudTheme.TitleFontStyle, normal = { textColor = HudTheme.TitleColor } };
            _barLabel = new GUIStyle(_text) { fontSize = HudTheme.Size(15), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _button = new GUIStyle(GUI.skin.button) { fontSize = HudTheme.Size(15) };
            _bigButton = new GUIStyle(GUI.skin.button) { fontSize = HudTheme.Size(19), fontStyle = FontStyle.Bold };
            _smallCentered = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            _shopLine = new GUIStyle(_small) { wordWrap = false };
            _wrap = new GUIStyle(_text) { fontSize = HudTheme.Size(15), wordWrap = true };
            _wrapDim = new GUIStyle(_small) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            HudTheme.Fit(_wrap, _wrapDim);
            HudTheme.Fit(_text, _small, _big, _centered, _title, _header, _barLabel, _button, _bigButton, _smallCentered);
        }
    }
}
