using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NowPlaying
{
    /// <summary>
    /// Крошечный JSON-парсер. Нужен, чтобы читать файлы плееров, которые
    /// не отдают метаданные в SMTC. Возвращает Dictionary&lt;string,object&gt;,
    /// List&lt;object&gt;, string, double, bool или null.
    /// </summary>
    static class Json
    {
        public static object Parse(string s)
        {
            int i = 0;
            object v = ParseValue(s, ref i);
            return v;
        }

        /// <summary>Достаёт значение по цепочке ключей/индексов. null, если пути нет.</summary>
        public static object Path(object root, params object[] keys)
        {
            object cur = root;
            foreach (object k in keys)
            {
                if (cur == null) return null;
                var d = cur as Dictionary<string, object>;
                if (d != null)
                {
                    string key = k as string;
                    if (key == null || !d.ContainsKey(key)) return null;
                    cur = d[key];
                    continue;
                }
                var l = cur as List<object>;
                if (l != null && k is int)
                {
                    int idx = (int)k;
                    if (idx < 0 || idx >= l.Count) return null;
                    cur = l[idx];
                    continue;
                }
                return null;
            }
            return cur;
        }

        public static string Str(object root, params object[] keys)
        {
            object v = Path(root, keys);
            if (v == null) return "";
            var s = v as string;
            if (s != null) return s;
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static double Num(object root, params object[] keys)
        {
            object v = Path(root, keys);
            if (v == null) return 0;
            if (v is double) return (double)v;
            double d;
            if (double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
            return 0;
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') i++;
                else break;
            }
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') { i += 4; return true; }
            if (c == 'f') { i += 5; return false; }
            if (c == 'n') { i += 4; return null; }
            return ParseNumber(s, ref i);
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;                       // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (i < s.Length)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') break;
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                object val = ParseValue(s, ref i);
                d[key] = val;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; break; }
                break;
            }
            return d;
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var l = new List<object>();
            i++;                       // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (i < s.Length)
            {
                object val = ParseValue(s, ref i);
                l.Add(val);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; break; }
                break;
            }
            return l;
        }

        static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;                       // "
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '/': sb.Append('/'); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    case 'u':
                        if (i + 4 <= s.Length)
                        {
                            int code;
                            if (int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture, out code))
                                sb.Append((char)code);
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length)
            {
                char c = s[i];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') i++;
                else break;
            }
            double d;
            if (double.TryParse(s.Substring(start, i - start), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out d)) return d;
            return 0.0;
        }
    }
}
