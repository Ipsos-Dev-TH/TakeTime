using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// จุดเชื่อมระบบใบลดหนี้ (PHASE19_33) กับส่วนอื่นของระบบ — อ่านตาราง Credit_Note ตรง ๆ (ไม่แตะ CreditNoteService)
    ///   • ด่านกันยกเลิก/ลบ/แก้ไข/ส่งซ้ำใบเสร็จที่มีใบลดหนี้อ้างอยู่ หรือส่ง e-Tax ถึงกรมสรรพากรแล้ว
    ///     (NextAcc ปฏิเสธการยกเลิกเอกสารที่มีใบลดหนี้อ้าง — Accounting/Helpers/DocumentVoidPreconditions.cs:21-29 ·
    ///      และเอกสารที่ e-Tax Submitted/Accepted — :91-94) ⇒ ถ้าปล่อยยกเลิกฝั่ง TakeTime สองระบบจะไม่ตรงกัน / รายได้กลับซ้ำ
    ///   • หัวข้อตรวจสุขภาพ (Admin → Accounting Integration → ตรวจสุขภาพ)
    ///   • แจ้งเตือน Telegram "ใบลดหนี้รออนุมัติ" แบบ poll จาก timer (Global.asax)
    ///   • ลิงก์ไปหน้าใบลดหนี้สำหรับการคืนเงินผ่านเกตเวย์ที่ทำนอกระบบใบลดหนี้
    /// ไม่มีตาราง / schema เก่า → ทุกเมธอดคืน "ไม่มีอะไรบล็อก" (พฤติกรรมเดิม)
    /// </summary>
    public static class CreditNoteGuards
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly object _lock = new object();
        private static DateTime _schemaAt = DateTime.MinValue;
        private static bool _schemaOk;

        /// <summary>ตาราง Credit_Note schema ปัจจุบัน (PHASE19_33: มี CN_No + Source_Receipt_ID) — cache 60 วินาที</summary>
        public static bool SchemaReady(string conn)
        {
            lock (_lock)
            {
                if ((DateTime.Now - _schemaAt).TotalSeconds < 60) return _schemaOk;
            }
            bool ok = false;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    @"SELECT CASE WHEN OBJECT_ID('dbo.Credit_Note','U') IS NOT NULL
                                   AND COL_LENGTH('dbo.Credit_Note','CN_No') IS NOT NULL
                                   AND COL_LENGTH('dbo.Credit_Note','Source_Receipt_ID') IS NOT NULL THEN 1 ELSE 0 END", null);
                ok = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 1;
            }
            catch { ok = false; }
            lock (_lock) { _schemaOk = ok; _schemaAt = DateTime.Now; }
            return ok;
        }

        private static bool Enabled(string conn)
        {
            try { return SchemaReady(conn) && CreditNoteService.Enabled(conn); } catch { return false; }
        }

        /// <summary>เลขใบลดหนี้ (ไม่นับ VOIDED) ที่อ้างใบเสร็จนี้</summary>
        public static List<string> ActiveForReceipt(string conn, string receiptId)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(receiptId) || !SchemaReady(conn)) return list;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT CN_No FROM Credit_Note WHERE Source_Receipt_ID = @r AND Status <> 'VOIDED' ORDER BY ID",
                    new Dictionary<string, object> { { "@r", receiptId.Trim() } });
                if (dt != null) foreach (DataRow r in dt.Rows) list.Add(Convert.ToString(r[0]));
            }
            catch { }
            return list;
        }

        /// <summary>เลขใบลดหนี้ (ไม่นับ VOIDED) ของการจองนี้</summary>
        public static List<string> ActiveForReservation(string conn, int reservationId)
        {
            var list = new List<string>();
            if (reservationId <= 0 || !SchemaReady(conn)) return list;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT CN_No FROM Credit_Note WHERE Reservation_ID = @id AND Status <> 'VOIDED' ORDER BY ID",
                    new Dictionary<string, object> { { "@id", reservationId } });
                if (dt != null) foreach (DataRow r in dt.Rows) list.Add(Convert.ToString(r[0]));
            }
            catch { }
            return list;
        }

        /// <summary>
        /// สถานะ e-Tax ล่าสุดของใบเสร็จ ถ้า "ถึงกรมสรรพากรแล้ว" (Submitted/Accepted — NextAcc EtaxStatus 2/3,
        /// Models/Enums/AllEnums.cs:1305-1314) — ไม่ถึง/ไม่มี/ยกเลิกแล้ว = null
        /// </summary>
        /// <summary>เลขใบกำกับเต็มรูปที่ออกแทนใบเสร็จนี้ (Nexaacc_Doc_Action PHASE19_41 — ข้อ 25) · null = ไม่มี/ไม่มีตาราง</summary>
        public static string FullTaxInvoiceReplacementOf(string conn, string receiptId)
        {
            if (string.IsNullOrWhiteSpace(receiptId)) return null;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    @"IF OBJECT_ID('dbo.Nexaacc_Doc_Action','U') IS NOT NULL
                        SELECT TOP 1 ISNULL(Result_Doc_No, CAST(Result_Doc_Id AS NVARCHAR(40))) AS DocNo
                          FROM dbo.Nexaacc_Doc_Action
                         WHERE Receipt_ID = @r AND Action = 'FULL_TAX_INVOICE' AND Status = 'DONE'
                           AND Path IN ('REPLACEMENT', 'APPROVE_DRAFT', 'ALREADY') AND Result_Doc_Id IS NOT NULL
                         ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@r", receiptId.Trim() } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value) return Convert.ToString(dt.Rows[0][0]);
            }
            catch { }
            return null;
        }

        public static string EtaxReachedRd(string conn, string receiptId)
        {
            if (string.IsNullOrWhiteSpace(receiptId)) return null;
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    @"IF OBJECT_ID('dbo.Accounting_ETax_Log','U') IS NOT NULL
                        SELECT TOP 1 ISNULL(Status, '') AS St, ISNULL(Etax_Ref_Number, '') AS RefNo,
                               CASE WHEN Submitted_Date IS NULL THEN 0 ELSE 1 END AS Sub
                          FROM Accounting_ETax_Log
                         WHERE Receipt_Number = @r AND Nexaacc_Etax_Id IS NOT NULL
                         ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@r", receiptId.Trim() } });
                if (dt == null || dt.Rows.Count == 0) return null;
                string st = Convert.ToString(dt.Rows[0]["St"]).Trim().ToUpperInvariant();
                bool sub = Convert.ToInt32(dt.Rows[0]["Sub"]) == 1;
                bool dead = st == "VOIDED" || st == "6" || st == "REJECTED" || st == "4" || st == "ERROR" || st == "5" || st == "FAILED";
                if (st == "SUBMITTED" || st == "2" || st == "ACCEPTED" || st == "3" || (sub && !dead))
                {
                    string refNo = Convert.ToString(dt.Rows[0]["RefNo"]);
                    return (st.Length > 0 ? st : "SUBMITTED") + (refNo.Length > 0 ? " · " + refNo : "");
                }
            }
            catch { }
            return null;
        }

        /// <summary>ลิงก์ออกใบลดหนี้จากใบเสร็จนี้ (หน้า CreditNotes รองรับ ?new=1&amp;src=)</summary>
        public static string CreditNoteUrlForReceipt(string receiptId)
            => "/Account/CreditNotes?new=1&src=" + Uri.EscapeDataString((receiptId ?? "").Trim());

        /// <summary>ลิงก์รายการใบลดหนี้ที่ค้นด้วยคำนี้ (หน้า CreditNotes รองรับ ?q=)</summary>
        public static string CreditNoteListUrl(string q)
            => "/Account/CreditNotes" + (string.IsNullOrWhiteSpace(q) ? "" : "?q=" + Uri.EscapeDataString(q.Trim()));

        /// <summary>
        /// เหตุที่ "ยกเลิก / ลบ / แก้ไข (void→สร้างใหม่) / ส่งซ้ำ" ใบเสร็จนี้ไม่ได้ — null = ทำได้ (พฤติกรรมเดิม)
        ///   1) มีใบลดหนี้ที่ยังไม่ยกเลิกอ้างอยู่ (ตรวจเมื่อมีตาราง PHASE19_33 ไม่ขึ้นกับสวิตช์ — ใบลดหนี้มีอยู่จริงแล้ว)
        ///   2) checkEtax และเปิดระบบใบลดหนี้: e-Tax ของใบนี้ถึงกรมสรรพากรแล้ว ⇒ ต้องออกใบลดหนี้แทนการยกเลิก
        ///      (ปิดระบบใบลดหนี้ = ไม่มีทางเลือกอื่นให้ ⇒ ไม่บล็อก ตามพฤติกรรมเดิม)
        /// </summary>
        public static string VoidBlockReason(string conn, string receiptId, bool checkEtax = true)
        {
            if (string.IsNullOrWhiteSpace(receiptId)) return null;
            string id = receiptId.Trim();
            try
            {
                var cns = ActiveForReceipt(conn, id);
                if (cns.Count > 0)
                    return "ใบ " + id + " มีใบลดหนี้ " + string.Join(", ", cns) + " อ้างอยู่ — ยกเลิก/ลบ/แก้ไข/ส่งซ้ำใบนี้ไม่ได้ "
                         + "(NextAcc ปฏิเสธการยกเลิกเอกสารที่มีใบลดหนี้อ้าง ม.86/10 และยอดที่ลดหนี้ไปแล้วจะถูกกลับซ้ำ) · "
                         + "ต้องการยกเลิกทั้งใบ ให้ออกใบลดหนี้ส่วนที่เหลือที่หน้า ใบลดหนี้ / คืนเงินลูกค้า ("
                         + CreditNoteUrlForReceipt(id) + ")";
                // ข้อ 25: ออกใบกำกับเต็มรูป "แทน" ใบนี้แล้ว (issue-full-tax-invoice) — ใบแทนอ้างเอกสารเดิม (RelatedDocumentId) และรายงานภาษีขายนับใบแทน ⇒
                // ยกเลิก/แก้ใบเดิมจากระบบนี้ = ลูกค้าถือใบกำกับของเอกสารที่ถูกยกเลิก · ต้องให้ผู้ทำบัญชีจัดการบน NextAcc ก่อน
                string rep = FullTaxInvoiceReplacementOf(conn, id);
                if (rep != null)
                    return "ใบ " + id + " ออกใบกำกับภาษีเต็มรูป " + rep + " แทนไปแล้ว — ยกเลิก/ลบ/แก้ไข/ส่งซ้ำใบนี้ไม่ได้ · "
                         + "ถ้าต้องแก้ ให้ผู้ทำบัญชียกเลิกใบแทนบน NextAcc ก่อน หรือออกใบลดหนี้อ้างใบแทน (" + CreditNoteUrlForReceipt(id) + ")";
                if (checkEtax && Enabled(conn))
                {
                    string et = EtaxReachedRd(conn, id);
                    if (et != null)
                        return "ใบ " + id + " ส่ง e-Tax ถึงกรมสรรพากรแล้ว (" + et + ") — ยกเลิก/แก้ไขทับไม่ได้ (NextAcc ปฏิเสธ) · "
                             + "ให้ออกใบลดหนี้เต็มจำนวน (ม.86/10) ที่หน้า ใบลดหนี้ / คืนเงินลูกค้า (" + CreditNoteUrlForReceipt(id) + ") แทน";
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// เหตุที่ปุ่มลงบัญชีมัดจำแบบเดิม (คืน/ริบ/รีเซ็ตทั้งการจอง) ใช้กับการจองนี้ไม่ได้ เพราะมีใบลดหนี้อยู่แล้ว
        /// (ยิงซ้ำ = คืน/ริบมัดจำสองรอบบน NextAcc) — null = ทำได้
        /// </summary>
        public static string ReservationBlockReason(string conn, int reservationId)
        {
            var cns = ActiveForReservation(conn, reservationId);
            if (cns.Count == 0) return null;
            return "การจอง #" + reservationId + " มีใบลดหนี้ " + string.Join(", ", cns)
                 + " อยู่แล้ว — ปุ่มนี้ลงบัญชีมัดจำ/กลับรายการแบบเดิมซึ่งจะซ้ำกับใบลดหนี้ · จัดการผ่านหน้า ใบลดหนี้ / คืนเงินลูกค้า ("
                 + CreditNoteListUrl(reservationId.ToString(Inv)) + ")";
        }

        // ── การคืนเงินผ่านเกตเวย์ที่ไม่ได้ทำผ่านใบลดหนี้ ───────────────────────────────

        /// <summary>
        /// ลิงก์ออกใบลดหนี้ให้ตรงกับรายการเกตเวย์ที่คืนเงินไปแล้ว:
        ///   ใบเสร็จต้นทางที่ไม่ใช่มัดจำ → ?new=1&amp;src={ใบ} · มัดจำ/ไม่รู้ใบ → รายการใบลดหนี้ค้นด้วยเลขการจอง/ใบ
        /// </summary>
        public static string GatewayRefundCnUrl(string conn, string receiptId, string sourceType, string sourceId)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(receiptId))
                {
                    var dt = new code().DatabaseQuerySafe(conn,
                        "SELECT TOP 1 ISNULL(CAST(IsDeposit AS INT), 0) AS D, Reservation_ID FROM Account_Receipt WHERE ID = @id",
                        new Dictionary<string, object> { { "@id", receiptId.Trim() } });
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        if (Convert.ToInt32(dt.Rows[0]["D"]) == 0) return CreditNoteUrlForReceipt(receiptId);
                        if (dt.Rows[0]["Reservation_ID"] != DBNull.Value) return CreditNoteListUrl(Convert.ToString(dt.Rows[0]["Reservation_ID"]));
                    }
                }
            }
            catch { }
            return CreditNoteListUrl(!string.IsNullOrWhiteSpace(sourceId) ? sourceId : receiptId);
        }

        /// <summary>ข้อความแนะนำหลังคืนเงินผ่านเกตเวย์ (หน้า Admin เกตเวย์ / webhook) — null = ไม่ต้องทำอะไรเพิ่ม</summary>
        public static string GatewayRefundFollowUp(string conn, bool appliedToSystem)
        {
            if (!appliedToSystem) return null;
            if (!Enabled(conn))
                return "รายการนี้ถูกบันทึกเข้าระบบแล้ว — ปรับใบเสร็จ/ยอดการจองให้ตรงตามขั้นตอนเดิม";
            return "รายการนี้ถูกบันทึกเข้าระบบแล้ว — ต้องออกใบลดหนี้ให้ตรงกับเงินที่คืน: เลือก \"ยังไม่คืน / คืนภายหลัง\" "
                 + "แล้วกด \"บันทึกการคืนเงิน\" โดยเลือกแหล่งเงินของเกตเวย์ (เงินออกไปแล้ว ห้ามเลือก \"คืนผ่านเกตเวย์\" ซ้ำ)";
        }

        /// <summary>SQL รายการเกตเวย์ที่คืนเงินแล้ว (ลงระบบแล้ว) แต่ยังไม่มีใบลดหนี้ที่เกี่ยวข้อง</summary>
        private const string GatewayRefundsWithoutCnSql =
            @"IF OBJECT_ID('dbo.Payment_Transaction','U') IS NOT NULL AND COL_LENGTH('dbo.Payment_Transaction','Refunded_Amount') IS NOT NULL
                SELECT TOP 20 t.ID, t.Txn_Ref, t.Source_Type, t.Source_ID, t.Receipt_ID, t.Refunded_Amount, t.Updated_Date
                  FROM Payment_Transaction t
                 WHERE ISNULL(t.Refunded_Amount, 0) > 0 AND t.Applied_At IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Credit_Note cn
                                    WHERE cn.Status <> 'VOIDED'
                                      AND (cn.Gateway_Txn_ID = t.ID
                                           OR (t.Receipt_ID IS NOT NULL AND cn.Source_Receipt_ID = t.Receipt_ID)
                                           OR (t.Source_Type = 'RESERVATION' AND cn.Reservation_ID = TRY_CONVERT(INT, t.Source_ID))))
                 ORDER BY t.ID DESC";

        // ── ตรวจสุขภาพ ───────────────────────────────────────────────────────────

        /// <summary>หัวข้อ "ใบลดหนี้" ของปุ่มตรวจสุขภาพ — add(level, title, detail) · ไม่ throw</summary>
        public static void AppendHealthIssues(string conn, bool haveCoa, Action<string, string, string> add)
        {
            var c = new code();
            try
            {
                if (!SchemaReady(conn))
                {
                    bool legacy = false;
                    try
                    {
                        var lg = c.DatabaseQuerySafe(conn,
                            "SELECT CASE WHEN OBJECT_ID('dbo.Credit_Note','U') IS NOT NULL AND COL_LENGTH('dbo.Credit_Note','CN_No') IS NULL THEN 1 ELSE 0 END", null);
                        legacy = lg != null && lg.Rows.Count > 0 && Convert.ToInt32(lg.Rows[0][0]) == 1;
                    }
                    catch { }
                    if (legacy)
                        add("error", "ใบลดหนี้: ตาราง Credit_Note เป็นโครงสร้างรุ่นเก่า",
                            "พบตาราง Credit_Note ที่ไม่มีคอลัมน์ CN_No (schema ของโค้ดเก่าที่เลิกใช้แล้ว) — ระบบใบลดหนี้ใช้งานไม่ได้\n"
                            + "รัน Database/PHASE19_Migration_33_Credit_Notes.sql รุ่นล่าสุด: จะเปลี่ยนชื่อตารางเก่าเป็น Credit_Note_Legacy_yyyyMMdd (ข้อมูลอยู่ครบ) แล้วสร้างตารางใหม่");
                    else
                        add("info", "ใบลดหนี้: ยังไม่ได้รัน PHASE19_33",
                            "ระบบใบลดหนี้/คืนเงินลูกค้ายังไม่เปิด — ปุ่มยกเลิกการจองแบบเดิมทำงานตามปกติ");
                    return;
                }

                bool on = CreditNoteService.Enabled(conn);
                bool appr = CreditNoteService.RequireApproval(conn);
                add("info", "ใบลดหนี้: " + (on ? "เปิดใช้" : "ปิดอยู่") + (on ? (appr ? " · ต้องอนุมัติ" : " · ไม่ต้องอนุมัติ") : ""),
                    "ตั้งได้ที่หัวข้อ \"ใบลดหนี้ / คืนเงินลูกค้า\" ในหน้านี้ (CreditNote_Enabled / CreditNote_Require_Approval)");

                // ROOM_AR — ใบลดหนี้ที่ยังไม่คืนเงิน/เส้นสำรองต้องพักเครดิตลูกค้าที่บัญชีนี้ (ไม่ผูก = คิวล้ม)
                try
                {
                    var ar = c.DatabaseQuerySafe(conn,
                        @"SELECT TOP 1 m.Nexaacc_AccountCode,
                                 CASE WHEN OBJECT_ID('dbo.Accounting_Nexaacc_Accounts','U') IS NOT NULL
                                       AND EXISTS (SELECT 1 FROM Accounting_Nexaacc_Accounts a WHERE a.Account_Code = m.Nexaacc_AccountCode)
                                      THEN 1 ELSE 0 END AS CodeExists
                            FROM Accounting_Account_Mapping m
                           WHERE m.TakeTime_Code = 'ROOM_AR' AND ISNULL(m.Is_Active, 1) = 1 AND ISNULL(m.Nexaacc_AccountCode, '') <> ''", null);
                    if (ar == null || ar.Rows.Count == 0)
                        add(on ? "error" : "warn", "ใบลดหนี้: ยังไม่ผูก mapping ROOM_AR (ลูกหนี้/เครดิตลูกค้า)",
                            "ใบลดหนี้ที่ยังไม่คืนเงิน และเส้นสำรอง /integration/credit-notes ต้องพักเครดิตลูกค้าที่บัญชีนี้ — ไม่ผูก = คิว CREATE_CREDIT_NOTE ล้ม\n"
                            + "ผูก ROOM_AR กับบัญชีลูกหนี้การค้าใน mapping ด้านล่างของหน้านี้");
                    else if (haveCoa && Convert.ToInt32(ar.Rows[0]["CodeExists"]) == 0)
                        add("error", "ใบลดหนี้: ROOM_AR ชี้รหัสบัญชีที่ NextAcc ไม่มี (" + ar.Rows[0]["Nexaacc_AccountCode"] + ")",
                            "กด Sync บัญชี แล้วตรวจอีกครั้ง — ยังไม่หายให้แก้ mapping ROOM_AR");
                }
                catch { /* ไม่มีตาราง mapping */ }

                // แหล่งเงินที่ใช้คืนเงินในใบลดหนี้ที่ยังไม่ส่ง NextAcc แต่ยังไม่ผูกบัญชี
                try
                {
                    var ph = c.DatabaseQuerySafe(conn,
                        @"SELECT PH.Paid_How, COUNT(*) AS N
                            FROM Credit_Note CN JOIN Account_Paid_How PH ON PH.ID = CN.Refund_Paid_How_ID
                           WHERE CN.Status IN ('DRAFT', 'APPROVED', 'FAILED')
                             AND ISNULL(CAST(PH.Nexaacc_AccountId AS NVARCHAR(50)), '') = ''
                           GROUP BY PH.Paid_How", null);
                    if (ph != null && ph.Rows.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (DataRow r in ph.Rows) names.Add(r["Paid_How"] + " (" + r["N"] + " ใบ)");
                        add("error", "ใบลดหนี้: แหล่งเงินคืนยังไม่ผูกบัญชี NextAcc",
                            string.Join(", ", names) + "\n\nเงินคืนจะลงบัญชีผิด/คิวล้ม — ผูกบัญชีที่หัวข้อ 'แหล่งเงิน' ในหน้านี้แล้วกด Retry ที่คิว");
                    }
                }
                catch { }

                // ร่างค้างอนุมัติเกิน 24 ชม.
                try
                {
                    var dr = c.DatabaseQuerySafe(conn,
                        @"SELECT TOP 10 CN_No, Created_At, Amount_Gross, ISNULL(Created_By, '') AS By_
                            FROM Credit_Note WHERE Status = 'DRAFT' AND Created_At < DATEADD(HOUR, -24, GETDATE())
                           ORDER BY Created_At", null);
                    if (dr != null && dr.Rows.Count > 0)
                    {
                        var lines = new List<string>();
                        foreach (DataRow r in dr.Rows)
                            lines.Add(r["CN_No"] + " · " + Convert.ToDateTime(r["Created_At"]).ToString("dd/MM HH:mm", Inv)
                                      + " · " + Convert.ToDecimal(r["Amount_Gross"]).ToString("#,##0.00", Inv) + " · โดย " + r["By_"]);
                        add("warn", "ใบลดหนี้รออนุมัติเกิน 24 ชั่วโมง (" + dr.Rows.Count + (dr.Rows.Count >= 10 ? "+" : "") + " ใบ)",
                            string.Join("\n", lines) + "\n\nอนุมัติ/ยกเลิกที่หน้า การเงิน & บัญชี → ใบลดหนี้ (Owner / Admin ที่มีสิทธิ์บัญชี) — "
                            + "ระหว่างนี้ยังไม่คืนเงิน/ไม่ลงบัญชี");
                    }
                }
                catch { }

                // คิวใบลดหนี้ล้ม/ค้าง + ใบที่อนุมัติแล้วแต่ส่งไม่ผ่าน
                try
                {
                    const string qSql =
                        @"SELECT
                            SUM(CASE WHEN Status = 'FAILED' THEN 1 ELSE 0 END) AS F,
                            SUM(CASE WHEN Status = 'PROCESSING' AND {0} < DATEADD(MINUTE, -30, GETDATE()) THEN 1 ELSE 0 END) AS P,
                            SUM(CASE WHEN Status = 'PENDING' AND Created_Date < DATEADD(HOUR, -6, GETDATE()) THEN 1 ELSE 0 END) AS S
                          FROM Accounting_Sync_Queue WHERE Entity_Type = 'CREDIT_NOTE'";
                    DataTable q;
                    try { q = c.DatabaseQuerySafe(conn, string.Format(qSql, "ISNULL(Processing_Started, Created_Date)"), null); }
                    catch { q = c.DatabaseQuerySafe(conn, string.Format(qSql, "Created_Date"), null); }   // ยังไม่รัน PHASE18_31
                    int f = 0, p = 0, s = 0;
                    if (q != null && q.Rows.Count > 0)
                    {
                        f = q.Rows[0]["F"] == DBNull.Value ? 0 : Convert.ToInt32(q.Rows[0]["F"]);
                        p = q.Rows[0]["P"] == DBNull.Value ? 0 : Convert.ToInt32(q.Rows[0]["P"]);
                        s = q.Rows[0]["S"] == DBNull.Value ? 0 : Convert.ToInt32(q.Rows[0]["S"]);
                    }
                    var se = c.DatabaseQuerySafe(conn,
                        @"SELECT TOP 10 CN_No, LEFT(ISNULL(Sync_Error, ''), 160) AS E FROM Credit_Note
                           WHERE Status IN ('APPROVED', 'FAILED') AND ISNULL(Sync_Error, '') <> '' ORDER BY ID DESC", null);
                    if (f + p + s > 0 || (se != null && se.Rows.Count > 0))
                    {
                        var sb = new StringBuilder();
                        if (f > 0) sb.Append("คิวล้มเหลว ").Append(f).Append(" รายการ · ");
                        if (p > 0) sb.Append("ค้าง PROCESSING เกิน 30 นาที ").Append(p).Append(" · ");
                        if (s > 0) sb.Append("ค้าง PENDING เกิน 6 ชม. ").Append(s).Append(" · ");
                        if (se != null) foreach (DataRow r in se.Rows) sb.Append("\n").Append(r["CN_No"]).Append(": ").Append(r["E"]);
                        add("error", "ใบลดหนี้ที่ยังไม่ขึ้น NextAcc", sb.ToString().TrimEnd(' ', '·')
                            + "\n\nดูสาเหตุที่ตารางคิว (Entity CREDIT_NOTE) แก้ต้นเหตุแล้วกด Retry");
                    }
                }
                catch { }

                // คืนเงินผ่านเกตเวย์ (หน้า Admin เกตเวย์ / แดชบอร์ดเกตเวย์) แต่ยังไม่มีใบลดหนี้
                try
                {
                    var g = c.DatabaseQuerySafe(conn, GatewayRefundsWithoutCnSql, null);
                    if (g != null && g.Rows.Count > 0)
                    {
                        var lines = new List<string>();
                        foreach (DataRow r in g.Rows)
                            lines.Add(r["Txn_Ref"] + " · " + r["Source_Type"] + " " + r["Source_ID"]
                                      + (r["Receipt_ID"] == DBNull.Value ? "" : " · ใบ " + r["Receipt_ID"])
                                      + " · คืน " + Convert.ToDecimal(r["Refunded_Amount"]).ToString("#,##0.00", Inv));
                        add("warn", "คืนเงินผ่านเกตเวย์แล้ว แต่ยังไม่มีใบลดหนี้ (" + g.Rows.Count + (g.Rows.Count >= 20 ? "+" : "") + " รายการ)",
                            string.Join("\n", lines) + "\n\nเงินออกจากบัญชีแล้วแต่รายได้/ภาษีขายยังอยู่ — ออกใบลดหนี้ของใบต้นทาง เลือก \"คืนภายหลัง\" "
                            + "แล้ว \"บันทึกการคืนเงิน\" ด้วยแหล่งเงินของเกตเวย์ (ห้ามเลือกคืนผ่านเกตเวย์ซ้ำ)");
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                add("warn", "ตรวจหัวข้อใบลดหนี้ไม่สำเร็จ", ex.Message);
            }
        }

        // ── แจ้งเตือนรออนุมัติ (timer) ───────────────────────────────────────────

        private static DateTime _lastPoll = DateTime.MinValue;
        private static DateTime _lastAlert = DateTime.MinValue;
        private static string _lastAlertKey = "";

        /// <summary>
        /// เรียกจาก timer เบื้องหลัง (Global.asax): ตรวจทุก 15 นาที แจ้ง Telegram เมื่อมีใบลดหนี้รออนุมัติ / คืนเงินเกตเวย์ที่ยังไม่มีใบลดหนี้
        /// ส่งซ้ำเมื่อรายการเปลี่ยน หรือครบรอบ Nexaacc_Queue_Alert_Hours (ค่าเริ่มต้น 6 ชม.) · ปิดระบบใบลดหนี้/ไม่มีตาราง = ไม่ทำอะไร
        /// </summary>
        public static void NotifyPendingIfDue(string conn)
        {
            lock (_lock)
            {
                if ((DateTime.Now - _lastPoll).TotalMinutes < 15) return;
                _lastPoll = DateTime.Now;
            }
            try
            {
                if (!Enabled(conn)) return;
                var c = new code();
                int drafts = 0; DateTime? oldest = null; decimal draftSum = 0m;
                if (CreditNoteService.RequireApproval(conn))
                {
                    var dt = c.DatabaseQuerySafe(conn,
                        "SELECT COUNT(*) AS N, MIN(Created_At) AS O, ISNULL(SUM(Amount_Gross), 0) AS A FROM Credit_Note WHERE Status = 'DRAFT'", null);
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        drafts = Convert.ToInt32(dt.Rows[0]["N"]);
                        if (dt.Rows[0]["O"] != DBNull.Value) oldest = Convert.ToDateTime(dt.Rows[0]["O"]);
                        draftSum = Convert.ToDecimal(dt.Rows[0]["A"]);
                    }
                }
                int gw = 0;
                try
                {
                    var g = c.DatabaseQuerySafe(conn, GatewayRefundsWithoutCnSql, null);
                    gw = g == null ? 0 : g.Rows.Count;
                }
                catch { }
                if (drafts == 0 && gw == 0) { lock (_lock) { _lastAlertKey = ""; } return; }

                double hours = 6;
                try { int h = new AccountingConfig(conn).QueueAlertHours; if (h > 0) hours = h; } catch { }
                string key = drafts + "|" + gw;
                lock (_lock)
                {
                    if (key == _lastAlertKey && (DateTime.Now - _lastAlert).TotalHours < hours) return;
                    _lastAlertKey = key;
                    _lastAlert = DateTime.Now;
                }

                var sb = new StringBuilder();
                sb.AppendLine("<b>🧾 ใบลดหนี้ / คืนเงินลูกค้า ต้องดำเนินการ</b>");
                if (drafts > 0)
                    sb.AppendLine("⏳ <b>รออนุมัติ " + drafts + " ใบ</b> รวม " + draftSum.ToString("#,##0.00", Inv) + " บาท"
                                  + (oldest.HasValue ? " (เก่าสุด " + oldest.Value.ToString("dd/MM HH:mm", Inv) + ")" : "")
                                  + "\n     ยังไม่คืนเงิน/ยังไม่ลงบัญชีจนกว่าจะอนุมัติ");
                if (gw > 0)
                    sb.AppendLine("💳 <b>คืนเงินผ่านเกตเวย์แล้วแต่ยังไม่มีใบลดหนี้ " + gw + " รายการ</b>\n     ต้องออกใบลดหนี้ให้ตรงกับเงินที่คืน");
                sb.Append("<i>การเงิน & บัญชี → ใบลดหนี้ / คืนเงินลูกค้า</i>");
                Notify.Send(Notify.Ev.AccQueueAlert, sb.ToString());
            }
            catch { /* แจ้งเตือนพังต้องไม่กระทบ timer */ }
        }
    }
}
