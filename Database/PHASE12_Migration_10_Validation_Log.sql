-- ══════════════════════════════════════════════════════════════
-- PHASE 12 — Migration 10: Accounting Validation Log
-- ══════════════════════════════════════════════════════════════
-- สร้าง audit table สำหรับเก็บผลการตรวจสอบบัญชีที่ผิดพลาด
-- เช่น VAT คำนวณไม่ตรง, journal DR≠CR, line sum ≠ subtotal
-- ใช้สำหรับตรวจสอบย้อนหลัง + ดู pattern ของข้อผิดพลาด
-- ══════════════════════════════════════════════════════════════

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounting_Validation_Log')
BEGIN
    CREATE TABLE Accounting_Validation_Log (
        ID              BIGINT IDENTITY(1,1) PRIMARY KEY,
        Entity_Type     NVARCHAR(50) NOT NULL,    -- RECEIPT, VOUCHER, PAYROLL, JOURNAL, STOCK_IN
        Entity_ID       NVARCHAR(100) NULL,       -- เลขที่เอกสาร / reservation_id / payment_id
        Validation_Code NVARCHAR(50) NOT NULL,    -- TOTAL_MISMATCH, VAT_MISMATCH, WHT_MISMATCH, DR_CR_IMBALANCE, ...
        Error_Message   NVARCHAR(MAX) NOT NULL,
        Expected_Value  DECIMAL(18, 4) NULL,
        Actual_Value    DECIMAL(18, 4) NULL,
        Discrepancy     DECIMAL(18, 4) NULL,
        Is_Blocking     BIT NOT NULL DEFAULT 1,   -- 1 = ปฏิเสธการบันทึก, 0 = แค่เตือน
        Override_By     NVARCHAR(100) NULL,       -- ถ้า override โดย supervisor
        Override_Reason NVARCHAR(500) NULL,
        Created_By      NVARCHAR(100) NULL,
        Created_Date    DATETIME NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX IX_Validation_Log_Entity ON Accounting_Validation_Log (Entity_Type, Entity_ID);
    CREATE INDEX IX_Validation_Log_Code ON Accounting_Validation_Log (Validation_Code);
    CREATE INDEX IX_Validation_Log_Date ON Accounting_Validation_Log (Created_Date DESC);

    PRINT 'Created table: Accounting_Validation_Log';
END
ELSE
    PRINT 'Skip: Accounting_Validation_Log already exists';
GO

-- View: failed validations ใน 30 วันล่าสุด สำหรับ dashboard
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Recent_Validation_Failures')
    DROP VIEW vw_Recent_Validation_Failures;
GO

CREATE VIEW vw_Recent_Validation_Failures AS
SELECT
    Entity_Type,
    Validation_Code,
    COUNT(*) AS Failure_Count,
    SUM(CASE WHEN Is_Blocking = 1 THEN 1 ELSE 0 END) AS Blocked_Count,
    SUM(CASE WHEN Override_By IS NOT NULL THEN 1 ELSE 0 END) AS Overridden_Count,
    AVG(Discrepancy) AS Avg_Discrepancy,
    MAX(Discrepancy) AS Max_Discrepancy,
    MAX(Created_Date) AS Last_Occurrence
FROM Accounting_Validation_Log
WHERE Created_Date >= DATEADD(DAY, -30, GETDATE())
GROUP BY Entity_Type, Validation_Code;
GO

PRINT 'Created view: vw_Recent_Validation_Failures';
GO
