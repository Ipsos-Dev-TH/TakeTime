-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 39: ใบลดหนี้ผู้ขาย = ตัวจริง ↔ คืนสินค้า (PRT-) · เครดิตกับผู้ขาย · ใบเพิ่มหนี้ตั้งเจ้าหนี้
-- ════════════════════════════════════════════════════════════════════════════
-- CLAUDE.md ข้อ 3 "ใบลดหนี้ผู้ขาย ↔ คืนสินค้า (PHASE19_39)" — คำตัดสิน 4 ข้อของทีม (เจ้าของสั่ง "ตั้งทีมตัดสินใจและพัฒนาเลย")
--
-- 1) Purchase_Return_Cn_Link — 1 แถว = 1 การคืนสินค้า (PRT-…) และสถานะใบลดหนี้ของการคืนนั้น
--    • Cn_Status   = PENDING (วางบิลแล้ว รอใบลดหนี้) | LINKED (ผูกใบลดหนี้ NextAcc แล้ว) | NOT_REQUIRED (ยังไม่วางบิล / ผู้ขายไม่ออก)
--    • Prt_Journal = FULL (JE PRT ครบเดิม) | PENDING (JE PRT ไม่กลับภาษีซื้อ — ถูกกลับทั้งใบ PRT-…-REV ตอนผูก) | NONE (ผูกใบที่บันทึกก่อน — ไม่มี JE PRT)
--    • Purchase_Net / Carrying_Cost = ราคาซื้อ / ต้นทุนเฉลี่ยที่ตัดสต๊อก ณ วันคืน → JE ปรับต้นทุน PRT-…-DIF = (ยอดบรรทัดใบลดหนี้ที่ลงบัญชีสินค้า − Carrying_Cost)
-- 2) Vendor_Credit_Ledger — ยอดกับผู้ขาย: CN_CREDIT (+ เครดิตที่ผู้ขายติดเรา) / CN_APPLIED (− หักตอนจ่ายใบซื้อเชื่อ)
--                                         DN_PAYABLE (+ ใบเพิ่มหนี้ที่ตั้งเจ้าหนี้รอจ่าย) / DN_PAID (− จ่ายแล้ว)
-- 3) mapping VENDOR_CREDIT_RECEIVABLE (ลูกหนี้ผู้ขาย/เครดิตจากผู้ขาย — ยังไม่ผูก = ใช้เจ้าหนี้การค้าเป็นยอดเดบิต)
-- 4) ค่าตั้ง Vendor_Cn_Primary (1) / Vendor_Cn_Reminder_Days (7) / Ocr_Fill_Missing_Contact (1)
--
-- ยังไม่รัน = พฤติกรรมเดิมทุกอย่าง (หน้าคืนสินค้าลง JE ครบ, ใบลดหนี้ของการคืน = เก็บเป็นหลักฐาน, ไม่มีหักเครดิตผู้ขาย)
-- ไม่แตะข้อมูลเดิม · idempotent รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.Purchase_Return_Cn_Link', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Purchase_Return_Cn_Link
    (
        Return_Ref            NVARCHAR(60)  NOT NULL CONSTRAINT PK_Purchase_Return_Cn_Link PRIMARY KEY,
        Receipt_Batch         NVARCHAR(50)  NULL,
        Supplier_Name         NVARCHAR(300) NULL,
        Supplier_TaxId        NVARCHAR(20)  NULL,
        Return_Date           DATETIME      NULL,
        Purchase_Net          DECIMAL(18,2) NOT NULL CONSTRAINT DF_PRCL_Net DEFAULT (0),
        Carrying_Cost         DECIMAL(18,2) NOT NULL CONSTRAINT DF_PRCL_Carry DEFAULT (0),
        Vat_Amount            DECIMAL(18,2) NOT NULL CONSTRAINT DF_PRCL_Vat DEFAULT (0),   -- VAT ของของที่คืน (ตามสัดส่วนใบรับ) — ใบลดหนี้ควรกลับเท่านี้
        Counter_Key           NVARCHAR(30)  NULL,       -- GRNI | ACCOUNTS_PAYABLE | CASH | NONE (PurchaseService.PlanReturn)
        Paid_How              NVARCHAR(100) NULL,
        Paid_How_Account_Id   NVARCHAR(50)  NULL,
        Cn_Status             NVARCHAR(20)  NOT NULL CONSTRAINT DF_PRCL_Status DEFAULT (N'PENDING'),
        Prt_Journal           NVARCHAR(20)  NOT NULL CONSTRAINT DF_PRCL_Journal DEFAULT (N'FULL'),
        Cn_Scan_Id            UNIQUEIDENTIFIER NULL,     -- Ocr_Document_Log.Scan_Id ของใบลดหนี้
        Cn_Doc_Id             UNIQUEIDENTIFIER NULL,     -- เอกสาร CreditNote บน NextAcc
        Cn_Doc_No             NVARCHAR(60)  NULL,
        Cn_Supplier_Doc_No    NVARCHAR(100) NULL,
        Cn_Inventory_Net      DECIMAL(18,2) NULL,        -- ยอดบรรทัดใบลดหนี้ที่ลงบัญชีสินค้าคงเหลือ
        Cn_Total              DECIMAL(18,2) NULL,
        Rev_Ref               NVARCHAR(60)  NULL,        -- PRT-…-REV (กลับ JE PRT ที่ลงระหว่างรอ)
        Dif_Ref               NVARCHAR(60)  NULL,        -- PRT-…-DIF (ปรับต้นทุนเฉลี่ย ↔ ราคาซื้อ)
        Dif_Amount            DECIMAL(18,2) NULL,
        Created_By            NVARCHAR(100) NULL,
        Created_At            DATETIME NOT NULL CONSTRAINT DF_PRCL_Created DEFAULT (GETDATE()),
        Linked_By             NVARCHAR(100) NULL,
        Linked_At             DATETIME NULL,
        Last_Reminded_At      DATETIME NULL
    );
    PRINT N'สร้างตาราง Purchase_Return_Cn_Link แล้ว';
