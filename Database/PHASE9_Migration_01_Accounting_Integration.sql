-- ================================================================
-- PHASE 9: Accounting System Integration (Nexaacc)
-- Migration 01: Configuration, Queue, and Account Mapping tables
-- ================================================================
-- This migration creates the infrastructure needed to sync
-- TakeTime data automatically to the Nexaacc Accounting System.
-- ================================================================

-- ──────────────────────────────────────────────
-- 1. Integration Configuration Table
-- ──────────────────────────────────────────────

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounting_Integration_Config')
BEGIN
    CREATE TABLE Accounting_Integration_Config (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        ConfigKey NVARCHAR(100) NOT NULL,
        ConfigValue NVARCHAR(500) NOT NULL,
        Description NVARCHAR(255),
        Updated_Date DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_Accounting_Integration_Config_Key UNIQUE (ConfigKey)
    );

    PRINT 'Created table: Accounting_Integration_Config';
END
GO

-- Seed default configuration
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Enabled')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description) VALUES
    ('Nexaacc_BaseUrl',          '',       'Base URL of Nexaacc Accounting API (e.g., https://accounting.example.com)'),
    ('Nexaacc_ApiKey_Encrypted', '',       'Encrypted API Key for Nexaacc (use X-Api-Key header)'),
    ('Nexaacc_CompanyId',        '',       'Company GUID from Nexaacc system'),
    ('Nexaacc_Enabled',          'false',  'Enable/disable automatic sync (true/false)'),
    ('Nexaacc_SyncInterval_Sec', '30',     'Queue processing interval in seconds'),
    ('Nexaacc_MaxRetries',       '5',      'Maximum retry attempts for failed sync items'),
    ('Nexaacc_TimeoutSec',       '30',     'HTTP request timeout in seconds');

    PRINT 'Seeded default configuration';
END
GO

-- ──────────────────────────────────────────────
-- 2. Account Mapping Table
-- ──────────────────────────────────────────────

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounting_Account_Mapping')
BEGIN
    CREATE TABLE Accounting_Account_Mapping (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        TakeTime_Code NVARCHAR(50) NOT NULL,
        TakeTime_Description NVARCHAR(255),
        Nexaacc_AccountCode NVARCHAR(20) NOT NULL,
        Nexaacc_AccountId UNIQUEIDENTIFIER NULL,
        Mapping_Type NVARCHAR(50) NOT NULL,  -- PAYMENT_METHOD, REVENUE, EXPENSE, ASSET, LIABILITY
        Is_Active BIT DEFAULT 1,
        CONSTRAINT UQ_Accounting_Account_Mapping_Code UNIQUE (TakeTime_Code)
    );

    PRINT 'Created table: Accounting_Account_Mapping';
END
GO

-- Seed default account mappings (Nexaacc_AccountId will be populated after connecting)
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'CASH')
BEGIN
    INSERT INTO Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type) VALUES
    -- Assets (หมวด 1) — codes match NextAcc 5-digit chart of accounts
    ('CASH',              N'เงินสด',                            '11111', 'ASSET'),
    ('BANK_KBANK',        N'เงินฝากธนาคาร - กสิกรไทย',           '',      'ASSET'),
    ('BANK_KTB',          N'เงินฝากธนาคาร - กรุงไทย',            '',      'ASSET'),
    ('BANK_CARD',         N'เงินฝากธนาคาร - บัตรเครดิต',         '',      'ASSET'),
    ('DIRECTOR_ADVANCE',  N'ลูกหนี้กรรมการ/เงินทดรองกรรมการ',    '11330', 'ASSET'),
    ('ROOM_AR',           N'ลูกหนี้ค่าห้องพัก',                  '11310', 'ASSET'),
    ('INVENTORY',         N'สินค้าคงเหลือ',                     '11500', 'ASSET'),
    ('INPUT_VAT',         N'ภาษีซื้อ',                          '11610', 'ASSET'),

    -- Liabilities (หมวด 2) — รหัสต้อง Sync มาจาก NextAcc
    ('ACCOUNTS_PAYABLE',  N'เจ้าหนี้การค้า',                    '',      'LIABILITY'),
    ('ADVANCE_DEPOSIT',   N'เงินรับล่วงหน้า - มัดจำ',            '',      'LIABILITY'),
    ('OUTPUT_VAT',        N'ภาษีขาย',                           '',      'LIABILITY'),
    ('WHT_PAYABLE',       N'ภาษีหัก ณ ที่จ่าย ค้างจ่าย',         '',      'LIABILITY'),

    -- Revenue (หมวด 4) — รหัสต้อง Sync มาจาก NextAcc
    ('ROOM_REVENUE',      N'รายได้ค่าห้องพัก',                  '',      'REVENUE'),
    ('PRODUCT_REVENUE',   N'รายได้ขายสินค้า',                   '',      'REVENUE'),
    ('FB_REVENUE',        N'รายได้ค่าอาหารและเครื่องดื่ม',       '',      'REVENUE'),
    ('SERVICE_REVENUE',   N'รายได้ค่าบริการอื่น',               '',      'REVENUE'),
    ('OTHER_INCOME',      N'รายได้อื่น',                        '',      'REVENUE'),

    -- Expenses (หมวด 5) — รหัสต้อง Sync มาจาก NextAcc
    ('COGS',              N'ต้นทุนสินค้าขาย',                   '',      'EXPENSE'),
    ('SALARY_EXPENSE',    N'เงินเดือนและค่าแรง',                '',      'EXPENSE'),
    ('EXPENSE_UTILITY',   N'ค่าสาธารณูปโภค',                    '',      'EXPENSE'),
    ('EXPENSE_MAINTENANCE', N'ค่าซ่อมแซมบำรุงรักษา',            '',      'EXPENSE'),
    ('EXPENSE_SUPPLIES',  N'ค่าวัสดุสิ้นเปลือง',                '',      'EXPENSE'),
    ('EXPENSE_OTA',       N'ค่าคอมมิชชั่น OTA',                 '',      'EXPENSE'),
    ('EXPENSE_OTHER',     N'ค่าใช้จ่ายอื่น',                    '',      'EXPENSE'),
    ('SSF_EMPLOYER_EXPENSE', N'ประกันสังคมส่วนนายจ้าง',          '',      'EXPENSE');

    PRINT 'Seeded default account mappings';
