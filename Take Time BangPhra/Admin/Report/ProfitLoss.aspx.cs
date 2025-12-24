using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using Take_Time_BangPhra;
using Newtonsoft.Json;

namespace Take_Time_BangPhra.Admin.Report
{
    public partial class ProfitLoss : System.Web.UI.Page
    {
        code codeInstance = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            // Check permissions
            try
            {
                if (Session["permission"]?.ToString() != "True" ||
                    (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
                {
                    Response.Redirect("/Default");
                    return;
                }
            }
            catch
            {
                Response.Redirect("/Default");
                return;
            }

            if (!IsPostBack)
            {
                InitializeFilters();
                LoadPLStatement();
            }
        }

        private void InitializeFilters()
        {
            // Populate Year dropdown
            ddlYear.Items.Clear();
            int currentYear = DateTime.Now.Year;
            for (int year = currentYear; year >= currentYear - 5; year--)
            {
                ddlYear.Items.Add(new ListItem(year.ToString(), year.ToString()));
            }
            ddlYear.SelectedValue = currentYear.ToString();

            // Populate Month dropdown
            ddlMonth.Items.Clear();
            ddlMonth.Items.Add(new ListItem("มกราคม", "1"));
            ddlMonth.Items.Add(new ListItem("กุมภาพันธ์", "2"));
            ddlMonth.Items.Add(new ListItem("มีนาคม", "3"));
            ddlMonth.Items.Add(new ListItem("เมษายน", "4"));
            ddlMonth.Items.Add(new ListItem("พฤษภาคม", "5"));
            ddlMonth.Items.Add(new ListItem("มิถุนายน", "6"));
            ddlMonth.Items.Add(new ListItem("กรกฎาคม", "7"));
            ddlMonth.Items.Add(new ListItem("สิงหาคม", "8"));
            ddlMonth.Items.Add(new ListItem("กันยายน", "9"));
            ddlMonth.Items.Add(new ListItem("ตุลาคม", "10"));
            ddlMonth.Items.Add(new ListItem("พฤศจิกายน", "11"));
            ddlMonth.Items.Add(new ListItem("ธันวาคม", "12"));
            ddlMonth.SelectedValue = DateTime.Now.Month.ToString();
        }

        protected void ddlPeriodType_SelectedIndexChanged(object sender, EventArgs e)
        {
            string periodType = ddlPeriodType.SelectedValue;

            // Show/hide controls based on period type
            divMonth.Visible = (periodType == "MONTH");
            divQuarter.Visible = (periodType == "QUARTER");
            divYear.Visible = true;

            LoadPLStatement();
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadPLStatement();
        }

        protected void btnExport_Click(object sender, EventArgs e)
        {
            ExportToExcel();
        }

        private void LoadPLStatement()
        {
            string periodType = ddlPeriodType.SelectedValue;
            DateTime startDate, endDate;
            string periodDisplay = "";

            GetDateRange(out startDate, out endDate, out periodDisplay);

            // Display period
            litPeriodDisplay.Text = $"<div style='margin-bottom: 20px; color: #718096; font-size: 15px;'><strong>ช่วงเวลา:</strong> {periodDisplay}</div>";

            // Calculate P&L components
            decimal totalRevenue = CalculateTotalRevenue(startDate, endDate);
            decimal accomRevenue = GetAccommodationRevenue(startDate, endDate);
            decimal productRevenue = GetProductRevenue(startDate, endDate);
            decimal serviceRevenue = GetServiceRevenue(startDate, endDate);
            decimal otherRevenue = totalRevenue - accomRevenue - productRevenue - serviceRevenue;

            decimal totalCOGS = CalculateCOGS(startDate, endDate);
            decimal grossProfit = totalRevenue - totalCOGS;

            decimal salaryExpense = GetExpenseByType(startDate, endDate, "เงินเดือนพนักงาน");
            decimal utilityExpense = GetExpenseByType(startDate, endDate, "ค่าสาธารณูปโภค");
            decimal maintenanceExpense = GetExpenseByType(startDate, endDate, "ค่าซ่อมบำรุง");
            decimal otherExpense = GetOtherExpenses(startDate, endDate, new[] { "เงินเดือนพนักงาน", "ค่าสาธารณูปโภค", "ค่าซ่อมบำรุง" });
            decimal totalExpenses = salaryExpense + utilityExpense + maintenanceExpense + otherExpense;

            decimal netProfit = grossProfit - totalExpenses;

            // Summary Cards
            litTotalRevenue.Text = totalRevenue.ToString("N2");
            litTotalCOGS.Text = totalCOGS.ToString("N2");
            litGrossProfit.Text = grossProfit.ToString("N2");
            litTotalExpenses.Text = totalExpenses.ToString("N2");
            litNetProfit.Text = netProfit.ToString("N2");

            // Percentages for summary cards
            decimal cogsPercent = totalRevenue > 0 ? (totalCOGS / totalRevenue) * 100 : 0;
            decimal grossMargin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0;
            decimal expensesPercent = totalRevenue > 0 ? (totalExpenses / totalRevenue) * 100 : 0;
            decimal netMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0;

            litCOGSPercent.Text = $"{cogsPercent:N2}% ของรายได้";
            litGrossMargin.Text = $"{grossMargin:N2}%";
            litExpensesPercent.Text = $"{expensesPercent:N2}% ของรายได้";
            litNetMargin.Text = $"{netMargin:N2}%";

            // Trend (compare with previous period)
            DateTime prevStartDate, prevEndDate;
            GetPreviousPeriodDateRange(startDate, endDate, out prevStartDate, out prevEndDate);
            decimal prevRevenue = CalculateTotalRevenue(prevStartDate, prevEndDate);

            if (prevRevenue > 0)
            {
                decimal revenueChange = ((totalRevenue - prevRevenue) / prevRevenue) * 100;
                string trendClass = revenueChange >= 0 ? "positive" : "negative";
                string trendIcon = revenueChange >= 0 ? "↑" : "↓";
                litRevenueTrend.Text = $"<span class='{trendClass}'>{trendIcon} {Math.Abs(revenueChange):N2}% จากช่วงก่อนหน้า</span>";
            }
            else
            {
                litRevenueTrend.Text = "<span style='color: #718096;'>ไม่มีข้อมูลเปรียบเทียบ</span>";
            }

            // Detailed P&L Statement
            litTotalRevenueDetail.Text = totalRevenue.ToString("N2");
            litAccomRevenue.Text = accomRevenue.ToString("N2");
            litProductRevenue.Text = productRevenue.ToString("N2");
            litServiceRevenue.Text = serviceRevenue.ToString("N2");
            litOtherRevenue.Text = otherRevenue.ToString("N2");

            litAccomRevenuePercent.Text = totalRevenue > 0 ? $"{(accomRevenue / totalRevenue) * 100:N2}%" : "0.00%";
            litProductRevenuePercent.Text = totalRevenue > 0 ? $"{(productRevenue / totalRevenue) * 100:N2}%" : "0.00%";
            litServiceRevenuePercent.Text = totalRevenue > 0 ? $"{(serviceRevenue / totalRevenue) * 100:N2}%" : "0.00%";
            litOtherRevenuePercent.Text = totalRevenue > 0 ? $"{(otherRevenue / totalRevenue) * 100:N2}%" : "0.00%";

            litProductCOGS.Text = totalCOGS.ToString("N2");
            litTotalCOGSDetail.Text = totalCOGS.ToString("N2");
            litProductCOGSPercent.Text = totalRevenue > 0 ? $"{cogsPercent:N2}%" : "0.00%";
            litTotalCOGSPercentDetail.Text = totalRevenue > 0 ? $"{cogsPercent:N2}%" : "0.00%";

            litGrossProfitDetail.Text = grossProfit.ToString("N2");
            litGrossProfitPercent.Text = $"{grossMargin:N2}%";

            litSalaryExpense.Text = salaryExpense.ToString("N2");
            litUtilityExpense.Text = utilityExpense.ToString("N2");
            litMaintenanceExpense.Text = maintenanceExpense.ToString("N2");
            litOtherExpense.Text = otherExpense.ToString("N2");
            litTotalExpensesDetail.Text = totalExpenses.ToString("N2");

            litSalaryExpensePercent.Text = totalRevenue > 0 ? $"{(salaryExpense / totalRevenue) * 100:N2}%" : "0.00%";
            litUtilityExpensePercent.Text = totalRevenue > 0 ? $"{(utilityExpense / totalRevenue) * 100:N2}%" : "0.00%";
            litMaintenanceExpensePercent.Text = totalRevenue > 0 ? $"{(maintenanceExpense / totalRevenue) * 100:N2}%" : "0.00%";
            litOtherExpensePercent.Text = totalRevenue > 0 ? $"{(otherExpense / totalRevenue) * 100:N2}%" : "0.00%";
            litTotalExpensesPercent.Text = $"{expensesPercent:N2}%";

            litNetProfitDetail.Text = netProfit.ToString("N2");
            litNetProfitPercent.Text = $"{netMargin:N2}%";

            // Metrics
            litGrossMarginMetric.Text = $"{grossMargin:N2}%";
            litNetMarginMetric.Text = $"{netMargin:N2}%";
            litCOGSRatio.Text = $"{cogsPercent:N2}%";
            litOpExRatio.Text = $"{expensesPercent:N2}%";

            // Load comparison chart data
            LoadComparisonChart(startDate, endDate);
        }

        private void GetDateRange(out DateTime startDate, out DateTime endDate, out string periodDisplay)
        {
            string periodType = ddlPeriodType.SelectedValue;
            int year = int.Parse(ddlYear.SelectedValue);

            switch (periodType)
            {
                case "MONTH":
                    int month = int.Parse(ddlMonth.SelectedValue);
                    startDate = new DateTime(year, month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    periodDisplay = $"{ddlMonth.SelectedItem.Text} {year}";
                    break;

                case "QUARTER":
                    string quarter = ddlQuarter.SelectedValue;
                    switch (quarter)
                    {
                        case "Q1":
                            startDate = new DateTime(year, 1, 1);
                            endDate = new DateTime(year, 3, 31);
                            periodDisplay = $"ไตรมาส 1/{year} (ม.ค.-มี.ค.)";
                            break;
                        case "Q2":
                            startDate = new DateTime(year, 4, 1);
                            endDate = new DateTime(year, 6, 30);
                            periodDisplay = $"ไตรมาส 2/{year} (เม.ย.-มิ.ย.)";
                            break;
                        case "Q3":
                            startDate = new DateTime(year, 7, 1);
                            endDate = new DateTime(year, 9, 30);
                            periodDisplay = $"ไตรมาส 3/{year} (ก.ค.-ก.ย.)";
                            break;
                        default: // Q4
                            startDate = new DateTime(year, 10, 1);
                            endDate = new DateTime(year, 12, 31);
                            periodDisplay = $"ไตรมาส 4/{year} (ต.ค.-ธ.ค.)";
                            break;
                    }
                    break;

                case "YEAR":
                    startDate = new DateTime(year, 1, 1);
                    endDate = new DateTime(year, 12, 31);
                    periodDisplay = $"ปี {year}";
                    break;

                default: // CUSTOM
                    startDate = DateTime.Now.AddMonths(-1);
                    endDate = DateTime.Now;
                    periodDisplay = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
                    break;
            }
        }

        private void GetPreviousPeriodDateRange(DateTime currentStart, DateTime currentEnd, out DateTime prevStart, out DateTime prevEnd)
        {
            TimeSpan duration = currentEnd - currentStart;
            prevEnd = currentStart.AddDays(-1);
            prevStart = prevEnd.AddDays(-duration.Days);
        }

        private decimal CalculateTotalRevenue(DateTime startDate, DateTime endDate)
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
            return dt != null && dt.Rows.Count > 0 ? Convert.ToDecimal(dt.Rows[0]["TotalRevenue"]) : 0;
        }

        private decimal GetAccommodationRevenue(DateTime startDate, DateTime endDate)
        {
            // Revenue from room bookings
            string query = @"
                SELECT ISNULL(SUM(ar.Total_Amount), 0) as AccomRevenue
                FROM Account_Receipt ar
                INNER JOIN Reservation r ON ar.Reservation_ID = r.ID
                WHERE CAST(ar.Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                  AND CAST(ar.Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                  AND ar.Status = 'Normal'
                  AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')";

            var parameters = new Dictionary<string, object>
            {
                { "@StartDate", startDate },
                { "@EndDate", endDate }
            };

            DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);
            return dt != null && dt.Rows.Count > 0 ? Convert.ToDecimal(dt.Rows[0]["AccomRevenue"]) : 0;
        }

        private decimal GetProductRevenue(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Revenue from product sales (using Product_Out table for sales marked as 'ขาย')
                string query = @"
                    SELECT ISNULL(SUM(po.Amount * po.PricePerUnit), 0) as ProductRevenue
                    FROM Product_Out po
                    WHERE CAST(po.DateTime_Out AS DATE) >= CAST(@StartDate AS DATE)
                      AND CAST(po.DateTime_Out AS DATE) <= CAST(@EndDate AS DATE)
                      AND po.Remark = N'ขาย'";

                var parameters = new Dictionary<string, object>
                {
                    { "@StartDate", startDate },
                    { "@EndDate", endDate }
                };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);
                return dt != null && dt.Rows.Count > 0 ? Convert.ToDecimal(dt.Rows[0]["ProductRevenue"]) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private decimal GetServiceRevenue(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Revenue from additional services/products charged to rooms (Room_Charge table)
                string query = @"
                    SELECT ISNULL(SUM(rc.TotalPrice), 0) as ServiceRevenue
                    FROM Room_Charge rc
                    WHERE CAST(rc.ChargedAt AS DATE) >= CAST(@StartDate AS DATE)
                      AND CAST(rc.ChargedAt AS DATE) <= CAST(@EndDate AS DATE)
                      AND rc.IsPaid = 1";

                var parameters = new Dictionary<string, object>
                {
                    { "@StartDate", startDate },
                    { "@EndDate", endDate }
                };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);

                // If Room_Charge table doesn't exist or returns null, try alternate calculation
                if (dt == null || dt.Rows.Count == 0 || dt.Rows[0]["ServiceRevenue"] == DBNull.Value)
                {
                    // Calculate from Product_Out with room-related remarks
                    string altQuery = @"
                        SELECT ISNULL(SUM(po.Amount * po.PricePerUnit), 0) as ServiceRevenue
                        FROM Product_Out po
                        WHERE CAST(po.DateTime_Out AS DATE) >= CAST(@StartDate AS DATE)
                          AND CAST(po.DateTime_Out AS DATE) <= CAST(@EndDate AS DATE)
                          AND po.Remark != N'ขาย'
                          AND po.Remark IS NOT NULL";

                    dt = codeInstance.DatabaseQuerySafe(conn, altQuery, parameters);
                }

                return dt != null && dt.Rows.Count > 0 && dt.Rows[0]["ServiceRevenue"] != DBNull.Value
                    ? Convert.ToDecimal(dt.Rows[0]["ServiceRevenue"]) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private decimal CalculateCOGS(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Calculate COGS from products sold (using Product_Out and Product_In for cost)
                // First try to get cost from Product_In (average cost method)
                string query = @"
                    SELECT ISNULL(SUM(po.Amount * ISNULL(
                        (SELECT TOP 1 pi.PricePerUnit
                         FROM Product_In pi
                         WHERE pi.Product_ID = po.Product_ID
                         ORDER BY pi.DateTime_In DESC), 0)), 0) as TotalCOGS
                    FROM Product_Out po
                    WHERE CAST(po.DateTime_Out AS DATE) >= CAST(@StartDate AS DATE)
                      AND CAST(po.DateTime_Out AS DATE) <= CAST(@EndDate AS DATE)
                      AND po.Remark = N'ขาย'";

                var parameters = new Dictionary<string, object>
                {
                    { "@StartDate", startDate },
                    { "@EndDate", endDate }
                };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);
                return dt != null && dt.Rows.Count > 0 && dt.Rows[0]["TotalCOGS"] != DBNull.Value
                    ? Convert.ToDecimal(dt.Rows[0]["TotalCOGS"]) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private decimal GetExpenseByType(DateTime startDate, DateTime endDate, string expenseType)
        {
            try
            {
                // Check if Type column exists in Account_Payment
                var checkParams = new Dictionary<string, object>();
                DataTable dtCheck = codeInstance.DatabaseQuerySafe(conn,
                    "SELECT COUNT(*) AS ColumnExists FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Account_Payment' AND COLUMN_NAME = 'Type'",
                    checkParams);

                if (dtCheck == null || dtCheck.Rows.Count == 0 || Convert.ToInt32(dtCheck.Rows[0]["ColumnExists"]) == 0)
                {
                    // Type column doesn't exist, return 0 for individual expense types
                    return 0;
                }

                string query = @"
                    SELECT ISNULL(SUM(Total_Amount), 0) as TotalExpense
                    FROM Account_Payment
                    WHERE CAST(Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                      AND CAST(Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                      AND Type = @ExpenseType
                      AND Status = 'Normal'";

                var parameters = new Dictionary<string, object>
                {
                    { "@StartDate", startDate },
                    { "@EndDate", endDate },
                    { "@ExpenseType", expenseType }
                };

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);
                return dt != null && dt.Rows.Count > 0 ? Convert.ToDecimal(dt.Rows[0]["TotalExpense"]) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private decimal GetOtherExpenses(DateTime startDate, DateTime endDate, string[] excludeTypes)
        {
            try
            {
                // Check if Type column exists in Account_Payment
                var checkParams = new Dictionary<string, object>();
                DataTable dtCheck = codeInstance.DatabaseQuerySafe(conn,
                    "SELECT COUNT(*) AS ColumnExists FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Account_Payment' AND COLUMN_NAME = 'Type'",
                    checkParams);

                if (dtCheck == null || dtCheck.Rows.Count == 0 || Convert.ToInt32(dtCheck.Rows[0]["ColumnExists"]) == 0)
                {
                    // Type column doesn't exist, return total expenses
                    string totalQuery = @"
                        SELECT ISNULL(SUM(Total_Amount), 0) as TotalExpense
                        FROM Account_Payment
                        WHERE CAST(Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                          AND CAST(Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                          AND Status = 'Normal'";

                    var totalParams = new Dictionary<string, object>
                    {
                        { "@StartDate", startDate },
                        { "@EndDate", endDate }
                    };

                    DataTable dtTotal = codeInstance.DatabaseQuerySafe(conn, totalQuery, totalParams);
                    return dtTotal != null && dtTotal.Rows.Count > 0 ? Convert.ToDecimal(dtTotal.Rows[0]["TotalExpense"]) : 0;
                }

                StringBuilder typeCondition = new StringBuilder();
                for (int i = 0; i < excludeTypes.Length; i++)
                {
                    if (i > 0) typeCondition.Append(" AND ");
                    typeCondition.Append($"Type != @ExcludeType{i}");
                }

                string query = $@"
                    SELECT ISNULL(SUM(Total_Amount), 0) as OtherExpense
                    FROM Account_Payment
                    WHERE CAST(Created_Date AS DATE) >= CAST(@StartDate AS DATE)
                      AND CAST(Created_Date AS DATE) <= CAST(@EndDate AS DATE)
                      AND {typeCondition}
                      AND Status = 'Normal'";

                var parameters = new Dictionary<string, object>
                {
                    { "@StartDate", startDate },
                    { "@EndDate", endDate }
                };

                for (int i = 0; i < excludeTypes.Length; i++)
                {
                    parameters.Add($"@ExcludeType{i}", excludeTypes[i]);
                }

                DataTable dt = codeInstance.DatabaseQuerySafe(conn, query, parameters);
                return dt != null && dt.Rows.Count > 0 ? Convert.ToDecimal(dt.Rows[0]["OtherExpense"]) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private void LoadComparisonChart(DateTime currentStartDate, DateTime currentEndDate)
        {
            List<string> labels = new List<string>();
            List<decimal> revenues = new List<decimal>();
            List<decimal> expenses = new List<decimal>();
            List<decimal> profits = new List<decimal>();

            // Get data for last 6 periods (months/quarters/years)
            string periodType = ddlPeriodType.SelectedValue;
            int periodsToShow = 6;

            for (int i = periodsToShow - 1; i >= 0; i--)
            {
                DateTime start, end;
                string label;

                switch (periodType)
                {
                    case "MONTH":
                        start = currentStartDate.AddMonths(-i);
                        end = start.AddMonths(1).AddDays(-1);
                        label = start.ToString("MMM yyyy");
                        break;

                    case "QUARTER":
                        start = currentStartDate.AddMonths(-i * 3);
                        end = start.AddMonths(3).AddDays(-1);
                        label = $"Q{((start.Month - 1) / 3) + 1}/{start.Year}";
                        break;

                    case "YEAR":
                        start = currentStartDate.AddYears(-i);
                        end = new DateTime(start.Year, 12, 31);
                        label = start.Year.ToString();
                        break;

                    default:
                        continue;
                }

                decimal revenue = CalculateTotalRevenue(start, end);
                decimal cogs = CalculateCOGS(start, end);
                decimal allExpenses = GetExpenseByType(start, end, "เงินเดือนพนักงาน")
                                    + GetExpenseByType(start, end, "ค่าสาธารณูปโภค")
                                    + GetExpenseByType(start, end, "ค่าซ่อมบำรุง")
                                    + GetOtherExpenses(start, end, new[] { "เงินเดือนพนักงาน", "ค่าสาธารณูปโภค", "ค่าซ่อมบำรุง" });
                decimal totalExpenses = cogs + allExpenses;
                decimal profit = revenue - totalExpenses;

                labels.Add(label);
                revenues.Add(revenue);
                expenses.Add(totalExpenses);
                profits.Add(profit);
            }

            // Convert to JSON
            hfComparisonLabels.Value = JsonConvert.SerializeObject(labels);
            hfComparisonRevenue.Value = JsonConvert.SerializeObject(revenues);
            hfComparisonExpenses.Value = JsonConvert.SerializeObject(expenses);
            hfComparisonProfit.Value = JsonConvert.SerializeObject(profits);
        }

        private void ExportToExcel()
        {
            string periodType = ddlPeriodType.SelectedValue;
            DateTime startDate, endDate;
            string periodDisplay = "";

            GetDateRange(out startDate, out endDate, out periodDisplay);

            // Create CSV content
            StringBuilder csv = new StringBuilder();
            csv.Append("\uFEFF"); // BOM for Excel to recognize UTF-8

            // Header
            csv.AppendLine("งบกำไรขาดทุน (Profit & Loss Statement)");
            csv.AppendLine($"ช่วงเวลา: {periodDisplay}");
            csv.AppendLine($"วันที่ออกรายงาน: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            csv.AppendLine("");

            // Column headers
            csv.AppendLine("รายการ,จำนวนเงิน (฿),% ของรายได้");

            // Calculate data
            decimal totalRevenue = CalculateTotalRevenue(startDate, endDate);
            decimal accomRevenue = GetAccommodationRevenue(startDate, endDate);
            decimal productRevenue = GetProductRevenue(startDate, endDate);
            decimal serviceRevenue = GetServiceRevenue(startDate, endDate);
            decimal otherRevenue = totalRevenue - accomRevenue - productRevenue - serviceRevenue;

            decimal totalCOGS = CalculateCOGS(startDate, endDate);
            decimal grossProfit = totalRevenue - totalCOGS;

            decimal salaryExpense = GetExpenseByType(startDate, endDate, "เงินเดือนพนักงาน");
            decimal utilityExpense = GetExpenseByType(startDate, endDate, "ค่าสาธารณูปโภค");
            decimal maintenanceExpense = GetExpenseByType(startDate, endDate, "ค่าซ่อมบำรุง");
            decimal otherExpense = GetOtherExpenses(startDate, endDate, new[] { "เงินเดือนพนักงาน", "ค่าสาธารณูปโภค", "ค่าซ่อมบำรุง" });
            decimal totalExpenses = salaryExpense + utilityExpense + maintenanceExpense + otherExpense;

            decimal netProfit = grossProfit - totalExpenses;

            // Revenue section
            csv.AppendLine("รายได้ (REVENUE),,");
            csv.AppendLine($"  รายได้จากห้องพัก,{accomRevenue:N2},{(totalRevenue > 0 ? (accomRevenue / totalRevenue) * 100 : 0):N2}%");
            csv.AppendLine($"  รายได้จากสินค้า,{productRevenue:N2},{(totalRevenue > 0 ? (productRevenue / totalRevenue) * 100 : 0):N2}%");
            csv.AppendLine($"  รายได้จากบริการ,{serviceRevenue:N2},{(totalRevenue > 0 ? (serviceRevenue / totalRevenue) * 100 : 0):N2}%");
            csv.AppendLine($"  รายได้อื่นๆ,{otherRevenue:N2},{(totalRevenue > 0 ? (otherRevenue / totalRevenue) * 100 : 0):N2}%");
            csv.AppendLine($"รายได้รวม,{totalRevenue:N2},100.00%");
            csv.AppendLine("");

            // COGS section
            csv.AppendLine("ต้นทุนขาย (COST OF GOODS SOLD),,");
            csv.AppendLine($"  ต้นทุนสินค้า,{totalCOGS:N2},{(totalRevenue > 0 ? (totalCOGS / totalRevenue) * 100 : 0):N2}%");
            csv.AppendLine($"ต้นทุนขายรวม,{totalCOGS:N2},{(totalRevenue > 0 ? (totalCOGS / totalRevenue) * 100 : 0):N2}%");
            csv.AppendLine("");

            // Gross profit
            csv.AppendLine($"กำไรขั้นต้น (GROSS PROFIT),{grossProfit:N2},{(totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0):N2}%");
            csv.AppendLine("");

            // Operating expenses
            csv.AppendLine("ค่าใช้จ่ายในการดำเนินงาน (OPERATING EXPENSES),,");
            csv.AppendLine($"  เงินเดือนพนักงาน,{salaryExpense:N2},{(totalRevenue > 0 ? (salaryExpense / totalRevenue) * 100 : 0):N2}%");
            csv.AppendLine($"  ค่าสาธารณูปโภค,{utilityExpense:N2},{(totalRevenue > 0 ? (utilityExpense / totalRevenue) * 100 : 0):N2}%");
            csv.AppendLine($"  ค่าซ่อมบำรุง,{maintenanceExpense:N2},{(totalRevenue > 0 ? (maintenanceExpense / totalRevenue) * 100 : 0):N2}%");
            csv.AppendLine($"  ค่าใช้จ่ายอื่นๆ,{otherExpense:N2},{(totalRevenue > 0 ? (otherExpense / totalRevenue) * 100 : 0):N2}%");
            csv.AppendLine($"ค่าใช้จ่ายรวม,{totalExpenses:N2},{(totalRevenue > 0 ? (totalExpenses / totalRevenue) * 100 : 0):N2}%");
            csv.AppendLine("");

            // Net profit
            csv.AppendLine($"กำไรสุทธิ (NET PROFIT),{netProfit:N2},{(totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0):N2}%");

            // Send to browser
            Response.Clear();
            Response.ContentType = "text/csv";
            Response.ContentEncoding = Encoding.UTF8;
            Response.AddHeader("Content-Disposition", $"attachment;filename=PL_Statement_{periodDisplay.Replace("/", "-").Replace(" ", "_")}.csv");
            Response.Write(csv.ToString());
            Response.End();
        }
    }
}
