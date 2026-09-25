-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 16: แยกลูกค้า OTA ที่ถูกยุบรวมเพราะ "เบอร์โทรค่าคงที่"
-- ════════════════════════════════════════════════════════════════════════════
-- ปัญหา (พบ ก.ย. 2569):
--   STAAH/Expedia ส่งเบอร์โทรเป็นค่าคงที่ '01111111111111' มาทุกใบ แทนเบอร์ลูกค้าจริง
--   เบอร์โทรเป็น key ของตาราง Customer (Reservation.Customer_MobilePhone → Customer.MobilePhone)
--   ⇒ ใบจอง Expedia ทุกใบผูกกับลูกค้า "แถวเดียวกัน"
--   ⇒ ชื่อผู้จองที่แสดงกลายเป็นชื่อคนแรกที่เคยจองด้วยค่านี้ ตลอดไป
--      (โค้ดเดิม EnsureCustomer เจอแถวแล้ว return ทันที ไม่เคยแก้ชื่อ)
--   เคสจริง: ใบจอง 149419 ผู้เข้าพัก "Samput Ekapan" แต่หน้าเว็บขึ้น "Kanthicha Suparojwathin"
--
-- ปัญหาที่ 2: เบอร์ต่างประเทศถูกทำเพี้ยน — โค้ดรุ่นเก่าตัด '+' ทิ้งแล้วเติม 0 นำหน้า
--   "+852 9545 6676" (แขกฮ่องกง) ถูกเก็บเป็น "085295456676" ⇒ ห้ามย้ายไป OTA_ key (เบอร์จริงจะหาย)
--   ให้แก้กลับเป็น "+85295456676" (รูปเดียวกับที่โค้ดใหม่ GuestPhone เก็บ) แทน
-- ปัญหาที่ 3: โค้ดรุ่นเก่าเคยเอา "เลขในวงเล็บของเลขที่จอง" (PIN ของ Booking.com/Expedia)
--   มาเก็บเป็นเบอร์ เช่น "1114600000001755 (5904188856)" → เบอร์ "5904188856" ⇒ เป็นค่าหลอก
--
-- สิ่งที่สคริปต์นี้ทำ (เฉพาะใบจอง OTA ที่มี OTA_Booking_ID):
--   1) จัดประเภทใบจองตาม Customer_MobilePhone:
--        PIN  = เบอร์ตรงกับเลขในวงเล็บของ OTA_Booking_ID (หรือ '0' + เลขนั้น) → OTA_ key
--        INTL = ยาว ≥ 11 ขึ้นต้น '0' ตามด้วยรหัสประเทศที่รู้จัก และไม่ใช่เลขซ้ำ → '+' + ตัด 0 หน้า
--        FAKE = "ไม่ใช่เบอร์ไทยจริง" (ยาวไม่ใช่ 9/10 หลัก หรือเลขซ้ำทั้งชุด) → OTA_ key
--   2) OTA_ key ประจำใบจาก OTA_Booking_ID → 'OTA_' + เลขในวงเล็บ (หรือ 12 หลักท้าย)
--   3) สร้างแถว Customer ของ key ใหม่ (PIN/FAKE: ชื่อผู้เข้าพักของใบนั้น (OTA_Guest_Name);
--      INTL: ชื่อลูกค้าเดิมของเบอร์นั้น — เป็นแขกคนเดิม ใบจองหลายใบของแขกประจำรวมอยู่แถวเดียวกัน)
--   4) ย้าย Reservation ไปผูก key ใหม่
--   ❗ ไม่ลบแถวลูกค้ารวมของเดิม และไม่แตะ Account_Receipt.Customer_ID
--      (ใบเสร็จที่ออกไปแล้ว = เอกสารบัญชี ต้องให้คนตัดสินใจเอง — สคริปต์รายงานจำนวนให้)
--
-- ปลอดภัย: รันซ้ำได้ (idempotent — ใบที่ย้ายแล้วขึ้นต้นด้วย 'OTA_' หรือ '+' จะไม่เข้าเงื่อนไขอีก)
--          มี @DryRun ให้ดูก่อนว่าจะแก้อะไรบ้าง
-- ⚠ รายการรหัสประเทศต้องตรงกับ GuestPhone.KnownCountryCodes (Class/Services/GuestPhone.cs)
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- ⚙️ ตั้ง 1 = ดูอย่างเดียวไม่แก้ / 0 = แก้จริง
-- ค่าเริ่มต้น = ดูอย่างเดียว (ปลอดภัย) — ตรวจรายการที่จะแก้แล้วค่อยเปลี่ยนเป็น 0 รันซ้ำ
DECLARE @DryRun BIT = 1;

