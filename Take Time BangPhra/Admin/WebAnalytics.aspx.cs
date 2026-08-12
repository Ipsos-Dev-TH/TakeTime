using System;
using System.Data;
using System.Globalization;
using System.Web.UI;
using Take_Time_BangPhra.Class;
using static Take_Time_BangPhra.Class.WebAnalyticsService;

namespace Take_Time_BangPhra.Admin
{
    public partial class WebAnalytics : System.Web.UI.Page
    {
        private WebAnalyticsService analyticsService;
        private int maxVisitCount = 1;
        private int maxDayCount = 1;
        private int maxHourCount = 1;
        private UserType currentUserType = UserType.All;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "WebAnalytics", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            analyticsService = new WebAnalyticsService();

            // Security check
            if (!CheckOwnerAccess())
            {
                Response.Redirect("/Default");
                return;
            }

            if (!IsPostBack)
            {
                // Set default date range (last 7 days)
                txtEndDate.Text = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                txtStartDate.Text = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                LoadAllData();
            }
        }

        #region Security

        private bool CheckOwnerAccess()
        {
            try
            {
                if (Session["permission"]?.ToString() == "True" && Session["User"]?.ToString() == "Owner")
                {
                    return true;
                }
            }
            catch { }
            return false;
        }

        #endregion

        #region Data Loading

        private UserType GetSelectedUserType()
        {
            if (int.TryParse(ddlUserType.SelectedValue, out int val))
            {
                return (UserType)val;
            }
            return UserType.All;
        }

        private void LoadAllData()
        {
            DateTime startDate = GetStartDate();
            DateTime endDate = GetEndDate();
            currentUserType = GetSelectedUserType();

            LoadDashboardStats(startDate, endDate);
            LoadOverviewData(startDate, endDate);
            LoadBrowserData(startDate, endDate);
            LoadTimeData(startDate, endDate);
            LoadVisitorData(startDate, endDate);
            LoadActivityData(startDate, endDate);
            LoadErrorData(startDate, endDate);
            LoadAccessLogData(startDate, endDate);
        }

        private void LoadDashboardStats(DateTime startDate, DateTime endDate)
        {
            try
            {
                DataTable stats = analyticsService.GetDashboardStats(startDate, endDate, currentUserType);
                if (stats.Rows.Count > 0)
                {
                    lblTotalVisits.Text = FormatNumber(stats.Rows[0]["TotalVisits"]);
                    lblUniqueVisitors.Text = FormatNumber(stats.Rows[0]["UniqueVisitors"]);
                    lblTodayVisits.Text = FormatNumber(stats.Rows[0]["TodayVisits"]);
                    lblWeeklyVisits.Text = FormatNumber(stats.Rows[0]["WeeklyVisits"]);
                    lblMonthlyVisits.Text = FormatNumber(stats.Rows[0]["MonthlyVisits"]);
                    lblErrorCount.Text = FormatNumber(stats.Rows[0]["ErrorCount"]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading dashboard stats: {ex.Message}");
            }
        }

        private void LoadOverviewData(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Daily trend
                DataTable dailyTrend = analyticsService.GetDailyVisitsTrend(startDate, endDate, currentUserType);
                if (dailyTrend.Rows.Count > 0)
                {
                    foreach (DataRow row in dailyTrend.Rows)
                    {
                        int count = Convert.ToInt32(row["VisitCount"]);
                        if (count > maxVisitCount) maxVisitCount = count;
                    }
                }
                gvDailyTrend.DataSource = dailyTrend;
                gvDailyTrend.DataBind();

                // Day of week
                DataTable dayOfWeek = analyticsService.GetVisitsByDayOfWeek(startDate, endDate, currentUserType);
                if (dayOfWeek.Rows.Count > 0)
                {
                    foreach (DataRow row in dayOfWeek.Rows)
                    {
                        int count = Convert.ToInt32(row["VisitCount"]);
                        if (count > maxDayCount) maxDayCount = count;
                    }
                }
                gvDayOfWeek.DataSource = dayOfWeek;
                gvDayOfWeek.DataBind();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading overview: {ex.Message}");
            }
        }

        private void LoadBrowserData(DateTime startDate, DateTime endDate)
        {
            try
            {
                DataTable browserStats = analyticsService.GetBrowserStats(startDate, endDate, currentUserType);
                gvBrowser.DataSource = browserStats;
                gvBrowser.DataBind();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading browser data: {ex.Message}");
            }
        }

        private void LoadTimeData(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Hourly
                DataTable hourly = analyticsService.GetVisitsByHour(startDate, endDate, currentUserType);
                if (hourly.Rows.Count > 0)
                {
                    foreach (DataRow row in hourly.Rows)
                    {
                        int count = Convert.ToInt32(row["VisitCount"]);
                        if (count > maxHourCount) maxHourCount = count;
                    }
                }
                gvHourly.DataSource = hourly;
                gvHourly.DataBind();

                // Monthly
                DataTable monthly = analyticsService.GetMonthlyVisitsSummary(DateTime.Now.Year, currentUserType);
                gvMonthly.DataSource = monthly;
                gvMonthly.DataBind();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading time data: {ex.Message}");
            }
        }

