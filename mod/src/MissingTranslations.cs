using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Il2CppObject = Il2CppSystem.Object;
using Il2CppList = Il2CppSystem.Collections.Generic.IList<Il2CppSystem.Object>;
using Il2CppCollection = Il2CppSystem.Collections.Generic.ICollection<Il2CppSystem.Object>;

namespace ProbablyStolenZhHant
{
    /// <summary>
    /// 簡中字串表裡空白的條目（缺翻）。遊戲沒設定備援語言，缺翻時畫面會出現「Translation Error '{key}' in {表}」。
    /// 攔截 LocalizedStringDatabase.GenerateLocalizedString（此時 key、表名、要代入的參數都還在）：
    /// 有我們的譯文（fills.tsv）就用譯文並代入參數，沒有就改用遊戲自己的英文。已經有翻譯的條目完全不動。
    /// </summary>
    internal static class MissingTranslations
    {
        static Dictionary<string, string> Fills = new Dictionary<string, string>(StringComparer.Ordinal); // "表/key" → 譯文
        static readonly HashSet<string> Logged = new HashSet<string>(StringComparer.Ordinal);
        static readonly Regex Placeholder = new Regex(@"\{(\d+)(?::[^{}]*)?\}", RegexOptions.Compiled);
        static MelonLogger.Instance _log;
        static bool _inFallback, _errorLogged;
        internal static bool SkipFills; // 自我測試用：強制走英文備援
        internal static int FillHits, EnglishHits;

        internal static void Init(Translator tr, MelonLogger.Instance log)
        {
            _log = log;
            Fills = tr.Fills;
        }

        internal static int Count => Fills.Count;

        /// <summary>Harmony postfix（參數名稱需與原方法相同）。</summary>
        internal static void GeneratePostfix(StringTable table, StringTableEntry entry, TableReference tableReference,
            TableEntryReference tableEntryReference, Locale locale, Il2CppList arguments, ref string __result)
        {
            if (_inFallback) return;
            try
            {
                if (entry != null && !string.IsNullOrEmpty(entry.Value)) return; // 有翻譯，不動
                var code = locale == null ? null : locale.Identifier.Code;
                if (code == null || !code.StartsWith("zh", StringComparison.Ordinal)) return;
                var tableName = table != null ? table.TableCollectionName : tableReference.TableCollectionName;
                var key = tableEntryReference.ResolveKeyName(table == null ? null : table.SharedData);
                var name = tableName + "/" + key;

                if (!SkipFills && key != null && Fills.TryGetValue(name, out var text))
                {
                    __result = Format(text, arguments);
                    FillHits++;
                    LogOnce(name, $"缺翻補上譯文：{name} → {__result}");
                    return;
                }

                var en = LocalizationSettings.ProjectLocale;
                if (en == null || en.Identifier.Code == code) return;
                _inFallback = true;
                try
                {
                    var s = LocalizationSettings.StringDatabase.GetLocalizedString(tableReference, tableEntryReference, arguments, en, FallbackBehavior.DontUseFallback);
                    if (!string.IsNullOrEmpty(s) && !s.StartsWith("Translation Error", StringComparison.Ordinal))
                    {
                        __result = s;
                        EnglishHits++;
                        LogOnce(name, $"缺翻改用英文：{name} → {s}");
                    }
                    else LogOnce(name, $"缺翻且英文也沒有：{name}（維持遊戲的錯誤訊息）");
                }
                finally { _inFallback = false; }
            }
            catch (Exception e)
            {
                if (!_errorLogged) _log?.Error($"處理缺翻失敗：{e}");
                _errorLogged = true;
            }
        }

        /// <summary>把 {0}、{1:N0} 這類佔位符換成參數（我們的譯文只用到簡單的位置參數）。</summary>
        static string Format(string text, Il2CppList args)
        {
            if (args == null || text.IndexOf('{') < 0) return text;
            int n = args.Cast<Il2CppCollection>().Count;
            return Placeholder.Replace(text, m =>
            {
                int i = int.Parse(m.Groups[1].Value);
                if (i >= n) return m.Value;
                var o = args[i];
                return o == null ? "" : o.ToString();
            });
        }

        static void LogOnce(string name, string msg)
        {
            if (Logged.Add(name)) _log?.Msg(msg);
        }

        /// <summary>啟動時故意要幾條缺翻的字串，把結果寫進 log，不用在遊戲裡找這些對話。</summary>
        internal static void SelfTest(Locale zh)
        {
            var db = LocalizationSettings.StringDatabase;
            var cases = new (string table, string key, string arg, bool skipFills, string note)[]
            {
                ("Dialogue", "dialog_biz_permit_s0_1", null, false, "缺翻，補譯文"),
                ("Dialogue", "dialog_biz_permit_choice_buy", "123", false, "缺翻，補譯文並代入參數 123"),
                ("Dialogue", "dialog_biz_permit_s0_1", null, true, "缺翻，假裝沒有譯文 → 應改用英文"),
                ("MainMenu", "delete_save", null, false, "缺翻，英文也是空的 → 維持錯誤訊息"),
                ("Dialogue", "dialog_cheng_intro_3", null, false, "有翻譯，不該被動到（應為簡體原文）"),
            };
            foreach (var c in cases)
            {
                try
                {
                    var list = new Il2CppSystem.Collections.Generic.List<Il2CppObject>();
                    if (c.arg != null) list.Add(new Il2CppObject(IL2CPP.ManagedStringToIl2Cpp(c.arg)));
                    Logged.Remove(c.table + "/" + c.key);
                    SkipFills = c.skipFills;
                    var s = db.GetLocalizedString(c.table, c.key, list.Cast<Il2CppList>(), zh, FallbackBehavior.UseProjectSettings);
                    _log.Msg($"自我測試［{c.note}］{c.table}/{c.key} → {s}");
                }
                catch (Exception e) { _log.Error($"自我測試失敗 {c.table}/{c.key}：{e.Message}"); }
                finally { SkipFills = false; }
            }
        }
    }
}
