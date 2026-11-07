-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 17: "ใครเก็บเงินค่าห้อง" เป็นคอลัมน์ (OTA_Collect_Mode / OTA_Collect_Source)
-- ════════════════════════════════════════════════════════════════════════════
-- ปัญหา:
--   ระบบตัดสิน Channel Collect / Hotel Collect จาก "ข้อความในหมายเหตุ" ("(Channel Collect)" / "(Hotel Collect)")
--   · เจ้าหน้าที่แก้หมายเหตุแล้วคำหายได้ · อีเมลแก้ไขการจองเขียนหมายเหตุใหม่ทั้งก้อน
--   · อีเมลที่ไม่บอกว่าใครเก็บ → ระบบรับอีเมล "เดา" เป็น CHANNEL (ค่าเริ่มต้น Email_Rsv_DefaultCollect)
--     และตั้งมัดจำเต็มยอด มีแค่ป้าย "[ระบบเดาให้]" ในหมายเหตุ = ทิศที่แย่ที่สุด (ไม่มีใครเก็บเงิน)
--
-- สิ่งที่สคริปต์นี้ทำ:
--   1) เพิ่ม Reservation.OTA_Collect_Mode   NVARCHAR(10) NULL  — CHANNEL | HOTEL | UNKNOWN
--          Reservation.OTA_Collect_Source NVARCHAR(10) NULL  — EMAIL | GUESS | STAFF | BACKFILL
--   2) ตารางประวัติ Reservation_Collect_Mode_Log (ใครเปลี่ยนโหมด เมื่อไร เพราะอะไร)
--   3) Backfill เฉพาะแถวที่ OTA_Collect_Mode ยังเป็น NULL (ทีละ 5,000 แถว):
--        หมายเหตุมี "(Hotel Collect)"   → HOTEL    (ตรวจ Hotel ก่อน — มีทั้งสองคำ = Hotel)
--        หมายเหตุมี "(Channel Collect)" → CHANNEL
--            ↳ source = GUESS ถ้าหมายเหตุมี "ระบบเดาให้" ไม่งั้น BACKFILL
--            (CHANNEL + GUESS ถูกโค้ดตีความเป็น UNKNOWN = ยังไม่มีใครยืนยัน → หน้างานเห็นยอดค้าง)
--        ไม่มีคำในหมายเหตุ แต่ OTA_Payment_Type จำแนกได้ (ตรรกะเดียวกับ EmailReservationService.ClassifyCollect:
--            คำฝั่ง Hotel ก่อน → คำฝั่ง Channel → คำว่า "channel" ลอย ๆ) → โหมดนั้น / BACKFILL
--        ไม่เข้าข้อใด แต่เป็นใบ OTA (มี OTA_Channel หรือ OTA_Booking_ID) → UNKNOWN / BACKFILL
--        ใบที่ไม่ใช่ OTA → คงเป็น NULL
--      ไม่แตะ Deposit / Remark — การยืนยันโหมดทำผ่านหน้าจอ (ReservationBalance.SetCollectMode) ซึ่งเขียนประวัติ
--      (backfill ไม่เขียนแถวประวัติ — source = BACKFILL บอกที่มาอยู่แล้ว)
--
-- ปลอดภัย: idempotent (รันซ้ำได้ — backfill แตะเฉพาะแถวที่ยัง NULL), มี @DryRun ให้ดูจำนวนก่อน
-- ⚠ รายการคำต้องตรงกับ ChannelCollectWords / HotelCollectWords ใน Class/Services/EmailReservationService.cs
-- ⚠ หลังรันจริง (@DryRun = 0) ให้ recycle app pool — SQL INSERT/UPDATE ของระบบรับอีเมลถูก cache ไว้
--   (ReservationInsertSql / ReservationUpdateSet) จะเริ่มเขียนคอลัมน์ใหม่หลัง recycle เท่านั้น
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- ⚙️ ตั้ง 1 = ดูอย่างเดียวไม่แก้ / 0 = แก้จริง
-- ค่าเริ่มต้น = ดูอย่างเดียว (ปลอดภัย) — ตรวจจำนวนแล้วค่อยเปลี่ยนเป็น 0 รันซ้ำ
DECLARE @DryRun BIT = 1;

IF OBJECT_ID('dbo.Reservation') IS NULL
BEGIN
    PRINT N'ข้าม: ไม่พบตาราง Reservation';
    RETURN;
END

