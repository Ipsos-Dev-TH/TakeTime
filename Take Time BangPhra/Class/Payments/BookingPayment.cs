using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;

namespace Take_Time_BangPhra.Payments
{
    /// <summary>
    /// "จองแล้วจ่ายด้วยบัตรทันที" สำหรับลูกค้าที่จองเอง
    ///
    /// เดิมหน้าจองบังคับให้โอนเงินแล้วแนบสลิปก่อนเสมอ ใบจองจึงถูกสร้างด้วยสถานะ
    /// "มัดจำแล้ว" ตายตัว — ไม่มีสถานะ "ยังไม่จ่าย" ในระบบเลย ⇒ จ่ายออนไลน์ไม่ได้
    /// เพราะตัวรับเงินต้องมีใบจองอยู่ก่อน แต่ใบจองสร้างไม่ได้ถ้ายังไม่จ่าย (ไก่กับไข่)
    ///
    /// คลาสนี้เพิ่มสถานะกลาง <see cref="PendingStatus"/> ให้วงจรครบ:
    ///   จองเสร็จ (รอชำระเงิน, กันห้องไว้) → ลูกค้าจ่าย → เลื่อนเป็น "มัดจำแล้ว"
    ///   ไม่จ่ายภายในเวลา → ยกเลิกอัตโนมัติ ห้องกลับมาว่าง
    ///
    /// ⚠ ปิดสวิตช์ = หน้าจองทำงานเหมือนเดิมเป๊ะ ๆ (โอน+แนบสลิปอย่างเดียว)
    ///    ไม่มีใบจองสถานะนี้เกิดขึ้นได้เลย
    /// </summary>
    public static class BookingPayment
    {
        /// <summary>สถานะใบจองที่ยังรอลูกค้าจ่าย — ห้องถูกกันไว้ชั่วคราว</summary>
        public const string PendingStatus = "รอชำระเงิน";

        /// <summary>สถานะปกติหลังได้รับเงินแล้ว (ค่าเดิมของระบบ)</summary>
        public const string PaidStatus = "มัดจำแล้ว";

        private static string Conn
        {
            get { return ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString; }
        }