        private void LoadVisitorData(DateTime startDate, DateTime endDate)
        {
            try
            {
                // New vs Returning
                DataTable newVsReturning = analyticsService.GetNewVsReturningVisitors(startDate, endDate, currentUserType);
                gvNewVsReturning.DataSource = newVsReturning;
                gvNewVsReturning.DataBind();

                // Top Visitors
                DataTable topVisitors = analyticsService.GetTopVisitorsByIP(startDate, endDate, 20, currentUserType);
                gvTopVisitors.DataSource = topVisitors;
                gvTopVisitors.DataBind();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading visitor data: {ex.Message}");
            }
        }

        private void LoadActivityData(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Active users
                DataTable activeUsers = analyticsService.GetActiveUsersSummary(startDate, endDate);
                gvActiveUsers.DataSource = activeUsers;
                gvActiveUsers.DataBind();

                // Action summary
                DataTable actionSummary = analyticsService.GetActionSummary(startDate, endDate);
                gvActionSummary.DataSource = actionSummary;
                gvActionSummary.DataBind();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading activity data: {ex.Message}");
            }
        }

        private void LoadErrorData(DateTime startDate, DateTime endDate)
        {
            try
            {
                // By category
                DataTable byCategory = analyticsService.GetSystemLogsByCategory(startDate, endDate);
                gvLogsByCategory.DataSource = byCategory;
                gvLogsByCategory.DataBind();

                // By level
                DataTable byLevel = analyticsService.GetSystemLogsByLevel(startDate, endDate);
                gvLogsByLevel.DataSource = byLevel;
                gvLogsByLevel.DataBind();

                // Recent errors
                DataTable recentErrors = analyticsService.GetRecentErrors(50);
                gvRecentErrors.DataSource = recentErrors;
                gvRecentErrors.DataBind();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading error data: {ex.Message}");
            }
        }

        private void LoadAccessLogData(DateTime startDate, DateTime endDate)
        {
            try
            {
                int totalCount = analyticsService.GetAccessLogCount(startDate, endDate, "", "", currentUserType);
                lblAccessLogCount.Text = $"{totalCount:N0} รายการ";

                DataTable accessLog = analyticsService.GetAccessLogs(startDate, endDate, "", "", 100, 1, currentUserType);
                gvAccessLog.DataSource = accessLog;
                gvAccessLog.DataBind();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading access log: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        protected void btnApplyFilter_Click(object sender, EventArgs e)
        {
            LoadAllData();
        }

        protected void ddlUserType_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadAllData();
        }

        protected void ddlDateRange_SelectedIndexChanged(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            DateTime startDate = now;
            DateTime endDate = now;

            switch (ddlDateRange.SelectedValue)
            {
                case "today":
                    startDate = now.Date;
                    endDate = now;
                    break;
                case "yesterday":
                    startDate = now.Date.AddDays(-1);
                    endDate = now.Date.AddSeconds(-1);
                    break;
                case "7days":
                    startDate = now.AddDays(-7);
                    endDate = now;
                    break;
                case "30days":
                    startDate = now.AddDays(-30);
                    endDate = now;
                    break;
                case "thismonth":
                    startDate = new DateTime(now.Year, now.Month, 1);
                    endDate = now;
                    break;
                case "lastmonth":
                    startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                    endDate = new DateTime(now.Year, now.Month, 1).AddSeconds(-1);
                    break;
                case "thisyear":
                    startDate = new DateTime(now.Year, 1, 1);
                    endDate = now;
                    break;
                case "custom":
                    return;
            }

            txtStartDate.Text = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            txtEndDate.Text = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            LoadAllData();
        }

        #endregion

        #region Tab Navigation

        protected void btnTabOverview_Click(object sender, EventArgs e) { SetActiveTab("overview"); }
        protected void btnTabBrowser_Click(object sender, EventArgs e) { SetActiveTab("browser"); }
        protected void btnTabTime_Click(object sender, EventArgs e) { SetActiveTab("time"); }
        protected void btnTabVisitors_Click(object sender, EventArgs e) { SetActiveTab("visitors"); }
        protected void btnTabActivity_Click(object sender, EventArgs e) { SetActiveTab("activity"); }
        protected void btnTabErrors_Click(object sender, EventArgs e) { SetActiveTab("errors"); }
        protected void btnTabAccessLog_Click(object sender, EventArgs e) { SetActiveTab("accesslog"); }

        private void SetActiveTab(string tabName)
        {
            hdnActiveTab.Value = tabName;

            // Reset all tabs
            btnTabOverview.CssClass = "tab-btn";
            btnTabBrowser.CssClass = "tab-btn";
            btnTabTime.CssClass = "tab-btn";
            btnTabVisitors.CssClass = "tab-btn";
            btnTabActivity.CssClass = "tab-btn";
            btnTabErrors.CssClass = "tab-btn";
            btnTabAccessLog.CssClass = "tab-btn";

            pnlOverview.CssClass = "tab-content";
            pnlBrowser.CssClass = "tab-content";
            pnlTime.CssClass = "tab-content";
            pnlVisitors.CssClass = "tab-content";
            pnlActivity.CssClass = "tab-content";
            pnlErrors.CssClass = "tab-content";
            pnlAccessLog.CssClass = "tab-content";

            // Set active
            switch (tabName)
            {
                case "overview":
                    btnTabOverview.CssClass = "tab-btn active";
                    pnlOverview.CssClass = "tab-content active";
                    break;
                case "browser":
                    btnTabBrowser.CssClass = "tab-btn active";
                    pnlBrowser.CssClass = "tab-content active";
                    break;
                case "time":
                    btnTabTime.CssClass = "tab-btn active";
                    pnlTime.CssClass = "tab-content active";
                    break;
                case "visitors":
                    btnTabVisitors.CssClass = "tab-btn active";
                    pnlVisitors.CssClass = "tab-content active";
                    break;
                case "activity":
                    btnTabActivity.CssClass = "tab-btn active";
                    pnlActivity.CssClass = "tab-content active";
                    break;
                case "errors":
                    btnTabErrors.CssClass = "tab-btn active";
                    pnlErrors.CssClass = "tab-content active";
                    break;
                case "accesslog":
                    btnTabAccessLog.CssClass = "tab-btn active";
                    pnlAccessLog.CssClass = "tab-content active";
                    break;
            }
        }

        #endregion

        #region Helper Methods

        private DateTime GetStartDate()
        {
            if (DateTime.TryParse(txtStartDate.Text, out DateTime result))
                return result;
            return DateTime.Now.AddDays(-7);
        }

        private DateTime GetEndDate()
        {
            if (DateTime.TryParse(txtEndDate.Text, out DateTime result))
                return result.AddDays(1).AddSeconds(-1);
            return DateTime.Now;
        }

        private string FormatNumber(object value)
        {
            if (value == null || value == DBNull.Value) return "0";
            if (int.TryParse(value.ToString(), out int num))
                return num.ToString("N0");
            return value.ToString();
        }

        protected string GetPercentage(object visitCount)
        {
            if (visitCount == null || visitCount == DBNull.Value) return "0";
            int count = Convert.ToInt32(visitCount);
            if (maxVisitCount == 0) return "0";
            return Math.Round((double)count / maxVisitCount * 100, 0).ToString();
        }

        protected string GetDayPercentage(object visitCount)
        {
            if (visitCount == null || visitCount == DBNull.Value) return "0";
            int count = Convert.ToInt32(visitCount);
            if (maxDayCount == 0) return "0";
            return Math.Round((double)count / maxDayCount * 100, 0).ToString();
        }

        protected string GetHourPercentage(object visitCount)
        {
            if (visitCount == null || visitCount == DBNull.Value) return "0";
            int count = Convert.ToInt32(visitCount);
            if (maxHourCount == 0) return "0";
            return Math.Round((double)count / maxHourCount * 100, 0).ToString();
        }

        protected string GetLevelBadgeClass(string levelName)
        {
            switch (levelName?.ToLower())
            {
                case "critical": return "badge-critical";
                case "error": return "badge-error";
                case "warning": return "badge-warning";
                case "info": return "badge-info";
                case "debug": return "badge-success";
                default: return "badge-info";
            }
        }

        #endregion
    }
}
