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
            if (!Perm.Guard(this, Perm.HrLeave)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (!Feature.Guard(this, "HR", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            leaveService = new LeaveService();

            if (!IsPostBack)
            {
                CheckAdminLogin();
                InitializeFilters();
                LoadStatistics();
                LoadLeaveRequests();
            }
            else
            {
                // Restore tab state on postback
                RestoreTabState();
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

            // Year dropdown for requests tab
            ddlYear.Items.Clear();
            for (int year = currentYear - 2; year <= currentYear; year++)
            {
                ddlYear.Items.Add(new ListItem((year + 543).ToString(), year.ToString()));
            }
            ddlYear.SelectedValue = currentYear.ToString();
            ddlStatus.SelectedValue = "PENDING";

            // Year dropdown for quotas tab
            ddlQuotaYear.Items.Clear();
            for (int year = currentYear - 2; year <= currentYear + 1; year++)
            {
                ddlQuotaYear.Items.Add(new ListItem((year + 543).ToString(), year.ToString()));
            }
            ddlQuotaYear.SelectedValue = currentYear.ToString();

            // Leave type dropdown for quotas tab
            LoadLeaveTypeDropdown();
        }

        private void LoadLeaveTypeDropdown()
        {
            try
            {
                DataTable leaveTypes = leaveService.GetLeaveTypes();
                ddlQuotaLeaveType.Items.Clear();
                ddlQuotaLeaveType.Items.Add(new ListItem("ทั้งหมด", ""));

                foreach (DataRow row in leaveTypes.Rows)
                {
                    ddlQuotaLeaveType.Items.Add(new ListItem(
                        row["LeaveTypeName"].ToString(),
                        row["ID"].ToString()
                    ));
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดประเภทการลา: " + ex.Message, "error");
            }
        }

        #endregion

        #region Tab Navigation

        private void RestoreTabState()
        {
            string activeTab = hdnActiveTab.Value;
            SetActiveTab(activeTab);
        }

        private void SetActiveTab(string tabName)
        {
            // Reset all tabs
            btnTabRequests.CssClass = "tab-btn";
            btnTabLeaveTypes.CssClass = "tab-btn";
            btnTabQuotas.CssClass = "tab-btn";
            pnlRequests.CssClass = "tab-content";
            pnlLeaveTypes.CssClass = "tab-content";
            pnlQuotas.CssClass = "tab-content";

            // Activate selected tab
            switch (tabName)
            {
                case "leavetypes":
                    btnTabLeaveTypes.CssClass = "tab-btn active";
                    pnlLeaveTypes.CssClass = "tab-content active";
                    hdnActiveTab.Value = "leavetypes";
                    break;
                case "quotas":
                    btnTabQuotas.CssClass = "tab-btn active";
                    pnlQuotas.CssClass = "tab-content active";
                    hdnActiveTab.Value = "quotas";
                    break;
                default:
                    btnTabRequests.CssClass = "tab-btn active";
                    pnlRequests.CssClass = "tab-content active";
                    hdnActiveTab.Value = "requests";
                    break;
            }
        }

        protected void btnTabRequests_Click(object sender, EventArgs e)
        {
            SetActiveTab("requests");
            LoadLeaveRequests();
            LoadStatistics();
        }

        protected void btnTabLeaveTypes_Click(object sender, EventArgs e)
        {
            SetActiveTab("leavetypes");
            LoadLeaveTypes();
        }

        protected void btnTabQuotas_Click(object sender, EventArgs e)
        {
            SetActiveTab("quotas");
            LoadQuotas();
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

        private void LoadLeaveTypes()
        {
            try
            {
                DataTable dt = leaveService.GetAllLeaveTypesForManagement();
                gvLeaveTypes.DataSource = dt;
                gvLeaveTypes.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดประเภทการลา: " + ex.Message, "error");
            }
        }

        private void LoadQuotas()
        {
            try
            {
                int year = Convert.ToInt32(ddlQuotaYear.SelectedValue);
                DataTable dt = leaveService.GetAllEmployeeQuotas(year);

                // Filter by leave type if selected
                string leaveTypeId = ddlQuotaLeaveType.SelectedValue;
                if (!string.IsNullOrEmpty(leaveTypeId))
                {
                    DataView dv = dt.DefaultView;
                    dv.RowFilter = $"LeaveTypeID = {leaveTypeId}";
                    dt = dv.ToTable();
                }

                // Filter by employee name if search is provided
                string searchTerm = txtQuotaSearch.Text.Trim();
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    string sanitizedSearchTerm = searchTerm.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
                    DataView dv = dt.DefaultView;
                    dv.RowFilter = $"EmployeeName LIKE '*{sanitizedSearchTerm}*' OR NickName LIKE '*{sanitizedSearchTerm}*'";
                    dt = dv.ToTable();
                }

                gvQuotas.DataSource = dt;
                gvQuotas.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดโควต้า: " + ex.Message, "error");
            }
        }

        #endregion

        #region Leave Requests Event Handlers

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
                        ShowMessage("ไม่สามารถอนุมัติได้ (อาจเกินโควต้าหรือถูกดำเนินการแล้ว)", "error");
                    }
                }
                else if (e.CommandName == "Reject")
                {
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

        #region Leave Types Event Handlers

        protected void btnAddLeaveType_Click(object sender, EventArgs e)
        {
            // Clear form
            hdnLeaveTypeId.Value = "";
            txtLeaveTypeCode.Text = "";
            txtLeaveTypeName.Text = "";
            txtLeaveTypeDesc.Text = "";
            txtAnnualQuota.Text = "0";
            txtDisplayOrder.Text = "0";
            chkDeductSalary.Checked = false;
            chkRequiresMedicalCert.Checked = false;
            chkRequiresApproval.Checked = true;
            chkIsActive.Checked = true;
            txtCertAfterDays.Text = "";
            ddlNoCertAction.SelectedValue = "DEDUCT";
            pnlCertRule.Visible = false;

            lblLeaveTypeModalTitle.Text = "เพิ่มประเภทการลา";
            pnlLeaveTypeModal.CssClass = "modal-overlay show";
        }

        protected void gvLeaveTypes_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            byte leaveTypeId = Convert.ToByte(e.CommandArgument);

            if (e.CommandName == "EditType")
            {
                // Load leave type data
                DataTable dt = leaveService.GetLeaveType(leaveTypeId);
                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    hdnLeaveTypeId.Value = leaveTypeId.ToString();
                    txtLeaveTypeCode.Text = row["LeaveTypeCode"]?.ToString() ?? "";
                    txtLeaveTypeName.Text = row["LeaveTypeName"]?.ToString() ?? "";
                    txtLeaveTypeDesc.Text = row["Description"]?.ToString() ?? "";
                    txtAnnualQuota.Text = row["AnnualQuota"]?.ToString() ?? "0";
                    txtDisplayOrder.Text = row["DisplayOrder"]?.ToString() ?? "0";
                    chkDeductSalary.Checked = row["DeductSalary"] != DBNull.Value && Convert.ToBoolean(row["DeductSalary"]);
                    chkRequiresMedicalCert.Checked = row["RequiresMedicalCert"] != DBNull.Value && Convert.ToBoolean(row["RequiresMedicalCert"]);
                    chkRequiresApproval.Checked = row["RequiresApproval"] != DBNull.Value && Convert.ToBoolean(row["RequiresApproval"]);
                    chkIsActive.Checked = row["IsActive"] != DBNull.Value && Convert.ToBoolean(row["IsActive"]);

                    // กฎใบรับรองแพทย์ — คอลัมน์อาจยังไม่มีถ้ายังไม่รัน PHASE19_Migration_14
                    txtCertAfterDays.Text = row.Table.Columns.Contains("MedicalCertAfterDays")
                        && row["MedicalCertAfterDays"] != DBNull.Value
                        ? Convert.ToDecimal(row["MedicalCertAfterDays"]).ToString("0.##")
                        : "";
                    string act = row.Table.Columns.Contains("NoCertAction")
                        ? Convert.ToString(row["NoCertAction"]) : "";
                    ddlNoCertAction.SelectedValue =
                        string.Equals(act, "BLOCK", StringComparison.OrdinalIgnoreCase) ? "BLOCK" : "DEDUCT";
                    pnlCertRule.Visible = chkRequiresMedicalCert.Checked;

                    lblLeaveTypeModalTitle.Text = "แก้ไขประเภทการลา";
                    pnlLeaveTypeModal.CssClass = "modal-overlay show";
                }
            }
            else if (e.CommandName == "DeleteType")
            {
                var result = leaveService.DeleteLeaveType(leaveTypeId);
                ShowMessage(result.Message, result.Success ? "success" : "error");
                LoadLeaveTypes();
            }
        }

        protected void btnSaveLeaveType_Click(object sender, EventArgs e)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(txtLeaveTypeName.Text))
            {
                ShowMessage("กรุณากรอกชื่อประเภทการลา", "error");
                return;
            }

            try
            {
                string code = txtLeaveTypeCode.Text.Trim();
                string name = txtLeaveTypeName.Text.Trim();
                string desc = txtLeaveTypeDesc.Text.Trim();
                decimal quota = decimal.TryParse(txtAnnualQuota.Text, out decimal q) ? q : 0;
                int order = int.TryParse(txtDisplayOrder.Text, out int o) ? o : 0;
                bool deductSalary = chkDeductSalary.Checked;
                bool requiresMedicalCert = chkRequiresMedicalCert.Checked;
                bool requiresApproval = chkRequiresApproval.Checked;
                bool isActive = chkIsActive.Checked;

                // เว้นว่าง = ต้องแนบทุกกรณี (null) · ใส่ 1 = ลาเกิน 1 วันถึงต้องแนบ
                decimal? certAfterDays = null;
                string certRaw = (txtCertAfterDays.Text ?? "").Trim();
                if (requiresMedicalCert && certRaw.Length > 0)
                {
                    decimal cd;
                    if (!decimal.TryParse(certRaw, out cd) || cd < 0)
                    {
                        ShowMessage("จำนวนวันของกฎใบรับรองแพทย์ต้องเป็นตัวเลขไม่ติดลบ", "error");
                        pnlLeaveTypeModal.CssClass = "modal-overlay show";
                        pnlCertRule.Visible = true;
                        return;
                    }
                    certAfterDays = cd;
                }
                string noCertAction = requiresMedicalCert ? ddlNoCertAction.SelectedValue : "BLOCK";

                if (string.IsNullOrEmpty(hdnLeaveTypeId.Value))
                {
                    // Create new
                    var result = leaveService.CreateLeaveType(name, code, desc, deductSalary,
                        requiresMedicalCert, quota, requiresApproval, order,
                        certAfterDays, noCertAction);
                    ShowMessage(result.Message, result.Success ? "success" : "error");
                }
                else
                {
                    // Update existing
                    byte leaveTypeId = Convert.ToByte(hdnLeaveTypeId.Value);
                    var result = leaveService.UpdateLeaveType(leaveTypeId, name, code, desc, deductSalary,
                        requiresMedicalCert, quota, requiresApproval, isActive, order,
                        certAfterDays, noCertAction);
                    ShowMessage(result.Message, result.Success ? "success" : "error");
                }

                pnlLeaveTypeModal.CssClass = "modal-overlay";
                LoadLeaveTypes();
                LoadLeaveTypeDropdown();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        /// <summary>ติ๊ก "ต้องมีใบรับรองแพทย์" แล้วค่อยโชว์กล่องตั้งกฎ — ไม่ติ๊กก็ไม่ต้องรก</summary>
        protected void chkRequiresMedicalCert_CheckedChanged(object sender, EventArgs e)
        {
            pnlCertRule.Visible = chkRequiresMedicalCert.Checked;
            pnlLeaveTypeModal.CssClass = "modal-overlay show";
        }

        /// <summary>
        /// ข้อความช่องใบรับรองแพทย์ในตาราง — เดิมบอกได้แค่ "ต้องการ / -"
        /// ซึ่งไม่พอสำหรับกฎแบบมีเงื่อนไขจำนวนวัน
        /// </summary>
        protected string MedCertText(object requires, object afterDays, object noCertAction)
        {
            try
            {
                bool req = requires != null && requires != DBNull.Value && Convert.ToBoolean(requires);
                if (!req) return "-";

                string when = (afterDays == null || afterDays == DBNull.Value)
                    ? "ต้องใช้ทุกกรณี"
                    : "ลาเกิน " + Convert.ToDecimal(afterDays).ToString("0.##") + " วัน";

                string act = Convert.ToString(noCertAction) ?? "";
                bool deduct = !string.Equals(act, "BLOCK", StringComparison.OrdinalIgnoreCase);

                return "<div style='line-height:1.5'>" + Server.HtmlEncode(when)
                     + "<br/><small style='color:" + (deduct ? "#dc3545" : "#6c757d") + "'>"
                     + (deduct ? "ไม่แนบ = หักเงิน" : "ไม่แนบ = ยื่นไม่ได้") + "</small></div>";
            }
            catch { return "-"; }
        }

        protected void btnCloseLeaveTypeModal_Click(object sender, EventArgs e)
        {
            pnlLeaveTypeModal.CssClass = "modal-overlay";
        }

        #endregion

        #region Quotas Event Handlers

        protected void ddlQuotaYear_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadQuotas();
        }

        protected void ddlQuotaLeaveType_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadQuotas();
        }

        protected void btnQuotaSearch_Click(object sender, EventArgs e)
        {
            LoadQuotas();
        }

        protected void btnInitializeQuotas_Click(object sender, EventArgs e)
        {
            try
            {
                int year = Convert.ToInt32(ddlQuotaYear.SelectedValue);
                var result = leaveService.InitializeQuotasForYear(year);
                ShowMessage(result.Message, result.Success ? "success" : "error");
                LoadQuotas();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void gvQuotas_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "EditQuota")
            {
                string[] args = e.CommandArgument.ToString().Split(',');
                if (args.Length == 2)
                {
                    short adminId = Convert.ToInt16(args[0]);
                    byte leaveTypeId = Convert.ToByte(args[1]);
                    int year = Convert.ToInt32(ddlQuotaYear.SelectedValue);

                    // Get employee and leave type info
                    DataTable quotas = leaveService.GetAllEmployeeQuotas(year);
                    DataView dv = quotas.DefaultView;
                    dv.RowFilter = $"AdminID = {adminId} AND LeaveTypeID = {leaveTypeId}";
                    DataTable filtered = dv.ToTable();

                    if (filtered.Rows.Count > 0)
                    {
                        DataRow row = filtered.Rows[0];
                        hdnQuotaAdminId.Value = adminId.ToString();
                        hdnQuotaLeaveTypeId.Value = leaveTypeId.ToString();
                        lblQuotaEmployee.Text = row["EmployeeName"]?.ToString() ?? "";
                        lblQuotaLeaveType.Text = row["LeaveTypeName"]?.ToString() ?? "";
                        txtQuotaTotalDays.Text = row["TotalDays"]?.ToString() ?? "0";
                        txtQuotaCarryForward.Text = row["CarryForwardDays"]?.ToString() ?? "0";
                        lblQuotaUsed.Text = (row["UsedDays"]?.ToString() ?? "0") + " วัน";

                        pnlQuotaModal.CssClass = "modal-overlay show";
                    }
                }
            }
        }

        protected void btnSaveQuota_Click(object sender, EventArgs e)
        {
            try
            {
                short adminId = Convert.ToInt16(hdnQuotaAdminId.Value);
                byte leaveTypeId = Convert.ToByte(hdnQuotaLeaveTypeId.Value);
                int year = Convert.ToInt32(ddlQuotaYear.SelectedValue);
                decimal totalDays = decimal.TryParse(txtQuotaTotalDays.Text, out decimal t) ? t : 0;
                decimal carryForward = decimal.TryParse(txtQuotaCarryForward.Text, out decimal c) ? c : 0;

                var result = leaveService.UpdateEmployeeQuota(adminId, leaveTypeId, year, totalDays, carryForward);
                ShowMessage(result.Message, result.Success ? "success" : "error");

                pnlQuotaModal.CssClass = "modal-overlay";
                LoadQuotas();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void btnCloseQuotaModal_Click(object sender, EventArgs e)
        {
            pnlQuotaModal.CssClass = "modal-overlay";
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
                    return $"<span style='color: #dc3545;'>-{amount:N2}</span>";
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
