using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using Take_Time_BangPhra.Class;

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
    private readonly DocumentHelper documentHelper;

    public PayrollService()
    {
        connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        documentHelper = new DocumentHelper(connectionString);
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

                            // Get days in month for calculations
                            int daysInMonth = DateTime.DaysInMonth(year, month);

                            foreach (var emp in employees)
                            {
                                decimal baseSalary = emp.Salary;

                                // Get leave days for this employee and period (exclude replaced leaves)
                                decimal leaveDays = 0;
                                using (SqlCommand leaveCmd = new SqlCommand(@"
                                    SELECT ISNULL(SUM(
                                        CASE WHEN ISNULL(IsReplaced, 0) = 1 THEN 0 ELSE TotalDays END
                                    ), 0) AS TotalLeaveDays
                                    FROM Leave_Requests
                                    WHERE Admin_ID = @AdminID
                                      AND Status = 'APPROVED'
                                      AND YEAR(StartDate) = @Year
                                      AND MONTH(StartDate) = @Month", conn, transaction))
                                {
                                    leaveCmd.Parameters.AddWithValue("@AdminID", emp.Id);
                                    leaveCmd.Parameters.AddWithValue("@Year", year);
                                    leaveCmd.Parameters.AddWithValue("@Month", month);
                                    object result = leaveCmd.ExecuteScalar();
                                    leaveDays = result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                                }

                                // Get OT hours for this employee and period
                                decimal otHours = 0;
                                using (SqlCommand otCmd = new SqlCommand(@"
                                    SELECT ISNULL(SUM(OTHours), 0) AS TotalOTHours
                                    FROM OT_Entry
                                    WHERE Admin_ID = @AdminID
                                      AND Status = 'APPROVED'
                                      AND YEAR(OTDate) = @Year
                                      AND MONTH(OTDate) = @Month", conn, transaction))
                                {
                                    otCmd.Parameters.AddWithValue("@AdminID", emp.Id);
                                    otCmd.Parameters.AddWithValue("@Year", year);
                                    otCmd.Parameters.AddWithValue("@Month", month);
                                    object result = otCmd.ExecuteScalar();
                                    otHours = result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                                }

                                // Calculate work days
                                int workDays = daysInMonth - (int)Math.Floor(leaveDays);
                                if (workDays < 0) workDays = 0;

                                // Calculate leave deduction and OT amount using centralized config
                                decimal leaveDeduction = HRConfiguration.CalculateLeaveDeduction(baseSalary, leaveDays, daysInMonth);
                                decimal otAmount = HRConfiguration.CalculateBasicOTAmount(baseSalary, otHours, daysInMonth);

                                // Calculate social security using centralized configuration
                                decimal socialSecurity = HRConfiguration.CalculateSocialSecurity(baseSalary);

                                // Calculate totals
                                decimal totalEarnings = baseSalary + otAmount;
                                decimal totalDeductionsForEmp = leaveDeduction + socialSecurity;
                                decimal netSalary = totalEarnings - totalDeductionsForEmp;

                                using (SqlCommand recCmd = new SqlCommand(@"
                                    INSERT INTO Payroll_Records
                                    (PayrollPeriod_ID, Admin_ID, EmployeeName, BaseSalary, WorkDays, LeaveDays,
                                     OTHours, OTAmount, BonusAmount, AllowanceAmount, TotalEarnings,
                                     LeaveDeduction, SocialSecurity, Tax, OtherDeductions, TotalDeductions, NetSalary,
                                     VoucherGenerated, CreatedDate)
                                    VALUES
                                    (@PeriodID, @AdminID, @Name, @Salary, @WorkDays, @LeaveDays,
                                     @OTHours, @OTAmount, 0, 0, @TotalEarnings,
                                     @LeaveDeduction, @SS, 0, 0, @TotalDeductions, @Net,
                                     0, GETDATE())", conn, transaction))
                                {
                                    recCmd.Parameters.AddWithValue("@PeriodID", periodId);
                                    recCmd.Parameters.AddWithValue("@AdminID", emp.Id);
                                    recCmd.Parameters.AddWithValue("@Name", emp.Name);
                                    recCmd.Parameters.AddWithValue("@Salary", baseSalary);
                                    recCmd.Parameters.AddWithValue("@WorkDays", workDays);
                                    recCmd.Parameters.AddWithValue("@LeaveDays", leaveDays);
                                    recCmd.Parameters.AddWithValue("@OTHours", otHours);
                                    recCmd.Parameters.AddWithValue("@OTAmount", otAmount);
                                    recCmd.Parameters.AddWithValue("@TotalEarnings", totalEarnings);
                                    recCmd.Parameters.AddWithValue("@LeaveDeduction", leaveDeduction);
                                    recCmd.Parameters.AddWithValue("@SS", socialSecurity);
                                    recCmd.Parameters.AddWithValue("@TotalDeductions", totalDeductionsForEmp);
                                    recCmd.Parameters.AddWithValue("@Net", netSalary);
                                    recCmd.ExecuteNonQuery();
                                }

                                totalGross += totalEarnings;
                                totalDeductions += totalDeductionsForEmp;
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
    /// Uses Employee_Salary as fallback when Payroll_Records.BaseSalary is 0
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
                        PR.ID, PR.Admin_ID, PR.EmployeeName,
                        CASE WHEN ISNULL(PR.BaseSalary, 0) = 0 THEN ISNULL(ES.MonthlySalary, 0) ELSE PR.BaseSalary END AS BaseSalary,
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
                        PR.VoucherNumber,
                        A.Username AS NickName, ES.Position,
                        ES.MonthlySalary AS CurrentSalary
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
    /// Uses Employee_Salary as fallback when Payroll_Records.BaseSalary is 0
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
                        CASE WHEN ISNULL(PR.BaseSalary, 0) = 0 THEN ISNULL(ES.MonthlySalary, 0) ELSE PR.BaseSalary END AS BaseSalary,
                        ISNULL(PR.WorkDays, 0) AS WorkDays,
                        ISNULL(PR.LeaveDays, 0) AS LeaveDays,
                        ISNULL(PR.OTHours, 0) AS OTHours,
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
                        PR.VoucherNumber,
                        PP.Year, PP.Month, PP.PeriodName,
                        A.Username AS NickName, ES.Position,
                        ES.MonthlySalary AS CurrentSalary
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
    /// Mark voucher as generated for payroll record and store voucher number
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
                        VoucherNumber = @VoucherNumber
                    WHERE ID = @RecordID";
                cmd.Parameters.AddWithValue("@RecordID", payrollRecordId);
                cmd.Parameters.AddWithValue("@VoucherNumber", voucherNumber ?? (object)DBNull.Value);

                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    /// <summary>
    /// Generate payment voucher for a payroll record
    /// Creates a proper payment voucher number using the same running number as Account_Payment
    /// Always creates an Account_Payment record so it appears in the payment voucher management
    /// </summary>
    public (bool Success, string VoucherNumber, string Message) GeneratePayrollVoucher(
        long payrollRecordId, short createdByAdminId, bool createAccountPayment = true)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();
            using (SqlTransaction transaction = conn.BeginTransaction())
            {
                try
                {
                    // Get payroll record details
                    DataRow payrollRecord = null;
                    using (SqlCommand getCmd = new SqlCommand(@"
                        SELECT PR.*, PP.Year, PP.Month, PP.PeriodName,
                               A.Username AS NickName,
                               ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS EmployeeName,
                               -- Calculate amounts with fallback to Employee_Salary
                               CASE WHEN ISNULL(PR.BaseSalary, 0) = 0
                                    THEN ISNULL(ES.MonthlySalary, 0)
                                    ELSE PR.BaseSalary
                               END AS CalcBaseSalary,
                               ISNULL(PR.OTAmount, 0) AS CalcOTAmount,
                               ISNULL(PR.BonusAmount, 0) AS CalcBonusAmount,
                               ISNULL(PR.AllowanceAmount, 0) AS CalcAllowanceAmount,
                               ISNULL(PR.SocialSecurity, 0) AS CalcSocialSecurity,
                               ISNULL(PR.Tax, 0) AS CalcTax,
                               ISNULL(PR.LeaveDeduction, 0) AS CalcLeaveDeduction,
                               ISNULL(PR.OtherDeductions, 0) AS CalcOtherDeductions
                        FROM Payroll_Records PR
                        INNER JOIN Payroll_Periods PP ON PP.ID = PR.PayrollPeriod_ID
                        INNER JOIN Admin A ON A.ID = PR.Admin_ID
                        LEFT JOIN Employee_Salary ES ON ES.Admin_ID = PR.Admin_ID AND ES.IsActive = 1
                        WHERE PR.ID = @RecordID", conn, transaction))
                    {
                        getCmd.Parameters.AddWithValue("@RecordID", payrollRecordId);
                        using (SqlDataAdapter adapter = new SqlDataAdapter(getCmd))
                        {
                            DataTable dt = new DataTable();
                            adapter.Fill(dt);
                            if (dt.Rows.Count == 0)
                            {
                                return (false, null, "ไม่พบข้อมูล Payroll Record");
                            }
                            payrollRecord = dt.Rows[0];
                        }
                    }

                    // Check if already generated
                    bool alreadyGenerated = payrollRecord["VoucherGenerated"] != DBNull.Value
                        && Convert.ToBoolean(payrollRecord["VoucherGenerated"]);
                    if (alreadyGenerated)
                    {
                        string existingVoucher = payrollRecord["VoucherNumber"]?.ToString() ?? "";
                        return (true, existingVoucher, "ใบสำคัญจ่ายถูกสร้างแล้ว");
                    }

                    // Generate voucher number using DocumentHelper - same running number as Account_Payment
                    // Format: PAYYYMMDDNNN (e.g., PAY2512170001)
                    string voucherNumber = documentHelper.CreateDocumentNumber("Account_Payment", "PAY", DateTime.Now);

                    // Update payroll record with voucher info
                    using (SqlCommand updateCmd = new SqlCommand(@"
                        UPDATE Payroll_Records
                        SET VoucherGenerated = 1,
                            VoucherNumber = @VoucherNumber,
                            VoucherGeneratedDate = GETDATE(),
                            VoucherGeneratedBy = @CreatedBy
                        WHERE ID = @RecordID", conn, transaction))
                    {
                        updateCmd.Parameters.AddWithValue("@RecordID", payrollRecordId);
                        updateCmd.Parameters.AddWithValue("@VoucherNumber", voucherNumber);
                        updateCmd.Parameters.AddWithValue("@CreatedBy", createdByAdminId);
                        updateCmd.ExecuteNonQuery();
                    }

                    // Always create Account_Payment record so it appears in payment voucher management
                    // Calculate NetSalary from individual components (don't rely on stored value which may be 0)
                    decimal baseSalary = payrollRecord["CalcBaseSalary"] != DBNull.Value ? Convert.ToDecimal(payrollRecord["CalcBaseSalary"]) : 0;
                    decimal otAmount = payrollRecord["CalcOTAmount"] != DBNull.Value ? Convert.ToDecimal(payrollRecord["CalcOTAmount"]) : 0;
                    decimal bonus = payrollRecord["CalcBonusAmount"] != DBNull.Value ? Convert.ToDecimal(payrollRecord["CalcBonusAmount"]) : 0;
                    decimal allowance = payrollRecord["CalcAllowanceAmount"] != DBNull.Value ? Convert.ToDecimal(payrollRecord["CalcAllowanceAmount"]) : 0;
                    decimal socialSecurity = payrollRecord["CalcSocialSecurity"] != DBNull.Value ? Convert.ToDecimal(payrollRecord["CalcSocialSecurity"]) : 0;
                    decimal tax = payrollRecord["CalcTax"] != DBNull.Value ? Convert.ToDecimal(payrollRecord["CalcTax"]) : 0;
                    decimal leaveDeduction = payrollRecord["CalcLeaveDeduction"] != DBNull.Value ? Convert.ToDecimal(payrollRecord["CalcLeaveDeduction"]) : 0;
                    decimal otherDeductions = payrollRecord["CalcOtherDeductions"] != DBNull.Value ? Convert.ToDecimal(payrollRecord["CalcOtherDeductions"]) : 0;

                    decimal totalEarnings = baseSalary + otAmount + bonus + allowance;
                    decimal totalDeductions = socialSecurity + tax + leaveDeduction + otherDeductions;
                    decimal netSalary = totalEarnings - totalDeductions;

                    string employeeName = payrollRecord["EmployeeName"].ToString();
                    int periodYear = Convert.ToInt32(payrollRecord["Year"]);
                    int periodMonth = Convert.ToInt32(payrollRecord["Month"]);
                    string periodName = payrollRecord["PeriodName"].ToString();

                    // Get or create payroll vendor ID
                    int payrollVendorId = GetOrCreatePayrollVendorId(conn, transaction);

                    // Insert into Account_Payment for tracking in payment voucher management
                    using (SqlCommand paymentCmd = new SqlCommand(@"
                        INSERT INTO Account_Payment
                        (ID, Vendor_ID, Created_Date, Total_Amount, Vat_Type_ID, Vat,
                         Total_Amount_Exclude_Vat, Paid_How, Paid_Type, Status, Created_By_ID, Notes)
                        VALUES
                        (@VoucherNumber, @VendorID, GETDATE(), @Amount, 1, 0,
                         @Amount, N'โอน', N'เงินเดือน', N'Normal', @CreatedBy, @Notes)", conn, transaction))
                    {
                        paymentCmd.Parameters.AddWithValue("@VoucherNumber", voucherNumber);
                        paymentCmd.Parameters.AddWithValue("@VendorID", payrollVendorId);
                        paymentCmd.Parameters.AddWithValue("@Amount", netSalary);
                        paymentCmd.Parameters.AddWithValue("@CreatedBy", createdByAdminId);
                        paymentCmd.Parameters.AddWithValue("@Notes", $"เงินเดือน {employeeName} - {periodName}");
                        paymentCmd.ExecuteNonQuery();
                    }

                    // Insert payment detail
                    using (SqlCommand detailCmd = new SqlCommand(@"
                        INSERT INTO Account_Payment_Detail (Payment_ID, Number, Detail, Amount)
                        VALUES (@VoucherNumber, 1, @Detail, @Amount)", conn, transaction))
                    {
                        detailCmd.Parameters.AddWithValue("@VoucherNumber", voucherNumber);
                        detailCmd.Parameters.AddWithValue("@Detail", $"จ่ายเงินเดือน {periodName} - {employeeName}");
                        detailCmd.Parameters.AddWithValue("@Amount", netSalary);
                        detailCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    return (true, voucherNumber, $"สร้างใบสำคัญจ่ายสำเร็จ: {voucherNumber}");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return (false, null, "เกิดข้อผิดพลาด: " + ex.Message);
                }
            }
        }
    }

    /// <summary>
    /// Generate payment vouchers for all records in a payroll period
    /// Always creates Account_Payment records so they appear in payment voucher management
    /// </summary>
    public (int SuccessCount, int FailCount, string Message) GenerateAllVouchersForPeriod(
        int payrollPeriodId, short createdByAdminId, bool createAccountPayment = true)
    {
        int successCount = 0;
        int failCount = 0;

        DataTable records = GetPayrollRecords(payrollPeriodId);

        foreach (DataRow row in records.Rows)
        {
            bool alreadyGenerated = row["VoucherGenerated"] != DBNull.Value
                && Convert.ToBoolean(row["VoucherGenerated"]);

            if (!alreadyGenerated)
            {
                long recordId = Convert.ToInt64(row["ID"]);
                var result = GeneratePayrollVoucher(recordId, createdByAdminId, createAccountPayment);

                if (result.Success)
                    successCount++;
                else
                    failCount++;
            }
        }

        return (successCount, failCount, $"สร้างใบสำคัญจ่ายสำเร็จ {successCount} รายการ" +
            (failCount > 0 ? $", ล้มเหลว {failCount} รายการ" : ""));
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
                // Use 'APPROVED' status for consistency with summary queries and reports
                cmd.CommandText = @"
                    UPDATE Payroll_Periods
                    SET Status = 'APPROVED',
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

    #region Helper Methods

    /// <summary>
    /// Get or create payroll vendor ID for Account_Payment entries
    /// </summary>
    private int GetOrCreatePayrollVendorId(SqlConnection conn, SqlTransaction transaction)
    {
        const string payrollVendorName = "เงินเดือนพนักงาน";

        // Try to find existing payroll vendor
        using (SqlCommand findCmd = new SqlCommand(
            "SELECT ID FROM Vendor WHERE Name = @Name", conn, transaction))
        {
            findCmd.Parameters.AddWithValue("@Name", payrollVendorName);
            object result = findCmd.ExecuteScalar();
            if (result != null && result != DBNull.Value)
            {
                return Convert.ToInt32(result);
            }
        }

        // Create payroll vendor if not exists (ID is IDENTITY column, auto-generated)
        using (SqlCommand createCmd = new SqlCommand(@"
            INSERT INTO Vendor (IDNumber, Vendor_Type_ID, Name, Address, Status)
            VALUES (N'0000000000000', 1, @Name, N'Internal - Payroll', N'True');
            SELECT SCOPE_IDENTITY();", conn, transaction))
        {
            createCmd.Parameters.AddWithValue("@Name", payrollVendorName);
            return Convert.ToInt32(createCmd.ExecuteScalar());
        }
    }

    #endregion
}
