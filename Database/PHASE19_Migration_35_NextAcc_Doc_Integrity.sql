-- ============================================================================
-- PHASE19 Migration 35 — กันเอกสาร NextAcc ซ้ำ + ตัวตรวจเทียบเอกสาร ↔ ใบเสร็จ
-- ============================================================================
-- เคสจริง (การจอง #149286): บน NextAcc มีใบมัดจำ 2 ใบ (REC-20260819-0001/-0002) และใบกำกับ 2 ใบ
-- (TIV-20260904-0002 3,000 / -0003 3,200) "ชำระแล้ว" ทั้งหมด ไม่มีใบใดถูกยกเลิก — TakeTime ชี้ได้แค่ใบละหนึ่ง
-- เอกสารที่เหลือมองไม่เห็น. ต้นเหตุ (โค้ดแก้ใน commit เดียวกัน):
--   • PrepareResync (โหมด DOCUMENT) supersede ตัวชี้โดยไม่ void → CREATE รอบใหม่ "หาเอกสารเดิมไม่เจอ" แล้วสร้างใบที่สอง
--   • Repost fallback ล้าง marker + เข้าคิว CREATE "ก่อน" void จะวิ่ง; ProcessVoidReceipt กลืน error แล้วนับว่าสำเร็จ
--   • ล้างทั้งหมด (ResetReservationAccounting) ล้าง marker ทุกใบแม้ void ล้ม
--   • POST สร้างเอกสารยิงซ้ำเองตอน timeout โดยไม่มี Idempotency-Key
--
-- migration นี้:
--   1) ให้แน่ใจว่ามีคอลัมน์ผูก Account_Receipt.Nexaacc_Doc_Id/Number/LinkedAt (PHASE18_32 — รันซ้ำได้)
--   2) ผูกย้อนหลังจากคิว "รวม SUPERSEDED" สำหรับใบที่ยังไม่ผูก (ด่านกันซ้ำใช้คอลัมน์นี้หาเอกสารเดิม)
--   3) ตาราง Accounting_Reconcile_Finding — ผลตัวตรวจเทียบ (ORPHAN_SUPERSEDED / ORPHAN_DELETED_LOCAL / ORPHAN_OTHER_BOOKING? /
--      UNKNOWN_NEXTACC_ONLY / DUPLICATE / VOID_STUCK / AMOUNT_MISMATCH / MISSING_ON_NEXTACC / DEPOSIT_MISMATCH)
--   4) ค่าตั้ง: Nexaacc_Idempotency_Keys=1, Nexaacc_Reconcile_Nightly=1 (ตีสาม, ย้อน 14 วัน, สูงสุด 150 การจอง)
--   5) อายุ Accounting_Sync_Log 90 → 180 วัน (เฉพาะที่ยังเป็นค่าเริ่มต้น 90) — แถวที่ผลตรวจเทียบที่ยังเปิดอ้างถึงไม่ถูกลบเลย
--
-- ปลอดภัย: รันซ้ำได้ · ไม่ลบข้อมูล · ไม่แตะ GL/NextAcc · ยังไม่รัน = โค้ดทำงานแบบเดิม (ด่านกันซ้ำยังทำงานจากคิว
-- COMPLETED/SUPERSEDED, ตัวตรวจเทียบแสดงผลได้แต่ไม่เก็บ, งานรายคืนปิด)
-- ============================================================================
SET NOCOUNT ON;

-- ── 1) คอลัมน์ผูกเอกสาร (PHASE18_32) ─────────────────────────────────────────
IF COL_LENGTH(N'dbo.Account_Receipt', N'Nexaacc_Doc_Id') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Receipt ADD Nexaacc_Doc_Id UNIQUEIDENTIFIER NULL;
    PRINT N'เพิ่มคอลัมน์ Account_Receipt.Nexaacc_Doc_Id';
END
GO
IF COL_LENGTH(N'dbo.Account_Receipt', N'Nexaacc_Doc_Number') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Receipt ADD Nexaacc_Doc_Number NVARCHAR(50) NULL;
    PRINT N'เพิ่มคอลัมน์ Account_Receipt.Nexaacc_Doc_Number';
