using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StreamChatAI.LiveSplit.Core
{
    /// <summary>
    /// Just enough JSON for this component. LiveSplit loads components from a
    /// single folder, so a NuGet dependency would mean shipping - and version
    /// clashing - a second DLL beside ours.
    /// </summary>
    public static class Json
    {
        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case IDictionary<string, object> map:
                    sb.Append('{');
                    var first = true;
                    foreach (var pair in map)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, pair.Key);
                        sb.Append(':');
                        Write(sb, pair.Value);
                    }
                    sb.Append('}');
                    break;
                case IEnumerable list:
                    sb.Append('[');
                    var firstItem = true;
                    foreach (var item in list)
                    {
                        if (!firstItem) sb.Append(',');
                        firstItem = false;
                        Write(sb, item);
                    }
                    sb.Append(']');
                    break;
                default:
                    WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        /// <summary>
        /// Reads the top-level string, number and boolean fields of a JSON
        /// object. Nested values are skipped: no response this component reads
        /// needs them.
        /// </summary>
        public static Dictionary<string, string> ParseFlatObject(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json))
            {
                return result;
            }

            var pos = 0;
            SkipWhitespace(json, ref pos);
            if (pos >= json.Length || json[pos] != '{')
            {
                return result;
            }
            pos++;

            while (pos < json.Length)
            {
                SkipWhitespace(json, ref pos);
                if (pos < json.Length && json[pos] == '}') break;
                if (pos >= json.Length || json[pos] != '"') break;

                var key = ReadString(json, ref pos);
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length || json[pos] != ':') break;
                pos++;
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length) break;

                var c = json[pos];
                if (c == '"')
                {
                    result[key] = ReadString(json, ref pos);
                }
                else if (c == '{' || c == '[')
                {
                    SkipNested(json, ref pos);
                }
                else
                {
                    var start = pos;
                    while (pos < json.Length && json[pos] != ',' && json[pos] != '}' && !char.IsWhiteSpace(json[pos])) pos++;
                    var raw = json.Substring(start, pos - start);
                    if (raw != "null") result[key] = raw;
                }

                SkipWhitespace(json, ref pos);
                if (pos < json.Length && json[pos] == ',') pos++;
            }

            return result;
        }

        private static void SkipWhitespace(string json, ref int pos)
        {
            while (pos < json.Length && char.IsWhiteSpace(json[pos])) pos++;
        }

        private static string ReadString(string json, ref int pos)
        {
            var sb = new StringBuilder();
            pos++; // opening quote
            while (pos < json.Length)
            {
                var c = json[pos++];
                if (c == '"') break;
                if (c != '\\' || pos >= json.Length)
                {
                    sb.Append(c);
                    continue;
                }

                var escaped = json[pos++];
                switch (escaped)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (pos + 4 <= json.Length && int.TryParse(json.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        {
                            sb.Append((char)code);
                            pos += 4;
                        }
                        break;
                    default: sb.Append(escaped); break;
                }
            }
            return sb.ToString();
        }

        private static void SkipNested(string json, ref int pos)
        {
            var depth = 0;
            while (pos < json.Length)
            {
                var c = json[pos];
                if (c == '"')
                {
                    ReadString(json, ref pos);
                    continue;
                }
                if (c == '{' || c == '[') depth++;
                if (c == '}' || c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        pos++;
                        return;
                    }
                }
                pos++;
            }
        }
    }
}
