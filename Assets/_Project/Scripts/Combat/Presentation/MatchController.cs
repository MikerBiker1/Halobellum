// MatchController.cs
// Runs a match in the Match scene (Bible Section 5). It owns the plain C# Match and connects it to
// the scene: during deployment it shows the player's squads as a preview battle (the AI's current
// squads stay hidden; its army as it was at the last battle start shows as grey ghosts the player
// can select, decision D25), forwards the player's actions (unlock, buy, move, turn, sell), and on Ready hands
// the round's battle to the BattleController to play. When the battle ends it applies the result
// (base damage, income) and the HUD shows the result screen. A bought squad is placed in the middle
// of the zone, drops in from high above and is selected when it lands, ready to be moved. Squads
// ready to level up show a pulsing arrow (decision D26). From round 2 each round opens with the card
// screen (Milestone 7): take a spell or reinforcement card, or skip. Owned spells are placed during
// deployment (SpellView draws the blue / red outlines and handles the mouse) and go off in battle.
// Items wait in the Inventory until the player selects one and picks a squad; Intensive Training fills
// a selected squad's XP bar (decision D33). The HUD draws both and calls the functions here.
// A click on a tower selects it (SquadSelection), and DescribeTower says what its window shows and sells (D55).
// A match can start with a MatchSetup (mode, base health, card filter), which goes on that match's own copy of the rules and
// is remembered, so that "New match" plays the same again. In Unlock Skirmish both sides draft from one made-up faction
// (SkirmishFaction), the seed picks one land for the whole field, and each unit keeps the looks of its real faction.
// The HUD (Scripts/UI/MatchHud) and DeploymentInput call the public methods here; tests do too.
// With -replayLog it adds a MatchReplay, which plays a battle log again through these same methods (D106).
// LocalTeam (M11) is the side this screen plays and shows at the bottom: Team A in single-player, Team B for a LAN guest. Every action,
// the preview, the colours and the camera's home follow it. UseMatch starts the scene on a match built elsewhere (a two-humans match
// from LanMatchFactory) for either side; PrepareFactions and FreshRules give that match the same unit and card objects as the views use.
// It adds a TopSquadGlow, which glows under each side's squad with the most damage this battle.
// A LAN match (M11 Stage 4): Start finds the launch GameFlow kept and adds a LanMatch, which builds the two-humans match and runs it
// through its coordinator. Then the starting card and Ready go through the coordinator, the battle starts when both games agree
// (ShowStartedBattle), the round finishes through it (the guest waits for the host's result), and after Ready no action is taken
// until the battle (ActionsLocked).
// Depends on: BattleController, SquadSelection, UnitViewManager, TopSquadGlow,
//             Halobellum.Economy (Match, RuleBasedAi, DeploymentGrid),
//             Halobellum.Units (MatchRulesDefinition, FactionDefinition, UnitDefinition), UnityEngine.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Units;
using UnityEngine;

namespace Halobellum.Presentation
{
    [RequireComponent(typeof(BattleController))]
    public sealed class MatchController : MonoBehaviour, IDeploymentPlacement, ISquadProgression
    {
        [SerializeField] private MatchRulesDefinition matchRules;
        [SerializeField] private List<FactionDefinition> factions = new List<FactionDefinition>();
        [Tooltip("Measured unit-vs-unit win rates (Data/AI/MatchupTable.asset) that the Hard AI uses to pick counters.")]
        [SerializeField] private MatchupTableDefinition matchupTable;
        [Tooltip("Match seed. 0 = a new random match every time.")]
        [SerializeField] private int seed;

        private BattleController _battle;
        private SquadSelection _selection;
        private UnitViewManager _views;
        private SpellView _spellView;
        private readonly Dictionary<CardStats, CardDefinition> _cardSources = new Dictionary<CardStats, CardDefinition>();
        /// <summary>Squads still dropping in: placed squad id -> Time.unscaledTime the drop began.</summary>
        private readonly Dictionary<int, float> _drops = new Dictionary<int, float>();
        private readonly Dictionary<UnitStats, UnitDefinition> _sources = new Dictionary<UnitStats, UnitDefinition>();
        private readonly List<PlacedSquad> _previewOrder = new List<PlacedSquad>();
        /// <summary>The enemy's ghost squads in the preview, after the player's (decision D25).</summary>
        private readonly List<SeenSquad> _ghostOrder = new List<SeenSquad>();
        /// <summary>What the match was started with, or null for Faction mode at the asset's values.</summary>
        private MatchSetup _setup;
        /// <summary>The real factions of this match, whatever the mode offers.</summary>
        private List<FactionInfo> _realFactions = new List<FactionInfo>();
        private static readonly string[] SkirmishLands = { BattleFactions.Unsc, BattleFactions.Covenant, BattleFactions.Flood };

        public Match Match { get; private set; }
        /// <summary>Feedback from the last action, for the HUD (empty when nothing to say).</summary>
        public string Message { get; private set; } = "";
        /// <summary>Raised after every phase change (draft chosen, battle started or finished, next round).</summary>
        public event Action PhaseChanged;
        /// <summary>Raised after each action the player takes, with the name of its method and how it went (for sound).</summary>
        public event Action<string, ActionResult> ActionTaken;
        /// <summary>The end of a round, between the last shot landing and its window; null the rest of the time.</summary>
        public RoundEnding Ending { get; private set; }
        /// <summary>LAN guest: the round ending that has landed while the host's result is still coming, so the bars keep its hits; null otherwise.</summary>
        private RoundEnding _landedEnding;
        /// <summary>Raised as a node of light hits a base (for sound).</summary>
        public event Action<RoundEnding.Node> EndingHit;
        /// <summary>The fastest battle speed a player can pick (BattleHud's 4x).</summary>
        public const float FastestPlayerSpeed = 4f;

        public const string DifficultyKey = "Halobellum.AiDifficulty";
        private MatchupTable _matchups;

        /// <summary>The AI's difficulty (chosen on the draft screen, remembered between matches).</summary>
        public AiDifficulty Difficulty { get; private set; } = AiDifficulty.Normal;

        /// <summary>The squad being moved, or -1.</summary>
        public int MovingSquadId { get; private set; } = -1;
        /// <summary>Whether the squad being moved is turned 90 degrees.</summary>
        public bool PlacingRotated { get; private set; }
        public bool IsPlacing => MovingSquadId >= 0;
        /// <summary>A side that has units that can flank sees where they could go, throughout deployment.</summary>
        public bool ShowsFlankZones => IsDeploying && Me.Faction != null && (HasUnitsThatCanFlank() || PulsesFlankZones);
        /// <summary>The tutorial's flank zone step: the zones glow on and off.</summary>
        public bool PulsesFlankZones => Tutorial?.Current?.FlankZones == true;
        /// <summary>Deployment, and this side has no card offer open (a two-humans match keeps the phase at Deployment while one is open).</summary>
        public bool IsDeploying => Match != null && Match.Phase == MatchPhase.Deployment && !Match.InCards(LocalTeam) && !ActionsLocked;
        /// <summary>The player always deploys in their own zone.</summary>
        public Team PlacingTeam => LocalTeam;

        // ------------------------------------------------------------------ the local side (M11)

        /// <summary>The side this screen plays and shows at the bottom: Team A, except for a LAN guest (Team B).</summary>
        public Team LocalTeam { get; private set; } = Team.A;
        /// <summary>The other side.</summary>
        public Team OpponentTeam => LocalTeam.Opponent();
        /// <summary>The local side's state, or null without a match.</summary>
        public PlayerState Me => Match?.Get(LocalTeam);
        /// <summary>The other side's state, or null without a match.</summary>
        public PlayerState Them => Match?.Get(OpponentTeam);
        /// <summary>True for the local side.</summary>
        public bool IsMine(Team team) => team == LocalTeam;
        /// <summary>The draft cards the local side picks from.</summary>
        public List<DraftCard> MyDraft => Match == null ? null : LocalTeam == Team.A ? Match.PlayerDraft : Match.EnemyDraft;
        /// <summary>True once the field has been set up for the armies after the draft (both picks in).</summary>
        private bool _draftDone;

