-- =============================================================================
-- Migration 005: Create Human Resources Service Schema
-- =============================================================================
-- Target Database: TakeTime_HR_{TenantId} (per-tenant)
-- Description: Creates all tables for the HR microservice including
--              employees, departments, attendance, leave, payroll,
--              and Thai labor law compliance (Social Security, PND).
-- =============================================================================

-- =============================================
-- Departments - Organizational structure
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Departments')
BEGIN
    CREATE TABLE Departments (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ParentId        UNIQUEIDENTIFIER NULL,
        Code            NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(200)    NOT NULL,
        NameThai        NVARCHAR(200)    NULL,
        ManagerId       UNIQUEIDENTIFIER NULL,
        IsActive        BIT              NOT NULL DEFAULT 1,
        SortOrder       INT              NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_Departments PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Departments_Parent FOREIGN KEY (ParentId) REFERENCES Departments(Id),
        CONSTRAINT UQ_Departments_Code UNIQUE (Code)
    );
END
GO

-- =============================================
-- Positions - Job titles/roles
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Positions')
BEGIN
    CREATE TABLE Positions (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        DepartmentId    UNIQUEIDENTIFIER NOT NULL,
        Code            NVARCHAR(50)     NOT NULL,
        Title           NVARCHAR(200)    NOT NULL,
        TitleThai       NVARCHAR(200)    NULL,
        Level           INT              NOT NULL DEFAULT 1,
        MinSalary       DECIMAL(18,2)    NULL,
        MaxSalary       DECIMAL(18,2)    NULL,
        IsActive        BIT              NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Positions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Positions_Departments FOREIGN KEY (DepartmentId) REFERENCES Departments(Id),
        CONSTRAINT UQ_Positions_Code UNIQUE (Code)
    );
END
GO

-- =============================================
-- Employees - Employee master records
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Employees')
BEGIN
    CREATE TABLE Employees (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        EmployeeCode        NVARCHAR(20)     NOT NULL,
        UserId              UNIQUEIDENTIFIER NULL,
        DepartmentId        UNIQUEIDENTIFIER NOT NULL,
        PositionId          UNIQUEIDENTIFIER NOT NULL,
        ReportsToId         UNIQUEIDENTIFIER NULL,
        FirstName           NVARCHAR(200)    NOT NULL,
        LastName            NVARCHAR(200)    NOT NULL,
        FirstNameThai       NVARCHAR(200)    NULL,
        LastNameThai        NVARCHAR(200)    NULL,
        Nickname            NVARCHAR(100)    NULL,
        Email               NVARCHAR(500)    NULL,
        Phone               NVARCHAR(50)     NULL,
        DateOfBirth         DATE             NULL,
        Gender              NVARCHAR(20)     NULL,
        NationalId          NVARCHAR(20)     NULL,
        PassportNumber      NVARCHAR(50)     NULL,
        Nationality         NVARCHAR(100)    NOT NULL DEFAULT N'Thai',
        Address             NVARCHAR(MAX)    NULL,
        EmergencyContactName NVARCHAR(200)   NULL,
        EmergencyContactPhone NVARCHAR(50)   NULL,
        EmergencyContactRelation NVARCHAR(100) NULL,
        HireDate            DATE             NOT NULL,
        ProbationEndDate    DATE             NULL,
        TerminationDate     DATE             NULL,
        TerminationReason   NVARCHAR(500)    NULL,
        EmploymentType      NVARCHAR(50)     NOT NULL DEFAULT 'FullTime',
        Status              NVARCHAR(50)     NOT NULL DEFAULT 'Active',
        MonthlySalary       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        DailyRate           DECIMAL(18,2)    NULL,
        HourlyRate          DECIMAL(18,2)    NULL,
        BankAccountName     NVARCHAR(200)    NULL,
        BankAccountNumber   NVARCHAR(50)     NULL,
        BankCode            NVARCHAR(20)     NULL,
        SocialSecurityNumber NVARCHAR(20)    NULL,
        TaxId               NVARCHAR(20)     NULL,
        PhotoUrl            NVARCHAR(1000)   NULL,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           NVARCHAR(200)    NULL,
        CONSTRAINT PK_Employees PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Employees_Departments FOREIGN KEY (DepartmentId) REFERENCES Departments(Id),
        CONSTRAINT FK_Employees_Positions FOREIGN KEY (PositionId) REFERENCES Positions(Id),
        CONSTRAINT FK_Employees_ReportsTo FOREIGN KEY (ReportsToId) REFERENCES Employees(Id),
        CONSTRAINT UQ_Employees_Code UNIQUE (EmployeeCode)
    );

    CREATE NONCLUSTERED INDEX IX_Employees_DepartmentId ON Employees (DepartmentId) INCLUDE (Status);
    CREATE NONCLUSTERED INDEX IX_Employees_Status ON Employees (Status) WHERE Status = 'Active';
    CREATE NONCLUSTERED INDEX IX_Employees_NationalId ON Employees (NationalId) WHERE NationalId IS NOT NULL;
