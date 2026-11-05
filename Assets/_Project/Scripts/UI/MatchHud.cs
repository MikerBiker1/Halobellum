// MatchHud.cs
// The match screens (Bible Section 13), one per phase:
//   Draft: the starting draft cards (faction, sub-faction, 2 free squads) to choose from. Up to 5 cards fit (CardLayout); each is
//     drawn by CardScreens. The AI's difficulty and the Sandbox are chosen in the Play menu, before this.
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
//     In Unlock Skirmish a bar at the bottom right takes the shop's place: an icon and price for each unlocked unit (click to buy, hover
//     for its stats), and an "Unlock units" button that opens a page of every unit by tier and price, over a dimmed field. Esc, a
//     right-click, its x or a successful unlock closes the page. Its state is in plain methods, so that batch tests can use them.
//   Battle: both base health bars and a live army-value comparison (speed controls are in BattleHud), and how long
//     a fallen tower's shockwave still weakens each army.
//   Result: base damage dealt by each side, income, survivors, top damage dealers, what killed what.
//   Match over: the winner, a New match button and a Main Menu button, Open battle logs, and Battle stats, which opens a window
//     with a tab for each battle: the field from above as it started, and both armies' damage dealt, taken and stopped (BattleStatsWindow, D69).
//     Esc, a right-click or its Close button closes it.
// The look follows the player's faction (HudTheme: UNSC or Covenant style); the draft screen is neutral.
// Drawn with Unity's immediate-mode GUI; scales with the screen height.
// Depends on: Halobellum.Presentation (MatchController, BattleController, UiHitAreas),
//             Halobellum.Economy (Match, PlayerState, RoundSummary, ShopOrder), HudScale, CardLayout, CardScreens, UnitIcons, MenuStyle, SquadStatsWindow, PauseMenu.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.Units;
using UnityEngine;
using UnityEngine.InputSystem;

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

        private GUIStyle _title, _big, _text, _small, _smallCentered, _button, _bigButton, _centered, _header, _barLabel, _wrap, _wrapDim, _shopLine, _cellLine, _bannerWord, _bannerLine, _hitSmall, _hitMedium, _hitBig;
        private float _scale;
        private Vector2 _shopScroll;
        private readonly List<UnitStats> _shopRows = new List<UnitStats>();
        private Vector2 _barScroll, _pageScroll;
        private bool _unlockPageOpen;
        private int _unlockPageClosedFrame = -1;
        private readonly HudLine _yourBaseLine = new HudLine(), _enemyBaseLine = new HudLine(), _yourValueLine = new HudLine(), _enemyValueLine = new HudLine();
        private readonly HudLine _yourShockwaveLine = new HudLine(), _enemyShockwaveLine = new HudLine();
        private SquadSelection _selection;
        private TowerPanel _towerPanel;
        private float _towerPanelRefresh;
        private readonly List<Rect> _panelsDrawn = new List<Rect>();
        private SquadStatsWindow _squadWindow;
        private Rect _towerWindow;
        private readonly BattleStatsWindow _battleStats = new BattleStatsWindow();
        private int _battleStatsClosedFrame = -1;

        /// <summary>The Battle stats window (D69), for tests.</summary>
        public BattleStatsWindow BattleStatsWindow => _battleStats;

        public void OpenBattleStats()
        {
            if (match == null || match.Match == null || match.Match.Phase != MatchPhase.MatchOver) return;
            _battleStats.Open(match.Match);
        }

        public void CloseBattleStats()
        {
            if (!_battleStats.IsOpen) return;
            _battleStats.Close();
            _battleStatsClosedFrame = Time.frameCount;
        }

        /// <summary>True while Esc is for a page of this HUD (the unlock page, Battle stats), or just was: PauseMenu mustn't open on the press that closes one.</summary>
        public bool TakesEscape => UnlockPageTakesEscape || _battleStats.IsOpen || _battleStatsClosedFrame == Time.frameCount;

        private void Awake() => useGUILayout = false; // Nothing here uses GUILayout. With the layout pass on, Unity makes new layout groups for this script every frame, which is garbage at every frame rate.

        /// <summary>The page has its own rules while it is open. A squad landing during the drop selects itself, and M, R and Del would then act on it behind the page.</summary>
        private void Update()
        {
            if (match != null && match.Match != null && match.Match.Phase != MatchPhase.Cards) CardsHidden = CardsStartHidden; // Each card screen starts with its cards showing.
            if (OpensBattleStatsAtTheEnd && !_battleStatsOpenedForTest && match != null && match.Match != null && match.Match.Phase == MatchPhase.MatchOver)
            {
                // The test game's picture of the window, with its best squad picked out.
                _battleStatsOpenedForTest = true;
                OpenBattleStats();
                BattleStats last = match.Match.BattleHistory.Count > 0 ? match.Match.BattleHistory[match.Match.BattleHistory.Count - 1] : null;
                if (last != null && last.Side(Team.A, false).Count > 0) _battleStats.Pick(last, last.Side(Team.A, false)[0]);
            }
            if (_battleStats.IsOpen) UpdateBattleStats();
            if (!_unlockPageOpen) return;
            if (match == null || match.Match == null || match.Match.Phase != MatchPhase.Deployment)
            {
                _unlockPageOpen = false;
                return;
            }
            Selection.Select(-1);
            Selection.ClearTower();

            Keyboard keyboard = Keyboard.current;
            PauseMenu pause = PauseMenu.Current;
            bool closedThePauseMenu = pause != null && pause.ClosedThisFrame; // That press was for the menu in front.
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !closedThePauseMenu) HandleEscape();
            else if (RightClick.WasClicked) CloseUnlockPage();
        }

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

            // Top bar: what this screen is.
            float barWidth = Mathf.Min(1180f, width - 28f);
            var bar = new Rect((width - barWidth) * 0.5f, 10f, barWidth, 84f);
            Panel(bar);
            float titleWidth = bar.width - 36f;
            CardScreens.Fit(new Rect(bar.x + 18f, bar.y + 8f, titleWidth, 36f), HudTheme.Title("Starting draft"), CardScreens.TitleStyle, 28, 18, TextAnchor.MiddleLeft, HudTheme.TitleColor);
            CardScreens.Fit(new Rect(bar.x + 18f, bar.y + 44f, titleWidth, 32f), "Its squads are free and start unlocked. Dearer squads mean a weaker base.", CardScreens.LineStyle, 15, 11, TextAnchor.MiddleLeft, HudTheme.DimText);

            int count = m.PlayerDraft.Count;
            CardRow row = CardLayout.Arrange(count, width, bar.yMax + 14f, height - 14f);
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
            bool skirmish = m.Rules.Mode == MatchMode.UnlockSkirmish;
            if (_unlockPageOpen) DimTheField(width, height); // First, so that the top bar stays lit.

            // Top bar.
            var bar = new Rect(width * 0.5f - 560f, 8f, 1120f, 64f);
            Panel(bar);
            GUI.Label(new Rect(bar.x + 16f, bar.y + 6f, 220f, 30f), HudTheme.Title($"Round {m.Round}: Deployment"), _header);
            GUI.Label(new Rect(bar.x + 16f, bar.y + 32f, 260f, 26f), $"Credits {player.Credits}  (+{m.NextIncomeFor(Team.A)} next round)", _text);
            GUI.Label(new Rect(bar.x + 290f, bar.y + 6f, 220f, 26f), $"Squads bought: {player.SquadsBoughtThisRound} of {m.SquadsPerRound(Team.A)}", _text);
            GUI.Label(new Rect(bar.x + 290f, bar.y + 32f, 220f, 26f), $"Unlocks: {player.UnlocksThisRound} of {m.Rules.UnlocksPerRound}", _text);
            BaseBar(new Rect(bar.x + 520f, bar.y + 10f, 220f, 20f), _yourBaseLine, "Your base", player.BaseHealth, player.StartingBaseHealth, playerColor);
            BaseBar(new Rect(bar.x + 520f, bar.y + 36f, 220f, 20f), _enemyBaseLine, "Enemy base", m.Enemy.BaseHealth, m.Enemy.StartingBaseHealth, enemyColor);
            GUI.Label(new Rect(bar.x + 760f, bar.y + 6f, 200f, 26f), skirmish ? "Unlock Skirmish" : $"{player.Faction.Name} vs {m.Enemy.Faction?.Name ?? "?"}", _small);
            GUI.Label(new Rect(bar.x + 760f, bar.y + 32f, 200f, 26f), m.EnemyAi.Label, _small);
            // The left button only: a right-click closes the unlock page, with this button still live behind it, and GUI.Button takes any mouse button.
            if (MenuStyle.Button(new Rect(bar.xMax - 150f, bar.y + 10f, 136f, 44f), ReadyLabel, _bigButton) && Event.current.button == 0) match.Ready();

            if (!string.IsNullOrEmpty(match.Message))
            {
                var message = new Rect(width * 0.5f - 400f, bar.yMax + 6f, 800f, 30f);
                Panel(message);
                GUI.Label(message, match.Message, _smallCentered);
            }

            if (_unlockPageOpen)
            {
                DrawUnlockPage(width, height);
                DrawHoveredUnit();
                ItemHud.DrawDragged(match.IconFor);
                return;
            }

            DrawSquadItemSlots();
            if (skirmish) DrawUnitBar(width, height);
            else DrawShop(width, height);
            DrawInventory(DrawSpellBar());
            DrawTrainingIcons();
            DrawSelectedSquadActions(width, height);
            DrawTowerButtons(height);
            DrawTowerWindow(height);
            DrawHoveredUnit();
            ItemHud.DrawDragged(match.IconFor);
        }

        /// <summary>
        /// A hovered unit's stats, drawn from here so that they are over everything else of this script: over the page's dim and the spell bar. An order between scripts
        /// won't do it (GUI.depth): the pause menu switches this script off and on, and from then on it draws last.
        /// </summary>
        private void DrawHoveredUnit()
        {
            if (Event.current.type == EventType.Repaint && StatsWindow != null) StatsWindow.DrawHover();
        }

        // ------------------------------------------------------------------ cards (Milestone 7)

        private void DrawCards()
        {
            Match m = match.Match;
            CardOffer offer = m.Player.Offer;
            if (offer == null) return;
            float width = HudScale.ScaledWidth(_scale), height = HudScale.ScaledHeight(_scale);
            var skip = new Rect(width * 0.5f - 150f, height - 62f, 300f, 52f);
            var toggle = new Rect(skip.xMax + 12f, skip.y, 180f, skip.height);
            if (CardsHidden)
            {
                DrawHiddenCards(m, toggle, width);
                return;
            }
            DimTheField(width, height);
            bool general = offer.Kind == CardKind.General;

            float barWidth = Mathf.Min(760f, width - 28f);
            var bar = new Rect((width - barWidth) * 0.5f, 10f, barWidth, 84f);
            Panel(bar);
            GUI.Label(new Rect(bar.x, bar.y + 8f, bar.width, 40f), HudTheme.Title(general ? $"Round {m.Round}: choose a card" : $"Round {m.Round}: choose reinforcements"), _centered);
            GUI.Label(new Rect(bar.x, bar.y + 48f, bar.width, 26f), $"Credits {m.Player.Credits}", _smallCentered);

            if (GUI.Button(skip, $"Skip (+{offer.SkipBonus} credits)", _bigButton)) match.SkipCards();
            if (GUI.Button(toggle, "Hide cards", _bigButton)) ToggleCards();
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

        /// <summary>True while the player has put the cards aside to look at the board. A new card screen always starts with its cards showing.</summary>
        public bool CardsHidden { get; private set; }

        /// <summary>For pictures of the board at a card screen, which the test game has no mouse to reach.</summary>
        private static readonly bool CardsStartHidden = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autoplayHideCards") >= 0;
        private static readonly bool OpensBattleStatsAtTheEnd = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autoplayBattleStats") >= 0;
        private bool _battleStatsOpenedForTest;

        /// <summary>Puts the cards aside, or brings them back. Anything clicked on the board meanwhile is let go of, so that its window doesn't sit over the cards.</summary>
        public void ToggleCards()
        {
            CardsHidden = !CardsHidden;
            if (CardsHidden || Selection == null) return;
            Selection.Select(-1);
            Selection.ClearTower();
        }

        /// <summary>The board, with only what brings the cards back. Squads and towers can be clicked to see their windows; nothing here can change them, as this is not deployment.</summary>
        private void DrawHiddenCards(Match m, Rect toggle, float width)
        {
            float barWidth = Mathf.Min(420f, width - 28f);
            var bar = new Rect((width - barWidth) * 0.5f, 10f, barWidth, 40f);
            Panel(bar);
            GUI.Label(bar, $"Round {m.Round}: looking at the board. Credits {m.Player.Credits}", _smallCentered);

            UiHitAreas.Add(HudScale.ToScreen(toggle, _scale));
            if (GUI.Button(toggle, "Show cards", _bigButton)) ToggleCards();
            DrawTowerWindow(HudScale.ScaledHeight(_scale));
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
        /// The shop's rows from the top, by tier and then by price (the owner; ShopOrder). For the display only: unlocking, prices, card offers and
        /// the AI go by the roster itself. It runs for every OnGUI event, so it sorts in a list that is kept.
        /// </summary>
        public static void FillShopRows(List<UnitStats> roster, List<UnitStats> rows)
        {
            rows.Clear();
            rows.AddRange(roster);
            ShopOrder.Sort(rows);
        }

        private void DrawShop(float width, float height)
        {
            Match m = match.Match;
            PlayerState player = m.Player;
            List<UnitStats> roster = _shopRows;
            FillShopRows(player.Roster, roster);
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

        // ------------------------------------------------------------------ the unit bar and unlock page (Unlock Skirmish)

        private const float ActionsWidth = 520f, ActionsTop = 70f;
        private const float BarCell = 64f, BarCellHeight = 86f, BarGap = 6f, BarPadding = 12f, BarCellsTop = 44f, BarRight = 112f, BarBottom = 10f;
        private const float PageLeft = 456f, PageTop = 110f, PageCell = 84f, PageCellHeight = 104f, PageGap = 6f, PageTierHeader = 26f, PageCellsTop = 50f, PagePadding = 8f;
        private const float ScrollBarRoom = 18f;
        public const string UnlockUnitsLabel = "Unlock units", UnlockPageCloseKey = "Unlock page/x", ReadyLabel = "Ready";

        /// <summary>The test tool's key of a unit's cell in the bar, and on the page.</summary>
        public static string BarCellKey(UnitStats unit) => "Unit bar/" + unit.Name;
        public static string PageCellKey(UnitStats unit) => "Unlock page/" + unit.Name;

        /// <summary>For the test tool, in place of the mouse: the unit whose cell counts as hovered.</summary>
        public UnitStats DemoHover { get; set; }
        /// <summary>For a test tool: where the bar and the page were last drawn, in HUD units.</summary>
        public Rect UnitBarShown { get; private set; }
        public Rect UnlockPageShown { get; private set; }
        public Rect UnlockPageListShown { get; private set; }

        private void UpdateBattleStats()
        {
            if (match == null || match.Match == null || match.Match.Phase != MatchPhase.MatchOver)
            {
                _battleStats.Close();
                return;
            }
            Keyboard keyboard = Keyboard.current;
            PauseMenu pause = PauseMenu.Current;
            bool closedThePauseMenu = pause != null && pause.ClosedThisFrame;
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !closedThePauseMenu) || RightClick.WasClicked) CloseBattleStats();
        }

        /// <summary>For the test tool: draws this HUD once under the event it has set, to press a button with a mouse button of its choosing.</summary>
        public void DrawForTest() => OnGUI();

        public bool IsUnlockPageOpen => _unlockPageOpen;

        /// <summary>True while Esc is the page's, or just was: PauseMenu mustn't open on the press that closes it, whichever of them runs first.</summary>
        public bool UnlockPageTakesEscape => _unlockPageOpen || _unlockPageClosedFrame == Time.frameCount;

        /// <summary>Opens the page, over nothing that could go on acting behind it: no selection, and nothing being placed.</summary>
        public void OpenUnlockPage()
        {
            if (match == null || match.Match == null || match.Match.Phase != MatchPhase.Deployment || match.Match.Rules.Mode != MatchMode.UnlockSkirmish) return;
            match.CancelPlacement();
            match.CancelSpellPlacement();
            match.CancelItemPlacement();
            Selection.Select(-1);
            Selection.ClearTower();
            _pageScroll = Vector2.zero;
            _unlockPageOpen = true;
        }

        public void CloseUnlockPage()
        {
            if (!_unlockPageOpen) return;
            _unlockPageOpen = false;
            _unlockPageClosedFrame = Time.frameCount;
        }

        /// <summary>Unlocks a unit, and closes the page if that went through. If not, the page stays and the message says why.</summary>
        public ActionResult UnlockFromPage(UnitStats unit)
        {
            ActionResult result = match.Unlock(unit);
            if (result.Ok) CloseUnlockPage();
            return result;
        }

        /// <summary>Esc while the page is open closes it. Returns whether it did.</summary>
        public bool HandleEscape()
        {
            if (!_unlockPageOpen) return false;
            CloseUnlockPage();
            return true;
        }

        /// <summary>The bar's rows: the units unlocked, in the shop's order. The set of them has no order to show.</summary>
        public static void FillUnitBarRows(List<UnitStats> roster, HashSet<UnitStats> unlocked, List<UnitStats> rows)
        {
            FillShopRows(roster, rows);
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (!unlocked.Contains(rows[i])) rows.RemoveAt(i);
            }
        }

        /// <summary>How many icons across: as many as fit between the Move / Turn / Sell panel and the Menu button, whatever the UI scale.</summary>
        public static int UnitBarColumns(float width)
        {
            float actionsRight = SquadStatsWindow.ActionsPanelLeft(width, ActionsWidth) + ActionsWidth;
            return Mathf.Clamp(Mathf.FloorToInt((width - BarRight - (actionsRight + BarGap) - 2f * BarPadding + BarGap) / (BarCell + BarGap)), 4, 8);
        }

        /// <summary>
        /// Where the bar goes, in HUD units: at the bottom right, growing upward by the row. In a window too narrow to keep clear of the Move / Turn / Sell
        /// panel it stands above that panel, and if it would reach the top bar it stops there and scrolls.
        /// </summary>
        public static Rect UnitBarRect(float width, float height, int unitCount)
        {
            int columns = UnitBarColumns(width);
            int rows = Mathf.Max(1, Mathf.CeilToInt(unitCount / (float)columns));
            float panelWidth = 2f * BarPadding + columns * (BarCell + BarGap) - BarGap;
            float panelHeight = BarCellsTop + rows * (BarCellHeight + BarGap) - BarGap + BarPadding;
            float actionsRight = SquadStatsWindow.ActionsPanelLeft(width, ActionsWidth) + ActionsWidth;
            float bottom = width - BarRight - panelWidth < actionsRight + BarGap ? height - ActionsTop - BarGap : height - BarBottom;
            if (panelHeight > bottom - PageTop)
            {
                panelHeight = bottom - PageTop;
                panelWidth += ScrollBarRoom;
            }
            return new Rect(width - BarRight - panelWidth, bottom - panelHeight, panelWidth, panelHeight);
        }

        /// <summary>Where the page goes, in HUD units: right of the column the stats window stands in, under the top bar and its message.</summary>
        public static Rect UnlockPageRect(float width, float height) => new Rect(PageLeft, PageTop, width - PageLeft - 10f, height - PageTop - 10f);

        /// <summary>
        /// The part of the page that holds its list, in HUD units. It stops at the top of the Menu button (menuTop), which is drawn over the page's lower right corner and would
        /// take the clicks of a scroll bar or a cell under it. The whole roster still fits above that at 1080p and UI scale 1.
        /// </summary>
        public static Rect UnlockPageListRect(Rect page, float menuTop)
        {
            float top = page.y + PageCellsTop;
            return new Rect(page.x + PagePadding, top, page.width - 2f * PagePadding, Mathf.Min(page.yMax - PagePadding, menuTop) - top);
        }

        /// <summary>How many cells across the page's list, for the width it has.</summary>
        public static int UnlockPageColumns(float viewWidth) => Mathf.Max(1, Mathf.FloorToInt((viewWidth + PageGap) / (PageCell + PageGap)));

        /// <summary>How tall the page's list is: a header per tier, and rows of cells. The rows must be in tier order.</summary>
        public static float UnlockPageContentHeight(List<UnitStats> rows, int columns)
        {
            float height = 0f;
            int at = 0;
            while (at < rows.Count)
            {
                int tier = rows[at].Tier, inTier = 0;
                for (; at < rows.Count && rows[at].Tier == tier; at++) inTier++;
                height += PageTierHeader + (inTier + columns - 1) / columns * (PageCellHeight + PageGap);
            }
            return height;
        }

        /// <summary>A unit's picture (in its own faction's look) and squad price, and under that a line of status if there is one. Greyed if it can't be had now.</summary>
        private void DrawUnitCell(Rect cell, UnitStats unit, int price, string status, Color statusColor, bool available, bool hovered)
        {
            Color old = GUI.color;
            if (!available) GUI.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            HudTheme.RowBox(cell);
            if (hovered && Event.current.type == EventType.Repaint)
            {
                Color kept = GUI.color;
                Color accent = HudTheme.Accent;
                GUI.color = new Color(accent.r, accent.g, accent.b, 0.22f);
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
                GUI.color = kept;
            }
            float size = Mathf.Min(cell.width - 8f, 60f);
            var picture = new Rect(cell.center.x - size * 0.5f, cell.y + 3f, size, size);
            string faction = BattleFactions.OfUnit(unit, Team.A);
            CardScreens.DrawUnitPicture(picture, unit, faction, UnitIcons.FactionColor(faction));
            GUI.color = old; // Its price stays easy to read: that is why it can't be had.
            CardScreens.Fit(new Rect(cell.x + 2f, picture.yMax, cell.width - 4f, 22f), $"{price} cr", _cellLine, 15, 12, TextAnchor.MiddleCenter, available ? HudTheme.Text : HudTheme.DimText);
            if (status != null) CardScreens.Fit(new Rect(cell.x + 2f, picture.yMax + 20f, cell.width - 4f, 20f), status, _cellLine, 14, 11, TextAnchor.MiddleCenter, statusColor);
        }

        /// <summary>The bottom-right bar of Unlock Skirmish: the Unlock units button, and the units unlocked so far.</summary>
        private void DrawUnitBar(float width, float height)
        {
            Match m = match.Match;
            PlayerState player = m.Player;
            FillUnitBarRows(player.Roster, player.Unlocked, _shopRows);
            Rect panel = UnitBarRect(width, height, _shopRows.Count);
            UnitBarShown = panel;
            Panel(panel);

            var strip = new Rect(panel.x + BarPadding, panel.y + 8f, panel.width - 2f * BarPadding, 30f);
            if (MenuStyle.Button(strip, UnlockUnitsLabel, _button) && Event.current.button == 0) OpenUnlockPage();

            var view = new Rect(panel.x + BarPadding, panel.y + BarCellsTop, panel.width - 2f * BarPadding, panel.height - BarCellsTop - BarPadding);
            int columns = UnitBarColumns(width);
            int rowCount = Mathf.CeilToInt(_shopRows.Count / (float)columns);
            float contentHeight = rowCount * (BarCellHeight + BarGap) - BarGap;
            bool scrolls = contentHeight > view.height + 0.5f;
            Vector2 pointer = Event.current.mousePosition;
            bool inView = view.Contains(pointer); // Before the scroll view, which moves the pointer to its own coordinates.
            Vector2 scroll = Vector2.zero, origin = view.position;
            if (scrolls)
            {
                _barScroll = GUI.BeginScrollView(view, _barScroll, new Rect(0f, 0f, columns * (BarCell + BarGap) - BarGap, contentHeight));
                scroll = _barScroll;
                origin = Vector2.zero;
            }

            for (int i = 0; i < _shopRows.Count; i++)
            {
                UnitStats unit = _shopRows[i];
                float cellX = i % columns * (BarCell + BarGap), cellY = i / columns * (BarCellHeight + BarGap);
                var cell = new Rect(origin.x + cellX, origin.y + cellY, BarCell, BarCellHeight);
                var onScreen = new Rect(view.x + cellX - scroll.x, view.y + cellY - scroll.y, BarCell, BarCellHeight);
                bool hovered = unit == DemoHover || (inView && onScreen.Contains(pointer));
                int price = m.SquadPrice(Team.A, unit);
                bool canBuy = player.Credits >= price && player.SquadsBoughtThisRound < m.SquadsPerRound(Team.A);
                DrawUnitCell(cell, unit, price, null, default, canBuy, hovered);
                if (MenuStyle.Button(cell, "", GUIStyle.none, BarCellKey(unit)) && Event.current.button == 0) match.Buy(unit);
                if (hovered && Event.current.type == EventType.Repaint && StatsWindow != null) StatsWindow.ShowHover(unit, panel.xMax, panel.y - BarGap, panel.x - BarGap);
            }
            if (scrolls) GUI.EndScrollView();
        }

        /// <summary>Every unit by tier and price: click one to unlock it. Hovering shows its stats in the column on the left.</summary>
        private void DrawUnlockPage(float width, float height)
        {
            Match m = match.Match;
            PlayerState player = m.Player;
            Rect page = UnlockPageRect(width, height);
            UnlockPageShown = page;
            Panel(page);
            GUI.Label(new Rect(page.x + 16f, page.y + 10f, page.width - 80f, 30f), HudTheme.Title(UnlockUnitsLabel), _header);
            if (MenuStyle.Button(new Rect(page.xMax - 46f, page.y + 10f, 32f, 28f), "x", _button, UnlockPageCloseKey)) CloseUnlockPage();

            FillShopRows(player.Roster, _shopRows);
            Rect view = UnlockPageListRect(page, PauseMenu.CornerButtonPixels(Screen.width, Screen.height, GameSettings.UiScale).y / _scale);
            UnlockPageListShown = view;
            int columns = UnlockPageColumns(view.width);
            float contentHeight = UnlockPageContentHeight(_shopRows, columns);
            if (contentHeight > view.height)
            {
                columns = UnlockPageColumns(view.width - ScrollBarRoom);
                contentHeight = UnlockPageContentHeight(_shopRows, columns);
            }

            Vector2 pointer = Event.current.mousePosition;
            bool inView = view.Contains(pointer); // Before the scroll view, which moves the pointer to its own coordinates.
            _pageScroll = GUI.BeginScrollView(view, _pageScroll, new Rect(0f, 0f, columns * (PageCell + PageGap) - PageGap, contentHeight));
            float y = 0f;
            int column = 0, tier = int.MinValue;
            for (int i = 0; i < _shopRows.Count; i++)
            {
                UnitStats unit = _shopRows[i];
                if (unit.Tier != tier)
                {
                    if (column > 0) y += PageCellHeight + PageGap;
                    column = 0;
                    tier = unit.Tier;
                    GUI.Label(new Rect(4f, y, 300f, PageTierHeader), $"Tier {tier}", _text);
                    y += PageTierHeader;
                }

                float cellX = column * (PageCell + PageGap);
                var cell = new Rect(cellX, y, PageCell, PageCellHeight);
                var onScreen = new Rect(view.x + cellX - _pageScroll.x, view.y + y - _pageScroll.y, PageCell, PageCellHeight);
                bool hovered = unit == DemoHover || (inView && onScreen.Contains(pointer));
                bool unlocked = player.Unlocked.Contains(unit);
                bool canUnlock = player.Credits >= unit.UnlockCost && player.UnlocksThisRound < m.Rules.UnlocksPerRound;
                string status = unlocked ? "Unlocked" : unit.UnlockCost > 0 ? $"Unlock {unit.UnlockCost}" : "Unlock (free)";
                DrawUnitCell(cell, unit, m.SquadPrice(Team.A, unit), status, unlocked ? HudTheme.Accent : canUnlock ? HudTheme.Text : HudTheme.DimText, unlocked || canUnlock, hovered);
                if (MenuStyle.Button(cell, "", GUIStyle.none, PageCellKey(unit)) && Event.current.button == 0) UnlockFromPage(unit);
                if (hovered && Event.current.type == EventType.Repaint && StatsWindow != null) StatsWindow.ShowHover(unit, SquadStatsWindow.ColumnRight, height - 10f, SquadStatsWindow.ColumnRight);

                column++;
                if (column == columns)
                {
                    column = 0;
                    y += PageCellHeight + PageGap;
                }
            }
            GUI.EndScrollView();
        }

        private void DrawSelectedSquadActions(float width, float height)
        {
            PlacedSquad squad = match.SelectedPlacedSquad;
            if (squad == null || match.IsPlacing) return;
            bool movable = match.Match.CanMove(squad);
            var panel = new Rect(SquadStatsWindow.ActionsPanelLeft(width, ActionsWidth), height - ActionsTop, ActionsWidth, 58f);
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
            Rect yourBar = new Rect(left.x + 12f, left.y + 10f, 256f, 20f);
            BaseBar(Kicked(yourBar, Team.A), _yourBaseLine, "Your base", match.ShownBaseHealth(Team.A), m.Player.StartingBaseHealth, playerColor);
            FlashBar(yourBar, Team.A);
            GUI.Label(new Rect(left.x + 12f, left.y + 34f, 256f, 24f), ArmyValueText(_yourValueLine, Team.A), _small);
            DrawShockwave(left, _yourShockwaveLine, Team.A);

            var right = new Rect(width - 290f, 40f, 280f, 64f);
            Panel(right);
            Rect enemyBar = new Rect(right.x + 12f, right.y + 10f, 256f, 20f);
            BaseBar(Kicked(enemyBar, Team.B), _enemyBaseLine, "Enemy base", match.ShownBaseHealth(Team.B), m.Enemy.StartingBaseHealth, enemyColor);
            FlashBar(enemyBar, Team.B);
            GUI.Label(new Rect(right.x + 12f, right.y + 34f, 256f, 24f), ArmyValueText(_enemyValueLine, Team.B), _small);
            DrawShockwave(right, _enemyShockwaveLine, Team.B);
            DrawTowerWindow(HudScale.ScaledHeight(_scale));
            DrawEnding(yourBar, enemyBar);
        }

        // ------------------------------------------------------------------ the nodes of light at the end of a round

        /// <summary>A base's bar, moved by the shake of the last hit on it.</summary>
        private Rect Kicked(Rect bar, Team team)
        {
            RoundEnding ending = match.Ending;
            if (ending == null) return bar;
            float kick = ending.Kick(team);
            if (kick <= 0f) return bar;
            bar.x += Mathf.Sin(ending.Clock * 70f) * 12f * kick;
            bar.y += Mathf.Cos(ending.Clock * 90f) * 6f * kick;
            return bar;
        }

        private void FlashBar(Rect bar, Team team)
        {
            RoundEnding ending = match.Ending;
            if (ending == null) return;
            float kick = ending.Kick(team);
            if (kick <= 0f) return;
            GUI.color = new Color(1f, 1f, 1f, 0.8f * kick);
            GUI.DrawTexture(Kicked(bar, team), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawEnding(Rect yourBar, Rect enemyBar)
        {
            RoundEnding ending = match.Ending;
            Camera camera = Camera.main;
            if (ending == null || camera == null) return;

            for (int i = 0; i < ending.Nodes.Count; i++)
            {
                RoundEnding.Node node = ending.Nodes[i];
                float time = (ending.Clock - node.Starts) / node.Seconds;
                if (node.Landed || time < 0f) continue;
                Vector3 screen = camera.WorldToScreenPoint(node.From);
                var from = new Vector2(screen.x / _scale, (Screen.height - screen.y) / _scale);
                Vector2 to = (node.Target == Team.A ? yourBar : enemyBar).center;
                Color color = Color.Lerp(node.Source == Team.A ? playerColor : enemyColor, Color.white, 0.25f);
                float radius = 13f + 32f * node.Size;
                float bend = i % 2 == 0 ? 60f : -60f; // Crowds spread out on their way instead of flying in one line.

                // A tail that shows how fast it has become: the points are close while it is slow and far apart when it is fast.
                for (int tail = TailPoints; tail >= 1; tail--)
                {
                    float share = 1f - tail / (TailPoints + 1f);
                    float tailTime = time - tail * 0.035f;
                    if (tailTime <= 0f) continue;
                    DrawGlow(NodePlace(from, to, tailTime, bend), radius * (0.4f + 0.5f * share), new Color(color.r, color.g, color.b, 0.45f * share));
                }
                Vector2 place = NodePlace(from, to, time, bend);
                DrawGlow(place, radius, color);
                DrawGlow(place, radius * 0.5f, Color.white);
            }

            for (int i = 0; i < ending.Numbers.Count; i++)
            {
                RoundEnding.Number number = ending.Numbers[i];
                float age = (ending.Clock - number.Born) / RoundEnding.NumberSeconds;
                Rect bar = number.Target == Team.A ? yourBar : enemyBar;
                GUIStyle style = number.Size < 0.45f ? _hitSmall : number.Size < 0.8f ? _hitMedium : _hitBig;
                // A crowd's numbers spread out under the bar, so that they can be read: each has its own place along it and its own distance below.
                float below = Mathf.Abs(number.Along * 7919f) % 1f * 70f;
                var rect = new Rect(Mathf.Clamp(bar.center.x + number.Along * bar.width * 0.62f, 40f, HudScale.ScaledWidth(_scale) - 40f) - 80f, bar.yMax + 6f + below + age * 50f, 160f, 60f);
                float alpha = 1f - age * age;
                GUI.color = new Color(0f, 0f, 0f, alpha);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), number.Text, style);
                GUI.color = number.Target == Team.A ? new Color(1f, 0.32f, 0.26f, alpha) : new Color(1f, 0.88f, 0.35f, alpha);
                GUI.Label(rect, number.Text, style);
            }
            GUI.color = Color.white;
        }

        private static Vector2 NodePlace(Vector2 from, Vector2 to, float time, float bend)
        {
            float along = RoundEnding.Ease(time);
            Vector2 way = to - from;
            Vector2 aside = new Vector2(-way.y, way.x).normalized;
            return from + way * along + aside * (Mathf.Sin(along * Mathf.PI) * bend);
        }

        private static void DrawGlow(Vector2 center, float radius, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), Glow);
        }

        private const int TailPoints = 6;
        private static Texture2D _glow;

        /// <summary>A soft round light, white so that it can take any colour.</summary>
        private static Texture2D Glow
        {
            get
            {
                if (_glow != null) return _glow;
                const int size = 64;
                _glow = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float off = new Vector2(x - size * 0.5f + 0.5f, y - size * 0.5f + 0.5f).magnitude / (size * 0.5f);
                        float light = Mathf.Clamp01(1f - off);
                        _glow.SetPixel(x, y, new Color(1f, 1f, 1f, light * light * (3f - 2f * light)));
                    }
                }
                _glow.Apply();
                return _glow;
            }
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
            Team? winner = s.RoundWinner;
            string word = winner == null ? "MATCH DRAWN" : winner == Team.A ? "MATCH WON" : "MATCH LOST";
            bool bothFell = s.Battle.TeamA.Survivors == 0 && s.Battle.TeamB.Survivors == 0;
            string line = bothFell ? $"Round {s.Round}: both armies fell, and neither base was hit" : $"Round {s.Round}: you hit the enemy base for {s.BaseDamageByA}, it hit yours for {s.BaseDamageByB}";
            DrawOutcome(word, winner == null ? DrawColor : winner == Team.A ? VictoryColor : DefeatColor, line, 550f);
            Rect panel = CenteredPanel(640f, 550f, BannerRoom);
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
            if (_battleStats.IsOpen)
            {
                float screenWidth = HudScale.ScaledWidth(_scale), screenHeight = HudScale.ScaledHeight(_scale);
                DimTheField(screenWidth, screenHeight);
                _battleStats.Draw(screenWidth, screenHeight, m, match.BattlePictureOf, match.IconFor, playerColor, enemyColor, Panel);
                if (!_battleStats.IsOpen) _battleStatsClosedFrame = Time.frameCount;
                return;
            }
            RoundSummary s = m.LastSummary;
            bool won = m.Winner == Team.A;
            DrawOutcome(won ? "VICTORY" : "DEFEAT", won ? GameVictoryColor : DefeatColor, won ? $"You won the game in round {m.Round}: the enemy base ended at {m.Enemy.BaseHealth}" : $"You lost the game in round {m.Round}: your base ended at {m.Player.BaseHealth}", 560f);
            Rect panel = CenteredPanel(640f, 560f, BannerRoom);
            float y = panel.y + 12f;
            GUI.Label(new Rect(panel.x, y, panel.width, 40f), HudTheme.Title("Match over"), _title);
            y += 52f;
            if (s != null) SummaryLines(s, panel, y);
            if (GUI.Button(new Rect(panel.x + panel.width * 0.5f - 230f, panel.yMax - 100f, 220f, 34f), "Battle stats", _button)) OpenBattleStats();
            if (GUI.Button(new Rect(panel.x + panel.width * 0.5f + 10f, panel.yMax - 100f, 220f, 34f), "Open battle logs", _button)) Application.OpenURL("file:///" + BattleLogFile.Folder.Replace('\\', '/'));
            if (GUI.Button(new Rect(panel.x + panel.width * 0.5f - 230f, panel.yMax - 58f, 220f, 44f), "New match", _bigButton)) match.NewMatch();
            if (MenuStyle.Button(new Rect(panel.x + panel.width * 0.5f + 10f, panel.yMax - 58f, 220f, 44f), "Main Menu", _bigButton)) GameFlow.OpenMainMenu();
        }

        private const float BannerHeight = 140f, BannerRoom = 72f;
        private static readonly Color VictoryColor = new Color(0.09f, 0.55f, 0.2f), GameVictoryColor = new Color(0.78f, 0.56f, 0.06f), DefeatColor = new Color(0.68f, 0.07f, 0.07f), DrawColor = new Color(0.32f, 0.35f, 0.4f);

        /// <summary>Whether it was won or lost, too big and too colourful to miss: a band right across the screen above the window says the word.</summary>
        private void DrawOutcome(string word, Color color, string line, float panelHeight)
        {
            float width = HudScale.ScaledWidth(_scale), height = HudScale.ScaledHeight(_scale);
            var band = new Rect(0f, (height - panelHeight) * 0.5f + BannerRoom - BannerHeight - 4f, width, BannerHeight);
            UiHitAreas.Add(HudScale.ToScreen(band, _scale));
            GUI.color = color;
            GUI.DrawTexture(band, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(band.x, band.y, band.width, 4f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(band.x, band.yMax - 4f, band.width, 4f), Texture2D.whiteTexture);

            var wordRect = new Rect(0f, band.y + 10f, width, 86f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.Label(new Rect(wordRect.x + 3f, wordRect.y + 3f, wordRect.width, wordRect.height), word, _bannerWord);
            GUI.color = Color.white;
            GUI.Label(wordRect, word, _bannerWord);
            GUI.Label(new Rect(0f, band.y + 98f, width, 34f), line, _bannerLine);
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
            return result.TeamA.UnitTypes.Concat(result.TeamB.UnitTypes).Concat(result.TeamA.Spells).Concat(result.TeamB.Spells)
                .Where(t => t.DamageDealt > 0f)
                .OrderByDescending(t => t.DamageDealt)
                .Take(count);
        }

        // ------------------------------------------------------------------ helpers

        private Rect CenteredPanel(float w, float h, float lower = 0f)
        {
            float width = HudScale.ScaledWidth(_scale), height = HudScale.ScaledHeight(_scale);
            var rect = new Rect((width - w) * 0.5f, (height - h) * 0.5f + lower, w, h);
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
            if (line.Changed(health, maxHealth)) line.Text = BaseLine(label, health, maxHealth);
            // Below zero the number goes on, so that it shows how far a base was overkilled.
            Color oldColor = GUI.contentColor;
            if (health < 0) GUI.contentColor = OverkillColor;
            GUI.Label(rect, line.Text, _barLabel);
            GUI.contentColor = oldColor;
        }

        private static readonly Color OverkillColor = new Color(1f, 0.55f, 0.48f);

        /// <summary>What a base's bar says. Its health can be below zero.</summary>
        public static string BaseLine(string label, int health, int maxHealth) => $"{label}: {health} / {maxHealth}";

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
            _cellLine = new GUIStyle(_small) { wordWrap = false };
            _bannerWord = new GUIStyle(_title) { fontSize = HudTheme.TitleSize(76), alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _bannerLine = new GUIStyle(_text) { fontSize = HudTheme.Size(21), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _hitSmall = new GUIStyle(_text) { fontSize = HudTheme.Size(21), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, normal = { textColor = Color.white } };
            _hitMedium = new GUIStyle(_hitSmall) { fontSize = HudTheme.Size(30) };
            _hitBig = new GUIStyle(_hitSmall) { fontSize = HudTheme.Size(44) };
            _wrap = new GUIStyle(_text) { fontSize = HudTheme.Size(15), wordWrap = true };
            _wrapDim = new GUIStyle(_small) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            HudTheme.Fit(_wrap, _wrapDim);
            HudTheme.Fit(_text, _small, _big, _centered, _title, _header, _barLabel, _button, _bigButton, _smallCentered, _bannerWord, _bannerLine, _hitSmall, _hitMedium, _hitBig);
        }
    }
}
