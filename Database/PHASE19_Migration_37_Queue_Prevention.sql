-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 37: กันคิว NextAcc ค้าง/ล้ม — ชนิดปัญหา + ตัวตรวจก่อนส่ง + ประวัติแก้ใบ (CLAUDE.md ข้อ 21)
-- ════════════════════════════════════════════════════════════════════════════
-- ที่มา: worker แยกปัญหาจาก "ชนิด exception" ⇒ ข้อมูลผิด retry เปล่า 5 รอบ (~2.7 ชม.), รอใบมัดจำตายทั้งที่อีกชั่วโมงก็ขึ้น,
--        mapping ผิดแก้แล้วไม่กลับมาเอง, ใบผิดถูกพบหลังบันทึกเป็นชั่วโมง (เคส #149366 REC261024001: หักมัดจำ 4,700 vs รับจริง 1,500)
--
-- 1) Accounting_Sync_Queue + Error_Class (DATA|CONFIG|WAIT|TRANSIENT|POLICY|DUPLICATE|UNKNOWN) / Error_Rule / Fix_Url /
--    Wait_Since / Depends_On_Queue_Id / Payload_Rebuilt_At + index (Status, Error_Class)
--    รอ = Status PENDING + Error_Class WAIT (ไม่มีสถานะใหม่) · พักเพราะข้อมูล/ตั้งค่า = FAILED + Retry_Count = Max
-- 2) Account_Receipt_Detail + Line_Kind (DEPOSIT_DEDUCTION | DISCOUNT | ADJUSTMENT; NULL = กฎเดิม "ติดลบ = หักมัดจำ")
-- 3) Account_Receipt_Edit_Log — ประวัติแก้ใบ/ยืนยันข้าม/ถูกบล็อก (ก่อน/หลัง JSON + เหตุผล + ผลตรวจ + ทางที่ส่งบัญชี)
-- 4) Accounting_Nexaacc_Period_Status — cache งวดบัญชี/เดือนภาษีที่ยื่นแล้วจาก NextAcc (worker refresh รายชั่วโมง)
-- 5) ค่าตั้ง: Nexaacc_Preflight_Mode = WARN (เตือน ไม่บล็อก — เปลี่ยนเป็น BLOCK ได้ที่หน้า Accounting Integration → คิว)
--    Nexaacc_Payload_Rebuild=1, Nexaacc_Wait_Max_Hours=48, Nexaacc_Transient_MaxRetries=10, Nexaacc_Vat_Filing_Day=23,
--    Nexaacc_Integrity_Hour=2, Nexaacc_Integrity_Days=14, Nexaacc_Integrity_Max=400
-- 6) จำแนกแถวที่ตายค้างอยู่แล้วตามข้อความ (ไม่เปลี่ยนสถานะ) — ให้หน้าคิวแสดงชนิด/ตัวกรองได้ทันที
--
-- ปลอดภัย: idempotent รันซ้ำได้ · ไม่ลบข้อมูล · ไม่แตะ GL/NextAcc · ยังไม่รัน = โค้ดทำงานแบบเดิมทุกประการ (โหมดตัวตรวจ = OFF)
-- รันแล้วให้ recycle app pool (cache schema 5 นาที)
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── 1) คอลัมน์ชนิดปัญหาบนคิว ───────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Accounting_Sync_Queue', N'U') IS NULL
    PRINT N'⚠ ไม่มีตาราง Accounting_Sync_Queue — ข้ามส่วนที่ 1';
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.Accounting_Sync_Queue', N'Error_Class') IS NULL         ALTER TABLE dbo.Accounting_Sync_Queue ADD Error_Class NVARCHAR(20) NULL;
    IF COL_LENGTH(N'dbo.Accounting_Sync_Queue', N'Error_Rule') IS NULL          ALTER TABLE dbo.Accounting_Sync_Queue ADD Error_Rule NVARCHAR(40) NULL;
    IF COL_LENGTH(N'dbo.Accounting_Sync_Queue', N'Fix_Url') IS NULL             ALTER TABLE dbo.Accounting_Sync_Queue ADD Fix_Url NVARCHAR(400) NULL;
    IF COL_LENGTH(N'dbo.Accounting_Sync_Queue', N'Wait_Since') IS NULL          ALTER TABLE dbo.Accounting_Sync_Queue ADD Wait_Since DATETIME NULL;
    IF COL_LENGTH(N'dbo.Accounting_Sync_Queue', N'Depends_On_Queue_Id') IS NULL ALTER TABLE dbo.Accounting_Sync_Queue ADD Depends_On_Queue_Id BIGINT NULL;
    IF COL_LENGTH(N'dbo.Accounting_Sync_Queue', N'Payload_Rebuilt_At') IS NULL  ALTER TABLE dbo.Accounting_Sync_Queue ADD Payload_Rebuilt_At DATETIME NULL;
    PRINT N'Accounting_Sync_Queue: คอลัมน์ชนิดปัญหา/สถานะรอ พร้อม';
