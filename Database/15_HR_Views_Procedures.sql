-- ============================================================================
-- Human Resources Management System - Views and Stored Procedures
-- Version: 1.0
-- Date: 2025-01-30
-- Description: Views and procedures for HR management
-- ============================================================================

USE TakeTime;
GO

PRINT '============================================================================';
PRINT 'Creating HR Management Views and Stored Procedures';
PRINT '============================================================================';
GO

-- ============================================================================
-- VIEWS
-- ============================================================================

-- View: Employee Complete Profile (360° view)
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Employee_Complete_Profile')
    DROP VIEW [dbo].[vw_Employee_Complete_Profile];
GO

CREATE VIEW [dbo].[vw_Employee_Complete_Profile]
AS
SELECT
    -- ข้อมูลพื้นฐานจาก Admin
    A.[ID] AS Admin_ID,
    A.[FirstName] + ' ' + A.[LastName] AS Name,
    A.[FirstName] AS AdminNickName,
    A.[Role] AS UserType,
    A.[Status],
    ES.[EffectiveDate] AS RegisterDate,

    -- ข้อมูลส่วนตัว
    EP.[FullNameTH],
    EP.[FullNameEN],
    EP.[NickName] AS ProfileNickName,
    EP.[BirthDate],
    EP.[Age],
    EP.[Gender],
    EP.[Nationality],
    EP.[Religion],
    EP.[BloodType],
    EP.[MaritalStatus],
    EP.[IDCardNumber],
    EP.[IDCardExpiryDate],
    EP.[MobilePhone],
    EP.[PersonalEmail],
    EP.[WorkEmail],
    EP.[PhotoPath],

    -- ข้อมูลการจ้างงานปัจจุบัน
    ES.[MonthlySalary] AS CurrentSalary,
    ES.[Position] AS CurrentPosition,
    ES.[EffectiveDate] AS SalaryEffectiveDate,

    -- สัญญาจ้างปัจจุบัน
    EC.[ContractNumber],
    EC.[ContractType],
    EC.[ContractStatus],
    EC.[StartDate] AS ContractStartDate,
    EC.[EndDate] AS ContractEndDate,
    EC.[DaysUntilExpiry] AS ContractDaysUntilExpiry,
    EC.[Department],

    -- อายุงาน (Days, Months, Years)
    DATEDIFF(DAY, COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE)), GETDATE()) AS TotalServiceDays,
    DATEDIFF(MONTH, COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE)), GETDATE()) AS TotalServiceMonths,
    DATEDIFF(YEAR, COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE)), GETDATE()) AS TotalServiceYears,

    -- สถิติการลาปีนี้
    (SELECT ISNULL(SUM(LR.[TotalDays]), 0)
     FROM [dbo].[Leave_Requests] LR
     WHERE LR.[Admin_ID] = A.[ID]
       AND YEAR(LR.[StartDate]) = YEAR(GETDATE())
       AND LR.[Status] = 'APPROVED') AS YearToDateLeaveDays,

    -- การประเมินผลล่าสุด
    (SELECT TOP 1 EPR.[OverallScore]
     FROM [dbo].[Employee_Performance_Reviews] EPR
     WHERE EPR.[Admin_ID] = A.[ID]
     ORDER BY EPR.[ReviewDate] DESC) AS LatestPerformanceScore,

    -- จำนวนเอกสาร
    (SELECT COUNT(*)
     FROM [dbo].[Employee_Documents] ED
     WHERE ED.[Admin_ID] = A.[ID] AND ED.[IsActive] = 1) AS TotalDocuments,

    -- เอกสารที่ใกล้หมดอายุ (< 30 วัน)
    (SELECT COUNT(*)
     FROM [dbo].[Employee_Documents] ED
     WHERE ED.[Admin_ID] = A.[ID]
       AND ED.[IsExpirable] = 1
       AND ED.[DaysUntilExpiry] <= 30
       AND ED.[DaysUntilExpiry] >= 0) AS ExpiringDocumentsCount

FROM [dbo].[Admin] A
LEFT JOIN [dbo].[Employee_Profile] EP ON EP.[Admin_ID] = A.[ID]
LEFT JOIN [dbo].[Employee_Salary] ES ON ES.[Admin_ID] = A.[ID] AND ES.[IsActive] = 1
LEFT JOIN [dbo].[Employee_Contracts] EC ON EC.[Admin_ID] = A.[ID] AND EC.[ContractStatus] = 'ACTIVE'
WHERE A.[Status] = 1;
GO

