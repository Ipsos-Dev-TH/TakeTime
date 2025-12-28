using System;
using System.Data;
using System.Web.UI;
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
            _guestPortalService = new GuestPortalService(_connectionString);
            _code = new code();

            // Check session
            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
            }

            if (!IsPostBack)
            {
                LoadChatHistory();
            }
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
                // Try to load from Guest_Chat table
                DataTable dtMessages = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 50
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
                    { "@CreatedDate", DateTime.Now }
                };

                int result = _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO Guest_Chat
                      (Reservation_ID, Guest_Phone, Message, IsFromGuest, Created_Date, Is_Read)
                      VALUES
                      (@ReservationId, @GuestPhone, @Message, @IsFromGuest, @CreatedDate, 0)",
                    parameters);

                if (result > 0)
                {
                    // Clear input
                    txtMessage.Text = "";

                    // Reload chat
                    LoadChatHistory();

                    // Send notification to Front Desk (optional - could use Telegram/Email)
                    try
                    {
                        SendNotificationToFrontDesk(message);
                    }
                    catch { }

                    // Add auto-reply for common questions
                    AddAutoReply(message);
                }
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Chat error: {ex.Message}");

                // Show friendly message
                ScriptManager.RegisterStartupScript(this, GetType(), "chatError",
                    "alert('ไม่สามารถส่งข้อความได้ กรุณาลองใหม่อีกครั้ง');", true);
            }
        }

        /// <summary>
        /// Send notification to Front Desk
        /// </summary>
        private void SendNotificationToFrontDesk(string message)
        {
            try
            {
                // Get room info
                DataTable dtRoom = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT a.Name AS RoomName
                      FROM Reservation r
                      JOIN Accommodation a ON r.Accommodation_ID = a.ID
                      WHERE r.ID = @ReservationId",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@ReservationId", _reservationId }
                    });

                string roomName = dtRoom.Rows.Count > 0 ? dtRoom.Rows[0]["RoomName"].ToString() : "Unknown";

                // Send Telegram notification
                var telegramBot = new TelegramBot();
                telegramBot.SendMessage($"💬 ข้อความจากแขกห้อง {roomName}:\n\n{message}");
            }
            catch { }
        }

        /// <summary>
        /// Add auto-reply for common questions
        /// </summary>
        private void AddAutoReply(string message)
        {
            string autoReply = null;
            message = message.ToLower();

            if (message.Contains("wifi") || message.Contains("password") || message.Contains("รหัส"))
            {
                autoReply = "🔐 WiFi Password: TakeTime2024\nNetwork Name: TakeTime_Guest\n\nหากมีปัญหาในการเชื่อมต่อ กรุณาติดต่อ Front Desk";
            }
            else if (message.Contains("check-out") || message.Contains("checkout") || message.Contains("เช็คเอาท์"))
            {
                autoReply = "⏰ เวลา Check-out มาตรฐาน: 12:00 น.\n\nหากต้องการ Late Check-out กรุณาแจ้งล่วงหน้า (ขึ้นอยู่กับห้องว่าง)\n- 13:00 น. ไม่มีค่าใช้จ่าย\n- 14:00 น. +300 บาท\n- หลัง 14:00 น. คิดเพิ่มครึ่งวัน";
            }
            else if (message.Contains("breakfast") || message.Contains("อาหารเช้า"))
            {
                autoReply = "🍳 อาหารเช้าให้บริการ: 07:00 - 10:00 น.\nสถานที่: ห้องอาหารชั้น 1\n\nหากต้องการรับประทานในห้อง สามารถสั่งผ่าน Room Service ได้";
            }

            if (!string.IsNullOrEmpty(autoReply))
            {
                try
                {
                    // Add auto-reply after 1 second delay simulation
                    var parameters = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@ReservationId", _reservationId },
                        { "@GuestPhone", _guestMobilePhone },
                        { "@Message", autoReply },
                        { "@IsFromGuest", false },
                        { "@CreatedDate", DateTime.Now.AddSeconds(1) }
                    };

                    _code.DatabaseInsertSafe(_connectionString,
                        @"INSERT INTO Guest_Chat
                          (Reservation_ID, Guest_Phone, Message, IsFromGuest, Created_Date, Is_Read)
                          VALUES
                          (@ReservationId, @GuestPhone, @Message, @IsFromGuest, @CreatedDate, 1)",
                        parameters);

                    // Reload to show auto-reply
                    LoadChatHistory();
                }
                catch { }
            }
        }
    }
}
