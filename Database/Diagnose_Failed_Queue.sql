-- ============================================================================
-- แยกสาเหตุ "คิวบัญชี NextAcc ล้มเหลวจนหมด retry" — 45 รายการเป็นเพราะอะไรบ้าง
-- ============================================================================
-- อ่านอย่างเดียว รันได้เลย
--
-- ใช้ตอบ Telegram: "❌ ล้มเหลวจนหมด retry: 45 รายการ"
-- แทนที่จะเปิดดูทีละใบ สคริปต์นี้จัดกลุ่มตามสาเหตุจริง แล้วบอกว่าต้องแก้อะไรก่อน
--
-- ⚠️ อย่าเพิ่งกด "Retry ทั้งหมด" ก่อนแก้ต้นเหตุ — จะล้มเหลวซ้ำแล้วเผา retry ทิ้งเปล่า ๆ
-- ============================================================================

SET NOCOUNT ON;

-- ── 1) จัดกลุ่มตามสาเหตุ ────────────────────────────────────────────────────
PRINT '--- 1) สาเหตุที่ทำให้คิวตาย (เรียงตามจำนวน) ---';
WITH q AS (
    SELECT ID, Action_Type, Created_Date,
           CAST(ISNULL(Error_Message, N'') AS NVARCHAR(MAX)) AS Err
    FROM Accounting_Sync_Queue
    WHERE Status = 'FAILED' AND Retry_Count >= Max_Retries
),
c AS (
    SELECT ID, Action_Type, Created_Date, Err,
        CASE
            WHEN Err LIKE N'%ไม่พบผังบัญชี%'                      THEN N'① ไม่พบผังบัญชี (mapping ชี้รหัสที่ NextAcc ไม่มี)'
            WHEN Err LIKE N'%86/4%' OR Err LIKE N'%u0e21u0e32u0e15u0e23u0e32 86%'
                                                                   THEN N'② ข้อมูลผู้ซื้อไม่ครบตาม §86/4'
            WHEN Err LIKE N'%401%' OR Err LIKE N'%Unauthorized%'   THEN N'③ คีย์ผิด/หมดอายุ (401)'
            WHEN Err LIKE N'%500%' OR Err LIKE N'%starting the application%'
                 OR Err LIKE N'%AggregateException%'               THEN N'④ NextAcc ล่มตอนนั้น (500) — น่าจะ retry ผ่านแล้ว'
            WHEN Err LIKE N'%timeout%' OR Err LIKE N'%timed out%'
                 OR Err LIKE N'%unreachable%' OR Err LIKE N'%No such host%'
                                                                   THEN N'⑤ ต่อ NextAcc ไม่ได้ (เน็ต/DNS)'
            WHEN Err LIKE N'%ชำระแล้ว%' OR Err LIKE N'%ยื่น%' OR Err LIKE N'%guard%'
                                                                   THEN N'⑥ ติด guard ฝั่ง NextAcc (แก้เอกสารเดิมไม่ได้)'
            WHEN Err LIKE N'%400%'                                 THEN N'⑦ ข้อมูลไม่ผ่าน validation อื่น (400)'
            ELSE N'⑧ อื่น ๆ'
        END AS Cause
    FROM q
)
SELECT Cause,
       COUNT(*)                                   AS จำนวน,
       MIN(Created_Date)                          AS เก่าสุด,
       MAX(Created_Date)                          AS ใหม่สุด,
       LEFT(MIN(CAST(ID AS NVARCHAR(20))), 20)    AS ตัวอย่างคิวID,
       LEFT(MIN(Err), 300)                        AS ตัวอย่างข้อความ
FROM c
GROUP BY Cause
ORDER BY COUNT(*) DESC;
GO

