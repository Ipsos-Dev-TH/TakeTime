-- ============================================================================
-- PHASE19 Migration 11 — เงินประกันความเสียหายครบวงจร (เงินสด/บัตร) + วงเงินรายห้อง
-- ============================================================================
-- ตามที่ตกลง:
--   • ตอนเช็คอินเลือกได้ว่ารับประกันเป็น "เงินสด" หรือ "กันวงเงินบัตรเครดิต"
--     — เงินสดถูกบันทึกเข้าระบบด้วย (เดิมอยู่นอกระบบทั้งขาเข้า-ขาออก)
--   • วงเงินประกันตั้งได้ "รายห้องพัก" (คอลัมน์ใหม่ใน Accommodation)
--   • วงเงินบัตรเกิน 7 วันหมดอายุ → ระบบสร้างลิงก์ใหม่ให้เองพร้อมแจ้งเตือน
--   • เช็คเอาท์: คืนวงเงิน/คืนเงินสด หรือหักค่าเสียหาย (ทุกคนที่เข้าหน้าเช็คเอาท์ได้)
--
-- ปลอดภัย: รันซ้ำได้ · เพิ่มคอลัมน์ nullable เดียว ไม่แตะข้อมูลเดิม
-- ต้องรัน PHASE19_09 (ตาราง Payment_Security_Holds) มาก่อน
-- ============================================================================

SET NOCOUNT ON;

-- ── 1) วงเงินประกันรายห้องพัก ───────────────────────────────────────────────
IF OBJECT_ID('dbo.Accommodation', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Accommodation', 'Security_Deposit_Amount') IS NULL
    BEGIN
        ALTER TABLE dbo.Accommodation ADD Security_Deposit_Amount DECIMAL(18,2) NULL;
        PRINT N'เพิ่มคอลัมน์ Accommodation.Security_Deposit_Amount (NULL = ใช้ค่ากลาง)';
    END
    ELSE PRINT N'มีคอลัมน์ Security_Deposit_Amount อยู่แล้ว — ข้าม';
END
GO

-- ── 2) ตารางวงเงินต้องรองรับเงินสด (ไม่มีอะไรต้องแก้ schema — Provider='CASH') ──
IF OBJECT_ID('dbo.Payment_Security_Holds', 'U') IS NULL
    PRINT N'⚠ ยังไม่มีตาราง Payment_Security_Holds — รัน PHASE19_Migration_09 ก่อน';
ELSE
    PRINT N'ตารางวงเงินพร้อม — เงินสดใช้ Provider=''CASH'' ในตารางเดียวกัน';
GO

-- ── 3) คอลัมน์ยอดคืนเงินสะสมบน Payment_Transaction (รองรับปุ่มคืนเงิน) ──────
IF OBJECT_ID('dbo.Payment_Transaction', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Payment_Transaction', 'Refunded_Amount') IS NULL
    BEGIN
        ALTER TABLE dbo.Payment_Transaction ADD Refunded_Amount DECIMAL(18,2) NULL;
        PRINT N'เพิ่มคอลัมน์ Payment_Transaction.Refunded_Amount (ยอดคืนสะสม — กันคืนเกิน)';
    END
    ELSE PRINT N'มีคอลัมน์ Refunded_Amount อยู่แล้ว — ข้าม';
END
GO

-- ── ตรวจผล + วิธีตั้งค่า ────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Accommodation', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Accommodation', 'Security_Deposit_Amount') IS NOT NULL
BEGIN
    PRINT '';
    PRINT N'--- วงเงินประกันปัจจุบันรายห้อง (NULL = ใช้ค่ากลาง Payment_SecurityHold_Default) ---';
    EXEC('SELECT ID, AccomName AS [ห้องพัก], Security_Deposit_Amount AS [วงเงินประกัน]
            FROM dbo.Accommodation ORDER BY ID');
    PRINT '';
    PRINT N'ตั้งวงเงินรายห้อง: UPDATE dbo.Accommodation SET Security_Deposit_Amount = 2000 WHERE ID = <เลขห้อง>;';
    PRINT N'หรือแก้ผ่านหน้า Admin → จัดการฐานข้อมูล → Accommodation';
END
