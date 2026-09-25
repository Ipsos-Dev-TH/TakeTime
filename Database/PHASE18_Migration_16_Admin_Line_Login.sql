-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 16: LINE Login สำหรับ Admin (เก็บ userId เพื่อส่งไลน์ส่วนตัว)
-- ════════════════════════════════════════════════════════════════════════════
-- พนักงาน/ผู้ดูแลกดผูกบัญชี LINE ของตัวเองผ่าน LINE Login (OAuth 2.0) ครั้งเดียว
-- ระบบเก็บ userId ไว้ → ส่งข้อความแจ้งเตือนเข้าไลน์ส่วนตัวรายคนได้ (ไม่ต้องยิงเข้ากลุ่ม)
--
-- ⚠️ สำคัญ: LINE Login channel ต้องอยู่ "provider เดียวกัน" กับ Messaging API channel
--    ที่ใช้ส่งข้อความ ไม่งั้น userId ที่ได้จะคนละตัวและ push ไม่ถึง
--    (LINE ออก userId ต่างกันต่อ provider) — ตั้งค่าใน LINE Developers Console
--    และผู้ใช้ต้องเป็นเพื่อนกับ LINE OA นั้นด้วย ถึงจะ push หาได้
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- 1) คอลัมน์เก็บข้อมูล LINE ของแต่ละ Admin
IF COL_LENGTH('Admin', 'Line_UserId') IS NULL
    ALTER TABLE [dbo].[Admin] ADD Line_UserId NVARCHAR(64) NULL;
IF COL_LENGTH('Admin', 'Line_DisplayName') IS NULL
    ALTER TABLE [dbo].[Admin] ADD Line_DisplayName NVARCHAR(200) NULL;
IF COL_LENGTH('Admin', 'Line_PictureUrl') IS NULL
    ALTER TABLE [dbo].[Admin] ADD Line_PictureUrl NVARCHAR(500) NULL;
IF COL_LENGTH('Admin', 'Line_LinkedDate') IS NULL
    ALTER TABLE [dbo].[Admin] ADD Line_LinkedDate DATETIME NULL;
IF COL_LENGTH('Admin', 'Line_NotifyEnabled') IS NULL
    ALTER TABLE [dbo].[Admin] ADD Line_NotifyEnabled BIT NOT NULL DEFAULT 1;
GO

-- ค้นหาด้วย userId ตอนรับ webhook / ส่งข้อความ (userId ต้องไม่ซ้ำคน)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Admin_LineUserId' AND object_id = OBJECT_ID('dbo.Admin'))
    CREATE INDEX IX_Admin_LineUserId ON [dbo].[Admin](Line_UserId) WHERE Line_UserId IS NOT NULL;
GO

-- 2) ค่าตั้งค่า LINE Login channel (คนละ channel กับ Messaging API)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'LineLogin_Enabled')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('LineLogin_Enabled', '0', N'เปิดให้ Admin ผูกบัญชี LINE ด้วย LINE Login');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'LineLogin_ChannelId')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('LineLogin_ChannelId', '', N'LINE Login Channel ID (จาก LINE Developers Console) — ต้องอยู่ provider เดียวกับ Messaging API');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'LineLogin_ChannelSecret_Encrypted')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('LineLogin_ChannelSecret_Encrypted', '', N'LINE Login Channel Secret (เข้ารหัส — ตั้งผ่านหน้า Admin เท่านั้น)');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'LineLogin_CallbackUrl')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('LineLogin_CallbackUrl', 'https://taketimebangphra.com/Admin/LineLinkCallback',
            N'Callback URL — ต้องใส่ค่าเดียวกันนี้ใน LINE Developers Console > LINE Login > Callback URL');
GO

SELECT ConfigKey, ConfigValue FROM Accounting_Integration_Config
 WHERE ConfigKey LIKE 'LineLogin_%' ORDER BY ConfigKey;

SELECT ID, Username, Role, Line_UserId, Line_DisplayName, Line_LinkedDate, Line_NotifyEnabled
  FROM [dbo].[Admin] WHERE Status = 1 ORDER BY Username;
GO

-- ════════════════════════════════════════════════════════════════════════════
-- เพิ่มเติม: บังคับเพิ่มเพื่อน LINE OA ก่อนใช้งาน
-- LINE ไม่มี API ให้ "บังคับ" ตอนล็อกอิน (ผู้ใช้กดข้ามได้) — ระบบจึงตรวจด้วย
-- friendship API แล้วไม่ปล่อยผ่านจนกว่าจะเพิ่มจริง
-- ════════════════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'LineLogin_RequireFriend')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('LineLogin_RequireFriend', '1',
            N'บังคับเพิ่ม LINE OA เป็นเพื่อนก่อนใช้งาน (1=บังคับ) — ไม่เป็นเพื่อน ระบบส่งข้อความหาไม่ได้');
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'LineLogin_BotBasicId')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('LineLogin_BotBasicId', '',
            N'Basic ID ของ LINE OA เช่น @taketime (ดูที่ LINE Official Account Manager) — ใช้ทำลิงก์/QR เพิ่มเพื่อน');
GO
