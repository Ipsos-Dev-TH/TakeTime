-- ============================================================================
-- ปลดล็อกที่ค้าง ทำให้ระบบกลับมาอ่านอีเมลจอง STAAH ได้ทันที (ไม่ต้อง deploy)
-- ============================================================================
-- อาการ: ใน Logs มีบรรทัด "intake skipped: lock held elsewhere" ทุก ๆ 5 นาที
--        ติดต่อกันเป็นวัน ๆ และไม่มีการจองจาก OTA เข้าระบบเลย
--
-- สาเหตุ: ล็อกกันรันซ้อนใช้ sp_getapplock (@LockOwner = 'Session') ซึ่งผูกกับ
--        "SQL session" ของ SqlConnection — แต่ .NET ใช้ connection pool
--        Dispose() แค่คืน connection เข้า pool ไม่ได้ปิด session จริง
--        พอ deploy DLL ใหม่ AppDomain เก่าถูกทิ้งพร้อม pool ที่ยังถือล็อกอยู่
--        connection นั้นไม่มีใครหยิบไปใช้อีก (จึงไม่เกิด sp_reset_connection)
--        ⇒ ล็อกค้างถาวรจนกว่า process w3wp จะตาย
--
-- สคริปต์นี้: ส่วนที่ 1–2 อ่านอย่างเดียว (รันได้เลย) · ส่วนที่ 3 คือการแก้
-- ============================================================================

SET NOCOUNT ON;

-- ── 1) ใครถือล็อกอยู่ ─────────────────────────────────────────────────────
PRINT '--- 1) session ที่ถือ applock ของงานอ่านอีเมล/คิวบัญชี ---';
SELECT
    l.request_session_id                      AS Spid,
    l.resource_description                    AS LockResource,
    l.request_mode                            AS Mode,
    s.login_name, s.host_name, s.program_name,
    s.login_time, s.last_request_start_time, s.last_request_end_time,
    DATEDIFF(MINUTE, s.last_request_end_time, GETDATE()) AS IdleMinutes,
    s.status
FROM sys.dm_tran_locks l
LEFT JOIN sys.dm_exec_sessions s ON s.session_id = l.request_session_id
WHERE l.resource_type = 'APPLICATION'
  AND (l.resource_description LIKE '%EmailRsvIntake%'
       OR l.resource_description LIKE '%AccountingSyncQueue%');
-- IdleMinutes สูงมาก (เป็นชั่วโมง/วัน) + status = 'sleeping' = connection ผีใน pool เก่า
-- ไม่ได้กำลังทำงานอะไรอยู่จริง — ปลอดภัยที่จะปิด

-- ── 2) ระบบข้ามรอบมานานแค่ไหน ─────────────────────────────────────────────
PRINT '';
PRINT '--- 2) รอบอ่านอีเมลล่าสุดที่ "ทำงานจริง" กับที่ "ถูกข้าม" ---';
SELECT TOP 5 'ทำงานจริงล่าสุด' AS Kind, l.LogDateTime,
       LEFT(CAST(l.LogDetail AS NVARCHAR(MAX)), 200) AS LogDetail
FROM Logs l
WHERE l.LogAction = 'EmailReservation'
  AND CAST(l.LogDetail AS NVARCHAR(MAX)) NOT LIKE '%skipped%'
ORDER BY l.LogDateTime DESC;

SELECT COUNT(*) AS SkippedRounds,
       MIN(l.LogDateTime) AS SkippingSince,
       MAX(l.LogDateTime) AS LastSkip
FROM Logs l
WHERE l.LogAction = 'EmailReservation'
  AND CAST(l.LogDetail AS NVARCHAR(MAX)) LIKE '%skipped%'
  AND l.LogDateTime >= DATEADD(DAY, -14, GETDATE());

-- ============================================================================
-- 3) วิธีแก้ให้กลับมาทำงานทันที — เลือกอย่างใดอย่างหนึ่ง
-- ============================================================================
-- วิธี ก (แนะนำ ไม่ต้องแตะ SQL): recycle application pool ของเว็บบน IIS
--        → w3wp ตาย → ทุก connection ปิด → ล็อกหลุดทั้งหมด
--        รอบถัดไป (ภายใน 5 นาที) ระบบจะอ่านอีเมลค้าง 2 วันย้อนหลังให้เองทั้งหมด
--        (อีเมลยังอยู่ใน INBOX สถานะ "ยังไม่อ่าน" — ไม่มีฉบับไหนหาย)
--
-- วิธี ข: ปิด session ที่ถือล็อกทิ้ง (ต้องมีสิทธิ์ ALTER ANY CONNECTION)
--        เอาเลข Spid จากผลข้อ 1 มาใส่ แล้วเอาคอมเมนต์ออก — ปิดทีละตัว
--        ⚠ ตรวจก่อนว่า IdleMinutes สูงและ status = 'sleeping' (ไม่ได้ทำงานค้างอยู่)
--
-- KILL 57;   -- <<< แก้เป็นเลข Spid จริงจากข้อ 1
--
-- หมายเหตุ: sp_releaseapplock ใช้ไม่ได้ที่นี่ — ปล่อยได้เฉพาะ session ที่ถือเอง
-- ============================================================================

-- ── 4) หลังแก้: ยืนยันว่าล็อกหลุดแล้ว ─────────────────────────────────────
PRINT '';
PRINT '--- 4) รันซ้ำหลังแก้ ควรได้ 0 แถว ---';
SELECT COUNT(*) AS StillLocked
FROM sys.dm_tran_locks
WHERE resource_type = 'APPLICATION'
  AND (resource_description LIKE '%EmailRsvIntake%'
       OR resource_description LIKE '%AccountingSyncQueue%');

-- ── 5) กันไม่ให้เกิดอีก ────────────────────────────────────────────────────
-- รัน Database/PHASE18_Migration_33_Run_Lease.sql + deploy DLL รุ่นใหม่
-- โค้ดใหม่เลิกใช้ sp_getapplock แล้ว เปลี่ยนเป็นล็อกแบบ "มีวันหมดอายุ"
-- (ตาราง App_Run_Lease) ⇒ ค้างได้อย่างมากไม่เกินอายุ lease แล้วยึดคืนเอง
-- พร้อมเตือนเข้า Telegram ถ้าไม่มีรอบอ่านอีเมลสำเร็จเกิน Email_Rsv_StaleAlertMin นาที
