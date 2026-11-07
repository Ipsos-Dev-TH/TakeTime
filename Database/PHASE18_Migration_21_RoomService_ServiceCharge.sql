-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 21: ค่าบริการรูมเซอร์วิส (Service Charge)
-- ════════════════════════════════════════════════════════════════════════════
-- ตั้งค่าได้ 3 แบบ (Admin → Room Service → ตั้งค่าเปิด-ปิดสั่งของ):
--   PERCENT   = คิดเป็น % ของยอดสินค้า      เช่น 10 → ยอด 500 บวก 50
--   PER_ITEM  = คิดเป็นบาท "ต่อชิ้น"        เช่น 5  → สั่ง 3 ชิ้น บวก 15
--   PER_ORDER = คิดเป็นบาท "ต่อครั้ง"       เช่น 20 → บวก 20 ไม่ว่าสั่งกี่ชิ้น
--   NONE      = ไม่คิดค่าบริการ (ค่าเริ่มต้น — ระบบเดิมไม่เปลี่ยนพฤติกรรม)
--
-- ค่าบริการถูกเก็บแยกในออเดอร์ (Service_Charge) และรวมอยู่ใน Total_Amount แล้ว
-- ⟹ ยอดที่ลงบิลห้องตอนเช็คเอาท์และยอดที่ส่งเข้าบัญชีรวมค่าบริการอัตโนมัติ
--
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- ── 1) ตั้งค่าฝั่งระบบ ────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Guest_RoomService_Settings', 'U') IS NULL
BEGIN
    RAISERROR(N'ต้องรัน PHASE13_Migration_01_RoomService_Ordering_Schedule.sql ก่อน', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Guest_RoomService_Settings') AND name = 'Service_Charge_Mode')
BEGIN
    ALTER TABLE dbo.Guest_RoomService_Settings
        ADD Service_Charge_Mode NVARCHAR(20) NOT NULL
            CONSTRAINT DF_GRS_SvcMode DEFAULT (N'NONE');   -- NONE | PERCENT | PER_ITEM | PER_ORDER
    PRINT 'Added Guest_RoomService_Settings.Service_Charge_Mode';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Guest_RoomService_Settings') AND name = 'Service_Charge_Value')
BEGIN
    ALTER TABLE dbo.Guest_RoomService_Settings
        ADD Service_Charge_Value DECIMAL(10,2) NOT NULL
            CONSTRAINT DF_GRS_SvcValue DEFAULT (0);        -- % หรือ บาท ตามโหมด
    PRINT 'Added Guest_RoomService_Settings.Service_Charge_Value';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Guest_RoomService_Settings') AND name = 'Service_Charge_Label')
BEGIN
    ALTER TABLE dbo.Guest_RoomService_Settings
        ADD Service_Charge_Label NVARCHAR(100) NULL;       -- ชื่อที่แสดงให้ลูกค้า เช่น "ค่าบริการ"
    PRINT 'Added Guest_RoomService_Settings.Service_Charge_Label';
END
GO

-- เพดานค่าบริการ (ใช้กับโหมด % / ต่อชิ้น เพื่อกันยอดพุ่งเวลาสั่งเยอะ) — 0/NULL = ไม่จำกัด
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Guest_RoomService_Settings') AND name = 'Service_Charge_Max')
BEGIN
    ALTER TABLE dbo.Guest_RoomService_Settings
        ADD Service_Charge_Max DECIMAL(10,2) NULL;
    PRINT 'Added Guest_RoomService_Settings.Service_Charge_Max';
END
GO

-- ── 2) เก็บค่าบริการที่คิดจริงไว้ในออเดอร์ (snapshot — แก้ตั้งค่าทีหลังไม่กระทบบิลเก่า) ──
IF OBJECT_ID('dbo.Guest_Room_Service_Orders', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.Guest_Room_Service_Orders') AND name = 'Service_Charge')
BEGIN
    ALTER TABLE dbo.Guest_Room_Service_Orders
        ADD Service_Charge DECIMAL(10,2) NOT NULL
            CONSTRAINT DF_GRSO_SvcCharge DEFAULT (0);
    PRINT 'Added Guest_Room_Service_Orders.Service_Charge';
END
GO

-- ── 3) บัญชีปลายทางของค่าบริการ ───────────────────────────────────────────────
-- ค่าบริการเป็น "รายได้ค่าบริการ" แยกจากรายได้สินค้า — ถ้าไม่ได้ map ระบบจะ fallback
-- ไปบัญชีรายได้สินค้าเดิม (PRODUCT_REVENUE) โดยอัตโนมัติ ไม่ค้างคิว
IF OBJECT_ID('dbo.Accounting_Account_Mapping', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'SERVICE_CHARGE_REVENUE')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        ('SERVICE_CHARGE_REVENUE', N'รายได้ค่าบริการ (Service Charge)', '', 'REVENUE', 1);
    PRINT 'Seeded SERVICE_CHARGE_REVENUE mapping (ยังไม่ได้ระบุรหัสบัญชี — ตั้งได้ในหน้าผังบัญชี)';
END
GO

PRINT 'PHASE18_21 completed';
GO
