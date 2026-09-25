-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 14: กันบาร์โค้ดสินค้า "ค่าว่าง" (ต้นเหตุสินค้าผีเข้าตะกร้า)
-- ════════════════════════════════════════════════════════════════════════════
-- อาการที่เจอ: หน้าขาย/รับเข้า/ปรับสต๊อก ค้นสินค้าด้วย
--     WHERE Product_Name = @text OR Barcode = @text
-- เมื่อ @text เป็นค่าว่าง → เงื่อนไข Barcode = '' ไป "แมตช์สินค้าที่บันทึกโดยไม่ใส่บาร์โค้ด"
-- (Barcode = '') → ระบบเพิ่ม/บวกจำนวนสินค้าตัวนั้นเองทุกครั้ง (เคสจริง: ฮูการ์เดน โรเซ่)
--
-- ฝั่งโค้ดแก้แล้ว (ช่องว่าง = ไม่ค้น + เทียบบาร์โค้ดเฉพาะแถวที่มีค่าจริง) แต่ตัวที่ "บันทึกสินค้า"
-- อยู่นอกระบบนี้ → กันที่ฐานข้อมูลด้วย: ค่าว่าง/ช่องว่างล้วน จะถูกแปลงเป็น NULL อัตโนมัติ
-- (NULL ไม่เท่ากับอะไรเลยใน SQL → ไม่มีทางถูกแมตช์โดยบังเอิญอีก)
--
-- ใช้ trigger ไม่ใช่ CHECK constraint — เพื่อ "ซ่อมให้เงียบ ๆ" ไม่ทำให้โปรแกรมที่บันทึกสินค้าพัง
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- 1) ซ่อมข้อมูลเดิม: บาร์โค้ดที่เป็นค่าว่าง/ช่องว่างล้วน → NULL
DECLARE @fixed INT = 0;
UPDATE [dbo].[Product]
   SET Barcode = NULL
 WHERE Barcode IS NOT NULL
   AND LTRIM(RTRIM(Barcode)) = '';
SET @fixed = @@ROWCOUNT;
PRINT CONCAT('Normalized blank barcodes -> NULL: ', @fixed, ' row(s)');
GO

-- 2) trigger: บันทึก/แก้ไขสินค้าครั้งต่อ ๆ ไป ถ้าบาร์โค้ดว่าง → เก็บเป็น NULL อัตโนมัติ
--    (ครอบทุกช่องทางที่เขียนตาราง Product รวมโปรแกรมภายนอก/แก้มือใน SSMS)
IF OBJECT_ID('dbo.TR_Product_NormalizeBarcode', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_Product_NormalizeBarcode;
GO

CREATE TRIGGER dbo.TR_Product_NormalizeBarcode
ON [dbo].[Product]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- แตะเฉพาะแถวที่เพิ่งเขียนและบาร์โค้ดเป็นค่าว่าง (ไม่มีก็ไม่ทำอะไร → ไม่วน trigger ซ้ำ)
    IF NOT EXISTS (SELECT 1 FROM inserted
                   WHERE Barcode IS NOT NULL AND LTRIM(RTRIM(Barcode)) = '')
        RETURN;

    UPDATE p
       SET p.Barcode = NULL
      FROM [dbo].[Product] p
     INNER JOIN inserted i ON i.ID = p.ID
     WHERE p.Barcode IS NOT NULL
       AND LTRIM(RTRIM(p.Barcode)) = '';
END
GO

PRINT 'Created trigger TR_Product_NormalizeBarcode (blank barcode -> NULL on insert/update)';
GO

-- 3) รายงาน: สินค้าที่ยังไม่มีบาร์โค้ด (ไม่ใช่ error — ไว้ให้ผู้ดูแลไล่เติมให้ครบ)
SELECT ID, Product_Name, Category_ID, Sell_Price,
       N'ยังไม่มีบาร์โค้ด (สแกนไม่ได้ — ค้นด้วยชื่อเท่านั้น)' AS หมายเหตุ
  FROM [dbo].[Product]
 WHERE Barcode IS NULL OR LTRIM(RTRIM(Barcode)) = ''
 ORDER BY Product_Name;

-- 4) รายงาน: บาร์โค้ดซ้ำ (ถ้ามี = สแกนแล้วได้สินค้าผิดตัวได้)
SELECT Barcode, COUNT(*) AS จำนวนสินค้าที่ใช้บาร์โค้ดนี้,
       STRING_AGG(CAST(Product_Name AS NVARCHAR(MAX)), N' | ') AS รายชื่อ
  FROM [dbo].[Product]
 WHERE Barcode IS NOT NULL AND LTRIM(RTRIM(Barcode)) <> ''
 GROUP BY Barcode
HAVING COUNT(*) > 1
 ORDER BY COUNT(*) DESC;
GO
