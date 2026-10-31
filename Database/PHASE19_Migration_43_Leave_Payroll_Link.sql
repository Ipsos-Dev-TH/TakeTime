-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 43: ผูกใบลาหักเงินกับรอบเงินเดือนที่หัก (PayrollLeaveSync)
-- ════════════════════════════════════════════════════════════════════════════
-- ปัญหา: ยอดหักลาคิดเฉพาะตอน "สร้างรอบ" / "คำนวณใหม่" ⇒ ใบลาที่อนุมัติหลังสร้างรอบไม่ถูกหัก
--        และถ้ารอบเดือนของใบลาทำจ่ายไปแล้ว จะไม่ถูกหักในรอบไหนเลย (เคส LR-260830-18-50 ลา 27/08 ยื่น 30/08)
--
-- 1) Leave_Requests.Payroll_Record_ID BIGINT NULL = รอบ (Payroll_Records.ID) ที่หักใบนี้ไปแล้ว
--      NULL = ยังไม่มีรอบไหนหัก · -1 = ใบเก่าก่อน migration (ไม่ยกมาหัก)
--    ใบที่ยังไม่ถูกหักและรอบเดือนของมันทำจ่ายแล้ว → ระบบยกไปหักในรอบถัดไปที่ยังไม่ทำจ่าย (ย้อนได้ 6 เดือน)
-- 2) Backfill (กันหักซ้ำ):
--      ก) ใบหักเงินที่เริ่มก่อน 6 เดือนที่แล้ว → -1
--      ข) ใบที่เหลือ: ผูกกับรอบของเดือนตัวเองตามลำดับวันอนุมัติ จนครบจำนวน LeaveDays ที่รอบนั้นบันทึกไว้
--         ⇒ ใบที่เกินจำนวน (อนุมัติหลังรอบถูกคำนวณ) ยังว่าง = จะถูกหักในรอบที่ยังไม่ทำจ่าย
-- 3) รายงานท้ายไฟล์: ใบที่จะถูกยกไปหักรอบถัดไป — ให้ HR/เจ้าของตรวจก่อนทำจ่ายรอบหน้า
--
-- ยังไม่รัน = หักเฉพาะใบของเดือนนั้น (อนุมัติแล้วคำนวณรอบที่ยังไม่ทำจ่ายให้ทันทีอยู่แล้ว) + หน้าเงินเดือนเตือนรอบที่จ่ายไปแล้ว
-- idempotent รันซ้ำได้ (backfill แตะเฉพาะแถวที่ยังว่าง) · รันแล้ว recycle app pool (cache schema 60 วินาที)
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.Leave_Requests', 'Payroll_Record_ID') IS NULL
BEGIN
    ALTER TABLE dbo.Leave_Requests ADD Payroll_Record_ID BIGINT NULL;
    PRINT 'Added Leave_Requests.Payroll_Record_ID';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Leave_Requests_Payroll_Record' AND object_id = OBJECT_ID('dbo.Leave_Requests'))
    CREATE INDEX IX_Leave_Requests_Payroll_Record ON dbo.Leave_Requests (Payroll_Record_ID) INCLUDE (Admin_ID, StartDate, Status);
GO

-- 2ก) ใบเก่า: ไม่ยกมาหัก
DECLARE @Cutoff DATE = DATEADD(MONTH, -6, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1));
UPDATE dbo.Leave_Requests
SET Payroll_Record_ID = -1
WHERE Payroll_Record_ID IS NULL
  AND ISNULL(DeductSalary, 0) = 1
  AND StartDate < @Cutoff;
PRINT CONCAT('Legacy (-1): ', @@ROWCOUNT);
GO

-- 2ข) ผูกกับรอบของเดือนตัวเองเท่าจำนวนวันลาที่รอบบันทึกไว้
;WITH L AS (
    SELECT LR.ID, PR.ID AS RecID, ISNULL(PR.LeaveDays, 0) AS RecDays,
           SUM(LR.TotalDays) OVER (PARTITION BY PR.ID
                                   ORDER BY ISNULL(LR.ApprovedDate, LR.CreatedDate), LR.ID
                                   ROWS UNBOUNDED PRECEDING) AS CumDays
    FROM dbo.Leave_Requests LR
    INNER JOIN dbo.Payroll_Periods PP ON PP.[Year] = YEAR(LR.StartDate) AND PP.[Month] = MONTH(LR.StartDate)
    INNER JOIN dbo.Payroll_Records PR ON PR.PayrollPeriod_ID = PP.ID AND PR.Admin_ID = LR.Admin_ID
    WHERE LR.Payroll_Record_ID IS NULL
      AND LR.Status = 'APPROVED'
      AND ISNULL(LR.DeductSalary, 0) = 1
      AND ISNULL(LR.IsReplaced, 0) = 0
)
UPDATE LR
SET Payroll_Record_ID = L.RecID
FROM dbo.Leave_Requests LR
INNER JOIN L ON L.ID = LR.ID
WHERE L.CumDays <= L.RecDays + 0.001;
PRINT CONCAT('Linked to own-month payroll: ', @@ROWCOUNT);
GO

-- 3) ใบที่ยังไม่ถูกหักและรอบเดือนตัวเองทำจ่ายแล้ว → จะยกไปหักรอบถัดไปที่ยังไม่ทำจ่าย
SELECT LR.RequestNumber, ISNULL(A.FirstName + ' ' + A.LastName, A.Username) AS Employee,
       LR.StartDate, LR.TotalDays, LR.DeductionAmount, LR.ApprovedDate,
       PR.ID AS PaidRecordId, PR.VoucherNumber,
       N'จะยกไปหักรอบถัดไปที่ยังไม่ทำจ่าย' AS Action
FROM dbo.Leave_Requests LR
INNER JOIN dbo.Admin A ON A.ID = LR.Admin_ID
INNER JOIN dbo.Payroll_Periods PP ON PP.[Year] = YEAR(LR.StartDate) AND PP.[Month] = MONTH(LR.StartDate)
INNER JOIN dbo.Payroll_Records PR ON PR.PayrollPeriod_ID = PP.ID AND PR.Admin_ID = LR.Admin_ID
WHERE LR.Payroll_Record_ID IS NULL
  AND LR.Status = 'APPROVED'
  AND ISNULL(LR.DeductSalary, 0) = 1
  AND ISNULL(LR.IsReplaced, 0) = 0
  AND ISNULL(PR.VoucherGenerated, 0) = 1
ORDER BY LR.StartDate;
GO
