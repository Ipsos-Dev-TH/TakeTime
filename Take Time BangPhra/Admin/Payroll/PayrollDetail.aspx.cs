using System;
using System.Data;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.Payroll
{
    public partial class PayrollDetail : System.Web.UI.Page
    {
        private PayrollService payrollService;
        private long payrollRecordId = 0;

        protected void Page_Load(object sender, EventArgs e)
        {
            payrollService = new PayrollService();

            if (!IsPostBack)
            {
                CheckAdminLogin();
                LoadPayrollRecord();
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

            // Get record ID from query string
            if (!string.IsNullOrEmpty(Request.QueryString["id"]))
            {
                long.TryParse(Request.QueryString["id"], out payrollRecordId);
            }

            if (payrollRecordId == 0)
            {
                Response.Redirect("PayrollManagement.aspx");
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

        #region Load Data

        private void LoadPayrollRecord()
        {
            try
            {
                DataTable dt = payrollService.GetPayrollRecord(payrollRecordId);

                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow record = dt.Rows[0];

                    // Period info
                    string periodName = record["PeriodName"]?.ToString() ?? "";
                    lblPeriod.Text = periodName;

                    // Employee info
                    lblEmployeeName.Text = record["EmployeeName"]?.ToString() ?? "-";
                    lblPosition.Text = record["Position"]?.ToString() ?? "-";
                    lblEmployeeCode.Text = record["Admin_ID"]?.ToString() ?? "-";
                    lblDepartment.Text = "-"; // Will be added from employee profile

                    // Earnings
                    lblBaseSalary.Text = FormatCurrency(record["BaseSalary"]);
                    lblOTAmount.Text = FormatCurrency(record["OTAmount"]);
                    lblBonus.Text = FormatCurrency(record["BonusAmount"]);
                    lblAllowances.Text = FormatCurrency(record["AllowanceAmount"]);
                    lblTotalEarnings.Text = FormatCurrency(record["TotalEarnings"]);

                    // Deductions
                    lblLeaveDeduction.Text = FormatCurrency(record["LeaveDeduction"]);
                    lblSocialSecurity.Text = FormatCurrency(record["SocialSecurity"]);
                    lblTax.Text = FormatCurrency(record["Tax"]);
                    lblOtherDeductions.Text = FormatCurrency(record["OtherDeductions"]);
                    lblTotalDeductions.Text = FormatCurrency(record["TotalDeductions"]);

                    // Totals
                    lblWorkDays.Text = record["WorkDays"]?.ToString() ?? "0";
                    lblLeaveDays.Text = record["LeaveDays"]?.ToString() ?? "0";
                    lblNetSalary.Text = FormatCurrency(record["NetSalary"]);

                    // Check if voucher already generated
                    bool voucherGenerated = record["VoucherGenerated"] != DBNull.Value && Convert.ToBoolean(record["VoucherGenerated"]);
                    btnGenerateVoucher.Visible = !voucherGenerated;

                    // Load OT details
                    LoadOTDetails();
                }
                else
                {
                    ShowMessage("ไม่พบข้อมูลเงินเดือน", "error");
                    Response.Redirect("PayrollManagement.aspx");
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, "error");
            }
        }

        private void LoadOTDetails()
        {
            try
            {
                DataTable dt = payrollService.GetOTDetails(payrollRecordId);

                if (dt != null && dt.Rows.Count > 0)
                {
                    gvOTDetails.DataSource = dt;
                    gvOTDetails.DataBind();
                    pnlOTDetails.Visible = true;
                }
            }
            catch { }
        }

        #endregion

        #region Event Handlers

        protected void btnGenerateVoucher_Click(object sender, EventArgs e)
        {
            try
            {
                short? adminId = GetAdminID();
                if (!adminId.HasValue)
                {
                    ShowMessage("ไม่พบข้อมูลผู้ใช้", "error");
                    return;
                }

                // Generate proper payment voucher with tracking
                var result = payrollService.GeneratePayrollVoucher(payrollRecordId, adminId.Value, false);

                if (result.Success)
                {
                    ShowMessage(result.Message, "success");
                    btnGenerateVoucher.Visible = false;
                    // Reload to show updated voucher info
                    LoadPayrollRecord();
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

        private string FormatCurrency(object value)
        {
            if (value == null || value == DBNull.Value)
                return "0.00";

            return string.Format("{0:N2}", Convert.ToDecimal(value));
        }

        private void ShowMessage(string message, string type)
        {
            string script = $"alert('{message.Replace("'", "\\'")}');";
            ClientScript.RegisterStartupScript(this.GetType(), "alert", script, true);
        }

        #endregion
    }
}