        // ------------------------------------------------------------------ a LAN match (M11 Stage 4)

        /// <summary>The LAN match driver of this scene, or null in single-player.</summary>
        public LanMatch Lan { get; internal set; }

        /// <summary>True when the LAN coordinator runs the match on show (the starting card, Ready, the battle's end and Continue go through it).</summary>
        private bool LanRuns => Lan != null && Lan.Coordinator != null && Lan.Coordinator.Match == Match;

        /// <summary>True in a LAN match while this side may not act: before the match is built, and from Ready until the battle.</summary>
        public bool ActionsLocked => Lan != null && (Lan.Coordinator == null || (Match != null && Match.Phase == MatchPhase.Deployment && !Lan.Coordinator.LocalCanAct));

        private ActionResult LockedResult() => ActionResult.Fail(Lan != null && Lan.Coordinator != null && Lan.Coordinator.LocalReady ? "You are ready. Waiting for the other player." : "Waiting for the other player.");

        /// <summary>The matchup table the AI uses (null without one), for an AI that plays a LAN test run's side.</summary>
        public MatchupTable Matchups => _matchups;

        /// <summary>The battle has started on this machine (LAN: once both games agree): shows it and plays it.</summary>
        public void ShowStartedBattle(BattleSimulation battle)
        {
            if (battle == null) return;
            CancelPlacement();
            CancelSpellPlacement();
            CancelItemPlacement();
            _drops.Clear();
            Message = "";
            _views.GhostTeam = null;
            _selection.ClearTower();
            _log?.BattleStarted(Match);
            _battle.ShowBattle(battle, _sources, play: true);
            _pictureInFrames = 2;
            PhaseChanged?.Invoke();
        }

        /// <summary>LAN: the round's result is in (the host's own, or the host's on the guest): the result screen shows.</summary>
        public void OnLanRoundFinished(RoundSummary summary)
        {
            _landedEnding = null; // The bases now hold the round's result.
            _selection.ClearTower();
            PhaseChanged?.Invoke();
        }

        /// <summary>Shows the local side's deployment again after the Match changed outside this script (the LAN test run's AI).</summary>
        public void RefreshView()
        {
            if (Match == null) return;
            RefreshPreview();
            PhaseChanged?.Invoke();
        }

        /// <summary>Sets the local side everywhere the scene shows it: the views' colours, the deployment squares and the camera's home.</summary>
        private void ApplyLocalTeam(Team team)
        {
            LocalTeam = team;
            if (_views != null) _views.LocalTeam = team;
            BattlefieldView field = GetComponent<BattlefieldView>();
            if (field != null) field.LocalTeam = team;
            BattleCameraController view = FindAnyObjectByType<BattleCameraController>();
            if (view != null && view.HomeTeam != team) view.ViewFrom(team); // Single-player never moves the camera here.
        }

        /// <summary>
        /// The real factions, built from this scene's faction assets, for a match built elsewhere (LanMatchFactory.Build). Their units and cards are
        /// the objects the views and icons know, so call it before building the match that UseMatch gets.
        /// </summary>
        public List<FactionInfo> PrepareFactions()
        {
            var cache = new Dictionary<UnitDefinition, UnitStats>();
            _cardSources.Clear();
            List<FactionInfo> infos = BuildFactions(cache, _cardSources);
            _sources.Clear();
            foreach (var pair in cache) _sources[pair.Value] = pair.Key;
            _realFactions = infos;
            return infos;
        }

        /// <summary>
        /// A new copy of the real factions from this scene's faction assets, without touching what the views know (for the other game's
        /// side of a LAN test, which builds its own match from the same content).
        /// </summary>
        public List<FactionInfo> BuildFactions() => BuildFactions(new Dictionary<UnitDefinition, UnitStats>(), new Dictionary<CardStats, CardDefinition>());

        private List<FactionInfo> BuildFactions(Dictionary<UnitDefinition, UnitStats> cache, Dictionary<CardStats, CardDefinition> cardSources)
        {
            var infos = new List<FactionInfo>();
            foreach (FactionDefinition faction in factions)
            {
                if (faction != null) infos.Add(faction.ToInfo(cache, cardSources));
            }
            return infos;
        }

        /// <summary>A fresh copy of this scene's match rules (a setup writes into it), for a match built elsewhere.</summary>
        public MatchRules FreshRules() => matchRules.ToRules();

        /// <summary>The battle rules of this scene, for a match built elsewhere.</summary>
        public CombatRules CombatRules => (_battle != null ? _battle : GetComponent<BattleController>()).Rules;

        /// <summary>
        /// Shows a match built elsewhere, played by localTeam (M11 LAN: the host is Team A, the guest Team B). Build it with PrepareFactions,
        /// FreshRules and CombatRules. No battle log is written and no AI is set. "New match" or NewMatch goes back to a single-player match as Team A.
        /// </summary>
        public void UseMatch(Match match, Team localTeam)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (Tutorial != null)
            {
                Destroy(Tutorial);
                Tutorial = null;
            }
            Ending = null;
            _landedEnding = null;
            CloseBattleLog();
            ForgetBattlePictures();
            Match = match;
            ApplyLocalTeam(localTeam);
            CancelPlacement();
            CancelSpellPlacement();
            CancelItemPlacement();
            _selection?.ClearTower();
            _drops.Clear();
            Message = "";
            _views.GhostTeam = null;
            _draftDone = false;
            _battle.ShowBattle(null, null, play: false);
            if (Match.Phase != MatchPhase.Draft) FinishDraft();
            PhaseChanged?.Invoke();
        }

        /// <summary>
        /// Call after the other side's commands were applied to the Match (M11 LAN): when they ended the draft, the field is set up for the armies.
        /// Nothing the other side does during deployment shows here (its army stays hidden until the battle).
        /// </summary>
        public void OnRemoteCommandsApplied()
        {
            if (Match == null) return;
            if (!_draftDone && Match.Phase != MatchPhase.Draft) FinishDraft();
            PhaseChanged?.Invoke();
        }

        /// <summary>Both starting cards are in: the field shows the factions' lands and the local side's army.</summary>
        private void FinishDraft()
        {
            _draftDone = true;
            if (Match.Rules.Mode == MatchMode.UnlockSkirmish) BattleFactions.SetSkirmish(SkirmishLand(Match.Seed), RealFactionOfEachUnit());
            else BattleFactions.Set(Match.Player.Faction?.Name, Match.Enemy.Faction?.Name); // Canonical: Team A's land is in the south on both screens.
            RefreshPreview();
        }

        private void Start()
        {
            _battle = GetComponent<BattleController>();
            _selection = GetComponent<SquadSelection>();
            _views = GetComponent<UnitViewManager>();
            _views.LevelUpReady = ShowsLevelUpArrow;
            _spellView = GetComponent<SpellView>();
            if (_spellView == null) _spellView = gameObject.AddComponent<SpellView>();
            if (GetComponent<TopSquadGlow>() == null) gameObject.AddComponent<TopSquadGlow>();
            _battle.BattleFinished += OnBattleFinished;
            MatchSetup setup = GameFlow.TakeMatchSetup();
            string[] args = Environment.GetCommandLineArgs();
            if (setup == null && Array.IndexOf(args, "-autoplaySkirmish") >= 0) setup = new MatchSetup { Mode = MatchMode.UnlockSkirmish }; // For pictures of it, from the test game.
            int health = Array.IndexOf(args, "-autoplayBaseHealth");
            if (health >= 0 && health + 1 < args.Length && int.TryParse(args[health + 1], out int baseHealth)) // A short game, for pictures of its end.
            {
                if (setup == null) setup = new MatchSetup();
                setup.BaseHealth = baseHealth;
            }
            LanLaunch lan = GameFlow.TakeLanMatch();
            if (lan != null)
            {
                // M11: an ordinary match stands in for the moment it takes to build the LAN match; the HUD shows "Starting" meanwhile.
                Lan = gameObject.AddComponent<LanMatch>();
                Lan.Launch = lan;
                NewMatch();
                return;
            }
            if (GameFlow.TakeTutorial()) StartTutorial();
            else NewMatch(setup: setup);
            if (MatchReplay.Requested(args) && gameObject.GetComponent<MatchReplay>() == null && !gameObject.AddComponent<MatchReplay>().ReadCommandLine(args)) Message = "The battle log to replay can't be read."; // D106
        }

