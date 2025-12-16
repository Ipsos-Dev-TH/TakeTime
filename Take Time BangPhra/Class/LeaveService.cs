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
        connectionString = ConfigurationManager.ConnectionStrings["TakeTime_DB"].ConnectionString;
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
            using (SqlCommand cmd = new SqlCommand("sp_Initialize_Leave_Quota_For_Year", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Year", year);
                cmd.Parameters.AddWithValue("@AdminID", adminId ?? (object)DBNull.Value);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string result = reader["Result"].ToString();
                        string message = reader["Message"].ToString();
                        int recordsCreated = result == "Success" ? Convert.ToInt32(reader["RecordsCreated"]) : 0;
                        return new LeaveOperationResult(result == "Success", message, recordsCreated);
                    }
                }
            }
        }
        return new LeaveOperationResult(false, "Unknown error", 0);
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
                            cmd.Parameters.Clear();
                            cmd.CommandText = "sp_Calculate_Leave_Deduction";
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@AdminID", adminId);
                            cmd.Parameters.AddWithValue("@StartDate", startDate);
                            cmd.Parameters.AddWithValue("@EndDate", endDate);
                            cmd.Parameters.AddWithValue("@TotalDays", totalDays);

                            SqlParameter deductionParam = new SqlParameter("@DeductionAmount", SqlDbType.Decimal)
                            {
                                Precision = 10,
                                Scale = 2,
                                Direction = ParameterDirection.Output
                            };
                            cmd.Parameters.Add(deductionParam);

                            cmd.ExecuteNonQuery();
                            deductionAmount = deductionParam.Value != DBNull.Value ? Convert.ToDecimal(deductionParam.Value) : 0;
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
                        A.Name AS EmployeeName, A.NickName,
                        LT.LeaveTypeName, LT.LeaveTypeCode,
                        LR.StartDate, LR.EndDate, LR.TotalDays,
                        LR.Reason, LR.Status, LR.DeductSalary, LR.DeductionAmount,
                        LR.MedicalCertPath, LR.CreatedDate AS SubmittedDate,
                        LR.ApprovedBy_AdminID, LR.ApprovedDate,
                        LR.RejectedReason,
                        ApprovedBy.Name AS ApprovedByName
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
                            SELECT Admin_ID, LeaveType_ID, TotalDays, YEAR(StartDate) AS Year
                            FROM Leave_Requests
                            WHERE ID = @RequestID";
                        cmd.Parameters.AddWithValue("@RequestID", requestId);

                        short adminId = 0;
                        byte leaveTypeId = 0;
                        decimal totalDays = 0;
                        short year = 0;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                adminId = Convert.ToInt16(reader["Admin_ID"]);
                                leaveTypeId = Convert.ToByte(reader["LeaveType_ID"]);
                                totalDays = Convert.ToDecimal(reader["TotalDays"]);
                                year = Convert.ToInt16(reader["Year"]);
                            }
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

                        // Update leave quota
                        cmd.Parameters.Clear();
                        cmd.CommandText = @"
                            UPDATE Employee_Leave_Quota
                            SET UsedDays = UsedDays + @TotalDays
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
            using (SqlCommand cmd = new SqlCommand("sp_Calculate_Leave_Deduction", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@AdminID", adminId);
                cmd.Parameters.AddWithValue("@StartDate", startDate);
                cmd.Parameters.AddWithValue("@EndDate", endDate);
                cmd.Parameters.AddWithValue("@TotalDays", totalDays);

                SqlParameter deductionParam = new SqlParameter("@DeductionAmount", SqlDbType.Decimal)
                {
                    Precision = 10,
                    Scale = 2,
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(deductionParam);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        decimal deduction = Convert.ToDecimal(reader["DeductionAmount"]);
                        decimal baseSalary = Convert.ToDecimal(reader["BaseSalary"]);
                        int daysInMonth = Convert.ToInt32(reader["DaysInMonth"]);
                        return (deduction, baseSalary, daysInMonth, "Success");
                    }
                }
            }
        }
        return (0, 0, 0, "Error");
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
}
