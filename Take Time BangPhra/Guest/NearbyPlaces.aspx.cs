using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Guest
{
    public partial class NearbyPlaces : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private GuestPortalService _guestPortalService;
        private code _code;

        protected DataTable DtPlaces;
        protected string[] Categories = { "beach", "restaurant", "cafe", "attraction", "shopping" };

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
                LoadPlaces();
            }
        }

        private void LoadPlaces()
        {
            try
            {
                DtPlaces = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT * FROM Guest_NearbyPlaces
                      WHERE Status = 'True'
                      ORDER BY Category, Sort_Order, Name",
                    null);
            }
            catch
            {
                DtPlaces = new DataTable();
            }

            if (DtPlaces.Rows.Count > 0)
            {
                rptPlaces.DataSource = DtPlaces;
                rptPlaces.DataBind();
                pnlNoData.Visible = false;
            }
            else
            {
                pnlNoData.Visible = true;
            }
        }

        protected DataRow[] GetPlacesByCategory(string category)
        {
            if (DtPlaces == null || DtPlaces.Rows.Count == 0) return new DataRow[0];
            return DtPlaces.Select("Category = '" + category.Replace("'", "''") + "'");
        }

        protected bool HasPlacesInCategory(string category)
        {
            return GetPlacesByCategory(category).Length > 0;
        }

        protected string GetCategoryLabel(string category)
        {
            switch (category)
            {
                case "beach": return "ชายหาด";
                case "restaurant": return "ร้านอาหาร";
                case "cafe": return "คาเฟ่";
                case "attraction": return "สถานที่ท่องเที่ยว";
                case "shopping": return "ช้อปปิ้ง";
                default: return category;
            }
        }

        protected string GetCategoryIcon(string category)
        {
            switch (category)
            {
                case "beach": return "🏖️";
                case "restaurant": return "🍽️";
                case "cafe": return "☕";
                case "attraction": return "🎯";
                case "shopping": return "🛒";
                default: return "📍";
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
