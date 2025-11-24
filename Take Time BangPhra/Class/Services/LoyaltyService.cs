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
        /// Get customer's loyalty balance
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
                    @"SELECT CL.*, LT.TierName, LT.TierNameEN, LT.TierColor, LT.PointsMultiplier, LT.DiscountPercent
                      FROM Customer_Loyalty CL
                      INNER JOIN Loyalty_Tiers LT ON LT.ID = CL.CurrentTier_ID
                      WHERE CL.Customer_MobilePhone = @CustomerPhone",
                    parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    return new CustomerLoyaltyInfo
                    {
                        CustomerPhone = customerPhone,
                        CurrentTierId = Convert.ToByte(row["CurrentTier_ID"]),
                        TierName = row["TierName"].ToString(),
                        TierColor = row["TierColor"].ToString(),
                        TotalPoints = Convert.ToInt32(row["TotalPoints"]),
                        AvailablePoints = Convert.ToInt32(row["AvailablePoints"]),
                        LifetimePoints = Convert.ToInt32(row["LifetimePoints"]),
                        PointsMultiplier = Convert.ToDecimal(row["PointsMultiplier"]),
                        DiscountPercent = Convert.ToDecimal(row["DiscountPercent"]),
                        MemberSince = Convert.ToDateTime(row["MemberSince"])
                    };
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
    /// Customer loyalty information
    /// </summary>
    public class CustomerLoyaltyInfo
    {
        public string CustomerPhone { get; set; }
        public byte CurrentTierId { get; set; }
        public string TierName { get; set; }
        public string TierColor { get; set; }
        public int TotalPoints { get; set; }
        public int AvailablePoints { get; set; }
        public int LifetimePoints { get; set; }
        public decimal PointsMultiplier { get; set; }
        public decimal DiscountPercent { get; set; }
        public DateTime MemberSince { get; set; }
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
