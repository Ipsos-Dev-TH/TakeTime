using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using Newtonsoft.Json;

namespace Take_Time_BangPhra.Admin.Leave
{
    public partial class SupervisorLeaveApproval : System.Web.UI.Page
    {
        private LeaveService leaveService;
        private SupervisorService supervisorService;
        private short currentAdminId = 0;
        private string connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            leaveService = new LeaveService();
            supervisorService = new SupervisorService();

            if (!IsPostBack)
            {
                if (!CheckLogin())
                    return;

                InitializePage();
            }
            else
            {
                GetCurrentAdminId();
            }
        }

        #region Authentication

        private bool CheckLogin()
        {
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("/Default");
                return false;
            }

            GetCurrentAdminId();
            if (currentAdminId == 0)
            {
                Response.Redirect("/Default");
                return false;
            }

            return true;
        }

        private void GetCurrentAdminId()
        {
            if (Session["UserID"] != null)
            {
                currentAdminId = Convert.ToInt16(Session["UserID"]);
            }
        }

        #endregion

        #region Initialization

        private void InitializePage()
        {
            // Check if user has subordinates
            DataTable subordinates = supervisorService.GetSubordinatesForSupervisor(currentAdminId);
            if (subordinates == null || subordinates.Rows.Count == 0)
            {
                // Check if user is Admin/Owner - they can see all
                string userRole = Session["User"]?.ToString();
                if (userRole != "Admin" && userRole != "Owner")
                {
                    pnlNoSubordinates.Visible = true;
                    pnlMainContent.Visible = false;
                    return;
                }
            }

            pnlNoSubordinates.Visible = false;
            pnlMainContent.Visible = true;

            InitializeYearDropdown();
            InitializeCalendar();
            LoadStatistics();
            LoadLeaveRequests();
            LoadCalendar();
        }

        private void InitializeCalendar()
        {
            // Set current month/year
            hdnCalendarYear.Value = DateTime.Now.Year.ToString();
            hdnCalendarMonth.Value = DateTime.Now.Month.ToString();
        }

        private void InitializeYearDropdown()
        {
            int currentYear = DateTime.Now.Year;
            ddlYear.Items.Clear();

            for (int year = currentYear - 2; year <= currentYear; year++)
            {
                ddlYear.Items.Add(new ListItem((year + 543).ToString(), year.ToString()));
            }

            ddlYear.SelectedValue = currentYear.ToString();
        }

        #endregion

        #region Load Data

        private void LoadStatistics()
        {
            try
            {
                // Get supervisor dashboard stats
                DataTable stats = supervisorService.GetSupervisorDashboardStats(currentAdminId);
                if (stats != null && stats.Rows.Count > 0)
                {
                    DataRow row = stats.Rows[0];
                    lblTotalSubordinates.Text = row["TotalSubordinates"].ToString();
                    lblPendingRequests.Text = row["PendingLeaveRequests"].ToString();
                    lblApprovedThisYear.Text = row["ApprovedThisYear"].ToString();
                }

                // If user is Admin/Owner, show all pending requests
                string userRole = Session["User"]?.ToString();
                if (userRole == "Admin" || userRole == "Owner")
                {
                    DataTable allStats = leaveService.GetLeaveStatistics((short)DateTime.Now.Year);
                    if (allStats != null && allStats.Rows.Count > 0)
                    {
                        lblPendingRequests.Text = allStats.Rows[0]["PendingRequests"].ToString();
                        lblApprovedThisYear.Text = allStats.Rows[0]["ApprovedThisYear"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดสถิติ: " + ex.Message, "error");
            }
        }

        private void LoadLeaveRequests()
        {
            try
            {
                string status = ddlStatus.SelectedValue;
                short? year = string.IsNullOrEmpty(ddlYear.SelectedValue) ? (short?)null : Convert.ToInt16(ddlYear.SelectedValue);

                DataTable dt;

                // If user is Admin/Owner, show all leave requests
                string userRole = Session["User"]?.ToString();
                if (userRole == "Admin" || userRole == "Owner")
                {
                    dt = leaveService.GetLeaveRequests(null, status, year);

                    // Add CanApproveLeave and IsPrimarySupervisor columns if not exists
                    if (!dt.Columns.Contains("CanApproveLeave"))
                    {
                        dt.Columns.Add("CanApproveLeave", typeof(bool));
                        dt.Columns.Add("IsPrimarySupervisor", typeof(bool));
                        dt.Columns.Add("EmployeePosition", typeof(string));
                        foreach (DataRow row in dt.Rows)
                        {
                            row["CanApproveLeave"] = true;
                            row["IsPrimarySupervisor"] = false;
                            row["EmployeePosition"] = "";
                        }
                    }
                }
                else
                {
                    dt = leaveService.GetLeaveRequestsForSupervisor(currentAdminId, status, year);
                }

                // Filter by employee name if provided
                string searchTerm = txtSearchEmployee.Text.Trim();
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    string sanitizedSearchTerm = searchTerm.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
                    DataView dv = dt.DefaultView;
                    dv.RowFilter = $"EmployeeName LIKE '*{sanitizedSearchTerm}*' OR NickName LIKE '*{sanitizedSearchTerm}*'";
                    dt = dv.ToTable();
                }

                gvLeaveRequests.DataSource = dt;
                gvLeaveRequests.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, "error");
            }
        }

        #endregion

        #region Event Handlers

        protected void ddlStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadLeaveRequests();
        }

        protected void ddlYear_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadStatistics();
            LoadLeaveRequests();
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            LoadLeaveRequests();
        }

        protected void gvLeaveRequests_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "Approve")
            {
                long requestId = Convert.ToInt64(e.CommandArgument);
                ApproveRequest(requestId);
            }
            else if (e.CommandName == "ShowReject")
            {
                long requestId = Convert.ToInt64(e.CommandArgument);
                hdnRejectRequestId.Value = requestId.ToString();

                // Show modal via script
                ScriptManager.RegisterStartupScript(this, GetType(), "showRejectModal",
                    $"showRejectModal({requestId});", true);
            }
        }

        protected void btnConfirmReject_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(hdnRejectRequestId.Value))
                {
                    ShowMessage("ไม่พบรหัสคำขอลา", "error");
                    return;
                }

                long requestId = Convert.ToInt64(hdnRejectRequestId.Value);
                string reason = txtRejectReason.Text.Trim();

                if (string.IsNullOrEmpty(reason))
                {
                    reason = "ไม่ได้ระบุเหตุผล";
                }

                var result = leaveService.RejectLeaveRequestBySupervisor(requestId, reason, currentAdminId);

                if (result.Success)
                {
                    ShowMessage(result.Message, "success");
                    LoadStatistics();
                    LoadLeaveRequests();
                }
                else
                {
                    ShowMessage(result.Message, "error");
                }

                // Clear modal
                txtRejectReason.Text = "";
                hdnRejectRequestId.Value = "";
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        private void ApproveRequest(long requestId)
        {
            try
            {
                var result = leaveService.ApproveLeaveRequestBySupervisor(requestId, currentAdminId);

                if (result.Success)
                {
                    ShowMessage(result.Message, "success");
                    LoadStatistics();
                    LoadLeaveRequests();
                }
                else
                {
                    ShowMessage(result.Message, "error");
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        #endregion

        #region Helper Methods

        protected string GetStatusBadge(string status)
        {
            switch (status)
            {
                case "PENDING":
                    return "<span class='badge badge-pending'>รออนุมัติ</span>";
                case "APPROVED":
                    return "<span class='badge badge-approved'>อนุมัติแล้ว</span>";
                case "REJECTED":
                    return "<span class='badge badge-rejected'>ปฏิเสธ</span>";
                case "CANCELLED":
                    return "<span class='badge badge-cancelled'>ยกเลิก</span>";
                default:
                    return "<span class='badge'>" + status + "</span>";
            }
        }

        protected string GetDeductionDisplay(object deductSalary, object deductionAmount)
        {
            try
            {
                bool shouldDeduct = deductSalary != DBNull.Value && Convert.ToBoolean(deductSalary);

                if (shouldDeduct && deductionAmount != DBNull.Value)
                {
                    decimal amount = Convert.ToDecimal(deductionAmount);
                    return $"<span style='color: #dc3545;'>-฿{amount:N2}</span>";
                }
                else
                {
                    return "<span style='color: #28a745;'>ไม่หัก</span>";
                }
            }
            catch
            {
                return "-";
            }
        }

        protected string GetAttachmentLink(object medicalCertPath)
        {
            if (medicalCertPath == null || medicalCertPath == DBNull.Value || string.IsNullOrEmpty(medicalCertPath.ToString()))
            {
                return "<span class='no-attachment'>-</span>";
            }

            string path = medicalCertPath.ToString();
            string fileName = System.IO.Path.GetFileName(path);
            string fullPath = ResolveUrl("~/" + path);

            return $"<a href='{fullPath}' target='_blank' class='attachment-link'><i class='fas fa-file-medical'></i> {fileName}</a>";
        }

        private void ShowMessage(string message, string type)
        {
            pnlMessage.Visible = true;
            lblMessage.Text = message;

            if (type == "success")
            {
                pnlMessage.CssClass = "alert alert-success";
            }
            else if (type == "warning")
            {
                pnlMessage.CssClass = "alert alert-warning";
            }
            else
            {
                pnlMessage.CssClass = "alert alert-error";
            }
        }

        #endregion

        #region Calendar Methods

        protected void btnPrevMonth_Click(object sender, EventArgs e)
        {
            int year = int.Parse(hdnCalendarYear.Value);
            int month = int.Parse(hdnCalendarMonth.Value);

            month--;
            if (month < 1)
            {
                month = 12;
                year--;
            }

            hdnCalendarYear.Value = year.ToString();
            hdnCalendarMonth.Value = month.ToString();
            LoadCalendar();
        }

        protected void btnNextMonth_Click(object sender, EventArgs e)
        {
            int year = int.Parse(hdnCalendarYear.Value);
            int month = int.Parse(hdnCalendarMonth.Value);

            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }

            hdnCalendarYear.Value = year.ToString();
            hdnCalendarMonth.Value = month.ToString();
            LoadCalendar();
        }

        private void LoadCalendar()
        {
            try
            {
                int year = int.Parse(hdnCalendarYear.Value);
                int month = int.Parse(hdnCalendarMonth.Value);

                // Update month label
                string[] thaiMonths = { "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน",
                                        "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม" };
                lblCurrentMonth.Text = thaiMonths[month - 1] + " " + (year + 543);

                // Get approved leaves for this month
                DataTable approvedLeaves = GetApprovedLeavesForMonth(year, month);

                // Build leave data dictionary for JavaScript
                var leaveDataDict = new Dictionary<string, List<object>>();
                foreach (DataRow row in approvedLeaves.Rows)
                {
                    DateTime startDate = Convert.ToDateTime(row["StartDate"]);
                    DateTime endDate = Convert.ToDateTime(row["EndDate"]);
                    string employeeName = row["EmployeeName"]?.ToString() ?? "";
                    string leaveType = row["LeaveTypeName"]?.ToString() ?? "";
                    string reason = row["Reason"]?.ToString() ?? "";

                    // Add entry for each day in the leave period
                    for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
                    {
                        if (date.Month == month && date.Year == year)
                        {
                            string dateKey = date.ToString("yyyy-MM-dd");
                            if (!leaveDataDict.ContainsKey(dateKey))
                            {
                                leaveDataDict[dateKey] = new List<object>();
                            }
                            leaveDataDict[dateKey].Add(new { name = employeeName, type = leaveType, reason = reason });
                        }
                    }
                }

                hdnCalendarLeaveData.Value = JsonConvert.SerializeObject(leaveDataDict);

                // Generate calendar HTML
                litCalendarDays.Text = GenerateCalendarHtml(year, month, leaveDataDict);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดปฏิทิน: " + ex.Message, "error");
            }
        }

        private DataTable GetApprovedLeavesForMonth(int year, int month)
        {
            DataTable dt = new DataTable();

            DateTime firstDay = new DateTime(year, month, 1);
            DateTime lastDay = firstDay.AddMonths(1).AddDays(-1);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT LR.ID, LR.StartDate, LR.EndDate, LR.TotalDays, LR.Reason,
                           LT.LeaveTypeName,
                           ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName
                    FROM Leave_Requests LR
                    INNER JOIN Leave_Types LT ON LT.ID = LR.LeaveType_ID
                    INNER JOIN Admin A ON A.ID = LR.Admin_ID
                    WHERE LR.Status = 'APPROVED'
                      AND LR.StartDate <= @LastDay
                      AND LR.EndDate >= @FirstDay";

                // If not Admin/Owner, only show subordinates' leaves
                string userRole = Session["User"]?.ToString();
                if (userRole != "Admin" && userRole != "Owner")
                {
                    query += @"
                      AND LR.Admin_ID IN (
                          SELECT Employee_AdminID FROM Employee_Supervisor WHERE Supervisor_AdminID = @SupervisorID
                      )";
                }

                query += " ORDER BY LR.StartDate";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FirstDay", firstDay);
                    cmd.Parameters.AddWithValue("@LastDay", lastDay);
                    cmd.Parameters.AddWithValue("@SupervisorID", currentAdminId);

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }

            return dt;
        }

        private string GenerateCalendarHtml(int year, int month, Dictionary<string, List<object>> leaveData)
        {
            StringBuilder sb = new StringBuilder();
            DateTime firstDay = new DateTime(year, month, 1);
            int daysInMonth = DateTime.DaysInMonth(year, month);
            int startDayOfWeek = (int)firstDay.DayOfWeek; // Sunday = 0

            DateTime today = DateTime.Today;

            // Add empty cells for days before the 1st
            for (int i = 0; i < startDayOfWeek; i++)
            {
                // Show days from previous month
                DateTime prevDate = firstDay.AddDays(-(startDayOfWeek - i));
                bool isWeekend = prevDate.DayOfWeek == DayOfWeek.Saturday || prevDate.DayOfWeek == DayOfWeek.Sunday;
                sb.Append($"<div class=\"calendar-day other-month{(isWeekend ? " weekend" : "")}\">");
                sb.Append($"<div class=\"day-number\">{prevDate.Day}</div>");
                sb.Append("</div>");
            }

            // Add days of the month
            for (int day = 1; day <= daysInMonth; day++)
            {
                DateTime currentDate = new DateTime(year, month, day);
                bool isToday = currentDate == today;
                bool isWeekend = currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday;

                string dateKey = currentDate.ToString("yyyy-MM-dd");
                List<object> dayLeaves = leaveData.ContainsKey(dateKey) ? leaveData[dateKey] : new List<object>();

                sb.Append($"<div class=\"calendar-day{(isToday ? " today" : "")}{(isWeekend ? " weekend" : "")}\"");
                if (dayLeaves.Count > 0)
                {
                    sb.Append($" onclick=\"showDayDetail('{dateKey}')\" style=\"cursor: pointer;\"");
                }
                sb.Append(">");

                sb.Append($"<div class=\"day-number\">{day}</div>");

                // Show leave entries (max 3 visible)
                int visibleCount = Math.Min(dayLeaves.Count, 3);
                for (int i = 0; i < visibleCount; i++)
                {
                    dynamic leave = dayLeaves[i];
                    string leaveClass = GetLeaveTypeClass(leave.type.ToString());
                    string displayName = leave.name.ToString();
                    if (displayName.Length > 8)
                    {
                        displayName = displayName.Substring(0, 8) + "...";
                    }
                    sb.Append($"<div class=\"leave-entry {leaveClass}\" title=\"{leave.name}: {leave.type}\">{displayName}</div>");
                }

                // Show "more" link if there are more leaves
                if (dayLeaves.Count > 3)
                {
                    sb.Append($"<div class=\"more-leaves\">+{dayLeaves.Count - 3} คนเพิ่มเติม</div>");
                }

                sb.Append("</div>");
            }

            // Add empty cells for days after the last day
            int lastDayOfWeek = (int)new DateTime(year, month, daysInMonth).DayOfWeek;
            for (int i = lastDayOfWeek + 1; i <= 6; i++)
            {
                DateTime nextDate = new DateTime(year, month, daysInMonth).AddDays(i - lastDayOfWeek);
                bool isWeekend = nextDate.DayOfWeek == DayOfWeek.Saturday || nextDate.DayOfWeek == DayOfWeek.Sunday;
                sb.Append($"<div class=\"calendar-day other-month{(isWeekend ? " weekend" : "")}\">");
                sb.Append($"<div class=\"day-number\">{nextDate.Day}</div>");
                sb.Append("</div>");
            }

            return sb.ToString();
        }

        private string GetLeaveTypeClass(string leaveTypeName)
        {
            if (leaveTypeName.Contains("ป่วย")) return "sick";
            if (leaveTypeName.Contains("กิจ")) return "personal";
            if (leaveTypeName.Contains("พักร้อน") || leaveTypeName.Contains("ร้อน")) return "vacation";
            if (leaveTypeName.Contains("คลอด")) return "maternity";
            if (leaveTypeName.Contains("บวช")) return "ordination";
            return "other";
        }

        #endregion
    }
}
