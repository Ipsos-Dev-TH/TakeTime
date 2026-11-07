using System;
using System.Data;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.Pricing
{
    public partial class DynamicPricing : Page
    {
        private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code _code;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.MgtReport)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (!Feature.Guard(this, "DynamicPricing", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            _code = new code();

            if (!IsPostBack)
            {
                LoadPricingData();
            }
        }

        private void LoadPricingData()
        {
            try
            {
                // Calculate today's occupancy
                DataTable dtOccupancy = _code.DatabaseQuerySafe(_connectionString,
                    @"DECLARE @TotalRooms INT = (SELECT COUNT(*) FROM Accommodation WHERE Status = 1);
                      DECLARE @OccupiedRooms INT = (
                          SELECT COUNT(DISTINCT ra.Accommodation_ID)
                          FROM Reservation r
                          INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                          WHERE CAST(GETDATE() AS DATE) BETWEEN CAST(r.CheckinDate AS DATE) AND CAST(r.CheckoutDate AS DATE)
                          AND r.Status NOT IN (N'ยกเลิก')
                      );
                      SELECT
                          @OccupiedRooms AS OccupiedRooms,
                          @TotalRooms AS TotalRooms,
                          CASE WHEN @TotalRooms > 0
                               THEN CAST(@OccupiedRooms * 100.0 / @TotalRooms AS DECIMAL(5,1))
                               ELSE 0
                          END AS OccupancyRate",
                    null);

                if (dtOccupancy.Rows.Count > 0)
                {
                    decimal occupancy = Convert.ToDecimal(dtOccupancy.Rows[0]["OccupancyRate"]);
                    lblTodayOccupancy.Text = occupancy.ToString("N1") + "%";

                    // Calculate multiplier based on occupancy
                    decimal multiplier = 1.00m;
                    string suggestion = "ราคาปกติ";

                    if (occupancy > 80)
                    {
                        multiplier = 1.20m;
                        suggestion = "แนะนำเพิ่มราคา +20%";
                    }
                    else if (occupancy < 40)
                    {
                        multiplier = 0.85m;
                        suggestion = "แนะนำลดราคา -15%";
                    }

                    lblCurrentMultiplier.Text = multiplier.ToString("N2");
                    lblSuggestedPrice.Text = suggestion;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading pricing data: {ex.Message}");
            }
        }
    }
}
