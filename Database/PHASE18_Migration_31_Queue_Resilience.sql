-- ============================================================================
-- PHASE18 Migration 31 — ความทนทานของคิว sync บัญชี (Accounting_Sync_Queue)
-- ============================================================================
-- แก้ 3 เรื่องที่เจอจริงตอน NextAcc ล่ม (DI circular dependency, ส.ค. 2026):
--
-- 1) เพิ่มคอลัมน์ Processing_Started
--    ของเดิมกู้รายการที่ค้าง PROCESSING โดยดูจาก Created_Date < 10 นาทีที่แล้ว
--    ซึ่งเป็น "เวลาที่สร้างรายการ" ไม่ใช่ "เวลาที่เริ่มยิง" → รายการที่สร้างไว้นาน
--    แล้วกำลังยิงอยู่จริง ๆ จะถูกดึงกลับเป็น PENDING แล้วยิงซ้ำ = เอกสารซ้ำใน NextAcc
--
-- 2) ตั้งค่าใหม่สำหรับกัน retry storm / แจ้งเตือน / ล้าง log
--
-- 3) index สำหรับหน้าคิว + การกู้รายการค้าง
--
-- ปลอดภัย: รันซ้ำได้, ไม่ลบข้อมูลเดิม
-- ============================================================================

SET NOCOUNT ON;

-- ── 1) คอลัมน์ Processing_Started ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Accounting_Sync_Queue' AND COLUMN_NAME = 'Processing_Started')
BEGIN
    ALTER TABLE Accounting_Sync_Queue ADD Processing_Started DATETIME NULL;
    PRINT 'เพิ่มคอลัมน์ Accounting_Sync_Queue.Processing_Started แล้ว';
END
ELSE
    PRINT 'มีคอลัมน์ Processing_Started อยู่แล้ว — ข้าม';
GO

-- รายการที่ค้าง PROCESSING อยู่ตอนนี้ (จากรอบก่อนหน้าที่ล่ม) ให้ถือว่าเริ่มตั้งแต่ตอนสร้าง
UPDATE Accounting_Sync_Queue
SET Processing_Started = Created_Date
WHERE Status = 'PROCESSING' AND Processing_Started IS NULL;
GO

-- ── 2) ค่าตั้งค่าใหม่ ───────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_ServerDown_Cooldown_Min')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_ServerDown_Cooldown_Min', N'5',
            N'NextAcc ล่ม (5xx/หน้า error page) -> พักไม่ยิงกี่นาที กัน retry storm', GETDATE());
GO

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Stuck_Processing_Min')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_Stuck_Processing_Min', N'15',
            N'รายการค้าง PROCESSING เกินกี่นาที ถือว่า worker ตาย -> คืนเป็น PENDING', GETDATE());
GO

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_SyncLog_Retention_Days')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_SyncLog_Retention_Days', N'90',
            N'เก็บ Accounting_Sync_Log ย้อนหลังกี่วัน (0 = ไม่ลบ)', GETDATE());
GO

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Queue_Alert')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_Queue_Alert', N'1',
            N'แจ้งเตือน Telegram เมื่อคิวมีรายการล้มเหลวค้าง / NextAcc ล่ม', GETDATE());
GO

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Queue_Alert_Hours')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_Queue_Alert_Hours', N'6',
            N'เตือนซ้ำได้ทุกกี่ชั่วโมง (กันสแปมกลุ่ม)', GETDATE());
GO

-- ── 3) index ────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SyncQueue_Processing'
               AND object_id = OBJECT_ID('Accounting_Sync_Queue'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SyncQueue_Processing
        ON Accounting_Sync_Queue (Status, Processing_Started);
    PRINT 'สร้าง IX_SyncQueue_Processing แล้ว';
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounting_Sync_Log')
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SyncLog_CreatedDate'
                   AND object_id = OBJECT_ID('Accounting_Sync_Log'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SyncLog_CreatedDate
        ON Accounting_Sync_Log (Created_Date);
    PRINT 'สร้าง IX_SyncLog_CreatedDate แล้ว (ให้การล้าง log เก่าเร็วขึ้น)';
END
GO

-- ── 4) กู้รายการที่ตกเป็น FAILED เพราะ NextAcc ล่ม (ไม่ใช่ข้อมูลผิด) ────────
-- คืนเฉพาะรายการที่ error บ่งชี้ว่าเป็นปัญหาฝั่งเซิร์ฟเวอร์ — รายการที่ผิดข้อมูลจริงคงไว้
DECLARE @revived INT;
UPDATE Accounting_Sync_Queue
SET Status = 'PENDING', Retry_Count = 0, Next_Retry_Date = NULL,
    Error_Message = N'คืนคิวอัตโนมัติ (migration 31): ล้มเหลวเพราะ NextAcc ไม่พร้อมใช้งาน ไม่ใช่ข้อมูลผิด'
WHERE Status = 'FAILED'
  AND Retry_Count >= Max_Retries
  AND (Error_Message LIKE N'%An error occurred while starting the application%'
    OR Error_Message LIKE N'%circular dependency%'
    OR Error_Message LIKE N'%Server error InternalServerError%'
    OR Error_Message LIKE N'%Server error BadGateway%'
    OR Error_Message LIKE N'%Server error ServiceUnavailable%');
SET @revived = @@ROWCOUNT;
PRINT 'คืนคิวรายการที่ล้มเพราะ NextAcc ล่ม: ' + CAST(@revived AS VARCHAR(10)) + ' รายการ';
GO

-- ── 5) ตรวจสอบ ─────────────────────────────────────────────────────────────
SELECT Status, COUNT(*) AS Items,
       SUM(CASE WHEN Retry_Count >= Max_Retries THEN 1 ELSE 0 END) AS RetriesExhausted
FROM Accounting_Sync_Queue
GROUP BY Status
ORDER BY Status;

SELECT ConfigKey, ConfigValue
FROM Accounting_Integration_Config
WHERE ConfigKey IN (N'Nexaacc_ServerDown_Cooldown_Min', N'Nexaacc_Stuck_Processing_Min',
                    N'Nexaacc_SyncLog_Retention_Days', N'Nexaacc_Queue_Alert', N'Nexaacc_Queue_Alert_Hours')
ORDER BY ConfigKey;
