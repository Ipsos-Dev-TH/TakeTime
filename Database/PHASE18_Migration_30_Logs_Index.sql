-- ============================================================================
-- PHASE18 Migration 30 — Index บนตาราง Logs
-- ============================================================================
-- อาการที่แก้: หน้า Admin → NextAcc → คิว → กดปุ่ม "Log" ของบางรายการแล้ว
--   หมุนค้าง "กำลังโหลด..." ไม่จบ
-- สาเหตุ: query ค้น log ใช้ LogDetail LIKE '%เลขใบเสร็จ%' บนตาราง Logs ที่โต
--   เป็นหลักแสน-ล้านแถว → full table scan ทุกครั้ง
-- ฝั่งโค้ดแก้แล้ว (จำกัดช่วงเวลา + CommandTimeout 20 วิ + ยกเลิกฝั่ง browser 40 วิ)
--   สคริปต์นี้เติม index ให้ query ช่วงเวลาเร็วขึ้นอีก และช่วยหน้า Logs ทั้งระบบด้วย
--
-- ปลอดภัย: รันซ้ำได้ (IF NOT EXISTS), ไม่แก้ข้อมูล, ไม่ล็อกตารางนาน
--   (ถ้าใช้ SQL Server Enterprise เพิ่ม WITH (ONLINE = ON) ได้)
-- ============================================================================

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Logs')
BEGIN
    PRINT 'ข้าม: ไม่พบตาราง Logs';
    RETURN;
END

-- ── 1) index หลัก: กรองตาม LogAction + ช่วงเวลา ────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_Action_Date' AND object_id = OBJECT_ID('Logs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Logs_Action_Date
        ON dbo.Logs (LogAction, LogDateTime DESC);
    PRINT 'สร้าง IX_Logs_Action_Date แล้ว';
END
ELSE
    PRINT 'มี IX_Logs_Action_Date อยู่แล้ว — ข้าม';

-- ── 2) index รอง: หน้า Logs ทั่วไปที่เรียงตามเวลาอย่างเดียว ────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_Date' AND object_id = OBJECT_ID('Logs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Logs_Date
        ON dbo.Logs (LogDateTime DESC);
    PRINT 'สร้าง IX_Logs_Date แล้ว';
END
ELSE
    PRINT 'มี IX_Logs_Date อยู่แล้ว — ข้าม';

-- ── 3) รายงานขนาดตาราง เผื่อควรตั้ง job ลบ log เก่า ────────────────────────
DECLARE @rows BIGINT;
SELECT @rows = SUM(p.rows)
FROM sys.partitions p
WHERE p.object_id = OBJECT_ID('Logs') AND p.index_id IN (0, 1);

PRINT 'จำนวนแถวในตาราง Logs ≈ ' + ISNULL(CAST(@rows AS VARCHAR(20)), '?');
IF @rows > 2000000
    PRINT '⚠ Logs เกิน 2 ล้านแถว — ควรตั้ง job ลบ log เก่ากว่า 6-12 เดือน เพื่อให้หน้า Logs/คิวเร็วขึ้น';

-- ── 4) ตรวจสอบ ─────────────────────────────────────────────────────────────
SELECT i.name AS IndexName,
       STUFF((SELECT ', ' + c.name
              FROM sys.index_columns ic
              JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
              ORDER BY ic.key_ordinal
              FOR XML PATH('')), 1, 2, '') AS Columns
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('Logs') AND i.name IS NOT NULL;
