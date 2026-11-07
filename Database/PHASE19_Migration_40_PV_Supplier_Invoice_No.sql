-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 40: หน้าใบสำคัญจ่าย — เลขที่ใบกำกับ/ใบเสร็จของผู้ขาย (ตรวจใบซ้ำ) + สวิตช์ผู้ช่วย AI
-- ════════════════════════════════════════════════════════════════════════════
-- CLAUDE.md ข้อ 3b (หน้า Account/PaymentVoucher ออกแบบใหม่)
--
-- 1) Account_Payment.Supplier_Invoice_No / Supplier_Invoice_No_Norm (ตัดช่องว่าง/สัญลักษณ์ + ตัวพิมพ์ใหญ่ —
--    กติกาเดียวกับ Ocr_Document_Log.Supplier_Doc_No_Norm) → หน้าเตือนใบซ้ำ "ผู้ขายเดียวกัน + เลขที่ใบเดียวกัน"
--    ก่อนบันทึก (ร่วมกับ Ocr_Document_Log และ document/duplicate-check ของ NextAcc)
--    ข้อมูลนี้เก็บในระบบอย่างเดียว — ไม่ได้ส่งเข้า payload คิว (mapper/sync เดิมไม่เปลี่ยน)
-- 2) ค่าตั้ง Nexaacc_Pv_Ai_Assist (1) — ชิปคำแนะนำจาก AI ของ NextAcc ในหน้าใบสำคัญจ่าย · 0 = ปิด
--
-- ยังไม่รัน = หน้าไม่เก็บเลขที่ใบของผู้ขาย และตรวจซ้ำเฉพาะกับหน้าสแกน/NextAcc · ผู้ช่วย AI เปิดตามค่าเริ่มต้น (1)
-- ไม่แตะข้อมูลเดิม · idempotent รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.Account_Payment', 'Supplier_Invoice_No') IS NULL
    ALTER TABLE dbo.Account_Payment ADD Supplier_Invoice_No NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.Account_Payment', 'Supplier_Invoice_No_Norm') IS NULL
    ALTER TABLE dbo.Account_Payment ADD Supplier_Invoice_No_Norm NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Account_Payment_SupplierInv' AND object_id = OBJECT_ID('dbo.Account_Payment'))
    CREATE INDEX IX_Account_Payment_SupplierInv ON dbo.Account_Payment (Supplier_Invoice_No_Norm) INCLUDE (Vendor_ID, Status);
GO

IF OBJECT_ID(N'dbo.Accounting_Integration_Config', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    SELECT N'Nexaacc_Pv_Ai_Assist', N'1',
           N'หน้าใบสำคัญจ่าย: ชิปคำแนะนำจาก AI ของ NextAcc (บัญชี/VAT/หัก ณ ที่จ่าย/เครดิต-เงินสด/ผู้ติดต่อ) ให้ผู้ใช้กดใช้เอง · 0 = ปิด',
           GETDATE()
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config x WHERE x.ConfigKey = N'Nexaacc_Pv_Ai_Assist');
END
GO

PRINT N'PHASE19_40 เสร็จ — หน้าใบสำคัญจ่ายเก็บเลขที่ใบของผู้ขาย + ตรวจใบซ้ำ';
GO
