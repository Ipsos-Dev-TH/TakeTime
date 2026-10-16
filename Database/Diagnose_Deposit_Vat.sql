-- ============================================================================
-- ทำไมใบมัดจำไม่แยก "ภาษีขายรอเรียกเก็บ" (21913) — ตรวจให้ครบทุกเงื่อนไข
-- ============================================================================
-- อ่านอย่างเดียว รันได้เลย · ตอบจากค่าจริงในฐานข้อมูล ไม่ใช่การเดา
--
-- โค้ดตัดสินใจด้วยนิพจน์เดียวนี้ (AccountingDataMapper.MapReceiptToDocument):
--     splitVat            = hasVat AND depositVatAtReceipt
--     DepositOutputVatDeferred = isDeposit AND hasVat AND depositVatAtReceipt AND deferOutputVat
--   โดย  hasVat              ← Business_Info.Use_Vat
--        depositVatAtReceipt ← Deposit_Vat_Recognition = 'RECEIPT'
--        deferOutputVat      ← Deposit_Defer_Output_Vat = '1'
--   และต้องผูก mapping OUTPUT_VAT_DEFERRED → 21913 ไว้ด้วย ไม่งั้น fallback ลง 21911
--
-- ⚠ "ไม่ครบข้อใดข้อหนึ่ง" = ใบมัดจำออกมาเป็น Cr เงินรับล่วงหน้าเต็มก้อน ไม่มีบรรทัดภาษี
--   และเดิมไม่มีอะไรเตือนเลย (ค่าเริ่มต้น CHECKOUT ไม่เข้าเงื่อนไขเตือนข้อไหน)
-- ============================================================================

SET NOCOUNT ON;

DECLARE @useVatRaw   NVARCHAR(50);
DECLARE @vatMode     NVARCHAR(50);
DECLARE @deferRaw    NVARCHAR(50);
DECLARE @deferCode   NVARCHAR(50);
DECLARE @deferInCoa  INT = 0;
DECLARE @advCode     NVARCHAR(50);

SELECT TOP 1 @useVatRaw = CONVERT(NVARCHAR(50), Use_Vat) FROM dbo.Business_Info;

IF OBJECT_ID('dbo.Accounting_Integration_Config','U') IS NOT NULL
BEGIN
    SELECT @vatMode  = ConfigValue FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Deposit_Vat_Recognition';
    SELECT @deferRaw = ConfigValue FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Deposit_Defer_Output_Vat';
END

IF OBJECT_ID('dbo.Accounting_Account_Mapping','U') IS NOT NULL
BEGIN
    SELECT TOP 1 @deferCode = Nexaacc_AccountCode
      FROM dbo.Accounting_Account_Mapping
     WHERE TakeTime_Code = 'OUTPUT_VAT_DEFERRED' AND ISNULL(Is_Active,1) = 1;

    SELECT TOP 1 @advCode = Nexaacc_AccountCode
      FROM dbo.Accounting_Account_Mapping
     WHERE TakeTime_Code = 'ADVANCE_DEPOSIT' AND ISNULL(Is_Active,1) = 1;
END

IF OBJECT_ID('dbo.Accounting_Nexaacc_Accounts','U') IS NOT NULL AND @deferCode IS NOT NULL
    SELECT @deferInCoa = COUNT(*) FROM dbo.Accounting_Nexaacc_Accounts WHERE Account_Code = @deferCode;

DECLARE @hasVat  BIT = CASE WHEN @useVatRaw IN ('1','True','true','TRUE') THEN 1 ELSE 0 END;
DECLARE @atRcpt  BIT = CASE WHEN UPPER(ISNULL(@vatMode,'CHECKOUT')) = 'RECEIPT' THEN 1 ELSE 0 END;
DECLARE @defer   BIT = CASE WHEN ISNULL(@deferRaw,'0') IN ('1','true','True','TRUE') THEN 1 ELSE 0 END;

