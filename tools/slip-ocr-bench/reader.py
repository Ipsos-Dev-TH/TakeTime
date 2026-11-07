"""Reference implementation of the local slip amount reader.

`Take Time BangPhra/Class/Payments/SlipCheck/LocalSlipAmountReader.cs` is a 1:1 port of this file — change both.

Pipeline
  0. decode (EXIF rotation) -> card crop for photos of a phone (largest bright+neutral blob, min(R,G,B))
  1. preprocess: grayscale (BT.601 luma) -> dark-mode invert (mean < 100) -> scale to ~TARGET_W -> 1..99 % stretch
  2. pass 1: tesseract tha+eng --psm 6 TSV -> words -> lines (boxes); no amount label found -> pass 1b
     (--psm 11 at 0.72x, merged only if it finds a label)
  3. candidates: money tokens on lines, scored by keyword + geometry (right of label on the same row, or below it)
  4. ROIs: top candidates' word boxes + label bands (right of / below the label); letter-only words blanked
  5. pass 2: one montage of all ROIs, each normalised to ~P2_H px text height, gray + Otsu versions,
     tesseract eng --psm 6 whitelist "0123456789.,B" TSV -> lines mapped back to ROI by y.
     A leading baht sign is detected by shape (blob taller than the digits, sticking out above and below) and erased.
  6. vote -> decision. Returning a wrong number is worse than "unreadable" (bypass), so low agreement => None.
     Confidence >= 85 only when pass 1 and >= 2 pass-2 readings agree in an amount-label context with no rival.

CLI:  python reader.py SLIP.jpg [expected]   -> prints every pass, candidates, votes and the decision
"""
import os
import re
import subprocess
import sys
import tempfile
import time
import unicodedata

import numpy as np
from PIL import Image, ImageOps

TESS = os.environ.get("TESS", "tesseract")
TARGET_W = int(os.environ.get("TARGET_W", "1500"))
P2_H = int(os.environ.get("P2_H", "48"))
MIN_CONF = int(os.environ.get("MIN_CONF", "60"))
PASS1B = os.environ.get("PASS1B", "1") == "1"

AMOUNT_KW = ["จำนวนเงิน", "จำนวน", "ยอดเงิน", "ยอดโอน", "ยอดชำระ", "ยอดรวม", "amount", "total"]
STRONG_KW = ["จำนวนเงิน", "จำนวน", "ยอดเงิน", "ยอดโอน", "ยอดชำระ", "amount"]
EXCLUDE_KW = ["ค่าธรรมเนียม", "fee", "คงเหลือ", "balance", "available", "ใช้ได้", "อ้างอิง", "เลขที่", "ref", "vat",
              "ภาษี", "transaction", "บัญชี", "kb", "รายการ"]
CURRENCY_KW = ["บาท", "thb", "฿"]
DATE_KW = ["ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค.",
           "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec", "วันที่", "เวลา", "date"]
MONEY_RE = re.compile(r"(?<![\d.,])(\d{1,3}(?:[,.' ]\d{3})+|\d+)[.,](\d{2})(?![\d])")
TIME_RE = re.compile(r"\d{1,2}[:.]\d{2}\s*น")
HHMM_RE = re.compile(r"\b\d{1,2}:\d{2}\b")
DATE_RE = re.compile(r"\b\d{1,2}/\d{1,2}/\d{2,4}\b")
GROUP_RE = re.compile(r"\d[,.' ]\d{3}")
BAHT_PREFIX_RE = re.compile(r"(?:^|[^\w])[B฿]\s?$")
MAX_AMOUNT = 2000000.0


def normalize(t):
    t = unicodedata.normalize("NFC", t or "")
    t = t.replace("ํา", "ำ").replace("​", "").replace("﻿", "")
    t = t.translate(str.maketrans("๐๑๒๓๔๕๖๗๘๙", "0123456789"))
    t = re.sub(r"(?<=[\d,.])[Oo](?=[\d,.])", "0", t)
    t = re.sub(r"(?<=[.,]\d)[Oo](?![\w])", "0", t)
    t = re.sub(r"(?<=[\d,.])[lI|](?=[\d,.])", "1", t)
    t = re.sub(r"(?<=\d)S(?=\d)", "5", t)
    return t


