using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Take_Time_BangPhra.Integration
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //  ตัวตรวจเทียบ "เอกสาร NextAcc ↔ ใบเสร็จ TakeTime" รายการจอง (PHASE19_35 — เคส #149286)
    //
    //  ที่มา: หน้าต่าง NextAcc ของการจองเดิมวนตาม Account_Receipt แล้ว resolve เอกสาร "ใบเดียว" ต่อใบเสร็จ
    //  (LookupNexaaccDocIdByReceipt, TOP 1 COMPLETED) ⇒ เอกสารจากคิวที่ถูก SUPERSEDED / ใบเสร็จที่ถูกลบ / เลขที่เปลี่ยน
    //  ไม่เคยถูกแสดง — REC-20260819-0002 และ TIV-20260904-0002 ค้าง "ชำระแล้ว" บน NextAcc โดยไม่มีใครเห็น
    //
    //  ชุดฝั่ง NextAcc  = GET /api/companies/{cid}/document/by-booking/RES-{id} (DocumentController.cs:515-521 →
    //                    DocumentService.GetDocumentsByBookingAsync :5398-5411 — ทุกใบที่ BookingNumber ตรงและไม่ Voided)
    //                  ∪ เอกสารฝั่งรับในช่วงวันที่ (GET /api/integration/documents) ที่ Reference = RES-{id} / RES-{id}-*
    //                    (เผื่อเอกสารเก่าที่ไม่มี BookingNumber) ∪ ตัวชี้ของใบเสร็จที่ไม่อยู่ในสองชุดแรก (GET รายใบ)
    //  ชุดฝั่ง TakeTime = Account_Receipt ทุกสถานะ + คิว CREATE_RECEIPT_DOCUMENT / VOID_RECEIPT ทุกสถานะ (รวม SUPERSEDED/
    //                    CANCELLED) ของการจองนี้ + Payment_Slips
    //  จำแนกเอกสาร: MATCHED / ORPHAN_SUPERSEDED / ORPHAN_DELETED_LOCAL / ORPHAN_OTHER_BOOKING? / UNKNOWN_NEXTACC_ONLY
    //  ธงรายใบเสร็จ: DUPLICATE / VOID_STUCK / AMOUNT_MISMATCH / MISSING_ON_NEXTACC · ธงรายการจอง: DEPOSIT_MISMATCH
    //  ผลถูกเก็บใน Accounting_Reconcile_Finding (ไม่มีตาราง = แสดงอย่างเดียว ไม่เก็บ) — งานรายคืน/ตรวจสุขภาพ/Telegram อ่านจากตารางนี้
    // ══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>เอกสาร NextAcc 1 ใบในผลตรวจเทียบ</summary>
    public class ReconNaDoc
    {
        public Guid Id { get; set; }
        public string Number { get; set; }
        public string DocTypeLabel { get; set; }
        public string Status { get; set; }
        public string Date { get; set; }
        public decimal Total { get; set; }
        public decimal DepositApplied { get; set; }
        public decimal DepositRefunded { get; set; }
        public bool IsDeposit { get; set; }
        public string Contact { get; set; }
        public string EtaxStatus { get; set; }
        public string Reference { get; set; }
        public string Notes { get; set; }
        /// <summary>BOOKING (by-booking) / RANGE (รายการช่วงวันที่) / POINTER (GET ตัวชี้ของใบเสร็จ)</summary>
        public string Source { get; set; }
        // ── ผล ──
        public string Class { get; set; }
        public string ReceiptNumber { get; set; }
        public string Detail { get; set; }
        public bool VoidStuck { get; set; }
        public bool CanVoid { get; set; }
        public string VoidWarning { get; set; }
    }

    /// <summary>ใบเสร็จ TakeTime 1 ใบในผลตรวจเทียบ</summary>
    public class ReconReceipt
    {
        public string ReceiptNumber { get; set; }
        public string LocalStatus { get; set; }
        public bool IsDeposit { get; set; }
        public decimal Amount { get; set; }
        public decimal DepositApplied { get; set; }
        public string Date { get; set; }
        public Guid? Pointer { get; set; }
        public string PointerNumber { get; set; }
        /// <summary>ACTIVE / VOIDED / MISSING / UNKNOWN / NONE</summary>
        public string PointerState { get; set; }
        public bool HasCompletedCreate { get; set; }
        public bool HasPendingCreate { get; set; }
        public int ActiveDocs { get; set; }
        public int SlipCount { get; set; }
        public List<string> Flags { get; set; } = new List<string>();
        public string Detail { get; set; }
    }

    /// <summary>แถวคิวที่เกี่ยวกับการจอง (CREATE/VOID ของใบเสร็จ)</summary>
    public class ReconQueueRow
    {
        public long Id { get; set; }
        /// <summary>CREATE / VOID</summary>
        public string Action { get; set; }
        public string Status { get; set; }
        public string ReceiptNumber { get; set; }
        public Guid DocId { get; set; }
        public DateTime Created { get; set; }
    }

    public class ReservationReconciliation
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int ReservationId { get; set; }
        public string CheckedAt { get; set; }
        public List<ReconNaDoc> Docs { get; set; } = new List<ReconNaDoc>();
        public List<ReconReceipt> Receipts { get; set; } = new List<ReconReceipt>();
        public decimal NaDepositTotal { get; set; }
        public decimal LocalDepositTotal { get; set; }
        public decimal ReservationDeposit { get; set; }
        public int UnlinkedSlips { get; set; }
        public List<string> BookingFlags { get; set; } = new List<string>();
        public List<string> Notes { get; set; } = new List<string>();
        public int IssueCount { get; set; }
    }

    public partial class AccountingSyncService
    {
        public const string RecMatched = "MATCHED";
        public const string RecOrphanSuperseded = "ORPHAN_SUPERSEDED";
        public const string RecOrphanDeletedLocal = "ORPHAN_DELETED_LOCAL";
        public const string RecOrphanOtherBooking = "ORPHAN_OTHER_BOOKING?";
        public const string RecUnknown = "UNKNOWN_NEXTACC_ONLY";

        private static bool IsCancelledLocalStatus(string s)
        {
            string v = (s ?? "").Trim();
            return v.StartsWith("Cancel", StringComparison.OrdinalIgnoreCase)
                || v.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)
                || v.Contains("ยกเลิก");
        }

        private static bool IsEtaxReachedRd(string etaxStatus)
        {
            return string.Equals(etaxStatus, "Submitted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(etaxStatus, "Accepted", StringComparison.OrdinalIgnoreCase)
                || etaxStatus == "2" || etaxStatus == "3";
        }

        /// <summary>
        /// ตรวจเทียบเอกสาร NextAcc ของการจองกับใบเสร็จ/คิวฝั่ง TakeTime (read-only ต่อ NextAcc).
        /// <paramref name="prefetchedRange"/> = เอกสารฝั่งรับช่วงวันที่ที่ดึงไว้แล้ว (งานรายคืนดึงครั้งเดียวทั้งช่วง) ·
        /// null = ดึงเองรอบวันที่ของใบเสร็จ (±3 วัน, ไม่เกิน 120 วัน)
        /// </summary>
        public async Task<ReservationReconciliation> GetReservationReconciliationAsync(int reservationId,
            List<NextAccPaymentDoc> prefetchedRange = null)
        {
            var r = new ReservationReconciliation { ReservationId = reservationId, CheckedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm") };
            if (reservationId <= 0) { r.Message = "รหัสการจองไม่ถูกต้อง"; return r; }
            if (!_config.IsConfigured) { r.Message = "ยังไม่ได้ตั้งค่า NextAcc"; return r; }
            if (!_config.CanUseCompanyEndpoints)
            {
                r.Message = "ต้องเปิด company endpoints (CompanyId + Nexaacc_Company_Endpoints=1) จึงจะอ่านเอกสารตามเลขจองจาก NextAcc ได้";
                return r;
            }
            string resRef = $"RES-{reservationId}";

            // ── 1) ใบเสร็จ local ทุกสถานะ ──
            DataTable rdt = null;
            bool hasLinkCol = true;
            try
            {
                rdt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID, ISNULL(Total_Amount,0) AS Amt, ISNULL(Deposit_Applied_Amount,0) AS DepApp, ISNULL(IsDeposit,0) AS IsDep,
                             Status, Created_Date, Nexaacc_Doc_Id
                      FROM Account_Receipt WHERE Reservation_ID = @rid ORDER BY Created_Date, ID",
                    new Dictionary<string, object> { { "@rid", reservationId } });
            }
            catch
            {
                hasLinkCol = false;
                rdt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID, ISNULL(Total_Amount,0) AS Amt, ISNULL(Deposit_Applied_Amount,0) AS DepApp, ISNULL(IsDeposit,0) AS IsDep,
                             Status, Created_Date
                      FROM Account_Receipt WHERE Reservation_ID = @rid ORDER BY Created_Date, ID",
                    new Dictionary<string, object> { { "@rid", reservationId } });
            }
            DateTime minDate = DateTime.MaxValue, maxDate = DateTime.MinValue;
            if (rdt != null)
                foreach (DataRow row in rdt.Rows)
                {
                    string rn = row["ID"]?.ToString();
                    if (string.IsNullOrEmpty(rn)) continue;
                    DateTime cd = row["Created_Date"] != DBNull.Value ? Convert.ToDateTime(row["Created_Date"]) : DateTime.MinValue;
                    if (cd != DateTime.MinValue) { if (cd < minDate) minDate = cd; if (cd > maxDate) maxDate = cd; }
                    var rc = new ReconReceipt
                    {
                        ReceiptNumber = rn,
                        LocalStatus = row["Status"] == DBNull.Value || string.IsNullOrWhiteSpace(row["Status"]?.ToString()) ? "Normal" : row["Status"].ToString(),
                        IsDeposit = Convert.ToInt32(row["IsDep"]) == 1,
                        Amount = Convert.ToDecimal(row["Amt"]),
                        DepositApplied = Convert.ToDecimal(row["DepApp"]),
                        Date = cd == DateTime.MinValue ? "-" : cd.ToString("dd/MM/yyyy"),
                        PointerState = "NONE"
                    };
                    Guid lg;
                    if (hasLinkCol && row["Nexaacc_Doc_Id"] != DBNull.Value && Guid.TryParse(row["Nexaacc_Doc_Id"].ToString(), out lg) && lg != Guid.Empty)
                        rc.Pointer = lg;
                    r.Receipts.Add(rc);
                }

            // ── 2) คิว CREATE/VOID ของการจองนี้ (ทุกสถานะ) ──
            var queue = LoadReconcileQueueRows(reservationId, r.Receipts.Select(x => x.ReceiptNumber).ToList());
            foreach (var q in queue)
                if (q.Created != DateTime.MinValue) { if (q.Created < minDate) minDate = q.Created; if (q.Created > maxDate) maxDate = q.Created; }

            foreach (var rc in r.Receipts)
            {
                var creates = queue.Where(q => q.Action == "CREATE" && string.Equals(q.ReceiptNumber, rc.ReceiptNumber, StringComparison.OrdinalIgnoreCase)).ToList();
                rc.HasCompletedCreate = creates.Any(q => q.Status == "COMPLETED");
                rc.HasPendingCreate = creates.Any(q => q.Status == "PENDING" || q.Status == "PROCESSING" || q.Status == "FAILED");
                if (rc.Pointer == null)
                {
                    // ไม่มีคอลัมน์ผูก → คิว CREATE COMPLETED ล่าสุดที่ไม่มี VOID สำเร็จตามหลัง
                    var last = creates.Where(q => q.Status == "COMPLETED" && q.DocId != Guid.Empty).OrderByDescending(q => q.Id).FirstOrDefault();
                    if (last != null && !queue.Any(v => v.Action == "VOID" && v.Status == "COMPLETED" && v.DocId == last.DocId && v.Id > last.Id))
                        rc.Pointer = last.DocId;
                }
            }

            // ── 3) เอกสารบน NextAcc ──
            var byId = new Dictionary<Guid, ReconNaDoc>();
            var bb = await _apiClient.GetDocumentsByBookingAsync(resRef);   // error → ให้ผู้เรียกเห็น (ไม่สรุปว่า "ไม่มีเอกสาร")
            if (bb?.data != null)
                foreach (var d in bb.data)
                {
                    if (d == null || d.Id == Guid.Empty || d.Status == NexaaccDocumentStatus.Voided) continue;
                    byId[d.Id] = FromDocumentResponse(d, "BOOKING");
                }

            // ช่วงวันที่ (เอกสารเก่าที่ไม่มี BookingNumber แต่ Reference = RES-{id})
            List<NextAccPaymentDoc> range = prefetchedRange;
            if (range == null && minDate != DateTime.MaxValue)
            {
                DateTime from = minDate.Date.AddDays(-3), to = maxDate.Date.AddDays(3);
                if ((to - from).TotalDays > 120) from = to.AddDays(-120);
                try { range = await FetchNextAccReceiptDocumentsAsync(from, to); }
                catch (Exception fx) { r.Notes.Add("ดึงรายการเอกสารช่วงวันที่ไม่สำเร็จ: " + fx.Message); }
            }
            if (range != null)
                foreach (var d in range)
                {
                    if (d == null || d.Id == Guid.Empty || byId.ContainsKey(d.Id)) continue;
                    string st = (d.Status ?? "").Trim();
                    if (st.Equals("Voided", StringComparison.OrdinalIgnoreCase) || st.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
                        || st.Equals("Canceled", StringComparison.OrdinalIgnoreCase) || st.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string rf = (d.Reference ?? "").Trim();
                    if (!(rf.Equals(resRef, StringComparison.OrdinalIgnoreCase) || rf.StartsWith(resRef + "-", StringComparison.OrdinalIgnoreCase)))
                        continue;
                    byId[d.Id] = new ReconNaDoc
                    {
                        Id = d.Id, Number = d.DocumentNumber, DocTypeLabel = d.DocumentTypeLabel ?? d.DocumentType, Status = d.Status,
                        Date = d.DocumentDate == DateTime.MinValue ? "-" : d.DocumentDate.ToString("dd/MM/yyyy"),
                        Total = d.TotalAmount, Contact = d.ContactName, Reference = d.Reference, Notes = d.Notes, Source = "RANGE"
                    };
                }

            // ตัวชี้ของใบเสร็จที่ไม่อยู่ในชุด → GET รายใบ (ยืนยันว่ายังอยู่/ถูก void/หาย)
            foreach (var rc in r.Receipts.Where(x => x.Pointer != null))
            {
                Guid pid = rc.Pointer.Value;
                ReconNaDoc have;
                if (byId.TryGetValue(pid, out have)) { rc.PointerState = "ACTIVE"; rc.PointerNumber = have.Number; continue; }
                try
                {
                    var g = await _apiClient.GetDocumentAsync(pid);
                    if (g?.data == null) { rc.PointerState = "UNKNOWN"; continue; }
                    rc.PointerNumber = g.data.DocumentNumber;
                    if (g.data.Status == NexaaccDocumentStatus.Voided || g.data.Status == NexaaccDocumentStatus.Rejected)
                        rc.PointerState = "VOIDED";
                    else
                    {
                        rc.PointerState = "ACTIVE";
                        byId[pid] = FromDocumentResponse(g.data, "POINTER");
                    }
                }
                catch (AccountingApiException gx) when (gx.StatusCode == 404 || gx.StatusCode == 410) { rc.PointerState = "MISSING"; }
                catch (Exception gx2) { rc.PointerState = "UNKNOWN"; r.Notes.Add($"อ่านเอกสารของใบ {rc.ReceiptNumber} ไม่ได้: {gx2.Message}"); }
            }
            r.Docs = byId.Values.ToList();

            // ── 4) เอกสารที่ไม่ใช่ของการจองนี้ตามคิว/ตัวชี้ แต่ถูกใบเสร็จของการจองอื่นอ้าง ──
            var knownHere = new HashSet<Guid>(queue.Where(q => q.DocId != Guid.Empty).Select(q => q.DocId));
            foreach (var rc in r.Receipts) if (rc.Pointer != null) knownHere.Add(rc.Pointer.Value);
            var otherClaims = LoadOtherBookingClaims(reservationId, r.Docs.Where(d => !knownHere.Contains(d.Id)).Select(d => d.Id).ToList());

            // ── 5) จำแนก ──
            ClassifyReconciliation(r, queue, otherClaims, DateTime.Now);

            // ── 6) สลิป + ยอดมัดจำการจอง ──
            foreach (var rc in r.Receipts)
            {
                try { var sd = QuerySlipsForReceipt(rc.ReceiptNumber); rc.SlipCount = sd?.Rows.Count ?? 0; } catch { }
            }
            try
            {
                var us = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) FROM Payment_Slips
                      WHERE Reservation_ID = @rid AND ISNULL(VerificationStatus, '') <> 'REJECTED'
                        AND ISNULL(CONVERT(NVARCHAR(50), Account_Receipt_ID), '') IN ('', '0')",
                    new Dictionary<string, object> { { "@rid", reservationId } });
                r.UnlinkedSlips = us != null && us.Rows.Count > 0 && us.Rows[0][0] != DBNull.Value ? Convert.ToInt32(us.Rows[0][0]) : 0;
            }
            catch { }
            try
            {
                var rv = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 ISNULL(Deposit, 0) FROM Reservation WHERE ID = @rid",
                    new Dictionary<string, object> { { "@rid", reservationId } });
                if (rv != null && rv.Rows.Count > 0 && rv.Rows[0][0] != DBNull.Value) r.ReservationDeposit = Convert.ToDecimal(rv.Rows[0][0]);
            }
            catch { }

            r.Success = true;
            if (r.IssueCount == 0) r.Message = $"เอกสารบน NextAcc {r.Docs.Count} ใบ ตรงกับใบเสร็จทั้งหมด";
            else r.Message = $"พบ {r.IssueCount} จุดที่ไม่ตรงกัน (เอกสารบน NextAcc {r.Docs.Count} ใบ · ใบเสร็จในระบบ {r.Receipts.Count} ใบ)";
            return r;
        }

        private static ReconNaDoc FromDocumentResponse(DocumentResponse d, string source)
        {
            return new ReconNaDoc
            {
                Id = d.Id,
                Number = d.DocumentNumber,
                DocTypeLabel = NexaaccDocTypeLabel(d.DocumentType),
                Status = NexaaccDocStatusLabel(d.Status),
                Date = d.DocumentDate == DateTime.MinValue ? "-" : d.DocumentDate.ToString("dd/MM/yyyy"),
                Total = d.TotalAmount,
                DepositApplied = d.DepositAppliedAmount,
                DepositRefunded = d.DepositRefundedAmount,
                IsDeposit = d.IsDeposit,
                Contact = d.Contact?.Name,
                EtaxStatus = d.EtaxStatus,
                Reference = d.Reference,
                Notes = d.Notes,
                Source = source
            };
        }

        /// <summary>คิว CREATE_RECEIPT_DOCUMENT / VOID_RECEIPT ทุกสถานะที่ผูกการจองนี้ (Entity_ID / reservationId ใน payload)
        /// หรือเลขใบเสร็จของการจองนี้ — รวมใบเสร็จที่ถูกลบไปแล้ว (เหลือแต่ในคิว)</summary>
        private List<ReconQueueRow> LoadReconcileQueueRows(int reservationId, List<string> receiptNumbers)
        {
            var list = new List<ReconQueueRow>();
            try
            {
                var prm = new Dictionary<string, object>
                {
                    { "@rid", reservationId },
                    { "@pr1", "%\"reservationId\":" + reservationId + ",%" },
                    { "@pr2", "%\"reservationId\":" + reservationId + "}%" }
                };
                var ors = new List<string> { "q.Entity_ID = @rid", "q.Payload LIKE @pr1", "q.Payload LIKE @pr2" };
                int i = 0;
                foreach (string rn in receiptNumbers.Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(40))
                {
                    string esc = rn.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
                    prm["@rn" + i] = "%\"receiptNumber\":\"" + esc + "\"%";
                    ors.Add("q.Payload LIKE @rn" + i);
                    i++;
                }
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT q.ID, q.Action_Type, q.Status, q.Created_Date, q.Nexaacc_Response_Id, q.Payload
                      FROM Accounting_Sync_Queue q
                      WHERE q.Action_Type IN ('CREATE_RECEIPT_DOCUMENT', 'VOID_RECEIPT')
                        AND (" + string.Join(" OR ", ors) + @")
                      ORDER BY q.ID", prm);
                if (dt == null) return list;
                foreach (DataRow row in dt.Rows)
                {
                    string payload = row["Payload"]?.ToString() ?? "";
                    var mRn = System.Text.RegularExpressions.Regex.Match(payload, "\"receiptNumber\"\\s*:\\s*\"([^\"]*)\"");
                    var mRes = System.Text.RegularExpressions.Regex.Match(payload, "\"reservationId\"\\s*:\\s*(\\d+)");
                    // payload ระบุการจองอื่นชัดเจน (เช่น Entity_ID ชนกันโดยบังเอิญ) → ไม่ใช่ของเรา
                    if (mRes.Success && mRes.Groups[1].Value != reservationId.ToString()
                        && !(mRn.Success && receiptNumbers.Contains(mRn.Groups[1].Value)))
                        continue;
                    string action = row["Action_Type"]?.ToString() == "VOID_RECEIPT" ? "VOID" : "CREATE";
                    Guid doc;
                    if (action == "VOID")
                    {
                        var mId = System.Text.RegularExpressions.Regex.Match(payload, "\"nexaaccId\"\\s*:\\s*\"([^\"]*)\"");
                        doc = mId.Success ? ExtractGuid(mId.Groups[1].Value) : Guid.Empty;
                    }
                    else doc = ExtractGuid(row["Nexaacc_Response_Id"] == DBNull.Value ? null : row["Nexaacc_Response_Id"].ToString());
                    list.Add(new ReconQueueRow
                    {
                        Id = Convert.ToInt64(row["ID"]),
                        Action = action,
                        Status = row["Status"]?.ToString() ?? "",
                        ReceiptNumber = mRn.Success ? mRn.Groups[1].Value : null,
                        DocId = doc,
                        Created = row["Created_Date"] != DBNull.Value ? Convert.ToDateTime(row["Created_Date"]) : DateTime.MinValue
                    });
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync", $"Reconcile #{reservationId}: อ่านคิวไม่ได้ {ex.Message}", "SYSTEM");
            }
            return list;
        }

        /// <summary>เอกสารที่ "ใบเสร็จของการจองอื่น" อ้าง (คิว CREATE / Nexaacc_Doc_Id) → (เลขใบ, การจอง)</summary>
        private Dictionary<Guid, KeyValuePair<string, int>> LoadOtherBookingClaims(int reservationId, List<Guid> docIds)
        {
            var map = new Dictionary<Guid, KeyValuePair<string, int>>();
            if (docIds == null || docIds.Count == 0) return map;
            var ids = docIds.Distinct().Take(50).ToList();
            var prm = new Dictionary<string, object>();
            var names = new List<string>();
            for (int i = 0; i < ids.Count; i++) { prm["@d" + i] = ids[i].ToString(); names.Add("@d" + i); }
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT Nexaacc_Response_Id, Entity_ID, Payload FROM Accounting_Sync_Queue
                      WHERE Action_Type = 'CREATE_RECEIPT_DOCUMENT' AND Nexaacc_Response_Id IN (" + string.Join(",", names) + ")", prm);
                if (dt != null)
                    foreach (DataRow row in dt.Rows)
                    {
                        Guid g = ExtractGuid(row["Nexaacc_Response_Id"]?.ToString());
                        string payload = row["Payload"]?.ToString() ?? "";
                        var mRn = System.Text.RegularExpressions.Regex.Match(payload, "\"receiptNumber\"\\s*:\\s*\"([^\"]*)\"");
                        var mRes = System.Text.RegularExpressions.Regex.Match(payload, "\"reservationId\"\\s*:\\s*(\\d+)");
                        int res = mRes.Success ? int.Parse(mRes.Groups[1].Value) : (row["Entity_ID"] != DBNull.Value ? Convert.ToInt32(row["Entity_ID"]) : 0);
                        if (g != Guid.Empty && res != reservationId && !map.ContainsKey(g))
                            map[g] = new KeyValuePair<string, int>(mRn.Success ? mRn.Groups[1].Value : "?", res);
                    }
            }
            catch { }
            try
            {
                var dt2 = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID, ISNULL(Reservation_ID, 0) AS Res, CAST(Nexaacc_Doc_Id AS NVARCHAR(36)) AS Doc FROM Account_Receipt
                      WHERE CAST(Nexaacc_Doc_Id AS NVARCHAR(36)) IN (" + string.Join(",", names) + ")", prm);
                if (dt2 != null)
                    foreach (DataRow row in dt2.Rows)
                    {
                        Guid g = ExtractGuid(row["Doc"]?.ToString());
                        int res = Convert.ToInt32(row["Res"]);
                        if (g != Guid.Empty && res != reservationId && !map.ContainsKey(g))
                            map[g] = new KeyValuePair<string, int>(row["ID"]?.ToString(), res);
                    }
            }
            catch { /* ยังไม่รัน PHASE18_32 */ }
            return map;
        }

        /// <summary>
        /// จำแนกผลตรวจเทียบ (pure — ไม่แตะ DB/NextAcc; ทดสอบได้ด้วยข้อมูลจำลอง). r.Docs = เอกสาร NextAcc ที่ยังใช้งาน,
        /// r.Receipts = ใบเสร็จ local พร้อม Pointer/PointerState, queue = คิว CREATE/VOID ของการจอง, otherClaims = เอกสาร→(ใบ, การจองอื่น)
        /// </summary>
        public static void ClassifyReconciliation(ReservationReconciliation r, List<ReconQueueRow> queue,
            Dictionary<Guid, KeyValuePair<string, int>> otherClaims, DateTime now)
        {
            queue = queue ?? new List<ReconQueueRow>();
            otherClaims = otherClaims ?? new Dictionary<Guid, KeyValuePair<string, int>>();
            var receiptByNo = new Dictionary<string, ReconReceipt>(StringComparer.OrdinalIgnoreCase);
            foreach (var rc in r.Receipts) if (!receiptByNo.ContainsKey(rc.ReceiptNumber)) receiptByNo[rc.ReceiptNumber] = rc;
            var pointerOwner = new Dictionary<Guid, ReconReceipt>();
            foreach (var rc in r.Receipts) if (rc.Pointer != null && !pointerOwner.ContainsKey(rc.Pointer.Value)) pointerOwner[rc.Pointer.Value] = rc;
            var createOwner = new Dictionary<Guid, string>();
            foreach (var q in queue.Where(x => x.Action == "CREATE" && x.DocId != Guid.Empty && !string.IsNullOrEmpty(x.ReceiptNumber)).OrderByDescending(x => x.Id))
                if (!createOwner.ContainsKey(q.DocId)) createOwner[q.DocId] = q.ReceiptNumber;
            var knownNos = new HashSet<string>(r.Receipts.Select(x => x.ReceiptNumber), StringComparer.OrdinalIgnoreCase);
            // เลขใบจากคิว CREATE เท่านั้น (VOID ของเอกสารกำพร้าใช้เลขเอกสาร NextAcc แทนเลขใบเสร็จ)
            foreach (var q in queue) if (q.Action == "CREATE" && !string.IsNullOrEmpty(q.ReceiptNumber)) knownNos.Add(q.ReceiptNumber);

            Action<ReconNaDoc, string, string> viaReceipt = (d, rn, how) =>
            {
                d.ReceiptNumber = rn;
                ReconReceipt rc;
                if (receiptByNo.TryGetValue(rn, out rc))
                {
                    if (IsCancelledLocalStatus(rc.LocalStatus))
                    {
                        d.Class = RecOrphanDeletedLocal;
                        d.Detail = $"ใบเสร็จ {rn} ถูกยกเลิกในระบบแล้ว ({rc.LocalStatus}) แต่เอกสารนี้ยังใช้งานอยู่{how}";
                    }
                    else if (rc.Pointer == null)
                    {
                        d.Class = RecMatched;
                        d.Detail = $"ผูกกับใบ {rn}{how} (ใบเสร็จไม่มีตัวชี้ — ผูกจากประวัติคิว)";
                    }
                    else
                    {
                        d.Class = RecOrphanSuperseded;
                        d.Detail = $"เอกสารรุ่นเก่าของใบ {rn}{how} — ตัวชี้ปัจจุบันคือ {rc.PointerNumber ?? rc.Pointer.ToString()} " +
                                   "(void→สร้างใหม่ที่ void ไม่สำเร็จ/ถูกยกเลิก) ⇒ รายได้/เงินลงซ้ำ";
                    }
                }
                else
                {
                    d.Class = RecOrphanDeletedLocal;
                    d.Detail = $"ใบเสร็จ {rn} ไม่มีในระบบแล้ว (ลบ/เปลี่ยนเลข){how} แต่เอกสารยังใช้งานอยู่";
                }
            };

            foreach (var d in r.Docs)
            {
                ReconReceipt prc;
                string crn;
                KeyValuePair<string, int> claim;
                if (pointerOwner.TryGetValue(d.Id, out prc))
                {
                    d.ReceiptNumber = prc.ReceiptNumber;
                    if (IsCancelledLocalStatus(prc.LocalStatus))
                    {
                        d.Class = RecOrphanDeletedLocal;
                        d.Detail = $"ใบเสร็จ {prc.ReceiptNumber} ถูกยกเลิกในระบบแล้ว ({prc.LocalStatus}) แต่เอกสารยังไม่ถูกยกเลิก";
                    }
                    else { d.Class = RecMatched; d.Detail = $"เอกสารปัจจุบันของใบ {prc.ReceiptNumber}"; }
                }
                else if (createOwner.TryGetValue(d.Id, out crn)) viaReceipt(d, crn, "");
                else if (otherClaims.TryGetValue(d.Id, out claim))
                {
                    d.Class = RecOrphanOtherBooking;
                    d.ReceiptNumber = claim.Key;
                    d.Detail = $"ใบ {claim.Key} ของการจอง #{claim.Value} อ้างเอกสารนี้ แต่เอกสารผูกเลขจองนี้ — ตรวจว่าเป็นของการจองไหน";
                }
                else
                {
                    string hit = knownNos.FirstOrDefault(n => (d.Notes ?? "").IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0
                                                            || (d.Reference ?? "").IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (hit != null) viaReceipt(d, hit, " (จับคู่จากหมายเหตุเอกสาร)");
                    else
                    {
                        d.Class = RecUnknown;
                        d.Detail = "ไม่พบที่มาในระบบ (ไม่มีคิว/ใบเสร็จอ้าง) — อาจสร้างซ้ำตอน timeout, สร้างมือบน NextAcc หรือเป็นเงินของการจองอื่น/มัดจำเพิ่ม · ตรวจก่อนยกเลิก";
                    }
                }

                // VOID ค้าง: มีคำสั่ง VOID เอกสารนี้ที่ล้ม/ถูกยกเลิก/ค้างนาน หรือ "สำเร็จ" ทั้งที่เอกสารยังใช้งาน (บั๊กกลืน error เดิม)
                var voids = queue.Where(q => q.Action == "VOID" && q.DocId == d.Id).ToList();
                var stuck = voids.FirstOrDefault(q => q.Status == "FAILED" || q.Status == "CANCELLED" || q.Status == "COMPLETED"
                    || ((q.Status == "PENDING" || q.Status == "PROCESSING") && q.Created != DateTime.MinValue && (now - q.Created).TotalHours > 24));
                if (stuck != null)
                {
                    d.VoidStuck = true;
                    d.Detail += stuck.Status == "COMPLETED"
                        ? $" · คิว VOID #{stuck.Id} ถูกบันทึกว่าสำเร็จ แต่เอกสารยังไม่ถูกยกเลิกจริง"
                        : $" · คิว VOID #{stuck.Id} ({stuck.Status}) — เอกสารยังไม่ถูกยกเลิก";
                }

                bool etax = IsEtaxReachedRd(d.EtaxStatus);
                d.CanVoid = d.Class != RecMatched && !etax;
                if (etax)
                    d.VoidWarning = "ส่ง e-Tax ถึงกรมสรรพากรแล้ว (" + d.EtaxStatus + ") — NextAcc ห้ามยกเลิก ต้องออกใบลดหนี้ (Account/CreditNotes)";
                else if (d.Class != RecMatched)
                    d.VoidWarning = "ถ้ารายงานภาษีเดือนของเอกสารนี้ยื่น/ปิดงวดแล้ว NextAcc จะปฏิเสธการยกเลิก → ออกใบลดหนี้แทน";
            }

            // ── ธงรายใบเสร็จ ──
            foreach (var rc in r.Receipts)
            {
                var mine = r.Docs.Where(d => string.Equals(d.ReceiptNumber, rc.ReceiptNumber, StringComparison.OrdinalIgnoreCase)
                    && (d.Class == RecMatched || d.Class == RecOrphanSuperseded)).ToList();
                rc.ActiveDocs = mine.Count;
                var notes = new List<string>();
                if (mine.Count >= 2)
                {
                    rc.Flags.Add("DUPLICATE");
                    notes.Add($"มีเอกสารใช้งาน {mine.Count} ใบ ({string.Join(", ", mine.Select(m => m.Number))})");
                }
                if (r.Docs.Any(d => d.VoidStuck && string.Equals(d.ReceiptNumber, rc.ReceiptNumber, StringComparison.OrdinalIgnoreCase)))
                {
                    rc.Flags.Add("VOID_STUCK");
                    notes.Add("คำสั่งยกเลิกเอกสารเดิมไม่สำเร็จ");
                }
                var ptr = rc.Pointer == null ? null : r.Docs.FirstOrDefault(d => d.Id == rc.Pointer.Value);
                if (ptr != null && !IsCancelledLocalStatus(rc.LocalStatus))
                {
                    decimal gross = rc.Amount + rc.DepositApplied;
                    if (Math.Abs(ptr.Total - gross) > 1m && Math.Abs(ptr.Total - rc.Amount) > 1m)
                    {
                        rc.Flags.Add("AMOUNT_MISMATCH");
                        notes.Add($"ยอดเอกสาร {ptr.Total:N2} ≠ ใบเสร็จ {gross:N2}" + (rc.DepositApplied > 0 ? $" (สุทธิ {rc.Amount:N2} + หักมัดจำ {rc.DepositApplied:N2})" : ""));
                    }
                }
                if (!IsCancelledLocalStatus(rc.LocalStatus) && mine.Count == 0 && rc.HasCompletedCreate && !rc.HasPendingCreate)
                {
                    rc.Flags.Add("MISSING_ON_NEXTACC");
                    notes.Add("เคย sync สำเร็จแต่ไม่มีเอกสารใช้งานบน NextAcc" + (rc.PointerState == "VOIDED" ? " (ถูกยกเลิกแล้ว)" : rc.PointerState == "MISSING" ? " (ถูกลบแล้ว)" : ""));
                }
                rc.Detail = string.Join(" · ", notes);
            }

            // ── ยอดมัดจำทั้งการจอง ──
            decimal naDep = 0m;
            foreach (var d in r.Docs)
            {
                ReconReceipt rc = null;
                bool dep = d.IsDeposit || (d.ReceiptNumber != null && receiptByNo.TryGetValue(d.ReceiptNumber, out rc) && rc.IsDeposit);
                if (dep) naDep += d.Total - d.DepositRefunded;
            }
            r.NaDepositTotal = naDep;
            r.LocalDepositTotal = r.Receipts.Where(x => x.IsDeposit && !IsCancelledLocalStatus(x.LocalStatus)
                && !x.LocalStatus.Equals("Refunded", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
            r.BookingFlags.Clear();
            if (Math.Abs(r.NaDepositTotal - r.LocalDepositTotal) > 1m)
                r.BookingFlags.Add($"DEPOSIT_MISMATCH: มัดจำบน NextAcc {r.NaDepositTotal:N2} ≠ ใบมัดจำในระบบ {r.LocalDepositTotal:N2}");

            r.IssueCount = r.Docs.Count(d => d.Class != RecMatched)
                + r.Receipts.Sum(x => x.Flags.Count)
                + r.BookingFlags.Count;
        }

        /// <summary>
        /// เก็บผลตรวจเทียบลง Accounting_Reconcile_Finding (upsert ตาม Finding_Key; ที่ไม่พบแล้ว = ปิดเอง).
        /// ไม่มีตาราง (ยังไม่รัน PHASE19_35) = ข้ามเงียบ
        /// </summary>
        public void SaveReconcileFindings(ReservationReconciliation r)
        {
            if (r == null || !r.Success || r.ReservationId <= 0) return;
            var items = new List<Tuple<string, string, string, Guid?, string, decimal?, string>>();   // key,type,receipt,doc,docNo,amount,detail
            foreach (var d in r.Docs.Where(x => x.Class != RecMatched || x.VoidStuck))
                items.Add(Tuple.Create("DOC:" + d.Id, d.Class == RecMatched ? "VOID_STUCK" : d.Class, d.ReceiptNumber, (Guid?)d.Id, d.Number, (decimal?)d.Total, d.Detail));
            foreach (var rc in r.Receipts)
                foreach (string f in rc.Flags)
                {
                    Guid? pid = rc.Pointer;
                    items.Add(Tuple.Create("RCPT:" + rc.ReceiptNumber + ":" + f, f, rc.ReceiptNumber, pid, rc.PointerNumber, (decimal?)(rc.Amount + rc.DepositApplied), rc.Detail));
                }
            foreach (string bf in r.BookingFlags)
            {
                string t = bf.Contains(":") ? bf.Substring(0, bf.IndexOf(':')) : bf;
                items.Add(Tuple.Create("RES:" + t, t, (string)null, (Guid?)null, (string)null, (decimal?)(r.NaDepositTotal - r.LocalDepositTotal), bf));
            }
            try
            {
                foreach (var it in items)
                {
                    string detail = it.Item7 ?? "";
                    if (detail.Length > 1000) detail = detail.Substring(0, 1000);
                    _code.DatabaseInsertSafe(_connectionString,
                        @"IF EXISTS (SELECT 1 FROM Accounting_Reconcile_Finding WHERE Reservation_ID = @r AND Finding_Key = @k AND Resolved_At IS NULL)
                              UPDATE Accounting_Reconcile_Finding
                                 SET Last_Seen = GETDATE(), Finding_Type = @t, Receipt_Number = @rn, Nexaacc_Doc_Id = @g,
                                     Nexaacc_Doc_Number = @n, Amount = @a, Detail = @d
                               WHERE Reservation_ID = @r AND Finding_Key = @k AND Resolved_At IS NULL
                          ELSE
                              INSERT INTO Accounting_Reconcile_Finding
                                  (Reservation_ID, Finding_Key, Finding_Type, Receipt_Number, Nexaacc_Doc_Id, Nexaacc_Doc_Number, Amount, Detail, First_Seen, Last_Seen)
                              VALUES (@r, @k, @t, @rn, @g, @n, @a, @d, GETDATE(), GETDATE())",
                        new Dictionary<string, object>
                        {
                            { "@r", r.ReservationId }, { "@k", it.Item1.Length > 200 ? it.Item1.Substring(0, 200) : it.Item1 },
                            { "@t", it.Item2 }, { "@rn", (object)it.Item3 ?? DBNull.Value },
                            { "@g", it.Item4.HasValue ? (object)it.Item4.Value : DBNull.Value },
                            { "@n", (object)it.Item5 ?? DBNull.Value },
                            { "@a", it.Item6.HasValue ? (object)it.Item6.Value : DBNull.Value },
                            { "@d", detail }
                        });
                }
                var prm = new Dictionary<string, object> { { "@r", r.ReservationId } };
                string notIn = "";
                if (items.Count > 0)
                {
                    var names = new List<string>();
                    for (int i = 0; i < items.Count && i < 200; i++) { prm["@k" + i] = items[i].Item1.Length > 200 ? items[i].Item1.Substring(0, 200) : items[i].Item1; names.Add("@k" + i); }
                    notIn = " AND Finding_Key NOT IN (" + string.Join(",", names) + ")";
                }
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Accounting_Reconcile_Finding SET Resolved_At = GETDATE(), Resolved_Note = N'ไม่พบในการตรวจเทียบล่าสุด'
                      WHERE Reservation_ID = @r AND Resolved_At IS NULL" + notIn, prm);
            }
            catch (Exception ex)
            {
                LogThrottled("reconcile-save", $"SaveReconcileFindings: เก็บผลตรวจเทียบไม่ได้ ({ex.Message}) — ยังไม่ได้รัน PHASE19_35?");
            }
        }

        private static DateTime _lastReconcileRunDate = DateTime.MinValue;
        private static volatile bool _reconcileRunning;
        private static readonly object _reconcileLock = new object();

        /// <summary>
        /// งานรายคืน (เรียกจาก timer Global.asax ทุก 30 วิ — no-op จนถึงเวลา): ตรวจเทียบการจองที่มีใบเสร็จสร้างใน N วันล่าสุด
        /// (Nexaacc_Reconcile_Days, สูงสุด Nexaacc_Reconcile_Max การจอง) แล้วเก็บผลลง Accounting_Reconcile_Finding →
        /// ตัวแจ้งเตือนคิว (NotifyQueueHealthIfNeeded) ส่ง Telegram เมื่อพบใหม่. วันละครั้ง (จำวันล่าสุดใน config
        /// Nexaacc_Reconcile_LastRun กัน app recycle รันซ้ำ) · รันเบื้องหลัง ไม่บล็อก tick · ปิดสวิตช์/ไม่มี company endpoint = no-op
        /// </summary>
        public void ReconcileRecentReservationsIfDue()
        {
            try
            {
                if (!_config.ReconcileNightlyEnabled || !_config.IsConfigured || !_config.Enabled || !_config.CanUseCompanyEndpoints) return;
                var now = DateTime.Now;
                if (now.Hour < _config.ReconcileHour) return;
                if (_lastReconcileRunDate.Date == now.Date || _reconcileRunning) return;
                string last = _config.GetConfigValue("Nexaacc_Reconcile_LastRun", "");
                DateTime lastDt;
                if (DateTime.TryParseExact(last, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out lastDt) && lastDt.Date == now.Date)
                { _lastReconcileRunDate = now.Date; return; }
                lock (_reconcileLock)
                {
                    if (_reconcileRunning) return;
                    _reconcileRunning = true;
                }
                _lastReconcileRunDate = now.Date;
                try { _config.SetConfig("Nexaacc_Reconcile_LastRun", now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)); } catch { }
                string conn = _connectionString;
                Task.Run(async () =>
                {
                    // instance แยก — ตัวที่ timer ถืออยู่อาจยังวิ่งคิวเบื้องหลัง (state ของ api client/เอกสารล่าสุดไม่ปนกัน)
                    try { await new AccountingSyncService(conn).RunNightlyReconcileAsync(); }
                    catch (Exception ex)
                    {
                        _code.Logs(_connectionString, "AccountingSync", $"Reconcile nightly ล้มเหลว: {ex.Message}", "SYSTEM");
                    }
                    finally { _reconcileRunning = false; }
                });
            }
            catch (Exception ex)
            {
                _reconcileRunning = false;
                LogThrottled("reconcile-due", "ReconcileRecentReservationsIfDue: " + ex.Message);
            }
        }

        private async Task RunNightlyReconcileAsync()
        {
            int days = _config.ReconcileDays, max = _config.ReconcileMaxReservations;
            var ids = new List<int>();
            var dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT TOP (@max) Reservation_ID FROM Account_Receipt
                  WHERE Created_Date >= DATEADD(DAY, -@d, GETDATE()) AND ISNULL(Reservation_ID, 0) > 0
                  GROUP BY Reservation_ID ORDER BY MAX(Created_Date) DESC",
                new Dictionary<string, object> { { "@max", max }, { "@d", days } });
            if (dt != null) foreach (DataRow row in dt.Rows) ids.Add(Convert.ToInt32(row[0]));
            // ใบเสร็จที่ถูกลบไปแล้วหายจาก Account_Receipt — เพิ่มการจองที่มีคิวสร้าง/ยกเลิกใบเสร็จในช่วงเดียวกัน
            try
            {
                var dq = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP (@max) Entity_ID FROM Accounting_Sync_Queue
                      WHERE Action_Type = 'CREATE_RECEIPT_DOCUMENT' AND Entity_Type = 'RECEIPT' AND Entity_ID > 0
                        AND Created_Date >= DATEADD(DAY, -@d, GETDATE())
                      GROUP BY Entity_ID ORDER BY MAX(ID) DESC",
                    new Dictionary<string, object> { { "@max", max }, { "@d", days } });
                if (dq != null) foreach (DataRow row in dq.Rows) { int id = Convert.ToInt32(row[0]); if (!ids.Contains(id) && ids.Count < max) ids.Add(id); }
            }
            catch { }
            if (ids.Count == 0) return;

            // ดึงเอกสารฝั่งรับทั้งช่วงครั้งเดียว (ใช้ร่วมทุกการจอง)
            List<NextAccPaymentDoc> range = null;
            try { range = await FetchNextAccReceiptDocumentsAsync(DateTime.Today.AddDays(-days - 7), DateTime.Today.AddDays(1)); }
            catch (Exception fx) { _code.Logs(_connectionString, "AccountingSync", $"Reconcile nightly: ดึงเอกสารช่วงวันที่ไม่ได้ ({fx.Message}) — ใช้ by-booking อย่างเดียว", "SYSTEM"); }
            if (range == null) range = new List<NextAccPaymentDoc>();

            int ok = 0, withIssues = 0, failed = 0, issues = 0;
            foreach (int rid in ids)
            {
                try
                {
                    var r = await GetReservationReconciliationAsync(rid, range);
                    if (r.Success) { ok++; SaveReconcileFindings(r); if (r.IssueCount > 0) { withIssues++; issues += r.IssueCount; } }
                    else failed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    if (failed <= 3)
                        _code.Logs(_connectionString, "AccountingSync", $"Reconcile nightly #{rid}: {ex.Message}", "SYSTEM");
                    if (ex is ServerUnavailableException || ex is AuthenticationFailedException || ex is DnsResolutionException) break;
                }
                await Task.Delay(250);
            }
            _code.Logs(_connectionString, "AccountingSync",
                $"Reconcile nightly: ตรวจ {ok}/{ids.Count} การจอง (ย้อน {days} วัน) · มีจุดไม่ตรง {withIssues} การจอง ({issues} จุด) · ล้มเหลว {failed}", "SYSTEM");
        }

        /// <summary>
        /// ยกเลิก (void) เอกสาร NextAcc ที่ "ไม่ผูกใบเสร็จ" ของการจอง (ปุ่มในหน้าต่าง NextAcc ของการจอง) — ด่านก่อนเข้าคิว:
        /// ต้องไม่ใช่ตัวชี้ปัจจุบันของใบเสร็จที่ยังใช้งาน (ใบนั้นใช้ปุ่มของใบเอง) · เอกสารต้องผูกการจองนี้ (BookingNumber/Reference/คิว) ·
        /// e-Tax ถึงกรมสรรพากรแล้ว = ปฏิเสธ (NextAcc DocumentService.cs:9564-9570) แนะนำใบลดหนี้.
        /// เข้าคิวผ่าน EnqueueVoidReceiptByNexaaccId (เลขใบ = เลขเอกสาร NextAcc → ไม่แตะ marker/มัดจำของใบเสร็จใด) แล้ว
        /// ProcessVoidReceipt ยืนยันสถานะ Voided ก่อนจบ
        /// </summary>
        public long EnqueueVoidOrphanNextAccDoc(int reservationId, string docIdText, string requestedBy, out string message)
        {
            message = null;
            Guid docId;
            if (reservationId <= 0 || !Guid.TryParse((docIdText ?? "").Trim(), out docId) || docId == Guid.Empty)
            { message = "ข้อมูลไม่ครบ (การจอง/รหัสเอกสาร)"; return -1; }
            if (!_config.IsConfigured || !_config.CanUseCompanyEndpoints)
            { message = "ต้องตั้งค่า NextAcc + company endpoints ก่อน"; return -1; }

            // ตัวชี้ปัจจุบันของใบเสร็จที่ยังใช้งาน (การจองใดก็ตาม) → ห้ามยกเลิกจากปุ่มนี้
            try
            {
                var own = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 ID, ISNULL(Status, 'Normal') AS St FROM Account_Receipt WHERE Nexaacc_Doc_Id = @g",
                    new Dictionary<string, object> { { "@g", docId } });
                if (own != null && own.Rows.Count > 0 && !IsCancelledLocalStatus(own.Rows[0]["St"]?.ToString()))
                {
                    message = $"เอกสารนี้เป็นเอกสารปัจจุบันของใบเสร็จ {own.Rows[0]["ID"]} — ใช้ปุ่ม 🔁 Resync / 🚫 ยกเลิก ของใบนั้นแทน";
                    return -1;
                }
            }
            catch { /* ยังไม่รัน PHASE18_32 */ }
            var rcs = _code.DatabaseQuerySafe(_connectionString,
                "SELECT ID, ISNULL(Status, 'Normal') AS St FROM Account_Receipt WHERE Reservation_ID = @rid",
                new Dictionary<string, object> { { "@rid", reservationId } });
            if (rcs != null)
                foreach (DataRow row in rcs.Rows)
                {
                    string rn = row["ID"]?.ToString();
                    if (string.IsNullOrEmpty(rn) || IsCancelledLocalStatus(row["St"]?.ToString())) continue;
                    if (LookupNexaaccDocIdByReceipt(rn) == docId)
                    {
                        message = $"เอกสารนี้เป็นเอกสารปัจจุบันของใบเสร็จ {rn} — ใช้ปุ่ม 🔁 Resync / 🚫 ยกเลิก ของใบนั้นแทน";
                        return -1;
                    }
                }

            DocumentResponse doc;
            try
            {
                doc = Task.Run(() => _apiClient.GetDocumentAsync(docId)).GetAwaiter().GetResult()?.data;
            }
            catch (AccountingApiException gx) when (gx.StatusCode == 404 || gx.StatusCode == 410)
            { message = "ไม่พบเอกสารนี้บน NextAcc แล้ว (ถูกลบไปแล้ว)"; return -1; }
            if (doc == null) { message = "อ่านเอกสารจาก NextAcc ไม่ได้ — ลองใหม่"; return -1; }
            if (doc.Status == NexaaccDocumentStatus.Voided) { message = $"เอกสาร {doc.DocumentNumber} ถูกยกเลิกไปแล้ว"; return -1; }
            string resRef = $"RES-{reservationId}";
            bool belongs = string.Equals((doc.BookingNumber ?? "").Trim(), resRef, StringComparison.OrdinalIgnoreCase)
                || string.Equals((doc.Reference ?? "").Trim(), resRef, StringComparison.OrdinalIgnoreCase)
                || (doc.Reference ?? "").Trim().StartsWith(resRef + "-", StringComparison.OrdinalIgnoreCase)
                || LoadReconcileQueueRows(reservationId, new List<string>()).Any(q => q.DocId == docId);
            if (!belongs)
            {
                message = $"เอกสาร {doc.DocumentNumber} ไม่ได้ผูกการจอง #{reservationId} (เลขจอง '{doc.BookingNumber}', อ้างอิง '{doc.Reference}') — ไม่ยกเลิกจากหน้านี้";
                return -1;
            }
            if (IsEtaxReachedRd(doc.EtaxStatus))
            {
                message = $"เอกสาร {doc.DocumentNumber} ส่ง e-Tax ถึงกรมสรรพากรแล้ว ({doc.EtaxStatus}) — NextAcc ห้ามยกเลิก " +
                          "ให้ออกใบลดหนี้อ้างเอกสารนี้แทน (Account/CreditNotes หรือในหน้าเอกสารของ NextAcc)";
                return -1;
            }

            long q2 = EnqueueVoidReceiptByNexaaccId(docId.ToString(), doc.DocumentNumber,
                $"ยกเลิกเอกสารที่ไม่ผูกใบเสร็จ (ตัวตรวจเทียบ การจอง #{reservationId}) โดย {requestedBy ?? "-"}", reservationId);
            if (q2 <= 0) { message = "เข้าคิวยกเลิกไม่สำเร็จ"; return -1; }
            _code.Logs(_connectionString, "AccountingSync",
                $"EnqueueVoidOrphanNextAccDoc: การจอง #{reservationId} doc={doc.DocumentNumber} {docId} ยอด {doc.TotalAmount:N2} by={requestedBy} → คิว #{q2}", "SYSTEM");
            message = $"เข้าคิวยกเลิกเอกสาร {doc.DocumentNumber} ({doc.TotalAmount:N2}) แล้ว (คิว #{q2}) — ระบบยืนยันสถานะ Voided จาก NextAcc ก่อนจบงาน. " +
                      $"ถ้ารายงานภาษีเดือน {doc.DocumentDate:MM/yyyy} ยื่นแล้ว NextAcc จะปฏิเสธ → คิวจะล้มพร้อมเหตุผล ให้ออกใบลดหนี้แทน. " +
                      "⚠ JV ปรับปรุงมัดจำที่ TakeTime โพสต์ผูกกับเอกสารนี้ (ref …-CSDEPADJ-xxxx) ไม่ถูกกลับอัตโนมัติ — ตรวจใน \"ดู JE\" ของใบเสร็จที่เกี่ยวข้อง";
            return q2;
        }
    }
}
