-- ============================================================================
-- PHASE19 Migration 12 — เก็บเหตุผลที่กันวงเงินไม่สำเร็จ + ปลดล็อกลิงก์ที่ตายไปแล้ว
--
-- ปัญหาที่แก้:
--   1. ลูกค้ากรอกบัตรแล้วเกตเวย์ปฏิเสธ → ระบบตั้งสถานะเป็น FAILED ทันที
--      ลิงก์ที่ส่งให้ลูกค้าจึงใช้ไม่ได้อีกเลย ต้องให้พนักงานสร้างใหม่ทุกครั้ง
--      ทั้งที่ความจริงแค่ "บัตรใบนั้นไม่ผ่าน" — ควรลองใบอื่นบนลิงก์เดิมได้
--   2. เหตุผลจริงจากเกตเวย์ (บัตรถูกปฏิเสธ / วงเงินไม่พอ / ยังไม่เปิดใช้ pre-auth)
--      ไม่ถูกเก็บไว้ที่ไหนเลย เหลือแต่ Raw_Response ดิบ ๆ ไม่มีใครเปิดดู
--
-- ปลอดภัย: รันซ้ำได้ ไม่แตะรายการที่กันวงเงินสำเร็จแล้ว
-- ============================================================================

SET NOCOUNT ON;

IF OBJECT_ID('dbo.Payment_Security_Holds', 'U') IS NULL
BEGIN
    PRINT N'⚠ ยังไม่มีตาราง Payment_Security_Holds — รัน PHASE19_Migration_09 ก่อน';
    RETURN;
END
GO

-- ── 1) คอลัมน์เก็บเหตุผลครั้งล่าสุดที่ไม่สำเร็จ ─────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Payment_Security_Holds')
                 AND name = 'Fail_Reason')
BEGIN
    ALTER TABLE dbo.Payment_Security_Holds ADD Fail_Reason NVARCHAR(400) NULL;
    PRINT N'เพิ่มคอลัมน์ Fail_Reason';
END
ELSE PRINT N'มีคอลัมน์ Fail_Reason อยู่แล้ว — ข้าม';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Payment_Security_Holds')
                 AND name = 'Fail_Count')
BEGIN
    ALTER TABLE dbo.Payment_Security_Holds ADD Fail_Count INT NOT NULL DEFAULT 0;
    PRINT N'เพิ่มคอลัมน์ Fail_Count (นับครั้งที่บัตรไม่ผ่าน)';
END
ELSE PRINT N'มีคอลัมน์ Fail_Count อยู่แล้ว — ข้าม';
GO

-- ── 2) ปลดล็อกรายการที่ตายไปเพราะบั๊กนี้ ───────────────────────────────────
-- เงื่อนไข: FAILED + ไม่เคยมี charge id ฝั่งเกตเวย์ = ไม่เคยกันวงเงินได้จริง
-- ⇒ กลับไปเป็น "รอลูกค้ากรอกบัตร" ลิงก์เดิมใช้ต่อได้ ไม่มีเงินค้างที่ไหน
UPDATE dbo.Payment_Security_Holds
   SET [Status] = 'PENDING_CARD',
       Updated_Date = GETDATE()
 WHERE [Status] = 'FAILED'
   AND ISNULL(Provider_Charge_ID, '') = ''
   AND Held_At IS NULL
   AND Captured_At IS NULL
   AND Released_At IS NULL;

PRINT N'ปลดล็อกลิงก์วงเงินที่ยังไม่เคยกันสำเร็จ: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' รายการ';
GO

SELECT N'Payment_Security_Holds' AS [ตาราง],
       SUM(CASE WHEN [Status] = 'PENDING_CARD' THEN 1 ELSE 0 END) AS [รอกรอกบัตร],
       SUM(CASE WHEN [Status] = 'HELD'         THEN 1 ELSE 0 END) AS [กันวงเงินอยู่],
       SUM(CASE WHEN [Status] = 'FAILED'       THEN 1 ELSE 0 END) AS [ไม่สำเร็จ]
  FROM dbo.Payment_Security_Holds;

PRINT '';
PRINT N'หลังรันไฟล์นี้: บัตรไม่ผ่านจะไม่ปิดลิงก์อีกต่อไป — ลูกค้าลองบัตรใบอื่นบนลิงก์เดิมได้';
PRINT N'และหน้ากรอกบัตรจะแสดงเหตุผลจริงจากเกตเวย์แทนข้อความ "รายการนี้ปิดไปแล้ว"';
GO
