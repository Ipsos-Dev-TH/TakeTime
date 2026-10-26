-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 33: ใบลดหนี้ + การคืนเงินลูกค้า (Credit notes & refunds)
-- ════════════════════════════════════════════════════════════════════════════
-- ใช้กับ Class/Accounting/CreditNoteService.cs + หน้า Account/CreditNotes.aspx
-- + ปุ่ม "คืนเงินแบบละเอียด / ออกใบลดหนี้" ในหน้าต่างยกเลิกคืนเงิน (ReserveTable)
--
-- 1) Credit_Note      — หัวเอกสาร (เลข CN{yyMM}-#### ของ TakeTime) 1 แถว = 1 เอกสารต้นทาง (Account_Receipt)
--      Source_Doc_Type  DEPOSIT (ใบเสร็จมัดจำ) | TAX_INVOICE (ใบเสร็จ/ใบกำกับที่ไม่ใช่มัดจำ)
--      Reason_Code      CANCEL_REFUND | CANCEL_PARTIAL | PRICE_ADJUST | COMPENSATION | GOODS_RETURN | WRONG_AMOUNT | OTHER
--      Refund_Method    TRANSFER | CASH | GATEWAY | NONE    · Refund_Status NONE | PENDING | PAID
--      Status           DRAFT | APPROVED | SYNCED | VOIDED | FAILED
--      Retained_Amount  ส่วนที่ไม่คืน (ยึดเป็นค่าธรรมเนียมยกเลิก) ของมัดจำ → NextAcc realize-deposit ForfeitAs
--      Nexaacc_Marker   DOC:{id} → APR:{id} → MNY → DONE (ใบลดหนี้) · REFPRE:{x} → REF:{id} → FFT → DONE (คืนมัดจำ)
-- 2) Credit_Note_Line — รายการ (ยอดรวม VAT) + จำนวนที่คืนเข้าสต๊อก
-- 3) ค่าตั้ง (Accounting_Integration_Config):
--      CreditNote_Enabled          = '1'  (ไม่มีค่า/0 = พฤติกรรมเดิม: ปุ่มยกเลิกเดิมเท่านั้น)
--      CreditNote_Require_Approval = '1'  (1 = พนักงานสร้างเป็นร่าง → Owner/Admin ที่มีสิทธิ์บัญชีอนุมัติ)
--
-- ไม่แตะตาราง/ข้อมูลเดิม · idempotent รันซ้ำได้ · ยังไม่รัน = โค้ดใช้พฤติกรรมเดิมทั้งหมด
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