END
GO

-- Add SSF Payable if missing
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'SSF_PAYABLE')
BEGIN
    INSERT INTO Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type) VALUES
    ('SSF_PAYABLE', N'ประกันสังคมค้างจ่าย', '', 'LIABILITY');
    PRINT 'Added SSF_PAYABLE account mapping';
END
GO

-- Add SSF Employer Expense if missing (for existing databases)
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'SSF_EMPLOYER_EXPENSE')
BEGIN
    INSERT INTO Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type) VALUES
    ('SSF_EMPLOYER_EXPENSE', N'ประกันสังคมส่วนนายจ้���ง', '', 'EXPENSE');
    PRINT 'Added SSF_EMPLOYER_EXPENSE account mapping';
END
GO

-- Add missing expense mapping for Payroll
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'EXPENSE_PAYROLL')
BEGIN
    INSERT INTO Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type) VALUES
    ('EXPENSE_PAYROLL', N'ค่าใช้จ่ายเงินเดือน', '', 'EXPENSE');
    PRINT 'Added EXPENSE_PAYROLL account mapping';
END
GO

-- Add missing expense mapping for Affiliate
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'EXPENSE_AFFILIATE')
BEGIN
    INSERT INTO Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type) VALUES
    ('EXPENSE_AFFILIATE', N'ค่าคอมมิชชั่น Affiliate', '', 'EXPENSE');
    PRINT 'Added EXPENSE_AFFILIATE account mapping';
END
GO

-- ──────────────────────────────────────────────
-- 3. Sync Queue Table
-- ──────────────────────────────────────────────

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounting_Sync_Queue')
BEGIN
    CREATE TABLE Accounting_Sync_Queue (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        Entity_Type NVARCHAR(50) NOT NULL,       -- RESERVATION, PAYMENT, VOUCHER, PRODUCT, RECEIPT, CREDIT_NOTE, PAYROLL
        Entity_ID INT NOT NULL,
        Action_Type NVARCHAR(50) NOT NULL,        -- CREATE_DEPOSIT_JOURNAL, CREATE_PAYMENT_JOURNAL, etc.
        Payload NVARCHAR(MAX),                    -- JSON payload with all data needed for API call
        Status NVARCHAR(20) DEFAULT 'PENDING',    -- PENDING, PROCESSING, COMPLETED, FAILED, SKIPPED
        Retry_Count INT DEFAULT 0,
        Max_Retries INT DEFAULT 5,
        Error_Message NVARCHAR(MAX),
        Nexaacc_Response_Id NVARCHAR(100),        -- GUID returned from Nexaacc after successful sync
        Created_Date DATETIME DEFAULT GETDATE(),
        Processed_Date DATETIME,
        Next_Retry_Date DATETIME
    );

    -- Indexes for queue processing performance
    CREATE NONCLUSTERED INDEX IX_SyncQueue_Status_NextRetry
        ON Accounting_Sync_Queue (Status, Next_Retry_Date)
        INCLUDE (Entity_Type, Action_Type, Retry_Count, Max_Retries);

    CREATE NONCLUSTERED INDEX IX_SyncQueue_Entity
        ON Accounting_Sync_Queue (Entity_Type, Entity_ID)
        INCLUDE (Status, Action_Type, Nexaacc_Response_Id);

    CREATE NONCLUSTERED INDEX IX_SyncQueue_CreatedDate
        ON Accounting_Sync_Queue (Created_Date DESC)
        INCLUDE (Status, Entity_Type);

    PRINT 'Created table: Accounting_Sync_Queue with indexes';
