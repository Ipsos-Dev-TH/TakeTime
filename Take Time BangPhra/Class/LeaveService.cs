using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

/// <summary>
/// Result class for leave operations returning an int ID
/// </summary>
public class LeaveOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int ID { get; set; }

    public LeaveOperationResult(bool success, string message, int id)
    {
        Success = success;
        Message = message;
        ID = id;
    }
}

/// <summary>
/// Result class for leave operations returning a long ID
/// </summary>
public class LeaveOperationResultLong
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public long ID { get; set; }

    public LeaveOperationResultLong(bool success, string message, long id)
    {
        Success = success;
        Message = message;
        ID = id;
    }
}

/// <summary>
/// Leave Service - Business logic for Leave Management
/// Handles leave types, quotas, requests, and approvals
/// </summary>
public class LeaveService
{
    private readonly string connectionString;

    public LeaveService()
    {
        connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
    }

    #region Leave Types Management

    /// <summary>
    /// Get all active leave types
    /// </summary>
    public DataTable GetLeaveTypes()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ID, LeaveTypeName, LeaveTypeCode, Description,
                        DeductSalary, RequiresMedicalCert, AnnualQuota,
                        RequiresApproval, IsActive, DisplayOrder
                    FROM Leave_Types
                    WHERE IsActive = 1
                    ORDER BY DisplayOrder, LeaveTypeName";

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
    /// Get leave type by ID
    /// </summary>
    public DataTable GetLeaveType(byte leaveTypeId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT * FROM Leave_Types
                    WHERE ID = @LeaveTypeID";
                cmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);

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
    /// Get all leave types including inactive (for management)
    /// </summary>
    public DataTable GetAllLeaveTypesForManagement()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ID, LeaveTypeName, LeaveTypeCode, Description,
                        DeductSalary, RequiresMedicalCert, AnnualQuota,
                        RequiresApproval, IsActive, DisplayOrder
                    FROM Leave_Types
                    ORDER BY DisplayOrder, LeaveTypeName";

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
    /// Create new leave type
    /// </summary>
    public (bool Success, string Message) CreateLeaveType(
        string leaveTypeName,
        string leaveTypeCode,
        string description,
        bool deductSalary,
        bool requiresMedicalCert,
        decimal annualQuota,
        bool requiresApproval,
        int displayOrder)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        INSERT INTO Leave_Types
                        (LeaveTypeName, LeaveTypeCode, Description, DeductSalary,
                         RequiresMedicalCert, AnnualQuota, RequiresApproval, IsActive, DisplayOrder)
                        VALUES
                        (@Name, @Code, @Desc, @Deduct, @MedCert, @Quota, @Approval, 1, @Order)";

                    cmd.Parameters.AddWithValue("@Name", leaveTypeName);
                    cmd.Parameters.AddWithValue("@Code", leaveTypeCode ?? "");
                    cmd.Parameters.AddWithValue("@Desc", description ?? "");
                    cmd.Parameters.AddWithValue("@Deduct", deductSalary);
                    cmd.Parameters.AddWithValue("@MedCert", requiresMedicalCert);
                    cmd.Parameters.AddWithValue("@Quota", annualQuota);
                    cmd.Parameters.AddWithValue("@Approval", requiresApproval);
                    cmd.Parameters.AddWithValue("@Order", displayOrder);

