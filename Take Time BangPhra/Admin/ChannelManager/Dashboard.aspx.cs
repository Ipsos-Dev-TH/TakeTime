using System;
using System.Data;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.ChannelManager
{
    public partial class Dashboard : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "ChannelManager", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            _code = new code();

            if (!IsPostBack)
            {
                LoadChannelData();
            }
        }

        private void LoadChannelData()
        {
            try
            {
                // Get this month's booking stats
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        COUNT(*) AS TotalBookings,
                        ISNULL(SUM(TotalPrice), 0) AS TotalRevenue,
                        ISNULL(AVG(TotalPrice / NULLIF(DATEDIFF(day, CheckinDate, CheckoutDate), 0)), 0) AS AvgRate
                      FROM Reservation
                      WHERE MONTH(CheckinDate) = MONTH(GETDATE())
                      AND YEAR(CheckinDate) = YEAR(GETDATE())
                      AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')",
                    null);

                if (dt.Rows.Count > 0)
                {
                    lblDirectBookings.Text = dt.Rows[0]["TotalBookings"].ToString();
                    lblTotalBookings.Text = dt.Rows[0]["TotalBookings"].ToString();

                    decimal revenue = Convert.ToDecimal(dt.Rows[0]["TotalRevenue"]);
                    lblDirectRevenue.Text = (revenue / 1000).ToString("N0") + "K";
                    lblTotalRevenue.Text = revenue.ToString("N0");

                    lblAvgRate.Text = Convert.ToDecimal(dt.Rows[0]["AvgRate"]).ToString("N0");
                }

                // Calculate occupancy rate
                DataTable dtOccupancy = _code.DatabaseQuerySafe(_connectionString,
                    @"DECLARE @TotalRooms INT = (SELECT COUNT(*) FROM Accommodation WHERE Status = 1);
                      DECLARE @DaysInMonth INT = DAY(EOMONTH(GETDATE()));
                      DECLARE @BookedNights INT = (
                          SELECT ISNULL(SUM(DATEDIFF(day, CheckinDate, CheckoutDate)), 0)
                          FROM Reservation
                          WHERE MONTH(CheckinDate) = MONTH(GETDATE())
                          AND Status NOT IN (N'ยกเลิก')
                      );
                      SELECT
                          CASE WHEN @TotalRooms * @DaysInMonth > 0
                               THEN CAST(@BookedNights * 100.0 / (@TotalRooms * @DaysInMonth) AS DECIMAL(5,1))
                               ELSE 0
                          END AS OccupancyRate",
                    null);

                if (dtOccupancy.Rows.Count > 0)
                {
                    lblOccupancy.Text = dtOccupancy.Rows[0]["OccupancyRate"].ToString() + "%";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading channel data: {ex.Message}");
            }
        }
    }
}
