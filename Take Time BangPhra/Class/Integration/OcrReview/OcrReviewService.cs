using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Take_Time_BangPhra.Purchasing;

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
        public decimal SubTotal { get; set; }
        /// <summary>เลขใบผู้ขาย/อ้างอิงของเอกสารนี้ตรงกับ "อ้างอิงใบกำกับภาษีเลขที่" บนใบลดหนี้</summary>
        public bool Exact { get; set; }
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
        /// <summary>การคืนสินค้าที่ผูกกับใบลดหนี้นี้ (PHASE19_39)</summary>
        public string LinkedReturnRef;
        /// <summary>ข้อมูลผู้ขายที่เติมให้ผู้ติดต่อ NextAcc (ช่องที่ว่าง) — แสดงในกล่องสำเร็จ</summary>
        public string ContactFilled;
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

        /// <summary>
        /// ทิ้งงานที่ยังไม่ส่ง (รายการ "งานที่ยังไม่ส่ง" / ปุ่มในหน้าตรวจทาน): ไม่มีเอกสาร NextAcc = ทิ้งเลย · มีร่างบน NextAcc = ลบร่างก่อน
        /// (`DELETE document/{id}` — เฉพาะ Draft) · เอกสารหายแล้ว/ถูกยกเลิกแล้ว = ทิ้งได้ · รออนุมัติ/อนุมัติแล้ว = ไม่ทิ้ง (ต้องยกเลิกใน NextAcc/ออกใบลดหนี้)
        /// คืน null = สำเร็จ, ไม่งั้นข้อความภาษาไทยว่าทำไมทิ้งไม่ได้
        /// </summary>
        public async Task<string> DiscardAsync(AccountingApiClient client, Guid scanId, string user)
        {
            var row = Load(scanId);
            if (row != null && (row.Status == StApproved || row.Status == StDone || row.Status == StEvidence))
                return "งานนี้ส่งเข้า NextAcc แล้ว" + (string.IsNullOrEmpty(row.NexaaccDocNo) ? "" : " (" + row.NexaaccDocNo + ")")
                    + " — ทิ้งไม่ได้ ถ้าต้องการยกเลิกให้ยกเลิกเอกสารใน NextAcc หรือออกใบลดหนี้";
            string note = null;
            if (row != null && row.NexaaccDocId.HasValue)
            {
                if (client == null) return "ยังไม่ได้ตั้งค่าการเชื่อมต่อ NextAcc — ลบร่าง " + (row.NexaaccDocNo ?? "") + " บน NextAcc ไม่ได้";
                Guid docId = row.NexaaccDocId.Value;
                int? status = null;
                string docNo = row.NexaaccDocNo;
                try
                {
                    var d = (await client.GetDocumentAsync(docId))?.data;
                    if (d != null) { status = d.Status; docNo = d.DocumentNumber ?? docNo; }
                }
                catch (AccountingApiException gx) when (gx.StatusCode == 404 || gx.StatusCode == 410) { status = -1; }
                catch (Exception ex) { return "อ่านเอกสาร " + (docNo ?? "") + " บน NextAcc ไม่ได้ (" + ShortError(ex) + ") — ลองใหม่อีกครั้ง"; }

                if (status == -1) note = "เอกสาร " + (docNo ?? "") + " ไม่มีบน NextAcc แล้ว";
                else if (status == 6) note = "เอกสาร " + (docNo ?? "") + " ถูกยกเลิกบน NextAcc แล้ว";
                else if (status == 0)
                {
                    try { await client.DeleteDraftDocumentAsync(docId); note = "ลบร่าง " + (docNo ?? "") + " บน NextAcc แล้ว"; }
                    catch (AccountingApiException dx) when (dx.StatusCode == 404) { note = "เอกสาร " + (docNo ?? "") + " ไม่มีบน NextAcc แล้ว"; }
                    catch (Exception ex) { return "ลบร่าง " + (docNo ?? "") + " บน NextAcc ไม่สำเร็จ: " + ApiMessage(ex); }
                }
                else if (status == 1 || status == 8)
                    return "เอกสาร " + (docNo ?? "") + " บน NextAcc อยู่ระหว่างรออนุมัติ/ถูกตีกลับ — ลบได้เฉพาะร่าง ให้ยกเลิกเอกสารใน NextAcc ก่อน แล้วกดทิ้งอีกครั้ง";
                else
                    return "เอกสาร " + (docNo ?? "") + " บน NextAcc อนุมัติแล้ว — ทิ้งไม่ได้ ให้ยกเลิกใน NextAcc หรือออกใบลดหนี้";
            }
            SetStatus(scanId, StDiscarded, null, null, note, user);
            try { _code.Logs(_conn, "OcrReview", "ทิ้งงาน OCR " + scanId + (row != null ? " (" + row.Status + ")" : "") + (note != null ? " · " + note : ""), user ?? "SYSTEM"); } catch { }
            return null;
        }

        /// <summary>บันทึก draft (upsert ตาม Scan_Id) — ไม่ทับ Ocr_Raw_Json ที่มีแล้ว · ไม่ลดสถานะที่เดินหน้าไปแล้ว</summary>
        public bool SaveDraft(OcrReviewState s, string rawJson, string fileName, string user)
        {
            if (s == null || !HasLogTable()) return false;
            Guid scanId;
            if (!Guid.TryParse(s.ScanId ?? "", out scanId)) return false;
            var t = OcrReviewMath.CalcTotals(s);
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

            // §86/10 บนใบลด/เพิ่มหนี้ (มูลค่าเดิม/ที่ถูกต้อง/ผลต่าง) — อ่านก่อนเพราะใช้กระทบยอดด้วย: OCR ของ NextAcc เคยอ่าน "มูลค่าตามใบกำกับภาษีเดิม 19,604"
            // เป็นยอดก่อน VAT ของใบลดหนี้ Makro (ยอดจริง 3,466.58 + 72.42 = 3,539.00)
            string raw = r.RawTextContent;
            s.OrigAmount = OcrCnPaperReader.ReadOriginalAmount(raw);
            s.CorrectAmount = OcrCnPaperReader.ReadCorrectAmount(raw);
            s.DiffAmount = OcrCnPaperReader.ReadDiffAmount(raw);

            // กระทบยอดกระดาษ 3 ช่องให้บวกกันลงตัวก่อนใช้ (OcrPaperTotals — ผู้สมัครจาก OCR/ป้ายในข้อความดิบ/แถว "รวม"/Σรายการ/§86/10 → ชุดที่หลักฐานมากสุด)
            decimal? linesSum = null, linesVat = null;
            if (r.ExtractedItems != null && r.ExtractedItems.Any(it => it != null))
            {
                linesSum = 0m;
                foreach (var it in r.ExtractedItems)
                {
                    if (it == null) continue;
                    decimal q = it.Quantity.HasValue && it.Quantity.Value > 0 ? it.Quantity.Value : 1m;
                    linesSum += it.Amount ?? OcrReviewMath.R2(q * (it.UnitPrice ?? 0m) - (it.LineDiscountAmount ?? 0m));
                    if (it.VatAmount.HasValue) linesVat = (linesVat ?? 0m) + it.VatAmount.Value;
                }
                linesSum = OcrReviewMath.R2(linesSum.Value);
                if (linesVat.HasValue) linesVat = OcrReviewMath.R2(linesVat.Value);
            }
            var paper = OcrPaperTotals.Reconcile(sub, vat, total, linesSum, linesVat, s.OrigAmount, s.CorrectAmount, s.DiffAmount, raw, OcrDocKind.IsNote(s.DocKind));
            if (paper.Changed) { sub = paper.SubTotal; vat = paper.Vat; total = paper.Total; }
            s.PaperNotes.AddRange(paper.Notes);
            if (paper.Confidence == "LOW") { s.LowConfidence.Add("SubTotal"); s.LowConfidence.Add("VatAmount"); s.LowConfidence.Add("TotalAmount"); }

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
                // ราคารวม VAT บนกระดาษ (เช่น Makro): เก็บราคาตามกระดาษ + PricesIncludeVat=true (ไม่ถอด 7% รายบรรทัด — เดิมถอดรายบรรทัด/ใส่ยอดรวม VAT
                // เป็นราคาก่อน VAT ⇒ ยอดก่อน VAT 3,447 / VAT 241.29 ผิดกระดาษ). ถอดรายบรรทัดเฉพาะเมื่อ OCR ติดธงแค่บางบรรทัด
                bool allIncl = r.ExtractedItems.All(it => it != null && it.PriceIncludesVat);
                bool anyFlag = r.ExtractedItems.Any(it => it != null && it.PriceIncludesVat);
                var rawPrices = new List<decimal[]>();   // [ราคา/หน่วย, ส่วนลด] ตามกระดาษ ก่อนถอด 7% — ใช้ตัดสินใหม่เมื่อ OCR ติดธงแค่บางบรรทัด
                var vatHints = new List<OcrLineVatHint>();   // VAT รายบรรทัดที่ OCR อ่านได้จริง (อัตรา/ยอด) — ไม่มี = ให้ InferLineVatTypes ตัดสินจากกระดาษ
                foreach (var it in r.ExtractedItems)
                {
                    if (it == null) continue;
                    vatHints.Add(new OcrLineVatHint { ExplicitRate = it.VatRate.HasValue, VatAmount = it.VatAmount });
                    var l = new OcrReviewLine
                    {
                        Description = (it.Description ?? "").Trim(),
                        AccountCode = FirstNonEmpty(it.SuggestedAccountCode, defAcc),
                        OcrAccountCode = FirstNonEmpty(it.SuggestedAccountCode, defAcc),
                        WhtRate = whtRate,
                        VatClaimable = claim
                    };
                    l.ProductCode = OcrReviewMath.ExtractItemCode(l.Description);
                    decimal qty = it.Quantity.HasValue && it.Quantity.Value > 0 ? it.Quantity.Value : 1m;
                    decimal disc = it.LineDiscountAmount ?? 0m;
                    decimal amount = it.Amount ?? OcrReviewMath.R2(qty * (it.UnitPrice ?? 0m) - disc);
                    decimal unit = it.UnitPrice ?? (qty != 0 ? amount / qty : amount);
                    rawPrices.Add(new[] { unit, disc });
                    if (it.PriceIncludesVat && !allIncl)
                    {
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
                if (allIncl && hasVat) { s.PricesIncludeVat = true; s.PriceBasisSource = "OCR"; }
                // OCR ติดธง "รวม VAT" แค่บางบรรทัด: ลองราคาตามกระดาษ (ไม่ถอด) ทั้งใบก่อน — ยอดบนกระดาษตัดสินได้ = ใช้ราคาตามกระดาษ + ฐานนั้นทั้งใบ
                // (เดิมถอด 7% เฉพาะบรรทัดที่ติดธงแล้วข้ามการตัดสิน ⇒ ใบรวม VAT ทั้งใบที่ OCR พลาดธงบางบรรทัดออกมาเป็น "ไม่รวม VAT" ยอดผิด)
                if (anyFlag && !allIncl && rawPrices.Count == s.Lines.Count)
                {
                    var rawLines = s.Lines.Select((x, i) => new OcrReviewLine
                    {
                        Qty = x.Qty, UnitPrice = rawPrices[i][0], Discount = rawPrices[i][1], VatType = x.VatType, WhtRate = x.WhtRate, VatClaimable = x.VatClaimable
                    }).ToList();
                    var detRaw = OcrReviewMath.DetectPricesIncludeVat(rawLines, s.PrintedSubTotal, s.PrintedVat, s.PrintedTotal);
                    if (detRaw.HasValue)
                    {
                        for (int i = 0; i < s.Lines.Count; i++) { s.Lines[i].UnitPrice = rawPrices[i][0]; s.Lines[i].Discount = rawPrices[i][1]; }
                        s.PricesIncludeVat = detRaw.Value;
                        s.PriceBasisSource = "AUTO";
                    }
                }
                // ตัดสินรวม/ไม่รวม VAT จากยอดบนกระดาษ (Σยอดบรรทัด = ยอดรวม ⇒ รวม VAT · = ยอดก่อน VAT ⇒ ไม่รวม) — ข้ามเมื่อ OCR ติดธงบางบรรทัด (ถอดไปแล้ว)
                if (!anyFlag || allIncl)
                {
                    var det = OcrReviewMath.DetectPricesIncludeVat(s.Lines, s.PrintedSubTotal, s.PrintedVat, s.PrintedTotal);
                    if (det.HasValue && det.Value != s.PricesIncludeVat) { s.PricesIncludeVat = det.Value; s.PriceBasisSource = "AUTO"; }
                    else if (det.HasValue) s.PriceBasisSource = s.PriceBasisSource ?? "AUTO";
                }
                // VAT รายบรรทัดตามกระดาษ — เดิมทุกบรรทัดได้ 7% เมื่อ OCR ไม่ส่งอัตรา ⇒ ใบ Makro บรรทัด "รหัส ภ.พ. 1" (VAT 0.00) ถูกบวก 7% (242.66 แทน 72.42).
                // ตัดสินจาก (ก) ยอด VAT รายบรรทัดที่ OCR อ่าน (ข) แถว "ฐาน VAT รวม" ในข้อความดิบที่ฐานตรงกับยอดบรรทัด (ค) ชุดบรรทัดที่ 7% บวกกันได้เท่า VAT บนกระดาษ (ชุดเดียว)
                if (hasVat && s.Lines.Count > 1 && vatHints.Count == s.Lines.Count)
                    s.PaperNotes.AddRange(OcrPaperTotals.InferLineVatTypes(s.Lines, vatHints, raw, s.PrintedVat, s.PricesIncludeVat));
                // ส่วนลดท้ายบิลที่บรรทัดยังไม่รวม → ใส่ที่บรรทัดยอดสูงสุด (ผู้ใช้ปรับได้) — เฉพาะราคาไม่รวม VAT
                decimal hdrDisc = r.ExtractedDiscountAmount ?? 0m;
                if (hdrDisc > 0m && s.PrintedSubTotal.HasValue && !s.PricesIncludeVat)
                {
                    var t0 = OcrReviewMath.CalcTotals(s);
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
            if (hasVat && OcrReviewMath.SuggestFixes(s).Any(f => f.Code == "FIT_VAT"))
                OcrReviewMath.ApplyFix(s, "FIT_VAT");

            // §86/9-10 บนใบลด/เพิ่มหนี้ (อ้างใบเดิม/เหตุผล/วิธีชำระ) — อ่านจากข้อความดิบ (มูลค่าเดิม/ที่ถูกต้อง/ผลต่าง อ่านไว้ด้านบนแล้ว); ใช้แสดงเมื่อชนิดเป็นใบลด/เพิ่มหนี้
            string origNo, origDate;
            if (OcrCnPaperReader.ReadOriginalInvoice(raw, out origNo, out origDate)
                && !string.Equals(OcrReviewMath.NormalizeDocNo(origNo), OcrReviewMath.NormalizeDocNo(s.SupplierDocNo), StringComparison.Ordinal))
            {
                s.OrigTaxInvoiceNo = origNo;
                s.OrigTaxInvoiceDate = origDate;
            }
            s.PaperPaymentType = OcrCnPaperReader.ReadPaymentType(raw);
            s.NoteReasonCode = OcrCnPaperReader.ReadReasonCode(raw, s.DocKind == OcrDocKind.VendorDebitNote);
            var rs = OcrNoteReasons.Find(s.DocKind, s.NoteReasonCode);
            if (rs != null) s.NoteReason = rs.NextAcc;

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
                s.SearchText = FirstNonEmpty(s.OrigTaxInvoiceNo, s.SupplierName);
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

        /// <summary>
        /// ค้นใบกำกับเดิมใน "ระบบเรา" จากเลขใบของผู้ขาย (อ้างอิงบนใบลด/เพิ่มหนี้) → id เอกสาร NextAcc + ที่มา:
        /// (1) ประวัติสแกน OCR (`Ocr_Document_Log` PV/EXP ที่อนุมัติแล้ว, `Supplier_Doc_No_Norm`) · (2) ใบสำคัญจ่ายที่บันทึกในระบบ
        /// (`Account_Payment.Supplier_Invoice_No_Norm`, PHASE19_40) · (3) รับของ (`Product_In.Supplier_Doc_No` → `Linked_Voucher_Id`, PHASE19_31)
        /// — (2)(3) หาเอกสาร NextAcc จาก `Account_Payment.Nexaacc_Voucher_Doc_Marker` (GUID) หรือคิว CREATE_VOUCHER_JOURNAL ที่สำเร็จ.
        /// ไม่มีตาราง/คอลัมน์ = ข้ามขั้นนั้น
        /// </summary>
        public List<KeyValuePair<Guid, string>> FindLocalSourceDocIds(string origNo)
        {
            var list = new List<KeyValuePair<Guid, string>>();
            var seen = new HashSet<Guid>();
            string norm = OcrReviewMath.NormalizeDocNo(origNo);
            if (norm.Length < 4) return list;
            Action<Guid, string> add = (g, why) => { if (g != Guid.Empty && seen.Add(g)) list.Add(new KeyValuePair<Guid, string>(g, why)); };
            Func<string, bool> colExists = sql =>
            {
                try { var t = _code.DatabaseQuerySafe(_conn, sql, null); return t != null && t.Rows.Count > 0 && Convert.ToInt32(t.Rows[0][0]) == 1; }
                catch { return false; }
            };

            // (1) ประวัติสแกน OCR
            if (HasLogTable())
            {
                try
                {
                    var dt = _code.DatabaseQuerySafe(_conn,
                        @"SELECT TOP 10 Nexaacc_Doc_Id, Nexaacc_Doc_No FROM Ocr_Document_Log
                           WHERE Supplier_Doc_No_Norm = @n AND Doc_Kind IN (N'PV', N'EXP') AND Nexaacc_Doc_Id IS NOT NULL
                             AND Status IN (N'APPROVED', N'DONE')
                           ORDER BY ISNULL(Updated_At, Created_At) DESC",
                        new Dictionary<string, object> { { "@n", norm } });
                    if (dt != null)
                        foreach (DataRow r in dt.Rows)
                            add((Guid)r["Nexaacc_Doc_Id"], "พบในระบบ: สแกน OCR ที่ส่งแล้ว " + (r["Nexaacc_Doc_No"] == DBNull.Value ? "" : r["Nexaacc_Doc_No"].ToString()));
                }
                catch { }
            }

            // (2)(3) ใบสำคัญจ่าย / รับของ → เลขใบสำคัญจ่ายในระบบ
            var payIds = new List<KeyValuePair<string, string>>();
            if (colExists("SELECT CASE WHEN COL_LENGTH('dbo.Account_Payment','Supplier_Invoice_No_Norm') IS NULL THEN 0 ELSE 1 END"))
            {
                try
                {
                    var dt = _code.DatabaseQuerySafe(_conn,
                        "SELECT TOP 10 CAST(ID AS NVARCHAR(100)) AS PID FROM Account_Payment WHERE Supplier_Invoice_No_Norm = @n ORDER BY Created_Date DESC",
                        new Dictionary<string, object> { { "@n", norm } });
                    if (dt != null) foreach (DataRow r in dt.Rows) payIds.Add(new KeyValuePair<string, string>(r["PID"].ToString(), "ใบสำคัญจ่าย"));
                }
                catch { }
            }
            if (colExists("SELECT CASE WHEN COL_LENGTH('dbo.Product_In','Supplier_Doc_No') IS NULL OR COL_LENGTH('dbo.Product_In','Linked_Voucher_Id') IS NULL THEN 0 ELSE 1 END"))
            {
                try
                {
                    // เทียบแบบตัดสัญลักษณ์ในโค้ด — กรองหยาบด้วยตัวท้าย 4 ตัวก่อน
                    string tail = norm.Substring(norm.Length - 4);
                    var dt = _code.DatabaseQuerySafe(_conn,
                        @"SELECT TOP 50 Supplier_Doc_No, CAST(Linked_Voucher_Id AS NVARCHAR(100)) AS PID FROM Product_In
                           WHERE Linked_Voucher_Id IS NOT NULL AND Supplier_Doc_No LIKE @t",
                        new Dictionary<string, object> { { "@t", "%" + tail + "%" } });
                    if (dt != null)
                        foreach (DataRow r in dt.Rows)
                            if (OcrReviewMath.NormalizeDocNo(r["Supplier_Doc_No"]?.ToString()) == norm && !string.IsNullOrWhiteSpace(r["PID"]?.ToString()))
                                payIds.Add(new KeyValuePair<string, string>(r["PID"].ToString().Trim(), "รับของเข้าสต๊อก"));
                }
                catch { }
            }
            bool hasMarker = colExists("SELECT CASE WHEN COL_LENGTH('dbo.Account_Payment','Nexaacc_Voucher_Doc_Marker') IS NULL THEN 0 ELSE 1 END");
            foreach (var p in payIds.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).Take(10))
            {
                Guid doc = Guid.Empty;
                try
                {
                    if (hasMarker)
                    {
                        var mt = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 Nexaacc_Voucher_Doc_Marker FROM Account_Payment WHERE CAST(ID AS NVARCHAR(100)) = @id",
                            new Dictionary<string, object> { { "@id", p.Key } });
                        string mk = mt != null && mt.Rows.Count > 0 && mt.Rows[0][0] != DBNull.Value ? mt.Rows[0][0].ToString() : null;
                        Guid g;
                        if (Guid.TryParse(mk ?? "", out g)) doc = g;
                    }
                    if (doc == Guid.Empty)
                    {
                        string idLike = p.Key.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
                        var qt = _code.DatabaseQuerySafe(_conn,
                            @"SELECT TOP 1 Nexaacc_Response_Id FROM Accounting_Sync_Queue
                               WHERE Entity_Type = 'VOUCHER' AND Action_Type = 'CREATE_VOUCHER_JOURNAL' AND Status = 'COMPLETED'
                                 AND Payload LIKE @p ORDER BY ID DESC",
                            new Dictionary<string, object> { { "@p", "%\"documentNumber\":\"" + idLike + "\"%" } });
                        Guid g;
                        if (qt != null && qt.Rows.Count > 0 && Guid.TryParse(qt.Rows[0][0]?.ToString() ?? "", out g)) doc = g;
                    }
                }
                catch { }
                add(doc, "พบในระบบ: " + p.Value + " " + p.Key);
            }
            return list;
        }

        /// <summary>ใบกำกับเดิมที่ควรเลือกให้อัตโนมัติจากผลค้น (<see cref="OcrReviewMath.PickSource"/>) — null = ให้ผู้ใช้เลือกเอง</summary>
        public static OcrSourceCandidate PickCandidate(List<OcrSourceCandidate> results, OcrReviewState s)
        {
            if (results == null || results.Count == 0) return null;
            var pick = OcrReviewMath.PickSource(results.Select(r => new OcrSourcePick
            {
                Id = r.Id, Exact = r.Exact, ContactId = r.ContactId, ContactName = r.ContactName, Date = r.Date, Remaining = r.Remaining
            }).ToList(), s);
            return pick == null ? null : results.FirstOrDefault(r => r.Id == pick.Id);
        }

        /// <summary>ค้นใบต้นทาง (PI/Expense/PV ที่อนุมัติแล้ว) ของผู้ขาย — คำค้น = เลขที่เอกสาร/ชื่อผู้ขาย · ผู้ติดต่อที่ OCR จับคู่ได้ช่วยกรอง
        /// + ใบที่ NextAcc จัดอันดับให้สแกน (predecessor-candidates) เมื่อมี + ใบที่พบใน "ระบบเรา" (<see cref="FindLocalSourceDocIds"/>)</summary>
        public static async Task<List<OcrSourceCandidate>> SearchSourceDocsAsync(AccountingApiClient client, OcrReviewState s, List<string> notes,
            IList<KeyValuePair<Guid, string>> localHits = null)
        {
            var map = new Dictionary<Guid, OcrSourceCandidate>();
            Guid contactId;
            bool hasContact = Guid.TryParse(s.SourceContactId ?? "", out contactId) && contactId != Guid.Empty;
            string text = (s.SearchText ?? "").Trim();

            var batches = new List<List<DocumentResponse>>();
            string orig = (s.OrigTaxInvoiceNo ?? "").Trim();
            string origNorm = OcrReviewMath.NormalizeDocNo(orig);
            try
            {
                if (text.Length > 0)
                    batches.Add((await client.ListDocumentsAsync(PurchaseSourceTypes, null, text, null, 30))?.data?.Items);
                // เลขใบกำกับเดิมบนใบลดหนี้ (search ของ NextAcc ครอบ Reference — DocumentService.cs:2427-2430; ใบจากหน้านี้ตั้ง Reference = เลขผู้ขาย)
                if (orig.Length > 0 && !string.Equals(orig, text, StringComparison.OrdinalIgnoreCase))
                    batches.Add((await client.ListDocumentsAsync(PurchaseSourceTypes, null, orig, null, 30))?.data?.Items);
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
                    var cand = new OcrSourceCandidate
                    {
                        Id = d.Id.ToString(), No = d.DocumentNumber, Type = d.DocumentType.ToString(CultureInfo.InvariantCulture),
                        TypeLabel = TypeLabel(d.DocumentType), Date = d.DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Total = d.TotalAmount, BalanceDue = d.BalanceDue, SubTotal = d.SubTotal,
                        ContactId = d.Contact?.Id.ToString(), ContactName = d.Contact?.Name, SupplierDocNo = d.SupplierInvoiceNumber
                    };
                    if (origNorm.Length > 0 && (OcrReviewMath.NormalizeDocNo(d.SupplierInvoiceNumber) == origNorm || OcrReviewMath.NormalizeDocNo(d.Reference) == origNorm))
                    {
                        cand.Exact = true;
                        cand.Reason = "ตรงเลขใบกำกับเดิม " + orig;
                    }
                    map[d.Id] = cand;
                }
            }

            // ใบที่พบใน "ระบบเรา" จากเลขใบกำกับเดิม (ใบสำคัญจ่าย/รับของ/สแกน OCR) — นับเป็นตรงเลขใบเดิม
            foreach (var hit in localHits ?? new List<KeyValuePair<Guid, string>>())
            {
                OcrSourceCandidate known;
                if (map.TryGetValue(hit.Key, out known)) { if (!known.Exact) { known.Exact = true; known.Reason = hit.Value; } continue; }
                try
                {
                    var d = (await client.GetDocumentAsync(hit.Key))?.data;
                    if (d == null || !IsLiveStatus(d.Status) || !PurchaseSourceTypes.Contains(d.DocumentType)) continue;
                    map[d.Id] = new OcrSourceCandidate
                    {
                        Id = d.Id.ToString(), No = d.DocumentNumber, Type = d.DocumentType.ToString(CultureInfo.InvariantCulture),
                        TypeLabel = TypeLabel(d.DocumentType), Date = d.DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Total = d.TotalAmount, BalanceDue = d.BalanceDue, SubTotal = d.SubTotal,
                        ContactId = d.Contact?.Id.ToString(), ContactName = d.Contact?.Name, SupplierDocNo = d.SupplierInvoiceNumber,
                        Exact = true, Reason = hit.Value + (orig.Length > 0 ? " (เลขใบกำกับเดิม " + orig + ")" : "")
                    };
                }
                catch { /* เอกสารหาย/ถูกลบบน NextAcc — ข้าม */ }
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
                        if (map.TryGetValue(c.DocumentId, out ex)) { if (!ex.Exact) ex.Reason = c.Reason; continue; }
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

            var list = map.Values.OrderByDescending(x => x.Exact).ThenByDescending(x => x.Reason != null).ThenByDescending(x => x.Date).Take(12).ToList();
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

        /// <summary>
        /// โหลดรายละเอียดใบต้นทางที่เลือก (GET document — DocumentController GET /document/{id}; บรรทัด DocumentLineResponse DocumentDtos.cs:1169-1210)
        /// → ยอด/ค้างจ่าย/ลดหนี้แล้ว + บรรทัดพร้อมบัญชี + บัญชีเงินที่ใบเดิมจ่าย แล้วตั้งบัญชีของบรรทัดใบลดหนี้ตามใบเดิม และขาเงินเริ่มต้น.
        /// บัญชีที่จ่าย: PV = PaymentAccountId ของใบ · อื่น ๆ = ขาเครดิตเงินของ JE ที่ SourceDocumentId = ใบนี้ (JE การชำระผูกใบ — DocumentService.cs:20046)
        /// เลือกเฉพาะบัญชีที่เป็นแหล่งเงินใน Account_Paid_How (<paramref name="moneyAccounts"/>) หรือ 111x/112x
        /// </summary>
        public static async Task<bool> LoadSourceDetailAsync(AccountingApiClient client, OcrReviewState s, IDictionary<Guid, string> moneyAccounts,
            bool cnPrimary, string companyId, List<string> notes)
        {
            Guid sid;
            if (s == null || !Guid.TryParse(s.SourceDocId ?? "", out sid) || sid == Guid.Empty) return false;
            DocumentResponse d;
            try { d = (await client.GetDocumentAsync(sid))?.data; }
            catch (Exception ex) { notes.Add("อ่านเอกสารต้นทางจาก NextAcc ไม่สำเร็จ: " + ApiMessage(ex)); return false; }
            if (d == null) { notes.Add("ไม่พบเอกสารต้นทางใน NextAcc"); return false; }
            s.SourceDocNo = d.DocumentNumber;
            s.SourceDocType = d.DocumentType.ToString(CultureInfo.InvariantCulture);
            s.SourceDocDate = d.DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            s.SourceTotal = d.TotalAmount;
            s.SourceSubTotal = d.SubTotal;
            s.SourceBalanceDue = d.BalanceDue;
            s.SourceSupplierDocNo = d.SupplierInvoiceNumber;
            s.SourceContactId = d.Contact?.Id.ToString();
            s.SourceContactName = d.Contact?.Name;
            if (string.IsNullOrWhiteSpace(s.SupplierName) && !string.IsNullOrWhiteSpace(d.Contact?.Name)) s.SupplierName = d.Contact.Name;
            try { s.SourceCredited = await SumCreditedAsync(client, sid); } catch { }
            s.SourceLines = (d.Lines ?? new List<NexaaccDocLine>()).Where(x => x != null).Select(x => new OcrSourceLine
            {
                Description = x.Description, ProductCode = x.ProductCode, Qty = x.Quantity, UnitPrice = x.UnitPrice, Amount = x.Amount,
                VatRate = x.VatRate, AccountCode = x.AccountCode, AccountName = x.AccountName
            }).ToList();
            if (s.SourceLines.Count == 0) notes.Add("ใบต้นทาง " + d.DocumentNumber + " ไม่มีรายการให้เทียบ — เลือกบัญชีของบรรทัดเอง");

            // บัญชีเงินที่ใบเดิมจ่ายออก
            s.SourcePaidAccountId = null; s.SourcePaidAccountLabel = null;
            if (d.PaymentAccountId.HasValue && d.PaymentAccountId.Value != Guid.Empty && (d.PaidAmount > 0m || d.BalanceDue <= 0.01m))
            {
                s.SourcePaidAccountId = d.PaymentAccountId.Value.ToString();
                string lb; moneyAccounts.TryGetValue(d.PaymentAccountId.Value, out lb);
                s.SourcePaidAccountLabel = lb ?? d.PaymentAccountName;
            }
            else if (d.PaidAmount > 0m && !string.IsNullOrEmpty(companyId))
            {
                try
                {
                    var r = await client.GetAsync<ApiResponse<PagedResponse<JournalEntryResponse>>>(
                        "/api/companies/" + companyId + "/accounting/journals?page=1&pageSize=20&sourceDocumentId=" + sid);
                    var leg = (r?.data?.Items ?? new List<JournalEntryResponse>())
                        .Where(j => j.Status != 2 && j.Status != 6 && (j.ReversedByEntryId == null || j.ReversedByEntryId == Guid.Empty) && j.Lines != null)
                        .SelectMany(j => j.Lines.Select(l => new { j.CreatedAt, l }))
                        .Where(x => x.l.CreditAmount > 0m
                                 && (moneyAccounts.ContainsKey(x.l.AccountId)
                                     || (x.l.AccountCode ?? "").StartsWith("111", StringComparison.Ordinal)
                                     || (x.l.AccountCode ?? "").StartsWith("112", StringComparison.Ordinal)))
                        .OrderByDescending(x => moneyAccounts.ContainsKey(x.l.AccountId)).ThenByDescending(x => x.l.CreditAmount).ThenByDescending(x => x.CreatedAt)
                        .Select(x => x.l).FirstOrDefault();
                    if (leg != null)
                    {
                        s.SourcePaidAccountId = leg.AccountId.ToString();
                        string lb; moneyAccounts.TryGetValue(leg.AccountId, out lb);
                        s.SourcePaidAccountLabel = lb ?? ((leg.AccountCode ?? "") + " " + (leg.AccountName ?? "")).Trim();
                    }
                }
                catch (Exception ex) { notes.Add("อ่านการจ่ายของใบต้นทางไม่สำเร็จ (" + ShortError(ex) + ") — เลือกบัญชีรับเงินคืนเอง"); }
            }

            int n = OcrReviewMath.AssignAccountsFromSource(s, false);
            if (n > 0) notes.Add("ตั้งบัญชี " + n + " รายการตามใบต้นทาง " + d.DocumentNumber + " (เปลี่ยนได้ที่ช่องบัญชี)");
            OcrReviewMath.DefaultSettlement(s, cnPrimary);
            return true;
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

        /// <summary>
        /// บรรทัดที่ยืนยัน → DocumentLineRequest — ราคา/ส่วนลดในฐานเดียวกับ <see cref="OcrReviewState.PricesIncludeVat"/> (ส่ง PricesIncludeVat ค่าเดียวกัน)
        /// และ VAT ทุกบรรทัดที่มีภาษีส่งเป็น VatAmountOverride (หลังกระทบเศษทั้งใบ) ⇒ NextAcc: ฐาน = หลังส่วนลด − VAT เมื่อรวม VAT,
        /// ไม่กระทบเศษซ้ำ (DocumentService.cs:653-659, :812-814) ⇒ ยอดใน NextAcc = ยอดบนกระดาษ (Makro 3,221.50 / 225.50 / 3,447.00)
        /// </summary>
        public static List<DocumentLineRequest> ToLines(OcrReviewState s)
        {
            var list = new List<DocumentLineRequest>();
            var lines = s.Lines ?? new List<OcrReviewLine>();
            var calcs = OcrReviewMath.CalcLines(lines, s.PricesIncludeVat);
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (l == null) continue;
                var c = calcs[i];
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

        /// <summary>
        /// หมายเหตุของเอกสาร — ใบลด/เพิ่มหนี้: เหตุผลของเรา (NextAcc พิมพ์เฉพาะข้อความมาตรฐานของ enum และไม่มีช่องเหตุผลอิสระ —
        /// PdfGenerationService.cs:2226-2231) + อ้างใบกำกับเดิม/มูลค่าตามกระดาษ (กรณีใบเดิมนอกระบบ NextAcc พิมพ์ได้แค่เลขอ้างอิง :1907-1916)
        /// </summary>
        public static string Notes(OcrReviewState s)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(s.Notes)) parts.Add(s.Notes.Trim());
            if (OcrDocKind.IsNote(s.DocKind))
            {
                var rs = OcrNoteReasons.Find(s.DocKind, s.NoteReasonCode);
                string txt = (s.NoteReasonText ?? "").Trim();
                if (rs != null) parts.Add("เหตุผล: " + rs.Label + (txt.Length > 0 && !string.Equals(txt, rs.Label, StringComparison.Ordinal) ? " — " + txt : ""));
                else if (txt.Length > 0) parts.Add("เหตุผล: " + txt);
                if (!string.IsNullOrWhiteSpace(s.OrigTaxInvoiceNo))
                {
                    var od = OcrReviewMath.ParseDate(s.OrigTaxInvoiceDate);
                    parts.Add("อ้างอิงใบกำกับภาษีเดิมเลขที่ " + s.OrigTaxInvoiceNo.Trim()
                        + (od.HasValue ? " ลงวันที่ " + od.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : ""));
                }
                if (s.OrigAmount.HasValue || s.CorrectAmount.HasValue || s.DiffAmount.HasValue)
                    parts.Add("มูลค่าตามใบกำกับเดิม " + (s.OrigAmount.HasValue ? OcrReviewMath.Money(s.OrigAmount.Value) : "-")
                        + " · มูลค่าที่ถูกต้อง " + (s.CorrectAmount.HasValue ? OcrReviewMath.Money(s.CorrectAmount.Value) : "-")
                        + " · ผลต่าง " + (s.DiffAmount.HasValue ? OcrReviewMath.Money(s.DiffAmount.Value) : "-") + " (ตามเอกสารผู้ขาย)");
            }
            parts.Add("สร้างจากหน้าตรวจทาน OCR (TakeTime)");
            return Cut(string.Join(" · ", parts), 2000);
        }

        /// <summary>
        /// อ้างอิงของเอกสาร: ใบลด/เพิ่มหนี้ = เลขที่ใบกำกับเดิม (NextAcc ใช้ Reference พิมพ์กล่อง §86/9-10 เมื่อไม่ได้ผูกใบต้นทาง —
        /// PdfGenerationService.cs:1907-1916; ผูกแล้วพิมพ์จากใบต้นทางเอง :1864-1901) · อื่น ๆ = เลขที่เอกสารผู้ขาย
        /// </summary>
        public static string ReferenceFor(OcrReviewState s)
        {
            if (OcrDocKind.IsNote(s.DocKind) && !string.IsNullOrWhiteSpace(s.OrigTaxInvoiceNo)) return s.OrigTaxInvoiceNo.Trim();
            return string.IsNullOrWhiteSpace(s.SupplierDocNo) ? null : s.SupplierDocNo.Trim();
        }

        /// <summary>NextAcc CreditNoteReason/DebitNoteReason จากรหัสเหตุผลของเรา (ร่างเก่าใช้ NoteReason เดิม)</summary>
        public static int ResolveNoteReason(OcrReviewState s)
        {
            var rs = OcrNoteReasons.Find(s.DocKind, s.NoteReasonCode);
            return rs != null ? rs.NextAcc : s.NoteReason;
        }

        private static UpdateDocumentRequest BuildUpdate(OcrReviewState s, OcrSubmitContext ctx)
        {
            bool note = OcrDocKind.IsNote(s.DocKind);
            var calcs = OcrReviewMath.CalcLines(s);
            var lines = s.Lines ?? new List<OcrReviewLine>();
            var claimsVat = Enumerable.Range(0, lines.Count).Any(i => lines[i] != null && lines[i].VatClaimable && calcs[i].Vat > 0);
            var upd = new UpdateDocumentRequest
            {
                DocumentDate = OcrReviewMath.ParseDate(s.DocDate),
                Lines = ToLines(s),
                PricesIncludeVat = s.PricesIncludeVat,
                SupplierInvoiceNumber = string.IsNullOrWhiteSpace(s.SupplierDocNo) ? null : s.SupplierDocNo.Trim(),
                Reference = ReferenceFor(s),
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
                // ใบต้นทางจ่ายครบ + ผู้ใช้เลือก "รับ/จ่ายผ่านบัญชี…" (เช่น 21230 เจ้าหนี้กรรมการ ที่ใบเดิมจ่ายออก) ⇒ ตั้งแหล่งเงินของใบลด/เพิ่มหนี้เป็นบัญชีนั้น —
                // NextAcc รุ่นใหม่ลงขาเงินของ CN/DN ผ่าน UseMoneyAccount() = PaymentAccountId ของเอกสาร (DocumentService.cs:18225-18229 "V-CN-03",
                // ตัวตัดสิน DocumentMoneyChannel: ผังที่ลงรายการได้ทุกชนิด) แทน 111 ตายตัว. รุ่นเก่าไม่สนช่องนี้ ⇒ JE ย้ายขา (TryReclassNoteMoneyLegAsync) ทำแทน
                Guid refundAcc;
                if (s.Settlement == OcrNoteSettlement.Bank && Guid.TryParse(s.RefundAccountId ?? "", out refundAcc) && refundAcc != Guid.Empty)
                {
                    upd.PaymentAccountId = refundAcc;
                    // ใบเพิ่มหนี้: NextAcc รุ่นใหม่ลงขาเงินเฉพาะใบที่ระบุ "จ่ายทันที" (PaymentType=Cash — AdjustmentNoteAccount.CounterLegIsMoney:197-200)
                    // ไม่งั้นตั้งเจ้าหนี้ ⇒ เลือกจ่ายทันทีจากบัญชี = Cash. ใบลดหนี้ตัดสินจากยอดค้างของใบต้นทางเอง (ไม่ต้องส่ง)
                    if (s.DocKind == OcrDocKind.VendorDebitNote) upd.PaymentType = 1;
                }
                int reason = ResolveNoteReason(s);
                if (s.DocKind == OcrDocKind.VendorCreditNote) upd.CreditNoteReason = reason;
                else upd.DebitNoteReason = reason;
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
            // PHASE19_39: นโยบาย "ใบลดหนี้ผู้ขายเป็นตัวจริง" — ร่างเก่าที่เลือก "หลักฐาน PRT" เปลี่ยนเป็นใบลดหนี้จริงที่ผูกการคืน
            var vcs = new VendorCnService(_conn);
            bool cnPrimary = note && vcs.CnPrimary();
            if (cnPrimary && s.SourceMode == OcrNoteSource.PurchaseReturn) s.SourceMode = OcrNoteSource.NextAcc;
            if (cnPrimary && s.DocKind != OcrDocKind.VendorCreditNote) s.ReturnRef = null;
            if (note) s.NoteReason = ResolveNoteReason(s);   // รหัสเหตุผลของเรา → enum NextAcc (AllEnums.cs:1636-1662)

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
                            s.SourceSubTotal = source.SubTotal;
                            s.SourceSupplierDocNo = source.SupplierInvoiceNumber;
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

            // ── PHASE19_39: ผูกการคืนสินค้า (ใบลดหนี้ = ตัวจริง) + ขาเงินที่ทำจริง ──
            ReturnCnLink link = null;
            string evidenceWhy = null;
            if (cnPrimary)
            {
                if (!string.IsNullOrWhiteSpace(s.ReturnRef))
                {
                    string err = CheckReturnLink(vcs, s, scanId, source, out link, out evidenceWhy);
                    if (err != null) { res.Errors.Add(err); return res; }
                    if (evidenceWhy != null) { s.SourceMode = OcrNoteSource.PurchaseReturn; link = null; }   // JE PRT ครบอยู่แล้ว → หลักฐาน
                }
                bool paid = s.SourceMode == OcrNoteSource.NextAcc && source != null && source.BalanceDue <= 0.01m;
                string eff = paid ? VendorCnPolicy.EffectiveSettlement(s.Settlement, true, link != null && link.IsRefund) : OcrNoteSettlement.Auto;
                if (eff == OcrNoteSettlement.Bank && string.IsNullOrWhiteSpace(s.RefundAccountId) && link != null && link.IsRefund)
                    s.RefundAccountId = link.PaidHowAccountId;
                if (!string.Equals(eff, s.Settlement ?? OcrNoteSettlement.Auto, StringComparison.Ordinal) && paid)
                    res.Notes.Add("ขาเงิน: ใบต้นทางจ่ายครบแล้ว ⇒ " + (eff == OcrNoteSettlement.Bank ? "รับ/จ่ายผ่านบัญชีที่เลือก"
                        : (s.DocKind == OcrDocKind.VendorCreditNote ? "เก็บเป็นเครดิตกับผู้ขาย" : "ตั้งเจ้าหนี้รอจ่าย")) + " (ไม่ลงเงินสด 111 ที่ไม่มีเงินเข้า-ออกจริง)");
                s.Settlement = eff;
            }

            // ── รวม/ไม่รวม VAT: ผู้ใช้ยังไม่เลือกเอง ⇒ ตามยอดบนกระดาษ (ตัวเดียวกับหน้าเว็บ — กันหน้าเก่า/สคริปต์ไม่ทำงาน) ──
            if (OcrReviewMath.AutoPriceBasis(s))
                res.Notes.Add("ระบบเลือก \"" + (s.PricesIncludeVat ? "ราคาในรายการรวม VAT แล้ว" : "ราคายังไม่รวม VAT") + "\" ให้ตามยอดบนเอกสาร");

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
            if (note && (s.SourceMode == OcrNoteSource.PurchaseReturn || evidenceWhy != null))
            {
                var exists = PurchaseReturnExists(s.ReturnRef);
                if (exists == false) { res.Errors.Add("ไม่พบการคืนสินค้าเลข " + s.ReturnRef + " ในหน้า \"คืนสินค้าให้ผู้ขาย\""); return res; }
                SaveDraft(s, null, null, user);
                SetStatus(scanId, StEvidence, null, null, null, user);
                res.Ok = true; res.Evidence = true;
                res.Message = "บันทึกใบลดหนี้ " + s.SupplierDocNo + " เป็นหลักฐานของการคืนสินค้า " + s.ReturnRef
                    + " แล้ว — ไม่สร้างใบลดหนี้ใน NextAcc ซ้ำ (การคืนสินค้าโพสต์ GL: เจ้าหนี้/เงินคืน · สินค้าคงเหลือ · ภาษีซื้อ ไว้ครบแล้ว)"
                    + (evidenceWhy != null ? " · " + evidenceWhy : "")
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
                            PricesIncludeVat = s.PricesIncludeVat,
                            PaymentAccountId = upd.PaymentAccountId,   // ขาเงินของใบที่ต้นทางจ่ายครบ → บัญชีที่เลือก (ดู BuildUpdate)
                            PaymentType = upd.PaymentType,             // ใบเพิ่มหนี้ "จ่ายทันที" = Cash
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
            if (editable) await TryEnrichContactAsync(client, docId.Value, s, res, user);

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
            if (link != null) LinkReturnAfterApprove(vcs, link, s, scanId, docId.Value, res, user);
            string reclassRef = null;
            if (note) reclassRef = await TryReclassNoteMoneyLegAsync(client, docId.Value, scanId, s, res, cnPrimary, vcs, user);

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
                var t = OcrReviewMath.CalcTotals(s);
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

        /// <summary>
        /// คำตัดสินข้อ 4 (PHASE19_39): เติมเฉพาะช่องที่ "ว่าง" ของผู้ติดต่อ NextAcc (ไม่ทับ) เพราะ §86/4 อ่านที่อยู่/สาขาจาก Contact
        /// (TaxInvoiceCompletenessChecker.cs:40-47) — ไม่เติม = ภาษีซื้อพัก 11640 ทั้งที่กระดาษครบ. สวิตช์ <c>Ocr_Fill_Missing_Contact</c> (default 1),
        /// บันทึก audit (Logs "OcrReview" ค่าเดิม→ค่าใหม่) และแสดง "เติมข้อมูลผู้ขายใน NextAcc: …" ในกล่องสำเร็จ
        /// </summary>
        private async Task TryEnrichContactAsync(AccountingApiClient client, Guid docId, OcrReviewState s, OcrSubmitResult res, string user)
        {
            try
            {
                var doc = (await client.GetDocumentAsync(docId))?.data;
                if (doc?.Contact == null || doc.Contact.Id == Guid.Empty) return;
                var c = (await client.GetContactAsync(doc.Contact.Id))?.data;
                if (c == null) return;
                var upd = new UpdateContactRequest();
                var parts = new List<string>();
                if (string.IsNullOrWhiteSpace(c.Address) && !string.IsNullOrWhiteSpace(s.SupplierAddress)) { upd.Address = s.SupplierAddress.Trim(); parts.Add("ที่อยู่ \"" + Cut(upd.Address, 80) + "\""); }
                string br = OcrReviewMath.NormalizeBranch(s.SupplierBranch);
                if (string.IsNullOrWhiteSpace(c.BranchCode) && OcrReviewMath.IsValidBranch(br)) { upd.BranchCode = br; parts.Add("สาขา " + br + (br == "00000" ? " (สำนักงานใหญ่)" : "")); }
                if (string.IsNullOrWhiteSpace(c.TaxId) && OcrReviewMath.IsValidThaiTaxId(s.SupplierTaxId)) { upd.TaxId = OcrReviewMath.Digits(s.SupplierTaxId); parts.Add("เลขผู้เสียภาษี " + upd.TaxId); }
                if (parts.Count == 0) return;
                bool enabled = true;
                try { enabled = new AccountingConfig(_conn).GetConfigValue("Ocr_Fill_Missing_Contact", "1") != "0"; } catch { }
                if (!enabled)
                {
                    res.Notes.Add("ผู้ติดต่อ " + (c.Name ?? "") + " ใน NextAcc ยังขาด " + string.Join(", ", parts.Select(p => p.Split(' ')[0]))
                        + " — ไม่ได้เติมอัตโนมัติ (ปิด Ocr_Fill_Missing_Contact) · ภาษีซื้ออาจพัก 11640 จนกว่าจะเติมใน NextAcc");
                    return;
                }
                // null = คงค่าเดิม (DocumentService.cs:13897-13916) — ไม่ทับข้อมูลที่ผู้ติดต่อมีอยู่แล้ว
                await client.UpdateContactAsync(c.Id, upd);
                res.ContactFilled = string.Join(", ", parts);
                res.Notes.Add("เติมข้อมูลผู้ขายใน NextAcc: " + res.ContactFilled + " (เฉพาะช่องที่ว่าง — ไม่ทับข้อมูลเดิม)");
                try
                {
                    _code.Logs(_conn, "OcrReview", "ContactFill contact=" + c.Id + " (" + (c.Name ?? "") + ") doc=" + docId + " scan=" + s.ScanId + " — "
                        + (upd.Address != null ? "Address: ''→'" + Cut(upd.Address, 200) + "'; " : "")
                        + (upd.BranchCode != null ? "BranchCode: ''→'" + upd.BranchCode + "'; " : "")
                        + (upd.TaxId != null ? "TaxId: ''→'" + upd.TaxId + "'; " : ""), user ?? "SYSTEM");
                }
                catch { }
            }
            catch (Exception ex) { res.Notes.Add("เติมข้อมูลผู้ขายใน NextAcc ไม่สำเร็จ: " + ShortError(ex)); }
        }

        /// <summary>
        /// ตรวจการผูกใบลดหนี้ ↔ การคืนสินค้า (PHASE19_39). คืนข้อความผิดพลาด หรือ null.
        /// evidenceWhy ≠ null = การคืนนี้ลง JE PRT ครบแล้ว (ก่อนเปิดนโยบาย / ยังไม่วางบิล / ปิดแบบผู้ขายไม่ออกที่กลับ VAT แล้ว) → เก็บเป็นหลักฐาน
        /// </summary>
        private string CheckReturnLink(VendorCnService vcs, OcrReviewState s, Guid scanId, DocumentResponse source, out ReturnCnLink link, out string evidenceWhy)
        {
            evidenceWhy = null;
            string rr = (s.ReturnRef ?? "").Trim();
            link = vcs.GetReturn(rr);
            if (link == null)
            {
                if (PurchaseReturnExists(rr) == true)
                {
                    evidenceWhy = "การคืนนี้บันทึกก่อนเปิดนโยบายใบลดหนี้เป็นตัวจริง (JE PRT ลงเจ้าหนี้/สต๊อก/ภาษีซื้อครบแล้ว)";
                    return null;
                }
                return "ไม่พบการคืนสินค้าเลข " + rr + " ในหน้า \"คืนสินค้าให้ผู้ขาย\"";
            }
            if (link.CnStatus == ReturnCnStatus.Linked && link.CnScanId.HasValue && link.CnScanId.Value != scanId)
                return "การคืน " + rr + " ผูกกับใบลดหนี้ " + (link.CnDocNo ?? link.CnSupplierDocNo ?? "อื่น") + " แล้ว — หนึ่งการคืนผูกได้หนึ่งใบลดหนี้";
            if (link.CnStatus == ReturnCnStatus.NotRequired && link.PrtJournal == ReturnPrtJournal.Full)
            {
                evidenceWhy = VendorCnPolicy.IsBilledCounter(link.CounterKey)
                    ? "การคืนนี้บันทึกแบบ \"ผู้ขายไม่ออกใบลดหนี้\" (JE PRT ครบแล้ว) — ถ้าต้องการให้ภาษีซื้อลดตามใบนี้ ให้ผู้ทำบัญชีกลับ JE " + rr + " แล้วบันทึกใบลดหนี้ใหม่โดยไม่ผูกการคืน"
                    : "การคืนนี้ยังไม่วางบิล (ลดยอดพักรับของแล้ว) — ใบแจ้งหนี้ของผู้ขายควรออกยอดสุทธิ";
                return null;
            }
            if (link.IsRefund)
            {
                if (s.SourceMode == OcrNoteSource.Outside)
                    return "การคืน " + rr + " บันทึกว่าผู้ขายคืนเงินแล้ว — ใบลดหนี้ต้องอ้างใบต้นทางใน NextAcc ที่จ่ายแล้ว (ใบเดิมนอกระบบ NextAcc ลงเจ้าหนี้แทนเงินที่รับ)";
                if (source != null && source.BalanceDue > 0.01m)
                    return "การคืน " + rr + " บันทึกว่าผู้ขายคืนเงินแล้ว แต่ใบต้นทาง " + source.DocumentNumber + " ยังค้างจ่าย — ใบลดหนี้จะลดเจ้าหนี้แทนเงินที่รับ (เงินขาด) · เลือกใบต้นทางที่จ่ายแล้ว หรือบันทึกใบลดหนี้โดยไม่ผูกการคืน";
            }
            string lt = VendorCnService.Digits(link.SupplierTaxId), st = OcrReviewMath.Digits(s.SupplierTaxId);
            if (lt.Length == 13 && st.Length == 13 && lt != st)
                return "ผู้ขายของใบลดหนี้ (" + st + ") ไม่ตรงกับผู้ขายของการคืน " + rr + " (" + lt + ")";
            return null;
        }

        /// <summary>หลังอนุมัติใบลดหนี้ที่ผูกการคืน: ผูก (ตัวสร้าง JE PRT จะข้าม) → คิวกลับ JE PRT ที่ลงไปแล้ว (PRT-…-REV) + ปรับต้นทุน (PRT-…-DIF)</summary>
        private void LinkReturnAfterApprove(VendorCnService vcs, ReturnCnLink link, OcrReviewState s, Guid scanId, Guid docId, OcrSubmitResult res, string user)
        {
            try
            {
                string inv = vcs.MappedCode("INVENTORY");
                decimal invNet = VendorCnService.InventoryNet(s, inv);
                decimal dif = VendorCnPolicy.CostDifference(invNet, link.CarryingCost);
                var t = OcrReviewMath.CalcTotals(s);
                string revRef = link.PrtJournal == ReturnPrtJournal.None ? null : VendorCnPolicy.ReverseRef(link.ReturnRef);
                string difRef = Math.Abs(dif) >= 0.005m ? VendorCnPolicy.CostDiffRef(link.ReturnRef) : null;
                if (!vcs.MarkLinked(link.ReturnRef, scanId, docId, res.DocNo, s.SupplierDocNo, invNet, t.Total, revRef, difRef, dif, null, user))
                {
                    res.Notes.Add("⚠ ผูกใบลดหนี้กับการคืน " + link.ReturnRef + " ไม่สำเร็จ (ถูกผูกกับใบอื่นไปแล้ว?) — ตรวจ JE " + link.ReturnRef + " ใน NextAcc ว่าไม่ซ้อนกับใบลดหนี้");
                    return;
                }
                res.LinkedReturnRef = link.ReturnRef;
                var sync = new AccountingSyncService(_conn);
                DateTime d = OcrReviewMath.ParseDate(s.DocDate) ?? DateTime.Today;
                var parts = new List<string>();
                if (revRef != null)
                {
                    long q = sync.EnqueuePurchaseReturnReverse(link.ReturnRef, d, "ใบลดหนี้ " + (res.DocNo ?? s.SupplierDocNo ?? ""));
                    parts.Add(q > 0 ? "กลับ JE ที่ลงระหว่างรอ " + revRef + " (คิว #" + q + ")" : "⚠ เข้าคิวกลับ " + revRef + " ไม่สำเร็จ — กลับ JE " + link.ReturnRef + " ใน NextAcc เอง");
                }
                if (difRef != null)
                {
                    long q2 = sync.EnqueuePurchaseReturnCostDiff(link.ReturnRef, dif, d, link.SupplierName, res.DocNo);
                    parts.Add(q2 > 0 ? "ปรับต้นทุน " + OcrReviewMath.Money(dif) + " " + difRef + " (คิว #" + q2 + ")" : "⚠ เข้าคิวปรับต้นทุน " + difRef + " ไม่สำเร็จ");
                }
                res.Notes.Add("ผูกกับการคืนสินค้า " + link.ReturnRef + " — ใบลดหนี้นี้เป็นรายการจริง" + (parts.Count > 0 ? ": " + string.Join(" · ", parts) : " (ต้นทุนตรง ไม่ต้องปรับ)"));
                _code.Logs(_conn, "VendorCn", "Linked " + link.ReturnRef + " ↔ CN " + (res.DocNo ?? docId.ToString()) + " scan=" + scanId
                    + " invNet=" + OcrReviewMath.Money(invNet) + " carrying=" + OcrReviewMath.Money(link.CarryingCost) + " dif=" + OcrReviewMath.Money(dif) + " rev=" + (revRef ?? "-"), user ?? "SYSTEM");
            }
            catch (Exception ex) { res.Notes.Add("⚠ ผูกการคืน " + link.ReturnRef + " ไม่สำเร็จ: " + ShortError(ex) + " — กดส่งอีกครั้ง (ทำต่อจากขั้นที่ค้าง)"); }
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
        /// ใบลด/เพิ่มหนี้ที่ใบต้นทางชำระครบ: เราตั้ง PaymentAccountId ของใบ = บัญชีที่เลือกแล้ว (BuildUpdate) — NextAcc HEAD ลงขาเงินที่บัญชีนั้นตรง
        /// (DocumentService.cs:18225-18229 V-CN-03) ⇒ ตรงนี้แค่ยืนยัน · NextAcc รุ่นก่อน ลง "111" ตายตัว ⇒ โพสต์ JE ย้ายขา (ExternalRef idempotent)
        /// ไปบัญชีที่เลือก (ธนาคาร/21230 ที่ใบเดิมจ่าย · เก็บเป็นยอดกับผู้ขาย = เจ้าหนี้)
        /// </summary>
        private async Task<string> TryReclassNoteMoneyLegAsync(AccountingApiClient client, Guid docId, Guid scanId, OcrReviewState s, OcrSubmitResult res,
            bool cnPrimary, VendorCnService vcs, string user)
        {
            if (s.Settlement != OcrNoteSettlement.Bank && s.Settlement != OcrNoteSettlement.VendorAccount) return null;
            bool cn = s.DocKind == OcrDocKind.VendorCreditNote;
            bool vendorLeg = s.Settlement == OcrNoteSettlement.VendorAccount;
            try
            {
                var mapper = new AccountingDataMapper(_conn);
                string target = null;
                bool vcrFallback = false;
                if (s.Settlement == OcrNoteSettlement.Bank)
                {
                    Guid acc;
                    if (Guid.TryParse(s.RefundAccountId ?? "", out acc)) target = mapper.GetAccountCodeFromId(acc);
                }
                else
                {
                    string ap = null;
                    try { ap = mapper.GetAccountCode("ACCOUNTS_PAYABLE"); } catch { ap = null; }
                    // คำตัดสินข้อ 3: เครดิตกับผู้ขาย (CN) → VENDOR_CREDIT_RECEIVABLE ถ้าผูกไว้ ไม่งั้นเจ้าหนี้การค้า (ยอดเดบิต) · ใบเพิ่มหนี้ → เจ้าหนี้
                    target = cn && cnPrimary ? VendorCnPolicy.VendorCreditAccount(vcs.MappedCode(VendorCnService.MapVendorCredit), ap, out vcrFallback) : ap;
                    if (cn && cnPrimary && vcrFallback && !string.IsNullOrEmpty(target))
                        res.Notes.Add("ยังไม่ผูกบัญชี VENDOR_CREDIT_RECEIVABLE — เครดิตกับผู้ขายลงเจ้าหนี้การค้า " + target + " เป็นยอดเดบิต (ผูก mapping แล้วยอดใหม่จะแยกบัญชี)");
                }
                if (string.IsNullOrEmpty(target)) { res.Notes.Add("⚠ ย้ายขาเงินไม่ได้: หา code บัญชีปลายทางไม่พบ (Sync ผังบัญชี/ตั้ง mapping ACCOUNTS_PAYABLE) — ปรับใน NextAcc เอง"); return null; }

                var r = await client.GetAsync<ApiResponse<PagedResponse<JournalEntryResponse>>>(
                    "/api/companies/" + new AccountingConfig(_conn).CompanyId + "/accounting/journals?page=1&pageSize=20&sourceDocumentId=" + docId);
                var je = (r?.data?.Items ?? new List<JournalEntryResponse>())
                    .Where(j => j.OriginalEntryId == null && (j.ReversedByEntryId == null || j.ReversedByEntryId == Guid.Empty)
                             && j.Status != 2 && j.Status != 6 && j.Lines != null && j.Lines.Count > 0)   // 2 = JE voided, 6 = ชื่อ "Voided" ผ่าน DocumentStatusConverter
                    .OrderByDescending(j => j.CreatedAt).FirstOrDefault();
                if (je == null) { res.Notes.Add("⚠ ไม่พบ JE ของเอกสาร — ตรวจขาเงินใน NextAcc เอง"); return null; }
                // NextAcc รุ่นใหม่ลงบัญชีที่เลือกเองแล้ว (PaymentAccountId ของใบ / ใบเพิ่มหนี้เงินเชื่อ = เจ้าหนี้ของใบต้นทาง) ⇒ ไม่ต้องย้าย
                var direct = je.Lines.FirstOrDefault(l => string.Equals(l.AccountCode, target, StringComparison.OrdinalIgnoreCase)
                                                       && (cn ? l.DebitAmount > 0 : l.CreditAmount > 0));
                if (direct != null)
                {
                    decimal damt = OcrReviewMath.R2(cn ? direct.DebitAmount : direct.CreditAmount);
                    res.Notes.Add("NextAcc ลงบัญชี " + target + " ตรงตามที่เลือกแล้ว " + OcrReviewMath.Money(damt) + " (ไม่ผ่านเงินสด)");
                    if (vendorLeg && cnPrimary) RecordVendorBalance(vcs, s, scanId, res, cn, damt, target, null, user);
                    return null;
                }
                var leg = je.Lines.FirstOrDefault(l => (l.AccountCode ?? "").StartsWith("111", StringComparison.Ordinal)
                                                   && (cn ? l.DebitAmount > 0 : l.CreditAmount > 0));
                if (leg == null)
                {
                    res.Notes.Add(cn ? "ใบต้นทางยังค้างจ่าย — NextAcc ลดเจ้าหนี้ให้แล้ว (ไม่มีขาเงินสดให้ย้าย)"
                                     : "NextAcc ตั้งเจ้าหนี้เพิ่มแล้ว (ไม่มีขาเงินสดให้ย้าย)");
                    return null;
                }
                decimal amt = OcrReviewMath.R2(cn ? leg.DebitAmount : leg.CreditAmount);
                if (string.Equals(leg.AccountCode, target, StringComparison.OrdinalIgnoreCase))
                {
                    if (vendorLeg && cnPrimary) RecordVendorBalance(vcs, s, scanId, res, cn, amt, target, null, user);
                    return null;
                }
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
                if (vendorLeg && cnPrimary) RecordVendorBalance(vcs, s, scanId, res, cn, amt, target, reff, user);
                return reff;
            }
            catch (Exception ex)
            {
                res.Notes.Add("⚠ ย้ายขาเงินไม่สำเร็จ: " + ShortError(ex) + " — เอกสารอนุมัติแล้ว ปรับขาเงินใน NextAcc เอง");
                return null;
            }
        }

        /// <summary>ยอดกับผู้ขาย (Vendor_Credit_Ledger): CN → CN_CREDIT (หักได้ตอนจ่ายใบซื้อเชื่อ) · DN → DN_PAYABLE (จ่ายได้ที่หน้าชำระใบซื้อเชื่อ)</summary>
        private void RecordVendorBalance(VendorCnService vcs, OcrReviewState s, Guid scanId, OcrSubmitResult res, bool cn, decimal amt, string accountCode, string journalRef, string user)
        {
            try
            {
                if (!vcs.HasLedgerTable() || amt <= 0m) return;
                Guid cid;
                Guid? contactId = Guid.TryParse(s.SourceContactId ?? "", out cid) && cid != Guid.Empty ? cid : (Guid?)null;
                string name = !string.IsNullOrWhiteSpace(s.SupplierName) ? s.SupplierName.Trim() : s.SourceContactName;
                int id = vcs.AddLedger(cn ? VendorCnService.EtCnCredit : VendorCnService.EtDnPayable, s.VendorId > 0 ? (int?)s.VendorId : null,
                    s.SupplierTaxId, name, contactId, amt, accountCode, "OCR-" + scanId.ToString("N"), res.DocNo ?? s.SupplierDocNo, null, null, journalRef,
                    (cn ? "ใบลดหนี้ผู้ขาย " : "ใบเพิ่มหนี้ผู้ขาย ") + (s.SupplierDocNo ?? "") + " อ้าง " + (s.SourceDocNo ?? "-"), user);
                if (id > 0)
                    res.Notes.Add(cn ? "บันทึกเครดิตกับผู้ขาย " + OcrReviewMath.Money(amt) + " (บัญชี " + accountCode + ") — หักได้ตอนบันทึกจ่ายใบซื้อเชื่อครั้งถัดไป"
                                     : "ตั้งเจ้าหนี้รอจ่าย " + OcrReviewMath.Money(amt) + " (บัญชี " + accountCode + ") — จ่ายได้ที่หน้า \"ชำระใบซื้อเชื่อ\" ส่วนใบเพิ่มหนี้รอจ่าย");
            }
            catch (Exception ex) { res.Notes.Add("⚠ บันทึกยอดกับผู้ขายไม่สำเร็จ: " + ShortError(ex)); }
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
