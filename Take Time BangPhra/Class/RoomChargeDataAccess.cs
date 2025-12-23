using System;
using System.Collections.Generic;
using System.Data;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// 🔒 SECURE Data Access Layer for Room Charge operations
    /// All methods use parameterized queries to prevent SQL Injection
    /// Created: 2025-11-06
    /// </summary>
    public class RoomChargeDataAccess
    {
        private readonly code _code;
        private readonly string _connectionString;

        public RoomChargeDataAccess(string connectionString)
        {
            _code = new code();
            _connectionString = connectionString;
        }

        #region Charge Creation

        /// <summary>
        /// Create a room charge record
        /// </summary>
        public long CreateRoomCharge(
            int reservationId,
            int productId,
            string productName,
            string productBarcode,
            int? categoryId,
            decimal quantity,
            decimal unitPrice,
            decimal totalAmount,
            string chargeType,
            int? chargedByAdminId,
            string notes = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@productId", productId },
                { "@productName", productName },
                { "@barcode", productBarcode ?? (object)DBNull.Value },
                { "@categoryId", categoryId ?? (object)DBNull.Value },
                { "@quantity", quantity },
                { "@unitPrice", unitPrice },
                { "@totalAmount", totalAmount },
                { "@chargeType", chargeType },
                { "@chargedBy", chargedByAdminId ?? (object)DBNull.Value },
                { "@notes", notes ?? (object)DBNull.Value }
            };

            return _code.DatabaseInsertReturnSafe(_connectionString,
                @"INSERT INTO Reservation_Product_Charges (
                    Reservation_ID, Product_ID, Product_Name, Product_Barcode,
                    Category_ID, Quantity, UnitPrice, TotalAmount, ChargeType,
                    ChargedBy_AdminID, Notes, Status, IsPaid, StockDeducted
                  )
                  VALUES (
                    @reservationId, @productId, @productName, @barcode,
                    @categoryId, @quantity, @unitPrice, @totalAmount, @chargeType,
                    @chargedBy, @notes, 'PENDING', 0, 1
                  );
                  SELECT SCOPE_IDENTITY();",
                parameters);
        }

        #endregion

        #region Charge Queries

        /// <summary>
        /// Get all charges for a reservation
        /// Query directly from table to avoid view dependency
        /// </summary>
        public DataTable GetReservationCharges(int reservationId, string status = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            // Query directly from table - no view dependency
            string query = @"
                SELECT
                    rpc.ID,
                    rpc.Reservation_ID,
                    rpc.Product_ID,
                    rpc.Product_Name,
                    rpc.Product_Barcode,
                    rpc.Quantity,
                    rpc.UnitPrice,
                    rpc.TotalAmount,
                    rpc.Status,
                    rpc.ChargeType,
                    rpc.IsPaid,
                    rpc.ChargedDate,
                    rpc.Receipt_ID,
                    rpc.PaymentDate,
                    rpc.Notes
                FROM Reservation_Product_Charges rpc
                WHERE rpc.Reservation_ID = @reservationId";

            if (!string.IsNullOrEmpty(status))
            {
                query += " AND rpc.Status = @status";
                parameters.Add("@status", status);
            }

            query += " ORDER BY rpc.ChargedDate DESC";

            return _code.DatabaseQuerySafe(_connectionString, query, parameters);
        }

        /// <summary>
        /// Get a single charge by ID
        /// </summary>
        public DataTable GetChargeById(long chargeId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@chargeId", chargeId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT * FROM Reservation_Product_Charges
                  WHERE ID = @chargeId",
                parameters);
        }

        /// <summary>
        /// Get total pending charges for a reservation (PENDING only)
        /// </summary>
        public decimal GetTotalPendingCharges(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            var result = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ISNULL(SUM(TotalAmount), 0) as TotalPending
                  FROM Reservation_Product_Charges
                  WHERE Reservation_ID = @reservationId
                  AND Status = 'PENDING'",
                parameters);

            if (result.Rows.Count > 0)
            {
                return Convert.ToDecimal(result.Rows[0]["TotalPending"]);
            }

            return 0;
        }

        /// <summary>
        /// Get total product charges for a reservation (ALL statuses except CANCELLED)
        /// Queries directly from Reservation_Product_Charges table
        /// </summary>
        public decimal GetTotalProductCharges(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            var result = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ISNULL(SUM(TotalAmount), 0) as TotalCharges
                  FROM Reservation_Product_Charges
                  WHERE Reservation_ID = @reservationId
                  AND Status <> 'CANCELLED'",
                parameters);

            if (result.Rows.Count > 0 && result.Rows[0]["TotalCharges"] != DBNull.Value)
            {
                return Convert.ToDecimal(result.Rows[0]["TotalCharges"]);
            }

            return 0;
        }

        /// <summary>
        /// Get active guest reservations (for POS dropdown)
        /// </summary>
        public DataTable GetActiveGuestReservations()
        {
            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM vw_ActiveGuestReservations ORDER BY CheckInDate DESC",
                null);
        }

        /// <summary>
        /// Get active guest reservations for a specific date (for POS dropdown with date filter)
        /// </summary>
        /// <param name="searchDate">Date to search for active guests</param>
        public DataTable GetActiveGuestReservations(DateTime searchDate)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@searchDate", searchDate.Date }
            };

            // Query similar to vw_ActiveGuestReservations but with parameterized date
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    R.ID AS ReservationID,
                    C.Name AS CustomerName,
                    C.NickName AS CustomerNickName,
                    C.MobilePhone AS CustomerPhone,
                    R.CheckinDate AS CheckInDate,
                    R.CheckoutDate AS CheckOutDate,
                    R.Status,
                    R.TotalPrice,
                    R.Deposit AS TotalPaid,
                    (R.TotalPrice - ISNULL(R.Deposit, 0)) AS RemainingBalance,
                    dbo.fn_GetReservationRoomNames(R.ID) AS RoomNames,
                    ISNULL(PC.PendingTotal, 0) AS PendingCharges,
                    C.Name + N' (' + dbo.fn_GetReservationRoomNames(R.ID) + N') - เข้า: ' +
                    CONVERT(VARCHAR, R.CheckinDate, 103) + N' ออก: ' +
                    CONVERT(VARCHAR, R.CheckoutDate, 103) AS DisplayText
                FROM Reservation R
                INNER JOIN Customer C ON R.Customer_MobilePhone = C.MobilePhone
                LEFT JOIN (
                    SELECT Reservation_ID, SUM(TotalAmount) AS PendingTotal
                    FROM Reservation_Product_Charges
                    WHERE Status = 'PENDING'
                    GROUP BY Reservation_ID
                ) PC ON R.ID = PC.Reservation_ID
                WHERE
                    CAST(@searchDate AS DATE) >= CAST(R.CheckinDate AS DATE)
                    AND CAST(@searchDate AS DATE) <= CAST(R.CheckoutDate AS DATE)
                    AND R.Status NOT IN (N'ยกเลิก', N'เช็คเอาท์แล้ว', N'เสร็จสิ้น', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                ORDER BY R.CheckinDate DESC",
                parameters);
        }

        /// <summary>
        /// Get reservation info by ID (for guest selection)
        /// </summary>
        public DataTable GetReservationById(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM vw_ActiveGuestReservations WHERE ReservationID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Get reservation info by ID for a specific date (for guest selection with date filter)
        /// </summary>
        public DataTable GetReservationById(int reservationId, DateTime searchDate)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@searchDate", searchDate.Date }
            };

            // Same query as GetActiveGuestReservations(DateTime) but filtered by ID
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    R.ID AS ReservationID,
                    C.Name AS CustomerName,
                    C.NickName AS CustomerNickName,
                    C.MobilePhone AS CustomerPhone,
                    R.CheckinDate AS CheckInDate,
                    R.CheckoutDate AS CheckOutDate,
                    R.Status,
                    R.TotalPrice,
                    R.Deposit AS TotalPaid,
                    (R.TotalPrice - ISNULL(R.Deposit, 0)) AS RemainingBalance,
                    dbo.fn_GetReservationRoomNames(R.ID) AS RoomNames,
                    ISNULL(PC.PendingTotal, 0) AS PendingCharges,
                    C.Name + N' (' + dbo.fn_GetReservationRoomNames(R.ID) + N') - เข้า: ' +
                    CONVERT(VARCHAR, R.CheckinDate, 103) + N' ออก: ' +
                    CONVERT(VARCHAR, R.CheckoutDate, 103) AS DisplayText
                FROM Reservation R
                INNER JOIN Customer C ON R.Customer_MobilePhone = C.MobilePhone
                LEFT JOIN (
                    SELECT Reservation_ID, SUM(TotalAmount) AS PendingTotal
                    FROM Reservation_Product_Charges
                    WHERE Status = 'PENDING'
                    GROUP BY Reservation_ID
                ) PC ON R.ID = PC.Reservation_ID
                WHERE
                    R.ID = @reservationId
                    AND CAST(@searchDate AS DATE) >= CAST(R.CheckinDate AS DATE)
                    AND CAST(@searchDate AS DATE) <= CAST(R.CheckoutDate AS DATE)
                    AND R.Status NOT IN (N'ยกเลิก', N'เช็คเอาท์แล้ว', N'เสร็จสิ้น', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')",
                parameters);
        }

        #endregion

        #region Charge Updates

        /// <summary>
        /// Mark charge as paid (when receipt is generated)
        /// </summary>
        public void MarkChargeAsPaid(long chargeId, string receiptId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@chargeId", chargeId },
                { "@receiptId", receiptId },
                { "@paymentDate", DateTime.Now }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Reservation_Product_Charges
                  SET Status = 'PAID',
                      IsPaid = 1,
                      Receipt_ID = @receiptId,
                      PaymentDate = @paymentDate
                  WHERE ID = @chargeId",
                parameters);
        }

        /// <summary>
        /// Mark all pending charges as paid for a reservation
        /// </summary>
        public int MarkAllChargesAsPaid(int reservationId, string receiptId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@receiptId", receiptId },
                { "@paymentDate", DateTime.Now }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Reservation_Product_Charges
                  SET Status = 'PAID',
                      IsPaid = 1,
                      Receipt_ID = @receiptId,
                      PaymentDate = @paymentDate
                  WHERE Reservation_ID = @reservationId
                  AND Status = 'PENDING'",
                parameters);

            // Return number of rows affected
            var countResult = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT COUNT(*) as UpdateCount
                  FROM Reservation_Product_Charges
                  WHERE Reservation_ID = @reservationId
                  AND Receipt_ID = @receiptId",
                new Dictionary<string, object>
                {
                    { "@reservationId", reservationId },
                    { "@receiptId", receiptId }
                });

            if (countResult.Rows.Count > 0)
            {
                return Convert.ToInt32(countResult.Rows[0]["UpdateCount"]);
            }

            return 0;
        }

        /// <summary>
        /// Cancel charge and update status
        /// </summary>
        public void CancelCharge(
            long chargeId,
            int? cancelledByAdminId,
            string cancelReason)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@chargeId", chargeId },
                { "@cancelledBy", cancelledByAdminId ?? (object)DBNull.Value },
                { "@cancelDate", DateTime.Now }
            };

            // Try to update with all columns, but catch error if columns don't exist
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Reservation_Product_Charges
                      SET Status = 'CANCELLED',
                          StockReturned = 1,
                          CancelledDate = @cancelDate,
                          CancelledBy_AdminID = @cancelledBy
                      WHERE ID = @chargeId",
                    parameters);
            }
            catch
            {
                // Fallback: minimal update if columns don't exist
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Reservation_Product_Charges
                      SET Status = 'CANCELLED'
                      WHERE ID = @chargeId",
                    new Dictionary<string, object> { { "@chargeId", chargeId } });
            }
        }

        #endregion

        #region Stock Management

        /// <summary>
        /// Deduct product stock by creating Product_Out record
        /// Stock = Product_In - Product_Out (no Amount column in Product table)
        /// </summary>
        public void DeductProductStock(int productId, decimal quantity, string receiptId = null, string remark = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@productId", productId },
                { "@quantity", quantity },
                { "@receiptId", receiptId ?? (object)DBNull.Value },
                { "@dateTime", DateTime.Now }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO Product_Out (DateTime_Out, Product_ID, Amount, PricePerUnit, Account_Receipt_ID)
                  SELECT @dateTime, @productId, @quantity,
                         ISNULL(Sell_Price, 0), @receiptId
                  FROM Product WHERE ID = @productId",
                parameters);
        }

        /// <summary>
        /// Return product stock by creating Product_In record
        /// Used when cancelling room charges
        /// Note: Uses Sell_Price as PricePerUnit (Product table has no Buy_Price column)
        /// </summary>
        public void ReturnProductStock(int productId, decimal quantity, string remark = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@productId", productId },
                { "@quantity", quantity },
                { "@dateTime", DateTime.Now }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO Product_In (DateTime_In, Product_ID, Amount, PricePerUnit)
                  SELECT @dateTime, @productId, @quantity,
                         ISNULL(Sell_Price, 0)
                  FROM Product WHERE ID = @productId",
                parameters);
        }

        /// <summary>
        /// Get current product stock (calculated from Product_In - Product_Out)
        /// </summary>
        public decimal GetProductStock(int productId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@productId", productId }
            };

            var result = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT dbo.fn_GetProductStock(@productId) as CurrentStock",
                parameters);

            if (result.Rows.Count > 0)
            {
                return Convert.ToDecimal(result.Rows[0]["CurrentStock"]);
            }

            return 0;
        }

        #endregion

        #region Reservation Updates

        /// <summary>
        /// Update reservation total price
        /// </summary>
        public void UpdateReservationTotal(int reservationId, decimal amount, bool isAddition)
        {
            string operation = isAddition ? "+" : "-";

            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@amount", amount }
            };

            _code.DatabaseInsertSafe(_connectionString,
                $@"UPDATE Reservation
                   SET TotalPrice = TotalPrice {operation} @amount
                   WHERE ID = @reservationId",
                parameters);
        }

        /// <summary>
        /// Get reservation total price
        /// </summary>
        public decimal GetReservationTotal(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            var result = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ISNULL(TotalPrice, 0) as TotalPrice
                  FROM Reservation
                  WHERE ID = @reservationId",
                parameters);

            if (result.Rows.Count > 0)
            {
                return Convert.ToDecimal(result.Rows[0]["TotalPrice"]);
            }

            return 0;
        }

        #endregion

        #region Pre-bookable Products

        /// <summary>
        /// Get pre-bookable products (for new reservations)
        /// Stock is calculated from Product_In - Product_Out
        /// </summary>
        public DataTable GetPreBookableProducts()
        {
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT
                    ID,
                    Product_Name,
                    Sell_Price,
                    Category_ID,
                    dbo.fn_GetProductStock(ID) AS CurrentStock
                  FROM Product
                  WHERE Status = 'True'
                  AND CanPreBook = 1
                  AND dbo.fn_GetProductStock(ID) > 0
                  ORDER BY Product_Name",
                null);
        }

        #endregion
    }
}
