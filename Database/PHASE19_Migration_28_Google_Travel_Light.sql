-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 28: Google Travel แบบเบา (Ingest_Method = 'GOOGLE_TRAVEL_LIGHT') + Platform_Meta
-- ════════════════════════════════════════════════════════════════════════════
-- 1) AI_Online_Reviews.Platform_Meta NVARCHAR(MAX) — ข้อมูลเสริมจากแพลตฟอร์มเป็น JSON
--    (คะแนนย่อย ห้องพัก/บริการ/สถานที่ตั้ง, ไฮไลต์, หัวข้อย่อย, วันที่โดยประมาณ "4 เดือนที่แล้ว", วันที่คำตอบเจ้าของแบบสัมพัทธ์)
--    แยกจาก TopicScores เพราะ TopicScores เป็นผล AI (ReviewAiAnalyzer เขียนทับ)
-- 2) แหล่ง GOOGLE: Source_Url = หน้ารีวิว Google Travel ของเรา "เฉพาะเมื่อว่าง"
--    วิธีดึง → GOOGLE_TRAVEL_LIGHT เฉพาะเมื่อ ว่าง / URL_FETCH / AI_SEARCH หรือ PLACES_API ที่ยังไม่เคยใช้งาน (ปิดอยู่ ไม่เคยสำเร็จ)
--    ❌ ไม่แตะเมื่อเป็น GBP_API (ทางที่ถูกข้อกำหนดและแนะนำ) · ไม่แตะ Is_Enabled (ค่าเริ่ม 0 — Owner/Admin เปิดเองหลังกด "ทดสอบดึง")
--
-- ต้องรัน PHASE19_Migration_26 มาก่อน · idempotent — รันซ้ำได้ ไม่ลบข้อมูล
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.AI_Online_Reviews', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.AI_Online_Reviews', 'Platform_Meta') IS NULL
BEGIN
    ALTER TABLE dbo.AI_Online_Reviews ADD Platform_Meta NVARCHAR(MAX) NULL;
    PRINT N'AI_Online_Reviews: เพิ่ม Platform_Meta';
END
GO

DECLARE @Url NVARCHAR(1000) = N'https://www.google.com/travel/hotels/entity/CgsIsoLIgrCxpI6cARAB/reviews?hl=th-TH&gl=th';

IF OBJECT_ID('dbo.AI_Review_Sources', 'U') IS NULL
   OR COL_LENGTH('dbo.AI_Review_Sources', 'Ingest_Method') IS NULL
   OR COL_LENGTH('dbo.AI_Review_Sources', 'Source_Url') IS NULL
BEGIN
    PRINT N'⚠ ยังไม่ได้รัน PHASE19_Migration_26_Review_Aggregation.sql — ข้ามส่วนแหล่ง GOOGLE';
END
ELSE
BEGIN
    UPDATE dbo.AI_Review_Sources
       SET Source_Url = @Url, Updated_Date = GETDATE()
     WHERE SourceCode = 'GOOGLE' AND ISNULL(LTRIM(RTRIM(Source_Url)), N'') = N'';
    PRINT N'GOOGLE Source_Url (เฉพาะที่ว่าง): ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว';

    UPDATE s
       SET Ingest_Method        = 'GOOGLE_TRAVEL_LIGHT',
           Fetch_Interval_Min   = 1440,
           Consecutive_Failures = 0,
           Next_Fetch_At        = NULL,
           Last_Error           = NULL,
           Updated_Date         = GETDATE()
      FROM dbo.AI_Review_Sources s
     WHERE s.SourceCode = 'GOOGLE'
       AND ISNULL(s.Ingest_Method, '') <> 'GBP_API'
       AND (   ISNULL(s.Ingest_Method, '') IN ('', 'URL_FETCH', 'AI_SEARCH')
            OR (s.Ingest_Method = 'PLACES_API' AND s.Is_Enabled = 0 AND s.Last_Success_At IS NULL));
    PRINT N'GOOGLE → GOOGLE_TRAVEL_LIGHT: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว (Is_Enabled ไม่เปลี่ยน)';

    UPDATE dbo.AI_Review_Sources
       SET Fetch_Interval_Min = 1440, Updated_Date = GETDATE()
     WHERE Ingest_Method = 'GOOGLE_TRAVEL_LIGHT' AND Fetch_Interval_Min < 720;
END
GO

-- ── ตรวจผล ──────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.AI_Review_Sources', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.AI_Review_Sources', 'Ingest_Method') IS NOT NULL
    EXEC('SELECT SourceCode, Ingest_Method, Source_Url, Fetch_Interval_Min, Is_Enabled, Last_Success_At
            FROM dbo.AI_Review_Sources WHERE SourceCode = ''GOOGLE''');
GO