IF COL_LENGTH('Reservation', 'OTA_Booking_ID') IS NULL
BEGIN
    PRINT N'ข้าม: ยังไม่มีคอลัมน์ Reservation.OTA_Booking_ID (รัน PHASE18_Migration_12 ก่อน)';
    RETURN;
END

-- ── 1) รวบรวมใบจองที่ต้องแก้ ────────────────────────────────────────────────
IF OBJECT_ID('tempdb..#Fix') IS NOT NULL DROP TABLE #Fix;

-- ผู้สมัครทั้งหมด = ใบจอง OTA ที่ยังไม่ได้ย้าย แล้วค่อยจัดประเภท (Kind) ทีละกฎ ใบที่ไม่เข้ากฎไหนถูกลบทิ้ง
SELECT
    r.ID                AS ReservationID,
    r.Customer_MobilePhone AS OldPhone,
    LTRIM(RTRIM(ISNULL(r.OTA_Booking_ID, N''))) AS BookingId,
    LTRIM(RTRIM(ISNULL(r.OTA_Guest_Name, N''))) AS OtaGuestName,
    CAST(N'' AS NVARCHAR(10)) AS Kind,
    CAST(N'' AS NVARCHAR(40)) AS Pin,
    CAST(N'' AS NVARCHAR(30)) AS NewKey,
    CAST(N'' AS NVARCHAR(200)) AS NewName
INTO #Fix
FROM Reservation r
WHERE ISNULL(r.Customer_MobilePhone, N'') <> N''
  AND r.Customer_MobilePhone NOT LIKE N'OTA[_]%'          -- ย้ายไปแล้ว (OTA_ key)
  AND r.Customer_MobilePhone NOT LIKE N'+%'               -- แก้เป็นเบอร์ต่างประเทศแล้ว
  AND ISNULL(r.OTA_Booking_ID, N'') <> N'';               -- ต้องมีเลขจองไว้ทำ key

-- เลขในวงเล็บของเลขที่จอง (PIN): "1114600000001755 (5904188856)" → 5904188856
-- (CASE กันความยาวติดลบ — SUBSTRING ความยาว < 0 = error ทั้งสคริปต์)
UPDATE #Fix
SET Pin = SUBSTRING(BookingId,
                    CHARINDEX(N'(', BookingId) + 1,
                    CASE WHEN CHARINDEX(N')', BookingId) > CHARINDEX(N'(', BookingId)
                         THEN CHARINDEX(N')', BookingId) - CHARINDEX(N'(', BookingId) - 1
                         ELSE 0 END)
WHERE CHARINDEX(N'(', BookingId) > 0
  AND CHARINDEX(N')', BookingId) > CHARINDEX(N'(', BookingId);

UPDATE #Fix SET Pin = LTRIM(RTRIM(Pin));
UPDATE #Fix SET Pin = N'' WHERE LEN(Pin) < 6 OR Pin LIKE N'%[^0-9]%';

-- 1a) PIN = เบอร์ตรงกับเลขในวงเล็บ (รุ่นเก่าเติม 0 ให้ PIN 9 หลัก จึงเทียบ '0' + PIN ด้วย)
UPDATE #Fix
SET Kind = N'PIN'
WHERE Pin <> N''
  AND (LTRIM(RTRIM(OldPhone)) = Pin OR LTRIM(RTRIM(OldPhone)) = N'0' + Pin);

-- 1b) INTL = เบอร์ต่างประเทศที่ถูกเติม 0: '0' + รหัสประเทศ + เลขหมาย, ตัวเลขล้วน 11-16 ตัว
--     ("0" + สูงสุด 15 หลักตามมาตรฐาน E.164) — ไม่ใช่เลขเดียวกันทั้งชุด และไม่มีเลขซ้ำติดกัน ≥ 7 ตัว
--     (01111111111111 ขึ้นต้น "01" เหมือนรหัส +1 แต่เป็นค่าคงที่ ⇒ ต้องตกไป FAKE)
--     รหัส 66 ไม่อยู่ในรายการ: เบอร์ไทยถูกแปลงเป็น 0 ตั้งแต่ตอนรับเข้ามาแล้ว
UPDATE #Fix
SET Kind = N'INTL',
    NewKey = N'+' + SUBSTRING(OldPhone, 2, LEN(OldPhone) - 1)
