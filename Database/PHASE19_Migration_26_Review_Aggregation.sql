-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 26: รวมรีวิวหลายแพลตฟอร์ม (Review Aggregation)
-- ════════════════════════════════════════════════════════════════════════════
-- ต่อยอดตารางเดิมของ PHASE16 (AI_Online_Reviews / AI_Review_Sources) — ไม่สร้างตารางใหม่แทน
--
-- 1) AI_Online_Reviews  : แพลตฟอร์มต้นทาง (Origin_Platform) + รหัสรีวิวของแพลตฟอร์ม (External_Review_Id)
--                         คะแนนดิบ + สเกล + คะแนนเทียบ /10 และ /5 (คำนวณใน C# — ไม่ให้ AI หาร)
--                         คำตอบของโรงแรมบนแพลตฟอร์ม, วันเข้าพัก, ประเภทห้อง, ประเภทนักเดินทาง,
--                         วิธีนำเข้า (Ingest_Method), Content_Hash (กันซ้ำเมื่อไม่มีรหัส),
--                         Is_Provisional (รีวิวจากอีเมลแจ้งเตือน รอข้อมูลจริง), Is_Excluded (สแปม/ทดสอบ — ไม่นับสถิติ)
--                         ⚠ คะแนนที่ไม่รู้ = NULL เสมอ (ไม่ใช่ 0) — แถวเก่าที่ Rating = 0 จะได้ Rating_5/Rating_10 = NULL
-- 2) AI_Review_Sources  : วิธีดึง, รอบดึง (นาที), เปิดดึงอัตโนมัติ (Is_Enabled — แยกจาก IsEnabled เดิมที่ใช้กับปุ่มในหน้าเก่า),
--                         สถานะรอบล่าสุด, คะแนนรวมที่แพลตฟอร์มประกาศ, lease กันดึงซ้ำข้าม worker
--                         ความลับ (API key / refresh token) ยังอยู่ใน ApiConfig เดิม — ระบบเข้ารหัส (dpapi:) และไม่ส่งไปเบราว์เซอร์
-- 3) Review_Fetch_Log   : บันทึกทุกรอบดึง (OK / PARTIAL / FAILED / SKIPPED)
-- 4) vw_AI_Review_AllSources : เพิ่มคอลัมน์ใหม่ (คอลัมน์เดิมคงชื่อ/ลำดับเดิม หน้าเก่าใช้ต่อได้)
-- 5) แหล่งใหม่ TRIPCOM / AIRBNB, ค่าตั้งใน System_Config, กฎแจ้งเตือน REVIEW_NEGATIVE / REVIEW_FETCH_FAILING
-- 6) Guest_Reviews ที่เกิดจาก "แนบภาพรีวิว Google รับแต้ม" (5 ดาวปลอม) → Status = 'REWARD_CLAIM' (หลุดจากสถิติ)
--
-- idempotent — รันซ้ำได้ (COL_LENGTH / OBJECT_ID / NOT EXISTS guard), ไม่ลบข้อมูล
-- ต้องรัน PHASE16_Migration_01 มาก่อน (ไม่มีตาราง = ข้ามส่วนนั้น)
-- หลังรัน: recycle App Pool (หรือรอ 5 นาทีให้ cache ตรวจ schema หมดอายุ)
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── 1) AI_Online_Reviews: คอลัมน์ใหม่ ───────────────────────────────────────
IF OBJECT_ID('dbo.AI_Online_Reviews', 'U') IS NULL
    PRINT N'⚠ ไม่มีตาราง AI_Online_Reviews (ยังไม่ได้รัน PHASE16_Migration_01) — ข้ามส่วนที่ 1';
