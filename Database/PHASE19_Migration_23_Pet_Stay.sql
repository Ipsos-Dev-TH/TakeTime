-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 23: สัตว์เลี้ยงเข้าพัก (Pet-friendly stay)
-- ════════════════════════════════════════════════════════════════════════════
-- หน้าจอง (Reserve.aspx): ติ๊ก "🐾 มีสัตว์เลี้ยงเข้าพัก" → ระบุจำนวนต่อห้อง (เฉพาะห้องที่รับสัตว์เลี้ยง)
--   ลูกค้าจองเองต้องติ๊กยอมรับ "นโยบายการนำสัตว์เลี้ยงเข้าพัก" ก่อนยืนยัน (บันทึกเวลา + ฉบับ)
-- ค่าบริการ = จำนวนตัว × ค่าบริการต่อตัว (× จำนวนคืน ถ้าคิดรายคืน) ต่อห้อง
--   ลงเป็น "ค่าใช้จ่ายในห้อง" (Reservation_Product_Charges, Product_ID = NULL) รายการละห้อง
--   → ไหลเข้ายอดรวม/ยอดค้าง (ReservationBalance: Total = ค่าห้อง + ค่าใช้จ่ายในห้อง), ใบเสร็จ
--     (บรรทัด "ค่าบริการสัตว์เลี้ยง") และ NextAcc ตามเส้นทางค่าใช้จ่ายในห้องเดิม
--   → ไม่บวกเข้า Reservation.TotalPrice / ราคาต่อคืน (Reservation_Accommodation.Price เป็นจำนวนเต็ม
--     และถูกอ่านด้วย Convert.ToInt32 หลายสิบจุด)
-- ตั้งค่าที่ ศูนย์ตั้งค่า → สัตว์เลี้ยงเข้าพัก (Admin/Settings/PetStay.aspx)
--
--   Accommodation.Pet_Allowed / Pet_Max_Per_Room / Pet_Fee_Per_Pet     ตั้งรายห้อง
--   Booking_Policy_Config: Pet_Enabled (0/1), Pet_Fee_Unit (NIGHT|STAY),
--                          Policy_Pet (ข้อความนโยบาย), Policy_Pet_Version
--   Booking_Pet_Policy_History                                         สำเนานโยบายทุกฉบับ
--   Reservation_Accommodation.Room_Pet_Count                           จำนวนสัตว์เลี้ยงต่อห้องที่จอง
--   Reservation.Pet_Count / Pet_Fee_Total / Pet_Notes /
--               Pet_Policy_Accepted_At / Pet_Policy_Version            สรุปบนใบจอง
--   Reservation_Product_Charges.Pet_Accommodation_ID                   ระบุว่าแถวไหนเป็นค่าสัตว์เลี้ยง (ของห้องใด)
--
-- ค่าเริ่มต้น: ปิดฟีเจอร์ (Pet_Enabled = 0) และทุกห้อง "ไม่รับสัตว์เลี้ยง" — รันแล้วหน้าจองยังเหมือนเดิม
-- ข้อความนโยบายตั้งต้นเป็นตัวอย่าง มีช่อง [แก้ไข: …] — ต้องแก้ให้ตรงนโยบายจริงก่อนเปิดใช้
-- idempotent — รันซ้ำได้, ไม่ทับค่าที่ตั้งไว้แล้ว, ไม่ลบข้อมูลเดิม
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- ── 1) ตั้งค่ารายห้อง ────────────────────────────────────────────────────────
IF COL_LENGTH('dbo.Accommodation', 'Pet_Allowed') IS NULL
BEGIN
    ALTER TABLE [dbo].[Accommodation] ADD [Pet_Allowed] BIT NOT NULL
        CONSTRAINT DF_Accommodation_Pet_Allowed DEFAULT (0);
    PRINT 'Added Accommodation.Pet_Allowed';
END
GO

IF COL_LENGTH('dbo.Accommodation', 'Pet_Max_Per_Room') IS NULL
BEGIN
    ALTER TABLE [dbo].[Accommodation] ADD [Pet_Max_Per_Room] INT NOT NULL
        CONSTRAINT DF_Accommodation_Pet_Max_Per_Room DEFAULT (0);
    PRINT 'Added Accommodation.Pet_Max_Per_Room';
END
GO

IF COL_LENGTH('dbo.Accommodation', 'Pet_Fee_Per_Pet') IS NULL
BEGIN
    ALTER TABLE [dbo].[Accommodation] ADD [Pet_Fee_Per_Pet] DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_Accommodation_Pet_Fee_Per_Pet DEFAULT (0);
    PRINT 'Added Accommodation.Pet_Fee_Per_Pet';
