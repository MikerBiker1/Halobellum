// MatchReplay.cs
// Plays a battle log again in the Match scene (D106), as it was played: the same match from the log's seed, mode, difficulty and starting card,
// then each of the player's actions through the MatchController, as the clicks did, with the AI answering as it did. Start the game with
//   Halobellum.exe -replayLog <file.jsonl>                     watch it (actions a moment apart, battles at 2x), and stay on the last screen
//   ... -replayStop 4                                           stop at round 4's deployment once its actions are done, before its battle: look around or play on
//   ... -replaySpeed 4 -replayStep 0.2                          battle speed, and seconds between actions
//   ... -replayShots <folder>                                   also save pictures (each card screen, deployment, battle at 2 and 10 s, result)
//                                                               and replay.txt, then quit
// Wherever the game now differs from the log (a balance change since, a squad no longer where it was), MatchLogReplay notes it, in replay.txt and
// on screen at the end; it carries on regardless. A replay writes no battle log of its own (BattleLogFile).
// Depends on: MatchController, BattleController, Halobellum.Economy (MatchLogReplay), UnityEngine.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Halobellum.Core;
using Halobellum.Economy;
using UnityEngine;

namespace Halobellum.Presentation
{
    [RequireComponent(typeof(MatchController))]
    public sealed class MatchReplay : MonoBehaviour
    {
        public const string LogFlag = "-replayLog";
        private const string ShotsFlag = "-replayShots", SpeedFlag = "-replaySpeed", StepFlag = "-replayStep", StopFlag = "-replayStop";

        /// <summary>The log's lines (set before Start, or read from -replayLog).</summary>
        public IReadOnlyList<string> Lines;
        public string LogName = "";
        /// <summary>Where pictures and replay.txt go, or null for none.</summary>
        public string ShotsFolder;
        public float BattleSpeed = 2f;
        public float StepSeconds = 0.4f;
        /// <summary>Stop at this round's deployment, before its battle (0 = play the whole log).</summary>
        public int StopRound;
        public bool QuitAtEnd;

        /// <summary>The log being replayed, with what drifted from it so far.</summary>
        public MatchLogReplay Replay { get; private set; }
        public bool Finished { get; private set; }
        /// <summary>What it says on screen while it runs and at the end.</summary>
        public string Status { get; private set; } = "";

        private MatchController _match;
        private BattleController _battle;
        private GUIStyle _style;

        public static bool Requested(string[] args) => Array.IndexOf(args, LogFlag) >= 0;

        /// <summary>Set up from the command line; false if the log can't be read.</summary>
        public bool ReadCommandLine(string[] args)
        {
            string file = Argument(args, LogFlag);
            ShotsFolder = Argument(args, ShotsFlag);
            if (float.TryParse(Argument(args, SpeedFlag), NumberStyles.Float, CultureInfo.InvariantCulture, out float speed)) BattleSpeed = speed;
            if (float.TryParse(Argument(args, StepFlag), NumberStyles.Float, CultureInfo.InvariantCulture, out float step)) StepSeconds = step;
            if (int.TryParse(Argument(args, StopFlag), out int stop)) StopRound = stop;
            QuitAtEnd = ShotsFolder != null;
            try
            {
                Lines = File.ReadAllLines(file);
                LogName = Path.GetFileName(file);
                return true;
            }
            catch (Exception problem) when (problem is IOException || problem is UnauthorizedAccessException || problem is ArgumentException || problem is NotSupportedException)
            {
                Status = $"The battle log can't be read: {problem.Message}";
                Debug.LogError(Status);
                return false;
            }
        }

