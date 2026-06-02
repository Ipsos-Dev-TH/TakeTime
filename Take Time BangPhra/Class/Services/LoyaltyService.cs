// ===========================================================================
// LoyaltyService.cs
// Loyalty Program Management Service
// Handles points earning, redemption, tier management, and rewards
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// Service for managing customer loyalty program
    /// </summary>
    public class LoyaltyService
    {
        private readonly string _connectionString;
        private readonly code _code;

        public LoyaltyService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _code = new code();
        }

        #region Points Management

        /// <summary>
        /// Award loyalty points to customer
        /// </summary>
        public LoyaltyResult EarnPoints(string customerPhone, int points, long? reservationId = null,
            long? receiptId = null, string description = null, int expiryMonths = 12, short? adminId = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone },
                    { "@Points", points },
                    { "@ReservationID", reservationId ?? (object)DBNull.Value },
                    { "@ReceiptID", receiptId ?? (object)DBNull.Value },
                    { "@Description", description ?? (object)DBNull.Value },
                    { "@ExpiryMonths", expiryMonths },
                    { "@AdminID", adminId ?? (object)DBNull.Value }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "EXEC sp_EarnLoyaltyPoints @CustomerPhone, @Points, @ReservationID, @ReceiptID, @Description, @ExpiryMonths, @AdminID",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    return new LoyaltyResult
                    {
                        Success = row["Result"].ToString() == "SUCCESS",
                        Message = row["Result"].ToString(),
                        PointsEarned = row["PointsEarned"] != DBNull.Value ? Convert.ToInt32(row["PointsEarned"]) : 0,
                        NewBalance = row["NewBalance"] != DBNull.Value ? Convert.ToInt32(row["NewBalance"]) : 0
                    };
                }

                return new LoyaltyResult { Success = false, Message = "Unknown error" };
            }
            catch (Exception ex)
            {
                return new LoyaltyResult { Success = false, Message = "Error: " + ex.Message };
            }
        }

        /// <summary>
        /// Redeem loyalty points for rewards
        /// </summary>
        public LoyaltyResult RedeemPoints(string customerPhone, int points, int? rewardId = null,
            string description = null, short? adminId = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone },
                    { "@Points", points },
                    { "@RewardID", rewardId ?? (object)DBNull.Value },
                    { "@Description", description ?? (object)DBNull.Value },
                    { "@AdminID", adminId ?? (object)DBNull.Value }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "EXEC sp_RedeemLoyaltyPoints @CustomerPhone, @Points, @RewardID, @Description, @AdminID",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    return new LoyaltyResult
                    {
                        Success = row["Result"].ToString() == "SUCCESS",
                        Message = row["Result"].ToString(),
                        PointsRedeemed = row.Table.Columns.Contains("PointsRedeemed") && row["PointsRedeemed"] != DBNull.Value
                            ? Convert.ToInt32(row["PointsRedeemed"]) : 0,
                        NewBalance = row["NewBalance"] != DBNull.Value ? Convert.ToInt32(row["NewBalance"]) : 0
                    };
                }

                return new LoyaltyResult { Success = false, Message = "Unknown error" };
            }
            catch (Exception ex)
            {
                return new LoyaltyResult { Success = false, Message = "Error: " + ex.Message };
            }
        }

        /// <summary>
        /// Automatically award points based on reservation amount
        /// Default: 1 point per 100 Baht spent
        /// </summary>
        public LoyaltyResult EarnPointsFromReservation(string customerPhone, long reservationId,
            decimal totalAmount, short? adminId = null)
        {
            // Calculate points: 1 point per 100 Baht
            int points = (int)Math.Floor(totalAmount / 100);

            if (points <= 0)
                return new LoyaltyResult { Success = false, Message = "Amount too small to earn points" };

            string description = $"คะแนนจากการจอง #{reservationId} (฿{totalAmount:N2})";

            return EarnPoints(customerPhone, points, reservationId, null, description, 12, adminId);
        }

        /// <summary>
        /// Get customer's loyalty balance (dual-pool: yearly tier points + redeemable points)
        /// </summary>
        public CustomerLoyaltyInfo GetLoyaltyInfo(string customerPhone)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT CL.*, LT.TierName, LT.TierNameEN, LT.TierColor, LT.PointsMultiplier, LT.DiscountPercent, LT.MinPoints AS TierMinPoints,
                        (SELECT TOP 1 NT.MinPoints FROM Loyalty_Tiers NT
                           WHERE NT.MinPoints > ISNULL(CL.YearlyPoints, 0) AND NT.IsActive = 1
                           ORDER BY NT.MinPoints ASC) AS NextTierMinPoints,
                        (SELECT TOP 1 NT.TierName FROM Loyalty_Tiers NT
                           WHERE NT.MinPoints > ISNULL(CL.YearlyPoints, 0) AND NT.IsActive = 1
                           ORDER BY NT.MinPoints ASC) AS NextTierName
                      FROM Customer_Loyalty CL
                      INNER JOIN Loyalty_Tiers LT ON LT.ID = CL.CurrentTier_ID
                      WHERE CL.Customer_MobilePhone = @CustomerPhone",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    var info = new CustomerLoyaltyInfo
                    {
                        CustomerPhone = customerPhone,
                        CurrentTierId = Convert.ToByte(row["CurrentTier_ID"]),
                        TierName = row["TierName"].ToString(),
                        TierNameEN = row.Table.Columns.Contains("TierNameEN") && row["TierNameEN"] != DBNull.Value ? row["TierNameEN"].ToString() : "",
                        TierColor = row["TierColor"].ToString(),
                        TotalPoints = Convert.ToInt32(row["TotalPoints"]),
                        AvailablePoints = Convert.ToInt32(row["AvailablePoints"]),
                        LifetimePoints = Convert.ToInt32(row["LifetimePoints"]),
                        PointsMultiplier = Convert.ToDecimal(row["PointsMultiplier"]),
                        DiscountPercent = Convert.ToDecimal(row["DiscountPercent"]),
                        MemberSince = Convert.ToDateTime(row["MemberSince"]),
                        TierMinPoints = row.Table.Columns.Contains("TierMinPoints") && row["TierMinPoints"] != DBNull.Value ? Convert.ToInt32(row["TierMinPoints"]) : 0
                    };

                    // Dual-pool fields (present after PHASE13 migration)
                    info.YearlyPoints = row.Table.Columns.Contains("YearlyPoints") && row["YearlyPoints"] != DBNull.Value
                        ? Convert.ToInt32(row["YearlyPoints"]) : info.LifetimePoints;
                    info.PointsYear = row.Table.Columns.Contains("PointsYear") && row["PointsYear"] != DBNull.Value
                        ? Convert.ToInt32(row["PointsYear"]) : DateTime.Now.Year;
                    info.NextTierMinPoints = row.Table.Columns.Contains("NextTierMinPoints") && row["NextTierMinPoints"] != DBNull.Value
                        ? (int?)Convert.ToInt32(row["NextTierMinPoints"]) : null;
                    info.NextTierName = row.Table.Columns.Contains("NextTierName") && row["NextTierName"] != DBNull.Value
                        ? row["NextTierName"].ToString() : null;

                    return info;
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting loyalty info: " + ex.Message);
            }
        }

        /// <summary>
        /// Get loyalty transaction history
        /// </summary>
        public DataTable GetTransactionHistory(string customerPhone, int limit = 50)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone },
                    { "@Limit", limit }
                };

                return _code.DatabaseQuerySafe(_connectionString,
                    code.AdaptSql($@"SELECT TOP {limit} *
                      FROM Loyalty_Transactions
                      WHERE Customer_MobilePhone = @CustomerPhone
                      ORDER BY TransactionDate DESC"),
                    parameters);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting transaction history: " + ex.Message);
            }
        }

        #endregion

        #region Tier Management

        /// <summary>
        /// Update customer's loyalty tier based on lifetime points
        /// </summary>
        public void UpdateTier(string customerPhone)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone }
                };

                _code.DatabaseQuerySafe(_connectionString,
                    "EXEC sp_UpdateLoyaltyTier @CustomerPhone",
                    parameters);
            }
            catch (Exception ex)
            {
                throw new Exception("Error updating tier: " + ex.Message);
            }
        }

        /// <summary>
        /// Get all loyalty tiers
        /// </summary>
        public DataTable GetLoyaltyTiers()
        {
            try
            {
                return _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM Loyalty_Tiers WHERE IsActive = 1 ORDER BY DisplayOrder",
                    null);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting tiers: " + ex.Message);
            }
        }

        /// <summary>
        /// Get loyalty program statistics
        /// </summary>
        public DataTable GetProgramStatistics()
        {
            try
            {
                return _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM vw_Loyalty_Program_Performance",
                    null);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting program statistics: " + ex.Message);
            }
        }

        #endregion

        #region Rewards Management

        /// <summary>
        /// Get available rewards catalog
        /// </summary>
        public DataTable GetAvailableRewards(byte? minTierId = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>();
                string query = "SELECT * FROM Loyalty_Rewards WHERE IsActive = 1";

                if (minTierId.HasValue)
                {
                    query += " AND MinTierRequired <= @MinTier";
                    parameters["@MinTier"] = minTierId.Value;
                }

                query += " ORDER BY DisplayOrder, PointsCost";

                return _code.DatabaseQuerySafe(_connectionString, query, parameters);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting rewards: " + ex.Message);
            }
        }

        /// <summary>
        /// Get reward details by ID
        /// </summary>
        public DataRow GetRewardById(int rewardId)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@RewardID", rewardId }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM Loyalty_Rewards WHERE ID = @RewardID",
                    parameters);

                return dt.Rows.Count > 0 ? dt.Rows[0] : null;
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting reward: " + ex.Message);
            }
        }

        /// <summary>
        /// Check if customer can redeem a reward
        /// </summary>
        public RewardEligibility CheckRewardEligibility(string customerPhone, int rewardId)
        {
            try
            {
                var loyaltyInfo = GetLoyaltyInfo(customerPhone);
                if (loyaltyInfo == null)
                {
                    return new RewardEligibility
                    {
                        IsEligible = false,
                        Message = "Customer not enrolled in loyalty program"
                    };
                }

                var reward = GetRewardById(rewardId);
                if (reward == null)
                {
                    return new RewardEligibility
                    {
                        IsEligible = false,
                        Message = "Reward not found"
                    };
                }

                int pointsCost = Convert.ToInt32(reward["PointsCost"]);
                byte minTier = Convert.ToByte(reward["MinTierRequired"]);

                if (loyaltyInfo.CurrentTierId < minTier)
                {
                    return new RewardEligibility
                    {
                        IsEligible = false,
                        Message = $"Tier too low. Requires: {reward["RewardName"]}"
                    };
                }

                if (loyaltyInfo.AvailablePoints < pointsCost)
                {
                    return new RewardEligibility
                    {
                        IsEligible = false,
                        Message = $"Insufficient points. Need {pointsCost - loyaltyInfo.AvailablePoints} more points",
                        PointsNeeded = pointsCost - loyaltyInfo.AvailablePoints
                    };
                }

                return new RewardEligibility
                {
                    IsEligible = true,
                    Message = "Eligible for redemption"
                };
            }
            catch (Exception ex)
            {
                return new RewardEligibility
                {
                    IsEligible = false,
                    Message = "Error checking eligibility: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Redeem a catalog reward for the customer (deducts redeemable points,
        /// decrements stock, creates a redemption voucher). Tier is unaffected.
        /// </summary>
        public RewardRedemptionResult RedeemReward(string customerPhone, int rewardId, short? adminId = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone },
                    { "@RewardID", rewardId },
                    { "@AdminID", adminId ?? (object)DBNull.Value }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "EXEC sp_RedeemReward @CustomerPhone, @RewardID, @AdminID", parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    string result = row["Result"].ToString();
                    return new RewardRedemptionResult
                    {
                        Success = result == "SUCCESS",
                        ResultCode = result,
                        Message = TranslateRedeemResult(result),
                        PointsRedeemed = row.Table.Columns.Contains("PointsRedeemed") && row["PointsRedeemed"] != DBNull.Value ? Convert.ToInt32(row["PointsRedeemed"]) : 0,
                        NewBalance = row.Table.Columns.Contains("NewBalance") && row["NewBalance"] != DBNull.Value ? Convert.ToInt32(row["NewBalance"]) : 0,
                        RedemptionCode = row.Table.Columns.Contains("RedemptionCode") && row["RedemptionCode"] != DBNull.Value ? row["RedemptionCode"].ToString() : null,
                        RewardName = row.Table.Columns.Contains("RewardName") && row["RewardName"] != DBNull.Value ? row["RewardName"].ToString() : null
                    };
                }

                return new RewardRedemptionResult { Success = false, Message = "ไม่สามารถแลกรางวัลได้" };
            }
            catch (Exception ex)
            {
                return new RewardRedemptionResult { Success = false, Message = "Error: " + ex.Message };
            }
        }

        private string TranslateRedeemResult(string code)
        {
            switch (code)
            {
                case "SUCCESS": return "แลกรางวัลสำเร็จ";
                case "REWARD_NOT_FOUND": return "ไม่พบรางวัลนี้";
                case "REWARD_INACTIVE": return "รางวัลนี้ปิดให้บริการชั่วคราว";
                case "OUT_OF_STOCK": return "รางวัลนี้หมดแล้ว";
                case "NOT_A_MEMBER": return "ยังไม่ได้เป็นสมาชิก";
                case "TIER_TOO_LOW": return "ระดับสมาชิกของคุณยังไม่ถึงเกณฑ์แลกรางวัลนี้";
                case "INSUFFICIENT_POINTS": return "คะแนนสะสมไม่เพียงพอ";
                default: return code;
            }
        }

        /// <summary>
        /// Manually adjust points (admin). Positive adds, negative deducts.
        /// </summary>
        public LoyaltyResult AdjustPoints(string customerPhone, int points, string description, short? adminId = null, bool affectTier = true)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone },
                    { "@Points", points },
                    { "@Description", description ?? (object)DBNull.Value },
                    { "@AdminID", adminId ?? (object)DBNull.Value },
                    { "@AffectTier", affectTier ? 1 : 0 }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "EXEC sp_AdjustLoyaltyPoints @CustomerPhone, @Points, @Description, @AdminID, @AffectTier", parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    string result = row["Result"].ToString();
                    return new LoyaltyResult
                    {
                        Success = result == "SUCCESS",
                        Message = result == "INSUFFICIENT_POINTS" ? "คะแนนคงเหลือไม่พอสำหรับการหักคะแนน" : result,
                        NewBalance = row["NewBalance"] != DBNull.Value ? Convert.ToInt32(row["NewBalance"]) : 0
                    };
                }

                return new LoyaltyResult { Success = false, Message = "Unknown error" };
            }
            catch (Exception ex)
            {
                return new LoyaltyResult { Success = false, Message = "Error: " + ex.Message };
            }
        }

        /// <summary>
        /// Has the customer already earned review points in the current calendar year?
        /// (Review reward is limited to once per year.)
        /// </summary>
        public bool HasReviewedThisYear(string customerPhone)
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS Cnt FROM Loyalty_Transactions
                      WHERE Customer_MobilePhone = @Phone
                        AND TransactionType = 'EARN'
                        AND Description LIKE '%Review%'
                        AND YEAR(TransactionDate) = YEAR(GETDATE())",
                    new Dictionary<string, object> { { "@Phone", customerPhone } });

                return dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["Cnt"]) > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Record the timestamp of a customer's most recent review (best-effort).
        /// </summary>
        public void MarkReviewed(string customerPhone)
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Customer_Loyalty SET LastReviewDate = GETDATE() WHERE Customer_MobilePhone = @Phone",
                    new Dictionary<string, object> { { "@Phone", customerPhone } });
            }
            catch { }
        }

        /// <summary>
        /// Search loyalty members for the admin management page.
        /// </summary>
        public DataTable GetMembers(string search = null, byte? tierId = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>();
                string query = "SELECT * FROM vw_Loyalty_Member_Summary WHERE 1=1";

                if (!string.IsNullOrEmpty(search))
                {
                    query += " AND (CustomerName LIKE @Search OR Customer_MobilePhone LIKE @Search)";
                    parameters["@Search"] = "%" + search + "%";
                }
                if (tierId.HasValue)
                {
                    query += " AND CurrentTier_ID = @TierId";
                    parameters["@TierId"] = tierId.Value;
                }

                query += " ORDER BY YearlyPoints DESC, AvailablePoints DESC";
                return _code.DatabaseQuerySafe(_connectionString, query, parameters);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting members: " + ex.Message);
            }
        }

        /// <summary>
        /// Get redemption history (all members, or one customer; optionally by status).
        /// </summary>
        public DataTable GetRedemptions(string customerPhone = null, string status = null, int limit = 200)
        {
            try
            {
                var parameters = new Dictionary<string, object>();
                string query = $@"SELECT TOP {limit} R.*, C.Name AS CustomerName
                                  FROM Loyalty_Redemptions R
                                  LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                                  WHERE 1=1";

                if (!string.IsNullOrEmpty(customerPhone))
                {
                    query += " AND R.Customer_MobilePhone = @Phone";
                    parameters["@Phone"] = customerPhone;
                }
                if (!string.IsNullOrEmpty(status))
                {
                    query += " AND R.Status = @Status";
                    parameters["@Status"] = status;
                }

                query += " ORDER BY R.RedeemedDate DESC";
                return _code.DatabaseQuerySafe(_connectionString, code.AdaptSql(query), parameters);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting redemptions: " + ex.Message);
            }
        }

        /// <summary>
        /// Mark a redemption voucher as fulfilled / cancelled (admin).
        /// </summary>
        public bool UpdateRedemptionStatus(long redemptionId, string status, short? adminId = null, string notes = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ID", redemptionId },
                    { "@Status", status },
                    { "@AdminID", adminId ?? (object)DBNull.Value },
                    { "@Notes", notes ?? (object)DBNull.Value }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Loyalty_Redemptions
                      SET Status = @Status,
                          FulfilledDate = CASE WHEN @Status = 'FULFILLED' THEN GETDATE() ELSE FulfilledDate END,
                          FulfilledBy_AdminID = @AdminID,
                          Notes = ISNULL(@Notes, Notes)
                      WHERE ID = @ID", parameters);

                // Refund points if cancelled
                if (status == "CANCELLED")
                {
                    DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                        "SELECT Customer_MobilePhone, PointsCost, RewardName FROM Loyalty_Redemptions WHERE ID = @ID",
                        new Dictionary<string, object> { { "@ID", redemptionId } });
                    if (dt.Rows.Count > 0)
                    {
                        string phone = dt.Rows[0]["Customer_MobilePhone"].ToString();
                        int cost = Convert.ToInt32(dt.Rows[0]["PointsCost"]);
                        AdjustPoints(phone, cost, $"คืนคะแนนจากการยกเลิกรางวัล: {dt.Rows[0]["RewardName"]}", adminId, false);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Initialize loyalty account for new customer
        /// </summary>
        public void InitializeLoyaltyAccount(string customerPhone)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@CustomerPhone", customerPhone }
                };

                // Check if already exists
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT COUNT(*) FROM Customer_Loyalty WHERE Customer_MobilePhone = @CustomerPhone",
                    parameters);

                if (dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) > 0)
                    return; // Already initialized

                // Create new loyalty account
                _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO Customer_Loyalty (Customer_MobilePhone, CurrentTier_ID, MemberSince)
                      VALUES (@CustomerPhone, 1, CAST(GETDATE() AS DATE))",
                    parameters);

                // Award welcome bonus (100 points)
                EarnPoints(customerPhone, 100, null, null, "Welcome bonus for joining loyalty program", 24);
            }
            catch (Exception ex)
            {
                throw new Exception("Error initializing loyalty account: " + ex.Message);
            }
        }

        /// <summary>
        /// Calculate discount percentage based on loyalty tier
        /// </summary>
        public decimal GetDiscountPercent(string customerPhone)
        {
            try
            {
                var loyaltyInfo = GetLoyaltyInfo(customerPhone);
                return loyaltyInfo?.DiscountPercent ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }

    #region Data Transfer Objects

    /// <summary>
    /// Result of loyalty operation
    /// </summary>
    public class LoyaltyResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int PointsEarned { get; set; }
        public int PointsRedeemed { get; set; }
        public int NewBalance { get; set; }
    }

    /// <summary>
    /// Result of a reward redemption
    /// </summary>
    public class RewardRedemptionResult
    {
        public bool Success { get; set; }
        public string ResultCode { get; set; }
        public string Message { get; set; }
        public int PointsRedeemed { get; set; }
        public int NewBalance { get; set; }
        public string RedemptionCode { get; set; }
        public string RewardName { get; set; }
    }

    /// <summary>
    /// Customer loyalty information (dual-pool model)
    /// </summary>
    public class CustomerLoyaltyInfo
    {
        public string CustomerPhone { get; set; }
        public byte CurrentTierId { get; set; }
        public string TierName { get; set; }
        public string TierNameEN { get; set; }
        public string TierColor { get; set; }
        public int TotalPoints { get; set; }
        /// <summary>Redeemable balance (spend on rewards)</summary>
        public int AvailablePoints { get; set; }
        public int LifetimePoints { get; set; }
        /// <summary>Tier-qualifying points earned this year (resets annually)</summary>
        public int YearlyPoints { get; set; }
        public int PointsYear { get; set; }
        public int TierMinPoints { get; set; }
        public int? NextTierMinPoints { get; set; }
        public string NextTierName { get; set; }
        public decimal PointsMultiplier { get; set; }
        public decimal DiscountPercent { get; set; }
        public DateTime MemberSince { get; set; }

        /// <summary>Points still needed to reach the next tier (0 if max)</summary>
        public int PointsToNextTier
        {
            get { return NextTierMinPoints.HasValue ? Math.Max(0, NextTierMinPoints.Value - YearlyPoints) : 0; }
        }

        /// <summary>Progress percent within the current tier band (0-100)</summary>
        public int TierProgressPercent
        {
            get
            {
                if (!NextTierMinPoints.HasValue) return 100;
                int band = NextTierMinPoints.Value - TierMinPoints;
                if (band <= 0) return 100;
                int into = YearlyPoints - TierMinPoints;
                int pct = (int)Math.Round(into * 100.0 / band);
                return Math.Max(0, Math.Min(100, pct));
            }
        }
    }

    /// <summary>
    /// Reward eligibility check result
    /// </summary>
    public class RewardEligibility
    {
        public bool IsEligible { get; set; }
        public string Message { get; set; }
        public int PointsNeeded { get; set; }
    }

    #endregion
}
