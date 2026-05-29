-- ============================================================
-- PHASE12 Migration 14: Add document cache columns to sync queue
-- Stores NextAcc document number for local display & deep links
-- ============================================================

-- Add document number cache column
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Accounting_Sync_Queue') AND name = 'Nexaacc_Document_Number')
BEGIN
    ALTER TABLE Accounting_Sync_Queue ADD Nexaacc_Document_Number NVARCHAR(50) NULL;
    PRINT 'Added Nexaacc_Document_Number column';
END
GO

-- Add document type column (INVOICE, EXPENSE, JOURNAL, CREDIT_NOTE, DEBIT_NOTE, PAYMENT)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Accounting_Sync_Queue') AND name = 'Nexaacc_Document_Type')
BEGIN
    ALTER TABLE Accounting_Sync_Queue ADD Nexaacc_Document_Type NVARCHAR(30) NULL;
    PRINT 'Added Nexaacc_Document_Type column';
END
GO

-- Create view for document cross-reference (TakeTime ↔ NextAcc)
IF OBJECT_ID('vw_Accounting_Document_Xref', 'V') IS NOT NULL
    DROP VIEW vw_Accounting_Document_Xref;
GO

CREATE VIEW vw_Accounting_Document_Xref AS
SELECT
    q.ID AS QueueId,
    q.Entity_Type,
    q.Entity_ID,
    q.Action_Type,
    q.Status AS SyncStatus,
    q.Nexaacc_Response_Id,
    q.Nexaacc_Document_Number,
    q.Nexaacc_Document_Type,
    q.Created_Date AS QueuedDate,
    q.Processed_Date AS SyncedDate,
    q.Error_Message,
    q.Retry_Count,
    -- Extract TakeTime document number from JSON payload
    CASE
        WHEN q.Action_Type IN ('CREATE_VOUCHER_JOURNAL','VOID_VOUCHER')
            THEN JSON_VALUE(q.Payload, '$.documentNumber')
        WHEN q.Action_Type IN ('CREATE_RECEIPT_DOCUMENT','VOID_RECEIPT')
            THEN JSON_VALUE(q.Payload, '$.receiptNumber')
        WHEN q.Action_Type = 'CREATE_PAYROLL_ENTRY'
            THEN JSON_VALUE(q.Payload, '$.documentNumber')
        ELSE NULL
    END AS TakeTime_Document_Number,
    -- Extract description from payload
    CASE
        WHEN q.Action_Type = 'CREATE_VOUCHER_JOURNAL'
            THEN JSON_VALUE(q.Payload, '$.description')
        WHEN q.Action_Type = 'CREATE_RECEIPT_DOCUMENT'
            THEN JSON_VALUE(q.Payload, '$.description')
        ELSE q.Action_Type
    END AS Description,
    -- Extract amount from payload
    CASE
        WHEN q.Action_Type IN ('CREATE_VOUCHER_JOURNAL','CREATE_RECEIPT_DOCUMENT','CREATE_PAYROLL_ENTRY')
            THEN TRY_CAST(JSON_VALUE(q.Payload, '$.amount') AS DECIMAL(18,2))
        WHEN q.Action_Type = 'CREATE_RECEIPT_DOCUMENT'
            THEN TRY_CAST(JSON_VALUE(q.Payload, '$.totalAmount') AS DECIMAL(18,2))
        ELSE NULL
    END AS Amount
FROM Accounting_Sync_Queue q
WHERE q.Status = 'COMPLETED'
  AND q.Nexaacc_Response_Id IS NOT NULL
  AND q.Nexaacc_Response_Id NOT LIKE 'SKIPPED%';
GO

PRINT 'Created vw_Accounting_Document_Xref view';
GO

-- Index for faster document lookups by NextAcc document number
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SyncQueue_NexaaccDocNum' AND object_id = OBJECT_ID('Accounting_Sync_Queue'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SyncQueue_NexaaccDocNum
    ON Accounting_Sync_Queue (Nexaacc_Document_Number)
    WHERE Nexaacc_Document_Number IS NOT NULL;
    PRINT 'Created IX_SyncQueue_NexaaccDocNum index';
END
GO
