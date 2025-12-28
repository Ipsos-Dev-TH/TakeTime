using System;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class AboutUs : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;

        protected void Page_Load(object sender, EventArgs e)
        {
            _guestPortalService = new GuestPortalService(_connectionString);

            // Check session
            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
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

            return true;
        }
    }
}
