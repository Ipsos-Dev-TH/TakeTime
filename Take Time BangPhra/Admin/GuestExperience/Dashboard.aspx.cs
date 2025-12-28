using System;
using System.Data;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.GuestExperience
{
    public partial class Dashboard : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            _code = new code();

            if (!IsPostBack)
            {
                LoadExperienceData();
            }
        }

        private void LoadExperienceData()
        {
            try
            {
                // Load loyalty members count
                DataTable dtLoyalty = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS MemberCount
                      FROM Customer
                      WHERE ISNULL(Loyalty_Points, 0) > 0",
                    null);

                if (dtLoyalty.Rows.Count > 0)
                {
                    lblLoyaltyMembers.Text = dtLoyalty.Rows[0]["MemberCount"].ToString();
                }

                // Load returning guest percentage
                DataTable dtReturning = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        CASE WHEN TotalGuests > 0
                             THEN CAST(ReturningGuests * 100.0 / TotalGuests AS DECIMAL(5,1))
                             ELSE 0
                        END AS ReturningPercent
                      FROM (
                          SELECT
                              COUNT(DISTINCT Customer_MobilePhone) AS TotalGuests,
                              SUM(CASE WHEN BookingCount > 1 THEN 1 ELSE 0 END) AS ReturningGuests
                          FROM (
                              SELECT Customer_MobilePhone, COUNT(*) AS BookingCount
                              FROM Reservation
                              WHERE Status NOT IN (N'ยกเลิก')
                              GROUP BY Customer_MobilePhone
                          ) t
                      ) x",
                    null);

                if (dtReturning.Rows.Count > 0)
                {
                    lblReturning.Text = dtReturning.Rows[0]["ReturningPercent"].ToString() + "%";
                }

                // Load pending messages
                DataTable dtMessages = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS PendingCount
                      FROM Guest_Chat
                      WHERE IsFromGuest = 1 AND (Is_Read = 0 OR Is_Read IS NULL)",
                    null);

                if (dtMessages.Rows.Count > 0)
                {
                    lblPendingMessages.Text = dtMessages.Rows[0]["PendingCount"].ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading experience data: {ex.Message}");
            }
        }
    }
}
