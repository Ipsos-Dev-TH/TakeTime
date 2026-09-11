using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.HR
{
    public partial class OTManagement : System.Web.UI.Page
    {
        private OTService otService;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.HrPayroll)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (!Feature.Guard(this, "HR", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            otService = new OTService();

            if (!IsPostBack)
            {
                CheckAdminAccess();
                InitializeFilters();
                LoadOTData();
            }
        }

        #region Security

        private void CheckAdminAccess()
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
            // Month dropdown
            ddlMonth.Items.Clear();
            ddlMonth.Items.Add(new ListItem("ทั้งหมด", ""));
            string[] thaiMonths = { "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน",
                                   "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม" };
            for (int i = 1; i <= 12; i++)
            {
                ddlMonth.Items.Add(new ListItem(thaiMonths[i - 1], i.ToString()));
            }
            ddlMonth.SelectedValue = DateTime.Now.Month.ToString();

            // Year dropdown
            ddlYear.Items.Clear();
            ddlYear.Items.Add(new ListItem("ทั้งหมด", ""));
            DataTable years = otService.GetAllDistinctOTYears();
            foreach (DataRow row in years.Rows)
            {
                int year = Convert.ToInt32(row["Year"]);
                ddlYear.Items.Add(new ListItem((year + 543).ToString(), year.ToString()));
            }
            if (ddlYear.Items.FindByValue(DateTime.Now.Year.ToString()) != null)
            {
                ddlYear.SelectedValue = DateTime.Now.Year.ToString();
            }

            // Employee dropdown
            ddlEmployee.Items.Clear();
            ddlEmployee.Items.Add(new ListItem("ทั้งหมด", ""));
            DataTable employees = otService.GetEmployeesWithOT();
            foreach (DataRow row in employees.Rows)
            {
                ddlEmployee.Items.Add(new ListItem(row["Name"].ToString(), row["ID"].ToString()));
            }
        }

        #endregion

        #region Data Loading

        private void LoadOTData()
        {
            try
            {
                // Get filter values
                string status = ddlStatus.SelectedValue;
                int? month = string.IsNullOrEmpty(ddlMonth.SelectedValue) ? (int?)null : Convert.ToInt32(ddlMonth.SelectedValue);
                int? year = string.IsNullOrEmpty(ddlYear.SelectedValue) ? (int?)null : Convert.ToInt32(ddlYear.SelectedValue);
                short? employeeId = string.IsNullOrEmpty(ddlEmployee.SelectedValue) ? (short?)null : Convert.ToInt16(ddlEmployee.SelectedValue);

                // Get OT entries
                DataTable dt = otService.GetAllOTEntries(status, month, year, employeeId);
                gvOTEntries.DataSource = dt;
                gvOTEntries.DataBind();

                // Calculate statistics
                CalculateStats(dt);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        private void CalculateStats(DataTable dt)
        {
            int totalEntries = dt.Rows.Count;
            int pendingCount = 0;
            int approvedCount = 0;
            decimal totalHours = 0;

            foreach (DataRow row in dt.Rows)
            {
                string status = row["Status"].ToString();
                if (status == "PENDING") pendingCount++;
                else if (status == "APPROVED") approvedCount++;

                if (row["OTMultipliedHours"] != DBNull.Value)
                {
                    totalHours += Convert.ToDecimal(row["OTMultipliedHours"]);
                }
            }

            lblTotalEntries.Text = totalEntries.ToString();
            lblPendingCount.Text = pendingCount.ToString();
            lblApprovedCount.Text = approvedCount.ToString();
            lblTotalHours.Text = string.Format("{0:N1}", totalHours);
        }

        #endregion

        #region Event Handlers

        protected void ddlFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadOTData();
        }

        protected void gvOTEntries_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "EditOT")
            {
                long otId = Convert.ToInt64(e.CommandArgument);
                LoadOTForEdit(otId);
            }
            else if (e.CommandName == "DeleteOT")
            {
                long otId = Convert.ToInt64(e.CommandArgument);
                DeleteOT(otId);
            }
        }

        private void LoadOTForEdit(long otId)
        {
            try
            {
                DataTable dt = otService.GetOTEntryById(otId);
                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];

                    hdnEditId.Value = otId.ToString();
                    lblEditEmployee.Text = row["EmployeeName"].ToString();
                    txtEditDate.Text = Convert.ToDateTime(row["OTDate"]).ToString("yyyy-MM-dd");
                    txtEditHours.Text = row["OTHours"].ToString();

                    decimal otRate = Convert.ToDecimal(row["OTRate"]);
                    if (ddlEditRate.Items.FindByValue(otRate.ToString()) != null)
                    {
                        ddlEditRate.SelectedValue = otRate.ToString();
                    }

                    txtEditDescription.Text = row["WorkDescription"].ToString();
                    ddlEditStatus.SelectedValue = row["Status"].ToString();
                    txtEditNotes.Text = row["Notes"] != DBNull.Value ? row["Notes"].ToString() : "";

                    // Open modal via JavaScript
                    ScriptManager.RegisterStartupScript(this, GetType(), "OpenModal", "openEditModal();", true);
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        protected void btnSaveEdit_Click(object sender, EventArgs e)
        {
            try
            {
                short? adminId = GetAdminID();
                if (!adminId.HasValue)
                {
                    ShowMessage("ไม่พบข้อมูลผู้ใช้", false);
                    return;
                }

                long otId = Convert.ToInt64(hdnEditId.Value);
                DateTime otDate = DateTime.Parse(txtEditDate.Text);
                decimal otHours = decimal.Parse(txtEditHours.Text);
                decimal otRate = decimal.Parse(ddlEditRate.SelectedValue);
                string workDescription = txtEditDescription.Text;
                string status = ddlEditStatus.SelectedValue;
                string notes = txtEditNotes.Text;

                var result = otService.UpdateOTEntry(otId, otDate, otHours, otRate, workDescription, status, notes, adminId.Value);

                if (result.Success)
                {
                    ShowMessage(result.Message, true);
                    LoadOTData();
                }
                else
                {
                    ShowMessage(result.Message, false);
                }

                // Close modal
                ScriptManager.RegisterStartupScript(this, GetType(), "CloseModal", "closeEditModal();", true);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        private void DeleteOT(long otId)
        {
            try
            {
                var result = otService.AdminDeleteOTEntry(otId);

                if (result.Success)
                {
                    ShowMessage(result.Message, true);
                    LoadOTData();
                }
                else
                {
                    ShowMessage(result.Message, false);
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        #endregion

        #region Helpers

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
                    return status;
            }
        }

        private void ShowMessage(string message, bool isSuccess)
        {
            pnlMessage.Visible = true;
            lblMessage.Text = message;
            pnlMessage.CssClass = isSuccess ? "alert alert-success" : "alert alert-error";
        }

        #endregion
    }
}
