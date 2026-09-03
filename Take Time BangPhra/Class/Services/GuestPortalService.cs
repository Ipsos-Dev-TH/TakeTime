using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using Take_Time_BangPhra;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// Service for managing Guest Portal operations including QR verification,
    /// room service orders, housekeeping requests, and concierge services
    /// </summary>
    public class GuestPortalService
    {
        private readonly string _connectionString;
        private readonly code _code;

        public GuestPortalService(string connectionString)
        {
            _connectionString = connectionString;
            _code = new code();
        }

        #region QR Code Management

        /// <summary>
        /// Generate QR code for a specific accommodation
        /// </summary>
        public DataTable GenerateRoomQRCode(byte accommodationId, short adminId, string baseUrl = "https://taketimebangphra.com/Guest/Portal?qr=")
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
                // Check if Room_QR_Codes table exists
                var checkParams = new Dictionary<string, object>();
                DataTable dtCheck = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT COUNT(*) AS TableExists FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Room_QR_Codes'",
                    checkParams);

                if (dtCheck == null || dtCheck.Rows.Count == 0 || Convert.ToInt32(dtCheck.Rows[0]["TableExists"]) == 0)
                {
                    // Table doesn't exist, return empty DataTable with expected columns
                    DataTable emptyDt = new DataTable();
                    emptyDt.Columns.Add("ID", typeof(int));
                    emptyDt.Columns.Add("Accommodation_ID", typeof(byte));
                    emptyDt.Columns.Add("Accommodation_Name", typeof(string));
                    emptyDt.Columns.Add("QR_Token", typeof(string));
                    emptyDt.Columns.Add("QR_Data", typeof(string));
                    emptyDt.Columns.Add("Is_Active", typeof(bool));
                    emptyDt.Columns.Add("Created_Date", typeof(DateTime));
                    emptyDt.Columns.Add("Status", typeof(bool));
                    return emptyDt;
                }

                string query = @"
                    SELECT RQR.*, A.AccomName AS Accommodation_Name, A.Status
                    FROM Room_QR_Codes RQR
                    INNER JOIN Accommodation A ON A.ID = RQR.Accommodation_ID
                    ORDER BY A.OrderID, A.AccomName";

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
        public string CreateGuestSession(long reservationId, string customerMobilePhone, byte accommodationId,
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
                    SELECT GPS.*, C.Name AS Customer_Name, A.AccomName AS Accommodation_Name
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
        /// <param name="totalAmount">ยอดรวมที่ลูกค้าต้องจ่าย = ค่าสินค้า + ค่าบริการ</param>
        /// <param name="serviceCharge">
        /// ค่าบริการที่คิดจริง (snapshot) — เก็บแยกเพื่อให้บิล/บัญชีแยกบรรทัดได้
        /// และแก้การตั้งค่าทีหลังไม่กระทบออเดอร์เก่า
        /// </param>
        public long CreateRoomServiceOrder(long reservationId, string customerMobilePhone, byte accommodationId,
            string deliveryInstructions, decimal totalAmount, string paymentMethod, string paymentSlipPath = null,
            decimal serviceCharge = 0m)
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

                // คอลัมน์ Service_Charge มาจาก PHASE18_21 — ถ้าฐานยังไม่อัปเดต ให้ insert แบบเดิมได้
                bool hasSvcColumn = ColumnExists("Guest_Room_Service_Orders", "Service_Charge");
                if (hasSvcColumn) parameters["@Service_Charge"] = serviceCharge;

                string query = hasSvcColumn
                    ? @"INSERT INTO Guest_Room_Service_Orders
                        (Order_Number, Reservation_ID, Customer_MobilePhone, Accommodation_ID,
                         Delivery_Instructions, Total_Amount, Payment_Method, Payment_Slip_Path, Service_Charge)
                        VALUES
                        (@Order_Number, @Reservation_ID, @Customer_MobilePhone, @Accommodation_ID,
                         @Delivery_Instructions, @Total_Amount, @Payment_Method, @Payment_Slip_Path, @Service_Charge);
                        SELECT CAST(SCOPE_IDENTITY() AS BIGINT);"
                    : @"INSERT INTO Guest_Room_Service_Orders
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
        public DataTable GetRoomServiceOrders(long reservationId)
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
        public long CreateHousekeepingRequest(long reservationId, string customerMobilePhone, byte accommodationId,
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
        public DataTable GetHousekeepingRequests(long reservationId)
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
        public long CreateConciergeRequest(long reservationId, string customerMobilePhone, byte accommodationId,
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
        public DataTable GetConciergeRequests(long reservationId)
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
        public long SendChatMessage(long reservationId, string customerMobilePhone, string messageText,
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
        public DataTable GetChatMessages(long reservationId, int limit = 100)
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
        public bool MarkMessagesAsRead(long reservationId, string readerType)
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
        public DataTable GetGuestBalance(long reservationId)
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

        #region Room Service Ordering Schedule (เปิด-ปิด ระบบสั่งของ)

        /// <summary>
        /// อ่านการตั้งค่าเปิด-ปิดระบบสั่งของ (แถวเดียว ID=1) — คืน null ถ้ายังไม่ได้รันสคริปต์ migration
        /// </summary>
        public DataRow GetRoomServiceSettings()
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 * FROM Guest_RoomService_Settings ORDER BY ID", null);
                return (dt != null && dt.Rows.Count > 0) ? dt.Rows[0] : null;
            }
            catch
            {
                // ตารางยังไม่ถูกสร้าง — ถือว่ายังไม่ตั้งค่า (เปิดบริการตามเดิม)
                return null;
            }
        }

        /// <summary>
        /// ตรวจสอบว่าตอนนี้เปิดให้ลูกค้าสั่งของหรือไม่
        ///   - Is_Enabled = 0      → ปิด (ปิดทั้งระบบ)
        ///   - Manual_Mode = OPEN  → เปิด (บังคับเปิด)
        ///   - Manual_Mode = CLOSED→ ปิด (บังคับปิด)
        ///   - Manual_Mode = AUTO  → เปิดตามช่วงเวลา Open_Time..Close_Time (รองรับข้ามเที่ยงคืน)
        /// ถ้ายังไม่ได้ตั้งค่า (ไม่มีตาราง/แถว) → เปิดตามเดิม (fail-open) เพื่อไม่ให้กระทบของเดิม
        /// </summary>
        public bool IsRoomServiceOpen(out string message)
        {
            message = null;
            DataRow s = GetRoomServiceSettings();
            if (s == null) return true; // ยังไม่ตั้งค่า → เปิดตามเดิม

            bool enabled = s["Is_Enabled"] != DBNull.Value && Convert.ToBoolean(s["Is_Enabled"]);
            string mode = (s["Manual_Mode"]?.ToString() ?? "AUTO").Trim().ToUpper();
            string closedMsg = s["Closed_Message"] == DBNull.Value ? null : s["Closed_Message"].ToString();
            if (string.IsNullOrWhiteSpace(closedMsg))
                closedMsg = "ขณะนี้ปิดรับออเดอร์ กรุณาสั่งในเวลาทำการ";

            if (!enabled)
            {
                message = closedMsg;
                return false;
            }

            if (mode == "OPEN") return true;
            if (mode == "CLOSED")
            {
                message = closedMsg;
                return false;
            }

            // AUTO — เทียบช่วงเวลา
            TimeSpan now = DateTime.Now.TimeOfDay;
            TimeSpan open = ToTimeSpan(s["Open_Time"], new TimeSpan(8, 0, 0));
            TimeSpan close = ToTimeSpan(s["Close_Time"], new TimeSpan(20, 0, 0));

            bool isOpen;
            if (open <= close)
                isOpen = now >= open && now <= close;            // ช่วงปกติในวันเดียว
            else
                isOpen = now >= open || now <= close;             // ช่วงข้ามเที่ยงคืน

            if (!isOpen)
            {
                message = $"{closedMsg} (เวลาทำการ {open:hh\\:mm} - {close:hh\\:mm})";
                return false;
            }
            return true;
        }

        private static TimeSpan ToTimeSpan(object value, TimeSpan fallback)
        {
            if (value == null || value == DBNull.Value) return fallback;
            if (value is TimeSpan ts) return ts;
            TimeSpan parsed;
            return TimeSpan.TryParse(value.ToString(), out parsed) ? parsed : fallback;
        }

        /// <summary>บันทึกการตั้งค่าเปิด-ปิดระบบสั่งของ (upsert แถว ID=1)</summary>
        public bool SaveRoomServiceSettings(bool isEnabled, string manualMode, string openTime,
            string closeTime, string closedMessage, string updatedBy)
        {
            string mode = (manualMode ?? "AUTO").Trim().ToUpper();
            if (mode != "OPEN" && mode != "CLOSED") mode = "AUTO";

            var parameters = new Dictionary<string, object>
            {
                { "@Is_Enabled", isEnabled ? 1 : 0 },
                { "@Manual_Mode", mode },
                { "@Open_Time", string.IsNullOrWhiteSpace(openTime) ? "08:00:00" : openTime },
                { "@Close_Time", string.IsNullOrWhiteSpace(closeTime) ? "20:00:00" : closeTime },
                { "@Closed_Message", (object)closedMessage ?? DBNull.Value },
                { "@Updated_By", (object)updatedBy ?? DBNull.Value }
            };

            int rows = _code.DatabaseInsertSafe(_connectionString,
                @"IF EXISTS (SELECT 1 FROM Guest_RoomService_Settings WHERE ID = 1)
                      UPDATE Guest_RoomService_Settings
                         SET Is_Enabled = @Is_Enabled, Manual_Mode = @Manual_Mode,
                             Open_Time = @Open_Time, Close_Time = @Close_Time,
                             Closed_Message = @Closed_Message,
                             Updated_Date = GETDATE(), Updated_By = @Updated_By
                       WHERE ID = 1;
                  ELSE
                      INSERT INTO Guest_RoomService_Settings
                          (ID, Is_Enabled, Manual_Mode, Open_Time, Close_Time, Closed_Message, Updated_Date, Updated_By)
                      VALUES
                          (1, @Is_Enabled, @Manual_Mode, @Open_Time, @Close_Time, @Closed_Message, GETDATE(), @Updated_By);",
                parameters);

            return rows > 0;
        }

        #endregion

        #region Room Service — ค่าบริการ (Service Charge)

        // cache ผลตรวจคอลัมน์ (schema ไม่เปลี่ยนระหว่างรัน) — กัน query INFORMATION_SCHEMA ทุกออเดอร์
        private static readonly Dictionary<string, bool> _columnCache =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        /// <summary>คอลัมน์มีอยู่จริงไหม — ให้โค้ดใหม่ทำงานได้แม้ยังไม่ได้รัน migration</summary>
        private bool ColumnExists(string table, string column)
        {
            string key = table + "." + column;
            lock (_columnCache)
            {
                bool cached;
                if (_columnCache.TryGetValue(key, out cached)) return cached;
            }
            bool exists = false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 1 FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_NAME = @t AND COLUMN_NAME = @c",
                    new Dictionary<string, object> { { "@t", table }, { "@c", column } });
                exists = dt != null && dt.Rows.Count > 0;
            }
            catch { }
            lock (_columnCache) { _columnCache[key] = exists; }
            return exists;
        }

        /// <summary>ผลการคิดค่าบริการ (คำนวณฝั่งเซิร์ฟเวอร์เสมอ — ห้ามเชื่อยอดจากหน้าเว็บ)</summary>
        public class ServiceChargeResult
        {
            /// <summary>NONE | PERCENT | PER_ITEM | PER_ORDER</summary>
            public string Mode = "NONE";
            /// <summary>ค่าที่ตั้งไว้ (% หรือ บาท ตามโหมด)</summary>
            public decimal Value;
            /// <summary>เพดานค่าบริการ (0 = ไม่จำกัด)</summary>
            public decimal MaxAmount;
            /// <summary>ชื่อที่แสดงให้ลูกค้า</summary>
            public string Label = "ค่าบริการ";
            /// <summary>ยอดค่าบริการที่คิดจริง</summary>
            public decimal Amount;
            /// <summary>คำอธิบายสั้น ๆ เช่น "10%" / "฿5 × 3 ชิ้น" / "ต่อครั้ง"</summary>
            public string Detail = "";
            public bool HasCharge { get { return Amount > 0m; } }
        }

        /// <summary>อ่านการตั้งค่าค่าบริการ (ไม่คิดยอด) — ใช้ส่งให้หน้าเว็บแสดงผลตอนเลือกสินค้า</summary>
        public ServiceChargeResult GetServiceChargeSetting()
        {
            var r = new ServiceChargeResult();
            try
            {
                DataRow s = GetRoomServiceSettings();
                if (s == null) return r;

                // คอลัมน์อาจยังไม่มี (ยังไม่รัน PHASE18_21) → คงค่า NONE
                if (!s.Table.Columns.Contains("Service_Charge_Mode")) return r;

                string mode = (s["Service_Charge_Mode"]?.ToString() ?? "NONE").Trim().ToUpperInvariant();
                if (mode != "PERCENT" && mode != "PER_ITEM" && mode != "PER_ORDER") mode = "NONE";
                r.Mode = mode;

                if (s.Table.Columns.Contains("Service_Charge_Value") && s["Service_Charge_Value"] != DBNull.Value)
                    r.Value = Convert.ToDecimal(s["Service_Charge_Value"]);
                if (s.Table.Columns.Contains("Service_Charge_Max") && s["Service_Charge_Max"] != DBNull.Value)
                    r.MaxAmount = Convert.ToDecimal(s["Service_Charge_Max"]);
                if (s.Table.Columns.Contains("Service_Charge_Label"))
                {
                    string lbl = s["Service_Charge_Label"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(lbl)) r.Label = lbl.Trim();
                }
                if (r.Value <= 0m) r.Mode = "NONE";
            }
            catch { /* ตาราง/คอลัมน์ยังไม่พร้อม → ไม่คิดค่าบริการ */ }
            return r;
        }

        /// <summary>
        /// คิดค่าบริการจากยอดสินค้า + จำนวนชิ้น ตามโหมดที่ตั้งไว้
        /// (ปัดทศนิยม 2 ตำแหน่ง, ไม่เกินเพดานถ้าตั้งไว้)
        /// </summary>
        public ServiceChargeResult CalculateServiceCharge(decimal subtotal, int totalQuantity)
        {
            var r = GetServiceChargeSetting();
            if (r.Mode == "NONE" || subtotal <= 0m) { r.Amount = 0m; return r; }

            switch (r.Mode)
            {
                case "PERCENT":
                    r.Amount = Math.Round(subtotal * r.Value / 100m, 2, MidpointRounding.AwayFromZero);
                    r.Detail = $"{r.Value:0.##}%";
                    break;
                case "PER_ITEM":
                    int qty = totalQuantity > 0 ? totalQuantity : 0;
                    r.Amount = Math.Round(r.Value * qty, 2, MidpointRounding.AwayFromZero);
                    r.Detail = $"฿{r.Value:0.##} × {qty} ชิ้น";
                    break;
                case "PER_ORDER":
                    r.Amount = Math.Round(r.Value, 2, MidpointRounding.AwayFromZero);
                    r.Detail = "ต่อครั้ง";
                    break;
            }

            if (r.MaxAmount > 0m && r.Amount > r.MaxAmount)
            {
                r.Amount = r.MaxAmount;
                r.Detail += $" (สูงสุด ฿{r.MaxAmount:0.##})";
            }
            if (r.Amount < 0m) r.Amount = 0m;
            return r;
        }

        /// <summary>บันทึกการตั้งค่าค่าบริการ — แยกจาก SaveRoomServiceSettings เพื่อไม่แตะ signature เดิม</summary>
        public bool SaveServiceChargeSettings(string mode, decimal value, decimal maxAmount, string label)
        {
            string m = (mode ?? "NONE").Trim().ToUpperInvariant();
            if (m != "PERCENT" && m != "PER_ITEM" && m != "PER_ORDER") m = "NONE";
            if (value < 0m) value = 0m;
            if (maxAmount < 0m) maxAmount = 0m;

            try
            {
                int rows = _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Guest_RoomService_Settings
                         SET Service_Charge_Mode  = @Mode,
                             Service_Charge_Value = @Value,
                             Service_Charge_Max   = @Max,
                             Service_Charge_Label = @Label,
                             Updated_Date = GETDATE()
                       WHERE ID = 1",
                    new Dictionary<string, object>
                    {
                        { "@Mode", m },
                        { "@Value", value },
                        { "@Max", maxAmount > 0m ? (object)maxAmount : DBNull.Value },
                        { "@Label", string.IsNullOrWhiteSpace(label) ? (object)DBNull.Value : label.Trim() }
                    });
                return rows > 0;
            }
            catch
            {
                return false;   // ยังไม่รัน PHASE18_21
            }
        }

        #endregion
    }
}
