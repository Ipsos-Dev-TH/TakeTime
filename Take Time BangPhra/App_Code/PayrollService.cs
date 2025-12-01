using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

/// <summary>
/// Payroll Service - Business logic for Payroll Management
/// Handles salary management, OT calculations, and payroll processing
/// </summary>
public class PayrollService
{
    private readonly string connectionString;

    public PayrollService()
    {
        connectionString = ConfigurationManager.ConnectionStrings["TakeTime_DB"].ConnectionString;
    }

    #region Salary Management

    /// <summary>
    /// Get current salary for an employee
    /// </summary>
    public DataTable GetEmployeeSalary(short adminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        Admin_ID, MonthlySalary, Position, EffectiveDate,
                        IsActive, CreatedDate
                    FROM Employee_Salary
                    WHERE Admin_ID = @AdminID
                    ORDER BY EffectiveDate DESC";
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
    /// Set employee salary
    /// </summary>
    public bool SetEmployeeSalary(short adminId, decimal monthlySalary, string position, DateTime effectiveDate)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                // Deactivate previous salary records
                cmd.CommandText = @"
                    UPDATE Employee_Salary
                    SET IsActive = 0
                    WHERE Admin_ID = @AdminID AND IsActive = 1";
                cmd.Parameters.AddWithValue("@AdminID", adminId);

                conn.Open();
                cmd.ExecuteNonQuery();

                // Insert new salary record
                cmd.CommandText = @"
                    INSERT INTO Employee_Salary (Admin_ID, MonthlySalary, Position, EffectiveDate, IsActive)
                    VALUES (@AdminID, @Salary, @Position, @EffectiveDate, 1)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@AdminID", adminId);
                cmd.Parameters.AddWithValue("@Salary", monthlySalary);
                cmd.Parameters.AddWithValue("@Position", position ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@EffectiveDate", effectiveDate);

                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    /// <summary>
    /// Get current payroll summary for all employees
    /// </summary>
    public DataTable GetCurrentPayrollSummary()
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = "SELECT * FROM vw_Current_Payroll_Summary ORDER BY EmployeeName";

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

    #region OT Calculation

    /// <summary>
    /// Calculate OT amount for an employee
    /// </summary>
    public (decimal OTRate, decimal OTAmount, string Message) CalculateOTAmount(
        short adminId, decimal otHours, DateTime calculationMonth)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand("sp_Calculate_OT_Amount", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@AdminID", adminId);
                cmd.Parameters.AddWithValue("@OTHours", otHours);
                cmd.Parameters.AddWithValue("@CalculationMonth", calculationMonth);

                SqlParameter otRateParam = new SqlParameter("@OTRate", SqlDbType.Decimal)
                {
                    Precision = 10,
                    Scale = 2,
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(otRateParam);

                SqlParameter otAmountParam = new SqlParameter("@OTAmount", SqlDbType.Decimal)
                {
                    Precision = 10,
                    Scale = 2,
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(otAmountParam);

                conn.Open();
                cmd.ExecuteNonQuery();

                decimal otRate = otRateParam.Value != DBNull.Value ? Convert.ToDecimal(otRateParam.Value) : 0;
                decimal otAmount = otAmountParam.Value != DBNull.Value ? Convert.ToDecimal(otAmountParam.Value) : 0;

                return (otRate, otAmount, "Success");
            }
        }
    }

    /// <summary>
    /// Add OT record for payroll
    /// </summary>
    public bool AddOTRecord(long payrollRecordId, DateTime otDate, decimal otHours, decimal otRate, decimal otAmount, short createdByAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    INSERT INTO Payroll_OT_Details
                        (PayrollRecord_ID, OTDate, OTHours, OTRate, OTAmount, CreatedBy_AdminID, CreatedDate)
                    VALUES
                        (@PayrollRecordID, @OTDate, @OTHours, @OTRate, @OTAmount, @CreatedBy, GETDATE())";

                cmd.Parameters.AddWithValue("@PayrollRecordID", payrollRecordId);
                cmd.Parameters.AddWithValue("@OTDate", otDate);
                cmd.Parameters.AddWithValue("@OTHours", otHours);
                cmd.Parameters.AddWithValue("@OTRate", otRate);
                cmd.Parameters.AddWithValue("@OTAmount", otAmount);
                cmd.Parameters.AddWithValue("@CreatedBy", createdByAdminId);

                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    /// <summary>
    /// Get OT details for a payroll record
    /// </summary>
    public DataTable GetOTDetails(long payrollRecordId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ID, OTDate, OTHours, OTRate, OTAmount,
                        Description, CreatedDate
                    FROM Payroll_OT_Details
                    WHERE PayrollRecord_ID = @PayrollRecordID
                    ORDER BY OTDate DESC";
                cmd.Parameters.AddWithValue("@PayrollRecordID", payrollRecordId);

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

    #region Payroll Period Management

    /// <summary>
    /// Generate payroll for a period
    /// </summary>
    public (bool Success, string Message, int PayrollPeriodID) GeneratePayrollForPeriod(
        short year, byte month, short createdByAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand("sp_Generate_Payroll_For_Period", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Year", year);
                cmd.Parameters.AddWithValue("@Month", month);
                cmd.Parameters.AddWithValue("@CreatedBy_AdminID", createdByAdminId);

                SqlParameter outputParam = new SqlParameter("@PayrollPeriodID", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(outputParam);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string result = reader["Result"].ToString();
                        string message = reader["Message"].ToString();
                        int periodId = result == "Success" ? Convert.ToInt32(reader["PayrollPeriodID"]) : 0;
                        return (result == "Success", message, periodId);
                    }
                }
            }
        }
        return (false, "Unknown error", 0);
    }

    /// <summary>
    /// Get payroll periods
    /// </summary>
    public DataTable GetPayrollPeriods(short? year = null)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ID, PeriodCode, Year, Month, PeriodName, Status,
                        TotalEmployees, TotalGrossPay, TotalDeductions, TotalNetPay,
                        CreatedDate, ApprovedDate
                    FROM Payroll_Periods" +
                    (year.HasValue ? " WHERE Year = @Year" : "") + @"
                    ORDER BY Year DESC, Month DESC";

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
    /// Get payroll records for a period
    /// </summary>
    public DataTable GetPayrollRecords(int payrollPeriodId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        PR.ID, PR.Admin_ID, PR.EmployeeName, PR.BaseSalary,
                        PR.WorkDays, PR.LeaveDays, PR.OTHours, PR.OTAmount,
                        PR.Bonus, PR.Allowances, PR.TotalEarnings,
                        PR.LeaveDeduction, PR.SocialSecurity, PR.Tax,
                        PR.OtherDeductions, PR.TotalDeductions, PR.NetSalary,
                        PR.VoucherGenerated, PR.VoucherNumber,
                        A.NickName, ES.Position
                    FROM Payroll_Records PR
                    INNER JOIN Admin A ON A.ID = PR.Admin_ID
                    LEFT JOIN Employee_Salary ES ON ES.Admin_ID = PR.Admin_ID AND ES.IsActive = 1
                    WHERE PR.PayrollPeriod_ID = @PeriodID
                    ORDER BY PR.EmployeeName";
                cmd.Parameters.AddWithValue("@PeriodID", payrollPeriodId);

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
    /// Get payroll record by ID
    /// </summary>
    public DataTable GetPayrollRecord(long payrollRecordId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        PR.*,
                        PP.Year, PP.Month, PP.PeriodName,
                        A.Name, A.NickName,
                        ES.Position
                    FROM Payroll_Records PR
                    INNER JOIN Payroll_Periods PP ON PP.ID = PR.PayrollPeriod_ID
                    INNER JOIN Admin A ON A.ID = PR.Admin_ID
                    LEFT JOIN Employee_Salary ES ON ES.Admin_ID = PR.Admin_ID AND ES.IsActive = 1
                    WHERE PR.ID = @RecordID";
                cmd.Parameters.AddWithValue("@RecordID", payrollRecordId);

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
    /// Update payroll record
    /// </summary>
    public bool UpdatePayrollRecord(long payrollRecordId, Dictionary<string, object> updateFields)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                var fields = new List<string>();
                foreach (var field in updateFields)
                {
                    fields.Add($"[{field.Key}] = @{field.Key}");
                    cmd.Parameters.AddWithValue($"@{field.Key}", field.Value ?? DBNull.Value);
                }

                cmd.CommandText = $@"
                    UPDATE Payroll_Records
                    SET {string.Join(", ", fields)},
                        UpdatedDate = GETDATE()
                    WHERE ID = @RecordID";
                cmd.Parameters.AddWithValue("@RecordID", payrollRecordId);

                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    /// <summary>
    /// Mark voucher as generated for payroll record
    /// </summary>
    public bool MarkVoucherGenerated(long payrollRecordId, string voucherNumber)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    UPDATE Payroll_Records
                    SET VoucherGenerated = 1,
                        VoucherNumber = @VoucherNumber,
                        UpdatedDate = GETDATE()
                    WHERE ID = @RecordID";
                cmd.Parameters.AddWithValue("@RecordID", payrollRecordId);
                cmd.Parameters.AddWithValue("@VoucherNumber", voucherNumber);

                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    /// <summary>
    /// Approve payroll period
    /// </summary>
    public bool ApprovePayrollPeriod(int payrollPeriodId, short approvedByAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    UPDATE Payroll_Periods
                    SET Status = 'APPROVED',
                        ApprovedBy_AdminID = @ApprovedBy,
                        ApprovedDate = GETDATE()
                    WHERE ID = @PeriodID";
                cmd.Parameters.AddWithValue("@PeriodID", payrollPeriodId);
                cmd.Parameters.AddWithValue("@ApprovedBy", approvedByAdminId);

                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    #endregion

    #region Statistics & Reports

    /// <summary>
    /// Get payroll statistics for dashboard
    /// </summary>
    public DataTable GetPayrollStatistics(short? year = null)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        ISNULL(COUNT(DISTINCT PP.ID), 0) AS TotalPeriods,
                        ISNULL(SUM(PP.TotalEmployees), 0) AS TotalEmployeePayments,
                        ISNULL(SUM(PP.TotalGrossPay), 0) AS TotalGrossPay,
                        ISNULL(SUM(PP.TotalDeductions), 0) AS TotalDeductions,
                        ISNULL(SUM(PP.TotalNetPay), 0) AS TotalNetPay,
                        ISNULL(SUM(CASE WHEN PP.Status = 'APPROVED' THEN 1 ELSE 0 END), 0) AS ApprovedPeriods,
                        ISNULL(SUM(CASE WHEN PP.Status = 'DRAFT' THEN 1 ELSE 0 END), 0) AS DraftPeriods
                    FROM Payroll_Periods PP" +
                    (year.HasValue ? " WHERE PP.Year = @Year" : "");

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
    /// Get monthly payroll comparison
    /// </summary>
    public DataTable GetMonthlyPayrollComparison(short year)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT
                        Month, PeriodName, TotalEmployees,
                        TotalGrossPay, TotalDeductions, TotalNetPay, Status
                    FROM Payroll_Periods
                    WHERE Year = @Year
                    ORDER BY Month";
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