END
GO

-- =============================================
-- WorkSchedules - Shift definitions
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkSchedules')
BEGIN
    CREATE TABLE WorkSchedules (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Code            NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(200)    NOT NULL,
        ShiftStart      TIME             NOT NULL,
        ShiftEnd        TIME             NOT NULL,
        BreakMinutes    INT              NOT NULL DEFAULT 60,
        WorkHours       DECIMAL(5,2)     NOT NULL DEFAULT 8.00,
        IsNightShift    BIT              NOT NULL DEFAULT 0,
        IsActive        BIT              NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_WorkSchedules PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_WorkSchedules_Code UNIQUE (Code)
    );
END
GO

-- =============================================
-- EmployeeSchedules - Employee shift assignments
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeeSchedules')
BEGIN
    CREATE TABLE EmployeeSchedules (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        EmployeeId      UNIQUEIDENTIFIER NOT NULL,
        WorkScheduleId  UNIQUEIDENTIFIER NOT NULL,
        [Date]          DATE             NOT NULL,
        IsHoliday       BIT              NOT NULL DEFAULT 0,
        IsDayOff        BIT              NOT NULL DEFAULT 0,
        Notes           NVARCHAR(500)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        CONSTRAINT PK_EmployeeSchedules PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_EmployeeSchedules_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(Id),
        CONSTRAINT FK_EmployeeSchedules_WorkSchedules FOREIGN KEY (WorkScheduleId) REFERENCES WorkSchedules(Id),
        CONSTRAINT UQ_EmployeeSchedules_EmployeeDate UNIQUE (EmployeeId, [Date])
    );
END
GO

-- =============================================
-- Attendance - Clock in/out records
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Attendance')
BEGIN
    CREATE TABLE Attendance (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        EmployeeId      UNIQUEIDENTIFIER NOT NULL,
        [Date]          DATE             NOT NULL,
        ClockIn         DATETIME2        NULL,
        ClockOut        DATETIME2        NULL,
        WorkedHours     DECIMAL(5,2)     NULL,
        OvertimeHours   DECIMAL(5,2)     NOT NULL DEFAULT 0,
        LateMinutes     INT              NOT NULL DEFAULT 0,
        EarlyLeaveMinutes INT            NOT NULL DEFAULT 0,
        Status          NVARCHAR(50)     NOT NULL DEFAULT 'Present',
        Source          NVARCHAR(50)     NOT NULL DEFAULT 'Manual',
        Notes           NVARCHAR(500)    NULL,
        ApprovedBy      NVARCHAR(200)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_Attendance PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Attendance_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(Id),
        CONSTRAINT UQ_Attendance_EmployeeDate UNIQUE (EmployeeId, [Date])
    );

    CREATE NONCLUSTERED INDEX IX_Attendance_Date ON Attendance ([Date]) INCLUDE (EmployeeId, Status);
END
GO

-- =============================================
-- LeaveTypes - Types of leave (Annual, Sick, Personal, etc.)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeaveTypes')
BEGIN
    CREATE TABLE LeaveTypes (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Code            NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(200)    NOT NULL,
        NameThai        NVARCHAR(200)    NULL,
        DefaultDaysPerYear DECIMAL(5,1)  NOT NULL DEFAULT 0,
        IsPaid          BIT              NOT NULL DEFAULT 1,
        RequiresApproval BIT             NOT NULL DEFAULT 1,
        AllowCarryOver  BIT              NOT NULL DEFAULT 0,
        MaxCarryOverDays DECIMAL(5,1)    NOT NULL DEFAULT 0,
        IsActive        BIT              NOT NULL DEFAULT 1,
        SortOrder       INT              NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_LeaveTypes PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_LeaveTypes_Code UNIQUE (Code)
    );
END
GO

-- =============================================
-- LeaveBalances - Annual leave balance per employee per type
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeaveBalances')
BEGIN
    CREATE TABLE LeaveBalances (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        EmployeeId      UNIQUEIDENTIFIER NOT NULL,
        LeaveTypeId     UNIQUEIDENTIFIER NOT NULL,
        [Year]          INT              NOT NULL,
        Entitled        DECIMAL(5,1)     NOT NULL DEFAULT 0,
        CarriedOver     DECIMAL(5,1)     NOT NULL DEFAULT 0,
        Used            DECIMAL(5,1)     NOT NULL DEFAULT 0,
        Remaining       AS (Entitled + CarriedOver - Used) PERSISTED,
        UpdatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_LeaveBalances PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_LeaveBalances_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(Id),
        CONSTRAINT FK_LeaveBalances_LeaveTypes FOREIGN KEY (LeaveTypeId) REFERENCES LeaveTypes(Id),
        CONSTRAINT UQ_LeaveBalances_Unique UNIQUE (EmployeeId, LeaveTypeId, [Year])
    );
