using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

/// <summary>
/// Result class for payroll operations
/// </summary>
public class PayrollOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int ID { get; set; }

    public PayrollOperationResult(bool success, string message, int id)
    {
        Success = success;
        Message = message;
        ID = id;
    }
}

/// <summary>
/// Payroll Service - Business logic for Payroll Management
/// Handles salary management, OT calculations, and payroll processing
/// </summary>
public class PayrollService
{
    private readonly string connectionString;

    public PayrollService()
    {
        connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
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
            // Get employee's monthly salary to calculate OT rate
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
                decimal monthlySalary = result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0;

                // Calculate OT rate (1.5x hourly rate)
                // Hourly rate = Monthly salary / (30 days * 8 hours)
                decimal hourlyRate = monthlySalary / 240m;
                decimal otRate = hourlyRate * 1.5m;
                decimal otAmount = otRate * otHours;

                return (Math.Round(otRate, 2), Math.Round(otAmount, 2), "Success");
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
                        (PayrollRecord_ID, OTDate, OTHours, OTRate, OTAmount, RecordedBy_AdminID, RecordedDate)
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
    public PayrollOperationResult GeneratePayrollForPeriod(
        short year, byte month, short createdByAdminId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();
            using (SqlTransaction transaction = conn.BeginTransaction())
            {
                try
                {
                    // Check if period already exists
                    using (SqlCommand checkCmd = new SqlCommand(
                        "SELECT ID FROM Payroll_Periods WHERE Year = @Year AND Month = @Month", conn, transaction))
                    {
                        checkCmd.Parameters.AddWithValue("@Year", year);
                        checkCmd.Parameters.AddWithValue("@Month", month);
                        object existingId = checkCmd.ExecuteScalar();
                        if (existingId != null)
                        {
                            return new PayrollOperationResult(false, "รอบเงินเดือนนี้มีอยู่แล้ว", 0);
                        }
                    }

                    // Generate period code
                    string periodCode = $"PAY{year}{month:D2}";
                    string[] thaiMonths = { "", "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน",
                                           "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม" };
                    string periodName = $"{thaiMonths[month]} {year + 543}";

                    // Create payroll period
                    int periodId;
                    using (SqlCommand insertCmd = new SqlCommand(@"
                        INSERT INTO Payroll_Periods (PeriodCode, Year, Month, PeriodName, Status, CreatedBy_AdminID, CreatedDate)
                        VALUES (@Code, @Year, @Month, @Name, 'DRAFT', @CreatedBy, GETDATE());
                        SELECT SCOPE_IDENTITY();", conn, transaction))
                    {
                        insertCmd.Parameters.AddWithValue("@Code", periodCode);
                        insertCmd.Parameters.AddWithValue("@Year", year);
                        insertCmd.Parameters.AddWithValue("@Month", month);
                        insertCmd.Parameters.AddWithValue("@Name", periodName);
                        insertCmd.Parameters.AddWithValue("@CreatedBy", createdByAdminId);
                        periodId = Convert.ToInt32(insertCmd.ExecuteScalar());
                    }

                    // Get all active employees with salary and create payroll records
                    decimal totalGross = 0, totalDeductions = 0, totalNet = 0;
                    int employeeCount = 0;

                    using (SqlCommand empCmd = new SqlCommand(@"
                        SELECT A.ID, ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS Name,
                               ISNULL(ES.MonthlySalary, 0) AS Salary
                        FROM Admin A
                        LEFT JOIN Employee_Salary ES ON ES.Admin_ID = A.ID AND ES.IsActive = 1
                        WHERE A.Status = 1", conn, transaction))
                    {
                        using (SqlDataReader reader = empCmd.ExecuteReader())
                        {
                            var employees = new System.Collections.Generic.List<(int Id, string Name, decimal Salary)>();
                            while (reader.Read())
                            {
                                employees.Add((
                                    Convert.ToInt32(reader["ID"]),
                                    reader["Name"].ToString(),
                                    Convert.ToDecimal(reader["Salary"])
                                ));
                            }
                            reader.Close();

                            foreach (var emp in employees)
                            {
                                decimal baseSalary = emp.Salary;
                                // Calculate social security using centralized configuration
                                decimal socialSecurity = HRConfiguration.CalculateSocialSecurity(baseSalary);
                                decimal netSalary = baseSalary - socialSecurity;

                                using (SqlCommand recCmd = new SqlCommand(@"
                                    INSERT INTO Payroll_Records
                                    (PayrollPeriod_ID, Admin_ID, EmployeeName, BaseSalary, WorkDays, LeaveDays,
                                     OTHours, OTAmount, BonusAmount, AllowanceAmount, TotalEarnings,
                                     LeaveDeduction, SocialSecurity, Tax, OtherDeductions, TotalDeductions, NetSalary,
                                     VoucherGenerated, CreatedDate)
                                    VALUES
                                    (@PeriodID, @AdminID, @Name, @Salary, 30, 0,
                                     0, 0, 0, 0, @Salary,
                                     0, @SS, 0, 0, @SS, @Net,
                                     0, GETDATE())", conn, transaction))
                                {
                                    recCmd.Parameters.AddWithValue("@PeriodID", periodId);
                                    recCmd.Parameters.AddWithValue("@AdminID", emp.Id);
                                    recCmd.Parameters.AddWithValue("@Name", emp.Name);
                                    recCmd.Parameters.AddWithValue("@Salary", baseSalary);
                                    recCmd.Parameters.AddWithValue("@SS", socialSecurity);
                                    recCmd.Parameters.AddWithValue("@Net", netSalary);
                                    recCmd.ExecuteNonQuery();
                                }

                                totalGross += baseSalary;
                                totalDeductions += socialSecurity;
                                totalNet += netSalary;
                                employeeCount++;
                            }
                        }
                    }

                    // Update period totals
                    using (SqlCommand updateCmd = new SqlCommand(@"
                        UPDATE Payroll_Periods
                        SET TotalEmployees = @Count, TotalGrossPay = @Gross,
                            TotalDeductions = @Deductions, TotalNetPay = @Net
                        WHERE ID = @PeriodID", conn, transaction))
                    {
                        updateCmd.Parameters.AddWithValue("@PeriodID", periodId);
                        updateCmd.Parameters.AddWithValue("@Count", employeeCount);
                        updateCmd.Parameters.AddWithValue("@Gross", totalGross);
                        updateCmd.Parameters.AddWithValue("@Deductions", totalDeductions);
                        updateCmd.Parameters.AddWithValue("@Net", totalNet);
                        updateCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    return new PayrollOperationResult(true, $"สร้างรอบเงินเดือนสำเร็จ ({employeeCount} คน)", periodId);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return new PayrollOperationResult(false, "เกิดข้อผิดพลาด: " + ex.Message, 0);
                }
            }
        }
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
                        CreatedDate, ProcessedDate AS ApprovedDate
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
                        ISNULL(PR.BonusAmount, 0) AS BonusAmount,
                        ISNULL(PR.AllowanceAmount, 0) AS AllowanceAmount,
                        PR.TotalEarnings,
                        ISNULL(PR.LeaveDeduction, 0) AS LeaveDeduction,
                        ISNULL(PR.SocialSecurity, 0) AS SocialSecurity,
                        ISNULL(PR.Tax, 0) AS Tax,
                        ISNULL(PR.OtherDeductions, 0) AS OtherDeductions,
                        PR.TotalDeductions, PR.NetSalary,
                        ISNULL(PR.VoucherGenerated, 0) AS VoucherGenerated,
                        'PAY' + CAST(PR.ID AS VARCHAR(20)) AS VoucherNumber,
                        A.Username AS NickName, ES.Position
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
                        PR.ID, PR.PayrollPeriod_ID, PR.Admin_ID,
                        ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                        ISNULL(PR.BaseSalary, 0) AS BaseSalary,
                        ISNULL(PR.OTAmount, 0) AS OTAmount,
                        ISNULL(PR.BonusAmount, 0) AS BonusAmount,
                        ISNULL(PR.AllowanceAmount, 0) AS AllowanceAmount,
                        ISNULL(PR.SocialSecurity, 0) AS SocialSecurity,
                        ISNULL(PR.LeaveDeduction, 0) AS LeaveDeduction,
                        ISNULL(PR.Tax, 0) AS Tax,
                        ISNULL(PR.OtherDeductions, 0) AS OtherDeductions,
                        ISNULL(PR.TotalEarnings, 0) AS TotalEarnings,
                        ISNULL(PR.TotalDeductions, 0) AS TotalDeductions,
                        ISNULL(PR.NetSalary, 0) AS NetSalary,
                        ISNULL(PR.VoucherGenerated, 0) AS VoucherGenerated,
                        'PAY' + CAST(PR.ID AS VARCHAR(20)) AS VoucherNumber,
                        PP.Year, PP.Month, PP.PeriodName,
                        A.Username AS NickName, ES.Position
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
                    fields.Add("[" + field.Key + "] = @" + field.Key);
                    cmd.Parameters.AddWithValue("@" + field.Key, field.Value ?? DBNull.Value);
                }

                cmd.CommandText = @"
                    UPDATE Payroll_Records
                    SET " + string.Join(", ", fields) + @",
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
                    SET VoucherGenerated = 1
                    WHERE ID = @RecordID";
                cmd.Parameters.AddWithValue("@RecordID", payrollRecordId);

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
                    SET Status = 'COMPLETED',
                        ClosedBy_AdminID = @ApprovedBy,
                        ClosedDate = GETDATE()
                    WHERE ID = @PeriodID";
                cmd.Parameters.AddWithValue("@PeriodID", payrollPeriodId);
                cmd.Parameters.AddWithValue("@ApprovedBy", approvedByAdminId);

                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    /// <summary>
    /// Update period totals from records
    /// </summary>
    public bool UpdatePeriodTotals(int payrollPeriodId)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    UPDATE Payroll_Periods
                    SET TotalEmployees = (SELECT COUNT(*) FROM Payroll_Records WHERE PayrollPeriod_ID = @PeriodID),
                        TotalGrossPay = (SELECT ISNULL(SUM(TotalEarnings), 0) FROM Payroll_Records WHERE PayrollPeriod_ID = @PeriodID),
                        TotalDeductions = (SELECT ISNULL(SUM(TotalDeductions), 0) FROM Payroll_Records WHERE PayrollPeriod_ID = @PeriodID),
                        TotalNetPay = (SELECT ISNULL(SUM(NetSalary), 0) FROM Payroll_Records WHERE PayrollPeriod_ID = @PeriodID)
                    WHERE ID = @PeriodID";
                cmd.Parameters.AddWithValue("@PeriodID", payrollPeriodId);

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
