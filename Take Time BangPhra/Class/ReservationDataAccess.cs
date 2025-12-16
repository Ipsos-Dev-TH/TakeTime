using System;
using System.Collections.Generic;
using System.Data;
using System.Configuration;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// 🔒 SECURE Data Access Layer for Reservation operations
    /// All methods use parameterized queries to prevent SQL Injection
    /// </summary>
    public class ReservationDataAccess
    {
        private readonly code _code;
        private readonly string _connectionString;

        public ReservationDataAccess()
        {
            _code = new code();
            _connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        }

        public ReservationDataAccess(string connectionString)
        {
            _code = new code();
            _connectionString = connectionString;
        }

        #region Reservation Queries

        /// <summary>
        /// Get reservation by ID and phone number (for verification)
        /// </summary>
        public DataTable GetReservationByIdAndPhone(int reservationId, string phoneNumber)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@phoneNumber", phoneNumber }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM [Reservation] WHERE ID = @reservationId AND Customer_MobilePhone = @phoneNumber",
                parameters);
        }

        /// <summary>
        /// Get reservation with accommodations
        /// </summary>
        public DataTable GetReservationWithAccommodations(int reservationId, string phoneNumber)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@phoneNumber", phoneNumber }
            };

            // 🔧 FIX: Use Reservation.Customer_MobilePhone for clarity
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT * FROM [Reservation]
                  RIGHT JOIN Reservation_Accommodation ON Reservation_Accommodation.Reservation_ID = Reservation.ID
                  WHERE Reservation.ID = @reservationId AND Reservation.Customer_MobilePhone = @phoneNumber",
                parameters);
        }

        /// <summary>
        /// Get reservation with items
        /// </summary>
        public DataTable GetReservationWithItems(int reservationId, string phoneNumber)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@phoneNumber", phoneNumber }
            };

            // 🔧 FIX: Use Reservation.Customer_MobilePhone for clarity
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT * FROM [Reservation]
                  RIGHT JOIN Reservation_Items ON Reservation_Items.Reservation_ID = Reservation.ID
                  WHERE Reservation.ID = @reservationId AND Reservation.Customer_MobilePhone = @phoneNumber",
                parameters);
        }

        /// <summary>
        /// Get reservation with full customer details
        /// </summary>
        public DataTable GetReservationWithCustomerDetails(int reservationId, string phoneNumber)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@phoneNumber", phoneNumber }
            };

            // 🔧 FIX: Changed INNER JOIN to LEFT JOIN for Customer table
            // This allows reservations to be loaded even if Customer record doesn't exist
            // (e.g., when booking with placeholder phone like "02")
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    Reservation.*,
                    -- ⚠️ ระบุ Customer columns ทั้งหมด ยกเว้น Province, District, Subdistrict, Postcode (เพื่อไม่ให้ซ้ำกับ Address table)
                    Customer.ID AS Customer_ID,
                    ISNULL(Customer.MobilePhone, Reservation.Customer_MobilePhone) AS MobilePhone,
                    Customer.Name,
                    Customer.NickName,
                    Customer.ComeFrom,
                    Customer.Remark AS Customer_Remark,
                    Customer.Status AS Customer_Status,
                    Customer.FullName,
                    Customer.Address,
                    Customer.Address1,
                    Customer.Address_ID,
                    Customer.IDNumber,
                    Customer.Email,
                    ISNULL(Customer.Customer_Type_ID, 2) AS Customer_Type_ID,
                    Customer.Branch_Number,
                    Customer.TaxID,
                    Customer.LastUpdated,
                    Customer.LastUpdatedBy_ID,
                    Customer.CreatedDate AS Customer_CreatedDate,
                    Customer.CreatedBy_ID,
                    Customer.IsActive,
                    Customer_Type.Customer_Type,
                    Customer_Type.Customer_Code,
                    Account_Receipt.ID AS Receipt_ID,
                    Account_Receipt.UID AS Receipt_UID,
                    Account_Receipt.IsDeposit,
                    Account_Receipt.UseDeposit,
                    Account_Receipt.Status AS Receipt_Status,
                    Account_Receipt.Paid_Type,
                    -- ✅ ระบุ Address columns จาก Address table (ไม่ใช่จาก Customer legacy columns)
                    Address.Province AS Province,
                    Address.District AS District,
                    Address.SubDistrict AS SubDistrict,
                    Address.PostalCode AS PostalCode,
                    Address.Address_Code
                  FROM [Reservation]
                  LEFT JOIN Customer ON Customer.MobilePhone = Reservation.Customer_MobilePhone
                  LEFT JOIN Customer_Type ON Customer_Type_ID = Customer_Type.ID
                  LEFT JOIN Address ON Address.ID = Customer.Address_ID
                  LEFT JOIN Account_Receipt ON Account_Receipt.Reservation_ID = Reservation.ID
                  WHERE Reservation.ID = @reservationId AND Reservation.Customer_MobilePhone = @phoneNumber",
                parameters);
        }

        /// <summary>
        /// Check for duplicate accommodation bookings
        /// </summary>
        public DataTable CheckDuplicateAccommodation(DateTime checkinDate, string accomName, int excludeReservationId = 0)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@checkinDate", checkinDate.ToString("yyyy-MM-dd") },
                { "@accomName", accomName },
                { "@excludeReservationId", excludeReservationId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT Reservation_Accommodation.*,
                         Reservation.Status,
                         Accommodation.LimitWithPeople, Accommodation.People
                  FROM [Taketime].[dbo].[Reservation_Accommodation]
                  INNER JOIN Reservation ON Reservation.ID = Reservation_ID
                  INNER JOIN Accommodation ON Accommodation.ID = Accommodation_ID
                  WHERE CheckinDate = @checkinDate
                  AND Accommodation.AccomName = @accomName
                  AND Reservation_ID != @excludeReservationId",
                parameters);
        }

        /// <summary>
        /// 🔒 Check if accommodation is available for a date range (prevent race condition)
        /// Returns DataTable with conflicting reservations (empty if available)
        /// </summary>
        public DataTable CheckAccommodationAvailability(
            int accommodationId,
            DateTime checkinDate,
            DateTime checkoutDate,
            int excludeReservationId = 0)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@accommodationId", accommodationId },
                { "@checkinDate", checkinDate.ToString("yyyy-MM-dd") },
                { "@checkoutDate", checkoutDate.ToString("yyyy-MM-dd") },
                { "@excludeReservationId", excludeReservationId }
            };

            // ✅ Check for date range overlap:
            // Two reservations conflict if their date ranges overlap
            // Overlap occurs when: NewCheckin < ExistingCheckout AND NewCheckout > ExistingCheckin
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    R.ID AS ReservationID,
                    R.CheckinDate,
                    R.CheckoutDate,
                    R.Status,
                    A.AccomName,
                    C.Name AS CustomerName,
                    C.MobilePhone
                  FROM [Reservation_Accommodation] RA
                  INNER JOIN [Reservation] R ON R.ID = RA.Reservation_ID
                  INNER JOIN [Accommodation] A ON A.ID = RA.Accommodation_ID
                  LEFT JOIN [Customer] C ON C.MobilePhone = R.Customer_MobilePhone
                  WHERE RA.Accommodation_ID = @accommodationId
                    AND R.ID != @excludeReservationId
                    AND R.Status NOT IN (N'ยกเลิก', N'เสร็จสิ้น', N'ไม่มาเช็คอิน')
                    AND (
                        -- Check for date range overlap
                        (@checkinDate < R.CheckoutDate AND @checkoutDate > R.CheckinDate)
                    )",
                parameters);
        }

        /// <summary>
        /// Get old accommodations for a reservation
        /// </summary>
        public DataTable GetOldAccommodations(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT * FROM [Reservation_Accommodation]
                  INNER JOIN Reservation ON Reservation.ID = Reservation_ID
                  INNER JOIN Accommodation ON Accommodation.ID = Accommodation_ID
                  WHERE Reservation.ID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Get old items for a reservation
        /// </summary>
        public DataTable GetOldItems(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT * FROM [Reservation_Items]
                  INNER JOIN Reservation ON Reservation.ID = Reservation_ID
                  WHERE Reservation.ID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Get receipts for reservation
        /// </summary>
        public DataTable GetReceiptsByReservation(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM [Account_Receipt] WHERE RESERVATION_ID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Get deposit receipts for reservation
        /// </summary>
        public DataTable GetDepositReceipts(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT * FROM Account_Receipt
                  WHERE Reservation_ID = @reservationId
                  AND IsDeposit = 'True'
                  AND Status = 'Normal'
                  AND UseDeposit = 'false'",
                parameters);
        }

        /// <summary>
        /// Get deposit receipt details
        /// </summary>
        public DataTable GetReceiptDetails(string receiptId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@receiptId", receiptId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM Account_Receipt_Detail WHERE Receipt_ID = @receiptId",
                parameters);
        }

        #endregion

        #region Customer Queries

        /// <summary>
        /// Get customer by phone number
        /// </summary>
        public DataTable GetCustomerByPhone(string phoneNumber)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@phoneNumber", phoneNumber }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM [Customer] WHERE MobilePhone = @phoneNumber",
                parameters);
        }

        #endregion

        #region Insert/Update Operations

        /// <summary>
        /// Insert reservation accommodation
        /// </summary>
        public void InsertReservationAccommodation(int reservationId, int accommodationId, int amount, decimal price, string useCoupon)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@accommodationId", accommodationId },
                { "@amount", amount },
                { "@price", price },
                { "@useCoupon", useCoupon }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO [dbo].[Reservation_Accommodation]
                  ([Reservation_ID], [Accommodation_ID], [Amount], [Price], [Use_Coupon])
                  VALUES (@reservationId, @accommodationId, @amount, @price, @useCoupon)",
                parameters);
        }

        /// <summary>
        /// Insert reservation item
        /// </summary>
        public void InsertReservationItem(int reservationId, int itemId, int amount, decimal price)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@itemId", itemId },
                { "@amount", amount },
                { "@price", price }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO [dbo].[Reservation_Items]
                  ([Reservation_ID], [Items_ID], [Amount], [Price])
                  VALUES (@reservationId, @itemId, @amount, @price)",
                parameters);
        }

        /// <summary>
        /// Update reservation accommodation (price and amount)
        /// </summary>
        public void UpdateReservationAccommodation(int reservationId, int accommodationId, int amount, decimal price)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@amount", amount },
                { "@price", price },
                { "@accommodationId", accommodationId },
                { "@reservationId", reservationId }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE [dbo].[Reservation_Accommodation]
                  SET [Amount] = @amount, [Price] = @price
                  WHERE Accommodation_ID = @accommodationId AND Reservation_ID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Update reservation item
        /// </summary>
        public void UpdateReservationItem(int reservationId, int itemId, int amount, decimal price)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@amount", amount },
                { "@price", price },
                { "@itemId", itemId },
                { "@reservationId", reservationId }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE [dbo].[Reservation_Items]
                  SET [Amount] = @amount, [Price] = @price
                  WHERE Items_ID = @itemId AND Reservation_ID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Delete reservation item
        /// </summary>
        public void DeleteReservationItem(int reservationId, int itemId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@itemId", itemId },
                { "@reservationId", reservationId }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"DELETE FROM [dbo].[Reservation_Items]
                  WHERE Items_ID = @itemId AND Reservation_ID = @reservationId",
                parameters);
        }

        public void DeleteReservationAccommodation(int reservationId, int accommodationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@accommodationId", accommodationId },
                { "@reservationId", reservationId }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"DELETE FROM [dbo].[Reservation_Accommodation]
                  WHERE Accommodation_ID = @accommodationId AND Reservation_ID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Update reservation with all details
        /// </summary>
        public void UpdateReservation(int reservationId, string phoneNumber, DateTime checkinDate,
            DateTime checkoutDate, int stayDays, decimal totalPrice, decimal deposit, string remark)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@phoneNumber", phoneNumber },
                { "@checkinDate", checkinDate.ToString("yyyy-MM-dd") },
                { "@checkoutDate", checkoutDate.ToString("yyyy-MM-dd") },
                { "@stayDays", stayDays },
                { "@totalPrice", totalPrice },
                { "@deposit", deposit },
                { "@remark", remark },
                { "@reservationId", reservationId }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE [dbo].[Reservation]
                  SET [Customer_MobilePhone] = @phoneNumber,
                      [CheckinDate] = @checkinDate,
                      [CheckoutDate] = @checkoutDate,
                      [StayDays] = @stayDays,
                      [TotalPrice] = @totalPrice,
                      [Deposit] = @deposit,
                      [Remark] = @remark
                  WHERE ID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Update reservation status to checked-in
        /// </summary>
        public void CheckInReservation(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE [dbo].[Reservation]
                  SET [Status] = N'เช็คอินแล้ว', [Deposit] = [TotalPrice]
                  WHERE ID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Insert a new reservation and return its ID
        /// </summary>
        public int InsertNewReservation(
            string customerPhone,
            DateTime checkinDate,
            DateTime checkoutDate,
            int stayDays,
            string status,
            decimal totalPrice,
            decimal deposit,
            string remark,
            string reserveBy,
            DateTime createdDate,
            bool noCreateReceipt,
            bool noNameInReceipt)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@customerPhone", customerPhone },
                { "@checkinDate", checkinDate.ToString("yyyy-MM-dd") },
                { "@checkoutDate", checkoutDate.ToString("yyyy-MM-dd") },
                { "@stayDays", stayDays },
                { "@status", status },
                { "@totalPrice", totalPrice },
                { "@deposit", deposit },
                { "@remark", remark },
                { "@reserveBy", reserveBy },
                { "@createdDate", createdDate.ToString("yyyy-MM-dd HH:mm:ss.fff") },
                { "@noCreateReceipt", noCreateReceipt ? "True" : "False" },
                { "@noNameInReceipt", noNameInReceipt ? "True" : "False" }
            };

            return _code.DatabaseInsertReturnSafe(_connectionString,
                @"INSERT INTO [dbo].[Reservation]
                  ([Customer_MobilePhone], [CheckinDate], [CheckoutDate], [StayDays], [Status],
                   [TotalPrice], [Deposit], [Remark], [Reserve_By], [Created_Date],
                   [NoCreateReceipt], [NoNameinReceipt])
                  VALUES
                  (@customerPhone, @checkinDate, @checkoutDate, @stayDays, @status,
                   @totalPrice, @deposit, @remark, @reserveBy, @createdDate,
                   @noCreateReceipt, @noNameInReceipt);
                  SELECT SCOPE_IDENTITY();",
                parameters);
        }

        #endregion
    }
}
