r"""Probably Stolen 的 MelonLoader mod 共用的建置、安裝與打包（只用 Python 標準函式庫，不用另外裝套件）。

各 mod 的 tools/install.py 只寫自己的名稱、說明與特有的功能，其餘都呼叫這裡，改一處所有 mod 都跟上
（例如正式版出了以後在 APP_IDS 加上它的 appid）。
私人 monorepo 裡這個檔在根目錄的 tools/；公開 repo 裡和 install.py 放在同一個資料夾（同步公開 repo 時複製過去）。
"""
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import zipfile
from dataclasses import dataclass
from pathlib import Path

# Demo 的 appid；正式版（2026-10-28）應該是另一個 Steam App，出了以後加進來
APP_IDS = ["4349200"]
EXE_NAME = "Probably Stolen.exe"
GAME_DIR_EXAMPLE = r"D:\SteamLibrary\steamapps\common\Probably Stolen Demo"


def _steam_library_dirs() -> list[Path]:
    roots: list[Path] = []
    try:
        import winreg

        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam") as k:
            roots.append(Path(winreg.QueryValueEx(k, "SteamPath")[0]))
    except OSError:
        pass
    roots.append(Path(r"C:\Program Files (x86)\Steam"))
    libs: list[Path] = []
    for root in roots:
        vdf = root / "steamapps" / "libraryfolders.vdf"
        if not vdf.is_file():
            continue
        libs.append(root)
        for m in re.finditer(r'"path"\s+"([^"]+)"', vdf.read_text(encoding="utf-8", errors="replace")):
            libs.append(Path(m.group(1).replace("\\\\", "\\")))
    return libs


def find_game_dir(explicit: str | None = None) -> Path:
    """--game 指定的 → 環境變數 PS_GAME_DIR → Steam 收藏庫裡裝的版本，第一個有遊戲執行檔的。"""
    candidates = [Path(explicit)] if explicit else []
    if os.environ.get("PS_GAME_DIR"):
        candidates.append(Path(os.environ["PS_GAME_DIR"]))
    for lib in _steam_library_dirs():
        for app_id in APP_IDS:
            acf = lib / "steamapps" / f"appmanifest_{app_id}.acf"
            if acf.is_file():
                m = re.search(r'"installdir"\s+"([^"]+)"', acf.read_text(encoding="utf-8", errors="replace"))
                if m:
                    candidates.append(lib / "steamapps" / "common" / m.group(1))
    for c in candidates:
        if (c / EXE_NAME).is_file():
            return c
    raise SystemExit(f'找不到遊戲資料夾，請用 --game 指定，例如：--game "{GAME_DIR_EXAMPLE}"')


def is_game_running() -> bool:
    try:
        out = subprocess.run(["tasklist", "/FI", f"IMAGENAME eq {EXE_NAME}", "/FO", "CSV", "/NH"],
                             capture_output=True, text=True, errors="replace").stdout
    except OSError:
        return False
    return EXE_NAME.lower() in out.lower()


def require_game_closed() -> None:
    if is_game_running():
        raise SystemExit("遊戲正在執行，請先關閉。")


def dotnet() -> str:
    return shutil.which("dotnet") or r"C:\Program Files\dotnet\dotnet.exe"


def build_dll(csproj: Path, dll: Path, game: Path, check_dll: str = "Assembly-CSharp.dll") -> None:
    """編譯 mod。check_dll：先確認 MelonLoader 產生的組件有這個（編譯要參考它們）。"""
    if not (game / "MelonLoader" / "Il2CppAssemblies" / check_dll).is_file():
        raise SystemExit("找不到 MelonLoader\\Il2CppAssemblies：請先安裝 MelonLoader 並啟動一次遊戲。")
    subprocess.run([dotnet(), "build", str(csproj), "-c", "Release", "-nologo", "-v", "q", f"-p:GameDir={game}"],
                   check=True)
    print(f"建置完成：{dll}")


