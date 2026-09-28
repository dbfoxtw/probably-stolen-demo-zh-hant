# Probably Stolen 繁體中文化 mod（非官方）

*Probably Stolen*（Demo）的繁體中文（台灣用語）顯示 mod，使用 MelonLoader。
非官方製作，與開發商 Questing Goose Studio 無關。

> Unofficial Traditional Chinese (Taiwan) display mod for *Probably Stolen* (Demo), built on MelonLoader. See [English](#english) below.

## 這是什麼

- 遊戲目前只有簡體中文。這個 mod 在文字要顯示時，把簡體中文轉成繁體中文（台灣用語）。
- 在遊戲設定選「简体中文」即可；裝了 mod 後，選單上會顯示「繁體中文」。
- 可選：把遊戲的簡體手寫字型換成支援繁體的手寫字型。

## 運作方式

- **只在顯示時轉換**：攔截 TextMeshPro 設定文字的地方，文字顯示前才轉成繁體。遊戲檔、字串表、存檔都不修改，拔掉 mod 就回到原版簡中。
- **在遊戲裡即時產生對照**：mod 讀取遊戲自己的簡中字串表，逐條用 [OpenCC](https://github.com/BYVoid/OpenCC)（s2tw）、術語表與逐條修正轉成繁體。
- **簡中缺翻的條目**：原版會顯示「Translation Error」，mod 會補上譯文；沒有譯文時改顯示遊戲的英文。
- 不連網，不收集或傳送任何資料。

## 這個 repo 不含遊戲內容

依遊戲社群的 mod 規範，這個 repo 與發布檔都不附遊戲的字串表或其他素材。`data/` 只有翻譯規則：

| 檔案 | 內容 |
|---|---|
| `terms.tsv` | 術語表：OpenCC 轉換後的詞 → 台灣用語 |
| `overrides.tsv` | 逐條修正：只存我們改動的片段與位置。自動轉換的結果和製作時不同（例如遊戲更新改了原文）時，這條修正會自動停用 |
| `fills.tsv` | 簡中缺翻的條目：我們的譯文，或參照字串表裡另一條的譯文。沒有這個檔時，缺翻的條目改顯示遊戲的英文 |
| `hardcoded.tsv` | 寫死在遊戲程式裡的少數英文（例如借據）的翻譯規則，只在中文模式套用 |
| `tables.txt` | 要讀取的字串表名稱 |

OpenCC 字典在建置時由 Python 套件產生（見 `tools/build_mod_data.py`）。

## 從原始碼建置

需要 Windows、Python 3.10 以上、.NET SDK 6 以上。

1. 安裝 [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.3，並啟動一次遊戲，讓它產生 `MelonLoader\Il2CppAssemblies`（編譯時要參考）。
2. 關閉遊戲，執行 `install.bat`：建置後安裝到遊戲的 `Mods\` 與 `UserData\ZhHant\`。移除用 `uninstall.bat`。
   - 遊戲不在 Steam 預設位置時，加上 `--game "遊戲資料夾"`。
   - 手寫字型：把支援繁體的 .ttf／.otf 放在 `fonts/handwritten.ttf`，或加上 `--font 字型檔`。
   - 除錯模式：加上 `--debug`，會記錄後備轉換（`UserData\ZhHant\misses.tsv`）、每分鐘統計，並做缺翻自我測試。
3. 只建置、不安裝：`python tools/install.py build`。
4. 打包發布用的壓縮檔：`python tools/package.py`。發布版附的手寫字型是霞鶩文楷 TC，放在 `fonts/handwritten.ttf`，授權檔放在 `fonts/handwritten.LICENSE.txt`。

## 已知衝突

- 其他翻譯 mod。
- 同樣修改 TextMeshPro 文字或字型的 mod。

## 授權

這個 repo 的程式碼與翻譯規則（`data/`）以 [MIT 授權](LICENSE)釋出。

遊戲的文字、名稱與素材，權利屬於 Questing Goose Studio，不在授權範圍內。第三方元件：

- OpenCC 字典：Apache License 2.0，建置時附上授權文字。
- MelonLoader：Apache License 2.0；Harmony：MIT（執行時由 MelonLoader 提供，不隨本 mod 散布）。

## English

An unofficial mod that shows *Probably Stolen*'s Simplified Chinese text in Traditional Chinese (Taiwan).

- **Display-only.** Text is converted right before TextMeshPro displays it. Game files, string tables, and saves are never modified, and removing the mod restores the original Simplified Chinese.
- **No game content is included.** The mod reads the game's own Simplified Chinese string tables at runtime and converts each entry with OpenCC (s2tw), a glossary, and per-entry corrections. The corrections are stored only as changed fragments and positions. Entries missing from the Simplified Chinese table get our translation, or fall back to the game's English text.
- No network access. No data is collected or sent.
- Build from source: install MelonLoader 0.7.3, start the game once, then run `install.bat` (requires Python 3.10+ and the .NET SDK 6+).
- License: MIT for the code and translation rules in this repository. The game's text, names, and assets belong to Questing Goose Studio.