        // ------------------------------------------------------------------ the tutorial (D77)

        /// <summary>The tutorial running in this match, or null.</summary>
        public TutorialDirector Tutorial { get; private set; }

        /// <summary>The scene's selection (the squad or tower selected).</summary>
        public SquadSelection Selection => _selection;

        /// <summary>A fixed match with the tutorial's steps (TutorialSetup): the same seed, start and enemy for everyone.</summary>
        public void StartTutorial()
        {
            NewMatch(TutorialSetup.Seed, TutorialSetup.Setup(), TutorialSetup.Enemy(), tutorial: true);
            TutorialSetup.FixDrafts(Match, _realFactions);
            Tutorial = gameObject.AddComponent<TutorialDirector>();
            Tutorial.Begin(this);
        }

        private void OnDestroy()
        {
            if (_battle != null) _battle.BattleFinished -= OnBattleFinished;
            CloseBattleLog();
            ForgetBattlePictures();
        }

        // ------------------------------------------------------------------ battle stats (D69)

        private readonly List<Texture2D> _battlePictures = new List<Texture2D>();
        private int _pictureInFrames = -1;

        /// <summary>The picture taken as battle number index of this match started (Match.BattleHistory's order), or null if there is none.</summary>
        public Texture2D BattlePictureOf(int index) => index >= 0 && index < _battlePictures.Count ? _battlePictures[index] : null;

        /// <summary>The pictures go with the match: a new game or leaving the scene drops them.</summary>
        private void ForgetBattlePictures()
        {
            foreach (Texture2D picture in _battlePictures)
            {
                if (picture != null) Destroy(picture);
            }
            _battlePictures.Clear();
            _pictureInFrames = -1;
        }

        /// <summary>The battle's bodies are shown and in place a frame or two after it starts, and have hardly moved yet.</summary>
        private void LateUpdate()
        {
            if (_pictureInFrames < 0 || --_pictureInFrames > 0) return;
            _pictureInFrames = -1;
            if (Match == null || Match.BattleHistory.Count == 0) return;
            while (_battlePictures.Count < Match.BattleHistory.Count - 1) _battlePictures.Add(null);
            if (_battlePictures.Count >= Match.BattleHistory.Count) return;
            BattleStats last = Match.BattleHistory[Match.BattleHistory.Count - 1];
            _battlePictures.Add(BattlePicture.Take(last.FieldWidth, last.FieldLength)); // The flank zones beside the field too.
        }

        /// <summary>The picture of a card's spell, or null.</summary>
        public Texture2D IconFor(SpellStats spell)
        {
            foreach (var pair in _cardSources)
            {
                if (spell != null && pair.Key.Spell != null && (pair.Key.Spell == spell || pair.Key.Spell.Name == spell.Name)) return pair.Value.icon; // A spell in play may be a copy of its card's.
            }
            return null;
        }

        // ------------------------------------------------------------------ the battle log (D68)

        private MatchLog _log;
        private BattleLogFile _logFile;

        /// <summary>A game's log starts when its starting card is chosen, so that a look at the draft leaves no file behind.</summary>
        private void OpenBattleLog(int cardChosen)
        {
            CloseBattleLog();
            string folder = BattleLogFile.FolderForThisRun();
            if (folder == null) return;
            DateTime now = DateTime.Now;
            bool skirmish = Match.Rules.Mode == MatchMode.UnlockSkirmish; // Both sides are the same made-up faction there, so its names say nothing.
            _logFile = new BattleLogFile(folder, BattleLogFile.FileName(now, Match.Rules.Mode.ToString(), skirmish ? null : Match.Player.SubFaction?.Name, skirmish ? null : Match.Enemy.SubFaction?.Name));
            _log = new MatchLog(_logFile.Write);
            _log.Started(Match, Difficulty.ToString(), now, cardChosen, Application.version, UnitStatsMods.LogLabel);
        }

        private void CloseBattleLog()
        {
            _logFile?.Dispose();
            _logFile = null;
            _log = null;
        }

        private void LogAnyOffer()
        {
            if (Match.Phase == MatchPhase.Cards) _log?.CardsOffered(Match);
        }

        private static string Label(PlacedSquad squad) => MatchLog.SquadLabel(squad);

        /// <summary>In Unlock Skirmish every unit is in the roster, so only those unlocked say anything about this side.</summary>
        private bool HasUnitsThatCanFlank()
        {
            if (Match.Rules.Mode != MatchMode.UnlockSkirmish) return Me.Roster.Exists(unit => unit.CanFlank);
            foreach (UnitStats unit in Me.Unlocked)
            {
                if (unit.CanFlank) return true;
            }
            return false;
        }

        /// <summary>
        /// Starts a fresh match at the draft. fixedSeed overrides the Inspector seed (tests). A setup is remembered for the
        /// matches after this one; without one, the last one is played again.
        /// </summary>
        public void NewMatch(int? fixedSeed = null, MatchSetup setup = null, IMatchAi enemy = null, bool tutorial = false, AiDifficulty? difficulty = null)
        {
            if (!tutorial && Tutorial != null)
            {
                // A new match after the tutorial is an ordinary one, with the settings it had before.
                Destroy(Tutorial);
                Tutorial = null;
            }
            Ending = null;
            _landedEnding = null;
            CloseBattleLog();
            ForgetBattlePictures();
            if (setup != null && !tutorial) _setup = setup; // The tutorial's own setup is not kept for the next match.
            MatchSetup applied = tutorial ? setup : _setup;
            List<FactionInfo> infos = PrepareFactions();

            int matchSeed = fixedSeed ?? (seed != 0 ? seed : Environment.TickCount & 0x7FFFFFFF);
            Difficulty = difficulty ?? GameSettings.Difficulty; // A replay's own difficulty (D106) is not saved as the player's.
            _matchups = matchupTable != null ? matchupTable.ToTable() : null;
            MatchRules rules = matchRules.ToRules();
            applied?.ApplyTo(rules);
            IReadOnlyList<FactionInfo> offered = infos;
            if (rules.Mode == MatchMode.UnlockSkirmish) offered = new[] { SkirmishFaction.Build(infos) };
            Match = new Match(rules, _battle.Rules, offered, matchSeed, enemy ?? new RuleBasedAi(Difficulty, matchSeed, _matchups));
            ApplyLocalTeam(Team.A); // Single-player: the player is Team A.
            _draftDone = false;
            CancelPlacement();
            CancelSpellPlacement();
            CancelItemPlacement();
            _selection?.ClearTower();
            _drops.Clear();
            Message = "";
            _views.GhostTeam = null;
            _battle.ShowBattle(null, null, play: false);
            PhaseChanged?.Invoke();
        }

        // ------------------------------------------------------------------ draft

        /// <summary>The UI theme of the player's faction (null before the draft or if the faction has none).</summary>
        public UiThemeDefinition PlayerTheme
        {
            get
            {
                if (Me?.Faction == null) return null;
                foreach (FactionDefinition faction in factions)
                {
                    if (faction != null && (faction.factionName == Me.Faction.Name || faction.name == Me.Faction.Name)) return faction.uiTheme;
                }
                return null;
            }
        }

        /// <summary>Sets the AI's difficulty for this match (only before a draft card is chosen).</summary>
        public void SetDifficulty(AiDifficulty difficulty)
        {
            if (Match == null || Match.Phase != MatchPhase.Draft) return;
            if (!Match.SetEnemyAi(new RuleBasedAi(difficulty, Match.Seed, _matchups))) return;
            Difficulty = difficulty;
            GameSettings.Difficulty = difficulty;
        }

