-- ============================================================================
-- PHASE19 Migration 18 — ตั้งค่า "หัวกระดาษเอกสารขาย" ที่ NextAcc พิมพ์ (Nexaacc_Receipt_Header_Type)
-- ============================================================================
-- ปัญหา: เอกสารขายที่ sync ขึ้น NextAcc (เช่น TIV-20260912-0005) พิมพ์หัว "ใบเสร็จรับเงิน"
--   แต่ต้องการ "ใบเสร็จรับเงิน/ใบกำกับภาษีอย่างย่อ"
--
-- ข้อเท็จจริงฝั่ง NextAcc (Wachira-d/Accounting — PdfGenerationService.ComputeDocumentTitle):
--   หัวถูกคำนวณตอนพิมพ์จากธงบนเอกสาร + ข้อมูลผู้ซื้อ + สิทธิ์ ภ.พ.06 ของบริษัท
--   • ผู้ซื้อ §86/4 ครบ + ขายสด            → "ใบเสร็จรับเงิน/ใบกำกับภาษี" (ใบเต็มรูป)
--   • ผู้ซื้อไม่ประสงค์รับใบกำกับ/walk-in/ไม่ครบ + บริษัทได้รับอนุมัติ ภ.พ.06
--                                          → "ใบเสร็จรับเงิน/ใบกำกับภาษีอย่างย่อ"
--   • กรณีเดียวกันแต่บริษัท "ยังไม่ได้ตั้ง ภ.พ.06" ใน NextAcc → ลดเหลือ "ใบเสร็จรับเงิน"
--     (ต้นเหตุของ TIV-20260912-0005 — แก้ที่ NextAcc: ข้อมูลบริษัท ติ๊ก "ได้รับอนุมัติ ภ.พ.06" + วันที่อนุมัติ)
--
-- ค่าที่ตั้งได้ (TakeTime เลือก "เส้นทาง/ธง" ที่ส่งให้ NextAcc — ไม่ได้พิมพ์หัวเอง):
--   AUTO        (ค่าเริ่มต้น = พฤติกรรมเดิม) ลูกค้ามีเลขภาษี 13 หลัก + ที่อยู่ = ใบเต็มรูป / ไม่ครบ = อย่างย่อ
--   ABBREVIATED อย่างย่อทุกใบ ยกเว้นลูกค้านิติบุคคล (เลขภาษีขึ้นต้น 0) ที่ข้อมูลครบ (ยังออกเต็มรูป)
-- ใบมัดจำไม่ขึ้นกับค่านี้ (ยังเป็นใบเสร็จรับเงิน REC)
--
-- ปลอดภัย: รันซ้ำได้ · ไม่แตะค่าที่ตั้งไว้แล้ว · ไม่แก้เอกสารใด ๆ
-- ============================================================================

SET NOCOUNT ON;

IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NULL
BEGIN
    PRINT N'ยังไม่มีตาราง Accounting_Integration_Config — ข้ามไมเกรชันนี้';
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Receipt_Header_Type')
BEGIN
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue)
    VALUES ('Nexaacc_Receipt_Header_Type', 'AUTO');
    PRINT N'เพิ่มค่า Nexaacc_Receipt_Header_Type = AUTO (พฤติกรรมเดิม)';
END
ELSE
BEGIN
    DECLARE @cur NVARCHAR(200);
    SELECT @cur = ConfigValue FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Receipt_Header_Type';
    PRINT N'มีค่า Nexaacc_Receipt_Header_Type = ' + ISNULL(@cur, N'(ว่าง)') + N' อยู่แล้ว — ไม่แตะค่าที่ตั้งไว้';
END
GO

PRINT '';
PRINT N'เปลี่ยนค่าได้ที่ Admin → ตั้งค่า → NextAcc → "หัวกระดาษเอกสารขาย"';
PRINT N'⚠ คำว่า "อย่างย่อ" จะพิมพ์ได้ต่อเมื่อบริษัทใน NextAcc ได้รับอนุมัติ ภ.พ.06 (ตั้งในข้อมูลบริษัทของ NextAcc)';
PRINT N'   ใบกำกับภาษีอย่างย่อ ผู้ซื้อเคลมภาษีซื้อไม่ได้ (§82/5(2)) · ลูกค้านิติบุคคลที่ขอใบกำกับต้องได้ใบเต็มรูป';
