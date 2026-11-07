-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 31: Purchase flow (รับของ → ชำระ/ซื้อเชื่อ → คืนของ) + ต้นทุนถัวเฉลี่ยเคลื่อนที่
-- ════════════════════════════════════════════════════════════════════════════
-- 1) Product_In (รับสินค้าเข้าสต๊อก — หน้า Product/In) เก็บข้อมูลเอกสารซื้อ:
--      Supplier_Name / Supplier_TaxId / Supplier_Doc_No / Supplier_Doc_Date — ผู้ขาย + เลขที่/วันที่ใบกำกับ/ใบส่งของ
--      Vat_Amount      — ภาษีซื้อของทั้งใบ (เก็บที่ทุกแถวของ batch เดียวกัน = ยอดรวมของ batch, ไม่ใช่รายแถว)
--      Payment_Status  — UNPAID (รับของแล้ว รอใบแจ้งหนี้) | PAID (จ่ายแล้ว) | CREDIT (ซื้อเชื่อ ตั้งเจ้าหนี้)
--      Linked_Voucher_Id — ใบสำคัญจ่าย (Account_Payment.ID) ที่ชำระ/ตั้งหนี้ค่าสินค้าของ batch นี้
--      Receipt_Batch   — เลขที่การรับของ (GR-…) หนึ่งครั้งที่กดบันทึก = หนึ่ง batch (หลายสินค้า)
--      Nexaacc_Movement_Id — StockMovement ของ NextAcc ที่ "เป็นของเดียวกัน" กับแถวนี้ (โหมด NEXTACC_OCR:
--                       pull เจอ movement ซื้อจาก OCR/ใบซื้อที่ตรงกับแถวที่คีย์ไว้ → ผูกแทนการ insert ซ้ำ)
-- 2) Account_Payment.Linked_Goods_Receipt — Receipt_Batch ที่ใบสำคัญจ่ายนี้ชำระ (batch แรก; ทุก batch ที่ผูก
--    ดูได้จาก Product_In.Linked_Voucher_Id). ใบที่ผูก → บรรทัดค่าสินค้าลง Dr GRNI (21240) แทนค่าใช้จ่าย
--    + Credit_Paid_Date / Credit_Paid_How / Credit_Paid_By / Credit_Paid_Queue_Id — บันทึกจ่ายชำระใบซื้อเชื่อ
--      (หน้า Account/CreditPurchasePayment) ฝั่ง TakeTime
-- 3) Product_Out.Purchase_Return_Ref / Return_Receipt_Batch — การคืนสินค้าให้ผู้ขาย (หน้า Product/PurchaseReturn)
--    OutType = 'RETURN_TO_VENDOR' (คอลัมน์จาก PHASE12_09)
-- 4) Config Nexaacc_Purchase_Qty_Source = TAKETIME (default) | NEXTACC_OCR — แหล่งจริงของ "จำนวน" สินค้าที่ซื้อเข้า
--
-- idempotent — รันซ้ำได้ (COL_LENGTH / NOT EXISTS guard), ไม่ลบ/ไม่แก้ข้อมูลเดิม (แถวเก่า Payment_Status = NULL = legacy)
-- โค้ดทำงานได้ทั้งก่อนและหลังรัน (ก่อนรัน = พฤติกรรมเดิม). หลังรัน: recycle App Pool (cache ตรวจคอลัมน์ 5 นาที)
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
GO

