Probably Stolen 繁體中文化 mod（非官方） v{version}
==================================================

把遊戲內建的簡體中文，在顯示時轉成繁體中文（台灣用語）。非官方製作。
對應遊戲版本：DEMO Version 049-REV5-L

翻譯是用 AI 校正的，如果有翻不順的地方歡迎回報。
這個 mod 還沒有做詳細測試，如果有任何 bug 也歡迎回報
（https://www.nexusmods.com/probablystolen/mods/280?tab=bugs
 或 https://github.com/dbfoxtw/probably-stolen-demo-zh-hant/issues）。

【安裝】
找遊戲資料夾：Steam 遊戲庫 → 在遊戲上按右鍵 → 管理 → 瀏覽本機檔案（裡面有 Probably Stolen.exe）。

1. 安裝 MelonLoader 0.7.3（只要裝一次）：
   到 https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3 ，在 Assets 底下下載
   MelonLoader.x64.zip（不是 x86，也不是 Installer），把它的內容解壓縮到遊戲資料夾
   （和 Probably Stolen.exe 同一層）。解壓後，遊戲資料夾裡會多出 version.dll 與 MelonLoader 資料夾。
2. 啟動一次遊戲。第一次會先出現 MelonLoader 的黑色視窗，花幾分鐘到十分鐘產生必要的檔案（需要連網）。
   等主選單出現後，關閉遊戲。
3. 把這個壓縮檔裡的 Mods 與 UserData 資料夾，解壓縮到遊戲資料夾，資料夾合併即可。
4. 開遊戲，在設定的語言選單選「简体中文」。裝了 mod 後，選單上會顯示「繁體中文」。
更新 mod 時，只要重做第 3 步（解壓覆蓋）。

【移除】
刪除遊戲資料夾裡的 Mods\ProbablyStolenZhHant.dll 與 UserData\ZhHant 資料夾。
MelonLoader 本身要另外移除：刪除 version.dll、MelonLoader、Mods、Plugins、UserData、UserLibs。

【說明】
- 只在顯示時轉換：不修改遊戲檔、字串表與存檔。拔掉 mod 就回到原版簡中，存檔可以直接沿用。
- 簡中缺翻的條目（原版會顯示 Translation Error）會補上譯文。
- 簽名、紙條等手寫文字，改用毛筆的佑字 肅（補上繁體缺的字），風格接近原版。
- 已知衝突：其他翻譯 mod，以及同樣修改 TextMeshPro 文字或字型的 mod。
- 其他 mod 顯示的簡體文字也會自動轉成繁體，但沒有逐條潤稿；不是用 TextMeshPro 顯示的文字不會轉換。
- 遊戲更新後如果有新的文字，仍會自動轉成繁體；只是潤稿修正過的句子，原文變了就會改回自動轉換。

【授權與致謝】
- 本 mod：MIT 授權（LICENSE.txt）。
  原始碼：https://github.com/dbfoxtw/probably-stolen-demo-zh-hant
- 簡繁轉換字典：OpenCC（https://github.com/BYVoid/OpenCC），Apache License 2.0（opencc\LICENSE）
- 手寫字型：佑字 肅（Yuji Syuku，片岡佑之，https://github.com/Kinutafontfactory/Yuji）的補字版
  Yuji Syuku ZhHant，SIL Open Font License 1.1（handwritten-font-LICENSE.txt）
- MelonLoader（https://github.com/LavaGang/MelonLoader），Apache License 2.0
- 遊戲的文字、名稱與素材，權利屬於 Questing Goose Studio。


Probably Stolen Traditional Chinese mod (unofficial) v{version}
--------------------------------------------------------------

Shows the game's Simplified Chinese text in Traditional Chinese (Taiwan). Unofficial.
Supported game version: DEMO Version 049-REV5-L

The translation was proofread with AI. If any line reads awkwardly, please report it.
This mod has not been thoroughly tested yet. Bug reports are welcome too
(https://www.nexusmods.com/probablystolen/mods/280?tab=bugs
 or https://github.com/dbfoxtw/probably-stolen-demo-zh-hant/issues).

Install
Find the game folder: Steam library → right-click the game → Manage → Browse local files
(it contains Probably Stolen.exe).

1. Install MelonLoader 0.7.3 (once): from https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3
   download MelonLoader.x64.zip (not x86, not the Installer) and extract it into the game folder,
   next to Probably Stolen.exe. You should now see version.dll and a MelonLoader folder there.
2. Start the game once. MelonLoader opens a console window and spends a few minutes (up to ten)
   generating files; it needs an internet connection. Close the game after the main menu appears.
3. Extract the Mods and UserData folders from this archive into the game folder,
   merging with the existing folders.
4. Start the game and choose the Simplified Chinese language in the settings.
   With the mod installed, the menu shows it as Traditional Chinese.
To update the mod, just repeat step 3.

Uninstall
Delete Mods\ProbablyStolenZhHant.dll and the UserData\ZhHant folder.

Notes
- Display-only: game files, string tables, and saves are never modified. Saves work with or without the mod.
- Known conflicts: other translation mods, and mods that change TextMeshPro text or fonts.
- Simplified Chinese text from other mods is also converted to Traditional Chinese automatically, without per-line proofreading.
  Text not displayed through TextMeshPro is left as is.

License
- This mod: MIT (LICENSE.txt). Source: https://github.com/dbfoxtw/probably-stolen-demo-zh-hant
- OpenCC dictionaries: Apache License 2.0 (opencc\LICENSE)
- Handwritten font: Yuji Syuku ZhHant, a modified Yuji Syuku (Kataoka Yuji, https://github.com/Kinutafontfactory/Yuji)
  with added Traditional Chinese glyphs. SIL Open Font License 1.1 (handwritten-font-LICENSE.txt)
- The game's text, names, and assets belong to Questing Goose Studio.
