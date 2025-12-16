-- ============================================================================
-- Human Resources Management System
-- Version: 1.0
-- Date: 2025-01-30
-- Description: Complete HR system for employee management
-- ============================================================================

USE TakeTime;
GO

PRINT '============================================================================';
PRINT 'Starting Human Resources Management System Installation';
PRINT '============================================================================';
GO

-- ============================================================================
-- PART 1: EMPLOYEE PROFILE & PERSONAL INFORMATION
-- ============================================================================

-- Table 1: Employee Detailed Profile (ขยายข้อมูลจาก Admin table)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Profile]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Profile] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL UNIQUE,

        -- ข้อมูลส่วนตัวเพิ่มเติม
        [FullNameTH] NVARCHAR(200),
        [FullNameEN] NVARCHAR(200),
        [NickName] NVARCHAR(50),
        [BirthDate] DATE,
        [Age] AS (DATEDIFF(YEAR, [BirthDate], GETDATE()) -
                  CASE WHEN MONTH([BirthDate]) > MONTH(GETDATE()) OR
                       (MONTH([BirthDate]) = MONTH(GETDATE()) AND DAY([BirthDate]) > DAY(GETDATE()))
                  THEN 1 ELSE 0 END),
        [Gender] VARCHAR(10), -- MALE, FEMALE, OTHER
        [Nationality] NVARCHAR(50) DEFAULT N'ไทย',
        [Religion] NVARCHAR(50),
        [BloodType] VARCHAR(5),
        [MaritalStatus] VARCHAR(20), -- SINGLE, MARRIED, DIVORCED, WIDOWED

        -- บัตรประชาชน
        [IDCardNumber] VARCHAR(13),
        [IDCardIssueDate] DATE,
        [IDCardExpiryDate] DATE,
        [IDCardIssuedBy] NVARCHAR(200),

        -- ที่อยู่
        [AddressCurrent] NVARCHAR(500),
        [AddressCurrentDistrict] NVARCHAR(100),
        [AddressCurrentProvince] NVARCHAR(100),
        [AddressCurrentPostalCode] VARCHAR(10),
        [AddressRegistered] NVARCHAR(500),
        [AddressRegisteredDistrict] NVARCHAR(100),
        [AddressRegisteredProvince] NVARCHAR(100),
        [AddressRegisteredPostalCode] VARCHAR(10),
        [IsSameAddress] BIT DEFAULT 0,

        -- ข้อมูลติดต่อ
        [PersonalEmail] NVARCHAR(100),
        [WorkEmail] NVARCHAR(100),
        [MobilePhone] VARCHAR(20),
        [HomePhone] VARCHAR(20),
        [LineID] NVARCHAR(50),
        [FacebookProfile] NVARCHAR(200),

        -- ข้อมูลภาพถ่าย
        [PhotoPath] NVARCHAR(500),
        [PhotoUploadDate] DATETIME,

        -- วันที่บันทึก
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [UpdatedDate] DATETIME,
        [UpdatedBy_AdminID] SMALLINT,

        CONSTRAINT [FK_EmpProfile_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_EmpProfile_UpdatedBy] FOREIGN KEY ([UpdatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_EmpProfile_Admin] ON [dbo].[Employee_Profile]([Admin_ID]);
    CREATE INDEX [IDX_EmpProfile_IDCard] ON [dbo].[Employee_Profile]([IDCardNumber]);

    PRINT '✅ Table Employee_Profile created';
END
ELSE
    PRINT '⚠️  Table Employee_Profile already exists';
GO

-- Table 2: Employment History (ประวัติการจ้างงาน)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Employment_History]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Employment_History] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL,

        -- รายละเอียดการจ้าง
        [EventType] VARCHAR(30) NOT NULL, -- HIRED, PROMOTION, TRANSFER, SALARY_CHANGE, TERMINATION, RESIGNATION
        [EventDate] DATE NOT NULL,
        [PositionBefore] NVARCHAR(100),
        [PositionAfter] NVARCHAR(100),
        [DepartmentBefore] NVARCHAR(100),
        [DepartmentAfter] NVARCHAR(100),
        [SalaryBefore] DECIMAL(10,2),
        [SalaryAfter] DECIMAL(10,2),

        -- เหตุผล
        [Reason] NVARCHAR(1000),
        [Notes] NVARCHAR(1000),

        -- สำหรับการออก
        [TerminationReason] NVARCHAR(1000),
        [IsEligibleForRehire] BIT,
        [ExitInterviewCompleted] BIT DEFAULT 0,
        [ExitInterviewNotes] NVARCHAR(MAX),

        -- เอกสารแนบ
        [DocumentPath] NVARCHAR(500),

        -- ผู้บันทึก
        [RecordedBy_AdminID] SMALLINT NOT NULL,
        [RecordedDate] DATETIME DEFAULT GETDATE(),

        CONSTRAINT [FK_EmpHistory_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_EmpHistory_RecordedBy] FOREIGN KEY ([RecordedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_EmpHistory_Admin] ON [dbo].[Employee_Employment_History]([Admin_ID]);
    CREATE INDEX [IDX_EmpHistory_EventDate] ON [dbo].[Employee_Employment_History]([EventDate]);
    CREATE INDEX [IDX_EmpHistory_EventType] ON [dbo].[Employee_Employment_History]([EventType]);

    PRINT '✅ Table Employee_Employment_History created';
END
ELSE
    PRINT '⚠️  Table Employee_Employment_History already exists';
GO

-- Table 3: Employee Documents (เอกสารพนักงาน)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Documents]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Documents] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL,

        -- ประเภทเอกสาร
        [DocumentType] VARCHAR(50) NOT NULL, -- CONTRACT, ID_CARD, CERTIFICATE, DIPLOMA, TRANSCRIPT, MEDICAL, PHOTO, OTHER
        [DocumentName] NVARCHAR(200) NOT NULL,
        [DocumentNumber] NVARCHAR(100),
        [Description] NVARCHAR(500),

        -- ไฟล์
        [FilePath] NVARCHAR(500) NOT NULL,
        [FileSize] BIGINT, -- in bytes
        [FileType] VARCHAR(50), -- PDF, JPG, PNG, DOCX, etc.
        [OriginalFileName] NVARCHAR(200),

        -- วันที่เอกสาร
        [IssueDate] DATE,
        [ExpiryDate] DATE,
        [IsExpirable] BIT DEFAULT 0,
        [DaysUntilExpiry] AS (DATEDIFF(DAY, GETDATE(), [ExpiryDate])),

        -- สถานะ
        [IsActive] BIT DEFAULT 1,
        [IsConfidential] BIT DEFAULT 0,
        [RequiresApproval] BIT DEFAULT 0,
        [ApprovalStatus] VARCHAR(20) DEFAULT 'PENDING', -- PENDING, APPROVED, REJECTED
        [ApprovedBy_AdminID] SMALLINT,
        [ApprovedDate] DATETIME,

        -- Tags สำหรับค้นหา
        [Tags] NVARCHAR(500),

        -- การอัพโหลด
        [UploadedBy_AdminID] SMALLINT NOT NULL,
        [UploadedDate] DATETIME DEFAULT GETDATE(),
        [UpdatedDate] DATETIME,
        [Notes] NVARCHAR(1000),

        CONSTRAINT [FK_EmpDoc_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_EmpDoc_UploadedBy] FOREIGN KEY ([UploadedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_EmpDoc_ApprovedBy] FOREIGN KEY ([ApprovedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_EmpDoc_Admin] ON [dbo].[Employee_Documents]([Admin_ID]);
    CREATE INDEX [IDX_EmpDoc_Type] ON [dbo].[Employee_Documents]([DocumentType]);
    CREATE INDEX [IDX_EmpDoc_Expiry] ON [dbo].[Employee_Documents]([ExpiryDate]) WHERE [IsExpirable] = 1;
    CREATE INDEX [IDX_EmpDoc_Active] ON [dbo].[Employee_Documents]([IsActive]);

    PRINT '✅ Table Employee_Documents created';
END
ELSE
    PRINT '⚠️  Table Employee_Documents already exists';
GO

-- Table 4: Education History (ประวัติการศึกษา)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Education]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Education] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL,

        -- ระดับการศึกษา
        [EducationLevel] VARCHAR(30) NOT NULL, -- PRIMARY, SECONDARY, HIGH_SCHOOL, VOCATIONAL, DIPLOMA, BACHELOR, MASTER, DOCTORATE
        [InstitutionName] NVARCHAR(300) NOT NULL,
        [Faculty] NVARCHAR(200),
        [Major] NVARCHAR(200),
        [Degree] NVARCHAR(200),
        [GPA] DECIMAL(3,2),
        [GPAScale] DECIMAL(3,2) DEFAULT 4.00,

        -- ช่วงเวลา
        [StartDate] DATE,
        [EndDate] DATE,
        [IsGraduated] BIT DEFAULT 1,
        [GraduationDate] DATE,

        -- เอกสารแนบ
        [CertificatePath] NVARCHAR(500),
        [TranscriptPath] NVARCHAR(500),

        -- อื่นๆ
        [Country] NVARCHAR(100) DEFAULT N'ไทย',
        [Honors] NVARCHAR(500),
        [Activities] NVARCHAR(1000),
        [Notes] NVARCHAR(1000),

        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [UpdatedDate] DATETIME,

        CONSTRAINT [FK_EmpEdu_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_EmpEdu_Admin] ON [dbo].[Employee_Education]([Admin_ID]);
    CREATE INDEX [IDX_EmpEdu_Level] ON [dbo].[Employee_Education]([EducationLevel]);

    PRINT '✅ Table Employee_Education created';
