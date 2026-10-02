-- ============================================================================
-- PHASE19 Migration 09 — เกตเวย์ Omise + วงเงินประกันความเสียหาย + สวิตช์รายช่องทาง
-- ============================================================================
-- ของใหม่ 4 เรื่อง:
--   1. เกตเวย์ Omise (สลับกับ Payso ได้ด้วยค่า Payment_Provider — โค้ดออกแบบเป็น
--      interface เดียว เปลี่ยนเจ้าในอนาคตได้โดยไม่แตะหน้าจอ)
--   2. วงเงินประกันความเสียหาย: กันวงเงินบนบัตร (ไม่ตัดเงิน) → เช็คเอาท์ค่อยตัดเฉพาะ
--      ค่าเสียหายจริงหรือคืนทั้งหมด — แทนวิธีโอนเข้าบัญชีส่วนตัว/คืนเงินสด
--   3. เปิด/ปิดการรับเงินออนไลน์ "รายช่องทาง" (จอง กิจกรรม POS รูมเซอร์วิส ฯลฯ)
--   4. แหล่งเงิน "Omise (จ่ายออนไลน์)" ใน Account_Paid_How — ผูกบัญชีพักเงินใน NextAcc
--      แล้วเอกสารบัญชีทุกใบเดินเส้นทางใบเสร็จเดิม (Dr เข้าบัญชีพักเงินถูกต้องเอง)
--
-- ⚠ ปิดอยู่ = ระบบเดิมทำงานเหมือนเดิมทุกอย่าง:
--   Feature_OnlinePayment ยังเป็นสวิตช์ใหญ่ (ปิดเป็นค่าเริ่มต้น), Omise_Enabled ปิด,
--   Payment_SecurityHold_Enabled ปิด — ไฟล์นี้เพิ่มตาราง/ค่าตั้งเท่านั้น ไม่แก้ของเดิม
--
-- รันซ้ำได้ · ต้องรัน PHASE19_05 (ระบบชำระเงิน) มาก่อน
-- ============================================================================

SET NOCOUNT ON;
GO

-- ── 1) ตารางวงเงินประกันความเสียหาย ─────────────────────────────────────────
IF OBJECT_ID('dbo.Payment_Security_Holds', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payment_Security_Holds (
        ID                 BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Hold_Ref           NVARCHAR(50)   NOT NULL,   -- อ้างอิงฝั่งเรา (อยู่ในลิงก์ที่ส่งลูกค้า)
        Reservation_ID     INT            NOT NULL,
        Provider           NVARCHAR(30)   NOT NULL,   -- OMISE / ...
        Provider_Charge_ID NVARCHAR(100)  NULL,       -- chrg_... ของเกตเวย์
        Amount             DECIMAL(18,2)  NOT NULL,   -- วงเงินที่กัน
        Captured_Amount    DECIMAL(18,2)  NULL,       -- ที่ตัดจริง (ค่าเสียหาย)
        -- PENDING_CARD → HELD → CAPTURED / RELEASED / EXPIRED / FAILED
        [Status]           NVARCHAR(20)   NOT NULL DEFAULT 'PENDING_CARD',
        Card_Brand         NVARCHAR(30)   NULL,
        Card_Last4         NVARCHAR(8)    NULL,
        Held_At            DATETIME       NULL,
        Expires_At         DATETIME       NULL,       -- Omise = 7 วันหลังกันวงเงิน
        Expiry_Warned      BIT            NOT NULL DEFAULT 0,
        Captured_At        DATETIME       NULL,
        Captured_By        INT            NULL,
        Capture_Reason     NVARCHAR(400)  NULL,
        Released_At        DATETIME       NULL,
        Released_By        INT            NULL,
        Raw_Response       NVARCHAR(MAX)  NULL,
        Created_Date       DATETIME       NOT NULL DEFAULT GETDATE(),
        Created_By         INT            NULL,
        Updated_Date       DATETIME       NULL
    );
    CREATE UNIQUE INDEX UX_Security_Holds_Ref ON dbo.Payment_Security_Holds (Hold_Ref);
    CREATE INDEX IX_Security_Holds_Res ON dbo.Payment_Security_Holds (Reservation_ID, [Status]);
    CREATE INDEX IX_Security_Holds_Status ON dbo.Payment_Security_Holds ([Status], Expires_At);
    PRINT N'สร้างตาราง Payment_Security_Holds';
END
ELSE PRINT N'มี Payment_Security_Holds อยู่แล้ว — ข้าม';
GO

-- ── 2) ค่าตั้งใหม่ใน Payment_Gateway_Config ────────────────────────────────
IF OBJECT_ID('dbo.Payment_Gateway_Config', 'U') IS NULL
BEGIN
    PRINT N'⚠ ยังไม่มีตาราง Payment_Gateway_Config — รัน PHASE19_Migration_05 ก่อน แล้วรันไฟล์นี้ซ้ำ';
    RETURN;
END

DECLARE @cfg TABLE (K NVARCHAR(80), V NVARCHAR(MAX), S BIT, Cat NVARCHAR(60),
                    DN NVARCHAR(200), D NVARCHAR(1000), IT NVARCHAR(20), Opt NVARCHAR(500), Ord INT);

