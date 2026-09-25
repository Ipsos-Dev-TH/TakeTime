-- ============================================================================
-- PHASE18 Migration 32 — เก็บ "การจับคู่ ใบเสร็จ ↔ เอกสาร NextAcc" ไว้บนใบเสร็จ
-- ============================================================================
-- ปัญหาเดิม (ต้นเหตุของอาการที่ไล่แก้กันหลายรอบ):
--   หน้าเอกสารหาคู่ของใบเสร็จจาก Accounting_Sync_Queue.Nexaacc_Response_Id
--   ของ "คิว CREATE ล่าสุด" ซึ่งเป็นตารางบันทึกงาน ไม่ใช่ที่เก็บข้อเท็จจริง:
--     - void แล้วสร้างใหม่หลายรอบ  -> ตัวชี้ไปค้างที่เอกสารที่ไม่มีแล้ว
--     - ลบเอกสารทิ้งบน NextAcc     -> จับคู่ไม่ติด ใบกลายเป็น NextAcc-only
--     - ล้าง/แก้คิว                -> การจับคู่หายไปด้วย
--   ผลคือปุ่มแก้ไขหาย เลขเอกสารหาย และต้องมีเครื่องมือ relink มาตามแก้
--
-- ทางแก้: ย้ายการจับคู่มาอยู่บน Account_Receipt โดยตรง (ข้อเท็จจริงของใบนั้น)
--   Nexaacc_Doc_Id      = GUID เอกสารบน NextAcc
--   Nexaacc_Doc_Number  = เลขเอกสาร (เช่น REC-20260809-0004)
--   Nexaacc_Doc_LinkedAt= เวลาที่ผูก (ไว้ตรวจย้อนหลัง)
-- คิวยังเก็บ Nexaacc_Response_Id ไว้เหมือนเดิมสำหรับประวัติ/ของเก่า
--
-- ปลอดภัย: รันซ้ำได้ ไม่ลบข้อมูล ไม่แตะ GL
-- ============================================================================

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Account_Receipt' AND COLUMN_NAME = 'Nexaacc_Doc_Id')
BEGIN
    ALTER TABLE Account_Receipt ADD Nexaacc_Doc_Id UNIQUEIDENTIFIER NULL;
    PRINT 'เพิ่มคอลัมน์ Account_Receipt.Nexaacc_Doc_Id';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Account_Receipt' AND COLUMN_NAME = 'Nexaacc_Doc_Number')
BEGIN
    ALTER TABLE Account_Receipt ADD Nexaacc_Doc_Number NVARCHAR(50) NULL;
    PRINT 'เพิ่มคอลัมน์ Account_Receipt.Nexaacc_Doc_Number';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Account_Receipt' AND COLUMN_NAME = 'Nexaacc_Doc_LinkedAt')
BEGIN
    ALTER TABLE Account_Receipt ADD Nexaacc_Doc_LinkedAt DATETIME NULL;
    PRINT 'เพิ่มคอลัมน์ Account_Receipt.Nexaacc_Doc_LinkedAt';
END
GO

-- ── Backfill จากคิว: เอาคิว CREATE ที่ COMPLETED ล่าสุดของแต่ละใบ ───────────
;WITH q AS (
    SELECT
        REPLACE(SUBSTRING(CAST(Payload AS NVARCHAR(MAX)),
                CHARINDEX('"receiptNumber":"', CAST(Payload AS NVARCHAR(MAX))) + 17,
                CHARINDEX('"', CAST(Payload AS NVARCHAR(MAX)), CHARINDEX('"receiptNumber":"', CAST(Payload AS NVARCHAR(MAX))) + 17)
                    - (CHARINDEX('"receiptNumber":"', CAST(Payload AS NVARCHAR(MAX))) + 17)), '', '') AS ReceiptNo,
        Nexaacc_Response_Id, Nexaacc_Document_Number,
        ROW_NUMBER() OVER (
            PARTITION BY SUBSTRING(CAST(Payload AS NVARCHAR(MAX)),
                CHARINDEX('"receiptNumber":"', CAST(Payload AS NVARCHAR(MAX))) + 17,
                CHARINDEX('"', CAST(Payload AS NVARCHAR(MAX)), CHARINDEX('"receiptNumber":"', CAST(Payload AS NVARCHAR(MAX))) + 17)
                    - (CHARINDEX('"receiptNumber":"', CAST(Payload AS NVARCHAR(MAX))) + 17))
            ORDER BY ID DESC) AS rn
    FROM Accounting_Sync_Queue
    WHERE Entity_Type = 'RECEIPT'
      AND Action_Type = 'CREATE_RECEIPT_DOCUMENT'
      AND Status = 'COMPLETED'
      AND Nexaacc_Response_Id IS NOT NULL
      AND CHARINDEX('"receiptNumber":"', CAST(Payload AS NVARCHAR(MAX))) > 0
)
UPDATE ar
SET ar.Nexaacc_Doc_Id = TRY_CAST(q.Nexaacc_Response_Id AS UNIQUEIDENTIFIER),
    ar.Nexaacc_Doc_Number = q.Nexaacc_Document_Number,
    ar.Nexaacc_Doc_LinkedAt = GETDATE()
FROM Account_Receipt ar
JOIN q ON q.ReceiptNo = ar.ID AND q.rn = 1
WHERE ar.Nexaacc_Doc_Id IS NULL
  AND TRY_CAST(q.Nexaacc_Response_Id AS UNIQUEIDENTIFIER) IS NOT NULL;

PRINT 'Backfill การจับคู่จากคิว: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' ใบ';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccountReceipt_NexaaccDoc'
               AND object_id = OBJECT_ID('Account_Receipt'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AccountReceipt_NexaaccDoc
        ON Account_Receipt (Nexaacc_Doc_Id) INCLUDE (Nexaacc_Doc_Number);
    PRINT 'สร้าง IX_AccountReceipt_NexaaccDoc';
END
GO

-- ── ตรวจสอบ ────────────────────────────────────────────────────────────────
SELECT COUNT(*) AS ReceiptsTotal,
       SUM(CASE WHEN Nexaacc_Doc_Id IS NOT NULL THEN 1 ELSE 0 END) AS Linked,
       SUM(CASE WHEN Nexaacc_Doc_Id IS NULL THEN 1 ELSE 0 END) AS NotLinked
FROM Account_Receipt;

-- เอกสารเดียวถูกผูกกับใบเสร็จมากกว่าหนึ่งใบ = ผิด ต้องแก้ด้วยเครื่องมือผูก/ปลด
SELECT Nexaacc_Doc_Id, COUNT(*) AS Receipts, MIN(ID) AS Sample1, MAX(ID) AS Sample2
FROM Account_Receipt
WHERE Nexaacc_Doc_Id IS NOT NULL
GROUP BY Nexaacc_Doc_Id
HAVING COUNT(*) > 1;
