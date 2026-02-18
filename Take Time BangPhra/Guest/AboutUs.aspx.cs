using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class AboutUs : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private code _code;

        protected DataTable DtSections;

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
                LoadSections();
            }
        }

        private void LoadSections()
        {
            try
            {
                DtSections = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT * FROM Guest_AboutUs_Sections
                      WHERE Status = 'True'
                      ORDER BY Section_Type, Sort_Order",
                    null);
            }
            catch
            {
                DtSections = new DataTable();
            }

            if (DtSections.Rows.Count > 0)
            {
                BindSectionRepeater(rptValues, "values");
                BindSectionRepeater(rptTimeline, "timeline");
                BindSectionRepeater(rptConcepts, "concept");
                pnlNoData.Visible = false;
            }
            else
            {
                pnlNoData.Visible = true;
            }
        }

        private void BindSectionRepeater(System.Web.UI.WebControls.Repeater rpt, string sectionType)
        {
            if (DtSections == null) return;
            DataRow[] rows = DtSections.Select("Section_Type = '" + sectionType + "'");
            if (rows.Length > 0)
            {
                DataTable dt = DtSections.Clone();
                foreach (DataRow row in rows) dt.ImportRow(row);
                rpt.DataSource = dt;
                rpt.DataBind();
            }
        }

        protected DataRow GetSingleSection(string sectionType)
        {
            if (DtSections == null || DtSections.Rows.Count == 0) return null;
            DataRow[] rows = DtSections.Select("Section_Type = '" + sectionType + "'");
            return rows.Length > 0 ? rows[0] : null;
        }

        protected string GetSectionValue(string sectionType, string column)
        {
            DataRow row = GetSingleSection(sectionType);
            if (row == null) return "";
            return row[column]?.ToString() ?? "";
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
