-- ============================================================================
-- PHASE19 Migration 20 — แคตตาล็อกช่องทางชำระเงิน + PaySo เป็นเกตเวย์หลัก
--                       + เงินประกันความเสียหายรับโดยการโอน (นอกเกตเวย์)
-- ============================================================================
-- 1. Account_Paid_How + คอลัมน์แคตตาล็อก (Channel_Code, Channel_Type, Gateway_Provider,
--    Gateway_Channel_Code, Customer_Visible, Staff_Visible, Instructions, Conditions,
--    Qr_Image_Url, Requires_Slip, Sort_Order) — โค้ด: Class/Payments/PaymentChannelCatalog.cs
--    • backfill เฉพาะแถวที่ยังไม่จัดชนิด (Channel_Type IS NULL) ตามชื่อ:
--        ทดรอง/กรรมการ → DIRECTOR_LOAN (พนักงานเท่านั้น)
--        Omise         → CARD provider OMISE (ลูกค้าไม่เห็น)
--        Agoda/Booking.com/Expedia/Trip.com/Traveloka/Airbnb/OTA → OTA (พนักงานเท่านั้น)
--        เงินสด/Cash   → CASH (พนักงานเท่านั้น)
--        โอน           → TRANSFER ลูกค้าเห็น + บังคับสลิป
--        ธนาคาร/บัญชี/พร้อมเพย์/Bank/Transfer → TRANSFER (ลูกค้ายังไม่เห็น — เปิดเองในหน้าช่องทาง) + บังคับสลิป
--        บัตร/Card/EDC → CARD (เครื่องรูดหน้าร้าน, พนักงานเท่านั้น) · อื่น ๆ → OTHER
--    • เพิ่มแถว PaySo: บัตรเครดิต VISA/Mastercard, บัตรเครดิต AMEX, พร้อมเพย์ (QR)
--      Customer_Visible = 1 แต่จะโผล่ให้ลูกค้าเห็น "เฉพาะเมื่อ" PaySo เปิด + พร้อม + เป็นเจ้าที่เลือก
--      + ช่องทางอยู่ใน Payso_Enabled_Channels + Payment_Methods_Enabled
--      มีแถวใน Account_Paid_How (Status='True') เพื่อผูกบัญชีพักเงิน NextAcc แยกรายช่องทางได้
--      (หน้า Accounting Integration → แหล่งเงิน) — ⚠ แถวเหล่านี้จะโผล่ใน dropdown แหล่งเงินของพนักงานด้วย
-- 2. Payment_Security_Holds + คอลัมน์เงินประกันโอน (Transfer_Ref/Note, Refund_*)
-- 3. Payment_Gateway_Config: Payment_Provider → PAYSO (หลัก) เว้นแต่ Omise ใช้งานอยู่จริง,
--    Payso_Enabled_Channels, Security_Hold_Mode (TRANSFER), Security_Hold_Transfer_Channel,
--    Security_Hold_Overdue_Days, Payment_Cancellation_Policy
--
-- ปลอดภัย: รันซ้ำได้ (COL_LENGTH/NOT EXISTS guard) · ไม่ลบข้อมูล · ไม่เขียนทับค่าที่ผู้ดูแลแก้แล้ว
-- ต้องรัน PHASE19_05 (ระบบชำระเงิน) + PHASE19_09 (วงเงินประกัน) มาก่อน
-- หลังรัน: recycle App Pool (หรือรอ 30 วิ ให้ cache หมดอายุ) แล้วเปิด ศูนย์ตั้งค่า → ช่องทางชำระเงิน
-- ============================================================================

SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NULL
BEGIN
    PRINT N'⚠ ไม่มีตาราง Account_Paid_How — ข้ามส่วนแคตตาล็อก';
END
GO

-- ── 1) คอลัมน์แคตตาล็อกใน Account_Paid_How ─────────────────────────────────
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Channel_Code') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Paid_How ADD Channel_Code NVARCHAR(50) NULL;
    PRINT N'เพิ่ม Account_Paid_How.Channel_Code';
