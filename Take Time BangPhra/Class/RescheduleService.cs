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
        /// ใช้ตอนสร้าง reservation ใหม่ที่ยังไม่มีวัน check-in
        /// </summary>
        public void MarkAsPostponed(int reservationId, string reason = null, short? adminId = null, string adminName = null)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE [dbo].[Reservation]
                    SET [IsPostponed] = 1,
                        [PostponedDate] = GETDATE()
                    WHERE ID = @ID", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", reservationId);
                    cmd.ExecuteNonQuery();
                }
            }

            // Log the postpone event
            LogReschedule(
                reservationId,
                "POSTPONE",
                null, null, null,
                null, null, null,
                "มัดจำแล้ว", "มัดจำแล้ว",
                reason ?? "ลูกค้ายังไม่กำหนดวันเข้าพัก",
                adminId, adminName);
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
        /// </summary>
        public void CancelPostpone(
            int reservationId,
            short? adminId = null,
            string adminName = null,
            string reason = null)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Get current info before update
                string oldStatus = null;
                using (SqlCommand getCmd = new SqlCommand(
                    "SELECT Status FROM Reservation WHERE ID = @ID", conn))
                {
                    getCmd.Parameters.AddWithValue("@ID", reservationId);
                    oldStatus = getCmd.ExecuteScalar()?.ToString();
                }

                // Update status
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE [dbo].[Reservation]
                    SET [Status] = N'ยกเลิกการเลื่อนวันเข้าพัก',
                        [IsPostponed] = 0
                    WHERE ID = @ID", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", reservationId);
                    cmd.ExecuteNonQuery();
                }
            }

            // Log the cancellation
            LogReschedule(
                reservationId,
                "CANCEL_POSTPONE",
                null, null, null,
                null, null, null,
                oldStatus: "มัดจำแล้ว",
                newStatus: "ยกเลิกการเลื่อนวันเข้าพัก",
                reason: reason ?? "ยกเลิกการเลื่อนวันเข้าพัก",
                adminId: adminId,
                adminName: adminName);
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
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM [dbo].[Reservation]
                    WHERE IsPostponed = 1
                      AND Status = N'มัดจำแล้ว'", conn))
                {
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        /// <summary>
        /// เปลี่ยนวันเข้าพักพร้อมบันทึกส่วนต่างราคาในระบบบัญชี
        /// </summary>
        public void LogDateChangeWithPriceDiff(
            int reservationId,
            DateTime oldCheckinDate, DateTime oldCheckoutDate, int oldStayDays,
            DateTime newCheckinDate, DateTime newCheckoutDate, int newStayDays,
            decimal oldPrice, decimal newPrice,
            short? adminId = null, string adminName = null, string reason = null)
        {
            // Log the date change normally
            LogDateChange(reservationId, oldCheckinDate, oldCheckoutDate, oldStayDays,
                newCheckinDate, newCheckoutDate, newStayDays, adminId, adminName, reason);

            // If price changed, enqueue accounting entry for the difference
            decimal priceDiff = newPrice - oldPrice;
            if (priceDiff != 0)
            {
                try
                {
                    string customerName = "ลูกค้า";
                    using (SqlConnection conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(
                            @"SELECT ISNULL(c.Customer_Name, c.NickName) AS Name
                              FROM Reservation r
                              LEFT JOIN Customer c ON r.Customer_MobilePhone = c.Customer_MobilePhone
                              WHERE r.ID = @ID", conn))
                        {
                            cmd.Parameters.AddWithValue("@ID", reservationId);
                            object result = cmd.ExecuteScalar();
                            if (result != null) customerName = result.ToString();
                        }
                    }

                    // Accounting sync disabled — ใช้ manual sync จากหน้าจัดการเอกสารแทน
                }
                catch { }
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
