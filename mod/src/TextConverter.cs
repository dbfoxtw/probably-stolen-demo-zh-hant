using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ProbablyStolenZhHant
{
    /// <summary>
    /// 顯示層的簡轉繁：只在文字要顯示時轉換，不動字串表與存檔（遊戲內部仍是簡體，存檔可自由拔除 mod）。
    /// 順序：整句對照 → 佔位符樣板 → 短詞分詞＋OpenCC＋術語表。已經是繁體的字串不會再轉。
    /// 整句對照是執行時從遊戲的簡中字串表逐條翻譯（Translator）建立的，發布的檔案不含遊戲原文。
    /// </summary>
    public sealed class TextConverter
    {
        static readonly Regex Markup = Translator.Markup;

        readonly Translator _tr;
        readonly Dictionary<string, string> _exact = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _exactTrim = new Dictionary<string, string>(StringComparer.Ordinal); // 去掉前後空白的版本
        readonly HashSet<string> _keyPrefixes = new HashSet<string>(StringComparer.Ordinal);
        readonly List<Template> _templates = new List<Template>();
        readonly List<Template> _plainTemplates = new List<Template>(); // 字面沒有簡體專用字的樣板，見 ConvertCore
        readonly Dictionary<char, List<KeyValuePair<string, string>>> _phrases = new Dictionary<char, List<KeyValuePair<string, string>>>();
        readonly bool[] _trigger = new bool[char.MaxValue + 1];
        readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly HashSet<string> _unchanged = new HashSet<string>(StringComparer.Ordinal); // 轉換前後相同的短字串（例如「胡安」），認說話者用

        TextConverter(Translator tr) { _tr = tr; }

        /// <summary>整句對照查不到、靠後備轉換的字串（原文 → 結果），用來檢查漏網之魚。</summary>
        public readonly Dictionary<string, string> Misses = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>寫死在程式碼裡的英文（text/hardcoded.tsv）：觸發字串、正規表示式、替換文字。</summary>
        readonly List<(string trigger, Regex re, string repl)> _hardcoded = new List<(string, Regex, string)>();

        /// <summary>遊戲語言是中文時才翻譯寫死的英文（由 mod 依目前語言設定）。</summary>
        public bool ChineseActive;

        public long Calls, ExactHits, TemplateHits, FallbackHits, CacheHits, Skipped, HardcodedHits, Ticks;
        public int ExactCount => _exact.Count;
        public int TemplateCount => _templates.Count;
        public int TriggerCount { get; private set; }
        public int PhraseCount { get; private set; }
        /// <summary>建立時翻譯的字串表條目數；同一句原文有兩種譯文、只能留一種的次數。</summary>
        public int EntryCount { get; private set; }
        public int Conflicts { get; private set; }

        // TextMeshPro 內建標籤；其他 <...> 視為 Text Animator 標籤（Text Animator 會先拿掉自己的標籤再交給 TMP）
        static readonly HashSet<string> TmpTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "a", "align", "allcaps", "alpha", "b", "br", "color", "cspace", "font", "font-weight", "gradient", "i",
            "indent", "line-height", "line-indent", "link", "lowercase", "margin", "mark", "mspace", "nobr",
            "noparse", "page", "pos", "rotate", "s", "size", "smallcaps", "space", "sprite", "strikethrough",
            "style", "sub", "sup", "u", "uppercase", "voffset", "width",
        };
        static readonly Regex TagRe = new Regex(@"<\s*/?\s*([A-Za-z\-]+)[^<>]*>", RegexOptions.Compiled);
        static readonly Regex PhraseExclude = new Regex(@"[<>{}\n。！？，…、；：]", RegexOptions.Compiled);

        static string StripAnimTags(string s) =>
            s.IndexOf('<') < 0 ? s : TagRe.Replace(s, m => TmpTags.Contains(m.Groups[1].Value.ToLowerInvariant()) ? m.Value : "");

        /// <summary>
        /// 由字串表的條目（表、key、簡中原文）建立對照；entries 依表名排序、表內依 key 的順序。
        /// 同一句原文有兩種譯文時，保留第一個（同 tools/export_mod_data.py）。hardcodedPath 可為 null。
        /// </summary>
        public static TextConverter Build(Translator tr, IEnumerable<(string table, string key, string src)> entries, string hardcodedPath)
        {
            var c = new TextConverter(tr);
            var outChars = new HashSet<char>();
            void Add(string s, string d)
            {
                if (s == d && s.Length > 0 && s.Length <= MaxSpeaker) c._unchanged.Add(s);
                if (string.IsNullOrEmpty(s) || s == d) return;
                if (!c._exact.TryAdd(s, d) && c._exact[s] != d) c.Conflicts++;
            }
            foreach (var (table, key, src) in entries)
            {
                if (string.IsNullOrEmpty(src)) continue;
                var dst = tr.Translate(table, key, src);
                c.EntryCount++;
                foreach (var ch in dst) outChars.Add(ch);
                Add(src, dst);
                Add(StripAnimTags(src), StripAnimTags(dst));
            }
            foreach (var f in tr.Fills.Values)
                foreach (var ch in f) outChars.Add(ch);
            Add("简体中文", "繁體中文"); // 語言選單（寫死在程式裡）
            Add("Simplified Chinese", "Traditional Chinese"); // 主選單右上角的語言下拉選單

            // 簡體專用字：單獨轉換會變、而且沒出現在任何譯文裡；含這些字的字串才需要轉換
            for (int ch = 0; ch <= char.MaxValue; ch++)
                if (tr.Changes((char)ch) && !outChars.Contains((char)ch)) { c._trigger[ch] = true; c.TriggerCount++; }

            foreach (var kv in c._exact)
            {
                var k = kv.Key.Trim();
                if (k.Length > 0 && !c._exactTrim.ContainsKey(k)) c._exactTrim[k] = kv.Value.Trim();
                // 拼接比對用的開頭索引（去掉前導空白／符號／括號後的前 1～6 字；樣板取第一個佔位符之前）
                int a = 0;
                while (a < k.Length && "-•·*[(（【「".IndexOf(k[a]) >= 0) a++;
                int end = k.IndexOf('{', a);
                if (end < 0) end = k.Length;
                for (int n = 1; n <= PrefixLen && a + n <= end; n++) c._keyPrefixes.Add(k.Substring(a, n));
                // 短詞（名稱、狀態等）：整句查不到時用來分詞，處理遊戲自己拼接的字串
                if (kv.Key.Length >= 2 && kv.Key.Length <= 12 && !PhraseExclude.IsMatch(kv.Key))
                {
                    AddByFirstChar(c._phrases, kv.Key, kv.Value);
                    c.PhraseCount++;
                }
            }
            if (hardcodedPath != null && File.Exists(hardcodedPath))
                foreach (var line in File.ReadAllLines(hardcodedPath, Encoding.UTF8))
                {
                    if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#")) continue;
                    var p = line.Split('\t');
                    if (p.Length >= 3 && p[0].Length > 0)
                        c._hardcoded.Add((p[0], new Regex(p[1], RegexOptions.CultureInvariant), p[2]));
                }
            foreach (var list in c._phrases.Values)
                list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length)); // 長詞優先
            foreach (var kv in c._exact)
                if (kv.Key.IndexOf('{') >= 0 && Template.TryCreate(kv.Key, kv.Value, out var t))
                    c._templates.Add(t);
            // 字面部分越長的樣板越具體，先比對（避免「…{0}…」把別的樣板的字面也吃進佔位符）
            c._templates.Sort((a, b) => b.LiteralLength.CompareTo(a.LiteralLength));
            // 字面沒有簡體專用字、套用後字面卻會變的樣板（「你下周的租金是{0}。」→「下週」）另外列出，不用轉的字串也要比對
            foreach (var t in c._templates)
                if (t.Anchored && !c.NeedsConversion(t.Literal) && !t.KeepsLiteral) c._plainTemplates.Add(t);
            return c;
        }

        /// <summary>不為 null 時記錄「轉換結果 → 原文」：字串表載入前的暫用轉換器用，載入後把畫面上這些字重新轉換。</summary>
        public Dictionary<string, string> Produced;

        public string Convert(string s)
        {
            var r = ConvertCore(s);
            if (Produced != null && !ReferenceEquals(r, s) && r != s) Produced[r] = s;
            return r;
        }

        string ConvertCore(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            Calls++;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (_exact.TryGetValue(s, out var r)) { ExactHits++; return r; }
                if ((r = TrySpeaker(s)) != null) { ExactHits++; return r; }
                if (ChineseActive && _hardcoded.Count > 0 && ApplyHardcoded(s) is string h) { HardcodedHits++; return h; }
                if (!NeedsConversion(s))
                {
                    // 樣板的字面沒有簡體專用字、填入的又是數字時，整句也沒有（「你下周的租金是{0}。」），仍要套樣板
                    foreach (var t in _plainTemplates)
                        if ((r = t.Apply(s, Convert)) != null) { TemplateHits++; return r; }
                    Skipped++;
                    return s;
                }
                if (_cache.TryGetValue(s, out r)) { CacheHits++; return r; }
                r = LookupFlexible(s, templates: false);
                if (r != null) ExactHits++;
                else if ((r = TryTemplates(s)) != null || (r = TryInner(s)) != null) TemplateHits++;
                else
                {
                    r = ConvertComposite(s);
                    FallbackHits++;
                }
                _cache[s] = r;
                return r;
            }
            finally { Ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>套用寫死英文的翻譯規則；沒有任何規則生效時傳回 null。</summary>
        public string ApplyHardcoded(string s)
        {
            var r = s;
            foreach (var (trigger, re, repl) in _hardcoded)
                if (r.IndexOf(trigger, StringComparison.Ordinal) >= 0) r = re.Replace(r, repl);
            return ReferenceEquals(r, s) || r == s ? null : r;
        }

        /// <summary>含有簡體專用字才需要轉（繁體譯文裡出現過的字都不算）。</summary>
        public bool NeedsConversion(string s)
        {
            foreach (var ch in s)
                if (_trigger[ch]) return true;
            return false;
        }

        /// <summary>anchored：只用前後都有固定文字的樣板（拼接比對時，開頭或結尾就是佔位符的樣板會把別段也吃進去）。</summary>
        string TryTemplates(string s) => TryTemplates(s, anchored: false);

        string TryTemplates(string s, bool anchored)
        {
            foreach (var t in _templates)
            {
                if (anchored && !t.Anchored) continue;
                var r = t.Apply(s, Convert);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>
        /// 遊戲自己拼接的字串（說明框、新聞、夜間報告…）：依標記與換行切段，每段（去掉前後空白與「-」）
        /// 先查整句對照與樣板，都查不到才逐字轉換，並記到 Misses。
        /// </summary>
        public string ConvertComposite(string s)
        {
            // 切成「標記／換行」與「文字片段」；說明框的長句會被遊戲按字數斷行，而且每行各包一組 <color>，
            // 所以連續幾個文字片段（跨標記、跨換行）拼起來查整句，查到就依各片段原本的長度分配回去。
            var tokens = new List<string>();
            var isText = new List<bool>();
            var parts = Markup.Split(s);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 1) { tokens.Add(parts[i]); isText.Add(false); continue; }
                var lines = parts[i].Split('\n');
                for (int j = 0; j < lines.Length; j++)
                {
                    if (j > 0) { tokens.Add("\n"); isText.Add(false); }
                    if (lines[j].Length > 0) { tokens.Add(lines[j]); isText.Add(true); }
                }
            }
            var units = new List<int>();
            for (int i = 0; i < tokens.Count; i++) if (isText[i]) units.Add(i);

            for (int u = 0; u < units.Count; u++)
            {
                // 多筆對話紀錄放在同一個文字物件時，每行各自是「說話者: 台詞」
                if (TrySpeaker(tokens[units[u]]) is string sp) { tokens[units[u]] = sp; continue; }
                if (!NeedsConversion(tokens[units[u]])) continue;
                int last = CouldStartKey(tokens[units[u]]) ? Math.Min(u + MaxJoin, units.Count) - 1 : u;
                int joinedTo = -1;
                foreach (bool templates in new[] { false, true })
                {
                    for (int v = last; v > u && joinedTo < 0; v--)
                    {
                        var sb = new StringBuilder();
                        for (int k = u; k <= v; k++) sb.Append(tokens[units[k]]);
                        var r = LookupFlexible(sb.ToString(), templates, anchored: true);
                        if (r == null) continue;
                        int pos = 0;
                        for (int k = u; k <= v; k++)
                        {
                            int len = k == v ? r.Length - pos : Math.Min(tokens[units[k]].Length, r.Length - pos);
                            tokens[units[k]] = r.Substring(pos, len);
                            pos += len;
                        }
                        joinedTo = v;
                    }
                    if (joinedTo >= 0) break;
                }
                if (joinedTo >= 0) u = joinedTo;
                else tokens[units[u]] = ConvertLine(tokens[units[u]]);
            }
            return string.Concat(tokens);
        }

        static readonly Regex OuterTags = new Regex(@"^((?:<[^<>/][^<>]*>)+)(.*?)((?:</[^<>]+>)+)$", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>遊戲在整句外面又包了標籤（例如 &lt;color&gt;），剝掉外層再查整句與樣板。</summary>
        string TryInner(string s)
        {
            var m = OuterTags.Match(s);
            if (!m.Success || m.Groups[2].Length == 0) return null;
            var inner = m.Groups[2].Value;
            var r = LookupFlexible(inner, templates: false) ?? TryTemplates(inner);
            return r == null ? null : m.Groups[1].Value + r + m.Groups[3].Value;
        }

        const int MaxJoin = 40;
        const int PrefixLen = 6;

        /// <summary>這段文字（去掉前導空白、項目符號、括號後）的開頭，是否可能是某句整句對照的開頭；不是就不必嘗試拼接。</summary>
        bool CouldStartKey(string t)
        {
            int a = 0;
            while (a < t.Length && (char.IsWhiteSpace(t[a]) || "-•·*[(（【「".IndexOf(t[a]) >= 0)) a++;
            if (a >= t.Length) return false;
            var p = t.Substring(a, Math.Min(PrefixLen, t.Length - a));
            return _keyPrefixes.Contains(p) || _keyPrefixes.Contains(t.Substring(0, Math.Min(PrefixLen, t.Length)));
        }

        // 由淺到深：只去空白 → 再去項目符號 → 再去「*」（原文本身可能就是「*動作描述*」）
        static readonly string[] TrimLevels = { "", "-•·", "-•·*" };
        const string Brackets = "[]()（）【】「」";

        string ConvertLine(string line)
        {
            if (line.Length == 0 || !NeedsConversion(line)) return line;
            // 先查所有整句對照的變體，再查樣板：寬鬆的樣板（開頭或結尾就是佔位符）可能先吃到這行，把沒對上的部分丟給逐字轉換
            var r = LookupFlexible(line, templates: false) ?? LookupFlexible(line, templates: true);
            if (r != null) return r;
            r = Fallback(line);
            Misses[line] = r;
            return r;
        }

        const string SpeakerSep = ": ";
        const int MaxSpeaker = 20;

        /// <summary>
        /// 錄音機的對話紀錄是「說話者: 台詞」一行。整行查不到整句對照，會走後備轉換而套不到逐條修正（例如「妥了。」）；
        /// 名字若沒有簡體專用字（「游客」），整行還可能被當成不用轉。所以說話者是字串表裡的整句（或寫死英文的
        /// 「Player」）時，名字查整句、台詞當成獨立的一句轉換。字串表的原文沒有「短字串: 」開頭的，不會誤拆。
        /// </summary>
        string TrySpeaker(string s)
        {
            // 實機的格式是「 导师: …」，開頭有一個空格
            int a = 0;
            while (a < s.Length && char.IsWhiteSpace(s[a])) a++;
            int i = s.IndexOf(SpeakerSep, a, StringComparison.Ordinal);
            if (i <= a || i - a > MaxSpeaker || s.IndexOf('\n') >= 0) return null;
            var name = s.Substring(a, i - a);
            if (!_exact.TryGetValue(name, out var n))
            {
                if (_unchanged.Contains(name)) n = name;
                else if (!ChineseActive || (n = ApplyHardcoded(name)) == null) return null;
            }
            return s.Substring(0, a) + n + SpeakerSep + Convert(s.Substring(i + SpeakerSep.Length));
        }

        /// <summary>
        /// 整行、去掉前後空白、再去掉「-」「*」等符號，各自查一次；每一層也試著剝一對括號
        /// （例如新聞結尾、說明框裡用方括號包住的標籤）。空白差異用「去空白」的對照表比對，
        /// 因為原文本身也可能以空白開頭結尾。查到就把原本的前後綴接回去。
        /// </summary>
        string LookupFlexible(string line, bool templates, bool anchored = false)
        {
            Func<string, string> find = templates
                ? x => TryTemplates(x, anchored)
                : s => _exact.TryGetValue(s, out var v) ? v : _exactTrim.TryGetValue(s, out v) ? v : null;
            var r = find(line);
            if (r != null) return r;
            foreach (var set in TrimLevels)
            {
                int a = 0, b = line.Length;
                while (a < b && (char.IsWhiteSpace(line[a]) || set.IndexOf(line[a]) >= 0)) a++;
                while (b > a && (char.IsWhiteSpace(line[b - 1]) || set.IndexOf(line[b - 1]) >= 0)) b--;
                if (b <= a) continue;
                if (a > 0 || b < line.Length)
                {
                    r = find(line.Substring(a, b - a));
                    if (r != null) return line.Substring(0, a) + r + line.Substring(b);
                }
                int k = Brackets.IndexOf(line[a]);
                if (b - a > 2 && k >= 0 && k % 2 == 0 && line[b - 1] == Brackets[k + 1])
                {
                    int ia = a + 1, ib = b - 1;
                    while (ia < ib && char.IsWhiteSpace(line[ia])) ia++;
                    while (ib > ia && char.IsWhiteSpace(line[ib - 1])) ib--;
                    r = find(line.Substring(ia, ib - ia));
                    if (r != null) return line.Substring(0, ia) + r + line.Substring(ib);
                }
            }
            return null;
        }

        /// <summary>後備轉換：標記原樣保留；其餘用短詞分詞，詞以外的部分用 OpenCC 轉換再套術語表。</summary>
        public string Fallback(string s)
        {
            var parts = Markup.Split(s);
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 1) { sb.Append(parts[i]); continue; }
                ConvertPlain(parts[i], sb);
            }
            return sb.ToString();
        }

        void ConvertPlain(string s, StringBuilder sb)
        {
            int gap = 0, i = 0;
            while (i < s.Length)
            {
                var hit = LongestMatch(_phrases, s, i);
                if (hit.Key == null) { i++; continue; }
                sb.Append(_tr.ConvertPlain(s.Substring(gap, i - gap)));
                sb.Append(hit.Value);
                i += hit.Key.Length;
                gap = i;
            }
            sb.Append(_tr.ConvertPlain(s.Substring(gap)));
        }

        static KeyValuePair<string, string> LongestMatch(Dictionary<char, List<KeyValuePair<string, string>>> dict, string s, int i)
        {
            if (dict.TryGetValue(s[i], out var list))
                foreach (var kv in list)
                    if (kv.Key.Length <= s.Length - i && string.CompareOrdinal(s, i, kv.Key, 0, kv.Key.Length) == 0)
                        return kv;
            return default;
        }

        static void AddByFirstChar(Dictionary<char, List<KeyValuePair<string, string>>> d, string s, string v)
        {
            if (!d.TryGetValue(s[0], out var list)) d[s[0]] = list = new List<KeyValuePair<string, string>>();
            list.Add(new KeyValuePair<string, string>(s, v));
        }

        public void LoadMisses(string path)
        {
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                int tab = line.IndexOf('\t');
                if (tab > 0) Misses[line.Substring(0, tab).Replace("⏎", "\n")] = line.Substring(tab + 1).Replace("⏎", "\n");
            }
        }

        public void WriteMisses(string path)
        {
            var sb = new StringBuilder();
            foreach (var kv in Misses)
                sb.Append(kv.Key.Replace("\n", "⏎")).Append('\t').Append(kv.Value.Replace("\n", "⏎")).Append('\n');
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        public string Stats()
        {
            double ms = Ticks * 1000.0 / Stopwatch.Frequency;
            return $"呼叫 {Calls}（整句 {ExactHits}、樣板 {TemplateHits}、後備 {FallbackHits}、快取 {CacheHits}、寫死英文 {HardcodedHits}、免轉 {Skipped}），累計 {ms:F1} ms";
        }

        /// <summary>含佔位符的整句：原文的字面部分要完全相符，佔位符的內容（名字、數字）再轉換後填進譯文。</summary>
        sealed class Template
        {
            readonly Regex _re;
            readonly string _prefix, _suffix;
            readonly string[] _names;   // 原文中各佔位符的文字，例如 "{0}"
            readonly string _target;
            public readonly string Literal; // 原文去掉佔位符後的字面部分
            public int LiteralLength => Literal.Length;
            /// <summary>譯文去掉佔位符後和原文字面相同：套用只會轉換佔位符的內容。</summary>
            public bool KeepsLiteral
            {
                get
                {
                    var r = _target;
                    foreach (var n in _names) r = r.Replace(n, "");
                    return r == Literal;
                }
            }
            public bool Anchored => _prefix.Length > 0 && _suffix.Length > 0;

            Template(Regex re, string prefix, string suffix, string[] names, string target, string literal)
            {
                _re = re; _prefix = prefix; _suffix = suffix; _names = names; _target = target;
                Literal = literal;
            }

            public static bool TryCreate(string src, string dst, out Template t)
            {
                t = null;
                var parts = Markup.Split(src);
                var names = new List<string>();
                var pattern = new StringBuilder("^");
                var literal = new StringBuilder();
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i % 2 == 0 || parts[i][0] != '{')
                    {
                        pattern.Append(Regex.Escape(parts[i]));
                        literal.Append(parts[i]);
                        continue;
                    }
                    if (names.Contains(parts[i])) return false; // 同一佔位符出現兩次，略過
                    names.Add(parts[i]);
                    pattern.Append("(.*?)");
                }
                if (names.Count == 0) return false;
                pattern.Append('$');
                string prefix = parts[0], suffix = parts[parts.Length - 1];
                t = new Template(new Regex(pattern.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant),
                                 prefix, suffix, names.ToArray(), dst, literal.ToString());
                return true;
            }

            public string Apply(string s, Func<string, string> convert)
            {
                if (!s.StartsWith(_prefix, StringComparison.Ordinal) || !s.EndsWith(_suffix, StringComparison.Ordinal)) return null;
                var m = _re.Match(s);
                if (!m.Success) return null;
                // 佔位符是名字、數字等，不會跨行；跨行代表把別段拼接的文字也吃進來了
                for (int i = 1; i < m.Groups.Count; i++)
                    if (m.Groups[i].Value.IndexOf('\n') >= 0) return null;
                var r = _target;
                for (int i = 0; i < _names.Length; i++)
                    r = r.Replace(_names[i], convert(m.Groups[i + 1].Value));
                return r;
            }
        }
    }
}