def mod_version(src: Path) -> str:
    """版本號只放在 MelonInfo 一處。"""
    m = re.search(r'MelonInfo\(.*?"(\d+\.\d+\.\d+)"', src.read_text(encoding="utf-8"))
    if not m:
        raise SystemExit(f"讀不到 mod 版本（{src.name} 的 MelonInfo）")
    return m.group(1)


def _toml_value(value: str | bool) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    return json.dumps(value, ensure_ascii=False)  # TOML 的基本字串和 JSON 字串相容（反斜線、引號都跳脫）


def set_pref(game: Path, section: str, key: str, value: str | bool) -> None:
    """改 UserData\\MelonPreferences.cfg 的 [section] key；段落或鍵不存在就加上（mod 第一次執行時會讀到這個值）。
    遊戲執行時不能改：MelonLoader 關遊戲時會把記憶體裡的設定寫回去、蓋掉這裡改的。"""
    cfg = game / "UserData" / "MelonPreferences.cfg"
    # utf-8-sig：有 BOM 時第一行的段落名稱才比對得到（比對不到會多加一個同名段落，TOML 解析失敗、所有 mod 的設定都失效）；
    # 只用 \n 切行：splitlines 會在 U+2028 之類的字元切開，弄壞別的 mod 的字串值
    text = cfg.read_text(encoding="utf-8-sig").replace("\r\n", "\n") if cfg.is_file() else ""
    lines = text.split("\n")
    if lines and lines[-1] == "":
        lines.pop()
    line = f"{key} = {_toml_value(value)}"
    start = next((i for i, s in enumerate(lines) if s.strip() == f"[{section}]"), None)
    if start is None:
        lines += ["", f"[{section}]", line]
    else:
        end = next((i for i in range(start + 1, len(lines)) if lines[i].lstrip().startswith("[")), len(lines))
        hit = next((i for i in range(start + 1, end) if re.match(rf"\s*{re.escape(key)}\s*=", lines[i])), None)
        if hit is None:
            lines.insert(start + 1, line)
        else:
            lines[hit] = line
    cfg.parent.mkdir(parents=True, exist_ok=True)
    cfg.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"設定檔 [{section}] {line}")


def write_text_crlf(src: Path, dest: Path, version: str | None = None) -> None:
    """給玩家看的文字檔：玩家多半用記事本打開，換行用 CRLF；version 有給就換掉範本裡的 {version}。"""
    text = src.read_text(encoding="utf-8")
    if version is not None:
        text = text.replace("{version}", version)
    dest.write_text(text, encoding="utf-8", newline="\r\n")


def write_zip(stage: Path, out: Path) -> Path:
    """把 stage 底下的檔案壓成 out（解壓到遊戲資料夾即可），印出檔案清單。"""
    out.parent.mkdir(parents=True, exist_ok=True)
    out.unlink(missing_ok=True)
    files = sorted(p for p in stage.rglob("*") if p.is_file())
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
        for p in files:
            zf.write(p, p.relative_to(stage).as_posix())
    size = out.stat().st_size
    print(f"已打包 {len(files)} 個檔案（{f'{size / 1e6:.1f} MB' if size >= 1e6 else f'{size / 1e3:.0f} KB'}）：{out}")
    for p in files:
        print(f"  {p.relative_to(stage).as_posix()}")
    return out


def new_stage(root: Path) -> Path:
    """打包用的暫存資料夾 build\\package（每次清空重來）。"""
    stage = root / "build" / "package"
    if stage.exists():
        shutil.rmtree(stage)
    stage.mkdir(parents=True)
    return stage


