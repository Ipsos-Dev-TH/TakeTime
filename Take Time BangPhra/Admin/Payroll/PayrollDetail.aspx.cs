using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Web.UI;
using Microsoft.Reporting.WebForms;

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

        #region PDF Generation

        protected void btnPrintVoucher_Click(object sender, EventArgs e)
        {
            try
            {
                GeneratePayrollVoucherPDF();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการสร้าง PDF: " + ex.Message, "error");
            }
        }

        private void GeneratePayrollVoucherPDF()
        {
            string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
            code codeHelper = new code();

            // Get payroll record data
            DataTable dtPayroll = payrollService.GetPayrollRecord(payrollRecordId);
            if (dtPayroll == null || dtPayroll.Rows.Count == 0)
            {
                ShowMessage("ไม่พบข้อมูลเงินเดือน", "error");
                return;
            }

            DataRow record = dtPayroll.Rows[0];

            // 1. Create CompanyInfo DataTable
            DataTable dtCompanyInfo = new DataTable("CompanyInfo");
            dtCompanyInfo.Columns.Add("Company_Name", typeof(string));
            dtCompanyInfo.Columns.Add("Address", typeof(string));
            dtCompanyInfo.Columns.Add("Phone_Number", typeof(string));
            dtCompanyInfo.Columns.Add("LegalEntity_Number", typeof(string));

            DataTable dtBusiness = codeHelper.DatabaseQuery(conn, "SELECT * FROM Business_Info");
            if (dtBusiness.Rows.Count > 0)
            {
                dtCompanyInfo.Rows.Add(
                    dtBusiness.Rows[0]["Company_Name"]?.ToString() ?? "",
                    dtBusiness.Rows[0]["Address"]?.ToString() ?? "",
                    dtBusiness.Rows[0]["Phone_Number"]?.ToString() ?? "",
                    dtBusiness.Rows[0]["LegalEntity_Number"]?.ToString() ?? ""
                );
            }
            else
            {
                dtCompanyInfo.Rows.Add("Take Time BangPhra", "", "", "");
            }

            // 2. Create EmployeeInfo DataTable
            DataTable dtEmployeeInfo = new DataTable("EmployeeInfo");
            dtEmployeeInfo.Columns.Add("EmployeeName", typeof(string));
            dtEmployeeInfo.Columns.Add("Position", typeof(string));
            dtEmployeeInfo.Columns.Add("EmployeeID", typeof(string));
            dtEmployeeInfo.Columns.Add("Department", typeof(string));

            dtEmployeeInfo.Rows.Add(
                record["EmployeeName"]?.ToString() ?? "-",
                record["Position"]?.ToString() ?? "-",
                record["Admin_ID"]?.ToString() ?? "-",
                "-" // Department
            );

            // 3. Create PayrollDetails DataTable
            DataTable dtPayrollDetails = new DataTable("PayrollDetails");
            dtPayrollDetails.Columns.Add("VoucherNumber", typeof(string));
            dtPayrollDetails.Columns.Add("PeriodName", typeof(string));
            dtPayrollDetails.Columns.Add("PayDate", typeof(string));
            dtPayrollDetails.Columns.Add("BaseSalary", typeof(decimal));
            dtPayrollDetails.Columns.Add("OTAmount", typeof(decimal));
            dtPayrollDetails.Columns.Add("BonusAmount", typeof(decimal));
            dtPayrollDetails.Columns.Add("AllowanceAmount", typeof(decimal));
            dtPayrollDetails.Columns.Add("TotalEarnings", typeof(decimal));
            dtPayrollDetails.Columns.Add("LeaveDeduction", typeof(decimal));
            dtPayrollDetails.Columns.Add("SocialSecurity", typeof(decimal));
            dtPayrollDetails.Columns.Add("Tax", typeof(decimal));
            dtPayrollDetails.Columns.Add("OtherDeductions", typeof(decimal));
            dtPayrollDetails.Columns.Add("TotalDeductions", typeof(decimal));
            dtPayrollDetails.Columns.Add("NetSalary", typeof(decimal));
            dtPayrollDetails.Columns.Add("WorkDays", typeof(int));
            dtPayrollDetails.Columns.Add("LeaveDays", typeof(decimal));
            dtPayrollDetails.Columns.Add("OTHours", typeof(decimal));

            string voucherNumber = record["VoucherNumber"] != DBNull.Value ? record["VoucherNumber"].ToString() : "PV-" + DateTime.Now.ToString("yyMMdd") + "-XXXX";
            string payDate = DateTime.Now.ToString("dd/MM/yyyy");

            dtPayrollDetails.Rows.Add(
                voucherNumber,
                record["PeriodName"]?.ToString() ?? "",
                payDate,
                record["BaseSalary"] != DBNull.Value ? Convert.ToDecimal(record["BaseSalary"]) : 0,
                record["OTAmount"] != DBNull.Value ? Convert.ToDecimal(record["OTAmount"]) : 0,
                record["BonusAmount"] != DBNull.Value ? Convert.ToDecimal(record["BonusAmount"]) : 0,
                record["AllowanceAmount"] != DBNull.Value ? Convert.ToDecimal(record["AllowanceAmount"]) : 0,
                record["TotalEarnings"] != DBNull.Value ? Convert.ToDecimal(record["TotalEarnings"]) : 0,
                record["LeaveDeduction"] != DBNull.Value ? Convert.ToDecimal(record["LeaveDeduction"]) : 0,
                record["SocialSecurity"] != DBNull.Value ? Convert.ToDecimal(record["SocialSecurity"]) : 0,
                record["Tax"] != DBNull.Value ? Convert.ToDecimal(record["Tax"]) : 0,
                record["OtherDeductions"] != DBNull.Value ? Convert.ToDecimal(record["OtherDeductions"]) : 0,
                record["TotalDeductions"] != DBNull.Value ? Convert.ToDecimal(record["TotalDeductions"]) : 0,
                record["NetSalary"] != DBNull.Value ? Convert.ToDecimal(record["NetSalary"]) : 0,
                record["WorkDays"] != DBNull.Value ? Convert.ToInt32(record["WorkDays"]) : 0,
                record["LeaveDays"] != DBNull.Value ? Convert.ToDecimal(record["LeaveDays"]) : 0,
                record["OTHours"] != DBNull.Value ? Convert.ToDecimal(record["OTHours"]) : 0
            );

            // 4. Create Signatures DataTable
            DataTable dtSignatures = new DataTable("Signatures");
            dtSignatures.Columns.Add("PreparedByName", typeof(string));
            dtSignatures.Columns.Add("PreparedBySignature", typeof(byte[]));
            dtSignatures.Columns.Add("ApprovedByName", typeof(string));
            dtSignatures.Columns.Add("ApprovedBySignature", typeof(byte[]));
            dtSignatures.Columns.Add("ReceivedByName", typeof(string));
            dtSignatures.Columns.Add("ReceivedBySignature", typeof(byte[]));

            // Get current admin name for prepared by
            string preparedByName = Session["UserName"]?.ToString() ?? "";
            string employeeName = record["EmployeeName"]?.ToString() ?? "";

            dtSignatures.Rows.Add(
                preparedByName,
                null,
                "",
                null,
                employeeName,
                null
            );

            // Create LocalReport and render to PDF
            LocalReport localReport = new LocalReport();
            localReport.ReportPath = Server.MapPath("~/Admin/Payroll/Report/PayrollVoucher.rdlc");

            localReport.DataSources.Clear();
            localReport.DataSources.Add(new ReportDataSource("CompanyInfo", dtCompanyInfo));
            localReport.DataSources.Add(new ReportDataSource("EmployeeInfo", dtEmployeeInfo));
            localReport.DataSources.Add(new ReportDataSource("PayrollDetails", dtPayrollDetails));
            localReport.DataSources.Add(new ReportDataSource("Signatures", dtSignatures));

            // Render PDF
            Warning[] warnings;
            string[] streamIds;
            string mimeType;
            string encoding;
            string extension;

            byte[] bytes = localReport.Render(
                "PDF",
                null,
                out mimeType,
                out encoding,
                out extension,
                out streamIds,
                out warnings
            );

            // Send PDF to browser
            Response.Clear();
            Response.ContentType = mimeType;
            Response.AddHeader("Content-Disposition", $"attachment; filename=PayrollVoucher_{voucherNumber.Replace("/", "-")}_{record["EmployeeName"]?.ToString().Replace(" ", "_")}.pdf");
            Response.BinaryWrite(bytes);
            Response.Flush();
            Response.End();
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
