-- ============================================================================
-- PHASE18 Migration 34 — กวาดกู้อีเมลจองที่ตกหล่น (self-healing intake)
-- ============================================================================
-- ปัญหาที่แก้ (ต่อจากเคสล็อกค้าง 19–21 ส.ค. 2026):
--   รอบปกติอ่านเฉพาะ "INBOX + ยังไม่อ่าน" เท่านั้น ⇒ อีเมลจองหายเงียบได้หลายทาง
--     1. มีโปรแกรมอ่านเมลตัวเก่ายังรันอยู่ แย่งอ่านไปก่อน แล้วมาร์คอ่าน/ย้าย folder
--        → INBOX ว่าง ทั้งที่ไม่มีการจองเข้าระบบ
--     2. ระบบหยุดไปหลายวัน (ล็อกค้าง/เซิร์ฟเวอร์ล่ม) แล้วใบเก่าหลุดหน้าต่าง retry
--     3. Email_Rsv_RetryHours ตั้งสั้น (พบจริง = 3 ชม.) → ใบที่ล้มเหลวเมื่อวาน
--        ไม่ถูกหยิบมาลองใหม่อีกเลยตลอดกาล
--
-- ทางแก้: เพิ่มรอบ "กวาดกู้" ที่ค้นหาอีเมลจากผู้ส่ง STAAH ใน **ทุกโฟลเดอร์**
--   ย้อนหลัง N วัน แล้วลงจองใบที่ยังไม่มีในระบบ (dedup ด้วย OTA_Booking_ID)
--   ทำงานอัตโนมัติวันละครั้ง + ทันทีเมื่อระบบเพิ่งกลับมาจากการหยุด ≥ 6 ชม.
--   และกดเองได้จากปุ่ม "กู้อีเมลย้อนหลัง" ในหน้า Admin → อีเมลจอง
--   ไม่ย้ายอีเมลไปไหน (แค่มาร์คว่าอ่านแล้วเมื่อจบเคส) — เป็นเครื่องมือซ่อม
--
-- ปลอดภัย: รันซ้ำได้ ไม่ลบข้อมูล ไม่แตะ GL
-- ============================================================================

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_RecoverDaily')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Email_Rsv_RecoverDaily', '1',
            N'กวาดหาอีเมลจองที่ตกหล่นทุกโฟลเดอร์อัตโนมัติวันละครั้ง (0=ปิด, 1=เปิด)', GETDATE());

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_RecoverDays')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Email_Rsv_RecoverDays', '7',
            N'รอบกวาดกู้อัตโนมัติ ย้อนหลังกี่วัน', GETDATE());

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_RecoverMax')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Email_Rsv_RecoverMax', '100',
            N'จำนวนอีเมลสูงสุดต่อรอบกวาดกู้ (กันรอบยาวเกินไป)', GETDATE());
GO

-- ── ยืดหน้าต่าง "ลองใหม่" ที่ตั้งไว้สั้นเกินไป ──────────────────────────────
-- พบค่าจริงในระบบ = 3 ชั่วโมง ⇒ อีเมลที่ลงจองไม่สำเร็จเมื่อวานตกขบวนถาวร
-- ค่าที่ออกแบบไว้คือ 72 ชั่วโมง (ให้เวลาคนแก้ mapping/ปล่อยห้องแล้วระบบหายเอง)
-- ปรับเฉพาะกรณีตั้งไว้ต่ำกว่า 24 ชั่วโมง — ถ้าตั้ง 24+ ไว้แล้วถือว่าตั้งใจ ไม่แตะ
UPDATE Accounting_Integration_Config
SET ConfigValue = '72', Updated_Date = GETDATE()
WHERE ConfigKey = 'Email_Rsv_RetryHours'
  AND TRY_CAST(ConfigValue AS INT) IS NOT NULL
  AND TRY_CAST(ConfigValue AS INT) < 24;

IF @@ROWCOUNT > 0
    PRINT N'ปรับ Email_Rsv_RetryHours จากค่าที่สั้นเกินไป → 72 ชั่วโมง';
GO

SELECT ConfigKey, ConfigValue, Updated_Date
FROM Accounting_Integration_Config
WHERE ConfigKey LIKE 'Email_Rsv_%'
ORDER BY ConfigKey;
