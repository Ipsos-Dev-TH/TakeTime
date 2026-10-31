-- ============================================================================
-- ตรวจว่า "deploy ครบแล้วจริงไหม" — ไมเกรชัน 30–34 + การตั้งค่าที่ต้องมี
-- ============================================================================
-- อ่านอย่างเดียว รันได้เลย · รันหลัง deploy ทุกครั้ง
--
-- ทำไมต้องมี: ที่ผ่านมาเคยเกิด "deploy ไม่ครบ" หลายรอบ (ลืมรันไมเกรชันบางตัว /
-- ลืมก๊อบไฟล์ .aspx / DLL เก่ายังค้าง) แล้วอาการที่เห็นคล้ายบั๊กจนไล่ผิดทาง
-- สคริปต์นี้ตอบชัด ๆ ว่าอะไรยังขาด พร้อมบอกว่าต้องรันไฟล์ไหน
--
-- ผลลัพธ์: ทุกแถวควรขึ้น ✅ ถ้ามี ❌ ให้รันไฟล์ที่ระบุในคอลัมน์ Fix
-- ============================================================================

SET NOCOUNT ON;

DECLARE @r TABLE (Seq INT, Item NVARCHAR(80), Status NVARCHAR(10), Detail NVARCHAR(300), Fix NVARCHAR(120));

-- ── 30) index บนตาราง Logs (แก้ปุ่มดู log ค้าง) ─────────────────────────────
INSERT INTO @r
SELECT 1, N'30 · index IX_Logs_Action_Date',
       CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_Action_Date'
                         AND object_id = OBJECT_ID('Logs')) THEN N'✅' ELSE N'❌' END,
       N'ไม่มี = การค้น log ช้ามาก ปุ่มดู log ค้าง',
       N'PHASE18_Migration_30_Logs_Index.sql';

-- ── 31) คิวกู้ตัวเองได้ (Processing_Started) ────────────────────────────────
INSERT INTO @r
SELECT 2, N'31 · Accounting_Sync_Queue.Processing_Started',
       CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                         WHERE TABLE_NAME = 'Accounting_Sync_Queue'
                           AND COLUMN_NAME = 'Processing_Started') THEN N'✅' ELSE N'❌' END,
       N'ไม่มี = คิวที่ค้าง PROCESSING ถูกดึงกลับผิดจังหวะ เสี่ยงเอกสารซ้ำ',
       N'PHASE18_Migration_31_Queue_Resilience.sql';

-- ── 32) ผูกใบเสร็จกับเอกสาร NextAcc บนตัวใบเอง ──────────────────────────────
INSERT INTO @r
SELECT 3, N'32 · Account_Receipt.Nexaacc_Doc_Id',
       CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                         WHERE TABLE_NAME = 'Account_Receipt'
                           AND COLUMN_NAME = 'Nexaacc_Doc_Id') THEN N'✅' ELSE N'❌' END,
       N'ไม่มี = ปุ่มแก้ไขเอกสารหาย / จับคู่ใบเสร็จกับ NextAcc ไม่ติด',
       N'PHASE18_Migration_32_Receipt_NextAcc_Link.sql';

-- ── 33) ล็อกงานเบื้องหลังแบบมีวันหมดอายุ (ตัวที่แก้เหตุการณ์ 19–21 ส.ค.) ────
INSERT INTO @r
SELECT 4, N'33 · ตาราง App_Run_Lease',
       CASE WHEN OBJECT_ID('App_Run_Lease', 'U') IS NOT NULL THEN N'✅' ELSE N'❌' END,
       N'ไม่มี = งานเบื้องหลังทำงานแบบไม่มีตัวกันรันซ้อน (ยังทำงานได้ แต่ไม่ปลอดภัย)',
       N'PHASE18_Migration_33_Run_Lease.sql';

-- ── 34) กวาดกู้อีเมลที่ตกหล่น ───────────────────────────────────────────────
INSERT INTO @r
SELECT 5, N'34 · คีย์ Email_Rsv_RecoverDaily',
       CASE WHEN EXISTS (SELECT 1 FROM Accounting_Integration_Config
                         WHERE ConfigKey = 'Email_Rsv_RecoverDaily') THEN N'✅' ELSE N'❌' END,
       N'ไม่มี = ใช้ค่าตั้งต้นในโค้ด (เปิดอยู่) แต่แก้จากฐานข้อมูลไม่ได้',
       N'PHASE18_Migration_34_Email_Recover.sql';

