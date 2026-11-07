-- ============================================================================
-- PHASE19 Migration 06 — เติมคอลัมน์ AI ที่ขาดใน OmniChannel_Messages
-- ============================================================================
-- อาการ: กดตอบกลับในกล่องแชทรวมแล้วขึ้น
--        "Invalid column name 'IsAIGenerated'. / 'AIConfidence'. / 'AISource'."
--
-- สาเหตุ: คอลัมน์ 3 ตัวนี้มาจาก PHASE14_Migration_02 (ระบบเรียนรู้ของ AI)
--         เครื่องนี้ยังไม่ได้รันไมเกรชันนั้น แต่โค้ดฝั่งส่งข้อความอ้างถึงคอลัมน์เสมอ
--         ⇒ INSERT ล้มทั้งก้อน = ตอบลูกค้าไม่ได้เลย
--
-- หมายเหตุ: ตัวตรวจใน PHASE14_02 เช็คแค่ IsAIGenerated ตัวเดียวแล้วเพิ่มทั้ง 3
--           ถ้าเคยมีบางตัวอยู่แล้วจะข้ามทั้งบล็อก — ไฟล์นี้จึงตรวจ "ทีละคอลัมน์"
--
-- ปลอดภัย: รันซ้ำได้ ไม่แตะข้อมูลเดิม เพิ่มคอลัมน์ที่ยอมให้เป็น NULL เท่านั้น
-- (โค้ดฝั่งแอปแก้ให้ทำงานได้แม้ยังไม่มีคอลัมน์แล้ว — ไฟล์นี้ทำให้กลับมาเก็บข้อมูล AI ได้ครบ)
-- ============================================================================

SET NOCOUNT ON;

IF OBJECT_ID('dbo.OmniChannel_Messages', 'U') IS NULL
BEGIN
    PRINT N'ยังไม่มีตาราง OmniChannel_Messages — ข้ามไมเกรชันนี้';
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'OmniChannel_Messages' AND COLUMN_NAME = 'IsAIGenerated')
BEGIN
    ALTER TABLE dbo.OmniChannel_Messages ADD IsAIGenerated BIT NOT NULL CONSTRAINT DF_OmniMsg_IsAIGenerated DEFAULT 0;
    PRINT N'เพิ่มคอลัมน์ IsAIGenerated';
END
ELSE PRINT N'มี IsAIGenerated อยู่แล้ว';
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'OmniChannel_Messages' AND COLUMN_NAME = 'AIConfidence')
BEGIN
    ALTER TABLE dbo.OmniChannel_Messages ADD AIConfidence FLOAT NULL;
    PRINT N'เพิ่มคอลัมน์ AIConfidence';
END
ELSE PRINT N'มี AIConfidence อยู่แล้ว';
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'OmniChannel_Messages' AND COLUMN_NAME = 'AISource')
BEGIN
    ALTER TABLE dbo.OmniChannel_Messages ADD AISource NVARCHAR(30) NULL;
    PRINT N'เพิ่มคอลัมน์ AISource';
END
ELSE PRINT N'มี AISource อยู่แล้ว';
GO

-- ── ตรวจผล ─────────────────────────────────────────────────────────────────
SELECT COLUMN_NAME AS [คอลัมน์], DATA_TYPE AS [ชนิด], IS_NULLABLE AS [ว่างได้]
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'OmniChannel_Messages'
  AND COLUMN_NAME IN ('IsAIGenerated','AIConfidence','AISource')
ORDER BY COLUMN_NAME;

PRINT '';
PRINT N'ครบ 3 แถว = ตอบข้อความได้ปกติและเก็บข้อมูล AI ได้ครบแล้ว';
