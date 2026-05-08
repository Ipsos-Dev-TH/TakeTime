-- =============================================
-- PHASE 12 Migration 09: Stock & Inventory Sync to NextAcc
-- รองรับ:
--   1. Stock-In (รับสินค้าจาก vendor)        → DR Inventory, CR AP/Cash
--   2. Stock-Out / COGS (จ่ายให้ลูกค้า/ขาย)  → DR COGS, CR Inventory
--   3. Stock Adjustment (count variance)    → DR/CR Inventory + Variance
--   4. Stock Write-off (damage/loss)        → DR Stock Loss, CR Inventory
--   5. Product master sync                   → POST/PUT NextAcc product
-- =============================================

-- ──────────────────────────────────────────────
-- 1. Account mappings ใหม่
-- ──────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'COGS')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        (N'COGS', N'ต้นทุนสินค้าขาย (Cost of Goods Sold)', '51100', 'EXPENSE', 1);
END

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'STOCK_LOSS_EXPENSE')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        (N'STOCK_LOSS_EXPENSE', N'ค่าใช้จ่ายสินค้าเสียหาย/สูญหาย', '51200', 'EXPENSE', 1);
END

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'INVENTORY_VARIANCE')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        (N'INVENTORY_VARIANCE', N'ส่วนต่างจากการนับสต็อก', '51210', 'EXPENSE', 1);
END

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'PURCHASE_RETURNS')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        (N'PURCHASE_RETURNS', N'ส่งคืนสินค้าให้ผู้ขาย', '51300', 'EXPENSE', 1);
END
GO

-- ──────────────────────────────────────────────
-- 2. Product_Out — เพิ่ม column OutType เพื่อแยก sale / writeoff / adjustment
-- ──────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='Product_Out' AND COLUMN_NAME='OutType'
)
BEGIN
    ALTER TABLE Product_Out
        ADD OutType NVARCHAR(20) NOT NULL CONSTRAINT DF_Product_Out_OutType DEFAULT 'SALE';
    -- SALE / ROOM_CHARGE / WRITEOFF / ADJUSTMENT_LOSS / RETURN_TO_VENDOR
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='Product_Out' AND COLUMN_NAME='Reason'
)
BEGIN
    ALTER TABLE Product_Out ADD Reason NVARCHAR(500) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='Product_In' AND COLUMN_NAME='InType'
)
BEGIN
    ALTER TABLE Product_In
        ADD InType NVARCHAR(20) NOT NULL CONSTRAINT DF_Product_In_InType DEFAULT 'PURCHASE';
    -- PURCHASE / RETURN_FROM_CHARGE / ADJUSTMENT_GAIN
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='Product_In' AND COLUMN_NAME='Vendor_ID'
)
BEGIN
    ALTER TABLE Product_In ADD Vendor_ID INT NULL;
END
GO

-- ──────────────────────────────────────────────
-- 3. Stock_Adjustment_Log — บันทึกการนับสต็อก / write-off
-- ──────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Stock_Adjustment_Log')
BEGIN
    CREATE TABLE Stock_Adjustment_Log (
        ID              BIGINT IDENTITY(1,1) PRIMARY KEY,
        Adjustment_Date DATETIME NOT NULL DEFAULT GETDATE(),
        Adjustment_Type NVARCHAR(20) NOT NULL, -- COUNT_VARIANCE / WRITEOFF
        Product_ID      INT NOT NULL,
        Expected_Qty    DECIMAL(18,2) NULL,
        Actual_Qty      DECIMAL(18,2) NULL,
        Difference_Qty  DECIMAL(18,2) NOT NULL,    -- + = gain, - = loss
        Cost_PerUnit    DECIMAL(18,2) NOT NULL DEFAULT 0,
        Total_Cost      DECIMAL(18,2) NOT NULL DEFAULT 0,
        Reason          NVARCHAR(500) NULL,
        Admin_ID        INT NULL,
        Created_Date    DATETIME DEFAULT GETDATE(),
        Sync_Status     NVARCHAR(20) DEFAULT 'PENDING',  -- PENDING / SYNCED / FAILED
        Nexaacc_Journal_Id UNIQUEIDENTIFIER NULL
    );

    CREATE NONCLUSTERED INDEX IX_StockAdjLog_Product_Date
        ON Stock_Adjustment_Log (Product_ID, Adjustment_Date DESC);
END
GO

-- ──────────────────────────────────────────────
-- 4. Accounting_Product_Map — cache product Guid ของ NextAcc
-- ──────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Accounting_Product_Map')
BEGIN
    CREATE TABLE Accounting_Product_Map (
        ID              BIGINT IDENTITY(1,1) PRIMARY KEY,
        TakeTime_Product_ID INT NOT NULL,
        Nexaacc_Product_Id UNIQUEIDENTIFIER NULL,
        Product_Code    NVARCHAR(50),       -- TT-{id:D5}
        Product_Name    NVARCHAR(200),
        Last_Synced     DATETIME NULL,
        Sync_Status     NVARCHAR(20) DEFAULT 'PENDING',
        Sync_Error      NVARCHAR(MAX) NULL,
        Created_Date    DATETIME DEFAULT GETDATE(),
        Updated_Date    DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_Accounting_Product_Map UNIQUE (TakeTime_Product_ID)
    );

    CREATE NONCLUSTERED INDEX IX_Product_Map_Synced
        ON Accounting_Product_Map (Sync_Status, Last_Synced DESC);
END
GO

PRINT 'Migration 09 complete: Stock + Inventory sync foundations';
GO