-- ── การตั้งค่าที่เคยตั้งผิดแล้วทำให้อีเมลตกหล่น ─────────────────────────────
INSERT INTO @r
SELECT 6, N'ตั้งค่า · Email_Rsv_RetryHours ≥ 24',
       CASE WHEN ISNULL(TRY_CAST((SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config
                                  WHERE ConfigKey = 'Email_Rsv_RetryHours') AS INT), 72) >= 24
            THEN N'✅' ELSE N'❌' END,
       N'ค่าปัจจุบัน = ' + ISNULL((SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config
                                   WHERE ConfigKey = 'Email_Rsv_RetryHours'), N'(ไม่ได้ตั้ง → 72)')
       + N' · ต่ำเกิน = อีเมลที่ลงจองไม่สำเร็จเมื่อวานตกขบวนถาวร',
       N'PHASE18_Migration_34_Email_Recover.sql';

INSERT INTO @r
SELECT 7, N'ตั้งค่า · Email_Rsv_Enabled = 1',
       CASE WHEN (SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config
                  WHERE ConfigKey = 'Email_Rsv_Enabled') = '1' THEN N'✅' ELSE N'❌' END,
       N'ปิดอยู่ = ไม่อ่านอีเมลเลย และไม่มี error ให้เห็น',
       N'เปิดในหน้า Admin → อีเมลจอง';

-- ── DLL ใหม่ทำงานอยู่จริงไหม (ดูจากร่องรอยที่มีเฉพาะรุ่นใหม่) ───────────────
INSERT INTO @r
SELECT 8, N'DLL ใหม่ · มีรอบอ่านอีเมลสำเร็จบันทึกไว้',
       CASE WHEN EXISTS (SELECT 1 FROM Accounting_Integration_Config
                         WHERE ConfigKey = 'Email_Rsv_LastSuccess') THEN N'✅' ELSE N'❌' END,
       N'ไม่มี = DLL ที่รันอยู่ยังเป็นรุ่นเก่า (รุ่นใหม่เขียนคีย์นี้ทุกรอบที่สำเร็จ) '
       + N'· ค่าล่าสุด = ' + ISNULL((SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config
                                     WHERE ConfigKey = 'Email_Rsv_LastSuccess'), N'(ยังไม่มี)'),
       N'deploy bin\*.dll + .aspx แล้ว recycle app pool';

INSERT INTO @r
SELECT 9, N'ไม่มี applock เก่าค้าง',
       CASE WHEN NOT EXISTS (SELECT 1 FROM sys.dm_tran_locks
                             WHERE resource_type = 'APPLICATION'
                               AND (resource_description LIKE '%EmailRsvIntake%'
                                    OR resource_description LIKE '%AccountingSyncQueue%'))
            THEN N'✅' ELSE N'❌' END,
       N'มีค้าง = DLL เก่ายังรันอยู่ใน process เดิม',
       N'recycle app pool · ดู Fix_Stuck_Intake_Lock.sql';

SELECT Item, Status, Detail, Fix FROM @r ORDER BY Seq;

DECLARE @bad INT = (SELECT COUNT(*) FROM @r WHERE Status = N'❌');
PRINT '';
IF @bad = 0
    PRINT N'✅ deploy ครบถ้วน — ระบบพร้อมทำงาน';
ELSE
    PRINT N'❌ ยังขาด ' + CAST(@bad AS NVARCHAR(10)) + N' รายการ — ดูคอลัมน์ Fix';
GO

-- ── สถานะงานเบื้องหลังตอนนี้ (ถ้ามีตาราง lease แล้ว) ────────────────────────
IF OBJECT_ID('App_Run_Lease', 'U') IS NOT NULL
BEGIN
    PRINT '';
    PRINT '--- สถานะล็อกงานเบื้องหลัง ---';
    SELECT Lock_Name, Owner, Acquired_At, Heartbeat_At, Expires_At,
           CASE WHEN Expires_At IS NULL OR Expires_At <= GETDATE()
                THEN N'ว่าง' ELSE N'กำลังทำงาน' END AS Status
    FROM App_Run_Lease;
END
GO
