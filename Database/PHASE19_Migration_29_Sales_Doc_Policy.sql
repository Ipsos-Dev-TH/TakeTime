-- ============================================================================
-- PHASE19 Migration 29 — นโยบายเอกสารขาย (Sales document policy)
-- ============================================================================
-- เจ้าของขอ: ขายที่รับเงินโอน / QR / บัตร → ออกใบกำกับภาษีอย่างย่อ/ใบเสร็จบน NextAcc "ทันที" รายใบ
--            ขายเงินสด → รวมเป็นเอกสารสรุปรายวันใบเดียว (ต่อวัน × แหล่งเงิน)
--            ทุกอย่างเปิด/ปิดได้ละเอียด (ช่องทางขาย × ชนิดการชำระเงิน + ตั้งเฉพาะรายแหล่งเงิน)
--
-- 1) ตาราง Sales_Doc_Policy (ช่องทางขาย × ชนิดการชำระเงิน → IMMEDIATE | DAILY_ROLLUP | OFF)
--      Sales_Channel: POS | ROOM_SERVICE | ACTIVITY | AMENITY | FRONTDESK_GATEWAY | DAMAGE | RESERVATION | '*'
--      Channel_Type : CASH | TRANSFER | QR | CARD | GATEWAY_OTHER | DIRECTOR_LOAN | OTA | OTHER | '*'
--    ลำดับการตัดสิน (Class/Integration/SalesDocumentPolicy.Resolve):
--      ค่าตั้งเฉพาะแหล่งเงิน (Account_Paid_How.Sales_Doc_Mode) → (ช่องทาง, ชนิด) → (ช่องทาง, *) → (*, ชนิด) → (*, *) → IMMEDIATE
--    แถวที่ปิด (Is_Enabled = 0) ถูกข้าม (ตกไปกฎถัดไป)
--    ค่าเริ่มต้น: ('*','CASH','DAILY_ROLLUP'), ('*','*','IMMEDIATE'), ('RESERVATION','*','IMMEDIATE')
--      ⇒ ขายหน้าร้านเงินสด = สรุปรายวัน · โอน/QR/บัตร = ทันที · ใบเสร็จการจอง (เช็คอิน/เช็คเอาท์) = ทันทีเสมอ
-- 2) Account_Paid_How.Sales_Doc_Mode (NULL = ใช้ตาราง) — ตั้งเฉพาะรายแหล่งเงิน
-- 3) ค่าตั้ง (Accounting_Integration_Config)
--      Sales_Doc_Policy_Enabled           = '1'  (ไม่มีค่า = '0' = พฤติกรรมเดิม: ติ๊ก "ออกใบกำกับ" เท่านั้นที่ออกรายใบ)
--      Sales_Doc_Local_Slip_Required      = '1'  ขายที่ไม่ได้ออกเอกสารรายใบบน NextAcc (สรุปรายวัน/ปิด)
--                                                 → POS พิมพ์ "ใบกำกับภาษีอย่างย่อ" เลขเรียงในเครื่องให้ลูกค้าทุกบิล
--      Sales_Doc_Send_Local_Invoice_Email = '0'  ไม่ส่งอีเมล PDF "[INV]" ของ TakeTime เมื่อมีเอกสารบน NextAcc แล้ว
-- 4) เลขที่ใบกำกับภาษีอย่างย่อในเครื่อง (POS): Product_Out.Pos_Slip_No + ตารางเลขเรียง Sales_Doc_Slip_Seq
-- 5) แถวขายหน้าร้านที่ยังไม่เคยลงบัญชี (Pos_Rollup_Ref IS NULL) "ก่อนวันนี้" ตอนที่ยังไม่เปิดรวบรายวัน
--    (Nexaacc_PosDailyRollup <> '1') → ทำเครื่องหมาย 'LEGACY' (ความหมายเดียวกับ PHASE18_01) เพื่อไม่ให้
--    นโยบายใหม่ไปรวบย้อนหลังทั้งประวัติ (เอกสารย้อนหลังเข้าเดือนภาษีที่ยื่นแล้ว)
--
-- ⚠ ข้อกฎหมาย (ม.86, ม.86/6): เอกสารสรุปรายวันบน NextAcc เป็น "เอกสารประกอบการลงบัญชี/รายงานภาษีขายรวมรายวัน"
--   ไม่ใช่ใบกำกับภาษีของลูกค้าแต่ละราย — ทุกการขายยังต้องมีใบกำกับภาษีอย่างย่อรายใบให้ลูกค้า
--   (POS พิมพ์ให้เมื่อ Sales_Doc_Local_Slip_Required = 1; การพิมพ์จากระบบคอมพิวเตอร์อาจต้องขอ ภ.พ.06 — ถามสรรพากรพื้นที่)
--
-- ปลอดภัย: รันซ้ำได้ (OBJECT_ID / COL_LENGTH / NOT EXISTS) · ไม่เขียนทับค่าที่ตั้งไว้แล้ว · ไม่ลบข้อมูล
-- หลังรัน: หน้าตั้งค่า "นโยบายเอกสารขาย" (ศูนย์ตั้งค่า → บัญชี & ภาษี) — cache ในเครื่องหมดอายุเองใน 60 วินาที
-- ============================================================================

