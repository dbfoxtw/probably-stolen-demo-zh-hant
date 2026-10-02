using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using Il2CppTMPro;
using Il2Cpp;
using UnityEngine;
using Locale = UnityEngine.Localization.Locale;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

[assembly: MelonInfo(typeof(ProbablyStolenZhHant.ZhHantMod), "Probably Stolen 繁體中文", "1.1.3", "dbfoxtw")]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]
[assembly: HarmonyDontPatchAll] // 轉換資料載入後才手動掛上攔截

namespace ProbablyStolenZhHant
{
    /// <summary>
    /// 攔截 TextMeshPro 設定文字的地方，顯示前把簡體轉成繁體。字串表與存檔完全不動，所以拔掉 mod 就回到原版簡中。
    /// 對照表在遊戲裡從簡中字串表逐條翻譯建立（Translator），mod 附的資料不含遊戲原文。
    /// </summary>
    public class ZhHantMod : MelonMod
    {
        internal static TextConverter Conv;
        internal static Translator Tr;
        static MelonLogger.Instance Log;
        string _dataDir;
        float _nextReport = 60f;
        bool _fontFallbackDone;
        /// <summary>UserData\ZhHant\debug 存在時開啟：記錄後備轉換（misses.tsv）、每分鐘統計、缺翻自我測試。發布版沒有這個檔。</summary>
        static bool _debug;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            _dataDir = Path.Combine(MelonEnvironment.UserDataDirectory, "ZhHant");
            _debug = File.Exists(Path.Combine(_dataDir, "debug"));
            var sw = Stopwatch.StartNew();
            Tr = Translator.Load(_dataDir);
            // 字串表載入前先用暫用轉換器（只有 OpenCC＋術語表），並記下轉過的字，載入後重新轉換
            Conv = TextConverter.Build(Tr, Array.Empty<(string, string, string)>(), Path.Combine(_dataDir, "hardcoded.tsv"));
            Conv.Produced = new Dictionary<string, string>(StringComparer.Ordinal);
            Log.Msg($"載入翻譯資料：OpenCC {Tr.OpenCCEntries} 詞、術語 {Tr.TermCount}、逐條修正 {Tr.OverrideCount}、" +
                    $"缺翻補譯文 {Tr.Fills.Count} 條＋參照字串表 {Tr.FillRefCount} 條、補佔位符 {Tr.ArgCount} 條（{sw.ElapsedMilliseconds} ms）{(_debug ? "；除錯模式" : "")}");

