r"""產生發布版的手寫字型：佑字 肅（Yuji Syuku，SIL OFL 1.1）補上繁中缺的字，並調整刪節號。

遊戲的手寫字型用在簽名與紙條，原版是只收簡體的馬善政體（毛筆）。佑字是書法家片岡佑之的毛筆字，風格最接近；
簽名與紙條用到的字只缺「你喔嗎汙」，這裡用它自己的部件拼出來：左邊取一個字的偏旁輪廓，右邊取另一個字的右側輪廓，
必要時水平縮放。刪節號「…」的三個點擠在中間，兩個連用（……）時中間會斷開，所以改成平均分布在整個字寬。

佑字沒有保留字型名稱（Reserved Font Name），OFL 允許修改後散布；修改版仍是 OFL。字型名稱改成 Yuji Syuku ZhHant，
授權檔附上原作者的版權聲明、修改說明與 OFL 全文。

原檔：https://github.com/google/fonts/tree/main/ofl/yujisyuku（YujiSyuku-Regular.ttf 與 OFL.txt，v3.002）
用法：python tools/make_handwritten_font.py [--src 原檔] [--ofl OFL.txt] [--out 輸出資料夾]
  預設讀 fonts/candidates/YujiSyuku-Regular.ttf 與 fonts/candidates/YujiSyuku-OFL.txt，
  輸出 fonts/handwritten.ttf 與 fonts/handwritten.LICENSE.txt（安裝與打包預設用這兩個檔）。
需要 fonttools（pip install -r requirements.txt）。
"""
from __future__ import annotations

import argparse
from pathlib import Path

from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[1]
FAMILY, NEW_FAMILY = "Yuji Syuku", "Yuji Syuku ZhHant"
PS, NEW_PS = "YujiSyuku", "YujiSyukuZhHant"

# 新字：(左邊取哪個字, 左邊輪廓中心 x 上限, 右邊取哪個字, 右邊輪廓中心 x 下限, 左邊 x 縮放, 右邊 x 縮放, 預期筆畫數)
# 縮放：左邊以 x=0 為基準縮窄，右邊以右緣（字寬）為基準縮窄。筆畫數是 v3.002 的輪廓數，對不上代表原檔改了，要重新調整。
RECIPES = {
    "你": ("他", 330, "弥", 420, 1.0, 1.0, (1, 3)),   # 亻＋尓
    "喔": ("呼", 300, "渥", 400, 0.72, 0.88, (2, 3)),  # 口＋屋（這套字的口很寬，要縮窄；「握」的扌和屋連成一筆，拆不開）
    "嗎": ("喝", 300, "媽", 380, 1.0, 1.0, (2, 8)),   # 口＋馬
    "汙": ("汗", 300, "紆", 480, 1.0, 1.0, (3, 1)),   # 氵＋于
}
ELLIPSIS = "…"


class Font:
    def __init__(self, path: Path):
        self.tt = TTFont(str(path))
        self.cmap = self.tt.getBestCmap()
        self.gs = self.tt.getGlyphSet()

    def contours(self, ch: str) -> list[list[tuple[str, tuple]]]:
        """一個字的輪廓（組合字形先拆開），每個輪廓是一串畫筆指令。"""
        if ord(ch) not in self.cmap:
            raise SystemExit(f"原檔沒有「{ch}」，補字的做法要重新調整")
        pen = DecomposingRecordingPen(self.gs)
        self.gs[self.cmap[ord(ch)]].draw(pen)
        out, cur = [], []
        for op, args in pen.value:
            cur.append((op, args))
            if op in ("closePath", "endPath"):
                out.append(cur)
                cur = []
        return out

    def bounds(self, contour) -> tuple[float, float, float, float]:
        bp = BoundsPen(self.gs)
        for op, args in contour:
            getattr(bp, op)(*args)
        return bp.bounds

    def set_glyph(self, name: str, parts: list[tuple[list, tuple]], like: str) -> None:
        """用 (輪廓, 仿射變換) 組出字形，寫進 glyf；水平與垂直度量照 like 這個字形，左側空白依新字形重算。"""
        pen = TTGlyphPen(None)
        for contour, t in parts:
            tp = TransformPen(pen, t)
            for op, args in contour:
                getattr(tp, op)(*args)
        glyf = self.tt["glyf"]
        if name not in glyf:
            self.tt.setGlyphOrder(self.tt.getGlyphOrder() + [name])
        g = pen.glyph()
        glyf[name] = g
        g.recalcBounds(glyf)
        adv, _ = self.tt["hmtx"][like]
        self.tt["hmtx"][name] = (adv, g.xMin)
        if "vmtx" in self.tt:
            vadv, vtsb = self.tt["vmtx"][like]
            like_ymax = glyf[like].yMax if hasattr(glyf[like], "yMax") else g.yMax
            self.tt["vmtx"][name] = (vadv, vtsb + like_ymax - g.yMax)


