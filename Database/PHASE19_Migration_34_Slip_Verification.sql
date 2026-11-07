-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 34: ตรวจสลิปโอนก่อนออกเอกสาร (Slip verification)
-- ════════════════════════════════════════════════════════════════════════════
-- ใช้กับ Class/Payments/SlipCheck/* + หน้า Admin/Settings/SlipCheckSettings.aspx (CLAUDE.md ข้อ 18)
-- นโยบาย: SlipOK (ฟรี 100 สลิป/เดือน) → หมด/ใช้ไม่ได้ = อ่านยอดจากรูปเอง (Tesseract) → มีปัญหา = ข้ามการตรวจ (ติดธง)
--
-- 1) Payment_Slips + ผลตรวจ: Slip_Amount, Slip_TransRef, Slip_Sending_Bank, Slip_Trans_Date, Slip_Sender_Name,
--    Slip_Receiver_Acct (เก็บเลขที่ปิดบังเท่านั้น — PDPA), Verify_Status, Verify_Source, Verify_Message, Verify_At,
--    Override_By, Override_Reason, Override_At, Matched_Paid_How_ID (+ คอลัมน์ OCR_* ของ PHASE2 ถ้ายังไม่มี)
--      Verify_Status: VERIFIED | MISMATCH | DUPLICATE | WRONG_RECEIVER | UNVERIFIED_BYPASS | PENDING_BANK | ALLOCATED | REJECTED
--      Verify_Source: SLIPOK | LOCAL_OCR | QR_ONLY | NONE
-- 2) UX_Payment_Slips_TransRef — unique (Slip_Sending_Bank, Slip_TransRef) เฉพาะแถวที่มี transRef และไม่ใช่ REJECTED/ALLOCATED
--    (ALLOCATED = แถวรองของสลิปที่ "แบ่งใช้หลายการจอง" — ยอดอยู่ใน Payment_Slip_Allocation ของแถวหลัก)
--    ⚠ filtered index ต้องการ SET ANSI_NULLS/QUOTED_IDENTIFIER ON ตอนเขียน (ค่าเริ่มต้นของ SqlClient อยู่แล้ว)
-- 3) Payment_Slip_Allocation(Slip_ID, Reservation_ID, Account_Receipt_ID, Amount, Child_Slip_ID) — สลิปเดียวโอนรวมหลายการจอง
-- 4) Slip_Verify_Usage(Year_Month, Provider, Used_Count, Exhausted) — นับโควต้า SlipOK รายเดือน (เดือนใหม่ = แถวใหม่ = รีเซ็ตเอง)
-- 5) Slip_Verify_Log — ประวัติการตรวจทุกครั้ง + Consumed = 1 เมื่อสลิปถูกใช้รับเงินกิจกรรม/รูมเซอร์วิส (กันสลิปซ้ำข้ามระบบ)
-- 6) Account_Paid_How + Bank_Code, Bank_Account_No, PromptPay_Id — บัญชีรับเงินของร้าน (ตรวจผู้รับ) ว่างได้ =
--    ใช้เลขจาก NextAcc (Nexaacc_BankAccountId → Accounting_Nexaacc_BankAccounts.Account_Number)
-- 7) ค่าตั้ง (System_Config): SlipVerify_Enabled = 0 (ปิดไว้ก่อน), SlipOK_BranchId, SlipOK_ApiKey_Encrypted (ลับ),
--    SlipOK_Monthly_Quota = 100, SlipVerify_Amount_Tolerance = 1.00, SlipVerify_Check_Receiver = 1, SlipOK_Timeout_Sec = 10,
--    SlipVerify_Local_Ocr = 1, SlipVerify_Tesseract_Path, SlipVerify_Tessdata_Dir
--
-- ไม่แตะข้อมูลเดิม · idempotent รันซ้ำได้ · ยังไม่รัน = โค้ดทำงานแบบเดิมทั้งหมด (ทุกจุดตรวจ COL_LENGTH/OBJECT_ID ก่อน)
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1) Payment_Slips
IF OBJECT_ID('dbo.Payment_Slips', 'U') IS NULL
    PRINT N'⚠ ไม่มีตาราง Payment_Slips — ข้ามส่วนที่ 1-2';
ELSE
BEGIN
    IF COL_LENGTH('dbo.Payment_Slips', 'Slip_Amount') IS NULL         ALTER TABLE dbo.Payment_Slips ADD Slip_Amount DECIMAL(18,2) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Slip_TransRef') IS NULL       ALTER TABLE dbo.Payment_Slips ADD Slip_TransRef NVARCHAR(40) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Slip_Sending_Bank') IS NULL   ALTER TABLE dbo.Payment_Slips ADD Slip_Sending_Bank NVARCHAR(10) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Slip_Trans_Date') IS NULL     ALTER TABLE dbo.Payment_Slips ADD Slip_Trans_Date DATETIME NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Slip_Sender_Name') IS NULL    ALTER TABLE dbo.Payment_Slips ADD Slip_Sender_Name NVARCHAR(150) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Slip_Receiver_Acct') IS NULL  ALTER TABLE dbo.Payment_Slips ADD Slip_Receiver_Acct NVARCHAR(150) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Verify_Status') IS NULL       ALTER TABLE dbo.Payment_Slips ADD Verify_Status NVARCHAR(20) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Verify_Source') IS NULL       ALTER TABLE dbo.Payment_Slips ADD Verify_Source NVARCHAR(20) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Verify_Message') IS NULL      ALTER TABLE dbo.Payment_Slips ADD Verify_Message NVARCHAR(1000) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Verify_At') IS NULL           ALTER TABLE dbo.Payment_Slips ADD Verify_At DATETIME NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Override_By') IS NULL         ALTER TABLE dbo.Payment_Slips ADD Override_By NVARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Override_Reason') IS NULL     ALTER TABLE dbo.Payment_Slips ADD Override_Reason NVARCHAR(500) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Override_At') IS NULL         ALTER TABLE dbo.Payment_Slips ADD Override_At DATETIME NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'Matched_Paid_How_ID') IS NULL ALTER TABLE dbo.Payment_Slips ADD Matched_Paid_How_ID INT NULL;

    -- คอลัมน์ OCR ของ PHASE2_Migration_03 (หน้า Account/SlipVerification อ่านอยู่แล้ว) — บางฐานยังไม่ได้รัน
    IF COL_LENGTH('dbo.Payment_Slips', 'OCR_Amount') IS NULL          ALTER TABLE dbo.Payment_Slips ADD OCR_Amount DECIMAL(18,2) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'OCR_Status') IS NULL          ALTER TABLE dbo.Payment_Slips ADD OCR_Status NVARCHAR(20) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'OCR_Confidence') IS NULL      ALTER TABLE dbo.Payment_Slips ADD OCR_Confidence DECIMAL(5,2) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'OCR_RawText') IS NULL         ALTER TABLE dbo.Payment_Slips ADD OCR_RawText NVARCHAR(MAX) NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'OCR_ProcessedDate') IS NULL   ALTER TABLE dbo.Payment_Slips ADD OCR_ProcessedDate DATETIME NULL;
    IF COL_LENGTH('dbo.Payment_Slips', 'OCR_ErrorMessage') IS NULL    ALTER TABLE dbo.Payment_Slips ADD OCR_ErrorMessage NVARCHAR(1000) NULL;
    PRINT N'Payment_Slips: คอลัมน์ผลตรวจสลิปพร้อม';
END
GO

-- 2) unique index กันสลิปซ้ำ (แยก batch — คอลัมน์ต้องมีก่อน compile)
IF OBJECT_ID('dbo.Payment_Slips', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Payment_Slips', 'Slip_TransRef') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Payment_Slips_TransRef' AND object_id = OBJECT_ID('dbo.Payment_Slips'))
BEGIN
    EXEC(N'CREATE UNIQUE NONCLUSTERED INDEX UX_Payment_Slips_TransRef
            ON dbo.Payment_Slips (Slip_Sending_Bank, Slip_TransRef)
            WHERE Slip_TransRef IS NOT NULL AND Verify_Status <> ''REJECTED'' AND Verify_Status <> ''ALLOCATED''');
    PRINT N'สร้าง UX_Payment_Slips_TransRef';
END
GO

-- 3) Payment_Slip_Allocation
IF OBJECT_ID('dbo.Payment_Slip_Allocation', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payment_Slip_Allocation (
        ID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payment_Slip_Allocation PRIMARY KEY,
        Slip_ID BIGINT NOT NULL,                 -- แถวหลักของสลิป (Payment_Slips.ID ที่ถือ transRef)
        Child_Slip_ID BIGINT NULL,               -- แถวรอง (Verify_Status = ALLOCATED) ของการจองถัดไป
        Reservation_ID BIGINT NULL,
        Account_Receipt_ID NVARCHAR(50) NULL,
        Amount DECIMAL(18,2) NOT NULL,
        Created_By NVARCHAR(100) NULL,
        Created_At DATETIME NOT NULL CONSTRAINT DF_Payment_Slip_Allocation_Created DEFAULT (GETDATE())
    );
    CREATE INDEX IX_Payment_Slip_Allocation_Slip ON dbo.Payment_Slip_Allocation (Slip_ID);
    PRINT N'สร้างตาราง Payment_Slip_Allocation';
END
GO

-- 4) Slip_Verify_Usage
IF OBJECT_ID('dbo.Slip_Verify_Usage', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Slip_Verify_Usage (
        Year_Month CHAR(6) NOT NULL,            -- yyyyMM
        Provider NVARCHAR(20) NOT NULL,          -- SLIPOK
        Used_Count INT NOT NULL CONSTRAINT DF_Slip_Verify_Usage_Count DEFAULT (0),
        Exhausted BIT NOT NULL CONSTRAINT DF_Slip_Verify_Usage_Exhausted DEFAULT (0),   -- SlipOK ตอบ 1003/1004
        Updated_At DATETIME NOT NULL CONSTRAINT DF_Slip_Verify_Usage_Updated DEFAULT (GETDATE()),
        CONSTRAINT PK_Slip_Verify_Usage PRIMARY KEY (Year_Month, Provider)
    );
    PRINT N'สร้างตาราง Slip_Verify_Usage';
END
GO

-- 5) Slip_Verify_Log
IF OBJECT_ID('dbo.Slip_Verify_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Slip_Verify_Log (
        ID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Slip_Verify_Log PRIMARY KEY,
        Created_At DATETIME NOT NULL CONSTRAINT DF_Slip_Verify_Log_Created DEFAULT (GETDATE()),
        Source_Type NVARCHAR(30) NULL,           -- RESERVATION | ACTIVITY | ROOM_SERVICE | TEST
        Source_ID BIGINT NULL,
        File_Hash CHAR(64) NULL,
        Sending_Bank NVARCHAR(10) NULL,
        Trans_Ref NVARCHAR(40) NULL,
        Amount DECIMAL(18,2) NULL,
        Expected_Amount DECIMAL(18,2) NULL,
        Verify_Status NVARCHAR(20) NULL,
        Verify_Source NVARCHAR(20) NULL,
        Message NVARCHAR(1000) NULL,
        Consumed BIT NOT NULL CONSTRAINT DF_Slip_Verify_Log_Consumed DEFAULT (0),
        Created_By NVARCHAR(100) NULL
    );
    CREATE INDEX IX_Slip_Verify_Log_Ref ON dbo.Slip_Verify_Log (Trans_Ref, Sending_Bank) INCLUDE (Consumed, Source_Type, Source_ID);
    PRINT N'สร้างตาราง Slip_Verify_Log';
END
GO

-- 6) บัญชีรับเงินของร้าน (ตรวจผู้รับ)
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Account_Paid_How', 'Bank_Code') IS NULL       ALTER TABLE dbo.Account_Paid_How ADD Bank_Code NVARCHAR(10) NULL;        -- 004 KBANK, 014 SCB …
    IF COL_LENGTH('dbo.Account_Paid_How', 'Bank_Account_No') IS NULL ALTER TABLE dbo.Account_Paid_How ADD Bank_Account_No NVARCHAR(30) NULL;  -- เลขบัญชี (ตัวเลข)
    IF COL_LENGTH('dbo.Account_Paid_How', 'PromptPay_Id') IS NULL    ALTER TABLE dbo.Account_Paid_How ADD PromptPay_Id NVARCHAR(20) NULL;     -- เบอร์/เลขผู้เสียภาษี
    PRINT N'Account_Paid_How: Bank_Code / Bank_Account_No / PromptPay_Id พร้อม';
END
GO

-- 7) ค่าตั้ง
IF OBJECT_ID('dbo.System_Config', 'U') IS NOT NULL
BEGIN
    DECLARE @cfg TABLE (K NVARCHAR(100), V NVARCHAR(400), N NVARCHAR(200), D NVARCHAR(500), S BIT, T NVARCHAR(20), O INT);
    INSERT INTO @cfg VALUES
      (N'SlipVerify_Enabled',          N'0',    N'ตรวจสลิปก่อนออกเอกสาร', N'1 = ตรวจสลิปโอน (SlipOK → อ่านยอดจากรูป → ข้ามการตรวจ) ก่อนออกใบเสร็จ/ส่งบัญชี · 0 = แบบเดิม', 0, N'bool', 10),
      (N'SlipOK_BranchId',             NULL,    N'SlipOK Branch ID', N'ไอดีสาขาจากเมนู API ใน LINE SlipOK', 0, N'text', 20),
      (N'SlipOK_ApiKey_Encrypted',     NULL,    N'SlipOK API key', N'เก็บแบบเข้ารหัส (เหมือน Nexaacc_ApiKey_Encrypted)', 1, N'text', 30),
      (N'SlipOK_Monthly_Quota',        N'100',  N'โควต้า SlipOK ต่อเดือน', N'แพ็กเกจฟรี = 100 สลิป/เดือน — ครบแล้วใช้การอ่านยอดจากรูปแทน', 0, N'number', 40),
      (N'SlipVerify_Amount_Tolerance', N'1.00', N'ยอมให้ยอดต่างได้ (บาท)', N'ต่างไม่เกินนี้ = ถือว่าตรง และใช้ยอดจากธนาคาร', 0, N'number', 50),
      (N'SlipVerify_Check_Receiver',   N'1',    N'ตรวจบัญชีผู้รับ', N'1 = เทียบบัญชีผู้รับในสลิปกับบัญชีรับเงินของร้าน (Account_Paid_How)', 0, N'bool', 60),
      (N'SlipOK_Timeout_Sec',          N'10',   N'เวลารอ SlipOK (วินาที)', N'สูงสุด 10 วินาที', 0, N'number', 70),
      (N'SlipVerify_Local_Ocr',        N'1',    N'อ่านยอดจากรูปเอง (Tesseract)', N'ใช้เมื่อโควต้า SlipOK หมด/ใช้ไม่ได้', 0, N'bool', 80),
      (N'SlipVerify_Tesseract_Path',   NULL,    N'ที่อยู่ tesseract.exe', N'ว่าง = C:\Program Files\Tesseract-OCR\tesseract.exe หรือ ~/App_Data/tesseract', 0, N'text', 90),
      (N'SlipVerify_Tessdata_Dir',     NULL,    N'โฟลเดอร์ tessdata', N'ว่าง = ~/App_Data/tessdata (ถ้ามี tha/eng.traineddata) หรือค่าเริ่มต้นของโปรแกรม', 0, N'text', 100);

    INSERT INTO dbo.System_Config (ConfigKey, ConfigValue, Category, DisplayName, [Description], IsSecret, InputType, DisplayOrder, ModifiedDate)
    SELECT c.K, c.V, N'ตรวจสลิป', c.N, c.D, c.S, c.T, 900 + c.O, GETDATE()
      FROM @cfg c
     WHERE NOT EXISTS (SELECT 1 FROM dbo.System_Config x WHERE x.ConfigKey = c.K);
    PRINT N'ค่าตั้งตรวจสลิป: เพิ่ม ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' ค่า (ค่าเดิมไม่แตะ)';
END
ELSE PRINT N'⚠ ไม่มี System_Config (PHASE18_18) — ตั้งค่าผ่าน Web.config appSettings ได้';
GO

PRINT N'PHASE19_34 เสร็จ — เปิดใช้ที่ ศูนย์ตั้งค่า → การชำระเงิน → ตรวจสลิปโอน (ต้อง recycle app pool หรือรอ cache 5 นาที)';
GO
