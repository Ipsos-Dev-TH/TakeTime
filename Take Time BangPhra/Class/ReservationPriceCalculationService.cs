using System;
using System.Data;

namespace Take_Time_BangPhra.Class
{
    /// <summary>
    /// 💰 Reservation Price Calculation Service
    /// Centralized service for calculating reservation total prices
    /// Ensures consistent price calculation across the system
    /// Created: 2025-11-17
    /// </summary>
    public class ReservationPriceCalculationService
    {
        private readonly code _code;
        private readonly string _connectionString;

        public ReservationPriceCalculationService(string connectionString)
        {
            _code = new code();
            _connectionString = connectionString;
        }

        #region Price Calculation

        /// <summary>
        /// Calculate total reservation price
        /// Total = Accommodation Price + Items Price + Product Charges
        /// </summary>
        /// <param name="reservationId">Reservation ID</param>
        /// <returns>Total price breakdown</returns>
        public ReservationPriceBreakdown GetReservationPriceBreakdown(int reservationId)
        {
            var breakdown = new ReservationPriceBreakdown();

            try
            {
                // 1. Get base reservation total (accommodations + items from Reservation table)
                var reservationParams = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };

                DataTable dtReservation = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        ISNULL(TotalPrice, 0) as BaseTotal,
                        ISNULL(Deposit, 0) as TotalPaid,
                        StayDays
                      FROM Reservation
                      WHERE ID = @reservationId",
                    reservationParams);

                if (dtReservation.Rows.Count == 0)
                {
                    throw new Exception($"Reservation {reservationId} not found");
                }

                breakdown.BaseTotal = Convert.ToDecimal(dtReservation.Rows[0]["BaseTotal"]);
                breakdown.TotalPaid = Convert.ToDecimal(dtReservation.Rows[0]["TotalPaid"]);
                breakdown.StayDays = Convert.ToInt32(dtReservation.Rows[0]["StayDays"]);

                // 2. Get product charges (ALL charges except CANCELLED)
                DataTable dtProductCharges = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ISNULL(SUM(TotalAmount), 0) as ProductCharges
                      FROM Reservation_Product_Charges
                      WHERE Reservation_ID = @reservationId
                      AND Status <> 'CANCELLED'",
                    reservationParams);

                if (dtProductCharges.Rows.Count > 0 && dtProductCharges.Rows[0]["ProductCharges"] != DBNull.Value)
                {
                    breakdown.ProductCharges = Convert.ToDecimal(dtProductCharges.Rows[0]["ProductCharges"]);
                }

                // 3. Calculate grand total
                breakdown.GrandTotal = breakdown.BaseTotal + breakdown.ProductCharges;
                breakdown.RemainingBalance = breakdown.GrandTotal - breakdown.TotalPaid;

