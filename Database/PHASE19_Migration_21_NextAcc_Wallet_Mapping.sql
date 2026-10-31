-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 21: NextAcc "กระเป๋าเงิน" (บัญชีธนาคาร / e-Wallet / พักเงินเกตเวย์ / ลูกหนี้ OTA)
--                        → แหล่งเงิน TakeTime (Account_Paid_How) + เอกสารรับเงิน OTA รายช่องทาง
-- ════════════════════════════════════════════════════════════════════════════
-- บริบท:
--   • NextAcc มี entity BankAccount (บัญชีธนาคาร/กระเป๋าเงิน) ที่ผูกผังบัญชีผ่าน LinkedAccountId —
--     ดึงได้ที่ GET /api/companies/{cid}/bank/accounts (Wachira-d/Accounting Controllers/BankController.cs:16,31)
--     และผังมาตรฐานมี 11113 กระเป๋าเงิน Digital + 11340 ลูกหนี้ผู้ให้บริการรับชำระเงิน
--     (Services/ChartOfAccountTemplates.cs:26,41). JE ของใบเสร็จลงบัญชีเงินตาม PaymentAccountId
--     > BankAccount.LinkedAccount > 111 (Services/Implementations/DocumentService.cs:14458-14473)
--   • หน้า Admin → Accounting Integration ปุ่ม "ดึงรายการจาก NextAcc" เก็บรายการไว้ในตาราง cache
--     Accounting_Nexaacc_BankAccounts แล้วให้เลือกต่อแถว Account_Paid_How
--   • OTA Channel Collect: Nexaacc_OtaDocument_Mode = RECEIPT_DOC → สร้างใบเสร็จรับเงินใน NextAcc
--     ต่อการจอง (ผู้ซื้อ = OTA, Dr บัญชีพักเงิน/ลูกหนี้ของ OTA รายช่องทาง) — ตั้งรายช่องทางใน
--     Accounting_Ota_Channel_Map. ค่าเริ่มต้น OFF (ไม่เปลี่ยนพฤติกรรมเดิม)
--
-- ⚠ ไม่แตะคอลัมน์ catalog ของช่องทาง (Channel_Code/Channel_Type/...) — เป็นของ PHASE19_20
-- idempotent — รันซ้ำได้, ไม่ลบข้อมูลเดิม
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

-- ── 1) แถวแหล่งเงินจำว่าผูกกับ "บัญชีธนาคาร/กระเป๋าเงิน" ตัวไหนของ NextAcc ─────────────
--      (Nexaacc_AccountId เดิม = ผังบัญชีที่ลง JE จริง; คอลัมน์ใหม่ = BankAccount id สำหรับแสดงผล/ตรวจสุขภาพ
--       และซ่อม Nexaacc_AccountId อัตโนมัติเมื่อฝั่ง NextAcc เปลี่ยนผังที่ผูก)
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Account_Paid_How', 'Nexaacc_BankAccountId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account_Paid_How] ADD Nexaacc_BankAccountId UNIQUEIDENTIFIER NULL;
    PRINT 'Added Account_Paid_How.Nexaacc_BankAccountId';
END
ELSE
    PRINT 'Account_Paid_How.Nexaacc_BankAccountId already exists (or table missing)';
GO

-- ── 2) cache ผังบัญชี (หน้า admin สร้างเองตอน Sync บัญชี — สร้างไว้ก่อนเพื่อให้ join ได้เสมอ) ──
IF OBJECT_ID('dbo.Accounting_Nexaacc_Accounts', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Accounting_Nexaacc_Accounts] (
        Nexaacc_AccountId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Account_Code NVARCHAR(20) NOT NULL,
        Account_Name NVARCHAR(255),
        Account_Name_En NVARCHAR(255),
        Account_Type NVARCHAR(50),
        Account_Type_Value INT DEFAULT 0,
        Parent_Account_Id UNIQUEIDENTIFIER NULL,
        Account_Level INT DEFAULT 0,
        Is_Active BIT DEFAULT 1,
        Is_System_Account BIT DEFAULT 0,
        Description NVARCHAR(500),
        Last_Synced DATETIME DEFAULT GETDATE()
    );
    PRINT 'Created Accounting_Nexaacc_Accounts';
