# Probably Stolen 繁體中文化 mod（非官方）

*Probably Stolen*（Demo）的繁體中文（台灣用語）顯示 mod，使用 MelonLoader。非官方製作。

對應遊戲版本：DEMO Version 049-REV5-L

下載：[Nexus Mods](https://www.nexusmods.com/probablystolen/mods/280)，或本 repo 的 [Releases](https://github.com/dbfoxtw/probably-stolen-demo-zh-hant/releases)。版本紀錄見 [CHANGELOG.md](CHANGELOG.md)。

翻譯是用 AI 校正的，如果有翻不順的地方歡迎回報。這個 mod 還沒有做詳細測試，如果有任何 bug 也歡迎回報（[Issues](https://github.com/dbfoxtw/probably-stolen-demo-zh-hant/issues)，或 [Nexus 頁面](https://www.nexusmods.com/probablystolen/mods/280?tab=bugs)的 Bugs 分頁）。

> Unofficial Traditional Chinese (Taiwan) display mod for *Probably Stolen* (Demo), built on MelonLoader. See [English](#english) below.

## 這是什麼

- 遊戲目前只有簡體中文。這個 mod 在文字要顯示時，把簡體中文轉成繁體中文（台灣用語）。
- 在遊戲設定選「简体中文」即可；裝了 mod 後，選單上會顯示「繁體中文」。
- 簽名、紙條等手寫文字，改用毛筆的佑字 肅（補上繁體缺的字），風格接近原版。

## 安裝

1. 安裝 [MelonLoader](https://github.com/LavaGang/MelonLoader/releases) 0.7.3，並啟動一次遊戲。遊戲是 Unity IL2CPP 版本，MelonLoader 要先產生遊戲的組件，第一次啟動可能需時十分鐘。
2. 從 [Releases](https://github.com/dbfoxtw/probably-stolen-demo-zh-hant/releases) 下載最新版的 `ProbablyStolen-ZhHant-<版本>.zip`（Assets 底下），或到 [Nexus Mods](https://www.nexusmods.com/probablystolen/mods/280) 下載。
3. 關閉遊戲，把壓縮檔裡的 `Mods` 與 `UserData` 資料夾解壓到遊戲資料夾（和 `Probably Stolen.exe` 同一層），資料夾合併即可。
   - 找遊戲資料夾：Steam 遊戲庫 → 在遊戲上按右鍵 → 管理 → 瀏覽本機檔案。
4. 開遊戲，在設定的語言選單選「简体中文」。裝了 mod 後，選單上會顯示「繁體中文」。

更新版本時，照同樣步驟解壓覆蓋即可。

## 移除

刪除遊戲資料夾裡的 `Mods\ProbablyStolenZhHant.dll` 與 `UserData\ZhHant` 資料夾，就回到原版簡中，存檔可以直接沿用。MelonLoader 本身要另外移除：刪除 `version.dll`、`MelonLoader`、`Mods`、`Plugins`、`UserData`、`UserLibs`。

## 運作方式

- 遊戲是 Unity IL2CPP 版本，mod 透過 MelonLoader（Il2CppInterop、Harmony）掛上攔截。
- **只在顯示時轉換**：攔截 TextMeshPro 設定文字的地方，文字顯示前才轉成繁體。遊戲檔、字串表、存檔都不修改，拔掉 mod 就回到原版簡中。
- **在遊戲裡即時產生對照**：mod 讀取遊戲自己的簡中字串表，逐條用 [OpenCC](https://github.com/BYVoid/OpenCC)（s2tw）、術語表與逐條修正轉成繁體。
- **簡中缺翻的條目**：原版會顯示「Translation Error」，mod 會補上譯文；沒有譯文時改顯示遊戲的英文。
- **簡中漏了佔位符的條目**：例如以物易物的「換取：」，原版後面不顯示物品。遊戲代入參數時，mod 改用補上佔位符的譯文。

## 這個 repo 不含遊戲內容

依遊戲社群的 mod 規範，這個 repo 與發布檔都不附遊戲的字串表或其他素材。`data/` 只有翻譯規則：

| 檔案 | 內容 |
|---|---|
| `terms.tsv` | 術語表：OpenCC 轉換後的詞 → 台灣用語 |
| `overrides.tsv` | 逐條修正：只存我們改動的片段與位置。自動轉換的結果和製作時不同（例如遊戲更新改了原文）時，這條修正會自動停用 |
| `fills.tsv` | 簡中缺翻的條目：我們的譯文，或參照字串表裡另一條的譯文。沒有這個檔時，缺翻的條目改顯示遊戲的英文 |
| `args.tsv` | 簡中漏了英文有的佔位符的少數條目（例如以物易物的「換取：」後面沒有物品）：補上佔位符的譯文，遊戲代入參數時改用 |
| `hardcoded.tsv` | 寫死在遊戲程式裡的少數英文（例如借據）的翻譯規則，只在中文模式套用 |
| `tables.txt` | 要讀取的字串表名稱 |

OpenCC 字典在建置時由 Python 套件產生（見 `tools/build_mod_data.py`）。

## 從原始碼建置（開發者）

一般玩家請看上面的「安裝」，不需要建置。

需要 Windows、Python 3.10 以上、.NET SDK 6 以上。

1. 安裝 [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.3，並啟動一次遊戲，讓它產生 `MelonLoader\Il2CppAssemblies`（編譯時要參考）。
2. 關閉遊戲，執行 `install.bat`：建置後安裝到遊戲的 `Mods\` 與 `UserData\ZhHant\`。移除用 `uninstall.bat`。
   - 遊戲不在 Steam 預設位置時，加上 `--game "遊戲資料夾"`。
   - 手寫字型：把支援繁體的 .ttf／.otf 放在 `fonts/handwritten.ttf`，或加上 `--font 字型檔`。
   - 除錯模式：加上 `--debug`，會記錄後備轉換（`UserData\ZhHant\misses.tsv`）、每分鐘統計，並做缺翻自我測試。
3. 只建置、不安裝：`python tools/install.py build`。
4. 打包發布用的壓縮檔：`python tools/package.py`。發布版附的手寫字型是補字版的佑字 肅（Yuji Syuku ZhHant）：
   - 從 [Google Fonts](https://github.com/google/fonts/tree/main/ofl/yujisyuku) 下載 `YujiSyuku-Regular.ttf` 與 `OFL.txt`，放到 `fonts/candidates/`，後者改名 `YujiSyuku-OFL.txt`。
   - 執行 `python tools/make_handwritten_font.py`，會產生 `fonts/handwritten.ttf` 與授權檔 `fonts/handwritten.LICENSE.txt`。
   - 這支腳本用佑字自己的部件拼出繁中缺的「你喔嗎汙」，並調整刪節號的間距。

## 已知衝突

- 其他翻譯 mod。
- 同樣修改 TextMeshPro 文字或字型的 mod。
- 其他 mod 顯示的簡體文字也會自動轉成繁體（OpenCC＋術語表），但沒有逐條潤稿；不是用 TextMeshPro 顯示的文字不會轉換。

## 授權

這個 repo 的程式碼與翻譯規則（`data/`）以 [MIT 授權](LICENSE)釋出。

遊戲的文字、名稱與素材，權利屬於 Questing Goose Studio，不在授權範圍內。第三方元件：

- OpenCC 字典：Apache License 2.0，建置時附上授權文字。
- 手寫字型（發布版）：佑字 肅（Yuji Syuku，片岡佑之，The Yuji Project Authors）的補字版，SIL Open Font License 1.1。修改版同樣是 OFL，附上授權文字。
- MelonLoader：Apache License 2.0；Harmony：MIT（執行時由 MelonLoader 提供，不隨本 mod 散布）。

## English

An unofficial mod that shows *Probably Stolen*'s Simplified Chinese text in Traditional Chinese (Taiwan). Supported game version: DEMO Version 049-REV5-L. Download from [Nexus Mods](https://www.nexusmods.com/probablystolen/mods/280) or this repository's [Releases](https://github.com/dbfoxtw/probably-stolen-demo-zh-hant/releases).

The translation was proofread with AI. If any line reads awkwardly, please report it. This mod has not been thoroughly tested yet, so bug reports are welcome too, via [Issues](https://github.com/dbfoxtw/probably-stolen-demo-zh-hant/issues) or the Bugs tab on the [Nexus page](https://www.nexusmods.com/probablystolen/mods/280?tab=bugs).

**Install**

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader/releases) 0.7.3 and start the game once. The first start may take up to ten minutes while MelonLoader generates the game's assemblies.
2. Download the latest `ProbablyStolen-ZhHant-<version>.zip` from [Releases](https://github.com/dbfoxtw/probably-stolen-demo-zh-hant/releases) (under Assets) or from [Nexus Mods](https://www.nexusmods.com/probablystolen/mods/280).
3. Close the game and extract the `Mods` and `UserData` folders into the game folder (next to `Probably Stolen.exe`), merging with the existing folders.
4. Start the game and choose the Simplified Chinese language in the settings. With the mod installed, it is shown as Traditional Chinese.

**Uninstall:** delete `Mods\ProbablyStolenZhHant.dll` and the `UserData\ZhHant` folder.

**Known conflicts:** other translation mods, and mods that change TextMeshPro text or fonts. Simplified Chinese text from other mods is also converted automatically, without per-line proofreading; text not displayed through TextMeshPro is left as is.

- **Display-only.** Text is converted right before TextMeshPro displays it. Game files, string tables, and saves are never modified, and removing the mod restores the original Simplified Chinese.
- **No game content is included.** The mod reads the game's own Simplified Chinese string tables at runtime and converts each entry with OpenCC (s2tw), a glossary, and per-entry corrections. The corrections are stored only as changed fragments and positions. Entries missing from the Simplified Chinese table get our translation, or fall back to the game's English text.
- The game is a Unity IL2CPP build; the mod hooks it through MelonLoader (Il2CppInterop, Harmony).
- Handwritten text (signatures and notes) uses Yuji Syuku, a brush font close to the original, with the few missing Traditional Chinese glyphs added (SIL OFL 1.1).
- Build from source (developers only): install MelonLoader 0.7.3, start the game once, then run `install.bat` (requires Python 3.10+ and the .NET SDK 6+).
- License: MIT for the code and translation rules in this repository. The game's text, names, and assets belong to Questing Goose Studio.
