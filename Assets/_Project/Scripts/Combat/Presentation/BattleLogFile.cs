// BattleLogFile.cs
// Writes a person's game to a file, a line at a time, so that a game that is quit or crashes keeps all it had (D68). Each game has its own file in the
// BattleLogs folder of the game's saved data. Tests and test runs write none unless told where: start the game with "-battleLogs <folder>".
// A problem with the disk stops the log, and never the game.
// Depends on: Halobellum.Economy (MatchLog writes the lines), UnityEngine.

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class BattleLogFile : IDisposable
    {
        public const string FolderFlag = "-battleLogs";

        private StreamWriter _writer;

        /// <summary>Where a person's games are written.</summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, "BattleLogs");

        /// <summary>The folder to write this game to, or null if this run writes no log.</summary>
        public static string FolderForThisRun()
        {
            string[] args = Environment.GetCommandLineArgs();
            int asked = Array.IndexOf(args, FolderFlag);
            if (asked >= 0 && asked + 1 < args.Length) return args[asked + 1];
            if (Application.isEditor || Application.isBatchMode || Array.IndexOf(args, GameFlow.AutoplayFlag) >= 0) return null;
            return Folder;
        }

        /// <summary>A file's name from the time and who played. Whatever a name can't hold in a file name becomes a dash.</summary>
        public static string FileName(DateTime localTime, string mode, string player, string enemy)
        {
            string name = $"{localTime:yyyy-MM-dd_HH-mm-ss}_{mode}";
            if (player != null && enemy != null) name += $"_{player}_vs_{enemy}";
            foreach (char bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '-');
            return name.Replace(' ', '-') + ".jsonl";
        }

        public BattleLogFile(string folder, string fileName)
        {
            try
            {
                Directory.CreateDirectory(folder);
                _writer = new StreamWriter(Path.Combine(folder, fileName), false, new UTF8Encoding(false)) { AutoFlush = true };
            }
            catch (Exception problem) when (problem is IOException || problem is UnauthorizedAccessException || problem is ArgumentException || problem is NotSupportedException)
            {
                Debug.LogWarning($"The battle log can't be written: {problem.Message}");
            }
        }

        public void Write(string line)
        {
            if (_writer == null) return;
            try
            {
                _writer.WriteLine(line);
            }
            catch (Exception problem) when (problem is IOException || problem is ObjectDisposedException)
            {
                Debug.LogWarning($"The battle log stopped: {problem.Message}");
                Dispose();
            }
        }

        public void Dispose()
        {
            StreamWriter writer = _writer;
            _writer = null;
            try
            {
                writer?.Dispose();
            }
            catch (IOException)
            {
                // What it had is written line by line; there's nothing left to save.
            }
        }
    }
}
