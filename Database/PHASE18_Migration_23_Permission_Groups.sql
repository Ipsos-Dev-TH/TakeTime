-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 23: กลุ่มสิทธิ์ (Permission Groups)
-- ════════════════════════════════════════════════════════════════════════════
-- เดิมระบบมีสิทธิ์ตายตัว 3 แบบ (Owner / Admin / Staff) ฝังอยู่ในโค้ด 54 หน้า
-- → เพิ่ม/แก้กลุ่มเองไม่ได้ และเปิด-ปิดรายส่วนไม่ได้
--
-- ของใหม่: สร้าง "กลุ่มสิทธิ์" ได้เอง แล้วกำหนดต่อโมดูลว่า
--   • มองเห็น (Can_View)   → เมนูขึ้นหรือไม่
--   • เข้าใช้งาน (Can_Access) → เปิดหน้านั้นได้หรือไม่
--
-- เข้ากันได้กับของเดิม: พนักงานที่ยังไม่ถูกกำหนดกลุ่ม จะใช้สิทธิ์ตาม Role เดิมทุกประการ
-- (Owner/Admin/Staff) ⟹ รันสคริปต์นี้แล้วระบบยังทำงานเหมือนเดิม 100% จนกว่าจะเริ่มจัดกลุ่ม
--
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- ── 1) ตารางกลุ่มสิทธิ์ ────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Permission_Groups', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permission_Groups (
        ID            INT IDENTITY(1,1) PRIMARY KEY,
        Group_Name    NVARCHAR(100) NOT NULL,
        Description   NVARCHAR(300) NULL,
        Base_Role     NVARCHAR(20)  NOT NULL DEFAULT N'Staff',  -- Role ที่ใช้เป็นฐาน (กันโค้ดเดิมที่เช็ค Role)
        Is_System     BIT NOT NULL DEFAULT 0,   -- 1 = กลุ่มมาตรฐานของระบบ (ลบไม่ได้)
        Is_Active     BIT NOT NULL DEFAULT 1,
        Created_Date  DATETIME NOT NULL DEFAULT GETDATE(),
        Updated_Date  DATETIME NULL,
        CONSTRAINT UQ_Permission_Groups_Name UNIQUE (Group_Name)
    );
    PRINT 'Created Permission_Groups';
END
GO

-- ── 2) สิทธิ์รายโมดูลของแต่ละกลุ่ม ────────────────────────────────────────────
IF OBJECT_ID('dbo.Permission_Group_Modules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permission_Group_Modules (
        ID          INT IDENTITY(1,1) PRIMARY KEY,
        Group_ID    INT NOT NULL,
        Module_Code VARCHAR(40) NOT NULL,
        Can_View    BIT NOT NULL DEFAULT 0,   -- เห็นเมนู
        Can_Access  BIT NOT NULL DEFAULT 0,   -- เปิดหน้าได้
        CONSTRAINT FK_PGM_Group FOREIGN KEY (Group_ID) REFERENCES dbo.Permission_Groups(ID) ON DELETE CASCADE,
        CONSTRAINT UQ_PGM UNIQUE (Group_ID, Module_Code)
    );
    PRINT 'Created Permission_Group_Modules';
END
GO

-- ── 3) ผูกพนักงานกับกลุ่ม ──────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Admin') AND name = 'Permission_Group_ID')
BEGIN
    ALTER TABLE dbo.Admin ADD Permission_Group_ID INT NULL;
    PRINT 'Added Admin.Permission_Group_ID';
END
GO

-- ── 4) กลุ่มมาตรฐาน 3 กลุ่ม (ให้ผลเท่ากับ Role เดิม) ──────────────────────────
IF NOT EXISTS (SELECT 1 FROM Permission_Groups WHERE Group_Name = N'เจ้าของกิจการ')
    INSERT INTO Permission_Groups (Group_Name, Description, Base_Role, Is_System)
    VALUES (N'เจ้าของกิจการ', N'เห็นและใช้งานได้ทุกส่วน รวมการตั้งค่าระบบ', N'Owner', 1);

IF NOT EXISTS (SELECT 1 FROM Permission_Groups WHERE Group_Name = N'ผู้ดูแล / ผู้จัดการ')
    INSERT INTO Permission_Groups (Group_Name, Description, Base_Role, Is_System)
    VALUES (N'ผู้ดูแล / ผู้จัดการ', N'งานประจำวัน + การเงิน + ลูกค้า (ไม่รวมตั้งค่าระบบ/HR)', N'Admin', 1);

