using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>ร่างใบเสร็จก่อนเขียน DB (หน้าใบเสร็จ / เช็คอิน)</summary>
    public sealed class ReceiptDraft
    {
        public string ReceiptNumber;
        public string OriginalReceiptNumber;
        public string Uid;
        public int ReservationId;
        public bool IsDeposit;
        public bool IsEdit;
        public decimal Total, Vat, ExclVat;
        public bool HeaderVatKnown = true;
        public bool LinesWillBeRescaled;
        public DateTime Date = DateTime.Today;
        public string PaidType;
        public long CustomerId;
        public bool EtaxRequested;
        /// <summary>ข้อมูลผู้ซื้อจากฟอร์ม (ถ้ามี) — ใช้แทนข้อมูลในตาราง Customer</summary>
        public string BuyerTaxId, BuyerAddress;
        public bool BuyerFromForm;
        public List<ReceiptLineFact> Lines = new List<ReceiptLineFact>();
    }

    // ส่วนอ่าน DB ของตัวตรวจใบเสร็จ — ไม่มีตาราง/คอลัมน์ = ข้ามกฎนั้น (ไม่โยน)
    public static partial class ReceiptPreflight
    {
        private static readonly object _schemaLock = new object();
        private static DateTime _schemaAt = DateTime.MinValue;
        private static bool _queueCols, _lineKindCol, _editLog, _periodTable;

        private static void RefreshSchema(string conn)
        {
            lock (_schemaLock)
            {
                if ((DateTime.Now - _schemaAt).TotalMinutes < 5) return;
            }
            bool q = false, lk = false, el = false, pt = false;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    @"SELECT CASE WHEN COL_LENGTH('dbo.Accounting_Sync_Queue','Error_Class') IS NOT NULL
                                   AND COL_LENGTH('dbo.Accounting_Sync_Queue','Wait_Since') IS NOT NULL THEN 1 ELSE 0 END AS Q,
                             CASE WHEN COL_LENGTH('dbo.Account_Receipt_Detail','Line_Kind') IS NOT NULL THEN 1 ELSE 0 END AS LK,
                             CASE WHEN OBJECT_ID('dbo.Account_Receipt_Edit_Log','U') IS NOT NULL THEN 1 ELSE 0 END AS EL,
                             CASE WHEN OBJECT_ID('dbo.Accounting_Nexaacc_Period_Status','U') IS NOT NULL THEN 1 ELSE 0 END AS PT", null);
                if (dt != null && dt.Rows.Count > 0)
                {
                    q = Convert.ToInt32(dt.Rows[0]["Q"]) == 1;
                    lk = Convert.ToInt32(dt.Rows[0]["LK"]) == 1;
                    el = Convert.ToInt32(dt.Rows[0]["EL"]) == 1;
                    pt = Convert.ToInt32(dt.Rows[0]["PT"]) == 1;
                }
            }
            catch { }
            lock (_schemaLock) { _queueCols = q; _lineKindCol = lk; _editLog = el; _periodTable = pt; _schemaAt = DateTime.Now; }
        }

        /// <summary>รัน PHASE19_37 แล้ว (คอลัมน์ชนิดปัญหาบนคิว)</summary>
        public static bool QueueSchemaReady(string conn) { RefreshSchema(conn); return _queueCols; }
        public static bool LineKindReady(string conn) { RefreshSchema(conn); return _lineKindCol; }
        public static bool EditLogReady(string conn) { RefreshSchema(conn); return _editLog; }
        public static bool PeriodCacheReady(string conn) { RefreshSchema(conn); return _periodTable; }

        /// <summary>โหมดที่มีผลจริง: ยังไม่รัน migration 37 = OFF เสมอ (ค่าเริ่มต้นเมื่อไม่มีคีย์ = OFF; migration seed WARN)</summary>
        public static PreflightMode CurrentMode(string conn)
        {
            try
            {
                if (!QueueSchemaReady(conn)) return PreflightMode.Off;
                return ParseMode(new AccountingConfig(conn).GetConfigValue("Nexaacc_Preflight_Mode", "OFF"));
            }
            catch { return PreflightMode.Off; }
        }

        /// <summary>ผู้ยืนยันข้ามได้: Owner เสมอ · Admin ที่มีสิทธิ์บัญชี (SYS_ACCOUNTING หรือ FIN_REPORT)</summary>
        public static bool CanOverrideCurrentUser()
        {
            try
            {
                string role = HttpContext.Current?.Session?["User"]?.ToString();
                if (role == "Owner") return true;
                return role == "Admin" && (Perm.CanAccess(Perm.SysAccounting) || Perm.CanAccess(Perm.FinReport));
            }
            catch { return false; }
        }

        public static string CurrentUserName()
        {
            try
            {
                var s = HttpContext.Current?.Session;
                return s?["UserName"]?.ToString() ?? s?["Username"]?.ToString() ?? s?["User"]?.ToString() ?? "SYSTEM";
            }
            catch { return "SYSTEM"; }
        }

        // ── ลิงก์ "แก้ที่นี่" ────────────────────────────────────────────────────
        public static string ReceiptEditUrl(string conn, string receiptNumber)
        {
            if (string.IsNullOrWhiteSpace(receiptNumber)) return null;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn, "SELECT TOP 1 UID FROM Account_Receipt WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", receiptNumber.Trim() } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value && dt.Rows[0][0].ToString().Length > 0)
                    return "/Account/Receipt?command=edit&uid=" + HttpUtility.UrlEncode(dt.Rows[0][0].ToString());
            }
            catch { }
            return null;
        }

        public static string BuildFixUrl(string conn, string fixKind, string receiptNumber, string mappingKey = null)
        {
            switch ((fixKind ?? "").ToUpperInvariant())
            {
                case "EDIT_RECEIPT":
                case "EDIT_CUSTOMER":
                    return ReceiptEditUrl(conn, receiptNumber);
                case "CREDIT_NOTE":
                    return string.IsNullOrWhiteSpace(receiptNumber) ? "/Account/CreditNotes"
                        : "/Account/CreditNotes?new=1&src=" + HttpUtility.UrlEncode(receiptNumber.Trim());
                case "MAPPING":
                    return "/Admin/Settings/AccountingIntegration?focus=mapping" +
                           (string.IsNullOrEmpty(mappingKey) ? "" : "&key=" + HttpUtility.UrlEncode(mappingKey));
                case "PAIDHOW":
                    return "/Admin/Settings/AccountingIntegration?focus=paidHow";
                default:
                    return null;
            }
        }

        // ── ตรวจใบที่บันทึกแล้ว ──────────────────────────────────────────────────
        public static PreflightResult Validate(string conn, string receiptNumber, PreflightContext ctx)
        {
            var f = LoadFacts(conn, receiptNumber);
            return f == null ? new PreflightResult() : Evaluate(f, ctx);
        }

        /// <summary>อ่านข้อเท็จจริงของใบที่บันทึกแล้ว (null = ไม่พบใบ)</summary>
        public static ReceiptFacts LoadFacts(string conn, string receiptNumber)
        {
            if (string.IsNullOrWhiteSpace(receiptNumber)) return null;
            var c = new code();
            var p = new Dictionary<string, object> { { "@id", receiptNumber.Trim() } };
            DataTable h;
            try { h = c.DatabaseQuerySafe(conn, "SELECT TOP 1 * FROM Account_Receipt WHERE ID = @id", p); }
            catch { return null; }
            if (h == null || h.Rows.Count == 0) return null;
            var row = h.Rows[0];
            var f = new ReceiptFacts
            {
                ReceiptNumber = receiptNumber.Trim(),
                ReservationId = Int(Col(row, "Reservation_ID")),
                IsDeposit = Bool(Col(row, "IsDeposit")),
                Status = Str(Col(row, "Status")),
                Total = Dec(Col(row, "Total_Amount")),
                Vat = Dec(Col(row, "Vat")),
                ExclVat = Dec(Col(row, "Total_Amount_Exclude_Vat")),
                PaidType = Str(Col(row, "Paid_Type")),
                DepositAppliedColumn = Dec(Col(row, "Deposit_Applied_Amount")),
                EtaxRequested = Bool(Col(row, "Etax")),
                Date = Col(row, "Created_Date") is DateTime ? (DateTime)Col(row, "Created_Date") : DateTime.Today
            };
            string uid = Str(Col(row, "UID"));
            if (uid.Length > 0) f.EditUrl = "/Account/Receipt?command=edit&uid=" + HttpUtility.UrlEncode(uid);
            string nd = Str(Col(row, "Nexaacc_Doc_Id"));
            if (nd.Length > 0) { f.HasNextAccDoc = true; f.NextAccDocNumber = Str(Col(row, "Nexaacc_Doc_Number")); }
            long custId = Long(Col(row, "Customer_ID"));

            try
            {
                var d = c.DatabaseQuerySafe(conn, "SELECT * FROM Account_Receipt_Detail WHERE Receipt_ID = @id ORDER BY Number", p);
                if (d != null)
                    foreach (DataRow lr in d.Rows) f.Lines.Add(LineFromRow(lr));
            }
            catch { }

            FillCommon(conn, f, custId, null, null, false);
            return f;
        }

        /// <summary>ตรวจร่าง (ก่อนเขียน DB) — ใช้ข้อมูลหัว/บรรทัดจากฟอร์ม + ข้อมูลประกอบจาก DB</summary>
        public static PreflightResult ValidateDraft(string conn, ReceiptDraft d, PreflightContext ctx)
        {
            if (d == null) return new PreflightResult();
            var f = new ReceiptFacts
            {
                ReceiptNumber = d.ReceiptNumber,
                OriginalReceiptNumber = d.OriginalReceiptNumber,
                ReservationId = d.ReservationId,
                IsDeposit = d.IsDeposit,
                Status = "Normal",
                Total = d.Total,
                Vat = d.Vat,
                ExclVat = d.ExclVat,
                HeaderVatKnown = d.HeaderVatKnown,
                LinesWillBeRescaled = d.LinesWillBeRescaled,
                Date = d.Date,
                PaidType = d.PaidType,
                EtaxRequested = d.EtaxRequested,
                Lines = d.Lines ?? new List<ReceiptLineFact>()
            };
            if (!string.IsNullOrEmpty(d.Uid)) f.EditUrl = "/Account/Receipt?command=edit&uid=" + HttpUtility.UrlEncode(d.Uid);
            string docOf = !string.IsNullOrEmpty(d.OriginalReceiptNumber) ? d.OriginalReceiptNumber : d.ReceiptNumber;
            if (d.IsEdit && !string.IsNullOrEmpty(docOf))
            {
                try
                {
                    var h = new code().DatabaseQuerySafe(conn, "SELECT TOP 1 * FROM Account_Receipt WHERE ID = @id",
                        new Dictionary<string, object> { { "@id", docOf } });
                    if (h != null && h.Rows.Count > 0)
                    {
                        string nd = Str(Col(h.Rows[0], "Nexaacc_Doc_Id"));
                        if (nd.Length > 0) { f.HasNextAccDoc = true; f.NextAccDocNumber = Str(Col(h.Rows[0], "Nexaacc_Doc_Number")); }
                    }
                }
                catch { }
            }
            FillCommon(conn, f, d.CustomerId, d.BuyerFromForm ? d.BuyerTaxId : null, d.BuyerFromForm ? d.BuyerAddress : null, d.BuyerFromForm);
            if (d.CustomerId <= 0 && !d.BuyerFromForm) f.BuyerResolves = null;   // ร่าง: ผู้ซื้อถูกผูกตอนบันทึก — ยังตัดสินไม่ได้
            if (d.IsEdit && !string.IsNullOrEmpty(docOf))
                try { f.CreditNoteBlock = CreditNoteGuards.VoidBlockReason(conn, docOf); } catch { }
            return Evaluate(f, ctx);
        }

        private static void FillCommon(string conn, ReceiptFacts f, long customerId, string formTax, string formAddr, bool buyerFromForm)
        {
            var c = new code();
            string self = f.ReceiptNumber ?? "";
            string orig = f.OriginalReceiptNumber ?? self;

            // VAT ของกิจการ
            try
            {
                var bi = c.DatabaseQuerySafe(conn, "SELECT TOP 1 Use_Vat FROM Business_Info", null);
                f.UseVat = bi != null && bi.Rows.Count > 0 && Bool(bi.Rows[0][0]);
            }
            catch { }

            // มัดจำของการจอง
            if (f.ReservationId > 0)
            {
                var rp = new Dictionary<string, object> { { "@r", f.ReservationId }, { "@self", self }, { "@orig", orig } };
                try
                {
                    var dr = c.DatabaseQuerySafe(conn,
                        @"SELECT ID, ISNULL(Total_Amount, 0) AS T FROM Account_Receipt
                          WHERE Reservation_ID = @r AND IsDeposit = 1 AND (Status = 'Normal' OR Status IS NULL)
                            AND ID <> @self AND ID <> @orig ORDER BY Created_Date, ID", rp);
                    if (dr != null)
                        foreach (DataRow x in dr.Rows)
                            f.DepositReceipts.Add(new DepositReceiptFact { ReceiptNumber = Str(x["ID"]), Total = Dec(x["T"]) });
                }
                catch { }
                if (!f.IsDeposit)
                    try
                    {
                        var ap = c.DatabaseQuerySafe(conn,
                            @"SELECT ISNULL(SUM(ISNULL(Deposit_Applied_Amount, 0)), 0) FROM Account_Receipt
                              WHERE Reservation_ID = @r AND ISNULL(IsDeposit, 0) = 0 AND (Status = 'Normal' OR Status IS NULL)
                                AND ID <> @self AND ID <> @orig", rp);
                        if (ap != null && ap.Rows.Count > 0) f.DepositAppliedByOtherReceipts = Dec(ap.Rows[0][0]);
                    }
                    catch { }
                if (f.IsDeposit)
                    try { var rb = ReservationBalance.Load(conn, f.ReservationId); if (rb != null) f.ReservationTotal = rb.Total; } catch { }
            }

            // แหล่งเงิน
            if (!string.IsNullOrWhiteSpace(f.PaidType))
            {
                try
                {
                    var ph = c.DatabaseQuerySafe(conn,
                        "SELECT TOP 1 CAST(Nexaacc_AccountId AS NVARCHAR(50)) AS Acc FROM Account_Paid_How WHERE Paid_How = @n AND Status = 'True'",
                        new Dictionary<string, object> { { "@n", f.PaidType.Trim() } });
                    f.PaidHowRowFound = ph != null && ph.Rows.Count > 0;
                    f.PaidHowAccountId = f.PaidHowRowFound ? Str(ph.Rows[0]["Acc"]) : null;
                    Guid g;
                    if (!string.IsNullOrEmpty(f.PaidHowAccountId) && Guid.TryParse(f.PaidHowAccountId, out g))
                    {
                        var na = c.DatabaseQuerySafe(conn,
                            @"SELECT (SELECT COUNT(*) FROM Accounting_Nexaacc_Accounts) AS N,
                                     (SELECT COUNT(*) FROM Accounting_Nexaacc_Accounts WHERE Nexaacc_AccountId = @g AND ISNULL(Is_Active, 1) = 1) AS Hit",
                            new Dictionary<string, object> { { "@g", g } });
                        if (na != null && na.Rows.Count > 0 && Convert.ToInt32(na.Rows[0]["N"]) > 0)
                            f.PaidHowAccountOnNextAcc = Convert.ToInt32(na.Rows[0]["Hit"]) > 0;
                    }
                }
                catch { f.PaidHowRowFound = true; }   // อ่านไม่ได้ = ไม่ฟ้อง
            }
            f.PaidHowUrl = "/Admin/Settings/AccountingIntegration?focus=paidHow";
            f.MappingUrl = "/Admin/Settings/AccountingIntegration?focus=mapping";
            f.CreditNoteUrl = BuildFixUrl(conn, "CREDIT_NOTE", f.OriginalReceiptNumber ?? f.ReceiptNumber);

            // mapping ที่บรรทัดต้องใช้ (เฉพาะเมื่อเชื่อม NextAcc แล้ว)
            try
            {
                var cfg = new AccountingConfig(conn);
                if (cfg.IsConfigured)
                {
                    var mapper = new AccountingDataMapper(conn);
                    var need = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!f.IsDeposit)
                        foreach (var l in f.Lines.Where(x => x.Amount > 0))
                            need.Add(mapper.GetProductTypeRevenueCode(l.ProductTypeId));
                    foreach (string k in need)
                    {
                        Guid g;
                        if (!mapper.TryGetAccountId(k, out g) || g == Guid.Empty) f.MissingRequiredMappings.Add(k);
                    }
                    bool usesDeposit = f.IsDeposit || f.Lines.Any(l => ReceiptLineClassifier.Classify(l.Amount, l.LineKind) == ReceiptLineRole.DepositApply);
                    Guid g2;
                    if (usesDeposit && (!mapper.TryGetAccountId("ADVANCE_DEPOSIT", out g2) || g2 == Guid.Empty))
                        f.MissingOptionalMappings.Add("ADVANCE_DEPOSIT");
                    int fd;
                    f.VatFilingDay = int.TryParse(cfg.GetConfigValue("Nexaacc_Vat_Filing_Day", "15"), out fd) && fd >= 1 && fd <= 28 ? fd : 15;
                }
            }
            catch { }

            // เอกสาร NextAcc จากคิว (เผื่อยังไม่มีคอลัมน์ Nexaacc_Doc_Id)
            if (!f.HasNextAccDoc && !string.IsNullOrEmpty(orig))
            {
                try
                {
                    string esc = orig.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
                    var q = c.DatabaseQuerySafe(conn,
                        @"SELECT TOP 1 c.ID, c.Nexaacc_Document_Number FROM Accounting_Sync_Queue c
                          WHERE c.Action_Type = 'CREATE_RECEIPT_DOCUMENT' AND c.Status = 'COMPLETED'
                            AND ISNULL(c.Nexaacc_Response_Id, '') NOT LIKE 'SKIPPED%' AND c.Payload LIKE @p
                            AND NOT EXISTS (SELECT 1 FROM Accounting_Sync_Queue v WHERE v.Action_Type = 'VOID_RECEIPT'
                                            AND v.Status = 'COMPLETED' AND v.ID > c.ID AND v.Payload LIKE @p)
                          ORDER BY c.ID DESC",
                        new Dictionary<string, object> { { "@p", "%\"receiptNumber\":\"" + esc + "\"%" } });
                    if (q != null && q.Rows.Count > 0)
                    {
                        f.HasNextAccDoc = true;
                        if (string.IsNullOrEmpty(f.NextAccDocNumber)) f.NextAccDocNumber = Str(q.Rows[0]["Nexaacc_Document_Number"]);
                    }
                }
                catch { }
            }

            // ผู้ซื้อ
            if (buyerFromForm)
            {
                f.BuyerResolves = true;
                f.BuyerTaxId = formTax;
                f.BuyerAddress = formAddr;
            }
            else if (customerId > 0)
            {
                try
                {
                    var cu = c.DatabaseQuerySafe(conn, "SELECT TOP 1 * FROM Customer WHERE ID = @id",
                        new Dictionary<string, object> { { "@id", customerId } });
                    f.BuyerResolves = cu != null && cu.Rows.Count > 0;
                    if (f.BuyerResolves == true)
                    {
                        string tax = Str(Col(cu.Rows[0], "IDNumber"));
                        if (tax.Length == 0) tax = Str(Col(cu.Rows[0], "TaxID"));
                        f.BuyerTaxId = tax;
                        string addr = Str(Col(cu.Rows[0], "Address"));
                        if (addr.Length == 0) addr = Str(Col(cu.Rows[0], "Address1"));
                        f.BuyerAddress = addr;
                    }
                }
                catch { }
            }
            else if (f.ReservationId > 0)
                f.BuyerResolves = false;

            // งวดบัญชี/เดือนภาษีจาก cache (Accounting_Nexaacc_Period_Status — refresh รายชั่วโมงโดย worker)
            if (PeriodCacheReady(conn))
            {
                try
                {
                    var ps = c.DatabaseQuerySafe(conn,
                        @"SELECT TOP 1 Period_Status, Vat_Filed, Refreshed_At FROM Accounting_Nexaacc_Period_Status
                          WHERE [Year] = @y AND [Month] = @m",
                        new Dictionary<string, object> { { "@y", f.Date.Year }, { "@m", f.Date.Month } });
                    if (ps != null && ps.Rows.Count > 0 && ps.Rows[0]["Refreshed_At"] is DateTime
                        && (DateTime.Now - (DateTime)ps.Rows[0]["Refreshed_At"]).TotalHours <= 24)
                    {
                        f.PeriodCacheFresh = true;
                        f.PeriodStatus = Str(ps.Rows[0]["Period_Status"]);
                        f.VatFiled = ps.Rows[0]["Vat_Filed"] == DBNull.Value ? (bool?)null : Bool(ps.Rows[0]["Vat_Filed"]);
                    }
                }
                catch { }
            }
        }

        private static ReceiptLineFact LineFromRow(DataRow lr)
        {
            decimal qty = Col(lr, "Product_Amount") == null ? 1m : Dec(Col(lr, "Product_Amount"));
            decimal unit = Dec(Col(lr, "Price_PerPeice"));
            object amtObj = Col(lr, "Price_Amount");
            decimal amt = amtObj == null ? qty * unit : Dec(amtObj);
            object pt = Col(lr, "ProductType_ID");
            int ptid;
            return new ReceiptLineFact
            {
                Number = Int(Col(lr, "Number")),
                ProductTypeId = pt != null && int.TryParse(Convert.ToString(pt, CultureInfo.InvariantCulture), out ptid) ? (int?)ptid : null,
                Description = Str(Col(lr, "Product_Data")),
                Quantity = qty <= 0 ? 1m : qty,
                UnitPrice = unit,
                Amount = amt,
                LineKind = Str(Col(lr, "Line_Kind"))
            };
        }

        /// <summary>แปลงตารางบรรทัดของหน้าเว็บ (dtReserve / dtDetail) เป็นบรรทัดของร่าง — kindOf คืนชนิดบรรทัด (null = ไม่ระบุ)</summary>
        public static List<ReceiptLineFact> LinesFromTable(DataTable dt, Func<DataRow, string> kindOf)
        {
            var list = new List<ReceiptLineFact>();
            if (dt == null) return list;
            foreach (DataRow r in dt.Rows)
            {
                if (r.RowState == DataRowState.Deleted) continue;
                var l = LineFromRow(r);
                string k = kindOf != null ? kindOf(r) : null;
                if (!string.IsNullOrEmpty(k)) l.LineKind = k;
                list.Add(l);
            }
            return list;
        }

        // ── Line_Kind: เขียน/อ่าน ────────────────────────────────────────────────
        /// <summary>เขียนชนิดบรรทัดติดลบ (เฉพาะบรรทัด Price_Amount &lt; 0 ของ Number นั้น) — ไม่มีคอลัมน์ = ข้าม</summary>
        public static void SaveLineKind(string conn, string receiptId, string number, string kind)
        {
            string k = ReceiptLineKinds.Normalize(kind);
            if (k == null || string.IsNullOrWhiteSpace(receiptId) || !LineKindReady(conn)) return;
            try
            {
                new code().DatabaseInsertSafe(conn,
                    @"UPDATE Account_Receipt_Detail SET Line_Kind = @k
                      WHERE Receipt_ID = @r AND CAST(Number AS NVARCHAR(20)) = @n AND Price_Amount < 0",
                    new Dictionary<string, object> { { "@k", k }, { "@r", receiptId.Trim() }, { "@n", (number ?? "").Trim() } });
            }
            catch { }
        }

        /// <summary>เติมคอลัมน์ Line_Kind ลงตารางบรรทัดของหน้าแก้ใบ (ไม่มีคอลัมน์ใน DB = เพิ่มคอลัมน์ว่าง)</summary>
        public static void LoadLineKindsInto(string conn, string receiptId, DataTable dt)
        {
            if (dt == null) return;
            if (!dt.Columns.Contains("Line_Kind")) dt.Columns.Add("Line_Kind", typeof(string));
            if (string.IsNullOrWhiteSpace(receiptId) || !LineKindReady(conn)) return;
            try
            {
                var k = new code().DatabaseQuerySafe(conn,
                    "SELECT CAST(Number AS NVARCHAR(20)) AS N, Line_Kind FROM Account_Receipt_Detail WHERE Receipt_ID = @r AND Line_Kind IS NOT NULL",
                    new Dictionary<string, object> { { "@r", receiptId.Trim() } });
                if (k == null || k.Rows.Count == 0) return;
                var map = new Dictionary<string, string>();
                foreach (DataRow r in k.Rows) map[Str(r["N"])] = Str(r["Line_Kind"]);
                foreach (DataRow r in dt.Rows)
                {
                    string kind;
                    if (map.TryGetValue(Str(r["Number"]), out kind)) r["Line_Kind"] = kind;
                }
            }
            catch { }
        }

        // ── ประวัติการแก้ใบ / ยืนยันข้าม (Account_Receipt_Edit_Log) ────────────────────
        /// <summary>ภาพใบปัจจุบัน (หัว + บรรทัด) เป็น JSON สำหรับบันทึกก่อน/หลังแก้</summary>
        public static string SnapshotJson(string conn, string receiptNumber)
        {
            var f = LoadFacts(conn, receiptNumber);
            if (f == null) return null;
            return SnapshotJson(f);
        }

        public static string SnapshotJson(ReceiptFacts f)
        {
            if (f == null) return null;
            try
            {
                var o = new Dictionary<string, object>
                {
                    { "receipt", f.ReceiptNumber }, { "reservationId", f.ReservationId }, { "isDeposit", f.IsDeposit },
                    { "total", f.Total }, { "vat", f.Vat }, { "date", f.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                    { "paidType", f.PaidType }, { "depositAppliedColumn", f.DepositAppliedColumn },
                    { "lines", f.Lines.Select(l => new Dictionary<string, object>
                        { { "n", l.Number }, { "type", l.ProductTypeId }, { "desc", l.Description }, { "qty", l.Quantity },
                          { "amount", l.Amount }, { "kind", l.LineKind } }).ToList() }
                };
                return new JavaScriptSerializer().Serialize(o);
            }
            catch { return null; }
        }

        /// <summary>บันทึกประวัติการแก้ใบ/การยืนยันข้าม — action: EDIT | OVERRIDE | BLOCKED (ไม่มีตาราง = log ธรรมดา)</summary>
        public static void WriteEditLog(string conn, string receiptId, string oldReceiptId, int reservationId, string action,
            string reason, string beforeJson, string afterJson, PreflightResult pre, string syncAction, long queueId = 0)
        {
            string who = CurrentUserName();
            string preText = pre == null ? null : pre.ToText(20, PreflightSeverity.Info);
            if (!EditLogReady(conn))
            {
                try
                {
                    new code().Logs(conn, "ReceiptPreflight",
                        $"{action} ใบ {receiptId}{(oldReceiptId != null && oldReceiptId != receiptId ? " (เดิม " + oldReceiptId + ")" : "")} โดย {who} · เหตุผล: {reason ?? "-"} · {preText}", who);
                }
                catch { }
                return;
            }
            try
            {
                new code().DatabaseInsertSafe(conn,
                    @"INSERT INTO dbo.Account_Receipt_Edit_Log
                        (Receipt_ID, Old_Receipt_ID, Reservation_ID, Action, Edited_At, Edited_By, Reason,
                         Before_Json, After_Json, Preflight_Text, Preflight_Rules, Sync_Action, Queue_Id)
                      VALUES (@r, @o, @res, @a, GETDATE(), @by, @reason, @b, @af, @pt, @pr, @sa, @q)",
                    new Dictionary<string, object>
                    {
                        { "@r", Trunc(receiptId, 50) }, { "@o", (object)Trunc(oldReceiptId, 50) ?? DBNull.Value },
                        { "@res", reservationId }, { "@a", Trunc(action, 20) }, { "@by", Trunc(who, 100) },
                        { "@reason", (object)Trunc(reason, 500) ?? DBNull.Value },
                        { "@b", (object)beforeJson ?? DBNull.Value }, { "@af", (object)afterJson ?? DBNull.Value },
                        { "@pt", (object)Trunc(preText, 4000) ?? DBNull.Value },
                        { "@pr", (object)Trunc(pre == null ? null : pre.RulesCsv(), 200) ?? DBNull.Value },
                        { "@sa", (object)Trunc(syncAction, 30) ?? DBNull.Value },
                        { "@q", queueId > 0 ? (object)queueId : DBNull.Value }
                    });
            }
            catch { }
        }

        // ── ตัวช่วยอ่านค่า ──────────────────────────────────────────────────────
        private static string Trunc(string s, int n) { return s == null ? null : (s.Length > n ? s.Substring(0, n) : s); }
        internal static object Col(DataRow r, string name)
        {
            if (r == null || !r.Table.Columns.Contains(name)) return null;
            object v = r[name];
            return v == DBNull.Value ? null : v;
        }
        internal static string Str(object o) { return o == null || o == DBNull.Value ? "" : Convert.ToString(o, CultureInfo.InvariantCulture).Trim(); }
        internal static decimal Dec(object o)
        {
            if (o == null || o == DBNull.Value) return 0m;
            decimal d;
            return decimal.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out d) ? d : 0m;
        }
        internal static int Int(object o)
        {
            int i; decimal d;
            string s = Str(o);
            if (int.TryParse(s, out i)) return i;
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out d) ? (int)d : 0;
        }
        internal static long Long(object o) { long l; return long.TryParse(Str(o), out l) ? l : 0; }
        internal static bool Bool(object o)
        {
            if (o is bool) return (bool)o;
            string s = Str(o).ToLowerInvariant();
            return s == "true" || s == "1";
        }
    }

    /// <summary>ตัวช่วยฝั่งหน้าเว็บ: ตรวจร่าง → ตัดสิน → แสดงข้อความ/ช่องยืนยันข้าม → บันทึกการยืนยันข้าม</summary>
    public static class ReceiptPreflightPage
    {
        /// <summary>ชื่อช่องซ่อนที่สคริปต์ยืนยันข้ามแนบมากับการกดบันทึกซ้ำ (หน้าที่ไม่มีช่องเหตุผลในฟอร์ม เช่น เช็คอิน)</summary>
        public const string OverrideField = "pfOverrideReason";

        /// <summary>
        /// ตรวจร่างแล้วตัดสิน. คืน false = ต้องหยุดบันทึก (ลงสคริปต์ alert/prompt ให้แล้ว). warn = ข้อความเตือน (บันทึกต่อได้)
        /// resubmitClientId: ปุ่มที่ต้องกดซ้ำหลังผู้มีสิทธิ์ใส่เหตุผลใน prompt (null = ใช้ช่องเหตุผลในฟอร์มแทน)
        /// </summary>
        public static bool Gate(System.Web.UI.Page page, string conn, ReceiptDraft draft, PreflightContext ctx,
            string overrideReason, string resubmitClientId, out string warn, out PreflightResult result)
        {
            warn = null;
            result = null;
            PreflightMode mode;
            try { mode = ReceiptPreflight.CurrentMode(conn); } catch { mode = PreflightMode.Off; }
            if (mode == PreflightMode.Off) return true;
            try { result = ReceiptPreflight.ValidateDraft(conn, draft, ctx); }
            catch (Exception ex)
            {
                try { new code().Logs(conn, "ReceiptPreflight", "ตรวจร่างใบไม่สำเร็จ (ไม่บล็อก): " + ex.Message, "SYSTEM"); } catch { }
                return true;
            }
            bool can = ReceiptPreflight.CanOverrideCurrentUser();
            var d = ReceiptPreflight.DecidePageSave(result, mode, can, overrideReason);
            string label = draft == null ? "" : (draft.ReceiptNumber ?? ("การจอง #" + draft.ReservationId));
            if (d.Proceed)
            {
                if (d.Overridden)
                    ReceiptPreflight.WriteEditLog(conn, label, draft?.OriginalReceiptNumber, draft?.ReservationId ?? 0, "OVERRIDE",
                        overrideReason, null, null, result, ctx.ToString());
                if (!string.IsNullOrEmpty(d.Message))
                {
                    warn = d.Message;
                    try { new code().Logs(conn, "ReceiptPreflight", $"{ctx} {label}: เตือน (บันทึกต่อ{(d.Overridden ? " — ยืนยันข้ามโดย " + ReceiptPreflight.CurrentUserName() : "")})\n{d.Message}", ReceiptPreflight.CurrentUserName()); } catch { }
                }
                return true;
            }

            ReceiptPreflight.WriteEditLog(conn, label, draft?.OriginalReceiptNumber, draft?.ReservationId ?? 0, "BLOCKED",
                overrideReason, null, null, result, ctx.ToString());
            if (page != null)
            {
                string js;
                if (d.NeedsOverride && can && !string.IsNullOrEmpty(resubmitClientId))
                    js = "(function(){var r=prompt('" + HttpUtility.JavaScriptStringEncode(d.Message) + "\\n\\nเหตุผลที่ยืนยันข้าม:','');"
                       + "if(r&&r.replace(/\\s/g,'').length>=10){var f=document.forms[0];var i=document.createElement('input');"
                       + "i.type='hidden';i.name='" + OverrideField + "';i.value=r;f.appendChild(i);"
                       + "var b=document.getElementById('" + HttpUtility.JavaScriptStringEncode(resubmitClientId) + "');"
                       + "if(b){b.disabled=false;b.click();}}else if(r!==null){alert('ต้องใส่เหตุผลอย่างน้อย 10 ตัวอักษร');}})();";
                else
                    js = "alert('" + HttpUtility.JavaScriptStringEncode(d.Message) + "');";
                page.ClientScript.RegisterStartupScript(page.GetType(), "pfGate", js, true);
            }
            return false;
        }
    }
}