DECLARE @HasPay BIT, @HasCh BIT, @HasBk BIT, @HasMode BIT, @HasSrc BIT, @HasLog BIT;
SELECT @HasPay  = CASE WHEN COL_LENGTH('Reservation', 'OTA_Payment_Type')   IS NULL THEN 0 ELSE 1 END,
       @HasCh   = CASE WHEN COL_LENGTH('Reservation', 'OTA_Channel')        IS NULL THEN 0 ELSE 1 END,
       @HasBk   = CASE WHEN COL_LENGTH('Reservation', 'OTA_Booking_ID')     IS NULL THEN 0 ELSE 1 END,
       @HasMode = CASE WHEN COL_LENGTH('Reservation', 'OTA_Collect_Mode')   IS NULL THEN 0 ELSE 1 END,
       @HasSrc  = CASE WHEN COL_LENGTH('Reservation', 'OTA_Collect_Source') IS NULL THEN 0 ELSE 1 END,
       @HasLog  = CASE WHEN OBJECT_ID('dbo.Reservation_Collect_Mode_Log')   IS NULL THEN 0 ELSE 1 END;

PRINT N'── สถานะก่อนรัน ──';
PRINT N'  Reservation.OTA_Payment_Type   : ' + CASE WHEN @HasPay  = 1 THEN N'มี' ELSE N'ไม่มี (ข้ามการจำแนกจากประเภทการชำระ)' END;
PRINT N'  Reservation.OTA_Channel        : ' + CASE WHEN @HasCh   = 1 THEN N'มี' ELSE N'ไม่มี' END;
PRINT N'  Reservation.OTA_Booking_ID     : ' + CASE WHEN @HasBk   = 1 THEN N'มี' ELSE N'ไม่มี' END;
PRINT N'  Reservation.OTA_Collect_Mode   : ' + CASE WHEN @HasMode = 1 THEN N'มีแล้ว' ELSE N'ยังไม่มี → จะเพิ่ม' END;
PRINT N'  Reservation.OTA_Collect_Source : ' + CASE WHEN @HasSrc  = 1 THEN N'มีแล้ว' ELSE N'ยังไม่มี → จะเพิ่ม' END;
PRINT N'  Reservation_Collect_Mode_Log   : ' + CASE WHEN @HasLog  = 1 THEN N'มีแล้ว' ELSE N'ยังไม่มี → จะสร้าง' END;

-- ── 1) โครงสร้าง (เฉพาะรันจริง) ──────────────────────────────────────────────
IF @DryRun = 0
BEGIN
    IF COL_LENGTH('Reservation', 'OTA_Collect_Mode') IS NULL
    BEGIN
        ALTER TABLE dbo.Reservation ADD OTA_Collect_Mode NVARCHAR(10) NULL;     -- CHANNEL | HOTEL | UNKNOWN
        PRINT N'เพิ่ม Reservation.OTA_Collect_Mode แล้ว';
    END
    IF COL_LENGTH('Reservation', 'OTA_Collect_Source') IS NULL
    BEGIN
        ALTER TABLE dbo.Reservation ADD OTA_Collect_Source NVARCHAR(10) NULL;   -- EMAIL | GUESS | STAFF | BACKFILL
        PRINT N'เพิ่ม Reservation.OTA_Collect_Source แล้ว';
    END
    IF OBJECT_ID('dbo.Reservation_Collect_Mode_Log') IS NULL
    BEGIN
        CREATE TABLE dbo.Reservation_Collect_Mode_Log
        (
            ID             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Reservation_Collect_Mode_Log PRIMARY KEY,
            Reservation_ID INT            NOT NULL,
            Old_Mode       NVARCHAR(10)   NULL,
            New_Mode       NVARCHAR(10)   NULL,
            Old_Source     NVARCHAR(10)   NULL,
            New_Source     NVARCHAR(10)   NULL,
            Reason         NVARCHAR(500)  NULL,
            Changed_By     NVARCHAR(100)  NULL,
            Changed_Date   DATETIME       NOT NULL CONSTRAINT DF_Reservation_Collect_Mode_Log_Date DEFAULT (GETDATE())
        );
        CREATE INDEX IX_Reservation_Collect_Mode_Log_Res
            ON dbo.Reservation_Collect_Mode_Log (Reservation_ID, Changed_Date);
        PRINT N'สร้างตาราง Reservation_Collect_Mode_Log แล้ว';
    END

    SELECT @HasMode = CASE WHEN COL_LENGTH('Reservation', 'OTA_Collect_Mode')   IS NULL THEN 0 ELSE 1 END,
           @HasSrc  = CASE WHEN COL_LENGTH('Reservation', 'OTA_Collect_Source') IS NULL THEN 0 ELSE 1 END;
END