PRINT '✅ View vw_Employee_Complete_Profile created';
GO

-- View: Employee Service Age Summary
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Employee_Service_Age')
    DROP VIEW [dbo].[vw_Employee_Service_Age];
GO

CREATE VIEW [dbo].[vw_Employee_Service_Age]
AS
SELECT
    A.[ID] AS Admin_ID,
    A.[FirstName] + ' ' + A.[LastName] AS Name,
    COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE)) AS HireDate,

    -- คำนวณอายุงานแบบละเอียด
    DATEDIFF(DAY, COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE)), GETDATE()) AS TotalDays,
    DATEDIFF(MONTH, COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE)), GETDATE()) AS TotalMonths,

    -- Years
    DATEDIFF(YEAR, COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE)), GETDATE()) -
    CASE WHEN MONTH(COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE))) > MONTH(GETDATE()) OR
         (MONTH(COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE))) = MONTH(GETDATE()) AND DAY(COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE))) > DAY(GETDATE()))
    THEN 1 ELSE 0 END AS Years,

    -- Months (remaining after years)
    (DATEDIFF(MONTH, COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE)), GETDATE()) % 12) AS Months,

    -- Days (approximate remaining after months)
    DATEDIFF(DAY,
        DATEADD(MONTH, DATEDIFF(MONTH, COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE)), GETDATE()), COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE))),
        GETDATE()
    ) AS Days,

    -- Formatted text
    CAST(
        DATEDIFF(YEAR, COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE)), GETDATE()) -
        CASE WHEN MONTH(COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE))) > MONTH(GETDATE()) OR
             (MONTH(COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE))) = MONTH(GETDATE()) AND DAY(COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE))) > DAY(GETDATE()))
        THEN 1 ELSE 0 END AS VARCHAR(10)
    ) + N' ปี ' +
    CAST((DATEDIFF(MONTH, COALESCE(ES.[EffectiveDate], CAST(GETDATE() AS DATE)), GETDATE()) % 12) AS VARCHAR(10)) + N' เดือน' AS ServiceAgeText,

    ES.[Position],
    ES.[MonthlySalary]

FROM [dbo].[Admin] A
LEFT JOIN [dbo].[Employee_Salary] ES ON ES.[Admin_ID] = A.[ID] AND ES.[IsActive] = 1
WHERE A.[Status] = 1;
GO

PRINT '✅ View vw_Employee_Service_Age created';
GO

-- View: Documents Expiry Alert
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Employee_Documents_Expiry_Alert')
    DROP VIEW [dbo].[vw_Employee_Documents_Expiry_Alert];
GO

CREATE VIEW [dbo].[vw_Employee_Documents_Expiry_Alert]
AS
SELECT
    A.[ID] AS Admin_ID,
    A.[FirstName] + ' ' + A.[LastName] AS EmployeeName,
    ED.[ID] AS Document_ID,
    ED.[DocumentType],
    ED.[DocumentName],
    ED.[ExpiryDate],
    ED.[DaysUntilExpiry],
    CASE
        WHEN ED.[DaysUntilExpiry] < 0 THEN 'EXPIRED'
        WHEN ED.[DaysUntilExpiry] <= 7 THEN 'CRITICAL'
        WHEN ED.[DaysUntilExpiry] <= 30 THEN 'WARNING'
        ELSE 'OK'
    END AS AlertLevel,
    EP.[MobilePhone],
    EP.[WorkEmail]
FROM [dbo].[Employee_Documents] ED
INNER JOIN [dbo].[Admin] A ON A.[ID] = ED.[Admin_ID]
LEFT JOIN [dbo].[Employee_Profile] EP ON EP.[Admin_ID] = A.[ID]
WHERE ED.[IsExpirable] = 1
  AND ED.[IsActive] = 1
  AND ED.[DaysUntilExpiry] <= 30;
GO

PRINT '✅ View vw_Employee_Documents_Expiry_Alert created';
GO

-- View: Contracts Expiry Alert
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Employee_Contracts_Expiry_Alert')
    DROP VIEW [dbo].[vw_Employee_Contracts_Expiry_Alert];
