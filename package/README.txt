Probably Stolen 繁體中文化 mod（非官方） v{version}
==================================================

把遊戲內建的簡體中文，在顯示時轉成繁體中文（台灣用語）。
非官方製作，與開發商 Questing Goose Studio 無關。

這個 mod 還沒有做詳細測試，如果有任何 bug 歡迎回報
（Nexus 頁面的 Bugs 分頁，或 https://github.com/dbfoxtw/probably-stolen-demo-zh-hant/issues）。

【安裝】
1. 安裝 MelonLoader 0.7.3（https://github.com/LavaGang/MelonLoader/releases），並啟動一次遊戲。
   第一次啟動會比較久，而且需要連網（遊戲是 Unity IL2CPP 版本，MelonLoader 要先產生遊戲的組件）。
2. 關閉遊戲，把壓縮檔裡的 Mods 與 UserData 資料夾，解壓到遊戲資料夾（和 Probably Stolen.exe 同一層），
   資料夾合併即可。
   找遊戲資料夾：Steam 遊戲庫 → 在遊戲上按右鍵 → 管理 → 瀏覽本機檔案。
3. 開遊戲，在設定的語言選單選「简体中文」。裝了 mod 後，選單上會顯示「繁體中文」。

【移除】
刪除遊戲資料夾裡的 Mods\ProbablyStolenZhHant.dll 與 UserData\ZhHant 資料夾。
MelonLoader 本身要另外移除：刪除 version.dll、MelonLoader、Mods、Plugins、UserData、UserLibs。

【說明】
- 只在顯示時轉換：不修改遊戲檔、字串表與存檔。拔掉 mod 就回到原版簡中，存檔可以直接沿用。
- 簡中缺翻的條目（原版會顯示 Translation Error）會補上譯文。
- 導師筆記、紙條等手寫文字，改用支援繁體的霞鶩文楷 TC。
  不想換字型的話，刪除 UserData\ZhHant\handwritten.ttf。
- 不連網，不收集或傳送任何資料。
- 已知衝突：其他翻譯 mod，以及同樣修改 TextMeshPro 文字或字型的 mod。
- 遊戲更新後如果有新的文字，仍會自動轉成繁體；只是人工修正過的句子，原文變了就會改回自動轉換。

【授權與致謝】
- 本 mod：MIT 授權（LICENSE.txt）。
  原始碼：https://github.com/dbfoxtw/probably-stolen-demo-zh-hant
- 簡繁轉換字典：OpenCC（https://github.com/BYVoid/OpenCC），Apache License 2.0（opencc\LICENSE）
- 手寫字型：霞鶩文楷 TC（LXGW WenKai TC，https://github.com/lxgw/LxgwWenkaiTC），
  SIL Open Font License 1.1（handwritten-font-LICENSE.txt）
- MelonLoader（https://github.com/LavaGang/MelonLoader），Apache License 2.0
- 遊戲的文字、名稱與素材，權利屬於 Questing Goose Studio。


Probably Stolen Traditional Chinese mod (unofficial) v{version}
--------------------------------------------------------------

Shows the game's Simplified Chinese text in Traditional Chinese (Taiwan).
Unofficial; not affiliated with Questing Goose Studio.

This mod has not been thoroughly tested yet. Bug reports are welcome
(the Bugs tab on the Nexus page, or https://github.com/dbfoxtw/probably-stolen-demo-zh-hant/issues).

Install
1. Install MelonLoader 0.7.3 (https://github.com/LavaGang/MelonLoader/releases) and start the game once.
   The first start takes a while and needs an internet connection
   (the game is a Unity IL2CPP build, so MelonLoader generates its assemblies first).
2. Close the game. Extract the Mods and UserData folders from this archive into the game folder
   (next to Probably Stolen.exe), merging with the existing folders.
3. Start the game and choose the Simplified Chinese language in the settings.
   With the mod installed, the menu shows it as Traditional Chinese.

Uninstall
Delete Mods\ProbablyStolenZhHant.dll and the UserData\ZhHant folder.

Notes
- Display-only: game files, string tables, and saves are never modified. Saves work with or without the mod.
- No network access. No data is collected or sent.
- Known conflicts: other translation mods, and mods that change TextMeshPro text or fonts.

License
- This mod: MIT (LICENSE.txt). Source: https://github.com/dbfoxtw/probably-stolen-demo-zh-hant
- OpenCC dictionaries: Apache License 2.0 (opencc\LICENSE)
- LXGW WenKai TC font: SIL Open Font License 1.1 (handwritten-font-LICENSE.txt)
- The game's text, names, and assets belong to Questing Goose Studio.