INSERT INTO @cfg (K, V, S, Cat, DN, D, IT, Opt, Ord) VALUES
-- ── เลือกผู้ให้บริการ ──
('Payment_Provider', 'OMISE', 0, N'ทั่วไป', N'ผู้ให้บริการเกตเวย์',
 N'OMISE (แนะนำ — รองรับกันวงเงินประกัน) หรือ PAYSO. เปลี่ยนได้ทันทีไม่ต้อง build ใหม่ รายการเก่ายังตรวจสอบกับเจ้าเดิมได้',
 'select', 'OMISE,PAYSO', 25),
('Payment_PaidHow_Name', N'Omise (จ่ายออนไลน์)', 0, N'ทั่วไป', N'ชื่อแหล่งเงินของเกตเวย์',
 N'ต้องตรงกับชื่อแถวใน Account_Paid_How เป๊ะ ๆ — ระบบใช้ชื่อนี้บันทึกทุกยอดที่รับผ่านเกตเวย์ เพื่อให้ NextAcc ลงบัญชีพักเงินถูกฝั่ง',
 'text', NULL, 26),

-- ── Omise ──
('Omise_Enabled', '0', 0, N'Omise', N'เปิดใช้เกตเวย์ Omise',
 N'ต้องเปิดพร้อม "เปิดรับชำระเงินออนไลน์" ด้วย', 'bool', NULL, 150),
('Omise_PublicKey', '', 0, N'Omise', N'Public Key (pkey_...)',
 N'ใช้ฝั่งเบราว์เซอร์สร้าง token บัตร — คีย์ test/live ดูจากตัวคีย์เอง ไม่ต้องตั้งโหมดแยก', 'text', NULL, 160),
('Omise_SecretKey', '', 1, N'Omise', N'Secret Key (skey_...)',
 N'ใช้ฝั่งเซิร์ฟเวอร์ เก็บแบบเข้ารหัส — ขึ้นต้น skey_test_ = โหมดทดสอบ', 'password', NULL, 170),

-- ── วงเงินประกันความเสียหาย ──
('Payment_SecurityHold_Enabled', '0', 0, N'วงเงินประกันความเสียหาย', N'เปิดใช้วงเงินประกัน (กันวงเงินบนบัตร)',
 N'แทนการให้โอนเข้าบัญชี: กันวงเงินไว้เฉย ๆ ไม่มีเงินเข้า-ออก เช็คเอาท์ค่อยตัดเฉพาะค่าเสียหายจริงหรือคืนทั้งหมด — ใช้ได้กับ Omise + บัตรเท่านั้น (PromptPay กันวงเงินไม่ได้)',
 'bool', NULL, 700),
('Payment_SecurityHold_Default', '1000', 0, N'วงเงินประกันความเสียหาย', N'วงเงินแนะนำ (บาท)',
 N'ตัวเลขตั้งต้นที่หน้าจุดรับเงินจะใส่ให้ แก้ได้ทุกครั้ง', 'number', NULL, 710),
('Payment_SecurityHold_WarnHours', '24', 0, N'วงเงินประกันความเสียหาย', N'เตือนก่อนวงเงินหมดอายุ (ชั่วโมง)',
 N'วงเงิน Omise อยู่ได้ 7 วันแล้วคืนลูกค้าอัตโนมัติ — ระบบแจ้งเตือนล่วงหน้าตามค่านี้ให้ตัดสินใจก่อน', 'number', NULL, 720),

-- ── เปิด/ปิดรายช่องทาง (1 = เปิด) — มีผลเฉพาะเมื่อสวิตช์ใหญ่เปิดอยู่ ──
('Payment_Channel_RESERVATION', '1', 0, N'ช่องทางที่เปิดรับเงินออนไลน์', N'การจองที่พัก / ยอดค้างของการจอง',
 N'หน้า Pay ที่ส่งลิงก์ให้ลูกค้า + ปุ่มในหน้าชำระเงินเพิ่ม', 'bool', NULL, 800),
('Payment_Channel_ACTIVITY', '1', 0, N'ช่องทางที่เปิดรับเงินออนไลน์', N'จองกิจกรรม',
 N'ตัวเลือก "จ่ายออนไลน์" ในหน้าจองกิจกรรมของแขก', 'bool', NULL, 810),
('Payment_Channel_POS', '1', 0, N'ช่องทางที่เปิดรับเงินออนไลน์', N'ขายหน้าร้าน (จุดรับเงิน)',
 N'หน้า "จุดรับเงินออนไลน์" ที่พนักงานสร้าง QR/ลิงก์ให้ลูกค้าจ่ายก่อนบันทึกขาย', 'bool', NULL, 820),
('Payment_Channel_ROOMSERVICE', '1', 0, N'ช่องทางที่เปิดรับเงินออนไลน์', N'รูมเซอร์วิส',
 N'ลิงก์จ่ายออนไลน์ของออเดอร์รูมเซอร์วิส (ที่ยังไม่ชาร์จเข้าห้อง)', 'bool', NULL, 830),