GO

CREATE VIEW [dbo].[vw_Employee_Contracts_Expiry_Alert]
AS
SELECT
    A.[ID] AS Admin_ID,
    A.[FirstName] + ' ' + A.[LastName] AS EmployeeName,
    EC.[ID] AS Contract_ID,
    EC.[ContractNumber],
    EC.[ContractType],
    EC.[Position],
    EC.[StartDate],
    EC.[EndDate],
    EC.[DaysUntilExpiry],
    CASE
        WHEN EC.[DaysUntilExpiry] < 0 THEN 'EXPIRED'
        WHEN EC.[DaysUntilExpiry] <= 7 THEN 'CRITICAL'
        WHEN EC.[DaysUntilExpiry] <= 30 THEN 'WARNING'
        WHEN EC.[DaysUntilExpiry] <= 90 THEN 'NOTICE'
        ELSE 'OK'
    END AS AlertLevel,
    EC.[Salary],
    EP.[MobilePhone],
    EP.[WorkEmail]
FROM [dbo].[Employee_Contracts] EC
INNER JOIN [dbo].[Admin] A ON A.[ID] = EC.[Admin_ID]
LEFT JOIN [dbo].[Employee_Profile] EP ON EP.[Admin_ID] = A.[ID]
WHERE EC.[ContractStatus] = 'ACTIVE'
  AND EC.[IsIndefinite] = 0
  AND EC.[DaysUntilExpiry] <= 90;
GO

PRINT '✅ View vw_Employee_Contracts_Expiry_Alert created';
GO

-- View: Employee Training Summary
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Employee_Training_Summary')
    DROP VIEW [dbo].[vw_Employee_Training_Summary];
GO

CREATE VIEW [dbo].[vw_Employee_Training_Summary]
AS
SELECT
    A.[ID] AS Admin_ID,
    A.[FirstName] + ' ' + A.[LastName] AS EmployeeName,
    ES.[Position],

    -- สถิติการอบรม
    (SELECT COUNT(*)
     FROM [dbo].[Employee_Training] ET
     WHERE ET.[Admin_ID] = A.[ID]
       AND ET.[CompletionStatus] = 'COMPLETED') AS TotalTrainingsCompleted,

    (SELECT COUNT(*)
     FROM [dbo].[Employee_Training] ET
     WHERE ET.[Admin_ID] = A.[ID]
       AND ET.[CompletionStatus] = 'REGISTERED') AS OngoingTrainings,

    (SELECT ISNULL(SUM(ET.[TotalHours]), 0)
     FROM [dbo].[Employee_Training] ET
     WHERE ET.[Admin_ID] = A.[ID]
       AND ET.[CompletionStatus] = 'COMPLETED') AS TotalTrainingHours,

    (SELECT ISNULL(SUM(ET.[TrainingCost]), 0)
     FROM [dbo].[Employee_Training] ET
     WHERE ET.[Admin_ID] = A.[ID]
       AND ET.[IsPaidByCompany] = 1) AS TotalTrainingInvestment,

    -- การอบรมล่าสุด
    (SELECT TOP 1 ET.[TrainingName]
     FROM [dbo].[Employee_Training] ET
     WHERE ET.[Admin_ID] = A.[ID]
     ORDER BY ET.[EndDate] DESC) AS LatestTraining,

    (SELECT TOP 1 ET.[EndDate]
     FROM [dbo].[Employee_Training] ET
     WHERE ET.[Admin_ID] = A.[ID]
     ORDER BY ET.[EndDate] DESC) AS LatestTrainingDate

FROM [dbo].[Admin] A
LEFT JOIN [dbo].[Employee_Salary] ES ON ES.[Admin_ID] = A.[ID] AND ES.[IsActive] = 1
WHERE A.[Status] = 1;
GO

PRINT '✅ View vw_Employee_Training_Summary created';
GO

-- ============================================================================
-- STORED PROCEDURES - EMPLOYEE MANAGEMENT
-- ============================================================================

-- SP: Create New Employee Profile
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_Create_Employee_Profile')
    DROP PROCEDURE [dbo].[sp_Create_Employee_Profile];
GO

