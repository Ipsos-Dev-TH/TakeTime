-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 25: Member Portal — บัตรสมาชิก + ส่วนลดตามวันเข้าพัก + Voucher
-- ════════════════════════════════════════════════════════════════════════════
-- ต่อยอดระบบ Loyalty เดิม (Loyalty_Tiers / Customer_Loyalty — PHASE 07/13) ไม่แตะของเก่า:
--   1) สมาชิกล็อกอินเองได้ (เบอร์โทร + PIN) → เห็นบัตร เลเวล วันหมดอายุ สิทธิ์ และ voucher
--   2) ส่วนลดค่าห้องแยกตามวันเข้าพัก ต่อ tier (วันธรรมดา X% / วันหยุด Y%)
--   3) Voucher: ตั้ง template ได้ (ชื่อ เงื่อนไข เช่น "เครื่องดื่มในคาเฟ่เท่านั้น") → แจกให้สมาชิก
--      → สมาชิกกดใช้ → ได้โค้ดโชว์ให้พนักงาน → พนักงานกดแลกด้วยโค้ด → tracking ครบ
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- ── 1) ล็อกอินสมาชิก + วันหมดอายุสมาชิก ────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Customer_Loyalty') AND name = 'Member_PIN_Hash')
BEGIN
    ALTER TABLE dbo.Customer_Loyalty ADD Member_PIN_Hash NVARCHAR(200) NULL;
    PRINT 'Added Customer_Loyalty.Member_PIN_Hash';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Customer_Loyalty') AND name = 'Membership_Expiry')
BEGIN
    -- วันหมดอายุ "สถานะสมาชิก" (คนละอย่างกับ ExpiryDate เดิมซึ่งเป็นวันหมดอายุแต้ม)
    ALTER TABLE dbo.Customer_Loyalty ADD Membership_Expiry DATE NULL;   -- NULL = ไม่มีหมดอายุ
    PRINT 'Added Customer_Loyalty.Membership_Expiry';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Customer_Loyalty') AND name = 'Pin_Fail_Count')
BEGIN
    ALTER TABLE dbo.Customer_Loyalty ADD Pin_Fail_Count INT NOT NULL DEFAULT 0,
                                          Pin_Locked_Until DATETIME NULL;
    PRINT 'Added PIN lockout columns';
END
GO

-- ── 2) รูปหน้าบัตรของแต่ละ tier ───────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Loyalty_Tiers') AND name = 'Card_Image_Path')
BEGIN
    ALTER TABLE dbo.Loyalty_Tiers ADD Card_Image_Path NVARCHAR(300) NULL;  -- NULL = ใช้บัตร CSS ตามสี tier
    PRINT 'Added Loyalty_Tiers.Card_Image_Path';
END
GO

-- ── 3) ส่วนลดค่าห้องตามวันเข้าพัก ต่อ tier ─────────────────────────────────────
IF OBJECT_ID('dbo.Loyalty_Tier_Room_Discounts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Loyalty_Tier_Room_Discounts (
        ID           INT IDENTITY(1,1) PRIMARY KEY,
        Tier_ID      TINYINT NOT NULL,
        Day_Type     VARCHAR(10) NOT NULL,          -- WEEKDAY | WEEKEND (ส-อา + วันในตาราง Accommodation_HolidayPrice)
        Discount_Pct DECIMAL(5,2) NOT NULL DEFAULT 0,
        Is_Active    BIT NOT NULL DEFAULT 1,
        Updated_Date DATETIME NULL,
        CONSTRAINT UQ_TierRoomDisc UNIQUE (Tier_ID, Day_Type),
        CONSTRAINT CK_TierRoomDisc_Day CHECK (Day_Type IN ('WEEKDAY', 'WEEKEND'))
    );
    PRINT 'Created Loyalty_Tier_Room_Discounts';
END
GO

