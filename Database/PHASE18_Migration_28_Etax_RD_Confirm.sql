-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 28: ยืนยัน e-Tax สำเร็จจากอีเมลตอบกลับของกรมสรรพากร
-- ════════════════════════════════════════════════════════════════════════════
-- เคส: ลูกค้าขอ e-Tax → ระบบ/ผู้ให้บริการส่งเอกสารเข้ากรมสรรพากร → กรมฯ ตอบกลับ
--      ทางอีเมลว่ารับเอกสารสำเร็จ ⟹ อ่านอีเมลนั้นแล้วมาร์คใบกำกับว่า "สรรพากรรับแล้ว"
--      แสดงทั้งหน้ารายการ e-Tax และหน้าการจอง
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

IF OBJECT_ID('dbo.Accounting_ETax_Log', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.Accounting_ETax_Log') AND name = 'Rd_Confirmed_Date')
    BEGIN
        ALTER TABLE dbo.Accounting_ETax_Log ADD
            Rd_Confirmed_Date DATETIME NULL,          -- เวลาที่ได้รับอีเมลยืนยันจากกรมสรรพากร
            Rd_Confirm_Ref    NVARCHAR(200) NULL,     -- เลขอ้างอิง/หัวเรื่องอีเมลตอบกลับ
            Rd_Confirm_MsgId  NVARCHAR(300) NULL;     -- Message-Id ของอีเมล (กันประมวลผลซ้ำ)
        PRINT 'Added RD confirmation columns to Accounting_ETax_Log';
    END
    ELSE
        PRINT 'RD confirmation columns already exist - skipped';
END
GO

-- ค่าตั้งค่า (อยู่กับกลุ่ม Accounting เดิม — ตั้งที่หน้า Accounting Integration)
IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_Rd_Watch_Enabled')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Etax_Rd_Watch_Enabled', '0');

    -- โดเมน/คำในผู้ส่งที่ถือว่าเป็นอีเมลจากกรมสรรพากร (คั่นจุลภาค)
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_Rd_FromContains')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue)
        VALUES ('Etax_Rd_FromContains', 'rd.go.th, etax, teda.th');

    -- คำที่บ่งชี้ว่า "สำเร็จ" ในหัวเรื่อง/เนื้อความ (คั่นจุลภาค)
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_Rd_SuccessWords')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue)
        VALUES ('Etax_Rd_SuccessWords', N'สำเร็จ, สมบูรณ์, ได้รับเอกสาร, นำส่งเรียบร้อย, success, accepted, completed');

    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_Rd_ProcessedLabel')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Etax_Rd_ProcessedLabel', 'RD-Processed');

    PRINT 'Seeded e-Tax RD watcher config';
END
GO

PRINT 'PHASE18_28 completed';
GO