-- ── 0) ใครแก้ค่าเมื่อไหร่ (หลักฐานย้อนหลัง) ─────────────────────────────────
PRINT N'--- 0) ค่าถูกแก้ล่าสุดเมื่อไหร่ ---';
IF OBJECT_ID('dbo.Accounting_Integration_Config','U') IS NOT NULL
    SELECT ConfigKey AS [คีย์], ConfigValue AS [ค่า], Updated_Date AS [แก้ล่าสุด]
      FROM dbo.Accounting_Integration_Config
     WHERE ConfigKey IN ('Deposit_Vat_Recognition','Deposit_Defer_Output_Vat')
     ORDER BY ConfigKey;
-- ⚠ ก่อน build 28/08/2026: การกด "บันทึก" การ์ด Sync Settings เขียนสองคีย์นี้ทับทุกครั้ง
--   ตามค่าบนหน้าจอ (แม้ไม่ได้ตั้งใจแก้) — Updated_Date จึงอาจเป็นแค่วันที่มีคนกดเซฟเรื่องอื่น
--   หลัง build ใหม่: เขียนเฉพาะเมื่อเปลี่ยนจริง + log ใคร/เก่า→ใหม่ ใน Logs

PRINT '';
PRINT N'--- 0b) ประวัติการเปลี่ยนนโยบาย/กดปุ่มตั้งค่าแนะนำ (90 วัน) ---';
IF OBJECT_ID('dbo.Logs','U') IS NOT NULL
    SELECT TOP 30 LogDateTime AS [เวลา], LogBy AS [โดย],
           LEFT(CAST(LogDetail AS NVARCHAR(MAX)), 300) AS [รายละเอียด]
      FROM dbo.Logs
     WHERE LogDateTime >= DATEADD(DAY, -90, GETDATE())
       AND (CAST(LogDetail AS NVARCHAR(MAX)) LIKE N'%Deposit_Vat_Recognition%'
            OR CAST(LogDetail AS NVARCHAR(MAX)) LIKE N'%Deposit_Defer_Output_Vat%'
            OR CAST(LogDetail AS NVARCHAR(MAX)) LIKE N'%ApplyRecommendedPreset%'
            OR CAST(LogDetail AS NVARCHAR(MAX)) LIKE N'%นโยบาย VAT มัดจำ%')
     ORDER BY LogDateTime DESC;

-- ── 1) เงื่อนไขทีละข้อ — ข้อไหนเป็น ❌ คือสาเหตุ ───────────────────────────
PRINT N'--- 1) เงื่อนไขที่ต้องครบทั้งหมด ---';
SELECT
    N'1. กิจการจด VAT (Business_Info.Use_Vat)' AS [เงื่อนไข],
    ISNULL(@useVatRaw, N'(ไม่มีค่า)')          AS [ค่าที่ตั้งไว้],
    CASE WHEN @hasVat = 1 THEN N'✅ ผ่าน' ELSE N'❌ ไม่ผ่าน — ต้องเป็น True/true/1' END AS [ผล]
UNION ALL SELECT
    N'2. แยก VAT ตอนรับมัดจำ (Deposit_Vat_Recognition)',
    ISNULL(@vatMode, N'(ไม่ได้ตั้ง → CHECKOUT)'),
    CASE WHEN @atRcpt = 1 THEN N'✅ ผ่าน (RECEIPT)'
         ELSE N'❌ ไม่ผ่าน — ต้องตั้งเป็น RECEIPT (CHECKOUT = ไม่แยก VAT เลย)' END
UNION ALL SELECT
    N'3. พัก VAT ที่ 21913 (Deposit_Defer_Output_Vat)',
    ISNULL(@deferRaw, N'(ไม่ได้ตั้ง → 0)'),
    CASE WHEN @defer = 1 THEN N'✅ ผ่าน'
         ELSE N'⚠ ปิดอยู่ — VAT จะเข้า 21911 ทันที (ไม่ใช่ 21913)' END
UNION ALL SELECT
    N'4. ผูกบัญชี OUTPUT_VAT_DEFERRED',
    ISNULL(@deferCode, N'(ยังไม่ผูก)'),
    CASE WHEN @deferCode IS NULL THEN N'❌ ยังไม่ผูก mapping'
         WHEN @deferInCoa = 0 THEN N'❌ ผูกไว้แต่ผังบัญชี NextAcc ไม่มีรหัสนี้ (กด Sync ผังบัญชี)'
         ELSE N'✅ ผ่าน' END