IF OBJECT_ID('dbo.Credit_Note', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Credit_Note (
        ID                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Credit_Note PRIMARY KEY,
        CN_No               NVARCHAR(30)   NOT NULL,
        CN_Date             DATE           NOT NULL CONSTRAINT DF_CN_Date DEFAULT (CAST(GETDATE() AS DATE)),
        Reservation_ID      INT            NULL,
        Source_Receipt_ID   NVARCHAR(50)   NULL,
        Source_Doc_Type     NVARCHAR(20)   NOT NULL CONSTRAINT DF_CN_SrcType DEFAULT ('TAX_INVOICE'),
        Reason_Code         NVARCHAR(30)   NOT NULL CONSTRAINT DF_CN_Reason DEFAULT ('OTHER'),
        Reason_Text         NVARCHAR(500)  NULL,
        Customer_Name       NVARCHAR(200)  NULL,
        Amount_Gross        DECIMAL(18,2)  NOT NULL CONSTRAINT DF_CN_Gross DEFAULT (0),
        Vat_Amount          DECIMAL(18,2)  NOT NULL CONSTRAINT DF_CN_Vat DEFAULT (0),
        Refund_Amount       DECIMAL(18,2)  NOT NULL CONSTRAINT DF_CN_Refund DEFAULT (0),
        Retained_Amount     DECIMAL(18,2)  NOT NULL CONSTRAINT DF_CN_Retained DEFAULT (0),
        Retained_As         NVARCHAR(20)   NULL,              -- PRICE_OR_FEE | COMPENSATION
        Refund_Paid_How_ID  INT            NULL,              -- Account_Paid_How.ID (แหล่งเงินที่จ่ายคืน)
        Refund_Method       NVARCHAR(20)   NOT NULL CONSTRAINT DF_CN_Method DEFAULT ('NONE'),
        Refund_Ref          NVARCHAR(200)  NULL,              -- เลขอ้างอิงโอน/สลิป
        Refund_Slip_Path    NVARCHAR(400)  NULL,
        Refund_Status       NVARCHAR(20)   NOT NULL CONSTRAINT DF_CN_RefundStatus DEFAULT ('NONE'),
        Gateway_Txn_ID      INT            NULL,              -- Payment_Transaction.ID เมื่อคืนผ่านเกตเวย์
        Restock             BIT            NOT NULL CONSTRAINT DF_CN_Restock DEFAULT (0),
        Status              NVARCHAR(20)   NOT NULL CONSTRAINT DF_CN_Status DEFAULT ('DRAFT'),
        Cancel_Batch        NVARCHAR(40)   NULL,              -- กลุ่มใบที่เกิดจากการยกเลิกการจองครั้งเดียว
        Created_By          NVARCHAR(100)  NULL,
        Created_At          DATETIME       NOT NULL CONSTRAINT DF_CN_Created DEFAULT (GETDATE()),
        Approved_By         NVARCHAR(100)  NULL,
        Approved_At         DATETIME       NULL,
        Voided_By           NVARCHAR(100)  NULL,
        Voided_At           DATETIME       NULL,
        Void_Reason         NVARCHAR(500)  NULL,
        Nexaacc_Path        NVARCHAR(30)   NULL,              -- COMPANY_CN | DEPOSIT_REFUND | INTEGRATION_CN | JOURNAL
        Nexaacc_Doc_Id      UNIQUEIDENTIFIER NULL,
        Nexaacc_Doc_No      NVARCHAR(60)   NULL,
        Nexaacc_Refund_Id   NVARCHAR(100)  NULL,
        Nexaacc_Counter_Code NVARCHAR(30)  NULL,              -- บัญชีฝั่งเครดิตที่ NextAcc ลงในใบลดหนี้ (111x/113x)
        Nexaacc_Marker      NVARCHAR(100)  NULL,
        Sync_Error          NVARCHAR(1000) NULL,
        Notes               NVARCHAR(1000) NULL
    );
    CREATE UNIQUE INDEX UX_Credit_Note_No ON dbo.Credit_Note (CN_No);
    CREATE INDEX IX_Credit_Note_Source ON dbo.Credit_Note (Source_Receipt_ID, Status);
    CREATE INDEX IX_Credit_Note_Date ON dbo.Credit_Note (CN_Date, Status);
    CREATE INDEX IX_Credit_Note_Res ON dbo.Credit_Note (Reservation_ID);
    PRINT N'สร้างตาราง Credit_Note';
END
ELSE
    PRINT N'มีตาราง Credit_Note อยู่แล้ว';
GO

IF OBJECT_ID('dbo.Credit_Note_Line', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Credit_Note_Line (
        ID              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Credit_Note_Line PRIMARY KEY,
        CN_ID           INT            NOT NULL,
        Line_No         INT            NOT NULL CONSTRAINT DF_CNL_No DEFAULT (1),
        Source_Line_No  INT            NULL,          -- Account_Receipt_Detail.Number ของใบต้นทาง
        Product_ID      INT            NULL,
        ProductType_ID  INT            NULL,
        Description     NVARCHAR(500)  NULL,
        Qty             DECIMAL(18,4)  NOT NULL CONSTRAINT DF_CNL_Qty DEFAULT (1),
        Unit            NVARCHAR(30)   NULL,
        Unit_Price      DECIMAL(18,4)  NOT NULL CONSTRAINT DF_CNL_Price DEFAULT (0),
        Amount          DECIMAL(18,2)  NOT NULL CONSTRAINT DF_CNL_Amount DEFAULT (0),   -- รวม VAT
        Restock_Qty     DECIMAL(18,4)  NOT NULL CONSTRAINT DF_CNL_Restock DEFAULT (0),
        Unit_Cost       DECIMAL(18,4)  NULL,
        Account_Code    NVARCHAR(30)   NULL,
        CONSTRAINT FK_Credit_Note_Line_CN FOREIGN KEY (CN_ID) REFERENCES dbo.Credit_Note (ID) ON DELETE CASCADE
    );
    CREATE INDEX IX_Credit_Note_Line_CN ON dbo.Credit_Note_Line (CN_ID);
    PRINT N'สร้างตาราง Credit_Note_Line';
END
ELSE
    PRINT N'มีตาราง Credit_Note_Line อยู่แล้ว';
GO

-- 3) ค่าตั้ง — เพิ่มเฉพาะที่ยังไม่มี (ไม่ทับค่าที่ผู้ดูแลตั้งไว้)
IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'CreditNote_Enabled')
    BEGIN
        INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('CreditNote_Enabled', '1');
        PRINT N'เพิ่ม CreditNote_Enabled = 1';
    END
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'CreditNote_Require_Approval')
    BEGIN
        INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('CreditNote_Require_Approval', '1');
        PRINT N'เพิ่ม CreditNote_Require_Approval = 1';
    END
END
ELSE
    PRINT N'ไม่มีตาราง Accounting_Integration_Config — ข้ามค่าตั้ง (โค้ดถือค่าเริ่มต้น: ปิดใช้)';
GO

PRINT N'PHASE19_33 เสร็จ — เปิดหน้า การเงิน & บัญชี → ใบลดหนี้ / คืนเงิน';
