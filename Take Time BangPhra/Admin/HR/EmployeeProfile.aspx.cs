using System;
using System.Data;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.HR
{
    public partial class EmployeeProfile : System.Web.UI.Page
    {
        private EmployeeService employeeService;
        private LeaveService leaveService;
        private PayrollService payrollService;
        private SignatureService signatureService;
        private short adminId = 0;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.HrEmployee)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            employeeService = new EmployeeService();
            leaveService = new LeaveService();
            payrollService = new PayrollService();
            signatureService = new SignatureService();

            if (!IsPostBack)
            {
                CheckAdminLogin();
                LoadEmployeeData();
                LoadSignature();
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

            // Get employee ID from query string
            if (!string.IsNullOrEmpty(Request.QueryString["id"]))
            {
                short.TryParse(Request.QueryString["id"], out adminId);
            }

            if (adminId == 0)
            {
                Response.Redirect("EmployeeManagement.aspx");
                return;
            }
        }

        #endregion

        #region Load Data

        private void LoadEmployeeData()
        {
            try
            {
                // Load basic profile
                DataTable dtProfile = employeeService.GetEmployeeCompleteProfile(adminId);

                if (dtProfile != null && dtProfile.Rows.Count > 0)
                {
                    DataRow profile = dtProfile.Rows[0];

                    // Basic info
                    lblName.Text = profile["Name"]?.ToString() ?? "N/A";
                    lblPosition.Text = profile["CurrentPosition"]?.ToString() ?? "N/A";
                    lblStatus.Text = profile["IsActive"] != DBNull.Value && Convert.ToBoolean(profile["IsActive"]) ? "Active" : "Inactive";
                    lblStatus.CssClass = profile["IsActive"] != DBNull.Value && Convert.ToBoolean(profile["IsActive"]) ? "status-badge status-active" : "status-badge status-inactive";

                    // Quick stats
                    lblServiceYears.Text = profile["TotalServiceYears"]?.ToString() ?? "0";
                    lblTrainingCount.Text = "0"; // Will be updated with actual count

                    // Personal info
                    lblEmployeeCode.Text = profile["EmployeeCode"]?.ToString() ?? "-";
                    lblFullNameTH.Text = profile["FullNameTH"]?.ToString() ?? "-";
                    lblFullNameEN.Text = profile["FullNameEN"]?.ToString() ?? "-";
                    lblIDCard.Text = MaskIDCard(profile["IDCardNumber"]?.ToString());
                    lblBirthDate.Text = profile["BirthDate"] != DBNull.Value ? Convert.ToDateTime(profile["BirthDate"]).ToString("dd/MM/yyyy") : "-";

                    // Contact info
                    lblPhone.Text = profile["MobilePhone"]?.ToString() ?? "-";
                    lblEmail.Text = profile["Email"]?.ToString() ?? "-";
                    lblAddress.Text = FormatAddress(profile);

                    // Emergency contact
                    lblEmergencyContact.Text = profile["EmergencyContactName"]?.ToString() ?? "-";
                    lblEmergencyPhone.Text = profile["EmergencyContactPhone"]?.ToString() ?? "-";

                    // Employment info
                    lblStartDate.Text = profile["HireDate"] != DBNull.Value ? Convert.ToDateTime(profile["HireDate"]).ToString("dd/MM/yyyy") : "-";
                    lblDepartment.Text = profile["Department"]?.ToString() ?? "-";
                    lblCurrentPosition.Text = profile["CurrentPosition"]?.ToString() ?? "-";
                    lblContractType.Text = profile["ContractType"]?.ToString() ?? "ประจำ";
                    lblContractEnd.Text = profile["ContractEndDate"] != DBNull.Value ? Convert.ToDateTime(profile["ContractEndDate"]).ToString("dd/MM/yyyy") : "ไม่มีกำหนด";

                    // Salary info
                    lblSalary.Text = profile["CurrentSalary"] != DBNull.Value ? string.Format("{0:N2}", profile["CurrentSalary"]) : "0.00";
                    lblSalaryEffective.Text = profile["SalaryEffectiveDate"] != DBNull.Value ? Convert.ToDateTime(profile["SalaryEffectiveDate"]).ToString("dd/MM/yyyy") : "-";
                }

                // Load employment history
                LoadEmploymentHistory();

                // Load salary history
                LoadSalaryHistory();

                // Load leave quota
                LoadLeaveQuota();

                // Load training history
                LoadTrainingHistory();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, "error");
            }
        }

        private void LoadEmploymentHistory()
        {
            try
            {
                DataTable dt = employeeService.GetEmploymentHistory(adminId);
                gvEmploymentHistory.DataSource = dt;
                gvEmploymentHistory.DataBind();
            }
            catch { }
        }

        private void LoadSalaryHistory()
        {
            try
            {
                DataTable dt = payrollService.GetEmployeeSalary(adminId);
                gvSalaryHistory.DataSource = dt;
                gvSalaryHistory.DataBind();
            }
            catch { }
        }

        private void LoadLeaveQuota()
        {
            try
            {
                DataTable dt = leaveService.GetEmployeeLeaveQuota(adminId);
                gvLeaveQuota.DataSource = dt;
                gvLeaveQuota.DataBind();

                // Calculate total remaining leave days
                decimal totalRemaining = 0;
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        if (row["RemainingDays"] != DBNull.Value)
                        {
                            totalRemaining += Convert.ToDecimal(row["RemainingDays"]);
                        }
                    }
                }
                lblLeaveBalance.Text = totalRemaining.ToString("N1");
            }
            catch { }
        }

        private void LoadTrainingHistory()
        {
            try
            {
                DataTable dt = employeeService.GetEmployeeTrainingRecords(adminId);
                gvTraining.DataSource = dt;
                gvTraining.DataBind();

                if (dt != null)
                {
                    lblTrainingCount.Text = dt.Rows.Count.ToString();
                }
            }
            catch { }
        }

        #endregion

        #region Helper Methods

        protected string GetProfilePhoto()
        {
            try
            {
                DataTable dt = employeeService.GetEmployeeCompleteProfile(adminId);
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0]["PhotoPath"] != DBNull.Value)
                {
                    return ResolveUrl("~/" + dt.Rows[0]["PhotoPath"].ToString());
                }
            }
            catch { }

            return ResolveUrl("~/Images/default-avatar.png");
        }

        private string MaskIDCard(string idCard)
        {
            if (string.IsNullOrEmpty(idCard) || idCard.Length < 13)
                return "-";

            return idCard.Substring(0, 3) + "-XXXX-XXXXX-" + idCard.Substring(12);
        }

        private string FormatAddress(DataRow profile)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (profile["Address"] != DBNull.Value && !string.IsNullOrEmpty(profile["Address"].ToString()))
                parts.Add(profile["Address"].ToString());
            if (profile["SubDistrict"] != DBNull.Value && !string.IsNullOrEmpty(profile["SubDistrict"].ToString()))
                parts.Add("ต." + profile["SubDistrict"].ToString());
            if (profile["District"] != DBNull.Value && !string.IsNullOrEmpty(profile["District"].ToString()))
                parts.Add("อ." + profile["District"].ToString());
            if (profile["Province"] != DBNull.Value && !string.IsNullOrEmpty(profile["Province"].ToString()))
                parts.Add("จ." + profile["Province"].ToString());
            if (profile["PostalCode"] != DBNull.Value && !string.IsNullOrEmpty(profile["PostalCode"].ToString()))
                parts.Add(profile["PostalCode"].ToString());

            return parts.Count > 0 ? string.Join(" ", parts) : "-";
        }

        private void ShowMessage(string message, string type)
        {
            string script = "alert('" + message.Replace("'", "\\'") + "');";
            ClientScript.RegisterStartupScript(this.GetType(), "alert", script, true);
        }

        #endregion

        #region Signature Management

        private void LoadSignature()
        {
            try
            {
                if (adminId == 0) return;

                bool hasSignature = signatureService.HasSignature(adminId);

                if (hasSignature)
                {
                    string signatureUrl = signatureService.GetSignatureUrl(adminId);
                    // Check if it's a base64 data URL or virtual path
                    if (signatureUrl.StartsWith("data:"))
                    {
                        // Base64 data URL - use directly without ResolveUrl
                        imgCurrentSignature.ImageUrl = signatureUrl;
                    }
                    else
                    {
                        // Virtual path - resolve it
                        imgCurrentSignature.ImageUrl = ResolveUrl(signatureUrl);
                    }
                    imgCurrentSignature.Visible = true;
                    currentSignatureSection.Visible = true;
                    noSignatureSection.Visible = false;
                    lblSignatureStatus.Text = "ลายเซ็นปัจจุบัน";
                    btnDeleteSignature.Visible = true;
                }
                else
                {
                    currentSignatureSection.Visible = false;
                    noSignatureSection.Visible = true;
                    btnDeleteSignature.Visible = false;
                }
            }
            catch (Exception ex)
            {
                lblSignatureMessage.Text = "ไม่สามารถโหลดลายเซ็นได้: " + ex.Message;
                lblSignatureMessage.Style["background"] = "#f8d7da";
                lblSignatureMessage.Style["color"] = "#721c24";
            }
        }

        protected void btnUploadSignature_Click(object sender, EventArgs e)
        {
            try
            {
                if (!fuSignature.HasFile)
                {
                    ShowSignatureMessage("กรุณาเลือกไฟล์ลายเซ็น", false);
                    return;
                }

                string message;
                bool success = signatureService.UploadSignature(adminId, fuSignature.PostedFile, out message);

                if (success)
                {
                    ShowSignatureMessage("อัพโหลดลายเซ็นสำเร็จ", true);
                    LoadSignature();
                }
                else
                {
                    ShowSignatureMessage(message, false);
                }
            }
            catch (Exception ex)
            {
                ShowSignatureMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        protected void btnDeleteSignature_Click(object sender, EventArgs e)
        {
            try
            {
                string message;
                bool success = signatureService.DeleteSignature(adminId, out message);

                if (success)
                {
                    ShowSignatureMessage("ลบลายเซ็นสำเร็จ", true);
                    LoadSignature();
                }
                else
                {
                    ShowSignatureMessage(message, false);
                }
            }
            catch (Exception ex)
            {
                ShowSignatureMessage("เกิดข้อผิดพลาด: " + ex.Message, false);
            }
        }

        private void ShowSignatureMessage(string message, bool isSuccess)
        {
            lblSignatureMessage.Text = message;
            if (isSuccess)
            {
                lblSignatureMessage.Style["background"] = "#d4edda";
                lblSignatureMessage.Style["color"] = "#155724";
            }
            else
            {
                lblSignatureMessage.Style["background"] = "#f8d7da";
                lblSignatureMessage.Style["color"] = "#721c24";
            }
        }

        #endregion
    }
}