-- 1) Product_In
IF OBJECT_ID('dbo.Product_In', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Product_In', 'Supplier_Name') IS NULL
        ALTER TABLE dbo.Product_In ADD Supplier_Name NVARCHAR(200) NULL;
    IF COL_LENGTH('dbo.Product_In', 'Supplier_TaxId') IS NULL
        ALTER TABLE dbo.Product_In ADD Supplier_TaxId NVARCHAR(20) NULL;
    IF COL_LENGTH('dbo.Product_In', 'Supplier_Doc_No') IS NULL
        ALTER TABLE dbo.Product_In ADD Supplier_Doc_No NVARCHAR(60) NULL;
    IF COL_LENGTH('dbo.Product_In', 'Supplier_Doc_Date') IS NULL
        ALTER TABLE dbo.Product_In ADD Supplier_Doc_Date DATE NULL;
    IF COL_LENGTH('dbo.Product_In', 'Vat_Amount') IS NULL
        ALTER TABLE dbo.Product_In ADD Vat_Amount DECIMAL(18,2) NULL;
    IF COL_LENGTH('dbo.Product_In', 'Payment_Status') IS NULL
        ALTER TABLE dbo.Product_In ADD Payment_Status NVARCHAR(20) NULL;   -- UNPAID | PAID | CREDIT
    IF COL_LENGTH('dbo.Product_In', 'Linked_Voucher_Id') IS NULL
        ALTER TABLE dbo.Product_In ADD Linked_Voucher_Id NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.Product_In', 'Receipt_Batch') IS NULL
        ALTER TABLE dbo.Product_In ADD Receipt_Batch NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.Product_In', 'Nexaacc_Movement_Id') IS NULL
        ALTER TABLE dbo.Product_In ADD Nexaacc_Movement_Id UNIQUEIDENTIFIER NULL;
    PRINT N'Product_In: เพิ่มคอลัมน์เอกสารซื้อ/สถานะชำระ/Receipt_Batch แล้ว';
END
ELSE
    PRINT N'⚠ ไม่มีตาราง Product_In — ข้าม';
GO

IF OBJECT_ID('dbo.Product_In', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Product_In', 'Receipt_Batch') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_In_Receipt_Batch' AND object_id = OBJECT_ID('dbo.Product_In'))
    EXEC sp_executesql N'CREATE INDEX IX_Product_In_Receipt_Batch ON dbo.Product_In (Receipt_Batch) INCLUDE (Product_ID, Amount, PricePerUnit, Payment_Status, Linked_Voucher_Id) WHERE Receipt_Batch IS NOT NULL;';
GO

-- 2) Account_Payment (ใบสำคัญจ่าย)
IF OBJECT_ID('dbo.Account_Payment', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Account_Payment', 'Linked_Goods_Receipt') IS NULL
        ALTER TABLE dbo.Account_Payment ADD Linked_Goods_Receipt NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.Account_Payment', 'Credit_Paid_Date') IS NULL
        ALTER TABLE dbo.Account_Payment ADD Credit_Paid_Date DATETIME NULL;
    IF COL_LENGTH('dbo.Account_Payment', 'Credit_Paid_How') IS NULL
        ALTER TABLE dbo.Account_Payment ADD Credit_Paid_How NVARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Account_Payment', 'Credit_Paid_By') IS NULL
        ALTER TABLE dbo.Account_Payment ADD Credit_Paid_By NVARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Account_Payment', 'Credit_Paid_Queue_Id') IS NULL
        ALTER TABLE dbo.Account_Payment ADD Credit_Paid_Queue_Id BIGINT NULL;
    PRINT N'Account_Payment: เพิ่ม Linked_Goods_Receipt + คอลัมน์จ่ายชำระใบซื้อเชื่อแล้ว';
END
GO

-- 3) Product_Out (คืนสินค้าให้ผู้ขาย)
IF OBJECT_ID('dbo.Product_Out', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Product_Out', 'Purchase_Return_Ref') IS NULL
        ALTER TABLE dbo.Product_Out ADD Purchase_Return_Ref NVARCHAR(60) NULL;
    IF COL_LENGTH('dbo.Product_Out', 'Return_Receipt_Batch') IS NULL
        ALTER TABLE dbo.Product_Out ADD Return_Receipt_Batch NVARCHAR(50) NULL;
    PRINT N'Product_Out: เพิ่ม Purchase_Return_Ref / Return_Receipt_Batch แล้ว';
END
GO

IF OBJECT_ID('dbo.Product_Out', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Product_Out', 'Return_Receipt_Batch') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_Out_Return_Receipt_Batch' AND object_id = OBJECT_ID('dbo.Product_Out'))
    EXEC sp_executesql N'CREATE INDEX IX_Product_Out_Return_Receipt_Batch ON dbo.Product_Out (Return_Receipt_Batch) INCLUDE (Product_ID, Amount, Purchase_Return_Ref) WHERE Return_Receipt_Batch IS NOT NULL;';
GO

-- 4) Config
IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Purchase_Qty_Source')
BEGIN
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_Purchase_Qty_Source', N'TAKETIME',
            N'แหล่งจริงของจำนวนสินค้าที่ซื้อเข้า: TAKETIME = หน้า Product/In (push qty ไป NextAcc, pull ข้าม movement ซื้อจาก OCR/ใบซื้อ) | NEXTACC_OCR = ใบซื้อ/OCR ใน NextAcc (Product/In ไม่ push qty, pull ผูกกับแถวที่คีย์ด้วยเลขที่ใบกำกับ)',
            GETDATE());
    PRINT N'เพิ่ม config Nexaacc_Purchase_Qty_Source = TAKETIME';
END
GO

-- 5) ตรวจ mapping ที่ flow นี้ใช้ (ไม่สร้างให้ — แจ้งเตือนเท่านั้น)
IF OBJECT_ID('dbo.Accounting_Account_Mapping', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = N'GRNI' AND Is_Active = 1)
        PRINT N'⚠ ยังไม่มี mapping GRNI (PHASE18_10) — ใบสำคัญจ่ายที่ผูกการรับของจะล้างบัญชีไม่ได้';
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = N'PURCHASE_RETURNS' AND Is_Active = 1)
        PRINT N'⚠ ยังไม่มี mapping PURCHASE_RETURNS (PHASE12_09) — ผลต่างราคาคืนสินค้า vs ต้นทุนเฉลี่ยจะลงไม่ได้';
END
GO
