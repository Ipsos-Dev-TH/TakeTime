-- =============================================
-- PHASE 12 Migration 05: E-Tax email fallback config
-- รองรับการส่งอีเมล E-Tax ผ่าน SMTP ของ TakeTime เมื่อ NextAcc API ส่งไม่สำเร็จ
-- =============================================

-- Etax_EmailFallback — ถ้า NextAcc ส่งอีเมลไม่สำเร็จ ให้ fallback ส่งผ่าน SMTP ของ TakeTime
-- (ดาวน์โหลด PDF/XML จาก NextAcc มาแนบเอง)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_EmailFallback')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Etax_EmailFallback', 'true',
        N'ถ้า NextAcc ส่งอีเมล E-Tax ไม่สำเร็จ ให้ส่งผ่าน SMTP ของ TakeTime (true/false)',
        GETDATE());
END

-- Etax_EmailLocalOnly — บังคับใช้ SMTP ของ TakeTime เท่านั้น (ข้าม NextAcc)
-- ใช้กรณี NextAcc email service ปิดอยู่ หรือไม่ต้องการให้ NextAcc ส่งอีเมล
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_EmailLocalOnly')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Etax_EmailLocalOnly', 'false',
        N'บังคับใช้ SMTP ของ TakeTime ส่งอีเมล E-Tax เท่านั้น ข้าม NextAcc (true/false)',
        GETDATE());
END
GO