-- ── 4) Voucher templates ──────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Member_Voucher_Templates', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Member_Voucher_Templates (
        ID               INT IDENTITY(1,1) PRIMARY KEY,
        Name             NVARCHAR(150) NOT NULL,             -- เช่น "เครื่องดื่มฟรี 1 แก้ว"
        Description      NVARCHAR(600) NULL,                 -- เงื่อนไข เช่น "เฉพาะเมนูในคาเฟ่ ไม่รวมของขาย"
        Code_Prefix      VARCHAR(8) NOT NULL DEFAULT 'VC',   -- โค้ดขึ้นต้น เช่น DRK → DRK-7K4MX
        Tier_ID          TINYINT NULL,                       -- NULL = ทุก tier (ใช้ตอนแจกแบบทั้ง tier)
        Valid_Days       INT NOT NULL DEFAULT 90,            -- อายุ voucher นับจากวันแจก
        Redeem_Window_Min INT NOT NULL DEFAULT 60,           -- กด "ใช้" แล้วโค้ดมีผลกี่นาที
        Is_Active        BIT NOT NULL DEFAULT 1,
        Created_Date     DATETIME NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Created Member_Voucher_Templates';
END
GO

-- ── 5) Voucher ที่แจกให้สมาชิกรายคน ────────────────────────────────────────────
IF OBJECT_ID('dbo.Member_Vouchers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Member_Vouchers (
        ID                BIGINT IDENTITY(1,1) PRIMARY KEY,
        Template_ID       INT NOT NULL REFERENCES dbo.Member_Voucher_Templates(ID),
        Customer_MobilePhone NVARCHAR(30) NOT NULL,
        Code              VARCHAR(20) NOT NULL,              -- เช่น DRK-7K4MX (unique)
        -- ISSUED = แจกแล้วยังไม่กดใช้ / ACTIVATED = สมาชิกกดใช้ โค้ดกำลังรอพนักงาน /
        -- REDEEMED = พนักงานแลกแล้ว / EXPIRED / CANCELLED
        Status            VARCHAR(12) NOT NULL DEFAULT 'ISSUED',
        Issued_Date       DATETIME NOT NULL DEFAULT GETDATE(),
        Issued_By         NVARCHAR(100) NULL,
        Expiry_Date       DATE NOT NULL,
        Activated_Date    DATETIME NULL,
        Activation_Expiry DATETIME NULL,                     -- Activated + Redeem_Window_Min
        Redeemed_Date     DATETIME NULL,
        Redeemed_By_AdminID SMALLINT NULL,
        Redeem_Note       NVARCHAR(300) NULL,                -- พนักงานจดได้ เช่น "ลาเต้เย็น"
        CONSTRAINT UQ_MemberVoucher_Code UNIQUE (Code),
        CONSTRAINT CK_MemberVoucher_Status CHECK (Status IN ('ISSUED','ACTIVATED','REDEEMED','EXPIRED','CANCELLED'))
    );
    CREATE INDEX IX_MemberVouchers_Phone ON dbo.Member_Vouchers (Customer_MobilePhone, Status);
    PRINT 'Created Member_Vouchers';
END
GO

-- ── 6) seed ตัวอย่าง template (แก้/ปิดได้ในหน้าจัดการ) ─────────────────────────
IF NOT EXISTS (SELECT 1 FROM Member_Voucher_Templates)
BEGIN
    INSERT INTO Member_Voucher_Templates (Name, Description, Code_Prefix, Valid_Days, Redeem_Window_Min)
    VALUES (N'เครื่องดื่มฟรี 1 แก้ว',
            N'ใช้ได้กับเมนูเครื่องดื่มในคาเฟ่เท่านั้น (ไม่รวมสินค้าขายหน้าร้าน) — 1 สิทธิ์/ใบ',
            'DRK', 90, 60);
    PRINT 'Seeded example voucher template';
END
GO

PRINT 'PHASE18_25 completed';
GO
