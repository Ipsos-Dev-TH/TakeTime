-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 08: Post-sync verify (ตรวจย้อนกลับว่าลงข้อมูลถูกบน NextAcc)
-- ════════════════════════════════════════════════════════════════════════════
-- หลัง sync ใบเสร็จ/เช็คเอาท์สำเร็จ ระบบจะ "อ่านเอกสาร+JE+ไฟล์แนบกลับจาก NextAcc" มาเทียบกับ
-- ความจริงฝั่งเรา (ยอดรับจริง + สลิป) → เก็บผลตรวจไว้ 2 คอลัมน์นี้:
--   Verify_Status : PASS / WARN (พบข้อผิดปกติ) — NULL = ยังไม่ตรวจ
--   Verify_Detail : รายละเอียดที่ตรวจ (ยอดตรง/JE บาลานซ์/21510 ไม่ติดลบ/สลิปแนบ … หรือรายการที่ผิด)
--
-- ดัก: ยอดรวมไม่ตรง, JE ไม่บาลานซ์, บัญชีมัดจำ 21510 ติดลบ (double-reverse), สลิปไม่แนบ, เอกสารไม่โพสต์.
-- แสดงผลในหน้าคิว (คอลัมน์ "ตรวจสอบ") + ปุ่ม Log. ควบคุมด้วย config Nexaacc_Post_Sync_Verify (default 1).
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Accounting_Sync_Queue' AND COLUMN_NAME = 'Verify_Status'
)
BEGIN
    ALTER TABLE [dbo].[Accounting_Sync_Queue] ADD Verify_Status NVARCHAR(10) NULL;
    PRINT 'Added Accounting_Sync_Queue.Verify_Status';
END
ELSE
    PRINT 'Accounting_Sync_Queue.Verify_Status already exists';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Accounting_Sync_Queue' AND COLUMN_NAME = 'Verify_Detail'
)
BEGIN
    ALTER TABLE [dbo].[Accounting_Sync_Queue] ADD Verify_Detail NVARCHAR(1000) NULL;
    PRINT 'Added Accounting_Sync_Queue.Verify_Detail';
END
ELSE
    PRINT 'Accounting_Sync_Queue.Verify_Detail already exists';
GO

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Post_Sync_Verify')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description) VALUES
    ('Nexaacc_Post_Sync_Verify', '1',
     N'1 = หลัง sync ใบเสร็จ/เช็คเอาท์ อ่าน GL กลับมาเทียบ (ยอด/บาลานซ์/21510/สลิป) เก็บผล Verify_Status/Detail; 0 = ปิด');
    PRINT 'Inserted config Nexaacc_Post_Sync_Verify = 1';
END
ELSE
    PRINT 'Config Nexaacc_Post_Sync_Verify already exists';
GO