WHERE Kind = N''
  AND LEN(OldPhone) BETWEEN 11 AND 16
  AND OldPhone NOT LIKE N'%[^0-9]%'
  AND LEFT(OldPhone, 1) = N'0'
  AND (    SUBSTRING(OldPhone, 2, 1) IN (N'1', N'7')
        OR SUBSTRING(OldPhone, 2, 2) IN (
               N'20', N'27', N'30', N'31', N'32', N'33', N'34', N'36', N'39', N'40', N'41',
               N'43', N'44', N'45', N'46', N'47', N'48', N'49',
               N'51', N'52', N'53', N'54', N'55', N'56', N'57', N'58',
               N'60', N'61', N'62', N'63', N'64', N'65',
               N'81', N'82', N'84', N'86',
               N'90', N'91', N'92', N'93', N'94', N'95')
        OR SUBSTRING(OldPhone, 2, 3) IN (
               N'852', N'853', N'855', N'856', N'880', N'886',
               N'971', N'972', N'973', N'974', N'977') )
  -- หลังหลักแรกไม่ใช่เลขเดียวกันทั้งหมด
  AND REPLACE(SUBSTRING(OldPhone, 2, LEN(OldPhone) - 1), SUBSTRING(OldPhone, 2, 1), N'') <> N''
  -- ไม่มีเลขซ้ำติดกัน 7 ตัว (เกณฑ์เดียวกับ GuestPhone.LooksFake)
  AND OldPhone NOT LIKE N'%0000000%' AND OldPhone NOT LIKE N'%1111111%'
  AND OldPhone NOT LIKE N'%2222222%' AND OldPhone NOT LIKE N'%3333333%'
  AND OldPhone NOT LIKE N'%4444444%' AND OldPhone NOT LIKE N'%5555555%'
  AND OldPhone NOT LIKE N'%6666666%' AND OldPhone NOT LIKE N'%7777777%'
  AND OldPhone NOT LIKE N'%8888888%' AND OldPhone NOT LIKE N'%9999999%';

-- 1c) FAKE = ไม่ใช่เบอร์ไทยจริง (กฎเดิมของสคริปต์นี้)
UPDATE #Fix
SET Kind = N'FAKE'
WHERE Kind = N''
  AND (
        -- ความยาวไม่ใช่เบอร์ไทย (เบอร์บ้าน 9 / มือถือ 10)
        LEN(OldPhone) NOT IN (9, 10)
        -- หรือหลังหลักแรกเป็นเลขเดียวกันทั้งหมด (0111111111 / 0000000000 / 0999999999)
        OR (LEN(OldPhone) > 6
            AND REPLACE(SUBSTRING(OldPhone, 2, LEN(OldPhone) - 1),
                        SUBSTRING(OldPhone, 2, 1), N'') = N'')
      );

-- ใบที่ไม่เข้ากฎไหนเลย = เบอร์ปกติ ไม่ต้องแก้
DELETE FROM #Fix WHERE Kind = N'';

-- ── 2) สร้าง OTA_ key ประจำใบจากเลขที่จอง (เฉพาะ PIN/FAKE — INTL ได้ key '+…' แล้ว) ──────
-- เลขในวงเล็บก่อน: "1114600000001741 (2557213715)" → 2557213715
UPDATE #Fix
SET NewKey = N'OTA_' + Pin
WHERE Kind IN (N'PIN', N'FAKE')
  AND Pin <> N'';

-- ไม่มีวงเล็บ → 12 หลักท้ายของท่อนแรก
UPDATE #Fix
SET NewKey = N'OTA_' + RIGHT(
        CASE WHEN CHARINDEX(N' ', BookingId) > 0
             THEN LEFT(BookingId, CHARINDEX(N' ', BookingId) - 1)
             ELSE BookingId END,
        12)
WHERE NewKey = N''
  AND Kind IN (N'PIN', N'FAKE');

-- ทิ้งอันที่ยังทำ key ไม่ได้ / key สั้นเกินจนเสี่ยงซ้ำ
DELETE FROM #Fix WHERE NewKey = N'' OR LEN(NewKey) < 10;

-- ── 3) ชื่อที่จะใช้ ─────────────────────────────────────────────────────────
--   PIN/FAKE: ชื่อจาก OTA ก่อน, ไม่มีก็ใช้ชื่อลูกค้าเดิม (แถวรวม = ชื่อคนอื่น จึงเป็นทางเลือกสุดท้าย)
--   INTL    : ชื่อลูกค้าเดิมของเบอร์นั้นก่อน (เป็นแขกคนเดิมจริง), ไม่มีค่อยใช้ชื่อจาก OTA
UPDATE f
SET NewName = CASE
                  WHEN f.Kind = N'INTL'
                      THEN COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(c.Name, N''))), N''),
                                    NULLIF(f.OtaGuestName, N''),
                                    f.NewKey)
                  WHEN f.OtaGuestName <> N'' THEN f.OtaGuestName
                  ELSE ISNULL(c.Name, f.NewKey)
              END