def kw_in(low, kws):
    return any(k in low for k in kws)


# ── image helpers (C#: System.Drawing + LockBits) ───────────────────────────────────────────

def load_gray(path, with_min=False):
    im = ImageOps.exif_transpose(Image.open(path)).convert("RGB")
    a = np.asarray(im).astype(np.int32)
    g = ((a[..., 0] * 299 + a[..., 1] * 587 + a[..., 2] * 114) // 1000).astype(np.uint8)
    if with_min:
        return g, a.min(axis=2).astype(np.uint8)
    return g


AUTOCROP = os.environ.get("AUTOCROP", "1") == "1"


def card_box(mn):
    """Photo of a phone: find the bright, neutral slip card (min(R,G,B) high — beige floors/skin/tables have a
    weak blue channel) as the largest bright component on a ~240px thumbnail. Returns (l,t,r,b) or None."""
    h, w = mn.shape
    k = max(1, w // 240)
    sh, sw = h // k, w // k
    if sh < 20 or sw < 20:
        return None
    small = mn[:sh * k, :sw * k].reshape(sh, k, sw, k).astype(np.int32).sum(axis=(1, 3)) // (k * k)
    small = small.astype(np.uint8)
    thr = max(150, otsu(small))
    lab, comps = components(small > thr)
    if not comps:
        return None
    kbest = max(comps, key=lambda kk: comps[kk][4])
    c = list(comps[kbest])
    if c[4] < sh * sw * 0.12:
        return None
    # glare / moire / a notification banner can split the card: absorb bright blobs stacked above/below it
    # (same column, small vertical gap) so the crop never cuts the amount line off
    changed = True
    used = {kbest}
    while changed:
        changed = False
        for kk, o in comps.items():
            if kk in used or o[4] < sh * sw * 0.01:
                continue
            ov = min(c[2], o[2]) - max(c[0], o[0]) + 1
            gap = max(o[1] - c[3], c[1] - o[3])
            if ov >= 0.6 * min(c[2] - c[0] + 1, o[2] - o[0] + 1) and gap <= sh * 0.12:
                c = [min(c[0], o[0]), min(c[1], o[1]), max(c[2], o[2]), max(c[3], o[3]), c[4] + o[4]]
                used.add(kk)
                changed = True
    bw, bh = c[2] - c[0] + 1, c[3] - c[1] + 1
    if bw * bh > sh * sw * 0.85 or bw < sw * 0.3:
        return None
    m = int(max(bw, bh) * 0.02) + 1
    return (max(0, (c[0] - m) * k), max(0, (c[1] - m) * k), min(w, (c[2] + 1 + m) * k), min(h, (c[3] + 1 + m) * k))


def resize(g, w, h):
    return np.asarray(Image.fromarray(g).resize((max(1, w), max(1, h)), Image.BICUBIC))


def stretch(g):
    hist = np.bincount(g.ravel(), minlength=256)
    c = np.cumsum(hist)
    n = c[-1]
    lo = int(np.searchsorted(c, n * 0.01))
    hi = int(np.searchsorted(c, n * 0.99))
    if hi - lo < 30:
        return g
    x = (g.astype(np.int32) - lo) * 255 // (hi - lo)
    return np.clip(x, 0, 255).astype(np.uint8)


def otsu(g):
    hist = np.bincount(g.ravel(), minlength=256).astype(np.float64)
    total = float(g.size)
    sum_all = float(np.dot(np.arange(256), hist))
    wb = sb = 0.0
    best, thr = -1.0, 127
    for t in range(256):
        wb += hist[t]
        if wb == 0:
            continue
        wf = total - wb
        if wf == 0:
            break
        sb += t * hist[t]
        mb = sb / wb
        mf = (sum_all - sb) / wf
        between = wb * wf * (mb - mf) ** 2
        if between > best:
            best, thr = between, t
    return thr


def binarize(g):
    t = otsu(g)
    b = np.where(g > t, 255, 0).astype(np.uint8)
    if (b == 0).mean() > 0.5:     # text must be dark on light
        b = 255 - b
    return b


def components(mask):
    """4-connected components -> (label image, list of [minx,miny,maxx,maxy,count]) — union-find, mirrors C#."""
    h, w = mask.shape
    lab = np.zeros((h, w), dtype=np.int32)
    parent = [0]

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    nxt = 1
    for y in range(h):
        for x in np.nonzero(mask[y])[0]:
            up = lab[y - 1, x] if y > 0 else 0
            left = lab[y, x - 1] if x > 0 else 0
            if up and left:
                a, b = find(up), find(left)
                lab[y, x] = min(a, b)
                if a != b:
                    parent[max(a, b)] = min(a, b)
            elif up or left:
                lab[y, x] = up or left
            else:
                parent.append(nxt)
                lab[y, x] = nxt
                nxt += 1
    roots = np.array([find(i) for i in range(nxt)], dtype=np.int32)
    lab = roots[lab]
    boxes = {}
    ys, xs = np.nonzero(lab)
    ls = lab[ys, xs]
    for k in np.unique(ls):
        sel = ls == k
        boxes[int(k)] = [int(xs[sel].min()), int(ys[sel].min()), int(xs[sel].max()), int(ys[sel].max()), int(sel.sum())]
    return lab, boxes


def find_baht_glyph(b):
    """b: binary (0 = ink). The ฿ sign is the leftmost blob and its vertical bar sticks out above AND below the digit
    row. Returns the component label to erase, or 0."""
    h, w = b.shape
    lab, comps = components(b == 0)
    big = [(k, c) for k, c in comps.items() if (c[3] - c[1] + 1) >= h * 0.25 and c[4] >= 12]
    if len(big) < 4:
        return lab, 0
    big.sort(key=lambda kc: kc[1][0])
    k0, first = big[0]
    rest = [c for _, c in big[1:] if (c[3] - c[1] + 1) >= h * 0.4]   # digits only (skip , .)
    if len(rest) < 3:
        return lab, 0
    hs = sorted(c[3] - c[1] + 1 for c in rest)
    med = hs[len(hs) // 2]
    # compare with the two blobs right next to it (robust to a slightly rotated line)
    nt = (rest[0][1] + rest[1][1]) / 2.0
    nb = (rest[0][3] + rest[1][3]) / 2.0
    fh = first[3] - first[1] + 1
    protrude = max(1.5, med * 0.035)
    if fh >= med * 1.07 and first[1] <= nt - protrude and first[3] >= nb + protrude:
        return lab, k0
    if fh >= med * 1.08 or (fh >= med * 1.03 and first[1] < nt and first[3] > nb):
        return lab, -1      # suspicious (could be ฿ with a short bar) — caller must not trust a leading 8/6
    return lab, 0


def dilate(m, r):
    out = m.copy()
    h, w = m.shape
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            ys0, ys1 = max(0, dy), min(h, h + dy)
            xs0, xs1 = max(0, dx), min(w, w + dx)
            out[ys0:ys1, xs0:xs1] |= m[ys0 - dy:ys1 - dy, xs0 - dx:xs1 - dx]
    return out


# ── tesseract ───────────────────────────────────────────────────────────────────────────────

def run_tess(img_u8, args, timeout=20):
    fd, p = tempfile.mkstemp(suffix=".png", prefix="ttslip_")
    os.close(fd)
    try:
        Image.fromarray(img_u8).save(p)
        r = subprocess.run([TESS, p, "stdout"] + args, capture_output=True, timeout=timeout)
        return r.returncode, r.stdout.decode("utf-8", "replace"), r.stderr.decode("utf-8", "replace")
    except subprocess.TimeoutExpired:
        return -1, "", "timeout"
    finally:
        try:
            os.remove(p)
        except OSError:
            pass


class Line:
    __slots__ = ("words", "text", "spans", "box", "hmed")


def parse_tsv(tsv, scale=1.0):
    groups = {}
    order = []
    for row in tsv.splitlines()[1:]:
        c = row.split("\t")
        if len(c) < 12 or c[0] != "5" or c[11].strip() == "":
            continue
        key = (c[1], c[2], c[3], c[4])
        if key not in groups:
            groups[key] = []
            order.append(key)
        groups[key].append((c[11], int(round(int(c[6]) * scale)), int(round(int(c[7]) * scale)),
                            int(round(int(c[8]) * scale)), int(round(int(c[9]) * scale))))
    out = []
    for k in order:
        ws = sorted(groups[k], key=lambda x: x[1])
        L = Line()
        L.words = ws
        L.hmed = sorted(x[4] for x in ws)[len(ws) // 2]
        text = ""
        spans = []
        prev_r = None
        for (t, l, tp, w, h) in ws:
            if prev_r is not None and (l - prev_r) > max(4, L.hmed * 0.35):
                text += " "
            nt = normalize(t)
            spans.append((len(text), len(text) + len(nt)))
            text += nt
            prev_r = l + w
        L.text = text
        L.spans = spans
        L.box = (min(x[1] for x in ws), min(x[2] for x in ws), max(x[1] + x[3] for x in ws), max(x[2] + x[4] for x in ws))
        out.append(L)
    out.sort(key=lambda L: (L.box[1] + L.box[3]) / 2.0)
    return out


THAI_MARKS_RE = re.compile("[\u0e47-\u0e4e]")


def kw_text(s):
    """keyword matching form: lower-case, no spaces, Thai tone/diacritic marks dropped and sara-am -> sara-aa
    (OCR adds/loses marks: 'จําน่วน' must still match 'จำนวน'). May change length."""
    s = s.lower().replace(" ", "").replace("\u0e33", "\u0e32")
    return THAI_MARKS_RE.sub("", s)


def kws(lst):
    return [kw_text(k) for k in lst]


def label_box(L):
    """bbox of the words spelling an amount keyword; None if absent. Returns (box, strong)."""
    s = ""
    owner = []
    for i, (t, l, tp, w, h) in enumerate(L.words):
        nt = kw_text(normalize(t))
        s += nt
        owner += [i] * len(nt)
    for k0 in AMOUNT_KW:
        k = kw_text(k0)
        j = s.find(k)
        if j >= 0:
            idx = sorted(set(owner[j:j + len(k)]))
            ws = [L.words[i] for i in idx]
            return (min(w[1] for w in ws), min(w[2] for w in ws), max(w[1] + w[3] for w in ws),
                    max(w[2] + w[4] for w in ws)), k0 in STRONG_KW
    return None, False


DIGITISH_RE = re.compile(r"^[0-9.,'฿BOolI|S]+$")


def token_box(L, a, b):
    idx = [i for i, (s, e) in enumerate(L.spans) if e > a and s < b]
    if not idx:
        return None, ""
    i0, i1 = min(idx), max(idx)
    # extend with adjacent digit-ish words (e.g. "B1" + "5,000.00" for ฿15,000.00)
    while i0 > 0 and DIGITISH_RE.match(L.words[i0 - 1][0]) and \
            L.words[i0][1] - (L.words[i0 - 1][1] + L.words[i0 - 1][3]) < L.hmed * 1.2:
        i0 -= 1
    ws = L.words[i0:i1 + 1]
    prefix = L.text[L.spans[i0][0]:a]
    return (min(w[1] for w in ws), min(w[2] for w in ws), max(w[1] + w[3] for w in ws), max(w[2] + w[4] for w in ws)), prefix


def parse_value(m):
    whole = re.sub(r"[,.' ]", "", m.group(1))
    try:
        v = round(float(whole + "." + m.group(2)), 2)
    except ValueError:
        return None
    return v if 0 < v <= MAX_AMOUNT else None


def find_candidates(lines, W):
    labels = []
    for i, L in enumerate(lines):
        low = kw_text(L.text)
        if kw_in(low, K_AMOUNT_KW) and not kw_in(low, K_EXCLUDE_KW):
            lb, strong = label_box(L)
            if lb:
                labels.append((i, lb, strong))
    cands = []
    for i, L in enumerate(lines):
        low = kw_text(L.text)
        excl = kw_in(low, K_EXCLUDE_KW)
        if not excl and i > 0:
            # value printed under its label (KBank "ค่าธรรมเนียม:" / next line "0.00 บาท")
            P = lines[i - 1]
            ph = P.box[3] - P.box[1]
            if kw_in(kw_text(P.text), K_EXCLUDE_KW) and not MONEY_RE.search(P.text) and \
                    not kw_in(kw_text(P.text), K_STRONG_KW) and 0 <= L.box[1] - P.box[3] < max(ph, 10) * 2.5:
                excl = True
        cur = kw_in(low, K_CURRENCY_KW)
        datey = kw_in(low, K_DATE_KW) or TIME_RE.search(L.text) or HHMM_RE.search(L.text) or DATE_RE.search(L.text)
        for m in MONEY_RE.finditer(L.text):
            v = parse_value(m)
            if v is None:
                continue
            box, prefix = token_box(L, m.start(), m.end())
            if box is None:
                continue
            s = 0
            ctx = ""
            cy = (box[1] + box[3]) / 2.0
            ch = box[3] - box[1]
            for (li, lb, strong) in labels:
                lh = lb[3] - lb[1]
                lcy = (lb[1] + lb[3]) / 2.0
                if abs(cy - lcy) < max(lh, ch) * 0.7 + 0.06 * max(0, box[0] - lb[2]) and box[0] > lb[2] - 5:
                    if s < (12 if strong else 8):
                        s, ctx = (12 if strong else 8), "right"
                elif li < i and 0 < (box[1] - lb[3]) < max(lh, ch) * 3.2 and box[0] < lb[0] + W * 0.6 and box[2] > lb[0] - W * 0.25:
                    between = any(MONEY_RE.search(lines[j].text) and lines[j].box[3] < box[1] - 2
                                  for j in range(li + 1, i))
                    if not between and s < (10 if strong else 6):
                        s, ctx = (10 if strong else 6), "below"
            baht = bool(BAHT_PREFIX_RE.search(L.text[:m.start()])) or "฿" in prefix or "B" in prefix
            if cur or baht:
                s += 3
            if GROUP_RE.search(m.group(1)):
                s += 1
            if excl:
                s -= 20
            if datey:
                s -= 8
            cands.append({"value": v, "score": s, "line": i, "raw": m.group(0), "box": box, "ctx": ctx, "h": ch,
                          "baht": baht or "฿" in L.text or m.group(0)[0] in "86"})
    return labels, cands


K_AMOUNT_KW, K_STRONG_KW, K_EXCLUDE_KW, K_CURRENCY_KW, K_DATE_KW = (kws(AMOUNT_KW), kws(STRONG_KW), kws(EXCLUDE_KW),
                                                                 kws(CURRENCY_KW), kws(DATE_KW))


# ── pass 2 ─────────────────────────────────────────────────────────────────────────────────

def crop(g, box):
    h, w = g.shape
    l, t, r, b = [int(round(x)) for x in box]
    l, t, r, b = max(0, l), max(0, t), min(w, r), min(h, b)
    if r - l < 4 or b - t < 4:
        return None
    return g[t:b, l:r]


def norm_height(img, text_h):
    f = P2_H / max(8.0, float(text_h))
    f = max(0.5, min(4.0, f))
    return resize(img, int(img.shape[1] * f), int(img.shape[0] * f))


NONDIGIT_WORD_RE = re.compile(r"^[^0-9]*[A-Za-z\u0e01-\u0e4f][^0-9]*$")


def blank_words(g, lines, cands):
    """Paint pass-1 words that contain letters but no digit with the page background, so 'บาท'/'THB'/labels never
    reach the digits-only pass (the whitelist would turn them into digits: 199.00 บาท -> 199.005). Money-token
    boxes are restored afterwards so a generous pad never eats digits of the next line."""
    hist = np.bincount(g.ravel(), minlength=256)
    bg = int(np.argmax(hist))
    out = g.copy()
    for L in lines:
        for (t, l, tp, w, h) in L.words:
            if NONDIGIT_WORD_RE.match(t):
                pd = max(2, int(h * 0.15))
                out[max(0, tp - pd):tp + h + pd, max(0, l - pd):l + w + pd] = bg
    for c in cands:
        l, t, r, b = c["box"]
        out[max(0, t - 1):b + 1, max(0, l - 1):r + 1] = g[max(0, t - 1):b + 1, max(0, l - 1):r + 1]
    return out


def build_rois(g, labels, cands):
    rois = []
    top = []
    for c in sorted([c for c in cands if c["score"] >= 0], key=lambda c: (-c["score"], c["line"])):
        if len(top) < 3 and all(abs(c["value"] - t["value"]) > 0.005 for t in top):
            top.append(c)   # distinct values only (merged passes repeat the same number)
    for c in top:
        th = c["h"]
        b = c["box"]
        im = crop(g, (b[0] - th * 0.6, b[1] - th * 0.35, b[2] + th * 0.3, b[3] + th * 0.35))
        if im is not None:
            rois.append({"kind": "cand", "cand": c, "img": norm_height(im, th)})
    H, W = g.shape
    for (li, lb, strong) in labels[:2]:
        lh = lb[3] - lb[1]
        band = (lb[2] + lh * 0.5, lb[1] - lh * 0.45, W - 2, lb[3] + lh * 0.45)
        if band[2] - band[0] > lh * 2:
            im = crop(g, band)
            if im is not None:
                rois.append({"kind": "right", "label": li, "img": norm_height(im, lh)})
        band = (lb[0] - W * 0.25, lb[3] + lh * 0.1, lb[0] + W * 0.75, lb[3] + lh * 3.4)
        im = crop(g, band)
        if im is not None:
            rois.append({"kind": "below", "label": li, "img": norm_height(im, lh * 1.25)})
    return rois, top


def montage(rois):
    gap = 28
    items = []
    for r in rois:
        gimg = stretch(r["img"])
        b = binarize(r["img"])
        r["baht_removed"] = False
        if r["kind"] == "cand" and r["cand"]["baht"]:
            lab, k = find_baht_glyph(b)
            r["baht_suspect"] = k == -1
            if k > 0:
                er = dilate(lab == k, 2)
                b = np.where(er, 255, b).astype(np.uint8)
                gimg = np.where(er, 255, gimg).astype(np.uint8)
                r["baht_removed"] = True
        items.append((r, "g", gimg))
        items.append((r, "b", b))
    if not items:
        return None, []
    W = max(i[2].shape[1] for i in items) + 2 * gap
    H = sum(i[2].shape[0] + gap for i in items) + gap
    can = np.full((H, W), 255, dtype=np.uint8)
    y = gap
    spans = []
    for r, ver, im in items:
        can[y:y + im.shape[0], gap:gap + im.shape[1]] = im
        spans.append((y, y + im.shape[0], r, ver))
        y += im.shape[0] + gap
    return can, spans


def values_in(text):
    t = normalize(text)
    out = []
    for m in MONEY_RE.finditer(t):
        v = parse_value(m)
        if v is not None:
            out.append((v, m.group(0), bool(re.search(r"[B฿]\s?$", t[:m.start()]))))
    return out


# ── entry points ────────────────────────────────────────────────────────────────────────────

def ocr_state(path):
    t0 = time.time()
    st = {"passes": [], "dark": False}
    g, mn = load_gray(path, True)
    if AUTOCROP:
        cb = card_box(mn)
        if cb:
            g = g[cb[1]:cb[3], cb[0]:cb[2]]
            st["crop"] = cb
    h0, w0 = g.shape
    if g.mean() < 100:
        g = 255 - g
        st["dark"] = True
    f = max(0.5, min(3.0, TARGET_W / float(w0)))
    if abs(f - 1) > 0.05:
        g = resize(g, int(round(w0 * f)), int(round(h0 * f)))
    g = stretch(g)
    code, out, err = run_tess(g, ["-l", "tha+eng", "--psm", "6", "-c", "tessedit_create_tsv=1", "-c", "tessedit_create_txt=0"])
    lines = parse_tsv(out) if code == 0 else []
    st["passes"].append(("p1", "\n".join(L.text for L in lines)))
    labels, cands = find_candidates(lines, g.shape[1])
    if PASS1B and not labels:
        # no amount label read (moire, glare, odd font): one more page pass at a different scale in sparse-text
        # mode; keep it only if it finds a label. Boxes are mapped back to the pass-1 image.
        sc = 0.72
        gb = resize(g, int(round(g.shape[1] * sc)), int(round(g.shape[0] * sc)))
        code, out, err = run_tess(gb, ["-l", "tha+eng", "--psm", "11", "-c", "tessedit_create_tsv=1", "-c", "tessedit_create_txt=0"])
        lines_b = parse_tsv(out, 1.0 / sc) if code == 0 else []
        st["passes"].append(("p1b", "\n".join(L.text for L in lines_b)))
        labels_b, cands_b = find_candidates(lines_b, g.shape[1])
        if labels_b:
            # merge: pass-1b labels give geometry to pass-1 numbers too (both in pass-1 image coordinates)
            lines = sorted(lines + lines_b, key=lambda L: (L.box[1] + L.box[3]) / 2.0)
            labels, cands = find_candidates(lines, g.shape[1])
    rois, top = build_rois(blank_words(g, lines, cands), labels, cands)
    can, spans = montage(rois)
    votes = []
    if can is not None:
        code2, out2, err2 = run_tess(can, ["-l", "eng", "--psm", "6", "-c", "tessedit_char_whitelist=0123456789.,B", "-c", "tessedit_create_tsv=1", "-c", "tessedit_create_txt=0"])
        l2 = parse_tsv(out2) if code2 == 0 else []
        st["passes"].append(("p2", "\n".join(L.text for L in l2)))
        for L in l2:
            cy = (L.box[1] + L.box[3]) / 2.0
            for (y0, y1, r, ver) in spans:
                if y0 - 6 <= cy <= y1 + 6:
                    for v, raw, bp in values_in(L.text):
                        votes.append({"roi": r, "ver": ver, "value": v, "raw": raw, "bprefix": bp})
                    break
    st.update(labels=labels, cands=cands, top=top, votes=votes, rois=rois)
    st["ms"] = int((time.time() - t0) * 1000)
    return st


def strip_lead(v_long, v_short):
    """True if v_long's digits == one extra leading digit + v_short's digits (฿ read as 8/6/3...)."""
    a = ("%.2f" % v_long).replace(".", "")
    b = ("%.2f" % v_short).replace(".", "")
    return len(a) == len(b) + 1 and a[1:] == b and a[0] in "8635B9"


def decide(st, expected=None):
    top, votes = st["top"], st["votes"]
    tally = {}

    def add(v, w, src, ctx):
        e = tally.setdefault(v, {"w": 0, "src": [], "ctx": False, "p1": False, "p2": 0, "baht": False})
        e["w"] += w
        e["src"].append(src)
        e["ctx"] = e["ctx"] or ctx
        return e

    for c in top:
        e = add(c["value"], 2 if c["ctx"] else 1, "p1:%s%d" % (c["ctx"] or "-", c["score"]), bool(c["ctx"]))
        e["p1"] = True
    for v in votes:
        r = v["roi"]
        if r["kind"] == "cand":
            ctx = bool(r["cand"]["ctx"])
            e = add(v["value"], 2 if ctx else 1, "p2c:" + v["ver"] + ("$" if r["baht_removed"] else ""), ctx)
            if r["baht_removed"] or v["bprefix"]:
                e["baht"] = True
            if r["baht_removed"]:
                e["baht_glyph"] = True
            if r.get("baht_suspect") and ("%.2f" % v["value"])[0] in "86":
                e["baht_suspect"] = True
            cv = r["cand"]["value"]
            if strip_lead(cv, v["value"]) and r["cand"]["raw"][:1] in "86":
                e["baht"] = True     # p2 on the same crop says the leading "8" of pass 1 is the ฿ sign
                e["baht_p2only"] = True
            if abs(cv - v["value"]) > 0.005 and not strip_lead(cv, v["value"]) and not strip_lead(v["value"], cv):
                e["contra"] = True   # pass 2 read the same spot differently from pass 1
        else:
            e = add(v["value"], 1, "p2" + r["kind"][0] + ":" + v["ver"], True)
            if v["bprefix"]:
                e["baht"] = True
        e["p2"] += 1
    if not tally:
        return None, 0, "ไม่พบตัวเลขจำนวนเงิน"
    # ฿ read as a leading digit: merge "8"+X into X when there is evidence of the baht sign
    for vl in list(tally):
        for vs in list(tally):
            if vl in tally and vs in tally and vl != vs and strip_lead(vl, vs):
                if tally[vs]["baht"]:
                    es, el = tally[vs], tally.pop(vl)
                    es["w"] += el["w"]
                    es["src"] += ["~" + s for s in el["src"]]
                    es["ctx"] = es["ctx"] or el["ctx"]
                    es["p1"] = es["p1"] or el["p1"]
                    es["p2"] += el["p2"]
                    es["contra"] = es.get("contra") or el.get("contra")
                else:
                    tally[vl]["conflict"] = vs
                    tally[vs]["conflict"] = vl
    ranked = sorted(tally.items(), key=lambda kv: -kv[1]["w"])
    v1, e1 = ranked[0]
    w2 = ranked[1][1]["w"] if len(ranked) > 1 else 0
    margin = e1["w"] - w2
    if e1["ctx"] and e1["p1"] and e1["p2"] >= 2 and margin >= 3:
        conf = 92
    elif e1["ctx"] and e1["p2"] >= 2 and margin >= 2:
        conf = 80
    elif e1["ctx"] and e1["p1"] and e1["p2"] >= 1 and margin >= 2:
        conf = 72
    elif not e1["ctx"] and e1["p1"] and e1["p2"] >= 2 and len(ranked) == 1:
        conf = 62   # no label read, but the only money number on the slip and all passes agree
    else:
        conf = 30
    if "conflict" in e1:
        conf = min(conf, 40)
    if e1.get("baht_p2only") and not e1.get("baht_glyph"):
        conf = min(conf, 80)
    if not e1["p1"] and ("%.2f" % v1)[0] in "86" and not e1.get("baht_glyph"):
        conf = min(conf, 40)   # only pass 2 saw it and it starts with 8/6: could be ฿ read as a digit
    if e1.get("contra") and not e1["p1"] and not e1.get("baht_glyph"):
        conf = min(conf, 40)   # pass 2 contradicts pass 1 on the same crop and pass 1 never saw this value
    if e1.get("baht_suspect") and not e1.get("baht_glyph"):
        conf = min(conf, 40)   # leading blob looks a bit like ฿ but not clearly: 8xxx vs ฿xxx undecidable
    why = "%s ← %s (คู่แข่ง %d)" % ("{:,.2f}".format(v1), " ".join(e1["src"]), w2)
    if expected is not None and abs(v1 - expected) > 0.005 and conf < 80:
        # expected may only choose among numbers really read in an amount context
        for v, e in ranked:
            if abs(v - expected) <= 0.005 and e["ctx"] and e["w"] >= 3:
                return v, 70, "ตรงยอดที่ลง: %s ← %s" % ("{:,.2f}".format(v), " ".join(e["src"]))
            alt = e.get("conflict")
            if alt is not None and abs(alt - expected) <= 0.005 and e["ctx"]:
                return alt, 60, "ซ่อม ฿ ที่ถูกอ่านเป็นตัวเลข (ตรงยอดที่ลง)"
    if conf < MIN_CONF:
        return None, conf, "กำกวม: " + why
    return v1, conf, why


def read(path, expected=None):
    st = ocr_state(path)
    v, c, w = decide(st, expected)
    return v, c, w, st


if __name__ == "__main__":
    v, c, w, st = read(sys.argv[1], float(sys.argv[2]) if len(sys.argv) > 2 else None)
    for k, t in st["passes"]:
        print("==", k)
        print(t)
    print("cands", [(x["value"], x["score"], x["ctx"], x["raw"]) for x in st["cands"]])
    print("votes", [(x["roi"]["kind"], x["ver"], x["value"], x["roi"].get("baht_removed")) for x in st["votes"]])
    print(v, c, w, st["ms"], "ms")