END
GO
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Channel_Type') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Paid_How ADD Channel_Type NVARCHAR(20) NULL;
    PRINT N'เพิ่ม Account_Paid_How.Channel_Type';
END
GO
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Gateway_Provider') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Paid_How ADD Gateway_Provider NVARCHAR(20) NULL;
    PRINT N'เพิ่ม Account_Paid_How.Gateway_Provider';
END
GO
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Gateway_Channel_Code') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Paid_How ADD Gateway_Channel_Code NVARCHAR(30) NULL;
    PRINT N'เพิ่ม Account_Paid_How.Gateway_Channel_Code';
END
GO
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Customer_Visible') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Paid_How ADD Customer_Visible BIT NOT NULL
        CONSTRAINT DF_Account_Paid_How_Customer_Visible DEFAULT (0);
    PRINT N'เพิ่ม Account_Paid_How.Customer_Visible';
END
GO
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Staff_Visible') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Paid_How ADD Staff_Visible BIT NOT NULL
        CONSTRAINT DF_Account_Paid_How_Staff_Visible DEFAULT (1);
    PRINT N'เพิ่ม Account_Paid_How.Staff_Visible';
END
GO
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Instructions') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Paid_How ADD Instructions NVARCHAR(MAX) NULL;
    PRINT N'เพิ่ม Account_Paid_How.Instructions';
END
GO
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Conditions') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Paid_How ADD Conditions NVARCHAR(MAX) NULL;
    PRINT N'เพิ่ม Account_Paid_How.Conditions';
END
GO
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Qr_Image_Url') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Paid_How ADD Qr_Image_Url NVARCHAR(500) NULL;
    PRINT N'เพิ่ม Account_Paid_How.Qr_Image_Url';
END
GO
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Requires_Slip') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Paid_How ADD Requires_Slip BIT NOT NULL
        CONSTRAINT DF_Account_Paid_How_Requires_Slip DEFAULT (0);
    PRINT N'เพิ่ม Account_Paid_How.Requires_Slip';
END
GO
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Sort_Order') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Paid_How ADD Sort_Order INT NOT NULL
        CONSTRAINT DF_Account_Paid_How_Sort_Order DEFAULT (100);
    PRINT N'เพิ่ม Account_Paid_How.Sort_Order';
END
GO