UNION ALL SELECT
    N'5. ผูกบัญชี ADVANCE_DEPOSIT (เงินรับล่วงหน้า)',
    ISNULL(@advCode, N'(ยังไม่ผูก)'),
    CASE WHEN @advCode IS NULL THEN N'❌ ยังไม่ผูก' ELSE N'✅ ผ่าน' END;

-- ── 2) สรุปว่าใบมัดจำใบต่อไปจะลงบัญชีอย่างไร ───────────────────────────────
PRINT '';
PRINT N'--- 2) ใบมัดจำ 1,000 บาท ใบต่อไปจะลงแบบนี้ ---';
SELECT CASE
    WHEN @hasVat = 1 AND @atRcpt = 1 AND @defer = 1 AND @deferCode IS NOT NULL AND @deferInCoa > 0 THEN
        N'Dr แหล่งเงิน 1,000.00 / Cr ' + ISNULL(@advCode,'21510') + N' 934.58 + Cr ' + @deferCode + N' 65.42'
        + N'  ⇒ พัก VAT ไว้ ไม่เข้า ภ.พ.30 จนเช็คเอาท์  ✅ ตรงกับที่ต้องการ'
    WHEN @hasVat = 1 AND @atRcpt = 1 THEN
        N'Dr แหล่งเงิน 1,000.00 / Cr ' + ISNULL(@advCode,'21510') + N' 934.58 + Cr 21911 (ภาษีขาย) 65.42'
        + N'  ⇒ VAT เข้า ภ.พ.30 เดือนที่รับมัดจำ'
    ELSE
        N'Dr แหล่งเงิน 1,000.00 / Cr ' + ISNULL(@advCode,'21510') + N' 1,000.00 (ไม่มีบรรทัดภาษีขาย)'
        + N'  ⇒ นี่คืออาการที่เจอ — VAT รับรู้ทั้งก้อนตอนเช็คเอาท์'
    END AS [ผลลัพธ์];

-- ── 3) วิธีแก้ตามผลข้างบน ──────────────────────────────────────────────────
PRINT '';
PRINT N'--- 3) วิธีแก้ ---';
IF @hasVat = 0
    PRINT N'▶ ข้อ 1 ไม่ผ่าน: แก้ Business_Info.Use_Vat เป็น True (มีผลกับเอกสารรับทุกใบ ไม่ใช่แค่มัดจำ)';
IF @atRcpt = 0
    PRINT N'▶ ข้อ 2 ไม่ผ่าน (สาเหตุที่พบบ่อยที่สุด): ตั้ง Deposit_Vat_Recognition = RECEIPT
   UPDATE dbo.Accounting_Integration_Config SET ConfigValue = ''RECEIPT'' WHERE ConfigKey = ''Deposit_Vat_Recognition'';
   (ถ้ายังไม่มีแถว) INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES (''Deposit_Vat_Recognition'', ''RECEIPT'');';
IF @defer = 0
    PRINT N'▶ ข้อ 3 ปิดอยู่: ถ้าต้องการพัก VAT ที่ 21913 ให้ตั้ง Deposit_Defer_Output_Vat = 1
   UPDATE dbo.Accounting_Integration_Config SET ConfigValue = ''1'' WHERE ConfigKey = ''Deposit_Defer_Output_Vat'';
   (ถ้ายังไม่มีแถว) INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES (''Deposit_Defer_Output_Vat'', ''1'');';
IF @deferCode IS NULL OR @deferInCoa = 0
    PRINT N'▶ ข้อ 4 ไม่ผ่าน: ผูก OUTPUT_VAT_DEFERRED กับบัญชี 21913 ในหน้า NextAcc → หัวข้อผังบัญชี แล้วกด Sync ผังบัญชี';

PRINT '';
PRINT N'หลังแก้: ใบมัดจำที่ออก "หลังจากนี้" จะถูกต้องเอง';
PRINT N'ใบที่ออกไปแล้ว → กด Retry ในคิว (void แล้วสร้างใหม่เลขเดิม) หรือให้ผู้ทำบัญชีออกใบปรับปรุง';
PRINT N'ตรวจซ้ำได้ที่ปุ่ม 🩺 ตรวจสุขภาพการเชื่อมต่อ — ตอนนี้จะบอกผลลัพธ์ที่จะเกิดขึ้นทุกครั้ง';

