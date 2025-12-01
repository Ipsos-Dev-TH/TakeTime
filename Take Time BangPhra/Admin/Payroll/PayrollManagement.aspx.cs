using System;
using System.Data;
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
                            break;
                        }
                    }
                }

                if (periodRow != null)
                {
                    // Payroll exists - load records
                    LoadPayrollRecords(currentPayrollPeriodId);
                    LoadStatistics(periodRow);

                    string status = periodRow["Status"].ToString();
                    btnGeneratePayroll.Visible = false;
                    btnApprovePayroll.Visible = (status == "DRAFT");
                    pnlStats.Visible = true;
                }
                else
                {
                    // No payroll yet
                    gvPayroll.DataSource = null;
                    gvPayroll.DataBind();
                    btnGeneratePayroll.Visible = true;
                    btnApprovePayroll.Visible = false;
                    pnlStats.Visible = false;
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

        private void LoadStatistics(DataRow periodRow)
        {
            try
            {
                lblTotalEmployees.Text = periodRow["TotalEmployees"].ToString();
                lblTotalGrossPay.Text = string.Format("{0:N2}", periodRow["TotalGrossPay"]);
                lblTotalDeductions.Text = string.Format("{0:N2}", periodRow["TotalDeductions"]);
                lblTotalNetPay.Text = string.Format("{0:N2}", periodRow["TotalNetPay"]);
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

                if (currentPayrollPeriodId > 0)
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

        protected void gvPayroll_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ViewDetails")
            {
                long payrollRecordId = Convert.ToInt64(e.CommandArgument);
                Response.Redirect($"PayrollDetail.aspx?id={payrollRecordId}");
            }
        }

        #endregion

        #region Helper Methods

        protected string GetVoucherStatusBadge(object voucherGenerated, object voucherNumber)
        {
            try
            {
                bool generated = voucherGenerated != DBNull.Value && Convert.ToBoolean(voucherGenerated);

                if (generated)
                {
                    string number = voucherNumber?.ToString() ?? "";
                    return $"<span class='badge badge-approved'>✓ {number}</span>";
                }
                else
                {
                    return "<span class='badge badge-pending'>รอสร้าง</span>";
                }
            }
            catch
            {
                return "<span class='badge badge-draft'>-</span>";
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
