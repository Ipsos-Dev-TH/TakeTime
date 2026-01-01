using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

/// <summary>
/// Supervisor Service - Business logic for Employee-Supervisor relationships
/// Supports many-to-many relationship (one employee can have multiple supervisors)
///
/// Required Database Table:
/// CREATE TABLE Employee_Supervisor (
///     ID INT IDENTITY(1,1) PRIMARY KEY,
///     Employee_AdminID SMALLINT NOT NULL,       -- The employee (subordinate)
///     Supervisor_AdminID SMALLINT NOT NULL,     -- The supervisor
///     IsPrimary BIT DEFAULT 0,                  -- Primary supervisor flag
///     CanApproveLeave BIT DEFAULT 1,            -- Can approve leave requests
///     CanViewSalary BIT DEFAULT 0,              -- Can view salary info
///     AssignedDate DATETIME DEFAULT GETDATE(),
///     IsActive BIT DEFAULT 1,
///     Notes NVARCHAR(500),
///     CONSTRAINT FK_EmpSup_Employee FOREIGN KEY (Employee_AdminID) REFERENCES Admin(ID),
///     CONSTRAINT FK_EmpSup_Supervisor FOREIGN KEY (Supervisor_AdminID) REFERENCES Admin(ID),
///     CONSTRAINT UQ_Employee_Supervisor UNIQUE (Employee_AdminID, Supervisor_AdminID)
/// )
/// </summary>
public class SupervisorService
{
    private readonly string connectionString;

    public SupervisorService()
    {
        connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
    }

    #region Table Initialization

    /// <summary>
    /// Ensure Employee_Supervisor table exists
    /// </summary>
    public bool EnsureTableExists()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Employee_Supervisor')
                    BEGIN
                        CREATE TABLE Employee_Supervisor (
                            ID INT IDENTITY(1,1) PRIMARY KEY,
                            Employee_AdminID SMALLINT NOT NULL,
                            Supervisor_AdminID SMALLINT NOT NULL,
                            IsPrimary BIT DEFAULT 0,
                            CanApproveLeave BIT DEFAULT 1,
                            CanViewSalary BIT DEFAULT 0,
                            AssignedDate DATETIME DEFAULT GETDATE(),
                            IsActive BIT DEFAULT 1,
                            Notes NVARCHAR(500),
                            CONSTRAINT FK_EmpSup_Employee FOREIGN KEY (Employee_AdminID) REFERENCES Admin(ID),
                            CONSTRAINT FK_EmpSup_Supervisor FOREIGN KEY (Supervisor_AdminID) REFERENCES Admin(ID),
                            CONSTRAINT UQ_Employee_Supervisor UNIQUE (Employee_AdminID, Supervisor_AdminID)
                        );
                        SELECT 1;
                    END
                    ELSE
                    BEGIN
                        SELECT 0;
                    END";