-- ── 2) รหัสบัญชีที่ NextAcc บอกว่า "ไม่พบ" (ต้นเหตุอันดับ 1) ─────────────────
-- ดึงรหัสที่อยู่หลังคำว่า "ไม่พบผังบัญชี:" ออกมานับ แล้วเทียบกับผังบัญชีที่ sync มา
PRINT '';
PRINT '--- 2) รหัสบัญชีที่หายไป + มีในผังบัญชี NextAcc แล้วหรือยัง ---';
WITH e AS (
    SELECT LTRIM(SUBSTRING(Err, CHARINDEX(N'ไม่พบผังบัญชี:', Err) + 14, 12)) AS Tail
    FROM (
        SELECT CAST(ISNULL(Error_Message, N'') AS NVARCHAR(MAX)) AS Err
        FROM Accounting_Sync_Queue
        WHERE Status = 'FAILED' AND Retry_Count >= Max_Retries
    ) x
    WHERE CHARINDEX(N'ไม่พบผังบัญชี:', Err) > 0
),
codes AS (
    SELECT LEFT(Tail, PATINDEX('%[^0-9]%' , Tail + 'x') - 1) AS Code FROM e
)
SELECT c.Code                                        AS รหัสบัญชี,
       COUNT(*)                                      AS คิวที่ติด,
       CASE WHEN EXISTS (SELECT 1 FROM Accounting_Nexaacc_Accounts a WHERE a.Account_Code = c.Code)
            THEN N'✅ มีในผังแล้ว (กด Sync บัญชี แล้ว Retry ได้เลย)'
            ELSE N'❌ ยังไม่มี — ต้องสร้างใน NextAcc หรือแก้ mapping' END AS สถานะ,
       ISNULL((SELECT TOP 1 m.TakeTime_Code FROM Accounting_Account_Mapping m
               WHERE m.Nexaacc_AccountCode = c.Code), N'(ไม่ได้ผูกใน mapping)') AS ผูกกับ
FROM codes c
WHERE c.Code <> ''
GROUP BY c.Code
ORDER BY COUNT(*) DESC;
GO

-- ── 3) รายการคิวที่ตาย แยกตามชนิดงาน (ผลกระทบทางบัญชี) ──────────────────────
PRINT '';
PRINT '--- 3) ชนิดเอกสารที่ยังไม่ขึ้น NextAcc ---';
SELECT Action_Type AS ชนิดงาน, COUNT(*) AS จำนวน,
       MIN(Created_Date) AS เก่าสุด, MAX(Created_Date) AS ใหม่สุด
FROM Accounting_Sync_Queue
WHERE Status = 'FAILED' AND Retry_Count >= Max_Retries
GROUP BY Action_Type
ORDER BY COUNT(*) DESC;
GO

-- ── 4) รายการเต็ม (ไว้ไล่ทีละใบถ้าจำเป็น) ───────────────────────────────────
PRINT '';
PRINT '--- 4) รายการทั้งหมดที่ตาย ---';
SELECT TOP 100 ID, Action_Type, Entity_Type, Entity_ID,
       Retry_Count, Max_Retries, Created_Date,
       LEFT(CAST(ISNULL(Error_Message, N'') AS NVARCHAR(MAX)), 250) AS Error_Message
FROM Accounting_Sync_Queue
WHERE Status = 'FAILED' AND Retry_Count >= Max_Retries
ORDER BY ID;
GO

-- ============================================================================
-- ลำดับการแก้ที่ถูกต้อง
-- ============================================================================
-- 1. Admin → Accounting Integration → กด "Sync บัญชี" (ดึงผังบัญชีล่าสุด)
-- 2. กด "🩺 ตรวจสุขภาพการเชื่อมต่อ" → ดูหัวข้อ "mapping ชี้รหัสบัญชีที่ NextAcc ไม่มี"
-- 3. รันสคริปต์นี้ข้อ 2 อีกรอบ — รหัสที่ขึ้น ✅ แล้วคือแก้ได้ ที่ยัง ❌ ต้อง:
--       สร้างบัญชีรหัสนั้นใน NextAcc  หรือ  แก้ mapping ให้ชี้รหัสที่มีจริง
-- 4. เมื่อข้อ 2 ไม่เหลือ ❌ แล้ว → กด "Retry ทั้งหมด" ในหน้าคิว
-- 5. กลุ่ม ④ (NextAcc ล่ม) กด Retry ได้เลยไม่ต้องแก้อะไร
-- 6. กลุ่ม ⑥ (ติด guard) แก้อัตโนมัติไม่ได้ — ต้องจัดการเอกสารเองใน NextAcc
-- ============================================================================