END
ELSE
    PRINT '⚠️  Table Employee_Education already exists';
GO

-- Table 5: Work Experience (ประสบการณ์การทำงาน)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Work_Experience]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Work_Experience] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL,

        -- บริษัท
        [CompanyName] NVARCHAR(300) NOT NULL,
        [CompanyAddress] NVARCHAR(500),
        [CompanyPhone] VARCHAR(20),
        [CompanyIndustry] NVARCHAR(100),

        -- ตำแหน่งงาน
        [Position] NVARCHAR(200) NOT NULL,
        [Department] NVARCHAR(200),
        [JobDescription] NVARCHAR(MAX),
        [Responsibilities] NVARCHAR(MAX),

        -- เงินเดือน
        [StartingSalary] DECIMAL(10,2),
        [EndingSalary] DECIMAL(10,2),

        -- ช่วงเวลา
        [StartDate] DATE NOT NULL,
        [EndDate] DATE,
        [IsCurrentJob] BIT DEFAULT 0,
        [YearsOfExperience] AS (
            DATEDIFF(MONTH, [StartDate], ISNULL([EndDate], GETDATE())) / 12.0
        ),

        -- เหตุผลในการออก
        [ReasonForLeaving] NVARCHAR(1000),

        -- ผู้อ้างอิง
        [ReferenceName] NVARCHAR(200),
        [ReferencePosition] NVARCHAR(200),
        [ReferencePhone] VARCHAR(20),
        [ReferenceEmail] NVARCHAR(100),

        [Notes] NVARCHAR(1000),
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [UpdatedDate] DATETIME,

        CONSTRAINT [FK_EmpWorkExp_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_EmpWorkExp_Admin] ON [dbo].[Employee_Work_Experience]([Admin_ID]);

    PRINT '✅ Table Employee_Work_Experience created';