                    cmd.ExecuteNonQuery();
                    return (true, "สร้างประเภทการลาสำเร็จ");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, $"เกิดข้อผิดพลาด: {ex.Message}");
        }
    }

    /// <summary>
    /// Update leave type
    /// </summary>
    public (bool Success, string Message) UpdateLeaveType(
        byte leaveTypeId,
        string leaveTypeName,
        string leaveTypeCode,
        string description,
        bool deductSalary,
        bool requiresMedicalCert,
        decimal annualQuota,
        bool requiresApproval,
        bool isActive,
        int displayOrder)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = @"
                        UPDATE Leave_Types SET
                            LeaveTypeName = @Name,
                            LeaveTypeCode = @Code,
                            Description = @Desc,
                            DeductSalary = @Deduct,
                            RequiresMedicalCert = @MedCert,
                            AnnualQuota = @Quota,
                            RequiresApproval = @Approval,
                            IsActive = @Active,
                            DisplayOrder = @Order
                        WHERE ID = @ID";

                    cmd.Parameters.AddWithValue("@ID", leaveTypeId);
                    cmd.Parameters.AddWithValue("@Name", leaveTypeName);
                    cmd.Parameters.AddWithValue("@Code", leaveTypeCode ?? "");
                    cmd.Parameters.AddWithValue("@Desc", description ?? "");
                    cmd.Parameters.AddWithValue("@Deduct", deductSalary);
                    cmd.Parameters.AddWithValue("@MedCert", requiresMedicalCert);
                    cmd.Parameters.AddWithValue("@Quota", annualQuota);
                    cmd.Parameters.AddWithValue("@Approval", requiresApproval);
                    cmd.Parameters.AddWithValue("@Active", isActive);
                    cmd.Parameters.AddWithValue("@Order", displayOrder);

                    cmd.ExecuteNonQuery();
                    return (true, "อัพเดทประเภทการลาสำเร็จ");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, $"เกิดข้อผิดพลาด: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete (deactivate) leave type
    /// </summary>
    public (bool Success, string Message) DeleteLeaveType(byte leaveTypeId)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "UPDATE Leave_Types SET IsActive = 0 WHERE ID = @ID";
                    cmd.Parameters.AddWithValue("@ID", leaveTypeId);
                    cmd.ExecuteNonQuery();
                    return (true, "ลบประเภทการลาสำเร็จ");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, $"เกิดข้อผิดพลาด: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all employees with their leave quotas for a year
    /// </summary>
    public DataTable GetAllEmployeeQuotas(int year)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        A.ID AS AdminID,
                        A.UserName,
                        ISNULL(A.FirstName, '') + ' ' + ISNULL(A.LastName, '') AS EmployeeName,
                        A.UserName AS NickName,
                        LT.ID AS LeaveTypeID,
                        LT.LeaveTypeName,
                        ISNULL(ELQ.TotalDays, LT.AnnualQuota) AS TotalDays,
                        ISNULL(ELQ.UsedDays, 0) AS UsedDays,
                        ISNULL(ELQ.RemainingDays, LT.AnnualQuota) AS RemainingDays,
                        ISNULL(ELQ.CarryForwardDays, 0) AS CarryForwardDays
                    FROM Admin A
                    CROSS JOIN Leave_Types LT
                    LEFT JOIN Employee_Leave_Quota ELQ ON ELQ.Admin_ID = A.ID
                        AND ELQ.LeaveType_ID = LT.ID
                        AND ELQ.Year = @Year
                    WHERE A.Status = 1 AND LT.IsActive = 1
                    ORDER BY A.FirstName, A.LastName, LT.DisplayOrder";
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
    /// Update employee leave quota
    /// </summary>
    public (bool Success, string Message) UpdateEmployeeQuota(
        short adminId,
        byte leaveTypeId,
        int year,
        decimal totalDays,
        decimal carryForwardDays)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Check if quota exists
                using (SqlCommand checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Employee_Leave_Quota WHERE Admin_ID = @AdminID AND LeaveType_ID = @LeaveTypeID AND Year = @Year", conn))
                {
                    checkCmd.Parameters.AddWithValue("@AdminID", adminId);
                    checkCmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);
                    checkCmd.Parameters.AddWithValue("@Year", year);

                    int count = (int)checkCmd.ExecuteScalar();

                    if (count > 0)
                    {
                        // Update existing
                        using (SqlCommand updateCmd = new SqlCommand(@"
                            UPDATE Employee_Leave_Quota SET
                                TotalDays = @Total,
                                RemainingDays = @Total - UsedDays,
                                CarryForwardDays = @Carry
                            WHERE Admin_ID = @AdminID AND LeaveType_ID = @LeaveTypeID AND Year = @Year", conn))
                        {
                            updateCmd.Parameters.AddWithValue("@AdminID", adminId);
                            updateCmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);
                            updateCmd.Parameters.AddWithValue("@Year", year);
                            updateCmd.Parameters.AddWithValue("@Total", totalDays);
                            updateCmd.Parameters.AddWithValue("@Carry", carryForwardDays);
                            updateCmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // Insert new
                        using (SqlCommand insertCmd = new SqlCommand(@"
                            INSERT INTO Employee_Leave_Quota
                            (Admin_ID, LeaveType_ID, Year, TotalDays, UsedDays, RemainingDays, CarryForwardDays)
                            VALUES (@AdminID, @LeaveTypeID, @Year, @Total, 0, @Total, @Carry)", conn))
                        {
                            insertCmd.Parameters.AddWithValue("@AdminID", adminId);
                            insertCmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);
                            insertCmd.Parameters.AddWithValue("@Year", year);
                            insertCmd.Parameters.AddWithValue("@Total", totalDays);
                            insertCmd.Parameters.AddWithValue("@Carry", carryForwardDays);
                            insertCmd.ExecuteNonQuery();
                        }
                    }
                }
                return (true, "อัพเดทโควต้าสำเร็จ");
            }
        }
        catch (Exception ex)
        {
            return (false, $"เกิดข้อผิดพลาด: {ex.Message}");
        }
    }

    /// <summary>
    /// Initialize default quotas for all employees for a year
    /// </summary>
    public (bool Success, string Message, int Count) InitializeQuotasForYear(int year)
    {
        try
        {
            int count = 0;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO Employee_Leave_Quota (Admin_ID, LeaveType_ID, Year, TotalDays, UsedDays, RemainingDays, CarryForwardDays)
                    SELECT
                        A.ID, LT.ID, @Year, LT.AnnualQuota, 0, LT.AnnualQuota, 0
                    FROM Admin A
                    CROSS JOIN Leave_Types LT
                    WHERE A.Status = 1 AND LT.IsActive = 1
                        AND NOT EXISTS (
                            SELECT 1 FROM Employee_Leave_Quota ELQ
                            WHERE ELQ.Admin_ID = A.ID AND ELQ.LeaveType_ID = LT.ID AND ELQ.Year = @Year
                        )", conn))
                {
                    cmd.Parameters.AddWithValue("@Year", year);
                    count = cmd.ExecuteNonQuery();
                }
            }
            return (true, $"สร้างโควต้าเริ่มต้นสำเร็จ {count} รายการ", count);
        }
        catch (Exception ex)
        {
            return (false, $"เกิดข้อผิดพลาด: {ex.Message}", 0);
        }
    }

    #endregion

    #region Leave Quota Management

    /// <summary>
    /// Initialize leave quota for year
    /// </summary>
    public LeaveOperationResult InitializeLeaveQuotaForYear(
        short year, short? adminId = null)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();
            try
            {
                int recordsCreated = 0;

                // Get all leave types
                DataTable leaveTypes;
                using (SqlCommand ltCmd = new SqlCommand(
                    "SELECT ID, AnnualQuota FROM Leave_Types WHERE IsActive = 1", conn))
                {
                    using (SqlDataAdapter adapter = new SqlDataAdapter(ltCmd))
                    {
                        leaveTypes = new DataTable();
                        adapter.Fill(leaveTypes);
                    }
                }

                // Get employees - either specific or all active
                string empSql = adminId.HasValue
                    ? "SELECT ID FROM Admin WHERE ID = @AdminID AND Status = 1"
                    : "SELECT ID FROM Admin WHERE Status = 1";
                DataTable employees;
                using (SqlCommand empCmd = new SqlCommand(empSql, conn))
                {
                    if (adminId.HasValue)
                        empCmd.Parameters.AddWithValue("@AdminID", adminId.Value);
                    using (SqlDataAdapter adapter = new SqlDataAdapter(empCmd))
                    {
                        employees = new DataTable();
                        adapter.Fill(employees);
                    }
                }

                // Create leave quota for each employee and leave type
                foreach (DataRow emp in employees.Rows)
                {
                    int empId = Convert.ToInt32(emp["ID"]);
                    foreach (DataRow lt in leaveTypes.Rows)
                    {
                        int leaveTypeId = Convert.ToInt32(lt["ID"]);
                        decimal annualQuota = lt["AnnualQuota"] != DBNull.Value ? Convert.ToDecimal(lt["AnnualQuota"]) : 0;

                        // Check if quota already exists
                        using (SqlCommand checkCmd = new SqlCommand(
                            "SELECT COUNT(*) FROM Employee_Leave_Quota WHERE Admin_ID = @AdminID AND LeaveType_ID = @LeaveTypeID AND Year = @Year", conn))
                        {
                            checkCmd.Parameters.AddWithValue("@AdminID", empId);
                            checkCmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);
                            checkCmd.Parameters.AddWithValue("@Year", year);
                            if ((int)checkCmd.ExecuteScalar() > 0) continue;
                        }

                        // Insert new quota
                        using (SqlCommand insertCmd = new SqlCommand(@"
                            INSERT INTO Employee_Leave_Quota (Admin_ID, LeaveType_ID, Year, TotalDays, UsedDays, RemainingDays, CarryForwardDays)
                            VALUES (@AdminID, @LeaveTypeID, @Year, @Total, 0, @Total, 0)", conn))
                        {
                            insertCmd.Parameters.AddWithValue("@AdminID", empId);
                            insertCmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);
                            insertCmd.Parameters.AddWithValue("@Year", year);
                            insertCmd.Parameters.AddWithValue("@Total", annualQuota);
                            insertCmd.ExecuteNonQuery();
                            recordsCreated++;
                        }
                    }
                }

                return new LeaveOperationResult(true, $"สร้างโควต้าวันลาสำเร็จ {recordsCreated} รายการ", recordsCreated);
            }
            catch (Exception ex)
            {
                return new LeaveOperationResult(false, "เกิดข้อผิดพลาด: " + ex.Message, 0);
            }
        }
    }

    /// <summary>
    /// Get employee leave quota
    /// </summary>
    public DataTable GetEmployeeLeaveQuota(short adminId, short? year = null)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                if (!year.HasValue)
                    year = (short)DateTime.Now.Year;

                cmd.CommandText = @"
                    SELECT
                        ELQ.Year, LT.LeaveTypeName, LT.LeaveTypeCode,
                        ELQ.TotalDays, ELQ.UsedDays, ELQ.RemainingDays,
                        ELQ.CarryForwardDays, LT.DeductSalary
                    FROM Employee_Leave_Quota ELQ
                    INNER JOIN Leave_Types LT ON LT.ID = ELQ.LeaveType_ID
                    WHERE ELQ.Admin_ID = @AdminID AND ELQ.Year = @Year
                    ORDER BY LT.DisplayOrder, LT.LeaveTypeName";
                cmd.Parameters.AddWithValue("@AdminID", adminId);
                cmd.Parameters.AddWithValue("@Year", year.Value);

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
    /// Get employee leave summary
    /// </summary>
    public DataTable GetEmployeeLeaveSummary(short adminId, short? year = null)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                cmd.CommandText = @"
                    SELECT * FROM vw_Employee_Leave_Summary
                    WHERE Admin_ID = @AdminID" +
                    (year.HasValue ? " AND Year = @Year" : "") + @"
                    ORDER BY LeaveTypeName";
                cmd.Parameters.AddWithValue("@AdminID", adminId);

                if (year.HasValue)
                    cmd.Parameters.AddWithValue("@Year", year.Value);

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

    #region Leave Request Management

    /// <summary>
    /// Create leave request
    /// </summary>
    public LeaveOperationResultLong CreateLeaveRequest(
        short adminId, byte leaveTypeId, DateTime startDate, DateTime endDate,
        decimal totalDays, string reason, string medicalCertPath = null,
        short? submittedByAdminId = null)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                try
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();
                    cmd.Transaction = transaction;

                    try
                    {
                        // Generate request number
                        string requestNumber = "LR-" + DateTime.Now.ToString("yyyyMMdd") + "-" + adminId + "-" + DateTime.Now.ToString("HHmmss");

                        // Get leave type details
                        cmd.CommandText = @"
                            SELECT DeductSalary, RequiresMedicalCert
                            FROM Leave_Types WHERE ID = @LeaveTypeID";
                        cmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);

                        bool deductSalary = false;
                        bool requiresMedicalCert = false;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                deductSalary = Convert.ToBoolean(reader["DeductSalary"]);
                                requiresMedicalCert = Convert.ToBoolean(reader["RequiresMedicalCert"]);
                            }
                        }

                        // Check if medical certificate is required but not provided
                        if (requiresMedicalCert && string.IsNullOrEmpty(medicalCertPath))
                        {
                            transaction.Rollback();
                            return new LeaveOperationResultLong(false, "Medical certificate is required for this leave type", 0);
                        }

                        // Calculate deduction amount if applicable
                        decimal deductionAmount = 0;
                        if (deductSalary)
                        {
                            // Get employee's monthly salary
                            cmd.Parameters.Clear();
                            cmd.CommandType = CommandType.Text;
                            cmd.CommandText = @"
                                SELECT TOP 1 ISNULL(MonthlySalary, 0) AS MonthlySalary
                                FROM Employee_Salary
                                WHERE Admin_ID = @AdminID AND IsActive = 1
                                ORDER BY EffectiveDate DESC";
                            cmd.Parameters.AddWithValue("@AdminID", adminId);

                            object result = cmd.ExecuteScalar();
                            decimal monthlySalary = result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0;

                            // Calculate daily rate and deduction
                            // Daily rate = Monthly salary / days in month
                            int daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
                            decimal dailyRate = monthlySalary / daysInMonth;
                            deductionAmount = Math.Round(dailyRate * totalDays, 2);
                        }

                        // Insert leave request
                        cmd.Parameters.Clear();
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = @"
                            INSERT INTO Leave_Requests (
                                RequestNumber, Admin_ID, LeaveType_ID,
                                StartDate, EndDate, TotalDays, Reason,
                                DeductSalary, DeductionAmount, MedicalCertPath,
                                Status, CreatedBy_AdminID, CreatedDate
                            )
                            VALUES (
                                @RequestNumber, @AdminID, @LeaveTypeID,
                                @StartDate, @EndDate, @TotalDays, @Reason,
                                @DeductSalary, @DeductionAmount, @MedicalCertPath,
                                'PENDING', @SubmittedBy, GETDATE()
                            );
                            SELECT SCOPE_IDENTITY();";

                        cmd.Parameters.AddWithValue("@RequestNumber", requestNumber);
                        cmd.Parameters.AddWithValue("@AdminID", adminId);
                        cmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);
                        cmd.Parameters.AddWithValue("@StartDate", startDate);
                        cmd.Parameters.AddWithValue("@EndDate", endDate);
                        cmd.Parameters.AddWithValue("@TotalDays", totalDays);
                        cmd.Parameters.AddWithValue("@Reason", reason ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@DeductSalary", deductSalary);
                        cmd.Parameters.AddWithValue("@DeductionAmount", deductionAmount);
                        cmd.Parameters.AddWithValue("@MedicalCertPath", medicalCertPath ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@SubmittedBy", submittedByAdminId ?? adminId);

                        long requestId = Convert.ToInt64(cmd.ExecuteScalar());

                        transaction.Commit();
                        return new LeaveOperationResultLong(true, "Leave request created successfully", requestId);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return new LeaveOperationResultLong(false, ex.Message, 0);
                    }
                }
                catch (Exception ex)
                {
                    return new LeaveOperationResultLong(false, ex.Message, 0);
                }
            }
        }
    }

    /// <summary>
    /// Get leave requests
    /// </summary>
    public DataTable GetLeaveRequests(short? adminId = null, string status = null, short? year = null)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                var whereClauses = new List<string>();

                if (adminId.HasValue)
                {
                    whereClauses.Add("LR.Admin_ID = @AdminID");
                    cmd.Parameters.AddWithValue("@AdminID", adminId.Value);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    whereClauses.Add("LR.Status = @Status");
                    cmd.Parameters.AddWithValue("@Status", status);
                }

                if (year.HasValue)
                {
                    whereClauses.Add("YEAR(LR.StartDate) = @Year");
                    cmd.Parameters.AddWithValue("@Year", year.Value);
                }

                string whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

                cmd.CommandText = @"
                    SELECT
                        LR.ID, LR.RequestNumber, LR.Admin_ID,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName, A.Username AS NickName,
                        LT.LeaveTypeName, LT.LeaveTypeCode,
                        LR.StartDate, LR.EndDate, LR.TotalDays,
                        LR.Reason, LR.Status, LR.DeductSalary, LR.DeductionAmount,
                        LR.MedicalCertPath, LR.CreatedDate AS SubmittedDate,
                        LR.ApprovedBy_AdminID, LR.ApprovedDate,
                        LR.RejectedReason,
                        ISNULL(ApprovedBy.FirstName + ' ' + ApprovedBy.LastName, ApprovedBy.Username) AS ApprovedByName
                    FROM Leave_Requests LR
                    INNER JOIN Admin A ON A.ID = LR.Admin_ID
                    INNER JOIN Leave_Types LT ON LT.ID = LR.LeaveType_ID
                    LEFT JOIN Admin ApprovedBy ON ApprovedBy.ID = LR.ApprovedBy_AdminID
                    " + whereClause + @"
                    ORDER BY LR.CreatedDate DESC";

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
    /// Approve leave request
    /// </summary>
    public bool ApproveLeaveRequest(long requestId, short approvedByAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                try
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();
                    cmd.Transaction = transaction;

                    try
                    {
                        // Get request details
                        cmd.CommandText = @"
                            SELECT Admin_ID, LeaveType_ID, TotalDays, YEAR(StartDate) AS Year, Status
                            FROM Leave_Requests
                            WHERE ID = @RequestID";
                        cmd.Parameters.AddWithValue("@RequestID", requestId);

                        short adminId = 0;
                        byte leaveTypeId = 0;
                        decimal totalDays = 0;
                        short year = 0;
                        string status = "";

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                adminId = Convert.ToInt16(reader["Admin_ID"]);
                                leaveTypeId = Convert.ToByte(reader["LeaveType_ID"]);
                                totalDays = Convert.ToDecimal(reader["TotalDays"]);
                                year = Convert.ToInt16(reader["Year"]);
                                status = reader["Status"]?.ToString() ?? "";
                            }
                            else
                            {
                                transaction.Rollback();
                                return false;
                            }
                        }

                        // Check if already processed
                        if (status != "PENDING")
                        {
                            transaction.Rollback();
                            return false;
                        }

                        // Check leave quota before approval
                        cmd.Parameters.Clear();
                        cmd.CommandText = @"
                            SELECT RemainingDays FROM Employee_Leave_Quota
                            WHERE Admin_ID = @AdminID AND LeaveType_ID = @LeaveTypeID AND Year = @Year";
                        cmd.Parameters.AddWithValue("@AdminID", adminId);
                        cmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);
                        cmd.Parameters.AddWithValue("@Year", year);

                        object remainingResult = cmd.ExecuteScalar();
                        decimal remainingDays = remainingResult != null && remainingResult != DBNull.Value
                            ? Convert.ToDecimal(remainingResult) : 0;

                        if (remainingDays < totalDays)
                        {
                            // Not enough leave quota
                            transaction.Rollback();
                            return false;
                        }

                        // Update leave request status
                        cmd.Parameters.Clear();
                        cmd.CommandText = @"
                            UPDATE Leave_Requests
                            SET Status = 'APPROVED',
                                ApprovedBy_AdminID = @ApprovedBy,
                                ApprovedDate = GETDATE()
                            WHERE ID = @RequestID";
                        cmd.Parameters.AddWithValue("@RequestID", requestId);
                        cmd.Parameters.AddWithValue("@ApprovedBy", approvedByAdminId);
                        cmd.ExecuteNonQuery();

                        // Update leave quota (UsedDays and RemainingDays)
                        cmd.Parameters.Clear();
                        cmd.CommandText = @"
                            UPDATE Employee_Leave_Quota
                            SET UsedDays = UsedDays + @TotalDays,
                                RemainingDays = RemainingDays - @TotalDays
                            WHERE Admin_ID = @AdminID
                              AND LeaveType_ID = @LeaveTypeID
                              AND Year = @Year";
                        cmd.Parameters.AddWithValue("@AdminID", adminId);
                        cmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);
                        cmd.Parameters.AddWithValue("@Year", year);
                        cmd.Parameters.AddWithValue("@TotalDays", totalDays);
                        cmd.ExecuteNonQuery();

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// Reject leave request
    /// </summary>
    public bool RejectLeaveRequest(long requestId, string rejectedReason, short rejectedByAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    UPDATE Leave_Requests
                    SET Status = 'REJECTED',
                        RejectedReason = @RejectedReason,
                        ApprovedBy_AdminID = @RejectedBy,
                        ApprovedDate = GETDATE()
                    WHERE ID = @RequestID";
                cmd.Parameters.AddWithValue("@RequestID", requestId);
                cmd.Parameters.AddWithValue("@RejectedReason", rejectedReason ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@RejectedBy", rejectedByAdminId);

                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    /// <summary>
    /// Calculate leave deduction
    /// </summary>
    public (decimal DeductionAmount, decimal BaseSalary, int DaysInMonth, string Message) CalculateLeaveDeduction(
        short adminId, DateTime startDate, DateTime endDate, decimal totalDays)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT TOP 1 ISNULL(MonthlySalary, 0) AS MonthlySalary
                    FROM Employee_Salary
                    WHERE Admin_ID = @AdminID AND IsActive = 1
                    ORDER BY EffectiveDate DESC";
                cmd.Parameters.AddWithValue("@AdminID", adminId);

                conn.Open();
                object result = cmd.ExecuteScalar();
                decimal baseSalary = result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0;

                // Calculate daily rate and deduction
                int daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
                decimal dailyRate = baseSalary / daysInMonth;
                decimal deductionAmount = Math.Round(dailyRate * totalDays, 2);

                return (deductionAmount, baseSalary, daysInMonth, "Success");
            }
        }
    }

    #endregion

    #region Statistics & Reports

    /// <summary>
    /// Get leave statistics for dashboard
    /// </summary>
    public DataTable GetLeaveStatistics(short? year = null)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                if (!year.HasValue)
                    year = (short)DateTime.Now.Year;

                cmd.CommandText = @"
                    SELECT
                        (SELECT COUNT(*) FROM Leave_Requests WHERE Status = 'PENDING') AS PendingRequests,
                        (SELECT COUNT(*) FROM Leave_Requests WHERE Status = 'APPROVED' AND YEAR(StartDate) = @Year) AS ApprovedThisYear,
                        (SELECT COUNT(*) FROM Leave_Requests WHERE Status = 'REJECTED' AND YEAR(StartDate) = @Year) AS RejectedThisYear,
                        (SELECT ISNULL(SUM(TotalDays), 0) FROM Leave_Requests WHERE Status = 'APPROVED' AND YEAR(StartDate) = @Year) AS TotalDaysApproved,
                        (SELECT COUNT(DISTINCT Admin_ID) FROM Leave_Requests WHERE Status = 'APPROVED' AND YEAR(StartDate) = @Year) AS EmployeesWithLeave";
                cmd.Parameters.AddWithValue("@Year", year.Value);

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
    /// Get leave usage report by type
    /// </summary>
    public DataTable GetLeaveUsageByType(short year)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        LT.LeaveTypeName,
                        COUNT(LR.ID) AS RequestCount,
                        SUM(LR.TotalDays) AS TotalDays,
                        AVG(LR.TotalDays) AS AvgDaysPerRequest
                    FROM Leave_Requests LR
                    INNER JOIN Leave_Types LT ON LT.ID = LR.LeaveType_ID
                    WHERE LR.Status = 'APPROVED' AND YEAR(LR.StartDate) = @Year
                    GROUP BY LT.LeaveTypeName
                    ORDER BY TotalDays DESC";
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

    #endregion

    #region Supervisor Leave Operations

    /// <summary>
    /// Get pending leave requests for supervisor's subordinates
    /// </summary>
    public DataTable GetPendingLeaveRequestsForSupervisor(short supervisorAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        LR.ID, LR.RequestNumber, LR.Admin_ID,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                        A.Username AS NickName,
                        LT.LeaveTypeName, LT.LeaveTypeCode,
                        LR.StartDate, LR.EndDate, LR.TotalDays,
                        LR.Reason, LR.Status, LR.DeductSalary, LR.DeductionAmount,
                        LR.MedicalCertPath, LR.CreatedDate AS SubmittedDate,
                        ES.IsPrimary AS IsPrimarySupervisor,
                        ISNULL(Salary.Position, A.Role) AS EmployeePosition
                    FROM Leave_Requests LR
                    INNER JOIN Admin A ON A.ID = LR.Admin_ID
                    INNER JOIN Leave_Types LT ON LT.ID = LR.LeaveType_ID
                    INNER JOIN Employee_Supervisor ES ON ES.Employee_AdminID = LR.Admin_ID
                    LEFT JOIN Employee_Salary Salary ON Salary.Admin_ID = A.ID AND Salary.IsActive = 1
                    WHERE ES.Supervisor_AdminID = @SupervisorID
                      AND ES.IsActive = 1
                      AND ES.CanApproveLeave = 1
                      AND LR.Status = 'PENDING'
                    ORDER BY LR.CreatedDate ASC";
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
    /// Get all leave requests for supervisor's subordinates (with filters)
    /// </summary>
    public DataTable GetLeaveRequestsForSupervisor(short supervisorAdminId, string status = null, short? year = null)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                var whereClauses = new List<string>();
                whereClauses.Add("ES.Supervisor_AdminID = @SupervisorID");
                whereClauses.Add("ES.IsActive = 1");
                cmd.Parameters.AddWithValue("@SupervisorID", supervisorAdminId);

                if (!string.IsNullOrEmpty(status))
                {
                    whereClauses.Add("LR.Status = @Status");
                    cmd.Parameters.AddWithValue("@Status", status);
                }

                if (year.HasValue)
                {
                    whereClauses.Add("YEAR(LR.StartDate) = @Year");
                    cmd.Parameters.AddWithValue("@Year", year.Value);
                }

                string whereClause = "WHERE " + string.Join(" AND ", whereClauses);

                cmd.CommandText = @"
                    SELECT
                        LR.ID, LR.RequestNumber, LR.Admin_ID,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                        A.Username AS NickName,
                        LT.LeaveTypeName, LT.LeaveTypeCode,
                        LR.StartDate, LR.EndDate, LR.TotalDays,
                        LR.Reason, LR.Status, LR.DeductSalary, LR.DeductionAmount,
                        LR.MedicalCertPath, LR.CreatedDate AS SubmittedDate,
                        LR.ApprovedBy_AdminID, LR.ApprovedDate,
                        LR.RejectedReason,
                        ISNULL(ApprovedBy.FirstName + ' ' + ApprovedBy.LastName, ApprovedBy.Username) AS ApprovedByName,
                        ES.IsPrimary AS IsPrimarySupervisor,
                        ES.CanApproveLeave
                    FROM Leave_Requests LR
                    INNER JOIN Admin A ON A.ID = LR.Admin_ID
                    INNER JOIN Leave_Types LT ON LT.ID = LR.LeaveType_ID
                    INNER JOIN Employee_Supervisor ES ON ES.Employee_AdminID = LR.Admin_ID
                    LEFT JOIN Admin ApprovedBy ON ApprovedBy.ID = LR.ApprovedBy_AdminID
                    " + whereClause + @"
                    ORDER BY LR.CreatedDate DESC";

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
    /// Check if supervisor can approve leave for employee
    /// </summary>
    public bool CanApproveLeaveFor(short supervisorAdminId, short employeeAdminId)
    {
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
    /// Approve leave request (with supervisor validation)
    /// </summary>
    public (bool Success, string Message) ApproveLeaveRequestBySupervisor(long requestId, short approverAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();
            SqlTransaction transaction = conn.BeginTransaction();

            try
            {
                // Get request details and verify approver has permission
                SqlCommand cmd = new SqlCommand();
                cmd.Connection = conn;
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    SELECT LR.Admin_ID, LR.LeaveType_ID, LR.TotalDays, YEAR(LR.StartDate) AS Year, LR.Status
                    FROM Leave_Requests LR
                    WHERE LR.ID = @RequestID";
                cmd.Parameters.AddWithValue("@RequestID", requestId);

                short adminId = 0;
                byte leaveTypeId = 0;
                decimal totalDays = 0;
                short year = 0;
                string currentStatus = "";

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        adminId = Convert.ToInt16(reader["Admin_ID"]);
                        leaveTypeId = Convert.ToByte(reader["LeaveType_ID"]);
                        totalDays = Convert.ToDecimal(reader["TotalDays"]);
                        year = Convert.ToInt16(reader["Year"]);
                        currentStatus = reader["Status"].ToString();
                    }
                    else
                    {
                        transaction.Rollback();
                        return (false, "ไม่พบคำขอลานี้");
                    }
                }

                if (currentStatus != "PENDING")
                {
                    transaction.Rollback();
                    return (false, "คำขอลานี้ได้รับการดำเนินการแล้ว");
                }

                // Check if approver is supervisor of this employee (or is Admin/Owner)
                cmd.Parameters.Clear();
                cmd.CommandText = @"
                    SELECT
                        (SELECT COUNT(*) FROM Employee_Supervisor
                         WHERE Supervisor_AdminID = @ApproverID AND Employee_AdminID = @EmpID
                         AND IsActive = 1 AND CanApproveLeave = 1) AS IsSupervisor,
                        (SELECT Role FROM Admin WHERE ID = @ApproverID) AS ApproverRole";
                cmd.Parameters.AddWithValue("@ApproverID", approverAdminId);
                cmd.Parameters.AddWithValue("@EmpID", adminId);

                bool canApprove = false;
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int isSupervisor = Convert.ToInt32(reader["IsSupervisor"]);
                        string approverRole = reader["ApproverRole"]?.ToString() ?? "";

                        // Can approve if: is supervisor, or is Admin/Owner
                        canApprove = isSupervisor > 0 || approverRole == "Admin" || approverRole == "Owner";
                    }
                }

                if (!canApprove)
                {
                    transaction.Rollback();
                    return (false, "คุณไม่มีสิทธิ์อนุมัติคำขอลานี้");
                }

                // Check leave quota before approval
                cmd.Parameters.Clear();
                cmd.CommandText = @"
                    SELECT RemainingDays FROM Employee_Leave_Quota
                    WHERE Admin_ID = @AdminID AND LeaveType_ID = @LeaveTypeID AND Year = @Year";
                cmd.Parameters.AddWithValue("@AdminID", adminId);
                cmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);
                cmd.Parameters.AddWithValue("@Year", year);

                object remainingResult = cmd.ExecuteScalar();
                decimal remainingDays = remainingResult != null && remainingResult != DBNull.Value
                    ? Convert.ToDecimal(remainingResult) : 0;

                if (remainingDays < totalDays)
                {
                    transaction.Rollback();
                    return (false, $"วันลาคงเหลือไม่เพียงพอ (เหลือ {remainingDays} วัน, ขอ {totalDays} วัน)");
                }

                // Update leave request status
                cmd.Parameters.Clear();
                cmd.CommandText = @"
                    UPDATE Leave_Requests
                    SET Status = 'APPROVED',
                        ApprovedBy_AdminID = @ApproverID,
                        ApprovedDate = GETDATE()
                    WHERE ID = @RequestID";
                cmd.Parameters.AddWithValue("@RequestID", requestId);
                cmd.Parameters.AddWithValue("@ApproverID", approverAdminId);
                cmd.ExecuteNonQuery();

                // Update leave quota
                cmd.Parameters.Clear();
                cmd.CommandText = @"
                    UPDATE Employee_Leave_Quota
                    SET UsedDays = UsedDays + @TotalDays,
                        RemainingDays = RemainingDays - @TotalDays
                    WHERE Admin_ID = @AdminID
                      AND LeaveType_ID = @LeaveTypeID
                      AND Year = @Year";
                cmd.Parameters.AddWithValue("@AdminID", adminId);
                cmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);
                cmd.Parameters.AddWithValue("@Year", year);
                cmd.Parameters.AddWithValue("@TotalDays", totalDays);
                cmd.ExecuteNonQuery();

                transaction.Commit();
                return (true, "อนุมัติคำขอลาสำเร็จ");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return (false, "เกิดข้อผิดพลาด: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Reject leave request (with supervisor validation)
    /// </summary>
    public (bool Success, string Message) RejectLeaveRequestBySupervisor(long requestId, string rejectedReason, short rejectorAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();

            try
            {
                // Get employee ID and current status
                SqlCommand cmd = new SqlCommand();
                cmd.Connection = conn;
                cmd.CommandText = @"SELECT Admin_ID, Status FROM Leave_Requests WHERE ID = @RequestID";
                cmd.Parameters.AddWithValue("@RequestID", requestId);

                short adminId = 0;
                string currentStatus = "";
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        adminId = Convert.ToInt16(reader["Admin_ID"]);
                        currentStatus = reader["Status"].ToString();
                    }
                    else
                    {
                        return (false, "ไม่พบคำขอลานี้");
                    }
                }

                if (currentStatus != "PENDING")
                {
                    return (false, "คำขอลานี้ได้รับการดำเนินการแล้ว");
                }

                // Check if rejector is supervisor of this employee (or is Admin/Owner)
                cmd.Parameters.Clear();
                cmd.CommandText = @"
                    SELECT
                        (SELECT COUNT(*) FROM Employee_Supervisor
                         WHERE Supervisor_AdminID = @RejectorID AND Employee_AdminID = @EmpID
                         AND IsActive = 1 AND CanApproveLeave = 1) AS IsSupervisor,
                        (SELECT Role FROM Admin WHERE ID = @RejectorID) AS RejectorRole";
                cmd.Parameters.AddWithValue("@RejectorID", rejectorAdminId);
                cmd.Parameters.AddWithValue("@EmpID", adminId);

                bool canReject = false;
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int isSupervisor = Convert.ToInt32(reader["IsSupervisor"]);
                        string rejectorRole = reader["RejectorRole"]?.ToString() ?? "";
                        canReject = isSupervisor > 0 || rejectorRole == "Admin" || rejectorRole == "Owner";
                    }
                }

                if (!canReject)
                {
                    return (false, "คุณไม่มีสิทธิ์ปฏิเสธคำขอลานี้");
                }

                // Update leave request status
                cmd.Parameters.Clear();
                cmd.CommandText = @"
                    UPDATE Leave_Requests
                    SET Status = 'REJECTED',
                        RejectedReason = @RejectedReason,
                        ApprovedBy_AdminID = @RejectorID,
                        ApprovedDate = GETDATE()
                    WHERE ID = @RequestID";
                cmd.Parameters.AddWithValue("@RequestID", requestId);
                cmd.Parameters.AddWithValue("@RejectedReason", rejectedReason ?? "ไม่ได้ระบุเหตุผล");
                cmd.Parameters.AddWithValue("@RejectorID", rejectorAdminId);

                int affected = cmd.ExecuteNonQuery();
                if (affected > 0)
                    return (true, "ปฏิเสธคำขอลาสำเร็จ");
                else
                    return (false, "ไม่สามารถปฏิเสธคำขอลาได้");
            }
            catch (Exception ex)
            {
                return (false, "เกิดข้อผิดพลาด: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Mark leave request as replaced by work (ทำงานทดแทน)
    /// When marked as replaced, the leave won't count toward payroll deductions
    /// </summary>
    public (bool Success, string Message) MarkLeaveAsReplaced(long requestId, short approverAdminId, DateTime replacementDate)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            try
            {
                conn.Open();

                // Check current status and get details
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT Admin_ID, Status, TotalDays, LeaveType_ID, YEAR(StartDate) AS Year
                    FROM Leave_Requests
                    WHERE ID = @RequestID", conn))
                {
                    cmd.Parameters.AddWithValue("@RequestID", requestId);

                    short adminId = 0;
                    string status = "";
                    decimal totalDays = 0;
                    byte leaveTypeId = 0;
                    int year = 0;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            adminId = Convert.ToInt16(reader["Admin_ID"]);
                            status = reader["Status"]?.ToString() ?? "";
                            totalDays = Convert.ToDecimal(reader["TotalDays"]);
                            leaveTypeId = Convert.ToByte(reader["LeaveType_ID"]);
                            year = Convert.ToInt32(reader["Year"]);
                        }
                        else
                        {
                            return (false, "ไม่พบคำขอลา");
                        }
                    }

                    // Only approved leaves can be marked as replaced
                    if (status != "APPROVED")
                    {
                        return (false, "สามารถทำงานทดแทนได้เฉพาะใบลาที่อนุมัติแล้วเท่านั้น");
                    }

                    // Check if approver has permission (Admin/Owner or supervisor)
                    using (SqlCommand checkCmd = new SqlCommand(@"
                        SELECT
                            (SELECT COUNT(*) FROM Employee_Supervisor
                             WHERE Supervisor_AdminID = @ApproverID AND Employee_AdminID = @EmpID
                             AND IsActive = 1) AS IsSupervisor,
                            (SELECT Role FROM Admin WHERE ID = @ApproverID) AS ApproverRole", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@ApproverID", approverAdminId);
                        checkCmd.Parameters.AddWithValue("@EmpID", adminId);

                        bool canApprove = false;
                        using (SqlDataReader permReader = checkCmd.ExecuteReader())
                        {
                            if (permReader.Read())
                            {
                                int isSupervisor = Convert.ToInt32(permReader["IsSupervisor"]);
                                string approverRole = permReader["ApproverRole"]?.ToString() ?? "";
                                canApprove = isSupervisor > 0 || approverRole == "Admin" || approverRole == "Owner";
                            }
                        }

                        if (!canApprove)
                        {
                            return (false, "คุณไม่มีสิทธิ์จัดการการลานี้");
                        }
                    }

                    // Begin transaction
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Update leave request to mark as replaced
                            using (SqlCommand updateCmd = new SqlCommand(@"
                                UPDATE Leave_Requests
                                SET IsReplaced = 1,
                                    ReplacementDate = @ReplacementDate,
                                    ReplacementApprovedBy = @ApproverID,
                                    ReplacementApprovedDate = GETDATE()
                                WHERE ID = @RequestID", conn, transaction))
                            {
                                updateCmd.Parameters.AddWithValue("@RequestID", requestId);
                                updateCmd.Parameters.AddWithValue("@ReplacementDate", replacementDate);
                                updateCmd.Parameters.AddWithValue("@ApproverID", approverAdminId);
                                updateCmd.ExecuteNonQuery();
                            }

                            // Return the leave days back to the quota (since it's now replaced)
                            using (SqlCommand quotaCmd = new SqlCommand(@"
                                UPDATE Employee_Leave_Quota
                                SET UsedDays = UsedDays - @TotalDays,
                                    RemainingDays = RemainingDays + @TotalDays
                                WHERE Admin_ID = @AdminID
                                  AND LeaveType_ID = @LeaveTypeID
                                  AND Year = @Year", conn, transaction))
                            {
                                quotaCmd.Parameters.AddWithValue("@TotalDays", totalDays);
                                quotaCmd.Parameters.AddWithValue("@AdminID", adminId);
                                quotaCmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);
                                quotaCmd.Parameters.AddWithValue("@Year", year);
                                quotaCmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            return (true, $"บันทึกการทำงานทดแทนสำเร็จ วันที่ {replacementDate:dd/MM/yyyy}");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, "เกิดข้อผิดพลาด: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Cancel work replacement (ยกเลิกทำงานทดแทน)
    /// </summary>
    public (bool Success, string Message) CancelWorkReplacement(long requestId, short approverAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            try
            {
                conn.Open();

                // Check current status and get details
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT Admin_ID, Status, TotalDays, LeaveType_ID, YEAR(StartDate) AS Year, ISNULL(IsReplaced, 0) AS IsReplaced
                    FROM Leave_Requests
                    WHERE ID = @RequestID", conn))
                {
                    cmd.Parameters.AddWithValue("@RequestID", requestId);

                    short adminId = 0;
                    string status = "";
                    decimal totalDays = 0;
                    byte leaveTypeId = 0;
                    int year = 0;
                    bool isReplaced = false;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            adminId = Convert.ToInt16(reader["Admin_ID"]);
                            status = reader["Status"]?.ToString() ?? "";
                            totalDays = Convert.ToDecimal(reader["TotalDays"]);
                            leaveTypeId = Convert.ToByte(reader["LeaveType_ID"]);
                            year = Convert.ToInt32(reader["Year"]);
                            isReplaced = Convert.ToBoolean(reader["IsReplaced"]);
                        }
                        else
                        {
                            return (false, "ไม่พบคำขอลา");
                        }
                    }

                    if (!isReplaced)
                    {
                        return (false, "คำขอลานี้ไม่ได้ทำงานทดแทน");
                    }

                    // Check if approver has permission
                    using (SqlCommand checkCmd = new SqlCommand(@"
                        SELECT Role FROM Admin WHERE ID = @ApproverID", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@ApproverID", approverAdminId);
                        string role = checkCmd.ExecuteScalar()?.ToString() ?? "";
                        if (role != "Admin" && role != "Owner")
                        {
                            return (false, "เฉพาะ Admin หรือ Owner เท่านั้นที่สามารถยกเลิกการทำงานทดแทนได้");
                        }
                    }

                    // Begin transaction
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Update leave request to cancel replacement
                            using (SqlCommand updateCmd = new SqlCommand(@"
                                UPDATE Leave_Requests
                                SET IsReplaced = 0,
                                    ReplacementDate = NULL,
                                    ReplacementApprovedBy = NULL,
                                    ReplacementApprovedDate = NULL
                                WHERE ID = @RequestID", conn, transaction))
                            {
                                updateCmd.Parameters.AddWithValue("@RequestID", requestId);
                                updateCmd.ExecuteNonQuery();
                            }

                            // Deduct the leave days from quota again
                            using (SqlCommand quotaCmd = new SqlCommand(@"
                                UPDATE Employee_Leave_Quota
                                SET UsedDays = UsedDays + @TotalDays,
                                    RemainingDays = RemainingDays - @TotalDays
                                WHERE Admin_ID = @AdminID
                                  AND LeaveType_ID = @LeaveTypeID
                                  AND Year = @Year", conn, transaction))
                            {
                                quotaCmd.Parameters.AddWithValue("@TotalDays", totalDays);
                                quotaCmd.Parameters.AddWithValue("@AdminID", adminId);
                                quotaCmd.Parameters.AddWithValue("@LeaveTypeID", leaveTypeId);
                                quotaCmd.Parameters.AddWithValue("@Year", year);
                                quotaCmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            return (true, "ยกเลิกการทำงานทดแทนสำเร็จ");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, "เกิดข้อผิดพลาด: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Get leave requests for a specific employee that can be replaced
    /// </summary>
    public DataTable GetReplaceableLeaves(short adminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT LR.ID, LR.StartDate, LR.EndDate, LR.TotalDays, LR.Reason,
                       LT.LeaveTypeName, ISNULL(LR.IsReplaced, 0) AS IsReplaced, LR.ReplacementDate
                FROM Leave_Requests LR
                INNER JOIN Leave_Types LT ON LT.ID = LR.LeaveType_ID
                WHERE LR.Admin_ID = @AdminID
                  AND LR.Status = 'APPROVED'
                ORDER BY LR.StartDate DESC", conn))
            {
                cmd.Parameters.AddWithValue("@AdminID", adminId);
                conn.Open();
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
    /// Get leave requests for a specific employee (for supervisor or self-view)
    /// </summary>
    public DataTable GetLeaveRequestsForEmployee(short employeeAdminId, short? year = null)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                string yearFilter = year.HasValue ? " AND YEAR(LR.StartDate) = @Year" : "";

                cmd.CommandText = @"
                    SELECT
                        LR.ID, LR.RequestNumber,
                        LT.LeaveTypeName, LT.LeaveTypeCode,
                        LR.StartDate, LR.EndDate, LR.TotalDays,
                        LR.Reason, LR.Status, LR.DeductSalary, LR.DeductionAmount,
                        LR.MedicalCertPath, LR.CreatedDate AS SubmittedDate,
                        LR.ApprovedBy_AdminID, LR.ApprovedDate, LR.RejectedReason,
                        ISNULL(ApprovedBy.FirstName + ' ' + ApprovedBy.LastName, ApprovedBy.Username) AS ApprovedByName
                    FROM Leave_Requests LR
                    INNER JOIN Leave_Types LT ON LT.ID = LR.LeaveType_ID
                    LEFT JOIN Admin ApprovedBy ON ApprovedBy.ID = LR.ApprovedBy_AdminID
                    WHERE LR.Admin_ID = @AdminID" + yearFilter + @"
                    ORDER BY LR.CreatedDate DESC";
                cmd.Parameters.AddWithValue("@AdminID", employeeAdminId);
                if (year.HasValue)
                    cmd.Parameters.AddWithValue("@Year", year.Value);

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
    /// Get all approved leave requests for work replacement management
    /// For Admin/Owner: returns all approved leaves
    /// For Supervisor: returns only their subordinates' leaves
    /// </summary>
    public DataTable GetLeavesForWorkReplacement(short approverAdminId, short? year = null, string employeeSearch = null)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                // Check approver role
                string approverRole = "";
                using (SqlCommand roleCmd = new SqlCommand("SELECT Role FROM Admin WHERE ID = @ApproverID", conn))
                {
                    roleCmd.Parameters.AddWithValue("@ApproverID", approverAdminId);
                    conn.Open();
                    approverRole = roleCmd.ExecuteScalar()?.ToString() ?? "";
                    conn.Close();
                }

                bool isAdminOrOwner = approverRole == "Admin" || approverRole == "Owner";

                var whereClauses = new System.Collections.Generic.List<string>();
                whereClauses.Add("LR.Status = 'APPROVED'");

                if (!isAdminOrOwner)
                {
                    // Supervisor - only show subordinates' leaves
                    whereClauses.Add(@"EXISTS (SELECT 1 FROM Employee_Supervisor ES
                                       WHERE ES.Supervisor_AdminID = @ApproverID
                                       AND ES.Employee_AdminID = LR.Admin_ID
                                       AND ES.IsActive = 1)");
                }

                if (year.HasValue)
                {
                    whereClauses.Add("YEAR(LR.StartDate) = @Year");
                    cmd.Parameters.AddWithValue("@Year", year.Value);
                }

                if (!string.IsNullOrEmpty(employeeSearch))
                {
                    whereClauses.Add("(A.FirstName + ' ' + A.LastName LIKE @Search OR A.Username LIKE @Search)");
                    cmd.Parameters.AddWithValue("@Search", "%" + employeeSearch + "%");
                }

                string whereClause = "WHERE " + string.Join(" AND ", whereClauses);

                cmd.CommandText = @"
                    SELECT LR.ID, LR.RequestNumber, LR.Admin_ID,
                           ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                           A.Username AS NickName,
                           LT.LeaveTypeName, LT.LeaveTypeCode,
                           LR.StartDate, LR.EndDate, LR.TotalDays,
                           LR.Reason, LR.Status,
                           ISNULL(LR.IsReplaced, 0) AS IsReplaced,
                           LR.ReplacementDate,
                           ISNULL(ReplacedBy.FirstName + ' ' + ReplacedBy.LastName, ReplacedBy.Username) AS ReplacedByName,
                           LR.ReplacementApprovedDate,
                           ISNULL(ApprovedBy.FirstName + ' ' + ApprovedBy.LastName, ApprovedBy.Username) AS ApprovedByName,
                           LR.ApprovedDate
                    FROM Leave_Requests LR
                    INNER JOIN Admin A ON A.ID = LR.Admin_ID
                    INNER JOIN Leave_Types LT ON LT.ID = LR.LeaveType_ID
                    LEFT JOIN Admin ApprovedBy ON ApprovedBy.ID = LR.ApprovedBy_AdminID
                    LEFT JOIN Admin ReplacedBy ON ReplacedBy.ID = LR.ReplacementApprovedBy
                    " + whereClause + @"
                    ORDER BY LR.StartDate DESC";

                cmd.Parameters.AddWithValue("@ApproverID", approverAdminId);

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
