using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.Leave
{
    public partial class SupervisorLeaveApproval : System.Web.UI.Page
    {
        private LeaveService leaveService;
        private SupervisorService supervisorService;
        private short currentAdminId = 0;

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
            LoadStatistics();
            LoadLeaveRequests();
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
                    return "<span class='badge'>ยกเลิก</span>";
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
    }
}