-- ── 1b) แถว PaySo (เพิ่มก่อน backfill เพื่อให้จัดชนิดเฉพาะของมันเอง) ─────────
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Channel_Code') IS NOT NULL
BEGIN
    DECLARE @ps TABLE (Code NVARCHAR(50), Nm NVARCHAR(200), Tp NVARCHAR(20), Gc NVARCHAR(30),
                       Ins NVARCHAR(MAX), Cond NVARCHAR(MAX), Srt INT);
    INSERT INTO @ps VALUES
    ('PAYSO_VISA', N'PaySo บัตรเครดิต VISA / Mastercard', 'CARD', 'CARD',
     N'ชำระด้วยบัตรเครดิต/เดบิต VISA หรือ Mastercard ผ่านหน้าชำระเงินที่ปลอดภัยของ PaySo — ระบบยืนยันการจองให้อัตโนมัติเมื่อชำระสำเร็จ ไม่ต้องแนบสลิป',
     N'รับบัตร VISA / Mastercard (อาจต้องยืนยันตัวตน 3-D Secure กับธนาคาร) · ค่าธรรมเนียมตามที่ประกาศ (ถ้ามี) · การคืนเงินตามนโยบายยกเลิก จะคืนเข้าบัตรเดิมภายใน 7–30 วันทำการตามรอบของธนาคาร',
     30),
    ('PAYSO_AMEX', N'PaySo บัตรเครดิต AMEX', 'CARD', 'CARD',
     N'ชำระด้วยบัตร American Express ผ่านหน้าชำระเงินที่ปลอดภัยของ PaySo — ระบบยืนยันการจองให้อัตโนมัติเมื่อชำระสำเร็จ',
     N'รับบัตร American Express เท่านั้น · อาจมีค่าธรรมเนียมสูงกว่าบัตรทั่วไป · การคืนเงินตามนโยบายยกเลิก จะคืนเข้าบัตรเดิมตามรอบของผู้ออกบัตร',
     31),
    ('PAYSO_PROMPTPAY', N'PaySo พร้อมเพย์ (QR)', 'QR', 'QR',
     N'สแกน QR พร้อมเพย์ที่ระบบสร้างให้ด้วยแอปธนาคารใดก็ได้ — ระบบรู้ผลและยืนยันการจองให้อัตโนมัติ ไม่ต้องแนบสลิป',
     N'QR มีอายุจำกัด (ดูเวลาบนหน้าชำระเงิน) · ชำระตรงยอดที่แสดงเท่านั้น · การคืนเงินตามนโยบายยกเลิก จะโอนคืนเข้าบัญชีของผู้ชำระ',
     25);

    -- เพิ่มแถวที่ยังไม่มี (ดูทั้งรหัสและชื่อ กันซ้ำเมื่อรันซ้ำ/ผู้ดูแลเปลี่ยนรหัส)
    INSERT INTO dbo.Account_Paid_How (Paid_How, Status)
    SELECT p.Nm, 'True' FROM @ps p
     WHERE NOT EXISTS (SELECT 1 FROM dbo.Account_Paid_How a WHERE a.Channel_Code = p.Code)
       AND NOT EXISTS (SELECT 1 FROM dbo.Account_Paid_How a WHERE a.Paid_How = p.Nm);
    PRINT N'เพิ่มแถวแหล่งเงิน PaySo: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว (ผูกบัญชีพักเงิน NextAcc ได้ที่หน้า Accounting Integration)';

    -- ตั้งค่าแคตตาล็อกเฉพาะแถวที่ยังไม่จัดชนิด (ไม่ทับค่าที่ผู้ดูแลแก้แล้ว)
    UPDATE a
       SET a.Channel_Code = p.Code, a.Channel_Type = p.Tp,
           a.Gateway_Provider = 'PAYSO', a.Gateway_Channel_Code = p.Gc,
           a.Customer_Visible = 1, a.Staff_Visible = 1, a.Requires_Slip = 0,
           a.Instructions = p.Ins, a.Conditions = p.Cond, a.Sort_Order = p.Srt
      FROM dbo.Account_Paid_How a
      JOIN @ps p ON p.Nm = a.Paid_How
     WHERE a.Channel_Type IS NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.Account_Paid_How x WHERE x.Channel_Code = p.Code AND x.ID <> a.ID);
END
GO