END
ELSE
    PRINT '⚠️  Table Employee_Work_Experience already exists';
GO

-- Table 6: Emergency Contacts (ผู้ติดต่อฉุกเฉิน)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Emergency_Contacts]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Emergency_Contacts] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL,

        -- ข้อมูลผู้ติดต่อ
        [ContactName] NVARCHAR(200) NOT NULL,
        [Relationship] NVARCHAR(50) NOT NULL, -- SPOUSE, PARENT, SIBLING, CHILD, FRIEND, OTHER
        [MobilePhone] VARCHAR(20) NOT NULL,
        [HomePhone] VARCHAR(20),
        [Email] NVARCHAR(100),
        [Address] NVARCHAR(500),

        -- ลำดับความสำคัญ
        [Priority] TINYINT DEFAULT 1, -- 1 = Primary, 2 = Secondary, etc.
        [IsPrimaryContact] BIT DEFAULT 0,

        [Notes] NVARCHAR(500),
        [IsActive] BIT DEFAULT 1,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [UpdatedDate] DATETIME,

        CONSTRAINT [FK_EmpEmergency_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_EmpEmergency_Admin] ON [dbo].[Employee_Emergency_Contacts]([Admin_ID]);
    CREATE INDEX [IDX_EmpEmergency_Priority] ON [dbo].[Employee_Emergency_Contacts]([Priority]);

    PRINT '✅ Table Employee_Emergency_Contacts created';