ELSE
BEGIN
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Origin_Platform') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Origin_Platform NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'External_Review_Id') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD External_Review_Id NVARCHAR(200) NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Rating_Raw') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Rating_Raw DECIMAL(5,2) NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Rating_Scale') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Rating_Scale DECIMAL(5,2) NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Rating_10') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Rating_10 DECIMAL(5,2) NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Rating_5') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Rating_5 DECIMAL(4,2) NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Review_Url') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Review_Url NVARCHAR(1000) NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Owner_Response') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Owner_Response NVARCHAR(MAX) NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Owner_Response_Date') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Owner_Response_Date DATETIME NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Stay_Date') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Stay_Date DATE NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Room_Type') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Room_Type NVARCHAR(200) NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Traveller_Type') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Traveller_Type NVARCHAR(100) NULL;
    -- Language มีอยู่แล้วตั้งแต่ PHASE16 (NVARCHAR(10) DEFAULT 'TH') — เพิ่มเฉพาะเมื่อไม่มี
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Language') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Language NVARCHAR(10) NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Ingest_Method') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Ingest_Method NVARCHAR(30) NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Content_Hash') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Content_Hash CHAR(64) NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Is_Provisional') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Is_Provisional BIT NOT NULL
            CONSTRAINT DF_AIOnlineReviews_Is_Provisional DEFAULT 0;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Is_Excluded') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Is_Excluded BIT NOT NULL
            CONSTRAINT DF_AIOnlineReviews_Is_Excluded DEFAULT 0;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Fetched_At') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Fetched_At DATETIME NULL;
    IF COL_LENGTH('dbo.AI_Online_Reviews', 'Updated_At') IS NULL
        ALTER TABLE dbo.AI_Online_Reviews ADD Updated_At DATETIME NULL;
    PRINT N'AI_Online_Reviews: ตรวจ/เพิ่มคอลัมน์ครบแล้ว';
END
GO

