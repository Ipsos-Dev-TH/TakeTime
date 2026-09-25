-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 04: Drives-journal สำหรับมัดจำที่เป็น JV-INT journal (NextAcc cb55e3b)
-- ════════════════════════════════════════════════════════════════════════════
-- มัดจำที่ sync มาเป็น "integration journal" (เลข JV-INT-...) ไม่ใช่ Receipt document (REC-) —
-- drives-journal เดิมของ NextAcc resolve depositAppliedRef ได้แค่ Document → JV-INT หาไม่เจอ.
--
-- NextAcc cb55e3b: เพิ่มการ resolve depositAppliedRef เป็น "JournalEntry.EntryNumber" (JV-INT) →
-- กลับ deferred (217xx/21913) จากบรรทัด Cr ของ journal นั้นเอง ใน JE ของใบเดียว (self-contained).
--
-- เมื่อเปิด Nexaacc_Drives_Journal_Ref = 1 → TakeTime ส่ง depositAppliedRef = JV-INT EntryNumber
-- + drives + เลิกส่ง reverse-JE แยก (กัน double-reverse).
--
-- ⚠ เปิดได้ "เมื่อ NextAcc deploy cb55e3b แล้วเท่านั้น" — ถ้าเปิดก่อน เอกสารจะค้าง draft
-- (approve ไม่ผ่าน เพราะ NextAcc รุ่นเก่า resolve JV-INT ref ไม่ได้). มี safety-net auto-fallback
-- (ปิด drives → reverse-JE แยก → approve ผ่าน) แต่ควรเปิด flag ตามลำดับให้ถูก.
--
-- default = 0 (ปิด) → มัดจำ JV-INT ใช้ reverse-JE แยก (ปลอดภัย, GL ถูก). idempotent — รันซ้ำได้.
-- ต้องใช้คู่กับ Nexaacc_Deposit_Drives_Journal = 1.
--
-- ลำดับเปิดใช้:
--   1. NextAcc pull cb55e3b → rebuild → deploy
--   2. TakeTime rebuild + deploy
--   3. ตั้ง Nexaacc_Deposit_Drives_Journal = 1 (ถ้ายังไม่เปิด) + Nexaacc_Drives_Journal_Ref = 1
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Drives_Journal_Ref')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description) VALUES
    ('Nexaacc_Drives_Journal_Ref', '0',
     N'1 = ส่ง JV-INT EntryNumber เป็น depositAppliedRef + drives (มัดจำ journal → self-contained JE, NextAcc cb55e3b); 0 = reverse-JE แยก. เปิดเมื่อ NextAcc deploy cb55e3b แล้วเท่านั้น');
    PRINT 'Inserted config Nexaacc_Drives_Journal_Ref = 0';
END
ELSE
    PRINT 'Config Nexaacc_Drives_Journal_Ref already exists';
GO

SELECT ConfigKey, ConfigValue, Description
FROM Accounting_Integration_Config
WHERE ConfigKey IN ('Nexaacc_Deposit_Drives_Journal', 'Nexaacc_Drives_Journal_Ref');
GO
