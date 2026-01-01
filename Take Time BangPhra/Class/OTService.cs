using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

/// <summary>
/// OT Service - Business logic for Overtime Entry Management
/// Handles OT entries for employees and their supervisors
///
/// Required Database Table:
/// CREATE TABLE OT_Entry (
///     ID BIGINT IDENTITY(1,1) PRIMARY KEY,
///     Admin_ID SMALLINT NOT NULL,                -- Employee who worked OT
///     OTDate DATE NOT NULL,                      -- Date of OT work
///     OTHours DECIMAL(4,2) NOT NULL,             -- Hours of OT (e.g., 2.5)
///     OTRate DECIMAL(5,2) DEFAULT 1.5,           -- OT rate multiplier
///     WorkDescription NVARCHAR(500),             -- What work was done
///     EnteredBy_AdminID SMALLINT NOT NULL,       -- Who entered this record (self or supervisor)
///     EntryDate DATETIME DEFAULT GETDATE(),
///     ApprovedBy_AdminID SMALLINT NULL,          -- Supervisor who approved
///     ApprovedDate DATETIME NULL,
///     Status VARCHAR(20) DEFAULT 'PENDING',      -- PENDING, APPROVED, REJECTED
///     RejectedReason NVARCHAR(500),
///     Notes NVARCHAR(500),
///     CONSTRAINT FK_OT_Employee FOREIGN KEY (Admin_ID) REFERENCES Admin(ID),
///     CONSTRAINT FK_OT_EnteredBy FOREIGN KEY (EnteredBy_AdminID) REFERENCES Admin(ID),
///     CONSTRAINT FK_OT_ApprovedBy FOREIGN KEY (ApprovedBy_AdminID) REFERENCES Admin(ID)
/// )
/// </summary>
public class OTService
{
    private readonly string connectionString;

    public OTService()
    {
        connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
    }

    #region Table Initialization

    /// <summary>
    /// Ensure OT_Entry table exists
    /// </summary>
    public bool EnsureTableExists()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OT_Entry')
                    BEGIN
                        CREATE TABLE OT_Entry (
                            ID BIGINT IDENTITY(1,1) PRIMARY KEY,
                            Admin_ID SMALLINT NOT NULL,
                            OTDate DATE NOT NULL,
                            OTHours DECIMAL(4,2) NOT NULL,
                            OTRate DECIMAL(5,2) DEFAULT 1.5,
                            WorkDescription NVARCHAR(500),
                            EnteredBy_AdminID SMALLINT NOT NULL,
                            EntryDate DATETIME DEFAULT GETDATE(),
                            ApprovedBy_AdminID SMALLINT NULL,
                            ApprovedDate DATETIME NULL,
                            Status VARCHAR(20) DEFAULT 'PENDING',
                            RejectedReason NVARCHAR(500),
                            Notes NVARCHAR(500),
                            CONSTRAINT FK_OT_Employee FOREIGN KEY (Admin_ID) REFERENCES Admin(ID),
                            CONSTRAINT FK_OT_EnteredBy FOREIGN KEY (EnteredBy_AdminID) REFERENCES Admin(ID)
                        );
                    END";

