// StatJson.cs
// A small, strict JSON reader and writer for the files players may edit (the unit stats files next to the built game, mod_json).
// Unity's JsonUtility gives no line numbers, ignores unknown fields and cannot tell a missing field from 0, so this one keeps the
// line of every value and says exactly where a file goes wrong: "UNSC.json line 7, column 5: expected ',' or '}'".
// Strict JSON: a leading byte order mark is accepted; comments, NaN, Infinity, trailing commas, a key given twice and anything after
// the root value are errors. A number keeps its text (Raw), so a reader can tell a whole number from 2.5 or 1e2.
// The writer helpers write numbers with the invariant culture, so a file reads the same on every machine.
// Plain C# 9 without the engine: the tournament, the counter web and the test harness compile it too.
// Depends on: nothing.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Halobellum.Core
{
    public enum JsonKind { Object, Array, String, Number, Bool, Null }

    /// <summary>One JSON value and the line it starts on (1-based).</summary>
    public sealed class JsonNode
    {
        public JsonKind Kind;
        /// <summary>The text of a number, as written.</summary>
        public string Raw;
        public string Str;
        public bool Bool;
        public int Line;
        /// <summary>An object's fields, in the file's order.</summary>
        public List<KeyValuePair<string, JsonNode>> Fields;
        public List<JsonNode> Items;

        /// <summary>A field of an object, or null.</summary>
        public JsonNode Field(string name)
        {
            if (Fields == null) return null;
            foreach (var pair in Fields)
            {
                if (pair.Key == name) return pair.Value;
            }
            return null;
        }

        public static string KindName(JsonKind kind)
        {
            switch (kind)
            {
                case JsonKind.Object: return "an object { }";
                case JsonKind.Array: return "a list [ ]";
                case JsonKind.String: return "text in quotes";
                case JsonKind.Number: return "a number";
                case JsonKind.Bool: return "true or false";
                default: return "null";
            }
        }
    }

    /// <summary>A file that is not valid JSON: where and why.</summary>
    public sealed class StatJsonException : Exception
    {
        public readonly string FileName;
        public readonly int Line;
        public readonly int Column;
        public readonly string Reason;

        public StatJsonException(string fileName, int line, int column, string reason)
            : base($"{fileName} line {line}, column {column}: {reason}")
        {
            FileName = fileName;
            Line = line;
            Column = column;
            Reason = reason;
        }
    }

    public static class StatJson
    {
        /// <summary>Reads a whole file. Throws StatJsonException naming the file, line and column of the first mistake.</summary>
        public static JsonNode Parse(string text, string fileName)
        {
            var parser = new Parser(text ?? "", fileName);
            return parser.ParseDocument();
        }

        /// <summary>Text in JSON quotes, with what must be escaped escaped.</summary>
        public static string Quote(string value)
        {
            var quoted = new StringBuilder("\"");
            foreach (char c in value ?? "")
            {
                switch (c)
                {
                    case '"': quoted.Append("\\\""); break;
                    case '\\': quoted.Append("\\\\"); break;
                    case '\n': quoted.Append("\\n"); break;
                    case '\r': quoted.Append("\\r"); break;
                    case '\t': quoted.Append("\\t"); break;
                    default:
                        if (c < ' ') quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else quoted.Append(c);
                        break;
                }
            }
            return quoted.Append('"').ToString();
        }

        /// <summary>A float as plain digits (no exponent), up to 7 decimals.</summary>
        public static string Number(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "0";
            return value.ToString("0.#######", CultureInfo.InvariantCulture);
        }

        public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private sealed class Parser
        {
            private readonly string _text;
            private readonly string _file;
            private int _pos;
            private int _line = 1;
            private int _lineStart;

            public Parser(string text, string file)
            {
                _text = text;
                _file = file;
                if (_text.Length > 0 && _text[0] == '﻿') _pos = 1;
                _lineStart = _pos;
            }

            public JsonNode ParseDocument()
            {
                SkipSpace();
                if (_pos >= _text.Length) throw Error("the file is empty");
                JsonNode root = ParseValue(0);
                SkipSpace();
                if (_pos < _text.Length) throw Error($"unexpected '{_text[_pos]}' after the end of the data");
                return root;
            }

            private StatJsonException Error(string reason) => new StatJsonException(_file, _line, _pos - _lineStart + 1, reason);

            private void SkipSpace()
            {
                while (_pos < _text.Length)
                {
                    char c = _text[_pos];
                    if (c == '\n')
                    {
                        _pos++;
                        _line++;
                        _lineStart = _pos;
                    }
                    else if (c == ' ' || c == '\t' || c == '\r') _pos++;
                    else if (c == '/' && _pos + 1 < _text.Length && (_text[_pos + 1] == '/' || _text[_pos + 1] == '*')) throw Error("comments are not allowed in JSON (use a \"_note\" field instead)");
                    else break;
                }
            }

            private JsonNode ParseValue(int depth)
            {
                if (depth > 64) throw Error("the data is nested too deeply");
                SkipSpace();
                if (_pos >= _text.Length) throw Error("the file ends too early (a missing '}' or ']'?)");
                char c = _text[_pos];
                int line = _line;
                switch (c)
                {
                    case '{': return ParseObject(depth);
                    case '[': return ParseArray(depth);
                    case '"': return new JsonNode { Kind = JsonKind.String, Str = ParseString(), Line = line };
                    case 't': Word("true"); return new JsonNode { Kind = JsonKind.Bool, Bool = true, Line = line };
                    case 'f': Word("false"); return new JsonNode { Kind = JsonKind.Bool, Bool = false, Line = line };
                    case 'n': Word("null"); return new JsonNode { Kind = JsonKind.Null, Line = line };
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return new JsonNode { Kind = JsonKind.Number, Raw = ParseNumber(), Line = line };
                        throw Error($"unexpected '{c}' (text needs \"quotes\"; NaN and Infinity are not allowed)");
                }
            }

            private void Word(string word)
            {
                if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0) throw Error($"unexpected '{_text[_pos]}' (did you mean {word}?)");
                _pos += word.Length;
            }

            private JsonNode ParseObject(int depth)
            {
                var node = new JsonNode { Kind = JsonKind.Object, Line = _line, Fields = new List<KeyValuePair<string, JsonNode>>() };
                _pos++; // {
                SkipSpace();
                if (_pos < _text.Length && _text[_pos] == '}')
                {
                    _pos++;
                    return node;
                }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                while (true)
                {
                    SkipSpace();
                    if (_pos >= _text.Length) throw Error("the file ends inside an object (a missing '}'?)");
                    if (_text[_pos] == '}') throw Error("a ',' before '}' is not allowed");
                    if (_text[_pos] != '"') throw Error($"expected a field name in \"quotes\", found '{_text[_pos]}'");
                    int keyLine = _line, keyColumn = _pos - _lineStart + 1;
                    string key = ParseString();
                    if (!seen.Add(key)) throw new StatJsonException(_file, keyLine, keyColumn, $"the field \"{key}\" is given twice");
                    SkipSpace();
                    if (_pos >= _text.Length || _text[_pos] != ':') throw Error($"expected ':' after \"{key}\"");
                    _pos++;
                    JsonNode value = ParseValue(depth + 1);
                    node.Fields.Add(new KeyValuePair<string, JsonNode>(key, value));
                    SkipSpace();
                    if (_pos >= _text.Length) throw Error("the file ends inside an object (a missing '}'?)");
                    char c = _text[_pos];
                    if (c == ',') { _pos++; continue; }
                    if (c == '}') { _pos++; return node; }
                    throw Error($"expected ',' or '}}' after the value of \"{key}\", found '{c}' (a missing comma on the line before?)");
                }
            }

            private JsonNode ParseArray(int depth)
            {
                var node = new JsonNode { Kind = JsonKind.Array, Line = _line, Items = new List<JsonNode>() };
                _pos++; // [
                SkipSpace();
                if (_pos < _text.Length && _text[_pos] == ']')
                {
                    _pos++;
                    return node;
                }
                while (true)
                {
                    SkipSpace();
                    if (_pos < _text.Length && _text[_pos] == ']') throw Error("a ',' before ']' is not allowed");
                    node.Items.Add(ParseValue(depth + 1));
                    SkipSpace();
                    if (_pos >= _text.Length) throw Error("the file ends inside a list (a missing ']'?)");
                    char c = _text[_pos];
                    if (c == ',') { _pos++; continue; }
                    if (c == ']') { _pos++; return node; }
                    throw Error($"expected ',' or ']' in a list, found '{c}' (a missing comma?)");
                }
            }

            private string ParseString()
            {
                _pos++; // opening quote
                var value = new StringBuilder();
                while (true)
                {
                    if (_pos >= _text.Length) throw Error("text in quotes is never closed (a missing '\"')");
                    char c = _text[_pos++];
                    if (c == '"') return value.ToString();
                    if (c == '\n' || c == '\r') { _pos--; throw Error("text in quotes is never closed on its line (a missing '\"')"); }
                    if (c < ' ') { _pos--; throw Error("a control character inside quotes"); }
                    if (c != '\\')
                    {
                        value.Append(c);
                        continue;
                    }
                    if (_pos >= _text.Length) throw Error("text in quotes is never closed");
                    char e = _text[_pos++];
                    switch (e)
                    {
                        case '"': value.Append('"'); break;
                        case '\\': value.Append('\\'); break;
                        case '/': value.Append('/'); break;
                        case 'b': value.Append('\b'); break;
                        case 'f': value.Append('\f'); break;
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _text.Length || !int.TryParse(_text.Substring(_pos, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                                throw Error("\\u needs four hex digits");
                            value.Append((char)code);
                            _pos += 4;
                            break;
                        default:
                            _pos--;
                            throw Error($"'\\{e}' is not a JSON escape (write a backslash as \\\\)");
                    }
                }
            }

            private string ParseNumber()
            {
                int start = _pos;
                if (_text[_pos] == '-') _pos++;
                if (_pos >= _text.Length || !IsDigit(_text[_pos])) throw Error("a '-' must be followed by digits");
                if (_text[_pos] == '0' && _pos + 1 < _text.Length && IsDigit(_text[_pos + 1])) throw Error("a number cannot start with 0 (write 7, not 07)");
                while (_pos < _text.Length && IsDigit(_text[_pos])) _pos++;
                if (_pos < _text.Length && _text[_pos] == '.')
                {
                    _pos++;
                    if (_pos >= _text.Length || !IsDigit(_text[_pos])) throw Error("a '.' must be followed by digits (write 1.0 or 1)");
                    while (_pos < _text.Length && IsDigit(_text[_pos])) _pos++;
                }
                if (_pos < _text.Length && (_text[_pos] == 'e' || _text[_pos] == 'E'))
                {
                    _pos++;
                    if (_pos < _text.Length && (_text[_pos] == '+' || _text[_pos] == '-')) _pos++;
                    if (_pos >= _text.Length || !IsDigit(_text[_pos])) throw Error("an exponent needs digits");
                    while (_pos < _text.Length && IsDigit(_text[_pos])) _pos++;
                }
                if (_pos < _text.Length && char.IsLetter(_text[_pos])) throw Error($"unexpected '{_text[_pos]}' after a number");
                return _text.Substring(start, _pos - start);
            }

            private static bool IsDigit(char c) => c >= '0' && c <= '9';
        }
    }
}