FROM #Fix f
LEFT JOIN Customer c ON c.MobilePhone = f.OldPhone;

UPDATE #Fix SET NewName = NewKey WHERE ISNULL(NewName, N'') = N'';

-- ── รายงานก่อนแก้ ───────────────────────────────────────────────────────────
DECLARE @n INT = (SELECT COUNT(*) FROM #Fix);
DECLARE @nPin INT  = (SELECT COUNT(*) FROM #Fix WHERE Kind = N'PIN');
DECLARE @nIntl INT = (SELECT COUNT(*) FROM #Fix WHERE Kind = N'INTL');
DECLARE @nFake INT = (SELECT COUNT(*) FROM #Fix WHERE Kind = N'FAKE');
PRINT N'ใบจองที่ต้องแก้เบอร์: ' + CAST(@n AS NVARCHAR(10)) + N' ใบ';
PRINT N'  • FAKE (เบอร์ค่าคงที่/ไม่ถูกรูปแบบ → OTA_ key): ' + CAST(@nFake AS NVARCHAR(10)) + N' ใบ';
PRINT N'  • PIN  (เลขในวงเล็บของเลขที่จอง → OTA_ key): ' + CAST(@nPin AS NVARCHAR(10)) + N' ใบ';
PRINT N'  • INTL (เบอร์ต่างประเทศที่ถูกเติม 0 → +รหัสประเทศ): ' + CAST(@nIntl AS NVARCHAR(10)) + N' ใบ';

SELECT TOP 200
    ReservationID, Kind, OldPhone, BookingId, NewKey, NewName
FROM #Fix
ORDER BY ReservationID DESC;

-- ใบเสร็จที่ยังชี้ลูกค้าแถวรวม (ไม่แก้ให้ — แจ้งจำนวนไว้ตัดสินใจ)
IF OBJECT_ID('Account_Receipt') IS NOT NULL AND COL_LENGTH('Account_Receipt', 'Customer_ID') IS NOT NULL
BEGIN
    DECLARE @rc INT = (
        SELECT COUNT(*)
        FROM Account_Receipt ar
        JOIN Customer c ON c.ID = ar.Customer_ID
        WHERE c.MobilePhone IN (SELECT DISTINCT OldPhone FROM #Fix));
    IF @rc > 0
        PRINT N'⚠️ มีใบเสร็จ ' + CAST(@rc AS NVARCHAR(10))
            + N' ใบที่ยังผูกกับลูกค้าแถวรวมเดิม — สคริปต์นี้ไม่แก้ให้ (เป็นเอกสารบัญชีที่ออกไปแล้ว)';
END

IF @DryRun = 1
BEGIN
    PRINT N'DryRun = 1 → ยังไม่แก้อะไร ตั้งเป็น 0 แล้วรันใหม่เพื่อแก้จริง';
    RETURN;
END

-- ── 4) แก้จริง ──────────────────────────────────────────────────────────────
-- หนึ่ง key = หนึ่งแถวลูกค้า (กันกรณีมีใบจองมากกว่าหนึ่งใบที่ได้ key เดียวกัน)
IF OBJECT_ID('tempdb..#New') IS NOT NULL DROP TABLE #New;
SELECT NewKey,
       MAX(NewName)  AS NewName,
       MAX(OldPhone) AS OldPhone,
       MAX(Kind)     AS Kind
INTO #New
FROM #Fix
GROUP BY NewKey;

BEGIN TRY
    BEGIN TRAN;

    -- 4.1 สร้างแถว Customer ของ key ใหม่ (เฉพาะที่ยังไม่มี) — คัดลอกคอลัมน์เท่าที่ตารางมีจริง
    DECLARE @cols NVARCHAR(MAX) = N'', @vals NVARCHAR(MAX) = N'';
    IF COL_LENGTH('Customer', 'FullName')  IS NOT NULL SELECT @cols = @cols + N', [FullName]',  @vals = @vals + N', f.NewName';
    IF COL_LENGTH('Customer', 'ComeFrom')  IS NOT NULL SELECT @cols = @cols + N', [ComeFrom]',  @vals = @vals + N', N''OTA''';
    IF COL_LENGTH('Customer', 'NickName')  IS NOT NULL SELECT @cols = @cols + N', [NickName]',  @vals = @vals + N', N''''';
    IF COL_LENGTH('Customer', 'Remark')    IS NOT NULL SELECT @cols = @cols + N', [Remark]',    @vals = @vals
        + N', CASE f.Kind WHEN N''INTL'' THEN N''แก้เบอร์ต่างประเทศที่ถูกเติม 0 จาก '''
        + N' WHEN N''PIN'' THEN N''แยกจากเบอร์ที่เป็นเลขอ้างอิงการจอง '''
        + N' ELSE N''แยกจากเบอร์ค่าคงที่ '' END + f.OldPhone';
    IF COL_LENGTH('Customer', 'Address')   IS NOT NULL SELECT @cols = @cols + N', [Address]',   @vals = @vals + N', N''''';
    IF COL_LENGTH('Customer', 'Address1')  IS NOT NULL SELECT @cols = @cols + N', [Address1]',  @vals = @vals + N', N''''';
    IF COL_LENGTH('Customer', 'IDNumber')  IS NOT NULL SELECT @cols = @cols + N', [IDNumber]',  @vals = @vals + N', N''''';
    IF COL_LENGTH('Customer', 'Email')     IS NOT NULL SELECT @cols = @cols + N', [Email]',     @vals = @vals + N', N''''';
    IF COL_LENGTH('Customer', 'Branch_Number') IS NOT NULL SELECT @cols = @cols + N', [Branch_Number]', @vals = @vals + N', N''00000''';
    IF COL_LENGTH('Customer', 'Address_ID')    IS NOT NULL SELECT @cols = @cols + N', [Address_ID]',    @vals = @vals + N', 0';
    IF COL_LENGTH('Customer', 'Created_Date')  IS NOT NULL SELECT @cols = @cols + N', [Created_Date]',  @vals = @vals + N', GETDATE()';
    -- Customer_Type_ID: ใช้ค่าเดียวกับลูกค้าแถวรวมเดิม ถ้าไม่มีก็ค่าต่ำสุดที่มีอยู่ (สุดท้าย 1)
    IF COL_LENGTH('Customer', 'Customer_Type_ID') IS NOT NULL
        SELECT @cols = @cols + N', [Customer_Type_ID]',
            @vals = @vals + N', ISNULL((SELECT TOP 1 c2.Customer_Type_ID FROM Customer c2 WHERE c2.MobilePhone = f.OldPhone), '
                          + N'ISNULL((SELECT MIN(c3.Customer_Type_ID) FROM Customer c3), 1))';

    DECLARE @inserted INT = 0;
    DECLARE @sql NVARCHAR(MAX) = N'
        INSERT INTO Customer ([MobilePhone], [Name]' + @cols + N')
        SELECT f.NewKey, f.NewName' + @vals + N'
        FROM #New f
        WHERE NOT EXISTS (SELECT 1 FROM Customer c WHERE c.MobilePhone = f.NewKey);
        SET @out = @@ROWCOUNT;';
    EXEC sp_executesql @sql, N'@out INT OUTPUT', @out = @inserted OUTPUT;
    PRINT N'สร้างลูกค้าใหม่: ' + CAST(@inserted AS NVARCHAR(10)) + N' ราย';

    -- 4.2 ย้ายใบจองไปผูก key ใหม่
    UPDATE r
    SET r.Customer_MobilePhone = f.NewKey
    FROM Reservation r
    JOIN #Fix f ON f.ReservationID = r.ID;
    PRINT N'ย้ายใบจอง: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' ใบ';

    -- 4.3 ชื่อลูกค้าของ OTA_ key ใหม่ต้องตรงกับชื่อผู้เข้าพักของใบนั้นเสมอ
    --     (ไม่แตะ INTL: key '+…' เป็นเบอร์จริงของแขก — แถวลูกค้าที่มีอยู่แล้วคงชื่อเดิม)
    UPDATE c
    SET c.Name = f.NewName
    FROM Customer c
    JOIN #New f ON f.NewKey = c.MobilePhone
    WHERE f.Kind <> N'INTL'
      AND ISNULL(c.Name, N'') <> f.NewName;
    PRINT N'ปรับชื่อลูกค้าให้ตรงใบจอง: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' ราย';

    COMMIT;
    PRINT N'✅ เสร็จสิ้น';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT N'❌ ล้มเหลว: ' + ERROR_MESSAGE();
    THROW;
END CATCH

-- ── 5) บันทึกค่าคงที่ที่เจอไว้ในตั้งค่า เผื่อ OTA เปลี่ยนค่าใหม่จะได้เทียบได้ ──
IF OBJECT_ID('Accounting_Integration_Config') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_DefaultPhone')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Email_Rsv_DefaultPhone', '',
            N'เบอร์สำรองเมื่ออีเมลไม่มีเบอร์และไม่มีเลขที่จอง (ปกติเว้นว่าง — ระบบใช้ OTA_<เลขจอง> แทน)');
GO
