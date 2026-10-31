-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 20: ลงบันทึกรายได้ที่ยังตกหล่น (รูมเซอร์วิส + ห้องจาก OTA)
-- ════════════════════════════════════════════════════════════════════════════
-- ปัญหาที่แก้ (ตรวจพบจากการไล่โค้ดเส้นทางเงิน):
--   1) รูมเซอร์วิส (สั่งอาหารผ่าน Guest Portal) — ออเดอร์ที่ลูกค้า "จ่ายเอง" (โอน/เงินสด)
--      ไม่ถูกบันทึกที่ไหนเลย และทุกออเดอร์ไม่เคยตัดต้นทุน/สต๊อก (ไม่มี COGS)
--   2) การจองจาก OTA (อีเมล STAAH) ถูกสร้างด้วย NoCreateReceipt=1 → ไม่มีใบเสร็จ
--      ⟹ รายได้ค่าห้องจาก OTA ไม่เคยเข้าบัญชีเลย
--
-- ทั้งสองอย่างเป็น "ตัวเลือก" — เปิด/ปิดได้ที่ Admin → Accounting Integration
-- (ค่าเริ่มต้น = ปิด เพื่อไม่ให้พฤติกรรมเดิมเปลี่ยนเองหลัง deploy)
--
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- ── 1) marker กันโพสต์ซ้ำ: รูมเซอร์วิส ────────────────────────────────────────
IF OBJECT_ID('dbo.Guest_Room_Service_Orders', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.Guest_Room_Service_Orders')
                     AND name = 'Acct_Post_Ref')
BEGIN
    ALTER TABLE [dbo].[Guest_Room_Service_Orders] ADD [Acct_Post_Ref] NVARCHAR(60) NULL;
    PRINT 'Added Guest_Room_Service_Orders.Acct_Post_Ref';
END
GO

-- backfill 'LEGACY' ให้ออเดอร์เดิมทั้งหมด — กันระบบไล่โพสต์ย้อนหลังทั้งประวัติ
-- ในรอบแรกที่เปิดใช้ (ต้องการย้อนหลังจริง ให้เคลียร์ค่าเป็น NULL เฉพาะช่วงที่ต้องการเอง)
IF OBJECT_ID('dbo.Guest_Room_Service_Orders', 'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Guest_Room_Service_Orders') AND name = 'Acct_Post_Ref')
BEGIN
    UPDATE [dbo].[Guest_Room_Service_Orders]
       SET [Acct_Post_Ref] = 'LEGACY'
     WHERE [Acct_Post_Ref] IS NULL;
    PRINT 'Backfilled Acct_Post_Ref = LEGACY (' + CAST(@@ROWCOUNT AS NVARCHAR) + ' rows)';
END
GO

-- ── 2) marker กันโพสต์ซ้ำ: รายได้ห้องจาก OTA ───────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'Ota_Revenue_Ref')
BEGIN
    ALTER TABLE [dbo].[Reservation] ADD [Ota_Revenue_Ref] NVARCHAR(60) NULL;
    PRINT 'Added Reservation.Ota_Revenue_Ref';
END
GO

-- backfill 'LEGACY' ให้การจองที่เช็คเอาท์/จบไปแล้วก่อนติดตั้งฟีเจอร์นี้
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'Ota_Revenue_Ref')
BEGIN
    UPDATE [dbo].[Reservation]
       SET [Ota_Revenue_Ref] = 'LEGACY'
     WHERE [Ota_Revenue_Ref] IS NULL
       AND [CheckoutDate] < CAST(GETDATE() AS DATE);
    PRINT 'Backfilled Ota_Revenue_Ref = LEGACY (' + CAST(@@ROWCOUNT AS NVARCHAR) + ' rows)';
END
GO

-- index ช่วย job หาแถวที่ยังไม่โพสต์ (partial index — เล็กมาก)
IF OBJECT_ID('dbo.Guest_Room_Service_Orders', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_RoomService_AcctPending'
                     AND object_id = OBJECT_ID('dbo.Guest_Room_Service_Orders'))
BEGIN
    CREATE INDEX IX_RoomService_AcctPending
        ON [dbo].[Guest_Room_Service_Orders] ([Order_Date])
        INCLUDE ([Payment_Method], [Order_Status], [Total_Amount])
        WHERE [Acct_Post_Ref] IS NULL;
    PRINT 'Created IX_RoomService_AcctPending';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Reservation_OtaRevenuePending'
                 AND object_id = OBJECT_ID('dbo.Reservation'))
BEGIN
    CREATE INDEX IX_Reservation_OtaRevenuePending
        ON [dbo].[Reservation] ([CheckoutDate])
        INCLUDE ([Status], [TotalPrice])
        WHERE [Ota_Revenue_Ref] IS NULL;
    PRINT 'Created IX_Reservation_OtaRevenuePending';
END
GO

-- ── 3) ค่าตั้งค่า (ค่าเริ่มต้น = ปิด) ──────────────────────────────────────────
IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
BEGIN
    -- รายได้รูมเซอร์วิส: 0=ปิด, 1=เปิด
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_RoomServiceRevenue')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue)
        VALUES ('Nexaacc_RoomServiceRevenue', '0');

    -- รายได้ห้องจาก OTA: 0=ปิด, 1=เปิด
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_OtaRoomRevenue')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue)
        VALUES ('Nexaacc_OtaRoomRevenue', '0');

    -- รวบยอดขายหน้าร้าน (มีอยู่แล้วตั้งแต่ PHASE18_01 — seed เผื่อฐานที่ยังไม่มี)
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_PosDailyRollup')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue)
        VALUES ('Nexaacc_PosDailyRollup', '0');

    PRINT 'Seeded revenue-posting config keys';
END
GO

-- ── 4) หมายเหตุการใช้งาน ──────────────────────────────────────────────────────
-- • รูมเซอร์วิส "จ่ายเอง" (โอน/เงินสด) → รวบเป็นใบรับเงินสด 1 ใบ/วัน/วิธีจ่าย + ตัดต้นทุนต่อสินค้า
-- • รูมเซอร์วิส "ลงบิลห้อง" (CHARGE_TO_ROOM) → รายได้ไปกับใบเสร็จตอนเช็คเอาท์อยู่แล้ว
--   ระบบจะโพสต์ **เฉพาะต้นทุน/สต๊อก (COGS)** ให้ ไม่โพสต์รายได้ซ้ำ
-- • OTA → Dr ลูกหนี้ OTA / Cr รายได้ห้อง / Cr ภาษีขาย ต่อการจอง (ต้อง map บัญชี
--   OTA_RECEIVABLE ใน Accounting_Account_Mapping ก่อน — ดู PHASE18_Migration_11)
PRINT 'PHASE18_20 completed';
GO
