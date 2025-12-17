using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin.HR
{
    public partial class EmployeeManagement : System.Web.UI.Page
    {
        private EmployeeService employeeService;
        private string connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            employeeService = new EmployeeService();

            if (!IsPostBack)
            {
                CheckAdminLogin();
                LoadStatistics();
                LoadEmployees();
            }
        }

        #region Authentication

        private void CheckAdminLogin()
        {
            // Check permissions
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("/Default");
                return;
            }

            // Check user role - HR pages should be restricted to Owner and Admin
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

        #region Load Data

        private void LoadStatistics()
        {
            try
            {
                DataTable stats = employeeService.GetHRDashboardStats();

                if (stats != null && stats.Rows.Count > 0)
                {
                    DataRow row = stats.Rows[0];
                    lblTotalEmployees.Text = row["TotalActiveEmployees"].ToString();
                    lblNewEmployees.Text = row["NewEmployeesThisMonth"].ToString();
                    lblExpiringContracts.Text = row["ContractsExpiringWithin30Days"].ToString();
                    lblExpiringDocuments.Text = row["DocumentsExpiringWithin30Days"].ToString();
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดสถิติ: " + ex.Message, "error");
            }
        }

        private void LoadEmployees()
        {
            try
            {
                string searchTerm = txtSearch.Text.Trim();
                string department = ddlDepartment.SelectedValue;
                string status = ddlStatus.SelectedValue;

                DataTable dt = SearchEmployeesWithStatus(searchTerm, department, status);
                gvEmployees.DataSource = dt;
                gvEmployees.DataBind();
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูลพนักงาน: " + ex.Message, "error");
            }
        }

        private DataTable SearchEmployeesWithStatus(string searchTerm, string department, string status)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        SELECT
                            A.ID AS Admin_ID,
                            ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS Name,
                            ISNULL(ES.Position, A.Role) AS CurrentPosition,
                            A.Role AS Department,
                            A.Username AS MobilePhone,
                            A.Username AS WorkEmail,
                            ES.MonthlySalary AS CurrentSalary,
                            NULL AS PhotoPath,
                            0 AS TotalServiceYears,
                            0 AS TotalServiceMonths,
                            NULL AS ContractDaysUntilExpiry,
                            A.Status
                        FROM Admin A
                        LEFT JOIN Employee_Salary ES ON ES.Admin_ID = A.ID AND ES.IsActive = 1
                        WHERE 1=1
                          AND (@Status = '' OR A.Status = @StatusInt)
                          AND (@SearchTerm = '' OR A.FirstName LIKE '%' + @SearchTerm + '%'
                               OR A.LastName LIKE '%' + @SearchTerm + '%'
                               OR A.Username LIKE '%' + @SearchTerm + '%')
                          AND (@Department = '' OR A.Role = @Department)
                        ORDER BY A.Status DESC, A.FirstName, A.LastName";

                    cmd.Parameters.AddWithValue("@SearchTerm", searchTerm ?? "");
                    cmd.Parameters.AddWithValue("@Department", department ?? "");
                    cmd.Parameters.AddWithValue("@Status", status ?? "");
                    cmd.Parameters.AddWithValue("@StatusInt", string.IsNullOrEmpty(status) ? 1 : Convert.ToInt32(status));

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }

        #endregion

        #region Event Handlers

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            LoadEmployees();
        }

        protected void btnReset_Click(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            ddlDepartment.SelectedIndex = 0;
            ddlStatus.SelectedIndex = 0;
            LoadEmployees();
        }

        protected void gvEmployees_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ViewProfile")
            {
                short adminId = Convert.ToInt16(e.CommandArgument);
                Response.Redirect($"EmployeeProfile.aspx?id={adminId}");
            }
            else if (e.CommandName == "Resign")
            {
                short adminId = Convert.ToInt16(e.CommandArgument);
                ResignEmployee(adminId);
            }
        }

        protected void btnSaveEmployee_Click(object sender, EventArgs e)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(txtUsername.Text))
                {
                    ShowMessage("กรุณากรอกชื่อผู้ใช้", "error");
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtPassword.Text))
                {
                    ShowMessage("กรุณากรอกรหัสผ่าน", "error");
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtFirstName.Text))
                {
                    ShowMessage("กรุณากรอกชื่อ", "error");
                    return;
                }

                // Check if username exists
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Check duplicate username
                    using (SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM Admin WHERE Username = @Username", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Username", txtUsername.Text.Trim().ToLower());
                        int count = (int)checkCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            ShowMessage("ชื่อผู้ใช้นี้มีอยู่แล้ว กรุณาใช้ชื่ออื่น", "error");
                            return;
                        }
                    }

                    // Insert new employee
                    using (SqlCommand cmd = new SqlCommand(@"
                        INSERT INTO Admin (Username, Password, FirstName, LastName, Role, Status)
                        VALUES (@Username, @Password, @FirstName, @LastName, @Role, 1);
                        SELECT SCOPE_IDENTITY();", conn))
                    {
                        cmd.Parameters.AddWithValue("@Username", txtUsername.Text.Trim().ToLower());
                        cmd.Parameters.AddWithValue("@Password", txtPassword.Text);
                        cmd.Parameters.AddWithValue("@FirstName", txtFirstName.Text.Trim());
                        cmd.Parameters.AddWithValue("@LastName", txtLastName.Text.Trim());
                        cmd.Parameters.AddWithValue("@Role", ddlRole.SelectedValue);

                        int newAdminId = Convert.ToInt32(cmd.ExecuteScalar());

                        // Add salary if provided
                        if (!string.IsNullOrWhiteSpace(txtSalary.Text))
                        {
                            decimal salary;
                            if (decimal.TryParse(txtSalary.Text, out salary) && salary > 0)
                            {
                                using (SqlCommand salaryCmd = new SqlCommand(@"
                                    INSERT INTO Employee_Salary (Admin_ID, MonthlySalary, Position, EffectiveDate, IsActive)
                                    VALUES (@AdminID, @Salary, @Position, GETDATE(), 1)", conn))
                                {
                                    salaryCmd.Parameters.AddWithValue("@AdminID", newAdminId);
                                    salaryCmd.Parameters.AddWithValue("@Salary", salary);
                                    salaryCmd.Parameters.AddWithValue("@Position", txtPosition.Text.Trim());
                                    salaryCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }

                // Clear form and reload
                ClearAddForm();
                LoadStatistics();
                LoadEmployees();
                ShowMessage("เพิ่มพนักงานใหม่สำเร็จ", "success");
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void btnConfirmResign_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(hdnEmployeeId.Value))
                {
                    ShowMessage("ไม่พบข้อมูลพนักงาน", "error");
                    return;
                }

                short adminId = Convert.ToInt16(hdnEmployeeId.Value);
                ResignEmployee(adminId);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        private void ResignEmployee(short adminId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Update employee status to inactive
                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE Admin SET Status = 0 WHERE ID = @AdminID", conn))
                    {
                        cmd.Parameters.AddWithValue("@AdminID", adminId);
                        cmd.ExecuteNonQuery();
                    }

                    // Deactivate salary
                    using (SqlCommand salaryCmd = new SqlCommand(@"
                        UPDATE Employee_Salary SET IsActive = 0 WHERE Admin_ID = @AdminID", conn))
                    {
                        salaryCmd.Parameters.AddWithValue("@AdminID", adminId);
                        salaryCmd.ExecuteNonQuery();
                    }
                }

                LoadStatistics();
                LoadEmployees();
                ShowMessage("บันทึกการลาออกสำเร็จ", "success");
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        private void ClearAddForm()
        {
            txtUsername.Text = "";
            txtPassword.Text = "";
            txtFirstName.Text = "";
            txtLastName.Text = "";
            txtSalary.Text = "";
            txtPosition.Text = "";
            ddlRole.SelectedIndex = 0;
        }

        #endregion

        #region Helper Methods

        protected string GetServiceAgeText(object years, object months)
        {
            try
            {
                if (years == DBNull.Value || months == DBNull.Value)
                    return "N/A";

                int totalYears = Convert.ToInt32(years);
                int totalMonths = Convert.ToInt32(months);
                int remainingMonths = totalMonths % 12;

                return $"{totalYears} ปี {remainingMonths} เดือน";
            }
            catch
            {
                return "N/A";
            }
        }

        protected string GetContractStatusBadge(object daysUntilExpiry)
        {
            try
            {
                if (daysUntilExpiry == DBNull.Value)
                    return "<span class='badge badge-success'>ไม่มีกำหนด</span>";

                int days = Convert.ToInt32(daysUntilExpiry);

                if (days < 0)
                    return "<span class='badge badge-danger'>หมดอายุแล้ว</span>";
                else if (days <= 30)
                    return $"<span class='badge badge-danger'>เหลือ {days} วัน</span>";
                else if (days <= 90)
                    return $"<span class='badge badge-warning'>เหลือ {days} วัน</span>";
                else
                    return $"<span class='badge badge-success'>เหลือ {days} วัน</span>";
            }
            catch
            {
                return "<span class='badge badge-success'>N/A</span>";
            }
        }

        private void ShowMessage(string message, string type)
        {
            pnlMessage.Visible = true;
            lblMessage.Text = message;

            if (type == "success")
            {
                pnlMessage.Style["background"] = "#d4edda";
                pnlMessage.Style["color"] = "#155724";
                pnlMessage.Style["border"] = "1px solid #c3e6cb";
            }
            else
            {
                pnlMessage.Style["background"] = "#f8d7da";
                pnlMessage.Style["color"] = "#721c24";
                pnlMessage.Style["border"] = "1px solid #f5c6cb";
            }
        }

        #endregion
    }
}