END
GO

-- =============================================
-- LeaveRequests - Leave applications
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeaveRequests')
BEGIN
    CREATE TABLE LeaveRequests (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        EmployeeId      UNIQUEIDENTIFIER NOT NULL,
        LeaveTypeId     UNIQUEIDENTIFIER NOT NULL,
        StartDate       DATE             NOT NULL,
        EndDate         DATE             NOT NULL,
        TotalDays       DECIMAL(5,1)     NOT NULL,
        IsHalfDay       BIT              NOT NULL DEFAULT 0,
        HalfDayPeriod   NVARCHAR(20)     NULL,
        Reason          NVARCHAR(500)    NULL,
        Status          NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
        ApprovedBy      NVARCHAR(200)    NULL,
        ApprovedAt      DATETIME2        NULL,
        RejectionReason NVARCHAR(500)    NULL,
        AttachmentUrl   NVARCHAR(1000)   NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_LeaveRequests PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_LeaveRequests_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(Id),
        CONSTRAINT FK_LeaveRequests_LeaveTypes FOREIGN KEY (LeaveTypeId) REFERENCES LeaveTypes(Id)
    );

    CREATE NONCLUSTERED INDEX IX_LeaveRequests_EmployeeId ON LeaveRequests (EmployeeId, Status);
    CREATE NONCLUSTERED INDEX IX_LeaveRequests_Dates ON LeaveRequests (StartDate, EndDate) INCLUDE (EmployeeId, Status);
END
GO

-- =============================================
-- PayrollPeriods - Monthly payroll runs
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PayrollPeriods')
BEGIN
    CREATE TABLE PayrollPeriods (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        PeriodCode      NVARCHAR(20)     NOT NULL,
        [Year]          INT              NOT NULL,
        [Month]         INT              NOT NULL,
        StartDate       DATE             NOT NULL,
        EndDate         DATE             NOT NULL,
        Status          NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
        TotalGrossPay   DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalDeductions DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalNetPay     DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalSocialSecurity DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalWithholdingTax DECIMAL(18,2) NOT NULL DEFAULT 0,
        ApprovedBy      NVARCHAR(200)    NULL,
        ApprovedAt      DATETIME2        NULL,
        PaidAt          DATETIME2        NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        CONSTRAINT PK_PayrollPeriods PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_PayrollPeriods_Code UNIQUE (PeriodCode)
    );
END
GO

-- =============================================
-- PayrollItems - Individual payslips per employee per period
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PayrollItems')
BEGIN
    CREATE TABLE PayrollItems (
        Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        PayrollPeriodId         UNIQUEIDENTIFIER NOT NULL,
        EmployeeId              UNIQUEIDENTIFIER NOT NULL,
        BaseSalary              DECIMAL(18,2)    NOT NULL DEFAULT 0,
        OvertimePay             DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Allowances              DECIMAL(18,2)    NOT NULL DEFAULT 0,
        ServiceCharge           DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Bonus                   DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Commission              DECIMAL(18,2)    NOT NULL DEFAULT 0,
        OtherIncome             DECIMAL(18,2)    NOT NULL DEFAULT 0,
        GrossPay                DECIMAL(18,2)    NOT NULL DEFAULT 0,
        SocialSecurityEmployee  DECIMAL(18,2)    NOT NULL DEFAULT 0,
        SocialSecurityEmployer  DECIMAL(18,2)    NOT NULL DEFAULT 0,
        WithholdingTax          DECIMAL(18,2)    NOT NULL DEFAULT 0,
        ProvidentFund           DECIMAL(18,2)    NOT NULL DEFAULT 0,
        LateDeduction           DECIMAL(18,2)    NOT NULL DEFAULT 0,
        AbsenceDeduction        DECIMAL(18,2)    NOT NULL DEFAULT 0,
        OtherDeductions         DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalDeductions         DECIMAL(18,2)    NOT NULL DEFAULT 0,
        NetPay                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
        PaymentMethod           NVARCHAR(50)     NOT NULL DEFAULT 'BankTransfer',
        IsPaid                  BIT              NOT NULL DEFAULT 0,
        PaidAt                  DATETIME2        NULL,
        CreatedAt               DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PayrollItems PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_PayrollItems_Periods FOREIGN KEY (PayrollPeriodId) REFERENCES PayrollPeriods(Id),
        CONSTRAINT FK_PayrollItems_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(Id),
        CONSTRAINT UQ_PayrollItems_Unique UNIQUE (PayrollPeriodId, EmployeeId)
    );
END
GO
