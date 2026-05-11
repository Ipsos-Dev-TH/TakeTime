-- =============================================
-- PHASE 12 Migration 04: E-Tax automation config + Document Source view
-- รองรับ:
--   1. E-Tax auto-generate / auto-sign / auto-submit / auto-email
--   2. ค่าเริ่มต้นของ subject/body อีเมล + แนบ PDF/XML
--   3. View vw_Receipt_Document_Source — รวมข้อมูล Account_Receipt + sync status
--      ใช้สำหรับหน้าต่าง ๆ แสดงเลขเอกสาร NextAcc, สถานะ E-Tax, link PDF
-- =============================================

-- ──────────────────────────────────────────────
-- 1. E-Tax automation config keys
-- ──────────────────────────────────────────────

-- Auto-sign หลังจาก generate (ส่วนใหญ่ควรเปิดเพราะ unsigned ไม่มีค่าตามกฎหมาย)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_AutoSign')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Etax_AutoSign', '1', N'ลงนามใบกำกับภาษีอิเล็กทรอนิกส์อัตโนมัติด้วย Certificate ของ NextAcc (0/1)', GETDATE());
END

-- Auto-submit ส่งกรมสรรพากร (ควรเริ่มจาก 0 จนกว่าจะตรวจสอบกระบวนการแล้ว)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_AutoSubmit')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Etax_AutoSubmit', '0', N'ส่ง E-Tax ไปกรมสรรพากรอัตโนมัติหลังลงนาม (0/1)', GETDATE());
END

-- Auto-send email ให้ลูกค้า
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_AutoSendEmail')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Etax_AutoSendEmail', '0', N'ส่งอีเมล E-Tax ให้ลูกค้าโดยอัตโนมัติเมื่อสร้างเสร็จ (0/1)', GETDATE());
END

-- Email template - subject
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_EmailSubject')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Etax_EmailSubject', N'ใบกำกับภาษีอิเล็กทรอนิกส์ {ReceiptNumber}',
        N'หัวข้ออีเมลส่ง E-Tax (ตัวแปร: {ReceiptNumber}, {GuestName}, {Amount}, {Date})', GETDATE());
END

-- Email template - body
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_EmailBody')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Etax_EmailBody', N'เรียน {GuestName}\n\nกรุณาดาวน์โหลดใบกำกับภาษีอิเล็กทรอนิกส์ {ReceiptNumber} (ยอด {Amount} บาท ลงวันที่ {Date}) จากเอกสารแนบ\n\nขอบคุณที่ใช้บริการ',
        N'เนื้อหาอีเมลส่ง E-Tax (ตัวแปร: {ReceiptNumber}, {GuestName}, {Amount}, {Date})', GETDATE());
END

-- Attach PDF (default ON)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_EmailAttachPdf')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Etax_EmailAttachPdf', 'true', N'แนบ PDF ใน E-Tax email (true/false)', GETDATE());
END

-- Attach XML (default OFF, เปิดสำหรับลูกค้าธุรกิจ)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_EmailAttachXml')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES ('Etax_EmailAttachXml', 'false', N'แนบ XML ใน E-Tax email (true/false)', GETDATE());
END
GO

-- ──────────────────────────────────────────────
-- 2. View — รวมข้อมูลใบเสร็จกับสถานะ Sync และ E-Tax
-- ใช้สำหรับหน้าต่าง ๆ ที่ต้องการแสดง:
--   - เลขที่ใบเสร็จในระบบ TakeTime
--   - เลขเอกสารและสถานะใน NextAcc (ถ้ามี)
--   - สถานะ E-Tax และลิงก์ PDF/XML
-- ตอบโจทย์: เมื่อระบบเลือก DOCUMENT mode ผู้ใช้ยังเห็นข้อมูลครบในที่เดียว
-- ──────────────────────────────────────────────
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Receipt_Document_Source')
    DROP VIEW vw_Receipt_Document_Source;
GO

CREATE VIEW vw_Receipt_Document_Source AS
SELECT
    R.ID AS Receipt_ID,
    R.ID AS Receipt_Number,
    R.Reservation_ID,
    R.Total_Amount AS Total,
    R.IsDeposit,
    R.Status AS Receipt_Status,
    R.Created_Date,
    -- Sync ใน NextAcc
    Q.ID AS Sync_Queue_ID,
    Q.Status AS Sync_Status,
    Q.Action_Type,
    Q.Nexaacc_Response_Id AS Nexaacc_Doc_Id,
    Q.Error_Message AS Sync_Error,
    Q.Retry_Count,
    Q.Processed_Date AS Synced_Date,
    -- E-Tax tracking
    E.ID AS Etax_Log_ID,
    E.Nexaacc_Etax_Id,
    E.Etax_Ref_Number,
    E.Status AS Etax_Status,
    E.Signed_Date AS Etax_Signed_Date,
    E.Submitted_Date AS Etax_Submitted_Date,
    E.Pdf_Url AS Etax_Pdf_Url,
    E.Xml_Url AS Etax_Xml_Url,
    E.Email_Sent AS Etax_Email_Sent,
    E.Error_Message AS Etax_Error,
    -- Source indicator (LOCAL vs NEXAACC)
    CASE
        WHEN Q.Nexaacc_Response_Id IS NOT NULL
         AND Q.Nexaacc_Response_Id <> 'SKIPPED_LOCAL_MODE'
         AND Q.Status = 'COMPLETED'
        THEN 'NEXAACC'
        ELSE 'LOCAL'
    END AS Document_Source,
    -- Customer (resolve via Reservation since Account_Receipt may not have phone column)
    Res.Customer_MobilePhone,
    C.FullName AS Customer_FullName,
    C.Email AS Customer_Email,
    C.TaxID AS Customer_TaxID
FROM Account_Receipt R
LEFT JOIN Reservation Res ON Res.ID = R.Reservation_ID
LEFT JOIN Customer C ON C.MobilePhone = Res.Customer_MobilePhone
OUTER APPLY (
    SELECT TOP 1 ID, Status, Action_Type, Nexaacc_Response_Id, Error_Message, Retry_Count, Processed_Date
    FROM Accounting_Sync_Queue
    WHERE Entity_Type = 'RECEIPT'
      AND Entity_ID = R.ID
    ORDER BY ID DESC
) Q
OUTER APPLY (
    SELECT TOP 1 ID, Nexaacc_Etax_Id, Etax_Ref_Number, Status, Signed_Date, Submitted_Date,
                 Pdf_Url, Xml_Url, Email_Sent, Error_Message
    FROM Accounting_ETax_Log
    WHERE Receipt_Number = R.ID
    ORDER BY ID DESC
) E;
GO

PRINT 'Created view: vw_Receipt_Document_Source';
GO
