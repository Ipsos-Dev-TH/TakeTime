// AccountingService.cs
// Standard Accounting Document Management Service
// Handles Credit Notes, Debit Notes, and Financial Reports

using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;

namespace Take_Time_BangPhra.Services
{
    public class AccountingService
    {
        private readonly SqlConnection _conn;

        public AccountingService(SqlConnection connection)
        {
            _conn = connection;
        }

        #region Credit Note Management

        /// <summary>
        /// Create Credit Note (ใบลดหนี้)
        /// </summary>
        public CreditNoteResult CreateCreditNote(CreditNoteData creditNote, List<CreditNoteDetailData> details, short? createdById = null)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = _conn;
                    cmd.CommandType = CommandType.Text;

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    SqlTransaction transaction = _conn.BeginTransaction();
                    cmd.Transaction = transaction;

                    try
                    {
                        // Generate Credit Note Number
                        string creditNoteNumber;
                        using (SqlCommand cmdNumber = new SqlCommand("sp_GenerateCreditNoteNumber", _conn, transaction))
                        {
                            cmdNumber.CommandType = CommandType.StoredProcedure;
                            SqlParameter outParam = new SqlParameter("@CreditNoteNumber", SqlDbType.NVarChar, 20)
                            {
                                Direction = ParameterDirection.Output
                            };
                            cmdNumber.Parameters.Add(outParam);
                            cmdNumber.ExecuteNonQuery();
                            creditNoteNumber = outParam.Value.ToString();
                        }

                        // Insert Credit Note
                        cmd.CommandText = @"
                            INSERT INTO Credit_Note (
                                CreditNoteNumber, CreditNoteDate, OriginalReceipt_ID, Reservation_ID,
                                Customer_MobilePhone, Reason, ReasonType, TotalAmount, VatAmount, GrandTotal,
                                CreatedBy_ID, Status
                            )
                            VALUES (
                                @CreditNoteNumber, @CreditNoteDate, @OriginalReceipt_ID, @Reservation_ID,
                                @Customer_MobilePhone, @Reason, @ReasonType, @TotalAmount, @VatAmount, @GrandTotal,
                                @CreatedBy_ID, @Status
                            );
                            SELECT SCOPE_IDENTITY();";

                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@CreditNoteNumber", creditNoteNumber);
                        cmd.Parameters.AddWithValue("@CreditNoteDate", creditNote.CreditNoteDate);
                        cmd.Parameters.AddWithValue("@OriginalReceipt_ID", creditNote.OriginalReceiptID);
                        cmd.Parameters.AddWithValue("@Reservation_ID", creditNote.ReservationID ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Customer_MobilePhone", creditNote.CustomerMobilePhone);
                        cmd.Parameters.AddWithValue("@Reason", creditNote.Reason);
                        cmd.Parameters.AddWithValue("@ReasonType", creditNote.ReasonType);
                        cmd.Parameters.AddWithValue("@TotalAmount", creditNote.TotalAmount);
                        cmd.Parameters.AddWithValue("@VatAmount", creditNote.VatAmount);
                        cmd.Parameters.AddWithValue("@GrandTotal", creditNote.GrandTotal);
                        cmd.Parameters.AddWithValue("@CreatedBy_ID", createdById ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Status", creditNote.Status ?? "DRAFT");

                        long creditNoteId = Convert.ToInt64(cmd.ExecuteScalar());

                        // Insert Details
                        foreach (var detail in details)
                        {
                            cmd.CommandText = @"
                                INSERT INTO Credit_Note_Detail (
                                    CreditNote_ID, ProductType_ID, Product_ID, Description,
                                    Quantity, UnitPrice, Amount
                                )
                                VALUES (
                                    @CreditNote_ID, @ProductType_ID, @Product_ID, @Description,
                                    @Quantity, @UnitPrice, @Amount
                                )";

                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@CreditNote_ID", creditNoteId);
                            cmd.Parameters.AddWithValue("@ProductType_ID", detail.ProductTypeID ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Product_ID", detail.ProductID ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Description", detail.Description);
                            cmd.Parameters.AddWithValue("@Quantity", detail.Quantity);
                            cmd.Parameters.AddWithValue("@UnitPrice", detail.UnitPrice);
                            cmd.Parameters.AddWithValue("@Amount", detail.Amount);

                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();

                        return new CreditNoteResult
                        {
                            Success = true,
                            CreditNoteNumber = creditNoteNumber,
                            CreditNoteID = creditNoteId,
                            Message = "สร้างใบลดหนี้สำเร็จ"
                        };
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                return new CreditNoteResult
                {
                    Success = false,
                    Message = "เกิดข้อผิดพลาด: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Approve Credit Note
        /// </summary>
        public bool ApproveCreditNote(long creditNoteId, short? approvedById = null)
        {
            try
            {
                string query = @"
                    UPDATE Credit_Note
                    SET Status = 'APPROVED',
                        ApprovedBy_ID = @ApprovedBy_ID,
                        ApprovedDate = GETDATE()
                    WHERE ID = @CreditNoteID";

                using (SqlCommand cmd = new SqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@CreditNoteID", creditNoteId);
                    cmd.Parameters.AddWithValue("@ApprovedBy_ID", approvedById ?? (object)DBNull.Value);

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Debit Note Management

        /// <summary>
        /// Create Debit Note (ใบเพิ่มหนี้)
        /// </summary>
        public DebitNoteResult CreateDebitNote(DebitNoteData debitNote, List<DebitNoteDetailData> details, short? createdById = null)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = _conn;
                    cmd.CommandType = CommandType.Text;

                    if (_conn.State != ConnectionState.Open)
                        _conn.Open();

                    SqlTransaction transaction = _conn.BeginTransaction();
                    cmd.Transaction = transaction;

                    try
                    {
                        // Generate Debit Note Number
                        string debitNoteNumber;
                        using (SqlCommand cmdNumber = new SqlCommand("sp_GenerateDebitNoteNumber", _conn, transaction))
                        {
                            cmdNumber.CommandType = CommandType.StoredProcedure;
                            SqlParameter outParam = new SqlParameter("@DebitNoteNumber", SqlDbType.NVarChar, 20)
                            {
                                Direction = ParameterDirection.Output
                            };
                            cmdNumber.Parameters.Add(outParam);
                            cmdNumber.ExecuteNonQuery();
                            debitNoteNumber = outParam.Value.ToString();
                        }

                        // Insert Debit Note
                        cmd.CommandText = @"
                            INSERT INTO Debit_Note (
                                DebitNoteNumber, DebitNoteDate, OriginalReceipt_ID, Reservation_ID,
                                Customer_MobilePhone, Reason, ReasonType, TotalAmount, VatAmount, GrandTotal,
                                CreatedBy_ID, Status
                            )
                            VALUES (
                                @DebitNoteNumber, @DebitNoteDate, @OriginalReceipt_ID, @Reservation_ID,
                                @Customer_MobilePhone, @Reason, @ReasonType, @TotalAmount, @VatAmount, @GrandTotal,
                                @CreatedBy_ID, @Status
                            );
                            SELECT SCOPE_IDENTITY();";

                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@DebitNoteNumber", debitNoteNumber);
                        cmd.Parameters.AddWithValue("@DebitNoteDate", debitNote.DebitNoteDate);
                        cmd.Parameters.AddWithValue("@OriginalReceipt_ID", debitNote.OriginalReceiptID ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Reservation_ID", debitNote.ReservationID ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Customer_MobilePhone", debitNote.CustomerMobilePhone);
                        cmd.Parameters.AddWithValue("@Reason", debitNote.Reason);
                        cmd.Parameters.AddWithValue("@ReasonType", debitNote.ReasonType);
                        cmd.Parameters.AddWithValue("@TotalAmount", debitNote.TotalAmount);
                        cmd.Parameters.AddWithValue("@VatAmount", debitNote.VatAmount);
                        cmd.Parameters.AddWithValue("@GrandTotal", debitNote.GrandTotal);
                        cmd.Parameters.AddWithValue("@CreatedBy_ID", createdById ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Status", debitNote.Status ?? "DRAFT");

                        long debitNoteId = Convert.ToInt64(cmd.ExecuteScalar());

                        // Insert Details
                        foreach (var detail in details)
                        {
                            cmd.CommandText = @"
                                INSERT INTO Debit_Note_Detail (
                                    DebitNote_ID, ProductType_ID, Product_ID, Description,
                                    Quantity, UnitPrice, Amount
                                )
                                VALUES (
                                    @DebitNote_ID, @ProductType_ID, @Product_ID, @Description,
                                    @Quantity, @UnitPrice, @Amount
                                )";

                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@DebitNote_ID", debitNoteId);
                            cmd.Parameters.AddWithValue("@ProductType_ID", detail.ProductTypeID ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Product_ID", detail.ProductID ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Description", detail.Description);
                            cmd.Parameters.AddWithValue("@Quantity", detail.Quantity);
                            cmd.Parameters.AddWithValue("@UnitPrice", detail.UnitPrice);
                            cmd.Parameters.AddWithValue("@Amount", detail.Amount);

                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();

                        return new DebitNoteResult
                        {
                            Success = true,
                            DebitNoteNumber = debitNoteNumber,
                            DebitNoteID = debitNoteId,
                            Message = "สร้างใบเพิ่มหนี้สำเร็จ"
                        };
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                return new DebitNoteResult
                {
                    Success = false,
                    Message = "เกิดข้อผิดพลาด: " + ex.Message
                };
            }
        }

        #endregion

        #region Front Team Daily Summary

        /// <summary>
        /// Get Front Team Daily Summary
        /// Shows only transactions handled by Front team on specified dates
        /// </summary>
        public DataTable GetFrontTeamDailySummary(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand("sp_GetFrontTeamDailySummary", _conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
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
                        AR.Receipt_Number,
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

    #region Data Models

    // Credit Note Models
    public class CreditNoteData
    {
        public DateTime CreditNoteDate { get; set; } = DateTime.Now;
        public long OriginalReceiptID { get; set; }
        public long? ReservationID { get; set; }
        public string CustomerMobilePhone { get; set; }
        public string Reason { get; set; }
        public string ReasonType { get; set; } // REFUND, CANCELLATION, DISCOUNT, CORRECTION, OTHER
        public decimal TotalAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public string Status { get; set; } = "DRAFT";
        public string Notes { get; set; }
    }

    public class CreditNoteDetailData
    {
        public byte? ProductTypeID { get; set; }
        public int? ProductID { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
    }

    public class CreditNoteResult
    {
        public bool Success { get; set; }
        public string CreditNoteNumber { get; set; }
        public long CreditNoteID { get; set; }
        public string Message { get; set; }
    }

    // Debit Note Models
    public class DebitNoteData
    {
        public DateTime DebitNoteDate { get; set; } = DateTime.Now;
        public long? OriginalReceiptID { get; set; }
        public long? ReservationID { get; set; }
        public string CustomerMobilePhone { get; set; }
        public string Reason { get; set; }
        public string ReasonType { get; set; } // ADDITIONAL_CHARGE, PENALTY, DAMAGE, CORRECTION, OTHER
        public decimal TotalAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public string Status { get; set; } = "DRAFT";
        public string Notes { get; set; }
    }

    public class DebitNoteDetailData
    {
        public byte? ProductTypeID { get; set; }
        public int? ProductID { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
    }

    public class DebitNoteResult
    {
        public bool Success { get; set; }
        public string DebitNoteNumber { get; set; }
        public long DebitNoteID { get; set; }
        public string Message { get; set; }
    }

    #endregion
}