            var prefix = new HarmonyMethod(typeof(ZhHantMod).GetMethod(nameof(FirstArgPrefix), BindingFlags.Static | BindingFlags.NonPublic));
            int patched = 0;
            HarmonyInstance.Patch(AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text)), prefix);
            patched++;
            foreach (var m in typeof(TMP_Text).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var ps = m.GetParameters();
                if (m.Name != "SetText" || ps.Length == 0 || ps[0].ParameterType != typeof(string)) continue;
                HarmonyInstance.Patch(m, prefix);
                patched++;
            }
            Log.Msg($"已攔截 TMP_Text 的 {patched} 個文字設定方法");

            // Text Animator 會記住自己設給 TMP 的文字，每幀比對；只在 TMP 層轉換會被當成「外部改了文字」，
            // 打字機效果就直接整段顯示。所以在它解析文字的入口先轉好，讓它記住的就是繁體。
            var tanim = FindType("Il2CppFebucci.UI.Core.TAnimCore", "Febucci");
            var convertText = tanim == null ? null : AccessTools.Method(tanim, "ConvertText");
            if (convertText != null)
            {
                HarmonyInstance.Patch(convertText, prefix);
                Log.Msg("已攔截 Text Animator 的 TAnimCore.ConvertText");
            }
            else Log.Warning("找不到 Text Animator 的 TAnimCore.ConvertText，打字機效果可能失效");

            // 簡中缺翻的條目：產生字串時補上譯文（沒有譯文就改用英文），不顯示「Translation Error」；
            // 原文漏了佔位符的條目：趁參數還在時補上並代入
            MissingTranslations.Init(Tr, Log);
            var generate = AccessTools.Method(typeof(UnityEngine.Localization.Settings.LocalizedStringDatabase), "GenerateLocalizedString");
            if (generate != null)
            {
                HarmonyInstance.Patch(generate, postfix: new HarmonyMethod(typeof(MissingTranslations).GetMethod(
                    nameof(MissingTranslations.GeneratePostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                Log.Msg($"已攔截 LocalizedStringDatabase.GenerateLocalizedString（缺翻補譯文在字串表載入後共 {Tr.Fills.Count + Tr.FillRefCount} 條）");
            }
            else Log.Warning("找不到 LocalizedStringDatabase.GenerateLocalizedString，缺翻會顯示遊戲的錯誤訊息");
        }

        /// <summary>
        /// 只在名稱含 assemblyHint 的組件裡找型別。AccessTools.TypeByName 會掃描所有組件，
        /// 掃到 MelonLoader 產生壞掉的型別（UnityEngine.CoreModule 的 ControlOptions 等）會印出警告。
        /// </summary>
        static Type FindType(string fullName, string assemblyHint)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                if (asm.GetName().Name.Contains(assemblyHint) && asm.GetType(fullName, false) is Type t)
                    return t;
            return null;
        }

        /// <summary>第一個參數就是要顯示的文字。</summary>
        static void FirstArgPrefix(ref string __0)
        {
            try { __0 = Conv.Convert(__0); }
            catch (Exception e) { Log.Error($"轉換失敗：{e.Message}"); }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            EnsureFontFallback();
            ApplyHandwrittenFont();
            _sceneReady = true;
            UpdateLocale();
            // 場景與預製物件裡序列化好的文字不會經過 setter（例如主選單寫死的按鈕文字）
            int changed = 0;
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                var s = t.text;
                if (string.IsNullOrEmpty(s)) continue;
                var r = Conv.Convert(s);
                if (r == s) continue;
                t.text = r;
                changed++;
            }
            Log.Msg($"場景 {sceneName}：轉換 {changed} 個既有文字物件{(_tables == TableState.Done ? "" : "（字串表還沒載入，用暫用轉換器）")}");
        }

        enum TableState { Waiting, Loading, Done, Failed }
        TableState _tables;
        readonly List<(string name, AsyncOperationHandle<StringTable> op)> _tableOps = new List<(string, AsyncOperationHandle<StringTable>)>();
        float _tableLoadStart;
        string _zhCode;

        /// <summary>
        /// Localization 初始化後，逐張非同步載入簡中字串表（表名見 tables.txt；另外納入已經載入、但不在清單上的簡中表），
        /// 全部載入後逐條翻譯、建立正式的轉換器。
        /// </summary>
        void UpdateTables()
        {
            try
            {
                if (_tables == TableState.Waiting)
                {
                    if (!LocalizationSettings.InitializationOperation.IsDone) return;
                    Locale zh = null;
                    var locales = LocalizationSettings.AvailableLocales.Locales;
                    for (int i = 0; i < locales.Count; i++)
                        if (locales[i] != null && locales[i].Identifier.Code.StartsWith("zh", StringComparison.Ordinal)) { zh = locales[i]; break; }
                    if (zh == null) { FailTables("找不到簡中語言"); return; }
                    _zhCode = zh.Identifier.Code;
                    var db = LocalizationSettings.StringDatabase;
                    foreach (var line in File.ReadAllLines(Path.Combine(_dataDir, "tables.txt"), Encoding.UTF8))
                        if (line.Trim().Length > 0) _tableOps.Add((line.Trim(), db.GetTableAsync(line.Trim(), zh)));
                    _tables = TableState.Loading;
                    _tableLoadStart = Time.unscaledTime;
                    return;
                }
                if (_tables != TableState.Loading) return;
                bool timedOut = Time.unscaledTime - _tableLoadStart > 30f;
                foreach (var (_, op) in _tableOps)
                    if (!op.IsDone && !timedOut) return;
                BuildConverter();
            }
            catch (Exception e) { FailTables(e.ToString()); }
        }

        void FailTables(string why)
        {
            _tables = TableState.Failed;
            Log.Error($"讀取簡中字串表失敗，只用 OpenCC＋術語表轉換（逐條修正不會套用）：{why}");
        }

        void BuildConverter()
        {
            var sw = Stopwatch.StartNew();
            var tables = new SortedDictionary<string, StringTable>(StringComparer.Ordinal);
            foreach (var (name, op) in _tableOps)
            {
                if (op.IsDone && op.Status == AsyncOperationStatus.Succeeded && op.Result != null) tables[op.Result.TableCollectionName] = op.Result;
                else Log.Warning($"讀不到簡中字串表 {name}（{(op.IsDone ? op.Status.ToString() : "逾時")}）");
            }
            foreach (var t in Resources.FindObjectsOfTypeAll<StringTable>())
                if (t != null && t.LocaleIdentifier.Code == _zhCode && !tables.ContainsKey(t.TableCollectionName))
                {
                    tables[t.TableCollectionName] = t;
                    Log.Msg($"納入不在 tables.txt 的簡中字串表 {t.TableCollectionName}");
                }
            var entries = new List<(string, string, string)>();
            foreach (var kv in tables)
            {
                var shared = kv.Value.SharedData.Entries; // 依 key 的順序（同 text/zh-Hans）
                for (int i = 0; i < shared.Count; i++)
                {
                    var e = kv.Value.GetEntry(shared[i].Id);
                    if (e != null) entries.Add((kv.Key, shared[i].Key, e.Value ?? ""));
                }
            }
            var conv = TextConverter.Build(Tr, entries, Path.Combine(_dataDir, "hardcoded.tsv"));
            conv.ChineseActive = Conv.ChineseActive;
            if (_debug)
            {
                conv.LoadMisses(Path.Combine(_dataDir, "misses.tsv")); // 跨次累積
                var hand = new StringBuilder();
                foreach (var (table, key, src) in entries)
                    if (src.Length > 0 && ((table == "Mechanic" && key.StartsWith("note_", StringComparison.Ordinal))
                                           || (table == "UI" && Array.IndexOf(HandwrittenUiKeys, key) >= 0)))
                        hand.Append(conv.Convert(src));
                _handText = hand.ToString();
            }
            var early = Conv;
            Conv = conv;
            _tables = TableState.Done;
            Log.Msg($"簡中字串表 {tables.Count} 張、{conv.EntryCount} 條 → 整句 {conv.ExactCount}、樣板 {conv.TemplateCount}、短詞 {conv.PhraseCount}、" +
                    $"簡體專用字 {conv.TriggerCount}、同句異譯 {conv.Conflicts}；逐條修正套用 {Tr.OverridesApplied} 條、缺翻補譯文 {Tr.Fills.Count} 條、補佔位符 {Tr.WithArgs.Count}/{Tr.ArgCount} 條（{sw.ElapsedMilliseconds} ms，" +
                    $"遊戲啟動後 {Time.realtimeSinceStartup:F1} 秒）");
            if (Tr.StaleOverrides.Count > 0)
                Log.Warning($"原文已變動、暫停套用的逐條修正 {Tr.StaleOverrides.Count} 條：{string.Join("、", Tr.StaleOverrides.GetRange(0, Math.Min(10, Tr.StaleOverrides.Count)))}");

            // 暫用轉換器轉過、還在畫面上的字：設回原文，讓攔截用正式的轉換器重轉（逐條修正、短詞等可能不同）
            int refreshed = 0;
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                var s = t.text;
                if (string.IsNullOrEmpty(s) || !early.Produced.TryGetValue(s, out var orig) || conv.Convert(orig) == s) continue;
                t.text = orig;
                refreshed++;
            }
            Log.Msg($"暫用轉換器轉過 {early.Produced.Count} 句（{early.Stats()}），畫面上重新轉換 {refreshed} 個文字物件");
        }

        /// <summary>手寫字型（馬善政體）只有簡體字，缺字時改用黑體（NotoSansSC）補上。</summary>
        void EnsureFontFallback()
        {
            if (_fontFallbackDone) return;
            TMP_FontAsset noto = null;
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f.name.StartsWith("NotoSansSC", StringComparison.Ordinal)) { noto = f; break; }
            var list = TMP_Settings.fallbackFontAssets;
            if (noto == null || list == null) return;
            if (!list.Contains(noto)) list.Add(noto);
            _fontFallbackDone = true;
            Log.Msg($"TMP 全域 fallback 已加入 {noto.name}（共 {list.Count} 個）");
        }

        TMP_FontAsset _handFont;
        string _origHandName;
        bool _handFontFailed, _handTested;
        string _handText; // 除錯模式：簽名與紙條的譯文，給手寫字型自我測試用

        /// <summary>遊戲用手寫字型的字串表條目（另外是 Mechanic/note_* 的紙條）；和 tools/check.py 的 HANDWRITTEN_UI_KEYS 相同。</summary>
        static readonly string[] HandwrittenUiKeys =
            { "intel_mentor_signature", "intel_mentor_note_appraisal_signature", "ui_exchange_signature1", "ui_exchange_signature2" };

        /// <summary>
        /// 除錯模式的自我測試：請 TMP 用手寫字型產生簽名與紙條用到的每個字，產生失敗的字寫進 log。
        /// 紙條是隨機出現的，不必等到抽中就能確認每個字（包括補字版拼出來的字）在遊戲裡都生得出來。
        /// </summary>
        void HandwrittenSelfTest()
        {
            try
            {
                var seen = new HashSet<char>();
                var sb = new StringBuilder();
                foreach (var c in Translator.Markup.Replace(_handText ?? "", ""))
                    if (!char.IsWhiteSpace(c) && seen.Add(c)) sb.Append(c);
                var chars = sb.ToString();
                if (chars.Length == 0) { Log.Warning("手寫字型自我測試：字串表裡找不到簽名與紙條"); return; }
                if (_handFont.TryAddCharacters(chars, out string missing) || string.IsNullOrEmpty(missing))
                    Log.Msg($"手寫字型自我測試：簽名與紙條用到的 {chars.Length} 字都產生成功");
                else
                    Log.Warning($"手寫字型自我測試：{chars.Length} 字中有 {missing.Length} 字產生失敗（遊戲會改用黑體）：{missing}");
            }
            catch (Exception e) { Log.Error($"手寫字型自我測試失敗：{e.Message}"); }
        }

        /// <summary>
        /// 手寫字型換成支援繁體的字型（UserData\ZhHant\handwritten.ttf/.otf）：執行時讀字型檔建立動態 TMP 字型，
        /// 沿用原手寫字型的取樣大小、padding、圖集大小與材質參數，再換掉 FontLoader 的設定與已套用的文字物件。
        /// </summary>
        void ApplyHandwrittenFont()
        {
            if (_handFontFailed) return;
            var loaders = Resources.FindObjectsOfTypeAll<FontLoader>();
            if (_handFont == null)
            {
                TMP_FontAsset orig = null;
                foreach (var l in loaders)
                {
                    var f = l.simplifiedChineseHandwrittenFont;
                    if (f != null && f.name.StartsWith("MaShanZheng", StringComparison.Ordinal)) { orig = f; break; }
                }
                if (orig == null) return;
                string path = null;
                foreach (var ext in new[] { ".ttf", ".otf" })
                    if (File.Exists(Path.Combine(_dataDir, "handwritten" + ext))) path = Path.Combine(_dataDir, "handwritten" + ext);
                if (path == null) { _handFontFailed = true; Log.Msg("沒有 handwritten.ttf/.otf，手寫字型維持原樣（缺字用黑體補）"); return; }
                try { _handFont = CreateFontLike(path, orig); }
                catch (Exception e) { _handFontFailed = true; Log.Error($"建立手寫字型失敗：{e}"); return; }
                _origHandName = orig.name;
                Log.Msg($"手寫字型：{Path.GetFileName(path)} 取代 {orig.name}（取樣 {orig.faceInfo.pointSize}、padding {orig.atlasPadding}、圖集 {orig.atlasWidth}×{orig.atlasHeight}）");
            }
            int loaded = 0, texts = 0;
            foreach (var l in loaders)
            {
                var f = l.simplifiedChineseHandwrittenFont;
                if (f != null && f.name == _origHandName) { l.simplifiedChineseHandwrittenFont = _handFont; loaded++; }
            }
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
                if (t.font != null && t.font.name == _origHandName) { t.font = _handFont; texts++; }
            if (loaded + texts > 0) Log.Msg($"手寫字型：更新 {loaded} 個 FontLoader、{texts} 個文字物件");
        }

        static TMP_FontAsset CreateFontLike(string path, TMP_FontAsset orig)
        {
            var font = new Font(path); // 傳入完整路徑時，Unity 會直接讀字型檔
            font.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var fa = TMP_FontAsset.CreateFontAsset(font, orig.faceInfo.pointSize, orig.atlasPadding, orig.atlasRenderMode,
                                                   orig.atlasWidth, orig.atlasHeight, AtlasPopulationMode.Dynamic, true);
            if (fa == null) throw new Exception("CreateFontAsset 傳回 null");
            fa.name = "ZhHant Handwritten SDF";
            fa.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var mat = fa.material;
            var atlas = mat.GetTexture("_MainTex");
            mat.CopyPropertiesFromMaterial(orig.material); // 沿用原手寫字型的外觀參數（粗細、柔化等）
            mat.SetTexture("_MainTex", atlas);
            mat.SetFloat("_TextureWidth", fa.atlasWidth);
            mat.SetFloat("_TextureHeight", fa.atlasHeight);
            mat.SetFloat("_GradientScale", fa.atlasPadding + 1);
            mat.hideFlags = HideFlags.DontUnloadUnusedAsset;
            if (atlas != null) atlas.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return fa;
        }

        float _nextLocaleCheck;
        bool _sceneReady, _localeError, _selfTested;

        /// <summary>每秒確認一次遊戲語言；寫死英文的翻譯只在中文模式套用。</summary>
        void UpdateLocale()
        {
            try
            {
                var loc = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
                var code = loc == null ? null : loc.Identifier.Code;
                bool zh = code != null && code.StartsWith("zh", StringComparison.Ordinal);
                if (zh != Conv.ChineseActive) Log.Msg($"目前語言：{code}（{(zh ? "翻譯" : "不翻譯")}寫死的英文）");
                Conv.ChineseActive = zh;
                if (_debug && zh && !_selfTested && _tables >= TableState.Done) // 參照型的缺翻補譯要等字串表載入
                {
                    _selfTested = true;
                    MissingTranslations.SelfTest(loc);
                }
                if (_debug && zh && !_handTested && _tables == TableState.Done && _handFont != null) // 手寫字型在場景初始化時才建立
                {
                    _handTested = true;
                    HandwrittenSelfTest();
                }
            }
            catch (Exception e)
            {
                if (!_localeError) Log.Warning($"讀取目前語言失敗：{e.Message}");
                _localeError = true;
            }
        }

        public override void OnUpdate()
        {
            if (_sceneReady && _tables < TableState.Done) UpdateTables();
            if (_sceneReady && Time.unscaledTime >= _nextLocaleCheck)
            {
                _nextLocaleCheck = Time.unscaledTime + 1f;
                UpdateLocale();
            }
            if (!_debug || Time.unscaledTime < _nextReport) return;
            _nextReport = Time.unscaledTime + 60f;
            Report();
        }

        public override void OnApplicationQuit() => Report();

        void Report()
        {
            Log.Msg(Conv.Stats() + $"；缺翻補譯文 {MissingTranslations.FillHits}、改用英文 {MissingTranslations.EnglishHits}、補佔位符 {MissingTranslations.ArgHits}");
            if (!_debug || _tables != TableState.Done) return; // 暫用轉換器的後備轉換不代表正式版會漏掉
            try { Conv.WriteMisses(Path.Combine(_dataDir, "misses.tsv")); }
            catch (Exception e) { Log.Warning($"寫入 misses.tsv 失敗：{e.Message}"); }
        }
    }
}