-- ── 2) รวบรวมข้อมูลที่ใช้จำแนก ────────────────────────────────────────────────
-- คอลัมน์ OTA_* / OTA_Collect_* อาจยังไม่มี → อ้างถึงผ่าน dynamic SQL เท่านั้น
-- (อ้างคอลัมน์ที่ไม่มีตรง ๆ ในสคริปต์ = compile error ทั้ง batch แม้อยู่ในกิ่ง IF ที่ไม่ทำงาน)
IF OBJECT_ID('tempdb..#Src') IS NOT NULL DROP TABLE #Src;
CREATE TABLE #Src
(
    ID    INT            NOT NULL PRIMARY KEY,
    Rm    NVARCHAR(MAX)  NOT NULL,
    P     NVARCHAR(400)  NOT NULL,     -- OTA_Payment_Type ตัวเล็ก ตัดช่องว่าง
    IsOta BIT            NOT NULL,
    Mode  NVARCHAR(10)   NULL,
    Src   NVARCHAR(10)   NULL
);

DECLARE @sql NVARCHAR(MAX), @payExpr NVARCHAR(400), @otaExpr NVARCHAR(400);

SET @payExpr = CASE WHEN @HasPay = 1
                    THEN N'LOWER(LTRIM(RTRIM(ISNULL(CAST(r.OTA_Payment_Type AS NVARCHAR(400)), N''''))))'
                    ELSE N'N''''' END;

SET @otaExpr = CASE
    WHEN @HasCh = 1 AND @HasBk = 1
        THEN N'CASE WHEN ISNULL(r.OTA_Channel, N'''') <> N'''' OR ISNULL(r.OTA_Booking_ID, N'''') <> N'''' THEN 1 ELSE 0 END'
    WHEN @HasCh = 1
        THEN N'CASE WHEN ISNULL(r.OTA_Channel, N'''') <> N'''' THEN 1 ELSE 0 END'
    WHEN @HasBk = 1
        THEN N'CASE WHEN ISNULL(r.OTA_Booking_ID, N'''') <> N'''' THEN 1 ELSE 0 END'
    ELSE N'0' END;

