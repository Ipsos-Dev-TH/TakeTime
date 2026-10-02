-- ============================================================================
-- PHASE19 Migration 03 — สถานที่ใกล้เคียง: ข้อความโปรโมท + ป้าย + ปักหมุดแนะนำ
-- ============================================================================
-- เดิมมีแต่ "คำอธิบาย" ซึ่งเป็นข้อความกลาง ๆ บอกว่าที่นี่คืออะไร
-- แต่สิ่งที่ทำให้แขกอยากไปคือ "ที่นี่ดียังไง / ต้องสั่งอะไร" — ยังไม่มีที่เก็บ
--
-- เพิ่ม:
--   Highlight    — ข้อความโปรโมทสั้น ๆ เช่น "เมนูเด็ด: ไก่อบโอ่งสูตรโบราณ 40 ปี"
--   Badge_Text   — ป้ายมุมรูป เช่น "แนะนำ" / "ยอดนิยม" / "ต้องลอง"
--   Badge_Color  — สีป้าย
--   Is_Featured  — ปักหมุดให้ขึ้นก่อนเพื่อนในกลุ่มเดียวกัน
--   Price_Range  — ระดับราคา ฿ / ฿฿ / ฿฿฿ (มีประโยชน์กับร้านอาหาร/คาเฟ่)
--
-- ปลอดภัย: รันซ้ำได้ ไม่ลบข้อมูล ค่าใหม่เป็น NULL ได้หมด ของเดิมแสดงผลเหมือนเดิม
-- ต้องรัน PHASE19_Migration_01 ก่อน
-- ============================================================================

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_NearbyPlaces')
BEGIN
    RAISERROR('ยังไม่มีตาราง Guest_NearbyPlaces — รัน PHASE19_Migration_01 ก่อน', 16, 1);
    RETURN;
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Highlight')
BEGIN
    ALTER TABLE Guest_NearbyPlaces ADD Highlight NVARCHAR(300) NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Highlight (ข้อความโปรโมท)';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Badge_Text')
BEGIN
    ALTER TABLE Guest_NearbyPlaces ADD Badge_Text NVARCHAR(50) NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Badge_Text';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Badge_Color')
BEGIN
    ALTER TABLE Guest_NearbyPlaces ADD Badge_Color NVARCHAR(20) NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Badge_Color';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Is_Featured')
BEGIN
    ALTER TABLE Guest_NearbyPlaces ADD Is_Featured BIT NOT NULL DEFAULT 0;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Is_Featured (ปักหมุดแนะนำ)';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Guest_NearbyPlaces' AND COLUMN_NAME = 'Price_Range')
BEGIN
    ALTER TABLE Guest_NearbyPlaces ADD Price_Range NVARCHAR(10) NULL;
    PRINT 'เพิ่ม Guest_NearbyPlaces.Price_Range';
END
GO

-- ── ตรวจผล ──────────────────────────────────────────────────────────────────
SELECT COUNT(*) AS สถานที่ทั้งหมด,
       SUM(CASE WHEN ISNULL(Highlight, '') <> '' THEN 1 ELSE 0 END)  AS มีข้อความโปรโมทแล้ว,
       SUM(CASE WHEN ISNULL(Badge_Text, '') <> '' THEN 1 ELSE 0 END) AS มีป้ายแล้ว,
       SUM(CASE WHEN Is_Featured = 1 THEN 1 ELSE 0 END)              AS ปักหมุดแนะนำ
FROM Guest_NearbyPlaces;
