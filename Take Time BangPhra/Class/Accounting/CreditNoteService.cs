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
            /// <summary>ใบของ folio การจอง (มัดจำ / มีบรรทัดค่าห้อง / ผูก Payment_History ของการจอง) — false = ใบอื่นที่แค่ผูกเลขจอง
            /// (รูมเซอร์วิส/POS ชำระทันที) ไม่รวมในยอดคืนตอนยกเลิกโดยอัตโนมัติ</summary>
            public bool IsFolio = true;
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
            /// <summary>ยอดคืนได้เฉพาะใบของ folio การจอง (ค่าเริ่มต้นตอนยกเลิก)</summary>
            public decimal FolioCreditable { get { decimal s = 0; foreach (var r in Receipts) if (r.IsFolio) s += r.Creditable; return s; } }
            public bool HasGateway;
            /// <summary>CHANNEL | HOTEL | UNKNOWN | NONE (ReservationBalance) · Ota_Revenue_Ref ของการจอง (ว่าง = ไม่มี/ยังไม่รัน migration)</summary>
            public string CollectMode = "", OtaRevenueRef = "";
            /// <summary>ใบที่ไม่นำมาคืน (เงิน OTA ที่ปรับเป็นลูกหนี้ OTA แล้ว) — แสดงเหตุผลให้ผู้ใช้เห็น</summary>
            public readonly List<string> Excluded = new List<string>();
            public bool IsChannelCollect => CollectMode == ReservationBalance.ModeChannel;
        }

        /// <summary>ผลตรวจว่ายกเลิก/ปิดเงินค้างของการจองได้ไหม (CheckCancellable)</summary>
        public sealed class CancelCheck
        {
            public bool Allowed;
            /// <summary>การจองถูกยกเลิกไปแล้ว (เลื่อนวันแล้วยกเลิก / ยกเลิกผ่านใบลดหนี้แต่ใบร่างถูกยกเลิก) — คืน/ยึดเงินค้างอย่างเดียว ไม่เปลี่ยนสถานะซ้ำ</summary>
            public bool SettleOnly;
            public string Message = "";
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

        public const string StatusPostponeCancelled = "ยกเลิกการเลื่อนวันเข้าพัก";

        /// <summary>
        /// เข้าพักแล้ว/เช็คเอาท์แล้ว — กฎเดียวกับ PaymentService.ProcessReservationPayment (arrived):
        /// เช็คเอาท์จริงเขียน 'เสร็จสิ้น' (sp_ProcessCheckout) · ตารางการจองใช้ 'เช็คเอ้าท์แล้ว' · 'ไม่มาเช็คอิน' (no-show) ไม่ใช่เข้าพัก
        /// </summary>
        public static bool IsArrivedStatus(string status)
        {
            string s = (status ?? "").Trim();
            return s == "เช็คอินแล้ว" || s == "เสร็จสิ้น"
                   || s.StartsWith("เช็คเอาท์", StringComparison.Ordinal)
                   || s.StartsWith("เช็คเอ้าท์", StringComparison.Ordinal);
        }

        /// <summary>มีใบลดหนี้ (ยังไม่ยกเลิก) ของการจองนี้/ใบเสร็จของการจองนี้ — ปุ่มยกเลิกแบบเดิมต้องไม่ void ใบเสร็จ/ลงคืน-ริบซ้ำ</summary>
        public static bool HasActiveCreditNotes(string conn, int reservationId)
        {
            if (reservationId <= 0 || !TableReady(conn)) return false;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    @"SELECT TOP 1 1 AS X FROM Credit_Note CN
                       WHERE CN.Status <> 'VOIDED'
                         AND (CN.Reservation_ID = @id
                              OR CN.Source_Receipt_ID IN (SELECT ID FROM Account_Receipt WHERE Reservation_ID = @id))",
                    new Dictionary<string, object> { { "@id", reservationId } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>เคยยกเลิกการจองนี้ผ่านใบลดหนี้ (มีชุด CXL-{id}-… แม้ใบร่างถูกยกเลิกภายหลัง)</summary>
        private bool HasCancelBatch(int reservationId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 1 AS X FROM Credit_Note WHERE Cancel_Batch LIKE @p",
                    new Dictionary<string, object> { { "@p", "CXL-" + reservationId.ToString(Inv) + "-%" } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>เคยเข้าคิวคืน/ริบมัดจำแบบเดิม (REFUND_DEPOSIT / FORFEIT_DEPOSIT) ของการจองนี้ — ห้ามคืน/ยึดผ่านใบลดหนี้ซ้ำ</summary>
        private bool LegacyDepositActionQueued(int reservationId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 1 AS X FROM Accounting_Sync_Queue
                       WHERE Entity_Type = 'RESERVATION' AND CAST(Entity_ID AS NVARCHAR(50)) = @id
                         AND Action_Type IN ('REFUND_DEPOSIT', 'FORFEIT_DEPOSIT') AND ISNULL(Status, '') <> 'CANCELLED'",
                    new Dictionary<string, object> { { "@id", reservationId.ToString(Inv) } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>สลิปที่ลูกค้าแนบเองยังรอตรวจ (CLAUDE.md ข้อ 16) — เงินอาจเข้าบัญชีแล้วแต่ยังไม่ลงระบบ</summary>
        public int PendingSlipCount(int reservationId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"IF OBJECT_ID('dbo.Payment_Slips','U') IS NOT NULL
                        SELECT COUNT(*) AS N FROM Payment_Slips
                         WHERE Reservation_ID = @id AND VerificationStatus = 'PENDING' AND ISNULL(IsActive, 1) = 1
                      ELSE SELECT 0 AS N",
                    new Dictionary<string, object> { { "@id", reservationId } });
                return dt != null && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// ใบเสร็จ "เงินสดปลอม" ของใบ OTA Channel Collect ที่ปรับเป็นลูกหนี้ OTA แล้ว (Payment_History OTA_RECLASS /
        /// Reservation.Ota_Revenue_Ref = 'RECLASS-{ใบเสร็จ}') — โรงแรมไม่ได้รับเงินนี้ OTA เป็นผู้คืนลูกค้า ⇒ ห้ามคืน/ลดหนี้จากโรงแรม
        /// </summary>
        public bool IsOtaReclassed(string receiptId, int reservationId)
        {
            if (string.IsNullOrWhiteSpace(receiptId)) return false;
            string rid = receiptId.Trim();
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 1 AS X FROM Payment_History WHERE Receipt_ID = @r AND Status = 'OTA_RECLASS'",
                    new Dictionary<string, object> { { "@r", rid } });
                if (dt != null && dt.Rows.Count > 0) return true;
            }
            catch { }
            return reservationId > 0 && LoadOtaRevenueRef(reservationId).Equals("RECLASS-" + rid, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Reservation.Ota_Revenue_Ref — ไม่มีคอลัมน์ (ยังไม่รัน migration) = ""</summary>
        private string LoadOtaRevenueRef(int reservationId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ISNULL(Ota_Revenue_Ref, N'') AS R FROM Reservation WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", reservationId } });
                return dt != null && dt.Rows.Count > 0 ? Str(dt.Rows[0][0]).Trim() : "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// ยกเลิก/ปิดเงินค้างได้ไหม:
        ///   · เข้าพัก/เช็คเอาท์แล้ว ('เช็คอินแล้ว', 'เสร็จสิ้น', 'เช็คเอ้าท์แล้ว') → ไม่ได้ — ออกก่อนกำหนด/ลดคืน = ใบลดหนี้ manual ต่อใบเสร็จ
        ///   · ยกเลิกแล้ว → ได้เฉพาะ "ปิดเงินค้าง" (ไม่เปลี่ยนสถานะซ้ำ) เมื่อเป็นใบเลื่อนวันที่ถูกยกเลิก หรือเคยยกเลิกผ่านใบลดหนี้
        ///     และไม่เคยคืน/ริบมัดจำแบบเดิม (กันลงบัญชีซ้ำ)
        ///   · มีสลิปลูกค้ารอตรวจ → ต้องตรวจสลิปก่อน (อนุมัติ = เงินเข้า / ปฏิเสธ = ไม่มีเงิน)
        /// </summary>
        public CancelCheck CheckCancellable(ReservationMoney money)
        {
            var c = new CancelCheck();
            if (money == null) { c.Message = "ไม่พบการจอง"; return c; }
            string st = (money.Status ?? "").Trim();
            if (st.StartsWith("ลบ", StringComparison.Ordinal))
            { c.Message = "ใบนี้ถูกย้ายไปใบใหม่จากการเลื่อนวันเข้าพัก (" + st + ") — ทำรายการที่ใบใหม่"; return c; }
            if (IsArrivedStatus(st))
            {
                c.Message = "การจองสถานะ \"" + st + "\" (เข้าพัก/เช็คเอาท์แล้ว) — ยกเลิกการจองไม่ได้: ออกก่อนกำหนด/ลดจำนวนคืน/คืนเงินบางส่วน " +
                            "ให้ออกใบลดหนี้ manual ต่อใบเสร็จ (แท็บ \"ออกใบลดหนี้\")";
                return c;
            }
            if (st.StartsWith("ยกเลิก", StringComparison.Ordinal))
            {
                bool ok = (st == StatusPostponeCancelled || HasCancelBatch(money.ReservationId))
                          && !LegacyDepositActionQueued(money.ReservationId);
                if (!ok)
                {
                    c.Message = "การจองนี้ถูกยกเลิกไปแล้ว (" + st + ") ด้วยปุ่ม/ขั้นตอนเดิม — เงินถูกคืน/ริบด้วยรายการเดิม ห้ามทำซ้ำที่นี่";
                    return c;
                }
                if (money.TotalCreditable <= 0.005m)
                {
                    c.Message = "การจองนี้ถูกยกเลิกแล้ว และไม่มีเงินค้างให้คืน/ยึด";
                    return c;
                }
                c.SettleOnly = true;
            }
            int slips = PendingSlipCount(money.ReservationId);
            if (slips > 0)
            {
                c.SettleOnly = false;
                c.Message = "มีสลิปที่ลูกค้าแนบรอตรวจ " + slips + " ใบ — ตรวจที่หน้า \"ตรวจสลิป\" (Account/SlipVerification) ก่อน: " +
                            "อนุมัติ = เงินเข้าแล้ว (คืนได้) / ปฏิเสธ = ไม่มีเงิน แล้วจึงยกเลิกการจอง";
                return c;
            }
            c.Allowed = true;
            if (c.SettleOnly)
                c.Message = "การจองถูกยกเลิกแล้ว (" + st + ") แต่ยังมีเงินรับไว้ — บันทึกคืน/ยึดเงินค้างได้ (ไม่เปลี่ยนสถานะการจองซ้ำ)";
            return c;
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

            try
            {
                var bal = ReservationBalance.Load(_conn, reservationId);
                if (bal != null) m.CollectMode = bal.CollectMode ?? "";
            }
            catch { }
            m.OtaRevenueRef = LoadOtaRevenueRef(reservationId);

            // Folio = มัดจำ / ผูก Payment_History ของการจอง (รับเงินผ่านหน้าการจอง) / มีบรรทัดค่าห้อง (ProductType 1)
            // Reclass = เงินสดปลอมของใบ OTA ที่ปรับเป็นลูกหนี้ OTA แล้ว (Payment_History OTA_RECLASS)
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT R.ID, R.Reservation_ID, R.Created_Date, R.Total_Amount, R.Vat, R.IsDeposit, R.Paid_Type,
                         ISNULL(R.Deposit_Applied_Amount, 0) AS DA, ISNULL(R.Status, N'Normal') AS St,
                         CAST(NULL AS NVARCHAR(200)) AS CustName,
                         CASE WHEN ISNULL(R.IsDeposit, 0) = 1
                                OR EXISTS (SELECT 1 FROM Payment_History ph WHERE ph.Receipt_ID = R.ID AND ISNULL(ph.PaymentType, '') <> 'REFUND')
                                OR EXISTS (SELECT 1 FROM Account_Receipt_Detail d WHERE d.Receipt_ID = R.ID AND TRY_CONVERT(INT, d.ProductType_ID) = 1)
                              THEN 1 ELSE 0 END AS Folio,
                         CASE WHEN EXISTS (SELECT 1 FROM Payment_History px WHERE px.Receipt_ID = R.ID AND px.Status = 'OTA_RECLASS')
                              THEN 1 ELSE 0 END AS Reclass
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
                if (Bool(r["Reclass"]) || m.OtaRevenueRef.Equals("RECLASS-" + s.Id, StringComparison.OrdinalIgnoreCase))
                {
                    m.Excluded.Add(s.Id + " " + s.Total.ToString("#,##0.00", Inv) + " — เงินที่ OTA เก็บ (ปรับเป็นลูกหนี้ OTA แล้ว) OTA เป็นผู้คืนลูกค้า");
                    continue;
                }
                s.IsFolio = Bool(r["Folio"]);
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
                     AND NOT EXISTS (SELECT 1 FROM Payment_History px WHERE px.Receipt_ID = R.ID AND px.Status = 'OTA_RECLASS')
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

        /// <summary>รัน UPDATE แล้วคืนจำนวนแถว (@@ROWCOUNT — ไม่พึ่งค่าคืนของ ExecuteNonQuery ที่เป็น −1 เมื่อเซิร์ฟเวอร์ตั้ง NOCOUNT)</summary>
        private int RowCount(string updateSql, Dictionary<string, object> ps)
        {
            var dt = _code.DatabaseQuerySafe(_conn, "SET NOCOUNT OFF; " + updateSql + " SELECT @@ROWCOUNT AS N;", ps);
            return dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value ? Convert.ToInt32(dt.Rows[0][0]) : 0;
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
            return CreateCore(req, fromCancellation, true, out message);
        }

        /// <param name="autoApprove">false = สร้างเป็นร่างเสมอ (ยกเลิกการจองสร้างทั้งชุดก่อน แล้วอนุมัติ/คืนผ่านเกตเวย์ทีหลังสุด)</param>
        private int CreateCore(Request req, bool fromCancellation, bool autoApprove, out string message)
        {
            message = null;
            if (!TableReady(_conn)) { message = "ยังไม่ได้รัน migration PHASE19_Migration_33_Credit_Notes.sql"; return -1; }
            if (req == null) { message = "ไม่มีข้อมูล"; return -1; }
            var src = LoadSource(req.SourceReceiptId);
            if (src == null) { message = "ไม่พบใบต้นทาง " + req.SourceReceiptId; return -1; }
            if (IsExcludedSource(src.Id)) { message = "ใบ " + src.Id + " เป็นเอกสารรับเงิน OTA — คืนเงินผ่านช่องทาง OTA (ออกใบลดหนี้จากหน้านี้ไม่ได้)"; return -1; }
            if (IsOtaReclassed(src.Id, src.ReservationId))
            { message = "ใบ " + src.Id + " เป็นเงินที่ OTA เก็บ (ปรับเป็นลูกหนี้ OTA แล้ว) — OTA เป็นผู้คืนลูกค้า โรงแรมออกใบลดหนี้/คืนเงินจากใบนี้ไม่ได้"; return -1; }
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
                // มัดจำที่ภาษีขายยังไม่เข้า ภ.พ.30 — CHECKOUT (ไม่แยก VAT) / RECEIPT+defer (พัก 21913) — ไม่มี VAT ให้ลด
                // ⇒ เอกสารเป็น "ใบคืนเงินมัดจำ/หลักฐานการคืนเงิน" ไม่ใช่ใบลดหนี้ (NextAcc refund-deposit ไม่ออกใบลดหนี้ในกรณีนี้)
                if (src.IsDeposit && (!cfg.IsDepositVatAtReceipt || cfg.IsDepositOutputVatDeferred)) vat = 0m;
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
                { "@notes", (object)Trunc(req.Notes, 1000) ?? DBNull.Value },
                // เพดานรวมของใบต้นทาง (ลดหนี้+ยึดทุกใบที่ยังไม่ยกเลิก): มัดจำ = ยอดใบ − ส่วนที่ถูกนำไปหักในใบอื่น · ใบกำกับ = ยอดใบ
                { "@cap", Math.Round(src.Creditable + src.Credited, 2, MidpointRounding.AwayFromZero) }
            };
            // บรรทัดอยู่ใน transaction เดียวกับหัวใบ (ล้มกลางทาง = ไม่มีใบครึ่ง ๆ)
            var lineSql = new StringBuilder();
            for (int i = 0; i < req.Lines.Count; i++)
            {
                var l = req.Lines[i];
                string k = "@l" + i.ToString(Inv) + "_";
                lineSql.Append(@"
                  INSERT INTO Credit_Note_Line (CN_ID, Line_No, Source_Line_No, Product_ID, ProductType_ID, Description, Qty, Unit,
                                               Unit_Price, Amount, Restock_Qty, Unit_Cost, Account_Code)
                  VALUES (@newId, ").Append(k).Append("no, ").Append(k).Append("sno, ").Append(k).Append("pid, ").Append(k).Append("pt, ")
                  .Append(k).Append("desc, ").Append(k).Append("qty, ").Append(k).Append("unit, ").Append(k).Append("price, ")
                  .Append(k).Append("amt, ").Append(k).Append("rq, ").Append(k).Append("cost, ").Append(k).Append("acc);");
                ps[k + "no"] = l.LineNo;
                ps[k + "sno"] = l.SourceLineNo.HasValue ? (object)l.SourceLineNo.Value : DBNull.Value;
                ps[k + "pid"] = l.ProductId.HasValue ? (object)l.ProductId.Value : DBNull.Value;
                ps[k + "pt"] = l.ProductTypeId.HasValue ? (object)l.ProductTypeId.Value : DBNull.Value;
                ps[k + "desc"] = (object)Trunc(l.Description, 500) ?? DBNull.Value;
                ps[k + "qty"] = l.Qty;
                ps[k + "unit"] = (object)Trunc(l.Unit, 30) ?? DBNull.Value;
                ps[k + "price"] = l.Qty > 0 ? Math.Round(l.Amount / l.Qty, 4) : l.Amount;
                ps[k + "amt"] = l.Amount;
                ps[k + "rq"] = l.RestockQty;
                ps[k + "cost"] = l.UnitCost.HasValue ? (object)l.UnitCost.Value : DBNull.Value;
                ps[k + "acc"] = (object)Trunc(l.AccountCode, 30) ?? DBNull.Value;
            }
            // เลขที่รันต่อเดือน CN{yyMM}-#### — UPDLOCK/HOLDLOCK ใน transaction เดียวกับ INSERT กันเลขซ้ำเมื่อกดพร้อมกัน
            // + ล็อกใบลดหนี้ของใบต้นทางเดียวกันแล้วตรวจเพดานซ้ำ (สองหน้าต่าง/กดซ้ำพร้อมกันผ่านการตรวจฝั่ง C# ได้ทั้งคู่)
            var ins = _code.DatabaseQuerySafe(_conn,
                @"SET NOCOUNT ON;
                  SET XACT_ABORT ON;
                  BEGIN TRAN;
                  DECLARE @used DECIMAL(18,2) = ISNULL((SELECT SUM(Amount_Gross + Retained_Amount) FROM Credit_Note WITH (UPDLOCK, HOLDLOCK)
                                                        WHERE Source_Receipt_ID = @src AND Status <> 'VOIDED'), 0);
                  IF @used + @gross + @ret > @cap + 0.01
                  BEGIN
                      ROLLBACK;
                      SELECT CAST(-1 AS INT) AS NewID, CAST(NULL AS NVARCHAR(30)) AS CnNo, @used AS Used;
                      RETURN;
                  END
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
                  DECLARE @newId INT = CAST(SCOPE_IDENTITY() AS INT);" + lineSql + @"
                  COMMIT;
                  SELECT @newId AS NewID, @no AS CnNo, @used AS Used;", ps);
            if (ins == null || ins.Rows.Count == 0 || ins.Rows[0]["NewID"] == DBNull.Value) { message = "บันทึกใบลดหนี้ไม่สำเร็จ"; return -1; }
            int id = Convert.ToInt32(ins.Rows[0]["NewID"]);
            if (id <= 0)
            {
                message = $"ใบ {src.Id} ถูกออกใบลดหนี้/ยึดไปแล้ว {Dec(ins.Rows[0]["Used"]):N2} จากเพดาน {src.Creditable + src.Credited:N2} " +
                          "(อาจมีการทำรายการซ้อนจากอีกหน้าต่าง) — โหลดหน้าใหม่แล้วตรวจยอด";
                return -1;
            }
            string cnNo = Str(ins.Rows[0]["CnNo"]);

            _code.Logs(_conn, "CreditNote",
                $"สร้างใบลดหนี้ {cnNo} (#{id}) ต้นทาง {src.Id} [{src.DocType}] ยอด {gross:N2} VAT {vat:N2} คืน {refund:N2} ({method}) ยึด {retained:N2} โดย {user}", user);
            message = "สร้างใบลดหนี้ " + cnNo + " แล้ว";

            if (!autoApprove) return id;
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

            // 1) เกตเวย์ — จดยอดคืนสะสมของรายการเกตเวย์ "ก่อน" สั่งคืน (GWPRE:) แล้วตรวจจากยอดจริงเมื่อผลไม่ชัด
            //    กันคืนซ้ำ: เกตเวย์คืนสำเร็จแต่บันทึกฝั่งเราล้ม → ใบกลับเป็นร่าง → อนุมัติใหม่จะไม่สั่งคืนซ้ำ
            if (rec.RefundMethod == MGateway && rec.RefundAmount > 0 && rec.GatewayTxnId.HasValue)
            {
                string origRef;
                decimal before = ParseGwPre(rec.RefundRef, out origRef);
                decimal now = GatewayRefundedAmount(rec.GatewayTxnId.Value);
                string r;
                if (now < 0m)
                {
                    RevertToDraft(id, rec.RefundRef, "อ่านยอดคืนของรายการเกตเวย์ไม่ได้ — ยังไม่ได้สั่งคืน");
                    message = "อ่านยอดคืนของรายการเกตเวย์ #" + rec.GatewayTxnId.Value + " ไม่ได้ — ใบยังเป็นร่าง (ยังไม่ได้สั่งคืน)";
                    return false;
                }
                if (before >= 0m && now >= before + rec.RefundAmount - 0.01m)
                {
                    r = "คืนผ่านเกตเวย์สำเร็จ (ตรวจพบยอดคืนในรายการเกตเวย์แล้ว — ไม่สั่งซ้ำ)";
                }
                else if (before >= 0m)
                {
                    RevertToDraft(id, rec.RefundRef, "เคยสั่งคืนผ่านเกตเวย์แล้วแต่ไม่ได้รับผลยืนยัน");
                    message = "เคยสั่งคืนผ่านเกตเวย์ของใบนี้แล้วแต่ไม่ได้รับผลยืนยัน (ยอดคืนสะสมในรายการเกตเวย์ไม่เพิ่ม) — " +
                              "ตรวจที่แดชบอร์ดเกตเวย์ก่อน: ถ้าคืนแล้วจริง ให้ผู้ดูแลบันทึกยอดคืนในรายการเกตเวย์แล้วอนุมัติใหม่ / " +
                              "ถ้ายังไม่ได้คืน ให้ยกเลิกใบร่างนี้แล้วทำรายการใหม่";
                    return false;
                }
                else
                {
                    SetFields(id, "Refund_Ref = @m",
                        new Dictionary<string, object> { { "@m", Trunc(GwPrePrefix + now.ToString(Inv) + "|" + origRef, 200) } });
                    bool threw = false;
                    try
                    {
                        r = new Take_Time_BangPhra.Payments.OnlinePaymentService(_conn)
                            .RefundTransaction(rec.GatewayTxnId.Value, rec.RefundAmount, "ใบลดหนี้ " + rec.CnNo + " " + (rec.ReasonText ?? ""), CurrentAdminId());
                    }
                    catch (Exception ex) { r = "คืนเงินไม่สำเร็จ: " + ex.Message; threw = true; }
                    bool ok = r != null && r.IndexOf("สำเร็จ", StringComparison.Ordinal) >= 0 && r.IndexOf("ไม่สำเร็จ", StringComparison.Ordinal) < 0;
                    if (!ok)
                    {
                        decimal after = GatewayRefundedAmount(rec.GatewayTxnId.Value);
                        if (after >= 0m && after >= now + rec.RefundAmount - 0.01m)
                        {
                            ok = true;
                            r = "คืนผ่านเกตเวย์สำเร็จ (ตรวจพบยอดคืนแล้ว — " + (r ?? "") + ")";
                        }
                    }
                    if (!ok)
                    {
                        // เกตเวย์ตอบปฏิเสธชัดเจน (ไม่มีเงินออก) → ล้างเครื่องหมาย ลองใหม่ได้ · exception = ไม่รู้ผล → คงเครื่องหมายกันสั่งซ้ำ
                        RevertToDraft(id, threw ? GwPrePrefix + now.ToString(Inv) + "|" + origRef : origRef, "คืนผ่านเกตเวย์ไม่สำเร็จ: " + r);
                        message = "คืนผ่านเกตเวย์ไม่สำเร็จ — ใบยังเป็นร่าง: " + r;
                        return false;
                    }
                }
                SetFields(id, "Refund_Ref = @r",
                    new Dictionary<string, object> { { "@r", Trunc((origRef.Length > 0 ? origRef + " · " : "") + r, 200) } });
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
            // 5) ใบมัดจำของการยกเลิก: ตั้งสถานะ Refunded/Forfeit เมื่อใบลดหนี้ทุกใบของใบนั้นอนุมัติแล้ว (ร่าง = ใบยังปกติ ยกเลิกร่างแล้วทำใหม่ได้)
            //    เฉพาะชุดยกเลิกการจอง (Cancel_Batch) — คืนมัดจำส่วนเกินตอนเช็คเอาท์ (การจองยังอยู่) ต้องคงใบมัดจำปกติ
            //    ไม่งั้นยอดมัดจำที่เหลือหลุดจากการตัดมัดจำตอนเช็คเอาท์ (CheckoutService อ่านเฉพาะใบมัดจำ Normal)
            if (rec.SourceDocType == SrcDeposit && !string.IsNullOrWhiteSpace(rec.CancelBatch))
                MarkDepositReceiptIfSettled(rec.SourceReceiptId);

            _code.Logs(_conn, "CreditNote", $"อนุมัติใบลดหนี้ {rec.CnNo} (#{id}) โดย {user} — {string.Join(" · ", notes)}", user);
            message = "อนุมัติแล้ว · " + string.Join(" · ", notes.Where(x => !string.IsNullOrEmpty(x)));
            return true;
        }

        private const string GwPrePrefix = "GWPRE:";

        /// <summary>Refund_Ref = "GWPRE:{ยอดคืนสะสมก่อนสั่ง}|{อ้างอิงเดิม}" → คืนยอดก่อนสั่ง (−1 = ยังไม่เคยสั่ง) + อ้างอิงเดิม</summary>
        private static decimal ParseGwPre(string refundRef, out string original)
        {
            string s = refundRef ?? "";
            original = s;
            if (!s.StartsWith(GwPrePrefix, StringComparison.Ordinal)) return -1m;
            int bar = s.IndexOf('|');
            string num = bar > 0 ? s.Substring(GwPrePrefix.Length, bar - GwPrePrefix.Length) : s.Substring(GwPrePrefix.Length);
            original = bar > 0 ? s.Substring(bar + 1) : "";
            decimal d;
            return decimal.TryParse(num, NumberStyles.Any, Inv, out d) ? d : 0m;
        }

        /// <summary>อ้างอิงการคืนสำหรับแสดงผล (ตัดเครื่องหมายภายใน GWPRE: ออก)</summary>
        public static string DisplayRefundRef(string refundRef)
        {
            string orig;
            ParseGwPre(refundRef, out orig);
            return orig;
        }

        /// <summary>Payment_Transaction.Refunded_Amount (−1 = อ่านไม่ได้)</summary>
        private decimal GatewayRefundedAmount(int txnId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT ISNULL(Refunded_Amount, 0) AS R FROM Payment_Transaction WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", txnId } });
                return dt != null && dt.Rows.Count > 0 ? Dec(dt.Rows[0][0]) : -1m;
            }
            catch { return -1m; }
        }

        private void RevertToDraft(int id, string refundRef, string error)
        {
            SetFields(id, "Status = 'DRAFT', Approved_By = NULL, Approved_At = NULL, Sync_Error = @e, Refund_Ref = @rr",
                new Dictionary<string, object>
                {
                    { "@e", Trunc(error, 1000) ?? (object)DBNull.Value },
                    { "@rr", (object)Trunc(refundRef, 200) ?? DBNull.Value }
                });
        }

        /// <summary>
        /// ใบเสร็จมัดจำที่ถูกคืน/ยึดผ่านใบลดหนี้: ตั้ง 'Refunded' (มีคืน) / 'Forfeit' (ยึดทั้งหมด) เมื่อใบลดหนี้ทุกใบของใบนั้น "ไม่ใช่ร่าง"
        /// (ไม่ใช่ 'Cancel' — ไม่ void บน NextAcc) · ยังมีร่าง = คงใบปกติ (ยกเลิกร่าง → ยอดคืนได้กลับมา)
        /// </summary>
        private void MarkDepositReceiptIfSettled(string receiptId)
        {
            if (string.IsNullOrWhiteSpace(receiptId)) return;
            try
            {
                var c = _code.DatabaseQuerySafe(_conn,
                    @"SELECT COUNT(*) AS N, ISNULL(SUM(CASE WHEN Status = 'DRAFT' THEN 1 ELSE 0 END), 0) AS D,
                             ISNULL(SUM(Refund_Amount), 0) AS R, ISNULL(SUM(Retained_Amount), 0) AS K
                        FROM Credit_Note WHERE Source_Receipt_ID = @r AND Status <> 'VOIDED'",
                    new Dictionary<string, object> { { "@r", receiptId.Trim() } });
                if (c == null || c.Rows.Count == 0) return;
                int n = Convert.ToInt32(c.Rows[0]["N"]), d = Convert.ToInt32(c.Rows[0]["D"]);
                decimal rf = Dec(c.Rows[0]["R"]), kp = Dec(c.Rows[0]["K"]);
                if (n == 0 || d > 0 || (rf <= 0 && kp <= 0)) return;
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Account_Receipt SET Status = @st
                       WHERE ID = @r AND ISNULL(IsDeposit, 0) = 1 AND ISNULL(Status, N'Normal') IN (N'Normal', N'')",
                    new Dictionary<string, object> { { "@r", receiptId.Trim() }, { "@st", rf > 0 ? "Refunded" : "Forfeit" } });
            }
            catch { }
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
                   WHERE ID = @id AND Refund_Status = 'PENDING' AND Status IN ('APPROVED', 'SYNCED');
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
            return VoidCore(id, reason, true, out message);
        }

        /// <param name="restoreBooking">true = ใบของชุดยกเลิกการจองที่ "ตั้งสถานะการจองไปแล้ว" → คืนยอดที่รับไว้ (Reservation.Deposit)
        /// + ใบมัดจำกลับเป็นปกติ ⇒ ปิดเงินค้างใหม่ได้ · false = ถอยชุดที่ยังไม่ได้แตะการจอง (สร้างชุดไม่สำเร็จ)</param>
        private bool VoidCore(int id, string reason, bool restoreBooking, out string message)
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
                string warn = "";
                string origRef;
                decimal before = ParseGwPre(rec.RefundRef, out origRef);
                if (before >= 0m && rec.GatewayTxnId.HasValue)
                {
                    decimal now = GatewayRefundedAmount(rec.GatewayTxnId.Value);
                    if (now >= 0m && now >= before + rec.RefundAmount - 0.01m)
                    {
                        message = "ตรวจพบว่าเกตเวย์คืนเงินของใบนี้ไปแล้ว — ห้ามยกเลิก ให้กด \"อนุมัติ\" (ระบบจะไม่สั่งคืนซ้ำ แค่บันทึกบัญชี)";
                        return false;
                    }
                    warn = " · ⚠ ใบนี้เคยสั่งคืนผ่านเกตเวย์แต่ไม่ได้ผลยืนยัน — ตรวจแดชบอร์ดเกตเวย์ว่าไม่มีเงินออกจริงก่อนทำรายการใหม่";
                }
                // ชิงสถานะแบบ atomic — กันอนุมัติ/ยกเลิกพร้อมกันจากสองหน้าต่าง
                int n = RowCount(
                    @"UPDATE Credit_Note SET Status = 'VOIDED', Voided_By = @by, Voided_At = GETDATE(), Void_Reason = @r
                       WHERE ID = @id AND Status = 'DRAFT';",
                    new Dictionary<string, object> { { "@id", id }, { "@by", user }, { "@r", Trunc(reason, 500) } });
                if (n <= 0) { message = "สถานะใบเปลี่ยนไปแล้ว (อาจถูกอนุมัติ/ยกเลิกจากอีกหน้าต่าง) — โหลดหน้าใหม่"; return false; }
                string rb = restoreBooking ? RestoreCancelledBooking(rec) : "";
                message = "ยกเลิกใบร่างแล้ว" + rb + warn;
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
                // ยังไม่ได้ส่ง = marker ว่าง และสถานะไม่เปลี่ยนตั้งแต่อ่าน (กันคิว/อีกหน้าต่างทำงานแทรก)
                int n = RowCount(
                    @"UPDATE Credit_Note SET Status = 'VOIDED', Voided_By = @by, Voided_At = GETDATE(), Void_Reason = @r
                       WHERE ID = @id AND Status = @st AND ISNULL(Nexaacc_Marker, '') = '';",
                    new Dictionary<string, object> { { "@id", id }, { "@st", rec.Status }, { "@by", user }, { "@r", Trunc(reason, 500) } });
                if (n <= 0) { message = "สถานะใบเปลี่ยนไปแล้ว (อาจเริ่มส่ง NextAcc แล้ว) — โหลดหน้าใหม่แล้วลองอีกครั้ง"; return false; }
                UndoLocalEffects(rec);
                string rb = restoreBooking ? RestoreCancelledBooking(rec) : "";
                message = "ยกเลิกแล้ว (ยังไม่ได้ส่ง NextAcc)" + rb + (rec.Restock ? " · ⚠ สินค้าที่รับคืนเข้าสต๊อกไม่ถูกตัดออกอัตโนมัติ — ปรับที่หน้า สต๊อก" : "");
                return true;
            }
            SetFields(id, "Void_Reason = @r, Voided_By = @by", new Dictionary<string, object> { { "@by", user }, { "@r", Trunc(reason, 500) } });
            long q = sync.EnqueueVoidCreditNote(rec.Id, rec.CnNo, reason);
            if (q <= 0) { message = "เข้าคิวยกเลิกบน NextAcc ไม่สำเร็จ"; return false; }
            UndoLocalEffects(rec);
            string rb2 = restoreBooking ? RestoreCancelledBooking(rec) : "";
            message = "เข้าคิวยกเลิกใบลดหนี้บน NextAcc #" + q + " — สถานะจะเป็น \"ยกเลิกแล้ว\" เมื่อ NextAcc ยืนยัน" + rb2
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

        /// <summary>
        /// ยกเลิกใบลดหนี้ของชุดยกเลิกการจอง (Cancel_Batch) ที่ตั้งสถานะการจองไปแล้ว:
        ///   · Reservation.Deposit (= เงินที่โรงแรมยังถือ) บวกยอดคืนของใบนี้กลับ — เงินไม่ได้ออกจริง
        ///   · ใบมัดจำที่ถูกตั้ง Refunded/Forfeit กลับเป็นปกติ (ถ้าไม่มีใบลดหนี้อื่นค้างอยู่) ⇒ ยอดคืนได้กลับมา
        ///     ทำ "ยกเลิกการจอง + คืนเงิน" ใหม่ได้ (โหมดปิดเงินค้าง — การจองไม่ถูกเปลี่ยนสถานะซ้ำ)
        /// </summary>
        private string RestoreCancelledBooking(Record rec)
        {
            if (rec == null || string.IsNullOrWhiteSpace(rec.CancelBatch) || rec.ReservationId <= 0) return "";
            try
            {
                if (rec.RefundAmount > 0)
                    _code.DatabaseInsertSafe(_conn,
                        "UPDATE Reservation SET Deposit = ISNULL(Deposit, 0) + @r WHERE ID = @id AND [Status] LIKE N'ยกเลิก%'",
                        new Dictionary<string, object> { { "@id", rec.ReservationId }, { "@r", rec.RefundAmount } });
                if (rec.SourceDocType == SrcDeposit)
                    _code.DatabaseInsertSafe(_conn,
                        @"UPDATE Account_Receipt SET Status = N'Normal'
                           WHERE ID = @src AND Status IN (N'Refunded', N'Forfeit')
                             AND NOT EXISTS (SELECT 1 FROM Credit_Note WHERE Source_Receipt_ID = @src AND Status <> 'VOIDED' AND ID <> @cn)",
                        new Dictionary<string, object> { { "@src", rec.SourceReceiptId }, { "@cn", rec.Id } });
                return " · เงินของใบนี้กลับเป็นยอดค้างของการจอง #" + rec.ReservationId +
                       " — คืน/ยึดใหม่ได้ที่ \"ยกเลิกการจอง + คืนเงิน\" (ไม่เปลี่ยนสถานะการจองซ้ำ)";
            }
            catch (Exception ex) { return " · ⚠ คืนยอดค้างของการจองไม่สำเร็จ: " + ex.Message; }
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
            /// <summary>ใบที่ยังเป็นร่าง (รออนุมัติ / อนุมัติไม่สำเร็จ) — เงินยังไม่ออก</summary>
            public int DraftCount;
        }

        public CancelResult CancelReservation(int reservationId, decimal refundTotal, string retainedAs, string method,
            int? paidHowId, string refundRef, string slipPath, string reason)
        {
            return CancelReservation(reservationId, refundTotal, retainedAs, method, paidHowId, refundRef, slipPath, reason, false);
        }

        /// <summary>
        /// ยกเลิกการจองพร้อมคืนเงินบางส่วน/ทั้งหมด/ไม่คืน (แทนปุ่มยกเลิกเดิมเมื่อเปิดระบบใบลดหนี้):
        ///   แบ่งยอดคืนลงใบต้นทาง — มัดจำคงเหลือก่อน (ส่วนที่ไม่คืนของมัดจำ = ยึด → NextAcc realize-deposit ForfeitAs)
        ///   แล้วใบกำกับของ folio (ส่วนที่ไม่คืน = รายได้คงเดิม ไม่ออกใบลดหนี้ — audit C §7) · ใบอื่นที่แค่ผูกเลขจอง (รูมเซอร์วิส/POS)
        ///   รวมเฉพาะเมื่อ includeLinked
        /// ลำดับ (กันสถานะครึ่ง ๆ): ตรวจทั้งหมด → สร้างใบลดหนี้ทั้งชุดเป็น "ร่าง" (ล้ม = ยกเลิกร่างทั้งชุด ไม่มีผลอื่น) →
        ///   ตั้งสถานะการจอง/ล้างห้องใน transaction เดียวแบบชิงสถานะ (สถานะเปลี่ยนระหว่างทาง = ยกเลิกร่าง) →
        ///   ชาร์จค้าง/ลิงก์ชำระที่ค้าง → อนุมัติ (คืนผ่านเกตเวย์/เข้าคิว NextAcc ทีหลังสุด) — ต้องอนุมัติ = ร่างรอ ใบมัดจำยังปกติ
        /// การจองที่ยกเลิกไปแล้ว (เลื่อนวันแล้วยกเลิก / ร่างของชุดเดิมถูกยกเลิก) = โหมดปิดเงินค้าง ไม่เปลี่ยนสถานะ/ห้องซ้ำ
        /// **ไม่** ยิงคิวคืน/ริบ/void แบบเดิม (กันลงบัญชีซ้ำ)
        /// </summary>
        public CancelResult CancelReservation(int reservationId, decimal refundTotal, string retainedAs, string method,
            int? paidHowId, string refundRef, string slipPath, string reason, bool includeLinked)
        {
            var res = new CancelResult();
            if (!Enabled(_conn)) { res.Message = "ระบบใบลดหนี้ยังไม่เปิดใช้"; return res; }
            var money = LoadReservationMoney(reservationId);
            if (money == null) { res.Message = "ไม่พบการจอง"; return res; }
            var chk = CheckCancellable(money);
            if (!chk.Allowed) { res.Message = chk.Message; return res; }
            bool settle = chk.SettleOnly;

            var scope = money.Receipts.Where(x => x.Creditable > 0.005m && (includeLinked || x.IsFolio)).ToList();
            decimal total = scope.Sum(x => x.Creditable);
            refundTotal = Math.Round(refundTotal, 2, MidpointRounding.AwayFromZero);
            if (refundTotal < 0 || refundTotal > total + 0.01m) { res.Message = $"ยอดคืนต้องอยู่ระหว่าง 0 – {total:N2}"; return res; }
            if (settle && total <= 0.005m) { res.Message = "ไม่มีเงินค้างของการจองนี้ให้คืน/ยึด (ใบที่เหลือเป็นใบอื่นที่ผูกการจอง — ติ๊ก \"รวมใบอื่น\")"; return res; }
            method = (method ?? MNone).Trim().ToUpperInvariant();
            if (method != MTransfer && method != MCash && method != MGateway) method = MNone;
            if (refundTotal > 0.005m && method == MNone) { res.Message = "เลือกวิธีคืนเงิน"; return res; }
            if (refundTotal > 0.005m && method != MGateway && !paidHowId.HasValue) { res.Message = "เลือกแหล่งเงินที่จ่ายคืน"; return res; }
            if (refundTotal <= 0.005m) { refundTotal = 0m; method = MNone; }

            string batch = "CXL-" + reservationId + "-" + DateTime.Now.ToString("yyMMddHHmmss", Inv);
            decimal left = refundTotal;
            var reqs = new List<Request>();
            foreach (var s in scope.Where(x => x.IsDeposit))
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
            // ใบกำกับของ folio ก่อน แล้วจึงใบอื่นที่ผูกการจอง (เมื่อเลือกรวม)
            foreach (var s in scope.Where(x => !x.IsDeposit).OrderBy(x => x.IsFolio ? 0 : 1))
            {
                decimal r = Math.Min(s.Creditable, left);
                left -= r;
                if (r <= 0.005m) continue;
                var q = NewCancelRequest(money, s, r, 0m, null, method, paidHowId, refundRef, slipPath, reason, batch);
                AddProportionalLines(q, s, r);
                reqs.Add(q);
            }
            if (left > 0.01m) { res.Message = "แบ่งยอดคืนลงใบต้นทางไม่ครบ (คงเหลือ " + left.ToString("N2") + ")"; return res; }

            // ตรวจเกตเวย์/แหล่งเงินก่อนสร้างอะไรเลย (เดิมล้มกลางชุดหลังคืนเงินใบแรกไปแล้ว)
            if (method == MGateway)
                foreach (var q in reqs)
                {
                    if (q.Lines.Sum(l => l.Amount) <= 0) continue;
                    if (!FindGatewayTxn(q.SourceReceiptId).HasValue)
                    { res.Message = "ใบ " + q.SourceReceiptId + " ไม่ได้รับเงินผ่านเกตเวย์ — คืนผ่านเกตเวย์ไม่ได้ (เลือกโอน/เงินสด)"; return res; }
                    var src = scope.First(x => x.Id == q.SourceReceiptId);
                    if (!src.PaidHowId.HasValue)
                    { res.Message = "ใบ " + q.SourceReceiptId + " รับชำระด้วย \"" + src.PaidType + "\" ที่ไม่ตรงกับแหล่งเงินในระบบ — คืนผ่านเกตเวย์ไม่ได้ (เลือกโอน/เงินสด)"; return res; }
                }

            string oldStatus = money.Status ?? "";
            string tgHead = BuildCancelTelegramHead(reservationId, money);   // ก่อนลบแถวห้อง/ตั้งราคา 0

            // 1) สร้างใบลดหนี้ทั้งชุดเป็นร่าง (ยังไม่มีเงินออก/ไม่แตะสต๊อก/ไม่เข้าคิว)
            foreach (var q in reqs)
            {
                string m;
                int id = CreateCore(q, true, false, out m);
                if (id <= 0)
                {
                    VoidBatchDrafts(res.CreditNoteIds, "ยกเลิกอัตโนมัติ: สร้างชุดใบลดหนี้ไม่สำเร็จ");
                    res.CreditNoteIds.Clear();
                    res.Message = "สร้างใบลดหนี้ของ " + q.SourceReceiptId + " ไม่สำเร็จ (ยังไม่ได้ยกเลิกการจอง): " + m;
                    return res;
                }
                res.CreditNoteIds.Add(id);
            }

            // 2) การจอง — transaction เดียว + ชิงสถานะ (กดซ้ำ/สองหน้าต่าง/ปุ่มเดิมจากหน้าเก่า)
            decimal retained = total - refundTotal;
            decimal newDeposit = retained + (settle ? PriorRetained(reservationId, batch) : 0m);
            if (!ApplyReservationCancellation(reservationId, oldStatus, settle, refundTotal, newDeposit))
            {
                VoidBatchDrafts(res.CreditNoteIds, "ยกเลิกอัตโนมัติ: สถานะการจองเปลี่ยนระหว่างทำรายการ");
                res.CreditNoteIds.Clear();
                res.Message = "สถานะการจองเปลี่ยนระหว่างทำรายการ (อาจถูกยกเลิก/เช็คอินจากอีกหน้าต่าง) — ยังไม่ได้ทำอะไร โหลดหน้าใหม่แล้วตรวจอีกครั้ง";
                return res;
            }

            // 3) หลังบันทึกการจอง: ชาร์จค้าง (คืนสต๊อก/กลับ COGS) · ลิงก์ชำระเงินที่ค้าง · เงินประกัน (แจ้งเท่านั้น)
            var notes = new List<string>();
            if (!settle) CancelPendingCharges(reservationId);
            int links = CancelOpenPaymentLinks(reservationId);
            if (links > 0) notes.Add("ปิดลิงก์ชำระเงินที่ค้าง " + links + " รายการ");
            string hold = OpenSecurityHoldNote(reservationId);
            if (hold.Length > 0) notes.Add(hold);

            // 4) อนุมัติ — คืนผ่านเกตเวย์ + เข้าคิว NextAcc หลังสุด (ล้ม = ใบนั้นยังเป็นร่าง อนุมัติซ้ำได้ ไม่คืนซ้ำ)
            bool canApprove = !RequireApproval(_conn) || CanApproveCurrentUser();
            var cnNos = new List<string>();
            foreach (int id in res.CreditNoteIds)
            {
                string am = null;
                bool ok = canApprove && Approve(id, out am);
                var rec = Load(id);
                if (rec != null) cnNos.Add(rec.CnNo + (ok ? "" : " (ร่าง)"));
                if (!ok)
                {
                    res.DraftCount++;
                    if (canApprove) notes.Add("⚠ " + (rec != null ? rec.CnNo : "#" + id) + " อนุมัติไม่สำเร็จ: " + am);
                }
            }

            string payName = paidHowId.HasValue ? PaidHowName(paidHowId.Value) : "";
            res.Ok = true;
            res.Message = (settle ? $"ปิดเงินค้างของการจอง #{reservationId} (ยกเลิกไว้แล้ว)" : $"ยกเลิกการจอง #{reservationId}")
                          + (refundTotal > 0 ? $" คืนเงิน {refundTotal:N2} ({MethodLabel(method)}{(payName.Length > 0 ? " · " + payName : "")})" : " ไม่คืนเงิน")
                          + (retained > 0.005m ? $" · ยึด {retained:N2}" : "")
                          + (cnNos.Count > 0 ? " · ใบลดหนี้ " + string.Join(", ", cnNos) : " · ไม่มีเงินรับไว้")
                          + (res.DraftCount > 0 && !canApprove ? " · รออนุมัติ " + res.DraftCount + " ใบ (Owner/Admin ที่มีสิทธิ์บัญชี) — เงินยังไม่ออกจนกว่าจะอนุมัติ" : "")
                          + (notes.Count > 0 ? " · " + string.Join(" · ", notes) : "");

            try
            {
                string tg = (tgHead ?? ("🆔 การจอง: " + reservationId + "\n👤 ลูกค้า: " + (money.CustomerName ?? "-")))
                            + "\n💵 คืนเงิน: " + refundTotal.ToString("N2", Inv) + " บาท"
                            + (refundTotal > 0 ? " (" + MethodLabel(method) + (payName.Length > 0 ? " · " + payName : "") + ")" : "")
                            + (retained > 0.005m ? "\n🧾 ยึดไว้: " + retained.ToString("N2", Inv) + " บาท" : "")
                            + (cnNos.Count > 0 ? "\n📄 ใบลดหนี้/ใบคืนเงิน: " + string.Join(", ", cnNos) : "")
                            + (res.DraftCount > 0 ? "\n⏳ รออนุมัติ " + res.DraftCount + " ใบ — เงินยังไม่ออกจนกว่าจะอนุมัติ" : "")
                            + (hold.Length > 0 ? "\n🔒 " + hold : "")
                            + (string.IsNullOrWhiteSpace(reason) ? "" : "\n📝 " + reason);
                string title = settle ? "💼 *ปิดเงินค้างของการจองที่ยกเลิกแล้ว (ใบลดหนี้)*"
                             : refundTotal > 0 ? "✅ *ยกเลิกการจอง (คืนเงิน — ใบลดหนี้)*" : "❌ *ยกเลิกการจอง (ไม่คืนเงิน — ใบลดหนี้)*";
                Notify.Send(Notify.Ev.BookingCancel, title + "\n\n" + tg);
            }
            catch { }
            _code.Logs(_conn, "CreditNote",
                $"{(settle ? "ปิดเงินค้าง" : "ยกเลิกการจอง")} #{reservationId} ผ่านใบลดหนี้: คืน {refundTotal:N2} ยึด {retained:N2} ร่าง {res.DraftCount} · {string.Join(", ", cnNos)}",
                CurrentUser());
            return res;
        }

        private void VoidBatchDrafts(List<int> ids, string reason)
        {
            foreach (int made in ids)
            {
                string vm;
                try { VoidCore(made, reason, false, out vm); } catch { }
            }
        }

        /// <summary>ยอดยึดของใบลดหนี้ชุดก่อน ๆ ของการจองนี้ (ยังไม่ยกเลิก) — โหมดปิดเงินค้างต้องไม่ทับยอดที่ยึดไว้แล้ว</summary>
        private decimal PriorRetained(int reservationId, string currentBatch)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT ISNULL(SUM(Retained_Amount), 0) FROM Credit_Note
                       WHERE Reservation_ID = @id AND Status <> 'VOIDED' AND ISNULL(Cancel_Batch, '') <> @b",
                    new Dictionary<string, object> { { "@id", reservationId }, { "@b", currentBatch ?? "" } });
                return dt != null && dt.Rows.Count > 0 ? Dec(dt.Rows[0][0]) : 0m;
            }
            catch { return 0m; }
        }

        private string PaidHowName(int id)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, "SELECT TOP 1 Paid_How FROM Account_Paid_How WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                return dt != null && dt.Rows.Count > 0 ? Str(dt.Rows[0][0]).Trim() : "";
            }
            catch { return ""; }
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

        /// <summary>
        /// ตั้งสถานะการจองที่ยกเลิก (หรือปิดเงินค้างของใบที่ยกเลิกแล้ว) ใน transaction เดียว:
        ///   ชิงสถานะ (WHERE Status = สถานะที่อ่านตอนเริ่ม) → TotalPrice = 0 / Deposit = เงินที่โรงแรมยังถือ / สถานะ →
        ///   Payment_History รับเงิน → CANCELLED, คืนเงิน → REFUNDED → ลบแถวห้อง/รายการ (เหมือนปุ่มเดิม)
        /// false = สถานะเปลี่ยนไปแล้ว (ไม่มีอะไรถูกแก้)
        /// ใบมัดจำ → Refunded/Forfeit ตอนใบลดหนี้ของใบนั้นอนุมัติ (MarkDepositReceiptIfSettled) ไม่ใช่ตรงนี้
        /// </summary>
        private bool ApplyReservationCancellation(int resId, string oldStatus, bool settle, decimal refunded, decimal newDeposit)
        {
            string status = refunded > 0.005m ? "ยกเลิกคืนเงิน" : "ยกเลิกไม่คืนเงิน";
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SET NOCOUNT ON;
                  SET XACT_ABORT ON;
                  DECLARE @n INT = 0;
                  BEGIN TRAN;
                  IF @settle = 1
                      UPDATE Reservation SET Deposit = @dep
                       WHERE ID = @id AND ISNULL([Status], N'') = @old;
                  ELSE
                      UPDATE Reservation SET TotalPrice = 0, Deposit = @dep, [Status] = @st
                       WHERE ID = @id AND ISNULL([Status], N'') = @old AND ISNULL([Status], N'') NOT LIKE N'ยกเลิก%';
                  SET @n = @@ROWCOUNT;
                  IF @n = 0
                  BEGIN
                      ROLLBACK;
                      SELECT 0 AS N;
                      RETURN;
                  END
                  UPDATE Payment_History SET Status = 'CANCELLED', Notes = @note
                   WHERE Reservation_ID = @id AND Status = 'COMPLETED' AND ISNULL(PaymentType, '') <> 'REFUND';
                  UPDATE Payment_History SET Status = 'REFUNDED'
                   WHERE Reservation_ID = @id AND PaymentType = 'REFUND' AND Status = 'COMPLETED';
                  IF @settle = 0
                  BEGIN
                      DELETE FROM Reservation_Accommodation WHERE Reservation_ID = @id;
                      DELETE FROM Reservation_Items WHERE Reservation_ID = @id;
                  END
                  COMMIT;
                  SELECT @n AS N;",
                new Dictionary<string, object>
                {
                    { "@id", resId }, { "@old", oldStatus ?? "" }, { "@settle", settle ? 1 : 0 },
                    { "@dep", Math.Max(0m, Math.Round(newDeposit, 2, MidpointRounding.AwayFromZero)) }, { "@st", status },
                    { "@note", "ยกเลิกจากการยกเลิกการจอง (ใบลดหนี้) ID: " + resId }
                });
            return dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value && Convert.ToInt32(dt.Rows[0][0]) > 0;
        }

        /// <summary>ชาร์จเข้าห้องที่ค้าง (PENDING) — คืนสต๊อก + กลับต้นทุน (เหมือนปุ่มยกเลิกเดิม) · ทำหลัง commit การจอง</summary>
        private void CancelPendingCharges(int resId)
        {
            try
            {
                var pend = _code.DatabaseQuerySafe(_conn,
                    "SELECT ID FROM Reservation_Product_Charges WHERE Reservation_ID = @id AND Status = 'PENDING'",
                    new Dictionary<string, object> { { "@id", resId } });
                if (pend == null || pend.Rows.Count == 0) return;
                var svc = new Take_Time_BangPhra.RoomChargeService(_conn);
                foreach (DataRow r in pend.Rows)
                    try { svc.CancelRoomCharge(Convert.ToInt64(r["ID"]), CurrentAdminId(), "ยกเลิกการจอง #" + resId); } catch { }
            }
            catch { }
        }

        /// <summary>
        /// ลิงก์/QR ชำระเงินออนไลน์ของการจองที่ยังรอจ่าย → CANCELLED (หน้า Pay ไม่หยิบมาใช้ต่อ)
        /// ถ้าลูกค้าจ่ายที่เกตเวย์ทีหลังจริง webhook ยังตั้ง PAID ได้ (MarkPaid ไม่กรองสถานะ CANCELLED) — เงินไม่หาย แจ้งให้คืนตามปกติ
        /// </summary>
        private int CancelOpenPaymentLinks(int resId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"IF OBJECT_ID('dbo.Payment_Transaction','U') IS NOT NULL
                      BEGIN
                          UPDATE Payment_Transaction
                             SET [Status] = 'CANCELLED', Fail_Reason = N'การจองถูกยกเลิก', Updated_Date = GETDATE()
                           WHERE Source_Type = 'RESERVATION' AND Source_ID = @sid AND [Status] IN ('INITIATED', 'PENDING');
                          SELECT @@ROWCOUNT AS N;
                      END
                      ELSE SELECT 0 AS N;",
                    new Dictionary<string, object> { { "@sid", resId.ToString(Inv) } });
                return dt != null && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;
            }
            catch { return 0; }
        }

        /// <summary>เงินประกันความเสียหายที่ยังถืออยู่ — ไม่ขยับเงินอัตโนมัติ แค่เตือนให้คืน/ปล่อยวงเงินที่หน้าเงินประกัน</summary>
        private string OpenSecurityHoldNote(int resId)
        {
            try
            {
                var h = new Take_Time_BangPhra.Payments.SecurityHoldService(_conn).GetOpenHold(resId);
                if (h == null) return "";
                return "มีเงินประกันค้าง " + h.Amount.ToString("N2", Inv) + " บาท (" + (h.Provider ?? "-") + ", " + (h.HoldRef ?? "") +
                       ") — คืน/ปล่อยวงเงินที่หน้าเงินประกัน (ระบบไม่ขยับเงินอัตโนมัติ)";
            }
            catch { return ""; }
        }

        /// <summary>หัวข้อความแจ้งยกเลิก (เบอร์/ห้อง/วันที่/ราคารวม) — ต้องอ่าน "ก่อน" ลบแถวห้องและตั้งราคา 0 · ไม่พบ = null</summary>
        private string BuildCancelTelegramHead(int resId, ReservationMoney money)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT r.Customer_MobilePhone, c.Name, c.NickName, r.CheckinDate, r.CheckoutDate, r.StayDays, a.AccomName
                        FROM Reservation r
                        LEFT JOIN Customer c ON c.MobilePhone = r.Customer_MobilePhone
                        LEFT JOIN Reservation_Accommodation ra ON ra.Reservation_ID = r.ID
                        LEFT JOIN Accommodation a ON a.ID = ra.Accommodation_ID
                       WHERE r.ID = @id",
                    new Dictionary<string, object> { { "@id", resId } });
                if (dt == null || dt.Rows.Count == 0) return null;
                var r0 = dt.Rows[0];
                decimal total = 0m;
                try { var b = ReservationBalance.Load(_conn, resId); if (b != null) total = b.Total; } catch { }
                var rooms = new List<string>();
                foreach (DataRow r in dt.Rows) { string a = Str(r["AccomName"]).Trim(); if (a.Length > 0 && !rooms.Contains(a)) rooms.Add(a); }
                Func<object, string> D = o => o == null || o == DBNull.Value ? "-" : Convert.ToDateTime(o).ToString("dd MMMM yyyy");
                string name = Str(r0["Name"]).Trim();
                string nick = Str(r0["NickName"]).Trim();
                return "🆔 การจอง: " + resId
                       + "\n👤 ลูกค้า: " + (name.Length > 0 ? name + (nick.Length > 0 ? " (" + nick + ")" : "") : (money?.CustomerName ?? "-"))
                       + "\n📞 โทรศัพท์: " + Str(r0["Customer_MobilePhone"])
                       + "\n📅 เช็คอิน: " + D(r0["CheckinDate"]) + " · เช็คเอ้าท์: " + D(r0["CheckoutDate"])
                       + (r0["StayDays"] != DBNull.Value ? " (" + Str(r0["StayDays"]) + " คืน)" : "")
                       + (rooms.Count > 0 ? "\n🏨 ห้อง: " + string.Join(", ", rooms) : "")
                       + (total > 0 ? "\n💰 ราคารวม: " + total.ToString("N0", Inv) + " บาท" : "")
                       + "\n📌 สถานะเดิม: " + (money?.Status ?? "-");
            }
            catch { return null; }
        }

        // ── พิมพ์ (ใบลดหนี้/หลักฐานการคืนเงินของ TakeTime) ─────────────────────

        public string RenderPrintHtml(Record rec)
        {
            string bizName, bizTax, bizAddr;
            Take_Time_BangPhra.TaxReports.TaxReportKit.BusinessHeader(_conn, out bizName, out bizTax, out bizAddr);
            Func<string, string> H = HttpUtility.HtmlEncode;
            // มัดจำที่ VAT ยังไม่เข้า ภ.พ.30 (โหมด CHECKOUT / RECEIPT+defer → VAT ของใบ = 0) = ใบคืนเงินมัดจำ ไม่ใช่ใบลดหนี้ ม.86/10
            // มัดจำโหมด RECEIPT ไม่พัก (VAT เข้า ภ.พ.30 ตอนรับมัดจำ) = ใบลดหนี้มี VAT ตามเดิม
            bool depositNoVat = rec.SourceDocType == SrcDeposit && rec.VatAmount <= 0;
            bool taxCn = !depositNoVat && (rec.SourceDocType == SrcTaxInvoice || rec.VatAmount > 0);
            string title = taxCn ? "ใบลดหนี้ / Credit Note"
                         : depositNoVat ? "ใบคืนเงินมัดจำ / หลักฐานการคืนเงิน" : "หลักฐานการคืนเงิน / Refund Voucher";
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
            sb.Append("<table class='cnp-tb'><tr><th>#</th><th>รายการ</th><th style='text-align:right'>จำนวน</th><th style='text-align:right'>")
              .Append(taxCn ? "จำนวนเงิน (รวม VAT)" : "จำนวนเงิน").Append("</th></tr>");
            foreach (var l in rec.Lines)
                sb.Append("<tr><td>").Append(l.LineNo).Append("</td><td>").Append(H(l.Description)).Append("</td><td style='text-align:right'>")
                  .Append(l.Qty.ToString("#,##0.##", Inv)).Append(' ').Append(H(l.Unit)).Append("</td><td style='text-align:right'>")
                  .Append(l.Amount.ToString("#,##0.00", Inv)).Append("</td></tr>");
            sb.Append("</table>");
            sb.Append("<table class='cnp-sum'>");
            if (taxCn)
            {
                sb.Append("<tr><td>มูลค่าที่ลดลง (ไม่รวม VAT)</td><td>").Append((rec.AmountGross - rec.VatAmount).ToString("#,##0.00", Inv)).Append("</td></tr>");
                sb.Append("<tr><td>ภาษีมูลค่าเพิ่ม</td><td>").Append(rec.VatAmount.ToString("#,##0.00", Inv)).Append("</td></tr>");
                sb.Append("<tr><td><b>รวมลดหนี้</b></td><td><b>").Append(rec.AmountGross.ToString("#,##0.00", Inv)).Append("</b></td></tr>");
            }
            else
                sb.Append("<tr><td><b>").Append(depositNoVat ? "รวมเงินมัดจำที่คืน" : "รวมเงินที่คืน").Append("</b></td><td><b>")
                  .Append(rec.AmountGross.ToString("#,##0.00", Inv)).Append("</b></td></tr>");
            if (rec.RetainedAmount > 0)
                sb.Append("<tr><td>ยึดไว้เป็นค่าธรรมเนียมยกเลิก</td><td>").Append(rec.RetainedAmount.ToString("#,##0.00", Inv)).Append("</td></tr>");
            sb.Append("<tr><td>คืนเงิน (").Append(H(MethodLabel(rec.RefundMethod)))
              .Append(string.IsNullOrEmpty(rec.RefundPaidHowName) ? "" : " · " + H(rec.RefundPaidHowName)).Append(")</td><td>")
              .Append(rec.RefundAmount.ToString("#,##0.00", Inv)).Append("</td></tr>");
            sb.Append("</table>");
            string shownRef = DisplayRefundRef(rec.RefundRef);
            if (!string.IsNullOrWhiteSpace(shownRef)) sb.Append("<p>อ้างอิงการโอน/คืนเงิน: ").Append(H(shownRef)).Append("</p>");
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
