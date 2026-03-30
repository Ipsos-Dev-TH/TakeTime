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
    ('Nexaacc_Email',            '',       'Login email for Nexaacc API'),
    ('Nexaacc_Password_Encrypted', '',     'Encrypted password (use Code.Crypt() to encrypt)'),
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
    -- Assets (หมวด 1)
    ('CASH',              N'เงินสด',                            '1110', 'ASSET'),
    ('BANK_KBANK',        N'เงินฝากธนาคาร - กสิกรไทย',           '1111', 'ASSET'),
    ('BANK_KTB',          N'เงินฝากธนาคาร - กรุงไทย',            '1112', 'ASSET'),
    ('BANK_CARD',         N'เงินฝากธนาคาร - บัตรเครดิต',         '1113', 'ASSET'),
    ('DIRECTOR_ADVANCE',  N'เงินทดรองจ่าย - กรรมการ',            '1114', 'ASSET'),
    ('ROOM_AR',           N'ลูกหนี้ค่าห้องพัก',                  '1121', 'ASSET'),
    ('INVENTORY',         N'สินค้าคงเหลือ',                     '1150', 'ASSET'),
    ('INPUT_VAT',         N'ภาษีซื้อ',                          '1160', 'ASSET'),

    -- Liabilities (หมวด 2)
    ('ACCOUNTS_PAYABLE',  N'เจ้าหนี้การค้า',                    '2110', 'LIABILITY'),
    ('ADVANCE_DEPOSIT',   N'เงินรับล่วงหน้า - มัดจำ',            '2130', 'LIABILITY'),
    ('OUTPUT_VAT',        N'ภาษีขาย',                           '2140', 'LIABILITY'),
    ('WHT_PAYABLE',       N'ภาษีหัก ณ ที่จ่าย ค้างจ่าย',         '2150', 'LIABILITY'),

    -- Revenue (หมวด 4)
    ('ROOM_REVENUE',      N'รายได้ค่าห้องพัก',                  '4100', 'REVENUE'),
    ('PRODUCT_REVENUE',   N'รายได้ขายสินค้า',                   '4200', 'REVENUE'),
    ('FB_REVENUE',        N'รายได้ค่าอาหารและเครื่องดื่ม',       '4210', 'REVENUE'),
    ('SERVICE_REVENUE',   N'รายได้ค่าบริการอื่น',               '4300', 'REVENUE'),
    ('OTHER_INCOME',      N'รายได้อื่น',                        '4900', 'REVENUE'),

    -- Expenses (หมวด 5)
    ('COGS',              N'ต้นทุนสินค้าขาย',                   '5100', 'EXPENSE'),
    ('SALARY_EXPENSE',    N'เงินเดือนและค่าแรง',                '5200', 'EXPENSE'),
    ('EXPENSE_UTILITY',   N'ค่าสาธารณูปโภค',                    '5300', 'EXPENSE'),
    ('EXPENSE_MAINTENANCE', N'ค่าซ่อมแซมบำรุงรักษา',            '5400', 'EXPENSE'),
    ('EXPENSE_SUPPLIES',  N'ค่าวัสดุสิ้นเปลือง',                '5500', 'EXPENSE'),
    ('EXPENSE_OTA',       N'ค่าคอมมิชชั่น OTA',                 '5600', 'EXPENSE'),
    ('EXPENSE_OTHER',     N'ค่าใช้จ่ายอื่น',                    '5900', 'EXPENSE');

    PRINT 'Seeded default account mappings';
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

PRINT '================================================================';
PRINT 'PHASE 9 Migration 01 completed successfully.';
PRINT '';
PRINT 'Next steps:';
PRINT '1. Configure Nexaacc API credentials in Accounting_Integration_Config';
PRINT '2. Map Nexaacc Account GUIDs in Accounting_Account_Mapping (Nexaacc_AccountId)';
PRINT '3. Set Nexaacc_Enabled = true to start sync';
PRINT '================================================================';
