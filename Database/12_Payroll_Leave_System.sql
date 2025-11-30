-- ============================================================================
-- Payroll and Leave Management System
-- Version: 1.0
-- Date: 2025-01-30
-- Description: Complete payroll and leave management with OT tracking
-- ============================================================================

USE TakeTime;
GO

PRINT '============================================================================';
PRINT 'Starting Payroll and Leave Management System Installation';
PRINT '============================================================================';
GO

-- ============================================================================
-- PART 1: LEAVE MANAGEMENT SYSTEM
-- ============================================================================

-- Table 1: Leave Types Configuration
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Leave_Types]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Leave_Types] (
        [ID] TINYINT IDENTITY(1,1) PRIMARY KEY,
        [LeaveTypeName] NVARCHAR(100) NOT NULL,
        [LeaveTypeCode] VARCHAR(20) NOT NULL UNIQUE,
        [Description] NVARCHAR(500),
        [DeductSalary] BIT NOT NULL DEFAULT 0, -- หักเงินหรือไม่
        [RequiresMedicalCert] BIT NOT NULL DEFAULT 0, -- ต้องใบรับรองแพทย์หรือไม่
        [AnnualQuota] DECIMAL(5,2) DEFAULT 0, -- สิทธิต่อปี (รองรับครึ่งวัน)
        [RequiresApproval] BIT NOT NULL DEFAULT 1,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [DisplayOrder] TINYINT NOT NULL DEFAULT 0,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [CreatedBy_AdminID] SMALLINT,

        CONSTRAINT [FK_LeaveTypes_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_LeaveTypes_Active] ON [dbo].[Leave_Types]([IsActive]);

    PRINT '✅ Table Leave_Types created';

    -- Insert default leave types
    INSERT INTO [dbo].[Leave_Types] ([LeaveTypeName], [LeaveTypeCode], [Description], [DeductSalary], [RequiresMedicalCert], [AnnualQuota], [DisplayOrder])
    VALUES
        (N'ลาป่วย', 'SICK', N'ลาป่วยพร้อมใบรับรองแพทย์ (ไม่หักเงิน)', 0, 1, 30, 1),
        (N'ลากิจ', 'PERSONAL', N'ลากิจส่วนตัว (ไม่หักเงินถ้าไม่เกินสิทธิ)', 0, 0, 6, 2),
        (N'ลาหยุดหักเงิน', 'UNPAID', N'ลาหยุดที่หักเงินเดือน', 1, 0, 0, 3),
        (N'ลาพักร้อน', 'VACATION', N'ลาพักร้อนประจำปี', 0, 0, 10, 4),
        (N'ลาคลอด', 'MATERNITY', N'ลาคลอดบุตร', 0, 0, 90, 5);

    PRINT '✅ Default leave types inserted';
END
ELSE
    PRINT '⚠️  Table Leave_Types already exists';
GO

-- Table 2: Employee Leave Quota (สิทธิการลาของพนักงาน)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Leave_Quota]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Leave_Quota] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL,
        [Year] SMALLINT NOT NULL,
        [LeaveType_ID] TINYINT NOT NULL,
        [TotalDays] DECIMAL(5,2) NOT NULL DEFAULT 0, -- สิทธิทั้งหมด
        [UsedDays] DECIMAL(5,2) NOT NULL DEFAULT 0, -- ใช้ไปแล้ว
        [RemainingDays] AS ([TotalDays] - [UsedDays]) PERSISTED, -- คงเหลือ
        [CarryForwardDays] DECIMAL(5,2) DEFAULT 0, -- ยกมาจากปีก่อน
        [Notes] NVARCHAR(500),
        [LastUpdated] DATETIME DEFAULT GETDATE(),

        CONSTRAINT [FK_LeaveQuota_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_LeaveQuota_LeaveType] FOREIGN KEY ([LeaveType_ID])
            REFERENCES [dbo].[Leave_Types]([ID]),
        CONSTRAINT [UQ_LeaveQuota_AdminYearType] UNIQUE ([Admin_ID], [Year], [LeaveType_ID])
    );

    CREATE INDEX [IDX_LeaveQuota_Admin] ON [dbo].[Employee_Leave_Quota]([Admin_ID]);
    CREATE INDEX [IDX_LeaveQuota_Year] ON [dbo].[Employee_Leave_Quota]([Year]);

    PRINT '✅ Table Employee_Leave_Quota created';
