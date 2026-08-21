-- ============================================================================
-- เช็คด่วน: "ตอนนี้ระบบอ่านอีเมลจองอยู่จริงไหม" — รันแล้วรู้คำตอบใน 1 หน้าจอ
-- ============================================================================
-- อ่านอย่างเดียว รันได้เลย · ใช้ตอบคำถาม "กดดึงแล้ว/รอหลายนาทีแล้ว ทำไมยังไม่มีจองเข้า"
-- ============================================================================

SET NOCOUNT ON;

-- ── 1) 30 นาทีล่าสุด ระบบทำอะไรกับอีเมลบ้าง ──────────────────────────────────
--    เห็น "intake skipped: lock held elsewhere" = ล็อกเก่ายังค้าง → ต้อง recycle
--      app pool (หรือ KILL spid ตาม Fix_Stuck_Intake_Lock.sql) ก่อน อย่างอื่นไม่ช่วย
--    เห็น "IMAP error: ..." = ต่อเมลไม่ได้ อ่านข้อความ error ตรง ๆ
--    เห็น "อ่านอีเมล booking=..." / "retry failed folder: ..." = ระบบทำงานแล้ว
--    ไม่มีบรรทัดเลย = timer ยังไม่ถึงรอบ (หลัง restart รอ 1 นาที + รอบทุก 5 นาที)
--      หรือ Email_Rsv_Enabled ปิดอยู่ (ดูข้อ 3)
PRINT '--- 1) log อีเมลจอง 30 นาทีล่าสุด ---';
SELECT l.LogDateTime, LEFT(CAST(l.LogDetail AS NVARCHAR(MAX)), 300) AS LogDetail
FROM Logs l
WHERE l.LogAction = 'EmailReservation'
  AND l.LogDateTime >= DATEADD(MINUTE, -30, GETDATE())
ORDER BY l.LogDateTime DESC;

-- ── 2) ล็อกเก่า (sp_getapplock) ยังค้างอยู่ไหม ───────────────────────────────
--    มีแถว = ยังค้าง → DLL เก่าจะข้ามรอบตลอดไป · DLL ใหม่ (2026-08-21.1) ไม่สนล็อกนี้แล้ว
PRINT '';
PRINT '--- 2) applock เก่าที่ยังค้าง (ควรว่าง) ---';
SELECT l.request_session_id AS Spid, l.resource_description,
       s.host_name, s.program_name, s.status,
       DATEDIFF(MINUTE, s.last_request_end_time, GETDATE()) AS IdleMinutes
FROM sys.dm_tran_locks l
LEFT JOIN sys.dm_exec_sessions s ON s.session_id = l.request_session_id
WHERE l.resource_type = 'APPLICATION'
  AND (l.resource_description LIKE '%EmailRsvIntake%'
       OR l.resource_description LIKE '%AccountingSyncQueue%');

-- ── 3) config + รอบสำเร็จล่าสุด ──────────────────────────────────────────────
PRINT '';
PRINT '--- 3) config (Enabled ต้อง = 1) + เวลารอบสำเร็จล่าสุด ---';
SELECT ConfigKey, ConfigValue, Updated_Date
FROM Accounting_Integration_Config
WHERE ConfigKey IN (N'Email_Rsv_Enabled', N'Email_Rsv_PollMinutes', N'Email_Rsv_RetryFailed',
                    N'Email_Rsv_RetryHours', N'Email_Rsv_LastSuccess', N'Email_Rsv_LeaseMinutes')
ORDER BY ConfigKey;

-- ── 4) lease ใหม่ (หลังไมเกรชัน 33) — สถานะล็อกปัจจุบันแบบอ่านง่าย ───────────
PRINT '';
PRINT '--- 4) App_Run_Lease (ถ้ายังไม่มีตาราง = ยังไม่ได้รันไมเกรชัน 33) ---';
IF OBJECT_ID('App_Run_Lease', 'U') IS NOT NULL
    SELECT Lock_Name, Owner, Acquired_At, Heartbeat_At, Expires_At,
           CASE WHEN Expires_At IS NULL OR Expires_At <= GETDATE()
                THEN N'ว่าง' ELSE N'กำลังทำงาน' END AS Status
    FROM App_Run_Lease;
ELSE
    PRINT 'ยังไม่มีตาราง App_Run_Lease';

-- ── 5) การจองที่เข้าวันนี้ (นับผลจริง) ───────────────────────────────────────
PRINT '';
PRINT '--- 5) การจองที่ถูกสร้างวันนี้ ---';
SELECT TOP 20 r.ID, r.Created_Date, r.Reserve_By, r.Status, r.CheckinDate, r.CheckoutDate,
       LEFT(CAST(ISNULL(r.Remark, '') AS NVARCHAR(MAX)), 150) AS Remark
FROM Reservation r
WHERE r.Created_Date >= CAST(GETDATE() AS DATE)
ORDER BY r.ID DESC;