-- ── 1b) Backfill แถวเดิม (Ingest_Method = 'LEGACY') ──────────────────────────
--   Rating เดิม = คะแนนที่ AI/โค้ดเก่าแปลงเป็น 1-5 แล้ว (สเกลจริงหายไป) ⇒ Raw = Rating, Scale = 5
--   Rating = 0 / NULL = "ไม่รู้คะแนน" ⇒ คอลัมน์ใหม่เป็น NULL ทั้งหมด (คอลัมน์ Rating เดิมไม่แตะ)
--   Content_Hash ของแถวเดิมระบบคำนวณเองทีละชุดตอนรอบดึง (ต้องใช้สูตร normalise เดียวกับ C#)
IF OBJECT_ID('dbo.AI_Online_Reviews', 'U') IS NOT NULL
BEGIN
    UPDATE dbo.AI_Online_Reviews
       SET Origin_Platform = CASE SourceCode
                                 WHEN 'GOOGLE'      THEN N'Google'
                                 WHEN 'FACEBOOK'    THEN N'Facebook'
                                 WHEN 'AGODA'       THEN N'Agoda'
                                 WHEN 'BOOKING'     THEN N'Booking.com'
                                 WHEN 'TRIPADVISOR' THEN N'Tripadvisor'
                                 WHEN 'EXPEDIA'     THEN N'Expedia'
                                 WHEN 'TRAVELOKA'   THEN N'Traveloka'
                                 WHEN 'TRIPCOM'     THEN N'Trip.com'
                                 WHEN 'AIRBNB'      THEN N'Airbnb'
                                 WHEN 'PANTIP'      THEN N'Pantip'
                                 WHEN 'WONGNAI'     THEN N'Wongnai'
                                 WHEN 'TIKTOK'      THEN N'TikTok'
                                 WHEN 'LEMON8'      THEN N'Lemon8'
                                 WHEN 'TWITTER'     THEN N'X (Twitter)'
                                 WHEN 'INSTAGRAM'   THEN N'Instagram'
                                 WHEN 'YOUTUBE'     THEN N'YouTube'
                                 ELSE SourceCode END,
           Rating_Raw   = CASE WHEN Rating > 0 AND Rating <= 5 THEN CAST(ROUND(Rating, 2) AS DECIMAL(5,2)) END,
           Rating_Scale = CASE WHEN Rating > 0 AND Rating <= 5 THEN 5 END,
           Rating_5     = CASE WHEN Rating > 0 AND Rating <= 5 THEN CAST(ROUND(Rating, 2) AS DECIMAL(4,2)) END,
           Rating_10    = CASE WHEN Rating > 0 AND Rating <= 5 THEN CAST(ROUND(Rating * 2, 2) AS DECIMAL(5,2)) END,
           Owner_Response = CASE WHEN ResponseStatus = 'RESPONDED' THEN ActualResponse END,
           Owner_Response_Date = CASE WHEN ResponseStatus = 'RESPONDED' THEN RespondedDate END,
           -- ค่าเริ่มต้น 'TH' ของ PHASE16 ไม่เคยถูกตั้งจริง (ใส่ให้ทุกแถว) ⇒ ล้างเป็น NULL = ไม่รู้ภาษา
           Language     = CASE WHEN Language = 'TH' THEN NULL ELSE Language END,
           Ingest_Method = 'LEGACY',
           Fetched_At   = ISNULL(Fetched_At, Created_Date)
     WHERE Ingest_Method IS NULL;
    PRINT N'Backfill แถวเดิม (LEGACY): ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว';
END
GO

-- ── 1c) Index ───────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.AI_Online_Reviews', 'U') IS NOT NULL
BEGIN
    -- แถวเก่าอาจมีรหัสซ้ำในแพลตฟอร์มเดียวกัน (กันซ้ำแบบเดิมไม่ scope ตาม Source) — backfill ไม่ได้คัด External_Review_Id
    -- จาก PlatformReviewId (รหัสเก่าทำจาก GetHashCode ไม่คงที่) จึงไม่มีทางชน index นี้ตอนสร้าง
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AI_Online_Reviews')
                                              AND name = 'UX_AIOnlineReviews_Origin_ExternalId')
        CREATE UNIQUE INDEX UX_AIOnlineReviews_Origin_ExternalId
            ON dbo.AI_Online_Reviews (Origin_Platform, External_Review_Id)
            WHERE External_Review_Id IS NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AI_Online_Reviews')
                                              AND name = 'IX_AIOnlineReviews_Source_Date')
        CREATE INDEX IX_AIOnlineReviews_Source_Date
            ON dbo.AI_Online_Reviews (SourceCode, ReviewDate DESC)
            INCLUDE (Origin_Platform, Rating_5, Rating_10, Is_Excluded, Is_Provisional);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AI_Online_Reviews')
                                              AND name = 'IX_AIOnlineReviews_Origin_Date')
        CREATE INDEX IX_AIOnlineReviews_Origin_Date
            ON dbo.AI_Online_Reviews (Origin_Platform, ReviewDate DESC);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AI_Online_Reviews')
                                              AND name = 'IX_AIOnlineReviews_ContentHash')
        CREATE INDEX IX_AIOnlineReviews_ContentHash
            ON dbo.AI_Online_Reviews (Content_Hash)
            WHERE Content_Hash IS NOT NULL;
    PRINT N'AI_Online_Reviews: ตรวจ/สร้าง index แล้ว';
END
GO

-- ── 2) AI_Review_Sources: คอลัมน์ใหม่ ───────────────────────────────────────
IF OBJECT_ID('dbo.AI_Review_Sources', 'U') IS NULL
    PRINT N'⚠ ไม่มีตาราง AI_Review_Sources — ข้ามส่วนที่ 2';