END
ELSE
    PRINT '⚠️  Table Employee_Leave_Quota already exists';
GO

-- Table 3: Leave Requests (การลา)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Leave_Requests]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Leave_Requests] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [RequestNumber] NVARCHAR(20) NOT NULL UNIQUE, -- LV-YYYYMM-0001
        [Admin_ID] SMALLINT NOT NULL, -- พนักงานที่ลา
        [LeaveType_ID] TINYINT NOT NULL,

        -- ช่วงเวลา
        [StartDate] DATE NOT NULL,
        [EndDate] DATE NOT NULL,
        [TotalDays] DECIMAL(5,2) NOT NULL, -- รองรับครึ่งวัน
        [IsHalfDay] BIT NOT NULL DEFAULT 0,
        [HalfDayPeriod] VARCHAR(10), -- MORNING, AFTERNOON

        -- เหตุผลและเอกสาร
        [Reason] NVARCHAR(1000) NOT NULL,
        [MedicalCertPath] NVARCHAR(500), -- path to uploaded file
        [AttachmentPath] NVARCHAR(500),

        -- การหักเงิน
        [DeductSalary] BIT NOT NULL DEFAULT 0,
        [DeductionAmount] DECIMAL(10,2) DEFAULT 0, -- ยอดเงินที่หัก
        [BaseSalaryUsed] DECIMAL(10,2), -- เงินเดือนที่ใช้คำนวณ

        -- สถานะ
        [Status] VARCHAR(20) NOT NULL DEFAULT 'PENDING', -- PENDING, APPROVED, REJECTED, CANCELLED
        [RequestDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [ApprovedBy_AdminID] SMALLINT,
        [ApprovedDate] DATETIME,
        [RejectedReason] NVARCHAR(500),

        -- ผู้บันทึก
        [CreatedBy_AdminID] SMALLINT NOT NULL, -- ผู้จัดการกรอกให้ หรือ พนักงานกรอกเอง
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [UpdatedDate] DATETIME,
        [Notes] NVARCHAR(1000),

        CONSTRAINT [FK_LeaveRequest_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_LeaveRequest_LeaveType] FOREIGN KEY ([LeaveType_ID])
            REFERENCES [dbo].[Leave_Types]([ID]),
        CONSTRAINT [FK_LeaveRequest_ApprovedBy] FOREIGN KEY ([ApprovedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_LeaveRequest_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [CHK_LeaveRequest_EndDate] CHECK ([EndDate] >= [StartDate])
    );

    CREATE INDEX [IDX_LeaveRequest_Admin] ON [dbo].[Leave_Requests]([Admin_ID]);
    CREATE INDEX [IDX_LeaveRequest_Status] ON [dbo].[Leave_Requests]([Status]);
    CREATE INDEX [IDX_LeaveRequest_Dates] ON [dbo].[Leave_Requests]([StartDate], [EndDate]);
    CREATE INDEX [IDX_LeaveRequest_RequestNumber] ON [dbo].[Leave_Requests]([RequestNumber]);

    PRINT '✅ Table Leave_Requests created';
END
ELSE
    PRINT '⚠️  Table Leave_Requests already exists';
GO

-- ============================================================================
-- PART 2: PAYROLL SYSTEM
-- ============================================================================

-- Table 4: Employee Salary (เงินเดือนพนักงาน)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Salary]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Salary] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL,
        [MonthlySalary] DECIMAL(10,2) NOT NULL,
        [Position] NVARCHAR(100),
        [EffectiveDate] DATE NOT NULL,
        [EndDate] DATE,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [Remarks] NVARCHAR(500),
        [CreatedBy_AdminID] SMALLINT,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [UpdatedDate] DATETIME,

        CONSTRAINT [FK_EmpSalary_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_EmpSalary_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_EmpSalary_Admin] ON [dbo].[Employee_Salary]([Admin_ID]);
    CREATE INDEX [IDX_EmpSalary_Active] ON [dbo].[Employee_Salary]([IsActive]);

    PRINT '✅ Table Employee_Salary created';
