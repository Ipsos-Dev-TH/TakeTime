using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.Leave
{
    public partial class LeaveManagement : System.Web.UI.Page
    {
        private LeaveService leaveService;

        protected void Page_Load(object sender, EventArgs e)
        {
            leaveService = new LeaveService();

            if (!IsPostBack)
            {
                CheckAdminLogin();
                InitializeFilters();
                LoadStatistics();
                LoadLeaveRequests();
            }
        }

        #region Authentication

        private void CheckAdminLogin()
        {
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("/Default");
                return;
            }

            string userType = Session["User"]?.ToString();
            if (userType != "Owner" && userType != "Admin")
            {
                Response.Redirect("/Default");
                return;
            }
        }

        private short? GetAdminID()
        {
            if (Session["UserID"] != null)
            {
                return Convert.ToInt16(Session["UserID"]);
            }
            return null;
        }

        #endregion

        #region Initialization

        private void InitializeFilters()
        {
            int currentYear = DateTime.Now.Year;
            ddlYear.Items.Clear();

            for (int year = currentYear - 2; year <= currentYear; year++)
            {
                ddlYear.Items.Add(new ListItem((year + 543).ToString(), year.ToString()));
            }

            ddlYear.SelectedValue = currentYear.ToString();
            ddlStatus.SelectedValue = "PENDING"; // Default to pending requests
        }

        #endregion

        #region Load Data

        private void LoadStatistics()
        {
            try
            {
                short year = Convert.ToInt16(ddlYear.SelectedValue);
                DataTable stats = leaveService.GetLeaveStatistics(year);

                if (stats != null && stats.Rows.Count > 0)
                {
                    DataRow row = stats.Rows[0];
                    lblPendingRequests.Text = row["PendingRequests"].ToString();
                    lblApprovedThisYear.Text = row["ApprovedThisYear"].ToString();
                    lblRejectedThisYear.Text = row["RejectedThisYear"].ToString();
                    lblTotalDays.Text = string.Format("{0:N1}", row["TotalDaysApproved"]);
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

                DataTable dt = leaveService.GetLeaveRequests(null, status, year);

                // Filter by employee name if search is provided
                string searchTerm = txtSearchEmployee.Text.Trim();
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    DataView dv = dt.DefaultView;
                    dv.RowFilter = $"EmployeeName LIKE '%{searchTerm}%' OR NickName LIKE '%{searchTerm}%'";
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
            LoadStatistics();
        }

        protected void ddlYear_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadLeaveRequests();
            LoadStatistics();
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            LoadLeaveRequests();
        }

        protected void gvLeaveRequests_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            long requestId = Convert.ToInt64(e.CommandArgument);
            short? adminId = GetAdminID();

            if (!adminId.HasValue)
            {
                ShowMessage("ไม่พบข้อมูลผู้ใช้", "error");
                return;
            }

            try
            {
                if (e.CommandName == "Approve")
                {
                    bool success = leaveService.ApproveLeaveRequest(requestId, adminId.Value);

                    if (success)
                    {
                        ShowMessage("อนุมัติคำขอลาสำเร็จ", "success");
                        LoadLeaveRequests();
                        LoadStatistics();
                    }
                    else
                    {
                        ShowMessage("ไม่สามารถอนุมัติได้", "error");
                    }
                }
                else if (e.CommandName == "Reject")
                {
                    // In a real application, you would show a modal to get the rejection reason
                    string rejectionReason = "ไม่ได้ระบุเหตุผล";
                    bool success = leaveService.RejectLeaveRequest(requestId, rejectionReason, adminId.Value);

                    if (success)
                    {
                        ShowMessage("ปฏิเสธคำขอลาสำเร็จ", "success");
                        LoadLeaveRequests();
                        LoadStatistics();
                    }
                    else
                    {
                        ShowMessage("ไม่สามารถปฏิเสธได้", "error");
                    }
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

        private void ShowMessage(string message, string type)
        {
            string script = $"alert('{message.Replace("'", "\\'")}');";
            ClientScript.RegisterStartupScript(this.GetType(), "alert", script, true);
        }

        #endregion
    }
}