END
GO
IF OBJECT_ID(N'dbo.Accounting_Sync_Queue', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Accounting_Sync_Queue', N'Error_Class') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ASQ_Status_ErrorClass' AND object_id = OBJECT_ID(N'dbo.Accounting_Sync_Queue'))
BEGIN
    CREATE INDEX IX_ASQ_Status_ErrorClass ON dbo.Accounting_Sync_Queue (Status, Error_Class)
        INCLUDE (Next_Retry_Date, Depends_On_Queue_Id, Entity_ID, Entity_Type);
    PRINT N'สร้าง index IX_ASQ_Status_ErrorClass';
END
GO

-- ── 2) ชนิดบรรทัดติดลบของใบเสร็จ ──────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Account_Receipt_Detail', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Account_Receipt_Detail', N'Line_Kind') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Receipt_Detail ADD Line_Kind NVARCHAR(20) NULL;
    PRINT N'เพิ่มคอลัมน์ Account_Receipt_Detail.Line_Kind (NULL = กฎเดิม)';
END
GO

-- ── 3) ประวัติแก้ใบ / ยืนยันข้าม ─────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Account_Receipt_Edit_Log', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Account_Receipt_Edit_Log (
        ID              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Account_Receipt_Edit_Log PRIMARY KEY,
        Receipt_ID      NVARCHAR(50) NOT NULL,
        Old_Receipt_ID  NVARCHAR(50) NULL,
        Reservation_ID  INT NULL,
        Action          NVARCHAR(20) NOT NULL,       -- EDIT | OVERRIDE | BLOCKED
        Edited_At       DATETIME NOT NULL CONSTRAINT DF_AREL_At DEFAULT GETDATE(),
        Edited_By       NVARCHAR(100) NULL,
        Reason          NVARCHAR(500) NULL,
        Before_Json     NVARCHAR(MAX) NULL,
        After_Json      NVARCHAR(MAX) NULL,
        Preflight_Text  NVARCHAR(4000) NULL,
        Preflight_Rules NVARCHAR(200) NULL,
        Sync_Action     NVARCHAR(30) NULL,           -- REFRESH | VOID_RECREATE | LEGACY | PageSave | CheckIn ...
        Queue_Id        BIGINT NULL
    );
    CREATE INDEX IX_AREL_Receipt ON dbo.Account_Receipt_Edit_Log (Receipt_ID, Edited_At);
    CREATE INDEX IX_AREL_At ON dbo.Account_Receipt_Edit_Log (Edited_At) INCLUDE (Action);
    PRINT N'สร้างตาราง Account_Receipt_Edit_Log';
END
GO

