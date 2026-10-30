using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Integration.OcrReview
{
    /// <summary>ผลกระทบยอดบนกระดาษ (ก่อน VAT / VAT / รวม) หลังตรวจความสอดคล้อง</summary>
    public sealed class OcrPaperTotalsResult
    {
        public decimal? SubTotal { get; set; }
        public decimal? Vat { get; set; }
        public decimal? Total { get; set; }
        /// <summary>แก้จากค่าที่ OCR (NextAcc) ส่งมาอย่างน้อย 1 ช่อง</summary>
        public bool Changed { get; set; }
        /// <summary>HIGH = มีหลักฐานยืนยันจากหลายแหล่ง · MEDIUM = สอดคล้องแต่หลักฐานน้อย · LOW = หาชุดที่สอดคล้องไม่ได้ (คงค่า OCR ให้คนตรวจ)</summary>
        public string Confidence { get; set; } = "MEDIUM";
        /// <summary>คำอธิบายภาษาไทย (ที่มาของยอด / เหตุที่แก้) — หน้าเว็บแสดงเหนือตารางเทียบยอด</summary>
        public List<string> Notes { get; } = new List<string>();
    }

    /// <summary>VAT รายบรรทัดที่ OCR อ่านได้จริง (OcrLineItemDto.VatRate / VatAmount) — ใช้ตัดสินชนิด VAT ของบรรทัด</summary>
    public sealed class OcrLineVatHint
    {
        /// <summary>OCR ส่งอัตรา VAT ของบรรทัดมาเอง (7/0/-1) — ไม่ทับ</summary>
        public bool ExplicitRate { get; set; }
        /// <summary>ยอด VAT ของบรรทัดตามกระดาษ (0 = บรรทัดไม่มี VAT)</summary>
        public decimal? VatAmount { get; set; }
    }

    /// <summary>
    /// ตัวกระทบยอดบนกระดาษ — pure (mono test). OCR ของ NextAcc คืน ExtractedSubTotal/VatAmount/TotalAmount โดยจับป้ายคำ
    /// (OcrService.cs:6385-6404 "ราคาสินค้า|ก่อนภาษี|รวมเงิน|…" / AmountTripleExtractor) ⇒ ใบลดหนี้ Makro ที่พิมพ์กล่อง §86/10
    /// "มูลค่าตามใบกำกับภาษีเดิม 19,604.00" ถูกอ่านเป็นยอดก่อน VAT ทั้งที่ยอดใบนี้คือ 3,466.58 + 72.42 = 3,539.00.
    /// เดิม <c>BuildInitialState</c> รับ 3 ช่องมาใช้ตรง ๆ ⇒ หน้าตรวจทานฟ้อง "ต้องแก้ก่อนส่ง (3)" ทุกใบแบบนี้.
    /// <para>วิธีคิด: รวมผู้สมัครของแต่ละช่องจากทุกแหล่ง (ค่า OCR · ตัวเลขหลังป้ายในข้อความดิบ · แถว "รวม x y z" ในตาราง ·
    /// Σบรรทัด/ΣVAT บรรทัด · กล่อง §86/10 · ค่าอนุมาน เช่น รวม − VAT) → ลองทุกชุด (ก่อน VAT, VAT, รวม) ที่ <b>บวกกันลงตัว</b> → ให้คะแนน
    /// ตามหลักฐานที่ยืนยัน (ค่าที่อ่านจากกระดาษ &gt; ค่าอนุมาน; ยอดรวม OCR น่าเชื่อสุด; ช่องที่เท่ากับ "มูลค่าเดิม/ที่ถูกต้อง" ของ §86/10
    /// ถูกหักคะแนนเพราะเป็นยอดของใบเดิม ไม่ใช่ใบนี้; VAT &gt; 7.5% ของฐาน = เป็นไปไม่ได้) → เลือกชุดคะแนนสูงสุด.
    /// ชุด OCR เดิมที่ลงตัวอยู่แล้วและไม่ชนหลักฐานอื่น = คงเดิม (ไม่แตะใบที่ถูกอยู่แล้ว).</para>
    /// </summary>
    public static class OcrPaperTotals
    {
        private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private const string MoneyPat = @"(\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?|\d+\.\d{2})";
        /// <summary>บวกกันลงตัว: ยอมเศษปัด 2 สตางค์ (กระดาษปัดต่อบรรทัด)</summary>
        private const decimal SumTol = 0.02m;
        private const decimal EqTol = 0.01m;

        private static readonly string[] SubLabels =
        {
            "ยอดก่อนภาษี", "มูลค่าก่อนภาษี", "ราคาก่อนภาษี", "ก่อนภาษี", "มูลค่าสินค้า", "ราคาสินค้า", "รวมมูลค่าสินค้า", "รวมเป็นเงิน",
            "SUB TOTAL", "SUBTOTAL", "Sub-Total", "Amount before VAT", "Net Amount"
        };
        private static readonly string[] VatLabels = { "ภาษีมูลค่าเพิ่ม 7%", "ภาษีมูลค่าเพิ่ม", "ภาษี 7%", "VAT 7%", "VAT" };
        private static readonly string[] TotalLabels =
        {
            "จำนวนเงินรวมสุทธิ", "จำนวนเงินรวมทั้งสิ้น", "รวมเงินทั้งสิ้น", "รวมทั้งสิ้น", "ยอดรวมสุทธิ", "ยอดเงินรวม", "ยอดรวม", "จำนวนเงินรวม",
            "มูลค่าลดหนี้", "มูลค่าเพิ่มหนี้", "CREDIT AMOUNT", "DEBIT AMOUNT", "GRAND TOTAL", "NET TOTAL", "Total Amount", "AMOUNT", "TOTAL"
        };

        private sealed class Cand
        {
            public decimal Value;
            /// <summary>OCR / LABEL / ROW / LINES / DIFF / DERIVED</summary>
            public string Source;
            public string Why;
        }

        /// <summary>
        /// กระทบยอดกระดาษ. <paramref name="linesSum"/> = Σยอดบรรทัดตามกระดาษ (null = OCR ไม่มีรายการ), <paramref name="linesVat"/> = ΣVAT ต่อบรรทัดที่ OCR อ่านได้
        /// (null = ไม่มี), <paramref name="origAmount"/>/<paramref name="correctAmount"/>/<paramref name="diffAmount"/> = กล่อง §86/10 ของใบลด/เพิ่มหนี้.
        /// </summary>
        public static OcrPaperTotalsResult Reconcile(decimal? ocrSub, decimal? ocrVat, decimal? ocrTotal,
            decimal? linesSum, decimal? linesVat, decimal? origAmount, decimal? correctAmount, decimal? diffAmount, string rawText, bool isNote)
        {
            var res = new OcrPaperTotalsResult { SubTotal = ocrSub, Vat = ocrVat, Total = ocrTotal };
            bool ocrConsistent = ocrSub.HasValue && ocrTotal.HasValue && Math.Abs(ocrSub.Value + (ocrVat ?? 0m) - ocrTotal.Value) <= SumTol;
            if (!ocrSub.HasValue && !ocrVat.HasValue && !ocrTotal.HasValue && !linesSum.HasValue) { res.Confidence = "LOW"; return res; }

            // ── ผู้สมัครแต่ละช่อง ──
            var subs = new List<Cand>(); var vats = new List<Cand>(); var totals = new List<Cand>();
            Add(subs, ocrSub, "OCR", "OCR อ่าน");
            Add(vats, ocrVat, "OCR", "OCR อ่าน");
            Add(totals, ocrTotal, "OCR", "OCR อ่าน");
            foreach (var v in ReadAllLabeled(rawText, SubLabels)) Add(subs, v.Value, "LABEL", "หลังป้าย \"" + v.Key + "\"");
            foreach (var v in ReadAllLabeled(rawText, VatLabels)) Add(vats, v.Value, "LABEL", "หลังป้าย \"" + v.Key + "\"");
            foreach (var v in ReadAllLabeled(rawText, TotalLabels)) Add(totals, v.Value, "LABEL", "หลังป้าย \"" + v.Key + "\"");
            var rows = ReadSumRows(rawText);
            foreach (var r in rows)
            {
                Add(subs, r[0], "ROW", "แถว \"รวม\" ในตาราง"); Add(vats, r[1], "ROW", "แถว \"รวม\" ในตาราง"); Add(totals, r[2], "ROW", "แถว \"รวม\" ในตาราง");
            }
            if (linesSum.HasValue && linesSum.Value > 0m)
            {
                Add(subs, linesSum, "LINES", "Σรายการ");
                Add(totals, linesSum, "LINES", "Σรายการ (ราคารวม VAT)");
                if (linesVat.HasValue && linesVat.Value > 0m)
                {
                    Add(vats, linesVat, "LINES", "ΣVAT รายบรรทัด");
                    Add(totals, OcrReviewMath.R2(linesSum.Value + linesVat.Value), "LINES", "Σรายการ + ΣVAT");
                    Add(subs, OcrReviewMath.R2(linesSum.Value - linesVat.Value), "LINES", "Σรายการ − ΣVAT");
                }
            }
            if (isNote && diffAmount.HasValue && diffAmount.Value > 0m) Add(totals, diffAmount, "DIFF", "ผลต่าง (ลด/เพิ่มหนี้) ตาม §86/10");
            if (isNote && origAmount.HasValue && correctAmount.HasValue && origAmount.Value != correctAmount.Value)
                Add(totals, Math.Abs(OcrReviewMath.R2(origAmount.Value - correctAmount.Value)), "DIFF", "มูลค่าเดิม − มูลค่าที่ถูกต้อง (§86/10)");
            // ค่าอนุมานจากคู่ที่อ่านได้ (คะแนนต่ำกว่าค่าที่อ่านจากกระดาษ)
            foreach (var t in totals.ToList()) foreach (var v in vats.ToList()) if (t.Value - v.Value > 0m) Add(subs, OcrReviewMath.R2(t.Value - v.Value), "DERIVED", "ยอดรวม − VAT");
            foreach (var t in totals.ToList()) foreach (var s in subs.Where(x => x.Source != "DERIVED").ToList()) if (t.Value - s.Value >= 0m) Add(vats, OcrReviewMath.R2(t.Value - s.Value), "DERIVED", "ยอดรวม − ยอดก่อน VAT");
            foreach (var s in subs.Where(x => x.Source != "DERIVED").ToList()) foreach (var v in vats.Where(x => x.Source != "DERIVED").ToList()) Add(totals, OcrReviewMath.R2(s.Value + v.Value), "DERIVED", "ยอดก่อน VAT + VAT");
            if (!vats.Any() || vats.All(x => x.Source == "DERIVED")) foreach (var s in subs.Where(x => x.Source != "DERIVED").ToList()) Add(vats, 0m, "DERIVED", "ไม่พบ VAT บนกระดาษ");

            // ── ลองทุกชุดที่บวกกันลงตัว ──
            Cand bs = null, bv = null, bt = null; int best = int.MinValue;
            foreach (var s in subs) foreach (var v in vats) foreach (var t in totals)
            {
                if (s.Value <= 0m || v.Value < 0m || t.Value <= 0m) continue;
                if (Math.Abs(s.Value + v.Value - t.Value) > SumTol) continue;
                int sc = Score(s, v, t, ocrSub, ocrVat, ocrTotal, linesSum, linesVat, rows, origAmount, correctAmount, diffAmount, isNote);
                if (sc > best) { best = sc; bs = s; bv = v; bt = t; }
            }

            if (bs == null)
            {
                res.Confidence = "LOW";
                if (ocrSub.HasValue || ocrTotal.HasValue)
                    res.Notes.Add("ยอดที่ OCR อ่าน (ก่อน VAT " + M(ocrSub) + " · VAT " + M(ocrVat) + " · รวม " + M(ocrTotal) + ") บวกกันไม่ลงตัว และหาชุดที่สอดคล้องจากกระดาษไม่ได้ — ตรวจกับภาพแล้วกรอกช่อง \"ตามเอกสาร\" เอง");
                return res;
            }

            bool changedSub = !Eq(bs.Value, ocrSub), changedVat = !Eq(bv.Value, ocrVat), changedTot = !Eq(bt.Value, ocrTotal);
            if (ocrConsistent && !changedSub && !changedVat && !changedTot) { res.Confidence = Strength(best); return res; }
            // ชุด OCR เดิมลงตัวแต่ชุดใหม่ชนะเพราะหลักฐานมากกว่า — แก้เฉพาะเมื่อชนะชัด (ไม่สลับใบที่ถูกอยู่แล้วด้วยคะแนนเฉียด)
            if (ocrConsistent)
            {
                int ocrScore = Score(new Cand { Value = ocrSub.Value, Source = "OCR" }, new Cand { Value = ocrVat ?? 0m, Source = "OCR" }, new Cand { Value = ocrTotal.Value, Source = "OCR" },
                    ocrSub, ocrVat, ocrTotal, linesSum, linesVat, rows, origAmount, correctAmount, diffAmount, isNote);
                if (best - ocrScore < 4) { res.Confidence = Strength(ocrScore); return res; }
            }

            res.SubTotal = bs.Value; res.Vat = bv.Value; res.Total = bt.Value; res.Changed = changedSub || changedVat || changedTot;
            res.Confidence = Strength(best);
            if (changedSub) res.Notes.Add(Explain("ยอดก่อน VAT", ocrSub, bs, origAmount, correctAmount, isNote));
            if (changedVat) res.Notes.Add(Explain("ภาษีมูลค่าเพิ่ม", ocrVat, bv, null, null, isNote));
            if (changedTot) res.Notes.Add(Explain("ยอดรวม", ocrTotal, bt, origAmount, correctAmount, isNote));
            res.Notes.Add("ตรวจแล้ว: " + OcrReviewMath.Money(bs.Value) + " + " + OcrReviewMath.Money(bv.Value) + " = " + OcrReviewMath.Money(bt.Value)
                + (linesSum.HasValue && (Eq(linesSum.Value, bs.Value) || Eq(linesSum.Value, bt.Value)) ? " (= Σรายการที่ OCR อ่าน)" : "")
                + (isNote && diffAmount.HasValue && Eq(diffAmount.Value, bt.Value) ? " · เท่ากับผลต่าง §86/10 บนใบ" : ""));
            return res;
        }

        // ──────────────────────────────────────────────────────────────
        // VAT รายบรรทัดตามกระดาษ
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// ตั้ง <c>VatType</c> ของบรรทัดที่ OCR ไม่ได้ส่งอัตรามา ให้ตรงกระดาษ (Makro: คอลัมน์ "รหัส ภ.พ." 1 = ไม่มี VAT, 2 = 7%). ลำดับหลักฐาน:
        /// (ก) ยอด VAT ของบรรทัดที่ OCR อ่าน (0 ⇒ "0", &gt;0 ⇒ "7") · (ข) แถว "ฐาน VAT รวม" ในข้อความดิบที่ฐาน (หรือยอดรวม) ตรงยอดบรรทัด ·
        /// (ค) เมื่อ VAT รวมที่คิดจากบรรทัดยังไม่ตรง VAT บนกระดาษ: หา <b>ชุดเดียว</b> ของบรรทัดที่ 7% บวกกันได้เท่า VAT กระดาษ (±0.05) ⇒ บรรทัดนอกชุด = "0".
        /// คืนคำอธิบายของบรรทัดที่เปลี่ยน (ว่าง = ไม่แตะ). บรรทัดที่ OCR ส่งอัตรามาเอง / ผู้ใช้ตั้ง override ไม่ถูกทับ.
        /// </summary>
        public static List<string> InferLineVatTypes(List<OcrReviewLine> lines, List<OcrLineVatHint> hints, string rawText, decimal? printedVat, bool pricesIncludeVat)
        {
            var notes = new List<string>();
            if (lines == null || lines.Count == 0 || hints == null || hints.Count != lines.Count) return notes;
            var amount = lines.Select(l => OcrReviewMath.R2(l.Qty * l.UnitPrice - l.Discount)).ToList();
            var open = new bool[lines.Count];   // ยังตัดสินไม่ได้
            for (int i = 0; i < lines.Count; i++)
            {
                var h = hints[i]; var l = lines[i];
                if (h.ExplicitRate || l.VatOverride.HasValue || l.VatType == "EX") continue;
                if (h.VatAmount.HasValue)
                {
                    string want = h.VatAmount.Value <= 0.005m ? "0" : "7";
                    if (l.VatType != want) { Set(l, want); notes.Add(LineNote(i, amount[i], want, "OCR อ่าน VAT ของบรรทัด = " + OcrReviewMath.Money(h.VatAmount.Value))); }
                    continue;
                }
                open[i] = true;
            }
            if (!open.Any(x => x)) return notes;

            // (ข) แถวในตาราง "ฐาน VAT รวม" (a + b = c) ที่ฐาน/ยอดรวมตรงยอดบรรทัด
            var rows = ReadVatRows(rawText);
            if (rows.Count > 0)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (!open[i]) continue;
                    var hit = rows.FirstOrDefault(rw => Eq(rw[0], amount[i]) || Eq(rw[2], amount[i]));
                    if (hit == null) continue;
                    // แถวเดียวกันห้ามเป็นทั้ง "ฐาน" และ "รวม" ของบรรทัดต่างกัน — ถ้าทั้งสองบรรทัดจับแถวเดียวกัน ให้ข้าม (กำกวม)
                    string want = hit[1] <= 0.005m ? "0" : "7";
                    if (lines[i].VatType != want) { Set(lines[i], want); notes.Add(LineNote(i, amount[i], want, "บนกระดาษบรรทัดนี้ VAT " + OcrReviewMath.Money(hit[1]))); }
                    open[i] = false;
                }
            }
            if (!open.Any(x => x) || !printedVat.HasValue) return notes;

            // (ค) ชุดบรรทัดที่ 7% บวกกันเท่า VAT กระดาษ — เฉพาะเมื่อตอนนี้ยังไม่ตรง และหาได้ชุดเดียว
            Func<int, decimal> vatOf = i => pricesIncludeVat
                ? OcrReviewMath.R2(amount[i] - OcrReviewMath.R2(amount[i] * 100m / 107m))
                : OcrReviewMath.R2(amount[i] * 0.07m);
            decimal fixedVat = 0m;   // VAT ของบรรทัดที่ตัดสินแล้ว (7%)
            var idx = new List<int>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (open[i]) idx.Add(i);
                else if (lines[i].VatType == "7") fixedVat += vatOf(i);
            }
            decimal target = printedVat.Value - fixedVat;
            decimal nowVat = fixedVat + idx.Where(i => lines[i].VatType == "7").Sum(i => vatOf(i));
            if (Math.Abs(nowVat - printedVat.Value) <= 0.05m || idx.Count > 16 || target < -0.05m) return notes;
            int found = -1, hits = 0;
            for (int mask = 0; mask < (1 << idx.Count); mask++)
            {
                decimal sum = 0m;
                for (int k = 0; k < idx.Count; k++) if ((mask & (1 << k)) != 0) sum += vatOf(idx[k]);
                if (Math.Abs(sum - target) <= 0.05m) { hits++; found = mask; if (hits > 1) break; }
            }
            if (hits != 1) return notes;
            for (int k = 0; k < idx.Count; k++)
            {
                int i = idx[k]; string want = (found & (1 << k)) != 0 ? "7" : "0";
                if (lines[i].VatType != want) { Set(lines[i], want); notes.Add(LineNote(i, amount[i], want, "VAT บนกระดาษ " + OcrReviewMath.Money(printedVat.Value) + " = 7% ของบรรทัดที่มี VAT เท่านั้น")); }
            }
            return notes;
        }

        private static void Set(OcrReviewLine l, string vatType)
        {
            l.VatType = vatType;
            l.VatOverride = null;
            if (vatType != "7") l.VatClaimable = true;
        }

        private static string LineNote(int i, decimal amount, string want, string why) =>
            "รายการ " + (i + 1) + " (" + OcrReviewMath.Money(amount) + "): " + (want == "0" ? "ไม่มี VAT" : "VAT 7%") + " — " + why;

        /// <summary>แถว "ฐาน VAT รวม" (ตัวเลขเงิน 3 ตัวติดกันบนบรรทัดเดียว a + b = c) — ใช้จับ VAT รายบรรทัดจากตารางสรุปตามรหัส ภ.พ.</summary>
        public static List<decimal[]> ReadVatRows(string raw)
        {
            var list = new List<decimal[]>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (Match m in Regex.Matches(raw, MoneyPat + @"[^\S\n]+" + MoneyPat + @"[^\S\n]+" + MoneyPat, Opt))
            {
                decimal a, b, c;
                if (decimal.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out a)
                    && decimal.TryParse(m.Groups[2].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out b)
                    && decimal.TryParse(m.Groups[3].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out c)
                    && a > 0m && b >= 0m && Math.Abs(a + b - c) <= SumTol && b <= a * 0.075m)
                    list.Add(new[] { a, b, c });
                if (list.Count >= 60) break;
            }
            return list;
        }

        private static int Score(Cand s, Cand v, Cand t, decimal? ocrSub, decimal? ocrVat, decimal? ocrTotal, decimal? linesSum, decimal? linesVat,
            List<decimal[]> rows, decimal? orig, decimal? correct, decimal? diff, bool isNote)
        {
            int sc = 0;
            sc += Weight(s) + Weight(v) + Weight(t);
            // ยอดรวมของ OCR น่าเชื่อกว่าช่องอื่น (ป้าย "รวม/TOTAL" ชัดกว่า "ราคาสินค้า") — ใช้เป็นตัวตัดสินเมื่อคะแนนเท่ากัน
            if (Eq(t.Value, ocrTotal)) sc += 1;
            if (linesSum.HasValue && linesSum.Value > 0m && (Eq(s.Value, linesSum.Value) || Eq(t.Value, linesSum.Value))) sc += 3;
            if (linesVat.HasValue && linesVat.Value > 0m && Eq(v.Value, linesVat.Value)) sc += 2;
            if (rows.Any(r => Eq(r[0], s.Value) && Eq(r[1], v.Value) && Eq(r[2], t.Value))) sc += 4;
            if (isNote && diff.HasValue && Eq(t.Value, diff.Value)) sc += 3;
            // VAT เป็นไปได้: 0–7.5% ของฐาน (ใบ VAT ผสมต่ำกว่า 7% ได้) · เกิน = เป็นไปไม่ได้
            if (s.Value > 0m)
            {
                decimal pct = v.Value / s.Value;
                if (pct > 0.075m) sc -= 8;
                else if (v.Value == 0m || Math.Abs(OcrReviewMath.R2(s.Value * 0.07m) - v.Value) <= 0.05m) sc += 1;
            }
            // ช่องที่เท่ากับ "มูลค่าตามใบกำกับเดิม / มูลค่าที่ถูกต้อง" ของ §86/10 = ยอดของใบเดิม ไม่ใช่ยอดใบนี้ (เว้นแต่เท่ากับผลต่างด้วย)
            if (isNote)
            {
                foreach (var x in new[] { s.Value, t.Value })
                {
                    bool isDiff = diff.HasValue && Eq(x, diff.Value);
                    if (!isDiff && ((orig.HasValue && Eq(x, orig.Value)) || (correct.HasValue && Eq(x, correct.Value)))) sc -= 6;
                }
            }
            return sc;
        }

        private static int Weight(Cand c)
        {
            switch (c.Source)
            {
                case "OCR": return 3;
                case "ROW": return 3;
                case "LABEL": return 2;
                case "LINES": return 2;
                case "DIFF": return 2;
                default: return 0;   // DERIVED
            }
        }

        private static string Strength(int score) => score >= 12 ? "HIGH" : score >= 7 ? "MEDIUM" : "LOW";

        private static string Explain(string field, decimal? ocr, Cand chosen, decimal? orig, decimal? correct, bool isNote)
        {
            string why = "";
            if (isNote && ocr.HasValue)
            {
                if (orig.HasValue && Eq(ocr.Value, orig.Value)) why = " = \"มูลค่าตามใบกำกับภาษีเดิม\" (ยอดของใบเดิม ไม่ใช่ใบนี้)";
                else if (correct.HasValue && Eq(ocr.Value, correct.Value)) why = " = \"มูลค่าที่ถูกต้อง\" (ยอดของใบเดิมหลังแก้ ไม่ใช่ใบนี้)";
            }
            return field + ": OCR อ่าน " + M(ocr) + why + " → ใช้ " + OcrReviewMath.Money(chosen.Value) + " (" + chosen.Why + ")";
        }

        private static string M(decimal? v) => v.HasValue ? OcrReviewMath.Money(v.Value) : "ไม่พบ";
        private static bool Eq(decimal a, decimal? b) => b.HasValue && Math.Abs(a - b.Value) <= EqTol;

        private static void Add(List<Cand> list, decimal? v, string source, string why)
        {
            if (!v.HasValue || v.Value < 0m) return;
            decimal x = OcrReviewMath.R2(v.Value);
            var dup = list.FirstOrDefault(c => Math.Abs(c.Value - x) <= EqTol);
            if (dup != null)
            {
                // แหล่งที่น่าเชื่อกว่าชนะ (คง Why ของแหล่งนั้น) — ค่าเดียวกันจากหลายแหล่งไม่บวกซ้ำ (หลักฐานซ้ำให้ผ่านกฎใน Score แทน)
                if (Weight(new Cand { Source = source }) > Weight(dup)) { dup.Source = source; dup.Why = why; }
                return;
            }
            if (list.Count >= 12) return;
            list.Add(new Cand { Value = x, Source = source, Why = why });
        }

        /// <summary>ตัวเลขเงินทุกตัวที่อยู่หลังป้าย (ภายใน 60 ตัวอักษรบรรทัดเดียวกัน) — ทุกป้าย ทุกตำแหน่ง</summary>
        public static List<KeyValuePair<string, decimal>> ReadAllLabeled(string raw, IEnumerable<string> labels)
        {
            var list = new List<KeyValuePair<string, decimal>>();
            if (string.IsNullOrWhiteSpace(raw) || labels == null) return list;
            foreach (var lb in labels)
            {
                string pat = Regex.Escape(lb).Replace(@"\ ", @"\s*");
                foreach (Match m in Regex.Matches(raw, pat + @"[^\d\n]{0,60}?" + MoneyPat, Opt))
                {
                    decimal v;
                    if (decimal.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out v) && v > 0m)
                        list.Add(new KeyValuePair<string, decimal>(lb, v));
                }
                if (list.Count >= 20) break;
            }
            return list;
        }

        /// <summary>แถวสรุปในตาราง "รวม 3,466.58 72.42 3,539.00" (ก่อน VAT · VAT · รวม) — เฉพาะแถวที่บวกลงตัว</summary>
        public static List<decimal[]> ReadSumRows(string raw)
        {
            var list = new List<decimal[]>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (Match m in Regex.Matches(raw, @"(?:^|\n)[^\S\n]*(?:รวม|Total)[^\S\n]*[:：]?[^\S\n]*" + MoneyPat + @"[^\S\n]+" + MoneyPat + @"[^\S\n]+" + MoneyPat, Opt))
            {
                decimal a, b, c;
                if (decimal.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out a)
                    && decimal.TryParse(m.Groups[2].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out b)
                    && decimal.TryParse(m.Groups[3].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out c)
                    && Math.Abs(a + b - c) <= SumTol)
                    list.Add(new[] { a, b, c });
            }
            return list;
        }
    }
}