END
GO

-- ──────────────────────────────────────────────
-- 4. Sync Log Table (API call audit trail)
-- ──────────────────────────────────────────────

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounting_Sync_Log')
BEGIN
    CREATE TABLE Accounting_Sync_Log (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        Queue_ID BIGINT NULL REFERENCES Accounting_Sync_Queue(ID),
        Action NVARCHAR(200),
        Request_Payload NVARCHAR(MAX),
        Response_Payload NVARCHAR(MAX),
        HTTP_Status INT,
        Success BIT,
        Duration_Ms INT,
        Created_Date DATETIME DEFAULT GETDATE()
    );

    CREATE NONCLUSTERED INDEX IX_SyncLog_QueueId
        ON Accounting_Sync_Log (Queue_ID);

    CREATE NONCLUSTERED INDEX IX_SyncLog_CreatedDate
        ON Accounting_Sync_Log (Created_Date DESC)
        INCLUDE (Action, Success, HTTP_Status);

    PRINT 'Created table: Accounting_Sync_Log with indexes';
END
GO

-- ──────────────────────────────────────────────
-- 5. Useful Views for Monitoring
-- ──────────────────────────────────────────────

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Accounting_Sync_Summary')
    DROP VIEW vw_Accounting_Sync_Summary;
GO

CREATE VIEW vw_Accounting_Sync_Summary AS
SELECT
    CAST(Created_Date AS DATE) AS Sync_Date,
    Entity_Type,
    Status,
    COUNT(*) AS Item_Count,
    SUM(CASE WHEN Status = 'COMPLETED' THEN 1 ELSE 0 END) AS Completed_Count,
    SUM(CASE WHEN Status = 'FAILED' AND Retry_Count >= Max_Retries THEN 1 ELSE 0 END) AS Permanently_Failed_Count,
    AVG(DATEDIFF(SECOND, Created_Date, ISNULL(Processed_Date, GETDATE()))) AS Avg_Processing_Sec
FROM Accounting_Sync_Queue
GROUP BY CAST(Created_Date AS DATE), Entity_Type, Status;
GO

PRINT 'Created view: vw_Accounting_Sync_Summary';
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Accounting_Sync_Failed')
    DROP VIEW vw_Accounting_Sync_Failed;
GO

CREATE VIEW vw_Accounting_Sync_Failed AS
SELECT
    q.ID,
    q.Entity_Type,
    q.Entity_ID,
    q.Action_Type,
    q.Error_Message,
    q.Retry_Count,
    q.Max_Retries,
    q.Created_Date,
    q.Processed_Date,
    l.HTTP_Status AS Last_HTTP_Status,
    l.Duration_Ms AS Last_Duration_Ms
FROM Accounting_Sync_Queue q
OUTER APPLY (
    SELECT TOP 1 HTTP_Status, Duration_Ms
    FROM Accounting_Sync_Log
    WHERE Queue_ID = q.ID
    ORDER BY Created_Date DESC
) l
WHERE q.Status = 'FAILED' AND q.Retry_Count >= q.Max_Retries;
GO

PRINT 'Created view: vw_Accounting_Sync_Failed';
GO

-- ──────────────────────────────────────────────
-- 6. API Performance Summary View
-- ──────────────────────────────────────────────

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Accounting_Api_Performance')
    DROP VIEW vw_Accounting_Api_Performance;
GO

CREATE VIEW vw_Accounting_Api_Performance AS
SELECT
    CAST(Created_Date AS DATE) AS Api_Date,
    Action,
    COUNT(*) AS Total_Calls,
    SUM(CASE WHEN Success = 1 THEN 1 ELSE 0 END) AS Success_Count,
    SUM(CASE WHEN Success = 0 THEN 1 ELSE 0 END) AS Failed_Count,
    AVG(Duration_Ms) AS Avg_Duration_Ms,
    MAX(Duration_Ms) AS Max_Duration_Ms,
    MIN(Duration_Ms) AS Min_Duration_Ms
