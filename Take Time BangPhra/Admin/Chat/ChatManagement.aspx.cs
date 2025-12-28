using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.Services;

namespace Take_Time_BangPhra.Admin.Chat
{
    public partial class ChatManagement : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;

        protected void Page_Load(object sender, EventArgs e)
        {
            _code = new code();

            // Check admin login
            if (Session["username"] == null)
            {
                Response.Redirect("~/Admin/Login.aspx");
                return;
            }

            if (!IsPostBack)
            {
                LoadChatList();
                UpdateUnreadBadge();
            }
        }

        /// <summary>
        /// Load list of all active chats
        /// </summary>
        private void LoadChatList()
        {
            try
            {
                DataTable dtChats = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        gc.Reservation_ID AS ReservationId,
                        c.Name AS GuestName,
                        a.Name AS RoomName,
                        gc.Guest_Phone AS Phone,
                        (SELECT TOP 1 Message FROM Guest_Chat gc2
                         WHERE gc2.Reservation_ID = gc.Reservation_ID
                         ORDER BY gc2.Created_Date DESC) AS LastMessage,
                        (SELECT TOP 1 Created_Date FROM Guest_Chat gc2
                         WHERE gc2.Reservation_ID = gc.Reservation_ID
                         ORDER BY gc2.Created_Date DESC) AS LastMessageTime,
                        (SELECT COUNT(*) FROM Guest_Chat gc2
                         WHERE gc2.Reservation_ID = gc.Reservation_ID
                           AND gc2.IsFromGuest = 1
                           AND (gc2.Is_Read = 0 OR gc2.Is_Read IS NULL)) AS UnreadCount
                      FROM Guest_Chat gc
                      INNER JOIN Reservation r ON gc.Reservation_ID = r.ID
                      INNER JOIN Customer c ON r.Customer_ID = c.ID
                      INNER JOIN Accommodation a ON r.Accommodation_ID = a.ID
                      WHERE r.Status NOT IN ('CANCELLED', 'NO-SHOW')
                      GROUP BY gc.Reservation_ID, c.Name, a.Name, gc.Guest_Phone
                      ORDER BY
                        (SELECT COUNT(*) FROM Guest_Chat gc2
                         WHERE gc2.Reservation_ID = gc.Reservation_ID
                           AND gc2.IsFromGuest = 1
                           AND (gc2.Is_Read = 0 OR gc2.Is_Read IS NULL)) DESC,
                        (SELECT TOP 1 Created_Date FROM Guest_Chat gc2
                         WHERE gc2.Reservation_ID = gc.Reservation_ID
                         ORDER BY gc2.Created_Date DESC) DESC",
                    null);

                if (dtChats.Rows.Count > 0)
                {
                    rptChatList.DataSource = dtChats;
                    rptChatList.DataBind();
                    pnlNoChats.Visible = false;
                }
                else
                {
                    pnlNoChats.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadChatList error: {ex.Message}");
                pnlNoChats.Visible = true;
            }
        }

