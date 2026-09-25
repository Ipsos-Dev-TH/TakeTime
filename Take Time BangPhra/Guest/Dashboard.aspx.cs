using System;
using System.Data;
using System.Web;
using System.Web.UI;
using Take_Time_BangPhra;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class Dashboard : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private LoyaltyService _loyaltyService;
        private code _code;
        private string _sessionToken;
        private string _guestMobilePhone;
        private int _reservationId;

        protected void Page_Load(object sender, EventArgs e)
        {
            _guestPortalService = new GuestPortalService(_connectionString);
            _loyaltyService = new LoyaltyService(_connectionString);
            _code = new code();

            // Check session
            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
            }

            ApplyFeatureToggles();

            if (!IsPostBack)
            {
                LoadGuestInfo();
                LoadQuickStats();
            }
        }

        /// <summary>ซ่อนการ์ด/ปุ่มของโมดูลที่ปิดไว้ (ตั้งค่าระบบ → หมวดฟีเจอร์)</summary>
        private void ApplyFeatureToggles()
        {
            try
            {
                bool rs = Feature.On("RoomService");
                bool hk = Feature.On("Housekeeping");
                bool act = Feature.On("Activities");
                bool pts = Feature.On("Loyalty");
                bool rev = Feature.On("Reviews");
                bool chat = Feature.On("Chat");

                phQaRoomService.Visible = rs;
                phSvcRoomService.Visible = rs;
                phBnRoomService.Visible = rs;
                phQaHousekeeping.Visible = hk;
                phSvcHousekeeping.Visible = hk;
                phSvcActivities.Visible = act;
                phSvcActivityBooking.Visible = act;
                phBnActivities.Visible = act;
                phSvcPoints.Visible = pts;
                phPromoReview.Visible = rev;
                phSvcReview.Visible = rev;
                phBnReview.Visible = rev;
                phSvcChat.Visible = chat;
            }
            catch { /* ยังไม่มีตาราง config → โชว์ครบตามเดิม */ }
        }

        /// <summary>
        /// Validate guest session
        /// </summary>
        private bool ValidateGuestSession()
        {
            // Try to get session token from cookie first
            _sessionToken = Request.Cookies["GuestSession"]?.Value;

            if (string.IsNullOrEmpty(_sessionToken))
            {
                // Try session variable
                _sessionToken = Session["GuestSessionToken"]?.ToString();
            }

            if (string.IsNullOrEmpty(_sessionToken))
            {
                return false;
            }

            // Validate session in database
            DataTable dtSession = _guestPortalService.ValidateGuestSession(_sessionToken);

            if (dtSession.Rows.Count == 0)
            {
                return false;
            }

            DataRow session = dtSession.Rows[0];

            // Store session info
            _reservationId = Convert.ToInt32(session["Reservation_ID"]);
            _guestMobilePhone = session["Customer_MobilePhone"].ToString();

            // Update session variables
            Session["GuestSessionToken"] = _sessionToken;
            Session["GuestReservationID"] = _reservationId;
            Session["GuestMobilePhone"] = _guestMobilePhone;
            Session["GuestName"] = session["Customer_Name"].ToString();
            Session["GuestAccommodationName"] = session["Accommodation_Name"].ToString();
            Session["GuestCheckOut"] = Convert.ToDateTime(session["Check_Out_Date"]);

            return true;
        }

        /// <summary>
        /// Load guest information
        /// </summary>
        private void LoadGuestInfo()
        {
            try
            {
                lblGuestName.Text = Session["GuestName"]?.ToString() ?? "Guest";
                lblRoomName.Text = Session["GuestAccommodationName"]?.ToString() ?? "";

                // Load reservation details for dates
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@Reservation_ID", _reservationId }
                };

                DataTable dtReservation = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT CheckIn, CheckOut FROM Reservation WHERE ID = @Reservation_ID",
                    parameters);

                if (dtReservation.Rows.Count > 0)
                {
                    DateTime checkIn = Convert.ToDateTime(dtReservation.Rows[0]["CheckIn"]);
                    DateTime checkOut = Convert.ToDateTime(dtReservation.Rows[0]["CheckOut"]);

                    lblCheckIn.Text = checkIn.ToString("dd MMM yyyy");
                    lblCheckOut.Text = checkOut.ToString("dd MMM yyyy");
                }
            }
            catch (Exception ex)
            {
                // Log error but don't show to user
                System.Diagnostics.Debug.WriteLine($"Error loading guest info: {ex.Message}");
            }
        }

        /// <summary>
        /// Load quick statistics
        /// </summary>
        private void LoadQuickStats()
        {
            try
            {
                // 1. Balance Due
                DataTable dtBalance = _guestPortalService.GetGuestBalance(_reservationId);
                if (dtBalance.Rows.Count > 0)
                {
                    decimal balanceDue = dtBalance.Rows[0]["Balance_Due"] != DBNull.Value
                        ? Convert.ToDecimal(dtBalance.Rows[0]["Balance_Due"]) : 0;
                    lblBalanceDue.Text = balanceDue.ToString("N0");
                }
                else
                {
                    lblBalanceDue.Text = "0";
                }

                // 2. Loyalty Points
                DataTable dtLoyalty = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT AvailablePoints FROM Customer_Loyalty
                      WHERE Customer_MobilePhone = @Phone",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@Phone", _guestMobilePhone }
                    });

                if (dtLoyalty.Rows.Count > 0)
                {
                    int points = dtLoyalty.Rows[0]["AvailablePoints"] != DBNull.Value
                        ? Convert.ToInt32(dtLoyalty.Rows[0]["AvailablePoints"]) : 0;
                    lblLoyaltyPoints.Text = points.ToString("N0");
                }
                else
                {
                    lblLoyaltyPoints.Text = "0";
                }

                // 3. Room Service Orders Count
                DataTable dtRSOrders = _guestPortalService.GetRoomServiceOrders(_reservationId);
                lblRSOrders.Text = dtRSOrders.Rows.Count.ToString();

                // 4. Pending Requests (Housekeeping + Concierge)
                var paramsRequests = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@Reservation_ID", _reservationId }
                };

                DataTable dtHousekeeping = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS Count FROM Guest_Housekeeping_Requests
                      WHERE Reservation_ID = @Reservation_ID
                        AND Request_Status IN ('PENDING', 'ASSIGNED', 'IN_PROGRESS')",
                    paramsRequests);

                DataTable dtConcierge = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS Count FROM Guest_Concierge_Requests
                      WHERE Reservation_ID = @Reservation_ID
                        AND Request_Status = 'PENDING'",
                    paramsRequests);

                int housekeepingCount = dtHousekeeping.Rows.Count > 0 ? Convert.ToInt32(dtHousekeeping.Rows[0][0]) : 0;
                int conciergeCount = dtConcierge.Rows.Count > 0 ? Convert.ToInt32(dtConcierge.Rows[0][0]) : 0;

                lblPendingRequests.Text = (housekeepingCount + conciergeCount).ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading quick stats: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle logout
        /// </summary>
        protected void btnLogout_Click(object sender, EventArgs e)
        {
            try
            {
                // Mark session as inactive
                if (!string.IsNullOrEmpty(_sessionToken))
                {
                    var parameters = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@Session_Token", _sessionToken }
                    };

                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Guest_Portal_Sessions
                          SET Is_Active = 0, Logout_Date = GETDATE()
                          WHERE Session_Token = @Session_Token",
                        parameters);
                }

                // Clear session
                Session.Clear();
                Session.Abandon();

                // Clear cookie
                if (Request.Cookies["GuestSession"] != null)
                {
                    HttpCookie sessionCookie = new HttpCookie("GuestSession")
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    };
                    Response.Cookies.Add(sessionCookie);
                }

                // Redirect to portal
                Response.Redirect("~/Guest/Portal");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during logout: {ex.Message}");
                // Still redirect to portal
                Response.Redirect("~/Guest/Portal");
            }
        }
    }
}
