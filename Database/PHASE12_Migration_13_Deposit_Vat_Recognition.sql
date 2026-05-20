-- ============================================================================
-- PHASE 12 Migration 13: Deposit VAT Recognition setting
-- ============================================================================
-- เพิ่มการตั้งค่า "จุดความรับผิด VAT ของเงินมัดจำ"
--
-- ตามประมวลรัษฎากร มาตรา 78/1: ธุรกิจ "บริการ" มีจุดความรับผิดในการเสีย VAT
-- = วันที่ได้รับชำระเงิน (ไม่ใช่วันส่งมอบบริการ)
-- → เมื่อโรงแรมรับเงินมัดจำ ต้องออกใบกำกับภาษีและนำส่ง VAT ทันที
--
-- ค่าที่ตั้งได้:
--   CHECKOUT (default) — รับรู้ VAT ตอนตัดมัดจำเป็นรายได้ (เช็คเอาท์)
--   RECEIPT  — รับรู้ VAT ทันทีที่รับเงินมัดจำ (ถูกต้องตาม ม.78/1 สำหรับบริการ)
-- ============================================================================

USE [Taketime]
GO

PRINT '============================================================================'
PRINT 'PHASE 12 Migration 13: Deposit VAT Recognition setting'
PRINT '============================================================================'
GO

IF OBJECT_ID(N'[dbo].[Accounting_Integration_Config]', N'U') IS NULL
BEGIN
    PRINT '! ไม่พบตาราง Accounting_Integration_Config — ข้าม';
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Deposit_Vat_Recognition')
    BEGIN
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
        VALUES ('Deposit_Vat_Recognition', 'CHECKOUT',
                N'จุดรับรู้ VAT ของเงินมัดจำ: CHECKOUT=ตอนตัดมัดจำเป็นรายได้, RECEIPT=ทันทีที่รับเงิน (ม.78/1 บริการ)',
                GETDATE());
        PRINT '+ เพิ่ม config: Deposit_Vat_Recognition = CHECKOUT (ค่าเริ่มต้น)';
    END
    ELSE
        PRINT '= config Deposit_Vat_Recognition มีอยู่แล้ว — ข้าม';
END
GO

PRINT '============================================================================'
PRINT 'PHASE 12 Migration 13: COMPLETED'
PRINT '============================================================================'
GO
