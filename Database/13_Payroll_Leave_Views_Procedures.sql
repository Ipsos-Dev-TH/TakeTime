-- ============================================================================
-- Payroll and Leave Management Views and Procedures
-- Version: 1.0
-- Date: 2025-01-30
-- ============================================================================

USE TakeTime;
GO

PRINT '============================================================================';
PRINT 'Creating Payroll and Leave Views and Stored Procedures';
PRINT '============================================================================';
GO

-- ============================================================================
-- VIEWS
-- ============================================================================

-- View: Employee Leave Summary
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Employee_Leave_Summary')
    DROP VIEW [dbo].[vw_Employee_Leave_Summary];
GO

CREATE VIEW [dbo].[vw_Employee_Leave_Summary]
AS
SELECT
    A.[ID] AS Admin_ID,
    A.[Name] AS EmployeeName,
    A.[User] AS UserType,
    ELQ.[Year],
    LT.[LeaveTypeName],
    LT.[LeaveTypeCode],
    ELQ.[TotalDays],
    ELQ.[UsedDays],
    ELQ.[RemainingDays],
    ELQ.[CarryForwardDays],
    -- สถิติการลา
    (SELECT COUNT(*)
     FROM [dbo].[Leave_Requests] LR
     WHERE LR.[Admin_ID] = A.[ID]
       AND YEAR(LR.[StartDate]) = ELQ.[Year]
       AND LR.[LeaveType_ID] = ELQ.[LeaveType_ID]
       AND LR.[Status] = 'APPROVED') AS ApprovedLeaveCount,
    (SELECT COUNT(*)
     FROM [dbo].[Leave_Requests] LR
     WHERE LR.[Admin_ID] = A.[ID]
       AND YEAR(LR.[StartDate]) = ELQ.[Year]
       AND LR.[LeaveType_ID] = ELQ.[LeaveType_ID]
       AND LR.[Status] = 'PENDING') AS PendingLeaveCount
FROM [dbo].[Employee_Leave_Quota] ELQ
INNER JOIN [dbo].[Admin] A ON A.[ID] = ELQ.[Admin_ID]
INNER JOIN [dbo].[Leave_Types] LT ON LT.[ID] = ELQ.[LeaveType_ID]
WHERE A.[Status] = 1;
GO

PRINT '✅ View vw_Employee_Leave_Summary created';
GO

-- View: Current Payroll Summary
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Current_Payroll_Summary')
    DROP VIEW [dbo].[vw_Current_Payroll_Summary];
GO

CREATE VIEW [dbo].[vw_Current_Payroll_Summary]
AS
SELECT
    A.[ID] AS Admin_ID,
    A.[Name] AS EmployeeName,
    A.[NickName],
    A.[User] AS UserType,
    ES.[MonthlySalary],
    ES.[Position],
    ES.[EffectiveDate],
    -- ข้อมูลเงินเดือนปัจจุบัน
    (SELECT TOP 1 PR.[NetSalary]
     FROM [dbo].[Payroll_Records] PR
     INNER JOIN [dbo].[Payroll_Periods] PP ON PP.[ID] = PR.[PayrollPeriod_ID]
     WHERE PR.[Admin_ID] = A.[ID]
     ORDER BY PP.[Year] DESC, PP.[Month] DESC) AS LastNetSalary,
    -- สถิติโอทีเดือนปัจจุบัน
    (SELECT ISNULL(SUM(PR.[OTHours]), 0)
     FROM [dbo].[Payroll_Records] PR
     INNER JOIN [dbo].[Payroll_Periods] PP ON PP.[ID] = PR.[PayrollPeriod_ID]
     WHERE PR.[Admin_ID] = A.[ID]
       AND PP.[Year] = YEAR(GETDATE())
       AND PP.[Month] = MONTH(GETDATE())) AS CurrentMonthOTHours,
    -- สถิติการลาปีนี้
    (SELECT ISNULL(SUM(LR.[TotalDays]), 0)
     FROM [dbo].[Leave_Requests] LR
     WHERE LR.[Admin_ID] = A.[ID]
       AND YEAR(LR.[StartDate]) = YEAR(GETDATE())
       AND LR.[Status] = 'APPROVED') AS YearToDateLeaveDays
