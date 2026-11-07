using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

        /// <summary>บรรทัดเดียว ราคาไม่รวม VAT (เข้ากันได้ย้อนหลัง) — ทั้งใบใช้ <see cref="CalcLines(OcrReviewState)"/></summary>
        public static OcrLineCalc CalcLine(OcrReviewLine l) => CalcLine(l, false);

        /// <summary>
        /// ยอดของบรรทัดเดียว (ก่อนกระทบเศษทั้งใบ) — สูตรเดียวกับ NextAcc ComputeLineAmounts (DocumentService.cs:642-684):
        /// gross = round(qty × ราคา) · หลังส่วนลด = gross − ส่วนลด · VatAmountOverride ⇒ VAT ตามนั้น (ราคารวม VAT: ฐาน = หลังส่วนลด − VAT) ·
        /// ราคารวม VAT ⇒ ฐาน = round(หลังส่วนลด × 100/(100+อัตรา)), VAT = ส่วนต่าง · ไม่รวม ⇒ VAT = round(ฐาน × อัตรา/100) · WHT จากฐานก่อน VAT
        /// </summary>
        public static OcrLineCalc CalcLine(OcrReviewLine l, bool pricesIncludeVat)
        {
            var c = new OcrLineCalc();
            if (l == null) return c;
            c.Gross = R2(l.Qty * l.UnitPrice);
            c.Amount = R2(c.Gross - l.Discount);
            c.NextAccVatRate = NextAccVatRate(l.VatType);
            if (c.NextAccVatRate > 0)
            {
                if (l.VatOverride.HasValue)
                {
                    c.Vat = R2(l.VatOverride.Value);
                    c.Net = pricesIncludeVat ? R2(c.Amount - c.Vat) : c.Amount;
                }
                else if (pricesIncludeVat)
                {
                    c.Net = R2(c.Amount * 100m / (100m + c.NextAccVatRate));
                    c.Vat = R2(c.Amount - c.Net);
                }
                else
                {
                    c.Net = c.Amount;
                    c.Vat = R2(c.Net * c.NextAccVatRate / 100m);
                }
            }
            else c.Net = c.Amount;
            c.Wht = l.WhtRate > 0 ? R2(c.Net * l.WhtRate / 100m) : 0m;
            return c;
        }

        /// <summary>
        /// ยอดทุกบรรทัดหลังกระทบเศษ "ทั้งใบ" ต่ออัตรา (เฉพาะบรรทัดที่ไม่ได้แก้ VAT เอง):
        ///   • ไม่รวม VAT: VAT กลุ่ม = round(Σฐาน × อัตรา/100) — ตรง NextAcc ReconcileTaxRounding (DocumentService.cs:802-892)
        ///   • รวม VAT: VAT กลุ่ม = Σยอด − round(Σยอด × 100/(100+อัตรา)) — วิธีที่ใบกำกับราคารวม VAT พิมพ์ (Makro: 3,447 → 3,221.50 + 225.50)
        ///     ขยับฐานสวนทางเพื่อคงยอดตามกระดาษ. NextAcc ใช้เป้า round(Σฐาน×7%) (= 225.51 ในเคสนี้) ⇒ หน้านี้ส่ง VatAmountOverride ทุกบรรทัด
        ///     ที่มีภาษี ซึ่ง NextAcc ไม่กระทบซ้ำ (:812-814) ⇒ ยอดใน NextAcc = ยอดบนกระดาษ
        /// เศษไปบรรทัดฐานใหญ่สุดของกลุ่ม · ต่างเกิน 1 บาทไม่แตะ · WHT กระทบเศษต่ออัตรา (:893-901)
        /// </summary>
        public static List<OcrLineCalc> CalcLines(IList<OcrReviewLine> lines, bool pricesIncludeVat)
        {
            var calcs = new List<OcrLineCalc>();
            if (lines == null) return calcs;
            foreach (var l in lines) calcs.Add(CalcLine(l, pricesIncludeVat));
            var idx = Enumerable.Range(0, calcs.Count).ToList();
            foreach (var g in idx.Where(i => lines[i] != null && !lines[i].VatOverride.HasValue && calcs[i].NextAccVatRate > 0 && calcs[i].Net != 0m)
                                 .GroupBy(i => calcs[i].NextAccVatRate))
            {
                decimal rate = g.Key, target;
                if (pricesIncludeVat)
                {
                    decimal amt = g.Sum(i => calcs[i].Amount);
                    target = R2(amt - R2(amt * 100m / (100m + rate)));
                }
                else target = R2(g.Sum(i => calcs[i].Net) * rate / 100m);
                decimal diff = R2(target - g.Sum(i => calcs[i].Vat));
                if (diff == 0m || Math.Abs(diff) > 1m) continue;
                int j = Largest(g, calcs);
                calcs[j].Vat = R2(calcs[j].Vat + diff);
                if (pricesIncludeVat) calcs[j].Net = R2(calcs[j].Net - diff);
            }
            foreach (var g in idx.Where(i => lines[i] != null && lines[i].WhtRate > 0 && calcs[i].Net != 0m).GroupBy(i => lines[i].WhtRate))
            {
                decimal target = R2(g.Sum(i => calcs[i].Net) * g.Key / 100m);
                decimal diff = R2(target - g.Sum(i => calcs[i].Wht));
                if (diff == 0m || Math.Abs(diff) > 1m) continue;
                int j = Largest(g, calcs);
                calcs[j].Wht = R2(calcs[j].Wht + diff);
            }
            return calcs;
        }

        private static int Largest(IEnumerable<int> g, List<OcrLineCalc> calcs)
        {
            int best = -1;
            foreach (int i in g) if (best < 0 || Math.Abs(calcs[i].Net) > Math.Abs(calcs[best].Net)) best = i;
            return best;
        }

        public static List<OcrLineCalc> CalcLines(OcrReviewState s) => CalcLines(s?.Lines, s != null && s.PricesIncludeVat);

        /// <summary>ไม่รวม VAT (เข้ากันได้ย้อนหลัง)</summary>
        public static OcrTotals CalcTotals(IEnumerable<OcrReviewLine> lines) => CalcTotals(lines, false);

        public static OcrTotals CalcTotals(OcrReviewState s) => CalcTotals(s?.Lines, s != null && s.PricesIncludeVat);

        public static OcrTotals CalcTotals(IEnumerable<OcrReviewLine> lines, bool pricesIncludeVat)
        {
            var t = new OcrTotals();
            if (lines == null) return t;
            var list = lines.ToList();
            var calcs = CalcLines(list, pricesIncludeVat);
            for (int i = 0; i < list.Count; i++)
            {
                var c = calcs[i];
                t.SubTotal += c.Net;
                t.Vat += c.Vat;
                if (list[i] == null || list[i].VatClaimable) t.VatClaimable += c.Vat; else t.VatNonClaimable += c.Vat;
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

        private static int ReconScore(OcrTotals t, decimal? sub, decimal? vat, decimal? total)
        {
            int n = 0;
            if (sub.HasValue && Math.Abs(t.SubTotal - sub.Value) <= Tolerance) n++;
            if (vat.HasValue && Math.Abs(t.Vat - vat.Value) <= Tolerance) n++;
            if (total.HasValue && Math.Abs(t.Total - total.Value) <= Tolerance) n++;
            return n;
        }

        private static List<OcrReviewLine> WithoutVatOverride(IEnumerable<OcrReviewLine> lines) =>
            (lines ?? Enumerable.Empty<OcrReviewLine>()).Where(l => l != null).Select(l => new OcrReviewLine
            {
                Description = l.Description, Qty = l.Qty, UnitPrice = l.UnitPrice, Discount = l.Discount, AccountCode = l.AccountCode,
                VatType = l.VatType, VatClaimable = l.VatClaimable, VatOverride = null, WhtRate = l.WhtRate
            }).ToList();

        /// <summary>
        /// ราคาในรายการรวม VAT แล้วหรือไม่ — ตัดสินจากยอดบนกระดาษ: คำนวณสองแบบ (ไม่นับ VAT ที่แก้เอง) แล้วดูว่าแบบไหนตรง
        /// ก่อน VAT/VAT/รวม มากกว่า; เสมอกัน ⇒ Σยอดบรรทัด ≈ ยอดรวมตามเอกสาร (±1) = รวม VAT · ≈ ยอดก่อน VAT = ไม่รวม.
        /// บรรทัด VAT 0%/ยกเว้น (Makro รหัส 1) คิดเท่ากันทั้งสองแบบ ⇒ ใบผสมตัดสินได้ด้วยสูตรเดียวกัน.
        /// null = ตัดสินไม่ได้ (ไม่มีบรรทัดที่มีภาษี / ไม่มียอดตามกระดาษ / ไม่ตรงทั้งสองแบบ)
        /// </summary>
        public static bool? DetectPricesIncludeVat(IEnumerable<OcrReviewLine> lines, decimal? printedSub, decimal? printedVat, decimal? printedTotal)
        {
            var raw = WithoutVatOverride(lines);
            if (raw.Count == 0 || !raw.Any(l => NextAccVatRate(l.VatType) > 0)) return null;
            if (!printedSub.HasValue && !printedTotal.HasValue) return null;
            if (printedVat.HasValue && printedVat.Value == 0m) return null;
            var ex = CalcTotals(raw, false);
            var inc = CalcTotals(raw, true);
            int se = ReconScore(ex, printedSub, printedVat, printedTotal), si = ReconScore(inc, printedSub, printedVat, printedTotal);
            if (si > se) return true;
            if (se > si) return false;
            decimal sum = R2(raw.Sum(l => CalcLine(l, false).Amount));
            bool nearTotal = printedTotal.HasValue && Math.Abs(sum - printedTotal.Value) <= 1m;
            bool nearSub = printedSub.HasValue && Math.Abs(sum - printedSub.Value) <= 1m;
            if (nearTotal && !nearSub) return true;
            if (nearSub && !nearTotal) return false;
            return null;
        }

        /// <summary>
        /// ปุ่มแก้ด่วนเมื่อยอดไม่ตรงกระดาษ (หน้าเว็บมีสำเนา JS — OcrMath.suggestFixes; parity test):
        ///   FILL_MISSING = ยอดตามเอกสารว่างช่องเดียว เติมจากอีกสองช่อง · SWITCH_BASIS = สลับรวม/ไม่รวม VAT แล้วตรงครบ ·
        ///   FIT_VAT = ต่างแค่ VAT ≤ 1 บาท ใส่เศษที่บรรทัดใหญ่สุด ("ใช้ยอดตามเอกสาร") · ADD_GAP = รายการขาด เพิ่มบรรทัดส่วนต่าง
        /// </summary>
        public static List<OcrFix> SuggestFixes(OcrReviewState s)
        {
            var fixes = new List<OcrFix>();
            if (s == null || s.Lines == null || s.Lines.Count == 0) return fixes;
            var t = CalcTotals(s);
            if (Reconcile(t, s.PrintedSubTotal, s.PrintedVat, s.PrintedTotal, null).All(r => r.Ok)) return fixes;
            int missing = (s.PrintedSubTotal.HasValue ? 0 : 1) + (s.PrintedVat.HasValue ? 0 : 1) + (s.PrintedTotal.HasValue ? 0 : 1);
            if (missing == 1)
            {
                fixes.Add(new OcrFix { Code = "FILL_MISSING", Label = "เติมช่องที่ว่างจากอีกสองช่องตามเอกสาร", Hint = "ยอดรวม = ก่อน VAT + VAT" });
                return fixes;
            }
            if (missing > 0) return fixes;
            var other = CalcTotals(WithoutVatOverride(s.Lines), !s.PricesIncludeVat);
            if (Reconcile(other, s.PrintedSubTotal, s.PrintedVat, s.PrintedTotal, null).All(r => r.Ok))
            {
                fixes.Add(new OcrFix
                {
                    Code = "SWITCH_BASIS",
                    Label = s.PricesIncludeVat ? "สลับเป็น \"ราคายังไม่รวม VAT\"" : "สลับเป็น \"ราคาในรายการรวม VAT แล้ว\"",
                    Hint = s.PricesIncludeVat ? "ผลรวมรายการเท่ากับยอดก่อน VAT บนเอกสาร" : "ผลรวมรายการเท่ากับยอดรวม (รวม VAT) บนเอกสาร — เช่นใบของ Makro"
                });
                return fixes;
            }
            var calcs = CalcLines(s);
            decimal amountSum = R2(calcs.Sum(c => c.Amount));
            bool baseOk = s.PricesIncludeVat ? Math.Abs(amountSum - s.PrintedTotal.Value) <= Tolerance
                                             : Math.Abs(t.SubTotal - s.PrintedSubTotal.Value) <= Tolerance;
            decimal vdiff = R2(s.PrintedVat.Value - t.Vat);
            int taxed = FitVatLine(calcs);
            if (baseOk && vdiff != 0m && Math.Abs(vdiff) <= 1m && taxed >= 0)
            {
                fixes.Add(new OcrFix
                {
                    Code = "FIT_VAT",
                    Label = "ใช้ยอดตามเอกสาร (ปรับ VAT " + Money(vdiff) + " ที่รายการที่ " + (taxed + 1) + ")",
                    Hint = "ต่างแค่เศษสตางค์จากการปัด — ตั้ง VAT ของรายการใหญ่สุดให้ตรงกระดาษ"
                });
                return fixes;
            }
            decimal gap = s.PricesIncludeVat ? R2(s.PrintedTotal.Value - amountSum) : R2(s.PrintedSubTotal.Value - t.SubTotal);
            if (gap > Tolerance)
                fixes.Add(new OcrFix
                {
                    Code = "ADD_GAP",
                    Label = "เพิ่มรายการส่วนต่าง " + Money(gap),
                    Hint = "OCR อ่านรายการไม่ครบ — ตรวจกับภาพแล้วแก้รายละเอียด/บัญชีของบรรทัดที่เพิ่ม"
                });
            return fixes;
        }

        private static int FitVatLine(List<OcrLineCalc> calcs)
        {
            int best = -1;
            for (int i = 0; i < calcs.Count; i++)
                if (calcs[i].NextAccVatRate > 0 && (best < 0 || Math.Abs(calcs[i].Net) > Math.Abs(calcs[best].Net))) best = i;
            return best;
        }

        /// <summary>
        /// เลือกใบกำกับเดิมให้อัตโนมัติจากผู้สมัครที่ "ตรงเลขใบกำกับเดิม" (Exact): ใบเดียว = เลือก · หลายใบ = คัดทีละขั้น
        /// (ผู้ขายเดียวกัน → ลงวันที่ไม่หลังใบลด/เพิ่มหนี้ → ยอดที่ลดได้อีกพอ) จนเหลือใบเดียว · ยังเหลือหลายใบ/ไม่มี = null (ให้ผู้ใช้เลือกเอง)
        /// </summary>
        public static OcrSourcePick PickSource(IList<OcrSourcePick> candidates, OcrReviewState s)
        {
            if (candidates == null) return null;
            var set = candidates.Where(c => c != null && c.Exact).ToList();
            if (set.Count <= 1) return set.FirstOrDefault();
            Func<List<OcrSourcePick>, List<OcrSourcePick>, List<OcrSourcePick>> narrow = (cur, next) => next.Count > 0 ? next : cur;

            // ผู้ขายเดียวกัน
            string cid = s?.SourceContactId;
            string name = NormalizeName(s?.SupplierName);
            var bySupplier = set.Where(c => (!string.IsNullOrEmpty(cid) && string.Equals(c.ContactId, cid, StringComparison.OrdinalIgnoreCase))
                                         || (name.Length >= 4 && NormalizeName(c.ContactName).Length >= 4
                                             && (NormalizeName(c.ContactName).Contains(name) || name.Contains(NormalizeName(c.ContactName))))).ToList();
            set = narrow(set, bySupplier);
            if (set.Count == 1) return set[0];

            // ลงวันที่ไม่หลังใบลด/เพิ่มหนี้
            DateTime noteDate;
            string nd = s?.SupplierDocDate ?? s?.DocDate;
            if (!string.IsNullOrEmpty(nd) && DateTime.TryParseExact(nd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out noteDate))
            {
                var before = set.Where(c => { DateTime d; return DateTime.TryParseExact(c.Date ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && d <= noteDate; }).ToList();
                set = narrow(set, before);
                if (set.Count == 1) return set[0];
            }

            // ยอดที่ลดได้อีกพอสำหรับใบนี้ (ใบเพิ่มหนี้ไม่มีเพดาน — ข้าม)
            if (s != null && s.DocKind == OcrDocKind.VendorCreditNote)
            {
                decimal need = Math.Abs(CalcTotals(s).Total);
                if (need > 0m)
                {
                    var enough = set.Where(c => c.Remaining + Tolerance >= need).ToList();
                    set = narrow(set, enough);
                    if (set.Count == 1) return set[0];
                }
            }
            return null;
        }

        /// <summary>ชื่อผู้ขายเพื่อเทียบหยาบ ๆ: ตัดคำนำหน้านิติบุคคล/วงเล็บ/ช่องว่าง/จุด, ตัวพิมพ์เล็ก</summary>
        public static string NormalizeName(string n)
        {
            if (string.IsNullOrWhiteSpace(n)) return "";
            string x = Regex.Replace(n, @"\(.*?\)", " ");
            x = Regex.Replace(x, @"บริษัท|บจก\.?|บมจ\.?|ห้างหุ้นส่วนจำกัด|หจก\.?|จำกัด|มหาชน|co\.?|ltd\.?|limited|public|company|plc\.?", " ", RegexOptions.IgnoreCase);
            return Regex.Replace(x, @"[\s\.\-,]", "").ToLowerInvariant();
        }

        /// <summary>
        /// เลือก "ราคาในรายการรวม VAT แล้ว / ยังไม่รวม" ให้เองตามยอดบนกระดาษ (<see cref="DetectPricesIncludeVat"/>) ตราบใดที่ผู้ใช้ยังไม่เลือกเอง
        /// (<c>PriceBasisSource != "USER"</c>) — เรียกทุกครั้งที่หน้าคำนวณใหม่และก่อนส่ง (เดิมตรวจครั้งเดียวตอนสแกน แล้วแค่ขึ้นคำใบ้ "ควรเป็น…"
        /// เมื่อผู้ใช้แก้ยอดตามเอกสาร/รายการ ⇒ ผู้ใช้ต้องสลับเอง). สลับแล้วล้าง VAT override และปรับเศษ VAT ตามกระดาษใหม่ (FIT_VAT).
        /// ตัวเดียวกับ JS <c>OcrMath.autoBasis</c> — คืน true เมื่อสลับ
        /// </summary>
        public static bool AutoPriceBasis(OcrReviewState s)
        {
            if (s == null || s.Lines == null || s.Lines.Count == 0) return false;
            if (string.Equals(s.PriceBasisSource, "USER", StringComparison.Ordinal)) return false;
            var det = DetectPricesIncludeVat(s.Lines, s.PrintedSubTotal, s.PrintedVat, s.PrintedTotal);
            if (!det.HasValue || det.Value == s.PricesIncludeVat) return false;
            s.PricesIncludeVat = det.Value;
            s.PriceBasisSource = "AUTO";
            foreach (var l in s.Lines) if (l != null) l.VatOverride = null;
            if (SuggestFixes(s).Any(f => f.Code == "FIT_VAT")) ApplyFix(s, "FIT_VAT");
            return true;
        }

        /// <summary>ทำตามปุ่มแก้ด่วน (ตัวเดียวกับ JS OcrMath.applyFix) — คืน true เมื่อเปลี่ยนสถานะ</summary>
        public static bool ApplyFix(OcrReviewState s, string code)
        {
            if (s == null || s.Lines == null) return false;
            switch (code)
            {
                case "FILL_MISSING":
                    if (!s.PrintedSubTotal.HasValue && s.PrintedVat.HasValue && s.PrintedTotal.HasValue) s.PrintedSubTotal = R2(s.PrintedTotal.Value - s.PrintedVat.Value);
                    else if (!s.PrintedVat.HasValue && s.PrintedSubTotal.HasValue && s.PrintedTotal.HasValue) s.PrintedVat = R2(s.PrintedTotal.Value - s.PrintedSubTotal.Value);
                    else if (!s.PrintedTotal.HasValue && s.PrintedSubTotal.HasValue && s.PrintedVat.HasValue) s.PrintedTotal = R2(s.PrintedSubTotal.Value + s.PrintedVat.Value);
                    else return false;
                    return true;
                case "SWITCH_BASIS":
                    s.PricesIncludeVat = !s.PricesIncludeVat;
                    s.PriceBasisSource = "USER";
                    foreach (var l in s.Lines) if (l != null) l.VatOverride = null;
                    return true;
                case "FIT_VAT":
                {
                    if (!s.PrintedVat.HasValue) return false;
                    var calcs = CalcLines(s);
                    int j = FitVatLine(calcs);
                    if (j < 0) return false;
                    decimal diff = R2(s.PrintedVat.Value - CalcTotals(s).Vat);
                    s.Lines[j].VatOverride = R2(calcs[j].Vat + diff);
                    return true;
                }
                case "ADD_GAP":
                {
                    var calcs = CalcLines(s);
                    decimal gap = s.PricesIncludeVat
                        ? (s.PrintedTotal.HasValue ? R2(s.PrintedTotal.Value - R2(calcs.Sum(c => c.Amount))) : 0m)
                        : (s.PrintedSubTotal.HasValue ? R2(s.PrintedSubTotal.Value - CalcTotals(s).SubTotal) : 0m);
                    if (gap <= Tolerance) return false;
                    var b = s.Lines.FirstOrDefault(x => x != null) ?? new OcrReviewLine();
                    s.Lines.Add(new OcrReviewLine
                    {
                        Description = "รายการเพิ่มเติมตามเอกสาร", Qty = 1m, UnitPrice = gap, AccountCode = b.AccountCode,
                        VatType = b.VatType ?? "7", VatClaimable = b.VatClaimable, WhtRate = b.WhtRate
                    });
                    return true;
                }
            }
            return false;
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
                    + " (ยอดใบ " + Money(sourceTotal) + " − ลดหนี้แล้ว " + Money(sourceCredited) + ") — §86/10 ลดหนี้เกินยอดเดิมไม่ได้"
                    + " · แก้: ตรวจว่าเลือกใบต้นทางถูก (เทียบ \"มูลค่าตามใบกำกับภาษีเดิม\" บนกระดาษ) / ราคาในรายการรวม VAT หรือไม่";
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
            bool incl = s.PricesIncludeVat;
            var calcs = CalcLines(lines, incl);
            v.Totals = CalcTotals(lines, incl);
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
                if (l == null) { v.Errors.Add("รายการที่ " + (i + 1) + ": ว่าง"); continue; }
                var c = calcs[i];
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
                    decimal std = incl ? R2(c.Amount - R2(c.Amount * 100m / (100m + VatRate))) : R2(c.Net * VatRate / 100m);
                    if (Math.Abs(l.VatOverride.Value - std) > 1.00m)
                        v.Errors.Add(n + ": VAT ที่แก้ (" + Money(l.VatOverride.Value) + ") ต่างจาก 7% (" + Money(std) + ") เกิน 1 บาท — ถ้ามีของไม่เสียภาษีปน ให้แยกบรรทัดเป็น VAT 0%/ยกเว้น"
                            + (incl ? "" : " · ถ้าราคาบนกระดาษรวม VAT แล้ว ให้สลับ \"ราคาในรายการรวม VAT แล้ว\""));
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
            v.Fixes = SuggestFixes(s);

            // ── ภาษีซื้อ / §86/4 ──
            bool claimsVat = Enumerable.Range(0, lines.Count).Any(i => lines[i] != null && lines[i].VatClaimable && calcs[i].Vat > 0);
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
                var reason = OcrNoteReasons.Find(s.DocKind, s.NoteReasonCode);
                if (reason == null && (s.NoteReason < 1 || s.NoteReason > 4)) v.Errors.Add("เลือกเหตุผลของ" + OcrDocKind.Label(s.DocKind) + " (§86/9-10 บังคับระบุ)");
                if (reason != null && reason.NeedsText && string.IsNullOrWhiteSpace(s.NoteReasonText))
                    v.Errors.Add("เหตุผล \"อื่น ๆ\" — พิมพ์รายละเอียดเหตุผล (§86/9-10 บังคับระบุบนเอกสาร)");
                bool physical = reason != null ? reason.PhysicalReturn : s.NoteReason == 1;
                if (s.DocKind == OcrDocKind.VendorCreditNote && !string.IsNullOrWhiteSpace(s.ReturnRef) && !physical)
                    v.Errors.Add("ผูกการคืนสินค้าได้เฉพาะเหตุผล \"คืนสินค้า\" / \"ชำรุด-ผิดสเปก (ส่งคืน)\" — "
                        + (reason != null && reason.Code == "SHORT" ? "ผู้ขายส่งไม่ครบไม่มีของส่งกลับ ให้เลือก \"ไม่ใช่การคืนสินค้า\"" : "ส่วนลด/ปรับราคาไม่มีของส่งกลับ"));
                CheckPaperAmounts(s, v);
                if (s.SourceMode == OcrNoteSource.NextAcc)
                {
                    Guid g;
                    if (!Guid.TryParse(s.SourceDocId ?? "", out g) || g == Guid.Empty)
                        v.Errors.Add("เลือกเอกสารต้นทางใน NextAcc (ค้นด้วยเลขที่เอกสารหรือชื่อผู้ขาย)");
                    else
                    {
                        if (s.DocKind == OcrDocKind.VendorCreditNote)
                        {
                            string cap = CheckCreditNoteCap(v.Totals.NetPay, s.SourceTotal, s.SourceCredited, s.SourceBalanceDue);
                            if (cap != null) v.Errors.Add(cap);
                        }
                        string a = NormalizeDocNo(s.OrigTaxInvoiceNo), b = NormalizeDocNo(s.SourceSupplierDocNo);
                        if (a.Length > 0 && b.Length > 0 && a != b)
                            v.Warnings.Add("ใบลด/เพิ่มหนี้อ้างใบกำกับเดิมเลข " + s.OrigTaxInvoiceNo.Trim() + " แต่ใบต้นทางที่เลือก (" + (s.SourceDocNo ?? "") + ") บันทึกเลขใบผู้ขาย "
                                + s.SourceSupplierDocNo.Trim() + " — ตรวจว่าเลือกใบถูก");
                        if (s.OrigAmount.HasValue && s.SourceTotal > 0m
                            && Math.Abs(s.OrigAmount.Value - s.SourceTotal) > 1m && Math.Abs(s.OrigAmount.Value - s.SourceSubTotal) > 1m)
                            v.Warnings.Add("มูลค่าตามใบกำกับเดิมบนกระดาษ " + Money(s.OrigAmount.Value) + " ≠ ยอดใบต้นทางใน NextAcc " + Money(s.SourceTotal)
                                + " (ก่อน VAT " + Money(s.SourceSubTotal) + ") — ตรวจว่าเลือกใบถูก หรือใบเดิมบันทึกยอดไม่ครบ");
                    }
                }
                else if (s.SourceMode == OcrNoteSource.Outside)
                {
                    if (!s.OutsideCapConfirmed)
                        v.Errors.Add("ใบเดิมอยู่นอกระบบ — ติ๊กยืนยันว่าตรวจแล้วยอดลด/เพิ่มหนี้ไม่เกินยอดใบเดิม (NextAcc ตรวจเพดานให้ไม่ได้)");
                    if (string.IsNullOrWhiteSpace(s.OrigTaxInvoiceNo))
                        v.Errors.Add("ใบเดิมอยู่นอกระบบ — กรอก \"เลขที่ใบกำกับภาษีเดิม\" (§86/9-10 บังคับอ้างเลขที่ใบเดิม; NextAcc พิมพ์จากช่องอ้างอิง)");
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

        /// <summary>
        /// §86/10 บนกระดาษ: มูลค่าตามใบกำกับเดิม − มูลค่าที่ถูกต้อง = ผลต่าง และผลต่าง = ยอดของใบนี้ (รวม VAT หรือก่อน VAT) — เตือนเมื่อ OCR อ่านผิด
        /// (NextAcc พิมพ์สามช่องนี้เองจากใบต้นทางที่ผูก: PdfGenerationService.cs:1862-1901, :2203-2232 ⇒ ไม่มีช่องให้ส่ง)
        /// </summary>
        private static void CheckPaperAmounts(OcrReviewState s, OcrValidation v)
        {
            if (s.OrigAmount.HasValue && s.CorrectAmount.HasValue && s.DiffAmount.HasValue
                && Math.Abs(R2(s.OrigAmount.Value - s.CorrectAmount.Value) - s.DiffAmount.Value) > Tolerance)
                v.Warnings.Add("บนกระดาษ: มูลค่าเดิม " + Money(s.OrigAmount.Value) + " − มูลค่าที่ถูกต้อง " + Money(s.CorrectAmount.Value)
                    + " ≠ ผลต่าง " + Money(s.DiffAmount.Value) + " — ตรวจตัวเลขที่ OCR อ่านกับภาพ");
            if (s.DiffAmount.HasValue && v.Totals != null
                && Math.Abs(s.DiffAmount.Value - v.Totals.Total) > Tolerance && Math.Abs(s.DiffAmount.Value - v.Totals.SubTotal) > Tolerance)
                v.Warnings.Add("ผลต่างบนกระดาษ " + Money(s.DiffAmount.Value) + " ≠ ยอดของใบนี้ (รวม " + Money(v.Totals.Total) + " / ก่อน VAT "
                    + Money(v.Totals.SubTotal) + ")");
        }

        // ──────────────────────────────────────────────────────────────
        // ใบต้นทาง: จับคู่บัญชีของบรรทัด / ขาเงินเริ่มต้น
        // ──────────────────────────────────────────────────────────────

        /// <summary>รหัสสินค้านำหน้ารายละเอียด (เช่น "803243 ซูการ์เดน…" → 803243) — ตัวเลข 4-14 หลัก หรือรหัสผสมที่มีตัวเลข ≥ 4 ตัว</summary>
        public static string ExtractItemCode(string desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(desc.Trim(), @"^([A-Za-z]{0,3}\d{4,14}[A-Za-z]?)(?=[\s\-:.]|$)");
            return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
        }

        private static string NormText(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char c in s.ToLowerInvariant())
                if (char.IsLetterOrDigit(c) || (c >= '฀' && c <= '๿')) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>ความคล้ายของรายละเอียด (Dice coefficient บน bigram ตัวอักษร หลังตัดช่องว่าง/เครื่องหมาย) 0..1</summary>
        public static double Similarity(string a, string b)
        {
            string x = NormText(a), y = NormText(b);
            if (x.Length == 0 || y.Length == 0) return 0;
            if (x == y) return 1;
            if (x.Length < 2 || y.Length < 2) return 0;
            var bx = new Dictionary<string, int>();
            for (int i = 0; i < x.Length - 1; i++) { string k = x.Substring(i, 2); int n; bx.TryGetValue(k, out n); bx[k] = n + 1; }
            int inter = 0;
            for (int i = 0; i < y.Length - 1; i++)
            {
                string k = y.Substring(i, 2); int n;
                if (bx.TryGetValue(k, out n) && n > 0) { inter++; bx[k] = n - 1; }
            }
            return 2.0 * inter / ((x.Length - 1) + (y.Length - 1));
        }

        /// <summary>
        /// หาบรรทัดของใบต้นทางที่ตรงกับบรรทัดใบลด/เพิ่มหนี้: (1) รหัสสินค้า (2) รายละเอียดคล้าย ≥ 0.6 (3) ราคาต่อหน่วยตรง (เทียบได้ทั้งรวม/ไม่รวม VAT).
        /// คืน index หรือ -1
        /// </summary>
        public static int MatchSourceLine(OcrReviewLine l, IList<OcrSourceLine> src, bool linePricesIncludeVat, out string why)
        {
            why = null;
            if (l == null || src == null || src.Count == 0) return -1;
            string code = !string.IsNullOrWhiteSpace(l.ProductCode) ? l.ProductCode.Trim().ToUpperInvariant() : ExtractItemCode(l.Description);
            if (!string.IsNullOrEmpty(code))
                for (int i = 0; i < src.Count; i++)
                {
                    var sl = src[i];
                    string sc = !string.IsNullOrWhiteSpace(sl.ProductCode) ? sl.ProductCode.Trim().ToUpperInvariant() : ExtractItemCode(sl.Description);
                    if (sc == code || (!string.IsNullOrEmpty(sl.Description) && sl.Description.IndexOf(code, StringComparison.OrdinalIgnoreCase) >= 0))
                    { why = "รหัสสินค้า " + code; return i; }
                }
            int best = -1; double bestSim = 0;
            for (int i = 0; i < src.Count; i++)
            {
                double sim = Similarity(l.Description, src[i].Description);
                if (sim > bestSim) { bestSim = sim; best = i; }
            }
            if (best >= 0 && bestSim >= 0.6) { why = "รายละเอียดใกล้เคียง " + Math.Round(bestSim * 100) + "%"; return best; }
            if (l.UnitPrice > 0)
            {
                decimal ex = linePricesIncludeVat ? R2(l.UnitPrice * 100m / 107m) : l.UnitPrice;
                decimal inc = linePricesIncludeVat ? l.UnitPrice : R2(l.UnitPrice * 1.07m);
                for (int i = 0; i < src.Count; i++)
                {
                    var sl = src[i];
                    decimal su = sl.UnitPrice, sa = sl.Qty != 0 ? R2(sl.Amount / sl.Qty) : sl.Amount;
                    if (Math.Abs(su - ex) <= Tolerance || Math.Abs(su - inc) <= Tolerance || Math.Abs(sa - ex) <= Tolerance)
                    { why = "ราคาต่อหน่วยตรง " + Money(su); return i; }
                }
            }
            return -1;
        }

        /// <summary>บัญชีหลักของใบต้นทาง (ยอดรวมสูงสุดต่อรหัสบัญชี)</summary>
        public static string DominantAccount(IList<OcrSourceLine> src)
        {
            if (src == null) return null;
            return src.Where(x => x != null && !string.IsNullOrWhiteSpace(x.AccountCode))
                      .GroupBy(x => x.AccountCode.Trim())
                      .OrderByDescending(g => g.Sum(x => Math.Abs(x.Amount))).ThenBy(g => g.Key, StringComparer.Ordinal)
                      .Select(g => g.Key).FirstOrDefault();
        }

        /// <summary>
        /// ตั้งบัญชีของบรรทัดใบลด/เพิ่มหนี้ตามบรรทัดที่จับคู่ได้ในใบต้นทาง (ไม่พบ = บัญชีหลักของใบเดิม).
        /// overwrite=false ⇒ แตะเฉพาะบรรทัดที่ยังไม่เลือกบัญชี หรือยังเป็นบัญชีที่ OCR แนะนำ (ผู้ใช้ยังไม่แก้). คืนจำนวนบรรทัดที่ตั้ง
        /// </summary>
        public static int AssignAccountsFromSource(OcrReviewState s, bool overwrite)
        {
            if (s?.Lines == null || s.SourceLines == null || s.SourceLines.Count == 0) return 0;
            string dom = DominantAccount(s.SourceLines);
            int n = 0;
            foreach (var l in s.Lines)
            {
                if (l == null) continue;
                string why;
                int i = MatchSourceLine(l, s.SourceLines, s.PricesIncludeVat, out why);
                bool matched = i >= 0 && !string.IsNullOrWhiteSpace(s.SourceLines[i].AccountCode);
                string code = matched ? s.SourceLines[i].AccountCode.Trim() : dom;
                if (string.IsNullOrEmpty(code)) continue;
                l.SourceAccountCode = code;
                l.SourceMatch = matched ? why : "บัญชีหลักของใบเดิม";
                bool untouched = string.IsNullOrWhiteSpace(l.AccountCode)
                    || string.Equals((l.AccountCode ?? "").Trim(), (l.OcrAccountCode ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
                if (overwrite || untouched)
                {
                    if (!string.Equals(l.AccountCode, code, StringComparison.Ordinal)) n++;
                    l.AccountCode = code;
                }
            }
            return n;
        }

        /// <summary>
        /// ขาเงินเริ่มต้นหลังเลือกใบต้นทาง (ผู้ใช้ยังเปลี่ยนได้ — ไม่ทับเมื่อ <see cref="OcrReviewState.SettlementUserSet"/>):
        /// ใบเดิมยังค้างจ่าย ⇒ AUTO (NextAcc ลดเจ้าหนี้ — DocumentService.cs:17933-17936) ·
        /// จ่ายครบแล้ว + รู้บัญชีที่จ่าย ⇒ BANK บัญชีเดียวกัน (หักเงินกลับเข้าแหล่งเดิม) · ไม่รู้บัญชี ⇒ VENDOR (cnPrimary) / AUTO
        /// </summary>
        public static void DefaultSettlement(OcrReviewState s, bool cnPrimary)
        {
            if (s == null || s.SettlementUserSet || !OcrDocKind.IsNote(s.DocKind)) return;
            bool hasSource = s.SourceMode == OcrNoteSource.NextAcc && !string.IsNullOrWhiteSpace(s.SourceDocId);
            if (!hasSource || s.SourceBalanceDue > 0.01m) { s.Settlement = OcrNoteSettlement.Auto; return; }
            if (!string.IsNullOrWhiteSpace(s.SourcePaidAccountId)) { s.Settlement = OcrNoteSettlement.Bank; s.RefundAccountId = s.SourcePaidAccountId; return; }
            s.Settlement = cnPrimary ? OcrNoteSettlement.VendorAccount : OcrNoteSettlement.Auto;
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
            Cmp(d, "ราคาในรายการ", ocr.PricesIncludeVat ? "รวม VAT" : "ไม่รวม VAT", confirmed.PricesIncludeVat ? "รวม VAT" : "ไม่รวม VAT");
            if (OcrDocKind.IsNote(confirmed.DocKind))
            {
                Cmp(d, "เหตุผล", ocr.NoteReasonCode, confirmed.NoteReasonCode);
                Cmp(d, "ใบกำกับเดิม", ocr.OrigTaxInvoiceNo, confirmed.OrigTaxInvoiceNo);
            }
            int a = ocr.Lines?.Count ?? 0, b = confirmed.Lines?.Count ?? 0;
            if (a != b) d.Add("จำนวนรายการ: " + a + " → " + b);
            for (int i = 0; i < Math.Min(a, b); i++)
            {
                var x = ocr.Lines[i]; var y = confirmed.Lines[i];
                string p = "รายการ " + (i + 1) + " ";
                Cmp(d, p + "รายละเอียด", x.Description, y.Description);
                Cmp(d, p + "ยอด", Money(CalcLine(x, ocr.PricesIncludeVat).Amount), Money(CalcLine(y, confirmed.PricesIncludeVat).Amount));
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

    /// <summary>
    /// อ่านข้อมูล §86/9-10 จากข้อความดิบของใบลด/เพิ่มหนี้ (OcrResultResponse.RawTextContent — NextAcc OcrService.cs MapToResponse ส่งคืน):
    /// เลขที่/วันที่ใบกำกับเดิม · มูลค่าตามใบเดิม/ที่ถูกต้อง/ผลต่าง · เหตุผล · วิธีชำระ. best-effort — ค่าที่อ่านได้ผู้ใช้แก้ได้ทุกช่อง
    /// (คำของเหตุผลตาม NextAcc OcrCreditNoteReasonReader.cs:18-33 + เพิ่ม "ส่งไม่ครบ/ไม่มีสินค้า", "ชำรุด/ผิดสเปก", "คิดราคาผิด")
    /// </summary>
    public static class OcrCnPaperReader
    {
        private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private const string MoneyPat = @"(\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?|\d+\.\d{2})";

        /// <summary>"อ้างอิงใบกำกับภาษีเลขที่ 005901386182 ลงวันที่ 20/10/2026"</summary>
        public static bool ReadOriginalInvoice(string raw, out string number, out string isoDate)
        {
            number = null; isoDate = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            // ป้ายที่พบบนใบลด/เพิ่มหนี้ของผู้ขายแต่ละเจ้า (เรียงจากชัดสุด) — เลขที่ = ตัวอักษร/ตัวเลขต่อกัน 4-31 ตัว (มี - / ได้), ตามด้วยวันที่ได้
            string[] labels =
            {
                @"อ้าง\s*(?:อิง|ถึง)\s*(?:ใบ\s*เสร็จ\s*(?:รับ\s*เงิน)?\s*/\s*)?ใบ\s*กำกับ\s*(?:ภาษี)?\s*(?:เดิม)?\s*(?:เลข\s*ที่|No\.?)?",
                @"(?:เลข\s*ที่\s*)?ใบ\s*กำกับ\s*ภาษี\s*เดิม\s*(?:เลข\s*ที่)?",
                @"ตาม\s*ใบ\s*กำกับ\s*ภาษี\s*(?:เลข\s*ที่)?",
                @"อ้าง\s*(?:อิง|ถึง)\s*(?:ใบ\s*)?(?:แจ้งหนี้|ส่งของ|ส่งสินค้า|อินวอยซ์|เอกสาร)\s*(?:เดิม)?\s*(?:เลข\s*ที่|No\.?)?",
                @"Ref(?:erence)?\.?\s*(?:Tax\s*)?Inv(?:oice)?\.?\s*(?:No\.?)?",
                @"Orig(?:inal)?\.?\s*(?:Tax\s*)?Inv(?:oice)?\.?\s*(?:No\.?)?",
                @"Invoice\s*Ref(?:erence)?\.?\s*(?:No\.?)?"
            };
            foreach (var lb in labels)
            {
                var m = Regex.Match(raw, "(?:" + lb + @")\s*[:：#]?\s*([A-Z0-9][A-Z0-9\-/]{3,30})"
                    + @"(?:[^\n\d]{0,30}?(\d{1,2}[/.\-]\d{1,2}[/.\-]\d{2,4}))?", Opt);
                if (!m.Success) continue;
                string no = m.Groups[1].Value.Trim().TrimEnd('-', '/');
                if (!Regex.IsMatch(no, @"\d")) continue;   // ต้องมีตัวเลข (กันคำอย่าง "DATE")
                number = no;
                if (m.Groups[2].Success) isoDate = ParseDate(m.Groups[2].Value);
                return true;
            }
            return false;
        }

        /// <summary>วันที่ dd/MM/yyyy (พ.ศ. &gt; 2400 แปลงเป็น ค.ศ.) → yyyy-MM-dd · ปี 2 หลัก = 20xx</summary>
        public static string ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var m = Regex.Match(s.Trim(), @"^(\d{1,2})[/.\-](\d{1,2})[/.\-](\d{2,4})$");
            if (!m.Success) return null;
            int d = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), mo = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                y = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (y < 100) y += 2000;
            if (y > 2400) y -= 543;
            if (mo < 1 || mo > 12 || d < 1 || d > DateTime.DaysInMonth(Math.Max(1, Math.Min(9999, y)), mo)) return null;
            return new DateTime(y, mo, d).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>ยอดเงินตัวแรกหลังป้าย (ภายใน 80 ตัวอักษร) — ไม่พบ = null</summary>
        public static decimal? ReadLabeledAmount(string raw, params string[] labels)
        {
            if (string.IsNullOrWhiteSpace(raw) || labels == null) return null;
            foreach (var lb in labels)
            {
                var m = Regex.Match(raw, Regex.Escape(lb).Replace(@"\ ", @"\s*") + @"[^\d\n]{0,60}?" + MoneyPat, Opt);
                if (!m.Success) m = Regex.Match(raw, Regex.Escape(lb).Replace(@"\ ", @"\s*") + @"[^\d]{0,80}?" + MoneyPat, Opt);
                decimal v;
                if (m.Success && decimal.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out v)) return v;
            }
            return null;
        }

        public static decimal? ReadOriginalAmount(string raw) =>
            ReadLabeledAmount(raw, "มูลค่าตามใบกำกับภาษีเดิม", "มูลค่าสินค้าตามใบกำกับภาษีเดิม", "มูลค่าเดิม", "ORIGINAL AMOUNT", "Original Value");

        public static decimal? ReadCorrectAmount(string raw) =>
            ReadLabeledAmount(raw, "มูลค่าที่ถูกต้อง", "มูลค่าสินค้าที่ถูกต้อง", "CORRECT AMOUNT", "Correct Value");

        public static decimal? ReadDiffAmount(string raw) =>
            ReadLabeledAmount(raw, "มูลค่าลดหนี้", "มูลค่าเพิ่มหนี้", "ผลต่าง", "CREDIT AMOUNT", "DEBIT AMOUNT", "Difference");

        /// <summary>รหัสเหตุผลของเรา (<see cref="OcrNoteReasons"/>) จากข้อความ — ไม่พบ = null (ผู้ใช้เลือกเอง)</summary>
        public static string ReadReasonCode(string raw, bool debitNote)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string t = raw.ToLowerInvariant();
            if (debitNote)
            {
                if (t.Contains("ค่าขนส่ง") || t.Contains("ค่าติดตั้ง") || t.Contains("เรียกเก็บเพิ่ม")) return "CHARGE";
                if (t.Contains("ส่งเกิน") || t.Contains("สินค้าเกิน")) return "EXTRA";
                if (t.Contains("ราคาเพิ่ม") || t.Contains("ปรับราคาขึ้น")) return "PRICE_UP";
                if (t.Contains("คำนวณผิด") || t.Contains("คลาดเคลื่อน")) return "CALC_ERR";
                return null;
            }
            if (t.Contains("ไม่ครบ") || t.Contains("ขาดส่ง") || t.Contains("ส่งขาด") || t.Contains("ไม่มีสินค้า") || t.Contains("สินค้าหมด") || t.Contains("short deliver"))
                return "SHORT";
            if (t.Contains("ชำรุด") || t.Contains("เสียหาย") || t.Contains("ผิดสเปก") || t.Contains("ผิดสเปค") || t.Contains("defect") || t.Contains("damaged"))
                return "DEFECT";
            if (t.Contains("คืนสินค้า") || t.Contains("สินค้าคืน") || t.Contains("goods return") || t.Contains("returned goods")) return "RETURN";
            if (t.Contains("คิดราคาผิด") || t.Contains("ราคาผิด") || t.Contains("คำนวณผิด") || t.Contains("คลาดเคลื่อน")) return "PRICE_ERR";
            if (Regex.IsMatch(t, @"(ส่วนลด|ลดราคา|discount)[^\n0-9]{0,20}[1-9]")) return "DISCOUNT";
            return null;
        }

        /// <summary>วิธีชำระบนใบ ("Credit Card/Debit Card", "เงินสด", "โอน") — คำใบ้เลือกบัญชีรับเงินคืน</summary>
        public static string ReadPaymentType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var m = Regex.Match(raw, @"(?:วิธี(?:การ)?ชำระ(?:เงิน)?|Payment\s*type)[^\n]{0,40}?(Credit\s*Card(?:\s*/\s*Debit\s*Card)?|Debit\s*Card|บัตรเครดิต|เงินสด|Cash|โอน(?:เงิน)?|Transfer|QR|พร้อมเพย์|PromptPay)", Opt);
            return m.Success ? Regex.Replace(m.Groups[1].Value, @"\s+", " ").Trim() : null;
        }
    }
}
