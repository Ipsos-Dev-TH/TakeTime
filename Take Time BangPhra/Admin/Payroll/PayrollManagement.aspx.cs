using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.Payroll
{
    public partial class PayrollManagement : System.Web.UI.Page
    {
        private PayrollService payrollService;
        private int currentPayrollPeriodId = 0;

        protected void Page_Load(object sender, EventArgs e)
        {
            payrollService = new PayrollService();

            if (!IsPostBack)
            {
                CheckAdminLogin();
                InitializeYearDropdown();
                LoadPayrollData();
            }
            else
            {
                // Restore currentPayrollPeriodId from hidden field
                if (!string.IsNullOrEmpty(hdnPayrollPeriodId.Value))
                {
                    int.TryParse(hdnPayrollPeriodId.Value, out currentPayrollPeriodId);
                }
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

        private void InitializeYearDropdown()
        {
            int currentYear = DateTime.Now.Year;
            ddlYear.Items.Clear();

            for (int year = currentYear - 2; year <= currentYear + 1; year++)
            {
                ddlYear.Items.Add(new ListItem((year + 543).ToString(), year.ToString()));
            }

            ddlYear.SelectedValue = currentYear.ToString();
            ddlMonth.SelectedValue = DateTime.Now.Month.ToString();
        }

        #endregion

        #region Load Data

        private void LoadPayrollData()
        {
            try
            {
                short year = Convert.ToInt16(ddlYear.SelectedValue);
                byte month = Convert.ToByte(ddlMonth.SelectedValue);

                // Check if payroll period exists
                DataTable periods = payrollService.GetPayrollPeriods(year);
                DataRow periodRow = null;

                if (periods != null && periods.Rows.Count > 0)
                {
                    foreach (DataRow row in periods.Rows)
                    {
                        if (Convert.ToByte(row["Month"]) == month)
                        {
                            periodRow = row;
                            currentPayrollPeriodId = Convert.ToInt32(row["ID"]);
                            hdnPayrollPeriodId.Value = currentPayrollPeriodId.ToString();
                            break;
                        }
                    }
                }

                if (periodRow != null)
                {
                    // Payroll exists - load records
                    LoadPayrollRecords(currentPayrollPeriodId);
                    LoadStatistics(currentPayrollPeriodId);

                    string status = periodRow["Status"].ToString();
                    btnGeneratePayroll.Visible = false;
                    btnApprovePayroll.Visible = (status == "DRAFT");
                    btnProcessAll.Visible = (status == "COMPLETED" || status == "APPROVED");
                    pnlStats.Visible = true;
                }
                else
                {
                    // No payroll yet
                    gvPayroll.DataSource = null;
                    gvPayroll.DataBind();
                    btnGeneratePayroll.Visible = true;
                    btnApprovePayroll.Visible = false;
                    btnProcessAll.Visible = false;
                    pnlStats.Visible = false;
                    hdnPayrollPeriodId.Value = "";
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        private void LoadPayrollRecords(int payrollPeriodId)
        {
            try
            {
                DataTable dt = payrollService.GetPayrollRecords(payrollPeriodId);
                gvPayroll.DataSource = dt;
                gvPayroll.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, "error");
            }
        }

        private void LoadStatistics(int payrollPeriodId)
        {
            try
            {
                DataTable dt = payrollService.GetPayrollRecords(payrollPeriodId);

                if (dt != null && dt.Rows.Count > 0)
                {
                    decimal totalGross = 0, totalOT = 0, totalSS = 0, totalNet = 0;

                    foreach (DataRow row in dt.Rows)
                    {
                        totalGross += row["BaseSalary"] != DBNull.Value ? Convert.ToDecimal(row["BaseSalary"]) : 0;
                        totalOT += row["OTAmount"] != DBNull.Value ? Convert.ToDecimal(row["OTAmount"]) : 0;
                        totalSS += row["SocialSecurity"] != DBNull.Value ? Convert.ToDecimal(row["SocialSecurity"]) : 0;
                        totalNet += row["NetSalary"] != DBNull.Value ? Convert.ToDecimal(row["NetSalary"]) : 0;
                    }

                    lblTotalEmployees.Text = dt.Rows.Count.ToString();
                    lblTotalGrossPay.Text = string.Format("{0:N0}", totalGross);
                    lblTotalOT.Text = string.Format("{0:N0}", totalOT);
                    lblTotalSS.Text = string.Format("{0:N0}", totalSS);
                    lblTotalNetPay.Text = string.Format("{0:N0}", totalNet);
                }
            }
            catch { }
        }

        #endregion

        #region Event Handlers

        protected void ddlYear_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadPayrollData();
        }

        protected void ddlMonth_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadPayrollData();
        }

        protected void btnGeneratePayroll_Click(object sender, EventArgs e)
        {
            try
            {
                short year = Convert.ToInt16(ddlYear.SelectedValue);
                byte month = Convert.ToByte(ddlMonth.SelectedValue);
                short? adminId = GetAdminID();

                if (!adminId.HasValue)
                {
                    ShowMessage("ไม่พบข้อมูลผู้ใช้", "error");
                    return;
                }

                var result = payrollService.GeneratePayrollForPeriod(year, month, adminId.Value);

                if (result.Success)
                {
                    ShowMessage("สร้างรอบเงินเดือนสำเร็จ", "success");
                    LoadPayrollData();
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

        protected void btnApprovePayroll_Click(object sender, EventArgs e)
        {
            try
            {
                short? adminId = GetAdminID();

                if (!adminId.HasValue)
                {
                    ShowMessage("ไม่พบข้อมูลผู้ใช้", "error");
                    return;
                }

                if (currentPayrollPeriodId > 0 || int.TryParse(hdnPayrollPeriodId.Value, out currentPayrollPeriodId))
                {
                    bool success = payrollService.ApprovePayrollPeriod(currentPayrollPeriodId, adminId.Value);

                    if (success)
                    {
                        ShowMessage("อนุมัติเงินเดือนสำเร็จ", "success");
                        LoadPayrollData();
                    }
                    else
                    {
                        ShowMessage("ไม่สามารถอนุมัติได้", "error");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void btnRecalculate_Click(object sender, EventArgs e)
        {
            try
            {
                if (!int.TryParse(hdnPayrollPeriodId.Value, out currentPayrollPeriodId) || currentPayrollPeriodId <= 0)
                {
                    ShowMessage("ไม่พบรอบเงินเดือน", "error");
                    return;
                }

                // Get period info for calculating leave and OT
                var periodInfo = GetPayrollPeriodInfo(currentPayrollPeriodId);
                int daysInMonth = HRConfiguration.GetDaysInMonth(periodInfo.Year, periodInfo.Month);

                // Pull latest salary from Employee_Salary and recalculate
                DataTable dt = payrollService.GetPayrollRecords(currentPayrollPeriodId);
                int updatedCount = 0;
                int salaryUpdatedCount = 0;

                foreach (DataRow row in dt.Rows)
                {
                    long recordId = Convert.ToInt64(row["ID"]);
                    short adminId = Convert.ToInt16(row["Admin_ID"]);
                    decimal baseSalary = row["BaseSalary"] != DBNull.Value ? Convert.ToDecimal(row["BaseSalary"]) : 0;

                    // If salary is 0, try to get from Employee_Salary
                    if (baseSalary == 0)
                    {
                        decimal latestSalary = GetEmployeeCurrentSalary(adminId);
                        if (latestSalary > 0)
                        {
                            baseSalary = latestSalary;
                            salaryUpdatedCount++;
                        }
                    }

                    // Get leave days and OT hours for this employee in this period
                    decimal leaveDays = GetEmployeeLeaveDays(adminId, periodInfo.Year, periodInfo.Month);
                    decimal otHours = GetEmployeeOTHours(adminId, periodInfo.Year, periodInfo.Month);

                    // Calculate leave deduction: (salary / days in month) * leave days
                    decimal leaveDeduction = HRConfiguration.CalculateLeaveDeduction(baseSalary, leaveDays, daysInMonth);

                    // Calculate OT amount: (salary / days in month / 8) * OT hours
                    decimal otAmount = HRConfiguration.CalculateBasicOTAmount(baseSalary, otHours, daysInMonth);

                    decimal bonus = row["BonusAmount"] != DBNull.Value ? Convert.ToDecimal(row["BonusAmount"]) : 0;
                    decimal allowance = row["AllowanceAmount"] != DBNull.Value ? Convert.ToDecimal(row["AllowanceAmount"]) : 0;
                    decimal tax = row["Tax"] != DBNull.Value ? Convert.ToDecimal(row["Tax"]) : 0;
                    decimal otherDeductions = row["OtherDeductions"] != DBNull.Value ? Convert.ToDecimal(row["OtherDeductions"]) : 0;

                    // Calculate social security using centralized configuration
                    decimal socialSecurity = HRConfiguration.CalculateSocialSecurity(baseSalary);

                    // Calculate work days (days in month minus leave days)
                    int workDays = daysInMonth - (int)Math.Floor(leaveDays);
                    if (workDays < 0) workDays = 0;

                    // Calculate totals
                    decimal totalEarnings = baseSalary + otAmount + bonus + allowance;
                    decimal totalDeductions = socialSecurity + leaveDeduction + tax + otherDeductions;
                    decimal netSalary = totalEarnings - totalDeductions;

                    var updateFields = new Dictionary<string, object>
                    {
                        { "BaseSalary", baseSalary },
                        { "WorkDays", workDays },
                        { "LeaveDays", leaveDays },
                        { "LeaveDeduction", leaveDeduction },
                        { "OTHours", otHours },
                        { "OTAmount", otAmount },
                        { "SocialSecurity", socialSecurity },
                        { "TotalEarnings", totalEarnings },
                        { "TotalDeductions", totalDeductions },
                        { "NetSalary", netSalary }
                    };

                    if (payrollService.UpdatePayrollRecord(recordId, updateFields))
                    {
                        updatedCount++;
                    }
                }

                // Update period totals
                payrollService.UpdatePeriodTotals(currentPayrollPeriodId);

                string msg = $"คำนวณใหม่สำเร็จ ({updatedCount} คน)";
                if (salaryUpdatedCount > 0)
                {
                    msg += $" - ดึงเงินเดือนจากการตั้งค่า {salaryUpdatedCount} คน";
                }
                ShowMessage(msg, "success");
                LoadPayrollData();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void btnProcessAll_Click(object sender, EventArgs e)
        {
            try
            {
                if (!int.TryParse(hdnPayrollPeriodId.Value, out currentPayrollPeriodId) || currentPayrollPeriodId <= 0)
                {
                    ShowMessage("ไม่พบรอบเงินเดือน", "error");
                    return;
                }

                DataTable dt = payrollService.GetPayrollRecords(currentPayrollPeriodId);
                int processedCount = 0;

                foreach (DataRow row in dt.Rows)
                {
                    bool voucherGenerated = row["VoucherGenerated"] != DBNull.Value && Convert.ToBoolean(row["VoucherGenerated"]);
                    if (!voucherGenerated)
                    {
                        long recordId = Convert.ToInt64(row["ID"]);
                        if (ProcessSinglePayment(recordId))
                        {
                            processedCount++;
                        }
                    }
                }

                ShowMessage($"ทำจ่ายสำเร็จ ({processedCount} คน)", "success");
                LoadPayrollData();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void btnExportSS_Click(object sender, EventArgs e)
        {
            try
            {
                if (!int.TryParse(hdnPayrollPeriodId.Value, out currentPayrollPeriodId) || currentPayrollPeriodId <= 0)
                {
                    ShowMessage("ไม่พบรอบเงินเดือน", "error");
                    return;
                }

                short year = Convert.ToInt16(ddlYear.SelectedValue);
                byte month = Convert.ToByte(ddlMonth.SelectedValue);

                DataTable dt = payrollService.GetPayrollRecords(currentPayrollPeriodId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    ShowMessage("ไม่มีข้อมูลสำหรับ Export", "error");
                    return;
                }

                // Generate CSV for Social Security submission
                StringBuilder csv = new StringBuilder();
                csv.AppendLine("ลำดับ,เลขประจำตัวประชาชน,คำนำหน้า,ชื่อ,นามสกุล,เงินเดือน,เงินสมทบ,หมายเหตุ");

                int seq = 1;
                foreach (DataRow row in dt.Rows)
                {
                    string name = row["EmployeeName"]?.ToString() ?? "";
                    string[] nameParts = name.Split(' ');
                    string firstName = nameParts.Length > 0 ? nameParts[0] : "";
                    string lastName = nameParts.Length > 1 ? string.Join(" ", nameParts, 1, nameParts.Length - 1) : "";

                    decimal baseSalary = row["BaseSalary"] != DBNull.Value ? Convert.ToDecimal(row["BaseSalary"]) : 0;
                    decimal socialSecurity = row["SocialSecurity"] != DBNull.Value ? Convert.ToDecimal(row["SocialSecurity"]) : 0;

                    // Cap salary base at 15000 for SS calculation display
                    decimal ssBase = Math.Min(15000, baseSalary);

                    csv.AppendLine($"{seq},,-,{firstName},{lastName},{ssBase:F0},{socialSecurity:F0},");
                    seq++;
                }

                // Send file to browser
                string fileName = $"SocialSecurity_{year + 543}_{month:D2}.csv";
                byte[] preamble = Encoding.UTF8.GetPreamble();
                byte[] content = Encoding.UTF8.GetBytes(csv.ToString());
                byte[] bytes = preamble.Concat(content).ToArray();

                Response.Clear();
                Response.ContentType = "text/csv";
                Response.AddHeader("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                Response.BinaryWrite(bytes);
                Response.End();
            }
            catch (System.Threading.ThreadAbortException)
            {
                // Response.End() throws this - ignore it
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void gvPayroll_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ViewDetails")
            {
                long payrollRecordId = Convert.ToInt64(e.CommandArgument);
                Response.Redirect($"PayrollDetail.aspx?id={payrollRecordId}");
            }
            else if (e.CommandName == "EditPayroll")
            {
                long payrollRecordId = Convert.ToInt64(e.CommandArgument);
                LoadPayrollForEdit(payrollRecordId);
            }
            else if (e.CommandName == "ProcessPayment")
            {
                long payrollRecordId = Convert.ToInt64(e.CommandArgument);
                if (ProcessSinglePayment(payrollRecordId))
                {
                    ShowMessage("ทำจ่ายสำเร็จ", "success");
                    LoadPayrollData();
                }
            }
        }

        private void LoadPayrollForEdit(long payrollRecordId)
        {
            try
            {
                DataTable dt = payrollService.GetPayrollRecord(payrollRecordId);

                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];

                    string employeeName = row["EmployeeName"]?.ToString() ?? "";
                    decimal baseSalary = row["BaseSalary"] != DBNull.Value ? Convert.ToDecimal(row["BaseSalary"]) : 0;
                    decimal otAmount = row["OTAmount"] != DBNull.Value ? Convert.ToDecimal(row["OTAmount"]) : 0;
                    decimal bonus = row["BonusAmount"] != DBNull.Value ? Convert.ToDecimal(row["BonusAmount"]) : 0;
                    decimal allowance = row["AllowanceAmount"] != DBNull.Value ? Convert.ToDecimal(row["AllowanceAmount"]) : 0;
                    decimal ss = row["SocialSecurity"] != DBNull.Value ? Convert.ToDecimal(row["SocialSecurity"]) : 0;
                    decimal leaveDeduction = row["LeaveDeduction"] != DBNull.Value ? Convert.ToDecimal(row["LeaveDeduction"]) : 0;
                    decimal tax = row["Tax"] != DBNull.Value ? Convert.ToDecimal(row["Tax"]) : 0;
                    decimal otherDeductions = row["OtherDeductions"] != DBNull.Value ? Convert.ToDecimal(row["OtherDeductions"]) : 0;

                    string script = $@"openEditModal('{payrollRecordId}', '{EscapeJsString(employeeName)}',
                        {baseSalary}, {otAmount}, {bonus}, {allowance}, {ss}, {leaveDeduction}, {tax}, {otherDeductions});";

                    ScriptManager.RegisterStartupScript(this, GetType(), "OpenEditModal", script, true);
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void btnSavePayroll_Click(object sender, EventArgs e)
        {
            try
            {
                long payrollRecordId;
                if (!long.TryParse(hdnPayrollRecordId.Value, out payrollRecordId) || payrollRecordId <= 0)
                {
                    ShowMessage("ไม่พบข้อมูลเงินเดือน", "error");
                    return;
                }

                decimal baseSalary = 0, otAmount = 0, bonus = 0, allowance = 0;
                decimal ss = 0, leaveDeduction = 0, tax = 0, otherDeductions = 0;

                decimal.TryParse(txtEditBaseSalary.Text, out baseSalary);
                decimal.TryParse(txtEditOTAmount.Text, out otAmount);
                decimal.TryParse(txtEditBonus.Text, out bonus);
                decimal.TryParse(txtEditAllowance.Text, out allowance);
                decimal.TryParse(txtEditSocialSecurity.Text, out ss);
                decimal.TryParse(txtEditLeaveDeduction.Text, out leaveDeduction);
                decimal.TryParse(txtEditTax.Text, out tax);
                decimal.TryParse(txtEditOtherDeductions.Text, out otherDeductions);

                decimal totalEarnings = baseSalary + otAmount + bonus + allowance;
                decimal totalDeductions = ss + leaveDeduction + tax + otherDeductions;
                decimal netSalary = totalEarnings - totalDeductions;

                var updateFields = new Dictionary<string, object>
                {
                    { "BaseSalary", baseSalary },
                    { "OTAmount", otAmount },
                    { "BonusAmount", bonus },
                    { "AllowanceAmount", allowance },
                    { "SocialSecurity", ss },
                    { "LeaveDeduction", leaveDeduction },
                    { "Tax", tax },
                    { "OtherDeductions", otherDeductions },
                    { "TotalEarnings", totalEarnings },
                    { "TotalDeductions", totalDeductions },
                    { "NetSalary", netSalary }
                };

                if (payrollService.UpdatePayrollRecord(payrollRecordId, updateFields))
                {
                    // Update period totals
                    if (int.TryParse(hdnPayrollPeriodId.Value, out currentPayrollPeriodId))
                    {
                        payrollService.UpdatePeriodTotals(currentPayrollPeriodId);
                    }

                    ShowMessage("บันทึกข้อมูลสำเร็จ", "success");
                    LoadPayrollData();
                }
                else
                {
                    ShowMessage("ไม่สามารถบันทึกข้อมูลได้", "error");
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        private bool ProcessSinglePayment(long payrollRecordId)
        {
            try
            {
                short? adminId = GetAdminID();
                if (!adminId.HasValue) return false;

                // Generate proper payment voucher with tracking (creates Account_Payment record)
                var result = payrollService.GeneratePayrollVoucher(payrollRecordId, adminId.Value);
                return result.Success;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Helper Methods

        protected string GetPaymentStatusBadge(object voucherGenerated, object voucherNumber)
        {
            try
            {
                bool generated = voucherGenerated != DBNull.Value && Convert.ToBoolean(voucherGenerated);

                if (generated)
                {
                    string voucherNo = voucherNumber != DBNull.Value ? voucherNumber.ToString() : "";
                    if (!string.IsNullOrEmpty(voucherNo))
                    {
                        return $"<span class='badge badge-paid'>จ่ายแล้ว</span><br/><small style='color:#28a745;'>{voucherNo}</small>";
                    }
                    return "<span class='badge badge-paid'>จ่ายแล้ว</span>";
                }
                else
                {
                    return "<span class='badge badge-pending'>รอทำจ่าย</span>";
                }
            }
            catch
            {
                return "<span class='badge badge-draft'>-</span>";
            }
        }

        protected string GetVoucherStatusBadge(object voucherGenerated, object voucherNumber)
        {
            return GetPaymentStatusBadge(voucherGenerated, voucherNumber);
        }

        private string EscapeJsString(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private void ShowMessage(string message, string type)
        {
            pnlMessage.Visible = true;
            lblMessage.Text = message;

            if (type == "success")
            {
                pnlMessage.CssClass = "alert alert-success";
            }
            else
            {
                pnlMessage.CssClass = "alert alert-error";
            }
        }

        private decimal GetEmployeeCurrentSalary(short adminId)
        {
            try
            {
                string connStr = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                        SELECT MonthlySalary
                        FROM Employee_Salary
                        WHERE Admin_ID = @AdminID AND IsActive = 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@AdminID", adminId);
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToDecimal(result);
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Get approved leave days for an employee in a specific month
        /// Excludes leaves that have been replaced by work
        /// </summary>
        private decimal GetEmployeeLeaveDays(short adminId, int year, int month)
        {
            try
            {
                string connStr = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    // Get leaves that are approved and NOT fully replaced
                    using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                        SELECT ISNULL(SUM(
                            CASE
                                WHEN ISNULL(IsReplaced, 0) = 1 THEN 0
                                ELSE TotalDays
                            END
                        ), 0) AS LeaveDays
                        FROM Leave_Requests
                        WHERE Admin_ID = @AdminID
                          AND Status = 'APPROVED'
                          AND YEAR(StartDate) = @Year
                          AND MONTH(StartDate) = @Month", conn))
                    {
                        cmd.Parameters.AddWithValue("@AdminID", adminId);
                        cmd.Parameters.AddWithValue("@Year", year);
                        cmd.Parameters.AddWithValue("@Month", month);
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToDecimal(result);
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Get approved OT hours for an employee in a specific month
        /// </summary>
        private decimal GetEmployeeOTHours(short adminId, int year, int month)
        {
            try
            {
                string connStr = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                        SELECT ISNULL(SUM(OTHours), 0) AS TotalOTHours
                        FROM OT_Entry
                        WHERE Admin_ID = @AdminID
                          AND Status = 'APPROVED'
                          AND YEAR(OTDate) = @Year
                          AND MONTH(OTDate) = @Month", conn))
                    {
                        cmd.Parameters.AddWithValue("@AdminID", adminId);
                        cmd.Parameters.AddWithValue("@Year", year);
                        cmd.Parameters.AddWithValue("@Month", month);
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToDecimal(result);
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Get payroll period info (year, month)
        /// </summary>
        private (int Year, int Month) GetPayrollPeriodInfo(int periodId)
        {
            try
            {
                string connStr = System.Configuration.ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                        SELECT Year, Month FROM Payroll_Periods WHERE ID = @PeriodID", conn))
                    {
                        cmd.Parameters.AddWithValue("@PeriodID", periodId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return (Convert.ToInt32(reader["Year"]), Convert.ToInt32(reader["Month"]));
                            }
                        }
                    }
                }
            }
            catch { }
            return (DateTime.Now.Year, DateTime.Now.Month);
        }

        #endregion
    }
}