FROM [dbo].[Admin] A
LEFT JOIN [dbo].[Employee_Salary] ES ON ES.[Admin_ID] = A.[ID] AND ES.[IsActive] = 1
WHERE A.[Status] = 1;
GO

PRINT '✅ View vw_Current_Payroll_Summary created';
GO

-- ============================================================================
-- STORED PROCEDURES - LEAVE MANAGEMENT
-- ============================================================================

-- SP: Initialize Leave Quota for New Year
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_Initialize_Leave_Quota_For_Year')
    DROP PROCEDURE [dbo].[sp_Initialize_Leave_Quota_For_Year];
GO

CREATE PROCEDURE [dbo].[sp_Initialize_Leave_Quota_For_Year]
    @Year SMALLINT,
    @AdminID SMALLINT = NULL -- NULL = ทุกคน, ระบุ = คนเดียว
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- ถ้าไม่ระบุ AdminID = สร้างให้ทุกคน
        INSERT INTO [dbo].[Employee_Leave_Quota] ([Admin_ID], [Year], [LeaveType_ID], [TotalDays], [UsedDays])
        SELECT
            A.[ID],
            @Year,
            LT.[ID],
            LT.[AnnualQuota],
            0
        FROM [dbo].[Admin] A
        CROSS JOIN [dbo].[Leave_Types] LT
        WHERE A.[Status] = 1
          AND LT.[IsActive] = 1
          AND (@AdminID IS NULL OR A.[ID] = @AdminID)
          AND NOT EXISTS (
              SELECT 1 FROM [dbo].[Employee_Leave_Quota] ELQ
              WHERE ELQ.[Admin_ID] = A.[ID]
                AND ELQ.[Year] = @Year
                AND ELQ.[LeaveType_ID] = LT.[ID]
          );

        COMMIT TRANSACTION;

        SELECT 'Success' AS Result,
               @@ROWCOUNT AS RecordsCreated,
               'Leave quota initialized for year ' + CAST(@Year AS VARCHAR(4)) AS Message;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SELECT 'Error' AS Result,
               ERROR_MESSAGE() AS Message;
    END CATCH
END;
GO

PRINT '✅ Procedure sp_Initialize_Leave_Quota_For_Year created';
GO

-- SP: Calculate Leave Deduction
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_Calculate_Leave_Deduction')
    DROP PROCEDURE [dbo].[sp_Calculate_Leave_Deduction];
GO

CREATE PROCEDURE [dbo].[sp_Calculate_Leave_Deduction]
    @AdminID SMALLINT,
    @StartDate DATE,
    @EndDate DATE,
    @TotalDays DECIMAL(5,2),
    @DeductionAmount DECIMAL(10,2) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MonthlySalary DECIMAL(10,2);
    DECLARE @DaysInMonth INT;

    -- ดึงเงินเดือนปัจจุบัน
    SELECT @MonthlySalary = [MonthlySalary]
    FROM [dbo].[Employee_Salary]
    WHERE [Admin_ID] = @AdminID AND [IsActive] = 1;

    -- นับจำนวนวันในเดือน (ใช้เดือนที่เริ่มลา)
    SET @DaysInMonth = DAY(EOMONTH(@StartDate));

    -- คำนวณยอดหัก: (เงินเดือน / จำนวนวันในเดือน) × จำนวนวันที่ลา
    SET @DeductionAmount = (@MonthlySalary / @DaysInMonth) * @TotalDays;

    SELECT @DeductionAmount AS DeductionAmount,
           @MonthlySalary AS BaseSalary,
           @DaysInMonth AS DaysInMonth,
           @TotalDays AS LeaveDays;
END;
GO