@dataclass
class Mod:
    """只有一個 dll、資料夾由 mod 自己建的 mod（待辦清單、交易所預覽、一鍵關窗）：build／install／uninstall／package
    都照同一套做，用 main(mod) 就好。"""
    root: Path            # mod 的根目錄（mod/、build/ 所在）
    name: str             # csproj 與 dll 的名稱
    main_cs: str          # mod/src 底下有 MelonInfo（版本號）的檔
    data_dir: str         # 遊戲資料夾 UserData\ 底下的資料夾，和 mod 程式裡的一致
    zip_name: str         # 打包檔名：<zip_name>-<版本>.zip
    debug_note: str       # 除錯標記檔的內容（說明這個檔存在時 mod 會多記什麼）
    data_label: str = "資料"           # 訊息裡怎麼稱呼 data_dir 裡的東西
    pref_section: str | None = None    # MelonPreferences.cfg 的段落（有用 MelonPreferences 的 mod）
    check_dll: str = "Assembly-CSharp.dll"

    @property
    def csproj(self) -> Path:
        return self.root / "mod" / f"{self.name}.csproj"

    @property
    def dll(self) -> Path:
        return self.root / "mod" / "bin" / "Release" / f"{self.name}.dll"

    @property
    def templates(self) -> Path:
        """打包範本（package\\README.txt、LICENSE）：私人 repo 放在 public\\（只給公開 repo 的檔案），公開 repo 放在根目錄。"""
        return self.root / "public" if (self.root / "public").is_dir() else self.root

    def build(self, game: Path) -> None:
        build_dll(self.csproj, self.dll, game, self.check_dll)

    def install(self, game: Path, debug: bool) -> None:
        require_game_closed()
        (game / "Mods").mkdir(exist_ok=True)
        shutil.copy2(self.dll, game / "Mods" / self.dll.name)
        data = game / "UserData" / self.data_dir
        flag = data / "debug"
        if debug:
            data.mkdir(parents=True, exist_ok=True)
            flag.write_text(self.debug_note, encoding="utf-8")
        else:
            flag.unlink(missing_ok=True)
        pref = f"；設定在 {game}\\UserData\\MelonPreferences.cfg 的 [{self.pref_section}]" if self.pref_section else ""
        print(f"已安裝到 {game}\\Mods{'（除錯模式）' if debug else ''}；{self.data_label}在 {data}{pref}")

    def uninstall(self, game: Path) -> None:
        require_game_closed()
        (game / "Mods" / self.dll.name).unlink(missing_ok=True)
        data = game / "UserData" / self.data_dir
        (data / "debug").unlink(missing_ok=True)
        pref = f"，以及 MelonPreferences.cfg 裡的 [{self.pref_section}] 段落" if self.pref_section else ""
        print(f"已移除 mod。{self.data_label}沒有刪除，要完全清掉請自己刪除 {data}{pref}")

    def package(self, out_dir: Path) -> Path:
        """把編譯好的 dll、說明與授權組成給玩家的 zip，傳回 zip 的路徑。
        不放除錯標記檔：玩家版沒有除錯模式。資料與設定由 mod 第一次執行時自己建，zip 裡只放說明和授權。"""
        version = mod_version(self.root / "mod" / "src" / self.main_cs)
        stage = new_stage(self.root)
        (stage / "Mods").mkdir()
        shutil.copy2(self.dll, stage / "Mods" / self.dll.name)
        data = stage / "UserData" / self.data_dir
        data.mkdir(parents=True)
        write_text_crlf(self.templates / "package" / "README.txt", data / "README.txt", version)
        write_text_crlf(self.templates / "LICENSE", data / "LICENSE.txt")
        return write_zip(stage, out_dir / f"{self.zip_name}-{version}.zip")


def main(mod: Mod) -> None:
    """tools/install.py 的命令列：build／install [--debug]／uninstall／package [--out]，都可加 --game。"""
    ap = argparse.ArgumentParser()
    ap.add_argument("action", choices=["build", "install", "uninstall", "package"])
    ap.add_argument("--game")
    ap.add_argument("--debug", action="store_true", help="除錯模式（見 tools/install.py 開頭的說明）")
    ap.add_argument("--out", type=Path, default=mod.root / "build", help="package 的輸出資料夾")
    args = ap.parse_args()
    game = find_game_dir(args.game)
    if args.action == "uninstall":
        mod.uninstall(game)
        return
    if args.action == "install":
        require_game_closed()
    mod.build(game)
    if args.action == "install":
        mod.install(game, args.debug)
    elif args.action == "package":
        mod.package(args.out)