END
GO

-- ── 2) จำนวนสัตว์เลี้ยงต่อห้องที่จอง (ใบจองหลายห้อง = แยกตามห้อง) ─────────────────────
--   ชื่อ Room_Pet_Count (ไม่ใช่ Pet_Count) เพราะหลายหน้าดึง SELECT * จาก Reservation JOIN
--   Reservation_Accommodation — ชื่อซ้ำกับ Reservation.Pet_Count จะกลายเป็น Pet_Count1
IF COL_LENGTH('dbo.Reservation_Accommodation', 'Room_Pet_Count') IS NULL
BEGIN
    ALTER TABLE [dbo].[Reservation_Accommodation] ADD [Room_Pet_Count] INT NULL;
    PRINT 'Added Reservation_Accommodation.Room_Pet_Count';
END
GO

-- ── 3) สรุปบนใบจอง (NULL = ไม่มีสัตว์เลี้ยง / จองก่อนมีฟีเจอร์) ───────────────────────
IF COL_LENGTH('dbo.Reservation', 'Pet_Count') IS NULL
BEGIN
    ALTER TABLE [dbo].[Reservation] ADD [Pet_Count] INT NULL;
    PRINT 'Added Reservation.Pet_Count';
END
GO

IF COL_LENGTH('dbo.Reservation', 'Pet_Fee_Total') IS NULL
BEGIN
    ALTER TABLE [dbo].[Reservation] ADD [Pet_Fee_Total] DECIMAL(18,2) NULL;
    PRINT 'Added Reservation.Pet_Fee_Total';
END
GO

IF COL_LENGTH('dbo.Reservation', 'Pet_Notes') IS NULL
BEGIN
    ALTER TABLE [dbo].[Reservation] ADD [Pet_Notes] NVARCHAR(500) NULL;
    PRINT 'Added Reservation.Pet_Notes';
END
GO

IF COL_LENGTH('dbo.Reservation', 'Pet_Policy_Accepted_At') IS NULL
BEGIN
    ALTER TABLE [dbo].[Reservation] ADD [Pet_Policy_Accepted_At] DATETIME NULL;
    PRINT 'Added Reservation.Pet_Policy_Accepted_At';
END
GO

IF COL_LENGTH('dbo.Reservation', 'Pet_Policy_Version') IS NULL
BEGIN
    ALTER TABLE [dbo].[Reservation] ADD [Pet_Policy_Version] INT NULL;
    PRINT 'Added Reservation.Pet_Policy_Version';
END
GO

-- ── 4) ค่าบริการสัตว์เลี้ยงเป็นค่าใช้จ่ายในห้อง — ระบุห้องที่มาของแถว ───────────────────
IF OBJECT_ID('dbo.Reservation_Product_Charges', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Reservation_Product_Charges', 'Pet_Accommodation_ID') IS NULL
BEGIN
    ALTER TABLE [dbo].[Reservation_Product_Charges] ADD [Pet_Accommodation_ID] INT NULL;
    PRINT 'Added Reservation_Product_Charges.Pet_Accommodation_ID';
END
GO

-- ค่าสัตว์เลี้ยงไม่ใช่สินค้าในสต๊อก → Product_ID ต้องเป็น NULL ได้ (PHASE18_15 ทำไว้แล้ว — กันกรณียังไม่ได้รัน)
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.Reservation_Product_Charges')
             AND name = 'Product_ID' AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[Reservation_Product_Charges] ALTER COLUMN [Product_ID] INT NULL;
    PRINT 'Reservation_Product_Charges.Product_ID -> NULLable';
END
GO

-- ── 5) ตารางค่าตั้ง/นโยบาย (ปกติสร้างแล้วใน migration 22 — สร้างให้เองถ้ายังไม่มี) ─────────
IF OBJECT_ID('dbo.Booking_Policy_Config', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Booking_Policy_Config] (
        [Config_Key]    NVARCHAR(100) NOT NULL PRIMARY KEY,
        [Config_Value]  NVARCHAR(MAX) NULL,
        [Modified_Date] DATETIME NULL,
        [Modified_By]   NVARCHAR(100) NULL
    );
    PRINT 'Created Booking_Policy_Config';
END
GO

