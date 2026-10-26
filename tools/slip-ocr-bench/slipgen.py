"""Synthetic Thai bank-transfer slip generator (benchmark corpus for the local slip amount reader).

Reproduces the *structure* of common Thai mobile-banking e-slips (not their artwork): header band, "transfer
successful" title, Buddhist-era date, sender/receiver blocks with masked accounts, amount label + value, fee,
reference numbers and a QR code. Every slip comes with ground truth (amount + the amount's bounding box so the
cropping augmentations never cut it away).

Usage:  python3 slipgen.py --out DIR [--fonts DIR] [--seed 7] [--aug 4]
Writes DIR/<id>.png|jpg and DIR/truth.csv (file,amount,bank,variant).
"""
import argparse
import csv
import io
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

try:
    import segno
except ImportError:  # QR is decoration only
    segno = None

FONT_DIR = None
TH_MONTHS = ["ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."]
EN_MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]
SENDERS = ["นาย สมชาย ใจดี", "นางสาว จิรผกา ว.", "น.ส. มณี ดีงาม", "นาย ธนพล ศรีสุข", "นาง วิไล รักไทย", "MR SOMCHAI J",
           "นาย กิตติ พ.", "บจก. สยามทัวร์"]
RECEIVERS = ["หจก. แอม แฮปปี้เนส", "AM HAPPINESS LP", "หจก. แอม แฮปปี้เนส", "นาย วชิร ดิลก", "เทคไทม์ บางพระ"]
BANKS_TH = {"scb": "ธ.ไทยพาณิชย์", "kbank": "ธ.กสิกรไทย", "ktb": "ธ.กรุงไทย", "bbl": "ธ.กรุงเทพ",
            "bay": "ธ.กรุงศรีอยุธยา", "ttb": "ธ.ทหารไทยธนชาต", "gsb": "ธ.ออมสิน"}


FALLBACK_FONTS = ["/usr/share/fonts/truetype/tlwg/Garuda%s.ttf", "C:/Windows/Fonts/LeelawUI%s.ttf",
                  "C:/Windows/Fonts/tahoma%s.ttf"]


def font(family, weight, size):
    """Thai-capable fonts: --fonts DIR with <family>-<weight>.ttf (see README: Sarabun/Kanit/Prompt/Noto Sans Thai/
    IBM Plex Sans Thai), else a system Thai font (TLWG Garuda on Linux, Leelawadee UI/Tahoma on Windows)."""
    p = os.path.join(FONT_DIR or "", "%s-%d.ttf" % (family, weight))
    if FONT_DIR and os.path.exists(p):
        return ImageFont.truetype(p, size)
    bold = weight >= 600
    for pat in FALLBACK_FONTS:
        for cand in ((pat % ("-Bold" if "Garuda" in pat else ("b" if "Leelaw" in pat else "bd"))) if bold else None, pat % ""):
            if cand and os.path.exists(cand):
                return ImageFont.truetype(cand, size)
    raise SystemExit("no Thai font found — pass --fonts DIR (see README)")


def money(v):
    return "{:,.2f}".format(v)


