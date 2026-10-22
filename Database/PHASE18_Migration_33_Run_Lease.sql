-- ============================================================================
-- PHASE18 Migration 33 — App_Run_Lease: ล็อกงานเบื้องหลังแบบมีวันหมดอายุ
-- ============================================================================
-- ปัญหาที่แก้ (เกิดจริง 19–21 ส.ค. 2026 — การจอง STAAH ไม่เข้าระบบ 2 วันเต็ม):
--   การอ่านอีเมลจองใช้ sp_getapplock (@LockOwner = 'Session') กันรันซ้อน
--   โดยคิดว่า "ปิด connection แล้วล็อกจะหลุดเอง" — ซึ่งไม่จริงกับ connection pool
--   ของ .NET: Dispose() แค่คืน connection เข้า pool ไม่ได้ปิด session ที่ฝั่ง SQL
--   พอ deploy DLL ใหม่ AppDomain เก่าถูกทิ้งแต่ w3wp ยังอยู่ → pool เก่าพร้อม
--   connection ที่ถือล็อกลอยค้าง ไม่มีใครหยิบไปใช้ (จึงไม่มี sp_reset_connection)
--   ⇒ ล็อกค้างถาวร ทุกรอบ 5 นาทีเขียน log "intake skipped: lock held elsewhere"
--   แล้วข้ามไป ไม่มีอีเมลฉบับไหนถูกอ่านอีกเลย และไม่มีการแจ้งเตือนใด ๆ
--
-- ทางแก้: เลิกผูกล็อกกับอายุ connection — เก็บเป็นแถวที่ "มีวันหมดอายุ" แทน
--   กรณีเลวร้ายที่สุด (process ตายคางาน / thread ค้าง) งานหยุดแค่ไม่เกินอายุ lease
--   แล้วรอบถัดไปยึดคืนได้เอง ไม่ต้อง recycle app pool ไม่ต้องมีคนมาแก้
--
-- ปลอดภัย: รันซ้ำได้ ไม่ลบข้อมูล ไม่แตะ GL
-- ============================================================================

SET NOCOUNT ON;

IF OBJECT_ID('App_Run_Lease', 'U') IS NULL
BEGIN
    CREATE TABLE App_Run_Lease (
        Lock_Name    NVARCHAR(100) NOT NULL PRIMARY KEY,  -- ชื่องาน เช่น EmailReservationIntake
        Owner        NVARCHAR(200) NULL,                  -- เครื่อง/process/รอบที่ถืออยู่
        Acquired_At  DATETIME      NULL,                  -- เริ่มถือเมื่อไหร่
        Heartbeat_At DATETIME      NULL,                  -- ต่ออายุครั้งล่าสุด (ดูว่ายังมีชีวิต)
        Expires_At   DATETIME      NULL                   -- หมดอายุเมื่อไหร่ (NULL/อดีต = ว่าง)
    );
    PRINT 'สร้างตาราง App_Run_Lease';
END
ELSE
    PRINT 'App_Run_Lease มีอยู่แล้ว — ข้าม';
GO

-- ── ตั้งค่าที่เกี่ยวข้อง (เพิ่มเฉพาะคีย์ที่ยังไม่มี — ไม่ทับค่าที่ตั้งไว้แล้ว) ──────
--   Email_Rsv_LeaseMinutes  : อายุ lease ของรอบอ่านอีเมล (นาที). รอบปกติจบใน < 1 นาที
--                             ตั้ง 15 = เผื่อ IMAP ช้า/อีเมลค้างเยอะ แต่ถ้าค้างจริงก็หลุดเองใน 15 นาที
--   Email_Rsv_StaleAlertMin : ไม่มีรอบไหน "สำเร็จ" เกินกี่นาที ให้เตือน Telegram (0 = ปิด)
--                             เตือนซ้ำได้ไม่เกินชั่วโมงละครั้ง
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_LeaseMinutes')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Email_Rsv_LeaseMinutes', '15',
            N'อายุล็อกรอบอ่านอีเมลจอง (นาที) — ค้างเกินนี้ระบบยึดคืนเองอัตโนมัติ', GETDATE());

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_StaleAlertMin')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Email_Rsv_StaleAlertMin', '60',
            N'ไม่มีรอบอ่านอีเมลสำเร็จเกินกี่นาที ให้เตือนเข้า Telegram (0 = ปิดการเตือน)', GETDATE());
GO

-- ── ล้างล็อกเก่าที่ค้างจาก sp_getapplock (ถ้ายังค้างอยู่ ณ ตอนรันไมเกรชัน) ──────
-- หมายเหตุ: sp_releaseapplock ปล่อยได้เฉพาะ "session ที่ถือเอง" ⇒ สคริปต์นี้ปล่อยแทนไม่ได้
-- แต่หลัง deploy โค้ดใหม่ ระบบเลิกใช้ล็อกตัวนั้นแล้ว ล็อกที่ค้างจึงไม่มีผลอีกต่อไป
-- (ถ้าอยากล้างทันทีก่อน deploy: recycle app pool หรือ KILL spid — ดู
--  Database/Diagnose_Stuck_Intake_Lock.sql)
IF EXISTS (SELECT 1 FROM sys.dm_tran_locks
           WHERE resource_type = 'APPLICATION'
             AND resource_description LIKE '%EmailRsvIntake%')
    PRINT '⚠️ ยังมี applock เก่า TakeTime_EmailRsvIntake ค้างอยู่ — ไม่เป็นไร โค้ดใหม่ไม่ใช้แล้ว';
GO

SELECT Lock_Name, Owner, Acquired_At, Heartbeat_At, Expires_At,
       CASE WHEN Expires_At IS NULL OR Expires_At <= GETDATE() THEN N'ว่าง' ELSE N'กำลังทำงาน' END AS Status
FROM App_Run_Lease;