                conn.Open();
                cmd.ExecuteNonQuery();
                return true;
            }
        }
    }

    #endregion

    #region Supervisor-Employee Relationship Management

    /// <summary>
    /// Assign supervisor to employee
    /// </summary>
    public (bool Success, string Message) AssignSupervisor(
        short employeeAdminId, short supervisorAdminId,
        bool isPrimary = false, bool canApproveLeave = true, bool canViewSalary = false, string notes = null)
    {
        try
        {
            // Prevent self-assignment
            if (employeeAdminId == supervisorAdminId)
            {
                return (false, "ไม่สามารถกำหนดตัวเองเป็นหัวหน้างานได้");
            }

            EnsureTableExists();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Check if relationship already exists
                using (SqlCommand checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Employee_Supervisor WHERE Employee_AdminID = @EmpID AND Supervisor_AdminID = @SupID", conn))
                {
                    checkCmd.Parameters.AddWithValue("@EmpID", employeeAdminId);
                    checkCmd.Parameters.AddWithValue("@SupID", supervisorAdminId);
                    if ((int)checkCmd.ExecuteScalar() > 0)
                    {
                        return (false, "หัวหน้างานนี้ได้ถูกกำหนดให้พนักงานแล้ว");
                    }
                }

                // If setting as primary, clear other primary flags first
                if (isPrimary)
                {
                    using (SqlCommand clearCmd = new SqlCommand(
                        "UPDATE Employee_Supervisor SET IsPrimary = 0 WHERE Employee_AdminID = @EmpID", conn))
                    {
                        clearCmd.Parameters.AddWithValue("@EmpID", employeeAdminId);
                        clearCmd.ExecuteNonQuery();
                    }
                }

                // Insert new relationship
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO Employee_Supervisor (Employee_AdminID, Supervisor_AdminID, IsPrimary, CanApproveLeave, CanViewSalary, Notes)
                    VALUES (@EmpID, @SupID, @IsPrimary, @CanApproveLeave, @CanViewSalary, @Notes)", conn))
                {
                    cmd.Parameters.AddWithValue("@EmpID", employeeAdminId);
                    cmd.Parameters.AddWithValue("@SupID", supervisorAdminId);
                    cmd.Parameters.AddWithValue("@IsPrimary", isPrimary);
                    cmd.Parameters.AddWithValue("@CanApproveLeave", canApproveLeave);
                    cmd.Parameters.AddWithValue("@CanViewSalary", canViewSalary);
                    cmd.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                }

                return (true, "กำหนดหัวหน้างานสำเร็จ");
            }
        }
        catch (Exception ex)
        {
            return (false, "เกิดข้อผิดพลาด: " + ex.Message);
        }
    }

    /// <summary>
    /// Remove supervisor from employee
    /// </summary>
    public (bool Success, string Message) RemoveSupervisor(short employeeAdminId, short supervisorAdminId)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(@"
                    DELETE FROM Employee_Supervisor
                    WHERE Employee_AdminID = @EmpID AND Supervisor_AdminID = @SupID", conn))
                {
                    cmd.Parameters.AddWithValue("@EmpID", employeeAdminId);
                    cmd.Parameters.AddWithValue("@SupID", supervisorAdminId);

                    conn.Open();
                    int affected = cmd.ExecuteNonQuery();

                    if (affected > 0)
                        return (true, "ลบหัวหน้างานสำเร็จ");
                    else
                        return (false, "ไม่พบข้อมูลความสัมพันธ์นี้");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, "เกิดข้อผิดพลาด: " + ex.Message);
        }
    }

    /// <summary>
    /// Update supervisor relationship
    /// </summary>
    public (bool Success, string Message) UpdateSupervisorRelationship(
        int relationshipId, bool isPrimary, bool canApproveLeave, bool canViewSalary, bool isActive)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // If setting as primary, clear other primary flags first
                if (isPrimary)
                {
                    using (SqlCommand getEmpCmd = new SqlCommand(
                        "SELECT Employee_AdminID FROM Employee_Supervisor WHERE ID = @ID", conn))
                    {
                        getEmpCmd.Parameters.AddWithValue("@ID", relationshipId);
                        object result = getEmpCmd.ExecuteScalar();
                        if (result != null)
                        {
                            short empId = Convert.ToInt16(result);
                            using (SqlCommand clearCmd = new SqlCommand(
                                "UPDATE Employee_Supervisor SET IsPrimary = 0 WHERE Employee_AdminID = @EmpID AND ID != @ID", conn))
                            {
                                clearCmd.Parameters.AddWithValue("@EmpID", empId);
                                clearCmd.Parameters.AddWithValue("@ID", relationshipId);
                                clearCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }

                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE Employee_Supervisor
                    SET IsPrimary = @IsPrimary, CanApproveLeave = @CanApproveLeave,
                        CanViewSalary = @CanViewSalary, IsActive = @IsActive
                    WHERE ID = @ID", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", relationshipId);
                    cmd.Parameters.AddWithValue("@IsPrimary", isPrimary);
                    cmd.Parameters.AddWithValue("@CanApproveLeave", canApproveLeave);
                    cmd.Parameters.AddWithValue("@CanViewSalary", canViewSalary);
                    cmd.Parameters.AddWithValue("@IsActive", isActive);

                    int affected = cmd.ExecuteNonQuery();
                    if (affected > 0)
                        return (true, "อัพเดทสำเร็จ");
                    else
                        return (false, "ไม่พบข้อมูล");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, "เกิดข้อผิดพลาด: " + ex.Message);
        }
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Get all supervisors for an employee
    /// </summary>
    public DataTable GetSupervisorsForEmployee(short employeeAdminId)
    {
        EnsureTableExists();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ES.ID, ES.Supervisor_AdminID,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS SupervisorName,
                        A.Username AS SupervisorUsername,
                        A.Role AS SupervisorRole,
                        ES.IsPrimary, ES.CanApproveLeave, ES.CanViewSalary,
                        ES.AssignedDate, ES.IsActive, ES.Notes
                    FROM Employee_Supervisor ES
                    INNER JOIN Admin A ON A.ID = ES.Supervisor_AdminID
                    WHERE ES.Employee_AdminID = @EmpID AND ES.IsActive = 1
                    ORDER BY ES.IsPrimary DESC, ES.AssignedDate";
                cmd.Parameters.AddWithValue("@EmpID", employeeAdminId);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get all subordinates for a supervisor
    /// Owner sees all employees automatically
    /// </summary>
    public DataTable GetSubordinatesForSupervisor(short supervisorAdminId)
    {
        EnsureTableExists();

        // If Owner, return all employees
        if (IsOwner(supervisorAdminId))
        {
            return GetAllEmployeesAsSubordinates(supervisorAdminId);
        }

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ES.ID, ES.Employee_AdminID,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                        A.Username AS EmployeeUsername,
                        A.Role AS EmployeeRole,
                        A.Status AS EmployeeStatus,
                        ISNULL(Salary.Position, A.Role) AS Position,
                        ES.IsPrimary, ES.CanApproveLeave, ES.CanViewSalary,
                        ES.AssignedDate, ES.IsActive, ES.Notes
                    FROM Employee_Supervisor ES
                    INNER JOIN Admin A ON A.ID = ES.Employee_AdminID
                    LEFT JOIN Employee_Salary Salary ON Salary.Admin_ID = A.ID AND Salary.IsActive = 1
                    WHERE ES.Supervisor_AdminID = @SupID AND ES.IsActive = 1 AND A.Status = 1
                    ORDER BY A.FirstName, A.LastName";
                cmd.Parameters.AddWithValue("@SupID", supervisorAdminId);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get all employees as subordinates (for Owner)
    /// </summary>
    private DataTable GetAllEmployeesAsSubordinates(short ownerAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        0 AS ID,
                        A.ID AS Employee_AdminID,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                        A.Username AS EmployeeUsername,
                        A.Role AS EmployeeRole,
                        A.Status AS EmployeeStatus,
                        ISNULL(Salary.Position, A.Role) AS Position,
                        CAST(1 AS BIT) AS IsPrimary,
                        CAST(1 AS BIT) AS CanApproveLeave,
                        CAST(1 AS BIT) AS CanViewSalary,
                        GETDATE() AS AssignedDate,
                        CAST(1 AS BIT) AS IsActive,
                        N'Owner สิทธิ์อัตโนมัติ' AS Notes
                    FROM Admin A
                    LEFT JOIN Employee_Salary Salary ON Salary.Admin_ID = A.ID AND Salary.IsActive = 1
                    WHERE A.Status = 1 AND A.ID != @OwnerID
                    ORDER BY A.FirstName, A.LastName";
                cmd.Parameters.AddWithValue("@OwnerID", ownerAdminId);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Check if user is supervisor of another user
    /// Owner role is automatically supervisor of everyone
    /// </summary>
    public bool IsSupervisorOf(short supervisorAdminId, short employeeAdminId)
    {
        // Owner is supervisor of everyone automatically
        if (IsOwner(supervisorAdminId))
        {
            return true;
        }

        EnsureTableExists();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM Employee_Supervisor
                WHERE Supervisor_AdminID = @SupID AND Employee_AdminID = @EmpID
                AND IsActive = 1 AND CanApproveLeave = 1", conn))
            {
                cmd.Parameters.AddWithValue("@SupID", supervisorAdminId);
                cmd.Parameters.AddWithValue("@EmpID", employeeAdminId);

                conn.Open();
                return (int)cmd.ExecuteScalar() > 0;
            }
        }
    }

    /// <summary>
    /// Check if admin is Owner role
    /// </summary>
    public bool IsOwner(short adminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM Admin WHERE ID = @AdminID AND Role = 'Owner' AND Status = 1", conn))
            {
                cmd.Parameters.AddWithValue("@AdminID", adminId);
                conn.Open();
                return (int)cmd.ExecuteScalar() > 0;
            }
        }
    }

    /// <summary>
    /// Check if admin is Owner by username (for session-based checks)
    /// </summary>
    public bool IsOwnerByUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
            return false;

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM Admin WHERE Username = @Username AND Role = 'Owner' AND Status = 1", conn))
            {
                cmd.Parameters.AddWithValue("@Username", username);
                conn.Open();
                return (int)cmd.ExecuteScalar() > 0;
            }
        }
    }

    /// <summary>
    /// Get all employees (for dropdown/selection)
    /// </summary>
    public DataTable GetAllActiveEmployees()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        A.ID,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS Name,
                        A.Username,
                        A.Role
                    FROM Admin A
                    WHERE A.Status = 1
                    ORDER BY A.FirstName, A.LastName, A.Username";

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get all supervisor relationships (for admin view)
    /// </summary>
    public DataTable GetAllSupervisorRelationships()
    {
        EnsureTableExists();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ES.ID,
                        ES.Employee_AdminID,
                        ISNULL(Emp.FirstName + ' ' + Emp.LastName, Emp.Username) AS EmployeeName,
                        Emp.Username AS EmployeeUsername,
                        Emp.Role AS EmployeeRole,
                        ES.Supervisor_AdminID,
                        ISNULL(Sup.FirstName + ' ' + Sup.LastName, Sup.Username) AS SupervisorName,
                        Sup.Username AS SupervisorUsername,
                        Sup.Role AS SupervisorRole,
                        ES.IsPrimary, ES.CanApproveLeave, ES.CanViewSalary,
                        ES.AssignedDate, ES.IsActive
                    FROM Employee_Supervisor ES
                    INNER JOIN Admin Emp ON Emp.ID = ES.Employee_AdminID
                    INNER JOIN Admin Sup ON Sup.ID = ES.Supervisor_AdminID
                    ORDER BY Emp.FirstName, Emp.LastName, ES.IsPrimary DESC";

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>
    /// Get statistics for supervisor dashboard
    /// </summary>
    public DataTable GetSupervisorDashboardStats(short supervisorAdminId)
    {
        EnsureTableExists();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        (SELECT COUNT(*) FROM Employee_Supervisor
                         WHERE Supervisor_AdminID = @SupID AND IsActive = 1) AS TotalSubordinates,
                        (SELECT COUNT(*) FROM Leave_Requests LR
                         INNER JOIN Employee_Supervisor ES ON ES.Employee_AdminID = LR.Admin_ID
                         WHERE ES.Supervisor_AdminID = @SupID AND ES.IsActive = 1
                         AND ES.CanApproveLeave = 1 AND LR.Status = 'PENDING') AS PendingLeaveRequests,
                        (SELECT COUNT(*) FROM Leave_Requests LR
                         INNER JOIN Employee_Supervisor ES ON ES.Employee_AdminID = LR.Admin_ID
                         WHERE ES.Supervisor_AdminID = @SupID AND ES.IsActive = 1
                         AND LR.Status = 'APPROVED' AND YEAR(LR.StartDate) = YEAR(GETDATE())) AS ApprovedThisYear";
                cmd.Parameters.AddWithValue("@SupID", supervisorAdminId);

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
}
