using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// AI Report Service
    /// Generates daily, weekly, and monthly AI-powered summaries using DeepSeek
    /// with KPI data gathered from the hotel management system.
    /// </summary>
    public class AIReportService
    {
        private readonly string _connStr;
        private readonly code _code;

        public AIReportService(string connectionString)
        {
            _connStr = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _code = new code();
        }

        #region Data Gathering Methods

        /// <summary>
        /// Gather daily KPIs for a specific date
        /// </summary>
        private Dictionary<string, object> GatherDailyKPIs(DateTime date)
        {
            var kpis = new Dictionary<string, object>();
            string dateStr = date.ToString("yyyy-MM-dd");

            try
            {
                // Checkins today
                DataTable dtCheckins = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT COUNT(*) AS Cnt FROM Reservation
                      WHERE CAST(CheckinDate AS DATE) = @Date
                        AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                    new Dictionary<string, object> { { "@Date", dateStr } });
                kpis["checkinsToday"] = dtCheckins.Rows.Count > 0 ? Convert.ToInt32(dtCheckins.Rows[0]["Cnt"]) : 0;

                // Checkouts today
                DataTable dtCheckouts = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT COUNT(*) AS Cnt FROM Reservation
                      WHERE CAST(CheckoutDate AS DATE) = @Date
                        AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                    new Dictionary<string, object> { { "@Date", dateStr } });
                kpis["checkoutsToday"] = dtCheckouts.Rows.Count > 0 ? Convert.ToInt32(dtCheckouts.Rows[0]["Cnt"]) : 0;

                // New bookings today (by Created_Date)
                DataTable dtNewBookings = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT COUNT(*) AS Cnt FROM Reservation
                      WHERE CAST(Created_Date AS DATE) = @Date
                        AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                    new Dictionary<string, object> { { "@Date", dateStr } });
                kpis["newBookings"] = dtNewBookings.Rows.Count > 0 ? Convert.ToInt32(dtNewBookings.Rows[0]["Cnt"]) : 0;

                // Cancellations today
                DataTable dtCancellations = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT COUNT(*) AS Cnt FROM Reservation
                      WHERE CAST(Created_Date AS DATE) = @Date
                        AND Status IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')",
                    new Dictionary<string, object> { { "@Date", dateStr } });
                kpis["cancellations"] = dtCancellations.Rows.Count > 0 ? Convert.ToInt32(dtCancellations.Rows[0]["Cnt"]) : 0;

                // Revenue: SUM(TotalPrice) from reservations with CheckinDate = today
                DataTable dtRevenue = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT ISNULL(SUM(TotalPrice), 0) AS Revenue FROM Reservation
                      WHERE CAST(CheckinDate AS DATE) = @Date
                        AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                    new Dictionary<string, object> { { "@Date", dateStr } });
                kpis["revenue"] = dtRevenue.Rows.Count > 0 ? Convert.ToDecimal(dtRevenue.Rows[0]["Revenue"]) : 0m;

                // Occupancy: booked rooms vs total rooms
                // Booked rooms (handling LimitWithPeople)
                DataTable dtBookedRooms = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT COUNT(DISTINCT ra.Accommodation_ID) AS BookedRooms
                      FROM Reservation_Accommodation ra
                      JOIN Reservation r ON r.ID = ra.Reservation_ID
                      WHERE r.CheckinDate <= @Date AND r.CheckoutDate > @Date
                        AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                    new Dictionary<string, object> { { "@Date", dateStr } });
                int bookedRooms = dtBookedRooms.Rows.Count > 0 ? Convert.ToInt32(dtBookedRooms.Rows[0]["BookedRooms"]) : 0;
                kpis["bookedRooms"] = bookedRooms;

                // Total rooms
                DataTable dtTotalRooms = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT COUNT(*) AS TotalRooms FROM Accommodation WHERE Status = 1", null);
                int totalRooms = dtTotalRooms.Rows.Count > 0 ? Convert.ToInt32(dtTotalRooms.Rows[0]["TotalRooms"]) : 1;
                kpis["totalRooms"] = totalRooms;

                // Booked people (for LimitWithPeople accommodations)
                DataTable dtBookedPeople = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT ISNULL(SUM(ra.Amount), 0) AS BookedPeople
                      FROM Reservation_Accommodation ra
                      JOIN Reservation r ON r.ID = ra.Reservation_ID
                      JOIN Accommodation a ON a.ID = ra.Accommodation_ID
                      WHERE r.CheckinDate <= @Date AND r.CheckoutDate > @Date
                        AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')
                        AND a.LimitWithPeople = 'True'",
                    new Dictionary<string, object> { { "@Date", dateStr } });
                int bookedPeople = dtBookedPeople.Rows.Count > 0 ? Convert.ToInt32(dtBookedPeople.Rows[0]["BookedPeople"]) : 0;

                DataTable dtTotalPeopleCapacity = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT ISNULL(SUM(People), 0) AS TotalCapacity FROM Accommodation
                      WHERE Status = 1 AND LimitWithPeople = 'True'", null);
                int totalPeopleCapacity = dtTotalPeopleCapacity.Rows.Count > 0 ? Convert.ToInt32(dtTotalPeopleCapacity.Rows[0]["TotalCapacity"]) : 0;

                // Count non-LimitWithPeople rooms
                DataTable dtNonLimitRooms = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT COUNT(*) AS Cnt FROM Accommodation
                      WHERE Status = 1 AND (LimitWithPeople = 'False' OR LimitWithPeople IS NULL)", null);
                int nonLimitRooms = dtNonLimitRooms.Rows.Count > 0 ? Convert.ToInt32(dtNonLimitRooms.Rows[0]["Cnt"]) : 0;

                DataTable dtBookedNonLimit = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT COUNT(DISTINCT ra.Accommodation_ID) AS Cnt
                      FROM Reservation_Accommodation ra
                      JOIN Reservation r ON r.ID = ra.Reservation_ID
                      JOIN Accommodation a ON a.ID = ra.Accommodation_ID
                      WHERE r.CheckinDate <= @Date AND r.CheckoutDate > @Date
                        AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')
                        AND (a.LimitWithPeople = 'False' OR a.LimitWithPeople IS NULL)",
                    new Dictionary<string, object> { { "@Date", dateStr } });
                int bookedNonLimit = dtBookedNonLimit.Rows.Count > 0 ? Convert.ToInt32(dtBookedNonLimit.Rows[0]["Cnt"]) : 0;

                // Calculate occupancy rate combining both types
                double occupancy = 0;
                int totalUnits = nonLimitRooms + totalPeopleCapacity;
                int bookedUnits = bookedNonLimit + bookedPeople;
                if (totalUnits > 0)
                    occupancy = Math.Round((double)bookedUnits / totalUnits * 100, 1);
                kpis["occupancyRate"] = occupancy;

                // Active guests count (currently checked-in)
                DataTable dtActiveGuests = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT COUNT(DISTINCT r.Customer_MobilePhone) AS Cnt FROM Reservation r
                      WHERE r.CheckinDate <= @Date AND r.CheckoutDate > @Date
                        AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                    new Dictionary<string, object> { { "@Date", dateStr } });
                kpis["activeGuests"] = dtActiveGuests.Rows.Count > 0 ? Convert.ToInt32(dtActiveGuests.Rows[0]["Cnt"]) : 0;

                // Room Service orders today
                try
                {
                    DataTable dtRoomService = _code.DatabaseQuerySafe(_connStr,
                        @"SELECT COUNT(*) AS Cnt FROM Room_Service_Orders
                          WHERE CAST(Created_Date AS DATE) = @Date",
                        new Dictionary<string, object> { { "@Date", dateStr } });
                    kpis["roomServiceOrders"] = dtRoomService.Rows.Count > 0 ? Convert.ToInt32(dtRoomService.Rows[0]["Cnt"]) : 0;
                }
                catch { kpis["roomServiceOrders"] = 0; }

                // Housekeeping: pending/completed today
                try
                {
                    DataTable dtHKPending = _code.DatabaseQuerySafe(_connStr,
                        @"SELECT COUNT(*) AS Cnt FROM Room_Status
                          WHERE CAST(Status_Date AS DATE) = @Date
                            AND Status IN ('DIRTY', 'CLEANING', 'INSPECTING')",
                        new Dictionary<string, object> { { "@Date", dateStr } });
                    kpis["housekeepingPending"] = dtHKPending.Rows.Count > 0 ? Convert.ToInt32(dtHKPending.Rows[0]["Cnt"]) : 0;

                    DataTable dtHKCompleted = _code.DatabaseQuerySafe(_connStr,
                        @"SELECT COUNT(*) AS Cnt FROM Room_Status
                          WHERE CAST(Status_Date AS DATE) = @Date
                            AND Status IN ('VACANT_CLEAN', 'INSPECTED')",
                        new Dictionary<string, object> { { "@Date", dateStr } });
                    kpis["housekeepingCompleted"] = dtHKCompleted.Rows.Count > 0 ? Convert.ToInt32(dtHKCompleted.Rows[0]["Cnt"]) : 0;
                }
                catch
                {
                    kpis["housekeepingPending"] = 0;
                    kpis["housekeepingCompleted"] = 0;
                }

                // Chat messages today
                try
                {
                    DataTable dtChat = _code.DatabaseQuerySafe(_connStr,
                        @"SELECT COUNT(*) AS Cnt FROM OmniChannel_Messages
                          WHERE CAST(Created_Date AS DATE) = @Date",
                        new Dictionary<string, object> { { "@Date", dateStr } });
                    kpis["chatMessages"] = dtChat.Rows.Count > 0 ? Convert.ToInt32(dtChat.Rows[0]["Cnt"]) : 0;
                }
                catch { kpis["chatMessages"] = 0; }

                // Reviews today
                try
                {
                    DataTable dtReviews = _code.DatabaseQuerySafe(_connStr,
                        @"SELECT COUNT(*) AS Cnt FROM Guest_Reviews
                          WHERE CAST(Created_Date AS DATE) = @Date",
                        new Dictionary<string, object> { { "@Date", dateStr } });
                    kpis["newReviews"] = dtReviews.Rows.Count > 0 ? Convert.ToInt32(dtReviews.Rows[0]["Cnt"]) : 0;
                }
                catch { kpis["newReviews"] = 0; }
            }
            catch (Exception ex)
            {
                kpis["error"] = ex.Message;
            }

            return kpis;
        }

        /// <summary>
        /// Gather monthly KPIs for a given period
        /// </summary>
        private Dictionary<string, object> GatherMonthlyKPIs(DateTime monthStart, DateTime monthEnd)
        {
            var kpis = new Dictionary<string, object>();
            string startStr = monthStart.ToString("yyyy-MM-dd");
            string endStr = monthEnd.ToString("yyyy-MM-dd");

            try
            {
                // Total revenue for the month
                DataTable dtRevenue = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT ISNULL(SUM(TotalPrice), 0) AS TotalRevenue FROM Reservation
                      WHERE CheckinDate >= @Start AND CheckinDate <= @End
                        AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                    new Dictionary<string, object> { { "@Start", startStr }, { "@End", endStr } });
                decimal totalRevenue = dtRevenue.Rows.Count > 0 ? Convert.ToDecimal(dtRevenue.Rows[0]["TotalRevenue"]) : 0m;
                kpis["totalRevenue"] = totalRevenue;

                // Revenue by accommodation type (group by AccomName)
                DataTable dtRevenueByType = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT a.AccomName, ISNULL(SUM(r.TotalPrice), 0) AS Revenue, COUNT(*) AS BookingCount
                      FROM Reservation r
                      JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                      JOIN Accommodation a ON a.ID = ra.Accommodation_ID
                      WHERE r.CheckinDate >= @Start AND r.CheckinDate <= @End
                        AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')
                      GROUP BY a.AccomName
                      ORDER BY Revenue DESC",
                    new Dictionary<string, object> { { "@Start", startStr }, { "@End", endStr } });
                var revenueByType = new List<Dictionary<string, object>>();
                foreach (DataRow row in dtRevenueByType.Rows)
                {
                    revenueByType.Add(new Dictionary<string, object>
                    {
                        { "name", row["AccomName"].ToString() },
                        { "revenue", Convert.ToDecimal(row["Revenue"]) },
                        { "bookings", Convert.ToInt32(row["BookingCount"]) }
                    });
                }
                kpis["revenueByType"] = revenueByType;

                // Total reservations
                DataTable dtTotalRes = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT COUNT(*) AS Cnt FROM Reservation
                      WHERE CheckinDate >= @Start AND CheckinDate <= @End
                        AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                    new Dictionary<string, object> { { "@Start", startStr }, { "@End", endStr } });
                int totalReservations = dtTotalRes.Rows.Count > 0 ? Convert.ToInt32(dtTotalRes.Rows[0]["Cnt"]) : 0;
                kpis["totalReservations"] = totalReservations;

                // Total cancellations
                DataTable dtCancel = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT COUNT(*) AS Cnt FROM Reservation
                      WHERE CheckinDate >= @Start AND CheckinDate <= @End
                        AND Status IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')",
                    new Dictionary<string, object> { { "@Start", startStr }, { "@End", endStr } });
                kpis["totalCancellations"] = dtCancel.Rows.Count > 0 ? Convert.ToInt32(dtCancel.Rows[0]["Cnt"]) : 0;

                // Average occupancy rate for the month
                int daysInMonth = (monthEnd - monthStart).Days + 1;

                DataTable dtTotalRooms = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT COUNT(*) AS TotalRooms FROM Accommodation WHERE Status = 1", null);
                int totalRooms = dtTotalRooms.Rows.Count > 0 ? Convert.ToInt32(dtTotalRooms.Rows[0]["TotalRooms"]) : 1;

                DataTable dtTotalRoomNights = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT ISNULL(SUM(r.StayDays), 0) AS TotalNights FROM Reservation r
                      WHERE r.CheckinDate >= @Start AND r.CheckinDate <= @End
                        AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                    new Dictionary<string, object> { { "@Start", startStr }, { "@End", endStr } });
                int totalRoomNights = dtTotalRoomNights.Rows.Count > 0 ? Convert.ToInt32(dtTotalRoomNights.Rows[0]["TotalNights"]) : 0;
                kpis["totalRoomNights"] = totalRoomNights;

                int availableRoomNights = totalRooms * daysInMonth;
                double avgOccupancy = availableRoomNights > 0 ? Math.Round((double)totalRoomNights / availableRoomNights * 100, 1) : 0;
                kpis["avgOccupancyRate"] = avgOccupancy;

                // ADR (Average Daily Rate): total revenue / total room nights
                decimal adr = totalRoomNights > 0 ? Math.Round(totalRevenue / totalRoomNights, 2) : 0m;
                kpis["adr"] = adr;

                // RevPAR: ADR * occupancy rate
                decimal revpar = Math.Round(adr * (decimal)(avgOccupancy / 100.0), 2);
                kpis["revpar"] = revpar;

                // New customers vs returning
                DataTable dtNewVsReturn = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT
                        SUM(CASE WHEN PrevCount = 0 THEN 1 ELSE 0 END) AS NewCustomers,
                        SUM(CASE WHEN PrevCount > 0 THEN 1 ELSE 0 END) AS ReturningCustomers
                      FROM (
                        SELECT r.Customer_MobilePhone,
                          (SELECT COUNT(*) FROM Reservation r2
                           WHERE r2.Customer_MobilePhone = r.Customer_MobilePhone
                             AND r2.CheckinDate < r.CheckinDate
                             AND r2.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')) AS PrevCount
                        FROM Reservation r
                        WHERE r.CheckinDate >= @Start AND r.CheckinDate <= @End
                          AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')
                      ) t",
                    new Dictionary<string, object> { { "@Start", startStr }, { "@End", endStr } });
                if (dtNewVsReturn.Rows.Count > 0)
                {
                    kpis["newCustomers"] = dtNewVsReturn.Rows[0]["NewCustomers"] != DBNull.Value ? Convert.ToInt32(dtNewVsReturn.Rows[0]["NewCustomers"]) : 0;
                    kpis["returningCustomers"] = dtNewVsReturn.Rows[0]["ReturningCustomers"] != DBNull.Value ? Convert.ToInt32(dtNewVsReturn.Rows[0]["ReturningCustomers"]) : 0;
                }
                else
                {
                    kpis["newCustomers"] = 0;
                    kpis["returningCustomers"] = 0;
                }

                // Total product sales
                try
                {
                    DataTable dtProducts = _code.DatabaseQuerySafe(_connStr,
                        @"SELECT ISNULL(SUM(TotalPrice), 0) AS TotalSales, COUNT(*) AS OrderCount
                          FROM Product_Orders
                          WHERE CAST(Created_Date AS DATE) >= @Start AND CAST(Created_Date AS DATE) <= @End",
                        new Dictionary<string, object> { { "@Start", startStr }, { "@End", endStr } });
                    if (dtProducts.Rows.Count > 0)
                    {
                        kpis["productSales"] = Convert.ToDecimal(dtProducts.Rows[0]["TotalSales"]);
                        kpis["productOrderCount"] = Convert.ToInt32(dtProducts.Rows[0]["OrderCount"]);
                    }
                    else
                    {
                        kpis["productSales"] = 0m;
                        kpis["productOrderCount"] = 0;
                    }
                }
                catch
                {
                    kpis["productSales"] = 0m;
                    kpis["productOrderCount"] = 0;
                }

                // Review count and average rating
                try
                {
                    DataTable dtReviews = _code.DatabaseQuerySafe(_connStr,
                        @"SELECT COUNT(*) AS ReviewCount, ISNULL(AVG(CAST(OverallRating AS FLOAT)), 0) AS AvgRating
                          FROM Guest_Reviews
                          WHERE CAST(Created_Date AS DATE) >= @Start AND CAST(Created_Date AS DATE) <= @End",
                        new Dictionary<string, object> { { "@Start", startStr }, { "@End", endStr } });
                    if (dtReviews.Rows.Count > 0)
                    {
                        kpis["reviewCount"] = Convert.ToInt32(dtReviews.Rows[0]["ReviewCount"]);
                        kpis["avgRating"] = Math.Round(Convert.ToDouble(dtReviews.Rows[0]["AvgRating"]), 1);
                    }
                    else
                    {
                        kpis["reviewCount"] = 0;
                        kpis["avgRating"] = 0.0;
                    }
                }
                catch
                {
                    kpis["reviewCount"] = 0;
                    kpis["avgRating"] = 0.0;
                }

                // Comparison with previous month
                DateTime prevMonthStart = monthStart.AddMonths(-1);
                DateTime prevMonthEnd = new DateTime(prevMonthStart.Year, prevMonthStart.Month,
                    DateTime.DaysInMonth(prevMonthStart.Year, prevMonthStart.Month));
                string prevStartStr = prevMonthStart.ToString("yyyy-MM-dd");
                string prevEndStr = prevMonthEnd.ToString("yyyy-MM-dd");

                DataTable dtPrevRevenue = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT ISNULL(SUM(TotalPrice), 0) AS PrevRevenue FROM Reservation
                      WHERE CheckinDate >= @Start AND CheckinDate <= @End
                        AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                    new Dictionary<string, object> { { "@Start", prevStartStr }, { "@End", prevEndStr } });
                decimal prevRevenue = dtPrevRevenue.Rows.Count > 0 ? Convert.ToDecimal(dtPrevRevenue.Rows[0]["PrevRevenue"]) : 0m;
                kpis["prevMonthRevenue"] = prevRevenue;
                kpis["revenueGrowthPct"] = prevRevenue > 0 ? Math.Round((double)((totalRevenue - prevRevenue) / prevRevenue * 100), 1) : 0.0;

                DataTable dtPrevOccupancy = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT ISNULL(SUM(r.StayDays), 0) AS PrevNights FROM Reservation r
                      WHERE r.CheckinDate >= @Start AND r.CheckinDate <= @End
                        AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')",
                    new Dictionary<string, object> { { "@Start", prevStartStr }, { "@End", prevEndStr } });
                int prevRoomNights = dtPrevOccupancy.Rows.Count > 0 ? Convert.ToInt32(dtPrevOccupancy.Rows[0]["PrevNights"]) : 0;
                int prevDays = (prevMonthEnd - prevMonthStart).Days + 1;
                int prevAvailableNights = totalRooms * prevDays;
                double prevOccupancy = prevAvailableNights > 0 ? Math.Round((double)prevRoomNights / prevAvailableNights * 100, 1) : 0;
                kpis["prevMonthOccupancy"] = prevOccupancy;
                kpis["occupancyChange"] = Math.Round(avgOccupancy - prevOccupancy, 1);

                // Top 5 revenue days
                DataTable dtTopDays = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT TOP 5 CAST(CheckinDate AS DATE) AS RevenueDate, SUM(TotalPrice) AS DayRevenue
                      FROM Reservation
                      WHERE CheckinDate >= @Start AND CheckinDate <= @End
                        AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')
                      GROUP BY CAST(CheckinDate AS DATE)
                      ORDER BY DayRevenue DESC",
                    new Dictionary<string, object> { { "@Start", startStr }, { "@End", endStr } });
                var topDays = new List<Dictionary<string, object>>();
                foreach (DataRow row in dtTopDays.Rows)
                {
                    topDays.Add(new Dictionary<string, object>
                    {
                        { "date", Convert.ToDateTime(row["RevenueDate"]).ToString("dd/MM/yyyy") },
                        { "revenue", Convert.ToDecimal(row["DayRevenue"]) }
                    });
                }
                kpis["topRevenueDays"] = topDays;

                // Busiest day of week
                DataTable dtBusyDay = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT TOP 1 DATEPART(WEEKDAY, CheckinDate) AS DayOfWeek, COUNT(*) AS Cnt
                      FROM Reservation
                      WHERE CheckinDate >= @Start AND CheckinDate <= @End
                        AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')
                      GROUP BY DATEPART(WEEKDAY, CheckinDate)
                      ORDER BY Cnt DESC",
                    new Dictionary<string, object> { { "@Start", startStr }, { "@End", endStr } });
                if (dtBusyDay.Rows.Count > 0)
                {
                    int dow = Convert.ToInt32(dtBusyDay.Rows[0]["DayOfWeek"]);
                    string[] thaiDays = { "", "อาทิตย์", "จันทร์", "อังคาร", "พุธ", "พฤหัสบดี", "ศุกร์", "เสาร์" };
                    kpis["busiestDayOfWeek"] = dow >= 1 && dow <= 7 ? thaiDays[dow] : "ไม่ทราบ";
                    kpis["busiestDayCount"] = Convert.ToInt32(dtBusyDay.Rows[0]["Cnt"]);
                }
                else
                {
                    kpis["busiestDayOfWeek"] = "ไม่มีข้อมูล";
                    kpis["busiestDayCount"] = 0;
                }
            }
            catch (Exception ex)
            {
                kpis["error"] = ex.Message;
            }

            return kpis;
        }

        /// <summary>
        /// Gather customer insights for a given period
        /// </summary>
        private Dictionary<string, object> GatherCustomerInsights(DateTime from, DateTime to)
        {
            var insights = new Dictionary<string, object>();
            string fromStr = from.ToString("yyyy-MM-dd");
            string toStr = to.ToString("yyyy-MM-dd");

            try
            {
                // New vs returning customers
                DataTable dtNewReturn = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT
                        SUM(CASE WHEN PrevCount = 0 THEN 1 ELSE 0 END) AS NewCustomers,
                        SUM(CASE WHEN PrevCount > 0 THEN 1 ELSE 0 END) AS ReturningCustomers
                      FROM (
                        SELECT r.Customer_MobilePhone,
                          (SELECT COUNT(*) FROM Reservation r2
                           WHERE r2.Customer_MobilePhone = r.Customer_MobilePhone
                             AND r2.CheckinDate < r.CheckinDate
                             AND r2.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')) AS PrevCount
                        FROM Reservation r
                        WHERE r.CheckinDate >= @From AND r.CheckinDate <= @To
                          AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')
                      ) t",
                    new Dictionary<string, object> { { "@From", fromStr }, { "@To", toStr } });
                if (dtNewReturn.Rows.Count > 0)
                {
                    insights["newCustomers"] = dtNewReturn.Rows[0]["NewCustomers"] != DBNull.Value ? Convert.ToInt32(dtNewReturn.Rows[0]["NewCustomers"]) : 0;
                    insights["returningCustomers"] = dtNewReturn.Rows[0]["ReturningCustomers"] != DBNull.Value ? Convert.ToInt32(dtNewReturn.Rows[0]["ReturningCustomers"]) : 0;
                }
                else
                {
                    insights["newCustomers"] = 0;
                    insights["returningCustomers"] = 0;
                }

                // Average spend per customer
                DataTable dtAvgSpend = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT ISNULL(AVG(CustomerTotal), 0) AS AvgSpend FROM (
                        SELECT Customer_MobilePhone, SUM(TotalPrice) AS CustomerTotal
                        FROM Reservation
                        WHERE CheckinDate >= @From AND CheckinDate <= @To
                          AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')
                        GROUP BY Customer_MobilePhone
                      ) t",
                    new Dictionary<string, object> { { "@From", fromStr }, { "@To", toStr } });
                insights["avgSpendPerCustomer"] = dtAvgSpend.Rows.Count > 0 ? Convert.ToDecimal(dtAvgSpend.Rows[0]["AvgSpend"]) : 0m;

                // Top 5 customers by revenue
                DataTable dtTopCustomers = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT TOP 5 r.Customer_MobilePhone, ISNULL(c.Name, r.Customer_MobilePhone) AS CustomerName,
                        SUM(r.TotalPrice) AS TotalSpent, COUNT(*) AS BookingCount
                      FROM Reservation r
                      LEFT JOIN Customer c ON c.MobilePhone = r.Customer_MobilePhone
                      WHERE r.CheckinDate >= @From AND r.CheckinDate <= @To
                        AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')
                      GROUP BY r.Customer_MobilePhone, c.Name
                      ORDER BY TotalSpent DESC",
                    new Dictionary<string, object> { { "@From", fromStr }, { "@To", toStr } });
                var topCustomers = new List<Dictionary<string, object>>();
                foreach (DataRow row in dtTopCustomers.Rows)
                {
                    topCustomers.Add(new Dictionary<string, object>
                    {
                        { "name", row["CustomerName"].ToString() },
                        { "phone", row["Customer_MobilePhone"].ToString() },
                        { "totalSpent", Convert.ToDecimal(row["TotalSpent"]) },
                        { "bookings", Convert.ToInt32(row["BookingCount"]) }
                    });
                }
                insights["topCustomers"] = topCustomers;

                // Most popular room types
                DataTable dtPopularRooms = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT a.AccomName, COUNT(*) AS BookingCount
                      FROM Reservation r
                      JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                      JOIN Accommodation a ON a.ID = ra.Accommodation_ID
                      WHERE r.CheckinDate >= @From AND r.CheckinDate <= @To
                        AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')
                      GROUP BY a.AccomName
                      ORDER BY BookingCount DESC",
                    new Dictionary<string, object> { { "@From", fromStr }, { "@To", toStr } });
                var popularRooms = new List<Dictionary<string, object>>();
                foreach (DataRow row in dtPopularRooms.Rows)
                {
                    popularRooms.Add(new Dictionary<string, object>
                    {
                        { "name", row["AccomName"].ToString() },
                        { "bookings", Convert.ToInt32(row["BookingCount"]) }
                    });
                }
                insights["popularRoomTypes"] = popularRooms;

                // Booking channel distribution (Reserve_By field)
                DataTable dtChannels = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT ISNULL(Reserve_By, N'ไม่ระบุ') AS Channel, COUNT(*) AS Cnt
                      FROM Reservation
                      WHERE CheckinDate >= @From AND CheckinDate <= @To
                        AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น')
                      GROUP BY Reserve_By
                      ORDER BY Cnt DESC",
                    new Dictionary<string, object> { { "@From", fromStr }, { "@To", toStr } });
                var channels = new Dictionary<string, int>();
                foreach (DataRow row in dtChannels.Rows)
                {
                    channels[row["Channel"].ToString()] = Convert.ToInt32(row["Cnt"]);
                }
                insights["bookingChannels"] = channels;
            }
            catch (Exception ex)
            {
                insights["error"] = ex.Message;
            }

            return insights;
        }

        #endregion

        #region AI Summary Generation

        /// <summary>
        /// Generate a daily AI summary for the given date
        /// </summary>
        public string GenerateDailySummary(DateTime date)
        {
            try
            {
                // Check for cached summary first
                string cached = GetSummaryByPeriod("DAILY", date, date);
                if (!string.IsNullOrEmpty(cached))
                    return cached;

                var kpis = GatherDailyKPIs(date);
                string formattedKPIs = FormatKPIsForAI(kpis);

                string prompt = string.Format(
                    "คุณเป็นผู้จัดการโรงแรม TakeTime BangPhra วิเคราะห์ข้อมูลประจำวันนี้ ({0}):\n\n{1}\n\n" +
                    "ให้สรุปเป็นภาษาไทย แบ่งเป็น:\n" +
                    "1. สรุปภาพรวม (2-3 ประโยค)\n" +
                    "2. ไฮไลท์สำคัญ (bullet points)\n" +
                    "3. จุดที่ต้องให้ความสนใจ\n" +
                    "4. ข้อเสนอแนะสำหรับวันพรุ่งนี้\n\n" +
                    "ใช้ภาษาเป็นทางการแต่อ่านง่าย ไม่ต้องใส่ emoji",
                    date.ToString("dd/MM/yyyy"), formattedKPIs);

                var deepSeek = new DeepSeekService(_connStr);
                string sessionKey = "report_daily_" + date.ToString("yyyyMMdd");
                var response = deepSeek.SendMessage(prompt, sessionKey, null);

                if (response.Success)
                {
                    SaveSummary("DAILY", "DAY", date, date, response.Message, response.TotalTokens);
                    return response.Message;
                }

                return "ไม่สามารถสร้างรายงานได้: " + response.Message;
            }
            catch (Exception ex)
            {
                return "เกิดข้อผิดพลาดในการสร้างรายงาน: " + ex.Message;
            }
        }

        /// <summary>
        /// Generate a weekly AI summary starting from the given date
        /// </summary>
        public string GenerateWeeklySummary(DateTime weekStart)
        {
            try
            {
                DateTime weekEnd = weekStart.AddDays(6);

                // Check for cached summary first
                string cached = GetSummaryByPeriod("WEEKLY", weekStart, weekEnd);
                if (!string.IsNullOrEmpty(cached))
                    return cached;

                // Gather daily KPIs for each day in the week and aggregate
                var weeklyData = new StringBuilder();
                decimal weeklyRevenue = 0;
                int weeklyCheckins = 0;
                int weeklyCheckouts = 0;
                int weeklyNewBookings = 0;
                int weeklyCancellations = 0;
                double totalOccupancy = 0;
                int dayCount = 0;

                for (int i = 0; i < 7; i++)
                {
                    DateTime day = weekStart.AddDays(i);
                    if (day > DateTime.Today) break;

                    var dailyKpis = GatherDailyKPIs(day);
                    dayCount++;

                    weeklyRevenue += dailyKpis.ContainsKey("revenue") ? Convert.ToDecimal(dailyKpis["revenue"]) : 0;
                    weeklyCheckins += dailyKpis.ContainsKey("checkinsToday") ? Convert.ToInt32(dailyKpis["checkinsToday"]) : 0;
                    weeklyCheckouts += dailyKpis.ContainsKey("checkoutsToday") ? Convert.ToInt32(dailyKpis["checkoutsToday"]) : 0;
                    weeklyNewBookings += dailyKpis.ContainsKey("newBookings") ? Convert.ToInt32(dailyKpis["newBookings"]) : 0;
                    weeklyCancellations += dailyKpis.ContainsKey("cancellations") ? Convert.ToInt32(dailyKpis["cancellations"]) : 0;
                    totalOccupancy += dailyKpis.ContainsKey("occupancyRate") ? Convert.ToDouble(dailyKpis["occupancyRate"]) : 0;

                    weeklyData.AppendLine(string.Format("--- {0} ({1}) ---",
                        day.ToString("dd/MM/yyyy"),
                        new CultureInfo("th-TH").DateTimeFormat.GetDayName(day.DayOfWeek)));
                    weeklyData.AppendLine(FormatKPIsForAI(dailyKpis));
                    weeklyData.AppendLine();
                }

                double avgOccupancy = dayCount > 0 ? Math.Round(totalOccupancy / dayCount, 1) : 0;

                var aggregateSummary = new StringBuilder();
                aggregateSummary.AppendLine("=== สรุปรวมสัปดาห์ ===");
                aggregateSummary.AppendLine(string.Format("รายได้รวม: {0:N0} บาท", weeklyRevenue));
                aggregateSummary.AppendLine(string.Format("เช็คอินรวม: {0:N0} ราย", weeklyCheckins));
                aggregateSummary.AppendLine(string.Format("เช็คเอาท์รวม: {0:N0} ราย", weeklyCheckouts));
                aggregateSummary.AppendLine(string.Format("การจองใหม่รวม: {0:N0} รายการ", weeklyNewBookings));
                aggregateSummary.AppendLine(string.Format("ยกเลิกรวม: {0:N0} รายการ", weeklyCancellations));
                aggregateSummary.AppendLine(string.Format("Occupancy เฉลี่ย: {0}%", avgOccupancy));
                aggregateSummary.AppendLine();
                aggregateSummary.AppendLine("=== รายละเอียดรายวัน ===");
                aggregateSummary.Append(weeklyData);

                string prompt = string.Format(
                    "คุณเป็นผู้จัดการโรงแรม TakeTime BangPhra วิเคราะห์ข้อมูลประจำสัปดาห์ ({0} - {1}):\n\n{2}\n\n" +
                    "ให้สรุปเป็นรายงานสัปดาห์ภาษาไทย แบ่งเป็น:\n" +
                    "1. สรุปภาพรวมสัปดาห์ (2-3 ประโยค)\n" +
                    "2. ไฮไลท์สำคัญของสัปดาห์ (bullet points)\n" +
                    "3. แนวโน้มที่สังเกตได้ (เปรียบเทียบวันต่อวัน)\n" +
                    "4. จุดที่ต้องปรับปรุง\n" +
                    "5. ข้อเสนอแนะสำหรับสัปดาห์ถัดไป\n\n" +
                    "ใช้ภาษาเป็นทางการแต่อ่านง่าย ไม่ต้องใส่ emoji",
                    weekStart.ToString("dd/MM/yyyy"), weekEnd.ToString("dd/MM/yyyy"), aggregateSummary.ToString());

                var deepSeek = new DeepSeekService(_connStr);
                string sessionKey = "report_weekly_" + weekStart.ToString("yyyyMMdd");
                var response = deepSeek.SendMessage(prompt, sessionKey, null);

                if (response.Success)
                {
                    SaveSummary("WEEKLY", "WEEK", weekStart, weekEnd, response.Message, response.TotalTokens);
                    return response.Message;
                }

                return "ไม่สามารถสร้างรายงานได้: " + response.Message;
            }
            catch (Exception ex)
            {
                return "เกิดข้อผิดพลาดในการสร้างรายงาน: " + ex.Message;
            }
        }

        /// <summary>
        /// Generate a monthly AI summary for the given month
        /// </summary>
        public string GenerateMonthlySummary(DateTime month)
        {
            try
            {
                DateTime monthStart = new DateTime(month.Year, month.Month, 1);
                DateTime monthEnd = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));

                // Check for cached summary first
                string cached = GetSummaryByPeriod("MONTHLY", monthStart, monthEnd);
                if (!string.IsNullOrEmpty(cached))
                    return cached;

                var monthlyKPIs = GatherMonthlyKPIs(monthStart, monthEnd);
                var customerInsights = GatherCustomerInsights(monthStart, monthEnd);

                var formattedData = new StringBuilder();
                formattedData.AppendLine("=== ข้อมูลผลประกอบการ ===");
                formattedData.AppendLine(FormatKPIsForAI(monthlyKPIs));
                formattedData.AppendLine();
                formattedData.AppendLine("=== ข้อมูลลูกค้า ===");
                formattedData.AppendLine(FormatKPIsForAI(customerInsights));

                string monthName = month.ToString("MMMM yyyy", new CultureInfo("th-TH"));

                string prompt = string.Format(
                    "วิเคราะห์ผลประกอบการประจำเดือน {0} ของโรงแรม TakeTime BangPhra:\n\n{1}\n\n" +
                    "ให้สรุปเป็นรายงานภาษาไทย แบ่งเป็น:\n" +
                    "1. สรุปผลประกอบการ (Executive Summary)\n" +
                    "2. รายได้และ Occupancy\n" +
                    "3. วิเคราะห์ลูกค้า\n" +
                    "4. เปรียบเทียบกับเดือนก่อน\n" +
                    "5. จุดแข็งและจุดอ่อน\n" +
                    "6. แผนการดำเนินงานเดือนถัดไป\n\n" +
                    "ใช้ภาษาเป็นทางการแต่อ่านง่าย ไม่ต้องใส่ emoji",
                    monthName, formattedData.ToString());

                var deepSeek = new DeepSeekService(_connStr);
                string sessionKey = "report_monthly_" + month.ToString("yyyyMM");
                var response = deepSeek.SendMessage(prompt, sessionKey, null);

                if (response.Success)
                {
                    SaveSummary("MONTHLY", "MONTH", monthStart, monthEnd, response.Message, response.TotalTokens);
                    return response.Message;
                }

                return "ไม่สามารถสร้างรายงานได้: " + response.Message;
            }
            catch (Exception ex)
            {
                return "เกิดข้อผิดพลาดในการสร้างรายงาน: " + ex.Message;
            }
        }

        /// <summary>
        /// Save a generated summary to the database
        /// </summary>
        private void SaveSummary(string reportType, string periodType, DateTime periodStart, DateTime periodEnd, string summary, int tokensUsed)
        {
            try
            {
                _code.DatabaseInsertSafe(_connStr,
                    @"INSERT INTO AI_Report_Summaries (ReportType, PeriodType, PeriodStart, PeriodEnd, AISummary, TokensUsed, Created_Date)
                      VALUES (@ReportType, @PeriodType, @PeriodStart, @PeriodEnd, @Summary, @TokensUsed, GETDATE())",
                    new Dictionary<string, object>
                    {
                        { "@ReportType", reportType },
                        { "@PeriodType", periodType },
                        { "@PeriodStart", periodStart },
                        { "@PeriodEnd", periodEnd },
                        { "@Summary", summary },
                        { "@TokensUsed", tokensUsed }
                    });
            }
            catch { }
        }

        #endregion

        #region Cached Summary Retrieval

        /// <summary>
        /// Get the most recent summary of the given report type
        /// </summary>
        public string GetLatestSummary(string reportType)
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT TOP 1 AISummary FROM AI_Report_Summaries
                      WHERE ReportType = @ReportType
                      ORDER BY Created_Date DESC",
                    new Dictionary<string, object> { { "@ReportType", reportType } });
                return dt.Rows.Count > 0 ? dt.Rows[0]["AISummary"].ToString() : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the last N summaries of the given report type
        /// </summary>
        public DataTable GetSummaryHistory(string reportType, int limit = 10)
        {
            try
            {
                return _code.DatabaseQuerySafe(_connStr,
                    @"SELECT TOP (@Limit) ID, ReportType, PeriodType, PeriodStart, PeriodEnd, AISummary, TokensUsed, Created_Date
                      FROM AI_Report_Summaries
                      WHERE ReportType = @ReportType
                      ORDER BY Created_Date DESC",
                    new Dictionary<string, object>
                    {
                        { "@ReportType", reportType },
                        { "@Limit", limit }
                    });
            }
            catch
            {
                return new DataTable();
            }
        }

        /// <summary>
        /// Check if a cached summary exists for the given period
        /// </summary>
        public string GetSummaryByPeriod(string reportType, DateTime periodStart, DateTime periodEnd)
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT TOP 1 AISummary FROM AI_Report_Summaries
                      WHERE ReportType = @ReportType
                        AND CAST(PeriodStart AS DATE) = @PeriodStart
                        AND CAST(PeriodEnd AS DATE) = @PeriodEnd
                      ORDER BY Created_Date DESC",
                    new Dictionary<string, object>
                    {
                        { "@ReportType", reportType },
                        { "@PeriodStart", periodStart.ToString("yyyy-MM-dd") },
                        { "@PeriodEnd", periodEnd.ToString("yyyy-MM-dd") }
                    });
                return dt.Rows.Count > 0 ? dt.Rows[0]["AISummary"].ToString() : null;
            }
            catch { return null; }
        }

        #endregion

        #region Quick Insights

        /// <summary>
        /// Get quick insights with today's KPIs, monthly running totals, and AI-generated alerts
        /// </summary>
        public Dictionary<string, object> GetQuickInsights()
        {
            var insights = new Dictionary<string, object>();

            try
            {
                // Today's KPIs
                var todayKPIs = GatherDailyKPIs(DateTime.Today);
                insights["todayKPIs"] = todayKPIs;

                // This month's running totals
                DateTime monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                DateTime monthEnd = DateTime.Today;
                var monthlyKPIs = GatherMonthlyKPIs(monthStart, monthEnd);
                insights["monthRunningTotals"] = monthlyKPIs;

                // Alerts
                var alerts = new List<string>();

                // Check occupancy alert
                double occupancy = todayKPIs.ContainsKey("occupancyRate") ? Convert.ToDouble(todayKPIs["occupancyRate"]) : 0;
                if (occupancy < 50)
                    alerts.Add(string.Format("Occupancy ต่ำกว่า 50% (ปัจจุบัน {0}%)", occupancy));
                if (occupancy > 90)
                    alerts.Add(string.Format("Occupancy สูงมาก ({0}%) - ตรวจสอบความพร้อมของห้องพัก", occupancy));

                // Check cancellation rate alert
                int newBookings = todayKPIs.ContainsKey("newBookings") ? Convert.ToInt32(todayKPIs["newBookings"]) : 0;
                int cancellations = todayKPIs.ContainsKey("cancellations") ? Convert.ToInt32(todayKPIs["cancellations"]) : 0;
                if (newBookings > 0 && cancellations > 0)
                {
                    double cancelRate = (double)cancellations / (newBookings + cancellations) * 100;
                    if (cancelRate > 30)
                        alerts.Add(string.Format("อัตราการยกเลิกสูง ({0:F1}%) - ควรตรวจสอบสาเหตุ", cancelRate));
                }

                // Monthly revenue alert
                decimal monthlyRevenue = monthlyKPIs.ContainsKey("totalRevenue") ? Convert.ToDecimal(monthlyKPIs["totalRevenue"]) : 0;
                decimal prevMonthRevenue = monthlyKPIs.ContainsKey("prevMonthRevenue") ? Convert.ToDecimal(monthlyKPIs["prevMonthRevenue"]) : 0;
                if (prevMonthRevenue > 0)
                {
                    double revenueChange = (double)((monthlyRevenue - prevMonthRevenue) / prevMonthRevenue * 100);
                    if (revenueChange < -20)
                        alerts.Add(string.Format("รายได้เดือนนี้ลดลง {0:F1}% เทียบกับเดือนก่อน", Math.Abs(revenueChange)));
                }

                // Housekeeping alert
                int hkPending = todayKPIs.ContainsKey("housekeepingPending") ? Convert.ToInt32(todayKPIs["housekeepingPending"]) : 0;
                if (hkPending > 5)
                    alerts.Add(string.Format("มีงานแม่บ้านค้าง {0} ห้อง - ควรเร่งดำเนินการ", hkPending));

                insights["alerts"] = alerts;

                // Generate 1-line AI insight for key metrics
                var quickInsightLines = new List<string>();
                quickInsightLines.Add(string.Format("รายได้วันนี้: {0:N0} บาท", todayKPIs.ContainsKey("revenue") ? Convert.ToDecimal(todayKPIs["revenue"]) : 0));
                quickInsightLines.Add(string.Format("Occupancy: {0}%", occupancy));
                quickInsightLines.Add(string.Format("เช็คอิน/เช็คเอาท์: {0}/{1}",
                    todayKPIs.ContainsKey("checkinsToday") ? todayKPIs["checkinsToday"] : 0,
                    todayKPIs.ContainsKey("checkoutsToday") ? todayKPIs["checkoutsToday"] : 0));
                quickInsightLines.Add(string.Format("รายได้เดือนนี้สะสม: {0:N0} บาท", monthlyRevenue));
                insights["quickInsightLines"] = quickInsightLines;
            }
            catch (Exception ex)
            {
                insights["error"] = ex.Message;
            }

            return insights;
        }

        #endregion

        #region KPI Formatting Helper

        /// <summary>
        /// Format a KPI dictionary into a readable text block for DeepSeek
        /// </summary>
        private string FormatKPIsForAI(Dictionary<string, object> kpis)
        {
            if (kpis == null || kpis.Count == 0)
                return "(ไม่มีข้อมูล)";

            var sb = new StringBuilder();

            foreach (var kvp in kpis)
            {
                if (kvp.Key == "error") continue;

                string label = GetThaiLabel(kvp.Key);

                if (kvp.Value is decimal decVal)
                {
                    sb.AppendLine(string.Format("- {0}: {1:N0} บาท", label, decVal));
                }
                else if (kvp.Value is double dblVal)
                {
                    if (kvp.Key.Contains("Rate") || kvp.Key.Contains("occupancy") || kvp.Key.Contains("Pct") || kvp.Key.Contains("Change"))
                        sb.AppendLine(string.Format("- {0}: {1}%", label, dblVal));
                    else
                        sb.AppendLine(string.Format("- {0}: {1}", label, dblVal));
                }
                else if (kvp.Value is int intVal)
                {
                    sb.AppendLine(string.Format("- {0}: {1:N0}", label, intVal));
                }
                else if (kvp.Value is List<Dictionary<string, object>> listVal)
                {
                    sb.AppendLine(string.Format("- {0}:", label));
                    foreach (var item in listVal)
                    {
                        var parts = new List<string>();
                        foreach (var p in item)
                        {
                            if (p.Value is decimal pd)
                                parts.Add(string.Format("{0}: {1:N0} บาท", p.Key, pd));
                            else
                                parts.Add(string.Format("{0}: {1}", p.Key, p.Value));
                        }
                        sb.AppendLine("    * " + string.Join(", ", parts));
                    }
                }
                else if (kvp.Value is Dictionary<string, int> dictIntVal)
                {
                    sb.AppendLine(string.Format("- {0}:", label));
                    foreach (var d in dictIntVal)
                    {
                        sb.AppendLine(string.Format("    * {0}: {1:N0}", d.Key, d.Value));
                    }
                }
                else if (kvp.Value != null)
                {
                    sb.AppendLine(string.Format("- {0}: {1}", label, kvp.Value));
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Map KPI keys to Thai labels for display
        /// </summary>
        private string GetThaiLabel(string key)
        {
            switch (key)
            {
                case "checkinsToday": return "เช็คอินวันนี้";
                case "checkoutsToday": return "เช็คเอาท์วันนี้";
                case "newBookings": return "การจองใหม่";
                case "cancellations": return "การยกเลิก";
                case "revenue": return "รายได้";
                case "bookedRooms": return "ห้องที่ถูกจอง";
                case "totalRooms": return "ห้องทั้งหมด";
                case "occupancyRate": return "อัตราการเข้าพัก (Occupancy)";
                case "activeGuests": return "จำนวนแขกที่พักอยู่";
                case "roomServiceOrders": return "ออเดอร์ Room Service";
                case "housekeepingPending": return "งานแม่บ้านรอดำเนินการ";
                case "housekeepingCompleted": return "งานแม่บ้านเสร็จสิ้น";
                case "chatMessages": return "ข้อความแชท";
                case "newReviews": return "รีวิวใหม่";
                case "totalRevenue": return "รายได้รวม";
                case "revenueByType": return "รายได้แยกตามประเภทที่พัก";
                case "totalReservations": return "จำนวนการจองทั้งหมด";
                case "totalCancellations": return "จำนวนยกเลิกทั้งหมด";
                case "totalRoomNights": return "จำนวน Room Nights";
                case "avgOccupancyRate": return "Occupancy เฉลี่ย";
                case "adr": return "ADR (ราคาเฉลี่ยต่อคืน)";
                case "revpar": return "RevPAR";
                case "newCustomers": return "ลูกค้าใหม่";
                case "returningCustomers": return "ลูกค้าเก่ากลับมา";
                case "productSales": return "ยอมขายสินค้า";
                case "productOrderCount": return "จำนวนออเดอร์สินค้า";
                case "reviewCount": return "จำนวนรีวิว";
                case "avgRating": return "คะแนนเฉลี่ย";
                case "prevMonthRevenue": return "รายได้เดือนก่อน";
                case "revenueGrowthPct": return "การเติบโตรายได้";
                case "prevMonthOccupancy": return "Occupancy เดือนก่อน";
                case "occupancyChange": return "Occupancy เปลี่ยนแปลง";
                case "topRevenueDays": return "วันที่มีรายได้สูงสุด (Top 5)";
                case "busiestDayOfWeek": return "วันที่ยุ่งที่สุดในสัปดาห์";
                case "busiestDayCount": return "จำนวนจองในวันที่ยุ่งที่สุด";
                case "avgSpendPerCustomer": return "ค่าใช้จ่ายเฉลี่ยต่อลูกค้า";
                case "topCustomers": return "ลูกค้ารายได้สูงสุด (Top 5)";
                case "popularRoomTypes": return "ประเภทห้องยอดนิยม";
                case "bookingChannels": return "ช่องทางการจอง";
                default: return key;
            }
        }

        #endregion
    }
}
