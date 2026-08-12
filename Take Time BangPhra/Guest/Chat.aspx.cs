using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using System.Web.Services;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class Chat : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private code _code;
        private long _reservationId;
        private string _guestMobilePhone;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Chat", "~/Guest/Dashboard")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            _guestPortalService = new GuestPortalService(_connectionString);
            _code = new code();

            // Check session
            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
            }

            hfReservationId.Value = _reservationId.ToString();

            if (!IsPostBack)
            {
                LoadChatHistory();
                MarkMessagesAsRead();
                CheckAiEnabled();
            }
        }

        private void CheckAiEnabled()
        {
            try
            {
                var aiSvc = new DeepSeekService(_connectionString);
                pnlAiTab.Visible = aiSvc.IsGuestChatEnabled;
            }
            catch { pnlAiTab.Visible = false; }
        }

        /// <summary>
        /// Validate guest session
        /// </summary>
        private bool ValidateGuestSession()
        {
            string sessionToken = Request.Cookies["GuestSession"]?.Value ?? Session["GuestSessionToken"]?.ToString();

            if (string.IsNullOrEmpty(sessionToken))
            {
                return false;
            }

            DataTable dtSession = _guestPortalService.ValidateGuestSession(sessionToken);

            if (dtSession.Rows.Count == 0)
            {
                return false;
            }

            DataRow session = dtSession.Rows[0];
            _reservationId = Convert.ToInt64(session["Reservation_ID"]);
            _guestMobilePhone = session["Customer_MobilePhone"].ToString();

            return true;
        }

        /// <summary>
        /// Load chat history
        /// </summary>
        private void LoadChatHistory()
        {
            try
            {
                DataTable dtMessages = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 100
                        Message,
                        IsFromGuest,
                        Created_Date AS CreatedAt
                      FROM Guest_Chat
                      WHERE Reservation_ID = @ReservationId
                      ORDER BY Created_Date ASC",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@ReservationId", _reservationId }
                    });

                if (dtMessages.Rows.Count > 0)
                {
                    rptMessages.DataSource = dtMessages;
                    rptMessages.DataBind();
                    pnlEmptyChat.Visible = false;
                }
                else
                {
                    pnlEmptyChat.Visible = true;
                }
            }
            catch
            {
                // Table might not exist yet - show welcome message
                pnlEmptyChat.Visible = true;
            }
        }

        /// <summary>
        /// Mark messages as read by guest
        /// </summary>
        private void MarkMessagesAsRead()
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Guest_Chat
                      SET Guest_Read = 1, Guest_Read_Date = @ReadDate
                      WHERE Reservation_ID = @ReservationId AND IsFromGuest = 0 AND (Guest_Read = 0 OR Guest_Read IS NULL)",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@ReservationId", _reservationId },
                        { "@ReadDate", DateTime.Now }
                    });
            }
            catch { }
        }

        /// <summary>
        /// Send message
        /// </summary>
        protected void btnSend_Click(object sender, EventArgs e)
        {
            string message = txtMessage.Text.Trim();

            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            // Re-validate session
            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
            }

            try
            {
                // Insert message to database
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@ReservationId", _reservationId },
                    { "@GuestPhone", _guestMobilePhone },
                    { "@Message", message },
                    { "@IsFromGuest", true },
                    { "@CreatedDate", DateTime.Now },
                    { "@IsRead", false },
                    { "@GuestRead", true }
                };

                int result = _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO Guest_Chat
                      (Reservation_ID, Guest_Phone, Message, IsFromGuest, Created_Date, Is_Read, Guest_Read)
                      VALUES
                      (@ReservationId, @GuestPhone, @Message, @IsFromGuest, @CreatedDate, @IsRead, @GuestRead)",
                    parameters);

                if (result > 0)
                {
                    // Clear input
                    txtMessage.Text = "";

                    // Reload chat
                    LoadChatHistory();

                    // Send notification to Front Desk
                    SendNotificationToFrontDesk(message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chat error: {ex.Message}");
                ScriptManager.RegisterStartupScript(this, GetType(), "chatError",
                    "alert('ไม่สามารถส่งข้อความได้ กรุณาลองใหม่อีกครั้ง');", true);
            }
        }

        /// <summary>
        /// Send notification to Front Desk via Telegram
        /// </summary>
        private void SendNotificationToFrontDesk(string message)
        {
            try
            {
                // Get room info
                DataTable dtRoom = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ISNULL(dbo.fn_GetReservationRoomNames(r.ID), N'ไม่ระบุห้อง') AS RoomName, c.Name AS GuestName
                      FROM Reservation r
                      JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                      WHERE r.ID = @ReservationId",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@ReservationId", _reservationId }
                    });

                string roomName = dtRoom.Rows.Count > 0 ? dtRoom.Rows[0]["RoomName"].ToString() : "Unknown";
                string guestName = dtRoom.Rows.Count > 0 ? dtRoom.Rows[0]["GuestName"].ToString() : "Guest";

                // Send Telegram notification with alert emoji
                var telegramBot = new TelegramBot();
                string alertMessage = $"🔔💬 ข้อความใหม่จากแขก!\n\n" +
                                     $"🚪 ห้อง: {roomName}\n" +
                                     $"👤 ชื่อ: {guestName}\n" +
                                     $"📱 เบอร์: {_guestMobilePhone}\n\n" +
                                     $"💬 ข้อความ:\n{message}\n\n" +
                                     $"⚡ กรุณาตอบกลับที่: Admin > Chat Management";
                telegramBot.SendMessage(alertMessage);
            }
            catch { }
        }

        /// <summary>
        /// Web method to check for new messages (for AJAX polling)
        /// </summary>
        [WebMethod]
        public static object GetNewMessages(long reservationId)
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                var code = new code();

                DataTable dt = code.DatabaseQuerySafe(connectionString,
                    @"SELECT COUNT(*) AS NewCount
                      FROM Guest_Chat
                      WHERE Reservation_ID = @ReservationId
                        AND IsFromGuest = 0
                        AND (Guest_Read = 0 OR Guest_Read IS NULL)",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@ReservationId", reservationId }
                    });

                int newCount = dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["NewCount"]) : 0;
                return new { hasNewMessages = newCount > 0, count = newCount };
            }
            catch
            {
                return new { hasNewMessages = false, count = 0 };
            }
        }

        [WebMethod]
        public static object AskAI(string message, string sessionKey)
        {
            try
            {
                string connStr = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                var aiSvc = new DeepSeekService(connStr);

                if (!aiSvc.IsGuestChatEnabled)
                    return new { success = false, message = "AI ผู้ช่วยยังไม่เปิดให้บริการ" };

                var history = aiSvc.GetHistory(sessionKey);
                var result = aiSvc.SendMessage(message, sessionKey, history);

                return new { success = result.Success, message = result.Message };
            }
            catch (Exception ex)
            {
                return new { success = false, message = "เกิดข้อผิดพลาด: " + ex.Message };
            }
        }
    }
}