ELSE
BEGIN
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Ingest_Method') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Ingest_Method NVARCHAR(30) NULL;
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Fetch_Interval_Min') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Fetch_Interval_Min INT NOT NULL
            CONSTRAINT DF_AIReviewSources_Fetch_Interval_Min DEFAULT 1440;
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Is_Enabled') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Is_Enabled BIT NOT NULL
            CONSTRAINT DF_AIReviewSources_Is_Enabled DEFAULT 0;
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Source_Url') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Source_Url NVARCHAR(1000) NULL;
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Next_Fetch_At') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Next_Fetch_At DATETIME NULL;
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Last_Success_At') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Last_Success_At DATETIME NULL;
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Last_Error') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Last_Error NVARCHAR(1000) NULL;
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Consecutive_Failures') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Consecutive_Failures INT NOT NULL
            CONSTRAINT DF_AIReviewSources_Consecutive_Failures DEFAULT 0;
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Platform_Rating') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Platform_Rating DECIMAL(5,2) NULL;
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Platform_Rating_Scale') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Platform_Rating_Scale DECIMAL(5,2) NULL;
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Platform_Review_Count') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Platform_Review_Count INT NULL;
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Lease_Until') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Lease_Until DATETIME NULL;
    IF COL_LENGTH('dbo.AI_Review_Sources', 'Lease_Owner') IS NULL
        ALTER TABLE dbo.AI_Review_Sources ADD Lease_Owner NVARCHAR(100) NULL;
    PRINT N'AI_Review_Sources: ตรวจ/เพิ่มคอลัมน์ครบแล้ว';
END
GO

-- ── 2b) แหล่งใหม่ + วิธีดึงตั้งต้น (ไม่เปิดดึงอัตโนมัติให้ — ผู้ดูแลเปิดเองหลังตั้งค่า) ──────
IF OBJECT_ID('dbo.AI_Review_Sources', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.AI_Review_Sources WHERE SourceCode = 'TRIPCOM')
        INSERT INTO dbo.AI_Review_Sources (SourceCode, SourceName, IconClass, BrandColor, IsEnabled)
        VALUES ('TRIPCOM', N'Trip.com', 'fas fa-plane-departure', '#287DFA', 0);
    IF NOT EXISTS (SELECT 1 FROM dbo.AI_Review_Sources WHERE SourceCode = 'AIRBNB')
        INSERT INTO dbo.AI_Review_Sources (SourceCode, SourceName, IconClass, BrandColor, IsEnabled)
        VALUES ('AIRBNB', N'Airbnb', 'fab fa-airbnb', '#FF5A5F', 0);

    -- วิธีดึงที่แนะนำต่อแพลตฟอร์ม (ตั้งเฉพาะแถวที่ยังไม่เคยตั้ง)
    UPDATE dbo.AI_Review_Sources
       SET Ingest_Method = CASE SourceCode
                               WHEN 'GOOGLE'      THEN 'PLACES_API'      -- เปลี่ยนเป็น GBP_API เมื่อ Google อนุมัติ
                               WHEN 'TRIPADVISOR' THEN 'TRIPADVISOR_API'
                               WHEN 'BOOKING'     THEN 'EMAIL'
                               WHEN 'AGODA'       THEN 'EMAIL'
                               WHEN 'EXPEDIA'     THEN 'EMAIL'
                               WHEN 'TRIPCOM'     THEN 'EMAIL'
                               WHEN 'TRAVELOKA'   THEN 'CSV'
                               WHEN 'FACEBOOK'    THEN 'MANUAL'          -- Graph API เลิกให้รีวิวเพจตั้งแต่ v22
                               WHEN 'AIRBNB'      THEN 'MANUAL'
                               WHEN 'WONGNAI'     THEN 'MANUAL'
                               WHEN 'INTERNAL'    THEN NULL
                               ELSE 'AI_SEARCH' END
     WHERE Ingest_Method IS NULL AND SourceCode <> 'INTERNAL';

    UPDATE dbo.AI_Review_Sources
       SET Platform_Rating_Scale = CASE WHEN SourceCode IN ('BOOKING','AGODA','EXPEDIA','TRAVELOKA') THEN 10 ELSE 5 END
     WHERE Platform_Rating_Scale IS NULL AND SourceCode <> 'INTERNAL';

    -- รอบดึงเริ่มต้นตามวิธี: อีเมล 60 นาที, Google API 180 นาที, อื่น ๆ วันละครั้ง (ค่าเริ่ม 1440)
    UPDATE dbo.AI_Review_Sources SET Fetch_Interval_Min = 60
     WHERE Ingest_Method = 'EMAIL' AND Fetch_Interval_Min = 1440 AND Last_Success_At IS NULL;
    UPDATE dbo.AI_Review_Sources SET Fetch_Interval_Min = 180
     WHERE Ingest_Method = 'GBP_API' AND Fetch_Interval_Min = 1440 AND Last_Success_At IS NULL;
    PRINT N'AI_Review_Sources: เพิ่มแหล่ง TRIPCOM/AIRBNB + ตั้งวิธีดึงตั้งต้นแล้ว';
