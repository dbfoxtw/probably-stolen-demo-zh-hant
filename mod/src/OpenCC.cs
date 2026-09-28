using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ProbablyStolenZhHant
{
    /// <summary>
    /// OpenCC 1.4.2 s2tw（簡體 → 臺灣正體字形）的 C# 移植，字典是 OpenCC 附的文字檔（Apache-2.0，由 tools/export_mod_data.py 匯出）。
    /// 流程同 s2tw.json：
    /// 1. 正規化：CJK 相容表意字 → 統一表意字。
    /// 2. 分詞：STPhrases ∪ STPhrases_GeneratedFromRegionalPhrases 最長匹配，連續查不到的字合成一段。
    /// 3. 每段依序套兩層轉換，每層都在段內由左到右最長匹配：詞組（同上兩個字典的聯集）查得到就用，
    ///    否則查 STCharacters；接著 TWVariantsPhrases 查得到就用，否則查 TWVariants。一詞多譯時取第一個。
    /// IDS（⿰氵青 這類組字描述序列）整組保留，不拆開轉換。
    /// </summary>
    public sealed class OpenCC
    {
        public static readonly string[] DictFiles =
        {
            "CJK_Compatibility_Ideographs.txt", "STPhrases.txt", "STPhrases_GeneratedFromRegionalPhrases.txt",
            "STCharacters.txt", "TWVariantsPhrases.txt", "TWVariants.txt",
        };

        readonly Dict _norm = new Dict(), _phrases = new Dict(), _chars = new Dict(), _twPhrases = new Dict(), _twChars = new Dict();

        public int EntryCount => _norm.Count + _phrases.Count + _chars.Count + _twPhrases.Count + _twChars.Count;

        public static OpenCC Load(string dir)
        {
            var cc = new OpenCC();
            string P(string name) => Path.Combine(dir, name);
            cc._norm.Load(P("CJK_Compatibility_Ideographs.txt"));
            cc._phrases.Load(P("STPhrases.txt")); // 聯集：同一個詞兩邊都有時，先載入的優先
            cc._phrases.Load(P("STPhrases_GeneratedFromRegionalPhrases.txt"));
            cc._chars.Load(P("STCharacters.txt"));
            cc._twPhrases.Load(P("TWVariantsPhrases.txt"));
            cc._twChars.Load(P("TWVariants.txt"));
            return cc;
        }

        public string Convert(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = Normalize(s);
            var sb = new StringBuilder(s.Length);
            var stage1 = new StringBuilder();
            void Segment(int start, int end)
            {
                if (end <= start) return;
                stage1.Clear();
                ConvertSegment(s, start, end, _phrases, _chars, stage1);
                var seg = stage1.ToString();
                ConvertSegment(seg, 0, seg.Length, _twPhrases, _twChars, sb);
            }
            // 分詞：查到的詞各自成段；連續查不到的字（含 IDS）合成一段，所以第二層的 TWVariantsPhrases 可以跨這些字比對
            int i = 0, run = 0;
            while (i < s.Length)
            {
                int len = IdsLength(s, i, s.Length);
                if (len == 0 && (len = _phrases.Match(s, i, s.Length, out _)) > 0)
                {
                    Segment(run, i);
                    Segment(i, i + len);
                    i += len;
                    run = i;
                    continue;
                }
                i += len > 0 ? len : CharLength(s, i);
            }
            Segment(run, s.Length);
            return sb.ToString();
        }

        string Normalize(string s)
        {
            if (_norm.Count == 0 || !_norm.AnyFirstChar(s)) return s;
            var sb = new StringBuilder(s.Length);
            ConvertSegment(s, 0, s.Length, _norm, null, sb);
            return sb.ToString();
        }

        /// <summary>段內由左到右：first 查得到就用（最長匹配），否則查 second；都查不到就原樣保留一個字（或一組 IDS）。</summary>
        static void ConvertSegment(string s, int start, int end, Dict first, Dict second, StringBuilder sb)
        {
            int i = start;
            while (i < end)
            {
                int len = IdsLength(s, i, end);
                if (len > 0) { sb.Append(s, i, len); i += len; continue; }
                len = first.Match(s, i, end, out var value);
                if (len == 0 && second != null) len = second.Match(s, i, end, out value);
                if (len > 0) { sb.Append(value); i += len; continue; }
                len = CharLength(s, i);
                sb.Append(s, i, len);
                i += len;
            }
        }

        static int CharLength(string s, int i) =>
            char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]) ? 2 : 1;

        static int CodePoint(string s, int i) => CharLength(s, i) == 2 ? char.ConvertToUtf32(s[i], s[i + 1]) : s[i];

        static int Arity(int cp)
        {
            switch (cp)
            {
                case 0x2FF2: case 0x2FF3: return 3;
                case 0x2FFE: case 0x2FFF: return 1;
                default: return cp >= 0x2FF0 && cp <= 0x2FFD ? 2 : 0;
            }
        }

        /// <summary>從 i 開始的完整 IDS 長度；不是 IDS 或不完整時傳回 0（同 OpenCC 的 UTF8Util）。</summary>
        static int IdsLength(string s, int i, int end)
        {
            if (Arity(CodePoint(s, i)) == 0) return 0;
            int codePoints = 0;
            return ConsumeIds(s, i, end, 16, ref codePoints, out int consumed) == IdsStatus.Complete ? consumed : 0;
        }

        enum IdsStatus { Complete, Incomplete, Invalid }

        static IdsStatus ConsumeIds(string s, int i, int end, int depthLeft, ref int codePoints, out int consumed)
        {
            consumed = 0;
            if (i >= end) return IdsStatus.Incomplete;
            if (depthLeft == 0 || codePoints >= 64) return IdsStatus.Invalid;
            int charLen = CharLength(s, i);
            if (i + charLen > end) return IdsStatus.Incomplete;
            codePoints++;
            int arity = Arity(CodePoint(s, i));
            if (arity == 0) { consumed = charLen; return IdsStatus.Complete; }
            int offset = charLen;
            for (int k = 0; k < arity; k++)
            {
                if (i + offset >= end) return IdsStatus.Incomplete;
                var status = ConsumeIds(s, i + offset, end, depthLeft - 1, ref codePoints, out int operand);
                if (status != IdsStatus.Complete) return status;
                offset += operand;
            }
            consumed = offset;
            return IdsStatus.Complete;
        }

        /// <summary>字典：key → 第一個候選；另記每個開頭字有哪些 key 長度（UTF-16 單位），最長匹配時只試這些長度。</summary>
        sealed class Dict
        {
            readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);
            readonly Dictionary<char, ulong> _lengths = new Dictionary<char, ulong>();

            public int Count => _map.Count;

            public void Load(string path)
            {
                foreach (var line in File.ReadLines(path, Encoding.UTF8))
                {
                    int tab = line.IndexOf('\t');
                    if (tab <= 0) continue;
                    int space = line.IndexOf(' ', tab + 1);
                    Add(line.Substring(0, tab), space < 0 ? line.Substring(tab + 1) : line.Substring(tab + 1, space - tab - 1));
                }
            }

            void Add(string key, string value)
            {
                if (key.Length > 63) throw new InvalidDataException($"OpenCC 字典的詞太長：{key}");
                if (!_map.TryAdd(key, value)) return;
                _lengths.TryGetValue(key[0], out var mask);
                _lengths[key[0]] = mask | (1UL << key.Length);
            }

            public bool AnyFirstChar(string s)
            {
                foreach (var ch in s)
                    if (_lengths.ContainsKey(ch)) return true;
                return false;
            }

            /// <summary>s[start..end) 開頭的最長 key，傳回長度（0 表示沒有）。</summary>
            public int Match(string s, int start, int end, out string value)
            {
                value = null;
                if (!_lengths.TryGetValue(s[start], out var mask)) return 0;
                for (int len = Math.Min(63, end - start); len >= 1; len--)
                    if ((mask & (1UL << len)) != 0 && _map.TryGetValue(s.Substring(start, len), out value))
                        return len;
                return 0;
            }
        }
    }
}