END
GO
IF COL_LENGTH(N'dbo.Account_Receipt', N'Nexaacc_Doc_LinkedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Receipt ADD Nexaacc_Doc_LinkedAt DATETIME NULL;
    PRINT N'เพิ่มคอลัมน์ Account_Receipt.Nexaacc_Doc_LinkedAt';
END
GO

-- ── 2) ผูกย้อนหลังจากคิว CREATE ที่ COMPLETED หรือ SUPERSEDED ──────────────────
--      เลือกต่อใบ: COMPLETED ก่อน SUPERSEDED แล้วใหม่สุด · ข้ามใบที่ marker บอกว่ายกเลิกแล้ว (VOIDED/VOIDING:)
--      และข้าม GUID ที่มีคิว VOID_RECEIPT สำเร็จตามหลัง (เอกสารนั้นถูกยกเลิกไปแล้ว)
;WITH q AS (
    SELECT
        SUBSTRING(CAST(c.Payload AS NVARCHAR(MAX)),
            CHARINDEX('"receiptNumber":"', CAST(c.Payload AS NVARCHAR(MAX))) + 17,
            CHARINDEX('"', CAST(c.Payload AS NVARCHAR(MAX)), CHARINDEX('"receiptNumber":"', CAST(c.Payload AS NVARCHAR(MAX))) + 17)
                - (CHARINDEX('"receiptNumber":"', CAST(c.Payload AS NVARCHAR(MAX))) + 17)) AS ReceiptNo,
        TRY_CAST(c.Nexaacc_Response_Id AS UNIQUEIDENTIFIER) AS DocId,
        c.Nexaacc_Document_Number AS DocNo,
        c.ID,
        ROW_NUMBER() OVER (
            PARTITION BY SUBSTRING(CAST(c.Payload AS NVARCHAR(MAX)),
                CHARINDEX('"receiptNumber":"', CAST(c.Payload AS NVARCHAR(MAX))) + 17,
                CHARINDEX('"', CAST(c.Payload AS NVARCHAR(MAX)), CHARINDEX('"receiptNumber":"', CAST(c.Payload AS NVARCHAR(MAX))) + 17)
                    - (CHARINDEX('"receiptNumber":"', CAST(c.Payload AS NVARCHAR(MAX))) + 17))
            ORDER BY CASE WHEN c.Status = 'COMPLETED' THEN 0 ELSE 1 END, c.ID DESC) AS rn
    FROM dbo.Accounting_Sync_Queue c
    WHERE c.Action_Type = 'CREATE_RECEIPT_DOCUMENT'
      AND c.Status IN ('COMPLETED', 'SUPERSEDED')
      AND c.Nexaacc_Response_Id IS NOT NULL
      AND TRY_CAST(c.Nexaacc_Response_Id AS UNIQUEIDENTIFIER) IS NOT NULL
      AND CHARINDEX('"receiptNumber":"', CAST(c.Payload AS NVARCHAR(MAX))) > 0
)
UPDATE ar
   SET ar.Nexaacc_Doc_Id = q.DocId,
       ar.Nexaacc_Doc_Number = COALESCE(ar.Nexaacc_Doc_Number, q.DocNo),
       ar.Nexaacc_Doc_LinkedAt = GETDATE()
  FROM dbo.Account_Receipt ar
  JOIN q ON q.ReceiptNo = ar.ID AND q.rn = 1
 WHERE ar.Nexaacc_Doc_Id IS NULL
   AND ISNULL(ar.Nexaacc_Receipt_Payment_Id, '') <> 'VOIDED'
   AND ISNULL(ar.Nexaacc_Receipt_Payment_Id, '') NOT LIKE 'VOIDING:%'
   AND NOT EXISTS (SELECT 1 FROM dbo.Accounting_Sync_Queue v
                    WHERE v.Action_Type = 'VOID_RECEIPT' AND v.Status = 'COMPLETED' AND v.ID > q.ID
                      AND CAST(v.Payload AS NVARCHAR(MAX)) LIKE N'%' + CAST(q.DocId AS NVARCHAR(36)) + N'%');
PRINT N'ผูก Nexaacc_Doc_Id ย้อนหลัง (รวม SUPERSEDED): ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' ใบ';
GO