        /// <summary>
        /// Update unread message badge
        /// </summary>
        private void UpdateUnreadBadge()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS UnreadCount
                      FROM Guest_Chat
                      WHERE IsFromGuest = 1 AND (Is_Read = 0 OR Is_Read IS NULL)",
                    null);

                int unreadCount = dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["UnreadCount"]) : 0;

                if (unreadCount > 0)
                {
                    pnlUnreadBadge.Visible = true;
                    lblUnreadCount.Text = unreadCount.ToString();
                }
                else
                {
                    pnlUnreadBadge.Visible = false;
                }
            }
            catch { }
        }

        /// <summary>
        /// Load selected chat
        /// </summary>
        protected void btnLoadChat_Click(object sender, EventArgs e)
        {
            string selectedChatId = hfSelectedChat.Value;

            if (string.IsNullOrEmpty(selectedChatId))
            {
                return;
            }

            LoadChatDetail(Convert.ToInt64(selectedChatId));
        }

        /// <summary>
        /// Load chat detail
        /// </summary>
        private void LoadChatDetail(long reservationId)
        {
            try
            {
                // Get guest info
                DataTable dtGuest = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        c.Name AS GuestName,
                        a.Name AS RoomName,
                        gc.Guest_Phone AS Phone
                      FROM Guest_Chat gc
                      INNER JOIN Reservation r ON gc.Reservation_ID = r.ID
                      INNER JOIN Customer c ON r.Customer_ID = c.ID
                      INNER JOIN Accommodation a ON r.Accommodation_ID = a.ID
                      WHERE gc.Reservation_ID = @ReservationId
                      GROUP BY c.Name, a.Name, gc.Guest_Phone",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@ReservationId", reservationId }
                    });

                if (dtGuest.Rows.Count > 0)
                {
                    lblChatGuestName.Text = dtGuest.Rows[0]["GuestName"].ToString();
                    lblChatRoomName.Text = dtGuest.Rows[0]["RoomName"].ToString();
                    lblChatPhone.Text = dtGuest.Rows[0]["Phone"].ToString();
                }

                // Get messages
                DataTable dtMessages = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        Message,
                        IsFromGuest,
                        Created_Date AS CreatedAt,
                        ISNULL(Staff_Name, 'Front Desk') AS StaffName
                      FROM Guest_Chat
                      WHERE Reservation_ID = @ReservationId
                      ORDER BY Created_Date ASC",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@ReservationId", reservationId }
                    });

                rptChatMessages.DataSource = dtMessages;
                rptChatMessages.DataBind();

                // Mark messages as read
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Guest_Chat
                      SET Is_Read = 1, Read_Date = @ReadDate
                      WHERE Reservation_ID = @ReservationId AND IsFromGuest = 1 AND (Is_Read = 0 OR Is_Read IS NULL)",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@ReservationId", reservationId },
                        { "@ReadDate", DateTime.Now }
                    });

                pnlChatDetail.Visible = true;
                pnlSelectChat.Visible = false;

                // Reload chat list to update unread counts
                LoadChatList();
                UpdateUnreadBadge();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadChatDetail error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send reply
        /// </summary>
        protected void btnSendReply_Click(object sender, EventArgs e)
        {
            string reply = txtReply.Text.Trim();
            string selectedChatId = hfSelectedChat.Value;

            if (string.IsNullOrEmpty(reply) || string.IsNullOrEmpty(selectedChatId))
            {
                return;
            }

            try
            {
                long reservationId = Convert.ToInt64(selectedChatId);

                // Get guest phone
                DataTable dtGuest = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 Guest_Phone FROM Guest_Chat WHERE Reservation_ID = @ReservationId",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@ReservationId", reservationId }
                    });

                string guestPhone = dtGuest.Rows.Count > 0 ? dtGuest.Rows[0]["Guest_Phone"].ToString() : "";
                string staffName = Session["username"]?.ToString() ?? "Front Desk";

                // Insert reply
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@ReservationId", reservationId },
                    { "@GuestPhone", guestPhone },
                    { "@Message", reply },
                    { "@IsFromGuest", false },
                    { "@CreatedDate", DateTime.Now },
                    { "@IsRead", true },
                    { "@GuestRead", false },
                    { "@StaffName", staffName }
                };

                int result = _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO Guest_Chat
                      (Reservation_ID, Guest_Phone, Message, IsFromGuest, Created_Date, Is_Read, Guest_Read, Staff_Name)
                      VALUES
                      (@ReservationId, @GuestPhone, @Message, @IsFromGuest, @CreatedDate, @IsRead, @GuestRead, @StaffName)",
                    parameters);

                if (result > 0)
                {
                    txtReply.Text = "";
                    LoadChatDetail(reservationId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendReply error: {ex.Message}");
                ScriptManager.RegisterStartupScript(this, GetType(), "replyError",
                    "alert('ไม่สามารถส่งข้อความได้ กรุณาลองใหม่');", true);
            }
        }

        /// <summary>
        /// Mark conversation as resolved
        /// </summary>
        protected void btnMarkResolved_Click(object sender, EventArgs e)
        {
            // Optional: Add a status field or archive the conversation
            ScriptManager.RegisterStartupScript(this, GetType(), "resolved",
                "alert('การสนทนานี้ถูกทำเครื่องหมายว่าแก้ไขแล้ว');", true);
        }

        /// <summary>
        /// Handle chat list item command
        /// </summary>
        protected void rptChatList_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            // Not used in current implementation
        }

        /// <summary>
        /// Web method to check for unread messages (for AJAX polling)
        /// </summary>
        [WebMethod]
        public static object CheckUnreadMessages()
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                var code = new code();

                // Get unread count
                DataTable dtCount = code.DatabaseQuerySafe(connectionString,
                    @"SELECT COUNT(*) AS UnreadCount
                      FROM Guest_Chat
                      WHERE IsFromGuest = 1 AND (Is_Read = 0 OR Is_Read IS NULL)",
                    null);

                int unreadCount = dtCount.Rows.Count > 0 ? Convert.ToInt32(dtCount.Rows[0]["UnreadCount"]) : 0;

                // Get latest unread message info
                string latestRoom = "";
                string latestGuest = "";

                if (unreadCount > 0)
                {
                    DataTable dtLatest = code.DatabaseQuerySafe(connectionString,
                        @"SELECT TOP 1
                            a.Name AS RoomName,
                            c.Name AS GuestName
                          FROM Guest_Chat gc
                          INNER JOIN Reservation r ON gc.Reservation_ID = r.ID
                          INNER JOIN Customer c ON r.Customer_ID = c.ID
                          INNER JOIN Accommodation a ON r.Accommodation_ID = a.ID
                          WHERE gc.IsFromGuest = 1 AND (gc.Is_Read = 0 OR gc.Is_Read IS NULL)
                          ORDER BY gc.Created_Date DESC",
                        null);

                    if (dtLatest.Rows.Count > 0)
                    {
                        latestRoom = dtLatest.Rows[0]["RoomName"].ToString();
                        latestGuest = dtLatest.Rows[0]["GuestName"].ToString();
                    }
                }

                return new
                {
                    hasUnread = unreadCount > 0,
                    count = unreadCount,
                    latestRoom = latestRoom,
                    latestGuest = latestGuest
                };
            }
            catch
            {
                return new { hasUnread = false, count = 0, latestRoom = "", latestGuest = "" };
            }
        }
    }
}
