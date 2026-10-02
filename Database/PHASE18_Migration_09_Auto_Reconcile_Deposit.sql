-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 09: Auto-reconcile บัญชีมัดจำ 21510 ที่ติดลบจาก adjustment ค้าง (opt-in)
-- ════════════════════════════════════════════════════════════════════════════
-- ปัญหา: booking ที่ churn ช่วง dev (เช็คเอาท์ drives fail หลายรอบ → void ไม่สมบูรณ์) ทิ้ง "deposit
-- adjustment" (JE ref ลงท้าย -DEPADJ, Dr 21510 / Cr เงินสด) ค้างบน NextAcc → 21510 ติดลบ (over-debit).
-- post-sync verify จับได้ ("บัญชีมัดจำ 21510 ยอดติดลบ") แต่เดิมต้องเคลียร์มือ.
--
-- flag นี้ (opt-in) เปิด FINAL GATE: หลัง sync ถ้า verify เจอ WARN + เช็คเอาท์รอบนี้ใช้ drives สำเร็จ
-- (การหักมัดจำอยู่ใน JE เดียว → -DEPADJ แยกทุกตัว = orphaned แน่นอน) → reverse -DEPADJ ที่ค้าง
-- "เท่าที่จำเป็น" (self-limiting: หยุดเมื่อ net 21510 กลับ ~0 ไม่ over-correct) → re-verify ผลจริง
-- → บันทึกสถานะสุดท้าย (check → correct → recheck).
--
-- ความปลอดภัย:
--   • reverse เฉพาะ JE ref ลงท้าย "-DEPADJ" (ไม่ใช่ -DEPADJ-REV) ที่ยังไม่ถูก reverse + มี Dr บน 21510 จริง
--   • ทำเฉพาะโหมด drives (ไม่งั้น -DEPADJ ของรอบปัจจุบันอาจ legit)
--   • self-limiting หยุดเมื่อ net กลับ ~0 (ไม่ over-correct)
--   • re-verify ผลจริงหลังทำ; ถ้ายังไม่ 0 (มีสาเหตุอื่นนอก -DEPADJ) → คง WARN + แจ้งตรวจมือ
--   • ทุก movement เป็น JE reversal จริงบน NextAcc (audit trail ครบ ไม่ลบ/ซ่อน)
--
--   Nexaacc_Auto_Reconcile_Deposit = 0 (default, ปิด) → verify แจ้งเตือน, เคลียร์มือ
--                                   = 1 (เปิด)        → auto-reconcile + recheck
--
-- ⚠ เปิดเมื่อ: (1) deploy build ล่าสุด (2) drives + auto-recover เปิดแล้ว (3) test + ตรวจ GL 1-2 ใบก่อน
-- idempotent — รันซ้ำได้.
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Auto_Reconcile_Deposit')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description) VALUES
    ('Nexaacc_Auto_Reconcile_Deposit', '0',
     N'1 = auto-reverse orphaned -DEPADJ ที่ทำให้ 21510 ติดลบ (โหมด drives, self-limiting, re-verify); 0 = แจ้งเตือนเคลียร์มือ. opt-in');
    PRINT 'Inserted config Nexaacc_Auto_Reconcile_Deposit = 0';
END
ELSE
    PRINT 'Config Nexaacc_Auto_Reconcile_Deposit already exists';
GO

SELECT ConfigKey, ConfigValue, Description
FROM Accounting_Integration_Config
WHERE ConfigKey = 'Nexaacc_Auto_Reconcile_Deposit';
GO
