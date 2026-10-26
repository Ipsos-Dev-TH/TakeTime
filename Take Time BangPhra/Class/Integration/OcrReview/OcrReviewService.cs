using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Integration.OcrReview
{
    /// <summary>แถวของ Ocr_Document_Log (PHASE19_38)</summary>
    public sealed class OcrLogRow
    {
        public Guid ScanId;
        public string DocKind;
        public string Status;
        public string FileName;
        public Guid? NexaaccDocId;
        public string NexaaccDocNo;
        public string ReviewJson;
        public string OcrRawJson;
        public string LastError;
        public string ReclassRef;
        public string SupplierName;
        public string SupplierDocNo;
        public decimal? Total;
        public DateTime? UpdatedAt;
    }

    /// <summary>ตัวเลือก "ใบต้นทาง" ของใบลด/เพิ่มหนี้ (ผลค้น NextAcc)</summary>
    public sealed class OcrSourceCandidate
    {
        public string Id { get; set; }
        public string No { get; set; }
        public string Type { get; set; }
        public string TypeLabel { get; set; }
        public string Date { get; set; }
        public decimal Total { get; set; }
        public decimal BalanceDue { get; set; }
        public decimal Credited { get; set; }
        public decimal Remaining { get; set; }
        public string ContactId { get; set; }
        public string ContactName { get; set; }
        public string SupplierDocNo { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>ข้อมูลประกอบการส่งที่มาจากหน้าเว็บ (ผู้ใช้/ไฟล์/ลายเซ็น)</summary>
    public sealed class OcrSubmitContext
    {
        public string UserName;
        public string PreparerName;
        public string PreparerSignature;
        public string ScanFilePath;
        public bool AcknowledgeWarnings;
    }

    public sealed class OcrSubmitResult
    {
        public bool Ok;
        public bool NeedsAck;
        public bool Evidence;
        public string Message;
        public Guid? DocId;
        public string DocNo;
        public List<string> Errors = new List<string>();
        public List<string> ApproveWarnings = new List<string>();
        public List<string> Notes = new List<string>();
        public string DuplicateDocNo;
        public Guid? DuplicateDocId;
    }

    /// <summary>
    /// หน้าตรวจทาน OCR → NextAcc: เก็บ draft/ประวัติ (Ocr_Document_Log), ค้นใบต้นทาง, กันซ้ำ และลำดับการสร้างเอกสาร
    /// (create → PUT → approve → แนบไฟล์ → 50 ทวิ → ย้ายขาเงินของใบลด/เพิ่มหนี้). ทุกขั้นบันทึกสถานะ ⇒ กดส่งซ้ำ = ทำต่อจากขั้นที่ค้าง
    /// </summary>
    public sealed class OcrReviewService
    {
        public const string StDraft = "DRAFT", StCreated = "CREATED", StUpdated = "UPDATED", StApproved = "APPROVED",
            StDone = "DONE", StEvidence = "EVIDENCE", StFailed = "FAILED", StDiscarded = "DISCARDED";

        private readonly string _conn;
        private readonly code _code = new code();

        public OcrReviewService(string connectionString) { _conn = connectionString; }

        public static JavaScriptSerializer Json() => new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };

        public static string ToJson(object o) => o == null ? null : Json().Serialize(o);

        public static OcrReviewState ParseState(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return Json().Deserialize<OcrReviewState>(json); }
            catch { return null; }
        }

        // ──────────────────────────────────────────────────────────────
        // Ocr_Document_Log (guarded — ไม่มีตาราง = คืนค่าว่าง / ไม่บันทึก)
        // ──────────────────────────────────────────────────────────────

        private static bool? _hasTable;
        private static DateTime _hasTableAt = DateTime.MinValue;

        public bool HasLogTable()
        {
            if (_hasTable.HasValue && DateTime.Now - _hasTableAt < TimeSpan.FromMinutes(5)) return _hasTable.Value;
            bool v = false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT CASE WHEN OBJECT_ID('dbo.Ocr_Document_Log','U') IS NULL THEN 0 ELSE 1 END", null);
                v = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 1;
            }
            catch { v = false; }
            _hasTable = v; _hasTableAt = DateTime.Now;
            return v;
        }

        public OcrLogRow Load(Guid scanId)
        {
            if (!HasLogTable()) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 Scan_Id, Doc_Kind, Status, Original_File_Name, Nexaacc_Doc_Id, Nexaacc_Doc_No, Review_Json, Ocr_Raw_Json,
                             Last_Error, Nexaacc_Reclass_Ref, Supplier_Name, Supplier_Doc_No, Total_Amount, ISNULL(Updated_At, Created_At) AS Upd
                        FROM Ocr_Document_Log WHERE Scan_Id = @s",
                    new Dictionary<string, object> { { "@s", scanId } });
                if (dt == null || dt.Rows.Count == 0) return null;
                return ToRow(dt.Rows[0]);
            }
            catch { return null; }
        }

        private static OcrLogRow ToRow(DataRow r)
        {
            Func<string, string> s = c => r.Table.Columns.Contains(c) && r[c] != DBNull.Value ? r[c].ToString() : null;
            var row = new OcrLogRow
            {
                ScanId = (Guid)r["Scan_Id"],
                DocKind = s("Doc_Kind"),
                Status = s("Status"),
                FileName = s("Original_File_Name"),
                NexaaccDocNo = s("Nexaacc_Doc_No"),
                ReviewJson = s("Review_Json"),
                OcrRawJson = s("Ocr_Raw_Json"),
                LastError = s("Last_Error"),
                ReclassRef = s("Nexaacc_Reclass_Ref"),
                SupplierName = s("Supplier_Name"),
                SupplierDocNo = s("Supplier_Doc_No"),
            };
            if (r.Table.Columns.Contains("Nexaacc_Doc_Id") && r["Nexaacc_Doc_Id"] != DBNull.Value) row.NexaaccDocId = (Guid)r["Nexaacc_Doc_Id"];
            if (r.Table.Columns.Contains("Total_Amount") && r["Total_Amount"] != DBNull.Value) row.Total = Convert.ToDecimal(r["Total_Amount"]);
            if (r.Table.Columns.Contains("Upd") && r["Upd"] != DBNull.Value) row.UpdatedAt = Convert.ToDateTime(r["Upd"]);
            return row;
        }

        /// <summary>งานที่ยังไม่จบ (draft/ร่างบน NextAcc/ล้ม) ล่าสุด — แสดงเป็นรายการ "ทำต่อ" บนหน้า</summary>
        public List<OcrLogRow> ListOpen(int top = 15)
        {
            var list = new List<OcrLogRow>();
            if (!HasLogTable()) return list;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP (@n) Scan_Id, Doc_Kind, Status, Original_File_Name, Nexaacc_Doc_Id, Nexaacc_Doc_No, Last_Error,
                             Supplier_Name, Supplier_Doc_No, Total_Amount, ISNULL(Updated_At, Created_At) AS Upd
                        FROM Ocr_Document_Log
                       WHERE Status IN (N'DRAFT', N'CREATED', N'UPDATED', N'APPROVED', N'FAILED')
                       ORDER BY ISNULL(Updated_At, Created_At) DESC",
                    new Dictionary<string, object> { { "@n", top } });
                if (dt != null) foreach (DataRow r in dt.Rows) list.Add(ToRow(r));
            }
            catch { }
            return list;
        }

        /// <summary>บันทึก draft (upsert ตาม Scan_Id) — ไม่ทับ Ocr_Raw_Json ที่มีแล้ว · ไม่ลดสถานะที่เดินหน้าไปแล้ว</summary>
        public bool SaveDraft(OcrReviewState s, string rawJson, string fileName, string user)
        {
            if (s == null || !HasLogTable()) return false;
            Guid scanId;
            if (!Guid.TryParse(s.ScanId ?? "", out scanId)) return false;
            var t = OcrReviewMath.CalcTotals(s.Lines);
            try
            {
                _code.DatabaseInsertSafe(_conn,
                    @"IF EXISTS (SELECT 1 FROM Ocr_Document_Log WHERE Scan_Id = @s)
                          UPDATE Ocr_Document_Log
                             SET Review_Json = @j, Doc_Kind = @k, Supplier_Name = @sn, Supplier_TaxId = @tx, Supplier_Doc_No = @dn,
                                 Supplier_Doc_No_Norm = @dnn, Doc_Date = @dd, Total_Amount = @tot, Net_Pay = @net,
                                 Source_Doc_Id = @src, Source_Doc_No = @srcNo, Return_Ref = @rr,
                                 Ocr_Raw_Json = ISNULL(Ocr_Raw_Json, @raw), Original_File_Name = ISNULL(Original_File_Name, @fn),
                                 Status = CASE WHEN Status IN (N'DISCARDED') THEN N'DRAFT' ELSE Status END,
                                 Updated_At = GETDATE()
                           WHERE Scan_Id = @s
                      ELSE
                          INSERT INTO Ocr_Document_Log (Scan_Id, Doc_Kind, Status, Original_File_Name, Supplier_Name, Supplier_TaxId,
                                 Supplier_Doc_No, Supplier_Doc_No_Norm, Doc_Date, Total_Amount, Net_Pay, Source_Doc_Id, Source_Doc_No, Return_Ref,
                                 Review_Json, Ocr_Raw_Json, Created_By, Created_At, Updated_At)
                          VALUES (@s, @k, N'DRAFT', @fn, @sn, @tx, @dn, @dnn, @dd, @tot, @net, @src, @srcNo, @rr, @j, @raw, @u, GETDATE(), GETDATE())",
                    new Dictionary<string, object>
                    {
                        { "@s", scanId }, { "@j", ToJson(s) }, { "@k", s.DocKind ?? "" },
                        { "@sn", Cut(s.SupplierName, 300) }, { "@tx", Cut(OcrReviewMath.Digits(s.SupplierTaxId), 20) },
                        { "@dn", Cut(s.SupplierDocNo, 100) }, { "@dnn", Cut(OcrReviewMath.NormalizeDocNo(s.SupplierDocNo), 100) },
                        { "@dd", (object)OcrReviewMath.ParseDate(s.DocDate) ?? DBNull.Value },
                        { "@tot", t.Total }, { "@net", t.NetPay },
                        { "@src", ParseGuidOrNull(s.SourceDocId) ?? (object)DBNull.Value }, { "@srcNo", Cut(s.SourceDocNo, 60) },
                        { "@rr", Cut(s.ReturnRef, 60) },
                        { "@raw", (object)rawJson ?? DBNull.Value }, { "@fn", Cut(fileName, 260) }, { "@u", Cut(user, 100) }
                    });
                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "OcrReview", "SaveDraft " + scanId + " failed: " + ex.Message, user ?? "SYSTEM");
                return false;
            }
        }

        public void SetStatus(Guid scanId, string status, Guid? docId, string docNo, string error, string user,
            string correctionsJson = null, string reclassRef = null)
        {
            if (!HasLogTable()) return;
            try
            {
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Ocr_Document_Log
                         SET Status = @st,
                             Nexaacc_Doc_Id = ISNULL(@d, Nexaacc_Doc_Id),
                             Nexaacc_Doc_No = ISNULL(@no, Nexaacc_Doc_No),
                             Last_Error = @e,
                             Corrections_Json = ISNULL(@c, Corrections_Json),
                             Nexaacc_Reclass_Ref = ISNULL(@rc, Nexaacc_Reclass_Ref),
                             Submitted_By = CASE WHEN @st IN (N'APPROVED', N'DONE', N'EVIDENCE') THEN @u ELSE Submitted_By END,
                             Submitted_At = CASE WHEN @st IN (N'APPROVED', N'DONE', N'EVIDENCE') AND Submitted_At IS NULL THEN GETDATE() ELSE Submitted_At END,
                             Updated_At = GETDATE()
                       WHERE Scan_Id = @s",
                    new Dictionary<string, object>
                    {
                        { "@s", scanId }, { "@st", status }, { "@d", (object)docId ?? DBNull.Value }, { "@no", (object)Cut(docNo, 60) ?? DBNull.Value },
                        { "@e", (object)Cut(error, 2000) ?? DBNull.Value }, { "@c", (object)correctionsJson ?? DBNull.Value },
                        { "@rc", (object)Cut(reclassRef, 60) ?? DBNull.Value }, { "@u", Cut(user, 100) }
                    });
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "OcrReview", "SetStatus " + scanId + " " + status + " failed: " + ex.Message, user ?? "SYSTEM");
            }
        }

        /// <summary>เอกสารที่หน้านี้สร้าง (ใช้คุมสิทธิ์เปิด PDF: เปิดได้เฉพาะเอกสารที่มาจากหน้าตรวจทาน)</summary>
        public bool IsKnownDocument(Guid docId)
        {
            if (!HasLogTable()) return false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 1 FROM Ocr_Document_Log WHERE Nexaacc_Doc_Id = @d",
                    new Dictionary<string, object> { { "@d", docId } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>กันซ้ำฝั่ง TakeTime: เลขผู้ขาย + เลขที่เอกสาร ที่ส่ง/บันทึกไปแล้ว (ไม่นับสแกนนี้เอง)</summary>
        public OcrLogRow FindLocalDuplicate(OcrReviewState s)
        {
            if (!HasLogTable()) return null;
            string tax = OcrReviewMath.Digits(s.SupplierTaxId);
            string norm = OcrReviewMath.NormalizeDocNo(s.SupplierDocNo);
            if (norm.Length == 0) return null;
            Guid scanId; Guid.TryParse(s.ScanId ?? "", out scanId);
            bool note = OcrDocKind.IsNote(s.DocKind);
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 Scan_Id, Doc_Kind, Status, Original_File_Name, Nexaacc_Doc_Id, Nexaacc_Doc_No, Last_Error,
                             Supplier_Name, Supplier_Doc_No, Total_Amount, ISNULL(Updated_At, Created_At) AS Upd
                        FROM Ocr_Document_Log
                       WHERE Scan_Id <> @s AND Supplier_Doc_No_Norm = @n
                         AND (@t = N'' OR ISNULL(Supplier_TaxId, N'') = @t OR ISNULL(Supplier_TaxId, N'') = N'')
                         AND Status IN (N'CREATED', N'UPDATED', N'APPROVED', N'DONE', N'EVIDENCE')
                         AND ((@note = 1 AND Doc_Kind IN (N'VCN', N'VDN') AND Doc_Kind = @k) OR (@note = 0 AND Doc_Kind IN (N'PV', N'EXP')))
                       ORDER BY ISNULL(Updated_At, Created_At) DESC",
                    new Dictionary<string, object> { { "@s", scanId }, { "@n", norm }, { "@t", tax }, { "@note", note ? 1 : 0 }, { "@k", s.DocKind ?? "" } });
                return dt != null && dt.Rows.Count > 0 ? ToRow(dt.Rows[0]) : null;
            }
            catch { return null; }
        }

        /// <summary>เลขคืนสินค้า PRT- มีจริงใน Product_Out (PHASE19_31 คอลัมน์ Purchase_Return_Ref — guarded)</summary>
        public bool? PurchaseReturnExists(string returnRef)
        {
            if (string.IsNullOrWhiteSpace(returnRef)) return false;
            try
            {
                if (!Take_Time_BangPhra.Purchasing.PurchaseService.HasReturnColumns(_conn)) return null;   // ตรวจไม่ได้
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 1 FROM Product_Out WHERE Purchase_Return_Ref = @r",
                    new Dictionary<string, object> { { "@r", returnRef.Trim() } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return null; }
        }

        // ──────────────────────────────────────────────────────────────
        // สถานะเริ่มต้นจากผล OCR
        // ──────────────────────────────────────────────────────────────

        /// <summary>แปลงผล OCR เป็นสถานะตรวจทานตั้งต้น (บรรทัดจาก ExtractedItems — ไม่มีรายการ = บรรทัดเดียวจากยอดหัวใบ)</summary>
        public static OcrReviewState BuildInitialState(OcrResultResponse r, string hintKind)
        {
            var s = new OcrReviewState { ScanId = r.Id.ToString() };
            s.OcrTargetType = r.TargetDocumentType;
            s.OcrRole = r.OurRole;
            s.DocKind = KindFromOcr(r, hintKind);

            s.SupplierName = FirstNonEmpty(r.ExtractedVendorName, r.DbdInfo != null && r.DbdInfo.Matched ? r.DbdInfo.CanonicalName : null);
            s.SupplierTaxId = OcrReviewMath.Digits(r.ExtractedVendorTaxId);
            s.SupplierBranch = OcrReviewMath.NormalizeBranch(r.VendorBranchCode);
            s.SupplierAddress = FirstNonEmpty(r.VendorAddress, r.DbdInfo != null && r.DbdInfo.Matched ? r.DbdInfo.Address : null);
            s.SupplierDocNo = r.ExtractedDocumentNumber;
            string d = r.ExtractedDate.HasValue ? r.ExtractedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            s.SupplierDocDate = d;
            s.DocDate = d ?? DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            s.Notes = r.UserNotes;
            if (r.MatchedContactId.HasValue) s.SourceContactId = r.MatchedContactId.Value.ToString();

            bool paperTaxInvoice = IsTaxInvoicePaper(r);
            decimal? sub = r.ExtractedSubTotal, vat = r.ExtractedVatAmount, total = r.ExtractedTotalAmount;
            // ไม่ใช่ใบกำกับ + ไม่พบ VAT → ไม่มีภาษีซื้อให้แยก: ยอดก่อน VAT = ยอดรวม (ตรรกะเดียวกับหน้าเดิม)
            if (!paperTaxInvoice && (!vat.HasValue || vat.Value == 0m))
            {
                decimal amount = total ?? sub ?? 0m;
                if (amount > 0m) sub = amount;
                vat = 0m;
                if (!total.HasValue && sub.HasValue) total = sub;
            }
            if (!total.HasValue && sub.HasValue) total = OcrReviewMath.R2(sub.Value + (vat ?? 0m));
            if (!sub.HasValue && total.HasValue) sub = OcrReviewMath.R2(total.Value - (vat ?? 0m));
            s.PrintedSubTotal = sub;
            s.PrintedVat = vat;
            s.PrintedTotal = total;
            bool hasVat = (vat ?? 0m) > 0m;
            s.FullTaxInvoice = paperTaxInvoice || (hasVat && OcrReviewMath.IsValidThaiTaxId(s.SupplierTaxId));
            if (hasVat && s.FullTaxInvoice) s.TaxInvoiceDate = d;

            string defAcc = r.SuggestedAccounts?.DebitAccountCode;
            decimal whtRate = r.HasWht && r.WhtRate.HasValue ? r.WhtRate.Value : 0m;
            bool claim = hasVat && s.FullTaxInvoice;

            if (r.ExtractedItems != null && r.ExtractedItems.Count > 0)
            {
                foreach (var it in r.ExtractedItems)
                {
                    var l = new OcrReviewLine
                    {
                        Description = (it.Description ?? "").Trim(),
                        AccountCode = FirstNonEmpty(it.SuggestedAccountCode, defAcc),
                        OcrAccountCode = FirstNonEmpty(it.SuggestedAccountCode, defAcc),
                        WhtRate = whtRate,
                        VatClaimable = claim
                    };
                    decimal qty = it.Quantity.HasValue && it.Quantity.Value > 0 ? it.Quantity.Value : 1m;
                    decimal disc = it.LineDiscountAmount ?? 0m;
                    decimal amount = it.Amount ?? OcrReviewMath.R2(qty * (it.UnitPrice ?? 0m) - disc);
                    decimal unit = it.UnitPrice ?? (qty != 0 ? amount / qty : amount);
                    if (it.PriceIncludesVat)
                    {
                        // ราคารวม VAT บนกระดาษ → ถอด 7% (UnitPrice 4 ตำแหน่ง; ยอดบรรทัดคิด round(qty×unit) ตรงกับ NextAcc)
                        unit = Math.Round(unit / 1.07m, 4, MidpointRounding.AwayFromZero);
                        disc = OcrReviewMath.R2(disc / 1.07m);
                    }
                    l.Qty = qty;
                    l.UnitPrice = unit;
                    l.Discount = disc;
                    decimal rate = it.VatRate ?? (hasVat ? 7m : 0m);
                    l.VatType = rate < 0 ? "EX" : rate == 0m ? "0" : "7";
                    if (l.VatType != "7") l.VatClaimable = true;
                    if (string.IsNullOrEmpty(l.Description)) l.Description = "รายการตามเอกสาร";
                    s.Lines.Add(l);
                }
                // ส่วนลดท้ายบิลที่บรรทัดยังไม่รวม → ใส่ที่บรรทัดยอดสูงสุด (ผู้ใช้ปรับได้)
                decimal hdrDisc = r.ExtractedDiscountAmount ?? 0m;
                if (hdrDisc > 0m && s.PrintedSubTotal.HasValue)
                {
                    var t0 = OcrReviewMath.CalcTotals(s.Lines);
                    if (Math.Abs(t0.SubTotal - hdrDisc - s.PrintedSubTotal.Value) <= OcrReviewMath.Tolerance)
                    {
                        var big = s.Lines.OrderByDescending(x => OcrReviewMath.CalcLine(x).Net).First();
                        big.Discount = OcrReviewMath.R2(big.Discount + hdrDisc);
                    }
                }
            }
            else
            {
                s.Lines.Add(new OcrReviewLine
                {
                    Description = FirstNonEmpty(r.SuggestedAccounts?.DebitAccountName, "ค่าใช้จ่ายตามเอกสาร") + (string.IsNullOrEmpty(s.SupplierName) ? "" : " - " + s.SupplierName),
                    Qty = 1m,
                    UnitPrice = sub ?? 0m,
                    AccountCode = defAcc,
                    OcrAccountCode = defAcc,
                    VatType = hasVat ? "7" : "0",
                    VatClaimable = claim || !hasVat,
                    WhtRate = whtRate
                });
            }

            // VAT รายบรรทัดให้ตรงยอด VAT บนกระดาษ (เศษสตางค์) — ถ้าต่างไม่เกิน 1 บาท ตั้ง override ที่บรรทัดภาษีที่ใหญ่สุด
            if (hasVat)
            {
                var t1 = OcrReviewMath.CalcTotals(s.Lines);
                decimal diff = OcrReviewMath.R2(vat.Value - t1.Vat);
                if (diff != 0m && Math.Abs(diff) <= 1.00m)
                {
                    var taxed = s.Lines.Where(x => x.VatType == "7").OrderByDescending(x => OcrReviewMath.CalcLine(x).Net).FirstOrDefault();
                    if (taxed != null) taxed.VatOverride = OcrReviewMath.R2(OcrReviewMath.CalcLine(taxed).Vat + diff);
                }
            }

            // ช่องที่ OCR ไม่มั่นใจ (< 0.70) — หน้าเว็บไฮไลต์ให้ตรวจ
            if (r.FieldConfidence != null)
                foreach (var kv in r.FieldConfidence)
                    if (kv.Value < 0.70 && !string.IsNullOrEmpty(kv.Key)) s.LowConfidence.Add(kv.Key);
            if (r.HasHandwriting) s.LowConfidence.Add("TotalAmount");

            if (OcrDocKind.IsNote(s.DocKind))
            {
                s.SourceMode = OcrNoteSource.NextAcc;
                if (r.LinkedPredecessorDocumentId.HasValue)
                {
                    s.SourceDocId = r.LinkedPredecessorDocumentId.Value.ToString();
                    s.SourceDocNo = r.LinkedPredecessorNumber;
                    s.SourceDocType = r.LinkedPredecessorType;
                }
                s.SearchText = s.SupplierName;
            }
            return s;
        }

        private static string KindFromOcr(OcrResultResponse r, string hintKind)
        {
            string t = (r.TargetDocumentType ?? "") + "|" + (r.ScannedDocumentType ?? "") + "|" + (r.DocumentType ?? "");
            if (t.IndexOf("CreditNote", StringComparison.OrdinalIgnoreCase) >= 0) return OcrDocKind.VendorCreditNote;
            if (t.IndexOf("DebitNote", StringComparison.OrdinalIgnoreCase) >= 0) return OcrDocKind.VendorDebitNote;
            if (OcrDocKind.IsKnown(hintKind)) return hintKind;
            if (string.Equals(r.TargetDocumentType, "Expense", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.TargetDocumentType, "PurchaseInvoice", StringComparison.OrdinalIgnoreCase))
                return OcrDocKind.Expense;
            return OcrDocKind.PaymentVoucher;
        }

        /// <summary>กระดาษเป็นใบกำกับภาษี (ไม่นับ "Invoice" ที่เป็นชนิดเอกสารที่จะสร้าง)</summary>
        public static bool IsTaxInvoicePaper(OcrResultResponse r)
        {
            if (r == null) return false;
            string t = ((r.ScannedDocumentType ?? "") + "|" + (r.DocumentType ?? "")).ToLowerInvariant();
            return t.Contains("taxinvoice") || t.Contains("tax invoice") || t.Contains("tax_invoice") || t.Contains("ใบกำกับ")
                || t.Contains("creditnote") || t.Contains("debitnote");
        }

        // ──────────────────────────────────────────────────────────────
        // NextAcc: ค้นใบต้นทาง / กันซ้ำ
        // ──────────────────────────────────────────────────────────────

        private static readonly int[] PurchaseSourceTypes = { 8, 9, 13 };   // PI / Expense / PV (DocumentService.cs:12136-12160)

        private static string TypeLabel(int t)
        {
            switch (t)
            {
                case 8: return "ใบแจ้งหนี้ซื้อ";
                case 9: return "ค่าใช้จ่าย";
                case 13: return "ใบสำคัญจ่าย";
                case 6: return "ใบลดหนี้";
                case 5: return "ใบเพิ่มหนี้";
                case 15: return "ใบรับรองแทนใบเสร็จ";
                default: return "เอกสาร " + t;
            }
        }

        private static bool IsLiveStatus(int status) => status != 0 && status != 6 && status != 8;   // ไม่ใช่ ร่าง/ยกเลิก/ถูกปฏิเสธ

        /// <summary>ผลรวมใบลดหนี้ที่อนุมัติแล้วซึ่งอ้างใบนี้ — ตัวนับเดียวกับเพดาน NextAcc (DocumentService.cs:11922-11927)</summary>
        public static async Task<decimal> SumCreditedAsync(AccountingApiClient client, Guid sourceId)
        {
            var r = await client.ListDocumentsAsync(new[] { 6 }, null, null, sourceId, 100);
            return OcrReviewMath.R2((r?.data?.Items ?? new List<DocumentResponse>())
                .Where(d => d.RelatedDocumentId == sourceId && IsLiveStatus(d.Status)).Sum(d => d.TotalAmount));
        }

        /// <summary>ค้นใบต้นทาง (PI/Expense/PV ที่อนุมัติแล้ว) ของผู้ขาย — คำค้น = เลขที่เอกสาร/ชื่อผู้ขาย · ผู้ติดต่อที่ OCR จับคู่ได้ช่วยกรอง
        /// + ใบที่ NextAcc จัดอันดับให้สแกน (predecessor-candidates) เมื่อมี</summary>
        public static async Task<List<OcrSourceCandidate>> SearchSourceDocsAsync(AccountingApiClient client, OcrReviewState s, List<string> notes)
        {
            var map = new Dictionary<Guid, OcrSourceCandidate>();
            Guid contactId;
            bool hasContact = Guid.TryParse(s.SourceContactId ?? "", out contactId) && contactId != Guid.Empty;
            string text = (s.SearchText ?? "").Trim();

            var batches = new List<List<DocumentResponse>>();
            try
            {
                if (text.Length > 0)
                    batches.Add((await client.ListDocumentsAsync(PurchaseSourceTypes, null, text, null, 30))?.data?.Items);
                if (hasContact)
                    batches.Add((await client.ListDocumentsAsync(PurchaseSourceTypes, contactId, null, null, 30))?.data?.Items);
                if (text.Length == 0 && !hasContact && !string.IsNullOrWhiteSpace(s.SupplierName))
                    batches.Add((await client.ListDocumentsAsync(PurchaseSourceTypes, null, s.SupplierName.Trim(), null, 30))?.data?.Items);
            }
            catch (Exception ex) { notes.Add("ค้นเอกสารใน NextAcc ไม่สำเร็จ: " + ShortError(ex)); }

            foreach (var b in batches)
            {
                if (b == null) continue;
                foreach (var d in b)
                {
                    if (d == null || map.ContainsKey(d.Id) || !IsLiveStatus(d.Status)) continue;
                    if (!PurchaseSourceTypes.Contains(d.DocumentType)) continue;
                    map[d.Id] = new OcrSourceCandidate
                    {
                        Id = d.Id.ToString(), No = d.DocumentNumber, Type = d.DocumentType.ToString(CultureInfo.InvariantCulture),
                        TypeLabel = TypeLabel(d.DocumentType), Date = d.DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Total = d.TotalAmount, BalanceDue = d.BalanceDue,
                        ContactId = d.Contact?.Id.ToString(), ContactName = d.Contact?.Name, SupplierDocNo = d.SupplierInvoiceNumber
                    };
                }
            }

            // ผู้สมัครที่ NextAcc จัดอันดับ (เมื่อสแกนนี้ถูกตีเป็นใบลด/เพิ่มหนี้อยู่แล้ว)
            Guid scanId;
            if (Guid.TryParse(s.ScanId ?? "", out scanId))
            {
                try
                {
                    var pc = await client.GetOcrPredecessorCandidatesAsync(scanId);
                    foreach (var c in pc?.data ?? new List<OcrPredecessorCandidateDto>())
                    {
                        OcrSourceCandidate ex;
                        if (map.TryGetValue(c.DocumentId, out ex)) { ex.Reason = c.Reason; continue; }
                        int type = TypeFromName(c.DocumentType);
                        if (!PurchaseSourceTypes.Contains(type)) continue;
                        map[c.DocumentId] = new OcrSourceCandidate
                        {
                            Id = c.DocumentId.ToString(), No = c.DocumentNumber, Type = type.ToString(CultureInfo.InvariantCulture), TypeLabel = TypeLabel(type),
                            Date = c.DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Total = c.TotalAmount, BalanceDue = c.BalanceDue,
                            Reason = "NextAcc แนะนำ: " + c.Reason
                        };
                    }
                }
                catch { /* ไม่มีผู้สมัคร/สแกนยังไม่ใช่ใบลดหนี้ — ข้าม */ }
            }

            var list = map.Values.OrderByDescending(x => x.Reason != null).ThenByDescending(x => x.Date).Take(12).ToList();
            foreach (var c in list)
            {
                try
                {
                    c.Credited = await SumCreditedAsync(client, Guid.Parse(c.Id));
                }
                catch { c.Credited = 0m; }
                c.Remaining = OcrReviewMath.R2(c.Total - c.Credited);
            }
            if (list.Count == 0) notes.Add("ไม่พบเอกสารต้นทางที่ตรงคำค้น — ลองค้นด้วยเลขที่เอกสาร NextAcc หรือชื่อผู้ขาย หรือเลือก \"ใบเดิมอยู่นอกระบบ\"");
            return list;
        }

        private static int TypeFromName(string name)
        {
            switch ((name ?? "").Trim())
            {
                case "PurchaseInvoice": return 8;
                case "Expense": return 9;
                case "PaymentVoucher": return 13;
                case "CreditNote": return 6;
                case "DebitNote": return 5;
                default: int v; return int.TryParse(name, out v) ? v : 0;
            }
        }

        private static int[] DuplicateTypes(string kind)
        {
            if (kind == OcrDocKind.VendorCreditNote) return new[] { 6 };
            if (kind == OcrDocKind.VendorDebitNote) return new[] { 5 };
            return new[] { 8, 9, 13, 15 };
        }

        /// <summary>กันซ้ำฝั่ง NextAcc: เอกสารของผู้ติดต่อรายเดียวกันที่ SupplierInvoiceNumber ตรงกัน (ไม่นับใบที่ยกเลิก/ใบของเราเอง)</summary>
        public static async Task<DocumentResponse> FindNextAccDuplicateAsync(AccountingApiClient client, Guid contactId, OcrReviewState s, Guid? excludeDocId)
        {
            string norm = OcrReviewMath.NormalizeDocNo(s.SupplierDocNo);
            if (norm.Length == 0 || contactId == Guid.Empty) return null;
            var r = await client.ListDocumentsAsync(DuplicateTypes(s.DocKind), contactId, null, null, 100);
            return (r?.data?.Items ?? new List<DocumentResponse>()).FirstOrDefault(d =>
                d.Status != 6 && (!excludeDocId.HasValue || d.Id != excludeDocId.Value)
                && OcrReviewMath.NormalizeDocNo(d.SupplierInvoiceNumber) == norm);
        }

        // ──────────────────────────────────────────────────────────────
        // ส่ง NextAcc
        // ──────────────────────────────────────────────────────────────

        /// <summary>บรรทัดที่ยืนยัน → DocumentLineRequest (ราคาไม่รวม VAT; VAT ทุกบรรทัดที่มีภาษีส่งเป็น VatAmountOverride ⇒ ยอดใน NextAcc = ยอดที่ผู้ใช้เห็น)</summary>
        public static List<DocumentLineRequest> ToLines(OcrReviewState s)
        {
            var list = new List<DocumentLineRequest>();
            foreach (var l in s.Lines ?? new List<OcrReviewLine>())
            {
                var c = OcrReviewMath.CalcLine(l);
                bool taxed = c.NextAccVatRate > 0;
                bool claim = !taxed || l.VatClaimable;
                list.Add(new DocumentLineRequest
                {
                    Description = Cut((l.Description ?? "").Trim(), 500),
                    Quantity = l.Qty,
                    UnitPrice = l.UnitPrice,
                    DiscountPercent = 0m,
                    DiscountAmount = l.Discount > 0m ? (decimal?)OcrReviewMath.R2(l.Discount) : null,
                    VatRate = c.NextAccVatRate,
                    WithholdingTaxRate = l.WhtRate,
                    AccountCode = (l.AccountCode ?? "").Trim(),
                    IsVatClaimable = claim,
                    VatNonClaimableReason = claim ? null : "ไม่ขอเครดิตภาษีซื้อ — รวม VAT เข้าค่าใช้จ่าย (§82/5)",
                    VatAmountOverride = taxed ? (decimal?)c.Vat : null,
                    IncomeTypeCode = string.IsNullOrWhiteSpace(l.IncomeType) ? null : l.IncomeType.Trim()
                });
            }
            return list;
        }

        private static string Notes(OcrReviewState s)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(s.Notes)) parts.Add(s.Notes.Trim());
            if (OcrDocKind.IsNote(s.DocKind) && !string.IsNullOrWhiteSpace(s.NoteReasonText)) parts.Add("เหตุผล: " + s.NoteReasonText.Trim());
            parts.Add("สร้างจากหน้าตรวจทาน OCR (TakeTime)");
            return string.Join(" · ", parts);
        }

        private static UpdateDocumentRequest BuildUpdate(OcrReviewState s, OcrSubmitContext ctx)
        {
            bool note = OcrDocKind.IsNote(s.DocKind);
            var claimsVat = (s.Lines ?? new List<OcrReviewLine>()).Any(l => l.VatClaimable && OcrReviewMath.CalcLine(l).Vat > 0);
            var upd = new UpdateDocumentRequest
            {
                DocumentDate = OcrReviewMath.ParseDate(s.DocDate),
                Lines = ToLines(s),
                PricesIncludeVat = false,
                SupplierInvoiceNumber = string.IsNullOrWhiteSpace(s.SupplierDocNo) ? null : s.SupplierDocNo.Trim(),
                Reference = string.IsNullOrWhiteSpace(s.SupplierDocNo) ? null : s.SupplierDocNo.Trim(),
                SupplierTaxInvoiceDate = OcrReviewMath.ParseDate(s.TaxInvoiceDate) ?? (s.FullTaxInvoice ? OcrReviewMath.ParseDate(s.SupplierDocDate) : null),
                SupplierBranchCode = OcrReviewMath.IsValidBranch(OcrReviewMath.NormalizeBranch(s.SupplierBranch)) ? OcrReviewMath.NormalizeBranch(s.SupplierBranch) : null,
                HasTaxInvoiceReference = s.FullTaxInvoice && (claimsVat || note),
                Notes = Notes(s),
                PreparerName = ctx?.PreparerName,
                PreparerSignatureBase64 = ctx?.PreparerSignature
            };
            if (s.DocKind == OcrDocKind.PaymentVoucher)
            {
                upd.PaymentType = 1;   // Cash — จ่ายแล้ว (AllEnums.cs:575)
                Guid pay;
                if (Guid.TryParse(s.PaidHowAccountId ?? "", out pay) && pay != Guid.Empty) upd.PaymentAccountId = pay;
            }
            else if (s.DocKind == OcrDocKind.Expense) upd.PaymentType = 2;   // Credit — ตั้งหนี้ (AllEnums.cs:576)
            else
            {
                upd.CnDnPurchaseSideOverride = true;
                if (s.DocKind == OcrDocKind.VendorCreditNote) upd.CreditNoteReason = s.NoteReason;
                else upd.DebitNoteReason = s.NoteReason;
            }
            return upd;
        }

        /// <summary>
        /// ส่งเอกสารที่ยืนยันแล้วไป NextAcc — ลำดับและการทำต่อจากขั้นที่ค้างอธิบายใน CLAUDE.md ข้อ 3 (หน้าตรวจทานก่อนส่ง)
        /// </summary>
        public async Task<OcrSubmitResult> SubmitAsync(AccountingApiClient client, OcrReviewState s, OcrSubmitContext ctx)
        {
            var res = new OcrSubmitResult();
            Guid scanId;
            if (!Guid.TryParse(s?.ScanId ?? "", out scanId)) { res.Errors.Add("ไม่พบรหัสสแกน"); return res; }
            client.IdempotencyContext = "OCR-" + scanId.ToString("N");   // POST สร้างซ้ำ body เดิม = NextAcc replay (item 19)
            var row = Load(scanId);
            Guid? docId = row?.NexaaccDocId;
            string user = ctx?.UserName;
            bool note = OcrDocKind.IsNote(s.DocKind);

            // ── ใบต้นทาง (ดึงสดจาก NextAcc — ค่าในหน้าเว็บใช้แสดงผลเท่านั้น) ──
            DocumentResponse source = null;
            if (note && s.SourceMode == OcrNoteSource.NextAcc)
            {
                Guid sid;
                if (Guid.TryParse(s.SourceDocId ?? "", out sid) && sid != Guid.Empty)
                {
                    try
                    {
                        source = (await client.GetDocumentAsync(sid))?.data;
                        if (source != null)
                        {
                            s.SourceDocNo = source.DocumentNumber;
                            s.SourceTotal = source.TotalAmount;
                            s.SourceBalanceDue = source.BalanceDue;
                            s.SourceContactId = source.Contact?.Id.ToString();
                            s.SourceContactName = source.Contact?.Name;
                            s.SourceCredited = await SumCreditedAsync(client, sid);
                            if (docId.HasValue)   // ส่งซ้ำหลังสร้างใบนี้แล้ว: อย่านับใบนี้เป็น "ลดหนี้แล้ว"
                            {
                                try
                                {
                                    var mine = (await client.GetDocumentAsync(docId.Value))?.data;
                                    if (mine != null && IsLiveStatus(mine.Status) && mine.RelatedDocumentId == sid)
                                        s.SourceCredited = OcrReviewMath.R2(Math.Max(0m, s.SourceCredited - mine.TotalAmount));
                                }
                                catch { }
                            }
                        }
                    }
                    catch (Exception ex) { res.Errors.Add("อ่านเอกสารต้นทางจาก NextAcc ไม่สำเร็จ: " + ShortError(ex)); return res; }
                    if (source == null) { res.Errors.Add("ไม่พบเอกสารต้นทางใน NextAcc"); return res; }
                    if (source.Status == 6) { res.Errors.Add("เอกสารต้นทาง " + source.DocumentNumber + " ถูกยกเลิกแล้ว — ออกใบลด/เพิ่มหนี้อ้างใบที่ยกเลิกไม่ได้ (§86/9-10)"); return res; }
                    if (!PurchaseSourceTypes.Contains(source.DocumentType)) { res.Errors.Add("เอกสารต้นทางต้องเป็นใบแจ้งหนี้ซื้อ / ค่าใช้จ่าย / ใบสำคัญจ่าย"); return res; }
                    if (source.Contact == null || source.Contact.Id == Guid.Empty) { res.Errors.Add("เอกสารต้นทาง " + source.DocumentNumber + " ไม่มีผู้ติดต่อ — ใบลด/เพิ่มหนี้ต้องออกให้คู่ค้ารายเดียวกับใบเดิม (§86/9-10)"); return res; }
                }
            }

            // ── ตรวจทั้งใบ (ตัวตัดสินฝั่งเซิร์ฟเวอร์) ──
            var v = OcrReviewMath.Validate(s);
            if (!v.CanSubmit) { res.Errors.AddRange(v.Errors); return res; }
            res.Notes.AddRange(v.Warnings);

            // ── กันซ้ำ (ข้ามเมื่อสแกนนี้มีเอกสารบน NextAcc แล้ว = กำลังทำต่อ) ──
            if (!docId.HasValue)
            {
                var dupLocal = FindLocalDuplicate(s);
                if (dupLocal != null)
                {
                    res.Errors.Add("เอกสารซ้ำ: เลขที่ " + s.SupplierDocNo + " ของผู้ขายรายนี้ถูกบันทึกจากหน้านี้แล้ว"
                        + (string.IsNullOrEmpty(dupLocal.NexaaccDocNo) ? " (สถานะ " + dupLocal.Status + ")" : " เป็น " + dupLocal.NexaaccDocNo));
                    res.DuplicateDocId = dupLocal.NexaaccDocId; res.DuplicateDocNo = dupLocal.NexaaccDocNo;
                    return res;
                }
            }

            // ── คืนสินค้าที่ลงด้วย PRT- แล้ว = เก็บหลักฐาน ไม่สร้างใบลดหนี้ซ้ำ ──
            if (note && s.SourceMode == OcrNoteSource.PurchaseReturn)
            {
                var exists = PurchaseReturnExists(s.ReturnRef);
                if (exists == false) { res.Errors.Add("ไม่พบการคืนสินค้าเลข " + s.ReturnRef + " ในหน้า \"คืนสินค้าให้ผู้ขาย\""); return res; }
                SaveDraft(s, null, null, user);
                SetStatus(scanId, StEvidence, null, null, null, user);
                res.Ok = true; res.Evidence = true;
                res.Message = "บันทึกใบลดหนี้ " + s.SupplierDocNo + " เป็นหลักฐานของการคืนสินค้า " + s.ReturnRef
                    + " แล้ว — ไม่สร้างใบลดหนี้ใน NextAcc ซ้ำ (การคืนสินค้าโพสต์ GL: เจ้าหนี้/เงินคืน · สินค้าคงเหลือ · ภาษีซื้อ ไว้ครบแล้ว)"
                    + (exists == null ? " · ⚠ ตรวจเลข PRT- อัตโนมัติไม่ได้ (ยังไม่รัน PHASE19_31)" : "");
                return res;
            }

            SaveDraft(s, null, null, user);

            // ── สแกนนี้สร้างเอกสารไปแล้วหรือยัง (ตัวยืนยันฝั่ง NextAcc — OcrService.cs:6665-6668) ──
            if (!docId.HasValue)
            {
                try
                {
                    var scan = (await client.GetOcrResultAsync(scanId))?.data;
                    if (scan?.CreatedDocumentId != null && scan.CreatedDocumentId.Value != Guid.Empty)
                    {
                        docId = scan.CreatedDocumentId.Value;
                        res.Notes.Add("สแกนนี้มีเอกสารร่างบน NextAcc อยู่แล้ว — ใช้ใบเดิม (ไม่สร้างใหม่)");
                    }
                }
                catch { }
            }

            bool directCreate = note && s.SourceMode == OcrNoteSource.NextAcc && source != null;
            bool createdNow = false;

            // ── กันซ้ำฝั่ง NextAcc (ผู้ติดต่อรายเดียวกัน + เลขที่ใบผู้ขายเดียวกัน) ──
            Guid dupContact = Guid.Empty;
            if (directCreate) Guid.TryParse(source.Contact?.Id.ToString() ?? "", out dupContact);
            else Guid.TryParse(s.SourceContactId ?? "", out dupContact);
            if (dupContact != Guid.Empty)
            {
                try
                {
                    var dup = await FindNextAccDuplicateAsync(client, dupContact, s, docId);
                    if (dup != null)
                    {
                        res.Errors.Add("เอกสารซ้ำใน NextAcc: " + dup.DocumentNumber + " ของผู้ขายรายนี้ใช้เลขที่ " + dup.SupplierInvoiceNumber + " แล้ว");
                        res.DuplicateDocId = dup.Id; res.DuplicateDocNo = dup.DocumentNumber;
                        return res;
                    }
                }
                catch (Exception ex) { res.Notes.Add("ตรวจซ้ำกับ NextAcc ไม่สำเร็จ (" + ShortError(ex) + ") — ด่านกันซ้ำของ NextAcc ยังทำงานตอนสร้าง"); }
            }

            // ── 1) สร้างเอกสาร ──
            if (!docId.HasValue)
            {
                try
                {
                    if (directCreate)
                    {
                        var upd = BuildUpdate(s, ctx);
                        var req = new CreateDocumentRequest
                        {
                            DocumentType = OcrDocKind.NextAccType(s.DocKind),
                            DocumentDate = upd.DocumentDate ?? DateTime.Today,
                            ContactId = source.Contact.Id,          // คู่ค้าต้องตรงใบต้นทาง (DocumentService.cs:17773-17775)
                            RelatedDocumentId = source.Id,          // ตั้งได้ตอน create เท่านั้น
                            CnDnPurchaseSideOverride = true,
                            CreditNoteReason = upd.CreditNoteReason,
                            DebitNoteReason = upd.DebitNoteReason,
                            Reference = upd.Reference,
                            Notes = upd.Notes,
                            Lines = upd.Lines,
                            PricesIncludeVat = false,
                            SupplierInvoiceNumber = upd.SupplierInvoiceNumber,
                            SupplierTaxInvoiceDate = upd.SupplierTaxInvoiceDate,
                            SupplierBranchCode = upd.SupplierBranchCode,
                            HasTaxInvoiceReference = upd.HasTaxInvoiceReference,
                            PreparerName = upd.PreparerName,
                            PreparerSignatureBase64 = upd.PreparerSignatureBase64
                        };
                        var created = await client.CreateDocumentAsync(req);
                        docId = created?.data?.Id;
                        if (!docId.HasValue || docId.Value == Guid.Empty) throw new Exception("NextAcc ไม่คืนรหัสเอกสาร: " + (created?.message ?? ""));
                        createdNow = true;
                        SetStatus(scanId, StCreated, docId, created.data.DocumentNumber, null, user);
                        try { await client.LinkOcrScanToDocumentAsync(scanId, docId.Value); res.Notes.Add("ผูกไฟล์สแกนต้นฉบับเข้าเอกสารแล้ว"); }
                        catch (Exception lex) { res.Notes.Add("ผูกไฟล์สแกนเข้าเอกสารไม่สำเร็จ (จะแนบไฟล์แทน): " + ShortError(lex)); }
                    }
                    else
                    {
                        await TryCorrectScanAsync(client, scanId, s, res);
                        var created = await client.CreateDocumentFromOcrAsync(scanId, OcrDocKind.OcrTargetType(s.DocKind), s.AllowDuplicate);
                        docId = created?.data?.CreatedDocumentId;
                        if (!docId.HasValue || docId.Value == Guid.Empty) throw new Exception("NextAcc ไม่คืนรหัสเอกสาร: " + (created?.message ?? ""));
                        createdNow = true;
                        SetStatus(scanId, StCreated, docId, null, null, user);
                    }
                }
                catch (Exception ex)
                {
                    string msg = ApiMessage(ex);
                    bool dupWarn = msg.Contains("ซ้ำ") || msg.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0;
                    res.Errors.Add("สร้างเอกสารบน NextAcc ไม่สำเร็จ: " + msg
                        + (dupWarn && !s.AllowDuplicate ? " — ถ้าตรวจแล้วว่าไม่ใช่ใบซ้ำ ให้ติ๊ก \"ยืนยันว่าไม่ใช่ใบซ้ำ\" แล้วส่งอีกครั้ง" : ""));
                    SetStatus(scanId, StFailed, null, null, msg, user);
                    return res;
                }
            }

            // ── 2) อ่านสถานะ แล้วแก้ร่างให้ตรงค่าที่ยืนยัน ──
            DocumentResponse doc = null;
            try { doc = (await client.GetDocumentAsync(docId.Value))?.data; }
            catch (Exception ex) { res.Notes.Add("อ่านเอกสารหลังสร้างไม่สำเร็จ: " + ShortError(ex)); }
            int status = doc?.Status ?? 0;
            bool editable = status == 0 || status == 1 || status == 8;   // ร่าง / รออนุมัติ / ถูกตีกลับ
            if (status == 6)
            {
                res.Errors.Add("เอกสาร " + (doc?.DocumentNumber ?? docId.ToString()) + " บน NextAcc ถูกยกเลิกแล้ว — สแกนเอกสารใหม่เพื่อสร้างอีกครั้ง");
                SetStatus(scanId, StFailed, docId, doc?.DocumentNumber, "เอกสารบน NextAcc ถูกยกเลิก", user);
                return res;
            }

            if (editable && (!directCreate || !createdNow))
            {
                try
                {
                    var upd = BuildUpdate(s, ctx);
                    if (!directCreate && s.VendorId > 0)
                    {
                        var sync = new AccountingSyncService(_conn);
                        Guid? cid = await sync.EnsureVendorContactAsync(s.VendorId);
                        if (cid.HasValue && cid.Value != Guid.Empty) upd.ContactId = cid.Value;
                        else res.Notes.Add("ผูกผู้ขายจากระบบไม่สำเร็จ — ใช้ผู้ติดต่อที่ NextAcc สร้างจากสแกน");
                    }
                    await client.UpdateDocumentAsync(docId.Value, upd);
                    SetStatus(scanId, StUpdated, docId, doc?.DocumentNumber, null, user);
                }
                catch (Exception ex)
                {
                    string msg = ApiMessage(ex);
                    res.Errors.Add("สร้างร่างบน NextAcc แล้ว แต่ปรับให้ตรงค่าที่ยืนยันไม่สำเร็จ: " + msg + " — กดส่งอีกครั้งเพื่อลองใหม่ (ใช้ร่างเดิม ไม่สร้างซ้ำ)");
                    SetStatus(scanId, StFailed, docId, doc?.DocumentNumber, msg, user);
                    res.DocId = docId;
                    return res;
                }
            }

            // ── 3) เติมที่อยู่/สาขา/เลขผู้เสียภาษีของผู้ติดต่อเมื่อว่าง (§86/4 อ่านจาก Contact — TaxInvoiceCompletenessChecker.cs:40-47) ──
            if (editable) await TryEnrichContactAsync(client, docId.Value, s, res);

            // ── 4) อนุมัติ ──
            if (editable)
            {
                try
                {
                    var ap = await client.ApproveDocumentAsync(docId.Value, new ApproveDocumentRequest
                    {
                        AcknowledgeWarnings = ctx != null && ctx.AcknowledgeWarnings,
                        Notes = ctx != null && ctx.AcknowledgeWarnings ? "ผู้ใช้ยืนยันคำเตือนในหน้าตรวจทาน OCR (TakeTime)" : null
                    });
                    if (ap?.data != null && !string.IsNullOrEmpty(ap.data.DocumentNumber)) res.DocNo = ap.data.DocumentNumber;
                }
                catch (AccountingApiException ae) when (ae.StatusCode == 422)
                {
                    res.NeedsAck = true;
                    res.ApproveWarnings.AddRange(ParseApproveWarnings(ae.ResponseBody));
                    if (res.ApproveWarnings.Count == 0) res.ApproveWarnings.Add(ApiMessage(ae));
                    res.DocId = docId;
                    res.Message = "NextAcc มีคำเตือนก่อนอนุมัติ — อ่านแล้วกด \"ยืนยันแม้มีคำเตือน\" หรือกลับไปแก้ข้อมูล (เอกสารยังเป็นร่าง)";
                    SetStatus(scanId, StUpdated, docId, doc?.DocumentNumber, "รอยืนยันคำเตือน: " + string.Join(" | ", res.ApproveWarnings), user);
                    return res;
                }
                catch (Exception ex)
                {
                    string msg = ApiMessage(ex);
                    res.Errors.Add("อนุมัติเอกสารบน NextAcc ไม่สำเร็จ: " + msg + " (เอกสารยังเป็นร่าง — แก้แล้วกดส่งอีกครั้ง)");
                    SetStatus(scanId, StFailed, docId, doc?.DocumentNumber, msg, user);
                    res.DocId = docId;
                    return res;
                }
            }
            if (string.IsNullOrEmpty(res.DocNo))
            {
                try { doc = (await client.GetDocumentAsync(docId.Value))?.data; res.DocNo = doc?.DocumentNumber; } catch { }
            }
            SetStatus(scanId, StApproved, docId, res.DocNo, null, user);

            // ── 5) หลังอนุมัติ: แนบไฟล์ / 50 ทวิ / ย้ายขาเงินของใบลด-เพิ่มหนี้ ──
            await TryAttachScanAsync(client, docId.Value, ctx?.ScanFilePath, res);
            if (s.DocKind == OcrDocKind.PaymentVoucher && v.Totals.Wht > 0m) await TryWhtCertAsync(client, docId.Value, res);
            string reclassRef = null;
            if (note) reclassRef = await TryReclassNoteMoneyLegAsync(client, docId.Value, scanId, s, res);

            var raw = ParseState(row?.OcrRawJson);
            string corrections = ToJson(OcrReviewMath.DiffCorrections(raw, s));
            SetStatus(scanId, StDone, docId, res.DocNo, null, user, corrections, reclassRef);
            res.Ok = true;
            res.DocId = docId;
            res.Message = "สร้างและอนุมัติ" + OcrDocKind.Label(s.DocKind) + " บน NextAcc แล้ว เลขที่ " + (res.DocNo ?? docId.ToString());
            _code.Logs(_conn, "OcrReview", "scan=" + scanId + " kind=" + s.DocKind + " doc=" + (res.DocNo ?? docId.ToString())
                + " total=" + OcrReviewMath.Money(v.Totals.Total) + " wht=" + OcrReviewMath.Money(v.Totals.Wht), user ?? "SYSTEM");
            return res;
        }

        private static async Task TryCorrectScanAsync(AccountingApiClient client, Guid scanId, OcrReviewState s, OcrSubmitResult res)
        {
            try
            {
                var t = OcrReviewMath.CalcTotals(s.Lines);
                decimal whtRate = (s.Lines ?? new List<OcrReviewLine>()).Select(l => l.WhtRate).DefaultIfEmpty(0m).Max();
                string br = OcrReviewMath.NormalizeBranch(s.SupplierBranch);
                await client.SubmitOcrCorrectionAsync(scanId, new OcrCorrectionRequest
                {
                    VendorName = string.IsNullOrWhiteSpace(s.SupplierName) ? null : s.SupplierName.Trim(),
                    VendorTaxId = OcrReviewMath.IsValidThaiTaxId(s.SupplierTaxId) ? OcrReviewMath.Digits(s.SupplierTaxId) : null,
                    DocumentNumber = string.IsNullOrWhiteSpace(s.SupplierDocNo) ? null : s.SupplierDocNo.Trim(),
                    DocumentDate = OcrReviewMath.ParseDate(s.SupplierDocDate) ?? OcrReviewMath.ParseDate(s.DocDate),
                    SubTotal = t.SubTotal,
                    VatAmount = t.Vat,
                    TotalAmount = t.Total,
                    HasWht = whtRate > 0m,
                    WhtRate = whtRate > 0m ? (decimal?)whtRate : null,
                    TargetDocumentType = OcrDocKind.OcrTargetType(s.DocKind),
                    OurRole = "Buyer",
                    VendorBranchCode = OcrReviewMath.IsValidBranch(br) ? br : null,
                    VendorBranchConfirmed = OcrReviewMath.IsValidBranch(br) ? (bool?)true : null,
                    VendorAddress = string.IsNullOrWhiteSpace(s.SupplierAddress) ? null : s.SupplierAddress.Trim(),
                    Notes = string.IsNullOrWhiteSpace(s.Notes) ? null : s.Notes.Trim()
                });
            }
            catch (Exception ex)
            {
                // ไม่บล็อก: ค่าที่ยืนยันทั้งหมดถูกส่งซ้ำใน PUT ของเอกสารอยู่แล้ว
                res.Notes.Add("บันทึกค่าที่แก้กลับไปที่ผลสแกน NextAcc ไม่สำเร็จ (" + ShortError(ex) + ") — ไม่กระทบเอกสาร");
            }
        }

        private static async Task TryEnrichContactAsync(AccountingApiClient client, Guid docId, OcrReviewState s, OcrSubmitResult res)
        {
            try
            {
                var doc = (await client.GetDocumentAsync(docId))?.data;
                if (doc?.Contact == null || doc.Contact.Id == Guid.Empty) return;
                var c = (await client.GetContactAsync(doc.Contact.Id))?.data;
                if (c == null) return;
                var upd = new UpdateContactRequest();
                bool any = false;
                if (string.IsNullOrWhiteSpace(c.Address) && !string.IsNullOrWhiteSpace(s.SupplierAddress)) { upd.Address = s.SupplierAddress.Trim(); any = true; }
                string br = OcrReviewMath.NormalizeBranch(s.SupplierBranch);
                if (string.IsNullOrWhiteSpace(c.BranchCode) && OcrReviewMath.IsValidBranch(br)) { upd.BranchCode = br; any = true; }
                if (string.IsNullOrWhiteSpace(c.TaxId) && OcrReviewMath.IsValidThaiTaxId(s.SupplierTaxId)) { upd.TaxId = OcrReviewMath.Digits(s.SupplierTaxId); any = true; }
                if (!any) return;
                // null = คงค่าเดิม (DocumentService.cs:13897-13916) — ไม่ทับข้อมูลที่ผู้ติดต่อมีอยู่แล้ว
                await client.UpdateContactAsync(c.Id, upd);
                res.Notes.Add("เติมข้อมูลผู้ขายที่ขาดใน NextAcc (" + string.Join("/", new[] { upd.Address != null ? "ที่อยู่" : null, upd.BranchCode != null ? "สาขา" : null, upd.TaxId != null ? "เลขผู้เสียภาษี" : null }.Where(x => x != null)) + ")");
            }
            catch (Exception ex) { res.Notes.Add("เติมข้อมูลผู้ขายใน NextAcc ไม่สำเร็จ: " + ShortError(ex)); }
        }

        private static async Task TryAttachScanAsync(AccountingApiClient client, Guid docId, string scanFile, OcrSubmitResult res)
        {
            try
            {
                var existing = await client.GetAttachmentsAsync("Document", docId);
                if (existing?.data != null && existing.data.Count > 0) { res.Notes.Add("เอกสารมีไฟล์แนบแล้ว"); return; }
                if (string.IsNullOrEmpty(scanFile) || !File.Exists(scanFile)) { res.Notes.Add("ไม่มีไฟล์สแกนในเครื่อง TakeTime ให้แนบ (ไฟล์ต้นฉบับอยู่กับผลสแกนใน NextAcc)"); return; }
                await client.UploadAttachmentAsync("Document", docId, scanFile);
                res.Notes.Add("แนบไฟล์เอกสารแล้ว");
            }
            catch (Exception ex) { res.Notes.Add("แนบไฟล์ไม่สำเร็จ: " + ShortError(ex)); }
        }

        private static async Task TryWhtCertAsync(AccountingApiClient client, Guid docId, OcrSubmitResult res)
        {
            try
            {
                var w = await client.AutoGenerateWhtCertAsync(new AutoGenerateWhtRequest { DocumentId = docId, AutoIssue = true });
                if (w?.data != null) res.Notes.Add("ออกหนังสือรับรองหัก ณ ที่จ่าย (50 ทวิ) เลขที่ " + w.data.CertificateNumber);
            }
            catch (AccountingApiException ae) when (ae.StatusCode == 400 || ae.StatusCode == 404 || ae.StatusCode == 409)
            {
                res.Notes.Add("50 ทวิ: " + ApiMessage(ae) + " (NextAcc อาจออกให้แล้วตอนอนุมัติ)");
            }
            catch (Exception ex) { res.Notes.Add("ออก 50 ทวิ ไม่สำเร็จ: " + ShortError(ex)); }
        }

        /// <summary>
        /// ใบลด/เพิ่มหนี้ที่ใบต้นทางชำระครบ: NextAcc ลงขาเงินที่บัญชีเงินสด "111" เสมอ (DocumentService.cs:17899, :17920-17924) —
        /// ถ้าผู้ใช้เลือกบัญชีอื่น (ธนาคารที่ผู้ขายโอนคืน / เก็บเป็นยอดกับผู้ขาย = เจ้าหนี้) โพสต์ JE ย้ายขา (ExternalRef idempotent)
        /// </summary>
        private async Task<string> TryReclassNoteMoneyLegAsync(AccountingApiClient client, Guid docId, Guid scanId, OcrReviewState s, OcrSubmitResult res)
        {
            if (s.Settlement != OcrNoteSettlement.Bank && s.Settlement != OcrNoteSettlement.VendorAccount) return null;
            bool cn = s.DocKind == OcrDocKind.VendorCreditNote;
            try
            {
                var mapper = new AccountingDataMapper(_conn);
                string target = null;
                if (s.Settlement == OcrNoteSettlement.Bank)
                {
                    Guid acc;
                    if (Guid.TryParse(s.RefundAccountId ?? "", out acc)) target = mapper.GetAccountCodeFromId(acc);
                }
                else
                {
                    try { target = mapper.GetAccountCode("ACCOUNTS_PAYABLE"); } catch { target = null; }
                }
                if (string.IsNullOrEmpty(target)) { res.Notes.Add("⚠ ย้ายขาเงินไม่ได้: หา code บัญชีปลายทางไม่พบ (Sync ผังบัญชี/ตั้ง mapping ACCOUNTS_PAYABLE) — ปรับใน NextAcc เอง"); return null; }

                var r = await client.GetAsync<ApiResponse<PagedResponse<JournalEntryResponse>>>(
                    "/api/companies/" + new AccountingConfig(_conn).CompanyId + "/accounting/journals?page=1&pageSize=20&sourceDocumentId=" + docId);
                var je = (r?.data?.Items ?? new List<JournalEntryResponse>())
                    .Where(j => j.OriginalEntryId == null && (j.ReversedByEntryId == null || j.ReversedByEntryId == Guid.Empty)
                             && j.Status != 2 && j.Status != 6 && j.Lines != null && j.Lines.Count > 0)   // 2 = JE voided, 6 = ชื่อ "Voided" ผ่าน DocumentStatusConverter
                    .OrderByDescending(j => j.CreatedAt).FirstOrDefault();
                if (je == null) { res.Notes.Add("⚠ ไม่พบ JE ของเอกสาร — ตรวจขาเงินใน NextAcc เอง"); return null; }
                var leg = je.Lines.FirstOrDefault(l => (l.AccountCode ?? "").StartsWith("111", StringComparison.Ordinal)
                                                   && (cn ? l.DebitAmount > 0 : l.CreditAmount > 0));
                if (leg == null)
                {
                    res.Notes.Add(cn ? "ใบต้นทางยังค้างจ่าย — NextAcc ลดเจ้าหนี้ให้แล้ว (ไม่มีขาเงินสดให้ย้าย)"
                                     : "NextAcc ตั้งเจ้าหนี้เพิ่มแล้ว (ไม่มีขาเงินสดให้ย้าย)");
                    return null;
                }
                if (string.Equals(leg.AccountCode, target, StringComparison.OrdinalIgnoreCase)) return null;
                decimal amt = OcrReviewMath.R2(cn ? leg.DebitAmount : leg.CreditAmount);
                string reff = (cn ? "OCRCN-" : "OCRDN-") + scanId.ToString("N").Substring(0, 8).ToUpperInvariant() + "-MNY";
                string desc = (cn ? "ใบลดหนี้ผู้ขาย " : "ใบเพิ่มหนี้ผู้ขาย ") + (res.DocNo ?? "") + " — ย้ายขาเงินจาก " + leg.AccountCode + " ไป " + target
                    + (s.Settlement == OcrNoteSettlement.VendorAccount ? " (เก็บเป็นยอดกับผู้ขาย)" : " (บัญชีที่รับ/จ่ายจริง)");
                var lines = new List<IntegrationJournalLineRequest>
                {
                    new IntegrationJournalLineRequest { AccountCode = cn ? target : leg.AccountCode, DebitAmount = amt, CreditAmount = 0m, Description = desc },
                    new IntegrationJournalLineRequest { AccountCode = cn ? leg.AccountCode : target, DebitAmount = 0m, CreditAmount = amt, Description = desc }
                };
                await client.CreateIntegrationJournalAsync(new CreateIntegrationJournalRequest
                {
                    ExternalId = reff, ExternalRef = reff, EntryDate = OcrReviewMath.ParseDate(s.DocDate) ?? DateTime.Today,
                    JournalType = "general", Description = desc, Lines = lines, AutoBalanceVat = false
                });
                res.Notes.Add("โพสต์ JE " + reff + " ย้ายขาเงิน " + OcrReviewMath.Money(amt) + " จาก " + leg.AccountCode + " ไป " + target);
                return reff;
            }
            catch (Exception ex)
            {
                res.Notes.Add("⚠ ย้ายขาเงินไม่สำเร็จ: " + ShortError(ex) + " — เอกสารอนุมัติแล้ว ปรับขาเงินใน NextAcc เอง");
                return null;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // helpers
        // ──────────────────────────────────────────────────────────────

        /// <summary>คำเตือนจาก 422 ของ approve: {data:{warnings:[...]}} (DocumentController.cs:675-688)</summary>
        public static List<string> ParseApproveWarnings(string body)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(body)) return list;
            try
            {
                var root = Json().DeserializeObject(body) as Dictionary<string, object>;
                object data;
                if (root == null || !TryGetCi(root, "data", out data)) return list;
                var d = data as Dictionary<string, object>;
                object w;
                if (d == null || !TryGetCi(d, "warnings", out w)) return list;
                var arr = w as object[];
                if (arr == null && w is System.Collections.ArrayList) arr = ((System.Collections.ArrayList)w).ToArray();
                if (arr != null) foreach (var x in arr) if (x != null) list.Add(x.ToString());
            }
            catch { }
            return list;
        }

        private static bool TryGetCi(Dictionary<string, object> d, string key, out object value)
        {
            foreach (var kv in d)
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) { value = kv.Value; return true; }
            value = null;
            return false;
        }

        /// <summary>ข้อความภาษาไทยจาก error ของ NextAcc (message ใน body — ถอด \uXXXX ด้วย JSON parser)</summary>
        public static string ApiMessage(Exception ex)
        {
            var ae = ex as AccountingApiException;
            if (ae != null && !string.IsNullOrWhiteSpace(ae.ResponseBody))
            {
                try
                {
                    var root = Json().DeserializeObject(ae.ResponseBody) as Dictionary<string, object>;
                    object m;
                    if (root != null && TryGetCi(root, "message", out m) && m != null && m.ToString().Trim().Length > 0)
                        return Cut(m.ToString(), 600);
                    object errs;
                    if (root != null && TryGetCi(root, "errors", out errs) && errs != null)
                        return Cut(Json().Serialize(errs), 600);
                }
                catch { }
                return Cut(ae.ResponseBody, 600);
            }
            return ShortError(ex);
        }

        public static string ShortError(Exception ex) => Cut(ex?.Message ?? "", 300);

        private static string Cut(string s, int max) => string.IsNullOrEmpty(s) ? s : (s.Length > max ? s.Substring(0, max) : s);

        private static Guid? ParseGuidOrNull(string s)
        {
            Guid g;
            return Guid.TryParse(s ?? "", out g) && g != Guid.Empty ? g : (Guid?)null;
        }

        private static string FirstNonEmpty(params string[] v)
        {
            foreach (var x in v) if (!string.IsNullOrWhiteSpace(x)) return x.Trim();
            return null;
        }
    }
}