CREATE PROCEDURE [dbo].[sp_Create_Employee_Profile]
    @Admin_ID SMALLINT,
    @FullNameTH NVARCHAR(200),
    @BirthDate DATE,
    @Gender VARCHAR(10),
    @IDCardNumber VARCHAR(13),
    @MobilePhone VARCHAR(20),
    @PersonalEmail NVARCHAR(100),
    @UpdatedBy_AdminID SMALLINT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- ตรวจสอบว่ามี Profile อยู่แล้วหรือไม่
        IF EXISTS (SELECT 1 FROM [dbo].[Employee_Profile] WHERE [Admin_ID] = @Admin_ID)
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 'Error' AS Result, 'Employee profile already exists' AS Message;
            RETURN;
        END

        -- สร้าง Profile
        INSERT INTO [dbo].[Employee_Profile] (
            [Admin_ID], [FullNameTH], [BirthDate], [Gender],
            [IDCardNumber], [MobilePhone], [PersonalEmail],
            [UpdatedBy_AdminID], [CreatedDate]
        )
        VALUES (
            @Admin_ID, @FullNameTH, @BirthDate, @Gender,
            @IDCardNumber, @MobilePhone, @PersonalEmail,
            @UpdatedBy_AdminID, GETDATE()
        );

        COMMIT TRANSACTION;

        SELECT 'Success' AS Result,
               'Employee profile created successfully' AS Message,
               SCOPE_IDENTITY() AS ProfileID;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SELECT 'Error' AS Result,
               ERROR_MESSAGE() AS Message;
    END CATCH
END;
GO

PRINT '✅ Procedure sp_Create_Employee_Profile created';
GO

-- SP: Record Employment History Event
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_Record_Employment_Event')
    DROP PROCEDURE [dbo].[sp_Record_Employment_Event];
GO

CREATE PROCEDURE [dbo].[sp_Record_Employment_Event]
    @Admin_ID SMALLINT,
    @EventType VARCHAR(30),
    @EventDate DATE,
    @PositionBefore NVARCHAR(100) = NULL,
    @PositionAfter NVARCHAR(100) = NULL,
    @SalaryBefore DECIMAL(10,2) = NULL,
    @SalaryAfter DECIMAL(10,2) = NULL,
    @Reason NVARCHAR(1000) = NULL,
    @RecordedBy_AdminID SMALLINT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- บันทึกประวัติ
        INSERT INTO [dbo].[Employee_Employment_History] (
            [Admin_ID], [EventType], [EventDate],
            [PositionBefore], [PositionAfter],
            [SalaryBefore], [SalaryAfter],
            [Reason], [RecordedBy_AdminID], [RecordedDate]
        )
        VALUES (
            @Admin_ID, @EventType, @EventDate,
            @PositionBefore, @PositionAfter,
            @SalaryBefore, @SalaryAfter,
            @Reason, @RecordedBy_AdminID, GETDATE()
        );

        -- ถ้าเป็นการเปลี่ยนเงินเดือน ให้อัพเดท Employee_Salary
        IF @EventType = 'SALARY_CHANGE' AND @SalaryAfter IS NOT NULL
        BEGIN
            -- ปิด salary record เดิม
            UPDATE [dbo].[Employee_Salary]
            SET [IsActive] = 0
            WHERE [Admin_ID] = @Admin_ID AND [IsActive] = 1;

            -- สร้าง record ใหม่
            INSERT INTO [dbo].[Employee_Salary] (
                [Admin_ID], [MonthlySalary], [Position], [EffectiveDate], [IsActive]
            )
            VALUES (
                @Admin_ID, @SalaryAfter, @PositionAfter, @EventDate, 1
            );
        END

        COMMIT TRANSACTION;

        SELECT 'Success' AS Result,
               'Employment event recorded successfully' AS Message,
               SCOPE_IDENTITY() AS EventID;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SELECT 'Error' AS Result,
               ERROR_MESSAGE() AS Message;
    END CATCH
END;
GO

PRINT '✅ Procedure sp_Record_Employment_Event created';
GO

-- SP: Upload Employee Document
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_Upload_Employee_Document')
    DROP PROCEDURE [dbo].[sp_Upload_Employee_Document];
GO