END
GO

-- ── 3) cache "กระเป๋าเงิน" (NextAcc BankAccount) ────────────────────────────────────
IF OBJECT_ID('dbo.Accounting_Nexaacc_BankAccounts', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Accounting_Nexaacc_BankAccounts] (
        Nexaacc_BankAccountId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Account_Name NVARCHAR(255) NULL,
        Bank_Name NVARCHAR(255) NULL,
        Account_Number NVARCHAR(100) NULL,
        Branch_Name NVARCHAR(255) NULL,
        Account_Type NVARCHAR(50) NULL,          -- Savings / Current / Fixed (ข้อความอิสระจาก NextAcc)
        Currency NVARCHAR(10) NULL,
        Linked_Account_Id UNIQUEIDENTIFIER NULL,  -- ผังบัญชีที่ JE ลงจริง
        Linked_Account_Code NVARCHAR(20) NULL,
        Linked_Account_Name NVARCHAR(255) NULL,
        Is_Active BIT NOT NULL CONSTRAINT DF_Acc_Nexaacc_Bank_IsActive DEFAULT (1),
        Is_Present BIT NOT NULL CONSTRAINT DF_Acc_Nexaacc_Bank_IsPresent DEFAULT (1), -- 0 = ไม่อยู่ในผลดึงล่าสุด (ถูกลบใน NextAcc)
        Last_Synced DATETIME NOT NULL CONSTRAINT DF_Acc_Nexaacc_Bank_LastSynced DEFAULT (GETDATE())
    );
    PRINT 'Created Accounting_Nexaacc_BankAccounts';
END
GO

-- ── 4) OTA รายช่องทาง → แหล่งเงิน (บัญชีพักเงิน/ลูกหนี้ OTA) + ผู้ซื้อบนเอกสาร ───────────────
IF OBJECT_ID('dbo.Accounting_Ota_Channel_Map', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Accounting_Ota_Channel_Map] (
        Channel_Key NVARCHAR(50) NOT NULL PRIMARY KEY,   -- ชื่อช่องทางตัวพิมพ์ใหญ่ A-Z0-9 (Booking.com → BOOKINGCOM)
        Display_Name NVARCHAR(100) NULL,
        Paid_How_ID INT NULL,                            -- Account_Paid_How.ID ที่ผูกบัญชี NextAcc ของ OTA นี้
        Buyer_Name NVARCHAR(255) NULL,                   -- ผู้ซื้อบนเอกสาร (ว่าง = Display_Name)
        Buyer_Tax_Id NVARCHAR(20) NULL,
        Buyer_Address NVARCHAR(500) NULL,
        Buyer_Branch NVARCHAR(10) NULL,
        Nexaacc_Contact_Id UNIQUEIDENTIFIER NULL,        -- cache ผู้ติดต่อ NextAcc (ล้างเมื่อแก้ข้อมูลผู้ซื้อ)
        Is_Active BIT NOT NULL CONSTRAINT DF_Acc_Ota_Channel_Map_IsActive DEFAULT (1), -- 0 = ไม่โพสต์ช่องทางนี้อัตโนมัติ
        Updated_Date DATETIME NOT NULL CONSTRAINT DF_Acc_Ota_Channel_Map_Updated DEFAULT (GETDATE()),
        Updated_By NVARCHAR(100) NULL
    );
    PRINT 'Created Accounting_Ota_Channel_Map';
END
GO

