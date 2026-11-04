// BattleHud.cs
// The battle screen's top bar (Bible Sections 5 and 13): battle time, living bodies on each side,
// and speed control (Pause, 1x, 2x, 4x). Keyboard: Space = pause/resume, 1 = 1x, 2 = 2x, 3 = 4x.
// Also shows the frame rate in the top-right corner when the Frame rate counter setting is on (off by default),
// and the result line when the battle ends.
// Placeholder UI drawn with Unity's immediate-mode GUI; scales with the screen height.
// Depends on: Halobellum.Presentation (BattleController, UiHitAreas), Unity Input System.

using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Halobellum.UI
{
    public sealed class BattleHud : MonoBehaviour
    {
        private static readonly float[] Speeds = { 1f, 2f, 4f };
        private static readonly string[] SpeedNames = { "1x", "2x", "4x" };
        private const float FpsTextSeconds = 0.25f;
        private const float ResultLineWidth = 600f, ResultLineHeight = 34f;

        [SerializeField] private BattleController battle;
        [Tooltip("Show the frames-per-second counter.")]
        [SerializeField] private bool showFps = true;
        [Tooltip("Show the one-line result when the battle ends (the Match scene has its own result screen).")]
        [SerializeField] private bool showResultLine = true;

        private float _resumeSpeed = 1f;
        private float _smoothedFrameTime;
        private GUIStyle _label, _centered, _button, _selectedButton;
        private readonly HudLine _topLine = new HudLine();
        private string _fpsText;
        private float _nextFpsText;

        /// <summary>How far down the result line stands. Under the speed controls, unless a scene's own HUD has something there (the Sandbox's bar).</summary>
        public float ResultLineTop { get; set; } = 90f;

        /// <summary>Where the result line is, in HUD units.</summary>
        public static Rect ResultLine(float screenWidth, float top) => new Rect(screenWidth * 0.5f - ResultLineWidth * 0.5f, top, ResultLineWidth, ResultLineHeight);

        private void Awake() => useGUILayout = false; // Nothing here uses GUILayout. With the layout pass on, Unity makes new layout groups for this script every frame, which is garbage at every frame rate.

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _smoothedFrameTime = _smoothedFrameTime <= 0f ? dt : Mathf.Lerp(_smoothedFrameTime, dt, 0.05f);

            Keyboard keyboard = Keyboard.current;
            if (battle == null || battle.Clock == null || keyboard == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
            if (keyboard.digit1Key.wasPressedThisFrame) SetSpeed(Speeds[0]);
            if (keyboard.digit2Key.wasPressedThisFrame) SetSpeed(Speeds[1]);
            if (keyboard.digit3Key.wasPressedThisFrame) SetSpeed(Speeds[2]);
        }

        private void TogglePause()
        {
            if (battle.Clock.IsPaused) battle.Speed = _resumeSpeed;
            else { _resumeSpeed = battle.Speed; battle.Speed = 0f; }
        }

        private void SetSpeed(float speed)
        {
            battle.Speed = speed;
            _resumeSpeed = speed;
        }

        private void OnGUI()
        {
            if (battle == null || battle.Simulation == null) return;
            if (!battle.IsPlaying && battle.Simulation.Tick == 0) return; // Deployment preview: no battle bar.
            HudTheme.Apply();
            EnsureStyles();
            float scale = HudScale.Apply();

            BattleSimulation sim = battle.Simulation;
            float width = HudScale.ScaledWidth(scale);

            // Top bar.
            var bar = new Rect(width * 0.5f - 300f, 8f, 600f, 74f);
            UiHitAreas.Add(HudScale.ToScreen(bar, scale));
            HudTheme.PanelBox(bar);
            float limit = battle.Rules.BattleTimeLimitSeconds;
            int aliveA = battle.CountAlive(Team.A), aliveB = battle.CountAlive(Team.B);
            if (_topLine.Changed(aliveA, aliveB, HudLine.Tenths(sim.ElapsedSeconds), HudLine.Whole(limit)))
                _topLine.Text = $"Team A  {aliveA}        {sim.ElapsedSeconds:0.0} s / {limit:0} s        {aliveB}  Team B";
            GUI.Label(new Rect(bar.x + 12f, bar.y + 6f, 576f, 26f), _topLine.Text, _centered);

            float buttonsX = bar.x + 120f;
            DrawSpeedButton(new Rect(buttonsX, bar.y + 38f, 80f, 28f), "Pause", 0f);
            for (int i = 0; i < Speeds.Length; i++)
            {
                DrawSpeedButton(new Rect(buttonsX + 90f * (i + 1), bar.y + 38f, 80f, 28f), SpeedNames[i], Speeds[i]);
            }

            if (showResultLine && sim.IsFinished && sim.Result != null)
            {
                Rect result = ResultLine(width, ResultLineTop);
                UiHitAreas.Add(HudScale.ToScreen(result, scale));
                HudTheme.PanelBox(result);
                GUI.Label(result, ResultText(sim.Result), _centered);
            }

            if (showFps && GameSettings.ShowFrameRate && _smoothedFrameTime > 0f)
            {
                // A few times a second: faster can't be read, and it would be a new string every frame.
                if (_fpsText == null || Time.unscaledTime >= _nextFpsText)
                {
                    _nextFpsText = Time.unscaledTime + FpsTextSeconds;
                    _fpsText = $"{1f / _smoothedFrameTime:0} FPS  ({_smoothedFrameTime * 1000f:0.0} ms)";
                }
                GUI.Label(new Rect(width - 190f, 8f, 180f, 26f), _fpsText, _label);
            }
        }

        private void DrawSpeedButton(Rect rect, string text, float speed)
        {
            bool current = speed <= 0f ? battle.Clock.IsPaused : !battle.Clock.IsPaused && Mathf.Approximately(battle.Speed, speed);
            if (!GUI.Button(rect, text, current ? _selectedButton : _button)) return;
            if (speed <= 0f) TogglePause();
            else SetSpeed(speed);
        }

        private static string ResultText(BattleResult result)
        {
            string outcome = result.Outcome == BattleOutcome.TeamAWins ? "Team A wins"
                : result.Outcome == BattleOutcome.TeamBWins ? "Team B wins" : "Draw";
            string reason = result.TimedOut ? "time limit" : "a side was wiped out";
            return $"Battle over ({reason}): {outcome}.  Survivors {result.TeamA.Survivors} vs {result.TeamB.Survivors}";
        }

        private int _styleVersion = -1;

        private void EnsureStyles()
        {
            if (_label != null && _styleVersion == HudTheme.Version) return;
            _styleVersion = HudTheme.Version;
            _label = new GUIStyle(GUI.skin.label) { fontSize = HudTheme.Size(16), normal = { textColor = HudTheme.Text } };
            _centered = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = HudTheme.Size(18) };
            _button = new GUIStyle(GUI.skin.button) { fontSize = HudTheme.Size(15) };
            _selectedButton = new GUIStyle(_button) { fontStyle = FontStyle.Bold, normal = { textColor = HudTheme.Accent }, hover = { textColor = HudTheme.Accent } };
            HudTheme.Fit(_label, _centered, _button, _selectedButton);
        }
    }
}