IF NOT EXISTS (SELECT 1 FROM Permission_Groups WHERE Group_Name = N'พนักงานหน้าร้าน')
    INSERT INTO Permission_Groups (Group_Name, Description, Base_Role, Is_System)
    VALUES (N'พนักงานหน้าร้าน', N'งานประจำวัน บริการลูกค้า และขายหน้าร้าน', N'Staff', 1);
GO

-- ── 5) เติมสิทธิ์เริ่มต้นให้กลุ่มมาตรฐาน (ตรงกับพฤติกรรมเดิมของแต่ละ Role) ─────
DECLARE @Modules TABLE (Code VARCHAR(40), OwnerOn BIT, AdminOn BIT, StaffOn BIT);
INSERT INTO @Modules (Code, OwnerOn, AdminOn, StaffOn) VALUES
    -- งานประจำวัน — ทุกคน
    ('OPS_BOOKING',      1,1,1),
    ('OPS_HOUSEKEEPING', 1,1,1),
    ('OPS_MAINTENANCE',  1,1,1),
    ('OPS_CHAT',         1,1,1),
    ('OPS_ROOMSERVICE',  1,1,1),
    ('OPS_ACTIVITY',     1,1,1),
    -- ขายหน้าร้าน — ทุกคน
    ('SALES_POS',        1,1,1),
    ('SALES_VOUCHER',    1,1,1),
    ('SALES_STOCK',      1,1,1),
    -- การเงิน — Admin ขึ้นไป (เดิม Staff เห็นด้วย แต่ตั้งใจปิดตามการจัดสิทธิ์ใหม่)
    ('FIN_RECEIPT',      1,1,0),
    ('FIN_VOUCHER',      1,1,0),
    ('FIN_REPORT',       1,1,0),
    -- ลูกค้า/การตลาด — Admin ขึ้นไป
    ('CRM_CUSTOMER',     1,1,0),
    ('CRM_LOYALTY',      1,1,0),
    ('CRM_REVIEW',       1,1,0),
    ('CRM_AFFILIATE',    1,1,0),
    -- ผู้บริหาร — Owner
    ('MGT_DASHBOARD',    1,0,0),
    ('MGT_REPORT',       1,0,0),
    ('MGT_CHANNEL',      1,0,0),
    -- บุคคล — Owner
    ('HR_EMPLOYEE',      1,0,0),
    ('HR_LEAVE',         1,0,0),
    ('HR_PAYROLL',       1,0,0),
    ('HR_ASSET',         1,0,0),
    -- ระบบ — Owner
    ('SYS_SETTINGS',     1,0,0),
    ('SYS_DATABASE',     1,0,0);

DECLARE @OwnerId INT = (SELECT ID FROM Permission_Groups WHERE Base_Role = N'Owner' AND Is_System = 1);
DECLARE @AdminId INT = (SELECT ID FROM Permission_Groups WHERE Base_Role = N'Admin' AND Is_System = 1);
DECLARE @StaffId INT = (SELECT ID FROM Permission_Groups WHERE Base_Role = N'Staff' AND Is_System = 1);

INSERT INTO Permission_Group_Modules (Group_ID, Module_Code, Can_View, Can_Access)
SELECT @OwnerId, m.Code, m.OwnerOn, m.OwnerOn FROM @Modules m
 WHERE @OwnerId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Permission_Group_Modules p WHERE p.Group_ID = @OwnerId AND p.Module_Code = m.Code);

INSERT INTO Permission_Group_Modules (Group_ID, Module_Code, Can_View, Can_Access)
SELECT @AdminId, m.Code, m.AdminOn, m.AdminOn FROM @Modules m
 WHERE @AdminId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Permission_Group_Modules p WHERE p.Group_ID = @AdminId AND p.Module_Code = m.Code);

INSERT INTO Permission_Group_Modules (Group_ID, Module_Code, Can_View, Can_Access)
SELECT @StaffId, m.Code, m.StaffOn, m.StaffOn FROM @Modules m
 WHERE @StaffId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Permission_Group_Modules p WHERE p.Group_ID = @StaffId AND p.Module_Code = m.Code);

PRINT 'Seeded default permission matrix';
GO

-- ── 6) หมายเหตุ ───────────────────────────────────────────────────────────────
-- • ยังไม่ผูกพนักงานคนไหนกับกลุ่มโดยอัตโนมัติ (Permission_Group_ID = NULL)
--   → ทุกคนยังใช้สิทธิ์ตาม Role เดิม จนกว่าผู้ดูแลจะกำหนดกลุ่มให้เองในหน้า "กลุ่มสิทธิ์"
-- • ผู้ใช้ Role = Owner จะเข้าถึงได้ทุกส่วนเสมอ (กันล็อกตัวเองออกจากระบบ)
PRINT 'PHASE18_23 completed';
GO