END
ELSE
    PRINT '⚠️  Table Employee_Emergency_Contacts already exists';
GO

-- Table 7: Family Information (ข้อมูลครอบครัว)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Family]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Family] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL,

        -- ข้อมูลสมาชิกครอบครัว
        [RelationshipType] VARCHAR(20) NOT NULL, -- SPOUSE, FATHER, MOTHER, CHILD, SIBLING
        [FullName] NVARCHAR(200) NOT NULL,
        [BirthDate] DATE,
        [Age] AS (DATEDIFF(YEAR, [BirthDate], GETDATE()) -
                  CASE WHEN MONTH([BirthDate]) > MONTH(GETDATE()) OR
                       (MONTH([BirthDate]) = MONTH(GETDATE()) AND DAY([BirthDate]) > DAY(GETDATE()))
                  THEN 1 ELSE 0 END),
        [IDCardNumber] VARCHAR(13),
        [Occupation] NVARCHAR(200),
        [Employer] NVARCHAR(300),
        [PhoneNumber] VARCHAR(20),

        -- ผู้พึ่งพา
        [IsDependent] BIT DEFAULT 0,
        [IsBeneficiary] BIT DEFAULT 0, -- ผู้รับผลประโยชน์
        [BeneficiaryPercentage] DECIMAL(5,2),

        -- สำหรับลูก
        [IsStudying] BIT DEFAULT 0,
        [School] NVARCHAR(300),
        [Grade] NVARCHAR(50),

        [Notes] NVARCHAR(500),
        [IsActive] BIT DEFAULT 1,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [UpdatedDate] DATETIME,

        CONSTRAINT [FK_EmpFamily_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_EmpFamily_Admin] ON [dbo].[Employee_Family]([Admin_ID]);
    CREATE INDEX [IDX_EmpFamily_Relationship] ON [dbo].[Employee_Family]([RelationshipType]);

    PRINT '✅ Table Employee_Family created';
END
ELSE
    PRINT '⚠️  Table Employee_Family already exists';
GO