END
GO

-- ── 3) Review_Fetch_Log ─────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Review_Fetch_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Review_Fetch_Log (
        ID          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Review_Fetch_Log PRIMARY KEY,
        Source_ID   INT            NULL,
        Started_At  DATETIME       NULL,
        Finished_At DATETIME       NULL,
        Status      NVARCHAR(20)   NULL,     -- OK | PARTIAL | FAILED | SKIPPED
        Fetched     INT            NULL,
        Inserted    INT            NULL,
        Updated     INT            NULL,
        Message     NVARCHAR(2000) NULL
    );
    CREATE INDEX IX_Review_Fetch_Log_Source ON dbo.Review_Fetch_Log (Source_ID, Started_At DESC);
    PRINT N'สร้างตาราง Review_Fetch_Log';
END
ELSE PRINT N'มี Review_Fetch_Log อยู่แล้ว — ข้าม';
GO

-- ── 4) View รวมทุกแหล่ง (คอลัมน์เดิมคงชื่อ/ลำดับ, คอลัมน์ใหม่ต่อท้าย) ────────────
IF OBJECT_ID('dbo.AI_Online_Reviews', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.Guest_Reviews', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Guest_Reviews', 'Sentiment') IS NOT NULL
   AND COL_LENGTH('dbo.AI_Online_Reviews', 'Origin_Platform') IS NOT NULL
