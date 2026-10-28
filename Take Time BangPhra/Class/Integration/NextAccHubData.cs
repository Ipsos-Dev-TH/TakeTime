using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace Take_Time_BangPhra.Integration
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //  ข้อมูลหน้า "ศูนย์บัญชี NextAcc" (Account/NextAccHub — CLAUDE.md ข้อ 24)
    //
    //  อ่าน DB อย่างเดียว (ไม่เรียก API NextAcc) — แต่ละส่วน try/catch แยก, ไม่มีตาราง/คอลัมน์ = ส่วนนั้นว่าง (Available=false)
    //  ปุ่มทำงาน (ประมวลผลคิว/ตรวจสุขภาพ/สแกน) เรียก action เดิมของหน้า AccountingIntegration — ไม่มีตรรกะซ้ำที่นี่
    // ══════════════════════════════════════════════════════════════════════════════════════════
    public sealed class NextAccHubData
    {
        public sealed class WorkItem
        {
            public long QueueId;
            public string ActionTh, EntityTh, Key, ErrorClass, Error, FixUrl;
            public DateTime? Created;
        }

        public sealed class Finding
        {
            public int ReservationId;
            public string Type, TypeTh, Receipt, DocNumber, Detail;
            public decimal? Amount;
            public DateTime FirstSeen;
            public bool IsLocal;
        }

        // 1) การเชื่อมต่อ
        public bool Configured, Enabled, HasKey, IsIntegrationKey, HasCompanyKey, CompanyEndpoints;
        public bool ServerDown;
        public DateTime? ServerDownUntil;
        public string ServerDownError;
        public DateTime? LastSuccess;
        public int ChartAccounts;
        public DateTime? ChartSyncedAt;
        public string PreflightMode = "OFF";

        // 2) คิว
        public bool QueueAvailable;
        public bool QueueClassReady;
        /// <summary>FAILED ตามชนิดปัญหา ("" = ยังไม่จำแนก)</summary>
        public readonly Dictionary<string, int> FailedByClass = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public int Pending, Processing, Waiting, AwaitingSlip, StalePending;
        public int TodayTotal, TodayCompleted, TodayFailed;

        // 3) งานที่ต้องมีคนทำ
        public readonly List<WorkItem> Work = new List<WorkItem>();

        // 4) ก่อนยื่น ภ.พ.30
        public int FilingDay = 23, DaysToFiling;
        public DateTime FilingDate, VatMonth;
        public int PreFilingGap;
        public decimal PreFilingGapAmount;
        public readonly List<Finding> PreFilingItems = new List<Finding>();
        public int VatMonthUnsentQueue;

        // 5) ตรวจเทียบ
        public bool FindingsAvailable;
        public readonly Dictionary<string, int> FindingsByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly List<Finding> Findings = new List<Finding>();

        // 6) งานข้ามโมดูล
        public int? SlipPending, SlipFlagged, VendorCnPending, VendorCnOldestDays, CnDraft, CnSyncError, CnRefundPending, OcrOpen;

        public readonly List<string> Errors = new List<string>();

        private readonly string _conn;
        private readonly code _db = new code();

        private NextAccHubData(string conn) { _conn = conn; }

        public static NextAccHubData Load(string conn, DateTime today)
        {
            var d = new NextAccHubData(conn);
            d.LoadConnection();
            d.LoadQueue(today);
            d.LoadWork();
            d.LoadFiling(today);
            d.LoadFindings();
            d.LoadCrossModule();
            return d;
        }

        private DataTable Q(string sql, Dictionary<string, object> ps = null)
        {
            return _db.DatabaseQuerySafe(_conn, sql, ps);
        }

        private static int I(object o) { return o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o, CultureInfo.InvariantCulture); }

        private void Fail(string part, Exception ex) { Errors.Add(part + ": " + ex.Message); }

        // ── 1) การเชื่อมต่อ ──────────────────────────────────────────────────────────

        private void LoadConnection()
        {
            try
            {
                var cfg = new AccountingConfig(_conn);
                Configured = cfg.IsConfigured;
                Enabled = cfg.Enabled;
                HasKey = !string.IsNullOrEmpty(cfg.ApiKey);
                IsIntegrationKey = cfg.IsIntegrationKey;
                HasCompanyKey = cfg.HasDedicatedCompanyKey;
                CompanyEndpoints = cfg.CanUseCompanyEndpoints;
                int fd;
                if (int.TryParse(cfg.GetConfigValue("Nexaacc_Vat_Filing_Day", "23"), out fd)) FilingDay = Math.Max(1, Math.Min(28, fd));
            }
            catch (Exception ex) { Fail("ตั้งค่า", ex); }
            try
            {
                DateTime until; string err;
                if (AccountingApiClient.IsServerDown(out until, out err)) { ServerDown = true; ServerDownUntil = until; ServerDownError = err; }
            }
            catch { }
            try { PreflightMode = ReceiptPreflight.CurrentMode(_conn).ToString().ToUpperInvariant(); } catch { }
            try
            {
                if (NextAccStatusData.HasColumn(_conn, "Accounting_Sync_Queue", null))
                {
                    var dt = Q("SELECT TOP 1 Processed_Date FROM Accounting_Sync_Queue WHERE Status = 'COMPLETED' ORDER BY ID DESC");
                    if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value) LastSuccess = Convert.ToDateTime(dt.Rows[0][0]);
                }
            }
            catch (Exception ex) { Fail("ส่งสำเร็จล่าสุด", ex); }
            try
            {
                if (NextAccStatusData.HasColumn(_conn, "Accounting_Nexaacc_Accounts", "Last_Synced"))
                {
                    var dt = Q("SELECT COUNT(*) AS N, MAX(Last_Synced) AS LastSync FROM Accounting_Nexaacc_Accounts");
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        ChartAccounts = I(dt.Rows[0]["N"]);
                        if (dt.Rows[0]["LastSync"] != DBNull.Value) ChartSyncedAt = Convert.ToDateTime(dt.Rows[0]["LastSync"]);
                    }
                }
            }
            catch (Exception ex) { Fail("ผังบัญชี", ex); }
        }

        // ── 2) คิว ───────────────────────────────────────────────────────────────────

        private void LoadQueue(DateTime today)
        {
            if (!NextAccStatusData.HasColumn(_conn, "Accounting_Sync_Queue", null)) return;
            QueueAvailable = true;
            QueueClassReady = ReceiptPreflight.QueueSchemaReady(_conn);
            try
            {
                var dt = Q(@"SELECT Status, " + (QueueClassReady ? "ISNULL(Error_Class, '')" : "CAST('' AS NVARCHAR(20))") + @" AS Cls,
                                    COUNT(*) AS N,
                                    SUM(CASE WHEN Status = 'PENDING' AND Created_Date < DATEADD(HOUR, -6, GETDATE()) THEN 1 ELSE 0 END) AS Stale
                               FROM Accounting_Sync_Queue
                              WHERE Status IN ('PENDING', 'PROCESSING', 'FAILED', 'AWAITING_SLIP')
                              GROUP BY Status" + (QueueClassReady ? ", ISNULL(Error_Class, '')" : ""));
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                    {
                        string st = Convert.ToString(r["Status"]).ToUpperInvariant();
                        string cls = Convert.ToString(r["Cls"]).ToUpperInvariant();
                        int n = I(r["N"]);
                        StalePending += I(r["Stale"]);
                        switch (st)
                        {
                            case "FAILED":
                                int c; FailedByClass.TryGetValue(cls, out c); FailedByClass[cls] = c + n; break;
                            case "PROCESSING": Processing += n; break;
                            case "AWAITING_SLIP": AwaitingSlip += n; break;
                            default:
                                if (cls == "WAIT") Waiting += n; else Pending += n;
                                break;
                        }
                    }
            }
            catch (Exception ex) { Fail("คิว", ex); }
            try
            {
                var dt = Q(@"SELECT COUNT(*) AS T,
                                    SUM(CASE WHEN Status = 'COMPLETED' THEN 1 ELSE 0 END) AS C,
                                    SUM(CASE WHEN Status = 'FAILED' THEN 1 ELSE 0 END) AS F
                               FROM Accounting_Sync_Queue WHERE Created_Date >= @d",
                    new Dictionary<string, object> { { "@d", today.Date } });
                if (dt != null && dt.Rows.Count > 0)
                {
                    TodayTotal = I(dt.Rows[0]["T"]); TodayCompleted = I(dt.Rows[0]["C"]); TodayFailed = I(dt.Rows[0]["F"]);
                }
            }
            catch (Exception ex) { Fail("คิววันนี้", ex); }
        }

        public int FailedTotal { get { return FailedByClass.Values.Sum(); } }

        public int FailedOf(string cls)
        {
            int n;
            return FailedByClass.TryGetValue(cls ?? "", out n) ? n : 0;
        }

        // ── 3) งานที่ต้องมีคนทำ ────────────────────────────────────────────────────────

        private static readonly string[] PayloadKeys = { "receiptNumber", "documentNumber", "voucherNumber", "cnNo", "stockRef", "refKey", "reservationRef", "returnRef" };

        private void LoadWork()
        {
            if (!QueueAvailable) return;
            try
            {
                string where = QueueClassReady
                    ? "Status = 'FAILED' AND Error_Class IN ('DATA', 'CONFIG', 'POLICY', 'DUPLICATE')"
                    : "Status = 'FAILED' AND Retry_Count >= Max_Retries";
                var dt = Q(@"SELECT TOP 20 ID, Entity_Type, Action_Type, Created_Date,
                                    LEFT(ISNULL(Error_Message, N''), 400) AS Err, LEFT(ISNULL(Payload, N''), 2000) AS P"
                           + (QueueClassReady ? ", Error_Class, Fix_Url" : ", CAST(NULL AS NVARCHAR(20)) AS Error_Class, CAST(NULL AS NVARCHAR(400)) AS Fix_Url")
                           + " FROM Accounting_Sync_Queue WHERE " + where + " ORDER BY ID DESC");
                if (dt == null) return;
                foreach (DataRow r in dt.Rows)
                {
                    string p = Convert.ToString(r["P"]);
                    string key = null;
                    foreach (var k in PayloadKeys) { key = NextAccUi.PayloadValue(p, k); if (!string.IsNullOrEmpty(key)) break; }
                    Work.Add(new WorkItem
                    {
                        QueueId = Convert.ToInt64(r["ID"]),
                        ActionTh = NextAccUi.ActionTh(Convert.ToString(r["Action_Type"])),
                        EntityTh = NextAccUi.EntityTh(Convert.ToString(r["Entity_Type"])),
                        Key = key,
                        ErrorClass = Convert.ToString(r["Error_Class"]).ToUpperInvariant(),
                        Error = Convert.ToString(r["Err"]),
                        FixUrl = Convert.ToString(r["Fix_Url"]),
                        Created = r["Created_Date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["Created_Date"])
                    });
                }
            }
            catch (Exception ex) { Fail("งานค้าง", ex); }
        }

        // ── 4) ก่อนยื่น ภ.พ.30 ────────────────────────────────────────────────────────

        private void LoadFiling(DateTime today)
        {
            DaysToFiling = NextAccUi.VatFilingCountdown(today, FilingDay, out FilingDate, out VatMonth);
            try
            {
                if (NextAccStatusData.HasColumn(_conn, "Accounting_Reconcile_Finding", null))
                {
                    var dt = Q(@"SELECT TOP 200 Reservation_ID, Receipt_Number, Amount, Detail, First_Seen
                                   FROM Accounting_Reconcile_Finding
                                  WHERE Resolved_At IS NULL AND Finding_Type = 'PRE_FILING_GAP'
                                  ORDER BY First_Seen");
                    if (dt != null)
                        foreach (DataRow r in dt.Rows)
                        {
                            PreFilingGap++;
                            decimal? a = r["Amount"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["Amount"]);
                            PreFilingGapAmount += a ?? 0m;
                            if (PreFilingItems.Count < 10)
                                PreFilingItems.Add(new Finding
                                {
                                    ReservationId = I(r["Reservation_ID"]), Type = "PRE_FILING_GAP", TypeTh = NextAccUi.FindingTypeTh("PRE_FILING_GAP"),
                                    Receipt = Convert.ToString(r["Receipt_Number"]), Amount = a, Detail = Convert.ToString(r["Detail"]),
                                    FirstSeen = Convert.ToDateTime(r["First_Seen"]), IsLocal = true
                                });
                        }
                }
            }
            catch (Exception ex) { Fail("ก่อนยื่น ภ.พ.30", ex); }
            try
            {
                if (QueueAvailable)
                {
                    var dt = Q(@"SELECT COUNT(*) FROM Accounting_Sync_Queue
                                  WHERE Action_Type = 'CREATE_RECEIPT_DOCUMENT' AND Status IN ('PENDING', 'PROCESSING', 'FAILED', 'AWAITING_SLIP')
                                    AND Created_Date >= @a AND Created_Date < @b",
                        new Dictionary<string, object> { { "@a", VatMonth }, { "@b", VatMonth.AddMonths(1) } });
                    if (dt != null && dt.Rows.Count > 0) VatMonthUnsentQueue = I(dt.Rows[0][0]);
                }
            }
            catch (Exception ex) { Fail("คิวเดือนภาษี", ex); }
        }

        // ── 5) ตรวจเทียบ ──────────────────────────────────────────────────────────────

        private void LoadFindings()
        {
            if (!NextAccStatusData.HasColumn(_conn, "Accounting_Reconcile_Finding", null)) return;
            FindingsAvailable = true;
            try
            {
                var dt = Q(@"SELECT Finding_Type, COUNT(*) AS N FROM Accounting_Reconcile_Finding
                              WHERE Resolved_At IS NULL AND Finding_Type <> 'PRE_FILING_GAP' GROUP BY Finding_Type");
                if (dt != null) foreach (DataRow r in dt.Rows) FindingsByType[Convert.ToString(r["Finding_Type"])] = I(r["N"]);
                var top = Q(@"SELECT TOP 10 Reservation_ID, Finding_Key, Finding_Type, Receipt_Number, Nexaacc_Doc_Number, Amount, Detail, First_Seen
                                FROM Accounting_Reconcile_Finding
                               WHERE Resolved_At IS NULL AND Finding_Type <> 'PRE_FILING_GAP'
                               ORDER BY First_Seen DESC");
                if (top != null)
                    foreach (DataRow r in top.Rows)
                        Findings.Add(new Finding
                        {
                            ReservationId = I(r["Reservation_ID"]), Type = Convert.ToString(r["Finding_Type"]),
                            TypeTh = NextAccUi.FindingTypeTh(Convert.ToString(r["Finding_Type"])),
                            Receipt = Convert.ToString(r["Receipt_Number"]), DocNumber = Convert.ToString(r["Nexaacc_Doc_Number"]),
                            Amount = r["Amount"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["Amount"]),
                            Detail = Convert.ToString(r["Detail"]), FirstSeen = Convert.ToDateTime(r["First_Seen"]),
                            IsLocal = Convert.ToString(r["Finding_Key"]).StartsWith("LOC:", StringComparison.OrdinalIgnoreCase)
                        });
            }
            catch (Exception ex) { Fail("ตรวจเทียบ", ex); }
        }

        public int FindingsTotal { get { return FindingsByType.Values.Sum(); } }

        // ── 6) งานข้ามโมดูล ───────────────────────────────────────────────────────────

        private int? Count(string sql, Dictionary<string, object> ps = null)
        {
            try
            {
                var dt = Q(sql, ps);
                return dt != null && dt.Rows.Count > 0 ? I(dt.Rows[0][0]) : (int?)null;
            }
            catch { return null; }
        }

        private void LoadCrossModule()
        {
            // สลิปโอนรอตรวจ (ข้อ 16/18)
            if (NextAccStatusData.HasColumn(_conn, "Payment_Slips", "VerificationStatus"))
            {
                SlipPending = Count("SELECT COUNT(*) FROM Payment_Slips WHERE VerificationStatus = 'PENDING' AND ISNULL(IsActive, 1) = 1 AND ISNULL(Status, 1) = 1");
                if (NextAccStatusData.HasColumn(_conn, "Payment_Slips", "Verify_Status"))
                    SlipFlagged = Count(@"SELECT COUNT(*) FROM Payment_Slips WHERE VerificationStatus = 'PENDING' AND ISNULL(IsActive, 1) = 1 AND ISNULL(Status, 1) = 1
                                            AND Verify_Status IN ('MISMATCH', 'DUPLICATE', 'WRONG_RECEIVER')");
            }
            // คืนสินค้ารอใบลดหนี้ผู้ขาย (ข้อ 3 PRT)
            if (NextAccStatusData.HasColumn(_conn, "Purchase_Return_Cn_Link", null))
            {
                try
                {
                    var dt = Q("SELECT COUNT(*) AS N, MIN(Created_At) AS Oldest FROM Purchase_Return_Cn_Link WHERE Cn_Status = N'PENDING'");
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        VendorCnPending = I(dt.Rows[0]["N"]);
                        if (dt.Rows[0]["Oldest"] != DBNull.Value)
                            VendorCnOldestDays = Math.Max(0, (int)(DateTime.Now - Convert.ToDateTime(dt.Rows[0]["Oldest"])).TotalDays);
                    }
                }
                catch (Exception ex) { Fail("รอใบลดหนี้ผู้ขาย", ex); }
            }
            // ใบลดหนี้ลูกค้า (ข้อ 17)
            if (CreditNoteGuards.SchemaReady(_conn))
            {
                try
                {
                    var dt = Q(@"SELECT SUM(CASE WHEN Status = 'DRAFT' THEN 1 ELSE 0 END) AS D,
                                        SUM(CASE WHEN Status NOT IN ('DRAFT', 'VOIDED', 'SYNCED') AND ISNULL(Sync_Error, N'') <> N'' THEN 1 ELSE 0 END) AS E,
                                        SUM(CASE WHEN Status <> 'VOIDED' AND Refund_Status = 'PENDING' THEN 1 ELSE 0 END) AS R
                                   FROM Credit_Note");
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        CnDraft = I(dt.Rows[0]["D"]); CnSyncError = I(dt.Rows[0]["E"]); CnRefundPending = I(dt.Rows[0]["R"]);
                    }
                }
                catch (Exception ex) { Fail("ใบลดหนี้", ex); }
            }
            // ร่าง OCR ที่ยังไม่จบ (ข้อ 3)
            if (NextAccStatusData.HasColumn(_conn, "Ocr_Document_Log", null))
                OcrOpen = Count(@"SELECT COUNT(*) FROM Ocr_Document_Log
                                   WHERE Status IN (N'DRAFT', N'CREATED', N'UPDATED', N'APPROVED', N'FAILED') AND Created_At < DATEADD(HOUR, -12, GETDATE())");
        }
    }
}
