using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Take_Time_BangPhra.Integration.OcrReview;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// ผู้ช่วยของหน้า <c>Account/PaymentVoucher</c> (CLAUDE.md ข้อ 3b) — เรียก AI ของ NextAcc ผ่านเซิร์ฟเวอร์ (หน้าเว็บไม่เห็นคีย์)
    /// และตรวจใบซ้ำ. ทุกอย่างเป็น "คำแนะนำ" — หน้าแสดงเป็นชิปให้ผู้ใช้กดใช้เอง ไม่เติมค่าเงียบ ๆ.
    ///
    /// NextAcc (Wachira-d/Accounting HEAD 9c5680d) — ทุกเส้น <c>/api/companies/{cid}/…</c> (X-Api-Key; int_ ผ่าน fallback):
    ///   • POST ai/payment-voucher/suggest-account  AiSuggestionController.cs:193-253 (body SuggestPaymentVoucherAccountRequest
    ///          SourceInvoiceId(Guid.Empty = ยังไม่มีใบต้นทาง)/LineDescription/Amount/Currency/CurrentAccountCode/ContactId) → ToDto :3238-3263
    ///          {primary(=รหัสบัญชี), confidence, alternatives, reasoning, feedbackId, usedAi, sourceLabel}
    ///   • POST ai/vat/infer-type                    :491-566  {vatType "7"|"0"|"Exempt", confidence, reasoning, feedbackId}
    ///   • POST ai/wht/infer-category                :437-483  ToDto (primary = รหัสประเภทเงินได้ ThaiWhtRateTable; รหัสแต่งขึ้น = answer null)
    ///   • GET  ai/payment-voucher/suggest-type?contactId=  :123-185 {paymentType "Cash"|"Credit", confidence, reasoning, feedbackId}
    ///   • POST ai/contact/fuzzy-match               :928-1056 {matches[{id,name,taxId,branchCode?,confidence,reason}], feedbackId}
    ///   • GET  document/duplicate-check             DocumentController.cs:907-921 → DuplicateCheckResult (DocumentDtos.cs:1345-1363)
    ///          {hasStrongMatch, candidates[{id,documentNumber,documentType,documentDate,totalAmount,status,supplierInvoiceNumber,
    ///          contactName,matchReason,isStrong}]} (DocumentService.cs:7617 — เลขใบกำกับผู้ขายตรง = strong · คู่ค้า+ยอด±0.5%+±60 วัน = weak)
    ///   • POST ai-feedback/record                   AiFeedbackController.cs:36-66 {feedbackId, chosenAnswer, acceptedAi, source "Explicit"}
    /// ปิด: <c>Nexaacc_Pv_Ai_Assist=0</c> หรือยังไม่เปิด company endpoint / NextAcc อยู่ในช่วงล่ม → หน้าไม่แสดงชิป (ฟอร์มใช้ได้ปกติ)
    /// </summary>
    public class PaymentVoucherAssist
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private const int SuggestTimeoutMs = 7000;
        private const int SmallTimeoutMs = 4000;

        private readonly string _conn;
        private readonly AccountingConfig _cfg;
        private readonly code _code = new code();

        public PaymentVoucherAssist(string connectionString)
        {
            _conn = connectionString;
            _cfg = new AccountingConfig(connectionString);
        }

        /// <summary>เรียก AI/ตรวจซ้ำบน NextAcc ได้หรือไม่ (ไม่ได้ = ตรวจซ้ำเฉพาะในระบบ)</summary>
        public bool RemoteAvailable
        {
            get
            {
                try
                {
                    if (!_cfg.IsConfigured || !_cfg.Enabled || !_cfg.CanUseCompanyEndpoints) return false;
                    DateTime until; string err;
                    if (AccountingApiClient.IsServerDown(out until, out err)) return false;
                    return true;
                }
                catch { return false; }
            }
        }

        public bool AiEnabled
        {
            get
            {
                try { return RemoteAvailable && _cfg.GetConfigValue("Nexaacc_Pv_Ai_Assist", "1") != "0"; }
                catch { return false; }
            }
        }

        // ──────────────────────────────────────────────────────────────
        // คำแนะนำ
        // ──────────────────────────────────────────────────────────────

        public sealed class SuggestRequest
        {
            public string VendorId;
            public string Description;
            public decimal Amount;
            public string CurrentLineAccount;   // ค่าใน ddlLineAccount (PT:/NA:)
            public string CurrentVatPercent;    // อัตรา % ของประเภทภาษีที่เลือก ("" = ยังไม่เลือก)
            public string CurrentWhtRate;       // ddlWHTRate
            public bool CurrentIsCredit;
            public bool WantLine;               // บัญชี/VAT/WHT (ต้องมีรายละเอียด)
            public bool WantVendor;             // เครดิต/เงินสด + ผู้ติดต่อ
        }

        public sealed class Suggestion
        {
            public string Kind;        // account | vat | wht | paytype | contact
            public string Target;      // control ที่จะเปลี่ยน (หน้าเว็บ map เป็น id)
            public string Value;       // ค่าที่จะตั้ง ("" = แสดงอย่างเดียว)
            public string Label;
            public int Confidence;
            public string Reasoning;
            public string FeedbackId;
            public string Answer;      // คำตอบดิบของ NextAcc (ส่งกลับตอนบันทึก feedback)
            public bool Same;          // ตรงกับค่าที่เลือกอยู่แล้ว
        }

        public List<Suggestion> Suggest(SuggestRequest req)
        {
            var list = new List<Suggestion>();
            if (!AiEnabled) return list;
            var vendor = LoadVendor(req.VendorId);
            Guid? contactId = ResolveContactId(req.VendorId, vendor);
            bool juristic = AccountingDataMapper.IsJuristicPerson(vendor?.TaxId);
            string desc = (req.Description ?? "").Trim();
            if (desc.Length > 300) desc = desc.Substring(0, 300);

            var tasks = new List<Task<Suggestion[]>>();
            if (req.WantLine && desc.Length >= 2)
            {
                tasks.Add(SafeAsync(() => SuggestAccountAsync(req, vendor, contactId, desc)));
                tasks.Add(SafeAsync(() => SuggestVatAsync(req, vendor, contactId, desc)));
                tasks.Add(SafeAsync(() => SuggestWhtAsync(req, vendor, juristic, desc)));
            }
            if (req.WantVendor && vendor != null)
            {
                if (contactId.HasValue) tasks.Add(SafeAsync(() => SuggestPayTypeAsync(req, contactId.Value)));
                // ผู้ติดต่อ: ถามเฉพาะเมื่อมีเรื่องให้ผู้ใช้รู้ (ยังไม่เคยผูกกับ NextAcc หรือผู้ขายในระบบยังไม่มีเลขผู้เสียภาษี)
                if (!contactId.HasValue || OcrReviewMath.Digits(vendor.TaxId).Length != 13)
                    tasks.Add(SafeAsync(() => FuzzyContactAsync(vendor)));
            }
            if (tasks.Count == 0) return list;
            try { Task.WaitAll(tasks.Cast<Task>().ToArray(), SuggestTimeoutMs + 1000); } catch { }
            foreach (var t in tasks)
                if (t.Status == TaskStatus.RanToCompletion && t.Result != null)
                    list.AddRange(t.Result.Where(s => s != null));
            return list;
        }

        private static async Task<Suggestion[]> SafeAsync(Func<Task<Suggestion[]>> f)
        {
            try { return await f().ConfigureAwait(false); }
            catch { return null; }
        }

        private async Task<Suggestion[]> SuggestAccountAsync(SuggestRequest req, VendorInfo v, Guid? contactId, string desc)
        {
            var cur = PaymentVoucherForm.ResolveLineAccount(req.CurrentLineAccount);
            string curCode = cur.ChargeCode.Length > 0 ? cur.ChargeCode : PaidTypeCode(cur.PaidTypeId);
            var body = new Dictionary<string, object>
            {
                { "sourceInvoiceId", Guid.Empty },
                { "lineDescription", desc },
                { "amount", req.Amount },
                { "currency", "THB" },
                { "currentAccountCode", string.IsNullOrEmpty(curCode) ? null : curCode },
                { "contactId", contactId }
            };
            var d = await CallAsync(HttpMethod.Post, "/ai/payment-voucher/suggest-account", body, SuggestTimeoutMs).ConfigureAwait(false);
            if (d == null) return null;
            string primary = Str(d, "primary");
            var m = Regex.Match(primary ?? "", @"\d{3,}");
            if (!m.Success) return null;
            string accCode = m.Value;
            string value, label;
            if (!ResolveLineAccountValue(accCode, out value, out label)) return null;
            return new[] { new Suggestion
            {
                Kind = "account", Target = "line", Value = value, Answer = accCode,
                Label = "หมวด: " + label,
                Confidence = PaymentVoucherForm.ConfidencePct(Dec(d, "confidence")),
                Reasoning = Str(d, "reasoning"), FeedbackId = Str(d, "feedbackId"),
                Same = string.Equals(value, req.CurrentLineAccount, StringComparison.Ordinal) || accCode == curCode
            } };
        }

        private async Task<Suggestion[]> SuggestVatAsync(SuggestRequest req, VendorInfo v, Guid? contactId, string desc)
        {
            string curRate = (req.CurrentVatPercent ?? "").Trim();
            var body = new Dictionary<string, object>
            {
                { "contactId", contactId },
                { "vendorName", v?.Name },
                { "vendorTaxId", v?.TaxId },
                { "vendorCountryCode", null },
                { "lineDescription", desc },
                { "amount", req.Amount },
                { "currentRate", curRate.Length == 0 ? null : curRate }
            };
            var d = await CallAsync(HttpMethod.Post, "/ai/vat/infer-type", body, SmallTimeoutMs).ConfigureAwait(false);
            if (d == null) return null;
            string ans = Str(d, "vatType");
            int? pct = PaymentVoucherForm.VatAnswerPercent(ans);
            if (!pct.HasValue) return null;
            string vatTypeId = VatTypeIdForPercent(pct.Value);
            if (vatTypeId == null) return null;
            bool exempt = string.Equals(ans, "Exempt", StringComparison.OrdinalIgnoreCase);
            int curPct;
            bool same = int.TryParse(curRate, out curPct) && curPct == pct.Value;
            return new[] { new Suggestion
            {
                Kind = "vat", Target = "vat", Value = vatTypeId, Answer = ans,
                Label = exempt ? "ภาษี: ยกเว้น VAT (ไม่มี VAT)" : (pct.Value > 0 ? "ภาษี: VAT " + pct.Value + "%" : "ภาษี: ไม่มี VAT (0%)"),
                Confidence = PaymentVoucherForm.ConfidencePct(Dec(d, "confidence")),
                Reasoning = Str(d, "reasoning"), FeedbackId = Str(d, "feedbackId"), Same = same
            } };
        }

        private async Task<Suggestion[]> SuggestWhtAsync(SuggestRequest req, VendorInfo v, bool juristic, string desc)
        {
            var body = new Dictionary<string, object>
            {
                { "documentId", null },
                { "vendorName", v?.Name },
                { "vendorTaxId", v?.TaxId },
                { "vendorType", v == null ? null : (juristic ? "JuristicPerson" : "Individual") },
                { "lineDescription", desc },
                { "amount", req.Amount },
                { "currentCode", null }
            };
            var d = await CallAsync(HttpMethod.Post, "/ai/wht/infer-category", body, SuggestTimeoutMs).ConfigureAwait(false);
            if (d == null) return null;
            string code = Str(d, "primary") ?? Str(d, "answer");
            if (string.IsNullOrWhiteSpace(code)) return null;     // NextAcc ทิ้งรหัสที่แต่งขึ้นเอง (answer null)
            string rateValue, label;
            if (PaymentVoucherForm.IsNoWhtAnswer(code)) { rateValue = "0"; label = "หัก ณ ที่จ่าย: ไม่หัก"; }
            else
            {
                decimal? rate = PaymentVoucherForm.WhtRateForIncomeCode(code, juristic);
                if (!rate.HasValue) return null;
                rateValue = rate.Value.ToString("0.##", CultureInfo.InvariantCulture);
                string nm = PaymentVoucherForm.WhtIncomeName(code);
                label = "หัก ณ ที่จ่าย " + rateValue + "%" + (nm.Length > 0 ? " (" + nm + ")" : "");
            }
            decimal curRate; decimal.TryParse(req.CurrentWhtRate, NumberStyles.Number, CultureInfo.InvariantCulture, out curRate);
            decimal sugRate; decimal.TryParse(rateValue, NumberStyles.Number, CultureInfo.InvariantCulture, out sugRate);
            return new[] { new Suggestion
            {
                Kind = "wht", Target = "wht", Value = rateValue, Answer = code, Label = label,
                Confidence = PaymentVoucherForm.ConfidencePct(Dec(d, "confidence")),
                Reasoning = Str(d, "reasoning"), FeedbackId = Str(d, "feedbackId"), Same = curRate == sugRate
            } };
        }

        private async Task<Suggestion[]> SuggestPayTypeAsync(SuggestRequest req, Guid contactId)
        {
            var d = await CallAsync(HttpMethod.Get, "/ai/payment-voucher/suggest-type?contactId=" + contactId, null, SmallTimeoutMs).ConfigureAwait(false);
            if (d == null) return null;
            string ans = Str(d, "paymentType");
            bool credit = string.Equals(ans, "Credit", StringComparison.OrdinalIgnoreCase);
            if (!credit && !string.Equals(ans, "Cash", StringComparison.OrdinalIgnoreCase)) return null;
            return new[] { new Suggestion
            {
                Kind = "paytype", Target = "credit", Value = credit ? "1" : "0", Answer = credit ? "Credit" : "Cash",
                Label = credit ? "การชำระ: ซื้อเชื่อ (ยังไม่จ่ายเงิน)" : "การชำระ: จ่ายทันที",
                Confidence = PaymentVoucherForm.ConfidencePct(Dec(d, "confidence")),
                Reasoning = Str(d, "reasoning"), FeedbackId = Str(d, "feedbackId"), Same = credit == req.CurrentIsCredit
            } };
        }

        private async Task<Suggestion[]> FuzzyContactAsync(VendorInfo v)
        {
            var d = await CallAsync(HttpMethod.Post, "/ai/contact/fuzzy-match", new Dictionary<string, object>
            {
                { "name", v.Name }, { "taxId", v.TaxId }, { "isCustomer", null }, { "isSupplier", null }, { "limit", 3 }
            }, SmallTimeoutMs).ConfigureAwait(false);
            var matches = Field(d, "matches") as object[];
            if (matches == null || matches.Length == 0) return null;
            var top = matches[0];
            string name = Str(top, "name");
            string tax = OcrReviewMath.Digits(Str(top, "taxId"));
            string myTax = OcrReviewMath.Digits(v.TaxId);
            string label;
            if (myTax.Length == 13 && tax.Length == 13 && tax != myTax)
                label = "ผู้ติดต่อใน NextAcc ชื่อใกล้เคียง \"" + name + "\" แต่เลขผู้เสียภาษีไม่ตรง (" + tax + ") — ตรวจข้อมูลผู้ขายก่อน";
            else if (myTax.Length != 13 && tax.Length == 13)
                label = "NextAcc มีผู้ติดต่อ \"" + name + "\" เลขผู้เสียภาษี " + tax + " — ผู้ขายนี้ในระบบยังไม่มีเลข (เติมในหน้าผู้ขายเพื่อเคลม VAT/ออก 50 ทวิ)";
            else
                label = "พบผู้ติดต่อใน NextAcc ที่น่าจะเป็นรายเดียวกัน: \"" + name + "\"" + (tax.Length > 0 ? " (" + tax + ")" : "")
                    + " — ใบนี้จะผูกกับรายนั้นตอนส่ง (จับคู่ด้วยเลขผู้เสียภาษี แล้วชื่อ)";
            return new[] { new Suggestion
            {
                Kind = "contact", Target = "", Value = "", Answer = Str(top, "id"), Label = label,
                Confidence = PaymentVoucherForm.ConfidencePct(Dec(top, "confidence")),
                Reasoning = Str(top, "reason"), FeedbackId = Str(d, "feedbackId")
            } };
        }

        /// <summary>ส่งผลที่ผู้ใช้เลือกกลับไปสอน NextAcc (กดใช้ = accepted, ปิด = rejected พร้อมค่าที่ใช้จริง)</summary>
        public bool RecordFeedback(string feedbackId, string chosenAnswer, bool accepted)
        {
            Guid fid;
            if (!AiEnabled || !Guid.TryParse(feedbackId ?? "", out fid) || fid == Guid.Empty) return false;
            if (string.IsNullOrWhiteSpace(chosenAnswer)) chosenAnswer = accepted ? "accepted" : "rejected";
            if (chosenAnswer.Length > 200) chosenAnswer = chosenAnswer.Substring(0, 200);
            try
            {
                var t = CallAsync(HttpMethod.Post, "/ai-feedback/record", new Dictionary<string, object>
                {
                    { "feedbackId", fid }, { "chosenAnswer", chosenAnswer.Trim() }, { "acceptedAi", accepted }, { "source", "Explicit" }
                }, SmallTimeoutMs, allowNullData: true);
                return t.Wait(SmallTimeoutMs + 500) && t.Result != null;
            }
            catch { return false; }
        }

        // ──────────────────────────────────────────────────────────────
        // ตรวจใบซ้ำ
        // ──────────────────────────────────────────────────────────────

        /// <param name="excludePaymentId">โหมดแก้ไข: เลขใบของตัวเอง (ไม่นับ)</param>
        /// <param name="includeRemote">ถาม NextAcc ด้วย (timeout สั้น — ล้ม = ข้ามเงียบ)</param>
        public List<PaymentVoucherForm.DuplicateHit> FindDuplicates(string vendorId, string supplierInvoiceNo, decimal amount,
            DateTime docDate, string excludePaymentId, bool includeRemote)
        {
            var hits = new List<PaymentVoucherForm.DuplicateHit>();
            string norm = OcrReviewMath.NormalizeDocNo(supplierInvoiceNo);
            if (norm.Length == 0) return hits;
            var vendor = LoadVendor(vendorId);
            string tax = OcrReviewMath.Digits(vendor?.TaxId);

            // 1) ใบสำคัญจ่ายในระบบ (PHASE19_40 Supplier_Invoice_No — ไม่มีคอลัมน์ = ข้าม)
            try
            {
                if (HasSupplierInvoiceColumn(_conn))
                {
                    var dt = _code.DatabaseQuerySafe(_conn,
                        @"SELECT TOP 5 p.ID, p.UID, p.Created_Date, p.Total_Amount, p.Status, p.Supplier_Invoice_No
                            FROM Account_Payment p LEFT JOIN Vendor v ON v.ID = p.Vendor_ID
                           WHERE p.Supplier_Invoice_No_Norm = @n
                             AND ISNULL(p.Status, N'') <> N'Cancel'
                             AND (@ex = N'' OR p.ID <> @ex)
                             AND (p.Vendor_ID = @vid
                                  OR (@t <> N'' AND REPLACE(REPLACE(ISNULL(v.IDNumber, N''), N'-', N''), N' ', N'') = @t))
                           ORDER BY p.Created_Date DESC",
                        new Dictionary<string, object>
                        {
                            { "@n", norm }, { "@ex", excludePaymentId ?? "" }, { "@vid", vendorId ?? "" }, { "@t", tax }
                        });
                    if (dt != null)
                        foreach (DataRow r in dt.Rows)
                            hits.Add(new PaymentVoucherForm.DuplicateHit
                            {
                                Source = "PV", Number = r["ID"]?.ToString(), Strong = true,
                                Date = r["Created_Date"] == DBNull.Value ? "" : Convert.ToDateTime(r["Created_Date"]).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                Amount = r["Total_Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Total_Amount"]),
                                Status = r["Status"]?.ToString(),
                                Url = "/Account/PaymentVoucher?command=edit&uid=" + Uri.EscapeDataString(r["UID"]?.ToString() ?? ""),
                                Reason = "เลขที่ใบกำกับ/ใบเสร็จ \"" + r["Supplier_Invoice_No"] + "\" ของผู้ขายเดียวกัน"
                            });
                }
            }
            catch { }

            // 2) หน้าสแกน OCR (PHASE19_38 — กติกาเดียวกับ OcrReviewService.FindLocalDuplicate)
            try
            {
                if (tax.Length > 0 || vendor != null)
                {
                    var dt = _code.DatabaseQuerySafe(_conn,
                        @"IF OBJECT_ID('dbo.Ocr_Document_Log', 'U') IS NOT NULL
                          SELECT TOP 5 Scan_Id, Doc_Kind, Status, Nexaacc_Doc_No, Supplier_Doc_No, Doc_Date, Total_Amount
                            FROM Ocr_Document_Log
                           WHERE Supplier_Doc_No_Norm = @n
                             AND (@t = N'' OR ISNULL(Supplier_TaxId, N'') = @t OR ISNULL(Supplier_TaxId, N'') = N'')
                             AND Status IN (N'CREATED', N'UPDATED', N'APPROVED', N'DONE', N'EVIDENCE')
                             AND Doc_Kind IN (N'PV', N'EXP')
                           ORDER BY ISNULL(Updated_At, Created_At) DESC",
                        new Dictionary<string, object> { { "@n", norm }, { "@t", tax } });
                    if (dt != null)
                        foreach (DataRow r in dt.Rows)
                            hits.Add(new PaymentVoucherForm.DuplicateHit
                            {
                                Source = "OCR", Strong = tax.Length > 0,
                                Number = string.IsNullOrEmpty(r["Nexaacc_Doc_No"]?.ToString()) ? (r["Supplier_Doc_No"]?.ToString() ?? "") : r["Nexaacc_Doc_No"].ToString(),
                                Date = r["Doc_Date"] == DBNull.Value ? "" : Convert.ToDateTime(r["Doc_Date"]).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                Amount = r["Total_Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Total_Amount"]),
                                Status = r["Status"]?.ToString(),
                                Url = "/Voucher/OcrUpload?scan=" + Uri.EscapeDataString(r["Scan_Id"]?.ToString() ?? ""),
                                Reason = "สแกนใบเดียวกัน (" + r["Doc_Kind"] + ") บันทึกไปแล้ว"
                            });
                }
            }
            catch { }

            // 3) NextAcc — เฉพาะเลขใบกำกับผู้ขายตรง (strong) ของผู้ติดต่อนี้
            if (includeRemote && RemoteAvailable)
            {
                try
                {
                    Guid? contactId = ResolveContactId(vendorId, vendor);
                    if (contactId.HasValue)
                    {
                        string q = "/document/duplicate-check?contactId=" + contactId.Value
                            + "&supplierInvoiceNumber=" + Uri.EscapeDataString(supplierInvoiceNo.Trim())
                            + "&amount=" + amount.ToString("0.00", CultureInfo.InvariantCulture)
                            + "&documentDate=" + docDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        var t = CallAsync(HttpMethod.Get, q, null, SmallTimeoutMs);
                        var d = t.Wait(SmallTimeoutMs + 500) ? t.Result : null;
                        var cands = Field(d, "candidates") as object[];
                        if (cands != null)
                        {
                            var sync = new AccountingSyncService(_conn);
                            foreach (var c in cands)
                            {
                                bool strong = Bool(c, "isStrong");
                                if (!strong) continue;   // ยอดใกล้เคียง (weak) ไม่ใช่หลักฐานใบซ้ำ — ไม่รบกวน
                                string id = Str(c, "id");
                                hits.Add(new PaymentVoucherForm.DuplicateHit
                                {
                                    Source = "NEXTACC", Strong = true,
                                    Number = Str(c, "documentNumber"),
                                    Date = IsoDate(c, "documentDate"),
                                    Amount = Dec(c, "totalAmount") ?? 0m,
                                    Status = Str(c, "status"),
                                    Url = string.IsNullOrEmpty(id) ? null : sync.BuildNexaaccDocumentUrl(id, "DOCUMENT"),
                                    Reason = "เลขที่ใบกำกับผู้ขาย \"" + Str(c, "supplierInvoiceNumber") + "\" อยู่ใน NextAcc แล้ว"
                                });
                            }
                        }
                    }
                }
                catch { }
            }
            return hits;
        }

        public static bool HasSupplierInvoiceColumn(string conn)
        {
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT CASE WHEN COL_LENGTH('dbo.Account_Payment', 'Supplier_Invoice_No_Norm') IS NULL THEN 0 ELSE 1 END", null);
                return dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 1;
            }
            catch { return false; }
        }

        // ──────────────────────────────────────────────────────────────
        // ข้อมูลประกอบ
        // ──────────────────────────────────────────────────────────────

        private sealed class VendorInfo { public string Id; public string Name; public string TaxId; }

        private VendorInfo LoadVendor(string vendorId)
        {
            int id;
            if (!int.TryParse(vendorId ?? "", out id) || id <= 0) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 ID, Name, IDNumber FROM Vendor WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                if (dt == null || dt.Rows.Count == 0) return null;
                return new VendorInfo { Id = dt.Rows[0]["ID"].ToString(), Name = dt.Rows[0]["Name"]?.ToString(), TaxId = dt.Rows[0]["IDNumber"]?.ToString() };
            }
            catch { return null; }
        }

        /// <summary>ผู้ติดต่อ NextAcc ของผู้ขาย: cache Accounting_Contact_Map (VENDOR-{id}, SUPPLIER) — ไม่มี = null
        /// (ไม่สร้างผู้ติดต่อจากหน้านี้ — สร้างตอน sync ใบเหมือนเดิม)</summary>
        private Guid? ResolveContactId(string vendorId, VendorInfo v)
        {
            if (v == null) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 Nexaacc_Contact_Id FROM Accounting_Contact_Map
                       WHERE External_Id = @e AND Contact_Type = 'SUPPLIER' AND Nexaacc_Contact_Id IS NOT NULL",
                    new Dictionary<string, object> { { "@e", "VENDOR-" + v.Id } });
                Guid g;
                if (dt != null && dt.Rows.Count > 0 && Guid.TryParse(dt.Rows[0][0]?.ToString(), out g) && g != Guid.Empty) return g;
            }
            catch { }
            return null;
        }

        private string PaidTypeCode(string paidTypeId)
        {
            if (string.IsNullOrEmpty(paidTypeId)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 Nexaacc_AccountCode FROM Account_Paid_Type WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", paidTypeId } });
                return dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value ? dt.Rows[0][0].ToString() : null;
            }
            catch { return null; }
        }

        /// <summary>รหัสบัญชีที่ AI แนะนำ → ค่าใน ddlLineAccount: มีหมวดเดิมผูกรหัสนี้ "หมวดเดียว" = PT:{id} (คงชื่อหมวดเดิมในรายงาน),
        /// ไม่งั้นบัญชีในผัง NextAcc ที่ sync ไว้ = NA:{code}; ไม่มีในผัง = ไม่เสนอ</summary>
        private bool ResolveLineAccountValue(string accCode, out string value, out string label)
        {
            value = null; label = null;
            try
            {
                string accName = null;
                var na = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 Account_Name FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @c AND ISNULL(Is_Active, 1) = 1",
                    new Dictionary<string, object> { { "@c", accCode } });
                if (na != null && na.Rows.Count > 0) accName = na.Rows[0][0]?.ToString();
                var pt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 2 ID, Paid_Type FROM Account_Paid_Type WHERE Status = 'True' AND Nexaacc_AccountCode = @c",
                    new Dictionary<string, object> { { "@c", accCode } });
                if (pt != null && pt.Rows.Count == 1)
                {
                    value = PaymentVoucherForm.PaidTypePrefix + pt.Rows[0]["ID"];
                    label = PaymentVoucherForm.LineAccountLabel(pt.Rows[0]["Paid_Type"]?.ToString(), accCode, accName);
                    return true;
                }
                if (accName == null) return false;
                value = PaymentVoucherForm.AccountPrefix + accCode;
                label = (accCode + " " + accName).Trim();
                return true;
            }
            catch { return false; }
        }

        /// <summary>อัตรา VAT → Account_Vat_Type.ID (ข้าม ID 2 = "รวม VAT" เหมือน PrefillFromGoodsReceipt — ไม่เดาว่าราคารวม VAT)</summary>
        private string VatTypeIdForPercent(int pct)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT ID, Vat_Percent FROM Account_Vat_Type WHERE Status = 'True' ORDER BY ID", null);
                if (dt == null) return null;
                foreach (DataRow r in dt.Rows)
                {
                    int p; int.TryParse(r["Vat_Percent"]?.ToString(), out p);
                    string id = r["ID"].ToString();
                    if (p == pct && id != "2") return id;
                }
            }
            catch { }
            return null;
        }

        // ──────────────────────────────────────────────────────────────
        // HTTP (timeout สั้น ไม่ retry — ห้ามทำให้หน้ากรอกค้าง)
        // ──────────────────────────────────────────────────────────────

        private async Task<object> CallAsync(HttpMethod method, string relPath, object body, int timeoutMs, bool allowNullData = false)
        {
            string url = _cfg.BaseUrl.TrimEnd('/') + "/api/companies/" + _cfg.CompanyId + relPath;
            var json = new JavaScriptSerializer();
            using (var cts = new CancellationTokenSource(timeoutMs))
            using (var req = new HttpRequestMessage(method, url))
            {
                req.Headers.Add("X-Api-Key", _cfg.CompanyApiKey);
                req.Headers.Add("Accept", "application/json");
                if (body != null)
                    req.Content = new StringContent(json.Serialize(body), Encoding.UTF8, "application/json");
                using (var res = await Http.SendAsync(req, cts.Token).ConfigureAwait(false))
                {
                    string text = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!res.IsSuccessStatusCode) return null;
                    object o;
                    try { o = json.DeserializeObject(text); } catch { return null; }
                    if (!(o is Dictionary<string, object>)) return null;
                    var ok = Field(o, "success");
                    if (ok is bool && !(bool)ok) return null;
                    var data = Field(o, "data");
                    if (data == null) return allowNullData ? o : null;
                    return data;
                }
            }
        }

        /// <summary>ฟิลด์ของวัตถุ JSON (JavaScriptSerializer → Dictionary) แบบไม่สนตัวพิมพ์ (NextAcc ส่ง camelCase)</summary>
        private static object Field(object t, string name)
        {
            var o = t as Dictionary<string, object>;
            if (o == null) return null;
            object v;
            if (o.TryGetValue(name, out v)) return v;
            foreach (var kv in o)
                if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }

        private static string Str(object t, string name)
        {
            var v = Field(t, name);
            if (v == null) return null;
            if (v is decimal) return ((decimal)v).ToString(CultureInfo.InvariantCulture);
            if (v is IFormattable) return ((IFormattable)v).ToString(null, CultureInfo.InvariantCulture);
            return v.ToString();
        }

        private static decimal? Dec(object t, string name)
        {
            var v = Field(t, name);
            if (v == null) return null;
            try
            {
                if (v is decimal) return (decimal)v;
                if (v is int || v is long || v is double) return Convert.ToDecimal(v, CultureInfo.InvariantCulture);
                decimal d;
                return decimal.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : (decimal?)null;
            }
            catch { return null; }
        }

        /// <summary>วันที่ ISO จาก JSON → yyyy-MM-dd (ปฏิทิน ค.ศ. เสมอ — ไม่ใช้ culture ของเครื่องที่อาจเป็น พ.ศ.)</summary>
        private static string IsoDate(object t, string name)
        {
            var v = Field(t, name);
            if (v == null) return "";
            if (v is DateTime) return ((DateTime)v).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string s = v.ToString();
            return s.Length >= 10 && Regex.IsMatch(s, @"^\d{4}-\d{2}-\d{2}") ? s.Substring(0, 10) : "";
        }

        private static bool Bool(object t, string name)
        {
            var v = Field(t, name);
            return v is bool && (bool)v;
        }
    }
}
