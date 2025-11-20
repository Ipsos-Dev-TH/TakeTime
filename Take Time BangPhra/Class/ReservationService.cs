// ReservationService.cs
using System;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Take_Time_BangPhra.Helpers;

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
            string query = $"Select * From Reservation inner join Customer on Customer.MobilePhone = Reservation.Customer_MobilePhone Where '{date:yyyy-MM-dd}' >= CheckinDate AND '{date:yyyy-MM-dd}' < CheckoutDate AND (Reservation.Status != N'ยกเลิกคืนเงิน' AND Reservation.Status != N'ยกเลิกไม่คืนเงิน')";
            return _dbHelper.ExecuteQuery(query);
        }

        public DataTable GetReservationAccommodations(DateTime date)
        {
            string query = $"Select * From Reservation right join Reservation_Accommodation on Reservation.ID = Reservation_Accommodation.Reservation_ID inner join Accommodation on Accommodation.ID = Reservation_Accommodation.Accommodation_ID Where '{date:yyyy-MM-dd}' >= CheckinDate AND '{date:yyyy-MM-dd}' < CheckoutDate order by Accommodation.orderID asc";
            return _dbHelper.ExecuteQuery(query);
        }

        public DataTable GetReservationItems(DateTime date)
        {
            string query = $"Select * From Reservation right join Reservation_Items on Reservation.ID = Reservation_Items.Reservation_ID inner join Items on Items.ID = Reservation_Items.Items_ID Where '{date:yyyy-MM-dd}' >= CheckinDate AND '{date:yyyy-MM-dd}' < CheckoutDate order by Items_ID asc";
            return _dbHelper.ExecuteQuery(query);
        }

        public DataTable GetReservationDetails(string reservationId)
        {
            string query = $"SELECT * FROM [Reservation_Accommodation] inner join Reservation on Reservation.ID = Reservation_ID inner join Accommodation on Accommodation.ID=Accommodation_ID Where Reservation.ID = {reservationId}";
            return _dbHelper.ExecuteQuery(query);
        }

        public DataTable GetAvailableAccommodations(DateTime date)
        {
            try
            {
                string query = $@"
            SELECT a.* 
            FROM Accommodation a
            WHERE a.Status = 1 
            AND a.ID NOT IN (
                SELECT ra.Accommodation_ID 
                FROM Reservation_Accommodation ra
                INNER JOIN Reservation r ON r.ID = ra.Reservation_ID
                WHERE '{date:yyyy-MM-dd}' >= r.CheckinDate 
                AND '{date:yyyy-MM-dd}' < r.CheckoutDate 
                AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เช็คเอ้าท์แล้ว')
                AND a.LimitWithPeople = 'False'
            )
            ORDER BY a.OrderID ASC";

                return _dbHelper.ExecuteQuery(query);
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
                string query = $@"
            SELECT i.*,
                   (i.Amount - ISNULL((
                       SELECT SUM(ri.Amount) 
                       FROM Reservation_Items ri
                       INNER JOIN Reservation r ON r.ID = ri.Reservation_ID
                       WHERE ri.Items_ID = i.ID 
                       AND '{date:yyyy-MM-dd}' >= r.CheckinDate 
                       AND '{date:yyyy-MM-dd}' < r.CheckoutDate 
                       AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เช็คเอ้าท์แล้ว')
                   ), 0)) as AvailableAmount
            FROM Items i
            WHERE i.Status = 1 
            ORDER BY i.OrderID ASC";

                return _dbHelper.ExecuteQuery(query);
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
                string query = $@"
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
                    WHERE '{date:yyyy-MM-dd}' >= r.CheckinDate 
                    AND '{date:yyyy-MM-dd}' < r.CheckoutDate
                    AND r.Status NOT IN ('ยกเลิกแล้ว', 'Cancelled')
                    AND r.ID != {excludeReservationId}
                )
            )
            ORDER BY a.AccomName";

                return _dbHelper.ExecuteQuery(query);
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
            string query = $"SELECT count([Customer_MobilePhone]) as CountReserved FROM [Reservation] Where Customer_MobilePhone = '{phoneNumber}' AND Status = N'เช็คอินแล้ว'";
            return _dbHelper.ExecuteQuery(query);
        }

        public async Task<bool> CancelReservationWithRefund(string reservationId)
        {
            try
            {
                // 🔧 FIX: Cancel Payment_History records first
                try
                {
                    _dbHelper.ExecuteInsert($@"
                        UPDATE [dbo].[Payment_History]
                        SET Status = 'CANCELLED',
                            Notes = N'ยกเลิกจากการยกเลิกการจอง (คืนเงิน) ID: {reservationId}'
                        WHERE Reservation_ID = {reservationId}
                        AND Status = 'COMPLETED'");

                    System.Diagnostics.Trace.TraceInformation($"✅ Cancelled Payment_History for Reservation {reservationId}");
                }
                catch (Exception phEx)
                {
                    System.Diagnostics.Trace.TraceWarning($"⚠️ Failed to cancel Payment_History: {phEx.Message}");
                    // Continue - this is non-critical
                }

                // Update reservation status
                _dbHelper.ExecuteInsert($"UPDATE [dbo].[Reservation] SET TotalPrice = 0, Deposit = 0 , [Status] = N'ยกเลิกคืนเงิน' WHERE ID = {reservationId}");
                _dbHelper.ExecuteInsert($"DELETE FROM [dbo].[Reservation_Accommodation] WHERE Reservation_ID = {reservationId}");
                _dbHelper.ExecuteInsert($"DELETE FROM [dbo].[Reservation_Items] WHERE Reservation_ID = {reservationId}");

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
                // 🔧 FIX: Cancel Payment_History records first
                try
                {
                    _dbHelper.ExecuteInsert($@"
                        UPDATE [dbo].[Payment_History]
                        SET Status = 'CANCELLED',
                            Notes = N'ยกเลิกจากการยกเลิกการจอง (ไม่คืนเงิน) ID: {reservationId}'
                        WHERE Reservation_ID = {reservationId}
                        AND Status = 'COMPLETED'");

                    System.Diagnostics.Trace.TraceInformation($"✅ Cancelled Payment_History for Reservation {reservationId}");
                }
                catch (Exception phEx)
                {
                    System.Diagnostics.Trace.TraceWarning($"⚠️ Failed to cancel Payment_History: {phEx.Message}");
                    // Continue - this is non-critical
                }

                // Update reservation status (Deposit is NOT reset to 0 - customer doesn't get refund)
                _dbHelper.ExecuteInsert($"UPDATE [dbo].[Reservation] SET TotalPrice = 0, [Status] = N'ยกเลิกไม่คืนเงิน' WHERE ID = {reservationId}");
                _dbHelper.ExecuteInsert($"DELETE FROM [dbo].[Reservation_Accommodation] WHERE Reservation_ID = {reservationId}");
                _dbHelper.ExecuteInsert($"DELETE FROM [dbo].[Reservation_Items] WHERE Reservation_ID = {reservationId}");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Error canceling reservation without refund {reservationId}: {ex.Message}");
                return false;
            }
        }

        public double CalculateTwoDecimalPoints(double num)
        {
            return Math.Round(num, 2);
        }
    }
}