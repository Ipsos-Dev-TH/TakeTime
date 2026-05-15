using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// Centralized accounting arithmetic validator.
    /// ตรวจสอบความถูกต้องทางบัญชีก่อนบันทึก/ส่งไประบบบัญชี
    ///
    /// กฎสำคัญ:
    ///   1. Total = Subtotal + VAT - WHT (สำหรับ payment voucher / supplier invoice)
    ///   2. Total = Subtotal + VAT (สำหรับ receipt / sales invoice)
    ///   3. VAT = Subtotal × Rate / 100 (proportional)
    ///   4. Net Payable = Gross Earnings - Deductions (สำหรับ payroll)
    ///   5. Sum(Debit) = Sum(Credit) (สำหรับ journal entry)
    ///   6. Sum(LineAmount) = Subtotal (สำหรับ multi-line documents)
    ///
    /// Tolerance: 0.01 บาท (รองรับ rounding error ของจุดทศนิยมที่ 2)
    /// </summary>
    public class AccountingArithmeticValidator
    {
        public const decimal DefaultToleranceBaht = 0.01m;
        private static readonly code _code = new code();

        /// <summary>ผลการตรวจสอบ — รวม IsValid + รายละเอียด discrepancy + suggested message</summary>
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public string Code { get; set; }            // เช่น "VAT_MISMATCH", "TOTAL_MISMATCH"
            public string ErrorMessage { get; set; }    // ข้อความภาษาไทยพร้อมแสดงผู้ใช้
            public decimal Expected { get; set; }
            public decimal Actual { get; set; }
            public decimal Discrepancy { get; set; }
            public bool IsBlocking { get; set; }        // true = ต้องแก้ไข, false = แค่เตือน

            public static ValidationResult Ok() => new ValidationResult { IsValid = true };

            public static ValidationResult Fail(string code, string message, decimal expected, decimal actual, bool blocking = true)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Code = code,
                    ErrorMessage = message,
                    Expected = expected,
                    Actual = actual,
                    Discrepancy = Math.Abs(expected - actual),
                    IsBlocking = blocking
                };
            }
        }

        // ──────────────────────────────────────────────
        // Core Validators
        // ──────────────────────────────────────────────

        /// <summary>
        /// ตรวจสอบ Supplier Invoice / Payment Voucher:
        ///   Net Payable = Subtotal (ex VAT) + VAT - WHT
        /// </summary>
        public static ValidationResult ValidateInvoiceTotal(
            decimal subtotal, decimal vatAmount, decimal whtAmount, decimal declaredTotal,
            decimal tolerance = DefaultToleranceBaht)
        {
            if (subtotal < 0)
                return ValidationResult.Fail("NEGATIVE_SUBTOTAL", "ยอดก่อนภาษีต้องไม่ติดลบ", 0, subtotal);
            if (vatAmount < 0)
                return ValidationResult.Fail("NEGATIVE_VAT", "ยอดภาษีมูลค่าเพิ่มต้องไม่ติดลบ", 0, vatAmount);
            if (whtAmount < 0)
                return ValidationResult.Fail("NEGATIVE_WHT", "ยอดภาษีหัก ณ ที่จ่ายต้องไม่ติดลบ", 0, whtAmount);

            decimal computed = subtotal + vatAmount - whtAmount;
            decimal diff = Math.Abs(computed - declaredTotal);

            if (diff > tolerance)
            {
                return ValidationResult.Fail("TOTAL_MISMATCH",
                    $"ยอดสุทธิไม่ตรง: ยอดก่อนภาษี ({subtotal:N2}) + VAT ({vatAmount:N2}) - หัก ณ ที่จ่าย ({whtAmount:N2}) = {computed:N2} แต่ระบุ {declaredTotal:N2} (ต่างกัน {diff:N2} บาท)",
                    computed, declaredTotal);
            }
            return ValidationResult.Ok();
        }

        /// <summary>
        /// ตรวจสอบ Sales Receipt / Tax Invoice:
        ///   Total (incl. VAT) = Subtotal (ex VAT) + VAT
        /// </summary>
        public static ValidationResult ValidateReceiptTotal(
            decimal subtotalExVat, decimal vatAmount, decimal declaredTotal,
            decimal tolerance = DefaultToleranceBaht)
        {
            if (subtotalExVat < 0)
                return ValidationResult.Fail("NEGATIVE_SUBTOTAL", "ยอดก่อนภาษีต้องไม่ติดลบ", 0, subtotalExVat);
            if (vatAmount < 0)
                return ValidationResult.Fail("NEGATIVE_VAT", "ยอดภาษีมูลค่าเพิ่มต้องไม่ติดลบ", 0, vatAmount);
            if (declaredTotal <= 0)
                return ValidationResult.Fail("NON_POSITIVE_TOTAL", "ยอดรวมต้องมากกว่า 0", 0, declaredTotal);

            decimal computed = subtotalExVat + vatAmount;
            decimal diff = Math.Abs(computed - declaredTotal);

            if (diff > tolerance)
            {
                return ValidationResult.Fail("TOTAL_MISMATCH",
                    $"ยอดรวมไม่ตรง: ยอดก่อนภาษี ({subtotalExVat:N2}) + VAT ({vatAmount:N2}) = {computed:N2} แต่ระบุ {declaredTotal:N2} (ต่างกัน {diff:N2} บาท)",
                    computed, declaredTotal);
            }
            return ValidationResult.Ok();
        }

        /// <summary>
        /// ตรวจ VAT calculation:
        ///   - VAT-Inclusive (ราคารวม VAT แล้ว): VAT = Total × 7 / 107
        ///   - VAT-Exclusive (ราคาก่อน VAT): VAT = Subtotal × 7 / 100
        /// </summary>
        public static ValidationResult ValidateVatCalculation(
            decimal baseAmount, decimal vatRatePercent, decimal declaredVat,
            bool isInclusive = false, decimal tolerance = DefaultToleranceBaht)
        {
            if (vatRatePercent < 0 || vatRatePercent > 100)
                return ValidationResult.Fail("INVALID_VAT_RATE", $"อัตรา VAT ไม่ถูกต้อง: {vatRatePercent}%", 7, vatRatePercent);

            decimal computed;
            if (isInclusive)
                computed = Math.Round(baseAmount * vatRatePercent / (100m + vatRatePercent), 2, MidpointRounding.AwayFromZero);
            else
                computed = Math.Round(baseAmount * vatRatePercent / 100m, 2, MidpointRounding.AwayFromZero);

            decimal diff = Math.Abs(computed - declaredVat);
            if (diff > tolerance)
            {
                return ValidationResult.Fail("VAT_MISMATCH",
                    $"ยอด VAT ไม่ตรง: คำนวณได้ {computed:N2} แต่ระบุ {declaredVat:N2} (ต่างกัน {diff:N2} บาท) — อัตรา {vatRatePercent}% บนยอด {baseAmount:N2} {(isInclusive ? "(VAT include)" : "(VAT exclude)")}",
                    computed, declaredVat);
            }
            return ValidationResult.Ok();
        }

        /// <summary>
        /// ตรวจ WHT calculation:
        ///   WHT = Subtotal (ex VAT) × Rate / 100
        /// คำนวณบนฐาน "ก่อน VAT" ตามมาตรฐานสรรพากร
        /// </summary>
        public static ValidationResult ValidateWhtCalculation(
            decimal subtotalExVat, decimal whtRatePercent, decimal declaredWht,
            decimal tolerance = DefaultToleranceBaht)
        {
            if (whtRatePercent < 0 || whtRatePercent > 30)
                return ValidationResult.Fail("INVALID_WHT_RATE", $"อัตรา WHT ไม่ถูกต้อง: {whtRatePercent}% (ปกติ 0-15%)", 0, whtRatePercent);

            decimal computed = Math.Round(subtotalExVat * whtRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
            decimal diff = Math.Abs(computed - declaredWht);
            if (diff > tolerance)
            {
                return ValidationResult.Fail("WHT_MISMATCH",
                    $"ยอดหัก ณ ที่จ่ายไม่ตรง: คำนวณได้ {computed:N2} แต่ระบุ {declaredWht:N2} (ต่างกัน {diff:N2} บาท) — อัตรา {whtRatePercent}% บนยอด {subtotalExVat:N2}",
                    computed, declaredWht);
            }
            return ValidationResult.Ok();
        }

        /// <summary>
        /// ตรวจ Sum(LineAmount) = Subtotal — สำหรับเอกสารหลายบรรทัด
        /// </summary>
        public static ValidationResult ValidateLineSum(
            IEnumerable<decimal> lineAmounts, decimal declaredSubtotal,
            decimal tolerance = DefaultToleranceBaht)
        {
            decimal sum = 0m;
            int count = 0;
            foreach (var amt in lineAmounts ?? new List<decimal>())
            {
                if (amt < 0)
                    return ValidationResult.Fail("NEGATIVE_LINE", $"พบรายการที่มียอดติดลบ: {amt:N2}", 0, amt);
                sum += amt;
                count++;
            }

            if (count == 0)
                return ValidationResult.Fail("NO_LINES", "ไม่มีรายการสินค้า/บริการในเอกสาร", 1, 0);

            decimal diff = Math.Abs(sum - declaredSubtotal);
            if (diff > tolerance)
            {
                return ValidationResult.Fail("LINE_SUM_MISMATCH",
                    $"ยอดรวมรายการไม่ตรง: ผลรวม {count} บรรทัด = {sum:N2} แต่ระบุยอดก่อนภาษี {declaredSubtotal:N2} (ต่างกัน {diff:N2} บาท)",
                    sum, declaredSubtotal);
            }
            return ValidationResult.Ok();
        }

        /// <summary>
        /// ตรวจ Double-entry: Sum(Debit) = Sum(Credit)
        /// ใช้ก่อนยิง journal entry ไประบบบัญชี
        /// </summary>
        public static ValidationResult ValidateJournalBalance(
            decimal totalDebit, decimal totalCredit,
            decimal tolerance = DefaultToleranceBaht)
        {
            if (totalDebit < 0)
                return ValidationResult.Fail("NEGATIVE_DEBIT", "ยอดเดบิตรวมต้องไม่ติดลบ", 0, totalDebit);
            if (totalCredit < 0)
                return ValidationResult.Fail("NEGATIVE_CREDIT", "ยอดเครดิตรวมต้องไม่ติดลบ", 0, totalCredit);
            if (totalDebit == 0 && totalCredit == 0)
                return ValidationResult.Fail("EMPTY_JOURNAL", "Journal entry ว่าง — ต้องมีอย่างน้อย 1 บรรทัด debit + 1 บรรทัด credit", 1, 0);

            decimal diff = Math.Abs(totalDebit - totalCredit);
            if (diff > tolerance)
            {
                return ValidationResult.Fail("DR_CR_IMBALANCE",
                    $"Journal entry ไม่สมดุล: ยอดเดบิต ({totalDebit:N2}) ≠ ยอดเครดิต ({totalCredit:N2}) (ต่างกัน {diff:N2} บาท)",
                    totalDebit, totalCredit);
            }
            return ValidationResult.Ok();
        }

        /// <summary>
        /// ตรวจ Payroll:
        ///   Net Payable = Gross Earnings - Total Deductions
        /// </summary>
        public static ValidationResult ValidatePayrollNet(
            decimal grossEarnings, decimal totalDeductions, decimal declaredNetPayable,
            decimal tolerance = DefaultToleranceBaht)
        {
            if (grossEarnings < 0)
                return ValidationResult.Fail("NEGATIVE_GROSS", "ยอดรายได้รวมต้องไม่ติดลบ", 0, grossEarnings);
            if (totalDeductions < 0)
                return ValidationResult.Fail("NEGATIVE_DEDUCTION", "ยอดหักรวมต้องไม่ติดลบ", 0, totalDeductions);
            if (totalDeductions > grossEarnings)
                return ValidationResult.Fail("DEDUCTION_EXCEEDS_GROSS",
                    $"ยอดหัก ({totalDeductions:N2}) มากกว่ารายได้รวม ({grossEarnings:N2}) — กรุณาตรวจสอบ",
                    grossEarnings, totalDeductions);

            decimal computed = grossEarnings - totalDeductions;
            decimal diff = Math.Abs(computed - declaredNetPayable);
            if (diff > tolerance)
            {
                return ValidationResult.Fail("NET_MISMATCH",
                    $"ยอดสุทธิจ่ายไม่ตรง: รายได้รวม ({grossEarnings:N2}) - หักรวม ({totalDeductions:N2}) = {computed:N2} แต่ระบุ {declaredNetPayable:N2} (ต่างกัน {diff:N2} บาท)",
                    computed, declaredNetPayable);
            }
            return ValidationResult.Ok();
        }

        /// <summary>
        /// ตรวจ SSF (Social Security Fund) ของพนักงาน — 5% ของฐานเงินเดือน เพดาน 750 บาท
        /// ตามกฎหมายไทย: ฐานสูงสุด 15,000 บาท × 5% = 750 บาท
        /// </summary>
        public static ValidationResult ValidateSsfDeduction(
            decimal salaryBase, decimal declaredSsf,
            decimal ssfRate = 5m, decimal ssfCap = 750m, decimal tolerance = DefaultToleranceBaht)
        {
            decimal computed = Math.Min(Math.Round(salaryBase * ssfRate / 100m, 2, MidpointRounding.AwayFromZero), ssfCap);
            decimal diff = Math.Abs(computed - declaredSsf);
            if (diff > tolerance)
            {
                return ValidationResult.Fail("SSF_MISMATCH",
                    $"ยอดประกันสังคมไม่ตรง: คำนวณได้ {computed:N2} ({ssfRate}% ของ {salaryBase:N2}, เพดาน {ssfCap:N2}) แต่ระบุ {declaredSsf:N2} (ต่างกัน {diff:N2} บาท)",
                    computed, declaredSsf);
            }
            return ValidationResult.Ok();
        }

        /// <summary>
        /// ตรวจว่ายอดเงินไม่เกินทศนิยม 2 หลัก (สำหรับสกุลบาท)
        /// </summary>
        public static ValidationResult ValidateDecimalPrecision(decimal amount, string fieldName = "ยอดเงิน")
        {
            decimal rounded = Math.Round(amount, 2);
            if (Math.Abs(amount - rounded) > 0.0001m)
            {
                return ValidationResult.Fail("PRECISION_ERROR",
                    $"{fieldName} มีทศนิยมเกิน 2 หลัก: {amount}",
                    rounded, amount);
            }
            return ValidationResult.Ok();
        }

        // ──────────────────────────────────────────────
        // Compound Validators (เรียกหลายตัวรวมกัน)
        // ──────────────────────────────────────────────

        /// <summary>
        /// ตรวจ Payment Voucher อย่างครบถ้วน:
        ///   1. ทุกบรรทัดไม่ติดลบ
        ///   2. Sum(Lines) = Subtotal
        ///   3. VAT คำนวณถูกต้อง (ถ้าระบุอัตรา)
        ///   4. WHT คำนวณถูกต้อง (ถ้าระบุอัตรา)
        ///   5. Net Payable = Subtotal + VAT - WHT
        /// </summary>
        public static List<ValidationResult> ValidatePaymentVoucher(
            IEnumerable<decimal> lineAmounts, decimal subtotal,
            decimal vatRate, decimal vatAmount,
            decimal whtRate, decimal whtAmount,
            decimal netPayable, bool isVatInclusive = false,
            decimal tolerance = DefaultToleranceBaht)
        {
            var results = new List<ValidationResult>();

            var lineCheck = ValidateLineSum(lineAmounts, subtotal, tolerance);
            if (!lineCheck.IsValid) results.Add(lineCheck);

            if (vatRate > 0)
            {
                var vatCheck = ValidateVatCalculation(subtotal, vatRate, vatAmount, isVatInclusive, tolerance);
                if (!vatCheck.IsValid) results.Add(vatCheck);
            }

            if (whtRate > 0)
            {
                var whtCheck = ValidateWhtCalculation(subtotal, whtRate, whtAmount, tolerance);
                if (!whtCheck.IsValid) results.Add(whtCheck);
            }

            var totalCheck = ValidateInvoiceTotal(subtotal, vatAmount, whtAmount, netPayable, tolerance);
            if (!totalCheck.IsValid) results.Add(totalCheck);

            return results;
        }

        // ──────────────────────────────────────────────
        // Audit Logging
        // ──────────────────────────────────────────────

        /// <summary>
        /// บันทึกผลการตรวจสอบลง Accounting_Validation_Log สำหรับการตรวจสอบย้อนหลัง
        /// ไม่ throw exception ถ้า log ไม่สำเร็จ
        /// </summary>
        public static void LogValidationFailure(string entityType, string entityId, ValidationResult result, string username = "SYSTEM")
        {
            if (result == null || result.IsValid) return;
            try
            {
                string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString;
                if (string.IsNullOrEmpty(conn)) return;

                _code.DatabaseInsertSafe(conn,
                    @"INSERT INTO Accounting_Validation_Log
                      (Entity_Type, Entity_ID, Validation_Code, Error_Message, Expected_Value, Actual_Value, Discrepancy, Is_Blocking, Created_By, Created_Date)
                      VALUES (@et, @eid, @code, @msg, @exp, @act, @diff, @blk, @usr, GETDATE())",
                    new Dictionary<string, object>
                    {
                        { "@et", entityType ?? (object)DBNull.Value },
                        { "@eid", entityId ?? (object)DBNull.Value },
                        { "@code", result.Code ?? "UNKNOWN" },
                        { "@msg", result.ErrorMessage ?? "" },
                        { "@exp", result.Expected },
                        { "@act", result.Actual },
                        { "@diff", result.Discrepancy },
                        { "@blk", result.IsBlocking ? 1 : 0 },
                        { "@usr", username ?? "SYSTEM" }
                    });
            }
            catch { /* logging is best-effort */ }
        }

        /// <summary>
        /// รวม error messages จาก list ของ ValidationResult — สำหรับแสดงผู้ใช้
        /// </summary>
        public static string FormatErrors(List<ValidationResult> results)
        {
            if (results == null || results.Count == 0) return "";
            var msgs = new List<string>();
            foreach (var r in results)
                if (r != null && !r.IsValid)
                    msgs.Add($"• {r.ErrorMessage}");
            return string.Join("\n", msgs);
        }

        /// <summary>
        /// คืน true ถ้ามี blocking error (ต้องแก้ก่อนบันทึก)
        /// </summary>
        public static bool HasBlockingError(List<ValidationResult> results)
        {
            if (results == null) return false;
            foreach (var r in results)
                if (r != null && !r.IsValid && r.IsBlocking) return true;
            return false;
        }
    }
}
