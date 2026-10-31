// BattleBenchmark.cs
// Measures frame rate during a battle, to check the M2 target (300 vs 300 at 60 FPS). It does
// nothing unless the game is started with "-benchmark" on the command line, for example:
//   Halobellum.exe -benchmark -benchmarkSpeed 1 -benchmarkOutput C:\temp\bench.txt
// It turns off vsync and frame caps, plays the whole battle at the given speed, writes frame-time
// statistics to the output file, and quits. Optional: "-screenshotAt 8 -screenshotPath C:\temp\shot.png"
// saves a screenshot when the battle clock reaches that many seconds.
// Depends on: BattleController, UnityEngine.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class BattleBenchmark : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [Tooltip("Frames ignored at the start while everything warms up.")]
        [SerializeField] private int warmUpFrames = 30;

        private readonly List<float> _frameTimes = new List<float>();
        private readonly List<float> _simulationTimes = new List<float>();
        private string _outputPath;
        private float _speed = 1f;
        private int _frames;
        private string _screenshotPath;
        private float _screenshotAt = -1f;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-benchmark") < 0)
            {
                enabled = false;
                return;
            }
            _outputPath = Argument(args, "-benchmarkOutput") ?? Path.Combine(Application.persistentDataPath, "benchmark.txt");
            if (float.TryParse(Argument(args, "-benchmarkSpeed"), NumberStyles.Float, CultureInfo.InvariantCulture, out float speed)) _speed = speed;
            _screenshotPath = Argument(args, "-screenshotPath");
            if (float.TryParse(Argument(args, "-screenshotAt"), NumberStyles.Float, CultureInfo.InvariantCulture, out float at)) _screenshotAt = at;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        private void Start()
        {
            if (battle == null || battle.Simulation == null) return;
            battle.Speed = _speed;
            // Optional "-benchmarkSelect <unit number>": selects that unit's squad, so screenshots show the stats window.
            if (int.TryParse(Argument(Environment.GetCommandLineArgs(), "-benchmarkSelect"), out int unitIndex)
                && unitIndex >= 0 && unitIndex < battle.Simulation.Units.Count)
            {
                battle.GetComponent<SquadSelection>().Select(battle.Simulation.Units[unitIndex].SquadId);
            }
        }

        private void Update()
        {
            if (battle == null || battle.Simulation == null) return;
            _frames++;
            if (_frames > warmUpFrames)
            {
                _frameTimes.Add(Time.unscaledDeltaTime);
                _simulationTimes.Add(battle.LastFrameSimulationMilliseconds);
            }
            if (_screenshotPath != null && _screenshotAt >= 0f && battle.Simulation.ElapsedSeconds >= _screenshotAt)
            {
                ScreenCapture.CaptureScreenshot(_screenshotPath);
                _screenshotPath = null;
            }
            if (battle.Simulation.IsFinished) Finish();
        }

        private void Finish()
        {
            enabled = false;
            var text = new StringBuilder();
            text.AppendLine(FormattableString.Invariant($"Halobellum battle benchmark, speed {_speed}x, {Screen.width}x{Screen.height}, {SystemInfo.graphicsDeviceName}, {SystemInfo.processorType}"));
            text.AppendLine(FormattableString.Invariant($"Bodies: {battle.Simulation.Units.Count}; battle length {battle.Simulation.ElapsedSeconds:0.0} s; frames measured {_frameTimes.Count}"));
            if (_frameTimes.Count > 0)
            {
                var sorted = new List<float>(_frameTimes);
                sorted.Sort();
                double total = 0;
                foreach (float t in _frameTimes) total += t;
                float average = (float)(total / _frameTimes.Count);
                float p99 = sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.99f))];
                float worst = sorted[sorted.Count - 1];
                float simAverage = 0f, simWorst = 0f;
                foreach (float t in _simulationTimes) { simAverage += t; simWorst = Mathf.Max(simWorst, t); }
                simAverage /= Mathf.Max(1, _simulationTimes.Count);
                int below60 = 0;
                foreach (float t in _frameTimes) if (t > 1f / 60f) below60++;

                text.AppendLine(FormattableString.Invariant($"Average FPS: {1f / average:0.0} ({average * 1000f:0.00} ms per frame)"));
                text.AppendLine(FormattableString.Invariant($"1% low FPS: {1f / p99:0.0} ({p99 * 1000f:0.00} ms); worst frame {worst * 1000f:0.00} ms"));
                text.AppendLine(FormattableString.Invariant($"Frames slower than 60 FPS: {below60} of {_frameTimes.Count} ({100f * below60 / _frameTimes.Count:0.0}%)"));
                text.AppendLine(FormattableString.Invariant($"Simulation time per frame: average {simAverage:0.00} ms, worst {simWorst:0.00} ms"));
            }
            text.AppendLine("Result: " + battle.Simulation.Result.ToSummaryLine());

            try
            {
                File.WriteAllText(_outputPath, text.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("Could not write benchmark file: " + e.Message);
            }
            Debug.Log(text.ToString());
            Application.Quit();
        }

        private static string Argument(string[] args, string flag)
        {
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
