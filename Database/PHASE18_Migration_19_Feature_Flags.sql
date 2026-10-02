-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 19: Feature Flags — สวิตช์เปิด/ปิดฟีเจอร์รายโมดูล
-- ════════════════════════════════════════════════════════════════════════════
-- ตั้งค่าที่ ศูนย์รวมการตั้งค่าระบบ (Admin → Settings → System Settings) หมวด "ฟีเจอร์"
-- ปิดแล้ว: เมนูซ่อน + เข้าหน้าโมดูลตรง ๆ ถูก redirect ออก + การ์ดใน Guest Portal ซ่อน
-- ข้อมูลเดิมไม่ถูกลบ — เปิดกลับมาใช้ต่อได้ทันที
--
-- ค่า: NULL = ใช้ค่าเริ่มต้นในโค้ด (เปิด ยกเว้น Housekeeping/Maintenance/DynamicPricing ปิด)
--      'true' / 'false' = บังคับตามที่ตั้ง
-- ต้องรัน PHASE18_Migration_18 (System_Config) ก่อน. idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

IF OBJECT_ID('dbo.System_Config', 'U') IS NULL
BEGIN
    RAISERROR(N'ต้องรัน PHASE18_Migration_18_System_Config.sql ก่อน', 16, 1);
    RETURN;
END

MERGE [dbo].[System_Config] AS t
USING (VALUES
    -- ปิดเป็นค่าเริ่มต้น (ยังไม่ใช้งานจริง / ยังไม่ได้ต่อกับระบบหลัก)
    ('Feature_Housekeeping',   'FEATURE', N'🧹 แม่บ้าน / สถานะทำความสะอาด',
     N'ค่าเริ่มต้น: ปิด — Dashboard แม่บ้าน + สถานะห้อง (ยังไม่ได้ผูกกับเช็คอิน/เช็คเอาท์)', 0, 'bool', 10),
    ('Feature_Maintenance',    'FEATURE', N'🔧 งานซ่อมบำรุง',
     N'ค่าเริ่มต้น: ปิด — Dashboard แจ้งซ่อม/ติดตามงานซ่อม', 0, 'bool', 20),
    ('Feature_DynamicPricing', 'FEATURE', N'📈 ราคาไดนามิก',
     N'ค่าเริ่มต้น: ปิด — หน้าตั้งราคาอัตโนมัติ (ยังไม่ได้ต่อเข้ากับราคาจองจริง)', 0, 'bool', 30),

    -- เปิดเป็นค่าเริ่มต้น
    ('Feature_RoomService',    'FEATURE', N'🍽️ รูมเซอร์วิส',
     N'ค่าเริ่มต้น: เปิด — สั่งอาหารจาก Guest Portal + หน้าจัดการออเดอร์', 0, 'bool', 40),
    ('Feature_Activities',     'FEATURE', N'🏓 กิจกรรมในที่พัก',
     N'ค่าเริ่มต้น: เปิด — หน้ากิจกรรม (เว็บ+Portal) + จองรอบเวลา + หน้าจัดการ', 0, 'bool', 50),
    ('Feature_Chat',           'FEATURE', N'💬 แชทลูกค้า (Omni-Channel)',
     N'ค่าเริ่มต้น: เปิด — กล่องแชทรวม LINE/FB/อีเมล OTA + แชท Guest Portal', 0, 'bool', 60),
    ('Feature_Loyalty',        'FEATURE', N'🏆 สะสมแต้ม / สมาชิก',
     N'ค่าเริ่มต้น: เปิด — Loyalty, Membership, แลกแต้ม, Tier Benefits, แต้มใน Portal', 0, 'bool', 70),
    ('Feature_Reviews',        'FEATURE', N'⭐ รีวิวลูกค้า',
     N'ค่าเริ่มต้น: เปิด — จัดการรีวิว + AI วิเคราะห์รีวิว + หน้ารีวิวใน Portal', 0, 'bool', 80),
    ('Feature_Affiliate',      'FEATURE', N'🤝 Affiliate',
     N'ค่าเริ่มต้น: เปิด — ระบบตัวแทน/ค่าคอมมิชชั่น + เมนูหน้าเว็บ', 0, 'bool', 90),
    ('Feature_AI',             'FEATURE', N'🤖 ผู้ช่วย AI',
     N'ค่าเริ่มต้น: เปิด — ตั้งค่า AI, คลังความรู้, รายงานสรุปด้วย AI', 0, 'bool', 100),
    ('Feature_ChannelManager', 'FEATURE', N'🌐 Channel Manager Dashboard',
     N'ค่าเริ่มต้น: เปิด — หน้าสรุปช่องทาง OTA', 0, 'bool', 110),
    ('Feature_WebAnalytics',   'FEATURE', N'📊 สถิติเว็บไซต์',
     N'ค่าเริ่มต้น: เปิด — หน้าวิเคราะห์การเข้าชมเว็บ', 0, 'bool', 120),
    ('Feature_Assets',         'FEATURE', N'🗄️ ทะเบียนทรัพย์สิน',
     N'ค่าเริ่มต้น: เปิด — จัดการทรัพย์สิน/ครุภัณฑ์', 0, 'bool', 130),
    ('Feature_HR',             'FEATURE', N'👥 HR (พนักงาน/ลา/เงินเดือน/OT)',
     N'ค่าเริ่มต้น: เปิด — เมนูและหน้าจัดการพนักงาน ใบลา เงินเดือน โอที', 0, 'bool', 140),
    ('Feature_GuestPortal',    'FEATURE', N'🏨 Guest Portal',
     N'ค่าเริ่มต้น: เปิด — พอร์ทัลลูกค้าเข้าพัก (เมนูหน้าเว็บ + หน้า Portal)', 0, 'bool', 150)
) AS s (ConfigKey, Category, DisplayName, Description, IsSecret, InputType, DisplayOrder)
ON t.ConfigKey = s.ConfigKey
WHEN MATCHED THEN UPDATE SET
    Category = s.Category, DisplayName = s.DisplayName, Description = s.Description,
    IsSecret = s.IsSecret, InputType = s.InputType, DisplayOrder = s.DisplayOrder
WHEN NOT MATCHED THEN
    INSERT (ConfigKey, ConfigValue, Category, DisplayName, Description, IsSecret, InputType, DisplayOrder)
    VALUES (s.ConfigKey, NULL, s.Category, s.DisplayName, s.Description, s.IsSecret, s.InputType, s.DisplayOrder);

PRINT N'PHASE18_19: Feature flags seeded (' + CAST(@@ROWCOUNT AS NVARCHAR) + N' keys)';
GO