PRINT '✅ Procedure sp_Calculate_Leave_Deduction created';
GO

-- ============================================================================
-- STORED PROCEDURES - PAYROLL MANAGEMENT
-- ============================================================================

-- SP: Calculate OT Rate and Amount
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_Calculate_OT_Amount')
    DROP PROCEDURE [dbo].[sp_Calculate_OT_Amount];
GO

CREATE PROCEDURE [dbo].[sp_Calculate_OT_Amount]
    @AdminID SMALLINT,
    @OTHours DECIMAL(5,2),
    @CalculationMonth DATE, -- ใช้สำหรับดึงจำนวนวันในเดือน
    @OTRate DECIMAL(10,2) OUTPUT,
    @OTAmount DECIMAL(10,2) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MonthlySalary DECIMAL(10,2);
    DECLARE @DaysInMonth INT;

    -- ดึงเงินเดือนปัจจุบัน
    SELECT @MonthlySalary = [MonthlySalary]
    FROM [dbo].[Employee_Salary]
    WHERE [Admin_ID] = @AdminID AND [IsActive] = 1;

    -- นับจำนวนวันในเดือน
    SET @DaysInMonth = DAY(EOMONTH(@CalculationMonth));

    -- คำนวณอัตราค่าโอทีต่อชั่วโมง: (เงินเดือน / วันในเดือน / 8 ชม.)
    SET @OTRate = @MonthlySalary / @DaysInMonth / 8.0;

    -- คำนวณค่าโอที: อัตราต่อชั่วโมง × จำนวนชั่วโมง
    SET @OTAmount = @OTRate * @OTHours;

    SELECT @OTRate AS OTRate,
           @OTAmount AS OTAmount,
           @MonthlySalary AS BaseSalary,
           @DaysInMonth AS DaysInMonth,
           @OTHours AS OTHours;
END;
GO

PRINT '✅ Procedure sp_Calculate_OT_Amount created';
GO

-- SP: Generate Payroll for Period
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_Generate_Payroll_For_Period')
    DROP PROCEDURE [dbo].[sp_Generate_Payroll_For_Period];
GO

