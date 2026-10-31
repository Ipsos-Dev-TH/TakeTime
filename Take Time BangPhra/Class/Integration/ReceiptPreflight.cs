using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Integration
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //  ตัวตรวจใบเสร็จก่อนส่งบัญชี (PHASE19_37 — CLAUDE.md ข้อ 21) — "กฎชุดเดียว" ใช้ทั้ง
    //    • หน้าบันทึก/แก้ใบเสร็จ + เช็คอิน (ก่อนเขียน DB)  • EnqueueReceipt  • worker (ProcessReceiptDocument)  • สแกนรายคืน
    //  ไฟล์นี้เป็นส่วน "บริสุทธิ์" (ไม่มี I/O) → ทดสอบได้นอก IIS; ส่วนอ่าน DB อยู่ใน ReceiptPreflight.Data.cs
    //
    //  โหมด Nexaacc_Preflight_Mode: OFF (ไม่ตรวจ — เดิมทุกอย่าง) · WARN (ค่าเริ่มต้นหลังรัน migration: หน้าเตือนแต่บันทึกได้,
    //  คิวพักเฉพาะปัญหาที่ส่งไปก็ล้มแน่ = HardFail) · BLOCK (หน้าบล็อกปัญหาข้อมูล — Owner/Admin บัญชี ยืนยันข้ามได้พร้อมเหตุผล,
    //  คิวพักทุกปัญหาระดับ Block). ปัญหาตั้งค่า (mapping/แหล่งเงิน) ไม่บล็อกหน้าร้านทุกโหมด — พักคิวแทน
    // ══════════════════════════════════════════════════════════════════════════════════════════

    public enum PreflightContext { PageSave, PageEdit, CheckIn, Enqueue, Worker, Scan }
    public enum PreflightSeverity { Info = 0, Warn = 1, Block = 2 }
    public enum PreflightMode { Off, Warn, Block }
    public enum ReceiptLineRole { Zero, Revenue, DepositApply, Discount, Adjustment }

    /// <summary>ชนิดบรรทัดติดลบ (Account_Receipt_Detail.Line_Kind) — NULL = กฎเดิม "ติดลบทุกบรรทัด = หักมัดจำ"</summary>
    public static class ReceiptLineKinds
    {
        public const string DepositDeduction = "DEPOSIT_DEDUCTION";
        public const string Discount = "DISCOUNT";
        public const string Adjustment = "ADJUSTMENT";

        public static string Normalize(string kind)
        {
            string k = (kind ?? "").Trim().ToUpperInvariant();
            switch (k)
            {
                case DepositDeduction:
                case "DEPOSIT_APPLY":
                case "DEPOSIT":
                    return DepositDeduction;
                case Discount:
                    return Discount;
                case Adjustment:
                case "PREPAID_ELSEWHERE":
                case "ROUNDING":
                    return Adjustment;
                default:
                    return null;
            }
        }

        public static string LabelTh(string kind)
        {
            switch (Normalize(kind))
            {
                case DepositDeduction: return "หักมัดจำ";
                case Discount: return "ส่วนลด";
                case Adjustment: return "ปรับยอด/ชำระที่อื่นแล้ว";
                default: return "ไม่ระบุ (ถือเป็นหักมัดจำ)";
            }
        }
    }

    /// <summary>
    /// ตัวจำแนกบรรทัดใบเสร็จ "ตัวเดียว" ที่ทั้ง worker (LookupReceiptLinesEx) และ preflight ใช้ — จะได้ไม่มีวันตัดสินต่างกัน
    /// </summary>
    public static class ReceiptLineClassifier
    {
        public static ReceiptLineRole Classify(decimal amount, string lineKind)
        {
            if (amount == 0m) return ReceiptLineRole.Zero;
            if (amount > 0m) return ReceiptLineRole.Revenue;
            switch (ReceiptLineKinds.Normalize(lineKind))
            {
                case ReceiptLineKinds.Discount: return ReceiptLineRole.Discount;
                case ReceiptLineKinds.Adjustment: return ReceiptLineRole.Adjustment;
                default: return ReceiptLineRole.DepositApply;   // DEPOSIT_DEDUCTION หรือไม่ระบุ (กฎเดิม)
            }
        }

        /// <summary>บรรทัดติดลบที่ไม่ได้ระบุชนิด (ถูกเดาเป็นหักมัดจำตามกฎเดิม)</summary>
        public static bool IsLegacyGuess(decimal amount, string lineKind)
        {
            return amount < 0m && ReceiptLineKinds.Normalize(lineKind) == null;
        }

        public static bool LooksLikeDiscount(string description)
        {
            return (description ?? "").TrimStart().StartsWith("ส่วนลด", StringComparison.Ordinal);
        }

        /// <summary>
        /// กระจาย "ส่วนลด/ปรับยอด" (ยอดบวก) ลงบรรทัดรายได้ตามสัดส่วน — ปัดเศษไปบรรทัดใหญ่สุด · คืนยอดที่กระจายได้จริง.
        /// ใช้ใน worker แทนการเดาว่าเป็นหักมัดจำ (บรรทัดที่ราคาต่อหน่วยกลายเป็นทศนิยมเกิน 2 หลัก → ยุบจำนวนเป็น 1)
        /// </summary>
        public static decimal ProrateReduction(IList<ProratableLine> lines, decimal reduction)
        {
            if (lines == null || lines.Count == 0 || reduction <= 0m) return 0m;
            decimal gross = lines.Sum(l => l.Amount > 0 ? l.Amount : 0m);
            if (gross <= 0m) return 0m;
            if (reduction > gross) reduction = gross;
            decimal applied = 0m;
            int biggest = 0;
            for (int i = 0; i < lines.Count; i++) if (lines[i].Amount > lines[biggest].Amount) biggest = i;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i == biggest || lines[i].Amount <= 0m) continue;
                decimal cut = Math.Round(reduction * lines[i].Amount / gross, 2, MidpointRounding.AwayFromZero);
                if (cut > lines[i].Amount) cut = lines[i].Amount;
                lines[i].Amount -= cut;
                applied += cut;
            }
            decimal rest = reduction - applied;
            if (rest > lines[biggest].Amount) rest = lines[biggest].Amount;
            lines[biggest].Amount -= rest;
            applied += rest;
            foreach (var l in lines)
            {
                if (l.Quantity <= 0m) l.Quantity = 1m;
                decimal unit = l.Amount / l.Quantity;
                if (Math.Round(unit, 2) != unit) { l.Quantity = 1m; }
            }
            return applied;
        }
    }

    /// <summary>บรรทัดที่ถูกกระจายส่วนลดได้ (adapter เล็ก ๆ — mapper มี ReceiptLineSpec ของตัวเอง)</summary>
    public sealed class ProratableLine
    {
        public decimal Quantity = 1m;
        public decimal Amount;
        public object Tag;
    }

    public sealed class ReceiptLineFact
    {
        public int Number;
        public int? ProductTypeId;
        public string Description;
        public decimal Quantity = 1m;
        public decimal UnitPrice;
        public decimal Amount;
        public string LineKind;
    }

    public sealed class DepositReceiptFact
    {
        public string ReceiptNumber;
        public decimal Total;
    }

    /// <summary>ข้อเท็จจริงของใบเสร็จ 1 ใบ (หรือร่างก่อนบันทึก) ที่กฎต้องใช้ — โหลดครั้งเดียว</summary>
    public sealed class ReceiptFacts
    {
        public string ReceiptNumber;
        public string OriginalReceiptNumber;
        public int ReservationId;
        public bool IsDeposit;
        public string Status;
        public decimal Total, Vat, ExclVat;
        /// <summary>false = ร่างจากหน้าเช็คอินที่ VAT/ยอดบรรทัดยังถูกคำนวณ/ปรับทีหลัง (ข้าม R01/R02)</summary>
        public bool HeaderVatKnown = true;
        public bool LinesWillBeRescaled;
        public DateTime Date = DateTime.Today;
        public DateTime Today = DateTime.Today;
        public bool UseVat;
        public List<ReceiptLineFact> Lines = new List<ReceiptLineFact>();
        public List<DepositReceiptFact> DepositReceipts = new List<DepositReceiptFact>();
        public decimal DepositAppliedByOtherReceipts;
        public decimal DepositAppliedColumn;
        public decimal ReservationTotal = -1m;
        public string PaidType;
        public bool PaidHowRowFound = true;
        public string PaidHowAccountId;
        public bool? PaidHowAccountOnNextAcc;
        public List<string> MissingRequiredMappings = new List<string>();
        public List<string> MissingOptionalMappings = new List<string>();
        public bool HasNextAccDoc;
        public string NextAccDocNumber;
        public string CreditNoteBlock;
        public bool? BuyerResolves;
        public bool EtaxRequested;
        public string BuyerTaxId, BuyerAddress;
        public string PeriodStatus;        // Open | Closed | Locked | null (ไม่รู้)
        public bool? VatFiled;
        public bool PeriodCacheFresh;
        public int VatFilingDay = 15;
        // ลิงก์ "แก้ที่นี่"
        public string EditUrl, CustomerUrl, CreditNoteUrl, MappingUrl, PaidHowUrl;
    }

    public sealed class PreflightIssue
    {
        public string Rule;
        public PreflightSeverity Severity;
        public SyncIssueClass Class;
        public string MessageTh;
        public string FixTh;
        public string FixKind;
        public string FixUrl;
        /// <summary>ส่งไป NextAcc ก็ล้มแน่ (หรือ worker โยนเองอยู่แล้ว) — พักคิวได้แม้โหมด WARN</summary>
        public bool HardFail;
        /// <summary>ความถูกต้องของเอกสารฝั่ง TakeTime เท่านั้น (worker คำนวณเองไม่พึ่งค่านี้) — บล็อกหน้าเว็บได้ แต่ไม่พักคิว</summary>
        public bool PageOnly;

        public override string ToString()
        {
            return "[" + Rule + "] " + MessageTh + (string.IsNullOrEmpty(FixTh) ? "" : " → " + FixTh);
        }
    }

    public sealed class PreflightResult
    {
        public List<PreflightIssue> Issues = new List<PreflightIssue>();

        public bool HasAny { get { return Issues.Count > 0; } }

        public IEnumerable<PreflightIssue> At(PreflightSeverity sev) { return Issues.Where(i => i.Severity == sev); }

        /// <summary>ปัญหาที่หน้าเว็บต้องหยุด (โหมด BLOCK) — ข้อมูล/นโยบาย เท่านั้น ตั้งค่าไม่หยุดหน้าร้าน</summary>
        public List<PreflightIssue> PageBlocking()
        {
            return Issues.Where(i => i.Severity == PreflightSeverity.Block
                && (i.Class == SyncIssueClass.Data || i.Class == SyncIssueClass.Policy)).ToList();
        }

        /// <summary>แถวคิวควรพักไหม (OFF ไม่พัก · WARN พักเฉพาะ HardFail · BLOCK พักทุก Block)</summary>
        public PreflightIssue QueueHoldIssue(PreflightMode mode)
        {
            if (mode == PreflightMode.Off) return null;
            var blocks = Issues.Where(i => i.Severity == PreflightSeverity.Block && !i.PageOnly
                && (mode == PreflightMode.Block || i.HardFail)).ToList();
            if (blocks.Count == 0) return null;
            // ลำดับความสำคัญ: นโยบาย > ข้อมูล > ตั้งค่า (คนต้องแก้ข้อมูลก่อน mapping จะช่วย)
            return blocks.FirstOrDefault(i => i.Class == SyncIssueClass.Policy)
                ?? blocks.FirstOrDefault(i => i.Class == SyncIssueClass.Data)
                ?? blocks[0];
        }

        public string ToText(int max = 8, PreflightSeverity minSeverity = PreflightSeverity.Warn)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var i in Issues.Where(x => x.Severity >= minSeverity)
                                    .OrderByDescending(x => x.Severity).ThenBy(x => x.Rule, StringComparer.Ordinal))
            {
                if (n++ >= max) { sb.Append("\n…"); break; }
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(i.Severity == PreflightSeverity.Block ? "⛔ " : (i.Severity == PreflightSeverity.Warn ? "⚠ " : "ℹ "));
                sb.Append(i.ToString());
            }
            return sb.ToString();
        }

        public string RulesCsv()
        {
            return string.Join(",", Issues.Where(i => i.Severity >= PreflightSeverity.Warn).Select(i => i.Rule).Distinct());
        }
    }

    /// <summary>ผลตัดสินฝั่งหน้าเว็บ</summary>
    public sealed class PreflightDecision
    {
        public bool Proceed = true;
        public bool Overridden;
        public bool NeedsOverride;
        public bool CanOverride;
        public string Message;   // ข้อความที่ต้องแสดง (ว่าง = ไม่มีอะไร)
    }

    public static partial class ReceiptPreflight
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Regex DepositRefRx = new Regex(@"\[(REC[0-9A-Za-z\-_]+)\]", RegexOptions.Compiled);

        public static PreflightMode ParseMode(string s)
        {
            switch ((s ?? "").Trim().ToUpperInvariant())
            {
                case "WARN": return PreflightMode.Warn;
                case "BLOCK": return PreflightMode.Block;
                default: return PreflightMode.Off;
            }
        }

        /// <summary>เลขประจำตัวผู้เสียภาษี 13 หลัก + check digit (mod 11)</summary>
        public static bool IsValidThaiTaxId(string taxId)
        {
            string t = new string((taxId ?? "").Where(char.IsDigit).ToArray());
            if (t.Length != 13) return false;
            int sum = 0;
            for (int i = 0; i < 12; i++) sum += (t[i] - '0') * (13 - i);
            int chk = (11 - (sum % 11)) % 10;
            return chk == t[12] - '0';
        }

        private static string N(decimal d) { return d.ToString("N2", Inv); }

        /// <summary>
        /// กฎทั้งหมด (บริสุทธิ์ — ไม่มี I/O). ctx กำหนดว่ากฎไหนใช้ได้ (เช่น R11/R20 เฉพาะการแก้ใบ)
        /// </summary>
        public static PreflightResult Evaluate(ReceiptFacts f, PreflightContext ctx)
        {
            var r = new PreflightResult();
            if (f == null) return r;
            Action<string, PreflightSeverity, SyncIssueClass, string, string, string, string, bool> add =
                (rule, sev, cls, msg, fix, kind, url, hard) => r.Issues.Add(new PreflightIssue
                { Rule = rule, Severity = sev, Class = cls, MessageTh = msg, FixTh = fix, FixKind = kind, FixUrl = url, HardFail = hard });

            var lines = f.Lines ?? new List<ReceiptLineFact>();
            decimal sumAll = lines.Sum(l => l.Amount);
            decimal positive = lines.Where(l => l.Amount > 0).Sum(l => l.Amount);
            var depLines = lines.Where(l => ReceiptLineClassifier.Classify(l.Amount, l.LineKind) == ReceiptLineRole.DepositApply).ToList();
            decimal depFromLines = -depLines.Sum(l => l.Amount);
            bool hasLines = lines.Any(l => l.Amount != 0m);
            bool edit = ctx == PreflightContext.PageEdit;
            string editUrl = f.EditUrl;

            // ── R03 ยอดใบ ≤ 0 ─────────────────────────────────────────────────────
            if (f.Total <= 0m)
            {
                if (!f.IsDeposit && depFromLines > 0.005m)
                    add("R03", PreflightSeverity.Warn, SyncIssueClass.Data,
                        "ยอดใบ " + N(f.Total) + " (มัดจำครอบคลุมทั้งหมด) — ใบยอด 0 จะไม่ถูกส่งบัญชี",
                        "มัดจำถูกตัดผ่านรายการเช็คเอาท์แยก (ตัดเงินรับล่วงหน้า) — ตรวจว่ามีรายการนั้นในคิว", "EDIT_RECEIPT", editUrl, false);
                else
                    add("R03", PreflightSeverity.Block, SyncIssueClass.Data,
                        "ยอดใบ " + N(f.Total) + " ≤ 0 — ระบบจะไม่ส่งใบนี้เข้าบัญชีเลย (รายได้/มัดจำจะไม่ถูกบันทึก)",
                        "แก้ยอดใบให้ถูกต้อง หรือยกเลิกใบ", "EDIT_RECEIPT", editUrl, true);
            }

            // ── R01 ผลรวมบรรทัด = ยอดหัวใบ ─────────────────────────────────────────
            if (hasLines && !f.LinesWillBeRescaled && Math.Abs(sumAll - f.Total) > 0.05m)
                add("R01", PreflightSeverity.Block, SyncIssueClass.Data,
                    "รายการรวม " + N(sumAll) + " ≠ ยอดใบ " + N(f.Total) + " (ต่าง " + N(sumAll - f.Total) + ")",
                    "แก้บรรทัดรายการหรือยอดรวมให้ตรงกัน", "EDIT_RECEIPT", editUrl, false);

            // ── R02 VAT ────────────────────────────────────────────────────────────
            if (f.HeaderVatKnown && f.Total > 0m)
            {
                if (f.UseVat)
                {
                    decimal exp = Math.Round(f.Total - f.Total / 1.07m, 2, MidpointRounding.AwayFromZero);
                    if (Math.Abs(f.Vat - exp) > 0.02m || Math.Abs(f.ExclVat + f.Vat - f.Total) > 0.02m)
                        add("R02", PreflightSeverity.Block, SyncIssueClass.Data,
                            "VAT ในใบ " + N(f.Vat) + " / ก่อน VAT " + N(f.ExclVat) + " ไม่ตรงกับยอด " + N(f.Total)
                            + " (ควรเป็น VAT " + N(exp) + " / ก่อน VAT " + N(f.Total - exp) + ")",
                            "คำนวณ VAT ใหม่จากยอดรวม (รวม VAT 7%)", "EDIT_RECEIPT", editUrl, false);
                }
                else if (f.Vat > 0.005m)
                    add("R02", PreflightSeverity.Block, SyncIssueClass.Data,
                        "กิจการไม่ได้จด VAT แต่ใบมี VAT " + N(f.Vat), "ตั้ง VAT = 0", "EDIT_RECEIPT", editUrl, false);
            }

            if (!f.IsDeposit)
            {
                // ── R04 หักมัดจำ ≤ มัดจำที่รับจริง (เคส #149366: หัก 4,700 vs มัดจำ 1,500) ─────────────
                decimal d = depFromLines;
                string src = "บรรทัดติดลบในใบ";
                if (d <= 0.005m && f.DepositAppliedColumn > 0.005m)
                {
                    if (f.DepositAppliedColumn > positive + 0.01m && positive > 0m)
                        add("R04", PreflightSeverity.Info, SyncIssueClass.Data,
                            "ยอดหักมัดจำที่เก็บไว้ " + N(f.DepositAppliedColumn) + " มากกว่ามูลค่าทั้งใบ " + N(positive)
                            + " และใบไม่มีบรรทัดหักมัดจำแล้ว — ค่าค้างจากรุ่นก่อนแก้ใบ ระบบจะใช้ 0", null, "EDIT_RECEIPT", editUrl, false);
                    else { d = f.DepositAppliedColumn; src = "คอลัมน์ยอดหักมัดจำของใบ"; }
                }
                decimal received = (f.DepositReceipts ?? new List<DepositReceiptFact>()).Sum(x => x.Total);
                bool r04Failed = false;
                if (d > 0.005m)
                {
                    string depList = f.DepositReceipts == null || f.DepositReceipts.Count == 0 ? "ไม่มี"
                        : string.Join(", ", f.DepositReceipts.Select(x => x.ReceiptNumber + " " + N(x.Total)));
                    string negList = string.Join(", ", lines.Where(l => l.Amount < 0)
                        .Select(l => "\"" + Short(l.Description) + "\" " + N(l.Amount) + " (" + ReceiptLineKinds.LabelTh(l.LineKind) + ")"));
                    if (received <= 0.005m)
                        add("R04", PreflightSeverity.Warn, SyncIssueClass.Data,
                            "มียอดหัก " + N(d) + " (" + src + ") แต่การจองไม่มีใบมัดจำ — ระบบจะบันทึกเฉพาะยอดสุทธิ (ถือว่าจ่ายล่วงหน้าที่อื่น/OTA)",
                            "ถ้าเป็นส่วนลดจริง ให้ระบุชนิดบรรทัดเป็น \"ส่วนลด\"", "EDIT_RECEIPT", editUrl, false);
                    else
                    {
                        decimal avail = received - f.DepositAppliedByOtherReceipts;
                        if (d > received + 0.01m)
                        {
                            r04Failed = true;
                            add("R04", PreflightSeverity.Block, SyncIssueClass.Data,
                                "หักมัดจำ " + N(d) + " เกินมัดจำที่รับจริง " + N(received) + " (เกิน " + N(d - received) + ") · ใบมัดจำ: " + depList
                                + (negList.Length > 0 ? " · บรรทัดติดลบ: " + negList : " · ที่มา: " + src),
                                "แก้ใบ: บรรทัดที่ไม่ใช่มัดจำ (ส่วนลด/จ่ายที่อื่น) ให้เลือกชนิด \"ส่วนลด\" หรือ \"ปรับยอด\" หรือลบบรรทัดที่ซ้ำ",
                                "EDIT_RECEIPT", editUrl, true);
                        }
                        else if (d > avail + 0.01m)
                        {
                            r04Failed = true;
                            add("R04", PreflightSeverity.Block, SyncIssueClass.Data,
                                "หักมัดจำ " + N(d) + " แต่มัดจำคงเหลือ " + N(avail) + " (รับ " + N(received) + " ถูกหักในใบอื่นแล้ว "
                                + N(f.DepositAppliedByOtherReceipts) + ")",
                                "ตรวจใบเสร็จอื่นของการจองนี้ที่หักมัดจำซ้ำ", "EDIT_RECEIPT", editUrl, false);
                        }
                    }
                }

                // ── R05 บรรทัด "ส่วนลด" ติดลบที่ไม่ได้ระบุชนิด → worker จะนับเป็นหักมัดจำ ─────────────
                foreach (var l in lines.Where(x => ReceiptLineClassifier.IsLegacyGuess(x.Amount, x.LineKind)
                                                   && ReceiptLineClassifier.LooksLikeDiscount(x.Description)))
                    add("R05", r04Failed ? PreflightSeverity.Block : PreflightSeverity.Warn, SyncIssueClass.Data,
                        "บรรทัด \"" + Short(l.Description) + "\" " + N(l.Amount) + " ไม่ได้ระบุชนิด — ระบบบัญชีจะนับเป็น \"หักมัดจำ\"",
                        "เลือกชนิดบรรทัด (ส่วนลด / ปรับยอด / หักมัดจำ) ในหน้าแก้ใบ", "EDIT_RECEIPT", editUrl, false);

                // ── R06 บรรทัดหักมัดจำที่คัดลอกจากใบมัดจำ ต้องไม่เกินยอดใบมัดจำนั้น (บั๊กคัดลอกบรรทัดแรกซ้ำ) ────
                var byRef = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                foreach (var l in depLines)
                {
                    var m = DepositRefRx.Match(l.Description ?? "");
                    if (!m.Success) continue;
                    decimal cur;
                    byRef.TryGetValue(m.Groups[1].Value, out cur);
                    byRef[m.Groups[1].Value] = cur - l.Amount;
                }
                foreach (var kv in byRef)
                {
                    var dep = (f.DepositReceipts ?? new List<DepositReceiptFact>())
                        .FirstOrDefault(x => string.Equals(x.ReceiptNumber, kv.Key, StringComparison.OrdinalIgnoreCase));
                    if (dep != null && kv.Value > dep.Total + 0.01m)
                        add("R06", PreflightSeverity.Block, SyncIssueClass.Data,
                            "บรรทัดหักมัดจำของใบ " + kv.Key + " รวม " + N(kv.Value) + " เกินยอดใบมัดจำ " + N(dep.Total) + " (บรรทัดถูกคัดลอกซ้ำ)",
                            "ลบบรรทัดหักมัดจำที่ซ้ำ", "EDIT_RECEIPT", editUrl, false);
                }
            }
            else
            {
                // ── R07 ใบมัดจำ ──────────────────────────────────────────────────
                if (lines.Any(l => l.Amount < 0))
                    add("R07", PreflightSeverity.Block, SyncIssueClass.Data,
                        "ใบมัดจำมีบรรทัดติดลบ — ใบมัดจำต้องเป็นยอดรับเงินล้วน", "ลบบรรทัดติดลบ หรือเปลี่ยนเป็นใบเสร็จปกติ",
                        "EDIT_RECEIPT", editUrl, false);
                if (f.ReservationTotal > 0m && f.Total > f.ReservationTotal + 0.01m)
                    add("R07", PreflightSeverity.Block, SyncIssueClass.Data,
                        "ใบมัดจำ " + N(f.Total) + " มากกว่ายอดรวมการจอง " + N(f.ReservationTotal),
                        "ตรวจยอด — ชำระเต็มควรออกเป็นใบเสร็จ/ใบกำกับ ไม่ใช่มัดจำ", "EDIT_RECEIPT", editUrl, false);
            }

            // ── R08 ข้อมูลผู้ซื้อใบกำกับเต็มรูป (§86/4) ────────────────────────────────
            if (!f.IsDeposit)
            {
                bool taxOk = IsValidThaiTaxId(f.BuyerTaxId);
                bool hasTax = !string.IsNullOrWhiteSpace(f.BuyerTaxId);
                if (f.EtaxRequested && (!taxOk || string.IsNullOrWhiteSpace(f.BuyerAddress)))
                    add("R08", PreflightSeverity.Block, SyncIssueClass.Data,
                        "ขอ e-Tax/ใบกำกับเต็มรูป แต่ข้อมูลผู้ซื้อไม่ครบ (" + (!taxOk ? "เลขผู้เสียภาษี 13 หลักไม่ถูกต้อง" : "") +
                        (!taxOk && string.IsNullOrWhiteSpace(f.BuyerAddress) ? ", " : "") +
                        (string.IsNullOrWhiteSpace(f.BuyerAddress) ? "ไม่มีที่อยู่" : "") + ")",
                        "เติมเลขผู้เสียภาษี + ที่อยู่ + สาขา (สำนักงานใหญ่ 00000) ของผู้ซื้อ", "EDIT_CUSTOMER", f.CustomerUrl ?? editUrl, false);
                else if (hasTax && !taxOk)
                    add("R08", PreflightSeverity.Warn, SyncIssueClass.Data,
                        "เลขผู้เสียภาษีผู้ซื้อ \"" + f.BuyerTaxId + "\" ไม่ผ่านการตรวจหลัก — NextAcc จะออกเป็นใบเสร็จ/อย่างย่อแทนใบเต็มรูป",
                        "ตรวจเลขผู้เสียภาษี", "EDIT_CUSTOMER", f.CustomerUrl ?? editUrl, false);
            }

            // ── R09 แหล่งเงินผูกบัญชี NextAcc ──────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(f.PaidType))
            {
                if (!f.PaidHowRowFound)
                    add("R09", PreflightSeverity.Warn, SyncIssueClass.Config,
                        "ไม่พบแหล่งเงิน \"" + f.PaidType + "\" ที่เปิดใช้งาน — NextAcc จะเดาบัญชีเงินสด/ธนาคารเอง",
                        "ตรวจรายการแหล่งเงิน", "PAIDHOW", f.PaidHowUrl, false);
                else if (string.IsNullOrWhiteSpace(f.PaidHowAccountId))
                    add("R09", PreflightSeverity.Block, SyncIssueClass.Config,
                        "แหล่งเงิน \"" + f.PaidType + "\" ยังไม่ได้ผูกบัญชี NextAcc — เงินจะเข้าบัญชีที่ NextAcc เดาเอง",
                        "ผูกบัญชีที่หัวข้อ 'แหล่งเงิน' หน้า Accounting Integration", "PAIDHOW", f.PaidHowUrl, false);
                else if (f.PaidHowAccountOnNextAcc == false)
                    add("R09", PreflightSeverity.Block, SyncIssueClass.Config,
                        "แหล่งเงิน \"" + f.PaidType + "\" ผูกกับบัญชีที่ไม่อยู่ในผังบัญชี NextAcc (หรือปิดใช้งาน)",
                        "กด Sync บัญชี แล้วผูกใหม่", "PAIDHOW", f.PaidHowUrl, false);
            }

            // ── R10 mapping ที่ต้องใช้ ─────────────────────────────────────────────
            foreach (string k in (f.MissingRequiredMappings ?? new List<string>()).Distinct())
                add("R10", PreflightSeverity.Block, SyncIssueClass.Config,
                    "ยังไม่ได้ผูกบัญชี NextAcc ให้รหัส " + k + " (ใช้กับบรรทัดในใบนี้)",
                    "ตั้ง mapping " + k + " หน้า Accounting Integration → ผังบัญชี/Mapping", "MAPPING",
                    f.MappingUrl == null ? null : f.MappingUrl + "&key=" + Uri.EscapeDataString(k), true);
            foreach (string k in (f.MissingOptionalMappings ?? new List<string>()).Distinct())
                add("R10", PreflightSeverity.Warn, SyncIssueClass.Config,
                    "ยังไม่ได้ผูกบัญชี NextAcc ให้รหัส " + k, "ตั้ง mapping " + k, "MAPPING",
                    f.MappingUrl == null ? null : f.MappingUrl + "&key=" + Uri.EscapeDataString(k), false);

            // ── R11 / R20 แก้ใบที่มีเอกสาร NextAcc แล้ว ──────────────────────────────
            if (edit || ctx == PreflightContext.Enqueue)
            {
                if (edit && !string.IsNullOrEmpty(f.CreditNoteBlock))
                    add("R11", PreflightSeverity.Block, SyncIssueClass.Policy, f.CreditNoteBlock,
                        "ออกใบลดหนี้ที่หน้า การเงิน & บัญชี → ใบลดหนี้", "CREDIT_NOTE", f.CreditNoteUrl, true);
                else if (edit && f.HasNextAccDoc)
                    add("R11", PreflightSeverity.Info, SyncIssueClass.Policy,
                        "ใบนี้มีเอกสารบน NextAcc แล้ว" + (string.IsNullOrEmpty(f.NextAccDocNumber) ? "" : " (" + f.NextAccDocNumber + ")")
                        + " — บันทึกแล้วระบบจะยกเลิกเอกสารเดิมและออกใหม่ให้ตรง", null, "RESYNC", null, false);
                if (edit && f.HasNextAccDoc && !string.IsNullOrEmpty(f.OriginalReceiptNumber)
                    && !string.Equals(f.OriginalReceiptNumber, f.ReceiptNumber, StringComparison.OrdinalIgnoreCase))
                    add("R20", PreflightSeverity.Block, SyncIssueClass.Policy,
                        "เปลี่ยนเลขใบ " + f.OriginalReceiptNumber + " → " + f.ReceiptNumber + " หลังมีเอกสาร NextAcc แล้ว — เลขสองฝั่งจะไม่ตรงกัน",
                        "คงเลขเดิม หรือออกใบลดหนี้แล้วออกใบใหม่", "CREDIT_NOTE", f.CreditNoteUrl, false);
            }

            // ── R12 งวดบัญชี/เดือนภาษี ───────────────────────────────────────────────
            string ym = f.Date.ToString("MM/yyyy", Inv);
            if (f.PeriodCacheFresh && (string.Equals(f.PeriodStatus, "Closed", StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(f.PeriodStatus, "Locked", StringComparison.OrdinalIgnoreCase)))
                add("R12", PreflightSeverity.Block, SyncIssueClass.Policy,
                    "วันที่ใบ " + f.Date.ToString("dd/MM/yyyy", Inv) + " อยู่ในงวดบัญชีที่ปิดแล้วบน NextAcc (" + ym + ")",
                    "ใช้วันที่ในงวดที่เปิด หรือขอผู้ทำบัญชีเปิดงวด / ปรับผ่านใบลดหนี้เดือนปัจจุบัน", "CREDIT_NOTE", f.CreditNoteUrl, true);
            else if (edit && f.HasNextAccDoc)
            {
                if (f.PeriodCacheFresh && f.VatFiled == true)
                    add("R12", PreflightSeverity.Block, SyncIssueClass.Policy,
                        "เดือนภาษี " + ym + " ยื่น ภ.พ.30 แล้ว — แก้/ยกเลิกเอกสารเดิมบน NextAcc ไม่ได้",
                        "ออกใบลดหนี้/เพิ่มหนี้ในเดือนปัจจุบันแทน", "CREDIT_NOTE", f.CreditNoteUrl, true);
                else if (!f.PeriodCacheFresh && LikelyVatFiled(f.Date, f.Today, f.VatFilingDay))
                    add("R12", PreflightSeverity.Warn, SyncIssueClass.Policy,
                        "เดือนภาษี " + ym + " น่าจะยื่น ภ.พ.30 แล้ว (เลยวันที่ " + f.VatFilingDay + " ของเดือนถัดไป) — NextAcc อาจปฏิเสธการแก้",
                        "ถ้าระบบปฏิเสธ ให้ออกใบลดหนี้แทน", "CREDIT_NOTE", f.CreditNoteUrl, false);
            }
            if (f.Date.Date > f.Today.Date.AddDays(1))
                add("R12", PreflightSeverity.Warn, SyncIssueClass.Data,
                    "วันที่ใบ " + f.Date.ToString("dd/MM/yyyy", Inv) + " อยู่ในอนาคต", "ตรวจวันที่เอกสาร", "EDIT_RECEIPT", editUrl, false);

            // ── R16 ทศนิยมบรรทัด (เส้น integration: ราคาต่อหน่วย ≤ 2 หลัก) ─────────────────
            foreach (var l in lines.Where(x => x.Amount > 0))
            {
                decimal q = l.Quantity <= 0 ? 1m : l.Quantity;
                decimal unit = l.Amount / q;
                if (Math.Round(q, 4) != q || Math.Round(unit, 2) != unit)
                {
                    add("R16", PreflightSeverity.Warn, SyncIssueClass.Data,
                        "บรรทัด \"" + Short(l.Description) + "\" จำนวน " + q.ToString("0.####", Inv) + " × ราคา ได้ทศนิยมเกิน 2 หลัก (" + N(l.Amount) + ")",
                        "ใช้จำนวน 1 หรือราคาที่หารลงตัว", "EDIT_RECEIPT", editUrl, false);
                    break;
                }
            }

            // ── R17 ผู้ซื้อของใบหาเจอ ────────────────────────────────────────────────
            if (f.BuyerResolves == false && !f.IsDeposit)
                add("R17", PreflightSeverity.Warn, SyncIssueClass.Data,
                    "หาผู้ซื้อของใบไม่เจอ (Customer_ID ว่าง/ถูกลบ) — เอกสารจะออกในนามผู้จอง",
                    "เลือก/บันทึกผู้ซื้อในหน้าใบเสร็จ", "EDIT_RECEIPT", editUrl, false);

            // R02 (VAT หัวใบ) / R07 (ใบมัดจำ) = ความถูกต้องของเอกสารฝั่ง TakeTime — worker คำนวณ VAT/มัดจำเองจากกิจการ+บรรทัด
            foreach (var i in r.Issues.Where(x => x.Rule == "R02" || x.Rule == "R07")) i.PageOnly = true;
            return r;
        }

        /// <summary>
        /// การแก้ใบที่ "มีนัยทางบัญชี" — ต้องใส่เหตุผล (P1 edit safety): หักมัดจำเปลี่ยน/ถูกลบ, ยอดรวมเปลี่ยน &gt; 1 บาท,
        /// วันที่ย้ายเดือน, แหล่งเงินเปลี่ยน, เปลี่ยนเลขใบ, ผู้ซื้อเปลี่ยนหลังมีเอกสาร NextAcc
        /// </summary>
        public static List<string> MaterialEditChanges(ReceiptFacts before, ReceiptFacts after, long beforeCustomerId, long afterCustomerId)
        {
            var list = new List<string>();
            if (before == null || after == null) return list;
            Func<ReceiptFacts, decimal> dep = x => -(x.Lines ?? new List<ReceiptLineFact>())
                .Where(l => ReceiptLineClassifier.Classify(l.Amount, l.LineKind) == ReceiptLineRole.DepositApply).Sum(l => l.Amount);
            Func<ReceiptFacts, int> depCount = x => (x.Lines ?? new List<ReceiptLineFact>())
                .Count(l => ReceiptLineClassifier.Classify(l.Amount, l.LineKind) == ReceiptLineRole.DepositApply);
            decimal db = dep(before), da = dep(after);
            if (Math.Abs(db - da) > 0.01m || depCount(before) != depCount(after))
                list.Add("ยอดหักมัดจำ " + N(db) + " → " + N(da));
            if (Math.Abs(before.Total - after.Total) > 1m)
                list.Add("ยอดรวม " + N(before.Total) + " → " + N(after.Total));
            if (before.Date.Year != after.Date.Year || before.Date.Month != after.Date.Month)
                list.Add("วันที่ย้ายเดือน " + before.Date.ToString("dd/MM/yyyy", Inv) + " → " + after.Date.ToString("dd/MM/yyyy", Inv));
            if (!string.Equals((before.PaidType ?? "").Trim(), (after.PaidType ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                list.Add("แหล่งเงิน \"" + before.PaidType + "\" → \"" + after.PaidType + "\"");
            if (!string.IsNullOrEmpty(after.ReceiptNumber) && !string.Equals(before.ReceiptNumber, after.ReceiptNumber, StringComparison.OrdinalIgnoreCase))
                list.Add("เลขใบ " + before.ReceiptNumber + " → " + after.ReceiptNumber);
            if (before.HasNextAccDoc && beforeCustomerId > 0 && afterCustomerId > 0 && beforeCustomerId != afterCustomerId)
                list.Add("ผู้ซื้อเปลี่ยน (มีเอกสาร NextAcc แล้ว)");
            if (before.IsDeposit != after.IsDeposit)
                list.Add("ชนิดใบ " + (before.IsDeposit ? "มัดจำ" : "ใบเสร็จ") + " → " + (after.IsDeposit ? "มัดจำ" : "ใบเสร็จ"));
            return list;
        }

        /// <summary>เดือนของวันที่เอกสารน่าจะยื่น ภ.พ.30 แล้ว: เดือนก่อนหน้าที่เลยวันกำหนดยื่น (default 15) หรือเก่ากว่านั้น</summary>
        public static bool LikelyVatFiled(DateTime docDate, DateTime today, int filingDay)
        {
            if (filingDay < 1 || filingDay > 28) filingDay = 15;
            var docMonth = new DateTime(docDate.Year, docDate.Month, 1);
            var deadline = docMonth.AddMonths(1).AddDays(filingDay - 1);
            return today.Date > deadline.Date;
        }

        private static string Short(string s)
        {
            s = (s ?? "").Trim();
            return s.Length > 40 ? s.Substring(0, 40) + "…" : s;
        }

        /// <summary>
        /// ตัดสินฝั่งหน้าเว็บ: OFF/ไม่มีปัญหาบล็อก/WARN → บันทึกได้ (คืนข้อความเตือน) · BLOCK → หยุด เว้นแต่ผู้มีสิทธิ์
        /// (Owner หรือ Admin บัญชี) ใส่เหตุผล ≥ 10 ตัวอักษร (ยืนยันข้ามได้เฉพาะปัญหาข้อมูล — ปัญหานโยบาย เช่น งวดปิด/มีใบลดหนี้ ข้ามไม่ได้)
        /// </summary>
        public static PreflightDecision DecidePageSave(PreflightResult r, PreflightMode mode, bool canOverride, string overrideReason)
        {
            var d = new PreflightDecision { CanOverride = canOverride };
            if (r == null || mode == PreflightMode.Off) return d;
            string text = r.ToText();
            var blocking = r.PageBlocking();
            if (mode == PreflightMode.Warn || blocking.Count == 0)
            {
                d.Message = text;
                return d;
            }
            bool policy = blocking.Any(i => i.Class == SyncIssueClass.Policy);
            string reason = (overrideReason ?? "").Trim();
            if (!policy && canOverride && reason.Length >= 10)
            {
                d.Overridden = true;
                d.Message = text;
                return d;
            }
            d.Proceed = false;
            d.NeedsOverride = !policy;
            d.Message = "ตรวจพบปัญหาที่จะทำให้ส่งบัญชีไม่ผ่าน — ยังไม่ได้บันทึก\n\n" + text
                + (policy ? "\n\nปัญหาข้างต้นข้ามไม่ได้ (นโยบายบัญชี/ภาษี) — ใช้ใบลดหนี้หรือแก้ข้อมูล"
                   : canOverride ? "\n\nถ้าแน่ใจว่าถูกต้อง ให้ใส่เหตุผล (อย่างน้อย 10 ตัวอักษร) เพื่อยืนยันข้าม — ระบบบันทึกผู้ยืนยันไว้"
                   : "\n\nให้ Owner หรือ Admin ที่มีสิทธิ์บัญชี เป็นผู้ยืนยันข้าม (พร้อมเหตุผล)");
            return d;
        }
    }
}
