// ReservationService.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Take_Time_BangPhra.Helpers;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Services
{
    public class ReservationService
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly TelegramService _telegramService;

        public ReservationService()
        {
            _dbHelper = new DatabaseHelper();
            _telegramService = new TelegramService();
        }

        public DataTable GetReservationsForDate(DateTime date)
        {
            string query = "Select * From Reservation inner join Customer on Customer.MobilePhone = Reservation.Customer_MobilePhone Where @Date >= CheckinDate AND @Date < CheckoutDate AND (Reservation.Status != N'ยกเลิกคืนเงิน' AND Reservation.Status != N'ยกเลิกไม่คืนเงิน')";
            var parameters = new Dictionary<string, object>
            {
                { "@Date", date.ToString("yyyy-MM-dd") }
            };
            return _dbHelper.ExecuteQueryWithParams(query, parameters);
        }

        public DataTable GetReservationAccommodations(DateTime date)
        {
            string query = "Select * From Reservation right join Reservation_Accommodation on Reservation.ID = Reservation_Accommodation.Reservation_ID inner join Accommodation on Accommodation.ID = Reservation_Accommodation.Accommodation_ID Where @Date >= CheckinDate AND @Date < CheckoutDate order by Accommodation.orderID asc";
            var parameters = new Dictionary<string, object>
            {
                { "@Date", date.ToString("yyyy-MM-dd") }
            };
            return _dbHelper.ExecuteQueryWithParams(query, parameters);
        }

        public DataTable GetReservationItems(DateTime date)
        {
            string query = "Select * From Reservation right join Reservation_Items on Reservation.ID = Reservation_Items.Reservation_ID inner join Items on Items.ID = Reservation_Items.Items_ID Where @Date >= CheckinDate AND @Date < CheckoutDate order by Items_ID asc";
            var parameters = new Dictionary<string, object>
            {
                { "@Date", date.ToString("yyyy-MM-dd") }
            };
            return _dbHelper.ExecuteQueryWithParams(query, parameters);
        }

        public DataTable GetReservationDetails(string reservationId)
        {
            string query = "SELECT * FROM [Reservation_Accommodation] inner join Reservation on Reservation.ID = Reservation_ID inner join Accommodation on Accommodation.ID=Accommodation_ID Where Reservation.ID = @ReservationId";
            var parameters = new Dictionary<string, object>
            {
                { "@ReservationId", reservationId }
            };
            return _dbHelper.ExecuteQueryWithParams(query, parameters);
        }

        public DataTable GetAvailableAccommodations(DateTime date)
        {
            try
            {
                string query = @"
            SELECT a.*
            FROM Accommodation a
            WHERE a.Status = 1
            AND a.ID NOT IN (
                SELECT ra.Accommodation_ID
                FROM Reservation_Accommodation ra
                INNER JOIN Reservation r ON r.ID = ra.Reservation_ID
                WHERE @Date >= r.CheckinDate
                AND @Date < r.CheckoutDate
                AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เช็คเอ้าท์แล้ว')
                AND a.LimitWithPeople = 'False'
            )
            ORDER BY a.OrderID ASC";

                var parameters = new Dictionary<string, object>
                {
                    { "@Date", date.ToString("yyyy-MM-dd") }
                };
                return _dbHelper.ExecuteQueryWithParams(query, parameters);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error in GetAvailableAccommodations: {ex.Message}");
                // Fallback to basic query
                return _dbHelper.ExecuteQuery("SELECT * FROM Accommodation WHERE Status = 1 ORDER BY OrderID ASC");
            }
        }

        // ADD THIS METHOD - Get available items for specific date
        public DataTable GetAvailableItems(DateTime date)
        {
            try
            {
                string query = @"
            SELECT i.*,
                   (i.Amount - ISNULL((
                       SELECT SUM(ri.Amount)
                       FROM Reservation_Items ri
                       INNER JOIN Reservation r ON r.ID = ri.Reservation_ID
                       WHERE ri.Items_ID = i.ID
                       AND @Date >= r.CheckinDate
                       AND @Date < r.CheckoutDate
                       AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เช็คเอ้าท์แล้ว')
                   ), 0)) as AvailableAmount
            FROM Items i
            WHERE i.Status = 1
            ORDER BY i.OrderID ASC";

                var parameters = new Dictionary<string, object>
                {
                    { "@Date", date.ToString("yyyy-MM-dd") }
                };
                return _dbHelper.ExecuteQueryWithParams(query, parameters);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error in GetAvailableItems: {ex.Message}");
                // Fallback to basic query
                return _dbHelper.ExecuteQuery("SELECT * FROM Items WHERE Status = 1 ORDER BY OrderID ASC");
            }
        }

        // ADD THIS METHOD - Get available accommodations with detailed availability
        // ใน ReservationService class
        public DataTable GetAvailableAccommodationsForDate(DateTime date, string excludeReservationId = "0")
        {
            try
            {
                string query = @"
            SELECT
                a.ID,
                a.AccomName,
                a.Price,
                a.People,
                a.Unit,
                a.LimitWithPeople,
                a.Status,
                a.Remark
            FROM Accommodation a
            WHERE a.Status = 1
            AND (
                a.LimitWithPeople = 'True'
                OR a.ID NOT IN (
                    SELECT ra.Accommodation_ID
                    FROM Reservation_Accommodation ra
                    INNER JOIN Reservation r ON r.ID = ra.Reservation_ID
                    WHERE @Date >= r.CheckinDate
                    AND @Date < r.CheckoutDate
                    AND r.Status NOT IN ('ยกเลิกแล้ว', 'Cancelled')
                    AND r.ID != @ExcludeReservationId
                )
            )
            ORDER BY a.AccomName";

                var parameters = new Dictionary<string, object>
                {
                    { "@Date", date.ToString("yyyy-MM-dd") },
                    { "@ExcludeReservationId", excludeReservationId }
                };
                return _dbHelper.ExecuteQueryWithParams(query, parameters);
            }
            catch (Exception ex)
            {
                // Fallback to all accommodations
                System.Diagnostics.Trace.TraceError($"GetAvailableAccommodationsForDate error: {ex.Message}");
                return GetAllAccommodations();
            }
        }
        public DataTable GetAllAccommodations()
        {
            try
            {
                string query = @"
            SELECT 
                a.ID,
                a.AccomName,
                a.Price,
                a.People,
                a.Unit,
                a.LimitWithPeople,
                a.Status,
                a.Remark
            FROM Accommodation a
            WHERE a.Status = 1
            ORDER BY a.AccomName";

                return _dbHelper.ExecuteQuery(query);
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Trace.TraceError($"GetAllAccommodations error: {ex.Message}");
                return new DataTable();
            }
        }

        public DataTable GetCustomerReservationHistory(string phoneNumber)
        {
            string query = "SELECT count([Customer_MobilePhone]) as CountReserved FROM [Reservation] Where Customer_MobilePhone = @PhoneNumber AND Status = N'เช็คอินแล้ว'";
            var parameters = new Dictionary<string, object>
            {
                { "@PhoneNumber", phoneNumber }
            };
            return _dbHelper.ExecuteQueryWithParams(query, parameters);
        }

        public async Task<bool> CancelReservationWithRefund(string reservationId)
        {
            try
            {
                // จับ deposit + customer name + payment method ก่อนล้าง — ใช้ส่งเข้า accounting sync
                var (depositAmt, customerName, paymentMethod) = LookupDepositForCancel(reservationId);

                // 🔧 FIX: Cancel Payment_History records first
                try
                {
                    string updatePaymentQuery = @"
                        UPDATE [dbo].[Payment_History]
                        SET Status = 'CANCELLED',
                            Notes = N'ยกเลิกจากการยกเลิกการจอง (คืนเงิน) ID: ' + @ReservationId
                        WHERE Reservation_ID = @ReservationId
                        AND Status = 'COMPLETED'";

                    var paymentParams = new Dictionary<string, object>
                    {
                        { "@ReservationId", reservationId }
                    };
                    _dbHelper.ExecuteNonQueryWithParams(updatePaymentQuery, paymentParams);

                    System.Diagnostics.Trace.TraceInformation($"✅ Cancelled Payment_History for Reservation {reservationId}");
                }
                catch (Exception phEx)
                {
                    System.Diagnostics.Trace.TraceWarning($"⚠️ Failed to cancel Payment_History: {phEx.Message}");
                    // Continue - this is non-critical
                }

                // Update reservation status
                var parameters = new Dictionary<string, object>
                {
                    { "@ReservationId", reservationId }
                };

                _dbHelper.ExecuteNonQueryWithParams("UPDATE [dbo].[Reservation] SET TotalPrice = 0, Deposit = 0 , [Status] = N'ยกเลิกคืนเงิน' WHERE ID = @ReservationId", parameters);
                _dbHelper.ExecuteNonQueryWithParams("DELETE FROM [dbo].[Reservation_Accommodation] WHERE Reservation_ID = @ReservationId", parameters);
                _dbHelper.ExecuteNonQueryWithParams("DELETE FROM [dbo].[Reservation_Items] WHERE Reservation_ID = @ReservationId", parameters);

                // ตัดเจ้าหนี้ ADVANCE_DEPOSIT → CR Cash/Bank (คืนเงินสด)
                TryEnqueueDepositRefund(reservationId, depositAmt, customerName, paymentMethod);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error canceling reservation with refund {reservationId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CancelReservationWithoutRefund(string reservationId)
        {
            try
            {
                var (depositAmt, customerName, _) = LookupDepositForCancel(reservationId);

                // 🔧 FIX: Cancel Payment_History records first
                try
                {
                    string updatePaymentQuery = @"
                        UPDATE [dbo].[Payment_History]
                        SET Status = 'CANCELLED',
                            Notes = N'ยกเลิกจากการยกเลิกการจอง (ไม่คืนเงิน) ID: ' + @ReservationId
                        WHERE Reservation_ID = @ReservationId
                        AND Status = 'COMPLETED'";

                    var paymentParams = new Dictionary<string, object>
                    {
                        { "@ReservationId", reservationId }
                    };
                    _dbHelper.ExecuteNonQueryWithParams(updatePaymentQuery, paymentParams);

                    System.Diagnostics.Trace.TraceInformation($"✅ Cancelled Payment_History for Reservation {reservationId}");
                }
                catch (Exception phEx)
                {
                    System.Diagnostics.Trace.TraceWarning($"⚠️ Failed to cancel Payment_History: {phEx.Message}");
                    // Continue - this is non-critical
                }

                // Update reservation status (Deposit is NOT reset to 0 - customer doesn't get refund)
                var parameters = new Dictionary<string, object>
                {
                    { "@ReservationId", reservationId }
                };

                _dbHelper.ExecuteNonQueryWithParams("UPDATE [dbo].[Reservation] SET TotalPrice = 0, [Status] = N'ยกเลิกไม่คืนเงิน' WHERE ID = @ReservationId", parameters);
                _dbHelper.ExecuteNonQueryWithParams("DELETE FROM [dbo].[Reservation_Accommodation] WHERE Reservation_ID = @ReservationId", parameters);
                _dbHelper.ExecuteNonQueryWithParams("DELETE FROM [dbo].[Reservation_Items] WHERE Reservation_ID = @ReservationId", parameters);

                // ริบมัดจำ → DR ADVANCE_DEPOSIT, CR Forfeit Income
                TryEnqueueDepositForfeit(reservationId, depositAmt, customerName, "ยกเลิกไม่คืนเงิน");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error canceling reservation without refund {reservationId}: {ex.Message}");
                return false;
            }
        }

        // ──────────────────────────────────────────────
        // Helpers สำหรับตัดมัดจำตอนยกเลิกการจอง
        // ──────────────────────────────────────────────

        private (decimal deposit, string customerName, string paymentMethod) LookupDepositForCancel(string reservationId)
        {
            try
            {
                if (!int.TryParse(reservationId, out int resId)) return (0, "", "CASH");

                string sql = @"SELECT R.Deposit, R.Customer_MobilePhone,
                                      ISNULL(C.FullName, C.Name) AS Name,
                                      (SELECT TOP 1 Paid_Type FROM Account_Receipt
                                       WHERE Reservation_ID = R.ID AND IsDeposit = 1
                                       ORDER BY Created_Date DESC) AS PaidType
                               FROM Reservation R
                               LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                               WHERE R.ID = @id";
                var dt = _dbHelper.ExecuteQueryWithParams(sql, new Dictionary<string, object> { { "@id", resId } });
                if (dt != null && dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    decimal dep = row["Deposit"] != DBNull.Value ? Convert.ToDecimal(row["Deposit"]) : 0m;
                    string name = row["Name"]?.ToString() ?? "";
                    string method = row["PaidType"]?.ToString() ?? "CASH";
                    return (dep, name, method);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"LookupDepositForCancel failed: {ex.Message}");
            }
            return (0, "", "CASH");
        }

        private void TryEnqueueDepositRefund(string reservationId, decimal depositAmt, string customerName, string paymentMethod)
        {
            if (depositAmt <= 0) return;
            try
            {
                if (!int.TryParse(reservationId, out int resId)) return;
                var sync = new AccountingSyncService(_dbHelper.ConnectionString);
                sync.EnqueueDepositRefund(resId, depositAmt, paymentMethod, customerName, DateTime.Now);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"TryEnqueueDepositRefund failed for {reservationId}: {ex.Message}");
            }
        }

        private void TryEnqueueDepositForfeit(string reservationId, decimal depositAmt, string customerName, string reason)
        {
            if (depositAmt <= 0) return;
            try
            {
                if (!int.TryParse(reservationId, out int resId)) return;
                var sync = new AccountingSyncService(_dbHelper.ConnectionString);
                sync.EnqueueDepositForfeit(resId, depositAmt, customerName, DateTime.Now, reason);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"TryEnqueueDepositForfeit failed for {reservationId}: {ex.Message}");
            }
        }

        public double CalculateTwoDecimalPoints(double num)
        {
            return Math.Round(num, 2);
        }
    }
}