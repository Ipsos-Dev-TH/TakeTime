-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 18: ศูนย์รวมการตั้งค่าระบบ (System Settings Center)
-- ════════════════════════════════════════════════════════════════════════════
-- ย้ายค่าที่เดิมต้องแก้ใน Web.config (และบางค่า hardcode ในโค้ด) มาเก็บใน DB
-- แก้ได้จากหน้าเว็บ มีผลทันที ไม่ต้องรีสตาร์ท App Pool (แก้ Web.config = ผู้ใช้หลุด session)
--
-- หลักการอ่านค่า (AppCfg.Get): DB มีค่า → ใช้ค่านั้น / ไม่มี → ใช้ Web.config เดิม
-- ⟹ ระบบที่ยังไม่ตั้งค่าใน DB ทำงานเหมือนเดิมทุกประการ (ไม่พังของเก่า)
-- ค่าลับ (IsSecret=1) เก็บเข้ารหัสด้วย code.Crypt
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

IF OBJECT_ID('dbo.System_Config', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[System_Config] (
        [ConfigKey] NVARCHAR(100) NOT NULL PRIMARY KEY,
        [ConfigValue] NVARCHAR(MAX) NULL,
        [Category] NVARCHAR(50) NOT NULL DEFAULT 'GENERAL',
        [DisplayName] NVARCHAR(200) NULL,
        [Description] NVARCHAR(500) NULL,
        [IsSecret] BIT NOT NULL DEFAULT 0,        -- 1 = เก็บเข้ารหัส + ปิดบังในหน้าจอ
        [InputType] VARCHAR(20) NOT NULL DEFAULT 'text',   -- text / password / number / bool
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy_AdminID] SMALLINT NULL
    );
    PRINT 'Created System_Config';
END
GO

-- ค่าเริ่มต้น: ConfigValue = NULL หมายถึง "ยังไม่ตั้งใน DB → ใช้ค่าจาก Web.config"
MERGE [dbo].[System_Config] AS t
USING (VALUES
    -- ── LINE ──────────────────────────────────────────────────────────────
    ('linechannelaccesstokentaketime', 'LINE', N'LINE OA Channel Access Token',
     N'Token ของ LINE Official Account ที่ใช้ส่งข้อความทั้งระบบ (Messaging API)', 1, 'password', 10),
    ('LineOaChatId',                   'LINE', N'LINE Group/User ID ปลายทางหลัก',
     N'ปลายทางเริ่มต้นเวลาส่งแจ้งเตือนเข้า LINE (เว้นว่างได้ถ้าส่งรายคนอยู่แล้ว)', 0, 'text', 20),

    -- ── Telegram ──────────────────────────────────────────────────────────
    ('TelegramTokenTakeTime', 'TELEGRAM', N'Telegram Bot Token',
     N'Token ของบอทที่ใช้แจ้งเตือนภายใน', 1, 'password', 10),
    ('TelegramChatId',        'TELEGRAM', N'Telegram Chat ID',
     N'กลุ่ม/ผู้รับแจ้งเตือน (เดิม hardcode ในโค้ด — ตั้งที่นี่ได้แล้ว)', 0, 'text', 20),

    -- ── Email / SMTP ──────────────────────────────────────────────────────
    ('SMTP',                        'EMAIL', N'SMTP Server', N'เช่น smtp.gmail.com', 0, 'text', 10),
    ('SMTP_Port',                   'EMAIL', N'SMTP Port', N'เช่น 587 (TLS) หรือ 465 (SSL)', 0, 'number', 20),
    ('SMTP_EnableSsl',              'EMAIL', N'ใช้ SSL/TLS', N'true / false', 0, 'bool', 30),
    ('SMTP_UseDefaultCredentials',  'EMAIL', N'ใช้ credential เริ่มต้นของเครื่อง', N'ปกติ false', 0, 'bool', 40),
    ('Email_From',                  'EMAIL', N'อีเมลผู้ส่ง', N'อีเมลที่ใช้ส่งใบเสร็จ/e-Tax', 0, 'text', 50),
    ('Email_Password_From',         'EMAIL', N'รหัสผ่านอีเมลผู้ส่ง', N'Gmail ให้ใช้ App Password', 1, 'password', 60),
    ('Email_CC',                    'EMAIL', N'สำเนาถึง (CC)', N'คั่นด้วย comma', 0, 'text', 70),

    -- ── API ภายนอก ────────────────────────────────────────────────────────
    ('GooglePlacesApiKey', 'API', N'Google Places API Key', N'ใช้ดึงรีวิว Google หน้าแรก', 1, 'password', 10),
    ('TaxInvoiceApiKey',   'API', N'Tax Invoice API Key',   N'บริการใบกำกับภาษีอิเล็กทรอนิกส์', 1, 'password', 20),

    -- ── ที่เก็บไฟล์ ────────────────────────────────────────────────────────
    ('ReceiptFolderPath',        'PATH', N'โฟลเดอร์ใบเสร็จ',        N'physical path บนเซิร์ฟเวอร์', 0, 'text', 10),
    ('PaymentFolderPath',        'PATH', N'โฟลเดอร์ใบสำคัญจ่าย',    N'physical path บนเซิร์ฟเวอร์', 0, 'text', 20),
    ('ImagesFolderPath',         'PATH', N'โฟลเดอร์รูปภาพ',          N'physical path บนเซิร์ฟเวอร์', 0, 'text', 30),
    ('BaseFolderPath',           'PATH', N'โฟลเดอร์หลัก',            N'physical path บนเซิร์ฟเวอร์', 0, 'text', 40),
    ('StaffSignatureFolderPath', 'PATH', N'โฟลเดอร์ลายเซ็นพนักงาน',  N'physical path บนเซิร์ฟเวอร์', 0, 'text', 50)
) AS s (ConfigKey, Category, DisplayName, Description, IsSecret, InputType, DisplayOrder)
ON t.ConfigKey = s.ConfigKey
WHEN NOT MATCHED THEN
    INSERT (ConfigKey, ConfigValue, Category, DisplayName, Description, IsSecret, InputType, DisplayOrder)
    VALUES (s.ConfigKey, NULL, s.Category, s.DisplayName, s.Description, s.IsSecret, s.InputType, s.DisplayOrder)
WHEN MATCHED THEN
    UPDATE SET Category = s.Category, DisplayName = s.DisplayName, Description = s.Description,
               IsSecret = s.IsSecret, InputType = s.InputType, DisplayOrder = s.DisplayOrder;
GO

SELECT Category, ConfigKey, DisplayName, IsSecret,
       CASE WHEN ConfigValue IS NULL THEN N'(ใช้ค่าจาก Web.config)' ELSE N'ตั้งใน DB แล้ว' END AS Status
  FROM [dbo].[System_Config]
 ORDER BY Category, DisplayOrder;
GO