BEGIN
    IF OBJECT_ID('dbo.vw_AI_Review_AllSources', 'V') IS NOT NULL
        DROP VIEW dbo.vw_AI_Review_AllSources;

    -- N'...' : ข้อความไทย (ข้อดี/ข้อเสีย) ในนิยาม view ต้องเป็น Unicode — literal ธรรมดาจะกลายเป็น ??? บน collation ที่ไม่ใช่ Thai
    EXEC(N'CREATE VIEW dbo.vw_AI_Review_AllSources AS
    SELECT
        ''INTERNAL'' AS SourceCode,
        gr.ID,
        c.Name AS ReviewerName,
        NULL AS ReviewerAvatar,
        CAST(gr.OverallRating AS FLOAT) AS Rating,
        gr.ReviewTitle,
        ISNULL(gr.ReviewText, '''') + CASE WHEN gr.Pros IS NOT NULL THEN N'' ข้อดี: '' + gr.Pros ELSE '''' END
                                    + CASE WHEN gr.Cons IS NOT NULL THEN N'' ข้อเสีย: '' + gr.Cons ELSE '''' END AS ReviewText,
        gr.SubmittedDate AS ReviewDate,
        gr.Sentiment,
        gr.SentimentScore,
        gr.AITopics AS Topics,
        gr.AISummary,
        gr.AISuggestedResponse AS SuggestedResponse,
        gr.Status AS ResponseStatus,
        gr.ResponseText AS ActualResponse,
        CASE WHEN gr.IsAIAnalyzed = 1 THEN 1 ELSE 0 END AS IsAnalyzed,
        ISNULL(gr.IsFlagged, 0) AS IsFlagged,
        gr.FlagReason,
        -- ── คอลัมน์ใหม่ (PHASE19_26) ──
        ''INTERNAL'' AS SourceType,
        N''INTERNAL'' AS Origin_Platform,
        CAST(NULL AS NVARCHAR(200)) AS External_Review_Id,
        CAST(gr.OverallRating AS DECIMAL(5,2)) AS Rating_Raw,
        CAST(5 AS DECIMAL(5,2)) AS Rating_Scale,
        CAST(gr.OverallRating * 2 AS DECIMAL(5,2)) AS Rating_10,
        CAST(gr.OverallRating AS DECIMAL(4,2)) AS Rating_5,
        CAST(NULL AS NVARCHAR(1000)) AS Review_Url,
        gr.ResponseText AS Owner_Response,
        gr.ResponseDate AS Owner_Response_Date,
        CAST(NULL AS DATE) AS Stay_Date,
        CAST(NULL AS NVARCHAR(200)) AS Room_Type,
        CAST(gr.TravelType AS NVARCHAR(100)) AS Traveller_Type,
        CAST(NULL AS NVARCHAR(10)) AS Language,
        CAST(''INTERNAL'' AS NVARCHAR(30)) AS Ingest_Method,
        CAST(0 AS BIT) AS Is_Provisional,
        CAST(0 AS BIT) AS Is_Excluded,
        gr.SubmittedDate AS Fetched_At,
        gr.ResponseDate AS Updated_At,
        gr.Status AS Moderation_Status,
        CASE WHEN gr.ResponseText IS NOT NULL AND LTRIM(RTRIM(gr.ResponseText)) <> '''' THEN ''RESPONDED'' ELSE ''PENDING'' END AS Reply_Status,
        gr.AITopicScores AS TopicScores,
        gr.SubmittedDate AS Created_Date
    FROM dbo.Guest_Reviews gr
    LEFT JOIN dbo.Customer c ON c.MobilePhone = gr.Customer_MobilePhone
    WHERE gr.Status IN (''APPROVED'', ''PENDING'')

    UNION ALL

    SELECT
        aor.SourceCode,
        aor.ID,
        aor.ReviewerName,
        aor.ReviewerAvatar,
        aor.Rating,
        aor.ReviewTitle,
        aor.ReviewText,
        aor.ReviewDate,
        aor.Sentiment,
        aor.SentimentScore,
        aor.Topics,
        aor.AISummary,
        aor.SuggestedResponse,
        aor.ResponseStatus,
        aor.ActualResponse,
        CAST(aor.IsAnalyzed AS INT) AS IsAnalyzed,
        ISNULL(aor.IsFlagged, 0) AS IsFlagged,
        aor.FlagReason,
        ''ONLINE'' AS SourceType,
        aor.Origin_Platform,
        aor.External_Review_Id,
        aor.Rating_Raw,
        aor.Rating_Scale,
        aor.Rating_10,
        aor.Rating_5,
        aor.Review_Url,
        aor.Owner_Response,
        aor.Owner_Response_Date,
        aor.Stay_Date,
        aor.Room_Type,
        aor.Traveller_Type,
        aor.Language,
        aor.Ingest_Method,
        aor.Is_Provisional,
        aor.Is_Excluded,
        aor.Fetched_At,
        aor.Updated_At,
        CAST(NULL AS VARCHAR(20)) AS Moderation_Status,
        CASE WHEN (aor.Owner_Response IS NOT NULL AND LTRIM(RTRIM(aor.Owner_Response)) <> '''')
                  OR aor.ResponseStatus = ''RESPONDED'' THEN ''RESPONDED'' ELSE ''PENDING'' END AS Reply_Status,
        aor.TopicScores,
        aor.Created_Date
    FROM dbo.AI_Online_Reviews aor');
    PRINT N'สร้าง vw_AI_Review_AllSources ใหม่ (เพิ่มคอลัมน์ PHASE19_26)';
END
ELSE PRINT N'⚠ ข้ามการสร้าง view ใหม่ (ตาราง/คอลัมน์ที่ต้องใช้ยังไม่ครบ)';
GO

-- ── 5) ค่าตั้งใน System_Config ───────────────────────────────────────────────
IF OBJECT_ID('dbo.System_Config', 'U') IS NOT NULL
BEGIN
    DECLARE @cfg TABLE (K NVARCHAR(100), V NVARCHAR(400), Cat NVARCHAR(60),
                        DN NVARCHAR(200), D NVARCHAR(1000), IT NVARCHAR(20), Ord INT);

    INSERT INTO @cfg (K, V, Cat, DN, D, IT, Ord) VALUES
    ('Review_Aggregation_Enabled', '0', N'รีวิวออนไลน์', N'เปิดดึงรีวิวอัตโนมัติ',
     N'สวิตช์ใหญ่ของงานเบื้องหลังดึงรีวิว (ต้องเปิด "ดึงอัตโนมัติ" รายแหล่งที่หน้า แหล่งรีวิว ด้วย)', 'bool', 10),
    ('Review_AI_AutoAnalyze', '1', N'รีวิวออนไลน์', N'ให้ AI วิเคราะห์รีวิวใหม่อัตโนมัติ',
     N'วิเคราะห์อารมณ์/หัวข้อ/ร่างคำตอบ ทีละชุดหลังดึงรีวิว (ต้องเปิด AI_Enabled ด้วย) — ไม่แก้คะแนน', 'bool', 20),
    ('Review_AI_Daily_Token_Budget', '50000', N'รีวิวออนไลน์', N'งบ token AI ต่อวัน (รีวิว)',
     N'รวม token ที่ใช้วิเคราะห์รีวิว/ดึงจาก URL ต่อวัน เกินแล้วหยุดจนถึงพรุ่งนี้ (0 = ไม่ใช้ AI)', 'number', 30),
    ('Review_Negative_Threshold', '2', N'รีวิวออนไลน์', N'แจ้งเตือนรีวิวคะแนนต่ำ (≤ ดาว /5)',
     N'รีวิวใหม่ที่ Rating_5 ไม่เกินค่านี้ส่งแจ้งเตือน REVIEW_NEGATIVE (เช่น Booking 4/10 = 2/5)', 'number', 40),
    ('Review_Alert_MaxAgeDays', '14', N'รีวิวออนไลน์', N'แจ้งเตือนเฉพาะรีวิวอายุไม่เกิน (วัน)',
     N'กันแจ้งเตือนท่วมตอนนำเข้ารีวิวย้อนหลัง', 'number', 50),
    ('Review_FetchFail_Alert_After', '3', N'รีวิวออนไลน์', N'แจ้งเตือนเมื่อดึงล้มเหลวติดกัน (ครั้ง)',
     N'ส่ง REVIEW_FETCH_FAILING เมื่อแหล่งใดล้มเหลวติดกันครบจำนวนนี้', 'number', 60),
    ('Review_UrlFetch_Allowed', '0', N'รีวิวออนไลน์', N'อนุญาตดึงจากหน้าเว็บ OTA อัตโนมัติ (URL_FETCH)',
     N'⚠ ขัดข้อกำหนดการใช้งานของ Booking/Agoda/Tripadvisor/Google — เปิดเฉพาะเมื่อเจ้าของยอมรับความเสี่ยง', 'bool', 70);

    INSERT INTO dbo.System_Config (ConfigKey, ConfigValue, Category, DisplayName, [Description], IsSecret, InputType, DisplayOrder, ModifiedDate)
    SELECT c.K, c.V, c.Cat, c.DN, c.D, 0, c.IT, c.Ord, GETDATE()
    FROM @cfg c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.System_Config s WHERE s.ConfigKey = c.K);

    PRINT N'เพิ่มค่าตั้งรีวิวออนไลน์: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' คีย์';
END
GO

-- ── 5b) กฎแจ้งเตือน (Telegram เปิด, LINE ปิด) ────────────────────────────────
IF OBJECT_ID('dbo.Notification_Rules', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.Notification_Rules (Event_Code, Channel, Enabled, Modified_Date)
    SELECT s.Code, s.Ch, s.En, GETDATE()
    FROM (VALUES ('REVIEW_NEGATIVE', 'TELEGRAM', 1), ('REVIEW_NEGATIVE', 'LINE', 0),
                 ('REVIEW_FETCH_FAILING', 'TELEGRAM', 1), ('REVIEW_FETCH_FAILING', 'LINE', 0)) s(Code, Ch, En)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Notification_Rules r WHERE r.Event_Code = s.Code AND r.Channel = s.Ch);
    PRINT N'เพิ่มกฎแจ้งเตือนรีวิว: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว';
END
GO

-- ── 6) Guest_Reviews จาก "แนบภาพรีวิว Google รับแต้ม" ไม่ใช่รีวิวจริง ──────────────
--   เดิม Guest/Review.aspx ใส่ OverallRating = 5 + ReviewText = path ไฟล์ภาพ ⇒ ปั่นคะแนนเฉลี่ย INTERNAL
--   ตอนนี้โค้ดบันทึกเป็น Status = 'REWARD_CLAIM' (view กรองเฉพาะ APPROVED/PENDING) — ย้อนแก้แถวเก่า
IF OBJECT_ID('dbo.Guest_Reviews', 'U') IS NOT NULL
BEGIN
    UPDATE dbo.Guest_Reviews
       SET Status = 'REWARD_CLAIM'
     WHERE Source = 'GOOGLE'
       AND ReviewTitle = 'Google Review'
       AND ReviewText LIKE '~/Uploads/ReviewScreenshots/%'
       AND Status IN ('PENDING', 'APPROVED');
    PRINT N'Guest_Reviews แถวรับแต้ม → REWARD_CLAIM: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว';
END
GO

-- 6b) sp_SubmitGuestReview ปฏิเสธ "รีวิวซ้ำ" ถ้ามีแถวใดก็ได้ของใบจองนั้น ⇒ แถว REWARD_CLAIM จะบล็อกรีวิวจริง
--     แก้เฉพาะเงื่อนไขนั้นในนิยามเดิม (ไม่เขียน procedure ใหม่ทั้งตัว) — ข้ามถ้าหน้าตาไม่ตรงหรือแก้ไปแล้ว
IF OBJECT_ID('dbo.sp_SubmitGuestReview', 'P') IS NOT NULL
BEGIN
    DECLARE @def NVARCHAR(MAX) = OBJECT_DEFINITION(OBJECT_ID('dbo.sp_SubmitGuestReview'));
    DECLARE @find NVARCHAR(200) = N'WHERE [Reservation_ID] = @ReservationID)';
    DECLARE @pos INT = CHARINDEX(N'CREATE PROCEDURE', @def);
    IF @def IS NOT NULL AND CHARINDEX(N'REWARD_CLAIM', @def) = 0 AND CHARINDEX(@find, @def) > 0 AND @pos > 0
    BEGIN
        SET @def = REPLACE(@def, @find, N'WHERE [Reservation_ID] = @ReservationID AND ISNULL([Status], '''') <> ''REWARD_CLAIM'')');
        SET @def = STUFF(@def, @pos, LEN(N'CREATE PROCEDURE'), N'ALTER PROCEDURE');
        EXEC (@def);
        PRINT N'sp_SubmitGuestReview: ไม่นับแถว REWARD_CLAIM เป็นรีวิวซ้ำแล้ว';
    END
    ELSE PRINT N'sp_SubmitGuestReview: ข้าม (แก้แล้ว หรือหน้าตาไม่ตรงที่คาด)';
END
GO

-- ── ตรวจผล ──────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.AI_Review_Sources', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.AI_Review_Sources', 'Ingest_Method') IS NOT NULL
    EXEC('SELECT SourceCode, SourceName, Ingest_Method, Fetch_Interval_Min, Is_Enabled, Platform_Rating_Scale
            FROM dbo.AI_Review_Sources ORDER BY SourceCode');
GO
