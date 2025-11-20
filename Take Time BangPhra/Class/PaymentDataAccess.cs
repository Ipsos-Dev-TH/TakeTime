using System;
using System.Collections.Generic;
using System.Data;
using System.Configuration;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// 🔒 SECURE Data Access Layer for Payment operations
    /// All methods use parameterized queries to prevent SQL Injection
    /// </summary>
    public class PaymentDataAccess
    {
        private readonly code _code;
        private readonly string _connectionString;

        public PaymentDataAccess(string connectionString)
        {
            _code = new code();
            _connectionString = connectionString;
        }

        #region Payment History Queries

        /// <summary>
        /// Get all payment history for a reservation
        /// </summary>
        public DataTable GetPaymentHistory(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ph.*,
                         ar.ID as ReceiptNumber,
                         a.Username as ProcessedBy,
                         ps.SlipFileURL,
                         ps.FileName as SlipFileName
                  FROM Payment_History ph
                  LEFT JOIN Account_Receipt ar ON ph.Receipt_ID = ar.ID
                  LEFT JOIN Admin a ON ph.ProcessedBy_AdminID = a.ID
                  LEFT JOIN Payment_Slips ps ON ph.PaymentSlip_ID = ps.ID
                  WHERE ph.Reservation_ID = @reservationId
                  ORDER BY ph.PaymentDate DESC",
                parameters);
        }

        /// <summary>
        /// Get total paid amount for a reservation
        /// </summary>
        public decimal GetTotalPaidAmount(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            var result = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ISNULL(SUM(PaymentAmount), 0) as TotalPaid
                  FROM Payment_History
                  WHERE Reservation_ID = @reservationId
                  AND Status = 'COMPLETED'",
                parameters);

            if (result.Rows.Count > 0)
            {
                return Convert.ToDecimal(result.Rows[0]["TotalPaid"]);
            }

            return 0;
        }

        /// <summary>
        /// Get remaining balance for a reservation
        /// </summary>
        public decimal GetRemainingBalance(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            var result = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    r.TotalPrice,
                    ISNULL(SUM(ph.PaymentAmount), 0) as TotalPaid
                  FROM Reservation r
                  LEFT JOIN Payment_History ph ON r.ID = ph.Reservation_ID AND ph.Status = 'COMPLETED'
                  WHERE r.ID = @reservationId
                  GROUP BY r.TotalPrice",
                parameters);

            if (result.Rows.Count > 0)
            {
                decimal totalPrice = Convert.ToDecimal(result.Rows[0]["TotalPrice"]);
                decimal totalPaid = Convert.ToDecimal(result.Rows[0]["TotalPaid"]);
                return totalPrice - totalPaid;
            }

            return 0;
        }

        /// <summary>
        /// Get payment summary view
        /// </summary>
        public DataTable GetPaymentSummary(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM vw_ReservationPaymentSummary WHERE ReservationID = @reservationId",
                parameters);
        }

        #endregion

        #region Payment Recording

        /// <summary>
        /// Record a new payment using stored procedure
        /// </summary>
        public long RecordPayment(
            int reservationId,
            decimal amount,
            string paymentType,
            string paymentMethod = null,
            string receiptId = null,
            long? paymentSlipId = null,
            string paidByCustomerPhone = null,
            int? processedByAdminId = null,
            string notes = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@ReservationID", reservationId },
                { "@PaymentAmount", amount },
                { "@PaymentType", paymentType },
                { "@PaymentMethod", paymentMethod },
                { "@ReceiptID", receiptId },
                { "@PaymentSlipID", paymentSlipId },
                { "@PaidByCustomerPhone", paidByCustomerPhone },
                { "@ProcessedByAdminID", processedByAdminId },
                { "@Notes", notes },
                { "@NewPaymentID", 0 } // OUTPUT parameter
            };

            // Execute stored procedure
            var result = _code.DatabaseInsertReturnSafe(_connectionString,
                @"EXEC sp_RecordPayment
                    @ReservationID, @PaymentAmount, @PaymentType, @PaymentMethod,
                    @ReceiptID, @PaymentSlipID, @PaidByCustomerPhone,
                    @ProcessedByAdminID, @Notes, @NewPaymentID OUTPUT",
                parameters);

            return result;
        }

        /// <summary>
        /// Record payment (direct INSERT - alternative to stored procedure)
        /// </summary>
        public long RecordPaymentDirect(
            int reservationId,
            decimal amount,
            string paymentType,
            string paymentMethod,
            string receiptId,
            long? paymentSlipId,
            string paidByCustomerPhone,
            int? processedByAdminId,
            string notes)
        {
            // Calculate remaining balance
            decimal remaining = GetRemainingBalance(reservationId) - amount;

            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@amount", amount },
                { "@paymentType", paymentType },
                { "@paymentMethod", paymentMethod },
                { "@receiptId", receiptId },
                { "@paymentSlipId", paymentSlipId },
                { "@remaining", remaining },
                { "@paidBy", paidByCustomerPhone },
                { "@processedBy", processedByAdminId },
                { "@notes", notes }
            };

            return _code.DatabaseInsertReturnSafe(_connectionString,
                @"INSERT INTO Payment_History (
                    Reservation_ID, PaymentDate, PaymentAmount, PaymentType, PaymentMethod,
                    Receipt_ID, PaymentSlip_ID, RemainingBalance, PaidBy_CustomerPhone,
                    ProcessedBy_AdminID, Notes, Status
                  )
                  VALUES (
                    @reservationId, GETDATE(), @amount, @paymentType, @paymentMethod,
                    @receiptId, @paymentSlipId, @remaining, @paidBy,
                    @processedBy, @notes, 'COMPLETED'
                  );
                  SELECT SCOPE_IDENTITY();",
                parameters);
        }

        /// <summary>
        /// Update payment status
        /// </summary>
        public void UpdatePaymentStatus(long paymentId, string status, string notes = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@paymentId", paymentId },
                { "@status", status },
                { "@notes", notes },
                { "@updateDate", DateTime.Now }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Payment_History
                  SET Status = @status,
                      Notes = ISNULL(@notes, Notes),
                      UpdatedDate = @updateDate
                  WHERE ID = @paymentId",
                parameters);
        }

        #endregion

        #region Payment Validation

        /// <summary>
        /// Check if reservation is fully paid
        /// </summary>
        public bool IsFullyPaid(int reservationId)
        {
            return GetRemainingBalance(reservationId) <= 0.01m; // Allow for rounding
        }

        /// <summary>
        /// Check if reservation has any payments
        /// </summary>
        public bool HasPayments(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            var result = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT COUNT(*) as PaymentCount
                  FROM Payment_History
                  WHERE Reservation_ID = @reservationId
                  AND Status = 'COMPLETED'",
                parameters);

            return result.Rows.Count > 0 && Convert.ToInt32(result.Rows[0]["PaymentCount"]) > 0;
        }

        /// <summary>
        /// Get payment status (PAID/PARTIAL/UNPAID)
        /// </summary>
        public string GetPaymentStatus(int reservationId)
        {
            var summary = GetPaymentSummary(reservationId);
            if (summary.Rows.Count > 0)
            {
                return summary.Rows[0]["PaymentStatus"].ToString();
            }

            return "UNPAID";
        }

        #endregion

        #region Payment Slips

        /// <summary>
        /// Get payment slip by ID
        /// </summary>
        public DataTable GetPaymentSlip(long slipId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@slipId", slipId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM Payment_Slips WHERE ID = @slipId",
                parameters);
        }

        /// <summary>
        /// Get all payment slips for a reservation
        /// </summary>
        public DataTable GetPaymentSlips(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT * FROM Payment_Slips
                  WHERE Reservation_ID = @reservationId
                  ORDER BY UploadedDate DESC",
                parameters);
        }

        /// <summary>
        /// Update payment slip verification status
        /// </summary>
        public void UpdateSlipVerification(
            long slipId,
            string status,
            int? verifiedById = null,
            string rejectionReason = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@slipId", slipId },
                { "@status", status },
                { "@verifiedBy", verifiedById },
                { "@verifiedDate", DateTime.Now },
                { "@rejectionReason", rejectionReason }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Payment_Slips
                  SET VerificationStatus = @status,
                      VerifiedBy_ID = @verifiedBy,
                      VerifiedDate = @verifiedDate,
                      RejectionReason = @rejectionReason
                  WHERE ID = @slipId",
                parameters);
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Get daily payment summary
        /// </summary>
        public DataTable GetDailyPaymentSummary(DateTime date)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@date", date.Date }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    PaymentType,
                    PaymentMethod,
                    COUNT(*) as TransactionCount,
                    SUM(PaymentAmount) as TotalAmount
                  FROM Payment_History
                  WHERE CAST(PaymentDate AS DATE) = @date
                  AND Status = 'COMPLETED'
                  GROUP BY PaymentType, PaymentMethod
                  ORDER BY PaymentType, PaymentMethod",
                parameters);
        }

        /// <summary>
        /// Get payment summary for date range
        /// </summary>
        public DataTable GetPaymentSummaryByDateRange(DateTime startDate, DateTime endDate)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@startDate", startDate.Date },
                { "@endDate", endDate.Date.AddDays(1).AddSeconds(-1) }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    CAST(PaymentDate AS DATE) as PaymentDate,
                    PaymentType,
                    COUNT(*) as TransactionCount,
                    SUM(PaymentAmount) as TotalAmount
                  FROM Payment_History
                  WHERE PaymentDate BETWEEN @startDate AND @endDate
                  AND Status = 'COMPLETED'
                  GROUP BY CAST(PaymentDate AS DATE), PaymentType
                  ORDER BY PaymentDate DESC, PaymentType",
                parameters);
        }

        #endregion
    }
}
