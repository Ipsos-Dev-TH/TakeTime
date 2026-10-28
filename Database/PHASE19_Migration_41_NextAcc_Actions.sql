-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 41: งาน NextAcc ที่เพิ่งเปิดใช้ (CLAUDE.md ข้อ 25)
-- ════════════════════════════════════════════════════════════════════════════
-- (เลข 40 มีทีมอื่นจองไว้แล้ว — PHASE19_Migration_40_PV_Supplier_Invoice_No)
--
-- 1) Nexaacc_Doc_Action — ประวัติงานที่สั่ง NextAcc จากหน้าจอ (ใครสั่ง/ขออะไร/ผลอะไร/เอกสารใหม่เลขอะไร)
--    Action = FULL_TAX_INVOICE   ลูกค้าขอใบกำกับเต็มรูปทีหลัง (issue-full-tax-invoice / แก้ผู้ติดต่อ / ยกเลิก→ออกใหม่)
--             RECLASSIFY_SOURCE  เปลี่ยนแหล่งเงินหลังอนุมัติ (reclassify-payment-source)
--             COMPLETE_TAX_INV   เติมใบกำกับภาษีซื้อ 11640→11610 (complete-tax-invoice)
--             CN_ETAX            e-Tax ใบลดหนี้ (etax/generate · submit)
--             DOC_EMAIL          ส่งอีเมลเอกสารจาก NextAcc (send-email)
--    Status = QUEUED | DONE | BLOCKED | FAILED
--    Path   = REPLACEMENT | CONTACT_UPGRADE | APPROVE_DRAFT | RECREATE | ALREADY | NEXTACC | LOCAL_ONLY ...
-- 2) Credit_Note.Etax_* — สถานะ e-Tax ของใบลดหนี้ (แสดงในหน้า ใบลดหนี้ / คืนเงินลูกค้า)
-- 3) ค่าตั้ง CreditNote_Etax_Auto = 0  (1 = ออก e-Tax ใบลดหนี้อัตโนมัติเมื่อใบต้นทางส่ง e-Tax ถึงกรมสรรพากรแล้ว)
--
-- ยังไม่รัน = หน้าใหม่ทำงานได้แต่ไม่มีประวัติ (log ธรรมดา) และไม่มีคอลัมน์สถานะ e-Tax ใบลดหนี้ (ปุ่มยังส่งได้ แสดงสถานะจาก NextAcc)
-- ไม่แตะข้อมูลเดิม · idempotent รันซ้ำได้ · รันแล้ว recycle app pool (cache schema 60 วินาที)
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.Nexaacc_Doc_Action', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Nexaacc_Doc_Action (
        ID              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Nexaacc_Doc_Action PRIMARY KEY,
        Action          NVARCHAR(30)     NOT NULL,
        Receipt_ID      NVARCHAR(50)     NULL,
        Credit_Note_ID  INT              NULL,
        Source_Doc_Id   UNIQUEIDENTIFIER NULL,
        Source_Doc_No   NVARCHAR(60)     NULL,
        Result_Doc_Id   UNIQUEIDENTIFIER NULL,
        Result_Doc_No   NVARCHAR(60)     NULL,
        Path            NVARCHAR(30)     NULL,
        Status          NVARCHAR(20)     NOT NULL CONSTRAINT DF_NDA_Status DEFAULT ('QUEUED'),
        Request_Json    NVARCHAR(MAX)    NULL,
        Result_Message  NVARCHAR(2000)   NULL,
        Queue_Id        BIGINT           NULL,
        Created_By      NVARCHAR(100)    NULL,
        Created_At      DATETIME         NOT NULL CONSTRAINT DF_NDA_Created DEFAULT (GETDATE()),
        Updated_At      DATETIME         NULL
    );
    CREATE INDEX IX_NDA_Receipt ON dbo.Nexaacc_Doc_Action (Receipt_ID, Created_At);
    CREATE INDEX IX_NDA_Action ON dbo.Nexaacc_Doc_Action (Action, Created_At) INCLUDE (Status);
    CREATE INDEX IX_NDA_Source ON dbo.Nexaacc_Doc_Action (Source_Doc_Id);
    PRINT N'สร้างตาราง Nexaacc_Doc_Action';
END
ELSE
    PRINT N'มีตาราง Nexaacc_Doc_Action อยู่แล้ว';
GO

IF OBJECT_ID(N'dbo.Credit_Note', N'U') IS NULL
    PRINT N'⚠ ไม่มีตาราง Credit_Note (ยังไม่รัน PHASE19_33) — ข้ามคอลัมน์ e-Tax ใบลดหนี้';
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.Credit_Note', N'Etax_Id') IS NULL     ALTER TABLE dbo.Credit_Note ADD Etax_Id UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'dbo.Credit_Note', N'Etax_Status') IS NULL ALTER TABLE dbo.Credit_Note ADD Etax_Status NVARCHAR(20) NULL;
    IF COL_LENGTH(N'dbo.Credit_Note', N'Etax_Ref') IS NULL    ALTER TABLE dbo.Credit_Note ADD Etax_Ref NVARCHAR(80) NULL;
    IF COL_LENGTH(N'dbo.Credit_Note', N'Etax_At') IS NULL     ALTER TABLE dbo.Credit_Note ADD Etax_At DATETIME NULL;
    IF COL_LENGTH(N'dbo.Credit_Note', N'Etax_Error') IS NULL  ALTER TABLE dbo.Credit_Note ADD Etax_Error NVARCHAR(1000) NULL;
    PRINT N'คอลัมน์ e-Tax ของ Credit_Note พร้อม';
END
GO

IF OBJECT_ID(N'dbo.Accounting_Integration_Config', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'CreditNote_Etax_Auto')
BEGIN
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('CreditNote_Etax_Auto', '0');
    PRINT N'เพิ่ม CreditNote_Etax_Auto = 0 (ส่ง e-Tax ใบลดหนี้ด้วยปุ่มในหน้าใบลดหนี้)';
END
GO

PRINT N'PHASE19_41 เสร็จ — recycle app pool';
GO
