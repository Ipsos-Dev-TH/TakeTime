-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 38: หน้าตรวจทาน OCR ก่อนส่ง NextAcc + ใบลด/เพิ่มหนี้จากผู้ขาย
-- ════════════════════════════════════════════════════════════════════════════
-- CLAUDE.md ข้อ 3 "หน้าตรวจทานก่อนส่ง + ใบลดหนี้ผู้ขาย" — Voucher/OcrUpload.aspx
--
-- 1) Ocr_Document_Log — 1 แถว = 1 สแกน (Scan_Id ของ NextAcc)
--    • Review_Json     = ค่าที่ผู้ใช้กำลังตรวจทาน/ยืนยัน (draft — ออกจากหน้าแล้วกลับมาทำต่อได้)
--    • Ocr_Raw_Json    = ค่าที่ OCR อ่านได้ครั้งแรก (ไม่แก้) · Corrections_Json = รายการช่องที่ผู้ใช้แก้ (audit)
--    • Status          = DRAFT | CREATED | UPDATED | APPROVED | DONE | EVIDENCE | FAILED | DISCARDED
--                        (CREATED/UPDATED = มีเอกสารร่างบน NextAcc แล้ว — กดส่งซ้ำจะทำต่อจากขั้นที่ค้าง ไม่สร้างใหม่)
--    • Supplier_TaxId + Supplier_Doc_No_Norm = คีย์กันซ้ำ (เลขผู้ขาย + เลขที่เอกสาร ตัดช่องว่าง/สัญลักษณ์)
--    • Nexaacc_Doc_Id / Nexaacc_Doc_No = เอกสารที่สร้างบน NextAcc (ลิงก์ PDF ในหน้า)
-- ยังไม่รัน migration = หน้าเก็บ draft ใน Session (หายเมื่อหมด session) และกันซ้ำเฉพาะกับเอกสารบน NextAcc
--
-- ไม่แตะข้อมูลเดิม · idempotent รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.Ocr_Document_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ocr_Document_Log
    (
        ID                    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ocr_Document_Log PRIMARY KEY,
        Scan_Id               UNIQUEIDENTIFIER NOT NULL,
        Doc_Kind              NVARCHAR(10)  NULL,      -- PV | EXP | VCN | VDN
        Status                NVARCHAR(20)  NOT NULL CONSTRAINT DF_Ocr_Document_Log_Status DEFAULT (N'DRAFT'),
        Original_File_Name    NVARCHAR(260) NULL,
        Supplier_Name         NVARCHAR(300) NULL,
        Supplier_TaxId        NVARCHAR(20)  NULL,
        Supplier_Doc_No       NVARCHAR(100) NULL,
        Supplier_Doc_No_Norm  NVARCHAR(100) NULL,
        Doc_Date              DATE          NULL,
        Total_Amount          DECIMAL(18,2) NULL,
        Net_Pay               DECIMAL(18,2) NULL,
        Source_Doc_Id         UNIQUEIDENTIFIER NULL,   -- ใบต้นทางของใบลด/เพิ่มหนี้ (NextAcc)
        Source_Doc_No         NVARCHAR(60)  NULL,
        Return_Ref            NVARCHAR(60)  NULL,      -- PRT- (คืนสินค้าที่บันทึกแล้ว)
        Nexaacc_Doc_Id        UNIQUEIDENTIFIER NULL,
        Nexaacc_Doc_No        NVARCHAR(60)  NULL,
        Nexaacc_Reclass_Ref   NVARCHAR(60)  NULL,      -- JE ย้ายขาเงินของใบลด/เพิ่มหนี้ (OCRCN-…-MNY)
        Review_Json           NVARCHAR(MAX) NULL,
        Ocr_Raw_Json          NVARCHAR(MAX) NULL,
        Corrections_Json      NVARCHAR(MAX) NULL,
        Last_Error            NVARCHAR(2000) NULL,
        Created_By            NVARCHAR(100) NULL,
        Created_At            DATETIME NOT NULL CONSTRAINT DF_Ocr_Document_Log_Created DEFAULT (GETDATE()),
        Updated_At            DATETIME NULL,
        Submitted_By          NVARCHAR(100) NULL,
        Submitted_At          DATETIME NULL
    );
    PRINT N'สร้างตาราง Ocr_Document_Log แล้ว';
END
ELSE PRINT N'Ocr_Document_Log มีอยู่แล้ว — ตรวจคอลัมน์';
GO

-- คอลัมน์ที่อาจขาด (รันกับตารางรุ่นก่อน)
IF COL_LENGTH('dbo.Ocr_Document_Log', 'Nexaacc_Reclass_Ref') IS NULL ALTER TABLE dbo.Ocr_Document_Log ADD Nexaacc_Reclass_Ref NVARCHAR(60) NULL;
IF COL_LENGTH('dbo.Ocr_Document_Log', 'Return_Ref') IS NULL          ALTER TABLE dbo.Ocr_Document_Log ADD Return_Ref NVARCHAR(60) NULL;
IF COL_LENGTH('dbo.Ocr_Document_Log', 'Corrections_Json') IS NULL    ALTER TABLE dbo.Ocr_Document_Log ADD Corrections_Json NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Ocr_Document_Log_Scan' AND object_id = OBJECT_ID('dbo.Ocr_Document_Log'))
    CREATE UNIQUE INDEX UX_Ocr_Document_Log_Scan ON dbo.Ocr_Document_Log (Scan_Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ocr_Document_Log_Dup' AND object_id = OBJECT_ID('dbo.Ocr_Document_Log'))
    CREATE INDEX IX_Ocr_Document_Log_Dup ON dbo.Ocr_Document_Log (Supplier_TaxId, Supplier_Doc_No_Norm) INCLUDE (Status, Doc_Kind, Nexaacc_Doc_No);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ocr_Document_Log_Status' AND object_id = OBJECT_ID('dbo.Ocr_Document_Log'))
    CREATE INDEX IX_Ocr_Document_Log_Status ON dbo.Ocr_Document_Log (Status, Updated_At);
GO

PRINT N'PHASE19_38 เสร็จ — หน้า Voucher/OcrUpload เก็บ draft + ประวัติการตรวจทานในตาราง Ocr_Document_Log';
GO
