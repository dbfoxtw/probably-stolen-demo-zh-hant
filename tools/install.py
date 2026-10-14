r"""從原始碼建置 mod 並安裝到遊戲。

事前準備：
1. 安裝 MelonLoader 0.7.3（https://github.com/LavaGang/MelonLoader），並啟動一次遊戲，
   讓它產生 MelonLoader\Il2CppAssemblies（編譯 mod 要參考這些組件）。
2. .NET SDK 6 以上、Python 3.10 以上（pip install -r requirements.txt）。

用法：
  python tools/install.py build        產生資料並編譯 mod（不動遊戲）
  python tools/install.py install      建置後複製到遊戲的 Mods\ 與 UserData\ZhHant\
  python tools/install.py uninstall    移除 mod（MelonLoader 本身不動）
都可加 --game "遊戲資料夾"；install 可加 --font 手寫字型.ttf（支援繁體的手寫字型，取代遊戲的簡體手寫字型），
以及 --debug（記錄後備轉換到 UserData\ZhHant\misses.tsv、每分鐘統計、缺翻自我測試）。
找遊戲資料夾、編譯的做法在共用的 modkit.py（Probably Stolen 的 mod 都用同一份），這裡只寫翻譯資料與字型的安裝。
"""
from __future__ import annotations

import argparse
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
# 共用的 modkit.py：公開 repo 裡和這個檔放在一起，私人 monorepo 裡在根目錄的 tools/
sys.path.insert(0, str(next((d for d in (HERE, *(p / "tools" for p in HERE.parents)) if (d / "modkit.py").is_file()), HERE)))
import build_mod_data  # noqa: E402
import modkit  # noqa: E402

ROOT = HERE.parent
DATA_OUT = ROOT / "build" / "mod-data"
DLL = ROOT / "mod" / "bin" / "Release" / "ProbablyStolenZhHant.dll"
OLD_FILES = ["exact.tsv", "phrases.tsv", "chars.tsv"]  # 舊版的資料檔，安裝時刪除

find_game_dir = modkit.find_game_dir
is_game_running = modkit.is_game_running


def build_dll(game: Path) -> None:
    # 用呼叫當下的 ROOT、DLL：私人 repo 的 tools/mod_poc.py 會把它們改成指向私人 repo
    modkit.build_dll(ROOT / "mod" / "ProbablyStolenZhHant.csproj", DLL, game, "Unity.TextMeshPro.dll")


def install_files(game: Path, data: Path, font: Path | None, debug: bool = False) -> None:
    """把編譯好的 mod 與資料（build_mod_data 的輸出）複製到遊戲。debug：放除錯標記檔。"""
    modkit.require_game_closed()
    (game / "Mods").mkdir(exist_ok=True)
    shutil.copy2(DLL, game / "Mods" / DLL.name)
    dest = game / "UserData" / "ZhHant"
    dest.mkdir(parents=True, exist_ok=True)
    for name in build_mod_data.DATA_FILES + build_mod_data.OPTIONAL_FILES + OLD_FILES:
        (dest / name).unlink(missing_ok=True)
    for name in build_mod_data.DATA_FILES + build_mod_data.OPTIONAL_FILES:
        if (data / name).is_file():
            shutil.copy2(data / name, dest / name)
    shutil.rmtree(dest / "opencc", ignore_errors=True)
    shutil.copytree(data / "opencc", dest / "opencc")
    (dest / "misses.tsv").unlink(missing_ok=True)  # 每個版本重新累積
    (dest / "debug").unlink(missing_ok=True)
    if debug:
        (dest / "debug").write_text("這個檔存在時，mod 會記錄後備轉換（misses.tsv）、每分鐘統計，並做缺翻自我測試。\n", encoding="utf-8")
    for old in dest.glob("handwritten.*"):
        old.unlink()
    if font:
        shutil.copy2(font, dest / f"handwritten{font.suffix.lower()}")
        print(f"手寫字型：{font.name}")
    print(f"已安裝到 {game}\\Mods 與 {dest}{'（除錯模式）' if debug else ''}")


def uninstall(game: Path) -> None:
    modkit.require_game_closed()
    (game / "Mods" / DLL.name).unlink(missing_ok=True)
    shutil.rmtree(game / "UserData" / "ZhHant", ignore_errors=True)
    print("已移除 mod（MelonLoader 本身仍在；要完全移除請刪除 version.dll、MelonLoader、Mods、Plugins、UserData、UserLibs）。")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("action", choices=["build", "install", "uninstall"])
    ap.add_argument("--game")
    ap.add_argument("--debug", action="store_true", help="除錯模式（見上）")
    ap.add_argument("--font", help="手寫字型 .ttf/.otf（預設 fonts/handwritten.ttf 或 .otf，沒有就維持遊戲原本的字型）")
    args = ap.parse_args()
    game = find_game_dir(args.game)
    if args.action == "uninstall":
        uninstall(game)
        return
    build_mod_data.build(ROOT / "data", DATA_OUT)
    build_dll(game)
    if args.action == "install":
        font = Path(args.font) if args.font else next(
            (p for p in (ROOT / "fonts" / "handwritten.ttf", ROOT / "fonts" / "handwritten.otf") if p.is_file()), None)
        install_files(game, DATA_OUT, font, args.debug)


if __name__ == "__main__":
    main()
