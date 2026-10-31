-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 36: ยอดล็อกตามสลิป + ลูกค้าแนบสลิปตรวจไม่ผ่าน + ผู้รับแบบ QR ร้านค้า (Biller)
-- ════════════════════════════════════════════════════════════════════════════
-- ต่อจาก PHASE19_34 (ตรวจสลิปโอน) — CLAUDE.md ข้อ 18 "ยอดล็อกตามสลิป"
-- กฎเจ้าของ: อ่านยอดจากสลิปได้ → ใส่ยอดอัตโนมัติและห้ามแก้มือ · อ่านไม่ได้/ความมั่นใจต่ำ → แก้ยอดเองได้
--           ลูกค้าแนบสลิปเองแล้วตรวจไม่ผ่าน → ตั้งได้ว่า "แจ้งให้ติดต่อเจ้าหน้าที่ (ไม่ให้ผ่าน)" หรือ "ให้ลูกค้ากรอกยอดเอง (รอเจ้าหน้าที่ตรวจ)"
--
-- 1) Payment_Slips + Amount_Locked (BIT) / Lock_Confidence (INT) / Lock_Source (SLIPOK | LOCAL_OCR) — ยอดที่บันทึกมาจากสลิปแบบล็อก
--    + Customer_Manual_Amount (BIT) — ลูกค้ากรอกยอดเอง (โหมด ALLOW_MANUAL) ให้เจ้าหน้าที่ตรวจยอดกับบัญชีธนาคาร
-- 2) ค่าตั้ง (System_Config):
--    SlipVerify_Lock_Min_Confidence = 85   (ยอดที่อ่านจากรูปเอง ≥ ค่านี้ = ล็อก; SlipOK ธนาคารยืนยัน = ล็อกเสมอ)
--    SlipVerify_Customer_Fail_Mode  = CONTACT_ADMIN | ALLOW_MANUAL
--    SlipVerify_Customer_Contact    = ข้อความช่องทางติดต่อ (ว่าง = เบอร์จาก Business_Info + LINE OA)
--    SlipVerify_Receiver_Biller_Ids = 010753600031508  (QR ร้านค้า K PLUS SHOP "TAKE TIME / Take Time Nature Resort")
--    SlipVerify_Receiver_Merchant_Ids = KB000001813229 (รหัสร้านค้า — รหัสธุรกรรม KPS004KB000001813229)
--
-- ไม่แตะข้อมูลเดิม · idempotent รันซ้ำได้ · ยังไม่รัน = ไม่บันทึกคอลัมน์ล็อก (ฟีเจอร์ยังทำงานด้วยค่าเริ่มต้นในโค้ด)
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1) Payment_Slips
IF OBJECT_ID('dbo.Payment_Slips', 'U') IS NULL
    PRINT N'⚠ ไม่มีตาราง Payment_Slips — ข้ามส่วนที่ 1';
ELSE
BEGIN
    IF COL_LENGTH('dbo.Payment_Slips', 'Amount_Locked') IS NULL          ALTER TABLE dbo.Payment_Slips ADD Amount_Locked BIT NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Lock_Confidence') IS NULL        ALTER TABLE dbo.Payment_Slips ADD Lock_Confidence INT NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Lock_Source') IS NULL            ALTER TABLE dbo.Payment_Slips ADD Lock_Source NVARCHAR(20) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Customer_Manual_Amount') IS NULL ALTER TABLE dbo.Payment_Slips ADD Customer_Manual_Amount BIT NULL;
    PRINT N'Payment_Slips: คอลัมน์ยอดล็อกตามสลิปพร้อม';
END
GO

-- 2) ค่าตั้ง
IF OBJECT_ID('dbo.System_Config', 'U') IS NOT NULL
BEGIN
    DECLARE @cfg TABLE (K NVARCHAR(100), V NVARCHAR(400), N NVARCHAR(200), D NVARCHAR(500), S BIT, T NVARCHAR(20), O INT);
    INSERT INTO @cfg VALUES
      (N'SlipVerify_Lock_Min_Confidence',   N'85',              N'ความมั่นใจขั้นต่ำที่ล็อกยอด (%)', N'ยอดที่อ่านจากรูปเอง ≥ ค่านี้ = ใส่ยอดอัตโนมัติและแก้ไม่ได้ · ต่ำกว่า = กรอกยอดเอง (SlipOK ธนาคารยืนยัน = ล็อกเสมอ)', 0, N'number', 110),
      (N'SlipVerify_Customer_Fail_Mode',    N'CONTACT_ADMIN',   N'ลูกค้าแนบสลิปแล้วตรวจไม่ผ่าน', N'CONTACT_ADMIN = แจ้งให้ติดต่อเจ้าหน้าที่ (ไม่ให้ผ่าน) · ALLOW_MANUAL = ให้ลูกค้ากรอกยอดเอง (รอเจ้าหน้าที่ตรวจ) · สลิปซ้ำ = ไม่รับเสมอ', 0, N'text', 120),
      (N'SlipVerify_Customer_Contact',      NULL,               N'ช่องทางติดต่อที่แสดงลูกค้า', N'ว่าง = เบอร์โทรจาก Business_Info + LINE OA (LineLogin_BotBasicId)', 0, N'text', 130),
      (N'SlipVerify_Receiver_Biller_Ids',   N'010753600031508', N'Biller ID ของร้าน (QR ร้านค้า)', N'คั่นด้วยจุลภาค — เทียบเฉพาะตัวเลขกับ receiver.proxy (BILLERID) / ref1 ของสลิป', 0, N'text', 140),
      (N'SlipVerify_Receiver_Merchant_Ids', N'KB000001813229',  N'รหัสร้านค้า (Merchant ID)', N'คั่นด้วยจุลภาค — เทียบกับ toMerchantId / ref1-3 ของสลิป (ไม่สนตัวพิมพ์)', 0, N'text', 150);

    INSERT INTO dbo.System_Config (ConfigKey, ConfigValue, Category, DisplayName, [Description], IsSecret, InputType, DisplayOrder, ModifiedDate)
    SELECT c.K, c.V, N'ตรวจสลิป', c.N, c.D, c.S, c.T, 900 + c.O, GETDATE()
      FROM @cfg c
     WHERE NOT EXISTS (SELECT 1 FROM dbo.System_Config x WHERE x.ConfigKey = c.K);
    PRINT N'ค่าตั้งยอดล็อกตามสลิป: เพิ่ม ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' ค่า (ค่าเดิมไม่แตะ)';
END
ELSE PRINT N'⚠ ไม่มี System_Config (PHASE18_18) — ตั้งค่าผ่าน Web.config appSettings ได้';
GO

PRINT N'PHASE19_36 เสร็จ — ตั้งค่าที่ ศูนย์ตั้งค่า → การชำระเงิน → ตรวจสลิปโอน (ต้อง recycle app pool หรือรอ cache 5 นาที)';
GO
