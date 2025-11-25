using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// Service for managing Guest Portal operations including QR verification,
    /// room service orders, housekeeping requests, and concierge services
    /// </summary>
    public class GuestPortalService
    {
        private readonly string _connectionString;
        private readonly Code _code;

        public GuestPortalService(string connectionString)
        {
            _connectionString = connectionString;
            _code = new Code();
        }

        #region QR Code Management

        /// <summary>
        /// Generate QR code for a specific accommodation
        /// </summary>
        public DataTable GenerateRoomQRCode(int accommodationId, short adminId, string baseUrl = "https://taketimebangphra.com/Guest/Portal?qr=")
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Accommodation_ID", accommodationId },
                    { "@Admin_ID", adminId },
                    { "@Base_URL", baseUrl }
                };

                return _code.DatabaseQuerySafe(_connectionString,
                    "EXEC sp_Generate_Room_QR_Code @Accommodation_ID, @Admin_ID, @Base_URL",
                    parameters);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating QR code: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get all room QR codes
        /// </summary>
        public DataTable GetAllRoomQRCodes()
        {
            try
            {
                string query = @"
                    SELECT RQR.*, A.Name AS Accommodation_Name, A.Status
                    FROM Room_QR_Codes RQR
                    INNER JOIN Accommodation A ON A.ID = RQR.Accommodation_ID
                    ORDER BY A.OrderID, A.Name";

                return _code.DatabaseQuerySafe(_connectionString, query, null);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving QR codes: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Verify guest access using QR token and mobile phone
        /// </summary>
        public DataTable VerifyGuestAccess(string qrToken, string customerMobilePhone)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@QR_Token", qrToken },
                    { "@Customer_MobilePhone", customerMobilePhone }
                };

                return _code.DatabaseQuerySafe(_connectionString,
                    "EXEC sp_Verify_Guest_Portal_Access @QR_Token, @Customer_MobilePhone",
                    parameters);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error verifying guest access: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Create guest portal session
        /// </summary>
        public string CreateGuestSession(int reservationId, string customerMobilePhone, int accommodationId,
            string qrToken, DateTime checkInDate, DateTime checkOutDate, string ipAddress, string userAgent)
        {
            try
            {
                // Generate session token
                string sessionToken = Guid.NewGuid().ToString("N") + "_" +
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

                var parameters = new Dictionary<string, object>
                {
                    { "@Session_Token", sessionToken },
                    { "@Reservation_ID", reservationId },
                    { "@Customer_MobilePhone", customerMobilePhone },
                    { "@Accommodation_ID", accommodationId },
                    { "@QR_Token", qrToken },
                    { "@Check_In_Date", checkInDate },
                    { "@Check_Out_Date", checkOutDate },
                    { "@IP_Address", ipAddress },
                    { "@User_Agent", userAgent }
                };

                string query = @"
                    INSERT INTO Guest_Portal_Sessions
                    (Session_Token, Reservation_ID, Customer_MobilePhone, Accommodation_ID, QR_Token,
                     Check_In_Date, Check_Out_Date, IP_Address, User_Agent)
                    VALUES
                    (@Session_Token, @Reservation_ID, @Customer_MobilePhone, @Accommodation_ID, @QR_Token,
                     @Check_In_Date, @Check_Out_Date, @IP_Address, @User_Agent)";

                _code.DatabaseInsertSafe(_connectionString, query, parameters);

                return sessionToken;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating guest session: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validate guest session token
        /// </summary>
        public DataTable ValidateGuestSession(string sessionToken)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Session_Token", sessionToken }
                };

                string query = @"
                    SELECT GPS.*, C.Name AS Customer_Name, A.Name AS Accommodation_Name
                    FROM Guest_Portal_Sessions GPS
                    INNER JOIN Customer C ON C.MobilePhone = GPS.Customer_MobilePhone
                    INNER JOIN Accommodation A ON A.ID = GPS.Accommodation_ID
                    WHERE GPS.Session_Token = @Session_Token
                      AND GPS.Is_Active = 1
                      AND GPS.Check_Out_Date >= CAST(GETDATE() AS DATE)";

                DataTable dt = _code.DatabaseQuerySafe(_connectionString, query, parameters);

                // Update last activity
                if (dt.Rows.Count > 0)
                {
                    var updateParams = new Dictionary<string, object>
                    {
                        { "@Session_Token", sessionToken }
                    };

                    _code.DatabaseInsertSafe(_connectionString,
                        "UPDATE Guest_Portal_Sessions SET Last_Activity_Date = GETDATE() WHERE Session_Token = @Session_Token",
                        updateParams);
                }

                return dt;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error validating session: {ex.Message}", ex);
            }
        }

        #endregion

        #region Room Service Orders

        /// <summary>
        /// Create room service order
        /// </summary>
        public long CreateRoomServiceOrder(int reservationId, string customerMobilePhone, int accommodationId,
            string deliveryInstructions, decimal totalAmount, string paymentMethod, string paymentSlipPath = null)
        {
            try
            {
                // Generate order number
                string orderNumber = "RS" + DateTime.Now.ToString("yyyyMMddHHmmss") +
                    new Random().Next(1000, 9999).ToString();

                var parameters = new Dictionary<string, object>
                {
                    { "@Order_Number", orderNumber },
                    { "@Reservation_ID", reservationId },
                    { "@Customer_MobilePhone", customerMobilePhone },
                    { "@Accommodation_ID", accommodationId },
                    { "@Delivery_Instructions", deliveryInstructions },
                    { "@Total_Amount", totalAmount },
                    { "@Payment_Method", paymentMethod },
                    { "@Payment_Slip_Path", paymentSlipPath ?? (object)DBNull.Value }
                };

                string query = @"
                    INSERT INTO Guest_Room_Service_Orders
                    (Order_Number, Reservation_ID, Customer_MobilePhone, Accommodation_ID,
                     Delivery_Instructions, Total_Amount, Payment_Method, Payment_Slip_Path)
                    VALUES
                    (@Order_Number, @Reservation_ID, @Customer_MobilePhone, @Accommodation_ID,
                     @Delivery_Instructions, @Total_Amount, @Payment_Method, @Payment_Slip_Path);
                    SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

                DataTable dt = _code.DatabaseQuerySafe(_connectionString, query, parameters);
                return dt.Rows.Count > 0 ? Convert.ToInt64(dt.Rows[0][0]) : 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating room service order: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Add item to room service order
        /// </summary>
        public bool AddRoomServiceItem(long orderId, int productId, string productName, int quantity,
            decimal unitPrice, string notes = null)
        {
            try
            {
                decimal subtotal = quantity * unitPrice;

                var parameters = new Dictionary<string, object>
                {
                    { "@Order_ID", orderId },
                    { "@Product_ID", productId },
                    { "@Product_Name", productName },
                    { "@Quantity", quantity },
                    { "@Unit_Price", unitPrice },
                    { "@Subtotal", subtotal },
                    { "@Notes", notes ?? (object)DBNull.Value }
                };

                string query = @"
                    INSERT INTO Guest_Room_Service_Items
                    (Order_ID, Product_ID, Product_Name, Quantity, Unit_Price, Subtotal, Notes)
                    VALUES
                    (@Order_ID, @Product_ID, @Product_Name, @Quantity, @Unit_Price, @Subtotal, @Notes)";

                _code.DatabaseInsertSafe(_connectionString, query, parameters);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error adding room service item: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get room service orders for a reservation
        /// </summary>
        public DataTable GetRoomServiceOrders(int reservationId)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Reservation_ID", reservationId }
                };

                string query = @"
                    SELECT * FROM Guest_Room_Service_Orders
                    WHERE Reservation_ID = @Reservation_ID
                    ORDER BY Order_Date DESC";

                return _code.DatabaseQuerySafe(_connectionString, query, parameters);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving room service orders: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get room service order items
        /// </summary>
        public DataTable GetRoomServiceOrderItems(long orderId)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Order_ID", orderId }
                };

                string query = @"
                    SELECT * FROM Guest_Room_Service_Items
                    WHERE Order_ID = @Order_ID
                    ORDER BY ID";

                return _code.DatabaseQuerySafe(_connectionString, query, parameters);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving order items: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Update room service order status
        /// </summary>
        public bool UpdateRoomServiceOrderStatus(long orderId, string status, short? staffId = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Order_ID", orderId },
                    { "@Order_Status", status }
                };

                string query = "UPDATE Guest_Room_Service_Orders SET Order_Status = @Order_Status";

                if (status == "CONFIRMED" && staffId.HasValue)
                {
                    query += ", Confirmed_By = @Staff_ID, Confirmed_Date = GETDATE()";
                    parameters["@Staff_ID"] = staffId.Value;
                }
                else if (status == "DELIVERED" && staffId.HasValue)
                {
                    query += ", Delivered_By = @Staff_ID, Delivered_Date = GETDATE()";
                    parameters["@Staff_ID"] = staffId.Value;
                }

                query += " WHERE ID = @Order_ID";

                _code.DatabaseInsertSafe(_connectionString, query, parameters);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating order status: {ex.Message}", ex);
            }
        }

        #endregion

        #region Housekeeping Requests

        /// <summary>
        /// Create housekeeping request
        /// </summary>
        public long CreateHousekeepingRequest(int reservationId, string customerMobilePhone, int accommodationId,
            string requestType, string description, string priority = "NORMAL", string preferredTime = null)
        {
            try
            {
                string requestNumber = "HK" + DateTime.Now.ToString("yyyyMMddHHmmss") +
                    new Random().Next(1000, 9999).ToString();

                var parameters = new Dictionary<string, object>
                {
                    { "@Request_Number", requestNumber },
                    { "@Reservation_ID", reservationId },
                    { "@Customer_MobilePhone", customerMobilePhone },
                    { "@Accommodation_ID", accommodationId },
                    { "@Request_Type", requestType },
                    { "@Request_Description", description },
                    { "@Priority", priority },
                    { "@Preferred_Time", preferredTime ?? (object)DBNull.Value }
                };

                string query = @"
                    INSERT INTO Guest_Housekeeping_Requests
                    (Request_Number, Reservation_ID, Customer_MobilePhone, Accommodation_ID,
                     Request_Type, Request_Description, Priority, Preferred_Time)
                    VALUES
                    (@Request_Number, @Reservation_ID, @Customer_MobilePhone, @Accommodation_ID,
                     @Request_Type, @Request_Description, @Priority, @Preferred_Time);
                    SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

                DataTable dt = _code.DatabaseQuerySafe(_connectionString, query, parameters);
                return dt.Rows.Count > 0 ? Convert.ToInt64(dt.Rows[0][0]) : 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating housekeeping request: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get housekeeping requests for a reservation
        /// </summary>
        public DataTable GetHousekeepingRequests(int reservationId)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Reservation_ID", reservationId }
                };

                string query = @"
                    SELECT * FROM Guest_Housekeeping_Requests
                    WHERE Reservation_ID = @Reservation_ID
                    ORDER BY Request_Date DESC";

                return _code.DatabaseQuerySafe(_connectionString, query, parameters);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving housekeeping requests: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Update housekeeping request status
        /// </summary>
        public bool UpdateHousekeepingStatus(long requestId, string status, short? staffId = null, string staffNotes = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Request_ID", requestId },
                    { "@Request_Status", status }
                };

                string query = "UPDATE Guest_Housekeeping_Requests SET Request_Status = @Request_Status";

                if (status == "ASSIGNED" && staffId.HasValue)
                {
                    query += ", Assigned_To = @Staff_ID, Assigned_Date = GETDATE()";
                    parameters["@Staff_ID"] = staffId.Value;
                }
                else if (status == "COMPLETED" && staffId.HasValue)
                {
                    query += ", Completed_By = @Staff_ID, Completed_Date = GETDATE()";
                    parameters["@Staff_ID"] = staffId.Value;
                }

                if (!string.IsNullOrEmpty(staffNotes))
                {
                    query += ", Staff_Notes = @Staff_Notes";
                    parameters["@Staff_Notes"] = staffNotes;
                }

                query += " WHERE ID = @Request_ID";

                _code.DatabaseInsertSafe(_connectionString, query, parameters);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating housekeeping status: {ex.Message}", ex);
            }
        }

        #endregion

        #region Concierge Services

        /// <summary>
        /// Create concierge service request
        /// </summary>
        public long CreateConciergeRequest(int reservationId, string customerMobilePhone, int accommodationId,
            string serviceType, string serviceName, string description, DateTime? preferredDate = null,
            string preferredTime = null, int? numberOfGuests = null)
        {
            try
            {
                string requestNumber = "CC" + DateTime.Now.ToString("yyyyMMddHHmmss") +
                    new Random().Next(1000, 9999).ToString();

                var parameters = new Dictionary<string, object>
                {
                    { "@Request_Number", requestNumber },
                    { "@Reservation_ID", reservationId },
                    { "@Customer_MobilePhone", customerMobilePhone },
                    { "@Accommodation_ID", accommodationId },
                    { "@Service_Type", serviceType },
                    { "@Service_Name", serviceName },
                    { "@Request_Description", description },
                    { "@Preferred_Date", preferredDate ?? (object)DBNull.Value },
                    { "@Preferred_Time", preferredTime ?? (object)DBNull.Value },
                    { "@Number_Of_Guests", numberOfGuests ?? (object)DBNull.Value }
                };

                string query = @"
                    INSERT INTO Guest_Concierge_Requests
                    (Request_Number, Reservation_ID, Customer_MobilePhone, Accommodation_ID,
                     Service_Type, Service_Name, Request_Description, Preferred_Date, Preferred_Time, Number_Of_Guests)
                    VALUES
                    (@Request_Number, @Reservation_ID, @Customer_MobilePhone, @Accommodation_ID,
                     @Service_Type, @Service_Name, @Request_Description, @Preferred_Date, @Preferred_Time, @Number_Of_Guests);
                    SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

                DataTable dt = _code.DatabaseQuerySafe(_connectionString, query, parameters);
                return dt.Rows.Count > 0 ? Convert.ToInt64(dt.Rows[0][0]) : 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating concierge request: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get concierge requests for a reservation
        /// </summary>
        public DataTable GetConciergeRequests(int reservationId)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Reservation_ID", reservationId }
                };

                string query = @"
                    SELECT * FROM Guest_Concierge_Requests
                    WHERE Reservation_ID = @Reservation_ID
                    ORDER BY Request_Date DESC";

                return _code.DatabaseQuerySafe(_connectionString, query, parameters);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving concierge requests: {ex.Message}", ex);
            }
        }

        #endregion

        #region Chat Messages

        /// <summary>
        /// Send chat message
        /// </summary>
        public long SendChatMessage(int reservationId, string customerMobilePhone, string messageText,
            string senderType, string senderName, short? senderId = null, string attachmentPath = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Reservation_ID", reservationId },
                    { "@Customer_MobilePhone", customerMobilePhone },
                    { "@Message_Text", messageText },
                    { "@Sender_Type", senderType },
                    { "@Sender_Name", senderName },
                    { "@Sender_ID", senderId ?? (object)DBNull.Value },
                    { "@Attachment_Path", attachmentPath ?? (object)DBNull.Value }
                };

                string query = @"
                    INSERT INTO Guest_Chat_Messages
                    (Reservation_ID, Customer_MobilePhone, Message_Text, Sender_Type, Sender_Name, Sender_ID, Attachment_Path)
                    VALUES
                    (@Reservation_ID, @Customer_MobilePhone, @Message_Text, @Sender_Type, @Sender_Name, @Sender_ID, @Attachment_Path);
                    SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

                DataTable dt = _code.DatabaseQuerySafe(_connectionString, query, parameters);
                return dt.Rows.Count > 0 ? Convert.ToInt64(dt.Rows[0][0]) : 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error sending chat message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get chat messages for a reservation
        /// </summary>
        public DataTable GetChatMessages(int reservationId, int limit = 100)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Reservation_ID", reservationId },
                    { "@Limit", limit }
                };

                string query = @"
                    SELECT TOP (@Limit) * FROM Guest_Chat_Messages
                    WHERE Reservation_ID = @Reservation_ID
                    ORDER BY Message_Date DESC";

                return _code.DatabaseQuerySafe(_connectionString, query, parameters);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving chat messages: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Mark messages as read
        /// </summary>
        public bool MarkMessagesAsRead(int reservationId, string readerType)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Reservation_ID", reservationId },
                    { "@Sender_Type", readerType == "STAFF" ? "GUEST" : "STAFF" }
                };

                string query = @"
                    UPDATE Guest_Chat_Messages
                    SET Is_Read = 1, Read_Date = GETDATE()
                    WHERE Reservation_ID = @Reservation_ID
                      AND Sender_Type = @Sender_Type
                      AND Is_Read = 0";

                _code.DatabaseInsertSafe(_connectionString, query, parameters);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error marking messages as read: {ex.Message}", ex);
            }
        }

        #endregion

        #region Guest Balance & Payment

        /// <summary>
        /// Get guest balance for current reservation
        /// </summary>
        public DataTable GetGuestBalance(int reservationId)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Reservation_ID", reservationId }
                };

                string query = @"
                    SELECT
                        R.ID AS Reservation_ID,
                        R.Accommodation,
                        R.CheckIn,
                        R.CheckOut,
                        R.TotalPrice AS Room_Charge,
                        ISNULL((SELECT SUM(Total_Amount)
                                FROM Guest_Room_Service_Orders
                                WHERE Reservation_ID = R.ID
                                  AND Payment_Method = 'CHARGE_TO_ROOM'
                                  AND Payment_Status = 'CHARGED'), 0) AS Room_Service_Charges,
                        R.TotalPrice + ISNULL((SELECT SUM(Total_Amount)
                                               FROM Guest_Room_Service_Orders
                                               WHERE Reservation_ID = R.ID
                                                 AND Payment_Method = 'CHARGE_TO_ROOM'
                                                 AND Payment_Status = 'CHARGED'), 0) AS Total_Charges,
                        R.DepositReceive AS Deposit_Paid,
                        (R.TotalPrice + ISNULL((SELECT SUM(Total_Amount)
                                                FROM Guest_Room_Service_Orders
                                                WHERE Reservation_ID = R.ID
                                                  AND Payment_Method = 'CHARGE_TO_ROOM'
                                                  AND Payment_Status = 'CHARGED'), 0)) - R.DepositReceive AS Balance_Due
                    FROM Reservation R
                    WHERE R.ID = @Reservation_ID";

                return _code.DatabaseQuerySafe(_connectionString, query, parameters);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving guest balance: {ex.Message}", ex);
            }
        }

        #endregion
    }
}
