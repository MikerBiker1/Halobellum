// TournamentDataExport.cs
// Writes the game's real rules, factions, units, techs, spells and cards to a JSON file for the Hard AI tournament (Tools/Balance/Tournament), which
// plays them outside Unity, many matches at a time. It goes through the same ToRules and ToInfo as the game, and copies every field of the plain C#
// objects these make, so the tournament can't drift from the game. Shared objects stay shared: a second reference to an object is written as its number.
// Run with: HB_TOURNAMENT_DATA=<file> bash /e/AI/_hbwt/unity.sh <ws> exec Halobellum.EditorTools.TournamentDataExport.ExportFromCommandLine
// Depends on: Halobellum.Units (the definitions), UnityEditor.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Halobellum.Economy;
using Halobellum.Units;
using UnityEditor;
using UnityEngine;

namespace Halobellum.EditorTools
{
    public static class TournamentDataExport
    {
        private const string FactionsFolder = "Assets/_Project/Data/Factions";
        private const string MatchRulesPath = "Assets/_Project/Data/MatchRules.asset";

        public static void ExportFromCommandLine()
        {
            int exitCode = 0;
            try
            {
                Export();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        private static void Export()
        {
            string path = Environment.GetEnvironmentVariable("HB_TOURNAMENT_DATA");
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Set HB_TOURNAMENT_DATA to the file to write.");

            var cache = new Dictionary<UnitDefinition, Core.UnitStats>();
            var factions = new List<FactionInfo>();
            foreach (string guid in AssetDatabase.FindAssets("t:FactionDefinition", new[] { FactionsFolder }))
            {
                factions.Add(AssetDatabase.LoadAssetAtPath<FactionDefinition>(AssetDatabase.GUIDToAssetPath(guid)).ToInfo(cache, null));
            }
            MatchRules rules = AssetDatabase.LoadAssetAtPath<MatchRulesDefinition>(MatchRulesPath).ToRules();
            Core.CombatRules combat = AssetDatabase.LoadAssetAtPath<CombatRulesDefinition>(CombatRulesDefinition.DefaultAssetPath).ToRules();

            var writer = new Writer();
            writer.Text.Append("{\"factions\":");
            writer.Write(factions);
            writer.Text.Append(",\"rules\":");
            writer.Write(rules);
            writer.Text.Append(",\"combat\":");
            writer.Write(combat);
            writer.Text.Append('}');
            File.WriteAllText(path, writer.Text.ToString(), new UTF8Encoding(false));
            Debug.Log($"Built tournament data: {factions.Count} factions, {writer.Objects} objects, to {path}");
        }

        private sealed class Writer
        {
            public readonly StringBuilder Text = new StringBuilder();
            private readonly Dictionary<object, int> _ids = new Dictionary<object, int>(new SameObject());

            public int Objects => _ids.Count;

            public void Write(object value)
            {
                switch (value)
                {
                    case null: Text.Append("null"); return;
                    case string text: WriteString(text); return;
                    case bool flag: Text.Append(flag ? "true" : "false"); return;
                    case float single: WriteFloat(single); return;
                    case double wide: WriteFloat((float)wide); return;
                    case Enum choice: Text.Append(Convert.ToInt64(choice).ToString(CultureInfo.InvariantCulture)); return;
                    case int _:
                    case long _:
                    case byte _:
                        Text.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                        return;
                }

                Type type = value.GetType();
                if (typeof(Delegate).IsAssignableFrom(type)) { Text.Append("null"); return; }
                if (!type.IsValueType)
                {
                    if (_ids.TryGetValue(value, out int known))
                    {
                        Text.Append("{\"$r\":").Append(known).Append('}');
                        return;
                    }
                    _ids[value] = _ids.Count;
                }

                Text.Append('{');
                if (!type.IsValueType) Text.Append("\"$i\":").Append(_ids[value]).Append(',');
                Text.Append("\"$t\":");
                WriteString(type.IsArray ? type.GetElementType().FullName + "[]" : TypeName(type));
                Text.Append(',');
                if (value is Array array && array.Rank == 2) WriteGrid(array);
                else if (value is IList list) WriteList(list);
                else if (value is IDictionary || (type.IsGenericType && type.Namespace == "System.Collections.Generic"))
                    throw new InvalidOperationException($"The tournament can't carry a {type}: only lists and arrays.");
                else WriteFields(value, type);
                Text.Append('}');
            }

            private static string TypeName(Type type) => type.IsGenericType ? "List:" + type.GetGenericArguments()[0].FullName : type.FullName;

            private void WriteList(IList list)
            {
                Text.Append("\"$l\":[");
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) Text.Append(',');
                    Write(list[i]);
                }
                Text.Append(']');
            }

            private void WriteGrid(Array grid)
            {
                int rows = grid.GetLength(0), columns = grid.GetLength(1);
                Text.Append("\"$g\":[").Append(rows).Append(',').Append(columns).Append("],\"$l\":[");
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < columns; c++)
                    {
                        if (r + c > 0) Text.Append(',');
                        Write(grid.GetValue(r, c));
                    }
                }
                Text.Append(']');
            }

            private void WriteFields(object value, Type type)
            {
                Text.Append("\"f\":{");
                bool first = true;
                for (Type at = type; at != null && at != typeof(object) && at != typeof(ValueType); at = at.BaseType)
                {
                    foreach (FieldInfo field in at.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
                        if (!first) Text.Append(',');
                        first = false;
                        WriteString(field.Name);
                        Text.Append(':');
                        Write(field.GetValue(value));
                    }
                }
                Text.Append('}');
            }

            private void WriteFloat(float value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) WriteString(value.ToString(CultureInfo.InvariantCulture));
                else Text.Append(value.ToString("R", CultureInfo.InvariantCulture));
            }

            private void WriteString(string text)
            {
                Text.Append('"');
                foreach (char c in text)
                {
                    if (c == '"' || c == '\\') Text.Append('\\').Append(c);
                    else if (c < ' ') Text.Append("\\u").Append(((int)c).ToString("x4"));
                    else Text.Append(c);
                }
                Text.Append('"');
            }
        }

        private sealed class SameObject : IEqualityComparer<object>
        {
            bool IEqualityComparer<object>.Equals(object a, object b) => ReferenceEquals(a, b);
            int IEqualityComparer<object>.GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
