-- PHASE18_Migration_01_Pos_Daily_Rollup.sql
-- รวบยอดขายหน้าร้านที่ "ไม่ออกใบกำกับภาษีในระบบ" (CheckBox1 ไม่ติ๊ก) เป็นใบรับเงินสดสรุปรายวัน
-- 1 ใบ/วัน/แหล่งรับเงิน → sync ไป NextAcc (รายได้ + VAT ขาย) + COGS ตัดสต๊อก (Dr COGS/Cr Inventory)
-- ทำงานอัตโนมัติผ่าน background timer ใน Global.asax (ไม่ต้องกดรายวัน)
--
-- กรณีที่ "ออกใบกำกับในระบบ" (มี Account_Receipt + ยิง EnqueueReceipt ต่อใบ) ไม่ถูกแตะ — sync ต่อใบเหมือนเดิม
-- ตัวแยก: การขายไม่ออกใบกำกับ = แถว Product_Out ที่ Remark = N'ขาย' และ Account_Receipt_ID = '0' (ไม่มีใบเสร็จ)

SET NOCOUNT ON;

-- 1) คอลัมน์ marker กันรวบซ้ำ (NULL = ยังไม่รวบ, มีค่า = อยู่ในใบสรุป POSDAY-... แล้ว, 'LEGACY' = ของเก่าก่อนเปิดฟีเจอร์)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Product_Out]') AND name = N'Pos_Rollup_Ref')
BEGIN
    ALTER TABLE [dbo].[Product_Out] ADD [Pos_Rollup_Ref] NVARCHAR(60) NULL;
END
GO

-- 2) Backfill: ทำเครื่องหมายแถวเก่าทั้งหมดเป็น 'LEGACY' เพื่อไม่ให้ระบบไปรวบยอดย้อนหลังทั้งประวัติ
--    (รวบเฉพาะการขายใหม่หลัง deploy ที่ Pos_Rollup_Ref = NULL)
UPDATE [dbo].[Product_Out]
SET [Pos_Rollup_Ref] = N'LEGACY'
WHERE [Pos_Rollup_Ref] IS NULL;
GO

-- 3) index ช่วย query หาแถวที่ยังไม่รวบ (เฉพาะ Remark ขาย + ยังไม่รวบ)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductOut_PosRollup')
BEGIN
    CREATE INDEX [IX_ProductOut_PosRollup]
    ON [dbo].[Product_Out] ([Pos_Rollup_Ref], [DateTime_Out])
    INCLUDE ([Product_ID], [Amount], [PricePerUnit], [Account_Receipt_ID], [Account_Paid_How_ID], [Remark]);
END
GO

-- 4) seed config flag (ปิดไว้ก่อน default — เปิดในหน้า Admin → Accounting Integration)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_PosDailyRollup')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_PosDailyRollup', N'0', N'รวบยอดขายหน้าร้านที่ไม่ออกใบกำกับเป็นใบรับเงินสดสรุปรายวัน', GETDATE());
END
GO