END
ELSE
    PRINT '⚠️  Table Employee_Salary already exists';
GO

-- Table 5: Payroll Periods (งวดเงินเดือน)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Payroll_Periods]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Payroll_Periods] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [PeriodCode] VARCHAR(20) NOT NULL UNIQUE, -- PAYROLL-YYYYMM
        [Year] SMALLINT NOT NULL,
        [Month] TINYINT NOT NULL CHECK ([Month] BETWEEN 1 AND 12),
        [PeriodName] NVARCHAR(100), -- เดือน มกราคม 2568
        [PayrollDate] DATE, -- วันที่จ่าย
        [DaysInMonth] TINYINT, -- จำนวนวันในเดือน (สำหรับคำนวณ)
        [Status] VARCHAR(20) NOT NULL DEFAULT 'DRAFT', -- DRAFT, PROCESSING, COMPLETED, CLOSED
        [TotalEmployees] INT DEFAULT 0,
        [TotalGrossPay] DECIMAL(15,2) DEFAULT 0,
        [TotalDeductions] DECIMAL(15,2) DEFAULT 0,
        [TotalNetPay] DECIMAL(15,2) DEFAULT 0,
        [CreatedBy_AdminID] SMALLINT,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [ProcessedBy_AdminID] SMALLINT,
        [ProcessedDate] DATETIME,
        [ClosedBy_AdminID] SMALLINT,
        [ClosedDate] DATETIME,
        [Notes] NVARCHAR(1000),

        CONSTRAINT [FK_PayrollPeriod_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_PayrollPeriod_ProcessedBy] FOREIGN KEY ([ProcessedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_PayrollPeriod_ClosedBy] FOREIGN KEY ([ClosedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [UQ_PayrollPeriod_YearMonth] UNIQUE ([Year], [Month])
    );

    CREATE INDEX [IDX_PayrollPeriod_Status] ON [dbo].[Payroll_Periods]([Status]);
    CREATE INDEX [IDX_PayrollPeriod_YearMonth] ON [dbo].[Payroll_Periods]([Year], [Month]);

    PRINT '✅ Table Payroll_Periods created';
END
ELSE
    PRINT '⚠️  Table Payroll_Periods already exists';
GO

-- Table 6: Payroll Records (รายการเงินเดือนแต่ละคน)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Payroll_Records]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Payroll_Records] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [PayrollPeriod_ID] INT NOT NULL,
        [Admin_ID] SMALLINT NOT NULL,
        [EmployeeName] NVARCHAR(200),

        -- รายได้
        [BaseSalary] DECIMAL(10,2) NOT NULL,
        [OTHours] DECIMAL(5,2) DEFAULT 0,
        [OTRate] DECIMAL(10,2) DEFAULT 0, -- อัตราค่าโอทีต่อชั่วโมง
        [OTAmount] DECIMAL(10,2) DEFAULT 0, -- รายได้พิเศษ
        [BonusAmount] DECIMAL(10,2) DEFAULT 0,
        [AllowanceAmount] DECIMAL(10,2) DEFAULT 0,
        [OtherEarnings] DECIMAL(10,2) DEFAULT 0,
        [TotalEarnings] AS ([BaseSalary] + [OTAmount] + [BonusAmount] + [AllowanceAmount] + [OtherEarnings]) PERSISTED,

        -- รายการหัก
        [LeaveDeduction] DECIMAL(10,2) DEFAULT 0, -- หักจากการลา
        [LateDeduction] DECIMAL(10,2) DEFAULT 0,
        [SocialSecurity] DECIMAL(10,2) DEFAULT 0,
        [Tax] DECIMAL(10,2) DEFAULT 0,
        [OtherDeductions] DECIMAL(10,2) DEFAULT 0,
        [TotalDeductions] AS ([LeaveDeduction] + [LateDeduction] + [SocialSecurity] + [Tax] + [OtherDeductions]) PERSISTED,

        -- สรุป
        [NetSalary] AS ([BaseSalary] + [OTAmount] + [BonusAmount] + [AllowanceAmount] + [OtherEarnings] -
                        [LeaveDeduction] - [LateDeduction] - [SocialSecurity] - [Tax] - [OtherDeductions]) PERSISTED,

        -- การสร้างใบสำคัญจ่าย
        [VoucherGenerated] BIT NOT NULL DEFAULT 0,
        [VoucherGeneratedDate] DATETIME,
        [VoucherGeneratedBy_AdminID] SMALLINT,

        -- รายละเอียดเพิ่มเติม
        [WorkDays] DECIMAL(5,2), -- วันทำงานจริง
        [LeaveDays] DECIMAL(5,2), -- วันลา
        [Notes] NVARCHAR(1000),
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [UpdatedDate] DATETIME,

        CONSTRAINT [FK_PayrollRecord_Period] FOREIGN KEY ([PayrollPeriod_ID])
            REFERENCES [dbo].[Payroll_Periods]([ID]) ON DELETE CASCADE,
        CONSTRAINT [FK_PayrollRecord_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_PayrollRecord_VoucherBy] FOREIGN KEY ([VoucherGeneratedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [UQ_PayrollRecord_PeriodAdmin] UNIQUE ([PayrollPeriod_ID], [Admin_ID])
    );

    CREATE INDEX [IDX_PayrollRecord_Period] ON [dbo].[Payroll_Records]([PayrollPeriod_ID]);
    CREATE INDEX [IDX_PayrollRecord_Admin] ON [dbo].[Payroll_Records]([Admin_ID]);
    CREATE INDEX [IDX_PayrollRecord_Voucher] ON [dbo].[Payroll_Records]([VoucherGenerated]);

    PRINT '✅ Table Payroll_Records created';
END
ELSE
    PRINT '⚠️  Table Payroll_Records already exists';
GO

-- Table 7: Payroll OT Details (บันทึกรายละเอียดโอที)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Payroll_OT_Details]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Payroll_OT_Details] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [PayrollRecord_ID] BIGINT NOT NULL,
        [OTDate] DATE NOT NULL,
        [OTHours] DECIMAL(5,2) NOT NULL,
        [OTRate] DECIMAL(10,2) NOT NULL,
        [OTAmount] DECIMAL(10,2) NOT NULL,
        [Description] NVARCHAR(500),
        [RecordedBy_AdminID] SMALLINT,
        [RecordedDate] DATETIME DEFAULT GETDATE(),

        CONSTRAINT [FK_OTDetail_PayrollRecord] FOREIGN KEY ([PayrollRecord_ID])
            REFERENCES [dbo].[Payroll_Records]([ID]) ON DELETE CASCADE,
        CONSTRAINT [FK_OTDetail_RecordedBy] FOREIGN KEY ([RecordedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_OTDetail_PayrollRecord] ON [dbo].[Payroll_OT_Details]([PayrollRecord_ID]);

    PRINT '✅ Table Payroll_OT_Details created';
END
ELSE
    PRINT '⚠️  Table Payroll_OT_Details already exists';
GO

PRINT '============================================================================';
PRINT 'Payroll and Leave Management System Installation Completed Successfully!';
PRINT '============================================================================';
GO
