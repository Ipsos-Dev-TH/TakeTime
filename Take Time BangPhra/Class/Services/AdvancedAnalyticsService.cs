using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Take_Time_BangPhra.Class.Services
{
    /// <summary>
    /// Service for advanced analytics and business intelligence
    /// </summary>
    public class AdvancedAnalyticsService
    {
        private readonly string _connectionString;

        public AdvancedAnalyticsService(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region Revenue Analytics

        /// <summary>
        /// Get comprehensive revenue analytics
        /// </summary>
        public RevenueAnalytics GetRevenueAnalytics(DateTime fromDate, DateTime toDate)
        {
            var analytics = new RevenueAnalytics
            {
                FromDate = fromDate,
                ToDate = toDate
            };

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Total revenue and breakdown
                string sql = @"
                SELECT
                    SUM(TotalPrice) as TotalRevenue,
                    SUM(CASE WHEN PaymentStatus = 'Paid' THEN TotalPrice ELSE 0 END) as CollectedRevenue,
                    SUM(CASE WHEN PaymentStatus != 'Paid' THEN TotalPrice ELSE 0 END) as PendingRevenue,
                    COUNT(*) as TotalReservations,
                    COUNT(DISTINCT CustomerId) as UniqueGuests,
                    AVG(TotalPrice) as AverageReservationValue,
                    SUM(DATEDIFF(day, CheckInDate, CheckOutDate)) as TotalRoomNights
                FROM Reservations
                WHERE CheckInDate BETWEEN @FromDate AND @ToDate
                AND Status NOT IN ('Cancelled')";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            analytics.TotalRevenue = reader["TotalRevenue"] != DBNull.Value ? Convert.ToDecimal(reader["TotalRevenue"]) : 0;
                            analytics.CollectedRevenue = reader["CollectedRevenue"] != DBNull.Value ? Convert.ToDecimal(reader["CollectedRevenue"]) : 0;
                            analytics.PendingRevenue = reader["PendingRevenue"] != DBNull.Value ? Convert.ToDecimal(reader["PendingRevenue"]) : 0;
                            analytics.TotalReservations = Convert.ToInt32(reader["TotalReservations"]);
                            analytics.UniqueGuests = Convert.ToInt32(reader["UniqueGuests"]);
                            analytics.AverageReservationValue = reader["AverageReservationValue"] != DBNull.Value ? Convert.ToDecimal(reader["AverageReservationValue"]) : 0;
                            analytics.TotalRoomNights = reader["TotalRoomNights"] != DBNull.Value ? Convert.ToInt32(reader["TotalRoomNights"]) : 0;
                        }
                    }
                }

                // Revenue by accommodation type
                sql = @"
                SELECT a.AccommodationName, a.AccommodationType,
                    SUM(r.TotalPrice) as Revenue,
                    COUNT(*) as Bookings,
                    AVG(r.TotalPrice) as AvgBookingValue
                FROM Reservations r
                INNER JOIN Accommodations a ON r.AccommodationId = a.Id
                WHERE r.CheckInDate BETWEEN @FromDate AND @ToDate
                AND r.Status NOT IN ('Cancelled')
                GROUP BY a.AccommodationName, a.AccommodationType
                ORDER BY Revenue DESC";

                analytics.RevenueByAccommodation = new List<AccommodationRevenue>();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            analytics.RevenueByAccommodation.Add(new AccommodationRevenue
                            {
                                AccommodationName = reader["AccommodationName"].ToString(),
                                AccommodationType = reader["AccommodationType"].ToString(),
                                Revenue = Convert.ToDecimal(reader["Revenue"]),
                                Bookings = Convert.ToInt32(reader["Bookings"]),
                                AverageBookingValue = Convert.ToDecimal(reader["AvgBookingValue"])
                            });
                        }
                    }
                }

                // Revenue by channel (source)
                sql = @"
                SELECT ISNULL(BookingSource, 'Direct') as Channel,
                    SUM(TotalPrice) as Revenue,
                    COUNT(*) as Bookings
                FROM Reservations
                WHERE CheckInDate BETWEEN @FromDate AND @ToDate
                AND Status NOT IN ('Cancelled')
                GROUP BY ISNULL(BookingSource, 'Direct')
                ORDER BY Revenue DESC";

                analytics.RevenueByChannel = new Dictionary<string, decimal>();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            analytics.RevenueByChannel[reader["Channel"].ToString()] = Convert.ToDecimal(reader["Revenue"]);
                        }
                    }
                }

                // Daily revenue trend
                sql = @"
                SELECT CAST(CheckInDate AS DATE) as Date,
                    SUM(TotalPrice) as Revenue,
                    COUNT(*) as Bookings
                FROM Reservations
                WHERE CheckInDate BETWEEN @FromDate AND @ToDate
                AND Status NOT IN ('Cancelled')
                GROUP BY CAST(CheckInDate AS DATE)
                ORDER BY Date";

                analytics.DailyRevenueTrend = new List<DailyRevenue>();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            analytics.DailyRevenueTrend.Add(new DailyRevenue
                            {
                                Date = Convert.ToDateTime(reader["Date"]),
                                Revenue = Convert.ToDecimal(reader["Revenue"]),
                                Bookings = Convert.ToInt32(reader["Bookings"])
                            });
                        }
                    }
                }

                // Calculate KPIs
                int totalDays = (int)(toDate - fromDate).TotalDays + 1;
                int totalRooms = GetTotalRooms(conn);
                int availableRoomNights = totalDays * totalRooms;

                analytics.ADR = analytics.TotalRoomNights > 0 ? analytics.TotalRevenue / analytics.TotalRoomNights : 0;
                analytics.OccupancyRate = availableRoomNights > 0 ? (decimal)analytics.TotalRoomNights / availableRoomNights * 100 : 0;
                analytics.RevPAR = analytics.ADR * (analytics.OccupancyRate / 100);
            }

            return analytics;
        }

        /// <summary>
        /// Get revenue comparison with previous period
        /// </summary>
        public RevenuePeriodComparison GetRevenuePeriodComparison(DateTime fromDate, DateTime toDate)
        {
            int days = (int)(toDate - fromDate).TotalDays;
            DateTime prevFromDate = fromDate.AddDays(-days - 1);
            DateTime prevToDate = fromDate.AddDays(-1);

            var current = GetRevenueAnalytics(fromDate, toDate);
            var previous = GetRevenueAnalytics(prevFromDate, prevToDate);

            return new RevenuePeriodComparison
            {
                CurrentPeriod = current,
                PreviousPeriod = previous,
                RevenueGrowth = previous.TotalRevenue > 0
                    ? ((current.TotalRevenue - previous.TotalRevenue) / previous.TotalRevenue * 100) : 0,
                BookingsGrowth = previous.TotalReservations > 0
                    ? ((decimal)(current.TotalReservations - previous.TotalReservations) / previous.TotalReservations * 100) : 0,
                ADRGrowth = previous.ADR > 0
                    ? ((current.ADR - previous.ADR) / previous.ADR * 100) : 0,
                OccupancyGrowth = previous.OccupancyRate > 0
                    ? ((current.OccupancyRate - previous.OccupancyRate) / previous.OccupancyRate * 100) : 0
            };
        }

        private int GetTotalRooms(SqlConnection conn)
        {
            string sql = "SELECT COUNT(*) FROM Accommodations WHERE IsActive = 1";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        #endregion

        #region Occupancy Analytics

        /// <summary>
        /// Get detailed occupancy analytics
        /// </summary>
        public OccupancyAnalytics GetOccupancyAnalytics(DateTime fromDate, DateTime toDate)
        {
            var analytics = new OccupancyAnalytics();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                int totalRooms = GetTotalRooms(conn);

                // Daily occupancy
                string sql = @"
                WITH DateRange AS (
                    SELECT @FromDate as Date
                    UNION ALL
                    SELECT DATEADD(day, 1, Date) FROM DateRange WHERE Date < @ToDate
                ),
                DailyOccupancy AS (
                    SELECT d.Date,
                        COUNT(DISTINCT r.AccommodationId) as OccupiedRooms
                    FROM DateRange d
                    LEFT JOIN Reservations r ON d.Date >= r.CheckInDate AND d.Date < r.CheckOutDate
                        AND r.Status NOT IN ('Cancelled')
                    GROUP BY d.Date
                )
                SELECT Date, OccupiedRooms,
                    @TotalRooms as TotalRooms,
                    CAST(OccupiedRooms AS DECIMAL) / @TotalRooms * 100 as OccupancyRate
                FROM DailyOccupancy
                OPTION (MAXRECURSION 366)";

                analytics.DailyOccupancy = new List<DailyOccupancy>();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);
                    cmd.Parameters.AddWithValue("@TotalRooms", totalRooms);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            analytics.DailyOccupancy.Add(new DailyOccupancy
                            {
                                Date = Convert.ToDateTime(reader["Date"]),
                                OccupiedRooms = Convert.ToInt32(reader["OccupiedRooms"]),
                                TotalRooms = Convert.ToInt32(reader["TotalRooms"]),
                                OccupancyRate = Convert.ToDecimal(reader["OccupancyRate"])
                            });
                        }
                    }
                }

                // Occupancy by day of week
                sql = @"
                SELECT DATEPART(dw, d.Date) as DayOfWeek,
                    AVG(CAST(oc.OccupiedRooms AS DECIMAL) / @TotalRooms * 100) as AvgOccupancyRate
                FROM (
                    SELECT @FromDate as Date
                    UNION ALL
                    SELECT DATEADD(day, 1, Date) FROM (SELECT @FromDate as Date) d WHERE Date < @ToDate
                ) d
                CROSS APPLY (
                    SELECT COUNT(DISTINCT r.AccommodationId) as OccupiedRooms
                    FROM Reservations r
                    WHERE d.Date >= r.CheckInDate AND d.Date < r.CheckOutDate
                    AND r.Status NOT IN ('Cancelled')
                ) oc
                GROUP BY DATEPART(dw, d.Date)
                ORDER BY DATEPART(dw, d.Date)";

                analytics.OccupancyByDayOfWeek = new Dictionary<string, decimal>();
                string[] dayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);
                    cmd.Parameters.AddWithValue("@TotalRooms", totalRooms);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int dow = Convert.ToInt32(reader["DayOfWeek"]) - 1;
                            analytics.OccupancyByDayOfWeek[dayNames[dow]] = Convert.ToDecimal(reader["AvgOccupancyRate"]);
                        }
                    }
                }

                // Occupancy by room type
                sql = @"
                SELECT a.AccommodationType,
                    COUNT(DISTINCT r.Id) as TotalBookings,
                    SUM(DATEDIFF(day, r.CheckInDate, r.CheckOutDate)) as RoomNights,
                    COUNT(DISTINCT a.Id) as RoomCount
                FROM Accommodations a
                LEFT JOIN Reservations r ON a.Id = r.AccommodationId
                    AND r.CheckInDate BETWEEN @FromDate AND @ToDate
                    AND r.Status NOT IN ('Cancelled')
                WHERE a.IsActive = 1
                GROUP BY a.AccommodationType";

                analytics.OccupancyByRoomType = new List<RoomTypeOccupancy>();
                int totalDays = (int)(toDate - fromDate).TotalDays + 1;

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int roomCount = Convert.ToInt32(reader["RoomCount"]);
                            int roomNights = reader["RoomNights"] != DBNull.Value ? Convert.ToInt32(reader["RoomNights"]) : 0;
                            int availableNights = roomCount * totalDays;

                            analytics.OccupancyByRoomType.Add(new RoomTypeOccupancy
                            {
                                RoomType = reader["AccommodationType"].ToString(),
                                RoomCount = roomCount,
                                TotalBookings = Convert.ToInt32(reader["TotalBookings"]),
                                RoomNights = roomNights,
                                OccupancyRate = availableNights > 0 ? (decimal)roomNights / availableNights * 100 : 0
                            });
                        }
                    }
                }

                // Calculate summary stats
                if (analytics.DailyOccupancy.Any())
                {
                    analytics.AverageOccupancy = analytics.DailyOccupancy.Average(d => d.OccupancyRate);
                    analytics.PeakOccupancy = analytics.DailyOccupancy.Max(d => d.OccupancyRate);
                    analytics.LowestOccupancy = analytics.DailyOccupancy.Min(d => d.OccupancyRate);
                    analytics.PeakDate = analytics.DailyOccupancy.First(d => d.OccupancyRate == analytics.PeakOccupancy).Date;
                }
            }

            return analytics;
        }

        #endregion

        #region Guest Analytics

        /// <summary>
        /// Get guest analytics and segmentation
        /// </summary>
        public GuestAnalytics GetGuestAnalytics(DateTime fromDate, DateTime toDate)
        {
            var analytics = new GuestAnalytics();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Guest overview
                string sql = @"
                SELECT
                    COUNT(DISTINCT CustomerId) as TotalGuests,
                    COUNT(*) as TotalStays,
                    AVG(DATEDIFF(day, CheckInDate, CheckOutDate)) as AvgStayLength,
                    SUM(TotalPrice) as TotalSpent
                FROM Reservations
                WHERE CheckInDate BETWEEN @FromDate AND @ToDate
                AND Status NOT IN ('Cancelled')";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            analytics.TotalGuests = Convert.ToInt32(reader["TotalGuests"]);
                            analytics.TotalStays = Convert.ToInt32(reader["TotalStays"]);
                            analytics.AverageStayLength = reader["AvgStayLength"] != DBNull.Value ? Convert.ToDouble(reader["AvgStayLength"]) : 0;
                            analytics.TotalRevenue = reader["TotalSpent"] != DBNull.Value ? Convert.ToDecimal(reader["TotalSpent"]) : 0;
                        }
                    }
                }

                // New vs returning guests
                sql = @"
                SELECT
                    SUM(CASE WHEN PreviousStays = 0 THEN 1 ELSE 0 END) as NewGuests,
                    SUM(CASE WHEN PreviousStays > 0 THEN 1 ELSE 0 END) as ReturningGuests
                FROM (
                    SELECT r.CustomerId,
                        (SELECT COUNT(*) FROM Reservations r2
                         WHERE r2.CustomerId = r.CustomerId AND r2.CheckInDate < r.CheckInDate
                         AND r2.Status NOT IN ('Cancelled')) as PreviousStays
                    FROM Reservations r
                    WHERE r.CheckInDate BETWEEN @FromDate AND @ToDate
                    AND r.Status NOT IN ('Cancelled')
                ) t";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            analytics.NewGuests = Convert.ToInt32(reader["NewGuests"]);
                            analytics.ReturningGuests = Convert.ToInt32(reader["ReturningGuests"]);
                        }
                    }
                }

                // Guest segmentation by spending
                sql = @"
                SELECT
                    c.Id as CustomerId,
                    c.FirstName + ' ' + c.LastName as GuestName,
                    COUNT(r.Id) as TotalBookings,
                    SUM(r.TotalPrice) as TotalSpent,
                    AVG(r.TotalPrice) as AvgBookingValue,
                    DATEDIFF(day, MIN(r.CheckInDate), MAX(r.CheckInDate)) as CustomerLifespan,
                    MAX(r.CheckInDate) as LastVisit
                FROM Customer c
                INNER JOIN Reservations r ON c.Id = r.CustomerId
                WHERE r.Status NOT IN ('Cancelled')
                GROUP BY c.Id, c.FirstName, c.LastName
                ORDER BY TotalSpent DESC";

                analytics.TopGuests = new List<GuestProfile>();
                analytics.GuestSegmentation = new Dictionary<string, int>
                {
                    { "VIP", 0 },
                    { "Frequent", 0 },
                    { "Regular", 0 },
                    { "New", 0 }
                };

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        int count = 0;
                        while (reader.Read())
                        {
                            var profile = new GuestProfile
                            {
                                CustomerId = Convert.ToInt32(reader["CustomerId"]),
                                GuestName = reader["GuestName"].ToString(),
                                TotalBookings = Convert.ToInt32(reader["TotalBookings"]),
                                TotalSpent = Convert.ToDecimal(reader["TotalSpent"]),
                                AverageBookingValue = Convert.ToDecimal(reader["AvgBookingValue"]),
                                LastVisit = Convert.ToDateTime(reader["LastVisit"])
                            };

                            // Segmentation logic
                            if (profile.TotalSpent >= 100000 || profile.TotalBookings >= 10)
                            {
                                profile.Segment = "VIP";
                                analytics.GuestSegmentation["VIP"]++;
                            }
                            else if (profile.TotalBookings >= 5)
                            {
                                profile.Segment = "Frequent";
                                analytics.GuestSegmentation["Frequent"]++;
                            }
                            else if (profile.TotalBookings >= 2)
                            {
                                profile.Segment = "Regular";
                                analytics.GuestSegmentation["Regular"]++;
                            }
                            else
                            {
                                profile.Segment = "New";
                                analytics.GuestSegmentation["New"]++;
                            }

                            if (count < 10) // Top 10 guests
                            {
                                analytics.TopGuests.Add(profile);
                            }
                            count++;
                        }
                    }
                }

                // Booking lead time analysis
                sql = @"
                SELECT
                    AVG(DATEDIFF(day, CreatedAt, CheckInDate)) as AvgLeadTime,
                    MIN(DATEDIFF(day, CreatedAt, CheckInDate)) as MinLeadTime,
                    MAX(DATEDIFF(day, CreatedAt, CheckInDate)) as MaxLeadTime
                FROM Reservations
                WHERE CheckInDate BETWEEN @FromDate AND @ToDate
                AND Status NOT IN ('Cancelled')
                AND CreatedAt < CheckInDate";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            analytics.AverageLeadTime = reader["AvgLeadTime"] != DBNull.Value ? Convert.ToInt32(reader["AvgLeadTime"]) : 0;
                        }
                    }
                }
            }

            analytics.AverageRevenuePerGuest = analytics.TotalGuests > 0 ? analytics.TotalRevenue / analytics.TotalGuests : 0;
            analytics.ReturnRate = analytics.TotalGuests > 0 ? (decimal)analytics.ReturningGuests / analytics.TotalGuests * 100 : 0;

            return analytics;
        }

        #endregion

        #region Forecasting

        /// <summary>
        /// Generate revenue forecast
        /// </summary>
        public List<ForecastData> GenerateRevenueForecast(int daysAhead = 30)
        {
            var forecast = new List<ForecastData>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Get historical data for same period last year
                DateTime futureStart = DateTime.Today;
                DateTime futureEnd = DateTime.Today.AddDays(daysAhead);
                DateTime histStart = futureStart.AddYears(-1);
                DateTime histEnd = futureEnd.AddYears(-1);

                // Get confirmed future bookings
                string sql = @"
                SELECT CAST(CheckInDate AS DATE) as Date,
                    SUM(TotalPrice) as ConfirmedRevenue,
                    COUNT(*) as ConfirmedBookings
                FROM Reservations
                WHERE CheckInDate BETWEEN @FutureStart AND @FutureEnd
                AND Status IN ('Confirmed', 'Pending')
                GROUP BY CAST(CheckInDate AS DATE)";

                var confirmedData = new Dictionary<DateTime, (decimal revenue, int bookings)>();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FutureStart", futureStart);
                    cmd.Parameters.AddWithValue("@FutureEnd", futureEnd);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            confirmedData[Convert.ToDateTime(reader["Date"])] = (
                                Convert.ToDecimal(reader["ConfirmedRevenue"]),
                                Convert.ToInt32(reader["ConfirmedBookings"])
                            );
                        }
                    }
                }

                // Get historical data for comparison
                sql = @"
                SELECT CAST(CheckInDate AS DATE) as Date,
                    SUM(TotalPrice) as Revenue,
                    COUNT(*) as Bookings
                FROM Reservations
                WHERE CheckInDate BETWEEN @HistStart AND @HistEnd
                AND Status NOT IN ('Cancelled')
                GROUP BY CAST(CheckInDate AS DATE)";

                var historicalData = new Dictionary<DateTime, (decimal revenue, int bookings)>();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@HistStart", histStart);
                    cmd.Parameters.AddWithValue("@HistEnd", histEnd);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            historicalData[Convert.ToDateTime(reader["Date"])] = (
                                Convert.ToDecimal(reader["Revenue"]),
                                Convert.ToInt32(reader["Bookings"])
                            );
                        }
                    }
                }

                // Calculate growth rate from recent period
                decimal growthRate = CalculateGrowthRate(conn);

                // Generate forecast
                for (int i = 0; i <= daysAhead; i++)
                {
                    DateTime date = futureStart.AddDays(i);
                    DateTime histDate = date.AddYears(-1);

                    var forecastItem = new ForecastData
                    {
                        Date = date,
                        DayOfWeek = date.DayOfWeek.ToString()
                    };

                    // Use confirmed bookings if available
                    if (confirmedData.ContainsKey(date))
                    {
                        forecastItem.ConfirmedRevenue = confirmedData[date].revenue;
                        forecastItem.ConfirmedBookings = confirmedData[date].bookings;
                    }

                    // Forecast based on historical + growth
                    if (historicalData.ContainsKey(histDate))
                    {
                        forecastItem.ForecastedRevenue = historicalData[histDate].revenue * (1 + growthRate / 100);
                        forecastItem.ForecastedBookings = (int)(historicalData[histDate].bookings * (1 + growthRate / 100));
                    }
                    else
                    {
                        // Use average if no historical data
                        forecastItem.ForecastedRevenue = GetAverageRevenue(conn, date.DayOfWeek) * (1 + growthRate / 100);
                        forecastItem.ForecastedBookings = GetAverageBookings(conn, date.DayOfWeek);
                    }

                    forecastItem.Confidence = CalculateForecastConfidence(forecastItem, date);
                    forecast.Add(forecastItem);
                }
            }

            return forecast;
        }

        private decimal CalculateGrowthRate(SqlConnection conn)
        {
            string sql = @"
            SELECT
                (SELECT SUM(TotalPrice) FROM Reservations
                 WHERE CheckInDate BETWEEN DATEADD(month, -1, GETDATE()) AND GETDATE()
                 AND Status NOT IN ('Cancelled')) as CurrentRevenue,
                (SELECT SUM(TotalPrice) FROM Reservations
                 WHERE CheckInDate BETWEEN DATEADD(month, -13, GETDATE()) AND DATEADD(month, -12, GETDATE())
                 AND Status NOT IN ('Cancelled')) as LastYearRevenue";

            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        decimal current = reader["CurrentRevenue"] != DBNull.Value ? Convert.ToDecimal(reader["CurrentRevenue"]) : 0;
                        decimal lastYear = reader["LastYearRevenue"] != DBNull.Value ? Convert.ToDecimal(reader["LastYearRevenue"]) : 0;

                        if (lastYear > 0)
                            return (current - lastYear) / lastYear * 100;
                    }
                }
            }
            return 0;
        }

        private decimal GetAverageRevenue(SqlConnection conn, DayOfWeek dayOfWeek)
        {
            string sql = @"
            SELECT AVG(TotalPrice) as AvgRevenue
            FROM Reservations
            WHERE DATEPART(dw, CheckInDate) = @DayOfWeek
            AND Status NOT IN ('Cancelled')
            AND CheckInDate >= DATEADD(month, -3, GETDATE())";

            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@DayOfWeek", (int)dayOfWeek + 1);
                var result = cmd.ExecuteScalar();
                return result != DBNull.Value ? Convert.ToDecimal(result) : 0;
            }
        }

        private int GetAverageBookings(SqlConnection conn, DayOfWeek dayOfWeek)
        {
            string sql = @"
            SELECT AVG(BookingCount) as AvgBookings
            FROM (
                SELECT CAST(CheckInDate AS DATE) as Date, COUNT(*) as BookingCount
                FROM Reservations
                WHERE DATEPART(dw, CheckInDate) = @DayOfWeek
                AND Status NOT IN ('Cancelled')
                AND CheckInDate >= DATEADD(month, -3, GETDATE())
                GROUP BY CAST(CheckInDate AS DATE)
            ) t";

            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@DayOfWeek", (int)dayOfWeek + 1);
                var result = cmd.ExecuteScalar();
                return result != DBNull.Value ? Convert.ToInt32(result) : 0;
            }
        }

        private decimal CalculateForecastConfidence(ForecastData forecast, DateTime date)
        {
            int daysAhead = (int)(date - DateTime.Today).TotalDays;

            // Confidence decreases with time
            decimal baseConfidence = 95;
            decimal dailyDecay = 1.5m;

            // Higher confidence if confirmed bookings exist
            if (forecast.ConfirmedBookings > 0)
                baseConfidence = 98;

            return Math.Max(50, baseConfidence - (daysAhead * dailyDecay));
        }

        #endregion

        #region Performance Metrics

        /// <summary>
        /// Get comprehensive hotel performance metrics
        /// </summary>
        public HotelPerformanceMetrics GetPerformanceMetrics(DateTime fromDate, DateTime toDate)
        {
            var metrics = new HotelPerformanceMetrics();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Revenue metrics
                var revenueAnalytics = GetRevenueAnalytics(fromDate, toDate);
                metrics.TotalRevenue = revenueAnalytics.TotalRevenue;
                metrics.ADR = revenueAnalytics.ADR;
                metrics.RevPAR = revenueAnalytics.RevPAR;
                metrics.OccupancyRate = revenueAnalytics.OccupancyRate;

                // Cancellation metrics
                string sql = @"
                SELECT
                    COUNT(*) as TotalReservations,
                    SUM(CASE WHEN Status = 'Cancelled' THEN 1 ELSE 0 END) as Cancellations,
                    SUM(CASE WHEN Status = 'Cancelled' THEN TotalPrice ELSE 0 END) as LostRevenue
                FROM Reservations
                WHERE CreatedAt BETWEEN @FromDate AND @ToDate";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int total = Convert.ToInt32(reader["TotalReservations"]);
                            metrics.Cancellations = Convert.ToInt32(reader["Cancellations"]);
                            metrics.CancellationRate = total > 0 ? (decimal)metrics.Cancellations / total * 100 : 0;
                            metrics.LostRevenue = reader["LostRevenue"] != DBNull.Value ? Convert.ToDecimal(reader["LostRevenue"]) : 0;
                        }
                    }
                }

                // Average length of stay
                sql = @"
                SELECT AVG(DATEDIFF(day, CheckInDate, CheckOutDate)) as ALOS
                FROM Reservations
                WHERE CheckInDate BETWEEN @FromDate AND @ToDate
                AND Status NOT IN ('Cancelled')";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    var result = cmd.ExecuteScalar();
                    metrics.ALOS = result != DBNull.Value ? Convert.ToDouble(result) : 0;
                }

                // Channel performance
                sql = @"
                SELECT ISNULL(BookingSource, 'Direct') as Channel,
                    COUNT(*) as Bookings,
                    SUM(TotalPrice) as Revenue,
                    AVG(TotalPrice) as AvgValue
                FROM Reservations
                WHERE CheckInDate BETWEEN @FromDate AND @ToDate
                AND Status NOT IN ('Cancelled')
                GROUP BY ISNULL(BookingSource, 'Direct')
                ORDER BY Revenue DESC";

                metrics.ChannelPerformance = new List<ChannelPerformance>();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            metrics.ChannelPerformance.Add(new ChannelPerformance
                            {
                                ChannelName = reader["Channel"].ToString(),
                                Bookings = Convert.ToInt32(reader["Bookings"]),
                                Revenue = Convert.ToDecimal(reader["Revenue"]),
                                AverageBookingValue = Convert.ToDecimal(reader["AvgValue"])
                            });
                        }
                    }
                }

                // TRevPAR (Total Revenue Per Available Room)
                int totalRooms = GetTotalRooms(conn);
                int totalDays = (int)(toDate - fromDate).TotalDays + 1;
                int totalRoomNights = totalRooms * totalDays;

                // Get total revenue including F&B, amenities, etc.
                sql = @"
                SELECT ISNULL(SUM(TotalPrice), 0) as RoomRevenue FROM Reservations
                WHERE CheckInDate BETWEEN @FromDate AND @ToDate AND Status NOT IN ('Cancelled')";

                decimal roomRevenue = 0;
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);
                    roomRevenue = Convert.ToDecimal(cmd.ExecuteScalar());
                }

                metrics.TRevPAR = totalRoomNights > 0 ? roomRevenue / totalRoomNights : 0;

                // GOP PAR (Gross Operating Profit Per Available Room)
                // This would require expense data
                metrics.GOPPAR = metrics.TRevPAR * 0.35m; // Estimated 35% GOP margin
            }

            return metrics;
        }

        #endregion
    }

    #region Data Models

    public class RevenueAnalytics
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal CollectedRevenue { get; set; }
        public decimal PendingRevenue { get; set; }
        public int TotalReservations { get; set; }
        public int UniqueGuests { get; set; }
        public decimal AverageReservationValue { get; set; }
        public int TotalRoomNights { get; set; }
        public decimal ADR { get; set; } // Average Daily Rate
        public decimal OccupancyRate { get; set; }
        public decimal RevPAR { get; set; } // Revenue Per Available Room
        public List<AccommodationRevenue> RevenueByAccommodation { get; set; }
        public Dictionary<string, decimal> RevenueByChannel { get; set; }
        public List<DailyRevenue> DailyRevenueTrend { get; set; }
    }

    public class AccommodationRevenue
    {
        public string AccommodationName { get; set; }
        public string AccommodationType { get; set; }
        public decimal Revenue { get; set; }
        public int Bookings { get; set; }
        public decimal AverageBookingValue { get; set; }
    }

    public class DailyRevenue
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int Bookings { get; set; }
    }

    public class RevenuePeriodComparison
    {
        public RevenueAnalytics CurrentPeriod { get; set; }
        public RevenueAnalytics PreviousPeriod { get; set; }
        public decimal RevenueGrowth { get; set; }
        public decimal BookingsGrowth { get; set; }
        public decimal ADRGrowth { get; set; }
        public decimal OccupancyGrowth { get; set; }
    }

    public class OccupancyAnalytics
    {
        public List<DailyOccupancy> DailyOccupancy { get; set; }
        public Dictionary<string, decimal> OccupancyByDayOfWeek { get; set; }
        public List<RoomTypeOccupancy> OccupancyByRoomType { get; set; }
        public decimal AverageOccupancy { get; set; }
        public decimal PeakOccupancy { get; set; }
        public decimal LowestOccupancy { get; set; }
        public DateTime PeakDate { get; set; }
    }

    public class DailyOccupancy
    {
        public DateTime Date { get; set; }
        public int OccupiedRooms { get; set; }
        public int TotalRooms { get; set; }
        public decimal OccupancyRate { get; set; }
    }

    public class RoomTypeOccupancy
    {
        public string RoomType { get; set; }
        public int RoomCount { get; set; }
        public int TotalBookings { get; set; }
        public int RoomNights { get; set; }
        public decimal OccupancyRate { get; set; }
    }

    public class GuestAnalytics
    {
        public int TotalGuests { get; set; }
        public int TotalStays { get; set; }
        public int NewGuests { get; set; }
        public int ReturningGuests { get; set; }
        public double AverageStayLength { get; set; }
        public int AverageLeadTime { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageRevenuePerGuest { get; set; }
        public decimal ReturnRate { get; set; }
        public List<GuestProfile> TopGuests { get; set; }
        public Dictionary<string, int> GuestSegmentation { get; set; }
    }

    public class GuestProfile
    {
        public int CustomerId { get; set; }
        public string GuestName { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal AverageBookingValue { get; set; }
        public DateTime LastVisit { get; set; }
        public string Segment { get; set; }
    }

    public class ForecastData
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; }
        public decimal ConfirmedRevenue { get; set; }
        public int ConfirmedBookings { get; set; }
        public decimal ForecastedRevenue { get; set; }
        public int ForecastedBookings { get; set; }
        public decimal Confidence { get; set; }
    }

    public class HotelPerformanceMetrics
    {
        public decimal TotalRevenue { get; set; }
        public decimal ADR { get; set; }
        public decimal RevPAR { get; set; }
        public decimal TRevPAR { get; set; }
        public decimal GOPPAR { get; set; }
        public decimal OccupancyRate { get; set; }
        public double ALOS { get; set; }
        public int Cancellations { get; set; }
        public decimal CancellationRate { get; set; }
        public decimal LostRevenue { get; set; }
        public List<ChannelPerformance> ChannelPerformance { get; set; }
    }

    public class ChannelPerformance
    {
        public string ChannelName { get; set; }
        public int Bookings { get; set; }
        public decimal Revenue { get; set; }
        public decimal AverageBookingValue { get; set; }
        public decimal MarketShare => 0; // Calculated externally
    }

    #endregion
}
