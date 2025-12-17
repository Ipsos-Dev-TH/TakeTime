using System;
using System.Data;
using System.IO;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.Leave
{
    public partial class LeaveRequest : System.Web.UI.Page
    {
        private LeaveService leaveService;
        private SupervisorService supervisorService;
        private short currentAdminId = 0;
        private short targetAdminId = 0; // Employee ID being processed (self or subordinate)

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
                GetTargetAdminId();
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

        private void GetTargetAdminId()
        {
            // If supervisor mode and subordinate selected, use subordinate's ID
            if (pnlSupervisorMode.Visible && !string.IsNullOrEmpty(ddlSubordinates.SelectedValue))
            {
                targetAdminId = Convert.ToInt16(ddlSubordinates.SelectedValue);
            }
            else
            {
                targetAdminId = currentAdminId;
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
                ddlSubordinates.Items.Clear();
                ddlSubordinates.Items.Add(new System.Web.UI.WebControls.ListItem("-- ขอลาให้ตัวเอง --", currentAdminId.ToString()));

                foreach (DataRow row in subordinates.Rows)
                {
                    string text = row["EmployeeName"].ToString();
                    if (row["Position"] != DBNull.Value && !string.IsNullOrEmpty(row["Position"].ToString()))
                    {
                        text += " (" + row["Position"].ToString() + ")";
                    }
                    ddlSubordinates.Items.Add(new System.Web.UI.WebControls.ListItem(text, row["Employee_AdminID"].ToString()));
                }
            }

            // Default target is self
            targetAdminId = currentAdminId;

            LoadLeaveTypes();
            LoadEmployeeInfo();
            LoadLeaveQuota();
            LoadMyLeaveRequests();

            // Set current year
            lblYear.Text = (DateTime.Now.Year + 543).ToString();

            // Set default dates
            txtStartDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtEndDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
        }

        private void LoadLeaveTypes()
        {
            try
            {
                DataTable dt = leaveService.GetLeaveTypes();
                ddlLeaveType.Items.Clear();
                ddlLeaveType.Items.Add(new System.Web.UI.WebControls.ListItem("-- เลือกประเภทการลา --", ""));

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        string text = row["LeaveTypeName"].ToString();
                        if (Convert.ToBoolean(row["DeductSalary"]))
                        {
                            text += " (หักเงิน)";
                        }
                        ddlLeaveType.Items.Add(new System.Web.UI.WebControls.ListItem(text, row["ID"].ToString()));
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดประเภทการลา: " + ex.Message, "error");
            }
        }

        private void LoadEmployeeInfo()
        {
            try
            {
                EmployeeService empService = new EmployeeService();
                DataTable dt = empService.GetEmployeeCompleteProfile(targetAdminId);

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

        private void LoadLeaveQuota()
        {
            try
            {
                DataTable dt = leaveService.GetEmployeeLeaveQuota(targetAdminId);
                gvLeaveQuota.DataSource = dt;
                gvLeaveQuota.DataBind();

                // Calculate total remaining
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
                lblTotalRemainingDays.Text = totalRemaining.ToString("N1");
            }
            catch
            {
                lblTotalRemainingDays.Text = "0";
            }
        }

        private void LoadMyLeaveRequests()
        {
            try
            {
                DataTable dt = leaveService.GetLeaveRequestsForEmployee(targetAdminId, (short)DateTime.Now.Year);
                gvMyLeaveRequests.DataSource = dt;
                gvMyLeaveRequests.DataBind();

                // Count pending requests
                int pendingCount = 0;
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        if (row["Status"].ToString() == "PENDING")
                        {
                            pendingCount++;
                        }
                    }
                }
                lblPendingRequests.Text = pendingCount.ToString();
            }
            catch { }
        }

        #endregion

        #region Event Handlers

        protected void ddlSubordinates_SelectedIndexChanged(object sender, EventArgs e)
        {
            GetTargetAdminId();
            LoadEmployeeInfo();
            LoadLeaveQuota();
            LoadMyLeaveRequests();
        }

        protected void btnSubmit_Click(object sender, EventArgs e)
        {
            try
            {
                GetTargetAdminId();

                // Validate
                if (string.IsNullOrEmpty(ddlLeaveType.SelectedValue))
                {
                    ShowMessage("กรุณาเลือกประเภทการลา", "error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtStartDate.Text))
                {
                    ShowMessage("กรุณาเลือกวันที่เริ่มต้น", "error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtEndDate.Text))
                {
                    ShowMessage("กรุณาเลือกวันที่สิ้นสุด", "error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtReason.Text))
                {
                    ShowMessage("กรุณาระบุเหตุผลการลา", "error");
                    return;
                }

                DateTime startDate = DateTime.Parse(txtStartDate.Text);
                DateTime endDate = DateTime.Parse(txtEndDate.Text);

                if (endDate < startDate)
                {
                    ShowMessage("วันที่สิ้นสุดต้องมากกว่าหรือเท่ากับวันที่เริ่มต้น", "error");
                    return;
                }

                decimal totalDays = 1;
                if (!decimal.TryParse(txtTotalDays.Text, out totalDays) || totalDays <= 0)
                {
                    ShowMessage("กรุณาระบุจำนวนวันลาให้ถูกต้อง", "error");
                    return;
                }

                byte leaveTypeId = Convert.ToByte(ddlLeaveType.SelectedValue);
                string reason = txtReason.Text.Trim();

                // Handle file upload
                string medicalCertPath = null;
                if (fuMedicalCert.HasFile)
                {
                    string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };
                    string fileExt = Path.GetExtension(fuMedicalCert.FileName).ToLower();

                    if (Array.IndexOf(allowedExtensions, fileExt) < 0)
                    {
                        ShowMessage("รองรับเฉพาะไฟล์ .jpg, .png, .pdf เท่านั้น", "error");
                        return;
                    }

                    if (fuMedicalCert.PostedFile.ContentLength > 5 * 1024 * 1024)
                    {
                        ShowMessage("ขนาดไฟล์ต้องไม่เกิน 5MB", "error");
                        return;
                    }

                    string fileName = $"MedCert_{targetAdminId}_{DateTime.Now:yyyyMMddHHmmss}{fileExt}";
                    string folderPath = Server.MapPath("~/Uploads/MedicalCerts/");

                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    string fullPath = Path.Combine(folderPath, fileName);
                    fuMedicalCert.SaveAs(fullPath);
                    medicalCertPath = "Uploads/MedicalCerts/" + fileName;
                }

                // Determine who is submitting
                short? submittedBy = null;
                if (targetAdminId != currentAdminId)
                {
                    submittedBy = currentAdminId; // Supervisor submitting on behalf of employee
                }

                // Create leave request
                var result = leaveService.CreateLeaveRequest(
                    targetAdminId, leaveTypeId, startDate, endDate,
                    totalDays, reason, medicalCertPath, submittedBy);

                if (result.Success)
                {
                    ShowMessage("ส่งคำขอลาสำเร็จ เลขที่คำขอ: " + result.Message, "success");
                    ClearForm();
                    LoadLeaveQuota();
                    LoadMyLeaveRequests();
                }
                else
                {
                    ShowMessage("เกิดข้อผิดพลาด: " + result.Message, "error");
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
            ddlLeaveType.SelectedIndex = 0;
            txtStartDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtEndDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtTotalDays.Text = "1";
            txtReason.Text = "";
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