CREATE PROCEDURE [dbo].[sp_Upload_Employee_Document]
    @Admin_ID SMALLINT,
    @DocumentType VARCHAR(50),
    @DocumentName NVARCHAR(200),
    @FilePath NVARCHAR(500),
    @FileSize BIGINT,
    @FileType VARCHAR(50),
    @ExpiryDate DATE = NULL,
    @IsConfidential BIT = 0,
    @UploadedBy_AdminID SMALLINT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        DECLARE @IsExpirable BIT = 0;
        IF @ExpiryDate IS NOT NULL
            SET @IsExpirable = 1;

        INSERT INTO [dbo].[Employee_Documents] (
            [Admin_ID], [DocumentType], [DocumentName],
            [FilePath], [FileSize], [FileType],
            [ExpiryDate], [IsExpirable], [IsConfidential],
            [UploadedBy_AdminID], [UploadedDate]
        )
        VALUES (
            @Admin_ID, @DocumentType, @DocumentName,
            @FilePath, @FileSize, @FileType,
            @ExpiryDate, @IsExpirable, @IsConfidential,
            @UploadedBy_AdminID, GETDATE()
        );

        SELECT 'Success' AS Result,
               'Document uploaded successfully' AS Message,
               SCOPE_IDENTITY() AS DocumentID;
    END TRY
    BEGIN CATCH
        SELECT 'Error' AS Result,
               ERROR_MESSAGE() AS Message;
    END CATCH
END;
GO

PRINT '✅ Procedure sp_Upload_Employee_Document created';
GO

-- SP: Get Employee Service Age
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_Get_Employee_Service_Age')
    DROP PROCEDURE [dbo].[sp_Get_Employee_Service_Age];
GO

CREATE PROCEDURE [dbo].[sp_Get_Employee_Service_Age]
    @Admin_ID SMALLINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Admin_ID,
        Name,
        HireDate,
        TotalDays,
        TotalMonths,
        Years,
        Months,
        Days,
        ServiceAgeText,
        Position,
        MonthlySalary
    FROM [dbo].[vw_Employee_Service_Age]
    WHERE Admin_ID = @Admin_ID;
END;
GO

PRINT '✅ Procedure sp_Get_Employee_Service_Age created';
GO

-- SP: Generate Contract Renewal
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_Generate_Contract_Renewal')
    DROP PROCEDURE [dbo].[sp_Generate_Contract_Renewal];
GO

CREATE PROCEDURE [dbo].[sp_Generate_Contract_Renewal]
    @Old_Contract_ID INT,
    @NewStartDate DATE,
    @NewEndDate DATE,
    @NewSalary DECIMAL(10,2),
    @CreatedBy_AdminID SMALLINT,
    @New_Contract_ID INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Admin_ID SMALLINT;
        DECLARE @Position NVARCHAR(200);
        DECLARE @ContractType VARCHAR(30);
        DECLARE @Department NVARCHAR(100);
        DECLARE @NewContractNumber NVARCHAR(50);

        -- ดึงข้อมูลสัญญาเดิม
        SELECT
            @Admin_ID = [Admin_ID],
            @Position = [Position],
            @ContractType = [ContractType],
            @Department = [Department]
        FROM [dbo].[Employee_Contracts]
        WHERE [ID] = @Old_Contract_ID;

        -- สร้างเลขที่สัญญาใหม่
        SET @NewContractNumber = 'CNT-' + CONVERT(VARCHAR(8), GETDATE(), 112) + '-' + CAST(@Admin_ID AS VARCHAR(10));

        -- ปิดสัญญาเดิม
        UPDATE [dbo].[Employee_Contracts]
        SET [ContractStatus] = 'RENEWED',
            [IsRenewed] = 1,
            [UpdatedDate] = GETDATE()
        WHERE [ID] = @Old_Contract_ID;

        -- สร้างสัญญาใหม่
        INSERT INTO [dbo].[Employee_Contracts] (
            [Admin_ID], [ContractNumber], [ContractType], [ContractStatus],
            [StartDate], [EndDate], [Position], [Department],
            [Salary], [CreatedBy_AdminID], [CreatedDate]
        )
        VALUES (
            @Admin_ID, @NewContractNumber, @ContractType, 'ACTIVE',
            @NewStartDate, @NewEndDate, @Position, @Department,
            @NewSalary, @CreatedBy_AdminID, GETDATE()
        );

        SET @New_Contract_ID = SCOPE_IDENTITY();

        -- อัพเดท link กลับไปสัญญาเดิม
        UPDATE [dbo].[Employee_Contracts]
        SET [RenewedTo_ContractID] = @New_Contract_ID
        WHERE [ID] = @Old_Contract_ID;

        COMMIT TRANSACTION;

        SELECT 'Success' AS Result,
               @New_Contract_ID AS NewContractID,
               @NewContractNumber AS NewContractNumber,
               'Contract renewed successfully' AS Message;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SELECT 'Error' AS Result,
               ERROR_MESSAGE() AS Message;
    END CATCH
