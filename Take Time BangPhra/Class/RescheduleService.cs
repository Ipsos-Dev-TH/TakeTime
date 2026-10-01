using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// Service สำหรับจัดการการเลื่อนวันเข้าพัก (Reschedule)
    /// - เก็บประวัติการเลื่อนทุกครั้ง
    /// - จัดการ status อย่างเป็นระบบ
    /// - รองรับการ postpone (ยังไม่กำหนดวัน), date change (เปลี่ยนวัน), cancel postpone (ยกเลิกการเลื่อน)
    /// </summary>
    public class RescheduleService
    {
        private readonly string _connectionString;

        public RescheduleService()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        }

        public RescheduleService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// บันทึกประวัติการเลื่อนวันเข้าพัก
        /// </summary>
        public long LogReschedule(
            int reservationId,
            string rescheduleType,
            DateTime? oldCheckinDate,
            DateTime? oldCheckoutDate,
            int? oldStayDays,
            DateTime? newCheckinDate,
            DateTime? newCheckoutDate,
            int? newStayDays,
            string oldStatus,
            string newStatus,
            string reason,
            short? adminId,
            string adminName,
            string remark = null)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO [dbo].[Reservation_Reschedule_History]
                        ([Reservation_ID], [RescheduleType],
                         [OldCheckinDate], [OldCheckoutDate], [OldStayDays],
                         [NewCheckinDate], [NewCheckoutDate], [NewStayDays],
                         [OldStatus], [NewStatus], [Reason],
                         [RescheduledBy_AdminID], [RescheduledBy_Name],
                         [RescheduledDate], [Remark])
                    VALUES
                        (@ReservationID, @RescheduleType,
                         @OldCheckinDate, @OldCheckoutDate, @OldStayDays,
                         @NewCheckinDate, @NewCheckoutDate, @NewStayDays,
                         @OldStatus, @NewStatus, @Reason,
                         @AdminID, @AdminName,
                         GETDATE(), @Remark);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@ReservationID", reservationId);
                    cmd.Parameters.AddWithValue("@RescheduleType", rescheduleType);
                    cmd.Parameters.AddWithValue("@OldCheckinDate", (object)oldCheckinDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OldCheckoutDate", (object)oldCheckoutDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OldStayDays", (object)oldStayDays ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@NewCheckinDate", (object)newCheckinDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@NewCheckoutDate", (object)newCheckoutDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@NewStayDays", (object)newStayDays ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OldStatus", (object)oldStatus ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@NewStatus", (object)newStatus ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Reason", (object)reason ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@AdminID", (object)adminId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@AdminName", (object)adminName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Remark", (object)remark ?? DBNull.Value);

                    object result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt64(result) : 0;
                }
            }
        }

        /// <summary>
        /// ตั้งค่า Reservation เป็น Postponed (เลื่อนวันเข้าพัก - ยังไม่กำหนดวันใหม่)
        /// ใช้ตอนสร้าง reservation ใหม่ที่ยังไม่มีวัน check-in / กดปุ่มเลื่อนเข้าพัก / บันทึกหน้าแก้ไขโดยไม่ใส่วัน
        ///
        /// ทำซ้ำได้ (idempotent): ใบที่เลื่อนอยู่แล้ว (เช่น เปิดหน้าแก้ไขแล้วบันทึกหมายเหตุโดยไม่ใส่วัน)
        /// จะไม่ถูกรีเซ็ต PostponedDate และไม่บันทึกประวัติ POSTPONE ซ้ำ — เดิมรีเซ็ตทุกครั้ง ทำให้
        /// "อายุการเลื่อน" (นับวันหมดอายุมัดจำ) เริ่มนับใหม่ทุกครั้งที่มีคนแก้ใบ
        ///
        /// ส่งวันเดิม (oldCheckin/oldCheckout/oldStayDays) มาด้วยเมื่อใบมีวันเข้าพักจริงก่อนเลื่อน —
        /// บันทึกเป็นประวัติแถวเดียว (เดิมปุ่มเลื่อนบันทึก 2 แถว: แถวแรกไม่มีวันเดิม แถวสองมีวันเดิม
        /// ⇒ หน้ารายการเลื่อนหยิบแถวแรกมาแสดงเป็น "การจองเดิม" จึงขึ้น "ไม่มีประวัติ")
        /// </summary>
        /// <returns>true = เพิ่งเปลี่ยนเป็นเลื่อน (บันทึกประวัติแล้ว), false = เลื่อนอยู่แล้วก่อนหน้า</returns>
        public bool MarkAsPostponed(int reservationId, string reason = null, short? adminId = null, string adminName = null,
            DateTime? oldCheckinDate = null, DateTime? oldCheckoutDate = null, int? oldStayDays = null)
        {
            bool wasPostponed = false;
            string oldStatus = null;
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                // SET ทุกนิพจน์อ่านค่า "ก่อน" update ⇒ CASE ใช้ค่าเดิมของ IsPostponed/PostponedDate
                using (SqlCommand cmd = new SqlCommand(@"
                    DECLARE @was bit, @st nvarchar(100);
                    SELECT @was = ISNULL([IsPostponed], 0), @st = [Status] FROM [dbo].[Reservation] WHERE ID = @ID;
                    UPDATE [dbo].[Reservation]
                    SET [IsPostponed] = 1,
                        [PostponedDate] = CASE WHEN ISNULL([IsPostponed], 0) = 1 AND [PostponedDate] IS NOT NULL
                                               THEN [PostponedDate] ELSE GETDATE() END
                    WHERE ID = @ID;
                    SELECT ISNULL(@was, 0) AS WasPostponed, @st AS OldStatus;", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", reservationId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            wasPostponed = reader["WasPostponed"] != DBNull.Value && Convert.ToBoolean(reader["WasPostponed"]);
                            oldStatus = reader["OldStatus"] != DBNull.Value ? reader["OldStatus"].ToString() : null;
                        }
                    }
                }
            }

            if (wasPostponed) return false;

            bool hasOldDates = oldCheckinDate.HasValue && !IsPlaceholderDate(oldCheckinDate);

            // Log the postpone event (แถวเดียว — พร้อมวันเดิมถ้ามี)
            LogReschedule(
                reservationId,
                "POSTPONE",
                hasOldDates ? oldCheckinDate : null,
                hasOldDates && !IsPlaceholderDate(oldCheckoutDate) ? oldCheckoutDate : null,
                hasOldDates ? oldStayDays : null,
                null, null, null,
                oldStatus ?? "มัดจำแล้ว", oldStatus ?? "มัดจำแล้ว",
                reason ?? "ลูกค้ายังไม่กำหนดวันเข้าพัก",
                adminId, adminName);
            return true;
        }

        /// <summary>
        /// กำหนดวันเข้าพักให้ reservation ที่ถูก postpone
        /// (เปลี่ยนจาก postponed -> มีวันเข้าพักแล้ว)
        /// </summary>
        public void SetDatesFromPostpone(
            int reservationId,
            DateTime newCheckinDate,
            DateTime newCheckoutDate,
            int stayDays,
            short? adminId = null,
            string adminName = null,
            string reason = null)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Get current reservation info
                DateTime? oldCheckinDate = null;
                DateTime? oldCheckoutDate = null;
                int? oldStayDays = null;
                string oldStatus = null;

                using (SqlCommand getCmd = new SqlCommand(
                    "SELECT CheckinDate, CheckoutDate, StayDays, Status FROM Reservation WHERE ID = @ID", conn))
                {
                    getCmd.Parameters.AddWithValue("@ID", reservationId);
                    using (SqlDataReader reader = getCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            oldCheckinDate = reader["CheckinDate"] != DBNull.Value ? (DateTime?)reader.GetDateTime(0) : null;
                            oldCheckoutDate = reader["CheckoutDate"] != DBNull.Value ? (DateTime?)reader.GetDateTime(1) : null;
                            oldStayDays = reader["StayDays"] != DBNull.Value ? (int?)Convert.ToInt32(reader["StayDays"]) : null;
                            oldStatus = reader["Status"]?.ToString();
                        }
                    }
                }

                // Update reservation: clear postponed flag
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE [dbo].[Reservation]
                    SET [IsPostponed] = 0,
                        [RescheduleCount] = [RescheduleCount] + 1
                    WHERE ID = @ID", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", reservationId);
                    cmd.ExecuteNonQuery();
                }

                // Log the date assignment
                LogReschedule(
                    reservationId,
                    "DATE_CHANGE",
                    oldCheckinDate, oldCheckoutDate, oldStayDays,
                    newCheckinDate, newCheckoutDate, stayDays,
                    oldStatus, oldStatus,
                    reason ?? "กำหนดวันเข้าพักจากการเลื่อน",
                    adminId, adminName);
            }
        }

        /// <summary>
        /// เปลี่ยนวันเข้าพัก (จากวันเดิมเป็นวันใหม่)
        /// ใช้ตอน edit reservation ที่มีวัน check-in อยู่แล้ว
        /// </summary>
        public void LogDateChange(
            int reservationId,
            DateTime oldCheckinDate,
            DateTime oldCheckoutDate,
            int oldStayDays,
            DateTime newCheckinDate,
            DateTime newCheckoutDate,
            int newStayDays,
            short? adminId = null,
            string adminName = null,
            string reason = null)
        {
            // Update reschedule count
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE [dbo].[Reservation]
                    SET [RescheduleCount] = [RescheduleCount] + 1
                    WHERE ID = @ID", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", reservationId);
                    cmd.ExecuteNonQuery();
                }
            }

            LogReschedule(
                reservationId,
                "DATE_CHANGE",
                oldCheckinDate, oldCheckoutDate, oldStayDays,
                newCheckinDate, newCheckoutDate, newStayDays,
                null, null,
                reason ?? "เปลี่ยนวันเข้าพัก",
                adminId, adminName);
        }

        /// <summary>
        /// ยกเลิกการเลื่อนวันเข้าพัก (ลบออกจากรายการเลื่อน)
        ///
        /// ยกเลิกได้เฉพาะใบที่ "ยังเลื่อนอยู่จริง" (ยังไม่มีวันเข้าพัก + สถานะ มัดจำแล้ว/รอชำระเงิน) —
        /// กันหน้ารายการที่เปิดค้างไว้ไปยกเลิกใบที่มีคนลงวันใหม่/เช็คอินไปแล้ว (เดิม UPDATE ตาม ID อย่างเดียว)
        /// ไม่แตะบัญชี: มัดจำที่รับไว้ยังเป็นเงินรับล่วงหน้า (หนี้สินต่อลูกค้า) จนกว่าจะคืนเงิน/ริบมัดจำแยกต่างหาก
        /// </summary>
        /// <returns>true = ยกเลิกแล้ว, false = ใบไม่อยู่ในสถานะเลื่อน (ไม่ได้เปลี่ยนอะไร)</returns>
        public bool CancelPostpone(
            int reservationId,
            short? adminId = null,
            string adminName = null,
            string reason = null)
        {
            string oldStatus = null;
            int affected;
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Get current info before update
                using (SqlCommand getCmd = new SqlCommand(
                    "SELECT Status FROM Reservation WHERE ID = @ID", conn))
                {
                    getCmd.Parameters.AddWithValue("@ID", reservationId);
                    oldStatus = getCmd.ExecuteScalar()?.ToString();
                }

                // Update status — เฉพาะใบที่ยังเลื่อนอยู่ (วันเข้าพักเป็นค่าแทน 1990/ว่าง)
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE [dbo].[Reservation]
                    SET [Status] = N'ยกเลิกการเลื่อนวันเข้าพัก',
                        [IsPostponed] = 0
                    WHERE ID = @ID
                      AND [Status] IN (N'มัดจำแล้ว', N'รอชำระเงิน')
                      AND ([CheckinDate] IS NULL OR [CheckinDate] < '19910101')", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", reservationId);
                    affected = cmd.ExecuteNonQuery();
                }
            }

            if (affected <= 0) return false;

            string why = string.IsNullOrWhiteSpace(reason) ? "ยกเลิกการเลื่อนวันเข้าพัก" : reason.Trim();
            if (why.Length > 500) why = why.Substring(0, 500);   // คอลัมน์ Reason NVARCHAR(500)

            // Log the cancellation — สถานะจริงก่อนยกเลิกได้ถูกอัปเดตไปแล้ว อย่าให้ log พังแล้วผู้ใช้เห็นว่า "ยกเลิกไม่สำเร็จ"
            try
            {
                LogReschedule(
                    reservationId,
                    "CANCEL_POSTPONE",
                    null, null, null,
                    null, null, null,
                    oldStatus: oldStatus ?? "มัดจำแล้ว",
                    newStatus: "ยกเลิกการเลื่อนวันเข้าพัก",
                    reason: why,
                    adminId: adminId,
                    adminName: adminName);
            }
            catch (Exception ex)
            {
                try
                {
                    new code().Logs(_connectionString, "PostponeList - Cancel history log error",
                        "Reservation " + reservationId + ": " + ex.Message, adminName ?? "SYSTEM");
                }
                catch { }
            }
            return true;
        }

        /// <summary>
        /// ดึงประวัติการเลื่อนวันเข้าพักทั้งหมดของ reservation
        /// </summary>
        public DataTable GetRescheduleHistory(int reservationId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT *
                    FROM [dbo].[Reservation_Reschedule_History]
                    WHERE Reservation_ID = @ReservationID
                    ORDER BY RescheduledDate DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@ReservationID", reservationId);
                    DataTable dt = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                    return dt;
                }
            }
        }

        /// <summary>
        /// ดึงรายการ reservation ที่ถูก postpone ทั้งหมด
        /// </summary>
        public DataTable GetPostponedReservations()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        R.ID,
                        R.Customer_MobilePhone,
                        R.TotalPrice,
                        R.Deposit,
                        R.Remark,
                        R.Created_Date,
                        R.IsPostponed,
                        R.PostponedDate,
                        R.RescheduleCount,
                        R.StayDays,
                        C.Name,
                        C.NickName,
                        C.FullName,
                        (SELECT COUNT(*) FROM Reservation_Reschedule_History RH
                         WHERE RH.Reservation_ID = R.ID) AS TotalReschedules,
                        (SELECT TOP 1 RH.Reason FROM Reservation_Reschedule_History RH
                         WHERE RH.Reservation_ID = R.ID ORDER BY RH.RescheduledDate DESC) AS LastRescheduleReason
                    FROM [dbo].[Reservation] R
                    INNER JOIN [dbo].[Customer] C ON C.MobilePhone = R.Customer_MobilePhone
                    WHERE R.IsPostponed = 1
                      AND R.Status = N'มัดจำแล้ว'
                    ORDER BY R.ID DESC", conn))
                {
                    DataTable dt = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                    return dt;
                }
            }
        }

        /// <summary>
        /// นับจำนวน reservation ที่ถูก postpone
        /// </summary>
        public int GetPostponedCount()
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                // เกณฑ์เดียวกับหน้ารายการเลื่อน/ตัวช่วย SqlIsPostponed (ค่าแทน 1990 หรือธง IsPostponed)
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM [dbo].[Reservation] R
                    WHERE R.Status IN (N'มัดจำแล้ว', N'รอชำระเงิน')
                      AND " + SqlIsPostponed("R", true), conn))
                {
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        // LogDateChangeWithPriceDiff ถูกลบ (ต.ค. 2026): ไม่มีผู้เรียกเลย และ query ชื่อลูกค้าอ้างคอลัมน์ที่ไม่มีจริง
        // (Customer.Customer_Name / Customer.Customer_MobilePhone — ของจริงคือ Name / MobilePhone) แล้วผลก็ไม่ถูกใช้
        // (บล็อกบัญชีถูกปิดไว้) ⇒ ถ้าเรียกเมื่อไรจะ throw ทิ้งเงียบ ๆ ใน catch. การเปลี่ยนวันใช้ LogDateChange ตรง ๆ

        // ── เงื่อนไข SQL กลาง: สถานะที่ยกเลิก / ใบที่ถือห้อง / ใบเลื่อน ─────────────────────────
        // ใช้ร่วมกันทุกหน้ารายงาน/รายการ/ห้องว่าง ให้ทุกหน้านับชุดเดียวกัน (เดิมแต่ละหน้าเขียน NOT IN เอง
        // คนละชุด — บางหน้าลืม "ลบจากการเลื่อนวันเข้าพัก"/"ยกเลิกการเลื่อนวันเข้าพัก", ตัวตรวจห้องว่างหลัก
        // ตัดแค่ N'ยกเลิก' ตรงตัว ⇒ ใบ "ยกเลิกคืนเงิน/ยกเลิกไม่คืนเงิน" ที่ยังมีแถวห้องค้างอยู่ถูกนับว่าห้องไม่ว่าง)
        //
        // นโยบายสถานะ (ตัดสินแล้ว — ต.ค. 2026):
        //   · ยกเลิก* / ลบ*        = ไม่ถือห้อง ไม่มียอดค้าง (ยกเลิก, ยกเลิกคืนเงิน, ยกเลิกไม่คืนเงิน,
        //                            ยกเลิกการเลื่อนวันเข้าพัก, ลบจากการเลื่อนวันเข้าพัก, สถานะใหม่ที่ขึ้นต้นแบบนี้)
        //   · ไม่มาเช็คอิน (no-show) = คืนห้องให้ขายต่อได้ (ตรงกับตัวตรวจห้องว่างเดิม / Default.aspx / ตัวอ่านอีเมล OTA)
        //                            และไม่นับเป็นผู้เข้าพัก (อัตราเข้าพัก/ตารางรายวัน) — แต่ยังแสดงในรายการจองให้เจ้าหน้าที่จัดการ
        //   · เสร็จสิ้น              = คืนห้องแล้วสำหรับตัวตรวจห้องว่าง (กฎเดิม) แต่ยังนับเป็นผู้เข้าพักของวันนั้นในรายงาน
        //   · ใบเลื่อน (ยังไม่มีวันเข้าพัก: CheckinDate ว่าง/ค่าแทน 1990-01-01, 0001-01-01) = ไม่ถือห้อง
        //     ไม่นับในยอดค้างชำระ (ลูกค้ายังไม่กำหนดวัน) — มัดจำที่ถือไว้แสดงแยกเป็น "มัดจำของใบเลื่อน"
        // '19910101' (ISO ไม่มีขีด) ไม่ขึ้นกับ DATEFORMAT และไม่ล้นช่วง datetime (เทียบ '0001-01-01' ตรง ๆ = error)

        private static string SqlPrefix(string alias)
        {
            return string.IsNullOrWhiteSpace(alias) ? "" : alias.Trim() + ".";
        }

        /// <summary>สถานะยังไม่ถูกยกเลิก/ลบ — alias = ชื่อ alias ของตาราง Reservation ("" = ไม่มี alias)</summary>
        public static string SqlNotCancelled(string alias)
        {
            string a = SqlPrefix(alias);
            return "(" + a + "Status NOT LIKE N'ยกเลิก%' AND " + a + "Status NOT LIKE N'ลบ%')";
        }

        /// <summary>
        /// ใบที่นับเป็น "ผู้เข้าพัก" ในช่วงวัน (ตารางรายวัน / อัตราเข้าพัก): ไม่ยกเลิก/ลบ, ไม่ใช่ no-show, มีวันเข้าพักจริง
        /// </summary>
        public static string SqlActiveStay(string alias)
        {
            string a = SqlPrefix(alias);
            return "(" + SqlNotCancelled(alias)
                 + " AND " + a + "Status <> N'ไม่มาเช็คอิน'"
                 + " AND " + a + "CheckinDate >= '19910101')";
        }

        /// <summary>
        /// ใบที่ "ถือห้อง" สำหรับตรวจห้องว่าง/กันจองซ้อน: <see cref="SqlActiveStay"/> และไม่ใช่ เสร็จสิ้น (กฎเดิมของตัวตรวจห้องว่าง)
        /// </summary>
        public static string SqlHoldsRoom(string alias)
        {
            string a = SqlPrefix(alias);
            return "(" + SqlActiveStay(alias) + " AND " + a + "Status <> N'เสร็จสิ้น')";
        }

        /// <summary>
        /// ใบเลื่อน: ไม่มีวันเข้าพัก (ว่าง/ค่าแทนปี ≤ 1990) หรือ IsPostponed = 1 ขณะยังไม่เช็คอิน
        /// (ธงอย่างเดียวตอนเช็คอินแล้วถือว่าค้างจากการล้างไม่สำเร็จ — ไม่นับ กันยอดค้างของแขกที่พักอยู่หายไป)
        /// withFlag = มีคอลัมน์ IsPostponed (ดู <see cref="HasIsPostponedColumn"/>)
        /// </summary>
        public static string SqlIsPostponed(string alias, bool withFlag)
        {
            string a = SqlPrefix(alias);
            return "(" + a + "CheckinDate IS NULL OR " + a + "CheckinDate < '19910101'"
                 + (withFlag
                     ? " OR (ISNULL(" + a + "IsPostponed, 0) = 1 AND " + a + "Status IN (N'มัดจำแล้ว', N'รอชำระเงิน'))"
                     : "")
                 + ")";
        }

        private static readonly object _postponeColLock = new object();
        private static bool? _hasPostponeCol;
        private static DateTime _postponeColCheckedAt = DateTime.MinValue;

        /// <summary>มีคอลัมน์ Reservation.IsPostponed (PHASE8) — cache; ไม่มีตรวจใหม่ทุก 5 นาที (เผื่อเพิ่งรัน migration)</summary>
        public static bool HasIsPostponedColumn(string connectionString)
        {
            lock (_postponeColLock)
            {
                if (_hasPostponeCol.HasValue
                    && (_hasPostponeCol.Value || (DateTime.Now - _postponeColCheckedAt).TotalMinutes < 5))
                    return _hasPostponeCol.Value;
            }
            bool has = false;
            try
            {
                DataTable dt = new code().DatabaseQuerySafe(connectionString,
                    "SELECT COL_LENGTH('Reservation', 'IsPostponed') AS C");
                has = dt != null && dt.Rows.Count > 0 && dt.Rows[0]["C"] != DBNull.Value;
            }
            catch { has = false; }
            lock (_postponeColLock)
            {
                _hasPostponeCol = has;
                _postponeColCheckedAt = DateTime.Now;
            }
            return has;
        }

        /// <summary>
        /// ใบเลื่อนที่ยังมีผล (มัดจำแล้ว/รอชำระเงิน) — จำนวนใบ + ยอดมัดจำที่ถือไว้ (สูตรกลาง: Payment_History ก่อน, ไม่มี → Deposit)
        /// ใช้แสดง "มัดจำของใบเลื่อน ฿x" แยกจากยอดค้างชำระ. extraWhere = เงื่อนไขเพิ่ม (alias r, เขียนในโค้ดเท่านั้น)
        /// </summary>
        public static void GetPostponedHeld(string connectionString, string extraWhere, Dictionary<string, object> parameters,
            out int count, out decimal held)
        {
            count = 0;
            held = 0m;
            string where = "r.Status IN (N'มัดจำแล้ว', N'รอชำระเงิน') AND "
                         + SqlIsPostponed("r", HasIsPostponedColumn(connectionString))
                         + (string.IsNullOrWhiteSpace(extraWhere) ? "" : " AND (" + extraWhere + ")");
            Dictionary<int, ReservationBalance> map = ReservationBalance.LoadMany(connectionString, where, parameters);
            foreach (ReservationBalance b in map.Values)
            {
                count++;
                // เงินที่ลูกค้าจ่ายโรงแรมจริง (ไม่ใช่ Received ซึ่งของใบ OTA Channel รวมเงินที่ OTA ถือ)
                held += b.LedgerRows > 0 ? b.PaidLedger
                      : (b.IsChannelCollect || b.IsCollectUnknown ? 0m : b.Deposit);
            }
        }

        /// <summary>
        /// ตรวจสอบว่า reservation เป็น placeholder date หรือไม่ (backward compatibility)
        /// </summary>
        public static bool IsPlaceholderDate(DateTime? date)
        {
            if (!date.HasValue) return true;
            return date.Value.Year <= 1990;
        }
    }
}