-- ── 1c) backfill แถวเดิมตามชื่อ (เฉพาะแถวที่ยังไม่จัดชนิด) ──────────────────
-- ⚠ ต้องตรงกับ PaymentChannelCatalog.InferType ในโค้ด
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Channel_Type') IS NOT NULL
BEGIN
    UPDATE dbo.Account_Paid_How
       SET Channel_Type = CASE
               WHEN Paid_How LIKE N'%ทดรอง%' OR Paid_How LIKE N'%กรรมการ%' THEN 'DIRECTOR_LOAN'
               WHEN UPPER(Paid_How) LIKE N'%OMISE%' THEN 'CARD'
               WHEN UPPER(Paid_How) LIKE N'%PAYSO%' THEN 'GATEWAY_OTHER'
               WHEN UPPER(Paid_How) LIKE N'%AGODA%' OR UPPER(Paid_How) LIKE N'%BOOKING.COM%'
                 OR UPPER(Paid_How) LIKE N'%EXPEDIA%' OR UPPER(Paid_How) LIKE N'%TRIP.COM%'
                 OR UPPER(Paid_How) LIKE N'%TRAVELOKA%' OR UPPER(Paid_How) LIKE N'%AIRBNB%'
                 OR UPPER(LTRIM(RTRIM(Paid_How))) = N'OTA' OR UPPER(Paid_How) LIKE N'OTA %' THEN 'OTA'
               WHEN Paid_How LIKE N'%เงินสด%' OR UPPER(Paid_How) LIKE N'%CASH%' THEN 'CASH'
               WHEN Paid_How LIKE N'%โอน%' OR Paid_How LIKE N'%ธนาคาร%' OR Paid_How LIKE N'%พร้อมเพย์%'
                 OR Paid_How LIKE N'%บัญชี%' OR UPPER(Paid_How) LIKE N'%BANK%'
                 OR UPPER(Paid_How) LIKE N'%TRANSFER%' OR UPPER(Paid_How) LIKE N'%PROMPTPAY%' THEN 'TRANSFER'
               WHEN Paid_How LIKE N'%บัตร%' OR UPPER(Paid_How) LIKE N'%CARD%' OR UPPER(Paid_How) LIKE N'%EDC%' THEN 'CARD'
               ELSE 'OTHER'
           END,
           Gateway_Provider = CASE
               WHEN Paid_How LIKE N'%ทดรอง%' OR Paid_How LIKE N'%กรรมการ%' THEN NULL
               WHEN UPPER(Paid_How) LIKE N'%OMISE%' THEN 'OMISE'
               WHEN UPPER(Paid_How) LIKE N'%PAYSO%' THEN 'PAYSO'
               ELSE NULL END
     WHERE Channel_Type IS NULL;
    PRINT N'จัดชนิดแถวเดิม: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว';
END
GO

-- ค่าเริ่มต้นตามชนิด — ทำเฉพาะแถวที่ยังไม่มีรหัส (= เพิ่งจัดชนิดในรอบนี้)
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Channel_Code') IS NOT NULL
BEGIN
    UPDATE dbo.Account_Paid_How
       SET Customer_Visible = CASE WHEN Channel_Type = 'TRANSFER' AND Paid_How LIKE N'%โอน%' THEN 1 ELSE 0 END,
           Staff_Visible    = 1,
           Requires_Slip    = CASE WHEN Channel_Type = 'TRANSFER' THEN 1 ELSE 0 END,
           Sort_Order       = CASE Channel_Type
                                  WHEN 'TRANSFER' THEN 10 WHEN 'QR' THEN 20 WHEN 'CARD' THEN 40
                                  WHEN 'GATEWAY_OTHER' THEN 50 WHEN 'CASH' THEN 80
                                  WHEN 'DIRECTOR_LOAN' THEN 90 WHEN 'OTA' THEN 95 ELSE 100 END,
           Channel_Code     = CASE Channel_Type
                                  WHEN 'DIRECTOR_LOAN' THEN 'DIRECTOR_' + CAST(ID AS VARCHAR(12))
                                  ELSE Channel_Type + '_' + CAST(ID AS VARCHAR(12)) END
     WHERE Channel_Code IS NULL AND Channel_Type IS NOT NULL;
    PRINT N'ตั้งค่าเริ่มต้นรายชนิด: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว';

    -- กฎตายตัว: เงินสด / ทดรองกรรมการ / OTA / Omise(แหล่งเงินรวม) ลูกค้าไม่เห็น
    UPDATE dbo.Account_Paid_How SET Customer_Visible = 0
     WHERE Customer_Visible = 1 AND (Channel_Type IN ('CASH','DIRECTOR_LOAN','OTA')
        OR (Gateway_Provider = 'OMISE' AND Gateway_Channel_Code IS NULL));
END
GO

-- รหัสช่องทางห้ามซ้ำ (เว้น NULL)
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Channel_Code') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Account_Paid_How_Channel_Code'
                    AND object_id = OBJECT_ID('dbo.Account_Paid_How'))
