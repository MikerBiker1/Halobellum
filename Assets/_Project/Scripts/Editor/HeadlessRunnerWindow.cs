// HeadlessRunnerWindow.cs
// Editor window for the headless battle runner: choose the units and squad counts for two armies,
// the number of battles and the first seed, then press Run. The report appears in the window and
// the Console, and a CSV is written to <project>/BalanceReports/.
// Open it from Halobellum > Headless Runner > Open Runner Window.
// Depends on: HeadlessRunnerTool, Halobellum.Combat, Halobellum.Units, UnityEditor.

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Units;
using UnityEditor;
using UnityEngine;

namespace Halobellum.EditorTools
{
    public sealed class HeadlessRunnerWindow : EditorWindow
    {
        [Serializable]
        public sealed class SquadEntry
        {
            public UnitDefinition unit;
            public int squads = 1;
        }

        [SerializeField] private string armyAName = "Army A";
        [SerializeField] private List<SquadEntry> armyA = new List<SquadEntry>();
        [SerializeField] private string armyBName = "Army B";
        [SerializeField] private List<SquadEntry> armyB = new List<SquadEntry>();
        [SerializeField] private int battles = HeadlessRunnerTool.DefaultBattles;
        [SerializeField] private int firstSeed = HeadlessRunnerTool.DefaultSeed;

        private SerializedObject _serialized;
        private Vector2 _scroll;
        private string _report = "";

        public static void Open()
        {
            GetWindow<HeadlessRunnerWindow>("Headless Runner").Show();
        }

        private void OnEnable()
        {
            _serialized = new SerializedObject(this);
        }

        private void OnGUI()
        {
            _serialized.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Army A (Team A, bottom half)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_serialized.FindProperty(nameof(armyAName)), new GUIContent("Name"));
            EditorGUILayout.PropertyField(_serialized.FindProperty(nameof(armyA)), new GUIContent("Squads"), true);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Army B (Team B, top half)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_serialized.FindProperty(nameof(armyBName)), new GUIContent("Name"));
            EditorGUILayout.PropertyField(_serialized.FindProperty(nameof(armyB)), new GUIContent("Squads"), true);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_serialized.FindProperty(nameof(battles)));
            EditorGUILayout.PropertyField(_serialized.FindProperty(nameof(firstSeed)));
            _serialized.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Run", GUILayout.Height(28))) Run();

            if (!string.IsNullOrEmpty(_report))
            {
                EditorGUILayout.Space();
                EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            }
            EditorGUILayout.EndScrollView();
        }

        private void Run()
        {
            try
            {
                ArmySetup a = BuildArmy(armyAName, armyA);
                ArmySetup b = BuildArmy(armyBName, armyB);
                MatchupSummary summary = HeadlessBattleRunner.RunMatchup(HeadlessRunnerTool.LoadRules(), a, b,
                    HeadlessRunnerTool.DefaultSettings(Math.Max(1, battles), firstSeed));
                string csvPath = HeadlessRunnerTool.WriteCsv("matchup", HeadlessBattleRunner.ToCsv(new[] { summary }));
                _report = HeadlessBattleRunner.FormatReport(new[] { summary }) + "CSV written to " + csvPath;
                Debug.Log(_report);
            }
            catch (Exception e)
            {
                _report = "Could not run: " + e.Message;
                Debug.LogError(_report);
            }
        }

        private static ArmySetup BuildArmy(string name, List<SquadEntry> entries)
        {
            var army = new ArmySetup(string.IsNullOrEmpty(name) ? "Unnamed army" : name);
            foreach (SquadEntry entry in entries)
            {
                if (entry.unit == null || entry.squads <= 0) continue;
                army.Add(HeadlessRunnerTool.ValidatedStats(entry.unit), entry.squads);
            }
            if (army.Squads.Count == 0) throw new InvalidOperationException($"{army.Name} has no squads. Add at least one unit with 1 or more squads.");
            return army;
        }
    }
}