class Canvas:
    """Thin helper that remembers the bounding box of the amount text."""

    def __init__(self, w, h, bg, family):
        self.img = Image.new("RGB", (w, h), bg)
        self.d = ImageDraw.Draw(self.img)
        self.w, self.h = w, h
        self.family = family
        self.amount_box = None

    def text(self, xy, s, size, color, weight=400, anchor="la", amount=False, family=None):
        f = font(family or self.family, weight, size)
        self.d.text(xy, s, font=f, fill=color, anchor=anchor)
        box = self.d.textbbox(xy, s, font=f, anchor=anchor)
        if amount:
            if self.amount_box is None:
                self.amount_box = box
            else:
                a = self.amount_box
                self.amount_box = (min(a[0], box[0]), min(a[1], box[1]), max(a[2], box[2]), max(a[3], box[3]))
        return box

    def rrect(self, box, r, fill, outline=None):
        self.d.rounded_rectangle(box, r, fill=fill, outline=outline)

    def vgrad(self, box, c1, c2):
        x0, y0, x1, y1 = box
        for y in range(y0, y1):
            t = (y - y0) / max(1, (y1 - y0 - 1))
            c = tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(3))
            self.d.line([(x0, y), (x1, y)], fill=c)

    def circle(self, cx, cy, r, fill, ring=None):
        self.d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill, outline=ring, width=3 if ring else 1)

    def pattern(self, box, color, step=60, rnd=None):
        rnd = rnd or random
        x0, y0, x1, y1 = box
        for y in range(y0, y1, step):
            for x in range(x0 + (step // 2 if (y // step) % 2 else 0), x1, step):
                self.d.arc([x, y, x + step * 0.6, y + step * 0.6], 200, 340, fill=color, width=2)

    def qr(self, x, y, size, payload, dark=(0, 0, 0), light=(255, 255, 255)):
        if segno is None:
            self.d.rectangle([x, y, x + size, y + size], fill=dark)
            return
        q = segno.make(payload, error="m")
        buf = io.BytesIO()
        q.save(buf, kind="png", scale=8, border=1, dark=dark, light=light)
        im = Image.open(io.BytesIO(buf.getvalue())).convert("RGB").resize((size, size), Image.NEAREST)
        self.img.paste(im, (x, y))


def rnd_ref(rnd, n=20, alnum=True):
    chars = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz" if alnum else "0123456789"
    return "".join(rnd.choice(chars) for _ in range(n))


def masked(rnd, style):
    d4 = "%04d" % rnd.randint(0, 9999)
    return {"kbank": "xxx-x-x%s-x" % d4, "scb": "xxx-xxx%s-%d" % (d4[1:], rnd.randint(0, 9)),
            "ktb": "XXX-X-XX%s-%d" % (d4[1:], rnd.randint(0, 9)), "short": "x-%s" % d4,
            "bbl": "XXX-X-X%s-X" % d4, "phone": "09x-xxx-%s" % d4}[style]


def th_date(rnd, short_year=True, sep=" ", with_time=True, dash=" - "):
    d, m = rnd.randint(1, 28), rnd.randint(0, 11)
    y = "69" if short_year else "2569"
    s = "%d%s%s%s%s" % (d, sep, TH_MONTHS[m], sep, y)
    if with_time:
        s += "%s%02d:%02d" % (dash, rnd.randint(0, 23), rnd.randint(0, 59))
    return s


def mini_qr_payload(rnd, bank):
    return "004600060000010103%s0225%s5102TH9104%04X" % (bank, rnd_ref(rnd, 25), rnd.randint(0, 65535))


# ── layouts ──────────────────────────────────────────────────────────────────────────────────────
# Each returns (Canvas, bank_code). `amt` float; `v` dict of variant switches.

def lay_scb(rnd, amt, v):
    dark = v.get("dark")
    W, H = 1080, 1500
    c = Canvas(W, H, (34, 30, 48) if dark else (248, 222, 210), v.get("family", "prompt"))
    c.vgrad((0, 0, W, 330), (112, 84, 170), (220, 170, 200))
    c.text((W // 2, 90), "SCB", 90, (255, 255, 255), 700, "mm", family="kanit")
    card_bg = (44, 42, 60) if dark else (255, 255, 255)
    fg = (235, 235, 240) if dark else (40, 40, 40)
    sub = (170, 170, 185) if dark else (110, 110, 110)
    c.rrect((40, 180, W - 40, 1320), 8, card_bg)
    if not dark:
        c.pattern((40, 180, W - 40, 560), (238, 238, 242), 70)
    en = v.get("en")
    c.circle(W // 2 - (260 if en else 190), 260, 26, (30, 180, 90))
    c.text((W // 2 + 20, 260), "Transfer successful" if en else "โอนเงินสำเร็จ", 44, (30, 170, 80), 500, "mm")
    c.text((W // 2, 340), th_date(rnd, short_year=False) if not en else "%d %s 2026 - 10:%02d" % (rnd.randint(1, 28), rnd.choice(EN_MONTHS), rnd.randint(0, 59)), 34, sub, 400, "mm")
    c.text((W // 2, 400), ("Ref ID: " if en else "รหัสอ้างอิง: ") + rnd_ref(rnd, 25), 32, sub, 400, "mm")
    c.d.line([(40, 470), (W - 40, 470)], fill=(225, 225, 230) if not dark else (70, 70, 85), width=2)
    c.text((90, 560), "From" if en else "จาก", 36, fg, 500)
    c.circle(W - 470, 580, 30, (90, 60, 150))
    c.text((W - 90, 580), rnd.choice(SENDERS), 42, fg, 500, "rm")
    c.text((W - 90, 660), masked(rnd, "scb"), 32, sub, 400, "rm")
    c.text((90, 740), "To" if en else "ไปยัง", 36, fg, 500)
    c.circle(W - 520, 760, 30, (30, 140, 60))
    c.text((W - 90, 760), rnd.choice(RECEIVERS), 42, fg, 500, "rm")
    c.text((W - 90, 840), masked(rnd, "short"), 32, sub, 400, "rm")
    c.d.line([(40, 920), (W - 40, 920)], fill=(225, 225, 230) if not dark else (70, 70, 85), width=2)
    c.text((90, 1010), "Amount" if en else "จำนวนเงิน", 36, fg, 500, "lm")
    c.text((W - 90, 1010), money(amt), 44, fg, 500, "rm", amount=True)
    if v.get("fee"):
        c.text((90, 1080), "Fee" if en else "ค่าธรรมเนียม", 32, sub, 400, "lm")
        c.text((W - 90, 1080), money(v["fee"]), 34, sub, 400, "rm")
    c.text((90, 1170), "ผู้รับเงินสามารถสแกนคิวอาร์โค้ดนี้เพื่อ", 34, fg, 400, "lm")
    c.text((90, 1225), "ตรวจสอบสถานะการโอนเงิน", 34, fg, 400, "lm")
    c.qr(W - 330, 1100, 230, mini_qr_payload(rnd, "014"))
    c.vgrad((0, 1330, W, H), (220, 190, 210), (120, 110, 160))
    return c, "014"


def lay_kbank(rnd, amt, v):
    dark = v.get("dark")
    en = v.get("en")
    W, H = 1080, 1520
    bg = (24, 30, 28) if dark else (247, 250, 248)
    c = Canvas(W, H, bg, v.get("family", "kanit"))
    fg = (230, 238, 232) if dark else (30, 30, 30)
    sub = (160, 175, 165) if dark else (100, 105, 100)
    c.pattern((0, 0, W, H), (36, 44, 40) if dark else (236, 243, 238), 90)
    c.text((60, 90), "Transfer Completed" if en else "โอนเงินสำเร็จ", 52, (0, 160, 80) if not dark else (90, 210, 140), 700)
    c.text((60, 175), th_date(rnd, True, " ", True, " ") + " น.", 34, sub, 400)
    c.text((W - 60, 110), "K+", 80, (0, 150, 70), 700, "ra", family="kanit")
    y = 300
    for who, acct in ((rnd.choice(SENDERS), masked(rnd, "kbank")), (rnd.choice(RECEIVERS), masked(rnd, "kbank"))):
        c.circle(130, y + 55, 55, (0, 150, 70) if who in SENDERS else (90, 50, 140))
        c.text((220, y), who, 40, fg, 500)
        c.text((220, y + 55), BANKS_TH[rnd.choice(list(BANKS_TH))], 32, sub, 400)
        c.text((220, y + 100), acct, 32, sub, 400)
        if y == 300:
            c.d.polygon([(110, y + 160), (150, y + 160), (130, y + 195)], fill=(0, 150, 70))
        y += 260
    y = 870
    c.text((60, y), "Transaction ID:" if en else "เลขที่รายการ:", 32, sub, 400)
    c.text((60, y + 48), "0162%s" % rnd_ref(rnd, 16).upper(), 34, fg, 400)
    c.text((60, y + 130), "Amount:" if en else "จำนวน:", 32, sub, 400)
    c.text((60, y + 178), money(amt) + (" THB" if en else " บาท"), 46, fg, 700, amount=True)
    c.text((60, y + 270), "Fee:" if en else "ค่าธรรมเนียม:", 32, sub, 400)
    c.text((60, y + 318), money(v.get("fee", 0.0)) + (" THB" if en else " บาท"), 36, fg, 400)
    c.qr(W - 340, y + 60, 260, mini_qr_payload(rnd, "004"))
    c.text((W - 210, y + 360), "Scan to verify" if en else "สแกนตรวจสอบสลิป", 28, sub, 400, "mm")
    return c, "004"


def lay_ktb(rnd, amt, v):
    dark = v.get("dark")
    W, H = 1080, 1600
    c = Canvas(W, H, (16, 26, 44) if dark else (236, 246, 252), v.get("family", "sarabun"))
    c.vgrad((0, 0, W, 300), (0, 120, 200), (40, 170, 230))
    c.text((W // 2, 110), "Krungthai", 70, (255, 255, 255), 700, "mm", family="prompt")
    card = (28, 40, 62) if dark else (255, 255, 255)
    fg = (230, 236, 245) if dark else (25, 40, 60)
    sub = (150, 165, 190) if dark else (100, 115, 130)
    c.rrect((50, 220, W - 50, 1450), 30, card)
    c.circle(W // 2, 320, 45, (0, 170, 120))
    c.text((W // 2, 420), "โอนเงินสำเร็จ", 50, (0, 140, 110), 700, "mm")
    c.text((W // 2, 490), "รหัสอ้างอิง " + rnd_ref(rnd, 18).upper(), 30, sub, 400, "mm")
    rows = [("จาก", rnd.choice(SENDERS), "กรุงไทย " + masked(rnd, "ktb")),
            ("ไปยัง", rnd.choice(RECEIVERS), BANKS_TH["scb"] + " " + masked(rnd, "ktb"))]
    y = 600
    for lab, name, acct in rows:
        c.text((100, y), lab, 34, sub, 400)
        c.text((W - 100, y), name, 40, fg, 700, "ra")
        c.text((W - 100, y + 55), acct, 30, sub, 400, "ra")
        y += 160
    c.d.line([(100, 930), (W - 100, 930)], fill=(220, 230, 240) if not dark else (60, 75, 100), width=2)
    c.text((100, 1000), "จำนวนเงิน", 36, fg, 400, "lm")
    c.text((W - 100, 1000), money(amt) + " บาท", 46, fg, 700, "rm", amount=True)
    c.text((100, 1080), "ค่าธรรมเนียม", 32, sub, 400, "lm")
    c.text((W - 100, 1080), money(v.get("fee", 0.0)) + " บาท", 34, sub, 400, "rm")
    c.text((100, 1160), "วันที่ทำรายการ", 32, sub, 400, "lm")
    c.text((W - 100, 1160), th_date(rnd, False), 34, sub, 400, "rm")
    c.qr(W // 2 - 110, 1210, 220, mini_qr_payload(rnd, "006"))
    return c, "006"


def lay_bbl(rnd, amt, v):
    en = v.get("en")
    W, H = 1080, 1560
    c = Canvas(W, H, (242, 244, 250), v.get("family", "noto-sans-thai"))
    c.vgrad((0, 0, W, 420), (12, 40, 120), (30, 80, 170))
    c.text((60, 80), "Bangkok Bank", 50, (255, 255, 255), 700)
    c.text((W // 2, 200), "Transfer successful" if en else "โอนเงินสำเร็จ", 48, (255, 255, 255), 700, "mm")
    c.text((W // 2, 270), "%d %s %s, %02d:%02d" % (rnd.randint(1, 28), rnd.choice(EN_MONTHS if en else TH_MONTHS),
                                                  "2026" if en else "69", rnd.randint(0, 23), rnd.randint(0, 59)),
           32, (210, 220, 245), 400, "mm")
    c.rrect((50, 360, W - 50, 1460), 24, (255, 255, 255))
    c.text((W // 2, 440), "Amount (THB)" if en else "จำนวนเงิน (บาท)", 34, (90, 100, 120), 400, "mm")
    c.text((W // 2, 530), money(amt), 76, (12, 40, 120), 700, "mm", amount=True)
    c.text((W // 2, 620), ("Fee " if en else "ค่าธรรมเนียม ") + money(v.get("fee", 0.0)), 30, (110, 115, 130), 400, "mm")
    c.d.line([(100, 680), (W - 100, 680)], fill=(225, 228, 238), width=2)
    y = 730
    for lab, name, acct in (("From" if en else "จาก", rnd.choice(SENDERS), masked(rnd, "bbl")),
                            ("To" if en else "ไปที่", rnd.choice(RECEIVERS), masked(rnd, "bbl"))):
        c.text((100, y), lab, 32, (110, 115, 130), 400)
        c.text((300, y), name, 38, (20, 30, 60), 500)
        c.text((300, y + 52), acct, 30, (110, 115, 130), 400)
        y += 150
    if v.get("balance"):
        c.text((100, y), "Available balance" if en else "ยอดเงินคงเหลือ", 32, (110, 115, 130), 400)
        c.text((W - 100, y), money(rnd.randint(1000, 99999) + rnd.randint(0, 99) / 100.0), 34, (20, 30, 60), 400, "ra")
        y += 80
    c.text((100, y + 20), "Reference no." if en else "หมายเลขอ้างอิง", 30, (110, 115, 130), 400)
    c.text((W - 100, y + 20), rnd_ref(rnd, 18, False), 32, (20, 30, 60), 400, "ra")
    c.qr(W // 2 - 120, 1200, 230, mini_qr_payload(rnd, "002"))
    return c, "002"


def lay_bay(rnd, amt, v):
    W, H = 1080, 1580
    c = Canvas(W, H, (250, 248, 240), v.get("family", "ibm-plex-sans-thai"))
    c.vgrad((0, 0, W, 260), (254, 200, 30), (250, 225, 120))
    c.text((60, 90), "krungsri", 60, (90, 70, 60), 700, family="kanit")
    c.text((W // 2, 330), "โอนเงินสำเร็จ", 50, (90, 70, 60), 700, "mm")
    c.text((W // 2, 420), "จำนวนเงิน", 34, (130, 120, 110), 400, "mm")
    c.text((W // 2, 500), money(amt) + " บาท", 66, (60, 50, 40), 700, "mm", amount=True)
    c.text((W // 2, 580), "ค่าธรรมเนียม " + money(v.get("fee", 0.0)) + " บาท", 30, (130, 120, 110), 400, "mm")
    c.d.line([(80, 640), (W - 80, 640)], fill=(225, 220, 205), width=2)
    y = 690
    for lab, name, acct in (("จาก", rnd.choice(SENDERS), "กรุงศรี " + masked(rnd, "bbl")),
                            ("ไปยัง", rnd.choice(RECEIVERS), BANKS_TH["scb"] + " " + masked(rnd, "bbl"))):
        c.text((80, y), lab, 32, (130, 120, 110), 400)
        c.text((W - 80, y), name, 38, (60, 50, 40), 500, "ra")
        c.text((W - 80, y + 52), acct, 30, (130, 120, 110), 400, "ra")
        y += 150
    c.text((80, y + 20), "วันที่ทำรายการ", 30, (130, 120, 110), 400)
    c.text((W - 80, y + 20), "%02d/%02d/2569 %02d:%02d:%02d" % (rnd.randint(1, 28), rnd.randint(1, 12), rnd.randint(0, 23),
                                                              rnd.randint(0, 59), rnd.randint(0, 59)), 32, (60, 50, 40), 400, "ra")
    c.text((80, y + 90), "เลขที่อ้างอิง", 30, (130, 120, 110), 400)
    c.text((W - 80, y + 90), "BAYM" + rnd_ref(rnd, 14, False), 32, (60, 50, 40), 400, "ra")
    c.qr(W // 2 - 120, 1250, 230, mini_qr_payload(rnd, "025"))
    return c, "025"


def lay_ttb(rnd, amt, v):
    dark = v.get("dark")
    W, H = 1080, 1500
    c = Canvas(W, H, (14, 22, 40) if dark else (245, 247, 252), v.get("family", "prompt"))
    c.vgrad((0, 0, W, 240), (0, 40, 120), (240, 120, 40))
    c.text((60, 80), "ttb", 70, (255, 255, 255), 700, family="kanit")
    fg = (232, 236, 245) if dark else (20, 30, 60)
    sub = (150, 160, 185) if dark else (110, 115, 135)
    c.text((W // 2, 320), "โอนเงินสำเร็จ", 50, (0, 150, 90) if not dark else (90, 210, 150), 700, "mm")
    c.text((W // 2, 390), th_date(rnd, False, " ", True, ", ") + " น.", 32, sub, 400, "mm")
    c.rrect((50, 450, W - 50, 1350), 24, (28, 38, 60) if dark else (255, 255, 255))
    y = 500
    for lab, name, acct in (("จาก", rnd.choice(SENDERS), "ทีทีบี " + masked(rnd, "bbl")),
                            ("ไปยัง", rnd.choice(RECEIVERS), BANKS_TH["kbank"] + " " + masked(rnd, "bbl"))):
        c.text((100, y), lab, 32, sub, 400)
        c.text((W - 100, y), name, 38, fg, 500, "ra")
        c.text((W - 100, y + 52), acct, 30, sub, 400, "ra")
        y += 160
    c.text((100, 880), "จำนวนเงิน", 36, fg, 500, "lm")
    c.text((W - 100, 880), money(amt), 50, fg, 700, "rm", amount=True)
    c.text((W - 100, 935), "บาท", 28, sub, 400, "rm")
    c.text((100, 1000), "ค่าธรรมเนียม", 32, sub, 400, "lm")
    c.text((W - 100, 1000), money(v.get("fee", 0.0)), 34, sub, 400, "rm")
    c.text((100, 1080), "รหัสอ้างอิง", 32, sub, 400, "lm")
    c.text((W - 100, 1080), rnd_ref(rnd, 20).upper(), 30, sub, 400, "rm")
    c.qr(W // 2 - 100, 1130, 200, mini_qr_payload(rnd, "011"))
    return c, "011"


def lay_gsb(rnd, amt, v):
    W, H = 1080, 1480
    c = Canvas(W, H, (255, 246, 250), v.get("family", "sarabun"))
    c.vgrad((0, 0, W, 300), (236, 0, 140), (250, 120, 190))
    c.text((60, 80), "MyMo", 64, (255, 255, 255), 700, family="kanit")
    c.rrect((50, 220, W - 50, 1380), 28, (255, 255, 255))
    c.text((W // 2, 310), "ทำรายการสำเร็จ", 50, (236, 0, 140), 700, "mm")
    c.text((W // 2, 380), "โอนเงิน", 36, (120, 100, 110), 400, "mm")
    rows = [("จาก", rnd.choice(SENDERS)), ("", "ออมสิน " + masked(rnd, "bbl")), ("ไปยัง", rnd.choice(RECEIVERS)),
            ("", BANKS_TH["scb"] + " " + masked(rnd, "bbl"))]
    y = 470
    for lab, val in rows:
        if lab:
            c.text((100, y), lab, 32, (120, 100, 110), 400)
        c.text((W - 100, y), val, 36 if lab else 30, (50, 30, 40), 500 if lab else 400, "ra")
        y += 70
    c.d.line([(100, 770), (W - 100, 770)], fill=(240, 220, 230), width=2)
    c.text((100, 840), "จำนวนเงิน", 36, (50, 30, 40), 500, "lm")
    c.text((W - 100, 840), money(amt) + " บาท", 46, (50, 30, 40), 700, "rm", amount=True)
    c.text((100, 920), "ค่าธรรมเนียม", 32, (120, 100, 110), 400, "lm")
    c.text((W - 100, 920), money(v.get("fee", 0.0)) + " บาท", 34, (120, 100, 110), 400, "rm")
    c.text((100, 1000), "วันที่", 32, (120, 100, 110), 400, "lm")
    c.text((W - 100, 1000), th_date(rnd, False, " ", True, " เวลา ") + " น.", 32, (120, 100, 110), 400, "rm")
    c.text((100, 1080), "รหัสอ้างอิง", 32, (120, 100, 110), 400, "lm")
    c.text((W - 100, 1080), rnd_ref(rnd, 16, False), 32, (120, 100, 110), 400, "rm")
    c.qr(W // 2 - 100, 1140, 200, mini_qr_payload(rnd, "030"))
    return c, "030"


def lay_promptpay(rnd, amt, v):
    W, H = 1080, 1400
    c = Canvas(W, H, (255, 255, 255), v.get("family", "noto-sans-thai"))
    c.rrect((0, 0, W, 220), 0, (16, 60, 110))
    c.text((60, 80), "PromptPay", 56, (255, 255, 255), 700, family="prompt")
    c.text((W // 2, 300), "ชำระเงินสำเร็จ", 50, (20, 140, 80), 700, "mm")
    c.text((W // 2, 370), th_date(rnd, True), 32, (110, 110, 110), 400, "mm")
    c.text((100, 470), "ยอดชำระ", 36, (60, 60, 60), 400, "lm")
    c.text((W - 100, 470), "฿" + money(amt), 56, (20, 20, 20), 700, "rm", amount=True)
    c.text((100, 570), "ไปยัง", 32, (110, 110, 110), 400, "lm")
    c.text((W - 100, 570), rnd.choice(RECEIVERS), 36, (20, 20, 20), 500, "rm")
    c.text((W - 100, 625), "เลขประจำตัวผู้เสียภาษี xxx-xxx-%04d" % rnd.randint(0, 9999), 28, (110, 110, 110), 400, "rm")
    c.text((100, 720), "จาก", 32, (110, 110, 110), 400, "lm")
    c.text((W - 100, 720), rnd.choice(SENDERS), 36, (20, 20, 20), 500, "rm")
    c.text((100, 820), "เลขที่อ้างอิง", 32, (110, 110, 110), 400, "lm")
    c.text((W - 100, 820), rnd_ref(rnd, 14, False), 32, (20, 20, 20), 400, "rm")
    c.qr(W // 2 - 120, 950, 240, mini_qr_payload(rnd, "004"))
    return c, "004"


def lay_truemoney(rnd, amt, v):
    W, H = 1080, 1500
    c = Canvas(W, H, (255, 250, 245), v.get("family", "kanit"))
    c.vgrad((0, 0, W, 360), (255, 120, 20), (255, 170, 80))
    c.text((60, 80), "TrueMoney", 56, (255, 255, 255), 700)
    c.text((W // 2, 240), "โอนเงินสำเร็จ", 48, (255, 255, 255), 700, "mm")
    c.rrect((50, 320, W - 50, 1380), 28, (255, 255, 255))
    c.text((W // 2, 410), "จำนวนเงิน", 34, (130, 110, 100), 400, "mm")
    c.text((W // 2, 500), "฿" + money(amt), 74, (40, 30, 20), 700, "mm", amount=True)
    c.d.line([(100, 590), (W - 100, 590)], fill=(240, 230, 220), width=2)
    y = 640
    for lab, val in (("ไปยัง", rnd.choice(RECEIVERS)), ("", masked(rnd, "phone")), ("จาก", rnd.choice(SENDERS)),
                     ("", masked(rnd, "phone")),
                     ("วันที่ทำรายการ", "%02d/%02d/26 %02d:%02d" % (rnd.randint(1, 28), rnd.randint(1, 12), rnd.randint(0, 23), rnd.randint(0, 59))),
                     ("หมายเลขอ้างอิง", rnd_ref(rnd, 14, False))):
        if lab:
            c.text((100, y), lab, 30, (130, 110, 100), 400)
        c.text((W - 100, y), val, 34 if lab else 28, (40, 30, 20), 500 if lab else 400, "ra")
        y += 80
    c.qr(W // 2 - 100, 1140, 200, "https://tmn.app.link/" + rnd_ref(rnd, 10))
    return c, "TMN"


LAYOUTS = {"scb": lay_scb, "kbank": lay_kbank, "ktb": lay_ktb, "bbl": lay_bbl, "bay": lay_bay, "ttb": lay_ttb,
           "gsb": lay_gsb, "promptpay": lay_promptpay, "truemoney": lay_truemoney}
VARIANTS = {"scb": [{}, {"dark": 1}, {"en": 1, "fee": 0.0}], "kbank": [{}, {"en": 1}, {"dark": 1, "fee": 10.0}],
            "ktb": [{}, {"dark": 1, "fee": 15.0}], "bbl": [{}, {"en": 1, "balance": 1}],
            "bay": [{}, {"fee": 25.0, "family": "sarabun"}], "ttb": [{}, {"dark": 1}], "gsb": [{}, {"family": "noto-sans-thai"}],
            "promptpay": [{}, {"family": "kanit"}], "truemoney": [{}, {"family": "prompt"}]}
AMOUNTS = [1000.00, 12345.50, 350.00, 7.00, 105000.00, 2500.00, 980.00, 35000.00, 1250.75, 60.00, 4200.00, 89.50,
           15000.00, 199.00, 520.25, 3333.33, 18900.00, 45.00, 7650.00, 100.00]


# ── augmentations ──────────────────────────────────────────────────────────────────────────────

def jpeg(img, q):
    buf = io.BytesIO()
    img.save(buf, "JPEG", quality=q)
    return Image.open(io.BytesIO(buf.getvalue())).convert("RGB")


def resize_w(img, w):
    h = int(img.height * w / img.width)
    return img.resize((w, h), Image.LANCZOS)


def aug_phone(img, rnd, box):
    return resize_w(img, rnd.choice([720, 828, 1170, 1440])), "phone"


def aug_line(img, rnd, box):
    im = resize_w(img, rnd.choice([720, 960]))
    return jpeg(jpeg(im, rnd.randint(60, 75)), rnd.randint(55, 70)), "line"


def aug_jpeg_low(img, rnd, box):
    return jpeg(resize_w(img, 900), rnd.randint(40, 55)), "jpeg_low"


def aug_blur(img, rnd, box):
    return jpeg(img.filter(ImageFilter.GaussianBlur(rnd.uniform(0.8, 1.5))), 80), "blur"


def aug_rotate(img, rnd, box):
    a = rnd.uniform(-3, 3)
    return jpeg(img.rotate(a, resample=Image.BICUBIC, expand=True, fillcolor=(235, 235, 235)), 85), "rot%+.1f" % a


def aug_half(img, rnd, box):
    return jpeg(img.resize((img.width // 2, img.height // 2), Image.LANCZOS), 80), "half"


def aug_photo(img, rnd, box):
    """photo-of-screen: slight perspective, uneven brightness, colour cast, sensor noise, blur."""
    import numpy as np
    w, h = img.size
    d = int(w * 0.03)
    src = [(rnd.randint(0, d), rnd.randint(0, d)), (w - rnd.randint(0, d), rnd.randint(0, d)),
           (w - rnd.randint(0, d), h - rnd.randint(0, d)), (rnd.randint(0, d), h - rnd.randint(0, d))]
    dst = [(0, 0), (w, 0), (w, h), (0, h)]
    coeffs = _persp_coeffs(dst, src)
    im = img.transform((w, h), Image.PERSPECTIVE, coeffs, Image.BICUBIC, fillcolor=(60, 60, 60))
    a = np.asarray(im).astype(np.float32)
    yy, xx = np.mgrid[0:h, 0:w]
    grad = 0.8 + 0.3 * (xx / w) * rnd.uniform(0.5, 1.0) + 0.1 * (yy / h)
    cast = np.array([rnd.uniform(0.9, 1.05), rnd.uniform(0.92, 1.05), rnd.uniform(0.85, 1.1)])
    a = a * grad[..., None] * cast + np.random.RandomState(rnd.randint(0, 99999)).normal(0, 8, a.shape)
    im = Image.fromarray(np.clip(a, 0, 255).astype("uint8")).filter(ImageFilter.GaussianBlur(0.7))
    return jpeg(resize_w(im, 1000), 75), "photo"


def _persp_coeffs(pa, pb):
    import numpy as np
    m = []
    for p1, p2 in zip(pa, pb):
        m.append([p1[0], p1[1], 1, 0, 0, 0, -p2[0] * p1[0], -p2[0] * p1[1]])
        m.append([0, 0, 0, p1[0], p1[1], 1, -p2[1] * p1[0], -p2[1] * p1[1]])
    A = np.array(m, dtype=np.float64)
    B = np.array(pb, dtype=np.float64).reshape(8)
    return np.linalg.solve(A, B).tolist()


def aug_crop(img, rnd, box):
    w, h = img.size
    top = rnd.randint(0, max(0, int(box[1]) - 120)) if rnd.random() < 0.6 else 0
    bot = rnd.randint(min(h, int(box[3]) + 120), h)
    return jpeg(img.crop((0, top, w, max(bot, top + 200))), 85), "crop"


def aug_embed(img, rnd, box):
    """slip shown inside a web/chat UI screenshot (like the real sample) — adds unrelated numbers around it."""
    w, h = img.size
    W = int(w * 1.45)
    can = Image.new("RGB", (W, h + 120), (250, 250, 252))
    d = ImageDraw.Draw(can)
    can.paste(img, (40, 60))
    f = font("sarabun", 400, max(18, w // 40))
    x = w + 70
    for i, s in enumerate(["%d_%010d_2026" % (rnd.randint(100000, 999999), rnd.randint(0, 10**10 - 1)),
                           "%.1f KB" % rnd.uniform(60, 300), "ดิลก สัมพันธ์", "19 ส.ค. 2569", "%d รายการ" % rnd.randint(2, 30)]):
        d.text((x, 200 + i * 60), s, font=f, fill=(90, 90, 110))
    d.rectangle([0, 0, W, 40], fill=(230, 230, 236))
    return jpeg(resize_w(can, rnd.choice([820, 1100])), 85), "embed"


def aug_combo(img, rnd, box):
    """LINE-forwarded photo: photo-of-screen + half size + heavy JPEG."""
    im, _ = aug_photo(img, rnd, box)
    im = im.resize((im.width * 2 // 3, im.height * 2 // 3), Image.LANCZOS)
    return jpeg(im, rnd.randint(40, 60)), "combo"


def aug_thumb(img, rnd, box):
    return jpeg(resize_w(img, rnd.choice([540, 600])), rnd.randint(55, 70)), "thumb"


HARD_AUGS = [aug_combo, aug_thumb]
AUGS = [aug_phone, aug_line, aug_jpeg_low, aug_blur, aug_rotate, aug_half, aug_photo, aug_crop, aug_embed]


def main():
    global FONT_DIR
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--fonts", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "fonts"))
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--aug", type=int, default=4, help="augmented copies per clean slip")
    ap.add_argument("--amount-offset", type=int, default=0)
    ap.add_argument("--amounts", default="", help="comma list overriding the amount pool")
    ap.add_argument("--hard", action="store_true", help="add combo/thumbnail degradations to the pool")
    ap.add_argument("--real", nargs="*", default=[], help="real slip images as path=amount (augmented too)")
    a = ap.parse_args()
    FONT_DIR = a.fonts
    os.makedirs(a.out, exist_ok=True)
    global AMOUNTS
    if a.amounts:
        AMOUNTS = [float(x) for x in a.amounts.split(",")]
    rnd = random.Random(a.seed)
    pool = AUGS + (HARD_AUGS if a.hard else [])
    rows = []
    ai = 0
    for bank, fn in LAYOUTS.items():
        for vi, var in enumerate(VARIANTS[bank]):
            for k in range(2):
                amt = AMOUNTS[(ai + a.amount_offset) % len(AMOUNTS)]
                ai += 1
                v = dict(var)
                c, _ = fn(rnd, amt, v)
                base = "%s_v%d_%d" % (bank, vi, k)
                c.img.save(os.path.join(a.out, base + ".png"))
                tag = ",".join("%s=%s" % kv for kv in sorted(var.items())) or "default"
                rows.append((base + ".png", "%.2f" % amt, bank, tag + ";clean"))
                augs = rnd.sample(pool, a.aug)
                for f in augs:
                    im, name = f(c.img, rnd, c.amount_box)
                    fn_out = "%s_%s.jpg" % (base, name.split("%")[0].replace("+", "p").replace("-", "m").replace(".", "_"))
                    im.save(os.path.join(a.out, fn_out), quality=95)
                    rows.append((fn_out, "%.2f" % amt, bank, tag + ";" + name))
    for spec in a.real:
        parts = spec.split("=")
        path, amt = parts[0], parts[1]
        rbank = "real_" + parts[2] if len(parts) > 2 else "real"
        img = Image.open(path).convert("RGB")
        base = "real_" + os.path.splitext(os.path.basename(path))[0]
        img.save(os.path.join(a.out, base + ".png"))
        rows.append((base + ".png", "%.2f" % float(amt), rbank, "clean"))
        box = (0, img.height * 0.55, img.width, img.height * 0.65)
        for f in (aug_line, aug_jpeg_low, aug_blur, aug_rotate, aug_half, aug_photo):
            im, name = f(img, rnd, box)
            fn_out = "%s_%s.jpg" % (base, name.split("%")[0].replace("+", "p").replace("-", "m").replace(".", "_"))
            im.save(os.path.join(a.out, fn_out), quality=95)
            rows.append((fn_out, "%.2f" % float(amt), rbank, name))
    with open(os.path.join(a.out, "truth.csv"), "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["file", "amount", "bank", "variant"])
        w.writerows(rows)
    print("wrote %d images to %s" % (len(rows), a.out))


if __name__ == "__main__":
    main()