-- ช่องทางที่พบบ่อย (ยังไม่ผูกบัญชี — ผู้ดูแลเลือกเองในหน้า admin; ช่องทางอื่นที่เจอในการจองจะโผล่ในหน้าเองอัตโนมัติ)
IF OBJECT_ID('dbo.Accounting_Ota_Channel_Map', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Ota_Channel_Map WHERE Channel_Key = N'AGODA')
        INSERT INTO dbo.Accounting_Ota_Channel_Map (Channel_Key, Display_Name) VALUES (N'AGODA', N'Agoda');
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Ota_Channel_Map WHERE Channel_Key = N'BOOKINGCOM')
        INSERT INTO dbo.Accounting_Ota_Channel_Map (Channel_Key, Display_Name) VALUES (N'BOOKINGCOM', N'Booking.com');
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Ota_Channel_Map WHERE Channel_Key = N'EXPEDIA')
        INSERT INTO dbo.Accounting_Ota_Channel_Map (Channel_Key, Display_Name) VALUES (N'EXPEDIA', N'Expedia');
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Ota_Channel_Map WHERE Channel_Key = N'TRIPCOM')
        INSERT INTO dbo.Accounting_Ota_Channel_Map (Channel_Key, Display_Name) VALUES (N'TRIPCOM', N'Trip.com');
END
GO

-- ── 5) ค่าตั้ง ──────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_OtaDocument_Mode')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
        VALUES (N'Nexaacc_OtaDocument_Mode', N'OFF',
                N'OTA Channel Collect: OFF = เดิม (Nexaacc_OtaRoomRevenue ลงลูกหนี้ OTA กลาง) / RECEIPT_DOC = สร้างใบเสร็จรับเงินใน NextAcc ต่อการจอง ผู้ซื้อ=OTA, Dr บัญชีพักเงินรายช่องทาง (Accounting_Ota_Channel_Map)', GETDATE());

    -- แหล่งเงินรายเกตเวย์ (ชื่อแถว Account_Paid_How) — ว่าง = ใช้ Payment_PaidHow_Name เดิม
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Gateway_PaidHow_OMISE')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
        VALUES (N'Nexaacc_Gateway_PaidHow_OMISE', N'', N'ยอดรับผ่าน Omise ลงแหล่งเงินชื่อนี้ (Account_Paid_How.Paid_How) — ว่าง = Payment_PaidHow_Name', GETDATE());
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Gateway_PaidHow_PAYSO')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
        VALUES (N'Nexaacc_Gateway_PaidHow_PAYSO', N'', N'ยอดรับผ่าน Payso ลงแหล่งเงินชื่อนี้ (Account_Paid_How.Paid_How) — ว่าง = Payment_PaidHow_Name', GETDATE());
    IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Gateway_PaidHow_MANUAL_QR')
        INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
        VALUES (N'Nexaacc_Gateway_PaidHow_MANUAL_QR', N'', N'ยอดรับผ่าน QR ตรวจสลิป (เงินเข้าธนาคารตรง) ลงแหล่งเงินชื่อนี้ — ว่าง = Payment_PaidHow_Name', GETDATE());
END
GO

-- ── ตรวจสอบ ─────────────────────────────────────────────────────────────────
SELECT 'Account_Paid_How.Nexaacc_BankAccountId' AS Item,
       CASE WHEN COL_LENGTH('dbo.Account_Paid_How', 'Nexaacc_BankAccountId') IS NULL THEN 'MISSING' ELSE 'OK' END AS Status
UNION ALL SELECT 'Accounting_Nexaacc_BankAccounts',
       CASE WHEN OBJECT_ID('dbo.Accounting_Nexaacc_BankAccounts', 'U') IS NULL THEN 'MISSING' ELSE 'OK' END
UNION ALL SELECT 'Accounting_Ota_Channel_Map',
       CASE WHEN OBJECT_ID('dbo.Accounting_Ota_Channel_Map', 'U') IS NULL THEN 'MISSING' ELSE 'OK' END;