-- Table 8: Training & Development (การอบรม)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Training]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Training] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL,

        -- ข้อมูลการอบรม
        [TrainingType] VARCHAR(30) NOT NULL, -- ORIENTATION, TECHNICAL, SAFETY, LEADERSHIP, COMPLIANCE, OTHER
        [TrainingName] NVARCHAR(300) NOT NULL,
        [TrainingProvider] NVARCHAR(300),
        [TrainingLocation] NVARCHAR(300),
        [Description] NVARCHAR(1000),

        -- วันที่
        [StartDate] DATE NOT NULL,
        [EndDate] DATE NOT NULL,
        [TotalHours] DECIMAL(5,2),
        [TotalDays] AS (DATEDIFF(DAY, [StartDate], [EndDate]) + 1) PERSISTED,

        -- ผลการอบรม
        [CompletionStatus] VARCHAR(20) DEFAULT 'REGISTERED', -- REGISTERED, COMPLETED, FAILED, CANCELLED
        [CompletionDate] DATE,
        [Score] DECIMAL(5,2),
        [Grade] VARCHAR(5),
        [IsPassed] BIT,
        [CertificateNumber] NVARCHAR(100),
        [CertificatePath] NVARCHAR(500),
        [CertificateExpiryDate] DATE,

        -- ค่าใช้จ่าย
        [TrainingCost] DECIMAL(10,2),
        [IsPaidByCompany] BIT DEFAULT 1,

        -- ผู้อนุมัติ
        [ApprovedBy_AdminID] SMALLINT,
        [ApprovedDate] DATETIME,

        [Notes] NVARCHAR(1000),
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [UpdatedDate] DATETIME,

        CONSTRAINT [FK_EmpTraining_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_EmpTraining_ApprovedBy] FOREIGN KEY ([ApprovedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_EmpTraining_Admin] ON [dbo].[Employee_Training]([Admin_ID]);
    CREATE INDEX [IDX_EmpTraining_Type] ON [dbo].[Employee_Training]([TrainingType]);
    CREATE INDEX [IDX_EmpTraining_Status] ON [dbo].[Employee_Training]([CompletionStatus]);

    PRINT '✅ Table Employee_Training created';
END
ELSE
    PRINT '⚠️  Table Employee_Training already exists';
GO

-- Table 9: Performance Reviews (ประเมินผลงาน)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Performance_Reviews]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Performance_Reviews] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL,

        -- ข้อมูลการประเมิน
        [ReviewType] VARCHAR(30) NOT NULL, -- PROBATION, ANNUAL, QUARTERLY, PROJECT_BASED, AD_HOC
        [ReviewPeriodStart] DATE NOT NULL,
        [ReviewPeriodEnd] DATE NOT NULL,
        [ReviewDate] DATE NOT NULL,

        -- คะแนนประเมิน (1-5)
        [WorkQualityScore] DECIMAL(3,2),
        [ProductivityScore] DECIMAL(3,2),
        [TeamworkScore] DECIMAL(3,2),
        [CommunicationScore] DECIMAL(3,2),
        [InitiativeScore] DECIMAL(3,2),
        [PunctualityScore] DECIMAL(3,2),
        [OverallScore] DECIMAL(3,2),
        [OverallRating] VARCHAR(20), -- OUTSTANDING, EXCEEDS, MEETS, NEEDS_IMPROVEMENT, UNSATISFACTORY

        -- ข้อเขียน
        [Strengths] NVARCHAR(MAX),
        [AreasForImprovement] NVARCHAR(MAX),
        [Goals] NVARCHAR(MAX),
        [EmployeeComments] NVARCHAR(MAX),
        [ManagerComments] NVARCHAR(MAX),

        -- ผลลัพธ์
        [RecommendedAction] VARCHAR(30), -- PROMOTION, SALARY_INCREASE, TRAINING, WARNING, NONE
        [SalaryAdjustment] DECIMAL(10,2),
        [BonusAmount] DECIMAL(10,2),

        -- ผู้ประเมิน
        [ReviewedBy_AdminID] SMALLINT NOT NULL,
        [ApprovedBy_AdminID] SMALLINT,
        [ApprovedDate] DATETIME,

        -- สถานะ
        [Status] VARCHAR(20) DEFAULT 'DRAFT', -- DRAFT, SUBMITTED, ACKNOWLEDGED, COMPLETED
        [EmployeeAcknowledgedDate] DATETIME,

        [DocumentPath] NVARCHAR(500),
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [UpdatedDate] DATETIME,

        CONSTRAINT [FK_EmpReview_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_EmpReview_ReviewedBy] FOREIGN KEY ([ReviewedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_EmpReview_ApprovedBy] FOREIGN KEY ([ApprovedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_EmpReview_Admin] ON [dbo].[Employee_Performance_Reviews]([Admin_ID]);
    CREATE INDEX [IDX_EmpReview_Type] ON [dbo].[Employee_Performance_Reviews]([ReviewType]);
    CREATE INDEX [IDX_EmpReview_Date] ON [dbo].[Employee_Performance_Reviews]([ReviewDate]);

    PRINT '✅ Table Employee_Performance_Reviews created';
END
ELSE
    PRINT '⚠️  Table Employee_Performance_Reviews already exists';
GO

-- Table 10: Employee Contracts (สัญญาจ้าง)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Contracts]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Contracts] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Admin_ID] SMALLINT NOT NULL,

        -- ข้อมูลสัญญา
        [ContractNumber] NVARCHAR(50) NOT NULL UNIQUE,
        [ContractType] VARCHAR(30) NOT NULL, -- PERMANENT, FIXED_TERM, PROBATION, CONTRACTOR, PART_TIME
        [ContractStatus] VARCHAR(20) DEFAULT 'ACTIVE', -- ACTIVE, EXPIRED, TERMINATED, RENEWED

        -- วันที่
        [StartDate] DATE NOT NULL,
        [EndDate] DATE,
        [SignedDate] DATE,
        [IsIndefinite] BIT DEFAULT 0, -- สัญญาไม่มีกำหนด
        [DaysUntilExpiry] AS (DATEDIFF(DAY, GETDATE(), [EndDate])),

        -- ข้อมูลตำแหน่ง
        [Position] NVARCHAR(200) NOT NULL,
        [Department] NVARCHAR(100),
        [WorkLocation] NVARCHAR(300),
        [ReportsTo_AdminID] SMALLINT,

        -- เงินเดือนและสวัสดิการ
        [Salary] DECIMAL(10,2) NOT NULL,
        [SalaryType] VARCHAR(20), -- MONTHLY, HOURLY, DAILY
        [BenefitsIncluded] NVARCHAR(1000),

        -- เอกสาร
        [ContractFilePath] NVARCHAR(500),
        [SignedCopyPath] NVARCHAR(500),

        -- การต่อสัญญา
        [IsRenewed] BIT DEFAULT 0,
        [RenewedTo_ContractID] INT,
        [RenewalNotes] NVARCHAR(1000),

        -- ผู้อนุมัติ
        [ApprovedBy_AdminID] SMALLINT,
        [ApprovedDate] DATETIME,

        [Notes] NVARCHAR(1000),
        [CreatedBy_AdminID] SMALLINT,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [UpdatedDate] DATETIME,

        CONSTRAINT [FK_EmpContract_Admin] FOREIGN KEY ([Admin_ID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_EmpContract_ReportsTo] FOREIGN KEY ([ReportsTo_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_EmpContract_ApprovedBy] FOREIGN KEY ([ApprovedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_EmpContract_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_EmpContract_RenewedTo] FOREIGN KEY ([RenewedTo_ContractID])
            REFERENCES [dbo].[Employee_Contracts]([ID])
    );

    CREATE INDEX [IDX_EmpContract_Admin] ON [dbo].[Employee_Contracts]([Admin_ID]);
    CREATE INDEX [IDX_EmpContract_Status] ON [dbo].[Employee_Contracts]([ContractStatus]);
    CREATE INDEX [IDX_EmpContract_Expiry] ON [dbo].[Employee_Contracts]([EndDate]) WHERE [IsIndefinite] = 0;

    PRINT '✅ Table Employee_Contracts created';
END
ELSE
    PRINT '⚠️  Table Employee_Contracts already exists';
GO

PRINT '============================================================================';
PRINT 'Human Resources Management System Installation Completed Successfully!';
PRINT '============================================================================';
PRINT 'Total Tables Created: 10';
PRINT '  - Employee_Profile';
PRINT '  - Employee_Employment_History';
PRINT '  - Employee_Documents';
PRINT '  - Employee_Education';
PRINT '  - Employee_Work_Experience';
PRINT '  - Employee_Emergency_Contacts';
PRINT '  - Employee_Family';
PRINT '  - Employee_Training';
PRINT '  - Employee_Performance_Reviews';
PRINT '  - Employee_Contracts';
PRINT '============================================================================';
GO
