-- =============================================
-- PHASE 12 Migration 08: NextAcc Contact Mapping Cache
-- เก็บความสัมพันธ์ระหว่าง TakeTime customer/supplier กับ Contact GUID ใน NextAcc
-- เพื่อ:
--   1. ไม่ต้องสร้าง contact ซ้ำทุกใบเสร็จ (cache)
--   2. ส่ง CustomerId ให้ NextAcc แทนการส่งแค่ชื่อ → AR ledger ถูกต้อง
--   3. Track sync state (last sync date)
-- =============================================

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounting_Contact_Map')
BEGIN
    CREATE TABLE Accounting_Contact_Map (
        ID              BIGINT IDENTITY(1,1) PRIMARY KEY,
        External_Id     NVARCHAR(100) NOT NULL,    -- ใช้ MobilePhone หรือ Vendor.ID เป็น natural key
        Contact_Type    NVARCHAR(20) NOT NULL,     -- CUSTOMER / SUPPLIER
        Nexaacc_Contact_Id UNIQUEIDENTIFIER NULL,
        Name            NVARCHAR(200),
        Tax_Id          NVARCHAR(20),
        Email           NVARCHAR(200),
        Phone           NVARCHAR(50),
        Address         NVARCHAR(500),
        Last_Synced     DATETIME NULL,
        Sync_Status     NVARCHAR(20) DEFAULT 'PENDING', -- PENDING / SYNCED / FAILED
        Sync_Error      NVARCHAR(MAX) NULL,
        Created_Date    DATETIME DEFAULT GETDATE(),
        Updated_Date    DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_Accounting_Contact_Map_External UNIQUE (External_Id, Contact_Type)
    );

    CREATE NONCLUSTERED INDEX IX_Contact_Map_LastSynced
        ON Accounting_Contact_Map (Last_Synced DESC, Sync_Status);

    PRINT 'Created table: Accounting_Contact_Map';
END
GO