IF OBJECT_ID('dbo.Booking_Pet_Policy_History', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Booking_Pet_Policy_History] (
        [ID]           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Version]      INT NOT NULL,
        [Policy_Pet]   NVARCHAR(MAX) NULL,
        [Created_Date] DATETIME NOT NULL CONSTRAINT DF_Booking_Pet_Policy_History_Created DEFAULT (GETDATE()),
        [Created_By]   NVARCHAR(100) NULL
    );
    CREATE INDEX IX_Booking_Pet_Policy_History_Version ON [dbo].[Booking_Pet_Policy_History] ([Version]);
    PRINT 'Created Booking_Pet_Policy_History';
END
GO

-- ── 6) ค่าตั้งต้น (เฉพาะคีย์ที่ยังไม่มี) ─────────────────────────────────────────────
MERGE [dbo].[Booking_Policy_Config] AS t
USING (VALUES
    (N'Pet_Enabled', N'0'),
    (N'Pet_Fee_Unit', N'NIGHT'),
    (N'Policy_Pet_Version', N'1'),
    (N'Policy_Pet', N'⚠ ตัวอย่าง — แก้ไขตามนโยบายจริงก่อนเปิดใช้งาน

นโยบายการนำสัตว์เลี้ยงเข้าพัก (Pet Policy)
1. รับเฉพาะ [แก้ไข: สุนัข / แมว] น้ำหนักไม่เกิน [แก้ไข: 15] กก. ต่อตัว ไม่เกินจำนวนสูงสุดที่ห้องพักกำหนด
2. ต้องแจ้งจำนวนสัตว์เลี้ยงตอนจอง — สัตว์เลี้ยงที่ไม่ได้แจ้งมีค่าปรับ [แก้ไข: 500] บาท/ตัว/คืน
3. สัตว์เลี้ยงต้องได้รับวัคซีนครบ (โดยเฉพาะพิษสุนัขบ้า) และแสดงสมุดวัคซีนเมื่อเจ้าหน้าที่ขอดู
4. ห้ามนำสัตว์เลี้ยงขึ้นเตียง/โซฟา และห้ามอาบน้ำสัตว์เลี้ยงในห้องน้ำของห้องพัก
5. ห้ามปล่อยสัตว์เลี้ยงไว้ในห้องตามลำพัง [แก้ไข: เกิน 2 ชั่วโมง] และต้องจูงสายจูงทุกครั้งในพื้นที่ส่วนกลาง
6. งดนำสัตว์เลี้ยงเข้า [แก้ไข: ห้องอาหาร / สระว่ายน้ำ]
7. เจ้าของต้องเก็บมูลและทำความสะอาดทันที
8. ความเสียหาย กลิ่น หรือขนที่ต้องทำความสะอาดพิเศษ คิดค่าใช้จ่ายตามจริง (ขั้นต่ำ [แก้ไข: 1,000] บาท)
9. หากสัตว์เลี้ยงส่งเสียงรบกวนหรือก่อความไม่สะดวกแก่ผู้เข้าพักอื่น ที่พักขอสงวนสิทธิ์ให้ย้ายออกโดยไม่คืนเงิน
10. ค่าบริการสัตว์เลี้ยงคิดตามอัตราที่แสดงบนหน้าจอง')
) AS s (Config_Key, Config_Value)
ON t.Config_Key = s.Config_Key
WHEN NOT MATCHED THEN
    INSERT (Config_Key, Config_Value, Modified_Date, Modified_By)
    VALUES (s.Config_Key, s.Config_Value, GETDATE(), N'MIGRATION');
GO

-- สำเนาฉบับแรก (ครั้งแรกเท่านั้น)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Booking_Pet_Policy_History])
BEGIN
    INSERT INTO [dbo].[Booking_Pet_Policy_History] ([Version], [Policy_Pet], [Created_By])
    SELECT ISNULL(TRY_CONVERT(INT, (SELECT Config_Value FROM [dbo].[Booking_Policy_Config] WHERE Config_Key = N'Policy_Pet_Version')), 1),
           (SELECT Config_Value FROM [dbo].[Booking_Policy_Config] WHERE Config_Key = N'Policy_Pet'),
           N'MIGRATION';
    PRINT 'Seeded Booking_Pet_Policy_History version 1';
END
GO

SELECT ID, AccomName, Pet_Allowed, Pet_Max_Per_Room, Pet_Fee_Per_Pet
  FROM [dbo].[Accommodation]
 WHERE [Status] = 1
 ORDER BY OrderID;

SELECT Config_Key, LEFT(Config_Value, 60) AS Value_Preview, Modified_Date, Modified_By
  FROM [dbo].[Booking_Policy_Config]
 WHERE Config_Key IN (N'Pet_Enabled', N'Pet_Fee_Unit', N'Policy_Pet', N'Policy_Pet_Version');
GO
