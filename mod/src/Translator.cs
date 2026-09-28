using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ProbablyStolenZhHant
{
    /// <summary>
    /// 把字串表的一條簡中原文轉成繁中譯文，和 tools/psmod/convert.py 產生 text/zh-Hant 的流程相同：
    /// OpenCC s2tw → 引號改「」『』 → 術語表（長詞優先）；標記語法（&lt;wave&gt;、{0}）原樣保留。
    /// 有逐條修正時，在自動轉換結果上套用修正的片段（自動轉換結果的雜湊要相符）。
    /// mod 附的資料只有 OpenCC 字典、術語表、修正片段，不含遊戲原文；原文在執行時才從字串表讀取。
    /// </summary>
    public sealed class Translator
    {
        public static readonly Regex Markup = new Regex(@"(<[^<>]*>|\{[^{}]*\})", RegexOptions.Compiled);

        readonly OpenCC _cc;
        readonly Dictionary<char, List<KeyValuePair<string, string>>> _terms = new Dictionary<char, List<KeyValuePair<string, string>>>();
        readonly Dictionary<string, (string hash, (int start, int end, string text)[] ops)> _overrides =
            new Dictionary<string, (string, (int, int, string)[])>(StringComparer.Ordinal);
        bool[] _changes;

        /// <summary>簡中缺翻的條目（表/key → 譯文），由 MissingTranslations 在產生字串時補上。</summary>
        public readonly Dictionary<string, string> Fills = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>遊戲原文（或轉換規則）變了、暫停套用的逐條修正（表/key）。</summary>
        public readonly List<string> StaleOverrides = new List<string>();
        public int OverridesApplied;
        public int OverrideCount => _overrides.Count;
        public IEnumerable<string> OverrideNames => _overrides.Keys;
        public OpenCC CC => _cc;
        public int TermCount { get; private set; }
        public int OpenCCEntries => _cc.EntryCount;

        Translator(OpenCC cc) { _cc = cc; }

        public static Translator Load(string dir)
        {
            var t = new Translator(OpenCC.Load(Path.Combine(dir, "opencc")));
            var terms = new Dictionary<string, string>(StringComparer.Ordinal); // 同一個詞列了兩次時，後面的優先（同 Python）
            foreach (var line in File.ReadAllLines(Path.Combine(dir, "terms.tsv"), Encoding.UTF8))
            {
                if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#")) continue;
                var p = line.Split('\t');
                if (p.Length >= 2 && p[0].Length > 0) terms[p[0]] = p[1];
            }
            foreach (var kv in terms)
            {
                if (!t._terms.TryGetValue(kv.Key[0], out var list)) t._terms[kv.Key[0]] = list = new List<KeyValuePair<string, string>>();
                list.Add(kv);
            }
            foreach (var list in t._terms.Values)
                list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length)); // 長詞優先
            t.TermCount = terms.Count;

            // 表<Tab>key<Tab>自動轉換結果的 SHA-256 前 16 碼<Tab>(起點<Tab>終點<Tab>替換文字)…
            foreach (var line in File.ReadAllLines(Path.Combine(dir, "overrides.tsv"), Encoding.UTF8))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split('\t');
                if (p.Length < 3 || (p.Length - 3) % 3 != 0) throw new InvalidDataException($"overrides.tsv 格式錯誤：{line}");
                var ops = new (int, int, string)[(p.Length - 3) / 3];
                for (int i = 0; i < ops.Length; i++)
                    ops[i] = (int.Parse(p[3 + i * 3]), int.Parse(p[4 + i * 3]), Unescape(p[5 + i * 3]));
                t._overrides[p[0] + "/" + p[1]] = (p[2], ops);
            }
            // 表<Tab>key<Tab>譯文<Tab>參照（可以沒有這個檔：缺翻時改用遊戲的英文）
            // 有參照時譯文留空，用字串表裡那條（表/key）的譯文，讀字串表時才填入
            var fills = Path.Combine(dir, "fills.tsv");
            if (File.Exists(fills))
                foreach (var line in File.ReadAllLines(fills, Encoding.UTF8))
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    var p = line.Split('\t');
                    if (p.Length < 3) continue;
                    var name = p[0] + "/" + p[1];
                    if (p.Length >= 4 && p[3].Length > 0)
                    {
                        if (!t._fillRefs.TryGetValue(p[3], out var list)) t._fillRefs[p[3]] = list = new List<string>();
                        list.Add(name);
                    }
                    else t.Fills[name] = Unescape(p[2]);
                }
            return t;
        }

        readonly Dictionary<string, List<string>> _fillRefs = new Dictionary<string, List<string>>(StringComparer.Ordinal); // 被參照的 表/key → 缺翻的 表/key

        /// <summary>參照字串表其他條目的缺翻補譯文數（讀到被參照的條目後才會放進 Fills）。</summary>
        public int FillRefCount { get { int n = 0; foreach (var l in _fillRefs.Values) n += l.Count; return n; } }

        /// <summary>字串表的一條原文 → 譯文。</summary>
        public string Translate(string table, string key, string src)
        {
            var name = table + "/" + key;
            var r = Convert(src);
            if (_overrides.TryGetValue(name, out var ov))
            {
                if (ov.hash == Hash(r)) { OverridesApplied++; r = Patch(r, ov.ops); }
                else StaleOverrides.Add(name);
            }
            if (_fillRefs.TryGetValue(name, out var fills))
                foreach (var f in fills) Fills[f] = r;
            return r;
        }

        static string Patch(string s, (int start, int end, string text)[] ops)
        {
            var sb = new StringBuilder(s.Length);
            int done = 0;
            foreach (var (start, end, text) in ops) // 由前往後、互不重疊
            {
                sb.Append(s, done, start - done).Append(text);
                done = end;
            }
            return sb.Append(s, done, s.Length - done).ToString();
        }

        /// <summary>簡中 → 繁中，標記語法原樣保留。</summary>
        public string Convert(string s) => MapText(s, ConvertPlain);

        /// <summary>不分標記，整段轉換：OpenCC → 引號 → 術語表。</summary>
        public string ConvertPlain(string s) => s.Length == 0 ? s : Terms(Punct(_cc.Convert(s)));

        /// <summary>這個字單獨轉換時會變（CJK 範圍內）；TextConverter 用來找出「簡體專用字」。</summary>
        public bool Changes(char c)
        {
            if (_changes == null)
            {
                var changes = new bool[char.MaxValue + 1];
                foreach (var (lo, hi) in CjkRanges)
                    for (int cp = lo; cp <= hi; cp++)
                    {
                        var s = ((char)cp).ToString();
                        changes[cp] = ConvertPlain(s) != s;
                    }
                _changes = changes;
            }
            return _changes[c];
        }

        static readonly (int, int)[] CjkRanges = { (0x3400, 0x4DBF), (0x4E00, 0x9FFF), (0xF900, 0xFAFF) };

        static string MapText(string s, Func<string, string> f)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var parts = Markup.Split(s);
            if (parts.Length == 1) return f(s);
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < parts.Length; i++)
                sb.Append(i % 2 == 1 ? parts[i] : f(parts[i]));
            return sb.ToString();
        }

        static string Punct(string s)
        {
            if (s.IndexOfAny(Quotes) < 0) return s;
            var cs = s.ToCharArray();
            for (int i = 0; i < cs.Length; i++)
                switch (cs[i])
                {
                    case '“': cs[i] = '「'; break;
                    case '”': cs[i] = '」'; break;
                    case '‘': cs[i] = '『'; break;
                    case '’': cs[i] = '』'; break;
                }
            return new string(cs);
        }

        static readonly char[] Quotes = { '“', '”', '‘', '’' };

        /// <summary>術語表：由左到右，每個位置取最長的詞，換完從詞尾繼續（同 Python 的 re.sub）。</summary>
        string Terms(string s)
        {
            if (s.Length == 0 || _terms.Count == 0) return s;
            StringBuilder sb = null;
            int done = 0, i = 0;
            while (i < s.Length)
            {
                if (_terms.TryGetValue(s[i], out var list))
                    foreach (var kv in list)
                        if (kv.Key.Length <= s.Length - i && string.CompareOrdinal(s, i, kv.Key, 0, kv.Key.Length) == 0)
                        {
                            sb ??= new StringBuilder(s.Length);
                            sb.Append(s, done, i - done).Append(kv.Value);
                            i += kv.Key.Length;
                            done = i;
                            goto next;
                        }
                i++;
            next:;
            }
            return sb == null ? s : sb.Append(s, done, s.Length - done).ToString();
        }

        /// <summary>SHA-256（UTF-8）前 16 個十六進位字；逐條修正用它確認自動轉換結果和製作時相同，不必附上原文。</summary>
        public static string Hash(string s)
        {
            using var sha = SHA256.Create();
            var h = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            var sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2"));
            return sb.ToString();
        }

        public static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '\\' || i + 1 == s.Length) { sb.Append(s[i]); continue; }
                char n = s[++i];
                sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n == 'r' ? '\r' : n);
            }
            return sb.ToString();
        }
    }
}