BEGIN
    IF EXISTS (SELECT Channel_Code FROM dbo.Account_Paid_How WHERE Channel_Code IS NOT NULL
                GROUP BY Channel_Code HAVING COUNT(*) > 1)
        PRINT N'⚠ มี Channel_Code ซ้ำ — ยังไม่สร้าง unique index (แก้รหัสในหน้า "ช่องทางชำระเงิน" แล้วรันไฟล์นี้ซ้ำ)';
    ELSE
    BEGIN
        EXEC('CREATE UNIQUE INDEX UX_Account_Paid_How_Channel_Code ON dbo.Account_Paid_How (Channel_Code) WHERE Channel_Code IS NOT NULL');
        PRINT N'สร้าง unique index UX_Account_Paid_How_Channel_Code';
    END
END
GO

-- ── 2) เงินประกันโอน: คอลัมน์ใน Payment_Security_Holds ─────────────────────
IF OBJECT_ID('dbo.Payment_Security_Holds', 'U') IS NULL
    PRINT N'⚠ ยังไม่มี Payment_Security_Holds — รัน PHASE19_Migration_09 ก่อน แล้วรันไฟล์นี้ซ้ำ';
GO
IF OBJECT_ID('dbo.Payment_Security_Holds', 'U') IS NOT NULL AND COL_LENGTH('dbo.Payment_Security_Holds', 'Transfer_Ref') IS NULL
    ALTER TABLE dbo.Payment_Security_Holds ADD Transfer_Ref NVARCHAR(100) NULL;
GO
IF OBJECT_ID('dbo.Payment_Security_Holds', 'U') IS NOT NULL AND COL_LENGTH('dbo.Payment_Security_Holds', 'Transfer_Note') IS NULL
    ALTER TABLE dbo.Payment_Security_Holds ADD Transfer_Note NVARCHAR(400) NULL;
GO
IF OBJECT_ID('dbo.Payment_Security_Holds', 'U') IS NOT NULL AND COL_LENGTH('dbo.Payment_Security_Holds', 'Refund_Method') IS NULL
    ALTER TABLE dbo.Payment_Security_Holds ADD Refund_Method NVARCHAR(20) NULL;
GO
IF OBJECT_ID('dbo.Payment_Security_Holds', 'U') IS NOT NULL AND COL_LENGTH('dbo.Payment_Security_Holds', 'Refund_Amount') IS NULL
    ALTER TABLE dbo.Payment_Security_Holds ADD Refund_Amount DECIMAL(18,2) NULL;
GO
IF OBJECT_ID('dbo.Payment_Security_Holds', 'U') IS NOT NULL AND COL_LENGTH('dbo.Payment_Security_Holds', 'Refund_Ref') IS NULL
    ALTER TABLE dbo.Payment_Security_Holds ADD Refund_Ref NVARCHAR(100) NULL;
GO
IF OBJECT_ID('dbo.Payment_Security_Holds', 'U') IS NOT NULL AND COL_LENGTH('dbo.Payment_Security_Holds', 'Refund_Note') IS NULL
    ALTER TABLE dbo.Payment_Security_Holds ADD Refund_Note NVARCHAR(400) NULL;
GO

-- ── 3) ค่าตั้งใน Payment_Gateway_Config ────────────────────────────────────
IF OBJECT_ID('dbo.Payment_Gateway_Config', 'U') IS NULL
BEGIN
    PRINT N'⚠ ยังไม่มีตาราง Payment_Gateway_Config — รัน PHASE19_Migration_05 ก่อน แล้วรันไฟล์นี้ซ้ำ';
    RETURN;
END

DECLARE @cfg TABLE (K NVARCHAR(80), V NVARCHAR(MAX), S BIT, Cat NVARCHAR(60),
                    DN NVARCHAR(200), D NVARCHAR(1000), IT NVARCHAR(20), Opt NVARCHAR(500), Ord INT);

