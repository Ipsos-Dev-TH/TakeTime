-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 29: อ่านอีเมลจอง OTA — ลองใหม่อัตโนมัติ + map ห้องยืดหยุ่นขึ้น
-- ════════════════════════════════════════════════════════════════════════════
-- ปัญหา: อีเมลจองที่ลงไม่สำเร็จ (ไม่มี mapping / ห้องไม่ว่าง) ถูกย้ายเข้า folder
--        "STAAH-Failed" แล้วจบ — รอบถัดไปค้นเฉพาะ INBOX + ยังไม่อ่าน จึงไม่มีวัน
--        ถูกหยิบมาลองใหม่ ต่อให้แก้ mapping หรือปล่อยห้องว่างแล้วก็ตาม
-- แก้:   เพิ่มรอบ retry อ่าน folder Failed ซ้ำจนสำเร็จ (จำกัดอายุอีเมล + จำนวนต่อรอบ)
--        และยอม match ห้องด้วยชื่อห้องอย่างเดียวเมื่อชื่อ Agency ไม่ตรงตาราง map
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
BEGIN
    -- วนกลับมาลองลงจองอีเมลใน folder Failed ให้เอง (ค่าเริ่มต้น = เปิด)
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_RetryFailed')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Email_Rsv_RetryFailed', '1');

    -- ลองใหม่เฉพาะอีเมลที่อายุไม่เกิน N ชั่วโมง (กันวนอีเมลเก่าไม่รู้จบ)
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_RetryHours')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Email_Rsv_RetryHours', '72');

    -- จำนวนอีเมลที่ลองใหม่สูงสุดต่อรอบ (กันรอบเดียวกินเวลานาน)
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_RetryMaxPerRun')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Email_Rsv_RetryMaxPerRun', '20');

    -- ถ้า Agency ในอีเมลไม่ตรงตาราง MapDataWithSTAAH ให้ยอม match ด้วยชื่อห้องอย่างเดียว
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_MapAnyChannel')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Email_Rsv_MapAnyChannel', '1');

    -- ลำดับห้องที่จัดให้ก่อน — โปรแกรมเดิม (SelectAccommodations) hard-code ไว้ 16,15,3,1,2,4,5
    -- เว้นว่าง = เรียงตาม Accommodation.OrderID
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_RoomPriority')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Email_Rsv_RoomPriority', '');

    -- เบอร์สำรองเมื่ออีเมลไม่มีเบอร์ (เบอร์คือ key ของตาราง Customer) เว้นว่าง = OTA_{BookingID}
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_DefaultPhone')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Email_Rsv_DefaultPhone', '');

    -- สถานะที่ตั้งเมื่อยกเลิกจากอีเมล OTA (ยกเลิก / ยกเลิกคืนเงิน / ยกเลิกไม่คืนเงิน)
    -- โปรแกรมเดิมใช้ 'ยกเลิกคืนเงิน' — ค่าเริ่มต้นที่นี่คือ 'ยกเลิก' (กลาง ๆ) เปลี่ยนได้ที่หน้า Admin
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_CancelStatus')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Email_Rsv_CancelStatus', N'ยกเลิก');

    PRINT 'Email intake retry/mapping config keys ready';
END
GO

-- ── ห้องแบบคิดตามจำนวนคน (LimitWithPeople) ที่ผูกกับ mapping OTA ────────────────
-- ตัวอ่านอีเมลรุ่นก่อนหน้าถือว่า "มีคนจอง = เต็ม" ทำให้ห้องรวมที่ยังมีที่ว่างถูกปฏิเสธ
-- (หน้าจอโชว์ว่าง แต่อีเมลลงจองไม่ได้) — แก้ในโค้ดแล้ว ตรงนี้แค่รายงานว่ามีห้องแบบนี้กี่ห้อง
IF OBJECT_ID('dbo.MapDataWithSTAAH', 'U') IS NOT NULL AND OBJECT_ID('dbo.Accommodation', 'U') IS NOT NULL
BEGIN
    DECLARE @limitRooms INT;
    SELECT @limitRooms = COUNT(DISTINCT a.ID)
      FROM MapDataWithSTAAH m
      INNER JOIN Accommodation a ON a.ID = m.Accommodation_ID
     WHERE ISNULL(CONVERT(NVARCHAR(10), a.LimitWithPeople), 'False') IN ('1', 'True');
    PRINT 'OTA-mapped rooms using LimitWithPeople (per-person capacity) = '
        + CONVERT(VARCHAR(10), ISNULL(@limitRooms, 0));
END
GO

-- ── ตรวจสุขภาพ mapping: ชี้ไปห้องที่ไม่มีอยู่จริง หรือถูกปิดใช้งาน ─────────────
-- (แค่รายงาน ไม่แก้ข้อมูลให้อัตโนมัติ — ให้ผู้ดูแลตัดสินใจเอง)
IF OBJECT_ID('dbo.MapDataWithSTAAH', 'U') IS NOT NULL AND OBJECT_ID('dbo.Accommodation', 'U') IS NOT NULL
BEGIN
    DECLARE @orphan INT, @disabled INT;

    SELECT @orphan = COUNT(*)
      FROM MapDataWithSTAAH m
      LEFT JOIN Accommodation a ON a.ID = m.Accommodation_ID
     WHERE a.ID IS NULL;

    SELECT @disabled = COUNT(*)
      FROM MapDataWithSTAAH m
      INNER JOIN Accommodation a ON a.ID = m.Accommodation_ID
     WHERE ISNULL(CONVERT(NVARCHAR(10), a.Status), '0') NOT IN ('1', 'True');

    PRINT 'MapDataWithSTAAH health: orphan rows = ' + CONVERT(VARCHAR(10), ISNULL(@orphan, 0))
        + ', rows pointing to disabled rooms = ' + CONVERT(VARCHAR(10), ISNULL(@disabled, 0));

    IF ISNULL(@orphan, 0) > 0 OR ISNULL(@disabled, 0) > 0
        PRINT '>> ตรวจตาราง MapDataWithSTAAH ที่เมนู Admin -> จัดการฐานข้อมูล -> MapDataWithSTAAH';
END
GO

PRINT '════════════════════════════════════════════════';
PRINT 'PHASE18_Migration_29 completed';
PRINT '════════════════════════════════════════════════';
GO