CREATE PROCEDURE [dbo].[sp_Generate_Payroll_For_Period]
    @Year SMALLINT,
    @Month TINYINT,
    @CreatedBy_AdminID SMALLINT,
    @PayrollPeriodID INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @PeriodCode VARCHAR(20);
        DECLARE @PeriodName NVARCHAR(100);
        DECLARE @DaysInMonth INT;
        DECLARE @FirstDayOfMonth DATE;

        SET @FirstDayOfMonth = DATEFROMPARTS(@Year, @Month, 1);
        SET @DaysInMonth = DAY(EOMONTH(@FirstDayOfMonth));
        SET @PeriodCode = 'PAYROLL-' + CAST(@Year AS VARCHAR(4)) + RIGHT('0' + CAST(@Month AS VARCHAR(2)), 2);

        -- สร้างชื่อเดือนภาษาไทย
        SET @PeriodName = N'เดือน ' +
            CASE @Month
                WHEN 1 THEN N'มกราคม' WHEN 2 THEN N'กุมภาพันธ์' WHEN 3 THEN N'มีนาคม'
                WHEN 4 THEN N'เมษายน' WHEN 5 THEN N'พฤษภาคม' WHEN 6 THEN N'มิถุนายน'
                WHEN 7 THEN N'กรกฎาคม' WHEN 8 THEN N'สิงหาคม' WHEN 9 THEN N'กันยายน'
                WHEN 10 THEN N'ตุลาคม' WHEN 11 THEN N'พฤศจิกายน' WHEN 12 THEN N'ธันวาคม'
            END + ' ' + CAST(@Year + 543 AS NVARCHAR(4)); -- แปลงเป็น พ.ศ.

        -- สร้าง Payroll Period
        IF NOT EXISTS (SELECT 1 FROM [dbo].[Payroll_Periods] WHERE [Year] = @Year AND [Month] = @Month)
        BEGIN
            INSERT INTO [dbo].[Payroll_Periods]
                ([PeriodCode], [Year], [Month], [PeriodName], [DaysInMonth], [CreatedBy_AdminID])
            VALUES
                (@PeriodCode, @Year, @Month, @PeriodName, @DaysInMonth, @CreatedBy_AdminID);

            SET @PayrollPeriodID = SCOPE_IDENTITY();

            -- สร้าง Payroll Records สำหรับพนักงานทุกคน
            INSERT INTO [dbo].[Payroll_Records]
                ([PayrollPeriod_ID], [Admin_ID], [EmployeeName], [BaseSalary], [WorkDays])
            SELECT
                @PayrollPeriodID,
                A.[ID],
                A.[Name],
                ISNULL(ES.[MonthlySalary], 0),
                @DaysInMonth
            FROM [dbo].[Admin] A
            LEFT JOIN [dbo].[Employee_Salary] ES ON ES.[Admin_ID] = A.[ID] AND ES.[IsActive] = 1
            WHERE A.[Status] = 1;

            -- คำนวณการหักเงินจากการลา
            UPDATE PR
            SET
                PR.[LeaveDeduction] = ISNULL(LeaveCalc.[TotalDeduction], 0),
                PR.[LeaveDays] = ISNULL(LeaveCalc.[TotalLeaveDays], 0),
                PR.[WorkDays] = @DaysInMonth - ISNULL(LeaveCalc.[TotalLeaveDays], 0)
            FROM [dbo].[Payroll_Records] PR
            LEFT JOIN (
                SELECT
                    LR.[Admin_ID],
                    SUM(LR.[DeductionAmount]) AS TotalDeduction,
                    SUM(LR.[TotalDays]) AS TotalLeaveDays
                FROM [dbo].[Leave_Requests] LR
                WHERE YEAR(LR.[StartDate]) = @Year
                  AND MONTH(LR.[StartDate]) = @Month
                  AND LR.[Status] = 'APPROVED'
                  AND LR.[DeductSalary] = 1
                GROUP BY LR.[Admin_ID]
            ) LeaveCalc ON LeaveCalc.[Admin_ID] = PR.[Admin_ID]
            WHERE PR.[PayrollPeriod_ID] = @PayrollPeriodID;

            -- อัพเดทสรุปใน Payroll Period
            UPDATE PP
            SET
                PP.[TotalEmployees] = (SELECT COUNT(*) FROM [dbo].[Payroll_Records] WHERE [PayrollPeriod_ID] = @PayrollPeriodID),
                PP.[TotalGrossPay] = (SELECT SUM([TotalEarnings]) FROM [dbo].[Payroll_Records] WHERE [PayrollPeriod_ID] = @PayrollPeriodID),
                PP.[TotalDeductions] = (SELECT SUM([TotalDeductions]) FROM [dbo].[Payroll_Records] WHERE [PayrollPeriod_ID] = @PayrollPeriodID),
                PP.[TotalNetPay] = (SELECT SUM([NetSalary]) FROM [dbo].[Payroll_Records] WHERE [PayrollPeriod_ID] = @PayrollPeriodID)
            FROM [dbo].[Payroll_Periods] PP
            WHERE PP.[ID] = @PayrollPeriodID;

            COMMIT TRANSACTION;

            SELECT 'Success' AS Result,
                   @PayrollPeriodID AS PayrollPeriodID,
                   'Payroll generated successfully' AS Message;
        END
        ELSE
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 'Error' AS Result,
                   'Payroll period already exists' AS Message;
        END
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SELECT 'Error' AS Result,
               ERROR_MESSAGE() AS Message;
    END CATCH
END;
GO

PRINT '✅ Procedure sp_Generate_Payroll_For_Period created';
GO

PRINT '============================================================================';
PRINT 'Payroll and Leave Views and Procedures Created Successfully!';
PRINT '============================================================================';
GO
