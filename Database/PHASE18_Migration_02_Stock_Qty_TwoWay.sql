-- PHASE18_Migration_02_Stock_Qty_TwoWay.sql
-- Sync "จำนวนสต๊อก (qty)" สองทางระหว่าง TakeTime <-> NextAcc
--   ขาออก: TakeTime stock movement -> NextAcc POST /product/stock/adjust (qty-only, ไม่โพสต์ GL ซ้ำ)
--   ขากลับ: ดึง NextAcc GET /product/{id}/stock/movements ที่ปรับฝั่ง NextAcc เอง -> ลง Product_In/Out
-- echo-safe: movement ที่เรา push บันทึก Id ใน Nexaacc_Stock_Movement_Seen -> ขากลับข้าม
-- ทั้งคู่ feature-flag (default off)

SET NOCOUNT ON;

-- 1) ตารางกัน echo/ซ้ำ: เก็บ StockMovement.Id ของ NextAcc ที่ "เคยเห็น/เคย push/เคย import" แล้ว
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Nexaacc_Stock_Movement_Seen]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Nexaacc_Stock_Movement_Seen] (
        [Nexaacc_Movement_Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [Direction]           NVARCHAR(12)     NULL,   -- 'PUSH' (เราดันไป) | 'PULL' (import เข้ามา)
        [TakeTime_Product_ID] INT              NULL,
        [Created_Date]        DATETIME         NOT NULL DEFAULT GETDATE()
    );
END
GO

-- 2) cursor pacing สำหรับ pull ต่อสินค้า (round-robin: ดึงสินค้าที่ค้างนานสุดก่อน)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Accounting_Product_Map]') AND name = N'Stock_Last_Pulled')
BEGIN
    ALTER TABLE [dbo].[Accounting_Product_Map] ADD [Stock_Last_Pulled] DATETIME NULL;
END
GO

-- 3) config flags (ปิดไว้ก่อน — เปิดในหน้า Admin -> Accounting Integration)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_StockQtySync')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_StockQtySync', N'0', N'ดันจำนวนสต๊อก (ขาออก) TakeTime -> NextAcc /product/stock/adjust', GETDATE());
GO

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_StockQtyPull')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_StockQtyPull', N'0', N'ดึงจำนวนสต๊อก (ขากลับ) NextAcc -> TakeTime (ปรับสต๊อกฝั่ง NextAcc เอง)', GETDATE());
GO
