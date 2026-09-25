-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 22: นโยบายการจอง (ข้อกำหนดและเงื่อนไข / ความเป็นส่วนตัว /
--                        การคืนเงิน / การยกเลิก) + บันทึกการยอมรับบนใบจอง
-- ════════════════════════════════════════════════════════════════════════════
-- หน้าจอง (Reserve.aspx) แสดงนโยบาย + ให้ลูกค้าติ๊ก "ยอมรับเงื่อนไข" ก่อนกดยืนยันการจอง
-- หน้า Reservation_Confirmed แสดงนโยบายชุดเดียวกัน + วันเวลา/ฉบับที่ลูกค้ายอมรับ
-- แก้ข้อความได้ที่ ศูนย์ตั้งค่า → นโยบายการจอง (Admin/Settings/BookingPolicies.aspx)
--
-- ทำไมไม่เก็บใน System_Config: หน้า SystemSettings วาดทุกแถวของ System_Config เป็น
-- <input type=text> บรรทัดเดียว — กดบันทึกหน้านั้นครั้งเดียว ข้อความหลายบรรทัดจะถูกตัด
-- ขึ้นบรรทัดทิ้งทับค่าเดิม ⇒ แยกตารางของตัวเอง (Booking_Policy_Config)
--
--   Booking_Policy_Config   ค่าปัจจุบัน (Policy_Terms / Policy_Privacy / Policy_Refund /
--                           Policy_Cancellation / Policy_Version)
--   Booking_Policy_History  สำเนาข้อความทุกฉบับ (ย้อนดูได้ว่าลูกค้ายอมรับฉบับไหนว่าอะไร)
--   Reservation.Policy_Accepted_At / Policy_Accepted_Version / Policy_Accepted_IP
--
-- ข้อความตั้งต้นเป็นตัวอย่าง — ต้อง "แก้ไขตามนโยบายจริง" ก่อนเปิดใช้งาน
-- seed เฉพาะคีย์ที่ยังไม่มี (ไม่ทับข้อความที่แก้ไว้แล้ว)
-- idempotent — รันซ้ำได้, ไม่ลบข้อมูลเดิม
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

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
ELSE
    PRINT 'Booking_Policy_Config already exists';
GO

IF OBJECT_ID('dbo.Booking_Policy_History', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Booking_Policy_History] (
        [ID]                  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Version]             INT NOT NULL,
        [Policy_Terms]        NVARCHAR(MAX) NULL,
        [Policy_Privacy]      NVARCHAR(MAX) NULL,
        [Policy_Refund]       NVARCHAR(MAX) NULL,
        [Policy_Cancellation] NVARCHAR(MAX) NULL,
        [Created_Date]        DATETIME NOT NULL CONSTRAINT DF_Booking_Policy_History_Created DEFAULT (GETDATE()),
        [Created_By]          NVARCHAR(100) NULL
    );
    CREATE INDEX IX_Booking_Policy_History_Version ON [dbo].[Booking_Policy_History] ([Version]);
    PRINT 'Created Booking_Policy_History';
END
ELSE
    PRINT 'Booking_Policy_History already exists';
GO

-- ── คอลัมน์บันทึกการยอมรับบนใบจอง (NULL = จองโดยพนักงาน / จองก่อนมีฟีเจอร์นี้) ─────────
IF COL_LENGTH('dbo.Reservation', 'Policy_Accepted_At') IS NULL
BEGIN
    ALTER TABLE [dbo].[Reservation] ADD [Policy_Accepted_At] DATETIME NULL;
    PRINT 'Added Reservation.Policy_Accepted_At';
END
GO

IF COL_LENGTH('dbo.Reservation', 'Policy_Accepted_Version') IS NULL
BEGIN
    ALTER TABLE [dbo].[Reservation] ADD [Policy_Accepted_Version] INT NULL;
    PRINT 'Added Reservation.Policy_Accepted_Version';
END
GO

IF COL_LENGTH('dbo.Reservation', 'Policy_Accepted_IP') IS NULL
BEGIN
    ALTER TABLE [dbo].[Reservation] ADD [Policy_Accepted_IP] NVARCHAR(64) NULL;
    PRINT 'Added Reservation.Policy_Accepted_IP';
END
GO