SET @sql = N'INSERT INTO #Src (ID, Rm, P, IsOta)
SELECT r.ID,
       ISNULL(CAST(r.Remark AS NVARCHAR(MAX)), N''''),
       ' + @payExpr + N',
       ' + @otaExpr + N'
  FROM dbo.Reservation r'
    + CASE WHEN @HasMode = 1 THEN N'
 WHERE r.OTA_Collect_Mode IS NULL' ELSE N'' END + N';';

EXEC sp_executesql @sql;

-- ── 3) จำแนก (ลำดับตรงกับ ReservationBalance.DetectCollectMode + EmailReservationService.ClassifyCollect) ──
UPDATE #Src
SET Mode = CASE
        -- 3a) หมายเหตุจากระบบรับอีเมล — Hotel ก่อน
        WHEN Rm LIKE N'%(Hotel Collect)%'   THEN N'HOTEL'
        WHEN Rm LIKE N'%(Channel Collect)%' THEN N'CHANNEL'
        -- 3b) OTA_Payment_Type: คำฝั่ง Hotel (HotelCollectWords) ก่อน
        WHEN P <> N'' AND (
                P LIKE N'%hotel collect%'       OR P LIKE N'%property collect%'   OR P LIKE N'%collect at property%'
             OR P LIKE N'%collect from guest%'  OR P LIKE N'%pay at hotel%'       OR P LIKE N'%pay at property%'
             OR P LIKE N'%pay at the hotel%'    OR P LIKE N'%payment at hotel%'   OR P LIKE N'%pay on arrival%'
             OR P LIKE N'%payment on arrival%'  OR P LIKE N'%pay at check-in%'    OR P LIKE N'%pay at checkin%'
             OR P LIKE N'%pah%'                 OR P LIKE N'%cash at hotel%'      OR P LIKE N'%direct payment%')
            THEN N'HOTEL'
        -- 3c) คำฝั่ง Channel (ChannelCollectWords) แล้วคำว่า "channel" ลอย ๆ เป็นตัวสุดท้าย
        WHEN P <> N'' AND (
                P LIKE N'%channel collect%'     OR P LIKE N'%expedia collect%'    OR P LIKE N'%agoda collect%'
             OR P LIKE N'%booking.com collect%' OR P LIKE N'%ota collect%'        OR P LIKE N'%prepaid%'
             OR P LIKE N'%pre-paid%'            OR P LIKE N'%pre paid%'           OR P LIKE N'%paid online%'
             OR P LIKE N'%paid to ota%'         OR P LIKE N'%virtual card%'       OR P LIKE N'%virtual credit%'
             OR P LIKE N'%vcc%'                 OR P LIKE N'%已付%'               OR P LIKE N'%bank transfer to ota%'
             OR P LIKE N'%channel%')
            THEN N'CHANNEL'
        -- 3d) ใบ OTA ที่ตัดสินไม่ได้
        WHEN IsOta = 1 THEN N'UNKNOWN'
        ELSE NULL END;

UPDATE #Src
SET Src = CASE WHEN (Rm LIKE N'%(Hotel Collect)%' OR Rm LIKE N'%(Channel Collect)%')
                    AND Rm LIKE N'%ระบบเดาให้%'
               THEN N'GUESS' ELSE N'BACKFILL' END
WHERE Mode IS NOT NULL;

DELETE FROM #Src WHERE Mode IS NULL;   -- ใบที่ไม่ใช่ OTA → คง NULL

-- ── 4) รายงาน ────────────────────────────────────────────────────────────────
DECLARE @nHotel INT, @nChannel INT, @nGuess INT, @nUnknown INT, @nAll INT;
SELECT @nHotel   = SUM(CASE WHEN Mode = N'HOTEL' THEN 1 ELSE 0 END),
       @nChannel = SUM(CASE WHEN Mode = N'CHANNEL' AND Src <> N'GUESS' THEN 1 ELSE 0 END),
       @nGuess   = SUM(CASE WHEN Mode = N'CHANNEL' AND Src = N'GUESS' THEN 1 ELSE 0 END),
       @nUnknown = SUM(CASE WHEN Mode = N'UNKNOWN' THEN 1 ELSE 0 END),
       @nAll     = COUNT(*)
  FROM #Src;

PRINT N'── ผลการจำแนก (เฉพาะแถวที่ OTA_Collect_Mode ยังว่าง) ──';
PRINT N'  HOTEL                       : ' + CAST(ISNULL(@nHotel, 0) AS NVARCHAR(20));
PRINT N'  CHANNEL (ยืนยันจากข้อมูล)     : ' + CAST(ISNULL(@nChannel, 0) AS NVARCHAR(20));
PRINT N'  CHANNEL + GUESS (= UNKNOWN) : ' + CAST(ISNULL(@nGuess, 0) AS NVARCHAR(20));
PRINT N'  UNKNOWN (OTA ไม่มีข้อมูล)     : ' + CAST(ISNULL(@nUnknown, 0) AS NVARCHAR(20));
PRINT N'  รวมที่จะเขียน                 : ' + CAST(ISNULL(@nAll, 0) AS NVARCHAR(20));

SELECT Mode, Src, COUNT(*) AS Reservations
  FROM #Src
 GROUP BY Mode, Src
 ORDER BY Mode, Src;

-- ตัวอย่างใบที่ต้องให้เจ้าหน้าที่ยืนยัน (เดา / ไม่รู้) — 50 ใบล่าสุด
SELECT TOP 50 s.ID AS Reservation_ID, s.Mode, s.Src, s.P AS OTA_Payment_Type_Lower,
       LEFT(s.Rm, 200) AS Remark_Head
  FROM #Src s
 WHERE s.Mode = N'UNKNOWN' OR s.Src = N'GUESS'
 ORDER BY s.ID DESC;

-- ── 5) เขียนจริง ทีละ 5,000 แถว ─────────────────────────────────────────────
IF @DryRun = 1
BEGIN
    PRINT N'';
    PRINT N'>> DRY RUN — ยังไม่ได้แก้อะไร (ไม่เพิ่มคอลัมน์ ไม่ backfill) ตั้ง @DryRun = 0 แล้วรันอีกครั้งเพื่อแก้จริง';
END
ELSE IF @HasMode = 0 OR @HasSrc = 0
BEGIN
    PRINT N'!! ไม่พบคอลัมน์ OTA_Collect_Mode / OTA_Collect_Source หลังเพิ่ม — ข้าม backfill';
END
ELSE
BEGIN
    DECLARE @batch INT = 1, @done INT = 0;
    WHILE @batch > 0
    BEGIN
        EXEC sp_executesql
            N'UPDATE TOP (5000) r
                 SET r.OTA_Collect_Mode = s.Mode,
                     r.OTA_Collect_Source = s.Src
                FROM dbo.Reservation r
                INNER JOIN #Src s ON s.ID = r.ID
               WHERE r.OTA_Collect_Mode IS NULL;
              SET @cnt = @@ROWCOUNT;',
            N'@cnt INT OUTPUT',
            @cnt = @batch OUTPUT;
        SET @done = @done + @batch;
    END
    PRINT N'>> backfill แล้ว ' + CAST(@done AS NVARCHAR(20)) + N' แถว';
    PRINT N'>> อย่าลืม recycle app pool เพื่อให้ระบบรับอีเมลเริ่มเขียน OTA_Collect_Mode / OTA_Collect_Source';
END

DROP TABLE #Src;
GO