END
ELSE PRINT N'Purchase_Return_Cn_Link มีอยู่แล้ว';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PRCL_Status' AND object_id = OBJECT_ID('dbo.Purchase_Return_Cn_Link'))
    CREATE INDEX IX_PRCL_Status ON dbo.Purchase_Return_Cn_Link (Cn_Status, Created_At);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PRCL_Scan' AND object_id = OBJECT_ID('dbo.Purchase_Return_Cn_Link'))
    CREATE INDEX IX_PRCL_Scan ON dbo.Purchase_Return_Cn_Link (Cn_Scan_Id) WHERE Cn_Scan_Id IS NOT NULL;
GO

IF OBJECT_ID('dbo.Vendor_Credit_Ledger', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Vendor_Credit_Ledger
    (
        ID                    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vendor_Credit_Ledger PRIMARY KEY,
        Entry_Type            NVARCHAR(20)  NOT NULL,   -- CN_CREDIT | CN_APPLIED | DN_PAYABLE | DN_PAID
        Vendor_Id             INT           NULL,       -- Vendor.ID (ถ้าผูก)
        Vendor_TaxId          NVARCHAR(20)  NULL,
        Vendor_Name           NVARCHAR(300) NULL,
        Nexaacc_Contact_Id    UNIQUEIDENTIFIER NULL,
        Amount                DECIMAL(18,2) NOT NULL,   -- CN_CREDIT/DN_PAYABLE = + · CN_APPLIED/DN_PAID = −
        Account_Code          NVARCHAR(20)  NULL,       -- บัญชีที่ยอดนี้นั่งอยู่ (VENDOR_CREDIT_RECEIVABLE หรือเจ้าหนี้)
        Source_Ref            NVARCHAR(100) NOT NULL,   -- CN/DN: OCR-{scanId} · ใช้/จ่าย: เลขใบสำคัญจ่าย/อ้างอิง
        Source_Doc_No         NVARCHAR(100) NULL,       -- เลข NextAcc / เลขที่ใบผู้ขาย
        Parent_Id             INT           NULL,       -- DN_PAID → แถว DN_PAYABLE ที่จ่าย
        Applied_Voucher_Id    NVARCHAR(50)  NULL,
        Journal_Ref           NVARCHAR(60)  NULL,
        Note                  NVARCHAR(500) NULL,
        Created_By            NVARCHAR(100) NULL,
        Created_At            DATETIME NOT NULL CONSTRAINT DF_VCL_Created DEFAULT (GETDATE())
    );
    PRINT N'สร้างตาราง Vendor_Credit_Ledger แล้ว';
END
ELSE PRINT N'Vendor_Credit_Ledger มีอยู่แล้ว';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_VCL_Type_Source' AND object_id = OBJECT_ID('dbo.Vendor_Credit_Ledger'))
    CREATE UNIQUE INDEX UX_VCL_Type_Source ON dbo.Vendor_Credit_Ledger (Entry_Type, Source_Ref);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VCL_Vendor' AND object_id = OBJECT_ID('dbo.Vendor_Credit_Ledger'))
    CREATE INDEX IX_VCL_Vendor ON dbo.Vendor_Credit_Ledger (Vendor_TaxId, Vendor_Id) INCLUDE (Entry_Type, Amount);
GO

-- 3) mapping บัญชีเครดิตกับผู้ขาย (ยังไม่ผูก = เจ้าหนี้การค้า)
IF OBJECT_ID('dbo.Accounting_Account_Mapping', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = 'VENDOR_CREDIT_RECEIVABLE')
    BEGIN
        INSERT INTO dbo.Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
        VALUES ('VENDOR_CREDIT_RECEIVABLE', N'ลูกหนี้ผู้ขาย / เครดิตจากผู้ขาย (ใบลดหนี้ที่เก็บไว้หักการซื้อครั้งหน้า) — ยังไม่ผูก = ลงเจ้าหนี้การค้า', '', 'ASSET', 1);
        PRINT N'เพิ่ม mapping VENDOR_CREDIT_RECEIVABLE (ยังไม่ผูกรหัสบัญชี — ผูกที่ Admin → Accounting Integration → ผังบัญชี)';
    END
    ELSE PRINT N'มี mapping VENDOR_CREDIT_RECEIVABLE อยู่แล้ว — ไม่แตะ';
