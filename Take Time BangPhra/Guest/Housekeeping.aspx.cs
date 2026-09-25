using System;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class Housekeeping : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private long _reservationId;
        private string _guestMobilePhone;
        private byte _accommodationId;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Housekeeping", "~/Guest/Dashboard")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            _guestPortalService = new GuestPortalService(_connectionString);

            // Check session
            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
            }

            if (!IsPostBack)
            {
                LoadRequestHistory();
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
            _accommodationId = Convert.ToByte(session["Accommodation_ID"]);

            return true;
        }

        /// <summary>
        /// Load request history
        /// </summary>
        private void LoadRequestHistory()
        {
            try
            {
                DataTable dtRequests = _guestPortalService.GetHousekeepingRequests(_reservationId);

                if (dtRequests.Rows.Count > 0)
                {
                    rptRequests.DataSource = dtRequests;
                    rptRequests.DataBind();
                    lblNoRequests.Visible = false;
                }
                else
                {
                    lblNoRequests.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading requests: {ex.Message}");
            }
        }

        /// <summary>
        /// Submit housekeeping request
        /// </summary>
        protected void btnSubmitRequest_Click(object sender, EventArgs e)
        {
            try
            {
                string serviceType = hfServiceType.Value;
                string description = txtDescription.Text.Trim();
                string priority = hfPriority.Value;
                string preferredTime = txtPreferredTime.Text.Trim();

                // Validation
                if (string.IsNullOrEmpty(serviceType))
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                        "alert('Please select a service type');", true);
                    return;
                }

                if (string.IsNullOrEmpty(description))
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                        "alert('Please describe your request');", true);
                    return;
                }

                // Create request
                long requestId = _guestPortalService.CreateHousekeepingRequest(
                    _reservationId,
                    _guestMobilePhone,
                    _accommodationId,
                    serviceType,
                    description,
                    priority,
                    string.IsNullOrEmpty(preferredTime) ? null : preferredTime);

                if (requestId > 0)
                {
                    // Clear form
                    txtDescription.Text = "";
                    txtPreferredTime.Text = "";
                    hfServiceType.Value = "";
                    hfPriority.Value = "NORMAL";

                    // Refresh history
                    LoadRequestHistory();

                    // Show success message and switch to history tab
                    ScriptManager.RegisterStartupScript(this, GetType(), "success",
                        @"alert('Request submitted successfully! Our staff will attend to it shortly.');
                          switchTab({currentTarget: document.querySelectorAll('.tab-btn')[1]}, 'history');",
                        true);
                }
                else
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                        "alert('Failed to submit request. Please try again.');", true);
                }
            }
            catch (Exception ex)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                    $"alert('Error: {ex.Message}');", true);
            }
        }
    }
}
