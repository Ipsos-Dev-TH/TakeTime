using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Take_Time_BangPhra.Class
{
    /// <summary>
    /// 🏠 Service สำหรับตรวจสอบความพร้อมของที่พัก
    /// รองรับทั้งห้องปกติและห้องแบบ LimitWithPeople
    /// </summary>
    public class AccommodationAvailabilityService
    {
        private readonly string _connectionString;
        private readonly code _code;

        public AccommodationAvailabilityService(string connectionString)
        {
            _connectionString = connectionString;
            _code = new code();
        }

        /// <summary>
        /// Result class สำหรับการตรวจสอบความพร้อม
        /// </summary>
        public class AvailabilityResult
        {
            public bool IsAvailable { get; set; }
            public string ErrorMessage { get; set; }
            public string ConflictDetails { get; set; }
            public bool IsLimitWithPeople { get; set; }
            public int CurrentOccupancy { get; set; }
            public int MaxCapacity { get; set; }
            public int RequestedPeople { get; set; }
        }

        /// <summary>
        /// ✅ ตรวจสอบว่าที่พักว่างหรือไม่สำหรับช่วงวันที่กำหนด
        /// รองรับทั้งห้องปกติและห้อง LimitWithPeople
        /// </summary>
        /// <param name="accommodationId">ID ของที่พัก</param>
        /// <param name="accomName">ชื่อที่พัก</param>
        /// <param name="checkinDate">วันเช็คอิน</param>
        /// <param name="checkoutDate">วันเช็คเอาท์</param>
        /// <param name="requestedPeople">จำนวนคนที่ต้องการจอง</param>
        /// <param name="excludeReservationId">Reservation ID ที่ต้องการยกเว้น (สำหรับกรณีแก้ไข)</param>
        /// <returns>AvailabilityResult</returns>
        public AvailabilityResult CheckAvailability(
            int accommodationId,
            string accomName,
            DateTime checkinDate,
            DateTime checkoutDate,
            int requestedPeople,
            int excludeReservationId = 0)
        {
            var result = new AvailabilityResult
            {
                IsAvailable = true,
                RequestedPeople = requestedPeople
            };

            // 🔍 Step 1: ดึงข้อมูลที่พัก
            var accomParams = new Dictionary<string, object>
            {
                { "@accommodationId", accommodationId }
            };

            DataTable dtAccom = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ID, AccomName, LimitWithPeople, People
                  FROM Accommodation
                  WHERE ID = @accommodationId",
                accomParams);

            if (dtAccom.Rows.Count == 0)
            {
                result.IsAvailable = false;
                result.ErrorMessage = $"ไม่พบข้อมูลที่พัก ID: {accommodationId}";
                return result;
            }

            bool isLimitWithPeople = dtAccom.Rows[0]["LimitWithPeople"].ToString() == "True" ||
                                      dtAccom.Rows[0]["LimitWithPeople"].ToString() == "1";
            int maxCapacity = Convert.ToInt32(dtAccom.Rows[0]["People"]);

            result.IsLimitWithPeople = isLimitWithPeople;
            result.MaxCapacity = maxCapacity;

            // 🔍 Step 2: ตรวจสอบการจองที่มีอยู่
            var conflictParams = new Dictionary<string, object>
            {
                { "@accommodationId", accommodationId },
                { "@checkinDate", checkinDate.ToString("yyyy-MM-dd") },
                { "@checkoutDate", checkoutDate.ToString("yyyy-MM-dd") },
                { "@excludeReservationId", excludeReservationId }
            };

            DataTable dtConflicts = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    R.ID AS ReservationID,
                    R.CheckinDate,
                    R.CheckoutDate,
                    R.Status,
                    R.Customer_MobilePhone,
                    RA.Amount,
                    C.Name AS CustomerName,
                    C.MobilePhone
                  FROM [Reservation_Accommodation] RA
                  INNER JOIN [Reservation] R ON R.ID = RA.Reservation_ID
                  LEFT JOIN [Customer] C ON C.MobilePhone = R.Customer_MobilePhone
                  WHERE RA.Accommodation_ID = @accommodationId
                    AND R.ID != @excludeReservationId
                    AND R.Status NOT IN (N'ยกเลิก', N'เสร็จสิ้น', N'ไม่มาเช็คอิน')
                    AND (
                        -- Check for date range overlap
                        (@checkinDate < R.CheckoutDate AND @checkoutDate > R.CheckinDate)
                    )",
                conflictParams);

            // 🔍 Step 3: ตรวจสอบตามประเภทห้อง
            if (!isLimitWithPeople)
            {
                // ห้องปกติ: ไม่อนุญาตให้จองซ้ำเลย
                if (dtConflicts.Rows.Count > 0)
                {
                    result.IsAvailable = false;
                    BuildConflictMessage(result, dtConflicts, accomName);
                }
            }
            else
            {
                // ห้อง LimitWithPeople: ตรวจสอบจำนวนคนที่จองรวมกัน
                int currentOccupancy = 0;
                foreach (DataRow row in dtConflicts.Rows)
                {
                    currentOccupancy += Convert.ToInt32(row["Amount"]);
                }

                result.CurrentOccupancy = currentOccupancy;

                if (currentOccupancy + requestedPeople > maxCapacity)
                {
                    // ห้องเต็ม!
                    result.IsAvailable = false;
                    result.ErrorMessage = $@"❌ ห้อง '{accomName}' เต็มแล้ว!

📊 ข้อมูลการจอง:
• จำนวนคนสูงสุด: {maxCapacity} คน
• จองแล้ว: {currentOccupancy} คน
• ที่ว่าง: {maxCapacity - currentOccupancy} คน
• คุณต้องการจอง: {requestedPeople} คน

⚠️ ไม่สามารถจองได้ กรุณาลดจำนวนคน หรือเลือกห้องอื่น";
                }
            }

            return result;
        }

        /// <summary>
        /// สร้างข้อความแสดง conflict details
        /// </summary>
        private void BuildConflictMessage(AvailabilityResult result, DataTable dtConflicts, string accomName)
        {
            string conflictCustomer = dtConflicts.Rows[0]["CustomerName"]?.ToString() ?? "ลูกค้าท่านอื่น";
            string conflictPhone = dtConflicts.Rows[0]["MobilePhone"]?.ToString() ?? "";
            DateTime conflictCheckin = Convert.ToDateTime(dtConflicts.Rows[0]["CheckinDate"]);
            DateTime conflictCheckout = Convert.ToDateTime(dtConflicts.Rows[0]["CheckoutDate"]);
            int conflictReservationId = Convert.ToInt32(dtConflicts.Rows[0]["ReservationID"]);

            result.ErrorMessage = $@"❌ ห้อง '{accomName}' ถูกจองไปแล้ว!

📋 รายละเอียด:
• ผู้จอง: {conflictCustomer}
• เบอร์โทร: {conflictPhone}
• วันที่เข้าพัก: {conflictCheckin:dd/MM/yyyy}
• วันที่ออก: {conflictCheckout:dd/MM/yyyy}
• หมายเลขการจอง: {conflictReservationId}

⚠️ ไม่สามารถจองห้องนี้ได้ กรุณาเลือกห้องอื่น หรือเลือกวันที่อื่น";

            result.ConflictDetails = $"Reservation ID: {conflictReservationId}, Customer: {conflictCustomer}, " +
                                     $"Dates: {conflictCheckin:yyyy-MM-dd} to {conflictCheckout:yyyy-MM-dd}";
        }

        /// <summary>
        /// ✅ ตรวจสอบความพร้อมสำหรับแต่ละวันในช่วงเวลาที่กำหนด
        /// ใช้สำหรับ validation แบบละเอียด (ตรวจทุกวัน)
        /// </summary>
        public Dictionary<DateTime, AvailabilityResult> CheckAvailabilityByDate(
            int accommodationId,
            string accomName,
            DateTime checkinDate,
            int stayDays,
            int requestedPeople,
            int excludeReservationId = 0)
        {
            var results = new Dictionary<DateTime, AvailabilityResult>();

            for (int i = 0; i < stayDays; i++)
            {
                DateTime currentDate = checkinDate.AddDays(i);
                DateTime nextDate = currentDate.AddDays(1);

                var dayResult = CheckAvailability(
                    accommodationId,
                    accomName,
                    currentDate,
                    nextDate,
                    requestedPeople,
                    excludeReservationId
                );

                results[currentDate] = dayResult;
            }

            return results;
        }
    }
}
