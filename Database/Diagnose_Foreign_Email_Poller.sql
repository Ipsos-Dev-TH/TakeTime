-- ============================================================================
-- ตรวจ "ใครเป็นคนอ่านอีเมลจอง STAAH" — ระบบนี้ หรือโปรแกรมเก่าที่ยังรันอยู่
-- ============================================================================
-- ⚠️ ข้อสรุปที่แก้แล้ว (21 ส.ค. 2026): ผลรันจริงของสคริปต์นี้ชี้ว่า "ไม่มีตัวอ่าน
--    แปลกปลอม" — ข้อ 3 เห็นเครื่องเดียว (THBKK1-WWW01) และเห็นบรรทัด
--    "intake skipped: lock held elsewhere" ทุก 5 นาทีติดกันตั้งแต่ 19/08 01:01
--    ต้นเหตุจริงคือ "ล็อกกันรันซ้อนค้างถาวร" ไม่ใช่โปรแกรมเก่าแย่งอ่าน
--    → ดู Database/Fix_Stuck_Intake_Lock.sql (แก้ทันที ไม่ต้อง deploy)
--      และ PHASE18_Migration_33_Run_Lease.sql (กันไม่ให้เกิดอีก)
--    สคริปต์นี้ยังใช้ได้สำหรับ "แยกว่ามีตัวอ่านตัวอื่นไหม" — แต่ต้องอ่านคู่กับข้อ 3:
--    ถ้าข้อ 3 เต็มไปด้วย skipped = ระบบไม่ได้อ่านเลย ไม่ใช่ถูกแย่ง
-- ============================================================================
-- อ่านอย่างเดียว รันได้เลย ไม่ต้อง deploy
--
-- บริบท: ข้อความ Telegram "บันทึกการจองไม่สำเร็จ (ห้องไม่ว่าง/ไม่มี mapping)"
-- เป็นข้อความของ "โค้ดรุ่นแรก" ที่ถูกลบไปแล้ว (commit 9d3dd19) และไม่มีบรรทัด
-- 🖥 เครื่อง/เวลา build ท้ายข้อความ ⇒ ผู้ส่งไม่ใช่ DLL ปัจจุบันบนเว็บ
-- ผู้ต้องสงสัย: โปรแกรม GetReservationfromGmail (Task Scheduler) / เว็บอินสแตนซ์เก่า
-- ============================================================================

SET NOCOUNT ON;

-- ── 1) ช่วงเวลาที่ได้รับข้อความเก่า (16:40 / 19:53) ระบบนี้ทำอะไรอยู่ ────────
--     ถ้าระบบนี้อ่านอีเมลจริง จะมี log ของรอบ intake ± ไม่กี่นาที
--     ไม่มี log เลย = คนส่งข้อความคือโปรแกรมอื่น
PRINT '--- 1) log การอ่านอีเมลของระบบนี้ (วันนี้ทั้งวัน) ---';
SELECT l.LogDateTime, LEFT(CAST(l.LogDetail AS NVARCHAR(MAX)), 500) AS LogDetail
FROM Logs l
WHERE l.LogAction = 'EmailReservation'
  AND l.LogDateTime >= CAST(GETDATE() AS DATE)
ORDER BY l.LogDateTime DESC;

-- ── 2) การจองที่ "โปรแกรมเก่า" ลงสำเร็จ (ถ้ามันใช้ DB เดียวกัน) ─────────────
--     โปรแกรมเก่าใส่ Reserve_By/Remark รูปแบบของมันเอง — การจอง STAAH วันนี้มีไหม
PRINT '';
PRINT '--- 2) การจอง OTA ที่ถูกสร้างวันนี้ (ดูว่าใครสร้าง) ---';
SELECT TOP 20 r.ID, r.Created_Date, r.Reserve_By, r.Status,
       r.CheckinDate, r.CheckoutDate, r.TotalPrice,
       LEFT(CAST(ISNULL(r.Remark, '') AS NVARCHAR(MAX)), 200) AS Remark
FROM Reservation r
WHERE r.Created_Date >= CAST(GETDATE() AS DATE)
ORDER BY r.ID DESC;

-- ── 3) รอบ intake ล่าสุดของระบบนี้ + instance ที่รัน ─────────────────────────
--     บรรทัด "อ่านอีเมล booking=..." / "intake skipped: lock held elsewhere"
--     มี [เครื่อง/เวลา build] ต่อท้าย — เห็นได้เลยว่ามีกี่ instance
PRINT '';
PRINT '--- 3) ร่องรอย instance (7 วันล่าสุด) ---';
SELECT l.LogDateTime, LEFT(CAST(l.LogDetail AS NVARCHAR(MAX)), 300) AS LogDetail
FROM Logs l
WHERE l.LogAction = 'EmailReservation'
  AND (CAST(l.LogDetail AS NVARCHAR(MAX)) LIKE N'%lock held%'
       OR CAST(l.LogDetail AS NVARCHAR(MAX)) LIKE N'%[[]%')   -- บรรทัดที่มี [stamp]
  AND l.LogDateTime >= DATEADD(DAY, -7, GETDATE())
ORDER BY l.LogDateTime DESC;

-- ── 4) config การอ่านอีเมลของระบบนี้ เปิดครบไหม ──────────────────────────────
PRINT '';
PRINT '--- 4) config intake ---';
SELECT ConfigKey, ConfigValue
FROM Accounting_Integration_Config
WHERE ConfigKey IN (N'Email_Rsv_Enabled', N'Email_Rsv_PollMinutes', N'Email_Rsv_RetryFailed',
                    N'Email_Rsv_RetryHours', N'Email_Rsv_RetryMaxPerRun', N'Email_Rsv_MoveFailed',
                    N'Email_Rsv_NotifyTelegram')
ORDER BY ConfigKey;
