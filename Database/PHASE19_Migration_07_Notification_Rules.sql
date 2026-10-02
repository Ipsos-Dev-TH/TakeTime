-- ============================================================================
-- PHASE19 Migration 07 — ตั้งค่าการแจ้งเตือนรายเหตุการณ์ × รายช่องทาง
-- ============================================================================
-- ปัญหาเดิม:
--   • ทุกจุดที่ส่ง Telegram ยิงตรงไปที่ api.telegram.org พร้อม chat id ที่ hard-code ไว้
--     ในโค้ด ("-4969611371") ⇒ ปิดเฉพาะบางเรื่องไม่ได้ ย้ายกลุ่มไม่ได้
--   • หน้า Admin → การแจ้งเตือน เป็น "เปลือก" — toggle เป็น JavaScript ล้วน
--     ไม่บันทึกค่า ไม่มีใครอ่าน กดแล้วไม่เกิดอะไรขึ้น
--   • LINE ส่งได้อย่างเดียวคือรายงานตารางจองรายวัน
--
-- ไมเกรชันนี้สร้างตารางกฎ "เหตุการณ์ × ช่องทาง" ให้เปิด/ปิดได้ทีละอย่าง
-- และกำหนดปลายทางแยกรายเหตุการณ์ได้ (เช่น เรื่องบัญชีเข้ากลุ่มบัญชี)
--
-- ⚠ ค่าเริ่มต้นถูกตั้งให้ "เหมือนพฤติกรรมปัจจุบันเป๊ะ":
--     Telegram = เปิดทุกเหตุการณ์ที่วันนี้ส่งอยู่แล้ว, ปิดเหตุการณ์ที่วันนี้ยังไม่ส่ง
--     LINE     = ปิดทุกเหตุการณ์ (วันนี้ยังไม่มีเหตุการณ์ไหนส่ง LINE)
--   และเคารพสวิตช์เดิมที่มีอยู่ (Email_Rsv_NotifyTelegram / Nexaacc_Queue_Alert)
--   ⇒ deploy แล้วไม่มีอะไรเปลี่ยนจนกว่าจะไปกดปิด/เปิดเอง
--
-- ปลอดภัย: รันซ้ำได้ ไม่ลบของเดิม ไม่แตะตารางอื่น
-- ============================================================================

SET NOCOUNT ON;
GO

-- ── 1) ตารางกฎการแจ้งเตือน ──────────────────────────────────────────────────
IF OBJECT_ID('dbo.Notification_Rules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notification_Rules (
        Event_Code    NVARCHAR(60)  NOT NULL,
        Channel       NVARCHAR(20)  NOT NULL,      -- TELEGRAM / LINE
        Enabled       BIT           NOT NULL DEFAULT 0,
        Target        NVARCHAR(300) NULL,          -- chat id / LINE userId|groupId (ว่าง = ใช้ปลายทางกลาง)
        Modified_Date DATETIME      NULL,
        Modified_By   INT           NULL,
        CONSTRAINT PK_Notification_Rules PRIMARY KEY (Event_Code, Channel)
    );
    PRINT N'สร้างตาราง Notification_Rules';
END
ELSE PRINT N'มี Notification_Rules อยู่แล้ว — ข้าม';
GO

-- ── 2) ค่าเดิมที่ต้องเคารพ (สวิตช์ที่ผู้ดูแลเคยตั้งไว้) ─────────────────────
DECLARE @otaOn BIT = 1, @otaSummaryOn BIT = 1, @queueOn BIT = 1;

IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
BEGIN
    SELECT @otaOn = CASE WHEN ISNULL(ConfigValue, '1') = '0' THEN 0 ELSE 1 END
      FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_NotifyTelegram';

    SELECT @otaSummaryOn = CASE WHEN ISNULL(ConfigValue, '1') = '0' THEN 0 ELSE 1 END
      FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_NotifySummary';

    SELECT @queueOn = CASE WHEN ISNULL(ConfigValue, '1') = '0' THEN 0 ELSE 1 END
      FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Queue_Alert';
END

-- ── 3) กฎตั้งต้น ────────────────────────────────────────────────────────────
-- คอลัมน์ TG = ค่าเริ่มต้นของ Telegram, LN = ของ LINE
DECLARE @seed TABLE (Code NVARCHAR(60), TG BIT, LN BIT);

INSERT INTO @seed (Code, TG, LN) VALUES
    -- การจองที่ทำในระบบ — วันนี้ส่ง Telegram อยู่แล้วทุกตัว
    ('BOOKING_NEW',       1, 0),
    ('BOOKING_EDIT',      1, 0),
    ('BOOKING_POSTPONE',  1, 0),
    ('BOOKING_CANCEL',    1, 0),
    -- การจองจากอีเมล OTA — เคารพสวิตช์เดิม
    ('OTA_BOOKING_OK',    @otaOn, 0),
    ('OTA_BOOKING_FAIL',  @otaOn, 0),
    ('OTA_SUMMARY',       @otaSummaryOn, 0),
    ('OTA_INTAKE_STALE',  @otaOn, 0),
    -- ข้อความจากลูกค้า — วันนี้ส่ง Telegram อยู่แล้ว
    ('CHAT_GUEST',        1, 0),
    ('CHAT_PUBLIC',       1, 0),
    ('CHAT_OTA_EMAIL',    1, 0),
    -- บริการในที่พัก — วันนี้ยังไม่ส่งเข้า Telegram/LINE (แจ้งในระบบอย่างเดียว)
    ('ORDER_ROOMSERVICE', 0, 0),
    ('ORDER_AMENITY',     1, 0),   -- วันนี้ส่ง Telegram อยู่แล้ว (AmenityService) จึงตั้งเปิดไว้
    ('ORDER_ACTIVITY',    0, 0),
    -- เงินและเอกสาร
    ('PAYMENT_ONLINE',    0, 0),
    ('ETAX_RD',           1, 0),
    -- ระบบ
    ('ACC_QUEUE_ALERT',   @queueOn, 0),
    ('SYSTEM_ERROR',      1, 0);

