-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 07: Auto-recover มัดจำ legacy ที่ถูก reverse ค้าง (opt-in)
-- ════════════════════════════════════════════════════════════════════════════
-- ปัญหา: booking เก่าที่รับมัดจำสมัย drives ปิด → เช็คเอาท์กลับมัดจำด้วย TryReverse (reverse JE มัดจำ
-- แยก) → แล้ว void+sync เอกสารใหม่หลายรอบ. พอเปิด drives ทีหลัง → drives ส่ง depositAppliedRef=JV-INT-…
-- แต่ NextAcc หา journal ไม่เจอ ("ถูก consume/reverse แล้ว") → drives ล้มเหลว วนซ้ำ (churn) + เสี่ยง
-- กลับมัดจำซ้ำ (21510 ติดลบ).
--
-- guard เดิม (commit a77f72f) กัน churn/double-reverse แล้ว (ตรวจเจอ reverse → ไม่ drives/ไม่กลับซ้ำ,
-- book Dr เงินสดเต็ม, net ถูก) แต่ได้ 2 JE (doc โชว์ Dr เต็ม).
--
-- flag นี้ (opt-in) เปิด AUTO-RECOVER: ตอนเช็คเอาท์ถ้ามัดจำถูก reverse ค้าง → "un-reverse" (กลับตัว
-- reversal) คืนหนี้สินมัดจำ 21510 ให้ active อีกครั้ง → drives กลับมัดจำใน JE เดียว (single-JE, Dr เงินสด
-- สุทธิ) ได้. idempotent: reversal ถูกกลับ/void แล้ว → ข้าม; หา reversal entry ไม่เจอ → ไม่ทำ (กัน double).
-- GL: deposit + reversal(เดิม) + un-reversal = deposit เดี่ยว (reversal/un-reversal หักล้าง) → 21510 คืน 500,
-- แล้ว drives หัก → 0. balanced.
--
--   Nexaacc_Auto_Recover_Deposit = 0 (default, ปิด) → ใช้ guard เดิม (2 JE, net ถูก, ปลอดภัยสุด)
--                                = 1 (เปิด)        → un-reverse + drives = single-JE
--
-- ⚠ เปิดเมื่อ: (1) deploy build ล่าสุด (2) drives เปิดแล้ว (PHASE18_06) (3) test + ตรวจ GL 1-2 ใบก่อน
-- ⚠ booking ที่ churn หนัก (มี adjustment ซ้อนหลายตัว) ควรตรวจ GL ด้วยตา — un-reverse คืนเฉพาะ JE มัดจำ,
--    ไม่แตะ adjustment ค้างอื่นที่อาจเกิดจาก void ที่ไม่สมบูรณ์.
-- idempotent — รันซ้ำได้.
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Auto_Recover_Deposit')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description) VALUES
    ('Nexaacc_Auto_Recover_Deposit', '0',
     N'1 = un-reverse ใบมัดจำ legacy ที่ถูก reverse ค้าง → drives ทำ single-JE ได้; 0 = guard เดิม (2 JE, net ถูก). opt-in, ต้อง test ก่อนเปิด');
    PRINT 'Inserted config Nexaacc_Auto_Recover_Deposit = 0';
END
ELSE
    PRINT 'Config Nexaacc_Auto_Recover_Deposit already exists';
GO

SELECT ConfigKey, ConfigValue, Description
FROM Accounting_Integration_Config
WHERE ConfigKey = 'Nexaacc_Auto_Recover_Deposit';
GO
