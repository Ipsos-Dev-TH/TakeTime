-- ============================================================================
-- ตรวจ "เงินเดือนขึ้น NextAcc แล้ว แต่ลงบัญชี (JE) หรือยัง"
-- ============================================================================
-- อ่านอย่างเดียว รันได้เลย
--
-- ใช้ตอบคำถาม: "ข้อมูลบน NextAcc ก็ขึ้นถูกต้องปกติ ทำไมระบบเราขึ้น error"
--
-- คำตอบสั้น ๆ: การนำเข้าเงินเดือนมี 3 ขั้น — import → approve → pay
--   • import สำเร็จ  = ข้อมูลพนักงาน/ยอดเงิน ขึ้นบน NextAcc ครบถ้วน (ที่เห็นว่าถูกต้อง)
--   • approve/pay    = ขั้นที่ NextAcc "สร้าง JE ลงบัญชี" ← ขั้นนี้ที่ล้มเหลว
-- ⇒ ข้อมูลถูกต้องจริง แต่ **ยังไม่เข้างบการเงิน** และออก ภ.ง.ด.1 / สปส.1-10 ไม่ได้
--
-- สคริปต์นี้ดูจาก log ว่าแต่ละงวดไปถึงขั้นไหน
-- ============================================================================

SET NOCOUNT ON;

-- ── 1) แต่ละงวดไปถึงขั้นไหน ─────────────────────────────────────────────────
PRINT '--- 1) ความคืบหน้าราย run (imported / approved / PAID) ---';
SELECT l.LogDateTime,
       CASE
           WHEN CAST(l.LogDetail AS NVARCHAR(MAX)) LIKE '%PAID run=%'     THEN N'③ PAID — ลง JE แล้ว ✅'
           WHEN CAST(l.LogDetail AS NVARCHAR(MAX)) LIKE '%approved run=%' THEN N'② approved'
           WHEN CAST(l.LogDetail AS NVARCHAR(MAX)) LIKE '%imported run=%' THEN N'① imported (ข้อมูลขึ้นแล้ว ยังไม่ลง JE)'
           WHEN CAST(l.LogDetail AS NVARCHAR(MAX)) LIKE '%FAILED run=%'   THEN N'❌ ล้มเหลว'
           ELSE N'อื่น ๆ'
       END AS ขั้นตอน,
       LEFT(CAST(l.LogDetail AS NVARCHAR(MAX)), 400) AS รายละเอียด
FROM Logs l
WHERE l.LogAction = 'AccountingSync'
  AND CAST(l.LogDetail AS NVARCHAR(MAX)) LIKE '%ProcessPayrollRunImport%'
ORDER BY l.LogDateTime DESC;
GO

-- ── 2) สรุป: งวดไหน import แล้วแต่ยังไม่ PAID (= ยังไม่ลง GL) ───────────────
PRINT '';
PRINT '--- 2) run ที่ข้อมูลขึ้นแล้วแต่ยังไม่ลงบัญชี ---';
WITH runs AS (
    SELECT
        SUBSTRING(d, CHARINDEX('run=', d) + 4, 36) AS RunId,
        MAX(CASE WHEN d LIKE '%PAID run=%' THEN 1 ELSE 0 END) AS Paid,
        MAX(CASE WHEN d LIKE '%imported run=%' THEN 1 ELSE 0 END) AS Imported,
        MIN(dt) AS FirstSeen, MAX(dt) AS LastSeen
    FROM (
        SELECT CAST(LogDetail AS NVARCHAR(MAX)) AS d, LogDateTime AS dt
        FROM Logs
        WHERE LogAction = 'AccountingSync'
          AND CAST(LogDetail AS NVARCHAR(MAX)) LIKE '%ProcessPayrollRunImport%'
          AND CHARINDEX('run=', CAST(LogDetail AS NVARCHAR(MAX))) > 0
    ) x
    GROUP BY SUBSTRING(d, CHARINDEX('run=', d) + 4, 36)
)
SELECT RunId, FirstSeen, LastSeen,
       CASE WHEN Paid = 1 THEN N'✅ ลง JE แล้ว'
            WHEN Imported = 1 THEN N'⚠️ ข้อมูลขึ้นแล้ว แต่ยังไม่ลง JE — ต้องกด Retry'
            ELSE N'?' END AS สถานะ
FROM runs
ORDER BY LastSeen DESC;
GO

-- ── 3) คิวเงินเดือนที่ยังค้าง ───────────────────────────────────────────────
PRINT '';
PRINT '--- 3) คิว IMPORT_PAYROLL_RUN ที่ยังไม่สำเร็จ ---';
SELECT ID, Entity_ID, Status, Retry_Count, Max_Retries, Created_Date,
       LEFT(CAST(ISNULL(Error_Message, N'') AS NVARCHAR(MAX)), 400) AS Error_Message
FROM Accounting_Sync_Queue
WHERE Action_Type = 'IMPORT_PAYROLL_RUN'
  AND Status <> 'COMPLETED'
ORDER BY ID DESC;
GO

-- ============================================================================
-- วิธียืนยันฝั่ง NextAcc (ทำในเว็บ NextAcc)
--   เปิดสมุดรายวัน/บัญชีแยกประเภท เดือนของงวดนั้น แล้วหา JE เงินเดือน
--   (Dr เงินเดือน + Dr สมทบนายจ้าง / Cr ประกันสังคม / Cr ภ.ง.ด.1 / Cr เงินสด)
--   • เจอ JE  → ลงบัญชีแล้วจริง (คิวขึ้น error เพราะขั้น pay ตอบ error แต่ NextAcc ลงให้แล้ว)
--               → กด Retry ครั้งเดียว ระบบจะเห็นสถานะ Paid แล้วปิดคิวเอง
--   • ไม่เจอ  → ยังไม่ลงบัญชีจริง ต้องแก้ต้นเหตุก่อน (ดูข้อความในคิวข้อ 3)
-- ============================================================================
