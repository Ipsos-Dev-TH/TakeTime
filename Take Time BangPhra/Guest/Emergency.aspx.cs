using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class Emergency : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private code _code;

        protected DataTable DtContacts;

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
                LoadContacts();
            }
        }

        private void LoadContacts()
        {
            try
            {
                DtContacts = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT * FROM Guest_Emergency_Contacts
                      WHERE Status = 'True'
                      ORDER BY Category, Sort_Order, Name",
                    null);
            }
            catch
            {
                DtContacts = new DataTable();
            }

            if (DtContacts.Rows.Count > 0)
            {
                // Bind category-specific repeaters
                BindCategoryRepeater(rptQuickDial, "quickdial");
                BindCategoryRepeater(rptHospitals, "hospital");
                BindCategoryRepeater(rptPolice, "police");
                BindCategoryRepeater(rptHotelServices, "hotel");
                BindCategoryRepeater(rptTransport, "transport");
                pnlNoData.Visible = false;
            }
            else
            {
                pnlNoData.Visible = true;
            }
        }

        private void BindCategoryRepeater(System.Web.UI.WebControls.Repeater rpt, string category)
        {
            if (DtContacts == null) return;
            DataRow[] rows = DtContacts.Select("Category = '" + category + "'");
            if (rows.Length > 0)
            {
                DataTable dt = DtContacts.Clone();
                foreach (DataRow row in rows) dt.ImportRow(row);
                rpt.DataSource = dt;
                rpt.DataBind();
            }
        }

        protected string GetCategoryLabel(string category)
        {
            switch (category)
            {
                case "quickdial": return "โทรด่วน";
                case "hospital": return "โรงพยาบาล";
                case "police": return "ตำรวจ / ฉุกเฉิน";
                case "hotel": return "บริการโรงแรม";
                case "transport": return "การเดินทาง";
                default: return category;
            }
        }

        protected string GetCategoryIcon(string category)
        {
            switch (category)
            {
                case "quickdial": return "fas fa-phone-alt";
                case "hospital": return "fas fa-hospital";
                case "police": return "fas fa-shield-alt";
                case "hotel": return "fas fa-concierge-bell";
                case "transport": return "fas fa-car";
                default: return "fas fa-info-circle";
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