SET NOCOUNT ON;
GO

-- ── 1) ตารางนโยบาย ─────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Sales_Doc_Policy', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sales_Doc_Policy
    (
        ID            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sales_Doc_Policy PRIMARY KEY,
        Sales_Channel NVARCHAR(30)  NOT NULL,
        Channel_Type  NVARCHAR(20)  NOT NULL,
        Doc_Mode      NVARCHAR(20)  NOT NULL,
        Is_Enabled    BIT           NOT NULL CONSTRAINT DF_Sales_Doc_Policy_Enabled DEFAULT (1),
        Note          NVARCHAR(300) NULL,
        Updated_By    NVARCHAR(100) NULL,
        Updated_At    DATETIME      NULL,
        CONSTRAINT UQ_Sales_Doc_Policy_Channel_Type UNIQUE (Sales_Channel, Channel_Type)
    );
    PRINT N'สร้างตาราง Sales_Doc_Policy';
END
ELSE
    PRINT N'มีตาราง Sales_Doc_Policy อยู่แล้ว';
GO

IF OBJECT_ID('dbo.Sales_Doc_Policy', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Sales_Doc_Policy WHERE Sales_Channel = N'*' AND Channel_Type = N'CASH')
        INSERT INTO dbo.Sales_Doc_Policy (Sales_Channel, Channel_Type, Doc_Mode, Is_Enabled, Note, Updated_By, Updated_At)
        VALUES (N'*', N'CASH', N'DAILY_ROLLUP', 1, N'ค่าเริ่มต้น: ขายเงินสด → รวมเอกสารสรุปรายวัน', N'MIGRATION', GETDATE());

    IF NOT EXISTS (SELECT 1 FROM dbo.Sales_Doc_Policy WHERE Sales_Channel = N'*' AND Channel_Type = N'*')
        INSERT INTO dbo.Sales_Doc_Policy (Sales_Channel, Channel_Type, Doc_Mode, Is_Enabled, Note, Updated_By, Updated_At)
        VALUES (N'*', N'*', N'IMMEDIATE', 1, N'ค่าเริ่มต้น: โอน / QR / บัตร / อื่น ๆ → ออกเอกสารบน NextAcc ทันทีรายใบ', N'MIGRATION', GETDATE());

    IF NOT EXISTS (SELECT 1 FROM dbo.Sales_Doc_Policy WHERE Sales_Channel = N'RESERVATION' AND Channel_Type = N'*')
        INSERT INTO dbo.Sales_Doc_Policy (Sales_Channel, Channel_Type, Doc_Mode, Is_Enabled, Note, Updated_By, Updated_At)
        VALUES (N'RESERVATION', N'*', N'IMMEDIATE', 1, N'ใบเสร็จการจอง (มัดจำ/เช็คอิน/เช็คเอาท์) ออกรายใบทันทีเสมอ — ใบต่อการเข้าพัก', N'MIGRATION', GETDATE());

    PRINT N'ค่าเริ่มต้นนโยบาย: เงินสด=สรุปรายวัน · อื่น ๆ=ทันที · การจอง=ทันที (แถวที่มีอยู่แล้วไม่แตะ)';
END
GO

-- ── 2) ค่าตั้งเฉพาะรายแหล่งเงิน ──────────────────────────────────────────────
IF OBJECT_ID('dbo.Account_Paid_How', 'U') IS NOT NULL AND COL_LENGTH('dbo.Account_Paid_How', 'Sales_Doc_Mode') IS NULL
BEGIN
    ALTER TABLE dbo.Account_Paid_How ADD Sales_Doc_Mode NVARCHAR(20) NULL;
    PRINT N'เพิ่ม Account_Paid_How.Sales_Doc_Mode (NULL = ใช้ตารางนโยบาย)';
END
GO

-- ── 3) ค่าตั้ง ───────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Sales_Doc_Policy_Enabled')
    BEGIN
        INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Sales_Doc_Policy_Enabled', '1');
        PRINT N'เพิ่ม Sales_Doc_Policy_Enabled = 1 (เปิดใช้นโยบายเอกสารขาย)';
    END
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Sales_Doc_Local_Slip_Required')
    BEGIN
        INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Sales_Doc_Local_Slip_Required', '1');
        PRINT N'เพิ่ม Sales_Doc_Local_Slip_Required = 1 (POS พิมพ์ใบกำกับภาษีอย่างย่อเลขเรียงให้ลูกค้าเมื่อไม่ได้ออกเอกสารรายใบ)';
    END
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Sales_Doc_Send_Local_Invoice_Email')
    BEGIN
        INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES ('Sales_Doc_Send_Local_Invoice_Email', '0');
        PRINT N'เพิ่ม Sales_Doc_Send_Local_Invoice_Email = 0 (ไม่ส่ง PDF [INV] ของ TakeTime ซ้ำเมื่อมีเอกสาร NextAcc)';
    END
