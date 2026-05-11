-- ══════════════════════════════════════════════════════════════
-- PHASE 12 — Migration 04a: Fix vw_Receipt_Document_Source
-- ══════════════════════════════════════════════════════════════
-- แก้ไข: Account_Receipt ใช้คอลัมน์ ID เป็นเลขที่ใบเสร็จ (เช่น "REC25050800001")
-- ไม่มีคอลัมน์ Receipt_Number — ต้องเปลี่ยน R.Receipt_Number เป็น R.ID
-- รัน migration นี้หลัง 04 เพื่อแก้ view ที่สร้างไปแล้ว
-- ══════════════════════════════════════════════════════════════

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
    Q.ID AS Sync_Queue_ID,
    Q.Status AS Sync_Status,
    Q.Action_Type,
    Q.Nexaacc_Response_Id AS Nexaacc_Doc_Id,
    Q.Error_Message AS Sync_Error,
    Q.Retry_Count,
    Q.Processed_Date AS Synced_Date,
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
    CASE
        WHEN Q.Nexaacc_Response_Id IS NOT NULL
         AND Q.Nexaacc_Response_Id <> 'SKIPPED_LOCAL_MODE'
         AND Q.Status = 'COMPLETED'
        THEN 'NEXAACC'
        ELSE 'LOCAL'
    END AS Document_Source,
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

PRINT 'Fixed view: vw_Receipt_Document_Source (R.Receipt_Number → R.ID)';
GO