INSERT INTO @cfg (K, V, S, Cat, DN, D, IT, Opt, Ord) VALUES
('Payso_Enabled_Channels', 'CARD,QR', 0, N'Payso — การเชื่อมต่อ', N'ช่องทางที่ PaySo เปิดให้',
 N'คั่นด้วยจุลภาค: CARD (บัตรเครดิต/เดบิต), QR (พร้อมเพย์), INSTALLMENT (ผ่อน) — ใส่เฉพาะช่องทางที่ PaySo อนุมัติให้ร้านจริง. ช่องทาง PaySo ในหน้า "ช่องทางชำระเงิน" จะโผล่ให้ลูกค้าเห็นเฉพาะรหัสที่อยู่ในนี้ (และต้องเปิดใน "วิธีชำระที่ลูกค้าเห็น" ด้วย)',
 'text', NULL, 205),
('Security_Hold_Mode', 'TRANSFER', 0, N'วงเงินประกันความเสียหาย', N'วิธีรับเงินประกัน',
 N'TRANSFER (ค่าเริ่มต้น) = ลูกค้าโอนเข้าบัญชีโรงแรม บันทึกนอกเกตเวย์ เช็คเอาท์บันทึกโอนคืน/หักค่าเสียหาย · CARD_HOLD = กันวงเงินบนบัตร (Omise เท่านั้น — PaySo ทำไม่ได้) · CASH = รับเงินสดเป็นหลัก',
 'select', 'TRANSFER,CARD_HOLD,CASH', 705),
('Security_Hold_Transfer_Channel', '', 0, N'วงเงินประกันความเสียหาย', N'บัญชีรับโอนเงินประกัน (รหัสช่องทาง)',
 N'รหัสช่องทางโอนจากหน้า "ช่องทางชำระเงิน" เช่น TRANSFER_3 — ว่าง = ใช้ช่องทางโอนแรกที่ลูกค้าเห็นได้',
 'text', NULL, 706),
('Security_Hold_Overdue_Days', '3', 0, N'วงเงินประกันความเสียหาย', N'เตือนเงินประกัน (โอน/เงินสด) ที่ยังไม่คืน หลังเช็คเอาท์ (วัน)',
 N'เช็คเอาท์ไปแล้วเกินจำนวนวันนี้แต่ยังไม่บันทึกคืน/หัก → แจ้งเตือนครั้งเดียว + ลง log', 'number', NULL, 725),
('Payment_Cancellation_Policy', '', 0, N'ช่องทางชำระเงินและนโยบายยกเลิก', N'นโยบายยกเลิกหลัก (ครอบทุกช่องทาง)',
 N'ข้อความนโยบายการยกเลิก/คืนเงินที่แสดงคู่กับทุกช่องทางชำระในหน้าจอง — เงื่อนไขเฉพาะช่องทาง (เวลาคืนเงินเข้าบัตร ฯลฯ) ใส่ที่หน้า "ช่องทางชำระเงิน"',
 'textarea', NULL, 900);

INSERT INTO dbo.Payment_Gateway_Config
    (Config_Key, Config_Value, Is_Secret, Category, Display_Name, [Description], Input_Type, Options, Display_Order)
SELECT c.K, c.V, c.S, c.Cat, c.DN, c.D, c.IT, c.Opt, c.Ord
FROM @cfg c
WHERE NOT EXISTS (SELECT 1 FROM dbo.Payment_Gateway_Config p WHERE p.Config_Key = c.K);
PRINT N'เพิ่มค่าตั้งใหม่: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' คีย์';

-- อัปเดตคำอธิบาย (ไม่แตะค่าที่ตั้งไว้)
UPDATE p
   SET p.Category = c.Cat, p.Display_Name = c.DN, p.[Description] = c.D,
       p.Input_Type = c.IT, p.Options = c.Opt, p.Display_Order = c.Ord, p.Is_Secret = c.S
FROM dbo.Payment_Gateway_Config p
JOIN @cfg c ON c.K = p.Config_Key;