END;
GO

PRINT '✅ Procedure sp_Generate_Contract_Renewal created';
GO

-- SP: Get Employee Dashboard Statistics
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_Get_Employee_Dashboard_Stats')
    DROP PROCEDURE [dbo].[sp_Get_Employee_Dashboard_Stats];
GO

CREATE PROCEDURE [dbo].[sp_Get_Employee_Dashboard_Stats]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        -- พนักงานทั้งหมด
        (SELECT COUNT(*) FROM [dbo].[Admin] WHERE [Status] = 1) AS TotalActiveEmployees,

        -- พนักงานใหม่เดือนนี้
        (SELECT COUNT(*) FROM [dbo].[Admin] A
         INNER JOIN [dbo].[Employee_Salary] ES ON ES.[Admin_ID] = A.[ID] AND ES.[IsActive] = 1
         WHERE A.[Status] = 1
           AND YEAR(ES.[EffectiveDate]) = YEAR(GETDATE())
           AND MONTH(ES.[EffectiveDate]) = MONTH(GETDATE())) AS NewEmployeesThisMonth,

        -- สัญญาที่กำลังจะหมดอายุ (30 วัน)
        (SELECT COUNT(*) FROM [dbo].[vw_Employee_Contracts_Expiry_Alert]
         WHERE [AlertLevel] IN ('WARNING', 'CRITICAL')) AS ContractsExpiringWithin30Days,

        -- เอกสารที่กำลังจะหมดอายุ (30 วัน)
        (SELECT COUNT(*) FROM [dbo].[vw_Employee_Documents_Expiry_Alert]
         WHERE [AlertLevel] IN ('WARNING', 'CRITICAL')) AS DocumentsExpiringWithin30Days,

        -- การอบรมที่กำลังดำเนินการ
        (SELECT COUNT(*) FROM [dbo].[Employee_Training]
         WHERE [CompletionStatus] = 'REGISTERED') AS OngoingTrainings,

        -- คำขอลาที่รออนุมัติ
        (SELECT COUNT(*) FROM [dbo].[Leave_Requests]
         WHERE [Status] = 'PENDING') AS PendingLeaveRequests,

        -- เงินเดือนรวมต่อเดือน
        (SELECT ISNULL(SUM(ES.[MonthlySalary]), 0)
         FROM [dbo].[Employee_Salary] ES
         INNER JOIN [dbo].[Admin] A ON A.[ID] = ES.[Admin_ID]
         WHERE ES.[IsActive] = 1 AND A.[Status] = 1) AS TotalMonthlyPayroll,

        -- ค่าใช้จ่ายการอบรมปีนี้
        (SELECT ISNULL(SUM([TrainingCost]), 0)
         FROM [dbo].[Employee_Training]
         WHERE YEAR([StartDate]) = YEAR(GETDATE())
           AND [IsPaidByCompany] = 1) AS TrainingCostThisYear;
END;
GO

PRINT '✅ Procedure sp_Get_Employee_Dashboard_Stats created';
GO

PRINT '============================================================================';
PRINT 'HR Management Views and Procedures Created Successfully!';
PRINT '============================================================================';
PRINT 'Views Created:';
PRINT '  - vw_Employee_Complete_Profile';
PRINT '  - vw_Employee_Service_Age';
PRINT '  - vw_Employee_Documents_Expiry_Alert';
PRINT '  - vw_Employee_Contracts_Expiry_Alert';
PRINT '  - vw_Employee_Training_Summary';
PRINT '';
PRINT 'Stored Procedures Created:';
PRINT '  - sp_Create_Employee_Profile';
PRINT '  - sp_Record_Employment_Event';
PRINT '  - sp_Upload_Employee_Document';
PRINT '  - sp_Get_Employee_Service_Age';
PRINT '  - sp_Generate_Contract_Renewal';
PRINT '  - sp_Get_Employee_Dashboard_Stats';
PRINT '============================================================================';
GO