                return breakdown;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString,
                    "ReservationPriceCalculationService.GetReservationPriceBreakdown Error",
                    $"Reservation ID: {reservationId}, Error: {ex.Message}",
                    "SYSTEM");
                throw;
            }
        }

        /// <summary>
        /// Calculate total from accommodations and items (for new/edit reservations)
        /// This calculates the base price WITHOUT product charges
        /// </summary>
        public decimal CalculateBaseTotalFromGridView(
            DataTable dtAccommodations,
            DataTable dtItems,
            int stayDays)
        {
            decimal total = 0;

            try
            {
                // 1. Calculate accommodation prices
                if (dtAccommodations != null)
                {
                    foreach (DataRow row in dtAccommodations.Rows)
                    {
                        if (row.RowState == DataRowState.Deleted) continue;

                        bool isLimitWithPeople = row["LimitWithPeople"]?.ToString() == "True";
                        decimal pricePerUnit = Convert.ToDecimal(row["Price"]);
                        int amount = Convert.ToInt32(row["Amount"]); // People count or quantity

                        if (isLimitWithPeople)
                        {
                            // Price per person per night
                            total += pricePerUnit * amount * stayDays;
                        }
                        else
                        {
                            // Price per unit per night
                            total += pricePerUnit * stayDays;
                        }
                    }
                }

                // 2. Calculate items prices
                if (dtItems != null)
                {
                    foreach (DataRow row in dtItems.Rows)
                    {
                        if (row.RowState == DataRowState.Deleted) continue;

                        decimal pricePerUnit = Convert.ToDecimal(row["Price"]);
                        int amount = Convert.ToInt32(row["Amount"]);

                        total += pricePerUnit * amount * stayDays;
                    }
                }

                return total;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString,
                    "ReservationPriceCalculationService.CalculateBaseTotalFromGridView Error",
                    $"Stay Days: {stayDays}, Error: {ex.Message}",
                    "SYSTEM");
                throw;
            }
        }

        /// <summary>
        /// Get accommodation price for a specific date (with coupon support)
        /// </summary>
        public decimal GetAccommodationPrice(int accommodationId, DateTime checkInDate, bool useCoupon = false)
        {
            try
            {
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@accommodationId", accommodationId },
                    { "@checkInDate", checkInDate.ToString("yyyy-MM-dd") }
                };

                // Check if there's a special price for this date
                DataTable dtSpecialPrice = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 Price
                      FROM Accommodation_Price
                      WHERE Accommodation_ID = @accommodationId
                      AND Date = @checkInDate
                      ORDER BY Date DESC",
                    parameters);

                decimal price = 0;

                if (dtSpecialPrice.Rows.Count > 0)
                {
                    // Use special price
                    price = Convert.ToDecimal(dtSpecialPrice.Rows[0]["Price"]);
                }
                else
                {
                    // Use default price from Accommodation table
                    var accomParams = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@accommodationId", accommodationId }
                    };

                    DataTable dtAccom = _code.DatabaseQuerySafe(_connectionString,
                        "SELECT Price FROM Accommodation WHERE ID = @accommodationId",
                        accomParams);

                    if (dtAccom.Rows.Count > 0)
                    {
                        price = Convert.ToDecimal(dtAccom.Rows[0]["Price"]);
                    }
                }

                // Apply coupon discount if applicable
                if (useCoupon && price > 0)
                {
                    // Check for active coupon/promotion
                    var couponResult = ApplyCouponDiscount(accommodationId, checkInDate, price);
                    if (couponResult.HasDiscount)
                    {
                        price = couponResult.DiscountedPrice;
                    }
                }

                return price;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString,
                    "ReservationPriceCalculationService.GetAccommodationPrice Error",
                    $"Accommodation ID: {accommodationId}, Date: {checkInDate:yyyy-MM-dd}, Error: {ex.Message}",
                    "SYSTEM");
                throw;
            }
        }

        #endregion

        #region Payment Calculations

        /// <summary>
        /// Get total paid amount from Payment_History
        /// </summary>
        public decimal GetTotalPaidAmount(int reservationId)
        {
            try
            {
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };

                DataTable dtPayments = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ISNULL(SUM(Amount), 0) as TotalPaid
                      FROM Payment_History
                      WHERE Reservation_ID = @reservationId
                      AND Status = 'APPROVED'",
                    parameters);

                if (dtPayments.Rows.Count > 0)
                {
                    return Convert.ToDecimal(dtPayments.Rows[0]["TotalPaid"]);
                }

                return 0;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString,
                    "ReservationPriceCalculationService.GetTotalPaidAmount Error",
                    $"Reservation ID: {reservationId}, Error: {ex.Message}",
                    "SYSTEM");
                return 0; // Don't throw - return 0 if error
            }
        }

        #endregion

        #region Coupon/Promotion System

        /// <summary>
        /// Apply coupon or promotional discount to price
        /// </summary>
        private CouponResult ApplyCouponDiscount(int accommodationId, DateTime checkInDate, decimal originalPrice)
        {
            try
            {
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@accommodationId", accommodationId },
                    { "@checkInDate", checkInDate },
                    { "@today", DateTime.Today }
                };

                // Check for active promotions
                DataTable dtPromo = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1
                        p.ID, p.Promotion_Name, p.Discount_Type, p.Discount_Value,
                        p.Min_Stay_Days, p.Max_Discount_Amount
                      FROM Promotions p
                      LEFT JOIN Promotion_Accommodations pa ON p.ID = pa.Promotion_ID
                      WHERE p.Is_Active = 1
                        AND p.Start_Date <= @checkInDate
                        AND p.End_Date >= @checkInDate
                        AND (pa.Accommodation_ID = @accommodationId OR pa.Accommodation_ID IS NULL)
                      ORDER BY p.Priority DESC, p.Discount_Value DESC",
                    parameters);

                if (dtPromo.Rows.Count > 0)
                {
                    var row = dtPromo.Rows[0];
                    string discountType = row["Discount_Type"]?.ToString() ?? "PERCENT";
                    decimal discountValue = Convert.ToDecimal(row["Discount_Value"]);
                    decimal maxDiscount = row["Max_Discount_Amount"] != DBNull.Value
                        ? Convert.ToDecimal(row["Max_Discount_Amount"])
                        : decimal.MaxValue;

                    decimal discountAmount = 0;
                    if (discountType == "PERCENT")
                    {
                        discountAmount = originalPrice * (discountValue / 100);
                    }
                    else // FIXED
                    {
                        discountAmount = discountValue;
                    }

                    // Apply max discount limit
                    if (discountAmount > maxDiscount)
                    {
                        discountAmount = maxDiscount;
                    }

                    return new CouponResult
                    {
                        HasDiscount = true,
                        PromotionId = Convert.ToInt32(row["ID"]),
                        PromotionName = row["Promotion_Name"]?.ToString(),
                        DiscountType = discountType,
                        DiscountValue = discountValue,
                        DiscountAmount = discountAmount,
                        OriginalPrice = originalPrice,
                        DiscountedPrice = originalPrice - discountAmount
                    };
                }

                return new CouponResult
                {
                    HasDiscount = false,
                    OriginalPrice = originalPrice,
                    DiscountedPrice = originalPrice
                };
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString,
                    "ReservationPriceCalculationService.ApplyCouponDiscount Error",
                    $"Accommodation ID: {accommodationId}, Error: {ex.Message}",
                    "SYSTEM");

                // Return original price on error
                return new CouponResult
                {
                    HasDiscount = false,
                    OriginalPrice = originalPrice,
                    DiscountedPrice = originalPrice
                };
            }
        }

        /// <summary>
        /// Validate and apply coupon code
        /// </summary>
        public CouponResult ValidateCouponCode(string couponCode, int accommodationId, DateTime checkInDate, int stayDays, decimal totalPrice)
        {
            try
            {
                if (string.IsNullOrEmpty(couponCode))
                {
                    return new CouponResult { HasDiscount = false, ErrorMessage = "กรุณาใส่รหัสคูปอง" };
                }

                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@couponCode", couponCode.Trim().ToUpper() },
                    { "@accommodationId", accommodationId },
                    { "@checkInDate", checkInDate },
                    { "@stayDays", stayDays }
                };

                DataTable dtCoupon = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        c.ID, c.Coupon_Code, c.Discount_Type, c.Discount_Value,
                        c.Min_Order_Amount, c.Max_Discount_Amount, c.Min_Stay_Days,
                        c.Usage_Limit, c.Used_Count, c.Start_Date, c.End_Date,
                        c.Is_Active, c.Description
                      FROM Coupons c
                      WHERE c.Coupon_Code = @couponCode",
                    parameters);

                if (dtCoupon.Rows.Count == 0)
                {
                    return new CouponResult { HasDiscount = false, ErrorMessage = "ไม่พบรหัสคูปองนี้" };
                }

                var coupon = dtCoupon.Rows[0];

                // Validate coupon
                if (Convert.ToBoolean(coupon["Is_Active"]) == false)
                {
                    return new CouponResult { HasDiscount = false, ErrorMessage = "คูปองนี้ไม่สามารถใช้งานได้" };
                }

                DateTime startDate = Convert.ToDateTime(coupon["Start_Date"]);
                DateTime endDate = Convert.ToDateTime(coupon["End_Date"]);
                if (checkInDate < startDate || checkInDate > endDate)
                {
                    return new CouponResult { HasDiscount = false, ErrorMessage = $"คูปองใช้ได้ระหว่าง {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}" };
                }

                int usageLimit = Convert.ToInt32(coupon["Usage_Limit"]);
                int usedCount = Convert.ToInt32(coupon["Used_Count"]);
                if (usageLimit > 0 && usedCount >= usageLimit)
                {
                    return new CouponResult { HasDiscount = false, ErrorMessage = "คูปองนี้ถูกใช้งานครบแล้ว" };
                }

                decimal minOrder = coupon["Min_Order_Amount"] != DBNull.Value
                    ? Convert.ToDecimal(coupon["Min_Order_Amount"]) : 0;
                if (totalPrice < minOrder)
                {
                    return new CouponResult { HasDiscount = false, ErrorMessage = $"ยอดขั้นต่ำสำหรับคูปองนี้คือ {minOrder:N0} บาท" };
                }

                int minStayDays = coupon["Min_Stay_Days"] != DBNull.Value
                    ? Convert.ToInt32(coupon["Min_Stay_Days"]) : 0;
                if (stayDays < minStayDays)
                {
                    return new CouponResult { HasDiscount = false, ErrorMessage = $"คูปองนี้ใช้ได้เมื่อเข้าพักอย่างน้อย {minStayDays} คืน" };
                }

                // Calculate discount
                string discountType = coupon["Discount_Type"].ToString();
                decimal discountValue = Convert.ToDecimal(coupon["Discount_Value"]);
                decimal maxDiscount = coupon["Max_Discount_Amount"] != DBNull.Value
                    ? Convert.ToDecimal(coupon["Max_Discount_Amount"]) : decimal.MaxValue;

                decimal discountAmount = 0;
                if (discountType == "PERCENT")
                {
                    discountAmount = totalPrice * (discountValue / 100);
                }
                else // FIXED
                {
                    discountAmount = discountValue;
                }

                if (discountAmount > maxDiscount)
                {
                    discountAmount = maxDiscount;
                }

                return new CouponResult
                {
                    HasDiscount = true,
                    CouponId = Convert.ToInt32(coupon["ID"]),
                    CouponCode = couponCode.Trim().ToUpper(),
                    DiscountType = discountType,
                    DiscountValue = discountValue,
                    DiscountAmount = discountAmount,
                    OriginalPrice = totalPrice,
                    DiscountedPrice = totalPrice - discountAmount,
                    Description = coupon["Description"]?.ToString()
                };
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString,
                    "ReservationPriceCalculationService.ValidateCouponCode Error",
                    $"Coupon: {couponCode}, Error: {ex.Message}",
                    "SYSTEM");

                return new CouponResult { HasDiscount = false, ErrorMessage = "เกิดข้อผิดพลาดในการตรวจสอบคูปอง" };
            }
        }

        #endregion
    }

    /// <summary>
    /// Coupon/Promotion discount result
    /// </summary>
    public class CouponResult
    {
        public bool HasDiscount { get; set; }
        public int CouponId { get; set; }
        public int PromotionId { get; set; }
        public string CouponCode { get; set; }
        public string PromotionName { get; set; }
        public string DiscountType { get; set; } // PERCENT or FIXED
        public decimal DiscountValue { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountedPrice { get; set; }
        public string Description { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Reservation Price Breakdown
    /// Contains all price components for a reservation
    /// </summary>
    public class ReservationPriceBreakdown
    {
        /// <summary>
        /// Base total from Reservation table (Accommodations + Items)
        /// Does NOT include Product Charges
        /// </summary>
        public decimal BaseTotal { get; set; }

        /// <summary>
        /// Product charges from Reservation_Product_Charges table
        /// Sum of all charges with Status != 'CANCELLED'
        /// </summary>
        public decimal ProductCharges { get; set; }

        /// <summary>
        /// Grand total = BaseTotal + ProductCharges
        /// </summary>
        public decimal GrandTotal { get; set; }

        /// <summary>
        /// Total amount paid (from Deposit or Payment_History)
        /// </summary>
        public decimal TotalPaid { get; set; }

        /// <summary>
        /// Remaining balance = GrandTotal - TotalPaid
        /// </summary>
        public decimal RemainingBalance { get; set; }

        /// <summary>
        /// Number of stay days
        /// </summary>
        public int StayDays { get; set; }

        public ReservationPriceBreakdown()
        {
            BaseTotal = 0;
            ProductCharges = 0;
            GrandTotal = 0;
            TotalPaid = 0;
            RemainingBalance = 0;
            StayDays = 0;
        }

        public override string ToString()
        {
            return $"Base: {BaseTotal:N2}, ProductCharges: {ProductCharges:N2}, " +
                   $"GrandTotal: {GrandTotal:N2}, Paid: {TotalPaid:N2}, " +
                   $"Remaining: {RemainingBalance:N2}";
        }
    }
}