        /// <summary>The local side's starting card. In a two-humans match the round begins once the other side's pick is in too (OnRemoteCommandsApplied).</summary>
        public ActionResult ChooseDraft(int cardIndex)
        {
            ActionResult result = LanRuns ? Lan.Coordinator.ChooseDraft(cardIndex) // LAN: picked here and sent
                : Match.IsTwoHumans ? Match.ChooseDraft(LocalTeam, cardIndex) : Match.ChooseDraft(cardIndex);
            Report(result);
            if (result.Ok)
            {
                if (!Match.IsTwoHumans) OpenBattleLog(cardIndex);
                if (Match.Phase != MatchPhase.Draft) FinishDraft();
                PhaseChanged?.Invoke();
            }
            return result;
        }

        private static string SkirmishLand(int matchSeed)
        {
            int count = SkirmishLands.Length;
            return SkirmishLands[(matchSeed % count + count) % count];
        }

        /// <summary>Each unit's real faction by name: everything in a roster, and what a faction's spells drop (ODST, which is in no roster).</summary>
        private Dictionary<string, string> RealFactionOfEachUnit()
        {
            var map = new Dictionary<string, string>();
            foreach (FactionInfo faction in _realFactions)
            {
                foreach (UnitStats unit in faction.BaseRoster) map.TryAdd(unit.Name, faction.Name);
                foreach (SubFactionInfo subFaction in faction.SubFactions)
                {
                    foreach (UnitStats unit in subFaction.ExclusiveUnits) map.TryAdd(unit.Name, faction.Name);
                }
            }
            foreach (FactionInfo faction in _realFactions)
            {
                foreach (CardStats card in faction.Cards)
                {
                    if (card?.Spell?.DropUnit != null) map.TryAdd(card.Spell.DropUnit.Name, faction.Name);
                }
            }
            return map;
        }

        // ------------------------------------------------------------------ deployment

        public ActionResult Unlock(UnitStats type) => Report(ActionsLocked ? LockedResult() : Match.Unlock(LocalTeam, type), type?.Name);

