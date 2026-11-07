using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// ตัวช่วยของหน้า <c>Account/PaymentVoucher</c> (CLAUDE.md ข้อ 3b) — ไม่มี System.Web/DB (mono test ได้)
    ///
    /// <para><b>สองตัวเลือกแทนชุด dropdown ซ้อน</b>: "จ่ายจาก" (<c>ddlPayFrom</c>) และ "หมวดค่าใช้จ่าย" ต่อบรรทัด
    /// (<c>ddlLineAccount</c>) — ค่าในตัวเลือกใหม่ถูกแปลงกลับเป็นค่าของ control เดิม (DropDownList2 + ddlPaidHowNexaacc /
    /// ddlLineCategory + ddlLineChargeNexaacc) ก่อนโค้ดบันทึกเดิมทำงาน ⇒ สิ่งที่เขียนลง Account_Payment(_Detail) และ payload
    /// คิว <c>EnqueuePaymentVoucher</c> เหมือนเดิมทุกตัว</para>
    ///   • "PH:{Account_Paid_How.ID}" = แหล่งเงินในระบบ (บัญชี NextAcc ตาม mapping ของแถวนั้น — override ว่าง)
    ///   • "NA:{Nexaacc_AccountId}"   = บัญชี NextAcc ที่ไม่มีแถวแหล่งเงินผูกไว้ → override = GUID นี้ + ผู้ใช้เลือก
    ///                                  "บันทึกในระบบเป็น" (DropDownList2) เอง (ข้อความ Paid_How ใช้ตัดสินจ่ายเงินสด/ธนาคาร)
    ///   • "PT:{Account_Paid_Type.ID}" = หมวดค่าใช้จ่ายเดิม (บัญชีตาม mapping)
    ///   • "NA:{Account_Code}"        = ผังบัญชี NextAcc โดยตรง (5x/12x/เจ้าหนี้กรรมการ)
    /// </summary>
    public static class PaymentVoucherForm
    {
        public const string PaidHowPrefix = "PH:";
        public const string PaidTypePrefix = "PT:";
        public const string AccountPrefix = "NA:";

        // ──────────────────────────────────────────────────────────────
        // จ่ายจาก
        // ──────────────────────────────────────────────────────────────

        public sealed class PayFromChoice
        {
            /// <summary>ค่าที่ต้องเลือกใน DropDownList2 (Account_Paid_How.ID) — "" = ยังไม่ได้เลือก</summary>
            public string PaidHowId = "";
            /// <summary>ค่าที่ต้องเลือกใน ddlPaidHowNexaacc (Nexaacc_AccountId) — "" = ใช้ mapping ของแหล่งเงิน</summary>
            public string AccountOverride = "";
            /// <summary>เลือกบัญชี NextAcc ที่ไม่มีแถวแหล่งเงิน — ต้องให้ผู้ใช้เลือก "บันทึกในระบบเป็น" เอง</summary>
            public bool NeedsLegacyPaidHow;
        }

        /// <param name="value">ค่าจาก ddlPayFrom</param>
        /// <param name="legacyPaidHowId">ค่าที่ผู้ใช้เลือกใน DropDownList2 (ใช้เมื่อเลือกบัญชี NextAcc ตรง ๆ)</param>
        public static PayFromChoice ResolvePayFrom(string value, string legacyPaidHowId)
        {
            var c = new PayFromChoice();
            value = (value ?? "").Trim();
            if (value.StartsWith(PaidHowPrefix, StringComparison.Ordinal))
            {
                c.PaidHowId = value.Substring(PaidHowPrefix.Length);
            }
            else if (value.StartsWith(AccountPrefix, StringComparison.Ordinal))
            {
                c.AccountOverride = value.Substring(AccountPrefix.Length);
                c.NeedsLegacyPaidHow = true;
                c.PaidHowId = IsPickedId(legacyPaidHowId) ? legacyPaidHowId.Trim() : "";
            }
            return c;
        }

        /// <summary>โหมดแก้ไข/เปิดจากหน้ารับของ: แหล่งเงินเดิม → ค่าใน ddlPayFrom</summary>
        public static string PayFromValueForPaidHow(string paidHowId)
            => IsPickedId(paidHowId) ? PaidHowPrefix + paidHowId.Trim() : "";

        public static string PayFromLabel(string paidHow, string accountCode, string accountName)
        {
            string acc = ((accountCode ?? "") + " " + (accountName ?? "")).Trim();
            return (paidHow ?? "").Trim() + (acc.Length > 0 ? " — " + acc : " — (ยังไม่ผูกบัญชี NextAcc)");
        }

        public static string AccountOnlyLabel(string accountCode, string accountName)
            => "บัญชี NextAcc: " + ((accountCode ?? "") + " " + (accountName ?? "")).Trim();

        // ──────────────────────────────────────────────────────────────
        // หมวดค่าใช้จ่ายต่อบรรทัด
        // ──────────────────────────────────────────────────────────────

        public sealed class LineAccountChoice
        {
            /// <summary>ค่าสำหรับ ddlLineCategory (Account_Paid_Type.ID) — "" = ไม่ใช้หมวดเดิม</summary>
            public string PaidTypeId = "";
            /// <summary>ค่าสำหรับ ddlLineChargeNexaacc (Account_Code) — "" = ใช้ mapping ของหมวด</summary>
            public string ChargeCode = "";
            public bool IsEmpty => PaidTypeId.Length == 0 && ChargeCode.Length == 0;
        }

        public static LineAccountChoice ResolveLineAccount(string value)
        {
            var c = new LineAccountChoice();
            value = (value ?? "").Trim();
            if (value.StartsWith(PaidTypePrefix, StringComparison.Ordinal)) c.PaidTypeId = value.Substring(PaidTypePrefix.Length);
            else if (value.StartsWith(AccountPrefix, StringComparison.Ordinal)) c.ChargeCode = value.Substring(AccountPrefix.Length);
            return c;
        }

        public static string LineAccountLabel(string paidType, string accountCode, string accountName)
        {
            string acc = ((accountCode ?? "") + " " + (accountName ?? "")).Trim();
            return (paidType ?? "").Trim() + (acc.Length > 0 ? " — " + acc : "");
        }

        /// <summary>แถวรายการที่เก็บใน Session["dtDetail"] (โค้ดเดิมของปุ่ม "เพิ่ม" ย้ายมาไว้ที่นี่ — พฤติกรรมเดิมทุกบรรทัด)</summary>
        public sealed class LineRow
        {
            public string PaidTypeId = "";
            public string PaidTypeName = "";
            public string NexaaccAccountId = "";
            public string AccountCode = "";
        }

        /// <param name="categoryId">ddlLineCategory.SelectedValue</param>
        /// <param name="categoryText">ddlLineCategory.SelectedItem?.Text</param>
        /// <param name="chargeCode">ddlLineChargeNexaacc.SelectedValue</param>
        /// <param name="chargeText">ddlLineChargeNexaacc.SelectedItem?.Text</param>
        /// <param name="lookupNexaaccByCode">Account_Code → (Nexaacc_AccountId, Account_Name) จาก Accounting_Nexaacc_Accounts</param>
        /// <param name="lookupPaidTypeAccountId">ชื่อหมวด → Account_Paid_Type.Nexaacc_AccountId (อาจโยน exception — กลืนเหมือนเดิม)</param>
        public static LineRow BuildLineRow(string categoryId, string categoryText, string chargeCode, string chargeText,
            Func<string, Tuple<string, string>> lookupNexaaccByCode, Func<string, string> lookupPaidTypeAccountId)
        {
            bool pickedNexaaccCharge = !string.IsNullOrEmpty(chargeCode);
            var row = new LineRow { PaidTypeId = categoryId, PaidTypeName = categoryText ?? "" };
            if (pickedNexaaccCharge)
            {
                row.AccountCode = chargeCode;
                string accName = chargeText ?? row.AccountCode;
                var info = lookupNexaaccByCode(row.AccountCode);
                row.NexaaccAccountId = info.Item1 ?? "";
                row.PaidTypeName = !string.IsNullOrEmpty(info.Item2) ? info.Item2 : accName;
                if (string.IsNullOrEmpty(categoryId)) row.PaidTypeId = "";
            }
            else
            {
                try { row.NexaaccAccountId = lookupPaidTypeAccountId(row.PaidTypeName) ?? ""; }
                catch { }
            }
            return row;
        }

        /// <summary>หมวดหลักของใบ (Account_Payment.Paid_Type) เมื่อผู้ใช้ยังไม่เลือก: หมวดเดิมของบรรทัดแรกที่มี →
        /// หมวดเดิมที่ผูกรหัสบัญชีเดียวกับบรรทัดแรกที่เลือกบัญชี NextAcc ตรง ๆ → null (ให้ผู้ใช้เลือก)</summary>
        public static string DeriveHeaderPaidTypeId(DataTable dtDetail, Func<string, string> paidTypeIdByAccountCode)
        {
            if (dtDetail == null) return null;
            if (dtDetail.Columns.Contains("PaidTypeId"))
                foreach (DataRow r in dtDetail.Rows)
                {
                    if (r.RowState == DataRowState.Deleted) continue;
                    string id = r["PaidTypeId"] == DBNull.Value ? "" : (r["PaidTypeId"]?.ToString() ?? "");
                    if (IsPickedId(id) && id != "0") return id.Trim();
                }
            if (dtDetail.Columns.Contains("AccountCode") && paidTypeIdByAccountCode != null)
                foreach (DataRow r in dtDetail.Rows)
                {
                    if (r.RowState == DataRowState.Deleted) continue;
                    string code = r["AccountCode"] == DBNull.Value ? "" : (r["AccountCode"]?.ToString() ?? "");
                    if (code.Length == 0) continue;
                    string id = paidTypeIdByAccountCode(code);
                    if (IsPickedId(id)) return id;
                }
            return null;
        }

        // ──────────────────────────────────────────────────────────────
        // payload คิว CREATE_VOUCHER_JOURNAL (ย้ายจาก Button3_Click เดิม — ห้ามเปลี่ยนผล)
        // ──────────────────────────────────────────────────────────────

        public interface IVoucherLookups
        {
            string LookupPaidHowAccountId(string paidHowText);
            string LookupPaidHowAccountCode(string paidHowText);
            string LookupPaidTypeAccountId(string paidTypeText);
            string LookupPaidTypeAccountCode(string paidTypeText);
            bool IsPaidHowCashOrBank(string paidHowText);
            /// <summary>(Nexaacc_AccountId, Account_Code) ของบัญชีล้างรับของตามโหมดรับของ</summary>
            Tuple<string, string> GoodsReceiptClearingAccount();
        }

        public sealed class VoucherInput
        {
            public string NetTotalText;            // TextBox6.Text
            public string PaymentMethod;           // DropDownList2.SelectedItem?.Text
            public string ExpenseCategory;         // DropDownList3.SelectedItem?.Text
            public string VendorName;              // DropDownList1.SelectedItem?.Text
            public string VendorExternalId;
            public string VendorTaxId;
            public string PaymentAccountOverride;  // ddlPaidHowNexaacc.SelectedValue
            public DataTable Detail;               // Session["dtDetail"]
            public string VatText;                 // TextBox4.Text
            public string VatClaimValue;           // ddlVatClaim.SelectedValue
            public bool IsCertInLieu;
            public string WhtRateText;             // ddlWHTRate.SelectedValue
            public string WhtAmountText;           // txtWHTAmount.Text
            public bool IsCredit;                  // chkIsCredit && !certInLieu
            public DateTime DocDate;
            public string DocNumber;
            public CertificateInLieuInfo CertificateInLieu;   // null เมื่อไม่ใช่ใบรับรอง
            public string GrniLinePrefix;          // PurchaseService.GrniLinePrefix
        }

        /// <summary>อาร์กิวเมนต์ของ <c>AccountingSyncService.EnqueuePaymentVoucher</c> (+ บัญชีค่าใช้จ่ายหัวใบสำหรับโอนเป็นสินทรัพย์)</summary>
        public sealed class VoucherPayload
        {
            public string ExpenseCategory;
            public decimal Amount;
            public string PaymentMethod;
            public DateTime VoucherDate;
            public string Description;
            public string PayeeName;
            public bool HasInputVat;
            public decimal WhtRate;
            public decimal WhtAmount;
            public string DocumentNumber;
            public string PaymentAccountId;
            public string PaymentAccountCode;      // คำนวณไว้เหมือนเดิม (ไม่ได้ส่งเข้าคิว)
            public string ExpenseAccountId;
            public List<Dictionary<string, object>> ExpenseLines;
            public bool IsCredit;
            public bool AutoRecordPayment;
            public string SupplierExternalId;
            public string SupplierTaxId;
            public decimal VatAmount;
            public CertificateInLieuInfo CertificateInLieu;
        }

        public static VoucherPayload BuildVoucherPayload(VoucherInput i, IVoucherLookups sync)
        {
            var p = new VoucherPayload();
            decimal voucherAmount = decimal.Parse(i.NetTotalText);
            string paymentMethod = i.PaymentMethod ?? "CASH";
            string expenseCategory = i.ExpenseCategory ?? "OTHER";
            string vendorName = i.VendorName ?? "";
            DataTable dtDetail = i.Detail;
            string description = "";
            if (dtDetail?.Rows.Count > 0) description = dtDetail.Rows[0]["Detail"]?.ToString() ?? "";

            // บังคับแหล่งจ่ายเงินจากผังบัญชี NextAcc โดยตรง (ถ้าผู้ใช้เลือก) — เว้นว่าง = mapping ตามวิธีจ่ายเงิน
            string payAccId = !string.IsNullOrEmpty(i.PaymentAccountOverride)
                ? i.PaymentAccountOverride
                : sync.LookupPaidHowAccountId(paymentMethod);
            string payAccCode = sync.LookupPaidHowAccountCode(paymentMethod);

            var expenseLines = new List<Dictionary<string, object>>();
            bool hasPerLineCategories = dtDetail.Columns.Contains("PaidTypeName");
            for (int r = 0; r < dtDetail.Rows.Count; r++)
            {
                string lineCat = hasPerLineCategories ? dtDetail.Rows[r]["PaidTypeName"]?.ToString() : expenseCategory;
                string lineAccId = hasPerLineCategories ? dtDetail.Rows[r]["NexaaccAccountId"]?.ToString() : null;
                if (string.IsNullOrEmpty(lineAccId))
                    lineAccId = sync.LookupPaidTypeAccountId(lineCat);
                string lineAccCode = dtDetail.Columns.Contains("AccountCode") ? dtDetail.Rows[r]["AccountCode"]?.ToString() : "";
                if (string.IsNullOrEmpty(lineAccCode))
                    lineAccCode = sync.LookupPaidTypeAccountCode(lineCat);
                // บรรทัด "ชำระค่าสินค้าที่รับเข้าแล้ว" → บัญชีล้างรับของ (ไม่ใช่ค่าใช้จ่าย)
                if ((lineCat ?? "").StartsWith(i.GrniLinePrefix ?? "\u0000"))
                {
                    var clr = sync.GoodsReceiptClearingAccount();
                    if (!string.IsNullOrEmpty(clr.Item2)) lineAccCode = clr.Item2;
                    if (!string.IsNullOrEmpty(clr.Item1)) lineAccId = clr.Item1;
                }

                expenseLines.Add(new Dictionary<string, object>
                {
                    { "category", lineCat ?? expenseCategory },
                    { "description", dtDetail.Rows[r]["Detail"]?.ToString() ?? "" },
                    { "amount", Convert.ToDecimal(dtDetail.Rows[r]["Amount"]) },
                    { "accountId", lineAccId ?? "" },
                    { "accountCode", lineAccCode ?? "" }
                });
            }

            string expAccId = sync.LookupPaidTypeAccountId(expenseCategory);

            bool hasVat = false;
            decimal vatAmt = 0m;
            try { vatAmt = Convert.ToDecimal(i.VatText); hasVat = vatAmt > 0; } catch { }

            // ไม่เคลมภาษีซื้อ (§82/5) / ใบรับรองแทนใบเสร็จ: กระจาย VAT เข้ายอดแต่ละบรรทัด แล้วส่ง hasInputVat=false/vatAmount=0
            bool noClaimVat = i.VatClaimValue == "0" || i.IsCertInLieu;
            if (noClaimVat && vatAmt > 0 && expenseLines.Count > 0)
            {
                decimal subtotalForVat = 0m;
                foreach (var ln in expenseLines) subtotalForVat += Convert.ToDecimal(ln["amount"]);
                if (subtotalForVat > 0)
                {
                    decimal distributed = 0m;
                    for (int r = 0; r < expenseLines.Count; r++)
                    {
                        decimal lineNet = Convert.ToDecimal(expenseLines[r]["amount"]);
                        decimal addVat = (r == expenseLines.Count - 1)
                            ? (vatAmt - distributed)
                            : Math.Round(vatAmt * (lineNet / subtotalForVat), 2, MidpointRounding.AwayFromZero);
                        distributed += addVat;
                        expenseLines[r]["amount"] = lineNet + addVat;
                    }
                }
                hasVat = false;
                vatAmt = 0m;
            }

            decimal syncWhtRate = 0;
            decimal.TryParse(i.WhtRateText, out syncWhtRate);
            decimal syncWhtAmount = 0;
            decimal.TryParse(i.WhtAmountText, out syncWhtAmount);

            bool paidHowIsCashOrBank = sync.IsPaidHowCashOrBank(paymentMethod);

            p.ExpenseCategory = expenseCategory;
            p.Amount = voucherAmount;
            p.PaymentMethod = paymentMethod;
            p.VoucherDate = i.DocDate;
            p.Description = description;
            p.PayeeName = vendorName;
            p.HasInputVat = hasVat;
            p.WhtRate = syncWhtRate;
            p.WhtAmount = syncWhtAmount;
            p.DocumentNumber = i.DocNumber;
            p.PaymentAccountId = payAccId;
            p.PaymentAccountCode = payAccCode;
            p.ExpenseAccountId = expAccId;
            p.ExpenseLines = expenseLines;
            p.IsCredit = i.IsCredit;
            p.AutoRecordPayment = paidHowIsCashOrBank && !i.IsCredit;
            p.SupplierExternalId = i.VendorExternalId;
            p.SupplierTaxId = i.VendorTaxId;
            p.VatAmount = vatAmt;
            p.CertificateInLieu = i.CertificateInLieu;
            return p;
        }

        // ──────────────────────────────────────────────────────────────
        // AI ของ NextAcc — แปลงคำตอบเป็นค่าของฟอร์ม
        // ──────────────────────────────────────────────────────────────

        /// <summary>รหัสประเภทเงินได้ของ NextAcc (Helpers/ThaiWhtRateTable.cs:58-133) → อัตรา (%) ตามผู้รับ
        /// (นิติบุคคล = เลขผู้เสียภาษี 13 หลักขึ้นต้น 0). null = ไม่มีอัตราคงที่/ไม่รู้จัก ⇒ ไม่เสนอให้กดใช้</summary>
        public static decimal? WhtRateForIncomeCode(string code, bool juristic)
        {
            switch ((code ?? "").Trim())
            {
                case "2": case "3": case "6": case "7": case "8": case "8pr": return 3m;
                case "4a": return juristic ? 1m : 15m;
                case "4b": return 10m;
                case "5": case "8cp": return 5m;
                case "8ad": return 2m;
                case "8tr": return 1m;
                case "8ins": return juristic ? 1m : (decimal?)null;
                case "8ent": return juristic ? (decimal?)null : 5m;
                default: return null;   // "1" เงินเดือน (ขั้นบันได), gov1 (ฝั่งถูกหัก), รหัสที่ไม่รู้จัก
            }
        }

        public static string WhtIncomeName(string code)
        {
            switch ((code ?? "").Trim())
            {
                case "1": return "เงินเดือน ค่าจ้าง";
                case "2": return "ค่านายหน้า/ค่าบริการ";
                case "3": return "ค่าลิขสิทธิ์/ค่าสิทธิ";
                case "4a": return "ดอกเบี้ย";
                case "4b": return "เงินปันผล";
                case "5": return "ค่าเช่าทรัพย์สิน";
                case "6": return "ค่าวิชาชีพอิสระ";
                case "7": return "ค่ารับเหมา";
                case "8": return "ค่าจ้างทำของ/ค่าบริการ";
                case "8ad": return "ค่าโฆษณา";
                case "8tr": return "ค่าขนส่ง";
                case "8ins": return "ค่าเบี้ยประกันวินาศภัย";
                case "8pr": return "รางวัล/ส่วนลดส่งเสริมการขาย";
                case "8cp": return "รางวัลการประกวด/ชิงโชค";
                case "8ent": return "ค่าแสดงของนักแสดง";
                default: return "";
            }
        }

        public static bool IsNoWhtAnswer(string answer)
        {
            string a = (answer ?? "").Trim();
            return a.Equals("None", StringComparison.OrdinalIgnoreCase) || a.Equals("Skip", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>คำตอบ vat/infer-type ("7" | "0" | "Exempt") → อัตรา %; null = อ่านไม่ออก</summary>
        public static int? VatAnswerPercent(string answer)
        {
            string a = (answer ?? "").Trim();
            if (a.Equals("Exempt", StringComparison.OrdinalIgnoreCase)) return 0;
            decimal d;
            if (decimal.TryParse(a.TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out d)) return (int)Math.Round(d);
            return null;
        }

        /// <summary>ความมั่นใจของ NextAcc (0–1) → เปอร์เซ็นต์เต็ม 0–100</summary>
        public static int ConfidencePct(decimal? confidence)
        {
            if (!confidence.HasValue) return 0;
            decimal c = confidence.Value;
            if (c <= 1m) c *= 100m;
            if (c < 0m) c = 0m;
            if (c > 100m) c = 100m;
            return (int)Math.Round(c, MidpointRounding.AwayFromZero);
        }

        // ──────────────────────────────────────────────────────────────
        // ตรวจใบซ้ำ (เลขผู้เสียภาษีผู้ขาย + เลขที่ใบกำกับ/ใบเสร็จของผู้ขาย)
        // ──────────────────────────────────────────────────────────────

        public sealed class DuplicateHit
        {
            public string Source;      // PV (ใบสำคัญจ่ายในระบบ) | OCR (หน้าสแกน) | NEXTACC
            public string Number;
            public string Date;        // yyyy-MM-dd
            public decimal Amount;
            public string Status;
            public string Url;
            public string Reason;
            public bool Strong;
        }

        /// <summary>ลายเซ็นของชุดผลตรวจซ้ำ — กดบันทึกซ้ำด้วยชุดเดิม = ยืนยันว่าไม่ใช่ใบซ้ำ</summary>
        public static string DuplicateSignature(IEnumerable<DuplicateHit> hits)
        {
            var sb = new StringBuilder("D:");
            if (hits != null)
                foreach (var h in hits) sb.Append(h.Source).Append('|').Append(h.Number).Append(';');
            return sb.ToString();
        }

        public static string SourceLabel(string source)
        {
            switch (source)
            {
                case "PV": return "ใบสำคัญจ่ายในระบบ";
                case "OCR": return "สแกนเอกสาร (OCR)";
                case "NEXTACC": return "เอกสารใน NextAcc";
                default: return source ?? "";
            }
        }

        public static bool IsPickedId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            string t = id.Trim();
            return !t.StartsWith("---", StringComparison.Ordinal) && t != "%";
        }
    }
}