        private static string Argument(string[] args, string flag)
        {
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private Match M => _match.Match;

        private IEnumerator Start()
        {
            _match = GetComponent<MatchController>();
            _battle = GetComponent<BattleController>();
            if (Lines == null) yield break;
            if (ShotsFolder != null) Directory.CreateDirectory(ShotsFolder);
            yield return null; // The scene's own new match comes first.

            try
            {
                Replay = MatchLogReplay.Read(Lines);
            }
            catch (FormatException problem)
            {
                Status = $"Not a battle log: {problem.Message}";
                Debug.LogError(Status);
                Finished = true;
                yield break;
            }

            var hands = new ControllerHands(_match);
            _match.NewMatch(Replay.Seed, Replay.Setup, difficulty: Replay.AiDifficulty);
            yield return null;
            Say($"Replaying {LogName}: seed {Replay.Seed}, {Replay.Mode}, {Replay.Difficulty}.");
            ActionResult drafted = hands.ChooseDraft(Replay.DraftChosen);
            if (!drafted.Ok) Replay.Drift.Add($"the starting card {Replay.DraftChosen}: {drafted.Message}");
            yield return Wait(StepSeconds);

            foreach (ReplayLine line in Replay.Lines)
            {
                if (M.Phase == MatchPhase.MatchOver) break;
                switch (line.Type)
                {
                    case "cards_offered":
                        yield return Shot($"r{line.Round:00}_1_cards");
                        break;

                    case "action":
                        if (!MatchLogReplay.IsReplayed(line)) break;
                        yield return Wait(StepSeconds);
                        ActionResult result = MatchLogReplay.Apply(hands, line);
                        Say($"Round {line.Round}: {line.Action} {line.Detail}{(result.Ok ? "" : " - " + result.Message)}");
                        if (!result.Ok) Replay.Note(line, $"{line.Action} {line.Detail}: {result.Message}");
                        while (_match.IsDropping) yield return null; // A bought squad lands before the next action.
                        break;

                    case "battle_start":
                        foreach (string difference in MatchLogReplay.Differences(M, line)) Replay.Note(line, difference);
                        yield return Wait(StepSeconds);
                        yield return Shot($"r{line.Round:00}_2_deployment");
                        if (line.Round == StopRound)
                        {
                            Finish($"Replay stopped at round {line.Round}'s deployment: the match is yours from here.");
                            yield break;
                        }
                        _battle.Speed = BattleSpeed;
                        _match.Ready();
                        break;

                    case "battle_end":
                        yield return Battle(line.Round);
                        foreach (string difference in MatchLogReplay.BattleDifferences(M, line)) Replay.Note(line, difference);
                        yield return Wait(0.6f);
                        yield return Shot($"r{line.Round:00}_4_result");
                        if (M.Phase == MatchPhase.Result) _match.Continue();
                        yield return null;
                        break;
                }
            }
            Finish(Replay.Drift.Count == 0 ? "Replay done: it matched the log." : $"Replay done: {Replay.Drift.Count} difference(s) from the log.");
        }

        private IEnumerator Battle(int round)
        {
            bool early = false, later = false;
            while (M.Phase == MatchPhase.Battle)
            {
                float seconds = _battle.Simulation != null ? _battle.Simulation.ElapsedSeconds : 0f;
                if (!early && seconds >= 2f) { early = true; yield return Shot($"r{round:00}_3_battle_02s"); }
                else if (!later && seconds >= 10f) { later = true; yield return Shot($"r{round:00}_3_battle_10s"); }
                else yield return null;
            }
        }

        private void Say(string status)
        {
            Status = status;
            Debug.Log("[Replay] " + status);
        }

        private void Finish(string status)
        {
            Say(status);
            Finished = true;
            foreach (string drift in Replay.Drift) Debug.Log("[Replay] " + drift);
            if (ShotsFolder != null)
            {
                var report = new StringBuilder();
                report.AppendLine($"Replay of {LogName}: seed {Replay.Seed}, {Replay.Mode}, {Replay.Difficulty}, starting card {Replay.DraftChosen}, game version {Application.version}.");
                foreach (string drift in Replay.Drift) report.AppendLine(drift);
                report.AppendLine(status);
                File.WriteAllText(Path.Combine(ShotsFolder, "replay.txt"), report.ToString());
            }
            if (QuitAtEnd) StartCoroutine(QuitSoon());
        }

        private static IEnumerator QuitSoon()
        {
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        private static IEnumerator Wait(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
        }

        /// <summary>A picture of the screen (saved at the end of the frame), if pictures were asked for.</summary>
        private IEnumerator Shot(string name)
        {
            if (ShotsFolder == null) yield break;
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(ShotsFolder, name + ".png"));
            yield return null;
            yield return null;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(Status) || ShotsFolder != null) return; // Pictures show the game as the player saw it.
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            int drift = Replay?.Drift.Count ?? 0;
            string text = drift > 0 && Finished ? $"{Status}\n{string.Join("\n", Replay.Drift.GetRange(0, Math.Min(drift, 6)))}" : Status;
            var box = new Rect(Screen.width * 0.5f - 420f, Screen.height - 150f, 840f, 70f + (Finished ? Math.Min(drift, 6) * 20f : 0f));
            box.y = Mathf.Min(box.y, Screen.height - box.height - 80f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, box.height - 16f), text, _style);
        }

