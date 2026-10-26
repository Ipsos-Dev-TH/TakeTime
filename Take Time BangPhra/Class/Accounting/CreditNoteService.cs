using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// ใบลดหนี้ + การคืนเงินลูกค้า (PHASE19_33) — ฝั่ง TakeTime (ข้อมูล/อนุมัติ/คืนเงิน/คืนสต๊อก/ยกเลิกการจอง)
    /// การส่ง NextAcc อยู่ที่ AccountingSyncService.CreditNote.cs (คิว CREATE_CREDIT_NOTE / RECORD_CN_REFUND / VOID_CREDIT_NOTE)
    ///
    /// 1 แถว Credit_Note = 1 เอกสารต้นทาง (Account_Receipt):
    ///   DEPOSIT     ใบเสร็จมัดจำ → NextAcc refund-deposit (คืน) + realize-deposit ForfeitAs (ส่วนที่ยึดเป็นค่าธรรมเนียม)
    ///   TAX_INVOICE ใบเสร็จ/ใบกำกับที่ไม่ใช่มัดจำ → NextAcc ใบลดหนี้ (DocumentType 6) อ้างใบต้นทาง + JE ขาเงินคืนออกแหล่งเงินที่เลือก
    /// ไม่มีตาราง (ยังไม่รัน migration) หรือ CreditNote_Enabled ≠ '1' → ทุกเมธอดคืนค่า "ปิดใช้" ระบบเดิมทำงานเหมือนเดิม
    /// </summary>
    public class CreditNoteService
    {
        // ── ค่าคงที่ ────────────────────────────────────────────────────────────
        public const string SrcDeposit = "DEPOSIT", SrcTaxInvoice = "TAX_INVOICE";
        public const string StDraft = "DRAFT", StApproved = "APPROVED", StSynced = "SYNCED", StVoided = "VOIDED", StFailed = "FAILED";
        public const string MTransfer = "TRANSFER", MCash = "CASH", MGateway = "GATEWAY", MNone = "NONE";
        public const string RsNone = "NONE", RsPending = "PENDING", RsPaid = "PAID";
        public const string RetPriceOrFee = "PRICE_OR_FEE", RetCompensation = "COMPENSATION";
        public const string PathCompanyCn = "COMPANY_CN", PathDepositRefund = "DEPOSIT_REFUND", PathIntegrationCn = "INTEGRATION_CN",
                            PathJournal = "JOURNAL", PathLocal = "LOCAL";

        public const string CfgEnabled = "CreditNote_Enabled";
        public const string CfgRequireApproval = "CreditNote_Require_Approval";

        public static readonly string[] ReasonCodes =
            { "CANCEL_REFUND", "CANCEL_PARTIAL", "PRICE_ADJUST", "COMPENSATION", "GOODS_RETURN", "WRONG_AMOUNT", "OTHER" };

        public static string ReasonLabel(string code)
        {
            switch ((code ?? "").ToUpperInvariant())
            {
                case "CANCEL_REFUND": return "ยกเลิกการจอง/บริการ — คืนเงินเต็มจำนวน";
                case "CANCEL_PARTIAL": return "ยกเลิก — คืนเงินบางส่วน (หักค่าธรรมเนียม)";
                case "PRICE_ADJUST": return "ลดราคา/ส่วนลดหลังออกใบกำกับ";
                case "COMPENSATION": return "ชดเชยลูกค้า (บริการไม่เป็นไปตามตกลง)";
                case "GOODS_RETURN": return "รับคืนสินค้า";
                case "WRONG_AMOUNT": return "คำนวณราคาผิด/เก็บเงินเกิน";
                default: return "อื่น ๆ";
            }
        }

        /// <summary>CreditNoteReason ของ NextAcc (AllEnums.cs:1636-1646): Return=1, Discount=2, Adjustment=3</summary>
        public static int NextAccReason(string code)
        {
            switch ((code ?? "").ToUpperInvariant())
            {
                case "GOODS_RETURN": return 1;
                case "PRICE_ADJUST":
                case "COMPENSATION": return 2;
                default: return 3;
            }
        }

        public static string MethodLabel(string m)
        {
            switch ((m ?? "").ToUpperInvariant())
            {
                case MTransfer: return "โอนคืน";
                case MCash: return "เงินสด";
                case MGateway: return "คืนผ่านเกตเวย์";
                default: return "ไม่คืนเงิน";
            }
        }

        public static string StatusLabel(string s)
        {
            switch ((s ?? "").ToUpperInvariant())
            {
                case StDraft: return "ร่าง (รออนุมัติ)";
                case StApproved: return "อนุมัติแล้ว (รอส่ง NextAcc)";
                case StSynced: return "ส่ง NextAcc แล้ว";
                case StVoided: return "ยกเลิกแล้ว";
                case StFailed: return "ส่งไม่สำเร็จ";
                default: return s ?? "";
            }
        }

        // ── โมเดล ───────────────────────────────────────────────────────────────
        public sealed class Line
        {
            public int LineNo;
            public int? SourceLineNo, ProductId, ProductTypeId;
            public string Description, Unit, AccountCode;
            public decimal Qty = 1m, UnitPrice, Amount, RestockQty;
            public decimal? UnitCost;
        }

        public sealed class Record
        {
            public int Id, ReservationId;
            public string CnNo, SourceReceiptId, SourceDocType, ReasonCode, ReasonText, CustomerName;
            public DateTime CnDate, CreatedAt;
            public decimal AmountGross, VatAmount, RefundAmount, RetainedAmount;
            public string RetainedAs, RefundMethod, RefundRef, RefundSlipPath, RefundStatus, RefundPaidHowName;
            public int? RefundPaidHowId, GatewayTxnId;
            public bool Restock;
            public string Status, CancelBatch, CreatedBy, ApprovedBy, VoidedBy, VoidReason;
            public DateTime? ApprovedAt, VoidedAt;
            public string NexaaccPath, NexaaccDocNo, NexaaccRefundId, NexaaccCounterCode, NexaaccMarker, SyncError, Notes;
            public Guid? NexaaccDocId;
            public readonly List<Line> Lines = new List<Line>();
        }

        /// <summary>บรรทัดของใบต้นทาง — เป็น property (ไม่ใช่ field) เพราะหน้าเว็บ bind ด้วย Eval()</summary>
        public sealed class SourceLine
        {
            public int No { get; set; }
            public int? ProductTypeId { get; set; }
            public int? ProductId { get; set; }
            public string Description { get; set; }
            public string Unit { get; set; }
            public decimal Qty { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Amount { get; set; }
            public decimal UnitCost { get; set; }
            public bool Restockable { get; set; }
        }

        public sealed class SourceReceipt
        {
            public string Id, PaidType, Status, CustomerName;
            public int ReservationId;
            public int? PaidHowId;
            public DateTime Date;
            public decimal Total, Vat, DepositApplied, Credited, Creditable;
            public bool IsDeposit;
            public readonly List<SourceLine> Lines = new List<SourceLine>();
            public string DocType => IsDeposit ? SrcDeposit : SrcTaxInvoice;
        }

        public sealed class Request
        {
            public int ReservationId;
            public string SourceReceiptId, ReasonCode, ReasonText, CustomerName, Notes, CancelBatch;
            public string RefundMethod = MNone, RefundRef, RefundSlipPath, RetainedAs;
            public int? RefundPaidHowId;
            public bool RefundPending;
            public decimal RetainedAmount;
            public DateTime? CnDate;
            public readonly List<Line> Lines = new List<Line>();
        }

        public sealed class ReservationMoney
        {
            public int ReservationId;
            public string CustomerName, Status;
            public readonly List<SourceReceipt> Receipts = new List<SourceReceipt>();
            public decimal TotalCreditable { get { decimal s = 0; foreach (var r in Receipts) s += r.Creditable; return s; } }
            public bool HasGateway;
        }

        // ── โครงสร้าง ───────────────────────────────────────────────────────────
        private readonly string _conn;
        private readonly code _code = new code();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public CreditNoteService(string conn) { _conn = conn; }

        private static DateTime _readyCheckedAt = DateTime.MinValue;
        private static bool _ready;

        /// <summary>มีตาราง Credit_Note + Credit_Note_Line (PHASE19_33) — cache 60 วินาที</summary>
        public static bool TableReady(string conn)
        {
            if ((DateTime.Now - _readyCheckedAt).TotalSeconds < 60) return _ready;
            bool ok = false;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT CASE WHEN OBJECT_ID('dbo.Credit_Note','U') IS NOT NULL AND OBJECT_ID('dbo.Credit_Note_Line','U') IS NOT NULL THEN 1 ELSE 0 END", null);
                ok = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 1;
            }
            catch { ok = false; }
            _ready = ok;
            _readyCheckedAt = DateTime.Now;
            return ok;
        }

        /// <summary>เปิดใช้ = มีตาราง + CreditNote_Enabled = '1' (ไม่มีค่า = ปิด = พฤติกรรมเดิม)</summary>
        public static bool Enabled(string conn)
        {
            if (!TableReady(conn)) return false;
            try { return new AccountingConfig(conn).GetConfigValue(CfgEnabled, "0") == "1"; }
            catch { return false; }
        }

        public static bool RequireApproval(string conn)
        {
            try { return new AccountingConfig(conn).GetConfigValue(CfgRequireApproval, "1") != "0"; }
            catch { return true; }
        }

        public static string CurrentUser()
        {
            try
            {
                var s = HttpContext.Current?.Session;
                if (s == null) return "SYSTEM";
                string n = s["UserName"]?.ToString();
                if (string.IsNullOrWhiteSpace(n)) n = s["username"]?.ToString();
                return string.IsNullOrWhiteSpace(n) ? "SYSTEM" : n;
            }
            catch { return "SYSTEM"; }
        }

        private static int? CurrentAdminId()
        {
            try
            {
                object v = HttpContext.Current?.Session?["UserID"];
                int id;
                if (v != null && int.TryParse(v.ToString(), out id) && id > 0) return id;
            }
            catch { }
            return null;
        }

        /// <summary>ผู้อนุมัติ/ยกเลิกใบลดหนี้ได้: Owner เสมอ · Admin ที่มีสิทธิ์ตั้งค่าบัญชี (SYS_ACCOUNTING)</summary>
        public static bool CanApproveCurrentUser()
        {
            try
            {
                string role = HttpContext.Current?.Session?["User"]?.ToString();
                if (role == "Owner") return true;
                return role == "Admin" && Perm.CanAccess(Perm.SysAccounting);
            }
            catch { return false; }
        }

        // ── อ่านข้อมูลต้นทาง ───────────────────────────────────────────────────

        private static decimal Dec(object o)
        {
            if (o == null || o == DBNull.Value) return 0m;
            decimal d;
            return decimal.TryParse(Convert.ToString(o, Inv), NumberStyles.Any, Inv, out d) ? d : 0m;
        }
        private static int? IntN(object o)
        {
            if (o == null || o == DBNull.Value) return null;
            int i;
            return int.TryParse(Convert.ToString(o, Inv), out i) ? (int?)i : null;
        }
        private static string Str(object o) => o == null || o == DBNull.Value ? "" : o.ToString();
        private static bool Bool(object o)
        {
            string s = Str(o).Trim();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>เลขที่ไม่ใช่ยอดขายรายใบ (ใบสรุปรายวัน / เอกสาร OTA) — ไม่ให้ออกใบลดหนี้จากหน้านี้</summary>
        public static bool IsExcludedSource(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return true;
            string s = id.Trim();
            if (AccountingSyncService.IsOtaSalesDocRef(s)) return true;
            if (s.StartsWith("OTA-", StringComparison.OrdinalIgnoreCase)) return true;
            return false;   // ใบสรุปขายหน้าร้าน (POSDAY…) = ขายจริงให้ลูกค้า walk-in — ออกใบลดหนี้คืนสินค้าได้
        }

        /// <summary>ยอดที่ออกใบลดหนี้/ยึดไปแล้วของใบต้นทาง (ไม่นับใบที่ยกเลิก)</summary>
        private decimal CreditedSoFar(string receiptId, int excludeCnId = 0)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT ISNULL(SUM(Amount_Gross + Retained_Amount), 0) FROM Credit_Note
                       WHERE Source_Receipt_ID = @r AND Status <> 'VOIDED' AND ID <> @x",
                    new Dictionary<string, object> { { "@r", receiptId }, { "@x", excludeCnId } });
                return dt != null && dt.Rows.Count > 0 ? Dec(dt.Rows[0][0]) : 0m;
            }
            catch { return 0m; }
        }

        private Dictionary<string, int> PaidHowIdsByName()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT ID, Paid_How FROM Account_Paid_How", null);
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                    {
                        string n = Str(r["Paid_How"]).Trim();
                        int? id = IntN(r["ID"]);
                        if (n.Length > 0 && id.HasValue && !map.ContainsKey(n)) map[n] = id.Value;
                    }
            }
            catch { }
            return map;
        }

        /// <summary>อ่านใบต้นทาง 1 ใบ + บรรทัด + ยอดที่ยังลดหนี้ได้ (มัดจำ: หักส่วนที่ถูกนำไปหักในใบอื่นแล้ว)</summary>
        public SourceReceipt LoadSource(string receiptId)
        {
            if (string.IsNullOrWhiteSpace(receiptId)) return null;
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT TOP 1 R.ID, R.Reservation_ID, R.Created_Date, R.Total_Amount, R.Vat, R.IsDeposit, R.Paid_Type,
                         ISNULL(R.Deposit_Applied_Amount, 0) AS DA, ISNULL(R.Status, N'Normal') AS St,
                         ISNULL(C.FullName, C.Name) AS CustName
                    FROM Account_Receipt R
                    LEFT JOIN Reservation Res ON Res.ID = R.Reservation_ID
                    LEFT JOIN Customer C ON C.MobilePhone = Res.Customer_MobilePhone
                   WHERE R.ID = @id",
                new Dictionary<string, object> { { "@id", receiptId.Trim() } });
            if (dt == null || dt.Rows.Count == 0) return null;
            var src = MapSource(dt.Rows[0], PaidHowIdsByName());
            LoadSourceLines(src);
            if (src.IsDeposit)
            {
                // มัดจำที่ถูกนำไปหักในใบเสร็จอื่นของการจองเดียวกันแล้ว (FIFO ตามวันที่) ไม่ใช่ยอดที่คืนได้
                var money = LoadReservationMoney(src.ReservationId);
                var same = money?.Receipts.FirstOrDefault(x => string.Equals(x.Id, src.Id, StringComparison.OrdinalIgnoreCase));
                src.Creditable = same != null ? same.Creditable : Math.Max(0m, src.Total - src.Credited);
            }
            return src;
        }

        private SourceReceipt MapSource(DataRow r, Dictionary<string, int> paidHows)
        {
            var s = new SourceReceipt
            {
                Id = Str(r["ID"]).Trim(),
                ReservationId = IntN(r["Reservation_ID"]) ?? 0,
                Date = r["Created_Date"] == DBNull.Value ? DateTime.Today : AccountingSyncService.NormalizeEra(Convert.ToDateTime(r["Created_Date"])),
                Total = Dec(r["Total_Amount"]),
                Vat = Dec(r["Vat"]),
                IsDeposit = Bool(r["IsDeposit"]),
                PaidType = Str(r["Paid_Type"]).Trim(),
                DepositApplied = Dec(r["DA"]),
                Status = Str(r["St"]).Trim(),
                CustomerName = Str(r["CustName"]).Trim()
            };
            int ph;
            if (paidHows != null && paidHows.TryGetValue(s.PaidType, out ph)) s.PaidHowId = ph;
            s.Credited = CreditedSoFar(s.Id);
            s.Creditable = Math.Max(0m, s.Total - s.Credited);
            return s;
        }

        private void LoadSourceLines(SourceReceipt src)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT D.Number, D.ProductType_ID, D.Product_ID, D.Product_Data, D.Product_Amount, D.Product_Unit,
                             D.Price_PerPeice, D.Price_Amount, P.ID AS PID, ISNULL(P.Cost_Price, 0) AS Cost
                        FROM Account_Receipt_Detail D
                        LEFT JOIN Product P ON P.ID = TRY_CONVERT(INT, D.Product_ID) AND TRY_CONVERT(INT, D.ProductType_ID) = 3
                       WHERE D.Receipt_ID = @id
                       ORDER BY TRY_CONVERT(INT, D.Number), D.Number",
                    new Dictionary<string, object> { { "@id", src.Id } });
                if (dt == null) return;
                int seq = 0;
                foreach (DataRow r in dt.Rows)
                {
                    decimal qty = Dec(r["Product_Amount"]);
                    decimal unit = Dec(r["Price_PerPeice"]);
                    decimal amt = r["Price_Amount"] == DBNull.Value ? qty * unit : Dec(r["Price_Amount"]);
                    if (amt <= 0) continue;   // บรรทัดติดลบ = หักมัดจำ (ไม่ใช่รายได้) — ตรงกับ LookupReceiptLinesEx
                    seq++;
                    src.Lines.Add(new SourceLine
                    {
                        No = IntN(r["Number"]) ?? seq,
                        ProductTypeId = IntN(r["ProductType_ID"]),
                        ProductId = IntN(r["Product_ID"]),
                        Description = Str(r["Product_Data"]),
                        Unit = Str(r["Product_Unit"]),
                        Qty = qty > 0 ? qty : 1m,
                        UnitPrice = unit,
                        Amount = amt,
                        UnitCost = Dec(r["Cost"]),
                        Restockable = r["PID"] != DBNull.Value
                    });
                }
            }
            catch (Exception ex)
            {
                try { _code.Logs(_conn, "CreditNote", "LoadSourceLines " + src.Id + ": " + ex.Message, "SYSTEM"); } catch { }
            }
        }

        /// <summary>เงินที่รับไว้ของการจอง แยกรายใบ + ยอดที่ยังคืน/ลดหนี้ได้ (มัดจำคงเหลือ FIFO หลังหักส่วนที่นำไปหักในใบอื่น)</summary>
        public ReservationMoney LoadReservationMoney(int reservationId)
        {
            if (reservationId <= 0) return null;
            var m = new ReservationMoney { ReservationId = reservationId };
            try
            {
                var rh = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 R.Status, ISNULL(C.FullName, C.Name) AS CustName FROM Reservation R
                       LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone WHERE R.ID = @id",
                    new Dictionary<string, object> { { "@id", reservationId } });
                if (rh != null && rh.Rows.Count > 0)
                {
                    m.Status = Str(rh.Rows[0]["Status"]);
                    m.CustomerName = Str(rh.Rows[0]["CustName"]);
                }
            }
            catch { }

            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT R.ID, R.Reservation_ID, R.Created_Date, R.Total_Amount, R.Vat, R.IsDeposit, R.Paid_Type,
                         ISNULL(R.Deposit_Applied_Amount, 0) AS DA, ISNULL(R.Status, N'Normal') AS St,
                         CAST(NULL AS NVARCHAR(200)) AS CustName
                    FROM Account_Receipt R
                   WHERE R.Reservation_ID = @id AND ISNULL(R.Status, N'Normal') IN (N'Normal', N'')
                   ORDER BY R.Created_Date, R.ID",
                new Dictionary<string, object> { { "@id", reservationId } });
            if (dt == null) return m;
            var paidHows = PaidHowIdsByName();
            var all = new List<SourceReceipt>();
            foreach (DataRow r in dt.Rows)
            {
                var s = MapSource(r, paidHows);
                if (IsExcludedSource(s.Id) || s.Total <= 0) continue;
                s.CustomerName = m.CustomerName;
                all.Add(s);
            }
            decimal appliedLeft = 0m;
            foreach (var s in all) if (!s.IsDeposit) appliedLeft += s.DepositApplied;
            foreach (var s in all)
            {
                if (s.IsDeposit)
                {
                    decimal consumed = Math.Min(s.Total, appliedLeft);
                    appliedLeft -= consumed;
                    s.Creditable = Math.Max(0m, s.Total - consumed - s.Credited);
                }
                LoadSourceLines(s);
                m.Receipts.Add(s);
            }
            m.HasGateway = m.Receipts.Any(x => FindGatewayTxn(x.Id).HasValue);
            return m;
        }

        /// <summary>ค้นใบต้นทางสำหรับออกใบลดหนี้ manual (เลขใบเสร็จ / เลขจอง / ชื่อลูกค้า) — ไม่รวมใบยกเลิก/ใบมัดจำ</summary>
        public List<SourceReceipt> SearchSources(string keyword, DateTime from, DateTime to, int top = 50)
        {
            var list = new List<SourceReceipt>();
            string kw = (keyword ?? "").Trim();
            var ps = new Dictionary<string, object> { { "@f", from.Date }, { "@t", to.Date.AddDays(1) } };
            string where = "R.Created_Date >= @f AND R.Created_Date < @t";
            if (kw.Length > 0)
            {
                where = "(R.ID LIKE @kw OR CAST(R.Reservation_ID AS NVARCHAR(20)) = @kwx OR C.FullName LIKE @kw OR C.Name LIKE @kw OR Res.Customer_MobilePhone LIKE @kw)";
                ps["@kw"] = "%" + kw.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
                ps["@kwx"] = kw;
            }
            var dt = _code.DatabaseQuerySafe(_conn,
                $@"SELECT TOP {Math.Max(1, Math.Min(top, 200))} R.ID, R.Reservation_ID, R.Created_Date, R.Total_Amount, R.Vat, R.IsDeposit, R.Paid_Type,
                         ISNULL(R.Deposit_Applied_Amount, 0) AS DA, ISNULL(R.Status, N'Normal') AS St,
                         ISNULL(C.FullName, C.Name) AS CustName
                    FROM Account_Receipt R
                    LEFT JOIN Reservation Res ON Res.ID = R.Reservation_ID
                    LEFT JOIN Customer C ON C.MobilePhone = Res.Customer_MobilePhone
                   WHERE {where} AND ISNULL(R.Status, N'Normal') NOT IN (N'Cancel', N'Void', N'ยกเลิก')
                   ORDER BY R.Created_Date DESC", ps);
            if (dt == null) return list;
            var paidHows = PaidHowIdsByName();
            foreach (DataRow r in dt.Rows)
            {
                var s = MapSource(r, paidHows);
                if (IsExcludedSource(s.Id)) continue;
                list.Add(s);
            }
            return list;
        }

        /// <summary>Payment_Transaction ที่รับเงินผ่านเกตเวย์และผูกใบเสร็จนี้ (คืนผ่านเกตเวย์ได้)</summary>
        public int? FindGatewayTxn(string receiptId)
        {
            if (string.IsNullOrWhiteSpace(receiptId)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"IF OBJECT_ID('dbo.Payment_Transaction','U') IS NOT NULL
                        SELECT TOP 1 ID FROM Payment_Transaction
                         WHERE Receipt_ID = @r AND [Status] IN ('PAID','REFUNDED')
                           AND ISNULL(Provider, '') NOT IN ('MANUAL_QR', 'TRANSFER', 'CASH')
                         ORDER BY ID DESC
                      ELSE SELECT CAST(NULL AS INT) AS ID WHERE 1 = 0",
                    new Dictionary<string, object> { { "@r", receiptId.Trim() } });
                if (dt != null && dt.Rows.Count > 0) return IntN(dt.Rows[0][0]);
            }
            catch { }
            return null;
        }

        // ── อ่าน/เขียน Credit_Note ─────────────────────────────────────────────

        public Record Load(int id)
        {
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT CN.*, PH.Paid_How AS PaidHowName FROM Credit_Note CN
                   LEFT JOIN Account_Paid_How PH ON PH.ID = CN.Refund_Paid_How_ID
                  WHERE CN.ID = @id",
                new Dictionary<string, object> { { "@id", id } });
            if (dt == null || dt.Rows.Count == 0) return null;
            var rec = MapRecord(dt.Rows[0]);
            var lt = _code.DatabaseQuerySafe(_conn,
                "SELECT * FROM Credit_Note_Line WHERE CN_ID = @id ORDER BY Line_No, ID",
                new Dictionary<string, object> { { "@id", id } });
            if (lt != null)
                foreach (DataRow r in lt.Rows)
                    rec.Lines.Add(new Line
                    {
                        LineNo = IntN(r["Line_No"]) ?? 1,
                        SourceLineNo = IntN(r["Source_Line_No"]),
                        ProductId = IntN(r["Product_ID"]),
                        ProductTypeId = IntN(r["ProductType_ID"]),
                        Description = Str(r["Description"]),
                        Qty = Dec(r["Qty"]),
                        Unit = Str(r["Unit"]),
                        UnitPrice = Dec(r["Unit_Price"]),
                        Amount = Dec(r["Amount"]),
                        RestockQty = Dec(r["Restock_Qty"]),
                        UnitCost = r["Unit_Cost"] == DBNull.Value ? (decimal?)null : Dec(r["Unit_Cost"]),
                        AccountCode = Str(r["Account_Code"])
                    });
            return rec;
        }

        private static Record MapRecord(DataRow r)
        {
            Func<string, DateTime?> D = c => r.Table.Columns.Contains(c) && r[c] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r[c]) : null;
            Func<string, string> S = c => r.Table.Columns.Contains(c) ? Str(r[c]) : "";
            var rec = new Record
            {
                Id = Convert.ToInt32(r["ID"]),
                CnNo = S("CN_No"),
                CnDate = D("CN_Date") ?? DateTime.Today,
                ReservationId = IntN(r["Reservation_ID"]) ?? 0,
                SourceReceiptId = S("Source_Receipt_ID"),
                SourceDocType = S("Source_Doc_Type"),
                ReasonCode = S("Reason_Code"),
                ReasonText = S("Reason_Text"),
                CustomerName = S("Customer_Name"),
                AmountGross = Dec(r["Amount_Gross"]),
                VatAmount = Dec(r["Vat_Amount"]),
                RefundAmount = Dec(r["Refund_Amount"]),
                RetainedAmount = Dec(r["Retained_Amount"]),
                RetainedAs = S("Retained_As"),
                RefundPaidHowId = IntN(r["Refund_Paid_How_ID"]),
                RefundMethod = S("Refund_Method"),
                RefundRef = S("Refund_Ref"),
                RefundSlipPath = S("Refund_Slip_Path"),
                RefundStatus = S("Refund_Status"),
                GatewayTxnId = IntN(r["Gateway_Txn_ID"]),
                Restock = Bool(r["Restock"]),
                Status = S("Status"),
                CancelBatch = S("Cancel_Batch"),
                CreatedBy = S("Created_By"),
                CreatedAt = D("Created_At") ?? DateTime.Now,
                ApprovedBy = S("Approved_By"),
                ApprovedAt = D("Approved_At"),
                VoidedBy = S("Voided_By"),
                VoidedAt = D("Voided_At"),
                VoidReason = S("Void_Reason"),
                NexaaccPath = S("Nexaacc_Path"),
                NexaaccDocNo = S("Nexaacc_Doc_No"),
                NexaaccRefundId = S("Nexaacc_Refund_Id"),
                NexaaccCounterCode = S("Nexaacc_Counter_Code"),
                NexaaccMarker = S("Nexaacc_Marker"),
                SyncError = S("Sync_Error"),
                Notes = S("Notes"),
                RefundPaidHowName = S("PaidHowName")
            };
            Guid g;
            if (r["Nexaacc_Doc_Id"] != DBNull.Value && Guid.TryParse(r["Nexaacc_Doc_Id"].ToString(), out g) && g != Guid.Empty) rec.NexaaccDocId = g;
            return rec;
        }

        /// <summary>รายการใบลดหนี้ตามช่วงวันที่/สถานะ/คำค้น (หน้า Account/CreditNotes)</summary>
        public DataTable List(DateTime from, DateTime to, string status, string keyword, int top = 300)
        {
            var ps = new Dictionary<string, object> { { "@f", from.Date }, { "@t", to.Date } };
            var sb = new StringBuilder($@"SELECT TOP {Math.Max(1, Math.Min(top, 1000))} CN.ID, CN.CN_No, CN.CN_Date, CN.Reservation_ID, CN.Source_Receipt_ID,
                   CN.Source_Doc_Type, CN.Reason_Code, CN.Reason_Text, CN.Customer_Name, CN.Amount_Gross, CN.Vat_Amount, CN.Refund_Amount,
                   CN.Retained_Amount, CN.Refund_Method, CN.Refund_Status, CN.Refund_Ref, CN.Status, CN.Nexaacc_Doc_No, CN.Nexaacc_Doc_Id,
                   CN.Nexaacc_Path, CN.Sync_Error, CN.Created_By, CN.Approved_By, CN.Cancel_Batch, PH.Paid_How AS PaidHowName
              FROM Credit_Note CN LEFT JOIN Account_Paid_How PH ON PH.ID = CN.Refund_Paid_How_ID
             WHERE CN.CN_Date >= @f AND CN.CN_Date <= @t");
            if (!string.IsNullOrWhiteSpace(status)) { sb.Append(" AND CN.Status = @st"); ps["@st"] = status.Trim().ToUpperInvariant(); }
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                sb.Append(" AND (CN.CN_No LIKE @kw OR CN.Source_Receipt_ID LIKE @kw OR CN.Customer_Name LIKE @kw OR CN.Nexaacc_Doc_No LIKE @kw OR CAST(CN.Reservation_ID AS NVARCHAR(20)) = @kwx)");
                string kw = keyword.Trim();
                ps["@kw"] = "%" + kw.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
                ps["@kwx"] = kw;
            }
            sb.Append(" ORDER BY CN.CN_Date DESC, CN.ID DESC");
            return _code.DatabaseQuerySafe(_conn, sb.ToString(), ps);
        }

        private void SetFields(int id, string setSql, Dictionary<string, object> ps)
        {
            if (ps == null) ps = new Dictionary<string, object>();
            ps["@id"] = id;
            _code.DatabaseInsertSafe(_conn, "UPDATE Credit_Note SET " + setSql + " WHERE ID = @id", ps);
        }

        // ── สร้าง ───────────────────────────────────────────────────────────────

        /// <summary>
        /// สร้างใบลดหนี้ 1 ใบจากใบต้นทาง (สถานะ DRAFT) — ถ้าไม่ต้องอนุมัติ/ผู้สร้างอนุมัติได้ → อนุมัติต่อทันที
        /// ตรวจ: ยอดรวมบรรทัด + ส่วนที่ยึด ≤ ยอดที่ยังลดหนี้ได้ · คืนเงินต้องมีแหล่งเงิน · มัดจำต้องมาจากการยกเลิกการจอง
        /// </summary>
        /// <returns>ID ใบลดหนี้ (&gt;0) หรือ -1 (ดู message)</returns>
        public int Create(Request req, bool fromCancellation, out string message)
        {
            message = null;
            if (!TableReady(_conn)) { message = "ยังไม่ได้รัน migration PHASE19_Migration_33_Credit_Notes.sql"; return -1; }
            if (req == null) { message = "ไม่มีข้อมูล"; return -1; }
            var src = LoadSource(req.SourceReceiptId);
            if (src == null) { message = "ไม่พบใบต้นทาง " + req.SourceReceiptId; return -1; }
            if (IsExcludedSource(src.Id)) { message = "ใบ " + src.Id + " เป็นเอกสารรับเงิน OTA — คืนเงินผ่านช่องทาง OTA (ออกใบลดหนี้จากหน้านี้ไม่ได้)"; return -1; }
            if (!(src.Status == "" || src.Status.Equals("Normal", StringComparison.OrdinalIgnoreCase)
                  || (fromCancellation && src.IsDeposit)))
            { message = "ใบต้นทางสถานะ " + src.Status + " — ออกใบลดหนี้ได้เฉพาะใบปกติ"; return -1; }
            if (src.IsDeposit && !fromCancellation)
            { message = "ใบ " + src.Id + " เป็นใบเสร็จมัดจำ — คืนมัดจำผ่าน \"ยกเลิกการจอง → คืนเงินแบบละเอียด\" (ระบบกลับหนี้สินมัดจำ + ภาษีขายตามโหมด VAT ให้)"; return -1; }

            decimal gross = 0m;
            int n = 0;
            foreach (var l in req.Lines)
            {
                l.Amount = Math.Round(l.Amount, 2, MidpointRounding.AwayFromZero);
                if (l.Amount < 0) { message = "ยอดรายการติดลบไม่ได้"; return -1; }
                if (l.Qty <= 0) l.Qty = 1m;
                if (l.RestockQty < 0) l.RestockQty = 0;
                gross += l.Amount;
                l.LineNo = ++n;
            }
            decimal retained = Math.Round(Math.Max(0m, req.RetainedAmount), 2, MidpointRounding.AwayFromZero);
            if (retained > 0 && !src.IsDeposit) retained = 0m;   // ใบกำกับ: ส่วนที่ไม่คืน = รายได้คงเดิม ไม่ต้องบันทึก
            if (gross <= 0 && retained <= 0) { message = "ยอดใบลดหนี้ต้องมากกว่า 0"; return -1; }
            src.Creditable = src.IsDeposit ? src.Creditable : Math.Max(0m, src.Total - src.Credited);
            if (gross + retained > src.Creditable + 0.01m)
            {
                message = $"ยอดรวม {gross + retained:N2} เกินยอดที่ยังลดหนี้/คืนได้ของ {src.Id} ({src.Creditable:N2} — ลดหนี้ไปแล้ว {src.Credited:N2})";
                return -1;
            }

            string method = (req.RefundMethod ?? MNone).Trim().ToUpperInvariant();
            if (method != MTransfer && method != MCash && method != MGateway) method = MNone;
            decimal refund = method == MNone ? 0m : gross;
            int? gatewayTxn = null;
            int? paidHowId = req.RefundPaidHowId;
            if (method == MGateway)
            {
                gatewayTxn = FindGatewayTxn(src.Id);
                if (!gatewayTxn.HasValue) { message = "ใบ " + src.Id + " ไม่ได้รับเงินผ่านเกตเวย์ — เลือกโอนคืน/เงินสดแทน"; return -1; }
                paidHowId = src.PaidHowId;   // เงินออกจากบัญชีพักเกตเวย์เดิม
            }
            if (refund > 0 && !paidHowId.HasValue) { message = "เลือกแหล่งเงินที่จ่ายคืน"; return -1; }
            bool pending = method == MNone && req.RefundPending && gross > 0;

            bool hasVat = false;
            try { hasVat = UsesVat(); } catch { }
            decimal vat = 0m;
            if (hasVat && gross > 0)
            {
                var cfg = new AccountingConfig(_conn);
                if (src.IsDeposit && !cfg.IsDepositVatAtReceipt) vat = 0m;                // CHECKOUT: มัดจำไม่แยก VAT
                else if (src.Total > 0 && src.Vat > 0) vat = Math.Round(gross * src.Vat / src.Total, 2, MidpointRounding.AwayFromZero);
                else if (!src.IsDeposit) vat = 0m;
                else vat = Math.Round(gross * 7m / 107m, 2, MidpointRounding.AwayFromZero);
            }

            // บัญชีรายได้ต่อบรรทัด (สำรองเมื่อจับคู่บรรทัดของเอกสาร NextAcc ไม่ได้)
            var mapper = new AccountingDataMapper(_conn);
            foreach (var l in req.Lines)
            {
                if (!string.IsNullOrWhiteSpace(l.AccountCode)) continue;
                try
                {
                    string key = src.IsDeposit ? "ADVANCE_DEPOSIT" : mapper.GetProductTypeRevenueCode(l.ProductTypeId);
                    l.AccountCode = mapper.GetAccountCode(key);
                }
                catch { l.AccountCode = null; }
            }

            string user = CurrentUser();
            DateTime cnDate = (req.CnDate ?? DateTime.Today).Date;
            if (cnDate < src.Date.Date) cnDate = DateTime.Today;
            string prefix = "CN" + cnDate.ToString("yyMM", Inv) + "-";
            var ps = new Dictionary<string, object>
            {
                { "@prefix", prefix }, { "@plen", prefix.Length + 1 },
                { "@d", cnDate }, { "@res", req.ReservationId > 0 ? (object)req.ReservationId : (src.ReservationId > 0 ? (object)src.ReservationId : DBNull.Value) },
                { "@src", src.Id }, { "@srcType", src.DocType },
                { "@reason", string.IsNullOrWhiteSpace(req.ReasonCode) ? "OTHER" : req.ReasonCode.Trim().ToUpperInvariant() },
                { "@rtext", (object)Trunc(req.ReasonText, 500) ?? DBNull.Value },
                { "@cust", (object)Trunc(string.IsNullOrWhiteSpace(req.CustomerName) ? src.CustomerName : req.CustomerName, 200) ?? DBNull.Value },
                { "@gross", gross }, { "@vat", vat }, { "@refund", refund }, { "@ret", retained },
                { "@retAs", retained > 0 ? (object)(req.RetainedAs == RetCompensation ? RetCompensation : RetPriceOrFee) : DBNull.Value },
                { "@ph", paidHowId.HasValue && refund > 0 ? (object)paidHowId.Value : DBNull.Value },
                { "@method", method },
                { "@rref", (object)Trunc(req.RefundRef, 200) ?? DBNull.Value },
                { "@slip", (object)Trunc(req.RefundSlipPath, 400) ?? DBNull.Value },
                { "@rstatus", refund > 0 ? RsPaid : (pending ? RsPending : RsNone) },
                { "@gw", gatewayTxn.HasValue ? (object)gatewayTxn.Value : DBNull.Value },
                { "@restock", req.Lines.Any(l => l.RestockQty > 0) },
                { "@batch", (object)Trunc(req.CancelBatch, 40) ?? DBNull.Value },
                { "@by", user },
                { "@notes", (object)Trunc(req.Notes, 1000) ?? DBNull.Value }
            };
            // เลขที่รันต่อเดือน CN{yyMM}-#### — UPDLOCK/HOLDLOCK ใน statement เดียวกับ INSERT กันเลขซ้ำเมื่อกดพร้อมกัน
            var ins = _code.DatabaseQuerySafe(_conn,
                @"SET XACT_ABORT ON;
                  BEGIN TRAN;
                  DECLARE @next INT = ISNULL((SELECT MAX(TRY_CONVERT(INT, SUBSTRING(CN_No, @plen, 10)))
                                                FROM Credit_Note WITH (UPDLOCK, HOLDLOCK)
                                               WHERE CN_No LIKE @prefix + '%'), 0) + 1;
                  DECLARE @no NVARCHAR(30) = @prefix + RIGHT('0000' + CAST(@next AS NVARCHAR(10)), CASE WHEN @next > 9999 THEN LEN(CAST(@next AS NVARCHAR(10))) ELSE 4 END);
                  INSERT INTO Credit_Note (CN_No, CN_Date, Reservation_ID, Source_Receipt_ID, Source_Doc_Type, Reason_Code, Reason_Text,
                                           Customer_Name, Amount_Gross, Vat_Amount, Refund_Amount, Retained_Amount, Retained_As,
                                           Refund_Paid_How_ID, Refund_Method, Refund_Ref, Refund_Slip_Path, Refund_Status, Gateway_Txn_ID,
                                           Restock, Status, Cancel_Batch, Created_By, Created_At, Notes)
                  VALUES (@no, @d, @res, @src, @srcType, @reason, @rtext, @cust, @gross, @vat, @refund, @ret, @retAs,
                          @ph, @method, @rref, @slip, @rstatus, @gw, @restock, 'DRAFT', @batch, @by, GETDATE(), @notes);
                  SELECT CAST(SCOPE_IDENTITY() AS INT) AS NewID, @no AS CnNo;
                  COMMIT;", ps);
            if (ins == null || ins.Rows.Count == 0 || ins.Rows[0]["NewID"] == DBNull.Value) { message = "บันทึกใบลดหนี้ไม่สำเร็จ"; return -1; }
            int id = Convert.ToInt32(ins.Rows[0]["NewID"]);
            string cnNo = Str(ins.Rows[0]["CnNo"]);

            foreach (var l in req.Lines)
            {
                _code.DatabaseInsertSafe(_conn,
                    @"INSERT INTO Credit_Note_Line (CN_ID, Line_No, Source_Line_No, Product_ID, ProductType_ID, Description, Qty, Unit,
                                                   Unit_Price, Amount, Restock_Qty, Unit_Cost, Account_Code)
                      VALUES (@cn, @no, @sno, @pid, @pt, @desc, @qty, @unit, @price, @amt, @rq, @cost, @acc)",
                    new Dictionary<string, object>
                    {
                        { "@cn", id }, { "@no", l.LineNo },
                        { "@sno", l.SourceLineNo.HasValue ? (object)l.SourceLineNo.Value : DBNull.Value },
                        { "@pid", l.ProductId.HasValue ? (object)l.ProductId.Value : DBNull.Value },
                        { "@pt", l.ProductTypeId.HasValue ? (object)l.ProductTypeId.Value : DBNull.Value },
                        { "@desc", (object)Trunc(l.Description, 500) ?? DBNull.Value },
                        { "@qty", l.Qty }, { "@unit", (object)Trunc(l.Unit, 30) ?? DBNull.Value },
                        { "@price", l.Qty > 0 ? Math.Round(l.Amount / l.Qty, 4) : l.Amount }, { "@amt", l.Amount },
                        { "@rq", l.RestockQty }, { "@cost", l.UnitCost.HasValue ? (object)l.UnitCost.Value : DBNull.Value },
                        { "@acc", (object)Trunc(l.AccountCode, 30) ?? DBNull.Value }
                    });
            }

            _code.Logs(_conn, "CreditNote",
                $"สร้างใบลดหนี้ {cnNo} (#{id}) ต้นทาง {src.Id} [{src.DocType}] ยอด {gross:N2} VAT {vat:N2} คืน {refund:N2} ({method}) ยึด {retained:N2} โดย {user}", user);
            message = "สร้างใบลดหนี้ " + cnNo + " แล้ว";

            if (!RequireApproval(_conn) || CanApproveCurrentUser())
            {
                string am;
                if (Approve(id, out am)) message += " · " + am;
                else message += " · ⚠ อนุมัติไม่สำเร็จ: " + am;
            }
            else message += " · รออนุมัติ (Owner/Admin ที่มีสิทธิ์บัญชี)";
            return id;
        }

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length > max ? s.Substring(0, max) : s;
        }

        private bool UsesVat() => Take_Time_BangPhra.TaxReports.TaxReportKit.BusinessUsesVat(_conn);

        // ── อนุมัติ ─────────────────────────────────────────────────────────────

        /// <summary>
        /// DRAFT → APPROVED (ชิงสถานะแบบ atomic) แล้วทำผลข้างเคียงตามลำดับ:
        ///   1) คืนผ่านเกตเวย์ (ถ้าเลือก) — ล้ม = ถอยกลับเป็น DRAFT ไม่มีผลอื่นเกิด
        ///   2) คืนสต๊อก (Product_In 'CN_RESTOCK' + กลับต้นทุนขาย/จำนวนบน NextAcc ผ่านคิว STOCK_OUT_COGS_REVERSE)
        ///   3) แถว Payment_History REFUND (ยอดติดลบ) ของการจอง — ReservationBalance เห็นยอดคืน
        ///   4) เข้าคิว CREATE_CREDIT_NOTE (เมื่อเชื่อม NextAcc)
        /// </summary>
        public bool Approve(int id, out string message)
        {
            message = null;
            string user = CurrentUser();
            var got = _code.DatabaseQuerySafe(_conn,
                @"UPDATE Credit_Note SET Status = 'APPROVED', Approved_By = @by, Approved_At = GETDATE(), Sync_Error = NULL
                   WHERE ID = @id AND Status = 'DRAFT';
                  SELECT @@ROWCOUNT AS N;",
                new Dictionary<string, object> { { "@id", id }, { "@by", user } });
            if (got == null || got.Rows.Count == 0 || Convert.ToInt32(got.Rows[0]["N"]) == 0)
            { message = "ใบนี้ไม่ได้อยู่ในสถานะร่าง (อาจถูกอนุมัติ/ยกเลิกไปแล้ว)"; return false; }

            var rec = Load(id);
            if (rec == null) { message = "ไม่พบใบลดหนี้"; return false; }

            // 1) เกตเวย์
            if (rec.RefundMethod == MGateway && rec.RefundAmount > 0 && rec.GatewayTxnId.HasValue)
            {
                string r;
                try
                {
                    r = new Take_Time_BangPhra.Payments.OnlinePaymentService(_conn)
                        .RefundTransaction(rec.GatewayTxnId.Value, rec.RefundAmount, "ใบลดหนี้ " + rec.CnNo + " " + (rec.ReasonText ?? ""), CurrentAdminId());
                }
                catch (Exception ex) { r = "คืนเงินไม่สำเร็จ: " + ex.Message; }
                if (r == null || r.IndexOf("สำเร็จ", StringComparison.Ordinal) < 0 || r.IndexOf("ไม่สำเร็จ", StringComparison.Ordinal) >= 0)
                {
                    SetFields(id, "Status = 'DRAFT', Approved_By = NULL, Approved_At = NULL, Sync_Error = @e",
                        new Dictionary<string, object> { { "@e", Trunc("คืนผ่านเกตเวย์ไม่สำเร็จ: " + r, 1000) } });
                    message = "คืนผ่านเกตเวย์ไม่สำเร็จ — ใบยังเป็นร่าง: " + r;
                    return false;
                }
                SetFields(id, "Refund_Ref = ISNULL(Refund_Ref, @r)", new Dictionary<string, object> { { "@r", Trunc(r, 200) } });
            }

            var notes = new List<string>();
            // 2) คืนสต๊อก
            if (rec.Restock)
            {
                try { notes.Add(Restock(rec)); }
                catch (Exception ex) { notes.Add("⚠ คืนสต๊อกไม่สำเร็จ: " + ex.Message); }
            }
            // 3) Payment_History
            if (rec.RefundAmount > 0 && rec.ReservationId > 0)
            {
                try { InsertRefundLedger(rec); }
                catch (Exception ex) { notes.Add("⚠ บันทึกประวัติการคืนเงินไม่สำเร็จ: " + ex.Message); }
            }
            // 4) NextAcc
            notes.Add(EnqueueToNextAcc(rec));

            _code.Logs(_conn, "CreditNote", $"อนุมัติใบลดหนี้ {rec.CnNo} (#{id}) โดย {user} — {string.Join(" · ", notes)}", user);
            message = "อนุมัติแล้ว · " + string.Join(" · ", notes.Where(x => !string.IsNullOrEmpty(x)));
            return true;
        }

        private string EnqueueToNextAcc(Record rec)
        {
            try
            {
                var cfg = new AccountingConfig(_conn);
                if (!cfg.IsConfigured || !cfg.Enabled)
                {
                    SetFields(rec.Id, "Nexaacc_Path = 'LOCAL'", null);
                    return "ยังไม่ได้เชื่อม NextAcc — บันทึกในระบบ (พิมพ์ใบลดหนี้/หลักฐานคืนเงินจากหน้านี้)";
                }
                if (cfg.IsReceiptLocal)
                {
                    SetFields(rec.Id, "Nexaacc_Path = 'LOCAL'", null);
                    return "ใบเสร็จตั้งเป็น LOCAL — ไม่ส่ง NextAcc";
                }
                long q = new AccountingSyncService(_conn).EnqueueCreditNote(rec.Id, rec.CnNo);
                return q > 0 ? "เข้าคิวส่ง NextAcc #" + q : "⚠ เข้าคิว NextAcc ไม่สำเร็จ";
            }
            catch (Exception ex) { return "⚠ เข้าคิว NextAcc ไม่สำเร็จ: " + ex.Message; }
        }

        public const string RestockInType = "CN_RESTOCK";

        private string Restock(Record rec)
        {
            var sync = new AccountingSyncService(_conn);
            var cfg = new AccountingConfig(_conn);
            bool acct = cfg.IsConfigured && cfg.Enabled;
            int done = 0;
            foreach (var l in rec.Lines)
            {
                if (l.RestockQty <= 0 || !l.ProductId.HasValue || l.ProductId.Value <= 0) continue;
                decimal cost = l.UnitCost ?? 0m;
                string name = l.Description;
                try
                {
                    var p = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 Product_Name, ISNULL(Cost_Price, 0) AS Cost FROM Product WHERE ID = @id",
                        new Dictionary<string, object> { { "@id", l.ProductId.Value } });
                    if (p == null || p.Rows.Count == 0) continue;
                    name = Str(p.Rows[0]["Product_Name"]);
                    if (cost <= 0) cost = Dec(p.Rows[0]["Cost"]);
                }
                catch { continue; }
                var ps = new Dictionary<string, object>
                {
                    { "@d", DateTime.Now }, { "@pid", l.ProductId.Value }, { "@amt", l.RestockQty }, { "@cost", cost }, { "@t", RestockInType }
                };
                try
                {
                    _code.DatabaseInsertSafe(_conn,
                        "INSERT INTO [dbo].[Product_In] ([DateTime_In],[Product_ID],[Amount],[PricePerUnit],[InType]) VALUES (@d,@pid,@amt,@cost,@t)", ps);
                }
                catch
                {
                    ps.Remove("@t");
                    _code.DatabaseInsertSafe(_conn,
                        "INSERT INTO [dbo].[Product_In] ([DateTime_In],[Product_ID],[Amount],[PricePerUnit]) VALUES (@d,@pid,@amt,@cost)", ps);
                }
                if (acct)
                    sync.EnqueueStockOutCogsReversal(l.ProductId.Value, name, l.RestockQty, cost, rec.CnDate,
                        "รับคืนสินค้าตามใบลดหนี้ " + rec.CnNo, "CN-" + rec.CnNo + "-L" + l.LineNo);
                done++;
            }
            return done > 0 ? "คืนเข้าสต๊อก " + done + " รายการ" : "";
        }

        private void InsertRefundLedger(Record rec)
        {
            bool cancelled = false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 Status FROM Reservation WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", rec.ReservationId } });
                string st = dt != null && dt.Rows.Count > 0 ? Str(dt.Rows[0][0]) : "";
                cancelled = st.StartsWith("ยกเลิก", StringComparison.Ordinal);
            }
            catch { }
            string method = rec.RefundMethod == MCash ? "CASH" : rec.RefundMethod == MGateway ? "ONLINE" : "TRANSFER";
            _code.DatabaseInsertSafe(_conn,
                @"INSERT INTO Payment_History (Reservation_ID, PaymentDate, PaymentAmount, PaymentType, PaymentMethod, Receipt_ID,
                                               ProcessedBy_AdminID, Notes, Status, CreatedDate)
                  VALUES (@res, @d, @amt, 'REFUND', @m, @rid, @admin, @n, @st, GETDATE())",
                new Dictionary<string, object>
                {
                    { "@res", rec.ReservationId }, { "@d", DateTime.Now }, { "@amt", -rec.RefundAmount }, { "@m", method },
                    { "@rid", string.IsNullOrWhiteSpace(rec.SourceReceiptId) ? (object)DBNull.Value : rec.SourceReceiptId },
                    { "@admin", CurrentAdminId().HasValue ? (object)CurrentAdminId().Value : DBNull.Value },
                    { "@n", Trunc("คืนเงินตามใบลดหนี้ " + rec.CnNo + (string.IsNullOrWhiteSpace(rec.RefundRef) ? "" : " อ้างอิง " + rec.RefundRef), 500) },
                    // การจองที่ยกเลิกแล้ว: แถวรับเงินเดิมถูกตั้ง CANCELLED ไปแล้ว → แถวคืนเป็น REFUNDED (ข้อมูลอ้างอิง ไม่นับในยอด)
                    { "@st", cancelled ? "REFUNDED" : "COMPLETED" }
                });
        }

        // ── บันทึกคืนเงินภายหลัง (ใบที่ตั้ง "จะคืนภายหลัง") ───────────────────

        public bool RecordRefund(int id, string method, int? paidHowId, string refundRef, string slipPath, out string message)
        {
            message = null;
            var rec = Load(id);
            if (rec == null) { message = "ไม่พบใบลดหนี้"; return false; }
            if (rec.RefundStatus != RsPending) { message = "ใบนี้ไม่ได้ค้างคืนเงิน"; return false; }
            if (rec.Status != StApproved && rec.Status != StSynced) { message = "ต้องอนุมัติใบลดหนี้ก่อน"; return false; }
            method = (method ?? "").Trim().ToUpperInvariant();
            if (method != MTransfer && method != MCash) { message = "เลือกวิธีคืนเงิน (โอน/เงินสด)"; return false; }
            if (!paidHowId.HasValue) { message = "เลือกแหล่งเงินที่จ่ายคืน"; return false; }
            var got = _code.DatabaseQuerySafe(_conn,
                @"UPDATE Credit_Note SET Refund_Status = 'PAID', Refund_Method = @m, Refund_Paid_How_ID = @ph, Refund_Amount = Amount_Gross,
                                         Refund_Ref = @r, Refund_Slip_Path = ISNULL(@s, Refund_Slip_Path)
                   WHERE ID = @id AND Refund_Status = 'PENDING';
                  SELECT @@ROWCOUNT AS N;",
                new Dictionary<string, object>
                {
                    { "@id", id }, { "@m", method }, { "@ph", paidHowId.Value },
                    { "@r", (object)Trunc(refundRef, 200) ?? DBNull.Value }, { "@s", (object)Trunc(slipPath, 400) ?? DBNull.Value }
                });
            if (got == null || got.Rows.Count == 0 || Convert.ToInt32(got.Rows[0]["N"]) == 0) { message = "บันทึกไม่สำเร็จ (สถานะเปลี่ยนไปแล้ว)"; return false; }
            rec = Load(id);
            try { if (rec.ReservationId > 0) InsertRefundLedger(rec); } catch { }
            string q = "";
            try
            {
                var cfg = new AccountingConfig(_conn);
                if (cfg.IsConfigured && cfg.Enabled && !cfg.IsReceiptLocal && rec.Status == StSynced)
                {
                    long qid = new AccountingSyncService(_conn).EnqueueCreditNoteRefund(rec.Id, rec.CnNo);
                    q = qid > 0 ? " · เข้าคิว NextAcc #" + qid : " · ⚠ เข้าคิวไม่สำเร็จ";
                }
                else if (rec.Status == StApproved) q = " · จะลงบัญชีพร้อมใบลดหนี้ตอนส่ง NextAcc";
            }
            catch (Exception ex) { q = " · ⚠ " + ex.Message; }
            _code.Logs(_conn, "CreditNote", $"บันทึกคืนเงินภายหลัง {rec.CnNo} {rec.AmountGross:N2} ({method})", CurrentUser());
            message = "บันทึกคืนเงินแล้ว" + q;
            return true;
        }

        // ── ยกเลิก ─────────────────────────────────────────────────────────────

        public bool Void(int id, string reason, out string message)
        {
            message = null;
            var rec = Load(id);
            if (rec == null) { message = "ไม่พบใบลดหนี้"; return false; }
            if (rec.Status == StVoided) { message = "ยกเลิกไปแล้ว"; return false; }
            string user = CurrentUser();
            if (string.IsNullOrWhiteSpace(reason)) { message = "ระบุเหตุผลการยกเลิก"; return false; }
            if (rec.RefundMethod == MGateway && rec.Status != StDraft)
            { message = "คืนเงินผ่านเกตเวย์ไปแล้ว — ยกเลิกใบลดหนี้ไม่ได้ (เงินออกไปแล้ว)"; return false; }

            if (rec.Status == StDraft)
            {
                SetFields(id, "Status = 'VOIDED', Voided_By = @by, Voided_At = GETDATE(), Void_Reason = @r",
                    new Dictionary<string, object> { { "@by", user }, { "@r", Trunc(reason, 500) } });
                message = "ยกเลิกใบร่างแล้ว";
                return true;
            }

            var sync = new AccountingSyncService(_conn);
            bool sent = rec.Status == StSynced || !string.IsNullOrEmpty(rec.NexaaccMarker);
            // มัดจำ: marker REFPRE:/JRN ถูกจด "ก่อน" ยิง refund-deposit/JE (Nexaacc_Path ตั้งทีหลัง) — มี marker = อาจคืนบน NextAcc ไปแล้ว ห้ามยกเลิกในระบบ
            if (sent && (rec.NexaaccPath == PathDepositRefund
                         || (rec.SourceDocType == SrcDeposit && rec.NexaaccPath != PathLocal)))
            {
                message = "คืนมัดจำบน NextAcc ไปแล้ว — NextAcc ยังไม่มี API ยกเลิกการคืนมัดจำ (กลับรายการที่หน้า \"เงินมัดจำ\" ของ NextAcc แล้วยกเลิกใบนี้ด้วยมือในฐานข้อมูล)";
                return false;
            }
            if (!sent)
            {
                // ยังไม่ถึง NextAcc — ถอนคิว (ถ้ายังรอส่ง) แล้วยกเลิกในระบบ
                if (!sync.CancelPendingCreditNoteQueue(rec.CnNo))
                {
                    message = "คิวส่ง NextAcc ของใบนี้กำลังประมวลผล — ลองใหม่ภายหลัง";
                    return false;
                }
                SetFields(id, "Status = 'VOIDED', Voided_By = @by, Voided_At = GETDATE(), Void_Reason = @r",
                    new Dictionary<string, object> { { "@by", user }, { "@r", Trunc(reason, 500) } });
                UndoLocalEffects(rec);
                message = "ยกเลิกแล้ว (ยังไม่ได้ส่ง NextAcc)" + (rec.Restock ? " · ⚠ สินค้าที่รับคืนเข้าสต๊อกไม่ถูกตัดออกอัตโนมัติ — ปรับที่หน้า สต๊อก" : "");
                return true;
            }
            SetFields(id, "Void_Reason = @r, Voided_By = @by", new Dictionary<string, object> { { "@by", user }, { "@r", Trunc(reason, 500) } });
            long q = sync.EnqueueVoidCreditNote(rec.Id, rec.CnNo, reason);
            if (q <= 0) { message = "เข้าคิวยกเลิกบน NextAcc ไม่สำเร็จ"; return false; }
            UndoLocalEffects(rec);
            message = "เข้าคิวยกเลิกใบลดหนี้บน NextAcc #" + q + " — สถานะจะเป็น \"ยกเลิกแล้ว\" เมื่อ NextAcc ยืนยัน"
                      + (rec.Restock ? " · ⚠ สินค้าที่รับคืนเข้าสต๊อกไม่ถูกตัดออกอัตโนมัติ — ปรับที่หน้า สต๊อก" : "");
            return true;
        }

        private void UndoLocalEffects(Record rec)
        {
            try
            {
                if (rec.RefundAmount > 0 && rec.ReservationId > 0)
                    _code.DatabaseInsertSafe(_conn,
                        @"UPDATE Payment_History SET Status = 'CANCELLED', UpdatedDate = GETDATE()
                           WHERE Reservation_ID = @res AND PaymentType = 'REFUND' AND Notes LIKE @n",
                        new Dictionary<string, object> { { "@res", rec.ReservationId }, { "@n", "%ใบลดหนี้ " + rec.CnNo + "%" } });
            }
            catch { }
        }

        /// <summary>เรียกจากคิวเมื่อ NextAcc ยืนยันการยกเลิกแล้ว</summary>
        internal void MarkVoided(int id)
        {
            SetFields(id, "Status = 'VOIDED', Voided_At = GETDATE()", null);
        }

        // ── ยกเลิกการจอง + คืนเงิน/ยึดเงิน ───────────────────────────────────

        public sealed class CancelResult
        {
            public bool Ok;
            public string Message;
            public readonly List<int> CreditNoteIds = new List<int>();
        }

        /// <summary>
        /// ยกเลิกการจองพร้อมคืนเงินบางส่วน/ทั้งหมด (แทนปุ่มยกเลิกเดิมเมื่อใช้ "คืนเงินแบบละเอียด"):
        ///   แบ่งยอดคืนลงใบต้นทาง — มัดจำคงเหลือก่อน (ส่วนที่ไม่คืนของมัดจำ = ยึดเป็นค่าธรรมเนียม → NextAcc realize-deposit)
        ///   แล้วใบกำกับ (ส่วนที่ไม่คืนของใบกำกับ = รายได้คงเดิม ไม่ออกใบลดหนี้ — audit C §7)
        /// จากนั้นตั้งสถานะการจอง/ล้างห้องเหมือนปุ่มเดิม แต่ **ไม่** ยิงคิวคืน/ริบ/void แบบเดิม (กันลงบัญชีซ้ำ)
        /// </summary>
        public CancelResult CancelReservation(int reservationId, decimal refundTotal, string retainedAs, string method,
            int? paidHowId, string refundRef, string slipPath, string reason)
        {
            var res = new CancelResult();
            if (!Enabled(_conn)) { res.Message = "ระบบใบลดหนี้ยังไม่เปิดใช้"; return res; }
            var money = LoadReservationMoney(reservationId);
            if (money == null) { res.Message = "ไม่พบการจอง"; return res; }
            if ((money.Status ?? "").StartsWith("ยกเลิก", StringComparison.Ordinal)) { res.Message = "การจองนี้ถูกยกเลิกไปแล้ว"; return res; }
            if ((money.Status ?? "").IndexOf("เช็คอิน", StringComparison.Ordinal) >= 0 || (money.Status ?? "").IndexOf("เช็คเอาท์", StringComparison.Ordinal) >= 0)
            { res.Message = "เข้าพักแล้ว — ยกเลิกการจองไม่ได้ (ออกใบลดหนี้ manual แทน)"; return res; }
            refundTotal = Math.Round(refundTotal, 2, MidpointRounding.AwayFromZero);
            decimal total = money.TotalCreditable;
            if (refundTotal < 0 || refundTotal > total + 0.01m) { res.Message = $"ยอดคืนต้องอยู่ระหว่าง 0 – {total:N2}"; return res; }
            method = (method ?? MNone).Trim().ToUpperInvariant();
            if (refundTotal > 0 && method == MNone) { res.Message = "เลือกวิธีคืนเงิน"; return res; }
            if (refundTotal > 0 && method != MGateway && !paidHowId.HasValue) { res.Message = "เลือกแหล่งเงินที่จ่ายคืน"; return res; }

            string batch = "CXL-" + reservationId + "-" + DateTime.Now.ToString("yyMMddHHmmss", Inv);
            decimal left = refundTotal;
            var reqs = new List<Request>();
            foreach (var s in money.Receipts.Where(x => x.IsDeposit && x.Creditable > 0.005m))
            {
                decimal r = Math.Min(s.Creditable, left);
                left -= r;
                decimal ret = s.Creditable - r;
                var q = NewCancelRequest(money, s, r, ret, retainedAs, method, paidHowId, refundRef, slipPath, reason, batch);
                q.Lines.Add(new Line
                {
                    Description = (r > 0 ? "คืนเงินมัดจำ" : "ยึดเงินมัดจำ") + " ตามใบเสร็จ " + s.Id + " (ยกเลิกการจอง #" + reservationId + ")",
                    Qty = 1m, Amount = r, ProductTypeId = 1
                });
                reqs.Add(q);
            }
            foreach (var s in money.Receipts.Where(x => !x.IsDeposit && x.Creditable > 0.005m))
            {
                decimal r = Math.Min(s.Creditable, left);
                left -= r;
                if (r <= 0.005m) continue;
                var q = NewCancelRequest(money, s, r, 0m, null, method, paidHowId, refundRef, slipPath, reason, batch);
                AddProportionalLines(q, s, r);
                reqs.Add(q);
            }
            if (left > 0.01m) { res.Message = "แบ่งยอดคืนลงใบต้นทางไม่ครบ (คงเหลือ " + left.ToString("N2") + ")"; return res; }

            // ตรวจเกตเวย์ก่อนสร้างอะไรเลย
            if (method == MGateway)
                foreach (var q in reqs)
                    if (q.Lines.Sum(l => l.Amount) > 0 && !FindGatewayTxn(q.SourceReceiptId).HasValue)
                    { res.Message = "ใบ " + q.SourceReceiptId + " ไม่ได้รับเงินผ่านเกตเวย์ — คืนผ่านเกตเวย์ไม่ได้ (เลือกโอน/เงินสด)"; return res; }

            var msgs = new List<string>();
            foreach (var q in reqs)
            {
                string m;
                int id = Create(q, true, out m);
                if (id <= 0)
                {
                    // ยังไม่ได้ยกเลิกการจอง — ยกเลิกใบที่สร้างไปแล้วในชุดนี้ (ร่าง/อนุมัติแต่ยังไม่ส่ง) แล้วรายงาน
                    foreach (int made in res.CreditNoteIds) { string vm; Void(made, "ยกเลิกอัตโนมัติ: สร้างชุดใบลดหนี้ไม่สำเร็จ", out vm); }
                    res.Message = "สร้างใบลดหนี้ของ " + q.SourceReceiptId + " ไม่สำเร็จ: " + m;
                    return res;
                }
                res.CreditNoteIds.Add(id);
                msgs.Add(m);
            }

            ApplyReservationCancellation(money, refundTotal, total - refundTotal, reason);
            res.Ok = true;
            res.Message = (refundTotal > 0 ? $"ยกเลิกการจอง #{reservationId} คืนเงิน {refundTotal:N2}" : $"ยกเลิกการจอง #{reservationId} ไม่คืนเงิน")
                          + (total - refundTotal > 0.005m ? $" · ยึด {total - refundTotal:N2}" : "")
                          + (msgs.Count > 0 ? " · " + string.Join(" | ", msgs) : " · ไม่มีเงินรับไว้");
            return res;
        }

        private static Request NewCancelRequest(ReservationMoney money, SourceReceipt s, decimal refund, decimal retained, string retainedAs,
            string method, int? paidHowId, string refundRef, string slipPath, string reason, string batch)
        {
            var q = new Request
            {
                ReservationId = money.ReservationId,
                SourceReceiptId = s.Id,
                ReasonCode = retained > 0.005m || refund < s.Creditable - 0.005m ? "CANCEL_PARTIAL" : "CANCEL_REFUND",
                ReasonText = string.IsNullOrWhiteSpace(reason) ? "ยกเลิกการจอง #" + money.ReservationId : reason,
                CustomerName = money.CustomerName,
                RefundMethod = refund > 0 ? method : MNone,
                RefundPaidHowId = paidHowId,
                RefundRef = refundRef,
                RefundSlipPath = slipPath,
                RetainedAmount = retained,
                RetainedAs = retainedAs,
                CancelBatch = batch,
                CnDate = DateTime.Today
            };
            return q;
        }

        /// <summary>แตกยอดคืนลงบรรทัดของใบต้นทางตามสัดส่วน (บรรทัดสุดท้ายรับเศษ) — บัญชีรายได้ตรงกับใบเดิม</summary>
        public static void AddProportionalLines(Request q, SourceReceipt s, decimal amount)
        {
            decimal baseTotal = s.Lines.Sum(l => l.Amount);
            if (s.Lines.Count == 0 || baseTotal <= 0)
            {
                q.Lines.Add(new Line { Description = "ลดหนี้ตามใบเสร็จ " + s.Id, Qty = 1m, Amount = amount });
                return;
            }
            decimal used = 0m;
            for (int i = 0; i < s.Lines.Count; i++)
            {
                var sl = s.Lines[i];
                decimal a = i == s.Lines.Count - 1 ? amount - used : Math.Round(amount * sl.Amount / baseTotal, 2, MidpointRounding.AwayFromZero);
                if (a <= 0) continue;
                used += a;
                q.Lines.Add(new Line
                {
                    SourceLineNo = sl.No, ProductId = sl.ProductId, ProductTypeId = sl.ProductTypeId,
                    Description = sl.Description, Unit = sl.Unit,
                    Qty = a == sl.Amount ? sl.Qty : 1m, Amount = a, UnitCost = sl.UnitCost
                });
            }
        }

        private void ApplyReservationCancellation(ReservationMoney money, decimal refunded, decimal retained, string reason)
        {
            int resId = money.ReservationId;
            string status = refunded > 0.005m ? "ยกเลิกคืนเงิน" : "ยกเลิกไม่คืนเงิน";
            var p = new Dictionary<string, object> { { "@id", resId } };
            try
            {
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Payment_History SET Status = 'CANCELLED', Notes = @n
                       WHERE Reservation_ID = @id AND Status = 'COMPLETED' AND ISNULL(PaymentType, '') <> 'REFUND'",
                    new Dictionary<string, object> { { "@id", resId }, { "@n", "ยกเลิกจากการยกเลิกการจอง (ใบลดหนี้) ID: " + resId } });
            }
            catch { }
            _code.DatabaseInsertSafe(_conn,
                "UPDATE Reservation SET TotalPrice = 0, Deposit = @dep, [Status] = @st WHERE ID = @id",
                new Dictionary<string, object> { { "@id", resId }, { "@dep", retained }, { "@st", status } });
            // Payment_History REFUND ที่เพิ่งสร้างตอนอนุมัติ (ถ้ามี) → REFUNDED (การจองยกเลิกแล้ว ไม่นับยอด)
            try
            {
                _code.DatabaseInsertSafe(_conn,
                    "UPDATE Payment_History SET Status = 'REFUNDED' WHERE Reservation_ID = @id AND PaymentType = 'REFUND' AND Status = 'COMPLETED'", p);
            }
            catch { }

            // ชาร์จเข้าห้องที่ค้าง (PENDING) — คืนสต๊อก + กลับต้นทุน (เหมือนปุ่มยกเลิกเดิม)
            try
            {
                var pend = _code.DatabaseQuerySafe(_conn,
                    "SELECT ID FROM Reservation_Product_Charges WHERE Reservation_ID = @id AND Status = 'PENDING'", p);
                if (pend != null && pend.Rows.Count > 0)
                {
                    var svc = new Take_Time_BangPhra.RoomChargeService(_conn);
                    foreach (DataRow r in pend.Rows)
                        try { svc.CancelRoomCharge(Convert.ToInt64(r["ID"]), CurrentAdminId(), "ยกเลิกการจอง #" + resId); } catch { }
                }
            }
            catch { }

            _code.DatabaseInsertSafe(_conn, "DELETE FROM Reservation_Accommodation WHERE Reservation_ID = @id", p);
            _code.DatabaseInsertSafe(_conn, "DELETE FROM Reservation_Items WHERE Reservation_ID = @id", p);

            // ใบมัดจำ: คืน (บางส่วน) = 'Refunded' · ยึดทั้งหมด = 'Forfeit' — ไม่ใช่ 'Cancel' (ไม่ void บน NextAcc; ใบลดหนี้จัดการบัญชีแล้ว)
            foreach (var s in money.Receipts.Where(x => x.IsDeposit))
            {
                try
                {
                    var c = _code.DatabaseQuerySafe(_conn,
                        "SELECT ISNULL(SUM(Refund_Amount), 0) AS R, ISNULL(SUM(Retained_Amount), 0) AS K FROM Credit_Note WHERE Source_Receipt_ID = @r AND Status <> 'VOIDED'",
                        new Dictionary<string, object> { { "@r", s.Id } });
                    decimal rf = c != null && c.Rows.Count > 0 ? Dec(c.Rows[0]["R"]) : 0m;
                    decimal kp = c != null && c.Rows.Count > 0 ? Dec(c.Rows[0]["K"]) : 0m;
                    if (rf <= 0 && kp <= 0) continue;
                    _code.DatabaseInsertSafe(_conn,
                        "UPDATE Account_Receipt SET Status = @st WHERE ID = @r AND ISNULL(Status, N'Normal') IN (N'Normal', N'')",
                        new Dictionary<string, object> { { "@r", s.Id }, { "@st", rf > 0 ? "Refunded" : "Forfeit" } });
                }
                catch { }
            }

            try
            {
                string msg = (refunded > 0.005m ? "✅ *ยกเลิกการจอง (คืนเงิน — ใบลดหนี้)*" : "❌ *ยกเลิกการจอง (ไม่คืนเงิน — ใบลดหนี้)*")
                             + "\n🆔 การจอง: " + resId + "\n👤 ลูกค้า: " + (money.CustomerName ?? "-")
                             + "\n💵 คืนเงิน: " + refunded.ToString("N2", Inv) + " บาท"
                             + (retained > 0.005m ? "\n🧾 ยึดไว้: " + retained.ToString("N2", Inv) + " บาท" : "")
                             + (string.IsNullOrWhiteSpace(reason) ? "" : "\n📝 " + reason);
                Notify.Send(Notify.Ev.BookingCancel, msg);
            }
            catch { }
            _code.Logs(_conn, "CreditNote", $"ยกเลิกการจอง #{resId} ผ่านใบลดหนี้: คืน {refunded:N2} ยึด {retained:N2}", CurrentUser());
        }

        // ── พิมพ์ (ใบลดหนี้/หลักฐานการคืนเงินของ TakeTime) ─────────────────────

        public string RenderPrintHtml(Record rec)
        {
            string bizName, bizTax, bizAddr;
            Take_Time_BangPhra.TaxReports.TaxReportKit.BusinessHeader(_conn, out bizName, out bizTax, out bizAddr);
            Func<string, string> H = HttpUtility.HtmlEncode;
            bool taxCn = rec.SourceDocType == SrcTaxInvoice || rec.VatAmount > 0;
            string title = taxCn ? "ใบลดหนี้ / Credit Note" : "หลักฐานการคืนเงิน / Refund Voucher";
            var sb = new StringBuilder();
            sb.Append("<div class='cnp'>");
            sb.Append("<div class='cnp-h'><div><b>").Append(H(bizName)).Append("</b><br/>").Append(H(bizAddr))
              .Append(bizTax.Length > 0 ? "<br/>เลขประจำตัวผู้เสียภาษี " + H(bizTax) : "").Append("</div>");
            sb.Append("<div style='text-align:right'><div class='cnp-t'>").Append(H(title)).Append("</div>เลขที่ <b>").Append(H(rec.CnNo))
              .Append("</b><br/>วันที่ ").Append(rec.CnDate.ToString("dd/MM/yyyy", Inv));
            if (!string.IsNullOrEmpty(rec.NexaaccDocNo)) sb.Append("<br/>เอกสาร NextAcc ").Append(H(rec.NexaaccDocNo));
            sb.Append("</div></div>");
            sb.Append("<p>ลูกค้า: <b>").Append(H(rec.CustomerName)).Append("</b>")
              .Append(rec.ReservationId > 0 ? " · การจอง #" + rec.ReservationId : "")
              .Append("<br/>อ้างอิงเอกสารเดิม: <b>").Append(H(rec.SourceReceiptId)).Append("</b>")
              .Append(" · เหตุผล: ").Append(H(ReasonLabel(rec.ReasonCode)))
              .Append(string.IsNullOrWhiteSpace(rec.ReasonText) ? "" : " — " + H(rec.ReasonText)).Append("</p>");
            sb.Append("<table class='cnp-tb'><tr><th>#</th><th>รายการ</th><th style='text-align:right'>จำนวน</th><th style='text-align:right'>จำนวนเงิน (รวม VAT)</th></tr>");
            foreach (var l in rec.Lines)
                sb.Append("<tr><td>").Append(l.LineNo).Append("</td><td>").Append(H(l.Description)).Append("</td><td style='text-align:right'>")
                  .Append(l.Qty.ToString("#,##0.##", Inv)).Append(' ').Append(H(l.Unit)).Append("</td><td style='text-align:right'>")
                  .Append(l.Amount.ToString("#,##0.00", Inv)).Append("</td></tr>");
            sb.Append("</table>");
            sb.Append("<table class='cnp-sum'>");
            sb.Append("<tr><td>มูลค่าที่ลดลง (ไม่รวม VAT)</td><td>").Append((rec.AmountGross - rec.VatAmount).ToString("#,##0.00", Inv)).Append("</td></tr>");
            sb.Append("<tr><td>ภาษีมูลค่าเพิ่ม</td><td>").Append(rec.VatAmount.ToString("#,##0.00", Inv)).Append("</td></tr>");
            sb.Append("<tr><td><b>รวมลดหนี้</b></td><td><b>").Append(rec.AmountGross.ToString("#,##0.00", Inv)).Append("</b></td></tr>");
            if (rec.RetainedAmount > 0)
                sb.Append("<tr><td>ยึดไว้เป็นค่าธรรมเนียมยกเลิก</td><td>").Append(rec.RetainedAmount.ToString("#,##0.00", Inv)).Append("</td></tr>");
            sb.Append("<tr><td>คืนเงิน (").Append(H(MethodLabel(rec.RefundMethod)))
              .Append(string.IsNullOrEmpty(rec.RefundPaidHowName) ? "" : " · " + H(rec.RefundPaidHowName)).Append(")</td><td>")
              .Append(rec.RefundAmount.ToString("#,##0.00", Inv)).Append("</td></tr>");
            sb.Append("</table>");
            if (!string.IsNullOrWhiteSpace(rec.RefundRef)) sb.Append("<p>อ้างอิงการโอน/คืนเงิน: ").Append(H(rec.RefundRef)).Append("</p>");
            if (!taxCn)
                sb.Append("<p class='cnp-note'>เอกสารนี้เป็นหลักฐานการคืนเงินมัดจำ (มัดจำยังไม่ได้ออกใบกำกับภาษี/ภาษีขายยังไม่ถึงกำหนด) — ไม่ใช่ใบลดหนี้ตาม ม.86/10</p>");
            else if (string.IsNullOrEmpty(rec.NexaaccDocNo))
                sb.Append("<p class='cnp-note'>ใบลดหนี้ฉบับภาษีออกจากระบบบัญชี (NextAcc) — ฉบับนี้เป็นสำเนาจาก TakeTime</p>");
            sb.Append("<div class='cnp-sig'><div>ผู้รับเงินคืน<br/><br/>________________</div><div>ผู้จ่ายเงิน/ผู้อนุมัติ<br/><br/>________________<br/>")
              .Append(H(rec.ApprovedBy)).Append("</div></div>");
            sb.Append("</div>");
            return sb.ToString();
        }
    }
}
