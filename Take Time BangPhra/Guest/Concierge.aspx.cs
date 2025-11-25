using System;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class Concierge : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TTBP"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private int _reservationId;
        private string _guestMobilePhone;
        private int _accommodationId;

        protected void Page_Load(object sender, EventArgs e)
        {
            _guestPortalService = new GuestPortalService(_connectionString);
            if (!ValidateGuestSession()) { Response.Redirect("~/Guest/Portal"); return; }
            if (!IsPostBack) LoadRequestHistory();
        }

        private bool ValidateGuestSession()
        {
            string sessionToken = Request.Cookies["GuestSession"]?.Value ?? Session["GuestSessionToken"]?.ToString();
            if (string.IsNullOrEmpty(sessionToken)) return false;
            DataTable dtSession = _guestPortalService.ValidateGuestSession(sessionToken);
            if (dtSession.Rows.Count == 0) return false;
            DataRow session = dtSession.Rows[0];
            _reservationId = Convert.ToInt32(session["Reservation_ID"]);
            _guestMobilePhone = session["Customer_MobilePhone"].ToString();
            _accommodationId = Convert.ToInt32(session["Accommodation_ID"]);
            return true;
        }

        private void LoadRequestHistory()
        {
            DataTable dtRequests = _guestPortalService.GetConciergeRequests(_reservationId);
            if (dtRequests.Rows.Count > 0) { rptRequests.DataSource = dtRequests; rptRequests.DataBind(); lblNoRequests.Visible = false; }
            else { lblNoRequests.Visible = true; }
        }

        protected void btnSubmitRequest_Click(object sender, EventArgs e)
        {
            string serviceType = hfServiceType.Value;
            string serviceName = txtServiceName.Text.Trim();
            string description = txtDescription.Text.Trim();
            if (string.IsNullOrEmpty(serviceType) || string.IsNullOrEmpty(serviceName) || string.IsNullOrEmpty(description))
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "alert", "alert('Please fill in all required fields');", true);
                return;
            }

            DateTime? preferredDate = null;
            if (!string.IsNullOrEmpty(txtPreferredDate.Text))
                preferredDate = Convert.ToDateTime(txtPreferredDate.Text);

            int? numberOfGuests = null;
            if (!string.IsNullOrEmpty(txtNumberOfGuests.Text))
                numberOfGuests = Convert.ToInt32(txtNumberOfGuests.Text);

            long requestId = _guestPortalService.CreateConciergeRequest(
                _reservationId, _guestMobilePhone, _accommodationId,
                serviceType, serviceName, description, preferredDate, null, numberOfGuests);

            if (requestId > 0)
            {
                txtServiceName.Text = "";
                txtDescription.Text = "";
                txtPreferredDate.Text = "";
                txtNumberOfGuests.Text = "1";
                hfServiceType.Value = "";
                LoadRequestHistory();
                ScriptManager.RegisterStartupScript(this, GetType(), "success",
                    @"alert('Request submitted successfully!');
                      switchTab({currentTarget: document.querySelectorAll('.tab-btn')[1]}, 'history');", true);
            }
        }
    }
}
