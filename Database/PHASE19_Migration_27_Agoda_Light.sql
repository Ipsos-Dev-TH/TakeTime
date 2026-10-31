-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 27: Agoda แบบเบา (Ingest_Method = 'AGODA_LIGHT')
-- ════════════════════════════════════════════════════════════════════════════
-- นโยบายเจ้าของ: Agoda ไม่ crawl — วันละครั้งอ่านเฉพาะสิ่งที่หน้าที่พักสาธารณะแสดง (AgodaLightAdapter)
--   • ตั้ง Source_Url ของแหล่ง AGODA = หน้าที่พักของเรา และวิธีดึง = AGODA_LIGHT
--     เฉพาะเมื่อวิธีเดิมยังว่าง (NULL/'') หรือเป็น 'URL_FETCH' (ไม่ทับวิธีที่ผู้ดูแลเลือกไว้ เช่น EMAIL / CSV)
--   • รอบดึง 1440 นาที (โค้ดบังคับขั้นต่ำ 720 สำหรับวิธีนี้)
--   • ไม่แตะ Is_Enabled (ค่าเริ่ม 0 = ปิด) — Owner/Admin เปิดเองที่หน้า แหล่งรีวิว หลังกด "ทดสอบดึง (ไม่บันทึก)"
--
-- หมายเหตุ: PHASE19_26 ตั้งวิธีตั้งต้นของ AGODA เป็น 'EMAIL' ⇒ ฐานที่รัน 26 แล้วจะ "ไม่" ถูกเปลี่ยนโดยค่าเริ่ม
--   ถ้าต้องการเปลี่ยนแถว EMAIL ที่ยังไม่เคยใช้งานเลย (ปิดอยู่ ไม่เคยดึงสำเร็จ ไม่มี log) ให้ตั้ง @ConvertUnusedEmail = 1
--   หรือเลือกวิธี "Agoda: อ่านหน้าที่พักวันละครั้ง (light)" เองที่หน้า แหล่งรีวิว
--
--   • เพิ่มคอลัมน์ AI_Online_Reviews.Is_Machine_Translated (รีวิวที่ได้มาแต่คำแปลของแพลตฟอร์ม) — รันซ้ำได้ ถ้าเคยรันไฟล์นี้แล้วให้รันอีกครั้ง
--
-- ต้องรัน PHASE19_Migration_26_Review_Aggregation.sql มาก่อน · idempotent — รันซ้ำได้ ไม่ลบข้อมูล
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @ConvertUnusedEmail BIT = 0;   -- 1 = เปลี่ยนแถว AGODA ที่เป็น EMAIL แต่ยังไม่เคยใช้งานเลย
DECLARE @Url NVARCHAR(1000) = N'https://www.agoda.com/th-th/take-time-mirror-room/hotel/chonburi-th.html';

IF OBJECT_ID('dbo.AI_Review_Sources', 'U') IS NULL
   OR COL_LENGTH('dbo.AI_Review_Sources', 'Ingest_Method') IS NULL
   OR COL_LENGTH('dbo.AI_Review_Sources', 'Source_Url') IS NULL
BEGIN
    PRINT N'⚠ ยังไม่ได้รัน PHASE19_Migration_26_Review_Aggregation.sql — ข้าม';
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.AI_Review_Sources WHERE SourceCode = 'AGODA')
    BEGIN
        INSERT INTO dbo.AI_Review_Sources (SourceCode, SourceName, IconClass, BrandColor, IsEnabled)
        VALUES ('AGODA', N'Agoda', 'fas fa-hotel', '#5C2D91', 0);
        PRINT N'เพิ่มแหล่ง AGODA (ปิดอยู่)';
    END

    UPDATE s
       SET Ingest_Method        = 'AGODA_LIGHT',
           Source_Url           = CASE WHEN ISNULL(LTRIM(RTRIM(s.Source_Url)), N'') = N''
                                         OR s.Source_Url NOT LIKE N'https://www.agoda.com/%'
                                       THEN @Url ELSE s.Source_Url END,
           Fetch_Interval_Min   = 1440,
           Consecutive_Failures = 0,
           Next_Fetch_At        = NULL,
           Last_Error           = NULL,
           Platform_Rating_Scale = COALESCE(s.Platform_Rating_Scale, 10),
           Updated_Date         = GETDATE()
      FROM dbo.AI_Review_Sources s
     WHERE s.SourceCode = 'AGODA'
       AND (   ISNULL(s.Ingest_Method, '') IN ('', 'URL_FETCH')
            OR (@ConvertUnusedEmail = 1 AND s.Ingest_Method = 'EMAIL' AND s.Is_Enabled = 0
                AND s.Last_Success_At IS NULL
                AND (OBJECT_ID('dbo.Review_Fetch_Log', 'U') IS NULL
                     OR NOT EXISTS (SELECT 1 FROM dbo.Review_Fetch_Log l WHERE l.Source_ID = s.ID))));
    PRINT N'AGODA → AGODA_LIGHT: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว (Is_Enabled ไม่เปลี่ยน)';

    -- แถวที่เป็น AGODA_LIGHT อยู่แล้วแต่รอบสั้นกว่าขั้นต่ำ → 1440 (โค้ดก็บังคับ 720 อยู่แล้ว — ทำให้หน้าแอดมินแสดงตรง)
    UPDATE dbo.AI_Review_Sources
       SET Fetch_Interval_Min = 1440, Updated_Date = GETDATE()
     WHERE Ingest_Method = 'AGODA_LIGHT' AND Fetch_Interval_Min < 720;
END
GO

-- ── รีวิวที่แพลตฟอร์มให้มาแต่คำแปลเครื่อง (ไม่มีต้นฉบับ) — ธงแยก ไม่เขียนหมายเหตุลงในข้อความ ──────────
--   NULL = ไม่ทราบ (แถวเก่า/แหล่งอื่น), 0 = ต้นฉบับ, 1 = คำแปลของแพลตฟอร์ม (เช่น Agoda)
--   ภาษาเก็บใน Language เดิม; ได้ต้นฉบับภายหลัง (External_Review_Id เดิม) ระบบแทนข้อความ/ภาษาให้ครั้งเดียว
IF OBJECT_ID('dbo.AI_Online_Reviews', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.AI_Online_Reviews', 'Is_Machine_Translated') IS NULL
BEGIN
    ALTER TABLE dbo.AI_Online_Reviews ADD Is_Machine_Translated BIT NULL;
    PRINT N'AI_Online_Reviews: เพิ่ม Is_Machine_Translated';
END
GO

-- ── ตรวจผล ──────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.AI_Review_Sources', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.AI_Review_Sources', 'Ingest_Method') IS NOT NULL
    EXEC('SELECT SourceCode, Ingest_Method, Source_Url, Fetch_Interval_Min, Is_Enabled, Last_Success_At
            FROM dbo.AI_Review_Sources WHERE SourceCode = ''AGODA''');
GO
