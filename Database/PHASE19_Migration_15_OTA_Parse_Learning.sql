-- ============================================================================
-- PHASE19 Migration 15 — ระบบเรียนรู้เทมเพลตอีเมลจอง OTA
--
-- ปัญหา: OTA เปลี่ยนเทมเพลตอีเมลได้ตลอดโดยไม่บอกใคร regex ที่ผูกกับเทมเพลตเดิม
-- ก็อ่านไม่ออกหรืออ่านผิด แล้วระบบเดินต่อเงียบ ๆ ⇒ ต้องไล่แก้โค้ดทุกครั้ง
--
-- แนวทางใหม่: อ่านค่าด้วยหลายวิธีแล้วให้คะแนน + จำว่า "เทมเพลตแบบนี้ วิธีไหนถูก"
-- ครั้งต่อไปเจอเทมเพลตเดิมจะมั่นใจขึ้นเอง · เทมเพลตใหม่ที่ยังไม่รู้จักจะถาม AI ช่วย
-- · คะแนนต่ำจริง ๆ ถึงค่อยแจ้งคน
--
-- ตารางที่เพิ่ม (ไม่แตะตารางเดิม ไม่แตะข้อมูลเดิม):
--   OTA_Email_Parse_Learning  — วิธีไหนถูก/ผิด กับเทมเพลตไหน ฟิลด์ไหน
--   OTA_Email_Template_Seen   — เจอเทมเพลตอะไรบ้าง ครั้งแรกเมื่อไหร่ คะแนนล่าสุดเท่าไหร่
--
-- รันซ้ำได้
-- ============================================================================

SET NOCOUNT ON;

-- ── 1) บทเรียนรายวิธี ───────────────────────────────────────────────────────
IF OBJECT_ID('dbo.OTA_Email_Parse_Learning', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OTA_Email_Parse_Learning (
        ID            BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        -- ลายนิ้วมือเทมเพลต (ทำจากป้ายชื่อฟิลด์ที่ปรากฏ + จำนวนตาราง)
        Template_Key  VARCHAR(32)    NOT NULL,
        Channel       NVARCHAR(60)   NULL,
        Field_Name    NVARCHAR(40)   NOT NULL,   -- BookingId / PaymentType / ...
        Strategy_Key  NVARCHAR(80)   NOT NULL,   -- regex#1 / dom-cell-right:... / ai
        Success_Count INT            NOT NULL DEFAULT 0,
        Fail_Count    INT            NOT NULL DEFAULT 0,
        Last_Value    NVARCHAR(200)  NULL,
        Last_Seen     DATETIME       NULL,
        Created_Date  DATETIME       NOT NULL DEFAULT GETDATE()
    );
    CREATE UNIQUE INDEX UX_OtaLearn_Key
        ON dbo.OTA_Email_Parse_Learning (Template_Key, Field_Name, Strategy_Key);
    PRINT N'สร้างตาราง OTA_Email_Parse_Learning';
END
ELSE PRINT N'มี OTA_Email_Parse_Learning อยู่แล้ว — ข้าม';
GO

-- ── 2) เทมเพลตที่เคยเจอ ─────────────────────────────────────────────────────
IF OBJECT_ID('dbo.OTA_Email_Template_Seen', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OTA_Email_Template_Seen (
        Template_Key    VARCHAR(32)   NOT NULL PRIMARY KEY,
        Channel         NVARCHAR(60)  NULL,
        Sample_Subject  NVARCHAR(300) NULL,
        Email_Count     INT           NOT NULL DEFAULT 0,
        First_Seen      DATETIME      NOT NULL DEFAULT GETDATE(),
        Last_Seen       DATETIME      NULL,
        Last_Confidence INT           NULL,      -- คะแนนความมั่นใจครั้งล่าสุด 0-100
        Needs_Review    BIT           NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_OtaTemplate_Review ON dbo.OTA_Email_Template_Seen (Needs_Review, Last_Seen);
    PRINT N'สร้างตาราง OTA_Email_Template_Seen';
END
ELSE PRINT N'มี OTA_Email_Template_Seen อยู่แล้ว — ข้าม';
GO

-- ── 3) ค่าตั้งค่า ───────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
BEGIN
    DECLARE @cfg TABLE (K NVARCHAR(100), V NVARCHAR(500));
    INSERT INTO @cfg (K, V) VALUES
        -- คะแนนขั้นต่ำที่ถือว่ามั่นใจพอจะใช้เลย (ต่ำกว่านี้ = ถาม AI แล้วถ้ายังต่ำ = แจ้งคน)
        ('Email_Rsv_MinConfidence', '60'),
        -- ให้ AI ช่วยอ่านตอนคะแนนต่ำไหม (ต้องเปิด AI_Enabled ในระบบด้วย)
        ('Email_Rsv_AiAssist', '1'),
        -- เพดานยอดจองที่ถือว่าปกติ เกินกว่านี้เตือน (ไม่บล็อก)
        ('Email_Rsv_MaxTotalSanity', '500000'),
        -- อีเมลไม่บอกว่าใครเก็บเงิน ให้ถือว่าอะไร (CHANNEL = OTA เก็บแล้ว / HOTEL = เก็บหน้างาน)
        ('Email_Rsv_DefaultCollect', 'CHANNEL');

    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue)
    SELECT c.K, c.V FROM @cfg c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config x WHERE x.ConfigKey = c.K);

    PRINT N'เพิ่มค่าตั้งใหม่: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' คีย์ (ของเดิมไม่ถูกแตะ)';
END
ELSE PRINT N'⚠ ไม่มีตาราง Accounting_Integration_Config — ข้ามส่วนค่าตั้งค่า';
GO

-- ── ตรวจผล ──────────────────────────────────────────────────────────────────
SELECT N'OTA_Email_Parse_Learning' AS [ตาราง],
       CASE WHEN OBJECT_ID('dbo.OTA_Email_Parse_Learning','U') IS NULL THEN N'❌' ELSE N'✅' END AS [สถานะ]
UNION ALL
SELECT N'OTA_Email_Template_Seen',
       CASE WHEN OBJECT_ID('dbo.OTA_Email_Template_Seen','U') IS NULL THEN N'❌' ELSE N'✅' END;

PRINT '';
PRINT N'ระบบจะเริ่มสะสมบทเรียนเองตั้งแต่อีเมลฉบับถัดไป — ไม่ต้องตั้งค่าอะไรเพิ่ม';
PRINT N'ดูว่าเจอเทมเพลตอะไรบ้าง: SELECT * FROM OTA_Email_Template_Seen ORDER BY Last_Seen DESC;';
PRINT N'ดูว่าอ่านด้วยวิธีไหนแล้วถูก:  SELECT * FROM OTA_Email_Parse_Learning ORDER BY Success_Count DESC;';
GO
