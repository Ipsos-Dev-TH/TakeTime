-- =============================================
-- PHASE 12 Migration 02: WHT Certificate & E-Tax Invoice Tracking
-- รองรับการเชื่อมต่อ NextAcc WHT Certificate API และ E-Tax Invoice API
-- =============================================

-- WHT Certificate tracking — ติดตามหนังสือรับรองหัก ณ ที่จ่ายที่ส่งไป NextAcc
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounting_WHT_Cert_Log')
BEGIN
    CREATE TABLE Accounting_WHT_Cert_Log (
        ID              BIGINT IDENTITY(1,1) PRIMARY KEY,
        Document_Number NVARCHAR(100),
        Voucher_ID      INT NULL,
        Nexaacc_Doc_Id  UNIQUEIDENTIFIER NULL,
        Nexaacc_Cert_Id UNIQUEIDENTIFIER NULL,
        Certificate_No  NVARCHAR(100) NULL,
        Payee_Name      NVARCHAR(200) NULL,
        Payee_TaxId     NVARCHAR(20) NULL,
        WHT_Rate        DECIMAL(5,2) NULL,
        WHT_Amount      DECIMAL(18,2) NULL,
        Income_Type     NVARCHAR(50) NULL,
        Form_Type       NVARCHAR(20) NULL,
        Status          NVARCHAR(20) DEFAULT 'PENDING',
        Error_Message   NVARCHAR(MAX) NULL,
        Created_Date    DATETIME DEFAULT GETDATE(),
        Processed_Date  DATETIME NULL
    );
    CREATE INDEX IX_WHT_Cert_Log_DocNum ON Accounting_WHT_Cert_Log (Document_Number);
    CREATE INDEX IX_WHT_Cert_Log_Status ON Accounting_WHT_Cert_Log (Status);
END
GO

-- E-Tax Invoice tracking — ติดตามใบกำกับภาษีอิเล็กทรอนิกส์ที่ส่งไป NextAcc
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounting_ETax_Log')
BEGIN
    CREATE TABLE Accounting_ETax_Log (
        ID              BIGINT IDENTITY(1,1) PRIMARY KEY,
        Document_Number NVARCHAR(100),
        Receipt_Number  NVARCHAR(100) NULL,
        Reservation_ID  INT NULL,
        Nexaacc_Doc_Id  UNIQUEIDENTIFIER NULL,
        Nexaacc_Etax_Id UNIQUEIDENTIFIER NULL,
        Etax_Ref_Number NVARCHAR(100) NULL,
        Status          NVARCHAR(20) DEFAULT 'PENDING',
        Signed_Date     DATETIME NULL,
        Submitted_Date  DATETIME NULL,
        Xml_Url         NVARCHAR(500) NULL,
        Pdf_Url         NVARCHAR(500) NULL,
        Email_Sent      BIT DEFAULT 0,
        Error_Message   NVARCHAR(MAX) NULL,
        Created_Date    DATETIME DEFAULT GETDATE(),
        Processed_Date  DATETIME NULL
    );
    CREATE INDEX IX_ETax_Log_DocNum ON Accounting_ETax_Log (Document_Number);
    CREATE INDEX IX_ETax_Log_Status ON Accounting_ETax_Log (Status);
END
GO

-- Add E-Tax and WHT config keys to integration config
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE Config_Key = 'Etax_AutoGenerate')
BEGIN
    INSERT INTO Accounting_Integration_Config (Config_Key, Config_Value, Description, Created_Date)
    VALUES ('Etax_AutoGenerate', '0', N'สร้าง E-Tax Invoice อัตโนมัติเมื่อออกใบเสร็จ (0=ปิด, 1=เปิด)', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE Config_Key = 'WHT_AutoGenerateCert')
BEGIN
    INSERT INTO Accounting_Integration_Config (Config_Key, Config_Value, Description, Created_Date)
    VALUES ('WHT_AutoGenerateCert', '1', N'สร้างหนังสือรับรองหัก ณ ที่จ่ายอัตโนมัติเมื่อบันทึกใบสำคัญจ่ายที่มี WHT (0=ปิด, 1=เปิด)', GETDATE());
END
GO
