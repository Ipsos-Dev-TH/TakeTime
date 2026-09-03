-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 10: Seed GRNI (Goods-Received-Not-Invoiced) account mapping
-- ════════════════════════════════════════════════════════════════════════════
-- GR/IR model (แยกรับของ ↔ ใบกำกับ/วางบิล กันโพสต์ซ้อน):
--   รับของ (Product/In):  Dr สินค้าคงเหลือ / Cr GRNI 21240        (ไม่มี VAT, ไม่แตะเจ้าหนี้)
--   ใบกำกับ (OCR/วางบิล):  Dr GRNI 21240 (ล้างรับของ) + Dr ภาษีซื้อ 11610 / Cr เจ้าหนี้-เงินสด
--   → สินค้า/VAT/เจ้าหนี้ ลงครั้งเดียว, ภาษีซื้อเคลมได้ (ใบกำกับมีเลขภาษี), COGS perpetual ยังทำงาน
--
-- GRNI เป็นบัญชี "พัก" (clearing) แบบ pool — net กันเองที่ยอดรวม ไม่ต้องแมพราย 1:1
--   ยอดคงค้าง +  = ของมาแล้วยังไม่มีใบกำกับ (ตามใบกำกับ/เคลม VAT)
--   ยอดคงค้าง −  = จ่าย/วางบิลแล้วของยังไม่มา (ตามของ)
--   ส่วนต่างราคา (ใบกำกับ ≠ ต้นทุนรับของ) → ลง INVENTORY_VARIANCE (51210) ตอนวางบิล
--
-- 21240 = เจ้าหนี้-รับสินค้ายังไม่วางบิล (มาตรฐาน CoA). ถ้า NextAcc ใช้เลขต่าง แก้ได้ในหน้า
--         Admin > Accounting Integration > Account Mapping แล้วกด "ดึง Chart of Accounts"
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'GRNI')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        ('GRNI', N'เจ้าหนี้-รับสินค้ายังไม่วางบิล (GR/IR clearing)', '21240', 'LIABILITY', 1);
    PRINT 'Inserted GRNI mapping (21240) — verify/override via Admin UI + ดึง Chart of Accounts';
END
ELSE
    PRINT 'GRNI mapping already exists';
GO

-- feature flag: เปิด/ปิด GR/IR สำหรับรับสินค้า (default = เปิด). ปิด = กลับไปพฤติกรรมเดิม (Cr เจ้าหนี้)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_StockIn_UseGRNI')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Nexaacc_StockIn_UseGRNI', '1',
            N'รับสินค้าเข้าสต๊อกใช้ GR/IR: Cr GRNI 21240 แทนเจ้าหนี้การค้า (ใบกำกับ OCR ล้าง GRNI ภายหลัง)');
    PRINT 'Seeded flag Nexaacc_StockIn_UseGRNI = 1';
END
ELSE
    PRINT 'Flag Nexaacc_StockIn_UseGRNI already exists';
GO

SELECT TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Nexaacc_AccountId, Mapping_Type
FROM Accounting_Account_Mapping
WHERE TakeTime_Code IN ('GRNI', 'INVENTORY', 'INPUT_VAT', 'INVENTORY_VARIANCE', 'ACCOUNTS_PAYABLE')
ORDER BY TakeTime_Code;
GO