-- ── 4) cache งวดบัญชี/เดือนภาษี NextAcc ─────────────────────────────────────────
IF OBJECT_ID(N'dbo.Accounting_Nexaacc_Period_Status', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Accounting_Nexaacc_Period_Status (
        [Year]         INT NOT NULL,
        [Month]        INT NOT NULL,
        Period_Status  NVARCHAR(20) NULL,            -- Open | Closed | Locked (NextAcc FiscalPeriodStatus)
        Vat_Filed      BIT NULL,                     -- ภ.พ.30 เดือนนั้น Status ≠ Draft หรือ filingPeriodLocked
        Refreshed_At   DATETIME NOT NULL CONSTRAINT DF_ANPS_At DEFAULT GETDATE(),
        CONSTRAINT PK_Accounting_Nexaacc_Period_Status PRIMARY KEY ([Year], [Month])
    );
    PRINT N'สร้างตาราง Accounting_Nexaacc_Period_Status';
END
GO

-- ── 5) ค่าตั้ง ───────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Accounting_Integration_Config', N'U') IS NOT NULL
BEGIN
    DECLARE @cfg TABLE (K NVARCHAR(100), V NVARCHAR(100), D NVARCHAR(400));
    INSERT INTO @cfg VALUES
      (N'Nexaacc_Preflight_Mode', N'WARN', N'ตัวตรวจใบเสร็จก่อนส่ง NextAcc: OFF (เดิม) / WARN (เตือน ไม่บล็อก — พักคิวเฉพาะที่ส่งไปก็ล้มแน่) / BLOCK (บล็อกปัญหาข้อมูลที่หน้าเว็บ — Owner/Admin บัญชี ยืนยันข้ามได้พร้อมเหตุผล)'),
      (N'Nexaacc_Payload_Rebuild', N'1', N'หยิบแถวคิวใบเสร็จแล้วสร้าง payload ใหม่จากข้อมูลใบปัจจุบัน (Retry ไม่ส่ง snapshot เก่า) · 0 = ปิด'),
      (N'Nexaacc_Wait_Max_Hours', N'48', N'แถวที่ "รอ" รายการอื่น (ใบมัดจำ/ใบต้นทาง) รอได้นานสุดกี่ชั่วโมงก่อนตั้งเป็นล้ม'),
      (N'Nexaacc_Transient_MaxRetries', N'10', N'จำนวน retry ของปัญหาชั่วคราว (เครือข่าย/หมดเวลา/429) — ถอยหลังถึง 6 ชม.'),
      (N'Nexaacc_Vat_Filing_Day', N'23', N'วันกำหนดยื่น ภ.พ.30 (15 = กระดาษ, 23 = e-Filing) — ตรวจรายคืนเตือนใบเดือนก่อนที่ยังไม่ขึ้น NextAcc'),
      (N'Nexaacc_Integrity_Hour', N'2', N'ชั่วโมงที่เริ่มตรวจความถูกต้องรายคืน (ไม่เรียก API)'),
      (N'Nexaacc_Integrity_Days', N'14', N'ตรวจรายคืนย้อนหลังกี่วัน'),
      (N'Nexaacc_Integrity_Max', N'400', N'ตรวจรายคืนสูงสุดกี่ใบ/การจอง');
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    SELECT c.K, c.V, c.D, GETDATE() FROM @cfg c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config x WHERE x.ConfigKey = c.K);
    PRINT N'ค่าตั้ง PHASE19_37 พร้อม (Nexaacc_Preflight_Mode = WARN ถ้ายังไม่เคยตั้ง)';
END
GO

-- ── 6) จำแนกแถวที่ตายค้างอยู่แล้ว (ไม่เปลี่ยนสถานะ) ───────────────────────────────────
IF OBJECT_ID(N'dbo.Accounting_Sync_Queue', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Accounting_Sync_Queue', N'Error_Class') IS NOT NULL
BEGIN
    UPDATE dbo.Accounting_Sync_Queue
       SET Error_Class = CASE
               WHEN Error_Message LIKE N'%หักมัดจำ%เกิน%' OR Error_Message LIKE N'%เกินมัดจำ%' THEN N'DATA'
               WHEN Error_Message LIKE N'%86/4%' OR Error_Message LIKE N'%หยุดออกเอกสารกันผิดชื่อ%' THEN N'DATA'
               WHEN Error_Message LIKE N'%ไม่พบผังบัญชี%' OR Error_Message LIKE N'%No Nexaacc account mapping%'
                    OR Error_Message LIKE N'%has empty GUID%' OR Error_Message LIKE N'%No NexaAcc AccountCode mapping%' THEN N'CONFIG'
               WHEN Error_Message LIKE N'%งวด%ปิดแล้ว%' OR Error_Message LIKE N'%ยื่น ภ.พ.30%' OR Error_Message LIKE N'%RD-ETAX-REACHED%' THEN N'POLICY'
               WHEN Error_Message LIKE N'%อ่านคำตอบไม่ได้%' THEN N'DUPLICATE'
               ELSE NULL END,
           Error_Rule = CASE
               WHEN Error_Message LIKE N'%หักมัดจำ%เกิน%' OR Error_Message LIKE N'%เกินมัดจำ%' THEN N'R04'
               WHEN Error_Message LIKE N'%86/4%' THEN N'BUYER_86_4'
               WHEN Error_Message LIKE N'%หยุดออกเอกสารกันผิดชื่อ%' THEN N'A8_BUYER_MISMATCH'
               WHEN Error_Message LIKE N'%ไม่พบผังบัญชี%' THEN N'NA_ACCOUNT_MISSING'
               WHEN Error_Message LIKE N'%No Nexaacc account mapping%' OR Error_Message LIKE N'%has empty GUID%'
                    OR Error_Message LIKE N'%No NexaAcc AccountCode mapping%' THEN N'MAPPING_MISSING'
               ELSE NULL END
     WHERE Status = 'FAILED' AND Retry_Count >= Max_Retries AND Error_Class IS NULL;
    PRINT N'จำแนกแถวที่ตายค้าง: ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + N' แถว (แถวที่ไม่เข้ากฎ Error_Class ยังเป็น NULL)';
END
GO
PRINT N'PHASE19_37 เสร็จ — recycle app pool แล้วเปิด Admin → ตั้งค่า → NextAcc → คิว (แถบ "ตัวตรวจก่อนส่ง")';
GO