        /// <summary>
        /// Buys a squad of this type: it is placed at the free spot nearest the middle of the zone,
        /// drops in from high above and is selected when it lands.
        /// </summary>
        public ActionResult Buy(UnitStats type)
        {
            if (Match.Phase != MatchPhase.Deployment) return ActionResult.Fail("Squads can only be bought during deployment.");
            CancelPlacement();
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.Buy(LocalTeam, type), type?.Name);
            if (result.Ok)
            {
                _drops[Me.Squads[Me.Squads.Count - 1].Id] = Time.unscaledTime;
                RefreshPreview();
            }
            return result;
        }

        /// <summary>True while a bought squad is still falling into place.</summary>
        public bool IsDropping => _drops.Count > 0;

        private void Update()
        {
            if (Ending != null) TickEnding();
            if (_drops.Count == 0) return;
            // Select each squad as it lands (the last one to land stays selected).
            float duration = _views.DropDuration;
            int landed = -1;
            foreach (var pair in _drops)
            {
                if (Time.unscaledTime - pair.Value >= duration) landed = pair.Key;
            }
            if (landed < 0) return;
            _drops.Remove(landed);
            if (Match.Phase == MatchPhase.Deployment && !IsPlacing) SelectPlaced(landed);
        }

        /// <summary>Starts moving one of this round's squads; the next PlaceAt moves it there.</summary>
        public void BeginMoving(PlacedSquad squad)
        {
            if (Match.Phase != MatchPhase.Deployment || squad == null || ActionsLocked) return;
            if (!Match.CanMove(squad))
            {
                Message = "Only squads bought this round can be moved or turned.";
                return;
            }
            CancelPlacement();
            CancelSpellPlacement();
            CancelItemPlacement();
            MovingSquadId = squad.Id;
            PlacingRotated = squad.Rotated90;
            _selection.InputEnabled = false;
            Message = "Left-click the new spot, R to turn, right-click to cancel.";
        }

        public void CancelPlacement()
        {
            MovingSquadId = -1;
            PlacingRotated = false;
            if (_selection != null) _selection.InputEnabled = true;
        }

        /// <summary>Turns the squad being moved, or turns the selected squad where it stands.</summary>
        public void ToggleRotation()
        {
            if (IsPlacing)
            {
                PlacingRotated = !PlacingRotated;
                ActionTaken?.Invoke(nameof(ToggleRotation), ActionResult.Success()); // Not through Report: the placing message stays.
                return;
            }
            PlacedSquad squad = SelectedPlacedSquad;
            if (squad == null) return;
            bool turned = !squad.Rotated90;
            if (Report(ActionsLocked ? LockedResult() : Match.Move(LocalTeam, squad.Id, squad.Column, squad.Row, turned), MatchLog.TurnDetail(squad, turned)).Ok) RefreshPreview(squad.Id);
        }

        /// <summary>The type of the squad being moved (for the ghost), or null.</summary>
        public UnitStats PlacementType => Me.FindSquad(MovingSquadId)?.Type;

        /// <summary>The squad being moved, or null.</summary>
        public PlacedSquad MovingSquad => MovingSquadId >= 0 && Match != null ? Me.FindSquad(MovingSquadId) : null;

        /// <summary>The preview's squad of the squad being moved, or -1 (D109).</summary>
        public int MovingSimSquadId => SimSquadOf(MovingSquad);

        private int SimSquadOf(PlacedSquad squad)
        {
            BattleSimulation sim = _battle.Simulation;
            int index = squad != null ? _previewOrder.IndexOf(squad) : -1;
            return index >= 0 && sim != null && index < sim.Squads.Count ? sim.Squads[index].Id : -1;
        }

        /// <summary>Moves the squad being moved to this spot (column and row in the player's grid).</summary>
        public ActionResult PlaceAt(int column, int row)
        {
            if (MovingSquadId < 0) return ActionResult.Fail("Nothing to place.");
            int placedId = MovingSquadId;
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.Move(LocalTeam, placedId, column, row, PlacingRotated), MatchLog.MoveDetail(Me.FindSquad(placedId), column, row, PlacingRotated));
            if (result.Ok)
            {
                CancelPlacement();
                _drops.Remove(placedId);
                RefreshPreview(placedId);
            }
            return result;
        }

        /// <summary>True if the squad being moved could go here (for the ghost's color).</summary>
        public bool CanPlaceAt(int column, int row)
        {
            UnitStats type = PlacementType;
            return type != null && DeploymentGrid.CanPlace(Me, type, column, row, PlacingRotated, Match.CombatRules, MovingSquadId);
        }

        /// <summary>True if a deployment action this round can be taken back (not the card choice).</summary>
        public bool CanUndo => Match != null && Match.CanUndoFor(LocalTeam); // Team A against the AI: Match.CanUndo.

        /// <summary>Takes back the last deployment action this round (the owner, 2026-11-06): buys, sells, moves, levels, techs, towers, spells, items.</summary>
        public ActionResult Undo()
        {
            if (Match == null) return ActionResult.Fail("There is no match.");
            CancelPlacement();
            CancelSpellPlacement();
            CancelItemPlacement();
            ActionResult result = ActionsLocked ? LockedResult() : Match.Undo(LocalTeam); // Team A against the AI: Match.Undo().
            Message = result.Message ?? "";
            _log?.PlayerAction(Match, nameof(Undo), null, result);
            ActionTaken?.Invoke(nameof(Undo), result);
            if (result.Ok)
            {
                foreach (int id in new List<int>(_drops.Keys)) if (Me.FindSquad(id) == null) _drops.Remove(id);
                RefreshPreview();
            }
            return result;
        }

        /// <summary>In a match, taking a squad off the field means selling it.</summary>
        public ActionResult Remove(PlacedSquad squad) => Sell(squad);

        public ActionResult Sell(PlacedSquad squad)
        {
            if (squad == null) return ActionResult.Fail("Select a squad first.");
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.Sell(LocalTeam, squad.Id), Label(squad));
            if (result.Ok)
            {
                _drops.Remove(squad.Id);
                RefreshPreview();
            }
            return result;
        }

        // ------------------------------------------------------------------ leveling and techs (M6)

        public ActionResult LevelUp(PlacedSquad squad)
        {
            if (squad == null) return ActionResult.Fail("Select a squad first.");
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.LevelUp(LocalTeam, squad.Id), Label(squad));
            if (result.Ok) RefreshPreview(squad.Id);
            return result;
        }

        /// <summary>Levels up every ready squad of this type at once (Match.LevelUpAll).</summary>
        public ActionResult LevelUpAll(UnitStats type, int reselectSquadId = -1)
        {
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.LevelUpAll(LocalTeam, type), type?.Name);
            if (result.Ok) RefreshPreview(reselectSquadId);
            return result;
        }

        public ActionResult BuyTech(UnitStats type, TechUpgrade tech, int reselectSquadId = -1)
        {
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.BuyTech(LocalTeam, type, tech), MatchLog.TechDetail(type, tech));
            if (result.Ok) RefreshPreview(reselectSquadId);
            return result;
        }

        // ------------------------------------------------------------------ towers (D55)

        public ActionResult LevelUpTower(TowerKind kind)
        {
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.LevelUpTower(LocalTeam, kind), kind.ToString());
            if (result.Ok) RefreshPreview();
            return result;
        }

        public ActionResult BuyExtraSlot() => Report(ActionsLocked ? LockedResult() : Match.BuyExtraSlot(LocalTeam));
        public ActionResult BuyOilZone() => Report(ActionsLocked ? LockedResult() : Match.BuyOilZone(LocalTeam));
        public ActionResult RapidResupply() => Report(ActionsLocked ? LockedResult() : Match.RapidResupply(LocalTeam));
        public ActionResult BuyEliteRecruitment() => Report(ActionsLocked ? LockedResult() : Match.BuyEliteRecruitment(LocalTeam));
        public ActionResult BuyDeploymentModule() => Report(ActionsLocked ? LockedResult() : Match.BuyDeploymentModule(LocalTeam));

        public ActionResult BuyResearch(bool health)
        {
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.BuyResearch(LocalTeam, health), health ? "health" : "damage");
            if (result.Ok) RefreshPreview();
            return result;
        }

        /// <summary>What the window of a tower says. The other side's show only what the local side has seen; only the local side's own sell anything, and only during deployment.</summary>
        public TowerPanel DescribeTower(Team team, TowerKind kind)
        {
            PlayerState owner = Match.Get(team);
            PlacedTower tower = owner.FindTower(kind);
            if (tower == null) return null;
            MatchRules rules = Match.Rules;
            bool own = team == LocalTeam;
            int level = ShownTowerLevel(Match, LocalTeam, team, kind);
            UnitStats stats = Match.TowerStats(kind, level);
            var panel = new TowerPanel { Title = Match.TowerName(kind), Side = own ? "Your tower" : "Enemy tower, as last seen" };
            panel.Lines.Add($"Level {level} of {rules.TowerMaxLevel}    Health {stats.MaxHealth:0}");
            panel.Lines.Add($"If it falls, {(own ? "your" : "their")} whole army loses {(1f - rules.ShockwaveSpeedMultiplier) * 100f:0}% speed, {(1f - rules.ShockwaveDamageMultiplier) * 100f:0}% damage and {(1f - rules.ShockwaveFireRateMultiplier) * 100f:0}% rate of fire, and takes {(rules.ShockwaveDamageTakenMultiplier - 1f) * 100f:0}% more damage, for {stats.Shockwave.Seconds:0} s. Two falling add up their time, not their severity.");
            float killShare = Math.Max(0f, Math.Min(1f, rules.KillXpShare)); // As Match.SplitKillXp and PayDamageXp clamp it (xp_split, D177).
            float towerXp = rules.TowerXpByLevel[level - 1];
            panel.Lines.Add(killShare >= 1f ? $"Whoever destroys it earns {towerXp:0} XP."
                : rules.DamageXpAsItLands ? $"It is worth {towerXp:0} XP: {killShare * 100f:0}% to the squad that lands the last hit, the rest to the squads that damage it, as the damage lands."
                : $"Destroying it earns {towerXp:0} XP: {killShare * 100f:0}% to the squad that lands the last hit, the rest shared by the squads that damaged it, by the damage each dealt.");
            if (!own) return panel;

            bool deploying = IsDeploying; // Not while the local side's own card offer is open (two humans), as in the Cards phase against the AI.
            int credits = owner.Credits;
            if (level < rules.TowerMaxLevel)
            {
                UnitStats next = Match.TowerStats(kind, level + 1);
                panel.Buttons.Add(new TowerButton
                {
                    Label = $"Level up ({rules.TowerLevelUpCost})",
                    Note = $"Health {next.MaxHealth:0}, shockwave {next.Shockwave.Seconds:0} s",
                    Enabled = deploying && credits >= rules.TowerLevelUpCost,
                    OnPress = () => LevelUpTower(kind),
                });
            }
            if (kind == TowerKind.CommandCenter)
            {
                panel.Buttons.Add(new TowerButton
                {
                    Label = $"Extra slot ({rules.ExtraSlotCost})", Note = owner.BoughtExtraSlot ? "Bought this round" : "One more squad this round",
                    Enabled = deploying && !owner.BoughtExtraSlot && credits >= rules.ExtraSlotCost, OnPress = () => BuyExtraSlot(),
                });
                if (rules.OilZone != null)
                {
                    // Bought once a match, then kept in the player's spells and recharging after each use.
                    OwnedSpell oil = owner.Spells.Find(s => s.Stats == rules.OilZone);
                    string oilLasts = rules.OilZone.DurationSeconds > 0f ? $"{rules.OilZone.DurationSeconds:0} s" : "the whole round";
                    int oilRecharge = rules.OilZone.CooldownRounds;
                    string oilNote = oil == null
                        ? $"{rules.OilZone.AreaDepth:0} x {rules.OilZone.AreaWidth:0} m for {oilLasts}; kept in your spells, {oilRecharge} round{(oilRecharge == 1 ? "" : "s")} to recharge"
                        : oil.IsReady(Match.Round) ? "In your spells: ready" : $"In your spells: recharging, {oil.RoundsLeft(Match.Round)} round(s)";
                    panel.Buttons.Add(new TowerButton
                    {
                        Label = oil == null ? $"Oil zone ({rules.OilZone.Cost})" : "Oil zone (owned)", Note = oilNote,
                        Enabled = deploying && oil == null && credits >= rules.OilZone.Cost, OnPress = () => BuyOilZone(),
                    });
                }
                panel.Buttons.Add(new TowerButton
                {
                    Label = "Rapid Resupply (free)", Note = owner.UsedRapidResupply ? "Used this round" : $"+{rules.RapidResupplyCredits} now, {rules.RapidResupplyPenalty} less next round",
                    Enabled = deploying && !owner.UsedRapidResupply, OnPress = () => RapidResupply(),
                });
                panel.Buttons.Add(new TowerButton
                {
                    Label = $"Elite Recruitment ({rules.EliteRecruitmentCost})", Note = owner.EliteRecruitment ? "On: new squads are level 2" : "New squads this round are level 2, for half their price more",
                    Enabled = deploying && !owner.EliteRecruitment && credits >= rules.EliteRecruitmentCost, OnPress = () => BuyEliteRecruitment(),
                });
                return panel;
            }

            AddResearch(panel, "Health", rules.ResearchHealthShare, owner.ResearchHealthSteps, true, deploying, credits);
            AddResearch(panel, "Damage", rules.ResearchDamageShare, owner.ResearchDamageSteps, false, deploying, credits);
            ItemStats module = Match.DeploymentModuleOf(owner);
            if (module != null)
            {
                int owned = owner.Inventory.FindAll(i => i == module).Count;
                panel.Buttons.Add(new TowerButton
                {
                    Label = $"{module.Name} ({rules.DeploymentModuleCost})",
                    Note = (owner.BoughtDeploymentModule ? "Bought this round" : "An item: its squad can move in every deployment phase; one a round")
                        + (owned > 0 ? $" ({owned} in your inventory)" : ""),
                    Enabled = deploying && !owner.BoughtDeploymentModule && credits >= rules.DeploymentModuleCost, OnPress = () => BuyDeploymentModule(),
                });
            }
            return panel;
        }

        /// <summary>
        /// The level a tower's window shows to the local side: its own towers' real level, the other side's as last seen at a battle start (1 before the
        /// first). For local Team A that is Match.SeenEnemyTowerLevel.
        /// </summary>
        public static int ShownTowerLevel(Match match, Team local, Team owner, TowerKind kind)
        {
            if (owner == local) return match.Get(owner).FindTower(kind)?.Level ?? 1;
            return match.SeenTowerLevel(local, kind);
        }

        private void AddResearch(TowerPanel panel, string name, float share, int steps, bool health, bool deploying, int credits)
        {
            int[] costs = Match.Rules.ResearchCosts;
            string now = steps > 0 ? $"Now +{share * steps * 100f:0}%" : "Your whole army, for the rest of the match";
            if (steps >= costs.Length)
            {
                panel.Buttons.Add(new TowerButton { Label = $"{name}: complete", Note = now, Enabled = false });
                return;
            }
            int cost = costs[steps];
            panel.Buttons.Add(new TowerButton
            {
                Label = $"{name} +{share * 100f:0}% ({cost})", Note = now,
                Enabled = deploying && credits >= cost, OnPress = () => BuyResearch(health),
            });
        }

        /// <summary>The placed squad behind a simulation squad (preview: the local side's; battle: Team A then Team B), or null.</summary>
        public PlacedSquad PlacedFor(int simSquadId, out Team team)
        {
            team = LocalTeam;
            BattleSimulation sim = _battle.Simulation;
            if (Match == null || sim == null) return null;
            int index = -1;
            for (int i = 0; i < sim.Squads.Count; i++) if (sim.Squads[i].Id == simSquadId) { index = i; break; }
            if (index < 0) return null;
            if (Match.Phase == MatchPhase.Deployment) return IsDeploying && index < _previewOrder.Count ? _previewOrder[index] : null; // Behind the local side's open card offer: nothing, as in the Cards phase.
            if (sim != Match.CurrentBattle) return null;
            team = Team.A; // The battle's order is canonical: Team A's squads first.
            if (index < Match.Player.Squads.Count) return Match.Player.Squads[index];
            team = Team.B;
            index -= Match.Player.Squads.Count;
            return index < Match.Enemy.Squads.Count ? Match.Enemy.Squads[index] : null;
        }

        private static string ItemText(ItemStats item) => item == null ? null : $"Item: {item.Name} ({item.EffectsText()})";

        /// <summary>The enemy ghost behind a preview squad (deployment only), or null.</summary>
        public SeenSquad GhostFor(int simSquadId)
        {
            BattleSimulation sim = _battle.Simulation;
            if (Match == null || sim == null || Match.Phase != MatchPhase.Deployment) return null;
            for (int i = 0; i < sim.Squads.Count; i++)
            {
                if (sim.Squads[i].Id != simSquadId) continue;
                int ghost = i - _previewOrder.Count;
                return ghost >= 0 && ghost < _ghostOrder.Count ? _ghostOrder[ghost] : null;
            }
            return null;
        }

        /// <summary>The preview squad showing this placed squad of the player's (deployment and the card screen), or -1.</summary>
        public int PreviewSquadOfPlaced(int placedSquadId)
        {
            BattleSimulation sim = _battle != null ? _battle.Simulation : null;
            if (Match == null || sim == null || placedSquadId < 0 || sim == Match.CurrentBattle) return -1;
            for (int i = 0; i < _previewOrder.Count && i < sim.Squads.Count; i++) if (_previewOrder[i].Id == placedSquadId) return sim.Squads[i].Id;
            return -1;
        }

        /// <summary>The preview squad showing this ghost of the enemy's last-battle army (D25), or -1.</summary>
        public int PreviewSquadOfGhost(SeenSquad ghost)
        {
            BattleSimulation sim = _battle != null ? _battle.Simulation : null;
            if (Match == null || sim == null || ghost == null || sim == Match.CurrentBattle) return -1;
            int j = _ghostOrder.IndexOf(ghost), index = _previewOrder.Count + j;
            return j >= 0 && index < sim.Squads.Count ? sim.Squads[index].Id : -1;
        }

        /// <summary>Decision D26: the player's squads that are ready to level up show an arrow (deployment and result).</summary>
        private bool ShowsLevelUpArrow(int simSquadId)
        {
            if (Match == null || (!IsDeploying && Match.Phase != MatchPhase.Result)) return false;
            PlacedSquad squad = PlacedFor(simSquadId, out Team team);
            return team == LocalTeam && Match.CanLevelUp(squad);
        }

        public SquadProgressView DescribeProgress(int simSquadId)
        {
            SeenSquad ghost = GhostFor(simSquadId);
            if (ghost != null)
            {
                // What the player saw of this enemy squad when the last battle started (D25), or before the first, of its starting squads (D58).
                bool first = Match.NoBattleYet;
                var seen = new SquadProgressView
                {
                    SideLabel = first ? "enemy, starting squad" : "enemy, last battle", Level = ghost.Level,
                    Note = first ? "Where it will stand is hidden." : "As seen at the start of the last battle.", ItemText = ItemText(ghost.Item),
                };
                foreach (TechUpgrade tech in ghost.Techs) seen.Techs.Add(new TechLine { Tech = tech, Owned = true });
                return seen;
            }

            PlacedSquad squad = PlacedFor(simSquadId, out Team team);
            if (squad == null) return null;
            var view = new SquadProgressView { Level = squad.Level, ItemText = ItemText(squad.Item) };
            if (team != LocalTeam)
            {
                // The enemy in battle: its level and the techs it fights with, nothing else.
                foreach (TechUpgrade tech in Match.Get(team).TechsFor(squad.Type)) view.Techs.Add(new TechLine { Tech = tech, Owned = true });
                return view;
            }

            bool deploying = IsDeploying;
            PlayerState player = Me;
            view.ShowXp = squad.Level < UnitUpgrades.MaxLevel;
            view.Xp = Match.XpSoFar(squad); // During a battle the bar fills as the squad kills (D136); the window asks again 5 times a second.
            view.XpNeeded = Match.XpNeeded(squad);
            if (squad.Level < UnitUpgrades.MaxLevel)
            {
                view.LevelUpLabel = $"Level up ({Match.LevelUpCost(squad)})";
                view.LevelUpEnabled = deploying && Match.CanLevelUp(squad) && player.Credits >= Match.LevelUpCost(squad);
                view.OnLevelUp = () => LevelUp(squad);
                bool fullNow = view.Xp >= view.XpNeeded - 1e-3f;
                view.Note = Match.CanLevelUp(squad) ? "XP bar full: ready to level up."
                    : fullNow ? "XP bar full: it can level up after the battle."
                    : "Earns XP from kills and damage.";
            }
            else view.Note = "Maximum level.";

            // Whether or not this squad is one of them: any squad of the type reaches the others.
            int ready = Match.SquadsReadyToLevel(LocalTeam, squad.Type, out int readyPrice);
            if (ready >= 2)
            {
                view.LevelAllLabel = $"Level all {ready} ({readyPrice})";
                view.LevelAllEnabled = deploying && player.Credits >= readyPrice;
                view.OnLevelAll = () => LevelUpAll(squad.Type, squad.Id);
            }

            foreach (TechUpgrade tech in squad.Type.TechUpgrades ?? Array.Empty<TechUpgrade>())
            {
                if (tech == null) continue;
                bool owned = player.HasTech(squad.Type, tech);
                view.Techs.Add(new TechLine
                {
                    Tech = tech,
                    Owned = owned,
                    ButtonLabel = owned ? null : $"Buy {tech.Cost}",
                    ButtonEnabled = deploying && player.Credits >= tech.Cost && player.Unlocked.Contains(squad.Type),
                    OnButton = () => BuyTech(squad.Type, tech, squad.Id),
                });
            }
            return view;
        }

        /// <summary>The player's squad currently selected in the deployment preview, or null.</summary>
        public PlacedSquad SelectedPlacedSquad
        {
            get
            {
                if (Match.Phase != MatchPhase.Deployment || _selection == null || _battle.Simulation == null) return null;
                IReadOnlyList<SimSquad> squads = _battle.Simulation.Squads;
                for (int i = 0; i < squads.Count && i < _previewOrder.Count; i++)
                {
                    if (squads[i].Id == _selection.SelectedSquadId) return _previewOrder[i];
                }
                return null;
            }
        }

        // ------------------------------------------------------------------ cards and spells (Milestone 7)

        /// <summary>True while the card screen is up (start of a round from round 2).</summary>
        public bool IsChoosingCards => Match != null && Match.InCards(LocalTeam); // Against the AI: the Cards phase.

        public ActionResult ChooseCard(int index)
        {
            CardOffer offer = Me.Offer;
            string card = offer != null && index >= 0 && index < offer.Count ? offer.NameOf(index) : null;
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.ChooseCard(LocalTeam, index), card);
            if (result.Ok)
            {
                LogAnyOffer();
                RefreshPreview();
                PhaseChanged?.Invoke();
            }
            return result;
        }

        public ActionResult SkipCards()
        {
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.SkipCards(LocalTeam));
            if (result.Ok)
            {
                LogAnyOffer();
                RefreshPreview();
                PhaseChanged?.Invoke();
            }
            return result;
        }

        /// <summary>The spell being placed (its outline follows the mouse), or -1.</summary>
        public int PlacingSpellId { get; private set; } = -1;
        public OwnedSpell PlacingSpell => Me?.FindSpell(PlacingSpellId);

        /// <summary>Starts placing (or moving) one of the player's ready spells; the next PlaceSpellAt puts it there.</summary>
        public void BeginPlacingSpell(OwnedSpell spell)
        {
            if (Match.Phase != MatchPhase.Deployment || spell == null || ActionsLocked) return;
            if (!spell.IsReady(Match.Round))
            {
                Message = $"{spell.Stats.Name} is recharging ({spell.RoundsLeft(Match.Round)} more round(s)).";
                return;
            }
            CancelPlacement();
            CancelItemPlacement();
            PlacingSpellId = spell.Id;
            PlacingLineStart = null;
            _selection.Select(-1);
            _selection.InputEnabled = false;
            Message = spell.Stats.Offensive
                ? $"{spell.Stats.Name}: left-click anywhere on the field (red), right-click to cancel."
                : $"{spell.Stats.Name}: left-click on your side (blue), right-click to cancel.";
            if (spell.Stats.IsRectangle) Message = $"{spell.Stats.Name}: left-click where one end goes, right-click to cancel.";
        }

        /// <summary>
        /// A line-shaped spell being placed (D35): where its first end was clicked, or null. The other end
        /// then follows the mouse, and a second click confirms.
        /// </summary>
        public Vec2? PlacingLineStart { get; private set; }

        /// <summary>The first click of a line-shaped spell.</summary>
        public void SetLineStart(Vec2 position)
        {
            if (PlacingSpell == null || !PlacingSpell.Stats.IsRectangle) return;
            PlacingLineStart = position;
            Message = $"{PlacingSpell.Stats.Name}: move the mouse to aim the other end, left-click to confirm, right-click to go back.";
        }

        /// <summary>Forgets the first click; the spell is still being placed.</summary>
        public void ClearLineStart()
        {
            if (PlacingSpell == null || PlacingLineStart == null) return;
            PlacingLineStart = null;
            Message = $"{PlacingSpell.Stats.Name}: left-click where one end goes, right-click to cancel.";
        }

        /// <summary>The second click of a line-shaped spell: it runs from the first click toward this point.</summary>
        public ActionResult PlaceLineTowards(Vec2 towards)
        {
            if (PlacingSpell == null || PlacingLineStart == null) return ActionResult.Fail("Click where one end goes first.");
            Vec2 centre = PlacingSpell.Stats.LineFrom(PlacingLineStart.Value, towards, out float angle);
            return PlaceSpellAt(centre, angle);
        }

        public void CancelSpellPlacement()
        {
            if (PlacingSpellId < 0) return;
            PlacingSpellId = -1;
            PlacingLineStart = null;
            if (_selection != null && !IsPlacing) _selection.InputEnabled = true;
        }

        /// <summary>True if the spell being placed could go at this battlefield point (metres).</summary>
        public bool CanPlaceSpellAt(Vec2 position) => PlacingSpell != null && Match.CanPlaceSpell(LocalTeam, PlacingSpell, position);

        public ActionResult PlaceSpellAt(Vec2 position, float angleDegrees = 0f)
        {
            if (PlacingSpellId < 0) return ActionResult.Fail("No spell to place.");
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.PlaceSpell(LocalTeam, PlacingSpellId, position, angleDegrees), PlacingSpell == null ? null : MatchLog.SpellDetail(PlacingSpell.Stats, position, angleDegrees));
            if (result.Ok) CancelSpellPlacement();
            return result;
        }

        /// <summary>Takes a placed spell back; it stays ready.</summary>
        public ActionResult UnplaceSpell(OwnedSpell spell) => spell == null ? ActionResult.Fail("No spell.") : Report(ActionsLocked ? LockedResult() : Match.UnplaceSpell(LocalTeam, spell.Id), spell.Stats.Name);

        // ------------------------------------------------------------------ items and training (D33)

        /// <summary>The item the player picked from the Inventory, waiting for a squad (hexagons show), or null.</summary>
        public ItemStats PlacingItem { get; private set; }

        public void BeginPlacingItem(ItemStats item)
        {
            if (Match.Phase != MatchPhase.Deployment || item == null || !Me.Inventory.Contains(item) || ActionsLocked) return;
            CancelPlacement();
            CancelSpellPlacement();
            PlacingItem = item;
            _selection.InputEnabled = false;
            Message = $"{item.Name}: click the hexagon over a squad, right-click to cancel.";
        }

        public void CancelItemPlacement()
        {
            if (PlacingItem == null) return;
            PlacingItem = null;
            if (_selection != null && !IsPlacing) _selection.InputEnabled = true;
        }

        public ActionResult PlaceItemOn(PlacedSquad squad)
        {
            if (PlacingItem == null || squad == null) return ActionResult.Fail("Pick an item first.");
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.PlaceItem(LocalTeam, PlacingItem, squad.Id), MatchLog.ItemDetail(PlacingItem, squad));
            if (result.Ok)
            {
                int squadId = squad.Id;
                CancelItemPlacement();
                RefreshPreview(squadId); // Its stats changed.
            }
            return result;
        }

        /// <summary>Intensive Training on this squad: its XP bar fills up, and it can then be leveled up.</summary>
        public ActionResult UseTraining(PlacedSquad squad)
        {
            if (squad == null) return Report(ActionResult.Fail("Select a squad first."));
            ActionResult result = Report(ActionsLocked ? LockedResult() : Match.UseTraining(LocalTeam, squad.Id), Label(squad));
            if (result.Ok) RefreshPreview(squad.Id);
            return result;
        }

        /// <summary>The icon of an item or card (Art/UI/Items), or null.</summary>
        public Texture2D IconFor(ItemStats item)
        {
            foreach (var pair in _cardSources)
            {
                if (pair.Key.Item == item) return pair.Value.icon;
            }
            return null;
        }

        public Texture2D IconFor(CardStats card) => card != null && _cardSources.TryGetValue(card, out CardDefinition definition) ? definition.icon : null;

        /// <summary>Screen position (pixels, origin bottom-left) just above one of the player's squads; false if it isn't on screen.</summary>
        public bool SquadOnScreen(PlacedSquad squad, out Vector3 screen)
        {
            screen = Vector3.zero;
            int index = _previewOrder.IndexOf(squad);
            BattleSimulation sim = _battle.Simulation;
            Camera camera = Camera.main;
            if (index < 0 || sim == null || index >= sim.Squads.Count || camera == null) return false;
            if (!_views.SquadTop(sim.Squads[index].Id, out Vector3 world)) return false;
            screen = camera.WorldToScreenPoint(world);
            return screen.z > 0f;
        }

        /// <summary>
        /// D145: during deployment, each shown squad's techs and the screen point just above it (pixels, origin bottom-left): the player's squads
        /// with the techs owned for their type now, the enemy's ghosts with the techs seen at the last battle start (never its current ones).
        /// </summary>
        public void SquadTechs(List<(Vector3 screen, IReadOnlyList<TechUpgrade> techs)> into)
        {
            into.Clear();
            BattleSimulation sim = _battle.Simulation;
            Camera camera = Camera.main;
            if (Match == null || Match.Phase != MatchPhase.Deployment || sim == null || camera == null) return;
            for (int i = 0; i < sim.Squads.Count; i++)
            {
                int ghost = i - _previewOrder.Count;
                IReadOnlyList<TechUpgrade> techs = ghost < 0 ? Me.TechsFor(_previewOrder[i].Type) : ghost < _ghostOrder.Count ? _ghostOrder[ghost].Techs : null;
                if (techs == null || techs.Count == 0 || !_views.SquadTop(sim.Squads[i].Id, out Vector3 world)) continue;
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z > 0f) into.Add((screen, techs));
            }
        }

        // ------------------------------------------------------------------ battle and result

        /// <summary>The player is ready: the round's battle starts (the AI's army is revealed).</summary>
        public void Ready()
        {
            if (LanRuns)
            {
                // LAN: the round's decisions go to the other game; the battle starts once both games agree (LanMatch, ShowStartedBattle).
                ActionResult sent = Lan.Coordinator.Ready();
                Message = sent.Ok ? "" : sent.Message ?? "";
                if (!sent.Ok) return;
                CancelPlacement();
                CancelSpellPlacement();
                CancelItemPlacement();
                _selection.ClearTower();
                PhaseChanged?.Invoke();
                return;
            }
            if (!Match.CanStartBattle) return; // Against the AI: deployment. Two humans: and no card offer open.
            if (Tutorial != null && !Tutorial.ReadyAllowed)
            {
                Message = "First: " + (Tutorial.Current?.Doing ?? "the tutorial's step.");
                return;
            }
            CancelPlacement();
            CancelSpellPlacement();
            CancelItemPlacement();
            _drops.Clear();
            Message = "";
            _views.GhostTeam = null;
            _selection.ClearTower();
            BattleSimulation battle = Match.StartBattle();
            _log?.BattleStarted(Match);
            _battle.ShowBattle(battle, _sources, play: true);
            _pictureInFrames = 2;
            PhaseChanged?.Invoke();
        }

        private void OnBattleFinished(BattleResult result)
        {
            if (Match == null || Match.Phase != MatchPhase.Battle || _battle.Simulation != Match.CurrentBattle) return;
            Ending = new RoundEnding(Match.CurrentBattle, _views.WorldPosition, Match.BattleSeed);
            Ending.Hit += OnEndingHit;
        }

        private void OnEndingHit(RoundEnding.Node node)
        {
            CameraShake.KickEverywhere(0.12f + 0.75f * node.Size * node.Size);
            EndingHit?.Invoke(node);
        }

        /// <summary>The nodes of light have all landed: only now does the round's result come, with its window and its sound.</summary>
        private void TickEnding()
        {
            if (_battle.Clock != null && _battle.Clock.IsPaused) return;
            // At the speeds a player can pick it takes its time, however fast the battle ran. Test runs go faster than those.
            Ending.Tick(Time.deltaTime * (_battle.Speed > FastestPlayerSpeed ? _battle.Speed : 1f));
            if (!Ending.IsDone) return;
            RoundEnding landed = Ending;
            Ending = null;
            if (LanRuns)
            {
                // The guest's bases change only when the host's result comes; until then the bars keep this ending's hits.
                _landedEnding = landed;
                Lan.FinishLocalBattle(); // LAN: the host finishes now; the guest once the host's result is here (OnLanRoundFinished).
                return;
            }
            RoundSummary summary = Match.FinishBattle();
            _log?.BattleFinished(summary);
            if (Match.Phase == MatchPhase.MatchOver)
            {
                _log?.Finished(Match);
                CloseBattleLog();
            }
            _selection.ClearTower();
            PhaseChanged?.Invoke();
        }

        /// <summary>
        /// A team's base health as the player sees it: while the nodes land, what is left after the hits so far. LAN guest: after they
        /// landed and until the host's result comes, what is left after all of them.
        /// </summary>
        public int ShownBaseHealth(Team team)
        {
            PlayerState player = Match.Get(team);
            RoundEnding ending = Ending ?? _landedEnding;
            return ending != null ? player.BaseHealth - ending.HitsTaken(team) : player.BaseHealth;
        }

        /// <summary>Leaves the result screen for the next round's deployment.</summary>
        public void Continue()
        {
            if (Match.Phase != MatchPhase.Result) return;
            if (LanRuns) Lan.Coordinator.ContinueAfterResult();
            else Match.ContinueAfterResult();
            LogAnyOffer();
            _selection.ClearTower();
            RefreshPreview();
            PhaseChanged?.Invoke();
        }

        /// <summary>
        /// Shows the player's current deployment as a battle that isn't running, with the enemy's army
        /// as it was at the last battle start as grey ghosts (D25; never the AI's current deployment).
        /// </summary>
        private void RefreshPreview(int selectPlacedSquadId = -1)
        {
            if (Match.Phase != MatchPhase.Deployment && Match.Phase != MatchPhase.Cards) return;
            BattleSimulation preview = Match.BuildPreview(LocalTeam); // Team A: BuildPreview(). The local side's squads first, then the ghosts.
            _previewOrder.Clear();
            _previewOrder.AddRange(Me.Squads);
            _ghostOrder.Clear();
            _ghostOrder.AddRange(Match.SeenArmy(OpponentTeam));
            _views.GhostTeam = OpponentTeam;
            _battle.ShowBattle(preview, _sources, play: false);

            // Squads still dropping in keep falling from where they were.
            foreach (var pair in _drops)
            {
                int dropIndex = _previewOrder.FindIndex(s => s.Id == pair.Key);
                if (dropIndex >= 0 && dropIndex < preview.Squads.Count) _views.DropSquad(preview.Squads[dropIndex].Id, pair.Value);
            }
            SelectPlaced(selectPlacedSquadId);
        }

        /// <summary>Selects one of the player's squads, as clicking it does (a replayed turn in place, D106).</summary>
        public void SelectSquad(PlacedSquad squad) => SelectPlaced(squad?.Id ?? -1);

        /// <summary>Selects the preview squad showing this placed squad (nothing if it is not shown).</summary>
        private void SelectPlaced(int placedSquadId)
        {
            int index = _previewOrder.FindIndex(s => s.Id == placedSquadId);
            if (_battle.Simulation != null && index >= 0 && index < _battle.Simulation.Squads.Count)
                _selection.Select(_battle.Simulation.Squads[index].Id);
        }

        private ActionResult Report(ActionResult result, string detail = null, [CallerMemberName] string action = "")
        {
            Message = result.Message ?? "";
            // Each deployment action that worked becomes a step Undo can take back; the card choice and the draft are the line it stops at.
            if (result.Ok && action != nameof(ChooseCard) && action != nameof(SkipCards) && action != nameof(ChooseDraft)) Match.RecordPlayerAction();
            _log?.PlayerAction(Match, action, detail, result);
            ActionTaken?.Invoke(action, result);
            return result;
        }
    }
}