-- ── PaySo เป็นเกตเวย์หลัก ──
UPDATE dbo.Payment_Gateway_Config
   SET Options = 'PAYSO,OMISE',
       [Description] = N'PAYSO = เกตเวย์หลัก (แนะนำ) · OMISE = สำรอง (เจ้าเดียวที่กันวงเงินบัตรได้). ปล่อยว่าง = PAYSO. PaySo ยังไม่อนุมัติ/ยังปิด = ลูกค้าไม่เห็นตัวเลือกเกตเวย์ใด ๆ. รายการเก่ายังตรวจสอบกับเจ้าเดิมได้'
 WHERE Config_Key = 'Payment_Provider';

-- สลับเป็น PAYSO เฉพาะเมื่อ Omise ไม่ได้ใช้งานอยู่จริง (ไม่ได้เปิด หรือยังไม่มี Secret Key)
IF EXISTS (SELECT 1 FROM dbo.Payment_Gateway_Config
            WHERE Config_Key = 'Payment_Provider' AND ISNULL(Config_Value, '') IN ('', 'OMISE'))
BEGIN
    DECLARE @omiseOn BIT = 0;
    IF EXISTS (SELECT 1 FROM dbo.Payment_Gateway_Config WHERE Config_Key = 'Omise_Enabled' AND Config_Value IN ('1','true','True'))
       AND EXISTS (SELECT 1 FROM dbo.Payment_Gateway_Config WHERE Config_Key = 'Omise_SecretKey' AND ISNULL(Config_Value,'') <> '')
        SET @omiseOn = 1;

    IF @omiseOn = 0
    BEGIN
        UPDATE dbo.Payment_Gateway_Config SET Config_Value = 'PAYSO', Modified_Date = GETDATE()
         WHERE Config_Key = 'Payment_Provider';
        PRINT N'Payment_Provider → PAYSO (เกตเวย์หลัก) — Omise ไม่ได้ใช้งานอยู่';
    END
    ELSE
        PRINT N'⚠ Omise เปิดใช้งานอยู่ (มี Secret Key) — ไม่สลับอัตโนมัติ เปลี่ยนเป็น PAYSO เองที่หน้า รับชำระเงินออนไลน์ เมื่อ PaySo พร้อม';
END

UPDATE dbo.Payment_Gateway_Config
   SET Display_Name = N'เปิดใช้เงินประกันความเสียหาย',
       [Description] = N'รับเงินประกันตามโหมดด้านล่าง (ค่าเริ่มต้น: โอนเข้าบัญชีโรงแรม จัดการนอกเกตเวย์) — เงินประกันไม่ใช่รายได้ ไม่ลงใบเสร็จ/ไม่ส่ง NextAcc'
 WHERE Config_Key = 'Payment_SecurityHold_Enabled';
GO

-- ── ตรวจผล ─────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Channel_Code') IS NOT NULL
    EXEC('SELECT ID, Paid_How, Status, Channel_Code, Channel_Type, Gateway_Provider, Gateway_Channel_Code,
                 Customer_Visible, Staff_Visible, Requires_Slip, Sort_Order
            FROM dbo.Account_Paid_How ORDER BY Sort_Order, ID');

SELECT Config_Key, LEFT(ISNULL(Config_Value,''), 40) AS Value
  FROM dbo.Payment_Gateway_Config
 WHERE Config_Key IN ('Payment_Provider','Payso_Enabled','Payso_Enabled_Channels','Security_Hold_Mode',
                      'Security_Hold_Transfer_Channel','Payment_SecurityHold_Enabled')
 ORDER BY Config_Key;

PRINT '';
PRINT N'ถัดไป: ศูนย์ตั้งค่า → ช่องทางชำระเงิน — ตรวจว่าลูกค้าเห็นเฉพาะช่องทางโอนที่ถูกต้อง + ใส่ QR/ข้อความแนะนำ';
PRINT N'PaySo อนุมัติแล้ว: หน้า รับชำระเงินออนไลน์ → เปิด Payso + ใส่กุญแจ + Payso_Enabled_Channels → ช่องทาง PaySo โผล่เอง';
PRINT N'ผูกบัญชีพักเงิน NextAcc ของแถว PaySo ที่หน้า Accounting Integration → แหล่งเงิน';
