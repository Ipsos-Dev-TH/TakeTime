-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 27: คอลัมน์ที่โค้ดอ้างถึงแต่ยังไม่มีในฐาน (แก้ error ที่พบหน้างาน)
-- ════════════════════════════════════════════════════════════════════════════
-- 1) Reservation.Modified_Date — ตัวอ่านอีเมลจอง OTA เขียนเวลาที่แก้ไข/ยกเลิกลงคอลัมน์นี้
--    ฐานที่ยังไม่มีคอลัมน์จะขึ้น "Invalid column name 'Modified_Date'" ตอนอีเมลแก้ไขเข้ามา
--    (โค้ดถูกแก้ให้ข้ามอัตโนมัติแล้วถ้าไม่มีคอลัมน์ — สคริปต์นี้ทำให้ได้ audit ครบ)
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'Modified_Date')
BEGIN
    ALTER TABLE dbo.Reservation ADD Modified_Date DATETIME NULL;
    PRINT 'Added Reservation.Modified_Date';
END
ELSE
    PRINT 'Reservation.Modified_Date already exists - skipped';
GO

PRINT 'PHASE18_27 completed';
GO
