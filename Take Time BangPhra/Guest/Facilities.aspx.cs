using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class Facilities : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private code _code;

        protected DataTable DtFacilities;

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
                LoadFacilities();
            }
        }

        private void LoadFacilities()
        {
            try
            {
                DtFacilities = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT * FROM Guest_Facilities
                      WHERE Status = 'True'
                      ORDER BY Category, Sort_Order, Name",
                    null);
            }
            catch
            {
                DtFacilities = new DataTable();
            }

            if (DtFacilities.Rows.Count > 0)
            {
                BindCategoryRepeater(rptFacilities, "facility");
                BindCategoryRepeater(rptAmenities, "amenity");
                BindCategoryRepeater(rptPolicies, "policy");
                pnlNoData.Visible = false;
            }
            else
            {
                pnlNoData.Visible = true;
            }
        }

        private void BindCategoryRepeater(System.Web.UI.WebControls.Repeater rpt, string category)
        {
            if (DtFacilities == null) return;
            DataRow[] rows = DtFacilities.Select("Category = '" + category + "'");
            if (rows.Length > 0)
            {
                DataTable dt = DtFacilities.Clone();
                foreach (DataRow row in rows) dt.ImportRow(row);
                rpt.DataSource = dt;
                rpt.DataBind();
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
