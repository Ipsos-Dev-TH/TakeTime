// ===========================================================================
// TierBenefitsService.cs
// Tier Benefits Management Service (Accor-style)
// Handles accommodation discounts, product discounts, and member perks
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// Service for managing tier-based benefits and discounts
    /// </summary>
    public class TierBenefitsService
    {
        private readonly string _connectionString;
        private readonly code _code;

        public TierBenefitsService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _code = new code();
        }

        #region Accommodation Discounts

        /// <summary>
        /// Calculate accommodation discount for customer
        /// </summary>
        public AccommodationDiscountResult CalculateAccommodationDiscount(
            string customerPhone, decimal originalAmount)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone },
                    { "@OriginalAmount", originalAmount }
                };

                // Use SQL function to calculate
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        [dbo].[fn_GetAccommodationDiscount](@CustomerPhone, @OriginalAmount) AS DiscountAmount,
                        CL.CurrentTier_ID AS TierID,
                        LT.TierName,
                        LT.AccommodationDiscountPercent
                      FROM Customer_Loyalty CL
                      INNER JOIN Loyalty_Tiers LT ON LT.ID = CL.CurrentTier_ID
                      WHERE CL.Customer_MobilePhone = @CustomerPhone",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    decimal discountAmount = row["DiscountAmount"] != DBNull.Value
                        ? Convert.ToDecimal(row["DiscountAmount"]) : 0;

                    return new AccommodationDiscountResult
                    {
                        Success = true,
                        OriginalAmount = originalAmount,
                        DiscountAmount = discountAmount,
                        FinalAmount = originalAmount - discountAmount,
                        DiscountPercent = row["AccommodationDiscountPercent"] != DBNull.Value
                            ? Convert.ToDecimal(row["AccommodationDiscountPercent"]) : 0,
                        TierName = row["TierName"].ToString()
                    };
                }

                return new AccommodationDiscountResult
                {
                    Success = false,
                    Message = "Customer not found in loyalty program"
                };
            }
            catch (Exception ex)
            {
                return new AccommodationDiscountResult
                {
                    Success = false,
                    Message = "Error: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Apply accommodation discount and log usage
        /// </summary>
        public AccommodationDiscountResult ApplyAccommodationDiscount(
            long reservationId, string customerPhone, decimal originalAmount, short? adminId = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ReservationID", reservationId },
                    { "@CustomerPhone", customerPhone },
                    { "@OriginalAmount", originalAmount },
                    { "@AdminID", adminId ?? (object)DBNull.Value }
                };

                using (var conn = new System.Data.SqlClient.SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("sp_ApplyAccommodationDiscount", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.CommandTimeout = 30;

                        cmd.Parameters.AddWithValue("@ReservationID", reservationId);
                        cmd.Parameters.AddWithValue("@CustomerPhone", customerPhone);
                        cmd.Parameters.AddWithValue("@OriginalAmount", originalAmount);
                        cmd.Parameters.AddWithValue("@AdminID", adminId ?? (object)DBNull.Value);

                        var discountParam = cmd.Parameters.Add("@DiscountAmount", System.Data.SqlDbType.Decimal);
                        discountParam.Direction = System.Data.ParameterDirection.Output;
                        discountParam.Precision = 10;
                        discountParam.Scale = 2;

                        var finalParam = cmd.Parameters.Add("@FinalAmount", System.Data.SqlDbType.Decimal);
                        finalParam.Direction = System.Data.ParameterDirection.Output;
                        finalParam.Precision = 10;
                        finalParam.Scale = 2;

                        cmd.ExecuteNonQuery();

                        decimal discountAmount = discountParam.Value != DBNull.Value
                            ? Convert.ToDecimal(discountParam.Value) : 0;
                        decimal finalAmount = finalParam.Value != DBNull.Value
                            ? Convert.ToDecimal(finalParam.Value) : originalAmount;

                        return new AccommodationDiscountResult
                        {
                            Success = true,
                            OriginalAmount = originalAmount,
                            DiscountAmount = discountAmount,
                            FinalAmount = finalAmount,
                            Message = discountAmount > 0
                                ? $"Applied discount: ฿{discountAmount:N2}"
                                : "No discount applied"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new AccommodationDiscountResult
                {
                    Success = false,
                    Message = "Error: " + ex.Message
                };
            }
        }

        #endregion

        #region Product Category Discounts

        /// <summary>
        /// Calculate product discount for customer
        /// </summary>
        public ProductDiscountResult CalculateProductDiscount(
            string customerPhone, long productCategoryId, decimal originalAmount)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone },
                    { "@ProductCategoryID", productCategoryId },
                    { "@OriginalAmount", originalAmount }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        [dbo].[fn_GetProductCategoryDiscount](@CustomerPhone, @ProductCategoryID, @OriginalAmount) AS DiscountAmount,
                        LPCD.DiscountPercent,
                        LPCD.MaxDiscountAmount,
                        PC.Category_Name AS CategoryName
                      FROM Customer_Loyalty CL
                      LEFT JOIN Loyalty_Product_Category_Discounts LPCD
                        ON LPCD.Tier_ID = CL.CurrentTier_ID
                        AND LPCD.ProductCategory_ID = @ProductCategoryID
                        AND LPCD.IsActive = 1
                      LEFT JOIN Product_Category PC ON PC.ID = @ProductCategoryID
                      WHERE CL.Customer_MobilePhone = @CustomerPhone",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    decimal discountAmount = row["DiscountAmount"] != DBNull.Value
                        ? Convert.ToDecimal(row["DiscountAmount"]) : 0;

                    return new ProductDiscountResult
                    {
                        Success = true,
                        OriginalAmount = originalAmount,
                        DiscountAmount = discountAmount,
                        FinalAmount = originalAmount - discountAmount,
                        DiscountPercent = row["DiscountPercent"] != DBNull.Value
                            ? Convert.ToDecimal(row["DiscountPercent"]) : 0,
                        CategoryName = row["CategoryName"]?.ToString()
                    };
                }

                return new ProductDiscountResult
                {
                    Success = false,
                    OriginalAmount = originalAmount,
                    FinalAmount = originalAmount,
                    Message = "No discount available for this category"
                };
            }
            catch (Exception ex)
            {
                return new ProductDiscountResult
                {
                    Success = false,
                    OriginalAmount = originalAmount,
                    FinalAmount = originalAmount,
                    Message = "Error: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Apply product discount and log usage
        /// </summary>
        public ProductDiscountResult ApplyProductDiscount(
            string customerPhone, long productCategoryId, decimal originalAmount,
            long? reservationId = null, long? receiptId = null, short? adminId = null)
        {
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("sp_ApplyProductDiscount", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.CommandTimeout = 30;

                        cmd.Parameters.AddWithValue("@CustomerPhone", customerPhone);
                        cmd.Parameters.AddWithValue("@ProductCategoryID", productCategoryId);
                        cmd.Parameters.AddWithValue("@OriginalAmount", originalAmount);
                        cmd.Parameters.AddWithValue("@ReservationID", reservationId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ReceiptID", receiptId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@AdminID", adminId ?? (object)DBNull.Value);

                        var discountParam = cmd.Parameters.Add("@DiscountAmount", System.Data.SqlDbType.Decimal);
                        discountParam.Direction = System.Data.ParameterDirection.Output;
                        discountParam.Precision = 10;
                        discountParam.Scale = 2;

                        var finalParam = cmd.Parameters.Add("@FinalAmount", System.Data.SqlDbType.Decimal);
                        finalParam.Direction = System.Data.ParameterDirection.Output;
                        finalParam.Precision = 10;
                        finalParam.Scale = 2;

                        cmd.ExecuteNonQuery();

                        decimal discountAmount = discountParam.Value != DBNull.Value
                            ? Convert.ToDecimal(discountParam.Value) : 0;
                        decimal finalAmount = finalParam.Value != DBNull.Value
                            ? Convert.ToDecimal(finalParam.Value) : originalAmount;

                        return new ProductDiscountResult
                        {
                            Success = true,
                            OriginalAmount = originalAmount,
                            DiscountAmount = discountAmount,
                            FinalAmount = finalAmount,
                            Message = discountAmount > 0
                                ? $"Applied discount: ฿{discountAmount:N2}"
                                : "No discount applied"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ProductDiscountResult
                {
                    Success = false,
                    OriginalAmount = originalAmount,
                    FinalAmount = originalAmount,
                    Message = "Error: " + ex.Message
                };
            }
        }

        #endregion

        #region Benefits Management

        /// <summary>
        /// Get all benefits for customer's tier
        /// </summary>
        public List<TierBenefit> GetCustomerBenefits(string customerPhone)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT * FROM [dbo].[fn_GetCustomerAvailableBenefits](@CustomerPhone)
                      ORDER BY BenefitType, DisplayOrder",
                    parameters);

                var benefits = new List<TierBenefit>();
                foreach (DataRow row in dt.Rows)
                {
                    benefits.Add(new TierBenefit
                    {
                        BenefitId = Convert.ToInt32(row["BenefitID"]),
                        BenefitType = row["BenefitType"].ToString(),
                        BenefitName = row["BenefitName"].ToString(),
                        BenefitNameEN = row["BenefitNameEN"]?.ToString(),
                        Description = row["Description"]?.ToString(),
                        DiscountType = row["DiscountType"]?.ToString(),
                        DiscountValue = row["DiscountValue"] != DBNull.Value
                            ? Convert.ToDecimal(row["DiscountValue"]) : (decimal?)null,
                        MaxDiscountAmount = row["MaxDiscountAmount"] != DBNull.Value
                            ? Convert.ToDecimal(row["MaxDiscountAmount"]) : (decimal?)null,
                        TimeValue = row["TimeValue"] != DBNull.Value
                            ? Convert.ToInt32(row["TimeValue"]) : (int?)null,
                        QuantityValue = row["QuantityValue"] != DBNull.Value
                            ? Convert.ToInt32(row["QuantityValue"]) : (int?)null,
                        Icon = row["Icon"]?.ToString(),
                        ColorCode = row["ColorCode"]?.ToString(),
                        IsAvailable = row["IsAvailable"] != DBNull.Value && Convert.ToBoolean(row["IsAvailable"])
                    });
                }

                return benefits;
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting customer benefits: " + ex.Message);
            }
        }

        /// <summary>
        /// Get benefits summary by tier
        /// </summary>
        public DataTable GetTierBenefitsSummary()
        {
            try
            {
                return _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM vw_Tier_Benefits_Summary ORDER BY TierID",
                    null);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting tier benefits summary: " + ex.Message);
            }
        }

        /// <summary>
        /// Check if customer can use a specific benefit
        /// </summary>
        public bool CanUseBenefit(string customerPhone, int benefitId, long? reservationId = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone },
                    { "@BenefitID", benefitId },
                    { "@ReservationID", reservationId ?? (object)DBNull.Value }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT [dbo].[fn_CanUseBenefit](@CustomerPhone, @BenefitID, @ReservationID) AS CanUse",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    return Convert.ToBoolean(dt.Rows[0]["CanUse"]);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get benefit usage history for customer
        /// </summary>
        public DataTable GetBenefitUsageHistory(string customerPhone, int limit = 50)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone },
                    { "@Limit", limit }
                };

                return _code.DatabaseQuerySafe(_connectionString,
                    code.AdaptSql($@"SELECT TOP {limit} LBU.*, LTB.BenefitName, LTB.BenefitType
                      FROM Loyalty_Benefit_Usage LBU
                      LEFT JOIN Loyalty_Tier_Benefits LTB ON LTB.ID = LBU.Benefit_ID
                      WHERE LBU.Customer_MobilePhone = @CustomerPhone
                      ORDER BY LBU.UsageDate DESC"),
                    parameters);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting benefit usage history: " + ex.Message);
            }
        }

        /// <summary>
        /// Get benefit usage statistics
        /// </summary>
        public DataTable GetBenefitUsageStatistics()
        {
            try
            {
                return _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM vw_Benefit_Usage_Statistics ORDER BY TotalDiscountGiven DESC",
                    null);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting benefit statistics: " + ex.Message);
            }
        }

        #endregion

        #region Product Category Discount Management

        /// <summary>
        /// Get all product category discounts by tier
        /// </summary>
        public DataTable GetProductCategoryDiscounts(byte? tierId = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>();
                string query = @"
                    SELECT LPCD.*, LT.TierName, PC.Category_Name AS CategoryName
                    FROM Loyalty_Product_Category_Discounts LPCD
                    INNER JOIN Loyalty_Tiers LT ON LT.ID = LPCD.Tier_ID
                    INNER JOIN Product_Category PC ON PC.ID = LPCD.ProductCategory_ID
                    WHERE LPCD.IsActive = 1";

                if (tierId.HasValue)
                {
                    query += " AND LPCD.Tier_ID = @TierID";
                    parameters["@TierID"] = tierId.Value;
                }

                query += " ORDER BY LPCD.Tier_ID, PC.Category_Name";

                return _code.DatabaseQuerySafe(_connectionString, query, parameters);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting product category discounts: " + ex.Message);
            }
        }

        /// <summary>
        /// Add or update product category discount
        /// </summary>
        public bool UpsertProductCategoryDiscount(
            byte tierId, long productCategoryId, decimal discountPercent,
            decimal? maxDiscountAmount = null, decimal? minPurchaseAmount = null,
            DateTime? validFrom = null, DateTime? validTo = null, short? adminId = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@TierID", tierId },
                    { "@ProductCategoryID", productCategoryId },
                    { "@DiscountPercent", discountPercent },
                    { "@MaxDiscountAmount", maxDiscountAmount ?? (object)DBNull.Value },
                    { "@MinPurchaseAmount", minPurchaseAmount ?? (object)DBNull.Value },
                    { "@ValidFrom", validFrom ?? (object)DBNull.Value },
                    { "@ValidTo", validTo ?? (object)DBNull.Value },
                    { "@AdminID", adminId ?? (object)DBNull.Value }
                };

                // Check if exists
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID FROM Loyalty_Product_Category_Discounts
                      WHERE Tier_ID = @TierID AND ProductCategory_ID = @ProductCategoryID",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    // Update
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Loyalty_Product_Category_Discounts
                          SET DiscountPercent = @DiscountPercent,
                              MaxDiscountAmount = @MaxDiscountAmount,
                              MinPurchaseAmount = @MinPurchaseAmount,
                              ValidFrom = @ValidFrom,
                              ValidTo = @ValidTo,
                              IsActive = 1
                          WHERE Tier_ID = @TierID AND ProductCategory_ID = @ProductCategoryID",
                        parameters);
                }
                else
                {
                    // Insert
                    _code.DatabaseInsertSafe(_connectionString,
                        @"INSERT INTO Loyalty_Product_Category_Discounts
                          (Tier_ID, ProductCategory_ID, DiscountPercent, MaxDiscountAmount,
                           MinPurchaseAmount, ValidFrom, ValidTo, CreatedBy_AdminID)
                          VALUES (@TierID, @ProductCategoryID, @DiscountPercent, @MaxDiscountAmount,
                                  @MinPurchaseAmount, @ValidFrom, @ValidTo, @AdminID)",
                        parameters);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }

    #region Data Transfer Objects

    /// <summary>
    /// Accommodation discount calculation result
    /// </summary>
    public class AccommodationDiscountResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public decimal DiscountPercent { get; set; }
        public string TierName { get; set; }
    }

    /// <summary>
    /// Product discount calculation result
    /// </summary>
    public class ProductDiscountResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public decimal DiscountPercent { get; set; }
        public string CategoryName { get; set; }
    }

    /// <summary>
    /// Tier benefit information
    /// </summary>
    public class TierBenefit
    {
        public int BenefitId { get; set; }
        public string BenefitType { get; set; }
        public string BenefitName { get; set; }
        public string BenefitNameEN { get; set; }
        public string Description { get; set; }
        public string DiscountType { get; set; }
        public decimal? DiscountValue { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public int? TimeValue { get; set; }
        public int? QuantityValue { get; set; }
        public string Icon { get; set; }
        public string ColorCode { get; set; }
        public bool IsAvailable { get; set; }
    }

    #endregion
}
