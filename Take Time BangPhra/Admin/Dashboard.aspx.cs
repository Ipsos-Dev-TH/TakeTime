using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.Admin
{
    public partial class Dashboard : Page
    {
        private readonly string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private code codeInstance = new code();

        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                // Check permission
                if (Session["permission"]?.ToString() != "True" ||
                    (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
                {
                    Response.Redirect("/Default");
                    return;
                }

                if (!IsPostBack)
                {
                    LoadDashboardData();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Dashboard Error: {ex.Message}");
                // Redirect to default page on error
                Response.Redirect("/Default");
            }
        }

        private void LoadDashboardData()
        {
            try
            {
                // Set username and last update
                litUsername.Text = Session["User"]?.ToString() ?? "ผู้ใช้งาน";
                litLastUpdate.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm", new CultureInfo("th-TH"));

                // Load Today's Stats
                LoadTodayStats();

                // Load This Month Stats
                LoadMonthStats();

                // Load Charts Data
                LoadRevenueTrendChart();
                LoadPaymentMethodChart();
                LoadTopAccommodationsChart();

                // Load Alerts
                LoadActionAlerts();

                System.Diagnostics.Debug.WriteLine($"✅ Dashboard loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadDashboardData Error: {ex.Message}");
                // Show error message
                litRevenueToday.Text = "N/A";
                litBookingsToday.Text = "N/A";
            }
        }

        #region Today's Stats

        private void LoadTodayStats()
        {
            try
            {
                DateTime today = DateTime.Today;
                DateTime yesterday = DateTime.Today.AddDays(-1);

                // 1. Revenue Today
                decimal revenueToday = GetRevenue(today, today);
                decimal revenueYesterday = GetRevenue(yesterday, yesterday);

                litRevenueToday.Text = revenueToday.ToString("N0");

                // Calculate trend
                if (revenueYesterday > 0)
                {
                    decimal percentChange = ((revenueToday - revenueYesterday) / revenueYesterday) * 100;
                    string trendClass = percentChange >= 0 ? "trend-up" : "trend-down";
                    string trendIcon = percentChange >= 0 ? "↑" : "↓";

                    litRevenueTrend.Text = $"<span class='{trendClass}'>{trendIcon} {Math.Abs(percentChange):N1}% vs เมื่อวาน</span>";
                }
                else
                {
                    litRevenueTrend.Text = "<span class='trend-neutral'>ไม่มีข้อมูลเปรียบเทียบ</span>";
                }

                // 2. Bookings Today
                int bookingsToday = GetBookingsCount(today, today);
                litBookingsToday.Text = bookingsToday.ToString();

                // Check-ins and Check-outs today
                int checkInsToday = GetCheckInsCount(today);
                int checkOutsToday = GetCheckOutsCount(today);

                litCheckIns.Text = checkInsToday.ToString();
                litCheckOuts.Text = checkOutsToday.ToString();

                // 3. Occupancy Rate
                var occupancyData = GetOccupancyRate(today);
                litOccupancyRate.Text = occupancyData.Item1.ToString("N0");
                litOccupancyDetails.Text = $"{occupancyData.Item2} / {occupancyData.Item3} ห้องถูกจอง";

                System.Diagnostics.Debug.WriteLine($"📊 Today Stats: Revenue=฿{revenueToday:N0}, Bookings={bookingsToday}, Occupancy={occupancyData.Item1}%");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadTodayStats Error: {ex.Message}");
                litRevenueToday.Text = "0";
                litBookingsToday.Text = "0";
                litOccupancyRate.Text = "0";
            }
        }

        private void LoadMonthStats()
        {
            try
            {
                DateTime startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                DateTime endOfMonth = DateTime.Today;

                // Net Profit = Revenue - Expenses
                decimal revenue = GetRevenue(startOfMonth, endOfMonth);
                decimal expenses = GetExpenses(startOfMonth, endOfMonth);
                decimal netProfit = revenue - expenses;

                litNetProfit.Text = netProfit.ToString("N0");

                // Profit Margin
                if (revenue > 0)
                {
                    decimal profitMargin = (netProfit / revenue) * 100;
                    string profitClass = netProfit >= 0 ? "trend-up" : "trend-down";

                    litProfitMargin.Text = $"<span class='{profitClass}'>Margin: {profitMargin:N1}%</span>";
                }
                else
                {
                    litProfitMargin.Text = "<span class='trend-neutral'>N/A</span>";
                }

                System.Diagnostics.Debug.WriteLine($"💹 Month Stats: Revenue=฿{revenue:N0}, Expenses=฿{expenses:N0}, Net=฿{netProfit:N0}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadMonthStats Error: {ex.Message}");
                litNetProfit.Text = "0";
            }
        }

        #endregion

        #region Charts Data

        private void LoadRevenueTrendChart()
        {
            try
            {
                // Get revenue for last 30 days
                List<string> chartData = new List<string>();

                for (int i = 29; i >= 0; i--)
                {
                    DateTime date = DateTime.Today.AddDays(-i);
                    decimal revenue = GetRevenue(date, date);

                    // Format as [timestamp, value]
                    long timestamp = new DateTimeOffset(date).ToUnixTimeMilliseconds();
                    chartData.Add($"[{timestamp}, {revenue}]");
                }

                hfRevenueTrendData.Value = "[" + string.Join(",", chartData) + "]";

                System.Diagnostics.Debug.WriteLine($"📈 Revenue Trend Chart: {chartData.Count} data points");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadRevenueTrendChart Error: {ex.Message}");
                hfRevenueTrendData.Value = "[]";
            }
        }

        private void LoadPaymentMethodChart()
        {
            try
            {
                DateTime startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                DateTime endOfMonth = DateTime.Today;

                // Get revenue by payment method
                decimal cash = GetRevenueByPaymentMethod(startOfMonth, endOfMonth, "เงินสด");
                decimal kbank = GetRevenueByPaymentMethod(startOfMonth, endOfMonth, "กสิกร");
                decimal ktb = GetRevenueByPaymentMethod(startOfMonth, endOfMonth, "กรุงไทย");
                decimal director = GetRevenueByPaymentMethod(startOfMonth, endOfMonth, "กรรมการ");

                hfPaymentMethodData.Value = $"[{cash}, {kbank}, {ktb}, {director}]";

                System.Diagnostics.Debug.WriteLine($"💳 Payment Method: Cash={cash}, KBANK={kbank}, KTB={ktb}, Director={director}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadPaymentMethodChart Error: {ex.Message}");
                hfPaymentMethodData.Value = "[0,0,0,0]";
            }
        }

        private void LoadTopAccommodationsChart()
        {
            try
            {
                DateTime startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                DateTime endOfMonth = DateTime.Today;

                // Get bookings count by accommodation type
                var data = GetTopAccommodations(startOfMonth, endOfMonth, 5);

                // Format as array
                hfTopAccommodationsData.Value = "[" + string.Join(",", data) + "]";

                System.Diagnostics.Debug.WriteLine($"🏆 Top Accommodations: {string.Join(", ", data)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadTopAccommodationsChart Error: {ex.Message}");
                hfTopAccommodationsData.Value = "[0,0,0,0,0]";
            }
        }

        #endregion

        #region Action Alerts

        private void LoadActionAlerts()
        {
            try
            {
                List<AlertItem> alerts = new List<AlertItem>();

                // 1. Check for outstanding payments
                int outstandingCount = GetOutstandingPaymentsCount();
                if (outstandingCount > 0)
                {
                    alerts.Add(new AlertItem
                    {
                        Type = "warning",
                        Icon = "⚠️",
                        Title = "ยอดค้างชำระ",
                        Description = $"มีลูกค้า {outstandingCount} ราย ที่มียอดค้างชำระ",
                        ActionUrl = "/ReserveTable"
                    });
                }

                // 2. Check for low stock items
                int lowStockCount = GetLowStockItemsCount();
                if (lowStockCount > 0)
                {
                    alerts.Add(new AlertItem
                    {
                        Type = "danger",
                        Icon = "📦",
                        Title = "สินค้าใกล้หมด",
                        Description = $"มีสินค้า {lowStockCount} รายการที่สต็อกเหลือน้อย",
                        ActionUrl = "/Product/Stock"
                    });
                }

                // 3. Check today's operations
                int todayCheckIns = GetCheckInsCount(DateTime.Today);
                int todayCheckOuts = GetCheckOutsCount(DateTime.Today);
                if (todayCheckIns > 0 || todayCheckOuts > 0)
                {
                    alerts.Add(new AlertItem
                    {
                        Type = "info",
                        Icon = "📋",
                        Title = "งานประจำวัน",
                        Description = $"วันนี้มี {todayCheckIns} เช็คอิน และ {todayCheckOuts} เช็คเอาท์",
                        ActionUrl = "/ReserveTable"
                    });
                }

                // Bind alerts
                if (alerts.Count > 0)
                {
                    rptAlerts.DataSource = alerts;
                    rptAlerts.DataBind();
                    litNoAlerts.Visible = false;
                }
                else
                {
                    rptAlerts.DataSource = null;
                    rptAlerts.DataBind();
                    litNoAlerts.Visible = true;
                }

                System.Diagnostics.Debug.WriteLine($"🔔 Alerts: {alerts.Count} items");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadActionAlerts Error: {ex.Message}");
                litNoAlerts.Visible = true;
            }
        }

        #endregion

        #region Helper Methods

        private decimal GetRevenue(DateTime startDate, DateTime endDate)
        {
            try
            {
                string query = @"
                    SELECT ISNULL(SUM(Total_Amount), 0) as TotalRevenue
                    FROM Account_Receipt
                    WHERE CAST(Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                      AND CAST(Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                      AND Status = 'Normal'";

                var parameters = new Dictionary<string, object>
                {
                    { "@StartDate", startDate },
                    { "@EndDate", endDate }
                };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                if (dt != null && dt.Rows.Count > 0)
                {
                    return Convert.ToDecimal(dt.Rows[0]["TotalRevenue"]);
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetRevenue Error: {ex.Message}");
                return 0;
            }
        }

        private decimal GetExpenses(DateTime startDate, DateTime endDate)
        {
            try
            {
                string query = @"
                    SELECT ISNULL(SUM(Total_Amount), 0) as TotalExpenses
                    FROM Account_Payment
                    WHERE CAST(Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                      AND CAST(Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                      AND Status = 'Normal'";

                var parameters = new Dictionary<string, object>
                {
                    { "@StartDate", startDate },
                    { "@EndDate", endDate }
                };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                if (dt != null && dt.Rows.Count > 0)
                {
                    return Convert.ToDecimal(dt.Rows[0]["TotalExpenses"]);
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetExpenses Error: {ex.Message}");
                return 0;
            }
        }

        private int GetBookingsCount(DateTime startDate, DateTime endDate)
        {
            try
            {
                string query = @"
                    SELECT COUNT(*) as BookingCount
                    FROM Reservation
                    WHERE CAST(Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                      AND CAST(Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                      AND Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')";

                var parameters = new Dictionary<string, object>
                {
                    { "@StartDate", startDate },
                    { "@EndDate", endDate }
                };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                if (dt != null && dt.Rows.Count > 0)
                {
                    return Convert.ToInt32(dt.Rows[0]["BookingCount"]);
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetBookingsCount Error: {ex.Message}");
                return 0;
            }
        }

        private int GetCheckInsCount(DateTime date)
        {
            try
            {
                string query = @"
                    SELECT COUNT(*) as CheckInCount
                    FROM Reservation
                    WHERE CAST(CheckinDate AS DATE) = CAST(@Date AS DATE)
                      AND Status IN (N'เช็คอินแล้ว', N'เช็คเอาท์แล้ว', N'เสร็จสิ้น')";

                var parameters = new Dictionary<string, object> { { "@Date", date } };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                if (dt != null && dt.Rows.Count > 0)
                {
                    return Convert.ToInt32(dt.Rows[0]["CheckInCount"]);
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetCheckInsCount Error: {ex.Message}");
                return 0;
            }
        }

        private int GetCheckOutsCount(DateTime date)
        {
            try
            {
                string query = @"
                    SELECT COUNT(*) as CheckOutCount
                    FROM Reservation
                    WHERE CAST(CheckoutDate AS DATE) = CAST(@Date AS DATE)
                      AND Status IN (N'เช็คเอาท์แล้ว', N'เสร็จสิ้น')";

                var parameters = new Dictionary<string, object> { { "@Date", date } };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                if (dt != null && dt.Rows.Count > 0)
                {
                    return Convert.ToInt32(dt.Rows[0]["CheckOutCount"]);
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetCheckOutsCount Error: {ex.Message}");
                return 0;
            }
        }

        private Tuple<decimal, int, int> GetOccupancyRate(DateTime date)
        {
            try
            {
                // Total accommodations (assuming fixed number - can be queried from DB)
                int totalRooms = 20; // Update this based on actual data

                // Get occupied rooms for the date
                string query = @"
                    SELECT COUNT(DISTINCT ra.Accommodation_ID) as OccupiedRooms
                    FROM Reservation r
                    INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                    WHERE CAST(@Date AS DATE) >= CAST(r.CheckinDate AS DATE)
                      AND CAST(@Date AS DATE) < CAST(r.CheckoutDate AS DATE)
                      AND r.Status IN (N'เช็คอินแล้ว', N'เช็คเอาท์แล้ว', N'เสร็จสิ้น')";

                var parameters = new Dictionary<string, object> { { "@Date", date } };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                int occupiedRooms = 0;
                if (dt != null && dt.Rows.Count > 0)
                {
                    occupiedRooms = Convert.ToInt32(dt.Rows[0]["OccupiedRooms"]);
                }

                decimal occupancyRate = totalRooms > 0 ? (decimal)occupiedRooms / totalRooms * 100 : 0;

                return new Tuple<decimal, int, int>(occupancyRate, occupiedRooms, totalRooms);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetOccupancyRate Error: {ex.Message}");
                return new Tuple<decimal, int, int>(0, 0, 0);
            }
        }

        private decimal GetRevenueByPaymentMethod(DateTime startDate, DateTime endDate, string paymentMethod)
        {
            try
            {
                string query = @"
                    SELECT ISNULL(SUM(Total_Amount), 0) as Revenue
                    FROM Account_Receipt
                    WHERE CAST(Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                      AND CAST(Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                      AND Status = 'Normal'
                      AND Paid_Type LIKE N'%' + @PaymentMethod + '%'";

                var parameters = new Dictionary<string, object>
                {
                    { "@StartDate", startDate },
                    { "@EndDate", endDate },
                    { "@PaymentMethod", paymentMethod }
                };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                if (dt != null && dt.Rows.Count > 0)
                {
                    return Convert.ToDecimal(dt.Rows[0]["Revenue"]);
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetRevenueByPaymentMethod Error: {ex.Message}");
                return 0;
            }
        }

        private List<int> GetTopAccommodations(DateTime startDate, DateTime endDate, int topN)
        {
            try
            {
                // Simplified: Return placeholder data
                // TODO: Implement actual query based on accommodation types
                return new List<int> { 15, 12, 10, 8, 5 };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetTopAccommodations Error: {ex.Message}");
                return new List<int> { 0, 0, 0, 0, 0 };
            }
        }

        private int GetOutstandingPaymentsCount()
        {
            try
            {
                string query = @"
                    SELECT COUNT(*) as OutstandingCount
                    FROM Reservation
                    WHERE (TotalPrice - ISNULL(Deposit, 0)) > 0
                      AND Status IN (N'มัดจำแล้ว', N'เช็คอินแล้ว')";

                DataTable dt = codeInstance.DatabaseQuery(conn, query);

                if (dt != null && dt.Rows.Count > 0)
                {
                    return Convert.ToInt32(dt.Rows[0]["OutstandingCount"]);
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetOutstandingPaymentsCount Error: {ex.Message}");
                return 0;
            }
        }

        private int GetLowStockItemsCount()
        {
            try
            {
                // Low stock threshold: < 5 units
                string query = @"
                    SELECT COUNT(*) as LowStockCount
                    FROM (
                        SELECT p.ID,
                               ISNULL(SUM(pi.Amount), 0) - ISNULL(SUM(po.Amount), 0) as StockLevel
                        FROM Product p
                        LEFT JOIN Product_In pi ON p.ID = pi.Product_ID
                        LEFT JOIN Product_Out po ON p.ID = po.Product_ID
                        WHERE p.Status = 'True'
                        GROUP BY p.ID
                        HAVING ISNULL(SUM(pi.Amount), 0) - ISNULL(SUM(po.Amount), 0) < 5
                          AND ISNULL(SUM(pi.Amount), 0) - ISNULL(SUM(po.Amount), 0) > 0
                    ) as LowStockItems";

                DataTable dt = codeInstance.DatabaseQuery(conn, query);

                if (dt != null && dt.Rows.Count > 0)
                {
                    return Convert.ToInt32(dt.Rows[0]["LowStockCount"]);
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetLowStockItemsCount Error: {ex.Message}");
                return 0;
            }
        }

        #endregion

        #region Alert Item Class

        public class AlertItem
        {
            public string Type { get; set; }
            public string Icon { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string ActionUrl { get; set; }
        }

        #endregion
    }
}