END
ELSE
    PRINT N'⚠ ไม่มีตาราง Accounting_Integration_Config — ข้ามค่าตั้ง (โค้ดถือว่านโยบายปิด = พฤติกรรมเดิม)';
GO

-- ── 4) เลขที่ใบกำกับภาษีอย่างย่อในเครื่อง ────────────────────────────────────
IF OBJECT_ID('dbo.Product_Out', 'U') IS NOT NULL AND COL_LENGTH('dbo.Product_Out', 'Pos_Slip_No') IS NULL
BEGIN
    ALTER TABLE dbo.Product_Out ADD Pos_Slip_No NVARCHAR(30) NULL;
    PRINT N'เพิ่ม Product_Out.Pos_Slip_No (เลขที่ใบกำกับภาษีอย่างย่อที่ POS พิมพ์ให้ลูกค้า)';
END
GO

IF OBJECT_ID('dbo.Sales_Doc_Slip_Seq', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sales_Doc_Slip_Seq
    (
        Seq_Key    NVARCHAR(30) NOT NULL CONSTRAINT PK_Sales_Doc_Slip_Seq PRIMARY KEY,   -- เช่น ABB-2610 (ต่อเดือน)
        Last_No    INT          NOT NULL CONSTRAINT DF_Sales_Doc_Slip_Seq_Last DEFAULT (0),
        Updated_At DATETIME     NULL
    );
    PRINT N'สร้างตาราง Sales_Doc_Slip_Seq (เลขเรียงใบกำกับภาษีอย่างย่อ)';
END
GO

-- ── 5) กันรวบย้อนหลัง: แถวขายเก่าที่ไม่เคยลงบัญชีตอนรวบรายวันยังปิดอยู่ ─────────────
IF OBJECT_ID('dbo.Product_Out', 'U') IS NOT NULL AND COL_LENGTH('dbo.Product_Out', 'Pos_Rollup_Ref') IS NOT NULL
   AND OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
BEGIN
    DECLARE @rollupOn NVARCHAR(50);
    SELECT @rollupOn = ConfigValue FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_PosDailyRollup';
    IF ISNULL(@rollupOn, '0') <> '1'
    BEGIN
        -- dynamic SQL: SQL Server compile ทั้ง batch ก่อนรัน — อ้างคอลัมน์ที่ยังไม่มี (ยังไม่รัน PHASE18_01) แบบ static
        -- จะ error "Invalid column name" แม้อยู่ใน IF COL_LENGTH(...) IS NOT NULL
        DECLARE @n INT;
        EXEC sp_executesql N'
        UPDATE dbo.Product_Out
           SET Pos_Rollup_Ref = N''LEGACY''
         WHERE Pos_Rollup_Ref IS NULL
           AND Remark = N''ขาย''
           AND (Account_Receipt_ID = ''0'' OR Account_Receipt_ID IS NULL)
           AND CAST(DateTime_Out AS DATE) < CAST(GETDATE() AS DATE);
        SET @cnt = @@ROWCOUNT;', N'@cnt INT OUTPUT', @cnt = @n OUTPUT;
        PRINT N'ทำเครื่องหมาย LEGACY ให้แถวขายเก่าที่ไม่เคยลงบัญชี ' + CAST(@n AS NVARCHAR(20)) + N' แถว (นโยบายมีผลตั้งแต่วันนี้)';
    END
    ELSE
        PRINT N'Nexaacc_PosDailyRollup = 1 อยู่แล้ว — แถวค้างรวบตามเดิม (ไม่แตะ)';
END
GO
IF OBJECT_ID('dbo.Product_Out', 'U') IS NOT NULL AND COL_LENGTH('dbo.Product_Out', 'Pos_Rollup_Ref') IS NULL
    PRINT N'⚠ ยังไม่มีคอลัมน์ Product_Out.Pos_Rollup_Ref — รัน PHASE18_Migration_01 (POS daily roll-up) ก่อน แล้วรันไฟล์นี้ซ้ำ (รันซ้ำได้)';
GO

PRINT '';
PRINT N'เสร็จ — ตั้งค่าได้ที่ ศูนย์ตั้งค่า → บัญชี & ภาษี → "นโยบายเอกสารขาย" (Admin/Settings/SalesDocPolicy)';
PRINT N'⚠ หัว "ใบกำกับภาษีอย่างย่อ" บน NextAcc ต้องตั้งบริษัทเป็นผู้ค้าปลีก (IsRetailApproved) ที่ NextAcc ไม่งั้นพิมพ์เป็น "ใบเสร็จรับเงิน"';
