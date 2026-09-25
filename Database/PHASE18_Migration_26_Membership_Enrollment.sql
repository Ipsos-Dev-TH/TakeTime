-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 26: สมัครสมาชิกแบบเก็บค่าสมัคร + ตัดสิทธิ์รายคน + ส่วนลดอัตโนมัติ
-- ════════════════════════════════════════════════════════════════════════════
-- ต่อยอด PHASE18_25 (Member Portal):
--   1) แต่ละ tier ตั้ง "ค่าสมัคร" และ "อายุสมาชิก (เดือน)" ได้ → สมัคร/ต่ออายุที่เคาน์เตอร์
--      เก็บเงิน + ลงรายได้อัตโนมัติ (ใบรับเงิน MBR-xxx + ส่งเข้า NextAcc เป็นรายได้ค่าสมาชิก)
--   2) ตัดสิทธิ์ voucher รายคน: สมาชิกได้ tier นี้ แต่ไม่ให้ template บางตัว (เช่น "พักฟรี")
--   3) สวิตช์ส่วนลดสมาชิกอัตโนมัติตอนจอง (Member_AutoDiscount — default ปิด)
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- ── 1) ค่าสมัคร + อายุสมาชิกต่อ tier ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Loyalty_Tiers') AND name = 'Signup_Fee')
BEGIN
    ALTER TABLE dbo.Loyalty_Tiers ADD
        Signup_Fee      DECIMAL(10,2) NOT NULL CONSTRAINT DF_Tier_Fee DEFAULT (0),   -- 0 = สมัครฟรี
        Duration_Months INT NOT NULL CONSTRAINT DF_Tier_Months DEFAULT (12);          -- 0 = ตลอดชีพ
    PRINT 'Added Loyalty_Tiers.Signup_Fee / Duration_Months';
END
GO

-- ── 2) ประวัติการชำระค่าสมาชิก (สมัคร/ต่ออายุ/อัปเกรด) ────────────────────────
IF OBJECT_ID('dbo.Membership_Payments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Membership_Payments (
        ID          BIGINT IDENTITY(1,1) PRIMARY KEY,
        Customer_MobilePhone NVARCHAR(30) NOT NULL,
        Tier_ID     TINYINT NOT NULL,
        Action_Type VARCHAR(10) NOT NULL,           -- NEW | RENEW | UPGRADE
        Amount      DECIMAL(10,2) NOT NULL,
        Paid_How    NVARCHAR(100) NULL,             -- แหล่งรับเงิน (เงินสด/โอน — ชื่อจาก Account_Paid_How)
        Receipt_Ref NVARCHAR(40) NULL,              -- ใบรับเงิน MBR-{id} ที่ลงบัญชี
        Old_Expiry  DATE NULL,
        New_Expiry  DATE NULL,                      -- NULL = ตลอดชีพ
        Created_Date DATETIME NOT NULL DEFAULT GETDATE(),
        Created_By  NVARCHAR(100) NULL,
        CONSTRAINT CK_MbrPay_Action CHECK (Action_Type IN ('NEW','RENEW','UPGRADE'))
    );
    CREATE INDEX IX_MbrPay_Phone ON dbo.Membership_Payments (Customer_MobilePhone);
    PRINT 'Created Membership_Payments';
END
GO

-- ── 3) ตัดสิทธิ์ voucher รายคน ─────────────────────────────────────────────────
IF OBJECT_ID('dbo.Member_Voucher_Exclusions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Member_Voucher_Exclusions (
        ID          BIGINT IDENTITY(1,1) PRIMARY KEY,
        Customer_MobilePhone NVARCHAR(30) NOT NULL,
        Template_ID INT NOT NULL REFERENCES dbo.Member_Voucher_Templates(ID) ON DELETE CASCADE,
        Created_Date DATETIME NOT NULL DEFAULT GETDATE(),
        Created_By  NVARCHAR(100) NULL,
        CONSTRAINT UQ_MbrExcl UNIQUE (Customer_MobilePhone, Template_ID)
    );
    PRINT 'Created Member_Voucher_Exclusions';
END
GO

-- ── 4) mapping บัญชีรายได้ค่าสมาชิก (กรอกรหัสจริงในหน้าผังบัญชี) ───────────────
IF OBJECT_ID('dbo.Accounting_Account_Mapping', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'MEMBER_FEE_REVENUE')
BEGIN
    INSERT INTO Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES ('MEMBER_FEE_REVENUE', N'รายได้ค่าสมัครสมาชิก', '', 'REVENUE', 1);
    PRINT 'Seeded MEMBER_FEE_REVENUE mapping';
END
GO

-- ── 5) สวิตช์ส่วนลดสมาชิกอัตโนมัติตอนจอง (ศูนย์ตั้งค่าระบบ → ทั่วไป) ────────────
IF OBJECT_ID('dbo.System_Config', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM System_Config WHERE ConfigKey = 'Member_AutoDiscount')
BEGIN
    INSERT INTO System_Config (ConfigKey, ConfigValue, Category, DisplayName, Description, IsSecret, InputType, DisplayOrder)
    VALUES ('Member_AutoDiscount', NULL, 'GENERAL',
            N'ส่วนลดสมาชิกอัตโนมัติตอนจอง',
            N'เปิด (true) = การจองผ่านหน้าเว็บของเบอร์ที่เป็นสมาชิก (บัตรไม่หมดอายุ) จะถูกหักส่วนลดค่าห้องตาม tier×วันเข้าพัก อัตโนมัติ พร้อมบันทึกในหมายเหตุการจอง — ค่าเริ่มต้น: ปิด (พนักงานให้ส่วนลดเอง)',
            0, 'bool', 5);
    PRINT 'Seeded Member_AutoDiscount setting';
END
GO

PRINT 'PHASE18_26 completed';
GO