('Payment_Channel_AMENITY', '1', 0, N'ช่องทางที่เปิดรับเงินออนไลน์', N'เบิกของใช้',
 N'สำรองไว้ — จะมีผลเมื่อหน้าเบิกของต่อระบบชำระเงิน', 'bool', NULL, 840),
('Payment_Channel_DAMAGE', '1', 0, N'ช่องทางที่เปิดรับเงินออนไลน์', N'ค่าเสียหาย (จากวงเงินประกัน)',
 N'การตัดค่าเสียหายจากวงเงินที่กันไว้', 'bool', NULL, 850),
('Payment_Channel_OTHER', '1', 0, N'ช่องทางที่เปิดรับเงินออนไลน์', N'ยอดอื่น ๆ',
 N'รายการที่พนักงานสร้างเองจากจุดรับเงิน', 'bool', NULL, 860);

INSERT INTO dbo.Payment_Gateway_Config
    (Config_Key, Config_Value, Is_Secret, Category, Display_Name, [Description], Input_Type, Options, Display_Order)
SELECT c.K, c.V, c.S, c.Cat, c.DN, c.D, c.IT, c.Opt, c.Ord
FROM @cfg c
WHERE NOT EXISTS (SELECT 1 FROM dbo.Payment_Gateway_Config p WHERE p.Config_Key = c.K);
PRINT N'เพิ่มค่าตั้งใหม่: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' คีย์';

UPDATE p
   SET p.Category = c.Cat, p.Display_Name = c.DN, p.[Description] = c.D,
       p.Input_Type = c.IT, p.Options = c.Opt, p.Display_Order = c.Ord, p.Is_Secret = c.S
FROM dbo.Payment_Gateway_Config p
JOIN @cfg c ON c.K = p.Config_Key;
GO

-- ── 3) แหล่งเงิน "Omise (จ่ายออนไลน์)" ──────────────────────────────────────
-- ทุกยอดที่รับผ่านเกตเวย์ใช้แหล่งเงินนี้ → ไปผูกบัญชีพักเงิน (เช่น 11xx เงินรอรับจาก Omise)
-- ที่ Admin → ตั้งค่า → NextAcc → แหล่งเงิน แล้วเอกสารบัญชีทุกใบ Dr ถูกฝั่งเอง
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Account_Paid_How WHERE Paid_How = N'Omise (จ่ายออนไลน์)')
    BEGIN
        INSERT INTO dbo.Account_Paid_How (Paid_How, Status) VALUES (N'Omise (จ่ายออนไลน์)', 'True');
        PRINT N'เพิ่มแหล่งเงิน "Omise (จ่ายออนไลน์)" — อย่าลืมผูกบัญชีพักเงิน NextAcc ในหน้า Accounting Integration';
    END
    ELSE PRINT N'มีแหล่งเงิน Omise อยู่แล้ว — ข้าม';
END
GO

-- ── 4) เหตุการณ์แจ้งเตือน "วงเงินประกัน" ────────────────────────────────────
IF OBJECT_ID('dbo.Notification_Rules', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Notification_Rules WHERE Event_Code = 'PAYMENT_HOLD' AND Channel = 'TELEGRAM')
        INSERT INTO dbo.Notification_Rules (Event_Code, Channel, Enabled, Modified_Date)
        VALUES ('PAYMENT_HOLD', 'TELEGRAM', 1, GETDATE());
    IF NOT EXISTS (SELECT 1 FROM dbo.Notification_Rules WHERE Event_Code = 'PAYMENT_HOLD' AND Channel = 'LINE')
        INSERT INTO dbo.Notification_Rules (Event_Code, Channel, Enabled, Modified_Date)
        VALUES ('PAYMENT_HOLD', 'LINE', 0, GETDATE());
    PRINT N'เพิ่มกฎแจ้งเตือน PAYMENT_HOLD (Telegram เปิด — เรื่องเงิน ควรรู้เสมอ)';
END
GO

-- ── ตรวจผล ─────────────────────────────────────────────────────────────────
SELECT N'Payment_Security_Holds' AS [ตาราง],
       CASE WHEN OBJECT_ID('dbo.Payment_Security_Holds','U') IS NULL THEN N'❌' ELSE N'✅' END AS [สถานะ];
SELECT Config_Key, LEFT(ISNULL(Config_Value,''), 30) AS Value
  FROM dbo.Payment_Gateway_Config
 WHERE Config_Key IN ('Payment_Provider','Omise_Enabled','Payment_SecurityHold_Enabled')
 ORDER BY Config_Key;

PRINT '';
PRINT N'ลำดับเปิดใช้งาน: ใส่คีย์ Omise (โหมด test) → ทดสอบการเชื่อมต่อ → ตั้ง Webhook ใน';
PRINT N'Omise Dashboard → ผูกบัญชีพักเงินให้แหล่งเงิน Omise → เปิดสวิตช์ทีละตัว';
PRINT N'ยังไม่เปิดสวิตช์ = ระบบเดิมทำงานเหมือนเดิมทุกประการ';
