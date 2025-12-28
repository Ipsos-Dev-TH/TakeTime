using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// Dynamic Pricing & Revenue Management Service
    /// Automatically adjusts room rates based on demand, occupancy, and market conditions
    /// </summary>
    public class DynamicPricingService
    {
        private readonly string _connectionString;
        private readonly code _code;

        public DynamicPricingService(string connectionString)
        {
            _connectionString = connectionString;
            _code = new code();
        }

        #region Price Calculation

        /// <summary>
        /// Calculate dynamic price for a specific date and accommodation
        /// </summary>
        public DynamicPriceResult CalculateDynamicPrice(int accommodationId, DateTime date, int stayDays = 1)
        {
            try
            {
                // Get base price
                decimal basePrice = GetBasePrice(accommodationId, date);

                // Get all applicable multipliers
                decimal occupancyMultiplier = GetOccupancyMultiplier(date);
                decimal seasonMultiplier = GetSeasonMultiplier(date);
                decimal demandMultiplier = GetDemandMultiplier(accommodationId, date);
                decimal dayOfWeekMultiplier = GetDayOfWeekMultiplier(date);
                decimal leadTimeMultiplier = GetLeadTimeMultiplier(date);
                decimal lengthOfStayDiscount = GetLengthOfStayDiscount(stayDays);

                // Calculate final multiplier
                decimal totalMultiplier = occupancyMultiplier * seasonMultiplier * demandMultiplier * dayOfWeekMultiplier * leadTimeMultiplier;

                // Apply length of stay discount
                totalMultiplier = totalMultiplier * (1 - lengthOfStayDiscount);

                // Apply min/max price limits
                decimal calculatedPrice = basePrice * totalMultiplier;
                var limits = GetPriceLimits(accommodationId);
                decimal finalPrice = Math.Max(limits.MinPrice, Math.Min(limits.MaxPrice, calculatedPrice));

                return new DynamicPriceResult
                {
                    AccommodationId = accommodationId,
                    Date = date,
                    BasePrice = basePrice,
                    FinalPrice = Math.Round(finalPrice, 0),
                    TotalMultiplier = totalMultiplier,
                    Multipliers = new PriceMultipliers
                    {
                        Occupancy = occupancyMultiplier,
                        Season = seasonMultiplier,
                        Demand = demandMultiplier,
                        DayOfWeek = dayOfWeekMultiplier,
                        LeadTime = leadTimeMultiplier,
                        LengthOfStayDiscount = lengthOfStayDiscount
                    },
                    PricingStrategy = DeterminePricingStrategy(totalMultiplier)
                };
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "DynamicPricing Error", ex.Message, "SYSTEM");
                return new DynamicPriceResult
                {
                    AccommodationId = accommodationId,
                    Date = date,
                    BasePrice = GetBasePrice(accommodationId, date),
                    FinalPrice = GetBasePrice(accommodationId, date),
                    TotalMultiplier = 1.0m
                };
            }
        }

        /// <summary>
        /// Get base price for accommodation on a specific date
        /// </summary>
        private decimal GetBasePrice(int accommodationId, DateTime date)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@accommodationId", accommodationId },
                { "@date", date }
            };

            // Check for date-specific price first
            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ISNULL(ap.Price, a.Price) as Price
                  FROM Accommodation a
                  LEFT JOIN Accommodation_Price ap ON a.ID = ap.Accommodation_ID AND ap.Date = @date
                  WHERE a.ID = @accommodationId",
                parameters);

            if (dt.Rows.Count > 0)
            {
                return Convert.ToDecimal(dt.Rows[0]["Price"]);
            }

            return 0;
        }

        /// <summary>
        /// Get price limits for accommodation
        /// </summary>
        private (decimal MinPrice, decimal MaxPrice) GetPriceLimits(int accommodationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@accommodationId", accommodationId }
            };

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    ISNULL(Min_Dynamic_Price, Price * 0.7) as MinPrice,
                    ISNULL(Max_Dynamic_Price, Price * 2.0) as MaxPrice
                  FROM Accommodation
                  WHERE ID = @accommodationId",
                parameters);

            if (dt.Rows.Count > 0)
            {
                return (
                    Convert.ToDecimal(dt.Rows[0]["MinPrice"]),
                    Convert.ToDecimal(dt.Rows[0]["MaxPrice"])
                );
            }

            return (0, decimal.MaxValue);
        }

        #endregion

        #region Multiplier Calculations

        /// <summary>
        /// Calculate occupancy-based multiplier
        /// Higher occupancy = higher prices
        /// </summary>
        private decimal GetOccupancyMultiplier(DateTime date)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@date", date }
            };

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    COUNT(DISTINCT r.ID) as BookedRooms,
                    (SELECT SUM(TotalRooms) FROM Accommodation WHERE Status = 'True') as TotalRooms
                  FROM Reservation r
                  INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                  WHERE r.CheckinDate <= @date AND r.CheckoutDate > @date
                    AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')",
                parameters);

            if (dt.Rows.Count > 0 && dt.Rows[0]["TotalRooms"] != DBNull.Value)
            {
                int bookedRooms = Convert.ToInt32(dt.Rows[0]["BookedRooms"]);
                int totalRooms = Convert.ToInt32(dt.Rows[0]["TotalRooms"]);

                if (totalRooms > 0)
                {
                    decimal occupancyRate = (decimal)bookedRooms / totalRooms;

                    // Occupancy pricing tiers
                    if (occupancyRate >= 0.9m) return 1.5m;  // 90%+ = 50% premium
                    if (occupancyRate >= 0.8m) return 1.3m;  // 80-90% = 30% premium
                    if (occupancyRate >= 0.7m) return 1.15m; // 70-80% = 15% premium
                    if (occupancyRate >= 0.5m) return 1.0m;  // 50-70% = base price
                    if (occupancyRate >= 0.3m) return 0.9m;  // 30-50% = 10% discount
                    return 0.8m;  // <30% = 20% discount
                }
            }

            return 1.0m;
        }

        /// <summary>
        /// Calculate season-based multiplier
        /// </summary>
        private decimal GetSeasonMultiplier(DateTime date)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@date", date }
            };

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT Rate_Multiplier
                  FROM Pricing_Seasons
                  WHERE Start_Date <= @date AND End_Date >= @date
                    AND Is_Active = 1
                  ORDER BY Priority DESC",
                parameters);

            if (dt.Rows.Count > 0)
            {
                return Convert.ToDecimal(dt.Rows[0]["Rate_Multiplier"]);
            }

            // Check Thai holidays
            if (IsThaiHoliday(date))
            {
                return 1.3m; // 30% premium on holidays
            }

            return 1.0m;
        }

        /// <summary>
        /// Calculate demand-based multiplier based on historical data
        /// </summary>
        private decimal GetDemandMultiplier(int accommodationId, DateTime date)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@accommodationId", accommodationId },
                { "@dayOfYear", date.DayOfYear },
                { "@lastYear", date.AddYears(-1) }
            };

            // Analyze historical demand for same period
            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT COUNT(*) as BookingCount
                  FROM Reservation r
                  INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                  WHERE ra.Accommodation_ID = @accommodationId
                    AND DATEPART(DAYOFYEAR, r.CheckinDate) BETWEEN @dayOfYear - 7 AND @dayOfYear + 7
                    AND r.Created_Date >= @lastYear
                    AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')",
                parameters);

            if (dt.Rows.Count > 0)
            {
                int historicalBookings = Convert.ToInt32(dt.Rows[0]["BookingCount"]);

                // Compare to average
                if (historicalBookings > 20) return 1.2m;  // High demand
                if (historicalBookings > 10) return 1.1m;  // Above average
                if (historicalBookings > 5) return 1.0m;   // Average
                return 0.95m;  // Low demand
            }

            return 1.0m;
        }

        /// <summary>
        /// Day of week multiplier
        /// </summary>
        private decimal GetDayOfWeekMultiplier(DateTime date)
        {
            switch (date.DayOfWeek)
            {
                case DayOfWeek.Friday:
                case DayOfWeek.Saturday:
                    return 1.2m;  // Weekend premium
                case DayOfWeek.Sunday:
                    return 1.1m;  // Slight Sunday premium
                case DayOfWeek.Monday:
                case DayOfWeek.Tuesday:
                case DayOfWeek.Wednesday:
                    return 0.9m;  // Midweek discount
                default:
                    return 1.0m;
            }
        }

        /// <summary>
        /// Lead time (booking advance) multiplier
        /// </summary>
        private decimal GetLeadTimeMultiplier(DateTime checkInDate)
        {
            int daysAhead = (checkInDate - DateTime.Today).Days;

            if (daysAhead <= 1) return 1.3m;      // Last minute premium
            if (daysAhead <= 3) return 1.15m;     // Short notice premium
            if (daysAhead <= 7) return 1.05m;     // Week ahead
            if (daysAhead <= 14) return 1.0m;     // 1-2 weeks ahead
            if (daysAhead <= 30) return 0.95m;    // 2-4 weeks ahead
            if (daysAhead <= 60) return 0.9m;     // 1-2 months ahead (early bird)
            return 0.85m;  // 2+ months ahead (super early bird)
        }

        /// <summary>
        /// Length of stay discount
        /// </summary>
        private decimal GetLengthOfStayDiscount(int stayDays)
        {
            if (stayDays >= 14) return 0.15m;  // 15% off for 2+ weeks
            if (stayDays >= 7) return 0.10m;   // 10% off for 1+ week
            if (stayDays >= 4) return 0.05m;   // 5% off for 4+ nights
            if (stayDays >= 3) return 0.03m;   // 3% off for 3 nights
            return 0;
        }

        /// <summary>
        /// Check if date is Thai holiday
        /// </summary>
        private bool IsThaiHoliday(DateTime date)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@date", date.Date }
            };

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT 1 FROM Thai_Holidays WHERE Holiday_Date = @date",
                parameters);

            if (dt.Rows.Count > 0) return true;

            // Fixed holidays
            var fixedHolidays = new List<(int Month, int Day)>
            {
                (1, 1),   // New Year
                (4, 6),   // Chakri Day
                (4, 13), (4, 14), (4, 15),  // Songkran
                (5, 1),   // Labor Day
                (5, 4),   // Coronation Day
                (6, 3),   // Queen's Birthday
                (7, 28),  // King's Birthday
                (8, 12),  // Mother's Day
                (10, 13), // King Bhumibol Memorial Day
                (10, 23), // Chulalongkorn Day
                (12, 5),  // Father's Day
                (12, 10), // Constitution Day
                (12, 31)  // New Year's Eve
            };

            return fixedHolidays.Contains((date.Month, date.Day));
        }

        /// <summary>
        /// Determine pricing strategy based on multiplier
        /// </summary>
        private string DeterminePricingStrategy(decimal multiplier)
        {
            if (multiplier >= 1.3m) return "PREMIUM";
            if (multiplier >= 1.1m) return "HIGH_DEMAND";
            if (multiplier >= 0.95m) return "STANDARD";
            if (multiplier >= 0.85m) return "DISCOUNT";
            return "DEEP_DISCOUNT";
        }

        #endregion

        #region Revenue Analytics

        /// <summary>
        /// Get revenue analytics for a date range
        /// </summary>
        public PricingRevenueAnalytics GetRevenueAnalytics(DateTime startDate, DateTime endDate)
        {
            var analytics = new PricingRevenueAnalytics
            {
                StartDate = startDate,
                EndDate = endDate
            };

            var parameters = new Dictionary<string, object>
            {
                { "@startDate", startDate },
                { "@endDate", endDate }
            };

            // Total revenue
            DataTable dtRevenue = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    ISNULL(SUM(TotalPrice), 0) as TotalRevenue,
                    COUNT(*) as TotalBookings,
                    AVG(TotalPrice) as AvgBookingValue,
                    AVG(DATEDIFF(DAY, CheckinDate, CheckoutDate)) as AvgStayLength
                  FROM Reservation
                  WHERE CheckinDate >= @startDate AND CheckinDate <= @endDate
                    AND Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')",
                parameters);

            if (dtRevenue.Rows.Count > 0)
            {
                analytics.TotalRevenue = Convert.ToDecimal(dtRevenue.Rows[0]["TotalRevenue"]);
                analytics.TotalBookings = Convert.ToInt32(dtRevenue.Rows[0]["TotalBookings"]);
                analytics.AverageBookingValue = dtRevenue.Rows[0]["AvgBookingValue"] != DBNull.Value
                    ? Convert.ToDecimal(dtRevenue.Rows[0]["AvgBookingValue"]) : 0;
                analytics.AverageStayLength = dtRevenue.Rows[0]["AvgStayLength"] != DBNull.Value
                    ? Convert.ToDouble(dtRevenue.Rows[0]["AvgStayLength"]) : 0;
            }

            // Calculate ADR (Average Daily Rate)
            DataTable dtADR = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    SUM(TotalPrice) / NULLIF(SUM(DATEDIFF(DAY, CheckinDate, CheckoutDate)), 0) as ADR
                  FROM Reservation
                  WHERE CheckinDate >= @startDate AND CheckinDate <= @endDate
                    AND Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')",
                parameters);

            if (dtADR.Rows.Count > 0 && dtADR.Rows[0]["ADR"] != DBNull.Value)
            {
                analytics.AverageDailyRate = Convert.ToDecimal(dtADR.Rows[0]["ADR"]);
            }

            // Calculate occupancy
            DataTable dtOccupancy = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    (SELECT SUM(TotalRooms) FROM Accommodation WHERE Status = 'True') as TotalRooms,
                    DATEDIFF(DAY, @startDate, @endDate) + 1 as TotalDays,
                    (SELECT COUNT(*) FROM Reservation r
                     INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                     WHERE r.CheckinDate >= @startDate AND r.CheckinDate <= @endDate
                       AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')) as BookedNights",
                parameters);

            if (dtOccupancy.Rows.Count > 0)
            {
                int totalRooms = Convert.ToInt32(dtOccupancy.Rows[0]["TotalRooms"]);
                int totalDays = Convert.ToInt32(dtOccupancy.Rows[0]["TotalDays"]);
                int bookedNights = Convert.ToInt32(dtOccupancy.Rows[0]["BookedNights"]);
                int totalAvailable = totalRooms * totalDays;

                if (totalAvailable > 0)
                {
                    analytics.OccupancyRate = (decimal)bookedNights / totalAvailable * 100;
                }
            }

            // Calculate RevPAR
            analytics.RevPAR = analytics.AverageDailyRate * (analytics.OccupancyRate / 100);

            return analytics;
        }

        /// <summary>
        /// Get price recommendations
        /// </summary>
        public List<PriceRecommendation> GetPriceRecommendations(int accommodationId, DateTime startDate, int days = 30)
        {
            var recommendations = new List<PriceRecommendation>();

            for (int i = 0; i < days; i++)
            {
                DateTime date = startDate.AddDays(i);
                var priceResult = CalculateDynamicPrice(accommodationId, date);

                recommendations.Add(new PriceRecommendation
                {
                    Date = date,
                    CurrentPrice = priceResult.BasePrice,
                    RecommendedPrice = priceResult.FinalPrice,
                    PriceChange = priceResult.FinalPrice - priceResult.BasePrice,
                    ChangePercentage = priceResult.BasePrice > 0
                        ? ((priceResult.FinalPrice - priceResult.BasePrice) / priceResult.BasePrice) * 100
                        : 0,
                    Strategy = priceResult.PricingStrategy,
                    Reasoning = GeneratePriceReasoning(priceResult)
                });
            }

            return recommendations;
        }

        private string GeneratePriceReasoning(DynamicPriceResult result)
        {
            var reasons = new List<string>();

            if (result.Multipliers.Occupancy > 1.1m)
                reasons.Add("ความต้องการสูง");
            else if (result.Multipliers.Occupancy < 0.9m)
                reasons.Add("ความต้องการต่ำ");

            if (result.Multipliers.Season > 1.1m)
                reasons.Add("ช่วงเทศกาล/ไฮซีซัน");
            else if (result.Multipliers.Season < 0.9m)
                reasons.Add("ช่วงโลว์ซีซัน");

            if (result.Multipliers.DayOfWeek > 1.1m)
                reasons.Add("วันหยุดสุดสัปดาห์");
            else if (result.Multipliers.DayOfWeek < 0.95m)
                reasons.Add("วันธรรมดา");

            if (result.Multipliers.LeadTime > 1.2m)
                reasons.Add("จองกระชั้นชิด");
            else if (result.Multipliers.LeadTime < 0.9m)
                reasons.Add("จองล่วงหน้านาน");

            if (result.Multipliers.LengthOfStayDiscount > 0)
                reasons.Add($"ส่วนลดเข้าพักหลายคืน {result.Multipliers.LengthOfStayDiscount * 100:N0}%");

            return reasons.Count > 0 ? string.Join(", ", reasons) : "ราคาปกติ";
        }

        #endregion

        #region Season Management

        /// <summary>
        /// Create or update pricing season
        /// </summary>
        public bool SavePricingSeason(PricingSeason season)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@id", season.Id },
                    { "@name", season.SeasonName },
                    { "@startDate", season.StartDate },
                    { "@endDate", season.EndDate },
                    { "@multiplier", season.RateMultiplier },
                    { "@priority", season.Priority },
                    { "@isActive", season.IsActive },
                    { "@description", season.Description ?? "" }
                };

                if (season.Id > 0)
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Pricing_Seasons SET
                            Season_Name = @name, Start_Date = @startDate, End_Date = @endDate,
                            Rate_Multiplier = @multiplier, Priority = @priority,
                            Is_Active = @isActive, Description = @description, Modified_Date = GETDATE()
                          WHERE ID = @id",
                        parameters);
                }
                else
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"INSERT INTO Pricing_Seasons
                            (Season_Name, Start_Date, End_Date, Rate_Multiplier, Priority, Is_Active, Description, Created_Date)
                          VALUES (@name, @startDate, @endDate, @multiplier, @priority, @isActive, @description, GETDATE())",
                        parameters);
                }

                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "DynamicPricing", $"Save season failed: {ex.Message}", "SYSTEM");
                return false;
            }
        }

        /// <summary>
        /// Get all pricing seasons
        /// </summary>
        public List<PricingSeason> GetPricingSeasons()
        {
            var seasons = new List<PricingSeason>();

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT * FROM Pricing_Seasons ORDER BY Start_Date DESC",
                new Dictionary<string, object>());

            foreach (DataRow row in dt.Rows)
            {
                seasons.Add(new PricingSeason
                {
                    Id = Convert.ToInt32(row["ID"]),
                    SeasonName = row["Season_Name"].ToString(),
                    StartDate = Convert.ToDateTime(row["Start_Date"]),
                    EndDate = Convert.ToDateTime(row["End_Date"]),
                    RateMultiplier = Convert.ToDecimal(row["Rate_Multiplier"]),
                    Priority = Convert.ToInt32(row["Priority"]),
                    IsActive = Convert.ToBoolean(row["Is_Active"]),
                    Description = row["Description"]?.ToString()
                });
            }

            return seasons;
        }

        #endregion
    }

    #region Data Models

    public class DynamicPriceResult
    {
        public int AccommodationId { get; set; }
        public DateTime Date { get; set; }
        public decimal BasePrice { get; set; }
        public decimal FinalPrice { get; set; }
        public decimal TotalMultiplier { get; set; }
        public PriceMultipliers Multipliers { get; set; }
        public string PricingStrategy { get; set; }
    }

    public class PriceMultipliers
    {
        public decimal Occupancy { get; set; }
        public decimal Season { get; set; }
        public decimal Demand { get; set; }
        public decimal DayOfWeek { get; set; }
        public decimal LeadTime { get; set; }
        public decimal LengthOfStayDiscount { get; set; }
    }

    public class PricingRevenueAnalytics
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalBookings { get; set; }
        public decimal AverageBookingValue { get; set; }
        public double AverageStayLength { get; set; }
        public decimal AverageDailyRate { get; set; }  // ADR
        public decimal OccupancyRate { get; set; }
        public decimal RevPAR { get; set; }  // Revenue Per Available Room
    }

    public class PriceRecommendation
    {
        public DateTime Date { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal RecommendedPrice { get; set; }
        public decimal PriceChange { get; set; }
        public decimal ChangePercentage { get; set; }
        public string Strategy { get; set; }
        public string Reasoning { get; set; }
    }

    public class PricingSeason
    {
        public int Id { get; set; }
        public string SeasonName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal RateMultiplier { get; set; }
        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public string Description { get; set; }
    }

    #endregion
}