FROM Accounting_Sync_Log
GROUP BY CAST(Created_Date AS DATE), Action;
GO

PRINT 'Created view: vw_Accounting_Api_Performance';
GO

-- ────────────���─────────────────────────────────
-- 7. Payment Method → NextAcc Account Direct Link
-- ───────────────────────────────���──────────────

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Account_Paid_How' AND COLUMN_NAME = 'Nexaacc_AccountId')
BEGIN
    ALTER TABLE Account_Paid_How ADD Nexaacc_AccountId UNIQUEIDENTIFIER NULL;
    PRINT 'Added Nexaacc_AccountId column to Account_Paid_How';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Account_Paid_How' AND COLUMN_NAME = 'Nexaacc_AccountCode')
BEGIN
    ALTER TABLE Account_Paid_How ADD Nexaacc_AccountCode NVARCHAR(20) NULL;
    PRINT 'Added Nexaacc_AccountCode column to Account_Paid_How';
END
GO

-- ─────────────────────────────────────────��────
-- 8. Expense Category → NextAcc Account Direct Link
-- ─────────────────────────────────────────���────

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Account_Paid_Type' AND COLUMN_NAME = 'Nexaacc_AccountId')
BEGIN
    ALTER TABLE Account_Paid_Type ADD Nexaacc_AccountId UNIQUEIDENTIFIER NULL;
    PRINT 'Added Nexaacc_AccountId column to Account_Paid_Type';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Account_Paid_Type' AND COLUMN_NAME = 'Nexaacc_AccountCode')
BEGIN
    ALTER TABLE Account_Paid_Type ADD Nexaacc_AccountCode NVARCHAR(20) NULL;
    PRINT 'Added Nexaacc_AccountCode column to Account_Paid_Type';
END
GO

-- ────────────���─────────────────────────────���───
-- 9. Revenue Type mapping (for receipts)
-- ───��──────────────────────────────────────────

-- Add PAYMENT_METHOD entries to Accounting_Account_Mapping for payment methods that aren't already covered
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'BANK_BBL')
BEGIN
    INSERT INTO Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type) VALUES
    ('BANK_BBL', N'เงินฝากธนาคาร - กรุงเทพ', '', 'ASSET');
    PRINT 'Added BANK_BBL account mapping';
END
GO

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'BANK_SCB')
BEGIN
    INSERT INTO Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type) VALUES
    ('BANK_SCB', N'เงินฝากธ��าคาร - ไทยพาณิชย์', '', 'ASSET');
    PRINT 'Added BANK_SCB account mapping';
END
GO

-- Add DIRECTOR_ADVANCE_REPAY for reimbursement flow
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'DIRECTOR_ADVANCE_REPAY')
BEGIN
    INSERT INTO Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type) VALUES
    ('DIRECTOR_ADVANCE_REPAY', N'เจ้าหนี้กรรมการ/เงินทดรองรับจากกรรมการ', '21230', 'LIABILITY');
    PRINT 'Added DIRECTOR_ADVANCE_REPAY account mapping';
END
GO

-- ──────────────────────────────────────────────
-- 10. Per-line Expense Category on Payment Detail
-- ──────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Account_Payment_Detail' AND COLUMN_NAME = 'Paid_Type_ID')
BEGIN
    ALTER TABLE Account_Payment_Detail ADD Paid_Type_ID INT NULL;
    PRINT 'Added Paid_Type_ID column to Account_Payment_Detail';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Account_Payment_Detail' AND COLUMN_NAME = 'Paid_Type_Name')
BEGIN
    ALTER TABLE Account_Payment_Detail ADD Paid_Type_Name NVARCHAR(100) NULL;
    PRINT 'Added Paid_Type_Name column to Account_Payment_Detail';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Account_Payment_Detail' AND COLUMN_NAME = 'Nexaacc_AccountId')
BEGIN
    ALTER TABLE Account_Payment_Detail ADD Nexaacc_AccountId UNIQUEIDENTIFIER NULL;
    PRINT 'Added Nexaacc_AccountId column to Account_Payment_Detail';
END
GO

PRINT '================================================================';
PRINT 'PHASE 9 Migration 01 completed successfully.';
PRINT '';
PRINT 'Next steps:';
PRINT '1. Configure Nexaacc API credentials in Accounting_Integration_Config';
PRINT '2. Map Nexaacc Account GUIDs in Accounting_Account_Mapping (Nexaacc_AccountId)';
PRINT '3. Link payment methods in Account_Paid_How to NextAcc accounts';
PRINT '4. Link expense categories in Account_Paid_Type to NextAcc accounts';
PRINT '5. Set Nexaacc_Enabled = true to start sync';
PRINT '================================================================';
