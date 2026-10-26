// AccountingService.cs
// Standard Accounting Document Management Service
// Front team daily summary + revenue summaries (ใบลดหนี้อยู่ที่ Class/Accounting/CreditNoteService.cs)

using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Services
{
    public class AccountingService
    {
        private readonly SqlConnection _conn;

        public AccountingService(SqlConnection connection)
        {
            _conn = connection;
        }

        // ใบลดหนี้/ใบเพิ่มหนี้แบบเก่า (CreateCreditNote/ApproveCreditNote/CreateDebitNote + โมเดล CreditNoteData ฯลฯ) ถูกลบ ต.ค. 2026:
        // ไม่มีผู้เรียกเลย และเขียนตาราง Credit_Note ด้วย schema เก่า (CreditNoteNumber/Credit_Note_Detail) ที่ชนกับ PHASE19_33.
        // ใบลดหนี้ปัจจุบัน = Take_Time_BangPhra.Integration.CreditNoteService (Class/Accounting/CreditNoteService.cs).

        #region Front Team Daily Summary

        /// <summary>
        /// Get Front Team Daily Summary
        /// Shows only transactions handled by Front team on specified dates
        /// </summary>
        public DataTable GetFrontTeamDailySummary(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = _conn;
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = @"
                        SELECT
                            CAST(R.CheckinDate AS DATE) AS SummaryDate,
                            COUNT(DISTINCT R.ID) AS TotalReservations,
                            ISNULL(SUM(R.TotalPrice), 0) AS TotalRevenue,
                            COUNT(DISTINCT CASE WHEN R.Status = 'CHECKIN' THEN R.ID END) AS CheckedInCount,
                            COUNT(DISTINCT CASE WHEN R.Status = 'CHECKOUT' THEN R.ID END) AS CheckedOutCount,
                            ISNULL(SUM(R.Deposit), 0) AS TotalDeposits
                        FROM Reservation R
                        WHERE R.CheckinDate >= @StartDate
                          AND R.CheckinDate <= @EndDate
                        GROUP BY CAST(R.CheckinDate AS DATE)
                        ORDER BY SummaryDate DESC";
                    cmd.Parameters.AddWithValue("@StartDate", startDate ?? DateTime.Today);
                    cmd.Parameters.AddWithValue("@EndDate", endDate ?? DateTime.Today);

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("ไม่สามารถดึงสรุปยอดรายวันได้: " + ex.Message);
            }
        }

        /// <summary>
        /// Get detailed transactions for Front team by date
        /// </summary>
        public DataTable GetFrontTeamTransactions(DateTime transactionDate)
        {
            try
            {
                string query = @"
                    SELECT
                        AR.TransactionDate,
                        AR.Created_Date AS TransactionTime,
                        AR.ID AS Receipt_Number,
                        R.ID AS ReservationID,
                        C.Name AS CustomerName,
                        C.MobilePhone AS CustomerPhone,
                        APT.ProductTypeName AS Category,
                        AR.RevenueCategory,
                        PT.PaymentTypeName AS PaymentChannel,
                        AR.Total_Amount,
                        AR.IsCheckIn,
                        R.CheckinDate,
                        R.CheckoutDate,
                        E.Name AS CreatedBy
                    FROM Account_Receipt AR
                    LEFT JOIN Reservation R ON AR.Reservation_ID = R.ID
                    LEFT JOIN Customer C ON AR.Customer_Phone = C.MobilePhone
                    LEFT JOIN Account_ProductType APT ON AR.ProductType_ID = APT.ID
                    LEFT JOIN PaymentType PT ON AR.Type_ID = PT.ID
                    LEFT JOIN Employees E ON AR.Created_By = E.ID
                    WHERE AR.TransactionDate = @TransactionDate
                      AND AR.IsFrontTransaction = 1
                      AND AR.Status = 1
                    ORDER BY AR.Created_Date DESC";

                using (SqlCommand cmd = new SqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@TransactionDate", transactionDate);

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("ไม่สามารถดึงรายการธุรกรรมได้: " + ex.Message);
            }
        }

        /// <summary>
        /// Get revenue summary by category
        /// </summary>
        public Dictionary<string, decimal> GetRevenueSummaryByCategory(DateTime startDate, DateTime endDate)
        {
            try
            {
                string query = @"
                    SELECT
                        COALESCE(AR.RevenueCategory, 'OTHER') AS Category,
                        SUM(AR.Total_Amount) AS TotalRevenue
                    FROM Account_Receipt AR
                    WHERE AR.TransactionDate BETWEEN @StartDate AND @EndDate
                      AND AR.IsFrontTransaction = 1
                      AND AR.Status = 1
                    GROUP BY AR.RevenueCategory";

                Dictionary<string, decimal> summary = new Dictionary<string, decimal>();

                using (SqlCommand cmd = new SqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string category = reader["Category"].ToString();
                            decimal revenue = Convert.ToDecimal(reader["TotalRevenue"]);
                            summary[category] = revenue;
                        }
                    }
                }

                return summary;
            }
            catch (Exception ex)
            {
                throw new Exception("ไม่สามารถดึงสรุปรายได้ตามหมวดหมู่ได้: " + ex.Message);
            }
        }

        /// <summary>
        /// Get revenue summary by payment channel
        /// </summary>
        public Dictionary<string, decimal> GetRevenueSummaryByPaymentChannel(DateTime startDate, DateTime endDate)
        {
            try
            {
                string query = @"
                    SELECT
                        PT.PaymentTypeName AS PaymentChannel,
                        SUM(AR.Total_Amount) AS TotalRevenue
                    FROM Account_Receipt AR
                    LEFT JOIN PaymentType PT ON AR.Type_ID = PT.ID
                    WHERE AR.TransactionDate BETWEEN @StartDate AND @EndDate
                      AND AR.IsFrontTransaction = 1
                      AND AR.Status = 1
                    GROUP BY PT.PaymentTypeName";

                Dictionary<string, decimal> summary = new Dictionary<string, decimal>();

                using (SqlCommand cmd = new SqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string channel = reader["PaymentChannel"]?.ToString() ?? "ไม่ระบุ";
                            decimal revenue = Convert.ToDecimal(reader["TotalRevenue"]);
                            summary[channel] = revenue;
                        }
                    }
                }

                return summary;
            }
            catch (Exception ex)
            {
                throw new Exception("ไม่สามารถดึงสรุปรายได้ตามช่องทางชำระเงินได้: " + ex.Message);
            }
        }

        #endregion
    }
}