-- ── ข้อความตั้งต้น (เฉพาะคีย์ที่ยังไม่มี) ──────────────────────────────────────────────
MERGE [dbo].[Booking_Policy_Config] AS t
USING (VALUES
    (N'Policy_Terms', N'⚠ ตัวอย่าง — แก้ไขตามนโยบายจริงก่อนเปิดใช้งาน

ข้อกำหนดและเงื่อนไขการจองห้องพัก
1. การจองจะสมบูรณ์เมื่อที่พักได้รับยอดมัดจำหรือยอดชำระเต็มจำนวนแล้ว และลูกค้าได้รับหน้ายืนยันการจอง
2. ยอดมัดจำขั้นต่ำเป็นไปตามที่แสดงบนหน้าจอง ยอดคงเหลือชำระ ณ วันเช็คอิน
3. เวลาเช็คอิน [แก้ไข: 14:00 น.] / เวลาเช็คเอาท์ [แก้ไข: 12:00 น.]
4. ผู้เข้าพักต้องแสดงบัตรประชาชนหรือหนังสือเดินทางตอนเช็คอิน
5. จำนวนผู้เข้าพักต้องไม่เกินที่ระบุในการจอง ผู้เข้าพักเพิ่มมีค่าใช้จ่ายตามอัตราของที่พัก
6. งดใช้เสียงดังหลังเวลา 22.30 น. และปฏิบัติตามกฎระเบียบของที่พัก
7. ความเสียหายต่อทรัพย์สินของที่พัก ผู้เข้าพักต้องรับผิดชอบตามจริง
8. ที่พักขอสงวนสิทธิ์ในการเปลี่ยนแปลงเงื่อนไขโดยจะแจ้งให้ทราบล่วงหน้า'),

    (N'Policy_Privacy', N'⚠ ตัวอย่าง — แก้ไขตามนโยบายจริงก่อนเปิดใช้งาน

นโยบายความเป็นส่วนตัว (PDPA)
1. ข้อมูลที่เก็บ: ชื่อ-นามสกุล เบอร์โทรศัพท์ ที่อยู่/เลขประจำตัวผู้เสียภาษี (กรณีขอใบกำกับภาษี) หลักฐานการชำระเงิน
2. วัตถุประสงค์: ยืนยันและจัดการการจอง ออกใบเสร็จ/ใบกำกับภาษี ติดต่อเรื่องการเข้าพัก และปฏิบัติตามกฎหมาย
3. ข้อมูลบัตรเครดิตถูกประมวลผลโดยผู้ให้บริการรับชำระเงิน ที่พักไม่ได้จัดเก็บเลขบัตรเต็ม
4. ไม่เปิดเผยข้อมูลแก่บุคคลภายนอก ยกเว้นผู้ให้บริการที่จำเป็นต่อการให้บริการหรือตามที่กฎหมายกำหนด
5. ระยะเวลาเก็บรักษา: [แก้ไข: ตามที่กฎหมายบัญชี/ภาษีกำหนด]
6. ลูกค้าขอเข้าถึง แก้ไข หรือลบข้อมูลได้ที่ [แก้ไข: ช่องทางติดต่อ]'),

    (N'Policy_Refund', N'⚠ ตัวอย่าง — แก้ไขตามนโยบายจริงก่อนเปิดใช้งาน

นโยบายการคืนเงิน
1. การคืนเงินเป็นไปตามนโยบายการยกเลิกการจอง
2. ชำระด้วยการโอน: คืนเข้าบัญชีธนาคารที่ลูกค้าแจ้ง ภายใน [แก้ไข: 7-14] วันทำการ
3. ชำระด้วยบัตรเครดิต/ช่องทางออนไลน์: คืนผ่านช่องทางเดิม ระยะเวลาขึ้นกับธนาคารผู้ออกบัตร (ประมาณ [แก้ไข: 7-30] วัน)
4. ค่าธรรมเนียมการชำระเงินออนไลน์ [แก้ไข: คืน/ไม่คืน]
5. กรณีเลื่อนวันเข้าพัก สามารถโอนยอดมัดจำไปใช้กับวันใหม่ได้ตามเงื่อนไขของที่พัก'),

    (N'Policy_Cancellation', N'⚠ ตัวอย่าง — แก้ไขตามนโยบายจริงก่อนเปิดใช้งาน

นโยบายการยกเลิกการจอง (ใช้กับทุกช่องทางการชำระเงิน)
• ยกเลิกก่อนวันเข้าพัก [แก้ไข: 7] วันขึ้นไป — คืนเงินมัดจำ [แก้ไข: 100%]
• ยกเลิกก่อนวันเข้าพัก [แก้ไข: 3-6] วัน — คืนเงินมัดจำ [แก้ไข: 50%] หรือเลื่อนวันเข้าพักได้ [แก้ไข: 1] ครั้ง
• ยกเลิกภายใน [แก้ไข: 2] วันก่อนเข้าพัก หรือไม่มาเข้าพัก (No-show) — ไม่คืนเงินมัดจำ
• ช่วงเทศกาล/วันหยุดยาว [แก้ไข: ไม่สามารถยกเลิกหรือคืนเงินได้]
• แจ้งยกเลิกหรือเลื่อนวันผ่าน [แก้ไข: LINE / โทรศัพท์] พร้อมหมายเลขการจอง'),

    (N'Policy_Version', N'1')
) AS s (Config_Key, Config_Value)
ON t.Config_Key = s.Config_Key
WHEN NOT MATCHED THEN
    INSERT (Config_Key, Config_Value, Modified_Date, Modified_By)
    VALUES (s.Config_Key, s.Config_Value, GETDATE(), N'MIGRATION');
GO

-- สำเนาฉบับที่ 1 (ครั้งแรกเท่านั้น)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Booking_Policy_History])
BEGIN
    INSERT INTO [dbo].[Booking_Policy_History]
        ([Version], [Policy_Terms], [Policy_Privacy], [Policy_Refund], [Policy_Cancellation], [Created_By])
    SELECT
        ISNULL(TRY_CONVERT(INT, (SELECT Config_Value FROM [dbo].[Booking_Policy_Config] WHERE Config_Key = N'Policy_Version')), 1),
        (SELECT Config_Value FROM [dbo].[Booking_Policy_Config] WHERE Config_Key = N'Policy_Terms'),
        (SELECT Config_Value FROM [dbo].[Booking_Policy_Config] WHERE Config_Key = N'Policy_Privacy'),
        (SELECT Config_Value FROM [dbo].[Booking_Policy_Config] WHERE Config_Key = N'Policy_Refund'),
        (SELECT Config_Value FROM [dbo].[Booking_Policy_Config] WHERE Config_Key = N'Policy_Cancellation'),
        N'MIGRATION';
    PRINT 'Seeded Booking_Policy_History version 1';
END
GO

SELECT Config_Key, LEN(Config_Value) AS Chars, Modified_Date, Modified_By
  FROM [dbo].[Booking_Policy_Config]
 ORDER BY Config_Key;
GO
