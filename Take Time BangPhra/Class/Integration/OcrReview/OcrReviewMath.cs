using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Take_Time_BangPhra.Integration.OcrReview
{
    /// <summary>
    /// ตัวตัดสินกลางของหน้าตรวจทาน OCR (ยอด/ภาษี/เลขผู้เสียภาษี/เพดานใบลดหนี้) — pure ไม่แตะ DB/HTTP
    /// (ทดสอบด้วย mono ได้). หน้าเว็บมี JS สูตรเดียวกันไว้แสดงผลสด แต่ <b>เซิร์ฟเวอร์ตัดสินด้วยคลาสนี้เท่านั้น</b> ก่อนส่ง NextAcc.
    /// </summary>
    public static class OcrReviewMath
    {
        /// <summary>ยอมให้ต่างได้ไม่เกิน 1 สตางค์ (คำสั่งเจ้าของ: ยอดต้องตรงกระดาษ ±0.01)</summary>
        public const decimal Tolerance = 0.01m;
        public const decimal VatRate = 7m;

        public static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

        public static string Digits(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw) if (c >= '0' && c <= '9') sb.Append(c);
            // เลขไทย ๐-๙
            if (sb.Length == 0)
                foreach (char c in raw) if (c >= '๐' && c <= '๙') sb.Append((char)('0' + (c - '๐')));
            return sb.ToString();
        }

        /// <summary>
        /// เลขประจำตัวผู้เสียภาษี 13 หลัก + mod-11 ตามสูตรเดียวกับ NextAcc
        /// (TaxInvoiceCompletenessChecker.IsValidThaiTaxId :153-165): sum = Σ d[i]×(13−i), i=0..11; check = (11 − sum%11) % 10 = d[12].
        /// ยอมเฉพาะตัวเลข/ขีด/ช่องว่างคั่น — ตัวอักษรอื่นปน = พิมพ์ผิด
        /// </summary>
        public static bool IsValidThaiTaxId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            foreach (char c in raw)
                if (!(c >= '0' && c <= '9') && c != ' ' && c != '-' && c != '\t') return false;
            string d = Digits(raw);
            if (d.Length != 13) return false;
            int sum = 0;
            for (int i = 0; i < 12; i++) sum += (d[i] - '0') * (13 - i);
            int check = (11 - sum % 11) % 10;
            return check == d[12] - '0';
        }

        /// <summary>นิติบุคคล = เลข 13 หลักขึ้นต้น 0 (ตรง AccountingDataMapper.IsJuristicPerson / NextAcc IsJuristicBuyer :86-92)</summary>
        public static bool IsJuristic(string taxId)
        {
            string d = Digits(taxId);
            return d.Length == 13 && d[0] == '0';
        }

        /// <summary>แบบยื่นภาษีหัก ณ ที่จ่ายที่จะเกิด: นิติบุคคล ภ.ง.ด.53 · บุคคลธรรมดา ภ.ง.ด.3</summary>
        public static string WhtFormLabel(string taxId) => IsJuristic(taxId) ? "ภ.ง.ด.53" : "ภ.ง.ด.3";

        /// <summary>
        /// รหัสสาขา 5 หลัก — "สำนักงานใหญ่"/"HQ"/"head office" = 00000 · ตัวเลข ≤ 5 หลักเติม 0 ด้านหน้า ·
        /// ว่าง = null · รูปแบบอื่น (ตัวเลขเกิน 5 หลัก) = คืนค่าเดิม (ให้ <see cref="IsValidBranch"/> ฟ้อง)
        /// </summary>
        public static string NormalizeBranch(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string t = raw.Trim();
            string low = t.ToLowerInvariant();
            if (low.Contains("สำนักงานใหญ่") || low.Contains("สนญ") || low == "hq" || low.Contains("head office") || low.Contains("headoffice"))
                return "00000";
            string d = Digits(t);
            if (d.Length == 0) return t;
            if (d.Length <= 5) return d.PadLeft(5, '0');
            return t;
        }

        public static bool IsValidBranch(string normalized) =>
            !string.IsNullOrEmpty(normalized) && normalized.Length == 5 && Digits(normalized) == normalized;

        /// <summary>อัตรา VAT ที่ส่ง NextAcc: 7 · 0 · -1 (ยกเว้น §81 — OcrDtos.cs:279-285)</summary>
        public static decimal NextAccVatRate(string vatType)
        {
            if (string.Equals(vatType, "EX", StringComparison.OrdinalIgnoreCase)) return -1m;
            if (vatType == "0") return 0m;
            return VatRate;
        }

        public static OcrLineCalc CalcLine(OcrReviewLine l)
        {
            var c = new OcrLineCalc();
            if (l == null) return c;
            c.Gross = R2(l.Qty * l.UnitPrice);
            c.Net = R2(c.Gross - l.Discount);
            c.NextAccVatRate = NextAccVatRate(l.VatType);
            if (c.NextAccVatRate > 0)
                c.Vat = l.VatOverride.HasValue ? R2(l.VatOverride.Value) : R2(c.Net * c.NextAccVatRate / 100m);
            c.Wht = l.WhtRate > 0 ? R2(c.Net * l.WhtRate / 100m) : 0m;
            return c;
        }

        public static OcrTotals CalcTotals(IEnumerable<OcrReviewLine> lines)
        {
            var t = new OcrTotals();
            if (lines == null) return t;
            foreach (var l in lines)
            {
                var c = CalcLine(l);
                t.SubTotal += c.Net;
                t.Vat += c.Vat;
                if (l.VatClaimable) t.VatClaimable += c.Vat; else t.VatNonClaimable += c.Vat;
                t.Wht += c.Wht;
            }
            t.SubTotal = R2(t.SubTotal);
            t.Vat = R2(t.Vat);
            t.VatClaimable = R2(t.VatClaimable);
            t.VatNonClaimable = R2(t.VatNonClaimable);
            t.Wht = R2(t.Wht);
            t.Total = R2(t.SubTotal + t.Vat);
            t.NetPay = R2(t.Total - t.Wht);
            return t;
        }

        /// <summary>
        /// เทียบยอดที่คำนวณจากบรรทัดกับยอดตามกระดาษ: ก่อน VAT · VAT · รวม ต้องกรอกและตรง (±0.01) ·
        /// WHT เทียบเฉพาะเมื่อกรอก (ใบผู้ขายส่วนใหญ่ไม่พิมพ์ภาษีที่เราหัก)
        /// </summary>
        public static List<OcrReconItem> Reconcile(OcrTotals t, decimal? printedSub, decimal? printedVat, decimal? printedTotal, decimal? printedWht)
        {
            var list = new List<OcrReconItem>
            {
                Item("SubTotal", "ยอดก่อน VAT", t.SubTotal, printedSub, required: true),
                Item("Vat", "ภาษีมูลค่าเพิ่ม", t.Vat, printedVat, required: true),
                Item("Total", "ยอดรวมตามเอกสาร", t.Total, printedTotal, required: true),
            };
            if (printedWht.HasValue)
                list.Add(Item("Wht", "หัก ณ ที่จ่าย", t.Wht, printedWht, required: false));
            return list;
        }

        private static OcrReconItem Item(string key, string label, decimal computed, decimal? printed, bool required)
        {
            var it = new OcrReconItem { Key = key, Label = label, Computed = computed, Printed = printed };
            if (!printed.HasValue)
            {
                it.Diff = 0m;
                it.Ok = !required;
            }
            else
            {
                it.Diff = R2(computed - printed.Value);
                it.Ok = Math.Abs(it.Diff) <= Tolerance;
            }
            return it;
        }

        /// <summary>
        /// เพดานใบลดหนี้ §86/10 แบบเดียวกับ NextAcc: ΣCN (รวมใบนี้) ≤ ยอดใบต้นทาง (DocumentService.cs:11919-11931) และเมื่อใบต้นทาง
        /// ยังค้างจ่าย ยอดใบนี้ ≤ ยอดค้าง (:11939-11946 — ต้องแยกใบ). ยอดเทียบ = NetPay (NextAcc TotalAmount = ก่อน VAT + VAT − WHT).
        /// คืน null = ผ่าน, ไม่งั้นข้อความภาษาไทย
        /// </summary>
        public static string CheckCreditNoteCap(decimal noteNetPay, decimal sourceTotal, decimal sourceCredited, decimal sourceBalanceDue)
        {
            if (noteNetPay <= 0m) return "ยอดใบลดหนี้ต้องมากกว่า 0";
            decimal remaining = R2(sourceTotal - sourceCredited);
            if (noteNetPay > remaining + Tolerance)
                return "ยอดใบลดหนี้ " + Money(noteNetPay) + " เกินยอดที่ลดหนี้ได้ของใบต้นทาง " + Money(remaining)
                    + " (ยอดใบ " + Money(sourceTotal) + " − ลดหนี้แล้ว " + Money(sourceCredited) + ") — §86/10 ลดหนี้เกินยอดเดิมไม่ได้";
            if (sourceBalanceDue > Tolerance && noteNetPay > sourceBalanceDue + Tolerance)
                return "ยอดใบลดหนี้ " + Money(noteNetPay) + " มากกว่ายอดค้างจ่ายของใบต้นทาง " + Money(sourceBalanceDue)
                    + " — NextAcc ให้ลดหนี้ได้ไม่เกินยอดค้าง (ส่วนที่จ่ายไปแล้วต้องแยกเป็นใบลดหนี้อีกใบหลังปิดยอดค้าง)";
            return null;
        }

        /// <summary>§86/4 ขั้นต่ำที่ NextAcc ใช้ตัดสินเคลมภาษีซื้อ (TaxInvoiceCompletenessChecker.cs:34-62)</summary>
        public static List<string> MissingTaxInvoiceFields(OcrReviewState s)
        {
            var miss = new List<string>();
            if (string.IsNullOrWhiteSpace(s.SupplierName)) miss.Add("ชื่อผู้ขาย");
            if (!IsValidThaiTaxId(s.SupplierTaxId)) miss.Add("เลขผู้เสียภาษีผู้ขาย (13 หลัก + mod-11)");
            if (string.IsNullOrWhiteSpace(s.SupplierAddress)) miss.Add("ที่อยู่ผู้ขาย");
            if (!IsValidBranch(NormalizeBranch(s.SupplierBranch))) miss.Add("รหัสสาขาผู้ขาย");
            if (string.IsNullOrWhiteSpace(s.SupplierDocNo)) miss.Add("เลขที่ใบกำกับภาษีจากผู้ขาย");
            if (ParseDate(s.TaxInvoiceDate) == null) miss.Add("วันที่ใบกำกับภาษีจากผู้ขาย");
            return miss;
        }

        public static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            DateTime d;
            if (DateTime.TryParseExact(s.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
            return null;
        }

        /// <summary>คีย์กันซ้ำ: เลขผู้ขาย (ตัวเลข) + เลขที่เอกสาร (ตัวอักษร/ตัวเลขพิมพ์ใหญ่ ตัดช่องว่าง/ขีด/ทับ)</summary>
        public static string NormalizeDocNo(string docNo)
        {
            if (string.IsNullOrWhiteSpace(docNo)) return "";
            var sb = new StringBuilder();
            foreach (char c in docNo.Trim().ToUpperInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        public static string Money(decimal v) => v.ToString("#,##0.00", CultureInfo.InvariantCulture);

        /// <summary>ตรวจทั้งใบก่อนส่ง (ตัวตัดสินฝั่งเซิร์ฟเวอร์)</summary>
        public static OcrValidation Validate(OcrReviewState s)
        {
            var v = new OcrValidation();
            if (s == null) { v.Errors.Add("ไม่มีข้อมูลที่ตรวจทาน"); return v; }
            bool isNote = OcrDocKind.IsNote(s.DocKind);
            if (!OcrDocKind.IsKnown(s.DocKind)) v.Errors.Add("เลือกชนิดเอกสารที่จะสร้าง");

            var lines = s.Lines ?? new List<OcrReviewLine>();
            v.Totals = CalcTotals(lines);
            v.Recon = Reconcile(v.Totals, s.PrintedSubTotal, s.PrintedVat, s.PrintedTotal, s.PrintedWht);

            // ── ผู้ขาย ──
            if (string.IsNullOrWhiteSpace(s.SupplierName) && s.VendorId <= 0 && !(isNote && s.SourceMode == OcrNoteSource.NextAcc))
                v.Errors.Add("ระบุชื่อผู้ขาย (หรือเลือกผู้ขายจากระบบ)");
            if (!string.IsNullOrWhiteSpace(s.SupplierTaxId) && !IsValidThaiTaxId(s.SupplierTaxId))
                v.Errors.Add("เลขผู้เสียภาษีผู้ขายไม่ถูกต้อง (ต้องเป็น 13 หลักและผ่าน mod-11) — แก้ให้ตรงเอกสาร หรือเว้นว่างถ้าเอกสารไม่มีเลข");
            string br = NormalizeBranch(s.SupplierBranch);
            if (br != null && !IsValidBranch(br))
                v.Errors.Add("รหัสสาขาผู้ขายต้องเป็นตัวเลข 5 หลัก (00000 = สำนักงานใหญ่)");

            // ── วันที่ ──
            if (ParseDate(s.DocDate) == null) v.Errors.Add("ระบุวันที่ลงบัญชี (yyyy-MM-dd)");
            if (!string.IsNullOrWhiteSpace(s.SupplierDocDate) && ParseDate(s.SupplierDocDate) == null)
                v.Errors.Add("วันที่เอกสารผู้ขายไม่ถูกรูปแบบ (yyyy-MM-dd)");
            if (!string.IsNullOrWhiteSpace(s.TaxInvoiceDate) && ParseDate(s.TaxInvoiceDate) == null)
                v.Errors.Add("วันที่ใบกำกับภาษีไม่ถูกรูปแบบ (yyyy-MM-dd)");
            var docD = ParseDate(s.DocDate);
            if (docD.HasValue && docD.Value.Date > DateTime.Today.AddDays(1))
                v.Errors.Add("วันที่ลงบัญชีเป็นวันในอนาคต");
            if (string.IsNullOrWhiteSpace(s.SupplierDocNo))
                v.Warnings.Add("ไม่มีเลขที่เอกสารผู้ขาย — กันซ้ำไม่ได้และเคลมภาษีซื้อไม่ได้");

            // ── บรรทัด ──
            if (lines.Count == 0) v.Errors.Add("ต้องมีอย่างน้อย 1 รายการ");
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                var c = CalcLine(l);
                string n = "รายการที่ " + (i + 1);
                if (string.IsNullOrWhiteSpace(l.Description)) v.Errors.Add(n + ": ระบุรายละเอียด");
                if (l.Qty <= 0) v.Errors.Add(n + ": จำนวนต้องมากกว่า 0");
                if (l.UnitPrice < 0 || l.Discount < 0) v.Errors.Add(n + ": ราคา/ส่วนลดติดลบไม่ได้ (NextAcc ไม่รับบรรทัดติดลบ)");
                if (c.Net <= 0) v.Errors.Add(n + ": ยอดหลังส่วนลดต้องมากกว่า 0");
                if (string.IsNullOrWhiteSpace(l.AccountCode)) v.Errors.Add(n + ": เลือกบัญชี");
                if (l.WhtRate < 0 || l.WhtRate > 15) v.Errors.Add(n + ": อัตราหัก ณ ที่จ่ายต้องอยู่ระหว่าง 0-15%");
                if (c.NextAccVatRate <= 0 && l.VatOverride.HasValue && l.VatOverride.Value != 0m)
                    v.Errors.Add(n + ": บรรทัดไม่มี VAT แต่กรอกยอด VAT");
                if (c.NextAccVatRate > 0 && l.VatOverride.HasValue)
                {
                    decimal std = R2(c.Net * VatRate / 100m);
                    if (Math.Abs(l.VatOverride.Value - std) > 1.00m)
                        v.Errors.Add(n + ": VAT ที่แก้ (" + Money(l.VatOverride.Value) + ") ต่างจาก 7% (" + Money(std) + ") เกิน 1 บาท — ถ้ามีของไม่เสียภาษีปน ให้แยกบรรทัดเป็น VAT 0%/ยกเว้น");
                }
                if (isNote && l.WhtRate > 0)
                    v.Warnings.Add(n + ": ใบลด/เพิ่มหนี้มีหัก ณ ที่จ่าย — NextAcc จะกลับ/เพิ่มผัง 2191x ตามใบต้นทาง ตรวจให้ตรงเงื่อนไขใบต้นทาง");
            }

            // ── ยอดตรงกระดาษ ──
            foreach (var r in v.Recon)
            {
                if (r.Ok) continue;
                if (!r.Printed.HasValue) v.Errors.Add("กรอก" + r.Label + "ตามเอกสาร");
                else v.Errors.Add(r.Label + ": คำนวณได้ " + Money(r.Computed) + " แต่เอกสาร " + Money(r.Printed.Value) + " (ต่าง " + Money(r.Diff) + ")");
            }

            // ── ภาษีซื้อ / §86/4 ──
            bool claimsVat = lines.Any(l => l.VatClaimable && CalcLine(l).Vat > 0);
            if (claimsVat)
            {
                v.MissingTaxInvoiceFields = MissingTaxInvoiceFields(s);
                if (!s.FullTaxInvoice)
                    v.Errors.Add("มีบรรทัดที่เคลมภาษีซื้อ แต่ไม่ได้ติ๊ก \"เป็นใบกำกับภาษีเต็มรูป\" — ใบเสร็จ/ใบกำกับอย่างย่อเคลมไม่ได้ (§82/5(3)) ให้เลือก \"ไม่เคลม\" ที่บรรทัด");
                else if (v.MissingTaxInvoiceFields.Count > 0)
                    v.Warnings.Add("ข้อมูลใบกำกับ §86/4 ยังไม่ครบ (" + string.Join(", ", v.MissingTaxInvoiceFields)
                        + ") — NextAcc จะลงภาษีซื้อพักไว้ที่ 11640 จนกว่าจะเติมครบ");
            }

            // ── ชนิดเอกสาร ──
            if (s.DocKind == OcrDocKind.PaymentVoucher && string.IsNullOrWhiteSpace(s.PaidHowAccountId))
                v.Warnings.Add("ยังไม่เลือกแหล่งจ่ายเงิน — NextAcc จะเลือกบัญชีเงินสด/ธนาคารเอง");

            if (isNote)
            {
                if (s.NoteReason < 1 || s.NoteReason > 4) v.Errors.Add("เลือกเหตุผลของ" + OcrDocKind.Label(s.DocKind) + " (§86/9-10 บังคับระบุ)");
                if (s.SourceMode == OcrNoteSource.NextAcc)
                {
                    Guid g;
                    if (!Guid.TryParse(s.SourceDocId ?? "", out g) || g == Guid.Empty)
                        v.Errors.Add("เลือกเอกสารต้นทางใน NextAcc (ค้นด้วยเลขที่เอกสารหรือชื่อผู้ขาย)");
                    else if (s.DocKind == OcrDocKind.VendorCreditNote)
                    {
                        string cap = CheckCreditNoteCap(v.Totals.NetPay, s.SourceTotal, s.SourceCredited, s.SourceBalanceDue);
                        if (cap != null) v.Errors.Add(cap);
                    }
                }
                else if (s.SourceMode == OcrNoteSource.Outside)
                {
                    if (!s.OutsideCapConfirmed)
                        v.Errors.Add("ใบเดิมอยู่นอกระบบ — ติ๊กยืนยันว่าตรวจแล้วยอดลด/เพิ่มหนี้ไม่เกินยอดใบเดิม (NextAcc ตรวจเพดานให้ไม่ได้)");
                }
                else if (s.SourceMode == OcrNoteSource.PurchaseReturn)
                {
                    if (s.DocKind != OcrDocKind.VendorCreditNote) v.Errors.Add("อ้างการคืนสินค้า (PRT-) ได้เฉพาะใบลดหนี้");
                    if (string.IsNullOrWhiteSpace(s.ReturnRef) || !s.ReturnRef.Trim().StartsWith("PRT-", StringComparison.OrdinalIgnoreCase))
                        v.Errors.Add("ระบุเลขอ้างอิงการคืนสินค้า (ขึ้นต้น PRT-)");
                }
                else v.Errors.Add("เลือกแหล่งใบต้นทาง");

                if (s.Settlement == OcrNoteSettlement.Bank && string.IsNullOrWhiteSpace(s.RefundAccountId))
                    v.Errors.Add("เลือกบัญชีที่รับเงินคืน/จ่ายเงิน");
                if (s.SourceMode != OcrNoteSource.PurchaseReturn && !string.IsNullOrWhiteSpace(s.ReturnRef) && s.DocKind != OcrDocKind.VendorCreditNote)
                    v.Errors.Add("ผูกการคืนสินค้าได้เฉพาะใบลดหนี้");
            }
            return v;
        }

        /// <summary>รายการช่องที่ผู้ใช้แก้จากผล OCR (audit) — "ช่อง: เดิม → ใหม่"</summary>
        public static List<string> DiffCorrections(OcrReviewState ocr, OcrReviewState confirmed)
        {
            var d = new List<string>();
            if (ocr == null || confirmed == null) return d;
            Cmp(d, "ชนิดเอกสาร", ocr.DocKind, confirmed.DocKind);
            Cmp(d, "ชื่อผู้ขาย", ocr.SupplierName, confirmed.SupplierName);
            Cmp(d, "เลขผู้เสียภาษี", Digits(ocr.SupplierTaxId), Digits(confirmed.SupplierTaxId));
            Cmp(d, "สาขา", NormalizeBranch(ocr.SupplierBranch), NormalizeBranch(confirmed.SupplierBranch));
            Cmp(d, "ที่อยู่", ocr.SupplierAddress, confirmed.SupplierAddress);
            Cmp(d, "เลขที่เอกสาร", ocr.SupplierDocNo, confirmed.SupplierDocNo);
            Cmp(d, "วันที่เอกสาร", ocr.SupplierDocDate, confirmed.SupplierDocDate);
            Cmp(d, "ยอดก่อน VAT", Dec(ocr.PrintedSubTotal), Dec(confirmed.PrintedSubTotal));
            Cmp(d, "VAT", Dec(ocr.PrintedVat), Dec(confirmed.PrintedVat));
            Cmp(d, "ยอดรวม", Dec(ocr.PrintedTotal), Dec(confirmed.PrintedTotal));
            int a = ocr.Lines?.Count ?? 0, b = confirmed.Lines?.Count ?? 0;
            if (a != b) d.Add("จำนวนรายการ: " + a + " → " + b);
            for (int i = 0; i < Math.Min(a, b); i++)
            {
                var x = ocr.Lines[i]; var y = confirmed.Lines[i];
                string p = "รายการ " + (i + 1) + " ";
                Cmp(d, p + "รายละเอียด", x.Description, y.Description);
                Cmp(d, p + "ยอด", Money(CalcLine(x).Net), Money(CalcLine(y).Net));
                Cmp(d, p + "บัญชี", x.AccountCode, y.AccountCode);
                Cmp(d, p + "VAT", x.VatType, y.VatType);
                Cmp(d, p + "เคลม", x.VatClaimable ? "เคลม" : "ไม่เคลม", y.VatClaimable ? "เคลม" : "ไม่เคลม");
                Cmp(d, p + "WHT%", x.WhtRate.ToString("0.##", CultureInfo.InvariantCulture), y.WhtRate.ToString("0.##", CultureInfo.InvariantCulture));
            }
            return d;
        }

        private static string Dec(decimal? v) => v.HasValue ? Money(v.Value) : "";

        private static void Cmp(List<string> d, string label, string a, string b)
        {
            string x = (a ?? "").Trim(), y = (b ?? "").Trim();
            if (!string.Equals(x, y, StringComparison.Ordinal))
                d.Add(label + ": " + (x.Length == 0 ? "(ว่าง)" : x) + " → " + (y.Length == 0 ? "(ว่าง)" : y));
        }
    }
}