-- ── 5) หลักฐานใบจริง: คำขอที่ส่งไป NextAcc ของการจองที่ระบุ ─────────────────
-- ใส่เลขการจองที่สงสัย (เช่นใบในภาพ = 149333) แล้วดูว่า vatRate อะไรถูกส่งไปจริง
DECLARE @ResId INT = 149333;   -- <<< แก้เป็นเลขการจองที่ต้องการตรวจ

PRINT '';
PRINT N'--- 5) คำขอใบมัดจำของการจอง #' + CAST(@ResId AS NVARCHAR(10)) + N' ที่ส่งไป NextAcc จริง ---';
PRINT N'    ดูในคอลัมน์คำขอ: "vatRate":7 = แยก VAT / "vatRate":0 = ไม่แยก (อาการที่เจอ)';
PRINT N'    "depositOutputVatDeferred":true = สั่งพักที่ 21913';
IF OBJECT_ID('dbo.Accounting_Sync_Log','U') IS NOT NULL
    SELECT TOP 5 Created_Date AS [เวลา], Action AS [ปลายทาง], HTTP_Status,
           LEFT(CAST(Request_Payload AS NVARCHAR(MAX)), 1500) AS [คำขอที่ส่งไป]
      FROM dbo.Accounting_Sync_Log
     WHERE Created_Date >= DATEADD(DAY, -180, GETDATE())
       AND CAST(Request_Payload AS NVARCHAR(MAX)) LIKE '%"isDeposit":true%'
       AND CAST(Request_Payload AS NVARCHAR(MAX)) LIKE '%RES-' + CAST(@ResId AS NVARCHAR(10)) + '%'
     ORDER BY ID DESC;

-- ── 6) มัดจำค้าง (ยังไม่เช็คเอาท์) ที่จะโดนตอน clearing ─────────────────────
-- โค้ดรุ่นใหม่ตัดสิน clearing จากหลักฐานใบจริงแล้ว (ไม่ใช่คอนฟิกวันนี้)
-- รายการนี้ไว้ให้ผู้ทำบัญชีรู้ว่ามีใบไหนที่ลงไว้คนละนโยบายกับปัจจุบัน
PRINT '';
PRINT N'--- 6) ใบมัดจำของการจองที่ยังไม่จบ (ตรวจนโยบายที่ใช้ตอนลงจากข้อ 5 รายใบ) ---';
IF OBJECT_ID('dbo.Account_Receipt','U') IS NOT NULL
    SELECT TOP 30 r.Account_Receipt_ID AS [เลขใบเสร็จ], r.Reservation_ID AS [การจอง],
           r.Total_Price AS [ยอด], r.Created_Date AS [วันที่รับมัดจำ], rv.Status AS [สถานะการจอง]
      FROM dbo.Account_Receipt r
      JOIN dbo.Reservation rv ON rv.ID = TRY_CONVERT(INT, r.Reservation_ID)
     WHERE ISNULL(r.IsDeposit, 0) = 1
       AND rv.Status NOT IN (N'เสร็จสิ้น', N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
     ORDER BY r.Created_Date DESC;

-- ── 4) ใบมัดจำล่าสุดที่ออกไปแล้ว (ไว้เทียบว่าใบไหนได้รับผลกระทบ) ───────────
PRINT '';
PRINT N'--- 4) ใบมัดจำ 20 ใบล่าสุด ---';
IF OBJECT_ID('dbo.Account_Receipt','U') IS NOT NULL
    SELECT TOP 20
           r.Account_Receipt_ID AS [เลขใบเสร็จ],
           r.Reservation_ID     AS [การจอง],
           r.Total_Price        AS [ยอด],
           r.Created_Date       AS [วันที่]
      FROM dbo.Account_Receipt r
     WHERE ISNULL(r.IsDeposit, 0) = 1
       AND r.Created_Date >= DATEADD(MONTH, -3, GETDATE())
     ORDER BY r.Created_Date DESC;
