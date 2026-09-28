r"""組出 mod 要安裝到遊戲 UserData\ZhHant\ 的資料：翻譯規則（data/）加上 OpenCC s2tw 字典。

翻譯規則不含遊戲原文（見 data/ 各檔開頭的說明），mod 在遊戲裡讀簡中字串表、逐條即時翻譯。
OpenCC 字典取自 Python 套件 OpenCC（pip install OpenCC==1.4.2）附的 .ocd2，用套件內的 opencc_dict 轉成文字檔。

用法：python tools/build_mod_data.py [翻譯規則資料夾，預設 data] [輸出資料夾，預設 build/mod-data]
"""
from __future__ import annotations

import json
import shutil
import subprocess
import sys
from pathlib import Path

import opencc

ROOT = Path(__file__).resolve().parents[1]
OPENCC_DICTS = ["CJK_Compatibility_Ideographs", "STPhrases", "STPhrases_GeneratedFromRegionalPhrases",
                "STCharacters", "TWVariantsPhrases", "TWVariants"]  # 要和 mod/src/OpenCC.cs 一致
DATA_FILES = ["terms.tsv", "overrides.tsv", "hardcoded.tsv", "tables.txt"]
OPTIONAL_FILES = ["fills.tsv"]


def export_opencc(out: Path) -> None:
    pkg = Path(opencc.__file__).parent
    share = pkg / "clib" / "share" / "opencc"
    tool = next((p for p in (pkg / "clib" / "bin" / "opencc_dict.exe", pkg / "clib" / "bin" / "opencc_dict") if p.is_file()), None)
    if tool is None:
        raise SystemExit("找不到 OpenCC 套件附的 opencc_dict，請安裝 OpenCC==1.4.2")
    config = json.loads((share / "s2tw.json").read_text(encoding="utf-8"))
    used = sorted(set(_dict_files(config)))
    if used != sorted(f"{d}.ocd2" for d in OPENCC_DICTS):
        raise SystemExit(f"s2tw.json 用到的字典和 mod 不同，要更新 mod/src/OpenCC.cs：{used}")
    out.mkdir(parents=True, exist_ok=True)
    for d in OPENCC_DICTS:
        subprocess.run([str(tool), "-i", str(share / f"{d}.ocd2"), "-o", str(out / f"{d}.txt"),
                        "-f", "ocd2", "-t", "text"], check=True)
    dist = next(pkg.parent.glob("opencc-*.dist-info"), None) or next(pkg.parent.glob("OpenCC-*.dist-info"))
    lic = next((p for p in (dist / "licenses" / "LICENSE", dist / "LICENSE") if p.is_file()))
    shutil.copy2(lic, out / "LICENSE")
    (out / "README.txt").write_text(
        f"OpenCC {opencc.__version__}（https://github.com/BYVoid/OpenCC）的 s2tw 字典，"
        "由 Python 套件附的 .ocd2 以 opencc_dict 轉成文字檔，內容未修改。授權：Apache License 2.0（見 LICENSE）。\n",
        encoding="utf-8", newline="\n")


def _dict_files(node):
    if isinstance(node, dict):
        if node.get("type") == "ocd2":
            yield node["file"]
        for v in node.values():
            yield from _dict_files(v)
    elif isinstance(node, list):
        for v in node:
            yield from _dict_files(v)


def build(data: Path, out: Path) -> None:
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    export_opencc(out / "opencc")
    for name in DATA_FILES:
        shutil.copy2(data / name, out / name)
    for name in OPTIONAL_FILES:
        if (data / name).is_file():
            shutil.copy2(data / name, out / name)
    print(f"OpenCC {opencc.__version__} 字典 {len(OPENCC_DICTS)} 個＋翻譯規則 → {out}")


def main() -> None:
    data = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "data"
    out = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / "build" / "mod-data"
    build(data, out)


if __name__ == "__main__":
    main()