END
GO

-- 4) ค่าตั้ง
IF OBJECT_ID(N'dbo.Accounting_Integration_Config', N'U') IS NOT NULL
BEGIN
    DECLARE @cfg TABLE (K NVARCHAR(100), V NVARCHAR(100), D NVARCHAR(400));
    INSERT INTO @cfg VALUES
      (N'Vendor_Cn_Primary', N'1', N'1 = ใบลดหนี้ผู้ขายใน NextAcc เป็นรายการบัญชี/ภาษีตัวจริงของการคืนสินค้า (PRT ระหว่างรอถูกกลับอัตโนมัติ) · 0 = เดิม (ใบลดหนี้ของการคืน = หลักฐาน)'),
      (N'Vendor_Cn_Reminder_Days', N'7', N'เตือน (Telegram + ตรวจสุขภาพ) เมื่อคืนสินค้าที่วางบิลแล้วเกินกี่วันยังไม่ได้ใบลดหนี้'),
      (N'Ocr_Fill_Missing_Contact', N'1', N'หน้าตรวจทาน OCR เติมที่อยู่/สาขา/เลขผู้เสียภาษีของผู้ขายใน NextAcc เฉพาะช่องที่ว่าง (ไม่ทับ) · 0 = ปิด');
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    SELECT c.K, c.V, c.D, GETDATE() FROM @cfg c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config x WHERE x.ConfigKey = c.K);
    PRINT N'ค่าตั้ง PHASE19_39 พร้อม';
END
GO

PRINT N'PHASE19_39 เสร็จ — ผูกบัญชี VENDOR_CREDIT_RECEIVABLE ในหน้า Accounting Integration แล้วกด "ตรวจสุขภาพ"';
GO