                conn.Open();
                cmd.ExecuteNonQuery();
                return true;
            }
        }
    }

    #endregion

    #region OT Entry Management

    /// <summary>
    /// Create OT entry
    /// Auto-approves if Owner records for anyone, or Supervisor records for subordinate
    /// </summary>
    public (bool Success, string Message, long ID) CreateOTEntry(
        short adminId, DateTime otDate, decimal otHours, string workDescription,
        short enteredByAdminId, decimal otRate = 1.5m, string notes = null)
    {
        try
        {
            EnsureTableExists();

            // Validate
            if (otHours <= 0 || otHours > 24)
            {
                return (false, "จำนวนชั่วโมง OT ต้องอยู่ระหว่าง 0.5 - 24 ชั่วโมง", 0);
            }

            if (string.IsNullOrWhiteSpace(workDescription))
            {
                return (false, "กรุณาระบุรายละเอียดงานที่ทำ", 0);
            }

            // Determine if auto-approve should apply
            SupervisorService supervisorService = new SupervisorService();
            bool isEnteredByOwner = supervisorService.IsOwner(enteredByAdminId);
            bool isSupervisorRecordingForSubordinate = (adminId != enteredByAdminId) &&
                supervisorService.IsSupervisorOf(enteredByAdminId, adminId);
            bool shouldAutoApprove = isEnteredByOwner || isSupervisorRecordingForSubordinate;

            // Check for duplicate entry
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (SqlCommand checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM OT_Entry WHERE Admin_ID = @AdminID AND OTDate = @OTDate AND Status != 'REJECTED'", conn))
                {
                    checkCmd.Parameters.AddWithValue("@AdminID", adminId);
                    checkCmd.Parameters.AddWithValue("@OTDate", otDate.Date);

                    if ((int)checkCmd.ExecuteScalar() > 0)
                    {
                        return (false, "มีการบันทึก OT วันนี้แล้ว", 0);
                    }
                }

                // Insert new entry - with auto-approve if applicable
                string insertSql;
                if (shouldAutoApprove)
                {
                    insertSql = @"
                        INSERT INTO OT_Entry (Admin_ID, OTDate, OTHours, OTRate, WorkDescription, EnteredBy_AdminID, Notes, Status, ApprovedBy_AdminID, ApprovedDate)
                        VALUES (@AdminID, @OTDate, @OTHours, @OTRate, @WorkDescription, @EnteredBy, @Notes, 'APPROVED', @ApprovedBy, GETDATE());
                        SELECT SCOPE_IDENTITY();";
                }
                else
                {
                    insertSql = @"
                        INSERT INTO OT_Entry (Admin_ID, OTDate, OTHours, OTRate, WorkDescription, EnteredBy_AdminID, Notes)
                        VALUES (@AdminID, @OTDate, @OTHours, @OTRate, @WorkDescription, @EnteredBy, @Notes);
                        SELECT SCOPE_IDENTITY();";
                }

                using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@AdminID", adminId);
                    cmd.Parameters.AddWithValue("@OTDate", otDate.Date);
                    cmd.Parameters.AddWithValue("@OTHours", otHours);
                    cmd.Parameters.AddWithValue("@OTRate", otRate);
                    cmd.Parameters.AddWithValue("@WorkDescription", workDescription);
                    cmd.Parameters.AddWithValue("@EnteredBy", enteredByAdminId);
                    cmd.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

                    if (shouldAutoApprove)
                    {
                        cmd.Parameters.AddWithValue("@ApprovedBy", enteredByAdminId);
                    }

                    long newId = Convert.ToInt64(cmd.ExecuteScalar());

                    string message = shouldAutoApprove
                        ? "บันทึก OT สำเร็จ (อนุมัติอัตโนมัติ)"
                        : "บันทึก OT สำเร็จ";
                    return (true, message, newId);
                }
            }
        }
        catch (Exception ex)
        {
            return (false, "เกิดข้อผิดพลาด: " + ex.Message, 0);
        }
    }

    /// <summary>
    /// Get OT entries for employee
    /// </summary>
    public DataTable GetOTEntriesForEmployee(short adminId, int? month = null, int? year = null)
    {
        EnsureTableExists();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                string dateFilter = "";
                if (month.HasValue && year.HasValue)
                {
                    dateFilter = " AND MONTH(O.OTDate) = @Month AND YEAR(O.OTDate) = @Year";
                    cmd.Parameters.AddWithValue("@Month", month.Value);
                    cmd.Parameters.AddWithValue("@Year", year.Value);
                }
                else if (year.HasValue)
                {
                    dateFilter = " AND YEAR(O.OTDate) = @Year";
                    cmd.Parameters.AddWithValue("@Year", year.Value);
                }

                cmd.CommandText = @"
                    SELECT
                        O.ID, O.Admin_ID, O.OTDate, O.OTHours, O.OTRate,
                        O.WorkDescription, O.Status, O.EntryDate,
                        O.ApprovedBy_AdminID, O.ApprovedDate, O.RejectedReason, O.Notes,
                        ISNULL(EnteredBy.FirstName + ' ' + EnteredBy.LastName, EnteredBy.Username) AS EnteredByName,
                        ISNULL(ApprovedBy.FirstName + ' ' + ApprovedBy.LastName, ApprovedBy.Username) AS ApprovedByName,
                        (O.OTHours * O.OTRate) AS OTMultipliedHours
                    FROM OT_Entry O
                    INNER JOIN Admin EnteredBy ON EnteredBy.ID = O.EnteredBy_AdminID
                    LEFT JOIN Admin ApprovedBy ON ApprovedBy.ID = O.ApprovedBy_AdminID
                    WHERE O.Admin_ID = @AdminID" + dateFilter + @"
                    ORDER BY O.OTDate DESC, O.EntryDate DESC";
                cmd.Parameters.AddWithValue("@AdminID", adminId);

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
    /// Get pending OT entries for supervisor's subordinates
    /// </summary>
    public DataTable GetPendingOTEntriesForSupervisor(short supervisorAdminId)
    {
        EnsureTableExists();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        O.ID, O.Admin_ID, O.OTDate, O.OTHours, O.OTRate,
                        O.WorkDescription, O.Status, O.EntryDate, O.Notes,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                        A.Username AS EmployeeUsername,
                        ISNULL(Salary.Position, A.Role) AS EmployeePosition,
                        ISNULL(EnteredBy.FirstName + ' ' + EnteredBy.LastName, EnteredBy.Username) AS EnteredByName,
                        (O.OTHours * O.OTRate) AS OTMultipliedHours
                    FROM OT_Entry O
                    INNER JOIN Admin A ON A.ID = O.Admin_ID
                    INNER JOIN Employee_Supervisor ES ON ES.Employee_AdminID = O.Admin_ID
                    INNER JOIN Admin EnteredBy ON EnteredBy.ID = O.EnteredBy_AdminID
                    LEFT JOIN Employee_Salary Salary ON Salary.Admin_ID = A.ID AND Salary.IsActive = 1
                    WHERE ES.Supervisor_AdminID = @SupervisorID
                      AND ES.IsActive = 1
                      AND O.Status = 'PENDING'
                    ORDER BY O.OTDate DESC, O.EntryDate DESC";
                cmd.Parameters.AddWithValue("@SupervisorID", supervisorAdminId);

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
    /// Get OT entries for supervisor's subordinates
    /// Owner sees all employees' OT entries
    /// </summary>
    public DataTable GetOTEntriesForSupervisor(short supervisorAdminId, string status = null, int? month = null, int? year = null)
    {
        EnsureTableExists();

        // Check if user is Owner
        SupervisorService supervisorService = new SupervisorService();
        bool isOwner = supervisorService.IsOwner(supervisorAdminId);

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                string filters = "";
                if (!string.IsNullOrEmpty(status))
                {
                    filters += " AND O.Status = @Status";
                    cmd.Parameters.AddWithValue("@Status", status);
                }
                if (month.HasValue)
                {
                    filters += " AND MONTH(O.OTDate) = @Month";
                    cmd.Parameters.AddWithValue("@Month", month.Value);
                }
                if (year.HasValue)
                {
                    filters += " AND YEAR(O.OTDate) = @Year";
                    cmd.Parameters.AddWithValue("@Year", year.Value);
                }

                if (isOwner)
                {
                    // Owner sees all employees' OT (except their own)
                    cmd.CommandText = @"
                        SELECT
                            O.ID, O.Admin_ID, O.OTDate, O.OTHours, O.OTRate,
                            O.WorkDescription, O.Status, O.EntryDate, O.Notes,
                            O.ApprovedBy_AdminID, O.ApprovedDate, O.RejectedReason,
                            ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                            A.Username AS EmployeeUsername,
                            ISNULL(Salary.Position, A.Role) AS EmployeePosition,
                            ISNULL(EnteredBy.FirstName + ' ' + EnteredBy.LastName, EnteredBy.Username) AS EnteredByName,
                            ISNULL(ApprovedBy.FirstName + ' ' + ApprovedBy.LastName, ApprovedBy.Username) AS ApprovedByName,
                            (O.OTHours * O.OTRate) AS OTMultipliedHours
                        FROM OT_Entry O
                        INNER JOIN Admin A ON A.ID = O.Admin_ID
                        INNER JOIN Admin EnteredBy ON EnteredBy.ID = O.EnteredBy_AdminID
                        LEFT JOIN Admin ApprovedBy ON ApprovedBy.ID = O.ApprovedBy_AdminID
                        LEFT JOIN Employee_Salary Salary ON Salary.Admin_ID = A.ID AND Salary.IsActive = 1
                        WHERE A.ID != @OwnerID" + filters + @"
                        ORDER BY O.OTDate DESC, O.EntryDate DESC";
                    cmd.Parameters.AddWithValue("@OwnerID", supervisorAdminId);
                }
                else
                {
                    // Regular supervisor - only their subordinates
                    cmd.CommandText = @"
                        SELECT
                            O.ID, O.Admin_ID, O.OTDate, O.OTHours, O.OTRate,
                            O.WorkDescription, O.Status, O.EntryDate, O.Notes,
                            O.ApprovedBy_AdminID, O.ApprovedDate, O.RejectedReason,
                            ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                            A.Username AS EmployeeUsername,
                            ISNULL(Salary.Position, A.Role) AS EmployeePosition,
                            ISNULL(EnteredBy.FirstName + ' ' + EnteredBy.LastName, EnteredBy.Username) AS EnteredByName,
                            ISNULL(ApprovedBy.FirstName + ' ' + ApprovedBy.LastName, ApprovedBy.Username) AS ApprovedByName,
                            (O.OTHours * O.OTRate) AS OTMultipliedHours
                        FROM OT_Entry O
                        INNER JOIN Admin A ON A.ID = O.Admin_ID
                        INNER JOIN Employee_Supervisor ES ON ES.Employee_AdminID = O.Admin_ID
                        INNER JOIN Admin EnteredBy ON EnteredBy.ID = O.EnteredBy_AdminID
                        LEFT JOIN Admin ApprovedBy ON ApprovedBy.ID = O.ApprovedBy_AdminID
                        LEFT JOIN Employee_Salary Salary ON Salary.Admin_ID = A.ID AND Salary.IsActive = 1
                        WHERE ES.Supervisor_AdminID = @SupervisorID
                          AND ES.IsActive = 1" + filters + @"
                        ORDER BY O.OTDate DESC, O.EntryDate DESC";
                    cmd.Parameters.AddWithValue("@SupervisorID", supervisorAdminId);
                }

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
    /// Approve OT entry
    /// </summary>
    public (bool Success, string Message) ApproveOTEntry(long otEntryId, short approverAdminId)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Check if entry exists and is pending
                using (SqlCommand checkCmd = new SqlCommand(
                    "SELECT Status FROM OT_Entry WHERE ID = @ID", conn))
                {
                    checkCmd.Parameters.AddWithValue("@ID", otEntryId);
                    object result = checkCmd.ExecuteScalar();

                    if (result == null)
                    {
                        return (false, "ไม่พบรายการ OT นี้");
                    }

                    if (result.ToString() != "PENDING")
                    {
                        return (false, "รายการนี้ได้รับการดำเนินการแล้ว");
                    }
                }

                // Approve
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE OT_Entry
                    SET Status = 'APPROVED',
                        ApprovedBy_AdminID = @ApproverID,
                        ApprovedDate = GETDATE()
                    WHERE ID = @ID", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", otEntryId);
                    cmd.Parameters.AddWithValue("@ApproverID", approverAdminId);
                    cmd.ExecuteNonQuery();
                }

                return (true, "อนุมัติ OT สำเร็จ");
            }
        }
        catch (Exception ex)
        {
            return (false, "เกิดข้อผิดพลาด: " + ex.Message);
        }
    }

    /// <summary>
    /// Reject OT entry
    /// </summary>
    public (bool Success, string Message) RejectOTEntry(long otEntryId, string reason, short rejecterAdminId)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE OT_Entry
                    SET Status = 'REJECTED',
                        RejectedReason = @Reason,
                        ApprovedBy_AdminID = @RejecterID,
                        ApprovedDate = GETDATE()
                    WHERE ID = @ID AND Status = 'PENDING'", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", otEntryId);
                    cmd.Parameters.AddWithValue("@Reason", reason ?? "ไม่ระบุเหตุผล");
                    cmd.Parameters.AddWithValue("@RejecterID", rejecterAdminId);

                    int affected = cmd.ExecuteNonQuery();
                    if (affected > 0)
                        return (true, "ปฏิเสธ OT สำเร็จ");
                    else
                        return (false, "ไม่สามารถปฏิเสธได้ หรือรายการถูกดำเนินการแล้ว");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, "เกิดข้อผิดพลาด: " + ex.Message);
        }
    }

    /// <summary>
    /// Delete OT entry (only if pending and created by same person)
    /// </summary>
    public (bool Success, string Message) DeleteOTEntry(long otEntryId, short requestByAdminId)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    DELETE FROM OT_Entry
                    WHERE ID = @ID AND Status = 'PENDING'
                      AND (EnteredBy_AdminID = @RequestBy OR Admin_ID = @RequestBy)", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", otEntryId);
                    cmd.Parameters.AddWithValue("@RequestBy", requestByAdminId);

                    int affected = cmd.ExecuteNonQuery();
                    if (affected > 0)
                        return (true, "ลบรายการ OT สำเร็จ");
                    else
                        return (false, "ไม่สามารถลบได้ หรือไม่มีสิทธิ์ลบรายการนี้");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, "เกิดข้อผิดพลาด: " + ex.Message);
        }
    }

    /// <summary>
    /// Get distinct years from OT_Entry table
    /// </summary>
    public DataTable GetDistinctOTYears()
    {
        EnsureTableExists();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT DISTINCT YEAR(OTDate) AS Year
                    FROM OT_Entry
                    UNION
                    SELECT YEAR(GETDATE()) AS Year
                    ORDER BY Year DESC";

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
    /// Get distinct years for supervisor's subordinates OT
    /// Owner sees all years from all OT entries
    /// </summary>
    public DataTable GetDistinctOTYearsForSupervisor(short supervisorAdminId)
    {
        EnsureTableExists();

        // Check if user is Owner - return all years
        SupervisorService supervisorService = new SupervisorService();
        bool isOwner = supervisorService.IsOwner(supervisorAdminId);

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                if (isOwner)
                {
                    // Owner sees all years from all OT entries
                    cmd.CommandText = @"
                        SELECT DISTINCT YEAR(O.OTDate) AS Year
                        FROM OT_Entry O
                        UNION
                        SELECT YEAR(GETDATE()) AS Year
                        ORDER BY Year DESC";
                }
                else
                {
                    // Regular supervisor - only years from their subordinates
                    cmd.CommandText = @"
                        SELECT DISTINCT YEAR(O.OTDate) AS Year
                        FROM OT_Entry O
                        INNER JOIN Employee_Supervisor ES ON ES.Employee_AdminID = O.Admin_ID
                        WHERE ES.Supervisor_AdminID = @SupervisorID AND ES.IsActive = 1
                        UNION
                        SELECT YEAR(GETDATE()) AS Year
                        ORDER BY Year DESC";
                    cmd.Parameters.AddWithValue("@SupervisorID", supervisorAdminId);
                }

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

    #region Statistics

    /// <summary>
    /// Get OT summary for employee
    /// </summary>
    public DataTable GetOTSummaryForEmployee(short adminId, int month, int year)
    {
        EnsureTableExists();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ISNULL(SUM(CASE WHEN Status = 'APPROVED' THEN OTHours ELSE 0 END), 0) AS ApprovedHours,
                        ISNULL(SUM(CASE WHEN Status = 'PENDING' THEN OTHours ELSE 0 END), 0) AS PendingHours,
                        ISNULL(SUM(CASE WHEN Status = 'APPROVED' THEN (OTHours * OTRate) ELSE 0 END), 0) AS TotalApprovedMultiplied,
                        COUNT(CASE WHEN Status = 'APPROVED' THEN 1 END) AS ApprovedCount,
                        COUNT(CASE WHEN Status = 'PENDING' THEN 1 END) AS PendingCount
                    FROM OT_Entry
                    WHERE Admin_ID = @AdminID
                      AND MONTH(OTDate) = @Month AND YEAR(OTDate) = @Year";
                cmd.Parameters.AddWithValue("@AdminID", adminId);
                cmd.Parameters.AddWithValue("@Month", month);
                cmd.Parameters.AddWithValue("@Year", year);

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
    /// Get OT dashboard stats for supervisor
    /// Owner sees stats for all employees
    /// </summary>
    public DataTable GetOTDashboardForSupervisor(short supervisorAdminId)
    {
        EnsureTableExists();

        // Check if user is Owner
        SupervisorService supervisorService = new SupervisorService();
        bool isOwner = supervisorService.IsOwner(supervisorAdminId);

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                if (isOwner)
                {
                    // Owner sees all employees' stats
                    cmd.CommandText = @"
                        SELECT
                            (SELECT COUNT(*) FROM OT_Entry O
                             WHERE O.Admin_ID != @OwnerID AND O.Status = 'PENDING') AS PendingOTEntries,
                            (SELECT ISNULL(SUM(O.OTHours), 0) FROM OT_Entry O
                             WHERE O.Admin_ID != @OwnerID AND O.Status = 'APPROVED'
                             AND MONTH(O.OTDate) = MONTH(GETDATE()) AND YEAR(O.OTDate) = YEAR(GETDATE())) AS ApprovedHoursThisMonth,
                            (SELECT COUNT(DISTINCT O.Admin_ID) FROM OT_Entry O
                             WHERE O.Admin_ID != @OwnerID AND O.Status = 'APPROVED'
                             AND MONTH(O.OTDate) = MONTH(GETDATE()) AND YEAR(O.OTDate) = YEAR(GETDATE())) AS EmployeesWithOTThisMonth";
                    cmd.Parameters.AddWithValue("@OwnerID", supervisorAdminId);
                }
                else
                {
                    // Regular supervisor
                    cmd.CommandText = @"
                        SELECT
                            (SELECT COUNT(*) FROM OT_Entry O
                             INNER JOIN Employee_Supervisor ES ON ES.Employee_AdminID = O.Admin_ID
                             WHERE ES.Supervisor_AdminID = @SupID AND ES.IsActive = 1
                             AND O.Status = 'PENDING') AS PendingOTEntries,
                            (SELECT ISNULL(SUM(O.OTHours), 0) FROM OT_Entry O
                             INNER JOIN Employee_Supervisor ES ON ES.Employee_AdminID = O.Admin_ID
                             WHERE ES.Supervisor_AdminID = @SupID AND ES.IsActive = 1
                             AND O.Status = 'APPROVED' AND MONTH(O.OTDate) = MONTH(GETDATE()) AND YEAR(O.OTDate) = YEAR(GETDATE())) AS ApprovedHoursThisMonth,
                            (SELECT COUNT(DISTINCT O.Admin_ID) FROM OT_Entry O
                             INNER JOIN Employee_Supervisor ES ON ES.Employee_AdminID = O.Admin_ID
                             WHERE ES.Supervisor_AdminID = @SupID AND ES.IsActive = 1
                             AND O.Status = 'APPROVED' AND MONTH(O.OTDate) = MONTH(GETDATE()) AND YEAR(O.OTDate) = YEAR(GETDATE())) AS EmployeesWithOTThisMonth";
                    cmd.Parameters.AddWithValue("@SupID", supervisorAdminId);
                }

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

    #region Admin Management

    /// <summary>
    /// Get all OT entries for admin management
    /// </summary>
    public DataTable GetAllOTEntries(string status = null, int? month = null, int? year = null, short? employeeId = null)
    {
        EnsureTableExists();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                string filters = "WHERE 1=1";
                if (!string.IsNullOrEmpty(status))
                {
                    filters += " AND O.Status = @Status";
                    cmd.Parameters.AddWithValue("@Status", status);
                }
                if (month.HasValue)
                {
                    filters += " AND MONTH(O.OTDate) = @Month";
                    cmd.Parameters.AddWithValue("@Month", month.Value);
                }
                if (year.HasValue)
                {
                    filters += " AND YEAR(O.OTDate) = @Year";
                    cmd.Parameters.AddWithValue("@Year", year.Value);
                }
                if (employeeId.HasValue)
                {
                    filters += " AND O.Admin_ID = @EmployeeID";
                    cmd.Parameters.AddWithValue("@EmployeeID", employeeId.Value);
                }

                cmd.CommandText = @"
                    SELECT
                        O.ID, O.Admin_ID, O.OTDate, O.OTHours, O.OTRate,
                        O.WorkDescription, O.Status, O.EntryDate, O.Notes,
                        O.ApprovedBy_AdminID, O.ApprovedDate, O.RejectedReason,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                        A.Username AS EmployeeUsername,
                        ISNULL(Salary.Position, A.Role) AS EmployeePosition,
                        ISNULL(EnteredBy.FirstName + ' ' + EnteredBy.LastName, EnteredBy.Username) AS EnteredByName,
                        ISNULL(ApprovedBy.FirstName + ' ' + ApprovedBy.LastName, ApprovedBy.Username) AS ApprovedByName,
                        (O.OTHours * O.OTRate) AS OTMultipliedHours
                    FROM OT_Entry O
                    INNER JOIN Admin A ON A.ID = O.Admin_ID
                    INNER JOIN Admin EnteredBy ON EnteredBy.ID = O.EnteredBy_AdminID
                    LEFT JOIN Admin ApprovedBy ON ApprovedBy.ID = O.ApprovedBy_AdminID
                    LEFT JOIN Employee_Salary Salary ON Salary.Admin_ID = A.ID AND Salary.IsActive = 1
                    " + filters + @"
                    ORDER BY O.OTDate DESC, O.EntryDate DESC";

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
    /// Get single OT entry by ID
    /// </summary>
    public DataTable GetOTEntryById(long otEntryId)
    {
        EnsureTableExists();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        O.ID, O.Admin_ID, O.OTDate, O.OTHours, O.OTRate,
                        O.WorkDescription, O.Status, O.EntryDate, O.Notes,
                        O.ApprovedBy_AdminID, O.ApprovedDate, O.RejectedReason,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                        A.Username AS EmployeeUsername
                    FROM OT_Entry O
                    INNER JOIN Admin A ON A.ID = O.Admin_ID
                    WHERE O.ID = @ID";
                cmd.Parameters.AddWithValue("@ID", otEntryId);

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
    /// Update OT entry (Admin only)
    /// </summary>
    public (bool Success, string Message) UpdateOTEntry(
        long otEntryId, DateTime otDate, decimal otHours, decimal otRate,
        string workDescription, string status, string notes, short updatedByAdminId)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Check if changing to APPROVED and currently not approved
                string currentStatus = "";
                using (SqlCommand checkCmd = new SqlCommand(
                    "SELECT Status FROM OT_Entry WHERE ID = @ID", conn))
                {
                    checkCmd.Parameters.AddWithValue("@ID", otEntryId);
                    object result = checkCmd.ExecuteScalar();
                    if (result == null)
                    {
                        return (false, "ไม่พบรายการ OT นี้");
                    }
                    currentStatus = result.ToString();
                }

                // Build update command
                string updateSql;
                if (status == "APPROVED" && currentStatus != "APPROVED")
                {
                    // Setting to approved - record approver
                    updateSql = @"
                        UPDATE OT_Entry
                        SET OTDate = @OTDate, OTHours = @OTHours, OTRate = @OTRate,
                            WorkDescription = @WorkDescription, Status = @Status,
                            Notes = @Notes, ApprovedBy_AdminID = @ApprovedBy, ApprovedDate = GETDATE()
                        WHERE ID = @ID";
                }
                else
                {
                    updateSql = @"
                        UPDATE OT_Entry
                        SET OTDate = @OTDate, OTHours = @OTHours, OTRate = @OTRate,
                            WorkDescription = @WorkDescription, Status = @Status, Notes = @Notes
                        WHERE ID = @ID";
                }

                using (SqlCommand cmd = new SqlCommand(updateSql, conn))
                {
                    cmd.Parameters.AddWithValue("@ID", otEntryId);
                    cmd.Parameters.AddWithValue("@OTDate", otDate.Date);
                    cmd.Parameters.AddWithValue("@OTHours", otHours);
                    cmd.Parameters.AddWithValue("@OTRate", otRate);
                    cmd.Parameters.AddWithValue("@WorkDescription", workDescription);
                    cmd.Parameters.AddWithValue("@Status", status);
                    cmd.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

                    if (status == "APPROVED" && currentStatus != "APPROVED")
                    {
                        cmd.Parameters.AddWithValue("@ApprovedBy", updatedByAdminId);
                    }

                    int affected = cmd.ExecuteNonQuery();
                    if (affected > 0)
                        return (true, "อัปเดต OT สำเร็จ");
                    else
                        return (false, "ไม่สามารถอัปเดตได้");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, "เกิดข้อผิดพลาด: " + ex.Message);
        }
    }

    /// <summary>
    /// Delete OT entry (Admin only - can delete any entry)
    /// </summary>
    public (bool Success, string Message) AdminDeleteOTEntry(long otEntryId)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(@"
                    DELETE FROM OT_Entry WHERE ID = @ID", conn))
                {
                    cmd.Parameters.AddWithValue("@ID", otEntryId);

                    conn.Open();
                    int affected = cmd.ExecuteNonQuery();
                    if (affected > 0)
                        return (true, "ลบรายการ OT สำเร็จ");
                    else
                        return (false, "ไม่พบรายการ OT นี้");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, "เกิดข้อผิดพลาด: " + ex.Message);
        }
    }

    /// <summary>
    /// Get all distinct years from OT entries
    /// </summary>
    public DataTable GetAllDistinctOTYears()
    {
        EnsureTableExists();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT DISTINCT YEAR(OTDate) AS Year
                    FROM OT_Entry
                    UNION
                    SELECT YEAR(GETDATE()) AS Year
                    ORDER BY Year DESC";

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
    /// Get all employees for dropdown filter
    /// </summary>
    public DataTable GetEmployeesWithOT()
    {
        EnsureTableExists();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT DISTINCT A.ID,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS Name
                    FROM OT_Entry O
                    INNER JOIN Admin A ON A.ID = O.Admin_ID
                    ORDER BY Name";

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