        /// <summary>The player's actions through the MatchController, as the clicks do them.</summary>
        private sealed class ControllerHands : IReplayHands
        {
            private readonly MatchController _c;

            public ControllerHands(MatchController controller) => _c = controller;

            public Match Match => _c.Match;
            public ActionResult ChooseDraft(int index) => _c.ChooseDraft(index);
            public ActionResult ChooseCard(int index) => _c.ChooseCard(index);
            public ActionResult SkipCards() => _c.SkipCards();
            public ActionResult Unlock(UnitStats type) => _c.Unlock(type);
            public ActionResult Buy(UnitStats type) => _c.Buy(type);

            public ActionResult Move(PlacedSquad squad, int column, int row, bool turned)
            {
                _c.BeginMoving(squad);
                if (!_c.IsPlacing) return ActionResult.Fail(_c.Message);
                if (_c.PlacingRotated != turned) _c.ToggleRotation(); // R while placing.
                ActionResult result = _c.PlaceAt(column, row);
                if (!result.Ok) _c.CancelPlacement();
                return result;
            }

            public ActionResult Turn(PlacedSquad squad)
            {
                bool before = squad.Rotated90;
                _c.SelectSquad(squad);
                _c.ToggleRotation();
                return squad.Rotated90 != before ? ActionResult.Success() : ActionResult.Fail(_c.Message);
            }

            public ActionResult Sell(PlacedSquad squad) => _c.Sell(squad);
            public ActionResult LevelUp(PlacedSquad squad) => _c.LevelUp(squad);
            public ActionResult LevelUpAll(UnitStats type) => _c.LevelUpAll(type);
            public ActionResult BuyTech(UnitStats type, TechUpgrade tech) => _c.BuyTech(type, tech);
            public ActionResult LevelUpTower(TowerKind kind) => _c.LevelUpTower(kind);
            public ActionResult BuyExtraSlot() => _c.BuyExtraSlot();
            public ActionResult BuyOilZone() => _c.BuyOilZone();
            public ActionResult RapidResupply() => _c.RapidResupply();
            public ActionResult BuyEliteRecruitment() => _c.BuyEliteRecruitment();
            public ActionResult BuyResearch(bool health) => _c.BuyResearch(health);

            public ActionResult PlaceSpell(OwnedSpell spell, Vec2 position, float angleDegrees)
            {
                _c.BeginPlacingSpell(spell);
                if (_c.PlacingSpellId != spell.Id) return ActionResult.Fail(_c.Message);
                ActionResult result = _c.PlaceSpellAt(position, angleDegrees);
                if (!result.Ok) _c.CancelSpellPlacement();
                return result;
            }

            public ActionResult UnplaceSpell(OwnedSpell spell) => _c.UnplaceSpell(spell);

            public ActionResult PlaceItem(ItemStats item, PlacedSquad squad)
            {
                _c.BeginPlacingItem(item);
                ActionResult result = _c.PlaceItemOn(squad);
                if (!result.Ok) _c.CancelItemPlacement();
                return result;
            }

            public ActionResult UseTraining(PlacedSquad squad) => _c.UseTraining(squad);
            public ActionResult Undo() => _c.Undo();
        }
    }
}
