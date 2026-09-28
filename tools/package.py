r"""打包發布用的壓縮檔（Nexus Mods）。玩家把壓縮檔解壓到遊戲資料夾即可（要先裝好 MelonLoader 0.7.3）。

壓縮檔結構：
  Mods\ProbablyStolenZhHant.dll
  UserData\ZhHant\   翻譯資料、opencc\（OpenCC 字典，Apache-2.0）、
                     handwritten.ttf 與 handwritten-font-LICENSE.txt（手寫字型與它的授權）、README.txt、LICENSE.txt（MIT）

用法：python tools/package.py [--game 遊戲資料夾] [--font 字型檔] [--font-license 字型授權檔] [--out 輸出資料夾]
  字型預設 fonts/handwritten.ttf（或 .otf），授權預設 fonts/handwritten.LICENSE.txt。
  沒有字型就不換手寫字型；附字型時一定要附它的授權檔。
"""
from __future__ import annotations

import argparse
import re
import shutil
import sys
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_mod_data  # noqa: E402
import install  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]  # mod/、data/、build/ 所在的 repo 根目錄
HERE = Path(__file__).resolve().parents[1]  # 打包範本（package/README.txt、LICENSE）所在的資料夾


def mod_version() -> str:
    src = (ROOT / "mod" / "src" / "ZhHantMod.cs").read_text(encoding="utf-8")
    m = re.search(r'MelonInfo\(.*?"(\d+\.\d+\.\d+)"', src)
    if not m:
        raise SystemExit("讀不到 mod 版本（ZhHantMod.cs 的 MelonInfo）")
    return m.group(1)


def assemble(data: Path, dll: Path, font: Path | None, font_license: Path | None, out_dir: Path) -> Path:
    """把編譯好的 mod、翻譯資料（build_mod_data 的輸出）、手寫字型組成壓縮檔，傳回壓縮檔路徑。"""
    if font and not (font_license and font_license.is_file()):
        raise SystemExit(f"附上手寫字型 {font.name} 時，要一併附上它的授權檔（--font-license）")
    version = mod_version()
    stage = ROOT / "build" / "package"
    if stage.exists():
        shutil.rmtree(stage)
    (stage / "Mods").mkdir(parents=True)
    shutil.copy2(dll, stage / "Mods" / dll.name)
    z = stage / "UserData" / "ZhHant"
    z.mkdir(parents=True)
    for name in build_mod_data.DATA_FILES + build_mod_data.OPTIONAL_FILES:
        if (data / name).is_file():
            shutil.copy2(data / name, z / name)
    shutil.copytree(data / "opencc", z / "opencc")
    if font:
        shutil.copy2(font, z / f"handwritten{font.suffix.lower()}")
        shutil.copy2(font_license, z / "handwritten-font-LICENSE.txt")
    readme = (HERE / "package" / "README.txt").read_text(encoding="utf-8").replace("{version}", version)
    (z / "README.txt").write_text(readme, encoding="utf-8", newline="\r\n")
    (z / "LICENSE.txt").write_text((HERE / "LICENSE").read_text(encoding="utf-8"), encoding="utf-8", newline="\r\n")

    out_dir.mkdir(parents=True, exist_ok=True)
    out = out_dir / f"ProbablyStolen-ZhHant-{version}.zip"
    out.unlink(missing_ok=True)
    files = sorted(p for p in stage.rglob("*") if p.is_file())
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
        for p in files:
            zf.write(p, p.relative_to(stage).as_posix())
    print(f"已打包 {len(files)} 個檔案（{out.stat().st_size / 1e6:.1f} MB）：{out}")
    for p in files:
        print(f"  {p.relative_to(stage).as_posix()}")
    return out


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--game")
    ap.add_argument("--font", type=Path)
    ap.add_argument("--font-license", type=Path)
    ap.add_argument("--out", type=Path, default=ROOT / "build")
    args = ap.parse_args()
    font = args.font or next((p for p in (ROOT / "fonts" / "handwritten.ttf", ROOT / "fonts" / "handwritten.otf") if p.is_file()), None)
    font_license = args.font_license or (ROOT / "fonts" / "handwritten.LICENSE.txt" if font else None)
    game = install.find_game_dir(args.game)
    build_mod_data.build(ROOT / "data", install.DATA_OUT)
    install.build_dll(game)
    assemble(install.DATA_OUT, install.DLL, font, font_license, args.out)


if __name__ == "__main__":
    main()
