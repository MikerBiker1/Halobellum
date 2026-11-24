// NetJson.cs
// A tiny JSON reader and writer for the LAN messages. A message is one JSON object; its values can be strings, numbers, true,
// false, null, nested objects and arrays. Numbers keep their text exactly as written (JsonNumber), so nothing is lost on the way:
// whole numbers are written as they are, a float with 9 significant digits and a double with 17, which read back to the very same
// bits on every machine (the lockstep relies on that). Everything is written and read with the invariant culture.
// The core's StatJson is not used on purpose: the transport compiles on its own, without the rest of the game.
// The reader refuses text that would cost a lot to hold (LanLimits): more than MaxValues values (a megabyte of "[{},{},...]" would
// otherwise become hundreds of megabytes of objects), a text longer than MaxChars (so no string in it is longer either), or
// nesting deeper than MaxDepth.
// Plain C# 9 without the engine.
// Depends on: LanTimings (LanLimits).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Halobellum.Net
{
    /// <summary>A JSON number, kept as its exact text.</summary>
    public sealed class JsonNumber
    {
        public readonly string Text;

        public JsonNumber(string text) { Text = text; }

        public override string ToString() => Text;
    }

    /// <summary>
    /// A JSON object: keys in the order they were set, values of these kinds only: string, JsonNumber, bool, null,
    /// JsonObject and List&lt;object&gt; (an array of any of these).
    /// </summary>
    public class JsonObject
    {
        private readonly List<string> keys = new List<string>();
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();

        public int Count => keys.Count;
        public IReadOnlyList<string> Keys => keys;

        public bool Has(string key) => values.ContainsKey(key);

        /// <summary>The raw value (string, JsonNumber, bool, null, JsonObject or List&lt;object&gt;), or null when missing.</summary>
        public object Get(string key) => values.TryGetValue(key, out object value) ? value : null;

        public bool Remove(string key)
        {
            if (!values.Remove(key)) return false;
            keys.Remove(key);
            return true;
        }

        /// <summary>Sets a raw value. It must be one of the kinds the class allows (see above).</summary>
        public JsonObject SetRaw(string key, object value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            CheckKind(value);
            if (!values.ContainsKey(key)) keys.Add(key);
            values[key] = value;
            return this;
        }

        public JsonObject Set(string key, string value) => SetRaw(key, value);
        public JsonObject Set(string key, bool value) => SetRaw(key, value);
        public JsonObject Set(string key, int value) => SetRaw(key, new JsonNumber(value.ToString(CultureInfo.InvariantCulture)));
        public JsonObject Set(string key, long value) => SetRaw(key, new JsonNumber(value.ToString(CultureInfo.InvariantCulture)));
        /// <summary>
        /// A 64-bit unsigned number (a state hash, a content fingerprint), written as its exact whole number. Without this overload
        /// C# would quietly pick Set(float) for a ulong and lose most of its digits.
        /// </summary>
        public JsonObject Set(string key, ulong value) => SetRaw(key, new JsonNumber(value.ToString(CultureInfo.InvariantCulture)));
        public JsonObject Set(string key, JsonObject value) => SetRaw(key, value);

        /// <summary>A float, written with 9 significant digits: it reads back to exactly the same float.</summary>
        public JsonObject Set(string key, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("JSON has no NaN or infinity: " + key);
            return SetRaw(key, new JsonNumber(value.ToString("G9", CultureInfo.InvariantCulture)));
        }

        /// <summary>A double, written with 17 significant digits: it reads back to exactly the same double.</summary>
        public JsonObject Set(string key, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("JSON has no NaN or infinity: " + key);
            return SetRaw(key, new JsonNumber(value.ToString("G17", CultureInfo.InvariantCulture)));
        }

        public JsonObject Set(string key, IEnumerable<string> list)
        {
            var array = new List<object>();
            if (list != null) foreach (string s in list) array.Add(s);
            return SetRaw(key, array);
        }

        public JsonObject Set(string key, IEnumerable<JsonObject> list)
        {
            var array = new List<object>();
            if (list != null) foreach (JsonObject o in list) array.Add(o);
            return SetRaw(key, array);
        }

        public JsonObject SetNull(string key) => SetRaw(key, null);

        public string GetString(string key, string fallback = null) => Get(key) as string ?? fallback;

        public bool GetBool(string key, bool fallback = false) => Get(key) is bool b ? b : fallback;

        public int GetInt(string key, int fallback = 0)
        {
            return Get(key) is JsonNumber n && int.TryParse(n.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
        }

        public long GetLong(string key, long fallback = 0)
        {
            return Get(key) is JsonNumber n && long.TryParse(n.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : fallback;
        }

        public ulong GetULong(string key, ulong fallback = 0)
        {
            return Get(key) is JsonNumber n && ulong.TryParse(n.Text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong v) ? v : fallback;
        }

        public float GetFloat(string key, float fallback = 0f)
        {
            return Get(key) is JsonNumber n && float.TryParse(n.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
        }

        public double GetDouble(string key, double fallback = 0.0)
        {
            return Get(key) is JsonNumber n && double.TryParse(n.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }

        public JsonObject GetObject(string key) => Get(key) as JsonObject;

        /// <summary>The raw array, or null when missing or not an array.</summary>
        public List<object> GetArray(string key) => Get(key) as List<object>;

        /// <summary>The strings of an array (other kinds of entries are skipped); an empty list when missing.</summary>
        public List<string> GetStringList(string key)
        {
            var result = new List<string>();
            List<object> array = GetArray(key);
            if (array != null) foreach (object o in array) if (o is string s) result.Add(s);
            return result;
        }

        /// <summary>The objects of an array (other kinds of entries are skipped); an empty list when missing.</summary>
        public List<JsonObject> GetObjectList(string key)
        {
            var result = new List<JsonObject>();
            List<object> array = GetArray(key);
            if (array != null) foreach (object o in array) if (o is JsonObject j) result.Add(j);
            return result;
        }

        /// <summary>This object as compact JSON text.</summary>
        public string ToJson() => NetJson.Write(this);

        public override string ToString() => ToJson();

        private static void CheckKind(object value)
        {
            if (value == null || value is string || value is JsonNumber || value is bool || value is JsonObject) return;
            if (value is List<object> list)
            {
                foreach (object o in list) CheckKind(o);
                return;
            }
            throw new ArgumentException("A JSON value must be a string, JsonNumber, bool, null, JsonObject or List<object>, not " + value.GetType().Name);
        }
    }

    /// <summary>Writes and reads the JSON text of a JsonObject.</summary>
    public static class NetJson
    {
        /// <summary>How deep objects and arrays may nest when reading; deeper input is refused.</summary>
        public const int MaxDepth = 32;

        /// <summary>The most values (objects, arrays, strings, numbers, true, false, null) one text may hold when reading.</summary>
        public const int MaxValues = LanLimits.MaxJsonValues;

        /// <summary>The longest text read, in characters (so no string or number in it is longer).</summary>
        public const int MaxChars = LanLimits.MaxJsonChars;

        public static string Write(JsonObject value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        /// <summary>Reads one JSON object (and nothing after it). Throws FormatException, with the position, on bad input.</summary>
        public static JsonObject ParseObject(string text)
        {
            if (text == null) throw new FormatException("No JSON text.");
            if (text.Length > MaxChars) throw new FormatException("JSON text of " + text.Length + " characters is over the limit of " + MaxChars + ".");
            var reader = new Reader(text);
            reader.SkipSpace();
            if (reader.Peek() != '{') throw reader.Error("expected '{'");
            var result = (JsonObject)reader.ReadValue(0);
            reader.SkipSpace();
            if (!reader.AtEnd) throw reader.Error("unexpected text after the object");
            return result;
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case JsonNumber n: sb.Append(n.Text); break;
                case JsonObject o:
                    sb.Append('{');
                    for (int i = 0; i < o.Keys.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        WriteString(sb, o.Keys[i]);
                        sb.Append(':');
                        WriteValue(sb, o.Get(o.Keys[i]));
                    }
                    sb.Append('}');
                    break;
                case List<object> list:
                    sb.Append('[');
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        WriteValue(sb, list[i]);
                    }
                    sb.Append(']');
                    break;
                default: throw new ArgumentException("Cannot write a " + value.GetType().Name + " as JSON.");
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        bool lonely = char.IsHighSurrogate(c) ? !(i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                            : char.IsLowSurrogate(c) && !(i > 0 && char.IsHighSurrogate(s[i - 1]));
                        // Control characters, and a half of a surrogate pair standing alone (UTF-8 cannot carry it), go as \uXXXX.
                        if (c < 0x20 || lonely) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Reader
        {
            private readonly string text;
            private int pos;
            private int values; // values read so far (MaxValues)

            public Reader(string text) { this.text = text; }

            public bool AtEnd => pos >= text.Length;

            public char Peek() => pos < text.Length ? text[pos] : '\0';

            public FormatException Error(string what) => new FormatException("JSON at character " + pos + ": " + what + ".");

            public void SkipSpace()
            {
                while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\t' || text[pos] == '\n' || text[pos] == '\r')) pos++;
            }

            public object ReadValue(int depth)
            {
                if (depth > MaxDepth) throw Error("nested too deeply");
                if (++values > MaxValues) throw Error("more than " + MaxValues + " values");
                SkipSpace();
                if (AtEnd) throw Error("unexpected end");
                char c = text[pos];
                if (c == '{') return ReadObject(depth);
                if (c == '[') return ReadArray(depth);
                if (c == '"') return ReadString();
                if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                if (Word("true")) return true;
                if (Word("false")) return false;
                if (Word("null")) return null;
                throw Error("unexpected '" + c + "'");
            }

            private bool Word(string word)
            {
                if (string.CompareOrdinal(text, pos, word, 0, word.Length) != 0) return false;
                pos += word.Length;
                return true;
            }

            private JsonObject ReadObject(int depth)
            {
                var result = new JsonObject();
                pos++; // '{'
                SkipSpace();
                if (Peek() == '}') { pos++; return result; }
                while (true)
                {
                    SkipSpace();
                    if (Peek() != '"') throw Error("expected a key in quotes");
                    string key = ReadString();
                    SkipSpace();
                    if (Peek() != ':') throw Error("expected ':'");
                    pos++;
                    result.SetRaw(key, ReadValue(depth + 1));
                    SkipSpace();
                    char c = Peek();
                    pos++;
                    if (c == ',') continue;
                    if (c == '}') return result;
                    pos--;
                    throw Error("expected ',' or '}'");
                }
            }

            private List<object> ReadArray(int depth)
            {
                var result = new List<object>();
                pos++; // '['
                SkipSpace();
                if (Peek() == ']') { pos++; return result; }
                while (true)
                {
                    result.Add(ReadValue(depth + 1));
                    SkipSpace();
                    char c = Peek();
                    pos++;
                    if (c == ',') continue;
                    if (c == ']') return result;
                    pos--;
                    throw Error("expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                pos++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = text[pos++];
                    if (c == '"') return sb.ToString();
                    if (c < 0x20) { pos--; throw Error("control character in a string"); }
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("unterminated string");
                    char e = text[pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 > text.Length
                                || !int.TryParse(text.Substring(pos, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                                throw Error("bad \\u escape");
                            sb.Append((char)code);
                            pos += 4;
                            break;
                        default: pos--; throw Error("bad escape '\\" + e + "'");
                    }
                }
            }

            private JsonNumber ReadNumber()
            {
                int start = pos;
                if (Peek() == '-') pos++;
                if (Peek() == '0') pos++;
                else if (Peek() >= '1' && Peek() <= '9') { while (Peek() >= '0' && Peek() <= '9') pos++; }
                else throw Error("bad number");
                if (Peek() == '.')
                {
                    pos++;
                    if (!(Peek() >= '0' && Peek() <= '9')) throw Error("bad number");
                    while (Peek() >= '0' && Peek() <= '9') pos++;
                }
                if (Peek() == 'e' || Peek() == 'E')
                {
                    pos++;
                    if (Peek() == '+' || Peek() == '-') pos++;
                    if (!(Peek() >= '0' && Peek() <= '9')) throw Error("bad number");
                    while (Peek() >= '0' && Peek() <= '9') pos++;
                }
                return new JsonNumber(text.Substring(start, pos - start));
            }
        }
    }
}
