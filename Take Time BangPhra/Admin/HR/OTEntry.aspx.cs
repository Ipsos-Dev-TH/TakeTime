using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.HR
{
    public partial class OTEntry : System.Web.UI.Page
    {
        private OTService otService;
        private SupervisorService supervisorService;
        private short currentAdminId = 0;

        protected void Page_Load(object sender, EventArgs e)
        {
            otService = new OTService();
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
            // Check if user has subordinates (is a supervisor)
            DataTable subordinates = supervisorService.GetSubordinatesForSupervisor(currentAdminId);
            if (subordinates != null && subordinates.Rows.Count > 0)
            {
                pnlSupervisorMode.Visible = true;
                pnlApprovalTab.Visible = true;

                ddlEmployee.Items.Clear();
                ddlEmployee.Items.Add(new ListItem("-- บันทึก OT ให้ตัวเอง --", currentAdminId.ToString()));

                foreach (DataRow row in subordinates.Rows)
                {
                    string text = row["EmployeeName"].ToString();
                    if (row["Position"] != DBNull.Value && !string.IsNullOrEmpty(row["Position"].ToString()))
                    {
                        text += " (" + row["Position"].ToString() + ")";
                    }
                    ddlEmployee.Items.Add(new ListItem(text, row["Employee_AdminID"].ToString()));
                }
            }
            else
            {
                pnlSupervisorMode.Visible = false;
                pnlApprovalTab.Visible = false;
            }

            // Initialize dropdowns
            InitializeMonthYearDropdowns();

            // Set default date
            txtOTDate.Text = DateTime.Now.ToString("yyyy-MM-dd");

            // Load data
            LoadStatistics();
            LoadMyOT();
            LoadSubordinateOT();
        }

        private void InitializeMonthYearDropdowns()
        {
            int currentYear = DateTime.Now.Year;
            int currentMonth = DateTime.Now.Month;

            string[] thaiMonths = { "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน",
                                    "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม" };

            // My OT - Month dropdown
            ddlMyMonth.Items.Clear();
            ddlMyMonth.Items.Add(new ListItem("ทุกเดือน", ""));
            for (int i = 1; i <= 12; i++)
            {
                ddlMyMonth.Items.Add(new ListItem(thaiMonths[i - 1], i.ToString()));
            }
            ddlMyMonth.SelectedValue = currentMonth.ToString();

            // My OT - Year dropdown (using distinct years from data)
            ddlMyYear.Items.Clear();
            DataTable myYears = otService.GetDistinctOTYears();
            foreach (DataRow row in myYears.Rows)
            {
                int year = Convert.ToInt32(row["Year"]);
                ddlMyYear.Items.Add(new ListItem((year + 543).ToString(), year.ToString()));
            }
            if (ddlMyYear.Items.FindByValue(currentYear.ToString()) != null)
            {
                ddlMyYear.SelectedValue = currentYear.ToString();
            }
            else if (ddlMyYear.Items.Count > 0)
            {
                ddlMyYear.SelectedIndex = 0;
            }

            // Approval - Month dropdown
            ddlApprovalMonth.Items.Clear();
            ddlApprovalMonth.Items.Add(new ListItem("ทุกเดือน", ""));
            for (int i = 1; i <= 12; i++)
            {
                ddlApprovalMonth.Items.Add(new ListItem(thaiMonths[i - 1], i.ToString()));
            }
            ddlApprovalMonth.SelectedValue = currentMonth.ToString();

            // Approval - Year dropdown (using distinct years from subordinates' data)
            ddlApprovalYear.Items.Clear();
            DataTable approvalYears = otService.GetDistinctOTYearsForSupervisor(currentAdminId);
            foreach (DataRow row in approvalYears.Rows)
            {
                int year = Convert.ToInt32(row["Year"]);
                ddlApprovalYear.Items.Add(new ListItem((year + 543).ToString(), year.ToString()));
            }
            if (ddlApprovalYear.Items.FindByValue(currentYear.ToString()) != null)
            {
                ddlApprovalYear.SelectedValue = currentYear.ToString();
            }
            else if (ddlApprovalYear.Items.Count > 0)
            {
                ddlApprovalYear.SelectedIndex = 0;
            }
        }

        #endregion

        #region Load Data

        private void LoadStatistics()
        {
            try
            {
                int currentMonth = DateTime.Now.Month;
                int currentYear = DateTime.Now.Year;

                // Load my OT summary
                DataTable myStats = otService.GetOTSummaryForEmployee(currentAdminId, currentMonth, currentYear);
                if (myStats != null && myStats.Rows.Count > 0)
                {
                    DataRow row = myStats.Rows[0];
                    lblApprovedHours.Text = string.Format("{0:N1}", row["ApprovedHours"]);
                    lblPendingHours.Text = string.Format("{0:N1}", row["PendingHours"]);
                }

                // Load subordinate pending count
                DataTable subStats = otService.GetOTDashboardForSupervisor(currentAdminId);
                if (subStats != null && subStats.Rows.Count > 0)
                {
                    lblSubordinatePending.Text = subStats.Rows[0]["PendingOTEntries"].ToString();
                }
                else
                {
                    lblSubordinatePending.Text = "0";
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดสถิติ: " + ex.Message, "error");
            }
        }

        private void LoadMyOT()
        {
            try
            {
                int? month = string.IsNullOrEmpty(ddlMyMonth.SelectedValue) ? (int?)null : Convert.ToInt32(ddlMyMonth.SelectedValue);
                int? year = string.IsNullOrEmpty(ddlMyYear.SelectedValue) ? (int?)null : Convert.ToInt32(ddlMyYear.SelectedValue);

                DataTable dt = otService.GetOTEntriesForEmployee(currentAdminId, month, year);
                gvMyOT.DataSource = dt;
                gvMyOT.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดประวัติ OT: " + ex.Message, "error");
            }
        }

        private void LoadSubordinateOT()
        {
            try
            {
                string status = ddlApprovalStatus.SelectedValue;
                int? month = string.IsNullOrEmpty(ddlApprovalMonth.SelectedValue) ? (int?)null : Convert.ToInt32(ddlApprovalMonth.SelectedValue);
                int? year = string.IsNullOrEmpty(ddlApprovalYear.SelectedValue) ? (int?)null : Convert.ToInt32(ddlApprovalYear.SelectedValue);

                DataTable dt = otService.GetOTEntriesForSupervisor(currentAdminId, status, month, year);
                gvSubordinateOT.DataSource = dt;
                gvSubordinateOT.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดรายการ OT ลูกน้อง: " + ex.Message, "error");
            }
        }

        #endregion

        #region Event Handlers

        protected void btnSaveOT_Click(object sender, EventArgs e)
        {
            try
            {
                // Validate
                if (string.IsNullOrWhiteSpace(txtOTDate.Text))
                {
                    ShowMessage("กรุณาเลือกวันที่ทำ OT", "error");
                    return;
                }

                decimal otHours;
                if (!decimal.TryParse(txtOTHours.Text, out otHours) || otHours <= 0)
                {
                    ShowMessage("กรุณาระบุจำนวนชั่วโมง OT ให้ถูกต้อง", "error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtWorkDescription.Text))
                {
                    ShowMessage("กรุณาระบุรายละเอียดงานที่ทำ", "error");
                    return;
                }

                DateTime otDate = DateTime.Parse(txtOTDate.Text);
                decimal otRate = Convert.ToDecimal(ddlOTRate.SelectedValue);
                string workDescription = txtWorkDescription.Text.Trim();
                string notes = txtNotes.Text.Trim();

                // Determine target employee
                short targetAdminId = currentAdminId;
                if (pnlSupervisorMode.Visible && !string.IsNullOrEmpty(ddlEmployee.SelectedValue))
                {
                    targetAdminId = Convert.ToInt16(ddlEmployee.SelectedValue);
                }

                var result = otService.CreateOTEntry(
                    targetAdminId, otDate, otHours, workDescription,
                    currentAdminId, otRate, notes);

                if (result.Success)
                {
                    ShowMessage(result.Message, "success");
                    ClearForm();
                    LoadStatistics();
                    LoadMyOT();
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

        protected void btnClear_Click(object sender, EventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            txtOTDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtOTHours.Text = "1";
            ddlOTRate.SelectedIndex = 0;
            txtWorkDescription.Text = "";
            txtNotes.Text = "";
            if (pnlSupervisorMode.Visible)
            {
                ddlEmployee.SelectedIndex = 0;
            }
        }

        protected void ddlMyMonth_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadMyOT();
        }

        protected void ddlMyYear_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadMyOT();
        }

        protected void ddlApprovalStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadSubordinateOT();
        }

        protected void ddlApprovalMonth_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadSubordinateOT();
        }

        protected void ddlApprovalYear_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadSubordinateOT();
        }

        protected void gvMyOT_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "DeleteOT")
            {
                try
                {
                    long otId = Convert.ToInt64(e.CommandArgument);
                    var result = otService.DeleteOTEntry(otId, currentAdminId);

                    if (result.Success)
                    {
                        ShowMessage(result.Message, "success");
                        LoadStatistics();
                        LoadMyOT();
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
        }

        protected void gvSubordinateOT_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            try
            {
                long otId = Convert.ToInt64(e.CommandArgument);

                if (e.CommandName == "ApproveOT")
                {
                    var result = otService.ApproveOTEntry(otId, currentAdminId);

                    if (result.Success)
                    {
                        ShowMessage(result.Message, "success");
                    }
                    else
                    {
                        ShowMessage(result.Message, "error");
                    }
                }
                else if (e.CommandName == "RejectOT")
                {
                    var result = otService.RejectOTEntry(otId, "ไม่ได้ระบุเหตุผล", currentAdminId);

                    if (result.Success)
                    {
                        ShowMessage(result.Message, "success");
                    }
                    else
                    {
                        ShowMessage(result.Message, "error");
                    }
                }

                LoadStatistics();
                LoadSubordinateOT();
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
                default:
                    return "<span class='badge'>" + status + "</span>";
            }
        }

        private void ShowMessage(string message, string type)
        {
            pnlMessage.Visible = true;
            lblMessage.Text = message;

            if (type == "success")
            {
                pnlMessage.CssClass = "alert alert-success";
            }
            else if (type == "info")
            {
                pnlMessage.CssClass = "alert alert-info";
            }
            else
            {
                pnlMessage.CssClass = "alert alert-error";
            }
        }

        #endregion
    }
}