-- ── 3) ผลตัวตรวจเทียบ ─────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Accounting_Reconcile_Finding', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Accounting_Reconcile_Finding (
        ID                  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Accounting_Reconcile_Finding PRIMARY KEY,
        Reservation_ID      INT NOT NULL,
        Finding_Key         NVARCHAR(200) NOT NULL,      -- DOC:{guid} / RCPT:{เลขใบ}:{ธง} / RES:{ธง}
        Finding_Type        NVARCHAR(40) NOT NULL,
        Receipt_Number      NVARCHAR(50) NULL,
        Nexaacc_Doc_Id      UNIQUEIDENTIFIER NULL,
        Nexaacc_Doc_Number  NVARCHAR(60) NULL,
        Amount              DECIMAL(18,2) NULL,
        Detail              NVARCHAR(1000) NULL,
        First_Seen          DATETIME NOT NULL CONSTRAINT DF_ARF_First DEFAULT GETDATE(),
        Last_Seen           DATETIME NOT NULL CONSTRAINT DF_ARF_Last DEFAULT GETDATE(),
        Resolved_At         DATETIME NULL,
        Resolved_Note       NVARCHAR(300) NULL
    );
    PRINT N'สร้างตาราง Accounting_Reconcile_Finding';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ARF_Open_Key' AND object_id = OBJECT_ID(N'dbo.Accounting_Reconcile_Finding'))
    CREATE UNIQUE INDEX UX_ARF_Open_Key ON dbo.Accounting_Reconcile_Finding (Reservation_ID, Finding_Key) WHERE Resolved_At IS NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ARF_Open' AND object_id = OBJECT_ID(N'dbo.Accounting_Reconcile_Finding'))
    CREATE INDEX IX_ARF_Open ON dbo.Accounting_Reconcile_Finding (Resolved_At, First_Seen) INCLUDE (Reservation_ID, Finding_Type, Nexaacc_Doc_Id);
GO

-- ── 4) ค่าตั้ง ───────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Idempotency_Keys')
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_Idempotency_Keys', N'1',
            N'ส่ง Idempotency-Key กับ POST สร้างเอกสาร/รับชำระ/JV (NextAcc replay ผลเดิม 24 ชม.) · 0 = ไม่ส่ง และไม่ยิงซ้ำเองตอน timeout', GETDATE());
IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Reconcile_Nightly')
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_Reconcile_Nightly', N'1',
            N'ตรวจเทียบเอกสาร NextAcc ↔ ใบเสร็จ ทุกคืน (เอกสารซ้ำ/กำพร้า/void ค้าง) แล้วแจ้ง Telegram เมื่อพบใหม่', GETDATE());
IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Reconcile_Days')
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_Reconcile_Days', N'14', N'ตัวตรวจเทียบรายคืน: ตรวจการจองที่มีใบเสร็จสร้างในกี่วันล่าสุด', GETDATE());
IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Reconcile_Max')
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_Reconcile_Max', N'150', N'ตัวตรวจเทียบรายคืน: สูงสุดกี่การจองต่อคืน (1 การจอง = 1 call by-booking)', GETDATE());
IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Reconcile_Hour')
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_Reconcile_Hour', N'3', N'ตัวตรวจเทียบรายคืน: เริ่มหลังกี่โมง (0-23)', GETDATE());
GO

-- ── 5) อายุ log 90 → 180 วัน (เฉพาะค่าเริ่มต้นเดิม — ผู้ดูแลที่ตั้งเองไม่ถูกแตะ) ─────────────
UPDATE dbo.Accounting_Integration_Config
   SET ConfigValue = N'180',
       Description = N'เก็บ Accounting_Sync_Log ย้อนหลังกี่วัน (0 = ไม่ลบ) · แถวที่ผลตรวจเทียบที่ยังเปิดอ้างถึงไม่ถูกลบ (PHASE19_35)',
       Updated_Date = GETDATE()
 WHERE ConfigKey = N'Nexaacc_SyncLog_Retention_Days' AND LTRIM(RTRIM(ConfigValue)) = N'90';
IF @@ROWCOUNT > 0 PRINT N'ยกอายุ Accounting_Sync_Log 90 → 180 วัน';
GO

PRINT N'PHASE19_35 เสร็จ — recycle app pool เพื่อโหลดค่าตั้งใหม่ (cache 5 นาทีก็ได้)';
GO
