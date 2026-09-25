-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 11: OTA Settlement foundation (ลูกหนี้ OTA / payout / AGP advance)
-- ════════════════════════════════════════════════════════════════════════════
-- Design: docs/OTA_Settlement_Design.md — agency model:
--   เช็คเอาท์ OTA:  Dr ลูกหนี้ OTA (gross) / Cr รายได้ + VAT   (ราย booking)
--   Payout:         Dr เงินฝาก + Dr เงินรับล่วงหน้า OTA(ที่ถูกหัก) + Dr ค่าคอม / Cr ลูกหนี้ OTA
--   AGP Advance:    Dr เงินฝาก / Cr เงินรับล่วงหน้า OTA (หนี้สิน)
--   ค่าคอมเจ้าต่างประเทศ → expense + IsForeignService → ภ.พ.36 (§83/6)
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- 1) Account mappings (Nexaacc_AccountCode ว่าง = ให้ผู้ดูแล map ในหน้า Admin — กัน auto-match ผิดบัญชี)
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'OTA_RECEIVABLE')
    INSERT INTO Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES ('OTA_RECEIVABLE', N'ลูกหนี้ OTA (Agoda/Booking ฯลฯ)', '', 'ASSET', 1);
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'OTA_COMMISSION')
    INSERT INTO Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES ('OTA_COMMISSION', N'ค่าคอมมิชชั่น OTA', '', 'EXPENSE', 1);
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'OTA_ADVANCE')
    INSERT INTO Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES ('OTA_ADVANCE', N'เงินรับล่วงหน้าจาก OTA (AGP Advance)', '', 'LIABILITY', 1);
GO

-- 2) OTA_Channels: คอลัมน์การเงิน (ตารางมีอยู่แล้วจาก Channel Manager)
IF COL_LENGTH('OTA_Channels', 'Default_Commission_Pct') IS NULL
    ALTER TABLE OTA_Channels ADD Default_Commission_Pct DECIMAL(5,2) NULL;          -- เช่น 17.00 — ใช้เตือน % เบี่ยง
IF COL_LENGTH('OTA_Channels', 'Is_Foreign') IS NULL
    ALTER TABLE OTA_Channels ADD Is_Foreign BIT NOT NULL DEFAULT 1;                 -- ค่าคอม → §83/6 ภ.พ.36
IF COL_LENGTH('OTA_Channels', 'Nexaacc_Contact_Id') IS NULL
    ALTER TABLE OTA_Channels ADD Nexaacc_Contact_Id UNIQUEIDENTIFIER NULL;          -- contact รายเจ้าบน NextAcc (aging รายเจ้า)
IF COL_LENGTH('OTA_Channels', 'Advance_Balance') IS NULL
    ALTER TABLE OTA_Channels ADD Advance_Balance DECIMAL(18,2) NOT NULL DEFAULT 0;  -- AGP คงเหลือ (denormalized)
GO

-- 3) งวดโอน (payout) + booking ในงวด + ก้อน advance
IF OBJECT_ID('OTA_Payout', 'U') IS NULL
BEGIN
    CREATE TABLE OTA_Payout (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        Channel_Code NVARCHAR(50) NOT NULL,
        Payout_Date DATE NOT NULL,
        Amount_Received DECIMAL(18,2) NOT NULL,          -- ยอดโอนเข้าบัญชีจริง
        Advance_Deducted DECIMAL(18,2) NOT NULL DEFAULT 0, -- AGP ที่ถูกหักงวดนี้
        Gross_Total DECIMAL(18,2) NOT NULL DEFAULT 0,    -- Σ ราคาขาย booking ที่เลือก
        Commission_Derived DECIMAL(18,2) NOT NULL DEFAULT 0, -- = Gross − Received − Advance
        Status NVARCHAR(20) NOT NULL DEFAULT 'DRAFT',    -- DRAFT / POSTED / VOIDED
        Nexaacc_Journal_Id UNIQUEIDENTIFIER NULL,        -- JE ปิดลูกหนี้
        Nexaacc_Expense_Id UNIQUEIDENTIFIER NULL,        -- expense ค่าคอม (§83/6)
        Notes NVARCHAR(500) NULL,
        Created_Date DATETIME NOT NULL DEFAULT GETDATE(),
        Created_By NVARCHAR(100) NULL
    );
    CREATE INDEX IX_OTA_Payout_Channel ON OTA_Payout (Channel_Code, Status);
END
GO

IF OBJECT_ID('OTA_Payout_Item', 'U') IS NULL
BEGIN
    CREATE TABLE OTA_Payout_Item (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        Payout_ID BIGINT NOT NULL,
        Reservation_ID INT NOT NULL,
        Gross_Amount DECIMAL(18,2) NOT NULL,
        CONSTRAINT FK_OTA_Payout_Item FOREIGN KEY (Payout_ID) REFERENCES OTA_Payout(ID)
    );
    -- booking หนึ่งปิดได้งวดเดียว (กันเลือกซ้ำ 2 payout) — VOIDED payout ปลด item ออกตอน void
    CREATE UNIQUE INDEX UX_OTA_Payout_Item_Res ON OTA_Payout_Item (Reservation_ID);
    CREATE INDEX IX_OTA_Payout_Item_Payout ON OTA_Payout_Item (Payout_ID);
END
GO

IF OBJECT_ID('OTA_Advance', 'U') IS NULL
BEGIN
    CREATE TABLE OTA_Advance (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        Channel_Code NVARCHAR(50) NOT NULL,
        Received_Date DATE NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,                   -- ก้อน AGP ที่รับ
        Nexaacc_Journal_Id UNIQUEIDENTIFIER NULL,        -- JE: Dr เงินฝาก / Cr เงินรับล่วงหน้า OTA
        Notes NVARCHAR(500) NULL,
        Created_Date DATETIME NOT NULL DEFAULT GETDATE(),
        Created_By NVARCHAR(100) NULL
    );
    CREATE INDEX IX_OTA_Advance_Channel ON OTA_Advance (Channel_Code);
END
GO

-- 4) feature flag (default off — เปิดหลังเคาะกับผู้ทำบัญชี + map 3 บัญชี + rebuild)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Ota_Settlement')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Nexaacc_Ota_Settlement', '0',
            N'OTA settlement: เช็คเอาท์ OTA ลง Dr ลูกหนี้ OTA/Cr รายได้+VAT ราย booking + หน้า payout/advance (ดู docs/OTA_Settlement_Design.md)');
    PRINT 'Seeded flag Nexaacc_Ota_Settlement = 0 (off)';
END
GO

SELECT TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type
FROM Accounting_Account_Mapping WHERE TakeTime_Code LIKE 'OTA_%' ORDER BY TakeTime_Code;
GO
