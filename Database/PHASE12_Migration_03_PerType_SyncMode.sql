-- =============================================
-- PHASE 12 Migration 03: Per-Document-Type Sync Mode & File Attachment Config
-- รองรับการตั้งค่าโหมด Sync แยกตามประเภทเอกสาร (ใบเสร็จ/ใบสำคัญจ่าย/เงินเดือน)
-- และเปิด/ปิดการแนบไฟล์อัตโนมัติ
-- =============================================

-- Per-type sync mode: Receipt (ใบเสร็จ/ใบกำกับภาษี)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_SyncMode_Receipt')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Nexaacc_SyncMode_Receipt', '', N'โหมด Sync สำหรับใบเสร็จ/ใบกำกับภาษี (LOCAL/JOURNAL_ONLY/DOCUMENT, ว่าง=ใช้ค่าเริ่มต้น)', GETDATE());
END

-- Per-type sync mode: Voucher (ใบสำคัญจ่าย)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_SyncMode_Voucher')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Nexaacc_SyncMode_Voucher', '', N'โหมด Sync สำหรับใบสำคัญจ่าย (LOCAL/JOURNAL_ONLY/DOCUMENT, ว่าง=ใช้ค่าเริ่มต้น)', GETDATE());
END

-- Per-type sync mode: Payroll (เงินเดือน)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_SyncMode_Payroll')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Nexaacc_SyncMode_Payroll', '', N'โหมด Sync สำหรับเงินเดือน (LOCAL/JOURNAL_ONLY/DOCUMENT, ว่าง=ใช้ค่าเริ่มต้น)', GETDATE());
END

-- File attachment toggle
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_AttachFiles')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Nexaacc_AttachFiles', 'true', N'แนบไฟล์เอกสาร (ใบเสร็จ/สลิป/ภาพใบสำคัญจ่าย) ไปกับ API (true/false)', GETDATE());
END
GO