def compose(f: Font) -> None:
    for new, (lch, lmax, rch, rmin, lsx, rsx, expect) in RECIPES.items():
        if ord(new) in f.cmap:
            raise SystemExit(f"原檔已經有「{new}」，不用再補（原檔可能更新了）")
        left = [c for c in f.contours(lch) if (lambda b: (b[0] + b[2]) / 2 < lmax)(f.bounds(c))]
        right = [c for c in f.contours(rch) if (lambda b: (b[0] + b[2]) / 2 > rmin)(f.bounds(c))]
        if (len(left), len(right)) != expect:
            raise SystemExit(f"「{new}」取到的筆畫數 {len(left)}＋{len(right)} 和預期 {expect} 不同，原檔可能改了")
        adv = f.tt["hmtx"][f.cmap[ord(lch)]][0]
        parts = [(c, (lsx, 0, 0, 1, 0, 0)) for c in left] + [(c, (rsx, 0, 0, 1, adv * (1 - rsx), 0)) for c in right]
        name = f"uni{ord(new):04X}"
        f.set_glyph(name, parts, like=f.cmap[ord(lch)])
        for t in f.tt["cmap"].tables:
            if t.isUnicode() and t.format in (4, 12):
                t.cmap[ord(new)] = name
        print(f"補字：{new} ← {lch}（左 {len(left)} 筆）＋{rch}（右 {len(right)} 筆）")
    f.cmap = f.tt.getBestCmap()


def respace_ellipsis(f: Font) -> None:
    """「…」的三個點平均分布在整個字寬（中心在 1/6、3/6、5/6），連用時點距一致。"""
    name = f.cmap[ord(ELLIPSIS)]
    dots = sorted(f.contours(ELLIPSIS), key=lambda c: f.bounds(c)[0])
    if len(dots) != 3:
        raise SystemExit(f"「…」預期 3 個點，實際 {len(dots)} 個，原檔可能改了")
    adv = f.tt["hmtx"][name][0]
    parts = []
    for i, c in enumerate(dots):
        b = f.bounds(c)
        parts.append((c, (1, 0, 0, 1, adv * (2 * i + 1) / 6 - (b[0] + b[2]) / 2, 0)))
    f.set_glyph(name, parts, like=name)
    print(f"刪節號：三個點的中心改到 {adv // 6}、{adv // 2}、{adv * 5 // 6}（字寬 {adv}）")


def rename(f: Font) -> None:
    """修改版改名（OFL 建議），版本加註；原作者版權聲明不動。"""
    for rec in f.tt["name"].names:
        s = rec.toUnicode()
        if rec.nameID in (1, 4, 16, 18):
            s = s.replace(FAMILY, NEW_FAMILY)
        elif rec.nameID in (3, 6):
            s = s.replace(PS, NEW_PS)
        elif rec.nameID == 5:
            s = f"{s}; ZhHant 1"
        else:
            continue
        rec.string = s
    f.tt["name"].setName("Yuji Syuku with 4 Traditional Chinese glyphs (你喔嗎汙) composed from its own glyphs "
                         "and a respaced ellipsis, for the Probably Stolen Demo Traditional Chinese mod.", 10, 3, 1, 0x409)


def license_text(ofl: Path) -> str:
    """原作者的版權聲明＋修改說明＋OFL 全文（取自原檔附的 OFL.txt）。"""
    text = ofl.read_text(encoding="utf-8-sig")
    head, sep, body = text.partition("-----")
    if not sep:
        raise SystemExit(f"{ofl} 看起來不是 OFL 授權檔")
    note = ("\nThis is a modified version (\"Yuji Syuku ZhHant\") made for the Probably Stolen Demo\n"
            "Traditional Chinese mod (https://github.com/dbfoxtw/probably-stolen-demo-zh-hant):\n"
            "four Traditional Chinese glyphs (你喔嗎汙) were composed from the font's own glyphs,\n"
            "and the ellipsis (…) was respaced. The modified version is also licensed under the\n"
            "SIL Open Font License, Version 1.1.\n\n\n")
    return head.rstrip() + "\n" + note + sep + body


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", type=Path, default=ROOT / "fonts" / "candidates" / "YujiSyuku-Regular.ttf")
    ap.add_argument("--ofl", type=Path, default=ROOT / "fonts" / "candidates" / "YujiSyuku-OFL.txt")
    ap.add_argument("--out", type=Path, default=ROOT / "fonts")
    args = ap.parse_args()
    f = Font(args.src)
    compose(f)
    respace_ellipsis(f)
    rename(f)
    args.out.mkdir(parents=True, exist_ok=True)
    out = args.out / "handwritten.ttf"
    f.tt.save(str(out))
    (args.out / "handwritten.LICENSE.txt").write_text(license_text(args.ofl), encoding="utf-8", newline="\n")
    print(f"→ {out}（{out.stat().st_size / 1e6:.1f} MB）與 {out.with_name('handwritten.LICENSE.txt').name}")


if __name__ == "__main__":
    main()
