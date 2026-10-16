-- ============================================================================
-- ลบใบสำคัญจ่ายเงินเดือนที่สร้างผิด (ออกมาก่อนแก้ code โหมด NextAcc ออกเอกสาร)
-- ลบเฉพาะตัวใบสำคัญจ่าย — *ไม่แตะ* สถานะ "ทำจ่าย" ใน Payroll_Records
-- รันบน SQL Server ของ TakeTime (สำรองข้อมูลก่อนรันเสมอ)
-- ============================================================================

BEGIN TRANSACTION;

-- ตรวจก่อนลบ: ดูว่าจะลบใบไหนบ้าง (ควรเห็น 6 แถว PAY260630001..006)
SELECT ID, Vendor_ID, Total_Amount, Paid_Type, Status, Created_Date
FROM   Account_Payment
WHERE  ID IN ('PAY260630001','PAY260630002','PAY260630003',
              'PAY260630004','PAY260630005','PAY260630006');

-- 1) ลบรายละเอียดบรรทัดของใบ
DELETE FROM Account_Payment_Detail
WHERE  Payment_ID IN ('PAY260630001','PAY260630002','PAY260630003',
                      'PAY260630004','PAY260630005','PAY260630006');

-- 2) ลบตัวใบสำคัญจ่าย
DELETE FROM Account_Payment
WHERE  ID IN ('PAY260630001','PAY260630002','PAY260630003',
              'PAY260630004','PAY260630005','PAY260630006');

-- หมายเหตุ: ไม่แตะ Payroll_Records (พนักงานยังคงสถานะ "จ่ายแล้ว" ตามเดิม)

-- ถ้าผลถูกต้อง → COMMIT ; ถ้าไม่ → ROLLBACK
COMMIT TRANSACTION;
-- ROLLBACK TRANSACTION;