        /// <summary>
        /// เปิดให้ลูกค้าจองแล้วจ่ายออนไลน์ทันทีไหม
        /// ต้องครบ: สวิตช์นี้เปิด + ระบบชำระเงินพร้อม + ช่องทางการจองเปิด + มีวิธีที่ไม่ใช่แนบสลิป
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try
                {
                    if (!PaymentGatewayConfig.GetBool("Payment_Booking_PayOnline", false)) return false;
                    if (!PaymentGatewayConfig.ChannelEnabled(PaymentSource.Reservation)) return false;
                    foreach (string m in PaymentGatewayConfig.AvailableMethods(0m))
                        if (m != PaymentGatewayConfig.MethodManualQr) return true;
                    return false;
                }
                catch { return false; }
            }
        }

        /// <summary>กี่นาทีที่ยอมให้ใบจองค้างสถานะ "รอชำระเงิน" ก่อนยกเลิกคืนห้อง</summary>
        public static int HoldMinutes
        {
            get { return Math.Max(5, PaymentGatewayConfig.GetInt("Payment_Booking_Hold_Minutes", 60)); }
        }

        /// <summary>ลิงก์พาลูกค้าไปจ่ายต่อทันทีหลังกดยืนยันการจอง</summary>
        public static string PayUrl(int reservationId, string phone, decimal amount)
        {
            string url = PaymentUrls.SiteBase()
                + "/Payment/Pay?src=" + PaymentSource.Reservation
                + "&id=" + reservationId
                + "&ph=" + Uri.EscapeDataString(phone ?? "");
            if (amount > 0)
                url += "&amt=" + amount.ToString("0.00", CultureInfo.InvariantCulture);
            return url;
        }

        /// <summary>
        /// ได้รับเงินแล้ว — เลื่อนใบจองจาก "รอชำระเงิน" เป็น "มัดจำแล้ว"
        ///
        /// เขียนแบบมีเงื่อนไขสถานะเดิมเสมอ (WHERE Status = @pending) ⇒ ยิงซ้ำกี่ครั้ง
        /// ก็ไม่ทับสถานะอื่นที่พนักงานอาจเปลี่ยนไปแล้ว (เช่น เช็คอินแล้ว/ยกเลิก)
        /// คืน true เฉพาะครั้งที่เปลี่ยนได้จริง
        /// </summary>
        public static bool PromoteIfPending(string conn, int reservationId)
        {
            try
            {
                var c = new code();
                int n = c.DatabaseInsertSafe(string.IsNullOrEmpty(conn) ? Conn : conn,
                    "UPDATE Reservation SET [Status] = @paid WHERE ID = @id AND [Status] = @pending",
                    new Dictionary<string, object>
                    {
                        { "@paid", PaidStatus },
                        { "@pending", PendingStatus },
                        { "@id", reservationId }
                    });
                return n > 0;
            }
            catch { return false; }
        }

        /// <summary>ใบจองนี้ยังรอชำระเงินอยู่ไหม</summary>
        public static bool IsAwaitingPayment(string conn, int reservationId)
        {
            try
            {
                DataTable dt = new code().DatabaseQuerySafe(string.IsNullOrEmpty(conn) ? Conn : conn,
                    "SELECT TOP 1 [Status] FROM Reservation WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", reservationId } });
                if (dt == null || dt.Rows.Count == 0) return false;
                return Convert.ToString(dt.Rows[0]["Status"]) == PendingStatus;
            }
            catch { return false; }
        }

        // ── กวาดใบจองที่ไม่จ่ายสักที ────────────────────────────────────────

        private static DateTime _lastSweep = DateTime.MinValue;
        private static readonly object _sweepLock = new object();

        /// <summary>
        /// ยกเลิกใบจองที่ค้าง "รอชำระเงิน" เกินเวลา — ห้องกลับมาว่างให้คนอื่นจองได้
        ///
        /// ถ้าไม่มีตัวนี้ ใบจองที่ลูกค้ากดแล้วหนีจะกินห้องค้างไว้ตลอดกาล (ตัวนับห้องว่าง
        /// นับทุกสถานะที่ไม่ใช่ ยกเลิก/เสร็จสิ้น/ไม่มาเช็คอิน)
        ///
        /// เรียกจาก timer เดียวกับงานเบื้องหลังอื่น — คุมความถี่ไว้ 5 นาทีต่อครั้ง
        /// </summary>
        public static void CancelStaleUnpaidIfDue(string conn)
        {
            lock (_sweepLock)
            {
                if ((DateTime.Now - _lastSweep).TotalMinutes < 5) return;
                _lastSweep = DateTime.Now;
            }

            try
            {
                if (!PaymentGatewayConfig.GetBool("Payment_Booking_PayOnline", false)) return;

                var c = new code();
                string cs = string.IsNullOrEmpty(conn) ? Conn : conn;

                // กันพลาด: ยกเลิกเฉพาะใบที่ยังไม่มีเงินเข้าจริงสักบาท
                int n = c.DatabaseInsertSafe(cs, @"
                    UPDATE r
                       SET r.[Status] = N'ยกเลิก',
                           r.Remark = ISNULL(r.Remark, N'') + N' [ระบบยกเลิกอัตโนมัติ: ไม่ชำระเงินภายในเวลาที่กำหนด]'
                      FROM Reservation r
                     WHERE r.[Status] = @pending
                       AND r.Created_Date < DATEADD(MINUTE, -@mins, GETDATE())
                       AND ISNULL(r.Deposit, 0) <= 0
                       AND NOT EXISTS (
                             SELECT 1 FROM Payment_History ph
                              WHERE ph.Reservation_ID = r.ID AND ph.[Status] = 'COMPLETED')",
                    new Dictionary<string, object>
                    {
                        { "@pending", PendingStatus },
                        { "@mins", HoldMinutes }
                    });

                if (n > 0)
                {
                    try
                    {
                        Notify.Send(Notify.Ev.BookingCancel,
                            "🕐 <b>ยกเลิกการจองอัตโนมัติ</b> " + n + " รายการ\n"
                            + "เหตุผล: ลูกค้าไม่ชำระเงินภายใน " + HoldMinutes + " นาที — ห้องกลับมาว่างแล้ว");
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
