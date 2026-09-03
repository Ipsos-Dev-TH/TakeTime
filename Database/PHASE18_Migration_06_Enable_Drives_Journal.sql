-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 06: เปิด drives-journal mode (หักมัดจำใน JE เดียว self-contained)
-- ════════════════════════════════════════════════════════════════════════════
-- บริบท: เดิม (display-only, flags=0) การหักมัดจำตอนเช็คเอาท์แยกเป็น 2 JE:
--   JE#1 (ตัวเอกสาร Receipt): Dr เงินฝาก "เต็มยอด" (เช่น 1,450) / Cr รายได้ / Cr ภาษีขาย
--   JE#2 (ปรับมัดจำ แยกใบ):   Dr 21510 + Dr ภาษีขายมัดจำ / Cr เงินฝาก (เช่น 500)
-- → สุทธิเงินฝาก = 950 ถูกต้อง แต่ "ตัว JE ของเอกสารโชว์ Dr เงินฝาก 1,450" (การกลับมัดจำอยู่คนละใบ)
--   → ผู้ใช้เห็นแล้วสับสน ("รับ 950 แต่ JE เดบิต 1,450").
--
-- เปิด drives (flags=1) → NextAcc ลง "JE เดียว self-contained":
--   Dr เงินฝาก 950 (= รับจริง) + Dr 21510 (มัดจำ net) + Dr 21913 (ภาษีมัดจำ)
--   / Cr รายได้ / Cr ภาษีขาย  → Dr เงินฝากบนเอกสารตรงกับยอดที่รับจริง ไม่ต้องมี JV แยก.
--
-- เงื่อนไขเปิด (ครบแล้ว ก.ค. 2026): NextAcc deploy ครบ —
--   • journal-ref (cb55e3b: depositAppliedRef รับ Document REC- และ JournalEntry JV-INT)
--   • #1 un-realize on void (void เช็คเอาท์ → clear DepositAppliedToDocumentId → CREATE#2 re-realize ได้)
--   • base/VAT split ตามใบมัดจำจริง
-- → เปิดปลอดภัย GL บาลานซ์. มี safety-net ฝั่ง TakeTime (IsDrivesRelatedFailure) auto-fallback
--   ไป reverse-JE แยก ถ้า NextAcc endpoint มีปัญหา — ไม่ค้างคิว.
--
-- ตั้งทั้ง 2 flag = 1:
--   Nexaacc_Deposit_Drives_Journal = 1  → เปิด drives (มัดจำที่เป็น Document REC- ใช้ได้)
--   Nexaacc_Drives_Journal_Ref     = 1  → มัดจำที่เป็น JV-INT journal ก็ drives ได้ (cb55e3b)
--
-- ⚠ ต้อง deploy TakeTime build ล่าสุด (ที่มีโค้ด drives + safety-net) ก่อนรัน migration นี้.
-- idempotent — รันซ้ำได้. ถอยกลับ: ตั้งกลับเป็น '0' (display-only, GL ยังถูกผ่าน JV แยก).
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

-- Nexaacc_Deposit_Drives_Journal → 1
IF EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Deposit_Drives_Journal')
BEGIN
    UPDATE Accounting_Integration_Config SET ConfigValue = '1'
    WHERE ConfigKey = 'Nexaacc_Deposit_Drives_Journal';
    PRINT 'Set Nexaacc_Deposit_Drives_Journal = 1';
END
ELSE
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description) VALUES
    ('Nexaacc_Deposit_Drives_Journal', '1',
     N'1 = หักมัดจำใน JE เดียว self-contained (Dr เงินฝาก=รับจริง + Dr มัดจำ/ภาษี / Cr รายได้/ภาษี); 0 = display-only + JV ปรับมัดจำแยก');
    PRINT 'Inserted config Nexaacc_Deposit_Drives_Journal = 1';
END
GO

-- Nexaacc_Drives_Journal_Ref → 1 (รองรับมัดจำที่เป็น JV-INT journal ด้วย)
IF EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Drives_Journal_Ref')
BEGIN
    UPDATE Accounting_Integration_Config SET ConfigValue = '1'
    WHERE ConfigKey = 'Nexaacc_Drives_Journal_Ref';
    PRINT 'Set Nexaacc_Drives_Journal_Ref = 1';
END
ELSE
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description) VALUES
    ('Nexaacc_Drives_Journal_Ref', '1',
     N'1 = ส่ง JV-INT EntryNumber เป็น depositAppliedRef + drives (มัดจำ journal → self-contained JE, NextAcc cb55e3b); 0 = reverse-JE แยก');
    PRINT 'Inserted config Nexaacc_Drives_Journal_Ref = 1';
END
GO

SELECT ConfigKey, ConfigValue, Description
FROM Accounting_Integration_Config
WHERE ConfigKey IN ('Nexaacc_Deposit_Drives_Journal', 'Nexaacc_Drives_Journal_Ref');
GO
