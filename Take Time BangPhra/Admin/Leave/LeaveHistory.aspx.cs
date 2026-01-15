using System;
using System.Data;

namespace Take_Time_BangPhra.Admin.Leave
{
    public partial class LeaveHistory : System.Web.UI.Page
    {
        private LeaveService leaveService;
        private short currentAdminId = 0;

        protected void Page_Load(object sender, EventArgs e)
        {
            leaveService = new LeaveService();

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
            LoadEmployeeInfo();
            LoadYearFilter();
            LoadLeaveTypeFilter();
            LoadData();
        }

        private void LoadEmployeeInfo()
        {
            try
            {
                EmployeeService empService = new EmployeeService();
                DataTable dt = empService.GetEmployeeCompleteProfile(currentAdminId);

                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    lblEmployeeName.Text = row["Name"]?.ToString() ?? "-";
                    lblPosition.Text = row["CurrentPosition"]?.ToString() ?? "";
                }
                else
                {
                    lblEmployeeName.Text = Session["UserName"]?.ToString() ?? "-";
                    lblPosition.Text = "";
                }
            }
            catch
            {
                lblEmployeeName.Text = Session["UserName"]?.ToString() ?? "-";
            }
        }

        private void LoadYearFilter()
        {
            try
            {
                DataTable dt = leaveService.GetLeaveYearsForEmployee(currentAdminId);
                ddlYear.Items.Clear();

                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        int year = Convert.ToInt32(row["Year"]);
                        int buddhistYear = year + 543;
                        ddlYear.Items.Add(new System.Web.UI.WebControls.ListItem(buddhistYear.ToString(), year.ToString()));
                    }
                }
                else
                {
                    // Add current year if no data
                    int currentYear = DateTime.Now.Year;
                    int buddhistYear = currentYear + 543;
                    ddlYear.Items.Add(new System.Web.UI.WebControls.ListItem(buddhistYear.ToString(), currentYear.ToString()));
                }
            }
            catch
            {
                int currentYear = DateTime.Now.Year;
                int buddhistYear = currentYear + 543;
                ddlYear.Items.Add(new System.Web.UI.WebControls.ListItem(buddhistYear.ToString(), currentYear.ToString()));
            }
        }

        private void LoadLeaveTypeFilter()
        {
            try
            {
                DataTable dt = leaveService.GetLeaveTypes();
                ddlLeaveType.Items.Clear();
                ddlLeaveType.Items.Add(new System.Web.UI.WebControls.ListItem("-- ทั้งหมด --", ""));

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        ddlLeaveType.Items.Add(new System.Web.UI.WebControls.ListItem(
                            row["LeaveTypeName"].ToString(),
                            row["ID"].ToString()));
                    }
                }
            }
            catch { }
        }

        private void LoadData()
        {
            short selectedYear = (short)DateTime.Now.Year;
            if (!string.IsNullOrEmpty(ddlYear.SelectedValue))
            {
                selectedYear = Convert.ToInt16(ddlYear.SelectedValue);
            }

            lblSelectedYear.Text = (selectedYear + 543).ToString();

            // Load leave quota for selected year
            LoadLeaveQuota(selectedYear);

            // Load leave totals
            LoadLeaveTotals(selectedYear);

            // Load leave history with filters
            LoadLeaveHistory(selectedYear);
        }

        private void LoadLeaveQuota(short year)
        {
            try
            {
                DataTable dt = leaveService.GetEmployeeLeaveQuota(currentAdminId, year);
                gvLeaveQuota.DataSource = dt;
                gvLeaveQuota.DataBind();
            }
            catch { }
        }

        private void LoadLeaveTotals(short year)
        {
            try
            {
                DataTable dt = leaveService.GetEmployeeLeaveTotals(currentAdminId, year);
                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    lblTotalQuota.Text = row["TotalQuota"] != DBNull.Value ? Convert.ToDecimal(row["TotalQuota"]).ToString("N1") : "0";
                    lblTotalUsed.Text = row["TotalUsed"] != DBNull.Value ? Convert.ToDecimal(row["TotalUsed"]).ToString("N1") : "0";
                    lblTotalRemaining.Text = row["TotalRemaining"] != DBNull.Value ? Convert.ToDecimal(row["TotalRemaining"]).ToString("N1") : "0";
                }
                else
                {
                    lblTotalQuota.Text = "0";
                    lblTotalUsed.Text = "0";
                    lblTotalRemaining.Text = "0";
                }
            }
            catch
            {
                lblTotalQuota.Text = "0";
                lblTotalUsed.Text = "0";
                lblTotalRemaining.Text = "0";
            }
        }

        private void LoadLeaveHistory(short year)
        {
            try
            {
                // Get all leave requests for this employee and year
                DataTable dt = leaveService.GetLeaveRequestsForEmployee(currentAdminId, year);

                // Apply additional filters
                if (dt != null && dt.Rows.Count > 0)
                {
                    DataView dv = dt.DefaultView;
                    string filter = "";

                    // Filter by leave type
                    if (!string.IsNullOrEmpty(ddlLeaveType.SelectedValue))
                    {
                        filter = "LeaveTypeID = " + ddlLeaveType.SelectedValue;
                    }

                    // Filter by status
                    if (!string.IsNullOrEmpty(ddlStatus.SelectedValue))
                    {
                        if (!string.IsNullOrEmpty(filter))
                            filter += " AND ";
                        filter += "Status = '" + ddlStatus.SelectedValue + "'";
                    }

                    if (!string.IsNullOrEmpty(filter))
                    {
                        dv.RowFilter = filter;
                    }

                    gvLeaveHistory.DataSource = dv;
                    gvLeaveHistory.DataBind();

                    lblResultCount.Text = dv.Count.ToString();
                    lblTotalRequests.Text = dv.Count.ToString();
                }
                else
                {
                    gvLeaveHistory.DataSource = null;
                    gvLeaveHistory.DataBind();
                    lblResultCount.Text = "0";
                    lblTotalRequests.Text = "0";
                }
            }
            catch
            {
                lblResultCount.Text = "0";
                lblTotalRequests.Text = "0";
            }
        }

        #endregion

        #region Event Handlers

        protected void ddlYear_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadData();
        }

        protected void ddlFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            short selectedYear = (short)DateTime.Now.Year;
            if (!string.IsNullOrEmpty(ddlYear.SelectedValue))
            {
                selectedYear = Convert.ToInt16(ddlYear.SelectedValue);
            }
            LoadLeaveHistory(selectedYear);
        }

        protected void btnClear_Click(object sender, EventArgs e)
        {
            // Reset filters
            if (ddlYear.Items.Count > 0)
                ddlYear.SelectedIndex = 0;
            ddlLeaveType.SelectedIndex = 0;
            ddlStatus.SelectedIndex = 0;

            LoadData();
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

        #endregion
    }
}
