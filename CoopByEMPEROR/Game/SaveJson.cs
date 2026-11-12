using System;
using System.Collections.Generic;
using System.Text;

namespace CoopByEMPEROR.Game
{
    // Мелкая работа с JSON сохранений игры без полного разбора: в них встречаются значения вроде
    // Infinity, которые обычные парсеры не принимают. Объект делится на пары «ключ — сырой текст
    // значения» только на верхнем уровне, вложенное не трогается.
    public static class SaveJson
    {
        public static List<KeyValuePair<string, string>> Split(string json)
        {
            var list = new List<KeyValuePair<string, string>>();
            int i = json.IndexOf('{');
            if (i < 0) throw new FormatException("не объект");
            i++;
            while (true)
            {
                i = SkipWs(json, i);
                if (i >= json.Length || json[i] == '}') break;
                if (json[i] == ',') { i++; continue; }
                int ks = i;
                i = SkipString(json, i);
                string key = Unquote(json.Substring(ks, i - ks));
                i = SkipWs(json, i);
                if (json[i] != ':') throw new FormatException("нет двоеточия после " + key);
                i = SkipWs(json, i + 1);
                int vs = i;
                i = SkipValue(json, i);
                list.Add(new KeyValuePair<string, string>(key, json.Substring(vs, i - vs).Trim()));
            }
            return list;
        }

        public static string Join(List<KeyValuePair<string, string>> pairs)
        {
            var sb = new StringBuilder("{");
            for (int n = 0; n < pairs.Count; n++)
            {
                if (n > 0) sb.Append(',');
                sb.Append(Quote(pairs[n].Key)).Append(':').Append(pairs[n].Value);
            }
            return sb.Append('}').ToString();
        }

        public static string Get(List<KeyValuePair<string, string>> pairs, string key)
        {
            foreach (var p in pairs) if (p.Key == key) return p.Value;
            return null;
        }

        public static void Set(List<KeyValuePair<string, string>> pairs, string key, string raw)
        {
            for (int n = 0; n < pairs.Count; n++)
                if (pairs[n].Key == key) { pairs[n] = new KeyValuePair<string, string>(key, raw); return; }
            pairs.Add(new KeyValuePair<string, string>(key, raw));
        }

        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public static string Unquote(string raw)
        {
            raw = raw.Trim();
            if (raw.Length < 2 || raw[0] != '"') throw new FormatException("не строка");
            var sb = new StringBuilder();
            for (int i = 1; i < raw.Length - 1; i++)
            {
                char c = raw[i];
                if (c != '\\') { sb.Append(c); continue; }
                char e = raw[++i];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(raw.Substring(i + 1, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        private static int SkipWs(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i;
        }

        private static int SkipString(string s, int i)
        {
            if (s[i] != '"') throw new FormatException("ожидалась строка в позиции " + i);
            i++;
            while (i < s.Length)
            {
                if (s[i] == '\\') { i += 2; continue; }
                if (s[i] == '"') return i + 1;
                i++;
            }
            throw new FormatException("строка не закрыта");
        }

        private static int SkipValue(string s, int i)
        {
            if (s[i] == '"') return SkipString(s, i);
            if (s[i] == '{' || s[i] == '[')
            {
                int depth = 0;
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == '"') { i = SkipString(s, i); continue; }
                    if (c == '{' || c == '[') depth++;
                    else if (c == '}' || c == ']') { depth--; if (depth == 0) return i + 1; }
                    i++;
                }
                throw new FormatException("скобка не закрыта");
            }
            while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']') i++;
            return i;
        }
    }
}
