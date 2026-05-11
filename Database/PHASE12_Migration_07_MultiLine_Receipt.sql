-- =============================================
-- PHASE 12 Migration 07: Multi-Line Receipt + ProductType Revenue Mapping
-- รองรับใบเสร็จที่มีหลายรายการ (ห้องพัก + อาหาร + สินค้า) → แยกบัญชีรายได้ใน NextAcc
-- เพิ่ม:
--   1. seed mappings F&B / EXTENSION / รายได้อื่น
--   2. column Revenue_Mapping_Code ใน Account_ProductType เพื่อ map ไป Account_Mapping
--   3. column DepositApplied ใน Account_Receipt เก็บมัดจำที่หักในใบเสร็จ
-- =============================================

-- ──────────────────────────────────────────────
-- 1. Seed account mappings ใหม่สำหรับรายได้ประเภทอื่น
-- ──────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'FNB_REVENUE')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        (N'FNB_REVENUE', N'รายได้อาหารและเครื่องดื่ม', '41110', 'REVENUE', 1);
END

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'ROOM_REVENUE_EXTENSION')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        (N'ROOM_REVENUE_EXTENSION', N'รายได้ค่าห้องพัก (ต่ออายุ/Walk-in เพิ่ม)', '41101', 'REVENUE', 1);
END

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'PRODUCT_REVENUE')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        (N'PRODUCT_REVENUE', N'รายได้ขายสินค้า/มินิบาร์', '41120', 'REVENUE', 1);
END

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'DISCOUNT_EXPENSE')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        (N'DISCOUNT_EXPENSE', N'ส่วนลดให้ลูกค้า (Contra-Revenue)', '41190', 'EXPENSE', 1);
END
GO

-- ──────────────────────────────────────────────
-- 2. เพิ่ม column Revenue_Mapping_Code ใน Account_ProductType
-- เก็บโค้ดที่ใช้ lookup Account_Mapping (เช่น ROOM_REVENUE, FNB_REVENUE)
-- หากไม่ตั้งค่า จะ fallback ไป ROOM_REVENUE
-- ──────────────────────────────────────────────

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account_ProductType' AND COLUMN_NAME = 'Revenue_Mapping_Code'
)
BEGIN
    ALTER TABLE Account_ProductType
        ADD Revenue_Mapping_Code NVARCHAR(50) NULL;
END
GO

-- Heuristic seed — set defaults based on ProductType_Name keywords
-- (admin ปรับตามจริงได้ภายหลังในหน้า Settings)
UPDATE Account_ProductType
SET Revenue_Mapping_Code = CASE
    WHEN ProductType_Name LIKE N'%ห้อง%' OR ProductType_Name LIKE N'%room%' OR ProductType_Name LIKE N'%accommodat%' THEN 'ROOM_REVENUE'
    WHEN ProductType_Name LIKE N'%อาหาร%' OR ProductType_Name LIKE N'%หมูกระทะ%' OR ProductType_Name LIKE N'%เครื่องดื่ม%'
         OR ProductType_Name LIKE N'%food%' OR ProductType_Name LIKE N'%bbq%' OR ProductType_Name LIKE N'%bever%' THEN 'FNB_REVENUE'
    WHEN ProductType_Name LIKE N'%สินค้า%' OR ProductType_Name LIKE N'%minibar%' OR ProductType_Name LIKE N'%product%'
         OR ProductType_Name LIKE N'%shop%' OR ProductType_Name LIKE N'%retail%' THEN 'PRODUCT_REVENUE'
    WHEN ProductType_Name LIKE N'%ต่ออายุ%' OR ProductType_Name LIKE N'%extension%' OR ProductType_Name LIKE N'%extra night%' THEN 'ROOM_REVENUE_EXTENSION'
    ELSE Revenue_Mapping_Code  -- เก็บค่าเดิมถ้ามี
END
WHERE Revenue_Mapping_Code IS NULL;
GO

-- ──────────────────────────────────────────────
-- 3. เพิ่ม column DepositApplied ใน Account_Receipt
-- เก็บจำนวนเงินมัดจำที่หักในใบเสร็จนี้ (ใช้ในการ build journal entry)
-- ──────────────────────────────────────────────

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account_Receipt' AND COLUMN_NAME = 'Deposit_Applied_Amount'
)
BEGIN
    ALTER TABLE Account_Receipt
        ADD Deposit_Applied_Amount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Account_Receipt_DepositApplied DEFAULT 0;
END
GO

-- Backfill: ใบเสร็จเดิมที่ UseDeposit=1 → ใส่จำนวนมัดจำที่ลูกค้าจ่ายในการจองนั้น
UPDATE AR
SET Deposit_Applied_Amount = ISNULL((
    SELECT SUM(Total_Amount)
    FROM Account_Receipt D
    WHERE D.Reservation_ID = AR.Reservation_ID
      AND D.IsDeposit = 1
      AND (D.Status = 'Normal' OR D.Status IS NULL)
), 0)
FROM Account_Receipt AR
WHERE AR.UseDeposit = 1
  AND AR.IsDeposit = 0
  AND AR.Deposit_Applied_Amount = 0;
GO

PRINT 'Migration 07 complete: ProductType revenue mapping + Deposit_Applied_Amount column';
GO
