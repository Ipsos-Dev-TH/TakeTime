-- ============================================================================
-- PHASE19 Migration 14 — ใบรับรองแพทย์: กำหนดได้ว่าต้องใช้เมื่อลาเกินกี่วัน
--
-- เดิม RequiresMedicalCert เป็น BIT ใช่/ไม่ใช่ล้วน ๆ และเมื่อ "ต้องใช้แต่ไม่แนบ"
-- ระบบจะ **ปฏิเสธคำขอทิ้ง** ไปเลย ⇒ ตั้งกฎแบบที่ใช้จริงไม่ได้:
--   "ลาป่วย 1 วันไม่ต้องมีใบรับรอง · 2 วันขึ้นไปต้องมีถึงจะไม่หักเงิน · ไม่มีก็หักหมด"
--
-- เพิ่ม 2 คอลัมน์:
--   MedicalCertAfterDays  ต้องใช้ใบรับรองเมื่อลา "เกิน" กี่วัน
--                         NULL = ต้องใช้ทุกกรณี (พฤติกรรมเดิม)
--                         1    = ลา 1 วันไม่ต้องใช้, 2 วันขึ้นไปต้องใช้
--   NoCertAction          ไม่แนบใบรับรองทั้งที่ต้องใช้ แล้วยังไง
--                         'BLOCK'  = ปฏิเสธคำขอ (พฤติกรรมเดิม — ค่าเริ่มต้น)
--                         'DEDUCT' = รับคำขอไว้ แต่หักเงินทั้งจำนวนวันที่ลา
--
-- ค่าเริ่มต้นของคอลัมน์ = พฤติกรรมเดิมทุกประการ ยกเว้น "ลาป่วย" (SICK) ที่ตั้งตาม
-- กฎที่ผู้ใช้ต้องการให้เลย (เกิน 1 วันต้องมีใบรับรอง / ไม่มี = หักเงิน)
--
-- รันซ้ำได้ · ไม่ลบข้อมูลเดิม
-- ============================================================================

SET NOCOUNT ON;

IF OBJECT_ID('dbo.Leave_Types', 'U') IS NULL
BEGIN
    PRINT N'⚠ ยังไม่มีตาราง Leave_Types — รัน 12_Payroll_Leave_System.sql ก่อน';
    RETURN;
END
GO

-- ── 1) เกินกี่วันถึงต้องใช้ใบรับรองแพทย์ ────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Leave_Types') AND name = 'MedicalCertAfterDays')
BEGIN
    ALTER TABLE dbo.Leave_Types ADD MedicalCertAfterDays DECIMAL(5,2) NULL;
    PRINT N'เพิ่มคอลัมน์ MedicalCertAfterDays (NULL = ต้องใช้ทุกกรณี เหมือนเดิม)';
END
ELSE PRINT N'มีคอลัมน์ MedicalCertAfterDays อยู่แล้ว — ข้าม';
GO

-- ── 2) ไม่แนบใบรับรองแล้วยังไง ──────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Leave_Types') AND name = 'NoCertAction')
BEGIN
    ALTER TABLE dbo.Leave_Types
        ADD NoCertAction VARCHAR(10) NOT NULL
            CONSTRAINT DF_LeaveTypes_NoCertAction DEFAULT 'BLOCK';
    PRINT N'เพิ่มคอลัมน์ NoCertAction (BLOCK = ปฏิเสธคำขอ เหมือนเดิม)';
END
ELSE PRINT N'มีคอลัมน์ NoCertAction อยู่แล้ว — ข้าม';
GO

-- ── 3) ตั้งกฎลาป่วยตามที่ต้องการ ────────────────────────────────────────────
-- ลา 1 วัน  → ไม่ต้องใช้ใบรับรอง ไม่หักเงิน
-- ลา 2 วันขึ้นไป → ต้องมีใบรับรอง ถึงจะไม่หักเงิน
-- ไม่มีใบรับรอง → หักเงินทั้งจำนวนวันที่ลา (ไม่ปฏิเสธคำขอ)
UPDATE dbo.Leave_Types
   SET MedicalCertAfterDays = 1,
       NoCertAction = 'DEDUCT',
       RequiresMedicalCert = 1
 WHERE LeaveTypeCode = 'SICK';

PRINT N'ตั้งกฎลาป่วย: เกิน 1 วันต้องมีใบรับรอง · ไม่มี = หักเงิน (' + CAST(@@ROWCOUNT AS VARCHAR(10)) + N' แถว)';
GO

SELECT LeaveTypeCode AS [รหัส],
       LeaveTypeName AS [ชื่อ],
       CASE WHEN RequiresMedicalCert = 1
            THEN CASE WHEN MedicalCertAfterDays IS NULL
                      THEN N'ต้องใช้ทุกกรณี'
                      ELSE N'ต้องใช้เมื่อลาเกิน ' + CAST(CAST(MedicalCertAfterDays AS DECIMAL(5,1)) AS NVARCHAR(10)) + N' วัน'
                 END
            ELSE N'ไม่ต้องใช้'
       END AS [ใบรับรองแพทย์],
       CASE NoCertAction WHEN 'DEDUCT' THEN N'ไม่มีใบรับรอง = หักเงิน'
                         ELSE N'ไม่มีใบรับรอง = ปฏิเสธคำขอ' END AS [ถ้าไม่แนบ],
       CASE WHEN DeductSalary = 1 THEN N'หักเสมอ' ELSE N'ไม่หัก' END AS [หักเงินพื้นฐาน]
  FROM dbo.Leave_Types
 ORDER BY DisplayOrder, LeaveTypeName;

PRINT '';
PRINT N'แก้กฎได้จากหน้า Admin → ระบบจัดการการลา → แท็บ "ตั้งค่าประเภทการลา"';
GO
