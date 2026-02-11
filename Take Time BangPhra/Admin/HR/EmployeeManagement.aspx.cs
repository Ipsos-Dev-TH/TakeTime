using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;
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
                            A.Username,
                            A.FirstName,
                            A.LastName,
                            ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS Name,
                            ISNULL(ES.Position, A.Role) AS CurrentPosition,
                            A.Role AS Department,
                            ES.MonthlySalary AS CurrentSalary,
                            ES.Position AS SalaryPosition,
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
            else if (e.CommandName == "EditEmployee")
            {
                short adminId = Convert.ToInt16(e.CommandArgument);
                LoadEmployeeForEdit(adminId);
            }
            else if (e.CommandName == "Reactivate")
            {
                short adminId = Convert.ToInt16(e.CommandArgument);
                ReactivateEmployee(adminId);
            }
            else if (e.CommandName == "DeleteEmployee")
            {
                short adminId = Convert.ToInt16(e.CommandArgument);
                DeleteEmployee(adminId);
            }
            else if (e.CommandName == "ViewSalaryHistory")
            {
                short adminId = Convert.ToInt16(e.CommandArgument);
                LoadSalaryHistory(adminId);
            }
            else if (e.CommandName == "ViewDocuments")
            {
                short adminId = Convert.ToInt16(e.CommandArgument);
                LoadEmployeeDocuments(adminId);
            }
        }

        private void LoadSalaryHistory(short adminId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Get employee name
                    string employeeName = "";
                    using (SqlCommand nameCmd = new SqlCommand(@"
                        SELECT ISNULL(FirstName + ' ' + LastName, Username) AS Name FROM Admin WHERE ID = @AdminID", conn))
                    {
                        nameCmd.Parameters.AddWithValue("@AdminID", adminId);
                        employeeName = nameCmd.ExecuteScalar()?.ToString() ?? "";
                    }

                    // Get salary history
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT MonthlySalary, Position, EffectiveDate, IsActive,
                               CASE WHEN IsActive = 1 THEN 'ปัจจุบัน' ELSE 'ประวัติ' END AS StatusText
                        FROM Employee_Salary
                        WHERE Admin_ID = @AdminID
                        ORDER BY EffectiveDate DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("@AdminID", adminId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var historyItems = new System.Collections.Generic.List<string>();
                            while (reader.Read())
                            {
                                decimal salary = reader["MonthlySalary"] != DBNull.Value ? Convert.ToDecimal(reader["MonthlySalary"]) : 0;
                                string position = reader["Position"]?.ToString() ?? "-";
                                DateTime effectiveDate = reader["EffectiveDate"] != DBNull.Value ? Convert.ToDateTime(reader["EffectiveDate"]) : DateTime.MinValue;
                                bool isActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"]);

                                string statusBadge = isActive
                                    ? "<span class=\"badge badge-success\">ปัจจุบัน</span>"
                                    : "<span class=\"badge badge-secondary\">ประวัติ</span>";

                                historyItems.Add($"<tr><td>{effectiveDate:dd/MM/yyyy}</td><td>{position}</td><td style=\"text-align:right\">฿{salary:N0}</td><td>{statusBadge}</td></tr>");
                            }

                            if (historyItems.Count > 0)
                            {
                                string tableHtml = string.Join("", historyItems);
                                string script = $@"openSalaryHistoryModal('{EscapeJsString(employeeName)}', '{EscapeJsString(tableHtml)}');";
                                ScriptManager.RegisterStartupScript(this, GetType(), "OpenSalaryHistory", script, true);
                            }
                            else
                            {
                                ShowMessage("ไม่พบประวัติเงินเดือน", "error");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        private void LoadEmployeeForEdit(short adminId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT A.ID, A.Username, A.Title, A.FirstName, A.LastName, A.Role,
                               A.Phone, A.Email, A.IDCard, A.Address, A.HireDate, A.BirthDate,
                               A.BankCode, A.BankAccountNumber, A.BankAccountName,
                               A.EmergencyContact, A.EmergencyPhone,
                               ES.MonthlySalary, ES.Position
                        FROM Admin A
                        LEFT JOIN Employee_Salary ES ON ES.Admin_ID = A.ID AND ES.IsActive = 1
                        WHERE A.ID = @AdminID", conn))
                    {
                        cmd.Parameters.AddWithValue("@AdminID", adminId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string username = reader["Username"]?.ToString() ?? "";
                                string title = reader["Title"]?.ToString() ?? "";
                                string firstName = reader["FirstName"]?.ToString() ?? "";
                                string lastName = reader["LastName"]?.ToString() ?? "";
                                string role = reader["Role"]?.ToString() ?? "Staff";
                                string salary = reader["MonthlySalary"] != DBNull.Value ? Convert.ToDecimal(reader["MonthlySalary"]).ToString("0") : "";
                                string position = reader["Position"]?.ToString() ?? "";

                                // Personal info
                                string idCard = reader["IDCard"]?.ToString() ?? "";
                                string birthDate = reader["BirthDate"] != DBNull.Value ? Convert.ToDateTime(reader["BirthDate"]).ToString("yyyy-MM-dd") : "";
                                string phone = reader["Phone"]?.ToString() ?? "";
                                string email = reader["Email"]?.ToString() ?? "";
                                string address = reader["Address"]?.ToString() ?? "";
                                string hireDate = reader["HireDate"] != DBNull.Value ? Convert.ToDateTime(reader["HireDate"]).ToString("yyyy-MM-dd") : "";

                                // Bank info
                                string bankCode = reader["BankCode"]?.ToString() ?? "";
                                string bankAccountNumber = reader["BankAccountNumber"]?.ToString() ?? "";
                                string bankAccountName = reader["BankAccountName"]?.ToString() ?? "";

                                // Emergency contact
                                string emergencyContact = reader["EmergencyContact"]?.ToString() ?? "";
                                string emergencyPhone = reader["EmergencyPhone"]?.ToString() ?? "";

                                // Load signature preview
                                string employeeFullName = string.IsNullOrEmpty(lastName) ? firstName : firstName + " " + lastName;
                                LoadSignaturePreview(employeeFullName);

                                // Register JavaScript to open modal with data
                                string script = $@"
                                    openEditModal('{adminId}', '{EscapeJsString(username)}', '{EscapeJsString(title)}', '{EscapeJsString(firstName)}',
                                                  '{EscapeJsString(lastName)}', '{EscapeJsString(role)}', '{salary}', '{EscapeJsString(position)}',
                                                  '{EscapeJsString(idCard)}', '{birthDate}', '{EscapeJsString(phone)}', '{EscapeJsString(email)}',
                                                  '{EscapeJsString(address)}', '{hireDate}',
                                                  '{EscapeJsString(bankCode)}', '{EscapeJsString(bankAccountNumber)}', '{EscapeJsString(bankAccountName)}',
                                                  '{EscapeJsString(emergencyContact)}', '{EscapeJsString(emergencyPhone)}');";
                                ScriptManager.RegisterStartupScript(this, GetType(), "OpenEditModal", script, true);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message, "error");
            }
        }

        private string EscapeJsString(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private void ReactivateEmployee(short adminId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Update employee status to active
                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE Admin SET Status = 1 WHERE ID = @AdminID", conn))
                    {
                        cmd.Parameters.AddWithValue("@AdminID", adminId);
                        cmd.ExecuteNonQuery();
                    }
                }

                LoadStatistics();
                LoadEmployees();
                ShowMessage("เรียกพนักงานกลับเข้าทำงานสำเร็จ", "success");
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        private void DeleteEmployee(short adminId)
        {
            try
            {
                // Don't allow deleting yourself
                short? currentUserId = GetAdminID();
                if (currentUserId.HasValue && currentUserId.Value == adminId)
                {
                    ShowMessage("ไม่สามารถลบบัญชีตัวเองได้", "error");
                    return;
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Use transaction to ensure data integrity
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Delete leave requests
                            using (SqlCommand leaveCmd = new SqlCommand(@"
                                DELETE FROM Leave_Requests WHERE Admin_ID = @AdminID", conn, transaction))
                            {
                                leaveCmd.Parameters.AddWithValue("@AdminID", adminId);
                                leaveCmd.ExecuteNonQuery();
                            }

                            // Delete leave quota
                            using (SqlCommand quotaCmd = new SqlCommand(@"
                                DELETE FROM Employee_Leave_Quota WHERE Admin_ID = @AdminID", conn, transaction))
                            {
                                quotaCmd.Parameters.AddWithValue("@AdminID", adminId);
                                quotaCmd.ExecuteNonQuery();
                            }

                            // Delete OT entries
                            using (SqlCommand otCmd = new SqlCommand(@"
                                DELETE FROM OT_Entry WHERE Admin_ID = @AdminID", conn, transaction))
                            {
                                otCmd.Parameters.AddWithValue("@AdminID", adminId);
                                otCmd.ExecuteNonQuery();
                            }

                            // Delete payroll records
                            using (SqlCommand payrollCmd = new SqlCommand(@"
                                DELETE FROM Payroll_Records WHERE Admin_ID = @AdminID", conn, transaction))
                            {
                                payrollCmd.Parameters.AddWithValue("@AdminID", adminId);
                                payrollCmd.ExecuteNonQuery();
                            }

                            // Delete salary records
                            using (SqlCommand salaryCmd = new SqlCommand(@"
                                DELETE FROM Employee_Salary WHERE Admin_ID = @AdminID", conn, transaction))
                            {
                                salaryCmd.Parameters.AddWithValue("@AdminID", adminId);
                                salaryCmd.ExecuteNonQuery();
                            }

                            // Delete supervisor relationships
                            using (SqlCommand supCmd = new SqlCommand(@"
                                DELETE FROM Employee_Supervisor WHERE Employee_AdminID = @AdminID OR Supervisor_AdminID = @AdminID", conn, transaction))
                            {
                                supCmd.Parameters.AddWithValue("@AdminID", adminId);
                                supCmd.ExecuteNonQuery();
                            }

                            // Delete admin record
                            using (SqlCommand cmd = new SqlCommand(@"
                                DELETE FROM Admin WHERE ID = @AdminID", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@AdminID", adminId);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                LoadStatistics();
                LoadEmployees();
                ShowMessage("ลบพนักงานสำเร็จ", "success");
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        protected void btnSaveEmployee_Click(object sender, EventArgs e)
        {
            try
            {
                bool isEditMode = hdnEditMode.Value == "edit";

                // Validate required fields
                if (!isEditMode && string.IsNullOrWhiteSpace(txtUsername.Text))
                {
                    ShowMessage("กรุณากรอกชื่อผู้ใช้", "error");
                    return;
                }
                if (!isEditMode && string.IsNullOrWhiteSpace(txtPassword.Text))
                {
                    ShowMessage("กรุณากรอกรหัสผ่าน", "error");
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtFirstName.Text))
                {
                    ShowMessage("กรุณากรอกชื่อ", "error");
                    return;
                }

                if (isEditMode)
                {
                    // Update existing employee
                    UpdateEmployee();
                }
                else
                {
                    // Add new employee
                    AddNewEmployee();
                }
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาด: " + ex.Message, "error");
            }
        }

        private void AddNewEmployee()
        {
            // Validate password strength
            var passwordValidation = SecurityHelper.ValidatePasswordStrength(txtPassword.Text);
            if (!passwordValidation.IsValid)
            {
                ShowMessage(passwordValidation.Message, "error");
                return;
            }

            // Validate salary if provided
            decimal salary = 0;
            if (!string.IsNullOrWhiteSpace(txtSalary.Text))
            {
                if (!decimal.TryParse(txtSalary.Text, out salary))
                {
                    ShowMessage("กรุณากรอกเงินเดือนเป็นตัวเลข", "error");
                    return;
                }
                if (salary < 0)
                {
                    ShowMessage("เงินเดือนต้องไม่ติดลบ", "error");
                    return;
                }
                if (salary > 0 && salary < HRConfiguration.MinimumWage)
                {
                    ShowMessage($"เงินเดือนต้องไม่ต่ำกว่าค่าแรงขั้นต่ำ ({HRConfiguration.MinimumWage:N0} บาท)", "error");
                    return;
                }
                if (salary > HRConfiguration.MaximumSalary)
                {
                    ShowMessage($"เงินเดือนเกินค่าสูงสุดที่กำหนด ({HRConfiguration.MaximumSalary:N0} บาท)", "error");
                    return;
                }
            }

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

                // Insert new employee (plain text password)
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO Admin (Username, Password, Title, FirstName, LastName, Role, Status,
                                       Phone, Email, IDCard, Address, HireDate, BirthDate,
                                       BankCode, BankAccountNumber, BankAccountName,
                                       EmergencyContact, EmergencyPhone)
                    VALUES (@Username, @Password, @Title, @FirstName, @LastName, @Role, 1,
                            @Phone, @Email, @IDCard, @Address, @HireDate, @BirthDate,
                            @BankCode, @BankAccountNumber, @BankAccountName,
                            @EmergencyContact, @EmergencyPhone);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@Username", txtUsername.Text.Trim().ToLower());
                    cmd.Parameters.AddWithValue("@Password", txtPassword.Text);
                    cmd.Parameters.AddWithValue("@Title", string.IsNullOrWhiteSpace(ddlTitle.SelectedValue) ? (object)DBNull.Value : ddlTitle.SelectedValue);
                    cmd.Parameters.AddWithValue("@FirstName", txtFirstName.Text.Trim());
                    cmd.Parameters.AddWithValue("@LastName", txtLastName.Text.Trim());
                    cmd.Parameters.AddWithValue("@Role", ddlRole.SelectedValue);

                    // Personal info
                    cmd.Parameters.AddWithValue("@Phone", string.IsNullOrWhiteSpace(txtPhone.Text) ? (object)DBNull.Value : txtPhone.Text.Trim());
                    cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(txtEmail.Text) ? (object)DBNull.Value : txtEmail.Text.Trim());
                    cmd.Parameters.AddWithValue("@IDCard", string.IsNullOrWhiteSpace(txtIDCard.Text) ? (object)DBNull.Value : txtIDCard.Text.Trim());
                    cmd.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(txtAddress.Text) ? (object)DBNull.Value : txtAddress.Text.Trim());

                    // Parse dates
                    DateTime hireDate;
                    cmd.Parameters.AddWithValue("@HireDate", DateTime.TryParse(txtHireDate.Text, out hireDate) ? (object)hireDate : DBNull.Value);
                    DateTime birthDate;
                    cmd.Parameters.AddWithValue("@BirthDate", DateTime.TryParse(txtBirthDate.Text, out birthDate) ? (object)birthDate : DBNull.Value);

                    // Bank info
                    cmd.Parameters.AddWithValue("@BankCode", string.IsNullOrWhiteSpace(ddlBank.SelectedValue) ? (object)DBNull.Value : ddlBank.SelectedValue);
                    cmd.Parameters.AddWithValue("@BankAccountNumber", string.IsNullOrWhiteSpace(txtBankAccountNumber.Text) ? (object)DBNull.Value : txtBankAccountNumber.Text.Trim());
                    cmd.Parameters.AddWithValue("@BankAccountName", string.IsNullOrWhiteSpace(txtBankAccountName.Text) ? (object)DBNull.Value : txtBankAccountName.Text.Trim());

                    // Emergency contact
                    cmd.Parameters.AddWithValue("@EmergencyContact", string.IsNullOrWhiteSpace(txtEmergencyContact.Text) ? (object)DBNull.Value : txtEmergencyContact.Text.Trim());
                    cmd.Parameters.AddWithValue("@EmergencyPhone", string.IsNullOrWhiteSpace(txtEmergencyPhone.Text) ? (object)DBNull.Value : txtEmergencyPhone.Text.Trim());

                    short newAdminId = Convert.ToInt16(cmd.ExecuteScalar());

                    // Add salary if provided
                    if (salary > 0)
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

            // Clear form and reload
            ClearAddForm();
            LoadStatistics();
            LoadEmployees();
            ShowMessage("เพิ่มพนักงานใหม่สำเร็จ", "success");
        }

        private void UpdateEmployee()
        {
            if (string.IsNullOrEmpty(hdnEmployeeId.Value))
            {
                ShowMessage("ไม่พบข้อมูลพนักงาน", "error");
                return;
            }

            // Validate password if provided
            if (!string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                var passwordValidation = SecurityHelper.ValidatePasswordStrength(txtPassword.Text);
                if (!passwordValidation.IsValid)
                {
                    ShowMessage(passwordValidation.Message, "error");
                    return;
                }
            }

            // Validate salary if provided
            decimal newSalary = 0;
            if (!string.IsNullOrWhiteSpace(txtSalary.Text))
            {
                if (!decimal.TryParse(txtSalary.Text, out newSalary))
                {
                    ShowMessage("กรุณากรอกเงินเดือนเป็นตัวเลข", "error");
                    return;
                }
                if (newSalary < 0)
                {
                    ShowMessage("เงินเดือนต้องไม่ติดลบ", "error");
                    return;
                }
                if (newSalary > 0 && newSalary < HRConfiguration.MinimumWage)
                {
                    ShowMessage($"เงินเดือนต้องไม่ต่ำกว่าค่าแรงขั้นต่ำ ({HRConfiguration.MinimumWage:N0} บาท)", "error");
                    return;
                }
                if (newSalary > HRConfiguration.MaximumSalary)
                {
                    ShowMessage($"เงินเดือนเกินค่าสูงสุดที่กำหนด ({HRConfiguration.MaximumSalary:N0} บาท)", "error");
                    return;
                }
            }

            short adminId = Convert.ToInt16(hdnEmployeeId.Value);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Update Admin record
                string updateSql = @"UPDATE Admin SET
                                  Title = @Title,
                                  FirstName = @FirstName,
                                  LastName = @LastName,
                                  Role = @Role,
                                  Phone = @Phone,
                                  Email = @Email,
                                  IDCard = @IDCard,
                                  Address = @Address,
                                  HireDate = @HireDate,
                                  BirthDate = @BirthDate,
                                  BankCode = @BankCode,
                                  BankAccountNumber = @BankAccountNumber,
                                  BankAccountName = @BankAccountName,
                                  EmergencyContact = @EmergencyContact,
                                  EmergencyPhone = @EmergencyPhone" +
                                  (!string.IsNullOrWhiteSpace(txtPassword.Text) ? ", Password = @Password" : "") +
                                  " WHERE ID = @AdminID";

                using (SqlCommand cmd = new SqlCommand(updateSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Title", string.IsNullOrWhiteSpace(ddlTitle.SelectedValue) ? (object)DBNull.Value : ddlTitle.SelectedValue);
                    cmd.Parameters.AddWithValue("@FirstName", txtFirstName.Text.Trim());
                    cmd.Parameters.AddWithValue("@LastName", txtLastName.Text.Trim());
                    cmd.Parameters.AddWithValue("@Role", ddlRole.SelectedValue);
                    cmd.Parameters.AddWithValue("@AdminID", adminId);

                    // Personal info
                    cmd.Parameters.AddWithValue("@Phone", string.IsNullOrWhiteSpace(txtPhone.Text) ? (object)DBNull.Value : txtPhone.Text.Trim());
                    cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(txtEmail.Text) ? (object)DBNull.Value : txtEmail.Text.Trim());
                    cmd.Parameters.AddWithValue("@IDCard", string.IsNullOrWhiteSpace(txtIDCard.Text) ? (object)DBNull.Value : txtIDCard.Text.Trim());
                    cmd.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(txtAddress.Text) ? (object)DBNull.Value : txtAddress.Text.Trim());

                    // Parse dates
                    DateTime hireDate;
                    cmd.Parameters.AddWithValue("@HireDate", DateTime.TryParse(txtHireDate.Text, out hireDate) ? (object)hireDate : DBNull.Value);
                    DateTime birthDate;
                    cmd.Parameters.AddWithValue("@BirthDate", DateTime.TryParse(txtBirthDate.Text, out birthDate) ? (object)birthDate : DBNull.Value);

                    // Bank info
                    cmd.Parameters.AddWithValue("@BankCode", string.IsNullOrWhiteSpace(ddlBank.SelectedValue) ? (object)DBNull.Value : ddlBank.SelectedValue);
                    cmd.Parameters.AddWithValue("@BankAccountNumber", string.IsNullOrWhiteSpace(txtBankAccountNumber.Text) ? (object)DBNull.Value : txtBankAccountNumber.Text.Trim());
                    cmd.Parameters.AddWithValue("@BankAccountName", string.IsNullOrWhiteSpace(txtBankAccountName.Text) ? (object)DBNull.Value : txtBankAccountName.Text.Trim());

                    // Emergency contact
                    cmd.Parameters.AddWithValue("@EmergencyContact", string.IsNullOrWhiteSpace(txtEmergencyContact.Text) ? (object)DBNull.Value : txtEmergencyContact.Text.Trim());
                    cmd.Parameters.AddWithValue("@EmergencyPhone", string.IsNullOrWhiteSpace(txtEmergencyPhone.Text) ? (object)DBNull.Value : txtEmergencyPhone.Text.Trim());

                    // Password if provided
                    if (!string.IsNullOrWhiteSpace(txtPassword.Text))
                    {
                        string hashedPassword = SecurityHelper.HashPassword(txtPassword.Text);
                        cmd.Parameters.AddWithValue("@Password", hashedPassword);
                    }

                    cmd.ExecuteNonQuery();
                }

                // Update or insert salary with history tracking
                string newPosition = txtPosition.Text.Trim();

                // Check current salary
                decimal currentSalary = 0;
                string currentPosition = "";
                using (SqlCommand checkCmd = new SqlCommand(@"
                    SELECT MonthlySalary, Position FROM Employee_Salary
                    WHERE Admin_ID = @AdminID AND IsActive = 1", conn))
                {
                    checkCmd.Parameters.AddWithValue("@AdminID", adminId);
                    using (SqlDataReader reader = checkCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            currentSalary = reader["MonthlySalary"] != DBNull.Value ? Convert.ToDecimal(reader["MonthlySalary"]) : 0;
                            currentPosition = reader["Position"]?.ToString() ?? "";
                        }
                    }
                }

                // If salary or position changed, create new record (keep history)
                if (newSalary != currentSalary || newPosition != currentPosition)
                {
                    if (currentSalary > 0)
                    {
                        // Deactivate old salary record
                        using (SqlCommand deactivateCmd = new SqlCommand(@"
                            UPDATE Employee_Salary SET IsActive = 0
                            WHERE Admin_ID = @AdminID AND IsActive = 1", conn))
                        {
                            deactivateCmd.Parameters.AddWithValue("@AdminID", adminId);
                            deactivateCmd.ExecuteNonQuery();
                        }
                    }

                    // Create new salary record
                    if (newSalary > 0 || !string.IsNullOrEmpty(newPosition))
                    {
                        using (SqlCommand salaryCmd = new SqlCommand(@"
                            INSERT INTO Employee_Salary (Admin_ID, MonthlySalary, Position, EffectiveDate, IsActive)
                            VALUES (@AdminID, @Salary, @Position, GETDATE(), 1)", conn))
                        {
                            salaryCmd.Parameters.AddWithValue("@AdminID", adminId);
                            salaryCmd.Parameters.AddWithValue("@Salary", newSalary);
                            salaryCmd.Parameters.AddWithValue("@Position", newPosition);
                            salaryCmd.ExecuteNonQuery();
                        }
                    }
                }
            }

            // Clear form and reload
            ClearAddForm();
            hdnEditMode.Value = "add";
            hdnEmployeeId.Value = "";
            LoadStatistics();
            LoadEmployees();
            ShowMessage("อัปเดตข้อมูลพนักงานสำเร็จ", "success");
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
            // Basic info
            txtUsername.Text = "";
            txtPassword.Text = "";
            txtFirstName.Text = "";
            txtLastName.Text = "";
            txtSalary.Text = "";
            txtPosition.Text = "";
            ddlRole.SelectedIndex = 0;

            // Personal info
            txtIDCard.Text = "";
            txtBirthDate.Text = "";
            txtPhone.Text = "";
            txtEmail.Text = "";
            txtAddress.Text = "";
            txtHireDate.Text = "";

            // Bank info
            ddlBank.SelectedIndex = 0;
            txtBankAccountNumber.Text = "";
            txtBankAccountName.Text = "";

            // Emergency contact
            txtEmergencyContact.Text = "";
            txtEmergencyPhone.Text = "";

            // Clear signature preview
            imgSignature.Visible = false;
            btnDeleteSignature.Visible = false;
        }

        #endregion

        #region Signature Management

        protected void btnUploadSignature_Click(object sender, EventArgs e)
        {
            try
            {
                if (!fuSignature.HasFile)
                {
                    ShowMessage("กรุณาเลือกไฟล์ลายเซ็น", "error");
                    return;
                }

                // Validate file type
                string fileExtension = Path.GetExtension(fuSignature.FileName).ToLower();
                if (fileExtension != ".png" && fileExtension != ".jpg" && fileExtension != ".jpeg")
                {
                    ShowMessage("รองรับเฉพาะไฟล์ PNG, JPG เท่านั้น", "error");
                    return;
                }

                // Validate file size (2MB max)
                if (fuSignature.PostedFile.ContentLength > 2 * 1024 * 1024)
                {
                    ShowMessage("ไฟล์ต้องมีขนาดไม่เกิน 2MB", "error");
                    return;
                }

                // Get employee name for filename
                string employeeName = GetEmployeeFullName();
                if (string.IsNullOrEmpty(employeeName))
                {
                    ShowMessage("กรุณาระบุชื่อพนักงานก่อน", "error");
                    return;
                }

                // Create directory if not exists
                string signatureDir = Server.MapPath("~/Documents/Staff/Signature");
                if (!Directory.Exists(signatureDir))
                {
                    Directory.CreateDirectory(signatureDir);
                }

                // Delete existing signature files for this employee
                DeleteExistingSignatureFiles(signatureDir, employeeName);

                // Save new signature file
                string newFileName = employeeName + ".png";
                string filePath = Path.Combine(signatureDir, newFileName);
                fuSignature.SaveAs(filePath);

                // Update UI
                LoadSignaturePreview(employeeName);
                ShowMessage("อัพโหลดลายเซ็นสำเร็จ", "success");

                // Keep modal open
                string script = "document.getElementById('employeeModal').style.display = 'flex';";
                ScriptManager.RegisterStartupScript(this, GetType(), "OpenModal", script, true);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการอัพโหลด: " + ex.Message, "error");
            }
        }

        protected void btnDeleteSignature_Click(object sender, EventArgs e)
        {
            try
            {
                string employeeName = GetEmployeeFullName();
                if (string.IsNullOrEmpty(employeeName))
                {
                    ShowMessage("ไม่สามารถระบุพนักงานได้", "error");
                    return;
                }

                string signatureDir = Server.MapPath("~/Documents/Staff/Signature");
                DeleteExistingSignatureFiles(signatureDir, employeeName);

                // Update UI
                imgSignature.Visible = false;
                btnDeleteSignature.Visible = false;
                ShowMessage("ลบลายเซ็นสำเร็จ", "success");

                // Keep modal open
                string script = "document.getElementById('employeeModal').style.display = 'flex'; document.getElementById('noSignatureText').style.display = 'block';";
                ScriptManager.RegisterStartupScript(this, GetType(), "OpenModal", script, true);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการลบ: " + ex.Message, "error");
            }
        }

        private string GetEmployeeFullName()
        {
            string firstName = txtFirstName.Text.Trim();
            string lastName = txtLastName.Text.Trim();

            if (string.IsNullOrEmpty(firstName))
                return null;

            return string.IsNullOrEmpty(lastName) ? firstName : firstName + " " + lastName;
        }

        private void DeleteExistingSignatureFiles(string signatureDir, string employeeName)
        {
            if (!Directory.Exists(signatureDir))
                return;

            // Delete any existing signature files for this employee
            string[] extensions = { ".png", ".jpg", ".jpeg" };
            foreach (var ext in extensions)
            {
                string filePath = Path.Combine(signatureDir, employeeName + ext);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        private void LoadSignaturePreview(string employeeName)
        {
            if (string.IsNullOrEmpty(employeeName))
            {
                imgSignature.Visible = false;
                btnDeleteSignature.Visible = false;
                return;
            }

            // Use physical path from config to avoid permission issues
            string signatureDir = ConfigurationManager.AppSettings["StaffSignatureFolderPath"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(signatureDir))
            {
                // Fallback to Server.MapPath if config not set
                signatureDir = Server.MapPath("~/Documents/Staff/Signature");
            }

            string[] extensions = { ".png", ".jpg", ".jpeg" };

            foreach (var ext in extensions)
            {
                string filePath = Path.Combine(signatureDir, employeeName + ext);
                if (File.Exists(filePath))
                {
                    // Read file and convert to base64 data URL to avoid permission issues
                    try
                    {
                        byte[] imageBytes = File.ReadAllBytes(filePath);
                        string base64 = Convert.ToBase64String(imageBytes);
                        string mimeType = ext == ".png" ? "image/png" : "image/jpeg";
                        imgSignature.ImageUrl = $"data:{mimeType};base64,{base64}";
                        imgSignature.Visible = true;
                        btnDeleteSignature.Visible = true;

                        // Hide "no signature" text
                        string script = "document.getElementById('noSignatureText').style.display = 'none';";
                        ScriptManager.RegisterStartupScript(this, GetType(), "HideNoSignature", script, true);
                        return;
                    }
                    catch
                    {
                        // If reading fails, serve via FileHandler to bypass IIS static file auth
                        imgSignature.ImageUrl = $"~/API/FileHandler.ashx?path=~/Documents/Staff/Signature/{employeeName}{ext}";
                        imgSignature.Visible = true;
                        btnDeleteSignature.Visible = true;
                        return;
                    }
                }
            }

            // No signature found
            imgSignature.Visible = false;
            btnDeleteSignature.Visible = false;
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

        #region Document Management

        private void LoadEmployeeDocuments(short adminId)
        {
            try
            {
                hdnDocEmployeeId.Value = adminId.ToString();

                // Get employee name
                string employeeName = "";
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT ISNULL(FirstName + ' ' + LastName, Username) AS Name FROM Admin WHERE ID = @AdminID", conn))
                    {
                        cmd.Parameters.AddWithValue("@AdminID", adminId);
                        employeeName = cmd.ExecuteScalar()?.ToString() ?? "";
                    }
                }

                // Load documents
                DataTable dtDocuments = GetEmployeeDocuments(adminId);

                if (dtDocuments.Rows.Count > 0)
                {
                    rptDocuments.DataSource = dtDocuments;
                    rptDocuments.DataBind();
                    pnlNoDocuments.Visible = false;
                }
                else
                {
                    rptDocuments.DataSource = null;
                    rptDocuments.DataBind();
                    pnlNoDocuments.Visible = true;
                }

                // Open modal
                string script = $"openDocumentsModal('{EscapeJsString(employeeName)}');";
                ScriptManager.RegisterStartupScript(this, GetType(), "OpenDocuments", script, true);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการโหลดเอกสาร: " + ex.Message, "error");
            }
        }

        private DataTable GetEmployeeDocuments(short adminId)
        {
            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ID, DocumentType, DocumentName, OriginalFileName AS FileName, FilePath, FileSize,
                           UploadedDate, ExpiryDate, Description
                    FROM Employee_Documents
                    WHERE Admin_ID = @AdminID AND IsActive = 1
                    ORDER BY UploadedDate DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@AdminID", adminId);
                    conn.Open();

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }

            return dt;
        }

        protected void btnUploadDocument_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(hdnDocEmployeeId.Value))
                {
                    ShowMessage("ไม่พบข้อมูลพนักงาน", "error");
                    return;
                }

                if (!fuDocument.HasFile)
                {
                    ShowMessage("กรุณาเลือกไฟล์ที่ต้องการอัพโหลด", "error");
                    KeepDocumentsModalOpen();
                    return;
                }

                short adminId = Convert.ToInt16(hdnDocEmployeeId.Value);

                // Validate file
                string[] allowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx" };
                string fileExtension = Path.GetExtension(fuDocument.FileName).ToLower();

                if (Array.IndexOf(allowedExtensions, fileExtension) < 0)
                {
                    ShowMessage("ไม่รองรับไฟล์ประเภทนี้ (รองรับ: PDF, JPG, PNG, Word, Excel)", "error");
                    KeepDocumentsModalOpen();
                    return;
                }

                // Check file size (10MB max)
                if (fuDocument.PostedFile.ContentLength > 10 * 1024 * 1024)
                {
                    ShowMessage("ขนาดไฟล์เกิน 10MB", "error");
                    KeepDocumentsModalOpen();
                    return;
                }

                // Create directory
                string documentsDir = Server.MapPath($"~/Documents/Staff/Employees/{adminId}");
                if (!Directory.Exists(documentsDir))
                {
                    Directory.CreateDirectory(documentsDir);
                }

                // Generate unique filename
                string uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                string filePath = Path.Combine(documentsDir, uniqueFileName);
                string relativePath = $"~/Documents/Staff/Employees/{adminId}/{uniqueFileName}";

                // Save file
                fuDocument.SaveAs(filePath);

                // Get document name
                string documentName = txtDocumentName.Text.Trim();
                if (string.IsNullOrEmpty(documentName))
                {
                    documentName = Path.GetFileNameWithoutExtension(fuDocument.FileName);
                }

                // Parse expiry date
                DateTime? expiryDate = null;
                if (!string.IsNullOrEmpty(txtDocExpiryDate.Text))
                {
                    expiryDate = DateTime.Parse(txtDocExpiryDate.Text);
                }

                // Save to database
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        INSERT INTO Employee_Documents
                        (Admin_ID, DocumentType, DocumentName, OriginalFileName, FilePath, FileSize,
                         FileType, Description, UploadedBy_AdminID, ExpiryDate)
                        VALUES
                        (@AdminID, @DocumentType, @DocumentName, @OriginalFileName, @FilePath, @FileSize,
                         @FileType, @Description, @UploadedBy_AdminID, @ExpiryDate)", conn))
                    {
                        cmd.Parameters.AddWithValue("@AdminID", adminId);
                        cmd.Parameters.AddWithValue("@DocumentType", ddlDocumentType.SelectedValue);
                        cmd.Parameters.AddWithValue("@DocumentName", documentName);
                        cmd.Parameters.AddWithValue("@OriginalFileName", fuDocument.FileName);
                        cmd.Parameters.AddWithValue("@FilePath", relativePath);
                        cmd.Parameters.AddWithValue("@FileSize", fuDocument.PostedFile.ContentLength);
                        cmd.Parameters.AddWithValue("@FileType", System.IO.Path.GetExtension(fuDocument.FileName).TrimStart('.').ToUpper());
                        cmd.Parameters.AddWithValue("@Description", string.IsNullOrEmpty(txtDocDescription.Text) ? (object)DBNull.Value : txtDocDescription.Text);
                        cmd.Parameters.AddWithValue("@UploadedBy_AdminID", GetAdminID() ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ExpiryDate", expiryDate ?? (object)DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }
                }

                // Clear form
                txtDocumentName.Text = "";
                txtDocExpiryDate.Text = "";
                txtDocDescription.Text = "";
                ddlDocumentType.SelectedIndex = 0;

                ShowMessage("อัพโหลดเอกสารสำเร็จ", "success");

                // Reload documents list
                LoadEmployeeDocuments(adminId);
            }
            catch (Exception ex)
            {
                ShowMessage("เกิดข้อผิดพลาดในการอัพโหลด: " + ex.Message, "error");
                KeepDocumentsModalOpen();
            }
        }

        protected void rptDocuments_ItemCommand(object source, System.Web.UI.WebControls.RepeaterCommandEventArgs e)
        {
            if (e.CommandName == "ViewDocument")
            {
                string[] args = e.CommandArgument.ToString().Split('|');
                if (args.Length >= 2)
                {
                    string filePath = args[1];
                    string physicalPath = Server.MapPath(filePath);

                    if (File.Exists(physicalPath))
                    {
                        // Open via FileHandler to bypass IIS static file auth
                        string handlerUrl = ResolveUrl($"~/API/FileHandler.ashx?path={Server.UrlEncode(filePath)}");
                        string script = $"window.open('{handlerUrl}', '_blank');";
                        ScriptManager.RegisterStartupScript(this, GetType(), "ViewDoc", script, true);
                    }
                    else
                    {
                        ShowMessage("ไม่พบไฟล์เอกสาร", "error");
                    }
                }
                KeepDocumentsModalOpen();
            }
            else if (e.CommandName == "DeleteDocument")
            {
                try
                {
                    long documentId = Convert.ToInt64(e.CommandArgument);

                    // Get file path before deleting
                    string filePath = "";
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        // Get file path
                        using (SqlCommand getCmd = new SqlCommand(
                            "SELECT FilePath FROM Employee_Documents WHERE ID = @ID", conn))
                        {
                            getCmd.Parameters.AddWithValue("@ID", documentId);
                            filePath = getCmd.ExecuteScalar()?.ToString() ?? "";
                        }

                        // Soft delete from database
                        using (SqlCommand cmd = new SqlCommand(
                            "UPDATE Employee_Documents SET IsActive = 0 WHERE ID = @ID", conn))
                        {
                            cmd.Parameters.AddWithValue("@ID", documentId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Optional: Delete physical file
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        string physicalPath = Server.MapPath(filePath);
                        if (File.Exists(physicalPath))
                        {
                            File.Delete(physicalPath);
                        }
                    }

                    ShowMessage("ลบเอกสารสำเร็จ", "success");

                    // Reload documents
                    if (!string.IsNullOrEmpty(hdnDocEmployeeId.Value))
                    {
                        LoadEmployeeDocuments(Convert.ToInt16(hdnDocEmployeeId.Value));
                    }
                }
                catch (Exception ex)
                {
                    ShowMessage("เกิดข้อผิดพลาดในการลบ: " + ex.Message, "error");
                    KeepDocumentsModalOpen();
                }
            }
        }

        private void KeepDocumentsModalOpen()
        {
            string script = "document.getElementById('documentsModal').style.display = 'flex';";
            ScriptManager.RegisterStartupScript(this, GetType(), "KeepDocModal", script, true);
        }

        protected string GetDocumentTypeText(string documentType)
        {
            switch (documentType.ToUpper())
            {
                case "ID_CARD": return "บัตรประชาชน";
                case "HOUSE_REG": return "ทะเบียนบ้าน";
                case "BANK_BOOK": return "สมุดบัญชี";
                case "CONTRACT": return "สัญญาจ้าง";
                case "RESUME": return "Resume";
                case "CERTIFICATE": return "ใบรับรอง";
                case "MEDICAL": return "ใบแพทย์";
                case "OTHER": return "อื่นๆ";
                default: return documentType;
            }
        }

        #endregion
    }
}