INSERT INTO dbo.Notification_Rules (Event_Code, Channel, Enabled, Modified_Date)
SELECT s.Code, 'TELEGRAM', s.TG, GETDATE()
FROM @seed s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Notification_Rules r
                  WHERE r.Event_Code = s.Code AND r.Channel = 'TELEGRAM');
PRINT N'เพิ่มกฎ Telegram: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' เหตุการณ์';

INSERT INTO dbo.Notification_Rules (Event_Code, Channel, Enabled, Modified_Date)
SELECT s.Code, 'LINE', s.LN, GETDATE()
FROM @seed s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Notification_Rules r
                  WHERE r.Event_Code = s.Code AND r.Channel = 'LINE');
PRINT N'เพิ่มกฎ LINE: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' เหตุการณ์';
GO

-- ── 4) ค่าตั้งกลางใน System_Config ─────────────────────────────────────────
IF OBJECT_ID('dbo.System_Config', 'U') IS NOT NULL
BEGIN
    DECLARE @cfg TABLE (K NVARCHAR(100), V NVARCHAR(400), Cat NVARCHAR(60),
                        DN NVARCHAR(200), D NVARCHAR(1000), IT NVARCHAR(20), Ord INT);

    INSERT INTO @cfg (K, V, Cat, DN, D, IT, Ord) VALUES
    ('Notify_Telegram_Enabled', '1', N'การแจ้งเตือน', N'เปิดช่องทาง Telegram',
     N'สวิตช์ใหญ่ — ปิดแล้วไม่ส่ง Telegram เลยไม่ว่าเหตุการณ์ไหนจะเปิดอยู่', 'bool', 10),
    ('Notify_Line_Enabled', '0', N'การแจ้งเตือน', N'เปิดช่องทาง LINE',
     N'สวิตช์ใหญ่ของ LINE (ต้องใส่ปลายทางด้วยจึงจะส่งได้)', 'bool', 20),
    ('Notify_Line_Target', '', N'การแจ้งเตือน', N'ปลายทาง LINE (กลาง)',
     N'userId หรือ groupId ที่จะรับแจ้งเตือน คั่นหลายรายการด้วยจุลภาค — หาได้จากหน้าตั้งค่า LINE', 'text', 30),
    ('Notify_QuietHours_From', '', N'การแจ้งเตือน', N'เริ่มช่วงเวลาเงียบ',
     N'รูปแบบ HH:mm เช่น 22:00 — ปล่อยว่าง = ไม่ใช้ช่วงเวลาเงียบ', 'text', 40),
    ('Notify_QuietHours_To', '', N'การแจ้งเตือน', N'สิ้นสุดช่วงเวลาเงียบ',
     N'รูปแบบ HH:mm เช่น 07:00 (ข้ามเที่ยงคืนได้)', 'text', 50),
    ('Notify_QuietHours_AllowUrgent', '1', N'การแจ้งเตือน', N'เรื่องด่วนส่งได้แม้ในช่วงเงียบ',
     N'เรื่องด่วน = ลงจอง OTA ไม่สำเร็จ, ไม่ได้รับอีเมลนานผิดปกติ, คิวบัญชีมีปัญหา, ข้อผิดพลาดระบบ', 'bool', 60);

    INSERT INTO dbo.System_Config (ConfigKey, ConfigValue, Category, DisplayName, [Description], IsSecret, InputType, DisplayOrder, ModifiedDate)
    SELECT c.K, c.V, c.Cat, c.DN, c.D, 0, c.IT, c.Ord, GETDATE()
    FROM @cfg c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.System_Config s WHERE s.ConfigKey = c.K);

    PRINT N'เพิ่มค่าตั้งกลางของการแจ้งเตือน: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' คีย์';
END
GO

-- ── ตรวจผล ─────────────────────────────────────────────────────────────────
SELECT r.Event_Code AS [เหตุการณ์],
       MAX(CASE WHEN r.Channel = 'TELEGRAM' AND r.Enabled = 1 THEN N'✓' ELSE N'—' END) AS [Telegram],
       MAX(CASE WHEN r.Channel = 'LINE'     AND r.Enabled = 1 THEN N'✓' ELSE N'—' END) AS [LINE],
       MAX(ISNULL(r.Target, N'')) AS [ปลายทางเฉพาะ]
FROM dbo.Notification_Rules r
GROUP BY r.Event_Code
ORDER BY r.Event_Code;

PRINT '';
PRINT N'ตั้งค่าต่อได้ที่: ศูนย์ตั้งค่า → การแจ้งเตือน';
PRINT N'ค่าเริ่มต้นตรงกับพฤติกรรมเดิมทุกอย่าง — ยังไม่มีอะไรเปลี่ยนจนกว่าจะไปกดเอง';
