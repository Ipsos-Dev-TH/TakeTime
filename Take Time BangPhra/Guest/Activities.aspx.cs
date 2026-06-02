using System;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class Activities : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private code _code;

        protected void Page_Load(object sender, EventArgs e)
        {
            _guestPortalService = new GuestPortalService(_connectionString);
            _code = new code();

            if (!ValidateGuestSession())
            {
                Response.Redirect("~/Guest/Portal");
                return;
            }

            if (!IsPostBack)
            {
                LoadActivities();
            }
        }

        private bool ValidateGuestSession()
        {
            string sessionToken = Request.Cookies["GuestSession"]?.Value ?? Session["GuestSessionToken"]?.ToString();
            if (string.IsNullOrEmpty(sessionToken))
                return false;

            DataTable dtSession = _guestPortalService.ValidateGuestSession(sessionToken);
            return dtSession.Rows.Count > 0;
        }

        private void LoadActivities()
        {
            try
            {
                bool tableExists = false;
                try
                {
                    DataTable dtCheck = _code.DatabaseQuerySafe(_connectionString,
                        "SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Property_Activities') AND type = 'U'", null);
                    tableExists = dtCheck != null && dtCheck.Rows.Count > 0;
                }
                catch { }

                if (!tableExists)
                {
                    lblNoOnsite.Visible = true;
                    lblNoOffsite.Visible = true;
                    return;
                }

                DataTable dtOnsite = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID, ActivityName, Description, ImagePath, Price, Duration, Location, ContactInfo, MapUrl
                      FROM Property_Activities
                      WHERE Category = 'ON_PROPERTY' AND IsActive = 1
                      ORDER BY DisplayOrder, ActivityName", null);

                if (dtOnsite.Rows.Count > 0)
                {
                    rptOnsiteActivities.DataSource = dtOnsite;
                    rptOnsiteActivities.DataBind();
                    lblOnsiteCount.Text = dtOnsite.Rows.Count.ToString();
                }
                else
                {
                    lblNoOnsite.Visible = true;
                }

                DataTable dtOffsite = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID, ActivityName, Description, ImagePath, Price, Duration, Location, ContactInfo, MapUrl
                      FROM Property_Activities
                      WHERE Category = 'OFF_PROPERTY' AND IsActive = 1
                      ORDER BY DisplayOrder, ActivityName", null);

                if (dtOffsite.Rows.Count > 0)
                {
                    rptOffsiteActivities.DataSource = dtOffsite;
                    rptOffsiteActivities.DataBind();
                    lblOffsiteCount.Text = dtOffsite.Rows.Count.ToString();
                }
                else
                {
                    lblNoOffsite.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading activities: {ex.Message}");
                lblNoOnsite.Visible = true;
                lblNoOffsite.Visible = true;
            }
        }
    }
}
