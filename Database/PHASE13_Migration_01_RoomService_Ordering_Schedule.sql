-- ============================================================================
-- PHASE 13 - Migration 01: Room Service Ordering Schedule / Open-Close Control
-- ----------------------------------------------------------------------------
-- เพิ่มตารางตั้งค่า "เปิด-ปิด" ระบบให้ลูกค้าสั่งของ (Room Service)
--   - ตั้งเวลาเปิด/ปิดอัตโนมัติ (Open_Time / Close_Time)
--   - หรือบังคับ เปิด/ปิด ด้วยมือ (Manual_Mode = AUTO / OPEN / CLOSED)
--   - ข้อความแจ้งลูกค้าตอนปิด (Closed_Message)
-- เป็นตารางแถวเดียว (Singleton, ID = 1)
-- รันซ้ำได้ (idempotent)
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Guest_RoomService_Settings')
BEGIN
    CREATE TABLE dbo.Guest_RoomService_Settings
    (
        ID              INT            NOT NULL CONSTRAINT PK_Guest_RoomService_Settings PRIMARY KEY,
        Is_Enabled      BIT            NOT NULL CONSTRAINT DF_GRS_IsEnabled   DEFAULT (1),  -- เปิดใช้งานระบบสั่งของหรือไม่
        Manual_Mode     NVARCHAR(10)   NOT NULL CONSTRAINT DF_GRS_ManualMode  DEFAULT (N'AUTO'), -- AUTO | OPEN | CLOSED
        Open_Time       TIME(0)        NOT NULL CONSTRAINT DF_GRS_OpenTime    DEFAULT ('08:00:00'),
        Close_Time      TIME(0)        NOT NULL CONSTRAINT DF_GRS_CloseTime   DEFAULT ('20:00:00'),
        Closed_Message  NVARCHAR(500)  NULL,
        Updated_Date    DATETIME       NOT NULL CONSTRAINT DF_GRS_Updated     DEFAULT (GETDATE()),
        Updated_By      NVARCHAR(100)  NULL
    );

    PRINT 'Created table Guest_RoomService_Settings';
END
ELSE
    PRINT 'Table Guest_RoomService_Settings already exists - skipped';
GO

-- Seed แถวค่าเริ่มต้น (ID = 1) ถ้ายังไม่มี
IF NOT EXISTS (SELECT 1 FROM dbo.Guest_RoomService_Settings WHERE ID = 1)
BEGIN
    INSERT INTO dbo.Guest_RoomService_Settings
        (ID, Is_Enabled, Manual_Mode, Open_Time, Close_Time, Closed_Message, Updated_Date, Updated_By)
    VALUES
        (1, 1, N'AUTO', '08:00:00', '20:00:00',
         N'ขณะนี้อยู่นอกเวลาให้บริการสั่งอาหาร กรุณาสั่งในเวลาทำการ', GETDATE(), N'SYSTEM');

    PRINT 'Seeded default Guest_RoomService_Settings row';
END
ELSE
    PRINT 'Default settings row already exists - skipped';
GO
